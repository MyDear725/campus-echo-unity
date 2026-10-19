using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds a standalone macOS player of the campus project.
//
//   Campus/Mac/Build macOS App            (editor menu)
//   -executeMethod MacPlayerBuilder.BuildFromCommandLine -buildTarget OSX
//
// Output: Builds/macOS/风起校园.app
//
// Day-to-day play uses 启动游戏.command and does not involve the Unity editor.
// Re-run this after changing code, scenes or assets to refresh the app.
public static class MacPlayerBuilder
{
    const string OutputDir = "Builds/macOS";
    const string AppName = "风起校园.app";
    const string DisplayName = "风起校园";

    [MenuItem("Campus/Mac/Build macOS App")]
    public static void BuildFromMenu() => Build();

    public static void BuildFromCommandLine() => Build();

    public static void Build() => Build(null);

    // Pass an explicit scene list to build a reduced set (diagnostics); null uses every
    // enabled scene from EditorBuildSettings.
    public static void Build(string[] sceneOverride)
    {
        var scenes = sceneOverride ?? EditorBuildSettings.scenes
            .Where(s => s.enabled && !string.IsNullOrEmpty(s.path))
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0) throw new System.Exception("EditorBuildSettings has no enabled scenes");

        Debug.Log("[MacPlayerBuilder] scenes: " + string.Join(", ", scenes));

        Directory.CreateDirectory(OutputDir);
        string location = Path.Combine(OutputDir, AppName);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = location,
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.Development
        };

        Debug.Log("[MacPlayerBuilder] building " + scenes.Length + " scene(s) -> " + location);
        var result = BuildPipeline.BuildPlayer(options);

        if (result.summary.result != BuildResult.Succeeded)
            throw new System.Exception("macOS build failed: " + result.summary.result);

        ApplyBundleIdentity(location);

        string exec = Path.Combine(location, "Contents/MacOS", DisplayName);
        if (!File.Exists(exec))
        {
            string macosDir = Path.Combine(location, "Contents/MacOS");
            exec = Directory.Exists(macosDir)
                ? Directory.GetFiles(macosDir).FirstOrDefault()
                : null;
        }
        Debug.Log("[MacPlayerBuilder] built OK, executable: " + exec);

        Debug.Log($"[MacPlayerBuilder] MAC_BUILD_COMPLETE size={FormatSize(GetSize(new DirectoryInfo(location)))} scenes={scenes.Length}");
    }

    // Unity names the bundle after PlayerSettings.productName ("Campus Echo"), so the
    // Finder/Dock label would ignore the 风起校园 folder name. Patch the display name
    // after every build so the app presents itself as 风起校园, while keeping the
    // executable file name the bundle already references.
    static void ApplyBundleIdentity(string appPath)
    {
        string plist = Path.Combine(appPath, "Contents/Info.plist");
        if (!File.Exists(plist))
        {
            Debug.LogWarning("[MacPlayerBuilder] Info.plist not found: " + plist);
            return;
        }

        string text = File.ReadAllText(plist);
        string exe = ReadPlistValue(text, "CFBundleExecutable");

        text = SetPlistValue(text, "CFBundleName", DisplayName);
        text = SetPlistValue(text, "CFBundleDisplayName", DisplayName);
        if (!string.IsNullOrEmpty(exe)) text = SetPlistValue(text, "CFBundleExecutable", exe);

        File.WriteAllText(plist, text);
        Debug.Log("[MacPlayerBuilder] bundle display name = " + DisplayName + " (executable: " + exe + ")");
    }

    static string ReadPlistValue(string text, string key)
    {
        var m = Regex.Match(text, "<key>" + key + "</key>\\s*<string>([^<]*)</string>");
        return m.Success ? m.Groups[1].Value : null;
    }

    static string SetPlistValue(string text, string key, string value)
    {
        string pattern = "<key>" + key + "</key>\\s*<string>[^<]*</string>";
        string replacement = "<key>" + key + "</key><string>" + value + "</string>";
        return Regex.IsMatch(text, pattern)
            ? Regex.Replace(text, pattern, replacement)
            : text;
    }

    static long GetSize(DirectoryInfo dir)
    {
        long size = 0;
        foreach (var f in dir.EnumerateFiles("*", SearchOption.AllDirectories)) size += f.Length;
        return size;
    }

    static string FormatSize(long bytes)
    {
        double b = bytes;
        string[] units = { "B", "KB", "MB", "GB" };
        int u = 0;
        while (b >= 1024 && u < units.Length - 1) { b /= 1024; u++; }
        return b.ToString("F1") + " " + units[u];
    }
}
