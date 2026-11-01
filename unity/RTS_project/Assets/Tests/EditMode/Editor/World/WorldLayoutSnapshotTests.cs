using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

public sealed class WorldLayoutSnapshotTests
{
    private WorldLayout _layout;

    [SetUp]
    public void SetUp()
    {
        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Default());

        WorldGenerationRequest request = new WorldGenerationRequest(
            new[] { 4, 2, 9 },
            generator.Rules.MaxWorldSize,
            40,
            12,
            -777);

        WorldGenerationResult result = generator.Generate(request);
        Assert.IsTrue(result.Success, result.ErrorMessage);

        _layout = result.Layout;
    }

    private void AssertSameLayout(WorldLayout expected, WorldLayout actual)
    {
        Assert.AreEqual(expected.GeneratorVersion, actual.GeneratorVersion);
        Assert.AreEqual(expected.Seed, actual.Seed);
        Assert.AreEqual(expected.WorldSize, actual.WorldSize);
        Assert.AreEqual(expected.Request.YellowBaseCount, actual.Request.YellowBaseCount);
        Assert.AreEqual(expected.Request.ObstacleCount, actual.Request.ObstacleCount);
        CollectionAssert.AreEqual(expected.Request.PlayerIds, actual.Request.PlayerIds);
        Assert.AreEqual(expected.Bases.Count, actual.Bases.Count);

        for (int i = 0; i < expected.Bases.Count; i++)
        {
            Assert.AreEqual(expected.Bases[i].BaseId, actual.Bases[i].BaseId);
            Assert.AreEqual(expected.Bases[i].Position, actual.Bases[i].Position);
            Assert.AreEqual(expected.Bases[i].OwnerType, actual.Bases[i].OwnerType);
            Assert.AreEqual(expected.Bases[i].PlayerId, actual.Bases[i].PlayerId);
        }
    }

    // Pillanatkép

    [Test]
    public void State_ContainsSeedSettingsAndVersion()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);

        Assert.AreEqual(WorldGenerator.Version, state.generatorVersion);
        Assert.AreEqual(-777, state.seed);
        Assert.AreEqual(_layout.WorldSize, state.worldSize);
        Assert.AreEqual(40, state.yellowBaseCount);
        Assert.AreEqual(12, state.obstacleCount);
        CollectionAssert.AreEqual(new[] { 4, 2, 9 }, state.playerIds);
        Assert.AreEqual(43, state.bases.Length);
    }

    [Test]
    public void StateRoundTrip_PreservesLayout()
    {
        WorldLayout restored = WorldLayoutSnapshot.FromState(WorldLayoutSnapshot.ToState(_layout));

        AssertSameLayout(_layout, restored);
    }

    [Test]
    public void JsonRoundTrip_PreservesLayoutExactly()
    {
        string json = WorldLayoutSnapshot.ToJson(WorldLayoutSnapshot.ToState(_layout));
        WorldLayout restored = WorldLayoutSnapshot.FromState(WorldLayoutSnapshot.FromJson(json));

        AssertSameLayout(_layout, restored);
    }

    [Test]
    public void RestoredSeedAndSettings_RegenerateSameWorld()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);
        WorldLayout restored = WorldLayoutSnapshot.FromState(state);

        WorldGenerationResult regenerated =
            new WorldGenerator(WorldGenerationTestConfigs.Default()).Generate(restored.Request);

        Assert.IsTrue(regenerated.Success, regenerated.ErrorMessage);
        AssertSameLayout(_layout, regenerated.Layout);
    }

    [Test]
    public void FromState_RejectsDuplicateBaseIds()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);
        state.bases[1].baseId = state.bases[0].baseId;

        Assert.Throws<ArgumentException>(() => WorldLayoutSnapshot.FromState(state));
    }

    [Test]
    public void FromState_RejectsBaseOutsideWorld()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);
        state.bases[0].x = state.worldSize;

        Assert.Throws<ArgumentException>(() => WorldLayoutSnapshot.FromState(state));
    }

    [Test]
    public void FromState_RejectsPlayerBaseOfUnknownPlayer()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);
        state.bases[0].playerId = 12345;

        Assert.Throws<ArgumentException>(() => WorldLayoutSnapshot.FromState(state));
    }

    [Test]
    public void FromState_RejectsMissingArrays()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);
        state.bases = null;

        Assert.Throws<ArgumentException>(() => WorldLayoutSnapshot.FromState(state));
    }

    [Test]
    public void FromJson_RejectsEmptyInput()
    {
        Assert.Throws<ArgumentException>(() => WorldLayoutSnapshot.FromJson(" "));
    }

    // Világállapot

    [Test]
    public void WorldState_StoresGenerationOnlyOnce()
    {
        WorldState world = new WorldState();
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);

        world.SetGeneration(state);

        Assert.AreSame(state, world.Generation);
        Assert.Throws<InvalidOperationException>(() => world.SetGeneration(state));
    }

    // Hálózati üzenet

    [Test]
    public void NetworkMessageRoundTrip_PreservesLayoutExactly()
    {
        WorldGenerationMessage sent = new WorldGenerationMessage(WorldLayoutSnapshot.ToState(_layout));

        using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp, 1024 * 1024);
        writer.WriteNetworkSerializable(sent);

        using FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);
        reader.ReadNetworkSerializable(out WorldGenerationMessage received);

        AssertSameLayout(_layout, WorldLayoutSnapshot.FromState(received.State));
    }

    [Test]
    public void NetworkMessage_MaxWorldFitsInFewKilobytes()
    {
        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Default());
        int size = generator.Rules.MaxWorldSize;

        WorldGenerationResult result = generator.Generate(new WorldGenerationRequest(
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, size, 140, 50, 1));

        Assert.IsTrue(result.Success, result.ErrorMessage);

        using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp, 1024 * 1024);
        writer.WriteNetworkSerializable(new WorldGenerationMessage(WorldLayoutSnapshot.ToState(result.Layout)));

        Assert.Less(writer.Length, 4 * 1024);
    }

    [Test]
    public void NetworkMessage_RejectsOversizedCount()
    {
        using FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp);

        // generatorVersion, seed, worldSize, yellowBaseCount, obstacleCount, playerCount
        int[] header = { 1, 0, 100, 1, 0, WorldGenerationMessage.MaxEntries + 1 };

        foreach (int value in header)
            writer.WriteValueSafe(value);

        using FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);

        Assert.Throws<InvalidOperationException>(
            () => reader.ReadNetworkSerializable(out WorldGenerationMessage _));
    }
}
