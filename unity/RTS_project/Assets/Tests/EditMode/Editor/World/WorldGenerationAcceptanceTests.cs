using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A világgenerálás elfogadási tesztjei a projekt tényleges beállításaival
/// (Data/WorldGeneration/WorldGenerationSettings.asset): nagyszámú seed,
/// minden játékosszám, kis, közepes és legnagyobb pálya, maximális
/// bázis- és akadályszám. Hosszabb futásúak, ezért „Slow” kategóriájúak.
/// </summary>
[Category("Slow")]
public sealed class WorldGenerationAcceptanceTests
{
    private const string SettingsAssetPath = "Assets/Data/WorldGeneration/WorldGenerationSettings.asset";
    private const int SeedCount = 520;
    private const float Tolerance = 0.001f;

    private WorldGenerationConfig _config;
    private TerrainGenerationConfig _terrain;
    private WorldGenerator _generator;
    private Dictionary<string, TerrainFeatureType> _types;

    [SetUp]
    public void SetUp()
    {
        WorldGenerationSettings settings = AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(SettingsAssetPath);
        Assert.IsNotNull(settings, SettingsAssetPath);

        _config = settings.ToConfig();
        _terrain = _config.Terrain;
        Assert.IsNotNull(_terrain, "A beállítás-asset nem tartalmaz tereptárgy-konfigurációt.");

        _generator = new WorldGenerator(_config);
        _types = new Dictionary<string, TerrainFeatureType>(StringComparer.OrdinalIgnoreCase);

        foreach (TerrainFeatureType type in _terrain.ObstacleTypes)
            _types[type.Id] = type;

        foreach (TerrainFeatureType type in _terrain.DecorationTypes)
            _types[type.Id] = type;
    }

