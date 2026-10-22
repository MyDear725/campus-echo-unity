using System;
using System.Collections.Generic;
using UnityEngine;

// Every staffed selling point in the cafeteria. The value is the STABLE id persisted in
// saves and used by the scene anchors, so it must never be reordered or renumbered.
public enum CafeteriaShop
{
    Restaurant = 0,   // legacy id: an old generic restaurant anchor; migrated on load
    WaterBar1F = 1,   // 1F 水吧（饮品）
    FruitStall3F = 2, // 3F 水果摊（水果，按份）
    Starter1F = 3,    // 1F 1号档口 · 精美冷盘
    Wok1F = 4,        // 1F 2号档口 · 经典小炒
    Noodle1F = 5,     // 1F 3号档口 · 招牌现煮面
    Steamed1F = 6,    // 1F 4号档口 · 蒸点主食
    Sichuan2F = 7,    // 2F 1号档口 · 川湘热炒
    Claypot2F = 8,    // 2F 2号档口 · 瓦罐砂锅
    Teppan2F = 9,     // 2F 3号档口 · 铁板炙烤
    Pastry2F = 10,    // 2F 4号档口 · 风味面点
    Roast3F = 11,     // 3F 3号档口 · 广式烧味与铁板
    HotDish3F = 12,   // 3F 2号档口 · 精品热炒与主食
    Soup3F = 13       // 3F 1号档口 · 砂锅原汤与蒸点
}

public enum CafeteriaBusiness
{
    Meal = 0,    // 点餐 -> 备餐倒计时 -> 取餐 -> 落座用餐 -> 还餐盘
    Drink = 1,   // 即时购买，交付饮品后可直接饮用
    Fruit = 2    // 即时购买，交付水果或果盘后可直接享用
}

// One selling point: identity, location, merchandise type, menu pool and the scene
// anchors it owns. Everything the UI shows or the player interacts with is looked up
// through this table, never by name matching or array order.
[Serializable]
public class CafeteriaShopDef
{
    public CafeteriaShop id;
    public string shortName;       // "水吧" / "水果摊" / "1号档口 · 精美冷盘"
    public int floor;              // 1..3
    public CafeteriaBusiness business;
    public string poolId;          // which dish pool the daily menu is rolled from
    public bool drinksOnly;        // pool only contains drinks (kept for display rules)
    public string stallSignName;   // the 3D sign above the counter, used to verify the link

    public string FloorLabel => floor + "F";

    // "1F 水吧" — the label used in the corner HUD and in every panel header.
    public string PanelLabel => FloorLabel + " " + shortName;

    public string BusinessLabel
    {
        get
        {
            switch (business)
            {
                case CafeteriaBusiness.Drink: return "即时购买 · 饮品";
                case CafeteriaBusiness.Fruit: return "即时购买 · 按份水果";
                default: return "点餐 · 备餐后取餐";
            }
        }
    }

    public bool ReadyToEat => business != CafeteriaBusiness.Meal;
}

