using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Shared generated room model; build geometry only for the room the player enters.
public sealed class DormitoryRoomInterior : MonoBehaviour
{
    public sealed class Doorway { public int Index; public float X; public int Side; public Transform Hinge; public Quaternion ClosedRotation; }
    DormitoryCorridor hallway;
    CampusGame game;
    Transform hallwayRoot, roomRoot, bedroom;
    Doorway[] doors;
    readonly List<Material> materials = new List<Material>();
    readonly List<Texture2D> textures = new List<Texture2D>();
    Doorway selected;
    Transform roomDoorHinge;
    bool inside, busy;
    public bool Inside => inside;
    public string RoomNumber => hallway ? hallway.CurrentRoomNumber : "";

    public string Prompt
    {
        get
        {
            if (!game || game.paused || game.UiBlocking || game.Interiors.Busy || busy) return "";
            if (inside)
            {
                Vector3 local=bedroom.InverseTransformPoint(game.player.position);
                return local.z<2f&&Mathf.Abs(local.x)<1.25f ? "[ E ] 返回宿舍走廊 · "+RoomNumber : "";
            }
            selected=NearestDoor();
            return selected==null ? "" : "[ E ] 进入宿舍 · "+hallway.Layout.RoomNumber(hallway.Floor,selected.Index);
        }
    }

    public void Initialize(DormitoryCorridor corridor,CampusGame campus,Transform corridorVisual,IEnumerable<Doorway> doorways)
    {
        hallway=corridor;game=campus;hallwayRoot=corridorVisual;doors=new List<Doorway>(doorways).ToArray();
        roomRoot=new GameObject("Shared dorm bedroom · active room only").transform;
        roomRoot.SetParent(transform,false);roomRoot.gameObject.SetActive(false);
    }

    Doorway NearestDoor()
    {
        if(!game||hallway.InsideRoom||hallway.Transitioning||hallway.Layout==null)return null;
        Vector3 p=transform.InverseTransformPoint(game.player.position);
        if(Mathf.Abs(p.y)>1.2f||Mathf.Abs(p.z)>3.2f||Mathf.Abs(p.x)>25)return null;
        Doorway nearest=null;float best=2.5f*2.5f;
        foreach(var door in doors)
        {
            Vector3 target=transform.TransformPoint(new Vector3(door.X,p.y,door.Side*(hallway.Layout.corridorWidth*.5f-.15f)));
            Vector3 delta=target-game.player.position;float distance=delta.sqrMagnitude;
            if(distance>=best||Vector3.Dot(game.player.forward,new Vector3(delta.x,0,delta.z).normalized)<.15f)continue;
            best=distance;nearest=door;
        }
        return nearest;
    }

    public bool TryInteract()
    {
        if(busy)return true;
        if(inside){if(string.IsNullOrEmpty(Prompt))return false;StartCoroutine(Leave());return true;}
        selected=NearestDoor();if(selected==null)return false;
        StartCoroutine(Enter(selected));return true;
    }

    IEnumerator Enter(Doorway door)
    {
        busy=true;selected=door;yield return AnimateDoor(door,true,.32f);
        hallway.SetRoomState(true,door.Index,door.Side);
        BuildBedroom(door);hallwayRoot.gameObject.SetActive(false);roomRoot.gameObject.SetActive(true);
        // BuildBedroom keeps its root inactive while constructing geometry;
        // explicitly activate the room before switching the player and camera.
        bedroom.gameObject.SetActive(true);
        bedroom.position=transform.TransformPoint(new Vector3(door.X,0,door.Side*(hallway.Layout.corridorWidth*.5f-.15f)));
        bedroom.rotation=transform.rotation*Quaternion.Euler(0,door.Side>0?0:180,0);
        roomDoorHinge.localRotation=Quaternion.Euler(0,90,0);
        game.SetTravelPose(bedroom.TransformPoint(new Vector3(0,.06f,1.15f)),bedroom.eulerAngles.y,12);
        Physics.SyncTransforms();inside=true;
        yield return AnimateRoomDoor(false,.28f);
        game.Notify("宿舍 "+RoomNumber+" · 阳台朝向中心花园；按 E 返回走廊。");
        yield return new WaitForSecondsRealtime(.18f);busy=false;
    }

