using UnityEngine;

/// <summary>
/// Teszt-konfigurációk a világgenerálás egységtesztjeihez.
/// </summary>
internal static class WorldGenerationTestConfigs
{
    /// <summary>
    /// A WorldGenerationSettings ScriptableObject alapértékei.
    /// </summary>
    public static WorldGenerationConfig Default()
    {
        WorldGenerationSettings settings =
            ScriptableObject.CreateInstance<WorldGenerationSettings>();

        try
        {
            return settings.ToConfig();
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    public static WorldGenerationConfig Create(
        int minPlayerCount = 1,
        int maxPlayerCount = 10,
        int maxBaseCount = 150,
        float minBaseDistance = 24f,
        float baseClearRadius = 9f,
        int minYellowBaseCount = 1,
        float yellowBasesPerPlayer = 1f,
        int minObstacleCount = 0,
        int maxObstacleCount = 50,
        float obstacleClearRadius = 8f,
        int absoluteMinWorldSize = 80,
        int worldSizeStep = 10,
        float targetFillRatio = 0.3f,
        int maxPlacementAttemptsPerBase = 200,
        int maxGenerationAttempts = 10,
        float playerRingRadiusFraction = 0.7f,
        float startPositionJitter = 2f,
        TerrainGenerationConfig terrain = null)
    {
        return new WorldGenerationConfig(
            minPlayerCount,
            maxPlayerCount,
            maxBaseCount,
            minBaseDistance,
            baseClearRadius,
            minYellowBaseCount,
            yellowBasesPerPlayer,
            minObstacleCount,
            maxObstacleCount,
            obstacleClearRadius,
            absoluteMinWorldSize,
            worldSizeStep,
            targetFillRatio,
            maxPlacementAttemptsPerBase,
            maxGenerationAttempts,
            playerRingRadiusFraction,
            startPositionJitter,
            terrain);
    }
}
