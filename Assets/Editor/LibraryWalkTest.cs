using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Physically walks a player capsule along the intended 2F route using the real scene
// colliders: 1F stair entry -> up the switchback stair -> 2F hall -> through the great
// door -> into the round reading hall. Prints progress per leg so any blocker is named.
//
// This is a regression test for the two failures found this way: the round hall's floor
// and roof cylinders carrying Unity's default CapsuleCollider (which degenerates into a
// huge sphere that plugs the doorway), and a structural column left standing on the
// walking line to the door. Run it after any change to the interior.
public static class LibraryWalkTest
{
    const float Height = 1.8f, Radius = 0.32f, Step = 0.3f;
    const float Speed = 3.0f;

    [MenuItem("Campus/Interiors/Validate Library Walkability")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Interiors/Scenes/library.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();

        var sb = new StringBuilder();

        // Route A: the whole 2F journey, from the 1F lobby up into the round hall.
        Walk(sb, "A: 1F lobby -> stairs -> 2F hall -> great door -> round hall",
            new Vector3(9.85f, 0.25f, -9.0f),
            new (float x, float z, string label)[]
            {
                (9.85f, -13.5f, "1F: up flight B (east lane)"),
                (9.85f, -15.0f, "1F: half landing"),
                (6.55f, -15.0f, "turn west onto flight A"),
                (6.55f, -12.0f, "climb flight A to 2F"),
                (6.80f,  -9.5f, "2F: step onto the landing slab"),
                (6.80f,   0.0f, "2F: north into the hall"),
                (12.0f,   0.0f, "2F: east toward the great door"),
                (19.9f,   0.0f, "at the great door"),
                (26.0f,   0.0f, "inside the round hall (main walkway)"),
                (29.5f,   0.0f, "at the railed central opening"),
            });

        // Route B: on 2F, from the stair landing to the restroom at the north wall.
        Walk(sb, "B: 2F stair landing -> restroom (north wall)",
            new Vector3(6.8f, 4.32f, -9.8f),
            new (float x, float z, string label)[]
            {
                (6.8f, -6.0f, "2F: walk north"),
                (8.0f,  4.0f, "2F: continue north"),
                (8.0f,  7.6f, "2F: approach the restroom door"),
                (8.0f, 10.5f, "2F: inside the restroom"),
            });

        // Route C: 1F, from the south entrance to the sand table and the north door.
        Walk(sb, "C: 1F south door -> sand table -> north door",
            new Vector3(0f, 0.25f, -13.5f),
            new (float x, float z, string label)[]
            {
                (0f, -6.0f, "1F: north from the entrance"),
                (-5.5f, -4.0f, "1F: swing west of the sand table"),
                (-5.5f, 5.0f, "1F: past the sand table"),
                (0f, 12.0f, "1F: north door lobby"),
                (0f, 14.6f, "1F: at the north door"),
            });

        // Routes D/E: climb from 2F to 3F and 3F to 4F, to prove every storey is reachable
        // by stair (the lift is a script teleport, tested separately).
        Walk(sb, "D: 2F -> 3F by stairs",
            new Vector3(9.85f, 4.32f, -9.0f),
            new (float x, float z, string label)[]
            {
                (9.85f, -13.5f, "2F: up flight B"),
                (9.85f, -15.0f, "2F: half landing"),
                (6.55f, -15.0f, "turn west onto flight A"),
                (6.55f, -12.0f, "climb flight A to 3F"),
                (6.80f,  -9.5f, "3F: step onto the landing slab"),
            });

        Walk(sb, "E: 3F -> 4F by stairs",
            new Vector3(9.85f, 8.62f, -9.0f),
            new (float x, float z, string label)[]
            {
                (9.85f, -13.5f, "3F: up flight B"),
                (9.85f, -15.0f, "3F: half landing"),
                (6.55f, -15.0f, "turn west onto flight A"),
                (6.55f, -12.0f, "climb flight A to 4F"),
                (6.80f,  -9.5f, "4F: step onto the landing slab"),
            });

        // Route F: on 3F, from the landing to the round hall on that storey.
        Walk(sb, "F: 3F hall -> great door -> 3F round hall",
            new Vector3(6.8f, 8.62f, -9.8f),
            new (float x, float z, string label)[]
            {
                (6.8f,   0.0f, "3F: north into the hall"),
                (12.0f,  0.0f, "3F: east toward the great door"),
                (19.9f,  0.0f, "3F: at the great door"),
                (26.0f,  0.0f, "3F: in the round hall walkway"),
                (29.5f,  0.0f, "3F: at the railed opening"),
            });

        CheckSandTable(sb);
        CheckDoorBarriers(sb);
        CheckVoidAllRound(sb);
        CheckRestrooms(sb);
        CheckLiftStops(sb);
        CheckVoid(sb);

        Debug.Log(sb.ToString());
        Debug.Log("WALKTEST_DONE");
    }

