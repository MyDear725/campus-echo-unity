using System;
using System.Collections.Generic;
using UnityEngine;

// Cafeteria ordering controller.
//
// Money lives in CampusWallet, menus are rolled per shop per game day, and orders are a
// list rather than a single current order, so a meal at one counter, a second meal at
// another counter and a drink from the water bar all run independently.
//
// Order flow
//   meals : Preparing -> ReadyToCollect -> Carrying -> Eating -> TrayToReturn -> Done
//   drink / fruit : Bought (instant, goods already in the inventory) -> Consumed
//
// E ownership: the counter the player stands at is resolved from the scene anchors
// (CafeteriaCounter) on the player's own floor, nearest first. Only that counter's shop
// answers the E key. Nothing is matched by name, index or a default shop.
public class CafeteriaOrdering : MonoBehaviour
{
    [Header("Menu")]
    public int menuItemsPerShop = 6;

    [Header("Timings (seconds)")]
    public int prepareSeconds = 30;
    public int eatSeconds = 10;

    [Header("Interaction reach (metres)")]
    public float orderReach = 3.6f;
    public float collectReach = 2.6f;
    public float returnReach = 2.6f;
    public float seatReach = 2.4f;
    public float floorTolerance = 1.0f;

    [Header("Scene anchors (wired by the cafeteria builder)")]
    public Transform[] orderSpots;
    public Transform[] collectSpots;
    public Transform[] returnSpots;
    public Transform[] seatSpots;

    public const string PrefsKey = "CafeteriaOrdering";

    // Tests point this at a throwaway key so the player's real save is never touched.
    public static string PrefsKeyOverride;
    static string Key => string.IsNullOrEmpty(PrefsKeyOverride) ? PrefsKey : PrefsKeyOverride;

    // ---------------------------------------------------------------- state
    [Serializable]
    class Persisted
    {
        public string menuDate;
        public List<string> shops = new List<string>();          // shop keys
        public List<ShopMenu> menus = new List<ShopMenu>();
        public List<CafeteriaOrder> orders = new List<CafeteriaOrder>();
        public int lastOrderId;
    }

    Persisted st;
    bool ready;
    CampusGame game;
    string gameDate = "";

    // Basket belongs to the panel, not to a shop, so it is cleared whenever the shop
    // being ordered from changes.
    readonly List<string> cartNames = new List<string>();
    readonly List<int> cartQty = new List<int>();
    readonly List<int> cartFen = new List<int>();
    readonly List<string> cartUnit = new List<string>();

    bool uiOpen;
    CafeteriaShop activeShop = CafeteriaShop.Starter1F;
    bool seated;
    Vector3 preSeatPos;
    Quaternion preSeatRot;
    Transform currentSeat;
    string lastMessage = "";
    int lastMessageSeq;

    // ---------------------------------------------------------------- accessors
    public bool UiOpen => uiOpen;
    public bool Seated => seated;
    public Transform CurrentSeat => currentSeat;
    public CafeteriaShop ActiveShop => activeShop;
    public CafeteriaShopDef ActiveDef => CafeteriaShops.Get(activeShop);

    public long BalanceFen { get { EnsureReady(); return CampusWallet.BalanceFen; } }
    public string BalanceText { get { EnsureReady(); return CampusWallet.BalanceText; } }

    public int OrderCount { get { EnsureReady(); return st.orders.Count; } }
    public CafeteriaOrder OrderAt(int i)
    {
        EnsureReady();
        return i >= 0 && i < st.orders.Count ? st.orders[i] : null;
    }

    // The meal currently in the player's hands (only one at a time).
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

    public bool HasTray
    {
        get
        {
            var o = CarriedOrder;
            return o != null && o.NeedsTrayReturn;
        }
    }

    public CafeteriaOrderState State
    {
        get
        {
            var o = CarriedOrder;
            return o != null ? (CafeteriaOrderState)o.state : CafeteriaOrderState.None;
        }
    }

    public float EatProgress
    {
        get
        {
            var o = CarriedOrder;
            return o != null ? o.eatProgress : 0f;
        }
    }

