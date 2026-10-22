using System.Collections.Generic;
using UnityEngine;
// Opt-in observation only: tests still deliver ordinary IMGUI mouse events.
public static class LibraryUiProbe {
    public static bool Enabled;
    public static readonly Dictionary<string,Rect> Rects=new Dictionary<string,Rect>();
    static readonly Dictionary<string,int> drawn=new Dictionary<string,int>();
    public static bool WasDrawn(string key)=>drawn.ContainsKey(key)&&drawn[key]>=Time.frameCount-1;
    static Vector2 origin,unit;
    public static void Begin(){if(!Enabled)return;if(Event.current.type==EventType.Repaint){origin=GUIUtility.GUIToScreenPoint(Vector2.zero);unit=GUIUtility.GUIToScreenPoint(Vector2.one)-origin;}}
    public static void Record(string key,Rect rect){if(!Enabled||Event.current.type!=EventType.Repaint)return;var a=GUIUtility.GUIToScreenPoint(rect.min)-origin;var b=GUIUtility.GUIToScreenPoint(rect.max)-origin;Rects[key]=Rect.MinMaxRect(a.x/unit.x,a.y/unit.y,b.x/unit.x,b.y/unit.y);drawn[key]=Time.frameCount;}
    public static void ApplyPointer(){if(Enabled&&UIDrawRecorder.InjectingClick)Event.current.mousePosition=GUIUtility.ScreenToGUIPoint(origin+Vector2.Scale(unit,CampusOverlayPainter.InputPoint));}
    public static bool Button(string key,Rect rect,string text,GUIStyle style){Record(key,rect);ApplyPointer();return GUI.Button(rect,text,style);}
}