    IEnumerator Leave()
    {
        busy=true;selected=NearestRoomDoor()??doors[Mathf.Clamp(hallway.CurrentRoomIndex,0,doors.Length-1)];
        yield return AnimateRoomDoor(true,.25f);
        roomRoot.gameObject.SetActive(false);hallwayRoot.gameObject.SetActive(true);inside=false;
        hallway.SetRoomState(false,selected.Index,selected.Side);
        Vector3 outside=transform.TransformPoint(new Vector3(selected.X,.06f,selected.Side*(hallway.Layout.corridorWidth*.5f-.73f)));
        float heading=(transform.rotation*Quaternion.Euler(0,selected.Side>0?0:180,0)).eulerAngles.y;
        game.SetTravelPose(outside,heading,12);Physics.SyncTransforms();
        yield return AnimateDoor(selected,false,.25f);game.Notify("已返回 "+RoomNumber+" 走廊。");busy=false;
    }

    IEnumerator AnimateRoomDoor(bool open,float duration)
    {
        if(!roomDoorHinge)yield break;
        Quaternion from=roomDoorHinge.localRotation;
        Quaternion to=Quaternion.Euler(0,open?90:0,0);
        for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
        {
            roomDoorHinge.localRotation=Quaternion.Slerp(from,to,Mathf.SmoothStep(0,1,t/duration));
            yield return null;
        }
        roomDoorHinge.localRotation=to;
        Physics.SyncTransforms();
    }

    Doorway NearestRoomDoor()
    {
        if(!game||!bedroom)return null;
        Vector3 local=bedroom.InverseTransformPoint(game.player.position);
        return local.z<=2.25f&&Mathf.Abs(local.x)<1.35f?selected:null;
    }

    IEnumerator AnimateDoor(Doorway door,bool open,float duration)
    {
        if(!door.Hinge)yield break;
        Quaternion from=door.Hinge.localRotation;
        Quaternion to=door.ClosedRotation*Quaternion.Euler(0,(door.Side>0?-1:1)*(open?102:0),0);
        for(float t=0;t<duration;t+=Time.unscaledDeltaTime){door.Hinge.localRotation=Quaternion.Slerp(from,to,Mathf.SmoothStep(0,1,t/duration));yield return null;}
        door.Hinge.localRotation=to;
    }

