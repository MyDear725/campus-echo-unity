using UnityEngine;

// In-world side of library seats and the two vending machines.
//
// Owns its own E handling and is consulted before the generic library spots, so one
// press only ever does one thing. All state comes from LibrarySeatRegistry and
// LibraryReservationManager, the same objects the phone reads.
public class LibrarySeatInteraction : MonoBehaviour, ICampusOverlay
{
    public static LibrarySeatInteraction Instance;

    public bool OverlayVisible => true;
    public void DrawOverlay() { DrawGUI(); }


    CampusGame game;
    LibraryStudy study;
    VendingMachineUI vending;

    GUIStyle promptStyle, boxStyle, btnStyle;
    bool styles;
    public bool SubjectsOpen { get; private set; }
    string pendingCourse, studyFeedback="";
    int pendingMinutes;

    // Cached so the painted highlight and the prompt agree with the E action.
    LibrarySeat nearSeat;
    string nearMachine;

    public static LibrarySeatInteraction Ensure()
    {
        if (Instance) { CampusOverlayHub.Register(Instance); return Instance; }

        // Prefer the copy the scene already carries; a second instance meant the scene
        // one drew nothing while the code talked to an invisible duplicate.
        var sceneOne = UnityEngine.Object.FindFirstObjectByType<LibrarySeatInteraction>();
        if (sceneOne) { Instance = sceneOne; sceneOne.enabled = true; CampusOverlayHub.Register(sceneOne); return Instance; }

        var go = new GameObject("LibrarySeatInteraction");
        if (Application.isPlaying) DontDestroyOnLoad(go);
        Instance = go.AddComponent<LibrarySeatInteraction>();
        return Instance;
    }

    void OnEnable() { if (Instance == this) CampusOverlayHub.Register(this); }

    void OnDestroy()
    {
        CampusOverlayHub.Unregister(this);
        if (Instance == this) Instance = null;
    }

    void OnDisable() { CampusOverlayHub.Unregister(this); }

    void Awake()
    {
        if (Instance && Instance != this) { enabled = false; Destroy(this); return; }
        Instance = this;
        CampusOverlayHub.Register(this);
    }

    void Update()
    {
        if (!game) game = FindFirstObjectByType<CampusGame>();
        if (!game || !game.player) return;
        if (!study) study = LibraryStudy.Ensure();
        if (!vending) vending = VendingMachineUI.Ensure();

        bool inLibrary = InLibrary();
        if (!inLibrary) { nearSeat = null; nearMachine = null; return; }

        if(SubjectsOpen){
            if(!inLibrary||!study.Seated){CloseSubjects();return;}
            if(Input.GetKeyDown(KeyCode.Escape)){CampusOverlayHub.ConsumeInput();if(pendingCourse!=null)pendingCourse=null;else CloseSubjects();}
            return;
        }
        // Another owned UI already has the cursor.
        if (game.UiBlocking) { nearSeat = null; nearMachine = null; return; }

        // Seated: only the study panel drives keys.
        if (study && study.Seated)
        {
            if (Input.GetKeyDown(KeyCode.Q)) { study.TryStandUp(); }
            return;
        }

        if (game.paused) return;
        if (game.Interiors.Interior.NearExit(game.player.position)) return;

        nearMachine = vending.MachineAtPlayerPublic();
        nearSeat = study.SeatNearPlayer();

    }

    // The main E dispatcher and the prompt use the same seat selection.
    public bool InteractOnce()
    {
        if (!game) game = FindFirstObjectByType<CampusGame>();
        if (!game || !game.player || !InLibrary() || game.paused || game.UiBlocking) return false;
        if (!study) study = LibraryStudy.Ensure();
        if (!vending) vending = VendingMachineUI.Ensure();
        if (study.Seated || game.Interiors.Interior.NearExit(game.player.position)) return false;
        nearMachine = vending.MachineAtPlayerPublic();
        if (nearMachine != null) { vending.OpenAt(nearMachine); return true; }
        nearSeat = study.SeatNearPlayer();
        if (nearSeat == null) return false;
        if (LibraryReservationManager.Ensure().IsReservedSeat(nearSeat.id)) study.TrySit(nearSeat);
        else game.Notify("该座位未预约，请打开手机（P）预约后再使用。");
        return true;
    }

    // Used by LibraryIndoorInteractions so the generic spots yield this frame.
    public bool HandlesCurrentE()
    {
        if (!game || !game.player) return false;
        if (!InLibrary()) return false;
        if (study && study.Seated) return true;
        if (vending && vending.MachineAtPlayerPublic() != null) return true;
        if (study && study.SeatNearPlayer() != null) return true;
        return false;
    }

    bool InLibrary()
    {
        return game && game.Interiors && game.Interiors.Inside
            && game.Interiors.Interior && game.Interiors.Interior.buildingId == "library";
    }

