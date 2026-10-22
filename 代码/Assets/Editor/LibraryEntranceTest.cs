using System.Collections;
using System.Text;
using UnityEditor;
using UnityEngine;

// Play-mode regression test for the library's two entrances.
//
// Drives the real BuildingInteriorTravel service inside CampusLiving: enters through
// the front door and the rear door (and alternates), then measures where the player
// actually ended up and which spawn point was used.
//
// Run headless with:
//   Unity -batchmode -projectPath <proj> -executeMethod LibraryEntranceTest.Run
public static class LibraryEntranceTest
{
    internal static StringBuilder Report;

    // Run from the Unity menu (cannot be driven headless: batchmode + EnterPlaymode
    // hangs waiting for the GI cache). Results are printed to the Console.
    [MenuItem("Campus/Interiors/Test Library Front and Rear Doors")]
    public static void RunFromMenu() => Run();

    public static void Run()
    {
        Report = new StringBuilder();

        // batchmode + EnterPlaymode is asynchronous, so drive it by polling rather than
        // expecting a coroutine to survive the domain transition.
        EditorApplication.update += Poll;

        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.EnterPlaymode();
    }

    static void Poll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var runner = new GameObject("__entranceTestRunner")
                .AddComponent<LibraryEntranceRunner>();
            runner.StartCoroutine(runner.Run());
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Debug.Log(Report.ToString());
            Debug.Log("ENTRANCETEST_DONE");
        }
    }
}

class SpawnResult
{
    public bool measured;
    public string desc = "(not measured)";
    public float y;
    public bool inside;
}

class LibraryEntranceRunner : MonoBehaviour
{
    const float Height = 1.8f, Radius = 0.32f;

    internal IEnumerator Run()
    {
        for (int i = 0; i < 10; i++) yield return null;

        var game = FindFirstObjectByType<CampusGame>();
        var travel = game ? game.GetComponent<BuildingInteriorTravel>() : null;

        if (game == null || travel == null)
        {
            Debug.LogError("ENTRANCETEST FATAL: CampusGame or BuildingInteriorTravel missing");
            LeavePlayMode();
            yield break;
        }

        game.paused = false;
        game.UiBlocking = false;

        var front = FindEntrance(travel, "");
        var rear = FindEntrance(travel, "library-rear");

        var sb = LibraryEntranceTest.Report;
        sb.AppendLine("=== LIBRARY ENTRANCE PLAY-MODE TEST ===");
        sb.AppendLine("runtime entrance list (library entries only):");
        foreach (var e in travel.entrances)
            if (e && e.buildingId == "library")
                sb.AppendLine("   '" + e.EntranceId + "' spawnId='" + e.spawnId +
                              "' approach=(" + e.approachMapPosition.x + "," + e.approachMapPosition.z + ")");
        sb.AppendLine();
        sb.AppendLine("front: entranceId='" + front.EntranceId + "' spawnId='" + front.spawnId + "'");
        sb.AppendLine("rear : entranceId='" + rear.EntranceId + "' spawnId='" + rear.spawnId + "'");
        sb.AppendLine();

        var rFront1 = new SpawnResult();
        var rRear = new SpawnResult();
        var rFront2 = new SpawnResult();

        yield return EnterThrough(game, travel, front, "FRONT", rFront1);
        yield return EnterThrough(game, travel, rear, "REAR", rRear);
        yield return EnterThrough(game, travel, front, "FRONT repeat", rFront2);

        sb.AppendLine("=== RESULTS ===");
        sb.AppendLine("front (1st)   : " + rFront1.desc + "  y=" + rFront1.y.ToString("F2"));
        sb.AppendLine("rear          : " + rRear.desc + "  y=" + rRear.y.ToString("F2"));
        sb.AppendLine("front (repeat): " + rFront2.desc + "  y=" + rFront2.y.ToString("F2"));
        sb.AppendLine();

        bool pass = rFront1.measured && rRear.measured && rFront2.measured
            && System.Math.Abs(rFront1.y - rRear.y) < 0.01f
            && rFront1.inside && rRear.inside;

        sb.AppendLine(pass
            ? "RESULT: PASS - front and rear doors place the player at different spawn points"
            : "RESULT: FAIL - see the details above");

        Debug.Log(sb.ToString());
        Debug.Log("ENTRANCETEST_DONE");
        LeavePlayMode();
    }

