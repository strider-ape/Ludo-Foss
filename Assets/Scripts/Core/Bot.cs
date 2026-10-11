using System;
using System.Collections.Generic;

namespace LudoTurbo.Core
{
    /// <summary>Computer player: scores each legal move with simple heuristics and picks the best.</summary>
    public sealed class Bot
    {
        readonly Random rng;

        public Bot(Random rng)
        {
            this.rng = rng;
        }

        public Move Choose(GameState s, List<Move> moves, int roll)
        {
            Move best = moves[0];
            double bestScore = double.MinValue;
            foreach (var m in moves)
            {
                double score = Score(s, m) + rng.NextDouble() * 2.0;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = m;
                }
            }
            return best;
        }

        double Score(GameState s, Move m)
        {
            double score = 0;
            var captures = Rules.CapturesAt(s, m.Player, m.To);
            foreach (var c in captures)
            {
                score += 60 + s.Get(c.Color, c.Index);
            }
            if (m.To == BoardLayout.HomeProgress) score += 70;
            if (m.LeavesBase) score += 45;
            if (m.To >= BoardLayout.FirstHomeColumnProgress && m.From <= BoardLayout.LastTrackProgress) score += 35;

            int landing = BoardLayout.GlobalIndex(m.Player, m.To);
            if (landing >= 0)
            {
                if (BoardLayout.IsSafe(landing)) score += 20;
                else score -= 22 * Threats(s, m.Player, landing);
            }

            int leaving = BoardLayout.GlobalIndex(m.Player, m.From);
            if (leaving >= 0 && !BoardLayout.IsSafe(leaving))
            {
                score += 15 * Threats(s, m.Player, leaving);
            }

            score += m.To * 0.4;
            return score;
        }

        /// <summary>Opponent tokens on the main track 1-6 squares behind a square (they could land on it next turn).</summary>
        static int Threats(GameState s, PlayerColor me, int globalIndex)
        {
            int threats = 0;
            for (int p = 0; p < GameState.Players; p++)
            {
                var other = (PlayerColor)p;
                if (other == me || !s.IsActive(other)) continue;
                for (int t = 0; t < GameState.TokensPerPlayer; t++)
                {
                    int progress = s.Get(other, t);
                    int g = BoardLayout.GlobalIndex(other, progress);
                    if (g < 0) continue;
                    int distance = (globalIndex - g + BoardLayout.TrackLength) % BoardLayout.TrackLength;
                    // The opponent must still be on the main track when it reaches this square.
                    if (distance >= 1 && distance <= 6 && progress + distance <= BoardLayout.LastTrackProgress) threats++;
                }
            }
            return threats;
        }
    }
}
