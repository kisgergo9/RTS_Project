using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A tereptárgyak (akadályok és dekorációk) generálásának Unity-független,
/// megváltoztathatatlan konfigurációja. A WorldGenerationConfig opcionális
/// része: ha nincs megadva, a generátor nem helyez el tereptárgyakat.
/// Minden távolság világegységben értendő.
/// </summary>
public sealed class TerrainGenerationConfig
{
    private readonly TerrainFeatureType[] _obstacleTypes;
    private readonly TerrainFeatureType[] _decorationTypes;
    private readonly Vector2[] _baseAccessPoints;

    public IReadOnlyList<TerrainFeatureType> ObstacleTypes => _obstacleTypes;
    public IReadOnlyList<TerrainFeatureType> DecorationTypes => _decorationTypes;

    /// <summary>
    /// A bázis mozgást tiltó területe (a Base prefab Blocker sokszöge).
    /// </summary>
    public Footprint BaseFootprint { get; }

    /// <summary>
    /// A bázis kötelezően szabadon és elérhetően tartandó pontjai a bázis
    /// középpontjához viszonyítva (egységkijárat, APC-kijárat, foglalási terület).
    /// </summary>
    public IReadOnlyList<Vector2> BaseAccessPoints => _baseAccessPoints;

    /// <summary>
    /// Az akadály alapterülete és a bázis szabad területének (BaseClearRadius
    /// sugarú kör) széle közötti minimális távolság.
    /// </summary>
    public float MinObstacleBaseDistance { get; }

    /// <summary>
    /// Két akadály alapterülete közötti minimális távolság.
    /// </summary>
    public float MinObstacleSpacing { get; }

    /// <summary>
    /// A legkeskenyebb átjáró, amelyet a járhatósági ellenőrzés még átjárhatónak
    /// tekint. Az akadályokat ennek felével megnövelve jelöljük a rácson.
    /// </summary>
    public float MinPassageWidth { get; }

    public float WalkabilityCellSize { get; }

    /// <summary>
    /// Egy akadály vagy dekoráció elhelyezésére tett próbálkozások száma.
    /// </summary>
    public int MaxPlacementAttemptsPerObstacle { get; }
    public int MaxObstacleGenerationAttempts { get; }

    /// <summary>
    /// A dekorációk száma 10 000 négyzetegységnyi pályaterületenként.
    /// </summary>
    public float DecorationsPer10000Area { get; }

    /// <summary>
    /// A dekorációk ekkora hányada kerül ligetként egy AttractsDecorations
    /// jelölésű akadály (pl. oázis) köré, ha van ilyen a pályán. [0; 1]
    /// </summary>
    public float DecorationGroveFraction { get; }

    /// <summary>
    /// A liget dekorációi legfeljebb ennyire lehetnek a vonzó akadály szélétől.
    /// </summary>
    public float DecorationGroveRadius { get; }

    public TerrainGenerationConfig(
        IEnumerable<TerrainFeatureType> obstacleTypes,
        IEnumerable<TerrainFeatureType> decorationTypes,
        Footprint baseFootprint,
        IEnumerable<Vector2> baseAccessPoints,
        float minObstacleBaseDistance,
        float minObstacleSpacing,
        float minPassageWidth,
        float walkabilityCellSize,
        int maxPlacementAttemptsPerObstacle,
        int maxObstacleGenerationAttempts,
        float decorationsPer10000Area,
        float decorationGroveFraction = 0f,
        float decorationGroveRadius = 8f)
    {
        _obstacleTypes = ToArray(obstacleTypes, nameof(obstacleTypes));
        _decorationTypes = ToArray(decorationTypes, nameof(decorationTypes));
        _baseAccessPoints = baseAccessPoints == null
            ? throw new ArgumentNullException(nameof(baseAccessPoints))
            : new List<Vector2>(baseAccessPoints).ToArray();

        BaseFootprint = baseFootprint ?? throw new ArgumentNullException(nameof(baseFootprint));

        ValidateTypes(_obstacleTypes, true, nameof(obstacleTypes));
        ValidateTypes(_decorationTypes, false, nameof(decorationTypes));
        ValidateUniqueIds();

        if (_baseAccessPoints.Length == 0)
            throw new ArgumentException(
                "Legalább egy kötelező bázispont (kijárat) szükséges.", nameof(baseAccessPoints));

        foreach (Vector2 point in _baseAccessPoints)
        {
            if (BaseFootprint.Contains(point))
                throw new ArgumentException(
                    $"A bázispont ({point}) a bázis tiltott területén belül van, " +
                    "így sosem lehetne elérhető.", nameof(baseAccessPoints));
        }

        if (minObstacleBaseDistance < 0f)
            throw new ArgumentOutOfRangeException(nameof(minObstacleBaseDistance));

        if (minObstacleSpacing < 0f)
            throw new ArgumentOutOfRangeException(nameof(minObstacleSpacing));

        if (minPassageWidth <= 0f)
            throw new ArgumentOutOfRangeException(nameof(minPassageWidth),
                "A minimális átjáró-szélességnek pozitívnak kell lennie.");

        if (walkabilityCellSize <= 0f || walkabilityCellSize > minPassageWidth)
            throw new ArgumentOutOfRangeException(nameof(walkabilityCellSize),
                "A rács cellamérete pozitív és legfeljebb a minimális átjáró-szélesség lehet, " +
                "különben a rács nem tudná ábrázolni a legkeskenyebb átjárót.");

        if (maxPlacementAttemptsPerObstacle < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPlacementAttemptsPerObstacle));

