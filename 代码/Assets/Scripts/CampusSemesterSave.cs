using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable] public sealed class SemesterPreference
{
    public string key, value;
    public bool exists, integer;
}
[Serializable] public sealed class SemesterSnapshot
{
    public int version = 1, day, floor = 1, roomIndex = -1;
    public double seconds;
    public string entrance, spawn;
    public Vector3 position;
    public float yaw;
    public List<SemesterPreference> preferences = new List<SemesterPreference>();
}
[Serializable] public sealed class SemesterBackup
{
    public SemesterSnapshot snapshot;
    public string daySnapshot;
}

// A snapshot restores balances AND the flags that earned them. Only owned gameplay keys
// are touched: never PlayerPrefs.DeleteAll, and never character/graphics/audio preferences.
public static class CampusSemesterSave
{
    public static string DayKey => CampusSemester.SaveKey + ".day";
    public static string ResumeKey => CampusSemester.SaveKey + ".resume";
    public static string BackupKey => CampusSemester.SaveKey + ".backup";
    static string InitialKey => CampusSemester.SaveKey + ".initial";
    public static bool HasBackup => PlayerPrefs.HasKey(BackupKey);
    public static string Error { get; private set; }
    static IEnumerable<Tuple<string, bool>> Keys(CampusSemester s)
    {
        yield return Tuple.Create(CampusSemester.SaveKey, false);
        yield return Tuple.Create(GameClock.KeyOverride ?? GameClock.PrefsKey, false);
        yield return Tuple.Create(CampusWallet.KeyOverride ?? CampusWallet.Key, false);
        yield return Tuple.Create(CampusInventory.KeyOverride ?? CampusInventory.Key, false);
        yield return Tuple.Create(CafeteriaOrdering.PrefsKeyOverride ?? CafeteriaOrdering.PrefsKey, false);
        yield return Tuple.Create(LibraryReservationManager.KeyOverride ?? LibraryReservationManager.PrefsKey, false);
        yield return Tuple.Create((VendingSystem.KeyOverridePrefix ?? VendingSystem.StockKeyPrefix) + "vending_drinks_1f", false);
        yield return Tuple.Create((VendingSystem.KeyOverridePrefix ?? VendingSystem.StockKeyPrefix) + "vending_snacks_1f", false);
        string welcome = CampusWelcomeStory.ProgressKey;
        yield return Tuple.Create(welcome, true);
        yield return Tuple.Create(welcome + "Pending", true);
        if (s.Game && s.Game.Story)
            foreach (var beat in s.Game.Story.Data.stages) yield return Tuple.Create(welcome + "Choice_" + beat.id, true);
    }
    public static void Initialize(CampusSemester s)
    {
        if (!PlayerPrefs.HasKey(InitialKey)) PlayerPrefs.SetString(InitialKey, JsonUtility.ToJson(FreshSnapshot(s)));
        if (!PlayerPrefs.HasKey(DayKey)) CaptureDay(s, false);
        var resume = Read(PlayerPrefs.GetString(ResumeKey, ""));
        if (resume != null && !s.Restoring) s.StartCoroutine(RestorePose(s, resume));
        else SaveResume(s);
    }
    public static SemesterSnapshot Capture(CampusSemester s)
    {
        PlayerPrefs.SetString(CampusSemester.SaveKey, JsonUtility.ToJson(s.State));
        s.Clock.Save();
        CampusWallet.Save(); CampusInventory.Save();
        var order = CampusSystemsRegistry.FindOrdering(); if (order) order.Save();
        var snapshot = new SemesterSnapshot { day=s.Clock.DayIndex, seconds=s.Clock.GameSeconds };
        if (s.Game && s.Game.player)
        {
            snapshot.position = s.Game.player.position; snapshot.yaw = s.Game.player.eulerAngles.y;
            if(s.ClassroomSeated){snapshot.position=s.ClassroomStandPosition;snapshot.yaw=s.ClassroomStandYaw;}
            else if(LibraryStudy.Instance&&LibraryStudy.Instance.Seated){snapshot.position=LibraryStudy.Instance.StandingPosition;snapshot.yaw=LibraryStudy.Instance.StandingRotation.eulerAngles.y;}
            else if(order&&order.Seated){snapshot.position=order.LastStandPosition;snapshot.yaw=order.LastStandRotation.eulerAngles.y;}
            var travel = s.Game.Interiors;
            if (travel && travel.Inside && travel.Current)
            {
                snapshot.entrance = travel.Current.EntranceId; snapshot.spawn = travel.CurrentSpawnId;
                var dorm = DormitoryCorridor.Current(s.Game);
                if (dorm) { snapshot.floor=dorm.Floor; snapshot.roomIndex=dorm.InsideRoom ? dorm.CurrentRoomIndex : -1; }
            }
        }
        foreach (var key in Keys(s))
            snapshot.preferences.Add(new SemesterPreference { key=key.Item1, integer=key.Item2, exists=PlayerPrefs.HasKey(key.Item1), value=key.Item2 ? PlayerPrefs.GetInt(key.Item1).ToString(CultureInfo.InvariantCulture) : PlayerPrefs.GetString(key.Item1, "") });
        return snapshot;
    }
    static SemesterSnapshot FreshSnapshot(CampusSemester s)
    {
        var fresh = new SemesterSnapshot { position=s.Game.MapToWorld(new Vector3(3,.23f,194)), yaw=180 };
        foreach (var key in Keys(s)) fresh.preferences.Add(new SemesterPreference { key=key.Item1, integer=key.Item2, exists=false, value="" });
        Set(fresh, CampusSemester.SaveKey, JsonUtility.ToJson(SemesterState.Fresh()));
        Set(fresh, GameClock.KeyOverride ?? GameClock.PrefsKey, "0");
        Set(fresh, CampusWallet.KeyOverride ?? CampusWallet.Key, CampusWallet.DefaultStartingFen.ToString(CultureInfo.InvariantCulture));
        Set(fresh, CampusInventory.KeyOverride ?? CampusInventory.Key, JsonUtility.ToJson(new InventoryState()));
        Set(fresh, CampusWelcomeStory.ProgressKey, "0"); Set(fresh, CampusWelcomeStory.ProgressKey+"Pending", "-1");
        return fresh;
    }
    static void Set(SemesterSnapshot snapshot, string key, string value)
    {
        var entry = snapshot.preferences.First(p => p.key == key); entry.exists=true; entry.value=value;
    }
    public static void CaptureDay(CampusSemester s, bool replace)
    {
        if (s.Restoring) return;
        var old = Read(PlayerPrefs.GetString(DayKey, ""));
        if (!replace && old != null && old.day == s.Clock.DayIndex) return;
        PlayerPrefs.SetString(DayKey, JsonUtility.ToJson(Capture(s)));
        PlayerPrefs.Save();
    }
    public static void SaveResume(CampusSemester s)
    {
        if (s.Restoring || !s.Game || !s.Game.player || (s.Game.Interiors && s.Game.Interiors.Busy)) return;
        PlayerPrefs.SetString(ResumeKey, JsonUtility.ToJson(Capture(s)));
        PlayerPrefs.Save();
    }
    public static SemesterSnapshot Read(string json)
    {
        try
        {
            var snapshot = JsonUtility.FromJson<SemesterSnapshot>(json);
            if (snapshot == null || snapshot.version != 1 || snapshot.preferences == null || snapshot.preferences.Count == 0 || double.IsNaN(snapshot.seconds) || double.IsInfinity(snapshot.seconds)) return null;
            return snapshot;
        }
        catch (Exception) { return null; }
    }
    public static bool RestoreDay(CampusSemester s) => Restore(s, PlayerPrefs.GetString(DayKey, ""), false);
    public static bool RestoreSemester(CampusSemester s) => Restore(s, PlayerPrefs.GetString(InitialKey, ""), true);
    static bool Restore(CampusSemester s, string json, bool semester)
    {
        var target = Read(json);
        if (target == null) { Error="存档无法读取，当前进度未改动。"; return s.Tell(Error); }
        if (s.Restoring || s.Game.Interiors.Busy) return s.Tell("正在切换场景，请稍后再试。");
        var backup = new SemesterBackup { snapshot=Capture(s), daySnapshot=PlayerPrefs.GetString(DayKey, "") };
        PlayerPrefs.SetString(BackupKey, JsonUtility.ToJson(backup));
        if (semester) PlayerPrefs.SetString(DayKey, json);
        return Apply(s, target);
    }
    public static bool RestoreBackup(CampusSemester s)
    {
        SemesterBackup backup;
        try { backup=JsonUtility.FromJson<SemesterBackup>(PlayerPrefs.GetString(BackupKey, "")); }
        catch (Exception) { return s.Tell("备份无法读取，当前进度未改动。"); }
        if (backup?.snapshot == null || Read(JsonUtility.ToJson(backup.snapshot)) == null) return s.Tell("没有可恢复的完整备份。");
        if (s.Restoring || s.Game.Interiors.Busy) return false;
        PlayerPrefs.SetString(DayKey, backup.daySnapshot);
        return Apply(s, backup.snapshot);
    }
    public static bool Apply(CampusSemester s, SemesterSnapshot target)
    {
        var known = Keys(s).ToDictionary(k => k.Item1, k => k.Item2);
        if (target.preferences.Count != known.Count || target.preferences.Select(p => p.key).Distinct().Count() != known.Count || target.preferences.Any(p => !known.ContainsKey(p.key) || known[p.key] != p.integer || (p.exists && p.integer && !int.TryParse(p.value,out _))))
            return s.Tell("存档结构不匹配，当前进度未改动。");
        s.Restoring = true;
        if (s.UI) s.UI.Close();
        if (PhoneUI.Instance && PhoneUI.Instance.IsOpen) PhoneUI.Instance.Close();
        if (LibraryStudy.Instance) LibraryStudy.Instance.ForceUp();
        // Write the entire owned-key set before reloading any consumer's cache.
        foreach (var entry in target.preferences)
        {
            if (!entry.exists) PlayerPrefs.DeleteKey(entry.key);
            else if (entry.integer) PlayerPrefs.SetInt(entry.key, int.Parse(entry.value, CultureInfo.InvariantCulture));
            else PlayerPrefs.SetString(entry.key, entry.value);
        }
        PlayerPrefs.SetString(ResumeKey, JsonUtility.ToJson(target)); PlayerPrefs.Save();
        CampusWallet.ResetCache(); CampusInventory.ResetCache();
        s.Clock.Restore(target.seconds); s.Clock.Save(); s.ReloadState();
        LibraryReservationManager.Ensure().Reload();
        // The additive interiors are unloaded with the old campus. This also discards live eating,
        // seating and menu locks, then restores their recorded gameplay state from the snapshot.
        s.StartCoroutine(ReloadCampus(s, target));
        return true;
    }
    static IEnumerator ReloadCampus(CampusSemester s, SemesterSnapshot target)
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/CampusLiving.unity", LoadSceneMode.Single);
        yield return null;
        s.Bind();
        yield return RestorePose(s, target);
        s.Restoring = false;
        s.Save();
        s.Tell("已恢复至 " + s.Clock.DateTimeText + "。余额、课程、任务与奖励记录已同步恢复。");
    }
    public static IEnumerator RestorePose(CampusSemester s, SemesterSnapshot target)
    {
        bool wasRestoring = s.Restoring; s.Restoring = true;
        yield return null;
        s.Bind();
        var game = s.Game;
        if (!game) { s.Restoring=wasRestoring; yield break; }
        bool originallyPaused=game.paused;
        game.paused=false; game.SetUiCursor(true);
        if (string.IsNullOrEmpty(target.entrance)) game.SetTravelPose(target.position, target.yaw, 12);
        else
        {
            var travel = game.Interiors;
            var entry = travel.entrances.FirstOrDefault(e => e && e.EntranceId == target.entrance);
            if (entry)
            {
                game.SetTravelPose(game.MapToWorld(entry.approachMapPosition), entry.enterYaw, 12);
                if (travel.TryEnter(entry))
                {
                    float deadline=Time.realtimeSinceStartup+50;
                    while (travel.Busy && Time.realtimeSinceStartup < deadline) yield return null;
                    if (travel.Inside)
                    {
                        var dorm = DormitoryCorridor.Current(game);
                        if (dorm)
                        {
                            dorm.RestoreFloor(target.floor);
                            if (target.roomIndex >= 0) yield return dorm.RestoreRoom(target.roomIndex);
                        }
                        var cc=travel.IndoorController; if(cc)cc.enabled=false;
                        game.SetTravelPose(target.position,target.yaw,12); Physics.SyncTransforms(); if(cc)cc.enabled=true;
                    }
                    else s.Tell("室内恢复失败，已保留全部进度并停在原建筑门外。");
                }
            }
            else s.Tell("原入口暂不可用，已保留进度并返回校园。");
        }
        game.paused=originallyPaused;
        game.SetUiCursor(false);
        s.Restoring = wasRestoring;
    }
}
