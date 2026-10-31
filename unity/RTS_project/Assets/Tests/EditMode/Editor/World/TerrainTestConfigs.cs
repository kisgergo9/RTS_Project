using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Teszt-konfigurációk a tereptárgyak egységtesztjeihez.
/// </summary>
internal static class TerrainTestConfigs
{
    public static TerrainFeatureType Obstacle(
        string id = "rock",
        float halfWidth = 4f,
        float halfHeight = 1.5f,
        float minScale = 1f,
        float maxScale = 1f,
        float weight = 1f)
    {
        return new TerrainFeatureType(
            id, true, Footprint.Ellipse(halfWidth, halfHeight), minScale, maxScale, weight, 1);
    }

    public static TerrainFeatureType Decoration(string id = "tree")
    {
        return new TerrainFeatureType(id, false, Footprint.Ellipse(0.4f, 0.2f), 1f, 1f, 1f, 3);
    }

    /// <summary>
    /// A Base prefab Blocker sokszögének egyszerűsített mása.
    /// </summary>
    public static Footprint BaseFootprint()
    {
        return Footprint.Ellipse(5.2f, 1.9f);
    }

    public static Vector2[] BaseAccessPoints()
    {
        return new[] { new Vector2(-5.5f, -1.2f), new Vector2(-4f, -2f) };
    }

    public static TerrainGenerationConfig Create(
        IEnumerable<TerrainFeatureType> obstacleTypes = null,
        IEnumerable<TerrainFeatureType> decorationTypes = null,
        Footprint baseFootprint = null,
        IEnumerable<Vector2> baseAccessPoints = null,
        float minObstacleBaseDistance = 2f,
        float minObstacleSpacing = 4f,
        float minPassageWidth = 4f,
        float walkabilityCellSize = 1f,
        int maxPlacementAttemptsPerObstacle = 100,
        int maxObstacleGenerationAttempts = 5,
        float decorationsPer10000Area = 6f)
    {
        return new TerrainGenerationConfig(
            obstacleTypes ?? new[] { Obstacle() },
            decorationTypes ?? new[] { Decoration() },
            baseFootprint ?? BaseFootprint(),
            baseAccessPoints ?? BaseAccessPoints(),
            minObstacleBaseDistance,
            minObstacleSpacing,
            minPassageWidth,
            walkabilityCellSize,
            maxPlacementAttemptsPerObstacle,
            maxObstacleGenerationAttempts,
            decorationsPer10000Area);
    }
}
