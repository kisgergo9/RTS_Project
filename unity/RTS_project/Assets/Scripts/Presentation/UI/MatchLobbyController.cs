using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A lobby felülete a hoston: játékosszám, világméret és sárga bázisok
/// beállítása, a játékoshelyek listája, valamint a Vissza, az
/// „1 játékos teszt” és az Indítás gomb.
///
/// Az állapotot a LobbyState tárolja; ez a komponens csak megjeleníti és a
/// csúszkák változását továbbítja. Amíg nincs hálózat, csak a host helye
/// foglalt, ezért az Indítás gomb inaktív.
/// </summary>
[DisallowMultipleComponent]
public sealed class MatchLobbyController : MonoBehaviour
{
    [SerializeField]
    private WorldGenerationSettings settings;

    [SerializeField]
    private string hostName = "Játékos 1";

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

    [SerializeField]
    private string startUnavailableMessage = "A mérkőzés indítása még nem elérhető.";

    private bool _refreshing;

    public LobbyState State { get; private set; }

    private void Awake()
    {
        if (settings == null)
        {
            Debug.LogError("A lobbyhoz nincs WorldGenerationSettings rendelve.", this);
            enabled = false;
            return;
        }

        State = new LobbyState(new WorldSizingRules(settings.ToConfig()), hostName);

        SetupSlider(playerCountSlider);
        SetupSlider(worldSizeSlider);
        SetupSlider(yellowBaseSlider);

        if (statusText != null)
            statusText.text = string.Empty;
    }

    private void OnEnable()
    {
        if (State == null)
            return;

        State.Changed += Refresh;

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
        if (State == null)
            return;

        State.Changed -= Refresh;

        RemoveListener(playerCountSlider, OnPlayerCountChanged);
        RemoveListener(worldSizeSlider, OnWorldSizeChanged);
        RemoveListener(yellowBaseSlider, OnYellowBaseCountChanged);
        RemoveListener(recommendedButton, OnRecommendedClicked);
        RemoveListener(backButton, OnBackClicked);
        RemoveListener(onePlayerTestButton, OnOnePlayerTestClicked);
        RemoveListener(startButton, OnStartClicked);
    }

    public void OnBackClicked()
    {
        LoadScene(mainMenuSceneName);
    }

    public void OnOnePlayerTestClicked()
    {
        LoadScene(onePlayerTestSceneName);
    }

    public void OnStartClicked()
    {
        if (statusText != null)
            statusText.text = startUnavailableMessage;
    }

    private void OnRecommendedClicked()
    {
        State.ApplyRecommendedSettings();
    }

    private void OnPlayerCountChanged(float value)
    {
        if (!_refreshing)
            State.SetPlayerCount(Mathf.RoundToInt(value));
    }

    // A világméret csúszkája lépésközönként halad: az érték a lépések száma
    // a minimális mérettől.
    private void OnWorldSizeChanged(float value)
    {
        if (!_refreshing)
            State.SetWorldSize(State.WorldSizeRange.Min + Mathf.RoundToInt(value) * State.WorldSizeStep);
    }

    private void OnYellowBaseCountChanged(float value)
    {
        if (!_refreshing)
            State.SetYellowBaseCount(Mathf.RoundToInt(value));
    }

    private void Refresh()
    {
        _refreshing = true;

        try
        {
            IntRange sizeRange = State.WorldSizeRange;
            IntRange yellowRange = State.YellowBaseCountRange;

            SetSlider(playerCountSlider, State.MinSelectablePlayerCount, State.MaxPlayerCount, State.PlayerCount);
            SetSlider(worldSizeSlider, 0, (sizeRange.Max - sizeRange.Min) / State.WorldSizeStep,
                (State.WorldSize - sizeRange.Min) / State.WorldSizeStep);
            SetSlider(yellowBaseSlider, yellowRange.Min, yellowRange.Max, State.YellowBaseCount);

            SetText(playerCountText, State.PlayerCount.ToString());
            SetText(worldSizeText, $"{State.WorldSize} × {State.WorldSize}");
            SetText(yellowBaseText, State.YellowBaseCount.ToString());
            SetText(obstacleText, $"Akadályok: {State.ObstacleCount}");
            SetText(slotsText, BuildSlotList(State));

            if (startButton != null)
                startButton.interactable = State.CanStart;
        }
        finally
        {
            _refreshing = false;
        }
    }

    public static string BuildSlotList(LobbyState state)
    {
        StringBuilder builder = new StringBuilder();

        foreach (LobbySlot slot in state.Slots)
        {
            if (builder.Length > 0)
                builder.Append('\n');

            builder.Append(slot.PlayerId).Append(". hely: ");
            builder.Append(slot.IsOccupied ? slot.PlayerName : "várakozás játékosra…");

            if (slot.PlayerId == LobbyState.HostPlayerId)
                builder.Append(" (host)");
        }

        builder.Append("\n\n")
            .Append(state.OccupiedSlotCount).Append(" / ").Append(state.PlayerCount)
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

    private static void SetSlider(Slider slider, int min, int max, int value)
    {
        if (slider == null)
            return;

        slider.minValue = min;
        slider.maxValue = Math.Max(min, max);
        slider.SetValueWithoutNotify(value);
        slider.interactable = max > min;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
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
