using System.Text;
using UnityEditor;
using UnityEngine;

// Confirms the library scene carries everything the phone and study flow need when the
// scene is opened and played directly (no campus round-trip).
public static class SceneSanityProbe
{
    public static void Run()
    {
        var sb = new StringBuilder("=== SCENE SANITY ===\n");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            "Assets/Interiors/Scenes/library.unity",
            UnityEditor.SceneManagement.OpenSceneMode.Single);

        var parts = Object.FindObjectsByType<LibrarySeatPart>(FindObjectsSortMode.None);
        sb.AppendLine($"  LibrarySeatPart: {parts.Length}");

        var regs = Object.FindObjectsByType<LibrarySeatRegistry>(FindObjectsSortMode.None);
        sb.AppendLine($"  LibrarySeatRegistry: {regs.Length}");

        var boots = Object.FindObjectsByType<LibrarySeatBootstrap>(FindObjectsSortMode.None);
        sb.AppendLine($"  LibrarySeatBootstrap: {boots.Length}");

        var hosts = Object.FindObjectsByType<CampusSystemsHost>(FindObjectsSortMode.None);
        sb.AppendLine($"  CampusSystemsHost: {hosts.Length}");

        var vms = Object.FindObjectsByType<VendingMachineUI>(FindObjectsSortMode.None);
        sb.AppendLine($"  VendingMachineUI: {vms.Length}");
        foreach (var vm in vms)
            sb.AppendLine($"    drinks={(vm.drinksMachine ? vm.drinksMachine.position.ToString() : "NULL")} " +
                          $"snacks={(vm.snacksMachine ? vm.snacksMachine.position.ToString() : "NULL")}");

        var sits = Object.FindObjectsByType<LibrarySeatInteraction>(FindObjectsSortMode.None);
        sb.AppendLine($"  LibrarySeatInteraction: {sits.Length}");

        var campusGame = Object.FindFirstObjectByType<CampusGame>();
        sb.AppendLine($"  CampusGame in library scene: {(campusGame ? "yes" : "NO - panels need it")}");

        Debug.Log(sb.ToString());
    }
}
