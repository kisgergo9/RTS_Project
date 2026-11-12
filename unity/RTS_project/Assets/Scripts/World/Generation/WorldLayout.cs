using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Egy generált bázis helye, kezdeti tulajdonosa és (sárga bázisnál)
/// erőssége. A PlayerId csak Player tulajdonos esetén értelmezett, egyébként
/// 0 (a BaseController konvenciója szerint). Az erősség sárga bázisnál 1–10
/// (lásd: YellowBaseStrength), egyébként 0; a 0 sárga bázisnál azt jelenti,
/// hogy az erősség ismeretlen (régi mentés).
/// </summary>
public readonly struct BaseLayout
{
    public int BaseId { get; }
    public Vector2 Position { get; }
    public BaseOwnerType OwnerType { get; }
    public int PlayerId { get; }
    public int Strength { get; }

    public BaseLayout(int baseId, Vector2 position, BaseOwnerType ownerType, int playerId, int strength = 0)
    {
        if (baseId <= 0)
            throw new ArgumentOutOfRangeException(nameof(baseId),
                "A bázisazonosítónak pozitívnak kell lennie.");

        if (strength < 0 || strength > YellowBaseStrength.Max)
            throw new ArgumentOutOfRangeException(nameof(strength),
                $"Az erősségnek a [0; {YellowBaseStrength.Max}] tartományba kell esnie.");

        if (ownerType != BaseOwnerType.Yellow && strength != 0)
            throw new ArgumentException("Erőssége csak sárga bázisnak lehet.", nameof(strength));

        BaseId = baseId;
        Position = position;
        OwnerType = ownerType;
        PlayerId = ownerType == BaseOwnerType.Player ? playerId : 0;
        Strength = strength;
    }

    public BaseLayout WithStrength(int strength)
    {
        return new BaseLayout(BaseId, Position, OwnerType, PlayerId, strength);
    }

    public override string ToString()
    {
        return OwnerType == BaseOwnerType.Player
            ? $"Base {BaseId} @ {Position} (Player {PlayerId})"
            : $"Base {BaseId} @ {Position} ({OwnerType}, strength {Strength})";
    }
}

/// <summary>
/// Egy elhelyezett tereptárgy (akadály vagy dekoráció): típus, grafikai
/// változat, pozíció és lépték. Az alapterületet a típus határozza meg.
/// </summary>
public readonly struct TerrainFeatureLayout
{
    public string TypeId { get; }
    public int Variant { get; }
    public Vector2 Position { get; }
    public float Scale { get; }

    public TerrainFeatureLayout(string typeId, int variant, Vector2 position, float scale)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            throw new ArgumentException("A tereptárgy típusa nem lehet üres.", nameof(typeId));

        if (variant < 0)
            throw new ArgumentOutOfRangeException(nameof(variant));

        if (!(scale > 0f))
            throw new ArgumentOutOfRangeException(nameof(scale));

        TypeId = typeId;
        Variant = variant;
        Position = position;
        Scale = scale;
    }

    public override string ToString()
    {
        return $"{TypeId}#{Variant} @ {Position} x{Scale:0.##}";
    }
}

/// <summary>
/// Egy sárga bázis előtti védőegység: melyik bázist védi, milyen típusú,
/// hányadik csapatba tartozik, és hol áll a csapata (a csapat vezetőjének
/// helye, világkoordinátában). Egy csapat a játékban egy UnitGroup; a
/// többi tag a vezető köré, az alakzat szerint kerül.
/// </summary>
public readonly struct DefenderLayout
{
    public int BaseId { get; }
    public string UnitTypeId { get; }
    public int SquadIndex { get; }
    public Vector2 Position { get; }

    public DefenderLayout(int baseId, string unitTypeId, int squadIndex, Vector2 position)
    {
        if (baseId <= 0)
            throw new ArgumentOutOfRangeException(nameof(baseId));

        if (string.IsNullOrWhiteSpace(unitTypeId))
            throw new ArgumentException("A védőegység típusa nem lehet üres.", nameof(unitTypeId));

        if (squadIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(squadIndex));

        BaseId = baseId;
        UnitTypeId = unitTypeId;
        SquadIndex = squadIndex;
        Position = position;
    }

    public override string ToString()
    {
        return $"{UnitTypeId} @ base {BaseId}, squad {SquadIndex}";
    }
}

/// <summary>
/// A világgenerálás teljes eredménye. A host ezt építi fel, és ezt küldi el
/// a klienseknek is (a kliensek nem generálnak újra). A seed és a kérés a
/// mentéshez és a reprodukálhatósághoz tartozik.
/// </summary>
public sealed class WorldLayout
{
    private readonly BaseLayout[] _bases;
    private readonly TerrainFeatureLayout[] _obstacles;
    private readonly TerrainFeatureLayout[] _decorations;
    private readonly DefenderLayout[] _defenders;

    public int GeneratorVersion { get; }
    public WorldGenerationRequest Request { get; }
    public WorldBounds Bounds { get; }
    public IReadOnlyList<BaseLayout> Bases => _bases;
    public IReadOnlyList<TerrainFeatureLayout> Obstacles => _obstacles;

    /// <summary>
    /// Dekoratív, mozgást nem akadályozó elemek (pl. pálmafák).
    /// </summary>
    public IReadOnlyList<TerrainFeatureLayout> Decorations => _decorations;

    /// <summary>
    /// A sárga bázisok előtti védőegységek kezdeti elhelyezése. Létrehozásuk
    /// után közönséges (sárga tulajdonú) egységek, ezért a világ pillanatképe
    /// és hálózati üzenete nem tartalmazza őket; mentésük és szinkronizálásuk
    /// az egységekével együtt történik.
    /// </summary>
    public IReadOnlyList<DefenderLayout> Defenders => _defenders;

    public int Seed => Request.Seed;
    public int WorldSize => Request.WorldSize;

    public WorldLayout(
        int generatorVersion,
        WorldGenerationRequest request,
        IEnumerable<BaseLayout> bases,
        IEnumerable<TerrainFeatureLayout> obstacles = null,
        IEnumerable<TerrainFeatureLayout> decorations = null,
        IEnumerable<DefenderLayout> defenders = null)
    {
        if (bases == null)
            throw new ArgumentNullException(nameof(bases));

        GeneratorVersion = generatorVersion;
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Bounds = new WorldBounds(request.WorldSize);
        _bases = new List<BaseLayout>(bases).ToArray();
        _obstacles = obstacles == null
            ? Array.Empty<TerrainFeatureLayout>()
            : new List<TerrainFeatureLayout>(obstacles).ToArray();
        _decorations = decorations == null
            ? Array.Empty<TerrainFeatureLayout>()
            : new List<TerrainFeatureLayout>(decorations).ToArray();
        _defenders = defenders == null
            ? Array.Empty<DefenderLayout>()
            : new List<DefenderLayout>(defenders).ToArray();
    }
}
