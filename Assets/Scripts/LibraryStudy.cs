using UnityEngine;

// Seated study at a reserved library seat.
//
// Sitting down reuses the cafeteria's procedural pose (CafeteriaSitPose) since the rig
// has no Sit clip, and freezes movement through CampusGame.Seated exactly like eating.
// Studying is a separate step: only pressing 开始学习 speeds the shared GameClock up.
public class LibraryStudy : MonoBehaviour
{
    public static LibraryStudy Instance;

    [Header("Reach")]
    public float sitReach = 2.2f;

    // height the hips drop so the body lands on the chair seat
    public const float HipDrop = 0.38f;

    CampusGame game;
    LibrarySeat seat;
    Vector3 standPos;
    Quaternion standRot;
    CafeteriaSitPose pose;

    public bool Seated { get; private set; }
    public bool Studying { get; private set; }
    public LibrarySeat Seat => seat;

    double studyStartGameSeconds;
    public double StudyGameSeconds =>
        Studying && GameClock.Instance ? GameClock.Instance.GameSeconds - studyStartGameSeconds : 0;

    public static LibraryStudy Ensure()
    {
        if (Instance) return Instance;

        // Prefer the copy the scene already carries; a second instance meant the scene
        // one drew nothing while the code talked to an invisible duplicate.
        var sceneOne = UnityEngine.Object.FindFirstObjectByType<LibraryStudy>(FindObjectsInactive.Include);
        if (sceneOne) { Instance = sceneOne; return Instance; }

        var go = new GameObject("LibraryStudy");
        if (Application.isPlaying) DontDestroyOnLoad(go);
        Instance = go.AddComponent<LibraryStudy>();
        return Instance;
    }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;
        game = FindFirstObjectByType<CampusGame>();
    }

    void OnDisable() { ForceUp(); }

    public LibrarySeat SeatNearPlayer()
    {
        if (!game || !game.player) return null;
        var reg = LibrarySeatRegistry.Instance;
        if (!reg) return null;
        return reg.NearestTo(game.player.position, sitReach);
    }

    // Sit on a seat. Returns false when there is no valid seat or the player already sits.
    public bool TrySit(LibrarySeat s)
    {
        if (Seated || s == null) return false;
        if (!game) game = FindFirstObjectByType<CampusGame>();
        if (!game || !game.player) return false;

        seat = s;
        standPos = game.player.position;
        standRot = game.player.rotation;

        var cc = game.player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        game.Seated = true;

        game.player.position = new Vector3(s.sitPos.x, s.sitPos.y - HipDrop, s.sitPos.z);
        game.player.rotation = s.facing;

        pose = game.player.GetComponent<CafeteriaSitPose>();
        if (!pose) pose = game.player.gameObject.AddComponent<CafeteriaSitPose>();
        pose.active = true;

        // Keep the camera where it is. Re-aiming it at the seat facing pushed the view
        // into the reading desk; the rig already faces the table, which is what matters.
        Physics.SyncTransforms();

        Seated = true;
        game.RefreshCursorState();
        return true;
    }

    public void StartStudy()
    {
        if (!Seated || Studying) return;          // repeated presses never stack the rate
        Studying = true;
        studyStartGameSeconds = GameClock.Instance ? GameClock.Instance.GameSeconds : 0;
        if (GameClock.Instance) GameClock.Instance.SetStudyMode(true);
        var rm = LibraryReservationManager.Instance;
        if (rm) rm.SetInUse(true);
    }

    public void StopStudy()
    {
        if (!Studying) return;
        Studying = false;
        if (GameClock.Instance) GameClock.Instance.RestoreNormal();
        var rm = LibraryReservationManager.Instance;
        if (rm) rm.SetInUse(false);
    }

    // Stand up, keeping the reservation so the player can sit again.
    public bool TryStandUp()
    {
        if (!Seated) return false;
        StopStudy();

        if (pose) pose.active = false;
        if (game && game.player)
        {
            var cc = game.player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;
            game.player.position = standPos;
            game.player.rotation = standRot;
            if (cc) cc.enabled = true;
        }
        if (game) { game.Seated = false; game.RefreshCursorState(); }
        Physics.SyncTransforms();

        Seated = false;
        seat = null;
        return true;
    }

    // Leaving the seat and dropping the reservation (完成本次使用).
    public void FinishAndRelease()
    {
        TryStandUp();
        var rm = LibraryReservationManager.Instance;
        if (rm) rm.Cancel();
    }

    // Safety net for scene unload / component disable: never leave the player frozen or
    // the clock sped up.
    public void ForceUp()
    {
        if (Studying)
        {
            Studying = false;
            if (GameClock.Instance) GameClock.Instance.RestoreNormal();
            var rm = LibraryReservationManager.Instance;
            if (rm) rm.SetInUse(false);
        }
        if (Seated)
        {
            if (pose) pose.active = false;
            if (game && game.player)
            {
                var cc = game.player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                game.player.position = standPos;
                game.player.rotation = standRot;
                if (cc) cc.enabled = true;
            }
            if (game) { game.Seated = false; game.RefreshCursorState(); }
            Seated = false;
            seat = null;
        }
    }
}
