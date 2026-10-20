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

    // Batch entry: renders the labelled 2F aerial on its own.
    // Batch entry: the view a player has the moment they reach the 2F stair head and
    // face the hall. Used to confirm the west corridor is not visible from there.
    public static void RenderStairViewBatch() => RenderStairView();

    static void RenderStairView()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError("Failed to open scene"); return; }
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        var camGo = new GameObject("CaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.12f, 0.14f);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.2f; cam.farClipPlane = 400f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.30f, 0.31f, 0.33f);
        var fill = new GameObject("CaptureFill").AddComponent<Light>();
        fill.type = LightType.Directional; fill.color = Color.white; fill.intensity = 0.55f;
        fill.transform.rotation = Quaternion.Euler(52f, -34f, 0f);
        fill.intensity = 0f;   // kept for API compatibility, scene sun does the work
        ShowStorey(2);

        Shot(cam, new Vector3(6.8f, 5.95f, -9.2f), Quaternion.Euler(1f, 0f, 0f), "19f_楼梯口望向前方.png");
        Debug.Log("STAIRVIEW_DONE");
    }

    public static void RenderPlan2FBatch() => RenderPlan2F(labelled: true);

    // Diagnostic: four rotations side by side, lifted clear of all geometry, so the
    // correct label orientation can be read off a single render.
    public static void RenderLabelProbe() => RenderPlanProbe();

    [MenuItem("Campus/Interiors/Capture 2F Plan Only (labelled)")]
    public static void CapturePlanOnly() => RenderPlan2F(labelled: true);

    // Renders just the 2F aerial, optionally with the Chinese plan labels. Kept separate
    // from CaptureAll so the labelled reference can be iterated on in one shot.
    static void RenderPlanProbe()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError("Failed to open scene"); return; }
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        var camGo = new GameObject("CaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.12f, 0.14f);
        cam.nearClipPlane = 0.2f; cam.farClipPlane = 400f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.34f, 0.34f, 0.36f);
        var fill = new GameObject("CaptureFill").AddComponent<Light>();
        fill.type = LightType.Directional; fill.color = Color.white; fill.intensity = 0.62f;
        fill.transform.rotation = Quaternion.Euler(52f, -34f, 0f);
        ShowStorey(2);

        var ink = Color.white;
        var plate = new Color(0.13f, 0.14f, 0.16f);
        // Lifted to Y = 12 so nothing in the scene can occlude them.
        PlanLabel("A 大厅 90",   new Vector3(-12f, 14f, 8f),  Quaternion.Euler(90f, 0f, 0f),    0.10f, ink, plate);
        PlanLabel("B 大厅 -90",  new Vector3(-4f,  14f, 8f),  Quaternion.Euler(-90f, 0f, 0f),   0.10f, ink, plate);
        PlanLabel("C 大厅 90/180", new Vector3(4f, 14f, 8f),  Quaternion.Euler(90f, 0f, 180f),  0.10f, ink, plate);
        PlanLabel("D 大厅 -90/180", new Vector3(12f, 14f, 8f), Quaternion.Euler(-90f, 0f, 180f), 0.10f, ink, plate);

        ShotPlan(cam, 40f, 18.4f, "98_标签探针.png");
        ClearPlanTemps();
        Debug.Log("LABEL_PROBE_DONE");
    }

    public static void RenderPlan2F(bool labelled)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid()) { Debug.LogError("Failed to open scene: " + ScenePath); return; }
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

        var camGo = new GameObject("CaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.12f, 0.14f);
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 400f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.17f, 0.18f, 0.20f);

        var fill = new GameObject("CaptureFill").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(1f, 0.97f, 0.92f);
        fill.intensity = 0.52f;
        fill.transform.rotation = Quaternion.Euler(52f, -34f, 0f);

        foreach (var n in new[] { "Ceiling_Roof", "Round_Roof", "Round_Dome" })
            SetActive(FindInactive(n), false);

        ShowStorey(2);

        var dummy = new RenderTexture(64, 64, 16);
        cam.targetTexture = dummy; cam.Render(); cam.targetTexture = null;

        if (labelled)
        {
            // Labels sit above every wall (18.6 m) and over the pale granite, so they are
            // never hidden and always read against the floor.
            var rot = Quaternion.Euler(90f, 0f, 0f);
            var ink = new Color(0.09f, 0.10f, 0.12f);

            PlanLabel("二层大厅",         new Vector3(0f, 18.6f, 1.5f),    rot, 0.115f, ink, ink);
            PlanLabel("二层楼梯口",       new Vector3(8.2f, 18.6f, -5.2f), rot, 0.085f, ink, ink);
            PlanLabel("电梯（原位保留）", new Vector3(14.2f, 18.6f, -10.4f), rot, 0.066f, ink, ink);
            PlanLabel("饮水机",           new Vector3(12.6f, 18.6f, 13.2f), rot, 0.085f, ink, ink);
            PlanLabel("卫生间",           new Vector3(7.4f, 18.6f, 6.6f),  rot, 0.085f, ink, ink);
            PlanLabel("左转进入走廊",     new Vector3(-14.6f, 18.6f, 6.0f), rot, 0.085f, ink, ink);
            PlanLabel("后门所在侧墙",     new Vector3(0f, 18.6f, -15.0f),  rot, 0.095f, ink, ink);
        }

        ShotPlan(cam, 40f, 18.4f, labelled ? "19_二层俯瞰_标注.png" : "19_二层俯瞰.png");
        ClearPlanTemps();

        UnityEngine.Object.DestroyImmediate(camGo);
        UnityEngine.Object.DestroyImmediate(fill.gameObject);
        Debug.Log("PLAN_2F_DONE labelled=" + labelled);
    }

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
        // The scene now carries its own balanced sun + ambient, so the capture leaves
        // them alone; adding a key light here would misrepresent the in-game look.
        var fillGo = new GameObject("CaptureFill");
        var fill = fillGo.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(1f, 0.97f, 0.92f);
        fill.intensity = 0.62f;
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

        // Plan view, north up: the camera looks straight down, so screen-up is +Z
        // (north) and screen-right is +X (east), matching the reference orientation.
        ShotPlan(cam, 40f, 17.5f, "01b_一层俯瞰.png");

        // The reworked 1F corners.
        Shot(cam, new Vector3(15.0f, 1.7f, -2.2f), Quaternion.Euler(8, 150, 0), "11_东南花坛.png");
        Shot(cam, new Vector3(6.0f, 1.7f, 7.0f), Quaternion.Euler(5, 18, 0), "12_东北休息区.png");
        Shot(cam, new Vector3(13.2f, 1.7f, 1.2f), Quaternion.Euler(4, 22, 0), "13_卫生间入口.png");
        Shot(cam, new Vector3(8.6f, 1.7f, 10.5f), Quaternion.Euler(4, 62, 0), "14_朗读亭.png");

        // ---- 2F ----
        ShowStorey(2);
        // Plan view of 2F with the roof cut away, following the reference layout.
        Shot(cam, new Vector3(12.2f, 5.05f, 11.6f), Quaternion.Euler(4f, 13.2f, 0), "19b_二层饮水机.png");
        // The upper-floor reading desks, seen from the hall.
        Shot(cam, new Vector3(-2.0f, 5.95f, 3.5f), Quaternion.Euler(4f, 250f, 0), "19g_二层阅览区.png");

        // The new 2F west corridor.
        Shot(cam, new Vector3(-13.5f, 5.95f, 6.0f), Quaternion.Euler(2f, 270f, 0), "19c_走廊入口.png");
        Shot(cam, new Vector3(-24.0f, 5.95f, 6.0f), Quaternion.Euler(2f, 270f, 0), "19d_走廊内部.png");
        Shot(cam, new Vector3(-33.0f, 5.95f, 6.0f), Quaternion.Euler(2f, 90f, 0),  "19e_走廊回望.png");
        Shot(cam, new Vector3(6.6f, 6.0f, -13.5f), Quaternion.Euler(4, 0, 0), "20_二层楼梯口.png");
        Shot(cam, new Vector3(6.7f, 5.4f, -9.6f), Quaternion.Euler(-4, 0, 0), "20b_楼梯口落脚点.png");
        Shot(cam, new Vector3(8.0f, 6.0f, 4.5f), Quaternion.Euler(2, 0, 0), "21_二层卫生间外观.png");
        Shot(cam, new Vector3(9.9f, 5.6f, 9.2f), Quaternion.Euler(2, 288, 0), "21b_二层卫生间洗手台.png");
        Shot(cam, new Vector3(8.0f, 8.15f, 12.2f), Quaternion.Euler(52, 0, 0), "21c_二层卫生间隔间.png");
        Shot(cam, new Vector3(14.0f, 6.0f, 0.0f), Quaternion.Euler(2, 90, 0), "23_二层大门.png");
        Shot(cam, new Vector3(21.5f, 6.2f, 0.0f), Quaternion.Euler(2, 90, 0), "24_圆厅入口.png");
        // Interior views need the roof above them, otherwise the shot shows empty sky.
        SetActive(FindInactive("Round_Roof"), true);
        SetActive(FindInactive("Round_Dome"), true);
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

            // 3F has the same west corridor as 2F; render it while this storey is shown.
            if (floor == 3)
            {
                Shot(cam, new Vector3(-13.5f, fy + 1.65f, 6.0f), Quaternion.Euler(2, 270, 0), "29c_三层走廊入口.png");
                Shot(cam, new Vector3(-24.0f, fy + 1.65f, 6.0f), Quaternion.Euler(2, 270, 0), "29d_三层走廊内部.png");
            }
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

    // True plan view: an orthographic camera straight down, so north is screen-up and
    // east is screen-right with no perspective skew.
    static void ShotPlan(Camera cam, float height, float orthoSize, string fileName)
    {
        bool prevOrtho = cam.orthographic;
        float prevSize = cam.orthographicSize;

        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        Shot(cam, new Vector3(0f, height, 0f), Quaternion.Euler(90f, 0f, 0f), fileName);

        cam.orthographic = prevOrtho;
        cam.orthographicSize = prevSize;
    }


    // ---- plan labels -------------------------------------------------------
    // Floating text used only for the aerial reference renders. Everything created
    // here is destroyed again after the shot, so the game scene stays clean.

    static readonly System.Collections.Generic.List<GameObject> PlanTemps =
        new System.Collections.Generic.List<GameObject>();

    static Shader PlanShader;

    static void AcquirePlanShader()
    {
        if (PlanShader) return;
        foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            var m = r.sharedMaterial;
            if (m && m.shader) { PlanShader = m.shader; return; }
        }
        PlanShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
    }

    static Material PlanMat(Color c)
    {
        AcquirePlanShader();
        var m = new Material(PlanShader);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        m.color = c;
        return m;
    }

    static void PlanLabel(string text, Vector3 pos, Quaternion rot, float charSize,
                          Color textColor, Color plateColor)
    {
        var font = Resources.Load<Font>("CampusFont");
        if (!font) { Debug.LogWarning("[PlanLabel] CampusFont missing"); return; }

        const int fontSize = 76;
        float glyph = fontSize * charSize * 0.175f;        // calibrated world height

        var go = new GameObject("PlanText_" + text);
        go.transform.position = pos + new Vector3(0f, 0.04f, 0f);
        go.transform.rotation = rot;
        var tm = go.AddComponent<TextMesh>();
        tm.font = font; tm.text = text; tm.fontSize = fontSize; tm.characterSize = charSize;
        tm.alignment = TextAlignment.Center; tm.anchor = TextAnchor.MiddleCenter;
        tm.color = textColor;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        PlanTemps.Add(go);

        Debug.Log($"[PlanLabel] '{text}' pos={pos} glyph={glyph:F2}m shader={mr.sharedMaterial.shader.name}");
    }

    static void ClearPlanTemps()
    {
        foreach (var go in PlanTemps) if (go) UnityEngine.Object.DestroyImmediate(go);
        PlanTemps.Clear();
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
