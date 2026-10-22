using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class InteriorLabelStairFix
{
    const string Output = "output/室内文字与楼梯修复";
    public static void ConfigureLabels(Transform root, int floors)
    {
        var controller = root.GetComponent<InteriorFloorTextVisibility>();
        if (!controller) controller = root.gameObject.AddComponent<InteriorFloorTextVisibility>();
        controller.floorCount = floors; controller.floorHeight = 4.3f;
        EditorUtility.SetDirty(controller);
    }

    public static void ConfigureCafeteriaRails(Transform root)
    {
        var old = root.Find("Cafeteria_Open_Stair_Rails");
        if (old) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject("Cafeteria_Open_Stair_Rails").transform;
        group.SetParent(root, false);
        var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Interiors/Materials/CafeteriaModern/Mat_Metal_Stainless.mat");
        if (!metal) throw new InvalidOperationException("Stair rail material missing.");
        foreach (var panel in root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Rail_Stair")).ToArray())
        {
            var bounds = panel.bounds;
            panel.enabled = false;
            EditorUtility.SetDirty(panel);
            bool alongX = bounds.size.x > bounds.size.z;
            float length = alongX ? bounds.size.x : bounds.size.z;
            float bottom = bounds.min.y;
            for (int level = 0; level < 2; level++)
            {
                var p = bounds.center; p.y = bottom + (level == 0 ? .55f : 1.06f);
                Bar(group, panel.name + "_Handrail_" + level, p,
                    alongX ? new Vector3(length, .045f, .045f) : new Vector3(.045f, .045f, length), metal);
            }
            int spans = Mathf.Max(1, Mathf.CeilToInt(length / 1.2f));
            for (int i = 0; i <= spans; i++)
            {
                var p = bounds.center; p.y = bottom + .55f;
                if (alongX) p.x += -length / 2 + i * length / spans;
                else p.z += -length / 2 + i * length / spans;
                Bar(group, panel.name + "_Post_" + i, p, new Vector3(.04f, 1.1f, .04f), metal);
            }
        }
    }

    static void Bar(Transform root, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.SetParent(root, false);
        go.transform.position = position; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // Preserve existing staircase collision geometry.
    }

    [MenuItem("Campus/Interiors/Fix Floor Labels and Cafeteria Stair Rails")]
    public static void ApplyAndReview()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(Output);
        foreach (string building in new[] { "cafeteria", "library" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Interiors/Scenes/" + building + ".unity", OpenSceneMode.Single);
            var root = Object.FindFirstObjectByType<InteriorSceneRoot>().transform;
            ConfigureLabels(root, building == "library" ? 4 : 3);
            if (building == "cafeteria") ConfigureCafeteriaRails(root);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Validate(root);
            Capture(root, building);
            Debug.Log("INTERIOR_LABEL_STAIR_PASS " + building);
        }
        LibraryWalkTest.Run();
        Debug.Log("INTERIOR_LABEL_STAIR_COMPLETE");
    }

    static void Validate(Transform root)
    {
        var controller = root.GetComponent<InteriorFloorTextVisibility>();
        controller.RefreshLabels();
        var texts = root.GetComponentsInChildren<TextMesh>(true);
        int checks = 0;
        // All floors, including round trips down stairs/elevators. Height uses the feet, never the camera.
        foreach (int floor in Enumerable.Range(0, controller.floorCount).Concat(Enumerable.Range(0, controller.floorCount).Reverse()))
        {
            controller.ApplyForHeight(root.position.y + floor * 4.3f + .02f);
            foreach (var text in texts)
            {
                bool expected = controller.FloorAtHeight(text.transform.position.y) != floor;
                if (text.GetComponent<Renderer>().forceRenderingOff != expected)
                    throw new InvalidOperationException("Label floor leak: " + text.name);
                checks++;
            }
        }
        controller.Restore();
        if (texts.Any(t => t.GetComponent<Renderer>().forceRenderingOff)) throw new InvalidOperationException("Label state not restored.");
        // Independent fixtures catch floor thresholds, descent, moving labels and disable restoration.
        var fixtures = Enumerable.Range(0, controller.floorCount).Select(floor =>
        {
            var go = new GameObject("Temporary label visibility fixture");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(0, floor * 4.3f + 1, 0);
            go.AddComponent<TextMesh>();
            return go;
        }).ToArray();
        controller.RefreshLabels();
        foreach (var sample in new[] { (3.8f, 0), (4.32f, 1), (8.62f, 2), (4.32f, 1), (.02f, 0) })
        {
            controller.ApplyForHeight(root.position.y + sample.Item1);
            for (int i = 0; i < fixtures.Length; i++)
                if (fixtures[i].GetComponent<Renderer>().forceRenderingOff != (i != sample.Item2))
                    throw new InvalidOperationException("Independent floor transition fixture failed.");
        }
        fixtures[0].transform.localPosition = new Vector3(0, 5.3f, 0);
        controller.ApplyForHeight(root.position.y + 4.32f);
        if (fixtures[0].GetComponent<Renderer>().forceRenderingOff) throw new InvalidOperationException("Moving cabin label did not follow its floor.");
        // Edit-mode batch checks do not dispatch runtime MonoBehaviour enable callbacks automatically.
        controller.SendMessage("OnDisable", SendMessageOptions.RequireReceiver);
        if (fixtures.Any(f => f.GetComponent<Renderer>().forceRenderingOff)) throw new InvalidOperationException("Disable did not restore labels.");
        foreach (var fixture in fixtures) Object.DestroyImmediate(fixture);
        controller.RefreshLabels();
        if (root.GetComponent<InteriorSceneRoot>().buildingId == "cafeteria")
        {
            var panels = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Rail_Stair") && !r.name.Contains("_Handrail_") && !r.name.Contains("_Post_")).ToArray();
            if (panels.Length != 12 || panels.Any(r => r.enabled)) throw new InvalidOperationException("Solid stair panels still visible.");
        }
        File.WriteAllText(Output + "/" + root.GetComponent<InteriorSceneRoot>().buildingId + "_validation.txt",
            "PASS: " + texts.Length + " labels, " + checks + " floor visibility checks; exit state restored.");
    }

    static void Capture(Transform root, string building)
    {
        var camera = new GameObject("Label stair review camera").AddComponent<Camera>();
        camera.fieldOfView = 65; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
        var data = camera.GetUniversalAdditionalCameraData();
        var presentation = root.GetComponent<InteriorPresentation>();
        if (presentation) data.SetRenderer(presentation.rendererIndex);
        data.renderPostProcessing = true;
        var controller = root.GetComponent<InteriorFloorTextVisibility>();
        for (int floor = 0; floor < controller.floorCount; floor++)
        {
            float y = floor * 4.3f;
            controller.ApplyForHeight(y + .02f);
            camera.transform.position = building == "cafeteria" ? new Vector3(1.6f, y + 1.8f, -5.8f) : new Vector3(6.8f, y + 1.8f, -8);
            camera.transform.LookAt(building == "cafeteria" ? new Vector3(6, y + 1.8f, -11.6f) : new Vector3(8, y + 1.8f, 12));
            var rt = new RenderTexture(1280, 800, 24); camera.targetTexture = rt;
            camera.Render(); camera.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + "/" + building + "_" + (floor + 1) + "F.png", texture.EncodeToPNG());
            RenderTexture.active = prev; camera.targetTexture = null;
            Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt);
        }
        controller.Restore(); Object.DestroyImmediate(camera.gameObject);
    }
}
