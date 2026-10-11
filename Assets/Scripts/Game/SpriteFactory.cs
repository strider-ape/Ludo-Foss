using System;
using UnityEngine;

namespace LudoTurbo
{
    /// <summary>Generates every sprite the game uses at runtime, so the project needs no image assets.</summary>
    public static class SpriteFactory
    {
        static Sprite square, circle, ring, star, rounded, center;
        static readonly Sprite[] dice = new Sprite[7];

        public static Sprite Square => square != null ? square : square = Make(8, (x, y) => 1f);

        public static Sprite Circle => circle != null ? circle : circle = Make(128, (x, y) => Disc(x, y, 1f, 64));

        public static Sprite Ring => ring != null ? ring : ring = Make(128, (x, y) => Disc(x, y, 1f, 64) * (1f - Disc(x, y, 0.72f, 64)));

        public static Sprite Star => star != null ? star : star = Make(128, StarCoverage);

        /// <summary>Rounded rectangle meant for Image.Type.Sliced.</summary>
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(64, 22);

        /// <summary>The 3x3 home area: four triangles in red (left), green (top), yellow (right), blue (bottom).</summary>
        public static Sprite Center
        {
            get
            {
                if (center != null) return center;
                const int size = 192;
                var tex = NewTexture(size);
                var pixels = new Color[size * size];
                for (int j = 0; j < size; j++)
                {
                    for (int i = 0; i < size; i++)
                    {
                        float dx = (i + 0.5f) / size - 0.5f;
                        float dy = (j + 0.5f) / size - 0.5f; // texture row 0 is the bottom, so positive = towards the top
                        Core.PlayerColor c;
                        if (Mathf.Abs(dx) > Mathf.Abs(dy)) c = dx < 0 ? Core.PlayerColor.Red : Core.PlayerColor.Yellow;
                        else c = dy > 0 ? Core.PlayerColor.Green : Core.PlayerColor.Blue;
                        pixels[j * size + i] = Palette.Of(c);
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                return center = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        public static Sprite DiceFace(int value)
        {
            if (dice[value] != null) return dice[value];
            const int size = 128;
            var tex = NewTexture(size);
            var pixels = new Color[size * size];
            var pips = PipPositions(value);
            var pipColor = new Color(0.12f, 0.16f, 0.27f);
            for (int j = 0; j < size; j++)
            {
                for (int i = 0; i < size; i++)
                {
                    float x = (i + 0.5f) / size;
                    float y = 1f - (j + 0.5f) / size;
                    float body = RoundedBox(x, y, 0.5f, 0.5f, 0.5f, 0.5f, 0.17f, size);
                    float pip = 0f;
                    foreach (var p in pips)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), p);
                        pip = Mathf.Max(pip, Mathf.Clamp01((0.095f - d) * size));
                    }
                    var col = Color.Lerp(Color.white, pipColor, pip);
                    col.a = body;
                    pixels[j * size + i] = col;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return dice[value] = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
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
