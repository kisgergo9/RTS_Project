using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A procedurális világgenerálás szerkeszthető beállításai.
/// Az alapértékek javaslatok, nem véglegesített játékszabályok.
/// A Base prefab mérete kb. 12 x 4,2 egység, a kijáratai legfeljebb kb.
/// 5,5 egységre, a tornyok felderítési területe 18 x 10 egységre terjed ki.
/// </summary>
[CreateAssetMenu(
    fileName = "WorldGenerationSettings",
    menuName = "Game/Configuration/World Generation Settings")]
public sealed class WorldGenerationSettings : ScriptableObject
{
    [Header("Players")]
    [SerializeField, Min(1)]
    private int minPlayerCount = 1;

    [SerializeField, Min(1)]
    private int maxPlayerCount = 10;

    [Header("Bases")]
    [Tooltip("Az összes bázis (játékos + sárga) maximális száma a legnagyobb világban.")]
    [SerializeField, Min(2)]
    private int maxBaseCount = 150;

    [Tooltip("Két bázis középpontja közötti minimális távolság.")]
    [SerializeField, Min(0.1f)]
    private float minBaseDistance = 24f;

    [Tooltip("A bázis körüli szabad terület sugara (megjelenési pontok, APC-foglalási terület). " +
             "A bázisok középpontja legalább ennyire van a pálya szélétől.")]
    [SerializeField, Min(0.1f)]
    private float baseClearRadius = 9f;

    [Tooltip("A sárga bázisok abszolút minimuma, játékosszámtól függetlenül.")]
    [SerializeField, Min(1)]
    private int minYellowBaseCount = 1;

    [Tooltip("Játékosonként legalább ennyi sárga bázis (felfelé kerekítve).")]
    [SerializeField, Min(0f)]
    private float yellowBasesPerPlayer = 1f;

    [Header("Obstacles (reserved space, not placed yet)")]
    [SerializeField, Min(0)]
    private int minObstacleCount = 0;

    [SerializeField, Min(0)]
    private int maxObstacleCount = 50;

    [Tooltip("Egy terepakadály helyigényének sugara.")]
    [SerializeField, Min(0.1f)]
    private float obstacleClearRadius = 8f;

    [Header("World Size")]
    [Tooltip("A négyzetes világ oldalhosszának abszolút minimuma.")]
    [SerializeField, Min(1)]
    private int absoluteMinWorldSize = 80;

    [Tooltip("A számított minimális és maximális világméretet ennek többszörösére kerekítjük felfelé.")]
    [SerializeField, Min(1)]
    private int worldSizeStep = 10;

    [Tooltip("A használható terület legfeljebb ekkora hányadát foglalják el a bázisok és akadályok. " +
             "Kisebb érték tágasabb világot és megbízhatóbb elhelyezést ad.")]
    [SerializeField, Range(0.05f, 0.5f)]
    private float targetFillRatio = 0.3f;

    [Header("Placement")]
    [SerializeField, Min(1)]
    private int maxPlacementAttemptsPerBase = 200;

    [SerializeField, Min(1)]
    private int maxGenerationAttempts = 10;

    [Header("Starting Bases")]
    [Tooltip("A kezdőbázisok köre sugarának aránya a játszható fél-szélességhez képest. " +
             "1 = a kezdőbázisok a pálya széle mentén, kisebb érték = közelebb a középhez.")]
    [SerializeField, Range(0.1f, 1f)]
    private float playerRingRadiusFraction = 0.7f;

    [Tooltip("A kezdőbázisok legfeljebb ennyivel térhetnek el a körön kijelölt helyüktől.")]
    [SerializeField, Min(0f)]
    private float startPositionJitter = 2f;

    [Header("Terrain")]
    [Tooltip("Akadálytípusok (hegy, szakadék, oázis). Ha nincs megadva akadály- és " +
             "dekorációtípus, a generátor nem helyez el tereptárgyakat.")]
    [SerializeField]
    private TerrainFeatureDefinition[] obstacleTypes = Array.Empty<TerrainFeatureDefinition>();

    [SerializeField]
    private TerrainFeatureDefinition[] decorationTypes = Array.Empty<TerrainFeatureDefinition>();

