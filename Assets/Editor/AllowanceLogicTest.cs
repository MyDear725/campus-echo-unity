using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// Fast, graphics-free regression for the allowance / shop / order logic.
//
//   Unity -batchmode -quit -projectPath <repo> \
//         -executeMethod AllowanceLogicTest.Run -logFile <log>
//
// It runs in edit mode (no play mode, no player build, no screenshots), so a full pass
// takes seconds after the editor has compiled. Use it while changing rules; use
// AllowanceShopQA (graphical) only when the drawing or the click path changed.
public static class AllowanceLogicTest
{
    const string WalletKey = "LogicTest_Wallet";
    const string OrderKey = "LogicTest_Cafeteria";
    const string InvKey = "LogicTest_Inventory";
    const string ClockKey = "LogicTest_Clock";

    static StringBuilder log;
    static int pass, fail;

    static readonly List<GameObject> created = new List<GameObject>();

    public static void Run()
    {
        log = new StringBuilder();
        pass = fail = 0;

        try
        {
            Isolate();
            WalletTests();
            MenuTests();
            CartAndDoubleClickTests();
            MultiOrderTests();
            InstantShopTests();
            CollectTests();
            PersistenceTests();
        }
        catch (Exception e)
        {
            Fail("unexpected exception: " + e.Message + "\n" + e.StackTrace);
        }
        finally
        {
            Cleanup();
        }

        log.AppendLine($"RESULT pass={pass} fail={fail} {(fail == 0 ? "OK" : "FAILED")}");
        Directory.CreateDirectory(OutputDir);
        File.WriteAllText(Path.Combine(OutputDir, "results.txt"), log.ToString());
        Debug.Log(log.ToString());
        EditorApplication.Exit(fail == 0 ? 0 : 1);
    }

    static string OutputDir => Path.Combine(Application.dataPath, "..", "QA", "AllowanceLogic");

    static void Isolate()
    {
        CampusWallet.KeyOverride = WalletKey;
        CafeteriaOrdering.PrefsKeyOverride = OrderKey;
        CampusInventory.KeyOverride = InvKey;
        GameClock.KeyOverride = ClockKey;
        foreach (var k in new[] { WalletKey, OrderKey, InvKey, ClockKey }) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        CampusWallet.ResetCache();
        CampusInventory.ResetCache();
    }

    // ---------------------------------------------------------------- tests
    static void WalletTests()
    {
        Check(CampusWallet.BalanceFen == CampusWallet.DefaultStartingFen, "fresh profile starts at the configured allowance");
        Check(CampusWallet.BalanceText == "生活费：¥300.00", "balance text format");

        CampusWallet.SetForTest(1000);
        Check(CampusWallet.BalanceText == "生活费：¥10.00", "fen to yuan formatting");
        Check(CampusWallet.TrySpend(250) && CampusWallet.BalanceFen == 750, "spend deducts exactly");
        Check(!CampusWallet.TrySpend(10000) && CampusWallet.BalanceFen == 750, "overspend refused, balance untouched");
        Check(CampusWallet.ShortfallFen(1000) == 250, "shortfall computed");
        Check(CampusWallet.ShortfallText(1000).Contains("¥2.50"), "shortfall text");

        // A reload must read the stored value, never re-initialise it.
        CampusWallet.ResetCache();
        Check(CampusWallet.BalanceFen == 750, "balance survives a cache reset (restart)");
    }

