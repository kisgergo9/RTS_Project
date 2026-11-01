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

    private WorldLayout _previewLayout;
    private float _previewClearRadius;

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
        WorldGenerationResult preview = new WorldGenerator(config)
            .Generate(CreateRequest(playerIds, seed));

        _previewClearRadius = config.BaseClearRadius;

        _previewLayout = preview.Layout;
        validRanges = preview.Success
            ? ranges
            : ranges + $"\n\nHIBA: {preview.ErrorMessage}";
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

        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(bounds.Size, bounds.Size, 0f));

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
#endif
}
