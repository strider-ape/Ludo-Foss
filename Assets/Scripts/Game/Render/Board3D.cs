using LudoGame.Core;
using UnityEngine;

namespace LudoGame
{
    /// <summary>
    /// The 3D board: a rounded wooden frame with the playing surface on top. One board cell is one world unit,
    /// the surface sits at y = 0, and the root can be turned in 90-degree steps so the local player's base is bottom-left.
    /// </summary>
    public sealed class Board3D
    {
        public const float FrameSize = 16.4f;
        public const float HalfExtent = FrameSize * 0.5f;
        public const float TableY = -0.6f;

        const float LinesY = 0.01f, CellsY = 0.02f, DecalY = 0.03f, SlotY = 0.04f;

        public readonly Transform Root;

        public Board3D(Transform parent)
        {
            Root = new GameObject("Board").transform;
            Root.SetParent(parent, false);
            Build();
        }

        public void SetRotationSteps(int clockwiseSteps) => Root.localRotation = Quaternion.Euler(0f, 90f * clockwiseSteps, 0f);

        /// <summary>Board-local position of a cell centre. Fractional cells are allowed.</summary>
        public static Vector3 Cell(float x, float y) => new Vector3(x - 7f, 0f, 7f - y);

        public static Vector3 Cell(Cell c) => Cell(c.X, c.Y);

        public Vector3 World(Vector3 local) => Root.TransformPoint(local);

        public Vector3 TokenLocal(PlayerColor color, int token, int progress)
        {
            if (progress == BoardLayout.InBase) return BaseSlot(color, token);
            if (progress <= BoardLayout.LastTrackProgress) return Cell(BoardLayout.Track[BoardLayout.GlobalIndex(color, progress)]);
            if (progress < BoardLayout.HomeProgress) return Cell(BoardLayout.HomeColumnCell(color, progress));
            return HomeSpot(color);
        }

        public Vector3 BaseSlot(PlayerColor color, int token)
        {
            var o = BoardLayout.BaseOrigin(color);
            float dx = token % 2 == 0 ? 1.75f : 3.25f;
            float dy = token < 2 ? 1.75f : 3.25f;
            return Cell(o.X + dx, o.Y + dy);
        }

        static Vector3 HomeSpot(PlayerColor color)
        {
            switch (color)
            {
                case PlayerColor.Red: return Cell(6.35f, 7f);
                case PlayerColor.Green: return Cell(7f, 6.35f);
                case PlayerColor.Yellow: return Cell(7.65f, 7f);
                default: return Cell(7f, 7.65f);
            }
        }

        void Build()
        {
            var frame = MeshBuilder.RoundedBox(new Vector3(FrameSize, 0.6f, FrameSize), 0.22f, 4);
            AddMesh("Frame", frame, Materials.Glossy(Palette.Wood, null, 20f, 0.2f, 0.12f), new Vector3(0f, -0.3f, 0f));

            var cells = new MeshBatch();
            cells.Quad(new Vector3(0f, LinesY, 0f), 15.1f, 15.1f, Palette.BoardLine);
            for (int p = 0; p < GameState.Players; p++)
            {
                var o = BoardLayout.BaseOrigin((PlayerColor)p);
                cells.Quad(Cell(o.X + 2.5f, o.Y + 2.5f) + Vector3.up * CellsY, 5.94f, 5.94f, Palette.Of((PlayerColor)p));
            }
            const float inset = 0.93f;
            for (int i = 0; i < BoardLayout.TrackLength; i++)
            {
                var color = BoardLayout.TryGetStartOwner(i, out var owner) ? Palette.Of(owner) : Palette.Cell;
                cells.Quad(Cell(BoardLayout.Track[i]) + Vector3.up * CellsY, inset, inset, color);
            }
            for (int p = 0; p < GameState.Players; p++)
            {
                var c = (PlayerColor)p;
                for (int progress = BoardLayout.FirstHomeColumnProgress; progress < BoardLayout.HomeProgress; progress++)
                {
                    cells.Quad(Cell(BoardLayout.HomeColumnCell(c, progress)) + Vector3.up * CellsY, inset, inset, Palette.Of(c));
                }
            }
            // The centre: each colour's triangle points at its home column.
            var tl = new Vector3(-1.5f, CellsY, 1.5f);
            var tr = new Vector3(1.5f, CellsY, 1.5f);
            var br = new Vector3(1.5f, CellsY, -1.5f);
            var bl = new Vector3(-1.5f, CellsY, -1.5f);
            var mid = new Vector3(0f, CellsY, 0f);
            cells.Triangle(tl, mid, bl, Palette.Of(PlayerColor.Red));
            cells.Triangle(tl, tr, mid, Palette.Of(PlayerColor.Green));
            cells.Triangle(tr, br, mid, Palette.Of(PlayerColor.Yellow));
            cells.Triangle(bl, mid, br, Palette.Of(PlayerColor.Blue));
            AddMesh("Cells", cells.Build("Cells"), Materials.Unlit(Texture2D.whiteTexture, Color.white, false), Vector3.zero);

            var inner = new MeshBatch();
            var slots = new MeshBatch();
            for (int p = 0; p < GameState.Players; p++)
            {
                var c = (PlayerColor)p;
                var o = BoardLayout.BaseOrigin(c);
                inner.Quad(Cell(o.X + 2.5f, o.Y + 2.5f) + Vector3.up * DecalY, 4.1f, 4.1f, Color.white);
                for (int t = 0; t < GameState.TokensPerPlayer; t++)
                {
                    slots.Quad(BaseSlot(c, t) + Vector3.up * SlotY, 1.05f, 1.05f, Palette.LightOf(c));
                }
            }
            AddMesh("BaseInner", inner.Build("BaseInner"), Materials.Unlit(SpriteFactory.Rounded.texture, Color.white, true, 1), Vector3.zero);
            AddMesh("Slots", slots.Build("Slots"), Materials.Unlit(SpriteFactory.Circle.texture, Color.white, true, 2), Vector3.zero);

            var stars = new MeshBatch();
            for (int i = 0; i < BoardLayout.TrackLength; i++)
            {
                if (BoardLayout.IsStarSquare(i)) stars.Quad(Cell(BoardLayout.Track[i]) + Vector3.up * DecalY, 0.72f, 0.72f, Palette.Star);
                else if (BoardLayout.IsStartSquare(i)) stars.Quad(Cell(BoardLayout.Track[i]) + Vector3.up * DecalY, 0.72f, 0.72f, new Color(1f, 1f, 1f, 0.85f));
            }
            AddMesh("Stars", stars.Build("Stars"), Materials.Unlit(SpriteFactory.Star.texture, Color.white, true, 1), Vector3.zero);
        }

        void AddMesh(string name, Mesh mesh, Material material, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