    // ---------------------------------------------------------------- prompts
    void InitStyles()
    {
        if (styles) return;
        var f = game && game.interfaceFont ? game.interfaceFont : Resources.Load<Font>("CampusFont");
        promptStyle = new GUIStyle { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        boxStyle = new GUIStyle(GUI.skin.box);
        btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        if (f) { promptStyle.font = f; btnStyle.font = f; }
        promptStyle.normal.textColor = new Color(1f, 0.95f, 0.70f);
        styles = true;
    }

    public void DrawGUI()
    {
        InitStyles();
        if (!game || !game.player) return;
        if (!InLibrary()) return;
        if(SubjectsOpen){DrawSubjects();return;}
        if (game.paused || game.UiBlocking || (PhoneUI.Instance && PhoneUI.Instance.IsOpen) || VendingMachineUI.AnyOpen) return;

        // Study panel while seated.
        if (study && study.Seated)
        {
            DrawStudyPanel();
            return;
        }

        if (game.UiBlocking || !string.IsNullOrEmpty(nearMachine))
        {
            if (!string.IsNullOrEmpty(nearMachine))
                DrawPrompt("[ E ] 使用自动售卖机");
            return;
        }

        if (nearSeat == null)
        {
            var booking = LibraryReservationManager.Ensure();
            var reserved = LibrarySeatRegistry.Ensure().ById(booking.ReservedSeatId);
            if (reserved != null)
            {
                float metres = Vector2.Distance(new Vector2(game.player.position.x,game.player.position.z),new Vector2(reserved.sitPos.x,reserved.sitPos.z));
                DrawPrompt(Mathf.Abs(game.player.position.y-reserved.sitPos.y)>=2f
                    ? $"预约 {reserved.id} · 请前往 {reserved.floor}F，靠近座椅按 E"
                    : $"预约 {reserved.id} · 距离 {metres:F0} 米 · 靠近座椅按 E");
            }
            return;
        }

        var rm = LibraryReservationManager.Instance;
        bool mine = rm && rm.IsReservedSeat(nearSeat.id);

        // Only mark the seat the player is close to, and only on this floor, so nothing
        // shows through the slab.
        DrawSeatMarker(nearSeat, mine);

        DrawPrompt(mine
            ? $"座位 {nearSeat.id} · 已预约 · 按 E 坐下"
            : $"座位 {nearSeat.id} · 未预约 · 按 P 打开手机预约");
    }

    void DrawPrompt(string text)
    {
        float w = 460f, h = 42f;
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height - 206f, w, h);
        GUI.Box(r, "");
        GUI.Label(r, text, promptStyle);
    }

