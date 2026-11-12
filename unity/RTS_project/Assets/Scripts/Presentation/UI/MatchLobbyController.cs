using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A lobby felülete: beállítások, játékoshelyek és gombok.
[DisallowMultipleComponent]
public sealed class MatchLobbyController : MonoBehaviour
{
    [SerializeField]
    private WorldGenerationSettings settings;

    [Header("Settings")]
    [SerializeField]
    private Slider playerCountSlider;

    [SerializeField]
    private TMP_Text playerCountText;

    [SerializeField]
    private Slider worldSizeSlider;

    [SerializeField]
    private TMP_Text worldSizeText;

    [SerializeField]
    private Slider yellowBaseSlider;

    [SerializeField]
    private TMP_Text yellowBaseText;

    [SerializeField]
    private TMP_Text obstacleText;

    [SerializeField]
    private Button recommendedButton;

    [Header("Slots")]
    [SerializeField]
    private TMP_Text slotsText;

    [Tooltip("A hoston a gép IP-címe, amelyre a többiek csatlakozhatnak.")]
    [SerializeField]
    private TMP_Text addressText;

    [Header("Buttons")]
    [SerializeField]
    private Button backButton;

    [SerializeField]
    private Button onePlayerTestButton;

    [SerializeField]
    private Button startButton;

    [SerializeField]
    private TMP_Text statusText;

    [Header("Scenes")]
    [Tooltip("A Vissza gombbal betöltött jelenet.")]
    [SerializeField]
    private string mainMenuSceneName = "MainMenu";

    [Tooltip("Az „1 játékos teszt” gombbal betöltött jelenet (a világgenerálás tesztjelenete).")]
    [SerializeField]
    private string onePlayerTestSceneName = "Test_WorldGeneration";

    [Tooltip("A kliens ide tér vissza, ha a kapcsolat megszakad.")]
    [SerializeField]
    private string joinSceneName = "MatchJoin";

    [Tooltip("Az Indítás gombbal (a hoston és a klienseken is) betöltött mérkőzés-jelenet.")]
    [SerializeField]
    private string matchSceneName = "Match";

    private bool _refreshing;
    private bool _isClient;

    // A host lobbyja (a kliensen null).
    public LobbyState State { get; private set; }

    private void Awake()
    {
        if (statusText != null)
            statusText.text = string.Empty;

        SetupSlider(playerCountSlider);
        SetupSlider(worldSizeSlider);
        SetupSlider(yellowBaseSlider);

        _isClient = NetworkSession.IsClientOnly;

        if (_isClient)
        {
            SetupClient();
            return;
        }

        if (settings == null)
        {
            Debug.LogError("A lobbyhoz nincs WorldGenerationSettings rendelve.", this);
            enabled = false;
            return;
        }

        State = new LobbyState(
            new WorldSizingRules(settings.ToConfig()),
            LobbyState.GetDefaultPlayerName(LobbyState.HostPlayerId));
    }

    // A host a Start-ban indul, mert Awake-ben a Netcode üzenettípusai még nincsenek regisztrálva.
    private IEnumerator Start()
    {
        if (State == null)
            yield break;

        while (NetworkSession.Manager != null && NetworkSession.Manager.ShutdownInProgress)
            yield return null;

        SetupHost();
    }

    private void SetupHost()
    {
        if (!NetworkSession.StartHost())
        {
            SetText(statusText, $"A hálózati host nem indult el (a {NetworkSession.DefaultPort}-es " +
                                "port foglalt lehet). Más nem tud csatlakozni.");
            SetText(addressText, string.Empty);
            return;
        }

        NetworkSession.Lobby.AttachHostLobby(State);

        List<string> addresses = NetworkSession.GetLocalIPv4Addresses();
        SetText(addressText, addresses.Count == 0
            ? "Csatlakozás: ezen a gépen 127.0.0.1"
            : $"Csatlakozási cím: {string.Join(", ", addresses)}");
    }

    private void SetupClient()
    {
        SetActive(recommendedButton, false);
        SetActive(onePlayerTestButton, false);
        SetActive(startButton, false);
        SetText(addressText, string.Empty);
    }

    private void OnEnable()
    {
        if (State != null)
            State.Changed += Refresh;

        LobbyNetwork lobby = NetworkSession.Lobby;

        if (_isClient && lobby != null)
        {
            lobby.SnapshotReceived += Refresh;
            lobby.Disconnected += OnDisconnected;
        }

        AddListener(playerCountSlider, OnPlayerCountChanged);
        AddListener(worldSizeSlider, OnWorldSizeChanged);
        AddListener(yellowBaseSlider, OnYellowBaseCountChanged);
        AddListener(recommendedButton, OnRecommendedClicked);
        AddListener(backButton, OnBackClicked);
        AddListener(onePlayerTestButton, OnOnePlayerTestClicked);
        AddListener(startButton, OnStartClicked);

        Refresh();
    }

    private void OnDisable()
    {
        if (State != null)
            State.Changed -= Refresh;

        LobbyNetwork lobby = NetworkSession.Lobby;

        if (lobby != null)
        {
            lobby.SnapshotReceived -= Refresh;
            lobby.Disconnected -= OnDisconnected;
        }

        RemoveListener(playerCountSlider, OnPlayerCountChanged);
        RemoveListener(worldSizeSlider, OnWorldSizeChanged);
        RemoveListener(yellowBaseSlider, OnYellowBaseCountChanged);
        RemoveListener(recommendedButton, OnRecommendedClicked);
        RemoveListener(backButton, OnBackClicked);
        RemoveListener(onePlayerTestButton, OnOnePlayerTestClicked);
        RemoveListener(startButton, OnStartClicked);
    }

