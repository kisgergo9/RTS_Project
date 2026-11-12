using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class YellowBaseStrengthTests
{
    private static List<Vector2> Line(int count, float spacing = 10f)
    {
        List<Vector2> positions = new List<Vector2>(count);

        for (int i = 0; i < count; i++)
            positions.Add(new Vector2((i + 1) * spacing, 0f));

        return positions;
    }

    private static readonly Vector2[] PlayerAtOrigin = { Vector2.zero };

    // Kiosztás

    [Test]
    public void StrengthAt_SpreadsEvenlyFrom1To10()
    {
        int[] expected = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        for (int i = 0; i < 10; i++)
            Assert.AreEqual(expected[i], YellowBaseStrength.StrengthAt(i, 10));

        CollectionAssert.AreEqual(new[] { 1, 3, 6, 8, 10 },
            Array.ConvertAll(new[] { 0, 1, 2, 3, 4 }, i => YellowBaseStrength.StrengthAt(i, 5)));
        Assert.AreEqual(1, YellowBaseStrength.StrengthAt(0, 1));
    }

    [Test]
    public void WithoutJitter_NearBasesAreWeakest()
    {
        int[] strengths = YellowBaseStrength.Assign(Line(10), PlayerAtOrigin, 0f, new DeterministicRandom(1));

        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, strengths);
    }

    [Test]
    public void EveryStrengthFrom1To10_IsUsedForTenBases([Values(1, 2, 3, 4, 5)] int seed)
    {
        int[] strengths = YellowBaseStrength.Assign(Line(10), PlayerAtOrigin, 0.2f, new DeterministicRandom(seed));

        CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, strengths);
    }

    [Test]
    public void ManyBases_ShareStrengthsEvenly()
    {
        int[] strengths = YellowBaseStrength.Assign(Line(140), PlayerAtOrigin, 0.2f, new DeterministicRandom(7));
        int[] counts = new int[YellowBaseStrength.Max + 1];

        foreach (int strength in strengths)
            counts[strength]++;

        Assert.AreEqual(0, counts[0]);

        for (int strength = YellowBaseStrength.Min; strength <= YellowBaseStrength.Max; strength++)
            Assert.That(counts[strength], Is.InRange(7, 16), $"strength={strength}");
    }

    [Test]
    public void NearestBaseToPlayers_IsNeverStrong([Values(2, 5, 10, 30, 140)] int count)
    {
        for (int seed = 0; seed < 200; seed++)
        {
            int[] strengths = YellowBaseStrength.Assign(Line(count), PlayerAtOrigin, 0.2f, new DeterministicRandom(seed));

            Assert.LessOrEqual(strengths[0], 5, $"count={count}, seed={seed}");
        }
    }

    [Test]
    public void Jitter_ChangesOrderBetweenSeeds()
    {
        HashSet<string> orders = new HashSet<string>();

        for (int seed = 0; seed < 20; seed++)
            orders.Add(string.Join(",", YellowBaseStrength.Assign(Line(10), PlayerAtOrigin, 0.2f, new DeterministicRandom(seed))));

        Assert.Greater(orders.Count, 1);
    }

    [Test]
    public void SameInput_GivesSameStrengths()
    {
        int[] a = YellowBaseStrength.Assign(Line(25), PlayerAtOrigin, 0.2f, new DeterministicRandom(99));
        int[] b = YellowBaseStrength.Assign(Line(25), PlayerAtOrigin, 0.2f, new DeterministicRandom(99));

        CollectionAssert.AreEqual(a, b);
    }

    [Test]
    public void Assign_HandlesEmptyAndRejectsInvalidJitter()
    {
        Assert.AreEqual(0, YellowBaseStrength.Assign(new List<Vector2>(), PlayerAtOrigin, 0.2f, new DeterministicRandom(1)).Length);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => YellowBaseStrength.Assign(Line(3), PlayerAtOrigin, 1.5f, new DeterministicRandom(1)));
    }

    // BaseLayout

    [Test]
    public void BaseLayout_AllowsStrengthOnlyOnYellowBases()
    {
        Assert.AreEqual(7, new BaseLayout(1, Vector2.zero, BaseOwnerType.Yellow, 0, 7).Strength);
        Assert.AreEqual(0, new BaseLayout(1, Vector2.zero, BaseOwnerType.Player, 1).Strength);
        Assert.Throws<ArgumentException>(() => new BaseLayout(1, Vector2.zero, BaseOwnerType.Player, 1, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BaseLayout(1, Vector2.zero, BaseOwnerType.Yellow, 0, 11));
    }

    // A generátorban

    [Test]
    public void GeneratedWorld_GivesEveryYellowBaseAStrength([Values(1, 2, 3)] int seed)
    {
        WorldGenerator generator = new WorldGenerator(WorldGenerationTestConfigs.Default());
        WorldGenerationResult result = generator.Generate(new WorldGenerationRequest(
            new[] { 1, 2, 3 }, generator.Rules.MaxWorldSize, 40, 0, seed));

        Assert.IsTrue(result.Success, result.ErrorMessage);

        List<int> strengths = new List<int>();

        foreach (BaseLayout baseLayout in result.Layout.Bases)
        {
            if (baseLayout.OwnerType == BaseOwnerType.Yellow)
                strengths.Add(baseLayout.Strength);
            else
                Assert.AreEqual(0, baseLayout.Strength);
        }

        Assert.AreEqual(40, strengths.Count);
        Assert.Contains(YellowBaseStrength.Min, strengths);
        Assert.Contains(YellowBaseStrength.Max, strengths);
    }

    [Test]
    public void GeneratedWorld_StrengthsDoNotMoveBases()
    {
        WorldGenerationRequest request = new WorldGenerationRequest(new[] { 1, 2 }, 200, 8, 0, 5);

        WorldLayout calm = new WorldGenerator(WorldGenerationTestConfigs.Create(yellowStrengthJitter: 0f)).Generate(request).Layout;
        WorldLayout mixed = new WorldGenerator(WorldGenerationTestConfigs.Create(yellowStrengthJitter: 1f)).Generate(request).Layout;

        for (int i = 0; i < calm.Bases.Count; i++)
            Assert.AreEqual(calm.Bases[i].Position, mixed.Bases[i].Position);
    }

    // Turretek

    [Test]
    public void TurretCount_GrowsFrom2To12WithStrength()
    {
        WorldGenerationConfig config = WorldGenerationTestConfigs.Create();
        int[] expected = { 2, 3, 4, 5, 6, 8, 9, 10, 11, 12 };

        for (int strength = 1; strength <= 10; strength++)
            Assert.AreEqual(expected[strength - 1], config.GetYellowTurretCount(strength), $"strength={strength}");

        Assert.AreEqual(2, config.GetYellowTurretCount(0), "Ismeretlen erősség: a legkisebb érték.");
    }

    [Test]
    public void TurretCount_UsesConfiguredRange()
    {
        WorldGenerationConfig config = WorldGenerationTestConfigs.Create(minYellowTurretCount: 4, maxYellowTurretCount: 4);

        Assert.AreEqual(4, config.GetYellowTurretCount(1));
        Assert.AreEqual(4, config.GetYellowTurretCount(10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorldGenerationTestConfigs.Create(minYellowTurretCount: 5, maxYellowTurretCount: 4));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorldGenerationTestConfigs.Create(minYellowTurretCount: -1));
    }

    [Test]
    public void SettingsAsset_Uses2To12Turrets()
    {
        WorldGenerationConfig config = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(
            "Assets/Data/WorldGeneration/WorldGenerationSettings.asset").ToConfig();

        Assert.AreEqual(2, config.MinYellowTurretCount);
        Assert.AreEqual(12, config.MaxYellowTurretCount);
    }

    [TestCase(0)]
    [TestCase(2)]
    [TestCase(7)]
    [TestCase(12)]
    public void ShowTurrets_EnablesTheFirstTurretsOfTheBasePrefab(int count)
    {
        BaseController prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<BaseController>("Assets/Prefabs/Bases/Base.prefab");
        BaseController instance = UnityEngine.Object.Instantiate(prefab);

        try
        {
            typeof(WorldSceneBuilder)
                .GetMethod("ShowTurrets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, new object[] { instance, count });

            Transform turrets = instance.transform.Find("Turrets");
            Assert.AreEqual(count > 0, turrets.gameObject.activeSelf);

            for (int number = 1; number <= 12; number++)
                Assert.AreEqual(number <= count, turrets.Find($"Turret_{number}").gameObject.activeSelf, $"Turret_{number}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance.gameObject);
        }
    }

    [Test]
    public void Config_RejectsInvalidJitter()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldGenerationTestConfigs.Create(yellowStrengthJitter: -0.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldGenerationTestConfigs.Create(yellowStrengthJitter: 1.1f));
    }
}
