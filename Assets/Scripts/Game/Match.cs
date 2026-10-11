using System;
using System.Collections;
using System.Collections.Generic;
using LudoTurbo.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LudoTurbo
{
    /// <summary>Runs one game: builds the board, tokens, dice and player panels, then drives the turn loop.</summary>
    public sealed class Match : MonoBehaviour
    {
        const float BoardSize = 1000f;
        const float BoardY = -20f;

        public event Action<PlayerColor> Won;

        GameState state;
        BoardView board;
        DiceView dice;
        Bot bot;
        Text hint;
        Text toast;
        Image toastBg;
        float toastUntil;
        bool rollRequested;
        TokenView tapped;

        readonly TokenView[,] tokens = new TokenView[GameState.Players, GameState.TokensPerPlayer];
        readonly PlayerPanel[] panels = new PlayerPanel[GameState.Players];
        readonly System.Random rng = new System.Random();

        public static Match Create(Transform parent, SeatType[] seats, Action onSettings)
        {
            var root = Ui.Fill("Match", parent);
            var match = root.gameObject.AddComponent<Match>();
            match.Build(seats, onSettings);
            return match;
        }

        void Build(SeatType[] seats, Action onSettings)
        {
            state = new GameState(seats);
            bot = new Bot(rng);

            Ui.Label(transform, "LUDO TURBO", 64, Palette.Text, new Vector2(600, 100), new Vector2(-200, 880), TextAnchor.MiddleLeft);
            Ui.Button(transform, "SETTINGS", Palette.ButtonMuted, new Vector2(300, 100), new Vector2(360, 880), onSettings, 38);

            board = new BoardView(transform, BoardSize, new Vector2(0, BoardY));
            var tokenLayer = Ui.Rect("Tokens", board.Root, new Vector2(BoardSize, BoardSize), Vector2.zero);

            int humans = 0;
            foreach (var s in seats) if (s == SeatType.Human) humans++;

            var panelPos = new[] { new Vector2(-270, 610), new Vector2(270, 610), new Vector2(270, -650), new Vector2(-270, -650) };
            for (int p = 0; p < GameState.Players; p++)
            {
                var c = (PlayerColor)p;
                string who = seats[p] == SeatType.Off ? "-" : seats[p] == SeatType.Computer ? "Computer" : humans == 1 ? "You" : "Player";
                panels[p] = new PlayerPanel(transform, c, who, panelPos[p], diceOnRight: p == 0 || p == 3, active: seats[p] != SeatType.Off);
                if (seats[p] == SeatType.Off) continue;
                for (int t = 0; t < GameState.TokensPerPlayer; t++)
                {
                    tokens[p, t] = new TokenView(tokenLayer, c, t, board.CellSize * 0.82f, OnTokenTapped);
                }
            }

            dice = new DiceView(transform, 150f, () => rollRequested = true);
            hint = Ui.Label(transform, "", 40, new Color(1f, 1f, 1f, 0.8f), new Vector2(1000, 80), new Vector2(0, -860), TextAnchor.MiddleCenter, FontStyle.Normal);

            toastBg = Ui.Image("Toast", transform, SpriteFactory.Rounded, new Color(0f, 0f, 0f, 0.75f), new Vector2(760, 130), new Vector2(0, BoardY), sliced: true);
            toast = Ui.Label(toastBg.transform, "", 46, Palette.Text, new Vector2(720, 120), Vector2.zero);
            toastBg.gameObject.SetActive(false);

            LayoutTokens();
            StartCoroutine(Loop());
        }

        void Update()
        {
            float t = Time.unscaledTime;
            dice.Tick(t);
            foreach (var token in tokens)
            {
                if (token != null) token.Tick(t);
            }
            if (toastBg.gameObject.activeSelf && Time.unscaledTime > toastUntil) toastBg.gameObject.SetActive(false);
        }

        IEnumerator Loop()
        {
            yield return null;
            while (true)
            {
                var p = state.Current;
                FocusPanel(p);
                bool human = state.Seats[(int)p] == SeatType.Human;

                if (human)
                {
                    hint.text = "Tap the dice to roll";
                    rollRequested = false;
                    dice.SetRollable(true);
                    while (!rollRequested) yield return null;
                    dice.SetRollable(false);
                    hint.text = "";
                }
                else
                {
                    yield return Wait(GameSettings.Scale(0.6f));
                }

                int roll = rng.Next(1, 7);
                yield return dice.Roll(roll, GameSettings.Scale(0.8f));

                state.ConsecutiveSixes = roll == 6 ? state.ConsecutiveSixes + 1 : 0;
                if (state.ConsecutiveSixes == 3 && GameSettings.ThreeSixesRule)
                {
                    Toast("Three 6s in a row - turn lost!");
                    yield return Wait(Pause(0.9f));
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
                    foreach (var m in moves) tokens[(int)p, m.Token].SetSelectable(true);
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
                    float back = GameSettings.Scale(0.5f);
                    foreach (var c in result.Captured)
                    {
                        var view = tokens[(int)c.Color, c.Index];
                        view.Rt.localScale = Vector3.one;
                        StartCoroutine(Hop(view.Rt, board.BaseSlot(c.Color, c.Index), back));
                    }
                    yield return Wait(back);
                }
                LayoutTokens();

                if (result.Won)
                {
                    panels[(int)p].SetStatus("WINNER!");
                    hint.text = "";
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

        void OnTokenTapped(TokenView view)
        {
            if (view.IsSelectable) tapped = view;
        }

        void ClearSelectable()
        {
            foreach (var token in tokens)
            {
                if (token != null) token.SetSelectable(false);
            }
        }

        void FocusPanel(PlayerColor current)
        {
            for (int p = 0; p < GameState.Players; p++)
            {
                panels[p].SetFocus(p == (int)current);
            }
            dice.Rt.anchoredPosition = panels[(int)current].DiceSpot;
        }

        IEnumerator AnimateMove(Move m)
        {
            var view = tokens[(int)m.Player, m.Token];
            view.Rt.SetAsLastSibling();
            view.Rt.localScale = Vector3.one;
            if (m.LeavesBase)
            {
                yield return Hop(view.Rt, board.TokenPos(m.Player, m.Token, 0), GameSettings.Scale(0.3f));
                yield break;
            }
            float step = GameSettings.Scale(0.22f);
            for (int progress = m.From + 1; progress <= m.To; progress++)
            {
                yield return Hop(view.Rt, board.TokenPos(m.Player, m.Token, progress), step);
            }
        }

        static IEnumerator Hop(RectTransform rt, Vector2 to, float duration)
        {
            if (duration <= 0f)
            {
                rt.anchoredPosition = to;
                yield break;
            }
            var from = rt.anchoredPosition;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                rt.anchoredPosition = Vector2.Lerp(from, to, k * k * (3f - 2f * k));
                rt.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            rt.anchoredPosition = to;
            rt.localScale = Vector3.one;
        }

        /// <summary>Places every token, fanning out tokens that share a square.</summary>
        void LayoutTokens()
        {
            var groups = new Dictionary<Vector2Int, List<TokenView>>();
            foreach (var token in tokens)
            {
                if (token == null) continue;
                var pos = board.TokenPos(token.Color, token.Index, state.Get(token.Color, token.Index));
                var key = Vector2Int.RoundToInt(pos);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<TokenView>();
                list.Add(token);
            }
            foreach (var pair in groups)
            {
                var list = pair.Value;
                Vector2 centre = pair.Key;
                if (list.Count == 1)
                {
                    list[0].Rt.anchoredPosition = centre;
                    list[0].Rt.localScale = Vector3.one;
                    continue;
                }
                float radius = board.CellSize * 0.2f;
                for (int i = 0; i < list.Count; i++)
                {
                    float angle = Mathf.PI / 4f + i * Mathf.PI * 2f / list.Count;
                    list[i].Rt.anchoredPosition = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    list[i].Rt.localScale = Vector3.one * 0.68f;
                }
            }
        }

        void Toast(string message)
        {
            toast.text = message;
            toastBg.gameObject.SetActive(true);
            toastBg.transform.SetAsLastSibling();
            toastUntil = Time.unscaledTime + 1.4f;
        }

        /// <summary>Message pauses stay readable even at high speed, but never block for long.</summary>
        static float Pause(float seconds) => Mathf.Max(GameSettings.Scale(seconds), 0.15f);

        static IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f) yield return null;
            else yield return new WaitForSeconds(seconds);
        }

        sealed class PlayerPanel
        {
            public readonly Vector2 DiceSpot;

            readonly RectTransform rt;
            readonly CanvasGroup group;
            readonly Text status;
            readonly bool active;

            public PlayerPanel(Transform parent, PlayerColor c, string who, Vector2 pos, bool diceOnRight, bool active)
            {
                this.active = active;
                var bg = Ui.Image(c + "Panel", parent, SpriteFactory.Rounded, active ? Palette.Of(c) : Palette.ButtonMuted, new Vector2(500, 200), pos, sliced: true);
                rt = bg.rectTransform;
                group = bg.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                float labelX = diceOnRight ? -80f : 80f;
                Ui.Label(rt, Palette.Name(c), 56, Palette.Text, new Vector2(300, 70), new Vector2(labelX, 30));
                status = Ui.Label(rt, who, 36, new Color(1f, 1f, 1f, 0.9f), new Vector2(300, 60), new Vector2(labelX, -35), TextAnchor.MiddleCenter, FontStyle.Normal);
                DiceSpot = pos + new Vector2(diceOnRight ? 150f : -150f, 0f);
                SetFocus(false);
            }

            public void SetFocus(bool on)
            {
                group.alpha = !active ? 0.35f : on ? 1f : 0.55f;
                rt.localScale = Vector3.one * (on ? 1.04f : 1f);
            }

            public void SetStatus(string text) => status.text = text;
        }
    }
}
