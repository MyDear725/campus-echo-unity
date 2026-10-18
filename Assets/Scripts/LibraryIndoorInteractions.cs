using System;
using UnityEngine;

// Interactive spots inside the library interior. Mirrors CafeteriaIndoorInteractions:
// CampusGame owns the single E-key dispatcher, so this component only reacts to spots
// that the travel service did not already consume (i.e. when the player is not at an exit).
public class LibraryIndoorInteractions : MonoBehaviour
{
    CampusGame game;

    struct InteractiveSpot
    {
        public string id;
        public Vector3 position;
        public float radius;
        public float heightTolerance;
        public string prompt;
        public string message;

        // 1F spots sit at y≈1.0 and the player capsule centre is near y≈1.5, so a
        // tolerance of 2.0m separates floors cleanly without needing per-spot values.
        public float Tolerance => heightTolerance > 0f ? heightTolerance : 2.0f;
    }

    static readonly InteractiveSpot[] spots = new InteractiveSpot[]
    {
        // ---------------------------------------------------------- 1F lobby
        new InteractiveSpot
        {
            id = "vending_drinks_1f",
            position = new Vector3(-4.0f, 1.0f, -14.6f),
            radius = 2.8f,
            prompt = "[ E ] 自动售卖机 · 购买饮品",
            message = "在自动售卖机上买了一瓶冰镇矿泉水，清爽解渴。"
        },
        new InteractiveSpot
        {
            id = "vending_snacks_1f",
            position = new Vector3(4.0f, 1.0f, -14.6f),
            radius = 2.8f,
            prompt = "[ E ] 自动售卖机 · 购买零食",
            message = "买了一包坚果能量棒，补充一下体力再继续看书。"
        },
        new InteractiveSpot
        {
            id = "sandtable_1f",
            position = new Vector3(0f, 1.2f, 4.0f),
            radius = 3.6f,
            prompt = "[ E ] 参观校园规划沙盘",
            message = "校园沙盘一览：教学区、图书馆、食堂、体育馆、公寓与涌泉湖的相对位置一目了然。"
        },
        new InteractiveSpot
        {
            id = "reception_1f",
            position = new Vector3(-10.5f, 1.0f, -3.6f),
            radius = 3.0f,
            prompt = "[ E ] 咨询总服务台",
            message = "馆员：二层为中文图书与期刊阅览，三层是密集书库、研讨间和古籍特藏，电梯可直达。"
        },
        new InteractiveSpot
        {
            id = "return_chute_1f",
            position = new Vector3(-7.3f, 1.0f, -5.0f),
            radius = 2.2f,
            prompt = "[ E ] 归还图书",
            message = "把书投入还书口，系统已自动登记归还。"
        },
        new InteractiveSpot
        {
            id = "plan_panel_1f",
            position = new Vector3(-18.0f, 1.6f, 0.5f),
            radius = 2.6f,
            prompt = "[ E ] 查看馆舍平面导览",
            message = "平面导览：一层大厅与校史展览馆、二层图书阅览区、三层书库与研讨间。"
        },
        new InteractiveSpot
        {
            id = "hours_panel_1f",
            position = new Vector3(-9.0f, 1.6f, -14.4f),
            radius = 2.6f,
            prompt = "[ E ] 查看开馆时间与借阅规则",
            message = "开馆时间 08:00–22:00；本科生可借 20 册，借期 30 天，可续借两次。"
        },
        new InteractiveSpot
        {
            id = "history_panel",
            position = new Vector3(18.2f, 2.0f, 0.0f),
            radius = 3.6f,
            prompt = "[ E ] 参观校史展览馆",
            message = "校史展览：展板记录了学校自 1949 年建校以来的沿革、校区建设、名师校友、科研学科与国际交流。"
        },
        new InteractiveSpot
        {
            id = "restroom_1f",
            position = new Vector3(15.6f, 1.0f, 6.0f),
            radius = 2.8f,
            prompt = "[ E ] 使用洗手间 / 洁手消毒",
            message = "使用一层卫生间与感应洁手台，完成手部清洁消毒。"
        },
        new InteractiveSpot
        {
            id = "stairs_1f",
            position = new Vector3(8.2f, 1.0f, -11.4f),
            radius = 3.0f,
            prompt = "[ E ] 沿楼梯上行",
            message = "双跑折返楼梯，上到中间平台后折返继续上行，可到二层。"
        },
        new InteractiveSpot
        {
            id = "elevator_1f",
            position = new Vector3(14.0f, 1.0f, -11.4f),
            radius = 3.0f,
            prompt = "[ E ] 乘坐电梯 · 选择楼层",
            message = ""
        },
        new InteractiveSpot
        {
            id = "elevator_2f",
            position = new Vector3(14.0f, 5.3f, -11.4f),
            radius = 3.0f,
            heightTolerance = 2.0f,
            prompt = "[ E ] 乘坐电梯 · 选择楼层",
            message = ""
        },
        new InteractiveSpot
        {
            id = "elevator_3f",
            position = new Vector3(14.0f, 9.6f, -11.4f),
            radius = 3.0f,
            heightTolerance = 2.0f,
            prompt = "[ E ] 乘坐电梯 · 选择楼层",
            message = ""
        },
        new InteractiveSpot
        {
            id = "elevator_4f",
            position = new Vector3(14.0f, 13.9f, -11.4f),
            radius = 3.0f,
            heightTolerance = 2.0f,
            prompt = "[ E ] 乘坐电梯 · 选择楼层",
            message = ""
        },

    };

