using System.Text;
using UnityEditor;
using UnityEngine;

public static class BoneProbe
{
    public static void Run()
    {
        var sb = new StringBuilder();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/Scenes/CampusLiving.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
        var game = Object.FindFirstObjectByType<CampusGame>();
        if (!game || !game.player) { Debug.Log("BONEPROBE: no player"); return; }

        int n = 0;
        foreach (var t in game.player.GetComponentsInChildren<Transform>(true))
        {
            if (n++ > 80) { sb.AppendLine("..."); break; }
            sb.AppendLine($"  {t.name}  localY={t.localPosition.y:F3}");
        }

        var anim = game.player.GetComponentInChildren<Animation>();
        if (anim)
        {
            sb.AppendLine("ANIM CLIPS:");
            foreach (AnimationState s in anim) sb.AppendLine("  " + s.name);
        }

        var smr = game.player.GetComponentInChildren<SkinnedMeshRenderer>();
        sb.AppendLine("bones: " + (smr && smr.bones != null ? smr.bones.Length.ToString() : "none"));
        if (smr && smr.bones != null)
            foreach (var b in smr.bones) sb.AppendLine("   bone " + b.name);

        Debug.Log("BONEPROBE\n" + sb);
    }
}
