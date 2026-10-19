using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

// Builds the three-floor library INTERIOR (图书馆室内) in Assets/Interiors/Scenes/library.unity.
// This is the companion to LibraryBuilder.cs, which builds the campus-scene EXTERIOR shell.
//
// Layout is 40m wide (X) x 32m deep (Z), three storeys of 4.3m each (total 12.9m):
//
//   1F  South main entrance, vending machines either side, central campus sand-table
//       model, north-east restroom, south-east stair + lift core, north rear door,
//       reserved west corridor towards the information building (not connected yet),
//       and the history gallery east of the lift (textured panels only, no solid exhibits).
//   2F  Circulation desk, catalogue terminals, Chinese collection, reading tables,
//       periodicals, electronic reading room, study carrels, lounge.
//   3F  Compact stacks, group study rooms, rare-book room, reading tables, carrels.
//
// The scene keeps exactly one InteriorSceneRoot; no player, camera or AudioListener is added.
public static class LibraryInteriorBuilder
{
    const string ScenePath = "Assets/Interiors/Scenes/library.unity";
    const string MatDir = "Assets/Interiors/Materials/LibraryModern";
    const string TexDir = "Assets/Interiors/Textures";

    // Geometry constants
    const float HalfW = 20f;     // X = +/-20 -> 40m wide
    const float HalfD = 16f;     // Z = +/-16 -> 32m deep
    const float FloorH = 4.3f;   // storey height
    const int Storeys = 4;       // 1F entrance lobby + 2F/3F/4F round reading halls
    const float TotalH = FloorH * Storeys;   // 17.2
    const float SlabT = 0.15f;

    // Walking surfaces and structural slab centres
    const float Y1 = 0f, Y2 = 4.3f, Y3 = 8.6f, Y4 = 12.9f;
    const float SlabY2 = 4.225f, SlabY3 = 8.525f, SlabY4 = 12.825f;

    // Ground level of storey f (0-based): 0 -> Y1, 1 -> Y2, ...
    static string AreaName(int floor, string area)
    {
        if (area == "A") return "圆厅自习区";
        if (area == "B") return "阅览区";
        return "自习桌区";
    }

    static float FloorY(int f) => f == 0 ? Y1 : f == 1 ? Y2 : f == 2 ? Y3 : Y4;

    // Chinese name of a 1-based storey number, for signage.
    static string FloorName(int floor) =>
        floor == 1 ? "一层" : floor == 2 ? "二层" : floor == 3 ? "三层" : "四层";

    // ---- Material palette ----
    static Material matFloorTerrazzo, matFloorDarkBasalt, matFloorCarpet;
    static Material matSofaOrange, matSofaSeatGrey, matStoolRed, matStoolYellow, matShrubLight, matFloorJoint;
    static Material matWallMicrocement, matWallDarkCharcoal, matWallAccentGreen, matCeilingWhite;
    static Material matWoodScandiOak, matWoodDarkWalnut, matWoodBirch;
    static Material matMarbleWhite, matStoneSlate;
    static Material matMetalStainless, matMetalTitanium, matMetalMatteBlack, matMetalBrass;
    static Material matGlassUltra, matGlassFrosted;
    static Material matChairCream, matChairSage, matChairTerracotta, matChairSlateBlue;
    static Material matLightWarmLED, matLightCoolLED, matSignBlackBox;
    static Material matPlantFoliage, matPlantPot;
    static Material matBook1, matBook2, matBook3, matBook4, matBook5, matBook6;
    static Material matSofaFabric, matRug;
    static Material matHistoryPic, matPlanPic, matPosterPic, matCampusPic;

    // ---------------------------------------------------------------- materials

