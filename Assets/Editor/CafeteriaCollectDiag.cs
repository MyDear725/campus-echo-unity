using System.Text;
using UnityEditor;
using UnityEngine;

// Checks the pickup-point decision chain at the 2F water bar without entering play mode.
public static class CafeteriaCollectDiag
{
    [MenuItem("Campus/Testing/Diagnose collect point")]
    public static void Run()
    {
        CafeteriaOrdering.PrefsKeyOverride = "CafeteriaOrdering__DIAG";
        PlayerPrefs.DeleteKey("CafeteriaOrdering__DIAG");

        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/Interiors/Scenes/cafeteria.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        var sys = Object.FindFirstObjectByType<CafeteriaOrdering>();
        var sb = new StringBuilder("=== COLLECT DIAG ===\n");
        if (!sys) { Debug.Log("no ordering"); return; }
        sys.Init();
        sys.SetBalanceForTest(30000);
        sys.prepareSeconds = 1;

        // place an order at the water bar
        var wbPos = new Vector3(-0.5f, 4.8f, 1.4f);
        sys.TestPlayerPos = wbPos;
        sys.SetActiveShop(sys.NearestOrderShop());
        sb.AppendLine($"  order shop at water bar: {sys.ActiveShop}");
        sys.CartAdd(sys.ShopItemName(0), sys.ShopItemFen(0));
        sb.AppendLine($"  paid: {sys.TryPay()}");

        System.Threading.Thread.Sleep(1200);
        sys.Tick();
        sb.AppendLine($"  state after tick: {(sys.OrderCount > 0 ? ((CafeteriaOrderState)sys.OrderAt(0).state).ToString() : "none")}");

        foreach (var p in new[]
        {
            new Vector3(1.6f, 4.8f, 1.2f),   // water bar pickup point
            new Vector3(-0.5f, 4.8f, 1.2f),  // water bar counter
            new Vector3(1.6f, 4.8f, -1.0f),  // right on the anchor
        })
        {
            sys.TestPlayerPos = p;
            sb.AppendLine($"  at {p}: shop={sys.NearestOrderShop()} nearCollect={sys.NearCollectSpot} " +
                          $"nearOrder={sys.NearOrderSpot} canOrderHere={sys.CanOrderAt(sys.NearestOrderShop())} " +
                          $"collectable={(sys.CollectableOrderHere() != null ? "YES" : "no")}");
        }
        sys.TestPlayerPos = null;

        Debug.Log(sb.ToString());
        PlayerPrefs.DeleteKey("CafeteriaOrdering__DIAG");
        CafeteriaOrdering.PrefsKeyOverride = null;
    }
}
