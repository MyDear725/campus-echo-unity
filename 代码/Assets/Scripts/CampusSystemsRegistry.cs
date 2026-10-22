using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Small scene-aware registry for systems that live inside an additive interior scene.
///
/// The cafeteria system is authored into cafeteria.unity, but the UI and the E-key router
/// live on the persistent campus side. The registry lets them find "the cafeteria of the
/// interior I am currently inside" without a direct reference, so normal building entry,
/// preview shortcuts and QA runs all behave the same.
/// </summary>
public static class CampusSystemsRegistry
{
    public static CafeteriaOrdering Ordering { get; private set; }

    public static event Action OrderingChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForNewSession()
    {
        Ordering = null;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ScanAfterLoad() { Scan(); }

    // Called by the component so late spawns are picked up even before a scan runs.
    public static void RegisterOrdering(CafeteriaOrdering o)
    {
        if (!o || Ordering == o) return;
        Ordering = o;
        Notify();
    }

    public static void UnregisterOrdering(CafeteriaOrdering o)
    {
        if (Ordering != o) return;
        Ordering = null;
        Notify();
    }

    static void Notify() { var h = OrderingChanged; if (h != null) h(); }

    static void OnSceneUnloaded(Scene s)
    {
        if (Ordering && Ordering.gameObject.scene == s) { Ordering = null; Notify(); }
    }

    // Finds the cafeteria system in whichever loaded scene currently carries one. The
    // active scene is checked first so a preview scene wins over a dormant campus copy.
    public static CafeteriaOrdering FindOrdering()
    {
        if (Ordering) return Ordering;

        var active = SceneManager.GetActiveScene();
        if (active.IsValid() && active.isLoaded)
        {
            foreach (var root in active.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<CafeteriaOrdering>(true);
                if (c) { Ordering = c; Notify(); return c; }
            }
        }

        var all = UnityEngine.Object.FindObjectsByType<CafeteriaOrdering>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
        {
            if (c && c.gameObject.scene.isLoaded) { Ordering = c; Notify(); return c; }
        }
        return null;
    }

    public static void Scan() { FindOrdering(); }
}
