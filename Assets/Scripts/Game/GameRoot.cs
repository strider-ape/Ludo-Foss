using LudoGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LudoGame
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

    /// <summary>Owns the camera, board and canvas, and switches between the menu, a match and the overlays.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        static readonly Color PlayGreen = new Color(0.17f, 0.75f, 0.35f);

        RectTransform canvas;
        RectTransform screen;
        RectTransform overlay;
        Match match;
        CameraRig rig;
        Board3D board;
        readonly SeatType[] seats = new SeatType[GameState.Players];

        void Awake()
        {
            FrameGovernor.ApplyTargetFrameRate();
            QualitySettings.vSyncCount = 0;
            for (int p = 0; p < GameState.Players; p++) seats[p] = GameSettings.GetSeat((PlayerColor)p);

            Materials.SetupLighting();
            rig = CameraRig.Create();
            board = new Board3D(null);
            Sfx.Init(rig.gameObject);

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
            if (Input.anyKeyDown || Input.touchCount > 0) FrameGovernor.Keep(0.35f);
            FrameGovernor.Tick();

            // Android back button.
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (overlay != null && Time.timeScale == 0f) CloseSettings();
            else if (match != null && overlay == null) OpenSettings();
            else if (match == null && overlay == null) Application.Quit();
        }

#if UNITY_ANDROID || UNITY_IOS
        void OnApplicationPause(bool paused)
        {
            if (paused && match != null && overlay == null) OpenSettings();
        }
