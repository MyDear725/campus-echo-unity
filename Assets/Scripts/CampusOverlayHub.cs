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
    public static int InputRevision { get; private set; }
    public static void ConsumeInput() { InputConsumedFrame = Time.frameCount; InputRevision++; }

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
        ICampusOverlay modal=null;
        if(LibrarySeatInteraction.Instance&&LibrarySeatInteraction.Instance.SubjectsOpen)modal=LibrarySeatInteraction.Instance;
        else if(CampusSemester.Instance&&CampusSemester.Instance.UI&&CampusSemester.Instance.UI.IsOpen)modal=CampusSemester.Instance.UI;
        else if(PhoneUI.Instance&&PhoneUI.Instance.IsOpen)modal=PhoneUI.Instance;
        bool input=Event.current.type!=EventType.Layout&&Event.current.type!=EventType.Repaint;
        int first=modal!=null?overlays.Count:overlays.Count-1;
        for (int i = first; i >= 0; i--)
        {
            var o = i==overlays.Count?modal:overlays[i];
            if(modal!=null&&i<first&&(ReferenceEquals(o,modal)||input))continue;
            int revision=InputRevision;
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
            if(input&&InputRevision!=revision)break;
        }
    }
}
