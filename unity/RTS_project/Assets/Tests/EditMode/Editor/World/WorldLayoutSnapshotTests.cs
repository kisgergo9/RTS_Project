using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

public sealed class WorldLayoutSnapshotTests
{
    private WorldLayout _layout;

    // A tesztek akadályokat is kérnek, ezért akadálytípusokat tartalmazó konfiguráció kell.
    private static WorldGenerationConfig TerrainConfig()
    {
        return WorldGenerationTestConfigs.Create(terrain: TerrainTestConfigs.Create());
    }

    [SetUp]
    public void SetUp()
    {
        WorldGenerator generator = new WorldGenerator(TerrainConfig());

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

    /// <param name="decorationTolerance">
    /// A dekorációk pozíciójának és léptékének megengedett eltérése. A hálózati
    /// üzenet a dekorációkat kerekítve viszi, a mentés pontosan.
    /// </param>
    private void AssertSameLayout(WorldLayout expected, WorldLayout actual, float decorationTolerance = 0f)
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

        AssertSameFeatures(expected.Obstacles, actual.Obstacles, 0f);
        AssertSameFeatures(expected.Decorations, actual.Decorations, decorationTolerance);
    }

    private static void AssertSameFeatures(
        System.Collections.Generic.IReadOnlyList<TerrainFeatureLayout> expected,
        System.Collections.Generic.IReadOnlyList<TerrainFeatureLayout> actual,
        float tolerance)
    {
        Assert.AreEqual(expected.Count, actual.Count);

        for (int i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i].TypeId, actual[i].TypeId);
            Assert.AreEqual(expected[i].Variant, actual[i].Variant);

