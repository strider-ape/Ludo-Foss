using System.Collections.Generic;
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
        static readonly Color Silver = new Color(0.78f, 0.82f, 0.87f);
        static readonly Color Bronze = new Color(0.82f, 0.54f, 0.31f);

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
            ApplyBoardSize();
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
            if (overlay != null && overlay.name == "Settings") CloseSettings();
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
                var avatar = Ui.Avatar(screen, Palette.Of((PlayerColor)p), 96f, new Vector2(-195f + p * 130f, 575f));
                if (GameSettings.ColorShapes) Ui.ShapeBadge(avatar, (PlayerColor)p, 96f);
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
            bool canContinue = GameSettings.ResumeGames && SaveGame.Exists;
            if (canContinue)
            {
                Ui.Button(screen, "CONTINUE", PlayGreen, new Vector2(620, 150), new Vector2(0, -545), ContinueMatch, 76);
                Ui.Button(screen, "NEW GAME", Palette.Button, new Vector2(500, 110), new Vector2(0, -705), () => StartMatch(null), 50);
                Ui.Button(screen, "SETTINGS", Palette.ButtonMuted, new Vector2(420, 90), new Vector2(0, -835), OpenSettings, 38);
            }
            else
            {
                Ui.Button(screen, "PLAY", PlayGreen, new Vector2(620, 170), new Vector2(0, -560), () => StartMatch(null), 84);
                Ui.Button(screen, "SETTINGS", Palette.ButtonMuted, new Vector2(420, 96), new Vector2(0, -760), OpenSettings, 40);
            }
        }

        Color SeatColor(PlayerColor c) => seats[(int)c] == SeatType.Off ? Palette.ButtonMuted : Palette.Of(c);

        string SeatLabel(PlayerColor c)
        {
            string who = seats[(int)c] == SeatType.Human ? "Player" : seats[(int)c] == SeatType.Computer ? "Computer" : "Off";
            return Palette.Name(c) + "  -  " + who;
        }

        void ContinueMatch()
        {
            var saved = SaveGame.Load();
            if (saved == null)
            {
                ShowMenu();
                return;
            }
            StartMatch(saved);
        }

        void StartMatch(GameState restored)
        {
            if (restored == null)
            {
                int active = 0;
                foreach (var s in seats) if (s != SeatType.Off) active++;
                if (active < 2) return;
            }

            ClearScreen();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            match = Match.Create(canvas, board, rig, (SeatType[])seats.Clone(), restored, OpenSettings);
            screen = (RectTransform)match.transform;
            match.GameOver += ShowResults;
        }

        // ---------- Settings ----------

        void OpenSettings()
        {
            if (overlay != null) return;
            if (match != null) Time.timeScale = 0f;
            bool inMatch = match != null;
            float cardHeight = inMatch ? 1500f : 1360f;
            overlay = MakeOverlay("Settings", cardHeight);
            var card = overlay.GetChild(0);
            float top = cardHeight * 0.5f;
            Ui.Label(card, inMatch ? "PAUSED" : "SETTINGS", 78, Palette.Text, new Vector2(800, 100), new Vector2(0, top - 85));

            const float viewportHeight = 1000f;
            float viewportY = top - 150f - viewportHeight * 0.5f;
            var rows = new SettingsRows(card, new Vector2(860f, viewportHeight), new Vector2(0f, viewportY));
            rows.Speed();
            rows.Header("GAMEPLAY");
            rows.Toggle("Auto-move", () => GameSettings.AutoMove, v => GameSettings.AutoMove = v);
            rows.Toggle("Three 6s skip turn", () => GameSettings.ThreeSixesRule, v => GameSettings.ThreeSixesRule = v);
            rows.Toggle("Play for all places", () => GameSettings.PlayForPlaces, v => GameSettings.PlayForPlaces = v);
            rows.Header("HELPERS");
            rows.Cycle("Move preview", new[] { "Off", "Marker", "Marker + path" }, () => (int)GameSettings.Preview, i => GameSettings.Preview = (PreviewMode)i);
            rows.Cycle("Turn timer", new[] { "Off", "10s", "15s", "30s" },
                () => System.Array.IndexOf(GameSettings.TimerChoices, GameSettings.TurnTimer), i => GameSettings.TurnTimer = GameSettings.TimerChoices[i]);
            rows.Cycle("When time runs out", new[] { "Skip turn", "Auto-play" }, () => GameSettings.TimerAutoPlays ? 1 : 0, i => GameSettings.TimerAutoPlays = i == 1);
            rows.Toggle("Resume unfinished game", () => GameSettings.ResumeGames, v => GameSettings.ResumeGames = v);
            rows.Header("LOOK");
            rows.Cycle("Pawn style", new[] { "Classic", "Outline", "Outline + coin" }, () => (int)GameSettings.Pawns, i => GameSettings.Pawns = (PawnLook)i);
            rows.Toggle("Colour shapes", () => GameSettings.ColorShapes, v => GameSettings.ColorShapes = v);
            rows.Header("BIGGER BOARD");
            rows.Toggle("Edge-to-edge board", () => GameSettings.EdgeToEdge, v => GameSettings.EdgeToEdge = v);
            rows.Toggle("Zoom to choices", () => GameSettings.ZoomToChoices, v => GameSettings.ZoomToChoices = v);
            rows.Toggle("Generous taps", () => GameSettings.GenerousTaps, v => GameSettings.GenerousTaps = v);
            rows.Header("SOUND & DEVICE");
            rows.Toggle("Sound", () => GameSettings.Sound, v => GameSettings.Sound = v);
            rows.Toggle("Vibration", () => GameSettings.Vibration, v => GameSettings.Vibration = v);
            rows.Toggle("Battery saver", () => GameSettings.BatterySaver, v => GameSettings.BatterySaver = v);
            rows.Finish();

            float buttonsY = viewportY - viewportHeight * 0.5f - 105f;
            Ui.Button(card, inMatch ? "RESUME" : "DONE", PlayGreen, new Vector2(640, 125), new Vector2(0, buttonsY), CloseSettings, 54);
            if (inMatch) Ui.Button(card, "QUIT TO MENU", Palette.ButtonMuted, new Vector2(640, 105), new Vector2(0, buttonsY - 140), ShowMenu, 42);
        }

        void CloseSettings()
        {
            if (overlay != null) Destroy(overlay.gameObject);
            overlay = null;
            Time.timeScale = 1f;
            FrameGovernor.Keep(0.3f);
            if (match != null)
            {
                match.ApplyLook();
                return;
            }
            ApplyBoardSize();
            ShowMenu(); // refresh, e.g. CONTINUE appears or disappears with "Resume unfinished game"
        }

        /// <summary>Thin edge-to-edge frame or the classic one, then refit the camera to it.</summary>
        void ApplyBoardSize()
        {
            board.SetEdgeToEdge(GameSettings.EdgeToEdge);
            rig.Frame();
        }

        // ---------- Results ----------

        void ShowResults(List<Match.Placing> placings)
        {
            overlay = MakeOverlay("Results", 1240);
            var card = overlay.GetChild(0);
            var winner = placings[0];
            string title = winner.Human && winner.Name == "You" ? "YOU WIN!" : winner.Name.ToUpperInvariant() + " WINS!";
            Ui.Label(card, title, 96, Palette.Of(winner.Color), new Vector2(860, 130), new Vector2(0, 500))
                .gameObject.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.85f);

            // Podium: 2nd on the left, 1st in the middle, 3rd on the right.
            const float baseY = -140f;
            int[] places = { 1, 0, 2 };
            float[] xs = { -290f, 0f, 290f };
            float[] heights = { 200f, 290f, 150f };
            Color[] medals = { Silver, Palette.Gold, Bronze };
            for (int k = 0; k < 3; k++)
            {
                int place = places[k];
                if (place >= placings.Count) continue;
                var entry = placings[place];
                var step = Ui.Image("Step", card, SpriteFactory.Rounded, medals[k], new Vector2(250f, heights[k]), new Vector2(xs[k], baseY + heights[k] * 0.5f), sliced: true);
                Ui.Label(step.transform, (place + 1).ToString(), 96, Color.white, new Vector2(250f, heights[k]), Vector2.zero);
                float avatarY = baseY + heights[k] + 80f;
                var avatar = Ui.Avatar(card, Palette.Of(entry.Color), 130f, new Vector2(xs[k], avatarY));
                if (GameSettings.ColorShapes) Ui.ShapeBadge(avatar, entry.Color, 130f);
                if (place == 0) Ui.Image("Crown", card, SpriteFactory.Crown, Palette.Gold, new Vector2(92f, 92f), new Vector2(xs[k], avatarY + 98f));
                Ui.Label(card, entry.Name, 38, Color.white, new Vector2(270f, 50f), new Vector2(xs[k], baseY - 40f));
            }
            if (placings.Count > 3)
            {
                Ui.Label(card, "4th   " + placings[3].Name, 36, new Color(1f, 1f, 1f, 0.7f), new Vector2(700f, 50f), new Vector2(0f, baseY - 110f), TextAnchor.MiddleCenter, shadow: false);
            }

            Ui.Button(card, "PLAY AGAIN", PlayGreen, new Vector2(640, 130), new Vector2(0, -400), () => StartMatch(null), 56);
            Ui.Button(card, "MENU", Palette.ButtonMuted, new Vector2(640, 110), new Vector2(0, -545), ShowMenu, 44);
        }

        // ---------- Shared widgets ----------

        RectTransform MakeOverlay(string name, float cardHeight)
        {
            var root = Ui.Fill(name, canvas);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = Palette.Overlay;
            dim.raycastTarget = true;
            Ui.Image("Card", root, SpriteFactory.Rounded, Palette.Card, new Vector2(920, cardHeight), Vector2.zero, sliced: true);
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

        /// <summary>A scrolling list of settings rows, laid out top to bottom.</summary>
        sealed class SettingsRows
        {
            const float ToggleHeight = 100f;
            const float Spacing = 18f;

            readonly RectTransform content;
            readonly List<(RectTransform rt, float top)> placed = new List<(RectTransform, float)>();
            float cursor;

            public SettingsRows(Transform parent, Vector2 size, Vector2 pos)
            {
                var viewport = Ui.Rect("Viewport", parent, size, pos);
                viewport.gameObject.AddComponent<RectMask2D>();
                var hit = viewport.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;
                content = Ui.Rect("Content", viewport, new Vector2(size.x, size.y), Vector2.zero);
                content.anchorMin = content.anchorMax = new Vector2(0.5f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.anchoredPosition = Vector2.zero;
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.content = content;
                scroll.viewport = viewport;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Elastic;
                scroll.scrollSensitivity = 40f;
                scroll.decelerationRate = 0.12f;
                viewport.gameObject.AddComponent<ScrollKeepAwake>().Init(scroll);
            }

            /// <summary>Reserves a slot of the given height and returns its top edge (distance from the content top).</summary>
            float Next(float height)
            {
                float top = cursor;
                cursor += height + Spacing;
                return top;
            }

            void Place(RectTransform rt, float top, float height)
            {
                placed.Add((rt, top + height * 0.5f));
            }

            public void Speed()
            {
                var holder = Ui.Rect("SpeedRow", content, new Vector2(860f, 180f), Vector2.zero);
                BuildSpeedRow(holder, -30f);
                Place(holder, Next(180f), 180f);
            }

            public void Header(string text)
            {
                var label = Ui.Label(content, text, 34, Palette.Gold, new Vector2(800f, 56f), Vector2.zero, TextAnchor.MiddleCenter, shadow: false);
                Place(label.rectTransform, Next(56f), 56f);
            }

            public void Toggle(string name, System.Func<bool> get, System.Action<bool> set)
            {
                Button button = null;
                button = Ui.Button(content, ToggleText(name, get()), get() ? Palette.Button : Palette.ButtonMuted, new Vector2(760f, ToggleHeight), Vector2.zero, () =>
                {
                    set(!get());
                    Ui.SetText(button, ToggleText(name, get()));
                    Ui.SetColor(button, get() ? Palette.Button : Palette.ButtonMuted);
                }, 40);
                Place((RectTransform)button.transform, Next(ToggleHeight), ToggleHeight);
            }

            /// <summary>A button that steps through several options each time it is tapped.</summary>
            public void Cycle(string name, string[] options, System.Func<int> get, System.Action<int> set)
            {
                Button button = null;
                string Caption() => name + ":  " + options[Mathf.Clamp(get(), 0, options.Length - 1)];
                Color Shade() => get() == 0 ? Palette.ButtonMuted : Palette.Button;
                button = Ui.Button(content, Caption(), Shade(), new Vector2(760f, ToggleHeight), Vector2.zero, () =>
                {
                    set((Mathf.Clamp(get(), 0, options.Length - 1) + 1) % options.Length);
                    Ui.SetText(button, Caption());
                    Ui.SetColor(button, Shade());
                }, 40);
                Place((RectTransform)button.transform, Next(ToggleHeight), ToggleHeight);
            }

            /// <summary>Sizes the content to fit the rows and positions them from the top.</summary>
            public void Finish()
            {
                float height = cursor + 20f;
                content.sizeDelta = new Vector2(content.sizeDelta.x, height);
                foreach (var (rt, centre) in placed) rt.anchoredPosition = new Vector2(0f, height * 0.5f - centre);
            }
        }

        static string ToggleText(string name, bool on) => name + ":  " + (on ? "ON" : "OFF");

        // ---------- Hooks for DemoCapture (desktop screenshots) ----------

        internal Match CurrentMatch => match;

        internal void DemoStartMatch(SeatType[] demoSeats)
        {
            for (int p = 0; p < GameState.Players; p++) seats[p] = demoSeats[p];
            StartMatch(null);
        }

        internal void DemoOpenSettings() => OpenSettings();

        internal void DemoCloseSettings() => CloseSettings();
    }

    /// <summary>Keeps rendering at full rate while a list is still scrolling after the finger lifts.</summary>
    public sealed class ScrollKeepAwake : MonoBehaviour
    {
        ScrollRect scroll;

        public void Init(ScrollRect scroll) => this.scroll = scroll;

        void Update()
        {
            if (scroll != null && scroll.velocity.sqrMagnitude > 4f) FrameGovernor.Keep(0.1f);
        }
    }
}