    static Material GetOrCreateMaterial(string name, Color baseColor, float metallic = 0f, float smoothness = 0.2f,
                                        Color? emission = null, bool transparent = false, string texturePath = null,
                                        Vector2? tiling = null)
    {
        var result = GetOrCreateMaterialCore(name, baseColor, metallic, smoothness, emission, transparent);
        if (!string.IsNullOrEmpty(texturePath))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (tex)
            {
                result.SetTexture("_BaseMap", tex);
                var t = tiling ?? Vector2.one;
                result.SetTextureScale("_BaseMap", t);
            }
        }
        return result;
    }

    static Material GetOrCreateMaterialCore(string name, Color baseColor, float metallic, float smoothness,
                                            Color? emission, bool transparent)
    {
        if (!Directory.Exists(MatDir))
        {
            Directory.CreateDirectory(MatDir);
            AssetDatabase.Refresh();
        }

        string path = MatDir + "/" + name + ".mat";
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

    // Loads an optional texture-backed material; returns null when the PNG is absent
    // so the caller can fall back to a flat accent colour.
    static Material GetOrCreateTextureMaterial(string matName, string texName)
    {
        string texPath = TexDir + "/" + texName + ".png";
        if (!File.Exists(texPath)) return null;

        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (!tex) return null;

        if (!Directory.Exists(MatDir))
        {
            Directory.CreateDirectory(MatDir);
            AssetDatabase.Refresh();
        }

        string path = MatDir + "/" + matName + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.mainTexture != tex)
        {
            mat.mainTexture = tex;
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    static void LoadMaterials()
    {
        matFloorTerrazzo = GetOrCreateMaterial("Mat_Lib_Floor_Terrazzo", new Color(0.855f, 0.845f, 0.825f), 0.02f, 0.34f, null, false, "Assets/Interiors/Textures/Library_Granite_Tile.png", new Vector2(16.6f, 13.3f));
        matFloorDarkBasalt = GetOrCreateMaterial("Mat_Lib_Floor_DarkBasalt", new Color(0.20f, 0.21f, 0.23f), 0.10f, 0.26f);
        matFloorCarpet = GetOrCreateMaterial("Mat_Lib_Floor_Carpet", new Color(0.33f, 0.34f, 0.36f), 0.0f, 0.04f);

        matWallMicrocement = GetOrCreateMaterial("Mat_Lib_Wall_Microcement", new Color(0.855f, 0.845f, 0.825f), 0.0f, 0.05f);
        matWallDarkCharcoal = GetOrCreateMaterial("Mat_Lib_Wall_DarkCharcoal", new Color(0.17f, 0.18f, 0.20f), 0.15f, 0.25f);
        matWallAccentGreen = GetOrCreateMaterial("Mat_Lib_Wall_AccentGreen", new Color(0.16f, 0.28f, 0.24f), 0.05f, 0.18f);
        matCeilingWhite = GetOrCreateMaterial("Mat_Lib_Ceiling_White", new Color(0.90f, 0.90f, 0.89f), 0.0f, 0.04f);

        matWoodScandiOak = GetOrCreateMaterial("Mat_Lib_Wood_ScandiOak", new Color(0.80f, 0.70f, 0.55f), 0.0f, 0.24f, null, false, "Assets/Interiors/Textures/Library_Oak_Grain.png", new Vector2(1f, 1f));
        matWoodDarkWalnut = GetOrCreateMaterial("Mat_Lib_Wood_DarkWalnut", new Color(0.28f, 0.22f, 0.18f), 0.0f, 0.35f);
        matWoodBirch = GetOrCreateMaterial("Mat_Lib_Wood_Birch", new Color(0.84f, 0.78f, 0.68f), 0.0f, 0.22f, null, false, "Assets/Interiors/Textures/Library_Oak_Grain.png", new Vector2(1f, 1f));

        matMarbleWhite = GetOrCreateMaterial("Mat_Lib_Marble_White", new Color(0.80f, 0.80f, 0.805f), 0.02f, 0.30f);
        matStoneSlate = GetOrCreateMaterial("Mat_Lib_Stone_Slate", new Color(0.46f, 0.47f, 0.48f), 0.04f, 0.16f, null, false, "Assets/Interiors/Textures/Library_Stone_Dado.png", new Vector2(6f, 1f));

        matMetalStainless = GetOrCreateMaterial("Mat_Lib_Metal_Stainless", new Color(0.72f, 0.74f, 0.76f), 0.85f, 0.62f);
        matMetalTitanium = GetOrCreateMaterial("Mat_Lib_Metal_Titanium", new Color(0.24f, 0.25f, 0.27f), 0.85f, 0.60f);
        matMetalMatteBlack = GetOrCreateMaterial("Mat_Lib_Metal_MatteBlack", new Color(0.11f, 0.115f, 0.125f), 0.45f, 0.26f);
        matMetalBrass = GetOrCreateMaterial("Mat_Lib_Metal_Brass", new Color(0.72f, 0.58f, 0.30f), 0.88f, 0.66f);

        matGlassUltra = GetOrCreateMaterial("Mat_Lib_Glass_Ultra", new Color(0.88f, 0.93f, 0.97f, 0.16f), 0.05f, 0.94f, null, true);
        matGlassFrosted = GetOrCreateMaterial("Mat_Lib_Glass_Frosted", new Color(0.94f, 0.96f, 0.97f, 0.55f), 0.05f, 0.70f, null, true);

        matChairCream = GetOrCreateMaterial("Mat_Lib_Chair_Cream", new Color(0.80f, 0.79f, 0.76f), 0.0f, 0.14f);
        matChairSage = GetOrCreateMaterial("Mat_Lib_Chair_Sage", new Color(0.47f, 0.55f, 0.44f), 0.0f, 0.12f);
        matChairTerracotta = GetOrCreateMaterial("Mat_Lib_Chair_Terracotta", new Color(0.72f, 0.46f, 0.33f), 0.0f, 0.22f);
        matChairSlateBlue = GetOrCreateMaterial("Mat_Lib_Chair_SlateBlue", new Color(0.30f, 0.36f, 0.42f), 0.0f, 0.22f);

        // Lobby seating accents (reference image: orange-backed sofa, grey seat,
        // primary-coloured poufs).
        matSofaOrange = GetOrCreateMaterial("Mat_Lib_Sofa_Orange", new Color(0.94f, 0.50f, 0.12f), 0.0f, 0.26f);
        matSofaSeatGrey = GetOrCreateMaterial("Mat_Lib_Sofa_SeatGrey", new Color(0.25f, 0.26f, 0.28f), 0.0f, 0.22f);
        matStoolRed = GetOrCreateMaterial("Mat_Lib_Stool_Red", new Color(0.78f, 0.17f, 0.14f), 0.0f, 0.28f);
        matStoolYellow = GetOrCreateMaterial("Mat_Lib_Stool_Yellow", new Color(0.95f, 0.77f, 0.16f), 0.0f, 0.28f);
        matShrubLight = GetOrCreateMaterial("Mat_Lib_Shrub_Light", new Color(0.36f, 0.52f, 0.24f), 0.0f, 0.18f);
        // Near-black and almost fully matte so floor joints stay dark instead of
        // catching a specular highlight and reading as pale grey.
        matFloorJoint = GetOrCreateMaterial("Mat_Lib_Floor_Joint", new Color(0.13f, 0.13f, 0.135f), 0.0f, 0.06f);

        matLightWarmLED = GetOrCreateMaterial("Mat_Lib_Light_WarmLED", new Color(1.0f, 0.97f, 0.90f), 0f, 0.5f, new Color(1.0f, 0.95f, 0.86f) * 1.15f);
        matLightCoolLED = GetOrCreateMaterial("Mat_Lib_Light_CoolLED", new Color(0.95f, 0.97f, 1.0f), 0f, 0.5f, new Color(0.90f, 0.95f, 1.0f) * 1.05f);
        matSignBlackBox = GetOrCreateMaterial("Mat_Lib_Sign_BlackBox", new Color(0.13f, 0.14f, 0.16f), 0.35f, 0.40f);

        matPlantFoliage = GetOrCreateMaterial("Mat_Lib_Plant_Foliage", new Color(0.29f, 0.42f, 0.24f), 0.0f, 0.16f);
        matPlantPot = GetOrCreateMaterial("Mat_Lib_Plant_Pot", new Color(0.30f, 0.30f, 0.29f), 0.05f, 0.18f);

        matBook1 = GetOrCreateMaterial("Mat_Lib_Book_1", new Color(0.46f, 0.26f, 0.24f), 0f, 0.14f);
        matBook2 = GetOrCreateMaterial("Mat_Lib_Book_2", new Color(0.26f, 0.33f, 0.44f), 0f, 0.14f);
        matBook3 = GetOrCreateMaterial("Mat_Lib_Book_3", new Color(0.30f, 0.38f, 0.31f), 0f, 0.14f);
        matBook4 = GetOrCreateMaterial("Mat_Lib_Book_4", new Color(0.62f, 0.55f, 0.37f), 0f, 0.14f);
        matBook5 = GetOrCreateMaterial("Mat_Lib_Book_5", new Color(0.40f, 0.33f, 0.42f), 0f, 0.14f);
        matBook6 = GetOrCreateMaterial("Mat_Lib_Book_6", new Color(0.70f, 0.68f, 0.63f), 0f, 0.14f);

        matSofaFabric = GetOrCreateMaterial("Mat_Lib_Sofa_Fabric", new Color(0.44f, 0.47f, 0.45f), 0.0f, 0.10f);
        matRug = GetOrCreateMaterial("Mat_Lib_Rug", new Color(0.55f, 0.35f, 0.28f), 0.0f, 0.08f);

        // Optional exhibition artwork. The campus picture reuses the cafeteria facade texture if present.
        matHistoryPic = GetOrCreateTextureMaterial("Mat_Lib_History_Panel", "Library_History_Panel");
        matPlanPic = GetOrCreateTextureMaterial("Mat_Lib_FloorPlan_Panel", "Library_FloorPlan_Panel");
        matPosterPic = GetOrCreateTextureMaterial("Mat_Lib_Poster_Panel", "Library_Poster_Panel");
        matCampusPic = GetOrCreateTextureMaterial("Mat_Lib_Campus_Panel", "Faculty_Dining_Facade");

        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- primitives

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

    // An invisible collider-only barrier. Used to stop the player walking off the edge
    // of the floor through an open doorway, and to make the central opening safe.
    static GameObject MakeAirWall(Transform parent, string name, Vector3 pos, Vector3 size,
                                  Quaternion? rot = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        if (rot.HasValue) go.transform.localRotation = rot.Value;
        var col = go.AddComponent<BoxCollider>();
        col.size = size;              // local space; the transform is unscaled
        return go;
    }

    static GameObject MakeCylinder(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider = true)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale = size;
        if (mat) obj.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // IMPORTANT: Unity's Cylinder primitive ships with a CapsuleCollider, and its
        // capsule radius is half the WIDEST axis. For a flat disc such as the round
        // hall's floor (scale 30 x 0.2 x 30) height < 2*radius, so the capsule
        // degenerates into a SPHERE of radius 15 that bulges through the doorway and
        // silently blocks it. Swap in a convex MeshCollider that matches the actual
        // cylinder shape instead.
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        if (collider)
        {
            var mf = obj.GetComponent<MeshFilter>();
            var mc = obj.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;
        }
        return obj;
    }

    static GameObject MakeSphere(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool collider = false)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
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

        // Parent to parent.parent so text never inherits a non-uniform sign scale.
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

    // A wall-mounted textured panel, with a walnut frame and brass caption.
    // Falls back to a flat accent board when no texture is available.
    static GameObject MakeArtPanel(Transform parent, string name, Vector3 pos, Vector3 size, Quaternion rot, Material pic, Material fallback, string caption)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);
        root.localPosition = pos;
        root.localRotation = rot;

        MakeBox(root, "Frame", Vector3.zero, size + new Vector3(0.14f, 0.14f, 0.03f), matWoodDarkWalnut, false);
        var art = MakeBox(root, "Art", new Vector3(0, 0, 0.035f), size, pic ? pic : (fallback ? fallback : matWallAccentGreen), false);

        if (!string.IsNullOrEmpty(caption))
        {
            var plate = MakeBox(root, "Caption", new Vector3(0, -size.y * 0.5f - 0.19f, 0.035f), new Vector3(size.x * 0.72f, 0.24f, 0.02f), matMetalBrass, false);
            MakeText(plate.transform, caption, new Vector3(0, 0, -0.02f), Quaternion.Euler(0, 180, 0), 20, 0.022f, new Color(0.12f, 0.10f, 0.06f));
        }
        return art;
    }

    // ---------------------------------------------------------------- architecture

    static void BuildArchitectureShell(Transform parent)
    {
        var shell = new GameObject("00_Architecture_Shell").transform;
        shell.SetParent(parent, false);

        MakeBox(shell, "Floor_1F_Main", new Vector3(0, -0.1f, 0), new Vector3(HalfW * 2, 0.2f, HalfD * 2), matFloorTerrazzo);

        // Entrance thresholds
        MakeBox(shell, "Floor_1F_Threshold_South", new Vector3(0, 0.005f, -15.4f), new Vector3(5.4f, 0.01f, 1.6f), matFloorDarkBasalt, false);
        MakeBox(shell, "Floor_1F_Threshold_North", new Vector3(0, 0.005f, 15.4f), new Vector3(5.4f, 0.01f, 1.6f), matFloorDarkBasalt, false);
        MakeBox(shell, "Floor_1F_Threshold_West", new Vector3(-19.4f, 0.005f, 6.0f), new Vector3(1.4f, 0.01f, 4.4f), matFloorDarkBasalt, false);

        BuildSlab(shell, "Floor_2F_Slab", SlabY2);
        BuildSlab(shell, "Floor_3F_Slab", SlabY3);
        BuildSlab(shell, "Floor_4F_Slab", SlabY4);

        MakeBox(shell, "Ceiling_Roof", new Vector3(0, TotalH, 0), new Vector3(HalfW * 2, 0.2f, HalfD * 2), matCeilingWhite, false);

        // Perimeter walls, TotalH tall.
        // North wall (Z = +16): 1F rear door at X in [-2.2, 2.2]
        MakeBox(shell, "Wall_North_West", new Vector3(-11.1f, TotalH * 0.5f, HalfD), new Vector3(17.8f, TotalH, 0.3f), matWallMicrocement);
        MakeBox(shell, "Wall_North_East", new Vector3(11.1f, TotalH * 0.5f, HalfD), new Vector3(17.8f, TotalH, 0.3f), matWallMicrocement);
        MakeBox(shell, "Wall_North_DoorLintel", new Vector3(0, 3.0f + (TotalH - 3.0f) * 0.5f, HalfD), new Vector3(4.4f, TotalH - 3.0f, 0.3f), matWallMicrocement);

        // South wall (Z = -16): main entrance at X in [-2.4, 2.4]
        MakeBox(shell, "Wall_South_West", new Vector3(-11.2f, TotalH * 0.5f, -HalfD), new Vector3(17.6f, TotalH, 0.3f), matWallMicrocement);
        MakeBox(shell, "Wall_South_East", new Vector3(11.2f, TotalH * 0.5f, -HalfD), new Vector3(17.6f, TotalH, 0.3f), matWallMicrocement);
        MakeBox(shell, "Wall_South_DoorLintel", new Vector3(0, 3.0f + (TotalH - 3.0f) * 0.5f, -HalfD), new Vector3(4.8f, TotalH - 3.0f, 0.3f), matWallMicrocement);

        // West wall (X = -20). The corridor behind it is entered from 2F and 3F only, so
        // the wall is built in horizontal bands: solid everywhere except two 3 m doorway
        // slots, one per storey. 1F and 4F stay sealed, so the player can never step out
        // into empty space on those floors.
        MakeBox(shell, "Wall_West_North", new Vector3(-HalfW, TotalH * 0.5f, 12.1f), new Vector3(0.3f, TotalH, 7.8f), matWallMicrocement);
        MakeBox(shell, "Wall_West_South", new Vector3(-HalfW, TotalH * 0.5f, -6.1f), new Vector3(0.3f, TotalH, 19.8f), matWallMicrocement);

        const float DoorH = 3.0f;
        float d2b = Y2, d2t = Y2 + DoorH;      // 2F doorway: 4.3 .. 7.3
        float d3b = Y3, d3t = Y3 + DoorH;      // 3F doorway: 8.6 .. 11.6

        // Seals 1F, below the 2F doorway.
        MakeBox(shell, "Wall_West_Door_Sill", new Vector3(-HalfW, d2b * 0.5f, 6.0f), new Vector3(0.3f, d2b, 4.4f), matWallMicrocement);
        // Solid band between the 2F and 3F doorways.
        MakeBox(shell, "Wall_West_Door_Mid", new Vector3(-HalfW, (d2t + d3b) * 0.5f, 6.0f), new Vector3(0.3f, d3b - d2t, 4.4f), matWallMicrocement);
        // Seals everything above the 3F doorway, including 4F.
        MakeBox(shell, "Wall_West_Door_Head", new Vector3(-HalfW, (d3t + TotalH) * 0.5f, 6.0f), new Vector3(0.3f, TotalH - d3t, 4.4f), matWallMicrocement);

        // East wall (X = +20). The round reading hall is a tower bolted onto this wall
        // at Z -3..3, and 2F/3F/4F each open into it through a 6m doorway. 1F stays
        // solid because the tower starts at 2F.
        MakeBox(shell, "Wall_East_SouthRun", new Vector3(HalfW, TotalH * 0.5f, -9.5f), new Vector3(0.3f, TotalH, 13.0f), matWallMicrocement);
        MakeBox(shell, "Wall_East_NorthRun", new Vector3(HalfW, TotalH * 0.5f, 9.5f), new Vector3(0.3f, TotalH, 13.0f), matWallMicrocement);
        for (int f = 0; f < Storeys; f++)
        {
            float fy = FloorY(f);
            if (f == 0)
            {
                MakeBox(shell, "Wall_East_Solid_1F", new Vector3(HalfW, fy + FloorH * 0.5f, 0), new Vector3(0.3f, FloorH, 6.0f), matWallMicrocement);
            }
            else
            {
                MakeBox(shell, "Wall_East_DoorLintel_" + (f + 1) + "F",
                    new Vector3(HalfW, fy + 3.3f + (FloorH - 3.3f) * 0.5f, 0),
                    new Vector3(0.3f, FloorH - 3.3f, 6.0f), matWallMicrocement);
            }
        }

        // Baseboards on every floor (thin, non-colliding)
        for (int f = 0; f < Storeys; f++)
        {
            float y = FloorY(f) + 0.08f;
            MakeBox(shell, "Baseboard_" + (f + 1) + "F_North", new Vector3(0, y, HalfD - 0.18f), new Vector3(HalfW * 2, 0.16f, 0.05f), matWoodDarkWalnut, false);
            MakeBox(shell, "Baseboard_" + (f + 1) + "F_South", new Vector3(0, y, -HalfD + 0.18f), new Vector3(HalfW * 2, 0.16f, 0.05f), matWoodDarkWalnut, false);
            MakeBox(shell, "Baseboard_" + (f + 1) + "F_East", new Vector3(HalfW - 0.18f, y, 0), new Vector3(0.05f, 0.16f, HalfD * 2), matWoodDarkWalnut, false);
            MakeBox(shell, "Baseboard_" + (f + 1) + "F_West", new Vector3(-HalfW + 0.18f, y, 0), new Vector3(0.05f, 0.16f, HalfD * 2), matWoodDarkWalnut, false);
        }

        // No structural columns in the halls: they obstructed sightlines and walkways
        // (one sat on the 1F entrance axis, another on the 2F line to the great door),
        // and the halls read better as clear spans.

        // ---- Air walls across the two 1F doorways ----
        // The floor slab ends at the facade, so walking through an open door would step
        // off the edge into nothing. These invisible barriers stop that while leaving
        // the doorways visually open. Exiting is done with E, not by walking out.
        MakeAirWall(shell, "AirWall_SouthDoor", new Vector3(0, 1.7f, -HalfD + 0.05f), new Vector3(5.4f, 3.4f, 0.30f));
        MakeAirWall(shell, "AirWall_NorthDoor", new Vector3(0, 1.7f, HalfD - 0.05f), new Vector3(5.0f, 3.4f, 0.30f));
    }

    // One floor slab, assembled from rectangles so the stair and lift openings stay clear.
    //
    //   楼梯 stair opening  X [ 3.0,  9.4]   Z [-16.0, -11.0]
    //   电梯 lift shaft     X [10.0, 14.0]   Z [-16.0, -12.0]
    //
    // Everything else is solid deck, so 2F and 3F are walkable once they are furnished.
    static void BuildSlab(Transform parent, string name, float slabY)
    {
        var slab = new GameObject(name).transform;
        slab.SetParent(parent, false);

        Material floorMat = slabY > Y2 + 0.1f ? matFloorCarpet : matFloorTerrazzo;

        // North of the core: Z from -8.6 to +16
        MakeBox(slab, "Slab_North_Main", new Vector3(0, slabY, 3.7f), new Vector3(HalfW * 2, SlabT, 24.6f), floorMat);

        // West of the stair: X from -20 to 5.0, across the full southern band
        MakeBox(slab, "Slab_West", new Vector3(-7.5f, slabY, -12.3f), new Vector3(25.0f, SlabT, 7.4f), floorMat);

        // Stairwell decks. The ONLY opening that should remain is over the west lane,
        // where flight A climbs up through this level:
        //     flight A (west lane)  climbs 2.15m -> 4.3m over Z[-14.70, -11.62]
        //     flight B (east lane)  tops out at 2.15m, so it has ~2.0m of headroom
        //                           under this slab and can be decked over.
        if (slabY > 1f)   // any storey above the ground floor
        {
            // Arrival landing: where flight A tops out on the west lane.
            MakeBox(slab, "Slab_Stair_Landing", new Vector3(6.8f, slabY, -10.11f),
                new Vector3(3.6f, SlabT, 3.02f), floorMat);
            // Deck over the east lane (flight B below) so the floor has no hole here.
            MakeBox(slab, "Slab_East_Lane", new Vector3(10.0f, slabY, -12.3f),
                new Vector3(2.8f, SlabT, 7.4f), floorMat);
        }

        // Between the stair and the lift: X from 11.4 to 12.0
        MakeBox(slab, "Slab_Gap_Stair_Lift", new Vector3(11.7f, slabY, -12.3f), new Vector3(0.6f, SlabT, 7.4f), floorMat);

        // East wall strips contain the two holes left by an earlier mistake: the slab
        // must run continuously up to the wall face at X = 20, because the doorway
        // opening is formed by the WALL, not by cutting the floor.
        MakeBox(slab, "Slab_East", new Vector3(18.0f, slabY, -12.3f), new Vector3(4.0f, SlabT, 7.4f), floorMat);
        MakeBox(slab, "Slab_East_Mid", new Vector3(18.5f, slabY, 0f), new Vector3(3.0f, SlabT, 17.2f), floorMat);
        MakeBox(slab, "Slab_East_North", new Vector3(18.0f, slabY, 12.0f), new Vector3(4.0f, SlabT, 8.0f), floorMat);

        // Lift lobby floor: Z from -12.0 (shaft face) north to where the main slab
        // begins at -8.6. Previously only -12..-10 was decked, leaving a 4 x 1.4m hole
        // right in front of the lift doors.
        MakeBox(slab, "Slab_North_Of_Lift", new Vector3(14.0f, slabY, -10.3f), new Vector3(4.0f, SlabT, 3.4f), floorMat);

        // Guardrails. The stairwell's west and east edges are walled by the stair
        // enclosure, so a rail is only needed along the open edge of the west-lane
        // void, plus one across the lift shaft.
        float railY = slabY + SlabT * 0.5f + 0.55f;
        MakeBox(slab, "Rail_Stair_Void", new Vector3(8.6f, railY, -13.9f), new Vector3(0.06f, 1.1f, 4.4f), matMetalMatteBlack, false);
        MakeBox(slab, "Rail_Lift_North", new Vector3(14.0f, railY, -12.0f), new Vector3(4.0f, 1.1f, 0.06f), matMetalMatteBlack, false);
    }

    // Vertical circulation, all against the south wall in the south-east corner:
    //
    //   楼梯 (double-run switchback, 双跑折返)   X [ 3.0, 9.4]   Z [-16.0, -11.0]
    //   电梯 (adjacent lift shaft)              X [10.0,14.0]   Z [-16.0, -12.0]
    //
    // The stair is recessed inward from the wall: flight 1 runs north (+Z) from the
    // entry landing at Z = -13.6 up to a half landing at Z = -11.6, then flight 2
    // doubles back south (-Z) to arrive at 2F beside the entry.
    static void BuildVerticalCirculation(Transform parent)
    {
        var root = new GameObject("00_Vertical_Circulation").transform;
        root.SetParent(parent, false);

        // One switchback pair per storey: 1F->2F, 2F->3F, 3F->4F.
        BuildSwitchbackStair(root, "Stair_1F_to_2F", Y1, "一层", "二层");
        BuildSwitchbackStair(root, "Stair_2F_to_3F", Y2, "二层", "三层");
        BuildSwitchbackStair(root, "Stair_3F_to_4F", Y3, "三层", "四层");

        BuildElevator(root);
    }

    // Stair core. You enter from the LOBBY side (north, +Z) and walk in; the
    // enclosure is open on that face and sealed on the other three.
    //
    //            X 5.0 .. 8.1       X 8.3 .. 11.4
    //  Z -8.6    +-----------+=====+-----------+   <-- ENTRY (open, faces lobby)
    //            |           |     | Flight A  |
    //            |  (void)   |     | climbs    |
    //            |           |     | NORTH     |
    //  Z -13.4   |           |     |           |
    //            +-----------+     +-----------+
    //            |  半层平台  (half landing, full width)  |
    //  Z -14.6   +-----------+-----+-----------+
    //            | Flight B  |     |           |
    //            | climbs    |     |           |
    //            | SOUTH     |     |           |
    //  Z -16.0   +-----------+-----+-----------+   (facade wall)
    //
    // Route: enter at north -> Flight B (east lane) climbs SOUTH to the half
    // landing -> turn -> Flight A (west lane) climbs NORTH and arrives on 2F.
    const float StairX0 = 5.0f, StairX1 = 11.4f;
    const float StairZ0 = -16.0f;                 // south face (facade)
    static readonly float StairZ1 = StairZ0 + 7.4f;   // north face: -8.6, open to lobby

    static void BuildSwitchbackStair(Transform parent, string name, float baseY, string fromName, string toName)
    {
        var root = new GameObject(name).transform;
        root.SetParent(parent, false);

        const int stepsPerFlight = 11;
        float riser = FloorH / (stepsPerFlight * 2);   // 0.1955m, under the 0.3m step offset
        const float going = 0.28f;
        const float landingD = 1.2f;

        const float laneB_X = 9.85f;   // east lane (FIRST flight, entered from the lobby)
        const float laneA_X = 6.55f;   // west lane (SECOND flight, arrives on 2F)
        const float laneW = 2.9f;

        float run = stepsPerFlight * going;            // 3.08
        float midY = baseY + FloorH * 0.5f;

        float zLandN = StairZ0 + landingD;             // -14.8, north edge of the landing
        float zLandS = StairZ0;                        // -16.0
        // Flight B's top step must land ON the landing edge, so its bottom sits one
        // full run (3.08m) further north. Starting it any closer leaves a gap you
        // would fall through, which is what made the stairs unclimbable before.
        float zEnter = zLandN + run;                   // -11.72, first step

        // ---- Half landing across the full width at the south end ----
        MakeBox(root, "Half_Landing",
            new Vector3((StairX0 + StairX1) * 0.5f, midY - 0.075f, (zLandN + zLandS) * 0.5f),
            new Vector3(StairX1 - StairX0 - 0.1f, 0.15f, landingD), matFloorDarkBasalt);

        // ---- Flight B (EAST lane): the first flight, climbs SOUTH from the entry ----
        for (int i = 0; i < stepsPerFlight; i++)
        {
            float topY = baseY + (i + 1) * riser;
            float z = zEnter - (i + 0.5f) * going;
            MakeBox(root, "B_Riser_" + i, new Vector3(laneB_X, topY - riser * 0.5f, z),
                new Vector3(laneW, riser, going), matWallDarkCharcoal);
            MakeBox(root, "B_Tread_" + i, new Vector3(laneB_X, topY - 0.015f, z),
                new Vector3(laneW + 0.02f, 0.03f, going + 0.03f), matWoodScandiOak);
        }

        // ---- Flight A (WEST lane): the second flight, climbs NORTH to 2F ----
        for (int i = 0; i < stepsPerFlight; i++)
        {
            float topY = midY + (i + 1) * riser;
            float z = (zLandN + 0.1f) + (i + 0.5f) * going;
            MakeBox(root, "A_Riser_" + i, new Vector3(laneA_X, topY - riser * 0.5f, z),
                new Vector3(laneW, riser, going), matWallDarkCharcoal);
            MakeBox(root, "A_Tread_" + i, new Vector3(laneA_X, topY - 0.015f, z),
                new Vector3(laneW + 0.02f, 0.03f, going + 0.03f), matWoodScandiOak);
        }

        // ---- Enclosure: sealed west/east/south, OPEN on the north face ----
        float wallH = FloorH;
        float wallMidY = baseY + wallH * 0.5f;
        MakeBox(root, "Stair_Wall_West", new Vector3(StairX0, wallMidY, (StairZ0 + StairZ1) * 0.5f),
            new Vector3(0.16f, wallH, StairZ1 - StairZ0), matWallMicrocement);
        MakeBox(root, "Stair_Wall_East", new Vector3(StairX1, wallMidY, (StairZ0 + StairZ1) * 0.5f),
            new Vector3(0.16f, wallH, StairZ1 - StairZ0), matWallMicrocement);

        // Spine wall between the lanes -- stops short of the north entry so the
        // opening stays clear, and stops short of the landing so you can turn.
        float spineZ0 = zLandN + 0.1f, spineZ1 = zEnter - 0.4f;
        MakeBox(root, "Spine_Wall", new Vector3((laneA_X + laneB_X) * 0.5f, wallMidY, (spineZ0 + spineZ1) * 0.5f),
            new Vector3(0.18f, wallH, spineZ1 - spineZ0), matWallMicrocement);

        // ---- Handrails ----
        BuildFlightRail(root, "Rail_B", StairX1 - 0.10f, baseY, stepsPerFlight, riser, going, zEnter, false);
        BuildFlightRail(root, "Rail_A", StairX0 + 0.10f, midY, stepsPerFlight, riser, going, zLandN + 0.1f, true);

        // ---- Entry sign above the opening, facing the lobby ----
        var sign = MakeBox(root, "Sign_Stair_Entry",
            new Vector3((StairX0 + StairX1) * 0.5f, baseY + 2.52f, StairZ1 - 0.02f),
            new Vector3(3.6f, 0.44f, 0.07f), matSignBlackBox, false);
        MakeBox(sign.transform, "Glow", new Vector3(0, 0, 0.035f), new Vector3(3.34f, 0.32f, 0.03f), matLightWarmLED, false);
        // TextMesh faces +Z, which is the lobby side of this board: no yaw needed. It
        // previously carried a 180 Y rotation and therefore read mirrored from the hall.
        MakeText(sign.transform, "▲ 楼梯 STAIRS · 上行至" + toName, new Vector3(0, 0, 0.055f), Quaternion.identity, 24, 0.030f, Color.white);
    }

    // A sloped handrail following one flight, with newel posts at both ends.
    static void BuildFlightRail(Transform parent, string name, float x, float baseY, int steps, float riser, float going, float z0, bool northward)
    {
        float run = steps * going;
        float rise = steps * riser;
        float tilt = (northward ? -1f : 1f) * Mathf.Atan2(rise, run) * Mathf.Rad2Deg;

        var rail = MakeBox(parent, name,
            new Vector3(x, baseY + rise * 0.5f + 0.95f, z0 + (northward ? run : -run) * 0.5f),
            new Vector3(0.05f, 0.05f, run), matMetalMatteBlack, false);
        rail.transform.localRotation = Quaternion.Euler(tilt, 0, 0);

        MakeBox(parent, name + "_Post_A", new Vector3(x, baseY + 1.0f, z0), new Vector3(0.06f, 1.0f, 0.06f), matMetalMatteBlack, false);
        MakeBox(parent, name + "_Post_B", new Vector3(x, baseY + rise + 1.0f, z0 + (northward ? run : -run)), new Vector3(0.06f, 1.0f, 0.06f), matMetalMatteBlack, false);
    }

    // Elevator shaft immediately east of the stair, also against the south wall.
    static void BuildElevator(Transform parent)
    {
        var root = new GameObject("00_Elevator").transform;
        root.SetParent(parent, false);

        // Shaft footprint X [10.0, 14.0], Z [-16.0, -12.0]
        const float x0 = 12.0f, x1 = 16.0f, z0 = -16.0f, z1 = -12.0f;
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;

        // Shaft walls: north (facing the hall) carries the doors; west adjoins the stair.
        MakeBox(root, "Shaft_Wall_West", new Vector3(x0, TotalH * 0.5f, cz), new Vector3(0.2f, TotalH, z1 - z0), matWallMicrocement);
        MakeBox(root, "Shaft_Wall_East", new Vector3(x1, TotalH * 0.5f, cz), new Vector3(0.2f, TotalH, z1 - z0), matWallMicrocement);
        MakeBox(root, "Shaft_Wall_South", new Vector3(cx, TotalH * 0.5f, z0), new Vector3(x1 - x0, TotalH, 0.2f), matWallMicrocement);

        for (int f = 0; f < Storeys; f++)
        {
            float baseY = FloorY(f);
            // North face: door opening, with the lintel and side piers around it.
            MakeBox(root, "Shaft_North_Lintel_" + (f + 1) + "F", new Vector3(cx, baseY + 3.45f, z1), new Vector3(x1 - x0, 2.2f, 0.18f), matWallMicrocement);
            MakeBox(root, "Shaft_North_Pier_W_" + (f + 1) + "F", new Vector3(x0 + 0.15f, baseY + 1.25f, z1), new Vector3(0.3f, 2.5f, 0.18f), matWallMicrocement);
            MakeBox(root, "Shaft_North_Pier_E_" + (f + 1) + "F", new Vector3(x1 - 0.15f, baseY + 1.25f, z1), new Vector3(0.3f, 2.5f, 0.18f), matWallMicrocement);
        }

        for (int f = 0; f < Storeys; f++)
        {
            float baseY = FloorY(f);
            var doorRoot = new GameObject("Elevator_Door_" + (f + 1) + "F").transform;
            doorRoot.SetParent(root, false);

            // Brushed-metal car doors in a brass frame, set into the north wall.
            MakeBox(doorRoot, "Door_Frame", new Vector3(cx, baseY + 1.2f, z1 + 0.06f), new Vector3(2.0f, 2.4f, 0.10f), matMetalBrass);
            MakeBox(doorRoot, "Door_Leaf_W", new Vector3(cx - 0.48f, baseY + 1.2f, z1 + 0.12f), new Vector3(0.92f, 2.2f, 0.06f), matMetalStainless, false);
            MakeBox(doorRoot, "Door_Leaf_E", new Vector3(cx + 0.48f, baseY + 1.2f, z1 + 0.12f), new Vector3(0.92f, 2.2f, 0.06f), matMetalStainless, false);
            MakeBox(doorRoot, "Door_Sill", new Vector3(cx, baseY + 0.02f, z1 + 0.12f), new Vector3(2.0f, 0.04f, 0.3f), matMetalTitanium, false);

            // Floor indicator above the doors
            var ind = MakeBox(doorRoot, "Indicator", new Vector3(cx, baseY + 2.62f, z1 + 0.17f), new Vector3(0.7f, 0.22f, 0.05f), matSignBlackBox, false);
            MakeText(ind.transform, (f + 1) + "F", new Vector3(0, 0, 0.03f), Quaternion.Euler(0, 180, 0), 22, 0.026f, new Color(1f, 0.85f, 0.4f));

            // Call panel beside the doors
            MakeBox(doorRoot, "Call_Panel", new Vector3(cx + 1.25f, baseY + 1.15f, z1 + 0.15f), new Vector3(0.16f, 0.26f, 0.05f), matMetalTitanium, false);
        }

        MakeBox(root, "Shaft_Cap", new Vector3(cx, TotalH + 0.2f, cz), new Vector3(x1 - x0 + 0.2f, 0.2f, z1 - z0 + 0.2f), matWallDarkCharcoal, false);

        // Prominent sign above the lift doors on 1F, matching the stair sign style.
        var sign = MakeBox(root, "Sign_Elevator_1F", new Vector3(cx, 2.75f, z1 + 0.22f), new Vector3(3.4f, 0.62f, 0.07f), matSignBlackBox, false);
        MakeBox(sign.transform, "Glow", new Vector3(0, 0, 0.02f), new Vector3(3.2f, 0.5f, 0.03f), matLightWarmLED, false);
        MakeText(sign.transform, "电梯 ELEVATOR · 1F / 2F / 3F / 4F", new Vector3(0, 0, 0.05f), Quaternion.Euler(0, 180, 0), 24, 0.030f, Color.white);
    }

    // ---------------------------------------------------------------- doors, exits, spawns

    static void BuildDoorsAndExits(Transform sceneRootTransform, Transform interiorTransform, InteriorSceneRoot sceneRoot)
    {
        var root = new GameObject("00_Doors_And_Portals").transform;
        root.SetParent(interiorTransform, false);

        // South main entrance
        var sFrame = new GameObject("Door_South_Frame").transform;
        sFrame.SetParent(root, false);
        sFrame.localPosition = new Vector3(0, 0, -15.9f);
        MakeBox(sFrame, "Post_W", new Vector3(-2.4f, 1.5f, 0), new Vector3(0.2f, 3.0f, 0.2f), matMetalTitanium);
        MakeBox(sFrame, "Post_E", new Vector3(2.4f, 1.5f, 0), new Vector3(0.2f, 3.0f, 0.2f), matMetalTitanium);
        MakeBox(sFrame, "Top_Header", new Vector3(0, 3.0f, 0), new Vector3(5.0f, 0.2f, 0.2f), matMetalTitanium);
        MakeBox(sFrame, "Glass_W", new Vector3(-1.2f, 1.4f, 0), new Vector3(2.2f, 2.7f, 0.06f), matGlassUltra, false);
        MakeBox(sFrame, "Glass_E", new Vector3(1.2f, 1.4f, 0), new Vector3(2.2f, 2.7f, 0.06f), matGlassUltra, false);
        var signS = MakeBox(sFrame, "Sign_South", new Vector3(0, 2.72f, 0.06f), new Vector3(2.8f, 0.4f, 0.05f), matSignBlackBox, false);
        MakeText(signS.transform, "图书馆 · 正门 [E]", new Vector3(0, 0, 0.04f), Quaternion.Euler(0, 180, 0), 24, 0.034f, Color.white);

        // North rear door
        var nFrame = new GameObject("Door_North_Frame").transform;
        nFrame.SetParent(root, false);
        nFrame.localPosition = new Vector3(0, 0, 15.9f);
        MakeBox(nFrame, "Post_W", new Vector3(-2.2f, 1.5f, 0), new Vector3(0.2f, 3.0f, 0.2f), matMetalTitanium);
        MakeBox(nFrame, "Post_E", new Vector3(2.2f, 1.5f, 0), new Vector3(0.2f, 3.0f, 0.2f), matMetalTitanium);
        MakeBox(nFrame, "Top_Header", new Vector3(0, 3.0f, 0), new Vector3(4.6f, 0.2f, 0.2f), matMetalTitanium);
        MakeBox(nFrame, "Glass_W", new Vector3(-1.1f, 1.4f, 0), new Vector3(2.0f, 2.7f, 0.06f), matGlassUltra, false);
        MakeBox(nFrame, "Glass_E", new Vector3(1.1f, 1.4f, 0), new Vector3(2.0f, 2.7f, 0.06f), matGlassUltra, false);
        var signN = MakeBox(nFrame, "Sign_North", new Vector3(0, 2.72f, -0.06f), new Vector3(2.6f, 0.4f, 0.05f), matSignBlackBox, false);
        MakeText(signN.transform, "图书馆 · 后门 [E]", new Vector3(0, 0, -0.04f), Quaternion.identity, 24, 0.034f, Color.white);

        // West reserved corridor to the information building: closed, no transition.
        MakeBox(root, "Wall_West_ReservedClosure", new Vector3(-19.85f, 1.75f, 6.0f), new Vector3(0.12f, 3.5f, 4.4f), matGlassFrosted);
        var wSign = MakeBox(root, "Sign_West_Reserved", new Vector3(-19.7f, 3.05f, 6.0f), new Vector3(0.05f, 0.45f, 3.2f), matSignBlackBox, false);
        MakeText(wSign.transform, "◀ 通往信息楼 · 连廊暂未开放", new Vector3(0.04f, 0, 0), Quaternion.Euler(0, -90, 0), 22, 0.030f, Color.white);

        // Exit anchors
        var exitSouth = EnsureChild(sceneRootTransform, "Exit_South");
        exitSouth.localPosition = new Vector3(0f, 0f, -15.4f);
        exitSouth.localRotation = Quaternion.Euler(0, 180, 0);

        var exitNorth = EnsureChild(sceneRootTransform, "Exit_North");
        exitNorth.localPosition = new Vector3(0f, 0f, 15.4f);
        exitNorth.localRotation = Quaternion.identity;

        sceneRoot.exits = new Transform[] { exitSouth, exitNorth };

        // Bind each interior exit to the campus doorway it belongs to. Leaving through
        // Exit_South returns to the FRONT entrance ("library"), leaving through
        // Exit_North returns to the REAR entrance ("library-rear"). The array is
        // index-aligned with exits. Without this the player always came back to the
        // door they had entered by, regardless of which door they left through.
        sceneRoot.exitEntranceIds = new string[] { "library", "library-rear" };

        // Spawn anchors. Front-door spawn faces into the hall (+Z); rear-door spawn faces back (-Z).
        var spawnMain = EnsureChild(sceneRootTransform, "Spawn_main");
        spawnMain.localPosition = new Vector3(0f, 0.1f, -13.6f);
        spawnMain.localRotation = Quaternion.Euler(0, 0, 0);

        var spawnNorth = EnsureChild(sceneRootTransform, "Spawn_North");
        spawnNorth.localPosition = new Vector3(0f, 0.1f, 13.6f);
        spawnNorth.localRotation = Quaternion.Euler(0, 180, 0);

        sceneRoot.spawnPoints = new InteriorSpawnPoint[]
        {
            new InteriorSpawnPoint { id = "main", anchor = spawnMain },
            new InteriorSpawnPoint { id = "north", anchor = spawnNorth }
        };
    }

    static Transform EnsureChild(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing) return existing;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    // ---------------------------------------------------------------- entry point

    [MenuItem("Campus/Interiors/Build Library Four Floors")]
    [MenuItem("Campus/Interiors/Build Library Three Floors")]
    public static void Build()
    {
        LoadMaterials();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("Could not open library scene: " + ScenePath);
            return;
        }

        var rootObj = GameObject.Find("InteriorRoot · 图书馆");
        if (!rootObj)
        {
            rootObj = new GameObject("InteriorRoot · 图书馆");
        }

        var sceneRoot = rootObj.GetComponent<InteriorSceneRoot>();
        if (!sceneRoot) sceneRoot = rootObj.AddComponent<InteriorSceneRoot>();
        sceneRoot.buildingId = "library";
        sceneRoot.isPlaceholder = false;
        sceneRoot.exitRadius = 2.4f;
        sceneRoot.fallRecoveryHeight = -5f;

        if (!rootObj.GetComponent<LibraryIndoorInteractions>())
        {
            rootObj.AddComponent<LibraryIndoorInteractions>();
        }

        // Drop the placeholder shell and EVERY previous interior build.
        // The build root has been renamed over time, so remove any child whose name
        // starts with "Library_" (plus the historical "Cafeteria"-style names) rather
        // than a single literal name — otherwise old geometry accumulates in the scene
        // and stale objects survive a rebuild.
        var oldShell = rootObj.transform.Find("PlaceholderShell · 可替换的空房间");
        if (oldShell) UnityEngine.Object.DestroyImmediate(oldShell.gameObject);

        for (int i = rootObj.transform.childCount - 1; i >= 0; i--)
        {
            var child = rootObj.transform.GetChild(i);
            if (child.name.StartsWith("Library_") || child.name.StartsWith("PlaceholderShell"))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }

        // The spawn/exit anchors live on the root itself and are intentionally kept.
        var multiRoot = new GameObject("Library_Interior").transform;
        multiRoot.SetParent(rootObj.transform, false);

        BuildArchitectureShell(multiRoot);
        BuildVerticalCirculation(multiRoot);

        // 1F is the entrance lobby; 2F, 3F and 4F are identical round reading halls.
        var f1 = new GameObject("01_Floor_1F").transform;
        f1.SetParent(multiRoot, false);
        BuildFloor1(f1, Y1);

        for (int floor = 2; floor <= Storeys; floor++)
        {
            var root = new GameObject("0" + floor + "_Floor_" + floor + "F").transform;
            root.SetParent(multiRoot, false);
            BuildUpperFloor(root, FloorY(floor - 1), floor);
        }

        // The round tower is continuous through 2F..4F, so its roof is added once.
        BuildRoundRoof(multiRoot);

        BuildDoorsAndExits(rootObj.transform, multiRoot, sceneRoot);
        BuildLighting(multiRoot);

        // Interior presentation: same treatment as the teaching buildings and the
        // cafeteria — SSAO renderer, ACES volume, reflection probe, runtime switch.
        BuildPresentation(rootObj.transform);
        LibraryLightingBalance.Configure(rootObj.transform);
        InteriorLabelStairFix.ConfigureLabels(rootObj.transform, 4);
        LibraryHallWindows.Configure(rootObj.transform);

        BuildSeatsAndVending(rootObj.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("LIBRARY_BUILD_COMPLETE: built 1F lobby and 2F/3F/4F reading halls for 图书馆");
    }

    // Registers every chair as a numbered reservable seat and binds the two existing
    // vending machines, so the phone, the in-world interaction and the save all agree.
    static void BuildSeatsAndVending(Transform interiorRoot)
    {
        // Stable, human-readable ids: floor + area + running index within that area.
        var perArea = new System.Collections.Generic.Dictionary<string, int>();

        // Keep only the three reservable floors that actually have chairs.
        builtSeats.Sort((a, b) =>
            a.floor != b.floor ? a.floor.CompareTo(b.floor)
            : a.area != b.area ? string.CompareOrdinal(a.area, b.area)
            : a.sitPos.z != b.sitPos.z ? a.sitPos.z.CompareTo(b.sitPos.z)
            : a.sitPos.x.CompareTo(b.sitPos.x));

        var kept = new System.Collections.Generic.List<LibrarySeat>();
        foreach (var seat in builtSeats)
        {
            if (seat.floor < 1 || seat.floor > 3) continue;
            string key = seat.floor + "-" + seat.area;
            int n;
            perArea.TryGetValue(key, out n);
            n++;
            perArea[key] = n;
            seat.id = $"{seat.floor}F-{seat.area}-{n:D3}";
            kept.Add(seat);
        }

        // Rebuild the holder from scratch. Editing components on a reused object left
        // duplicate registries behind, which Unity then reported as "references runtime
        // script in scene file".
        var old = interiorRoot.Find("LibrarySeats");
        if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
        // Also clear any stray holder created in an earlier build.
        for (int i = interiorRoot.childCount - 1; i >= 0; i--)
        {
            var c = interiorRoot.GetChild(i);
            if (c.name.StartsWith("LibrarySeats")) UnityEngine.Object.DestroyImmediate(c.gameObject);
        }

        var go2 = new GameObject("LibrarySeats");
        go2.transform.SetParent(interiorRoot, false);
        var holder = go2.transform;

        var reg = holder.gameObject.AddComponent<LibrarySeatRegistry>();
        UnityEditor.EditorUtility.SetDirty(reg);
        UnityEditor.EditorUtility.SetDirty(holder.gameObject);
        UnityEditor.EditorUtility.SetDirty(holder);

        foreach (var seat in kept)
        {
            var go = new GameObject(seat.id);
            go.transform.SetParent(holder, false);
            go.transform.position = seat.sitPos;
            go.transform.rotation = seat.facing;
            var marker = go.AddComponent<LibrarySeatPart>();
            marker.seatId = seat.id;
            marker.floor = seat.floor;
            marker.area = seat.area;
            marker.areaName = seat.areaName;
            marker.standOffset = seat.standPos - seat.sitPos;
        }

        // Bind the machines that already exist in the 1F hall.
        var vendingGo = interiorRoot.Find("VendingMachines");
        if (!vendingGo)
        {
            var go = new GameObject("VendingMachines");
            go.transform.SetParent(interiorRoot, false);
            vendingGo = go.transform;
        }
        var vms = vendingGo.gameObject.GetComponents<VendingMachineUI>();
        for (int i = 1; i < vms.Length; i++) UnityEngine.Object.DestroyImmediate(vms[i]);
        var vm = vms.Length > 0 ? vms[0] : vendingGo.gameObject.AddComponent<VendingMachineUI>();

        Transform FindOrMake(string name, Vector3 pos)
        {
            var t = interiorRoot.Find(name);
            if (t) { t.position = pos; return t; }
            var go = new GameObject(name);
            go.transform.SetParent(interiorRoot, false);
            go.transform.position = pos;
            return go.transform;
        }
        vm.drinksMachine = FindOrMake("VendingAnchor_Drinks", new Vector3(-4.0f, Y1, -15.35f));
        vm.snacksMachine = FindOrMake("VendingAnchor_Snacks", new Vector3(4.0f, Y1, -15.35f));

        // Scene-side singletons used by the phone and the seat interaction.
        var sits = interiorRoot.GetComponents<LibrarySeatInteraction>();
        for (int i = 1; i < sits.Length; i++) UnityEngine.Object.DestroyImmediate(sits[i]);
        if (sits.Length == 0) interiorRoot.gameObject.AddComponent<LibrarySeatInteraction>();
        var boot = holder.gameObject.AddComponent<LibrarySeatBootstrap>();
        UnityEditor.EditorUtility.SetDirty(boot);

        // Serialise the phone into the library scene: runtime-created panels did not
        // appear on screen, while scene-hosted panels (the cafeteria one) render fine.
        var phones = interiorRoot.GetComponents<PhoneUI>();
        for (int i = 1; i < phones.Length; i++) UnityEngine.Object.DestroyImmediate(phones[i]);
        if (phones.Length == 0)
        {
            var ph = interiorRoot.gameObject.AddComponent<PhoneUI>();
            UnityEditor.EditorUtility.SetDirty(ph);
        }

        // Serialise the systems host into the library scene so the phone and the clock
        // exist even when the bootstrap does not run (e.g. entering the library directly).
        var hosts = interiorRoot.GetComponents<CampusSystemsHost>();
        for (int i = 1; i < hosts.Length; i++) UnityEngine.Object.DestroyImmediate(hosts[i]);
        if (hosts.Length == 0) interiorRoot.gameObject.AddComponent<CampusSystemsHost>();

        Debug.Log($"[Library] reservable seats: {kept.Count}  " +
                  $"1F={countOn(kept, 1)} 2F={countOn(kept, 2)} 3F={countOn(kept, 3)}");
    }

    static int countOn(System.Collections.Generic.List<LibrarySeat> list, int floor)
    {
        int n = 0;
        foreach (var s in list) if (s.floor == floor) n++;
        return n;
    }

    // ================================================================ 1F

    // Interior lighting for the furnished floors (1F and 2F). 3F is bare structure
    // and deliberately left unlit until its contents are specified.

    // Library is 40 x 32 m over four storeys with a 30 m round hall bolted on, so the
    // interior camera needs a longer far clip and a wider shadow reach than a classroom.
    static void BuildPresentation(Transform sceneRootTransform)
    {
        // Clear any grey-box placeholder light before the presentation is wired, so the
        // old flat wash cannot survive a rebuild.
        foreach (var l in sceneRootTransform.GetComponentsInChildren<Light>(true))
            if (l.name.StartsWith("Placeholder")) UnityEngine.Object.DestroyImmediate(l.gameObject);

        int rendererIndex;
        UniversalRendererData renderer;
        rendererIndex = InteriorPresentationSetup.EnsureInteriorRenderer(0.20f, out renderer);

        var profile = InteriorPresentationSetup.EnsureVolume(
            "Assets/Interiors/LibraryInterior/InteriorVolume.asset",
            postExposure: 0.22f, contrast: 6f, saturation: -3f,
            bloomIntensity: 0.06f, bloomThreshold: 1.25f);

        var volumeGo = sceneRootTransform.Find("图书馆 · 色调与柔和灯光");
        GameObject vol = volumeGo ? volumeGo.gameObject : new GameObject("图书馆 · 色调与柔和灯光");
        vol.transform.SetParent(sceneRootTransform, false);
        var volume = vol.GetComponent<Volume>();
        if (!volume) volume = vol.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 20;
        volume.sharedProfile = profile;

        // One probe over the main hall and one inside the round hall, so the glass,
        // stone floor and metal actually pick up the interior environment.
        InteriorPresentationSetup.EnsureProbe(sceneRootTransform, "图书馆材质反射",
            new Vector3(0f, 6.0f, 0f), new Vector3(42f, 18f, 34f), new Color(0.33f, 0.34f, 0.35f));
        InteriorPresentationSetup.EnsureProbe(sceneRootTransform, "圆厅材质反射",
            new Vector3(34.5f, 6.0f, 0f), new Vector3(32f, 18f, 32f), new Color(0.33f, 0.34f, 0.35f));

        var pres = sceneRootTransform.GetComponent<InteriorPresentation>();
        if (!pres) pres = sceneRootTransform.gameObject.AddComponent<InteriorPresentation>();
        pres.rendererIndex = rendererIndex;
        pres.interiorFarClip = 110f;      // must still see the far side of the round hall
        pres.interiorShadowDistance = 55f;
        pres.interiorCascades = 4;

        UnityEditor.EditorUtility.SetDirty(sceneRootTransform.gameObject);
    }

    static void BuildLighting(Transform parent)
    {
        var root = new GameObject("00_Interior_Lighting").transform;
        root.SetParent(parent, false);

        var sunObj = new GameObject("Main_Interior_Sun");
        sunObj.transform.SetParent(root, false);
        var sun = sunObj.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1.0f, 0.965f, 0.915f);
        sun.intensity = 0.55f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.72f;
        sun.shadowBias = 0.03f;
        sun.shadowNormalBias = 0.35f;
        sunObj.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        // Ambient drives the whole interior, so it is the main cause of the washed-out
        // white look. A lower, slightly warm ambient keeps wall, floor and desktop
        // texture visible while the LED strips do the actual lighting work.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.40f, 0.405f, 0.415f);

        // A skybox behind the glass means the curtain walls look out onto daylight
        // rather than flat grey, which was the biggest remaining "fake" tell.
        var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/CampusSky.mat");
        if (sky) { RenderSettings.skybox = sky; RenderSettings.fog = false; }

        // Recessed LED strips under every slab (1F..4F) and, on the upper storeys,
        // point lights so the concave glass cylinder is actually lit.
        for (int floor = 1; floor <= Storeys; floor++)
        {
            float ceilingY = FloorY(floor - 1) + FloorH - 0.07f;
            Material strip = floor == 1 ? matLightWarmLED : matLightCoolLED;

            for (float x = -16.5f; x <= 16.5f; x += 5.5f)
            {
                for (float z = -13f; z <= 13f; z += 5.2f)
                {
                    // Keep strips clear of the stair and lift core
                    if (x > 4.5f && x < 16.5f && z < -8.0f) continue;
                    var lamp = MakeBox(root, "Light_" + floor + "F_" + Mathf.RoundToInt(x) + "_" + Mathf.RoundToInt(z),
                        new Vector3(x, ceilingY, z), new Vector3(2.0f, 0.04f, 0.34f), strip, false);
                    // Recessed strips must not cast shadows: they sit right under the slab
                    // and their shadow map showed up as saw-tooth banding on the walls.
                    var lampMr = lamp.GetComponent<MeshRenderer>();
                    lampMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    lampMr.receiveShadows = false;
                }
            }
        }

        // Round-hall lighting, one set per storey (2F..4F).
        for (int floor = 2; floor <= Storeys; floor++)
        {
            float baseY = FloorY(floor - 1);

            // Wash lights on the curved wall: a single directional key cannot light a
            // concave cylinder, so these have to be local sources.
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                var go = new GameObject(floor + "F_Round_Wash_" + i);
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(
                    RoundCX + Mathf.Cos(a) * (RoundR - 3.5f),
                    baseY + 2.6f,
                    RoundCZ + Mathf.Sin(a) * (RoundR - 3.5f));
                var pl = go.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.range = 16f;
                pl.intensity = 1.35f;
                pl.color = new Color(1f, 0.98f, 0.94f);
                pl.shadows = LightShadows.None;
            }

            // A soft fill over the hall itself so the restroom and lobby read clearly.
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject(floor + "F_Hall_Fill_" + i);
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(-12f + i * 10f, baseY + 2.6f, -2f);
                var pl = go.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.range = 18f;
                pl.intensity = 1.25f;
                pl.color = new Color(1f, 0.98f, 0.95f);
                pl.shadows = LightShadows.None;
            }
        }
    }

    // 1F: entrance lobby with vending machines either side, a central campus sand-table
    // model, a north-east restroom, the south-east stair + lift core, the north rear
    // door, the reserved west corridor, and the history gallery east of the lift.
    static void BuildFloor1(Transform floorRoot, float baseY)
    {
        BuildLobbyFloorGraphics(floorRoot, baseY);
        BuildLobbySkirting(floorRoot, baseY);
        BuildLobbyWallPilasters(floorRoot, baseY);
        BuildSecurityGates(floorRoot, baseY);
        BuildVendingMachines(floorRoot, baseY);
        BuildCampusSandTable(floorRoot, baseY);
        BuildLobbyFlowerBed(floorRoot, baseY);

        // The south-east corner is far from the main light strips; without this it
        // reads as a dark pocket.
        var bedLight = new GameObject("1F_Bed_Light");
        bedLight.transform.SetParent(floorRoot, false);
        bedLight.transform.localPosition = new Vector3(16.6f, baseY + 3.5f, -7.0f);
        var bl = bedLight.AddComponent<Light>();
        bl.type = LightType.Point;
        bl.range = 10f;
        bl.intensity = 1.15f;
        bl.color = new Color(1f, 0.97f, 0.90f);
        bl.shadows = LightShadows.None;
        BuildLoungeCorner(floorRoot, baseY);
        BuildRestrooms(floorRoot, baseY, "1F", new Vector2(15.6f, 11.0f), 8.0f, 9.0f);
        BuildHistoryExhibition(floorRoot, baseY);
        BuildLobbyReception(floorRoot, baseY);
        BuildLibraryPlanPanels(floorRoot, baseY);
        BuildLobbyGreenery(floorRoot, baseY);
    }

    // ================================================================ 2F

    // 2F layout, as specified:
    //   * coming up the stair (west lane), the restroom is straight ahead;
    //   * leaving the lift, the big door on the right (east) leads into a large
    //     circular study hall that bulges out beyond the east wall.
    const float RoundR = 15f;                       // 30m diameter
    // The cylinder sits outside the east wall. A perfectly tangent circle would only
    // touch at a knife-edge, so it is pulled 0.5m west: a 0.5m throat through the wall
    // gives the doorway real depth without the room intruding into the 40x32 hall.
    const float RoundCX = HalfW + RoundR - 0.5f;    // = 34.5
    const float RoundCZ = 0f;
    const float RoundRoofY = 8.525f;                // top of the cylinder = 3F slab level

    // 2F, 3F and 4F are identical in layout, so one routine builds all three; the
    // storey number only goes into object names and label text.
    static void BuildUpperFloor(Transform floorRoot, float baseY, int floor)
    {
        currentBuildFloor = floor;
        currentSeatArea = "A";
        // The restroom is set INTO the north wall (Z = +16), not opposite the stair.
        // You walk north across the hall and find it against the far wall.
        BuildRestrooms(floorRoot, baseY, floor + "F", new Vector2(8.0f, 12.1f), 6.6f, 7.2f);

        BuildUpperFloorDressing(floorRoot, baseY, floor);
        currentSeatArea = "A";
        BuildRoundStudyHall(floorRoot, baseY, floor);
        BuildGreatDoor(floorRoot, baseY, floor);
        BuildUpperFloorLobby(floorRoot, baseY, floor);
        if (floor == 2 || floor == 3) BuildWestCorridor(floorRoot, baseY, floor);   // 2F + 3F
        currentSeatArea = "B";
        BuildUpperReadingArea(floorRoot, baseY, floor);
        if (floor == 2 || floor == 3) LibraryUpperHallLandscape.Build(floorRoot, baseY, floor);
    }

    // The circular reading hall: a cylinder sitting outside the east wall, reached
    // through the 6m doorway in that wall.
    //
    // Laid out radially, from the centre outwards exactly as specified:
    //
    //     r  0.0 .. 4.5   中央镂空 —— a REAL 9m opening in the floor (railed)
    //     r  4.5 .. 6.0   内圈过道
    //     r  ~6.6         环形书架（独立双面书架，留出通行缺口）
    //     r  7.2 .. 9.8   主过道
    //     r  ~11.0        自习桌环（桌椅 + 台灯）
    //     r 12.5 .. 15.0  外圈过道
    //     r 15.0          外墙：朝外落地玻璃，靠建筑一侧实墙
    //
    // The floor is assembled from concentric rings of boxes rather than one disc,
    // because a convex collider cannot have a hole in it.
    const float VoidR = 4.5f;                 // 9m central opening
    const float SolidWallSpan = 60f;          // solid wall within +/-60 deg of the door

    // Position on a ring. Uses X = CX + sin(a)*r, Z = CZ + cos(a)*r, which puts the
    // doorway (facing the hall, i.e. X = CX - r) at a = 270.
    static Vector3 RingPos(float a, float r, float y)
    {
        float rad = a * Mathf.Deg2Rad;
        return new Vector3(RoundCX + Mathf.Sin(rad) * r, y, RoundCZ + Mathf.Cos(rad) * r);
    }

    // Rotation whose LOCAL Z follows the circle's tangent (length runs around the arc).
    static Quaternion RingTangent(float a)
    {
        float rad = a * Mathf.Deg2Rad;
        return Quaternion.LookRotation(new Vector3(Mathf.Cos(rad), 0f, -Mathf.Sin(rad)), Vector3.up);
    }

    // Rotation whose LOCAL Z points along the outward radius (depth runs outward).
    // Local X then follows the tangent, so a box sized (across, h, deep) sits correctly.
    static Quaternion RingRadial(float a)
    {
        float rad = a * Mathf.Deg2Rad;
        return Quaternion.LookRotation(new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)), Vector3.up);
    }

    // One storey of the circular reading hall. The tower is built once per level
    // (2F, 3F, 4F) with identical internal layout; its roof is added at the top by
    // BuildRoundRoof.
    //
    // Radial layout, centre outwards:
    //     r 0.0 .. 4.5   中央镂空 —— a REAL 9m opening in the floor (railed)
    //     r 4.5 .. 6.0   内圈过道
    //     r ~6.6         环形书架（独立双面书架，留出通行缺口）
    //     r 7.2 .. 9.8   主过道
    //     r ~11.0        自习桌环（桌椅 + 台灯）
    //     r 12.5 .. 15.0  外圈过道
    //     r 15.0          外墙：朝外落地玻璃，靠建筑一侧实墙
    static void BuildRoundStudyHall(Transform floorRoot, float baseY, int floor)
    {
        currentSeatArea = "A";
        string tag = floor + "F_";
        var root = new GameObject(tag + "Round_Study_Hall").transform;
        root.SetParent(floorRoot, false);

        const int segs = 48;
        float segAngle = 360f / segs;
        float chord = 2f * RoundR * Mathf.Sin(Mathf.Deg2Rad * segAngle * 0.5f) * 1.06f;

        // ---- Floor: an annulus, so the 9m centre really is open ----
        BuildAnnulusFloor(root, baseY, VoidR, RoundR, tag);

        // ---- Railing around the opening ----
        BuildVoidRailing(root, baseY, VoidR, tag);

        // ---- Perimeter: glass facing outwards, solid wall toward the building ----
        float doorHalfAngle = Mathf.Rad2Deg * Mathf.Asin(3f / RoundR);
        const float doorAngle = 270f;

        for (int i = 0; i < segs; i++)
        {
            float a = i * segAngle;
            float offDoor = Mathf.Abs(Mathf.DeltaAngle(a, doorAngle));
            if (offDoor < doorHalfAngle) continue;               // the doorway itself
            bool solid = offDoor < SolidWallSpan;                // building side

            var seg = MakeBox(root, tag + (solid ? "Round_Wall_" : "Round_Glass_") + i,
                RingPos(a, RoundR, baseY + FloorH * 0.5f),
                new Vector3(0.35f, FloorH, chord),
                solid ? matWallMicrocement : matGlassUltra);
            seg.transform.localRotation = RingTangent(a);
        }

        // Head and sill beams running right around, so the glass reads as a curtain wall.
        for (int i = 0; i < segs; i++)
        {
            float a = i * segAngle;
            if (Mathf.Abs(Mathf.DeltaAngle(a, doorAngle)) < doorHalfAngle) continue;
            MakeBox(root, tag + "Round_Head_" + i, RingPos(a, RoundR, baseY + FloorH - 0.12f),
                new Vector3(0.42f, 0.24f, chord), matMetalTitanium, false)
                .transform.localRotation = RingTangent(a);
            MakeBox(root, tag + "Round_Sill_" + i, RingPos(a, RoundR, baseY + 0.10f),
                new Vector3(0.42f, 0.20f, chord), matMetalTitanium, false)
                .transform.localRotation = RingTangent(a);
        }

        // ---- Lintel over the doorway ----
        MakeBox(root, tag + "Round_Door_Lintel",
            new Vector3(RoundCX - RoundR, baseY + 3.75f, RoundCZ),
            new Vector3(0.35f, 1.1f, 6.6f), matWallMicrocement);

        // ---- Ring of study tables, just inside the glass ----
        // 20 tables of 2.2m at r=11 leaves a 1.26m gap between them (walkable);
        // a half-step offset puts one of those gaps on the door axis.
        BuildRoundStudyRing(root, baseY, 11.0f, 20, tag);

        // ---- Free-standing ring of bookcases between the void and the tables ----
        BuildRoundShelfRing(root, baseY, 6.6f, 22, tag);

        // ---- Air wall around the opening ----
        // The visible railing is only 1.1m tall and has gaps between its posts, so a
        // player could slip between them (or jump over) and drop into the void. This is
        // a solid, taller invisible ring sharing the same radius.
        for (int i = 0; i < segs; i++)
        {
            float a = i * segAngle;
            MakeAirWall(root, tag + "Void_AirWall_" + i,
                RingPos(a, VoidR, baseY + 1.1f),
                new Vector3(0.30f, 2.2f, chord * 1.1f),
                RingTangent(a));
        }

        // ---- Ring lights under this storey's ceiling ----
        for (int i = 0; i < 16; i++)
        {
            float a = i * (360f / 16f);
            MakeBox(root, tag + "Round_Light_" + i,
                RingPos(a, RoundR - 2.6f, baseY + FloorH - 0.15f),
                new Vector3(0.34f, 0.05f, 2.2f), matLightCoolLED, false)
                .transform.localRotation = RingTangent(a);
        }
    }

    // Roof over the whole round tower, added once after every storey is built.
    static void BuildRoundRoof(Transform parent)
    {
        var root = new GameObject("Round_Tower_Roof").transform;
        root.SetParent(parent, false);

        MakeCylinder(root, "Round_Roof",
            new Vector3(RoundCX, TotalH + 0.1f, RoundCZ),
            new Vector3(RoundR * 2f + 0.6f, 0.2f, RoundR * 2f + 0.6f), matCeilingWhite);
        MakeSphere(root, "Round_Dome",
            new Vector3(RoundCX, TotalH + 0.2f, RoundCZ),
            new Vector3(RoundR * 2f - 1.0f, 3.2f, RoundR * 2f - 1.0f), matCeilingWhite);
    }

    // A solid annulus floor built from concentric rings of overlapping boxes. Rings of
    // boxes (rather than one cylinder) are used deliberately: a convex collider cannot
    // represent a hole, so the opening would be sealed over.
    static void BuildAnnulusFloor(Transform parent, float baseY, float rIn, float rOut, string tag)
    {
        var root = new GameObject(tag + "Round_Floor_Annulus").transform;
        root.SetParent(parent, false);

        var rings = new (float ri, float ro, int n)[]
        {
            (rIn,    8.0f, 30),
            (8.0f,  12.0f, 36),
            (12.0f, rOut,  48),
        };

        foreach (var (ri, ro, n) in rings)
        {
            float rMid = (ri + ro) * 0.5f;
            float depth = ro - ri;
            float width = 2f * Mathf.PI * rMid / n * 1.20f;   // 20% overlap: no gaps

            for (int i = 0; i < n; i++)
            {
                float a = i * (360f / n);
                MakeBox(root, $"{tag}Floor_R{Mathf.RoundToInt(ri)}_{i}",
                    RingPos(a, rMid, baseY - 0.20f),
                    new Vector3(width, 0.40f, depth), matFloorTerrazzo)
                    .transform.localRotation = RingRadial(a);
            }
        }
    }

    // Guardrail around the central opening: posts plus a top rail, one segment per wall
    // segment so it follows the curve.
    static void BuildVoidRailing(Transform parent, float baseY, float r, string tag)
    {
        var root = new GameObject(tag + "Round_Void_Railing").transform;
        root.SetParent(parent, false);

        const int n = 36;
        float half = 2f * Mathf.PI * r / n * 0.62f;   // segment half-length, with overlap
        float len = half * 2f;

        for (int i = 0; i < n; i++)
        {
            float a = i * (360f / n);
            MakeBox(root, tag + "Rail_Top_" + i, RingPos(a, r, baseY + 1.05f),
                new Vector3(0.07f, 0.07f, len), matMetalMatteBlack, false)
                .transform.localRotation = RingTangent(a);
            MakeBox(root, tag + "Rail_Mid_" + i, RingPos(a, r, baseY + 0.60f),
                new Vector3(0.05f, 0.05f, len), matMetalMatteBlack, false)
                .transform.localRotation = RingTangent(a);
            // Infill panel behind the rails. These carry colliders so the player is
            // stopped by the visible glass rather than by nothing at all.
            if (i % 3 == 0)
                MakeBox(root, tag + "Rail_Solid_" + i, RingPos(a, r, baseY + 0.55f),
                    new Vector3(0.10f, 1.10f, len * 3f), matGlassFrosted)
                    .transform.localRotation = RingTangent(a);
            MakeBox(root, tag + "Rail_Post_" + i, RingPos(a, r, baseY + 0.55f),
                new Vector3(0.07f, 1.10f, 0.07f), matMetalMatteBlack);
        }
    }

    // A ring of reading tables, long axis following the curve, chairs on the inner and
    // outer sides, each place with a task lamp.
    static void BuildRoundStudyRing(Transform parent, float baseY, float radius, int count, string tag)
    {
        var root = new GameObject(tag + "Round_Study_Ring").transform;
        root.SetParent(parent, false);

        Material[] chairPalette = { matChairCream, matChairSage, matChairTerracotta, matChairSlateBlue };

        for (int i = 0; i < count; i++)
        {
            // Half-step offset so the gaps line up with the doorway at a = 270.
            float a = (i + 0.5f) * (360f / count);
            var t = new GameObject(tag + "Round_Table_" + i).transform;
            t.SetParent(root, false);
            t.localPosition = RingPos(a, radius, baseY);
            // Local X = tangent (table length), local Z = radial (table depth).
            t.localRotation = RingRadial(a);

            const float len = 2.2f, depth = 1.0f, topY = 0.75f;

            MakeBox(t, "Top", new Vector3(0, topY, 0), new Vector3(len, 0.05f, depth), matWoodScandiOak);
            MakeBox(t, "Apron", new Vector3(0, topY - 0.075f, 0), new Vector3(len - 0.22f, 0.075f, depth - 0.22f), matWoodDarkWalnut, false);
            // Thin darker edge band so the top does not read as a flat card.
            MakeBox(t, "Edge_Band", new Vector3(0, topY - 0.028f, 0), new Vector3(len - 0.005f, 0.012f, depth - 0.005f), matWoodDarkWalnut, false);
            MakeBox(t, "Leg_W", new Vector3(-len * 0.4f, topY * 0.5f, 0), new Vector3(0.09f, topY, depth * 0.82f), matMetalMatteBlack);
            MakeBox(t, "Leg_E", new Vector3(len * 0.4f, topY * 0.5f, 0), new Vector3(0.09f, topY, depth * 0.82f), matMetalMatteBlack);

            // Slim task lamps: a thin stem with a small domed shade, scaled to the table
            // rather than the old mushroom-shaped post.
            for (int k = 0; k < 2; k++)
            {
                float lx = -len * 0.25f + k * len * 0.5f;
                MakeCylinder(t, "Lamp_Base_" + k, new Vector3(lx, topY + 0.02f, 0.30f),
                    new Vector3(0.11f, 0.012f, 0.11f), matMetalMatteBlack, false);
                MakeCylinder(t, "Lamp_Stem_" + k, new Vector3(lx, topY + 0.19f, 0.30f),
                    new Vector3(0.018f, 0.17f, 0.018f), matMetalMatteBlack, false);
                MakeCylinder(t, "Lamp_Shade_" + k, new Vector3(lx, topY + 0.37f, 0.22f),
                    new Vector3(0.16f, 0.045f, 0.16f), matMetalMatteBlack, false);
                MakeCylinder(t, "Lamp_Glow_" + k, new Vector3(lx, topY + 0.335f, 0.22f),
                    new Vector3(0.145f, 0.008f, 0.145f), matLightWarmLED, false);
            }

            MakeBox(t, "Power_Pillar", new Vector3(0, topY + 0.07f, 0), new Vector3(0.24f, 0.09f, 0.24f), matMetalStainless, false);

            // Chairs face the table from the outward (+Z) and inward (-Z) sides.
            BuildChair(t, new Vector3(0, 0, depth * 0.5f + 0.42f), 0, chairPalette[i % chairPalette.Length]);
            BuildChair(t, new Vector3(0, 0, -depth * 0.5f - 0.42f), 180, chairPalette[(i + 1) % chairPalette.Length]);
        }
    }

    // A free-standing double-sided ring of bookcases. Four evenly spaced gaps let you
    // walk between the outer ring of tables and the railed opening.
    static void BuildRoundShelfRing(Transform parent, float baseY, float radius, int bays, string tag)
    {
        var root = new GameObject(tag + "Round_Shelf_Ring").transform;
        root.SetParent(parent, false);

        const float shelfH = 2.1f;
        Material[] palette = { matBook1, matBook2, matBook3, matBook4, matBook5, matBook6 };
        float width = 2f * Mathf.PI * radius / bays * 1.06f;   // slight overlap, no gaps

        for (int i = 0; i < bays; i++)
        {
            float a = i * (360f / bays);
            // Access gaps: four evenly spaced, plus one on the doorway axis so you can
            // walk straight in from the door towards the railed opening.
            if (i % 5 == 0) continue;
            if (Mathf.Abs(Mathf.DeltaAngle(a, 270f)) < 11f) continue;
            var bay = new GameObject(tag + "Shelf_Bay_" + i).transform;
            bay.SetParent(root, false);
            bay.localPosition = RingPos(a, radius, baseY);
            // Local X = tangent (bay width), local Z = radial (shelf depth).
            bay.localRotation = RingRadial(a);

            // Light oak end panels and back, with a dark metal frame edge: the combination
            // the reference uses for the shelving.
            MakeBox(bay, "Back", new Vector3(0, shelfH * 0.5f, 0), new Vector3(width, shelfH, 0.06f), matWoodBirch, false);
            MakeBox(bay, "Side_W", new Vector3(-width * 0.5f, shelfH * 0.5f, 0), new Vector3(0.06f, shelfH, 0.66f), matWoodBirch);
            MakeBox(bay, "Side_E", new Vector3(width * 0.5f, shelfH * 0.5f, 0), new Vector3(0.06f, shelfH, 0.66f), matWoodBirch);
            MakeBox(bay, "Frame_Top", new Vector3(0, shelfH - 0.025f, 0), new Vector3(width + 0.02f, 0.05f, 0.70f), matMetalMatteBlack, false);
            MakeBox(bay, "Frame_Base", new Vector3(0, 0.045f, 0), new Vector3(width, 0.09f, 0.66f), matMetalMatteBlack, false);
            MakeBox(bay, "Top", new Vector3(0, shelfH, 0), new Vector3(width, 0.04f, 0.72f), matWoodBirch, false);
            // End-panel trim caps in dark metal, tying the panels to the frame.
            foreach (float sx in new[] { -1f, 1f })
                MakeBox(bay, "Edge_Trim", new Vector3(sx * (width * 0.5f - 0.005f), shelfH * 0.5f, 0.33f),
                    new Vector3(0.02f, shelfH, 0.02f), matMetalMatteBlack, false);

            var rng = new System.Random(i * 613 + 29);
            for (int s = 0; s <= 4; s++)
            {
                float y = 0.20f + s * ((shelfH - 0.35f) / 4f);
                MakeBox(bay, "Shelf_" + s, new Vector3(0, y, 0), new Vector3(width, 0.035f, 0.62f), matWoodBirch, s == 0);
            }

            // Spines on BOTH faces of every shelf.
            for (int s = 1; s <= 4; s++)
            {
                float y = 0.20f + (s - 1) * ((shelfH - 0.35f) / 4f) + 0.12f;
                for (int face = 0; face < 2; face++)
                {
                    float fz = face == 0 ? -0.16f : 0.16f;
                    float cursor = -width * 0.5f + 0.08f;
                    while (cursor < width * 0.5f - 0.14f)
                    {
                        float bw = 0.045f + (float)rng.NextDouble() * 0.07f;
                        float bh = 0.19f + (float)rng.NextDouble() * 0.09f;
                        if (rng.NextDouble() < 0.08) { cursor += 0.14f; continue; }
                        MakeBox(bay, "Book_" + s + "_" + face + "_" + Mathf.RoundToInt((cursor + 2f) * 100),
                            new Vector3(cursor + bw * 0.5f, y + bh * 0.5f - 0.01f, fz),
                            new Vector3(bw, bh, 0.2f), palette[rng.Next(palette.Length)], false);
                        cursor += bw + 0.006f;
                    }
                }
            }

            var label = MakeBox(bay, "Label", new Vector3(-width * 0.5f + 0.2f, shelfH + 0.16f, 0),
                new Vector3(0.5f, 0.22f, 0.05f), matSignBlackBox, false);
            MakeText(label.transform, "A-" + (i + 1).ToString("00"), new Vector3(0, 0, 0.04f), Quaternion.identity, 18, 0.018f, Color.white);
        }
    }

    // The "big door" between the hall and the round room: a framed opening in the
    // east wall at Z = 0, with glass leaves and a header sign.
    static void BuildGreatDoor(Transform parent, float baseY, int floor)
    {
        var root = new GameObject(floor + "F_Great_Door").transform;
        root.SetParent(parent, false);

        const float halfW = 3.0f;

        // Jambs
        MakeBox(root, "Jamb_S", new Vector3(HalfW - 0.1f, baseY + 1.65f, -halfW), new Vector3(0.5f, 3.3f, 0.35f), matMetalTitanium);
        MakeBox(root, "Jamb_N", new Vector3(HalfW - 0.1f, baseY + 1.65f, halfW), new Vector3(0.5f, 3.3f, 0.35f), matMetalTitanium);
        // Header
        MakeBox(root, "Header", new Vector3(HalfW - 0.1f, baseY + 3.45f, 0), new Vector3(0.5f, 0.35f, 2f * halfW + 0.7f), matMetalTitanium);

        // Glass leaves, slightly ajar as if held open
        var leafS = MakeBox(root, "Leaf_S", new Vector3(HalfW - 0.12f, baseY + 1.55f, -halfW + 1.45f), new Vector3(0.08f, 3.1f, 2.7f), matGlassUltra, false);
        leafS.transform.localRotation = Quaternion.Euler(0, -22f, 0);
        var leafN = MakeBox(root, "Leaf_N", new Vector3(HalfW - 0.12f, baseY + 1.55f, halfW - 1.45f), new Vector3(0.08f, 3.1f, 2.7f), matGlassUltra, false);
        leafN.transform.localRotation = Quaternion.Euler(0, 22f, 0);

        // Push bars
        MakeBox(root, "Push_Bar_S", new Vector3(HalfW - 0.2f, baseY + 1.05f, -halfW + 1.45f), new Vector3(0.06f, 0.06f, 2.3f), matMetalStainless, false);
        MakeBox(root, "Push_Bar_N", new Vector3(HalfW - 0.2f, baseY + 1.05f, halfW - 1.45f), new Vector3(0.06f, 0.06f, 2.3f), matMetalStainless, false);

        // Illuminated sign over the door, facing west into the hall
        var sign = MakeBox(root, "Sign_RoundHall", new Vector3(HalfW - 0.42f, baseY + 2.95f, 0), new Vector3(0.08f, 0.5f, 4.2f), matSignBlackBox, false);
        MakeBox(sign.transform, "Glow", new Vector3(-0.03f, 0, 0), new Vector3(0.03f, 0.4f, 3.9f), matLightWarmLED, false);
        MakeText(sign.transform, "◀ 圆形自习厅 · ROUND READING HALL", new Vector3(0.06f, 0, 0), Quaternion.Euler(0, -90, 0), 26, 0.032f, Color.white);
    }

    // ---------------------------------------------------------------- upper-floor dressing
    // Shared by 2F/3F/4F. Adds what the reference plan shows that the upper halls were
    // missing: a stainless drinking fountain opposite the stair, granite floor joints
    // with black edge trim, and the grey stone dado with its black capping and skirting.
    // The east wall, its great door and the round study hall are deliberately untouched.

    static void BuildUpperFloorDressing(Transform parent, float baseY, int floor)
    {
        BuildDrinkingFountain(parent, baseY, floor);
        BuildUpperFloorFloorGraphics(parent, baseY, floor);
        BuildUpperFloorDado(parent, baseY, floor);
    }

    // Multi-tap stainless drinking fountain against the north wall, roughly opposite
    // the stair, as in the reference plan.
    static void BuildDrinkingFountain(Transform parent, float baseY, int floor)
    {
        var root = new GameObject(floor + "F_Drinking_Fountain").transform;
        root.SetParent(parent, false);

        // Against the north wall (inner face Z = +15.85), west of the restroom door so
        // the two do not crowd each other. The unit faces south into the hall.
        Vector3 c = new Vector3(13.1f, baseY, 15.45f);

        // Splash-back panel on the wall and the basin body standing out from it.
        MakeBox(root, "Fountain_Panel", c + new Vector3(0f, 1.25f, 0.20f), new Vector3(1.60f, 2.05f, 0.08f), matStoneSlate, false);
        MakeBox(root, "Fountain_Body", c + new Vector3(0f, 0.84f, 0f), new Vector3(1.48f, 0.60f, 0.44f), matMetalStainless);
        MakeBox(root, "Fountain_Basin", c + new Vector3(0f, 1.16f, 0f), new Vector3(1.38f, 0.07f, 0.38f), matMetalStainless, false);

        // Three taps, spouts pointing SOUTH (into the hall), not into the wall.
        foreach (float ox in new[] { -0.48f, 0f, 0.48f })
        {
            MakeBox(root, "Tap_Base", c + new Vector3(ox, 1.33f, 0.10f), new Vector3(0.11f, 0.18f, 0.11f), matMetalMatteBlack, false);
            MakeBox(root, "Tap_Spout", c + new Vector3(ox, 1.41f, -0.07f), new Vector3(0.05f, 0.05f, 0.24f), matMetalStainless, false);
        }

        // Drain grille across the basin front and a cup dispenser that sits IN the body.
        MakeBox(root, "Fountain_Grille", c + new Vector3(0f, 1.12f, -0.19f), new Vector3(1.32f, 0.09f, 0.03f), matMetalMatteBlack, false);
        MakeBox(root, "Cup_Dispenser", c + new Vector3(0.62f, 1.40f, 0.02f), new Vector3(0.22f, 0.30f, 0.16f), matMetalStainless, false);

        // Plinth aligned with the body so nothing appears to float.
        MakeBox(root, "Fountain_Base", c + new Vector3(0f, 0.07f, 0f), new Vector3(1.46f, 0.14f, 0.46f), matMetalMatteBlack, false);

        // Label high on the splash-back, facing the hall.
        var sign = MakeBox(root, "Fountain_Sign", c + new Vector3(0f, 2.06f, 0.15f), new Vector3(0.86f, 0.20f, 0.03f), matSignBlackBox, false);
        MakeText(sign.transform, "饮水机", new Vector3(0f, 0f, -0.02f), Quaternion.Euler(0, 180, 0), 20, 0.020f, Color.white);
    }

    // Granite floor joints plus the black stone border running round the hall edge,
    // matching the reference floor. Pure decals: no colliders, no walk impact.
    static void BuildUpperFloorFloorGraphics(Transform parent, float baseY, int floor)
    {
        var root = new GameObject(floor + "F_Floor_Graphics").transform;
        root.SetParent(parent, false);

        const float y = 0.007f;

        void Strip(string name, float cx, float cz, float sx, float sz, Material m)
            => MakeBox(root, name, new Vector3(cx, baseY + y, cz), new Vector3(sx, 0.010f, sz), m, false);

        // Black stone border band just inside the walls.
        const float bandW = 0.45f;
        float x0 = -HalfW + bandW * 0.5f, x1 = HalfW - bandW * 0.5f;
        float z0 = -HalfD + bandW * 0.5f, z1 = HalfD - bandW * 0.5f;

        Strip("Border_S", 0f, z0, HalfW * 2f - bandW * 2f, bandW, matFloorDarkBasalt);
        Strip("Border_N", 0f, z1, HalfW * 2f - bandW * 2f, bandW, matFloorDarkBasalt);
        Strip("Border_W", x0, 0f, bandW, HalfD * 2f - bandW * 2f, matFloorDarkBasalt);
        Strip("Border_E", x1, 0f, bandW, HalfD * 2f - bandW * 2f, matFloorDarkBasalt);

        // Thin joint grid across the open floor.
        const float t = 0.028f;
        for (float x = -16f; x <= 16f; x += 4f)
            Strip("Joint_X_" + x, x, 0f, t, HalfD * 2f - bandW * 2f, matFloorJoint);
        for (float z = -12f; z <= 12f; z += 4f)
            Strip("Joint_Z_" + z, 0f, z, HalfW * 2f - bandW * 2f, t, matFloorJoint);
    }

    // Grey textured stone dado on the lower half of the perimeter walls, with a black
    // capping line on top and black skirting at the floor, as in the reference.
    static void BuildUpperFloorDado(Transform parent, float baseY, int floor)
    {
        var root = new GameObject(floor + "F_Dado").transform;
        root.SetParent(parent, false);

        const float dadoH = 1.10f;      // grey stone up to this height
        const float capH = 0.05f;
        const float skirtH = 0.12f;
        const float thick = 0.03f;

        float dadoY = baseY + skirtH + dadoH * 0.5f;
        float capY = baseY + skirtH + dadoH + capH * 0.5f;
        float skirtY = baseY + skirtH * 0.5f;

        // Two segments per wall so the dado never runs across a doorway.
        foreach (var (name, cx, cz, sx, sz) in new (string, float, float, float, float)[]
        {
            ("S_W", -11.2f, -HalfD + thick * 0.5f, 17.6f, thick),
            ("S_E",  11.2f, -HalfD + thick * 0.5f, 17.6f, thick),
            ("N_W", -11.1f,  HalfD - thick * 0.5f, 17.8f, thick),
            ("N_E",  11.1f,  HalfD - thick * 0.5f, 17.8f, thick),
            ("W_S", -HalfW + thick * 0.5f,  -6.05f, thick, 19.6f),
            ("W_N", -HalfW + thick * 0.5f,  12.05f, thick, 7.6f),
        })
        {
            MakeBox(root, "Dado_" + name, new Vector3(cx, dadoY, cz), new Vector3(sx, dadoH, sz), matStoneSlate, false);
            MakeBox(root, "DadoCap_" + name, new Vector3(cx, capY, cz), new Vector3(sx, capH, sz + 0.01f), matMetalMatteBlack, false);
            MakeBox(root, "Skirt_" + name, new Vector3(cx, skirtY, cz), new Vector3(sx, skirtH, sz + 0.01f), matMetalMatteBlack, false);
        }
    }

    // ---------------------------------------------------------------- 2F west corridor

    // The long corridor behind the west wall, built identically on 2F and 3F. The route
    // is: come up the stair, walk north across the open hall, turn left, and pass through
    // the doorway the shell leaves for that storey. Nothing is built in the hall itself
    // to hide it: the doorway faces east while the corridor runs west, so from the stair
    // you look at its edge and cannot see down it until you actually turn.
    const float CorrZ0 = 3.8f, CorrZ1 = 8.2f, CorrXEnd = -38.0f, CorrHeight = 3.0f;

    // Free-standing reading desks in the open part of the upper hall, west of the main
    // north-south route so they never block the stair-to-restroom or stair-to-corridor
    // walks. Each desk is a 1.8 m oak top on dark legs with four chairs.
    static void BuildUpperReadingArea(Transform parent, float baseY, int floor)
    {
        var root = new GameObject(floor + "F_Reading_Area").transform;
        root.SetParent(parent, false);

        float[] xs = { -13.0f, -7.6f };
        float[] zs = { -6.4f, -1.6f, 3.2f };

        int n = 0;
        foreach (float z in zs)
        {
            foreach (float x in xs)
            {
                BuildReadingDesk(root, new Vector3(x, baseY, z), 0f, n);
                n++;
            }
        }
    }

    static void BuildReadingDesk(Transform parent, Vector3 pos, float rotY, int index)
    {
        var t = new GameObject("Reading_Desk_" + index).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        t.localRotation = Quaternion.Euler(0, rotY, 0);

        const float len = 1.8f, depth = 0.9f, topY = 0.75f;

        InteriorGeometry.RoundedBox(t, "Top", new Vector3(0, topY, 0), new Vector3(len, 0.05f, depth), 0.022f, matWoodScandiOak, true);
        InteriorGeometry.RoundedBox(t, "Edge_Band", new Vector3(0, topY - 0.026f, 0),
            new Vector3(len - 0.006f, 0.012f, depth - 0.006f), 0.006f, matWoodDarkWalnut, false);

        // Round tube legs at each end, with a slim stretcher.
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                InteriorGeometry.Tube(t, "Leg",
                    new Vector3(sx * (len * 0.42f), 0.01f, sz * (depth * 0.34f)),
                    new Vector3(sx * (len * 0.42f), topY - 0.02f, sz * (depth * 0.34f)),
                    0.022f, matMetalMatteBlack);
        InteriorGeometry.Tube(t, "Stretcher", new Vector3(-len * 0.42f, 0.20f, 0f),
            new Vector3(len * 0.42f, 0.20f, 0f), 0.015f, matMetalMatteBlack);

        // Shared task lamp + a couple of books so the desks are not bare.
        MakeCylinder(t, "Desk_Lamp_Base", new Vector3(0, topY + 0.02f, 0), new Vector3(0.10f, 0.012f, 0.10f), matMetalMatteBlack, false);
        MakeCylinder(t, "Desk_Lamp_Stem", new Vector3(0, topY + 0.16f, 0), new Vector3(0.016f, 0.14f, 0.016f), matMetalMatteBlack, false);
        MakeCylinder(t, "Desk_Lamp_Shade", new Vector3(0, topY + 0.31f, 0), new Vector3(0.13f, 0.04f, 0.13f), matMetalMatteBlack, false);
        MakeCylinder(t, "Desk_Lamp_Glow", new Vector3(0, topY + 0.285f, 0), new Vector3(0.115f, 0.007f, 0.115f), matLightWarmLED, false);

        var rng = new System.Random(index * 97 + 11);
        Material[] books = { matBook1, matBook2, matBook3, matBook4, matBook5, matBook6 };
        for (int b = 0; b < 2; b++)
        {
            float bx = -0.55f + b * 1.10f;
            MakeBox(t, "Book_" + b, new Vector3(bx, topY + 0.035f, 0.18f),
                new Vector3(0.20f, 0.035f, 0.15f), books[(index + b) % books.Length], false);
        }

        // Two chairs per long side, tucked in and facing the desk.
        foreach (float sx in new[] { -0.45f, 0.45f })
        {
            BuildChair(t, new Vector3(sx, 0, depth * 0.5f + 0.40f), 0f, matChairSlateBlue);
            BuildChair(t, new Vector3(sx, 0, -depth * 0.5f - 0.40f), 180f, matChairSage);
        }
    }

    static void BuildWestCorridor(Transform parent, float baseY, int floor)
    {
        string pre = floor + "F_Corridor";
        var root = new GameObject(pre).transform;
        root.SetParent(parent, false);

        float zc = (CorrZ0 + CorrZ1) * 0.5f;
        float zw = CorrZ1 - CorrZ0;
        float xInner = -HalfW + 0.15f;                  // inner face of the west wall
        float xc = (xInner + CorrXEnd) * 0.5f;
        float spanX = xInner - CorrXEnd;
        float ceilY = baseY + CorrHeight;

        // Floor and ceiling. The floor runs 0.35 m past the wall line so the doorway has
        // no seam and no lip to catch the character controller on.
        MakeBox(root, pre + "_Floor", new Vector3(xc, baseY - 0.10f, zc),
            new Vector3(spanX + 0.35f, 0.20f, zw), matFloorTerrazzo);
        MakeBox(root, pre + "_Ceiling", new Vector3(xc, ceilY + 0.10f, zc),
            new Vector3(spanX + 0.35f, 0.20f, zw), matCeilingWhite, false);

        // Side walls and the far end wall.
        MakeBox(root, pre + "_Wall_N", new Vector3(xc, baseY + CorrHeight * 0.5f, CorrZ1 + 0.15f),
            new Vector3(spanX + 0.35f, CorrHeight, 0.30f), matWallMicrocement);
        MakeBox(root, pre + "_Wall_S", new Vector3(xc, baseY + CorrHeight * 0.5f, CorrZ0 - 0.15f),
            new Vector3(spanX + 0.35f, CorrHeight, 0.30f), matWallMicrocement);
        MakeBox(root, pre + "_Wall_W", new Vector3(CorrXEnd - 0.15f, baseY + CorrHeight * 0.5f, zc),
            new Vector3(0.30f, CorrHeight, zw + 0.60f), matWallMicrocement);

        // Grey stone dado with black capping and skirting, matching the hall.
        const float dadoH = 1.10f, capH = 0.05f, skirtH = 0.12f;
        foreach (var tag in new[] { "N", "S" })
        {
            float z = tag == "N" ? CorrZ1 - 0.015f : CorrZ0 + 0.015f;
            MakeBox(root, pre + "_Dado_" + tag, new Vector3(xc, baseY + skirtH + dadoH * 0.5f, z),
                new Vector3(spanX, dadoH, 0.03f), matStoneSlate, false);
            MakeBox(root, pre + "_DadoCap_" + tag, new Vector3(xc, baseY + skirtH + dadoH + capH * 0.5f, z),
                new Vector3(spanX, capH, 0.04f), matMetalMatteBlack, false);
            MakeBox(root, pre + "_Skirt_" + tag, new Vector3(xc, baseY + skirtH * 0.5f, z),
                new Vector3(spanX, skirtH, 0.04f), matMetalMatteBlack, false);
        }

        // Black stone floor edges, as in the hall.
        foreach (var tag in new[] { "N", "S" })
        {
            float z = tag == "N" ? CorrZ1 - 0.225f : CorrZ0 + 0.225f;
            MakeBox(root, pre + "_Edge_" + tag, new Vector3(xc, baseY + 0.007f, z),
                new Vector3(spanX, 0.012f, 0.45f), matFloorDarkBasalt, false);
        }

        // Closed doors for rooms that are not built yet. They sit ON the wall face, so the
        // wall behind them stays solid and nothing leads into empty space.
        foreach (var x in new[] { -22.6f, -27.4f, -32.2f, -36.4f })
            CorridorDoor(root, pre, x, CorrZ1 - 0.04f, -1f, baseY);
        foreach (var x in new[] { -25.0f, -34.3f })
            CorridorDoor(root, pre, x, CorrZ0 + 0.04f, 1f, baseY);

        // Lighting: a ceiling strip plus a point light per bay.
        for (int i = 0; i < 4; i++)
        {
            float x = -22.0f - i * 4.6f;
            MakeBox(root, pre + "_Light_" + i, new Vector3(x, ceilY - 0.03f, zc),
                new Vector3(1.8f, 0.06f, 0.32f), matLightWarmLED, false);

            var lg = new GameObject(pre + "_Point_" + i);
            lg.transform.SetParent(root, false);
            lg.transform.localPosition = new Vector3(x, ceilY - 0.45f, zc);
            var pl = lg.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.range = 8.5f;
            pl.intensity = 1.05f;
            pl.color = new Color(1f, 0.96f, 0.90f);
            pl.shadows = LightShadows.None;
        }

        // Bench against the north wall, clear of the last two door bays.
        float benchX = CorrXEnd + 3.7f;
        float benchZ = CorrZ1 - 0.4f;
        MakeBox(root, pre + "_Bench", new Vector3(benchX, baseY + 0.44f, benchZ),
            new Vector3(2.4f, 0.09f, 0.55f), matWoodScandiOak);
        MakeBox(root, pre + "_BenchBase", new Vector3(benchX, baseY + 0.21f, benchZ),
            new Vector3(2.2f, 0.42f, 0.16f), matMetalMatteBlack, false);
        BuildPotPlant(root, new Vector3(CorrXEnd + 1.1f, baseY, CorrZ0 + 1.0f), 1.1f);

        // Direction sign on the hall side of the doorway.
        var sign = MakeBox(root, pre + "_Sign_Hall", new Vector3(-19.66f, baseY + 2.62f, zc),
            new Vector3(0.05f, 0.46f, 2.8f), matSignBlackBox, false);
        MakeText(sign.transform, "◀ 长走廊 · 阅览区", new Vector3(0.04f, 0f, 0f),
            Quaternion.Euler(0, -90, 0), 22, 0.030f, Color.white);
    }

    static void CorridorDoor(Transform root, string pre, float x, float z, float facing, float baseY)
    {
        float f = facing;
        MakeBox(root, pre + "_DoorFrame", new Vector3(x, baseY + 1.10f, z),
            new Vector3(1.14f, 2.24f, 0.06f), matMetalMatteBlack, false);
        MakeBox(root, pre + "_DoorLeaf", new Vector3(x, baseY + 1.06f, z + f * 0.05f),
            new Vector3(0.96f, 2.10f, 0.05f), matWoodDarkWalnut, false);
        MakeBox(root, pre + "_DoorPanelTop", new Vector3(x, baseY + 1.60f, z + f * 0.08f),
            new Vector3(0.74f, 0.60f, 0.02f), matWoodDarkWalnut, false);
        MakeBox(root, pre + "_DoorPanelBot", new Vector3(x, baseY + 0.70f, z + f * 0.08f),
            new Vector3(0.74f, 0.80f, 0.02f), matWoodDarkWalnut, false);
        MakeBox(root, pre + "_DoorHandle", new Vector3(x + 0.36f, baseY + 1.02f, z + f * 0.11f),
            new Vector3(0.05f, 0.05f, 0.07f), matMetalBrass, false);
        MakeBox(root, pre + "_DoorPlate", new Vector3(x, baseY + 2.44f, z + f * 0.03f),
            new Vector3(0.34f, 0.16f, 0.03f), matSignBlackBox, false);
    }

    static void BuildUpperFloorLobby(Transform parent, float baseY, int floor)
    {
        var root = new GameObject(floor + "F_Lobby").transform;
        root.SetParent(parent, false);

        // Directory board near the stair exit
        var dir = MakeBox(root, floor + "F_Sign_Directory", new Vector3(4.6f, baseY + 2.4f, -10.6f), new Vector3(0.06f, 1.3f, 2.4f), matSignBlackBox, false);
        MakeText(dir.transform, FloorName(floor) + " · " + floor + "F", new Vector3(0.04f, 0.46f, 0), Quaternion.Euler(0, -90, 0), 26, 0.034f, new Color(1f, 0.9f, 0.6f));
        MakeText(dir.transform, "← 卫生间", new Vector3(0.04f, 0.14f, 0), Quaternion.Euler(0, -90, 0), 20, 0.024f, Color.white);
        MakeText(dir.transform, "圆形自习厅 →", new Vector3(0.04f, -0.12f, 0), Quaternion.Euler(0, -90, 0), 20, 0.024f, Color.white);
        MakeText(dir.transform, "楼梯 / 电梯", new Vector3(0.04f, -0.38f, 0), Quaternion.Euler(0, -90, 0), 20, 0.024f, Color.white);

        // Waiting bench and a plant just north of the lift lobby
        var bench = new GameObject("Bench").transform;
        bench.SetParent(root, false);
        bench.localPosition = new Vector3(17.5f, baseY, -7.0f);
        MakeBox(bench, "Seat", new Vector3(0, 0.44f, 0), new Vector3(1.1f, 0.1f, 2.6f), matWoodScandiOak);
        MakeBox(bench, "Leg_S", new Vector3(0, 0.2f, -1.1f), new Vector3(0.9f, 0.4f, 0.1f), matMetalMatteBlack, false);
        MakeBox(bench, "Leg_N", new Vector3(0, 0.2f, 1.1f), new Vector3(0.9f, 0.4f, 0.1f), matMetalMatteBlack, false);

        BuildPotPlant(root, new Vector3(17.5f, baseY, -10.0f), 1.2f);
        BuildPotPlant(root, new Vector3(4.2f, baseY, -13.4f), 1.0f);
    }

    // Two vending machines, one on each side of the south entrance door, standing
    // against the south wall just inside. The door opening is X [-2.4, 2.4], so the
    // machines sit at X = +/-4.0 and face north (+Z) into the lobby.
    // ---------------------------------------------------------------- 1F lobby dressing
    // Everything below is 1F only. 2F/3F/4F, the exterior and the building shell are
    // untouched by these methods.

    // The reference lobby floor is large grey terrazzo divided by thin dark lines.
    // These are flat inlays a few millimetres above the slab (never z-fighting) with
    // no collider at all, so they are pure decoration and cannot affect movement.
    static void BuildLobbyFloorGraphics(Transform parent, float baseY)
    {
        var root = new GameObject("1F_Floor_Graphics").transform;
        root.SetParent(parent, false);

        const float t = 0.07f;
        const float lift = 0.007f;

        void Strip(string name, float cx, float cz, float sx, float sz)
            => MakeBox(root, name, new Vector3(cx, baseY + lift, cz),
                       new Vector3(sx, 0.012f, sz), matMetalMatteBlack, false);

        void Rect(string tag, float x0, float x1, float z0, float z1)
        {
            float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
            float sx = x1 - x0, sz = z1 - z0;
            Strip(tag + "_S", cx, z0, sx + t, t);
            Strip(tag + "_N", cx, z1, sx + t, t);
            Strip(tag + "_W", x0, cz, t, sz + t);
            Strip(tag + "_E", x1, cz, t, sz + t);
        }

        // Two nested divisions across the open middle, plus the south-east bay
        // outline that frames the flower bed and the lift core.
        Rect("Joint_Outer", -13.0f, 13.0f, -10.0f, 11.0f);
        Rect("Joint_Inner", -8.5f, 8.5f, -6.0f, 7.0f);
        Rect("Joint_SE", 11.2f, 19.5f, -14.2f, -2.2f);
    }

    // Black skirting where the white wall meets the stone floor, split so it never
    // floats inside a doorway.
    static void BuildLobbySkirting(Transform parent, float baseY)
    {
        var root = new GameObject("1F_Skirting").transform;
        root.SetParent(parent, false);

        const float h = 0.13f, d = 0.05f;
        float y = baseY + h * 0.5f;
        Material m = matMetalMatteBlack;

        // South wall (inner face Z = -15.85), main door X in [-2.4, 2.4]
        MakeBox(root, "Skirt_S_W", new Vector3(-11.2f, y, -15.83f), new Vector3(17.6f, h, d), m, false);
        MakeBox(root, "Skirt_S_E", new Vector3(11.2f, y, -15.83f), new Vector3(17.6f, h, d), m, false);
        // North wall (inner face Z = +15.85), rear door X in [-2.2, 2.2]
        MakeBox(root, "Skirt_N_W", new Vector3(-11.1f, y, 15.83f), new Vector3(17.8f, h, d), m, false);
        MakeBox(root, "Skirt_N_E", new Vector3(11.1f, y, 15.83f), new Vector3(17.8f, h, d), m, false);
        // West wall (X = -19.85), reserved corridor gap Z in [3.8, 8.2]
        MakeBox(root, "Skirt_W_S", new Vector3(-19.83f, y, -6.03f), new Vector3(d, h, 19.65f), m, false);
        MakeBox(root, "Skirt_W_N", new Vector3(-19.83f, y, 12.03f), new Vector3(d, h, 7.65f), m, false);
        // East wall (X = +19.85); 1F has no east doorway
        MakeBox(root, "Skirt_E", new Vector3(19.83f, y, 0f), new Vector3(d, h, 31.7f), m, false);
    }

    // White pilasters against the north and south walls. They give the room the white
    // vertical rhythm of the reference while standing flush to the wall, so nothing at
    // all is placed in the walking lines.
    static void BuildLobbyWallPilasters(Transform parent, float baseY)
    {
        var root = new GameObject("1F_Wall_Pilasters").transform;
        root.SetParent(parent, false);

        const float w = 0.9f, d = 0.26f, h = 3.0f;
        float y = baseY + h * 0.5f;

        foreach (float x in new[] { -13.5f, -7.0f, 7.0f, 13.5f })
        {
            MakeBox(root, "Pilaster_S", new Vector3(x, y, -15.85f + d * 0.5f), new Vector3(w, h, d), matMarbleWhite);
            MakeBox(root, "Pilaster_N", new Vector3(x, y, 15.85f - d * 0.5f), new Vector3(w, h, d), matMarbleWhite);
        }
    }

    // Stainless-and-glass security gates just inside the main entrance, as in the
    // reference. Two clear walking lanes stay open; only the pods have colliders.
    static void BuildSecurityGates(Transform parent, float baseY)
    {
        var root = new GameObject("1F_Security_Gates").transform;
        root.SetParent(parent, false);

        // Pulled north of the front-door spawn point (Z = -13.6) and split into FOUR
        // pods, so the middle lane lines up with the spawn and the player walks
        // straight through instead of being stopped by a pod on arrival.
        const float z = -11.80f;

        foreach (float x in new[] { -5.40f, -1.80f, 1.80f, 5.40f })
        {
            MakeBox(root, "Gate_Pod", new Vector3(x, baseY + 0.58f, z),
                new Vector3(0.46f, 1.16f, 1.30f), matMetalStainless);
            MakeBox(root, "Gate_Top", new Vector3(x, baseY + 1.19f, z),
                new Vector3(0.50f, 0.06f, 1.34f), matMetalMatteBlack, false);
            MakeBox(root, "Gate_Led", new Vector3(x, baseY + 1.10f, z + 0.66f),
                new Vector3(0.14f, 0.08f, 0.02f), matLightCoolLED, false);
        }

        foreach (float x in new[] { -6.30f, 6.30f })
        {
            MakeBox(root, "Gate_Screen", new Vector3(x, baseY + 0.62f, z),
                new Vector3(0.06f, 1.24f, 1.30f), matGlassUltra);
            MakeBox(root, "Gate_Screen_Post", new Vector3(x, baseY + 1.26f, z),
                new Vector3(0.10f, 0.08f, 1.34f), matMetalMatteBlack, false);
        }
    }

    // The big corner bed in the lower-right of the reference: a low dark-stone rim
    // holding layered greenery with small red flowers along the outer edge. It sits in
    // the south-east quadrant, east of the lift core and hard against the east wall, so
    // the lift exit corridor (X 12..16) and the 1F history panel (Z ~ 0) both stay clear.
    static void BuildLobbyFlowerBed(Transform parent, float baseY)
    {
        var root = new GameObject("1F_Flower_Bed").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(17.95f, baseY, -7.30f);

        const float rx = 1.75f, rz = 2.90f;

        // Low dark-stone kerb as a smooth flattened ellipse (one scaled cylinder), so
        // the outline has no segment steps. MakeCylinder swaps in a convex MeshCollider,
        // so it also blocks the player correctly instead of degenerating to a sphere.
        MakeCylinder(root, "Bed_Kerb", new Vector3(0f, 0.15f, 0f),
            new Vector3(rx * 2f, 0.15f, rz * 2f), matStoneSlate);

        // Soil mound sitting just proud of the kerb.
        MakeCylinder(root, "Bed_Soil", new Vector3(0f, 0.31f, 0f),
            new Vector3((rx - 0.10f) * 2f, 0.04f, (rz - 0.10f) * 2f), matPlantPot);

        // Layered planting: tall in the middle, falling away to low edge planting.
        var rng = new System.Random(90210);
        for (int i = 0; i < 42; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float rr = Mathf.Sqrt((float)rng.NextDouble()) * 0.84f;
            float x = Mathf.Cos(a) * rr * rx;
            float z = Mathf.Sin(a) * rr * rz;
            float hgt = 0.55f + (1f - rr) * 0.85f + (float)rng.NextDouble() * 0.35f;
            float wid = 0.42f + (float)rng.NextDouble() * 0.28f;

            MakeSphere(root, "Shrub_" + i, new Vector3(x, 0.32f + hgt * 0.5f, z),
                new Vector3(wid, hgt, wid) * 0.9f, matShrubLight);
        }

        // Small red flowers dotted around the rim.
        for (int i = 0; i < 46; i++)
        {
            float a = i / 46f * Mathf.PI * 2f + 0.06f;
            float j = 0.82f + (float)rng.NextDouble() * 0.12f;
            float fy = 0.38f + (float)rng.NextDouble() * 0.05f;
            MakeSphere(root, "Flower_" + i,
                new Vector3(Mathf.Cos(a) * rx * j, fy, Mathf.Sin(a) * rz * j),
                new Vector3(0.11f, 0.09f, 0.11f), matStoolRed);
        }
    }

    // North-east lounge from the reference: an orange-backed crescent sofa, coloured
    // poufs, a low table, the black-framed glass reading booth ("小黑屋") and a white
    // self-service kiosk. It all sits between the north wall and the restroom's west
    // wall, clear of the rear-door approach and of the restroom door.
    static void BuildLoungeCorner(Transform parent, float baseY)
    {
        var root = new GameObject("1F_Lounge").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(0f, baseY, 0f);

        MakeBox(root, "Lounge_Rug", new Vector3(4.80f, 0.006f, 9.90f),
            new Vector3(4.40f, 0.012f, 3.20f), matRug, false);

        // The sofa is held west so a 2m walking gap stays open between its east end
        // and the booth's west wall.
        BuildArcSofa(root, new Vector3(4.80f, 0f, 10.60f), 1.70f);
        BuildCoffeeTable(root, new Vector3(4.80f, 0f, 8.85f));
        BuildPouf(root, new Vector3(3.20f, 0f, 8.55f), matStoolRed);
        BuildPouf(root, new Vector3(6.40f, 0f, 8.55f), matStoolYellow);
        BuildReadingBooth(root, new Vector3(10.00f, 0f, 13.20f));
        BuildInfoKiosk(root, new Vector3(10.80f, 0f, 8.60f), 250f);
    }

    // Crescent sofa: segments along a half ellipse so the back follows the room corner
    // the way the reference does. Orange back, dark grey seat.
    static void BuildArcSofa(Transform parent, Vector3 center, float radius)
    {
        var root = new GameObject("Sofa_Arc").transform;
        root.SetParent(parent, false);
        root.localPosition = center;

        const int segs = 15;
        const float a0 = -0.10f * Mathf.PI;
        const float a1 = 1.10f * Mathf.PI;

        for (int i = 0; i < segs; i++)
        {
            float t = i / (float)(segs - 1);
            float a = Mathf.Lerp(a0, a1, t);
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Quaternion rot = Quaternion.LookRotation(dir);   // local +Z points outward

            var seat = MakeBox(root, "Seat_" + i, dir * radius + new Vector3(0f, 0.44f, 0f),
                new Vector3(0.62f, 0.16f, 0.82f), matSofaSeatGrey);
            seat.transform.localRotation = rot;

            var back = MakeBox(root, "Back_" + i, dir * (radius + 0.26f) + new Vector3(0f, 0.80f, 0f),
                new Vector3(0.62f, 0.64f, 0.16f), matSofaOrange, false);
            back.transform.localRotation = rot;

            var bas = MakeBox(root, "Base_" + i, dir * radius + new Vector3(0f, 0.14f, 0f),
                new Vector3(0.58f, 0.28f, 0.70f), matMetalMatteBlack, false);
            bas.transform.localRotation = rot;
        }
    }

    static void BuildPouf(Transform parent, Vector3 pos, Material mat)
    {
        var root = new GameObject("Pouf").transform;
        root.SetParent(parent, false);
        root.localPosition = pos;

        MakeCylinder(root, "Pouf_Seat", new Vector3(0f, 0.23f, 0f), new Vector3(0.52f, 0.23f, 0.52f), mat);
        MakeCylinder(root, "Pouf_Base", new Vector3(0f, 0.03f, 0f), new Vector3(0.40f, 0.03f, 0.40f), matMetalMatteBlack, false);
    }

    static void BuildCoffeeTable(Transform parent, Vector3 pos)
    {
        var root = new GameObject("Coffee_Table").transform;
        root.SetParent(parent, false);
        root.localPosition = pos;

        MakeCylinder(root, "Table_Top", new Vector3(0f, 0.40f, 0f), new Vector3(1.05f, 0.04f, 1.05f), matWoodDarkWalnut);
        MakeCylinder(root, "Table_Leg", new Vector3(0f, 0.19f, 0f), new Vector3(0.16f, 0.19f, 0.16f), matMetalMatteBlack, false);
        MakeCylinder(root, "Table_Foot", new Vector3(0f, 0.02f, 0f), new Vector3(0.55f, 0.02f, 0.55f), matMetalMatteBlack, false);
    }

    // "小黑屋": black-framed glass reading booth with a clear doorway on the
    // lobby-facing (west) side.
    static void BuildReadingBooth(Transform parent, Vector3 pos)
    {
        var root = new GameObject("Reading_Booth").transform;
        root.SetParent(parent, false);
        root.localPosition = pos;

        const float w = 3.00f, d = 3.00f, h = 2.60f;
        float hx = w * 0.5f, hz = d * 0.5f;

        MakeBox(root, "Booth_Plinth", new Vector3(0f, 0.04f, 0f), new Vector3(w, 0.08f, d), matFloorDarkBasalt);

        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                MakeBox(root, "Booth_Post", new Vector3(sx * (hx - 0.07f), h * 0.5f, sz * (hz - 0.07f)),
                    new Vector3(0.14f, h, 0.14f), matMetalMatteBlack);

        MakeBox(root, "Booth_Head", new Vector3(0f, h - 0.07f, 0f), new Vector3(w, 0.14f, d), matMetalMatteBlack);
        MakeBox(root, "Booth_Roof", new Vector3(0f, h + 0.03f, 0f), new Vector3(w - 0.10f, 0.06f, d - 0.10f), matWallDarkCharcoal, false);

        MakeBox(root, "Booth_Glass_N", new Vector3(0f, h * 0.5f, hz - 0.05f), new Vector3(w - 0.24f, h - 0.24f, 0.05f), matGlassUltra);
        MakeBox(root, "Booth_Glass_E", new Vector3(hx - 0.05f, h * 0.5f, 0f), new Vector3(0.05f, h - 0.24f, d - 0.24f), matGlassUltra);
        MakeBox(root, "Booth_Glass_S", new Vector3(0f, h * 0.5f, -hz + 0.05f), new Vector3(w - 0.24f, h - 0.24f, 0.05f), matGlassUltra);

        // West face: a clear doorway in the middle with glass either side.
        const float doorW = 1.10f;
        float sideLen = (d - 0.24f - doorW) * 0.5f;
        float sideCz = (d - 0.24f) * 0.5f - sideLen * 0.5f;
        MakeBox(root, "Booth_Glass_WN", new Vector3(-hx + 0.05f, h * 0.5f, sideCz), new Vector3(0.05f, h - 0.24f, sideLen), matGlassUltra);
        MakeBox(root, "Booth_Glass_WS", new Vector3(-hx + 0.05f, h * 0.5f, -sideCz), new Vector3(0.05f, h - 0.24f, sideLen), matGlassUltra);
        MakeBox(root, "Booth_Door_Head", new Vector3(-hx + 0.05f, h - 0.30f, 0f), new Vector3(0.10f, 0.10f, doorW), matMetalMatteBlack, false);

        MakeBox(root, "Booth_Desk", new Vector3(0.55f, 0.76f, 0f), new Vector3(0.70f, 0.06f, 1.40f), matWoodScandiOak);
        MakeBox(root, "Booth_Light", new Vector3(0f, h - 0.16f, 0f), new Vector3(w - 0.60f, 0.04f, d - 0.60f), matLightCoolLED, false);
    }

    static void BuildInfoKiosk(Transform parent, Vector3 pos, float yaw)
    {
        var root = new GameObject("Info_Kiosk").transform;
        root.SetParent(parent, false);
        root.localPosition = pos;
        root.localRotation = Quaternion.Euler(0f, yaw, 0f);

        MakeBox(root, "Kiosk_Base", new Vector3(0f, 0.03f, 0f), new Vector3(0.70f, 0.06f, 0.60f), matMetalStainless);
        MakeBox(root, "Kiosk_Column", new Vector3(0f, 0.75f, 0f), new Vector3(0.34f, 1.44f, 0.26f), matMarbleWhite);

        var panel = MakeBox(root, "Kiosk_Panel", new Vector3(0f, 1.36f, 0.05f), new Vector3(0.62f, 0.86f, 0.09f), matMarbleWhite, false);
        panel.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);

        var screen = MakeBox(root, "Kiosk_Screen", new Vector3(0f, 1.36f, 0.11f), new Vector3(0.52f, 0.72f, 0.03f), matLightCoolLED, false);
        screen.transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
    }

    static void BuildVendingMachines(Transform parent, float baseY)
    {
        var root = new GameObject("1F_Vending_Machines").transform;
        root.SetParent(parent, false);

        BuildVendingBank(root, new Vector3(-4.0f, baseY, -15.35f), Quaternion.identity, "饮品");
        BuildVendingBank(root, new Vector3(4.0f, baseY, -15.35f), Quaternion.identity, "零食");
    }

    // A single vending machine, glass facing local +Z.
    static void BuildVendingBank(Transform parent, Vector3 origin, Quaternion rot, string kind)
    {
        var root = new GameObject("Vending_" + kind).transform;
        root.SetParent(parent, false);
        root.localPosition = origin;
        root.localRotation = rot;

        const float ox = 0f;

        MakeBox(root, "Machine", new Vector3(ox, 0.95f, 0), new Vector3(1.25f, 1.9f, 0.85f), matMetalTitanium);
        MakeBox(root, "Glass", new Vector3(ox, 1.05f, 0.44f), new Vector3(1.05f, 1.25f, 0.04f), matGlassUltra, false);
        MakeBox(root, "Glow", new Vector3(ox, 1.05f, 0.40f), new Vector3(0.95f, 1.15f, 0.02f), matLightCoolLED, false);

        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                MakeBox(root, "Item_" + r + "_" + c,
                    new Vector3(ox - 0.34f + c * 0.34f, 0.62f + r * 0.36f, 0.30f),
                    new Vector3(0.20f, 0.26f, 0.10f),
                    (r + c) % 2 == 0 ? matBook2 : matBook1, false);
            }
        }

        MakeBox(root, "Pay", new Vector3(ox, 1.55f, 0.45f), new Vector3(0.22f, 0.28f, 0.04f), matSignBlackBox, false);
        MakeBox(root, "Slot", new Vector3(ox, 0.42f, 0.44f), new Vector3(0.55f, 0.20f, 0.04f), matMetalMatteBlack, false);

        var sign = MakeBox(root, "Sign", new Vector3(ox, 1.82f, 0.46f), new Vector3(1.0f, 0.22f, 0.03f), matSignBlackBox, false);
        MakeText(sign.transform, "自动售卖机 · " + kind, new Vector3(0, 0, 0.03f), Quaternion.identity, 20, 0.020f, Color.white);
    }

    // Central campus sand-table model: a compact lit relief of the campus.
    // Kept deliberately small (6.4 x 4.6m footprint) so it reads as a model in the
    // middle of the lobby without dominating it.
    static void BuildCampusSandTable(Transform parent, float baseY)
    {
        var root = new GameObject("1F_Campus_SandTable").transform;
        root.SetParent(parent, false);

        Vector3 c = new Vector3(0, baseY, 1.5f);

        // Plinth
        MakeBox(root, "Plinth_Base", c + new Vector3(0, 0.15f, 0), new Vector3(6.8f, 0.3f, 5.0f), matStoneSlate);
        MakeBox(root, "Plinth_Body", c + new Vector3(0, 0.62f, 0), new Vector3(6.5f, 0.64f, 4.7f), matMarbleWhite);
        MakeBox(root, "Plinth_Trim", c + new Vector3(0, 0.96f, 0), new Vector3(6.7f, 0.08f, 4.9f), matMetalBrass, false);

        // Model terrain and water
        MakeBox(root, "Terrain", c + new Vector3(0, 1.02f, 0), new Vector3(6.1f, 0.10f, 4.3f), matPlantFoliage, false);
        MakeBox(root, "Lake", c + new Vector3(-1.5f, 1.09f, -0.6f), new Vector3(2.1f, 0.04f, 1.6f), matChairSlateBlue, false);

        // Road grid
        for (int i = -2; i <= 2; i++)
            MakeBox(root, "Road_X_" + i, c + new Vector3(0, 1.10f, i * 0.92f), new Vector3(5.9f, 0.02f, 0.17f), matChairCream, false);
        for (int i = -2; i <= 2; i++)
            MakeBox(root, "Road_Z_" + i, c + new Vector3(i * 1.28f, 1.10f, 0), new Vector3(0.17f, 0.02f, 4.1f), matChairCream, false);

        // Miniature buildings laid out like the real campus
        MakeMiniBuilding(root, c, -1.95f, -0.85f, 0.67f, 0.55f, 0.52f, "A楼");
        MakeMiniBuilding(root, c, -2.68f, -0.12f, 0.55f, 0.46f, 0.61f, "B楼");
        MakeMiniBuilding(root, c, -1.22f, -0.12f, 0.55f, 0.46f, 0.61f, "C楼");

        MakeCylinder(root, "Fountain", c + new Vector3(-1.95f, 1.14f, -0.18f), new Vector3(0.30f, 0.06f, 0.30f), matChairSlateBlue, false);
        MakeCylinder(root, "Fountain_Jet", c + new Vector3(-1.95f, 1.26f, -0.18f), new Vector3(0.04f, 0.18f, 0.04f), matGlassUltra, false);

        MakeMiniBuilding(root, c, 1.58f, 0.37f, 0.92f, 0.64f, 0.67f, "图书馆");
        MakeBox(root, "Marker_Library", c + new Vector3(1.58f, 1.62f, 0.37f), new Vector3(0.3f, 0.3f, 0.3f), matMetalBrass, false);

        MakeMiniBuilding(root, c, 2.07f, -1.22f, 0.73f, 0.49f, 0.55f, "食堂");
        MakeMiniBuilding(root, c, 0.49f, 1.34f, 0.97f, 0.46f, 0.61f, "体育馆");

        for (int i = 0; i < 4; i++)
            MakeMiniBuilding(root, c, -1.83f + i * 0.98f, 1.46f, 0.52f, 0.61f, 0.30f, "公寓" + (i + 1));

        // Greenery dots, kept out of the lake
        var rng = new System.Random(20240);
        for (int i = 0; i < 26; i++)
        {
            float x = -2.8f + (float)rng.NextDouble() * 5.6f;
            float z = -1.9f + (float)rng.NextDouble() * 3.8f;
            if (Mathf.Abs(x + 1.5f) < 1.2f && Mathf.Abs(z + 0.6f) < 1.0f) continue;
            MakeSphere(root, "Tree_" + i, c + new Vector3(x, 1.17f, z), new Vector3(0.15f, 0.21f, 0.15f), matPlantFoliage, false);
        }

        // Glass protective case
        MakeBox(root, "Case_Glass_W", c + new Vector3(-3.1f, 1.75f, 0), new Vector3(0.06f, 1.3f, 4.3f), matGlassUltra, false);
        MakeBox(root, "Case_Glass_E", c + new Vector3(3.1f, 1.75f, 0), new Vector3(0.06f, 1.3f, 4.3f), matGlassUltra, false);
        MakeBox(root, "Case_Glass_N", c + new Vector3(0, 1.75f, 2.18f), new Vector3(6.2f, 1.3f, 0.06f), matGlassUltra, false);
        MakeBox(root, "Case_Glass_S", c + new Vector3(0, 1.75f, -2.18f), new Vector3(6.2f, 1.3f, 0.06f), matGlassUltra, false);
        MakeBox(root, "Case_Top", c + new Vector3(0, 2.42f, 0), new Vector3(6.3f, 0.06f, 4.5f), matGlassUltra, false);
        foreach (var sx in new[] { -3.1f, 3.1f })
            foreach (var sz in new[] { -2.18f, 2.18f })
                MakeBox(root, "Case_Post", c + new Vector3(sx, 1.75f, sz), new Vector3(0.08f, 1.3f, 0.08f), matMetalBrass, false);

        var title = MakeBox(root, "SandTable_Title", c + new Vector3(0, 0.62f, -2.32f), new Vector3(4.2f, 0.34f, 0.05f), matSignBlackBox, false);
        MakeText(title.transform, "大连理工大学 · 开发区校区 总体规划沙盘", new Vector3(0, 0, -0.04f), Quaternion.identity, 22, 0.024f, Color.white);

        // ---- Air wall around the model ----
        // The plinth is only ~0.95m high, so the player could jump onto it and then walk
        // straight through the glass case and the model. This invisible box stands just
        // outside the plinth and is taller than the maximum jump (about 1.2m), so the
        // display can be looked at but never entered.
        MakeAirWall(root, "SandTable_AirWall",
            c + new Vector3(0, 1.35f, 0), new Vector3(7.1f, 2.7f, 5.3f));
    }

    static void MakeMiniBuilding(Transform parent, Vector3 center, float x, float z, float sx, float sy, float sz, string label)
    {
        MakeBox(parent, "Mini_" + label, center + new Vector3(x, 1.09f + sy * 0.5f, z), new Vector3(sx, sy, sz), matChairCream, false);
        MakeBox(parent, "Mini_Roof_" + label, center + new Vector3(x, 1.09f + sy + 0.03f, z), new Vector3(sx * 1.06f, 0.06f, sz * 1.06f), matWallDarkCharcoal, false);
    }

    // A fully enclosed restroom block: four walls, one door, an internal partition
    // separating the wash area from the cubicles, basins, mirrors and stalls.
    //
    // Previously this built only three walls (the east side was missing) and put the
    // partition straight across the entrance, so the room read as an open set of
    // screens. It is now sealed on all four sides.
    static void BuildRestrooms(Transform parent, float baseY, string prefix, Vector2 center, float width, float depth)
    {
        var root = new GameObject(prefix + "_Restrooms").transform;
        root.SetParent(parent, false);

        float cx = center.x, cz = center.y;
        float x0 = cx - width * 0.5f, x1 = cx + width * 0.5f;
        float z0 = cz - depth * 0.5f, z1 = cz + depth * 0.5f;

        // Where the block sits against the building shell, snap it onto the inner face
        // so there is no hairline slit between the room and the outer wall.
        if (z1 > HalfD - 2.5f) z1 = HalfD - 0.15f;
        if (x1 > HalfW - 2.5f) x1 = HalfW - 0.15f;

        const float wallT = 0.18f;
        const float doorW = 1.6f;
        const float doorH = 2.2f;
        const float H = 2.7f;
        float midY = baseY + H * 0.5f;

        // ---- Four enclosing walls ----
        // South wall, split around the entrance door.
        MakeBox(root, "Wall_South_W", new Vector3((x0 + (cx - doorW * 0.5f)) * 0.5f, midY, z0),
            new Vector3((cx - doorW * 0.5f) - x0, H, wallT), matMarbleWhite);
        MakeBox(root, "Wall_South_E", new Vector3((x1 + (cx + doorW * 0.5f)) * 0.5f, midY, z0),
            new Vector3(x1 - (cx + doorW * 0.5f), H, wallT), matMarbleWhite);
        MakeBox(root, "Wall_South_Lintel", new Vector3(cx, baseY + doorH + (H - doorH) * 0.5f, z0),
            new Vector3(doorW, H - doorH, wallT), matMarbleWhite);

        // North, west and east. The east wall was the missing one.
        MakeBox(root, "Wall_North", new Vector3(cx, midY, z1), new Vector3(x1 - x0, H, wallT), matMarbleWhite);
        MakeBox(root, "Wall_West", new Vector3(x0, midY, (z0 + z1) * 0.5f), new Vector3(wallT, H, z1 - z0), matMarbleWhite);
        MakeBox(root, "Wall_East", new Vector3(x1, midY, (z0 + z1) * 0.5f), new Vector3(wallT, H, z1 - z0), matMarbleWhite);

        // ---- Internal partition: wash area (south) | cubicles (north) ----
        // The passage sits on the door axis so you can walk straight in.
        float partZ = z0 + 3.0f;
        const float passW = 1.4f;
        MakeBox(root, "Partition_W", new Vector3((x0 + (cx - passW * 0.5f)) * 0.5f, midY, partZ),
            new Vector3((cx - passW * 0.5f) - x0, H, 0.12f), matMarbleWhite);
        MakeBox(root, "Partition_E", new Vector3((x1 + (cx + passW * 0.5f)) * 0.5f, midY, partZ),
            new Vector3(x1 - (cx + passW * 0.5f), H, 0.12f), matMarbleWhite);
        MakeBox(root, "Partition_Lintel", new Vector3(cx, baseY + doorH + (H - doorH) * 0.5f, partZ),
            new Vector3(passW, H - doorH, 0.12f), matMarbleWhite);

        // ---- Wet floor inlay ----
        MakeBox(root, "Wet_Floor", new Vector3(cx, baseY + 0.012f, (z0 + z1) * 0.5f),
            new Vector3(x1 - x0 - 0.3f, 0.02f, z1 - z0 - 0.3f), matFloorDarkBasalt, false);

        // ---- Basins and mirrors along the west wall of the wash area ----
        for (int i = 0; i < 2; i++)
        {
            float bz = z0 + 1.05f + i * 1.15f;
            MakeBox(root, "Basin_" + i, new Vector3(x0 + 0.55f, baseY + 0.82f, bz), new Vector3(0.7f, 0.16f, 0.9f), matMarbleWhite);
            MakeBox(root, "Basin_Pedestal_" + i, new Vector3(x0 + 0.55f, baseY + 0.4f, bz), new Vector3(0.5f, 0.8f, 0.62f), matMarbleWhite, false);
            MakeBox(root, "Mirror_" + i, new Vector3(x0 + 0.14f, baseY + 1.75f, bz), new Vector3(0.05f, 1.2f, 0.9f), matMetalStainless, false);
            MakeBox(root, "Tap_" + i, new Vector3(x0 + 0.45f, baseY + 1.0f, bz), new Vector3(0.18f, 0.05f, 0.05f), matMetalStainless, false);
        }

        // ---- Cubicles along the north wall ----
        // Dividers run south from the north wall; each stall opens south with a door
        // panel that leaves a gap so it is walkable.
        int stalls = 3;
        float sw = (x1 - x0) / stalls;
        float cubicleDepth = Mathf.Min(1.9f, (z1 - partZ) - 0.6f);
        float frontZ = z1 - cubicleDepth;

        for (int i = 1; i < stalls; i++)
            MakeBox(root, "Stall_Divider_" + i, new Vector3(x0 + i * sw, midY, z1 - cubicleDepth * 0.5f),
                new Vector3(0.10f, H, cubicleDepth), matMarbleWhite);

        for (int i = 0; i < stalls; i++)
        {
            float sc = x0 + (i + 0.5f) * sw;
            // Front panel offset to one side, leaving an 0.8m entrance to the stall.
            float panelW = sw - 0.8f;
            MakeBox(root, "Stall_Front_" + i, new Vector3(sc + 0.4f, midY, frontZ),
                new Vector3(panelW, H, 0.08f), matMarbleWhite);
            // A frosted leaf standing open against the panel, purely visual.
            MakeBox(root, "Stall_Leaf_" + i, new Vector3(sc - 0.1f, baseY + 1.05f, frontZ - 0.10f),
                new Vector3(0.68f, 1.9f, 0.04f), matGlassFrosted, false);
        }

        // ---- Sign over the entrance, outside the room ----
        var sign = MakeBox(root, "Sign_" + prefix + "_Restroom", new Vector3(cx, baseY + 3.05f, z0 - 0.14f),
            new Vector3(2.0f, 0.4f, 0.05f), matSignBlackBox, false);
        MakeText(sign.transform, "卫生间 RESTROOM", new Vector3(0, 0, -0.04f), Quaternion.identity, 22, 0.028f, Color.white);
    }

    // 校史展览馆: a single wall-mounted texture panel with an [E] interaction.
    // Per the design brief there is NO physical exhibit geometry here — no display
    // cases, no plinths, no freestanding walls. Just the panel on the east wall.
    static void BuildHistoryExhibition(Transform parent, float baseY)
    {
        var root = new GameObject("1F_History_Exhibition").transform;
        root.SetParent(parent, false);

        // One large panel mounted flat on the east wall (X = 19.85 inner face),
        // facing west into the hall so the player reads it from the lobby.
        MakeArtPanel(root, "Panel_History", new Vector3(19.55f, baseY + 2.0f, 0.0f),
            new Vector3(5.0f, 2.8f, 0.05f), Quaternion.Euler(0, -90, 0),
            matHistoryPic, matWallAccentGreen, "校史展览馆 · HISTORY GALLERY");

        // Its own light so the panel reads clearly.
        MakeBox(root, "Panel_Spot", new Vector3(19.0f, baseY + 3.85f, 0.0f), new Vector3(0.3f, 0.05f, 5.2f), matLightWarmLED, false);
    }

    static void BuildLobbyReception(Transform parent, float baseY)
    {
        var root = new GameObject("1F_Reception").transform;
        root.SetParent(parent, false);

        Vector3 c = new Vector3(-10.5f, baseY, -5.0f);
        MakeBox(root, "Desk_Body", c + new Vector3(0, 0.55f, 0), new Vector3(4.6f, 1.1f, 0.9f), matWoodDarkWalnut);
        MakeBox(root, "Desk_Top", c + new Vector3(0, 1.13f, 0), new Vector3(4.8f, 0.06f, 1.05f), matMarbleWhite, false);
        MakeBox(root, "Desk_Base", c + new Vector3(0, 0.03f, 0), new Vector3(4.6f, 0.06f, 0.9f), matMetalTitanium, false);

        for (int i = 0; i < 2; i++)
        {
            float ox = -1.1f + i * 2.2f;
            MakeBox(root, "Monitor_" + i, c + new Vector3(ox, 1.42f, 0.1f), new Vector3(0.62f, 0.42f, 0.05f), matSignBlackBox, false);
            MakeBox(root, "Monitor_Screen_" + i, c + new Vector3(ox, 1.42f, 0.14f), new Vector3(0.56f, 0.36f, 0.02f), matLightCoolLED, false);
            MakeBox(root, "Monitor_Stand_" + i, c + new Vector3(ox, 1.20f, 0.1f), new Vector3(0.08f, 0.16f, 0.08f), matMetalMatteBlack, false);

            Vector3 s = c + new Vector3(ox, 0, 1.2f);
            MakeBox(root, "Chair_Seat_" + i, s + new Vector3(0, 0.45f, 0), new Vector3(0.48f, 0.06f, 0.48f), matChairSlateBlue);
            MakeBox(root, "Chair_Back_" + i, s + new Vector3(0, 0.78f, 0.22f), new Vector3(0.46f, 0.55f, 0.05f), matChairSlateBlue, false);
            MakeBox(root, "Chair_Post_" + i, s + new Vector3(0, 0.22f, 0), new Vector3(0.08f, 0.44f, 0.08f), matMetalMatteBlack, false);
        }

        var sign = MakeBox(root, "Sign_Reception", c + new Vector3(0, 2.9f, 0), new Vector3(3.6f, 0.5f, 0.06f), matSignBlackBox, false);
        MakeText(sign.transform, "总服务台 · INFORMATION", new Vector3(0, 0, -0.04f), Quaternion.identity, 24, 0.032f, Color.white);

        // Book return chute beside the desk
        MakeBox(root, "Return_Chute", c + new Vector3(3.2f, 0.5f, 0), new Vector3(0.9f, 1.0f, 0.8f), matMetalTitanium);
        MakeBox(root, "Return_Slot", c + new Vector3(3.2f, 1.02f, 0), new Vector3(0.6f, 0.06f, 0.3f), matSignBlackBox, false);
    }

    static void BuildLibraryPlanPanels(Transform parent, float baseY)
    {
        var root = new GameObject("1F_Plan_Panels").transform;
        root.SetParent(parent, false);

        MakeArtPanel(root, "Panel_FloorPlan", new Vector3(-19.7f, baseY + 1.85f, 0.5f), new Vector3(4.2f, 2.2f, 0.04f), Quaternion.Euler(0, 90, 0), matPlanPic, matWallAccentGreen, "馆舍平面导览");
        MakeArtPanel(root, "Panel_Opening_Hours", new Vector3(-9.0f, baseY + 1.85f, -15.7f), new Vector3(3.4f, 1.9f, 0.04f), Quaternion.Euler(0, 0, 0), matPosterPic, matWallAccentGreen, "开馆时间 · 借阅规则");
    }

    static void BuildLobbyGreenery(Transform parent, float baseY)
    {
        var root = new GameObject("1F_Greenery").transform;
        root.SetParent(parent, false);

        // The two tall mid-lobby planters used to stand square in the north approach.
        // The main planting is now the south-east bed, so only slim wall-side accents
        // remain here and every walking line stays open.
        BuildPlanter(root, new Vector3(-18.9f, baseY, 2.1f), 3.0f, 0);
        BuildPlanter(root, new Vector3(-18.9f, baseY, -9.4f), 3.4f, 0);
        BuildPotPlant(root, new Vector3(-13.4f, baseY, -10.6f), 1.3f);
        BuildPotPlant(root, new Vector3(-3.2f, baseY, 14.8f), 1.5f);
        BuildPotPlant(root, new Vector3(3.0f, baseY, 14.8f), 1.5f);
    }

    // ---------------------------------------------------------------- shared furniture

    static void BuildPlanter(Transform parent, Vector3 center, float length, float rotY)
    {
        var root = new GameObject("Planter").transform;
        root.SetParent(parent, false);
        root.localPosition = center;
        root.localRotation = Quaternion.Euler(0, rotY, 0);

        MakeBox(root, "Trough", new Vector3(0, 0.32f, 0), new Vector3(0.6f, 0.64f, length), matPlantPot);
        MakeBox(root, "Trough_Cap", new Vector3(0, 0.66f, 0), new Vector3(0.66f, 0.04f, length + 0.06f), matWoodScandiOak, false);

        int segs = Mathf.Max(3, Mathf.RoundToInt(length / 0.75f));
        for (int i = 0; i < segs; i++)
        {
            float t = segs == 1 ? 0f : (i / (float)(segs - 1) - 0.5f);
            float z = t * (length - 0.7f);
            var leaf = MakeBox(root, "Foliage_" + i, new Vector3(0, 0.92f, z), new Vector3(0.5f, 0.5f, 0.58f), matPlantFoliage, false);
            leaf.transform.localRotation = Quaternion.Euler(0, i * 37f, 0);
            MakeBox(root, "Foliage_Top_" + i, new Vector3(0, 1.2f, z), new Vector3(0.34f, 0.28f, 0.4f), matPlantFoliage, false);
        }
    }

    // Potted plant with a real trunk and branch structure. Foliage is built from several
    // flattened spheres clustered along branch tips, so the plant reads as a plant rather
    // than a pile of green balls. Scale 1.0 = about 1.9 m tall.
    static void BuildPotPlant(Transform parent, Vector3 center, float scale)
    {
        var root = new GameObject("PotPlant").transform;
        root.SetParent(parent, false);
        root.localPosition = center;

        float potH = 0.42f * scale;
        float potW = 0.72f * scale;

        // Planter: tapered pot with a lighter rim, using the shared stone/wood palette.
        MakeCylinder(root, "Pot", new Vector3(0, potH * 0.5f, 0), new Vector3(potW, potH * 0.5f, potW), matPlantPot);
        MakeCylinder(root, "Pot_Rim", new Vector3(0, potH - 0.02f * scale, 0),
            new Vector3(potW + 0.10f * scale, 0.025f * scale, potW + 0.10f * scale), matWoodDarkWalnut, false);
        MakeCylinder(root, "Pot_Soil", new Vector3(0, potH - 0.05f * scale, 0),
            new Vector3(potW - 0.10f * scale, 0.02f * scale, potW - 0.10f * scale), matPlantPot, false);

        // Trunk: main stem plus three primary branches built from tubes.
        Vector3 trunkTop = new Vector3(0, potH + 0.95f * scale, 0);
        InteriorGeometry.Tube(root, "Trunk", new Vector3(0, potH - 0.05f * scale, 0),
            trunkTop, 0.035f * scale, matWoodDarkWalnut);

        var rng = new System.Random(Mathf.RoundToInt(Mathf.Abs(center.x) * 31f + Mathf.Abs(center.z) * 17f) + 7);
        var tips = new List<Vector3> { trunkTop };

        int branchCount = 3;
        for (int i = 0; i < branchCount; i++)
        {
            float a = i * (Mathf.PI * 2f / branchCount) + (float)rng.NextDouble() * 0.8f;
            float reach = (0.34f + (float)rng.NextDouble() * 0.14f) * scale;
            float rise = (0.42f + (float)rng.NextDouble() * 0.16f) * scale;
            var mid = new Vector3(Mathf.Cos(a) * reach * 0.45f, potH + 0.55f * scale, Mathf.Sin(a) * reach * 0.45f);
            var tip = new Vector3(Mathf.Cos(a) * reach, potH + 0.55f * scale + rise, Mathf.Sin(a) * reach);

            InteriorGeometry.Tube(root, "Branch_" + i,
                new Vector3(Mathf.Cos(a) * 0.06f * scale, potH + 0.42f * scale, Mathf.Sin(a) * 0.06f * scale), mid,
                0.020f * scale, matWoodDarkWalnut);
            InteriorGeometry.Tube(root, "Branch_" + i + "_b", mid, tip, 0.014f * scale, matWoodDarkWalnut);
            tips.Add(tip);
        }

        // Foliage clusters sit on the branch tips: a core blob plus two smaller lobes, so
        // each cluster has silhouette instead of one flat sphere.
        foreach (var tip in tips)
        {
            float crown = 0.52f * scale;
            MakeSphere(root, "Leaf_Cluster", tip + new Vector3(0, crown * 0.28f, 0),
                new Vector3(crown * 2.1f, crown * 1.5f, crown * 2.1f), matPlantFoliage, false);

            for (int i = 0; i < 3; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = (0.45f + (float)rng.NextDouble() * 0.35f) * crown;
                float h = ((float)rng.NextDouble() - 0.4f) * crown * 0.9f;
                MakeSphere(root, "Leaf_Cluster_Lobe",
                    tip + new Vector3(Mathf.Cos(a) * d, crown * 0.30f + h, Mathf.Sin(a) * d),
                    new Vector3(crown * 1.5f, crown * 1.05f, crown * 1.5f), matShrubLight, false);
            }
        }

        // A few fallen leaves on the pot soil.
        for (int i = 0; i < 4; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float d = (float)rng.NextDouble() * potW * 0.35f;
            MakeSphere(root, "Soil_Leaf",
                new Vector3(Mathf.Cos(a) * d, potH - 0.03f * scale, Mathf.Sin(a) * d),
                new Vector3(0.10f * scale, 0.012f * scale, 0.10f * scale), matPlantFoliage, false);
        }
    }

    static void BuildSofa(Transform parent, Vector3 center, float rotY)
    {
        var root = new GameObject("Sofa").transform;
        root.SetParent(parent, false);
        root.localPosition = center;
        root.localRotation = Quaternion.Euler(0, rotY, 0);

        MakeBox(root, "Base", new Vector3(0, 0.22f, 0), new Vector3(2.0f, 0.44f, 0.85f), matSofaFabric);
        MakeBox(root, "Seat_Cushion", new Vector3(0, 0.47f, 0.05f), new Vector3(1.9f, 0.14f, 0.72f), matSofaFabric, false);
        MakeBox(root, "Back", new Vector3(0, 0.72f, -0.36f), new Vector3(2.0f, 0.6f, 0.14f), matSofaFabric, false);
        MakeBox(root, "Arm_W", new Vector3(-0.97f, 0.55f, 0), new Vector3(0.12f, 0.3f, 0.85f), matSofaFabric, false);
        MakeBox(root, "Arm_E", new Vector3(0.97f, 0.55f, 0), new Vector3(0.12f, 0.3f, 0.85f), matSofaFabric, false);
        MakeBox(root, "Leg_W", new Vector3(-0.85f, 0.05f, 0), new Vector3(0.06f, 0.1f, 0.7f), matMetalMatteBlack, false);
        MakeBox(root, "Leg_E", new Vector3(0.85f, 0.05f, 0), new Vector3(0.06f, 0.1f, 0.7f), matMetalMatteBlack, false);
    }

    // A single chair, sized off the 1.8 m character: 0.45 m seat height, 0.46 m seat
    // width, backrest rising to 1.02 m. Four tapered metal legs and a slightly reclined
    // back read far better than a stack of cubes.
    // Chairs registered while the library is built become the reservable seat list.
    static readonly System.Collections.Generic.List<LibrarySeat> builtSeats = new System.Collections.Generic.List<LibrarySeat>();
    static int currentBuildFloor = 1;
    static string currentSeatArea = "A";

    static void BuildChair(Transform parent, Vector3 localPos, float rotY, Material mat)
    {
        var ch = new GameObject("Chair").transform;
        ch.SetParent(parent, false);
        ch.localPosition = localPos;
        ch.localRotation = Quaternion.Euler(0, rotY, 0);

        // Record the seat: world position is the chair footprint, and the body faces the
        // same way the chair does (chairs are built turned toward their table).
        Vector3 world = ch.TransformPoint(Vector3.zero);
        Quaternion facing = ch.rotation;
        Vector3 back = facing * Vector3.forward;   // chair back direction
        builtSeats.Add(new LibrarySeat
        {
            floor = currentBuildFloor,
            area = currentSeatArea,
            areaName = AreaName(currentBuildFloor, currentSeatArea),
            sitPos = new Vector3(world.x, world.y, world.z),
            standPos = new Vector3(world.x - back.x * 0.75f, world.y, world.z - back.z * 0.75f),
            facing = facing
        });

        const float seatH = 0.45f, seatW = 0.46f, seatD = 0.44f;

        // Bevelled upholstered seat on a dark frame, two raked back panels and round legs.
        InteriorGeometry.RoundedBox(ch, "Seat", new Vector3(0, seatH, 0),
            new Vector3(seatW, 0.075f, seatD), 0.026f, mat, true);
        InteriorGeometry.RoundedBox(ch, "Seat_Frame", new Vector3(0, seatH - 0.058f, 0),
            new Vector3(seatW - 0.05f, 0.035f, seatD - 0.05f), 0.012f, matMetalMatteBlack, false);

        var backLow = InteriorGeometry.RoundedBox(ch, "Back_Low",
            new Vector3(0, seatH + 0.15f, seatD * 0.44f),
            new Vector3(seatW * 0.96f, 0.22f, 0.055f), 0.018f, mat, false);
        backLow.transform.localRotation = Quaternion.Euler(-5f, 0f, 0f);

        var backHigh = InteriorGeometry.RoundedBox(ch, "Back_High",
            new Vector3(0, seatH + 0.33f, seatD * 0.47f),
            new Vector3(seatW * 0.90f, 0.16f, 0.05f), 0.016f, mat, false);
        backHigh.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);

        foreach (float sx in new[] { -1f, 1f })
        {
            InteriorGeometry.Tube(ch, "Leg_F", new Vector3(sx * seatW * 0.40f, 0.005f, seatD * 0.36f),
                new Vector3(sx * seatW * 0.40f, seatH, seatD * 0.36f), 0.018f, matMetalMatteBlack);
            InteriorGeometry.Tube(ch, "Leg_B", new Vector3(sx * seatW * 0.40f, 0.005f, -seatD * 0.36f),
                new Vector3(sx * seatW * 0.40f, seatH, -seatD * 0.36f), 0.018f, matMetalMatteBlack);
            InteriorGeometry.Tube(ch, "Back_Post", new Vector3(sx * seatW * 0.42f, seatH - 0.02f, seatD * 0.40f),
                new Vector3(sx * seatW * 0.42f, seatH + 0.40f, seatD * 0.44f), 0.019f, matMetalMatteBlack);
        }
        InteriorGeometry.Tube(ch, "Stretcher", new Vector3(-seatW * 0.40f, 0.17f, 0f),
            new Vector3(seatW * 0.40f, 0.17f, 0f), 0.013f, matMetalMatteBlack);
    }

}
