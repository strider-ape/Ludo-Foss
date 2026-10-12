using System;
using System.Collections.Generic;
using LudoGame.Core;
using UnityEditor;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Checks board geometry and rules, then plays hundreds of bot-only games.
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod LudoGame.EditorTools.SelfTest.Run
    /// </summary>
    public static class SelfTest
    {
        static int failures;

        [MenuItem("Ludo/Run Self Test")]
        public static void Run()
        {
            failures = 0;
            CheckGeometry();
            CheckRules();
            Simulate(400);
            CheckPlayOut();
            CheckSaveRoundTrip();
            CheckTapTargets();
            Debug.Log(failures == 0 ? "[Ludo] SELF TEST PASSED" : $"[Ludo] SELF TEST FAILED ({failures} failures)");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void Check(bool condition, string message)
        {
            if (condition) return;
            failures++;
            Debug.LogError("[Ludo] FAIL: " + message);
        }

        static bool Adjacent(Cell a, Cell b) => Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1 && !(a.X == b.X && a.Y == b.Y);

        static void CheckGeometry()
        {
            var track = BoardLayout.Track;
            var unique = new HashSet<(int, int)>();
            foreach (var c in track) unique.Add((c.X, c.Y));
            Check(track.Length == 52 && unique.Count == 52, "track has 52 unique cells");
            for (int i = 0; i < track.Length; i++)
            {
                Check(Adjacent(track[i], track[(i + 1) % track.Length]), $"track {i}->{i + 1} is adjacent");
            }

            for (int p = 0; p < GameState.Players; p++)
            {
                var color = (PlayerColor)p;
                var start = track[BoardLayout.StartOf(color)];
                var origin = BoardLayout.BaseOrigin(color);
                bool besideBase = start.X >= origin.X - 1 && start.X <= origin.X + 6 && start.Y >= origin.Y - 1 && start.Y <= origin.Y + 6;
                Check(besideBase, $"{color} start square {start} touches its base");

                var last = track[BoardLayout.GlobalIndex(color, BoardLayout.LastTrackProgress)];
                Check(Adjacent(last, BoardLayout.HomeColumnCell(color, BoardLayout.FirstHomeColumnProgress)), $"{color} turns into its home column");
                for (int progress = BoardLayout.FirstHomeColumnProgress; progress < BoardLayout.HomeProgress; progress++)
                {
                    var cell = BoardLayout.HomeColumnCell(color, progress);
                    Check(!unique.Contains((cell.X, cell.Y)), $"{color} home column {cell} is off the track");
                }
                var end = BoardLayout.HomeColumnCell(color, BoardLayout.HomeProgress - 1);
                Check(end.X >= 5 && end.X <= 9 && end.Y >= 5 && end.Y <= 9, $"{color} home column ends next to the centre");
            }
        }

        static GameState TwoPlayer() => new GameState(new[] { SeatType.Human, SeatType.Human, SeatType.Off, SeatType.Off });

        static void CheckRules()
        {
            var s = TwoPlayer();
            Check(Rules.LegalMoves(s, PlayerColor.Red, 5).Count == 0, "tokens in base need a 6");
            Check(Rules.LegalMoves(s, PlayerColor.Red, 6).Count == 4, "a 6 lets any base token out");

            s.Set(PlayerColor.Red, 0, 54);
            var moves = Rules.LegalMoves(s, PlayerColor.Red, 3);
            Check(moves.TrueForAll(m => m.Token != 0), "overshooting home is not allowed");
            moves = Rules.LegalMoves(s, PlayerColor.Red, 2);
            Check(moves.Exists(m => m.Token == 0 && m.To == BoardLayout.HomeProgress), "an exact roll reaches home");

            // Capture on a normal square: Green sits on global square 5, Red lands there.
            s = TwoPlayer();
            s.Set(PlayerColor.Red, 0, 2);
            s.Set(PlayerColor.Green, 0, (5 - BoardLayout.StartOf(PlayerColor.Green) + 52) % 52);
            var result = Rules.Apply(s, new Move(PlayerColor.Red, 0, 2, 5));
            Check(result.Captured.Count == 1 && s.Get(PlayerColor.Green, 0) == BoardLayout.InBase, "landing on an opponent captures it");
            Check(Rules.GrantsExtraTurn(3, result), "a capture grants an extra turn");

            // No capture on a star square (global 8).
            s = TwoPlayer();
            s.Set(PlayerColor.Red, 0, 4);
            s.Set(PlayerColor.Green, 0, (8 - BoardLayout.StartOf(PlayerColor.Green) + 52) % 52);
            result = Rules.Apply(s, new Move(PlayerColor.Red, 0, 4, 8));
            Check(result.Captured.Count == 0, "star squares are safe");

            // Home column squares can't be captured.
            s = TwoPlayer();
            s.Set(PlayerColor.Red, 0, 52);
            Check(BoardLayout.GlobalIndex(PlayerColor.Red, 52) == -1, "home column is off the shared track");
        }

        static void Simulate(int games)
        {
            var rng = new System.Random(12345);
            var bot = new Bot(rng);
            long totalTurns = 0;
            int finished = 0;
            for (int g = 0; g < games; g++)
            {
                var seats = new SeatType[GameState.Players];
                int active = 0;
                while (active < 2)
                {
                    active = 0;
                    for (int p = 0; p < GameState.Players; p++)
                    {
                        seats[p] = rng.Next(4) == 0 ? SeatType.Off : SeatType.Computer;
                        if (seats[p] != SeatType.Off) active++;
                    }
                }

                // Play for all places, the way a match does by default.
                var s = new GameState(seats);
                int turns = 0;
                while (!s.IsOver && turns++ < 40000)
                {
                    var p = s.Current;
                    int roll = rng.Next(1, 7);
                    s.ConsecutiveSixes = roll == 6 ? s.ConsecutiveSixes + 1 : 0;
                    if (s.ConsecutiveSixes == 3)
                    {
                        s.PassTurn();
                        continue;
                    }
                    var moves = Rules.LegalMoves(s, p, roll);
                    if (moves.Count == 0)
                    {
                        if (roll != 6) s.PassTurn();
                        continue;
                    }
                    var move = bot.Choose(s, moves, roll);
                    Check(moves.Contains(move), "bot picks a legal move");
                    var result = Rules.Apply(s, move);
                    if (!Validate(s)) return;
                    if (result.Finished)
                    {
                        Check(s.Finished[s.Finished.Count - 1] == p, "finishers are recorded in order");
                        s.PassTurn();
                        Check(s.IsOver || !s.HasFinished(s.Current), "a finished player never gets another turn");
                        continue;
                    }
                    if (!Rules.GrantsExtraTurn(roll, result)) s.PassTurn();
                }
                Check(s.IsOver, $"game {g} decides every place");
                if (s.IsOver) finished++;
                Check(s.Ranking().Count == active && new HashSet<PlayerColor>(s.Ranking()).Count == active, $"game {g} ranks each player once");
                totalTurns += turns;
            }
            Debug.Log($"[Ludo] Simulated {games} games, {finished} decided, {totalTurns / Math.Max(1, games)} rolls on average");
        }

        /// <summary>"Skip to results" plays a game out from the middle and must rank everyone.</summary>
        static void CheckPlayOut()
        {
            var rng = new System.Random(777);
            var bot = new Bot(rng);
            for (int g = 0; g < 100; g++)
            {
                var s = new GameState(new[] { SeatType.Human, SeatType.Computer, SeatType.Computer, g % 2 == 0 ? SeatType.Computer : SeatType.Off });
                int active = g % 2 == 0 ? 4 : 3;
                AutoPlay.PlayOut(s, bot, rng, true, false, 60);
                var all = AutoPlay.PlayOut(s, bot, rng, true, true);
                Check(s.IsOver && all.Count == active && new HashSet<PlayerColor>(all).Count == active, $"play-out {g} ranks every player once");

                var quick = new GameState(new[] { SeatType.Computer, SeatType.Computer, SeatType.Computer, SeatType.Computer });
                var first = AutoPlay.PlayOut(quick, bot, rng, true, false);
                Check(quick.Finished.Count == 1 && first[0] == quick.Finished[0] && first.Count == 4, $"first-winner play-out {g} stops at one winner");
            }
        }

        /// <summary>
        /// Builds the real board, camera and pawns at phone resolution and taps each glowing pawn at its base, middle and
        /// head: the tapped pawn must be the one picked, in every board orientation. Covers a pawn standing exactly on
        /// another pawn's landing square (the rolled number equals the gap between them).
        /// </summary>
        static void CheckTapTargets()
        {
            var red = PlayerColor.Red;
            var scenarios = new (string name, (PlayerColor color, int token, int progress, bool selectable)[] pieces)[]
            {
                ("one roll apart", new[] { (red, 0, 10, true), (red, 1, 14, true) }),
                ("six apart", new[] { (red, 0, 20, true), (red, 1, 26, true) }),
                ("one roll apart at the home turn", new[] { (red, 0, 46, true), (red, 1, 50, true) }),
                ("one roll apart into the home column", new[] { (red, 0, 49, true), (red, 1, 53, true) }),
                ("neighbouring squares", new[] { (red, 0, 2, true), (red, 1, 3, true) }),
                ("diagonal corner", new[] { (red, 0, 4, true), (red, 1, 5, true), (red, 2, 6, true) }),
                ("base slots", new[] { (red, 0, -1, true), (red, 1, -1, true), (red, 2, -1, true), (red, 3, -1, true) }),
                ("opponent beside", new[] { (red, 0, 30, true), (PlayerColor.Green, 0, 18, false), (red, 1, 32, true) }),
            };

            var created = new List<UnityEngine.Object>();
            var target = new RenderTexture(1080, 2340, 16);
            created.Add(target);
            var rig = CameraRig.Create();
            created.Add(rig.gameObject);
            var table = GameObject.Find("Table");
            if (table != null) created.Add(table);
            rig.Cam.targetTexture = target;
            rig.Frame();
            var board = new Board3D(null);
            created.Add(board.Root.gameObject);
            int taps = 0;
            var savedStyle = (PawnStyle.Size, PawnStyle.Outline, PawnStyle.Coin, PawnStyle.StackScale, PawnStyle.Shapes);
            try
            {
                foreach (var look in new[] { PawnLook.Classic, PawnLook.Outline, PawnLook.OutlineCoin })
                for (int rotation = 0; rotation < 4; rotation++)
                {
                    PawnStyle.Apply(look, true);
                    board.SetRotationSteps(rotation);
                    foreach (var (name, pieces) in scenarios)
                    {
                        var pawns = new Pawn3D[GameState.Players, GameState.TokensPerPlayer];
                        foreach (var (color, token, progress, selectable) in pieces)
                        {
                            var pawn = Pawn3D.Create(board.Root, color, token);
                            pawn.transform.localPosition = board.TokenLocal(color, token, progress);
                            pawn.SetSelectable(selectable);
                            pawns[(int)color, token] = pawn;
                        }
                        foreach (var pawn in pawns)
                        {
                            if (pawn == null || !pawn.IsSelectable) continue;
                            PawnPicker.ScreenSpan(pawn, rig, out var foot, out var head);
                            foreach (var tap in new[] { foot, Vector2.Lerp(foot, head, 0.5f), head })
                            {
                                taps++;
                                var picked = PawnPicker.Pick(tap, pawns, rig);
                                Check(picked == pawn, $"tap on {pawn.Color} pawn {pawn.Index} ({name}, {look} pawns, board turned {rotation * 90} deg) picked "
                                    + (picked == null ? "nothing" : $"{picked.Color} pawn {picked.Index}"));
                            }
                        }
                        foreach (var pawn in pawns)
                        {
                            if (pawn != null) UnityEngine.Object.DestroyImmediate(pawn.gameObject);
                        }
                    }
                }
            }
            finally
            {
                (PawnStyle.Size, PawnStyle.Outline, PawnStyle.Coin, PawnStyle.StackScale, PawnStyle.Shapes) = savedStyle;
                foreach (var o in created) UnityEngine.Object.DestroyImmediate(o);
            }
            Debug.Log($"[Ludo] Tap check: {taps} simulated taps on glowing pawns");
        }

        /// <summary>A saved game must come back exactly as it was.</summary>
        static void CheckSaveRoundTrip()
        {
            var rng = new System.Random(4242);
            var bot = new Bot(rng);
            for (int g = 0; g < 50; g++)
            {
                var s = new GameState(new[] { SeatType.Human, SeatType.Computer, g % 3 == 0 ? SeatType.Off : SeatType.Human, SeatType.Computer });
                AutoPlay.PlayOut(s, bot, rng, true, true, rng.Next(20, 400));
                if (s.IsOver) continue;
                var copy = SaveGame.FromJson(SaveGame.ToJson(s));
                Check(copy != null, $"save {g} loads");
                if (copy == null) continue;
                bool same = copy.Current == s.Current && copy.ConsecutiveSixes == s.ConsecutiveSixes && copy.Finished.Count == s.Finished.Count;
                for (int p = 0; p < GameState.Players; p++)
                {
                    same &= copy.Seats[p] == s.Seats[p];
                    for (int t = 0; t < GameState.TokensPerPlayer; t++) same &= copy.Get((PlayerColor)p, t) == s.Get((PlayerColor)p, t);
                }
                for (int i = 0; i < s.Finished.Count; i++) same &= copy.Finished[i] == s.Finished[i];
                Check(same, $"save {g} round-trips exactly");
            }
            Check(SaveGame.FromJson("not json") == null, "a corrupt save is ignored");
            Check(SaveGame.FromJson("{\"version\":99}") == null, "a save from another version is ignored");
        }

        /// <summary>Progress stays in range, and no unsafe square holds two different colours.</summary>
        static bool Validate(GameState s)
        {
            var owner = new Dictionary<int, PlayerColor>();
            for (int p = 0; p < GameState.Players; p++)
            {
                var color = (PlayerColor)p;
                if (!s.IsActive(color)) continue;
                for (int t = 0; t < GameState.TokensPerPlayer; t++)
                {
                    int progress = s.Get(color, t);
                    if (progress < BoardLayout.InBase || progress > BoardLayout.HomeProgress)
                    {
                        Check(false, $"{color} token {t} has progress {progress}");
                        return false;
                    }
                    int g = BoardLayout.GlobalIndex(color, progress);
                    if (g < 0 || BoardLayout.IsSafe(g)) continue;
                    if (owner.TryGetValue(g, out var other) && other != color)
                    {
                        Check(false, $"{color} and {other} share unsafe square {g}");
                        return false;
                    }
                    owner[g] = color;
                }
            }
            return true;
        }
    }
}
