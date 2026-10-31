using System;

/// <summary>
/// A világgenerálás Unity-független, megváltoztathatatlan konfigurációja.
/// A WorldGenerationSettings ScriptableObject ebből készít példányt, így a
/// generátor és a méretezési szabályok ScriptableObject nélkül is tesztelhetők.
/// Minden távolság és méret világegységben értendő.
/// </summary>
public sealed class WorldGenerationConfig
{
    // Játékosok
    public int MinPlayerCount { get; }
    public int MaxPlayerCount { get; }

    // Bázisok
    public int MaxBaseCount { get; }
    public float MinBaseDistance { get; }
    public float BaseClearRadius { get; }
    public int MinYellowBaseCount { get; }
    public float YellowBasesPerPlayer { get; }

    // Terepakadályok (a generátor még nem helyezi el őket,
    // de a világméret számításában már helyet foglalnak)
    public int MinObstacleCount { get; }
    public int MaxObstacleCount { get; }
    public float ObstacleClearRadius { get; }

    // Világméret
    public int AbsoluteMinWorldSize { get; }
    public int WorldSizeStep { get; }
    public float TargetFillRatio { get; }

    // Elhelyezés
    public int MaxPlacementAttemptsPerBase { get; }
    public int MaxGenerationAttempts { get; }

    public WorldGenerationConfig(
        int minPlayerCount,
        int maxPlayerCount,
        int maxBaseCount,
        float minBaseDistance,
        float baseClearRadius,
        int minYellowBaseCount,
        float yellowBasesPerPlayer,
        int minObstacleCount,
        int maxObstacleCount,
        float obstacleClearRadius,
        int absoluteMinWorldSize,
        int worldSizeStep,
        float targetFillRatio,
        int maxPlacementAttemptsPerBase,
        int maxGenerationAttempts)
    {
        if (minPlayerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(minPlayerCount),
                "Legalább egy játékos szükséges.");

        if (maxPlayerCount < minPlayerCount)
            throw new ArgumentOutOfRangeException(nameof(maxPlayerCount),
                "A maximális játékosszám nem lehet kisebb a minimálisnál.");

        if (minYellowBaseCount < 1)
            throw new ArgumentOutOfRangeException(nameof(minYellowBaseCount),
                "Legalább egy sárga bázis szükséges.");

        if (yellowBasesPerPlayer < 0f)
            throw new ArgumentOutOfRangeException(nameof(yellowBasesPerPlayer),
                "A játékosonkénti sárga bázisszám nem lehet negatív.");

        if (maxBaseCount < maxPlayerCount + minYellowBaseCount)
            throw new ArgumentOutOfRangeException(nameof(maxBaseCount),
                "A maximális bázisszámnak el kell érnie a maximális játékosszám " +
                "és a minimális sárga bázisszám összegét.");

        if (minBaseDistance <= 0f)
            throw new ArgumentOutOfRangeException(nameof(minBaseDistance),
                "A bázisok közötti minimális távolságnak pozitívnak kell lennie.");

        if (baseClearRadius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(baseClearRadius),
                "A bázis körüli szabad terület sugarának pozitívnak kell lennie.");

        if (minObstacleCount < 0)
            throw new ArgumentOutOfRangeException(nameof(minObstacleCount),
                "Az akadályok minimális száma nem lehet negatív.");

        if (maxObstacleCount < minObstacleCount)
            throw new ArgumentOutOfRangeException(nameof(maxObstacleCount),
                "Az akadályok maximális száma nem lehet kisebb a minimálisnál.");

        if (obstacleClearRadius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(obstacleClearRadius),
                "Az akadályok helyigényének sugara pozitív kell legyen.");

        if (worldSizeStep <= 0)
            throw new ArgumentOutOfRangeException(nameof(worldSizeStep),
                "A világméret lépésközének pozitívnak kell lennie.");

        if (absoluteMinWorldSize <= 2f * baseClearRadius)
            throw new ArgumentOutOfRangeException(nameof(absoluteMinWorldSize),
                "A legkisebb világnak nagyobbnak kell lennie egy bázis " +
                "szabad területénél.");

        // A véletlenszerű, egymást nem átfedő elhelyezés elméleti felső
        // határa körülbelül 0,547 (random sequential adsorption). Ennél
        // nagyobb kitöltés esetén a generálás nem tudna sikeresen lefutni.
        if (targetFillRatio <= 0f || targetFillRatio > 0.5f)
            throw new ArgumentOutOfRangeException(nameof(targetFillRatio),
                "A célzott kitöltési aránynak a (0; 0,5] tartományba kell esnie.");

        if (maxPlacementAttemptsPerBase < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPlacementAttemptsPerBase));

        if (maxGenerationAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxGenerationAttempts));

        MinPlayerCount = minPlayerCount;
        MaxPlayerCount = maxPlayerCount;
        MaxBaseCount = maxBaseCount;
        MinBaseDistance = minBaseDistance;
        BaseClearRadius = baseClearRadius;
        MinYellowBaseCount = minYellowBaseCount;
        YellowBasesPerPlayer = yellowBasesPerPlayer;
        MinObstacleCount = minObstacleCount;
        MaxObstacleCount = maxObstacleCount;
        ObstacleClearRadius = obstacleClearRadius;
        AbsoluteMinWorldSize = absoluteMinWorldSize;
        WorldSizeStep = worldSizeStep;
        TargetFillRatio = targetFillRatio;
        MaxPlacementAttemptsPerBase = maxPlacementAttemptsPerBase;
        MaxGenerationAttempts = maxGenerationAttempts;
    }
}
