using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Integration checks intentionally load the existing scenes; they never regenerate
// campus geometry, roads, or building interiors.
public static class GithubIntegrationChecks
{
    public static void PrepareAndBuild()
    {
        PlayerCharacterSelectionInstaller.InstallAllScenes();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var game = UnityEngine.Object.FindFirstObjectByType<CampusGame>();
        game.guideCharacterCatalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(PlayerCharacterSelectionInstaller.CatalogPath);
        if (!game.GetComponent<CampusOutdoorTraversal>() || !game.GetComponent<CampusPlanQA>())
            throw new Exception("The current campus road/terrain components were lost during merge");
        if (!game.mapData.text.Contains("图书馆正门方向步道") || !game.mapData.text.Contains("两湖直行步道"))
            throw new Exception("The latest bridge-arrival roads were lost during merge");
        foreach (var entrance in game.GetComponent<BuildingInteriorTravel>().entrances)
        {
            if (!entrance || !File.Exists(entrance.interiorScenePath))
                throw new Exception("A building entrance points at a missing scene");
        }
        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        PhoneBookingTest.Run();
        CampusBuilder.Build();
        Directory.CreateDirectory("QA/GithubIntegration");
        File.WriteAllText("QA/GithubIntegration/build.txt", "PASS: existing outdoor map retained; UMA selection installed; all entrance scene paths exist; Windows build succeeded.\n");
        Debug.Log("GITHUB_INTEGRATION_BUILD_PASS");
    }
}
