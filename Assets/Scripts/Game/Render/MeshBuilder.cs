using System.Collections.Generic;
using UnityEngine;

namespace LudoGame
{
    /// <summary>Generates the game's meshes in code: lathed pawns, rounded boxes and flat decal batches.</summary>
    public static class MeshBuilder
    {
        /// <summary>
        /// Spins a side profile (x = radius, y = height, listed bottom to top) around the Y axis.
        /// </summary>
        public static Mesh Lathe(IList<Vector2> profile, int segments)
        {
            int rings = profile.Count;
            int cols = segments + 1;
            var verts = new Vector3[rings * cols];
            var normals = new Vector3[rings * cols];
            for (int i = 0; i < rings; i++)
            {
                var tangent = (profile[Mathf.Min(rings - 1, i + 1)] - profile[Mathf.Max(0, i - 1)]).normalized;
                var outward = new Vector2(tangent.y, -tangent.x);
                for (int j = 0; j < cols; j++)
                {
                    float a = j * Mathf.PI * 2f / segments;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    verts[i * cols + j] = new Vector3(profile[i].x * c, profile[i].y, profile[i].x * s);
                    normals[i * cols + j] = new Vector3(outward.x * c, outward.y, outward.x * s).normalized;
                }
            }

            var tris = new List<int>((rings - 1) * segments * 6);
            for (int i = 0; i < rings - 1; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int a = i * cols + j, b = a + 1, c = a + cols, d = c + 1;
                    tris.Add(c); tris.Add(d); tris.Add(b);
                    tris.Add(c); tris.Add(b); tris.Add(a);
                }
            }

            var mesh = new Mesh { name = "Lathe" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Box with rounded edges. faceUv (optional) maps each face to a rectangle of a texture atlas.</summary>
        public static Mesh RoundedBox(Vector3 size, float radius, int steps, Rect[] faceUv = null)
        {
            // Faces in dice order: +Y (1), +Z (2), +X (3), -X (4), -Z (5), -Y (6).
            Vector3[] normals = { Vector3.up, Vector3.forward, Vector3.right, Vector3.left, Vector3.back, Vector3.down };
            Vector3[] ups = { Vector3.forward, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.forward };
            var half = size * 0.5f;
            var inner = new Vector3(half.x - radius, half.y - radius, half.z - radius);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int f = 0; f < 6; f++)
            {
                var n = normals[f];
                var v = ups[f];
                var u = Vector3.Cross(n, v);
                float hu = Mathf.Abs(Vector3.Dot(half, u)), hv = Mathf.Abs(Vector3.Dot(half, v)), hn = Mathf.Abs(Vector3.Dot(half, n));
                var rect = faceUv != null ? faceUv[f] : new Rect(0, 0, 1, 1);
                int start = verts.Count;
                for (int j = 0; j <= steps; j++)
                {
                    for (int i = 0; i <= steps; i++)
                    {
                        float fi = (float)i / steps, fj = (float)j / steps;
                        var p = n * hn + u * Mathf.Lerp(-hu, hu, fi) + v * Mathf.Lerp(-hv, hv, fj);
                        var core = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                        var dir = p - core;
                        var normal = dir.sqrMagnitude > 1e-8f ? dir.normalized : n;
                        verts.Add(core + normal * radius);
                        norms.Add(normal);
                        uvs.Add(new Vector2(rect.x + fi * rect.width, rect.y + fj * rect.height));
                    }
                }
                int row = steps + 1;
                for (int j = 0; j < steps; j++)
                {
                    for (int i = 0; i < steps; i++)
                    {
                        int ll = start + j * row + i, lr = ll + 1, ul = ll + row, ur = ul + 1;
                        tris.Add(ul); tris.Add(ur); tris.Add(lr);
                        tris.Add(ul); tris.Add(lr); tris.Add(ll);
                    }
                }
            }

            var mesh = new Mesh { name = "RoundedBox" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A 1x1 quad lying flat on the XZ plane, facing up.</summary>
        public static Mesh FlatQuad()
        {
            var batch = new MeshBatch();
            batch.Quad(Vector3.zero, 1f, 1f, Color.white);
            return batch.Build("FlatQuad");
        }
    }

    /// <summary>Collects many flat, vertex-coloured quads and triangles into one mesh (one draw call).</summary>
    public sealed class MeshBatch
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();

        public void Quad(Vector3 center, float width, float depth, Color color)
        {
            int s = verts.Count;
            float hw = width * 0.5f, hd = depth * 0.5f;
            verts.Add(center + new Vector3(-hw, 0, -hd)); uvs.Add(new Vector2(0, 0));
            verts.Add(center + new Vector3(hw, 0, -hd)); uvs.Add(new Vector2(1, 0));
            verts.Add(center + new Vector3(-hw, 0, hd)); uvs.Add(new Vector2(0, 1));
            verts.Add(center + new Vector3(hw, 0, hd)); uvs.Add(new Vector2(1, 1));
            for (int i = 0; i < 4; i++) colors.Add(color);
            tris.Add(s + 2); tris.Add(s + 3); tris.Add(s + 1);
            tris.Add(s + 2); tris.Add(s + 1); tris.Add(s);
        }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int s = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(new Vector2(0.5f, 0.5f)); uvs.Add(new Vector2(0.5f, 0.5f)); uvs.Add(new Vector2(0.5f, 0.5f));
            colors.Add(color); colors.Add(color); colors.Add(color);
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
