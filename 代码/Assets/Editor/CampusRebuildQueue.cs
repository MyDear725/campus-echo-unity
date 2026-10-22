using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Opens a completed scene revision after the editor refreshes its assets,
// preserving an unsaved scene as a separate copy before switching scenes.
[InitializeOnLoad]
public static class CampusRebuildQueue {
 static bool busy;
 static double nextPoll;
 static CampusRebuildQueue(){EditorApplication.update+=Poll;}
 static void Poll(){
  if(busy||EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
  nextPoll=EditorApplication.timeSinceStartup+1;
  var root=Directory.GetParent(Application.dataPath).FullName;
  var request=Path.Combine(root,"Temp/open-living.request");if(!File.Exists(request)||!File.Exists(Path.Combine(root,"Assets/Scenes/CampusLiving.unity")))return;
  busy=true;File.Delete(request);
  EditorApplication.delayCall+=()=>{
   try{var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.IsValid()&&current.isDirty)EditorSceneManager.SaveScene(current,"Assets/Scenes/UnsavedBeforeLivingUpdate.unity",true);CampusBuilder.OpenProject();Debug.Log("LIVING_SCENE_OPENED");}
   catch(Exception e){Debug.LogException(e);}
   finally{busy=false;}
  };
 }
}
