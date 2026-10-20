using System.Text;
using UnityEditor;
using UnityEngine;

// Exercises the ordering flow against the real cafeteria scene contents, the same way
// a player would: find the components in the built scene, then walk the state machine.
public static class CafeteriaOrderingFlowTest
{
    [MenuItem("Campus/Testing/Verify Cafeteria Ordering Flow (scene)")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== CAFETERIA ORDERING FLOW (real scene) ===");

        CafeteriaOrdering.PrefsKeyOverride = "CafeteriaOrdering__FLOWTEST";
        PlayerPrefs.DeleteKey("CafeteriaOrdering__FLOWTEST");

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/Interiors/Scenes/cafeteria.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        var ordering = UnityEngine.Object.FindFirstObjectByType<CafeteriaOrdering>();
        var ui = UnityEngine.Object.FindFirstObjectByType<CafeteriaOrderingUI>();
        var interactions = UnityEngine.Object.FindFirstObjectByType<CafeteriaIndoorInteractions>();

        sb.AppendLine($"  CafeteriaOrdering       : {(ordering ? "FOUND" : "MISSING")}");
        sb.AppendLine($"  CafeteriaOrderingUI     : {(ui ? "FOUND" : "MISSING")}");
        sb.AppendLine($"  CafeteriaIndoorInteract : {(interactions ? "FOUND" : "MISSING")}");

        if (ordering)
        {
            sb.AppendLine($"  anchors wired: order={Cnt(ordering.orderSpots)} collect={Cnt(ordering.collectSpots)} " +
                          $"return={Cnt(ordering.returnSpots)} seat={Cnt(ordering.seatSpots)}");
            sb.AppendLine($"  order/collect/return/seat anchors: " +
                          $"{ordering.orderSpots?.Length}/{ordering.collectSpots?.Length}/" +
                          $"{ordering.returnSpots?.Length}/{ordering.seatSpots?.Length}");
            sb.AppendLine($"  enabled: {ordering.enabled}, state={ordering.State}, balance={ordering.BalanceFen}");

            ordering.Init();
            ordering.SetBalanceForTest(30000);
            ordering.CartClearForTest();
            ordering.ForceIdleForTest();
            sb.AppendLine($"  after Init: dishes={ordering.DishCount} drinks={ordering.DrinkCount} " +
                          $"date={ordering.MenuDateForTest()}");

            // Simulate: pick two dishes + a drink, pay, force-ready, collect, eat, return.
            int d0 = ordering.DishFen(0), d1 = ordering.DishFen(1), dr = ordering.DrinkFen(0);
            ordering.CartAdd(ordering.DishName(0), d0);
            ordering.CartAdd(ordering.DishName(1), d1);
            ordering.CartAdd(ordering.DrinkName(0), dr);
            int total = ordering.CartTotal();
            sb.AppendLine($"  cart: {ordering.CartCount} lines, total={total/100f:C}");

            long before = ordering.BalanceFen;
            bool paid = ordering.TryPay();
            sb.AppendLine($"  paid={paid}, balance {before} -> {ordering.BalanceFen}, " +
                          $"state={ordering.State}, order=#{ordering.OrderId}");

            ordering.ForceReadyForTest();
            sb.AppendLine($"  forced ready: state={ordering.State}, CanCollect={ordering.CanCollect}");
            sb.AppendLine($"  collect={ordering.TryCollect()}, carrying={ordering.HasTray}");

            ordering.SetEating(true);
            ordering.AddEatProgress(1f);
            sb.AppendLine($"  ate fully: progress={ordering.EatProgress}, state={ordering.State}");
            sb.AppendLine($"  return={ordering.TryReturnTray()}, state={ordering.State}, " +
                          $"balance={ordering.BalanceFen} (unchanged expected)");

            // Second round must be possible immediately.
            ordering.CartAdd(ordering.DishName(2), ordering.DishFen(2));
            sb.AppendLine($"  can re-order: {ordering.CartCount == 1}");

            // ---- per-floor interaction: the case that was broken ----
            // The flow above ended in state None, so the counters are orderable again.
            var fresh = ordering;

            var p1f = new Vector3(-4f, 0.5f, 10f);
            var p2f = new Vector3(-4f, 4.8f, 10f);
            var p3f = new Vector3(10f, 9.1f, 8.5f);
            sb.AppendLine($"  1F counter owns spot: {fresh.OwnsSpotAt(p1f)}");
            sb.AppendLine($"  2F counter owns spot: {fresh.OwnsSpotAt(p2f)}   <-- the reported failure");
            sb.AppendLine($"  3F counter owns spot: {fresh.OwnsSpotAt(p3f)}");
            // Genuinely empty spots on each floor (south end, no counter within range).
            bool e1 = fresh.OwnsSpotAt(new Vector3(0f, 0.5f, -13f));
            bool e2 = fresh.OwnsSpotAt(new Vector3(0f, 4.8f, -13f));
            bool e3 = fresh.OwnsSpotAt(new Vector3(0f, 9.1f, -13f));
            sb.AppendLine($"  empty 1F/2F/3F spots own nothing: {!e1}/{!e2}/{!e3}");
            sb.AppendLine($"  can order now: {fresh.CanOrder}, dishes: {fresh.DishCount}");

            sb.AppendLine($"  seat anchors: {fresh.SeatAnchorCount}");

            // every floor: order, collect, return and seat must be covered where the
            // counters and chairs actually are
            bool cover =
                fresh.NearOrderAt(new Vector3(-4f, 0.5f, 10f)) &&
                fresh.NearCollectAt(new Vector3(-4f, 0.5f, 8.2f)) &&
                fresh.NearReturnAt(new Vector3(12f, 0.5f, 4f)) &&
                fresh.NearSeatAt(new Vector3(-10.5f, 0.5f, 5.65f)) &&
                fresh.NearOrderAt(new Vector3(-4f, 4.8f, 10f)) &&
                fresh.NearCollectAt(new Vector3(-4f, 4.8f, 8.2f)) &&
                fresh.NearReturnAt(new Vector3(12f, 4.8f, 4f)) &&
                fresh.NearSeatAt(new Vector3(-10.5f, 4.8f, 5.65f)) &&
                fresh.NearOrderAt(new Vector3(10f, 9.1f, 8.5f)) &&
                fresh.NearCollectAt(new Vector3(10f, 9.1f, 6.6f)) &&
                fresh.NearReturnAt(new Vector3(12f, 9.1f, 1.2f)) &&
                fresh.NearSeatAt(new Vector3(-10.5f, 9.1f, 5.65f));
            sb.AppendLine($"  three-floor order/collect/return/seat coverage: {cover}");
            sb.AppendLine($"  seat hides other floors: {!fresh.NearSeatAt(new Vector3(-10.5f, 9.1f, 5.65f + 0f)) == false}");

            string wb = fresh.NearestOrderShopAtForTest(new Vector3(-0.5f, 4.8f, 1.4f)).ToString();
            string fr = fresh.NearestOrderShopAtForTest(new Vector3(-3.8f, 9.1f, 12.4f)).ToString();
            sb.AppendLine($"  shop at 2F water bar: {wb}   shop at 3F fruit stall: {fr}");

            bool floorOk = fresh.OwnsSpotAt(p1f) && fresh.OwnsSpotAt(p2f) && fresh.OwnsSpotAt(p3f)
                        && !e1 && !e2 && !e3
                        && wb == "WaterBar" && fr == "FruitStall" && cover;
            sb.AppendLine($"  FLOOR CHECK: {(floorOk ? "PASS" : "FAIL")}");

            sb.AppendLine($"  RESULT: {(paid && ordering.BalanceFen == before - total && floorOk ? "PASS" : "FAIL")}");
        }

        Debug.Log(sb.ToString());
        Debug.Log("CAFETERIA_FLOW_TEST_DONE");
        PlayerPrefs.DeleteKey("CafeteriaOrdering__FLOWTEST");
        CafeteriaOrdering.PrefsKeyOverride = null;
        PlayerPrefs.Save();
    }

    static string Fmt(Transform t) => t ? $"({t.position.x:F1},{t.position.y:F1},{t.position.z:F1})" : "null";
    static string Cnt(Transform[] a) => a == null ? "MISSING" : a.Length + " ok";
    static string List(Transform[] a)
    {
        if (a == null) return "null";
        var sb = new System.Text.StringBuilder();
        foreach (var t in a) sb.Append(Fmt(t) + " ");
        return sb.ToString().Trim();
    }
}
