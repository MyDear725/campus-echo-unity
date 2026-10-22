using UnityEditor;
using UnityEditor.SceneManagement;

public static class DormitoryPolishReview
{
    public static void Run()
    {
        ShaderUtil.allowAsyncCompilation=false;
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        EditorApplication.EnterPlaymode();
    }
}
