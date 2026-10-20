using System.Collections.Generic;
using UnityEngine;

// Vending machine menu. One panel at a time; opens only when the player stands at the
// machine on its floor, and closes when they walk away or the scene unloads.
public class VendingMachineUI : MonoBehaviour, ICampusOverlay
{
    public static VendingMachineUI Instance;
    public static bool AnyOpen => Instance != null && Instance.open;
    public bool OverlayVisible => open;
    public void DrawOverlay() { DrawGUI(); }


    [Header("Machines (wired by the library builder)")]
    public Transform drinksMachine;
    public Transform snacksMachine;

    [Header("Reach")]
    public float reach = 2.6f;

    bool open;
    string machineId;
    VendingState state;
    int selected;
    int qty = 1;
    string message;
    float messageUntil;

    CampusGame game;
    GUIStyle title, body, small, btnStyle;
    bool styles;

    public static VendingMachineUI Ensure()
    {
        if (Instance) { CampusOverlayHub.Register(Instance); return Instance; }
        var sceneOne = FindFirstObjectByType<VendingMachineUI>();
        if (sceneOne) { Instance = sceneOne; sceneOne.enabled = true; CampusOverlayHub.Register(sceneOne); return sceneOne; }
        var go = new GameObject("VendingMachines");
        if (Application.isPlaying) DontDestroyOnLoad(go);
        Instance = go.AddComponent<VendingMachineUI>();
        return Instance;
    }

    void OnEnable() { if (Instance == this) CampusOverlayHub.Register(this); }

    void OnDestroy()
    {
        CampusOverlayHub.Unregister(this);
        if (Instance == this) Instance = null;
    }

    void Awake()
    {
        if (Instance && Instance != this)
        {
            // The library scene carries the machine anchors; transfer them to the live UI.
            if (drinksMachine) Instance.drinksMachine = drinksMachine;
            if (snacksMachine) Instance.snacksMachine = snacksMachine;
            enabled = false; Destroy(this); return;
        }
        Instance = this;
        CampusOverlayHub.Register(this);
        game = FindFirstObjectByType<CampusGame>();
    }

    // Which machine the player is standing at, on their own floor.
    // Public so the in-world interaction can decide who owns the E key.
    public string MachineAtPlayerPublic() => MachineAtPlayer();

    string MachineAtPlayer()
    {
        if (!game || !game.player) return null;
        Vector3 p = game.player.position;
        if (drinksMachine && Near(p, drinksMachine.position)) return "vending_drinks_1f";
        if (snacksMachine && Near(p, snacksMachine.position)) return "vending_snacks_1f";
        return null;
    }

    bool Near(Vector3 playerPos, Vector3 machinePos)
    {
        if (Mathf.Abs(playerPos.y - machinePos.y) >= 2.0f) return false;
        return (new Vector2(playerPos.x, playerPos.z) - new Vector2(machinePos.x, machinePos.z)).magnitude <= reach;
    }

