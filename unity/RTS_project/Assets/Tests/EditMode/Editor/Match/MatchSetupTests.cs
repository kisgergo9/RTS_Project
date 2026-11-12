using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public sealed class MatchSetupTests
{
    private WorldGenerator _generator;
    private LobbyState _lobby;
    private WorldLayout _layout;

    [SetUp]
    public void SetUp()
    {
        // A projekt valódi beállításai (tereptárgytípusokkal), ahogy a lobby is használja.
        _generator = new WorldGenerator(AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(
            "Assets/Data/WorldGeneration/WorldGenerationSettings.asset").ToConfig());
        _lobby = new LobbyState(_generator.Rules, LobbyState.GetDefaultPlayerName(LobbyState.HostPlayerId));
        _lobby.SetPlayerCount(3);
        _lobby.TryOccupyFreeSlot(out _);
        _lobby.TryOccupyFreeSlot(out _);

        WorldGenerationResult result = _generator.Generate(_lobby.CreateRequest(77));
        Assert.IsTrue(result.Success, result.ErrorMessage);
        _layout = result.Layout;
    }

    // MatchSetup

    [Test]
    public void FromLobby_ContainsPlayersLocalPlayerAndWorld()
    {
        MatchSetup setup = MatchSetup.FromLobby(_lobby, _layout, 2);

        Assert.AreEqual(3, setup.Players.Count);
        Assert.AreEqual(1, setup.Players[0].PlayerId);
        Assert.AreEqual("Játékos 1", setup.Players[0].Name);
        Assert.AreEqual(3, setup.Players[2].PlayerId);
        Assert.AreEqual(2, setup.LocalPlayerId);
        Assert.AreEqual(77, setup.World.seed);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, setup.World.playerIds);
    }

    [Test]
    public void FromLobby_RequiresFullLobby()
    {
        _lobby.ReleaseSlot(3);

        Assert.Throws<InvalidOperationException>(() => MatchSetup.FromLobby(_lobby, _layout, 1));
    }

    [Test]
    public void Constructor_RejectsInconsistentData()
    {
        WorldGenerationState world = WorldLayoutSnapshot.ToState(_layout);
        MatchPlayer[] players = { new MatchPlayer(1, "A"), new MatchPlayer(2, "B"), new MatchPlayer(3, "C") };

        // Ismeretlen helyi játékos
        Assert.Throws<ArgumentException>(() => new MatchSetup(players, 4, world));

        // Duplikált azonosító
        Assert.Throws<ArgumentException>(() => new MatchSetup(
            new[] { new MatchPlayer(1, "A"), new MatchPlayer(1, "B"), new MatchPlayer(3, "C") }, 1, world));

        // A világ játékosai mások
        Assert.Throws<ArgumentException>(() => new MatchSetup(
            new[] { new MatchPlayer(1, "A"), new MatchPlayer(2, "B") }, 1, world));
    }

    [Test]
    public void MatchPlayer_WithoutName_UsesDefaultName()
    {
        Assert.AreEqual("Játékos 4", new MatchPlayer(4, " ").Name);
    }

    // Indítóüzenet

    [Test]
    public void StartMessageRoundTrip_PreservesSetupForEachClient()
    {
        MatchSetup hostSetup = MatchSetup.FromLobby(_lobby, _layout, LobbyState.HostPlayerId);

        using FastBufferWriter writer = new FastBufferWriter(8 * 1024, Allocator.Temp, 64 * 1024);
        writer.WriteNetworkSerializable(MatchStartMessage.FromSetup(hostSetup, 3));

        using FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);
        reader.ReadNetworkSerializable(out MatchStartMessage received);
        MatchSetup clientSetup = received.ToSetup();

        Assert.AreEqual(3, clientSetup.LocalPlayerId);
        Assert.AreEqual(hostSetup.Players.Count, clientSetup.Players.Count);

        for (int i = 0; i < hostSetup.Players.Count; i++)
        {
            Assert.AreEqual(hostSetup.Players[i].PlayerId, clientSetup.Players[i].PlayerId);
            Assert.AreEqual(hostSetup.Players[i].Name, clientSetup.Players[i].Name);
        }

        WorldLayout clientLayout = WorldLayoutSnapshot.FromState(clientSetup.World);

        Assert.AreEqual(_layout.Seed, clientLayout.Seed);
        Assert.AreEqual(_layout.WorldSize, clientLayout.WorldSize);
        Assert.AreEqual(_layout.Bases.Count, clientLayout.Bases.Count);

        for (int i = 0; i < _layout.Bases.Count; i++)
        {
            Assert.AreEqual(_layout.Bases[i].Position, clientLayout.Bases[i].Position);
            Assert.AreEqual(_layout.Bases[i].PlayerId, clientLayout.Bases[i].PlayerId);
        }
    }

    [Test]
    public void StartMessage_RejectsInvalidPlayerCount()
    {
        using FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe(1);
        writer.WriteValueSafe(MatchStartMessage.MaxPlayers + 1);

        using FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);

        Assert.Throws<InvalidOperationException>(() => reader.ReadNetworkSerializable(out MatchStartMessage _));
    }

    // A kapott világ használata

    [Test]
    public void WithDefenders_RestoresExactlyTheHostDefenders()
    {
        Assert.Greater(_layout.Defenders.Count, 0, "A teszthez védőcsapatok kellenek.");

        WorldLayout received = WorldLayoutSnapshot.FromState(WorldLayoutSnapshot.ToState(_layout));
        Assert.AreEqual(0, received.Defenders.Count, "A pillanatkép nem viszi a védőcsapatokat.");

        // A csapatok a bázisok erősségéből számolódnak, ezért annak is utaznia kell.
        for (int i = 0; i < _layout.Bases.Count; i++)
            Assert.AreEqual(_layout.Bases[i].Strength, received.Bases[i].Strength);

        WorldLayout restored = _generator.WithDefenders(received);

        Assert.AreEqual(_layout.Defenders.Count, restored.Defenders.Count);

        for (int i = 0; i < _layout.Defenders.Count; i++)
        {
            Assert.AreEqual(_layout.Defenders[i].BaseId, restored.Defenders[i].BaseId);
            Assert.AreEqual(_layout.Defenders[i].UnitTypeId, restored.Defenders[i].UnitTypeId);
            Assert.AreEqual(_layout.Defenders[i].SquadIndex, restored.Defenders[i].SquadIndex);
            Assert.AreEqual(_layout.Defenders[i].Position, restored.Defenders[i].Position);
        }

        Assert.AreSame(restored, _generator.WithDefenders(restored));
    }

    [Test]
    public void WorldGenerationSetup_UsesPresetLayoutInsteadOfGenerating()
    {
        GameObject go = new GameObject("WorldGenerationSetupTest");

        try
        {
            WorldGenerationSetup setup = go.AddComponent<WorldGenerationSetup>();
            SerializedObject serialized = new SerializedObject(setup);
            serialized.FindProperty("settings").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(
                    "Assets/Data/WorldGeneration/WorldGenerationSettings.asset");
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // Ahogy a kliens kapja: pillanatképből, védőcsapatok nélkül.
            setup.UsePresetLayout(WorldLayoutSnapshot.FromState(WorldLayoutSnapshot.ToState(_layout)));

            PlayerState[] players = { new PlayerState(1, "A"), new PlayerState(2, "B"), new PlayerState(3, "C") };
            WorldGenerationResult result = setup.Generate(players);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(_layout.Bases.Count, result.Layout.Bases.Count);
            Assert.AreEqual(_layout.Defenders.Count, result.Layout.Defenders.Count);
            Assert.AreEqual(77, setup.LastUsedSeed);

            Assert.Throws<InvalidOperationException>(
                () => setup.Generate(new[] { new PlayerState(1, "A"), new PlayerState(2, "B") }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    // Jelenetek

    [Test]
    public void MatchScene_IsInBuildSettingsAndWorldStartsInactive()
    {
        bool found = false;

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && scene.path == "Assets/Scenes/Match.unity")
                found = true;
        }

        Assert.IsTrue(found, "A Match jelenet nincs a Build Settings listájában.");

        // A World GameObjectnek inaktívnak kell lennie (a MatchStarter kapcsolja be).
        string text = System.IO.File.ReadAllText("Assets/Scenes/Match.unity").Replace("\r\n", "\n");

        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(
            text, @"m_Name: World\n(?:  .*\n){0,6}?  m_IsActive: 0\n"), "A World GameObject aktívan van mentve.");
        StringAssert.Contains("world: {fileID:", text);
    }
}
