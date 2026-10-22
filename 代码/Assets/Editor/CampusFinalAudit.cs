using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Inspect authored scenes in place; never call the original campus generators.
public static class CampusFinalAudit
{
    public const string Output = "Builds/Windows/QA/FinalAudit";
    static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
    static IEnumerable<GameObject> Objects(UnityEngine.SceneManagement.Scene s) => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);
    public static void Audit()
    {
        Directory.CreateDirectory(Output);
        var report = new StringBuilder(); var labels = new StringBuilder(); var props = new StringBuilder();
        foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenScene(entry.path); var objects = Objects(scene).ToArray();
            report.AppendLine("SCENE " + entry.path + " | objects=" + objects.Length);
            foreach (var root in scene.GetRootGameObjects()) report.AppendLine(" ROOT " + root.name);
            foreach (var go in objects)
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0) report.AppendLine("MISSING_SCRIPT " + Path(go.transform) + " " + missing);
                if (!go.activeInHierarchy) continue;
                var r = go.GetComponent<MeshRenderer>(); var f = go.GetComponent<MeshFilter>();
                if (r && r.enabled && f && !f.sharedMesh) report.AppendLine("MISSING_MESH " + Path(go.transform));
                if (r && r.enabled && !go.GetComponent<TextMesh>() && r.sharedMaterials.Any(m => !m || !m.shader || m.shader.name.Contains("InternalError"))) report.AppendLine("MISSING_MATERIAL " + Path(go.transform));
                var text = go.GetComponent<TextMesh>();
                if (text && r && r.enabled)
                {
                    labels.AppendLine(entry.path + " | " + Path(go.transform) + " | " + text.text.Replace("\n", " / ") + " | at=" + go.transform.position.ToString("F2") + " scale=" + go.transform.lossyScale.ToString("F3") + " char=" + text.characterSize + " px=" + text.fontSize + " bounds=" + r.bounds.size.ToString("F3"));
                    if (r.bounds.size.magnitude > 12) report.AppendLine("LARGE_LABEL " + Path(go.transform) + " " + r.bounds.size.ToString("F2"));
                }
            }
            var boxes = objects.Where(g => g.activeInHierarchy).SelectMany(g => g.GetComponents<BoxCollider>()).Where(c => c.enabled).ToArray();
            var activeMeshes = objects.Where(g => g.activeInHierarchy).Select(g => g.GetComponent<MeshFilter>()).Where(f => f && f.sharedMesh && f.GetComponent<MeshRenderer>() && f.GetComponent<MeshRenderer>().enabled);
            foreach (var duplicate in activeMeshes.GroupBy(f => f.sharedMesh.GetInstanceID() + ":" + f.transform.localToWorldMatrix.ToString("F3") + ":" + string.Join(",",f.GetComponent<MeshRenderer>().sharedMaterials.Select(m=>m?m.GetInstanceID().ToString():"null"))).Where(g => g.Count() > 1))
                report.AppendLine("DUPLICATE_GEOMETRY " + string.Join(" | ",duplicate.Select(f=>Path(f.transform))));
            var negative = boxes.Where(c => c.transform.lossyScale.x < 0 || c.transform.lossyScale.y < 0 || c.transform.lossyScale.z < 0 || c.size.x < 0 || c.size.y < 0 || c.size.z < 0).ToArray();
            report.AppendLine("COLLIDERS " + boxes.Length + "; negative scale=" + negative.Length);
            foreach (var group in negative.GroupBy(c => c.transform.parent ? Path(c.transform.parent) : "root")) report.AppendLine(" NEGATIVE " + group.Key + " count=" + group.Count());
            var game = Object.FindFirstObjectByType<CampusGame>();
            if (game)
            {
                var data = JsonUtility.FromJson<CampusData>(game.mapData.text);
                var root = GameObject.Find("校园环境").transform;
                foreach (Transform child in root)
                {
                    var rs = child.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
                    var bounds = new Bounds(child.position, Vector3.zero); foreach (var r in rs) bounds.Encapsulate(r.bounds);
                    report.AppendLine(" GROUP " + child.name + " renderers=" + rs.Length + " centre=" + game.WorldToMap(bounds.center).ToString("F2") + " size=" + bounds.size.ToString("F2"));
                }
                foreach (var lod in root.GetComponentsInChildren<LODGroup>().Where(l => l.gameObject.activeInHierarchy))
                {
                    var at = game.WorldToMap(lod.transform.position); var road = data.roads.Where(r => r.kind != "bridge").OrderBy(r => CampusPlanRoadNetwork.Distance(at.x, at.z, r) - r.width / 2).First();
                    float distance = CampusPlanRoadNetwork.Distance(at.x, at.z, road) - road.width / 2;
                    bool footprint = data.features.Any(b => b.kind == "building" && CampusCollision.Inside(at.x, at.z, b.points));
                    if (distance < .3f || footprint || Mathf.Abs(at.y - CampusPlanTerrain.Height(at.x, at.z)) > .3f)
                        props.AppendLine("TREE " + Path(lod.transform) + " at=" + at.ToString("F2") + " road=" + road.name + " shoulder=" + distance.ToString("F2") + " building=" + footprint + " groundDelta=" + (at.y - CampusPlanTerrain.Height(at.x, at.z)).ToString("F2"));
                }
                foreach (var c in boxes.Where(c => c.name.IndexOf("lamp", StringComparison.OrdinalIgnoreCase) >= 0 || c.name.Contains("Tree trunk")))
                {
                    var at = game.WorldToMap(c.bounds.center);var road = data.roads.Where(r => r.kind != "bridge").OrderBy(r => CampusPlanRoadNetwork.Distance(at.x, at.z, r) - r.width / 2).First();
                    float shoulder = CampusPlanRoadNetwork.Distance(at.x, at.z, road) - road.width / 2;
                    if (shoulder < .4f) props.AppendLine("PROP " + Path(c.transform) + " at=" + at.ToString("F2") + " road=" + road.name + " shoulder=" + shoulder.ToString("F2"));
                }
                foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh && f.GetComponent<Renderer>() && f.GetComponent<Renderer>().enabled))
                {
                    var r = f.GetComponent<Renderer>();
                    if (f.transform.parent == root || f.name.Contains("Metal") || f.name.Contains("Leaf") || f.name.Contains("Foliage"))
                        props.AppendLine("MESH " + Path(f.transform) + " asset=" + AssetDatabase.GetAssetPath(f.sharedMesh) + " at=" + game.WorldToMap(r.bounds.center).ToString("F2") + " size=" + r.bounds.size.ToString("F2") + " vertices=" + f.sharedMesh.vertexCount);
                }
                report.AppendLine("ROAD_COUNT " + data.roads.Length + "; BUILDING_COUNT " + data.features.Count(f => f.kind == "building"));
            }
        }
        string prefix = Environment.GetCommandLineArgs().Contains("-finalAudit") ? "/final-" : "/baseline-";
        File.WriteAllText(Output + prefix + "assets.txt", report.ToString());
        File.WriteAllText(Output + prefix + "labels.txt", labels.ToString());
        File.WriteAllText(Output + prefix + "props.txt", props.ToString());
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        Debug.Log("FINAL_ASSET_AUDIT_COMPLETE");
    }
}
