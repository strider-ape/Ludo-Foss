using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// One-click Android build: applies the icon, signs with the release key when present (see ReleaseSigning),
    /// and fails if the privacy check finds extra permissions or tracking code. From the command line:
    /// Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod LudoGame.EditorTools.BuildAndroid.Build
    /// </summary>
    public static class BuildAndroid
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        static string OutputPath => "Builds/" + AppInfo.FileName + ".apk";

        [MenuItem("Ludo/Build Android APK")]
        public static void Build()
        {
            EnsureScene();
            ConfigurePlayer();
            AppIcon.Apply();
            bool releaseSigned = ReleaseSigning.Configure();
            Debug.Log(releaseSigned ? "[Ludo] Signing with the release key" : "[Ludo] No release key found: this APK will be debug-signed");

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = OutputPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.None,
                });
            }
            finally
            {
                ReleaseSigning.Clear();
            }

            var summary = report.summary;
            bool ok = summary.result == BuildResult.Succeeded;
            Debug.Log($"[Ludo] Build {summary.result}: {summary.outputPath} ({summary.totalErrors} errors)");
            if (ok)
            {
                ok = PrivacyGuard.Check(OutputPath, AppInfo.AndroidId, PrivacyGuard.FindAapt(), out var privacy);
                if (ok)
                {
                    Debug.Log(privacy);
                }
                else
                {
                    Debug.LogError(privacy);
                    File.Delete(OutputPath);
                }
            }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>The game builds its UI from code, so the scene only has to exist.</summary>
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "gdr97";
            PlayerSettings.productName = AppInfo.Name;
            PlayerSettings.bundleVersion = AppInfo.Version;
            PlayerSettings.Android.bundleVersionCode = AppInfo.VersionCode;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppInfo.AndroidId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.forceInternetPermission = false;
            EditorUserBuildSettings.buildAppBundle = false;
            DisableSplash();
        }

        /// <summary>No "Made with Unity" splash: the game opens straight to the menu.</summary>
        public static void DisableSplash()
        {
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
        }
    }
}
