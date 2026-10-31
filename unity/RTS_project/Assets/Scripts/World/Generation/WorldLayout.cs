using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Egy generált bázis helye és kezdeti tulajdonosa.
/// A PlayerId csak Player tulajdonos esetén értelmezett, egyébként 0
/// (a BaseController konvenciója szerint).
/// </summary>
public readonly struct BaseLayout
{
    public int BaseId { get; }
    public Vector2 Position { get; }
    public BaseOwnerType OwnerType { get; }
    public int PlayerId { get; }

    public BaseLayout(int baseId, Vector2 position, BaseOwnerType ownerType, int playerId)
    {
        if (baseId <= 0)
            throw new ArgumentOutOfRangeException(nameof(baseId),
                "A bázisazonosítónak pozitívnak kell lennie.");

        BaseId = baseId;
        Position = position;
        OwnerType = ownerType;
        PlayerId = ownerType == BaseOwnerType.Player ? playerId : 0;
    }

    public override string ToString()
    {
        return OwnerType == BaseOwnerType.Player
            ? $"Base {BaseId} @ {Position} (Player {PlayerId})"
            : $"Base {BaseId} @ {Position} ({OwnerType})";
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
/// A világgenerálás teljes eredménye. A host ezt építi fel, és ezt küldi el
/// a klienseknek is (a kliensek nem generálnak újra). A seed és a kérés a
/// mentéshez és a reprodukálhatósághoz tartozik.
/// </summary>
public sealed class WorldLayout
{
    private readonly BaseLayout[] _bases;
    private readonly TerrainFeatureLayout[] _obstacles;

    public int GeneratorVersion { get; }
    public WorldGenerationRequest Request { get; }
    public WorldBounds Bounds { get; }
    public IReadOnlyList<BaseLayout> Bases => _bases;
    public IReadOnlyList<TerrainFeatureLayout> Obstacles => _obstacles;

    public int Seed => Request.Seed;
    public int WorldSize => Request.WorldSize;

    public WorldLayout(
        int generatorVersion,
        WorldGenerationRequest request,
        IEnumerable<BaseLayout> bases,
        IEnumerable<TerrainFeatureLayout> obstacles = null)
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
    }
}
