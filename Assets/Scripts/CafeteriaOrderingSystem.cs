using System;
using System.Collections.Generic;
using UnityEngine;

// Cafeteria ordering: daily menu, allowance balance, order lifecycle and persistence.
//
// All money is stored as integer fen (1 yuan = 100 fen) so nothing drifts; the UI
// formats to ¥x.xx only for display. The menu is cached by local date, so closing the
// UI, leaving the building or restarting the game cannot re-roll the same day.
//
// State flow: None -> Preparing -> ReadyToCollect -> Carrying -> Eating -> TrayToReturn -> Done
public enum CafeteriaShop
{
    Restaurant = 0,   // 售餐窗口：菜品 + 饮品
    WaterBar = 1,     // 水吧：现调饮品
    FruitStall = 2    // 鲜果超市：水果与果切
}

// Marks an ordering anchor as belonging to one shop, so walking to the water bar opens
// the water bar menu rather than the restaurant menu.
public class CafeteriaCounter : MonoBehaviour
{
    public CafeteriaShop shop = CafeteriaShop.Restaurant;
}

// One shop's menu for one day. Every price is in fen and capped at ¥20 for the drink
// and fruit shops (the restaurant keeps its own lower dish cap).
[Serializable]
public class ShopMenu
{
    public string date;
    public List<string> names = new List<string>();
    public List<int> fen = new List<int>();
}

// One purchased order. Several can be in flight at once (one per counter), each with
// its own timer and progress.
[Serializable]
public class CafeteriaOrder
{
    public int id;
    public int shop;                 // CafeteriaShop it was bought at
    public List<string> items = new List<string>();
    public List<int> quantities = new List<int>();
    public List<int> unitFen = new List<int>();
    public int totalFen;
    public int state;                // CafeteriaOrderState
    public long readyTicks;          // wall-clock preparation deadline
    public float eatProgress;        // 0..1

    public bool IsActive =>
        state != (int)CafeteriaOrderState.None && state != (int)CafeteriaOrderState.Done;

    // Water bar and fruit stall hand out items that are consumed on the spot, so they
    // never produce a tray to return.
    public bool NeedsTrayReturn => (CafeteriaShop)shop == CafeteriaShop.Restaurant;

    public string ShopLabel
    {
        get
        {
            switch ((CafeteriaShop)shop)
            {
                case CafeteriaShop.WaterBar: return "水吧";
                case CafeteriaShop.FruitStall: return "水果摊";
                default: return "食堂";
            }
        }
    }

    public string ItemsSummary
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append("、");
                sb.Append(items[i]);
                if (quantities[i] > 1) sb.Append("×").Append(quantities[i]);
            }
            return sb.ToString();
        }
    }
}

[Serializable]
public class CafeteriaOrderingState
{
    public long balanceFen = 30000;
    public string menuDate;
    public List<string> dishNames = new List<string>();
    public List<int> dishFen = new List<int>();
    public List<string> drinkNames = new List<string>();
    public List<int> drinkFen = new List<int>();

    public int orderId;
    public int state;                 // mirrors the carried order, kept for old saves
    public List<CafeteriaOrder> orders = new List<CafeteriaOrder>();
    public List<string> items = new List<string>();
    public List<int> quantities = new List<int>();
    public List<int> unitFen = new List<int>();
    public int totalFen;
    public long readyTicks;
    public float eatProgress;
    public bool carrying;

    public ShopMenu waterBar = new ShopMenu();
    public ShopMenu fruitStall = new ShopMenu();
    public int orderShop;                 // CafeteriaShop the active order was bought at

    public const int MaxDishFen = 1000;    // ¥10.00 per dish
    public const int MaxDrinkFen = 2000;   // ¥20.00 per drink
}

public enum CafeteriaOrderState
{
    None = 0, Preparing = 1, ReadyToCollect = 2, Carrying = 3,
    Eating = 4, TrayToReturn = 5, Done = 6
}

public static class CafeteriaMenu
{
    static readonly string[] DishPool =
    {
        "番茄炒蛋", "土豆炖鸡", "鱼香肉丝", "麻婆豆腐", "红烧排骨", "宫保鸡丁",
        "清炒时蔬", "青椒肉丝", "番茄牛腩", "地三鲜", "酸菜鱼", "糖醋里脊",
        "手撕包菜", "红烧茄子", "蒜蓉西兰花", "肉末蒸蛋", "干煸豆角", "家常豆腐"
    };

    static readonly string[] DrinkPool =
    {
        "豆浆", "柠檬水", "酸梅汤", "绿豆汤", "热牛奶", "茉莉绿茶", "冰红茶", "橙汁"
    };

    static readonly string[] WaterBarPool =
    {
        "珍珠奶茶", "生椰拿铁", "鲜榨橙汁", "柠檬气泡水", "冰美式", "抹茶拿铁",
        "杨枝甘露", "桂花乌龙", "草莓奶昔", "冬瓜茶"
    };

