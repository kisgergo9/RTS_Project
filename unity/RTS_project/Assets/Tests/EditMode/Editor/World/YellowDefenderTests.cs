using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class YellowDefenderTests
{
    private static readonly Vector2[] Anchors =
    {
        new Vector2(-7.5f, 0.5f), new Vector2(-7.5f, -3.5f), new Vector2(-1f, -4.5f)
    };

    private static YellowDefenderConfig Defenders(
        int minBasePoints = 15,
        int maxBasePoints = 60,
        int minSquadPoints = 5,
        float fraction = 1f,
        IEnumerable<Vector2> anchors = null)
    {
        return new YellowDefenderConfig(
            new[] { new DefenderUnitType("tank", 5), new DefenderUnitType("bigtank", 10) },
            minBasePoints,
            maxBasePoints,
            minSquadPoints,
            fraction,
            anchors ?? Anchors);
    }

    private static WorldGenerator Generator(YellowDefenderConfig defenders)
    {
        return new WorldGenerator(WorldGenerationTestConfigs.Create(defenders: defenders));
    }

    private static WorldLayout Generate(WorldGenerator generator, int players, int seed, int yellow = 20)
    {
        int size = generator.Rules.MaxWorldSize;
        WorldGenerationResult result = generator.Generate(new WorldGenerationRequest(
            BuildPlayerIds(players), size, yellow, 0, seed));

        Assert.IsTrue(result.Success, result.ErrorMessage);
        return result.Layout;
    }

    private static int[] BuildPlayerIds(int count)
    {
        int[] ids = new int[count];

        for (int i = 0; i < count; i++)
            ids[i] = i + 1;

        return ids;
    }

    private static Dictionary<int, BaseLayout> BasesById(WorldLayout layout)
    {
        Dictionary<int, BaseLayout> bases = new Dictionary<int, BaseLayout>();

        foreach (BaseLayout baseLayout in layout.Bases)
            bases[baseLayout.BaseId] = baseLayout;

        return bases;
    }

    // Bázisonként, csapatonként csoportosítva.
    private static Dictionary<int, SortedDictionary<int, List<DefenderLayout>>> BySquad(WorldLayout layout)
    {
        Dictionary<int, SortedDictionary<int, List<DefenderLayout>>> result =
            new Dictionary<int, SortedDictionary<int, List<DefenderLayout>>>();

        foreach (DefenderLayout defender in layout.Defenders)
        {
            if (!result.TryGetValue(defender.BaseId, out SortedDictionary<int, List<DefenderLayout>> squads))
                result[defender.BaseId] = squads = new SortedDictionary<int, List<DefenderLayout>>();

            if (!squads.TryGetValue(defender.SquadIndex, out List<DefenderLayout> squad))
                squads[defender.SquadIndex] = squad = new List<DefenderLayout>();

            squad.Add(defender);
        }

        return result;
    }

    private static int Cost(string id)
    {
        switch (id)
        {
            case "tank": return 5;
            case "bigtank": return 10;
            default: throw new ArgumentException(id);
        }
    }

    // Egységtulajdon

    [Test]
    public void YellowUnit_RegistersWithoutAnyPlayer()
    {
        GameState state = new GameState();

        state.RegisterUnit(new UnitState(1, BaseOwnerType.Yellow, 0, UnitType.Ground));

        Assert.AreEqual(1, state.World.UnitCount);
        Assert.IsTrue(state.World.GetUnit(1).IsYellow);
        Assert.AreEqual(0, state.World.GetUnit(1).OwnerPlayerId);
    }

    [Test]
    public void PlayerUnit_StillRequiresRegisteredPlayer()
    {
        GameState state = new GameState();

        Assert.Throws<InvalidOperationException>(() => state.RegisterUnit(new UnitState(1, 7, UnitType.Ground)));

        state.RegisterPlayer(new PlayerState(7, "Hét"));
        state.RegisterUnit(new UnitState(2, 7, UnitType.Ground));

        Assert.AreEqual(BaseOwnerType.Player, state.World.GetUnit(2).OwnerType);
    }

    [Test]
    public void UnitState_RejectsUnitWithoutOwner()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UnitState(1, BaseOwnerType.None, 0, UnitType.Ground));
    }

    [Test]
    public void UnitState_IgnoresPlayerIdForYellowUnits()
    {
        Assert.AreEqual(0, new UnitState(1, BaseOwnerType.Yellow, 42, UnitType.Ground).OwnerPlayerId);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new UnitState(1, BaseOwnerType.Player, -1, UnitType.Ground));
    }

    // Konfiguráció

    [Test]
    public void BasePoints_GrowFrom15To60WithStrength()
    {
        YellowDefenderConfig config = Defenders();

        Assert.AreEqual(15, config.GetBasePoints(1));
        Assert.AreEqual(60, config.GetBasePoints(10));
        Assert.AreEqual(35, config.GetBasePoints(5));
        Assert.AreEqual(15, config.GetBasePoints(0), "Ismeretlen erősség: a legkisebb érték.");

        for (int strength = 2; strength <= 10; strength++)
            Assert.Greater(config.GetBasePoints(strength), config.GetBasePoints(strength - 1));
    }

    [Test]
    public void SquadCount_IsAtMostThreeAndEverySquadGetsTheMinimum()
    {
        YellowDefenderConfig config = Defenders();

        Assert.AreEqual(3, config.GetSquadCount(15));
        Assert.AreEqual(3, config.GetSquadCount(60));
        Assert.AreEqual(2, config.GetSquadCount(14));
        Assert.AreEqual(1, config.GetSquadCount(5));
    }

    [Test]
    public void Config_RejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Defenders(minBasePoints: 30, maxBasePoints: 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => Defenders(minBasePoints: 4, minSquadPoints: 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => Defenders(fraction: 1.5f));
        Assert.Throws<ArgumentException>(() => Defenders(anchors: Array.Empty<Vector2>()));
        Assert.Throws<ArgumentException>(() => new YellowDefenderConfig(
            new[] { new DefenderUnitType("a", 5), new DefenderUnitType("A", 6) }, 15, 60, 5, 1f, Anchors));
    }

    [Test]
    public void Config_RejectsSquadMinimumSmallerThanCheapestUnit()
    {
        Assert.Throws<ArgumentException>(() => new YellowDefenderConfig(
            new[] { new DefenderUnitType("bigtank", 10) }, 15, 60, 5, 1f, Anchors));
    }

    [Test]
    public void WorldConfig_RejectsAnchorOutsideBaseClearArea()
    {
        Assert.Throws<ArgumentException>(() => WorldGenerationTestConfigs.Create(
            defenders: Defenders(anchors: new[] { new Vector2(0f, -20f) })));
    }

    [Test]
    public void WorldConfig_RejectsAnchorInsideBaseFootprint()
    {
        Assert.Throws<ArgumentException>(() => WorldGenerationTestConfigs.Create(
            terrain: TerrainTestConfigs.Create(),
            defenders: Defenders(anchors: new[] { Vector2.zero })));
    }

    // Pontok felosztása

    [Test]
    public void SplitPoints_GivesEverySquadTheMinimumAndKeepsTheTotal([Values(15, 16, 37, 60)] int points)
    {
        for (int seed = 0; seed < 50; seed++)
        {
            int[] shares = WorldGenerator.SplitPoints(points, 3, 5, new DeterministicRandom(seed));
            int sum = 0;

            foreach (int share in shares)
            {
                Assert.GreaterOrEqual(share, 5);
                sum += share;
            }

            Assert.AreEqual(points, sum, $"seed={seed}");
        }
    }

    [Test]
    public void SplitPoints_IsRandom()
    {
        HashSet<string> splits = new HashSet<string>();

        for (int seed = 0; seed < 20; seed++)
            splits.Add(string.Join(",", WorldGenerator.SplitPoints(60, 3, 5, new DeterministicRandom(seed))));

        Assert.Greater(splits.Count, 5);
    }

    // Generálás

    [Test]
    public void WithoutDefenderConfig_NoDefenders()
    {
        WorldLayout layout = Generate(new WorldGenerator(WorldGenerationTestConfigs.Create()), 2, 1);

        Assert.AreEqual(0, layout.Defenders.Count);
    }

    [Test]
    public void EveryYellowBaseGetsThreeNonEmptySquads_WhenFractionIsOne()
    {
        WorldLayout layout = Generate(Generator(Defenders()), 3, 5);
        Dictionary<int, SortedDictionary<int, List<DefenderLayout>>> squads = BySquad(layout);

        foreach (BaseLayout baseLayout in layout.Bases)
        {
            if (baseLayout.OwnerType != BaseOwnerType.Yellow)
            {
                Assert.IsFalse(squads.ContainsKey(baseLayout.BaseId), $"Játékosbázisnak nincs védőcsapata: {baseLayout}");
                continue;
            }

            Assert.IsTrue(squads.ContainsKey(baseLayout.BaseId), baseLayout.ToString());
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, squads[baseLayout.BaseId].Keys, baseLayout.ToString());

            foreach (List<DefenderLayout> squad in squads[baseLayout.BaseId].Values)
                Assert.Greater(squad.Count, 0);
        }
    }

    [Test]
    public void ZeroFraction_NoDefenders()
    {
        Assert.AreEqual(0, Generate(Generator(Defenders(fraction: 0f)), 3, 5).Defenders.Count);
    }

    [Test]
    public void PartialFraction_DefendsSomeYellowBases()
    {
        WorldLayout layout = Generate(Generator(Defenders(fraction: 0.5f)), 2, 9);
        int defended = BySquad(layout).Count;

        Assert.Greater(defended, 0);
        Assert.Less(defended, 20);
    }

    [Test]
    public void DefenderPoints_FollowBaseStrength([Values(1, 2, 3, 4, 5)] int seed)
    {
        YellowDefenderConfig config = Defenders();
        WorldLayout layout = Generate(Generator(config), 2, seed);
        Dictionary<int, BaseLayout> bases = BasesById(layout);

        foreach (KeyValuePair<int, SortedDictionary<int, List<DefenderLayout>>> entry in BySquad(layout))
        {
            int basePoints = config.GetBasePoints(bases[entry.Key].Strength);
            int total = 0;

            foreach (List<DefenderLayout> squad in entry.Value.Values)
            {
                int squadSum = 0;

                foreach (DefenderLayout defender in squad)
                    squadSum += Cost(defender.UnitTypeId);

                total += squadSum;
            }

            // A keret sosem lépődik túl; csapatonként legfeljebb a legolcsóbb
            // egységnél kisebb maradék vész el.
            Assert.LessOrEqual(total, basePoints, $"base {entry.Key}");
            Assert.Greater(total, basePoints - entry.Value.Count * config.CheapestCost, $"base {entry.Key}");
        }
    }

    [Test]
    public void StrongBases_GetMoreDefendersThanWeakOnes()
    {
        WorldLayout layout = Generate(Generator(Defenders()), 2, 3, yellow: 30);
        Dictionary<int, BaseLayout> bases = BasesById(layout);
        int weakest = int.MaxValue;
        int strongest = 0;

        foreach (KeyValuePair<int, SortedDictionary<int, List<DefenderLayout>>> entry in BySquad(layout))
        {
            int points = 0;

            foreach (List<DefenderLayout> squad in entry.Value.Values)
                foreach (DefenderLayout defender in squad)
                    points += Cost(defender.UnitTypeId);

            if (bases[entry.Key].Strength == 1)
                weakest = Math.Min(weakest, points);

            if (bases[entry.Key].Strength == 10)
                strongest = Math.Max(strongest, points);
        }

        Assert.LessOrEqual(weakest, 15);
        Assert.Greater(strongest, 45);
    }

    [Test]
    public void SquadMembers_StandAtTheirSquadAnchor()
    {
        WorldLayout layout = Generate(Generator(Defenders()), 2, 7);
        Dictionary<int, BaseLayout> bases = BasesById(layout);

        foreach (DefenderLayout defender in layout.Defenders)
        {
            Assert.That(defender.SquadIndex, Is.InRange(0, Anchors.Length - 1));
            Assert.AreEqual(bases[defender.BaseId].Position + Anchors[defender.SquadIndex], defender.Position);
        }
    }

    [Test]
    public void Squads_DifferBetweenBases()
    {
        WorldLayout layout = Generate(Generator(Defenders()), 2, 11);
        HashSet<string> compositions = new HashSet<string>();

        foreach (SortedDictionary<int, List<DefenderLayout>> squads in BySquad(layout).Values)
        {
            foreach (List<DefenderLayout> squad in squads.Values)
            {
                List<string> ids = new List<string>();

                foreach (DefenderLayout defender in squad)
                    ids.Add(defender.UnitTypeId);

                compositions.Add(string.Join(",", ids));
            }
        }

        Assert.Greater(compositions.Count, 1);
    }

    [Test]
    public void Defenders_AreDeterministic()
    {
        WorldGenerator generator = Generator(Defenders());
        WorldLayout a = Generate(generator, 3, 21);
        WorldLayout b = Generate(generator, 3, 21);

        Assert.AreEqual(a.Defenders.Count, b.Defenders.Count);

        for (int i = 0; i < a.Defenders.Count; i++)
        {
            Assert.AreEqual(a.Defenders[i].UnitTypeId, b.Defenders[i].UnitTypeId);
            Assert.AreEqual(a.Defenders[i].SquadIndex, b.Defenders[i].SquadIndex);
            Assert.AreEqual(a.Defenders[i].Position, b.Defenders[i].Position);
        }
    }

    [Test]
    public void Defenders_DoNotMoveBases()
    {
        WorldLayout without = Generate(new WorldGenerator(WorldGenerationTestConfigs.Create()), 4, 33);
        WorldLayout with = Generate(Generator(Defenders()), 4, 33);

        for (int i = 0; i < without.Bases.Count; i++)
            Assert.AreEqual(without.Bases[i].Position, with.Bases[i].Position);
    }

    // A projekt beállítás-assetje

    [Test]
    public void SettingsAsset_DefendersAreTanksNotApcs()
    {
        WorldGenerationSettings settings = AssetDatabase.LoadAssetAtPath<WorldGenerationSettings>(
            "Assets/Data/WorldGeneration/WorldGenerationSettings.asset");

        Assert.Greater(settings.DefenderUnits.Count, 0);

        foreach (DefenderUnitEntry entry in settings.DefenderUnits)
        {
            Assert.IsNotNull(entry.prefab, entry.id);
            Assert.AreNotEqual(UnitType.APC, entry.prefab.Type, entry.id);
        }

        YellowDefenderConfig config = settings.ToConfig().Defenders;

        Assert.IsNotNull(config);
        Assert.AreEqual(15, config.MinBasePoints);
        Assert.AreEqual(60, config.MaxBasePoints);
        Assert.AreEqual(5, config.MinSquadPoints);
        Assert.AreEqual(3, config.SquadAnchors.Count);
    }
}