    public bool AwaitingTrayReturn => State == CafeteriaOrderState.TrayToReturn;

    public bool CarriedNeedsSeat
    {
        get
        {
            var o = CarriedOrder;
            return o != null && o.NeedsTrayReturn;
        }
    }

    public static string FormatFen(int fen) { return CampusWallet.FormatYuan((long)fen); }
    public static string FormatFen(long fen) { return CampusWallet.FormatYuan(fen); }

    // ---------------------------------------------------------------- lifecycle
    public static CafeteriaOrdering Ensure(CampusGame owner)
    {
        CampusGame g = owner != null ? owner : FindFirstObjectByType<CampusGame>();
        if (g != null && g.Interiors != null && g.Interiors.Interior != null)
        {
            var inScene = g.Interiors.Interior.GetComponent<CafeteriaOrdering>();
            if (inScene != null) return inScene;
        }
        var any = FindFirstObjectByType<CafeteriaOrdering>(FindObjectsInactive.Include);
        return any;
    }

    void Awake()
    {
        game = FindFirstObjectByType<CampusGame>();
        EnsureReady();
    }

    void OnEnable() { CampusSystemsRegistry.RegisterOrdering(this); }
    void OnDisable() { CampusSystemsRegistry.UnregisterOrdering(this); }

    public void Init() { EnsureReady(); }

    public void SetGame(CampusGame g) { if (g) game = g; }

    public CampusGame Game
    {
        get
        {
            if (!game) game = FindFirstObjectByType<CampusGame>();
            return game;
        }
    }

    void EnsureReady()
    {
        if (ready) return;
        ready = true;
        st = new Persisted();
        Load();
        RefreshMenusIfNeeded();
    }

    string TodayGameDate()
    {
        var clock = GameClock.Ensure();
        return clock != null ? clock.Now.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd");
    }

    // Rolls every shop's menu when the game day changes, and only then. Opening a menu,
    // re-entering the cafeteria or restarting the same day never re-rolls.
    public void RefreshMenusIfNeeded()
    {
        string today = TodayGameDate();
        if (gameDate == today && st.menuDate == today && st.menus.Count == CafeteriaShops.All.Length) return;

        gameDate = today;
        if (st.menuDate != today || st.shops.Count != CafeteriaShops.All.Length)
        {
            st.shops.Clear(); st.menus.Clear();
            foreach (var def in CafeteriaShops.All)
            {
                var menu = new ShopMenu();
                CafeteriaMenu.Roll(menu, def, today, menuItemsPerShop);
                st.shops.Add(def.id.ToString().ToLowerInvariant());
                st.menus.Add(menu);
            }
            st.menuDate = today;
            Save();
        }
        else
        {
            // Same day: keep the stored menus, but guard against malformed data.
            for (int i = 0; i < st.menus.Count; i++)
                if (st.menus[i] == null || st.menus[i].names.Count == 0)
                {
                    var menu = new ShopMenu();
                    CafeteriaMenu.Roll(menu, CafeteriaShops.All[i], today, menuItemsPerShop);
                    st.menus[i] = menu;
                }
        }
    }

    public string MenuDate { get { EnsureReady(); return st.menuDate; } }

    public ShopMenu MenuFor(CafeteriaShop shop)
    {
        EnsureReady();
        RefreshMenusIfNeeded();
        int idx = Array.FindIndex(CafeteriaShops.All, d => d.id == shop);
        return idx >= 0 && idx < st.menus.Count ? st.menus[idx] : null;
    }

    // ---------------------------------------------------------------- menu accessors
    public string ShopTitle
    {
        get
        {
            var def = ActiveDef;
            return def != null ? def.PanelLabel : "食堂";
        }
    }

    public string ShopSubtitle
    {
        get
        {
            var def = ActiveDef;
            if (def == null) return "";
            switch (def.business)
            {
                case CafeteriaBusiness.Drink: return "现制饮品 · 只售饮品 · 确认后立即交付";
                case CafeteriaBusiness.Fruit: return "鲜果按份售卖 · 确认后立即交付";
                default: return "点餐 · 备餐完成后回到取餐点领取";
            }
        }
    }

