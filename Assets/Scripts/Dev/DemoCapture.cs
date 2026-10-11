using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace LudoGame
{
    /// <summary>
    /// Development aid: launch a desktop build with "-demo-capture &lt;folder&gt;" to save screenshots of the menu
    /// and a computer-only match, then quit. Does nothing in normal play.
    /// </summary>
    sealed class DemoCapture : MonoBehaviour
    {
        string folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-demo-capture");
            if (i < 0 || i + 1 >= args.Length) return;
            new GameObject("DemoCapture").AddComponent<DemoCapture>().folder = args[i + 1];
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture("1-menu.png");

            GameSettings.Speed = 3;
            FindAnyObjectByType<GameRoot>().StartDemoMatch();
            yield return new WaitForSecondsRealtime(4f);
            yield return Capture("2-match-start.png");

            GameSettings.Speed = 6;
            yield return new WaitForSecondsRealtime(25f);
            yield return Capture("3-match-mid.png");

            GameSettings.Speed = GameSettings.MaxSpeed;
            float giveUp = Time.realtimeSinceStartup + 90f;
            while (GameObject.Find("Winner") == null && Time.realtimeSinceStartup < giveUp) yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("4-match-end.png");
            Application.Quit();
        }

        IEnumerator Capture(string name)
        {
            // Make sure this frame is actually drawn: the frame governor skips frames while nothing moves.
            FrameGovernor.Keep(0.5f);
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name));
            yield return null;
        }
    }
}
