using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class YellowDefenderTests
{
    private static readonly Vector2[] Slots =
    {
        new Vector2(-2f, -3f), new Vector2(-1f, -3f), new Vector2(0f, -3f), new Vector2(1f, -3f),
        new Vector2(-2f, -4f), new Vector2(-1f, -4f), new Vector2(0f, -4f), new Vector2(1f, -4f)
    };

    private static YellowDefenderConfig Defenders(
        int minDifficulty = 20,
        int maxDifficulty = 40,
        float fraction = 1f,
        IEnumerable<Vector2> slots = null)
    {
        return new YellowDefenderConfig(
            new[] { new DefenderUnitType("light", 5), new DefenderUnitType("heavy", 10), new DefenderUnitType("medium", 7) },
            minDifficulty,
            maxDifficulty,
            fraction,
            slots ?? Slots);
    }

    private static WorldGenerator Generator(YellowDefenderConfig defenders)
    {
        return new WorldGenerator(WorldGenerationTestConfigs.Create(defenders: defenders));
    }

    private static WorldLayout Generate(WorldGenerator generator, int players, int seed)
    {
        int size = generator.Rules.MaxWorldSize;
        WorldGenerationResult result = generator.Generate(new WorldGenerationRequest(
            BuildPlayerIds(players), size, 20, 0, seed));

        Assert.IsTrue(result.Success, result.ErrorMessage);
        return result.Layout;
    }

    private static int[] BuildPlayerIds(int count)
    {
        int[] ids = new int[count];

        for (int i = 0; i < count; i++)
            ids[i] = i;

        return ids;
    }

    private static Dictionary<int, List<DefenderLayout>> BySquad(WorldLayout layout)
    {
        Dictionary<int, List<DefenderLayout>> squads = new Dictionary<int, List<DefenderLayout>>();

        foreach (DefenderLayout defender in layout.Defenders)
        {
            if (!squads.TryGetValue(defender.BaseId, out List<DefenderLayout> squad))
                squads[defender.BaseId] = squad = new List<DefenderLayout>();

            squad.Add(defender);
        }

        return squads;
    }

    private static int Cost(string id)
    {
        switch (id)
        {
            case "light": return 5;
            case "heavy": return 10;
            case "medium": return 7;
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
    public void Config_RejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Defenders(minDifficulty: 30, maxDifficulty: 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => Defenders(fraction: 1.5f));
        Assert.Throws<ArgumentException>(() => Defenders(slots: Array.Empty<Vector2>()));
        Assert.Throws<ArgumentException>(() => new YellowDefenderConfig(
            new[] { new DefenderUnitType("a", 5), new DefenderUnitType("A", 6) }, 20, 40, 1f, Slots));
    }

    [Test]
    public void Config_RejectsBudgetSmallerThanCheapestUnit()
    {
        Assert.Throws<ArgumentException>(() => new YellowDefenderConfig(
            new[] { new DefenderUnitType("heavy", 10) }, 5, 40, 1f, Slots));
    }

    [Test]
    public void WorldConfig_RejectsSlotOutsideBaseClearArea()
    {
        Assert.Throws<ArgumentException>(() => WorldGenerationTestConfigs.Create(
            defenders: Defenders(slots: new[] { new Vector2(0f, -20f) })));
    }

    [Test]
    public void WorldConfig_RejectsSlotInsideBaseFootprint()
    {
        Assert.Throws<ArgumentException>(() => WorldGenerationTestConfigs.Create(
            terrain: TerrainTestConfigs.Create(),
            defenders: Defenders(slots: new[] { Vector2.zero })));
    }

    // Generálás

    [Test]
    public void WithoutDefenderConfig_NoDefenders()
    {
        WorldLayout layout = Generate(new WorldGenerator(WorldGenerationTestConfigs.Create()), 2, 1);

        Assert.AreEqual(0, layout.Defenders.Count);
    }

    [Test]
    public void EveryYellowBaseGetsANonEmptySquad_WhenFractionIsOne()
    {
        WorldLayout layout = Generate(Generator(Defenders()), 3, 5);
        Dictionary<int, List<DefenderLayout>> squads = BySquad(layout);

        foreach (BaseLayout baseLayout in layout.Bases)
        {
            if (baseLayout.OwnerType == BaseOwnerType.Yellow)
                Assert.IsTrue(squads.ContainsKey(baseLayout.BaseId), baseLayout.ToString());
            else
                Assert.IsFalse(squads.ContainsKey(baseLayout.BaseId), $"Játékosbázisnak nincs védőcsapata: {baseLayout}");
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
        int squads = BySquad(layout).Count;

        Assert.Greater(squads, 0);
        Assert.Less(squads, 20);
    }

    [Test]
    public void SquadDifficulty_StaysWithinBudgetAndIsFilled([Values(1, 2, 3, 4, 5)] int seed)
    {
        YellowDefenderConfig config = Defenders();
        WorldLayout layout = Generate(Generator(config), 2, seed);

        foreach (KeyValuePair<int, List<DefenderLayout>> squad in BySquad(layout))
        {
            int sum = 0;

            foreach (DefenderLayout defender in squad.Value)
                sum += Cost(defender.UnitTypeId);

            Assert.LessOrEqual(sum, config.MaxSquadDifficulty, $"base {squad.Key}");

            // Csak akkor áll meg, ha már egy egység sem fér bele (vagy elfogytak a helyek):
            // a maradék kisebb a legolcsóbb egységnél.
            if (squad.Value.Count < config.Slots.Count)
                Assert.Greater(sum, config.MinSquadDifficulty - config.CheapestCost, $"base {squad.Key}");
        }
    }

    [Test]
    public void Defenders_UseFixedSlotsInFrontOfTheirBase()
    {
        WorldLayout layout = Generate(Generator(Defenders()), 2, 7);
        Dictionary<int, BaseLayout> bases = new Dictionary<int, BaseLayout>();

        foreach (BaseLayout baseLayout in layout.Bases)
            bases[baseLayout.BaseId] = baseLayout;

        foreach (KeyValuePair<int, List<DefenderLayout>> squad in BySquad(layout))
        {
            HashSet<int> usedSlots = new HashSet<int>();

            for (int i = 0; i < squad.Value.Count; i++)
            {
                DefenderLayout defender = squad.Value[i];

                Assert.AreEqual(i, defender.SlotIndex, "A helyek sorban töltődnek fel.");
                Assert.IsTrue(usedSlots.Add(defender.SlotIndex));
                Assert.AreEqual(bases[squad.Key].Position + Slots[defender.SlotIndex], defender.Position);
            }

            Assert.LessOrEqual(squad.Value.Count, Slots.Length);
        }
    }

    [Test]
    public void Squads_DifferBetweenBases()
    {
        WorldLayout layout = Generate(Generator(Defenders()), 2, 11);
        HashSet<string> compositions = new HashSet<string>();

        foreach (List<DefenderLayout> squad in BySquad(layout).Values)
        {
            List<string> ids = new List<string>();

            foreach (DefenderLayout defender in squad)
                ids.Add(defender.UnitTypeId);

            compositions.Add(string.Join(",", ids));
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

        Assert.IsNotNull(settings.ToConfig().Defenders);
    }
}
