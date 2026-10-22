using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Clerestories for the existing three lobby floors. Keep the original solid wall
// colliders: these are fixed glazed windows, not additional exits.
public static class LibraryHallWindows
{
    const string ScenePath = "Assets/Interiors/Scenes/library.unity";
    const string GroupName = "Library_Hall_High_Windows";
    const string Materials = "Assets/Interiors/Materials/LibraryModern/";
    const string Output = "output/图书馆截图/大厅高窗";
    static readonly float[] Centers = { -16.6f, -10.25f, -4.6f };
    const float Bottom = 3.05f, Top = 4f, Width = 4f;

    static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
    }

    static Material DaylightGlass()
    {
        string path = Materials + "Mat_Lib_Clerestory_DiffusingGlass.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        // Privacy/diffusing glass intentionally hides the unloaded exterior scene.
        // Gentle emission represents daylight without exposing the dark clear color.
        material.SetColor("_BaseColor", new Color(.64f, .79f, .84f, 1));
        material.SetFloat("_Smoothness", .65f);
        material.SetFloat("_Metallic", .08f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(.30f, .40f, .45f));
        EditorUtility.SetDirty(material);
        return material;
    }

    public static void Configure(Transform root)
    {
        var shell = root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name == "00_Architecture_Shell");
        if (!shell) throw new InvalidOperationException("Library shell missing.");
        var old = root.Find(GroupName);
        if (old) Object.DestroyImmediate(old.gameObject);
        var group = Group(root, GroupName);
        var glass = DaylightGlass();
        var frame = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Mat_Lib_Metal_Titanium.mat");
        var sill = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Mat_Lib_Wood_ScandiOak.mat");
        if (!frame || !sill) throw new InvalidOperationException("Library window materials missing.");

        foreach (bool north in new[] { true, false })
        {
            string side = north ? "North" : "South";
            var original = shell.Find("Wall_" + side + "_West");
            if (!original) throw new InvalidOperationException("Original wall missing: " + side);
            var originalRenderer = original.GetComponent<MeshRenderer>();
            var wall = originalRenderer.sharedMaterial;
            // Replace only its visual mesh with bands around real openings. Leave
            // the existing transform/collider and all adjacent doorway walls intact.
            originalRenderer.enabled = false;
            var wallGroup = Group(group, side + "_Wall_With_Openings");
            float z = north ? 16f : -16f, end = north ? -2.2f : -2.4f;
            float cursorY = 0;
            for (int floor = 1; floor <= 3; floor++)
            {
                float low = (floor - 1) * 4.3f + Bottom;
                float high = (floor - 1) * 4.3f + Top;
                Box(wallGroup, floor + "F_Wall_Below", new Vector3((-20 + end) / 2, (cursorY + low) / 2, z),
                    new Vector3(end + 20, low - cursorY, .3f), wall);
                float cursorX = -20;
                foreach (float x in Centers)
                {
                    float left = x - Width / 2, right = x + Width / 2;
                    Box(wallGroup, floor + "F_Pier_" + x, new Vector3((cursorX + left) / 2, (low + high) / 2, z),
                        new Vector3(left - cursorX, high - low, .3f), wall);
                    var window = Group(group, floor + "F_" + side + "_Window_" + x);
                    window.localPosition = new Vector3(x, (low + high) / 2, z);
                    Box(window, "Diffusing_Glazing", Vector3.zero, new Vector3(Width - .12f, Top - Bottom - .12f, .045f), glass);
                    foreach (float dx in new[] { -Width / 2 + .04f, 0, Width / 2 - .04f })
                        Box(window, "Vertical_Frame", new Vector3(dx, 0, 0), new Vector3(.08f, high - low, .22f), frame);
                    foreach (float dy in new[] { -(Top - Bottom) / 2 + .04f, (Top - Bottom) / 2 - .04f })
                        Box(window, "Horizontal_Frame", new Vector3(0, dy, 0), new Vector3(Width, .08f, .22f), frame);
                    Box(window, "Oak_Sill", new Vector3(0, -(Top - Bottom) / 2 - .035f, north ? -.1f : .1f),
                        new Vector3(Width + .12f, .07f, .46f), sill);
                    cursorX = right;
                }
                Box(wallGroup, floor + "F_End_Pier", new Vector3((cursorX + end) / 2, (low + high) / 2, z),
                    new Vector3(end - cursorX, high - low, .3f), wall);
                cursorY = high;
            }
            Box(wallGroup, "Upper_Wall_Including_4F", new Vector3((-20 + end) / 2, (cursorY + 17.2f) / 2, z),
                new Vector3(end + 20, 17.2f - cursorY, .3f), wall);
        }
    }

    [MenuItem("Campus/Interiors/Add Library Hall High Windows")]
    public static void ApplyAndReview()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var root = Object.FindFirstObjectByType<InteriorSceneRoot>().transform;
        int colliders = root.GetComponentsInChildren<Collider>(true).Length;
        Configure(root);
        var group = root.Find(GroupName);
        if (group.GetComponentsInChildren<Transform>().Count(t => t.name.Contains("_Window_")) != 18 ||
            root.GetComponentsInChildren<Collider>(true).Length != colliders)
            throw new InvalidOperationException("Window count or existing collision geometry changed.");
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Capture();
        LibraryWalkTest.Run();
        Debug.Log("LIBRARY_HIGH_WINDOWS_PASS: 18 windows, 6 per floor; existing colliders preserved.");
    }

    public static void Capture()
    {
        Directory.CreateDirectory(Output);
        var camera = new GameObject("Window review camera").AddComponent<Camera>();
        camera.fieldOfView = 66; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Object.FindFirstObjectByType<InteriorSceneRoot>().backgroundColor;
        camera.allowHDR = true;
        var data = camera.GetUniversalAdditionalCameraData();
        data.SetRenderer(Object.FindFirstObjectByType<InteriorPresentation>().rendererIndex);
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var labels = Object.FindFirstObjectByType<InteriorFloorTextVisibility>();
        for (int floor = 1; floor <= 3; floor++)
        {
            float y = (floor - 1) * 4.3f;
            labels.ApplyForHeight(y);
            camera.transform.position = new Vector3(-4, y + 1.85f, -2);
            camera.transform.LookAt(new Vector3(-10, y + 2.5f, 16));
            var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGBHalf);
            camera.targetTexture = rt;
            camera.Render(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + "/" + floor + "F_大厅高窗.png", texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null;
            Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt);
        }
        labels.Restore();
        Object.DestroyImmediate(camera.gameObject);
    }
}