    static readonly string[] FruitPool =
    {
        "鲜切西瓜", "草莓盒装", "蓝莓一盒", "阳光玫瑰", "香蕉一把", "砂糖橘",
        "红心火龙果", "芒果切块", "麒麟瓜片", "圣女果盒"
    };

    // Rolls (or restores) the menu for today. Idempotent within one day.
    public static void Ensure(CafeteriaOrderingState st, int dishCount, int drinkCount)
    {
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (st.menuDate == today && st.dishNames.Count > 0) return;

        st.menuDate = today;
        st.dishNames.Clear(); st.dishFen.Clear();
        st.drinkNames.Clear(); st.drinkFen.Clear();

        var dishes = new List<string>(DishPool);
        Shuffle(dishes);
        for (int i = 0; i < Mathf.Min(dishCount, dishes.Count); i++)
        {
            st.dishNames.Add(dishes[i]);
            st.dishFen.Add(UnityEngine.Random.Range(480, CafeteriaOrderingState.MaxDishFen + 1));
        }

        var drinks = new List<string>(DrinkPool);
        Shuffle(drinks);
        for (int i = 0; i < Mathf.Min(drinkCount, drinks.Count); i++)
        {
            st.drinkNames.Add(drinks[i]);
            st.drinkFen.Add(UnityEngine.Random.Range(200, CafeteriaOrderingState.MaxDrinkFen + 1));
        }
    }

    // Rolls (or restores) the water bar / fruit stall menu for today.
    public static void EnsureShop(CafeteriaOrderingState st, CafeteriaShop shop, int count)
    {
        ShopMenu menu = shop == CafeteriaShop.WaterBar ? st.waterBar
                      : shop == CafeteriaShop.FruitStall ? st.fruitStall : null;
        if (menu == null) return;

        string today = DateTime.Now.ToString("yyyy-MM-dd");
        if (menu.date == today && menu.names.Count > 0) return;

        menu.date = today;
        menu.names.Clear(); menu.fen.Clear();

        string[] pool = shop == CafeteriaShop.WaterBar ? WaterBarPool : FruitPool;
        var pick = new List<string>(pool);
        Shuffle(pick);

        for (int i = 0; i < Mathf.Min(count, pick.Count); i++)
        {
            menu.names.Add(pick[i]);
            // Both shops are capped at ¥20.00 per item.
            menu.fen.Add(UnityEngine.Random.Range(600, 2001));
        }
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}

// Runtime controller. All state access funnels through EnsureReady(), which loads the
// save on first use, so neither play mode nor editor tools can ever see a null state.
public class CafeteriaOrdering : MonoBehaviour
{
    [Header("Menu roll")]
    public int dishCount = 8;
    public int drinkCount = 4;
    public int waterBarCount = 6;
    public int fruitCount = 6;

    [Header("Allowance")]
    public int startingBalanceFen = 30000;

    [Header("Timings (seconds)")]
    public int prepareSeconds = 30;
    public int eatSeconds = 10;

    [Header("Scene anchors (wired by the cafeteria builder, one per floor)")]
    public Transform[] orderSpots;
    public Transform[] collectSpots;
    public Transform[] returnSpots;
    public Transform[] seatSpots;

    public const string PrefsKey = "CafeteriaOrdering";

    // Tests point this at a throwaway key so the player's real save is never touched.
    public static string PrefsKeyOverride;
    static string Key => string.IsNullOrEmpty(PrefsKeyOverride) ? PrefsKey : PrefsKeyOverride;

    CafeteriaOrderingState st;
    bool ready;
    CampusGame game;
    BuildingInteriorTravel travel;

    bool uiOpen;
    int selected = -1;
    bool seated;
    Vector3 preSeatPos;
    Quaternion preSeatRot;
    Transform currentSeat;
    GameObject trayVisual;

    public CafeteriaOrderState State
    {
        get
        {
            EnsureReady();
            var o = CarriedOrder;
            return o != null ? (CafeteriaOrderState)o.state : CafeteriaOrderState.None;
        }
    }
    public string BalanceText { get { EnsureReady(); return "生活费：¥" + (st.balanceFen / 100.0).ToString("F2"); } }
    public long BalanceFen { get { EnsureReady(); return st.balanceFen; } }
    public int OrderId { get { EnsureReady(); return st.orderId; } }
    public float EatProgress { get { EnsureReady(); var o = CarriedOrder; return o != null ? o.eatProgress : 0f; } }

