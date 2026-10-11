using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Windowed desktop build for checking the game without a phone.
    /// Unity.exe -batchmode -quit -projectPath . -buildTarget Win64 -executeMethod LudoGame.EditorTools.WindowsPreview.Build
    /// Then: Builds/Windows/LudoFOSS.exe -screen-width 540 -screen-height 960 -demo-capture screenshots
    /// </summary>
    public static class WindowsPreview
    {
        [MenuItem("Ludo/Build Windows Preview")]
        public static void Build()
        {
            BuildAndroid.EnsureScene();
            PlayerSettings.companyName = "gdr97";
            PlayerSettings.productName = AppInfo.Name;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = "Builds/Windows/" + AppInfo.FileName + ".exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[Ludo] Windows preview {report.summary.result} ({report.summary.totalErrors} errors)");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
