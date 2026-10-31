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
///   5. Az akadályokat a seedből származtatott, külön véletlensorozattal helyezi
///      el, így a bázisok helyét nem befolyásolják. Egy akadály csak akkor
///      fogadható el, ha a pályán belül van, minden bázis szabad területétől
///      legalább MinObstacleBaseDistance, a többi akadálytól legalább
///      MinObstacleSpacing távolságra esik.
///   6. Végül a járhatósági rácson ellenőrzi, hogy minden bázis minden kijárata
///      ugyanabba az összefüggő régióba esik. Ha nem, vagy egy akadály nem
///      helyezhető el, az akadályokat a következő származtatott sorozattal
///      generálja újra (legfeljebb MaxObstacleGenerationAttempts-szer). A bázisok
///      ilyenkor sem mozdulnak. Ha ez sem sikerül, hibás eredményt ad vissza.
///   7. A dekorációkat (pl. pálmafák) újabb származtatott sorozattal helyezi el,
///      a bázisok szabad területén és az akadályokon kívül. Egy részük
///      (DecorationGroveFraction) ligetként a vonzó akadályok (pl. oázis) köré
///      kerül, a többi szétszórva. Nem kerülnek a járhatósági rácsba, és sosem
///      okoznak hibát: ha egy dekoráció nem fér el, kimarad.
/// </summary>
public sealed class WorldGenerator
{
    /// <summary>
    /// A generálási algoritmus verziója. Növelni kell minden olyan változtatásnál,
    /// amely ugyanabból a seedből és kérésből más világot eredményez.
    /// </summary>
    public const int Version = 2;

    private const double FullCircle = 2.0 * Math.PI;

    // A származtatott véletlensorozatok első sorszáma az akadályokhoz.
    private const int ObstacleStream = 1000;

    // A dekorációk származtatott véletlensorozatának sorszáma.
    private const int DecorationStream = 2000;

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