    // Carrying a tray only applies to restaurant orders; drinks and fruit are consumed
    // on the spot and never produce one.
    public bool HasTray
    {
        get
        {
            EnsureReady();
            var o = CarriedOrder;
            return o != null && o.NeedsTrayReturn &&
                   (o.state == (int)CafeteriaOrderState.Carrying ||
                    o.state == (int)CafeteriaOrderState.Eating ||
                    o.state == (int)CafeteriaOrderState.TrayToReturn);
        }
    }
    public bool Seated { get { return seated; } }
    public bool UiOpen { get { return uiOpen; } }
    public Transform CurrentSeat { get { return currentSeat; } }
    public GameObject TrayVisual { get { return trayVisual; } }
    public int CartCount { get { EnsureReady(); return st.items.Count; } }
    public int DishCount { get { EnsureReady(); return st.dishNames.Count; } }
    public int DrinkCount { get { EnsureReady(); return st.drinkNames.Count; } }
    // The order currently in the player's hands (carried / being eaten / awaiting tray).
    // Only one can be held at a time; the others wait as ready-to-collect.
    public CafeteriaOrder CarriedOrder
    {
        get
        {
            EnsureReady();
            foreach (var o in st.orders)
                if (o.state == (int)CafeteriaOrderState.Carrying ||
                    o.state == (int)CafeteriaOrderState.Eating ||
                    o.state == (int)CafeteriaOrderState.TrayToReturn) return o;
            return null;
        }
    }

    public bool CanOrder { get { return CanOrderAt(CafeteriaShop.Restaurant); } }

    // Each counter can host one unfinished order, so several can run at the same time.
    public bool CanOrderAt(CafeteriaShop shop)
    {
        EnsureReady();
        foreach (var o in st.orders)
            if (o.shop == (int)shop && o.IsActive) return false;
        return true;
    }

    // The countdown is authoritative through readyTicks (persisted, wall-clock based).
    // Nothing used to move Preparing -> ReadyToCollect at runtime, so an order that had
    // counted down to 00:00 stayed Preparing forever and could never be collected.
    // Any ready-to-collect order the player is standing next to qualifies.
    public bool CanCollect { get { EnsureReady(); Tick(); return CollectableOrderHere() != null; } }

    // Promotes an order whose preparation window has elapsed. Idempotent, so calling it
    // from every accessor is safe; the transition saves once and notifies once.
    // Promotes every order whose preparation window has elapsed. Only the transitions
    // that actually happen are saved, and each one is announced once.
    public void Tick()
    {
        EnsureReady();
        bool changed = false;
        string announce = null;

        long now = DateTime.Now.Ticks;
        foreach (var o in st.orders)
        {
            if (o.state != (int)CafeteriaOrderState.Preparing) continue;
            if (o.readyTicks <= 0 || now < o.readyTicks) continue;

            o.state = (int)CafeteriaOrderState.ReadyToCollect;
            changed = true;
            if (announce == null) announce = o.ShopLabel + "的餐品已做好，请前往取餐处按 E 取餐";
        }

        if (changed)
        {
            Save();
            if (announce != null) Notify(announce);
        }
    }

    void Update() { Tick(); }

    public string Format(int fen) { return "¥" + (fen / 100.0).ToString("F2"); }

    public string OrderTotalText { get { EnsureReady(); return "合计：" + Format(CartTotal()); } }

    public string StatusText
    {
        get
        {
            EnsureReady();
            var o = CarriedOrder;
            if (o == null)
            {
                // Nothing in hand: describe the most relevant pending order instead.
                var ready = FirstReady();
                if (ready != null) return ready.ShopLabel + "餐品已做好，请前往取餐处";
                var prep = FirstPreparing();
                if (prep != null) return prep.ShopLabel + LabelOf(prep);
                return "";
            }
            return o.ShopLabel + LabelOf(o);
        }
    }

    public string DishName(int i) { EnsureReady(); return st.dishNames[i]; }
    public int DishFen(int i) { EnsureReady(); return st.dishFen[i]; }
    public string DrinkName(int i) { EnsureReady(); return st.drinkNames[i]; }
    public int DrinkFen(int i) { EnsureReady(); return st.drinkFen[i]; }
    public string CartDishName(int i) { EnsureReady(); return st.items.Count > i ? st.items[i] : ""; }
    public int CartQty(int i) { EnsureReady(); return st.items.Count > i ? st.quantities[i] : 0; }
    public int CartUnit(int i) { EnsureReady(); return st.items.Count > i ? st.unitFen[i] : 0; }

    public int CartTotal()
    {
        EnsureReady();
        int total = 0;
        for (int i = 0; i < st.items.Count; i++) total += st.quantities[i] * st.unitFen[i];
        return total;
    }

    // Paying is judged against the counter whose menu is open, not a global lock, so a
    // player with a running order at the restaurant can still buy a drink elsewhere.
    public bool CanPay
    {
        get
        {
            EnsureReady();
            return CanOrderAt(ActiveShop) && st.items.Count > 0 && CartTotal() <= st.balanceFen;
        }
    }

