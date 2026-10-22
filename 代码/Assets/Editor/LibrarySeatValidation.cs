using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class LibrarySeatValidation
{
    static LibrarySeatValidation(){EditorApplication.update+=Poll;}
    public static void RunUI(){
        SessionState.SetBool("CampusLibrarySeatQA",true);SessionState.SetString("CampusLibrarySeatQAMode","ui");
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");EditorApplication.EnterPlaymode();
    }
    public static void RunRoutine(){
        SessionState.SetBool("CampusLibrarySeatQA",true);SessionState.SetString("CampusLibrarySeatQAMode","routine");
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");EditorApplication.EnterPlaymode();
    }
    static double readyAfter;
    static void Poll(){
        const string request="Temp/library-seat-validation.request";
        if(!File.Exists(request))return;
        string pending=File.ReadAllText(request).Trim();
        if(pending=="stop"){File.Delete(request);SessionState.SetBool("CampusLibrarySeatQA",false);EditorApplication.isPlaying=false;return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<readyAfter)return;
        if(pending.StartsWith("prepare:")){File.WriteAllText(request,pending.Substring(8));readyAfter=EditorApplication.timeSinceStartup+5;UnityEditor.AssetDatabase.Refresh();return;}
        string mode=File.ReadAllText(request).Trim();File.Delete(request);
        if(mode=="repair"){Repair();return;}
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty){File.WriteAllText("QA/LibrarySeat/result.txt","BLOCKED: Unsaved scene; not replaced.");return;}
        SessionState.SetBool("CampusLibrarySeatQA",true);
        SessionState.SetString("CampusLibrarySeatQAMode",mode);
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Repair(){
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try {
            var scene=EditorSceneManager.OpenScene("Assets/Interiors/Scenes/library.unity");
            var root=UnityEngine.Object.FindFirstObjectByType<InteriorSceneRoot>();
            var holder=root.transform.Find("LibrarySeats");
            UnityEditor.GameObjectUtility.RemoveMonoBehavioursWithMissingScript(holder.gameObject);
            if(!holder.GetComponent<LibrarySeatRegistry>())holder.gameObject.AddComponent<LibrarySeatRegistry>();
            var markers=root.GetComponentsInChildren<LibrarySeatPart>(true);
            var chairs=root.GetComponentsInChildren<UnityEngine.Transform>(true);
            int added=0;
            foreach(var ch in System.Linq.Enumerable.OrderBy(System.Linq.Enumerable.ThenBy(System.Linq.Enumerable.OrderBy(chairs,t=>t.position.z),t=>t.position.x),t=>t.position.y)){
                if(ch.name!="Chair")continue;
                bool exists=System.Linq.Enumerable.Any(markers,m=>UnityEngine.Vector3.Distance(m.transform.position,ch.position)<.02f);
                if(exists)continue;
                int floor=UnityEngine.Mathf.RoundToInt((ch.position.y-root.transform.position.y)/4.3f)+1;
                if(floor<2||floor>4)continue;
                bool round=false;for(var parent=ch.parent;parent&&parent!=root.transform;parent=parent.parent)if(parent.name.Contains("Round_Study_Hall"))round=true;
                string area=round?"A":"B";int n=1;
                while(holder.Find($"{floor}F-{area}-{n:D3}"))n++;
                var go=new UnityEngine.GameObject($"{floor}F-{area}-{n:D3}");go.transform.SetParent(holder,false);go.transform.position=ch.position;go.transform.rotation=ch.rotation;
                var marker=go.AddComponent<LibrarySeatPart>();marker.seatId=go.name;marker.floor=floor;marker.area=area;marker.areaName=round?"圆厅自习区":"阅览区";marker.standOffset=-ch.forward*.75f;added++;
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("QA/LibrarySeat/repair.txt","Added "+added+" missing seat markers; preserved all existing seat IDs; repaired registry MonoScript.");
        }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
}
