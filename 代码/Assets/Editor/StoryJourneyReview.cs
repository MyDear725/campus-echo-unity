using UnityEditor;
using UnityEditor.SceneManagement;

public static class StoryJourneyReview
{
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");EditorApplication.EnterPlaymode();}
}