    public int ShopItemCount { get { var m = MenuFor(activeShop); return m == null ? 0 : m.names.Count; } }
    public string ShopItemName(int i) { var m = MenuFor(activeShop); return m != null && i < m.names.Count ? m.names[i] : ""; }
    public int ShopItemFen(int i) { var m = MenuFor(activeShop); return m != null && i < m.fen.Count ? m.fen[i] : 0; }
    public string ShopItemUnit(int i) { var m = MenuFor(activeShop); return m != null && i < m.units.Count ? m.units[i] : "份"; }

    public bool IsInstantShop => ActiveDef != null && ActiveDef.business != CafeteriaBusiness.Meal;

    // ---------------------------------------------------------------- cart
    public int CartCount => cartNames.Count;
    public string CartName(int i) => i >= 0 && i < cartNames.Count ? cartNames[i] : "";
    public int CartQty(int i) => i >= 0 && i < cartQty.Count ? cartQty[i] : 0;
    public int CartUnitFen(int i) => i >= 0 && i < cartFen.Count ? cartFen[i] : 0;
    public string CartUnit(int i) => i >= 0 && i < cartUnit.Count ? cartUnit[i] : "份";

    public int CartTotalFen()
    {
        int total = 0;
        for (int i = 0; i < cartNames.Count; i++) total += cartQty[i] * cartFen[i];
        return total;
    }

    public string OrderTotalText => "合计：" + FormatFen(CartTotalFen());

    public int QtyInCart(string name)
    {
        int i = cartNames.IndexOf(name);
        return i >= 0 ? cartQty[i] : 0;
    }

    public void CartAdd(string name, int unitFen, string unit)
    {
        int i = cartNames.IndexOf(name);
        if (i >= 0) cartQty[i]++;
        else { cartNames.Add(name); cartQty.Add(1); cartFen.Add(unitFen); cartUnit.Add(string.IsNullOrEmpty(unit) ? "份" : unit); }
    }

    public void CartAdd(int index) { CartAdd(ShopItemName(index), ShopItemFen(index), ShopItemUnit(index)); }

    public void CartChange(int index, int delta)
    {
        if (index < 0 || index >= cartNames.Count) return;
        cartQty[index] += delta;
        if (cartQty[index] <= 0)
        {
            cartNames.RemoveAt(index); cartQty.RemoveAt(index); cartFen.RemoveAt(index); cartUnit.RemoveAt(index);
        }
    }

    public void CartRemove(int index)
    {
        if (index < 0 || index >= cartNames.Count) return;
        cartNames.RemoveAt(index); cartQty.RemoveAt(index); cartFen.RemoveAt(index); cartUnit.RemoveAt(index);
    }

    public void CartClear()
    {
        cartNames.Clear(); cartQty.Clear(); cartFen.Clear(); cartUnit.Clear();
    }

    // ---------------------------------------------------------------- payment
    public bool CanPay
    {
        get
        {
            if (cartNames.Count == 0) return false;
            return CartTotalFen() <= BalanceFen;
        }
    }

    public string PayBlockReason
    {
        get
        {
            if (cartNames.Count == 0) return "请先选择商品";
            long total = CartTotalFen();
            if (total > BalanceFen) return CampusWallet.ShortfallText(total);
            return "";
        }
    }

