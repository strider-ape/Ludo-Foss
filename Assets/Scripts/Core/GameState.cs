using System;
using System.Collections.Generic;

namespace LudoGame.Core
{
    public enum SeatType { Off = 0, Human = 1, Computer = 2 }

    public struct TokenRef
    {
        public readonly PlayerColor Color;
        public readonly int Index;

        public TokenRef(PlayerColor color, int index)
        {
            Color = color;
            Index = index;
        }
    }

    public sealed class GameState
    {
        public const int Players = 4;
        public const int TokensPerPlayer = 4;

        public readonly SeatType[] Seats = new SeatType[Players];
        readonly int[,] progress = new int[Players, TokensPerPlayer];

        public PlayerColor Current;
        public int ConsecutiveSixes;

        public GameState(IList<SeatType> seats)
        {
            int active = 0;
            for (int p = 0; p < Players; p++)
            {
                Seats[p] = seats[p];
                if (seats[p] != SeatType.Off) active++;
                for (int t = 0; t < TokensPerPlayer; t++)
                {
                    progress[p, t] = BoardLayout.InBase;
                }
            }
            if (active < 2)
            {
                throw new ArgumentException("At least two seats must be playing.");
            }
            Current = PlayerColor.Red;
            if (!IsActive(Current))
            {
                Current = NextPlayer(Current);
            }
        }

        GameState(GameState other)
        {
            Array.Copy(other.Seats, Seats, Players);
            Array.Copy(other.progress, progress, other.progress.Length);
            Current = other.Current;
            ConsecutiveSixes = other.ConsecutiveSixes;
        }

        public GameState Clone() => new GameState(this);

        public bool IsActive(PlayerColor c) => Seats[(int)c] != SeatType.Off;

        public int Get(PlayerColor c, int token) => progress[(int)c, token];

        public void Set(PlayerColor c, int token, int value) => progress[(int)c, token] = value;

        public bool HasFinished(PlayerColor c)
        {
            for (int t = 0; t < TokensPerPlayer; t++)
            {
                if (progress[(int)c, t] != BoardLayout.HomeProgress) return false;
            }
            return true;
        }

        /// <summary>Next seat clockwise that is playing and has not finished.</summary>
        public PlayerColor NextPlayer(PlayerColor from)
        {
            for (int i = 1; i <= Players; i++)
            {
                var c = (PlayerColor)(((int)from + i) % Players);
                if (IsActive(c) && !HasFinished(c)) return c;
            }
            return from;
        }
    }
}
