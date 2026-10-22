using System.Collections;
using System.IO;
using UnityEngine;

// Game-view verification for the phone, booking, seated study and vending machines.
// Runs only with -phoneQA; uses throw-away save keys so real data is untouched.
public class PhoneQA : MonoBehaviour
{
    static bool started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (started) return;
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-phoneQA") < 0) return;
        started = true;
        var go = new GameObject("__phoneQA");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<PhoneQA>();
    }

    string Dir => Path.Combine(Application.dataPath, "../QA/Phone");
    readonly System.Text.StringBuilder log = new System.Text.StringBuilder();

    IEnumerator Start()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "progress.txt"), "RUNNING\n");

        // isolate every save this run touches
        CampusWallet.KeyOverride = "QA_Wallet";
        LibraryReservationManager.KeyOverride = "QA_Reservation";
        VendingSystem.KeyOverridePrefix = "QA_Stock_";
        VendingSystem.InventoryKeyOverride = "QA_Inventory";
        CampusWallet.ResetCache();
        PlayerPrefs.DeleteKey("QA_Wallet");
        PlayerPrefs.DeleteKey("QA_Reservation");
        PlayerPrefs.DeleteKey("QA_Stock_vending_drinks_1f");
        PlayerPrefs.DeleteKey("QA_Stock_vending_snacks_1f");
        PlayerPrefs.DeleteKey("QA_Inventory");
        CampusWallet.SetForTest(30000);

        yield return new WaitForSecondsRealtime(2f);

        var game = FindFirstObjectByType<CampusGame>();
        var travel = game ? game.GetComponent<BuildingInteriorTravel>() : null;
        if (!game || !travel) { Log("FATAL no game/travel"); yield break; }

        game.paused = false;
        game.UiBlocking = false;
        game.Seated = false;

        // ---- 1. phone opens ----
        var phone = PhoneUI.Ensure();
        phone.Open();
        yield return new WaitForSecondsRealtime(0.7f);
        Log($"phoneOpen={phone.IsOpen} uiBlocking={game.UiBlocking} cursorFree={Cursor.lockState == CursorLockMode.None}");
        yield return null;
        Shot("01_phone_home.png");

        // ---- 2. enter the library ----
        phone.Close();
        yield return new WaitForSecondsRealtime(0.3f);
        Log($"phoneClosed={!phone.IsOpen} uiBlocking={game.UiBlocking}");

        BuildingEntranceDefinition lib = null;
        foreach (var e in travel.entrances) if (e && e.buildingId == "library") lib = e;
        if (!lib) { Log("FATAL no library entrance"); yield break; }

        game.SetTravelPose(game.MapToWorld(lib.approachMapPosition), lib.enterYaw, 12f);
        Physics.SyncTransforms();
        yield return null;
        travel.TryEnter(lib);
        for (int i = 0; i < 600 && travel.Busy; i++) yield return null;
        for (int i = 0; i < 60; i++) yield return null;
        if (!travel.Inside) { Log("FATAL not inside: " + travel.LastError); yield break; }

        var reg = LibrarySeatRegistry.Ensure();
        Log($"seats total={reg.All.Count} 1F={reg.CountOnFloor(1)} 2F={reg.CountOnFloor(2)} 3F={reg.CountOnFloor(3)}");

        // ---- 3. book a 2F seat with the phone open, so the planner is on screen ----
        var seat = reg.OnFloor(2).Count > 0 ? reg.OnFloor(2)[0] : null;
        if (seat == null) { Log("FATAL no 2F seat"); yield break; }
        var rm = LibraryReservationManager.Ensure();

        phone.Open();
        phone.ShowSeatPageForTest(2);
        yield return new WaitForSecondsRealtime(0.6f);
        Shot("02_phone_seatmap.png");
        yield return new WaitForSecondsRealtime(0.3f);
        Log($"phoneDrawCount={PhoneUI.DrawCount} rect={PhoneUI.LastRect}");
        phone.PickForTest(seat);
        yield return new WaitForSecondsRealtime(0.4f);
        bool ok = rm.TryReserve(seat.id, seat.floor, seat.area);
        phone.ShowMyBookingForTest();
        yield return new WaitForSecondsRealtime(0.6f);
        Shot("02b_phone_mybooking.png");
        Log($"reserved={ok} id={rm.ReservedSeatId} floor={rm.ReservedFloor}");
        phone.Close();
        yield return new WaitForSecondsRealtime(0.3f);

        // ---- 4. walk to that exact seat and sit ----
        MovePlayer(game, seat.standPos, seat.facing.eulerAngles.y);
        yield return new WaitForSecondsRealtime(0.6f);

        var study = LibraryStudy.Ensure();
        var near = study.SeatNearPlayer();
        Log($"nearSeat={(near != null ? near.id : "none")} matchesReserved={(near != null && near.id == seat.id)}");

        bool sat = study.TrySit(seat);
        yield return new WaitForSecondsRealtime(0.8f);
        Log($"sat={sat} seated={study.Seated} playerY={game.player.position.y:F2} seatY={seat.sitPos.y:F2} frozen={game.Seated}");
        Shot("03_seated.png");
        yield return new WaitForSecondsRealtime(0.8f);

        // ---- 5. study: clock accelerates ----
        var clock = GameClock.Ensure();
        double before = clock.GameSeconds;
        double t0 = Time.realtimeSinceStartupAsDouble;
        study.StartStudy();
        study.StartStudy();                     // must not stack
        yield return new WaitForSecondsRealtime(3f);
        double gained = clock.GameSeconds - before;
        double real = Time.realtimeSinceStartupAsDouble - t0;
        Log($"studying={study.Studying} mult={clock.CurrentMultiplier} gameSec={gained:F0} realSec={real:F1} " +
            $"ratio={(gained / Mathf.Max(0.01f, (float)real)):F1}x");
        Shot("04_studying.png");

        // ---- 6. stop, stand, verify normal rate ----
        study.StopStudy();
        yield return new WaitForSecondsRealtime(0.3f);
        double b2 = clock.GameSeconds;
        yield return new WaitForSecondsRealtime(1f);
        double g2 = clock.GameSeconds - b2;
        Log($"afterStop mult={clock.CurrentMultiplier} gameSecPerReal={g2:F1}");

        study.TryStandUp();
        yield return new WaitForSecondsRealtime(0.5f);
        Log($"stoodUp={!study.Seated} gameSeated={game.Seated} reservedStill={rm.ReservedSeatId}");
        Shot("05_stood_up.png");

        // ---- 7. vending machine ----
        var vm = VendingMachineUI.Ensure();
        vm.drinksMachine = FindAnchor("VendingAnchor_Drinks");
        vm.snacksMachine = FindAnchor("VendingAnchor_Snacks");
        if (vm.drinksMachine)
        {
            MovePlayer(game, vm.drinksMachine.position + new Vector3(0f, 0.1f, 1.6f), 0f);
            yield return new WaitForSecondsRealtime(0.5f);
            string at = vm.MachineAtPlayerPublic();
            Log($"machineAt={at}");
            if (at != null)
            {
                vm.OpenAt(at);
                yield return new WaitForSecondsRealtime(0.8f);
                Shot("06_vending_open.png");          // menu on screen, before buying
                yield return new WaitForSecondsRealtime(0.4f);

                long balBefore = CampusWallet.BalanceFen;
                string msg;
                var st = VendingSystem.Load(at);
                bool bought = VendingSystem.TryBuy(st, 0, 1, out msg);
                yield return new WaitForSecondsRealtime(0.6f);
                Log($"bought={bought} {msg} balance {balBefore} -> {CampusWallet.BalanceFen}");
                Shot("07_vending_bought.png");
                yield return new WaitForSecondsRealtime(0.3f);
                vm.Close();
            }
        }

        Log("DONE");
        File.WriteAllText(Path.Combine(Dir, "log.txt"), log.ToString());
        File.WriteAllText(Path.Combine(Dir, "progress.txt"), "DONE\n");
        Debug.Log("[PhoneQA]\n" + log);
        yield return new WaitForSecondsRealtime(1f);
        Application.Quit(0);
    }

    Transform FindAnchor(string name)
    {
        var travel = FindFirstObjectByType<BuildingInteriorTravel>();
        if (travel && travel.Interior)
        {
            var t = travel.Interior.transform.Find(name);
            if (t) return t;
        }
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name == name) return t;
        return null;
    }

    void MovePlayer(CampusGame game, Vector3 pos, float yaw)
    {
        var cc = game.player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        game.player.position = pos;
        game.player.rotation = Quaternion.Euler(0, yaw, 0);
        if (cc) cc.enabled = true;
        Physics.SyncTransforms();
    }

    // Grabs the frame synchronously so the IMGUI pass that just ran is included.
    void Shot(string name)
    {
        StartCoroutine(ShotRoutine(name));
    }

    System.Collections.IEnumerator ShotRoutine(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        if (tex)
        {
            File.WriteAllBytes(Path.Combine(Dir, name), tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
    void Log(string m) { log.AppendLine(m); Debug.Log("[PhoneQA] " + m); }
}
