using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders review screenshots of the library interior into output/图书馆截图.
// Open the library scene and run the menu item, or call this from batchmode
// with -executeMethod LibraryScreenshotCapture.CaptureAll.
public static class LibraryScreenshotCapture
{
    const string ScenePath = "Assets/Interiors/Scenes/library.unity";
    const string OutDir = "output/图书馆截图";

    [MenuItem("Campus/Interiors/Capture Library Screenshots")]
    public static void CaptureAll()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("Failed to open scene: " + ScenePath);
            return;
        }

        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        var camGo = new GameObject("CaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.12f, 0.14f);
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 400f;

        // Capture lighting. The scene's own flat ambient (0.76) is tuned for in-game
        // play with tone mapping and blows every white material out in a headless
        // render, so lower it and light with a single directional key.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.11f, 0.13f);

        var fillGo = new GameObject("CaptureFill");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(1f, 0.97f, 0.92f);
        fill.intensity = 0.45f;
        fillGo.transform.rotation = Quaternion.Euler(52f, -34f, 0f);

        // Warm-up render
        var dummy = new RenderTexture(64, 64, 16);
        cam.targetTexture = dummy;
        cam.Render();
        cam.targetTexture = null;

        // Hide the two near perimeter walls and the roofs for the whole run: without
        // this most cameras are either inside a wall or looking down onto the roof.
        foreach (var n in new[] { "Wall_East", "Wall_South_West", "Wall_South_East",
                                  "Wall_South_DoorLintel", "Ceiling_Roof",
                                  "Round_Roof", "Round_Dome" })
            SetActive(FindInactive(n), false);

        // ---- 1F: entrance lobby ----
        ShowStorey(1);
        Shot(cam, new Vector3(0, 2.2f, -13.0f), Quaternion.Euler(6, 0, 0), "01_门厅全景.png");
        Shot(cam, new Vector3(0, 2.0f, -17.5f), Quaternion.Euler(4, 0, 0), "02_南门与售卖机.png");
        Shot(cam, new Vector3(0, 2.6f, -8.0f), Quaternion.Euler(8, 0, 0), "03_中央校园沙盘.png");
        Shot(cam, new Vector3(8.2f, 2.2f, -5.0f), Quaternion.Euler(10, 180, 0), "05_楼梯入口.png");
        Shot(cam, new Vector3(8.2f, 3.4f, -10.5f), Quaternion.Euler(14, 180, 0), "05b_楼梯内部.png");
        Shot(cam, new Vector3(14.0f, 2.2f, -9.0f), Quaternion.Euler(4, 180, 0), "06_电梯.png");
        Shot(cam, new Vector3(6.0f, 2.6f, -5.0f), Quaternion.Euler(10, 200, 0), "07_楼梯电梯并排.png");
        Shot(cam, new Vector3(14.0f, 2.6f, 0.0f), Quaternion.Euler(2, 90, 0), "08_校史展板.png");
        Shot(cam, new Vector3(11.0f, 2.4f, 7.5f), Quaternion.Euler(8, 0, 0), "09_卫生间.png");
        Shot(cam, new Vector3(-11.0f, 2.4f, -2.0f), Quaternion.Euler(6, 150, 0), "10_服务台.png");

        // ---- 2F ----
        ShowStorey(2);
        Shot(cam, new Vector3(6.6f, 6.0f, -13.5f), Quaternion.Euler(4, 0, 0), "20_二层楼梯口.png");
        Shot(cam, new Vector3(6.7f, 5.4f, -9.6f), Quaternion.Euler(-4, 0, 0), "20b_楼梯口落脚点.png");
        Shot(cam, new Vector3(8.0f, 6.0f, 4.5f), Quaternion.Euler(2, 0, 0), "21_二层卫生间外观.png");
        Shot(cam, new Vector3(9.9f, 5.6f, 9.2f), Quaternion.Euler(2, 288, 0), "21b_二层卫生间洗手台.png");
        Shot(cam, new Vector3(8.0f, 8.15f, 12.2f), Quaternion.Euler(52, 0, 0), "21c_二层卫生间隔间.png");
        Shot(cam, new Vector3(14.0f, 6.0f, 0.0f), Quaternion.Euler(2, 90, 0), "23_二层大门.png");
        Shot(cam, new Vector3(21.5f, 6.2f, 0.0f), Quaternion.Euler(2, 90, 0), "24_圆厅入口.png");
        Shot(cam, new Vector3(26.0f, 6.2f, 0.0f), Quaternion.Euler(2, 90, 0), "25_圆厅内部.png");
        Shot(cam, new Vector3(29.4f, 7.0f, 0.0f), Quaternion.Euler(26, 90, 0), "25b_中央镂空.png");
        Shot(cam, new Vector3(27.0f, 6.2f, -6.5f), Quaternion.Euler(4, 130, 0), "25c_书架与过道.png");
        // Looking OUT through the glass; the scene has no skybox, so tint the capture.
        cam.backgroundColor = new Color(0.53f, 0.72f, 0.90f);
        Shot(cam, new Vector3(45.0f, 6.2f, 0.0f), Quaternion.Euler(2, 90, 0), "26_圆厅玻璃幕墙.png");
        cam.backgroundColor = new Color(0.10f, 0.12f, 0.14f);
        Shot(cam, new Vector3(34.5f, 30.0f, 0.0f), Quaternion.Euler(90, 0, 0), "27_圆厅俯视.png");

        // ---- 3F and 4F: identical layout, one set of shots each ----
        for (int floor = 3; floor <= 4; floor++)
        {
            float fy = (floor - 1) * 4.3f;
            ShowStorey(floor);
            Shot(cam, new Vector3(6.6f, fy + 1.7f, -13.5f), Quaternion.Euler(4, 0, 0), floor + "0_楼梯口.png");
            Shot(cam, new Vector3(14.0f, fy + 1.7f, 0.0f), Quaternion.Euler(2, 90, 0), floor + "1_大门.png");
            Shot(cam, new Vector3(26.0f, fy + 1.9f, 0.0f), Quaternion.Euler(2, 90, 0), floor + "2_圆厅.png");
            Shot(cam, new Vector3(34.5f, fy + 26f, 0.0f), Quaternion.Euler(90, 0, 0), floor + "3_圆厅俯视.png");
        }

        // ---- cutaway: restore everything, then strip the near walls ----
        for (int floor = 1; floor <= 4; floor++) ShowStorey(floor, true);
        foreach (var n in new[] { "Ceiling_Roof", "Wall_East", "Wall_South_West", "Wall_South_East",
                                  "Wall_South_DoorLintel", "Wall_North_West", "Wall_North_East",
                                  "Wall_North_DoorLintel", "Wall_West_North", "Wall_West_South",
                                  "Wall_West_DoorLintel" })
            SetActive(FindInactive(n), false);
        Shot(cam, new Vector3(-42, 22, -42), Quaternion.Euler(26, 45, 0), "14_整馆剖视.png");

        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(fillGo);
        Object.DestroyImmediate(dummy);

        Debug.Log("LIBRARY_SCREENSHOTS_COMPLETE: written to " + Path.GetFullPath(OutDir));
    }

    // Show only one storey's contents, plus the floor slabs up to it, and hide
    // everything above so a camera can see in. With showAll, restore the lot.
    static void ShowStorey(int storey, bool showAll = false)
    {
        for (int i = 1; i <= 4; i++)
            SetActive(FindInactive("0" + i + "_Floor_" + i + "F"), showAll || i == storey);

        SetActive(FindInactive("Floor_2F_Slab"), showAll || storey >= 2);
        SetActive(FindInactive("Floor_3F_Slab"), showAll || storey >= 3);
        SetActive(FindInactive("Floor_4F_Slab"), showAll || storey >= 4);

        SetActive(FindInactive("Ceiling_Roof"), showAll);
        SetActive(FindInactive("Round_Roof"), showAll);
        SetActive(FindInactive("Round_Dome"), showAll);

        // The shown storey's emissive ceiling strips sit between a top-down camera and
        // the floor, so hide just those.
        for (int f = 1; f <= 4; f++)
            foreach (var t in FindAllInactive("Light_" + f + "F_"))
                SetActive(t, showAll || f != storey);
    }

    static void Shot(Camera cam, Vector3 pos, Quaternion rot, string fileName)
    {
        cam.transform.position = pos;
        cam.transform.rotation = rot;

        const int width = 1600, height = 900;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;

        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        File.WriteAllBytes(Path.Combine(OutDir, fileName), tex.EncodeToPNG());

        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Debug.Log("Saved screenshot: " + fileName);
    }

    // Finds an object by name even when it is currently disabled.
    static GameObject FindInactive(string name)
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t.gameObject;
            }
        }
        return null;
    }

    // All transform objects whose name starts with the given prefix.
    static System.Collections.Generic.List<GameObject> FindAllInactive(string prefix)
    {
        var found = new System.Collections.Generic.List<GameObject>();
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(prefix)) found.Add(t.gameObject);
        return found;
    }

    static void SetActive(GameObject go, bool state)
    {
        if (go) go.SetActive(state);
    }
}
