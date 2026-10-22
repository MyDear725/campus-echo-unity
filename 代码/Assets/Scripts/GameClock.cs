using System;
using System.Globalization;
using UnityEngine;

// The only calendar used by classes, reservations and the phone. Offline time never advances it.
public class GameClock : MonoBehaviour
{
    public static GameClock Instance;
    public static readonly DateTime SemesterStart = new DateTime(2026, 9, 1, 8, 0, 0);
    public static readonly DateTime SemesterEnd = new DateTime(2027, 1, 17, 22, 0, 0);
    public const string PrefsKey = "CampusGameClockV2";
    public static string KeyOverride;
    static string Key => KeyOverride ?? PrefsKey;
    public int startHour = 8, startMinute;
    public float normalRate = 5f, studyMultiplier = 5f;
    double gameSeconds;
    CampusGame game;
    public float CurrentMultiplier { get; private set; } = 1f;
    public bool Accelerated => CurrentMultiplier > 1.001f;
    public event Action<DateTime> DayChanged;
    public DateTime Now => SemesterStart.AddSeconds(gameSeconds);
    public int DayIndex => (Now.Date - SemesterStart.Date).Days;
    public int TeachingWeek => DayIndex / 7 + 1;
    public string DateText => Now.ToString("yyyy年M月d日", CultureInfo.InvariantCulture);
    public string TimeText => Now.ToString("HH:mm", CultureInfo.InvariantCulture);
    public string WeekText => "星期" + "日一二三四五六"[(int)Now.DayOfWeek] + " · 第" + TeachingWeek + "周";
    public string DateTimeText => DateText + " " + TimeText;
    public double GameSeconds => gameSeconds;
    public int EstimatedSaveKeyDay => DayIndex;
    public static GameClock Ensure()
    {
        if (Instance) return Instance;
        var go = new GameObject("GameClock");
        if (Application.isPlaying) DontDestroyOnLoad(go);
        return go.AddComponent<GameClock>();
    }
    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        // Serialized values from the old clock are deliberately superseded by the semester pace.
        normalRate = 5f;
        Reload();
    }
    void Update()
    {
        if (!Application.isPlaying) return;
        if (!game) game = FindFirstObjectByType<CampusGame>();
        if (!game || game.paused || game.UiBlocking || (game.Interiors && game.Interiors.Busy)) return;
        // Onboarding is untimed; the first morning starts after the player rests.
        if (game.Story && !game.Story.Finished) return;
        if (CampusSemester.Instance && CampusSemester.Instance.Restoring) return;
        AdvanceSeconds(Time.unscaledDeltaTime * normalRate * CurrentMultiplier);
    }
    public void AdvanceMinutes(double minutes) { AdvanceSeconds(Math.Max(0, minutes) * 60); }
    public void AdvanceSeconds(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0) return;
        double target = Math.Min(gameSeconds + seconds, (SemesterEnd - SemesterStart).TotalSeconds);
        // Commit each midnight separately so a long action cannot skip daily snapshots or allowances.
        while (Now.Date < SemesterStart.AddSeconds(target).Date)
        {
            gameSeconds = (Now.Date.AddDays(1) - SemesterStart).TotalSeconds;
            DayChanged?.Invoke(Now);
        }
        gameSeconds = target;
    }
    public void Restore(double seconds)
    {
        gameSeconds = double.IsNaN(seconds) || double.IsInfinity(seconds) ? 0 : Math.Max(0, Math.Min(seconds, (SemesterEnd - SemesterStart).TotalSeconds));
        RestoreNormal();
    }
    public void SetStudyMode(bool on) { CurrentMultiplier = on ? Mathf.Max(1f, studyMultiplier) : 1f; }
    public void RestoreNormal() { CurrentMultiplier = 1f; }
    public void Reload()
    {
        double value;
        Restore(double.TryParse(PlayerPrefs.GetString(Key, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : 0);
    }
    public void Save()
    {
        PlayerPrefs.SetString(Key, gameSeconds.ToString("R", CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }
    void OnApplicationQuit() { Save(); }
    void OnDestroy() { if (Instance == this) Instance = null; }
}
