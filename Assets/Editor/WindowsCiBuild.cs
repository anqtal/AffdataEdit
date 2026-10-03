using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class WindowsCiBuild
{
    public static void Build()
    {
        const string output = "Build/Windows/AffdataEdit.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        const string startupScenePath = "Assets/AffdataStartup.unity";
        if (File.Exists(startupScenePath))
            throw new BuildFailedException($"Temporary startup scene already exists: {startupScenePath}");
        var startupScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var checker = new GameObject("Startup version check").AddComponent<Arcade.Compose.AdeUpdateChecker>();
        checker.DisplayFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSans-Regular.ttf");
        if (!EditorSceneManager.SaveScene(startupScene, startupScenePath))
            throw new BuildFailedException("Could not create startup version check scene.");
        BuildReport report;
        try
        {
            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { startupScenePath, "Assets/_Scenes/ArcEditor.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.StrictMode
            });
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(startupScenePath);
        }
        if (report.summary.result != BuildResult.Succeeded || report.summary.totalWarnings != 0)
            throw new BuildFailedException($"Windows build: {report.summary.result}, " +
                $"{report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings.");
        foreach (string backup in Directory.GetDirectories("Build/Windows", "*_BackUpThisFolder_ButDontShipItWithYourGame"))
            Directory.Delete(backup, true);
    }
}
