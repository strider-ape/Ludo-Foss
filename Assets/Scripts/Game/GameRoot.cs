using LudoTurbo.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LudoTurbo
{
    static class Bootstrap
    {
        /// <summary>The scene is empty: everything is created from code when it loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindAnyObjectByType<GameRoot>() == null) new GameObject("GameRoot").AddComponent<GameRoot>();
        }
    }

    /// <summary>Owns the camera, canvas and screens (menu, match, settings, winner).</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        RectTransform canvas;
        RectTransform screen;
        RectTransform overlay;
        Match match;
        readonly SeatType[] seats = new SeatType[GameState.Players];

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            for (int p = 0; p < GameState.Players; p++) seats[p] = GameSettings.GetSeat((PlayerColor)p);

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            var c = canvasGo.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvas = (RectTransform)canvasGo.transform;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            ShowMenu();
        }

        void Update()
        {
            // Android back button.
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (overlay != null && match != null && Time.timeScale == 0f) CloseSettings();
            else if (match != null && overlay == null) OpenSettings();
            else if (match == null && overlay == null) Application.Quit();
        }

        void ClearScreen()
        {
            Time.timeScale = 1f;
            if (overlay != null) Destroy(overlay.gameObject);
            if (screen != null) Destroy(screen.gameObject);
            overlay = null;
            screen = null;
            match = null;
        }

        // ---------- Menu ----------

        void ShowMenu()
        {
            ClearScreen();
            screen = Ui.Fill("Menu", canvas);
            Ui.Label(screen, "LUDO TURBO", 120, Palette.Text, new Vector2(1000, 160), new Vector2(0, 720));
            Ui.Label(screen, "Offline  -  No ads  -  Your speed", 40, new Color(1f, 1f, 1f, 0.7f), new Vector2(1000, 60), new Vector2(0, 620), TextAnchor.MiddleCenter, FontStyle.Normal);

            Ui.Label(screen, "Tap a colour to change who plays it", 36, new Color(1f, 1f, 1f, 0.6f), new Vector2(1000, 50), new Vector2(0, 500), TextAnchor.MiddleCenter, FontStyle.Normal);
            for (int p = 0; p < GameState.Players; p++)
            {
                var color = (PlayerColor)p;
                Button seatButton = null;
                seatButton = Ui.Button(screen, SeatLabel(color), Palette.Of(color), new Vector2(820, 120), new Vector2(0, 400 - p * 145), () =>
                {
                    seats[(int)color] = seats[(int)color] == SeatType.Human ? SeatType.Computer
                        : seats[(int)color] == SeatType.Computer ? SeatType.Off : SeatType.Human;
                    GameSettings.SetSeat(color, seats[(int)color]);
                    Ui.SetText(seatButton, SeatLabel(color));
                    seatButton.targetGraphic.color = seats[(int)color] == SeatType.Off ? Palette.ButtonMuted : Palette.Of(color);
                });
                if (seats[p] == SeatType.Off) seatButton.targetGraphic.color = Palette.ButtonMuted;
            }

            BuildSpeedRow(screen, -230);
            BuildToggle(screen, -380, "Auto-move", () => GameSettings.AutoMove, v => GameSettings.AutoMove = v);

            Ui.Button(screen, "PLAY", Palette.Button, new Vector2(620, 160), new Vector2(0, -620), StartMatch, 72);
        }

        string SeatLabel(PlayerColor c)
        {
            string who = seats[(int)c] == SeatType.Human ? "Player" : seats[(int)c] == SeatType.Computer ? "Computer" : "Off";
            return Palette.Name(c) + "  -  " + who;
        }

        void StartMatch()
        {
            int active = 0;
            foreach (var s in seats) if (s != SeatType.Off) active++;
            if (active < 2) return;

            ClearScreen();
            var seatsCopy = (SeatType[])seats.Clone();
            match = Match.Create(canvas, seatsCopy, OpenSettings);
            screen = (RectTransform)match.transform;
            match.Won += ShowWinner;
        }

        /// <summary>Starts a computer-only match without changing the saved seats (used by DemoCapture).</summary>
        internal void StartDemoMatch()
        {
            for (int p = 0; p < GameState.Players; p++) seats[p] = SeatType.Computer;
            StartMatch();
        }

        // ---------- Settings ----------

        void OpenSettings()
        {
            if (overlay != null) return;
            Time.timeScale = 0f;
            overlay = MakeOverlay("Settings", 960);
            var card = overlay.GetChild(0);
            Ui.Label(card, "SETTINGS", 70, Palette.Text, new Vector2(800, 100), new Vector2(0, 370));
            BuildSpeedRow(card, 220);
            BuildToggle(card, 80, "Auto-move", () => GameSettings.AutoMove, v => GameSettings.AutoMove = v);
            BuildToggle(card, -50, "Three 6s skip turn", () => GameSettings.ThreeSixesRule, v => GameSettings.ThreeSixesRule = v);
            Ui.Button(card, "RESUME", Palette.Button, new Vector2(640, 130), new Vector2(0, -220), CloseSettings, 54);
            Ui.Button(card, "QUIT TO MENU", Palette.ButtonMuted, new Vector2(640, 110), new Vector2(0, -370), ShowMenu, 44);
        }

        void CloseSettings()
        {
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = null;
            Time.timeScale = 1f;
        }

        // ---------- Winner ----------

        void ShowWinner(PlayerColor winner)
        {
            overlay = MakeOverlay("Winner", 640);
            var card = overlay.GetChild(0);
            var title = Ui.Label(card, Palette.Name(winner) + " WINS!", 96, Palette.Of(winner), new Vector2(800, 140), new Vector2(0, 180));
            title.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            Ui.Button(card, "PLAY AGAIN", Palette.Button, new Vector2(640, 130), new Vector2(0, -20), StartMatch, 54);
            Ui.Button(card, "MENU", Palette.ButtonMuted, new Vector2(640, 110), new Vector2(0, -180), ShowMenu, 44);
        }

        // ---------- Shared widgets ----------

        RectTransform MakeOverlay(string name, float cardHeight)
        {
            var root = Ui.Fill(name, canvas);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = Palette.Overlay;
            dim.raycastTarget = true;
            Ui.Image("Card", root, SpriteFactory.Rounded, Palette.Card, new Vector2(880, cardHeight), Vector2.zero, sliced: true);
            return root;
        }

        static void BuildSpeedRow(Transform parent, float y)
        {
            Ui.Label(parent, "Speed", 40, new Color(1f, 1f, 1f, 0.7f), new Vector2(800, 50), new Vector2(0, y + 70), TextAnchor.MiddleCenter, FontStyle.Normal);
            var value = Ui.Label(parent, GameSettings.SpeedLabel, 56, Palette.Text, new Vector2(420, 100), new Vector2(0, y));
            Ui.Button(parent, "-", Palette.ButtonMuted, new Vector2(140, 110), new Vector2(-320, y), () =>
            {
                GameSettings.Speed--;
                value.text = GameSettings.SpeedLabel;
            }, 70);
            Ui.Button(parent, "+", Palette.ButtonMuted, new Vector2(140, 110), new Vector2(320, y), () =>
            {
                GameSettings.Speed++;
                value.text = GameSettings.SpeedLabel;
            }, 70);
        }

        static void BuildToggle(Transform parent, float y, string name, System.Func<bool> get, System.Action<bool> set)
        {
            Button button = null;
            button = Ui.Button(parent, ToggleText(name, get()), get() ? Palette.Button : Palette.ButtonMuted, new Vector2(780, 110), new Vector2(0, y), () =>
            {
                set(!get());
                Ui.SetText(button, ToggleText(name, get()));
                button.targetGraphic.color = get() ? Palette.Button : Palette.ButtonMuted;
            }, 44);
        }

        static string ToggleText(string name, bool on) => name + ":  " + (on ? "ON" : "OFF");
    }
}