    void BuildBedroom(Doorway door)
    {
        if(bedroom)Destroy(bedroom.gameObject);
        foreach(var material in materials)if(material)Destroy(material);
        foreach(var texture in textures)if(texture)Destroy(texture);
        materials.Clear();textures.Clear();
        bedroom=new GameObject("Dorm room "+hallway.Layout.RoomNumber(hallway.Floor,door.Index)).transform;
        bedroom.SetParent(roomRoot,false);
        var floor=Mat("#d5d2c6");floor.mainTexture=FloorTexture();
        var wall=Mat("#f2eee2");var green=Mat("#9ba99c");var wood=Mat("#aa7950");wood.mainTexture=WoodTexture();
        var dark=Mat("#34483e");var trim=Mat("#d9c7a0");var white=Mat("#faf7e9");
        var linen=Mat("#ebe5d8");var quilt=Mat("#82978a");
        Box(bedroom,"Bedroom floor",new Vector3(0,-.12f,5f),new Vector3(6.2f,.24f,10f),floor);
        Box(bedroom,"Balcony floor",new Vector3(0,-.12f,11.1f),new Vector3(6.2f,.24f,2.2f),floor);
        // A sealed room shell keeps the player on the modeled floor, including
        // the short landing outside the bedroom door while the corridor is hidden.
        Box(bedroom,"Bedroom entry landing",new Vector3(0,-.12f,-1.55f),new Vector3(6.2f,.24f,3.1f),floor);
        Wall(bedroom,-2.70f,0,.8f,wall,green);Wall(bedroom,2.70f,0,.8f,wall,green);
        Box(bedroom,"Entry front wall left",new Vector3(-1.95f,1.6f,0),new Vector3(2.3f,3.2f,.18f),wall);
        Box(bedroom,"Entry front wall right",new Vector3(1.95f,1.6f,0),new Vector3(2.3f,3.2f,.18f),wall);
        Box(bedroom,"Entry lintel",new Vector3(0,2.95f,0),new Vector3(1.55f,.50f,.18f),wall);
        Box(bedroom,"Bedroom entry landing end wall",new Vector3(0,1.6f,-3.12f),new Vector3(6.2f,3.2f,.18f),wall);
        Box(bedroom,"Bedroom entry landing left boundary",new Vector3(-3.08f,1.6f,-1.55f),new Vector3(.16f,3.2f,3.1f),wall);
        Box(bedroom,"Bedroom entry landing right boundary",new Vector3(3.08f,1.6f,-1.55f),new Vector3(.16f,3.2f,3.1f),wall);
        Wall(bedroom,-3.08f,5.2f,10.4f,wall,green);Wall(bedroom,3.08f,5.2f,10.4f,wall,green);
        Box(bedroom,"Bedroom ceiling",new Vector3(0,3.28f,5.8f),new Vector3(6.2f,.16f,11.6f),white);
        Box(bedroom,"Entry door jamb left",new Vector3(-.72f,1.15f,.03f),new Vector3(.12f,2.3f,.18f),dark);
        Box(bedroom,"Entry door jamb right",new Vector3(.72f,1.15f,.03f),new Vector3(.12f,2.3f,.18f),dark);
        Box(bedroom,"Entry door frame top",new Vector3(0,2.30f,.03f),new Vector3(1.56f,.12f,.18f),dark);
        roomDoorHinge=new GameObject("Bedroom entrance door hinge").transform;
        roomDoorHinge.SetParent(bedroom,false);roomDoorHinge.localPosition=new Vector3(.66f,0,.15f);
        Box(roomDoorHinge,"Bedroom entrance door leaf",new Vector3(-.66f,1.1f,0),new Vector3(1.32f,2.16f,.08f),wood);
        Box(bedroom,"Entry door plaque",new Vector3(0,2.53f,.04f),new Vector3(.76f,.28f,.06f),dark);
        Label(bedroom,"Room title","宿舍 "+RoomNumber,new Vector3(0,2.53f,-.01f),180,.15f,Color.white);
        BuildToilet(bedroom,wall,green,white,dark);
        BuildVanity(bedroom,wood,white,trim);
        BuildLoftDesk(bedroom,-1.8f,4.3f,wood,dark,trim,white,linen,quilt);
        BuildLoftDesk(bedroom, 1.8f,4.3f,wood,dark,trim,white,linen,quilt);
        BuildLoftDesk(bedroom,-1.8f,7.55f,wood,dark,trim,white,linen,quilt);
        BuildLoftDesk(bedroom, 1.8f,7.55f,wood,dark,trim,white,linen,quilt);
        BuildBalcony(bedroom,dark,trim);
        for(int i=0;i<3;i++)Box(bedroom,"Ceiling light",new Vector3(0,3.12f,2.7f+i*3f),new Vector3(.9f,.06f,.22f),white);
        bedroom.gameObject.SetActive(false);
    }

    void BuildToilet(Transform p,Material wall,Material green,Material white,Material dark)
    {
        float x=-2.1f,z=2.05f;
        Box(p,"Toilet stall left partition",new Vector3(x-.88f,1.2f,z),new Vector3(.12f,2.4f,2.25f),wall);
        Box(p,"Toilet stall right partition",new Vector3(x+.88f,1.2f,z),new Vector3(.12f,2.4f,2.25f),wall);
        Box(p,"Toilet stall rear partition",new Vector3(x,1.2f,z+1.12f),new Vector3(1.88f,2.4f,.12f),wall);
        // Split front screen leaves a comfortable doorway while keeping the WC private.
        Box(p,"Toilet stall entrance panel left",new Vector3(x-.78f,1.2f,z-1.08f),new Vector3(.32f,2.4f,.12f),green);
        Box(p,"Toilet stall entrance panel right",new Vector3(x+.78f,1.2f,z-1.08f),new Vector3(.32f,2.4f,.12f),green);
        Box(p,"Toilet cistern",new Vector3(x,.72f,z+.64f),new Vector3(.66f,.78f,.20f),white);
        Cylinder(p,"Toilet tank cap",new Vector3(x,.12f,z+.48f),new Vector3(.73f,.18f,.83f),white);
        Cylinder(p,"Toilet bowl",new Vector3(x,.34f,z+.20f),new Vector3(.70f,.52f,.91f),white);
        Cylinder(p,"Toilet seat",new Vector3(x,.61f,z+.18f),new Vector3(.72f,.12f,.93f),dark);
        Box(p,"Toilet paper holder",new Vector3(x+.76f,.94f,z+.54f),new Vector3(.18f,.16f,.16f),dark);
    }