    [Test]
    public void ManySeeds_ProducePlayableWorlds()
    {
        List<string> problems = new List<string>();
        long totalMilliseconds = 0;
        long worstMilliseconds = 0;
        int worlds = 0;

        for (int seed = 0; seed < SeedCount; seed++)
        {
            int players = 1 + seed % 10;
            WorldGenerationRequest request = CreateStressRequest(players, (seed / 10) % 3, seed);

            Stopwatch stopwatch = Stopwatch.StartNew();
            WorldGenerationResult result = _generator.Generate(request);
            stopwatch.Stop();

            totalMilliseconds += stopwatch.ElapsedMilliseconds;
            worstMilliseconds = Math.Max(worstMilliseconds, stopwatch.ElapsedMilliseconds);
            worlds++;

            string context = $"seed={seed}, játékosok={players}, méret={request.WorldSize}";

            if (!result.Success)
            {
                problems.Add($"{context}: sikertelen generálás: {result.ErrorMessage}");
                continue;
            }

            CheckLayout(result.Layout, context, problems);

            if (problems.Count > 20)
                break;
        }

        UnityEngine.Debug.Log(
            $"Elfogadási teszt: {worlds} világ, átlag {(double)totalMilliseconds / worlds:0.0} ms, " +
            $"leglassabb {worstMilliseconds} ms");

        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void LargestWorld_GenerationTimeIsAcceptable()
    {
        const int runs = 50;
        const long limitMilliseconds = 500;

        int size = _generator.Rules.MaxWorldSize;
        List<long> times = new List<long>(runs);

        for (int seed = 0; seed < runs; seed++)
        {
            WorldGenerationRequest request = CreateStressRequest(10, 2, 10000 + seed);

            Stopwatch stopwatch = Stopwatch.StartNew();
            WorldGenerationResult result = _generator.Generate(request);
            stopwatch.Stop();

            Assert.IsTrue(result.Success, result.ErrorMessage);
            Assert.AreEqual(size, result.Layout.WorldSize);
            times.Add(stopwatch.ElapsedMilliseconds);
        }

        times.Sort();
        long total = 0;

        foreach (long time in times)
            total += time;

        long median = times[times.Count / 2];
        long worst = times[times.Count - 1];

        UnityEngine.Debug.Log(
            $"Legnagyobb világ ({size}x{size}, 150 bázis, {_generator.Rules.GetObstacleCountRange(size).Max} akadály, " +
            $"{_generator.GetRequestedDecorationCount(size)} dekoráció), {runs} futás: " +
            $"átlag {(double)total / runs:0.0} ms, medián {median} ms, leglassabb {worst} ms");

        Assert.Less(worst, limitMilliseconds);
    }

    /// <summary>
    /// sizeKind: 0 = a játékosszámhoz tartozó legkisebb, 1 = közepes, 2 = legnagyobb pálya.
    /// A bázis- és akadályszám mindig a pályamérethez tartozó maximum.
    /// </summary>
    private WorldGenerationRequest CreateStressRequest(int players, int sizeKind, int seed)
    {
        IntRange sizeRange = _generator.Rules.GetWorldSizeRange(players);

        int size = sizeKind switch
        {
            0 => sizeRange.Min,
            1 => (sizeRange.Min + sizeRange.Max) / 2,
            _ => sizeRange.Max
        };

        int[] playerIds = new int[players];

        for (int i = 0; i < players; i++)
            playerIds[i] = i;

        return new WorldGenerationRequest(
            playerIds,
            size,
            _generator.Rules.GetYellowBaseCountRange(players, size).Max,
            _generator.Rules.GetObstacleCountRange(size).Max,
            seed);
    }

    private void CheckLayout(WorldLayout layout, string context, List<string> problems)
    {
        if (!_generator.AreBasesReachable(layout))
            problems.Add($"{context}: elérhetetlen bázis");

        CheckBases(layout, context, problems);
        CheckObstacles(layout, context, problems);
        CheckDecorations(layout, context, problems);
    }

    private void CheckBases(WorldLayout layout, string context, List<string> problems)
    {
        IReadOnlyList<BaseLayout> bases = layout.Bases;

        for (int i = 0; i < bases.Count; i++)
        {
            if (!layout.Bounds.Contains(bases[i].Position, _config.BaseClearRadius - Tolerance))
                problems.Add($"{context}: {bases[i]} túl közel a pálya széléhez");

            for (int j = i + 1; j < bases.Count; j++)
            {
                if (Vector2.Distance(bases[i].Position, bases[j].Position) < _config.MinBaseDistance - Tolerance)
                    problems.Add($"{context}: {bases[i]} és {bases[j]} átfedi egymást");
            }

            // A kötelező pontok (kijáratok, foglalási terület) egyik bázis tiltott
            // területére sem eshetnek.
            foreach (Vector2 accessPoint in _terrain.BaseAccessPoints)
            {
                Vector2 point = bases[i].Position + accessPoint;

                foreach (BaseLayout other in bases)
                {
                    if (_terrain.BaseFootprint.Contains(point - other.Position))
                        problems.Add($"{context}: {bases[i]} kijáratát {other} fedi");
                }
            }
        }
    }

    private void CheckObstacles(WorldLayout layout, string context, List<string> problems)
    {
        IReadOnlyList<TerrainFeatureLayout> obstacles = layout.Obstacles;
        float baseDistance = _config.BaseClearRadius + _terrain.MinObstacleBaseDistance;

        if (obstacles.Count != layout.Request.ObstacleCount)
            problems.Add($"{context}: {obstacles.Count} akadály a kért {layout.Request.ObstacleCount} helyett");

        for (int i = 0; i < obstacles.Count; i++)
        {
            Footprint footprint = Footprint(obstacles[i]);

            if (!layout.Bounds.Contains(obstacles[i].Position + footprint.HalfExtents) ||
                !layout.Bounds.Contains(obstacles[i].Position - footprint.HalfExtents))
            {
                problems.Add($"{context}: {obstacles[i]} kilóg a pályáról");
            }

            foreach (BaseLayout baseLayout in layout.Bases)
            {
                Vector2 local = baseLayout.Position - obstacles[i].Position;

                if (footprint.Contains(local) || footprint.DistanceToEdge(local) < baseDistance - Tolerance)
                    problems.Add($"{context}: {obstacles[i]} fedi vagy túl közel van: {baseLayout}");

                foreach (Vector2 accessPoint in _terrain.BaseAccessPoints)
                {
                    if (footprint.Contains(baseLayout.Position + accessPoint - obstacles[i].Position))
                        problems.Add($"{context}: {obstacles[i]} fedi {baseLayout} kijáratát");
                }
            }

            for (int j = i + 1; j < obstacles.Count; j++)
            {
                float distance = global::Footprint.Distance(
                    footprint, obstacles[i].Position, Footprint(obstacles[j]), obstacles[j].Position);

                if (distance < _terrain.MinObstacleSpacing - Tolerance)
                    problems.Add($"{context}: {obstacles[i]} és {obstacles[j]} túl közel");
            }
        }
    }

    private void CheckDecorations(WorldLayout layout, string context, List<string> problems)
    {
        foreach (TerrainFeatureLayout decoration in layout.Decorations)
        {
            Footprint footprint = Footprint(decoration);

            foreach (BaseLayout baseLayout in layout.Bases)
            {
                if (Vector2.Distance(baseLayout.Position, decoration.Position) < _config.BaseClearRadius - Tolerance)
                    problems.Add($"{context}: {decoration} a bázis szabad területén: {baseLayout}");
            }

            foreach (TerrainFeatureLayout obstacle in layout.Obstacles)
            {
                if (global::Footprint.Distance(footprint, decoration.Position, Footprint(obstacle), obstacle.Position) <= 0f)
                    problems.Add($"{context}: {decoration} fedi: {obstacle}");
            }
        }
    }

    private Footprint Footprint(TerrainFeatureLayout feature)
    {
        return _types[feature.TypeId].Footprint.Scaled(feature.Scale);
    }
}
