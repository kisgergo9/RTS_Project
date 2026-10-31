using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class TerrainConfigTests
{
    private const string SettingsAssetPath = "Assets/Data/WorldGeneration/WorldGenerationSettings.asset";

    // Footprint

    [Test]
    public void Footprint_RejectsTooFewPoints()
    {
        Assert.Throws<ArgumentException>(
            () => new Footprint(new[] { Vector2.zero, Vector2.one }));
    }

    [Test]
    public void Footprint_RejectsDegeneratePolygon()
    {
        Assert.Throws<ArgumentException>(
            () => new Footprint(new[] { Vector2.zero, Vector2.one, new Vector2(2f, 2f) }));
    }

    [Test]
    public void Ellipse_HasExpectedExtents()
    {
        Footprint ellipse = Footprint.Ellipse(5f, 2f);

        Assert.AreEqual(5f, ellipse.HalfExtents.x, 0.001f);
        Assert.AreEqual(2f, ellipse.HalfExtents.y, 0.001f);
        Assert.AreEqual(5f, ellipse.BoundingRadius, 0.001f);
    }

    [Test]
    public void Contains_DistinguishesInsideAndOutside()
    {
        Footprint ellipse = Footprint.Ellipse(5f, 2f);

        Assert.IsTrue(ellipse.Contains(Vector2.zero));
        Assert.IsTrue(ellipse.Contains(new Vector2(4f, 0.5f)));
        Assert.IsFalse(ellipse.Contains(new Vector2(4.5f, 1.5f)));
        Assert.IsFalse(ellipse.Contains(new Vector2(0f, 2.5f)));
    }

    [Test]
    public void Contains_WorksForConcavePolygon()
    {
        // L alakú sokszög.
        Footprint shape = new Footprint(new[]
        {
            new Vector2(0f, 0f), new Vector2(4f, 0f), new Vector2(4f, 1f),
            new Vector2(1f, 1f), new Vector2(1f, 4f), new Vector2(0f, 4f)
        });

        Assert.IsTrue(shape.Contains(new Vector2(0.5f, 3f)));
        Assert.IsTrue(shape.Contains(new Vector2(3f, 0.5f)));
        Assert.IsFalse(shape.Contains(new Vector2(3f, 3f)));
    }

    [Test]
    public void Scaled_ScalesExtents()
    {
        Footprint scaled = Footprint.Ellipse(5f, 2f).Scaled(1.5f);

        Assert.AreEqual(7.5f, scaled.HalfExtents.x, 0.001f);
        Assert.AreEqual(3f, scaled.HalfExtents.y, 0.001f);
    }

    [Test]
    public void DistanceToEdge_MeasuresFromBoundary()
    {
        Footprint square = new Footprint(new[]
        {
            new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f)
        });

        Assert.AreEqual(2f, square.DistanceToEdge(new Vector2(3f, 0f)), 0.001f);
        Assert.AreEqual(1f, square.DistanceToEdge(Vector2.zero), 0.001f);
    }

    // TerrainFeatureType

    [Test]
    public void FeatureType_RejectsEmptyId()
    {
        Assert.Throws<ArgumentException>(() => TerrainTestConfigs.Obstacle(id: " "));
    }

    [Test]
    public void FeatureType_RejectsInvertedScaleRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TerrainTestConfigs.Obstacle(minScale: 1.2f, maxScale: 1f));
    }

    [Test]
    public void FeatureType_RejectsNonPositiveWeight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TerrainTestConfigs.Obstacle(weight: 0f));
    }

    [Test]
    public void FeatureType_RejectsMissingVariants()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TerrainFeatureType("x", true, Footprint.Ellipse(1f, 1f), 1f, 1f, 1f, 0));
    }

    [Test]
    public void FeatureType_MaxBoundingRadiusUsesMaxScale()
    {
        TerrainFeatureType type = TerrainTestConfigs.Obstacle(halfWidth: 4f, maxScale: 1.5f);

        Assert.AreEqual(6f, type.MaxBoundingRadius, 0.001f);
    }

    // TerrainGenerationConfig

    [Test]
    public void TerrainConfig_AcceptsValidConfiguration()
    {
        Assert.DoesNotThrow(() => TerrainTestConfigs.Create());
    }

    [Test]
    public void TerrainConfig_RejectsNonBlockingObstacleType()
    {
        Assert.Throws<ArgumentException>(() => TerrainTestConfigs.Create(
            obstacleTypes: new[] { TerrainTestConfigs.Decoration("fake") }));
    }

    [Test]
    public void TerrainConfig_RejectsBlockingDecorationType()
    {
        Assert.Throws<ArgumentException>(() => TerrainTestConfigs.Create(
            decorationTypes: new[] { TerrainTestConfigs.Obstacle("fake") }));
    }

    [Test]
    public void TerrainConfig_RejectsDuplicateIds()
    {
        Assert.Throws<ArgumentException>(() => TerrainTestConfigs.Create(
            obstacleTypes: new[] { TerrainTestConfigs.Obstacle("same") },
            decorationTypes: new[] { TerrainTestConfigs.Decoration("SAME") }));
    }

    [Test]
    public void TerrainConfig_RejectsAccessPointInsideBaseFootprint()
    {
        Assert.Throws<ArgumentException>(() => TerrainTestConfigs.Create(
            baseAccessPoints: new[] { new Vector2(0f, 0f) }));
    }

    [Test]
    public void TerrainConfig_RejectsMissingAccessPoints()
    {
        Assert.Throws<ArgumentException>(() => TerrainTestConfigs.Create(
            baseAccessPoints: Array.Empty<Vector2>()));
    }

    [Test]
    public void TerrainConfig_RejectsCellLargerThanPassage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TerrainTestConfigs.Create(
            minPassageWidth: 2f, walkabilityCellSize: 3f));
    }

    [Test]
    public void TerrainConfig_ReportsLargestObstacle()
    {
        TerrainGenerationConfig config = TerrainTestConfigs.Create(obstacleTypes: new[]
        {
            TerrainTestConfigs.Obstacle("small", halfWidth: 3f),
            TerrainTestConfigs.Obstacle("big", halfWidth: 5f, maxScale: 1.2f)
        });

        Assert.AreEqual(6f, config.MaxObstacleBoundingRadius, 0.001f);
    }

    // Összevetés a világ beállításaival

    [Test]
    public void WorldConfig_AcceptsTerrain()
    {
        WorldGenerationConfig config = WorldGenerationTestConfigs.Create(
            terrain: TerrainTestConfigs.Create());

        Assert.IsNotNull(config.Terrain);
    }

    [Test]
    public void WorldConfig_WithoutTerrain_HasNullTerrain()
    {
        Assert.IsNull(WorldGenerationTestConfigs.Create().Terrain);
    }

    [Test]
    public void WorldConfig_RejectsBaseFootprintLargerThanClearRadius()
    {
        Assert.Throws<ArgumentException>(() => WorldGenerationTestConfigs.Create(
            baseClearRadius: 4f,
            absoluteMinWorldSize: 80,
            terrain: TerrainTestConfigs.Create()));
    }

    [Test]
    public void WorldConfig_RejectsAccessPointOutsideClearRadius()
    {
        Assert.Throws<ArgumentException>(() => WorldGenerationTestConfigs.Create(
            terrain: TerrainTestConfigs.Create(
                baseAccessPoints: new[] { new Vector2(-12f, 0f) })));
    }

    [Test]
    public void WorldConfig_RejectsObstacleLargerThanReservedSpace()
    {
        Assert.Throws<ArgumentException>(() => WorldGenerationTestConfigs.Create(
            obstacleClearRadius: 5f,
            terrain: TerrainTestConfigs.Create(
                obstacleTypes: new[] { TerrainTestConfigs.Obstacle(halfWidth: 6f) })));
    }

    // A projekt beállítás-assetje

    [Test]
    public void SettingsAsset_ProvidesHillsChasmsOasisAndPalms()
    {
        WorldGenerationSettings settings =
            AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(SettingsAssetPath);

        Assert.IsNotNull(settings, SettingsAssetPath);

        TerrainGenerationConfig terrain = settings.ToConfig().Terrain;

        Assert.IsNotNull(terrain);
        CollectionAssert.AreEquivalent(
            new[] { "hill", "chasm", "oasis" },
            new[] { terrain.ObstacleTypes[0].Id, terrain.ObstacleTypes[1].Id, terrain.ObstacleTypes[2].Id });
        Assert.AreEqual(1, terrain.DecorationTypes.Count);
        Assert.AreEqual("palm", terrain.DecorationTypes[0].Id);
    }

    [Test]
    public void SettingsAsset_DefinitionsHaveSpritesForEveryVariant()
    {
        WorldGenerationSettings settings =
            AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(SettingsAssetPath);

        foreach (TerrainFeatureDefinition definition in settings.ObstacleDefinitions)
        {
            for (int i = 0; i < definition.VariantCount; i++)
                Assert.IsNotNull(definition.GetSprite(i), $"{definition.Id} #{i}");
        }

        foreach (TerrainFeatureDefinition definition in settings.DecorationDefinitions)
        {
            for (int i = 0; i < definition.VariantCount; i++)
                Assert.IsNotNull(definition.GetSprite(i), $"{definition.Id} #{i}");
        }
    }

    [Test]
    public void SettingsAsset_BaseFootprintBlocksCenterButNotExits()
    {
        WorldGenerationSettings settings =
            AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(SettingsAssetPath);

        TerrainGenerationConfig terrain = settings.ToConfig().Terrain;

        Assert.IsTrue(terrain.BaseFootprint.Contains(Vector2.zero));

        foreach (Vector2 point in terrain.BaseAccessPoints)
            Assert.IsFalse(terrain.BaseFootprint.Contains(point), point.ToString());
    }
}
