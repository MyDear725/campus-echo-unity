using UnityEngine;

// Keep this component in its own file so Unity imports a usable MonoScript.
public sealed class CampusOverlayPainter : MonoBehaviour
{
    static CampusOverlayPainter instance;

    // Events posted by automated runs. They must be delivered from inside OnGUI, so they
    // are queued here and handed to IMGUI on the next real event.
    static readonly System.Collections.Generic.Queue<Event> injected = new System.Collections.Generic.Queue<Event>();
    static readonly System.Collections.Generic.Queue<Vector2> injectedPositions = new System.Collections.Generic.Queue<Vector2>();

    static readonly System.Collections.Generic.Queue<EventType> injectedTypes=new System.Collections.Generic.Queue<EventType>();
    public static Vector2 InputPoint { get; private set; }
    public static EventType InputType { get; private set; }
    public static void Inject(Event e) { if(e!=null)InjectAt(e,e.mousePosition); }
    public static void InjectAt(Event e, Vector2 position) { if(e!=null)InjectAt(e,position,e.type); }
    public static void InjectAt(Event e,Vector2 position,EventType type){if(e!=null){injectedPositions.Enqueue(position);injectedTypes.Enqueue(type);injected.Enqueue(e);}}

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForNewSession() { instance = null; injected.Clear(); injectedPositions.Clear(); injectedTypes.Clear(); }

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
        GUI.matrix = Matrix4x4.identity;
        UIDrawRecorder.BeginPass();
        LibraryUiProbe.Begin();

        // Deliver one injected event per GUI pass so names (needed to make a click hit a
        // specific button rect) are not stolen by a neighbouring control drawn later.
        // The real event still flows through the painter, so nothing starves.
        if (injected.Count > 0 && Event.current.type != EventType.Layout)
        {
            var e = injected.Peek();
            var inputPosition = injectedPositions.Peek();
            InputPoint=inputPosition;InputType=injectedTypes.Peek();
            if (e.type == EventType.Repaint || e.type == EventType.Layout)
            {
                injected.Dequeue(); injectedPositions.Dequeue(); injectedTypes.Dequeue();
            }
            else
            {
                injected.Clear(); injectedPositions.Clear(); injectedTypes.Clear();
            }
            var saved = Event.current;
            Event.current = e;
            Event.current.type=InputType;
            Event.current.mousePosition = inputPosition;
            UIDrawRecorder.InjectingClick = true;
            try { CampusOverlayHub.Draw(); }
            finally { UIDrawRecorder.InjectingClick = false; Event.current = saved; }
            return;
        }

        CampusOverlayHub.Draw();
    }
}
