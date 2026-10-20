using System;
using UnityEngine;

[Serializable] public sealed class WelcomeStoryData { public string title; public WelcomeStoryStage[] stages; }
[Serializable] public sealed class WelcomeStoryStage
{
    public string id,title,objective,condition,speaker,place,closing;
    public string[] lines,choices,replies;
}

// Versioned progress deliberately ignores the retired memory-collection save.
// Milestones come from actual gameplay; opening a menu or visiting a marker is
// insufficient to complete the meal or library objectives.
public sealed class CampusWelcomeStory : MonoBehaviour
{
    public static string KeyOverride;
    public static string ProgressKey => KeyOverride ?? "CampusWelcomeStoryV1";
    static string SaveKey => ProgressKey;
    public WelcomeStoryData Data { get; private set; }
    public int StageIndex { get; private set; }
    public bool DialogueOpen { get; private set; }
    public bool Finished => Data!=null && StageIndex>=Data.stages.Length;
    public string Objective => Finished?"迎新第一章完成 · 自由探索校园":Data.stages[StageIndex].objective;
    public string Heading => Finished?"明天见，同学":Data.stages[StageIndex].title;
    CampusGame game;
    bool pending, previousUi, dialogueDismissed;
    int line, choice=-1;
    bool response, closing;
    Vector3 guideHome;
    GUIStyle textStyle, speakerStyle, buttonStyle, metaStyle, keyStyle, choiceStyle, primaryStyle, avatarStyle, chapterStyle;
    Font styledFont;

