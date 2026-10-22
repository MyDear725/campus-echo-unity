using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// North-up map in the same map metres as the campus. Labels remain live UI, never baked pixels.
[DefaultExecutionOrder(-150)]
public sealed class CampusMapUI : MonoBehaviour, ICampusOverlay
{
    public const float West=-230, North=-168, Width=540, Height=388;
    public bool IsOpen { get; private set; }
    public bool OverlayVisible=>IsOpen;
    public int BuildingCount=>buildings.Length;
    public string[] BuildingNames=>buildings.Select(b=>b.name).ToArray();
    public int SelectedId=>selected;
    public Rect LastMapRect {get;private set;}
    public Rect[] VisibleLabelRects {get;private set;}=Array.Empty<Rect>();
    CampusGame game; CampusFeature[] buildings=Array.Empty<CampusFeature>();
    Texture2D plan; GUIStyle title,body,muted,label,button,meta;
    Font font; int selected; float zoom=1; Vector2 pan,scroll;
    readonly Dictionary<int,Vector2> centres=new Dictionary<int,Vector2>();
    readonly Dictionary<int,string> captions=new Dictionary<int,string>();
    readonly Color dark=new Color(.12f,.20f,.24f),paper=new Color(.96f,.95f,.91f),green=new Color(.32f,.48f,.39f);
    public void Initialize(CampusGame owner,CampusData data)
    {
        game=owner;buildings=data.features.Where(f=>f.kind=="building").OrderBy(Order).ToArray();
        foreach(var b in buildings){centres[b.id]=new Vector2(b.points.Average(p=>p.x),b.points.Average(p=>p.z));captions[b.id]=Caption(b);}
        plan=Resources.Load<Texture2D>("CampusMap/Plan");CampusOverlayHub.Register(this);
    }
    static string Caption(CampusFeature b)
    {
        if(b.id==96575)return "三轴实验室";
        if(b.name.StartsWith("第")&&b.name.Contains("学生")&&b.id!=2304982)return b.name.Replace("学生","学生\n");
        return b.name.Replace("教学楼A区","A楼").Replace("教学楼B区","B楼").Replace("教学楼C区","C楼");
    }
    static int Order(CampusFeature b)
    {
        int[] order={77914,77917,77921,77923,96575,77925,77928,77927,77931,77933,77935,77937,77938,77941,2304982,77943};
        int i=Array.IndexOf(order,b.id);return i<0?order.Length+b.id:i;
    }
    void OnDestroy(){CampusOverlayHub.Unregister(this);if(IsOpen&&game)game.SetUiCursor(false);}
    void Update()
    {
        if(!game)return;
        if(IsOpen){if(Input.GetKeyDown(KeyCode.M)||Input.GetKeyDown(KeyCode.Escape))Close();return;}
        if(Input.GetKeyDown(KeyCode.M)&&!game.paused&&!game.UiBlocking&&!game.Seated&&game.ExteriorShortcutsAllowed&&CampusOverlayHub.InputConsumedFrame!=Time.frameCount)Open();
    }
    public bool Open()
    {
        if(!game||game.paused||game.UiBlocking||!game.ExteriorShortcutsAllowed)return false;
        IsOpen=true;zoom=1;pan=Vector2.zero;CampusOverlayHub.ConsumeInput();game.SetUiCursor(true);return true;
    }
    public void Close(){if(!IsOpen)return;IsOpen=false;CampusOverlayHub.ConsumeInput();if(game)game.SetUiCursor(false);}
    public void SelectBuilding(int id)
    {
        if(!centres.ContainsKey(id))return;selected=id;
        if(zoom>1.01f){pan=centres[id]-new Vector2(West+Width/2,North+Height/2);ClampPan();}
    }
    void Styles()
    {
        if(title!=null&&font==game.interfaceFont)return;font=game.interfaceFont;
        title=CampusStoryStyle.Label(font,28,paper,true);
        body=CampusStoryStyle.Label(font,15,paper,false,TextAnchor.MiddleLeft);
        muted=CampusStoryStyle.Label(font,13,CampusStoryStyle.Muted,false,TextAnchor.MiddleLeft);
        meta=CampusStoryStyle.Label(font,11,CampusStoryStyle.Muted,false,TextAnchor.MiddleLeft);
        label=CampusStoryStyle.Label(font,13,dark,true,TextAnchor.MiddleCenter);
        button=CampusStoryStyle.Label(font,14,paper,false,TextAnchor.MiddleLeft);
    }
    void ClampPan(){pan.x=Mathf.Clamp(pan.x,-Width/2*(1-1/zoom),Width/2*(1-1/zoom));pan.y=Mathf.Clamp(pan.y,-Height/2*(1-1/zoom),Height/2*(1-1/zoom));}
    Vector2 Point(Vector2 p,Rect r)=>new Vector2(r.width/2+(p.x-West-Width/2-pan.x)*r.width/Width*zoom,r.height/2+(p.y-North-Height/2-pan.y)*r.height/Height*zoom);
    public void DrawOverlay()
    {
        if(!game||!plan)return;Styles();
        float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
        float w=Screen.width/scale,h=Screen.height/scale;
        CampusStoryStyle.Fill(new Rect(0,0,w,h),new Color(.025f,.045f,.06f,.92f));
        var outer=new Rect(26,24,w-52,h-48);CampusStoryStyle.Panel(outer);
        GUI.Label(new Rect(52,43,600,23),"CAMPUS ATLAS  /  DALIAN UNIVERSITY OF TECHNOLOGY",meta);
        GUI.Label(new Rect(52,72,620,42),"开发区校区 · 校园导览",title);
        GUI.Label(new Rect(52,116,650,23),"循着林荫与湖畔，发现每一处校园日常",muted);
        if(CampusStoryStyle.Button(new Rect(w-183,67,130,42),"M / ESC  关闭",body))Close();
        CampusStoryStyle.Fill(new Rect(52,151,w-104,1),CampusStoryStyle.Line);
        var side=new Rect(52,175,226,h-255);CampusStoryStyle.Round(side,CampusStoryStyle.Surface);
        GUI.Label(new Rect(side.x+17,side.y+12,195,27),"建筑索引",body);
        GUI.Label(new Rect(side.x+17,side.y+40,195,23),BuildingCount+" 处建筑 · 点击高亮位置",meta);
        var viewport=new Rect(side.x+10,side.y+76,side.width-20,side.height-89);
        scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,viewport.width-18,buildings.Length*27+12),false,false);
        for(int i=0;i<buildings.Length;i++)
        {
            var b=buildings[i];var row=new Rect(0,5+i*27,viewport.width-18,26);
            bool active=b.id==selected;if(active)CampusStoryStyle.Round(row,new Color(.29f,.39f,.40f));
            var dot=new Rect(9,row.y+10,5,5);CampusStoryStyle.Round(dot,b.id==77943?CampusStoryStyle.Gold:b.name.Contains("公寓")||b.name.Contains("宿舍")?new Color(.70f,.59f,.43f):new Color(.53f,.70f,.69f));
            GUI.Label(new Rect(24,row.y,row.width-25,row.height),b.name=="大连理工大学三轴实验室"?"三轴实验室":b.name,button);
            if(GUI.Button(row,GUIContent.none,GUIStyle.none))SelectBuilding(b.id);
        }
        GUI.EndScrollView();
        // Fit the map without stretching. Spare space stays within the paper frame.
        var area=new Rect(298,175,w-350,h-302);float ratio=Width/Height;
        float mw=Mathf.Min(area.width,area.height*ratio),mh=mw/ratio;
        var map=new Rect(area.x+(area.width-mw)/2,area.y+(area.height-mh)/2,mw,mh);LastMapRect=map;
        CampusStoryStyle.Round(new Rect(area.x-1,area.y-1,area.width+2,area.height+2),new Color(.52f,.57f,.53f));
        CampusStoryStyle.Fill(area,paper);
        HandleMapInput(map);
        GUI.BeginGroup(map);
        GUI.DrawTexture(new Rect(map.width/2-map.width*zoom/2-pan.x*map.width/Width*zoom,map.height/2-map.height*zoom/2-pan.y*map.height/Height*zoom,map.width*zoom,map.height*zoom),plan);
        var local=new Rect(0,0,map.width,map.height);var labels=new List<Rect>();
        foreach(var b in buildings)
        {
            var at=Point(centres[b.id],map);string caption=captions[b.id];
            float bw=Mathf.Clamp(label.CalcSize(new GUIContent(caption)).x+14,42,160),bh=caption.Contains("\n")?34:25;
            var desired=new Rect(at.x-bw/2,at.y-bh/2,bw,bh);if(!local.Overlaps(desired))continue;
            var box=PlaceLabel(desired,labels,map);
            if(Vector2.Distance(box.center,at)>5)Line(at,box.center,new Color(.38f,.48f,.43f,.65f),1);
            if(b.id==selected){CampusStoryStyle.Round(new Rect(box.x-3,box.y-3,bw+6,bh+6),CampusStoryStyle.Gold);}
            CampusStoryStyle.Round(box,new Color(.985f,.98f,.95f,.94f));GUI.Label(box,caption,label);
            labels.Add(new Rect(box.x+map.x,box.y+map.y,box.width,box.height));
            if(GUI.Button(box,GUIContent.none,GUIStyle.none))SelectBuilding(b.id);
        }
        MapName("田径场",new Vector2(-135,-104),map);MapName("网球场",new Vector2(-197,-25),map);
        MapName("室外\n篮球场",new Vector2(135.5f,-104),map);MapName("锦屏山",new Vector2(-54,-99),map);
        MapName("思源湖",new Vector2(-25,-29),map);MapName("涌泉湖",new Vector2(-34,29),map);
        MapName("书韵广场",new Vector2(22,103),map);MapName("学苑桥",new Vector2(151,10),map);
        MapName("学\n府\n大\n街",new Vector2(175,-84),map);MapName("教学区南门",new Vector2(2,207),map);
        var pos=game.WorldToMap(game.player.position);var p=Point(new Vector2(pos.x,pos.z),map);
        CampusStoryStyle.Round(new Rect(p.x-10,p.y-10,20,20),new Color(.12f,.38f,.51f,.22f));
        CampusStoryStyle.Round(new Rect(p.x-6,p.y-6,12,12),new Color(.05f,.32f,.48f));
        var forward=game.WorldToMap(game.player.forward);Vector2 tip=p+new Vector2(forward.x,forward.z).normalized*17;Line(p,tip,new Color(.05f,.32f,.48f),2.5f);
        if(game.guide){var g=game.WorldToMap(game.guide.position);var q=Point(new Vector2(g.x,g.z),map);CampusStoryStyle.Round(new Rect(q.x-4,q.y-4,8,8),new Color(.72f,.42f,.18f));}
        GUI.EndGroup();VisibleLabelRects=labels.ToArray();
        // Compass, scale and controls sit on the paper, above the map content.
        var compass=new Rect(area.xMax-47,area.y+10,32,48);GUI.Label(compass,"N\n↑",label);
        var bar=new Rect(area.x+16,area.yMax-22,50*map.width/Width*zoom,2);CampusStoryStyle.Fill(bar,dark);GUI.Label(new Rect(bar.x,bar.y-18,100,17),"50 m",label);
        float bx=area.xMax-145,by=area.yMax-43;
        if(MapButton(new Rect(bx,by,32,29),"−"))SetZoom(zoom/1.3f);
        if(MapButton(new Rect(bx+38,by,32,29),"+"))SetZoom(zoom*1.3f);
        if(MapButton(new Rect(bx+76,by,54,29),"全图")){zoom=1;pan=Vector2.zero;}
        var pick=buildings.FirstOrDefault(b=>b.id==selected);
        GUI.Label(new Rect(302,h-119,w-510,27),pick!=null?pick.name:"从建筑索引或地图选择一栋建筑",body);
        GUI.Label(new Rect(302,h-89,w-520,21),pick!=null?"地图位置已高亮 · 走近建筑入口，按 E 交互":"滚轮缩放 · 按住空白处拖动 · 蓝色标记为当前位置",muted);
        GUI.Label(new Rect(53,h-65,220,22),"北向上  ·  校园空间示意",meta);
        var legend=new[]{"教学 / 公共建筑","学生公寓","湖泊与绿地","步行道路"};
        var colors=new[]{new Color(.51f,.67f,.69f),new Color(.74f,.63f,.46f),green,new Color(.83f,.81f,.75f)};
        for(int i=0;i<legend.Length;i++){float x=302+i*143;CampusStoryStyle.Round(new Rect(x,h-54,7,7),colors[i]);GUI.Label(new Rect(x+15,h-64,130,25),legend[i],meta);}
    }
    static Rect PlaceLabel(Rect desired,List<Rect> drawn,Rect map)
    {
        Rect At(float dx,float dy){var r=desired;r.x+=dx;r.y+=dy;r.x=Mathf.Clamp(r.x,3,Mathf.Max(3,map.width-r.width-3));r.y=Mathf.Clamp(r.y,3,Mathf.Max(3,map.height-r.height-3));return r;}
        bool Free(Rect r){r.xMin-=1;r.xMax+=1;r.yMin-=1;r.yMax+=1;return !drawn.Any(b=>r.Overlaps(new Rect(b.x-map.x,b.y-map.y,b.width,b.height)));}
        var first=At(0,0);if(Free(first))return first;
        for(int distance=8;distance<=96;distance+=8)
            foreach(var d in new[]{new Vector2(-distance,0),new Vector2(distance,0),new Vector2(0,-distance),new Vector2(0,distance),new Vector2(-distance,-distance),new Vector2(distance,distance)})
            {var r=At(d.x,d.y);if(Free(r))return r;}
        return first;
    }
    void MapName(string text,Vector2 at,Rect map){var q=Point(at,map);GUI.Label(new Rect(q.x-70,q.y-22,140,text.Contains("\n")?90:44),text,label);}
    bool MapButton(Rect r,string s){CampusStoryStyle.Round(r,new Color(.98f,.97f,.93f,.96f));GUI.Label(r,s,label);return GUI.Button(r,GUIContent.none,GUIStyle.none);}
    void SetZoom(float v){zoom=Mathf.Clamp(v,1,2.6f);ClampPan();}
    void HandleMapInput(Rect r)
    {
        var e=Event.current;if(!r.Contains(e.mousePosition))return;
        if(e.type==EventType.ScrollWheel){SetZoom(zoom*Mathf.Pow(1.12f,-e.delta.y));e.Use();}
        else if(e.type==EventType.MouseDrag&&e.button==0){pan-=new Vector2(e.delta.x*Width/r.width,e.delta.y*Height/r.height)/zoom;ClampPan();e.Use();}
    }
    static void Line(Vector2 a,Vector2 b,Color c,float width){if(Event.current.type!=EventType.Repaint)return;var old=GUI.matrix;var d=b-a;GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg,a);CampusStoryStyle.Fill(new Rect(a.x,a.y-width/2,d.magnitude,width),c);GUI.matrix=old;}
}
