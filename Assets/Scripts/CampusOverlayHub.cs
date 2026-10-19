using System.Collections.Generic;
using UnityEngine;

// One painter dispatches every IMGUI event (layout, input AND repaint).
public interface ICampusOverlay
{
    bool OverlayVisible { get; }
    void DrawOverlay();
}

public static class CampusOverlayHub
{
    static readonly List<ICampusOverlay> overlays = new List<ICampusOverlay>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForNewSession() { overlays.Clear(); InputConsumedFrame = -1; }

    public static int InputConsumedFrame { get; private set; } = -1;
    public static void ConsumeInput() { InputConsumedFrame = Time.frameCount; }

    public static void Register(ICampusOverlay o)
    {
        if (o == null || overlays.Contains(o)) return;
        overlays.Add(o);
        if (Application.isPlaying) CampusOverlayPainter.Ensure();
    }

    public static void Unregister(ICampusOverlay o) { overlays.Remove(o); }

    // Called only by CampusOverlayPainter. A frame guard would consume Layout and
    // skip Repaint/MouseUp, leaving both pixels and buttons broken.
    public static void Draw()
    {
        for (int i = overlays.Count - 1; i >= 0; i--)
        {
            var o = overlays[i];
            // Interface references do not use Unity's destroyed-object null check.
            if (o is Object owner && !owner) { overlays.RemoveAt(i); continue; }
            if (o is Behaviour behaviour && !behaviour.isActiveAndEnabled) continue;
            if (!o.OverlayVisible) continue;
            var matrix = GUI.matrix;
            var color = GUI.color;
            var contentColor = GUI.contentColor;
            var backgroundColor = GUI.backgroundColor;
            var skin = GUI.skin;
            bool enabled = GUI.enabled;
            try
            {
                GUI.matrix = Matrix4x4.identity;
                GUI.color = GUI.contentColor = GUI.backgroundColor = Color.white;
                GUI.enabled = true;
                o.DrawOverlay();
            }
            finally
            {
                GUI.matrix = matrix;
                GUI.color = color;
                GUI.contentColor = contentColor;
                GUI.backgroundColor = backgroundColor;
                GUI.skin = skin;
                GUI.enabled = enabled;
            }
        }
    }
}
