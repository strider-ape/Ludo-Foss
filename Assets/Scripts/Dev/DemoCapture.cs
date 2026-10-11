using System;
using System.Collections;
using System.IO;
using LudoGame.Core;
using UnityEngine;

namespace LudoGame
{
    /// <summary>
    /// Development aid: launch a desktop build with "-demo-capture &lt;folder&gt;" to save screenshots of the menu,
    /// settings, the move preview, a match and the results podium, then quit. Does nothing in normal play.
    /// Red is played by this script (rolling and picking like a person would) so the helpers appear.
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
            var root = FindAnyObjectByType<GameRoot>();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Capture("1-menu.png");

            root.DemoOpenSettings();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("2-settings.png");
            root.DemoCloseSettings();

            GameSettings.Speed = 4;
            GameSettings.AutoMove = false;
            GameSettings.TurnTimer = 15;
            GameSettings.Preview = PreviewMode.MarkerAndPath;
            GameSettings.PlayForPlaces = true;
            root.DemoStartMatch(new[] { SeatType.Human, SeatType.Computer, SeatType.Computer, SeatType.Computer });

            bool previewShot = false, matchShot = false;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup < start + 300f && GameObject.Find("Results") == null)
            {
                var match = root.CurrentMatch;
                float elapsed = Time.realtimeSinceStartup - start;
                if (match.AwaitingRoll)
                {
                    if (!matchShot) yield return new WaitForSecondsRealtime(0.4f);
                    match.DemoRoll();
                }
                else if (match.Picking)
                {
                    if (!previewShot && match.PreviewDots > 0)
                    {
                        yield return new WaitForSecondsRealtime(0.5f);
                        yield return Capture("3-preview.png");
                        previewShot = true;
                    }
                    match.DemoPick();
                }
                if (!matchShot && (previewShot && elapsed > 25f || elapsed > 70f))
                {
                    yield return Capture("4-match.png");
                    matchShot = true;
                    GameSettings.Speed = GameSettings.MaxSpeed;
                }
                if (matchShot && match.CanSkip) match.DemoSkip();
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("5-results.png");
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