    // One atomic purchase. The balance is re-read here, so a double click that lands in
    // the same frame cannot charge twice: the first call empties the basket.
    public bool TryPay(out string message)
    {
        EnsureReady();
        message = "";
        if (cartNames.Count == 0) { message = "请先选择商品"; return false; }

        int total = CartTotalFen();
        if (total <= 0) { message = "订单金额无效"; return false; }
        if (CampusWallet.BalanceFen < total) { message = CampusWallet.ShortfallText(total); return false; }
        if (!CampusWallet.TrySpend(total)) { message = CampusWallet.ShortfallText(total); return false; }

        var def = ActiveDef ?? CafeteriaShops.All[0];
        var order = new CafeteriaOrder
        {
            id = ++st.lastOrderId,
            shop = (int)def.id,
            floor = def.floor,
            shopName = def.shortName,
            totalFen = total,
            placedDate = st.menuDate,
            eatProgress = 0f,
            collected = false,
            feedbackSeq = 0
        };
        for (int i = 0; i < cartNames.Count; i++)
        {
            order.items.Add(cartNames[i]);
            order.quantities.Add(cartQty[i]);
            order.unitFen.Add(cartFen[i]);
            order.units.Add(cartUnit[i]);
        }

        if (def.business == CafeteriaBusiness.Meal)
        {
            order.state = (int)CafeteriaOrderState.Preparing;
            order.readyTicks = DateTime.Now.AddSeconds(prepareSeconds).Ticks;
            order.pickupAnchor = "Collect_" + def.id.ToString().ToLowerInvariant();
        }
        else
        {
            // Instant purchase: the goods enter the shared inventory right now and the
            // order is recorded as bought and ready to be consumed. The charge happened
            // above, after the affordability check, so the player either gets both the
            // goods and the deduction or neither.
            order.state = (int)CafeteriaOrderState.Bought;
            order.readyTicks = DateTime.Now.Ticks;
            order.pickupAnchor = "Collect_" + def.id.ToString().ToLowerInvariant();
            for (int i = 0; i < cartNames.Count; i++) CampusInventory.Add(cartNames[i], cartQty[i]);
        }

        st.orders.Add(order);
        CartClear();
        Save();

        if (def.business == CafeteriaBusiness.Meal)
            message = "已下单：" + def.PanelLabel + " 共 " + FormatFen(total) + "，请稍候备餐（订单 #" + order.id + "）";
        else
            message = "购买成功：" + order.ItemsSummary + " 共 " + FormatFen(total) + "，已放入随身物品";

        lastMessage = message;
        lastMessageSeq = order.id;
        return true;
    }

    public bool TryPay() { string ignored; return TryPay(out ignored); }

    // ---------------------------------------------------------------- order progress
    public void Tick()
    {
        EnsureReady();
        RefreshMenusIfNeeded();
        long now = DateTime.Now.Ticks;
        bool changed = false;

        foreach (var o in st.orders)
        {
            if (o.state != (int)CafeteriaOrderState.Preparing) continue;
            if (o.readyTicks <= 0 || now < o.readyTicks) continue;
            o.state = (int)CafeteriaOrderState.ReadyToCollect;
            o.feedbackSeq++;
            lastMessage = "【" + o.FloorShopLabel + "】餐品已做好，请到取餐点按 E 取餐";
            lastMessageSeq = o.id;
            changed = true;
        }

        if (changed) Save();
    }

    void Update() { Tick(); }

    public string LabelOf(CafeteriaOrder o)
    {
        if (o == null) return "";
        switch ((CafeteriaOrderState)o.state)
        {
            case CafeteriaOrderState.Preparing:
                long remain = o.readyTicks - DateTime.Now.Ticks;
                int sec = Mathf.Max(0, Mathf.CeilToInt((float)(remain / (double)TimeSpan.TicksPerSecond)));
                return "备餐中 " + (sec / 60).ToString("00") + ":" + (sec % 60).ToString("00");
            case CafeteriaOrderState.ReadyToCollect: return "可取餐";
            case CafeteriaOrderState.Carrying: return o.NeedsTrayReturn ? "已取餐 · 请找座位" : "已取餐";
            case CafeteriaOrderState.Eating: return "用餐中";
            case CafeteriaOrderState.TrayToReturn: return "用餐结束 · 请还餐盘";
            case CafeteriaOrderState.Bought: return "已购买 · 可享用";
            case CafeteriaOrderState.Consumed: return "已享用";
            default: return "";
        }
    }

    // ---------------------------------------------------------------- anchors / shops
    bool TryPlayerPos(out Vector3 p)
    {
        if (TestPlayerPos.HasValue) { p = TestPlayerPos.Value; return true; }
        var g = Game;
        if (g && g.player) { p = g.player.position; return true; }
        p = Vector3.zero;
        return false;
    }

    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    public bool SameFloor(Vector3 p, Vector3 anchor) => Mathf.Abs(p.y - anchor.y) <= floorTolerance;

