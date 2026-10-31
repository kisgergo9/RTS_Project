using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldGeneratorDecorationTests
{
    private TerrainGenerationConfig _terrain;
    private WorldGenerationConfig _config;
    private WorldGenerator _generator;

    [SetUp]
    public void SetUp()
    {
        _terrain = TerrainTestConfigs.Create(
            obstacleTypes: new[] { TerrainTestConfigs.Obstacle("rock", 4f, 1.5f, 0.9f, 1.1f, 1f) },
            decorationTypes: new[]
            {
                TerrainTestConfigs.Decoration("palm"),
                new TerrainFeatureType("bush", false, Footprint.Ellipse(0.6f, 0.4f), 0.8f, 1.2f, 0.5f, 2)
            },
            decorationsPer10000Area: 6f);

        _config = WorldGenerationTestConfigs.Create(terrain: _terrain);
        _generator = new WorldGenerator(_config);
    }

    private static int[] PlayerIds(int count)
    {
        int[] ids = new int[count];

        for (int i = 0; i < count; i++)
            ids[i] = i + 1;

        return ids;
    }

    private WorldGenerationRequest MaxRequest(WorldGenerator generator, int players, int size, int seed)
    {
        return new WorldGenerationRequest(
            PlayerIds(players),
            size,
            generator.Rules.GetMinYellowBaseCount(players),
            generator.Rules.GetObstacleCountRange(size).Max,
            seed);
    }

    private static WorldLayout GenerateOrFail(WorldGenerator generator, WorldGenerationRequest request)
    {
        WorldGenerationResult result = generator.Generate(request);

        Assert.IsTrue(result.Success, result.ErrorMessage);

        return result.Layout;
    }

    private TerrainFeatureType DecorationType(string id)
    {
        foreach (TerrainFeatureType type in _terrain.DecorationTypes)
        {
            if (type.Id == id)
                return type;
        }

        Assert.Fail($"Ismeretlen dekorációtípus: {id}");
        return null;
    }

    private TerrainFeatureType ObstacleType(string id)
    {
        foreach (TerrainFeatureType type in _terrain.ObstacleTypes)
        {
            if (type.Id == id)
                return type;
        }

        Assert.Fail($"Ismeretlen akadálytípus: {id}");
        return null;
    }

    // Darabszám

    [TestCase(100, 6)]
    [TestCase(140, 12)]
    [TestCase(530, 169)]
    public void RequestedCount_FollowsDensity(int size, int expected)
    {
        Assert.AreEqual(expected, _generator.GetRequestedDecorationCount(size));
    }

    [Test]
    public void SpaciousWorld_PlacesAllRequestedDecorations()
    {
        int size = _generator.Rules.MaxWorldSize;
        WorldLayout layout = GenerateOrFail(_generator, MaxRequest(_generator, 4, size, 3));

        Assert.AreEqual(_generator.GetRequestedDecorationCount(size), layout.Decorations.Count);
    }

    [Test]
    public void ZeroDensity_PlacesNoDecorations()
    {
        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Create(
            terrain: TerrainTestConfigs.Create(decorationsPer10000Area: 0f)));

        WorldLayout layout = GenerateOrFail(generator, MaxRequest(generator, 2, generator.Rules.MaxWorldSize, 1));

        Assert.AreEqual(0, layout.Decorations.Count);
    }

    [Test]
    public void NoDecorationTypes_PlacesNoDecorations()
    {
        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Create(
            terrain: TerrainTestConfigs.Create(decorationTypes: new TerrainFeatureType[0])));

        WorldLayout layout = GenerateOrFail(generator, MaxRequest(generator, 2, generator.Rules.MaxWorldSize, 1));

        Assert.AreEqual(0, layout.Decorations.Count);
        Assert.AreEqual(0, generator.GetRequestedDecorationCount(generator.Rules.MaxWorldSize));
    }

    [Test]
    public void WithoutTerrain_PlacesNoDecorations()
    {
        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Create());
        WorldLayout layout = GenerateOrFail(generator, new WorldGenerationRequest(PlayerIds(2), 200, 2, 0, 1));

        Assert.AreEqual(0, layout.Decorations.Count);
    }

    [Test]
    public void CrowdedWorld_SkipsDecorationsInsteadOfFailing()
    {
        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Create(
            terrain: TerrainTestConfigs.Create(
                decorationsPer10000Area: 5000f,
                maxPlacementAttemptsPerObstacle: 5)));

        int size = generator.Rules.GetMinWorldSize(2);
        WorldGenerationResult result = generator.Generate(MaxRequest(generator, 2, size, 7));

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.Greater(result.Layout.Decorations.Count, 0);
        Assert.Less(result.Layout.Decorations.Count, generator.GetRequestedDecorationCount(size));
    }

    // Determinizmus és függetlenség

    [Test]
    public void SameRequest_ProducesSameDecorations()
    {
        WorldGenerationRequest request = MaxRequest(_generator, 3, _generator.Rules.MaxWorldSize, 13);

        WorldLayout a = GenerateOrFail(_generator, request);
        WorldLayout b = GenerateOrFail(_generator, request);

        Assert.AreEqual(a.Decorations.Count, b.Decorations.Count);

        for (int i = 0; i < a.Decorations.Count; i++)
        {
            Assert.AreEqual(a.Decorations[i].TypeId, b.Decorations[i].TypeId);
            Assert.AreEqual(a.Decorations[i].Variant, b.Decorations[i].Variant);
            Assert.AreEqual(a.Decorations[i].Position, b.Decorations[i].Position);
            Assert.AreEqual(a.Decorations[i].Scale, b.Decorations[i].Scale);
        }
    }

    [Test]
    public void Decorations_DoNotChangeBasesOrObstacles()
    {
        WorldGenerator withoutDecorations = new WorldGenerator(WorldGenerationTestConfigs.Create(
            terrain: TerrainTestConfigs.Create(
                obstacleTypes: _terrain.ObstacleTypes,
                decorationTypes: _terrain.DecorationTypes,
                decorationsPer10000Area: 0f)));

        int size = _generator.Rules.MaxWorldSize;
        WorldLayout plain = GenerateOrFail(withoutDecorations, MaxRequest(withoutDecorations, 5, size, 21));
        WorldLayout decorated = GenerateOrFail(_generator, MaxRequest(_generator, 5, size, 21));

        Assert.Greater(decorated.Decorations.Count, 0);

        for (int i = 0; i < plain.Bases.Count; i++)
            Assert.AreEqual(plain.Bases[i].Position, decorated.Bases[i].Position);

        Assert.AreEqual(plain.Obstacles.Count, decorated.Obstacles.Count);

        for (int i = 0; i < plain.Obstacles.Count; i++)
            Assert.AreEqual(plain.Obstacles[i].Position, decorated.Obstacles[i].Position);
    }

    [Test]
    public void Decorations_DoNotAffectWalkability()
    {
        WorldLayout layout = GenerateOrFail(_generator, MaxRequest(_generator, 4, _generator.Rules.MaxWorldSize, 9));
        WorldLayout withoutDecorations = new WorldLayout(
            layout.GeneratorVersion, layout.Request, layout.Bases, layout.Obstacles);

        Assert.AreEqual(
            _generator.BuildWalkabilityGrid(withoutDecorations).CountBlocked(),
            _generator.BuildWalkabilityGrid(layout).CountBlocked());
        Assert.IsTrue(_generator.AreBasesReachable(layout));
    }

    // Érvényesség

    [Test]
    public void Decorations_AreValid()
    {
        WorldLayout layout = GenerateOrFail(_generator, MaxRequest(_generator, 6, _generator.Rules.MaxWorldSize, 31));
        IReadOnlyList<TerrainFeatureLayout> decorations = layout.Decorations;

        Assert.Greater(decorations.Count, 0);

        for (int i = 0; i < decorations.Count; i++)
        {
            TerrainFeatureLayout decoration = decorations[i];
            TerrainFeatureType type = DecorationType(decoration.TypeId);
            Footprint footprint = type.Footprint.Scaled(decoration.Scale);

            Assert.That(decoration.Variant, Is.InRange(0, type.VariantCount - 1));
            Assert.That(decoration.Scale, Is.InRange(type.MinScale, type.MaxScale));

            // Pályán belül.
            Assert.IsTrue(layout.Bounds.Contains(decoration.Position + footprint.HalfExtents), decoration.ToString());
            Assert.IsTrue(layout.Bounds.Contains(decoration.Position - footprint.HalfExtents), decoration.ToString());

            // A bázisok szabad területén kívül.
            foreach (BaseLayout baseLayout in layout.Bases)
            {
                Vector2 local = baseLayout.Position - decoration.Position;

                Assert.IsFalse(footprint.Contains(local), $"{decoration} a bázison: {baseLayout}");
                Assert.GreaterOrEqual(footprint.DistanceToEdge(local), _config.BaseClearRadius - 0.001f,
                    $"{decoration} túl közel: {baseLayout}");
            }

            // Nem fedi az akadályokat.
            foreach (TerrainFeatureLayout obstacle in layout.Obstacles)
            {
                Footprint obstacleFootprint = ObstacleType(obstacle.TypeId).Footprint.Scaled(obstacle.Scale);

                Assert.Greater(
                    Footprint.Distance(footprint, decoration.Position, obstacleFootprint, obstacle.Position),
                    0f,
                    $"{decoration} fedi: {obstacle}");
            }

            // Nem fedik egymást.
            for (int j = i + 1; j < decorations.Count; j++)
            {
                Footprint other = DecorationType(decorations[j].TypeId).Footprint.Scaled(decorations[j].Scale);
                float distance = Vector2.Distance(decoration.Position, decorations[j].Position);

                Assert.GreaterOrEqual(distance, footprint.BoundingRadius + other.BoundingRadius - 0.001f,
                    $"{decoration} és {decorations[j]}");
            }
        }
    }
}
