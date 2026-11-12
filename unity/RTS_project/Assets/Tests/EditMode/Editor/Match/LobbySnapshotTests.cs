using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

public sealed class LobbySnapshotTests
{
    private WorldSizingRules _rules;
    private LobbyState _lobby;

    [SetUp]
    public void SetUp()
    {
        _rules = new WorldSizingRules(WorldGenerationTestConfigs.Default());
        _lobby = new LobbyState(_rules, LobbyState.GetDefaultPlayerName(LobbyState.HostPlayerId));
    }

    [Test]
    public void FromState_CopiesSettingsRangesAndSlots()
    {
        _lobby.SetPlayerCount(3);
        _lobby.TryOccupyFreeSlot(out _);

        LobbySnapshot snapshot = LobbySnapshot.FromState(_lobby, 2);

        Assert.AreEqual(2, snapshot.LocalPlayerId);
        Assert.AreEqual(3, snapshot.PlayerCount);
        Assert.AreEqual(_lobby.WorldSize, snapshot.WorldSize);
        Assert.AreEqual(_lobby.WorldSizeRange.Min, snapshot.WorldSizeMin);
        Assert.AreEqual(_lobby.WorldSizeRange.Max, snapshot.WorldSizeMax);
        Assert.AreEqual(_lobby.YellowBaseCount, snapshot.YellowBaseCount);
        Assert.AreEqual(_lobby.ObstacleCount, snapshot.ObstacleCount);
        CollectionAssert.AreEqual(new[] { "Játékos 1", "Játékos 2", "" }, snapshot.SlotNames);
        Assert.AreEqual(2, snapshot.OccupiedSlotCount);
        Assert.IsFalse(snapshot.CanStart);
    }

    [Test]
    public void NetworkRoundTrip_PreservesSnapshot()
    {
        _lobby.SetPlayerCount(4);
        _lobby.TryOccupyFreeSlot(out _);
        LobbySnapshot sent = LobbySnapshot.FromState(_lobby, 2);

        using FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp, 16 * 1024);
        writer.WriteNetworkSerializable(sent);

        using FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);
        reader.ReadNetworkSerializable(out LobbySnapshot received);

        Assert.AreEqual(sent.LocalPlayerId, received.LocalPlayerId);
        Assert.AreEqual(sent.MinPlayerCount, received.MinPlayerCount);
        Assert.AreEqual(sent.MaxPlayerCount, received.MaxPlayerCount);
        Assert.AreEqual(sent.WorldSize, received.WorldSize);
        Assert.AreEqual(sent.WorldSizeMin, received.WorldSizeMin);
        Assert.AreEqual(sent.WorldSizeMax, received.WorldSizeMax);
        Assert.AreEqual(sent.WorldSizeStep, received.WorldSizeStep);
        Assert.AreEqual(sent.YellowBaseCount, received.YellowBaseCount);
        Assert.AreEqual(sent.YellowBaseMin, received.YellowBaseMin);
        Assert.AreEqual(sent.YellowBaseMax, received.YellowBaseMax);
        Assert.AreEqual(sent.ObstacleCount, received.ObstacleCount);
        CollectionAssert.AreEqual(sent.SlotNames, received.SlotNames);
    }

    [Test]
    public void Read_RejectsOversizedSlotCount()
    {
        using FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);

        // 11 egész szám a beállításokhoz, majd a helyszám.
        for (int i = 0; i < 11; i++)
            writer.WriteValueSafe(0);

        writer.WriteValueSafe(LobbySnapshot.MaxSlots + 1);

        using FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);

        Assert.Throws<InvalidOperationException>(
            () => reader.ReadNetworkSerializable(out LobbySnapshot _));
    }

    [Test]
    public void TryOccupyFreeSlot_WithoutName_UsesSlotNumber()
    {
        _lobby.SetPlayerCount(3);

        Assert.IsTrue(_lobby.TryOccupyFreeSlot(out int playerId));
        Assert.AreEqual(2, playerId);
        Assert.AreEqual("Játékos 2", _lobby.Slots[1].PlayerName);
    }

    [Test]
    public void SlotList_MarksHostAndLocalPlayer()
    {
        _lobby.TryOccupyFreeSlot(out _);

        string text = MatchLobbyController.BuildSlotList(LobbySnapshot.FromState(_lobby, 2));

        StringAssert.Contains("1. hely: Játékos 1 (host)\n", text);
        StringAssert.Contains("2. hely: Játékos 2 – te", text);
        StringAssert.Contains("2 / 2 játékos csatlakozott", text);
    }

    // Kapcsolatbontás oka

    [TestCase(null, LobbyNetwork.ConnectionLostReason)]
    [TestCase("", LobbyNetwork.ConnectionLostReason)]
    [TestCase(LobbyNetwork.LobbyFullReason, LobbyNetwork.LobbyFullReason)]
    [TestCase(LobbyNetwork.NoLobbyReason, LobbyNetwork.NoLobbyReason)]
    [TestCase("Disconnected due to host shutting down.", LobbyNetwork.HostClosedReason)]
    [TestCase("Some other transport error", LobbyNetwork.ConnectionLostReason)]
    public void DisconnectReason_IsShownInHungarian(string reason, string expected)
    {
        Assert.AreEqual(expected, LobbyNetwork.TranslateDisconnectReason(reason));
    }

    // Címfeldolgozás a csatlakozási képernyőn

    [TestCase("192.168.0.12", "192.168.0.12", NetworkSession.DefaultPort)]
    [TestCase(" 10.0.0.5 ", "10.0.0.5", NetworkSession.DefaultPort)]
    [TestCase("192.168.0.12:8000", "192.168.0.12", (ushort)8000)]
    [TestCase("localhost", "localhost", NetworkSession.DefaultPort)]
    public void ParseAddress_AcceptsHostAndOptionalPort(string input, string host, ushort port)
    {
        Assert.IsTrue(MatchJoinController.TryParseAddress(input, out string parsedHost, out ushort parsedPort));
        Assert.AreEqual(host, parsedHost);
        Assert.AreEqual(port, parsedPort);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("192.168.0.12:")]
    [TestCase("192.168.0.12:0")]
    [TestCase("192.168.0.12:99999")]
    [TestCase(":7777")]
    [TestCase("192.168 .0.12")]
    public void ParseAddress_RejectsInvalidInput(string input)
    {
        Assert.IsFalse(MatchJoinController.TryParseAddress(input, out _, out _));
    }
}
