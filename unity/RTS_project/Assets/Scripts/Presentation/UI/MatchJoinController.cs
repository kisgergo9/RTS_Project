using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A csatlakozási képernyő: a host IP-címe, Csatlakozás és Vissza gomb.
[DisallowMultipleComponent]
public sealed class MatchJoinController : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField addressInput;

    [SerializeField]
    private Button connectButton;

    [SerializeField]
    private Button backButton;

    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private string defaultAddress = "127.0.0.1";

    [Tooltip("A Vissza gombbal betöltött jelenet.")]
    [SerializeField]
    private string mainMenuSceneName = "MainMenu";

    private bool _connecting;

    private void Awake()
    {
        if (addressInput != null && string.IsNullOrWhiteSpace(addressInput.text))
            addressInput.text = defaultAddress;

        string reason = NetworkSession.LastDisconnectReason;
        NetworkSession.LastDisconnectReason = null;
        SetStatus(string.IsNullOrEmpty(reason) ? string.Empty : reason);
    }

    private void OnEnable()
    {
        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectClicked);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnDisable()
    {
        if (connectButton != null)
            connectButton.onClick.RemoveListener(OnConnectClicked);

        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);

        StopListening();
    }

    public void OnConnectClicked()
    {
        if (_connecting)
            return;

        if (!TryParseAddress(addressInput != null ? addressInput.text : defaultAddress,
                out string host, out ushort port))
        {
            SetStatus("Érvénytelen cím. Példa: 192.168.0.12 vagy 192.168.0.12:7777");
            return;
        }

        NetworkSession.Shutdown();

        if (!NetworkSession.StartClient(host, port))
        {
            SetStatus("A csatlakozás nem indítható el.");
            return;
        }

        _connecting = true;
        NetworkSession.Lobby.Disconnected += OnDisconnected;

        if (connectButton != null)
            connectButton.interactable = false;

        SetStatus($"Csatlakozás: {host}:{port}…");
    }

    public void OnBackClicked()
    {
        StopListening();
        NetworkSession.Shutdown();

        if (Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else
            Debug.LogError($"A(z) '{mainMenuSceneName}' jelenet nincs a Build Settings listájában.", this);
    }

    // „cím” vagy „cím:port” alakú bemenet feldolgozása.
    public static bool TryParseAddress(string input, out string host, out ushort port)
    {
        host = null;
        port = NetworkSession.DefaultPort;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        string trimmed = input.Trim();
        int colon = trimmed.LastIndexOf(':');

        if (colon >= 0)
        {
            if (!ushort.TryParse(trimmed.Substring(colon + 1), out port) || port == 0)
                return false;

            trimmed = trimmed.Substring(0, colon);
        }

        if (trimmed.Length == 0 || trimmed.Contains(" "))
            return false;

        host = trimmed;
        return true;
    }

    private void OnDisconnected(string reason)
    {
        StopListening();
        NetworkSession.Shutdown();

        if (connectButton != null)
            connectButton.interactable = true;

        SetStatus(reason == LobbyNetwork.ConnectionLostReason
            ? "Nem sikerült csatlakozni. Ellenőrizd a címet, és hogy a host lobbyja nyitva van-e."
            : reason);
    }

    private void StopListening()
    {
        if (!_connecting)
            return;

        _connecting = false;

        if (NetworkSession.Lobby != null)
            NetworkSession.Lobby.Disconnected -= OnDisconnected;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
