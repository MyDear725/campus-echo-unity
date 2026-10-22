using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Reconcile the remote building into the locally refined scene. Never regenerate the campus.
public static class CampusRemoteCompatibility
{
    public static void ApplyAndBuild()
    {
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var game=Object.FindFirstObjectByType<CampusGame>();var root=GameObject.Find("开发区校区 · 校园环境").transform;
        var data=JsonUtility.FromJson<CampusData>(game.mapData.text);
        string preservedRoads=JsonUtility.ToJson(new RoadSnapshot{roads=data.roads});
        string preservedSports=JsonUtility.ToJson(new FeatureSnapshot{features=data.features.Where(f=>f.id==39327169||f.id==39327816).ToArray()});
        var travel=game.GetComponent<BuildingInteriorTravel>();var entrances=travel.entrances.ToArray();
        if(!root.Find("信息楼 · 石材格栅与玻璃中庭")||!root.Find("室外篮球场 · 主路接驳"))throw new Exception("Local environment revisions missing before reconciliation");
        var catalog=AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>("Assets/Characters/PlayerSelection/PlayerCharacterCatalog.asset");
        if(!catalog||!catalog.ResolveVisual(PlayerCharacterChoice.Male)||!catalog.ResolveVisual(PlayerCharacterChoice.Female))throw new Exception("Local VRoid catalogue missing");

        ComprehensiveBuildingBuilder.Apply(root,data,game);
        ComprehensiveBuildingBuilder.Validate();
        var combined=JsonUtility.FromJson<CampusData>(game.mapData.text);
        if(preservedRoads!=JsonUtility.ToJson(new RoadSnapshot{roads=combined.roads}))throw new Exception("Comprehensive integration changed local road network");
        if(preservedSports!=JsonUtility.ToJson(new FeatureSnapshot{features=combined.features.Where(f=>f.id==39327169||f.id==39327816).ToArray()}))throw new Exception("Comprehensive integration changed corrected sports sites");
        if(!entrances.SequenceEqual(travel.entrances)||entrances.Any(e=>!e||!File.Exists(e.interiorScenePath)))throw new Exception("Building travel contracts changed");
        var field=combined.features.Single(f=>f.id==77921);
        if(!combined.obstacles.Any(o=>o.name=="Comprehensive rear")||field.points.Min(p=>p.x)!=-158)throw new Exception("Remote comprehensive map/collision revision missing");
        if(!game.GetComponent<CampusOutdoorTraversal>()||!game.GetComponent<CampusPlanQA>())throw new Exception("Outdoor walking components missing");
        CampusCourtMapBuilder.RefreshMap(combined);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("QA/GithubIntegration");
        File.WriteAllText("QA/GithubIntegration/20261022-compatibility.txt","Remote: 71e2ea18 (comprehensive building glass atrium, accessible entrance and forecourt). Local base scene restored from backed-up pre-merge copy, then remote authoring applied to it. Existing roads and corrected athletics/basketball features compared before/after and identical; all interior entrance references retained; both VRoid visual prefabs remain; combined live map and M-map texture updated. Remote physical entrance/steps/ramp authoring validation passed. Local work remains uncommitted for GitHub Desktop review.\n");
        CampusPerformanceBuild.OptimizeAndBuild();Debug.Log("REMOTE_COMPATIBILITY_BUILD_PASS");
    }
    [Serializable]class RoadSnapshot{public CampusRoad[] roads;}
    [Serializable]class FeatureSnapshot{public CampusFeature[] features;}
}