        if (maxObstacleGenerationAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxObstacleGenerationAttempts));

        if (decorationsPer10000Area < 0f)
            throw new ArgumentOutOfRangeException(nameof(decorationsPer10000Area));

        if (decorationGroveFraction < 0f || decorationGroveFraction > 1f)
            throw new ArgumentOutOfRangeException(nameof(decorationGroveFraction),
                "A ligetbe kerülő dekorációk aránya a [0; 1] tartományba kell essen.");

        if (!(decorationGroveRadius > 0f))
            throw new ArgumentOutOfRangeException(nameof(decorationGroveRadius),
                "A liget sugarának pozitívnak kell lennie.");

        foreach (TerrainFeatureType type in _decorationTypes)
        {
            if (type.AttractsDecorations)
                throw new ArgumentException(
                    $"Dekoráció nem lehet liget középpontja: {type.Id}", nameof(decorationTypes));
        }

        MinObstacleBaseDistance = minObstacleBaseDistance;
        MinObstacleSpacing = minObstacleSpacing;
        MinPassageWidth = minPassageWidth;
        WalkabilityCellSize = walkabilityCellSize;
        MaxPlacementAttemptsPerObstacle = maxPlacementAttemptsPerObstacle;
        MaxObstacleGenerationAttempts = maxObstacleGenerationAttempts;
        DecorationsPer10000Area = decorationsPer10000Area;
        DecorationGroveFraction = decorationGroveFraction;
        DecorationGroveRadius = decorationGroveRadius;
    }

    public float MaxObstacleBoundingRadius
    {
        get
        {
            float max = 0f;

            foreach (TerrainFeatureType type in _obstacleTypes)
                max = Mathf.Max(max, type.MaxBoundingRadius);

            return max;
        }
    }

    private static TerrainFeatureType[] ToArray(IEnumerable<TerrainFeatureType> types, string name)
    {
        if (types == null)
            throw new ArgumentNullException(name);

        TerrainFeatureType[] array = new List<TerrainFeatureType>(types).ToArray();

        foreach (TerrainFeatureType type in array)
        {
            if (type == null)
                throw new ArgumentException("A tereptárgy-típusok között null elem van.", name);
        }

        return array;
    }

    private static void ValidateTypes(TerrainFeatureType[] types, bool blocksMovement, string name)
    {
        foreach (TerrainFeatureType type in types)
        {
            if (type.BlocksMovement != blocksMovement)
                throw new ArgumentException(
                    blocksMovement
                        ? $"Az akadálytípusnak el kell zárnia a mozgást: {type.Id}"
                        : $"A dekorációtípus nem zárhatja el a mozgást: {type.Id}",
                    name);
        }
    }

    private void ValidateUniqueIds()
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (TerrainFeatureType type in _obstacleTypes)
        {
            if (!ids.Add(type.Id))
                throw new ArgumentException($"Duplikált tereptárgy-azonosító: {type.Id}");
        }

        foreach (TerrainFeatureType type in _decorationTypes)
        {
            if (!ids.Add(type.Id))
                throw new ArgumentException($"Duplikált tereptárgy-azonosító: {type.Id}");
        }
    }
}
