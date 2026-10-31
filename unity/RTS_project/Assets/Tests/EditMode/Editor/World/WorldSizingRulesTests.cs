using System;
using NUnit.Framework;

public sealed class WorldSizingRulesTests
{
    private WorldGenerationConfig _config;
    private WorldSizingRules _rules;

    [SetUp]
    public void SetUp()
    {
        _config = WorldGenerationTestConfigs.Default();
        _rules = new WorldSizingRules(_config);
    }

    [Test]
    public void MaxWorldSize_FitsMaxBasesAndObstacles()
    {
        Assert.AreEqual(_config.MaxBaseCount, _rules.GetMaxBaseCount(_rules.MaxWorldSize));
        Assert.AreEqual(_config.MaxObstacleCount, _rules.GetMaxObstacleCount(_rules.MaxWorldSize));
    }

    [Test]
    public void MaxWorldSize_IsSmallestStepThatFitsMaxBases()
    {
        int smaller = _rules.MaxWorldSize - _config.WorldSizeStep;

        Assert.Less(_rules.GetMaxBaseCount(smaller), _config.MaxBaseCount);
    }

    [Test]
    public void MinWorldSize_IsStepAlignedAndNotBelowAbsoluteMinimum()
    {
        for (int players = _rules.MinPlayerCount; players <= _rules.MaxPlayerCount; players++)
        {
            int minSize = _rules.GetMinWorldSize(players);

            Assert.GreaterOrEqual(minSize, _config.AbsoluteMinWorldSize);
            Assert.AreEqual(0, minSize % _config.WorldSizeStep, $"players={players}");
            Assert.LessOrEqual(minSize, _rules.MaxWorldSize, $"players={players}");
        }
    }

    [Test]
    public void MinWorldSize_DoesNotDecreaseWithMorePlayers()
    {
        for (int players = _rules.MinPlayerCount + 1; players <= _rules.MaxPlayerCount; players++)
        {
            Assert.GreaterOrEqual(
                _rules.GetMinWorldSize(players),
                _rules.GetMinWorldSize(players - 1),
                $"players={players}");
        }
    }

    [Test]
    public void MinWorldSize_FitsPlayersAndMinimumYellowBases()
    {
        for (int players = _rules.MinPlayerCount; players <= _rules.MaxPlayerCount; players++)
        {
            int minSize = _rules.GetMinWorldSize(players);
            int requiredBases = players + _rules.GetMinYellowBaseCount(players);

            Assert.GreaterOrEqual(_rules.GetMaxBaseCount(minSize), requiredBases, $"players={players}");
        }
    }

    [Test]
    public void MinWorldSize_LeavesRoomForEvenlySpacedStartingBases()
    {
        WorldSizingRules rules = new WorldSizingRules(
            WorldGenerationTestConfigs.Create(minBaseDistance: 40f));

        int players = 10;
        double ringRadius = 40.0 / (2.0 * Math.Sin(Math.PI / players));

        Assert.GreaterOrEqual(rules.GetMinWorldSize(players), 2.0 * (ringRadius + 9.0));
    }

    [Test]
    public void MaxBaseCount_GrowsWithWorldSize()
    {
        int previous = 0;

        for (int size = _rules.GetMinWorldSize(1); size <= _rules.MaxWorldSize; size += _config.WorldSizeStep)
        {
            int current = _rules.GetMaxBaseCount(size);

            Assert.GreaterOrEqual(current, previous, $"size={size}");
            previous = current;
        }
    }

    [Test]
    public void MinYellowBaseCount_HasAbsoluteMinimumAndScalesWithPlayers()
    {
        WorldSizingRules rules = new WorldSizingRules(
            WorldGenerationTestConfigs.Create(minYellowBaseCount: 2, yellowBasesPerPlayer: 1.5f));

        Assert.AreEqual(2, rules.GetMinYellowBaseCount(1));
        Assert.AreEqual(5, rules.GetMinYellowBaseCount(3));
        Assert.AreEqual(15, rules.GetMinYellowBaseCount(10));
    }

