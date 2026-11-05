using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuTests
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    private GameObject _root;

    [TearDown]
    public void TearDown()
    {
        if (_root != null)
            Object.DestroyImmediate(_root);
    }

    private MainMenuController CreateMenu(out TMP_Text statusText)
    {
        _root = new GameObject("MainMenuTest");
        MainMenuController controller = _root.AddComponent<MainMenuController>();

        statusText = new GameObject("Status").AddComponent<TextMeshProUGUI>();
        statusText.transform.SetParent(_root.transform);

        Button hostButton = new GameObject("Host").AddComponent<Button>();
        hostButton.transform.SetParent(_root.transform);

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("statusText").objectReferenceValue = statusText;
        serialized.FindProperty("hostButton").objectReferenceValue = hostButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return controller;
    }

    [Test]
    public void MainMenu_IsTheFirstSceneInBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        Assert.Greater(scenes.Length, 0);
        Assert.AreEqual(MainMenuScenePath, scenes[0].path);
        Assert.IsTrue(scenes[0].enabled);
    }

    [Test]
    public void HostScene_IsInBuildSettings()
    {
        // A MainMenu jelenetben beállított Host-jelenet neve.
        string sceneText = File.ReadAllText(MainMenuScenePath);
        Match match = Regex.Match(sceneText, @"hostSceneName:\s*(\S+)");

        Assert.IsTrue(match.Success, "A MainMenu jelenetben nincs MainMenuController.");

        string hostScene = match.Groups[1].Value;
        bool found = false;

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && Path.GetFileNameWithoutExtension(scene.path) == hostScene)
                found = true;
        }

        Assert.IsTrue(found, $"A Host-jelenet ({hostScene}) nincs a Build Settings jelenetlistájában.");
    }

    [Test]
    public void Join_ShowsLobbyNotAvailableMessage()
    {
        MainMenuController controller = CreateMenu(out TMP_Text statusText);

        controller.OnJoinClicked();

        Assert.AreEqual("A lobby még nem elérhető.", statusText.text);
    }

    [Test]
    public void StatusText_StartsEmpty()
    {
        MainMenuController controller = CreateMenu(out TMP_Text statusText);
        statusText.text = "régi szöveg";

        controller.SendMessage("Awake");

        Assert.AreEqual(string.Empty, statusText.text);
    }
}