        if (request.ObstacleCount > 0 &&
            (_config.Terrain == null || _config.Terrain.ObstacleTypes.Count == 0))
        {
            return WorldGenerationResult.Failed(
                WorldGenerationError.InvalidRequest,
                $"{request.ObstacleCount} akadály kérve, de nincs akadálytípus konfigurálva.",
                0);
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

            List<BaseLayout> bases = CreateBases(playerIds, positions);

            if (!TryGenerateObstacles(request, bounds, bases, out List<TerrainFeatureLayout> obstacles))
            {
                return WorldGenerationResult.Failed(
                    WorldGenerationError.PlacementFailed,
                    $"Nem sikerült {request.ObstacleCount} akadályt úgy elhelyezni a " +
                    $"{request.WorldSize} méretű világban, hogy minden bázis elérhető maradjon " +
                    $"({_config.Terrain.MaxObstacleGenerationAttempts} akadály-generálási kísérlet, " +
                    $"seed: {request.Seed}). Próbálj kevesebb akadályt, nagyobb világot " +
                    "vagy másik seedet.",
                    attempt);
            }

            List<TerrainFeatureLayout> decorations = GenerateDecorations(request, bounds, bases, obstacles);

            return WorldGenerationResult.Succeeded(
                new WorldLayout(Version, request, bases, obstacles, decorations),
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

    // Akadályok

    /// <summary>
    /// A layoutnak megfelelő járhatósági rács: a pálya széle mentén és az
    /// akadályok körül MinPassageWidth / 2 szélességben megnövelve, a bázisok
    /// tiltott területe pontosan. Az útvonaltervezés is ebből indulhat ki.
    /// </summary>
    public WalkabilityGrid BuildWalkabilityGrid(WorldLayout layout)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        return BuildWalkabilityGrid(layout.Bounds, layout.Bases, layout.Obstacles);
    }

    /// <summary>
    /// Igaz, ha minden bázis minden kötelező pontja (kijárat, foglalási terület)
    /// ugyanabba az összefüggő járható régióba esik.
    /// </summary>
    public bool AreBasesReachable(WorldLayout layout)
    {
        if (layout == null)
            throw new ArgumentNullException(nameof(layout));

        return AreBasesReachable(BuildWalkabilityGrid(layout), layout.Bases);
    }

    private WalkabilityGrid BuildWalkabilityGrid(
        WorldBounds bounds,
        IEnumerable<BaseLayout> bases,
        IEnumerable<TerrainFeatureLayout> obstacles)
    {
        TerrainGenerationConfig terrain = RequireTerrain();
        float inflation = terrain.MinPassageWidth * 0.5f;

        WalkabilityGrid grid = new WalkabilityGrid(bounds, terrain.WalkabilityCellSize);
        grid.BlockBorder(inflation);

        foreach (BaseLayout baseLayout in bases)
            grid.BlockFootprint(terrain.BaseFootprint, baseLayout.Position);

        foreach (TerrainFeatureLayout obstacle in obstacles)
        {
            TerrainFeatureType type = FindObstacleType(obstacle.TypeId);
            grid.BlockFootprint(type.Footprint, obstacle.Position, obstacle.Scale, inflation);
        }

        return grid;
    }

    private bool AreBasesReachable(WalkabilityGrid grid, IReadOnlyList<BaseLayout> bases)
    {
        TerrainGenerationConfig terrain = RequireTerrain();

        // A kijárat közvetlenül a bázis tiltott területe mellett lehet, ezért a
        // legközelebbi járható cellát keressük, legfeljebb két cellányi távolságban.
        float searchDistance = grid.CellSize * 2f;
        List<Vector2Int> cells = new List<Vector2Int>(bases.Count * terrain.BaseAccessPoints.Count);

        foreach (BaseLayout baseLayout in bases)
        {
            foreach (Vector2 accessPoint in terrain.BaseAccessPoints)
            {
                if (!grid.TryFindNearestWalkableCell(
                        baseLayout.Position + accessPoint, searchDistance, out Vector2Int cell))
                {
                    return false;
                }

                cells.Add(cell);
            }
        }

        return grid.AreConnected(cells);
    }

    private bool TryGenerateObstacles(
        WorldGenerationRequest request,
        WorldBounds bounds,
        List<BaseLayout> bases,
        out List<TerrainFeatureLayout> obstacles)
    {
        obstacles = new List<TerrainFeatureLayout>(request.ObstacleCount);

        if (request.ObstacleCount == 0)
            return true;

        TerrainGenerationConfig terrain = RequireTerrain();

        for (int attempt = 0; attempt < terrain.MaxObstacleGenerationAttempts; attempt++)
        {
            DeterministicRandom random = new DeterministicRandom(
                DeterministicRandom.DeriveSeed(request.Seed, ObstacleStream + attempt));

            obstacles.Clear();

            if (!TryPlaceObstacles(bounds, bases, request.ObstacleCount, random, obstacles))
                continue;

            WalkabilityGrid grid = BuildWalkabilityGrid(bounds, bases, obstacles);

            if (AreBasesReachable(grid, bases))
                return true;
        }

        obstacles = null;
        return false;
    }

    private bool TryPlaceObstacles(
        WorldBounds bounds,
        List<BaseLayout> bases,
        int count,
        DeterministicRandom random,
        List<TerrainFeatureLayout> obstacles)
    {
        TerrainGenerationConfig terrain = RequireTerrain();
        List<Footprint> placedFootprints = new List<Footprint>(count);

        for (int i = 0; i < count; i++)
        {
            bool placed = false;

            for (int attempt = 0; attempt < terrain.MaxPlacementAttemptsPerObstacle; attempt++)
            {
                TerrainFeatureType type = PickWeighted(terrain.ObstacleTypes, random);
                int variant = random.NextInt(type.VariantCount);
                float scale = random.NextFloat(type.MinScale, type.MaxScale);
                Footprint footprint = type.Footprint.Scaled(scale);

                Vector2 limit = new Vector2(bounds.HalfSize, bounds.HalfSize) - footprint.HalfExtents;

                if (limit.x <= 0f || limit.y <= 0f)
                    continue;

                Vector2 position = new Vector2(
                    random.NextFloat(-limit.x, limit.x),
                    random.NextFloat(-limit.y, limit.y));

                if (!IsValidObstaclePosition(footprint, position, bases, obstacles, placedFootprints))
                    continue;

                obstacles.Add(new TerrainFeatureLayout(type.Id, variant, position, scale));
                placedFootprints.Add(footprint);
                placed = true;
                break;
            }

            if (!placed)
                return false;
        }

        return true;
    }

    private bool IsValidObstaclePosition(
        Footprint footprint,
        Vector2 position,
        List<BaseLayout> bases,
        List<TerrainFeatureLayout> obstacles,
        List<Footprint> placedFootprints)
    {
        TerrainGenerationConfig terrain = RequireTerrain();
        float baseDistance = _config.BaseClearRadius + terrain.MinObstacleBaseDistance;

        foreach (BaseLayout baseLayout in bases)
        {
            Vector2 local = baseLayout.Position - position;

            if (local.magnitude > footprint.BoundingRadius + baseDistance)
                continue;

            if (footprint.Contains(local) || footprint.DistanceToEdge(local) < baseDistance)
                return false;
        }

        for (int i = 0; i < obstacles.Count; i++)
        {
            Footprint other = placedFootprints[i];
            float reach = footprint.BoundingRadius + other.BoundingRadius + terrain.MinObstacleSpacing;

            if ((obstacles[i].Position - position).magnitude > reach)
                continue;

            if (Footprint.Distance(footprint, position, other, obstacles[i].Position) < terrain.MinObstacleSpacing)
                return false;
        }

        return true;
    }

    // Dekorációk

    /// <summary>
    /// A pálya méretéhez tartozó dekorációszám (DecorationsPer10000Area alapján).
    /// Zsúfolt pályán ennél kevesebb is elhelyezhető.
    /// </summary>
    public int GetRequestedDecorationCount(int worldSize)
    {
        TerrainGenerationConfig terrain = _config.Terrain;

        if (terrain == null || terrain.DecorationTypes.Count == 0)
            return 0;

        double count = terrain.DecorationsPer10000Area * (double)worldSize * worldSize / 10000.0;

        return (int)Math.Floor(count + 0.5);
    }

    private List<TerrainFeatureLayout> GenerateDecorations(
        WorldGenerationRequest request,
        WorldBounds bounds,
        List<BaseLayout> bases,
        List<TerrainFeatureLayout> obstacles)
    {
        int count = GetRequestedDecorationCount(request.WorldSize);
        List<TerrainFeatureLayout> decorations = new List<TerrainFeatureLayout>(count);

        if (count == 0)
            return decorations;

        TerrainGenerationConfig terrain = RequireTerrain();
        DeterministicRandom random = new DeterministicRandom(
            DeterministicRandom.DeriveSeed(request.Seed, DecorationStream));

        List<Footprint> obstacleFootprints = new List<Footprint>(obstacles.Count);
        List<int> groveAnchors = new List<int>();

        for (int i = 0; i < obstacles.Count; i++)
        {
            TerrainFeatureType obstacleType = FindObstacleType(obstacles[i].TypeId);
            obstacleFootprints.Add(obstacleType.Footprint.Scaled(obstacles[i].Scale));

            if (obstacleType.AttractsDecorations)
                groveAnchors.Add(i);
        }

        List<float> decorationRadii = new List<float>(count);

        for (int i = 0; i < count; i++)
        {
            // Ligetbe kerül-e ez a dekoráció (csak ha van vonzó akadály).
            bool inGrove = groveAnchors.Count > 0 &&
                           random.NextDouble() < terrain.DecorationGroveFraction;

            for (int attempt = 0; attempt < terrain.MaxPlacementAttemptsPerObstacle; attempt++)
            {
                TerrainFeatureType type = PickWeighted(terrain.DecorationTypes, random);
                int variant = random.NextInt(type.VariantCount);
                float scale = random.NextFloat(type.MinScale, type.MaxScale);
                Footprint footprint = type.Footprint.Scaled(scale);

                Vector2 limit = new Vector2(bounds.HalfSize, bounds.HalfSize) - footprint.HalfExtents;

                if (limit.x <= 0f || limit.y <= 0f)
                    continue;

                Vector2 position;

                if (inGrove)
                {
                    int anchor = groveAnchors[random.NextInt(groveAnchors.Count)];
                    position = NextGrovePosition(
                        obstacles[anchor].Position, obstacleFootprints[anchor], footprint, random);

                    if (Mathf.Abs(position.x) > limit.x || Mathf.Abs(position.y) > limit.y)
                        continue;
                }
                else
                {
                    position = new Vector2(
                        random.NextFloat(-limit.x, limit.x),
                        random.NextFloat(-limit.y, limit.y));
                }

                if (!IsValidDecorationPosition(
                        footprint, position, bases, obstacles, obstacleFootprints, decorations, decorationRadii))
                {
                    continue;
                }

                decorations.Add(new TerrainFeatureLayout(type.Id, variant, position, scale));
                decorationRadii.Add(footprint.BoundingRadius);
                break;
            }

            // Ha nem sikerült elhelyezni, a dekoráció kimarad: ez nem hiba.
        }

        return decorations;
    }

    /// <summary>
    /// Véletlen pont a vonzó akadály körül: az akadály fél-kiterjedéseiből
    /// képzett ellipszis egy pontjától kifelé, a dekoráció méretének kétszerese
    /// és ehhez hozzáadott legfeljebb DecorationGroveRadius távolságra.
    /// </summary>
    private Vector2 NextGrovePosition(
        Vector2 anchorPosition,
        Footprint anchorFootprint,
        Footprint decorationFootprint,
        DeterministicRandom random)
    {
        double angle = random.NextDouble() * FullCircle;
        Vector2 direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        Vector2 edge = Vector2.Scale(anchorFootprint.HalfExtents, direction);

        float distance = 2f * decorationFootprint.BoundingRadius +
                         (float)random.NextDouble() * RequireTerrain().DecorationGroveRadius;

        return anchorPosition + edge + direction * distance;
    }

    private bool IsValidDecorationPosition(
        Footprint footprint,
        Vector2 position,
        List<BaseLayout> bases,
        List<TerrainFeatureLayout> obstacles,
        List<Footprint> obstacleFootprints,
        List<TerrainFeatureLayout> decorations,
        List<float> decorationRadii)
    {
        // A dekoráció a saját méretével megegyező távolságot tart az
        // akadályoktól, hogy ne lógjon bele a grafikájukba.
        float margin = footprint.BoundingRadius;

        foreach (BaseLayout baseLayout in bases)
        {
            Vector2 local = baseLayout.Position - position;

            if (local.magnitude > footprint.BoundingRadius + _config.BaseClearRadius)
                continue;

            if (footprint.Contains(local) || footprint.DistanceToEdge(local) < _config.BaseClearRadius)
                return false;
        }

        for (int i = 0; i < obstacles.Count; i++)
        {
            Footprint other = obstacleFootprints[i];
            float reach = footprint.BoundingRadius + other.BoundingRadius + margin;

            if ((obstacles[i].Position - position).magnitude > reach)
                continue;

            if (Footprint.Distance(footprint, position, other, obstacles[i].Position) < margin)
                return false;
        }

        for (int i = 0; i < decorations.Count; i++)
        {
            if ((decorations[i].Position - position).magnitude < footprint.BoundingRadius + decorationRadii[i])
                return false;
        }

        return true;
    }

    private static TerrainFeatureType PickWeighted(
        IReadOnlyList<TerrainFeatureType> types,
        DeterministicRandom random)
    {
        double total = 0.0;

        foreach (TerrainFeatureType type in types)
            total += type.Weight;

        double pick = random.NextDouble() * total;

        foreach (TerrainFeatureType type in types)
        {
            pick -= type.Weight;

            if (pick < 0.0)
                return type;
        }

        // Lebegőpontos kerekítés esetén az utolsó típus.
        return types[types.Count - 1];
    }

    private TerrainFeatureType FindObstacleType(string typeId)
    {
        foreach (TerrainFeatureType type in RequireTerrain().ObstacleTypes)
        {
            if (string.Equals(type.Id, typeId, StringComparison.OrdinalIgnoreCase))
                return type;
        }

        throw new ArgumentException($"Ismeretlen akadálytípus: {typeId}", nameof(typeId));
    }

    private TerrainGenerationConfig RequireTerrain()
    {
        return _config.Terrain ?? throw new InvalidOperationException(
            "A művelethez tereptárgy-konfiguráció (WorldGenerationConfig.Terrain) szükséges.");
    }

    private static List<BaseLayout> CreateBases(
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

        return bases;
    }
}
