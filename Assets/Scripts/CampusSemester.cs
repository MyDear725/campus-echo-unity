using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Owns all semester transactions. World actions and the phone use the same state and validation.
public sealed class CampusSemester : MonoBehaviour
{
    public static CampusSemester Instance;
    public const string PrefsKey = "CampusSemesterV1";
    public static string KeyOverride;
    public static string SaveKey => KeyOverride ?? PrefsKey;
    public SemesterState State { get; private set; }
    public SemesterStoryCatalog Catalog { get; private set; }
    public CampusGame Game { get; private set; }
    public GameClock Clock => GameClock.Ensure();
    public CampusSemesterUI UI { get; private set; }
    public CampusRouteGuide Route { get; private set; }
    public CampusDayNight Lighting { get; private set; }
    public bool Restoring { get; internal set; }
    public string LastMessage { get; private set; } = "";
    public bool Ready => Game && Game.Story && Game.Story.Finished;
    public string Location => CampusSemesterPoint.Nearby(Game)?.location ?? "";
    float saveTimer;
    bool initialized;
    int preparedScene=-1;
    public bool ClassroomSeated { get; private set; }
    public Vector3 ClassroomStandPosition { get; private set; }
    public float ClassroomStandYaw { get; private set; }
    public static CampusSemester Ensure()
    {
        if (Instance) return Instance;
        var go = new GameObject("CampusSemester");
        DontDestroyOnLoad(go);
        return go.AddComponent<CampusSemester>();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { Instance = null; }
    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        var source = Resources.Load<TextAsset>("Semester/story");
        Catalog = JsonUtility.FromJson<SemesterStoryCatalog>(source.text);
        ReloadState();
        UI = gameObject.AddComponent<CampusSemesterUI>();
        UI.Initialize(this);
        Route=gameObject.AddComponent<CampusRouteGuide>();Route.Initialize(this);
        Lighting=gameObject.AddComponent<CampusDayNight>();Lighting.Initialize(this);
        Clock.DayChanged += OnDay;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    void OnDestroy()
    {
        if (GameClock.Instance) GameClock.Instance.DayChanged -= OnDay;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode) { StartCoroutine(BindSoon()); }
    IEnumerator BindSoon() { yield return null; Bind(); }
    void Start() { Bind(); }
    public void Bind()
    {
        Game = FindFirstObjectByType<CampusGame>();
        if (!Game) return;
        CampusSemesterPoint.Prepare(Game);
        if (!initialized)
        {
            initialized = true;
            CampusSemesterSave.Initialize(this);
        }
    }
    public void ReloadState()
    {
        try { State = JsonUtility.FromJson<SemesterState>(PlayerPrefs.GetString(SaveKey, "")); }
        catch (Exception) { State = null; }
        if (State == null || State.version != 1 || State.courses == null || State.courses.Count != 3) State = SemesterState.Fresh();
        State.flags ??= new List<string>(); State.attendance ??= new List<string>();
        State.assignments ??= new List<SemesterAssignment>(); State.journal ??= new List<SemesterJournalEntry>();
        State.remindersSeen ??= new List<string>();
        State.energy = Mathf.Clamp(State.energy, 0, 100);
    }
    void Update()
    {
        if (!Game) { Bind(); return; }
        if (Restoring) return;
        if(!Game.Interiors.Busy)
        {
            int scene=Game.Interiors.Inside&&Game.Interiors.Interior?Game.Interiors.Interior.gameObject.scene.handle:Game.gameObject.scene.handle;
            if(scene!=preparedScene){CampusSemesterPoint.Prepare(Game);preparedScene=scene;}
        }
        if (Input.GetKeyDown(KeyCode.K) && !Game.UiBlocking) UI.Open("today");
        if (Ready && !Game.paused && !Game.UiBlocking)
        {
            foreach (var lesson in SemesterRules.Lessons(Clock.Now))
            {
                double left = (lesson.start - Clock.Now).TotalMinutes;
                if (left <= 30 && left >= 0) Remind(lesson.id + (left <= 5 ? "-5" : "-30"), lesson.start.ToString("HH:mm") + " " + lesson.title + " · " + lesson.room + "，按 K 查看日程。");
            }
            foreach (var task in State.assignments.Where(t => !t.submitted && t.dueDay - Clock.DayIndex <= 1))
                Remind(task.id + "-due", State.Course(task.course).name + "作业即将截止；可到图书馆学习后提交。");
        }
        saveTimer += Time.unscaledDeltaTime;
        var dorm = DormitoryCorridor.Current(Game);
        if (saveTimer > 15 && !Game.Interiors.Busy && !(dorm && dorm.Transitioning))
        {
            saveTimer = 0;
            Save();
        }
    }
    void Remind(string id, string message)
    {
        if (!State.reminders || State.remindersSeen.Contains(id)) return;
        State.remindersSeen.Add(id); Tell(message);
    }
    public string NextSchedule
    {
        get
        {
            if (!Ready) return "迎新不计时 · 跟随小岚熟悉校园";
            var lesson = SemesterRules.Lessons(Clock.Now).FirstOrDefault(l => l.end > Clock.Now && !State.attendance.Contains(l.id));
            if (lesson != null) return lesson.start.ToString("HH:mm") + " " + lesson.title + " · " + lesson.room;
            var beat = Catalog.beats.FirstOrDefault(b => SemesterRules.Available(State, b, Clock.DayIndex));
            return beat != null ? beat.title + " · " + beat.place : "今日课余时间 · 学习、活动或回宿舍休息";
        }
    }
    public string Objective
    {
        get
        {
            if (!Ready) return "先完成迎新，再在宿舍休息至9月2日。";
            if (!State.Has("class-first")) return "查看课表，提前前往教室，上第一节课。";
            if (!State.Has("homework-first")) return "预约图书馆座位，选课学习，完成并提交第一份作业。";
            return NextSchedule;
        }
    }
    public bool Tell(string message) { LastMessage = message; if (Game) Game.Notify(message); return false; }
    bool RequireReady() => Ready || Tell("先完成小岚的迎新剧情。当前迎新没有时间限制。");
    public bool At(string location) => Location == location;
    bool RequirePlace(string location)
    {
        if (At(location)) return true;
        return Tell("请先前往" + CampusSemesterPoint.PlaceName(location) + "的交互点，按 E 开始。");
    }
    public string Conflict(int minutes, string exceptLesson = null)
    {
        var end = Clock.Now.AddMinutes(minutes);
        if (end.Date != Clock.Now.Date) return "这项安排将进入下一天，请先回宿舍休息。";
        var lesson = SemesterRules.Lessons(Clock.Now).FirstOrDefault(l => l.id != exceptLesson && !State.attendance.Contains(l.id) && l.start < end && l.end > Clock.Now);
        return lesson == null ? "" : "会占用 " + lesson.start.ToString("HH:mm") + " " + lesson.title + "（" + lesson.room + "）的时间。";
    }
    bool CanSpend(int minutes, int energy, bool allowConflict, string exceptLesson = null)
    {
        if (Clock.Now.AddMinutes(minutes) > GameClock.SemesterEnd) return Tell("本学期已经结束。可以查看回顾或在ESC重新开始本学期。");
        if (Clock.Now.AddMinutes(minutes).Date != Clock.Now.Date) return Tell("这项安排会进入下一天，请先回宿舍休息。");
        if (State.energy < energy) return Tell("精力不足，请先吃饭或回宿舍休息。");
        string conflict = Conflict(minutes, exceptLesson);
        if (!string.IsNullOrEmpty(conflict) && !allowConflict) return Tell(conflict);
        return true;
    }
    void Commit(string title, string message, double minutes, int energy)
    {
        State.energy = Mathf.Clamp(State.energy - energy, 0, 100);
        State.Note(Clock.Now, title, message);
        Clock.AdvanceMinutes(minutes);
        Tell(message);
        Save();
    }
    public string ClassroomQuestion(string course)
    {
        if(course=="math")return "老师：观察一个变化过程时，先写清条件，再解释变化的趋势。把你的理解说出来，也可以记下不懂的那一步。";
        if(course=="code")return "老师：循环没有按预期结束时，先检查输入与循环条件。用一个小例子验证，再解释你的排查顺序。";
        return "老师：第一次向同学介绍校园，先说明目的地，再给出清楚的路线。试着用自己的表达组织这段介绍。";
    }
    public bool BeginClassroom(string courseId)
    {
        if(ClassroomSeated||!RequireReady())return false;
        var course=State.Course(courseId);if(course==null)return false;
        if(!RequirePlace(courseId=="math"?"a":courseId=="code"?"c":"b"))return false;
        var lesson=SemesterRules.Lessons(Clock.Now).FirstOrDefault(l=>l.course==courseId&&Clock.Now>=l.start.AddMinutes(-30)&&Clock.Now<l.end&&!State.attendance.Contains(l.id));
        if(lesson==null)return Tell("现在没有可参加的这门课程，请先查看课表。");
        ClassroomStandPosition=Game.player.position;ClassroomStandYaw=Game.player.eulerAngles.y;
        var cc=Game.Interiors.IndoorController;if(cc)cc.enabled=false;
        var root=Game.Interiors.Interior.transform;
        // Existing first-row chair coordinates from the A/B/C interior builders.
        var seat=courseId=="math"?new Vector3(-17,.18f,7.75f):new Vector3(7.2f,0,4.85f);
        Game.SetTravelPose(root.TransformPoint(seat+Vector3.down*LibraryStudy.HipDrop),180,10);
        Game.Seated=true;
        var pose=Game.player.GetComponent<CafeteriaSitPose>();if(!pose)pose=Game.player.gameObject.AddComponent<CafeteriaSitPose>();pose.active=true;
        ClassroomSeated=true;return true;
    }
    public void EndClassroom()
    {
        if(!ClassroomSeated)return;
        ClassroomSeated=false;
        if(!Game)return;
        var pose=Game.player.GetComponent<CafeteriaSitPose>();if(pose)pose.active=false;
        Game.SetTravelPose(ClassroomStandPosition,ClassroomStandYaw,12);Game.Seated=false;
        Physics.SyncTransforms();if(Game.Interiors.IndoorController)Game.Interiors.IndoorController.enabled=true;
    }
    public bool Attend(string courseId, bool participate, bool allowConflict = false)
    {
        if (!RequireReady()) return false;
        var course = State.Course(courseId);
        if (course == null) return Tell("课程不存在。");
        if (!RequirePlace(courseId == "math" ? "a" : courseId == "code" ? "c" : "b")) return false;
        var lesson = SemesterRules.Lessons(Clock.Now).FirstOrDefault(l => l.course == courseId && Clock.Now >= l.start.AddMinutes(-30) && Clock.Now < l.end);
        if (lesson == null) return Tell("现在没有这门课。请查看今日课表；可以提前30分钟到场。");
        if (State.attendance.Contains(lesson.id)) return Tell("这节课已经完成，可以安排下一件事。");
        int minutes = (int)Math.Ceiling((lesson.end - Clock.Now).TotalMinutes);
        if (!CanSpend(minutes, 12, allowConflict, lesson.id)) return false;
        bool late = Clock.Now > lesson.start.AddMinutes(10);
        State.attendance.Add(lesson.id); course.attended++;
        course.mastery = Mathf.Min(100, course.mastery + (participate ? 6 : 4));
        if (!State.Has("class-first")) { State.Mark("class-first"); CampusInventory.Add("课堂笔记"); }
        State.assignments.Add(new SemesterAssignment { id=lesson.id, course=course.id, dueDay=Clock.DayIndex+3 });
        Commit("完成" + course.name, (late ? "已记录迟到；下次可以提前出发。" : "课堂已完成。") + "老师：把今天不理解的步骤记下来，课后到图书馆练习。新作业三天后截止。", minutes, 12);
        return true;
    }
    public bool Study(string courseId, int minutes, bool allowConflict = false)
    {
        if (!RequireReady()) return false;
        if (minutes != 30 && minutes != 60 && minutes != 90) return Tell("请选择30、60或90分钟。");
        var study = LibraryStudy.Instance;
        if (!study || !study.Seated || !LibraryReservationManager.Ensure().HasReservation || !Game.Interiors.Inside || Game.Interiors.Interior.buildingId != "library")
            return Tell("先在手机预约图书馆座位，走到自己的座位按 E 坐下，再选科目学习。");
        var course = State.Course(courseId); if (course == null) return false;
        int cost = minutes / 6;
        if (!CanSpend(minutes, cost, allowConflict)) return false;
        study.StopStudy();
        float gain = SemesterRules.StudyGain(course, minutes);
        course.mastery = Mathf.Min(100, course.mastery + gain);
        var task = State.assignments.FirstOrDefault(t => t.course == courseId && !t.prepared && !t.submitted);
        if (task != null) task.prepared = true;
        State.Mark("study-" + courseId);
        Commit("针对学习 · " + course.name, course.topic + "：掌握度 +" + gain.ToString("F1") + (task != null ? "，一份作业已完成，可在手册提交。" : "，学习记录已保存。"), minutes, cost);
        return true;
    }
    public bool Submit(string assignmentId)
    {
        var task = State.assignments.FirstOrDefault(t => t.id == assignmentId);
        if (task == null || task.submitted) return Tell("这份作业已经提交或不存在。");
        if (!task.prepared) return Tell("先到图书馆完成对应科目的学习，再提交作业。");
        task.submitted = true; State.Course(task.course).homeworkSubmitted++;
        if (Clock.DayIndex > task.dueDay) State.Course(task.course).lateSubmitted++; State.Mark("homework-first");
        Commit("提交作业", Clock.DayIndex > task.dueDay ? "补交已记录。作业仍能帮助你进步，下次留意截止提醒。" : "作业已按时提交，平时表现已更新。", 0, 0);
        return true;
    }
    public bool CompleteBeat(string id, int choice, bool allowConflict = false)
    {
        if (!RequireReady()) return false;
        var beat = Catalog.beats.FirstOrDefault(b => b.id == id);
        if (beat == null || !SemesterRules.Available(State, beat, Clock.DayIndex)) return Tell("这段故事尚未开放或已经完成。请查看前置任务和日期。");
        if (choice < 0 || choice >= beat.choices.Length) return Tell("请选择对话选项。");
        if (!RequirePlace(beat.location) || !CanSpend(beat.minutes, beat.minutes / 10, allowConflict)) return false;
        // Mark before applying any reward, so revisiting an event never grants it twice.
        State.Mark(beat.id); State.Mark(beat.id + "-choice-" + choice);
        switch (beat.effect)
        {
            case "club": State.club = choice == 0 ? "编程兴趣小组" : "湖畔摄影小组"; break;
            case "role": State.projectRole = choice == 0 ? "资料与展示" : "实践与验证"; break;
            case "quiz": foreach (var c in State.courses) if (c.baseline < 0) c.baseline = SemesterRules.ExamScore(c, State.energy); break;
            case "tutor": State.Course("math").mastery = Mathf.Min(100, State.Course("math").mastery + 8); break;
            case "bug": State.Course("code").mastery = Mathf.Min(100, State.Course("code").mastery + 5); break;
            case "practice": State.practice++; CampusInventory.Add("小组作业成果"); break;
            case "photo": CampusInventory.Add("湖畔校园照片"); break;
            case "run": State.fitness += 5; break;
            case "job": State.contribution++; CampusWallet.Credit(3000); break;
            case "volunteer": State.contribution += 2; break;
            case "lead": State.contribution++; break;
            case "exhibit-prep": CampusInventory.Add("秋日作品素材"); break;
            case "exhibit": State.practice++; CampusInventory.Add("秋日作品展纪念"); break;
            case "project-topic": State.projectStep = 1; break;
            case "project-draft": State.projectStep = 2; break;
            case "project-feedback": State.projectStep = 3; break;
            case "project-revise": State.projectStep = 4; break;
            case "project-present": State.projectStep = 5; State.practice += 2; State.Course("code").homeworkSubmitted += 2; CampusInventory.Add("期末项目作品"); break;
            case "award-apply": State.awardsApplied = true; break;
            case "ending": CampusInventory.Add("第一学期回忆册"); break;
        }
        Commit(beat.title, beat.replies[choice] + "\n" + beat.closing, beat.minutes, beat.minutes / 10);
        return true;
    }
    public bool Exam(bool final, bool allowConflict = false)
    {
        if (!RequireReady() || !RequirePlace("a")) return false;
        int day = final ? 133 : 70;
        string flag = final ? "finals" : "midterms";
        if (Clock.DayIndex < day) return Tell((final ? "期末" : "期中") + "考核从" + GameClock.SemesterStart.AddDays(day).ToString("M月d日") + "开放。");
        if (State.Has(flag)) return Tell("本次考核已经完成。结果在成长页。");
        if (!CanSpend(120, 20, allowConflict)) return false;
        var results = new List<string>();
        foreach (var c in State.courses)
        {
            int score = SemesterRules.ExamScore(c, State.energy);
            if (final) c.final = score; else c.midterm = score;
            results.Add(c.name + " " + score);
        }
        State.Mark(flag);
        Commit(final ? "期末考核" : "期中考核", string.Join(" / ", results) + "。按实际掌握与平时表现结算；可继续针对学习。", 120, 20);
        return true;
    }
    public string AwardSummary
    {
        get
        {
            if (!State.Has("finals")) return "期末考核后评定。学业优秀：三门总评≥85；学习进步：较首次小测提高≥15；实践成果：完成期末项目；校园贡献：贡献记录≥3。";
            var awards = new List<string>();
            if (State.courses.All(c => c.Grade >= 85)) awards.Add("学业优秀奖学金预评通过（下学期发放）");
            if (State.courses.Any(c => c.baseline >= 0 && c.Grade - c.baseline >= 15)) awards.Add("学习进步");
            if (State.projectStep >= 5) awards.Add("实践成果");
            if (State.contribution >= 3) awards.Add("校园贡献");
            return awards.Count == 0 ? "本次未达到评奖条件；学期经历与已取得的成绩仍会保留。" : string.Join("、", awards);
        }
    }
    public bool ClaimAward()
    {
        if (!State.awardsApplied || !State.Has("finals")) return Tell("先完成学期成果申请与期末考核。");
        if (State.awardClaimed) return Tell("本学期的结果已经领取。");
        State.awardClaimed = true;
        bool eligible = State.projectStep >= 5 || State.contribution >= 3 || State.courses.Any(c => c.baseline >= 0 && c.Grade - c.baseline >= 15) || State.courses.All(c => c.Grade >= 85);
        if (eligible) { CampusWallet.Credit(20000); CampusInventory.Add("学期奖励证书"); }
        Commit("学期奖励评定", AwardSummary + (eligible ? "。游戏内学期学习奖励200元已到账。" : "。可以在下一次学期体验里尝试新的安排。"), 0, 0);
        return true;
    }
    public void MealCompleted()
    {
        State.Mark("meal");
        string id = "meal-energy-" + Clock.DayIndex + "-" + (Clock.Now.Hour < 11 ? "breakfast" : Clock.Now.Hour < 16 ? "lunch" : "dinner");
        if (!State.Has(id)) { State.Mark(id); State.energy = Mathf.Min(100, State.energy + 15); }
        Save();
    }
    public bool WaitUntil(DateTime target, bool allowConflict = false)
    {
        int minutes = (int)Math.Ceiling((target - Clock.Now).TotalMinutes);
        if (!RequireReady() || minutes <= 0 || minutes > 24 * 60) return Tell("只能等候至今天稍后的时间。");
        if (!CanSpend(minutes, 0, allowConflict)) return false;
        Commit("等候", "已等候至 " + target.ToString("HH:mm"), minutes, 0);
        return true;
    }
    public bool Rest()
    {
        if (!RequireReady() || !RequirePlace("dorm")) return false;
        if (Clock.Now.Date >= GameClock.SemesterEnd.Date) return Tell("本学期已结束，先查看回顾吧。");
        if (LibraryStudy.Instance) LibraryStudy.Instance.ForceUp();
        // Sleep restores energy only after the date advance; midnight alone never restores it.
        Clock.AdvanceMinutes((Clock.Now.Date.AddDays(1).AddHours(7) - Clock.Now).TotalMinutes);
        State.energy = 100;
        State.Note(Clock.Now, "新的一天", "小岚：先看课表，也给自己留一点课余时间。");
        Save();
        CampusSemesterSave.CaptureDay(this, true);
        Tell("早上好。" + NextSchedule);
        return true;
    }
    public bool RoutineDay()
    {
        if (!RequireReady() || !RequirePlace("dorm")) return false;
        if (Clock.DayIndex < 7 || SemesterRules.IsImportantDay(Clock.DayIndex, Catalog)) return Tell("今天有重要内容，或仍在第一周，请亲自体验这一天。");
        if (Clock.Now.Hour >= 8) return Tell("平常日计划需在早晨8点前、从宿舍开始。可先休息至明早。");
        if (Catalog.beats.Any(b => b.day <= Clock.DayIndex && b.day > Clock.DayIndex-1 && SemesterRules.Available(State,b,Clock.DayIndex))) return Tell("今天有新的故事，请先查看手册。");
        if (State.assignments.Any(t=>!t.submitted&&t.dueDay<=Clock.DayIndex)) return Tell("今天有作业截止或待补交，请先处理学习任务再使用平常日计划。");
        if (!CampusWallet.TrySpend(2000)) return Tell("平常日餐费需要20元，余额不足。可亲自安排更便宜的餐食或参与勤工助学。");
        foreach (var lesson in SemesterRules.Lessons(Clock.Now))
        {
            if (State.attendance.Contains(lesson.id)) continue;
            State.attendance.Add(lesson.id); var c = State.Course(lesson.course); c.attended++; c.mastery = Mathf.Min(100,c.mastery+4);
            c.homeworkSubmitted++; State.Mark("class-first"); State.Mark("homework-first");
        }
        State.energy = Mathf.Max(20, State.energy - 35);
        Commit("按计划度过平常日", "已完成今日课程与课后练习，餐费20元已结算。未代做故事、考试或活动。", (Clock.Now.Date.AddHours(21) - Clock.Now).TotalMinutes, 0);
        return true;
    }
    void OnDay(DateTime date)
    {
        if (Restoring || !initialized) return;
        int day = Clock.DayIndex;
        if (day / 7 > State.lastAllowanceDay / 7)
        {
            CampusWallet.Credit((day / 7 - State.lastAllowanceDay / 7) * 30000L);
            State.lastAllowanceDay = day;
        }
        LibraryReservationManager.Ensure().Cancel();
        State.remindersSeen.Clear();
        CampusSemesterSave.CaptureDay(this, true);
    }
    public void Save()
    {
        if (Restoring) return;
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(State));
        Clock.Save();
        CampusSemesterSave.SaveResume(this);
    }
    void OnApplicationPause(bool pause) { if (pause && initialized && !Restoring) Save(); }
    void OnApplicationQuit() { if (initialized && !Restoring) Save(); }
}
