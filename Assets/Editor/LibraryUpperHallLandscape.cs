using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Landscape-only update: preserve the existing floor plans and interactive objects.
public static class LibraryUpperHallLandscape
{
    const string ScenePath = "Assets/Interiors/Scenes/library.unity";
    const string AssetDir = "Assets/Interiors/LibraryInterior/UpperHallLandscape";
    const string MaterialDir = "Assets/Interiors/Materials/LibraryModern/Mat_Lib_";
    const string OutputDir = "output/图书馆截图/二三楼景观";
    static Material oak, metal, sage, cream, leaf, soil, paper;

    static Material Existing(string name) => AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + name + ".mat");
    static Material Material(string name, Color color, float smoothness)
    {
        var path = AssetDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat) return mat;
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void LoadAssets()
    {
        Directory.CreateDirectory(AssetDir);
        AssetDatabase.Refresh();
        oak = Existing("Wood_ScandiOak");
        metal = Existing("Metal_MatteBlack");
        sage = Existing("Chair_Sage");
        cream = Material("WarmCeramic", new Color(.70f, .69f, .62f), .3f);
        leaf = Material("LivingLeaves", new Color(.16f, .30f, .12f), .25f);
        leaf.SetFloat("_Cull", 0); // Two-sided leaves without cancelling their vertex normals.
        EditorUtility.SetDirty(leaf);
        soil = Material("PlantingSoil", new Color(.10f, .075f, .04f), .05f);
        paper = Material("WarmPaper", new Color(.88f, .85f, .74f), .15f);
        if (!oak || !metal || !sage) throw new InvalidOperationException("Missing library furniture materials.");
    }

    static Transform Group(Transform parent, string name, Vector3 position)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = position;
        return t;
    }

    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat,
        bool solid = false, float radius = .04f)
    {
        string key = string.Format(CultureInfo.InvariantCulture, "Round_{0:F3}_{1:F3}_{2:F3}_{3:F3}", size.x, size.y, size.z, radius);
        string path = AssetDir + "/" + key + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        GameObject go;
        if (!mesh)
        {
            go = InteriorGeometry.RoundedBox(parent, name, Vector3.zero, size, radius, mat);
            mesh = go.GetComponent<MeshFilter>().sharedMesh;
            mesh.name = key;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < uv.Length; i++)
            {
                var p = vertices[i]; var n = normals[i];
                uv[i] = Mathf.Abs(n.y) > .5f ? new Vector2(p.x, p.z) :
                    Mathf.Abs(n.x) > .5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
            }
            mesh.uv = uv;
            AssetDatabase.CreateAsset(mesh, path);
        }
        else
        {
            go = Group(parent, name, Vector3.zero).gameObject;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        go.transform.localPosition = pos;
        if (solid) go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    static void Cylinder(Transform parent, string name, Vector3 p, float radius, float height, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = p;
        go.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    // One shared mesh per plant variant, with curved double-sided leaves instead of spheres.
    static Mesh Foliage(int variant)
    {
        string path = AssetDir + "/Foliage_" + variant + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh)
        {
            // Migrate the first generated mesh, which shared vertices between reversed faces.
            if (mesh.triangles.Length == 42 * 6 * 12)
            {
                var old = mesh.triangles;
                mesh.triangles = Enumerable.Range(0, old.Length / 12).SelectMany(i => old.Skip(i * 12).Take(6)).ToArray();
                mesh.RecalculateNormals(); EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }
        var v = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
        var random = new System.Random(713 + variant);
        for (int k = 0; k < 42; k++)
        {
            float a = k * 2.39996f;
            var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var side = Vector3.Cross(Vector3.up, outward);
            float h = .15f + (float)random.NextDouble() * 1.15f;
            float length = .35f + (float)random.NextDouble() * .5f;
            float width = .09f + (float)random.NextDouble() * .09f;
            int first = v.Count;
            for (int segment = 0; segment <= 6; segment++)
            {
                float t = segment / 6f;
                Vector3 center = Vector3.up * h + outward * (t * length) + Vector3.up * (.4f * Mathf.Sin(t * Mathf.PI) - .1f * t);
                float w = Mathf.Sin(t * Mathf.PI) * width;
                v.Add(center - side * w); v.Add(center + side * w);
                uv.Add(new Vector2(0, t)); uv.Add(new Vector2(1, t));
                if (segment == 6) continue;
                int i = first + segment * 2;
                triangles.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
            }
        }
        mesh = new Mesh { name = "Landscape foliage " + variant };
        mesh.SetVertices(v); mesh.SetTriangles(triangles, 0); mesh.SetUVs(0, uv);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static void Plant(Transform parent, Vector3 position, float scale, int variant)
    {
        var t = Group(parent, "Leafy planting", position);
        t.localScale = Vector3.one * scale;
        t.localRotation = Quaternion.Euler(0, variant * 61, 0);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh = Foliage(variant % 3);
        t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = leaf;
        Cylinder(t, "Stem", new Vector3(0, .65f, 0), .022f, 1.3f, oak);
    }

    static void Pot(Transform parent, float x, float z, float scale = 1)
    {
        var t = Group(parent, "Ceramic planter", new Vector3(x, 0, z));
        Box(t, "Pot", new Vector3(0, .3f, 0), new Vector3(.7f, .6f, .7f), cream, true, .12f);
        Box(t, "Soil", new Vector3(0, .605f, 0), new Vector3(.59f, .025f, .59f), soil);
        Plant(t, new Vector3(0, .62f, 0), scale, Mathf.RoundToInt(z + 30));
    }

    static void Planter(Transform parent, float z, float length)
    {
        var t = Group(parent, "West wall planted border", new Vector3(-19.05f, 0, z));
        Box(t, "Oak planter", new Vector3(0, .35f, 0), new Vector3(1.05f, .7f, length), oak, true);
        Box(t, "Inset soil", new Vector3(0, .705f, 0), new Vector3(.89f, .025f, length - .16f), soil);
        int count = Mathf.FloorToInt(length / 1.1f);
        for (int i = 0; i < count; i++)
            Plant(t, new Vector3(0, .72f, -length / 2 + .6f + i * 1.1f), .65f + .2f * (i % 3), i);
    }

    static void Chair(Transform parent, Vector3 p, float yaw)
    {
        var t = Group(parent, "Sage reading armchair", p);
        t.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(t, "Seat cushion", new Vector3(0, .45f, 0), new Vector3(.82f, .22f, .79f), sage, false, .1f);
        Box(t, "Back cushion", new Vector3(0, .78f, -.35f), new Vector3(.82f, .65f, .18f), sage, false, .08f);
        foreach (float x in new[] { -.44f, .44f })
        {
            Box(t, "Upholstered arm", new Vector3(x, .65f, 0), new Vector3(.15f, .32f, .85f), sage);
            foreach (float z in new[] { -.28f, .28f })
                Box(t, "Oak foot", new Vector3(x * .8f, .15f, z), new Vector3(.07f, .3f, .07f), oak);
        }
        var collider = t.gameObject.AddComponent<BoxCollider>();
        collider.center = new Vector3(0, .54f, 0); collider.size = new Vector3(1.03f, 1.08f, .88f);
    }

    static void ReadingCorner(Transform parent, float z)
    {
        var t = Group(parent, "Two-seat reading corner", new Vector3(-16.45f, 0, z));
        Chair(t, new Vector3(0, 0, -1.05f), 0);
        Chair(t, new Vector3(0, 0, 1.05f), 180);
        Cylinder(t, "Round oak table", new Vector3(0, .56f, 0), .43f, .07f, oak);
        Cylinder(t, "Table pedestal", new Vector3(0, .28f, 0), .07f, .5f, metal);
        Cylinder(t, "Table base", new Vector3(0, .035f, 0), .3f, .07f, metal);
        var c = Group(t, "Table collision", new Vector3(0, .3f, 0)).gameObject.AddComponent<BoxCollider>();
        c.size = new Vector3(.86f, .6f, .86f);
        Box(t, "Book cover", new Vector3(.02f, .612f, 0), new Vector3(.24f, .025f, .31f), sage);
        Box(t, "Book pages", new Vector3(.02f, .634f, 0), new Vector3(.22f, .025f, .29f), paper);
    }

    static void Display(Transform parent)
    {
        var t = Group(parent, "Reading display", new Vector3(-19.43f, 0, 1.4f));
        Box(t, "Oak back", new Vector3(0, 1.1f, 0), new Vector3(.12f, 2.2f, 2.4f), oak);
        for (int shelf = 0; shelf < 3; shelf++)
        {
            float y = .3f + shelf * .6f;
            Box(t, "Display shelf", new Vector3(.22f, y, 0), new Vector3(.55f, .06f, 2.4f), oak, true);
            for (int book = 0; book < 6; book++)
                Box(t, "Displayed volume", new Vector3(.19f, y + .2f, -.95f + book * .38f),
                    new Vector3(.09f, .34f, .25f), book % 2 == 0 ? sage : paper);
        }
    }

    static void Art(Transform parent, float z, string caption)
    {
        var t = Group(parent, "Botanical wall panel", new Vector3(-19.67f, 2.55f, z));
        Box(t, "Oak frame", Vector3.zero, new Vector3(.09f, 1.15f, 1.7f), oak);
        Box(t, "Linen panel", new Vector3(.055f, 0, 0), new Vector3(.025f, 1.04f, 1.58f), paper);
        t.name = "Botanical artwork · " + caption;
        string path = AssetDir + "/BotanicalPrintLeaf.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!mesh)
        {
            var vertices = new List<Vector3> { Vector3.zero };
            var indices = new List<int>();
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2 / 24;
                vertices.Add(new Vector3(0, .17f * Mathf.Cos(a), .065f * Mathf.Sin(a)));
                indices.AddRange(new[] { 0, i + 1, (i + 1) % 24 + 1 });
            }
            mesh = new Mesh { name = "Botanical print leaf" };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
        }
        else if (mesh.triangles.Length == 144)
        {
            var old = mesh.triangles;
            mesh.triangles = Enumerable.Range(0, 24).SelectMany(i => old.Skip(i * 6).Take(3)).ToArray();
            mesh.RecalculateNormals(); EditorUtility.SetDirty(mesh);
        }
        foreach (float branch in new[] { -.4f, .35f })
        {
            Box(t, "Printed stem", new Vector3(.075f, 0, branch), new Vector3(.008f, .76f, .012f), leaf, false, .001f);
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? 1 : -1;
                var l = Group(t, "Printed leaf", new Vector3(.082f, -.27f + i * .105f, branch + side * .1f));
                l.localRotation = Quaternion.Euler(side * 48, 0, 0);
                l.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                l.gameObject.AddComponent<MeshRenderer>().sharedMaterial = leaf;
            }
        }
    }

    static void LandscapeLights(Transform parent)
    {
        var lampMaterial = Material("LandscapeLightDiffuser", new Color(.96f, .94f, .87f), .2f);
        lampMaterial.EnableKeyword("_EMISSION");
        lampMaterial.SetColor("_EmissionColor", new Color(1.2f, 1.12f, .98f));
        EditorUtility.SetDirty(lampMaterial);
        foreach (float z in new[] { -11.2f, -6f, -.8f })
        {
            var t = Group(parent, "Reading corner ceiling light", new Vector3(-16.8f, 3.92f, z));
            Box(t, "Light housing", Vector3.zero, new Vector3(1.5f, .06f, .18f), cream);
            Box(t, "Diffuser", new Vector3(0, -.04f, 0), new Vector3(1.4f, .025f, .13f), lampMaterial);
            var source = Group(t, "Warm downward light", new Vector3(0, -.12f, 0));
            source.localRotation = Quaternion.Euler(90, 0, 0);
            var light = source.gameObject.AddComponent<Light>();
            light.type = LightType.Spot; light.range = 7; light.spotAngle = 115;
            light.innerSpotAngle = 85; light.intensity = 14;
            light.color = new Color(1, .96f, .88f);
            light.shadows = LightShadows.Soft; light.shadowStrength = .65f;
            light.shadowBias = .025f; light.shadowNormalBias = .15f;
        }
    }

    public static void Build(Transform floorRoot, float baseY, int floor)
    {
        if (floor != 2 && floor != 3) return;
        LoadAssets();
        string name = floor + "F_Hall_Landscape";
        var old = floorRoot.Find(name);
        if (old) Object.DestroyImmediate(old.gameObject);
        var root = Group(floorRoot, name, new Vector3(0, baseY, 0));
        if (floor == 2)
        {
            Planter(root, -8.8f, 8.4f);
            ReadingCorner(root, -10.8f); ReadingCorner(root, -4.5f);
            Art(root, -9.5f, "阅读 · 自然"); Art(root, -5.5f, "READ & GROW");
        }
        else
        {
            Planter(root, -8.1f, 9f);
            var bench = Group(root, "Planted reading bench", new Vector3(-18.08f, 0, -8.1f));
            Box(bench, "Oak seat", new Vector3(0, .45f, 0), new Vector3(.8f, .16f, 8.6f), oak, true);
            for (int i = 0; i < 4; i++)
            {
                float z = -3.15f + i * 2.1f;
                Box(bench, "Bench support", new Vector3(0, .2f, z), new Vector3(.65f, .4f, .18f), metal, true);
                Box(bench, "Sage seat pad", new Vector3(0, .56f, z), new Vector3(.65f, .1f, 1.55f), sage);
            }
            ReadingCorner(root, -.8f);
            Art(root, -10.1f, "静心阅读"); Art(root, -6.1f, "QUIET READING");
        }
        Display(root);
        Pot(root, -18.7f, 11, 1.1f);
        Pot(root, 18.6f, 12.3f, .95f);
        LandscapeLights(root);
    }

    static Transform Floor(int floor) => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
        FindObjectsSortMode.None).Single(t => t.name == "0" + floor + "_Floor_" + floor + "F");

    [MenuItem("Campus/Interiors/Apply 2F and 3F Hall Landscape")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (int floor in new[] { 2, 3 }) Build(Floor(floor), (floor - 1) * 4.3f, floor);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("UPPER_HALL_LANDSCAPE_SAVED");
    }

    public static void ApplyAndReview()
    {
        Apply();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Physics.SyncTransforms();
        foreach (int floor in new[] { 2, 3 })
        {
            var root = Floor(floor).Find(floor + "F_Hall_Landscape");
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                if (!filter.sharedMesh || !AssetDatabase.Contains(filter.sharedMesh))
                    throw new InvalidOperationException("Nonpersistent landscape mesh: " + filter.name);
            // The hall centre, door approaches and west-corridor turn remain free of new props.
            var route = new[] { new Vector3(6.8f, 0, -9.6f), new Vector3(6.8f, 0, 0),
                new Vector3(-8, 0, 6), new Vector3(-19, 0, 6), new Vector3(-27, 0, 6),
                new Vector3(-8, 0, 6), new Vector3(14.2f, 0, 8.4f), new Vector3(14.2f, 0, 13.6f) };
            for (int i = 1; i < route.Length; i++)
                for (float t = 0; t <= 1; t += .025f)
                {
                    var p = Vector3.Lerp(route[i - 1], route[i], t) + Vector3.up * ((floor - 1) * 4.3f + .85f);
                    foreach (var hit in Physics.OverlapCapsule(p, p + Vector3.up * .6f, .8f))
                        if (hit.transform.IsChildOf(root)) throw new InvalidOperationException("Landscape blocks route: " + hit.name);
                }
            Debug.Log("LANDSCAPE_QA_PASS " + floor + "F, persistent meshes and main routes clear");
        }
        Capture();
        LibraryWalkTest.Run();
    }

    public static void Capture()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(OutputDir);
        var camera = new GameObject("Landscape review camera").AddComponent<Camera>();
        camera.fieldOfView = 67; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
        var data = camera.GetUniversalAdditionalCameraData();
        var presentation = Object.FindFirstObjectByType<InteriorPresentation>();
        if (presentation) data.SetRenderer(presentation.rendererIndex);
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        foreach (int floor in new[] { 2, 3 })
        {
            float y = (floor - 1) * 4.3f;
            // Existing legacy labels use a GUI shader: hide other storeys' labels only in review renders.
            foreach (var text in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                text.GetComponent<Renderer>().enabled = text.transform.position.y >= y && text.transform.position.y < y + 4.3f;
            camera.transform.position = new Vector3(-13.9f, y + 2.3f, -14.2f);
            camera.transform.LookAt(new Vector3(-18, y + 1.2f, -5.7f));
            var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            File.WriteAllBytes(OutputDir + "/" + floor + "F_景观实景.png", image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null;
            Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt);
        }
        Object.DestroyImmediate(camera.gameObject);
        Debug.Log("LANDSCAPE_CAPTURE_DONE");
    }
}
