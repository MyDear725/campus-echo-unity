using UnityEngine;

// Builds the registry from the markers when the library scene becomes active.
public class LibrarySeatBootstrap : MonoBehaviour
{
    public static int LastPartCount = -1;

    void Start() { StartCoroutine(RegisterSoon()); }

    System.Collections.IEnumerator RegisterSoon()
    {
        yield return null;
        yield return null;
        var reg = LibrarySeatRegistry.Ensure();
        reg.Clear();

        var parts = FindObjectsByType<LibrarySeatPart>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        LastPartCount = parts.Length;
        Debug.Log($"[LibrarySeatBootstrap] parts found={parts.Length} registry={(reg?"ok":"null")}");
        foreach (var p in parts)
        {
            if (!p || string.IsNullOrEmpty(p.seatId)) continue;
            reg.Add(p.ToSeat());
        }
        reg.ComputeMapPositions();

        // Make sure the managers exist for the phone and the interaction.
        LibraryReservationManager.Ensure();
        GameClock.Ensure();
    }

    void OnDestroy()
    {
        var reg = LibrarySeatRegistry.Instance;
        if (reg) reg.Clear();
    }
}
