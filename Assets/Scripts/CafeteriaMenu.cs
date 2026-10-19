using System;
using System.Collections.Generic;
using UnityEngine;

// One shop's menu for one game day. Prices are integer fen.
[Serializable]
public class ShopMenu
{
    public string date;
    public List<string> names = new List<string>();
    public List<int> fen = new List<int>();
    public List<string> units = new List<string>();   // "份" / "杯" / "碗" / "盒"

    public bool HasFor(string date)
    {
        return this.date == date && names != null && names.Count > 0 && fen != null && fen.Count >= names.Count;
    }

    public void Clear()
    {
        date = null;
        names.Clear(); fen.Clear(); units.Clear();
    }
}

// Daily menus, one per shop, rolled from category pools with a deterministic seed.
//
// The seed is (shop id, game day). Unity's global Random is never touched, so opening the
// menu twice, walking in and out of the cafeteria or restarting the game on the same
// game day produces exactly the same items at exactly the same prices.
public static class CafeteriaMenu
{
    // Price rules. All in fen; change these constants to retune the whole game.
    public const int DishMinFen = 100, DishMaxFen = 1000;     // 菜品：>0 且 ≤ ¥10
    public const int DrinkMinFen = 300, DrinkMaxFen = 2000;   // 饮品：>0 且 ≤ ¥20
    public const int FruitMinFen = 200, FruitMaxFen = 1000;   // 水果：每份 >0 且 ≤ ¥10

    // Fruits are sold by the portion; the unit is shown next to every item.
    public const string FruitUnit = "份";

    struct PoolItem
    {
        public string name;
        public string unit;
        public PoolItem(string name, string unit) { this.name = name; this.unit = unit; }
    }

    static readonly Dictionary<string, PoolItem[]> Pools = new Dictionary<string, PoolItem[]>
    {
        ["starter"] = new[] { new PoolItem("凉拌黄瓜", "份"), new PoolItem("五香酱牛肉", "份"), new PoolItem("口水鸡", "份"), new PoolItem("凉拌木耳", "份"), new PoolItem("蒜泥白肉", "份"), new PoolItem("醋泡花生", "份"), new PoolItem("卤味拼盘", "份"), new PoolItem("皮蛋豆腐", "份") },
        ["wok"] = new[] { new PoolItem("鱼香肉丝", "份"), new PoolItem("宫保鸡丁", "份"), new PoolItem("番茄炒蛋", "份"), new PoolItem("青椒肉丝", "份"), new PoolItem("手撕包菜", "份"), new PoolItem("干煸豆角", "份"), new PoolItem("地三鲜", "份"), new PoolItem("糖醋里脊", "份"), new PoolItem("麻婆豆腐", "份") },
        ["noodle"] = new[] { new PoolItem("大工牛肉面", "碗"), new PoolItem("番茄鸡蛋面", "碗"), new PoolItem("雪菜肉丝面", "碗"), new PoolItem("酸辣粉", "碗"), new PoolItem("鲜虾馄饨", "碗"), new PoolItem("炸酱面", "碗"), new PoolItem("清汤手擀面", "碗") },
        ["steamed"] = new[] { new PoolItem("猪肉大葱包子", "份"), new PoolItem("虾仁烧麦", "份"), new PoolItem("白粥配小菜", "份"), new PoolItem("蒸饺", "份"), new PoolItem("红糖发糕", "份"), new PoolItem("玉米", "份"), new PoolItem("花卷", "份"), new PoolItem("卤蛋", "份") },
        ["sichuan"] = new[] { new PoolItem("水煮肉片", "份"), new PoolItem("剁椒鱼头", "份"), new PoolItem("辣子鸡丁", "份"), new PoolItem("小炒黄牛肉", "份"), new PoolItem("毛血旺", "份"), new PoolItem("蒜苔炒腊肉", "份"), new PoolItem("农家小炒肉", "份"), new PoolItem("干锅花菜", "份") },
        ["claypot"] = new[] { new PoolItem("瓦罐鸡汤", "罐"), new PoolItem("牛腩砂锅", "罐"), new PoolItem("排骨莲藕汤", "罐"), new PoolItem("砂锅豆腐", "罐"), new PoolItem("黄焖鸡米饭", "份"), new PoolItem("酸菜鱼砂锅", "罐") },
        ["teppan"] = new[] { new PoolItem("铁板黑椒牛柳", "份"), new PoolItem("铁板炒面", "份"), new PoolItem("铁板鸡排饭", "份"), new PoolItem("铁板鱿鱼", "份"), new PoolItem("铁板蛋包饭", "份"), new PoolItem("铁板炒饭", "份") },
        ["pastry"] = new[] { new PoolItem("生煎包", "份"), new PoolItem("葱油饼", "份"), new PoolItem("鲜肉月饼", "份"), new PoolItem("南瓜饼", "份"), new PoolItem("糯米烧麦", "份"), new PoolItem("韭菜盒子", "份"), new PoolItem("豆沙包", "份") },
        ["hotdish3"] = new[] { new PoolItem("红烧排骨", "份"), new PoolItem("土豆炖鸡", "份"), new PoolItem("番茄牛腩", "份"), new PoolItem("清炒时蔬", "份"), new PoolItem("酸菜鱼", "份"), new PoolItem("蒜蓉西兰花", "份"), new PoolItem("肉末蒸蛋", "份") },
        ["soup3"] = new[] { new PoolItem("砂锅鸡汤面", "碗"), new PoolItem("蒸蛋羹", "份"), new PoolItem("原汤牛腩面", "碗"), new PoolItem("小笼包", "份"), new PoolItem("菌菇汤", "碗"), new PoolItem("枣泥蒸糕", "份") },
        ["roast3"] = new[] { new PoolItem("广式叉烧饭", "份"), new PoolItem("蜜汁烤鸡腿饭", "份"), new PoolItem("烧肉双拼饭", "份"), new PoolItem("铁板牛河", "份"), new PoolItem("豉油鸡饭", "份"), new PoolItem("烧鹅濑粉", "碗") },

        ["waterbar"] = new[] { new PoolItem("珍珠奶茶", "杯"), new PoolItem("生椰拿铁", "杯"), new PoolItem("鲜榨橙汁", "杯"), new PoolItem("柠檬气泡水", "杯"), new PoolItem("冰美式", "杯"), new PoolItem("抹茶拿铁", "杯"), new PoolItem("杨枝甘露", "杯"), new PoolItem("桂花乌龙", "杯"), new PoolItem("草莓奶昔", "杯"), new PoolItem("冬瓜茶", "杯"), new PoolItem("茉莉绿茶", "杯"), new PoolItem("冰镇矿泉水", "瓶") },
        ["fruit"] = new[] { new PoolItem("红富士苹果", "份"), new PoolItem("砂糖橘", "份"), new PoolItem("鲜切西瓜", "份"), new PoolItem("水果拼盘", "份"), new PoolItem("阳光玫瑰葡萄", "份"), new PoolItem("香蕉", "份"), new PoolItem("圣女果盒", "份"), new PoolItem("芒果切块", "份"), new PoolItem("蓝莓盒装", "份"), new PoolItem("红心火龙果", "份") }
    };

