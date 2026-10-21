using UnityEngine;

// OnGUI layer for cafeteria ordering.
//
// One panel component, but every shop gets its OWN header, item list and purchase rule:
//   - meal counters  : 确认付款 -> 备餐倒计时 -> 取餐 -> 落座 -> 还餐盘
//   - water bar      : 确认购买 -> 立即可用（只卖饮品）
//   - fruit stall    : 确认购买 -> 立即可用（按份卖水果）
//
// The persistent corner HUD (CampusHud) owns the balance and the order list, so this
// panel never draws a second allowance or a status stack.
[RequireComponent(typeof(CafeteriaOrdering))]
public class CafeteriaOrderingUI : MonoBehaviour, ICampusOverlay
{
    CafeteriaOrdering sys;
    CampusGame game;
    Vector2 scroll;

    GUIStyle titleStyle, rowStyle, smallStyle, boxStyle, buttonStyle, headStyle;
    bool stylesInit;
    GameObject trayObj;
    bool mealDoneAnnounced;

    public bool OverlayVisible => sys && sys.UiOpen;
    public void DrawOverlay() { DrawOrderPanel(); }

    // ---------------------------------------------------------------- lifecycle
    void Awake()
    {
        sys = GetComponent<CafeteriaOrdering>();
        game = FindFirstObjectByType<CampusGame>();
        if (sys) sys.SetGame(game);
    }

    void OnEnable() { CampusOverlayHub.Register(this); }

    void OnDisable()
    {
        CampusOverlayHub.Unregister(this);
        if (sys && sys.UiOpen) sys.ClosePanel();
        if (trayObj) Destroy(trayObj);
    }

