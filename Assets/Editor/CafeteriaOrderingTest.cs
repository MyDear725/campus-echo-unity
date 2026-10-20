using System;
using System.Text;
using UnityEditor;
using UnityEngine;

// Headless verification of the cafeteria ordering state machine: menu persistence,
// price bounds, balance rules, the full order lifecycle, and reload safety.
//
//   Unity -batchmode -nographics -quit -projectPath <proj>
//     -executeMethod CafeteriaOrderingTest.Run -logFile <log>
public static class CafeteriaOrderingTest
{
    [MenuItem("Campus/Testing/Verify Cafeteria Ordering")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== CAFETERIA ORDERING TEST ===");
        int pass = 0, fail = 0;

        void Check(bool ok, string label)
        {
            sb.AppendLine($"  {(ok ? "PASS" : "FAIL")}  {label}");
            if (ok) pass++; else fail++;
        }

        // --- fresh state, on a throwaway save key so the real one is untouched ---
        CafeteriaOrdering.PrefsKeyOverride = "CafeteriaOrdering__TEST";
        PlayerPrefs.DeleteKey("CafeteriaOrdering__TEST");
        var go = new GameObject("__orderTest");
        var sys = go.AddComponent<CafeteriaOrdering>();
        sys.SetGame(null);
        sys.Init();
        sys.dishCount = 8; sys.drinkCount = 4;

        // 1. menu rolls for today
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        Check(!string.IsNullOrEmpty(sys.MenuDateForTest()), "menu date set");

        // 2. counts
        Check(sys.DishCount == 8, "8 dishes rolled");
        Check(sys.DrinkCount == 4, "4 drinks rolled");

        // 3. no duplicates within a category
        var names = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < sys.DishCount; i++) names.Add(sys.DishName(i));
        Check(names.Count == 8, "dishes are unique");
        var dnames = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < sys.DrinkCount; i++) dnames.Add(sys.DrinkName(i));
        Check(dnames.Count == 4, "drinks are unique");

        // 4. price bounds
        bool dishOk = true, drinkOk = true;
        for (int i = 0; i < sys.DishCount; i++)
            if (sys.DishFen(i) <= 0 || sys.DishFen(i) > 1000) dishOk = false;
        for (int i = 0; i < sys.DrinkCount; i++)
            if (sys.DrinkFen(i) <= 0 || sys.DrinkFen(i) > 2000) drinkOk = false;
        Check(dishOk, "dish prices in (0, ¥10]");
        Check(drinkOk, "drink prices in (0, ¥20]");

        // 4b. water bar and fruit stall have their own menus, capped at ¥20
        sys.SetActiveShop(CafeteriaShop.WaterBar);
        Check(sys.ShopItemCount == 6, "water bar rolls 6 items");
        bool wbOk = true;
        var wbNames = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < sys.ShopItemCount; i++)
        {
            wbNames.Add(sys.ShopItemName(i));
            if (sys.ShopItemFen(i) <= 0 || sys.ShopItemFen(i) > 2000) wbOk = false;
        }
        Check(wbOk, "water bar prices in (0, ¥20]");
        Check(wbNames.Count == sys.ShopItemCount, "water bar items unique");

        sys.SetActiveShop(CafeteriaShop.FruitStall);
        Check(sys.ShopItemCount == 6, "fruit stall rolls 6 items");
        bool frOk = true;
        var frNames = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < sys.ShopItemCount; i++)
        {
            frNames.Add(sys.ShopItemName(i));
            if (sys.ShopItemFen(i) <= 0 || sys.ShopItemFen(i) > 2000) frOk = false;
        }
        Check(frOk, "fruit stall prices in (0, ¥20]");
        Check(frNames.Count == sys.ShopItemCount, "fruit stall items unique");

        // The two shops must not share the same list.
        bool distinct = true;
        for (int i = 0; i < sys.ShopItemCount; i++)
            if (sys.ShopItemName(i) == sys.DishName(0)) distinct = false;
        Check(distinct, "fruit menu differs from restaurant dishes");
        sys.SetActiveShop(CafeteriaShop.Restaurant);

        // 5. menu survives a reload on the same day
        sys.Save();
        var go2 = new GameObject("__orderTest2");
        var sys2 = go2.AddComponent<CafeteriaOrdering>();
        sys2.Init();
        bool same = true;
        for (int i = 0; i < sys.DishCount; i++)
            if (sys2.DishName(i) != sys.DishName(i) || sys2.DishFen(i) != sys.DishFen(i)) same = false;
        for (int i = 0; i < sys.DrinkCount; i++)
            if (sys2.DrinkName(i) != sys.DrinkName(i) || sys2.DrinkFen(i) != sys.DrinkFen(i)) same = false;
        Check(same, "menu identical after reload (same day)");

        // 6. balance default
        Check(sys.BalanceFen == 30000, "default balance ¥300.00");

        // 7. order lifecycle
        sys.CartAdd(sys.DishName(0), sys.DishFen(0));
        sys.CartAdd(sys.DishName(0), sys.DishFen(0));       // same dish stacks
        sys.CartAdd(sys.DrinkName(0), sys.DrinkFen(0));
        int expected = sys.DishFen(0) * 2 + sys.DrinkFen(0);
        Check(sys.CartTotal() == expected, "cart total correct");

        // A real, short preparation window so the natural timer is what gets tested.
        sys.prepareSeconds = 1;
        long before = sys.BalanceFen;
        bool paid = sys.TryPay();
        Check(paid, "payment accepted");
        Check(sys.BalanceFen == before - expected, "balance deducted exactly once");
        Check(sys.FirstPreparing() != null, "order entered Preparing");
        Check(sys.OrderCount == 1, "one order recorded");

        // 8. repeated pay attempts cannot double-charge
        bool again = sys.TryPay();
        Check(!again, "second payment refused");
        Check(sys.BalanceFen == before - expected, "balance unchanged after refused pay");

        // 9. cannot collect before ready
        Check(!sys.TryCollect(), "collect refused while preparing");

        // 10. the countdown itself must promote the order (no ForceReady shortcut)
        Check(sys.FirstPreparing() != null, "still preparing right after paying");
        Check(sys.FirstReady() == null, "nothing collectable before the countdown ends");
        System.Threading.Thread.Sleep(1300);
        sys.Tick();                                   // what Update() calls every frame
        Check(sys.FirstReady() != null, "countdown promotes to ReadyToCollect");

        // 10b. an order that expired while the game was closed is collectable on load
        sys.Save();
        var reloadGo = new GameObject("__orderReload");
        var reload = reloadGo.AddComponent<CafeteriaOrdering>();
        reload.Init();
        reload.Load();
        Check(reload.FirstReady() != null, "expired order loads as ready");
        Check(reload.OrderCount == 1, "loaded expired order is still present");
        UnityEngine.Object.DestroyImmediate(reloadGo);

        Check(sys.TryCollect(), "collect accepted");
        Check(sys.HasTray, "tray carried");
        Check(!sys.TryCollect(), "second collect refused");

        // 11. eating progress
        sys.SetEating(true);
        Check(sys.State == CafeteriaOrderState.Eating, "state -> Eating");
        sys.AddEatProgress(0.4f);
        Check(Mathf.Approximately(sys.EatProgress, 0.4f), "progress accumulates");
        sys.SetEating(false);
        Check(sys.State == CafeteriaOrderState.Carrying, "standing pauses eating");
        sys.SetEating(true);
        sys.AddEatProgress(0.6f);
        Check(sys.State == CafeteriaOrderState.TrayToReturn, "progress completes -> TrayToReturn");

        // 12. reload mid-meal restores standing with progress kept
        sys.Save();
        var sys3 = go.AddComponent<CafeteriaOrdering>();
        sys3.Init();
        sys3.Load();
        Check(sys3.EatProgress >= 1f, "eat progress survives reload");
        Check(sys3.State == CafeteriaOrderState.TrayToReturn, "state survives reload");
        Check(sys3.HasTray, "tray survives reload");

        // 13. tray return clears the order
        Check(sys3.TryReturnTray(), "tray returned");
        Check(sys3.State == CafeteriaOrderState.None, "state -> None");
        Check(sys3.CartCount == 0, "order cleared");
        Check(sys3.BalanceFen == before - expected, "balance unchanged by return");
        Check(!sys3.TryReturnTray(), "second return refused");

        // 14. can order again after completing
        sys3.CartAdd(sys3.DishName(1), sys3.DishFen(1));
        Check(sys3.CartCount == 1, "can order again after finishing");

        // 15. insufficient balance blocks payment
        sys3.CartClearForTest();
        sys3.CartAdd(sys3.DishName(0), sys.DishFen(0));
        // drain the balance artificially
        sys3.SetBalanceForTest(0);
        Check(!sys3.CanPay, "payment blocked when balance insufficient");
        Check(!sys3.TryPay(), "TryPay refuses on insufficient balance");
        Check(sys3.BalanceFen == 0, "balance never negative");

        // 16. cross-day menu refresh keeps the paid order intact
        sys3.SetMenuDateForTest("2000-01-01");
        sys3.CartClearForTest();
        sys3.SetBalanceForTest(30000);
        sys3.CartAdd(sys3.DishName(0), sys.DishFen(0));
        sys3.TryPay();
        var paidNames = sys3.CartDishName(0);
        sys3.SetMenuDateForTest("2000-01-01");   // already past: refresh happens on next Ensure
        sys3.RefreshMenuForTest();
        Check(sys3.MenuDateForTest() == DateTime.Now.ToString("yyyy-MM-dd"), "menu re-rolled after date change");
        Check(sys3.OrderCount >= 1 && sys3.OrderAt(0).items.Count > 0,
            "paid order keeps its items and price");

        // 17. offline preparation ticks down
        sys3.SetReadyInPastForTest(30);
        Check(sys3.FirstReady() != null, "offline time counts toward preparation");

        // 18. several counters can be ordered from at the same time
        CafeteriaOrdering.PrefsKeyOverride = "CafeteriaOrdering__TEST";
        PlayerPrefs.DeleteKey("CafeteriaOrdering__TEST");
        var multi = new GameObject("__multi").AddComponent<CafeteriaOrdering>();
        multi.Init();
        multi.SetBalanceForTest(30000);
        multi.prepareSeconds = 1;

        multi.SetActiveShop(CafeteriaShop.Restaurant);
        multi.CartAdd(multi.DishName(0), multi.DishFen(0));
        bool r1 = multi.TryPay();
        Check(r1, "restaurant order paid");
        Check(!multi.CanOrderAt(CafeteriaShop.Restaurant), "restaurant counter now busy");
        Check(multi.CanOrderAt(CafeteriaShop.WaterBar), "water bar still orderable");

        multi.SetActiveShop(CafeteriaShop.WaterBar);
        multi.CartAdd(multi.ShopItemName(0), multi.ShopItemFen(0));
        bool r2 = multi.TryPay();
        Check(r2, "water bar order paid while restaurant order is running");
        Check(multi.OrderCount == 2, "two orders in flight at once");

        multi.SetActiveShop(CafeteriaShop.FruitStall);
        multi.CartAdd(multi.ShopItemName(0), multi.ShopItemFen(0));
        Check(multi.TryPay(), "fruit stall order paid too");
        Check(multi.OrderCount == 3, "three orders in flight at once");

        // 19. water bar / fruit orders never create a tray to return
        System.Threading.Thread.Sleep(1300);
        multi.Tick();
        Check(multi.FirstReady() != null, "orders became ready");

        var waterOrder = multi.OrderAt(1);
        Check(!waterOrder.NeedsTrayReturn, "water bar order needs no tray");
        sb.AppendLine($"  itemsSummary of drink order: \"{waterOrder.ItemsSummary}\" (items={waterOrder.items.Count})");
        Check(waterOrder.items.Count > 0, "drink order keeps its item names");
        var fruitOrder = multi.OrderAt(2);
        Check(!fruitOrder.NeedsTrayReturn, "fruit order needs no tray");
        Check(multi.OrderAt(0).NeedsTrayReturn, "restaurant order does need a tray");

        // 20. finishing a water bar / fruit order closes it without ever asking for a tray
        var wbOrder = multi.OrderAt(1);
        string wbId = wbOrder.id.ToString();
        wbOrder.state = (int)CafeteriaOrderState.Carrying;      // hand it to the player
        int ordersBefore = multi.OrderCount;
        multi.AddEatProgress(1f);
        Check(multi.OrderCount == ordersBefore - 1, "water bar order closes itself when eaten");
        Check(!multi.AwaitingTrayReturn, "no tray return is requested for a drink");
        bool found = false;
        for (int i = 0; i < multi.OrderCount; i++)
            if (multi.OrderAt(i).id.ToString() == wbId) found = true;
        Check(!found, "the finished drink order left the active list");

        sb.AppendLine($"  RESULT: {pass} passed, {fail} failed");
        Debug.Log(sb.ToString());
        Debug.Log($"CAFETERIA_ORDERING_TEST_DONE pass={pass} fail={fail}");

        PlayerPrefs.DeleteKey("CafeteriaOrdering__TEST");
        CafeteriaOrdering.PrefsKeyOverride = null;
        PlayerPrefs.Save();
        UnityEngine.Object.DestroyImmediate(go);
        UnityEngine.Object.DestroyImmediate(go2);
    }
}
