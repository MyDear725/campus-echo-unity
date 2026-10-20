using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

// Opt-in integration regression; never persists a character choice or changes saves.
public sealed class GithubIntegrationQA : MonoBehaviour
{
    static string Folder => Path.Combine(Application.dataPath,"../QA/GithubIntegration");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void IsolateNewSystems()
    {
        var args=Environment.GetCommandLineArgs();
        var flag=args.FirstOrDefault(a=>a.EndsWith("QA",StringComparison.Ordinal));
        if(flag==null||args.Contains("-semesterQA")||args.Contains("-allowanceQA"))return;
        string prefix="GithubCompatibility."+flag+".";
        CampusSemester.KeyOverride??=prefix+"semester";
        CampusWelcomeStory.KeyOverride??=prefix+"welcome";
        CampusRouteGuide.PreferenceKeyOverride??=prefix+"route";
        GameClock.KeyOverride??=prefix+"clock";
        CampusWallet.KeyOverride??=prefix+"wallet";
        CampusInventory.KeyOverride??=prefix+"inventory";
        CafeteriaOrdering.PrefsKeyOverride??=prefix+"orders";
        LibraryReservationManager.KeyOverride??=prefix+"booking";
        VendingSystem.KeyOverridePrefix??=prefix+"stock";
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if(!Environment.GetCommandLineArgs().Contains("-githubIntegrationQA"))return;
        var go=new GameObject("Github integration QA");DontDestroyOnLoad(go);go.AddComponent<GithubIntegrationQA>();
    }
    IEnumerator Start()
    {
        Directory.CreateDirectory(Folder);
        var run=Run();
        while(true)
        {
            bool next;
            try{next=run.MoveNext();}
            catch(Exception error){File.WriteAllText(Path.Combine(Folder,"result.txt"),"FAIL: "+error);Debug.LogException(error);Application.Quit(1);yield break;}
            if(!next)break;
            yield return run.Current;
        }
    }
    static void Require(bool ok,string error){if(!ok)throw new Exception(error);}
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(2);
        var game=FindFirstObjectByType<CampusGame>();Require(game&&game.Automated,"Missing automated game");game.paused=false;
        var travel=game.GetComponent<BuildingInteriorTravel>();
        var selector=game.player.GetComponent<PlayerCharacterSelector>();Require(selector,"Missing UMA selector");
        int built=0;selector.VisualChanged+=()=>built++;
        var entry=travel.entrances.First(e=>e&&e.buildingId=="cafeteria");
        for(int stage=0;stage<3;stage++)
        {
            if(stage==1)
            {
                game.SetTravelPose(game.MapToWorld(entry.approachMapPosition),entry.enterYaw);
                Require(travel.TryEnter(entry),"Cafeteria entry rejected");
                float until=Time.realtimeSinceStartup+30;
                while(travel.Busy&&Time.realtimeSinceStartup<until)yield return null;
                Require(travel.Inside,"Cafeteria entry failed: "+travel.LastError);
                Require(FindFirstObjectByType<UMA.UMAGeneratorBase>(),"UMA generator was hidden on interior entry");
            }
            var choice=stage==1?PlayerCharacterChoice.Male:PlayerCharacterChoice.Female;
            int before=built;var position=game.player.position;var rotation=game.player.rotation;
            Require(selector.ApplyChoice(choice,false),"Character selection rejected: "+choice);
            float deadline=Time.realtimeSinceStartup+45;
            while(built==before&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(built>before,"Character generation timed out: "+choice);
            Require(selector.PrimaryRenderer&&selector.PrimaryRenderer.sharedMesh&&selector.PrimaryRenderer.sharedMesh.vertexCount>1000,"Generated character has no usable mesh");
            Require(selector.CurrentChoice==choice,"Wrong selected character");
            Require(Vector3.Distance(game.player.position,position)<.01f&&Quaternion.Angle(game.player.rotation,rotation)<.1f,"Changing character moved the player root");
            Require(selector.PrimaryRenderer.sharedMaterials.All(m=>m&&m.shader&&m.shader.name!="Hidden/InternalErrorShader"),"Character material is missing or invalid");
            DormitoryPreview.SaveCamera(game.followCamera,Path.Combine(Folder,"character-"+stage+".png"));
            Debug.Log("INTEGRATION_CHARACTER_PASS: "+(stage==0?"outdoors":"indoors")+" "+choice);
        }
        travel.IndoorController.enabled=false;game.SetTravelPose(travel.Interior.exits[0].position,180);travel.IndoorController.enabled=true;Physics.SyncTransforms();
        Require(travel.TryExit(),"Could not leave cafeteria after switching character");
        float end=Time.realtimeSinceStartup+30;
        while(travel.Busy&&Time.realtimeSinceStartup<end)yield return null;
        Require(!travel.Inside&&Vector3.Distance(game.player.position,game.MapToWorld(entry.returnMapPosition))<.05f,"Wrong campus return point after character switching");
        File.WriteAllText(Path.Combine(Folder,"result.txt"),"PASS: female character generated outdoors; male and female characters regenerated inside cafeteria; usable meshes/materials; unchanged player transform; correct exit to current campus.\n");
        Debug.Log("GITHUB_RUNTIME_INTEGRATION_PASS");Application.Quit(0);
    }
}