    // Anchor nearest to the player whose marker says it belongs to a shop. The marker,
    // not the object name, decides which shop answers; each anchor has exactly one.
    public Transform NearestOrderAnchor(Vector3 p, out CafeteriaShop shop, out float distance)
    {
        shop = CafeteriaShop.Starter1F;
        distance = float.MaxValue;
        Transform best = null;
        if (orderSpots == null) return null;

        for (int i = 0; i < orderSpots.Length; i++)
        {
            var a = orderSpots[i];
            if (!a || !a.gameObject.activeInHierarchy) continue;
            var marker = a.GetComponent<CafeteriaCounter>();
            if (!marker) continue;
            if (!SameFloor(p, a.position)) continue;

            float d = Flat(p, a.position);
            if (d > orderReach) continue;
            if (d < distance) { distance = d; best = a; shop = marker.shop; }
        }
        return best;
    }

    public bool NearOrderSpot
    {
        get
        {
            if (!TryPlayerPos(out var p)) return false;
            CafeteriaShop s; float d;
            return NearestOrderAnchor(p, out s, out d) != null;
        }
    }

    // Shop the player is standing at, or null when no counter is in reach.
    public CafeteriaShop? ShopAtPlayer()
    {
        if (!TryPlayerPos(out var p)) return null;
        CafeteriaShop s; float d;
        var a = NearestOrderAnchor(p, out s, out d);
        return a != null ? s : (CafeteriaShop?)null;
    }

    public CafeteriaShop NearestOrderShop() => ShopAtPlayer() ?? activeShop;

    // Collect anchors are named Collect_<shopKey>; a fallback keeps older scenes working
    // (a CollectSpot_<suffix> whose object/suffix contains the shop key).
    public Transform CollectAnchorFor(CafeteriaShop shop, Vector3 p)
    {
        if (collectSpots == null) return null;
        string key = shop.ToString().ToLowerInvariant();
        Transform exact = null;
        float exactSqr = float.MaxValue;

        for (int i = 0; i < collectSpots.Length; i++)
        {
            var a = collectSpots[i];
            if (a == null || !a.gameObject.activeInHierarchy) continue;
            if (!SameFloor(p, a.position)) continue;

            string an = a.name.ToLowerInvariant();
            if (an == "collect_" + key)
            {
                float d0 = Flat(p, a.position);
                if (d0 < exactSqr) { exactSqr = d0; exact = a; }
            }
        }
        return exact;
    }

