using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable] public sealed class DormitoryLayout
{
    public int floors, roomsPerSide;
    public float corridorLength, corridorWidth, wallHeight, roomPitch, firstRoomX, stairX;
    public string wallColor, lowerWallColor, doorColor, floorColor;
    public static DormitoryLayout Load() => JsonUtility.FromJson<DormitoryLayout>(Resources.Load<TextAsset>("Onboarding/dormitory").text);
    public string RoomNumber(int floor, int index) => (floor * 100 + index + 1).ToString();
}

// One physical corridor is reused for six logical floors. Each room door opens
// the same reusable bedroom model; portals connect adjacent floors at either stair.
public sealed class DormitoryCorridor : MonoBehaviour
{
    public int Floor { get; private set; } = 1;
    public bool Transitioning { get; private set; }
    public event Action<int> FloorChanged;
    readonly List<TextMesh> numbers = new List<TextMesh>();
    readonly List<Material> ownedMaterials = new List<Material>();
    readonly List<Texture2D> ownedTextures = new List<Texture2D>();
    readonly List<DormitoryRoomInterior.Doorway> roomDoors = new List<DormitoryRoomInterior.Doorway>();
    TextMesh westSign, eastSign, exitSign;
    readonly List<TextMesh> upSigns = new List<TextMesh>(), downSigns = new List<TextMesh>();
    readonly List<GameObject> upBarriers = new List<GameObject>(), downBarriers = new List<GameObject>();
    readonly List<GameObject> upFlights = new List<GameObject>(), downFlights = new List<GameObject>();
    InteriorSceneRoot interior;
    CampusGame game;
    DormitoryLayout layout;
    float fade;
    string transitionLabel;
    bool restoringUi;
    Transform corridorVisual;
    DormitoryRoomInterior roomInterior;
    int roomIndex=-1, roomSide;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register()
    {
        SceneManager.sceneLoaded -= Prepare;
        SceneManager.sceneLoaded += Prepare;
    }
    static void Prepare(Scene scene, LoadSceneMode mode)
    {
        foreach (var root in scene.GetRootGameObjects())
        foreach (var contract in root.GetComponentsInChildren<InteriorSceneRoot>(true))
            if (contract.isPlaceholder && contract.buildingId.StartsWith("dormitory-", StringComparison.Ordinal) && !contract.GetComponent<DormitoryCorridor>())
                contract.gameObject.AddComponent<DormitoryCorridor>().Build(contract);
    }
    public static DormitoryCorridor Current(CampusGame campus)
    {
        return campus && campus.Interiors && campus.Interiors.Interior
            ? campus.Interiors.Interior.GetComponent<DormitoryCorridor>() : null;
    }
    public DormitoryLayout Layout=>layout;
    public bool InsideRoom=>roomInterior&&roomInterior.Inside;
    public int CurrentRoomIndex=>roomIndex;
    public string CurrentRoomNumber=>roomIndex<0?"":layout.RoomNumber(Floor,roomIndex);
    public void SetRoomState(bool inside,int index,int side){roomIndex=index;roomSide=side;}
    void Build(InteriorSceneRoot contract)
    {
        interior = contract;
        game = FindFirstObjectByType<CampusGame>();
        layout = DormitoryLayout.Load();
        // Preserve the travel contract and its anchors, retire just the placeholder.
        foreach (Transform child in transform)
            if (child.name.StartsWith("PlaceholderShell", StringComparison.Ordinal)) child.gameObject.SetActive(false);
        corridorVisual = new GameObject("Dormitory public corridor · six floors").transform;
        corridorVisual.SetParent(transform, false);
        Transform parent = corridorVisual;
        Material plaster = Mat(layout.wallColor), lower = Mat(layout.lowerWallColor), wood = Mat(layout.doorColor);
        Material stone = Mat(layout.floorColor), dark = Mat("#34483e"), brass = Mat("#d9c7a0"), white = Mat("#faf7e9");
        float half = layout.corridorWidth / 2;
        var tiledFloor=Mat("#ffffff");tiledFloor.mainTexture=SurfaceTexture(layout.floorColor,false);tiledFloor.mainTextureScale=new Vector2(50,4);
        wood.mainTexture=SurfaceTexture(layout.doorColor,true);wood.color=Color.white;
        Box(parent,"Corridor floor",new Vector3(0,-.12f,0),new Vector3(layout.corridorLength,.24f,layout.corridorWidth),tiledFloor);
        Box(parent,"Entrance floor",new Vector3(0,-.12f,-2.75f),new Vector3(3,.24f,1.7f),stone);
        for(int side=-1;side<=1;side+=2)
        {
            // South wall has a real central opening for the entrance vestibule.
            if(side==1) Wall(parent,0,side*half,layout.corridorLength,plaster,lower);
            else { Wall(parent,-11.25f,-half,19.5f,plaster,lower); Wall(parent,11.25f,-half,19.5f,plaster,lower); }
            for(int i=0;i<layout.roomsPerSide;i++)
            {
                float x=layout.firstRoomX+i*layout.roomPitch;
                float z=side*(half-.10f), face=side==1?180:0;
                Box(parent,"Door frame",new Vector3(x,1.16f,z),new Vector3(1.34f,2.32f,.16f),dark);
                var hinge=new GameObject("Room door hinge").transform;hinge.SetParent(parent,false);
                hinge.localPosition=new Vector3(x-.58f,1.10f,z-side*.10f);
                Box(hinge,"Closed room door",new Vector3(.58f,0,0),new Vector3(1.16f,2.20f,.10f),wood);
                Box(hinge,"Door inset",new Vector3(.58f,.24f,-side*.055f),new Vector3(.85f,1.30f,.025f),wood);
                Box(hinge,"Door handle",new Vector3(.98f,-.08f,-side*.09f),new Vector3(.15f,.04f,.06f),brass);
                Box(parent,"Room number plate",new Vector3(x,2.58f,z-side*.09f),new Vector3(.70f,.30f,.07f),dark);
                int index=side==1?i:i+layout.roomsPerSide;
                var label=Text(parent,"Room number",layout.RoomNumber(1,index),new Vector3(x,2.58f,z-side*.14f),face,.19f,Color.white);
                // Store numbers in room order, independent of build order.
                while(numbers.Count<=index)numbers.Add(null);
                numbers[index]=label;
                roomDoors.Add(new DormitoryRoomInterior.Doorway{Index=index,X=x,Side=side,Hinge=hinge,ClosedRotation=hinge.localRotation});
                if(i%2==0)
                    Text(parent,"Hallway motto",i%4==0?"好好学习 · 天天向上":"文明宿舍 · 温暖同行",
                        new Vector3(x,2.94f,z+side*.015f),face,.19f,new Color(.25f,.35f,.28f));
            }
            // Tile seams belong to the floor material, not tiny walkable colliders.
        }
        Box(parent,"Entrance west jamb",new Vector3(-1.60f,1.6f,-2.75f),new Vector3(.2f,3.2f,1.9f),plaster);
        Box(parent,"Entrance east jamb",new Vector3(1.60f,1.6f,-2.75f),new Vector3(.2f,3.2f,1.9f),plaster);
        Box(parent,"Entrance south wall",new Vector3(0,1.6f,-3.7f),new Vector3(3.4f,3.2f,.2f),lower);
        Box(parent,"Main exit door",new Vector3(0,1.2f,-3.55f),new Vector3(1.8f,2.4f,.08f),dark);
        exitSign=Text(parent,"Exit sign","出口 · 返回校园",new Vector3(0,2.65f,-3.48f),0,.16f,Color.white);
        // Entry appears between the two room rows, facing across the long corridor.
        foreach(var spawn in interior.spawnPoints)if(spawn?.anchor){spawn.anchor.localPosition=new Vector3(0,.06f,-.70f);spawn.anchor.localRotation=Quaternion.Euler(0,90,0);}
        foreach(var exit in interior.exits)if(exit)exit.localPosition=new Vector3(0,.06f,-2.85f);
        interior.exitRadius=1.15f;
        interior.isPlaceholder=false;
        interior.showExitMarkers=false;
        for(int end=-1;end<=1;end+=2)
        {
            float x=end*(layout.stairX+2);
            Box(parent,"Stair end wall",new Vector3(end*25,1.6f,0),new Vector3(.2f,3.2f,layout.corridorWidth),plaster);
            Box(parent,"Stair landing",new Vector3(x,-.08f,0),new Vector3(4,.16f,1.4f),stone);
            // Two short visible flights lead to interaction landings, not to
            // walkable stacked storeys. The fade is an explicit floor transition.
            for(int direction=-1;direction<=1;direction+=2)
            {
                var flight = new GameObject(direction==1?"Ascending flight":"Descending flight");
                flight.transform.SetParent(parent,false);
                (direction==1?upFlights:downFlights).Add(flight);
                // Keep a continuous hidden collision ramp under the visible stair
                // treads. The small gaps between separate tread boxes must never
                // expose the floorless stair shaft to the player capsule.
                var support=Box(flight.transform,"Continuous stair support",new Vector3(x,direction*.68f,direction*1.98f),new Vector3(1.90f,.18f,2.84f),stone);
                // Both flights share a positive world-space rise over +Z: the
                // descending one has negative tread heights at negative Z, so
                // its support still uses the same slope orientation.
                float stairSlope=-Mathf.Atan(.15f/.32f)*Mathf.Rad2Deg;
                support.transform.localRotation=Quaternion.Euler(stairSlope,0,0);
                support.GetComponent<Renderer>().enabled=false;
                for(int step=0;step<8;step++)
                {
                    float top=direction*(step+1)*.15f;
                    Box(flight.transform,"Stair tread",new Vector3(x,top-.075f,direction*(.86f+step*.32f)),new Vector3(2,.15f,.32f),stone);
                    for(int railSide=-1;railSide<=1;railSide+=2)
                        Box(flight.transform,"Stair rail post",new Vector3(x+railSide*1.07f,top+.48f,direction*(.86f+step*.32f)),new Vector3(.055f,.96f,.055f),dark);
                }
                for(int railSide=-1;railSide<=1;railSide+=2)
                {
                    var rail=Box(flight.transform,"Sloping stair handrail",new Vector3(x+railSide*1.07f,.96f+direction*.675f,direction*1.98f),new Vector3(.075f,.075f,2.84f),dark);
                    rail.transform.localRotation=Quaternion.Euler(stairSlope,0,0);
                    var stringer=Box(flight.transform,"Solid stair stringer",new Vector3(x+railSide*.91f,direction>0?.525f:-.675f,direction*1.98f),new Vector3(.24f,.20f,3.12f),stone);
                    stringer.transform.localRotation=Quaternion.Euler(stairSlope,0,0);
                }
                Box(flight.transform,"Safe stair landing",new Vector3(x,direction*1.2f-.08f,direction*3.52f),new Vector3(2.36f,.16f,1.08f),stone);
                var label=Text(parent,"Stair direction",direction==1?"上楼  [E]":"下楼  [E]",new Vector3(x,2.3f,direction*1.75f),direction==1?180:0,.17f,dark.color);
                (direction==1?upSigns:downSigns).Add(label);
                var terminal=new GameObject("Terminal floor closed stairwell");terminal.transform.SetParent(parent,false);
                Box(terminal.transform,"Terminal floor plaster wall",new Vector3(x,1.6f,direction*.84f),new Vector3(4,3.2f,.26f),plaster);
                Box(terminal.transform,"Terminal sage dado",new Vector3(x,.61f,direction*.697f),new Vector3(4,1.22f,.025f),lower);
                Box(terminal.transform,"Terminal skirting",new Vector3(x,.075f,direction*.68f),new Vector3(4,.15f,.035f),dark);
                (direction==1?upBarriers:downBarriers).Add(terminal);
                // Enclose the otherwise floorless strips beside each 2m flight.
                // Solid masonry reaches below the lowest tread and up to the roof.
                foreach(int edge in new[]{-1,1})
                    Box(parent,"Stairwell side wall",new Vector3(x+edge*1.55f,.7f,direction*2.43f),new Vector3(.9f,5f,3.46f),plaster);
                // Block deeper travel beyond the landing during the interaction.
                Box(parent,"Stair boundary",new Vector3(x,.95f,direction*4.1f),new Vector3(4,5.1f,.15f),plaster);
            }
            Box(parent,"Stairwell ceiling",new Vector3(x,3.28f,0),new Vector3(4.2f,.16f,8.4f),white);
            // Secondary solid foundation catches the feet even at tread/landing seams.
            Box(parent,"Stairwell foundation",new Vector3(x,-1.6f,0),new Vector3(4.2f,.20f,8.4f),stone).GetComponent<Renderer>().enabled=false;
            // Close the outside edge; leave the corridor mouth open.
            Box(parent,"Stair outer guard",new Vector3(end*25,1.15f,0),new Vector3(.12f,2.3f,7),lower);
            var sign=Text(parent,"Floor sign","1F",new Vector3(end*24.85f,2.15f,0),end==-1?90:270,.48f,dark.color);
            if(end==-1)westSign=sign;else eastSign=sign;
        }
        // A complete ceiling and warm luminaires keep the space readable indoors.
        Box(parent,"Ceiling",new Vector3(0,layout.wallHeight+.08f,0),new Vector3(50,.16f,layout.corridorWidth),white);
        for(int i=-3;i<=3;i++)
        {
            Box(parent,"Ceiling luminaire",new Vector3(i*6,3.12f,0),new Vector3(1.2f,.07f,.35f),white);
            var light=new GameObject("Warm corridor light").AddComponent<Light>();light.transform.SetParent(parent,false);light.transform.localPosition=new Vector3(i*6,2.9f,0);light.type=LightType.Point;light.range=8;light.intensity=1.3f;light.color=new Color(1,.93f,.78f);light.shadows=LightShadows.None;
        }
        roomInterior=gameObject.AddComponent<DormitoryRoomInterior>();
        roomInterior.Initialize(this,game,corridorVisual,roomDoors);
        DormitoryInteriorRefinement.Hall(corridorVisual,layout);
        RefreshFloor();
    }
    void Wall(Transform parent,float x,float z,float width,Material plaster,Material lower)
    {
        float side=Mathf.Sign(z);
        Box(parent,"Plaster wall",new Vector3(x,1.6f,z),new Vector3(width,3.2f,.20f),plaster);
        Box(parent,"Sage wall dado",new Vector3(x,.61f,z-side*.105f),new Vector3(width,1.22f,.025f),lower);
        Box(parent,"Dado cap",new Vector3(x,1.24f,z-side*.13f),new Vector3(width,.045f,.04f),lower);
        Box(parent,"Skirting",new Vector3(x,.075f,z-side*.14f),new Vector3(width,.15f,.05f),lower);
    }
    Texture2D SurfaceTexture(string hex,bool isWood)
    {
        ColorUtility.TryParseHtmlString(hex,out var baseColor);
        const int size=256;
        var pixels=new Color[size*size];var random=new System.Random(isWood?41:73);
        for(int x=0;x<size;x++)
        {
            float grain=(float)random.NextDouble()*.06f;
            for(int y=0;y<size;y++)
            {
                float variation=isWood?grain:(float)random.NextDouble()*.025f;
                Color color=baseColor*(1-variation);
                if(!isWood&&(x<2||y<2))color=baseColor*.85f;
                if(!isWood&&(x==4||y==4))color=Color.Lerp(baseColor,Color.white,.20f);
                pixels[y*size+x]=color;
            }
        }
        var texture=new Texture2D(size,size,TextureFormat.RGB24,true){wrapMode=TextureWrapMode.Repeat,anisoLevel=4};texture.SetPixels(pixels);texture.Apply();ownedTextures.Add(texture);return texture;
    }
    Material Mat(string hex)
    {
        ColorUtility.TryParseHtmlString(hex,out var color);
        Shader shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
        var material=new Material(shader){color=color};ownedMaterials.Add(material);return material;
    }
    static GameObject Box(Transform parent,string name,Vector3 at,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    TextMesh Text(Transform parent,string name,string value,Vector3 at,float yaw,float size,Color color)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localRotation=Quaternion.Euler(0,yaw+180,0);
        var text=go.AddComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.characterSize=size/10f;text.fontSize=96;text.color=color;
        if(game&&game.interfaceFont){text.font=game.interfaceFont;go.GetComponent<MeshRenderer>().sharedMaterial=game.interfaceFont.material;}
        return text;
    }
    void RefreshFloor()
    {
        for(int i=0;i<numbers.Count;i++)if(numbers[i])numbers[i].text=layout.RoomNumber(Floor,i);
        westSign.text=eastSign.text=Floor+"F";
        foreach(var sign in upSigns)sign.text=Floor<layout.floors?"上楼  [E]":"六楼 · 无上行楼层";
        foreach(var sign in downSigns)sign.text=Floor>1?"下楼  [E]":"一楼 · 无下行楼层";
        foreach(var sign in upSigns)sign.gameObject.SetActive(Floor<layout.floors);
        foreach(var sign in downSigns)sign.gameObject.SetActive(Floor>1);
        foreach(var barrier in upBarriers)barrier.SetActive(Floor==layout.floors);
        foreach(var barrier in downBarriers)barrier.SetActive(Floor==1);
        foreach(var flight in upFlights)flight.SetActive(Floor<layout.floors);
        foreach(var flight in downFlights)flight.SetActive(Floor>1);
        foreach(var exit in interior.exits)if(exit)exit.gameObject.SetActive(Floor==1);
        exitSign.text=Floor==1?"出口 · 返回校园":"请从两端楼梯返回一楼";
    }
    public string Prompt
    {
        get
        {
            if(roomInterior&&roomInterior.Inside)return roomInterior.Prompt;
            if(roomInterior&&!string.IsNullOrEmpty(roomInterior.Prompt))return roomInterior.Prompt;
            if(!game||game.paused||Transitioning||game.UiBlocking)return "";
            Vector3 p=transform.InverseTransformPoint(game.player.position);
            if(Mathf.Abs(p.x)<layout.stairX-.8f||Mathf.Abs(p.x)>25||Mathf.Abs(p.z)>3.5f)return "";
            if(p.z>=.35f)return Floor<layout.floors?"[ E ] 上楼 · "+Floor+"F → "+(Floor+1)+"F":"已到六楼 · 这里是顶层";
            if(p.z<=-.35f)return Floor>1?"[ E ] 下楼 · "+Floor+"F → "+(Floor-1)+"F":"这里是一楼 · 从中央门厅返回校园";
            return "楼梯口 · 向前上楼，向后下楼";
        }
    }
    public bool TryInteract()
    {
        if(Transitioning)return true;
        if(roomInterior&&roomInterior.TryInteract())return true;
        if(string.IsNullOrEmpty(Prompt))return false;
        Vector3 p=transform.InverseTransformPoint(game.player.position);
        if(Mathf.Abs(p.z)<.35f)return true;
        int next=Floor+(p.z>0?1:-1);
        if(next<1||next>layout.floors){game.Notify(Prompt);return true;}
        StartCoroutine(ChangeFloor(next,p.x<0?-1:1));return true;
    }
    IEnumerator ChangeFloor(int next,int side)
    {
        Transitioning=true;restoringUi=game.UiBlocking;game.SetUiCursor(true);
        transitionLabel="沿"+(side<0?"西":"东")+"侧楼梯前往 "+next+"F";
        for(float t=0;t<.35f;t+=Time.unscaledDeltaTime){fade=Mathf.Clamp01(t/.35f);yield return null;}
        fade=1;Floor=next;RefreshFloor();
        var controller=game.Interiors.IndoorController;bool enabledBefore=controller&&controller.enabled;if(controller)controller.enabled=false;
        game.SetTravelPose(transform.TransformPoint(new Vector3(side*(layout.stairX-1.6f),.06f,0)),side<0?90:270,10);
        Physics.SyncTransforms();if(controller)controller.enabled=enabledBefore;
        FloorChanged?.Invoke(Floor);yield return new WaitForSecondsRealtime(.18f);
        for(float t=.35f;t>0;t-=Time.unscaledDeltaTime){fade=Mathf.Clamp01(t/.35f);yield return null;}
        fade=0;Transitioning=false;game.SetUiCursor(restoringUi);
    }
    public void RestoreFloor(int floor) { Floor=Mathf.Clamp(floor,1,layout.floors);RefreshFloor(); }
    // Keep recovery in the active room/floor; the generic entrance spawn is hidden
    // while a bedroom is active and is therefore not a safe recovery destination.
    public bool RecoverIfUnsafe()
    {
        if(!game||Transitioning)return false;
        if(InsideRoom)return roomInterior.RecoverIfUnsafe();
        Vector3 p=transform.InverseTransformPoint(game.player.position);
        if(p.y>=-2.8f&&p.y<5&&Mathf.Abs(p.x)<25.8f&&Mathf.Abs(p.z)<4.7f)return false;
        var cc=game.Interiors.IndoorController;bool wasEnabled=cc&&cc.enabled;if(cc)cc.enabled=false;
        game.SetTravelPose(transform.TransformPoint(new Vector3(0,.06f,-.7f)),transform.eulerAngles.y+90);
        Physics.SyncTransforms();if(cc)cc.enabled=wasEnabled;game.Notify("已回到本层安全位置。");return true;
    }
    public IEnumerator RestoreRoom(int index)
    {
        if(roomInterior) yield return roomInterior.RestoreRoom(index);
    }
    void OnDisable(){if(Transitioning&&game){Transitioning=false;fade=0;game.SetUiCursor(restoringUi);}}
    void OnDestroy(){foreach(var material in ownedMaterials)if(material)Destroy(material);foreach(var texture in ownedTextures)if(texture)Destroy(texture);}
    void OnGUI()
    {
        if(!Transitioning)return;
        var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;GUI.matrix=Matrix4x4.identity;GUI.depth=-90;
        GUI.color=new Color(.08f,.11f,.10f,fade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=new Color(1,1,1,fade);
        GUI.Label(new Rect(0,Screen.height/2-30,Screen.width,60),transitionLabel,new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,font=game.interfaceFont,fontSize=25});GUI.color=color;GUI.matrix=matrix;GUI.depth=depth;
    }
}