    // A small ring at the seat position, projected to screen space. Drawn only when the
    // player is close, which already implies the same floor.
    void DrawSeatMarker(LibrarySeat seat, bool mine)
    {
        var cam = game.followCamera ? game.followCamera : Camera.main;
        if (!cam) return;

        Vector3 world = seat.sitPos + Vector3.up * 0.45f;
        var sp = cam.WorldToScreenPoint(world);
        if (sp.z <= 0) return;

        var r = new Rect(sp.x - 26f, Screen.height - sp.y - 26f, 52f, 52f);
        var prev = GUI.color;
        GUI.color = mine ? new Color(0.45f, 0.95f, 0.55f, 0.55f) : new Color(0.9f, 0.9f, 0.9f, 0.3f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = prev;
    }

    void DrawStudyPanel()
    {
        float w = 380f, h = 230f;
        var box = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.32f, w, h);

        var prev = GUI.color;
        GUI.color = new Color(0.06f, 0.07f, 0.09f, 0.94f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = prev;

        var clock = GameClock.Ensure();
        var seat = study.Seat;

        GUI.Label(new Rect(box.x + 18f, box.y + 12f, w - 36f, 26f),
                  "座位 " + (seat != null ? seat.id : "") + " · " + (seat != null ? seat.areaName : ""), promptStyle);

        GUI.Label(new Rect(box.x + 18f, box.y + 44f, w - 36f, 22f), "当前时间 " + clock.DateTimeText, promptStyle);

        if (study.Studying)
        {
            double secs = study.StudyGameSeconds;
            GUI.Label(new Rect(box.x + 18f, box.y + 70f, w - 36f, 22f),
                      $"本次学习 {Mathf.FloorToInt((float)secs / 60f)} 分钟", promptStyle);
            GUI.Label(new Rect(box.x + 18f, box.y + 94f, w - 36f, 22f),
                      "时间加速 ×" + Mathf.RoundToInt(clock.CurrentMultiplier), promptStyle);

            if (GUI.Button(new Rect(box.x + 18f, box.y + 130f, w - 36f, 34f), "结束学习", btnStyle))
                study.StopStudy();
        }
        else
        {
            if (LibraryUiProbe.Button("seat:start",new Rect(box.x + 18f, box.y + 80f, w - 36f, 34f), "开始学习", btnStyle))
            {
                var semester=CampusSemester.Instance;
                if(semester&&semester.Ready)OpenSubjects();else study.StartStudy();
            }
        }

        if (LibraryUiProbe.Button("seat:stand",new Rect(box.x + 18f, box.y + h - 44f, (w - 48f) * 0.5f, 32f), "起身 (Q)", btnStyle))
            study.TryStandUp();

        if (LibraryUiProbe.Button("seat:release",new Rect(box.x + w - 18f - (w - 48f) * 0.5f, box.y + h - 44f, (w - 48f) * 0.5f, 32f),
                        "结束并释放座位", btnStyle))
            study.FinishAndRelease();
    }

    public void OpenSubjects(){
        if(!study||!study.Seated||!game||game.UiBlocking)return;
        SubjectsOpen=true;pendingCourse=null;studyFeedback="选择科目与时长。完成学习后仍保留座位。";
        game.SetUiCursor(true);CampusOverlayHub.ConsumeInput();
    }
    public void CloseSubjects(){
        if(!SubjectsOpen)return;
        SubjectsOpen=false;pendingCourse=null;game.SetUiCursor(false);CampusOverlayHub.ConsumeInput();
    }
    void ChooseStudy(string course,int minutes){
        var semester=CampusSemester.Instance;
        string conflict=semester.Conflict(minutes);
        if(!string.IsNullOrEmpty(conflict)){pendingCourse=course;pendingMinutes=minutes;studyFeedback=conflict;return;}
        semester.Study(course,minutes);studyFeedback=semester.LastMessage;
    }
    void DrawSubjects(){
        var semester=CampusSemester.Instance;if(!semester||!study||!study.Seated){CloseSubjects();return;}
        float scale=Mathf.Min(1,(Screen.width-24)/680f,(Screen.height-24)/530f);
        var origin=new Vector2((Screen.width-680*scale)/2,(Screen.height-530*scale)/2);
        System.Func<float,float,float,float,Rect> rect=(x,y,w,h)=>new Rect(origin+new Vector2(x,y)*scale,new Vector2(w,h)*scale);
        var font=game.interfaceFont;
        var heading=new GUIStyle(promptStyle){fontSize=Mathf.RoundToInt(26*scale),alignment=TextAnchor.MiddleLeft};
        var text=new GUIStyle(promptStyle){fontSize=Mathf.RoundToInt(18*scale),alignment=TextAnchor.UpperLeft,wordWrap=true,normal={textColor=CampusStoryStyle.Paper}};
        var action=new GUIStyle(btnStyle){fontSize=Mathf.RoundToInt(17*scale),wordWrap=true};
        var old=GUI.color;GUI.color=new Color(.025f,.035f,.05f,.70f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=old;
        CampusStoryStyle.Panel(rect(0,0,680,530));
        GUI.Label(rect(24,18,490,36),"图书馆自习 · "+study.Seat.id,heading);
        if(LibraryUiProbe.Button("study:close",rect(564,18,92,36),"返回座位",action)){CloseSubjects();return;}
        GUI.Label(rect(24,62,632,30),semester.Clock.DateTimeText+"  ·  精力 "+semester.State.energy+" / 100",text);
        if(pendingCourse!=null){
            GUI.Label(rect(24,126,632,120),"学习 "+pendingMinutes+"分钟将结束于 "+semester.Clock.Now.AddMinutes(pendingMinutes).ToString("HH:mm")+"。\n"+studyFeedback+"\n是否继续？",text);
            if(LibraryUiProbe.Button("confirm:accept",rect(24,284,306,48),"确认继续学习",action)){
                string course=pendingCourse;int minutes=pendingMinutes;pendingCourse=null;
                semester.Study(course,minutes,true);studyFeedback=semester.LastMessage;
            }
            if(LibraryUiProbe.Button("confirm:cancel",rect(350,284,306,48),"取消，保留座位",action))pendingCourse=null;
        }else{
            int row=0;
            foreach(var course in semester.State.courses){
                float y=108+row*86;
                GUI.Label(rect(24,y,632,26),course.name+" · 掌握度 "+course.mastery.ToString("F1")+" / 100",text);
                int column=0;
                foreach(int minutes in new[]{30,60,90}){
                    if(LibraryUiProbe.Button("study:"+course.id+":"+minutes,rect(24+column*216,y+30,200,42),minutes+"分钟 · 精力 "+minutes/6,action))ChooseStudy(course.id,minutes);
                    column++;
                }
                row++;
            }
        }
        GUI.Label(rect(24,380,632,104),studyFeedback,text);
        GUI.Label(rect(24,492,632,26),"关闭面板保留座位；返回后可起身或结束预约。",text);
    }
}
