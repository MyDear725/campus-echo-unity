using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in full-project check. It uses the real CampusLiving scene, UMA characters,
// original third-person camera, E dispatcher, indoor controller and scene travel.
public sealed class WelcomeIntegrationQA : MonoBehaviour
{
    CampusGame game;
    BuildingInteriorTravel travel;
    PlayerCharacterSelector selector;
    string folder;
    readonly List<string> checks=new List<string>();
    readonly Dictionary<string,int?> prefs=new Dictionary<string,int?>();
    [Serializable] sealed class FrameRecord {public string name;public Vector3 camera,player,headViewport,feetViewport;public float cameraDistance;}
    [Serializable] sealed class FrameRecords {public List<FrameRecord> frames=new List<FrameRecord>();}
    readonly FrameRecords frames=new FrameRecords();
    bool done;
    readonly MethodInfo updateCamera=typeof(CampusGame).GetMethod("UpdateCamera",BindingFlags.Instance|BindingFlags.NonPublic);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-welcomeIntegrationQA"))return;
        var campus=FindFirstObjectByType<CampusGame>();
        if(campus&&!campus.GetComponent<WelcomeIntegrationQA>())campus.gameObject.AddComponent<WelcomeIntegrationQA>();
    }
    void Start()
    {
        folder=Path.GetFullPath(Environment.GetCommandLineArgs().Contains("-welcomeRevisionQA")?"QA/WelcomeRevision":Environment.GetCommandLineArgs().Contains("-welcomeStairProbeQA")?"QA/WelcomeRevision/attempt-stair-probe":"QA/WelcomeIntegration");Directory.CreateDirectory(folder);
        game=GetComponent<CampusGame>();travel=game?game.Interiors:null;selector=game&&game.player?game.player.GetComponent<PlayerCharacterSelector>():null;
        if(!game||!travel||!selector||!game.Story){Finish(false,"Required real game, travel, player selector or story missing");return;}
        File.WriteAllText(Path.Combine(folder,"result.txt"),"RUNNING: real project Play Mode\n");
        foreach(string key in new[]{"CampusWelcomeStoryV1","CampusWelcomeStoryV1Pending"}.Concat(game.Story.Data.stages.Select(s=>"CampusWelcomeStoryV1Choice_"+s.id)))
            prefs[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetInt(key):(int?)null;
        Application.logMessageReceived+=OnLog;
        StartCoroutine(Run());StartCoroutine(Timeout());
    }
    void OnDestroy(){Application.logMessageReceived-=OnLog;RestorePrefs();}
    void RestorePrefs(){foreach(var pair in prefs){if(pair.Value.HasValue)PlayerPrefs.SetInt(pair.Key,pair.Value.Value);else PlayerPrefs.DeleteKey(pair.Key);}PlayerPrefs.Save();}
    void OnLog(string message,string stack,LogType type)
    {
#if UNITY_EDITOR
        // Unity 6000.3 may throw while creating its editor search database in
        // batch Play Mode. Preserve this exact editor-only failure separately;
        // gameplay/UMA/rendering exceptions remain fatal to this test.
        if(stack.Contains("UnityEditor.Search.SearchDatabase")&&stack.Contains("IndexationOnStartup"))
        {
            File.AppendAllText(Path.Combine(folder,"editor-setup-issues.txt"),message+"\n"+stack+"\n");
            return;
        }
#endif
        if(!done&&(type==LogType.Error||type==LogType.Exception))Finish(false,message+"\n"+stack);
    }
    void Finish(bool pass,string detail)
    {
        if(done)return;done=true;RestorePrefs();
        File.WriteAllText(Path.Combine(folder,"result.txt"),(pass?"PASS":"FAIL")+": "+detail+"\n"+string.Join("\n",checks));
        Debug.Log("WELCOME_REAL_PROJECT_"+(pass?"PASS":"FAIL")+": "+detail);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(pass?0:1);
#else
        Application.Quit(pass?0:1);
#endif
    }
    void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add("PASS: "+message);}
    IEnumerator Timeout(){yield return new WaitForSecondsRealtime(300);if(!done)Finish(false,"Integration timeout");}
    IEnumerator Settle()
    {
        float deadline=Time.realtimeSinceStartup+45;
        while(travel.Busy&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(!travel.Busy,"Travel settles within deadline");yield return null;
    }
    IEnumerator Character(PlayerCharacterChoice choice)
    {
        bool generated=false;
        Action changed=()=>generated=true;selector.VisualChanged+=changed;
        Check(selector.ApplyChoice(choice,false),"Real UMA preset applies: "+choice);
        float deadline=Time.realtimeSinceStartup+40;
        while((!generated||!selector.PrimaryRenderer||!selector.PrimaryRenderer.sharedMesh)&&Time.realtimeSinceStartup<deadline)yield return null;
        selector.VisualChanged-=changed;
        Check(generated,"Selected avatar completes its actual asynchronous UMA rebuild: "+choice);
        yield return new WaitForSecondsRealtime(.5f);
        var skin=selector.PrimaryRenderer;
        Check(skin&&skin.sharedMesh&&skin.sharedMesh.vertexCount>100,"Real skinned character generated: "+choice);
        Check(skin.bounds.size.y>1&&skin.bounds.size.y<2.5f,"Character scale fits existing 1.8m capsule: "+choice);
        Check(game.player.GetComponentInChildren<Animator>(true).runtimeAnimatorController,"Real character animator connected: "+choice);
    }
    IEnumerator Capture(string name)
    {
        selector.SetMotion(0,0,true);
        updateCamera.Invoke(game,null);
        yield return new WaitForSecondsRealtime(.3f);
        var camera=game.followCamera;
        Check(camera&&!camera.orthographic,"Original perspective follow camera retained: "+name);
        var avatarPoint=camera.WorldToViewportPoint(selector.PrimaryRenderer.bounds.center);
        Check(avatarPoint.z>0&&avatarPoint.x>0&&avatarPoint.x<1&&avatarPoint.y>0&&avatarPoint.y<1,"Selected character is framed by original camera: "+name);
        var delta=camera.transform.position-(game.player.position+Vector3.up*(DormitoryCorridor.Current(game)?1.0f:1.5f));
        Check(delta.magnitude>.55f&&delta.magnitude<=11.1f,"Third-person camera distance valid: "+name);
        Check(!Physics.CheckSphere(camera.transform.position,.08f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),"Camera is outside wall geometry: "+name);
        var bounds=selector.PrimaryRenderer.bounds;
        var head=camera.WorldToViewportPoint(new Vector3(bounds.center.x,bounds.max.y,bounds.center.z));
        var feet=camera.WorldToViewportPoint(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
        frames.frames.Add(new FrameRecord{name=name,camera=camera.transform.position,player=game.player.position,cameraDistance=delta.magnitude,headViewport=camera.WorldToViewportPoint(new Vector3(bounds.center.x,bounds.max.y,bounds.center.z)),feetViewport=camera.WorldToViewportPoint(new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))});
        File.WriteAllText(Path.Combine(folder,"camera-frames.json"),JsonUtility.ToJson(frames,true));
        DormitoryPreview.SaveCamera(camera,Path.Combine(folder,name+".png"));
        Check(head.y<=.99f&&feet.y>=.01f,"Full avatar height stays inside third-person frame: "+name+" head="+head+" feet="+feet+" FOV="+camera.fieldOfView);
    }
    IEnumerator Walk(Vector3 destination)
    {
        for(int step=0;step<1300;step++)
        {
            Vector3 delta=destination-game.player.position;delta.y=0;
            if(delta.magnitude<.07f)yield break;
            game.player.rotation=Quaternion.LookRotation(delta);
            game.MoveIndoor(Vector3.ClampMagnitude(delta,3.2f/60f),1f/60f);
            selector.SetMotion(3.2f,3.2f,true);
            updateCamera.Invoke(game,null);
            if(step%6==0)yield return null;
        }
        throw new Exception("Actual capsule movement blocked before "+destination+" at "+game.player.position);
    }
    IEnumerator Floor(int destination,int side)
    {
        var dorm=DormitoryCorridor.Current(game);
        yield return Walk(new Vector3(side*23f,.06f,0));
        yield return Walk(new Vector3(side*23f,.06f,destination>dorm.Floor?.6f:-.6f));
        Check(game.Interact(),"Original E dispatcher handles stairs");
        float deadline=Time.realtimeSinceStartup+5;
        while((dorm.Transitioning||dorm.Floor!=destination)&&Time.realtimeSinceStartup<deadline)yield return null;
        Check(dorm.Floor==destination&&!dorm.Transitioning,"Actual floor coroutine reaches "+destination+" (floor="+dorm.Floor+", transitioning="+dorm.Transitioning+", prompt='"+dorm.Prompt+"')");
        Check(!game.UiBlocking&&travel.IndoorController.enabled,"Floor transition restores input and capsule");
    }
    IEnumerator RevisionRun()
    {
        yield return new WaitForSecondsRealtime(2);
        game.enabled=false;game.paused=false;game.SetUiCursor(false);game.Story.enabled=false;
        var guideVisual=game.guide.GetComponent<GuideCharacterVisual>();
        var guideAvatar=game.guide.GetComponentInChildren<UMA.CharacterSystem.DynamicCharacterAvatar>();
        Check(guideVisual&&guideAvatar,"Original Xiaolan UMA visual exists");
        float deadline=Time.realtimeSinceStartup+40;
        while(!guideAvatar.GetComponentInChildren<Animator>()&&Time.realtimeSinceStartup<deadline)yield return null;
        var animator=guideAvatar.GetComponentInChildren<Animator>();
        Check(animator&&animator.isHuman&&animator.runtimeAnimatorController==game.guideCharacterCatalog.LocomotionController,"Xiaolan generated humanoid binds actual locomotion controller");
        foreach(var choice in new[]{PlayerCharacterChoice.Male,PlayerCharacterChoice.Female})
        {
            yield return Character(choice);
            selector.SetMotion(0,0,true);yield return new WaitForSecondsRealtime(.7f);
            var guideSkin=guideAvatar.GetComponentInChildren<SkinnedMeshRenderer>();
            var guideMesh=new Mesh();var playerMesh=new Mesh();
            guideSkin.BakeMesh(guideMesh);selector.PrimaryRenderer.BakeMesh(playerMesh);
            guideMesh.RecalculateBounds();playerMesh.RecalculateBounds();
            float guideHeight=guideMesh.bounds.size.y*guideSkin.transform.lossyScale.y;
            float playerHeight=playerMesh.bounds.size.y*selector.PrimaryRenderer.transform.lossyScale.y;
            Destroy(guideMesh);Destroy(playerMesh);
            Check(Mathf.Abs(guideHeight-playerHeight)<.035f,"Actual baked standing heights agree within 3.5cm: "+choice+" guide="+guideHeight+" player="+playerHeight);
            game.guide.position=game.MapToWorld(new Vector3(3,.23f,187));
            game.guide.rotation=Quaternion.Euler(0,180,0);
            game.SetTravelPose(game.guide.position+Vector3.right*1.2f,180,12);
            yield return new WaitForSecondsRealtime(.6f);
            DormitoryPreview.SaveCamera(game.followCamera,Path.Combine(folder,choice+"-height.png"));
        }
        typeof(CampusWelcomeStory).GetProperty("StageIndex").SetValue(game.Story,1);
        typeof(CampusWelcomeStory).GetField("pending",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(game.Story,false);
        Vector3 origin=game.MapToWorld(new Vector3(3,.23f,187)),direction=Vector3.zero;
        foreach(var candidate in new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left})
        {
            bool clear=true;for(float d=0;d<=14;d+=.5f)clear&=game.IsMapWalkable(game.WorldToMap(origin+candidate*d));
            if(clear){direction=candidate;break;}
        }
        Check(direction.sqrMagnitude>.9f,"Real walkable outdoor path available for Xiaolan following");
        game.guide.position=origin;game.player.position=origin+direction*6;
        game.Story.enabled=true;
        var limb=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        var arm=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Check(limb&&arm,"Xiaolan has real humanoid arm and leg bones");
        Quaternion initialLeg=limb.localRotation,initialArm=arm.localRotation;
        float maxLeg=0,maxArm=0;Vector3 first=game.guide.position;
        int frame=0;
        for(float t=0;t<2.4f;t+=Time.deltaTime)
        {
            game.player.position+=direction*(3.2f*Time.deltaTime);
            // A fixed review camera shows her actual following translation and bones.
            game.followCamera.transform.position=game.guide.position-direction*3.4f+Vector3.Cross(direction,Vector3.up)*3.2f+Vector3.up*1.7f;
            game.followCamera.transform.LookAt(game.guide.position+Vector3.up*.95f);
            yield return null;
            maxLeg=Mathf.Max(maxLeg,Quaternion.Angle(initialLeg,limb.localRotation));
            maxArm=Mathf.Max(maxArm,Quaternion.Angle(initialArm,arm.localRotation));
            if(t>=frame*.15f&&frame<16){DormitoryPreview.SaveCamera(game.followCamera,Path.Combine(folder,"guide-walk-"+frame.ToString("D2")+".png"));frame++;}
        }
        Check(Vector3.Distance(first,game.guide.position)>2,"Real story follow moves Xiaolan across campus");
        Check(animator.GetFloat("Speed")>.8f&&animator.GetCurrentAnimatorStateInfo(0).IsName("Run"),"Actual follow enters locomotion state");
        Check(maxLeg>10&&maxArm>10,"Walking visibly swings limbs: leg="+maxLeg+" arm="+maxArm);
        game.SetUiCursor(true);yield return new WaitForSecondsRealtime(.8f);
        Check(animator.GetFloat("Speed")<.05f&&animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"Dialogue/UI block returns Xiaolan to idle");
        game.SetUiCursor(false);game.paused=true;yield return new WaitForSecondsRealtime(.5f);
        Check(animator.GetFloat("Speed")<.05f,"Paused Xiaolan does not walk in place");
        game.paused=false;game.Story.enabled=false;
        var entry=travel.entrances.First(e=>e&&e.buildingId.StartsWith("dormitory-",StringComparison.Ordinal));
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw,12);
        Check(game.Interact(),"Original E enters dormitory for stair regression");yield return Settle();
        var dorm=DormitoryCorridor.Current(game);
        Check(dorm,"Revised corridor loads in actual original interior");
        foreach(var flight in dorm.GetComponentsInChildren<Transform>().Where(t=>t.name=="Ascending flight"||t.name=="Descending flight"))
        {
            var treads=flight.Cast<Transform>().Where(t=>t.name=="Stair tread").ToArray();
            Check(treads.Length==8,"Eight actual collidable treads: "+flight.name);
            for(int i=0;i<treads.Length;i++)
            {
                float expected=(flight.name=="Ascending flight"?1:-1)*(i+1)*.15f;
                var collider=treads[i].GetComponent<BoxCollider>();
                Check(collider&&Mathf.Abs(collider.bounds.max.y-expected)<.001f,"Tread surface matches signed height "+flight.name+" step "+i);
            }
        }
        bool hasStairSurface=Physics.Raycast(new Vector3(23,2,-1.6f),Vector3.down,out var hit,5,Physics.AllLayers,QueryTriggerInteraction.Ignore);
        Check(hasStairSurface&&(hit.collider.name=="Continuous stair support"||hit.collider.name=="Stair tread"),"Descending stairwell ray hits a stair surface, not a flat corridor slab; hit="+(hasStairSurface?hit.collider.name:"none")+" y="+(hasStairSurface?hit.point.y:-99));
        for(int f=2;f<=6;f++)
        {
            yield return Floor(f,f%2==0?1:-1);
            Check(dorm.GetComponentsInChildren<TextMesh>().Any(t=>t.name=="Room number"&&t.text==f+"01"),"Room labels update on floor "+f);
            if(f==2||f==6)
            {
                game.followCamera.transform.position=new Vector3(19.1f,2.85f,0);
                game.followCamera.transform.LookAt(new Vector3(23,-.05f,0));
                DormitoryPreview.SaveCamera(game.followCamera,Path.Combine(folder,"stairs-floor-"+f+".png"));
                int activeBarriers=dorm.GetComponentsInChildren<Transform>().Count(t=>t.name=="Terminal floor safety barrier");
                Check(activeBarriers==(f==6?2:0),"Terminal safety barriers match floor "+f);
            }
        }
        for(int f=5;f>=1;f--)yield return Floor(f,f%2==0?-1:1);
        Check(dorm.GetComponentsInChildren<Transform>().Count(t=>t.name=="Terminal floor safety barrier")==2,"Ground floor closes only unavailable descending flights");
        // Walk the real CharacterController over both physical stair flights. The
        // transition interaction is separate from movement, so every tread must
        // carry the capsule down and back up without a fall. Test on floor two:
        // floor one's descending barrier correctly blocks access to the shaft.
        yield return Floor(2,1);
        foreach(int stairDirection in new[]{1,-1})
        {
            game.SetTravelPose(new Vector3(23,.06f,0),90,12);
            Physics.SyncTransforms();
            for(int step=0;step<8;step++)
            {
                Vector3 probe=new Vector3(23,3,stairDirection*(.86f+step*.32f));
                bool supported=Physics.Raycast(probe,Vector3.down,out var surface,5,Physics.AllLayers,QueryTriggerInteraction.Ignore);
                Debug.Log("STAIR_SUPPORT_PROBE direction="+stairDirection+" step="+step+" supported="+supported+" hit="+(supported?surface.collider.name:"none")+" y="+(supported?surface.point.y:-99));
            }
            yield return Walk(new Vector3(23,.06f,stairDirection*3.6f));
            Check(stairDirection>0?game.player.position.y>-.15f&&game.player.position.y<1.5f:game.player.position.y> -1.5f&&game.player.position.y<.15f,"Real capsule remains supported on "+(stairDirection>0?"ascending":"descending")+" stair flight; y="+game.player.position.y);
            yield return Walk(new Vector3(23,.06f,0));
            Check(game.player.position.y>-.1f,"Real capsule returns to corridor floor after stair traversal");
        }
        yield return Floor(1,-1);

        Vector3 firstDoor=dorm.transform.TransformPoint(new Vector3(-15,.06f,.65f));
        game.SetTravelPose(firstDoor,dorm.transform.eulerAngles.y,12);
        Check(dorm.Prompt.Contains("101"),"Room 101 door interaction identifies the current floor and room");
        Check(game.Interact(),"Original E dispatcher opens the numbered room");
        float roomDeadline=Time.realtimeSinceStartup+5;
        while(!dorm.InsideRoom&&Time.realtimeSinceStartup<roomDeadline)yield return null;
        while(dorm.InsideRoom&&string.IsNullOrEmpty(dorm.Prompt)&&Time.realtimeSinceStartup<roomDeadline)yield return null;
        Check(dorm.InsideRoom&&dorm.CurrentRoomNumber=="101","Room doorway enters the correct numbered bedroom");
        var room=FindFirstObjectByType<DormitoryRoomInterior>();
        Check(room&&room.gameObject.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Loft bed mattress")==4,"Bedroom contains four upper bunks");
        Check(room.gameObject.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Study desk top")==4,"Four desks sit beneath the loft beds");
        Check(room.gameObject.GetComponentsInChildren<Transform>(true).Count(t=>t.name.Contains("Toilet stall")||t.name=="Toilet stall left partition"||t.name=="Toilet stall right partition"||t.name=="Toilet stall rear partition")==5,"Toilet has enclosing side, rear and split entrance partitions");
        Check(room.gameObject.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Washbasin")&&room.gameObject.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Washbasin cabinet"),"Washbasin and cabinet are modeled to the right of the entrance");
        var panorama=room.gameObject.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r=>r.name=="Balcony view · campus central garden");
        Check(panorama&&panorama.sharedMaterial.mainTexture is Texture2D garden&&garden.width>1000&&garden.height>700,"Balcony displays the anonymous in-game garden panorama");
        var gardenWall=room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Balcony garden collision wall");
        Check(gardenWall&&gardenWall.GetComponent<Collider>()&&gardenWall.GetComponent<Collider>().enabled&&!gardenWall.GetComponent<Renderer>().enabled,"Balcony photo has an invisible solid rear wall");
        var roomHinge=room.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Bedroom entrance door hinge");
        Check(roomHinge&&roomHinge.GetComponentInChildren<Collider>()&&Quaternion.Angle(roomHinge.localRotation,Quaternion.identity)<1f,"Bedroom entrance closes behind the player after entry");
        Check(room.Prompt.Contains("返回宿舍走廊"),"Bedroom offers a clear return prompt by its door");
        var bedroom=room.gameObject.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Dorm room 101");
        Check(bedroom,"Shared bedroom root is present for room 101");
        Check(bedroom.gameObject.activeInHierarchy&&bedroom.GetComponentsInChildren<Renderer>().Length>40,"Entered bedroom geometry is active and renderable in the real scene");
        // Capture from inside and below the ceiling; a high angle from the
        // entry landing sees the ceiling slab instead of the bedroom layout.
        game.followCamera.transform.position=bedroom.TransformPoint(new Vector3(0,2.75f,1.35f));
        game.followCamera.transform.LookAt(bedroom.TransformPoint(new Vector3(0,1.05f,6.35f)));
        DormitoryPreview.SaveCamera(game.followCamera,Path.Combine(folder,"room-101-layout.png"));
        // Reproduce the user's route from the entry, around the open leaf, and
        // through the wider front opening into the toilet stall.
        yield return Walk(bedroom.TransformPoint(new Vector3(0,.06f,.65f)));
        yield return Walk(bedroom.TransformPoint(new Vector3(-2.1f,.06f,.65f)));
        yield return Walk(bedroom.TransformPoint(new Vector3(-2.1f,.06f,1.35f)));
        Check(bedroom.InverseTransformPoint(game.player.position).z>1.15f,"Actual capsule passes through the toilet partition opening");
        Check(string.IsNullOrEmpty(room.Prompt),"Room exit prompt does not activate from inside the toilet stall");
        yield return Walk(bedroom.TransformPoint(new Vector3(-2.1f,.06f,.65f)));
        yield return Walk(bedroom.TransformPoint(new Vector3(0,.06f,.65f)));
        game.SetTravelPose(bedroom.TransformPoint(new Vector3(0,.06f,10.35f)),bedroom.eulerAngles.y,12);
        Physics.SyncTransforms();
        for(int probe=0;probe<90;probe++){game.MoveIndoor(bedroom.forward*(3.2f/60f),1f/60f);if(probe%6==0)yield return null;}
        Vector3 blockedAt=bedroom.InverseTransformPoint(game.player.position);
        Check(blockedAt.z<11.5f&&game.player.position.y>-.1f,"Actual player capsule cannot pass through the balcony photo or fall outside; z="+blockedAt.z+" y="+game.player.position.y);
        game.SetTravelPose(bedroom.TransformPoint(new Vector3(0,.06f,.56f)),bedroom.eulerAngles.y+180,12);
        Check(room.Prompt.Contains("返回宿舍走廊"),"Room door remains reachable after moving through the bedroom");
        Check(game.Interact(),"Original E dispatcher returns from the bedroom");
        roomDeadline=Time.realtimeSinceStartup+5;
        while(dorm.InsideRoom&&Time.realtimeSinceStartup<roomDeadline)yield return null;
        Check(!dorm.InsideRoom&&dorm.Floor==1,"Bedroom exit returns to the same corridor floor");

        yield return Walk(new Vector3(0,.06f,0));yield return Walk(new Vector3(0,.06f,-2.6f));
        Check(game.Interact(),"Original E exits after stair regression");yield return Settle();
        Check(!travel.Inside,"Revised stairs retain campus return flow");
        Finish(true,"Real CampusLiving Xiaolan locomotion and male/female height matching, signed ascending/descending geometry, no slab over descending well, floors 1–6 and terminal guards. Original character texture issue deferred by user. Windows not run.");
    }

    IEnumerator StairProbeRun()
    {
        yield return new WaitForSecondsRealtime(2);
        game.enabled=false;game.Story.enabled=false;game.paused=false;game.SetUiCursor(false);
        var entry=travel.entrances.First(e=>e&&e.buildingId.StartsWith("dormitory-",StringComparison.Ordinal));
        game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw,12);
        Check(game.Interact(),"Original entrance enters for real stair capsule probe");yield return Settle();
        var dorm=DormitoryCorridor.Current(game);Check(dorm,"Live stair geometry initialized");
        // Temporarily clear terminal floor gates so this QA pass can traverse
        // both geometries from their corridor-side entrances.
        foreach(var gate in dorm.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Terminal floor safety barrier"))
            foreach(var collider in gate.GetComponentsInChildren<Collider>(true))collider.enabled=false;
        foreach(int direction in new[]{1,-1})
        {
            game.SetTravelPose(new Vector3(23,.06f,0),90,12);Physics.SyncTransforms();
            for(int step=0;step<8;step++)
            {
                Vector3 probe=new Vector3(23,3,direction*(.86f+step*.32f));
                bool supported=Physics.Raycast(probe,Vector3.down,out var hit,5,Physics.AllLayers,QueryTriggerInteraction.Ignore);
                Check(supported&&(hit.collider.name=="Stair tread"||hit.collider.name=="Continuous stair support"||(step==7&&hit.collider.name=="Safe stair landing")),"Stair ray supports "+(direction>0?"up":"down")+" tread "+step+" with "+(supported?hit.collider.name:"nothing"));
            }
            yield return Walk(new Vector3(23,.06f,direction*3.6f));
            Check(direction>0?game.player.position.y>-.15f&&game.player.position.y<1.5f:game.player.position.y> -1.5f&&game.player.position.y<.15f,"Real capsule stays supported through "+(direction>0?"ascending":"descending")+" flight; y="+game.player.position.y);
        }
        Finish(true,"Real player capsule walked both physical stair directions without falling through the treads.");
    }

    IEnumerator Run()
    {
        if(Environment.GetCommandLineArgs().Contains("-welcomeStairProbeQA")){yield return StairProbeRun();yield break;}
        if(Environment.GetCommandLineArgs().Contains("-welcomeRevisionQA")){yield return RevisionRun();yield break;}
        bool cameraOnly=Environment.GetCommandLineArgs().Contains("-welcomeCameraQA");
        yield return new WaitForSecondsRealtime(2);
        Check(game.Story&&travel&&selector,"Real scene contains story, travel and player selector");
        game.Story.enabled=false;game.enabled=false;game.paused=false;game.SetUiCursor(false);
        Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==1,"Exactly one active camera outdoors");
        var player=game.player;var camera=game.followCamera;float originalFov=camera.fieldOfView;
        var entries=travel.entrances.Where(e=>e&&e.buildingId.StartsWith("dormitory-",StringComparison.Ordinal)).ToArray();
        Check(entries.Length==6,"All six original dormitory entrances present");
        foreach(var choice in new[]{PlayerCharacterChoice.Male,PlayerCharacterChoice.Female})
        {
            yield return Character(choice);
            foreach(var entry in cameraOnly?entries.Take(1):choice==PlayerCharacterChoice.Male?entries:entries.Take(1))
            {
                game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw,12);
                Check(game.Interact(),"Original entrance E works: "+entry.buildingId);yield return Settle();
                Check(travel.Inside&&travel.Current==entry,"Original dormitory scene loads: "+entry.buildingId);
                Check(game.player==player&&game.followCamera==camera,"Player root and third-person camera unchanged");
                Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length==1,"No duplicate camera inside dormitory");
                Check(DormitoryCorridor.Current(game),"Public corridor attached to original dormitory scene");
                yield return Capture(choice+"-"+entry.buildingId+"-entry");
                yield return Walk(new Vector3(5,.06f,-.7f));
                if(entry==entries[0])
                {
                    if(!cameraOnly){for(int f=2;f<=6;f++)yield return Floor(f,f%2==0?1:-1);
                    yield return Capture(choice+"-sixth-floor");
                    for(int f=5;f>=1;f--)yield return Floor(f,f%2==0?-1:1);}
                    // Original orbit-camera code must remain collision safe at four headings.
                    foreach(float heading in new[]{0f,90f,180f,270f})
                    {
                        travel.IndoorController.enabled=false;game.SetTravelPose(new Vector3(0,.06f,0),heading,12);travel.IndoorController.enabled=true;
                        yield return Capture(choice+"-camera-"+heading);
                    }
                }
                yield return Walk(new Vector3(0,.06f,0));yield return Walk(new Vector3(0,.06f,-2.6f));
                Check(game.Interact(),"Original E returns from dormitory");yield return Settle();
                Check(!travel.Inside&&SceneManager.sceneCount==1,"Interior unloads with no leaked scenes");
                Check(Mathf.Abs(camera.fieldOfView-originalFov)<.01f,"Original campus camera FOV restored after dormitory exit");
                Check(Vector3.Distance(game.player.position,game.MapToWorld(entry.returnMapPosition))<.07f,"Returns to correct original campus doorway");
            }
        }
        // Real story object, not a replacement service; preserve its saved keys.
        game.Story.ResetProgress();game.Story.Reach("guide");
        var pending=typeof(CampusWelcomeStory).GetField("pending",BindingFlags.NonPublic|BindingFlags.Instance);
        Check((bool)pending.GetValue(game.Story),"Actual story accepts initial guide milestone");
        Check(selector.CurrentChoice==PlayerCharacterChoice.Female,"Story reset retains real selected avatar");
        Finish(true,cameraOnly?"Camera-only diagnostic: real male/female avatars, four camera headings, dormitory entry/exit. Six-floor traversal not part of this run.":"Real CampusLiving, both UMA avatars, third-person camera, six dormitory entrances, movement, floors 1–6 and return flow checked. Screenshots are actual Unity camera renders. Food/library end-to-end actions not included.");
    }
}
