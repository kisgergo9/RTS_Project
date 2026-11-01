using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldGeneratorObstacleTests
{
    private TerrainGenerationConfig _terrain;
    private WorldGenerationConfig _config;
    private WorldGenerator _generator;

    [SetUp]
    public void SetUp()
    {
        _terrain = TerrainTestConfigs.Create(obstacleTypes: new[]
        {
            TerrainTestConfigs.Obstacle("rock", 4f, 1.5f, 0.9f, 1.1f, 1f),
            TerrainTestConfigs.Obstacle("ridge", 5f, 1.5f, 0.9f, 1.15f, 2f)
        });

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

    private WorldGenerationRequest Request(int players, int size, int obstacles, int seed)
    {
        return new WorldGenerationRequest(
            PlayerIds(players), size, _generator.Rules.GetMinYellowBaseCount(players), obstacles, seed);
    }

    private WorldGenerationRequest MaxObstacleRequest(int players, int size, int seed)
    {
        return Request(players, size, _generator.Rules.GetObstacleCountRange(size).Max, seed);
    }

    private WorldLayout GenerateOrFail(WorldGenerator generator, WorldGenerationRequest request)
    {
        WorldGenerationResult result = generator.Generate(request);

        Assert.IsTrue(result.Success, result.ErrorMessage);

        return result.Layout;
    }

    private TerrainFeatureType TypeOf(TerrainFeatureLayout obstacle)
    {
        foreach (TerrainFeatureType type in _terrain.ObstacleTypes)
        {
            if (type.Id == obstacle.TypeId)
                return type;
        }

        Assert.Fail($"Ismeretlen típus: {obstacle.TypeId}");
        return null;
    }

    // Kérés és konfiguráció

    [Test]
    public void ObstaclesWithoutTerrainConfig_AreRejected()
    {
        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Create());
        int size = generator.Rules.MaxWorldSize;

        WorldGenerationResult result = generator.Generate(
            new WorldGenerationRequest(PlayerIds(2), size, 2, 5, 1));

        Assert.AreEqual(WorldGenerationError.InvalidRequest, result.Error);
        Assert.AreEqual(0, result.Attempts);
    }

    [Test]
    public void ZeroObstacles_ProducesNoObstacles()
    {
        WorldLayout layout = GenerateOrFail(_generator, Request(4, _generator.Rules.MaxWorldSize, 0, 3));

        Assert.AreEqual(0, layout.Obstacles.Count);
    }

    // Determinizmus

    [Test]
    public void Obstacles_DoNotMoveBases()
    {
        int size = _generator.Rules.MaxWorldSize;

        WorldLayout without = GenerateOrFail(_generator, Request(6, size, 0, 77));
        WorldLayout with = GenerateOrFail(_generator, MaxObstacleRequest(6, size, 77));

        Assert.AreEqual(without.Bases.Count, with.Bases.Count);

        for (int i = 0; i < without.Bases.Count; i++)
            Assert.AreEqual(without.Bases[i].Position, with.Bases[i].Position, $"base {i + 1}");
    }

    [Test]
    public void Obstacles_DoNotMoveBasesComparedToConfigWithoutTerrain()
    {
        WorldGenerator plain = new WorldGenerator(WorldGenerationTestConfigs.Create());
        int size = plain.Rules.MaxWorldSize;

        WorldLayout without = GenerateOrFail(plain, Request(3, size, 0, 5));
        WorldLayout with = GenerateOrFail(_generator, MaxObstacleRequest(3, size, 5));

        for (int i = 0; i < without.Bases.Count; i++)
            Assert.AreEqual(without.Bases[i].Position, with.Bases[i].Position, $"base {i + 1}");
    }

    [Test]
    public void SameRequest_ProducesSameObstacles()
    {
        WorldGenerationRequest request = MaxObstacleRequest(4, _generator.Rules.MaxWorldSize, 9);

        WorldLayout a = GenerateOrFail(_generator, request);
        WorldLayout b = GenerateOrFail(_generator, request);

        Assert.AreEqual(a.Obstacles.Count, b.Obstacles.Count);

        for (int i = 0; i < a.Obstacles.Count; i++)
        {
            Assert.AreEqual(a.Obstacles[i].TypeId, b.Obstacles[i].TypeId);
            Assert.AreEqual(a.Obstacles[i].Variant, b.Obstacles[i].Variant);
            Assert.AreEqual(a.Obstacles[i].Position, b.Obstacles[i].Position);
            Assert.AreEqual(a.Obstacles[i].Scale, b.Obstacles[i].Scale);
        }
    }

    [Test]
    public void DifferentSeeds_ProduceDifferentObstacles()
    {
        int size = _generator.Rules.MaxWorldSize;

        WorldLayout a = GenerateOrFail(_generator, MaxObstacleRequest(4, size, 1));
        WorldLayout b = GenerateOrFail(_generator, MaxObstacleRequest(4, size, 2));

        Assert.AreNotEqual(a.Obstacles[0].Position, b.Obstacles[0].Position);
    }

    // Érvényesség

    [Test]
    public void Obstacles_HaveRequestedCountKnownTypesAndValidVariation()
    {
        WorldGenerationRequest request = MaxObstacleRequest(5, _generator.Rules.MaxWorldSize, 11);
        WorldLayout layout = GenerateOrFail(_generator, request);

        Assert.AreEqual(request.ObstacleCount, layout.Obstacles.Count);

        foreach (TerrainFeatureLayout obstacle in layout.Obstacles)
        {
            TerrainFeatureType type = TypeOf(obstacle);

            Assert.That(obstacle.Variant, Is.InRange(0, type.VariantCount - 1));
            Assert.That(obstacle.Scale, Is.InRange(type.MinScale, type.MaxScale));
        }
    }

    [Test]
    public void Obstacles_StayInsideWorld()
    {
        WorldLayout layout = GenerateOrFail(_generator, MaxObstacleRequest(4, _generator.Rules.MaxWorldSize, 21));

        foreach (TerrainFeatureLayout obstacle in layout.Obstacles)
        {
            Vector2 extents = TypeOf(obstacle).Footprint.HalfExtents * obstacle.Scale;

            Assert.IsTrue(layout.Bounds.Contains(obstacle.Position + extents), obstacle.ToString());
            Assert.IsTrue(layout.Bounds.Contains(obstacle.Position - extents), obstacle.ToString());
        }
    }

    [Test]
    public void Obstacles_KeepDistanceFromBases()
    {
        WorldLayout layout = GenerateOrFail(_generator, MaxObstacleRequest(8, _generator.Rules.MaxWorldSize, 31));
        float required = _config.BaseClearRadius + _terrain.MinObstacleBaseDistance;

        foreach (TerrainFeatureLayout obstacle in layout.Obstacles)
        {
            Footprint footprint = TypeOf(obstacle).Footprint.Scaled(obstacle.Scale);

            foreach (BaseLayout baseLayout in layout.Bases)
            {
                Vector2 local = baseLayout.Position - obstacle.Position;

                Assert.IsFalse(footprint.Contains(local), $"{obstacle} fedi: {baseLayout}");
                Assert.GreaterOrEqual(footprint.DistanceToEdge(local), required - 0.001f,
                    $"{obstacle} túl közel: {baseLayout}");
            }
        }
    }

    [Test]
    public void Obstacles_KeepSpacingFromEachOther()
    {
        WorldLayout layout = GenerateOrFail(_generator, MaxObstacleRequest(2, _generator.Rules.MaxWorldSize, 41));
        IReadOnlyList<TerrainFeatureLayout> obstacles = layout.Obstacles;

        for (int i = 0; i < obstacles.Count; i++)
        {
            Footprint a = TypeOf(obstacles[i]).Footprint.Scaled(obstacles[i].Scale);

            for (int j = i + 1; j < obstacles.Count; j++)
            {
                Footprint b = TypeOf(obstacles[j]).Footprint.Scaled(obstacles[j].Scale);
                float distance = Footprint.Distance(a, obstacles[i].Position, b, obstacles[j].Position);

                Assert.GreaterOrEqual(distance, _terrain.MinObstacleSpacing - 0.001f,
                    $"{obstacles[i]} és {obstacles[j]}");
            }
        }
    }

    [Test]
    public void AllBasesReachable_InSmallestAndLargestWorlds([Values(1, 2, 5, 10)] int players)
    {
        int[] sizes = { _generator.Rules.GetMinWorldSize(players), _generator.Rules.MaxWorldSize };

        foreach (int size in sizes)
        {
            for (int seed = 0; seed < 5; seed++)
            {
                WorldLayout layout = GenerateOrFail(_generator, MaxObstacleRequest(players, size, seed));

                Assert.IsTrue(_generator.AreBasesReachable(layout), $"size={size}, seed={seed}");
            }
        }
    }

    [Test]
    public void WeightedChoice_FollowsWeights()
    {
        TerrainGenerationConfig terrain = TerrainTestConfigs.Create(obstacleTypes: new[]
        {
            TerrainTestConfigs.Obstacle("common", 3f, 1.5f, 1f, 1f, 1f),
            TerrainTestConfigs.Obstacle("rare", 3f, 1.5f, 1f, 1f, 0.001f)
        });

        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Create(terrain: terrain));
        int size = generator.Rules.MaxWorldSize;

        WorldLayout layout = GenerateOrFail(generator, new WorldGenerationRequest(
            PlayerIds(2), size, 2, generator.Rules.GetObstacleCountRange(size).Max, 8));

        int common = 0;

        foreach (TerrainFeatureLayout obstacle in layout.Obstacles)
        {
            if (obstacle.TypeId == "common")
                common++;
        }

        Assert.GreaterOrEqual(common, layout.Obstacles.Count - 1);
    }

    // Elérhetőség

    [Test]
    public void AreBasesReachable_DetectsWallAcrossMap()
    {
        TerrainGenerationConfig terrain = TerrainTestConfigs.Create(obstacleTypes: new[]
        {
            TerrainTestConfigs.Obstacle("post", 2f, 2f)
        });

        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Create(terrain: terrain));
        WorldGenerationRequest request = new WorldGenerationRequest(new[] { 1 }, 100, 1, 0, 1);

        BaseLayout[] bases =
        {
            new BaseLayout(1, new Vector2(-30f, 0f), BaseOwnerType.Player, 1),
            new BaseLayout(2, new Vector2(30f, 0f), BaseOwnerType.Yellow, 0)
        };

        List<TerrainFeatureLayout> wall = new List<TerrainFeatureLayout>();

        for (float y = -48f; y <= 48f; y += 3f)
            wall.Add(new TerrainFeatureLayout("post", 0, new Vector2(0f, y), 1f));

        WorldLayout open = new WorldLayout(WorldGenerator.Version, request, bases);
        WorldLayout blocked = new WorldLayout(WorldGenerator.Version, request, bases, wall);

        Assert.IsTrue(generator.AreBasesReachable(open));
        Assert.IsFalse(generator.AreBasesReachable(blocked));
    }

    [Test]
    public void AreBasesReachable_DetectsNarrowPassageAsClosed()
    {
        // Két oszlop a fal két felén, köztük a minimális átjárónál (4) keskenyebb rés.
        TerrainGenerationConfig terrain = TerrainTestConfigs.Create(obstacleTypes: new[]
        {
            TerrainTestConfigs.Obstacle("post", 2f, 2f)
        });

        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Create(terrain: terrain));
        WorldGenerationRequest request = new WorldGenerationRequest(new[] { 1 }, 100, 1, 0, 1);

        BaseLayout[] bases =
        {
            new BaseLayout(1, new Vector2(-30f, 0f), BaseOwnerType.Player, 1),
            new BaseLayout(2, new Vector2(30f, 0f), BaseOwnerType.Yellow, 0)
        };

        List<TerrainFeatureLayout> narrow = new List<TerrainFeatureLayout>();
        List<TerrainFeatureLayout> wide = new List<TerrainFeatureLayout>();

        for (float y = -48f; y <= 48f; y += 3f)
        {
            // Keskeny rés: a középső oszlopok (y = ±3,5, sugár 2) széle között 3 egység (< 4).
            if (Mathf.Abs(y) > 2f)
                narrow.Add(new TerrainFeatureLayout("post", 0, new Vector2(0f, y + Mathf.Sign(y) * 0.5f), 1f));

            // Széles rés: az oszlopok széle között 12 egység.
            if (Mathf.Abs(y) > 8f)
                wide.Add(new TerrainFeatureLayout("post", 0, new Vector2(0f, y), 1f));
        }

        Assert.IsFalse(generator.AreBasesReachable(new WorldLayout(WorldGenerator.Version, request, bases, narrow)));
        Assert.IsTrue(generator.AreBasesReachable(new WorldLayout(WorldGenerator.Version, request, bases, wide)));
    }

    [Test]
    public void BuildWalkabilityGrid_BlocksBasesObstaclesAndBorder()
    {
        WorldLayout layout = GenerateOrFail(_generator, MaxObstacleRequest(4, _generator.Rules.MaxWorldSize, 5));
        WalkabilityGrid grid = _generator.BuildWalkabilityGrid(layout);

        foreach (BaseLayout baseLayout in layout.Bases)
            Assert.IsFalse(grid.IsWalkable(baseLayout.Position), baseLayout.ToString());

        foreach (TerrainFeatureLayout obstacle in layout.Obstacles)
            Assert.IsFalse(grid.IsWalkable(obstacle.Position), obstacle.ToString());

        Assert.IsFalse(grid.IsWalkable(0, 0));
        Assert.IsFalse(grid.IsWalkable(grid.Width - 1, grid.Height / 2));
    }

    // Hibakezelés

    [Test]
    public void ImpossibleObstaclePlacement_FailsClearly()
    {
        // A bázisoktól megkövetelt távolság miatt sehol sincs érvényes hely.
        TerrainGenerationConfig terrain = TerrainTestConfigs.Create(
            minObstacleBaseDistance: 500f,
            maxPlacementAttemptsPerObstacle: 10,
            maxObstacleGenerationAttempts: 3);

        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Create(terrain: terrain));
        int size = generator.Rules.MaxWorldSize;

        WorldGenerationResult result = generator.Generate(new WorldGenerationRequest(
            PlayerIds(2), size, 2, 5, 4));

        Assert.IsFalse(result.Success);
        Assert.AreEqual(WorldGenerationError.PlacementFailed, result.Error);
        StringAssert.Contains("akadály", result.ErrorMessage);
        StringAssert.Contains("seed: 4", result.ErrorMessage);
        Assert.IsNull(result.Layout);
    }
}