    void Awake() { EnsureReady(); }

    // The ordering component lives in the additively-loaded interior scene, while
    // CampusGame lives in the campus scene. Nothing calls SetGame at runtime, so the
    // reference is resolved lazily; without this every proximity check saw a null
    // player and the panel could never open.
    CampusGame EnsureGame()
    {
        if (!game) game = FindFirstObjectByType<CampusGame>();
        return game;
    }

    public void Init() { EnsureReady(); }

    void EnsureReady()
    {
        if (ready) return;
        ready = true;
        st = new CafeteriaOrderingState();
        Load();
        if (st.waterBar == null) st.waterBar = new ShopMenu();
        if (st.fruitStall == null) st.fruitStall = new ShopMenu();
        CafeteriaMenu.Ensure(st, dishCount, drinkCount);
        CafeteriaMenu.EnsureShop(st, CafeteriaShop.WaterBar, waterBarCount);
        CafeteriaMenu.EnsureShop(st, CafeteriaShop.FruitStall, fruitCount);
        Save();
    }

    // ---------------------------------------------------------------- persistence
    [Serializable]
    class Snapshot
    {
        public long bal;
        public string date;
        public string[] dn; public int[] dp;
        public string[] vn; public int[] vp;
        public int oid, state, total;
        public string[] iname; public int[] iq, iprice;
        public long ready;
        public float eat;
        public bool carry;
        public ShopMenu waterBar;
        public ShopMenu fruitStall;
        public int orderShop;                 // legacy single-order fields below
        public List<CafeteriaOrder> orders;   // current format
    }

    public void Save()
    {
        EnsureReady();
        var s = new Snapshot
        {
            bal = st.balanceFen, date = st.menuDate,
            dn = st.dishNames.ToArray(), dp = st.dishFen.ToArray(),
            vn = st.drinkNames.ToArray(), vp = st.drinkFen.ToArray(),
            oid = st.orderId, state = st.state, total = st.totalFen,
            iname = st.items.ToArray(), iq = st.quantities.ToArray(), iprice = st.unitFen.ToArray(),
            ready = st.readyTicks, eat = st.eatProgress, carry = st.carrying,
            waterBar = st.waterBar, fruitStall = st.fruitStall, orderShop = st.orderShop,
            orders = st.orders
        };
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(s));
        PlayerPrefs.Save();
    }

    public void Load()
    {
        if (!PlayerPrefs.HasKey(Key))
        {
            st.balanceFen = startingBalanceFen;
            return;
        }

        var s = JsonUtility.FromJson<Snapshot>(PlayerPrefs.GetString(Key));
        if (s == null) { st.balanceFen = startingBalanceFen; return; }

        st.balanceFen = Mathf.Max(0, (int)s.bal);
        st.menuDate = s.date ?? "";
        st.dishNames = new List<string>(s.dn ?? new string[0]);
        st.dishFen = new List<int>(s.dp ?? new int[0]);
        st.drinkNames = new List<string>(s.vn ?? new string[0]);
        st.drinkFen = new List<int>(s.vp ?? new int[0]);
        st.orderId = s.oid;
        st.state = s.state;
        st.items = new List<string>(s.iname ?? new string[0]);
        st.quantities = new List<int>(s.iq ?? new int[0]);
        st.unitFen = new List<int>(s.iprice ?? new int[0]);
        st.totalFen = s.total;
        st.readyTicks = s.ready;
        st.eatProgress = s.eat;
        st.carrying = s.carry;
        st.orders = s.orders ?? new List<CafeteriaOrder>();
        for (int i = st.orders.Count - 1; i >= 0; i--)
            if (st.orders[i] == null) st.orders.RemoveAt(i);

        // Older saves stored a single order in flat fields; keep that order rather than
        // dropping something the player paid for.
        if (st.orders.Count == 0 && (s.state == (int)CafeteriaOrderState.Preparing ||
                                     s.state == (int)CafeteriaOrderState.ReadyToCollect ||
                                     s.state == (int)CafeteriaOrderState.Carrying ||
                                     s.state == (int)CafeteriaOrderState.Eating ||
                                     s.state == (int)CafeteriaOrderState.TrayToReturn))
        {
            var legacy = new CafeteriaOrder
            {
                id = s.oid, shop = s.orderShop, totalFen = s.total,
                state = s.state, readyTicks = s.ready, eatProgress = s.eat
            };
            legacy.items = new List<string>(s.iname ?? new string[0]);
            legacy.quantities = new List<int>(s.iq ?? new int[0]);
            legacy.unitFen = new List<int>(s.iprice ?? new int[0]);
            st.orders.Add(legacy);
        }

        st.waterBar = s.waterBar ?? new ShopMenu();
        st.fruitStall = s.fruitStall ?? new ShopMenu();
        st.orderShop = s.orderShop;
        if (st.waterBar.names == null) st.waterBar.names = new List<string>();
        if (st.waterBar.fen == null) st.waterBar.fen = new List<int>();
        if (st.fruitStall.names == null) st.fruitStall.names = new List<string>();
        if (st.fruitStall.fen == null) st.fruitStall.fen = new List<int>();

        // Repairs per order. A seat cannot survive a reload, so an order that was being
        // eaten resumes as being carried with its progress kept.
        long nowTicks = DateTime.Now.Ticks;
        foreach (var o in st.orders)
        {
            if (o.state == (int)CafeteriaOrderState.Eating)
                o.state = o.eatProgress >= 1f
                    ? (int)CafeteriaOrderState.TrayToReturn
                    : (int)CafeteriaOrderState.Carrying;

            if (o.state == (int)CafeteriaOrderState.Preparing &&
                o.readyTicks > 0 && nowTicks >= o.readyTicks)
                o.state = (int)CafeteriaOrderState.ReadyToCollect;
        }

        // Keep the legacy mirror roughly consistent for anything still reading it.
        var mirror = CarriedOrder;
        st.state = mirror != null ? mirror.state : (int)CafeteriaOrderState.None;
        // A "done" order with no items is meaningless; treat it as finished cleanly.
        if (st.state == (int)CafeteriaOrderState.Done && st.items.Count == 0)
            st.state = (int)CafeteriaOrderState.None;
    }

