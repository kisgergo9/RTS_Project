using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A főmenü gombjainak kezelése: Host, Join, Exit.
///
///   - Host: a hostSceneName jelenetet (a lobbyt) tölti be,
///   - Join: rövid tájékoztató üzenetet jelenít meg (amíg nincs hálózat),
///   - Exit: kilép a játékból (szerkesztőben leállítja a Play módot).
/// </summary>
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
    [Tooltip("A Join gomb megnyomásakor megjelenő üzenet helye. Kezdetben üres.")]
    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private string joinUnavailableMessage = "A lobby még nem elérhető.";

    public string HostSceneName => hostSceneName;

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
        if (string.IsNullOrWhiteSpace(hostSceneName))
        {
            Debug.LogError("A Host gombhoz nincs jelenet megadva.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(hostSceneName))
        {
            Debug.LogError(
                $"A(z) '{hostSceneName}' jelenet nem tölthető be: nincs a Build Settings " +
                "jelenetlistájában.",
                this);
            return;
        }

        SceneManager.LoadScene(hostSceneName);
    }

    public void OnJoinClicked()
    {
        if (statusText != null)
            statusText.text = joinUnavailableMessage;
    }

    public void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
