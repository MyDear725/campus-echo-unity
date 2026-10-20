using UnityEngine;
using UnityEngine.SceneManagement;

// Starts shared services; scene-specific seat and machine data bind after loading.
public class CampusSystemsHost : MonoBehaviour
{
    void OnEnable() { SceneManager.sceneLoaded += SceneLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= SceneLoaded; }
    void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Interiors may be entered directly (previews, shortcuts) as well as through a
        // door, so the shared systems are re-created and the scene's systems rediscovered
        // after every load. All of them are idempotent.
        EnsureSharedSystems();
        CampusSystemsRegistry.Scan();
        StartCoroutine(RegisterSeatsSoon());
    }

    void EnsureSharedSystems()
    {
        GameClock.Ensure();
        CampusSemester.Ensure();
        LibrarySeatRegistry.Ensure();
        LibraryReservationManager.Ensure();
        CampusOverlayPainter.Ensure();
        CampusHud.Ensure();
        PhoneUI.Ensure();
        VendingMachineUI.Ensure();
        LibrarySeatInteraction.Ensure();
    }
    void Awake() { EnsureSharedSystems(); }

    // Registration is deferred: when the host awakes, an additively loaded interior may
    // not have instantiated its seat markers yet, which produced "parts 0".
    void Start() { StartCoroutine(RegisterSeatsSoon()); }

    System.Collections.IEnumerator RegisterSeatsSoon()
    {
        yield return null;              // let the scene finish activating
        yield return null;

        var reg = LibrarySeatRegistry.Instance;
        if (!reg) yield break;

        if (reg.All.Count == 0)
        {
            var parts = FindObjectsByType<LibrarySeatPart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var pt in parts)
                if (pt && !string.IsNullOrEmpty(pt.seatId)) reg.Add(pt.ToSeat());
            reg.ComputeMapPositions();
            Debug.Log($"[CampusSystemsHost] seats registered: {reg.All.Count} (parts {parts.Length})");
        }

        if (!LibraryStudy.Instance) LibraryStudy.Ensure();
        if (!PhoneUI.Instance) PhoneUI.Ensure();
        if (!VendingMachineUI.Instance) VendingMachineUI.Ensure();
        if (!LibrarySeatInteraction.Instance) LibrarySeatInteraction.Ensure();
        CampusSystemsRegistry.Scan();

        // The persistent UI is created before the additive library scene. Bind its
        // scene-owned anchors even when an old serialized UI component is discarded.
        var vending = VendingMachineUI.Ensure();
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name == "VendingAnchor_Drinks") vending.drinksMachine = t;
            if (t.name == "VendingAnchor_Snacks") vending.snacksMachine = t;
        }
        var ordering = CampusSystemsRegistry.FindOrdering();
        var game = FindFirstObjectByType<CampusGame>();
        if (ordering && game) ordering.SetGame(game);
    }
}
