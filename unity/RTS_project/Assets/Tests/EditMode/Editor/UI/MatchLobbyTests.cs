using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

public sealed class MatchLobbyTests
{
    private const string MatchLobbyScenePath = "Assets/Scenes/MatchLobby.unity";
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

    private static string ReadSceneValue(string scenePath, string field)
    {
        Match match = Regex.Match(File.ReadAllText(scenePath), field + @":\s*(.+)");

        Assert.IsTrue(match.Success, $"{scenePath}: nincs '{field}' mező.");
        return match.Groups[1].Value.Trim();
    }

    private static bool IsInBuildSettings(string sceneName)
    {
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && Path.GetFileNameWithoutExtension(scene.path) == sceneName)
                return true;
        }

        return false;
    }

    [Test]
    public void MainMenuHost_OpensMatchLobby()
    {
        Assert.AreEqual("MatchLobby", ReadSceneValue(MainMenuScenePath, "hostSceneName"));
        Assert.IsTrue(IsInBuildSettings("MatchLobby"));
    }

    [Test]
    public void MatchLobby_HasWorldGenerationSettings()
    {
        StringAssert.DoesNotContain("fileID: 0", ReadSceneValue(MatchLobbyScenePath, "  settings"));
    }

    [Test]
    public void MatchLobbyButtons_OpenScenesInBuildSettings()
    {
        Assert.IsTrue(IsInBuildSettings(ReadSceneValue(MatchLobbyScenePath, "mainMenuSceneName")));
        Assert.IsTrue(IsInBuildSettings(ReadSceneValue(MatchLobbyScenePath, "onePlayerTestSceneName")));
    }
}
