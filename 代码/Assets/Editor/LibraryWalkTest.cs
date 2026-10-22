using System.Collections.Generic;
using System.Linq;
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

        // Route G: the reworked south-east corner. Proves the gate lanes, the lobbys
        // main axis, the new flower bed's surroundings and the restroom door are all
        // physically reachable.
        Walk(sb, "G: 1F spawn -> gate lane -> SE flower bed -> restroom door",
            new Vector3(0f, 0.25f, -13.5f),
            new (float x, float z, string label)[]
            {
                (0f,   -11.0f, "through the centre gate lane"),
                (0f,    -4.0f, "open lobby south of the sand table"),
                (10.0f, -4.0f, "east toward the bed"),
                (14.6f, -3.4f, "north-west of the bed"),
                (17.9f, -2.4f, "in front of the flower bed"),
                (15.6f,  5.2f, "restroom door"),
            });

        // Route H: the reworked north-east lounge, plus the lift and stair approach.
        Walk(sb, "H: 1F spawn -> lounge -> reading booth",
            new Vector3(0f, 0.25f, -13.5f),
            new (float x, float z, string label)[]
            {
                (0f,   -9.0f, "past the gates"),
                (4.5f,   2.0f, "east of the sand table"),
                (5.0f,   7.4f, "in front of the sofa"),
                (8.0f,   9.5f, "between sofa and booth"),
                (8.0f,  13.2f, "booth west side"),
                (9.5f,  13.2f, "reading booth doorway"),
            });

        // Route I: the lift and stair approach stays clear of the new bed.
        Walk(sb, "I: 1F spawn -> lift -> stair approach",
            new Vector3(0f, 0.25f, -13.5f),
            new (float x, float z, string label)[]
            {
                (0f,   -9.0f, "past the gates"),
                (6.0f,  -7.4f, "clear of the stair head"),
                (14.0f, -7.4f, "east toward the lift"),
                (14.0f, -9.4f, "lift door"),
                (8.2f,  -7.4f, "stair approach"),
            });

        // Routes J/K: on 2F and 3F, walk from the stair head across the hall to the new
        // drinking fountain and the restroom door, then on to the corridor corner.
        foreach (var (fl, y) in new[] { (2, 4.32f), (3, 8.62f) })
        {
            Walk(sb, fl + "F: stair head -> fountain -> restroom -> corridor corner",
                new Vector3(6.8f, y + 0.1f, -9.6f),
                new (float x, float z, string label)[]
                {
                    (6.8f,  -2.0f, fl + "F: north into the hall"),
                    (11.0f,  4.0f, fl + "F: toward the north wall"),
                    (14.2f, 13.6f, fl + "F: at the drinking fountain"),
                    (14.2f,  8.4f, fl + "F: south of the restroom"),
                    (9.0f,   8.4f, fl + "F: west along the restroom front"),
                    (8.0f,   9.6f, fl + "F: restroom door"),
                    (6.0f,   0.0f, fl + "F: back into the hall"),
                    (-13.0f, 6.0f, fl + "F: corridor corner"),
                });
        }

        // Route J: the 2F route the corridor exists for - up the stair, north across the
        // open hall, left through the west doorway, the length of the corridor and back.
        Walk(sb, "J: 2F stair head -> hall -> west corridor -> back to stair",
            new Vector3(6.8f, 4.42f, -9.6f),
            new (float x, float z, string label)[]
            {
                (6.8f,   0.0f, "2F: north into the hall"),
                (-8.0f,  6.0f, "2F: left turn toward the west wall"),
                (-18.0f, 6.0f, "2F: at the corridor doorway"),
                (-26.0f, 6.0f, "2F: inside the corridor"),
                (-36.0f, 6.0f, "2F: far end of the corridor"),
                (-26.0f, 6.0f, "2F: walking back"),
                (-18.0f, 6.0f, "2F: back at the doorway"),
                (-6.0f,  2.0f, "2F: back in the hall"),
                (6.8f,  -9.6f, "2F: back at the stair head"),
            });

        // Route K: the same corridor walk one storey up, so 3F is verified too.
        Walk(sb, "K: 3F stair head -> hall -> west corridor -> back to stair",
            new Vector3(6.8f, 8.72f, -9.6f),
            new (float x, float z, string label)[]
            {
                (6.8f,   0.0f, "3F: north into the hall"),
                (-8.0f,  6.0f, "3F: left turn toward the west wall"),
                (-18.0f, 6.0f, "3F: at the corridor doorway"),
                (-26.0f, 6.0f, "3F: inside the corridor"),
                (-36.0f, 6.0f, "3F: far end of the corridor"),
                (-26.0f, 6.0f, "3F: walking back"),
                (-18.0f, 6.0f, "3F: back at the doorway"),
                (-6.0f,  2.0f, "3F: back in the hall"),
                (6.8f,  -9.6f, "3F: back at the stair head"),
            });

        CheckWestWallSealed(sb);
        CheckExitBindings(sb);
        CheckSpawns(sb);
        CheckSpotSelection(sb);
        CheckSandTable(sb);
        CheckDoorBarriers(sb);
        CheckVoidAllRound(sb);
        CheckRestrooms(sb);
        CheckLiftStops(sb);
        CheckVoid(sb);

        Debug.Log(sb.ToString());
        Debug.Log("WALKTEST_DONE");
    }


    // The 2F corridor doorway is the only opening in the west wall. 1F, 3F and 4F must
    // still be sealed there, otherwise the player could walk out into empty space.
    static void CheckWestWallSealed(StringBuilder sb)
    {
        sb.AppendLine("=== WEST WALL: open on 2F only ===");
        float[] floors = { 0f, 4.3f, 8.6f, 12.9f };
        for (int i = 0; i < floors.Length; i++)
        {
            float y = floors[i] + 1.0f;
            var origin = new Vector3(-18.0f, y, 6.0f);
            var hits = Physics.RaycastAll(origin, Vector3.left, 6f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool open = i == 1 || i == 2;   // 2F and 3F both have the corridor
            bool blocked = hits.Length > 0 && hits[0].distance < 3.0f;
            bool ok = open ? !blocked : blocked;

            string what = hits.Length > 0 ? $"'{hits[0].collider.gameObject.name}' at {hits[0].distance:F2}m" : "nothing";
            sb.AppendLine($"  {(ok ? "OK  " : "FAIL")} {i + 1}F west wall at Z=6: " +
                          $"{(open ? "should be OPEN" : "should be SEALED")} -> {what}");
        }
        sb.AppendLine();
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





    // Checks every spawn point declared on InteriorSceneRoot: it must sit on solid
    // ground with room for the player capsule, and be on the correct side of the plan.
    static void CheckSpawns(StringBuilder sb)
    {
        sb.AppendLine("=== LIBRARY SPAWN POINTS ===");
        var root = Object.FindFirstObjectByType<InteriorSceneRoot>();
        if (!root) { sb.AppendLine("  InteriorSceneRoot NOT FOUND"); return; }

        if (root.spawnPoints == null || root.spawnPoints.Length == 0)
        {
            sb.AppendLine("  no spawn points declared");
            return;
        }

        foreach (var sp in root.spawnPoints)
        {
            if (sp == null || sp.anchor == null)
            {
                sb.AppendLine($"  FAIL {sp?.id}: anchor is null");
                continue;
            }

            Vector3 p = sp.anchor.position;

            var down = Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 5f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(down, (a, b) => a.distance.CompareTo(b.distance));
            string ground = down.Length > 0
                ? $"{down[0].collider.gameObject.name} @Y={down[0].point.y:F2}"
                : "NOTHING";

            Vector3 lo = new Vector3(p.x, p.y + Radius, p.z);
            Vector3 hi = new Vector3(p.x, p.y + Height - Radius, p.z);
            int blocked = Physics.OverlapCapsule(lo, hi, Radius - 0.02f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore).Length;

            bool onGround = down.Length > 0 && Mathf.Abs(down[0].point.y - p.y) < 0.6f;
            bool ok = onGround && blocked == 0;

            string side = p.z < -8f ? "south" : p.z > 8f ? "north" : "MIDDLE";
            bool sideOk = (sp.id == "main" && side == "south") || (sp.id != "main" && side == "north");

            sb.AppendLine($"  {(ok && sideOk ? "OK  " : "FAIL")} id='{sp.id}' at ({p.x:F1},{p.y:F2},{p.z:F1}) " +
                          $"side={side}, ground={ground}, blocking colliders={blocked}");
        }

        sb.AppendLine($"  exits declared: {(root.exits == null ? 0 : root.exits.Length)}");
        if (root.exits != null)
            foreach (var e in root.exits)
                sb.AppendLine($"    exit '{(e ? e.name : "null")}' at " +
                              $"{(e ? $"({e.position.x:F1},{e.position.z:F1})" : "-")}");
        sb.AppendLine();
    }

    // Verifies the interaction prompt and the E action cannot disagree: both call
    // LibraryIndoorInteractions.PickSpot, and where ranges overlap the nearer spot wins.
    static void CheckSpotSelection(StringBuilder sb)
    {
        sb.AppendLine("=== INTERACTION SPOT SELECTION ===");
        sb.AppendLine("(both the prompt and the E action call the same selector)");

        var reception = new Vector3(-10.5f, 1.0f, -3.6f);
        var chute = new Vector3(-7.3f, 1.0f, -5.0f);
        const float receptionR = 3.0f, chuteR = 2.2f;

        int overlapSamples = 0, wrong = 0;
        for (int i = 0; i <= 12; i++)
        {
            Vector3 p = Vector3.Lerp(reception, chute, i / 12f);
            p.y = 1.0f;

            float dr = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(reception.x, reception.z));
            float dc = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(chute.x, chute.z));
            bool inR = dr <= receptionR, inC = dc <= chuteR;

            string picked = LibraryIndoorInteractions.PickSpotId(p);

            if (inR && inC)
            {
                overlapSamples++;
                string expected = dr < dc ? "reception_1f" : dc < dr ? "return_chute_1f" : null;
                bool ok = expected == null
                    ? (picked == "reception_1f" || picked == "return_chute_1f")
                    : picked == expected;
                if (!ok) wrong++;
                if (i % 3 == 0)
                    sb.AppendLine($"    t={i / 12f:F2} (dr={dr:F2}, dc={dc:F2}) -> '{picked}'" +
                                  $"{(ok ? "" : "  <-- WRONG (not the nearest)")}");
            }
        }

        sb.AppendLine($"  overlap samples: {overlapSamples}, disagreements with nearest rule: {wrong}");

        string far = LibraryIndoorInteractions.PickSpotId(new Vector3(0f, 1.0f, -6f));
        string lift = LibraryIndoorInteractions.PickSpotId(new Vector3(14.0f, 1.0f, -11.4f));
        sb.AppendLine($"  empty spot at (0,-6): '{(far ?? "null")}' (expected null)");
        sb.AppendLine($"  at the 1F lift door : '{(lift ?? "null")}' (expected elevator_1f)");
        sb.AppendLine();
    }

    // Verifies the exit -> entrance bindings for the four possible door combinations.
    // Needs CampusLiving open (for the runtime entrance list) plus the library scene
    // loaded additively (for the exit anchors), so it manages both scenes itself.
    static void CheckExitBindings(StringBuilder sb)
    {
        sb.AppendLine("=== EXIT -> ENTRANCE BINDING (four combinations) ===");

        // Remember what was open so the rest of the suite is unaffected.
        string previous = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;

        var campus = EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);
        var library = EditorSceneManager.OpenScene("Assets/Interiors/Scenes/library.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Additive);

        var travel = Object.FindFirstObjectByType<BuildingInteriorTravel>();
        var root = Object.FindFirstObjectByType<InteriorSceneRoot>();

        if (travel == null || root == null)
        {
            sb.AppendLine("  FAIL: BuildingInteriorTravel or InteriorSceneRoot not found");
        }
        else
        {
            sb.AppendLine($"  travel.entrances = {travel.entrances.Length}, " +
                          $"root.exits = {(root.exits == null ? 0 : root.exits.Length)}, " +
                          $"bindings = {(root.exitEntranceIds == null ? 0 : root.exitEntranceIds.Length)}");

            foreach (var exit in root.exits)
            {
                string boundId = root.ExitEntranceId(exit);
                var bound = BuildingInteriorTravel.ResolveExitEntrance(
                    exit, root.exits, root.exitEntranceIds, travel.entrances);

                sb.AppendLine($"    exit '{exit.name}' -> bound '{(boundId ?? "null")}' -> " +
                              $"entrance '{(bound ? bound.EntranceId : "null")}', " +
                              $"return=({bound.returnMapPosition.x:F1},{bound.returnMapPosition.z:F1}), " +
                              $"yaw={bound.enterYaw:F0}");
            }

            // The four combinations: leaving through S or N, having entered via front or rear.
            var exitS = root.exits[0];
            var exitN = root.exits.Length > 1 ? root.exits[1] : null;
            var front = travel.entrances.FirstOrDefault(e => e && e.buildingId == "library" && e.EntranceId == "library");
            var rear = travel.entrances.FirstOrDefault(e => e && e.buildingId == "library" && e.EntranceId == "library-rear");

            var cases = new (string label, Transform exit, BuildingEntranceDefinition cameIn, string expect)[]
            {
                ("front in  -> front out", exitS, front, "library"),
                ("front in  -> rear  out", exitN, front, "library-rear"),
                ("rear  in  -> front out", exitS, rear,  "library"),
                ("rear  in  -> rear  out", exitN, rear,  "library-rear"),
            };

            int ok = 0;
            foreach (var (label, exit, cameIn, expect) in cases)
            {
                var resolved = BuildingInteriorTravel.ResolveExitEntrance(
                    exit, root.exits, root.exitEntranceIds, travel.entrances);
                // BuildingInteriorTravel falls back to the entrance used when the exit
                // has no binding; the library binds both, so resolution is direct.
                bool good = resolved && resolved.EntranceId == expect;
                if (good) ok++;
                sb.AppendLine($"    {(good ? "OK  " : "FAIL")} {label}: returns to '{(resolved ? resolved.EntranceId : "null")}' " +
                              $"({(resolved ? resolved.returnMapPosition.x.ToString("F1") + "," + resolved.returnMapPosition.z.ToString("F1") : "-")})");
            }

            sb.AppendLine($"  -> {ok}/{cases.Length} combinations correct");
        }

        sb.AppendLine();

        // Close the library scene and restore the single-scene state the rest uses.
        EditorSceneManager.CloseScene(library, true);
        EditorSceneManager.OpenScene(previous.Length > 0 ? previous
            : "Assets/Interiors/Scenes/library.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
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
