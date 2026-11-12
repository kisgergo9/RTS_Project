using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A főmenü gombjainak kezelése: Host, Join, Exit.
[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField]
    private Button hostButton;

    [SerializeField]
    private Button joinButton;

    [SerializeField]
    private Button exitButton;

    [Header("Host")]
    [Tooltip("A Host gombbal betöltött jelenet neve. A jelenetnek szerepelnie kell a " +
             "Build Settings jelenetlistájában.")]
    [SerializeField]
    private string hostSceneName = "MatchLobby";

    [Header("Join")]
    [Tooltip("A Join gombbal betöltött jelenet neve (csatlakozás a hosthoz).")]
    [SerializeField]
    private string joinSceneName = "MatchJoin";

    [Tooltip("Hibaüzenetek helye. Kezdetben üres.")]
    [SerializeField]
    private TMP_Text statusText;

    public string HostSceneName => hostSceneName;
    public string JoinSceneName => joinSceneName;

    private void Awake()
    {
        if (statusText != null)
            statusText.text = string.Empty;
    }

    private void OnEnable()
    {
        AddListener(hostButton, OnHostClicked);
        AddListener(joinButton, OnJoinClicked);
        AddListener(exitButton, OnExitClicked);
    }

    private void OnDisable()
    {
        RemoveListener(hostButton, OnHostClicked);
        RemoveListener(joinButton, OnJoinClicked);
        RemoveListener(exitButton, OnExitClicked);
    }

    public void OnHostClicked()
    {
        LoadScene(hostSceneName, "Host");
    }

    public void OnJoinClicked()
    {
        LoadScene(joinSceneName, "Join");
    }

    public void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadScene(string sceneName, string buttonName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"A {buttonName} gombhoz nincs jelenet megadva.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            string message = $"A(z) '{sceneName}' jelenet nem tölthető be: nincs a Build Settings " +
                             "jelenetlistájában.";

            Debug.LogError(message, this);

            if (statusText != null)
                statusText.text = message;

            return;
        }

        SceneManager.LoadScene(sceneName);
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
