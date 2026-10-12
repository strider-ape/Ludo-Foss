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
        /// <summary>Width of the wooden frame: classic, or a thin rim for the edge-to-edge board.</summary>
        const float FrameSize = 16.4f;
        const float ThinFrameSize = 15.4f;
        public const float TableY = -0.6f;

        const float LinesY = 0.01f, CellsY = 0.02f, DecalY = 0.03f, SlotY = 0.04f;

        /// <summary>True when the board uses its thin rim and fills the screen width.</summary>
        public static bool EdgeToEdge { get; private set; }

        /// <summary>Half the board's outer width, including the frame.</summary>
        public static float HalfExtent => (EdgeToEdge ? ThinFrameSize : FrameSize) * 0.5f;

        public readonly Transform Root;

        GameObject frame;
        GameObject thinFrame;
        Transform shapes;
        readonly System.Collections.Generic.List<Transform> shapeIcons = new System.Collections.Generic.List<Transform>();

        public Board3D(Transform parent)
        {
            Root = new GameObject("Board").transform;
            Root.SetParent(parent, false);
            Build();
            BuildShapes();
        }

        public void SetRotationSteps(int clockwiseSteps)
        {
            Root.localRotation = Quaternion.Euler(0f, 90f * clockwiseSteps, 0f);
            // Shape icons stay upright on screen however the board is turned.
            foreach (var icon in shapeIcons) icon.rotation = Quaternion.identity;
        }

        /// <summary>Shows or hides the colour-blind shapes on the bases and home triangles.</summary>
        public void SetShapesVisible(bool visible) => shapes.gameObject.SetActive(visible);

        /// <summary>Swaps between the classic wooden frame and the thin edge-to-edge rim. Refit the camera afterwards.</summary>
        public void SetEdgeToEdge(bool on)
        {
            EdgeToEdge = on;
            frame.SetActive(!on);
            thinFrame.SetActive(on);
        }

        void BuildShapes()
        {
            shapes = new GameObject("Shapes").transform;
            shapes.SetParent(Root, false);
            var quad = MeshBuilder.FlatQuad();
            var triangleCentres = new[] { Cell(6f, 7f), Cell(7f, 6f), Cell(8f, 7f), Cell(7f, 8f) }; // red, green, yellow, blue
            for (int p = 0; p < GameState.Players; p++)
            {
                var c = (PlayerColor)p;
                var texture = SpriteFactory.ShapeOf(c).texture;
                var o = BoardLayout.BaseOrigin(c);
                // In the colour on the white middle of the base, and in white on the colour's home triangle.
                AddShape(quad, Materials.Unlit(texture, Palette.Of(c), true, 3), Cell(o.X + 2.5f, o.Y + 2.5f) + Vector3.up * SlotY, 0.8f);
                AddShape(quad, Materials.Unlit(texture, new Color(1f, 1f, 1f, 0.95f), true, 3), triangleCentres[p] + Vector3.up * DecalY, 0.62f);
            }
            shapes.gameObject.SetActive(false);
        }

        void AddShape(Mesh quad, Material material, Vector3 localPos, float size)
        {
            var go = new GameObject("Shape");
            go.transform.SetParent(shapes, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size, 1f, size);
            go.transform.rotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            shapeIcons.Add(go.transform);
        }

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
            var wood = Materials.Glossy(Palette.Wood, null, 20f, 0.2f, 0.12f);
            frame = AddMesh("Frame", MeshBuilder.RoundedBox(new Vector3(FrameSize, 0.6f, FrameSize), 0.22f, 4), wood, new Vector3(0f, -0.3f, 0f));
            thinFrame = AddMesh("ThinFrame", MeshBuilder.RoundedBox(new Vector3(ThinFrameSize, 0.6f, ThinFrameSize), 0.12f, 4), wood, new Vector3(0f, -0.3f, 0f));
            thinFrame.SetActive(EdgeToEdge);
            frame.SetActive(!EdgeToEdge);

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

        GameObject AddMesh(string name, Mesh mesh, Material material, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
