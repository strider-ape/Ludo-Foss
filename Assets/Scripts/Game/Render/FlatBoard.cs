using System.Collections.Generic;
using LudoGame.Core;
using UnityEngine;

namespace LudoGame
{
    /// <summary>
    /// The 2D board: a flat, top-down Ludo board in the classic printed style. White track squares with thin grey
    /// lines, solid colour bases with a white yard and four slot discs, outlined stars, an arrow into each home column
    /// and map-pin pawns. Like the 3D board, everything is drawn in code.
    /// </summary>
    public static class FlatBoard
    {
        static readonly Color[] CellColors = { Hex(0xEC1C22), Hex(0x00A048), Hex(0xFEDF16), Hex(0x23AFFD) };
        static readonly Color[] BaseColors = { Hex(0xEB1B21), Hex(0x00A04A), Hex(0xC7AD0B), Hex(0x23AFFD) };
        static readonly Color GridLine = new Color(0f, 0f, 0f, 0.3f);
        static readonly Color StarEdge = Hex(0x8A8A8A);
        static readonly Color PinLight = Hex(0xF7F8FA);
        static readonly Color PinDark = Hex(0xC2C5CC);
        static readonly Color PinEdge = Hex(0x8C8F96);

        /// <summary>The dark ring each pawn stands on; see-through, so on a base slot it reads as a darker shade of the slot.</summary>
        public static readonly Color FootRing = new Color(0.29f, 0.06f, 0.05f, 0.75f);

        public const float HalfExtent = 7.5f;

        const float LineWidth = 0.045f;
        const float CornerRadius = 0.35f;
        const float GroundY = 0f, TopY = 0.005f, LinesY = 0.01f, DecalY = 0.02f;

        // Map pin, in board squares with the tip at the origin and the head pointing up the screen.
        public const float PinHeadY = 0.92f, PinRadius = 0.5f, BallRadius = 0.36f;
        public const float PinLeft = -0.6f, PinBottom = -0.1f, PinWidth = 1.2f, PinHeight = 1.6f;

        static Texture2D roundedCorner, star, arrow;
        static readonly Texture2D[] pins = new Texture2D[GameState.Players];

        public static Color CellColor(PlayerColor c) => CellColors[(int)c];

        public static Color BaseColor(PlayerColor c) => BaseColors[(int)c];

        /// <summary>Board-local position from edge coordinates: (0, 0) is the board's top-left corner, (15, 15) its bottom-right.</summary>
        static Vector3 Edge(float x, float y, float height) => new Vector3(x - 7.5f, height, 7.5f - y);

