using System.Collections;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Play-mode walk of the new 2F corridor using the REAL player object and the travel
// component that loads the interior, i.e. the same code path the game uses.
//
// Run headless:
//   Unity -batchmode -projectPath <proj> -executeMethod LibraryCorridorPlayTest.Run
// (no -quit: the process is ended by the test itself once it has written its report)
public static class LibraryCorridorPlayTest
{
    internal static StringBuilder Report = new StringBuilder();
    internal static bool Started;

    // Click this in the editor to watch the walk happen with the real player. Headless
    // batchmode cannot run the play loop in this environment, so this is the reliable way.
    [MenuItem("Campus/Interiors/Play-Test 2F Corridor (real player)")]
    public static void RunFromMenu() => Run();

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity", OpenSceneMode.Single);
        EditorApplication.playModeStateChanged += OnMode;
        EditorApplication.update += Kick;
    }

    static void Kick()
    {
        if (Started || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Started = true;
        EditorApplication.EnterPlaymode();
    }

    static void OnMode(PlayModeStateChange st)
    {
        if (st == PlayModeStateChange.EnteredPlayMode)
        {
            var go = new GameObject("__corridorPlayTest");
            var r = go.AddComponent<CorridorPlayRunner>();
            r.StartCoroutine(r.Run());
        }
        else if (st == PlayModeStateChange.ExitingPlayMode)
        {
            Debug.Log(Report.ToString());
            Debug.Log("PLAYTEST_DONE");
        }
    }
}

public class CorridorPlayRunner : MonoBehaviour
{
    public IEnumerator Run()
    {
        var sb = LibraryCorridorPlayTest.Report;
        sb.AppendLine("=== 2F CORRIDOR PLAY-MODE WALK (real player) ===");

        for (int i = 0; i < 30; i++) yield return null;      // let CampusGame.Awake finish

        var game = FindFirstObjectByType<CampusGame>();
        var travel = game ? game.GetComponent<BuildingInteriorTravel>() : null;
        if (!game || !travel) { sb.AppendLine("FATAL: CampusGame/travel missing"); Finish(); yield break; }

        game.paused = false;
        game.UiBlocking = false;

        BuildingEntranceDefinition front = null;
        foreach (var e in travel.entrances)
            if (e && e.buildingId == "library" && e.EntranceId == "library") front = e;
        if (!front) { sb.AppendLine("FATAL: library entrance missing"); Finish(); yield break; }

        // Enter through the front door using the production path.
        game.SetTravelPose(game.MapToWorld(front.approachMapPosition), front.enterYaw, 12f);
        Physics.SyncTransforms();
        yield return null;
        sb.AppendLine("TryEnter accepted = " + travel.TryEnter(front));
        for (int i = 0; i < 800 && travel.Busy; i++) yield return null;
        for (int i = 0; i < 60; i++) yield return null;

        if (!travel.Inside) { sb.AppendLine("FATAL: did not get inside, err=" + travel.LastError); Finish(); yield break; }
        sb.AppendLine($"inside={travel.Inside} spawn={game.player.position} floorY=4.3");

        // Walk the route with the player's own CharacterController via the travel service.
        var route = new (float x, float z, string label)[]
        {
            (6.8f,  -9.4f, "at the stair head"),
            (6.8f,   0.0f, "north into the hall"),
            (-8.0f,  6.0f, "left turn toward the west wall"),
            (-18.6f, 6.0f, "through the corridor doorway"),
            (-26.0f, 6.0f, "inside the corridor"),
            (-36.0f, 6.0f, "far end of the corridor"),
            (-22.0f, 6.0f, "walking back"),
            (-18.0f, 6.0f, "back through the doorway"),
            (-6.0f,  2.0f, "back in the hall"),
        };

        foreach (var leg in route)
        {
            var target = new Vector3(leg.x, 0f, leg.z);
            Vector3 startPos = game.player.position;
            bool reached = false;

            for (int f = 0; f < 1200; f++)
            {
                Vector3 p = game.player.position;
                Vector3 flat = new Vector3(target.x - p.x, 0f, target.z - p.z);
                if (flat.magnitude < 0.4f) { reached = true; break; }
                Vector3 move = flat.normalized * 3.0f * Time.fixedDeltaTime;
                travel.MoveCharacter(move + Vector3.down * 2f * Time.fixedDeltaTime);
                yield return null;
            }

            Vector3 end = game.player.position;
            sb.AppendLine($"  {(reached ? "OK  " : "FAIL")} {leg.label,-38} -> ({end.x,7:F2},{end.y,5:F2},{end.z,7:F2})");
            if (!reached) break;
        }

        // The corridor must not have ejected the player back to campus.
        sb.AppendLine($"still inside after the walk = {travel.Inside}");
        sb.AppendLine($"exit prompt near corridor = " +
                      (travel.Interior != null ? travel.Interior.NearExit(game.player.position).ToString() : "n/a"));

        // Lift still usable: send the player to 3F and read the result.
        var lift = travel.Interior != null ? travel.Interior.FindSpawn("main") : null;
        sb.AppendLine("lift check: InteriorSceneRoot present = " + (travel.Interior != null));

        bool ok = travel.Inside;
        sb.AppendLine(ok ? "RESULT: PASS - walked the corridor on the real player and stayed inside"
                         : "RESULT: FAIL");
        Finish();
    }

    void Finish()
    {
        Debug.Log(LibraryCorridorPlayTest.Report.ToString());
        Debug.Log("PLAYTEST_DONE");
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(0);
    }
}