#endif

        void ClearScreen()
        {
            Time.timeScale = 1f;
            if (overlay != null) Destroy(overlay.gameObject);
            if (screen != null) Destroy(screen.gameObject);
            overlay = null;
            screen = null;
            match = null;
            FrameGovernor.Keep(0.3f);
        }

        // ---------- Menu ----------

        void ShowMenu()
        {
            ClearScreen();
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            board.SetRotationSteps(0);
            screen = Ui.Fill("Menu", canvas);
            var dim = screen.gameObject.AddComponent<Image>();
            dim.color = new Color(0.05f, 0.07f, 0.17f, 0.86f);
            dim.raycastTarget = true;

            Ui.Label(screen, AppInfo.Name.ToUpperInvariant(), 150, Palette.Text, new Vector2(1040, 190), new Vector2(0, 720));
            for (int p = 0; p < GameState.Players; p++)
            {
                Ui.Avatar(screen, Palette.Of((PlayerColor)p), 96f, new Vector2(-195f + p * 130f, 575f));
            }
            Ui.Label(screen, "No ads  -  No tracking  -  Just Ludo", 40, new Color(1f, 1f, 1f, 0.75f), new Vector2(1000, 60), new Vector2(0, 470), TextAnchor.MiddleCenter, shadow: false);

            Ui.Label(screen, "WHO'S PLAYING?", 46, Palette.Gold, new Vector2(1000, 60), new Vector2(0, 375));
            for (int p = 0; p < GameState.Players; p++)
            {
                var color = (PlayerColor)p;
                Button seatButton = null;
                seatButton = Ui.Button(screen, SeatLabel(color), SeatColor(color), new Vector2(820, 108), new Vector2(0, 280 - p * 130), () =>
                {
                    seats[(int)color] = seats[(int)color] == SeatType.Human ? SeatType.Computer
                        : seats[(int)color] == SeatType.Computer ? SeatType.Off : SeatType.Human;
                    GameSettings.SetSeat(color, seats[(int)color]);
                    Ui.SetText(seatButton, SeatLabel(color));
                    Ui.SetColor(seatButton, SeatColor(color));
                }, 46);
            }
            Ui.Label(screen, "Tap a colour to switch Player / Computer / Off", 30, new Color(1f, 1f, 1f, 0.55f), new Vector2(1000, 44), new Vector2(0, -205), TextAnchor.MiddleCenter, shadow: false);

            BuildSpeedRow(screen, -350);
            Ui.Button(screen, "PLAY", PlayGreen, new Vector2(620, 170), new Vector2(0, -560), StartMatch, 84);
            Ui.Button(screen, "SETTINGS", Palette.ButtonMuted, new Vector2(420, 96), new Vector2(0, -760), OpenSettings, 40);
        }

        Color SeatColor(PlayerColor c) => seats[(int)c] == SeatType.Off ? Palette.ButtonMuted : Palette.Of(c);

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
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            match = Match.Create(canvas, board, rig, (SeatType[])seats.Clone(), OpenSettings);
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
            if (match != null) Time.timeScale = 0f;
            bool inMatch = match != null;
            overlay = MakeOverlay("Settings", inMatch ? 1340 : 1200);
            var card = overlay.GetChild(0);
            float top = inMatch ? 560 : 490;
            Ui.Label(card, inMatch ? "PAUSED" : "SETTINGS", 78, Palette.Text, new Vector2(800, 100), new Vector2(0, top));
            BuildSpeedRow(card, top - 150);
            BuildToggle(card, top - 290, "Auto-move", () => GameSettings.AutoMove, v => GameSettings.AutoMove = v);
            BuildToggle(card, top - 410, "Three 6s skip turn", () => GameSettings.ThreeSixesRule, v => GameSettings.ThreeSixesRule = v);
            BuildToggle(card, top - 530, "Sound", () => GameSettings.Sound, v => GameSettings.Sound = v);
            BuildToggle(card, top - 650, "Vibration", () => GameSettings.Vibration, v => GameSettings.Vibration = v);
            BuildToggle(card, top - 770, "Battery saver", () => GameSettings.BatterySaver, v => GameSettings.BatterySaver = v);
            Ui.Button(card, inMatch ? "RESUME" : "DONE", PlayGreen, new Vector2(640, 130), new Vector2(0, top - 930), CloseSettings, 56);
            if (inMatch) Ui.Button(card, "QUIT TO MENU", Palette.ButtonMuted, new Vector2(640, 110), new Vector2(0, top - 1080), ShowMenu, 44);
        }

        void CloseSettings()
        {
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = null;
            Time.timeScale = 1f;
            FrameGovernor.Keep(0.3f);
        }

        // ---------- Winner ----------

        void ShowWinner(PlayerColor winner)
        {
            overlay = MakeOverlay("Winner", 700);
            var card = overlay.GetChild(0);
            Ui.Avatar(card, Palette.Of(winner), 150f, new Vector2(0, 230));
            Ui.Label(card, Palette.Name(winner) + " WINS!", 104, Palette.Of(winner), new Vector2(840, 140), new Vector2(0, 70))
                .gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.85f);
            Ui.Button(card, "PLAY AGAIN", PlayGreen, new Vector2(640, 130), new Vector2(0, -100), StartMatch, 56);
            Ui.Button(card, "MENU", Palette.ButtonMuted, new Vector2(640, 110), new Vector2(0, -250), ShowMenu, 44);
        }

        // ---------- Shared widgets ----------

        RectTransform MakeOverlay(string name, float cardHeight)
        {
            var root = Ui.Fill(name, canvas);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = Palette.Overlay;
            dim.raycastTarget = true;
            Ui.Image("Card", root, SpriteFactory.Rounded, Palette.Card, new Vector2(900, cardHeight), Vector2.zero, sliced: true);
            FrameGovernor.Keep(0.3f);
            return root;
        }

        static void BuildSpeedRow(Transform parent, float y)
        {
            Ui.Label(parent, "SPEED", 36, new Color(1f, 1f, 1f, 0.7f), new Vector2(800, 50), new Vector2(0, y + 72), TextAnchor.MiddleCenter, shadow: false);
            var value = Ui.Label(parent, GameSettings.SpeedLabel, 60, Palette.Text, new Vector2(420, 100), new Vector2(0, y));
            Ui.Button(parent, "-", Palette.ButtonMuted, new Vector2(140, 110), new Vector2(-320, y), () =>
            {
                GameSettings.Speed--;
                value.text = GameSettings.SpeedLabel;
            }, 76);
            Ui.Button(parent, "+", Palette.ButtonMuted, new Vector2(140, 110), new Vector2(320, y), () =>
            {
                GameSettings.Speed++;
                value.text = GameSettings.SpeedLabel;
            }, 76);
        }

        static void BuildToggle(Transform parent, float y, string name, System.Func<bool> get, System.Action<bool> set)
        {
            Button button = null;
            button = Ui.Button(parent, ToggleText(name, get()), get() ? Palette.Button : Palette.ButtonMuted, new Vector2(760, 100), new Vector2(0, y), () =>
            {
                set(!get());
                Ui.SetText(button, ToggleText(name, get()));
                Ui.SetColor(button, get() ? Palette.Button : Palette.ButtonMuted);
            }, 42);
        }

        static string ToggleText(string name, bool on) => name + ":  " + (on ? "ON" : "OFF");
    }
}