        static void Rect(MeshBatch batch, float x0, float y0, float x1, float y1, float height, Color color)
        {
            batch.Quad(Edge((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, height), x1 - x0, y1 - y0, color);
        }

        static bool IsTrack(int x, int y) => (x >= 6 && x <= 8) != (y >= 6 && y <= 8);

        /// <summary>Builds the whole 2D board under <paramref name="parent"/> (the board root).</summary>
        public static GameObject Build(Transform parent)
        {
            var root = new GameObject("FlatBoard");
            root.transform.SetParent(parent, false);
            var flat = Materials.Unlit(Texture2D.whiteTexture, Color.white, false);

            // White cross under the track; the bases fill the four corners.
            var ground = new MeshBatch();
            Rect(ground, 6f, 0f, 9f, 15f, GroundY, Color.white);
            Rect(ground, 0f, 6f, 6f, 9f, GroundY, Color.white);
            Rect(ground, 9f, 6f, 15f, 9f, GroundY, Color.white);
            AddMesh(root.transform, "Ground", ground.Build("FlatGround"), flat);

            // Coloured squares, the white yards inside the bases and the centre triangles.
            var top = new MeshBatch();
            for (int p = 0; p < GameState.Players; p++)
            {
                var c = (PlayerColor)p;
                var o = BoardLayout.BaseOrigin(c);
                Rect(top, o.X + 1, o.Y + 1, o.X + 5, o.Y + 5, TopY, Color.white);
                for (int progress = BoardLayout.FirstHomeColumnProgress; progress < BoardLayout.HomeProgress; progress++)
                {
                    var cell = BoardLayout.HomeColumnCell(c, progress);
                    Rect(top, cell.X, cell.Y, cell.X + 1, cell.Y + 1, TopY, CellColor(c));
                }
                var start = BoardLayout.Track[BoardLayout.StartOf(c)];
                Rect(top, start.X, start.Y, start.X + 1, start.Y + 1, TopY, CellColor(c));
            }
            Vector3 tl = Edge(6f, 6f, TopY), tr = Edge(9f, 6f, TopY), br = Edge(9f, 9f, TopY), bl = Edge(6f, 9f, TopY), mid = Edge(7.5f, 7.5f, TopY);
            top.Triangle(tl, mid, bl, CellColor(PlayerColor.Red));
            top.Triangle(tl, tr, mid, CellColor(PlayerColor.Green));
            top.Triangle(tr, br, mid, CellColor(PlayerColor.Yellow));
            top.Triangle(bl, mid, br, CellColor(PlayerColor.Blue));
            AddMesh(root.transform, "Squares", top.Build("FlatSquares"), flat);

            // Bases: one rounded outer corner each, turned to face their corner of the board.
            var quad = MeshBuilder.FlatQuad();
            for (int p = 0; p < GameState.Players; p++)
            {
                var c = (PlayerColor)p;
                var o = BoardLayout.BaseOrigin(c);
                var go = AddMesh(root.transform, c + "Base", quad, Materials.Unlit(RoundedCorner, BaseColor(c), true, 0));
                go.transform.localPosition = Edge(o.X + 3f, o.Y + 3f, GroundY);
                go.transform.localRotation = Quaternion.Euler(0f, 90f * p, 0f);
                go.transform.localScale = new Vector3(6f, 1f, 6f);
            }

            AddMesh(root.transform, "Lines", BuildLines(), Materials.Unlit(Texture2D.whiteTexture, Color.white, true, 1));

            var slots = new MeshBatch();
            for (int p = 0; p < GameState.Players; p++)
            {
                var c = (PlayerColor)p;
                for (int t = 0; t < GameState.TokensPerPlayer; t++)
                {
                    slots.Quad(Board3D.BaseSlot(c, t, true) + Vector3.up * DecalY, 1.05f, 1.05f, CellColor(c));
                }
            }
            AddMesh(root.transform, "Slots", slots.Build("FlatSlots"), Materials.Unlit(SpriteFactory.Circle.texture, Color.white, true, 2));

            var stars = new MeshBatch();
            for (int i = 0; i < BoardLayout.TrackLength; i++)
            {
                if (BoardLayout.IsStarSquare(i)) stars.Quad(Board3D.Cell(BoardLayout.Track[i]) + Vector3.up * DecalY, 1f, 1f, Color.white);
            }
            AddMesh(root.transform, "Stars", stars.Build("FlatStars"), Materials.Unlit(Star, Color.white, true, 2));

            // An arrow on the last track square before each home column, pointing into it.
            for (int p = 0; p < GameState.Players; p++)
            {
                var c = (PlayerColor)p;
                var cell = BoardLayout.Track[BoardLayout.GlobalIndex(c, BoardLayout.LastTrackProgress)];
                var go = AddMesh(root.transform, c + "Arrow", quad, Materials.Unlit(Arrow, Color.Lerp(CellColor(c), Color.white, 0.2f), true, 2));
                go.transform.localPosition = Board3D.Cell(cell) + Vector3.up * DecalY;
                go.transform.localRotation = Quaternion.Euler(0f, 90f * p, 0f);
                go.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
            }
            return root;
        }

        /// <summary>
        /// Thin grey lines between all track squares (not along the board's outer edge). Each line segment stops short
        /// of the corners and every corner gets one small square, so no spot is drawn twice and darkened.
        /// </summary>
        static Mesh BuildLines()
        {
            var vertical = new HashSet<Vector2Int>();   // line at x, from y to y + 1
            var horizontal = new HashSet<Vector2Int>(); // line at y, from x to x + 1
            for (int y = 0; y < BoardLayout.Size; y++)
            {
                for (int x = 0; x < BoardLayout.Size; x++)
                {
                    if (!IsTrack(x, y)) continue;
                    if (x > 0) vertical.Add(new Vector2Int(x, y));
                    if (x + 1 < BoardLayout.Size) vertical.Add(new Vector2Int(x + 1, y));
                    if (y > 0) horizontal.Add(new Vector2Int(x, y));
                    if (y + 1 < BoardLayout.Size) horizontal.Add(new Vector2Int(x, y + 1));
                }
            }
            const float h = LineWidth * 0.5f;
            var batch = new MeshBatch();
            var corners = new HashSet<Vector2Int>();
            foreach (var v in vertical)
            {
                Rect(batch, v.x - h, v.y + h, v.x + h, v.y + 1 - h, LinesY, GridLine);
                corners.Add(v);
                corners.Add(new Vector2Int(v.x, v.y + 1));
            }
            foreach (var l in horizontal)
            {
                Rect(batch, l.x + h, l.y - h, l.x + 1 - h, l.y + h, LinesY, GridLine);
                corners.Add(l);
                corners.Add(new Vector2Int(l.x + 1, l.y));
            }
            foreach (var k in corners)
            {
                Rect(batch, Mathf.Max(0f, k.x - h), Mathf.Max(0f, k.y - h), Mathf.Min(BoardLayout.Size, k.x + h), Mathf.Min(BoardLayout.Size, k.y + h), LinesY, GridLine);
            }
            return batch.Build("FlatLines");
        }

        static GameObject AddMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        // ---------- Textures ----------

        /// <summary>A white square with its top-left corner rounded, sized for a 6x6 base.</summary>
        static Texture2D RoundedCorner
        {
            get
            {
                if (roundedCorner != null) return roundedCorner;
                const int size = 256;
                float r = CornerRadius / 6f * size;
                return roundedCorner = Paint(size, size, false, (x, y) =>
                {
                    float dx = r - x, dy = y - (size - r);
                    float a = dx > 0f && dy > 0f ? Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f) : 1f;
                    return new Color(1f, 1f, 1f, a);
                });
            }
        }

