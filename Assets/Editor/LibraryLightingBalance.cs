using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Library-only lighting, applied to the existing scene and after full regeneration.
public static class LibraryLightingBalance
{
    const string ScenePath = "Assets/Interiors/Scenes/library.unity";
    const string RendererPath = "Assets/Interiors/LibraryInterior/LibraryRenderer.asset";
    const string Output = "output/图书馆截图/亮度调整";

    static int RendererIndex()
    {
        if (!AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath))
        {
            if (!AssetDatabase.CopyAsset(InteriorPresentationSetup.InteriorRendererPath, RendererPath))
                throw new InvalidOperationException("Cannot copy the library renderer.");
        }
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        var settings = new SerializedObject(renderer);
        // Forward+ removes Forward's four-additional-lights-per-object limit on large floor meshes.
        settings.FindProperty("m_RenderingMode").intValue = (int)RenderingMode.ForwardPlus;
        settings.ApplyModifiedPropertiesWithoutUndo();
        renderer.SetDirty(); EditorUtility.SetDirty(renderer);

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/CampusURP.asset");
        var serialized = new SerializedObject(pipeline);
        var list = serialized.FindProperty("m_RendererDataList");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) return i;
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = renderer;
        serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline);
        return index;
    }

    static void LightAt(Transform parent, string name, Vector3 position, float intensity, float range, bool down)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false); go.transform.localPosition = position;
        var light = go.AddComponent<Light>();
        light.type = down ? LightType.Spot : LightType.Point;
        light.color = new Color(1f, .975f, .93f);
        light.intensity = intensity; light.range = range;
        // Diffuse ceiling fill: no extra realtime shadow maps for dozens of lamps.
        light.shadows = LightShadows.None;
        if (down)
        {
            go.transform.localRotation = Quaternion.Euler(90, 0, 0);
            light.spotAngle = 125; light.innerSpotAngle = 95;
        }
    }

    public static void Configure(Transform root)
    {
        var presentation = root.GetComponent<InteriorPresentation>();
        if (!presentation) throw new InvalidOperationException("Library presentation component missing.");
        presentation.rendererIndex = RendererIndex();
        presentation.exteriorRendererIndex = -1;
        EditorUtility.SetDirty(presentation);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.66f, .68f, .71f);
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Interiors/LibraryInterior/InteriorVolume.asset");
        if (profile && profile.TryGet<ColorAdjustments>(out var grade))
        {
            grade.postExposure.Override(.55f);
            grade.contrast.Override(3);
            EditorUtility.SetDirty(grade); EditorUtility.SetDirty(profile);
        }

        foreach (var light in root.GetComponentsInChildren<Light>(true))
        {
            if (light.name == "Main_Interior_Sun") RenderSettings.sun = light;
            else if (light.name.Contains("_Round_Wash_")) light.intensity = 6f;
            else if (light.name.Contains("_Hall_Fill_")) light.intensity = 5f;
            else if (light.name.Contains("Corridor") && light.name.Contains("_Point_")) light.intensity = 6f;
            else if (light.name == "1F_Bed_Light") light.intensity = 4f;
        }

        var old = root.Find("Library_Balanced_Lighting");
        if (old) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject("Library_Balanced_Lighting").transform;
        group.SetParent(root, false);
        // Broad shadowless bounce lights supply the diffuse fill missing in this unbaked scene.
        // The existing sun and SSAO retain contact shadows; local lights keep furniture shaped.
        foreach (var bounce in new[] { ("Soft ceiling bounce", 60f, -25f, 1.0f), ("Soft floor bounce", -65f, 135f, .8f) })
        {
            var go = new GameObject(bounce.Item1);
            go.transform.SetParent(group, false);
            go.transform.localRotation = Quaternion.Euler(bounce.Item2, bounce.Item3, 0);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional; light.shadows = LightShadows.None;
            light.color = new Color(1, .98f, .95f); light.intensity = bounce.Item4;
        }
        for (int floor = 1; floor <= 4; floor++)
        {
            float y = (floor - 1) * 4.3f;
            foreach (float x in new[] { -16f, -8f, 0f, 8f, 16f })
                foreach (float z in new[] { -12f, -4f, 4f, 12f })
                {
                    if (x == 8 && z == -12) continue; // Stairwell opening.
                    LightAt(group, floor + "F ceiling fill " + x + "," + z,
                        new Vector3(x, y + 3.65f, z), 18, 8, true);
                }
            foreach (float x in new[] { -12f, 8f })
                foreach (float z in new[] { -8f, 8f })
                    LightAt(group, floor + "F bounced fill " + x + "," + z,
                        new Vector3(x, y + 2.5f, z), 4, 8, false);
            if (floor == 2 || floor == 3)
                foreach (float x in new[] { -23f, -29f, -35f })
                    LightAt(group, floor + "F corridor fill " + x,
                        new Vector3(x, y + 2.9f, 6), 5, 6, false);
        }
    }

    [MenuItem("Campus/Interiors/Brighten Library Interior")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Configure(Object.FindFirstObjectByType<InteriorSceneRoot>().transform);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("LIBRARY_LIGHTING_SAVED");
    }

    public static void ApplyAndReview()
    {
        if (!File.Exists(Output + "/2F景观_before.png")) Capture("before");
        Apply();
        Capture("after");
        // Reload to check saved configuration rather than temporary capture state.
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var presentation = Object.FindFirstObjectByType<InteriorPresentation>();
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer.renderingMode != RenderingMode.ForwardPlus || presentation.rendererIndex < 0)
            throw new InvalidOperationException("Library renderer configuration did not persist.");
        Debug.Log("LIBRARY_LIGHTING_REVIEW_PASS");
    }

    public static void Capture(string suffix)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(Output);
        var camera = new GameObject("Lighting review camera").AddComponent<Camera>();
        camera.fieldOfView = 67; camera.nearClipPlane = .1f; camera.farClipPlane = 110;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
        camera.allowHDR = true;
        var data = camera.GetUniversalAdditionalCameraData();
        data.SetRenderer(Object.FindFirstObjectByType<InteriorPresentation>().rendererIndex);
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        var shots = new[] {
            ("1F大厅", new Vector3(-6, 1.8f, -12), new Vector3(0, 1.6f, 5), 1),
            ("2F景观", new Vector3(-13.9f, 6.6f, -14.2f), new Vector3(-18, 5.5f, -5.7f), 2),
            ("3F景观", new Vector3(-13.9f, 10.9f, -14.2f), new Vector3(-18, 9.8f, -5.7f), 3),
            ("2F走廊", new Vector3(-21, 6, 6), new Vector3(-36, 6, 6), 2),
            ("2F阅览区", new Vector3(22, 6, -1), new Vector3(34.5f, 5.8f, 8), 2)
        };
        foreach (var shot in shots)
        {
            camera.transform.position = shot.Item2; camera.transform.LookAt(shot.Item3);
            float y = (shot.Item4 - 1) * 4.3f;
            foreach (var text in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                text.GetComponent<Renderer>().enabled = text.transform.position.y >= y && text.transform.position.y < y + 4.3f;
            var rt = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGBHalf);
            camera.targetTexture = rt;
            camera.Render(); camera.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + "/" + shot.Item1 + "_" + suffix + ".png", texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null;
            Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt);
        }
        Object.DestroyImmediate(camera.gameObject);
        Debug.Log("LIBRARY_LIGHTING_CAPTURE_" + suffix);
    }
}
