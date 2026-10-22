using System.Collections.Generic;
using UnityEngine;

// Registry of every seat that exists in the built library, plus the reservation state.
// Both the phone and the in-world interaction read from here, so they cannot disagree.
public class LibrarySeatRegistry : MonoBehaviour
{
    public static LibrarySeatRegistry Instance;

    public static LibrarySeatRegistry Ensure()
    {
        if (Instance) return Instance;

        // Reuse the registry the library scene already carries; creating a second one
        // left the scene's copy empty (seats=0) while the code talked to the new one.
        var existing = UnityEngine.Object.FindFirstObjectByType<LibrarySeatRegistry>();
        if (existing) { Instance = existing; return Instance; }

        var go = new GameObject("LibrarySeats");
        if (Application.isPlaying) DontDestroyOnLoad(go);
        Instance = go.AddComponent<LibrarySeatRegistry>();
        return Instance;
    }

    readonly List<LibrarySeat> seats = new List<LibrarySeat>();
    public IReadOnlyList<LibrarySeat> All => seats;

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void Clear() { seats.Clear(); }

    public void Add(LibrarySeat s)
    {
        if (s == null || string.IsNullOrEmpty(s.id)) return;
        for (int i = 0; i < seats.Count; i++)
            if (seats[i].id == s.id) { seats[i] = s; return; }
        seats.Add(s);
    }

    public LibrarySeat ById(string id)
    {
        for (int i = 0; i < seats.Count; i++)
            if (seats[i].id == id) return seats[i];
        return null;
    }

    public List<LibrarySeat> OnFloor(int floor)
    {
        var list = new List<LibrarySeat>();
        for (int i = 0; i < seats.Count; i++)
            if (seats[i].floor == floor) list.Add(seats[i]);
        return list;
    }

    public int CountOnFloor(int floor) => OnFloor(floor).Count;

    // Seat nearest the player on their own floor, within reach.
    public LibrarySeat NearestTo(Vector3 p, float maxDist)
    {
        LibrarySeat best = null;
        float bestSqr = maxDist * maxDist;
        for (int i = 0; i < seats.Count; i++)
        {
            var s = seats[i];
            if (Mathf.Abs(p.y - s.sitPos.y) >= 2.0f) continue;      // other floor
            float sqr = (new Vector2(p.x, p.z) - new Vector2(s.sitPos.x, s.sitPos.z)).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = s; }
        }
        return best;
    }

    // Normalises every seat of a floor into 0..1 map space for the phone plan view.
    public void ComputeMapPositions()
    {
        foreach (int f in new[] { 1, 2, 3, 4 })
        {
            var list = OnFloor(f);
            if (list.Count == 0) continue;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var s in list)
            {
                minX = Mathf.Min(minX, s.sitPos.x); maxX = Mathf.Max(maxX, s.sitPos.x);
                minZ = Mathf.Min(minZ, s.sitPos.z); maxZ = Mathf.Max(maxZ, s.sitPos.z);
            }
            float dx = Mathf.Max(0.001f, maxX - minX);
            float dz = Mathf.Max(0.001f, maxZ - minZ);
            float span = Mathf.Max(dx, dz);      // keep the plan square so it is not skewed

            foreach (var s in list)
                s.mapPos = new Vector2((s.sitPos.x - minX) / span, (s.sitPos.z - minZ) / span);
        }
    }
}