        /// <summary>A white five-pointed star with a thin grey outline.</summary>
        static Texture2D Star
        {
            get
            {
                if (star != null) return star;
                const int size = 128;
                var points = new Vector2[10];
                for (int k = 0; k < points.Length; k++)
                {
                    float radius = k % 2 == 0 ? 0.93f : 0.44f;
                    float angle = Mathf.PI / 2f + k * Mathf.PI / 5f;
                    points[k] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }
                return star = Paint(size, size, true, (px, py) =>
                {
                    var p = new Vector2(px / size * 2f - 1f, py / size * 2f - 1f);
                    float d = PolygonDistance(points, p) * size * 0.5f; // in pixels
                    var c = Color.Lerp(StarEdge, Color.white, Mathf.Clamp01(-d - 6f));
                    c.a = Mathf.Clamp01(0.5f - d);
                    return c;
                });
            }
        }

        /// <summary>A thin white arrow pointing right (tinted per player).</summary>
        static Texture2D Arrow
        {
            get
            {
                if (arrow != null) return arrow;
                const int size = 128;
                var tip = new Vector2(0.8f, 0f);
                return arrow = Paint(size, size, true, (px, py) =>
                {
                    var p = new Vector2(px / size * 2f - 1f, py / size * 2f - 1f);
                    float d = Mathf.Min(SegmentDistance(p, new Vector2(-0.8f, 0f), tip),
                        Mathf.Min(SegmentDistance(p, tip, new Vector2(0.42f, 0.36f)), SegmentDistance(p, tip, new Vector2(0.42f, -0.36f))));
                    return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - (d - 0.05f) * size * 0.5f));
                });
            }
        }

        /// <summary>
        /// A map-pin pawn: a silver teardrop with a glossy coloured ball in its head and a soft shadow. The texture
        /// covers <see cref="PinWidth"/> x <see cref="PinHeight"/> squares, starting at (<see cref="PinLeft"/>, <see cref="PinBottom"/>) from the tip.
        /// </summary>
        public static Texture2D Pin(PlayerColor c)
        {
            if (pins[(int)c] != null) return pins[(int)c];
            const float ppu = 160f;
            int w = Mathf.RoundToInt(PinWidth * ppu), h = Mathf.RoundToInt(PinHeight * ppu);
            var ball = CellColor(c) * 0.94f;
            var rim = new Color(ball.r * 0.32f, ball.g * 0.32f, ball.b * 0.32f);
            var head = new Vector2(0f, PinHeadY);
            var highlight = head + new Vector2(-0.09f, 0.1f);
            var spark = head + new Vector2(-0.1f, 0.13f);
            return pins[(int)c] = Paint(w, h, true, (px, py) =>
            {
                float x = px / ppu + PinLeft, y = py / ppu + PinBottom;
                var p = new Vector2(x, y);
                float shadow = Mathf.Clamp01((0.03f - PinDistance(x - 0.035f, y + 0.035f)) / 0.07f) * 0.35f;
                float d = PinDistance(x, y);
                float body = Mathf.Clamp01(0.5f - d * ppu);

                // Silver body: lit from the top left, with a darker rim.
                var col = Color.Lerp(PinDark, PinLight, Mathf.Clamp01(0.55f - x * 0.9f + (y - PinHeadY) * 0.25f));
                col = Color.Lerp(PinEdge, col, Mathf.Clamp01((-d - 0.012f) * ppu * 0.5f));

                float db = Vector2.Distance(p, head) - BallRadius;
                if (db < 0.02f)
                {
                    float k = Mathf.Clamp01(Vector2.Distance(p, highlight) / (BallRadius * 1.4f));
                    var bc = Color.Lerp(Color.Lerp(ball, Color.white, 0.35f), ball * 0.8f, k);
                    bc = Color.Lerp(rim, bc, Mathf.Clamp01((-db - 0.03f) * ppu));
                    bc = Color.Lerp(bc, Color.white, Mathf.Clamp01((0.06f - Vector2.Distance(p, spark)) * ppu * 0.5f) * 0.7f);
                    col = Color.Lerp(col, bc, Mathf.Clamp01(0.5f - db * ppu));
                }

                float a = body + shadow * (1f - body);
                if (a <= 0f) return Color.clear;
                var result = col * (body / a); // the shadow underneath is black
                result.a = a;
                return result;
            });
        }

        /// <summary>Signed distance (in squares) to the pin outline: a circle for the head, narrowing to the tip.</summary>
        static float PinDistance(float x, float y)
        {
            float sin = PinRadius / PinHeadY, cos = Mathf.Sqrt(1f - sin * sin);
            float tangentY = PinHeadY * cos * cos; // where the sides meet the head
            float headDistance = Mathf.Sqrt(x * x + (y - PinHeadY) * (y - PinHeadY)) - PinRadius;
            float cone = Mathf.Max(Mathf.Max(Mathf.Abs(x) * cos - y * sin, y - tangentY), -y);
            return Mathf.Min(headDistance, cone);
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Signed distance to a polygon outline: negative inside.</summary>
        static float PolygonDistance(Vector2[] pts, Vector2 p)
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                d = Mathf.Min(d, SegmentDistance(p, pts[j], pts[i]));
                if ((pts[i].y > p.y) != (pts[j].y > p.y) && p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
            }
            return inside ? -d : d;
        }

        /// <summary>Fills a texture from colour(x, y), with x and y at pixel centres (0.5 to size - 0.5).</summary>
        static Texture2D Paint(int width, int height, bool mipmaps, System.Func<float, float, Color> colour)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, mipmaps)
            {
                filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * height];
            for (int j = 0; j < height; j++)
            {
                for (int i = 0; i < width; i++) pixels[j * width + i] = colour(i + 0.5f, j + 0.5f);
            }
            tex.SetPixels(pixels);
            tex.Apply(mipmaps);
            return tex;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
