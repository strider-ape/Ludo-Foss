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

                var s = new GameState(seats);
                int turns = 0;
                bool won = false;
                while (!won && turns++ < 20000)
                {
                    var p = s.Current;
                    int roll = rng.Next(1, 7);
                    s.ConsecutiveSixes = roll == 6 ? s.ConsecutiveSixes + 1 : 0;
                    if (s.ConsecutiveSixes == 3)
                    {
                        s.ConsecutiveSixes = 0;
                        s.Current = s.NextPlayer(p);
                        continue;
                    }
                    var moves = Rules.LegalMoves(s, p, roll);
                    if (moves.Count == 0)
                    {
                        if (roll != 6)
                        {
                            s.ConsecutiveSixes = 0;
                            s.Current = s.NextPlayer(p);
                        }
                        continue;
                    }
                    var move = bot.Choose(s, moves, roll);
                    Check(moves.Contains(move), "bot picks a legal move");
                    var result = Rules.Apply(s, move);
                    if (!Validate(s)) return;
                    won = result.Won;
                    if (!won && !Rules.GrantsExtraTurn(roll, result))
                    {
                        s.ConsecutiveSixes = 0;
                        s.Current = s.NextPlayer(p);
                    }
                }
                Check(won, $"game {g} finishes");
                if (won) finished++;
                totalTurns += turns;
            }
            Debug.Log($"[Ludo] Simulated {games} games, {finished} finished, {totalTurns / Math.Max(1, games)} rolls on average");
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