public static class CafeteriaShops
{
    public static readonly CafeteriaShopDef[] All =
    {
        new CafeteriaShopDef { id = CafeteriaShop.Starter1F, shortName = "1号档口 · 精美冷盘", floor = 1, business = CafeteriaBusiness.Meal, poolId = "starter", stallSignName = "1号档口 · 精美冷盘" },
        new CafeteriaShopDef { id = CafeteriaShop.Wok1F, shortName = "2号档口 · 经典小炒", floor = 1, business = CafeteriaBusiness.Meal, poolId = "wok", stallSignName = "2号档口 · 经典小炒" },
        new CafeteriaShopDef { id = CafeteriaShop.Noodle1F, shortName = "3号档口 · 招牌现煮面", floor = 1, business = CafeteriaBusiness.Meal, poolId = "noodle", stallSignName = "3号档口 · 招牌现煮面" },
        new CafeteriaShopDef { id = CafeteriaShop.Steamed1F, shortName = "4号档口 · 蒸点主食", floor = 1, business = CafeteriaBusiness.Meal, poolId = "steamed", stallSignName = "4号档口 · 蒸点主食" },
        new CafeteriaShopDef { id = CafeteriaShop.WaterBar1F, shortName = "水吧", floor = 1, business = CafeteriaBusiness.Drink, poolId = "waterbar", drinksOnly = true, stallSignName = "1F 水吧" },

        new CafeteriaShopDef { id = CafeteriaShop.Sichuan2F, shortName = "1号档口 · 川湘热炒", floor = 2, business = CafeteriaBusiness.Meal, poolId = "sichuan", stallSignName = "1号档口 · 川湘热炒" },
        new CafeteriaShopDef { id = CafeteriaShop.Claypot2F, shortName = "2号档口 · 瓦罐砂锅", floor = 2, business = CafeteriaBusiness.Meal, poolId = "claypot", stallSignName = "2号档口 · 瓦罐砂锅" },
        new CafeteriaShopDef { id = CafeteriaShop.Teppan2F, shortName = "3号档口 · 铁板炙烤", floor = 2, business = CafeteriaBusiness.Meal, poolId = "teppan", stallSignName = "3号档口 · 铁板炙烤" },
        new CafeteriaShopDef { id = CafeteriaShop.Pastry2F, shortName = "4号档口 · 风味面点", floor = 2, business = CafeteriaBusiness.Meal, poolId = "pastry", stallSignName = "4号档口 · 风味面点" },

        new CafeteriaShopDef { id = CafeteriaShop.Soup3F, shortName = "1号档口 · 砂锅原汤与蒸点", floor = 3, business = CafeteriaBusiness.Meal, poolId = "soup3", stallSignName = "1号档口 · 砂锅原汤与蒸点" },
        new CafeteriaShopDef { id = CafeteriaShop.HotDish3F, shortName = "2号档口 · 精品热炒与主食", floor = 3, business = CafeteriaBusiness.Meal, poolId = "hotdish3", stallSignName = "2号档口 · 精品热炒与主食" },
        new CafeteriaShopDef { id = CafeteriaShop.Roast3F, shortName = "3号档口 · 广式烧味与铁板", floor = 3, business = CafeteriaBusiness.Meal, poolId = "roast3", stallSignName = "3号档口 · 广式烧味与铁板" },
        new CafeteriaShopDef { id = CafeteriaShop.FruitStall3F, shortName = "水果摊", floor = 3, business = CafeteriaBusiness.Fruit, poolId = "fruit", stallSignName = "3F 水果摊" }
    };

    static readonly Dictionary<CafeteriaShop, CafeteriaShopDef> byId = BuildIndex();
    static readonly Dictionary<string, CafeteriaShopDef> byKey = BuildKeyIndex();

    static Dictionary<CafeteriaShop, CafeteriaShopDef> BuildIndex()
    {
        var d = new Dictionary<CafeteriaShop, CafeteriaShopDef>();
        foreach (var s in All) d[s.id] = s;
        return d;
    }

    // Lower-case enum names are used in object names so anchors are self-describing.
    static Dictionary<string, CafeteriaShopDef> BuildKeyIndex()
    {
        var d = new Dictionary<string, CafeteriaShopDef>();
        foreach (var s in All) d[s.id.ToString().ToLowerInvariant()] = s;
        return d;
    }

    public static CafeteriaShopDef Get(CafeteriaShop id)
    {
        CafeteriaShopDef def;
        return byId.TryGetValue(id, out def) ? def : null;
    }

    public static bool TryParseKey(string key, out CafeteriaShop id)
    {
        CafeteriaShopDef def;
        if (!string.IsNullOrEmpty(key) && byKey.TryGetValue(key.Trim().ToLowerInvariant(), out def))
        {
            id = def.id;
            return true;
        }
        id = CafeteriaShop.Starter1F;
        return false;
    }

    public static IEnumerable<CafeteriaShopDef> OnFloor(int floor)
    {
        foreach (var s in All) if (s.floor == floor) yield return s;
    }

    public static string Label(CafeteriaShop id)
    {
        var def = Get(id);
        return def != null ? def.PanelLabel : "食堂";
    }

    public static int Seed(CafeteriaShop id)
    {
        // Stable per-shop seed so two shops on the same floor never roll the same list.
        return 9001 + ((int)id + 1) * 137;
    }
}