    private void OnDestroy()
    {
        if (State != null && NetworkSession.Lobby != null)
            NetworkSession.Lobby.DetachHostLobby(State);
    }

    public void OnBackClicked()
    {
        NetworkSession.Shutdown();
        LoadScene(mainMenuSceneName);
    }

    public void OnOnePlayerTestClicked()
    {
        NetworkSession.Shutdown();
        LoadScene(onePlayerTestSceneName);
    }

    // A host legenerálja és elküldi a világot, majd mindenki betölti a mérkőzést.
    public void OnStartClicked()
    {
        if (State == null || !State.CanStart)
            return;

        if (!NetworkSession.IsHost)
        {
            SetText(statusText, "A mérkőzés csak futó hálózati hosttal indítható.");
            return;
        }

        WorldGenerationResult result = new WorldGenerator(settings.ToConfig())
            .Generate(State.CreateRequest(DeterministicRandom.CreateRandomSeed()));

        if (!result.Success)
        {
            SetText(statusText, $"A világ generálása nem sikerült: {result.ErrorMessage}");
            return;
        }

        MatchSetup setup = MatchSetup.FromLobby(State, result.Layout, LobbyState.HostPlayerId);
        MatchSetup.Current = setup;

        if (!NetworkSession.Lobby.TryStartMatch(setup, matchSceneName, out string error))
        {
            MatchSetup.Current = null;
            NetworkSession.Lobby.AttachHostLobby(State);
            SetText(statusText, error);
        }
    }

    private void OnDisconnected(string reason)
    {
        NetworkSession.LastDisconnectReason = reason;
        NetworkSession.Shutdown();
        LoadScene(joinSceneName);
    }

    private void OnRecommendedClicked()
    {
        State?.ApplyRecommendedSettings();
    }

    private void OnPlayerCountChanged(float value)
    {
        if (!_refreshing && State != null)
            State.SetPlayerCount(Mathf.RoundToInt(value));
    }

    // A világméret csúszkája lépésközönként halad.
    private void OnWorldSizeChanged(float value)
    {
        if (!_refreshing && State != null)
            State.SetWorldSize(State.WorldSizeRange.Min + Mathf.RoundToInt(value) * State.WorldSizeStep);
    }

    private void OnYellowBaseCountChanged(float value)
    {
        if (!_refreshing && State != null)
            State.SetYellowBaseCount(Mathf.RoundToInt(value));
    }

    private LobbySnapshot CurrentSnapshot()
    {
        if (State != null)
            return LobbySnapshot.FromState(State, LobbyState.HostPlayerId);

        LobbyNetwork lobby = NetworkSession.Lobby;
        return lobby != null ? lobby.LatestSnapshot : null;
    }

    private void Refresh()
    {
        LobbySnapshot snapshot = CurrentSnapshot();

        if (snapshot == null)
        {
            SetText(slotsText, "Kapcsolódás a lobbyhoz…");
            return;
        }

        _refreshing = true;

        try
        {
            bool editable = State != null;
            int sizeStep = Math.Max(1, snapshot.WorldSizeStep);

            SetSlider(playerCountSlider, snapshot.MinPlayerCount, snapshot.MaxPlayerCount,
                snapshot.PlayerCount, editable);
            SetSlider(worldSizeSlider, 0, (snapshot.WorldSizeMax - snapshot.WorldSizeMin) / sizeStep,
                (snapshot.WorldSize - snapshot.WorldSizeMin) / sizeStep, editable);
            SetSlider(yellowBaseSlider, snapshot.YellowBaseMin, snapshot.YellowBaseMax,
                snapshot.YellowBaseCount, editable);

            SetText(playerCountText, snapshot.PlayerCount.ToString());
            SetText(worldSizeText, $"{snapshot.WorldSize} × {snapshot.WorldSize}");
            SetText(yellowBaseText, snapshot.YellowBaseCount.ToString());
            SetText(obstacleText, $"Akadályok: {snapshot.ObstacleCount}");
            SetText(slotsText, BuildSlotList(snapshot));

            if (startButton != null)
                startButton.interactable = snapshot.CanStart;
        }
        finally
        {
            _refreshing = false;
        }
    }

    public static string BuildSlotList(LobbySnapshot snapshot)
    {
        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < snapshot.SlotNames.Length; i++)
        {
            int playerId = i + 1;
            string name = snapshot.SlotNames[i];

            if (builder.Length > 0)
                builder.Append('\n');

            builder.Append(playerId).Append(". hely: ");
            builder.Append(string.IsNullOrEmpty(name) ? "várakozás játékosra…" : name);

            if (playerId == LobbyState.HostPlayerId)
                builder.Append(" (host)");

            if (playerId == snapshot.LocalPlayerId)
                builder.Append(" – te");
        }

        builder.Append("\n\n")
            .Append(snapshot.OccupiedSlotCount).Append(" / ").Append(snapshot.PlayerCount)
            .Append(" játékos csatlakozott");

        return builder.ToString();
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"A(z) '{sceneName}' jelenet nem tölthető be: nincs a Build Settings jelenetlistájában.",
                this);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private static void SetupSlider(Slider slider)
    {
        if (slider != null)
            slider.wholeNumbers = true;
    }

    private static void SetSlider(Slider slider, int min, int max, int value, bool editable)
    {
        if (slider == null)
            return;

        slider.minValue = min;
        slider.maxValue = Math.Max(min, max);
        slider.SetValueWithoutNotify(value);
        slider.interactable = editable && max > min;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private static void SetActive(Component component, bool active)
    {
        if (component != null)
            component.gameObject.SetActive(active);
    }

    private static void AddListener(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider != null)
            slider.onValueChanged.AddListener(action);
    }

    private static void RemoveListener(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(action);
    }

    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.AddListener(action);
    }

    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }
}
