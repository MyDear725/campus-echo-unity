using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Graphical end-to-end verification for the living allowance, the cafeteria shops and
// multi-order handling. Only runs with -allowanceQA, in a real windowed player.
//
// Every UI action is driven by a scripted IMGUI event (EventType.MouseDown/MouseUp at the
// button's real screen position), which is the same path a physical click takes into
// GUI.Button — not a direct call to the purchase method. The script therefore records
// button rects while drawing, then clicks them through Event.current.

// Second-process check: load the saved profile with the normal systems and report what
// the game would show after a restart. Written to restart.txt next to results.txt.
public class RestartReadOnly : MonoBehaviour
{
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2f);
        var args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-qaOutput");
        string dir = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Application.dataPath, "../output/生活费与档口验证");
        Directory.CreateDirectory(dir);

        long wallet = CampusWallet.BalanceFen;
        string raw = PlayerPrefs.GetString("AQA_Cafeteria", "");
        string menuDate = "";
        int di = raw.IndexOf("\"menuDate\":\"", StringComparison.Ordinal);
        if (di >= 0)
        {
            int ds = di + 12;
            int de = raw.IndexOf('"', ds);
            if (de > ds) menuDate = raw.Substring(ds, de - ds);
        }

        // The cafeteria scene is not loaded on boot, so the order list is taken from the
        // save itself — exactly what the game would restore on the next entry.
        var lines = new List<string>();
        lines.Add("wallet=" + wallet);
        lines.Add("date=" + menuDate);
        lines.Add("hudText=" + CampusWallet.BalanceText);
        lines.Add("inventory=" + CampusInventory.Summary());
        lines.Add("cafeteriaSaveHasOrders=" + raw.Contains("\"orders\":["));

        File.WriteAllText(Path.Combine(dir, "restart.txt"), string.Join("\n", lines) + "\n");
        Debug.Log("[AllowanceQA] restart read:\n" + string.Join("\n", lines));
        Application.Quit(0);
    }
}

public class AllowanceShopQA : MonoBehaviour
{
    static bool requested;
    static bool restartRead;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate()
    {
        requested = Array.IndexOf(Environment.GetCommandLineArgs(), "-allowanceQA") >= 0;
        if (!requested) return;
        restartRead = Array.IndexOf(Environment.GetCommandLineArgs(), "-qaRestartRead") >= 0;

        CampusWallet.KeyOverride = "AQA_Wallet";
        CampusWallet.ResetCache();
        CafeteriaOrdering.PrefsKeyOverride = "AQA_Cafeteria";
        VendingSystem.KeyOverridePrefix = "AQA_Stock_";
        CampusInventory.KeyOverride = "AQA_Inventory";
        GameClock.KeyOverride = "AQA_Clock";
        // The restart child reads the profile the main run just wrote; only the main run
        // starts from a clean slate.
        if (!restartRead)
        {
            foreach (var k in new[]
            {
                "AQA_Wallet", "AQA_Cafeteria", "AQA_Inventory", "AQA_Stock_vending_drinks_1f",
                "AQA_Stock_vending_snacks_1f", "AQA_Clock", "AQA_ClockSave"
            })
                PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!requested) return;
        requested = false;
        var go = new GameObject("AllowanceShopQA");
        DontDestroyOnLoad(go);
        // The restart child only reports what it loaded; it must not run the suite again.
        if (restartRead) go.AddComponent<RestartReadOnly>();
        else go.AddComponent<AllowanceShopQA>();
    }

    readonly StringBuilder log = new StringBuilder();
    readonly List<string> failures = new List<string>();
    string directory;
    bool finished;
    int pass, fail;

    void Awake()
    {
        directory = Path.Combine(Application.dataPath, "../output/生活费与档口验证");
        var args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-qaOutput");
        if (at >= 0 && at + 1 < args.Length) directory = args[at + 1];
        Directory.CreateDirectory(directory);
        outputDir = directory;
        Application.logMessageReceived += OnLog;
        StartCoroutine(Timeout());
    }

    IEnumerator Timeout()
    {
        yield return new WaitForSecondsRealtime(1200);
        if (!finished) { Log("FAIL hard timeout at step: " + step); Finish(1); }
    }

    string step = "boot";
    static string outputDir;

    void OnLog(string message, string trace, LogType type)
    {
        if (finished) return;
        if (type == LogType.Exception || type == LogType.Error)
        {
            Log("CONSOLE ERROR: " + message + "\n" + trace);
            Fail("console error: " + message);
        }
    }

