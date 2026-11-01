using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldGeneratorTests
{
    private WorldGenerationConfig _config;
    private WorldGenerator _generator;

    [SetUp]
    public void SetUp()
    {
        _config = WorldGenerationTestConfigs.Default();
        _generator = new WorldGenerator(_config);
    }

    private static int[] PlayerIds(int count)
    {
        int[] ids = new int[count];

        for (int i = 0; i < count; i++)
            ids[i] = i + 1;

        return ids;
    }

    private WorldGenerationRequest MinimalRequest(int players, int seed)
    {
        return new WorldGenerationRequest(
            PlayerIds(players),
            _generator.Rules.GetMinWorldSize(players),
            _generator.Rules.GetMinYellowBaseCount(players),
            0,
            seed);
    }

    private WorldGenerationRequest MaximalRequest(int players, int seed)
    {
        int size = _generator.Rules.MaxWorldSize;

        return new WorldGenerationRequest(
            PlayerIds(players),
            size,
            _generator.Rules.GetYellowBaseCountRange(players, size).Max,
            0,
            seed);
    }

    private WorldLayout GenerateOrFail(WorldGenerationRequest request)
    {
        WorldGenerationResult result = _generator.Generate(request);

        Assert.IsTrue(result.Success, result.ErrorMessage);
        Assert.IsNotNull(result.Layout);

        return result.Layout;
    }

    private void AssertLayoutIsValid(WorldLayout layout)
    {
        IReadOnlyList<BaseLayout> bases = layout.Bases;

        for (int i = 0; i < bases.Count; i++)
        {
            Assert.IsTrue(
                layout.Bounds.Contains(bases[i].Position, _config.BaseClearRadius),
                $"{bases[i]} túl közel van a pálya széléhez (size={layout.WorldSize}, seed={layout.Seed}).");

            for (int j = i + 1; j < bases.Count; j++)
            {
                float distance = Vector2.Distance(bases[i].Position, bases[j].Position);

                Assert.GreaterOrEqual(
                    distance,
                    _config.MinBaseDistance,
                    $"{bases[i]} és {bases[j]} túl közel van (seed={layout.Seed}).");
            }
        }
    }

    // Determinizmus

    [Test]
    public void SameRequest_ProducesIdenticalLayout()
    {
        WorldLayout a = GenerateOrFail(MaximalRequest(6, 1234));
        WorldLayout b = GenerateOrFail(MaximalRequest(6, 1234));

        Assert.AreEqual(a.Bases.Count, b.Bases.Count);

        for (int i = 0; i < a.Bases.Count; i++)
        {
            Assert.AreEqual(a.Bases[i].BaseId, b.Bases[i].BaseId);
            Assert.AreEqual(a.Bases[i].Position, b.Bases[i].Position);
            Assert.AreEqual(a.Bases[i].OwnerType, b.Bases[i].OwnerType);
            Assert.AreEqual(a.Bases[i].PlayerId, b.Bases[i].PlayerId);
        }
    }

    [Test]
    public void DifferentSeeds_ProduceDifferentLayouts()
    {
        WorldLayout a = GenerateOrFail(MaximalRequest(4, 1));
        WorldLayout b = GenerateOrFail(MaximalRequest(4, 2));

        Assert.AreNotEqual(a.Bases[0].Position, b.Bases[0].Position);
        Assert.AreNotEqual(a.Bases[a.Bases.Count - 1].Position, b.Bases[b.Bases.Count - 1].Position);
    }

    /// <summary>
    /// Regressziós teszt: ha elbukik, ugyanabból a seedből már más világ
    /// generálódik. Ilyenkor a WorldGenerator.Version értékét is növelni kell,
    /// majd ezt az elvárt értéket frissíteni.
    /// </summary>
    [Test]
    public void KnownRequest_ProducesKnownLayout()
    {
        WorldGenerationRequest request = new WorldGenerationRequest(
            new[] { 1, 2, 3 }, 200, 5, 0, 42);

        WorldLayout layout = GenerateOrFail(request);

        Assert.AreEqual(1, WorldGenerator.Version);
        Assert.AreEqual(ExpectedKnownLayoutFingerprint, Fingerprint(layout));
    }

    private const string ExpectedKnownLayoutFingerprint = 
        "1:2705,-5728;2:3839,5030;3:-6145,478;4:-2230,8792;" +
        "5:-2829,-3049;6:1639,-8209;7:5131,-8180;8:-8630,-6269;";

    private static string Fingerprint(WorldLayout layout)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        foreach (BaseLayout baseLayout in layout.Bases)
        {
            builder.Append(baseLayout.BaseId).Append(':')
                .Append(Mathf.RoundToInt(baseLayout.Position.x * 100f)).Append(',')
                .Append(Mathf.RoundToInt(baseLayout.Position.y * 100f)).Append(';');
        }

        return builder.ToString();
    }

    // Érvényesség

    [Test]
    public void MinimalWorlds_AreValidForEveryPlayerCount([Values(1, 2, 3, 5, 8, 10)] int players)
    {
        for (int seed = 0; seed < 20; seed++)
            AssertLayoutIsValid(GenerateOrFail(MinimalRequest(players, seed)));
    }

    [Test]
    public void MaximalWorlds_FitAllBases([Values(1, 4, 10)] int players)
    {
        for (int seed = 0; seed < 5; seed++)
        {
            WorldLayout layout = GenerateOrFail(MaximalRequest(players, seed));

            Assert.AreEqual(_config.MaxBaseCount, layout.Bases.Count);
            AssertLayoutIsValid(layout);
        }
    }

    [Test]
    public void Layout_ContainsRequestedBaseCountsAndOwners()
    {
        WorldGenerationRequest request = MaximalRequest(5, 77);
        WorldLayout layout = GenerateOrFail(request);

        int playerBases = 0;
        int yellowBases = 0;

        foreach (BaseLayout baseLayout in layout.Bases)
        {
            if (baseLayout.OwnerType == BaseOwnerType.Player)
                playerBases++;
            else if (baseLayout.OwnerType == BaseOwnerType.Yellow)
                yellowBases++;
            else
                Assert.Fail($"Váratlan tulajdonos: {baseLayout}");
        }

        Assert.AreEqual(5, playerBases);
        Assert.AreEqual(request.YellowBaseCount, yellowBases);
    }

    [Test]
    public void Layout_HasSequentialUniqueBaseIds()
    {
        WorldLayout layout = GenerateOrFail(MinimalRequest(4, 5));

        for (int i = 0; i < layout.Bases.Count; i++)
            Assert.AreEqual(i + 1, layout.Bases[i].BaseId);
    }

    [Test]
    public void Layout_KeepsRequestAndVersionForSaving()
    {
        WorldGenerationRequest request = MinimalRequest(3, 99);
        WorldLayout layout = GenerateOrFail(request);

        Assert.AreSame(request, layout.Request);
        Assert.AreEqual(99, layout.Seed);
        Assert.AreEqual(request.WorldSize, layout.WorldSize);
        Assert.AreEqual(WorldGenerator.Version, layout.GeneratorVersion);
    }

    // Kezdőbázisok

    [Test]
    public void StartingBases_AreAssignedInAscendingPlayerIdOrder()
    {
        WorldGenerationRequest request = new WorldGenerationRequest(
            new[] { 7, 3, 5 },
            _generator.Rules.GetMinWorldSize(3),
            3,
            0,
            11);

        WorldLayout layout = GenerateOrFail(request);

        Assert.AreEqual(3, layout.Bases[0].PlayerId);
        Assert.AreEqual(5, layout.Bases[1].PlayerId);
        Assert.AreEqual(7, layout.Bases[2].PlayerId);
    }

    [Test]
    public void StartingBases_LieOnRingWithinJitter([Values(2, 5, 10)] int players)
    {
        WorldLayout layout = GenerateOrFail(MinimalRequest(players, 3));
        float ringRadius = WorldGenerator.GetPlayerRingRadius(_config, layout.Bounds);

        for (int i = 0; i < players; i++)
        {
            float distanceFromCenter = layout.Bases[i].Position.magnitude;

            Assert.That(
                distanceFromCenter,
                Is.InRange(ringRadius - _config.StartPositionJitter - 0.001f,
                           ringRadius + _config.StartPositionJitter + 0.001f));
        }
    }

    [Test]
    public void StartingBases_AreEvenlySpacedAroundRing([Values(3, 6, 10)] int players)
    {
        WorldGenerationConfig noJitter = WorldGenerationTestConfigs.Create(startPositionJitter: 0f);
        WorldGenerator generator = new WorldGenerator(noJitter);

        WorldGenerationRequest request = new WorldGenerationRequest(
            PlayerIds(players),
            generator.Rules.GetMinWorldSize(players),
            generator.Rules.GetMinYellowBaseCount(players),
            0,
            21);

        WorldGenerationResult result = generator.Generate(request);
        Assert.IsTrue(result.Success, result.ErrorMessage);

        float expectedStep = 360f / players;

        for (int i = 0; i < players; i++)
        {
            Vector2 current = result.Layout.Bases[i].Position;
            Vector2 next = result.Layout.Bases[(i + 1) % players].Position;

            float step = Vector2.SignedAngle(current, next);

            Assert.AreEqual(expectedStep, step, 0.01f, $"i={i}");
        }
    }

    // Hibakezelés

    [Test]
    public void InvalidRequest_ReturnsErrorWithoutThrowing()
    {
        WorldGenerationRequest request = new WorldGenerationRequest(
            PlayerIds(4), _generator.Rules.MaxWorldSize + 10, 4, 0, 1);

        WorldGenerationResult result = _generator.Generate(request);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(WorldGenerationError.InvalidRequest, result.Error);
        Assert.IsNotEmpty(result.ErrorMessage);
        Assert.IsNull(result.Layout);
        Assert.AreEqual(0, result.Attempts);
    }

    [Test]
    public void TooManyPlayers_ReturnsInvalidRequest()
    {
        WorldGenerationRequest request = new WorldGenerationRequest(
            PlayerIds(11), _generator.Rules.MaxWorldSize, 20, 0, 1);

        Assert.AreEqual(WorldGenerationError.InvalidRequest, _generator.Generate(request).Error);
    }

    [Test]
    public void ImpossiblePlacement_FailsAfterMaxAttempts()
    {
        // Közel a véletlen elhelyezés elméleti határához, próbálkozás nélkül:
        // a méretezési szabály még elfogadja, de az elhelyezés nem sikerülhet.
        WorldGenerationConfig crowded = WorldGenerationTestConfigs.Create(
            targetFillRatio: 0.5f,
            maxObstacleCount: 0,
            maxPlacementAttemptsPerBase: 1,
            maxGenerationAttempts: 3);

        WorldGenerator generator = new WorldGenerator(crowded);
        int size = generator.Rules.MaxWorldSize;

        WorldGenerationRequest request = new WorldGenerationRequest(
            PlayerIds(2), size, generator.Rules.GetYellowBaseCountRange(2, size).Max, 0, 5);

        WorldGenerationResult result = generator.Generate(request);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(WorldGenerationError.PlacementFailed, result.Error);
        Assert.AreEqual(3, result.Attempts);
        StringAssert.Contains("seed: 5", result.ErrorMessage);
        Assert.IsNull(result.Layout);
    }

    [Test]
    public void Generate_NullRequest_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _generator.Generate(null));
    }

    // Kérés

    [Test]
    public void Request_RejectsDuplicatePlayerIds()
    {
        Assert.Throws<ArgumentException>(
            () => new WorldGenerationRequest(new[] { 1, 2, 1 }, 200, 3, 0, 1));
    }

    [Test]
    public void Request_RejectsNegativePlayerIds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorldGenerationRequest(new[] { 1, -2 }, 200, 3, 0, 1));
    }

    [Test]
    public void Request_CopiesPlayerIds()
    {
        int[] ids = { 1, 2 };
        WorldGenerationRequest request = new WorldGenerationRequest(ids, 200, 3, 0, 1);

        ids[0] = 99;

        Assert.AreEqual(1, request.PlayerIds[0]);
    }
}
