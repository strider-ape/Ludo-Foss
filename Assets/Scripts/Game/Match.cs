using System;
using System.Collections;
using System.Collections.Generic;
using LudoGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LudoGame
{
    /// <summary>
    /// Runs one game on the 3D board: creates the pawns, die and player cards, then drives the turn loop
    /// with animations, sounds and vibration. Optional helpers: move preview, turn timer, play for places, auto-save.
    /// </summary>
    public sealed class Match : MonoBehaviour
    {
        public struct Placing
        {
            public PlayerColor Color;
            public string Name;
            public bool Human;
        }

        /// <summary>Raised when every place is decided (or the first winner ends the game).</summary>
        public event Action<List<Placing>> GameOver;

        static Material markerMaterial, discMaterial, dotMaterial, shieldMaterial, burstMaterial, crownMaterial;
        static Mesh quadMesh;

        GameState state;
        Bot bot;
        Board3D board;
        CameraRig rig;
        RectTransform canvas;
        Transform pieces;
        Transform previewRoot;
        IDie dice;
        RectTransform dieLayer;
        bool rolling;
        Text hint;
        Text toast;
        Image toastBg;
        Button skipButton;
        float toastUntil;
        bool awaitingRoll;
        bool rollRequested;
        bool picking;
        int previewDots;
        Pawn3D tapped;
        PlayerColor current;
        Coroutine loop;
        float timerDeadline = float.PositiveInfinity;
        int lastTick;

        readonly Pawn3D[,] pawns = new Pawn3D[GameState.Players, GameState.TokensPerPlayer];
        readonly PlayerCard[] cards = new PlayerCard[GameState.Players];
        readonly string[] names = new string[GameState.Players];
        readonly Vector3[] diceSpots = new Vector3[GameState.Players];
        readonly Vector2[] diceCanvasSpots = new Vector2[GameState.Players];
        readonly List<Transform> markers = new List<Transform>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly System.Random rng = new System.Random();

        /// <summary>Starts a new game, or continues <paramref name="restored"/> if one is given.</summary>
        public static Match Create(RectTransform canvas, Board3D board, CameraRig rig, SeatType[] seats, GameState restored, Action onSettings)
        {
            var root = Ui.Fill("Match", canvas);
            var match = root.gameObject.AddComponent<Match>();
            match.Build(canvas, board, rig, restored ?? new GameState(seats), onSettings);
            return match;
        }

        void Build(RectTransform canvas, Board3D board, CameraRig rig, GameState game, Action onSettings)
        {
            this.canvas = canvas;
            this.board = board;
            this.rig = rig;
            state = game;
            bot = new Bot(rng);
            var seats = state.Seats;

            // The board always has the same layout: Blue bottom-left, then clockwise Red, Green, Yellow.
            board.SetRotationSteps(0);
            board.SetShapesVisible(GameSettings.ColorShapes);
            PawnStyle.Apply(GameSettings.Pawns, GameSettings.ColorShapes);

            pieces = new GameObject("Pieces").transform;
            pieces.SetParent(board.Root, false);
            int humans = 0;
            foreach (var s in seats) if (s == SeatType.Human) humans++;
            int humanNumber = 0;
            for (int p = 0; p < GameState.Players; p++)
            {
                var c = (PlayerColor)p;
                bool active = seats[p] != SeatType.Off;
                names[p] = seats[p] == SeatType.Computer ? "Computer"
                    : seats[p] == SeatType.Off ? "-"
                    : humans == 1 ? "You" : "Player " + (++humanNumber);
                int seat = p;
                cards[p] = new PlayerCard(transform, c, names[p], active, () => OnCardTapped(seat));
                if (!active) continue;
                for (int t = 0; t < GameState.TokensPerPlayer; t++) pawns[p, t] = Pawn3D.Create(pieces, c, t);
            }
            for (int i = 0; i < state.Finished.Count; i++) cards[(int)state.Finished[i]].SetPlace(i + 1);
            dieLayer = Ui.Fill("Die", transform);
            CreateDie();

            var pause = Ui.Button(transform, "II", Palette.ButtonMuted, new Vector2(120, 110), Vector2.zero, onSettings, 54);
            var pauseRt = (RectTransform)pause.transform;
            pauseRt.anchorMin = pauseRt.anchorMax = new Vector2(1f, 1f);
            pauseRt.anchoredPosition = new Vector2(-95f, -95f);

            hint = Ui.Label(transform, "", 44, new Color(1f, 1f, 1f, 0.9f), new Vector2(1000, 80), Vector2.zero);
            skipButton = Ui.Button(transform, "SKIP TO RESULTS", Palette.Button, new Vector2(560, 110), Vector2.zero, OnSkip, 44);
            skipButton.gameObject.SetActive(false);
            badgeLayer = Ui.Fill("Badges", transform);
            toastBg = Ui.Image("Toast", transform, SpriteFactory.Rounded, new Color(0.05f, 0.07f, 0.16f, 0.9f), new Vector2(820, 150), Vector2.zero, sliced: true);
            toast = Ui.Label(toastBg.transform, "", 52, Palette.Text, new Vector2(780, 140), Vector2.zero);
            toastBg.gameObject.SetActive(false);

            LayoutUi();
            rig.Reframed += LayoutUi;
            LayoutTokens(0f);
            loop = StartCoroutine(Loop());
        }

        void OnDestroy()
        {
            if (rig != null)
            {
                rig.Reframed -= LayoutUi;
                rig.ResetZoom(0f);
            }
            if (pieces != null) Destroy(pieces.gameObject);
            if (previewRoot != null) Destroy(previewRoot.gameObject);
            if (dice != null) dice.Remove();
        }

        /// <summary>Places the player cards beside the board corners and works out where the die sits for each player.</summary>
        void LayoutUi()
        {
            float h = Board3D.HalfExtent;
            var corners = new[] { new Vector3(-h, 0f, h), new Vector3(h, 0f, h), new Vector3(h, 0f, -h), new Vector3(-h, 0f, -h) };
            const float gap = 26f;
            float lowest = 0f;
            for (int p = 0; p < GameState.Players; p++)
            {
                int corner = p; // Red top-left, Green top-right, Yellow bottom-right, Blue bottom-left
                bool left = corner == 0 || corner == 3, top = corner == 0 || corner == 1;
                var c = ToCanvas(rig.WorldToScreen(corners[corner]));
                var pos = new Vector2(
                    c.x + (left ? PlayerCard.Width : -PlayerCard.Width) * 0.5f,
                    c.y + (top ? 1f : -1f) * (gap + PlayerCard.Height * 0.5f));
                cards[p].Rt.anchoredPosition = pos;
                if (!top) lowest = Mathf.Min(lowest, pos.y - PlayerCard.Height * 0.5f);

                var diceCanvas = pos + new Vector2((left ? 1f : -1f) * (PlayerCard.Width * 0.5f + 105f), 0f);
                diceCanvasSpots[p] = diceCanvas;
                if (rig.ScreenToPlane(FromCanvas(diceCanvas), Board3D.TableY + Dice3D.Size * 0.5f, out var world))
                {
                    diceSpots[p] = new Vector3(world.x, Board3D.TableY, world.z);
                }
            }
            hint.rectTransform.anchoredPosition = new Vector2(0f, lowest - 70f);
            ((RectTransform)skipButton.transform).anchoredPosition = new Vector2(0f, lowest - 110f);
            toastBg.rectTransform.anchoredPosition = ToCanvas(rig.WorldToScreen(Vector3.zero));
            PositionBadges();
            dice.MoveTo(diceSpots[(int)state.Current], diceCanvasSpots[(int)state.Current], 0f);
        }

        Vector2 ToCanvas(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local);
            return local;
        }

        Vector2 FromCanvas(Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, canvas.TransformPoint(local));

        void Update()
        {
            bool pulsing = dice.IsRollable || picking;
            FrameGovernor.Pulsing = pulsing;
            if (toastBg.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastBg.gameObject.SetActive(false);
            // Stack badges follow the camera when it zooms or shakes.
            var cameraPosition = rig.Cam.transform.position;
            if (cameraPosition != lastCameraPosition)
            {
                lastCameraPosition = cameraPosition;
                PositionBadges();
            }
            UpdateTimer();
            AnimateMarkers();

            if (Time.timeScale == 0f || !Input.GetMouseButtonDown(0) || PointerOverUi()) return;
            Vector2 tap = Input.mousePosition;
            if (awaitingRoll)
            {
                if (dice.HitTest(tap, rig)) rollRequested = true;
                return;
            }
            if (!picking) return;
            var picked = PawnPicker.Pick(tap, pawns, rig);
            if (picked != null) tapped = picked;
        }

        static bool PointerOverUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return Input.touchCount > 0 ? es.IsPointerOverGameObject(Input.GetTouch(0).fingerId) : es.IsPointerOverGameObject();
        }

        void OnCardTapped(int seat)
        {
            if (awaitingRoll && (int)current == seat) rollRequested = true;
        }

        /// <summary>Creates the die the player chose (flat on-screen or 3D on the table), replacing any current one.</summary>
        void CreateDie()
        {
            bool wasRollable = dice != null && dice.IsRollable;
            if (dice != null) dice.Remove();
            dice = GameSettings.FlatDice
                ? Dice2D.Create(dieLayer, () => { if (awaitingRoll) rollRequested = true; })
                : (IDie)Dice3D.Create(null);
            int seat = (int)state.Current;
            dice.MoveTo(diceSpots[seat], diceCanvasSpots[seat], 0f);
            if (wasRollable) dice.SetRollable(true);
        }

        // ---------- Turn loop ----------

        static float Dur(float classicSeconds) => GameSettings.Scale(classicSeconds);

        static float StepDuration => GameSettings.Instant ? 0f : Mathf.Max(0.06f, Dur(0.25f));

        static float DiceDuration => GameSettings.Instant ? 0f : Mathf.Max(0.35f, Dur(1f));

        IEnumerator Loop()
        {
            yield return null;
            while (true)
            {
                if (GameSettings.ResumeGames) SaveGame.Save(state);
                var p = state.Current;
                current = p;
                FocusCard(p);
                UpdateSkipButton();
                if ((dice is Dice2D) != GameSettings.FlatDice) CreateDie();
                dice.MoveTo(diceSpots[(int)p], diceCanvasSpots[(int)p], Dur(0.35f));
                bool human = state.Seats[(int)p] == SeatType.Human;
                bool timedOut = false;
                if (human && GameSettings.TurnTimer > 0) StartTimer(GameSettings.TurnTimer);

                if (human)
                {
                    Sfx.Play("your_turn", 0.45f);
                    hint.text = "Tap the dice to roll";
                    rollRequested = false;
                    awaitingRoll = true;
                    dice.SetRollable(true);
                    while (!rollRequested)
                    {
                        if (Time.time >= timerDeadline)
                        {
                            timedOut = true;
                            break;
                        }
                        yield return null;
                    }
                    awaitingRoll = false;
                    dice.SetRollable(false);
                    hint.text = "";
                    if (timedOut && !GameSettings.TimerAutoPlays)
                    {
                        yield return SkipTurnForTime();
                        continue;
                    }
                }
                else
                {
                    yield return Wait(Dur(0.7f));
                }

                int roll = rng.Next(1, 7);
                rolling = true;
                yield return dice.Roll(roll, DiceDuration);
                rolling = false;
                Haptics.Tap(12, 70);
                if (roll == 6)
                {
                    Sfx.Play("six", 0.5f);
                    if (!GameSettings.Instant) StartCoroutine(dice.Celebrate());
                }

                state.ConsecutiveSixes = roll == 6 ? state.ConsecutiveSixes + 1 : 0;
                if (state.ConsecutiveSixes == 3 && GameSettings.ThreeSixesRule)
                {
                    StopTimer();
                    Toast("Three 6s in a row!\nTurn lost");
                    yield return Wait(Pause(1f));
                    state.PassTurn();
                    continue;
                }

                var moves = Rules.LegalMoves(state, p, roll);
                if (moves.Count == 0)
                {
                    // Nothing can move: no message, just a moment to see the number, then carry on.
                    StopTimer();
                    yield return Wait(Pause(0.5f));
                    if (roll != 6) state.PassTurn();
                    continue;
                }

                Move move;
                if (!human || timedOut)
                {
                    move = bot.Choose(state, moves, roll);
                }
                else if (GameSettings.AutoMove && Rules.DistinctChoices(moves) == 1)
                {
                    move = moves[0];
                }
                else
                {
                    hint.text = "Tap a glowing token";
                    tapped = null;
                    foreach (var m in moves) pawns[(int)p, m.Token].SetSelectable(true);
                    ShowPreview(moves);
                    if (GameSettings.ZoomToChoices) rig.ZoomToFit(ChoicePoints(moves), 0.35f);
                    picking = true;
                    while (tapped == null)
                    {
                        if (Time.time >= timerDeadline)
                        {
                            timedOut = true;
                            break;
                        }
                        yield return null;
                    }
                    picking = false;
                    ClearSelectable();
                    ClearPreview();
                    rig.ResetZoom(0.3f);
                    hint.text = "";
                    if (timedOut && !GameSettings.TimerAutoPlays)
                    {
                        yield return SkipTurnForTime();
                        continue;
                    }
                    if (timedOut)
                    {
                        move = bot.Choose(state, moves, roll);
                    }
                    else
                    {
                        int chosen = tapped.Index;
                        move = moves.Find(m => m.Token == chosen);
                    }
                }
                StopTimer();
                if (timedOut) Toast("Time's up!\nPlayed for you", 1f);

                yield return AnimateMove(move);
                var result = Rules.Apply(state, move);

                if (result.Captured.Count > 0)
                {
                    Sfx.Play("capture", 0.9f);
                    Haptics.Tap(45, 200);
                    rig.Shake(0.22f, GameSettings.Instant ? 0f : 0.3f);
                    float fly = GameSettings.Instant ? 0f : Mathf.Max(0.4f, Dur(1f));
                    foreach (var c in result.Captured)
                    {
                        var victim = pawns[(int)c.Color, c.Index];
                        victim.ApplyBaseScale(1f);
                        StartCoroutine(victim.FlyTo(board.BaseSlot(c.Color, c.Index), fly, 3.5f));
                    }
                    yield return Wait(fly);
                }
                if (result.ReachedHome)
                {
                    Sfx.Play("home", 0.8f);
                    Haptics.Tap(25, 120);
                    var spot = board.World(board.TokenLocal(p, move.Token, BoardLayout.HomeProgress)) + Vector3.up * 0.6f;
                    Fx.Confetti(spot, new[] { Palette.Of(p), Palette.LightOf(p), Palette.Gold, Color.white });
                }
                LayoutTokens(GameSettings.Instant ? 0f : 0.15f);

                if (result.Finished)
                {
                    int place = state.Finished.Count;
                    cards[(int)p].SetPlace(place);
                    if (!GameSettings.PlayForPlaces || state.IsOver)
                    {
                        yield return FinishGame();
                        yield break;
                    }
                    Sfx.Play("win", 0.6f);
                    Haptics.Tap(40, 180);
                    Toast(DisplayName(p).ToUpperInvariant() + " FINISHED " + Ordinal(place).ToUpperInvariant() + "!", 1.6f);
                    yield return Wait(Pause(1.2f));
                    state.PassTurn();
                    continue;
                }
                if (!Rules.GrantsExtraTurn(roll, result)) state.PassTurn();
            }
        }

        IEnumerator SkipTurnForTime()
        {
            StopTimer();
            Toast("Time's up!\nTurn skipped");
            yield return Wait(Pause(0.8f));
            state.PassTurn();
        }

        IEnumerator FinishGame()
        {
            SaveGame.Clear();
            StopTimer();
            hint.text = "";
            skipButton.gameObject.SetActive(false);
            var ranking = state.Ranking();
            for (int i = 0; i < ranking.Count; i++) cards[(int)ranking[i]].SetPlace(i + 1);
            Sfx.Play("win", 0.9f);
            Haptics.Tap(60, 255);
            Fx.Celebration(new[] { Palette.Of(PlayerColor.Red), Palette.Of(PlayerColor.Green), Palette.Of(PlayerColor.Yellow), Palette.Of(PlayerColor.Blue), Palette.Gold });
            yield return Wait(GameSettings.Instant ? 0.3f : 1.1f);
            var placings = new List<Placing>();
            foreach (var c in ranking)
            {
                placings.Add(new Placing { Color = c, Name = DisplayName(c), Human = state.Seats[(int)c] == SeatType.Human });
            }
            GameOver?.Invoke(placings);
        }

        /// <summary>"You" / "Player 2" for people, the colour name for computers.</summary>
        string DisplayName(PlayerColor c) => state.Seats[(int)c] == SeatType.Human ? names[(int)c] : Palette.Name(c);

        static string Ordinal(int n) => n == 1 ? "1st" : n == 2 ? "2nd" : n == 3 ? "3rd" : n + "th";

        // ---------- Skip to results ----------

        bool NoHumanLeft
        {
            get
            {
                for (int p = 0; p < GameState.Players; p++)
                {
                    if (state.Seats[p] == SeatType.Human && !state.HasFinished((PlayerColor)p)) return false;
                }
                return true;
            }
        }

        bool CanSkipToResults => loop != null && !state.IsOver && NoHumanLeft && state.ActiveUnfinished >= 2;

        void UpdateSkipButton() => skipButton.gameObject.SetActive(CanSkipToResults);

        void OnSkip()
        {
            if (!CanSkipToResults) return;
            StopAllCoroutines();
            loop = null;
            StopTimer();
            awaitingRoll = false;
            picking = false;
            dice.SetRollable(false);
            ClearSelectable();
            ClearPreview();
            rig.ResetZoom(0f);
            AutoPlay.PlayOut(state, bot, rng, GameSettings.ThreeSixesRule, GameSettings.PlayForPlaces);
            foreach (var pawn in pawns)
            {
                if (pawn != null) pawn.ResetPose();
            }
            LayoutTokens(0f);
            StartCoroutine(FinishGame());
        }

        // ---------- Turn timer ----------

        void StartTimer(int seconds)
        {
            timerDeadline = Time.time + seconds;
            lastTick = int.MaxValue;
        }

        void StopTimer()
        {
            timerDeadline = float.PositiveInfinity;
            foreach (var card in cards) card.HideTimer();
        }

        void UpdateTimer()
        {
            if (float.IsPositiveInfinity(timerDeadline)) return;
            float left = Mathf.Max(0f, timerDeadline - Time.time);
            cards[(int)current].SetTimer(left / GameSettings.TurnTimer);
            int whole = Mathf.CeilToInt(left);
            if (whole <= 3 && whole >= 1 && whole < lastTick)
            {
                lastTick = whole;
                Sfx.Play("click", 0.7f);
                Haptics.Tap(10, 60);
            }
        }

        // ---------- Move preview ----------

        static void EnsurePreviewMaterials()
        {
            if (quadMesh != null) return;
            quadMesh = MeshBuilder.FlatQuad();
            discMaterial = Materials.Unlit(SpriteFactory.Circle.texture, Color.white, true, 3);
            markerMaterial = Materials.Unlit(SpriteFactory.Ring.texture, Color.white, true, 4);
            dotMaterial = Materials.Unlit(SpriteFactory.Circle.texture, Color.white, true, 3);
            shieldMaterial = Materials.Unlit(SpriteFactory.Shield.texture, new Color(1f, 1f, 1f, 0.95f), true, 7);
            burstMaterial = Materials.Unlit(SpriteFactory.Burst.texture, new Color(1f, 0.32f, 0.2f, 1f), true, 7);
            crownMaterial = Materials.Unlit(SpriteFactory.Crown.texture, Palette.Gold, true, 7);
        }

        /// <summary>Marks where each choice would land (and optionally the route), with capture / safe / home hints.</summary>
        void ShowPreview(List<Move> moves)
        {
            ClearPreview();
            if (GameSettings.Preview == PreviewMode.Off) return;
            EnsurePreviewMaterials();
            previewRoot = new GameObject("Preview").transform;
            previewRoot.SetParent(board.Root, false);
            var path = new MeshBatch();
            previewDots = 0;
            var seen = new HashSet<int>();
            foreach (var m in moves)
            {
                if (!seen.Add(m.From)) continue;
                bool capture = Rules.CapturesAt(state, m.Player, m.To).Count > 0;
                bool home = m.To == BoardLayout.HomeProgress;
                bool safe = !capture && !home && Rules.IsSafeLanding(m.Player, m.To);
                var color = capture ? new Color(1f, 0.25f, 0.2f) : home ? Palette.Gold : Palette.Of(m.Player);
                var marker = new GameObject("Marker").transform;
                marker.SetParent(previewRoot, false);
                marker.localPosition = board.TokenLocal(m.Player, m.Token, m.To) + Vector3.up * 0.07f;
                AddQuad(marker, discMaterial, 1f, new Color(color.r, color.g, color.b, 0.55f), 0f, false);
                AddQuad(marker, markerMaterial, 1.2f, color, 0.005f, false);
                if (capture) AddQuad(marker, burstMaterial, 0.75f, null, 0.01f, true);
                else if (home) AddQuad(marker, crownMaterial, 0.75f, null, 0.01f, true);
                else if (safe) AddQuad(marker, shieldMaterial, 0.62f, null, 0.01f, true);
                markers.Add(marker);

                if (GameSettings.Preview != PreviewMode.MarkerAndPath) continue;
                var dot = Palette.DarkOf(m.Player);
                dot.a = 0.85f;
                for (int progress = m.From + 1; progress < m.To; progress++)
                {
                    path.Quad(board.TokenLocal(m.Player, m.Token, progress) + Vector3.up * 0.06f, 0.3f, 0.3f, dot);
                    previewDots++;
                }
            }
            if (previewDots > 0)
            {
                var go = new GameObject("Path");
                go.transform.SetParent(previewRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = path.Build("PreviewPath");
                go.AddComponent<MeshRenderer>().sharedMaterial = dotMaterial;
            }
            FrameGovernor.Keep(0.2f);
        }

        void AddQuad(Transform parent, Material material, float size, Color? color, float y, bool faceScreen)
        {
            var go = new GameObject("Quad");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(size, 1f, size);
            // Icons keep facing up the screen however the board is turned.
            if (faceScreen) go.transform.rotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (color.HasValue)
            {
                block.SetColor("_Color", color.Value);
                renderer.SetPropertyBlock(block);
            }
        }

        void AnimateMarkers()
        {
            if (markers.Count == 0) return;
            float s = 0.92f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            foreach (var marker in markers) marker.localScale = new Vector3(s, 1f, s);
        }

        void ClearPreview()
        {
            markers.Clear();
            previewDots = 0;
            if (previewRoot != null) Destroy(previewRoot.gameObject);
            previewRoot = null;
        }

        // ---------- Pieces ----------

        void ClearSelectable()
        {
            foreach (var pawn in pawns)
            {
                if (pawn != null) pawn.SetSelectable(false);
            }
        }

        void FocusCard(PlayerColor player)
        {
            for (int p = 0; p < GameState.Players; p++) cards[p].SetFocus(p == (int)player);
        }

        IEnumerator AnimateMove(Move m)
        {
            var pawn = pawns[(int)m.Player, m.Token];
            HideBadges();
            pawn.ApplyBaseScale(1f);
            if (m.LeavesBase)
            {
                Sfx.Play("token_out", 0.8f, 0.05f);
                yield return pawn.Hop(board.TokenLocal(m.Player, m.Token, 0), GameSettings.Instant ? 0f : Mathf.Max(0.18f, StepDuration * 2f), 1.6f);
                yield break;
            }
            for (int progress = m.From + 1; progress <= m.To; progress++)
            {
                yield return pawn.Hop(board.TokenLocal(m.Player, m.Token, progress), StepDuration, 0.65f);
                if (!GameSettings.Instant) Sfx.Play("step_" + UnityEngine.Random.Range(1, 4), 0.5f, 0.08f);
            }
        }

        /// <summary>Places every pawn, fanning out pawns that share a square and labelling the stack with its count.</summary>
        void LayoutTokens(float duration)
        {
            var groups = new Dictionary<Vector2Int, List<Pawn3D>>();
            foreach (var pawn in pawns)
            {
                if (pawn == null) continue;
                var pos = board.TokenLocal(pawn.Color, pawn.Index, state.Get(pawn.Color, pawn.Index));
                var key = new Vector2Int(Mathf.RoundToInt(pos.x * 100f), Mathf.RoundToInt(pos.z * 100f));
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<Pawn3D>();
                list.Add(pawn);
            }
            stacks.Clear();
            foreach (var pair in groups)
            {
                var list = pair.Value;
                var centre = new Vector3(pair.Key.x / 100f, 0f, pair.Key.y / 100f);
                if (list.Count == 1)
                {
                    StartCoroutine(list[0].SlideTo(centre, 1f, duration));
                    continue;
                }
                stacks.Add((centre, list.Count));
                for (int i = 0; i < list.Count; i++)
                {
                    float angle = Mathf.PI / 4f + i * Mathf.PI * 2f / list.Count;
                    var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.25f;
                    StartCoroutine(list[i].SlideTo(centre + offset, PawnStyle.StackScale, duration));
                }
            }
            ShowBadges();
        }

        // ---------- Stack badges ----------

        /// <summary>Squares holding more than one pawn (board-local centre, count).</summary>
        readonly List<(Vector3 centre, int count)> stacks = new List<(Vector3, int)>();
        readonly List<(RectTransform rt, Text label)> badges = new List<(RectTransform, Text)>();
        RectTransform badgeLayer;
        Vector3 lastCameraPosition;

        void ShowBadges()
        {
            while (badges.Count < stacks.Count)
            {
                var disc = Ui.Image("Badge", badgeLayer, SpriteFactory.Circle, Color.white, new Vector2(60f, 60f), Vector2.zero);
                Ui.Image("Fill", disc.transform, SpriteFactory.Circle, new Color(0.09f, 0.11f, 0.24f), new Vector2(50f, 50f), Vector2.zero);
                var label = Ui.Label(disc.transform, "", 36, Color.white, new Vector2(60f, 60f), new Vector2(0f, 1f), TextAnchor.MiddleCenter, shadow: false);
                badges.Add((disc.rectTransform, label));
            }
            for (int i = 0; i < badges.Count; i++)
            {
                bool used = i < stacks.Count;
                badges[i].rt.gameObject.SetActive(used);
                if (used) badges[i].label.text = stacks[i].count.ToString();
            }
            PositionBadges();
        }

        /// <summary>Keeps each badge just above and to the right of its stack.</summary>
        void PositionBadges()
        {
            for (int i = 0; i < stacks.Count && i < badges.Count; i++)
            {
                var anchor = board.World(stacks[i].centre) + Vector3.up * (1.25f * PawnStyle.Size * PawnStyle.StackScale) + rig.transform.right * 0.6f;
                badges[i].rt.anchoredPosition = ToCanvas(rig.WorldToScreen(anchor));
            }
        }

        void HideBadges()
        {
            foreach (var (rt, _) in badges) rt.gameObject.SetActive(false);
        }

        /// <summary>Everything the camera should frame while choosing: each choice's pawn and where it would land.</summary>
        List<Vector3> ChoicePoints(List<Move> moves)
        {
            var points = new List<Vector3>();
            float height = 1.25f * PawnStyle.Size;
            foreach (var m in moves)
            {
                var at = pawns[(int)m.Player, m.Token].transform.position;
                var to = board.World(board.TokenLocal(m.Player, m.Token, m.To));
                points.Add(at);
                points.Add(at + Vector3.up * height);
                points.Add(to);
                points.Add(to + Vector3.up * height);
            }
            return points;
        }

        /// <summary>Applies look and board-size settings changed mid-game.</summary>
        public void ApplyLook()
        {
            // Swap the die now unless it's mid-roll; otherwise the next turn picks it up.
            if (!rolling && (dice is Dice2D) != GameSettings.FlatDice) CreateDie();
            if (Board3D.EdgeToEdge != GameSettings.EdgeToEdge)
            {
                board.SetEdgeToEdge(GameSettings.EdgeToEdge);
                rig.Frame(); // refits the camera and re-places the cards and die (Reframed -> LayoutUi)
            }
            PawnStyle.Apply(GameSettings.Pawns, GameSettings.ColorShapes);
            board.SetShapesVisible(GameSettings.ColorShapes);
            foreach (var pawn in pawns)
            {
                if (pawn != null) pawn.RefreshStyle();
            }
            foreach (var card in cards) card.SetShapeVisible(GameSettings.ColorShapes);
            PositionBadges();
        }

        void Toast(string message, float seconds = 1.5f)
        {
            toast.text = message;
            toastBg.gameObject.SetActive(true);
            toastBg.transform.SetAsLastSibling();
            toastUntil = Time.unscaledTime + seconds;
            FrameGovernor.Keep(0.2f);
        }

        /// <summary>Message pauses stay readable even at high speed, but never block for long.</summary>
        static float Pause(float seconds) => Mathf.Max(GameSettings.Scale(seconds), 0.15f);

        static IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f) yield return null;
            else yield return new WaitForSeconds(seconds);
        }

        // ---------- Hooks for DemoCapture (desktop screenshots) ----------

        internal bool AwaitingRoll => awaitingRoll;
        internal bool Picking => picking;
        internal int PreviewDots => previewDots;
        internal bool CanSkip => CanSkipToResults;

        internal void DemoRoll()
        {
            if (awaitingRoll) rollRequested = true;
        }

        internal void DemoPick()
        {
            foreach (var pawn in pawns)
            {
                if (pawn == null || !pawn.IsSelectable) continue;
                tapped = pawn;
                return;
            }
        }

        internal void DemoSkip() => OnSkip();

        /// <summary>
        /// Taps each glowing pawn at its base, middle and head and checks that pawn is the one picked.
        /// Pawns sharing a square count as the same choice, since they make the same move.
        /// </summary>
        internal int DemoCheckTaps(System.Text.StringBuilder failures)
        {
            int checkedTaps = 0;
            foreach (var pawn in pawns)
            {
                if (pawn == null || !pawn.IsSelectable) continue;
                PawnPicker.ScreenSpan(pawn, rig, out var foot, out var head);
                foreach (var tap in new[] { foot, Vector2.Lerp(foot, head, 0.5f), head })
                {
                    checkedTaps++;
                    var picked = PawnPicker.Pick(tap, pawns, rig);
                    bool same = picked == pawn
                        || picked != null && state.Get(picked.Color, picked.Index) == state.Get(pawn.Color, pawn.Index);
                    if (!same)
                    {
                        failures.AppendLine($"tap on {pawn.Color} pawn {pawn.Index} (progress {state.Get(pawn.Color, pawn.Index)}) picked "
                            + (picked == null ? "nothing" : $"{picked.Color} pawn {picked.Index} (progress {state.Get(picked.Color, picked.Index)})"));
                    }
                }
            }
            return checkedTaps;
        }

        /// <summary>Avatar, name, colour, place and turn timer for one player, beside their corner of the board.</summary>
        sealed class PlayerCard
        {
            public const float Width = 330f;
            public const float Height = 140f;

            public readonly RectTransform Rt;

            readonly CanvasGroup group;
            readonly Image glow;
            readonly Image timer;
            readonly Image crown;
            readonly Image shape;
            readonly Text status;
            readonly bool active;

            public PlayerCard(Transform parent, PlayerColor c, string name, bool active, Action onTap)
            {
                this.active = active;
                Rt = Ui.Rect(c + "Card", parent, new Vector2(Width, Height), Vector2.zero);
                group = Rt.gameObject.AddComponent<CanvasGroup>();
                glow = Ui.Image("Glow", Rt, SpriteFactory.Rounded, Palette.Of(c), new Vector2(Width + 18f, Height + 18f), Vector2.zero, sliced: true);
                var bg = Ui.Image("Bg", Rt, SpriteFactory.Rounded, new Color(0.09f, 0.12f, 0.24f, 0.94f), new Vector2(Width, Height), Vector2.zero, sliced: true);
                bg.raycastTarget = true;
                var button = Rt.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = bg;
                button.onClick.AddListener(() => onTap());
                var avatarPos = new Vector2(-Width * 0.5f + 70f, 0f);
                var avatar = Ui.Avatar(Rt, active ? Palette.Of(c) : Palette.ButtonMuted, 104f, avatarPos);
                shape = Ui.ShapeBadge(avatar, c, 104f);
                SetShapeVisible(GameSettings.ColorShapes);
                timer = Ui.Image("Timer", Rt, SpriteFactory.Ring, Color.white, new Vector2(128f, 128f), avatarPos);
                timer.type = Image.Type.Filled;
                timer.fillMethod = Image.FillMethod.Radial360;
                timer.fillOrigin = (int)Image.Origin360.Top;
                timer.fillClockwise = false;
                timer.enabled = false;
                crown = Ui.Image("Crown", Rt, SpriteFactory.Crown, Palette.Gold, new Vector2(62f, 62f), avatarPos + new Vector2(0f, 66f));
                crown.enabled = false;
                Ui.Label(Rt, name, 42, Palette.Text, new Vector2(190f, 52f), new Vector2(64f, 18f), TextAnchor.MiddleLeft);
                status = Ui.Label(Rt, Palette.Name(c), 30, active ? Palette.LightOf(c) : Palette.ButtonMuted, new Vector2(190f, 40f), new Vector2(64f, -28f), TextAnchor.MiddleLeft, shadow: false);
                SetFocus(false);
            }

            public void SetShapeVisible(bool on) => shape.gameObject.SetActive(on && active);

            public void SetFocus(bool on)
            {
                glow.enabled = on && active;
                group.alpha = !active ? 0.35f : on ? 1f : 0.62f;
                Rt.localScale = Vector3.one * (on ? 1.06f : 1f);
                FrameGovernor.Keep(0.1f);
            }

            public void SetPlace(int place)
            {
                status.text = Ordinal(place).ToUpperInvariant() + " PLACE";
                status.color = place == 1 ? Palette.Gold : Color.white;
                crown.enabled = place == 1;
            }

            /// <summary>Shows the turn timer ring; it turns orange then red as time runs out.</summary>
            public void SetTimer(float fraction)
            {
                timer.enabled = true;
                timer.fillAmount = fraction;
                timer.color = fraction > 0.5f ? new Color(0.35f, 0.9f, 0.45f) : fraction > 0.25f ? new Color(1f, 0.7f, 0.2f) : new Color(1f, 0.3f, 0.25f);
            }

            public void HideTimer() => timer.enabled = false;
        }
    }
}
