using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

// Builds a separate, windowed player for the allowance/shop regression. It never replaces
// the player the shortcut starts; QA output goes to output/生活费与档口验证.
public static class AllowanceQABuild
{
    public static void Build()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[]
            {
                "Assets/Scenes/CampusLiving.unity",
                "Assets/Interiors/Scenes/cafeteria.unity",
                "Assets/Interiors/Scenes/library.unity"
            },
            locationPathName = "/private/tmp/campus-allowance-qa/AllowanceQA.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Allowance QA build failed: " + report.summary.result);
        Console.WriteLine("ALLOWANCE_QA_BUILD_OK");
    }
}