    static void MenuTests()
    {
        var order = NewOrdering();
        string date = order.MenuDate;
        Check(!string.IsNullOrEmpty(date), "menu date set");

        // Same day, same shop, repeated access -> identical list.
        string first = Signature(order, CafeteriaShop.Wok1F);
        string again = Signature(order, CafeteriaShop.Wok1F);
        Check(first == again && first.Length > 0, "menu is stable within the same game day");

        // A second instance (a restart) reads the same stored menu.
        var second = NewOrdering(shareSave: true);
        Check(Signature(second, CafeteriaShop.Wok1F) == first, "menu survives a simulated restart");
        UnityEngine.Object.DestroyImmediate(second.gameObject);

        // Every shop has its own pool and its own prices.
        var seen = new Dictionary<string, CafeteriaShop>();
        bool allPoolsDistinct = true;
        foreach (var def in CafeteriaShops.All)
        {
            string sig = Signature(order, def.id);
            Check(sig.Length > 0, def.shortName + ": menu not empty");
            if (seen.TryGetValue(sig, out var other)) { allPoolsDistinct = false; Log($"  duplicate pool: {def.shortName} == {other}"); }
            else seen[sig] = def.id;

            for (int i = 0; i < order.MenuItemCountFor(def.id); i++)
            {
                int fen = order.MenuItemFenFor(def.id, i);
                int cap = def.business == CafeteriaBusiness.Drink ? CafeteriaMenu.DrinkMaxFen
                        : def.business == CafeteriaBusiness.Fruit ? CafeteriaMenu.FruitMaxFen
                        : CafeteriaMenu.DishMaxFen;
                if (fen <= 0 || fen > cap) Fail($"{def.shortName}: price {fen} out of range (cap {cap})");
            }
        }
        Check(allPoolsDistinct, "every shop rolls its own pool");

        // Business types never mix.
        bool drinksOnly = true, fruitOnly = true, dishOnly = true;
        for (int i = 0; i < order.MenuItemCountFor(CafeteriaShop.WaterBar1F); i++)
        {
            var m = order.MenuFor(CafeteriaShop.WaterBar1F);
            string unit = m.units[i];
            if (unit != "杯" && unit != "瓶") drinksOnly = false;
        }
        for (int i = 0; i < order.MenuItemCountFor(CafeteriaShop.FruitStall3F); i++)
        {
            var m = order.MenuFor(CafeteriaShop.FruitStall3F);
            if (m.units[i] != CafeteriaMenu.FruitUnit) fruitOnly = false;
        }
        for (int i = 0; i < order.MenuItemCountFor(CafeteriaShop.Sichuan2F); i++)
        {
            var m = order.MenuFor(CafeteriaShop.Sichuan2F);
            if (m.units[i] == "杯" || m.units[i] == "瓶") dishOnly = false;
        }
        Check(drinksOnly, "water bar only sells by the cup/bottle");
        Check(fruitOnly, "fruit stall sells by the portion");
        Check(dishOnly, "meal counters never sell drinks");

        UnityEngine.Object.DestroyImmediate(order.gameObject);
    }

    static void CartAndDoubleClickTests()
    {
        var order = NewOrdering();
        order.SetBalanceForTest(10000);
        order.OpenPanel(CafeteriaShop.Wok1F);
        order.CartAdd(0);
        int total = order.CartTotalFen();
        Check(total > 0, "cart total computed");

        long before = CampusWallet.BalanceFen;
        Check(order.TryPay(), "first pay succeeds");
        Check(CampusWallet.BalanceFen == before - total, "charged once");
        Check(!order.TryPay(), "second pay on the emptied basket is refused");
        Check(CampusWallet.BalanceFen == before - total, "second pay charged nothing");

        // Insufficient funds never go negative and give the shortfall.
        order.SetBalanceForTest(1);
        order.OpenPanel(CafeteriaShop.Wok1F);
        order.CartAdd(0);
        long poor = CampusWallet.BalanceFen;
        Check(!order.TryPay() && CampusWallet.BalanceFen == poor, "insufficient funds refused");
        Check(order.PayBlockReason.Contains("还差"), "shortfall explained");
        order.ClosePanel();

        UnityEngine.Object.DestroyImmediate(order.gameObject);
    }

    static void MultiOrderTests()
    {
        var order = NewOrdering();
        order.SetBalanceForTest(30000);
        order.prepareSeconds = 1;

        BuyMeal(order, CafeteriaShop.Starter1F);
        BuyMeal(order, CafeteriaShop.Sichuan2F);

        Check(order.OrderCount == 2, "two orders coexist");
        var a = FindOrder(order, CafeteriaShop.Starter1F);
        var b = FindOrder(order, CafeteriaShop.Sichuan2F);
        Check(a != null && b != null && a.id != b.id, "distinct ids");
        Check(a.floor == 1 && b.floor == 2, "each order keeps its floor");
        Check(a.totalFen > 0 && b.totalFen > 0, "each order keeps its snapshot total");

        // Both timers must mature independently of each other.
        order.ForceReadyForTest(a.id);
        order.Tick();
        Check(a.state == (int)CafeteriaOrderState.ReadyToCollect, "order A promoted to collectable");
        Check(b.state == (int)CafeteriaOrderState.Preparing, "order B still preparing, untouched");

        order.ForceReadyForTest(b.id);
        order.Tick();
        Check(b.state == (int)CafeteriaOrderState.ReadyToCollect, "order B promoted to collectable");

        UnityEngine.Object.DestroyImmediate(order.gameObject);
    }

