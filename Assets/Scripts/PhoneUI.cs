using System.Collections.Generic;
using UnityEngine;

// In-game phone with a small app launcher. Only the library booking app exists for now,
// but the launcher is a list so more apps can be added without restructuring.
//
// P opens/closes it. While open the mouse is freed and the player is frozen through the
// same UiBlocking lock the other interior UIs use, so no new lock type is introduced.
public class PhoneUI : MonoBehaviour, ICampusOverlay
{
    public static PhoneUI Instance;

    public const string PhonePrefsKey = "PhoneApp";

    enum Page { Home, Booking, FloorSeats, MyBooking }

    bool open;
    Page page = Page.Home;
    int floor = 2;
    string seatArea="A";
    LibrarySeat selected;
    string toast;
    float toastUntil;
    Vector2 scroll;

    CampusGame game;
    GUIStyle title, body, small, big, btnStyle;
    bool styles;

    public bool IsOpen => open;
    public bool OverlayVisible => open;
    public void DrawOverlay() { DrawGUI(); }

    public static PhoneUI Ensure()
    {
        if (Instance) { CampusOverlayHub.Register(Instance); return Instance; }

        // Prefer the copy the scene already carries; a second instance meant the scene
        // one drew nothing while the code talked to an invisible duplicate.
        var sceneOne = UnityEngine.Object.FindFirstObjectByType<PhoneUI>();
        if (sceneOne) { Instance = sceneOne; sceneOne.enabled = true; CampusOverlayHub.Register(sceneOne); return Instance; }

        var go = new GameObject("PhoneUI");
        if (Application.isPlaying) DontDestroyOnLoad(go);
        var p = go.AddComponent<PhoneUI>();
        Instance = p;
        return p;
    }

    void OnEnable() { if (Instance == this) CampusOverlayHub.Register(this); }

    void OnDestroy()
    {
        CampusOverlayHub.Unregister(this);
        if (Instance == this) Instance = null;
    }

    void Awake()
    {
        if (Instance && Instance != this) { enabled = false; Destroy(this); return; }
        Instance = this;
        CampusOverlayHub.Register(this);
        game = FindFirstObjectByType<CampusGame>();
    }

    void Update()
    {
        if (!game) { game = FindFirstObjectByType<CampusGame>(); if (!game) return; }

        // A vending menu or the cafeteria panel owns input while it is open.
        if (!open && VendingMachineUI.AnyOpen) return;
        if (!open && CafePanelOpen()) return;
        if (!open && game.UiBlocking) return;

        if (Input.GetKeyDown(KeyCode.P))
        {
            if (open) Close();
            else if (!game.paused) Open();
            return;
        }

        // Escape walks back one page, and only closes the phone from the home screen, so
        // one press can never also toggle the pause menu.
        if (open && Input.GetKeyDown(KeyCode.Escape))
        {
            CampusOverlayHub.ConsumeInput();
            if (page != Page.Home) { page = Page.Home; selected = null; }
            else Close();
        }
    }

    static bool CafePanelOpen()
    {
        var o = FindFirstObjectByType<CafeteriaOrdering>();
        return o && o.UiOpen;
    }

    public void Open()
    {
        if (VendingMachineUI.AnyOpen) VendingMachineUI.Instance.Close();
        if (!game) game = FindFirstObjectByType<CampusGame>();
        CampusOverlayHub.ConsumeInput();
        open = true;
        page = Page.Home;
        selected = null;
        LibrarySeatRegistry.Ensure();
        LibraryReservationManager.Ensure();
        if (game) game.SetUiCursor(true);
    }

    public void Close()
    {
        CampusOverlayHub.ConsumeInput();
        open = false;
        if (game) game.SetUiCursor(false);
    }

    void OnDisable() { if (open) Close(); CampusOverlayHub.Unregister(this); }

    // ---- hooks used by automated verification -------------------------------
    public void ShowSeatPageForTest(int f) { floor = f; page = Page.FloorSeats; selected = null; }
    public void ShowMyBookingForTest() { page = Page.MyBooking; }
    public void PickForTest(LibrarySeat s) { selected = s; }

    void Toast(string msg) { toast = msg; toastUntil = Time.unscaledTime + 2.5f; }