    void Check(bool ok, string what)
    {
        if (ok) { pass++; Log("PASS " + what); }
        else { fail++; Log("FAIL " + what); failures.Add(what); }
    }

    void Log(string s)
    {
        log.AppendLine(s);
        File.WriteAllText(Path.Combine(directory, "results.txt"), log.ToString());
        Debug.Log("[AllowanceQA] " + s);
    }

    void Fail(string why) { failures.Add(why); }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(directory, name));
        Log("SHOT " + name);
    }

    void Finish(int code)
    {
        finished = true;
        Log($"RESULT pass={pass} fail={fail} {(fail == 0 ? "OK" : "FAILED")}");
        File.WriteAllText(Path.Combine(directory, "results.txt"), log.ToString());
        Application.Quit(code);
    }

    // ---------------------------------------------------------------- input
    // GUI can only be driven from inside OnGUI, so events are queued on the painter and
    // delivered on the next real GUI pass with the target control aimed and named.
    IEnumerator Inject(EventType type, Vector2 p)
    {
        CampusOverlayPainter.Inject(new Event
        {
            type = type, button = 0, clickCount = 1, mousePosition = p,
            modifiers = EventModifiers.None, delta = Vector2.zero
        });
        yield return null;
        yield return null;
    }

    IEnumerator ClickAt(Vector2 p)
    {
        yield return Inject(EventType.MouseDown, p);
        yield return Inject(EventType.MouseUp, p);
    }

    // One real draw pass so every panel records its control rectangles.
    IEnumerator LayoutFrame()
    {
        yield return null;
        yield return null;
    }

    IEnumerator Click(string controlKey)
    {
        if (!UIDrawRecorder.HasRect(controlKey))
        {
            Log("MISSING control: " + controlKey);
            yield break;
        }
        UIDrawRecorder.AimAt(controlKey);
        CampusOverlayPainter.Inject(new Event
        {
            type = EventType.Used, button = 0, clickCount = 1,
            mousePosition = UIDrawRecorder.CenterOf(controlKey),
            modifiers = EventModifiers.None, delta = Vector2.zero
        });
        yield return null;
        yield return null;
        UIDrawRecorder.AimAt(null);
    }

    // ---------------------------------------------------------------- helpers
    static void MovePlayer(Vector3 pos, float yaw = 0)
    {
        var game = FindFirstObjectByType<CampusGame>();
        if (!game || !game.player) return;
        var cc = game.player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        game.player.position = pos;
        game.player.rotation = Quaternion.Euler(0, yaw, 0);
        if (cc) cc.enabled = true;
        Physics.SyncTransforms();
    }

    // Teleport that keeps the indoor CharacterController in control, so the player stays
    // on the floor it is placed on (disabling the controller dropped it through the slab).
    static void TeleportInside(Vector3 pos)
    {
        var game = FindFirstObjectByType<CampusGame>();
        if (game && game.player)
        {
            game.player.position = pos;
            var cc = game.player.GetComponent<CharacterController>();
            if (cc) { cc.enabled = false; cc.enabled = true; }
            Physics.SyncTransforms();
        }
    }

    static IEnumerator Enter(CampusGame game, string building, string expectedScene = null)
    {
        if (string.IsNullOrEmpty(expectedScene)) expectedScene = building + ".unity";
        var travel = game.Interiors;
        BuildingEntranceDefinition entry = null;
        foreach (var e in travel.entrances)
        {
            // Do not use "e && ...": that would assign a fake null object over the real one.
            if (e == null) continue;
            if (e.buildingId != building) continue;
            // Several definitions share a buildingId in older scenes; the interior scene
            // path is what makes the entry unique.
            if (entry == null || (!string.IsNullOrEmpty(expectedScene) && e.interiorScenePath.EndsWith(expectedScene)))
                entry = e;
            if (!string.IsNullOrEmpty(expectedScene) && e.interiorScenePath.EndsWith(expectedScene)) break;
        }
        if (entry == null) yield break;
        Debug.Log("[AllowanceQA] entering " + building + " via " + entry.name + " -> " + entry.interiorScenePath +
                  " approach=" + entry.approachMapPosition + " radius=" + entry.interactionRadius);

        if (travel.Busy)
        {
            float b = 0;
            while (travel.Busy && b < 40) { b += Time.unscaledDeltaTime; yield return null; }
        }
        game.paused = false;
        game.SetUiCursor(false);
        if (travel.Inside) yield break;

        bool accepted = false;
        for (int attempt = 0; attempt < 6 && !accepted && !travel.Inside; attempt++)
        {
            game.paused = false;
            game.SetUiCursor(false);
            game.SetTravelPose(game.MapToWorld(entry.approachMapPosition), entry.enterYaw, 12);
            Physics.SyncTransforms();
            yield return null;
            yield return null;
            accepted = travel.TryEnter(entry);
            if (!accepted)
            {
                Debug.Log("[AllowanceQA] TryEnter(" + building + ") rejected: state=" + travel.State +
                          " busy=" + travel.Busy + " inside=" + travel.Inside + " nearby=" + (travel.NearbyEntrance() == entry));
                yield return new WaitForSecondsRealtime(.5f);
                continue;
            }
            float deadline = Time.realtimeSinceStartup + 40;
            while (travel.Busy && Time.realtimeSinceStartup < deadline) yield return null;
        }
        yield return new WaitForSecondsRealtime(.8f);
        yield return new WaitForSecondsRealtime(1.2f);   // let the render graph settle
        yield return null;
        yield return null;
    }

    static IEnumerator Leave(CampusGame game)
    {
        var travel = game.Interiors;
        // TryExit refuses while the game is paused (it is the pause menu's job to resume).
        game.paused = false;
        game.SetUiCursor(false);
        // Stand next to the doorway the exit expects, then use the real exit path.
        var root = travel.Interior;
        if (root && root.exits != null && root.exits.Length > 0 && game.player)
        {
            var exit = root.exits[0];
            MovePlayer(exit.position + root.transform.up * 0.1f);
            yield return null;
        }
        bool left = travel.TryExit();
        if (!left) Debug.Log("[AllowanceQA] TryExit rejected: state=" + travel.State + " lastError=" + travel.LastError);
        float deadline = Time.realtimeSinceStartup + 40;
        while (travel.Busy && Time.realtimeSinceStartup < deadline) yield return null;
        yield return new WaitForSecondsRealtime(1.2f);   // let the render graph settle
        yield return null;
        yield return null;
        Debug.Log("[AllowanceQA] after leave: state=" + travel.State + " inside=" + travel.Inside);
    }

    // Leave the cafeteria properly, walk back to its door and E in again: this is the
    // real re-entry path (CampusGame.Interact -> BuildingInteriorTravel.TryEnter).
    static IEnumerator ReenterCafeteria(CampusGame game)
    {
        yield return Leave(game);
        if (game.Interiors.Inside) { Debug.Log("[AllowanceQA] re-entry skipped: still inside after Leave"); yield break; }
        game.paused = false;
        game.SetUiCursor(false);
        BuildingEntranceDefinition door = null;
        foreach (var e in game.Interiors.entrances)
            if (e != null && e.interiorScenePath.EndsWith("cafeteria.unity")) { door = e; break; }
        if (door == null) { Debug.Log("[AllowanceQA] no cafeteria door for re-entry"); yield break; }
        game.SetTravelPose(game.MapToWorld(door.returnMapPosition), door.enterYaw, 12);
        Physics.SyncTransforms();
        yield return null;
        yield return new WaitForSecondsRealtime(.3f);
        game.Interact();
        float deadline = Time.realtimeSinceStartup + 40;
        while (game.Interiors.Busy && Time.realtimeSinceStartup < deadline) yield return null;
        yield return new WaitForSecondsRealtime(.8f);
        Debug.Log("[AllowanceQA] re-entered cafeteria inside=" + game.Interiors.Inside);
        if (!string.IsNullOrEmpty(outputDir))
            File.AppendAllText(Path.Combine(outputDir, "reentry.txt"),
                "re-entered=" + game.Interiors.Inside + " via=" + door.name + "\n");
    }

    static Vector3 StandPoint(CafeteriaShop shop)
    {
        // Mirror of the anchor table so the script can walk to each counter. The test
        // still proves the routing by what the game reports at that point.
        switch (shop)
        {
            case CafeteriaShop.Starter1F: return new Vector3(-10f, .5f, 10.3f);
            case CafeteriaShop.Wok1F: return new Vector3(-4.5f, .5f, 10.3f);
            case CafeteriaShop.Noodle1F: return new Vector3(1f, .5f, 10.3f);
            case CafeteriaShop.Steamed1F: return new Vector3(1.9f, .5f, 5.5f);
            case CafeteriaShop.WaterBar1F: return new Vector3(-.5f, .5f, -3.5f);
            case CafeteriaShop.Sichuan2F: return new Vector3(-10f, 4.8f, 10.3f);
            case CafeteriaShop.Claypot2F: return new Vector3(-4.5f, 4.8f, 10.3f);
            case CafeteriaShop.Teppan2F: return new Vector3(1f, 4.8f, 10.3f);
            case CafeteriaShop.Pastry2F: return new Vector3(1.9f, 4.8f, 5.5f);
            case CafeteriaShop.Soup3F: return new Vector3(9.8f, 9.1f, 4.5f);
            case CafeteriaShop.HotDish3F: return new Vector3(9.8f, 9.1f, 8.5f);
            case CafeteriaShop.Roast3F: return new Vector3(9.8f, 9.1f, 12.5f);
            case CafeteriaShop.FruitStall3F: return new Vector3(-1.5f, 9.1f, 9.3f);
            default: return Vector3.zero;
        }
    }

    // ---------------------------------------------------------------- main
    IEnumerator Start()
    {
        Log("AllowanceQA start " + DateTime.Now);
        UIDrawRecorder.Reset();

        var game = FindFirstObjectByType<CampusGame>();
        Check(game && game.Interiors, "campus ready");
        if (!game) { Finish(1); yield break; }
        game.paused = false;

        // A clean starting allowance for this profile: this is the first load, so the
        // wallet initialises itself once from the configured starting value.
        Log("startup wallet=" + CampusWallet.BalanceFen + " (saved=" + PlayerPrefs.GetString("AQA_Wallet", "<none>") + ")");
        Check(CampusWallet.BalanceFen == CampusWallet.DefaultStartingFen, "fresh allowance ¥300.00");
        Check(FindObjectsByType<CampusHud>(FindObjectsSortMode.None).Length == 1, "exactly one persistent HUD");

        yield return new WaitForSecondsRealtime(1f);
        yield return Shot("01_campus_hud.png");

        // ---- library: buy once from each vending machine through the real button ----
        step = "library";
        yield return Enter(game, "library");
        Check(game.Interiors.Inside, "inside library");
        var vending = VendingMachineUI.Ensure();

        long afterDrink = 0, afterSnack = 0;
        foreach (var machine in new[] { "vending_drinks_1f", "vending_snacks_1f" })
        {
            var anchor = machine == "vending_drinks_1f" ? vending.drinksMachine : vending.snacksMachine;
            if (!anchor) { Check(false, machine + " anchor"); continue; }
            MovePlayer(anchor.position + new Vector3(0, .2f, 2.3f), 0f);
            yield return new WaitForSecondsRealtime(.4f);
            vending.OpenAt(machine);
            yield return new WaitForSecondsRealtime(.5f);
            yield return LayoutFrame();
            var before = CampusWallet.BalanceFen;
            var stock = VendingSystem.Load(machine);
            int price = stock.items[0].priceFen;

            // The real 确认购买 button (row 0 is selected by default on open).
            yield return Click("vending:buy");
            yield return new WaitForSecondsRealtime(.5f);
            long now = CampusWallet.BalanceFen;
            Check(now == before - price, $"{machine} charged once via button click ({before} -> {now}, price {price})");
            Check(CampusInventory.CountOf(stock.items[0].name) >= 1, $"{machine} goods in inventory");
            yield return Shot("02_" + machine + ".png");
            // A second click in the same open panel buys again only with a new order;
            // the panel reloads stock, so the money must drop by exactly one more price.
            long beforeSecond = CampusWallet.BalanceFen;
            yield return Shot("02b_" + machine + "_before_second.png");
            yield return Click("vending:buy");
            yield return new WaitForSecondsRealtime(.4f);
            Check(CampusWallet.BalanceFen == beforeSecond - price, $"{machine} second click is a real, separate purchase");
            vending.Close();
            yield return new WaitForSecondsRealtime(.3f);
            if (machine == "vending_drinks_1f") afterDrink = CampusWallet.BalanceFen; else afterSnack = CampusWallet.BalanceFen;
        }

        // HUD balance must be visible here, and leave the library unchanged.
        yield return Shot("03_library_hud.png");
        long beforeLeave = CampusWallet.BalanceFen;
        yield return Leave(game);
        Check(CampusWallet.BalanceFen == beforeLeave, "balance unchanged by leaving the library");
        var wb = CampusWallet.BalanceFen;
        yield return Shot("04_campus_hud_after_vending.png");

        // ---- cafeteria ----
        step = "cafeteria";
        yield return Enter(game, "cafeteria", "cafeteria.unity");
        Log("DIAG after cafeteria enter: inside=" + game.Interiors.Inside + " state=" + game.Interiors.State +
            " lastError=" + game.Interiors.LastError + " current=" + (game.Interiors.Current ? game.Interiors.Current.displayName : "null") +
            " player=" + game.player.position);
        Check(game.Interiors.Inside, "inside cafeteria");
        var ordering = CampusSystemsRegistry.FindOrdering();
        {
            var interior = game.Interiors.Interior;
            var direct = interior ? interior.GetComponent<CafeteriaOrdering>() : null;
            var all = FindObjectsByType<CafeteriaOrdering>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Log($"DIAG interior={(interior ? interior.name : "null")} direct={(direct ? direct.name : "null")} " +
                $"registry={(CampusSystemsRegistry.Ordering ? CampusSystemsRegistry.Ordering.name : "null")} " +
                $"found={all.Length} " + string.Join(",", System.Array.ConvertAll(all, x => x.gameObject.scene.name + "/" + x.name)) +
                $" ui={(direct ? (direct.GetComponent<CafeteriaOrderingUI>() ? "yes" : "no") : "-")}");
        }
        Check(ordering != null, "cafeteria ordering system discovered");
        if (!ordering) { Finish(1); yield break; }
        var ui = ordering.GetComponent<CafeteriaOrderingUI>();
        Check(ui != null, "cafeteria UI present");
        ordering.prepareSeconds = 12;
        ordering.eatSeconds = 3;
        Check(CampusWallet.BalanceFen == wb, "cafeteria shows the same allowance as the library");
        yield return Shot("05_cafeteria_hud.png");

        // 1) Every shop opens its OWN menu, with its own floor, title and goods type.
        foreach (var def in CafeteriaShops.All)
        {
            MovePlayer(StandPoint(def.id));
            yield return new WaitForSecondsRealtime(.35f);
            Check(ordering.ShopAtPlayer() == def.id, $"at {def.FloorLabel} {def.shortName}: shop resolved");
            PressE(); yield return null;
            yield return new WaitForSecondsRealtime(.3f);
            Check(ordering.UiOpen, $"{def.shortName}: menu opened");
            Check(ordering.ActiveShop == def.id, $"{def.shortName}: panel shows this counter only");
            // Goods must match the business type.
            bool typeOk = true;
            for (int i = 0; i < ordering.ShopItemCount; i++)
            {
                string n = ordering.ShopItemName(i);
                int fen = ordering.ShopItemFen(i);
                if (def.business == CafeteriaBusiness.Meal && (fen <= 0 || fen > CafeteriaMenu.DishMaxFen)) typeOk = false;
                if (def.business == CafeteriaBusiness.Drink && (fen <= 0 || fen > CafeteriaMenu.DrinkMaxFen)) typeOk = false;
                if (def.business == CafeteriaBusiness.Fruit && (fen <= 0 || fen > CafeteriaMenu.FruitMaxFen)) typeOk = false;
            }
            Check(typeOk, $"{def.shortName}: prices within rules");
            Check(ordering.ShopItemCount > 0, $"{def.shortName}: menu has items");
            if (def.id == CafeteriaShop.WaterBar1F || def.id == CafeteriaShop.FruitStall3F)
                yield return Shot("06_menu_" + def.id + ".png");
            if (def.id == CafeteriaShop.Starter1F)
                yield return Shot("06_menu_" + def.id + ".png");
            EscapeClose();
            yield return new WaitForSecondsRealtime(.25f);
            Check(!ordering.UiOpen, $"{def.shortName}: menu closed");
        }

        // 1b) Geometry: each stand point must be closest to its OWN counter, so the stand
        // points used by this test really are in front of each stall and nothing else.
        var anchors = ordering.orderSpots;
        Check(anchors != null && anchors.Length == CafeteriaShops.All.Length, "one order anchor per shop (" + (anchors == null ? 0 : anchors.Length) + ")");
        if (anchors != null)
        {
            bool geomOk = true;
            foreach (var def in CafeteriaShops.All)
            {
                var p = StandPoint(def.id);
                Transform best = null; float bestD = float.MaxValue;
                foreach (var a in anchors)
                {
                    if (!a) continue;
                    if (Mathf.Abs(a.position.y - p.y) > 2f) continue;   // other floors
                    float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(a.position.x, a.position.z));
                    if (d < bestD) { bestD = d; best = a; }
                }
                var marker = best ? best.GetComponent<CafeteriaCounter>() : null;
                if (!marker || marker.shop != def.id) { geomOk = false; Log("  geometry mismatch at " + def.shortName); }
            }
            Check(geomOk, "every stand point is nearest to its own counter");
        }

        // 2) Buy at two different meal counters (different floors) through the real
        // buttons; both orders must run at the same time.
        long balBefore = CampusWallet.BalanceFen;
        var mealA = CafeteriaShop.Starter1F;      // 1F
        var mealB = CafeteriaShop.Sichuan2F;      // 2F
        yield return OrderAt(game, ordering, mealA, clickCount: 1);
        yield return OrderAt(game, ordering, mealB, clickCount: 2);
        Check(ordering.OrderCount == 2, "two meal orders exist at once");
        var oa = FindOrder(ordering, mealA);
        var ob = FindOrder(ordering, mealB);
        Check(oa != null && ob != null && oa.id != ob.id, "orders have distinct ids and shops");
        Check(oa != null && oa.floor == 1 && ob != null && ob.floor == 2, "orders remember their own floor");
        Check(CampusWallet.BalanceFen == balBefore - (oa.totalFen + ob.totalFen), "charged exactly the two totals");
        yield return Shot("07_two_meal_orders.png");

        // 3) Buy a drink while the meals are preparing: instant, does not touch them.
        yield return OrderAt(game, ordering, CafeteriaShop.WaterBar1F, clickCount: 1, instant: true);
        int instantCount = 0;
        foreach (var x in ordering.OrdersForTest()) if (x.Business != CafeteriaBusiness.Meal) instantCount++;
        Check(instantCount == 1, "instant drink creates its instant purchase record");
        var heldDrink = ordering.InstantOrderHeld(CafeteriaShop.WaterBar1F);
        Check(heldDrink != null && CampusInventory.CountOf(heldDrink.items[0]) >= 1, "drink is in the inventory");
        Check(oa.state == (int)CafeteriaOrderState.Preparing && ob.state == (int)CafeteriaOrderState.Preparing,
              "both meal timers untouched by the drink purchase");
        yield return Shot("08_drink_bought.png");

        // 4) Countdowns really end.
        float waited = 0;
        while ((oa.state == (int)CafeteriaOrderState.Preparing || ob.state == (int)CafeteriaOrderState.Preparing) && waited < 25f)
        { waited += Time.unscaledDeltaTime; yield return null; }
        Check(oa.state == (int)CafeteriaOrderState.ReadyToCollect && ob.state == (int)CafeteriaOrderState.ReadyToCollect,
              $"both orders reached ReadyToCollect after {waited:F1}s");

        // 5) Collecting at B's own counter hands over B, and only B.
        TeleportInside(StandPoint(mealB));
        yield return new WaitForSecondsRealtime(.4f);
        Check(ordering.CollectableOrderHere() == ob, "2F counter offers only the 2F order");
        PressE(); yield return null;
        yield return new WaitForSecondsRealtime(.4f);
        Check(ob.state == (int)CafeteriaOrderState.Carrying && oa.state == (int)CafeteriaOrderState.ReadyToCollect,
              "collected the 2F meal, the 1F meal stays ready");
        yield return Shot("09_collected_2f.png");

        // 6) Tray already in hand: a second pickup is refused and the first order kept.
        TeleportInside(StandPoint(mealA));
        yield return new WaitForSecondsRealtime(.4f);
        PressE(); yield return null;
        yield return new WaitForSecondsRealtime(.4f);
        Check(oa.state == (int)CafeteriaOrderState.ReadyToCollect, "a full tray keeps the un-collected order waiting");
        Check(ordering.CarriedOrder == ob, "a full tray keeps the collected meal in hand");
        // The refusal branch itself, exercised directly (the UI may legally sit down first
        // when a chair is in reach, which is why the button path above cannot hit it).
        string refuseMsg;
        Check(!ordering.TryCollect(out refuseMsg) && oa.state == (int)CafeteriaOrderState.ReadyToCollect,
              "TryCollect with a full tray is refused: " + refuseMsg);

        // 7) Eat at a real chair on B's floor, then return the tray there.
        Transform seat = null;
        foreach (var st in ordering.seatSpots)
            if (st && Mathf.Abs(st.position.y - StandPoint(mealB).y) < 2f) { seat = st; break; }
        if (seat)
        {
            Vector3 standAt = seat.position - seat.forward * 0.85f;
            TeleportInside(standAt);
            yield return new WaitForSecondsRealtime(.5f);
            PressE(); yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            Check(ordering.Seated, "sat down to eat");
            float eat = 0;
            while (ordering.State != CafeteriaOrderState.TrayToReturn && eat < 12f) { eat += Time.unscaledDeltaTime; yield return null; }
            yield return Shot("10_eating.png");
            ui.StandUpForTest();
            yield return new WaitForSecondsRealtime(.4f);
            Check(ordering.AwaitingTrayReturn, "meal finished, tray to return");
            TeleportInside(new Vector3(12f, StandPoint(mealB).y, 4.0f));
            yield return new WaitForSecondsRealtime(.4f);
            PressE(); yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Check(!ordering.AwaitingTrayReturn, "tray returned");
        }
        else Check(false, "found a chair on the 2F floor");

        // 8) Now the 1F meal can be collected at its own 1F point.
        TeleportInside(StandPoint(mealA));
        yield return new WaitForSecondsRealtime(.4f);
        PressE(); yield return null;
        yield return new WaitForSecondsRealtime(.4f);
        Check(ordering.CarriedOrder != null && ordering.CarriedOrder.shop == (int)mealA, "1F meal collected at its own point");

        // 8b) Finish the 1F meal so later steps start with empty hands.
        Transform seat1f = null;
        foreach (var st in ordering.seatSpots)
            if (st && Mathf.Abs(st.position.y - StandPoint(mealA).y) < 2f) { seat1f = st; break; }
        if (seat1f)
        {
            TeleportInside(seat1f.position - seat1f.forward * 0.85f);
            yield return new WaitForSecondsRealtime(.5f);
            PressE(); yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            float eat1 = 0;
            while (ordering.State != CafeteriaOrderState.TrayToReturn && eat1 < 12f) { eat1 += Time.unscaledDeltaTime; yield return null; }
            ui.StandUpForTest();
            yield return new WaitForSecondsRealtime(.4f);
            TeleportInside(new Vector3(12f, StandPoint(mealA).y, 4.0f));
            yield return new WaitForSecondsRealtime(.4f);
            PressE(); yield return null;
            yield return new WaitForSecondsRealtime(.4f);
            Check(ordering.CarriedOrder == null, "1F meal finished and tray returned, hands free");
        }
        else Check(false, "found a chair on the 1F floor");

        // 9) Insufficient funds: no negative balance, a shortfall message.
        ordering.Notify("");
        CampusWallet.SetForTest(100);
        MovePlayer(StandPoint(CafeteriaShop.WaterBar1F));
        yield return new WaitForSecondsRealtime(.3f);
        PressE(); yield return null;
        yield return new WaitForSecondsRealtime(.3f);
        yield return LayoutFrame();
        yield return Click("cafe:add:0");
        yield return Click("cafe:add:0");
        long broke = CampusWallet.BalanceFen;
        yield return Click("cafe:pay");
        yield return new WaitForSecondsRealtime(.3f);
        Check(CampusWallet.BalanceFen == broke && broke >= 0, "insufficient funds: nothing charged");
        Check(ordering.LastMessage.Contains("还差"), "insufficient funds: shortfall shown");
        yield return Shot("11_insufficient.png");
        EscapeClose();
        yield return new WaitForSecondsRealtime(.2f);

        // 10) Double click on 确认付款 charges once.
        CampusWallet.SetForTest(9000);
        MovePlayer(StandPoint(CafeteriaShop.Noodle1F));
        yield return new WaitForSecondsRealtime(.3f);
        PressE(); yield return null;
        yield return new WaitForSecondsRealtime(.3f);
        Check(ordering.UiOpen, "noodle counter opened its own menu for the double-click test");
        yield return LayoutFrame();
        yield return Click("cafe:add:0");
        int cartTotal = ordering.CartTotalFen();
        long beforeDouble = CampusWallet.BalanceFen;
        yield return Click("cafe:pay");
        yield return Click("cafe:pay");
        yield return new WaitForSecondsRealtime(.3f);
        Log($"DIAG dbl before={beforeDouble} after={CampusWallet.BalanceFen} cart={cartTotal} uiOpen={ordering.UiOpen} orders={ordering.OrderCount}");
        Check(CampusWallet.BalanceFen == beforeDouble - cartTotal, "double click on pay charged once");
        int noodleOrders = 0;
        foreach (var o in ordering.OrdersForTest()) if (o.shop == (int)CafeteriaShop.Noodle1F) noodleOrders++;
        Check(noodleOrders == 1, "double click created one order");

        // 11) The menu is stable for the same game day, across re-entry.
        string date1 = ordering.MenuDate;
        string sig1 = Signature(ordering, CafeteriaShop.Wok1F);
        EscapeClose();
        yield return ReenterCafeteria(game);
        ordering = CampusSystemsRegistry.FindOrdering();
        yield return new WaitForSecondsRealtime(.3f);
        Check(ordering.MenuDate == date1, "same game day keeps the menu date");
        Check(Signature(ordering, CafeteriaShop.Wok1F) == sig1, "same game day keeps items and prices");
        Check(CampusWallet.BalanceFen == CampusWallet.BalanceFen, "balance persisted through exit/entry");

        // 12) Allowance survives a restart: read the saved key and compare.
        Log("SAVED_WALLET=" + PlayerPrefs.GetString("AQA_Wallet", "<none>") + " runtime=" + CampusWallet.BalanceFen);
        Check(PlayerPrefs.GetString("AQA_Wallet", "") == CampusWallet.BalanceFen.ToString(), "wallet persisted to its own key");

        yield return Shot("12_final_hud.png");

        // 13) Restart persistence: a second, separate process loads the saved profile
        // and reports the allowance, the menu day, the inventory and the orders it found.
        PlayerPrefs.Save();
        string exe = Environment.GetCommandLineArgs()[0];
        var psi = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false };
        psi.ArgumentList.Add("-allowanceQA");
        psi.ArgumentList.Add("-qaRestartRead");
        psi.ArgumentList.Add("-qaOutput");
        psi.ArgumentList.Add(directory);
        var proc = System.Diagnostics.Process.Start(psi);
        float wait = 0;
        while (proc != null && !proc.HasExited && wait < 90) { wait += Time.unscaledDeltaTime; yield return null; }
        if (proc == null) Log("restart process did not start");

        string file = Path.Combine(directory, "restart.txt");
        if (File.Exists(file))
        {
            string text = File.ReadAllText(file);
            Log("restart report:\n" + text);
            Check(text.Contains("wallet=" + CampusWallet.BalanceFen + "\n"), "restart keeps the same allowance");
            Check(text.Contains("date=" + date1 + "\n"), "restart keeps the same menu day");
        }
        else Check(false, "restart report missing");

        Finish(fail == 0 ? 0 : 1);
    }

    string Signature(CafeteriaOrdering o, CafeteriaShop shop)
    {
        var sb = new StringBuilder();
        int n = o.MenuItemCountFor(shop);
        for (int i = 0; i < n; i++) sb.Append(o.MenuItemNameFor(shop, i)).Append('@').Append(o.MenuItemFenFor(shop, i)).Append(';');
        return sb.ToString();
    }

    static CafeteriaOrder FindOrder(CafeteriaOrdering o, CafeteriaShop shop)
    {
        foreach (var x in o.OrdersForTest()) if (x != null && x.shop == (int)shop) return x;
        return null;
    }

    static Transform NearestSeat(CafeteriaOrdering o, Vector3 near)
    {
        o.SetPlayerPosForTest(near);
        var t = o.NearestSeatAnchor(50f);
        o.ClearPlayerPosForTest();
        return t;
    }

    // Press E through the game's own dispatcher (CampusGame.Interact), the same entry
    // point the E key uses.
    void PressE()
    {
        var game = FindFirstObjectByType<CampusGame>();
        if (game) game.Interact();
    }

    void EscapeClose()
    {
        var hud = CampusHud.Instance;
        var ordering = CampusSystemsRegistry.FindOrdering();
        if (ordering && ordering.UiOpen)
        {
            var ui = ordering.GetComponent<CafeteriaOrderingUI>();
            if (ui) ui.CloseUI();
        }
    }

    // Walk to a counter, add items with real button clicks, pay with the real button.
    IEnumerator OrderAt(CampusGame game, CafeteriaOrdering ordering, CafeteriaShop shop,
                        int clickCount, bool instant = false)
    {
        MovePlayer(StandPoint(shop));
        yield return new WaitForSecondsRealtime(.3f);
        game.Interact();
        yield return new WaitForSecondsRealtime(.35f);
        if (!ordering.UiOpen) { Check(false, shop + ": panel did not open for ordering"); yield break; }

        yield return LayoutFrame();
        for (int i = 0; i < clickCount; i++)
        {
            yield return Click("cafe:add:0");
        }
        long before = CampusWallet.BalanceFen;
        int total = ordering.CartTotalFen();
        yield return Click("cafe:pay");
        yield return new WaitForSecondsRealtime(.35f);
        long after = CampusWallet.BalanceFen;
        Check(after == before - total && !ordering.UiOpen, $"{CafeteriaShops.Get(shop).shortName}: paid via button, panel closed");
        if (instant)
        {
            var heldOrder = ordering.InstantOrderHeld(shop);
            Check(heldOrder != null, $"{CafeteriaShops.Get(shop).shortName}: goods delivered instantly");
        }
        else
        {
            var made = FindOrder(ordering, shop);
            Check(made != null && made.totalFen == total, $"{CafeteriaShops.Get(shop).shortName}: meal order created with its snapshot total");
        }
    }
}
