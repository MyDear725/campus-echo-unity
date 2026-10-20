using System.Collections.Generic;
using UnityEngine;

// Rounded-box and tube geometry, ported from the teaching-interior builder so the
// cafeteria and library can put real bevels on the furniture the player walks up to.
//
// Each helper builds one mesh in local space and returns the GameObject, so callers
// keep using their existing MakeBox-style code but get a bevelled, smooth-shaded
// result instead of a hard cube.
public static class InteriorGeometry
{
    // Rounded box: 6 faces subdivided 3x3 with a radius, then normal-smoothed across
    // the bevel only, so the silhouette is soft but the flat faces stay flat.
    public static GameObject RoundedBox(Transform parent, string name, Vector3 center, Vector3 size,
                                        float radius, Material mat, bool collider = false)
    {
        var h = size * 0.5f;
        float r = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.8f);
        var inner = h - Vector3.one * r;

        Vector3 Round(Vector3 p)
        {
            var q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x),
                                Mathf.Clamp(p.y, -inner.y, inner.y),
                                Mathf.Clamp(p.z, -inner.z, inner.z));
            return center + q + (p - q).normalized * r;
        }

        float[] Corners(float e) => new[] { -e, -e + r, e - r, e };

        var verts = new List<Vector3>();
        var tris = new List<int>();

        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int i = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        for (int axis = 0; axis < 3; axis++)
            foreach (float sign in new[] { -1f, 1f })
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                var a = Corners(h[u]); var b = Corners(h[v]);

                Vector3 P(float s, float t)
                {
                    var p = Vector3.zero;
                    p[axis] = h[axis] * sign;
                    p[u] = s; p[v] = t;
                    return Round(p);
                }

                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                    {
                        var p0 = P(a[i], b[j]); var p1 = P(a[i + 1], b[j]);
                        var p2 = P(a[i + 1], b[j + 1]); var p3 = P(a[i], b[j + 1]);
                        if (sign > 0) Quad(p0, p1, p2, p3); else Quad(p3, p2, p1, p0);
                    }
            }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        SmoothBevels(mesh);
        mesh.RecalculateBounds();

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (mat) mr.sharedMaterial = mat;
        if (!collider) return go;

        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
        mc.convex = true;
        return go;
    }

    // Welds normals only where the angle is gentle, keeping 90-degree corners crisp.
    static void SmoothBevels(Mesh mesh)
    {
        var vertices = mesh.vertices;
        var normals = mesh.normals;
        var output = new Vector3[normals.Length];
        var groups = new Dictionary<Vector3Int, List<int>>();

        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i];
            var key = new Vector3Int(Mathf.RoundToInt(p.x * 10000f),
                                     Mathf.RoundToInt(p.y * 10000f),
                                     Mathf.RoundToInt(p.z * 10000f));
            if (!groups.TryGetValue(key, out var ids)) { ids = new List<int>(); groups.Add(key, ids); }
            ids.Add(i);
        }

        foreach (var ids in groups.Values)
            foreach (var i in ids)
            {
                var sum = Vector3.zero;
                foreach (var j in ids)
                    if (Vector3.Dot(normals[i], normals[j]) > 0.55f) sum += normals[j];
                output[i] = sum.normalized;
            }

        mesh.normals = output;
    }

    // Round tube between two points, for legs, rails and stems.
    public static GameObject Tube(Transform parent, string name, Vector3 a, Vector3 b, float radius,
                                  Material mat, int segments = 10)
    {
        var axis = (b - a).normalized;
        var u = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
        var v = Vector3.Cross(axis, u);

        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i < segments; i++)
        {
            float t = i * 2f * Mathf.PI / segments, s = (i + 1) * 2f * Mathf.PI / segments;
            var p = (u * Mathf.Cos(t) + v * Mathf.Sin(t)) * radius;
            var q = (u * Mathf.Cos(s) + v * Mathf.Sin(s)) * radius;

            int idx = verts.Count;
            verts.Add(a + p); verts.Add(a + q); verts.Add(b + q); verts.Add(b + p);
            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        if (mat) mr.sharedMaterial = mat;
        return go;
    }
}
