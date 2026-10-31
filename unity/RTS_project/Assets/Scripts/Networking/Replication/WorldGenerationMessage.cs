using System;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// A host által generált világ hálózati üzenete (Netcode for GameObjects).
/// A host a mérkőzés indításakor ezt küldi el minden kliensnek; a kliens
/// a WorldLayoutSnapshot.FromState segítségével visszaállítja a layoutot,
/// és a WorldSceneBuilder-rel felépíti a jelenetet. A kliens nem generál.
///
/// Tömör formátum: a tereptárgy-típusazonosítók egyszer, egy táblázatban
/// szerepelnek, az egyes tárgyak csak a sorszámukra hivatkoznak; a kis
/// értékkészletű mezők (változat, tulajdonostípus, bázisazonosító) 1–2 bájtosak.
/// A csak megjelenítést szolgáló dekorációk pozíciója 16 bites, a pályához
/// viszonyított értékként (a legnagyobb pályán kb. 0,008 egység pontossággal),
/// léptéke 0,01-es lépésközzel utazik; a játékmenetet befolyásoló bázisok és
/// akadályok pontos értékkel. A bázis tulajdonosa a játékoslista 1 bájtos
/// sorszámaként utazik. A legnagyobb világ (150 bázis, 100 akadály,
/// 337 dekoráció) így a Unity Transport alapértelmezett 6144 bájtos
/// MaxPayloadSize értéke alatt marad. Sűrűbb dekoráció esetén a
/// MaxPayloadSize-t növelni kell.
///
/// Megbízható (fragmentált) csatornán kell küldeni. A tényleges küldést a
/// hálózati session elkészülte után kell bekötni.
/// </summary>
public struct WorldGenerationMessage : INetworkSerializable
{
    // Felső korlát a beolvasott elemszámokra, hogy egy hibás üzenet ne
    // okozhasson túlzott memóriafoglalást.
    public const int MaxEntries = 4096;

    // A tereptárgy-típusazonosítók legnagyobb hossza.
    public const int MaxTypeIdLength = 64;

    // A típustáblázat és a grafikai változatok bájtos indexelése miatt.
    public const int MaxTypeCount = byte.MaxValue + 1;
    public const int MaxVariant = byte.MaxValue;

    // A bázistulajdonos a játékoslista sorszámaként utazik; ez jelöli a
    // nem játékos (sárga vagy gazdátlan) tulajdonost.
    private const byte NoPlayerIndex = byte.MaxValue;
    public const int MaxPlayerCount = byte.MaxValue;

    // A dekorációk léptékének kódolása: lépték * 100, egy bájton (0,01–2,55).
    public const float DecorationScaleStep = 0.01f;
    public const float MaxDecorationScale = byte.MaxValue * DecorationScaleStep;

    public WorldGenerationState State;

    public WorldGenerationMessage(WorldGenerationState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        if (serializer.IsReader)
            State = new WorldGenerationState();

        serializer.SerializeValue(ref State.generatorVersion);
        serializer.SerializeValue(ref State.seed);
        serializer.SerializeValue(ref State.worldSize);
        serializer.SerializeValue(ref State.yellowBaseCount);
        serializer.SerializeValue(ref State.obstacleCount);

        SerializePlayerIds(serializer);
        SerializeBases(serializer);

        List<string> typeTable = SerializeTypeTable(serializer);

        State.obstacles = SerializeFeatures(serializer, State.obstacles, typeTable);
        State.decorations = SerializeDecorations(serializer, State.decorations, typeTable, State.worldSize);
    }

    private void SerializePlayerIds<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int count = serializer.IsReader ? 0 : State.playerIds.Length;
        serializer.SerializeValue(ref count);

        if (count < 0 || count > MaxPlayerCount)
            throw new InvalidOperationException(
                $"Érvénytelen játékosszám a világüzenetben: {count}");

        if (serializer.IsReader)
            State.playerIds = new int[count];