    void BuildVanity(Transform p,Material wood,Material white,Material trim)
    {
        float x=2.1f,z=1.65f;
        Box(p,"Washbasin cabinet",new Vector3(x,.46f,z),new Vector3(1.45f,.88f,.76f),wood);
        Box(p,"Vanity countertop",new Vector3(x,.94f,z),new Vector3(1.58f,.12f,.83f),trim);
        Cylinder(p,"Washbasin",new Vector3(x,.99f,z),new Vector3(.76f,.10f,.53f),white);
        Box(p,"Washbasin faucet",new Vector3(x,1.13f,z+.27f),new Vector3(.08f,.28f,.08f),trim);
        Box(p,"Bathroom mirror frame",new Vector3(x,1.92f,z+.42f),new Vector3(1.3f,1.25f,.08f),trim);
        Box(p,"Bathroom mirror",new Vector3(x,1.92f,z+.365f),new Vector3(1.16f,1.10f,.025f),Mat("#bed0ca"));
        Label(p,"Handwashing note","洗漱区",new Vector3(x,2.78f,z+.35f),180,.15f,new Color(.2f,.28f,.23f));
    }

    void BuildLoftDesk(Transform p,float x,float z,Material wood,Material dark,Material trim,Material white,Material linen,Material quilt)
    {
        const float bedY=2.10f;
        // A full single-size loft frame with a safer rail and readable ladder.
        Box(p,"Loft bed frame",new Vector3(x,bedY-.12f,z),new Vector3(1.48f,.20f,2.42f),dark);
        Box(p,"Loft bed mattress",new Vector3(x,bedY,z),new Vector3(1.36f,.20f,2.24f),linen);
        Box(p,"Loft bed quilt",new Vector3(x,bedY+.12f,z-.32f),new Vector3(1.30f,.07f,1.22f),quilt);
        Box(p,"Loft pillow",new Vector3(x,bedY+.17f,z+.86f),new Vector3(.82f,.20f,.42f),white);
        Box(p,"Loft headboard",new Vector3(x,bedY+.15f,z+1.12f),new Vector3(1.40f,.42f,.09f),wood);
        for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)
            Box(p,"Loft bed post",new Vector3(x+sx*.64f,1.34f,z+sz*1.08f),new Vector3(.11f,2.68f,.11f),wood);
        Box(p,"Loft guard rail",new Vector3(x,2.43f,z-.84f),new Vector3(1.36f,.18f,.10f),wood);
        Box(p,"Loft guard rail",new Vector3(x+(x<0?1:-1)*.64f,2.40f,z),new Vector3(.10f,.18f,2.12f),wood);
        float lx=x+(x<0?1:-1)*.78f;
        for(int side=-1;side<=1;side+=2)
            Box(p,"Bed ladder rail",new Vector3(lx+side*.22f,1.08f,z-.04f),new Vector3(.075f,2.16f,.085f),wood);
        for(int rung=0;rung<7;rung++)
            Box(p,"Bed ladder rung",new Vector3(lx,.18f+rung*.31f,z-.04f),new Vector3(.48f,.075f,.10f),trim);

