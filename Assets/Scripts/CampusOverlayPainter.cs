using UnityEngine;

// Keep this component in its own file so Unity imports a usable MonoScript.
public sealed class CampusOverlayPainter : MonoBehaviour
{
    static CampusOverlayPainter instance;

    // Events posted by automated runs. They must be delivered from inside OnGUI, so they
    // are queued here and handed to IMGUI on the next real event.
    static readonly System.Collections.Generic.Queue<Event> injected = new System.Collections.Generic.Queue<Event>();

    public static void Inject(Event e) { if (e != null) injected.Enqueue(e); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForNewSession() { instance = null; injected.Clear(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { Ensure(); }

    public static void Ensure()
    {
        if (instance) return;
        instance = FindFirstObjectByType<CampusOverlayPainter>();
        if (instance) { instance.enabled = true; return; }
        var go = new GameObject("CampusOverlayPainter");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<CampusOverlayPainter>();
    }

    void Awake()
    {
        if (instance && instance != this) { enabled = false; Destroy(this); return; }
        instance = this;
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void OnGUI()
    {
        GUI.depth = -1000;
        UIDrawRecorder.BeginPass();

        // Deliver one injected event per GUI pass so names (needed to make a click hit a
        // specific button rect) are not stolen by a neighbouring control drawn later.
        // The real event still flows through the painter, so nothing starves.
        if (injected.Count > 0)
        {
            var e = injected.Peek();
            if (e.type == EventType.Repaint || e.type == EventType.Layout)
            {
                injected.Dequeue();
            }
            else
            {
                injected.Clear();
            }
            var saved = Event.current;
            Event.current = e;
            UIDrawRecorder.InjectingClick = true;
            try { CampusOverlayHub.Draw(); }
            finally { UIDrawRecorder.InjectingClick = false; Event.current = saved; }
            return;
        }

        CampusOverlayHub.Draw();
    }
}