    // ---- Lift floor picker -------------------------------------------------
    // Pressing E at a lift door opens this instead of printing a message; the
    // player then picks a storey with 1-4 or by clicking, and is repositioned at
    // that storey's lift lobby.
    bool liftOpen;
    const float StoreyHeight = 4.3f;   // must match LibraryInteriorBuilder.FloorH

    void Start()
    {
        game = FindFirstObjectByType<CampusGame>();
    }

    static string FloorName(int floor) =>
        floor == 1 ? "一层" : floor == 2 ? "二层" : floor == 3 ? "三层" : "四层";

    static string FloorSubtitle(int floor) =>
        floor == 1 ? "入口门厅 · 校园沙盘" : FloorName(floor) + " · 圆形自习厅";

    int CurrentFloor()
    {
        if (!game || !game.player) return 1;
        return Mathf.Clamp(Mathf.RoundToInt(game.player.position.y / StoreyHeight) + 1, 1, 4);
    }

    void OpenLift()
    {
        if (liftOpen) return;
        liftOpen = true;
        if (game) game.SetUiCursor(true);
    }

    void CloseLift()
    {
        if (!liftOpen) return;
        liftOpen = false;
        if (game) game.SetUiCursor(false);
    }

    // Move to the lift lobby on the chosen storey. The CharacterController has to be
    // switched off around the reposition, exactly as BuildingInteriorTravel does.
    void ChooseFloor(int floor)
    {
        if (!game || !game.player) { CloseLift(); return; }
        if (floor == CurrentFloor()) { CloseLift(); return; }

        var cc = game.Interiors ? game.Interiors.IndoorController : null;
        if (cc) cc.enabled = false;
        game.SetTravelPose(new Vector3(14.0f, (floor - 1) * StoreyHeight + 0.06f, -10.6f), 0f, 8f);
        Physics.SyncTransforms();
        if (cc) cc.enabled = true;

        CloseLift();
        game.Notify("电梯已到达 " + FloorName(floor) + "。");
    }

    bool InLibrary()
    {
        return game && game.Interiors && game.Interiors.Inside
            && game.Interiors.Interior != null
            && game.Interiors.Interior.buildingId == "library";
    }

