using System;
using NUnit.Framework;

public sealed class LobbyStateTests
{
    private WorldSizingRules _rules;
    private LobbyState _lobby;

    [SetUp]
    public void SetUp()
    {
        _rules = new WorldSizingRules(WorldGenerationTestConfigs.Default());
        _lobby = new LobbyState(_rules, "Host");
    }

    // Kezdőállapot

    [Test]
    public void NewLobby_HasTwoSlotsWithHostInFirst()
    {
        Assert.AreEqual(2, _lobby.PlayerCount);
        Assert.AreEqual(1, _lobby.Slots[0].PlayerId);
        Assert.AreEqual("Host", _lobby.Slots[0].PlayerName);
        Assert.AreEqual(2, _lobby.Slots[1].PlayerId);
        Assert.IsFalse(_lobby.Slots[1].IsOccupied);
        Assert.IsFalse(_lobby.CanStart);
    }

    [Test]
    public void NewLobby_UsesRecommendedSettings()
    {
        Assert.AreEqual(_rules.GetRecommendedWorldSize(2), _lobby.WorldSize);
        Assert.AreEqual(_rules.GetRecommendedYellowBaseCount(2, _lobby.WorldSize), _lobby.YellowBaseCount);
        Assert.AreEqual(_rules.GetRecommendedObstacleCount(_lobby.WorldSize), _lobby.ObstacleCount);
    }

    [Test]
    public void Constructor_RejectsMissingArguments()
    {
        Assert.Throws<ArgumentNullException>(() => new LobbyState(null, "Host"));
        Assert.Throws<ArgumentException>(() => new LobbyState(_rules, " "));
    }

    // Játékosszám

    [Test]
    public void SetPlayerCount_AppliesRecommendedSettings()
    {
        _lobby.SetPlayerCount(10);

        Assert.AreEqual(10, _lobby.PlayerCount);
        Assert.AreEqual(500, _lobby.WorldSize);
        Assert.AreEqual(140, _lobby.YellowBaseCount);
        Assert.AreEqual(50, _lobby.ObstacleCount);
    }

    [TestCase(1, 2)]
    [TestCase(0, 2)]
    [TestCase(11, 10)]
    public void SetPlayerCount_ClampsToLobbyRange(int requested, int expected)
    {
        _lobby.SetPlayerCount(requested);

        Assert.AreEqual(expected, _lobby.PlayerCount);
    }

    [Test]
    public void SetPlayerCount_KeepsOccupiedSlots()
    {
        _lobby.SetPlayerCount(4);
        _lobby.TryOccupyFreeSlot("A", out _);
        _lobby.TryOccupyFreeSlot("B", out int third);

        Assert.AreEqual(3, third);
        Assert.AreEqual(3, _lobby.MinSelectablePlayerCount);

        _lobby.SetPlayerCount(2);

        Assert.AreEqual(3, _lobby.PlayerCount);
        Assert.AreEqual("B", _lobby.Slots[2].PlayerName);
    }

    // Világméret és sárga bázisok

    [Test]
    public void SetWorldSize_RoundsToStepAndResetsDependentValues()
    {
        _lobby.SetWorldSize(333);

        Assert.AreEqual(330, _lobby.WorldSize);
        Assert.AreEqual(_rules.GetRecommendedYellowBaseCount(2, 330), _lobby.YellowBaseCount);
        Assert.AreEqual(_rules.GetRecommendedObstacleCount(330), _lobby.ObstacleCount);
    }

    [Test]
    public void SetWorldSize_ClampsToValidRange()
    {
        _lobby.SetWorldSize(1);
        Assert.AreEqual(_rules.GetMinWorldSize(2), _lobby.WorldSize);

        _lobby.SetWorldSize(10000);
        Assert.AreEqual(_rules.MaxWorldSize, _lobby.WorldSize);
    }

    [Test]
    public void SetYellowBaseCount_ClampsToValidRange()
    {
        IntRange range = _lobby.YellowBaseCountRange;

        _lobby.SetYellowBaseCount(0);
        Assert.AreEqual(range.Min, _lobby.YellowBaseCount);

        _lobby.SetYellowBaseCount(1000);
        Assert.AreEqual(range.Max, _lobby.YellowBaseCount);
    }