    void Update()
    {
        if (!game) { game = FindFirstObjectByType<CampusGame>(); if (!game) return; }

        if (open)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            // Walking away closes the panel so the cursor lock cannot be stranded.
            if (MachineAtPlayer() != machineId) { Close(); }
        }
    }

    public void OpenAt(string id)
    {
        if (PhoneUI.Instance && PhoneUI.Instance.IsOpen) PhoneUI.Instance.Close();
        machineId = id;
        state = VendingSystem.Load(id);
        selected = 0;
        qty = 1;
        if (!game) game = FindFirstObjectByType<CampusGame>();
        CampusOverlayHub.ConsumeInput();
        open = true;
        if (game) game.SetUiCursor(true);
    }

    public void Close()
    {
        if (!open) return;
        CampusOverlayHub.ConsumeInput();
        open = false;
        if (game) game.SetUiCursor(false);
    }

    void OnDisable() { if (open) Close(); CampusOverlayHub.Unregister(this); }

    void Toast(string m) { message = m; messageUntil = Time.unscaledTime + 3f; }

    void InitStyles()
    {
        if (styles) return;
        var f = game && game.interfaceFont ? game.interfaceFont : Resources.Load<Font>("CampusFont");
        title = new GUIStyle { fontSize = 20, fontStyle = FontStyle.Bold };
        body = new GUIStyle { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        small = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleLeft };
        btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        if (f) { title.font = f; body.font = f; small.font = f; btnStyle.font = f; }
        title.normal.textColor = new Color(0.95f, 0.95f, 0.98f);
        body.normal.textColor = new Color(0.86f, 0.88f, 0.92f);
        small.normal.textColor = new Color(0.68f, 0.72f, 0.78f);
        styles = true;
    }

    public void DrawGUI()
    {
        if (!open) return;
        InitStyles();

        float w = Mathf.Min(520f, Screen.width * 0.8f);
        float h = Mathf.Min(430f, Screen.height * 0.85f);
        var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

        var prev = GUI.color;
        GUI.DrawTexture(box, PhoneUI.SolidFill, ScaleMode.StretchToFill);
        GUI.color = prev;

        string name = machineId == "vending_drinks_1f" ? "自动售卖机 · 饮品" : "自动售卖机 · 零食";
        GUI.Label(new Rect(box.x + 20f, box.y + 14f, w - 40f, 28f), name, title);
        // The allowance is shown once, by the persistent HUD, never duplicated here.
        GUI.Label(new Rect(box.x + w - 180f, box.y + 18f, 160f, 22f), "生活费见右上角", small);

        float y = box.y + 56f;
        for (int i = 0; i < state.items.Count; i++)
        {
            var it = state.items[i];
            bool soldOut = it.stock <= 0;

            var row = new Rect(box.x + 16f, y, w - 32f, 46f);
            if (i == selected)
            {
                GUI.color = new Color(0.16f, 0.20f, 0.26f, 1f);
                GUI.DrawTexture(row, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            GUI.Label(new Rect(row.x + 12f, row.y + 12f, row.width * 0.45f, 24f),
                      soldOut ? it.name + "（售罄）" : it.name, body);
            GUI.Label(new Rect(row.x + row.width * 0.50f, row.y + 12f, 90f, 24f),
                      "¥" + (it.priceFen / 100.0).ToString("F2"), body);
            GUI.Label(new Rect(row.x + row.width * 0.68f, row.y + 12f, 90f, 24f),
                      "库存 " + it.stock, small);

            if (!soldOut && GUI.Button(row, GUIContent.none, GUIStyle.none))
            { selected = i; qty = 1; }
            y += 50f;
        }

        // quantity row
        float qy = box.y + h - 118f;
        GUI.Label(new Rect(box.x + 20f, qy, 70f, 26f), "数量", body);
        if (GUI.Button(new Rect(box.x + 70f, qy, 30f, 26f), "−", btnStyle)) qty = Mathf.Max(1, qty - 1);
        GUI.Label(new Rect(box.x + 104f, qy, 40f, 26f), qty.ToString(), body);
        if (GUI.Button(new Rect(box.x + 140f, qy, 30f, 26f), "+", btnStyle)) qty = Mathf.Min(99, qty + 1);

        long total = (state.items.Count > selected) ? (long)state.items[selected].priceFen * qty : 0;
        GUI.Label(new Rect(box.x + 200f, qy, w - 220f, 26f), "合计 ¥" + (total / 100.0).ToString("F2"), body);

        var buyRect = new Rect(box.x + 20f, box.y + h - 80f, w - 150f, 36f);
        bool buyClicked = GUI.Button(buyRect, "确认购买", UIDrawRecorder.Style("vending:buy", btnStyle, buyRect));
        // An injected pass reports the aimed control; treat it exactly like a click.
        buyClicked |= UIDrawRecorder.InjectingClick && UIDrawRecorder.ClickTarget == "vending:buy";
        if (buyClicked)
        {
            string msg;
            if (VendingSystem.TryBuy(state, selected, qty, out msg))
            {
                Toast(msg);
                // Reload so the shown stock matches what was just written.
                state = VendingSystem.Load(machineId);
            }
            else Toast("购买失败：" + msg);
        }
        if (GUI.Button(new Rect(box.x + w - 120f, box.y + h - 80f, 100f, 36f), "关闭 (ESC)", btnStyle))
            Close();

        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            GUI.Label(new Rect(box.x + 20f, box.y + h - 36f, w - 40f, 24f), message, small);

        UIDrawRecorder.EndPass();

        // Inventory strip so bought goods are visible and usable.
        var inv = VendingSystem.LoadInventory();
        if (inv.names.Count > 0)
        {
            string line = "持有：";
            for (int i = 0; i < inv.names.Count; i++)
                line += inv.names[i] + "×" + inv.counts[i] + "  ";
            GUI.Label(new Rect(box.x + 20f, box.y + h - 14f, w - 40f, 20f), line, small);
        }
    }
}
