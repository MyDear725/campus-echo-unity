using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CafeteriaInteriorBuilder
{
    const string ScenePath = "Assets/Interiors/Scenes/cafeteria.unity";

    // Modern architectural material palette
    static Material matFloorTerrazzo, matFloorDarkBasalt;
    static Material matWallMicrocement, matWallDarkCharcoal, matCeilingWhite;
    static Material matWoodScandiOak, matWoodDarkWalnut;
    static Material matMarbleWhite;
    static Material matMetalStainless, matMetalTitanium, matMetalMatteBlack;
    static Material matGlassUltra;
    static Material matChairCream, matChairSage, matChairTerracotta, matChairSlateBlue;
    static Material matLightWarmLED, matSignBlackBox;
    static Material matKitchenPic, matFacultyPic;

    // Detailed food, fruit & plant materials
    static Material matFoodBeef, matFoodGolden, matFoodGreen, matFoodRed, matFoodEgg, matFoodRice, matFoodBamboo;
    static Material matFruitApple, matFruitOrange, matFruitBanana, matFruitWatermelon, matFruitMelon, matFruitGrape;
    static Material matPlantFoliage, matPlantPot;

    static Material GetOrCreateMaterial(string name, Color baseColor, float metallic = 0f, float smoothness = 0.2f,
                                        Color? emission = null, bool transparent = false, string texturePath = null,
                                        Vector2? tiling = null)
    {
        var mat = GetOrCreateMaterialCore(name, baseColor, metallic, smoothness, emission, transparent);
        if (!string.IsNullOrEmpty(texturePath))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex)
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            }
        }
        return mat;
    }

    static Material GetOrCreateMaterialCore(string name, Color baseColor, float metallic, float smoothness,
                                            Color? emission, bool transparent)
    {
        string dir = "Assets/Interiors/Materials/CafeteriaModern";
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        string path = dir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        if (!mat)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
        {
            mat.shader = shader;
        }

        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", smoothness);

        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }

        if (transparent)
        {
            mat.SetFloat("_Surface", 1);
            mat.SetFloat("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
        }
        else
        {
            mat.SetFloat("_Surface", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = -1;
        }

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void LoadMaterials()
    {
        matFloorTerrazzo = GetOrCreateMaterial("Mat_Floor_Terrazzo", new Color(0.84f, 0.835f, 0.82f), 0.02f, 0.32f, null, false, "Assets/Interiors/Textures/Library_Granite_Tile.png", new Vector2(15f, 13.3f));
        matFloorDarkBasalt = GetOrCreateMaterial("Mat_Floor_DarkBasalt", new Color(0.18f, 0.19f, 0.21f), 0.10f, 0.25f);

        matWallMicrocement = GetOrCreateMaterial("Mat_Wall_Microcement", new Color(0.845f, 0.835f, 0.815f), 0.0f, 0.05f);
        matWallDarkCharcoal = GetOrCreateMaterial("Mat_Wall_DarkCharcoal", new Color(0.17f, 0.18f, 0.20f), 0.15f, 0.25f);
        matCeilingWhite = GetOrCreateMaterial("Mat_Ceiling_White", new Color(0.88f, 0.88f, 0.87f), 0.0f, 0.04f);

        matWoodScandiOak = GetOrCreateMaterial("Mat_Wood_ScandiOak", new Color(0.80f, 0.70f, 0.55f), 0.0f, 0.26f, null, false, "Assets/Interiors/Textures/Library_Oak_Grain.png", new Vector2(1f, 1f));
        matWoodDarkWalnut = GetOrCreateMaterial("Mat_Wood_DarkWalnut", new Color(0.30f, 0.24f, 0.19f), 0.0f, 0.28f, null, false, "Assets/Interiors/Textures/Library_Oak_Grain.png", new Vector2(1f, 1f));

        matMarbleWhite = GetOrCreateMaterial("Mat_Marble_White", new Color(0.96f, 0.96f, 0.97f), 0.05f, 0.65f);

        matMetalStainless = GetOrCreateMaterial("Mat_Metal_Stainless", new Color(0.88f, 0.90f, 0.92f), 0.92f, 0.78f);
        matMetalTitanium = GetOrCreateMaterial("Mat_Metal_Titanium", new Color(0.22f, 0.23f, 0.25f), 0.85f, 0.60f);
        matMetalMatteBlack = GetOrCreateMaterial("Mat_Metal_MatteBlack", new Color(0.12f, 0.12f, 0.14f), 0.60f, 0.30f);

        matGlassUltra = GetOrCreateMaterial("Mat_Glass_Ultra", new Color(0.92f, 0.96f, 1.0f, 0.32f), 0.1f, 0.96f, null, true);

        matChairCream = GetOrCreateMaterial("Mat_Chair_Cream", new Color(0.91f, 0.89f, 0.85f), 0.0f, 0.22f);
        matChairSage = GetOrCreateMaterial("Mat_Chair_Sage", new Color(0.50f, 0.58f, 0.48f), 0.0f, 0.22f);
        matChairTerracotta = GetOrCreateMaterial("Mat_Chair_Terracotta", new Color(0.72f, 0.46f, 0.33f), 0.0f, 0.22f);
        matChairSlateBlue = GetOrCreateMaterial("Mat_Chair_SlateBlue", new Color(0.30f, 0.36f, 0.42f), 0.0f, 0.22f);

        matLightWarmLED = GetOrCreateMaterial("Mat_Light_WarmLED", new Color(1.0f, 0.97f, 0.90f), 0f, 0.5f, new Color(1.0f, 0.94f, 0.82f) * 1.8f);
        matSignBlackBox = GetOrCreateMaterial("Mat_Sign_BlackBox", new Color(0.14f, 0.14f, 0.16f), 0.35f, 0.40f);

        matFoodBeef = GetOrCreateMaterial("Mat_Food_Beef", new Color(0.38f, 0.22f, 0.15f), 0.05f, 0.40f);
        matFoodGolden = GetOrCreateMaterial("Mat_Food_Golden", new Color(0.85f, 0.58f, 0.22f), 0.05f, 0.30f);
        matFoodGreen = GetOrCreateMaterial("Mat_Food_Green", new Color(0.26f, 0.58f, 0.22f), 0.05f, 0.35f);
        matFoodRed = GetOrCreateMaterial("Mat_Food_Red", new Color(0.78f, 0.25f, 0.18f), 0.05f, 0.40f);
        matFoodEgg = GetOrCreateMaterial("Mat_Food_Egg", new Color(0.95f, 0.78f, 0.24f), 0.05f, 0.30f);
        matFoodRice = GetOrCreateMaterial("Mat_Food_Rice", new Color(0.96f, 0.95f, 0.92f), 0.0f, 0.15f);
        matFoodBamboo = GetOrCreateMaterial("Mat_Food_Bamboo", new Color(0.80f, 0.68f, 0.48f), 0.0f, 0.25f);

        matFruitApple = GetOrCreateMaterial("Mat_Fruit_Apple", new Color(0.85f, 0.12f, 0.12f), 0.05f, 0.40f);
        matFruitOrange = GetOrCreateMaterial("Mat_Fruit_Orange", new Color(0.96f, 0.52f, 0.08f), 0.05f, 0.35f);
        matFruitBanana = GetOrCreateMaterial("Mat_Fruit_Banana", new Color(0.96f, 0.85f, 0.20f), 0.05f, 0.25f);
        matFruitWatermelon = GetOrCreateMaterial("Mat_Fruit_Watermelon", new Color(0.80f, 0.18f, 0.22f), 0.05f, 0.35f);
        matFruitMelon = GetOrCreateMaterial("Mat_Fruit_Melon", new Color(0.55f, 0.78f, 0.35f), 0.05f, 0.30f);
        matFruitGrape = GetOrCreateMaterial("Mat_Fruit_Grape", new Color(0.45f, 0.20f, 0.55f), 0.05f, 0.35f);

        matPlantFoliage = GetOrCreateMaterial("Mat_Plant_Foliage", new Color(0.24f, 0.48f, 0.22f), 0.0f, 0.20f);
        matPlantPot = GetOrCreateMaterial("Mat_Plant_Pot", new Color(0.18f, 0.19f, 0.21f), 0.15f, 0.25f);

        AssetDatabase.ImportAsset("Assets/Interiors/Textures/Kitchen_Backdrop.png", ImportAssetOptions.ForceSynchronousImport);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Interiors/Textures/Kitchen_Backdrop.png");
        matKitchenPic = AssetDatabase.LoadAssetAtPath<Material>("Assets/Interiors/Materials/Kitchen_Backdrop.mat");
        if (!matKitchenPic)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            matKitchenPic = new Material(shader);
            if (tex) matKitchenPic.mainTexture = tex;
            AssetDatabase.CreateAsset(matKitchenPic, "Assets/Interiors/Materials/Kitchen_Backdrop.mat");
        }
        else if (tex && matKitchenPic.mainTexture != tex)
        {
            matKitchenPic.mainTexture = tex;
            EditorUtility.SetDirty(matKitchenPic);
        }

        AssetDatabase.ImportAsset("Assets/Interiors/Textures/Faculty_Dining_Facade.png", ImportAssetOptions.ForceSynchronousImport);
        var fTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Interiors/Textures/Faculty_Dining_Facade.png");
        matFacultyPic = AssetDatabase.LoadAssetAtPath<Material>("Assets/Interiors/Materials/Faculty_Dining_Facade.mat");
        if (!matFacultyPic)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            matFacultyPic = new Material(shader);
            if (fTex) matFacultyPic.mainTexture = fTex;
            AssetDatabase.CreateAsset(matFacultyPic, "Assets/Interiors/Materials/Faculty_Dining_Facade.mat");
        }
        else if (fTex && matFacultyPic.mainTexture != fTex)
        {
            matFacultyPic.mainTexture = fTex;
            EditorUtility.SetDirty(matFacultyPic);
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem("Campus/Interiors/Build Cafeteria Three Floors")]
    [MenuItem("Campus/Interiors/Build Cafeteria First and Second Floor")]
    public static void Build()
    {
        seatAnchorPos.Clear();
        seatAnchorRot.Clear();

        LoadMaterials();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("Could not open cafeteria scene: " + ScenePath);
            return;
        }

        var rootObj = GameObject.Find("InteriorRoot · 第六食堂");
        if (!rootObj)
        {
            rootObj = new GameObject("InteriorRoot · 第六食堂");
        }

        var sceneRoot = rootObj.GetComponent<InteriorSceneRoot>();
        if (!sceneRoot)
        {
            sceneRoot = rootObj.AddComponent<InteriorSceneRoot>();
        }
        sceneRoot.buildingId = "cafeteria";
        sceneRoot.isPlaceholder = false;
        sceneRoot.exitRadius = 2.4f;
        sceneRoot.fallRecoveryHeight = -5f;

        var interactions = rootObj.GetComponent<CafeteriaIndoorInteractions>();
        if (!interactions)
        {
            interactions = rootObj.AddComponent<CafeteriaIndoorInteractions>();
        }

        // Clean up old objects
        var oldShell = rootObj.transform.Find("PlaceholderShell · 可替换的空房间");
        if (oldShell) UnityEngine.Object.DestroyImmediate(oldShell.gameObject);

        var oldBuild = rootObj.transform.Find("Cafeteria_1F_Interior");
        if (oldBuild) UnityEngine.Object.DestroyImmediate(oldBuild.gameObject);

        var oldBuild2 = rootObj.transform.Find("Cafeteria_2F_Interior");
        if (oldBuild2) UnityEngine.Object.DestroyImmediate(oldBuild2.gameObject);

        var oldMulti = rootObj.transform.Find("Cafeteria_MultiFloor_Interior");
        if (oldMulti) UnityEngine.Object.DestroyImmediate(oldMulti.gameObject);

        // Root container for all floors
        var multiRoot = new GameObject("Cafeteria_MultiFloor_Interior").transform;
        multiRoot.SetParent(rootObj.transform, false);

        // 1. Build Base Architecture (Perimeter walls from Y=0 to 12.9m, Ground Floor, 2F Slab, 3F Slab, Roof)
        BuildArchitectureShell(multiRoot);

        // 2. Build Vertical Circulation (Stairs 1 & Stairs 2 connecting 1F, 2F & 3F)
        BuildStaircases(multiRoot);

        // 3. Build 1F Interior Components
        var f1Root = new GameObject("01_Floor_1F").transform;
        f1Root.SetParent(multiRoot, false);
        BuildFloorInterior(f1Root, 0f, 1);

        // 4. Build 2F Interior Components (Slab height Y = 4.3m)
        var f2Root = new GameObject("02_Floor_2F").transform;
        f2Root.SetParent(multiRoot, false);
        BuildFloorInterior(f2Root, 4.3f, 2);

        // 5. Build 3F Interior Components (Slab height Y = 8.6m)
        var f3Root = new GameObject("03_Floor_3F").transform;
        f3Root.SetParent(multiRoot, false);
        BuildFloorInterior3F(f3Root, 8.6f);

        // 6. Doors, Exits & Spawn Points
        BuildDoorsAndExits(rootObj.transform, multiRoot, sceneRoot);

        // 7. Lighting for all 3 floors
        BuildLighting(multiRoot);

        // 8. Interior presentation: dedicated SSAO renderer, ACES volume, reflection
        //    probe and the runtime presentation switch, all persisted as real assets.
        BuildPresentation(rootObj.transform, sceneRoot);

        // 9. Ordering system: anchors are placed at the existing serving window, the
        //    tray-return counter and the dining hall, so the flow uses the real plan.
        BuildOrdering(rootObj.transform, sceneRoot);
        InteriorLabelStairFix.ConfigureLabels(rootObj.transform, 3);
        InteriorLabelStairFix.ConfigureCafeteriaRails(rootObj.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("CAFETERIA_MULTI_FLOOR_BUILD_COMPLETE: Built exact blueprint-matching 1F, 2F & 3F for 第六食堂");
    }


    // Cafeteria is 36 x 32 m over three storeys, so the interior camera only needs to
    // see ~55 m; shadow distance is sized to cover a floor plus its furniture.
    static void BuildPresentation(Transform rootTransform, InteriorSceneRoot sceneRoot)
    {
        int rendererIndex;
        UniversalRendererData renderer;
        rendererIndex = InteriorPresentationSetup.EnsureInteriorRenderer(0.22f, out renderer);

        var profile = InteriorPresentationSetup.EnsureVolume(
            "Assets/Interiors/CafeteriaInterior/InteriorVolume.asset",
            postExposure: 0.42f, contrast: 5f, saturation: -3f,
            bloomIntensity: 0.06f, bloomThreshold: 1.2f);

        var volumeGo = rootTransform.Find("食堂 · 色调与柔和灯光");
        GameObject vol = volumeGo ? volumeGo.gameObject : new GameObject("食堂 · 色调与柔和灯光");
        vol.transform.SetParent(rootTransform, false);
        var volume = vol.GetComponent<Volume>();
        if (!volume) volume = vol.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 20;
        volume.sharedProfile = profile;

        InteriorPresentationSetup.EnsureProbe(rootTransform, "食堂材质反射",
            new Vector3(0f, 4.4f, 0f), new Vector3(38f, 14f, 34f), new Color(0.34f, 0.35f, 0.36f));

        var pres = rootTransform.GetComponent<InteriorPresentation>();
        if (!pres) pres = rootTransform.gameObject.AddComponent<InteriorPresentation>();
        pres.rendererIndex = rendererIndex;
        pres.interiorFarClip = 70f;
        pres.interiorShadowDistance = 46f;
        pres.interiorCascades = 4;

        UnityEditor.EditorUtility.SetDirty(sceneRoot);
    }


    // Wires every selling point to its own anchors.
    //
    // Each real counter in the plan becomes ONE order anchor tagged with a unique
    // CafeteriaShop id (CafeteriaCounter.shop). Each anchor gets its own pickup point at
    // its own counter, so a meal is collected where it was bought and never at another
    // stall. Names follow the stable rule:
    //     Order_<shopKey>      -> stands in front of the counter
    //     Collect_<shopKey>    -> its pickup point
    // The cafeteria component finds these by the CafeteriaCounter marker on the player's
    // own floor, not by name, so renaming an object cannot re-route a counter.
    static readonly Vector3[] StallSignPositions =
    {
        new Vector3(-10.0f, 3.2f, 12.0f),
        new Vector3(-4.5f, 3.2f, 12.0f),
        new Vector3(1.0f, 3.2f, 12.0f),
        new Vector3(3.5f, 3.2f, 5.5f)
    };

    static void BuildOrdering(Transform rootTransform, InteriorSceneRoot sceneRoot)
    {
        // floor base heights: 1F 0, 2F 4.3, 3F 8.6
        float[] fy = { 0f, 4.3f, 8.6f };

        // Remove anchors from earlier layouts so the scene never keeps two copies of one
        // counter (which is what let several menus compete for the same E press).
        var stale = new System.Collections.Generic.List<Transform>();
        foreach (var t in rootTransform.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n.StartsWith("OrderSpot_") || n.StartsWith("Order_") ||
                n.StartsWith("CollectSpot_") || n.StartsWith("Collect_") ||
                n.StartsWith("ReturnSpot_") || n.StartsWith("Return_"))
                stale.Add(t);
        }
        foreach (var t in stale) UnityEngine.Object.DestroyImmediate(t.gameObject);

        Transform Anchor(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rootTransform, false);
            go.transform.position = pos;
            return go.transform;
        }

        var order = new System.Collections.Generic.List<Transform>();
        var collect = new System.Collections.Generic.List<Transform>();
        var ret = new System.Collections.Generic.List<Transform>();
        var seat = new System.Collections.Generic.List<Transform>();

        // Perimeter seating walk-in points are fine; every shop's stand point sits one
        // metre in front of its own counter so a player can only reach one at a time.
        foreach (var def in CafeteriaShops.All)
        {
            Vector3 stand;
            Vector3 pickup;
            Vector3 counterFront;
            switch (def.id)
            {
                case CafeteriaShop.Starter1F:
                    counterFront = StallSignPositions[0] + new Vector3(0, 0, -1.7f); break;
                case CafeteriaShop.Wok1F:
                    counterFront = StallSignPositions[1] + new Vector3(0, 0, -1.7f); break;
                case CafeteriaShop.Noodle1F:
                    counterFront = StallSignPositions[2] + new Vector3(0, 0, -1.7f); break;
                case CafeteriaShop.Steamed1F:
                    // Vertical arm at X = 3.5, customer corridor on its west side.
                    counterFront = new Vector3(3.5f - 1.6f, 0, 5.5f); break;
                case CafeteriaShop.WaterBar1F:
                    // Island at (-0.5, -1): customers stand on its south side.
                    counterFront = new Vector3(-0.5f, 0, -1.0f - 2.5f); break;
                case CafeteriaShop.Sichuan2F:
                    counterFront = StallSignPositions[0] + new Vector3(0, 0, -1.7f); break;
                case CafeteriaShop.Claypot2F:
                    counterFront = StallSignPositions[1] + new Vector3(0, 0, -1.7f); break;
                case CafeteriaShop.Teppan2F:
                    counterFront = StallSignPositions[2] + new Vector3(0, 0, -1.7f); break;
                case CafeteriaShop.Pastry2F:
                    counterFront = new Vector3(3.5f - 1.6f, 0, 5.5f); break;
                case CafeteriaShop.Soup3F:
                    // 3F straight line at X = 11.5, sign 1 is at Z = 4.5; western customer side.
                    counterFront = new Vector3(11.5f - 1.7f, 0, 4.5f); break;
                case CafeteriaShop.HotDish3F:
                    counterFront = new Vector3(11.5f - 1.7f, 0, 8.5f); break;
                case CafeteriaShop.Roast3F:
                    counterFront = new Vector3(11.5f - 1.7f, 0, 12.5f); break;
                case CafeteriaShop.FruitStall3F:
                    // Adjoining the 3F produce racks / island aisle.
                    counterFront = new Vector3(-1.5f, 0, 11.5f - 2.2f); break;
                default:
                    counterFront = new Vector3(0, 0, 10); break;
            }

            float y = fy[def.floor - 1];
            stand = new Vector3(counterFront.x, y, counterFront.z);
            pickup = stand;

            string key = def.id.ToString().ToLowerInvariant();
            var orderAnchor = Anchor("Order_" + key, stand + Vector3.up * 0.5f);
            var marker = orderAnchor.gameObject.AddComponent<CafeteriaCounter>();
            marker.shop = def.id;

            collect.Add(Anchor("Collect_" + key, pickup + Vector3.up * 0.5f));
            order.Add(orderAnchor);
        }

        // One tray-return counter per floor, beside that floor's dining hall.
        ret.Add(Anchor("Return_1f", new Vector3(12.0f, fy[0] + 0.5f, 4.0f)));
        ret.Add(Anchor("Return_2f", new Vector3(12.0f, fy[1] + 0.5f, 4.0f)));
        ret.Add(Anchor("Return_3f", new Vector3(12.0f, fy[2] + 0.5f, 1.2f)));

        // Seats come from the chairs the dining builder actually created, on every floor.
        for (int i = 0; i < seatAnchorPos.Count; i++)
        {
            var a = Anchor("SeatSpot_" + i.ToString("D3"), seatAnchorPos[i]);
            a.rotation = seatAnchorRot[i];
            seat.Add(a);
        }
        Debug.Log($"[Cafeteria] shops={order.Count} collect={collect.Count} return={ret.Count} seats={seat.Count}");

        var ordering = rootTransform.GetComponent<CafeteriaOrdering>();
        if (!ordering) ordering = rootTransform.gameObject.AddComponent<CafeteriaOrdering>();
        ordering.orderSpots = order.ToArray();
        ordering.collectSpots = collect.ToArray();
        ordering.returnSpots = ret.ToArray();
        ordering.seatSpots = seat.ToArray();

        var ui = rootTransform.GetComponent<CafeteriaOrderingUI>();
        if (!ui) ui = rootTransform.gameObject.AddComponent<CafeteriaOrderingUI>();

        UnityEditor.EditorUtility.SetDirty(sceneRoot);
    }

    static GameObject MakeBox(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider = true)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale = size;
        if (mat) obj.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!collider) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        return obj;
    }

    static GameObject MakeText(Transform parent, string text, Vector3 localPos, Quaternion localRot, int fontSize = 28, float charSize = 0.04f, Color? color = null)
    {
        var font = Resources.Load<Font>("CampusFont");
        if (!font) return null;

        var go = new GameObject("Text_" + text);
        Vector3 worldPos = parent.TransformPoint(localPos);
        Quaternion worldRot = parent.rotation * localRot;

        // Parent to parent.parent so text does not inherit non-uniform scaling of box signs
        Transform targetParent = parent.parent != null ? parent.parent : parent;
        go.transform.SetParent(targetParent, true);
        go.transform.position = worldPos;
        go.transform.rotation = worldRot;
        go.transform.localScale = Vector3.one;

        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.text = text;
        tm.fontSize = fontSize;
        tm.characterSize = charSize;
        tm.alignment = TextAlignment.Center;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.color = color ?? Color.white;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        return go;
    }

    // --- 1. Base Architecture Shell (36m wide x 32m deep, total height 12.9m) ---
    static void BuildArchitectureShell(Transform parent)
    {
        var structRoot = new GameObject("00_Architecture_Shell").transform;
        structRoot.SetParent(parent, false);

        // 1F Ground Floor: Warm Terrazzo
        MakeBox(structRoot, "Floor_1F_Main", new Vector3(0, -0.1f, 0), new Vector3(36, 0.2f, 32), matFloorTerrazzo);

        // 1F Entrance Basalt Thresholds
        MakeBox(structRoot, "Floor_1F_Threshold_West", new Vector3(-16.8f, 0.005f, -2.0f), new Vector3(2.4f, 0.01f, 4.2f), matFloorDarkBasalt, false);
        MakeBox(structRoot, "Floor_1F_Threshold_Southeast", new Vector3(12.0f, 0.005f, -14.8f), new Vector3(4.2f, 0.01f, 2.4f), matFloorDarkBasalt, false);

        // 2F Floor Slab (Y = 4.225m, thickness 0.15m), with cutouts for Stair 1 (climbing West) and Stair 2 (climbing North)
        // Stair 1 opening: X in [-17.2, -10.2], Z in [-13.9, -11.1] (climbing Westwards, landing at West X = -17.44)
        // Stair 2 opening: X in [4.8, 7.2], Z in [-15.2, -8.8] (climbing Northwards, landing at North Z = -8.84)
        var slabRoot = new GameObject("Floor_2F_Slab").transform;
        slabRoot.SetParent(structRoot, false);

        // North Main Slab: Z in [-8.8, 16.0] (depth 24.8m, center Z = 3.6m, width 36m)
        MakeBox(slabRoot, "Slab_North_Main", new Vector3(0, 4.225f, 3.6f), new Vector3(36, 0.15f, 24.8f), matFloorTerrazzo);

        // South Section: Z in [-16.0, -8.8] (depth 7.2m)
        MakeBox(slabRoot, "Slab_South_West_Landing", new Vector3(-17.6f, 4.225f, -12.4f), new Vector3(0.8f, 0.15f, 7.2f), matFloorTerrazzo);
        MakeBox(slabRoot, "Slab_South_Stair1_South", new Vector3(-13.7f, 4.225f, -14.95f), new Vector3(7.0f, 0.15f, 2.1f), matFloorTerrazzo);
        MakeBox(slabRoot, "Slab_South_Stair1_North", new Vector3(-13.7f, 4.225f, -9.95f), new Vector3(7.0f, 0.15f, 2.3f), matFloorTerrazzo);
        MakeBox(slabRoot, "Slab_South_Center", new Vector3(-2.7f, 4.225f, -12.4f), new Vector3(15.0f, 0.15f, 7.2f), matFloorTerrazzo);
        MakeBox(slabRoot, "Slab_South_Stair2_South", new Vector3(6.0f, 4.225f, -15.6f), new Vector3(2.4f, 0.15f, 0.8f), matFloorTerrazzo);
        MakeBox(slabRoot, "Slab_South_East", new Vector3(12.6f, 4.225f, -12.4f), new Vector3(10.8f, 0.15f, 7.2f), matFloorTerrazzo);

        // 2F Stairwell Safety Guardrails (护栏)
        MakeBox(slabRoot, "Rail_Stair1_North", new Vector3(-13.7f, 4.85f, -11.1f), new Vector3(7.0f, 1.1f, 0.06f), matMetalMatteBlack, false);
        MakeBox(slabRoot, "Rail_Stair1_South", new Vector3(-13.7f, 4.85f, -13.9f), new Vector3(7.0f, 1.1f, 0.06f), matMetalMatteBlack, false);
        MakeBox(slabRoot, "Rail_Stair1_East", new Vector3(-10.2f, 4.85f, -12.5f), new Vector3(0.06f, 1.1f, 2.8f), matMetalMatteBlack, false);

        MakeBox(slabRoot, "Rail_Stair2_West", new Vector3(4.8f, 4.85f, -12.0f), new Vector3(0.06f, 1.1f, 6.4f), matMetalMatteBlack, false);
        MakeBox(slabRoot, "Rail_Stair2_East", new Vector3(7.2f, 4.85f, -12.0f), new Vector3(0.06f, 1.1f, 6.4f), matMetalMatteBlack, false);
        MakeBox(slabRoot, "Rail_Stair2_South", new Vector3(6.0f, 4.85f, -15.2f), new Vector3(2.4f, 1.1f, 0.06f), matMetalMatteBlack, false);

        // 3F Floor Slab (Y = 8.525m, thickness 0.15m), with cutouts for Stair 1 (climbing West) and Stair 2 (climbing North)
        var slab3FRoot = new GameObject("Floor_3F_Slab").transform;
        slab3FRoot.SetParent(structRoot, false);

        MakeBox(slab3FRoot, "Slab_North_Main", new Vector3(0, 8.525f, 3.6f), new Vector3(36, 0.15f, 24.8f), matFloorTerrazzo);
        MakeBox(slab3FRoot, "Slab_South_West_Landing", new Vector3(-17.6f, 8.525f, -12.4f), new Vector3(0.8f, 0.15f, 7.2f), matFloorTerrazzo);
        MakeBox(slab3FRoot, "Slab_South_Stair1_South", new Vector3(-13.7f, 8.525f, -14.95f), new Vector3(7.0f, 0.15f, 2.1f), matFloorTerrazzo);
        MakeBox(slab3FRoot, "Slab_South_Stair1_North", new Vector3(-13.7f, 8.525f, -9.95f), new Vector3(7.0f, 0.15f, 2.3f), matFloorTerrazzo);
        MakeBox(slab3FRoot, "Slab_South_Center", new Vector3(-2.7f, 8.525f, -12.4f), new Vector3(15.0f, 0.15f, 7.2f), matFloorTerrazzo);
        MakeBox(slab3FRoot, "Slab_South_Stair2_South", new Vector3(6.0f, 8.525f, -15.6f), new Vector3(2.4f, 0.15f, 0.8f), matFloorTerrazzo);
        MakeBox(slab3FRoot, "Slab_South_East", new Vector3(12.6f, 8.525f, -12.4f), new Vector3(10.8f, 0.15f, 7.2f), matFloorTerrazzo);

        // 3F Stairwell Safety Guardrails (护栏)
        MakeBox(slab3FRoot, "Rail_Stair1_North", new Vector3(-13.7f, 9.15f, -11.1f), new Vector3(7.0f, 1.1f, 0.06f), matMetalMatteBlack, false);
        MakeBox(slab3FRoot, "Rail_Stair1_South", new Vector3(-13.7f, 9.15f, -13.9f), new Vector3(7.0f, 1.1f, 0.06f), matMetalMatteBlack, false);
        MakeBox(slab3FRoot, "Rail_Stair1_East", new Vector3(-10.2f, 9.15f, -12.5f), new Vector3(0.06f, 1.1f, 2.8f), matMetalMatteBlack, false);

        MakeBox(slab3FRoot, "Rail_Stair2_West", new Vector3(4.8f, 9.15f, -12.0f), new Vector3(0.06f, 1.1f, 6.4f), matMetalMatteBlack, false);
        MakeBox(slab3FRoot, "Rail_Stair2_East", new Vector3(7.2f, 9.15f, -12.0f), new Vector3(0.06f, 1.1f, 6.4f), matMetalMatteBlack, false);
        MakeBox(slab3FRoot, "Rail_Stair2_South", new Vector3(6.0f, 9.15f, -15.2f), new Vector3(2.4f, 1.1f, 0.06f), matMetalMatteBlack, false);

        // Roof / 3F Ceiling (Y = 12.9m)
        MakeBox(structRoot, "Ceiling_Roof", new Vector3(0, 12.9f, 0), new Vector3(36, 0.2f, 32), matCeilingWhite, false);

        // Perimeter Walls (Height = 12.9m, Center Y = 6.45m)
        // North wall (Z = +16) - Solid perimeter
        MakeBox(structRoot, "Wall_North", new Vector3(0, 6.45f, 16), new Vector3(36, 12.9f, 0.3f), matWallMicrocement);

        // East wall (X = +18) - Solid perimeter
        MakeBox(structRoot, "Wall_East", new Vector3(18, 6.45f, 0), new Vector3(0.3f, 12.9f, 32), matWallMicrocement);

        // West wall (X = -18) - 1F West Door at Z = -2.0m (width 4.0m, height 3.0m)
        // West Wall North Part (Z: 0 to 16, length 16m)
        MakeBox(structRoot, "Wall_West_North", new Vector3(-18, 6.45f, 8.0f), new Vector3(0.3f, 12.9f, 16.0f), matWallMicrocement);
        // West Wall South Part (Z: -16 to -4, length 12m)
        MakeBox(structRoot, "Wall_West_South", new Vector3(-18, 6.45f, -10.0f), new Vector3(0.3f, 12.9f, 12.0f), matWallMicrocement);
        // West Door Lintel (Z: -4 to 0, height 3.0 to 12.9, center Y = 7.95m, height 9.9m)
        MakeBox(structRoot, "Wall_West_DoorLintel", new Vector3(-18, 7.95f, -2.0f), new Vector3(0.3f, 9.9f, 4.0f), matWallMicrocement);

        // South wall (Z = -16) - 1F Southeast Door at X = 12.0m (width 4.0m, height 3.0m)
        // South Wall West Part (X: -18 to 10, length 28m)
        MakeBox(structRoot, "Wall_South_West", new Vector3(-4.0f, 6.45f, -16), new Vector3(28.0f, 12.9f, 0.3f), matWallMicrocement);
        // South Wall East Part (X: 14 to 18, length 4m)
        MakeBox(structRoot, "Wall_South_East", new Vector3(16.0f, 6.45f, -16), new Vector3(4.0f, 12.9f, 0.3f), matWallMicrocement);
        // South Door Lintel (X: 10 to 14, height 3.0 to 12.9, center Y = 7.95m, height 9.9m)
        MakeBox(structRoot, "Wall_South_DoorLintel", new Vector3(12.0f, 7.95f, -16), new Vector3(4.0f, 9.9f, 0.3f), matWallMicrocement);

        // Baseboards for 1F
        MakeBox(structRoot, "Baseboard_1F_North", new Vector3(0, 0.08f, 15.82f), new Vector3(36.0f, 0.16f, 0.05f), matWallDarkCharcoal, false);
        MakeBox(structRoot, "Baseboard_1F_East", new Vector3(17.82f, 0.08f, 0), new Vector3(0.05f, 0.16f, 32.0f), matWallDarkCharcoal, false);

        // Baseboards for 2F
        MakeBox(structRoot, "Baseboard_2F_North", new Vector3(0, 4.38f, 15.82f), new Vector3(36.0f, 0.16f, 0.05f), matWallDarkCharcoal, false);
        MakeBox(structRoot, "Baseboard_2F_East", new Vector3(17.82f, 4.38f, 0), new Vector3(0.05f, 0.16f, 32.0f), matWallDarkCharcoal, false);

        // Baseboards for 3F
        MakeBox(structRoot, "Baseboard_3F_North", new Vector3(0, 8.68f, 15.82f), new Vector3(36.0f, 0.16f, 0.05f), matWallDarkCharcoal, false);
        MakeBox(structRoot, "Baseboard_3F_East", new Vector3(17.82f, 8.68f, 0), new Vector3(0.05f, 0.16f, 32.0f), matWallDarkCharcoal, false);

        // Structural Support Columns (Columns pass through 1F, 2F and 3F)
        Vector3[] colCoords = {
            new Vector3(-6, 0, 4), new Vector3(4, 0, 4),
            new Vector3(-6, 0, -6), new Vector3(4, 0, -6)
        };
        foreach (var c in colCoords)
        {
            var colRoot = new GameObject("Column_MultiFloor").transform;
            colRoot.SetParent(structRoot, false);

            // 1F lower basalt base (0 to 1.1m)
            MakeBox(colRoot, "Col_1F_Base", c + new Vector3(0, 0.55f, 0), new Vector3(0.82f, 1.1f, 0.82f), matWallDarkCharcoal);
            MakeBox(colRoot, "Col_1F_Shaft", c + new Vector3(0, 2.65f, 0), new Vector3(0.80f, 3.1f, 0.80f), matWallMicrocement);

            // 2F lower basalt base (4.3 to 5.4m)
            MakeBox(colRoot, "Col_2F_Base", c + new Vector3(0, 4.85f, 0), new Vector3(0.82f, 1.1f, 0.82f), matWallDarkCharcoal);
            MakeBox(colRoot, "Col_2F_Shaft", c + new Vector3(0, 6.95f, 0), new Vector3(0.80f, 3.1f, 0.80f), matWallMicrocement);

            // 3F lower basalt base (8.6 to 9.7m)
            MakeBox(colRoot, "Col_3F_Base", c + new Vector3(0, 9.15f, 0), new Vector3(0.82f, 1.1f, 0.82f), matWallDarkCharcoal);
            MakeBox(colRoot, "Col_3F_Shaft", c + new Vector3(0, 11.25f, 0), new Vector3(0.80f, 3.1f, 0.80f), matWallMicrocement);
        }
    }

    // --- 2. Staircases (楼梯 1 与 楼梯 2，将一层、二层与三层无缝连接) ---
    static void BuildStaircases(Transform parent)
    {
        var sRoot = new GameObject("00_Vertical_Circulation").transform;
        sRoot.SetParent(parent, false);

        // 楼梯 1 (1F -> 2F): 位于西南角, 从东侧 (X = -10.4m) 向西爬升到 2F (X = -17.44m, Y = 4.3m)
        BuildWestboundConnectingStairs(sRoot, "Stairs_1_1F_to_2F", new Vector3(-10.4f, 0, -12.5f), 22, 2.4f, "楼梯 1", 1, 2);

        // 楼梯 1 (2F -> 3F): 位于西南角, 从东侧 (X = -10.4m, Y = 4.3m) 向西爬升到 3F (X = -17.44m, Y = 8.6m)
        BuildWestboundConnectingStairs(sRoot, "Stairs_1_2F_to_3F", new Vector3(-10.4f, 4.3f, -12.5f), 22, 2.4f, "楼梯 1", 2, 3);

        // 楼梯 2 (1F -> 2F): 位于东南角, 紧靠东南门西侧，从南侧 (Z = -15.0m) 向北爬升至 Z = -8.84m 到达 2F
        BuildSingleConnectingStairs(sRoot, "Stairs_2_1F_to_2F", new Vector3(6.0f, 0, -15.0f), 22, 2.0f, "楼梯 2", 1, 2);

        // 楼梯 2 (2F -> 3F): 位于东南角, 从南侧 (Z = -15.0m, Y = 4.3m) 向北爬升至 Z = -8.84m 到达 3F
        BuildSingleConnectingStairs(sRoot, "Stairs_2_2F_to_3F", new Vector3(6.0f, 4.3f, -15.0f), 22, 2.0f, "楼梯 2", 2, 3);
    }

    // 楼梯 1 朝西攀升 (Climbing West along -X, 从右向左升高)
    static void BuildWestboundConnectingStairs(Transform parent, string name, Vector3 origin, int steps, float width, string label, int fromFloor, int toFloor)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);

        float totalHeight = 4.3f;
        float stepHeight = totalHeight / steps; // ~0.1955m
        float stepDepth = 0.32f;                // Total run ~ 7.04m

        for (int i = 0; i < steps; i++)
        {
            Vector3 pos = origin + new Vector3(-(i + 0.5f) * stepDepth, (i + 0.5f) * stepHeight, 0);
            // Riser: Dark Charcoal
            MakeBox(root, "Riser_" + i, pos, new Vector3(stepDepth, stepHeight, width), matWallDarkCharcoal);
            // Tread Top: Warm Scandinavian Oak
            MakeBox(root, "Tread_" + i, pos + new Vector3(0, stepHeight * 0.5f - 0.015f, 0), new Vector3(stepDepth + 0.03f, 0.03f, width + 0.02f), matWoodScandiOak);
        }

        float totalRun = steps * stepDepth;
        float totalRise = steps * stepHeight;

        // South Handrail (along Z = origin.z - width*0.5 - 0.04)
        Vector3 southRailPos = origin + new Vector3(-totalRun * 0.5f, totalRise * 0.5f + 0.9f, -width * 0.5f - 0.04f);
        var southRail = MakeBox(root, "Handrail_South", southRailPos, new Vector3(totalRun, 0.05f, 0.05f), matMetalMatteBlack, false);
        southRail.transform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(totalRise, totalRun) * Mathf.Rad2Deg);

        // North Handrail (along Z = origin.z + width*0.5 + 0.04)
        Vector3 northRailPos = origin + new Vector3(-totalRun * 0.5f, totalRise * 0.5f + 0.9f, width * 0.5f + 0.04f);
        var northRail = MakeBox(root, "Handrail_North", northRailPos, new Vector3(totalRun, 0.05f, 0.05f), matMetalMatteBlack, false);
        northRail.transform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(totalRise, totalRun) * Mathf.Rad2Deg);

        // Lower Entrance Signboard (facing East +X, towards player entering)
        var signEntry = MakeBox(root, "Sign_Entry", origin + new Vector3(0.4f, 2.6f, 0), new Vector3(0.06f, 0.45f, width + 0.4f), matSignBlackBox, false);
        string toName = (toFloor == 2) ? "二层特色餐厅" : "三层自选餐厅 / 鲜果超市";
        MakeText(signEntry.transform, $"▲ {label} · 朝西上楼 通往{toName}", new Vector3(0.04f, 0, 0), Quaternion.Euler(0, -90, 0), 22, 0.032f, Color.white);

        // Upper Landing Signboard (facing West -X, towards player arriving)
        Vector3 topLanding = origin + new Vector3(-totalRun, totalRise, 0);
        var signTop = MakeBox(root, "Sign_Top", topLanding + new Vector3(-0.4f, 2.5f, 0), new Vector3(0.06f, 0.40f, width + 0.2f), matSignBlackBox, false);
        string fromName = (fromFloor == 1) ? "一层大众餐厅" : "二层特色餐厅";
        MakeText(signTop.transform, $"▼ {label} · 返回{fromName}", new Vector3(-0.04f, 0, 0), Quaternion.Euler(0, 90, 0), 22, 0.032f, Color.white);
    }

    static void BuildSingleConnectingStairs(Transform parent, string name, Vector3 origin, int steps, float width, string label, int fromFloor, int toFloor)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);

        float totalHeight = 4.3f;
        float stepHeight = totalHeight / steps; // ~0.1955m
        float stepDepth = 0.28f;

        for (int i = 0; i < steps; i++)
        {
            Vector3 pos = origin + new Vector3(0, (i + 0.5f) * stepHeight, (i + 0.5f) * stepDepth);
            // Riser: Dark Charcoal
            MakeBox(root, "Riser_" + i, pos, new Vector3(width, stepHeight, stepDepth), matWallDarkCharcoal);
            // Tread Top: Warm Scandinavian Oak
            MakeBox(root, "Tread_" + i, pos + new Vector3(0, stepHeight * 0.5f - 0.015f, 0), new Vector3(width + 0.02f, 0.03f, stepDepth + 0.03f), matWoodScandiOak);
        }

        float totalRun = steps * stepDepth;
        float totalRise = steps * stepHeight;

        // West Handrail
        Vector3 westRailPos = origin + new Vector3(-width * 0.5f - 0.04f, totalRise * 0.5f + 0.9f, totalRun * 0.5f);
        var westRail = MakeBox(root, "Handrail_West", westRailPos, new Vector3(0.05f, 0.05f, totalRun), matMetalMatteBlack, false);
        westRail.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(totalRise, totalRun) * Mathf.Rad2Deg, 0, 0);

        // East Handrail
        Vector3 eastRailPos = origin + new Vector3(width * 0.5f + 0.04f, totalRise * 0.5f + 0.9f, totalRun * 0.5f);
        var eastRail = MakeBox(root, "Handrail_East", eastRailPos, new Vector3(0.05f, 0.05f, totalRun), matMetalMatteBlack, false);
        eastRail.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(totalRise, totalRun) * Mathf.Rad2Deg, 0, 0);

        // Lower Entrance Signboard
        var signEntry = MakeBox(root, "Sign_Entry", origin + new Vector3(0, 2.6f, -0.4f), new Vector3(2.4f, 0.45f, 0.06f), matSignBlackBox, false);
        string toName = (toFloor == 2) ? "二层特色餐厅" : "三层自选餐厅 / 鲜果超市";
        MakeText(signEntry.transform, $"▲ {label} · 朝北上楼 通往{toName}", new Vector3(0, 0, -0.04f), Quaternion.Euler(0, 0, 0), 22, 0.032f, Color.white);

        // Upper Landing Signboard
        Vector3 topLanding = origin + new Vector3(0, totalRise, totalRun);
        var signTop = MakeBox(root, "Sign_Top", topLanding + new Vector3(0, 2.5f, 0.4f), new Vector3(2.2f, 0.40f, 0.06f), matSignBlackBox, false);
        string fromName = (fromFloor == 1) ? "一层大众餐厅" : "二层特色餐厅";
        MakeText(signTop.transform, $"▼ {label} · 返回{fromName}", new Vector3(0, 0, 0.04f), Quaternion.Euler(0, 180, 0), 22, 0.032f, Color.white);
    }

    // --- 3 & 4. Floor Interior Layout (1F & 2F share the schematic layout!) ---
    static void BuildFloorInterior(Transform floorRoot, float baseY, int floorNum)
    {
        string prefix = (floorNum == 1) ? "1F" : "2F";

        // A. Cutlery Station (西门内侧北侧: X = -16.5, Z = 4.5)
        BuildCutleryStation(floorRoot, baseY, prefix);

        // B. L-Shaped Food Serving Area (西北向东延伸至 X=3.5，再向南转向至 Z=1.0)
        BuildFoodServingArea(floorRoot, baseY, prefix, floorNum);

        // C. Kitchen Area (东北角: X from 5.5 to 18, Z from 6.5 to 16)
        BuildKitchenArea(floorRoot, baseY, prefix, floorNum);

        // D. Tray Return Area (后厨正下方东墙侧: X from 7.0 to 17.5, Z from 2.5 to 5.8)
        BuildTrayReturnArea(floorRoot, baseY, prefix);

        // E. Restroom Alcove (嵌入东墙侧: 紧贴东墙 X in [17.5, 17.85], Z in [-7.0, 1.0], 全交互无实体隔间阻挡)
        BuildRestroomFlushAlcove(floorRoot, baseY, prefix);

        // F. Water Bar Island (中央区域，自选点餐通道尽头西侧: X = -0.5, Z = -1.0)
        BuildWaterBarIsland(floorRoot, baseY, prefix, floorNum);

        // G. Dining Hall Seating (用餐区: 标准四人/六人桌椅列阵)
        BuildDiningArea(floorRoot, baseY, prefix, floorNum);

        // H. Modern Greenery Planter Partitions (绿植隔离带)
        BuildGreeneryPartitions(floorRoot, baseY, prefix);
    }

    // A. Cutlery Station
    static void BuildCutleryStation(Transform parent, float baseY, string prefix)
    {
        var root = new GameObject(prefix + "_Cutlery_Station").transform;
        root.SetParent(parent, false);

        Vector3 pos = new Vector3(-16.5f, baseY, 4.5f);

        MakeBox(root, "Cabinet_Body", pos + new Vector3(0, 0.45f, 0), new Vector3(2.4f, 0.9f, 0.9f), matMetalTitanium);
        MakeBox(root, "Cabinet_Top", pos + new Vector3(0, 0.92f, 0), new Vector3(2.45f, 0.04f, 0.95f), matMetalStainless);

        MakeBox(root, "Trays_Sage", pos + new Vector3(-0.7f, 1.15f, 0), new Vector3(0.45f, 0.25f, 0.35f), matChairSage, false);
        MakeBox(root, "Trays_Cream", pos + new Vector3(0f, 1.15f, 0), new Vector3(0.45f, 0.25f, 0.35f), matChairCream, false);

        MakeBox(root, "Chopstick_Box", pos + new Vector3(0.7f, 1.12f, 0.15f), new Vector3(0.35f, 0.2f, 0.25f), matMetalStainless, false);
        MakeBox(root, "Spoon_Box", pos + new Vector3(0.7f, 1.12f, -0.15f), new Vector3(0.35f, 0.2f, 0.25f), matMetalStainless, false);

        var sign = MakeBox(root, "Sign_Cutlery", pos + new Vector3(0, 2.2f, 0), new Vector3(2.4f, 0.38f, 0.06f), matSignBlackBox, false);
        MakeText(sign.transform, prefix + " 餐具取放处 · 请有序取餐", new Vector3(0, 0, -0.04f), Quaternion.Euler(0, 0, 0), 24, 0.035f, Color.white);
    }

    // B. Food Serving Area (L-Shaped: Horizontal from X=-13 to 3.5 at Z=12; Vertical from Z=12 down to 1.0 at X=3.5)
    static void BuildFoodServingArea(Transform parent, float baseY, string prefix, int floorNum)
    {
        var root = new GameObject(prefix + "_Food_Serving_Area").transform;
        root.SetParent(parent, false);

        // 1. Horizontal Arm along North: from X = -13.0m to X = 3.5m at Z = 12.0m (Length = 16.5m)
        Vector3 hCenter = new Vector3(-4.75f, baseY + 0.45f, 12.0f);
        MakeBox(root, "Counter_Horiz_Body", hCenter, new Vector3(16.5f, 0.9f, 1.0f), matMetalTitanium);
        MakeBox(root, "Counter_Horiz_Top", hCenter + new Vector3(0, 0.46f, 0), new Vector3(16.5f, 0.03f, 1.06f), matMetalStainless);
        // Sneeze guard glass on customer side (South side of counter)
        MakeBox(root, "Glass_Horiz", hCenter + new Vector3(0, 0.70f, -0.48f), new Vector3(16.5f, 0.5f, 0.04f), matGlassUltra, false);
        MakeBox(root, "Tray_Rail_Horiz", hCenter + new Vector3(0, 0.40f, -0.75f), new Vector3(16.5f, 0.06f, 0.35f), matMetalStainless, false);

        // 2. Vertical Arm (自选点餐通道): from Z = 12.0m down to Z = 1.0m at X = 3.5m (Length = 11.0m)
        Vector3 vCenter = new Vector3(3.5f, baseY + 0.45f, 6.5f);
        MakeBox(root, "Counter_Vert_Body", vCenter, new Vector3(1.0f, 0.9f, 11.0f), matMetalTitanium);
        MakeBox(root, "Counter_Vert_Top", vCenter + new Vector3(0, 0.46f, 0), new Vector3(1.06f, 0.03f, 11.0f), matMetalStainless);
        // Sneeze guard glass on customer side (West side of counter)
        MakeBox(root, "Glass_Vert", vCenter + new Vector3(-0.48f, 0.70f, 0), new Vector3(0.04f, 0.5f, 11.0f), matGlassUltra, false);
        MakeBox(root, "Tray_Rail_Vert", vCenter + new Vector3(-0.75f, 0.40f, 0), new Vector3(0.35f, 0.06f, 11.0f), matMetalStainless, false);

        // Corner block at (3.5, 12.0)
        MakeBox(root, "Counter_Corner", new Vector3(3.5f, baseY + 0.45f, 12.0f), new Vector3(1.0f, 0.9f, 1.0f), matMetalTitanium);
        MakeBox(root, "Counter_Corner_Top", new Vector3(3.5f, baseY + 0.91f, 12.0f), new Vector3(1.06f, 0.03f, 1.06f), matMetalStainless);

        // Food warmer pans with 3D dishes
        Material[] foodMats = { matFoodBeef, matFoodGolden, matFoodGreen, matFoodRed, matFoodEgg, matFoodBeef, matMarbleWhite };
        for (int i = 0; i < 6; i++)
        {
            Vector3 panPos = new Vector3(-11.5f + i * 2.3f, baseY + 0.93f, 12.0f);
            MakeBox(root, "Warmer_H_" + i, panPos, new Vector3(1.4f, 0.02f, 0.65f), matMetalStainless, false);
            MakeBox(root, "Food_H_" + i, panPos + new Vector3(0, 0.02f, 0), new Vector3(1.25f, 0.025f, 0.55f), foodMats[i % foodMats.Length], false);
        }

        for (int i = 0; i < 4; i++)
        {
            Vector3 panPos = new Vector3(3.5f, baseY + 0.93f, 10.0f - i * 2.2f);
            MakeBox(root, "Warmer_V_" + i, panPos, new Vector3(0.65f, 0.02f, 1.4f), matMetalStainless, false);
            MakeBox(root, "Food_V_" + i, panPos + new Vector3(0, 0.02f, 0), new Vector3(0.55f, 0.025f, 1.25f), foodMats[(i + 3) % foodMats.Length], false);
        }

        // Overhead Hanging Stall Signboards (4 stalls)
        string[] stallNames1F = { "1号档口 · 精美冷盘", "2号档口 · 经典小炒", "3号档口 · 大工现煮面", "4号档口 · 蒸点主食" };
        string[] stallNames2F = { "1号档口 · 川湘热炒", "2号档口 · 瓦罐砂锅", "3号档口 · 铁板炙烤", "4号档口 · 风味面点" };
        string[] stallNames = (floorNum == 1) ? stallNames1F : stallNames2F;

        Vector3[] signPositions = {
            new Vector3(-10.0f, baseY + 3.2f, 12.0f),
            new Vector3(-4.5f, baseY + 3.2f, 12.0f),
            new Vector3(1.0f, baseY + 3.2f, 12.0f),
            new Vector3(3.5f, baseY + 3.2f, 5.5f)
        };

        for (int i = 0; i < stallNames.Length; i++)
        {
            var sign = MakeBox(root, "Stall_Sign_" + i, signPositions[i],
                i < 3 ? new Vector3(2.8f, 0.45f, 0.12f) : new Vector3(0.12f, 0.45f, 2.8f), matSignBlackBox, false);
            MakeBox(sign.transform, "Sign_Face", new Vector3(0, 0, 0),
                i < 3 ? new Vector3(2.6f, 0.35f, 0.13f) : new Vector3(0.13f, 0.35f, 2.6f), matLightWarmLED, false);
            MakeBox(sign.transform, "Hanger_1", new Vector3(i < 3 ? -0.9f : 0, 0.5f, i < 3 ? 0 : -0.9f), new Vector3(0.03f, 1.0f, 0.03f), matMetalMatteBlack, false);
            MakeBox(sign.transform, "Hanger_2", new Vector3(i < 3 ? 0.9f : 0, 0.5f, i < 3 ? 0 : 0.9f), new Vector3(0.03f, 1.0f, 0.03f), matMetalMatteBlack, false);

            if (i < 3)
            {
                MakeText(sign.transform, stallNames[i], new Vector3(0, 0, -0.08f), Quaternion.Euler(0, 0, 0), 28, 0.038f, Color.white);
            }
            else
            {
                MakeText(sign.transform, stallNames[i], new Vector3(-0.08f, 0, 0), Quaternion.Euler(0, 90, 0), 28, 0.038f, Color.white);
            }
        }

        // Queue Guidance Stanchions along 自选点餐通道 (forming a clean 1.6m wide lane)
        for (int i = 0; i < 5; i++)
        {
            MakeBox(root, "Stanchion_V_" + i, new Vector3(1.8f, baseY + 0.45f, 2.5f + i * 2.0f), new Vector3(0.07f, 0.9f, 0.07f), matMetalMatteBlack, false);
            MakeBox(root, "Base_V_" + i, new Vector3(1.8f, baseY + 0.02f, 2.5f + i * 2.0f), new Vector3(0.26f, 0.04f, 0.26f), matMetalMatteBlack, false);
        }
        MakeBox(root, "Rope_V", new Vector3(1.8f, baseY + 0.85f, 6.5f), new Vector3(0.04f, 0.04f, 9.0f), matChairSlateBlue, false);
    }

    // C. Kitchen Area (Northeast: X from 5.5 to 18, Z from 6.5 to 16)
    static void BuildKitchenArea(Transform parent, float baseY, string prefix, int floorNum)
    {
        var root = new GameObject(prefix + "_Kitchen_Northeast").transform;
        root.SetParent(parent, false);

        // West partition wall: X = 5.5m, Z from 6.5 to 16.0 (Length 9.5m)
        MakeBox(root, "Kitchen_WestWall", new Vector3(5.5f, baseY + 2.0f, 11.25f), new Vector3(0.25f, 4.0f, 9.5f), matWallMicrocement);
        MakeBox(root, "Kitchen_WestWall_Cap", new Vector3(5.5f, baseY + 4.02f, 11.25f), new Vector3(0.35f, 0.05f, 9.5f), matWallDarkCharcoal);

        // South partition wall: Z = 6.5m, X from 5.5 to 18.0 (Length 12.5m)
        // Part Left (X: 5.5 to 10.5)
        MakeBox(root, "Kitchen_SouthWall_Left", new Vector3(8.0f, baseY + 2.0f, 6.5f), new Vector3(5.0f, 4.0f, 0.25f), matWallMicrocement);
        MakeBox(root, "Kitchen_SouthWall_Left_Cap", new Vector3(8.0f, baseY + 4.02f, 6.5f), new Vector3(5.0f, 0.05f, 0.35f), matWallDarkCharcoal);

        // Serving Pass-Through Windows (出餐窗口) at X = 13.0m
        MakeBox(root, "Serving_Window_Sill", new Vector3(13.0f, baseY + 0.85f, 6.5f), new Vector3(4.5f, 0.06f, 0.45f), matMetalStainless);
        MakeBox(root, "Serving_Window_Lintel", new Vector3(13.0f, baseY + 3.2f, 6.5f), new Vector3(4.5f, 1.6f, 0.25f), matWallDarkCharcoal);
        MakeBox(root, "Serving_Window_Glass", new Vector3(13.0f, baseY + 2.1f, 6.5f), new Vector3(4.3f, 0.6f, 0.04f), matGlassUltra, false);
        MakeBox(root, "Kitchen_SouthWall_Right", new Vector3(16.75f, baseY + 2.0f, 6.5f), new Vector3(2.5f, 4.0f, 0.25f), matWallMicrocement);
        MakeBox(root, "Kitchen_SouthWall_Right_Cap", new Vector3(16.75f, baseY + 4.02f, 6.5f), new Vector3(2.5f, 0.05f, 0.35f), matWallDarkCharcoal);

        // Kitchen Interior Equipment
        MakeBox(root, "Kitchen_PrepIsland_1", new Vector3(10.5f, baseY + 0.45f, 11.5f), new Vector3(4.5f, 0.9f, 1.6f), matMetalStainless);
        MakeBox(root, "Kitchen_PrepIsland_2", new Vector3(10.5f, baseY + 0.45f, 8.8f), new Vector3(4.5f, 0.9f, 1.2f), matMetalTitanium);
        MakeBox(root, "Kitchen_Range_Stoves", new Vector3(16.2f, baseY + 0.5f, 11.5f), new Vector3(1.6f, 1.0f, 5.0f), matMetalMatteBlack);

        // Staff Door at X = 6.8m, Z = 6.5m
        var staffDoor = MakeBox(root, "Staff_Door", new Vector3(6.8f, baseY + 1.25f, 6.5f), new Vector3(1.3f, 2.5f, 0.06f), matWoodDarkWalnut);
        MakeBox(staffDoor.transform, "Push_Bar", new Vector3(0, -0.2f, -0.05f), new Vector3(1.0f, 0.06f, 0.06f), matMetalStainless, false);

        var sign = MakeBox(root, "Sign_StaffOnly", new Vector3(6.8f, baseY + 2.65f, 6.45f), new Vector3(1.4f, 0.32f, 0.05f), matSignBlackBox, false);
        MakeText(sign.transform, "后厨重地 · 闲人免进", new Vector3(0, 0, -0.03f), Quaternion.Euler(0, 0, 0), 22, 0.034f, Color.white);

        // Kitchen Identification Wall Sign
        var titleSign = MakeBox(root, "Sign_KitchenTitle", new Vector3(13.0f, baseY + 2.8f, 6.45f), new Vector3(3.8f, 0.42f, 0.05f), matSignBlackBox, false);
        MakeText(titleSign.transform, "后厨 (内部操作间) · 设出餐窗口", new Vector3(0, 0, -0.03f), Quaternion.Euler(0, 0, 0), 22, 0.032f, Color.white);

        // North wall interior backdrop mural
        if (floorNum == 1)
        {
            Vector3 muralPos = new Vector3(11.75f, baseY + 2.5f, 15.82f);
            var mural = MakeBox(root, "Kitchen_Mural", muralPos, new Vector3(10.0f, 2.5f, 0.05f), matKitchenPic, false);
            mural.transform.localRotation = Quaternion.Euler(0, 180, 0);
        }
    }

    // D. Tray Return Area (East side, directly south of kitchen: X in [7.0, 17.5], Z in [2.5, 5.8])
    static void BuildTrayReturnArea(Transform parent, float baseY, string prefix)
    {
        var root = new GameObject(prefix + "_Tray_Return_Station").transform;
        root.SetParent(parent, false);

        Vector3 pos = new Vector3(12.5f, baseY, 4.2f);

        // Multi-tier stainless collection racks along East wall
        MakeBox(root, "Rack_Counter", pos + new Vector3(0, 0.5f, 0), new Vector3(4.5f, 1.0f, 1.6f), matMetalTitanium);
        MakeBox(root, "Rack_Shelves", pos + new Vector3(0, 1.4f, 0), new Vector3(4.3f, 0.8f, 1.5f), matMetalStainless, false);

        // 3 Waste sorting bins with Chinese classification text
        string[] binNames = { "餐厨垃圾", "可回收物", "其他垃圾" };
        Material[] binAccents = { matChairSage, matChairSlateBlue, matWallDarkCharcoal };
        for (int i = 0; i < 3; i++)
        {
            Vector3 bPos = pos + new Vector3(-1.4f + i * 1.4f, 0.45f, -1.3f);
            var bin = MakeBox(root, "Waste_Bin_" + i, bPos, new Vector3(0.65f, 0.9f, 0.65f), matMetalTitanium);
            MakeBox(bin.transform, "Bin_Lid", new Vector3(0, 0.46f, 0), new Vector3(0.67f, 0.04f, 0.67f), binAccents[i], false);
            MakeText(bin.transform, binNames[i], new Vector3(0, 0.12f, -0.34f), Quaternion.Euler(0, 0, 0), 20, 0.03f, Color.white);
        }

        // Area Signboard
        var sign = MakeBox(root, "Sign_TrayReturn", pos + new Vector3(0, 2.6f, -1.0f), new Vector3(3.8f, 0.45f, 0.06f), matSignBlackBox, false);
        MakeText(sign.transform, prefix + " 残餐回收区 (餐盘归还 / 分类垃圾桶)", new Vector3(0, 0, -0.04f), Quaternion.Euler(0, 0, 0), 22, 0.034f, Color.white);
    }

    // E. Restroom Alcove (嵌入式洗手间背景墙与洁手交互区)
    // 紧贴东外墙 (X in [17.4, 17.85], Z in [-7.0, 1.0]), 不做内部封闭实体模型隔间，释放大厅空间，做整体木饰面移门与洁手台交互
    static void BuildRestroomFlushAlcove(Transform parent, float baseY, string prefix)
    {
        var root = new GameObject(prefix + "_Restroom_Flush_Alcove").transform;
        root.SetParent(parent, false);

        // 1. Sleek Charcoal Feature Backing Wall flush on East Wall (X = 17.82m, Z in [-7.0, 1.0], Length = 8.0m)
        MakeBox(root, "WC_Backing_Wall", new Vector3(17.82f, baseY + 2.0f, -3.0f), new Vector3(0.08f, 4.0f, 8.0f), matWallDarkCharcoal);
        MakeBox(root, "WC_Top_Molding", new Vector3(17.78f, baseY + 4.02f, -3.0f), new Vector3(0.16f, 0.05f, 8.0f), matWoodDarkWalnut);
        MakeBox(root, "WC_Base_Kickplate", new Vector3(17.78f, baseY + 0.05f, -3.0f), new Vector3(0.16f, 0.10f, 8.0f), matMetalTitanium);

        // 2. North Door: Female WC Door Panel (Z = -1.0m)
        MakeBox(root, "WC_DoorFrame_F", new Vector3(17.78f, baseY + 1.25f, -1.0f), new Vector3(0.06f, 2.5f, 1.3f), matMetalMatteBlack, false);
        MakeBox(root, "WC_Door_F", new Vector3(17.75f, baseY + 1.2f, -1.0f), new Vector3(0.04f, 2.4f, 1.2f), matWoodDarkWalnut, false);
        MakeBox(root, "WC_Handle_F", new Vector3(17.71f, baseY + 1.0f, -0.55f), new Vector3(0.04f, 0.25f, 0.03f), matMetalStainless, false);
        var signF = MakeBox(root, "Sign_WC_F", new Vector3(17.72f, baseY + 1.85f, -1.0f), new Vector3(0.02f, 0.32f, 0.8f), matSignBlackBox, false);
        MakeText(signF.transform, "♀ 女洗手间 (WC)", new Vector3(-0.02f, 0, 0), Quaternion.Euler(0, 90, 0), 20, 0.032f, Color.white);

        // 3. South Door: Male WC Door Panel (Z = -5.0m)
        MakeBox(root, "WC_DoorFrame_M", new Vector3(17.78f, baseY + 1.25f, -5.0f), new Vector3(0.06f, 2.5f, 1.3f), matMetalMatteBlack, false);
        MakeBox(root, "WC_Door_M", new Vector3(17.75f, baseY + 1.2f, -5.0f), new Vector3(0.04f, 2.4f, 1.2f), matWoodDarkWalnut, false);
        MakeBox(root, "WC_Handle_M", new Vector3(17.71f, baseY + 1.0f, -4.55f), new Vector3(0.04f, 0.25f, 0.03f), matMetalStainless, false);
        var signM = MakeBox(root, "Sign_WC_M", new Vector3(17.72f, baseY + 1.85f, -5.0f), new Vector3(0.02f, 0.32f, 0.8f), matSignBlackBox, false);
        MakeText(signM.transform, "♂ 男洗手间 (WC)", new Vector3(-0.02f, 0, 0), Quaternion.Euler(0, 90, 0), 20, 0.032f, Color.white);

        // 4. Center Double Washbasin & Hygiene Counter (Z = -3.0m, X = 17.48m)
        MakeBox(root, "Washbasin_Body", new Vector3(17.48f, baseY + 0.42f, -3.0f), new Vector3(0.58f, 0.84f, 2.2f), matWallDarkCharcoal);
        MakeBox(root, "Washbasin_Top", new Vector3(17.45f, baseY + 0.86f, -3.0f), new Vector3(0.64f, 0.05f, 2.25f), matMarbleWhite);
        MakeBox(root, "Faucet_1", new Vector3(17.60f, baseY + 1.0f, -3.5f), new Vector3(0.12f, 0.22f, 0.04f), matMetalStainless, false);
        MakeBox(root, "Faucet_2", new Vector3(17.60f, baseY + 1.0f, -2.5f), new Vector3(0.12f, 0.22f, 0.04f), matMetalStainless, false);
        MakeBox(root, "Soap_Dispenser", new Vector3(17.72f, baseY + 1.15f, -3.0f), new Vector3(0.06f, 0.22f, 0.12f), matMetalStainless, false);

        // LED Backlit Vanity Mirror
        MakeBox(root, "Mirror_Glow", new Vector3(17.77f, baseY + 1.6f, -3.0f), new Vector3(0.02f, 1.1f, 2.2f), matLightWarmLED, false);
        MakeBox(root, "Mirror_Glass", new Vector3(17.75f, baseY + 1.6f, -3.0f), new Vector3(0.02f, 1.04f, 2.14f), matGlassUltra, false);

        // 5. Overhead 3D Illuminated Header Signboard
        var signMain = MakeBox(root, "Sign_WC_Main", new Vector3(17.65f, baseY + 2.7f, -3.0f), new Vector3(0.06f, 0.42f, 2.6f), matSignBlackBox, false);
        MakeText(signMain.transform, prefix + " 洗手间 · 洁手交互区 [E 交互]", new Vector3(-0.04f, 0, 0), Quaternion.Euler(0, 90, 0), 22, 0.034f, Color.white);
    }

    // F. Water Bar Island (Central: X = -0.5m, Z = -1.0m, Size: 4.0m x 3.6m)
    static void BuildWaterBarIsland(Transform parent, float baseY, string prefix, int floorNum)
    {
        var root = new GameObject(prefix + "_Water_Bar_Island").transform;
        root.SetParent(parent, false);

        Vector3 center = new Vector3(-0.5f, baseY, -1.0f);

        // Main Bar Counter: Warm Scandinavian Oak Fluted Body + Calacatta Sintered Stone Top
        MakeBox(root, "Bar_Kickplate", center + new Vector3(0, 0.05f, 0), new Vector3(3.8f, 0.1f, 3.4f), matMetalTitanium);
        MakeBox(root, "Bar_Body", center + new Vector3(0, 0.55f, 0), new Vector3(4.0f, 0.9f, 3.6f), matWoodScandiOak);
        MakeBox(root, "Bar_Countertop", center + new Vector3(0, 1.05f, 0), new Vector3(4.3f, 0.1f, 3.9f), matMarbleWhite);

        // Overhead Canopy: Sleek Matte Black Steel Frame with Warm Oak Louvers
        MakeBox(root, "Bar_Canopy_Frame", center + new Vector3(0, 3.2f, 0), new Vector3(4.1f, 0.15f, 3.7f), matMetalMatteBlack, false);
        MakeBox(root, "Bar_Canopy_WoodLouvers", center + new Vector3(0, 3.25f, 0), new Vector3(3.9f, 0.08f, 3.5f), matWoodScandiOak, false);
        MakeBox(root, "Bar_Canopy_Downlight", center + new Vector3(0, 3.12f, 0), new Vector3(3.0f, 0.02f, 2.6f), matLightWarmLED, false);

        MakeBox(root, "Pillar_NW", center + new Vector3(-1.9f, 2f, 1.7f), new Vector3(0.08f, 2.3f, 0.08f), matMetalMatteBlack, false);
        MakeBox(root, "Pillar_NE", center + new Vector3(1.9f, 2f, 1.7f), new Vector3(0.08f, 2.3f, 0.08f), matMetalMatteBlack, false);
        MakeBox(root, "Pillar_SW", center + new Vector3(-1.9f, 2f, -1.7f), new Vector3(0.08f, 2.3f, 0.08f), matMetalMatteBlack, false);
        MakeBox(root, "Pillar_SE", center + new Vector3(1.9f, 2f, -1.7f), new Vector3(0.08f, 2.3f, 0.08f), matMetalMatteBlack, false);

        // Commercial Espresso Machine
        MakeBox(root, "Espresso_Body", center + new Vector3(0.8f, 1.35f, 0.4f), new Vector3(0.75f, 0.5f, 0.55f), matMetalStainless, false);
        MakeBox(root, "Espresso_Cups", center + new Vector3(0.8f, 1.68f, 0.4f), new Vector3(0.6f, 0.08f, 0.4f), matMarbleWhite, false);

        // Refrigerated Pastry Display Showcase
        MakeBox(root, "Pastry_Case", center + new Vector3(-0.6f, 1.28f, 0.4f), new Vector3(1.0f, 0.38f, 0.5f), matGlassUltra, false);
        MakeBox(root, "Pastry_Croissant", center + new Vector3(-0.6f, 1.15f, 0.4f), new Vector3(0.5f, 0.06f, 0.2f), matFoodGolden, false);

        // Cold Drink Dispenser & POS Register
        MakeBox(root, "Drink_Dispenser", center + new Vector3(1.0f, 1.45f, -0.6f), new Vector3(0.6f, 0.7f, 0.5f), matMetalTitanium);
        MakeBox(root, "POS_Terminal", center + new Vector3(-1.4f, 1.25f, -0.8f), new Vector3(0.45f, 0.35f, 0.4f), matMetalMatteBlack);

        // Mini Menu Board
        var menuBoard = MakeBox(root, "Menu_Board", center + new Vector3(-0.6f, 1.28f, -1.55f), new Vector3(0.8f, 0.45f, 0.04f), matSignBlackBox, false);
        menuBoard.transform.localRotation = Quaternion.Euler(-15f, 180f, 0f);
        MakeText(menuBoard.transform, "拿铁 ¥12 · 美式 ¥9\n生椰奶茶 ¥10 · 鲜橙茉莉 ¥8", new Vector3(0, 0, -0.03f), Quaternion.identity, 22, 0.028f, Color.white);

        // Overhead Large Signplate "水吧 (饮品 / 奶茶 / 收银岛台)"
        var sign = MakeBox(root, "Sign_WaterBar", center + new Vector3(0, 3.2f, -1.88f), new Vector3(3.6f, 0.5f, 0.06f), matSignBlackBox, false);
        MakeBox(sign.transform, "Sign_GlowText", new Vector3(0, 0, -0.01f), new Vector3(3.4f, 0.35f, 0.02f), matLightWarmLED, false);
        MakeText(sign.transform, prefix + " 水吧 (饮品 / 奶茶 / 收银岛台)", new Vector3(0, 0, -0.05f), Quaternion.Euler(0, 0, 0), 26, 0.038f, Color.white);
    }

    // G. Dining Area (用餐区 - 四人座 / 六人座标准校园餐桌椅列阵)
    static void BuildDiningArea(Transform parent, float baseY, string prefix, int floorNum)
    {
        var root = new GameObject(prefix + "_Dining_Area").transform;
        root.SetParent(parent, false);

        int tableIdx = 0;

        // Zone 1: North-Central Dining (X from -11.0 to 1.5, Z from 4.5 to 9.5) - below the horizontal food counter
        for (float x = -10.5f; x <= 0.5f; x += 3.2f)
        {
            for (float z = 5.0f; z <= 9.0f; z += 3.0f)
            {
                BuildTableSet(root, new Vector3(x, baseY, z), tableIdx++, prefix);
            }
        }

        // Zone 2: Central-South Dining (X from -11.5 to 3.5, Z from -6.5 to 1.5) - West of water bar
        for (float x = -11.5f; x <= -2.5f; x += 3.2f)
        {
            for (float z = -6.5f; z <= 1.5f; z += 2.8f)
            {
                BuildTableSet(root, new Vector3(x, baseY, z), tableIdx++, prefix);
            }
        }

        // Zone 3: South Dining Hall (X from -11.5 to 3.5, Z from -13.5 to -8.5) - between the two stairs
        for (float x = -11.5f; x <= 3.5f; x += 3.2f)
        {
            for (float z = -14.0f; z <= -9.0f; z += 2.8f)
            {
                // Leave clearance near Stair 1 entrance (X=-10.4m, Z=-12.5m)
                if (x < -8.5f && z < -10.5f) continue;
                BuildTableSet(root, new Vector3(x, baseY, z), tableIdx++, prefix);
            }
        }

        // Zone 4: East Dining Corridor (X = 13.5m, Z from -6.0 to -1.0) - opened up by flush restroom alcove
        for (float z = -6.0f; z <= -1.0f; z += 2.5f)
        {
            BuildTableSet(root, new Vector3(13.5f, baseY, z), tableIdx++, prefix);
        }
    }

    // Every chair built for the cafeteria records its seat anchor: standing position and
    // the yaw that faces the table. The ordering system consumes this list, so seats match
    // the furniture actually in the scene instead of hand-written guesses.
    static readonly System.Collections.Generic.List<Vector3> seatAnchorPos = new System.Collections.Generic.List<Vector3>();
    static readonly System.Collections.Generic.List<Quaternion> seatAnchorRot = new System.Collections.Generic.List<Quaternion>();

    static void BuildTableSet(Transform parent, Vector3 center, int tableIndex, string prefix)
    {
        var tRoot = new GameObject("TableSet_" + tableIndex).transform;
        tRoot.SetParent(parent, false);

        Material[] chairPalette = { matChairCream, matChairSage, matChairTerracotta, matChairSlateBlue };
        Material chairMat = chairPalette[tableIndex % chairPalette.Length];

        bool isSixSeater = (tableIndex % 4 == 1);
        float tableLen = isSixSeater ? 1.9f : 1.4f;

        // Table top: bevelled slab with a darker edge band and round tube legs, so the
        // silhouette reads as furniture rather than a stack of cubes.
        InteriorGeometry.RoundedBox(tRoot, "Table_Top", center + new Vector3(0, 0.75f, 0),
            new Vector3(tableLen, 0.05f, 0.8f), 0.022f, matWoodScandiOak, true);
        InteriorGeometry.RoundedBox(tRoot, "Table_Edge", center + new Vector3(0, 0.722f, 0),
            new Vector3(tableLen - 0.01f, 0.012f, 0.79f), 0.006f, matWoodDarkWalnut, false);

        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
            {
                var a = center + new Vector3(sx * tableLen * 0.40f, 0.02f, sz * 0.28f);
                var b = center + new Vector3(sx * tableLen * 0.40f, 0.72f, sz * 0.28f);
                InteriorGeometry.Tube(tRoot, "Table_Leg", a, b, 0.024f, matMetalMatteBlack);
            }
        // Slim stretcher tying the legs together.
        InteriorGeometry.Tube(tRoot, "Table_Stretcher",
            center + new Vector3(-tableLen * 0.40f, 0.16f, 0f),
            center + new Vector3(tableLen * 0.40f, 0.16f, 0f), 0.016f, matMetalMatteBlack);

        // Table Number Stand
        var numStand = MakeBox(tRoot, "Table_Num", center + new Vector3(-0.4f, 0.83f, 0), new Vector3(0.12f, 0.12f, 0.04f), matSignBlackBox, false);
        MakeText(numStand.transform, prefix + "-" + (tableIndex + 1), new Vector3(0, 0, -0.03f), Quaternion.identity, 20, 0.03f, Color.white);
        MakeBox(tRoot, "Condiments", center + new Vector3(0.4f, 0.80f, 0), new Vector3(0.14f, 0.08f, 0.14f), matMetalStainless, false);

        // Chairs
        int chairPairs = isSixSeater ? 3 : 2;
        float spacing = isSixSeater ? 0.6f : 0.8f;
        float startX = -(chairPairs - 1) * 0.5f * spacing;

        for (int p = 0; p < chairPairs; p++)
        {
            float cx = startX + p * spacing;
            seatTableTarget = center;
            BuildCafeteriaChair(tRoot, center + new Vector3(cx, 0, 0.65f), 0f, chairMat, "N" + p);
            seatTableTarget = center;
            BuildCafeteriaChair(tRoot, center + new Vector3(cx, 0, -0.65f), 180f, chairMat, "S" + p);
        }
    }

    // One dining chair: bevelled seat and back, four round tube legs, back uprights.
    // The seat carries a collider so the player cannot walk through it.
    static void BuildCafeteriaChair(Transform parent, Vector3 floorPos, float rotY, Material mat, string tag)
    {
        var ch = new GameObject("Chair_" + tag).transform;
        ch.SetParent(parent, false);
        ch.localPosition = floorPos;
        ch.localRotation = Quaternion.Euler(0, rotY, 0);

        const float seatH = 0.44f, seatW = 0.44f, seatD = 0.42f;

        InteriorGeometry.RoundedBox(ch, "Seat", new Vector3(0, seatH, 0),
            new Vector3(seatW, 0.06f, seatD), 0.024f, mat, true);
        InteriorGeometry.RoundedBox(ch, "Seat_Frame", new Vector3(0, seatH - 0.05f, 0),
            new Vector3(seatW - 0.05f, 0.035f, seatD - 0.05f), 0.012f, matMetalMatteBlack, false);

        // Raked back made of two bevelled panels.
        var backLow = InteriorGeometry.RoundedBox(ch, "Back_Low",
            new Vector3(0, seatH + 0.13f, seatD * 0.40f),
            new Vector3(seatW * 0.98f, 0.26f, 0.055f), 0.018f, mat, false);
        backLow.transform.localRotation = Quaternion.Euler(-2f, 0f, 0f);

        var backHigh = InteriorGeometry.RoundedBox(ch, "Back_High",
            new Vector3(0, seatH + 0.30f, seatD * 0.42f),
            new Vector3(seatW * 0.90f, 0.16f, 0.05f), 0.016f, mat, false);
        backHigh.transform.localRotation = Quaternion.Euler(-2.5f, 0f, 0f);

        foreach (float sx in new[] { -1f, 1f })
        {
            InteriorGeometry.Tube(ch, "Leg_F", new Vector3(sx * seatW * 0.40f, 0.005f, seatD * 0.36f),
                new Vector3(sx * seatW * 0.40f, seatH, seatD * 0.36f), 0.018f, matMetalMatteBlack);
            InteriorGeometry.Tube(ch, "Leg_B", new Vector3(sx * seatW * 0.40f, 0.005f, -seatD * 0.36f),
                new Vector3(sx * seatW * 0.40f, seatH, -seatD * 0.36f), 0.018f, matMetalMatteBlack);
            // Uprights rise from the rear legs and rake slightly back with the panels.
            InteriorGeometry.Tube(ch, "Back_Post", new Vector3(sx * seatW * 0.42f, seatH - 0.02f, seatD * 0.40f),
                new Vector3(sx * seatW * 0.42f, seatH + 0.40f, seatD * 0.44f), 0.019f, matMetalMatteBlack);
        }
        InteriorGeometry.Tube(ch, "Stretcher", new Vector3(-seatW * 0.40f, 0.16f, 0f),
            new Vector3(seatW * 0.40f, 0.16f, 0f), 0.013f, matMetalMatteBlack);

        // Record the seat: the chair's footprint, turned toward the table it serves.
        Vector3 seatPos = new Vector3(floorPos.x, floorPos.y, floorPos.z);
        Quaternion seatRot = ch.rotation;
        if (seatTableTarget.HasValue)
        {
            Vector3 t = seatTableTarget.Value;
            Vector3 dir = new Vector3(t.x - seatPos.x, 0f, t.z - seatPos.z);
            if (dir.sqrMagnitude > 0.0001f)
                seatRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        seatAnchorPos.Add(seatPos);
        seatAnchorRot.Add(seatRot);
    }

    // Set just before a chair is created so its anchor faces the table it belongs to.
    static Vector3? seatTableTarget;

    // H. Modern Greenery Planter Partitions
    static void BuildGreeneryPartitions(Transform parent, float baseY, string prefix)
    {
        var root = new GameObject(prefix + "_Greenery_Partitions").transform;
        root.SetParent(parent, false);

        // Placed along West corridor at X = -13.5m (between cutlery aisle and dining hall)
        BuildSinglePlanter(root, new Vector3(-13.5f, baseY, -1.0f), 3.2f);
        BuildSinglePlanter(root, new Vector3(-13.5f, baseY, 3.5f), 3.2f);
    }

    static void BuildSinglePlanter(Transform parent, Vector3 center, float length)
    {
        var pRoot = new GameObject("Planter_Box").transform;
        pRoot.SetParent(parent, false);

        MakeBox(pRoot, "Trough_Body", center + new Vector3(0, 0.35f, 0), new Vector3(0.5f, 0.7f, length), matPlantPot);
        MakeBox(pRoot, "Trough_Cap", center + new Vector3(0, 0.72f, 0), new Vector3(0.56f, 0.04f, length + 0.06f), matWoodScandiOak, false);

        int segments = Mathf.Max(3, Mathf.RoundToInt(length / 0.7f));
        float startZ = -length * 0.5f + 0.35f;
        float stepZ = (length - 0.7f) / Mathf.Max(1, segments - 1);

        for (int i = 0; i < segments; i++)
        {
            Vector3 leafPos = center + new Vector3(0, 0.95f, startZ + i * stepZ);
            MakeBox(pRoot, "Foliage_" + i, leafPos, new Vector3(0.42f, 0.45f, 0.52f), matPlantFoliage, false);
            MakeBox(pRoot, "Foliage_Top_" + i, leafPos + new Vector3(0, 0.25f, 0), new Vector3(0.28f, 0.25f, 0.35f), matPlantFoliage, false);
        }
    }

    // --- 4b. Floor 3F Interior Layout ---
    static void BuildFloorInterior3F(Transform floorRoot, float baseY)
    {
        string prefix = "3F";

        // A. Fruit Market (North Zone: 买水果的区域)
        BuildFruitMarket3F(floorRoot, baseY, prefix);

        // B. Straight Meal Serving Line (East Zone: 一条买饭的区域)
        BuildMealServingLine3F(floorRoot, baseY, prefix);

        // C. Faculty Dining Room Facade ONLY (Southeast Zone: 教师餐厅没有实体模型，不做交互制作贴图)
        BuildFacultyDiningFacade3F(floorRoot, baseY, prefix);

        // D. Restroom Flush Alcove (East Wall: 与 1/2 层同位置)
        BuildRestroomFlushAlcove(floorRoot, baseY, prefix);

        // E. Cutlery Station (West landing arrival area)
        BuildCutleryStation3F(floorRoot, baseY, prefix);

        // F. Tray Return Area (East side adjacent to meal line)
        BuildTrayReturn3F(floorRoot, baseY, prefix);

        // G. Central & South Dining Hall Seating (中间大部分都是桌子)
        BuildDiningArea3F(floorRoot, baseY, prefix);

        // H. Modern Greenery Planter Partitions (绿植隔离带)
        BuildGreeneryPartitions3F(floorRoot, baseY, prefix);
    }

    static void BuildFruitMarket3F(Transform parent, float baseY, string prefix)
    {
        var root = new GameObject(prefix + "_Fruit_Supermarket").transform;
        root.SetParent(parent, false);

        // 1. Large illuminated hanging/wall header sign facing South
        var sign = MakeBox(root, "Sign_FruitMarket_Header", new Vector3(-2.0f, baseY + 3.2f, 15.65f), new Vector3(8.5f, 0.55f, 0.08f), matSignBlackBox, false);
        MakeBox(sign.transform, "Sign_FruitGlow", new Vector3(0, 0, -0.01f), new Vector3(8.3f, 0.45f, 0.03f), matLightWarmLED, false);
        MakeText(sign.transform, "鲜果超市 · 精品鲜切水果 / 现榨果汁 [Fruit Market]", new Vector3(0, 0, -0.06f), Quaternion.Euler(0, 0, 0), 26, 0.036f, Color.white);

        // 2. North Wall Tiered Produce Display Racks (4 distinct multi-shelf racks)
        // From X = -11.0m to X = 3.5m at Z = 14.8m
        for (int k = 0; k < 4; k++)
        {
            float rx = -9.5f + k * 3.8f;
            Vector3 rackPos = new Vector3(rx, baseY, 14.8f);

            // Rack structure: Scandi oak frame with titanium trim
            MakeBox(root, $"ProduceRack_{k}_Base", rackPos + new Vector3(0, 0.15f, 0), new Vector3(3.2f, 0.3f, 1.2f), matFloorDarkBasalt);
            MakeBox(root, $"ProduceRack_{k}_Body", rackPos + new Vector3(0, 0.75f, 0), new Vector3(3.2f, 0.9f, 1.1f), matWoodScandiOak);
            MakeBox(root, $"ProduceRack_{k}_Top", rackPos + new Vector3(0, 1.25f, 0), new Vector3(3.25f, 0.05f, 1.15f), matWoodDarkWalnut);

            // Tiered fruit crates and colourful fruit displays
            Material fruitA = (k == 0) ? matFruitApple : (k == 1) ? matFruitOrange : (k == 2) ? matFruitWatermelon : matFruitMelon;
            Material fruitB = (k == 0) ? matFruitBanana : (k == 1) ? matFruitGrape : (k == 2) ? matFruitApple : matFruitOrange;

            // Upper tier boxes
            MakeBox(root, $"FruitCrate_Top_{k}A", rackPos + new Vector3(-0.75f, 1.35f, 0.15f), new Vector3(1.3f, 0.16f, 0.65f), matWoodScandiOak, false);
            MakeBox(root, $"FruitPile_Top_{k}A", rackPos + new Vector3(-0.75f, 1.48f, 0.15f), new Vector3(1.2f, 0.12f, 0.55f), fruitA, false);

            MakeBox(root, $"FruitCrate_Top_{k}B", rackPos + new Vector3(0.75f, 1.35f, 0.15f), new Vector3(1.3f, 0.16f, 0.65f), matWoodScandiOak, false);
            MakeBox(root, $"FruitPile_Top_{k}B", rackPos + new Vector3(0.75f, 1.48f, 0.15f), new Vector3(1.2f, 0.12f, 0.55f), fruitB, false);

            // Mid tier angled display
            MakeBox(root, $"FruitPile_Mid_{k}", rackPos + new Vector3(0, 0.85f, -0.2f), new Vector3(2.8f, 0.18f, 0.65f), (k % 2 == 0) ? matFruitOrange : matFruitApple, false);

            // Price tag bar
            var priceBar = MakeBox(root, $"PriceBar_{k}", rackPos + new Vector3(0, 1.55f, -0.45f), new Vector3(2.8f, 0.15f, 0.04f), matSignBlackBox, false);
            string[] fruitNames = { "红富士苹果 ¥6.8/斤 · 进口香蕉 ¥3.9/斤", "砂糖橘 ¥5.5/斤 · 阳光玫瑰葡萄 ¥12.8/斤", "麒麟西瓜 ¥3.5/斤 · 蜜瓜 ¥5.2/斤", "鲜榨橙汁 ¥9/杯 · 鲜切果盘 ¥12/盒" };
            MakeText(priceBar.transform, fruitNames[k], new Vector3(0, 0, -0.03f), Quaternion.Euler(0, 0, 0), 20, 0.026f, Color.white);
        }

        // 3. Central Fruit Display Islands (2 stepped islands in the aisle)
        Vector3[] islandCenters = { new Vector3(-7.5f, baseY, 11.5f), new Vector3(-1.5f, baseY, 11.5f) };
        for (int m = 0; m < islandCenters.Length; m++)
        {
            Vector3 ic = islandCenters[m];
            MakeBox(root, $"FruitIsland_{m}_Kick", ic + new Vector3(0, 0.06f, 0), new Vector3(3.6f, 0.12f, 2.0f), matMetalTitanium);
            MakeBox(root, $"FruitIsland_{m}_Body", ic + new Vector3(0, 0.48f, 0), new Vector3(3.8f, 0.72f, 2.2f), matWoodScandiOak);
            MakeBox(root, $"FruitIsland_{m}_Rim", ic + new Vector3(0, 0.86f, 0), new Vector3(3.85f, 0.06f, 2.25f), matWoodDarkWalnut);

            // Upper stepped tier
            MakeBox(root, $"FruitIsland_{m}_Tier2", ic + new Vector3(0, 1.05f, 0), new Vector3(2.6f, 0.35f, 1.2f), matWoodScandiOak);

            // Fruit mounds on island
            Material matM1 = (m == 0) ? matFruitWatermelon : matFruitMelon;
            Material matM2 = (m == 0) ? matFruitGrape : matFruitBanana;
            MakeBox(root, $"FruitIsland_{m}_Mound1", ic + new Vector3(-1.1f, 0.96f, 0), new Vector3(1.1f, 0.15f, 1.7f), matFruitApple, false);
            MakeBox(root, $"FruitIsland_{m}_Mound2", ic + new Vector3(1.1f, 0.96f, 0), new Vector3(1.1f, 0.15f, 1.7f), matFruitOrange, false);
            MakeBox(root, $"FruitIsland_{m}_Mound3", ic + new Vector3(0, 1.28f, 0), new Vector3(2.2f, 0.14f, 0.9f), matM1, false);

            var isSign = MakeBox(root, $"Sign_Island_{m}", ic + new Vector3(0, 1.75f, 0), new Vector3(1.8f, 0.28f, 0.06f), matSignBlackBox, false);
            MakeText(isSign.transform, (m == 0) ? "时令鲜果 · 自选特惠" : "精品切果 · 原果现切", new Vector3(0, 0, -0.04f), Quaternion.Euler(0, 0, 0), 20, 0.028f, Color.white);
        }

        // 4. Refrigerated Open Chiller Showcase (鲜切果盒冷风幕柜)
        // Along West wall of fruit zone: X = -14.0m, Z = 12.5m
        Vector3 chillerPos = new Vector3(-14.0f, baseY, 12.5f);
        MakeBox(root, "Chiller_Body", chillerPos + new Vector3(0, 1.0f, 0), new Vector3(1.2f, 2.0f, 4.2f), matMetalTitanium);
        MakeBox(root, "Chiller_Back", chillerPos + new Vector3(-0.45f, 1.0f, 0), new Vector3(0.2f, 2.0f, 4.2f), matMetalStainless);
        MakeBox(root, "Chiller_LED_Strip", chillerPos + new Vector3(0.45f, 1.95f, 0), new Vector3(0.08f, 0.06f, 4.0f), matLightWarmLED, false);
        MakeBox(root, "Chiller_Glass_South", chillerPos + new Vector3(0, 1.1f, -2.1f), new Vector3(1.1f, 1.6f, 0.04f), matGlassUltra, false);
        MakeBox(root, "Chiller_Glass_North", chillerPos + new Vector3(0, 1.1f, 2.1f), new Vector3(1.1f, 1.6f, 0.04f), matGlassUltra, false);

        // Chiller interior tiers of packaged fresh cut fruits
        for (int c = 0; c < 3; c++)
        {
            float cy = 0.55f + c * 0.45f;
            MakeBox(root, $"Chiller_Shelf_{c}", chillerPos + new Vector3(0.05f, cy, 0), new Vector3(0.9f, 0.04f, 4.0f), matMetalStainless, false);
            MakeBox(root, $"FruitBowls_{c}_A", chillerPos + new Vector3(0.05f, cy + 0.08f, -1.2f), new Vector3(0.6f, 0.12f, 1.2f), matFruitWatermelon, false);
            MakeBox(root, $"FruitBowls_{c}_B", chillerPos + new Vector3(0.05f, cy + 0.08f, 0), new Vector3(0.6f, 0.12f, 1.0f), matFruitMelon, false);
            MakeBox(root, $"FruitBowls_{c}_C", chillerPos + new Vector3(0.05f, cy + 0.08f, 1.2f), new Vector3(0.6f, 0.12f, 1.2f), matFruitOrange, false);
        }
        var chillerSign = MakeBox(root, "Sign_Chiller", chillerPos + new Vector3(0.62f, 1.8f, 0), new Vector3(0.04f, 0.35f, 3.2f), matSignBlackBox, false);
        MakeText(chillerSign.transform, "冰镇鲜切果盒专柜 · 每日现切保鲜", new Vector3(0.03f, 0, 0), Quaternion.Euler(0, -90, 0), 22, 0.032f, Color.white);

        // 5. Fruit Weighing & Checkout Counter (称重收银台)
        Vector3 weighPos = new Vector3(5.0f, baseY, 11.5f);
        MakeBox(root, "Weigh_Counter_Body", weighPos + new Vector3(0, 0.45f, 0), new Vector3(2.2f, 0.9f, 1.4f), matWoodScandiOak);
        MakeBox(root, "Weigh_Counter_Top", weighPos + new Vector3(0, 0.92f, 0), new Vector3(2.25f, 0.05f, 1.45f), matMarbleWhite);
        MakeBox(root, "Digital_Scale", weighPos + new Vector3(-0.5f, 1.06f, 0), new Vector3(0.55f, 0.22f, 0.55f), matMetalStainless, false);
        MakeBox(root, "POS_Touch_Reg", weighPos + new Vector3(0.5f, 1.15f, 0), new Vector3(0.42f, 0.40f, 0.35f), matMetalMatteBlack, false);
        MakeBox(root, "Bag_Dispenser", weighPos + new Vector3(-0.95f, 1.25f, 0.4f), new Vector3(0.18f, 0.55f, 0.18f), matMetalStainless, false);

        var weighSign = MakeBox(root, "Sign_Weigh", weighPos + new Vector3(0, 2.3f, 0), new Vector3(2.2f, 0.40f, 0.06f), matSignBlackBox, false);
        MakeText(weighSign.transform, "水果称重处 · 请先称重贴码", new Vector3(0, 0, -0.04f), Quaternion.Euler(0, 0, 0), 22, 0.032f, Color.white);
    }

    static void BuildMealServingLine3F(Transform parent, float baseY, string prefix)
    {
        var root = new GameObject(prefix + "_Straight_Meal_Serving_Line").transform;
        root.SetParent(parent, false);

        // Straight continuous meal serving line along East side: X = 11.5m, Z from 2.5m to 14.5m (Length = 12.0m)
        Vector3 center = new Vector3(11.5f, baseY + 0.45f, 8.5f);

        // Main Counter Body
        MakeBox(root, "Counter_Straight_Body", center, new Vector3(1.1f, 0.9f, 12.0f), matMetalTitanium);
        MakeBox(root, "Counter_Straight_Top", center + new Vector3(0, 0.47f, 0), new Vector3(1.16f, 0.04f, 12.0f), matMetalStainless);
        // Sneeze Guard Glass on customer side (West side)
        MakeBox(root, "Glass_SneezeGuard", center + new Vector3(-0.52f, 0.72f, 0), new Vector3(0.04f, 0.50f, 12.0f), matGlassUltra, false);
        // Stainless Tray Slide Rail on customer side
        MakeBox(root, "Tray_Slide_Rail", center + new Vector3(-0.80f, 0.40f, 0), new Vector3(0.35f, 0.06f, 12.0f), matMetalStainless, false);

        // 8 Food Warming Steam Pans with 3D Dishes
        Material[] foodPalette = { matFoodBeef, matFoodGolden, matFoodGreen, matFoodRed, matFoodEgg, matFoodBeef, matFoodRice, matFoodBamboo };
        for (int i = 0; i < 8; i++)
        {
            float pz = 3.2f + i * 1.5f;
            Vector3 panPos = new Vector3(11.5f, baseY + 0.94f, pz);
            MakeBox(root, $"SteamPan_{i}", panPos, new Vector3(0.75f, 0.02f, 1.15f), matMetalStainless, false);
            MakeBox(root, $"FoodItem_{i}", panPos + new Vector3(0, 0.02f, 0), new Vector3(0.65f, 0.025f, 1.0f), foodPalette[i % foodPalette.Length], false);
        }

        // Overhead Stall Signboards (3 illuminated signs along the straight line, facing West)
        string[] stallTitles = {
            "3号档口 · 广式烧味与铁板",
            "2号档口 · 精品热炒与主食",
            "1号档口 · 砂锅原汤与蒸点"
        };
        float[] stallZ = { 12.5f, 8.5f, 4.5f };

        for (int s = 0; s < 3; s++)
        {
            Vector3 sPos = new Vector3(11.5f, baseY + 3.2f, stallZ[s]);
            var sSign = MakeBox(root, $"Stall_Sign_{s}", sPos, new Vector3(0.10f, 0.45f, 3.2f), matSignBlackBox, false);
            MakeBox(sSign.transform, "Glow", new Vector3(0, 0, 0), new Vector3(0.11f, 0.35f, 3.0f), matLightWarmLED, false);
            MakeBox(sSign.transform, "Hanger_1", new Vector3(0, 0.5f, -1.0f), new Vector3(0.03f, 1.0f, 0.03f), matMetalMatteBlack, false);
            MakeBox(sSign.transform, "Hanger_2", new Vector3(0, 0.5f, 1.0f), new Vector3(0.03f, 1.0f, 0.03f), matMetalMatteBlack, false);
            MakeText(sSign.transform, stallTitles[s], new Vector3(-0.07f, 0, 0), Quaternion.Euler(0, 90, 0), 26, 0.036f, Color.white);
        }

        // Queue Guidance Stanchions along the straight line (forming a clean 1.6m customer corridor at X = 9.8m)
        for (int q = 0; q < 6; q++)
        {
            float qz = 2.5f + q * 2.4f;
            MakeBox(root, $"Stanchion_{q}", new Vector3(9.8f, baseY + 0.45f, qz), new Vector3(0.07f, 0.9f, 0.07f), matMetalMatteBlack, false);
            MakeBox(root, $"Base_{q}", new Vector3(9.8f, baseY + 0.02f, qz), new Vector3(0.26f, 0.04f, 0.26f), matMetalMatteBlack, false);
        }
        MakeBox(root, "Queue_Rope", new Vector3(9.8f, baseY + 0.85f, 8.5f), new Vector3(0.04f, 0.04f, 12.0f), matChairSlateBlue, false);

        // Back kitchen prep table behind the serving counter (East side: X = 15.5m, Z from 4.0 to 14.0)
        MakeBox(root, "Kitchen_Prep_Back", new Vector3(15.5f, baseY + 0.45f, 8.5f), new Vector3(1.2f, 0.9f, 9.0f), matMetalStainless);
        MakeBox(root, "Kitchen_Storage_Upper", new Vector3(15.5f, baseY + 2.4f, 8.5f), new Vector3(0.8f, 0.6f, 8.0f), matMetalTitanium, false);
    }

    static void BuildFacultyDiningFacade3F(Transform parent, float baseY, string prefix)
    {
        var root = new GameObject(prefix + "_Faculty_Dining_Facade_Only").transform;
        root.SetParent(parent, false);

        // Southeast Zone: X in [7.5, 17.8], Z in [-15.5, -7.5]
        // IMPORTANT: 教师餐厅没有实体模型，不做交互制作贴图！
        // Strictly ONLY the architectural boundary facade facing the dining hall!

        // 1. North Boundary Facade Wall: at Z = -7.5m, X in [7.5, 17.8] (Length 10.3m, center X = 12.65m, height 4.0m)
        MakeBox(root, "Faculty_North_Wall", new Vector3(12.65f, baseY + 2.0f, -7.5f), new Vector3(10.3f, 4.0f, 0.15f), matWallDarkCharcoal);

        // High-resolution artistic texture panel mounted on North face (+Z, facing the main dining hall)
        var texturePanel = MakeBox(root, "Faculty_Facade_Texture_Quad", new Vector3(12.65f, baseY + 2.0f, -7.41f), new Vector3(10.2f, 3.8f, 0.02f), matFacultyPic, false);
        texturePanel.transform.localRotation = Quaternion.Euler(0, 0, 0);

        // Architectural Dark Walnut & Titanium Top Trim & Baseboard
        MakeBox(root, "Faculty_Top_Trim", new Vector3(12.65f, baseY + 3.98f, -7.38f), new Vector3(10.3f, 0.08f, 0.18f), matWoodDarkWalnut, false);
        MakeBox(root, "Faculty_Kickplate", new Vector3(12.65f, baseY + 0.06f, -7.38f), new Vector3(10.3f, 0.12f, 0.18f), matMetalTitanium, false);

        // Elegant Illuminated 3D Header Sign
        var signFaculty = MakeBox(root, "Sign_Faculty_North", new Vector3(12.65f, baseY + 3.4f, -7.36f), new Vector3(5.2f, 0.45f, 0.06f), matSignBlackBox, false);
        MakeBox(signFaculty.transform, "Glow", new Vector3(0, 0, 0.01f), new Vector3(5.0f, 0.35f, 0.03f), matLightWarmLED, false);
        MakeText(signFaculty.transform, "教工餐厅 · FACULTY DINING ROOM (教工专属)", new Vector3(0, 0, 0.04f), Quaternion.Euler(0, 180, 0), 24, 0.034f, Color.white);

        // 2. West Boundary Partition Wall: at X = 7.5m, Z in [-15.5, -7.5] (Length 8.0m, center Z = -11.5m, height 4.0m)
        MakeBox(root, "Faculty_West_Wall", new Vector3(7.5f, baseY + 2.0f, -11.5f), new Vector3(0.2f, 4.0f, 8.0f), matWallMicrocement);
        MakeBox(root, "Faculty_West_Top_Trim", new Vector3(7.5f, baseY + 3.98f, -11.5f), new Vector3(0.25f, 0.08f, 8.0f), matWoodDarkWalnut, false);
        MakeBox(root, "Faculty_West_Kickplate", new Vector3(7.5f, baseY + 0.06f, -11.5f), new Vector3(0.25f, 0.12f, 8.0f), matMetalTitanium, false);

        // Directional Nameplate on West Wall facing West
        var signWest = MakeBox(root, "Sign_Faculty_West", new Vector3(7.38f, baseY + 2.5f, -11.5f), new Vector3(0.04f, 0.40f, 2.6f), matSignBlackBox, false);
        MakeText(signWest.transform, "教工餐厅 · 贵宾专属通道", new Vector3(-0.03f, 0, 0), Quaternion.Euler(0, 90, 0), 22, 0.032f, Color.white);

        // INTERIOR CONSTRAINTS:
        // No tables, no chairs, no colliders, no interactive spot inside X > 7.5 and Z < -7.5!
    }

    static void BuildCutleryStation3F(Transform parent, float baseY, string prefix)
    {
        var root = new GameObject(prefix + "_Cutlery_Station").transform;
        root.SetParent(parent, false);

        Vector3 pos = new Vector3(-15.5f, baseY, -8.0f);

        MakeBox(root, "Cabinet_Body", pos + new Vector3(0, 0.45f, 0), new Vector3(2.4f, 0.9f, 0.9f), matMetalTitanium);
        MakeBox(root, "Cabinet_Top", pos + new Vector3(0, 0.92f, 0), new Vector3(2.45f, 0.04f, 0.95f), matMetalStainless);

        MakeBox(root, "Trays_Sage", pos + new Vector3(-0.7f, 1.15f, 0), new Vector3(0.45f, 0.25f, 0.35f), matChairSage, false);
        MakeBox(root, "Trays_Cream", pos + new Vector3(0f, 1.15f, 0), new Vector3(0.45f, 0.25f, 0.35f), matChairCream, false);

        MakeBox(root, "Chopstick_Box", pos + new Vector3(0.7f, 1.12f, 0.15f), new Vector3(0.35f, 0.2f, 0.25f), matMetalStainless, false);
        MakeBox(root, "Spoon_Box", pos + new Vector3(0.7f, 1.12f, -0.15f), new Vector3(0.35f, 0.2f, 0.25f), matMetalStainless, false);

        var sign = MakeBox(root, "Sign_Cutlery", pos + new Vector3(0, 2.2f, 0), new Vector3(2.4f, 0.38f, 0.06f), matSignBlackBox, false);
        MakeText(sign.transform, prefix + " 餐具取放处 · 请有序取餐", new Vector3(0, 0, -0.04f), Quaternion.Euler(0, 0, 0), 22, 0.034f, Color.white);
    }

    static void BuildTrayReturn3F(Transform parent, float baseY, string prefix)
    {
        var root = new GameObject(prefix + "_Tray_Return_Station").transform;
        root.SetParent(parent, false);

        Vector3 pos = new Vector3(12.5f, baseY, 1.2f);

        // Stainless collection racks
        MakeBox(root, "Rack_Counter", pos + new Vector3(0, 0.5f, 0), new Vector3(4.5f, 1.0f, 1.4f), matMetalTitanium);
        MakeBox(root, "Rack_Shelves", pos + new Vector3(0, 1.4f, 0), new Vector3(4.3f, 0.8f, 1.3f), matMetalStainless, false);

        // 3 Waste sorting bins
        string[] binNames = { "餐厨垃圾", "可回收物", "其他垃圾" };
        Material[] binAccents = { matChairSage, matChairSlateBlue, matWallDarkCharcoal };
        for (int i = 0; i < 3; i++)
        {
            Vector3 bPos = pos + new Vector3(-1.4f + i * 1.4f, 0.45f, -1.2f);
            var bin = MakeBox(root, $"Waste_Bin_{i}", bPos, new Vector3(0.65f, 0.9f, 0.65f), matMetalTitanium);
            MakeBox(bin.transform, "Bin_Lid", new Vector3(0, 0.46f, 0), new Vector3(0.67f, 0.04f, 0.67f), binAccents[i], false);
            MakeText(bin.transform, binNames[i], new Vector3(0, 0.12f, -0.34f), Quaternion.Euler(0, 0, 0), 20, 0.03f, Color.white);
        }

        var sign = MakeBox(root, "Sign_TrayReturn", pos + new Vector3(0, 2.5f, -0.9f), new Vector3(3.6f, 0.45f, 0.06f), matSignBlackBox, false);
        MakeText(sign.transform, prefix + " 残餐回收区 (餐盘归还 / 分类垃圾桶)", new Vector3(0, 0, -0.04f), Quaternion.Euler(0, 0, 0), 22, 0.034f, Color.white);
    }

    static void BuildDiningArea3F(Transform parent, float baseY, string prefix)
    {
        var root = new GameObject(prefix + "_Dining_Area").transform;
        root.SetParent(parent, false);

        int tableIdx = 0;

        // Zone 1: North Dining Hall (below the fruit supermarket: X in [-10.5, 6.0], Z in [4.5, 7.5])
        for (float x = -10.5f; x <= 5.5f; x += 3.2f)
        {
            for (float z = 5.0f; z <= 7.5f; z += 2.8f)
            {
                BuildTableSet(root, new Vector3(x, baseY, z), tableIdx++, prefix);
            }
        }

        // Zone 2: Central Dining Hall (X in [-11.5, 5.0], Z in [-6.5, 2.5])
        for (float x = -11.5f; x <= 4.5f; x += 3.2f)
        {
            for (float z = -6.0f; z <= 2.0f; z += 2.8f)
            {
                BuildTableSet(root, new Vector3(x, baseY, z), tableIdx++, prefix);
            }
        }

        // Zone 3: South Hall between Stair 1 and Faculty Dining West Wall (X in [-7.5, 4.5], Z in [-14.0, -9.0])
        for (float x = -7.5f; x <= 4.5f; x += 3.2f)
        {
            for (float z = -14.0f; z <= -9.0f; z += 2.8f)
            {
                BuildTableSet(root, new Vector3(x, baseY, z), tableIdx++, prefix);
            }
        }
    }

    static void BuildGreeneryPartitions3F(Transform parent, float baseY, string prefix)
    {
        var root = new GameObject(prefix + "_Greenery_Partitions").transform;
        root.SetParent(parent, false);

        // Planters along aisle boundaries
        BuildSinglePlanter(root, new Vector3(-13.5f, baseY, -1.0f), 3.2f);
        BuildSinglePlanter(root, new Vector3(-13.5f, baseY, 3.5f), 3.2f);
        BuildSinglePlanter(root, new Vector3(6.5f, baseY, 0.5f), 2.8f);
    }

    // --- 5. Doors, Exits & Spawn Points ---
    static void BuildDoorsAndExits(Transform sceneRootTransform, Transform interiorTransform, InteriorSceneRoot sceneRoot)
    {
        var root = new GameObject("00_Doors_And_Portals").transform;
        root.SetParent(interiorTransform, false);

        // 1. West Door (西门 [E]: X = -18.0m, Z = -2.0m)
        var wFrame = new GameObject("Door_West_Frame").transform;
        wFrame.SetParent(root, false);
        wFrame.localPosition = new Vector3(-17.9f, 0, -2.0f);
        MakeBox(wFrame, "Post_S", new Vector3(0, 1.5f, -2.0f), new Vector3(0.2f, 3.0f, 0.2f), matMetalTitanium);
        MakeBox(wFrame, "Post_N", new Vector3(0, 1.5f, 2.0f), new Vector3(0.2f, 3.0f, 0.2f), matMetalTitanium);
        MakeBox(wFrame, "Top_Header", new Vector3(0, 3.0f, 0), new Vector3(0.2f, 0.2f, 4.2f), matMetalTitanium);
        MakeBox(wFrame, "Glass_S", new Vector3(0, 1.4f, -1.0f), new Vector3(0.06f, 2.7f, 1.8f), matGlassUltra, false);
        MakeBox(wFrame, "Glass_N", new Vector3(0, 1.4f, 1.0f), new Vector3(0.06f, 2.7f, 1.8f), matGlassUltra, false);

        var signW = MakeBox(wFrame, "Sign_WestDoor", new Vector3(0.06f, 2.7f, 0), new Vector3(0.05f, 0.35f, 1.8f), matSignBlackBox, false);
        MakeText(signW.transform, "西门 · 出入口 [E]", new Vector3(0.04f, 0, 0), Quaternion.Euler(0, -90, 0), 24, 0.035f, Color.white);

        // 2. Southeast Door (东南门 [E]: Z = -16.0m, X = 12.0m)
        var seFrame = new GameObject("Door_Southeast_Frame").transform;
        seFrame.SetParent(root, false);
        seFrame.localPosition = new Vector3(12.0f, 0, -15.9f);
        MakeBox(seFrame, "Post_W", new Vector3(-2.0f, 1.5f, 0), new Vector3(0.2f, 3.0f, 0.2f), matMetalTitanium);
        MakeBox(seFrame, "Post_E", new Vector3(2.0f, 1.5f, 0), new Vector3(0.2f, 3.0f, 0.2f), matMetalTitanium);
        MakeBox(seFrame, "Top_Header", new Vector3(0, 3.0f, 0), new Vector3(4.2f, 0.2f, 0.2f), matMetalTitanium);
        MakeBox(seFrame, "Glass_W", new Vector3(-1.0f, 1.4f, 0), new Vector3(1.8f, 2.7f, 0.06f), matGlassUltra, false);
        MakeBox(seFrame, "Glass_E", new Vector3(1.0f, 1.4f, 0), new Vector3(1.8f, 2.7f, 0.06f), matGlassUltra, false);

        var signSE = MakeBox(seFrame, "Sign_SEDoor", new Vector3(0, 2.7f, 0.06f), new Vector3(2.2f, 0.35f, 0.05f), matSignBlackBox, false);
        MakeText(signSE.transform, "东南门 · 出入口 [E]", new Vector3(0, 0, 0.04f), Quaternion.Euler(0, 180, 0), 24, 0.035f, Color.white);

        // Exits for InteriorSceneRoot
        var exitWest = sceneRootTransform.Find("Exit_West");
        if (!exitWest)
        {
            var go = new GameObject("Exit_West");
            go.transform.SetParent(sceneRootTransform, false);
            exitWest = go.transform;
        }
        exitWest.localPosition = new Vector3(-17.5f, 0f, -2.0f);

        var exitSE = sceneRootTransform.Find("Exit_Southeast");
        if (!exitSE)
        {
            var go = new GameObject("Exit_Southeast");
            go.transform.SetParent(sceneRootTransform, false);
            exitSE = go.transform;
        }
        exitSE.localPosition = new Vector3(12.0f, 0f, -15.5f);

        sceneRoot.exits = new Transform[] { exitSE, exitWest };

        // Spawn Points
        var spawnSE = sceneRootTransform.Find("Spawn_main");
        if (!spawnSE)
        {
            var go = new GameObject("Spawn_main");
            go.transform.SetParent(sceneRootTransform, false);
            spawnSE = go.transform;
        }
        spawnSE.localPosition = new Vector3(12.0f, 0.1f, -14.0f);
        spawnSE.localRotation = Quaternion.Euler(0, 0, 0);

        var spawnW = sceneRootTransform.Find("Spawn_West");
        if (!spawnW)
        {
            var go = new GameObject("Spawn_West");
            go.transform.SetParent(sceneRootTransform, false);
            spawnW = go.transform;
        }
        spawnW.localPosition = new Vector3(-16.0f, 0.1f, -2.0f);
        spawnW.localRotation = Quaternion.Euler(0, 90, 0);

        sceneRoot.spawnPoints = new InteriorSpawnPoint[]
        {
            new InteriorSpawnPoint { id = "main", anchor = spawnSE },
            new InteriorSpawnPoint { id = "west", anchor = spawnW }
        };
    }

    // --- 6. Lighting for both 1F and 2F ---
    static void BuildLighting(Transform parent)
    {
        // Remove anything left over from the grey-box pass before re-lighting, otherwise
        // the old flat directional light keeps washing the room out. The placeholder was
        // parented straight to the scene root, so sweep upward from the interior root too.
        Transform top = parent;
        while (top.parent != null) top = top.parent;
        foreach (var l in top.GetComponentsInChildren<Light>(true))
            if (l.name.StartsWith("Placeholder")) UnityEngine.Object.DestroyImmediate(l.gameObject);

        var root = new GameObject("00_Interior_Lighting").transform;
        root.SetParent(parent, false);

        var sunObj = new GameObject("Main_Interior_Sun");
        sunObj.transform.SetParent(root, false);
        var sun = sunObj.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1.0f, 0.965f, 0.915f);
        sun.intensity = 0.62f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.7f;
        sun.shadowBias = 0.03f;
        sun.shadowNormalBias = 0.32f;
        sunObj.transform.rotation = Quaternion.Euler(50f, -33f, 0f);
        RenderSettings.sun = sun;

        // Directional ambient with real vertical separation: bright ceiling bounce,
        // mid walls, dark floor. Flat ambient at 0.78 was what made everything look
        // like a white box.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.66f, 0.68f, 0.69f);
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.46f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.24f, 0.24f, 0.24f);
        RenderSettings.fog = false;

        // The storey slabs use the granite floor material on both faces, so from below
        // they read as a black ceiling. Cap the underside of each slab with a pale
        // ceiling finish so the rooms are not roofed in dark stone.
        var ceilRoot = new GameObject("00_Ceiling_Finish").transform;
        ceilRoot.SetParent(parent, false);
        for (int f = 0; f < 3; f++)
        {
            float slabBottom = (f + 1) * 4.30f - 0.15f;   // underside of the slab above
            MakeBox(ceilRoot, "Ceiling_" + (f + 1) + "F",
                new Vector3(0, slabBottom - 0.02f, 3.6f),
                new Vector3(36f, 0.03f, 24.8f), matCeilingWhite, false);
            MakeBox(ceilRoot, "Ceiling_" + (f + 1) + "F_S",
                new Vector3(-9f, slabBottom - 0.02f, -12.4f),
                new Vector3(22f, 0.03f, 7.2f), matCeilingWhite, false);
        }

        string[] floorName = { "1F", "2F", "3F" };
        float[] floorY = { 4.18f, 8.52f, 12.82f };
        float[] baseY = { 0f, 4.30f, 8.60f };

        for (int f = 0; f < 3; f++)
        {
            int n = 0;
            for (float x = -13.5f; x <= 14.5f; x += 5.5f)
            {
                for (float z = -12f; z <= 12f; z += 5.2f)
                {
                    // The emissive strip is only the fitting; the point light below it is
                    // what actually lights the room.
                    var lamp = MakeBox(root, "Light_" + floorName[f] + "_" + Mathf.RoundToInt(x) + "_" + Mathf.RoundToInt(z),
                        new Vector3(x, floorY[f], z), new Vector3(1.8f, 0.04f, 0.35f), matLightWarmLED, false);
                    var mr = lamp.GetComponent<MeshRenderer>();
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;

                    var lg = new GameObject("Light_" + floorName[f] + "_Point_" + n);
                    lg.transform.SetParent(root, false);
                    lg.transform.localPosition = new Vector3(x, floorY[f] - 0.42f, z);
                    var pl = lg.AddComponent<Light>();
                    pl.type = LightType.Point;
                    pl.range = 9.5f;
                    pl.intensity = 3.1f;
                    pl.color = new Color(1f, 0.955f, 0.895f);
                    pl.shadows = LightShadows.None;     // SSAO + the sun carry the shading
                    n++;
                }
            }
        }
    }
}

// Backwards-compatibility alias for legacy calls
public static class Cafeteria1FBuilder
{
    public static void Build() => CafeteriaInteriorBuilder.Build();
}

