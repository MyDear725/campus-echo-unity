using System;
using System.Linq;
using UnityEngine;

public class CafeteriaIndoorInteractions : MonoBehaviour
{
    CampusGame game;

    struct InteractiveSpot
    {
        public string id;
        public Vector3 position;
        public float radius;
        public string prompt;
        public string message;
    }

    static readonly InteractiveSpot[] spots = new InteractiveSpot[]
    {
        // --- 1F Spots ---
        new InteractiveSpot
        {
            id = "cutlery_1f",
            position = new Vector3(-16.5f, 0.5f, 4.5f),
            radius = 2.4f,
            prompt = "[ E ] 领取餐盘与消毒筷勺",
            message = "已领取餐盘与不锈钢消毒餐具，请顺着通道前往北侧打菜选餐区！"
        },
        new InteractiveSpot
        {
            id = "washbasin_1f",
            position = new Vector3(16.5f, 0.5f, -3.0f),
            radius = 2.6f,
            prompt = "[ E ] 使用洗手间 / 洁手消毒",
            message = "使用东侧洗手间与感应洁手台，完成手部清洁消毒！"
        },
        new InteractiveSpot
        {
            id = "stairs_1_up",
            position = new Vector3(-10.2f, 0.5f, -12.5f),
            radius = 2.4f,
            prompt = "[ E ] 楼梯 1 · 从右向左（朝西）上楼",
            message = "沿西南角楼梯 1 从右向左（向西）拾级而上，前往二层特色餐厅！"
        },
        new InteractiveSpot
        {
            id = "stairs_2_up",
            position = new Vector3(6.0f, 0.5f, -15.0f),
            radius = 2.4f,
            prompt = "[ E ] 楼梯 2 · 朝北攀升前往二层",
            message = "沿东南门旁楼梯 2 向北拾级而上，前往二层特色餐厅！"
        },

        // --- 2F Spots ---
        new InteractiveSpot
        {
            id = "washbasin_2f",
            position = new Vector3(16.5f, 4.8f, -3.0f),
            radius = 2.6f,
            prompt = "[ E ] 二层洗手间 · 洁手消毒",
            message = "使用二层东侧洗手间与感应洁手台，完成手部清洁消毒！"
        },
        new InteractiveSpot
        {
            id = "stairs_1_down",
            position = new Vector3(-17.2f, 4.8f, -12.5f),
            radius = 2.4f,
            prompt = "[ E ] 楼梯 1 · 向东下楼返回一层",
            message = "沿西南角楼梯 1 向东缓步下楼，返回一层大众餐厅！"
        },
        new InteractiveSpot
        {
            id = "stairs_2_down",
            position = new Vector3(6.0f, 4.8f, -8.6f),
            radius = 2.4f,
            prompt = "[ E ] 楼梯 2 · 向南下楼返回一层",
            message = "沿东南侧楼梯 2 向南缓步下楼，返回一层大众餐厅东南门厅！"
        },
        new InteractiveSpot
        {
            id = "stairs_1_up_2f",
            position = new Vector3(-10.2f, 4.8f, -12.5f),
            radius = 2.4f,
            prompt = "[ E ] 楼梯 1 · 朝西上楼前往三层",
            message = "沿西南角楼梯 1 从右向左（向西）上楼，前往三层鲜果市与自选餐厅！"
        },
        new InteractiveSpot
        {
            id = "stairs_2_up_2f",
            position = new Vector3(6.0f, 4.8f, -15.0f),
            radius = 2.4f,
            prompt = "[ E ] 楼梯 2 · 朝北上楼前往三层",
            message = "沿东南侧楼梯 2 向北上楼，前往三层鲜果市与自选餐厅！"
        },

        // --- 3F Spots ---
        new InteractiveSpot
        {
            id = "fruit_3f",
            position = new Vector3(-4.0f, 9.1f, 11.5f),
            radius = 3.5f,
            prompt = "[ E ] 选购新鲜水果 / 鲜切拼盘",
            message = "挑选了新鲜切块西瓜与红富士苹果，清甜爽口，健康满分！"
        },
        new InteractiveSpot
        {
            id = "weigh_3f",
            position = new Vector3(5.0f, 9.1f, 11.5f),
            radius = 2.5f,
            prompt = "[ E ] 鲜果超市称重结账",
            message = "在称重台完成水果称重打包，方便快捷！"
        },
        new InteractiveSpot
        {
            id = "washbasin_3f",
            position = new Vector3(16.5f, 9.1f, -3.0f),
            radius = 2.6f,
            prompt = "[ E ] 三层洗手间 · 洁手消毒",
            message = "使用三层东侧洗手间与感应洁手台，完成手部清洁消毒！"
        },
        new InteractiveSpot
        {
            id = "cutlery_3f",
            position = new Vector3(-15.5f, 9.1f, -8.0f),
            radius = 2.4f,
            prompt = "[ E ] 领取三层餐盘与消毒筷勺",
            message = "已领取三层餐盘与消毒餐具！"
        },
        new InteractiveSpot
        {
            id = "stairs_1_down_3f",
            position = new Vector3(-17.2f, 9.1f, -12.5f),
            radius = 2.4f,
            prompt = "[ E ] 楼梯 1 · 朝东下楼返回二层",
            message = "沿西南角楼梯 1 向东缓步下楼，返回二层风味特色餐厅！"
        },
        new InteractiveSpot
        {
            id = "stairs_2_down_3f",
            position = new Vector3(6.0f, 9.1f, -8.6f),
            radius = 2.4f,
            prompt = "[ E ] 楼梯 2 · 朝南下楼返回二层",
            message = "沿东南侧楼梯 2 向南缓步下楼，返回二层风味特色餐厅！"
        }
    };

    void Start()
    {
        game = FindFirstObjectByType<CampusGame>();
    }

    void Update()
    {
        if (!game || !game.Interiors || !game.Interiors.Inside || game.paused) return;
        if (game.Interiors.Interior == null || game.Interiors.Interior.buildingId != "cafeteria") return;
        if (game.Interiors.Interior.NearExit(game.player.position)) return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            // CampusGame is the single E dispatcher. It either hands the press to the
            // ordering system (which then owns this frame) or to us; it never does both,
            // so no spot can fire together with a counter, exit or seat action.
            if (CampusOverlayHub.InputConsumedFrame == Time.frameCount) return;

            var spot = CurrentSpot();
            if (spot.HasValue)
            {
                game.Notify(spot.Value.message);
            }
        }
    }

    InteractiveSpot? CurrentSpot()
    {
        if (!game || !game.player) return null;
        Vector3 p = game.player.position;

        foreach (var s in spots)
        {
            float dist = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(s.position.x, s.position.z));
            if (dist <= s.radius && Mathf.Abs(p.y - s.position.y) < 2.0f)
            {
                return s;
            }
        }
        return null;
    }

    void OnGUI()
    {
        if (!game || !game.Interiors || !game.Interiors.Inside || game.paused) return;
        if (game.Interiors.Interior == null || game.Interiors.Interior.buildingId != "cafeteria") return;
        if (game.Interiors.Interior.NearExit(game.player.position)) return;

        // One prompt at a time: when a counter, pickup point, seat or tray return is in
        // reach, CampusHud already draws that prompt, so this legacy one stays silent.
        var ordering = CampusSystemsRegistry.FindOrdering();
        if (ordering != null && ordering.OwnsSpotAt(game.player.position)) return;

        var spot = CurrentSpot();
        if (!spot.HasValue) return;

        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;

        float w = 380, h = 42;
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
}