    public void Initialize(CampusGame campus)
    {
        game=campus;
        Data=JsonUtility.FromJson<WelcomeStoryData>(Resources.Load<TextAsset>("Onboarding/story").text);
        StageIndex=Mathf.Clamp(PlayerPrefs.GetInt(SaveKey,0),0,Data.stages.Length);
        pending=!Finished && PlayerPrefs.GetInt(SaveKey+"Pending",-1)==StageIndex;
        if(game.guide){guideHome=game.guide.position;PlaceGuideNearGate();}
        foreach(var memory in game.memories)if(memory)memory.SetActive(false);
        game.Notify("迎新第一章："+Objective+"。按 J 查看当前指引。");
    }
    public void ReloadProgress()
    {
        StageIndex=Mathf.Clamp(PlayerPrefs.GetInt(SaveKey,0),0,Data.stages.Length);
        pending=!Finished&&PlayerPrefs.GetInt(SaveKey+"Pending",-1)==StageIndex;
        dialogueDismissed=false;DialogueOpen=false;
    }
    void PlaceGuideNearGate()
    {
        if(StageIndex!=0||!game.guide)return;
        var map=new Vector3(3,.23f,187);
        if(game.IsMapWalkable(map))game.guide.position=game.MapToWorld(map);
    }
    public string Prompt => game && !game.paused && !game.UiBlocking && !game.Interiors.Inside && game.guide && Vector3.Distance(game.player.position,game.guide.position)<4
        ? "[ E ] 与小岚交谈" : "";
    void Update()
    {
        if(!game||Data==null)return;
        if(DialogueOpen)
        {
            if(Input.GetKeyDown(KeyCode.Escape)){CampusOverlayHub.ConsumeInput();CloseDialogue();}
            else if(!closing&&!response&&line==Data.stages[StageIndex].lines.Length-1)
            {
                for(int i=0;i<Mathf.Min(9,Data.stages[StageIndex].choices.Length);i++)
                    if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+i))){Choose(i);break;}
            }
            else if(Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Return))Advance();
            return;
        }
        if(game.paused||game.Automated)return;
        if(Finished){if(Input.GetKeyDown(KeyCode.J)&&!game.UiBlocking)CampusSemester.Ensure().UI.Open("today");return;}
        if(Input.GetKeyDown(KeyCode.J)&&!game.UiBlocking)
        {
            if(pending&&!game.Seated)OpenDialogue();else game.Notify("小岚："+Objective);
        }
        if(!game.UiBlocking&&!game.Seated&&!game.Interiors.Busy)
        {
            if(!Finished&&!pending)CheckLocation();
            if(pending&&!dialogueDismissed)OpenDialogue();
        }
    }
    void LateUpdate()
    {
        if(!game||!game.guide||game.paused||game.UiBlocking||game.Interiors.Inside||game.Interiors.Busy)return;
        if(StageIndex>0&&!Finished)
        {
            Vector3 delta=game.player.position-game.guide.position;delta.y=0;
            if(delta.magnitude>30)
            {
                // Exterior shortcut: regroup only on a verified walkable point.
                for(int i=0;i<8;i++)
                {
                    var candidate=game.player.position+Quaternion.Euler(0,i*45,0)*(-game.player.forward*3);
                    if(game.IsMapWalkable(game.WorldToMap(candidate))){game.guide.position=candidate;break;}
                }
            }
            else if(delta.magnitude>3)
            {
                var step=delta.normalized*Mathf.Min(4*Time.deltaTime,delta.magnitude-3);
                var candidate=game.guide.position+step;
                if(game.IsMapWalkable(game.WorldToMap(candidate))){game.guide.position=candidate;game.guide.rotation=Quaternion.LookRotation(delta);}
            }
        }
    }
    void CheckLocation()
    {
        if(!game.Interiors.Inside||!game.Interiors.Interior)return;
        string building=game.Interiors.Interior.buildingId;
        string condition=Data.stages[StageIndex].condition;
        var dorm=DormitoryCorridor.Current(game);
        if(condition=="dorm-floor-2"&&dorm&&dorm.Floor==2&&!dorm.Transitioning)Reach(condition);
        else if(condition=="cafeteria"&&building=="cafeteria")Reach(condition);
        else if(condition=="teaching"&&building.StartsWith("teaching-",StringComparison.Ordinal))Reach(condition);
        else if(condition=="dorm-return"&&dorm&&dorm.Floor==1)Reach(condition);
    }
    public void Reach(string condition)
    {
        if(Finished||Data==null||Data.stages[StageIndex].condition!=condition)return;
        pending=true;dialogueDismissed=false;Save();
    }
    public bool TryInteract()
    {
        if(string.IsNullOrEmpty(Prompt))return false;
        if(StageIndex==0)Reach("guide");
        if(pending)OpenDialogue();else game.Notify("小岚："+Objective);
        return true;
    }
    void OpenDialogue()
    {
        if(DialogueOpen||Finished||!pending||game.UiBlocking||game.Seated)return;
        previousUi=game.UiBlocking;DialogueOpen=true;line=0;choice=-1;response=closing=false;
        game.SetUiCursor(true);
    }
    void CloseDialogue()
    {
        if(!DialogueOpen)return;
        DialogueOpen=false;dialogueDismissed=true;game.SetUiCursor(previousUi);
        game.Notify("对话已收起；按 J 继续与小岚交谈。");
    }
    void CompleteDialogue()
    {
        PlayerPrefs.SetInt(SaveKey+"Choice_"+Data.stages[StageIndex].id,choice);
        StageIndex++;pending=false;Save();DialogueOpen=false;game.SetUiCursor(previousUi);
        game.Notify(Finished?"迎新第一章完成。明天见，同学。":"接下来："+Objective);
    }
    public void ResetProgress()
    {
        CloseDialogue();foreach(var stage in Data.stages)PlayerPrefs.DeleteKey(SaveKey+"Choice_"+stage.id);StageIndex=0;pending=false;dialogueDismissed=false;Save();PlaceGuideNearGate();
        game.Notify("迎新剧情已重置。回南门找小岚开始；生活费和物品保持当前状态。");
    }
    void Save()
    {
        PlayerPrefs.SetInt(SaveKey,StageIndex);PlayerPrefs.SetInt(SaveKey+"Pending",pending?StageIndex:-1);PlayerPrefs.Save();
    }
    void OnDisable(){if(DialogueOpen&&game){DialogueOpen=false;game.SetUiCursor(previousUi);}}
    void OnDestroy(){if(game&&game.guide)game.guide.position=guideHome;}
    void Choose(int index){CampusOverlayHub.ConsumeInput();choice=index;response=true;}
    void Advance()
    {
        CampusOverlayHub.ConsumeInput();
        if(closing)CompleteDialogue();else if(response)closing=true;else line++;
    }
    void Styles()
    {
        if(textStyle!=null&&styledFont==game.interfaceFont)return;
        styledFont=game.interfaceFont;
        textStyle=CampusStoryStyle.Label(styledFont,22,CampusStoryStyle.Paper);
        speakerStyle=CampusStoryStyle.Label(styledFont,24,CampusStoryStyle.Paper,true);
        metaStyle=CampusStoryStyle.Label(styledFont,13,CampusStoryStyle.Muted);
        keyStyle=CampusStoryStyle.Label(styledFont,12,CampusStoryStyle.Muted,false,TextAnchor.MiddleCenter);
        buttonStyle=CampusStoryStyle.Label(styledFont,16,CampusStoryStyle.Paper,false,TextAnchor.MiddleCenter);
        choiceStyle=CampusStoryStyle.Label(styledFont,17,CampusStoryStyle.Paper,false,TextAnchor.MiddleLeft);
        primaryStyle=CampusStoryStyle.Label(styledFont,17,CampusStoryStyle.Ink,true,TextAnchor.MiddleCenter);
        avatarStyle=CampusStoryStyle.Label(styledFont,24,CampusStoryStyle.Gold,true,TextAnchor.MiddleCenter);
        chapterStyle=CampusStoryStyle.Label(styledFont,13,CampusStoryStyle.Ink,true,TextAnchor.MiddleCenter);
    }
    void OnGUI()
    {
        if(!DialogueOpen||Data==null)return;
        var oldMatrix=GUI.matrix;var oldColor=GUI.color;var oldContent=GUI.contentColor;int oldDepth=GUI.depth;
        float scale=Mathf.Min(Screen.height/900f,Screen.width/1160f,1.5f);
        GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);GUI.depth=-40;GUI.color=GUI.contentColor=Color.white;
        Styles();
        var stage=Data.stages[StageIndex];
        string content=closing?stage.closing:response?stage.replies[choice]:stage.lines[line];
        bool choices=!closing&&!response&&line==stage.lines.Length-1;
        float w=Screen.width/scale,h=Screen.height/scale,pw=Mathf.Min(1000,w-80),left=(w-pw)/2;
        float textHeight=Mathf.Max(78,textStyle.CalcHeight(new GUIContent(content),pw-80));
        float[] choiceHeights=new float[choices?stage.choices.Length:0];float optionsHeight=0;
        for(int i=0;i<choiceHeights.Length;i++)
        {choiceHeights[i]=Mathf.Max(48,choiceStyle.CalcHeight(new GUIContent(stage.choices[i]),pw-156)+22);optionsHeight+=choiceHeights[i]+10;}
        float actionHeight=choices?optionsHeight:50;
        float panelHeight=124+textHeight+24+actionHeight+57,top=h-panelHeight-42;
        CampusStoryStyle.Fill(new Rect(0,0,w,h),new Color(.025f,.04f,.065f,.18f));
        var panel=new Rect(left,top,pw,panelHeight);CampusStoryStyle.Panel(panel);
        CampusStoryStyle.Round(new Rect(left+32,top-14,192,28),CampusStoryStyle.Gold);
        GUI.Label(new Rect(left+42,top-14,172,28),"迎新故事  /  "+(StageIndex+1).ToString("00")+" — "+Data.stages.Length.ToString("00"),
            chapterStyle);
        CampusStoryStyle.Round(new Rect(left+32,top+35,48,48),new Color(.87f,.74f,.48f,.12f));
        GUI.Label(new Rect(left+32,top+35,48,48),"岚",avatarStyle);
        GUI.Label(new Rect(left+96,top+32,pw-340,34),stage.speaker,speakerStyle);
        GUI.Label(new Rect(left+96,top+68,pw-340,25),stage.place+"   /   "+stage.title,metaStyle);
        if(CampusStoryStyle.Button(new Rect(left+pw-144,top+36,112,36),"收起  ESC",buttonStyle)){CampusOverlayHub.ConsumeInput();CloseDialogue();}
        CampusStoryStyle.Fill(new Rect(left+32,top+106,pw-64,1),CampusStoryStyle.Line);
        GUI.Label(new Rect(left+40,top+124,pw-80,textHeight),content,textStyle);
        float actionY=top+124+textHeight+24;
        if(choices)
        {
            for(int i=0;i<stage.choices.Length;i++)
            {
                var r=new Rect(left+32,actionY,pw-64,choiceHeights[i]);
                bool clicked=CampusStoryStyle.Button(r,"",choiceStyle);
                CampusStoryStyle.Key(new Rect(r.x+14,r.y+(r.height-26)/2,26,26),(i+1).ToString(),keyStyle);
                GUI.Label(new Rect(r.x+56,r.y+10,r.width-104,r.height-20),stage.choices[i],choiceStyle);
                GUI.Label(new Rect(r.xMax-34,r.y,18,r.height),"›",buttonStyle);
                if(clicked)Choose(i);
                actionY+=choiceHeights[i]+10;
            }
        }
        else
        {
            string label=closing?(StageIndex==Data.stages.Length-1?"结束对话":"记住了，出发吧"):"继续对话   ›";
            if(CampusStoryStyle.Button(new Rect(left+pw-252,actionY,220,50),label,primaryStyle,true))Advance();
        }
        GUI.Label(new Rect(left+40,top+panelHeight-35,pw-80,21),choices?"选择你的回应  ·  点击选项或按数字键":"空格 / Enter 继续   ·   J 重新打开指引",metaStyle);
        if(!choices)
            for(int i=0;i<stage.lines.Length;i++)CampusStoryStyle.Round(new Rect(left+40+i*22,actionY+21,i==line&&!response&&!closing?14:6,6),i==line&&!response&&!closing?CampusStoryStyle.Gold:CampusStoryStyle.Line);
        GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.contentColor=oldContent;GUI.depth=oldDepth;
    }
}