    [Test]
    public void YellowBaseCountRange_UpperBoundLeavesRoomForPlayers()
    {
        IntRange range = _rules.GetYellowBaseCountRange(4, _rules.MaxWorldSize);

        Assert.AreEqual(_rules.GetMinYellowBaseCount(4), range.Min);
        Assert.AreEqual(_config.MaxBaseCount - 4, range.Max);
    }

    [Test]
    public void ObstacleCountRange_ClampsMinimumToCapacity()
    {
        WorldSizingRules rules = new WorldSizingRules(
            WorldGenerationTestConfigs.Create(minObstacleCount: 40));

        int smallest = rules.GetMinWorldSize(1);
        IntRange range = rules.GetObstacleCountRange(smallest);

        Assert.LessOrEqual(range.Min, range.Max);
        Assert.AreEqual(40, rules.GetObstacleCountRange(rules.MaxWorldSize).Min);
    }

    [TestCase(0)]
    [TestCase(11)]
    public void GetMinWorldSize_InvalidPlayerCount_Throws(int players)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _rules.GetMinWorldSize(players));
    }

    [Test]
    public void TryValidate_AcceptsMinimalSettings()
    {
        int players = 4;
        int size = _rules.GetMinWorldSize(players);

        bool valid = _rules.TryValidate(
            players, size, _rules.GetMinYellowBaseCount(players), 0, out string error);

        Assert.IsTrue(valid, error);
        Assert.IsNull(error);
    }

    [Test]
    public void TryValidate_AcceptsMaximalSettings()
    {
        int players = _rules.MaxPlayerCount;

        bool valid = _rules.TryValidate(
            players,
            _rules.MaxWorldSize,
            _config.MaxBaseCount - players,
            _config.MaxObstacleCount,
            out string error);

        Assert.IsTrue(valid, error);
    }

    [TestCase(0, TestName = "TryValidate_RejectsTooFewPlayers")]
    [TestCase(11, TestName = "TryValidate_RejectsTooManyPlayers")]
    public void TryValidate_RejectsInvalidPlayerCount(int players)
    {
        Assert.IsFalse(_rules.TryValidate(players, _rules.MaxWorldSize, 1, 0, out string error));
        Assert.IsNotNull(error);
    }

    [Test]
    public void TryValidate_RejectsWorldBelowPlayerMinimum()
    {
        int players = 10;
        int tooSmall = _rules.GetMinWorldSize(players) - 1;

        Assert.IsFalse(_rules.TryValidate(players, tooSmall, 10, 0, out _));
    }

    [Test]
    public void TryValidate_RejectsWorldAboveMaximum()
    {
        Assert.IsFalse(_rules.TryValidate(2, _rules.MaxWorldSize + 1, 2, 0, out _));
    }

    [Test]
    public void TryValidate_RejectsTooFewYellowBases()
    {
        int players = 6;

        Assert.IsFalse(_rules.TryValidate(
            players, _rules.MaxWorldSize, _rules.GetMinYellowBaseCount(players) - 1, 0, out _));
    }

    [Test]
    public void TryValidate_RejectsMoreBasesThanWorldFits()
    {
        int players = 2;
        int size = _rules.GetMinWorldSize(players);
        int tooMany = _rules.GetMaxBaseCount(size) - players + 1;

        Assert.IsFalse(_rules.TryValidate(players, size, tooMany, 0, out _));
    }

    [Test]
    public void TryValidate_RejectsTooManyObstacles()
    {
        int size = _rules.MaxWorldSize;

        Assert.IsFalse(_rules.TryValidate(2, size, 2, _rules.GetMaxObstacleCount(size) + 1, out _));
    }

    [Test]
    public void Constructor_RejectsConfigWherePlayersCannotFit()
    {
        WorldGenerationConfig config = WorldGenerationTestConfigs.Create(
            maxBaseCount: 30, yellowBasesPerPlayer: 3f);

        Assert.Throws<ArgumentException>(() => new WorldSizingRules(config));
    }

    [Test]
    public void Config_RejectsFillRatioAboveRandomPlacementLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorldGenerationTestConfigs.Create(targetFillRatio: 0.6f));
    }

    [Test]
    public void Config_RejectsMaxPlayersBelowMinPlayers()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorldGenerationTestConfigs.Create(minPlayerCount: 5, maxPlayerCount: 4));
    }
}