    // ---------------------------------------------------------------- drawing
    void InitStyles()
    {
        if (styles) return;
        var f = game && game.interfaceFont ? game.interfaceFont : Resources.Load<Font>("CampusFont");

        title = new GUIStyle { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        body = new GUIStyle { fontSize = 16, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        small = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleLeft };
        big = new GUIStyle { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        if (f) { title.font = f; body.font = f; small.font = f; big.font = f; btnStyle.font = f; }

        title.normal.textColor = new Color(0.95f, 0.95f, 0.98f);
        body.normal.textColor = new Color(0.86f, 0.88f, 0.92f);
        small.normal.textColor = new Color(0.66f, 0.70f, 0.76f);
        big.normal.textColor = Color.white;
        styles = true;
    }

    // Cached 1x1 opaque fill so the panel never lets the 3D scene bleed through.
    static Texture2D solid;
    public static Texture2D SolidFill { get { return Solid; } }

    static Texture2D Solid
    {
        get
        {
            if (solid) return solid;
            solid = new Texture2D(1, 1);
            solid.SetPixel(0, 0, new Color(0.055f, 0.065f, 0.085f, 1f));
            solid.Apply();
            solid.hideFlags = HideFlags.HideAndDontSave;
            return solid;
        }
    }

    public static int DrawCount;
    public static string LastRect = "";

    // Dispatched by the single overlay painter for every IMGUI event.
    public void DrawGUI()
    {
        if (!open) return;
        if (Event.current.type == EventType.Repaint) DrawCount++;

        // Force a known-good GUI state: the host scene touches skin, colour and matrix,
        // and a stale transparent colour made the panel invisible before.
        GUI.color = Color.white;
        GUI.matrix = Matrix4x4.identity;
        GUI.enabled = true;
        InitStyles();

        // Phone body: fixed aspect, clamped to the window so it fits small screens.
        float h = Mathf.Min(Screen.height * 0.82f, 760f);
        float w = Mathf.Min(h * 0.56f, Screen.width * 0.40f);
        h = w / 0.56f;
        var p = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        LastRect = $"screen={Screen.width}x{Screen.height} phone={w:F0}x{h:F0} at=({p.x:F0},{p.y:F0})";

        var prev = GUI.color;
        GUI.DrawTexture(p, Solid, ScaleMode.StretchToFill);
        GUI.color = prev;

        // notch / status bar
        var bar = new Rect(p.x, p.y, p.width, 34f);
        GUI.color = new Color(0.09f, 0.10f, 0.13f, 1f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = prev;

        var clock = GameClock.Ensure();
        GUI.Label(new Rect(p.x + 12f, p.y, 160f, 34f), clock.TimeText, small);
        GUI.Label(new Rect(p.x + p.width - 120f, p.y, 108f, 34f), "P 关闭", small);

        var content = new Rect(p.x + 14f, p.y + 42f, p.width - 28f, p.height - 56f);

        switch (page)
        {
            case Page.Home: DrawHome(content); break;
            case Page.Booking: DrawBooking(content); break;
            case Page.FloorSeats: DrawFloorSeats(content); break;
            case Page.MyBooking: DrawMyBooking(content); break;
        }

        if (!string.IsNullOrEmpty(toast) && Time.unscaledTime < toastUntil)
            GUI.Label(new Rect(p.x + 20f, p.y + p.height - 44f, p.width - 40f, 24f), toast, small);
    }

    void DrawHome(Rect c)
    {
        var clock = GameClock.Ensure();
        GUI.Label(new Rect(c.x, c.y, c.width, 30f), clock.DateText, big);
        GUI.Label(new Rect(c.x, c.y + 34f, c.width, 24f), clock.TimeText + " · 校园生活", small);

        // App grid (single column for legibility, but structured as a list of apps).
        float y = c.y + 74f;
        if (AppButton(c, ref y, "图书馆预约", LiblaryBookingSummary())) return;

        if (GUI.Button(new Rect(c.x,y+8,c.width,58),"课表与学期进度",btnStyle)) CampusSemester.Ensure().UI.Open("today");
        if (GUI.Button(new Rect(c.x,y+74,c.width,58),"学习任务与作业",btnStyle)) CampusSemester.Ensure().UI.Open("study");
        if (GUI.Button(new Rect(c.x,y+140,c.width,58),"校园故事与活动",btnStyle)) CampusSemester.Ensure().UI.Open("story");
    }

    string LiblaryBookingSummary()
    {
        var rm = LibraryReservationManager.Ensure();
        return rm.HasReservation ? "已预约 " + rm.ReservedSeatId : "预约自习座位";
    }

    bool AppButton(Rect c, ref float y, string name, string subtitle)
    {
        var r = new Rect(c.x, y, c.width, 64f);
        GUI.color = new Color(0.14f, 0.16f, 0.20f, 1f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, 24f), name, title);
        GUI.Label(new Rect(r.x + 14f, r.y + 32f, r.width - 28f, 22f), subtitle, small);

        bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);
        y += 72f;
        if (hit) { page = Page.Booking; selected = null; }
        return hit;
    }

    void Label(Rect plan, float u, float v, string text)
    {
        var st = new GUIStyle(small) { fontSize = 12 };
        st.normal.textColor = new Color(0.55f, 0.62f, 0.72f);
        GUI.Label(new Rect(plan.x + 8f + u * (plan.width - 60f),
                           plan.y + 8f + v * (plan.height - 30f), 56f, 18f), text, st);
    }

    void DrawBooking(Rect c)
    {
        GUI.Label(new Rect(c.x, c.y, c.width, 30f), "图书馆预约", title);
        GUI.Label(new Rect(c.x, c.y + 32f, c.width, 22f), "选择楼层查看可预约座位", small);

        float y = c.y + 66f;
        var reg = LibrarySeatRegistry.Ensure();
        for (int f = 1; f <= 4; f++)
        {
            int n = reg.CountOnFloor(f);
            string label = f + "F   " + (n > 0 ? n + " 个可预约座位" : "暂无可预约座位");

            var r = new Rect(c.x, y, c.width, 52f);
            GUI.color = new Color(0.14f, 0.16f, 0.20f, 1f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 14f, r.y + 14f, r.width - 28f, 24f), label, body);

            if (n > 0 && GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                floor = f; page = Page.FloorSeats; selected = null;
            }
            y += 60f;
        }

        var rm = LibraryReservationManager.Ensure();
        if (rm.HasReservation)
        {
            if (GUI.Button(new Rect(c.x, c.yMax - 40f, c.width, 34f), "查看我的预约 (" + rm.ReservedSeatId + ")", btnStyle))
            { page = Page.MyBooking; }
        }

        if (GUI.Button(new Rect(c.x, c.yMax - 82f, 90f, 30f), "‹ 返回", btnStyle))
            page = Page.Home;
    }

