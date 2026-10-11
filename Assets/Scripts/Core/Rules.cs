using System.Collections.Generic;

namespace LudoGame.Core
{
    public struct Move
    {
        public readonly PlayerColor Player;
        public readonly int Token;
        public readonly int From;
        public readonly int To;

        public Move(PlayerColor player, int token, int from, int to)
        {
            Player = player;
            Token = token;
            From = from;
            To = to;
        }

        public bool LeavesBase => From == BoardLayout.InBase;
    }

    public sealed class MoveResult
    {
        public readonly List<TokenRef> Captured = new List<TokenRef>();
        public bool ReachedHome;
        public bool Won;
    }

    /// <summary>Classic Ludo rules: a 6 to leave base, exact roll to finish, captures on unsafe squares.</summary>
    public static class Rules
    {
        public const int ExitRoll = 6;

        public static List<Move> LegalMoves(GameState s, PlayerColor p, int roll)
        {
            var moves = new List<Move>();
            for (int t = 0; t < GameState.TokensPerPlayer; t++)
            {
                int from = s.Get(p, t);
                if (from == BoardLayout.HomeProgress) continue;
                if (from == BoardLayout.InBase)
                {
                    if (roll == ExitRoll) moves.Add(new Move(p, t, from, 0));
                    continue;
                }
                int to = from + roll;
                if (to <= BoardLayout.HomeProgress) moves.Add(new Move(p, t, from, to));
            }
            return moves;
        }

        /// <summary>Opponent tokens that would be sent home if <paramref name="mover"/> landed on progress <paramref name="to"/>.</summary>
        public static List<TokenRef> CapturesAt(GameState s, PlayerColor mover, int to)
        {
            var captured = new List<TokenRef>();
            int g = BoardLayout.GlobalIndex(mover, to);
            if (g < 0 || BoardLayout.IsSafe(g)) return captured;
            for (int p = 0; p < GameState.Players; p++)
            {
                var other = (PlayerColor)p;
                if (other == mover || !s.IsActive(other)) continue;
                for (int t = 0; t < GameState.TokensPerPlayer; t++)
                {
                    if (BoardLayout.GlobalIndex(other, s.Get(other, t)) == g) captured.Add(new TokenRef(other, t));
                }
            }
            return captured;
        }

        public static MoveResult Apply(GameState s, Move m)
        {
            var result = new MoveResult();
            result.Captured.AddRange(CapturesAt(s, m.Player, m.To));
            foreach (var c in result.Captured)
            {
                s.Set(c.Color, c.Index, BoardLayout.InBase);
            }
            s.Set(m.Player, m.Token, m.To);
            result.ReachedHome = m.To == BoardLayout.HomeProgress;
            result.Won = result.ReachedHome && s.HasFinished(m.Player);
            return result;
        }

        public static bool GrantsExtraTurn(int roll, MoveResult r) => roll == 6 || r.Captured.Count > 0 || r.ReachedHome;

        /// <summary>Number of genuinely different choices (tokens stacked on the same square count once).</summary>
        public static int DistinctChoices(List<Move> moves)
        {
            var seen = new HashSet<int>();
            foreach (var m in moves) seen.Add(m.From);
            return seen.Count;
        }
    }
}
