using UnityEngine;

// OnGUI layer for the cafeteria ordering flow: allowance readout, ordering panel,
// preparation countdown, eating progress bar, and the E prompts for each anchor.
//
// One key at a time: when the panel is open it consumes E/Escape, and CampusGame is
// already frozen via SetUiCursor, so no other interaction can fire.
[RequireComponent(typeof(CafeteriaOrdering))]
public class CafeteriaOrderingUI : MonoBehaviour
{
    CafeteriaOrdering sys;
    CampusGame game;
    Vector2 scroll;
    int selected = -1;

    GUIStyle titleStyle, rowStyle, smallStyle, boxStyle, buttonStyle;
    bool stylesInit;

    void Start()
    {
        sys = GetComponent<CafeteriaOrdering>();
        game = sys.Game;
    }

    void InitStyles()
    {
        if (stylesInit) return;
        var f = game ? game.interfaceFont : null;

        titleStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
        if (f) titleStyle.font = f;
        titleStyle.normal.textColor = new Color(0.98f, 0.94f, 0.80f);

        rowStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 17
        };
        if (f) rowStyle.font = f;
        rowStyle.normal.textColor = Color.white;

        smallStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 15
        };
        if (f) smallStyle.font = f;
        smallStyle.normal.textColor = new Color(0.85f, 0.88f, 0.90f);

        buttonStyle = new GUIStyle(GUI.skin.button);
        if (f) buttonStyle.font = f;
        buttonStyle.fontSize = 16;

        boxStyle = new GUIStyle(GUI.skin.box);
        stylesInit = true;
    }

    void Update()
    {
        if (!sys || !game) { game = FindFirstObjectByType<CampusGame>(); return; }

        // Panel hotkeys. While open, CampusGame is frozen (SetUiCursor), so nothing else
        // consumes these keys.
        if (sys.UiOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseUI();
            return;
        }

        if (!InCafeteria() || game.paused) return;
        if (game.Interiors.Interior.NearExit(game.player.position)) return;

        // ---- eating: advance progress while seated, freeze movement ----
        if (sys.Seated)
        {
            // Q or Space stands up. Movement is frozen by CampusGame.Seated, so no other
            // world interaction can run here.
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Q))
            {
                StandUp();
                return;
            }

            if (sys.State == CafeteriaOrderState.Eating)
            {
                sys.AddEatProgress(Time.deltaTime / Mathf.Max(1f, sys.eatSeconds));
                if (sys.EatProgress >= 1f && !mealDoneAnnounced)
                {
                    mealDoneAnnounced = true;
                    RefreshTray();

                    // Water bar / fruit stall close on the spot; the restaurant owes a tray.
                    if (sys.AwaitingTrayReturn)
                        sys.Notify("用餐结束，请起身前往餐盘回收处。");
                    else
                        sys.Notify("已享用完毕，本次点单完成。");
                }
            }
            return;   // seated: no world interaction
        }

        // E interacts with whichever anchor is in range.
        if (Input.GetKeyDown(KeyCode.E)) InteractOnce();
    }

    // One E press == one action. Public so automated runs exercise the real branches
    // instead of calling the underlying methods directly.
    // Set by InteractOnce so callers (and automated runs) can see why nothing happened.
    public string LastInteractBlock;

    public bool InteractOnce()
    {
        LastInteractBlock = null;
        if (!sys || !game) { LastInteractBlock = "no refs"; return false; }
        if (!InCafeteria()) { LastInteractBlock = "not in cafeteria"; return false; }
        if (game.paused) { LastInteractBlock = "game paused"; return false; }
        if (sys.UiOpen) { LastInteractBlock = "ordering panel open"; return false; }

        // Tray return only applies to restaurant orders that finished eating.
        if (sys.NearReturnSpot && sys.AwaitingTrayReturn)
        {
            if (sys.TryReturnTray())
            {
                SetTrayVisible(false);
                mealDoneAnnounced = false;
                sys.Notify("餐盘已归还，本次用餐完成");
            }
            return true;
        }

        if (sys.NearCollectSpot && sys.CanCollect)
        {
            var before = sys.CollectableOrderHere();
            if (!sys.TryCollect())
            {
                // Hands already full, or something else intervened: say so instead of
                // pretending the pickup happened.
                sys.Notify("无法取餐：请先完成手上的订单。");
                return false;
            }
            bool needsTray = before != null && before.NeedsTrayReturn;
            SetTrayVisible(needsTray);
            sys.Notify(needsTray ? "已取餐，请找座位用餐" : "已取餐，可直接享用");
            return true;
        }

        // Standing at a pickup point with an order that is not ready yet.
        if (sys.NearCollectSpot && !sys.CanCollect && sys.FirstPreparing() != null)
        {
            sys.Notify("餐品还在准备中，请稍候。");
            LastInteractBlock = "collect: order still preparing";
            return false;
        }

        // A drink or fruit box is consumed on the spot; a meal needs a chair.
        if (sys.State == CafeteriaOrderState.Carrying && !sys.CarriedNeedsSeat)
        {
            ConsumeDrinkInPlace();
            return true;
        }

        if (sys.NearSeat && sys.State == CafeteriaOrderState.Carrying)
        {
            StartEating();
            return true;
        }

        // A counter can be used again once its own order is finished, even while other
        // counters still have orders running.
        if (sys.NearOrderSpot)
        {
            var shop = sys.NearestOrderShop();
            if (sys.CanOrderAt(shop)) { OpenUI(); return true; }
            sys.Notify("该窗口还有未完成的订单：" + sys.StatusText);
            return true;
        }

        LastInteractBlock = "no anchor in range";
        return false;
    }

    // Water bar / fruit stall: drink or eat standing up, then the order closes itself.
    void ConsumeDrinkInPlace()
    {
        if (sys.State != CafeteriaOrderState.Carrying) return;
        sys.SetEating(true);
        sys.AddEatProgress(1f);          // drinks are finished at once
        sys.SetEating(false);
        mealDoneAnnounced = false;
        SetTrayVisible(false);
        sys.Notify("已享用完毕，本次点单完成。");
    }

    // Public wrapper used by automated verification to stand up (normally Q / Space).
    public void StandUpNow() { StandUp(); }

    bool InCafeteria()
    {
        return game && game.Interiors && game.Interiors.Inside
            && game.Interiors.Interior && game.Interiors.Interior.buildingId == "cafeteria";
    }

    // ---------------------------------------------------------------- eating
    // Hip height above the player root when standing (rig Hips localY), minus the seat
    // height the hips should rest at.
    const float HipDrop = 0.38f;

    void StartEating()
    {
        if (!sys.CanStartEating) return;

        // Must find a real chair on this floor; never sit in place.
        var seat = sys.NearestSeatAnchor();
        if (!seat)
        {
            sys.Notify("附近没有可用座位，请走到餐桌旁的椅子前再按 E。");
            return;
        }

        if (!game.player) return;

        // Freeze first, then move: the CharacterController must not fight the placement.
        var cc = game.player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        game.Seated = true;

        // Hips land on the seat surface and the body faces the table (seat rotation is
        // built from the chair-to-table direction).
        Vector3 sitPos = new Vector3(seat.position.x, seat.position.y - HipDrop, seat.position.z);
        sys.BeginSeat(seat, sitPos, seat.rotation);
        sys.SetEating(true);
        mealDoneAnnounced = false;

        var pose = game.player.GetComponent<CafeteriaSitPose>();
        if (!pose) pose = game.player.gameObject.AddComponent<CafeteriaSitPose>();
        pose.active = true;

        // Frame the table the way the seated body is facing.
        game.AlignViewToSeat(seat.eulerAngles.y);

        Physics.SyncTransforms();
        RefreshTray();
    }

    void StandUp()
    {
        if (!sys.Seated) return;

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

        // Keeps TrayToReturn when the meal finished, returns to Carrying otherwise.
        sys.SetEating(false);

        Physics.SyncTransforms();
        RefreshTray();

        if (finished) sys.Notify("用餐结束，请起身前往餐盘回收处。");
        else sys.Notify("已起身，进食进度已保留，可再次落座继续。");
    }

    // Rebuilds the carried tray so the food disappears once the meal is finished.
    void RefreshTray()
    {
        if (!sys.HasTray) { SetTrayVisible(false); return; }
        SetTrayVisible(true);
    }

    GameObject trayObj;

    void SetTrayVisible(bool on)
    {
        if (trayObj) { UnityEngine.Object.Destroy(trayObj); trayObj = null; }
        if (!on || !game || !game.player) return;

        trayObj = new GameObject("Player_Tray");
        trayObj.transform.SetParent(game.player, false);

        // While seated the hips are 0.38 lower, so the tray is lifted back onto the table
        // and pushed forward over it; standing, it is carried in front of the chest.
        if (sys.Seated)
        {
            trayObj.transform.localPosition = new Vector3(0f, 1.16f, 0.44f);
            trayObj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            trayObj.transform.localPosition = new Vector3(0.36f, 1.05f, 0.18f);
            trayObj.transform.localRotation = Quaternion.identity;
        }
        BuildTrayMesh(trayObj.transform, sys.EatProgress >= 1f);
    }

    bool mealDoneAnnounced;

    void BuildTrayMesh(Transform parent, bool empty)
    {
        var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tray.name = "TrayPlate";
        UnityEngine.Object.DestroyImmediate(tray.GetComponent<Collider>());
        tray.transform.SetParent(parent, false);
        tray.transform.localPosition = new Vector3(0, 0, 0);
        tray.transform.localScale = new Vector3(0.34f, 0.015f, 0.24f);

        var mr = tray.GetComponent<MeshRenderer>();
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.72f, 0.73f, 0.74f) };
        mr.sharedMaterial = m;

        if (empty) return;

        string[] names = { "Dish_A", "Dish_B", "Dish_C", "Drink" };
        Vector3[] pos = {
            new Vector3(-0.10f, 0.03f, -0.05f), new Vector3(0.09f, 0.03f, -0.05f),
            new Vector3(-0.10f, 0.03f, 0.06f), new Vector3(0.10f, 0.05f, 0.07f)
        };
        Vector3[] size = {
            new Vector3(0.13f, 0.035f, 0.085f), new Vector3(0.13f, 0.035f, 0.085f),
            new Vector3(0.10f, 0.03f, 0.08f), new Vector3(0.06f, 0.09f, 0.06f)
        };
        Color[] col = {
            new Color(0.75f, 0.45f, 0.28f), new Color(0.42f, 0.52f, 0.38f),
            new Color(0.68f, 0.62f, 0.42f), new Color(0.85f, 0.90f, 0.95f)
        };

        for (int i = 0; i < names.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = names[i];
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos[i];
            go.transform.localScale = size[i];
            var r = go.GetComponent<MeshRenderer>();
            var mm = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = col[i] };
            r.sharedMaterial = mm;
        }
    }

    // ---------------------------------------------------------------- GUI
    void OnGUI()
    {
        if (!sys || !game) return;
        InitStyles();

        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;

        DrawBalance();

        if (InCafeteria() && !game.paused)
        {
            DrawStatusHud();
            DrawPrompt();
        }

        if (sys.UiOpen) DrawOrderPanel();

        GUI.matrix = matrix;
    }

    void DrawBalance()
    {
        if (!InCafeteria()) return;
        var r = new Rect(Screen.width - 260f, 18f, 240f, 34f);
        GUI.Box(r, "");
        var style = new GUIStyle(rowStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(0.98f, 0.92f, 0.65f);
        GUI.Label(r, sys.BalanceText, style);
    }

    // Right-hand stack of every unfinished order: shop, contents, step and, while
    // preparing, the live countdown. Multiple counters can be running at once.
    void DrawStatusHud()
    {
        int n = sys.OrderCount;
        if (n == 0) return;

        const float w = 380f, rowH = 70f, gap = 8f;
        float top = 62f;
        var panel = new Rect(Screen.width - w - 18f, top, w, n * (rowH + gap) + 10f);

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = prev;

        float y = top + 8f;
        for (int i = 0; i < n; i++)
        {
            var o = sys.OrderAt(i);
            if (o == null) continue;

            var row = new Rect(panel.x + 8f, y, w - 16f, rowH);

            // Shop tag + total on the first line.
            var tag = new GUIStyle(smallStyle) { fontSize = 16, fontStyle = FontStyle.Bold };
            tag.normal.textColor = o.shop == (int)CafeteriaShop.Restaurant
                ? new Color(0.98f, 0.92f, 0.65f)
                : o.shop == (int)CafeteriaShop.WaterBar
                    ? new Color(0.72f, 0.88f, 0.98f)
                    : new Color(0.80f, 0.95f, 0.72f);

            GUI.Label(new Rect(row.x, row.y, 120f, 20f), "【" + o.ShopLabel + "】", tag);
            GUI.Label(new Rect(row.x + 110f, row.y, row.width - 110f, 20f),
                "¥" + (o.totalFen / 100.0).ToString("F2"), smallStyle);

            // What was ordered, trimmed so it cannot spill over the next card.
            string summary = o.ItemsSummary;
            if (summary.Length > 26) summary = summary.Substring(0, 25) + "…";
            GUI.Label(new Rect(row.x, row.y + 20f, row.width, 20f), summary, smallStyle);

            // Current step, with a countdown while preparing.
            string step = sys.LabelOf(o);
            GUIStyle stepStyle = rowStyle;
            if (o.state == (int)CafeteriaOrderState.ReadyToCollect)
            {
                stepStyle = new GUIStyle(rowStyle);
                stepStyle.normal.textColor = new Color(0.55f, 0.95f, 0.55f);
            }
            GUI.Label(new Rect(row.x, row.y + 39f, row.width, 20f), step, stepStyle);

            // Eating progress bar for the order in hand.
            if (o.state == (int)CafeteriaOrderState.Eating)
            {
                var barBg = new Rect(row.x, row.y + rowH - 6f, row.width, 5f);
                GUI.DrawTexture(barBg, Texture2D.whiteTexture);
                GUI.color = new Color(0.45f, 0.85f, 0.45f);
                GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * o.eatProgress, barBg.height),
                    Texture2D.whiteTexture);
                GUI.color = prev;
            }

            y += rowH + gap;
        }
    }

    void DrawPrompt()
    {
        string prompt = "";

        // While seated the only meaningful action is standing up, so show that alone.
        if (sys.Seated)
        {
            prompt = sys.EatProgress >= 1f ? "[ 空格 / Q ] 起身去归还餐盘" : "[ 空格 / Q ] 起身（进度已保留）";
            DrawPromptBox(prompt);
            return;
        }

        if (sys.NearReturnSpot && sys.HasTray && sys.EatProgress >= 1f)
            prompt = "[ E ] 归还餐盘";
        else if (sys.NearCollectSpot && sys.CanCollect)
            prompt = "[ E ] 取餐";
        else if (sys.State == CafeteriaOrderState.Carrying && !sys.CarriedNeedsSeat)
            prompt = "[ E ] 享用饮品 / 水果";
        else if (sys.NearSeat && sys.State == CafeteriaOrderState.Carrying)
            prompt = "[ E ] 落座用餐";
        else if (sys.NearOrderSpot && sys.CanOrder)
            prompt = "[ E ] 打开点餐菜单";
        else if (sys.NearOrderSpot && sys.State != CafeteriaOrderState.None)
            prompt = "当前订单：" + sys.StatusText;

        DrawPromptBox(prompt);
    }

    void DrawPromptBox(string prompt)
    {
        if (string.IsNullOrEmpty(prompt)) return;

        // The Notify message bar occupies h-130 .. h-58; sit above it so the two never
        // overlap.
        float w = 420, h = 42;
        var rect = new Rect((Screen.width - w) * 0.5f, Screen.height - 206, w, h);
        GUI.Box(rect, "");

        var style = new GUIStyle(rowStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(1.0f, 0.95f, 0.70f);
        GUI.Label(rect, prompt, style);
    }

    void OpenUI()
    {
        scroll = Vector2.zero;
        // Open the menu that belongs to the counter in front of the player.
        sys.SetActiveShop(sys.NearestOrderShop());
        sys.SetUi(true);
    }

    void CloseUI() => sys.SetUi(false);

    void DrawOrderPanel()
    {
        float w = 560, h = 620;
        var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        // Solid backing: the default GUI.Box is translucent and let world-space signs
        // and the scene bleed through the dish list.
        var prevColor = GUI.color;
        GUI.color = new Color(0.075f, 0.085f, 0.10f, 0.985f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = prevColor;
        GUI.Box(box, "", boxStyle);

        GUI.Label(new Rect(box.x + 20f, box.y + 14f, 320f, 30f), sys.ShopTitle, titleStyle);
        GUI.Label(new Rect(box.x + w - 200f, box.y + 18f, 180f, 24f), sys.BalanceText, smallStyle);

        var list = new Rect(box.x + 16f, box.y + 52f, w - 32f, 300f);

        bool restaurant = sys.ActiveShop == CafeteriaShop.Restaurant;
        int rows = restaurant ? sys.DishCount + sys.DrinkCount : sys.ShopItemCount;
        scroll = GUI.BeginScrollView(list, scroll, new Rect(0, 0, w - 52f, rows * 30f + 90f));

        float y = 0f;

        void ItemRow(string name, int fen)
        {
            GUI.Label(new Rect(4, y, 220f, 26f), name, rowStyle);
            GUI.Label(new Rect(224f, y, 90f, 26f), sys.Format(fen), rowStyle);
            GUI.Label(new Rect(314f, y, 60f, 26f), "×" + CartQtyOf(name), smallStyle);
            if (GUI.Button(new Rect(370f, y + 2f, 46f, 24f), "加入")) sys.CartAdd(name, fen);
            y += 30f;
        }

        if (restaurant)
        {
            GUI.Label(new Rect(4, y, 200f, 24f), "— 菜品 —", titleStyle); y += 30f;
            for (int i = 0; i < sys.DishCount; i++) ItemRow(sys.DishName(i), sys.DishFen(i));

            y += 8f;
            GUI.Label(new Rect(4, y, 200f, 24f), "— 饮品 —", titleStyle); y += 30f;
            for (int i = 0; i < sys.DrinkCount; i++) ItemRow(sys.DrinkName(i), sys.DrinkFen(i));
        }
        else
        {
            // Water bar and fruit stall each have a single list, capped at ¥20 per item.
            string section = sys.ActiveShop == CafeteriaShop.WaterBar ? "— 现调饮品 —" : "— 鲜果 · 果切 —";
            GUI.Label(new Rect(4, y, 260f, 24f), section, titleStyle); y += 30f;
            if (sys.ShopItemCount == 0)
            {
                GUI.Label(new Rect(4, y, 400f, 24f), "今日暂无供应。", smallStyle);
            }
            for (int i = 0; i < sys.ShopItemCount; i++) ItemRow(sys.ShopItemName(i), sys.ShopItemFen(i));
        }

        GUI.EndScrollView();

        // ---- current order ----
        var cartY = box.y + 372f;
        GUI.Label(new Rect(box.x + 20f, cartY, 200f, 24f), "当前订单", titleStyle);

        var cartRect = new Rect(box.x + 16f, cartY + 28f, w - 32f, 130f);
        GUI.Box(cartRect, "");

        float cy = cartRect.y + 6f;
        if (sys.CartCount == 0)
        {
            GUI.Label(new Rect(cartRect.x + 12f, cy, cartRect.width - 24f, 22f),
                "尚未选择菜品，空订单不能付款。", smallStyle);
        }
        else
        {
            for (int i = 0; i < sys.CartCount; i++)
            {
                string line = sys.CartDishName(i) + "  ×" + sys.CartQty(i) + "  = " + sys.Format(sys.CartUnit(i) * sys.CartQty(i));
                GUI.Label(new Rect(cartRect.x + 12f, cy, cartRect.width - 130f, 22f), line, rowStyle);

                if (GUI.Button(new Rect(cartRect.x + cartRect.width - 116f, cy - 1f, 24f, 20f), "−"))
                    sys.CartChange(i, -1);
                if (GUI.Button(new Rect(cartRect.x + cartRect.width - 88f, cy - 1f, 24f, 20f), "+"))
                    sys.CartChange(i, 1);
                if (GUI.Button(new Rect(cartRect.x + cartRect.width - 60f, cy - 1f, 48f, 20f), "移除"))
                    sys.CartRemove(i);
                cy += 24f;
            }
        }

        // ---- totals + confirm ----
        var footY = cartRect.y + cartRect.height + 12f;
        GUI.Label(new Rect(box.x + 20f, footY, 200f, 26f), sys.OrderTotalText, titleStyle);
        GUI.Label(new Rect(box.x + 220f, footY + 4f, 200f, 24f), sys.BalanceText, smallStyle);

        // Explain the lock per shop instead of silently disabling the button.
        bool shopBusy = sys.ShopBusy(sys.ActiveShop);
        bool payable = sys.CanPay;
        GUI.enabled = payable;
        if (GUI.Button(new Rect(box.x + w - 190f, footY, 170f, 34f), "确认付款"))
        {
            if (sys.TryPay())
            {
                sys.Notify("付款成功，正在备餐。订单号 #" + sys.OrderId);
                CloseUI();
            }
        }
        GUI.enabled = true;

        if (!payable && sys.CartCount > 0 && sys.CartTotal() > sys.BalanceFen / 100f)
        {
            GUI.Label(new Rect(box.x + 20f, footY + 36f, w - 40f, 24f),
                "余额不足，无法付款。", smallStyle);
        }

        if (GUI.Button(new Rect(box.x + w - 110f, box.y + h - 42f, 90f, 28f), "关闭 (ESC)"))
            CloseUI();
    }

    int CartQtyOf(string name)
    {
        int i = 0;
        // walk the cart for a live quantity readout
        for (int k = 0; k < sys.CartCount; k++)
            if (sys.CartDishName(k) == name) { i = sys.CartQty(k); break; }
        return i;
    }

    long BalanceFenValue() => sys.BalanceFen;
}