    static void InstantShopTests()
    {
        var order = NewOrdering();
        order.SetBalanceForTest(30000);
        BuyMeal(order, CafeteriaShop.Wok1F);
        var meal = FindOrder(order, CafeteriaShop.Wok1F);
        Check(meal != null && meal.state == (int)CafeteriaOrderState.Preparing, "meal order placed");

        // A drink must not disturb the meal, and must land in the shared inventory.
        order.OpenPanel(CafeteriaShop.WaterBar1F);
        order.CartAdd(0);
        string drink = order.ShopItemName(0);
        int total = order.CartTotalFen();
        long before = CampusWallet.BalanceFen;
        Check(order.TryPay() && CampusWallet.BalanceFen == before - total, "drink paid");
        Check(CampusInventory.CountOf(drink) >= 1, "drink is in the shared inventory");
        Check(meal.state == (int)CafeteriaOrderState.Preparing, "meal order untouched by the drink");
        Check(order.InstantOrderHeld(CafeteriaShop.WaterBar1F) != null, "instant purchase recorded");

        var instant = order.InstantOrderHeld(CafeteriaShop.WaterBar1F);
        string msg;
        Check(order.ConsumeInstant(instant, out msg), "instant drink consumed from inventory");
        Check(CampusInventory.CountOf(drink) == 0, "inventory decremented");
        order.ClosePanel();

        UnityEngine.Object.DestroyImmediate(order.gameObject);
    }

    static void CollectTests()
    {
        var order = NewOrdering();
        order.SetBalanceForTest(30000);
        BuyMeal(order, CafeteriaShop.Starter1F);
        BuyMeal(order, CafeteriaShop.Claypot2F);
        var a = FindOrder(order, CafeteriaShop.Starter1F);
        var b = FindOrder(order, CafeteriaShop.Claypot2F);
        order.ForceReadyForTest(a.id);
        order.ForceReadyForTest(b.id);
        order.Tick();

        // Standing at B's own pickup point offers B only.
        order.SetPlayerPosForTest(AnchorPos(order, CafeteriaShop.Claypot2F));
        Check(order.CollectableOrderHere() == b, "2F pickup offers only the 2F order");
        string msg;
        Check(order.TryCollect(out msg) && b.state == (int)CafeteriaOrderState.Carrying, "2F order collected");
        Check(a.state == (int)CafeteriaOrderState.ReadyToCollect, "1F order still ready");

        // Hands full -> A cannot be taken, and stays ready.
        order.SetPlayerPosForTest(AnchorPos(order, CafeteriaShop.Starter1F));
        Check(!order.TryCollect(out msg), "second pickup refused while a tray is held");
        Check(msg.Contains("先用餐"), "refusal explains the held tray");
        Check(a.state == (int)CafeteriaOrderState.ReadyToCollect, "un-collected order kept");

        // Eat, return the tray, then A is collectable again.
        order.SetEating(true);
        order.AddEatProgress(2f);
        Check(order.AwaitingTrayReturn, "meal finished, tray owed");
        Check(order.TryReturnTray(), "tray returned");
        order.SetPlayerPosForTest(AnchorPos(order, CafeteriaShop.Starter1F));
        Check(order.TryCollect(out msg) && a.state == (int)CafeteriaOrderState.Carrying, "1F order collected after the tray");
        order.ClearPlayerPosForTest();

        UnityEngine.Object.DestroyImmediate(order.gameObject);
    }

    static void PersistenceTests()
    {
        var order = NewOrdering();
        order.SetBalanceForTest(23456);
        order.prepareSeconds = 60;
        BuyMeal(order, CafeteriaShop.Teppan2F);
        long expectedBalance = CampusWallet.BalanceFen;   // after the meal was paid for
        int orders = order.OrderCount;
        string date = order.MenuDate;
        UnityEngine.Object.DestroyImmediate(order.gameObject);

        // A new instance over the same save == a restart.
        var reloaded = NewOrdering(shareSave: true);
        Check(CampusWallet.BalanceFen == expectedBalance,
              $"allowance restored after restart (expected {expectedBalance}, got {CampusWallet.BalanceFen})");
        Check(reloaded.MenuDate == date, "menu day restored after restart");
        Check(reloaded.OrderCount == orders, "orders restored after restart");
        UnityEngine.Object.DestroyImmediate(reloaded.gameObject);
    }