    // Verifies each of the four lift stops has solid floor and room for the player
    // capsule, since ChooseFloor() simply repositions the player there.
    static void CheckLiftStops(StringBuilder sb)
    {
        sb.AppendLine("=== LIFT STOPS (ChooseFloor targets) ===");
        const float storey = 4.3f;
        const float lx = 14.0f, lz = -10.6f;

        for (int floor = 1; floor <= 4; floor++)
        {
            float y = (floor - 1) * storey;

            // Solid floor just under the feet?
            var down = Physics.RaycastAll(new Vector3(lx, y + 0.4f, lz), Vector3.down, 2.0f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(down, (a, b) => a.distance.CompareTo(b.distance));
            string floorHit = down.Length > 0
                ? $"{down[0].collider.gameObject.name} @Y={down[0].point.y:F2}"
                : "NOTHING";

            // Enough room for the capsule to stand there?
            Vector3 lo = new Vector3(lx, y + 0.10f + Radius, lz);
            Vector3 hi = new Vector3(lx, y + 0.10f + Height - Radius, lz);
            int overlaps = 0;
            foreach (var c in Physics.OverlapCapsule(lo, hi, Radius - 0.02f,
                         Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                overlaps++;
            }

            bool ok = down.Length > 0 && overlaps == 0;
            sb.AppendLine($"  {(ok ? "OK  " : "FAIL")} {floor}F at Y={y:F1}: floor={floorHit}, " +
                          $"blocking colliders={overlaps}");
        }
        sb.AppendLine();
    }




    // Confirms the campus sand table cannot be entered, including by jumping on it.
    // Rays are cast horizontally at two heights: knee height, and at the apex of a
    // jump (the jump gives about 1.2m, so a body there spans 1.2m to 3.0m).
    static void CheckSandTable(StringBuilder sb)
    {
        sb.AppendLine("=== 1F SAND TABLE BARRIER ===");
        var c = new Vector3(0f, 0f, 1.5f);          // plinth centre
        var probes = new (string name, Vector3 from, Vector3 dir, float expected)[]
        {
            ("from south", new Vector3(0f, 0f, -6f), Vector3.forward, 2.65f),
            ("from north", new Vector3(0f, 0f, 9f), Vector3.back, 2.65f),
            ("from west", new Vector3(-7f, 0f, 1.5f), Vector3.right, 3.55f),
            ("from east", new Vector3(7f, 0f, 1.5f), Vector3.left, 3.55f),
        };

        foreach (var (name, from, dir, expected) in probes)
        {
            foreach (float h in new[] { 0.6f, 1.6f })
            {
                var origin = new Vector3(from.x, h, from.z);
                var hits = Physics.RaycastAll(origin, dir, 12f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                if (hits.Length == 0)
                {
                    sb.AppendLine($"  FAIL {name} @y={h:F1}: nothing hit at all");
                    continue;
                }

                var h0 = hits[0];
                bool airWall = h0.collider.gameObject.name == "SandTable_AirWall";
                sb.AppendLine($"  {(airWall ? "OK  " : "FAIL")} {name} @y={h:F1}: first hit " +
                              $"'{h0.collider.gameObject.name}' at {h0.distance:F2}m");
            }
        }
        sb.AppendLine();
    }
    // Walks into each 1F doorway and confirms the invisible barrier stops the player
    // before they step off the edge of the floor.
    static void CheckDoorBarriers(StringBuilder sb)
    {
        sb.AppendLine("=== 1F DOOR BARRIERS (should not fall off the edge) ===");
        var cases = new (string name, Vector3 start, float targetZ)[]
        {
            ("south main door", new Vector3(0f, 0.06f, -13.0f), -25f),
            ("north rear door", new Vector3(0f, 0.06f, 13.0f), 25f),
        };

        foreach (var (name, start, targetZ) in cases)
        {
            var probe = new GameObject("DoorProbe");
            var cc = probe.AddComponent<CharacterController>();
            cc.height = Height; cc.radius = Radius; cc.center = new Vector3(0, Height * 0.5f, 0);
            cc.stepOffset = Step; cc.slopeLimit = 50f; cc.skinWidth = 0.02f; cc.minMoveDistance = 0;
            probe.transform.position = start;
            Physics.SyncTransforms();

            float gravity = 0f;
            for (int f = 0; f < 900; f++)
            {
                Vector3 p = probe.transform.position;
                Vector3 flat = new Vector3(0f, 0f, targetZ - p.z);
                if (flat.magnitude < 0.2f) break;
                gravity = cc.isGrounded ? -2f : gravity - 18f * Time.fixedDeltaTime;
                Vector3 vel = flat.normalized * Speed;
                vel.y = gravity;
                cc.Move(vel * Time.fixedDeltaTime);
            }
            Vector3 end = probe.transform.position;
            bool fell = end.y < -0.6f;
            bool stoppedInside = Mathf.Abs(end.z) < 16f;
            sb.AppendLine($"  {(fell ? "FAIL" : "OK  ")} {name}: stopped at z={end.z:F2}, y={end.y:F2}" +
                          $"{(fell ? "  <-- FELL THROUGH" : "")}" +
                          $"{(stoppedInside ? "" : "  <-- walked past the facade")}");
            Object.DestroyImmediate(probe);
        }
        sb.AppendLine();
    }

    // Tries to walk into the central opening from eight directions on every upper
    // storey. All of them must be held back at the railing.
    static void CheckVoidAllRound(StringBuilder sb)
    {
        sb.AppendLine("=== CENTRAL OPENING: 8-WAY CONTAINMENT ===");
        const float cx = 34.5f, cz = 0f;

        foreach (int floor in new[] { 2, 3, 4 })
        {
            float fy = (floor - 1) * 4.3f;
            int held = 0, fell = 0;
            float worst = 99f;

            for (int k = 0; k < 8; k++)
            {
                float ang = k * 45f * Mathf.Deg2Rad;
                var start = new Vector3(cx + Mathf.Cos(ang) * 5.5f, fy + 0.06f, cz + Mathf.Sin(ang) * 5.5f);

                var probe = new GameObject("VoidProbe");
                var cc = probe.AddComponent<CharacterController>();
                cc.height = Height; cc.radius = Radius; cc.center = new Vector3(0, Height * 0.5f, 0);
                cc.stepOffset = Step; cc.slopeLimit = 50f; cc.skinWidth = 0.02f; cc.minMoveDistance = 0;
                probe.transform.position = start;
                Physics.SyncTransforms();

                float gravity = 0f;
                for (int f = 0; f < 600; f++)
                {
                    Vector3 p = probe.transform.position;
                    Vector3 flat = new Vector3(cx - p.x, 0f, cz - p.z);
                    if (flat.magnitude < 0.15f) break;
                    gravity = cc.isGrounded ? -2f : gravity - 18f * Time.fixedDeltaTime;
                    Vector3 vel = flat.normalized * Speed;
                    vel.y = gravity;
                    cc.Move(vel * Time.fixedDeltaTime);
                }

                Vector3 end = probe.transform.position;
                float d = Mathf.Sqrt((end.x - cx) * (end.x - cx) + (end.z - cz) * (end.z - cz));
                worst = Mathf.Min(worst, d);
                if (end.y < fy - 0.6f) fell++; else held++;
                Object.DestroyImmediate(probe);
            }

            sb.AppendLine($"  {floor}F: held={held}/8  fell={fell}  closest approach={worst:F2}m (railing at 4.5m)");
        }
        sb.AppendLine();
    }
    // Confirms each restroom is a closed box: casting outwards from its centre must hit
    // the room's own wall in every direction. A missing wall shows up as a hit much
    // further away than the room's own extent (or as nothing at all).
    static void CheckRestrooms(StringBuilder sb)
    {
        sb.AppendLine("=== RESTROOM ENCLOSURE ===");
        var dirs = new (string name, Vector3 dir)[]
        {
            ("+X east", Vector3.right), ("-X west", Vector3.left),
            ("+Z north", Vector3.forward), ("-Z south", Vector3.back),
        };

        foreach (var floor in new[] { "1F", "2F", "3F", "4F" })
        {
            var go = FindInScene(floor + "_Restrooms");
            if (!go) { sb.AppendLine($"  {floor}: room object NOT FOUND"); continue; }

            var b = new Bounds();
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }

            Vector3 c = new Vector3(b.center.x, b.min.y + 1.0f, b.center.z);
            var line = new System.Text.StringBuilder($"  {floor}: ");
            bool ok = true;

            foreach (var (name, dir) in dirs)
            {
                float limit = (Mathf.Abs(dir.x) > 0.5f ? b.extents.x : b.extents.z);
                var hits = Physics.RaycastAll(c, dir, 30f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

                float d = hits.Length > 0 ? hits[0].distance : 99f;
                string what = hits.Length > 0 ? hits[0].collider.gameObject.name : "NOTHING";
                bool closed = d <= limit + 0.7f;

                // South is the doorway, so it may legitimately open into the hall.
                if (name.Contains("south")) closed = true;

                if (!closed) ok = false;
                line.Append($"{name}={d:F1}m({what}){(closed ? "" : " <-- OPEN")}  ");
            }

            sb.AppendLine(line.ToString());
            sb.AppendLine($"         -> {(ok ? "OK: enclosed" : "FAIL: a wall is missing")}");
        }
        sb.AppendLine();
    }

    static GameObject FindInScene(string name)
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.gameObject;
        return null;
    }
    // Verifies the central opening is a real hole and that the railing stops a walker
    // before they reach it.
    static void CheckVoid(StringBuilder sb)
    {
        sb.AppendLine("=== CENTRAL OPENING (should be a real 9m hole) ===");
        const float cx = 34.5f, cz = 0f;    // round hall centre
        float floorY = 4.3f;

        var down = Physics.RaycastAll(new Vector3(cx, floorY + 1.0f, cz), Vector3.down, 14f,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(down, (a, b) => a.distance.CompareTo(b.distance));
        if (down.Length == 0)
        {
            sb.AppendLine("  centre: nothing below within 14m -> open");
        }
        else
        {
            var h = down[0];
            bool isFloor = Mathf.Abs(h.point.y - floorY) < 0.25f;
            sb.AppendLine($"  centre: first hit '{h.collider.gameObject.name}' at Y={h.point.y:F2}");
            sb.AppendLine(isFloor
                ? "  -> FAIL: the centre is still decked over, not an opening"
                : "  -> OK: the centre is open (the hit is well below the 2F floor)");
        }

        // Walk from just inside the door straight at the centre; the railing should stop us.
        var probe = new GameObject("VoidProbe");
        var cc = probe.AddComponent<CharacterController>();
        cc.height = Height; cc.radius = Radius; cc.center = new Vector3(0, Height * 0.5f, 0);
        cc.stepOffset = Step; cc.slopeLimit = 50f; cc.skinWidth = 0.02f; cc.minMoveDistance = 0;
        probe.transform.position = new Vector3(cx - 5.5f, floorY + 0.02f, cz);
        Physics.SyncTransforms();

        float gravity = 0f;
        for (int f = 0; f < 1200; f++)
        {
            Vector3 p = probe.transform.position;
            Vector3 flat = new Vector3(cx - p.x, 0f, cz - p.z);
            if (flat.magnitude < 0.2f) break;
            gravity = cc.isGrounded ? -2f : gravity - 18f * Time.fixedDeltaTime;
            Vector3 vel = flat.normalized * Speed;
            vel.y = gravity;
            cc.Move(vel * Time.fixedDeltaTime);
        }
        Vector3 end = probe.transform.position;
        float distToCentre = Mathf.Sqrt((end.x - cx) * (end.x - cx) + (end.z - cz) * (end.z - cz));
        sb.AppendLine($"  walked at the centre, stopped {distToCentre:F2}m from it at Y={end.y:F2}");
        bool fell = end.y < floorY - 0.6f;
        bool heldAtRail = distToCentre > 3.8f && distToCentre < 6.0f;
        string verdict = fell ? "FAIL: fell into the opening"
            : heldAtRail ? "OK: held back by the railing (expected ~4.8m)"
            : "CHECK: stopped at an unexpected distance";
        sb.AppendLine($"  -> {verdict}");
        sb.AppendLine();
        Object.DestroyImmediate(probe);
    }

    static void Walk(StringBuilder sb, string title, Vector3 start,
                     (float x, float z, string label)[] legs)
    {
        sb.AppendLine("=== ROUTE " + title + " ===");
        var probe = new GameObject("WalkProbe");
        var cc = probe.AddComponent<CharacterController>();
        cc.height = Height; cc.radius = Radius; cc.center = new Vector3(0, Height * 0.5f, 0);
        cc.stepOffset = Step; cc.slopeLimit = 50f; cc.skinWidth = 0.02f; cc.minMoveDistance = 0;
        probe.transform.position = start;
        Physics.SyncTransforms();

        float gravity = 0f;
        int failures = 0;
        foreach (var leg in legs)
        {
            float startY = probe.transform.position.y;
            bool reached = false;
            var target = new Vector3(leg.x, 0f, leg.z);

            for (int f = 0; f < 900; f++)
            {
                Vector3 p = probe.transform.position;
                Vector3 flat = new Vector3(target.x - p.x, 0f, target.z - p.z);
                if (flat.magnitude < 0.35f) { reached = true; break; }
                gravity = cc.isGrounded ? -2f : gravity - 18f * Time.fixedDeltaTime;
                Vector3 vel = flat.normalized * Speed;
                vel.y = gravity;
                cc.Move(vel * Time.fixedDeltaTime);
            }

            Vector3 end = probe.transform.position;
            sb.AppendLine($"  {(reached ? "OK  " : "FAIL")} {leg.label,-40} " +
                          $"-> ({end.x,6:F2},{end.y,6:F2},{end.z,7:F2})  dy={end.y - startY,6:F2}");
            if (!reached)
            {
                failures++;
                Vector3 lo = new Vector3(end.x, end.y + Radius, end.z);
                Vector3 hi = new Vector3(end.x, end.y + Height - Radius, end.z);
                Vector3 dir = new Vector3(leg.x - end.x, 0f, leg.z - end.z).normalized;
                var hs = Physics.CapsuleCastAll(lo, hi, Radius, dir, 3f,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hs, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var h in hs)
                    sb.AppendLine($"         blocker {h.distance,5:F2}m  '{h.collider.gameObject.name}'");
            }
        }
        Vector3 fin = probe.transform.position;
        sb.AppendLine($"  -> {(failures == 0 ? "PASS" : failures + " leg(s) blocked")}   " +
                      $"final ({fin.x:F2},{fin.y:F2},{fin.z:F2})");
        sb.AppendLine();
        Object.DestroyImmediate(probe);
    }
}