    void Update()
    {
        if (!InLibrary() || game.paused) { CloseLift(); return; }

        if (liftOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E)) { CloseLift(); return; }
            for (int f = 1; f <= 4; f++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + (f - 1)) ||
                    Input.GetKeyDown(KeyCode.Keypad1 + (f - 1)))
                {
                    ChooseFloor(f);
                    return;
                }
            }
            return;   // no world interaction while the picker is up
        }

        // The exit prompt owns E while the player stands at a door.
        if (game.Interiors.Interior.NearExit(game.player.position)) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            var spot = NearbySpot();
            if (!spot.HasValue) return;
            if (spot.Value.id.StartsWith("elevator")) { OpenLift(); return; }
            game.Notify(spot.Value.message);
        }
    }

    // The one and only interaction-point selector. The on-screen prompt and the E-key
    // action both go through this, so a spot can never be advertised and then a
    // different one triggered. Where ranges overlap (e.g. the reception desk and the
    // book-return chute), the CLOSEST eligible spot wins, decided by squared distance.
    static InteractiveSpot? PickSpot(Vector3 p)
    {
        InteractiveSpot? best = null;
        float bestSqr = float.MaxValue;

        foreach (var s in spots)
        {
            if (Mathf.Abs(p.y - s.position.y) >= s.Tolerance) continue;
            float sqr = (new Vector2(p.x, p.z) - new Vector2(s.position.x, s.position.z)).sqrMagnitude;
            if (sqr > s.radius * s.radius) continue;
            if (sqr < bestSqr) { bestSqr = sqr; best = s; }
        }
        return best;
    }

    // The spot the player is currently in, or null.
    InteractiveSpot? NearbySpot()
    {
        if (!game || !game.player) return null;
        return PickSpot(game.player.position);
    }

    // Test hook: the id of the spot that would be selected at a given position, so the
    // prompt/action consistency can be asserted from the editor without entering play
    // mode. Returns null when no spot is in range.
    public static string PickSpotId(Vector3 playerPosition)
    {
        var s = PickSpot(playerPosition);
        return s.HasValue ? s.Value.id : null;
    }

    void OnGUI()
    {
        if (!InLibrary() || game.paused) return;
        if (liftOpen) { DrawLiftPicker(); return; }
        if (game.Interiors.Interior.NearExit(game.player.position)) return;
        if (game.Interiors.Busy) return;

        var spot = NearbySpot();
        if (!spot.HasValue) return;

        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;

        float w = 400, h = 42;
        Rect rect = new Rect((Screen.width - w) * 0.5f, Screen.height - 110, w, h);
        GUI.Box(rect, "");

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            font = game.interfaceFont,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = new Color(1.0f, 0.95f, 0.70f);

        GUI.Label(rect, spot.Value.prompt, style);
        GUI.matrix = matrix;
    }

    void DrawLiftPicker()
    {
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;

        const float w = 460f, h = 320f;
        var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        // Dim the scene behind the panel.
        var oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;

        GUI.Box(box, "");

        var title = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            font = game.interfaceFont,
            fontSize = 24,
            fontStyle = FontStyle.Bold
        };
        title.normal.textColor = new Color(1f, 0.92f, 0.65f);
        GUI.Label(new Rect(box.x, box.y + 12, w, 32), "电梯 ELEVATOR", title);

        var sub = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            font = game.interfaceFont,
            fontSize = 15
        };
        sub.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
        int here = CurrentFloor();
        GUI.Label(new Rect(box.x, box.y + 44, w, 22), "当前位置：" + FloorName(here) + " · 选择要去的楼层", sub);

        var btn = new GUIStyle(GUI.skin.button) { font = game.interfaceFont, fontSize = 18 };

        for (int f = 1; f <= 4; f++)
        {
            float bw = 190f, bh = 62f;
            float bx = box.x + 24f + ((f - 1) % 2) * (bw + 28f);
            float by = box.y + 82f + ((f - 1) / 2) * (bh + 18f);

            bool isHere = f == here;
            string label = f + "F  " + FloorName(f) + (isHere ? "（当前）" : "");

            // Grey out the floor we are already on.
            bool prevEnabled = GUI.enabled;
            GUI.enabled = !isHere;
            if (GUI.Button(new Rect(bx, by, bw, bh), label, btn)) ChooseFloor(f);
            GUI.enabled = prevEnabled;

            var cap = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                font = game.interfaceFont,
                fontSize = 12
            };
            cap.normal.textColor = new Color(0.75f, 0.78f, 0.82f);
            GUI.Label(new Rect(bx, by + bh - 18f, bw, 16f), FloorSubtitle(f), cap);
        }

        var hint = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            font = game.interfaceFont,
            fontSize = 14
        };
        hint.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        GUI.Label(new Rect(box.x, box.y + h - 34f, w, 22f), "按 1 - 4 选择楼层，或点击按钮      ESC / E 取消", hint);

        GUI.matrix = matrix;
    }
}
