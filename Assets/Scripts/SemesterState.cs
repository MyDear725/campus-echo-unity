using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class SemesterCourse
{
    public string id, name, building, room, topic;
    public float mastery = 20;
    public int attended, homeworkSubmitted, lateSubmitted, midterm = -1, final = -1, baseline = -1;
    public float Coursework => Mathf.Clamp(attended * 7 + homeworkSubmitted * 12 - lateSubmitted * 4, 0, 100);
    public int Grade => Mathf.RoundToInt(Coursework * .3f + Math.Max(0, midterm) * .2f + Math.Max(0, final) * .5f);
}
[Serializable] public sealed class SemesterAssignment
{
    public string id, course;
    public int dueDay;
    public bool prepared, submitted;
}
[Serializable] public sealed class SemesterJournalEntry
{
    public string date, title, text;
}
[Serializable] public sealed class SemesterState
{
    public int version = 1, energy = 100, lastAllowanceDay, projectStep;
    public string club = "", projectRole = "", trackedLocation = "";
    public bool reminders = true, awardsApplied, awardClaimed;
    public int practice, contribution, fitness;
    public List<SemesterCourse> courses = new List<SemesterCourse>();
    public List<SemesterAssignment> assignments = new List<SemesterAssignment>();
    public List<string> flags = new List<string>(), attendance = new List<string>(), remindersSeen = new List<string>();
    public List<SemesterJournalEntry> journal = new List<SemesterJournalEntry>();
    public static SemesterState Fresh()
    {
        return new SemesterState { courses = new List<SemesterCourse> {
            new SemesterCourse { id="math", name="高等数学", building="teaching-a", room="A101", topic="极限与变化" },
            new SemesterCourse { id="code", name="程序设计", building="teaching-c", room="C103", topic="循环与调试" },
            new SemesterCourse { id="english", name="大学英语", building="teaching-b", room="B103", topic="听力与校园表达" }
        }};
    }
    public bool Has(string id) => flags.Contains(id);
    public void Mark(string id) { if (!Has(id)) flags.Add(id); }
    public SemesterCourse Course(string id) => courses.FirstOrDefault(c => c.id == id);
    public void Note(DateTime now, string title, string text)
    {
        journal.Add(new SemesterJournalEntry { date=now.ToString("M月d日 HH:mm"), title=title, text=text });
    }
}
public sealed class SemesterLesson
{
    public string id, course, room, building, title;
    public DateTime start, end;
}
[Serializable] public sealed class SemesterStoryCatalog { public SemesterStoryBeat[] beats; }
[Serializable] public sealed class SemesterStoryBeat
{
    public string id, chapter, title, speaker, place, location, requires, effect, text, closing;
    public int day, minutes;
    public string[] choices, replies;
}

// Deterministic timetable and progression rules, shared by UI, reminders and the classroom interaction.
public static class SemesterRules
{
    public static List<SemesterLesson> Lessons(DateTime date)
    {
        var result = new List<SemesterLesson>();
        int day = (date.Date - GameClock.SemesterStart.Date).Days;
        if (day < 1 || date.Date > new DateTime(2027, 1, 15)) return result;
        var weekday = date.DayOfWeek;
        if (weekday == DayOfWeek.Monday || weekday == DayOfWeek.Wednesday || weekday == DayOfWeek.Friday)
            AddLesson(result, date, "math", "高等数学", "teaching-a", "A101", 8, 30);
        if (weekday == DayOfWeek.Tuesday || weekday == DayOfWeek.Thursday)
            AddLesson(result, date, "code", "程序设计", "teaching-c", "C103", 13, 30);
        if (weekday == DayOfWeek.Tuesday || weekday == DayOfWeek.Friday)
            AddLesson(result, date, "english", "大学英语", "teaching-b", "B103", 10, 20);
        return result.OrderBy(l => l.start).ToList();
    }
    static void AddLesson(List<SemesterLesson> list, DateTime date, string course, string title, string building, string room, int hour, int minute)
    {
        var start = date.Date.AddHours(hour).AddMinutes(minute);
        list.Add(new SemesterLesson { id=date.ToString("yyyyMMdd")+"-"+course, course=course, title=title, room=room, building=building, start=start, end=start.AddMinutes(90) });
    }
    public static bool Available(SemesterState state, SemesterStoryBeat beat, int day)
    {
        return day >= beat.day && !state.Has(beat.id) && (string.IsNullOrEmpty(beat.requires) || beat.requires.Split(',').All(state.Has));
    }
    public static int ExamScore(SemesterCourse c, int energy) => Mathf.Clamp(Mathf.RoundToInt(c.mastery * .85f + c.Coursework * .15f - (energy < 20 ? 8 : 0)), 0, 100);
    public static float StudyGain(SemesterCourse c, int minutes) => Mathf.Max(.5f, (100 - c.mastery) / 100f * minutes / 6f);
    public static bool IsImportantDay(int day, SemesterStoryCatalog catalog) => day == 5 || day == 40 || day == 70 || day == 133 || day == 138 || catalog.beats.Any(b => b.day == day);
    public static string Chapter(int day)
    {
        if (day == 0) return "今天开始，慢慢熟悉这里";
        if (day < 13) return "明天开始，自己认路";
        if (day < 30) return "课表以外，也有想做的事";
        if (day < 61) return "找到自己的学习方法";
        if (day < 91) return "学苑桥两边，都是生活";
        if (day < 122) return "原来我也能做好一件事";
        return "这一学期，我有自己的答案";
    }
}
