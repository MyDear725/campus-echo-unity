using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Small, non-solid notice stands make semester actions discoverable in the real world.
// Classroom points are inside the actual numbered rooms, not a remote phone attendance button.
public sealed class CampusSemesterPoint : MonoBehaviour
{
    public string location, label;
    public float radius = 2.4f;
    static readonly List<CampusSemesterPoint> points = new List<CampusSemesterPoint>();
    void OnEnable() { if (!points.Contains(this)) points.Add(this); }
    void OnDisable() { points.Remove(this); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Clear() { points.Clear(); }
    public static string PlaceName(string id)
    {
        switch (id)
        {
            case "a": return "A101教室";
            case "b": return "B103教室";
            case "c": return "C103机房";
            case "library": return "图书馆服务台";
            case "dorm": return "宿舍一楼休息登记处";
            case "cafeteria": return "第六食堂同学交流处";
            case "bridge": return "学苑桥校园活动点";
            case "lake": return "湖畔作品采集点";
            case "track": return "田径场跑步集合点";
            default: return "校园活动点";
        }
    }
    public static CampusSemesterPoint Nearby(CampusGame game)
    {
        if (!game || !game.player || (game.Interiors && game.Interiors.Busy)) return null;
        CampusSemesterPoint best = null;
        float distance = float.MaxValue;
        foreach (var p in points)
        {
            if (!p || !p.gameObject.activeInHierarchy) continue;
            bool inside = game.Interiors && game.Interiors.Inside;
            if (inside ? p.gameObject.scene != game.Interiors.Interior.gameObject.scene : p.gameObject.scene != game.gameObject.scene) continue;
            if (p.location == "dorm")
            {
                var dorm = DormitoryCorridor.Current(game);
                if (!dorm || dorm.Floor != 1 || dorm.InsideRoom) continue;
            }
            var delta = p.transform.position - game.player.position;
            if (Mathf.Abs(delta.y) > 1.4f) continue;
            float sq = new Vector2(delta.x, delta.z).sqrMagnitude;
            if (sq > p.radius * p.radius || sq >= distance) continue;
            distance = sq; best = p;
        }
        return best;
    }
    public static void Prepare(CampusGame game)
    {
        // Each scene owns its stands; unloading an interior removes only its own points.
        if (!game.Interiors || !game.Interiors.Inside)
        {
            Add(game.gameObject.scene, null, "bridge", game.MapToWorld(SafeOutdoor(game,new Vector3(145,.23f,25))), "校园活动 · 学期故事");
            Add(game.gameObject.scene, null, "lake", game.MapToWorld(SafeOutdoor(game,new Vector3(-33,.23f,64))), "湖畔作品采集");
            Add(game.gameObject.scene, null, "track", game.MapToWorld(SafeOutdoor(game,new Vector3(-132,.23f,-51))), "跑步小队集合");
            return;
        }
        var root = game.Interiors.Interior;
        string building = root.buildingId;
        if (building == "teaching-a") Add(root.gameObject.scene, root.transform, "a", new Vector3(-6.2f,.06f,5.8f), "A101 · 高等数学 / 考核");
        if (building == "teaching-b") Add(root.gameObject.scene, root.transform, "b", new Vector3(5.8f,.06f,5.8f), "B103 · 英语 / 学期事务");
        if (building == "teaching-c") Add(root.gameObject.scene, root.transform, "c", new Vector3(5.8f,.06f,5.8f), "C103 · 程序设计 / 项目");
        // Spawn-relative stands avoid assumptions about the unrelated cafeteria/library origins.
        var spawn = root.spawnPoints != null && root.spawnPoints.Length > 0 ? root.spawnPoints[0]?.anchor : null;
        if (!spawn) return;
        var local = root.transform.InverseTransformPoint(spawn.position) + new Vector3(2.6f,0,1.4f);
        if (building == "library") Add(root.gameObject.scene, root.transform, "library", local, "图书整理 · 互助自习");
        if (building == "cafeteria") Add(root.gameObject.scene, root.transform, "cafeteria", local, "同班伙伴 · 餐后交流");
        if (building.StartsWith("dormitory-", StringComparison.Ordinal)) Add(root.gameObject.scene, root.transform, "dorm", new Vector3(2,.06f,-.6f), "休息至明早 · 每日计划");
    }
    static Vector3 SafeOutdoor(CampusGame game,Vector3 target)
    {
        if(game.IsMapWalkable(target))return target;
        for(int radius=1;radius<=18;radius++)for(int angle=0;angle<16;angle++)
        {
            var candidate=target+Quaternion.Euler(0,angle*22.5f,0)*(Vector3.forward*radius);
            if(game.IsMapWalkable(candidate))return candidate;
        }
        return target;
    }
    static void Add(Scene scene, Transform parent, string id, Vector3 position, string text)
    {
        foreach (var old in points) if (old && old.gameObject.scene == scene && old.location == id) return;
        var go = new GameObject("SemesterPoint_" + id);
        if (parent) { go.transform.SetParent(parent, false); go.transform.localPosition = position; }
        else { SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = position; }
        var point = go.AddComponent<CampusSemesterPoint>(); point.location = id; point.label = text;
        // TextMesh uses the same bundled Chinese font as the existing scene labels.
        var label = new GameObject("学期互动提示").AddComponent<TextMesh>();
        label.transform.SetParent(go.transform, false); label.transform.localPosition = Vector3.up * 1.75f;
        label.text = text + "\n靠近按 E"; label.fontSize = 42; label.characterSize = .035f;
        label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = new Color(1,.93f,.72f);
        label.font = Resources.Load<Font>("CampusFont");
        if (label.font) label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
    }
    void LateUpdate()
    {
        var s = CampusSemester.Instance;
        if (s && s.Game && s.Game.followCamera) transform.rotation = Quaternion.LookRotation(transform.position - s.Game.followCamera.transform.position, Vector3.up);
    }
}
