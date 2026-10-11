using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Draws the app icon in code (the four Ludo colours with a white pawn) and applies it to the build.
    /// Regenerate with Ludo > Generate App Icon, or:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod LudoGame.EditorTools.AppIcon.GenerateAndExit
    /// </summary>
    public static class AppIcon
    {
        const string Folder = "Assets/Icons";
        const string BackgroundPath = Folder + "/icon-background.png";
        const string ForegroundPath = Folder + "/icon-foreground.png";
        const string LegacyPath = Folder + "/icon.png";
        const string RoundPath = Folder + "/icon-round.png";
        const int Size = 512;

        static readonly Color Outline = new Color(0.1f, 0.13f, 0.27f);

        [MenuItem("Ludo/Generate App Icon")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            var pawn = Silhouette();
            // Adaptive icon: Android crops it to the phone's icon shape (only the middle ~2/3 is always visible).
            Write(BackgroundPath, (x, y) => Background(x, y, 0.56f));
            Write(ForegroundPath, (x, y) => Pawn(pawn, x, y, 0.44f));
            // Older launchers: a finished rounded square and a circle.
            Write(LegacyPath, (x, y) => Mask(Over(Pawn(pawn, x, y, 0.62f), Background(x, y, 0.74f)), RoundedSquare(x, y)));
            Write(RoundPath, (x, y) => Mask(Over(Pawn(pawn, x, y, 0.58f), Background(x, y, 0.66f)), Disc(x, y)));
            AssetDatabase.Refresh();
            foreach (var path in new[] { BackgroundPath, ForegroundPath, LegacyPath, RoundPath })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            Debug.Log("[Ludo] App icon generated in " + Folder);
        }

        public static void GenerateAndExit()
        {
            Generate();
            EditorApplication.Exit(0);
        }

        /// <summary>Sets the generated icon for every platform, plus Android's adaptive and round variants.</summary>
        public static void Apply()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(LegacyPath) == null) Generate();
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(LegacyPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);
#if UNITY_ANDROID
            SetAndroidIcons(AndroidPlatformIconKind.Adaptive,
                AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath), AssetDatabase.LoadAssetAtPath<Texture2D>(ForegroundPath));
            SetAndroidIcons(AndroidPlatformIconKind.Round, AssetDatabase.LoadAssetAtPath<Texture2D>(RoundPath));
            SetAndroidIcons(AndroidPlatformIconKind.Legacy, legacy);
#endif
        }

#if UNITY_ANDROID
        static void SetAndroidIcons(PlatformIconKind kind, params Texture2D[] textures)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var icon in icons) icon.SetTextures(textures);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }
