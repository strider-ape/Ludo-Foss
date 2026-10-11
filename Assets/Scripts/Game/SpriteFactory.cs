using System;
using UnityEngine;

namespace LudoGame
{
    /// <summary>Generates every sprite the game uses at runtime, so the project needs no image assets.</summary>
    public static class SpriteFactory
    {
        static Sprite square, circle, ring, star, rounded, face;
        static Texture2D diceAtlas, radialShadow, table;

        public static Sprite Square => square != null ? square : square = Make(8, (x, y) => 1f);

        public static Sprite Circle => circle != null ? circle : circle = Make(128, (x, y) => Disc(x, y, 1f, 64));

        public static Sprite Ring => ring != null ? ring : ring = Make(128, (x, y) => Disc(x, y, 1f, 64) * (1f - Disc(x, y, 0.72f, 64)));

        public static Sprite Star => star != null ? star : star = Make(128, StarCoverage);

        /// <summary>Rounded rectangle meant for Image.Type.Sliced.</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(64, 22);

        /// <summary>A friendly face (eyes and smile) in white, drawn over a coloured circle for player avatars.</summary>
        public static Sprite Face => face != null ? face : face = Make(128, (x, y) =>
        {
            float eyes = Mathf.Max(Disc(x + 0.32f, y - 0.18f, 0.13f, 64), Disc(x - 0.32f, y - 0.18f, 0.13f, 64));
            float r = Mathf.Sqrt(x * x + (y + 0.05f) * (y + 0.05f));
            float smile = y < -0.12f ? Mathf.Clamp01((0.09f - Mathf.Abs(r - 0.45f)) * 64f) : 0f;
            return Mathf.Max(eyes, smile);
        });

        /// <summary>Dice faces 1-6 in a 3x2 atlas: values 1-3 on the bottom row, 4-6 on the top row.</summary>
        public static Texture2D DiceAtlas
        {
            get
            {
                if (diceAtlas != null) return diceAtlas;
                const int cell = 128;
                var tex = new Texture2D(cell * 3, cell * 2, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
                var pixels = new Color[cell * 3 * cell * 2];
                var pipColor = new Color(0.13f, 0.15f, 0.24f);
                var onePip = new Color(0.85f, 0.15f, 0.15f);
                for (int value = 1; value <= 6; value++)
                {
                    int ox = (value - 1) % 3 * cell, oy = (value - 1) / 3 * cell;
                    var pips = PipPositions(value);
                    for (int j = 0; j < cell; j++)
                    {
                        for (int i = 0; i < cell; i++)
                        {
                            float x = (i + 0.5f) / cell, y = (j + 0.5f) / cell;
                            float edge = Mathf.Min(Mathf.Min(x, 1f - x), Mathf.Min(y, 1f - y));
                            var col = Color.Lerp(new Color(0.86f, 0.86f, 0.9f), Color.white, Mathf.Clamp01(edge * 8f));
                            foreach (var p in pips)
                            {
                                float d = Vector2.Distance(new Vector2(x, y), p);
                                col = Color.Lerp(col, value == 1 ? onePip : pipColor, Mathf.Clamp01(((value == 1 ? 0.15f : 0.1f) - d) * cell));
                            }
                            pixels[(oy + j) * cell * 3 + ox + i] = col;
                        }
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply(true);
                return diceAtlas = tex;
            }
        }

        /// <summary>Soft round shadow: opaque in the middle, fading to nothing at the edge.</summary>
        public static Texture2D RadialShadow => radialShadow != null ? radialShadow : radialShadow = Make(64, (x, y) =>
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d) ;
        }).texture;

        /// <summary>Table under the board: a soft radial gradient.</summary>
        public static Texture2D Table
        {
            get
            {
                if (table != null) return table;
                const int size = 256;
                var tex = NewTexture(size);
                var pixels = new Color[size * size];
                for (int j = 0; j < size; j++)
                {
                    for (int i = 0; i < size; i++)
                    {
                        float x = (i + 0.5f) / size * 2f - 1f, y = (j + 0.5f) / size * 2f - 1f;
                        float d = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
                        pixels[j * size + i] = Color.Lerp(Palette.TableCenter, Palette.Background, Ease.Smooth(d));
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                return table = tex;
            }
        }

        static Vector2[] PipPositions(int value)
        {
            const float a = 0.27f, b = 0.5f, c = 0.73f;
            switch (value)
            {
                case 1: return new[] { new Vector2(b, b) };
                case 2: return new[] { new Vector2(a, a), new Vector2(c, c) };
                case 3: return new[] { new Vector2(a, a), new Vector2(b, b), new Vector2(c, c) };
                case 4: return new[] { new Vector2(a, a), new Vector2(c, a), new Vector2(a, c), new Vector2(c, c) };
                case 5: return new[] { new Vector2(a, a), new Vector2(c, a), new Vector2(b, b), new Vector2(a, c), new Vector2(c, c) };
                default: return new[] { new Vector2(a, a), new Vector2(c, a), new Vector2(a, b), new Vector2(c, b), new Vector2(a, c), new Vector2(c, c) };
            }
        }

        /// <summary>Builds a white sprite whose alpha comes from coverage(x, y), with x and y in -1..1.</summary>
        static Sprite Make(int size, Func<float, float, float> coverage)
        {
            var tex = NewTexture(size);
            var pixels = new Color[size * size];
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float x = (i + 0.5f) / size * 2f - 1f;
                    float y = (j + 0.5f) / size * 2f - 1f;
                    pixels[j * size + i] = new Color(1f, 1f, 1f, coverage(x, y));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = NewTexture(size);
            var pixels = new Color[size * size];
            float r = (float)radius / size;
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float x = (i + 0.5f) / size;
                    float y = (j + 0.5f) / size;
                    pixels[j * size + i] = new Color(1f, 1f, 1f, RoundedBox(x, y, 0.5f, 0.5f, 0.5f, 0.5f, r, size));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        static float Disc(float x, float y, float radius, int halfSize)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return Mathf.Clamp01((radius - d) * halfSize);
        }

        /// <summary>Anti-aliased coverage of a rounded box (all units 0..1 of the texture).</summary>
        static float RoundedBox(float x, float y, float cx, float cy, float hw, float hh, float r, int size)
        {
            float qx = Mathf.Abs(x - cx) - (hw - r);
            float qy = Mathf.Abs(y - cy) - (hh - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float dist = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            return Mathf.Clamp01(-dist * size);
        }

        static float StarCoverage(float x, float y)
        {
            // 4x4 supersampling of a five-pointed star.
            const int samples = 4;
            const float step = 2f / 128f / samples;
            int inside = 0;
            for (int sy = 0; sy < samples; sy++)
            {
                for (int sx = 0; sx < samples; sx++)
                {
                    if (InStar(x + (sx - 1.5f) * step, y + (sy - 1.5f) * step)) inside++;
                }
            }
            return inside / (float)(samples * samples);
        }

        static readonly Vector2[] StarPoints = BuildStarPoints();

        static Vector2[] BuildStarPoints()
        {
            var pts = new Vector2[10];
            for (int k = 0; k < 10; k++)
            {
                float radius = k % 2 == 0 ? 0.95f : 0.4f;
                float angle = Mathf.PI / 2f + k * Mathf.PI / 5f;
                pts[k] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return pts;
        }

        /// <summary>Even-odd point-in-polygon test against the star outline.</summary>
        static bool InStar(float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = StarPoints.Length - 1; i < StarPoints.Length; j = i++)
            {
                var a = StarPoints[i];
                var b = StarPoints[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }
}
