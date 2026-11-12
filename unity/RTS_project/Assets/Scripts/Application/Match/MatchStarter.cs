using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// A lobbyból indított mérkőzés jelenetének indítója.
[DisallowMultipleComponent]
public sealed class MatchStarter : MonoBehaviour
{
    [Tooltip("A GameBootstrappert tartalmazó, a jelenetben inaktívan mentett GameObject.")]
    [SerializeField]
    private GameObject world;

    [SerializeField]
    private GameBootstrapper bootstrapper;

    [SerializeField]
    private WorldGenerationSetup worldGeneration;

    [Tooltip("A kliens ide tér vissza, ha a kapcsolat megszakad.")]
    [SerializeField]
    private string joinSceneName = "MatchJoin";

    [Tooltip("Ide tér vissza a host (vagy a kliens, ha nincs csatlakozási képernyő), ha a mérkőzés nem indítható.")]
    [SerializeField]
    private string mainMenuSceneName = "MainMenu";

    [Tooltip("Ennyi ideig vár a mérkőzés adataira (a kliensen a hosttól érkeznek).")]
    [SerializeField, Min(1f)]
    private float setupTimeoutSeconds = 10f;

    private bool _isClient;

    private void Awake()
    {
        if (world != null && world.activeSelf)
            Debug.LogWarning("A mérkőzés világ-GameObjectje aktívan van mentve; inaktívnak kell lennie.", this);
    }

    private void OnEnable()
    {
        _isClient = NetworkSession.IsClientOnly;

        if (_isClient && NetworkSession.Lobby != null)
            NetworkSession.Lobby.Disconnected += OnDisconnected;
    }

    private void OnDisable()
    {
        if (NetworkSession.Lobby != null)
            NetworkSession.Lobby.Disconnected -= OnDisconnected;
    }

    private IEnumerator Start()
    {
        float deadline = Time.realtimeSinceStartup + setupTimeoutSeconds;

        while (MatchSetup.Current == null)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Fail("Nem érkeztek meg a mérkőzés adatai a hosttól.");
                yield break;
            }

            yield return null;
        }

        MatchSetup setup = MatchSetup.Current;
        MatchSetup.Current = null;

        StartMatch(setup);
    }

    private void StartMatch(MatchSetup setup)
    {
        WorldLayout layout = WorldLayoutSnapshot.FromState(setup.World);
        PlayerSetup[] players = new PlayerSetup[setup.Players.Count];

        for (int i = 0; i < players.Length; i++)
        {
            players[i] = new PlayerSetup
            {
                playerId = setup.Players[i].PlayerId,
                playerName = setup.Players[i].Name
            };
        }

        worldGeneration.UsePresetLayout(layout);
        bootstrapper.ConfigureMatch(players, setup.LocalPlayerId);
        world.SetActive(true);

        FocusCameraOnPlayerBase(layout, setup.LocalPlayerId);
    }

    private static void FocusCameraOnPlayerBase(WorldLayout layout, int playerId)
    {
        Camera camera = Camera.main;

        if (camera == null)
            return;

        foreach (BaseLayout baseLayout in layout.Bases)
        {
            if (baseLayout.OwnerType != BaseOwnerType.Player || baseLayout.PlayerId != playerId)
                continue;

            Vector3 position = camera.transform.position;
            camera.transform.position = new Vector3(baseLayout.Position.x, baseLayout.Position.y, position.z);
            return;
        }
    }

    private void OnDisconnected(string reason)
    {
        NetworkSession.LastDisconnectReason = reason;
        NetworkSession.Shutdown();
        LoadScene(joinSceneName);
    }

    private void Fail(string message)
    {
        Debug.LogError(message, this);

        bool wasClient = _isClient;
        NetworkSession.LastDisconnectReason = message;
        NetworkSession.Shutdown();
        LoadScene(wasClient ? joinSceneName : mainMenuSceneName);
    }

    private void LoadScene(string sceneName)
    {
        if (Application.CanStreamedLevelBeLoaded(sceneName))
            SceneManager.LoadScene(sceneName);
        else
            Debug.LogError($"A(z) '{sceneName}' jelenet nincs a Build Settings listájában.", this);
    }
}
