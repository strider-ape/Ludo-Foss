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
    /// with animations, sounds and vibration.
    /// </summary>
    public sealed class Match : MonoBehaviour
    {
        public event Action<PlayerColor> Won;

        GameState state;
        Bot bot;
        Board3D board;
        CameraRig rig;
        RectTransform canvas;
        Transform pieces;
        Dice3D dice;
        Text hint;
        Text toast;
        Image toastBg;
        float toastUntil;
        int rotationSteps;
        bool awaitingRoll;
        bool rollRequested;
        Pawn3D tapped;
        PlayerColor current;

        readonly Pawn3D[,] pawns = new Pawn3D[GameState.Players, GameState.TokensPerPlayer];
        readonly PlayerCard[] cards = new PlayerCard[GameState.Players];
        readonly Vector3[] diceSpots = new Vector3[GameState.Players];
        readonly System.Random rng = new System.Random();

        public static Match Create(RectTransform canvas, Board3D board, CameraRig rig, SeatType[] seats, Action onSettings)
        {
            var root = Ui.Fill("Match", canvas);
            var match = root.gameObject.AddComponent<Match>();
            match.Build(canvas, board, rig, seats, onSettings);
            return match;
        }

        void Build(RectTransform canvas, Board3D board, CameraRig rig, SeatType[] seats, Action onSettings)
        {
            this.canvas = canvas;
            this.board = board;
            this.rig = rig;
            state = new GameState(seats);
            bot = new Bot(rng);

            // Turn the board so the first human player's base is bottom-left, like sitting at a real table.
            rotationSteps = 0;
            for (int p = 0; p < GameState.Players; p++)
            {
                if (seats[p] != SeatType.Human) continue;
                rotationSteps = (3 - p + 4) % 4;
                break;
            }
            board.SetRotationSteps(rotationSteps);

            pieces = new GameObject("Pieces").transform;
            pieces.SetParent(board.Root, false);
            int humans = 0;
            foreach (var s in seats) if (s == SeatType.Human) humans++;
            int humanNumber = 0;
            for (int p = 0; p < GameState.Players; p++)
            {
                var c = (PlayerColor)p;
                bool active = seats[p] != SeatType.Off;
                string name = seats[p] == SeatType.Computer ? "Computer"
                    : seats[p] == SeatType.Off ? "-"
                    : humans == 1 ? "You" : "Player " + (++humanNumber);
                int seat = p;
                cards[p] = new PlayerCard(transform, c, name, active, () => OnCardTapped(seat));
                if (!active) continue;
                for (int t = 0; t < GameState.TokensPerPlayer; t++) pawns[p, t] = Pawn3D.Create(pieces, c, t);
            }
            dice = Dice3D.Create(null);

            var pause = Ui.Button(transform, "II", Palette.ButtonMuted, new Vector2(120, 110), Vector2.zero, onSettings, 54);
            var pauseRt = (RectTransform)pause.transform;
            pauseRt.anchorMin = pauseRt.anchorMax = new Vector2(1f, 1f);
            pauseRt.anchoredPosition = new Vector2(-95f, -95f);

            hint = Ui.Label(transform, "", 44, new Color(1f, 1f, 1f, 0.9f), new Vector2(1000, 80), Vector2.zero);
            toastBg = Ui.Image("Toast", transform, SpriteFactory.Rounded, new Color(0.05f, 0.07f, 0.16f, 0.88f), new Vector2(780, 140), Vector2.zero, sliced: true);
            toast = Ui.Label(toastBg.transform, "", 52, Palette.Text, new Vector2(740, 130), Vector2.zero);
            toastBg.gameObject.SetActive(false);

            LayoutUi();
            rig.Reframed += LayoutUi;
            LayoutTokens(0f);
            StartCoroutine(Loop());
        }

        void OnDestroy()
        {
            if (rig != null) rig.Reframed -= LayoutUi;
            if (pieces != null) Destroy(pieces.gameObject);
            if (dice != null) Destroy(dice.gameObject);
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
                int corner = (p + rotationSteps) % 4;
                bool left = corner == 0 || corner == 3, top = corner == 0 || corner == 1;
                var c = ToCanvas(rig.WorldToScreen(corners[corner]));
                var pos = new Vector2(
                    c.x + (left ? PlayerCard.Width : -PlayerCard.Width) * 0.5f,
                    c.y + (top ? 1f : -1f) * (gap + PlayerCard.Height * 0.5f));
                cards[p].Rt.anchoredPosition = pos;
                if (!top) lowest = Mathf.Min(lowest, pos.y - PlayerCard.Height * 0.5f);

                var diceCanvas = pos + new Vector2((left ? 1f : -1f) * (PlayerCard.Width * 0.5f + 105f), 0f);
                if (rig.ScreenToPlane(FromCanvas(diceCanvas), Board3D.TableY + Dice3D.Size * 0.5f, out var world))
                {
                    diceSpots[p] = new Vector3(world.x, Board3D.TableY, world.z);
                }
            }
            hint.rectTransform.anchoredPosition = new Vector2(0f, lowest - 70f);
            toastBg.rectTransform.anchoredPosition = ToCanvas(rig.WorldToScreen(Vector3.zero));
            dice.MoveTo(diceSpots[(int)state.Current], 0f);
        }

        Vector2 ToCanvas(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local);
            return local;
        }

        Vector2 FromCanvas(Vector2 local) => RectTransformUtility.WorldToScreenPoint(null, canvas.TransformPoint(local));

        void Update()
        {
            bool pulsing = dice.IsRollable;
            foreach (var pawn in pawns)
            {
                if (pawn != null && pawn.IsSelectable) pulsing = true;
            }
            FrameGovernor.Pulsing = pulsing;
            if (toastBg.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastBg.gameObject.SetActive(false);

            if (Time.timeScale == 0f || !Input.GetMouseButtonDown(0) || PointerOverUi()) return;
            Vector2 tap = Input.mousePosition;
            if (awaitingRoll)
            {
                var top = dice.TopWorld;
                float radius = (rig.WorldToScreen(top) - rig.WorldToScreen(top + Vector3.right * Dice3D.Size * 0.5f)).magnitude;
                if ((tap - rig.WorldToScreen(top)).magnitude < radius * 2f) rollRequested = true;
                return;
            }
            Pawn3D best = null;
            float bestDistance = (rig.WorldToScreen(Vector3.zero) - rig.WorldToScreen(Vector3.right)).magnitude * 0.9f;
            foreach (var pawn in pawns)
            {
                if (pawn == null || !pawn.IsSelectable) continue;
                float d = (tap - rig.WorldToScreen(pawn.Body.position + Vector3.up * 0.4f)).magnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = pawn;
                }
            }
            if (best != null) tapped = best;
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

        // ---------- Turn loop ----------

        static float Dur(float classicSeconds) => GameSettings.Scale(classicSeconds);

        static float StepDuration => GameSettings.Instant ? 0f : Mathf.Max(0.06f, Dur(0.25f));

        static float DiceDuration => GameSettings.Instant ? 0f : Mathf.Max(0.35f, Dur(1f));

        IEnumerator Loop()
        {
            yield return null;
            while (true)
            {
                var p = state.Current;
                current = p;
                FocusCard(p);
                dice.MoveTo(diceSpots[(int)p], Dur(0.35f));
                bool human = state.Seats[(int)p] == SeatType.Human;

                if (human)
                {
                    Sfx.Play("your_turn", 0.45f);
                    hint.text = "Tap the dice to roll";
                    rollRequested = false;
                    awaitingRoll = true;
                    dice.SetRollable(true);
                    while (!rollRequested) yield return null;
                    awaitingRoll = false;
                    dice.SetRollable(false);
                    hint.text = "";
                }
                else
                {
                    yield return Wait(Dur(0.7f));
                }

                int roll = rng.Next(1, 7);
                yield return dice.Roll(roll, DiceDuration);
                Haptics.Tap(12, 70);
                if (roll == 6)
                {
                    Sfx.Play("six", 0.5f);
                    if (!GameSettings.Instant) StartCoroutine(dice.Celebrate());
                }

                state.ConsecutiveSixes = roll == 6 ? state.ConsecutiveSixes + 1 : 0;
                if (state.ConsecutiveSixes == 3 && GameSettings.ThreeSixesRule)
                {
                    Toast("Three 6s in a row!\nTurn lost");
                    yield return Wait(Pause(1f));
                    PassTurn();
                    continue;
                }

                var moves = Rules.LegalMoves(state, p, roll);
                if (moves.Count == 0)
                {
                    if (human) Toast(roll == 6 ? "No moves - roll again" : "No moves");
                    yield return Wait(Pause(0.6f));
                    if (roll != 6) PassTurn();
                    continue;
                }

                Move move;
                if (!human)
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
                    while (tapped == null) yield return null;
                    ClearSelectable();
                    hint.text = "";
                    int chosen = tapped.Index;
                    move = moves.Find(m => m.Token == chosen);
                }

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

                if (result.Won)
                {
                    cards[(int)p].SetStatus("WINNER!");
                    hint.text = "";
                    Sfx.Play("win", 0.9f);
                    Haptics.Tap(60, 255);
                    Fx.Celebration(new[] { Palette.Of(PlayerColor.Red), Palette.Of(PlayerColor.Green), Palette.Of(PlayerColor.Yellow), Palette.Of(PlayerColor.Blue), Palette.Gold });
                    yield return Wait(GameSettings.Instant ? 0.3f : 1.1f);
                    Won?.Invoke(p);
                    yield break;
                }
                if (!Rules.GrantsExtraTurn(roll, result)) PassTurn();
            }
        }

        void PassTurn()
        {
            state.ConsecutiveSixes = 0;
            state.Current = state.NextPlayer(state.Current);
        }

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

        /// <summary>Places every pawn, fanning out pawns that share a square.</summary>
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
            foreach (var pair in groups)
            {
                var list = pair.Value;
                var centre = new Vector3(pair.Key.x / 100f, 0f, pair.Key.y / 100f);
                if (list.Count == 1)
                {
                    StartCoroutine(list[0].SlideTo(centre, 1f, duration));
                    continue;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    float angle = Mathf.PI / 4f + i * Mathf.PI * 2f / list.Count;
                    var offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.22f;
                    StartCoroutine(list[i].SlideTo(centre + offset, 0.75f, duration));
                }
            }
        }

        void Toast(string message)
        {
            toast.text = message;
            toastBg.gameObject.SetActive(true);
            toastBg.transform.SetAsLastSibling();
            toastUntil = Time.unscaledTime + 1.5f;
            FrameGovernor.Keep(0.2f);
        }

        /// <summary>Message pauses stay readable even at high speed, but never block for long.</summary>
        static float Pause(float seconds) => Mathf.Max(GameSettings.Scale(seconds), 0.15f);

        static IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f) yield return null;
            else yield return new WaitForSeconds(seconds);
        }

        /// <summary>Avatar, name and colour for one player, beside their corner of the board.</summary>
        sealed class PlayerCard
        {
            public const float Width = 330f;
            public const float Height = 140f;

            public readonly RectTransform Rt;

            readonly CanvasGroup group;
            readonly Image glow;
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
                Ui.Avatar(Rt, active ? Palette.Of(c) : Palette.ButtonMuted, 104f, new Vector2(-Width * 0.5f + 70f, 0f));
                Ui.Label(Rt, name, 42, Palette.Text, new Vector2(190f, 52f), new Vector2(64f, 18f), TextAnchor.MiddleLeft);
                status = Ui.Label(Rt, Palette.Name(c), 30, active ? Palette.LightOf(c) : Palette.ButtonMuted, new Vector2(190f, 40f), new Vector2(64f, -28f), TextAnchor.MiddleLeft, shadow: false);
                SetFocus(false);
            }

            public void SetFocus(bool on)
            {
                glow.enabled = on && active;
                group.alpha = !active ? 0.35f : on ? 1f : 0.62f;
                Rt.localScale = Vector3.one * (on ? 1.06f : 1f);
                FrameGovernor.Keep(0.1f);
            }

            public void SetStatus(string text) => status.text = text;
        }
    }
}
