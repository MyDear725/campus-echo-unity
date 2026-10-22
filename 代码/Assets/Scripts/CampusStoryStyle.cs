using UnityEngine;

// Shared, resolution-independent IMGUI surfaces for story dialogue and guidance.
// Native rounded drawing also handles slim progress bars without sliced-corner artifacts.
public static class CampusStoryStyle
{
    public static readonly Color Ink = new Color(.065f, .095f, .13f, .96f);
    public static readonly Color Surface = new Color(.12f, .16f, .20f, .97f);
    public static readonly Color Paper = new Color(.96f, .95f, .90f);
    public static readonly Color Muted = new Color(.65f, .73f, .77f);
    public static readonly Color Gold = new Color(.87f, .74f, .48f);
    public static readonly Color Line = new Color(.60f, .72f, .78f, .18f);
    static GUIStyle guideTitle, guideBody, guideMeta, guideKey;
    static Font guideFont;

    public static void Round(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint) return;
        GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0,
            Mathf.Min(14, Mathf.Min(rect.width, rect.height) / 2));
    }
    public static void Fill(Rect rect, Color color)
    {
        var old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old;
    }
    public static void Panel(Rect rect)
    {
        Round(new Rect(rect.x - 3, rect.y + 6, rect.width + 6, rect.height + 4), new Color(0, 0, 0, .17f));
        Round(rect, Line);
        Round(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), Ink);
    }
    public static GUIStyle Label(Font font, int size, Color color, bool bold = false, TextAnchor alignment = TextAnchor.UpperLeft)
    {
        return new GUIStyle { font = font, fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            wordWrap = true, richText = false, alignment = alignment, normal = { textColor = color } };
    }
    public static bool Button(Rect rect, string text, GUIStyle style, bool primary = false)
    {
        bool hovered = GUI.enabled && rect.Contains(Event.current.mousePosition);
        Round(rect, primary ? (hovered ? new Color(.96f, .85f, .63f) : Gold)
            : hovered ? new Color(.21f, .28f, .33f) : Surface);
        if (!primary)
            Fill(new Rect(rect.x + 16, rect.yMax - 1, rect.width - 32, 1), Line);
        var labelRect = new Rect(rect.x + 16, rect.y, rect.width - 32, rect.height);
        GUI.Label(labelRect, text, style);
        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
    }
    public static void Key(Rect rect, string key, GUIStyle style)
    {
        Round(rect, new Color(.60f, .72f, .78f, .13f)); GUI.Label(rect, key, style);
    }
    public static void Guidance(Font font, float canvasWidth, string heading, string objective, int completed, int count)
    {
        if (guideBody == null || guideFont != font)
        {
            guideFont = font;
            guideTitle = Label(font, 20, Paper, true);
            guideBody = Label(font, 15, Paper);
            guideMeta = Label(font, 11, Muted);
            guideKey = Label(font, 12, Muted, false, TextAnchor.MiddleCenter);
        }
        float width = Mathf.Min(390, canvasWidth - 40);
        float bodyHeight = guideBody.CalcHeight(new GUIContent(objective), width - 48);
        var panel = new Rect(24, 24, width, 112 + bodyHeight);
        Panel(panel);
        Fill(new Rect(panel.x + 24, panel.y + 23, 18, 2), Gold);
        GUI.Label(new Rect(panel.x + 51, panel.y + 15, width - 128, 20), count > 0 ? "迎新第一章 · 剧情指引" : "校园生活 · 当前目标", guideMeta);
        Key(new Rect(panel.xMax - 47, panel.y + 14, 25, 23), "J", guideKey);
        GUI.Label(new Rect(panel.x + 24, panel.y + 47, width - 48, 29), heading, guideTitle);
        GUI.Label(new Rect(panel.x + 24, panel.y + 85, width - 48, bodyHeight), objective, guideBody);
        if (count > 0)
        {
            float segment = (width - 48 - (count - 1) * 5) / count;
            for (int i = 0; i < count; i++)
                Round(new Rect(panel.x + 24 + i * (segment + 5), panel.yMax - 13, segment, 3), i < completed ? Gold : Line);
        }
    }
}
