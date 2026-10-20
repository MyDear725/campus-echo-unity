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
    GUIStyle textStyle, speakerStyle, buttonStyle;

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
    void OnGUI()
    {
        if(!DialogueOpen||Data==null)return;
        var oldMatrix=GUI.matrix;var oldColor=GUI.color;int oldDepth=GUI.depth;
        float scale=Mathf.Clamp(Screen.height/900f,.65f,1.5f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);GUI.depth=-40;
        float w=Screen.width/scale,h=Screen.height/scale,pw=Mathf.Min(820,w-32),left=(w-pw)/2,top=h-390;
        GUI.color=new Color(.06f,.10f,.09f,.98f);GUI.DrawTexture(new Rect(left,top,pw,320),Texture2D.whiteTexture);GUI.color=Color.white;
        textStyle=new GUIStyle(GUI.skin.label){font=game.interfaceFont,fontSize=20,wordWrap=true,normal={textColor=new Color(.95f,.94f,.87f)}};
        speakerStyle=new GUIStyle(textStyle){fontSize=17,normal={textColor=new Color(.86f,.76f,.53f)}};
        buttonStyle=new GUIStyle(GUI.skin.button){font=game.interfaceFont,fontSize=17,wordWrap=true};
        var stage=Data.stages[StageIndex];
        GUI.Label(new Rect(left+24,top+16,pw-48,30),stage.speaker+"  ·  "+stage.place,speakerStyle);
        string content=closing?stage.closing:response?stage.replies[choice]:stage.lines[line];
        GUI.Label(new Rect(left+24,top+62,pw-48,116),content,textStyle);
        bool choices=!closing&&!response&&line==stage.lines.Length-1;
        if(choices)
        {
            for(int i=0;i<stage.choices.Length;i++)if(GUI.Button(new Rect(left+24,top+188+i*49,pw-48,42),stage.choices[i],buttonStyle)){choice=i;response=true;}
        }
        else if(GUI.Button(new Rect(left+24,top+233,pw-48,48),closing?(Finished?"结束对话":"记住了，出发吧"):"继续",buttonStyle))
        {
            if(closing)CompleteDialogue();else if(response)closing=true;else line++;
        }
        GUI.Label(new Rect(left+24,top+291,pw-48,23),"ESC 收起对话 · J 重新阅读",speakerStyle);
        GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.depth=oldDepth;
    }
}
