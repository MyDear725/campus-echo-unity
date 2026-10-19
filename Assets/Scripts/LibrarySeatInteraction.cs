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

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (nearMachine != null) { vending.OpenAt(nearMachine); return; }

            if (nearSeat != null)
            {
                var rm = LibraryReservationManager.Instance;
                bool mine = rm && rm.IsReservedSeat(nearSeat.id);
                if (mine)
                {
                    study.TrySit(nearSeat);
                    return;
                }
                game.Notify("该座位未预约，请打开手机（P）预约后再使用。");
                return;
            }
        }
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
        if (game.paused || (PhoneUI.Instance && PhoneUI.Instance.IsOpen) || VendingMachineUI.AnyOpen) return;

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

        if (nearSeat == null) return;

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
            if (GUI.Button(new Rect(box.x + 18f, box.y + 80f, w - 36f, 34f), "开始学习", btnStyle))
                study.StartStudy();
        }

        if (GUI.Button(new Rect(box.x + 18f, box.y + h - 44f, (w - 48f) * 0.5f, 32f), "起身 (Q)", btnStyle))
            study.TryStandUp();

        if (GUI.Button(new Rect(box.x + w - 18f - (w - 48f) * 0.5f, box.y + h - 44f, (w - 48f) * 0.5f, 32f),
                        "结束并释放座位", btnStyle))
            study.FinishAndRelease();
    }
}