    // Unit a shop sells in; falls back to 份 for anything unknown.
    public static string UnitFor(string poolId)
    {
        PoolItem[] pool;
        return Pools.TryGetValue(poolId, out pool) && pool.Length > 0 ? pool[0].unit : "份";
    }

    public static int CountFor(string poolId)
    {
        PoolItem[] pool;
        return Pools.TryGetValue(poolId, out pool) ? pool.Length : 0;
    }

    // Deterministic, date-stable roll of one shop's menu.
    public static void Roll(ShopMenu menu, CafeteriaShopDef shop, string gameDate, int maxItems = 6)
    {
        if (menu == null || shop == null) return;
        if (string.IsNullOrEmpty(gameDate)) gameDate = DateTime.Now.ToString("yyyy-MM-dd");

        PoolItem[] pool;
        if (!Pools.TryGetValue(shop.poolId, out pool)) pool = Pools["wok"];

        menu.Clear();
        menu.date = gameDate;

        // A small deterministic permutation, seeded only by the shop and the day.
        int seed = CafeteriaShops.Seed(shop.id) * 31 + (int)(StableHash(gameDate) & 0xFFFF);
        var indices = new List<int>();
        for (int i = 0; i < pool.Length; i++) indices.Add(i);
        var rng = new System.Random(seed);
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        int count = Mathf.Clamp(maxItems, 1, indices.Count);
        for (int k = 0; k < count; k++)
        {
            var item = pool[indices[k]];
            menu.names.Add(item.name);
            menu.units.Add(item.unit);
            menu.fen.Add(Price(shop, rng));
        }
    }

    static int Price(CafeteriaShopDef shop, System.Random rng)
    {
        switch (shop.business)
        {
            case CafeteriaBusiness.Drink:
                return Round5(rng.Next(DrinkMinFen, DrinkMaxFen + 1));
            case CafeteriaBusiness.Fruit:
                return Round5(rng.Next(FruitMinFen, FruitMaxFen + 1));
            default:
                return Round5(rng.Next(DishMinFen, DishMaxFen + 1));
        }
    }

    // Prices read like a real canteen board: multiples of 50 fen (¥x.50 / ¥x.00).
    static int Round5(int fen)
    {
        int r = Mathf.RoundToInt(fen / 50f) * 50;
        return Mathf.Clamp(r, 50, 2000);
    }

    static uint StableHash(string text)
    {
        uint h = 2166136261;
        foreach (char c in text) { h ^= c; h *= 16777619; }
        return h;
    }

    // Upper bound enforced on load, so a stale save can never show an out-of-range price.
    public static int ClampPrice(CafeteriaBusiness business, int fen)
    {
        switch (business)
        {
            case CafeteriaBusiness.Drink: return Mathf.Clamp(fen, 1, DrinkMaxFen);
            case CafeteriaBusiness.Fruit: return Mathf.Clamp(fen, 1, FruitMaxFen);
            default: return Mathf.Clamp(fen, 1, DishMaxFen);
        }
    }
}
