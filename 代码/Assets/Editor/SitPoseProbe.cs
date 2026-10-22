using System.Text;
using UnityEditor;
using UnityEngine;

public static class SitPoseProbe
{
    public static void Run()
    {
        var sb = new StringBuilder("=== SIT POSE PROBE ===\n");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/Scenes/CampusLiving.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        var game = Object.FindFirstObjectByType<CampusGame>();
        if (!game || !game.player) { Debug.Log("no player"); return; }

        var pose = game.player.GetComponent<CafeteriaSitPose>();
        if (!pose) pose = game.player.gameObject.AddComponent<CafeteriaSitPose>();
        sb.AppendLine($"  component present: {pose != null}");
        sb.AppendLine($"  Ready (bones found): {pose.Ready}");

        foreach (var n in new[] { "Thigh_L", "Thigh_R", "Shin_L", "Shin_R", "Spine" })
        {
            Transform found = null;
            foreach (var t in game.player.GetComponentsInChildren<Transform>(true))
                if (t.name == n) { found = t; break; }
            sb.AppendLine($"  {n}: {(found ? "found rot=" + found.localRotation.eulerAngles.ToString() : "MISSING")}");
        }

        var anim = game.player.GetComponentInChildren<Animation>();
        sb.AppendLine($"  animation component: {(anim ? anim.name : "none")}, enabled={(anim ? anim.enabled : false)}");
        Debug.Log(sb.ToString());
    }
}
