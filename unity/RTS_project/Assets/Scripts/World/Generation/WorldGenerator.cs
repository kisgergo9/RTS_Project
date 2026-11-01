using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity-független, seed-alapú világgenerátor. Csak a host futtatja; a
/// kliensek a kész WorldLayout-ot kapják meg.
///
/// Működés:
///   1. A kérést a WorldSizingRules alapján ellenőrzi.
///   2. A kezdőbázisokat egy középpontos körre, egyenletes szögközzel,
///      növekvő játékosazonosító szerinti sorrendben helyezi el. A kör
///      elforgatása és a pozíciók kis szórása a seedből származik.
///   3. A sárga bázisokat véletlenszerű jelöltekből választja ki: egy jelölt
///      csak akkor fogadható el, ha minden bázistól legalább MinBaseDistance,
///      a pálya szélétől legalább BaseClearRadius távolságra van.
///   4. Ha egy bázis MaxPlacementAttemptsPerBase próbálkozásból sem helyezhető
///      el, a teljes generálás újraindul (legfeljebb MaxGenerationAttempts-szer),
///      ugyanazzal a folytatódó véletlensorozattal, így az eredmény továbbra is
///      determinisztikus. Ha ez sem sikerül, hibás eredményt ad vissza.
/// </summary>
public sealed class WorldGenerator
{
    /// <summary>
    /// A generálási algoritmus verziója. Növelni kell minden olyan változtatásnál,
    /// amely ugyanabból a seedből és kérésből más világot eredményez.
    /// </summary>
    public const int Version = 1;

    private const double FullCircle = 2.0 * Math.PI;

    private readonly WorldGenerationConfig _config;

    public WorldSizingRules Rules { get; }

    public WorldGenerator(WorldGenerationConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Rules = new WorldSizingRules(config);
    }

    public WorldGenerationResult Generate(WorldGenerationRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (!Rules.TryValidate(
                request.PlayerCount,
                request.WorldSize,
                request.YellowBaseCount,
                request.ObstacleCount,
                out string error))
        {
            return WorldGenerationResult.Failed(WorldGenerationError.InvalidRequest, error, 0);
        }

        WorldBounds bounds = new WorldBounds(request.WorldSize);
        DeterministicRandom random = new DeterministicRandom(request.Seed);

        List<int> playerIds = new List<int>(request.PlayerIds);
        playerIds.Sort();

        int totalBases = playerIds.Count + request.YellowBaseCount;
        List<Vector2> positions = new List<Vector2>(totalBases);

        for (int attempt = 1; attempt <= _config.MaxGenerationAttempts; attempt++)
        {
            positions.Clear();

            if (!TryPlaceStartingBases(bounds, playerIds.Count, random, positions))
                continue;

            if (!TryPlaceYellowBases(bounds, request.YellowBaseCount, random, positions))
                continue;

            return WorldGenerationResult.Succeeded(
                CreateLayout(request, playerIds, positions),
                attempt);
        }

        return WorldGenerationResult.Failed(
            WorldGenerationError.PlacementFailed,
            $"Nem sikerült elhelyezni {totalBases} bázist a {request.WorldSize} méretű " +
            $"világban {_config.MaxGenerationAttempts} generálási kísérlet alatt " +
            $"(seed: {request.Seed}). Próbálj nagyobb világot, kevesebb sárga bázist " +
            "vagy másik seedet.",
            _config.MaxGenerationAttempts);
    }

    /// <summary>
    /// A kezdőbázisok körének sugara. A körön lévő bázis a szórással együtt is
    /// legalább BaseClearRadius távolságra marad a pálya szélétől.
    /// </summary>
    public static float GetPlayerRingRadius(WorldGenerationConfig config, WorldBounds bounds)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        float available = bounds.HalfSize - config.BaseClearRadius - config.StartPositionJitter;

        return Mathf.Max(0f, available * config.PlayerRingRadiusFraction);
    }

    private bool TryPlaceStartingBases(
        WorldBounds bounds,
        int playerCount,
        DeterministicRandom random,
        List<Vector2> positions)
    {
        double ringRadius = GetPlayerRingRadius(_config, bounds);
        double rotation = random.NextDouble() * FullCircle;
        double step = FullCircle / playerCount;

        for (int i = 0; i < playerCount; i++)
        {
            double angle = rotation + i * step;

            Vector2 ringPosition = new Vector2(
                (float)(ringRadius * Math.Cos(angle)),
                (float)(ringRadius * Math.Sin(angle)));

            Vector2 candidate = ringPosition + NextJitter(random);

            if (!IsValidBasePosition(bounds, candidate, positions))
                return false;

            positions.Add(candidate);
        }

        return true;
    }

    private bool TryPlaceYellowBases(
        WorldBounds bounds,
        int yellowBaseCount,
        DeterministicRandom random,
        List<Vector2> positions)
    {
        float limit = bounds.HalfSize - _config.BaseClearRadius;

        for (int i = 0; i < yellowBaseCount; i++)
        {
            bool placed = false;

            for (int attempt = 0; attempt < _config.MaxPlacementAttemptsPerBase; attempt++)
            {
                Vector2 candidate = new Vector2(
                    random.NextFloat(-limit, limit),
                    random.NextFloat(-limit, limit));

                if (!IsValidBasePosition(bounds, candidate, positions))
                    continue;

                positions.Add(candidate);
                placed = true;
                break;
            }

            if (!placed)
                return false;
        }

        return true;
    }

    private Vector2 NextJitter(DeterministicRandom random)
    {
        if (_config.StartPositionJitter <= 0f)
            return Vector2.zero;

        // Egyenletes eloszlás a jitter sugarú körlapon.
        double distance = _config.StartPositionJitter * Math.Sqrt(random.NextDouble());
        double angle = random.NextDouble() * FullCircle;

        return new Vector2(
            (float)(distance * Math.Cos(angle)),
            (float)(distance * Math.Sin(angle)));
    }

    private bool IsValidBasePosition(
        WorldBounds bounds,
        Vector2 candidate,
        List<Vector2> placed)
    {
        if (!bounds.Contains(candidate, _config.BaseClearRadius))
            return false;

        float minDistanceSqr = _config.MinBaseDistance * _config.MinBaseDistance;

        foreach (Vector2 other in placed)
        {
            if ((other - candidate).sqrMagnitude < minDistanceSqr)
                return false;
        }

        return true;
    }

    private static WorldLayout CreateLayout(
        WorldGenerationRequest request,
        List<int> sortedPlayerIds,
        List<Vector2> positions)
    {
        List<BaseLayout> bases = new List<BaseLayout>(positions.Count);

        for (int i = 0; i < positions.Count; i++)
        {
            int baseId = i + 1;

            bases.Add(i < sortedPlayerIds.Count
                ? new BaseLayout(baseId, positions[i], BaseOwnerType.Player, sortedPlayerIds[i])
                : new BaseLayout(baseId, positions[i], BaseOwnerType.Yellow, 0));
        }

        return new WorldLayout(Version, request, bases);
    }
}
