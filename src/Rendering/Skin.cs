using System.Collections.Generic;
using UnityEngine;

namespace LeaderboardPlus;

public struct Look
{
    public int board, row;
    public float jit, wob, tilt;
    public bool studs;
}

public static class Skin
{
    public const int grainV = 0, grainH = 1, blotch = 2, scan = 3, facet = 4, plain = 5;
    public static readonly Look[] looks =
    {
        new() { board = blotch, row = blotch, jit = 0.3f, wob = 0.5f, tilt = 0.8f },
        new() { board = grainV, row = grainH, jit = 0.55f, wob = 1.1f, tilt = 2.4f, studs = true },
        new() { board = blotch, row = grainH, jit = 0.6f, wob = 1.2f, tilt = 1.8f },
        new() { board = scan, row = scan, studs = true },
        new() { board = facet, row = facet, jit = 0.9f, wob = 0.4f, tilt = 1.4f }
    };

    static readonly Vector3 sun = new Vector3(-0.35f, 0.6f, -0.72f).normalized;
    static Texture2D[] textures = new Texture2D[6];
    static Dictionary<long, Mesh> slabs = new(), rounds = new();
    static List<Vector3> verts = new();
    static List<Color> colors = new();
    static List<Vector2> uvs = new();
    static List<int> tris = new();

    public static Texture2D Tex(int kind)
    {
        if (textures[kind] != null) return textures[kind];
        const int n = 32;
        var rng = new System.Random(11 + kind * 7);
        var pixels = new Color[n * n];
        var line = new float[n];
        var grid = new float[81];
        var walk = 0.85f;
        for (var i = 0; i < n; i++)
        {
            walk = Mathf.Clamp(walk + ((float)rng.NextDouble() - 0.5f) * 0.16f, 0.68f, 1f);
            line[i] = walk;
        }
        for (var i = 0; i < grid.Length; i++) grid[i] = (float)rng.NextDouble();
        for (var i = 0; i < pixels.Length; i++)
        {
            var v = Mathf.Clamp01(Pixel(kind, i % n, i / n, (float)rng.NextDouble(), line, grid));
            pixels[i] = new Color(v, v, v, 1f);
        }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        tex.SetPixels(pixels);
        tex.Apply();
        textures[kind] = tex;
        return tex;
    }

    static float Pixel(int kind, int x, int y, float noise, float[] line, float[] grid)
    {
        if (kind == grainV || kind == grainH)
        {
            int along = kind == grainV ? y : x, across = kind == grainV ? x : y;
            return line[across] + (noise - 0.5f) * 0.07f - ((across * 7 + along / 5) % 13 == 0 ? 0.14f : 0f);
        }
        if (kind == scan) return (y % 4 == 0 ? 0.8f : 1f) - noise * 0.03f;
        if (kind == facet) return 0.7f + Mathf.Floor(Mathf.Repeat((x + y * 0.6f) / 11f, 1f) * 4f) / 4f * 0.3f;
        if (kind != blotch) return 0.94f + noise * 0.06f;
        float gx = x / 4f, gy = y / 4f;
        int ix = (int)gx, iy = (int)gy, nx = (ix + 1) % 8, ny = (iy + 1) % 8 * 9;
        var top = Mathf.Lerp(grid[iy * 9 + ix], grid[iy * 9 + nx], gx - ix);
        var bottom = Mathf.Lerp(grid[ny + ix], grid[ny + nx], gx - ix);
        return 0.72f + Mathf.Floor(Mathf.Lerp(top, bottom, gy - iy) * 5f) / 5f * 0.28f - (noise > 0.97f ? 0.15f : 0f);
    }

    static void Tri(Vector3 a, Vector3 b, Vector3 c)
    {
        var normal = Vector3.Cross(b - a, c - a).normalized;
        if (normal.z > 0f) normal = -normal;
        var shade = 0.5f + 0.5f * Mathf.Clamp01(Vector3.Dot(normal, sun) * 1.15f);
        Vert(a, shade);
        Vert(b, shade);
        Vert(c, shade);
    }

    static void Vert(Vector3 point, float shade)
    {
        tris.Add(verts.Count);
        verts.Add(point);
        colors.Add(new Color(shade, shade, shade, 1f));
        uvs.Add(new Vector2(point.x, point.y) / 40f);
    }

    static Mesh Flush(string name)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static float Edge(int i, int last, float bevel)
    {
        if (i == 0) return -bevel;
        return i == last ? bevel : 0f;
    }

