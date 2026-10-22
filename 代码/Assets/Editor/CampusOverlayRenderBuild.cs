using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class CampusOverlayRenderBuild
{
    // A separate, graphical player for UI regression checks; never replaces the user's app.
    public static void Build()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/CampusLiving.unity", "Assets/Interiors/Scenes/library.unity" },
            locationPathName = "/private/tmp/campus-overlay-render/CampusOverlayQA.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Overlay graphical test build failed: " + report.summary.result);
    }
}
