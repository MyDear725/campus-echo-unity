using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// Release inspection: every mapped campus route, not just selected landmarks.
// Explicit command-line opt-in; GithubIntegrationQA provides private save keys.
public sealed class CampusFinalEnvironmentQA : MonoBehaviour
{
    CampusGame game; string folder; float vertical; int samples; bool done;
    readonly StringBuilder report = new StringBuilder();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { if (Environment.GetCommandLineArgs().Contains("-finalEnvironmentQA")) new GameObject("Final campus environment QA").AddComponent<CampusFinalEnvironmentQA>(); }
    void Check(bool ok, string message) { if (!ok) throw new Exception(message); report.AppendLine("PASS: " + message); }
    IEnumerator Start()
    {
        folder = Path.Combine(Application.dataPath, "../QA/FinalAudit"); Directory.CreateDirectory(folder);
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            bool more; object next = null;
            try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
            catch (Exception e) { File.WriteAllText(folder + "/routes-result.txt", "FAIL: " + e + "\n" + report); Debug.LogException(e); Application.Quit(1); yield break; }
            if (!more) { stack.Pop(); continue; } if (next is IEnumerator child) stack.Push(child); else yield return next;
        }
    }
    void LateUpdate() { if (game) game.paused = false; }
    void Update() { if (!done && Time.realtimeSinceStartup > 300) { File.WriteAllText(folder + "/routes-result.txt", "FAIL: timeout\n" + report); Application.Quit(1); } }
    void Pose(MapPoint p)
    {
        vertical = 0; game.SetTravelPose(game.MapToWorld(new Vector3(p.x, CampusPlanTerrain.Height(p.x, p.z) + .08f, p.z)), 180);
    }
    IEnumerator Walk(MapPoint p, string name)
    {
        var target = game.MapToWorld(new Vector3(p.x, 0, p.z)); int count = 0;
        while (true)
        {
            var delta = target - game.player.position; delta.y = 0; if (delta.magnitude < .10f) break;
            var before = game.player.position;
            game.Outdoor.Move(Vector3.ClampMagnitude(delta, .12f), .023f, ref vertical, false); samples++;
            if (++count > 4800) throw new Exception(name + " blocked toward " + p.x + "," + p.z + " at " + game.WorldToMap(game.player.position) + "; nearby=" + string.Join(" | ", Physics.OverlapCapsule(game.player.position+Vector3.up*.4f,game.player.position+Vector3.up*1.4f,.38f,(1<<9)|(1<<10)).Select(c=>c.transform.parent.name+"/"+c.name)));
            if (game.player.position.y < -1 || before.y - game.player.position.y > .5f) throw new Exception(name + " has an unsupported floor at " + game.WorldToMap(game.player.position));
            if (count % 30 == 0) yield return null;
        }
    }
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    static bool Connects(CampusRoad a, CampusRoad b)
    {
        for (int i = 1; i < a.points.Length; i++) for (int j = 1; j < b.points.Length; j++)
        {
            var p = new Vector2(a.points[i - 1].x, a.points[i - 1].z); var q = new Vector2(b.points[j - 1].x, b.points[j - 1].z);
            var u = new Vector2(a.points[i].x, a.points[i].z) - p; var v = new Vector2(b.points[j].x, b.points[j].z) - q;
            float denominator = Cross(u, v);
            if (Mathf.Abs(denominator) > .0001f)
            { float t = Cross(q - p, v) / denominator, s = Cross(q - p, u) / denominator; if (t >= 0 && t <= 1 && s >= 0 && s <= 1) return true; }
            float d = Mathf.Min(CampusCollision.Segment(p.x, p.y, b.points[j - 1], b.points[j]), CampusCollision.Segment((p + u).x, (p + u).y, b.points[j - 1], b.points[j]));
            d = Mathf.Min(d, CampusCollision.Segment(q.x, q.y, a.points[i - 1], a.points[i]), CampusCollision.Segment((q + v).x, (q + v).y, a.points[i - 1], a.points[i]));
            if (d < (a.width + b.width) / 2 - .15f) return true;
        }
        return false;
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(3); game = FindFirstObjectByType<CampusGame>(); game.paused = false;
        var map = JsonUtility.FromJson<CampusData>(game.mapData.text); var roads = map.roads.Where(r => r.kind != "external").ToArray();
        Check(!FindObjectsByType<BoxCollider>(FindObjectsSortMode.None).Any(c => c.enabled && (c.transform.lossyScale.x < 0 || c.transform.lossyScale.y < 0 || c.transform.lossyScale.z < 0)), "No reflected active outdoor BoxColliders");
        var visited = new HashSet<int> { 0 }; var queue = new Queue<int>(); queue.Enqueue(0);
        while (queue.Count > 0) { int i = queue.Dequeue(); for (int j = 0; j < roads.Length; j++) if (!visited.Contains(j) && Connects(roads[i], roads[j])) { visited.Add(j); queue.Enqueue(j); } }
        Check(visited.Count == roads.Length, "All " + roads.Length + " campus road ribbons form one connected network: " + string.Join(",", Enumerable.Range(0, roads.Length).Where(i => !visited.Contains(i)).Select(i => roads[i].name)));
        var blocked = new List<string>();
        foreach (var road in roads)
        {
            Pose(road.points[0]); string failure = null;
            var destinations = road.points.Skip(1).ToList();
            // Short entrance spurs also have to remain walkable on the way out.
            if (road.points.Length <= 3) destinations.AddRange(road.points.Reverse().Skip(1));
            foreach (var p in destinations)
            {
                var walk = Walk(p, road.name); bool more;
                do { try { more=walk.MoveNext(); } catch(Exception e) { failure=e.Message; more=false; } if(more)yield return walk.Current; } while(more);
                if(failure!=null)break;
            }
            if(failure!=null){blocked.Add(failure);report.AppendLine("FAIL: "+failure);}else Check(true, "Real capsule walked " + road.name);
            File.WriteAllText(folder + "/routes-progress.txt", report.ToString());
        }
        Check(blocked.Count==0,"No blocked campus road: "+string.Join("; ",blocked));
        foreach (var e in game.Interiors.entrances) Check(game.IsMapWalkable(e.approachMapPosition) && game.IsMapWalkable(e.returnMapPosition), "Entrance map clearance " + e.EntranceId);
        done = true; report.AppendLine("Physical movement steps=" + samples + ". Public vehicle streets excluded; bridge is included as an independent elevated walk.");
        File.WriteAllText(folder + "/routes-result.txt", report.ToString()); Debug.Log("FINAL_ENVIRONMENT_QA_PASS"); Application.Quit(0);
    }
}