    void DrawFloorSeats(Rect c)
    {
        var reg = LibrarySeatRegistry.Ensure();
        var rm = LibraryReservationManager.Ensure();
        var seats = reg.OnFloor(floor).FindAll(s=>s.area==seatArea);

        GUI.Label(new Rect(c.x, c.y, c.width, 30f), floor + "F 座位平面图", title);

        // Plan view area, kept square so the layout is not distorted.
        float column=(c.width-6)/2;
        if(LibraryUiProbe.Button("phone:area:A",new Rect(c.x,c.y+36,column,28),seatArea=="A"?"圆厅 A · 当前":"圆厅 A",btnStyle)){seatArea="A";selected=null;}
        if(LibraryUiProbe.Button("phone:area:B",new Rect(c.x+column+6,c.y+36,column,28),seatArea=="B"?"阅览 B · 当前":"阅览 B",btnStyle)){seatArea="B";selected=null;}
        float size = Mathf.Min(c.width - 10f, c.height - 224f);
        var plan = new Rect(c.x + (c.width - size) * 0.5f, c.y + 70f, size, size);

        GUI.color = new Color(0.10f, 0.12f, 0.15f, 1f);
        GUI.DrawTexture(plan, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Orientation marker: north is up.
        GUI.Label(new Rect(plan.x + size - 58f, plan.y + 4f, 54f, 20f), "北 ▲", small);

        // Landmarks, so the plan is usable for finding the seat in the real hall.
        Label(plan, 0.06f, 0.92f, "楼梯");
        Label(plan, 0.06f, 0.78f, "电梯");
        Label(plan, 0.80f, 0.92f, "出入口");
        Label(plan, 0.44f, 0.06f, "圆厅");

        // Select by nearest centre once; neighbouring hit boxes must not steal clicks.
        float minX=1,maxX=0,minY=1,maxY=0;
        foreach(var seat in seats){minX=Mathf.Min(minX,seat.mapPos.x);maxX=Mathf.Max(maxX,seat.mapPos.x);minY=Mathf.Min(minY,seat.mapPos.y);maxY=Mathf.Max(maxY,seat.mapPos.y);}
        float span=Mathf.Max(.001f,maxX-minX,maxY-minY);
        System.Func<LibrarySeat,Vector2> centre = s => new Vector2(plan.x+16+(s.mapPos.x-minX)/span*(size-32),plan.y+16+(maxY-s.mapPos.y)/span*(size-32));
        foreach(var seat in seats)LibraryUiProbe.Record("phone:seat:"+seat.id,new Rect(centre(seat)-Vector2.one*8,Vector2.one*16));
        if(LibraryUiProbe.Button("phone:plan",plan,"",GUIStyle.none)){
            var pointer=Event.current.mousePosition;LibrarySeat closest=null;float best=18*18;
            foreach(var seat in seats){float sq=(centre(seat)-pointer).sqrMagnitude;if(sq<best){best=sq;closest=seat;}}
            if(closest!=null)selected=closest;
        }
        foreach(var seat in seats)if(selected==null||seat.id!=selected.id)PaintSeat(seat,centre(seat),rm,false);
        if(selected!=null)PaintSeat(selected,centre(selected),rm,true);
        float ly=plan.yMax+8;
        float legend=c.width/3;
        SeatLegend(new Rect(c.x,ly,legend,20),new Color(.62f,.68f,.78f),"可预约");
        SeatLegend(new Rect(c.x+legend,ly,legend,20),new Color(.45f,.95f,.55f),"我的预约");
        SeatLegend(new Rect(c.x+legend*2,ly,legend,20),new Color(1f,.85f,.35f),"已选中");
        if(selected!=null){
            var previous=title.normal.textColor;title.normal.textColor=new Color(1f,.85f,.35f);
            GUI.Label(new Rect(c.x,ly+25,c.width,26),"已选中 "+selected.id,title);title.normal.textColor=previous;
            GUI.Label(new Rect(c.x,ly+52,c.width,22),selected.areaName+" · "+(rm.IsReservedSeat(selected.id)?"这就是我的预约":"确认后预约此座位"),small);
        }else GUI.Label(new Rect(c.x,ly+28,c.width,44),"点击座位选中，再确认预约。",body);
        bool occupied=rm.HasReservation&&rm.State.inUse;
        bool canBook=selected!=null&&!occupied&&!rm.IsBlocked(selected.id,out _);
        bool oldEnabled=GUI.enabled;GUI.enabled=canBook;
        string bookingLabel=occupied?"正在使用座位，请先起身":selected!=null&&rm.IsReservedSeat(selected.id)?"已预约此座位":rm.HasReservation?"确认更换座位":"确认预约所选座位";
        if(LibraryUiProbe.Button("phone:reserve",new Rect(c.x,c.yMax-40,c.width,36),bookingLabel,btnStyle)){
            if(rm.TryReplace(selected.id,selected.floor,selected.areaName,true)){Toast("预约成功："+selected.id);page=Page.MyBooking;}
            else Toast("预约失败；使用中请先起身再更换座位。");
        }
        GUI.enabled=oldEnabled;
        if(LibraryUiProbe.Button("phone:back",new Rect(c.xMax-76,c.y,76,28),"‹ 楼层",btnStyle))page=Page.Booking;
    }
    void PaintSeat(LibrarySeat seat,Vector2 centre,LibraryReservationManager rm,bool selectedSeat){
        var previous=GUI.color;
        float extent=selectedSeat?9:2.5f;
        if(selectedSeat){GUI.color=Color.white;GUI.DrawTexture(new Rect(centre-Vector2.one*11,Vector2.one*22),Texture2D.whiteTexture);}
        GUI.color=selectedSeat?new Color(1f,.85f,.35f):rm.IsReservedSeat(seat.id)?new Color(.45f,.95f,.55f):new Color(.62f,.68f,.78f);
        GUI.DrawTexture(new Rect(centre-Vector2.one*extent,Vector2.one*(extent*2)),Texture2D.whiteTexture);GUI.color=previous;
        if(selectedSeat){var style=new GUIStyle(small){fontSize=12,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.06f,.07f,.09f)}};GUI.Label(new Rect(centre-Vector2.one*12,Vector2.one*24),seat.id.Substring(seat.id.Length-2),style);}
    }
    void SeatLegend(Rect rect,Color color,string text){var previous=GUI.color;GUI.color=color;GUI.DrawTexture(new Rect(rect.x,rect.y+6,8,8),Texture2D.whiteTexture);GUI.color=previous;GUI.Label(new Rect(rect.x+14,rect.y,rect.width-14,rect.height),text,small);}

    void DrawMyBooking(Rect c)
    {
        var rm = LibraryReservationManager.Ensure();
        GUI.Label(new Rect(c.x, c.y, c.width, 30f), "我的预约", title);

        if (!rm.HasReservation)
        {
            GUI.Label(new Rect(c.x, c.y + 40f, c.width, 24f), "当前没有预约。", body);
        }
        else
        {
            var seat = LibrarySeatRegistry.Ensure().ById(rm.ReservedSeatId);
            string area = seat != null ? seat.areaName : rm.State.seatArea;

            GUI.Label(new Rect(c.x, c.y + 40f, c.width, 26f), "座位 " + rm.ReservedSeatId, title);
            GUI.Label(new Rect(c.x, c.y + 70f, c.width, 22f), $"{rm.ReservedFloor}F · {area}", body);
            GUI.Label(new Rect(c.x, c.y + 96f, c.width, 22f),
                rm.State.inUse ? "状态：使用中" : "状态：已预约，请前往座位", small);

            if (GUI.Button(new Rect(c.x, c.yMax - 80f, c.width, 32f), "取消预约", btnStyle))
            {
                var study = LibraryStudy.Instance;
                if (study && study.Seated) study.TryStandUp();
                rm.Cancel();
                Toast("已取消预约");
            }
        }

        if (GUI.Button(new Rect(c.x, c.yMax - 38f, 90f, 32f), "‹ 返回", btnStyle))
            page = Page.Booking;
    }
}
