using System;
using UnityEngine;

// The player's single library reservation. Stored once, read by both the phone and the
// in-world seat interaction.
[Serializable]
public class LibraryReservationState
{
    public string seatId;
    public int seatFloor;
    public string seatArea;
    public string reservedOnDate;   // game date the reservation was made
    public bool inUse;              // player is currently seated studying
}

public class LibraryReservationManager : MonoBehaviour
{
    public static LibraryReservationManager Instance;

    public const string PrefsKey = "LibraryReservation";

    // Tests point this at a throwaway key so the real reservation is untouched.
    public static string KeyOverride;
    static string Key => string.IsNullOrEmpty(KeyOverride) ? PrefsKey : KeyOverride;

    public LibraryReservationState State { get; private set; } = new LibraryReservationState();

    public static LibraryReservationManager Ensure()
    {
        if (Instance) return Instance;
        var go = new GameObject("LibraryReservation");
        if (Application.isPlaying) DontDestroyOnLoad(go);
        Instance = go.AddComponent<LibraryReservationManager>();
        return Instance;
    }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;
        Load();
    }

    public bool HasReservation => State != null && !string.IsNullOrEmpty(State.seatId);

    public string ReservedSeatId => HasReservation ? State.seatId : null;
    public int ReservedFloor => HasReservation ? State.seatFloor : 0;

    public bool IsReservedSeat(string seatId) =>
        HasReservation && State.seatId == seatId;

    // True when this seat cannot be taken: reserved by the player (they must use their own)
    // or already in use.
    public bool IsBlocked(string seatId, out string reason)
    {
        reason = null;
        if (IsReservedSeat(seatId)) return true;
        if (HasReservation && State.inUse) { reason = "正在使用其他座位"; return true; }
        if (IsOccupiedByOthers(seatId)) { reason = "该座位已被占用"; return true; }
        return false;
    }

    // Placeholder for NPC occupation; none in this build, kept as the single hook.
    bool IsOccupiedByOthers(string seatId) => false;

    public bool TryReserve(string seatId, int floor, string area)
    {
        if (string.IsNullOrEmpty(seatId)) return false;

        // One live reservation at a time.
        if (HasReservation && State.seatId != seatId) return false;

        State.seatId = seatId;
        State.seatFloor = floor;
        State.seatArea = area;
        State.inUse = false;
        State.reservedOnDate = SafeDate();
        Save();
        return true;
    }

    // Replacing an existing reservation is only allowed after an explicit confirmation,
    // which the caller expresses by passing allowReplace.
    public bool TryReplace(string seatId, int floor, string area, bool allowReplace)
    {
        // Re-confirming the seat already held is a harmless no-op success.
        if (HasReservation && State.seatId == seatId) return true;
        if (HasReservation && !allowReplace) return false;
        if (HasReservation) { State.seatId = null; }
        return TryReserve(seatId, floor, area);
    }

    public void Cancel()
    {
        State.seatId = null;
        State.seatFloor = 0;
        State.seatArea = null;
        State.inUse = false;
        Save();
    }

    public void SetInUse(bool inUse)
    {
        if (!HasReservation) return;
        State.inUse = inUse;
        Save();
    }

    static string SafeDate()
    {
        var c = GameClock.Instance;
        return c ? c.DateText : DateTime.Now.ToString("yyyy年M月d日");
    }

    public void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(State));
        PlayerPrefs.Save();
    }

    public void Load()
    {
        if (!PlayerPrefs.HasKey(Key)) return;
        var json = PlayerPrefs.GetString(Key);
        var s = JsonUtility.FromJson<LibraryReservationState>(json);
        if (s != null) State = s;
    }

    // A reload never restores a seated state: the player always comes back standing.
    public void DropLiveStateOnLoad() { State.inUse = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Instance = null; }
}