    // ---------------------------------------------------------------- helpers
    static CafeteriaOrdering NewOrdering(bool shareSave = false)
    {
        // A fresh instance must start from an empty save, otherwise state leaks between
        // tests through the very persistence layer they are meant to exercise.
        if (!shareSave)
        {
            PlayerPrefs.DeleteKey(OrderKey);
            PlayerPrefs.DeleteKey(InvKey);
            PlayerPrefs.Save();
            CampusInventory.ResetCache();
        }
        var go = new GameObject("LogicTest_Cafeteria" + (shareSave ? "_reload" : ""));
        created.Add(go);
        var order = go.AddComponent<CafeteriaOrdering>();
        // Minimal anchors: one counter + one pickup per shop, laid out on its own floor.
        var orderSpots = new List<Transform>();
        var collectSpots = new List<Transform>();
        var returnSpots = new List<Transform>();
        var seatSpots = new List<Transform>();
        foreach (var def in CafeteriaShops.All)
        {
            var pos = new Vector3((int)def.id * 10f, (def.floor - 1) * 4.3f, 0f);
            var o = new GameObject("Order_" + def.id.ToString().ToLowerInvariant());
            o.transform.SetParent(go.transform, false);
            o.transform.position = pos;
            var marker = o.AddComponent<CafeteriaCounter>();
            marker.shop = def.id;
            orderSpots.Add(o.transform);

            var c = new GameObject("Collect_" + def.id.ToString().ToLowerInvariant());
            c.transform.SetParent(go.transform, false);
            c.transform.position = pos;
            collectSpots.Add(c.transform);
        }
        for (int f = 1; f <= 3; f++)
        {
            var r = new GameObject("Return_" + f + "f");
            r.transform.SetParent(go.transform, false);
            r.transform.position = new Vector3(-50f, (f - 1) * 4.3f, 0f);
            returnSpots.Add(r.transform);
        }
        var s1 = new GameObject("Seat_1f");
        s1.transform.SetParent(go.transform, false);
        s1.transform.position = new Vector3(-60f, 0f, 0f);
        seatSpots.Add(s1.transform);

        order.orderSpots = orderSpots.ToArray();
        order.collectSpots = collectSpots.ToArray();
        order.returnSpots = returnSpots.ToArray();
        order.seatSpots = seatSpots.ToArray();
        order.eatSeconds = 1;
        order.Init();
        return order;
    }

    static Vector3 AnchorPos(CafeteriaOrdering order, CafeteriaShop shop)
    {
        foreach (var t in order.collectSpots)
            if (t && t.name == "Collect_" + shop.ToString().ToLowerInvariant()) return t.position;
        return Vector3.zero;
    }

    static void BuyMeal(CafeteriaOrdering order, CafeteriaShop shop)
    {
        order.OpenPanel(shop);
        order.CartAdd(0);
        if (!order.TryPay()) Fail(shop + ": pay failed (" + order.PayBlockReason + ")");
        order.ClosePanel();
    }

    static CafeteriaOrder FindOrder(CafeteriaOrdering order, CafeteriaShop shop)
    {
        foreach (var o in order.OrdersForTest()) if (o != null && o.shop == (int)shop) return o;
        return null;
    }

    static string Signature(CafeteriaOrdering order, CafeteriaShop shop)
    {
        var sb = new StringBuilder();
        int n = order.MenuItemCountFor(shop);
        for (int i = 0; i < n; i++) sb.Append(order.MenuItemNameFor(shop, i)).Append('@').Append(order.MenuItemFenFor(shop, i)).Append(';');
        return sb.ToString();
    }

    static void Cleanup()
    {
        foreach (var go in created) if (go) UnityEngine.Object.DestroyImmediate(go);
        created.Clear();
        foreach (var k in new[] { WalletKey, OrderKey, InvKey, ClockKey }) PlayerPrefs.DeleteKey(k);
        PlayerPrefs.Save();
        CampusWallet.KeyOverride = null;
        CafeteriaOrdering.PrefsKeyOverride = null;
        CampusInventory.KeyOverride = null;
        GameClock.KeyOverride = null;
        CampusWallet.ResetCache();
        CampusInventory.ResetCache();
    }

    static void Check(bool ok, string what)
    {
        if (ok) { pass++; log.AppendLine("PASS " + what); }
        else Fail(what);
    }

    static void Fail(string what)
    {
        fail++;
        log.AppendLine("FAIL " + what);
    }

    static void Log(string what) { log.AppendLine("     " + what); }
}