    void Update()
    {
        if (!sys) return;
        if (!game) game = FindFirstObjectByType<CampusGame>();

        if (sys.UiOpen)
        {
            // The panel is drawn by the shared painter; here we only handle its hotkeys.
            if (Input.GetKeyDown(KeyCode.Escape)) { CloseUI(); return; }
            return;
        }
        if (!sys.Seated) return;

        // Seated: only standing up is meaningful. Movement is frozen by CampusGame.Seated.
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Q)) { StandUp(); return; }

        if (sys.State == CafeteriaOrderState.Eating)
        {
            sys.AddEatProgress(Time.deltaTime / Mathf.Max(1f, sys.eatSeconds));
            if (sys.EatProgress >= 1f && !mealDoneAnnounced)
            {
                mealDoneAnnounced = true;
                RefreshTray();
                sys.Notify("用餐结束，请起身前往餐盘回收处。");
            }
        }
    }

    bool InCafeteria()
    {
        return game && game.Interiors && game.Interiors.Inside
            && game.Interiors.Interior && game.Interiors.Interior.buildingId == "cafeteria";
    }

    bool NearExit()
    {
        return game && game.player && game.Interiors && game.Interiors.Interior
            && game.Interiors.Interior.NearExit(game.player.position);
    }

    // ---------------------------------------------------------------- styles
    void InitStyles()
    {
        if (stylesInit) return;
        var f = game ? game.interfaceFont : null;
        if (!f) f = Resources.Load<Font>("CampusFont");

        titleStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, fontSize = 24, fontStyle = FontStyle.Bold };
        headStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, fontSize = 20, fontStyle = FontStyle.Bold };
        rowStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, fontSize = 21 };
        smallStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, fontSize = 18 };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };
        boxStyle = new GUIStyle(GUI.skin.box);
        if (f) { titleStyle.font = f; headStyle.font = f; rowStyle.font = f; smallStyle.font = f; buttonStyle.font = f; }

        titleStyle.normal.textColor = new Color(0.98f, 0.94f, 0.80f);
        headStyle.normal.textColor = new Color(0.72f, 0.86f, 0.98f);
        rowStyle.normal.textColor = Color.white;
        smallStyle.normal.textColor = new Color(0.78f, 0.82f, 0.88f);
        stylesInit = true;
    }

    // ---------------------------------------------------------------- E handling
    // One E press == exactly one ordered action. Returns true when the cafeteria system
    // consumed the press, so CampusGame never also runs exit/building/NPC interactions.
    public bool InteractOnce()
    {
        LastInteractBlock = null;
        if (!sys) { LastInteractBlock = "no system"; return false; }
        if (sys.UiOpen) return false;
        if (!InCafeteria() || NearExit()) return false;

        // 1) A meal already in hand -> sit down to eat (only needs a nearby chair).
        if (sys.CanStartEating && sys.CarriedNeedsSeat && sys.NearSeat)
        {
            StartEating();
            return true;
        }

        // 2) A finished order waiting at THIS counter wins the press: the player came to
        // pick it up, so E must hand over the food rather than reopen the menu. Once it is
        // collected (or if nothing is ready) the next E opens the counter's own menu.
        if (sys.CanCollect)
        {
            string msg;
            if (!sys.TryCollect(out msg)) { sys.Notify(msg); return true; }
            RefreshTray();
            sys.Notify(msg);
            return true;
        }

        // 3) Stand at a counter: that counter's shop, and only that shop.
        var shop = sys.ShopAtPlayer();
        if (shop.HasValue)
        {
            OpenPanel(shop.Value);
            return true;
        }

        var preparing = sys.PreparingOrderHere();
        if (preparing != null)
        {
            sys.Notify("【" + preparing.FloorShopLabel + "】" + sys.LabelOf(preparing) + "，稍后回来取餐");
            return true;
        }

        // 4) Tray return.
        if (sys.NearReturnSpot && sys.AwaitingTrayReturn)
        {
            if (sys.TryReturnTray())
            {
                SetTrayVisible(false);
                mealDoneAnnounced = false;
                sys.Notify("餐盘已归还，本次用餐完成");
                return true;
            }
        }

        LastInteractBlock = "no cafeteria anchor in range";
        return false;
    }

    public string LastInteractBlock;
    public string StatusText => sys ? sys.LastMessage : "";

    // ---------------------------------------------------------------- panel
    void OpenPanel(CafeteriaShop shop)
    {
        scroll = Vector2.zero;
        sys.OpenPanel(shop);
        CampusOverlayHub.ConsumeInput();
    }

    public void CloseUI()
    {
        sys.ClosePanel();
        CampusOverlayHub.ConsumeInput();
    }

    void DrawOrderPanel()
    {
        if (!sys || !sys.UiOpen) return;
        InitStyles();

        float w = 620f, h = 660f;
        var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        var prevColor = GUI.color;
        GUI.color = new Color(0.075f, 0.085f, 0.10f, 1f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = prevColor;

        var def = sys.ActiveDef;

        // Header: floor + shop name, plus the shop's own purchase rule.
        GUI.Label(new Rect(box.x + 20f, box.y + 12f, w - 220f, 30f), sys.ShopTitle, titleStyle);
        GUI.Label(new Rect(box.x + 20f, box.y + 42f, w - 40f, 22f), sys.ShopSubtitle, smallStyle);
        GUI.Label(new Rect(box.x + w - 160f, box.y + 16f, 140f, 24f), "ESC 关闭", smallStyle);

        // Item list: only this shop's menu, with its unit and price.
        var list = new Rect(box.x + 16f, box.y + 72f, w - 32f, 340f);
        int count = sys.ShopItemCount;
        scroll = GUI.BeginScrollView(list, scroll, new Rect(0, 0, w - 52f, count * 32f + 10f));
        for (int i = 0; i < count; i++)
        {
            float y = i * 32f;
            string name = sys.ShopItemName(i);
            string unit = sys.ShopItemUnit(i);
            int fen = sys.ShopItemFen(i);

            GUI.Label(new Rect(6f, y, 230f, 26f), name, rowStyle);
            GUI.Label(new Rect(240f, y + 3f, 40f, 22f), "／" + unit, smallStyle);
            GUI.Label(new Rect(288f, y, 80f, 26f), CampusWallet.FormatYuan(fen), rowStyle);
            GUI.Label(new Rect(372f, y + 2f, 70f, 24f), "×" + sys.QtyInCart(name), smallStyle);
            var addRect = new Rect(446f, y + 2f, 52f, 24f);
            string addKey = "cafe:add:" + i;
            var screenAdd = new Rect(list.x + addRect.x, list.y + addRect.y - scroll.y, addRect.width, addRect.height);
            bool addClicked = GUI.Button(addRect, "加入", UIDrawRecorder.Style(addKey, buttonStyle, screenAdd));
            addClicked |= UIDrawRecorder.InjectingClick && UIDrawRecorder.ClickTarget == addKey;
            if (addClicked) sys.CartAdd(i);
        }
        GUI.EndScrollView();

        // Basket
        var cartY = box.y + 422f;
        GUI.Label(new Rect(box.x + 20f, cartY, 260f, 28f), "本次结算 · " + (def != null ? def.FloorLabel : ""), titleStyle);
        var cartRect = new Rect(box.x + 16f, cartY + 28f, w - 32f, 120f);
        GUI.Box(cartRect, "");

        if (sys.CartCount == 0)
        {
            GUI.Label(new Rect(cartRect.x + 12f, cartRect.y + 8f, cartRect.width - 24f, 22f),
                "尚未选择商品。", smallStyle);
        }
        else
        {
            float cy = cartRect.y + 6f;
            for (int i = 0; i < sys.CartCount && i < 4; i++)
            {
                string line = sys.CartName(i) + "  ×" + sys.CartQty(i) + "  = " + CampusWallet.FormatYuan(sys.CartUnitFen(i) * sys.CartQty(i));
                GUI.Label(new Rect(cartRect.x + 12f, cy, cartRect.width - 140f, 26f), line, rowStyle);
                if (GUI.Button(new Rect(cartRect.x + cartRect.width - 116f, cy - 1f, 24f, 20f), "−")) sys.CartChange(i, -1);
                if (GUI.Button(new Rect(cartRect.x + cartRect.width - 88f, cy - 1f, 24f, 20f), "+")) sys.CartChange(i, 1);
                if (GUI.Button(new Rect(cartRect.x + cartRect.width - 60f, cy - 1f, 48f, 20f), "移除")) sys.CartRemove(i);
                cy += 26f;
            }
        }

        // Totals and the pay button (its label states the actual rule).
        var footY = cartRect.y + cartRect.height + 12f;
        GUI.Label(new Rect(box.x + 20f, footY, 220f, 26f), sys.OrderTotalText, titleStyle);
        // Balance lives in the corner HUD only; here we show what this basket costs
        // against it, without a second allowance readout.
        GUI.Label(new Rect(box.x + 220f, footY + 4f, 240f, 24f), "生活费见右上角", smallStyle);

        bool payable = sys.CanPay;
        GUI.enabled = payable;
        string payLabel = sys.IsInstantShop ? "确认购买" : "确认付款 · 开始备餐";
        var payRect = new Rect(box.x + w - 230f, footY - 2f, 210f, 34f);
        var payScreen = new Rect(box.x + payRect.x, box.y + footY - 2f, payRect.width, payRect.height);
        var payStyle = UIDrawRecorder.Style("cafe:pay", buttonStyle, new Rect(payRect.x, footY - 2f, payRect.width, payRect.height));
        bool payClicked = GUI.Button(payRect, payLabel, payStyle);
        payClicked |= UIDrawRecorder.InjectingClick && UIDrawRecorder.ClickTarget == "cafe:pay";
        if (payClicked)
        {
            string msg;
            if (sys.TryPay(out msg))
            {
                sys.Notify(msg);
                CloseUI();
            }
            else sys.Notify(msg);
        }
        GUI.enabled = true;

        string block = sys.PayBlockReason;
        if (!string.IsNullOrEmpty(block))
            GUI.Label(new Rect(box.x + 20f, footY + 38f, w - 40f, 24f), block, smallStyle);

        // Held instant goods: use them straight from the shop.
        var held = sys.InstantOrderHeld(sys.ActiveShop);
        if (held != null)
        {
            float by = box.y + h - 76f;
            GUI.Label(new Rect(box.x + 20f, by, w - 200f, 24f), "已购：" + held.ItemsSummary, smallStyle);
            if (GUI.Button(new Rect(box.x + w - 160f, by - 4f, 140f, 30f), "立即享用", buttonStyle))
            {
                string msg;
                if (sys.ConsumeInstant(held, out msg)) sys.Notify(msg); else sys.Notify(msg);
            }
        }

        if (GUI.Button(new Rect(box.x + w - 146f, box.y + h - 40f, 126f, 30f), "关闭 (ESC)", buttonStyle))
            CloseUI();
        UIDrawRecorder.EndPass();
    }

    // Automated runs use the same branch the Q / Space key takes.
    public void StandUpForTest() { StandUp(); }

    // ---------------------------------------------------------------- eating
    const float HipDrop = 0.38f;

    void StartEating()
    {
        if (!sys.CanStartEating) return;
        var seat = sys.NearestSeatAnchor();
        if (!seat) { sys.Notify("附近没有可用座位，请走到餐桌旁的椅子前再按 E。"); return; }
        if (!game || !game.player) return;

        var cc = game.player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        game.Seated = true;

        Vector3 sitPos = new Vector3(seat.position.x, seat.position.y - HipDrop, seat.position.z);
        sys.BeginSeat(seat, sitPos, seat.rotation);
        sys.SetEating(true);
        mealDoneAnnounced = false;

        var pose = game.player.GetComponent<CafeteriaSitPose>();
        if (!pose) pose = game.player.gameObject.AddComponent<CafeteriaSitPose>();
        pose.active = true;

        game.AlignViewToSeat(seat.eulerAngles.y);
        Physics.SyncTransforms();
        RefreshTray();
    }

    void StandUp()
    {
        if (!sys.Seated || !game) return;
        bool finished = sys.EatProgress >= 1f;
        Vector3 standPos = sys.EndSeat();
        Quaternion standRot = sys.LastStandRotation;

        var pose = game.player ? game.player.GetComponent<CafeteriaSitPose>() : null;
        if (pose) pose.active = false;

        if (game.player)
        {
            game.player.position = standPos;
            game.player.rotation = standRot;
            game.AlignViewToSeat(standRot.eulerAngles.y);
            var cc = game.player.GetComponent<CharacterController>();
            if (cc) cc.enabled = true;
        }
        game.Seated = false;
        sys.SetEating(false);
        Physics.SyncTransforms();
        RefreshTray();

        if (finished) sys.Notify("用餐结束，请起身前往餐盘回收处。");
        else sys.Notify("已起身，进食进度已保留，可再次落座继续。");
    }

    // ---------------------------------------------------------------- tray visual
    void RefreshTray()
    {
        if (!sys || !sys.HasTray) { SetTrayVisible(false); return; }
        SetTrayVisible(true);
    }

    void SetTrayVisible(bool on)
    {
        if (trayObj) { Destroy(trayObj); trayObj = null; }
        if (!on || !game || !game.player) return;

        trayObj = new GameObject("Player_Tray");
        trayObj.transform.SetParent(game.player, false);
        if (sys.Seated)
        {
            trayObj.transform.localPosition = new Vector3(0f, 1.16f, 0.44f);
        }
        else
        {
            trayObj.transform.localPosition = new Vector3(0.36f, 1.05f, 0.18f);
        }
        trayObj.transform.localRotation = Quaternion.identity;
        BuildTrayMesh(trayObj.transform, sys.EatProgress >= 1f);
    }

    void BuildTrayMesh(Transform parent, bool empty)
    {
        var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tray.name = "TrayPlate";
        DestroyImmediate(tray.GetComponent<Collider>());
        tray.transform.SetParent(parent, false);
        tray.transform.localPosition = Vector3.zero;
        tray.transform.localScale = new Vector3(0.34f, 0.015f, 0.24f);
        tray.GetComponent<MeshRenderer>().sharedMaterial =
            new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.72f, 0.73f, 0.74f) };

        if (empty) return;
        string[] names = { "Dish_A", "Dish_B" };
        Vector3[] pos = { new Vector3(-0.10f, 0.03f, -0.03f), new Vector3(0.09f, 0.03f, -0.03f) };
        Vector3[] size = { new Vector3(0.15f, 0.035f, 0.12f), new Vector3(0.15f, 0.035f, 0.12f) };
        Color[] col = { new Color(0.75f, 0.45f, 0.28f), new Color(0.42f, 0.52f, 0.38f) };
        for (int i = 0; i < names.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = names[i];
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos[i];
            go.transform.localScale = size[i];
            go.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = col[i] };
        }
    }
}
