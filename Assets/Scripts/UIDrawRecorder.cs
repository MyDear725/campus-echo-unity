using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Automated-input support for the graphical regression (AllowanceShopQA).
///
/// While active, each interactive control records its screen rect during the normal draw
/// pass. The run then injects a real IMGUI MouseDown/MouseUp at that rect's centre; for
/// that single pass the target control (and only the target) receives a unique style name,
/// which is what Unity's IMGUI uses to decide that the hot control under the mouse is the
/// button that gets clicked. The click therefore travels the same path as a physical one.
///
/// Off unless -allowanceQA is on the command line.
/// </summary>
public static class UIDrawRecorder
{
    public static bool Enabled;

    static readonly Dictionary<string, Rect> lastRects = new Dictionary<string, Rect>();
    static readonly Dictionary<string, Rect> passRects = new Dictionary<string, Rect>();

    // Key of the control the current injected click is aimed at (null when none).
    public static string ClickTarget { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Reset(); }

    public static void Reset()
    {
        lastRects.Clear();
        passRects.Clear();
        ClickTarget = null;
        Enabled = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-allowanceQA") >= 0;
    }

    public static void BeginPass() { if (Enabled) passRects.Clear(); }

    public static void EndPass()
    {
        if (!Enabled) return;
        foreach (var kv in passRects) lastRects[kv.Key] = kv.Value;
    }

    public static bool HasRect(string key) { return Enabled && lastRects.ContainsKey(key); }

    public static Rect RectOf(string key) { return lastRects[key]; }

    public static Vector2 CenterOf(string key) { return lastRects[key].center; }

    // Aim the next injected click at this control.
    public static void AimAt(string key) { ClickTarget = key; }

    // True during the injected pass that carries the click into the aimed control.
    public static bool InjectingClick { get; set; }

    // Return the style to draw with: records the rect, and names the control when it is
    // the aimed click target so IMGUI hands it the click.
    public static GUIStyle Style(string key, GUIStyle baseStyle, Rect r)
    {
        if (!Enabled || string.IsNullOrEmpty(key)) return baseStyle;
        passRects[key] = r;
        if (ClickTarget != key) return baseStyle;

        var named = new GUIStyle(baseStyle);
        named.name = key;
        return named;
    }
}
