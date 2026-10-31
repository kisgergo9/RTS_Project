using System;
using Unity.Netcode;

/// <summary>
/// A host által generált világ hálózati üzenete (Netcode for GameObjects).
/// A host a mérkőzés indításakor ezt küldi el minden kliensnek; a kliens
/// a WorldLayoutSnapshot.FromState segítségével visszaállítja a layoutot,
/// és a WorldSceneBuilder-rel felépíti a jelenetet. A kliens nem generál.
///
/// Mérete 150 bázis esetén kb. 3 KB, ezért megbízható (fragmentált)
/// csatornán kell küldeni. A tényleges küldést a hálózati session
/// elkészülte után kell bekötni.
/// </summary>
public struct WorldGenerationMessage : INetworkSerializable
{
    // Felső korlát a beolvasott elemszámokra, hogy egy hibás üzenet ne
    // okozhasson túlzott memóriafoglalást.
    public const int MaxEntries = 4096;

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

        int playerCount = serializer.IsReader ? 0 : State.playerIds.Length;
        serializer.SerializeValue(ref playerCount);
        ValidateCount(playerCount);

        if (serializer.IsReader)
            State.playerIds = new int[playerCount];

        for (int i = 0; i < playerCount; i++)
            serializer.SerializeValue(ref State.playerIds[i]);

        int baseCount = serializer.IsReader ? 0 : State.bases.Length;
        serializer.SerializeValue(ref baseCount);
        ValidateCount(baseCount);

        if (serializer.IsReader)
            State.bases = new BasePlacementState[baseCount];

        for (int i = 0; i < baseCount; i++)
        {
            BasePlacementState placement = State.bases[i];
            int ownerType = (int)placement.ownerType;

            serializer.SerializeValue(ref placement.baseId);
            serializer.SerializeValue(ref placement.x);
            serializer.SerializeValue(ref placement.y);
            serializer.SerializeValue(ref ownerType);
            serializer.SerializeValue(ref placement.playerId);

            placement.ownerType = (BaseOwnerType)ownerType;
            State.bases[i] = placement;
        }
    }

    private static void ValidateCount(int count)
    {
        if (count < 0 || count > MaxEntries)
            throw new InvalidOperationException(
                $"Érvénytelen elemszám a világüzenetben: {count}");
    }
}
