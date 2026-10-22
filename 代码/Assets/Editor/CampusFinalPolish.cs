using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Small, repeatable repairs to authored assets. No scene or campus regeneration.
public static class CampusFinalPolish
{
    const string Folder = "Assets/FinalCampusPolish";
    static readonly StringBuilder report = new StringBuilder();
    public static void ApplyAndBuild()
    {
        report.Clear(); Directory.CreateDirectory(Folder); Directory.CreateDirectory(CampusFinalAudit.Output); AssetDatabase.Refresh();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var game = Object.FindFirstObjectByType<CampusGame>();
        var data = JsonUtility.FromJson<CampusData>(game.mapData.text);
        var courtyard = data.roads.First(r => r.name == "北庭院横轴");
        courtyard.points[0].x = 225.2f; // Start at the real third-dorm entrance, beyond its projecting facade.
        AlignDoorRoutes(data, game.GetComponent<BuildingInteriorTravel>());
        string mapPath = AssetDatabase.GetAssetPath(game.mapData);
        File.WriteAllText(mapPath, JsonUtility.ToJson(data,true)); AssetDatabase.ImportAsset(mapPath);
        const string planPath = "Assets/CampusPlanRevision/CampusMap.json";
        if (mapPath != planPath)
        {
            var plan = JsonUtility.FromJson<CampusData>(File.ReadAllText(planPath));
            plan.roads.First(r => r.name == "北庭院横轴").points[0].x = 225.2f;
            AlignDoorRoutes(plan, game.GetComponent<BuildingInteriorTravel>());
            File.WriteAllText(planPath,JsonUtility.ToJson(plan,true));AssetDatabase.ImportAsset(planPath);
        }
        report.AppendLine("Aligned north residence courtyard route start with the actual third-dorm approach (225.2,-103), outside the facade collision.");
        RepairMirroredColliders(game);
        MoveJunctionLamp(game);
        ClearLowVegetation(game, data);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled && s.path != scene.path))
        {
            var inside = EditorSceneManager.OpenScene(entry.path);
            int count = RefineSigns();
            int duplicates = RemoveDuplicateStairPosts();
            if (count > 0 || duplicates > 0) { EditorSceneManager.MarkSceneDirty(inside); EditorSceneManager.SaveScene(inside); }
            report.AppendLine(entry.path + ": " + count + " signs refined");
            if (duplicates > 0) report.AppendLine("Disabled " + duplicates + " exactly overlapping cafeteria stair-guard post renderers; one visible post and safety collisions retained at each position.");
        }
        AssetDatabase.SaveAssets();
        // D3D12Core.dll crashed during shutdown in two complete Windows traversal
        // runs. The same shaders run on D3D11, including glass, water and SMAA.
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
        report.AppendLine("Windows defaults to Direct3D 11 after reproducing D3D12Core.dll shutdown crashes on this machine.");
        File.WriteAllText(CampusFinalAudit.Output + "/repairs.txt", report.ToString());
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        Debug.Log("FINAL_POLISH_APPLIED\n" + report);
        CampusBuilder.Build();
        CampusFinalAudit.Audit();
    }
    static void AlignDoorRoutes(CampusData data, BuildingInteriorTravel travel)
    {
        var routes = new[] {
            ("二舍入口", "dormitory-2", true), ("一舍入口", "dormitory-1", true),
            ("四舍入口", "dormitory-4", true), ("五舍入口", "dormitory-5", true),
            ("六舍入口", "dormitory-6", true), ("七舍北门LG引道", "dormitory-7-north", false),
            ("七舍南门1F入口", "dormitory-7-south", false), ("食堂西正门广场", "cafeteria", false)
        };
        foreach (var pair in routes)
        {
            var road = data.roads.First(r => r.name == pair.Item1);
            var door = travel.entrances.First(e => e.EntranceId == pair.Item2);
            int index = pair.Item3 ? 0 : road.points.Length - 1;
            road.points[index].x = door.approachMapPosition.x; road.points[index].z = door.approachMapPosition.z;
        }
        report.AppendLine("Aligned eight residence/cafeteria route termini with their real E interaction approaches; paving up to each facade retained.");
    }
    static int RemoveDuplicateStairPosts()
    {
        int removed = 0;
        var groups = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
            .Where(f => f.name == "StairGuardPost" && f.sharedMesh && f.GetComponent<MeshRenderer>().enabled)
            .GroupBy(f => f.sharedMesh.GetInstanceID() + ":" + f.transform.localToWorldMatrix.ToString("F3") + ":" + f.GetComponent<MeshRenderer>().sharedMaterial.GetInstanceID());
        foreach (var group in groups) foreach (var f in group.Skip(1)) { f.GetComponent<MeshRenderer>().enabled = false; removed++; }
        return removed;
    }
    static void RepairMirroredColliders(CampusGame game)
    {
        var physics = GameObject.Find("Campus plan physical surfaces").transform;
        var boxes = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Where(c => c.transform.lossyScale.x < 0 || c.transform.lossyScale.y < 0 || c.transform.lossyScale.z < 0).ToArray();
        foreach (var c in boxes)
        {
            var old = c.bounds; var t = c.transform;
            Vector3 center = t.TransformPoint(c.center), x = t.TransformVector(Vector3.right), y = t.TransformVector(Vector3.up), z = t.TransformVector(Vector3.forward);
            var size = new Vector3(Mathf.Abs(c.size.x) * x.magnitude, Mathf.Abs(c.size.y) * y.magnitude, Mathf.Abs(c.size.z) * z.magnitude);
            t.SetParent(physics, false); t.position = center; t.rotation = Quaternion.LookRotation(z.normalized, y.normalized); t.localScale = Vector3.one;
            c.center = Vector3.zero; c.size = size; Physics.SyncTransforms();
            if (Vector3.Distance(old.center, c.bounds.center) > .002f || Vector3.Distance(old.size, c.bounds.size) > .003f) throw new Exception("Collision bounds changed: " + c.name);
        }
        report.AppendLine("Rebased " + boxes.Length + " reflected BoxColliders to positive metre scale; world bounds retained within 3 mm.");
    }
    static void MoveJunctionLamp(CampusGame game)
    {
        var old = new Vector3(213, CampusPlanTerrain.Height(213, 39), 39);
        var goal = new Vector3(214.2f, CampusPlanTerrain.Height(214.2f, 42), 42);
        var delta = goal - old;
        var lamp = GameObject.Find("Campus plan physical surfaces/Landscape lamp 80");
        if (!lamp) return;
        bool moved = Vector3.Distance(game.WorldToMap(lamp.transform.position), goal + Vector3.up * 2.25f) < .2f;
        if (!moved && Vector3.Distance(game.WorldToMap(lamp.transform.position), old + Vector3.up * 2.25f) > .2f) return;
        // The post, base, arm and diffuser are batched by material. Move only this
        // lamp's complete triangles, not the surrounding pavement or planting.
        var holder = GameObject.Find("校园环境/校园平面图修订 · 生活区与学苑桥").transform;
        int meshes = 0;
        foreach (var f in holder.GetComponentsInChildren<MeshFilter>())
        {
            if (!f.sharedMesh || !(f.name.StartsWith("Metal-chunk") || f.name.StartsWith("Light-chunk") || f.name == "Light")) continue;
            var source = f.sharedMesh; var vertices = source.vertices; bool changed = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                var q = game.WorldToMap(f.transform.TransformPoint(vertices[i]));
                if (q.x < 212.84f || q.x > 213.89f || q.z < 38.84f || q.z > 39.16f || q.y < old.y - .01f || q.y > old.y + 4.65f) continue;
                vertices[i] = f.transform.InverseTransformPoint(game.MapToWorld(q + delta)); changed = true;
            }
            if (!changed) continue;
            var copy = Object.Instantiate(source); copy.name = source.name + " · clear junction"; copy.vertices = vertices; copy.RecalculateBounds();
            f.sharedMesh = Save(copy, "junction-" + f.name); meshes++;
        }
        if (!moved) lamp.transform.position += game.MapToWorld(delta);
        report.AppendLine("Moved cafeteria south-junction lamp with its visible base, arm and lens: map (213,39) -> (214.2,42), outside both intersecting routes. Meshes=" + meshes);
    }
    static Mesh Save(Mesh mesh, string name)
    {
        string path = Folder + "/" + name + ".asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old) { EditorUtility.CopySerialized(mesh, old); Object.DestroyImmediate(mesh); return old; }
        AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    static void ClearLowVegetation(CampusGame game, CampusData data)
    {
        int removed = 0, changed = 0;
        var root = GameObject.Find("校园环境").transform;
        // Broad phase bounds avoid testing the entire road graph for every leaf.
        var roads = data.roads.Where(r => r.kind != "bridge").Select(r => new RoadArea(r)).ToArray();
        foreach (var f in root.GetComponentsInChildren<MeshFilter>())
        {
            var r = f.GetComponent<MeshRenderer>(); if (!r || !r.enabled || !f.sharedMesh || f.GetComponentInParent<LODGroup>()) continue;
            string name = f.name + " " + string.Join(" ", r.sharedMaterials.Where(m => m).Select(m => m.name));
            bool plants = new[] { "garden-plants", "GrassDetail", "ShrubLight", "Leaf-chunk", "grass-blade" }.Any(n => name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!plants) continue;
            var source = f.sharedMesh; var local = source.vertices; var points = local.Select(p => game.WorldToMap(f.transform.TransformPoint(p))).ToArray();
            var filtered = new List<int[]>(); int cut = 0;
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                var triangles = source.GetTriangles(sub); var keep = new List<int>(triangles.Length);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = points[triangles[i]]; var b = points[triangles[i + 1]]; var c = points[triangles[i + 2]]; var p = (a + b + c) / 3;
                    float ground = CampusPlanTerrain.Height(p.x, p.z);
                    bool bridge = p.x > 108 && p.x < 192 && p.z > 7.4f && p.z < 12.6f && p.y > 6.42f && p.y < 8.4f;
                    bool low = p.y > ground + .045f && Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < ground + 1.05f;
                    bool road = low && roads.Any(area => area.Contains(p));
                    if (bridge || road) cut++;
                    else { keep.Add(triangles[i]); keep.Add(triangles[i + 1]); keep.Add(triangles[i + 2]); }
                }
                filtered.Add(keep.ToArray());
            }
            if (cut == 0) continue;
            var copy = Object.Instantiate(source); copy.name = "Vegetation · pavement clearance";
            for (int sub = 0; sub < filtered.Count; sub++) copy.SetTriangles(filtered[sub], sub);
            copy.RecalculateBounds(); f.sharedMesh = Save(copy, "plant-clearance-" + changed++); removed += cut;
        }
        report.AppendLine("Removed " + removed + " low vegetation triangles intruding into paved route interiors / bridge walking headroom, in " + changed + " meshes. Tree canopies and roadside planting retained.");
    }
    sealed class RoadArea
    {
        readonly CampusRoad road; readonly float minX, maxX, minZ, maxZ, radius;
        public RoadArea(CampusRoad r) { road = r; radius = Mathf.Max(.3f, r.width / 2 - .18f); minX = r.points.Min(p => p.x) - radius; maxX = r.points.Max(p => p.x) + radius; minZ = r.points.Min(p => p.z) - radius; maxZ = r.points.Max(p => p.z) + radius; }
        public bool Contains(Vector3 p) => p.x >= minX && p.x <= maxX && p.z >= minZ && p.z <= maxZ && CampusPlanRoadNetwork.Distance(p.x, p.z, road) < radius;
    }
    static int RefineSigns()
    {
        int count = 0;
        foreach (var text in Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
        {
            // The older library signs used 18–28 px glyphs. Re-request a crisp
            // atlas while keeping existing physical size; minimum 10 cm caps.
            var interior = Object.FindFirstObjectByType<InteriorSceneRoot>();
            if (!interior) continue;
            bool librarySign = interior && interior.buildingId == "library" && text.name.StartsWith("Text_");
            if (text.fontSize <= 0 || (text.fontSize > 32 && !librarySign) || !text.transform.IsChildOf(interior.transform)) continue;
            float size = text.characterSize * text.fontSize / 96f;
            text.fontSize = 96; text.characterSize = Mathf.Max(size, .0094f);
            var parent = text.transform.parent; var plate = parent ? parent.Find("Caption") : null;
            if (plate && text.name.StartsWith("Text_"))
            {
                // Captions previously sat inside the metal plate because a
                // centimetre offset was multiplied by the plate's scaled Z.
                text.transform.position = plate.position + parent.forward * (Mathf.Abs(plate.lossyScale.z) * .5f + .005f);
                text.transform.rotation = parent.rotation * Quaternion.Euler(0, 180, 0);
                text.characterSize = .013f;
            }
            else if (parent)
            {
                var facing = -text.transform.forward;
                var board = parent.GetComponentsInChildren<MeshRenderer>(false)
                    .Where(r => !r.GetComponent<TextMesh>() && r.transform.parent == parent &&
                        new[] { "Sign", "Label", "Indicator", "Directory" }.Any(n => r.name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                    .Where(r => { var b = r.bounds; b.Expand(.18f); return b.Contains(text.transform.position); })
                    .OrderBy(r => Vector3.Distance(r.bounds.center, text.transform.position)).FirstOrDefault();
                if (board)
                {
                    var b = board.bounds; var a = new Vector3(Mathf.Abs(facing.x), Mathf.Abs(facing.y), Mathf.Abs(facing.z));
                    float depth = Vector3.Dot(a, b.extents);
                    if (depth < .18f)
                    {
                        float offset = Vector3.Dot(text.transform.position - b.center, facing);
                        text.transform.position += facing * (depth + .006f - offset);
                    }
                }
            }
            if (!text.GetComponent<InteriorWorldLabel>()) text.gameObject.AddComponent<InteriorWorldLabel>();
            count++;
        }
        return count;
    }
}
