using System;
using UnityEngine;

// Single in-game clock shared by the phone and the study UI.
//
// The project had no time system: the cafeteria only used wall-clock DateTime for its
// countdowns. This clock advances on real time multiplied by an adjustable rate, so
// studying can speed up the game day without touching Time.timeScale (which would also
// speed up animation, physics and every UI timer).
public class GameClock : MonoBehaviour
{
    public static GameClock Instance;

    [Header("Starting point")]
    [Tooltip("Game time when a fresh profile starts.")]
    public int startHour = 8;
    public int startMinute = 0;

    [Header("Rate")]
    [Tooltip("Normal game-seconds per real second.")]
    public float normalRate = 60f;      // 1 real minute = 1 game hour
    [Tooltip("Multiplier applied while studying.")]
    public float studyMultiplier = 5f;

    // Absolute game time, in seconds since the profile began.
    double gameSeconds;
    bool loaded;

    public float CurrentMultiplier { get; private set; } = 1f;
    public bool Accelerated => CurrentMultiplier > 1.001f;

    public static GameClock Ensure()
    {
        if (Instance) return Instance;
        var go = new GameObject("GameClock");
        if (Application.isPlaying) DontDestroyOnLoad(go);
        Instance = go.AddComponent<GameClock>();
        return Instance;
    }

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        Load();
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        // Pausing the game freezes the clock as well.
        var game = FindFirstObjectByType<CampusGame>();
        if (game && (game.paused || game.UiBlocking && false)) return;

        gameSeconds += Time.unscaledDeltaTime * normalRate * CurrentMultiplier;
    }

    // ---- rate control -------------------------------------------------------
    // Applies the study multiplier once; repeated calls never stack.
    public void SetStudyMode(bool on)
    {
        CurrentMultiplier = on ? Mathf.Max(1f, studyMultiplier) : 1f;
    }

    public void RestoreNormal() { CurrentMultiplier = 1f; }

    // ---- display ------------------------------------------------------------
    public DateTime Now
    {
        get
        {
            var start = DateTime.Today.AddHours(startHour).AddMinutes(startMinute);
            return start.AddSeconds(gameSeconds);
        }
    }

    public string DateText => Now.ToString("yyyy年M月d日");
    public string TimeText => Now.ToString("HH:mm");
    public string DateTimeText => DateText + " " + TimeText;

    // Whole game-seconds elapsed, used to measure a study session accurately even when
    // the rate changes mid-session.
    public double GameSeconds => gameSeconds;

    public int EstimatedSaveKeyDay => Now.DayOfYear;

    // ---- persistence --------------------------------------------------------
    public static string KeyOverride;
    static string Key => KeyOverride ?? "CampusGameClockSeconds";

    void Load()
    {
        if (loaded) return;
        loaded = true;
        if (PlayerPrefs.HasKey(Key))
        {
            double v;
            if (double.TryParse(PlayerPrefs.GetString(Key), out v)) gameSeconds = Math.Max(0, v);
        }
    }

    public void Save()
    {
        PlayerPrefs.SetString(Key, gameSeconds.ToString("F1"));
        PlayerPrefs.Save();
    }

    void OnApplicationQuit() { Save(); }
}
