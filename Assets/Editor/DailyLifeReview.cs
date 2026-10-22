using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class DailyLifeReview
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/CampusLiving.unity");
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.GameView")).First(t=>t!=null);
        var view=EditorWindow.GetWindow(type);view.Show();view.Focus();
        EditorApplication.EnterPlaymode();
    }
}
