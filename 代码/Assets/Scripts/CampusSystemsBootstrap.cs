using UnityEngine;

// Starts the shared systems once per play session. UI drawing has its own persistent host.
public class CampusSystemsBootstrap : MonoBehaviour
{
    // SubsystemRegistration runs once per play session, before any scene loads, and in
    // the editor it also runs for every new Play. Keeping the guard here (not in
    // AfterSceneLoad) means the flag is reset each time and the systems are never
    // skipped because of a stale static from a previous session.
    static bool started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForNewSession()
    {
        started = false;
        PhoneUI.Instance = null;
        PhoneUI.DrawCount = 0;
        PhoneUI.LastRect = "";
        VendingMachineUI.Instance = null;
        LibrarySeatInteraction.Instance = null;
        LibraryStudy.Instance = null;
        LibrarySeatRegistry.Instance = null;
        LibraryReservationManager.Instance = null;
        GameClock.Instance = null;
        CampusHud.Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (started) return;
        started = true;

        // A scene object already carrying the host (e.g. the library scene) is reused;
        // otherwise one is created so the systems exist in every scene.
        if (Object.FindFirstObjectByType<CampusSystemsHost>()) return;
        var host = new GameObject("CampusSystems");
        Object.DontDestroyOnLoad(host);
        host.AddComponent<CampusSystemsHost>();
    }
}
