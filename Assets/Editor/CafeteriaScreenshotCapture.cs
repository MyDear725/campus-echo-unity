using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CafeteriaScreenshotCapture
{
    const string ScenePath = "Assets/Interiors/Scenes/cafeteria.unity";
    const string OutDir = "output/食堂截图";

    [MenuItem("Campus/Interiors/Capture Cafeteria Screenshots")]
    public static void CaptureAll()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("Failed to open scene: " + ScenePath);
            return;
        }

        if (!Directory.Exists(OutDir))
        {
            Directory.CreateDirectory(OutDir);
        }

        var roofObj = GameObject.Find("Ceiling_Roof");
        var slabObj = GameObject.Find("Floor_2F_Slab");
        var slab3Obj = GameObject.Find("Floor_3F_Slab");
        var f1Obj = GameObject.Find("01_Floor_1F");
        var f2Obj = GameObject.Find("02_Floor_2F");
        var f3Obj = GameObject.Find("03_Floor_3F");
        var mainSun = GameObject.Find("Main_Interior_Sun");
        var wallWestSouth = GameObject.Find("Wall_West_South");
        var wallSouthWest = GameObject.Find("Wall_South_West");
        var wallWestLintel = GameObject.Find("Wall_West_DoorLintel");
        var wallSouthLintel = GameObject.Find("Wall_South_DoorLintel");

        var camGo = new GameObject("CaptureCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Color;
        cam.backgroundColor = new Color(0.12f, 0.14f, 0.16f);
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 300f;

        Shader.WarmupAllShaders();

        // Top-down soft vertical lighting for architectural clarity
        var topLightGo = new GameObject("TopDownPlanLight");
        var topLight = topLightGo.AddComponent<Light>();
        topLight.type = LightType.Directional;
        topLight.color = new Color(1f, 0.98f, 0.95f);
        topLight.intensity = 0.95f;
        topLightGo.transform.rotation = Quaternion.Euler(85f, -15f, 0f);

        // Dummy render
        var dummyRt = new RenderTexture(64, 64, 16);
        cam.targetTexture = dummyRt;
        cam.Render();
        cam.targetTexture = null;
        Object.DestroyImmediate(dummyRt);

        // ==========================================
        // 1. 1F Pure Architectural Plan (Roof, 2F & 3F hidden)
        // ==========================================
        if (mainSun) mainSun.SetActive(false);
        if (roofObj) roofObj.SetActive(false);
        if (slabObj) slabObj.SetActive(false);
        if (slab3Obj) slab3Obj.SetActive(false);
        if (f2Obj) f2Obj.SetActive(false);
        if (f3Obj) f3Obj.SetActive(false);

        cam.orthographic = true;
        cam.orthographicSize = 17.5f;
        CaptureShot(cam, new Vector3(0f, 35f, 0f), Quaternion.Euler(90f, 0f, 0f), Path.Combine(OutDir, "cafeteria_1f_topdown.png"));

        // ==========================================
        // 2. 1F Annotated Floor Plan with Badges
        // ==========================================
        var badgesRoot1F = new GameObject("TopDown_Annotated_Badges_1F").transform;
        BuildPlanBadges1F(badgesRoot1F);

        CaptureShot(cam, new Vector3(0f, 35f, 0f), Quaternion.Euler(90f, 0f, 0f), Path.Combine(OutDir, "cafeteria_1f_plan_annotated.png"));
        Object.DestroyImmediate(badgesRoot1F.gameObject);

        // ==========================================
        // 3. 2F Top-Down Plan View (Roof & 3F hidden, 2F visible)
        // ==========================================
        if (slabObj) slabObj.SetActive(true);
        if (f2Obj) f2Obj.SetActive(true);

        CaptureShot(cam, new Vector3(0f, 35f, 0f), Quaternion.Euler(90f, 0f, 0f), Path.Combine(OutDir, "cafeteria_2f_topdown.png"));

        // ==========================================
        // 4. 3F Top-Down Plan View (Roof hidden, 3F visible)
        // ==========================================
        if (slab3Obj) slab3Obj.SetActive(true);
        if (f3Obj) f3Obj.SetActive(true);

        CaptureShot(cam, new Vector3(0f, 35f, 0f), Quaternion.Euler(90f, 0f, 0f), Path.Combine(OutDir, "cafeteria_3f_topdown.png"));

        // ==========================================
        // 5. 3F Annotated Floor Plan with Badges Matching User Request
        // ==========================================
        var badgesRoot3F = new GameObject("TopDown_Annotated_Badges_3F").transform;
        BuildPlanBadges3F(badgesRoot3F);

        CaptureShot(cam, new Vector3(0f, 35f, 0f), Quaternion.Euler(90f, 0f, 0f), Path.Combine(OutDir, "cafeteria_3f_plan_annotated.png"));
        Object.DestroyImmediate(badgesRoot3F.gameObject);

        cam.orthographic = false;

        // Restore scene sun and remove top-down light
        if (mainSun) mainSun.SetActive(true);
        Object.DestroyImmediate(topLightGo);

        // ==========================================
        // 6. Cutaway Sectional View of All Three Floors
        // ==========================================
        if (wallWestSouth) wallWestSouth.SetActive(false);
        if (wallSouthWest) wallSouthWest.SetActive(false);
        if (wallWestLintel) wallWestLintel.SetActive(false);
        if (wallSouthLintel) wallSouthLintel.SetActive(false);
        cam.fieldOfView = 50f;
        CaptureShot(cam, new Vector3(-28f, 26f, -27f), Quaternion.Euler(32f, 44f, 0f), Path.Combine(OutDir, "cafeteria_multi_cutaway.png"));

        // Restore exterior walls and roof
        if (wallWestSouth) wallWestSouth.SetActive(true);
        if (wallSouthWest) wallSouthWest.SetActive(true);
        if (wallWestLintel) wallWestLintel.SetActive(true);
        if (wallSouthLintel) wallSouthLintel.SetActive(true);
        if (roofObj) roofObj.SetActive(true);

        cam.fieldOfView = 75f;

        // ==========================================
        // 7. 1F Key Perspectives
        // ==========================================
        CaptureShot(cam, new Vector3(9.5f, 1.7f, -3.0f), Quaternion.Euler(3f, 90f, 0f), Path.Combine(OutDir, "cafeteria_1f_restroom_focus.png"));
        CaptureShot(cam, new Vector3(-16.0f, 1.6f, -2.0f), Quaternion.Euler(0f, 85f, 0f), Path.Combine(OutDir, "cafeteria_1f_west_entrance.png"));
        CaptureShot(cam, new Vector3(-5.0f, 1.6f, 5.0f), Quaternion.Euler(0f, 60f, 0f), Path.Combine(OutDir, "cafeteria_1f_waterbar_serving.png"));
        CaptureShot(cam, new Vector3(-8.0f, 1.7f, -12.5f), Quaternion.Euler(5f, -90f, 0f), Path.Combine(OutDir, "cafeteria_1f_stair1_view.png"));
        CaptureShot(cam, new Vector3(11.5f, 1.7f, -14.2f), Quaternion.Euler(5f, -70f, 0f), Path.Combine(OutDir, "cafeteria_1f_stairs_view.png"));
        CaptureShot(cam, new Vector3(4.5f, 5.9f, -8.0f), Quaternion.Euler(5f, -65f, 0f), Path.Combine(OutDir, "cafeteria_2f_dining_view.png"));

        // ==========================================
        // 8. 3F Key Perspectives
        // ==========================================
        if (f1Obj) f1Obj.SetActive(false);
        if (f2Obj) f2Obj.SetActive(false);

        // 8a. 3F Fruit Supermarket View (北侧水果超市中岛、陈列货架与冷风幕柜)
        CaptureShot(cam, new Vector3(-2.0f, 10.2f, 6.0f), Quaternion.Euler(5f, -15f, 0f), Path.Combine(OutDir, "cafeteria_3f_fruit_market.png"));

        // 8b. 3F Straight Meal Serving Line (东侧直排打菜窗口与保温餐台)
        CaptureShot(cam, new Vector3(7.5f, 10.2f, 3.2f), Quaternion.Euler(4f, 20f, 0f), Path.Combine(OutDir, "cafeteria_3f_meal_line.png"));

        // 8c. 3F Faculty Dining Facade (东南教师餐厅艺术格栅贴图背景墙，空置内部无实体模型)
        CaptureShot(cam, new Vector3(12.6f, 10.2f, -3.2f), Quaternion.Euler(3f, 180f, 0f), Path.Combine(OutDir, "cafeteria_3f_faculty_facade.png"));

        // 8d. 3F Central Dining Hall Seating (三层中间高密度现代餐桌椅)
        CaptureShot(cam, new Vector3(-13.0f, 10.3f, -5.5f), Quaternion.Euler(6f, 60f, 0f), Path.Combine(OutDir, "cafeteria_3f_dining_view.png"));

        // Cleanup
        if (roofObj) roofObj.SetActive(true);
        if (slabObj) slabObj.SetActive(true);
        if (slab3Obj) slab3Obj.SetActive(true);
        if (f1Obj) f1Obj.SetActive(true);
        if (f2Obj) f2Obj.SetActive(true);
        if (f3Obj) f3Obj.SetActive(true);
        Object.DestroyImmediate(camGo);

        Debug.Log("CAFETERIA_SCREENSHOTS_CAPTURED_SUCCESS");
    }

    static void BuildPlanBadges1F(Transform parent)
    {
        var font = Resources.Load<Font>("CampusFont");
        if (!font) return;

        // Color theme matching user schematic:
        Color colCyan = new Color(0.12f, 0.48f, 0.72f);      // 打餐/选餐区
        Color colGreen = new Color(0.14f, 0.58f, 0.42f);     // 水吧 / 出入口
        Color colOrange = new Color(0.85f, 0.52f, 0.12f);    // 残餐回收 / 餐具
        Color colPurple = new Color(0.48f, 0.22f, 0.65f);    // 洗手间 (WC)
        Color colCharcoal = new Color(0.18f, 0.22f, 0.26f);  // 后厨
        Color colSlate = new Color(0.25f, 0.32f, 0.42f);     // 楼梯

        // 1. 打餐-选餐-区 (L型横段)
        MakeBadge(parent, font, "打菜-选餐-区 (保温台 / 菜品陈列窗)", new Vector3(-4.5f, 4.3f, 14.5f), new Vector2(15.5f, 1.8f), colCyan, Color.white, 38, 0.13f);

        // 2. 自选点餐通道 (L型纵段)
        MakeBadge(parent, font, "自选点餐通道", new Vector3(2.4f, 4.3f, 6.5f), new Vector2(1.6f, 7.5f), colCyan, Color.white, 36, 0.12f, true);

        // 3. 后厨 (内部操作间)
        MakeBadge(parent, font, "后 厨 (内部操作间)\n非开放区 · 设出餐窗口与封闭隔墙", new Vector3(11.8f, 4.3f, 11.2f), new Vector2(9.8f, 3.2f), colCharcoal, Color.white, 34, 0.115f);

        // 4. 残餐回收区
        MakeBadge(parent, font, "残餐回收区 (餐盘归还 / 分类垃圾桶)", new Vector3(12.5f, 4.3f, 4.2f), new Vector2(9.5f, 1.8f), colOrange, Color.white, 34, 0.115f);

        // 5. 洗手间 (WC) - 紧贴东外墙全交互设计
        MakeBadge(parent, font, "洗手间(WC)[E]", new Vector3(17.2f, 4.3f, -3.0f), new Vector2(2.0f, 7.5f), colPurple, Color.white, 32, 0.11f, true);

        // 5b. 东区新增用餐席位
        MakeBadge(parent, font, "东区新增用餐位\n(实体模型缩进释放大厅空间)", new Vector3(13.5f, 4.3f, -3.5f), new Vector2(4.8f, 2.4f), new Color(0.15f, 0.17f, 0.20f, 0.9f), Color.white, 30, 0.10f);

        // 6. 水吧
        MakeBadge(parent, font, "水 吧\n饮品 / 奶茶 / 收银岛台", new Vector3(-0.5f, 4.3f, -1.0f), new Vector2(5.2f, 3.0f), colGreen, Color.white, 36, 0.12f);

        // 7. 餐具柜
        MakeBadge(parent, font, "餐具柜\n托盘/筷勺", new Vector3(-16.0f, 4.3f, 4.5f), new Vector2(3.2f, 2.2f), colOrange, Color.white, 32, 0.11f);

        // 8. 用餐区
        MakeBadge(parent, font, "用 餐 区\n四人座 / 六人座标准校园餐桌椅列阵", new Vector3(-5.5f, 4.3f, -5.5f), new Vector2(10.5f, 2.8f), new Color(0.15f, 0.17f, 0.20f, 0.9f), Color.white, 36, 0.125f);

        // 9. 西门 [E]
        MakeBadge(parent, font, "西门[E]", new Vector3(-17.5f, 4.3f, -2.0f), new Vector2(1.4f, 4.5f), colGreen, Color.white, 34, 0.12f, true);

        // 10. 东南门 [E]
        MakeBadge(parent, font, "东南门 [E]", new Vector3(12.0f, 4.3f, -15.5f), new Vector2(5.5f, 1.6f), colGreen, Color.white, 36, 0.12f);

        // 11. 楼梯 1 (西南角，从右向左 / 朝西攀升)
        MakeBadge(parent, font, "楼梯 1 (从右向左升高 ◄ 前往 2F)", new Vector3(-13.9f, 4.3f, -12.5f), new Vector2(7.2f, 2.4f), colSlate, Color.white, 32, 0.11f);

        // 12. 楼梯 2 (东南角，朝北攀升)
        MakeBadge(parent, font, "楼梯 2\n(▲ 朝北上楼)", new Vector3(6.0f, 4.3f, -11.9f), new Vector2(2.4f, 6.5f), colSlate, Color.white, 32, 0.11f, true);
    }

    static void BuildPlanBadges3F(Transform parent)
    {
        var font = Resources.Load<Font>("CampusFont");
        if (!font) return;

        Color colCyan = new Color(0.12f, 0.48f, 0.72f);      // 直排打餐区
        Color colOrange = new Color(0.85f, 0.52f, 0.12f);    // 水果超市 / 餐具 / 回收
        Color colPurple = new Color(0.48f, 0.22f, 0.65f);    // 洗手间 (WC)
        Color colCharcoal = new Color(0.18f, 0.22f, 0.26f);  // 教师餐厅贴图区
        Color colSlate = new Color(0.25f, 0.32f, 0.42f);     // 楼梯
        Color colDining = new Color(0.15f, 0.17f, 0.20f, 0.9f);

        // 1. 北侧水果超市
        MakeBadge(parent, font, "北面: 鲜果生鲜超市 (精品鲜切水果 / 现榨果汁 / 自选称重)", new Vector3(-2.5f, 8.65f, 13.0f), new Vector2(17.5f, 3.2f), colOrange, Color.white, 36, 0.12f);

        // 2. 东侧直排买饭区域
        MakeBadge(parent, font, "东侧: 直排打饭选餐窗口 (广式烧味 / 精品热炒 / 滋补炖汤)", new Vector3(11.5f, 8.65f, 8.5f), new Vector2(2.2f, 12.0f), colCyan, Color.white, 34, 0.115f, true);

        // 3. 东南教师餐厅 (艺术格栅贴图展示，无实体模型，不交互)
        MakeBadge(parent, font, "东南面: 教工餐厅 (艺术木饰隔墙贴图展示)\n严格无实体家具模型 · 无交互触发", new Vector3(12.65f, 8.65f, -11.5f), new Vector2(9.8f, 7.0f), colCharcoal, Color.white, 34, 0.115f);

        // 4. 洗手间 (WC) - 紧贴东外墙
        MakeBadge(parent, font, "洗手间(WC)[E]", new Vector3(17.2f, 8.65f, -3.0f), new Vector2(2.0f, 7.5f), colPurple, Color.white, 32, 0.11f, true);

        // 5. 中间与南侧用餐大厅
        MakeBadge(parent, font, "中间 & 南面: 现代用餐大厅 (四人/六人餐桌椅密集列阵)", new Vector3(-4.0f, 8.65f, -2.5f), new Vector2(12.5f, 3.0f), colDining, Color.white, 36, 0.12f);

        // 6. 餐具取放处
        MakeBadge(parent, font, "餐具柜\n托盘/筷勺", new Vector3(-15.5f, 8.65f, -8.0f), new Vector2(3.2f, 2.0f), colOrange, Color.white, 32, 0.11f);

        // 7. 残餐回收处
        MakeBadge(parent, font, "残餐回收区\n餐盘归还/垃圾分类", new Vector3(12.5f, 8.65f, 1.2f), new Vector2(4.5f, 2.0f), colOrange, Color.white, 32, 0.11f);

        // 8. 楼梯 1 (西南角，从右向左 / 朝西攀升)
        MakeBadge(parent, font, "楼梯 1 (从右向左升高 ◄ 2F通往3F / 返回2F)", new Vector3(-13.9f, 8.65f, -12.5f), new Vector2(7.2f, 2.4f), colSlate, Color.white, 32, 0.11f);

        // 9. 楼梯 2 (东南角，朝北攀升)
        MakeBadge(parent, font, "楼梯 2\n(▲ 朝北上楼 2F通往3F / 返回2F)", new Vector3(6.0f, 8.65f, -11.9f), new Vector2(2.4f, 6.5f), colSlate, Color.white, 32, 0.11f, true);
    }

    static void MakeBadge(Transform parent, Font font, string text, Vector3 pos, Vector2 size, Color bgColor, Color textColor, int fontSize, float charSize, bool vertical = false)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Badge_" + text;
        box.transform.SetParent(parent, false);
        box.transform.position = pos;
        box.transform.localScale = new Vector3(size.x, 0.04f, size.y);
        Object.DestroyImmediate(box.GetComponent<Collider>());

        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var mat = new Material(shader);
        mat.color = bgColor;
        box.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var textObj = new GameObject("Text_" + text);
        textObj.transform.SetParent(parent, false);
        textObj.transform.position = pos + new Vector3(0, 0.05f, 0);
        textObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var tm = textObj.AddComponent<TextMesh>();
        tm.font = font;
        if (vertical)
        {
            // insert newline between each character for vertical badge
            string vStr = "";
            for (int i = 0; i < text.Length; i++)
            {
                vStr += text[i];
                if (i < text.Length - 1 && text[i] != '\n') vStr += "\n";
            }
            tm.text = vStr;
        }
        else
        {
            tm.text = text;
        }

        tm.fontSize = fontSize;
        tm.characterSize = charSize;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = textColor;

        var mr = textObj.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
    }

    static void CaptureShot(Camera cam, Vector3 pos, Quaternion rot, string filePath)
    {
        cam.transform.position = pos;
        cam.transform.rotation = rot;

        int width = 1600;
        int height = 900;
        var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;

        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

        cam.Render();
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(filePath, bytes);

        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);

        Debug.Log("Saved screenshot: " + filePath);
    }
}
