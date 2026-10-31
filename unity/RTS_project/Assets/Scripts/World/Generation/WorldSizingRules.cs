using System;

/// <summary>
/// A világgenerálás méretezési szabályai. A generálási beállítások
/// kiválasztásának sorrendje:
///   1. játékosszám,
///   2. a játékosszámtól függő világméret-tartomány,
///   3. a világmérettől függő bázis- (sárga bázis) és akadályszám-tartomány.
///
/// Helyigény-modell: minden bázis egy MinBaseDistance átmérőjű, minden akadály
/// egy ObstacleClearRadius sugarú kört foglal el. A bázisok középpontja legalább
/// BaseClearRadius távolságra van a pálya szélétől, ezért a használható terület
/// (worldSize - 2 * BaseClearRadius)^2. Ennek legfeljebb TargetFillRatio
/// hányadát foglalhatják el az objektumok. A maximális világméret az, amelyben
/// a MaxBaseCount bázis és a MaxObstacleCount akadály ezzel a kitöltéssel
/// elfér; kisebb világban a bázisok és az akadályok helyigénye ugyanebben
/// az arányban osztozik a területen.
/// </summary>
public sealed class WorldSizingRules
{
    private const double Epsilon = 1e-6;

    private readonly WorldGenerationConfig _config;
    private readonly double _baseArea;
    private readonly double _obstacleArea;
    private readonly double _baseAreaShare;

    public int MinPlayerCount => _config.MinPlayerCount;
    public int MaxPlayerCount => _config.MaxPlayerCount;
    public int MaxWorldSize { get; }

    public WorldSizingRules(WorldGenerationConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        double baseRadius = config.MinBaseDistance * 0.5;
        _baseArea = Math.PI * baseRadius * baseRadius;
        _obstacleArea = Math.PI * config.ObstacleClearRadius * config.ObstacleClearRadius;

        double maxBaseDemand = config.MaxBaseCount * _baseArea;
        double maxObstacleDemand = config.MaxObstacleCount * _obstacleArea;
        _baseAreaShare = maxBaseDemand / (maxBaseDemand + maxObstacleDemand);

        MaxWorldSize = RoundUpToStep(Math.Max(
            config.AbsoluteMinWorldSize,
            GetSideLengthForBases(config.MaxBaseCount)));

        ValidatePlayerCountsFit();
    }

    // Játékosszám -> világméret

    public IntRange GetWorldSizeRange(int playerCount)
    {
        return new IntRange(GetMinWorldSize(playerCount), MaxWorldSize);
    }

    public int GetMinWorldSize(int playerCount)
    {
        ValidatePlayerCount(playerCount);

        double requiredSide = Math.Max(
            _config.AbsoluteMinWorldSize,
            Math.Max(
                GetSideLengthForBases(playerCount + GetMinYellowBaseCount(playerCount)),
                GetPlayerRingSideLength(playerCount)));

        return RoundUpToStep(requiredSide);
    }

    public int GetMinYellowBaseCount(int playerCount)
    {
        ValidatePlayerCount(playerCount);

        int scaled = (int)Math.Ceiling(
            playerCount * (double)_config.YellowBasesPerPlayer - Epsilon);

        return Math.Max(_config.MinYellowBaseCount, scaled);
    }

    // Világméret -> bázisok és akadályok

    public int GetMaxBaseCount(int worldSize)
    {
        double capacity = GetFillableArea(worldSize) * _baseAreaShare / _baseArea;

        return Math.Min(_config.MaxBaseCount, FloorWithTolerance(capacity));
    }

    public int GetMaxObstacleCount(int worldSize)
    {
        if (_config.MaxObstacleCount == 0)
            return 0;

        double capacity = GetFillableArea(worldSize) * (1.0 - _baseAreaShare) / _obstacleArea;

        return Math.Min(_config.MaxObstacleCount, FloorWithTolerance(capacity));
    }

    public IntRange GetYellowBaseCountRange(int playerCount, int worldSize)
    {
        ValidateWorldSize(playerCount, worldSize);

        int min = GetMinYellowBaseCount(playerCount);
        int max = GetMaxBaseCount(worldSize) - playerCount;

        return new IntRange(min, max);
    }

    public IntRange GetObstacleCountRange(int worldSize)
    {
        int max = GetMaxObstacleCount(worldSize);
        int min = Math.Min(_config.MinObstacleCount, max);

        return new IntRange(min, max);
    }

    // Ellenőrzés