        // A deeper writing surface, modest drawer unit and four solid chair legs.
        Box(p,"Study desk top",new Vector3(x,.82f,z),new Vector3(1.58f,.14f,1.48f),wood);
        for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)
            Box(p,"Study desk leg",new Vector3(x+sx*.68f,.40f,z+sz*.61f),new Vector3(.085f,.78f,.085f),dark);
        Box(p,"Study desk drawer unit",new Vector3(x,.62f,z-.54f),new Vector3(.92f,.30f,.36f),wood);
        for(int drawer=0;drawer<2;drawer++)
        {
            Box(p,"Desk drawer front",new Vector3(x,.54f+drawer*.15f,z-.735f),new Vector3(.84f,.11f,.035f),trim);
            Box(p,"Desk drawer pull",new Vector3(x,.54f+drawer*.15f,z-.77f),new Vector3(.14f,.025f,.025f),dark);
        }
        float chairZ=z-1.10f;
        Box(p,"Desk chair seat",new Vector3(x,.48f,chairZ),new Vector3(.76f,.14f,.70f),dark);
        Box(p,"Desk chair back",new Vector3(x,.94f,chairZ-.30f),new Vector3(.76f,.82f,.13f),wood);
        for(int sx=-1;sx<=1;sx+=2)for(int sz=-1;sz<=1;sz+=2)
            Box(p,"Desk chair leg",new Vector3(x+sx*.30f,.23f,chairZ+sz*.26f),new Vector3(.07f,.46f,.07f),dark);
        Box(p,"Desk book stack",new Vector3(x-.48f,.96f,z+.43f),new Vector3(.42f,.12f,.46f),trim);
        Box(p,"Desk lamp base",new Vector3(x+.48f,.92f,z-.34f),new Vector3(.28f,.07f,.26f),dark);
        Box(p,"Desk lamp stem",new Vector3(x+.48f,1.14f,z-.34f),new Vector3(.05f,.40f,.05f),trim);
        Box(p,"Desk lamp shade",new Vector3(x+.48f,1.36f,z-.34f),new Vector3(.38f,.20f,.34f),white);
        Label(p,"Study motto","好好学习\n天天向上",new Vector3(x,1.58f,z+.91f),0,.16f,new Color(.2f,.3f,.24f));
    }

    void BuildBalcony(Transform p,Material dark,Material trim)
    {
        var glass=Mat("#d9e6df");glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.color=new Color(.78f,.88f,.84f,.16f);glass.renderQueue=3000;
        Box(p,"Balcony window frame left",new Vector3(-2.82f,1.68f,9.62f),new Vector3(.12f,2.75f,.12f),dark);
        Box(p,"Balcony window frame right",new Vector3(2.82f,1.68f,9.62f),new Vector3(.12f,2.75f,.12f),dark);
        Box(p,"Balcony sliding door frame top",new Vector3(0,3.04f,9.62f),new Vector3(5.55f,.12f,.12f),dark);
        Box(p,"Balcony sliding glass left panel",new Vector3(-2f,1.63f,9.66f),new Vector3(2f,2.68f,.035f),glass);
        Box(p,"Balcony sliding glass right panel",new Vector3(2f,1.63f,9.66f),new Vector3(2f,2.68f,.035f),glass);
        Box(p,"Balcony threshold",new Vector3(0,.04f,9.66f),new Vector3(5.5f,.08f,.26f),trim);
        // Close the balcony at the picture plane: the photo is scenery, and the
        // invisible collider behind it prevents walking through into empty space.
        var gardenWall=Box(p,"Balcony garden collision wall",new Vector3(0,1.65f,11.66f),new Vector3(6.0f,3.3f,.16f),dark);
        gardenWall.GetComponent<Renderer>().enabled=false;
        Box(p,"Balcony side safety wall left",new Vector3(-3.02f,1.5f,10.85f),new Vector3(.16f,3.0f,1.8f),dark).GetComponent<Renderer>().enabled=false;
        Box(p,"Balcony side safety wall right",new Vector3(3.02f,1.5f,10.85f),new Vector3(.16f,3.0f,1.8f),dark).GetComponent<Renderer>().enabled=false;
        Box(p,"Balcony rail",new Vector3(0,1.04f,10.96f),new Vector3(5.9f,.08f,.1f),dark);
        for(int i=-12;i<=12;i++)Box(p,"Balcony baluster",new Vector3(i*.245f,.53f,10.96f),new Vector3(.055f,1f,.07f),dark);
        var garden=Resources.Load<Texture2D>("Onboarding/central-garden")??GardenPanorama();
        var surface=GameObject.CreatePrimitive(PrimitiveType.Quad);surface.name="Balcony view · campus central garden photo";surface.transform.SetParent(p,false);surface.transform.localPosition=new Vector3(0,1.65f,11.55f);surface.transform.localScale=new Vector3(5.95f,3.34f,1);
        var picture=Mat("#ffffff");picture.mainTexture=garden;picture.mainTextureScale=new Vector2(1f,.75f);picture.mainTextureOffset=new Vector2(0,.125f);if(picture.HasProperty("_Cull"))picture.SetFloat("_Cull",0);surface.GetComponent<MeshRenderer>().sharedMaterial=picture;Destroy(surface.GetComponent<Collider>());
        Label(p,"Balcony view title","中心花园",new Vector3(0,3.32f,11.52f),180,.20f,Color.white);
        Box(p,"Balcony planter",new Vector3(-2.25f,.38f,10.55f),new Vector3(.9f,.42f,.48f),trim);
        for(int i=0;i<7;i++)Cylinder(p,"Balcony flowers",new Vector3(-2.55f+i*.17f,.76f,10.52f),new Vector3(.12f,.28f,.12f),Mat(i%2==0?"#d293a0":"#e2c36f"));
    }

    Texture2D GardenPanorama()
    {
        const int w=1024,h=512;var tex=new Texture2D(w,h,TextureFormat.RGB24,false){name="Illustrated central-campus-garden panorama",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        var pixels=new Color[w*h];Color sky=new Color(.63f,.76f,.78f),lawn=new Color(.28f,.42f,.29f);
        for(int y=0;y<h;y++){Color c=Color.Lerp(sky,lawn,Mathf.SmoothStep(.43f,.58f,y/(float)h));for(int x=0;x<w;x++)pixels[y*w+x]=c;}
        var rng=new System.Random(2049);
        for(int row=0;row<5;row++)
        {
            int x0=30+row*204;RectFill(pixels,w,h,x0,215,x0+138,313,new Color(.62f,.60f,.50f));RectFill(pixels,w,h,x0+8,233,x0+130,306,new Color(.71f,.72f,.66f));
            for(int window=0;window<5;window++)RectFill(pixels,w,h,x0+14+window*23,251,x0+27+window*23,286,new Color(.37f,.53f,.56f));
            for(int tree=0;tree<7;tree++){int tx=x0-15+tree*24,ty=295+rng.Next(-10,26);RectFill(pixels,w,h,tx-3,ty-55,tx+3,ty+5,new Color(.32f,.25f,.19f));EllipseFill(pixels,w,h,tx,ty+8,18+rng.Next(0,12),18+rng.Next(0,17),new Color(.22f,.37f,.26f));}
        }
        PolygonFill(pixels,w,h,new[]{new Vector2(250,0),new Vector2(774,0),new Vector2(558,314),new Vector2(475,314)},new Color(.72f,.67f,.54f));
        EllipseFill(pixels,w,h,w/2,321,120,42,new Color(.80f,.74f,.58f));EllipseFill(pixels,w,h,w/2,321,91,28,new Color(.24f,.41f,.25f));EllipseFill(pixels,w,h,w/2,321,37,13,new Color(.69f,.71f,.63f));
        for(int i=0;i<120;i++){float a=(float)(rng.NextDouble()*Math.PI*2),r=43+(float)rng.NextDouble()*42;int x=Mathf.RoundToInt(w*.5f+Mathf.Cos(a)*r),y=Mathf.RoundToInt(321+Mathf.Sin(a)*r*.42f);Color c=i%3==0?new Color(.88f,.69f,.70f):i%3==1?new Color(.92f,.81f,.52f):new Color(.72f,.76f,.64f);EllipseFill(pixels,w,h,x,y,2,2,c);}
        tex.SetPixels(pixels);tex.Apply(false,false);textures.Add(tex);return tex;
    }
    static void RectFill(Color[] p,int w,int h,int x0,int y0,int x1,int y1,Color c){for(int y=Mathf.Clamp(y0,0,h);y<Mathf.Clamp(y1,0,h);y++)for(int x=Mathf.Clamp(x0,0,w);x<Mathf.Clamp(x1,0,w);x++)p[y*w+x]=c;}
    static void EllipseFill(Color[] p,int w,int h,int cx,int cy,int rx,int ry,Color c){for(int y=Mathf.Max(0,cy-ry);y<Mathf.Min(h,cy+ry+1);y++)for(int x=Mathf.Max(0,cx-rx);x<Mathf.Min(w,cx+rx+1);x++){float dx=(x-cx)/(float)Mathf.Max(1,rx),dy=(y-cy)/(float)Mathf.Max(1,ry);if(dx*dx+dy*dy<=1)p[y*w+x]=c;}}
    static void PolygonFill(Color[] p,int w,int h,Vector2[] v,Color c){float a=v[0].y,b=a;foreach(var q in v){a=Mathf.Min(a,q.y);b=Mathf.Max(b,q.y);}for(int y=Mathf.Max(0,(int)a);y<Mathf.Min(h,(int)b);y++)for(int x=0;x<w;x++){bool hit=false;for(int i=0,j=v.Length-1;i<v.Length;j=i++)if((v[i].y>y)!=(v[j].y>y)&&x<(v[j].x-v[i].x)*(y-v[i].y)/(v[j].y-v[i].y)+v[i].x)hit=!hit;if(hit)p[y*w+x]=c;}}

    Texture2D FloorTexture(){const int n=256;var t=new Texture2D(n,n,TextureFormat.RGB24,true){name="Dorm room stone tiles",wrapMode=TextureWrapMode.Repeat};var p=new Color[n*n];for(int y=0;y<n;y++)for(int x=0;x<n;x++){Color c=new Color(.76f,.75f,.69f);if(x%64<2||y%64<2)c*=.78f;p[y*n+x]=c;}t.SetPixels(p);t.Apply();textures.Add(t);return t;}
    Texture2D WoodTexture(){const int n=256;var t=new Texture2D(n,n,TextureFormat.RGB24,true){name="Dorm room oak veneer",wrapMode=TextureWrapMode.Repeat};var p=new Color[n*n];var r=new System.Random(902);for(int x=0;x<n;x++){float d=(float)r.NextDouble()*.09f;for(int y=0;y<n;y++)p[y*n+x]=Color.Lerp(new Color(.43f,.28f,.17f),new Color(.67f,.48f,.30f),d*4);}t.SetPixels(p);t.Apply();textures.Add(t);return t;}
    Material Mat(string hex){ColorUtility.TryParseHtmlString(hex,out var c);var m=new Material(Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard")){color=c};materials.Add(m);return m;}
    static GameObject Box(Transform p,string n,Vector3 at,Vector3 sz,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localScale=sz;g.GetComponent<Renderer>().sharedMaterial=m;return g;}
    static GameObject Cylinder(Transform p,string n,Vector3 at,Vector3 sz,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localScale=sz;g.GetComponent<Renderer>().sharedMaterial=m;return g;}
    TextMesh Label(Transform p,string n,string value,Vector3 at,float yaw,float size,Color color){var g=new GameObject(n);g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localRotation=Quaternion.Euler(0,yaw+180,0);var t=g.AddComponent<TextMesh>();t.text=value;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.characterSize=size/10f;t.fontSize=96;t.color=color;if(game.interfaceFont){t.font=game.interfaceFont;g.GetComponent<MeshRenderer>().sharedMaterial=game.interfaceFont.material;}return t;}
    void Wall(Transform p,float x,float z,float depth,Material wall,Material lower){Box(p,"Bedroom plaster wall",new Vector3(x,1.6f,z),new Vector3(.2f,3.2f,depth),wall);Box(p,"Bedroom green dado",new Vector3(x-Mathf.Sign(x)*.11f,.62f,z),new Vector3(.035f,1.24f,depth),lower);Box(p,"Bedroom skirting",new Vector3(x-Mathf.Sign(x)*.14f,.08f,z),new Vector3(.05f,.16f,depth),lower);}
    void OnDestroy(){foreach(var m in materials)if(m)Destroy(m);foreach(var t in textures)if(t)Destroy(t);}
}
