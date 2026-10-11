using System.Collections.Generic;

namespace LudoGame.Core
{
    public enum PlayerColor { Red = 0, Green = 1, Yellow = 2, Blue = 3 }

    /// <summary>A square on the 15x15 board. X is the column (left to right), Y is the row (top to bottom).</summary>
    public struct Cell
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X},{Y})";
    }

    /// <summary>
    /// Board geometry and token progress mapping.
    /// Progress: -1 = in base, 0..50 = main track (0 is the player's start square),
    /// 51..55 = home column, 56 = finished.
    /// </summary>
    public static class BoardLayout
    {
        public const int Size = 15;
        public const int TrackLength = 52;
        public const int InBase = -1;
        public const int LastTrackProgress = 50;
        public const int FirstHomeColumnProgress = 51;
        public const int HomeProgress = 56;
        public const int StarOffset = 8;

        public static readonly Cell[] Track = BuildTrack();

        static readonly int[] StartIndex = { 0, 13, 26, 39 };

        static readonly HashSet<int> SafeIndices = new HashSet<int> { 0, 8, 13, 21, 26, 34, 39, 47 };

        static readonly Cell[][] HomeColumns =
        {
            new[] { new Cell(1, 7), new Cell(2, 7), new Cell(3, 7), new Cell(4, 7), new Cell(5, 7) },
            new[] { new Cell(7, 1), new Cell(7, 2), new Cell(7, 3), new Cell(7, 4), new Cell(7, 5) },
            new[] { new Cell(13, 7), new Cell(12, 7), new Cell(11, 7), new Cell(10, 7), new Cell(9, 7) },
            new[] { new Cell(7, 13), new Cell(7, 12), new Cell(7, 11), new Cell(7, 10), new Cell(7, 9) },
        };

        /// <summary>Top-left cell of each player's 6x6 base.</summary>
        static readonly Cell[] BaseOrigins = { new Cell(0, 0), new Cell(9, 0), new Cell(9, 9), new Cell(0, 9) };

        public static int StartOf(PlayerColor c) => StartIndex[(int)c];

        public static Cell BaseOrigin(PlayerColor c) => BaseOrigins[(int)c];

        public static Cell HomeColumnCell(PlayerColor c, int progress) => HomeColumns[(int)c][progress - FirstHomeColumnProgress];

        /// <summary>Index into <see cref="Track"/> for a token on the main track, or -1 if it is not on the main track.</summary>
        public static int GlobalIndex(PlayerColor c, int progress)
        {
            if (progress < 0 || progress > LastTrackProgress)
            {
                return -1;
            }
            return (StartIndex[(int)c] + progress) % TrackLength;
        }

        public static bool IsSafe(int globalIndex) => SafeIndices.Contains(globalIndex);

        public static bool IsStartSquare(int globalIndex) => globalIndex % 13 == 0;

        public static bool IsStarSquare(int globalIndex) => globalIndex % 13 == StarOffset;

        /// <summary>Which player's start square this is, if any.</summary>
        public static bool TryGetStartOwner(int globalIndex, out PlayerColor owner)
        {
            owner = (PlayerColor)(globalIndex / 13);
            return IsStartSquare(globalIndex);
        }

        static Cell[] BuildTrack()
        {
            // Clockwise, starting on Red's start square just below Red's base.
            var t = new List<Cell>(TrackLength);
            for (int x = 1; x <= 5; x++) t.Add(new Cell(x, 6));
            for (int y = 5; y >= 0; y--) t.Add(new Cell(6, y));
            t.Add(new Cell(7, 0));
            for (int y = 0; y <= 5; y++) t.Add(new Cell(8, y));
            for (int x = 9; x <= 14; x++) t.Add(new Cell(x, 6));
            t.Add(new Cell(14, 7));
            for (int x = 14; x >= 9; x--) t.Add(new Cell(x, 8));
            for (int y = 9; y <= 14; y++) t.Add(new Cell(8, y));
            t.Add(new Cell(7, 14));
            for (int y = 14; y >= 9; y--) t.Add(new Cell(6, y));
            for (int x = 5; x >= 0; x--) t.Add(new Cell(x, 8));
            t.Add(new Cell(0, 7));
            t.Add(new Cell(0, 6));
            return t.ToArray();
        }
    }
}