    [Test]
    public void ApplyRecommendedSettings_RestoresDefaults()
    {
        _lobby.SetWorldSize(_rules.MaxWorldSize);
        _lobby.SetYellowBaseCount(2);
        _lobby.ApplyRecommendedSettings();

        Assert.AreEqual(_rules.GetRecommendedWorldSize(2), _lobby.WorldSize);
        Assert.AreEqual(_rules.GetRecommendedYellowBaseCount(2, _lobby.WorldSize), _lobby.YellowBaseCount);
    }

    // Játékoshelyek

    [Test]
    public void TryOccupyFreeSlot_FillsSlotsInOrderUntilFull()
    {
        Assert.IsTrue(_lobby.TryOccupyFreeSlot("Kliens", out int playerId));
        Assert.AreEqual(2, playerId);
        Assert.IsTrue(_lobby.CanStart);

        Assert.IsFalse(_lobby.TryOccupyFreeSlot("Késő", out _));
    }

    [Test]
    public void ReleaseSlot_FreesSlotButNotHost()
    {
        _lobby.TryOccupyFreeSlot("Kliens", out int playerId);
        _lobby.ReleaseSlot(playerId);

        Assert.IsFalse(_lobby.Slots[1].IsOccupied);
        Assert.IsFalse(_lobby.CanStart);
        Assert.Throws<InvalidOperationException>(() => _lobby.ReleaseSlot(LobbyState.HostPlayerId));
    }

    [Test]
    public void ReleasedSlot_IsReusedFirst()
    {
        _lobby.SetPlayerCount(3);
        _lobby.TryOccupyFreeSlot("A", out int a);
        _lobby.TryOccupyFreeSlot("B", out _);
        _lobby.ReleaseSlot(a);

        Assert.IsTrue(_lobby.TryOccupyFreeSlot("C", out int c));
        Assert.AreEqual(a, c);
    }

    [Test]
    public void Changed_IsRaisedOnEveryChange()
    {
        int changes = 0;
        _lobby.Changed += () => changes++;

        _lobby.SetPlayerCount(3);
        _lobby.SetWorldSize(_rules.MaxWorldSize);
        _lobby.SetYellowBaseCount(5);
        _lobby.TryOccupyFreeSlot("A", out int playerId);
        _lobby.ReleaseSlot(playerId);

        Assert.AreEqual(5, changes);
    }

    // Generálási kérés

    [Test]
    public void CreateRequest_RequiresAllSlotsFilled()
    {
        Assert.Throws<InvalidOperationException>(() => _lobby.CreateRequest(1));
    }

    [Test]
    public void CreateRequest_ContainsSlotsAndSettingsAndIsValid()
    {
        _lobby.SetPlayerCount(4);

        for (int i = 0; i < 3; i++)
            _lobby.TryOccupyFreeSlot($"Kliens {i}", out _);

        WorldGenerationRequest request = _lobby.CreateRequest(42);

        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, request.PlayerIds);
        Assert.AreEqual(_lobby.WorldSize, request.WorldSize);
        Assert.AreEqual(_lobby.YellowBaseCount, request.YellowBaseCount);
        Assert.AreEqual(_lobby.ObstacleCount, request.ObstacleCount);
        Assert.AreEqual(42, request.Seed);

        Assert.IsTrue(_rules.TryValidate(4, request.WorldSize, request.YellowBaseCount,
            request.ObstacleCount, out string error), error);
    }

    // Megjelenítés

    [Test]
    public void SlotList_ShowsHostAndWaitingSlots()
    {
        string text = MatchLobbyController.BuildSlotList(LobbySnapshot.FromState(_lobby, 1));

        StringAssert.Contains("1. hely: Host (host) – te", text);
        StringAssert.Contains("2. hely: várakozás játékosra", text);
        StringAssert.Contains("1 / 2 játékos csatlakozott", text);
    }
}
