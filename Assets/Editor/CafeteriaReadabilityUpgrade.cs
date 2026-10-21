using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Idempotent migration of the saved scene; also applied by the cafeteria builder.
public static class CafeteriaReadabilityUpgrade
{
    const string ScenePath = "Assets/Interiors/Scenes/cafeteria.unity";
    const string Output = "output/食堂文字楼梯验证";
    const float H = 4.3f, FlightRun = 3.08f, Landing = 1.2f;
    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Interiors/Materials/CafeteriaModern/Mat_" + name + ".mat");
    static GameObject Box(Transform root, string name, Vector3 pos, Vector3 size, Material mat, bool solid = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(root, false); go.transform.localPosition = pos; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }
    static void Remove(Transform parent, string name)
    {
        var old = parent.Find(name); if (old) Object.DestroyImmediate(old.gameObject);
    }
    public static void Apply(Transform root)
    {
        var multi = root.Find("Cafeteria_MultiFloor_Interior");
        if (!multi) throw new InvalidOperationException("Cafeteria architecture not found");
        // Keep the existing stairwell footprints, exits, furniture and ordering anchors.
        Remove(multi, "00_Vertical_Circulation"); Remove(root, "Cafeteria_Open_Stair_Rails");
        var stairs = Group(multi, "00_Vertical_Circulation");
        for (int storey = 0; storey < 2; storey++)
        {
            BuildStair(stairs, 1, storey, new Vector3(-10.2f, storey * H, -12.5f), -90, 2.8f);
            BuildStair(stairs, 2, storey, new Vector3(6f, storey * H, -8.8f), 180, 2.4f);
        }
        RebuildDecks(multi);
        foreach (var text in root.GetComponentsInChildren<TextMesh>(true)) ResizeText(text);
        InteriorLabelStairFix.ConfigureLabels(root, 3);
    }
    static void BuildStair(Transform parent, int id, int floor, Vector3 origin, float yaw, float width)
    {
        var t = Group(parent, $"Stairs_{id}_{floor+1}F_to_{floor+2}F");
        t.localPosition = origin; t.localRotation = Quaternion.Euler(0, yaw, 0);
        float lane = width / 2 - .18f, offset = width / 4, rise = H / 22;
        var tread = Mat("Wood_ScandiOak"); var riser = Mat("Wall_DarkCharcoal"); var wall = Mat("Wall_Microcement");
        Box(t, "Half_Landing", new Vector3(0,H/2-.075f,FlightRun+Landing/2),new Vector3(width-.12f,.15f,Landing),Mat("Floor_DarkBasalt"));
        for(int i=0;i<11;i++)
        {
            float y=(i+1)*rise;
            Box(t,"B_Riser_"+i,new Vector3(offset,y-rise/2,(i+.5f)*.28f),new Vector3(lane,rise,.28f),riser);
            Box(t,"B_Tread_"+i,new Vector3(offset,y-.015f,(i+.5f)*.28f),new Vector3(lane+.02f,.03f,.29f),tread);
            Box(t,"A_Riser_"+i,new Vector3(-offset,H/2+y-rise/2,FlightRun-(i+.5f)*.28f),new Vector3(lane,rise,.28f),riser);
            Box(t,"A_Tread_"+i,new Vector3(-offset,H/2+y-.015f,FlightRun-(i+.5f)*.28f),new Vector3(lane+.02f,.03f,.29f),tread);
        }
        // Same open-front, two-flight enclosure as the library, scaled to this well.
        foreach(float side in new[]{-1f,1f})
            Box(t,"Stair_Wall_"+side,new Vector3(side*(width/2-.03f),H/2,(FlightRun+Landing)/2),new Vector3(.06f,H,FlightRun+Landing),wall);
        Box(t,"Stair_Back_Wall",new Vector3(0,H/2,FlightRun+Landing-.03f),new Vector3(width,H,.06f),wall);
        Box(t,"Spine_Wall",new Vector3(0,H/2,FlightRun/2),new Vector3(.12f,H,FlightRun-.1f),wall);
        Rail(t, "B", width/2-.12f, 0,0,FlightRun,H/2);
        Rail(t, "A", -width/2+.12f,H/2,FlightRun,0,H/2);
        Box(t,"Sign_Entry",new Vector3(0,2.75f,-.14f),new Vector3(width-.1f,.65f,.06f),Mat("Sign_BlackBox"),false);
        var label=Group(t,"Text_Stair").gameObject.AddComponent<TextMesh>();
        label.transform.localPosition=new Vector3(0,2.75f,-.18f);
        label.text=$"楼梯 {id} · {floor+1}F → {floor+2}F\n平台转弯上楼";
        label.font=Resources.Load<Font>("CampusFont"); label.fontSize=32; label.characterSize=.06f;
        label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center; label.color=Color.white;
        label.GetComponent<Renderer>().sharedMaterial=label.font.material;
    }
    static void Rail(Transform root,string name,float x,float y,float z0,float z1,float rise)
    {
        Vector3 a=new Vector3(x,y+.95f,z0), b=new Vector3(x,y+rise+.95f,z1);
        var bar=Box(root,"Rail_"+name,(a+b)/2,new Vector3(.045f,.045f,Vector3.Distance(a,b)),Mat("Metal_MatteBlack"),false);
        bar.transform.localRotation=Quaternion.LookRotation(b-a);
        foreach(var p in new[]{a,b}) Box(root,"Post_"+name,p-Vector3.up*.475f,new Vector3(.045f,.95f,.045f),Mat("Metal_MatteBlack"),false);
    }
    static void RebuildDecks(Transform multi)
    {
        var shell=multi.Find("00_Architecture_Shell");
        // Cut only the returning flight; deck over the first flight has >=2m clearance.
        var holes=new[]{new Rect(-14.48f,-13.84f,4.28f,1.31f),new Rect(6.03f,-13.08f,1.11f,4.28f)};
        float[] xs=new[]{-18f,holes[0].xMin,holes[0].xMax,holes[1].xMin,holes[1].xMax,18f}.Distinct().OrderBy(v=>v).ToArray();
        float[] zs=new[]{-16f,holes[0].yMin,holes[0].yMax,holes[1].yMin,holes[1].yMax,16f}.Distinct().OrderBy(v=>v).ToArray();
        for(int f=1;f<=2;f++)
        {
            Remove(shell,$"Floor_{f+1}F_Slab"); var deck=Group(shell,$"Floor_{f+1}F_Slab");
            for(int x=0;x<xs.Length-1;x++) for(int z=0;z<zs.Length-1;z++)
            {
                var center=new Vector2((xs[x]+xs[x+1])/2,(zs[z]+zs[z+1])/2);
                if(holes.Any(h=>h.Contains(center)))continue;
                Box(deck,$"Deck_{x}_{z}",new Vector3(center.x,f*H-.075f,center.y),new Vector3(xs[x+1]-xs[x],.15f,zs[z+1]-zs[z]),Mat("Floor_Terrazzo"));
            }
            // Top-storey guardrails around the three closed edges of each well.
            if(f==2) foreach(var hole in holes){
                bool west=hole.x<0;
                Vector3 a=new Vector3(hole.xMin,f*H,hole.yMin), b=new Vector3(hole.xMax,f*H,hole.yMin);
                Vector3 c=new Vector3(hole.xMax,f*H,hole.yMax), d=new Vector3(hole.xMin,f*H,hole.yMax);
                Guard(deck,a,b);Guard(deck,a,d);
                if(west)Guard(deck,d,c);else Guard(deck,b,c);
            }
        }
    }
    static void Guard(Transform root,Vector3 a,Vector3 b)
    {
        var material=Mat("Metal_MatteBlack"); float length=Vector3.Distance(a,b);
        var barrier=Box(root,"StairGuardCollision",(a+b)/2+Vector3.up*.55f,new Vector3(.06f,1.1f,length),material);
        barrier.transform.localRotation=Quaternion.LookRotation(b-a);barrier.GetComponent<Renderer>().enabled=false;
        foreach(float h in new[]{.55f,1.05f}){
            var bar=Box(root,"StairGuardRail",(a+b)/2+Vector3.up*h,new Vector3(.045f,.045f,length),material,false);
            bar.transform.localRotation=Quaternion.LookRotation(b-a);
        }
        int count=Mathf.CeilToInt(length);
        for(int i=0;i<=count;i++)Box(root,"StairGuardPost",Vector3.Lerp(a,b,(float)i/count)+Vector3.up*.55f,new Vector3(.045f,1.1f,.045f),material,false);
    }
    public static void ResizeText(TextMesh text)
    {
        // Absolute sizing makes the migration safe to run repeatedly.
        bool table=text.text.StartsWith("1F-")||text.text.StartsWith("2F-")||text.text.StartsWith("3F-");
        var candidates=text.transform.parent.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r=>!r.GetComponent<TextMesh>()&&(r.name.Contains("Sign")||r.name.Contains("Board")||r.name.Contains("Price")||r.name=="Table_Num"));
        var board=candidates.OrderBy(r=>Vector3.Distance(r.bounds.center,text.transform.position)).FirstOrDefault();
        if(board&&Vector3.Distance(board.bounds.center,text.transform.position)>1.2f)board=null;
        if(table&&board){var s=board.transform.localScale;s.x=Mathf.Max(s.x,.3f);s.y=Mathf.Max(s.y,.18f);board.transform.localScale=s;}
        float width=board?Mathf.Max(board.bounds.size.x,board.bounds.size.z)*.90f:3f;
        float height=board?board.bounds.size.y*.80f:.4f;
        text.fontSize=96; text.fontStyle=FontStyle.Bold; text.characterSize=.1f; text.lineSpacing=1.05f;
        text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
        var renderer=text.GetComponent<MeshRenderer>();
        Vector3 bounds=renderer.localBounds.size;
        if(bounds.x<.0001f||bounds.y<.0001f)throw new InvalidOperationException("Empty label bounds: "+text.name);
        float targetHeight=table?.10f:(text.text.Contains("\n")?.42f:.26f);
        float factor=Mathf.Min(width/bounds.x,Mathf.Min(height,targetHeight)/bounds.y);
        text.characterSize*=factor;
        EditorUtility.SetDirty(text);
    }
    [MenuItem("Campus/Interiors/Upgrade Cafeteria Text and Stairs")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        Directory.CreateDirectory(Output);
        try{
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var root=Object.FindFirstObjectByType<InteriorSceneRoot>(); Apply(root.transform);
            Physics.SyncTransforms(); Validate(root.transform);
            Capture(root.transform);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Output+"/complete.txt","Saved cafeteria scene; labels fitted; all stair journeys passed.");
            Debug.Log("CAFETERIA_UPGRADE_PASS");
        }finally{if(setup.Any(s=>s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    static void Capture(Transform root)
    {
        var go = new GameObject("CafeteriaReviewCamera"); var camera = go.AddComponent<Camera>();
        camera.fieldOfView=68; camera.nearClipPlane=.05f; camera.farClipPlane=100;
        var rt=new RenderTexture(1600,1000,24); camera.targetTexture=rt;
        var previous=RenderTexture.active;
        var visibility=root.GetComponent<InteriorFloorTextVisibility>();
        visibility.ApplyForHeight(root.position.y+.02f);
        try {
            Vector3[] positions={new Vector3(-6,2,-12.5f),new Vector3(6,2,-4.8f),new Vector3(0,2,9)};
            Vector3[] targets={new Vector3(-12.5f,2,-12.5f),new Vector3(6,2,-11),new Vector3(0,2.5f,15)};
            for(int i=0;i<positions.Length;i++){
                camera.transform.position=root.TransformPoint(positions[i]); camera.transform.LookAt(root.TransformPoint(targets[i]));
                camera.Render(); RenderTexture.active=rt;
                var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply();
                File.WriteAllBytes(Output+"/preview_"+i+".png",image.EncodeToPNG()); Object.DestroyImmediate(image);
            }
        }finally{visibility.Restore();RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
    }
    static void Validate(Transform root)
    {
        var log=new StringBuilder();
        foreach(var stair in root.Find("Cafeteria_MultiFloor_Interior/00_Vertical_Circulation").Cast<Transform>())
        {
            float width=stair.name.StartsWith("Stairs_1")?2.8f:2.4f, off=width/4;
            Vector3[] points={new Vector3(off,.03f,-.6f),new Vector3(off,H/2,FlightRun+.6f),new Vector3(-off,H/2,FlightRun+.6f),new Vector3(-off,H,-.6f)};
            Walk(stair,points,log);Walk(stair,points.Reverse().ToArray(),log);
        }
        File.WriteAllText(Output+"/walkability.txt",log.ToString());
        if(log.ToString().Contains("FAIL"))throw new InvalidOperationException("Stair validation failed: "+log);
        var vis=root.GetComponent<InteriorFloorTextVisibility>();vis.RefreshLabels();
        for(int f=0;f<3;f++){
            vis.ApplyForHeight(root.position.y+f*H+.02f);
            foreach(var t in root.GetComponentsInChildren<TextMesh>(true))
                if(t.GetComponent<Renderer>().forceRenderingOff!=(vis.FloorAtHeight(t.transform.position.y)!=f))throw new Exception("Label floor mismatch");
        }
        vis.Restore();
    }
    static void Walk(Transform stair,Vector3[] points,StringBuilder log)
    {
        var probe=new GameObject("CafeteriaWalkProbe");var cc=probe.AddComponent<CharacterController>();
        cc.height=1.8f;cc.radius=.32f;cc.center=Vector3.up*.9f;cc.stepOffset=.3f;cc.skinWidth=.02f;cc.slopeLimit=50;cc.minMoveDistance=0;
        probe.transform.position=stair.TransformPoint(points[0])+Vector3.up*.08f;Physics.SyncTransforms();
        try{
            foreach(var local in points.Skip(1)){
                Vector3 target=stair.TransformPoint(local);float gravity=0;
                for(int i=0;i<1000;i++){
                    var delta=target-probe.transform.position;delta.y=0;
                    if(delta.magnitude<.08f)break;
                    gravity=cc.isGrounded?-2:gravity-18*.02f;
                    cc.Move((delta.normalized*2+Vector3.up*gravity)*.02f);
                }
                var end=probe.transform.position;var flat=target-end;flat.y=0;
                bool ok=flat.magnitude<.15f&&Mathf.Abs(end.y-target.y)<.25f;
                log.AppendLine($"{(ok?"PASS":"FAIL")} {stair.name} local {local} -> {stair.InverseTransformPoint(end)}");
            }
        }finally{Object.DestroyImmediate(probe);}
    }
}