#endif

        // ---------- Drawing (x and y run from -1 to 1, y up) ----------

        static void Write(string path, Func<float, float, Color> shade)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color[Size * Size];
            for (int j = 0; j < Size; j++)
            {
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) / Size * 2f - 1f, y = (j + 0.5f) / Size * 2f - 1f;
                    pixels[j * Size + i] = shade(x, y);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// The game's navy table with the board's home "pinwheel": a square split corner to corner into
        /// red (left), green (top), yellow (right) and blue (bottom) triangles, framed in white.
        /// </summary>
        static Color Background(float x, float y, float half)
        {
            float px = 2f / Size;
            float r = Mathf.Sqrt(x * x + y * y) / 1.414f;
            var table = Color.Lerp(Palette.TableCenter, Palette.Background, Ease.Smooth(Mathf.Clamp01(r * 1.2f)));

            const float corner = 0.1f;
            const float frame = 0.045f;
            float box = RoundedBoxDistance(x, y, half, corner);
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            Color tri = ax > ay
                ? (x < 0f ? Palette.Of(Core.PlayerColor.Red) : Palette.Of(Core.PlayerColor.Yellow))
                : (y > 0f ? Palette.Of(Core.PlayerColor.Green) : Palette.Of(Core.PlayerColor.Blue));
            tri = Color.Lerp(tri, Color.white, (1f - Mathf.Clamp01(Mathf.Max(ax, ay) / half)) * 0.22f);
            tri.a = 1f;
            // Thin white seams along the diagonals, like the board.
            float seam = Mathf.Clamp01((0.018f - Mathf.Abs(ax - ay) / 1.414f) / px + 0.5f);
            var inside = Color.Lerp(tri, Color.white, seam);
            var framed = Color.Lerp(inside, Color.white, Mathf.Clamp01((box + frame) / px + 0.5f));
            float coverage = Mathf.Clamp01(0.5f - box / px);
            // A soft shadow under the square lifts it off the table.
            float shadow = 0.35f * Mathf.Clamp01(0.5f - RoundedBoxDistance(x - 0.02f, y + 0.035f, half, corner) / 0.06f);
            var under = Color.Lerp(table, Color.black, shadow);
            var result = Color.Lerp(under, framed, coverage);
            result.a = 1f;
            return result;
        }

        static float RoundedBoxDistance(float x, float y, float half, float radius)
        {
            float qx = Mathf.Abs(x) - (half - radius), qy = Mathf.Abs(y) - (half - radius);
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        /// <summary>The pawn's outline from its lathe profile: right side up, left side down.</summary>
        static Vector2[] Silhouette()
        {
            var profile = Pawn3D.Profile;
            var pts = new List<Vector2>();
            for (int i = 0; i < profile.Length; i++) pts.Add(new Vector2(profile[i].x, profile[i].y));
            for (int i = profile.Length - 2; i > 0; i--) pts.Add(new Vector2(-profile[i].x, profile[i].y));
            return pts.ToArray();
        }

        /// <summary>A white pawn with a dark outline and a soft drop shadow; height is a fraction of the icon.</summary>
        static Color Pawn(Vector2[] pawn, float x, float y, float height)
        {
            float scale = height * 2f;          // pawn units -> icon units
            float bottom = -height * 0.98f;
            float toPixels = scale * Size * 0.5f;
            var p = new Vector2(x / scale, (y - bottom) / scale);
            float d = Sdf(pawn, p) * toPixels;

            var s = new Vector2((x - 0.035f) / scale, (y + 0.05f - bottom) / scale);
            float blur = Size * 0.025f;
            float shadowAlpha = 0.45f * Mathf.Clamp01(0.5f - Sdf(pawn, s) * toPixels / (2f * blur));
            var shadow = new Color(0.05f, 0.06f, 0.15f, shadowAlpha);

            float outlineWidth = Size * 0.016f;
            float fillShade = Mathf.Lerp(0.84f, 1f, Mathf.Clamp01(p.y));
            var fill = new Color(fillShade, fillShade, Mathf.Min(1f, fillShade + 0.03f));
            var body = Color.Lerp(Outline, fill, Mathf.Clamp01(0.5f - (d + outlineWidth)));
            body.a = Mathf.Clamp01(0.5f - d);
            return Over(body, shadow);
        }

        static Color RoundedSquare(float x, float y)
        {
            const float r = 0.36f;
            float qx = Mathf.Abs(x) - (1f - r), qy = Mathf.Abs(y) - (1f - r);
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d * Size * 0.5f));
        }

        static Color Disc(float x, float y) => new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - (Mathf.Sqrt(x * x + y * y) - 1f) * Size * 0.5f));

        static Color Mask(Color c, Color mask) => new Color(c.r, c.g, c.b, c.a * mask.a);

        /// <summary>Straight-alpha "a over b".</summary>
        static Color Over(Color a, Color b)
        {
            float alpha = a.a + b.a * (1f - a.a);
            if (alpha <= 0f) return new Color(0f, 0f, 0f, 0f);
            var rgb = ((Vector4)a * a.a + (Vector4)b * b.a * (1f - a.a)) / alpha;
            return new Color(rgb.x, rgb.y, rgb.z, alpha);
        }

        /// <summary>Signed distance to a polygon: negative inside.</summary>
        static float Sdf(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[j];
                var b = poly[i];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-9f));
                best = Mathf.Min(best, (p - (a + ab * t)).sqrMagnitude);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            float d = Mathf.Sqrt(best);
            return inside ? -d : d;
        }
    }
}