    // ---------------------------------------------------------------- cart
    public void CartAdd(string name, int unitFen)
    {
        EnsureReady();
        if (!CanOrderAt(ActiveShop)) return;   // the open counter, not the restaurant
        int i = st.items.IndexOf(name);
        if (i >= 0) st.quantities[i]++;
        else { st.items.Add(name); st.quantities.Add(1); st.unitFen.Add(unitFen); }
        Save();
    }

    public void CartChange(int index, int delta)
    {
        EnsureReady();
        if (!CanOrderAt(ActiveShop) || index < 0 || index >= st.items.Count) return;
        st.quantities[index] += delta;
        if (st.quantities[index] <= 0)
        {
            st.items.RemoveAt(index); st.quantities.RemoveAt(index); st.unitFen.RemoveAt(index);
        }
        Save();
    }

    public void CartRemove(int index)
    {
        EnsureReady();
        if (!CanOrderAt(ActiveShop) || index < 0 || index >= st.items.Count) return;
        st.items.RemoveAt(index); st.quantities.RemoveAt(index); st.unitFen.RemoveAt(index);
        Save();
    }

    // ---------------------------------------------------------------- actions
    // Payment. The UI only enables the button when CanPay, so repeated clicks cannot
    // double-charge; this also re-checks and refuses.
    public bool TryPay()
    {
        EnsureReady();
        if (!CanPay) return false;

        int total = CartTotal();

        // Balance and the new order are written in the same save so a crash can never
        // leave the money gone without the order.
        st.balanceFen -= total;

        var order = new CafeteriaOrder
        {
            id = ++st.orderId,
            shop = (int)ActiveShop,
            totalFen = total,
            state = (int)CafeteriaOrderState.Preparing,
            readyTicks = DateTime.Now.AddSeconds(prepareSeconds).Ticks,
            eatProgress = 0f
        };
        foreach (var it in st.items) order.items.Add(it);
        foreach (var q in st.quantities) order.quantities.Add(q);
        foreach (var u in st.unitFen) order.unitFen.Add(u);

        st.orders.Add(order);

        // The basket is empty again so another counter can be used right away.
        st.items.Clear(); st.quantities.Clear(); st.unitFen.Clear();
        st.totalFen = 0;

        Save();
        return true;
    }

    // The ready order whose counter the player is standing at (floor-aware).
    public CafeteriaOrder FirstReady()
    {
        EnsureReady();
        foreach (var o in st.orders)
            if (o.state == (int)CafeteriaOrderState.ReadyToCollect) return o;
        return null;
    }

    public CafeteriaOrder FirstPreparing()
    {
        EnsureReady();
        foreach (var o in st.orders)
            if (o.state == (int)CafeteriaOrderState.Preparing) return o;
        return null;
    }

    // Shared phrasing for one order's current step.
    public string LabelOf(CafeteriaOrder o)
    {
        if (o == null) return "";
        switch ((CafeteriaOrderState)o.state)
        {
            case CafeteriaOrderState.Preparing:
                int remain = Mathf.CeilToInt((float)(new DateTime(o.readyTicks) - DateTime.Now).TotalSeconds);
                remain = Mathf.Max(0, remain);
                return "正在备餐 " + (remain / 60).ToString("00") + ":" + (remain % 60).ToString("00");
            case CafeteriaOrderState.ReadyToCollect: return "已做好，请去取餐";
            case CafeteriaOrderState.Carrying: return o.NeedsTrayReturn ? "已取餐，请找座位" : "已取餐，可直接享用";
            case CafeteriaOrderState.Eating: return "正在享用";
            case CafeteriaOrderState.TrayToReturn: return "用餐结束，请还餐盘";
            default: return "";
        }
    }

