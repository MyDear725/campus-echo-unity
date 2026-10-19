using System.Text;
using UnityEditor;
using UnityEngine;

// Verifies the shared wallet, reservation rules, vending purchases and the game clock
// against a throw-away save so the player's real data is never touched.
public static class PhoneBookingTest
{
    [MenuItem("Campus/Testing/Verify Phone Booking and Vending")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== PHONE / BOOKING / VENDING TEST ===");
        int pass = 0, fail = 0;

        void Check(bool ok, string label)
        {
            sb.AppendLine($"  {(ok ? "PASS" : "FAIL")}  {label}");
            if (ok) pass++; else fail++;
        }

        // ---- isolated saves ----
        const string walletKey = "TestWallet";
        const string resKey = "TestReservation";
        CampusWallet.KeyOverride = walletKey;
        CampusWallet.ResetCache();
        LibraryReservationManager.KeyOverride = resKey;
        VendingSystem.KeyOverridePrefix = "TestStock_";
        VendingSystem.InventoryKeyOverride = "TestInventory";

        PlayerPrefs.DeleteKey(walletKey);
        PlayerPrefs.DeleteKey(resKey);
        PlayerPrefs.DeleteKey("TestStock_vending_drinks_1f");
        PlayerPrefs.DeleteKey("TestStock_vending_snacks_1f");
        PlayerPrefs.DeleteKey("TestInventory");

        // ---- wallet ----
        CampusWallet.SetForTest(30000);
        Check(CampusWallet.BalanceFen == 30000, "wallet starts at ¥300.00");
        Check(CampusWallet.TrySpend(5000), "spending ¥50 succeeds");
        Check(CampusWallet.BalanceFen == 25000, "balance drops to ¥250.00");
        Check(!CampusWallet.TrySpend(999999), "overspending is refused");
        Check(CampusWallet.BalanceFen == 25000, "refused spend left balance untouched");

        // ---- seats from the built scene ----
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/Interiors/Scenes/library.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        var parts = Object.FindObjectsByType<LibrarySeatPart>(FindObjectsSortMode.None);
        sb.AppendLine($"  seat markers in scene: {parts.Length}");

        var reg = LibrarySeatRegistry.Ensure();
        reg.Clear();
        foreach (var p in parts) if (p) reg.Add(p.ToSeat());
        reg.ComputeMapPositions();

        int f2 = reg.CountOnFloor(2), f3 = reg.CountOnFloor(3);
        Check(f2 > 0, $"floor 2 has reservable seats ({f2})");
        Check(f3 > 0, $"floor 3 has reservable seats ({f3})");
        Check(reg.CountOnFloor(1) == 0, "floor 1 has none, so the phone must say so");

        // ids unique and stable-looking
        var ids = new System.Collections.Generic.HashSet<string>();
        bool allNamed = true;
        foreach (var s in reg.All)
        {
            if (string.IsNullOrEmpty(s.id)) allNamed = false;
            ids.Add(s.id);
        }
        Check(ids.Count == reg.All.Count, "seat ids are unique");
        Check(allNamed, "every seat has an id");

        // ---- reservation rules ----
        var rm = LibraryReservationManager.Ensure();
        rm.Cancel();
        var first = reg.OnFloor(2)[0];
        Check(rm.TryReserve(first.id, 2, first.area), "reserve first seat");
        Check(rm.HasReservation, "reservation recorded");
        Check(rm.ReservedSeatId == first.id, "reservation points at that seat");

        var second = reg.OnFloor(2)[1];
        Check(!rm.TryReserve(second.id, 2, second.area), "second reservation refused without replace");
        Check(rm.ReservedSeatId == first.id, "original reservation survived the refusal");

        Check(rm.TryReplace(second.id, 2, second.area, true), "explicit replace succeeds");
        Check(rm.ReservedSeatId == second.id, "reservation moved to the new seat");
        Check(rm.TryReplace(second.id, 2, second.area, false), "same-seat re-confirm is a no-op success");

        rm.Cancel();
        Check(!rm.HasReservation, "cancel clears the reservation");

        // ---- vending: atomic purchase ----
        var drinks = VendingSystem.Load("vending_drinks_1f");
        Check(drinks.items.Count > 0, "drinks machine has stock configured");
        bool priceOk = true;
        foreach (var it in drinks.items) if (it.priceFen <= 0 || it.priceFen > 2000) priceOk = false;
        Check(priceOk, "all drink prices in (0, ¥20]");

        var snacks = VendingSystem.Load("vending_snacks_1f");
        bool snackOk = true;
        foreach (var it in snacks.items) if (it.priceFen <= 0 || it.priceFen > 1000) snackOk = false;
        Check(snackOk, "all snack prices in (0, ¥10]");

        CampusWallet.SetForTest(1000);
        int stockBefore = drinks.items[0].stock;
        string msg;
        bool bought = VendingSystem.TryBuy(drinks, 0, 1, out msg);
        Check(bought, "affordable purchase succeeds");
        Check(drinks.items[0].stock == stockBefore - 1, "stock reduced by one");
        Check(CampusWallet.BalanceFen == 1000 - drinks.items[0].priceFen, "charged exactly the price");

        var inv = VendingSystem.LoadInventory();
        Check(inv.names.Contains(drinks.items[0].name), "item added to inventory");

        // insufficient funds must not change stock
        CampusWallet.SetForTest(10);
        int s2 = drinks.items[0].stock;
        bool poor = VendingSystem.TryBuy(drinks, 0, 1, out msg);
        Check(!poor, "purchase blocked when funds are short");
        Check(drinks.items[0].stock == s2, "stock unchanged on failure");
        Check(CampusWallet.BalanceFen == 10, "balance unchanged on failure");

        // sold out must not charge
        CampusWallet.SetForTest(100000);
        drinks.items[0].stock = 0;
        VendingSystem.Save(drinks);
        long balBefore = CampusWallet.BalanceFen;
        Check(!VendingSystem.TryBuy(drinks, 0, 1, out msg), "sold-out slot refuses purchase");
        Check(CampusWallet.BalanceFen == balBefore, "sold-out did not charge");

        // ---- clock ----
        var clock = GameClock.Ensure();
        clock.RestoreNormal();
        Check(Mathf.Approximately(clock.CurrentMultiplier, 1f), "clock starts at normal rate");
        clock.SetStudyMode(true);
        float m1 = clock.CurrentMultiplier;
        clock.SetStudyMode(true);
        Check(Mathf.Approximately(clock.CurrentMultiplier, m1), "study rate does not stack");
        Check(m1 > 1f, "study rate is accelerated");
        clock.RestoreNormal();
        Check(Mathf.Approximately(clock.CurrentMultiplier, 1f), "normal rate restored");

        // cleanup: never leave test keys or overrides behind
        PlayerPrefs.DeleteKey(walletKey);
        PlayerPrefs.DeleteKey(resKey);
        PlayerPrefs.DeleteKey("TestStock_vending_drinks_1f");
        PlayerPrefs.DeleteKey("TestStock_vending_snacks_1f");
        PlayerPrefs.DeleteKey("TestInventory");
        CampusWallet.KeyOverride = null;
        CampusWallet.ResetCache();
        LibraryReservationManager.KeyOverride = null;
        VendingSystem.KeyOverridePrefix = null;
        VendingSystem.InventoryKeyOverride = null;
        PlayerPrefs.Save();

        sb.AppendLine($"  RESULT: {pass} passed, {fail} failed");
        Debug.Log(sb.ToString());
        Debug.Log($"PHONE_BOOKING_TEST_DONE pass={pass} fail={fail}");
    }
}
