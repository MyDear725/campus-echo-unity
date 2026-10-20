using System.Collections;
using System.IO;
using UnityEngine;

// End-to-end Game-view verification of the cafeteria ordering flow. Only runs with
// -cafeteriaQA, so normal play is untouched.
//
// It uses the same entry points the player does (InteractOnce for E, StandUpNow for Q)
// and a real wall-clock countdown, then writes screenshots and a state log.
public class CafeteriaOrderingQA : MonoBehaviour
{
    static bool started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (started) return;
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-cafeteriaQA") < 0) return;
        started = true;
        var go = new GameObject("__cafeteriaOrderingQA");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<CafeteriaOrderingQA>();
    }

    string Dir => Path.Combine(Application.dataPath, "../QA/CafeteriaOrdering");

    IEnumerator Start()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, "progress.txt"), "RUNNING\n");
        var log = new System.Text.StringBuilder();

        yield return new WaitForSecondsRealtime(2f);

        var game = FindFirstObjectByType<CampusGame>();
        var travel = game ? game.GetComponent<BuildingInteriorTravel>() : null;
        if (!game || !travel) { Log(log, "FATAL missing CampusGame/travel"); yield break; }

        game.paused = false;
        game.UiBlocking = false;
        game.Seated = false;

        BuildingEntranceDefinition cafeteria = null;
        foreach (var e in travel.entrances)
            if (e && e.buildingId == "cafeteria") cafeteria = e;
        if (!cafeteria) { Log(log, "FATAL no cafeteria entrance"); yield break; }

        game.SetTravelPose(game.MapToWorld(cafeteria.approachMapPosition), cafeteria.enterYaw, 12f);
        Physics.SyncTransforms();
        yield return null;
        travel.TryEnter(cafeteria);
        for (int i = 0; i < 600 && travel.Busy; i++) yield return null;
        for (int i = 0; i < 60; i++) yield return null;
        if (!travel.Inside) { Log(log, "FATAL not inside: " + travel.LastError); yield break; }

        var ordering = travel.Interior.GetComponent<CafeteriaOrdering>();
        var ui = travel.Interior.GetComponent<CafeteriaOrderingUI>();
        if (!ordering || !ui) { Log(log, "FATAL missing ordering/ui"); yield break; }

        // Fresh order state on a throwaway key so the player's save is untouched.
        CafeteriaOrdering.PrefsKeyOverride = "CafeteriaOrdering__QA";
        PlayerPrefs.DeleteKey("CafeteriaOrdering__QA");
        ordering.SetBalanceForTest(30000);
        ordering.prepareSeconds = 6;
        ordering.eatSeconds = 6;

        // ---- 1. stand at the 2F counter and press E -> panel ----
        MovePlayer(game, new Vector3(-4f, 4.8f, 11.2f), 180f);
        yield return new WaitForSecondsRealtime(0.6f);
        bool opened = ui.InteractOnce();
        yield return new WaitForSecondsRealtime(0.6f);
        Log(log, $"openPanel={opened} uiOpen={ordering.UiOpen} title={ordering.ShopTitle} items={ordering.ShopItemCount}");
        Shot("01_panel.png");

        // ---- 2. pick food and pay ----
        ordering.CartAdd(0);
        ordering.CartAdd(1);
        long before = ordering.BalanceFen;
        int total = ordering.CartTotalFen();
        bool paid = ordering.TryPay();
        ordering.ClosePanel();
        yield return new WaitForSecondsRealtime(0.5f);
        Log(log, $"paid={paid} before={before} after={ordering.BalanceFen} total={total} state={ordering.State} status={ordering.LastMessage}");
        Shot("02_paid_countdown.png");

        // ---- 2b. order a drink at the 2F water bar while the meal is still preparing ----
        MovePlayer(game, new Vector3(-0.5f, 4.8f, -3.5f), 180f);
        yield return new WaitForSecondsRealtime(0.4f);
        bool openedWb = ui.InteractOnce();
        yield return new WaitForSecondsRealtime(0.3f);
        int instantBefore = ordering.OrderCount;
        ordering.CartAdd(0);
        bool paidWb = ordering.TryPay();
        ordering.ClosePanel();
        yield return new WaitForSecondsRealtime(0.4f);
        Log(log, $"waterBarOpen={openedWb} waterBarPaid={paidWb} mealOrdersIntact={ordering.OrderCount == instantBefore} orders={ordering.OrderCount} " +
                 $"shops=" + DescribeOrders(ordering));
        Shot("02b_two_orders.png");

        // back to the serving counter for the meal
        MovePlayer(game, new Vector3(-4f, 4.8f, 11.2f), 180f);
        yield return new WaitForSecondsRealtime(0.3f);

        // ---- 3. wait for the REAL countdown, then collect with E ----
        float waited = 0f;
        while (!ordering.CanCollect && waited < 15f) { waited += Time.unscaledDeltaTime; yield return null; }
        Log(log, $"countdown ended after {waited:F1}s state={ordering.State} canCollect={ordering.CanCollect} status={ordering.LastMessage}");
        Shot("03_ready_to_collect.png");


        // ---- 3b. the drink was delivered instantly; there is nothing to pick up and no
        // tray. The meal order must be untouched by that purchase.
        Log(log, $"drinkInstant inventory={CampusInventory.Summary()} orders=" + DescribeOrders(ordering));

        // back to the serving counter to pick the meal up at ITS OWN pickup point
        MovePlayer(game, new Vector3(-4.5f, 4.8f, 10.3f), 0f);
        yield return new WaitForSecondsRealtime(0.5f);
        bool collected = ui.InteractOnce();
        yield return new WaitForSecondsRealtime(0.4f);
        Log(log, $"mealCollect={collected} state={ordering.State} carrying={ordering.HasTray} " +
                 $"needsSeat={ordering.CarriedNeedsSeat} orders={ordering.OrderCount}");
        Shot("04_collected_tray.png");

        // ---- 4. walk to a real chair behind it, press E to sit ----
        var seat = NearestSeatTo(ordering, new Vector3(-7.3f, 4.8f, 5.0f));
        if (!seat) { Log(log, "FATAL no seat found"); yield break; }
        Vector3 standAt = seat.position - seat.forward * 0.85f;
        standAt.y = seat.position.y;
        MovePlayer(game, standAt, seat.eulerAngles.y);
        yield return new WaitForSecondsRealtime(0.5f);
        bool sat = ui.InteractOnce();
        yield return new WaitForSecondsRealtime(0.9f);
        Log(log, $"sit={sat} seated={ordering.Seated} state={ordering.State} seatPos={seat.position} " +
                 $"playerPos={game.player.position}");
        Shot("05_seated_start.png");

        // ---- 5. eat part way, stand up, sit again and finish ----
        yield return new WaitForSecondsRealtime(2f);
        float partial = ordering.EatProgress;
        ui.StandUpForTest();
        yield return new WaitForSecondsRealtime(0.4f);
        Log(log, $"midStandUp progress={ordering.EatProgress:F2} state={ordering.State} seated={ordering.Seated}");

        MovePlayer(game, standAt, seat.eulerAngles.y);
        yield return new WaitForSecondsRealtime(0.4f);
        bool resat = ui.InteractOnce();
        yield return new WaitForSecondsRealtime(0.4f);
        Log(log, $"resit={resat} progressKept={ordering.EatProgress:F2} (was {partial:F2}) state={ordering.State}");
        Shot("06_seated_resumed.png");

        float eatWaited = 0f;
        while (ordering.State != CafeteriaOrderState.TrayToReturn && eatWaited < 20f)
        { eatWaited += Time.unscaledDeltaTime; yield return null; }
        Log(log, $"meal finished progress={ordering.EatProgress:F2} state={ordering.State}");
        Shot("07_meal_done.png");

        // ---- 6. stand up; must stay TrayToReturn ----
        ui.StandUpForTest();
        yield return new WaitForSecondsRealtime(0.5f);
        Log(log, $"afterStandUp state={ordering.State} seated={ordering.Seated} carrying={ordering.HasTray}");
        Shot("08_stood_up_tray.png");

        // ---- 7. return the tray at the 2F return counter ----
        MovePlayer(game, new Vector3(12f, 4.8f, 4.0f), 0f);
        yield return new WaitForSecondsRealtime(0.5f);
        bool returned = ui.InteractOnce();
        yield return new WaitForSecondsRealtime(0.4f);
        Log(log, $"return={returned} state={ordering.State} balance={ordering.BalanceFen}");
        Shot("09_returned.png");

        // ---- 8. can order again ----
        Log(log, $"canOrderAgain={ordering.ShopAtPlayer() != null || ordering.NearReturnSpot}");

        File.WriteAllText(Path.Combine(Dir, "log.txt"), log.ToString());
        File.WriteAllText(Path.Combine(Dir, "progress.txt"), "DONE\n");
        Debug.Log("[CafeteriaQA] DONE\n" + log);
        yield return new WaitForSecondsRealtime(1f);
        PlayerPrefs.DeleteKey("CafeteriaOrdering__QA");
        CafeteriaOrdering.PrefsKeyOverride = null;
        Application.Quit(0);
    }

    static CafeteriaOrder FindShopOrder(CafeteriaOrdering o, CafeteriaShop shop)
    {
        for (int i = 0; i < o.OrderCount; i++)
        {
            var x = o.OrderAt(i);
            if (x != null && x.shop == (int)shop) return x;
        }
        return null;
    }

    static string DescribeOrders(CafeteriaOrdering o)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < o.OrderCount; i++)
        {
            var x = o.OrderAt(i);
            if (x == null) continue;
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append(x.FloorShopLabel).Append(":").Append((CafeteriaOrderState)x.state);
        }
        return sb.ToString();
    }

    static Transform NearestSeatTo(CafeteriaOrdering o, Vector3 near)
    {
        o.SetPlayerPosForTest(near);
        var t = o.NearestSeatAnchor(50f);
        o.ClearPlayerPosForTest();
        return t;
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

    void Shot(string name) { ScreenCapture.CaptureScreenshot(Path.Combine(Dir, name)); }

    void Log(System.Text.StringBuilder sb, string m) { sb.AppendLine(m); Debug.Log("[CafeteriaQA] " + m); }
}
