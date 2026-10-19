using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

// Opt-in graphical regression. Run without -batchmode/-nographics so IMGUI is painted.
public class CampusOverlayRenderQA : MonoBehaviour
{
    static bool requested;
    readonly StringBuilder log = new StringBuilder();
    string directory;
    bool finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate()
    {
        requested = Array.IndexOf(Environment.GetCommandLineArgs(), "-overlayRenderQA") >= 0;
        if (!requested) return;
        CampusWallet.KeyOverride = "OverlayQA_Wallet";
        CampusWallet.ResetCache();
        LibraryReservationManager.KeyOverride = "OverlayQA_Reservation";
        VendingSystem.KeyOverridePrefix = "OverlayQA_Stock_";
        VendingSystem.InventoryKeyOverride = "OverlayQA_Inventory";
        GameClock.KeyOverride = "OverlayQA_Clock";
        foreach (var key in new[] { "OverlayQA_Reservation", "OverlayQA_Stock_vending_drinks_1f",
            "OverlayQA_Stock_vending_snacks_1f", "OverlayQA_Inventory", "OverlayQA_Clock" })
            PlayerPrefs.DeleteKey(key);
        CampusWallet.SetForTest(30000);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!requested) return;
        requested = false;
        var go = new GameObject("OverlayRenderQA");
        DontDestroyOnLoad(go);
        go.AddComponent<CampusOverlayRenderQA>();
    }

    void Awake()
    {
        directory = Path.Combine(Application.dataPath, "../QA/Overlay");
        var args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-overlayQAOutput");
        if (at >= 0 && at + 1 < args.Length) directory = args[at + 1];
        Directory.CreateDirectory(directory);
        Application.logMessageReceived += OnLog;
        StartCoroutine(Timeout());
    }

    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        var game = FindFirstObjectByType<CampusGame>();
        Check(game && game.Interiors, "campus player ready");
        game.paused = false;
        var phone = PhoneUI.Ensure();
        int count = PhoneUI.DrawCount;
        phone.Open();
        yield return new WaitForSecondsRealtime(.5f);
        Check(PhoneUI.DrawCount > count, "phone Repaint events on campus");
        Check(game.UiBlocking, "phone owns movement/cursor lock");
        yield return Shot("01_phone_home.png");
        phone.Close();
        Check(!game.UiBlocking, "phone releases movement/cursor lock");

        BuildingEntranceDefinition entry = null;
        foreach (var e in game.Interiors.entrances) if (e && e.buildingId == "library") { entry = e; break; }
        Check(entry, "library entrance exists");
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition), entry.enterYaw, 12);
        Physics.SyncTransforms();
        yield return null;
        Check(game.Interiors.TryEnter(entry), "library entry accepted");
        float deadline = Time.realtimeSinceStartup + 30;
        while (game.Interiors.Busy && Time.realtimeSinceStartup < deadline) yield return null;
        yield return new WaitForSecondsRealtime(.5f);
        Check(game.Interiors.Inside, "entered library additively");
        Check(PhoneUI.Instance == phone, "same phone instance survives library load");
        Check(FindObjectsByType<PhoneUI>(FindObjectsSortMode.None).Length == 1, "one active phone");
        Check(FindObjectsByType<CampusOverlayPainter>(FindObjectsSortMode.None).Length == 1, "one painter");
        Check(LibrarySeatRegistry.Ensure().All.Count > 0, "real seats registered");

        phone.Open();
        phone.ShowSeatPageForTest(2);
        yield return new WaitForSecondsRealtime(.5f);
        yield return Shot("02_library_seatmap.png");
        var seat = LibrarySeatRegistry.Instance.OnFloor(2)[0];
        Check(LibraryReservationManager.Instance.TryReserve(seat.id, seat.floor, seat.area), "reserve real seat");
        phone.ShowMyBookingForTest();
        yield return new WaitForSecondsRealtime(.3f);
        yield return Shot("03_my_booking.png");
        phone.Close();

        // Disabled or destroyed overlays must unregister instead of poisoning the next frame.
        phone.enabled = false;
        phone.enabled = true;
        count = PhoneUI.DrawCount;
        phone.Open();
        yield return new WaitForSecondsRealtime(.3f);
        Check(PhoneUI.DrawCount > count, "phone renders after disable/enable");
        phone.Close();

        var vending = VendingMachineUI.Ensure();
        Check(vending.drinksMachine && vending.snacksMachine, "both serialized machine anchors transferred");
        foreach (var machine in new[] { "vending_drinks_1f", "vending_snacks_1f" })
        {
            var anchor = machine == "vending_drinks_1f" ? vending.drinksMachine : vending.snacksMachine;
            var cc = game.player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            game.SetTravelPose(anchor.position + new Vector3(0, .2f, 1.5f), 180);
            if (cc) cc.enabled = true;
            Physics.SyncTransforms();
            yield return null;
            vending.OpenAt(machine);
            yield return new WaitForSecondsRealtime(.4f);
            Check(VendingMachineUI.AnyOpen, machine + " stays open at actual machine");
            yield return Shot(machine + ".png");
            var stock = VendingSystem.Load(machine);
            long balance = CampusWallet.BalanceFen;
            Check(VendingSystem.TryBuy(stock, 0, 1, out string message), machine + " purchase");
            Check(CampusWallet.BalanceFen == balance - stock.items[0].priceFen, "charged once");
            vending.Close();
        }

        // Exercise the visible study overlay as well as the phone and vending panels.
        var controller = game.player.GetComponent<CharacterController>();
        if (controller) controller.enabled = false;
        game.SetTravelPose(seat.standPos, seat.facing.eulerAngles.y);
        if (controller) controller.enabled = true;
        Physics.SyncTransforms();
        var study = LibraryStudy.Ensure();
        Check(study.TrySit(seat), "sit at reserved seat");
        study.StartStudy();
        yield return new WaitForSecondsRealtime(.5f);
        Check(Mathf.Approximately(GameClock.Instance.CurrentMultiplier, 5), "study multiplier 5x");
        yield return Shot("06_study.png");
        study.TryStandUp();
        Check(Mathf.Approximately(GameClock.Instance.CurrentMultiplier, 1), "study multiplier restored");
        Log("PASS: graphical overlay regression complete");
        finished = true;
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-overlayQAHold") >= 0)
        {
            // Keep the isolated test save open for real mouse/keyboard verification.
            if (controller) controller.enabled = false;
            game.SetTravelPose(vending.drinksMachine.position + new Vector3(0, .2f, 1.5f), 180);
            if (controller) controller.enabled = true;
            Physics.SyncTransforms();
            vending.OpenAt("vending_drinks_1f");
            Log("READY: manual UI verification (isolated test save)");
            yield break;
        }
        Application.Quit(0);
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        Check(texture && texture.width > 0, "captured " + name);
        File.WriteAllBytes(Path.Combine(directory, name), texture.EncodeToPNG());
        Destroy(texture);
    }

    void Check(bool result, string message)
    {
        Log((result ? "PASS " : "FAIL ") + message);
        if (!result) throw new Exception("Overlay render regression: " + message);
    }

    void Log(string text)
    {
        log.AppendLine(text);
        File.WriteAllText(Path.Combine(directory, "results.txt"), log.ToString());
        Debug.Log("[OverlayRenderQA] " + text);
    }

    void OnLog(string message, string trace, LogType type)
    {
        if (finished || (type != LogType.Exception && type != LogType.Error)) return;
        finished = true;
        log.AppendLine("ERROR " + message + "\n" + trace);
        File.WriteAllText(Path.Combine(directory, "results.txt"), log.ToString());
        Application.Quit(1);
    }

    IEnumerator Timeout()
    {
        yield return new WaitForSecondsRealtime(90);
        if (!finished) { Log("FAIL timed out waiting for graphical frames"); Application.Quit(1); }
    }

    void OnDestroy() { Application.logMessageReceived -= OnLog; }
}