    // Orders shown in the corner panel, newest last.
    public int OrderCount { get { EnsureReady(); return st.orders.Count; } }
    public CafeteriaOrder OrderAt(int i)
    {
        EnsureReady();
        return i >= 0 && i < st.orders.Count ? st.orders[i] : null;
    }

    // True when a counter of the given shop still has an unfinished order.
    public bool ShopBusy(CafeteriaShop shop)
    {
        EnsureReady();
        foreach (var o in st.orders)
            if (o.shop == (int)shop && o.IsActive) return true;
        return false;
    }

    public CafeteriaOrder CollectableOrderHere()
    {
        EnsureReady();
        var shop = NearestOrderShop();
        // Exact counter match first, so a drink is picked up at the water bar and a meal
        // at the serving window even when several orders are ready at once.
        foreach (var o in st.orders)
            if (o.state == (int)CafeteriaOrderState.ReadyToCollect && (CafeteriaShop)o.shop == shop)
                return o;
        return null;
    }

    public bool TryCollect()
    {
        EnsureReady();
        var order = CollectableOrderHere();
        if (order == null) return false;
        if (CarriedOrder != null) return false;      // hands are full

        order.state = (int)CafeteriaOrderState.Carrying;
        Save();
        return true;
    }

    public bool CanStartEating
    {
        get
        {
            EnsureReady();
            var o = CarriedOrder;
            return o != null && o.state == (int)CafeteriaOrderState.Carrying;
        }
    }

    // Drinks and fruit are consumed standing up; only a restaurant meal needs a seat.
    public bool CarriedNeedsSeat
    {
        get
        {
            EnsureReady();
            var o = CarriedOrder;
            return o != null && o.NeedsTrayReturn;
        }
    }

    // Sitting promotes only the carried order, and standing back up must not undo a
    // finished meal.
    public void SetEating(bool on)
    {
        EnsureReady();
        var o = CarriedOrder;
        if (o == null) return;

        if (on)
        {
            if (o.state == (int)CafeteriaOrderState.Carrying)
                o.state = (int)CafeteriaOrderState.Eating;
        }
        else if (o.state == (int)CafeteriaOrderState.Eating)
        {
            o.state = o.eatProgress >= 1f
                ? (int)CafeteriaOrderState.TrayToReturn
                : (int)CafeteriaOrderState.Carrying;
        }
        Save();
    }

    public void AddEatProgress(float delta)
    {
        EnsureReady();
        var o = CarriedOrder;
        if (o == null) return;

        o.eatProgress = Mathf.Clamp01(o.eatProgress + delta);
        if (o.eatProgress < 1f) { Save(); return; }

        // Drinks and fruit are finished on the spot: no tray, so the order closes itself.
        if (!o.NeedsTrayReturn)
        {
            o.state = (int)CafeteriaOrderState.Done;
            st.orders.Remove(o);
        }
        else
        {
            o.state = (int)CafeteriaOrderState.TrayToReturn;
        }
        Save();
    }

    // True when the carried order finished eating but still owes a tray.
    public bool AwaitingTrayReturn
    {
        get
        {
            EnsureReady();
            var o = CarriedOrder;
            return o != null && o.state == (int)CafeteriaOrderState.TrayToReturn;
        }
    }

    public bool TryReturnTray()
    {
        EnsureReady();
        var o = CarriedOrder;
        if (o == null) return false;
        if (o.state != (int)CafeteriaOrderState.TrayToReturn) return false;
        if (!o.NeedsTrayReturn) return false;

        st.orders.Remove(o);
        Save();
        return true;
    }

    // ---------------------------------------------------------------- seating
    public void BeginSeat(Transform seat, Vector3 sitPos, Quaternion sitRot)
    {
        seated = true; currentSeat = seat;
        if (EnsureGame() && game.player)
        {
            preSeatPos = game.player.position;
            preSeatRot = game.player.rotation;
            game.player.position = sitPos;
            game.player.rotation = sitRot;
        }
    }

    public Vector3 EndSeat()
    {
        seated = false; currentSeat = null;
        return preSeatPos;
    }

    // Standing pose recorded when the player sat down, so standing up returns to a spot
    // that was already walkable.
    public Quaternion LastStandRotation { get { return preSeatRot; } }

    // ---------------------------------------------------------------- scene queries
    // When set, proximity checks use this position instead of the live player. Used by
    // the editor verification to prove per-floor gating without entering play mode.
    [NonSerialized] public Vector3? TestPlayerPos;

