using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class SemesterDailyCatalog { public SemesterDailyActivity[] activities; }
[Serializable] public sealed class SemesterDailyActivity
{
    public string id, category, title, speaker, place, location, text;
    public int openHour = 7, closeHour = 22;
    public SemesterDailyChoice[] choices;
}
[Serializable] public sealed class SemesterDailyChoice
{
    public string label, reply, course;
    public int minutes, energy, fitness;
    public float mastery;
}
[Serializable] public sealed class SemesterDailyRecord
{
    public int day, choice;
    public string id, category, title, reply;
}

public static class CampusDailyRules
{
    // Stable per date and category: reloading or reopening the handbook cannot reroll it.
    public static SemesterDailyActivity[] ForDay(SemesterDailyCatalog catalog, int day)
    {
        if(day<1)return Array.Empty<SemesterDailyActivity>();
        var result=new List<SemesterDailyActivity>();
        foreach(var category in new[]{"learn","company","rest"})
        {
            var pool=catalog.activities.Where(a=>a.category==category).ToArray();
            if(pool.Length>0)result.Add(pool[(day-1)%pool.Length]);
        }
        return result.ToArray();
    }
    public static string CategoryName(string id)=>id=="learn"?"学习小事":id=="company"?"同伴时光":"留点空闲";
    public static string MilestoneName(string id)=>id=="daily-learn-3"?"累计参与三次学习小事":id=="daily-company-3"?"累计参与三次同伴时光":id=="daily-rest-3"?"累计参与三次放松事项":id;
}

public sealed partial class CampusSemester
{
    int dailyOptionsDay=-1;
    SemesterDailyActivity[] dailyOptions;
    public SemesterDailyActivity[] DailyOptions
    {
        get
        {
            if(dailyOptions==null||dailyOptionsDay!=Clock.DayIndex)
            {dailyOptionsDay=Clock.DayIndex;dailyOptions=CampusDailyRules.ForDay(DailyCatalog,dailyOptionsDay);}
            return dailyOptions;
        }
    }
    public bool DailyCompleted(string id)=>State.dailyActivities.Any(a=>a.day==Clock.DayIndex&&a.id==id);
    public bool DailyTimeAvailable(SemesterDailyActivity activity)=>Clock.Now.Hour>=activity.openHour
        &&activity.choices.Any(c=>Clock.Now.AddMinutes(c.minutes)<=Clock.Now.Date.AddHours(activity.closeHour)&&Clock.Now.AddMinutes(c.minutes)<=GameClock.SemesterEnd);
    public string DailyRecap
    {
        get
        {
            string date=Clock.Now.ToString("yyyyMMdd")+"-";
            int classes=State.attendance.Count(id=>id.StartsWith(date,StringComparison.Ordinal));
            var done=State.dailyActivities.Where(a=>a.day==Clock.DayIndex).ToArray();
            return "上课 "+classes+" 节，课余小事 "+done.Length+" / 3。"+(done.Length>0?"留下了："+string.Join("、",done.Select(a=>a.title))+"。":"今天也可以只是好好休息，不必填满每个空档。");
        }
    }
    public string StoryText(SemesterStoryBeat beat)
    {
        if(string.IsNullOrEmpty(beat.recallFlag))return beat.text;
        int separator=beat.recallFlag.IndexOf("-choice-",StringComparison.Ordinal);
        if(separator>=0&&!State.flags.Any(f=>f.StartsWith(beat.recallFlag.Substring(0,separator)+"-choice-",StringComparison.Ordinal)))return beat.text;
        string recall=State.Has(beat.recallFlag)?beat.recallText:beat.alternateRecallText;
        return (string.IsNullOrEmpty(recall)?"":recall+"\n\n")+beat.text;
    }
    public string DailyFollowUp(SemesterDailyActivity activity)
    {
        var previous=State.dailyActivities.LastOrDefault(a=>a.id==activity.id&&a.day<Clock.DayIndex);
        if(previous==null)return activity.text;
        return "上回留下的片段："+previous.reply+"\n\n"+activity.text;
    }
    public string DailyUnavailableReason(string id,int choice,bool allowConflict=false)
    {
        if(!Ready)return "完成迎新后，从9月2日开始安排课余小事。";
        var activity=DailyOptions.FirstOrDefault(a=>a.id==id);
        if(activity==null)return "这不是今天的事项，请重新查看今日小事。";
        if(choice<0||choice>=activity.choices.Length)return "请选择一种参与方式。";
        if(DailyCompleted(id))return "这件小事今天已经完成，回忆已保留。";
        if(!At(activity.location))return "请先前往"+activity.place+"的交互点。";
        var option=activity.choices[choice];var end=Clock.Now.AddMinutes(option.minutes);
        if(end>GameClock.SemesterEnd)return "本学期已结束，请查看学期回顾。";
        if(end.Date!=Clock.Now.Date)return "这项安排会进入下一天，请先回宿舍休息。";
        if(!DailyTimeAvailable(activity)||end>Clock.Now.Date.AddHours(activity.closeHour))
            return "参与时段 "+activity.openHour.ToString("00")+":00—"+activity.closeHour.ToString("00")+":00，请在这个时段内完成。";
        if(State.energy<option.energy)return "精力不足，请先吃饭或休息。";
        return allowConflict?"":Conflict(option.minutes);
    }
    public bool CompleteDaily(string id,int choice,bool allowConflict=false)
    {
        string reason=DailyUnavailableReason(id,choice,allowConflict);
        if(!string.IsNullOrEmpty(reason))return Tell(reason);
        var activity=DailyOptions.First(a=>a.id==id);var option=activity.choices[choice];
        int count=State.dailyActivities.Count(a=>a.category==activity.category);
        string memory=count==0?"这是你第一次给这件事留出时间。":count<3?"从偶然碰见，到愿意再来一次，校园开始有了熟悉的节奏。":"熟悉的日常里，也能留下一个新的小片段。";
        State.dailyActivities.Add(new SemesterDailyRecord{day=Clock.DayIndex,id=id,category=activity.category,choice=choice,title=activity.title,reply=option.reply});
        if(!string.IsNullOrEmpty(option.course))
        {
            var course=State.Course(option.course);
            course.mastery=Mathf.Min(100,course.mastery+option.mastery);
        }
        State.fitness+=option.fitness;
        string milestone="daily-"+activity.category+"-3";
        if(count+1>=3&&!State.Has(milestone))
        {
            State.Mark(milestone);
            memory+="\n这些经历会接成一段新的生活故事，可在「故事与回忆」查看。";
        }
        Commit(activity.title,"我："+option.label+"\n"+activity.speaker+"："+option.reply+"\n"+memory,option.minutes,option.energy);
        return true;
    }
}
