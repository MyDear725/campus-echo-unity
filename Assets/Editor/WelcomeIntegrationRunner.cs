using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Open the actual project scene, then run the opt-in check in editor Play Mode.
public static class WelcomeIntegrationRunner
{
    public static void Run()
    {
        Debug.Log("WELCOME_INTEGRATION: opening real CampusLiving scene");
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
}
