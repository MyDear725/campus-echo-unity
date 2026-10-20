using System;
using System.Linq;
using UnityEngine;

// A campus handbook shared by K, the phone and contextual E. No action bypasses world validation.
public sealed class CampusSemesterUI : MonoBehaviour, ICampusOverlay
{
    CampusSemester semester;
    public bool IsOpen { get; private set; }
    public bool OverlayVisible => IsOpen;
    string page="today", selectedBeat, confirmTitle, confirmText;
    Func<bool> confirmAction;
    Vector2 scroll;
    bool previousPaused;
    GUIStyle title, body, muted, button, selectedButton, section;
    Texture2D normalFill, hoverFill, activeFill;
    readonly Color paper = new Color(.95f,.94f,.87f), sage = new Color(.74f,.84f,.76f);
    public void Initialize(CampusSemester owner) { semester=owner; CampusOverlayHub.Register(this); }
    void OnEnable() { if (semester) CampusOverlayHub.Register(this); }
    void OnDisable() { CampusOverlayHub.Unregister(this); }
    void OnDestroy() { CampusOverlayHub.Unregister(this);if(normalFill)Destroy(normalFill);if(hoverFill)Destroy(hoverFill);if(activeFill)Destroy(activeFill); }
    void Update()
    {
        if (!IsOpen || !semester.Game) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.K))
        {
            CampusOverlayHub.ConsumeInput();
            if (confirmAction != null) { confirmAction=null; semester.EndClassroom(); return; }
            Close();
        }
    }
    public void Open(string target="today")
    {
        if (!semester || !semester.Game || semester.Restoring) return;
        if (!IsOpen)
        {
            if (semester.Game.UiBlocking && !(PhoneUI.Instance && PhoneUI.Instance.IsOpen)) return;
            previousPaused=semester.Game.paused;
        }
        if (PhoneUI.Instance && PhoneUI.Instance.IsOpen) PhoneUI.Instance.Close();
        CampusOverlayHub.ConsumeInput(); IsOpen=true; page=target; scroll=Vector2.zero; selectedBeat=null;
        semester.Game.SetUiCursor(true);
    }
    public void Close()
    {
        IsOpen=false; confirmAction=null;
        if(semester)semester.EndClassroom();
        CampusOverlayHub.ConsumeInput();
        if (semester && semester.Game) { semester.Game.paused=previousPaused; semester.Game.SetUiCursor(false); }
    }
    public void OpenAt(CampusSemesterPoint point)
    {
        if(point.location=="dorm"){Open("life");return;}
        var beat=semester.Catalog.beats.FirstOrDefault(b=>b.location==point.location&&SemesterRules.Available(semester.State,b,semester.Clock.DayIndex));
        if(beat!=null){Open("story");selectedBeat=beat.id;return;}
        Open(point.location=="a"||point.location=="b"||point.location=="c"?"today":"story");
    }
    public void RequestReset(bool whole)
    {
        Open("today");
        Ask(whole ? "重新开始本学期" : "重新开始今天", whole
            ? "回到2026年9月1日08:00。本学期的剧情、课程、成绩、资金、物品与奖励都会恢复初始状态。角色及操作设置保留。系统会先留一份完整备份。"
            : "恢复今天开始时的完整进度。今天发生的消费、获得的物品、学习、成绩、任务与奖励同时撤回；此前各天的进度保留。系统会先留一份完整备份。",
            () => whole ? CampusSemesterSave.RestoreSemester(semester) : CampusSemesterSave.RestoreDay(semester));
    }
    void Ask(string heading,string text,Func<bool> action) { confirmTitle=heading;confirmText=text;confirmAction=action; }
    void WithTime(string heading,int minutes,Func<bool> normal,Func<bool> overrideConflict,string except=null)
    {
        string conflict=semester.Conflict(minutes,except);
        if (!string.IsNullOrEmpty(conflict)) Ask(heading, "预计结束 " + semester.Clock.Now.AddMinutes(minutes).ToString("HH:mm") + "。" + conflict + "仍要这样安排吗？",overrideConflict);
        else normal();
    }
    void Styles()
    {
        if (body != null) return;
        var font=semester.Game.interfaceFont;
        body=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true,richText=false,normal={textColor=paper}};
        muted=new GUIStyle(body){fontSize=16,normal={textColor=sage}};
        title=new GUIStyle(body){fontSize=30,fontStyle=FontStyle.Bold};
        section=new GUIStyle(body){fontSize=23,fontStyle=FontStyle.Bold};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=18,wordWrap=true,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(14,14,9,9),margin=new RectOffset(0,0,4,7)};
        button.normal.textColor=paper; button.hover.textColor=Color.white; button.focused.textColor=Color.white;
        selectedButton=new GUIStyle(button);selectedButton.normal.textColor=new Color(1,.86f,.57f);
        normalFill=Swatch(new Color(.10f,.18f,.14f));hoverFill=Swatch(new Color(.17f,.27f,.21f));activeFill=Swatch(new Color(.23f,.33f,.25f));
        foreach(var style in new[]{button,selectedButton})
        {
            style.fixedHeight=0;style.stretchHeight=false;style.alignment=TextAnchor.MiddleLeft;
            style.normal.background=normalFill;style.hover.background=hoverFill;style.focused.background=hoverFill;style.active.background=activeFill;
        }
        selectedButton.normal.background=activeFill;
    }
    static Texture2D Swatch(Color color)
    {
        var t=new Texture2D(1,1){hideFlags=HideFlags.HideAndDontSave};t.SetPixel(0,0,color);t.Apply();return t;
    }
    public void DrawOverlay()
    {
        if (!semester.Game) return;
        Styles();
        float scale=Mathf.Min(Screen.width/1180f,Screen.height/840f); scale=Mathf.Min(scale,1.4f);
        GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
        float w=Screen.width/scale,h=Screen.height/scale;
        var panel=new Rect((w-1100)/2,(h-780)/2,1100,780);
        Fill(new Rect(0,0,w,h),new Color(.02f,.04f,.035f,.78f));
        Fill(panel,new Color(.055f,.105f,.085f,1));
        GUI.Label(new Rect(panel.x+28,panel.y+22,710,42),"我的第一个大学学期",title);
        GUI.Label(new Rect(panel.x+28,panel.y+69,960,30),semester.Clock.DateTimeText+"  ·  "+semester.Clock.WeekText,muted);
        if(GUI.Button(new Rect(panel.x+962,panel.y+24,110,42),"关闭  K",button))Close();
        Fill(new Rect(panel.x+24,panel.y+112,1052,1),new Color(.32f,.43f,.35f));
        string[] ids={"today","study","growth","story","life"};string[] labels={"今日日程","学习与作业","成绩与奖励","故事与回忆","生活与设置"};
        for(int i=0;i<ids.Length;i++)if(GUI.Button(new Rect(panel.x+24,panel.y+134+i*62,178,50),labels[i],page==ids[i]?selectedButton:button)){page=ids[i];scroll=Vector2.zero;selectedBeat=null;confirmAction=null;}
        GUI.Label(new Rect(panel.x+28,panel.y+492,168,110),"精力 "+semester.State.energy+" / 100\n"+CampusWallet.BalanceText,body);
        GUI.Label(new Rect(panel.x+28,panel.y+620,168,92),"K 学期手册\nP 手机预约\nE 到场交互",muted);
        GUILayout.BeginArea(new Rect(panel.x+230,panel.y+132,842,520));
        scroll=GUILayout.BeginScrollView(scroll,false,true);
        if(confirmAction!=null)DrawConfirmation();
        else switch(page)
        {
            case "today": Today();break;
            case "study": Study();break;
            case "growth": Growth();break;
            case "story": Stories();break;
            case "life": Life();break;
        }
        GUILayout.EndScrollView();GUILayout.EndArea();
        Fill(new Rect(panel.x+230,panel.y+675,842,1),new Color(.32f,.43f,.35f));
        GUI.Label(new Rect(panel.x+230,panel.y+690,820,68),string.IsNullOrEmpty(semester.LastMessage)?"查看日程时暂停计时。到场后开始上课、学习或活动。":semester.LastMessage,muted);
    }
    static void Fill(Rect rect,Color color) { var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old; }
    void Heading(string text){GUILayout.Label(text,section);GUILayout.Space(9);}
    void Text(string text){GUILayout.Label(text,body);GUILayout.Space(9);}
    void Hint(string text){GUILayout.Label(text,muted);GUILayout.Space(7);}
    bool Btn(string text,bool enabled=true)
    {
        bool old=GUI.enabled;GUI.enabled=enabled;
        bool clicked=GUILayout.Button(text,button,GUILayout.Height(text.Contains("\n")?76:48));GUI.enabled=old;return clicked;
    }
    void DrawConfirmation()
    {
        Heading(confirmTitle);Text(confirmText);
        if(Btn("确认 · "+confirmTitle)){var action=confirmAction;confirmAction=null;action();}
        if(Btn("返回，保留当前进度")){confirmAction=null;semester.EndClassroom();}
    }
    void Today()
    {
        Heading(SemesterRules.Chapter(semester.Clock.DayIndex));Text(semester.Objective);
        if(!semester.Ready){Hint("迎新结束后，在宿舍一楼的休息登记处按 E，进入9月2日。迎新期间不计时。");return;}
        var lessons=SemesterRules.Lessons(semester.Clock.Now);
        if(lessons.Count==0)Text("今天没有排课，可以安排学习和活动。");
        foreach(var l in lessons)
        {
            bool done=semester.State.attendance.Contains(l.id);
            Heading(l.start.ToString("HH:mm")+"—"+l.end.ToString("HH:mm")+"  "+l.title+" · "+l.room);
            Hint(done?"已完成课堂与到课记录。":semester.Clock.Now>=l.end?"已错过本节课；后续仍可学习并参加下一次课程。":"90分钟课堂 · 精力12 · 课后开放一份作业");
            if(!done && Btn("指引至 "+l.room))Track(l.course=="math"?"a":l.course=="code"?"c":"b");
            bool can=semester.At(l.course=="math"?"a":l.course=="code"?"c":"b")&&!done&&semester.Clock.Now>=l.start.AddMinutes(-30)&&semester.Clock.Now<l.end;
            if(Btn("入座上课 · 参与课堂互动",can))
            {
                if(semester.BeginClassroom(l.course))
                    Ask(l.title+" · 课堂互动",semester.ClassroomQuestion(l.course)+"\n课堂将推进至 "+l.end.ToString("HH:mm")+"，消耗12精力，获得课堂记录与作业。",()=>{semester.EndClassroom();return semester.Attend(l.course,true);});
            }
            if(!done&&semester.Clock.Now<l.start.AddMinutes(-30)&&Btn("等候至开课前20分钟"))
            { var target=l.start.AddMinutes(-20);WithTime("等候",(int)(target-semester.Clock.Now).TotalMinutes,()=>semester.WaitUntil(target),()=>semester.WaitUntil(target,true)); }
            GUILayout.Space(14);
        }
        if(semester.Clock.DayIndex>=70&&!semester.State.Has("midterms"))
        {
            Text("期中考核已开放 · A101 · 120分钟 / 精力20");
            if(Btn("参加期中考核",semester.At("a")))WithTime("期中考核",120,()=>semester.Exam(false),()=>semester.Exam(false,true));
        }
        if(semester.Clock.DayIndex>=133&&!semester.State.Has("finals"))
        {
            Text("期末考核已开放 · A101 · 120分钟 / 精力20");
            if(Btn("参加期末考核",semester.At("a")))WithTime("期末考核",120,()=>semester.Exam(true),()=>semester.Exam(true,true));
        }
        var next=semester.Catalog.beats.FirstOrDefault(b=>SemesterRules.Available(semester.State,b,semester.Clock.DayIndex));
        if(next!=null){Heading("可以继续的故事");Text(next.title+" · "+next.place);if(Btn("查看故事与前往指引")){page="story";selectedBeat=next.id;}}
    }
    void Study()
    {
        Heading("把不会的那一步弄懂");Hint("到图书馆预约并落座后，选择科目和时长。学习完成一份对应作业；提交后才记录平时表现。重复学习同一内容的收益会逐渐降低。");
        bool seated=LibraryStudy.Instance&&LibraryStudy.Instance.Seated;
        if(!seated&&Btn("打开手机预约座位")){Close();PhoneUI.Ensure().Open();}
        foreach(var c in semester.State.courses)
        {
            Text(c.name+" · "+c.topic+"\n掌握度 "+c.mastery.ToString("F1")+" / 100");
            GUILayout.BeginHorizontal();
            foreach(int duration in new[]{30,60,90})
            {
                int minutes=duration;
                if(Btn("学习 "+minutes+"分钟\n精力 "+minutes/6,seated&&semester.Ready))WithTime(c.name+"针对学习",minutes,()=>semester.Study(c.id,minutes),()=>semester.Study(c.id,minutes,true));
            }
            GUILayout.EndHorizontal();GUILayout.Space(10);
        }
        Heading("我的作业");
        if(semester.State.assignments.Count==0)Hint("还没有作业。参加第一节课后，老师会留下学习任务。");
        foreach(var task in semester.State.assignments.Where(t=>!t.submitted).ToArray())
        {
            Text(semester.State.Course(task.course).name+" · "+GameClock.SemesterStart.AddDays(task.dueDay).ToString("M月d日")+"截止 · "+(task.prepared?"已完成，待提交":"待学习"));
            if(Btn(task.prepared?"提交这份作业":"先完成对应科目学习",task.prepared))semester.Submit(task.id);
        }
        Hint("已提交 "+semester.State.assignments.Count(t=>t.submitted)+" 份作业。错过截止仍可补交，记录会注明补交。");
    }
    void Growth()
    {
        Heading("课程成长");Hint("暂定游戏规则：平时30%＋期中20%＋期末50%。每门通过课程计3学分，期末后结算。");
        foreach(var c in semester.State.courses)
        {
            Text(c.name+"\n知识掌握 "+c.mastery.ToString("F1")+" / 100  ·  到课 "+c.attended+"  ·  作业成果 "+c.homeworkSubmitted);
            Hint("首次小测 "+Score(c.baseline)+"  /  平时 "+c.Coursework.ToString("F0")+"  /  期中 "+Score(c.midterm)+"  /  期末 "+Score(c.final)+(c.final>=0?"  /  总评 "+c.Grade+" · "+(c.Grade>=60?"3学分":"待通过"):""));
        }
        Heading("学期成果与奖励");Text("社团："+(string.IsNullOrEmpty(semester.State.club)?"尚未选择":semester.State.club)+"\n项目进度："+semester.State.projectStep+" / 5  ·  实践记录 "+semester.State.practice+"  ·  校园贡献 "+semester.State.contribution);
        Text(semester.AwardSummary);
        Hint("学业优秀奖学金在下学期发放；本学期达成任一奖励方向，可领取一次游戏内学期学习奖励200元。先到B103完成申请。");
        if(Btn(semester.State.awardClaimed?"本学期结果已领取":"领取学期评定结果",semester.State.awardsApplied&&semester.State.Has("finals")&&!semester.State.awardClaimed))semester.ClaimAward();
    }
    static string Score(int value)=>value<0?"未考核":value.ToString();
    void Stories()
    {
        if(selectedBeat!=null)
        {
            var beat=semester.Catalog.beats.First(b=>b.id==selectedBeat);
            if(Btn("返回故事列表")){selectedBeat=null;return;}
            Heading(beat.title);Hint(beat.speaker+" · "+beat.place+" · "+beat.minutes+"分钟");Text(beat.text);
            bool available=semester.Ready&&SemesterRules.Available(semester.State,beat,semester.Clock.DayIndex);
            if(semester.State.Has(beat.id))
            {
                for(int i=0;i<beat.choices.Length;i++)if(semester.State.Has(beat.id+"-choice-"+i)){Text("我："+beat.choices[i]);Text(beat.speaker+"："+beat.replies[i]);}
                Hint(beat.closing);return;
            }
            if(!available)Hint(Requirement(beat));
            if(Btn("前往 "+beat.place))Track(beat.location);
            if(available&&!semester.At(beat.location))Hint("请到场后再开始对话，按 E 可打开本页。");
            for(int i=0;i<beat.choices.Length;i++)
            {
                int choice=i;
                if(Btn(beat.choices[i],available&&semester.At(beat.location)))WithTime(beat.title,beat.minutes,()=>semester.CompleteBeat(beat.id,choice),()=>semester.CompleteBeat(beat.id,choice,true));
            }
            return;
        }
        Heading("校园里的故事");
        foreach(var beat in semester.Catalog.beats)
        {
            string state=semester.State.Has(beat.id)?"已完成":SemesterRules.Available(semester.State,beat,semester.Clock.DayIndex)?"可继续":"尚未开放";
            if(Btn(beat.title+"  ·  "+state+"\n"+beat.place)){selectedBeat=beat.id;scroll=Vector2.zero;}
            if(state=="尚未开放")Hint(Requirement(beat));
        }
    }
    string Requirement(SemesterStoryBeat beat)
    {
        string info="从"+GameClock.SemesterStart.AddDays(beat.day).ToString("M月d日")+"开放";
        if(!string.IsNullOrEmpty(beat.requires))
        {
            var names=beat.requires.Split(',').Where(id=>!semester.State.Has(id)).Select(id=>semester.Catalog.beats.FirstOrDefault(b=>b.id==id)?.title??(id=="class-first"?"参加第一节课":id=="homework-first"?"提交第一份作业":id=="finals"?"完成期末考核":id=="meal"?"完成一次用餐与还盘":id.StartsWith("study-")?"完成"+semester.State.Course(id.Substring(6)).name+"学习":id));
            var text=string.Join("、",names);if(text.Length>0)info+="；先完成："+text;
        }
        return info;
    }
    void Life()
    {
        Heading("生活与日程");Text("每周生活费300元自动到账。到第六食堂完整用餐可恢复精力，每个用餐时段生效一次。图书整理岗位提供一次30元补贴。休息恢复精力，跨过午夜本身不会恢复。");
        if(Btn("指引至宿舍休息登记处"))Track("dorm");
        if(Btn("休息至明早07:00",semester.Ready&&semester.At("dorm")))Ask("休息至明早","将结束今天，未完成的课程与任务不会自动完成。明早恢复精力并生成新的每日起点存档。",()=>semester.Rest());
        if(Btn("查看平常日计划",semester.Ready&&semester.At("dorm")))Ask("按计划度过平常日","仅第一周之后的普通日、早晨8点前可用。完成今日课程与练习，结算餐费20元，时间到21:00；不会代做新剧情、考试和活动。",()=>semester.RoutineDay());
        if(Btn(semester.State.reminders?"课程与作业提醒：已开启 · 点击关闭":"课程与作业提醒：已关闭 · 点击开启"))
        {semester.State.reminders=!semester.State.reminders;semester.Save();}
        if(Btn(semester.Route.LineEnabled?"路线指引线：已开启 · 点击关闭":"路线指引线：已关闭 · 点击开启"))
        {semester.Route.SetEnabled(!semester.Route.LineEnabled);semester.Tell(semester.Route.LineEnabled?"路线指引线已开启。追踪目标后沿地面路线前进。":"路线指引线已关闭，仍保留文字方向提示。");}
        if(Btn("取消当前追踪",!string.IsNullOrEmpty(semester.State.trackedLocation))){semester.State.trackedLocation="";semester.Save();}
        if(CampusSemesterSave.HasBackup&&Btn("恢复重置前的备份"))Ask("恢复重置前备份","将完整恢复最近一次重置之前的日期、任务、余额、成绩和每日起点，不与当前进度混合。",()=>CampusSemesterSave.RestoreBackup(semester));
        GUILayout.Space(15);Heading("学期回忆");
        if(semester.State.journal.Count==0)Hint("第一条学习与校园活动记录，会从这里开始。");
        foreach(var entry in semester.State.journal.AsEnumerable().Reverse().Take(80)){Text(entry.date+" · "+entry.title);Hint(entry.text);}
    }
    public void Track(string location)
    {
        semester.State.trackedLocation=location;semester.Save();semester.Tell("已追踪："+CampusSemesterPoint.PlaceName(location)+"。地面指引线可在生活与设置中开关。");
    }
    public string Navigation()
    {
        if(!semester.Route.Resolve(out var destination,out var instruction))return "";
        var game=semester.Game;var delta=destination-game.player.position;
        float angle=Vector3.SignedAngle(game.followCamera.transform.forward,new Vector3(delta.x,0,delta.z),Vector3.up);
        string direction=Mathf.Abs(angle)<25?"前方":Mathf.Abs(angle)>150?"后方":angle>0?"右侧":"左侧";
        return instruction+" · "+direction+" "+new Vector2(delta.x,delta.z).magnitude.ToString("F0")+"米";
    }
}
