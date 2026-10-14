using UnityEditor;
public class CharacterImport : AssetPostprocessor {
 void OnPreprocessModel(){if(!assetPath.EndsWith("CampusTraveler.fbx"))return;var m=(ModelImporter)assetImporter;m.globalScale=1f;m.animationType=ModelImporterAnimationType.Legacy;m.importBlendShapes=true;m.importCameras=false;m.importLights=false;m.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;}
 void OnPreprocessAnimation(){if(!assetPath.EndsWith("CampusTraveler.fbx"))return;var m=(ModelImporter)assetImporter;var clips=m.defaultClipAnimations;foreach(var c in clips){foreach(var n in new[]{"Idle","Walk","Run","Jump","Fall","Land","Tired","Talk"})if(c.name.Contains(n)){c.name=n;break;}c.loopTime=c.name!="Jump"&&c.name!="Land";c.wrapMode=c.loopTime?UnityEngine.WrapMode.Loop:UnityEngine.WrapMode.ClampForever;}m.clipAnimations=clips;}
}
