using System;
using System.Collections.Generic;
using UnityEngine;

// A stockable vending slot.
[Serializable]
public class VendingItem
{
    public string name;
    public int priceFen;
    public int stock;
}

// Per-machine catalogue and stock. Two machines exist in the library; each keeps its own
// list so they can be restocked or reconfigured independently.
[Serializable]
public class VendingState
{
    public string machineId;
    public List<VendingItem> items = new List<VendingItem>();
}

public static class VendingSystem
{
    public const string StockKeyPrefix = "VendingStock_";
    public const string InventoryKey = "CampusInventory";

    // Tests point these at throwaway keys.
    public static string KeyOverridePrefix;
    public static string InventoryKeyOverride;

    static string StockKey(string id) => (KeyOverridePrefix ?? StockKeyPrefix) + id;

    // ---- default catalogues, matched to the machines' appearance ----
    public static List<VendingItem> DefaultCatalogue(string machineId)
    {
        var list = new List<VendingItem>();
        if (machineId == "vending_drinks_1f")
        {
            list.Add(new VendingItem { name = "冰镇矿泉水", priceFen = 200, stock = 12 });
            list.Add(new VendingItem { name = "无糖气泡水", priceFen = 500, stock = 8 });
            list.Add(new VendingItem { name = "冰红茶", priceFen = 450, stock = 10 });
            list.Add(new VendingItem { name = "拿铁咖啡", priceFen = 1200, stock = 6 });
            list.Add(new VendingItem { name = "鲜榨橙汁", priceFen = 1500, stock = 5 });
        }
        else
        {
            list.Add(new VendingItem { name = "原味薯片", priceFen = 550, stock = 10 });
            list.Add(new VendingItem { name = "巧克力棒", priceFen = 800, stock = 8 });
            list.Add(new VendingItem { name = "坚果混合", priceFen = 950, stock = 6 });
            list.Add(new VendingItem { name = "全麦饼干", priceFen = 600, stock = 9 });
        }
        return list;
    }

    public static VendingState Load(string machineId)
    {
        string k = StockKey(machineId);
        if (PlayerPrefs.HasKey(k))
        {
            var s = JsonUtility.FromJson<VendingState>(PlayerPrefs.GetString(k));
            if (s != null && s.items != null && s.items.Count > 0) return s;
        }
        var fresh = new VendingState { machineId = machineId, items = DefaultCatalogue(machineId) };
        Save(fresh);
        return fresh;
    }

    public static void Save(VendingState s)
    {
        PlayerPrefs.SetString(StockKey(s.machineId), JsonUtility.ToJson(s));
        PlayerPrefs.Save();
    }

    // The player's goods live in the one shared inventory (CampusInventory), so the
    // water bar, the fruit stall and these machines all add to the same record.
    public static InventoryState LoadInventory() { return CampusInventory.Load(); }

    public static void SaveInventory(InventoryState inv) { CampusInventory.Save(); }

    // One atomic purchase: money is charged only if the slot still has stock and the
    // quantity is valid, and the stock is reduced in the same step.
    public static bool TryBuy(VendingState state, int index, int qty,
                              out string message)
    {
        message = null;
        if (state == null || index < 0 || index >= state.items.Count) { message = "商品不存在"; return false; }
        if (qty <= 0) { message = "请选择数量"; return false; }

        var item = state.items[index];
        if (item.stock < qty) { message = "库存不足"; return false; }

        long total = (long)item.priceFen * qty;
        if (CampusWallet.BalanceFen < total) { message = CampusWallet.ShortfallText(total); return false; }

        if (!CampusWallet.TrySpend(total)) { message = CampusWallet.ShortfallText(total); return false; }

        item.stock -= qty;
        Save(state);

        CampusInventory.Add(item.name, qty);

        message = $"购买成功：{item.name} ×{qty}，共 {CampusWallet.FormatYuan(total)}";
        return true;
    }
}
