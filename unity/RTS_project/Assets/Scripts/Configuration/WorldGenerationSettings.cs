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

    [Tooltip("Akadályok legnagyobb száma 10 000 négyzetegységnyi használható pályaterületenként " +
             "(maxObstacleCount felső korláttal). 0 = a bázisokkal arányos elosztás.")]
    [SerializeField, Min(0f)]
    private float obstaclesPer10000Area = 8f;

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
    private float targetFillRatio = 0.34f;

    [Header("Recommended (lobby defaults)")]
    [Tooltip("Az ajánlott világméret a legkisebb játékosszámnál. A legnagyobb játékosszámnál " +
             "a maximális világméret az ajánlott, közte egyenletesen nő. " +
             "0 = a legkisebb játékosszám minimális világmérete.")]
    [SerializeField, Min(0)]
    private int recommendedWorldSizeAtMinPlayers = 120;

    [Tooltip("Az ajánlott akadályszám a legkisebb ajánlott világban. A maximális világméretnél " +
             "maxObstacleCount, közte a világmérettel egyenletesen nő.")]
    [SerializeField, Min(0)]
    private int recommendedObstacleCountAtMinPlayers = 8;

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

    [Header("Yellow Bases")]
    [Tooltip("A sárga bázisok erősségi (1–10) sorrendjét keverő zaj. 0 = szigorúan a kezdőbázisoktól " +
             "mért távolság szerint (a közeliek a leggyengébbek), nagyobb érték = véletlenszerűbb. " +
             "0,2 mellett a kezdőbázisokhoz legközelebbi sárga bázis erőssége legfeljebb 5.")]
    [SerializeField, Range(0f, 1f)]
    private float yellowStrengthJitter = 0.2f;

    [Tooltip("A leggyengébb (1-es erősségű) sárga bázis turretjeinek száma.")]
    [SerializeField, Min(0)]
    private int minYellowTurretCount = 2;

    [Tooltip("A legerősebb (10-es erősségű) sárga bázis turretjeinek száma. Közte az erősséggel " +
             "egyenletesen nő. A Base prefabon 12 turret van.")]
    [SerializeField, Min(0)]
    private int maxYellowTurretCount = 12;

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
    private float minObstacleSpacing = 4f;

    [Tooltip("A legkeskenyebb átjáró, amelyet az elérhetőségi ellenőrzés még átjárhatónak tekint.")]
    [SerializeField, Min(0.1f)]
    private float minPassageWidth = 4f;

    [Tooltip("A járhatósági rács cellamérete. Legfeljebb a minimális átjáró-szélesség lehet.")]
    [SerializeField, Min(0.1f)]
    private float walkabilityCellSize = 1f;

    [Tooltip("Egy akadály vagy dekoráció elhelyezésére tett próbálkozások száma.")]
    [SerializeField, Min(1)]
    private int maxPlacementAttemptsPerObstacle = 100;

    [Tooltip("Egy akadály ennyi érvényes jelölt hely közül a legtávolabbit választja a többi " +
             "akadálytól, a bázisoktól és a pálya szélétől, így egyenletesen oszlanak el. 1 = véletlen hely.")]
    [SerializeField, Min(1)]
    private int obstacleCandidateCount = 20;

    [Tooltip("Ha az akadályok nem helyezhetők el érvényesen, ennyiszer generálja újra őket " +
             "új, a seedből származtatott sorozattal.")]
    [SerializeField, Min(1)]
    private int maxObstacleGenerationAttempts = 5;

    [Tooltip("Dekorációk száma 10 000 négyzetegységnyi pályaterületenként.")]
    [SerializeField, Min(0f)]
    private float decorationsPer10000Area = 26f;

    [Tooltip("A dekorációk legnagyobb száma pályától függetlenül, hogy a világ hálózati " +
             "üzenete a Unity Transport alapértelmezett méretkorlátja alatt maradjon. 0 = nincs korlát.")]
    [SerializeField, Min(0)]
    private int maxDecorationCount = 350;

    [Tooltip("A szétszórt dekorációk ennyi érvényes jelölt hely közül a legtávolabbit választják, " +
             "így egyenletesen oszlanak el. 1 = véletlen hely.")]
    [SerializeField, Min(1)]
    private int decorationCandidateCount = 10;

    [Tooltip("A dekorációk ekkora hányada kerül ligetként a vonzó akadályok (pl. oázis) köré.")]
    [SerializeField, Range(0f, 1f)]
    private float decorationGroveFraction = 0.5f;

    [Tooltip("A liget dekorációi legfeljebb ennyire lehetnek a vonzó akadály szélétől.")]
    [SerializeField, Min(0.1f)]
    private float decorationGroveRadius = 8f;

    [Header("Yellow Defenders")]
    [Tooltip("A sárga bázisok előtti védőcsapatokban szerepelhető egységek és nehézségi értékük. " +
             "Ha üres, nincsenek védőcsapatok.")]
    [SerializeField]
    private DefenderUnitEntry[] defenderUnits = Array.Empty<DefenderUnitEntry>();

    [Tooltip("Az 1-es erősségű (leggyengébb) sárga bázis összes védőpontja.")]
    [SerializeField, Min(1)]
    private int minBaseDefenderPoints = 15;

    [Tooltip("A 10-es erősségű (legerősebb) sárga bázis összes védőpontja. Közte az erősséggel " +
             "egyenletesen nő. A pontok véletlenszerűen oszlanak meg a csapatok között.")]
    [SerializeField, Min(1)]
    private int maxBaseDefenderPoints = 60;

    [Tooltip("Egy csapat legkisebb pontja (legalább a legolcsóbb egység ára, így nincs üres csapat).")]
    [SerializeField, Min(1)]
    private int minSquadPoints = 5;

    [Tooltip("A sárga bázisok ekkora hányada kap védőcsapatokat.")]
    [SerializeField, Range(0f, 1f)]
    private float defendedYellowBaseFraction = 1f;

    [Tooltip("A csapatok helye (a csapat vezetőjéé) a bázis középpontjához viszonyítva: balra, " +
             "balra lent és középen lent, a bázis kijáratait és az APC-foglalási területet szabadon hagyva. " +
             "Egy bázisnak legfeljebb ennyi csapata van; a többi tag az alakzat szerint a vezető köré kerül.")]
    [SerializeField]
    private Vector2[] squadAnchors =
    {
        new Vector2(-7.5f, 0.5f),
        new Vector2(-7.5f, -3.5f),
        new Vector2(-1.0f, -4.5f)
    };

    public IReadOnlyList<DefenderUnitEntry> DefenderUnits => defenderUnits;

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
            CreateTerrainConfig(),
            obstaclesPer10000Area,
            CreateDefenderConfig(),
            recommendedWorldSizeAtMinPlayers,
            recommendedObstacleCountAtMinPlayers,
            yellowStrengthJitter,
            minYellowTurretCount,
            maxYellowTurretCount);
    }

    private YellowDefenderConfig CreateDefenderConfig()
    {
        if (defenderUnits == null || defenderUnits.Length == 0)
            return null;

        List<DefenderUnitType> types = new List<DefenderUnitType>(defenderUnits.Length);

        foreach (DefenderUnitEntry entry in defenderUnits)
        {
            if (entry == null)
                throw new InvalidOperationException(
                    "A WorldGenerationSettings védőegység-listájában üres elem van.");

            types.Add(entry.ToType());
        }

        return new YellowDefenderConfig(
            types,
            minBaseDefenderPoints,
            maxBaseDefenderPoints,
            minSquadPoints,
            defendedYellowBaseFraction,
            squadAnchors);
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
            decorationsPer10000Area,
            decorationGroveFraction,
            decorationGroveRadius,
            obstacleCandidateCount,
            decorationCandidateCount,
            maxDecorationCount);
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

        if (recommendedObstacleCountAtMinPlayers > maxObstacleCount)
            recommendedObstacleCountAtMinPlayers = maxObstacleCount;

        if (maxYellowTurretCount < minYellowTurretCount)
            maxYellowTurretCount = minYellowTurretCount;

        if (maxBaseDefenderPoints < minBaseDefenderPoints)
            maxBaseDefenderPoints = minBaseDefenderPoints;
    }
#endif
}
