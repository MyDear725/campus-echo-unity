using System.Collections;
using System.IO;
using UnityEngine;

// Verifies the systems when the LIBRARY scene is played on its own, which is how the
// reported bug showed up (no CampusGame in that scene).
public class LibraryDirectQA : MonoBehaviour
{
    static bool started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (started) return;
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-libDirectQA") < 0) return;
        started = true;
        var go = new GameObject("__libDirectQA");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<LibraryDirectQA>();
    }

    string Dir => Path.Combine(Application.dataPath, "../QA/Direct");

    IEnumerator Start()
    {
        Directory.CreateDirectory(Dir);
        var log = new System.Text.StringBuilder();
        yield return new WaitForSecondsRealtime(1.5f);

        CampusWallet.KeyOverride = "QA_D_Wallet";
        LibraryReservationManager.KeyOverride = "QA_D_Res";
        VendingSystem.KeyOverridePrefix = "QA_D_Stock_";
        VendingSystem.InventoryKeyOverride = "QA_D_Inv";
        CampusWallet.ResetCache();
        CampusWallet.SetForTest(30000);

        // Enter the library through the normal travel flow; the scene is additive, so
        // nothing exists until it is loaded.
        var game0 = FindFirstObjectByType<CampusGame>();
        var travel = game0 ? game0.GetComponent<BuildingInteriorTravel>() : null;
        if (game0 && travel)
        {
            BuildingEntranceDefinition lib = null;
            foreach (var e in travel.entrances) if (e && e.buildingId == "library") lib = e;
            if (lib)
            {
                game0.paused = false;
                game0.SetTravelPose(game0.MapToWorld(lib.approachMapPosition), lib.enterYaw, 12f);
                Physics.SyncTransforms();
                yield return null;
                travel.TryEnter(lib);
                for (int i = 0; i < 600 && travel.Busy; i++) yield return null;
                for (int i = 0; i < 40; i++) yield return null;
            }
        }
        log.AppendLine($"insideLibrary={(travel && travel.Inside)}");

        var reg = LibrarySeatRegistry.Instance;
        log.AppendLine($"registry={(reg ? "ok" : "NULL")} seats={(reg ? reg.All.Count : 0)}");
        log.AppendLine($"phone={(PhoneUI.Instance ? "ok" : "NULL")} " +
                       $"vending={(VendingMachineUI.Instance ? "ok" : "NULL")} " +
                       $"seatUI={(LibrarySeatInteraction.Instance ? "ok" : "NULL")} " +
                       $"clock={(GameClock.Instance ? "ok" : "NULL")}");
        log.AppendLine($"bootstrapParts={LibrarySeatBootstrap.LastPartCount}");
        log.AppendLine($"campusGame={(FindFirstObjectByType<CampusGame>() ? "present" : "absent")}");
        log.AppendLine($"painter={(FindFirstObjectByType<CampusOverlayPainter>() ? "present" : "ABSENT")}");

        var phone = PhoneUI.Instance;
        if (phone)
        {
            phone.Open();
            yield return new WaitForSecondsRealtime(0.8f);
            log.AppendLine($"phoneOpen={phone.IsOpen} drawCount={PhoneUI.DrawCount}");
            Shot("direct_01_phone.png");
            yield return new WaitForSecondsRealtime(0.5f);
            phone.Close();
        }

        var vm = VendingMachineUI.Instance;
        if (vm && vm.drinksMachine)
        {
            var cam = FindFirstObjectByType<Camera>();
            if (cam)
            {
                cam.transform.position = vm.drinksMachine.position + new Vector3(0f, 1.6f, 3.2f);
                cam.transform.LookAt(vm.drinksMachine.position + Vector3.up * 1.2f);
            }
            vm.OpenAt("vending_drinks_1f");
            yield return new WaitForSecondsRealtime(0.8f);
            log.AppendLine($"vendingOpen={VendingMachineUI.AnyOpen}");
            Shot("direct_02_vending.png");
            vm.Close();
        }

        File.WriteAllText(Path.Combine(Dir, "log.txt"), log.ToString());
        Debug.Log("[DirectQA]\n" + log);
        yield return new WaitForSecondsRealtime(0.8f);
        Application.Quit(0);
    }

    void Shot(string n) { StartCoroutine(Grab(n)); }
    IEnumerator Grab(string n)
    {
        yield return new WaitForEndOfFrame();
        var t = ScreenCapture.CaptureScreenshotAsTexture();
        if (t) { File.WriteAllBytes(Path.Combine(Dir, n), t.EncodeToPNG()); Object.Destroy(t); }
    }
}