    public static Mesh Slab(float w, float h, float depth, float bevel, float jit, float wob, int seed)
    {
        var key = ((long)Mathf.RoundToInt(w * 4f) << 46) | ((long)Mathf.RoundToInt(h * 4f) << 32)
            | ((long)Mathf.RoundToInt(depth * 8f) << 24) | ((long)Mathf.RoundToInt(bevel * 8f) << 18)
            | ((long)Mathf.RoundToInt(jit * 8f) << 12) | ((long)Mathf.RoundToInt(wob * 8f) << 6) | (long)(seed & 63);
        if (slabs.TryGetValue(key, out var mesh) && mesh != null) return mesh;
        int nx = Mathf.Clamp(Mathf.RoundToInt(w / 9f), 1, 14), ny = Mathf.Clamp(Mathf.RoundToInt(h / 9f), 1, 14), row = nx + 1;
        bevel = Mathf.Min(bevel, Mathf.Min(w, h) * 0.3f);
        float cw = (w - 2f * bevel) / nx, ch = (h - 2f * bevel) / ny;
        var rng = new System.Random(seed * 7919 + Mathf.RoundToInt(w * 31f) + Mathf.RoundToInt(h * 17f));
        float Rand() => (float)rng.NextDouble() * 2f - 1f;

        float Shift(bool edge, float cell)
        {
            if (edge) return Rand() * wob;
            return jit > 0f ? Rand() * cell * 0.18f : 0f;
        }

        var front = new Vector3[row * (ny + 1)];
        for (var i = 0; i < front.Length; i++)
        {
            int x = i % row, y = i / row;
            bool edgeX = x == 0 || x == nx, edgeY = y == 0 || y == ny;
            var px = -w / 2f + bevel + cw * x + Shift(edgeX, cw);
            var py = -h / 2f + bevel + ch * y + Shift(edgeY, ch);
            front[i] = new Vector3(px, py, -depth + Rand() * jit * (edgeX || edgeY ? 0.3f : 1f));
        }

        Vector3 Back(int x, int y)
        {
            var p = front[y * row + x];
            return new Vector3(p.x + Edge(x, nx, bevel), p.y + Edge(y, ny, bevel), 0f);
        }

        void Side(int x0, int y0, int x1, int y1)
        {
            Vector3 a = front[y0 * row + x0], b = front[y1 * row + x1], backB = Back(x1, y1);
            Tri(a, b, backB);
            Tri(a, backB, Back(x0, y0));
        }

        verts.Clear();
        colors.Clear();
        uvs.Clear();
        tris.Clear();
        for (var x = 0; x < nx; x++)
        {
            Side(x, 0, x + 1, 0);
            Side(x, ny, x + 1, ny);
        }
        for (var y = 0; y < ny; y++)
        {
            Side(0, y, 0, y + 1);
            Side(nx, y, nx, y + 1);
        }
        for (var i = 0; i < nx * ny; i++)
        {
            int x = i % nx, y = i / nx, at = y * row + x;
            Vector3 a = front[at], b = front[at + 1], c = front[at + row + 1], d = front[at + row];
            if (((x + y) & 1) == 0)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }
            else
            {
                Tri(a, b, d);
                Tri(b, c, d);
            }
        }
        mesh = Flush("lbslab");
        slabs[key] = mesh;
        return mesh;
    }

    public static Mesh Round(float w, float h, float r)
    {
        r = Mathf.Max(0f, Mathf.Min(r, Mathf.Min(w, h) / 2f));
        var key = ((long)Mathf.RoundToInt(w * 8f) << 40) | ((long)Mathf.RoundToInt(h * 8f) << 20)
            | (long)Mathf.RoundToInt(r * 8f);
        if (rounds.TryGetValue(key, out var mesh) && mesh != null) return mesh;
        var points = new Vector3[25];
        var shades = new Color[25];
        var fan = new int[72];
        shades[0] = new Color(0.92f, 0.92f, 0.92f, 1f);
        for (var i = 0; i < 24; i++)
        {
            int corner = i / 6;
            var angle = (corner * 90f + i % 6 * 18f) * Mathf.Deg2Rad;
            float cx = (corner == 0 || corner == 3 ? 1f : -1f) * (w / 2f - r), cy = (corner < 2 ? 1f : -1f) * (h / 2f - r);
            points[i + 1] = new Vector3(cx + Mathf.Cos(angle) * r, cy + Mathf.Sin(angle) * r, 0f);
            var shade = Mathf.Lerp(0.82f, 1f, points[i + 1].y / h + 0.5f);
            shades[i + 1] = new Color(shade, shade, shade, 1f);
            fan[i * 3] = 0;
            fan[i * 3 + 1] = 1 + (i + 1) % 24;
            fan[i * 3 + 2] = 1 + i;
        }
        mesh = new Mesh { name = "lbround", vertices = points, colors = shades, triangles = fan };
        mesh.RecalculateBounds();
        rounds[key] = mesh;
        return mesh;
    }
}