    /// <summary>
    /// Ellenőrzi a host által választott generálási beállításokat.
    /// Hiba esetén false értéket ad, és az error tartalmazza az okot.
    /// </summary>
    public bool TryValidate(
        int playerCount,
        int worldSize,
        int yellowBaseCount,
        int obstacleCount,
        out string error)
    {
        if (playerCount < MinPlayerCount || playerCount > MaxPlayerCount)
        {
            error = $"A játékosszám ({playerCount}) a megengedett " +
                    $"[{MinPlayerCount}; {MaxPlayerCount}] tartományon kívül esik.";
            return false;
        }

        IntRange worldSizeRange = GetWorldSizeRange(playerCount);

        if (!worldSizeRange.Contains(worldSize))
        {
            error = $"A világméret ({worldSize}) {playerCount} játékos esetén " +
                    $"a megengedett {worldSizeRange} tartományon kívül esik.";
            return false;
        }

        IntRange yellowRange = GetYellowBaseCountRange(playerCount, worldSize);

        if (!yellowRange.Contains(yellowBaseCount))
        {
            error = $"A sárga bázisok száma ({yellowBaseCount}) a megengedett " +
                    $"{yellowRange} tartományon kívül esik.";
            return false;
        }

        IntRange obstacleRange = GetObstacleCountRange(worldSize);

        if (!obstacleRange.Contains(obstacleCount))
        {
            error = $"Az akadályok száma ({obstacleCount}) a megengedett " +
                    $"{obstacleRange} tartományon kívül esik.";
            return false;
        }

        error = null;
        return true;
    }

    // Belső számítások

    private double GetFillableArea(int worldSize)
    {
        double usableSide = Math.Max(0.0, worldSize - 2.0 * _config.BaseClearRadius);

        return usableSide * usableSide * _config.TargetFillRatio;
    }

    private double GetSideLengthForBases(int baseCount)
    {
        double requiredArea =
            baseCount * _baseArea / (_baseAreaShare * _config.TargetFillRatio);

        return Math.Sqrt(requiredArea) + 2.0 * _config.BaseClearRadius;
    }

    /// <summary>
    /// A kezdőbázisok egy kör mentén, egyenletesen helyezkednek el, és
    /// legfeljebb StartPositionJitter távolsággal térhetnek el a körtől.
    /// Ez az a világméret, amelyben a szomszédos kezdőbázisok között a
    /// legrosszabb esetben is legalább MinBaseDistance távolság marad.
    /// A kör sugarát a WorldGenerator.GetPlayerRingRadius számolja.
    /// </summary>
    private double GetPlayerRingSideLength(int playerCount)
    {
        if (playerCount < 2)
            return 0.0;

        double jitter = _config.StartPositionJitter;
        double requiredRingRadius =
            (_config.MinBaseDistance + 2.0 * jitter) / (2.0 * Math.Sin(Math.PI / playerCount));

        double halfSize = requiredRingRadius / _config.PlayerRingRadiusFraction
                          + _config.BaseClearRadius
                          + jitter;

        return 2.0 * halfSize;
    }

    private int RoundUpToStep(double value)
    {
        int step = _config.WorldSizeStep;

        return (int)Math.Ceiling(value / step - Epsilon) * step;
    }

    private static int FloorWithTolerance(double value)
    {
        return (int)Math.Floor(value + Epsilon);
    }

    private void ValidatePlayerCount(int playerCount)
    {
        if (playerCount < MinPlayerCount || playerCount > MaxPlayerCount)
            throw new ArgumentOutOfRangeException(nameof(playerCount),
                $"A játékosszámnak a [{MinPlayerCount}; {MaxPlayerCount}] " +
                $"tartományba kell esnie: {playerCount}");
    }

    private void ValidateWorldSize(int playerCount, int worldSize)
    {
        IntRange range = GetWorldSizeRange(playerCount);

        if (!range.Contains(worldSize))
            throw new ArgumentOutOfRangeException(nameof(worldSize),
                $"A világméretnek {playerCount} játékos esetén a {range} " +
                $"tartományba kell esnie: {worldSize}");
    }

    private void ValidatePlayerCountsFit()
    {
        for (int players = MinPlayerCount; players <= MaxPlayerCount; players++)
        {
            int requiredBases = players + GetMinYellowBaseCount(players);

            if (requiredBases > _config.MaxBaseCount)
                throw new ArgumentException(
                    $"{players} játékos esetén legalább {requiredBases} bázis " +
                    $"szükséges, de a maximum {_config.MaxBaseCount}.");

            if (GetMinWorldSize(players) > MaxWorldSize)
                throw new ArgumentException(
                    $"{players} játékos esetén a minimális világméret " +
                    $"({GetMinWorldSize(players)}) nagyobb a maximálisnál ({MaxWorldSize}).");
        }
    }
}