    bool Near(Transform anchor, float radius)
    {
        if (!anchor) return false;

        Vector3 p;
        if (TestPlayerPos.HasValue) p = TestPlayerPos.Value;
        else if (EnsureGame() && game.player) p = game.player.position;
        else return false;

        return Vector2.Distance(new Vector2(p.x, p.z), new Vector2(anchor.position.x, anchor.position.z)) <= radius
            && Mathf.Abs(p.y - anchor.position.y) < 2.0f;
    }

    // World position of the closest pickup anchor on the player's floor, used by the
    // automated run so it stands where the game says the pickup point is.
    // Pickup anchor closest to a position, on the same floor. Pass a position that is
    // already next to the intended counter so the nearest hit is the right one.
    public Vector3? NearestCollectAnchorPos(Vector3 from)
    {
        EnsureReady();
        if (collectSpots == null) return null;

        Vector3 best = Vector3.zero;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < collectSpots.Length; i++)
        {
            var a = collectSpots[i];
            if (!a) continue;
            if (Mathf.Abs(from.y - a.position.y) >= 2.0f) continue;
            float sqr = (new Vector2(from.x, from.z) - new Vector2(a.position.x, a.position.z)).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = a.position; }
        }
        return bestSqr == float.MaxValue ? (Vector3?)null : best;
    }

    bool NearAny(Transform[] anchors, float radius)
    {
        if (anchors == null) return false;
        for (int i = 0; i < anchors.Length; i++)
            if (Near(anchors[i], radius)) return true;
        return false;
    }

    // The counters are long, so the radius is generous; the height window inside Near()
    // is what keeps floors apart.
    public bool NearOrderSpot { get { return NearAny(orderSpots, 3.6f); } }
    public bool NearCollectSpot { get { return NearAny(collectSpots, 2.6f); } }
    public bool NearReturnSpot { get { return NearAny(returnSpots, 2.6f); } }
    public bool NearSeat { get { return NearAny(seatSpots, 2.4f); } }

    // Seat anchor closest to the player on this floor, used when sitting down.
    // Nearest seat on the player's own floor within reach. Returns null when nothing
    // valid is close, so callers never seat the player in mid-air.
    public Transform NearestSeatAnchor(float maxDistance = 2.6f)
    {
        if (!TryPlayerPos(out var p) || seatSpots == null) return null;

        Transform best = null;
        float bestSqr = maxDistance * maxDistance;
        for (int i = 0; i < seatSpots.Length; i++)
        {
            var a = seatSpots[i];
            if (!a) continue;
            if (Mathf.Abs(p.y - a.position.y) >= 2.0f) continue;   // other floor
            float sqr = (new Vector2(p.x, p.z) - new Vector2(a.position.x, a.position.z)).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = a; }
        }
        return best;
    }

    // True when this system should answer the E key at the player's current position.
    // Pose-only check: does an ordering action apply at this position? Independent of
    // scene/game references so it can be verified directly.
    public bool OwnsSpotAt(Vector3 p)
    {
        EnsureReady();
        var saved = TestPlayerPos;
        TestPlayerPos = p;
        bool owns =
            (NearReturnSpot && HasTray && EatProgress >= 1f) ||
            (NearCollectSpot && CanCollect) ||
            (NearSeat && State == CafeteriaOrderState.Carrying) ||
            NearOrderSpot;
        TestPlayerPos = saved;
        return owns;
    }

    bool TryPlayerPos(out Vector3 p)
    {
        if (TestPlayerPos.HasValue) { p = TestPlayerPos.Value; return true; }
        if (EnsureGame() && game.player) { p = game.player.position; return true; }
        p = Vector3.zero; return false;
    }

    // Which counter the player is standing at. Used both to decide that E opens the
    // panel and to pick which shop's menu to show.
    public CafeteriaShop NearestOrderShop()
    {
        EnsureReady();
        if (!TryPlayerPos(out var p) || orderSpots == null) return CafeteriaShop.Restaurant;

        Transform best = null; float bestSqr = float.MaxValue;
        for (int i = 0; i < orderSpots.Length; i++)
        {
            var a = orderSpots[i];
            if (!a) continue;
            if (Mathf.Abs(p.y - a.position.y) >= 2.0f) continue;
            float sqr = (new Vector2(p.x, p.z) - new Vector2(a.position.x, a.position.z)).sqrMagnitude;
            if (sqr > 3.6f * 3.6f) continue;
            if (sqr < bestSqr) { bestSqr = sqr; best = a; }
        }
        if (!best) return CafeteriaShop.Restaurant;
        var counter = best.GetComponent<CafeteriaCounter>();
        return counter ? counter.shop : CafeteriaShop.Restaurant;
    }

    // Shop whose menu the open panel is showing.
    public CafeteriaShop ActiveShop { get; private set; } = CafeteriaShop.Restaurant;

    public void SetActiveShop(CafeteriaShop shop) { ActiveShop = shop; }

    // ---- menu accessors for the water bar / fruit stall (single list each) ----
    ShopMenu ActiveMenu =>
        ActiveShop == CafeteriaShop.WaterBar ? st.waterBar
        : ActiveShop == CafeteriaShop.FruitStall ? st.fruitStall : null;

    public int ShopItemCount { get { EnsureReady(); var m = ActiveMenu; return m == null ? 0 : m.names.Count; } }
    public string ShopItemName(int i) { EnsureReady(); var m = ActiveMenu; return m != null && i < m.names.Count ? m.names[i] : ""; }
    public int ShopItemFen(int i) { EnsureReady(); var m = ActiveMenu; return m != null && i < m.fen.Count ? m.fen[i] : 0; }

    public string ShopTitle
    {
        get
        {
            switch (ActiveShop)
            {
                case CafeteriaShop.WaterBar: return "水吧菜单";
                case CafeteriaShop.FruitStall: return "鲜果超市";
                default: return "今日食堂菜单";
            }
        }
    }

    // Pose probes for verification: does an anchor of this kind cover a position?
    public bool NearOrderAt(Vector3 p) { var s = TestPlayerPos; TestPlayerPos = p; var r = NearOrderSpot; TestPlayerPos = s; return r; }
    public bool NearCollectAt(Vector3 p) { var s = TestPlayerPos; TestPlayerPos = p; var r = NearCollectSpot; TestPlayerPos = s; return r; }
    public bool NearReturnAt(Vector3 p) { var s = TestPlayerPos; TestPlayerPos = p; var r = NearReturnSpot; TestPlayerPos = s; return r; }
    public bool NearSeatAt(Vector3 p) { var s = TestPlayerPos; TestPlayerPos = p; var r = NearSeat; TestPlayerPos = s; return r; }
    public int SeatAnchorCount { get { EnsureReady(); return seatSpots == null ? 0 : seatSpots.Length; } }

    // Test helper: which shop a counter position maps to.
    public CafeteriaShop NearestOrderShopAtForTest(Vector3 p)
    {
        var saved = TestPlayerPos;
        TestPlayerPos = p;
        var shop = NearestOrderShop();
        TestPlayerPos = saved;
        return shop;
    }

    public bool HandlesCurrentSpot()
    {
        EnsureReady();
        if (!EnsureGame() || !game.Interiors || !game.Interiors.Inside) return false;
        var interior = game.Interiors.Interior;
        if (!interior || interior.buildingId != "cafeteria") return false;
        if (interior.NearExit(game.player.position)) return false;
        if (game.paused || uiOpen) return true;
        return OwnsSpotAt(game.player.position);
    }

    public void Notify(string msg) { if (EnsureGame()) game.Notify(msg); }
    public void SetUi(bool open) { uiOpen = open; if (EnsureGame()) game.SetUiCursor(open); }

    public void SetUiForTest(bool open) { uiOpen = open; }
    public void SetGame(CampusGame g) { game = g; }
    public CampusGame Game { get { return EnsureGame(); } }

    // ---------------------------------------------------------------- test hooks
    public string MenuDateForTest() { EnsureReady(); return st.menuDate; }
    public void SetMenuDateForTest(string d) { EnsureReady(); st.menuDate = d; Save(); }
    public void RefreshMenuForTest() { EnsureReady(); st.menuDate = ""; CafeteriaMenu.Ensure(st, dishCount, drinkCount); Save(); }
    public void ForceReadyForTest() { EnsureReady(); st.readyTicks = DateTime.Now.Ticks; st.state = (int)CafeteriaOrderState.ReadyToCollect; Save(); }
    public void SetReadyInPastForTest(int secondsAgo)
    {
        EnsureReady();
        st.readyTicks = DateTime.Now.AddSeconds(-secondsAgo).Ticks;
        foreach (var o in st.orders)
            if (o.state == (int)CafeteriaOrderState.Preparing)
            {
                o.readyTicks = st.readyTicks;
                o.state = (int)CafeteriaOrderState.ReadyToCollect;
            }
        Save();
    }

    public void SetBalanceForTest(long fen) { EnsureReady(); st.balanceFen = Mathf.Max(0, (int)fen); Save(); }
    // Reset to a clean, orderable state without touching balance or menus.
    public void ForceIdleForTest()
    {
        EnsureReady();
        st.state = (int)CafeteriaOrderState.None;
        st.items.Clear(); st.quantities.Clear(); st.unitFen.Clear();
        st.totalFen = 0; st.eatProgress = 0f; st.carrying = false;
        Save();
    }

    public void CartClearForTest() { EnsureReady(); st.items.Clear(); st.quantities.Clear(); st.unitFen.Clear(); Save(); }
}
