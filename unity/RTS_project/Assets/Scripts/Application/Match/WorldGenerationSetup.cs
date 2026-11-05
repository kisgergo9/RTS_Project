using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A host generálási beállításai. Amíg nincs lobby, ezeket az Inspectorban
/// lehet megadni; a lobby elkészülte után a lobby tölti ki ugyanezeket az
/// értékeket. A játékosokat a GameBootstrapper adja meg.
///
/// Szerkesztőben a Scene nézetben előnézetet rajzol a generált világról,
/// így a seed és a méretek Play mód nélkül is kipróbálhatók.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldGenerationSetup : MonoBehaviour
{
    [SerializeField]
    private WorldGenerationSettings settings;

    [Header("Host Settings (lobby stand-in)")]
    [SerializeField, Min(1)]
    private int worldSize = 140;

    [SerializeField, Min(1)]
    private int yellowBaseCount = 4;

    [Tooltip("Fenntartva: a generátor még nem helyez el akadályokat, de a szám ellenőrzésre kerül.")]
    [SerializeField, Min(0)]
    private int obstacleCount;

    [SerializeField]
    private int seed = 12345;

    [Tooltip("Bekapcsolva minden indításkor új, véletlen seedet használ.")]
    [SerializeField]
    private bool randomizeSeed;

    [Header("Info (read only)")]
    [SerializeField, TextArea(3, 6)]
    private string validRanges;

    [Header("Editor Preview")]
    [SerializeField]
    private bool drawPreview = true;

    [Tooltip("A járhatósági rács megjelenítése: sötétvörös = tiltott, zöld = a fő régió, " +
             "lila = attól elzárt járható terület.")]
    [SerializeField]
    private bool drawWalkability;

    private WorldLayout _previewLayout;
    private float _previewClearRadius;
    private readonly Dictionary<string, Footprint> _previewFootprints =
        new Dictionary<string, Footprint>(StringComparer.OrdinalIgnoreCase);
    private Texture2D _walkabilityTexture;

    public WorldGenerationSettings Settings => settings;

    /// <summary>
    /// A legutóbbi generáláshoz használt seed (véletlen seed esetén is).
    /// </summary>
    public int LastUsedSeed { get; private set; }

    public WorldGenerationResult Generate(IEnumerable<PlayerState> players)
    {
        if (players == null)
            throw new ArgumentNullException(nameof(players));

        if (settings == null)
            throw new InvalidOperationException(
                $"A WorldGenerationSetup-hoz nincs WorldGenerationSettings rendelve: {name}");

        List<int> playerIds = new List<int>();

        foreach (PlayerState player in players)
            playerIds.Add(player.PlayerId);

        LastUsedSeed = randomizeSeed ? DeterministicRandom.CreateRandomSeed() : seed;

        WorldGenerator generator = new WorldGenerator(settings.ToConfig());

        return generator.Generate(CreateRequest(playerIds, LastUsedSeed));
    }

    private WorldGenerationRequest CreateRequest(IEnumerable<int> playerIds, int usedSeed)
    {
        return new WorldGenerationRequest(
            playerIds,
            worldSize,
            yellowBaseCount,
            obstacleCount,
            usedSeed);
    }

#if UNITY_EDITOR
    [ContextMenu("Use Minimum Values")]
    private void UseMinimumValues()
    {
        if (!TryGetEditorContext(out WorldSizingRules rules, out List<int> playerIds))
            return;

        UnityEditor.Undo.RecordObject(this, "Use Minimum World Values");
        worldSize = rules.GetMinWorldSize(playerIds.Count);
        yellowBaseCount = rules.GetMinYellowBaseCount(playerIds.Count);
        obstacleCount = rules.GetObstacleCountRange(worldSize).Min;
        OnValidate();
    }

    [ContextMenu("Use Maximum Values")]
    private void UseMaximumValues()
    {
        if (!TryGetEditorContext(out WorldSizingRules rules, out List<int> playerIds))
            return;

        UnityEditor.Undo.RecordObject(this, "Use Maximum World Values");
        worldSize = rules.MaxWorldSize;
        yellowBaseCount = rules.GetYellowBaseCountRange(playerIds.Count, worldSize).Max;
        obstacleCount = rules.GetObstacleCountRange(worldSize).Max;
        OnValidate();
    }

    [ContextMenu("New Random Seed")]
    private void NewRandomSeed()
    {
        UnityEditor.Undo.RecordObject(this, "New Random World Seed");
        seed = DeterministicRandom.CreateRandomSeed();
        OnValidate();
    }

    private void OnValidate()
    {
        _previewLayout = null;

        if (!TryGetEditorContext(out WorldSizingRules rules, out List<int> playerIds))
            return;

        int players = playerIds.Count;

        if (players < rules.MinPlayerCount || players > rules.MaxPlayerCount)
        {
            validRanges = $"A játékosszám ({players}) a [{rules.MinPlayerCount}; " +
                          $"{rules.MaxPlayerCount}] tartományon kívül esik.";
            return;
        }

        IntRange sizeRange = rules.GetWorldSizeRange(players);
        string ranges = $"Játékosok: {players}\nVilágméret: {sizeRange}";

        if (sizeRange.Contains(worldSize))
        {
            ranges += $"\nSárga bázisok: {rules.GetYellowBaseCountRange(players, worldSize)}" +
                      $"\nAkadályok: {rules.GetObstacleCountRange(worldSize)}";
        }

        WorldGenerationConfig config = settings.ToConfig();
        WorldGenerator generator = new WorldGenerator(config);
        WorldGenerationResult preview = generator.Generate(CreateRequest(playerIds, seed));

        _previewClearRadius = config.BaseClearRadius;
        _previewLayout = preview.Layout;
        CachePreviewFootprints(config.Terrain);
        ReleaseWalkabilityTexture();

        if (!preview.Success)
        {
            validRanges = ranges + $"\n\nHIBA: {preview.ErrorMessage}";
            return;
        }

        ranges += $"\nDekorációk: {preview.Layout.Decorations.Count}";

        if (config.Terrain != null)
        {
            WalkabilityGrid grid = generator.BuildWalkabilityGrid(preview.Layout);
            bool reachable = generator.AreBasesReachable(preview.Layout);

            ranges += $"\nJárható régiók: {grid.RegionCount}, minden bázis elérhető: " +
                      (reachable ? "igen" : "NEM");

            if (drawWalkability)
                _walkabilityTexture = CreateWalkabilityTexture(grid);
        }

        validRanges = ranges;
    }

    private void CachePreviewFootprints(TerrainGenerationConfig terrain)
    {
        _previewFootprints.Clear();

        if (terrain == null)
            return;

        foreach (TerrainFeatureType type in terrain.ObstacleTypes)
            _previewFootprints[type.Id] = type.Footprint;

        foreach (TerrainFeatureType type in terrain.DecorationTypes)
            _previewFootprints[type.Id] = type.Footprint;
    }

    /// <summary>
    /// Cellánként egy képpont: tiltott = sötétvörös, a legnagyobb (fő) régió =
    /// zöld, minden más járható régió = lila (ezek a fő régióból elérhetetlenek).
    /// </summary>
    private static Texture2D CreateWalkabilityTexture(WalkabilityGrid grid)
    {
        int[] regionSizes = new int[grid.RegionCount];

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                int region = grid.GetRegion(x, y);

                if (region != WalkabilityGrid.NoRegion)
                    regionSizes[region]++;
            }
        }

        int mainRegion = 0;

        for (int i = 1; i < regionSizes.Length; i++)
        {
            if (regionSizes[i] > regionSizes[mainRegion])
                mainRegion = i;
        }

        Color32 blocked = new Color32(140, 0, 0, 150);
        Color32 main = new Color32(0, 200, 0, 50);
        Color32 isolated = new Color32(200, 0, 255, 170);

        Color32[] pixels = new Color32[grid.Width * grid.Height];

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                int region = grid.GetRegion(x, y);

                // A Gizmos.DrawGUITexture GUI-koordinátákat használ (felülről
                // lefelé), ezért a sorokat fordított sorrendben írjuk.
                pixels[(grid.Height - 1 - y) * grid.Width + x] = region == WalkabilityGrid.NoRegion
                    ? blocked
                    : region == mainRegion ? main : isolated;
            }
        }

        Texture2D texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        texture.SetPixels32(pixels);
        texture.Apply();

        return texture;
    }

    private void ReleaseWalkabilityTexture()
    {
        if (_walkabilityTexture == null)
            return;

        DestroyImmediate(_walkabilityTexture);
        _walkabilityTexture = null;
    }

    private void OnDisable()
    {
        ReleaseWalkabilityTexture();
    }

    private bool TryGetEditorContext(out WorldSizingRules rules, out List<int> playerIds)
    {
        rules = null;
        playerIds = null;

        if (settings == null)
        {
            validRanges = "Nincs WorldGenerationSettings megadva.";
            return false;
        }

        GameBootstrapper bootstrapper = GetComponent<GameBootstrapper>();

        if (bootstrapper == null)
        {
            validRanges = "A GameBootstrapper-rel azonos GameObjecten kell lennie.";
            return false;
        }

        try
        {
            rules = new WorldSizingRules(settings.ToConfig());
            playerIds = new List<int>(bootstrapper.ConfiguredPlayerIds);
            return true;
        }
        catch (ArgumentException exception)
        {
            validRanges = $"Érvénytelen konfiguráció: {exception.Message}";
            return false;
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawPreview || Application.isPlaying || _previewLayout == null)
            return;

        WorldBounds bounds = _previewLayout.Bounds;

        if (drawWalkability && _walkabilityTexture != null)
        {
            Vector2 min = bounds.Min;
            Gizmos.DrawGUITexture(new Rect(min.x, min.y, bounds.Size, bounds.Size), _walkabilityTexture);
        }

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(bounds.Size, bounds.Size, 0f));

        UnityEditor.Handles.color = new Color(1f, 0.55f, 0.1f);

        foreach (TerrainFeatureLayout obstacle in _previewLayout.Obstacles)
            DrawFootprint(obstacle);

        UnityEditor.Handles.color = new Color(0.2f, 0.85f, 0.2f);

        foreach (TerrainFeatureLayout decoration in _previewLayout.Decorations)
            UnityEditor.Handles.DrawSolidDisc(decoration.Position, Vector3.forward, 0.6f);

        foreach (BaseLayout baseLayout in _previewLayout.Bases)
        {
            Vector3 position = baseLayout.Position;

            Gizmos.color = baseLayout.OwnerType == BaseOwnerType.Player
                ? new Color(0.2f, 0.6f, 1f)
                : Color.yellow;

            Gizmos.DrawWireCube(position, new Vector3(12f, 4.2f, 0f));
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.DrawWireDisc(position, Vector3.forward, _previewClearRadius);
            UnityEditor.Handles.Label(position, baseLayout.OwnerType == BaseOwnerType.Player
                ? $"P{baseLayout.PlayerId}"
                : $"Y{baseLayout.BaseId}");
        }
    }

    private void DrawFootprint(TerrainFeatureLayout feature)
    {
        if (!_previewFootprints.TryGetValue(feature.TypeId, out Footprint footprint))
            return;

        IReadOnlyList<Vector2> points = footprint.Points;
        Vector3[] outline = new Vector3[points.Count + 1];

        for (int i = 0; i < points.Count; i++)
            outline[i] = feature.Position + points[i] * feature.Scale;

        outline[points.Count] = outline[0];

        UnityEditor.Handles.DrawAAPolyLine(2f, outline);
    }
#endif
}
