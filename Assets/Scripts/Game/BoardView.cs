using LudoTurbo.Core;
using UnityEngine;

namespace LudoTurbo
{
    /// <summary>Draws the 15x15 board and converts token progress into screen positions.</summary>
    public sealed class BoardView
    {
        public readonly RectTransform Root;
        public readonly float Size;
        public readonly float CellSize;

        public BoardView(Transform parent, float size, Vector2 pos)
        {
            Size = size;
            CellSize = size / BoardLayout.Size;
            Root = Ui.Rect("Board", parent, new Vector2(size, size), pos);
            Build();
        }

        /// <summary>Centre of a cell, in board-local coordinates. Fractional cells are allowed.</summary>
        public Vector2 CellPos(float x, float y) =>
            new Vector2((x + 0.5f) * CellSize - Size / 2f, Size / 2f - (y + 0.5f) * CellSize);

        public Vector2 CellPos(Cell c) => CellPos(c.X, c.Y);

        public Vector2 TokenPos(PlayerColor color, int token, int progress)
        {
            if (progress == BoardLayout.InBase) return BaseSlot(color, token);
            if (progress <= BoardLayout.LastTrackProgress) return CellPos(BoardLayout.Track[BoardLayout.GlobalIndex(color, progress)]);
            if (progress < BoardLayout.HomeProgress) return CellPos(BoardLayout.HomeColumnCell(color, progress));
            return HomeSpot(color);
        }

        public Vector2 BaseSlot(PlayerColor color, int token)
        {
            var o = BoardLayout.BaseOrigin(color);
            float dx = token % 2 == 0 ? 1.75f : 3.25f;
            float dy = token < 2 ? 1.75f : 3.25f;
            return CellPos(o.X + dx, o.Y + dy);
        }

        Vector2 HomeSpot(PlayerColor color)
        {
            switch (color)
            {
                case PlayerColor.Red: return CellPos(6.35f, 7f);
                case PlayerColor.Green: return CellPos(7f, 6.35f);
                case PlayerColor.Yellow: return CellPos(7.65f, 7f);
                default: return CellPos(7f, 7.65f);
            }
        }

        void Build()
        {
            // Board frame and grid-line colour (cells are drawn slightly inset over it).
            Ui.Image("Frame", Root, SpriteFactory.Rounded, Color.white, new Vector2(Size + 24, Size + 24), Vector2.zero, sliced: true);
            Ui.Image("Lines", Root, SpriteFactory.Square, Palette.BoardLine, new Vector2(Size, Size), Vector2.zero);

            for (int p = 0; p < GameState.Players; p++)
            {
                DrawBase((PlayerColor)p);
            }

            float inset = CellSize - 3f;
            for (int i = 0; i < BoardLayout.TrackLength; i++)
            {
                var cell = BoardLayout.Track[i];
                var color = Palette.Cell;
                if (BoardLayout.TryGetStartOwner(i, out var owner)) color = Palette.Of(owner);
                var square = Ui.Image("Track" + i, Root, SpriteFactory.Square, color, new Vector2(inset, inset), CellPos(cell));
                if (BoardLayout.IsStarSquare(i))
                {
                    Ui.Image("Star", square.transform, SpriteFactory.Star, Palette.Star, new Vector2(CellSize * 0.7f, CellSize * 0.7f), Vector2.zero);
                }
                else if (BoardLayout.IsStartSquare(i))
                {
                    Ui.Image("Star", square.transform, SpriteFactory.Star, new Color(1f, 1f, 1f, 0.85f), new Vector2(CellSize * 0.7f, CellSize * 0.7f), Vector2.zero);
                }
            }

            for (int p = 0; p < GameState.Players; p++)
            {
                var c = (PlayerColor)p;
                for (int progress = BoardLayout.FirstHomeColumnProgress; progress < BoardLayout.HomeProgress; progress++)
                {
                    Ui.Image("Home" + c, Root, SpriteFactory.Square, Palette.Of(c), new Vector2(inset, inset), CellPos(BoardLayout.HomeColumnCell(c, progress)));
                }
            }

            Ui.Image("Center", Root, SpriteFactory.Center, Color.white, new Vector2(CellSize * 3f, CellSize * 3f), CellPos(7f, 7f));
        }

        void DrawBase(PlayerColor c)
        {
            var o = BoardLayout.BaseOrigin(c);
            var centre = CellPos(o.X + 2.5f, o.Y + 2.5f);
            Ui.Image("Base" + c, Root, SpriteFactory.Square, Palette.Of(c), new Vector2(CellSize * 6f - 3f, CellSize * 6f - 3f), centre);
            Ui.Image("BaseInner" + c, Root, SpriteFactory.Rounded, Color.white, new Vector2(CellSize * 4.1f, CellSize * 4.1f), centre, sliced: true);
            for (int t = 0; t < GameState.TokensPerPlayer; t++)
            {
                Ui.Image("Slot", Root, SpriteFactory.Circle, Palette.LightOf(c), new Vector2(CellSize * 1.05f, CellSize * 1.05f), BaseSlot(c, t));
            }
        }
    }
}