            if (tolerance <= 0f)
            {
                Assert.AreEqual(expected[i].Position, actual[i].Position);
                Assert.AreEqual(expected[i].Scale, actual[i].Scale);
            }
            else
            {
                Assert.AreEqual(expected[i].Position.x, actual[i].Position.x, tolerance);
                Assert.AreEqual(expected[i].Position.y, actual[i].Position.y, tolerance);
                Assert.AreEqual(expected[i].Scale, actual[i].Scale, tolerance);
            }
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
    public void State_ContainsObstaclesAndDecorations()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);

        Assert.AreEqual(12, _layout.Obstacles.Count);
        Assert.Greater(_layout.Decorations.Count, 0);
        Assert.AreEqual(_layout.Obstacles.Count, state.obstacles.Length);
        Assert.AreEqual(_layout.Decorations.Count, state.decorations.Length);
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
            new WorldGenerator(TerrainConfig()).Generate(restored.Request);

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
    public void FromState_TreatsMissingTerrainArraysAsEmpty()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);
        state.obstacles = null;
        state.decorations = null;

        WorldLayout restored = WorldLayoutSnapshot.FromState(state);

        Assert.AreEqual(0, restored.Obstacles.Count);
        Assert.AreEqual(0, restored.Decorations.Count);
        Assert.AreEqual(_layout.Bases.Count, restored.Bases.Count);
    }

    [Test]
    public void FromJson_LoadsSaveWrittenBeforeTerrainExisted()
    {
        // Az 1-es generátorverzió mentése: nincs obstacles és decorations mező.
        string json =
            "{\"generatorVersion\":1,\"seed\":5,\"worldSize\":140,\"yellowBaseCount\":1," +
            "\"obstacleCount\":0,\"playerIds\":[1]," +
            "\"bases\":[{\"baseId\":1,\"x\":10.0,\"y\":-5.0,\"ownerType\":1,\"playerId\":1}," +
            "{\"baseId\":2,\"x\":-30.0,\"y\":20.0,\"ownerType\":2,\"playerId\":0}]}";

        WorldLayout restored = WorldLayoutSnapshot.FromState(WorldLayoutSnapshot.FromJson(json));

        Assert.AreEqual(1, restored.GeneratorVersion);
        Assert.AreEqual(2, restored.Bases.Count);
        Assert.AreEqual(0, restored.Obstacles.Count);
        Assert.AreEqual(0, restored.Decorations.Count);
    }

    [Test]
    public void FromState_RejectsObstacleOutsideWorld()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);
        state.obstacles[0].x = state.worldSize;

        Assert.Throws<ArgumentException>(() => WorldLayoutSnapshot.FromState(state));
    }

    [Test]
    public void FromState_RejectsInvalidTerrainFeature()
    {
        WorldGenerationState emptyType = WorldLayoutSnapshot.ToState(_layout);
        emptyType.obstacles[0].typeId = "";

        WorldGenerationState badScale = WorldLayoutSnapshot.ToState(_layout);
        badScale.decorations[0].scale = 0f;

        WorldGenerationState badVariant = WorldLayoutSnapshot.ToState(_layout);
        badVariant.obstacles[0].variant = -1;

        Assert.Throws<ArgumentException>(() => WorldLayoutSnapshot.FromState(emptyType));
        Assert.Throws<ArgumentException>(() => WorldLayoutSnapshot.FromState(badScale));
        Assert.Throws<ArgumentException>(() => WorldLayoutSnapshot.FromState(badVariant));
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
    public void NetworkMessageRoundTrip_PreservesLayout()
    {
        WorldGenerationMessage sent = new WorldGenerationMessage(WorldLayoutSnapshot.ToState(_layout));

        using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp, 1024 * 1024);
        writer.WriteNetworkSerializable(sent);

        using FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);
        reader.ReadNetworkSerializable(out WorldGenerationMessage received);

        // Bázisok és akadályok pontosan; dekorációk a kerekítés pontosságán belül
        // (pozíció: pályaméret / 65535 / 2, lépték: 0,005).
        float decorationTolerance = _layout.WorldSize / 65535f;

        AssertSameLayout(_layout, WorldLayoutSnapshot.FromState(received.State), decorationTolerance);
    }

    [Test]
    public void NetworkMessage_MaxWorldFitsInDefaultTransportPayload()
    {
        // A Unity Transport alapértelmezett MaxPayloadSize értéke.
        const int defaultMaxPayloadSize = 6144;

        // A projekt valódi beállításai (dekorációsűrűség, típusok).
        WorldGenerationSettings settings = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(
            "Assets/Data/WorldGeneration/WorldGenerationSettings.asset");
        WorldGenerator generator = new WorldGenerator(settings.ToConfig());
        int size = generator.Rules.MaxWorldSize;

        WorldGenerationResult result = generator.Generate(new WorldGenerationRequest(
            new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, size, 140, 50, 1));

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.AreEqual(50, result.Layout.Obstacles.Count);
        Assert.AreEqual(generator.GetRequestedDecorationCount(size), result.Layout.Decorations.Count);

        using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp, 1024 * 1024);
        writer.WriteNetworkSerializable(new WorldGenerationMessage(WorldLayoutSnapshot.ToState(result.Layout)));

        UnityEngine.Debug.Log($"WorldGenerationMessage, legnagyobb világ: {writer.Length} bájt");
        Assert.Less(writer.Length, defaultMaxPayloadSize);
    }

    [Test]
    public void NetworkMessage_RejectsUnknownTypeReference()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);

        using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp, 1024 * 1024);
        writer.WriteNetworkSerializable(new WorldGenerationMessage(state));

        // Az első akadály típushivatkozásának átírása egy nem létező sorszámra.
        byte[] bytes = writer.ToArray();
        int typeIndexOffset = FindFirstObstacleTypeIndexOffset(state);
        bytes[typeIndexOffset] = 200;

        using FastBufferReader reader = new FastBufferReader(bytes, Allocator.Temp);

        Assert.Throws<InvalidOperationException>(
            () => reader.ReadNetworkSerializable(out WorldGenerationMessage _));
    }

    /// <summary>
    /// Az üzenetformátum szerint: 5 egész fejléc, játékosok (darabszám + egészek),
    /// bázisok (darabszám + 2+4+4+1+4 bájt), típustáblázat (darabszám + karakterláncok),
    /// majd az akadályok darabszáma.
    /// </summary>
    private static int FindFirstObstacleTypeIndexOffset(WorldGenerationState state)
    {
        int offset = 5 * 4;
        offset += 4 + state.playerIds.Length * 4;
        offset += 4 + state.bases.Length * (2 + 4 + 4 + 1 + 4);

        System.Collections.Generic.List<string> table = new System.Collections.Generic.List<string>();

        foreach (TerrainFeatureState feature in state.obstacles)
        {
            if (!table.Contains(feature.typeId))
                table.Add(feature.typeId);
        }

        foreach (TerrainFeatureState feature in state.decorations)
        {
            if (!table.Contains(feature.typeId))
                table.Add(feature.typeId);
        }

        offset += 4;

        // Egybájtos karakterlánc: 4 bájtos hossz + a karakterek.
        foreach (string typeId in table)
            offset += 4 + typeId.Length;

        return offset + 4;
    }

    [Test]
    public void NetworkMessage_RejectsDecorationScaleOutOfRange()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);
        state.decorations[0].scale = WorldGenerationMessage.MaxDecorationScale + 0.1f;

        using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp, 1024 * 1024);

        Assert.Throws<InvalidOperationException>(
            () => writer.WriteNetworkSerializable(new WorldGenerationMessage(state)));
    }

    [Test]
    public void NetworkMessage_KeepsDecorationsAtWorldEdges()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);
        float half = state.worldSize * 0.5f;

        state.decorations[0].x = -half;
        state.decorations[0].y = half;

        using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp, 1024 * 1024);
        writer.WriteNetworkSerializable(new WorldGenerationMessage(state));

        using FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp);
        reader.ReadNetworkSerializable(out WorldGenerationMessage received);

        Assert.AreEqual(-half, received.State.decorations[0].x, 0.001f);
        Assert.AreEqual(half, received.State.decorations[0].y, 0.001f);
        Assert.DoesNotThrow(() => WorldLayoutSnapshot.FromState(received.State));
    }

    [Test]
    public void NetworkMessage_RejectsTooLongTypeId()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);
        state.obstacles[0].typeId = new string('x', WorldGenerationMessage.MaxTypeIdLength + 1);

        using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp, 1024 * 1024);

        Assert.Throws<InvalidOperationException>(
            () => writer.WriteNetworkSerializable(new WorldGenerationMessage(state)));
    }

    [Test]
    public void NetworkMessage_RejectsNonAsciiTypeId()
    {
        WorldGenerationState state = WorldLayoutSnapshot.ToState(_layout);
        state.obstacles[0].typeId = "szakadék";

        using FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp, 1024 * 1024);

        Assert.Throws<InvalidOperationException>(
            () => writer.WriteNetworkSerializable(new WorldGenerationMessage(state)));
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