    [Tooltip("A bázis mozgást tiltó területe a középponthoz viszonyítva " +
             "(a Base prefab Blocker sokszöge).")]
    [SerializeField]
    private Vector2[] baseFootprint =
    {
        new Vector2(-0.04f, 1.79f), new Vector2(-2.22f, 1.70f), new Vector2(-3.91f, 1.24f),
        new Vector2(-4.55f, 1.03f), new Vector2(-5.06f, 0.74f), new Vector2(-5.24f, 0.28f),
        new Vector2(-4.97f, -0.56f), new Vector2(-4.48f, -1.09f), new Vector2(-3.67f, -1.62f),
        new Vector2(-2.84f, -1.84f), new Vector2(-0.67f, -1.87f), new Vector2(1.37f, -1.85f),
        new Vector2(3.13f, -1.54f), new Vector2(5.11f, -0.52f), new Vector2(5.07f, 0.34f),
        new Vector2(4.24f, 0.97f), new Vector2(2.56f, 1.61f)
    };

    [Tooltip("A bázis kötelezően szabadon és elérhetően tartandó pontjai a középponthoz " +
             "viszonyítva: GroundExit, APCExit, CaptureZone.")]
    [SerializeField]
    private Vector2[] baseAccessPoints =
    {
        new Vector2(-5.47f, -1.19f),
        new Vector2(-3.98f, -1.97f),
        new Vector2(-3.83f, -1.80f)
    };

    [Tooltip("Az akadály és a bázis szabad területének széle közötti minimális távolság.")]
    [SerializeField, Min(0f)]
    private float minObstacleBaseDistance = 2f;

    [Tooltip("Két akadály közötti minimális távolság.")]
    [SerializeField, Min(0f)]
    private float minObstacleSpacing = 3f;

    [Tooltip("A legkeskenyebb átjáró, amelyet az elérhetőségi ellenőrzés még átjárhatónak tekint.")]
    [SerializeField, Min(0.1f)]
    private float minPassageWidth = 4f;

    [Tooltip("A járhatósági rács cellamérete. Legfeljebb a minimális átjáró-szélesség lehet.")]
    [SerializeField, Min(0.1f)]
    private float walkabilityCellSize = 1f;

    [SerializeField, Min(1)]
    private int maxPlacementAttemptsPerObstacle = 100;

    [Tooltip("Ha az akadályok nem helyezhetők el érvényesen, ennyiszer generálja újra őket " +
             "új, a seedből származtatott sorozattal.")]
    [SerializeField, Min(1)]
    private int maxObstacleGenerationAttempts = 5;

    [Tooltip("Dekorációk száma 10 000 négyzetegységnyi pályaterületenként.")]
    [SerializeField, Min(0f)]
    private float decorationsPer10000Area = 6f;

    public IReadOnlyList<TerrainFeatureDefinition> ObstacleDefinitions => obstacleTypes;
    public IReadOnlyList<TerrainFeatureDefinition> DecorationDefinitions => decorationTypes;

    public WorldGenerationConfig ToConfig()
    {
        return new WorldGenerationConfig(
            minPlayerCount,
            maxPlayerCount,
            maxBaseCount,
            minBaseDistance,
            baseClearRadius,
            minYellowBaseCount,
            yellowBasesPerPlayer,
            minObstacleCount,
            maxObstacleCount,
            obstacleClearRadius,
            absoluteMinWorldSize,
            worldSizeStep,
            targetFillRatio,
            maxPlacementAttemptsPerBase,
            maxGenerationAttempts,
            playerRingRadiusFraction,
            startPositionJitter,
            CreateTerrainConfig());
    }

    private TerrainGenerationConfig CreateTerrainConfig()
    {
        if ((obstacleTypes == null || obstacleTypes.Length == 0) &&
            (decorationTypes == null || decorationTypes.Length == 0))
        {
            return null;
        }

        return new TerrainGenerationConfig(
            ToTypes(obstacleTypes),
            ToTypes(decorationTypes),
            new Footprint(baseFootprint),
            baseAccessPoints,
            minObstacleBaseDistance,
            minObstacleSpacing,
            minPassageWidth,
            walkabilityCellSize,
            maxPlacementAttemptsPerObstacle,
            maxObstacleGenerationAttempts,
            decorationsPer10000Area);
    }

    private static List<TerrainFeatureType> ToTypes(TerrainFeatureDefinition[] definitions)
    {
        List<TerrainFeatureType> types = new List<TerrainFeatureType>();

        if (definitions == null)
            return types;

        foreach (TerrainFeatureDefinition definition in definitions)
        {
            if (definition == null)
                throw new InvalidOperationException(
                    "A WorldGenerationSettings tereptárgy-listájában üres elem van.");

            types.Add(definition.ToType());
        }

        return types;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (maxPlayerCount < minPlayerCount)
            maxPlayerCount = minPlayerCount;

        if (maxObstacleCount < minObstacleCount)
            maxObstacleCount = minObstacleCount;
    }
#endif
}
