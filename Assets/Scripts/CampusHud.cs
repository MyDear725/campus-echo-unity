using UnityEngine;

// The single persistent HUD: living allowance (生活费) in the top-right corner, the
// cafeteria order list directly beneath it, and the one contextual interaction prompt at
// the bottom of the screen.
//
// It is drawn by CampusOverlayPainter (the only IMGUI painter), lives on a
// DontDestroyOnLoad object, and never touches the scene, so it keeps drawing across the
// campus, every floor of every building, and while any panel is open. Because it is the
// only place the balance is shown, there is exactly one allowance readout on screen.
public class CampusHud : MonoBehaviour, ICampusOverlay
{
    public static CampusHud Instance;

    public bool OverlayVisible => true;

    // ---------------------------------------------------------------- style
    GUIStyle balanceStyle, smallStyle, bigStyle, bodyStyle, promptStyle, promptBig;
    Font cachedFont;
    bool styles;

    CampusGame game;

    public static CampusHud Ensure()
    {
        if (Instance) { CampusOverlayHub.Register(Instance); return Instance; }
        var go = new GameObject("CampusHud");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<CampusHud>();
        return Instance;
    }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CampusOverlayHub.Register(this);
    }

    void OnEnable() { if (Instance == this) CampusOverlayHub.Register(this); }
    void OnDestroy() { CampusOverlayHub.Unregister(this); if (Instance == this) Instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForNewSession() { Instance = null; }

    void Update()
    {
        if (!game) game = FindFirstObjectByType<CampusGame>();
    }

    // ---------------------------------------------------------------- fonts
    Font UiFont()
    {
        if (cachedFont) return cachedFont;
        if (game && game.interfaceFont) cachedFont = game.interfaceFont;
        if (!cachedFont) cachedFont = Resources.Load<Font>("CampusFont");
        return cachedFont;
    }

    void InitStyles()
    {
        var f = UiFont();
        if (!styles || (smallStyle != null && smallStyle.font != f))
        {
            balanceStyle = new GUIStyle { alignment = TextAnchor.MiddleRight, fontSize = 18, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, fontSize = 13 };
            bigStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, fontSize = 15, fontStyle = FontStyle.Bold };
            bodyStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, fontSize = 14, wordWrap = false };
            promptStyle = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
            promptBig = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 18, fontStyle = FontStyle.Bold };
            if (f) { balanceStyle.font = f; smallStyle.font = f; bigStyle.font = f; bodyStyle.font = f; promptStyle.font = f; promptBig.font = f; }
            styles = true;
        }
    }

    static Texture2D Fill => Texture2D.whiteTexture;

    static void Panel(Rect r, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Fill);
        GUI.color = prev;
    }

    // Text width in the current resolution for laying out without overlap.
    float Measure(string text, GUIStyle style) { return style != null ? style.CalcSize(new GUIContent(text)).x : text.Length * 10f; }

    // ---------------------------------------------------------------- drawing
    public void DrawOverlay()
    {
        GUI.matrix = Matrix4x4.identity;
        GUI.color = Color.white;
        GUI.contentColor = Color.white;
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        InitStyles();

        bool paused = game && game.paused;
        var ordering = CampusSystemsRegistry.FindOrdering();

        // Interface scale: larger windows get larger text, small windows shrink a little.
        float scale = Mathf.Clamp(Screen.height / 900f, 0.82f, 1.35f);
        float right = 16f * scale;
        float top = 14f * scale;

        DrawBalance(ref top, right, scale);
        if(LibrarySeatInteraction.Instance&&LibrarySeatInteraction.Instance.SubjectsOpen)return;
        var semester=CampusSemester.Instance;
        if(semester){
            var clock=semester.Clock;
            float sw=Mathf.Min(450*scale,Screen.width-32);
            var rect=new Rect(Screen.width-right-sw,top,sw,88*scale);
            Panel(rect,new Color(.04f,.08f,.06f,.94f));
            var timeStyle=new GUIStyle(balanceStyle){fontSize=Mathf.RoundToInt(16*scale),wordWrap=true};
            timeStyle.normal.textColor=new Color(.96f,.95f,.87f);
            GUI.Label(new Rect(rect.x+12,rect.y+8,rect.width-24,rect.height-12),clock.DateTimeText+"  "+clock.WeekText+"\n"+semester.NextSchedule,timeStyle);
            top+=rect.height+8*scale;
            if(!paused&&!game.UiBlocking){
                string navigation=semester.UI.Navigation();
                if(!string.IsNullOrEmpty(navigation)){Panel(new Rect(16,Screen.height-210,Screen.width-32,36),new Color(.04f,.08f,.06f,.94f));GUI.Label(new Rect(28,Screen.height-205,Screen.width-56,30),navigation,timeStyle);}
                var point=CampusSemesterPoint.Nearby(game);
                if(semester.Ready&&point){Panel(new Rect(Screen.width/2-240,Screen.height-265,480,38),new Color(.04f,.08f,.06f,.94f));GUI.Label(new Rect(Screen.width/2-226,Screen.height-260,452,30),"[ E ] "+point.label,timeStyle);}
            }
        }
        if (!paused) DrawOrders(ref top, ordering, right, scale);

        DrawInteractionPrompt(ordering, scale);
    }

    void DrawBalance(ref float top, float right, float scale)
    {
        string text = CampusWallet.BalanceText;
        float w = Measure(text, balanceStyle) + 34f * scale;
        float h = 38f * scale;
        float x = Screen.width - right - w;
        var r = new Rect(x, top, w, h);

        Panel(r, new Color(0.04f, 0.06f, 0.08f, 0.72f));
        Panel(new Rect(r.x, r.y, 4f * scale, r.height), new Color(0.95f, 0.78f, 0.32f, 0.95f));
        balanceStyle.normal.textColor = new Color(1f, 0.92f, 0.62f);
        balanceStyle.fontSize = Mathf.RoundToInt(18f * scale);
        GUI.Label(new Rect(r.x + 4f, r.y, r.width - 16f * scale, r.height), text, balanceStyle);

        top += h + 8f * scale;
    }

    float OrderListBottom = -1f;

    void DrawOrders(ref float top, CafeteriaOrdering ordering, float right, float scale)
    {
        if (!ordering) { OrderListBottom = -1f; return; }

        var orders = ordering.OrdersForTest();
        if (orders == null) { OrderListBottom = -1f; return; }

        // Only unfinished work is shown: preparing, ready, in hand.
        int visible = 0;
        float maxText = 0f;
        for (int i = 0; i < orders.Count; i++)
        {
            var o = orders[i];
            if (o == null || !IsListed(o)) continue;
            visible++;
            // Line: "1F 2号档口 · 经典小炒：备餐中 00:18"
            float m = Measure(HeaderLine(ordering, o), bigStyle);
            maxText = Mathf.Max(maxText, m);
        }
        if (visible == 0) { OrderListBottom = -1f; return; }

        float lineH = 20f * scale;
        float rowH = lineH * 2f + 10f * scale;
        // Wide enough for the header line ("1F 2号档口 · 经典小炒：备餐中 00:18") so the
        // items line below it never wraps.
        float w = Mathf.Min(Mathf.Max(maxText + 44f * scale, 300f * scale), Screen.width * 0.62f);
        var panel = new Rect(Screen.width - right - w, top, w, visible * rowH + 10f * scale);

        Panel(panel, new Color(0.04f, 0.06f, 0.08f, 0.66f));

        float y = panel.y + 5f * scale;
        for (int i = 0; i < orders.Count; i++)
        {
            var o = orders[i];
            if (o == null || !IsListed(o)) continue;

            var st = (CafeteriaOrderState)o.state;
            Color tag = st == CafeteriaOrderState.ReadyToCollect ? new Color(0.55f, 0.95f, 0.55f)
                      : st == CafeteriaOrderState.Preparing ? new Color(0.98f, 0.92f, 0.65f)
                      : new Color(0.72f, 0.88f, 0.98f);

            bigStyle.fontSize = Mathf.RoundToInt(15f * scale);
            bigStyle.normal.textColor = tag;
            GUI.Label(new Rect(panel.x + 10f * scale, y, w - 20f * scale, lineH), HeaderLine(ordering, o), bigStyle);

            smallStyle.fontSize = Mathf.RoundToInt(13f * scale);
            smallStyle.normal.textColor = new Color(0.78f, 0.82f, 0.88f);
            GUI.Label(new Rect(panel.x + 10f * scale, y + lineH, w - 20f * scale, lineH),
                Fit(LabelOf(ordering, o), smallStyle, w - 22f * scale), smallStyle);

            // A slim progress line under a meal in hand.
            if (st == CafeteriaOrderState.Eating)
            {
                var bg = new Rect(panel.x + 10f * scale, y + rowH - 4f * scale, w - 20f * scale, 3f * scale);
                Panel(bg, new Color(0.2f, 0.24f, 0.28f, 1f));
                Panel(new Rect(bg.x, bg.y, bg.width * Mathf.Clamp01(o.eatProgress), bg.height),
                    new Color(0.45f, 0.85f, 0.45f, 1f));
            }
            y += rowH;
        }

        OrderListBottom = panel.yMax;
        top = panel.yMax + 10f * scale;
    }

    bool IsListed(CafeteriaOrder o)
    {
        var st = (CafeteriaOrderState)o.state;
        return st == CafeteriaOrderState.Preparing || st == CafeteriaOrderState.ReadyToCollect ||
               st == CafeteriaOrderState.Carrying || st == CafeteriaOrderState.Eating ||
               st == CafeteriaOrderState.TrayToReturn;
    }

    string HeaderLine(CafeteriaOrdering ordering, CafeteriaOrder o)
    {
        return o.FloorShopLabel + "：" + ordering.LabelOf(o);
    }

    string LabelOf(CafeteriaOrdering ordering, CafeteriaOrder o)
    {
        return o.ItemsSummary + "  " + CampusWallet.FormatYuan(o.totalFen);
    }

    // Trim text to the available width so a long order never wraps into the next card.
    string Fit(string text, GUIStyle style, float maxWidth)
    {
        if (Measure(text, style) <= maxWidth) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            string cut = text.Substring(0, n) + "…";
            if (Measure(cut, style) <= maxWidth) return cut;
        }
        return "…";
    }

    // ---------------------------------------------------------------- prompt
    string CurrentPrompt(CafeteriaOrdering ordering)
    {
        if (!game || !game.player) return "";
        if (game.paused) return "";

        bool indoors = game.Interiors && game.Interiors.Inside;
        bool inCafeteria = indoors && game.Interiors.Interior && game.Interiors.Interior.buildingId == "cafeteria";

        if (inCafeteria && ordering)
        {
            if (ordering.Seated)
                return ordering.EatProgress >= 1f ? "[ 空格 / Q ] 起身去归还餐盘" : "[ 空格 / Q ] 起身（进度已保留）";

            if (ordering.CanStartEating && ordering.CarriedNeedsSeat && ordering.NearSeat) return "[ E ] 落座用餐";
            if (ordering.NearReturnSpot && ordering.AwaitingTrayReturn) return "[ E ] 归还餐盘";

            // A ready order at this counter takes priority over opening its menu.
            if (ordering.CanCollect)
            {
                var ready = ordering.CollectableOrderHere();
                return ready != null ? "[ E ] 取餐 · " + ready.FloorShopLabel : "[ E ] 取餐";
            }

            var shop = ordering.ShopAtPlayer();
            if (shop.HasValue)
            {
                var def = CafeteriaShops.Get(shop.Value);
                if (def != null)
                {
                    string label = def.business == CafeteriaBusiness.Meal ? "打开点餐菜单" :
                                   def.business == CafeteriaBusiness.Drink ? "打开饮品菜单" : "打开水果摊";
                    return "[ E ] " + def.FloorLabel + " " + def.shortName + " · " + label;
                }
            }
            var preparing = ordering.PreparingOrderHere();
            if (preparing != null) return preparing.FloorShopLabel + " · " + ordering.LabelOf(preparing);
        }

        var dorm = DormitoryCorridor.Current(game);
        if (dorm && !string.IsNullOrEmpty(dorm.Prompt)) return dorm.Prompt;

        // Building doors and exits keep their own prompt from the travel system.
        if (game.Interiors)
        {
            string door = game.Interiors.InteractionPrompt;
            if (!string.IsNullOrEmpty(door)) return door;
        }
        if (game.Story && !string.IsNullOrEmpty(game.Story.Prompt)) return game.Story.Prompt;
        return "";
    }

    void DrawInteractionPrompt(CafeteriaOrdering ordering, float scale)
    {
        string prompt = CurrentPrompt(ordering);
        if (string.IsNullOrEmpty(prompt)) return;

        promptBig.fontSize = Mathf.RoundToInt(17f * scale);
        float w = Mathf.Min(Measure(prompt, promptBig) + 60f * scale, Screen.width * 0.7f);
        float h = 42f * scale;
        float y = Screen.height - 86f * scale;
        var r = new Rect((Screen.width - w) * 0.5f, y, w, h);

        Panel(r, new Color(0.04f, 0.06f, 0.08f, 0.72f));
        promptBig.normal.textColor = new Color(1f, 0.95f, 0.70f);
        GUI.Label(r, prompt, promptBig);
    }
}