    // Nearest collect anchor of any kind, used only for prompts/debug.
    public Transform NearestAnyCollectAnchor(Vector3 p)
    {
        if (collectSpots == null) return null;
        Transform best = null; float bestD = float.MaxValue;
        for (int i = 0; i < collectSpots.Length; i++)
        {
            var a = collectSpots[i];
            if (!a || !a.gameObject.activeInHierarchy || !SameFloor(p, a.position)) continue;
            float d = Flat(p, a.position);
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    public bool NearReturnSpot
    {
        get
        {
            if (!TryPlayerPos(out var p) || returnSpots == null) return false;
            for (int i = 0; i < returnSpots.Length; i++)
            {
                var a = returnSpots[i];
                if (a && a.gameObject.activeInHierarchy && SameFloor(p, a.position) && Flat(p, a.position) <= returnReach) return true;
            }
            return false;
        }
    }

    public Transform NearestSeatAnchor(float maxDistance = -1f)
    {
        if (!TryPlayerPos(out var p) || seatSpots == null) return null;
        float reach = maxDistance > 0 ? maxDistance : seatReach;
        Transform best = null; float bestD = reach;
        for (int i = 0; i < seatSpots.Length; i++)
        {
            var a = seatSpots[i];
            if (!a || !a.gameObject.activeInHierarchy || !SameFloor(p, a.position)) continue;
            float d = Flat(p, a.position);
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    public bool NearSeat => NearestSeatAnchor() != null;

    // A ready order that belongs to the counter the player is standing at.
    public CafeteriaOrder CollectableOrderHere()
    {
        if (!TryPlayerPos(out var p)) return null;
        for (int i = 0; i < st.orders.Count; i++)
        {
            var o = st.orders[i];
            if (o == null || o.state != (int)CafeteriaOrderState.ReadyToCollect) continue;
            var anchor = CollectAnchorFor(o.Shop, p);
            if (anchor != null && Flat(p, anchor.position) <= collectReach) return o;
        }
        return null;
    }

    public bool CanCollect { get { return CollectableOrderHere() != null; } }

    // True when the player stands at a pickup counter that has an order (ready or still
    // preparing), so the HUD and the E key can answer "here".
    public bool NearCollectSpot
    {
        get
        {
            if (!TryPlayerPos(out var p)) return false;
            for (int i = 0; i < st.orders.Count; i++)
            {
                var o = st.orders[i];
                if (o == null) continue;
                if (o.state != (int)CafeteriaOrderState.ReadyToCollect && o.state != (int)CafeteriaOrderState.Preparing) continue;
                var anchor = CollectAnchorFor(o.Shop, p);
                if (anchor != null && Flat(p, anchor.position) <= collectReach) return true;
            }
            return false;
        }
    }

    // Diagnostic for the automated run: where the player is and what each anchor reports.
    public string DescribeCollectStateAtPlayer()
    {
        if (!TryPlayerPos(out var p)) return "no player";
        var sb = new System.Text.StringBuilder();
        sb.Append("p=").Append(p.ToString("F2")).Append(" anchors=").Append(collectSpots == null ? 0 : collectSpots.Length);
        for (int i = 0; i < st.orders.Count; i++)
        {
            var o = st.orders[i];
            sb.Append(" [").Append(o.id).Append(' ').Append(o.Shop).Append(" st=").Append(o.state).Append(']');
        }
        return sb.ToString();
    }

    // The preparing order whose pickup counter the player stands at, for the
    // "还在准备中" answer instead of a silent no-op.
    public CafeteriaOrder PreparingOrderHere()
    {
        if (!TryPlayerPos(out var p)) return null;
        for (int i = 0; i < st.orders.Count; i++)
        {
            var o = st.orders[i];
            if (o == null || o.state != (int)CafeteriaOrderState.Preparing) continue;
            var anchor = CollectAnchorFor(o.Shop, p);
            if (anchor != null && Flat(p, anchor.position) <= collectReach) return o;
        }
        return null;
    }

    // Hands over one order. Refused when another order is already in the player's hands,
    // so a full tray never swallows an order and the ready one stays waiting.
    public bool TryCollect(out string message)
    {
        message = "";
        var order = CollectableOrderHere();
        if (order == null) { message = "这里没有可领取的订单"; return false; }
        var held = CarriedOrder;
        if (held != null)
        {
            message = "手里已有 " + held.FloorShopLabel + " 的餐品，请先用餐并归还餐盘";
            return false;
        }

        order.state = (int)CafeteriaOrderState.Carrying;
        order.collected = true;
        Save();
        message = "已取餐：" + order.FloorShopLabel + " · " + order.ItemsSummary;
        return true;
    }

    public bool TryCollect() { string ignored; return TryCollect(out ignored); }

    public bool CanStartEating => State == CafeteriaOrderState.Carrying;

    public bool TryReturnTray()
    {
        var o = CarriedOrder;
        if (o == null || o.state != (int)CafeteriaOrderState.TrayToReturn) return false;
        if (!o.NeedsTrayReturn) return false;
        st.orders.Remove(o);
        Save();
        var campus = FindFirstObjectByType<CampusGame>();
        if (campus && campus.Story) campus.Story.Reach("meal-returned");
        if (CampusSemester.Instance) CampusSemester.Instance.MealCompleted();
        return true;
    }

    public void SetEating(bool on)
    {
        var o = CarriedOrder;
        if (o == null) return;
        if (on)
        {
            if (o.state == (int)CafeteriaOrderState.Carrying) o.state = (int)CafeteriaOrderState.Eating;
        }
        else if (o.state == (int)CafeteriaOrderState.Eating)
        {
            o.state = o.eatProgress >= 1f ? (int)CafeteriaOrderState.TrayToReturn : (int)CafeteriaOrderState.Carrying;
        }
        Save();
    }

    // Eating advances every frame; the save is throttled so a meal does not write to
    // disk 60 times a second. Standing up, finishing and quitting always force a save.
    float eatSaveTimer;

    public void AddEatProgress(float delta)
    {
        var o = CarriedOrder;
        if (o == null) return;
        o.eatProgress = Mathf.Clamp01(o.eatProgress + delta);

        bool finished = o.eatProgress >= 1f && o.NeedsTrayReturn;
        if (finished) o.state = (int)CafeteriaOrderState.TrayToReturn;

        eatSaveTimer += Mathf.Max(0f, delta);
        if (finished || eatSaveTimer >= 0.25f) { eatSaveTimer = 0f; Save(); }
    }

    // Instant goods (drinks, fruit) are consumed from the real inventory.
    public bool ConsumeInstant(CafeteriaOrder order, out string message)
    {
        message = "";
        if (order == null || order.state != (int)CafeteriaOrderState.Bought) { message = "该订单不可享用"; return false; }
        for (int i = 0; i < order.items.Count; i++)
        {
            if (!CampusInventory.Consume(order.items[i], order.quantities[i]))
            {
                message = "物品已不在随身物品中";
                return false;
            }
        }
        order.state = (int)CafeteriaOrderState.Consumed;
        st.orders.Remove(order);
        Save();
        message = "已享用：" + order.ItemsSummary;
        return true;
    }

    public CafeteriaOrder InstantOrderHeld(CafeteriaShop shop)
    {
        EnsureReady();
        foreach (var o in st.orders)
            if (o.shop == (int)shop && o.state == (int)CafeteriaOrderState.Bought) return o;
        return null;
    }

    // ---------------------------------------------------------------- seating
    public void BeginSeat(Transform seat, Vector3 sitPos, Quaternion sitRot)
    {
        seated = true; currentSeat = seat;
        var g = Game;
        if (g && g.player)
        {
            preSeatPos = g.player.position;
            preSeatRot = g.player.rotation;
            g.player.position = sitPos;
            g.player.rotation = sitRot;
        }
    }

    public Vector3 EndSeat()
    {
        seated = false; currentSeat = null;
        return preSeatPos;
    }

    public Quaternion LastStandRotation => preSeatRot;
    public Vector3 LastStandPosition => preSeatPos;

    // ---------------------------------------------------------------- E ownership
    // True when the ordering system should answer the E key at the player's position.
    public bool HandlesCurrentSpot()
    {
        var g = Game;
        if (!g || g.Interiors == null || !g.Interiors.Inside) return false;
        var interior = g.Interiors.Interior;
        if (!interior || interior.buildingId != "cafeteria") return false;
        if (uiOpen) return true;
        if (interior.NearExit(g.player.position)) return false;
        return OwnsSpotAt(g.player.position);
    }

    public bool OwnsSpotAt(Vector3 p)
    {
        EnsureReady();
        var saved = TestPlayerPos;
        TestPlayerPos = p;
        CafeteriaShop s; float d;
        bool owns =
            (NearReturnSpot && AwaitingTrayReturn) ||
            CanCollect ||
            (NearestSeatAnchor() != null && State == CafeteriaOrderState.Carrying) ||
            NearestOrderAnchor(p, out s, out d) != null ||
            HeldInstantHere() != null;
        TestPlayerPos = saved;
        return owns;
    }

    CafeteriaOrder HeldInstantHere()
    {
        if (!TryPlayerPos(out var p)) return null;
        CafeteriaShop s; float d;
        var anchor = NearestOrderAnchor(p, out s, out d);
        if (!anchor) return null;
        return InstantOrderHeld(s);
    }

    // ---------------------------------------------------------------- UI plumbing
    public void OpenPanel(CafeteriaShop shop)
    {
        activeShop = shop;
        CartClear();
        RefreshMenusIfNeeded();
        uiOpen = true;
        var g = Game;
        if (g) g.SetUiCursor(true);
    }

    public void ClosePanel()
    {
        if (!uiOpen) return;
        uiOpen = false;
        var g = Game;
        if (g) g.SetUiCursor(false);
    }

    public void SetUi(bool open) { if (open) uiOpen = true; else ClosePanel(); }

    public void Notify(string msg)
    {
        lastMessage = msg;
        var g = Game;
        if (g) g.Notify(msg);
    }

    public string LastMessage => lastMessage;

    // ---------------------------------------------------------------- persistence
    public void Save()
    {
        st.menus ??= new List<ShopMenu>();
        st.orders ??= new List<CafeteriaOrder>();
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(st));
        PlayerPrefs.Save();
    }

    public void Load()
    {
        // A fresh profile starts with the configured allowance, recorded once in the wallet.
        st = new Persisted();
        string raw = PlayerPrefs.GetString(Key, "");
        if (string.IsNullOrEmpty(raw))
        {
            if (!PlayerPrefs.HasKey(CampusWallet.KeyOverride ?? CampusWallet.Key))
                CampusWallet.SetForTest(CampusWallet.DefaultStartingFen);
            return;
        }

        Persisted loaded = null;
        try { loaded = JsonUtility.FromJson<Persisted>(raw); }
        catch (Exception e) { Debug.LogWarning("[CafeteriaOrdering] save unreadable: " + e.Message); }
        if (loaded == null) return;

        st = loaded;
        st.menus ??= new List<ShopMenu>();
        st.orders ??= new List<CafeteriaOrder>();
        st.shops ??= new List<string>();

        // Repair orders written by older builds.
        for (int i = st.orders.Count - 1; i >= 0; i--)
        {
            var o = st.orders[i];
            if (o == null) { st.orders.RemoveAt(i); continue; }
            if (o.items == null) o.items = new List<string>();
            if (o.quantities == null) o.quantities = new List<int>();
            if (o.unitFen == null) o.unitFen = new List<int>();
            if (o.units == null) o.units = new List<string>();
            while (o.units.Count < o.items.Count) o.units.Add("份");
            if (o.floor <= 0) o.floor = CafeteriaShops.Get(o.Shop)?.floor ?? 1;
            if (o.state == (int)CafeteriaOrderState.Eating) { o.state = (int)CafeteriaOrderState.Carrying; o.eatProgress = 0f; }
        }

        // Orders that were already finished, or that lost their goods, are dropped.
        st.orders.RemoveAll(o => o.state == (int)CafeteriaOrderState.Done || o.state == (int)CafeteriaOrderState.Consumed);
    }

    // ---------------------------------------------------------------- test hooks
    [NonSerialized] public Vector3? TestPlayerPos;

    public void SetPlayerPosForTest(Vector3 p) { TestPlayerPos = p; }
    public void ClearPlayerPosForTest() { TestPlayerPos = null; }
    public void SetBalanceForTest(long fen) { CampusWallet.SetForTest(fen); }
    public int MenuItemCountFor(CafeteriaShop shop) { var m = MenuFor(shop); return m == null ? 0 : m.names.Count; }
    public string MenuItemNameFor(CafeteriaShop shop, int i) { var m = MenuFor(shop); return m != null && i < m.names.Count ? m.names[i] : ""; }
    public int MenuItemFenFor(CafeteriaShop shop, int i) { var m = MenuFor(shop); return m != null && i < m.fen.Count ? m.fen[i] : 0; }
    public void ForceReadyForTest(int orderId)
    {
        foreach (var o in st.orders)
            if (o.id == orderId && o.state == (int)CafeteriaOrderState.Preparing)
            {
                o.readyTicks = DateTime.Now.Ticks - 1;
                o.state = (int)CafeteriaOrderState.ReadyToCollect;
            }
        Save();
    }
    public List<CafeteriaOrder> OrdersForTest() => st.orders;
    public void ClearOrdersForTest() { st.orders.Clear(); }
    public void ClearCartForTest() { CartClear(); }
    public void SetOrdersForTest(int orderId) { st.lastOrderId = orderId; }
    public int LastOrderId => st.lastOrderId;
}

public enum CafeteriaOrderState
{
    None = 0, Preparing = 1, ReadyToCollect = 2, Carrying = 3,
    Eating = 4, TrayToReturn = 5, Done = 6, Bought = 7, Consumed = 8
}
