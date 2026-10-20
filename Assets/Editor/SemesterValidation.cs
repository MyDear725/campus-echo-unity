using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SemesterValidation
{
    [MenuItem("Campus/Semester/Run local semester validation")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
}