    static void LeavePlayMode() => EditorApplication.ExitPlaymode();

    static BuildingEntranceDefinition FindEntrance(BuildingInteriorTravel travel, string suffix)
    {
        foreach (var e in travel.entrances)
            if (e && e.buildingId == "library" && e.EntranceId == ("library" + suffix))
                return e;
        return null;
    }

    IEnumerator EnterThrough(CampusGame game, BuildingInteriorTravel travel,
                             BuildingEntranceDefinition entry, string label, SpawnResult result)
    {
        var sb = LibraryEntranceTest.Report;
        sb.AppendLine("--- " + label + " ---");

        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition), entry.enterYaw, 12f);
        Physics.SyncTransforms();
        yield return null;

        var reachable = travel.NearbyEntrance();
        sb.AppendLine("  nearby entrance at the approach point: '" +
                      (reachable ? reachable.EntranceId : "null") + "'");

        bool accepted = travel.TryEnter(entry);
        sb.AppendLine("  TryEnter -> " + accepted);

        for (int i = 0; i < 600 && travel.Busy; i++) yield return null;
        for (int i = 0; i < 90; i++) yield return null;

        var root = travel.Interior;
        result.inside = travel.Inside;
        result.y = game.player ? game.player.position.y : 0f;

        if (root != null)
        {
            var want = root.FindSpawn(entry.spawnId);
            var player = game.player.position;
            if (want == null)
            {
                result.desc = "spawn '" + entry.spawnId + "' NOT FOUND in the interior";
            }
            else
            {
                float d = Vector3.Distance(player, want.position);
                result.measured = d < 1.5f;
                result.desc = result.measured
                    ? "spawn '" + entry.spawnId + "' at (" + want.position.x.ToString("F1") + "," +
                      want.position.y.ToString("F2") + "," + want.position.z.ToString("F1") + ") - player landed there"
                    : "MISMATCH: player (" + player.x.ToString("F1") + "," + player.y.ToString("F2") + "," +
                      player.z.ToString("F1") + ") vs anchor (" + want.position.x.ToString("F1") + "," +
                      want.position.y.ToString("F2") + "," + want.position.z.ToString("F1") + "), d=" + d.ToString("F1") + "m";
            }
        }
        else
        {
            result.desc = "interior did not load (Interior == null), LastError=" + travel.LastError;
        }

        sb.AppendLine("  -> " + result.desc + "  (inside=" + result.inside + ", y=" + result.y.ToString("F2") + ")");

        if (result.inside && travel.Interior != null)
        {
            int guard = 0;
            while (travel.Interior != null && !travel.Interior.NearExit(game.player.position) && guard++ < 40)
            {
                Transform ex = null;
                if (travel.Interior.exits != null && travel.Interior.exits.Length > 0)
                    ex = travel.Interior.exits[0];
                if (ex == null) break;

                Vector3 flat = new Vector3(ex.position.x - game.player.position.x, 0f,
                                           ex.position.z - game.player.position.z);
                if (flat.magnitude < 0.15f) break;
                game.player.position += flat.normalized * Mathf.Min(0.9f, flat.magnitude);
                Physics.SyncTransforms();
                yield return null;
            }

            bool exited = travel.TryExit();
            sb.AppendLine("  TryExit -> " + exited);
            for (int i = 0; i < 600 && travel.Busy; i++) yield return null;
            for (int i = 0; i < 30; i++) yield return null;

            Vector3 expect = game.MapToWorld(entry.returnMapPosition);
            sb.AppendLine("  back on campus at (" + game.player.position.x.ToString("F1") + "," +
                          game.player.position.z.ToString("F1") + "), expected (" +
                          expect.x.ToString("F1") + "," + expect.z.ToString("F1") + ")");
        }

        sb.AppendLine();
        yield return new WaitForSecondsRealtime(0.2f);
    }
}