        for (int i = 0; i < count; i++)
            serializer.SerializeValue(ref State.playerIds[i]);
    }

    private void SerializeBases<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        int count = serializer.IsReader ? 0 : State.bases.Length;
        serializer.SerializeValue(ref count);
        ValidateCount(count);

        if (serializer.IsReader)
            State.bases = new BasePlacementState[count];

        for (int i = 0; i < count; i++)
        {
            BasePlacementState placement = State.bases[i];

            if (!serializer.IsReader && (placement.baseId < 0 || placement.baseId > ushort.MaxValue))
                throw new InvalidOperationException(
                    $"A bázisazonosító nem fér el a világüzenetben: {placement.baseId}");

            ushort baseId = (ushort)placement.baseId;
            byte ownerType = (byte)placement.ownerType;
            byte playerIndex = serializer.IsReader ? NoPlayerIndex : ToPlayerIndex(placement);

            serializer.SerializeValue(ref baseId);
            serializer.SerializeValue(ref placement.x);
            serializer.SerializeValue(ref placement.y);
            serializer.SerializeValue(ref ownerType);
            serializer.SerializeValue(ref playerIndex);

            placement.baseId = baseId;
            placement.ownerType = (BaseOwnerType)ownerType;

            if (serializer.IsReader)
                placement.playerId = FromPlayerIndex(placement.ownerType, playerIndex);

            State.bases[i] = placement;
        }
    }

    private byte ToPlayerIndex(BasePlacementState placement)
    {
        if (placement.ownerType != BaseOwnerType.Player)
            return NoPlayerIndex;

        int index = Array.IndexOf(State.playerIds, placement.playerId);

        if (index < 0)
            throw new InvalidOperationException(
                $"A(z) {placement.baseId}. bázis tulajdonosa nem résztvevő játékos: {placement.playerId}");

        return (byte)index;
    }

    private int FromPlayerIndex(BaseOwnerType ownerType, byte playerIndex)
    {
        if (ownerType != BaseOwnerType.Player)
            return 0;

        if (playerIndex >= State.playerIds.Length)
            throw new InvalidOperationException(
                $"Érvénytelen játékoshivatkozás a világüzenetben: {playerIndex}");

        return State.playerIds[playerIndex];
    }

    /// <summary>
    /// A tereptárgy-típusazonosítók táblázata. Íráskor az akadályokból és a
    /// dekorációkból gyűjti össze (első előfordulás szerinti sorrendben).
    /// </summary>
    private List<string> SerializeTypeTable<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        List<string> table = new List<string>();

        if (!serializer.IsReader)
        {
            CollectTypes(State.obstacles, table);
            CollectTypes(State.decorations, table);

            if (table.Count > MaxTypeCount)
                throw new InvalidOperationException(
                    $"Túl sok tereptárgy-típus a világüzenetben: {table.Count}");
        }

        int count = table.Count;
        serializer.SerializeValue(ref count);

        if (count < 0 || count > MaxTypeCount)
            throw new InvalidOperationException(
                $"Érvénytelen típusszám a világüzenetben: {count}");

        for (int i = 0; i < count; i++)
        {
            string typeId = serializer.IsReader ? null : table[i];

            if (!serializer.IsReader)
                ValidateTypeId(typeId);

            serializer.SerializeValue(ref typeId, true);

            if (serializer.IsReader)
            {
                ValidateTypeId(typeId);
                table.Add(typeId);
            }
        }

        return table;
    }

    private static void CollectTypes(TerrainFeatureState[] features, List<string> table)
    {
        if (features == null)
            return;

        foreach (TerrainFeatureState feature in features)
        {
            string typeId = feature.typeId ?? string.Empty;

            if (!table.Contains(typeId))
                table.Add(typeId);
        }
    }

    private static TerrainFeatureState[] SerializeFeatures<T>(
        BufferSerializer<T> serializer,
        TerrainFeatureState[] features,
        List<string> typeTable) where T : IReaderWriter
    {
        int count = serializer.IsReader ? 0 : (features?.Length ?? 0);
        serializer.SerializeValue(ref count);
        ValidateCount(count);

        if (serializer.IsReader)
            features = new TerrainFeatureState[count];

        for (int i = 0; i < count; i++)
        {
            TerrainFeatureState feature = features[i];

            if (!serializer.IsReader && (feature.variant < 0 || feature.variant > MaxVariant))
                throw new InvalidOperationException(
                    $"A grafikai változat nem fér el a világüzenetben: {feature.variant}");

            byte typeIndex = serializer.IsReader
                ? (byte)0
                : (byte)typeTable.IndexOf(feature.typeId ?? string.Empty);
            byte variant = (byte)feature.variant;

            serializer.SerializeValue(ref typeIndex);
            serializer.SerializeValue(ref variant);
            serializer.SerializeValue(ref feature.x);
            serializer.SerializeValue(ref feature.y);
            serializer.SerializeValue(ref feature.scale);

            if (serializer.IsReader)
            {
                if (typeIndex >= typeTable.Count)
                    throw new InvalidOperationException(
                        $"Érvénytelen típushivatkozás a világüzenetben: {typeIndex}");

                feature.typeId = typeTable[typeIndex];
                feature.variant = variant;
            }

            features[i] = feature;
        }

        return features ?? Array.Empty<TerrainFeatureState>();
    }

    /// <summary>
    /// A dekorációk tömör kódolása: típus- és változatsorszám 1-1 bájt, a pozíció
    /// tengelyenként 16 bites, a pályán belüli relatív érték, a lépték 1 bájt.
    /// Csak megjelenítést befolyásol, ezért a kerekítés megengedett.
    /// </summary>
    private static TerrainFeatureState[] SerializeDecorations<T>(
        BufferSerializer<T> serializer,
        TerrainFeatureState[] decorations,
        List<string> typeTable,
        int worldSize) where T : IReaderWriter
    {
        if (worldSize <= 0)
            throw new InvalidOperationException(
                $"Érvénytelen világméret a világüzenetben: {worldSize}");

        int count = serializer.IsReader ? 0 : (decorations?.Length ?? 0);
        serializer.SerializeValue(ref count);
        ValidateCount(count);

        if (serializer.IsReader)
            decorations = new TerrainFeatureState[count];

        float half = worldSize * 0.5f;

        for (int i = 0; i < count; i++)
        {
            TerrainFeatureState decoration = decorations[i];

            if (!serializer.IsReader)
            {
                if (decoration.variant < 0 || decoration.variant > MaxVariant)
                    throw new InvalidOperationException(
                        $"A grafikai változat nem fér el a világüzenetben: {decoration.variant}");

                if (!(decoration.scale >= DecorationScaleStep) || decoration.scale > MaxDecorationScale)
                    throw new InvalidOperationException(
                        $"A dekoráció léptéke nem fér el a világüzenetben: {decoration.scale}");
            }

            byte typeIndex = serializer.IsReader
                ? (byte)0
                : (byte)typeTable.IndexOf(decoration.typeId ?? string.Empty);
            byte variant = (byte)decoration.variant;
            ushort x = serializer.IsReader ? (ushort)0 : Quantize(decoration.x, half, worldSize);
            ushort y = serializer.IsReader ? (ushort)0 : Quantize(decoration.y, half, worldSize);
            byte scale = serializer.IsReader
                ? (byte)0
                : (byte)Math.Round(decoration.scale / DecorationScaleStep);

            serializer.SerializeValue(ref typeIndex);
            serializer.SerializeValue(ref variant);
            serializer.SerializeValue(ref x);
            serializer.SerializeValue(ref y);
            serializer.SerializeValue(ref scale);

            if (serializer.IsReader)
            {
                if (typeIndex >= typeTable.Count)
                    throw new InvalidOperationException(
                        $"Érvénytelen típushivatkozás a világüzenetben: {typeIndex}");

                if (scale == 0)
                    throw new InvalidOperationException("Nulla léptékű dekoráció a világüzenetben.");

                decoration.typeId = typeTable[typeIndex];
                decoration.variant = variant;
                decoration.x = Dequantize(x, half, worldSize);
                decoration.y = Dequantize(y, half, worldSize);
                decoration.scale = scale * DecorationScaleStep;
            }

            decorations[i] = decoration;
        }

        return decorations ?? Array.Empty<TerrainFeatureState>();
    }

    private static ushort Quantize(float value, float half, int worldSize)
    {
        double relative = (value + half) / worldSize;

        if (relative < 0.0 || relative > 1.0)
            throw new InvalidOperationException(
                $"A dekoráció a pályán kívül van: {value}");

        return (ushort)Math.Round(relative * ushort.MaxValue);
    }

    private static float Dequantize(ushort value, float half, int worldSize)
    {
        return (float)((double)value / ushort.MaxValue * worldSize - half);
    }

    private static void ValidateCount(int count)
    {
        if (count < 0 || count > MaxEntries)
            throw new InvalidOperationException(
                $"Érvénytelen elemszám a világüzenetben: {count}");
    }

    private static void ValidateTypeId(string typeId)
    {
        if (typeId == null)
            throw new InvalidOperationException("Hiányzó tereptárgy-azonosító a világüzenetben.");

        if (typeId.Length > MaxTypeIdLength)
            throw new InvalidOperationException(
                $"Túl hosszú tereptárgy-azonosító a világüzenetben: {typeId.Length} karakter");

        foreach (char character in typeId)
        {
            if (character > 127)
                throw new InvalidOperationException(
                    "A tereptárgy-azonosító csak ASCII karaktereket tartalmazhat.");
        }
    }
}
