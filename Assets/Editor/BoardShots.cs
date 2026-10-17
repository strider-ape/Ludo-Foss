using System;
using System.Collections.Generic;
using System.IO;
using LudoGame.Core;
using UnityEditor;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Renders the 3D board with pawns in chosen layouts to PNGs at phone resolution, for checking how readable the
    /// pieces are (no desktop build needed):
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod LudoGame.EditorTools.BoardShots.RenderAll -out shots
    /// (run without -nographics so there is a GPU to render with).
    /// </summary>
    public static class BoardShots
    {
        const int Width = 1080;
        const int Height = 2340;

        struct Piece
        {
            public PlayerColor Color;
            public int Token;
            public int Progress;

            public Piece(PlayerColor color, int token, int progress)
            {
                Color = color;
                Token = token;
                Progress = progress;
            }
        }

        public static void RenderAll()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-out");
            string folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Builds/boardshots";
            Directory.CreateDirectory(folder);
            try
            {
                Render(folder);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Render(string folder)
        {
            const PlayerColor R = PlayerColor.Red, G = PlayerColor.Green, Y = PlayerColor.Yellow, B = PlayerColor.Blue;
            var layouts = new (string name, Piece[] pieces)[]
            {
                // Each colour standing on its own colour: start square, home column, and at home in base.
                ("own-colour", new[]
                {
                    new Piece(R, 0, 0), new Piece(R, 1, 52), new Piece(R, 2, -1), new Piece(R, 3, -1),
                    new Piece(G, 0, 0), new Piece(G, 1, 52), new Piece(G, 2, -1), new Piece(G, 3, -1),
                    new Piece(Y, 0, 0), new Piece(Y, 1, 52), new Piece(Y, 2, -1), new Piece(Y, 3, -1),
                    new Piece(B, 0, 0), new Piece(B, 1, 52), new Piece(B, 2, -1), new Piece(B, 3, -1),
                }),
                // A busy middle game: white squares, stars, stacks of two and three, pawns side by side.
                ("busy", new[]
                {
                    new Piece(R, 0, 6), new Piece(R, 1, 6), new Piece(R, 2, 17), new Piece(R, 3, -1),
                    new Piece(G, 0, 3), new Piece(G, 1, 8), new Piece(G, 2, 22), new Piece(G, 3, 22),
                    new Piece(Y, 0, 10), new Piece(Y, 1, 11), new Piece(Y, 2, 30), new Piece(Y, 3, -1),
                    new Piece(B, 0, 21), new Piece(B, 1, 21), new Piece(B, 2, 21), new Piece(B, 3, 45),
                }),
            };

            Materials.SetupLighting();
            var target = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            var created = new List<UnityEngine.Object> { target };
            var rig = CameraRig.Create();
            created.Add(rig.gameObject);
            var table = GameObject.Find("Table");
            if (table != null) created.Add(table);
            rig.Cam.targetTexture = target;
            rig.Frame();
            var board = new Board3D(null);
            created.Add(board.Root.gameObject);
            board.SetRotationSteps(3); // Red at the bottom-left, as when Red is the local player
            // The pawn looks offered in Settings, the colour-blind shapes, and the edge-to-edge board.
            var styles = new (string name, PawnLook look, bool shapes, bool edge)[]
            {
                ("1-classic", PawnLook.Classic, false, false),
                ("2-outline", PawnLook.Outline, false, false),
                ("3-outline-coin", PawnLook.OutlineCoin, false, false),
                ("4-outline-shapes", PawnLook.Outline, true, false),
                ("5-edge-to-edge", PawnLook.Outline, false, true),
            };
            var saved = (PawnStyle.Size, PawnStyle.Outline, PawnStyle.Coin, PawnStyle.StackScale, PawnStyle.Shapes);
            var crops = new Dictionary<string, List<Texture2D>>();
            var fullShots = new List<Texture2D>();
            rig.ApplyPitch();
            try
            {
                foreach (var style in styles)
                {
                    PawnStyle.Apply(style.look, style.shapes);
                    board.SetShapesVisible(style.shapes);
                    board.SetEdgeToEdge(style.edge);
                    rig.Frame();
                    foreach (var (name, pieces) in layouts)
                    {
                        var pawns = Place(board, pieces);
                        var shot = Capture(rig.Cam, target);
                        Save(shot, Path.Combine(folder, $"{style.name}-{name}.png"));
                        if (name == "busy" && (style.name == "2-outline" || style.edge)) fullShots.Add(Capture(rig.Cam, target));
                        // A 1:1 crop of the bottom-left quarter of the board: how big things really are on a phone.
                        var crop = Crop(shot, rig, new Vector3(-Board3D.HalfExtent, 0f, 0f), new Vector3(0f, 0f, -Board3D.HalfExtent));
                        if (!crops.TryGetValue(name, out var list)) crops[name] = list = new List<Texture2D>();
                        list.Add(crop);
                        UnityEngine.Object.DestroyImmediate(shot);
                        foreach (var pawn in pawns) UnityEngine.Object.DestroyImmediate(pawn.gameObject);
                    }
                    var cellPx = (rig.WorldToScreen(Vector3.zero) - rig.WorldToScreen(Vector3.right)).magnitude;
                    Debug.Log($"[Ludo] {style.name}: one cell = {cellPx:F0} px, pawn base = {cellPx * 0.78f * PawnStyle.Size:F0} px wide at {Width}x{Height}");
                }
                // "Zoom to choices": Red may move its pair on square 6 or its pawn on 17, with a roll of 4.
                {
                    var busy = layouts[1].pieces;
                    var pawns = Place(board, busy);
                    var points = new List<Vector3>();
                    float height = 1.25f * PawnStyle.Size;
                    foreach (var (token, progress) in new[] { (0, 6), (2, 17) })
                    {
                        var at = board.World(board.TokenLocal(PlayerColor.Red, token, progress));
                        var to = board.World(board.TokenLocal(PlayerColor.Red, token, progress + 4));
                        points.Add(at);
                        points.Add(at + Vector3.up * height);
                        points.Add(to);
                        points.Add(to + Vector3.up * height);
                    }
                    rig.ZoomToFit(points, 0f);
                    var focus = board.World(board.TokenLocal(PlayerColor.Red, 2, 17));
                    var zoomedCell = (rig.WorldToScreen(focus) - rig.WorldToScreen(focus + Vector3.right)).magnitude;
                    var shot = Capture(rig.Cam, target);
                    Save(shot, Path.Combine(folder, "6-zoom-to-choices.png"));
                    fullShots.Add(shot);
                    Debug.Log($"[Ludo] 6-zoom-to-choices: one cell = {zoomedCell:F0} px around the choices");
                    rig.ResetZoom(0f);
                    foreach (var pawn in pawns) UnityEngine.Object.DestroyImmediate(pawn.gameObject);
                }

                foreach (var pair in crops)
                {
                    var sheet = SideBySide(pair.Value, 16);
                    Save(sheet, Path.Combine(folder, $"compare-{pair.Key}.png"));
                    UnityEngine.Object.DestroyImmediate(sheet);
                }
                var sizes = SideBySide(fullShots, 24);
                Save(sizes, Path.Combine(folder, "compare-board-size.png"));
                UnityEngine.Object.DestroyImmediate(sizes);

                // The flat die's six faces, on the game's background.
                var faces = new List<Texture2D>();
                for (int v = 1; v <= 6; v++)
                {
                    var src = SpriteFactory.DiceFace(v).texture;
                    var flat = new Texture2D(src.width, src.height, TextureFormat.RGB24, false);
                    var px = src.GetPixels();
                    for (int k = 0; k < px.Length; k++) px[k] = Color.Lerp(Palette.Background, px[k], px[k].a);
                    flat.SetPixels(px);
                    flat.Apply();
                    faces.Add(flat);
                }
                var faceSheet = SideBySide(faces, 20);
                Save(faceSheet, Path.Combine(folder, "dice-faces.png"));
                UnityEngine.Object.DestroyImmediate(faceSheet);
                foreach (var f in faces) UnityEngine.Object.DestroyImmediate(f);
                Debug.Log($"[Ludo] Board shots saved to {folder}");
            }
            finally
            {
                (PawnStyle.Size, PawnStyle.Outline, PawnStyle.Coin, PawnStyle.StackScale, PawnStyle.Shapes) = saved;
                board.SetEdgeToEdge(false);
                foreach (var list in crops.Values)
                {
                    foreach (var t in list) UnityEngine.Object.DestroyImmediate(t);
                }
                foreach (var t in fullShots) UnityEngine.Object.DestroyImmediate(t);
                foreach (var o in created) UnityEngine.Object.DestroyImmediate(o);
            }
        }

        /// <summary>Puts pawns on the board, fanning out pawns that share a square exactly like a match does.</summary>
        static List<Pawn3D> Place(Board3D board, Piece[] pieces)
        {
            var pawns = new List<Pawn3D>();
            var groups = new Dictionary<Vector2Int, List<(Pawn3D pawn, Vector3 pos)>>();
            foreach (var p in pieces)
            {
                var pawn = Pawn3D.Create(board.Root, p.Color, p.Token);
                pawns.Add(pawn);
                var pos = board.TokenLocal(p.Color, p.Token, p.Progress);
                var key = new Vector2Int(Mathf.RoundToInt(pos.x * 100f), Mathf.RoundToInt(pos.z * 100f));
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<(Pawn3D, Vector3)>();
                list.Add((pawn, pos));
            }
            foreach (var list in groups.Values)
            {
                for (int k = 0; k < list.Count; k++)
                {
                    var (pawn, pos) = list[k];
                    if (list.Count == 1)
                    {
                        pawn.transform.localPosition = pos;
                        continue;
                    }
                    float angle = Mathf.PI / 4f + k * Mathf.PI * 2f / list.Count;
                    pawn.transform.localPosition = pos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.22f;
                    pawn.ApplyBaseScale(PawnStyle.StackScale);
                }
            }
            return pawns;
        }

        static Texture2D Capture(Camera cam, RenderTexture target)
        {
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var tex = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            return tex;
        }

        static Texture2D Crop(Texture2D source, CameraRig rig, Vector3 cornerA, Vector3 cornerB)
        {
            var a = rig.WorldToScreen(cornerA);
            var b = rig.WorldToScreen(cornerB);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x)), 0, source.width - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y)), 0, source.height - 1);
            int w = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(a.x - b.x)), 1, source.width - x0);
            int h = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(a.y - b.y)), 1, source.height - y0);
            var crop = new Texture2D(w, h, TextureFormat.RGB24, false);
            crop.SetPixels(source.GetPixels(x0, y0, w, h));
            crop.Apply();
            return crop;
        }

        static void Save(Texture2D tex, string path) => File.WriteAllBytes(path, tex.EncodeToPNG());

        /// <summary>Joins images left to right on the table colour, bottoms aligned.</summary>
        static Texture2D SideBySide(List<Texture2D> images, int gap)
        {
            int width = gap, height = 0;
            foreach (var img in images)
            {
                width += img.width + gap;
                height = Mathf.Max(height, img.height);
            }
            height += gap * 2;
            var sheet = new Texture2D(width, height, TextureFormat.RGB24, false);
            var fill = new Color[width * height];
            for (int i = 0; i < fill.Length; i++) fill[i] = Palette.Background;
            sheet.SetPixels(fill);
            int x = gap;
            foreach (var img in images)
            {
                sheet.SetPixels(x, gap, img.width, img.height, img.GetPixels());
                x += img.width + gap;
            }
            sheet.Apply();
            return sheet;
        }
    }
}
