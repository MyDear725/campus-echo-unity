using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Optional submission check: supply a UTF-8, newline-separated block list as Base64
// using -submissionBlockList. Identity terms are deliberately not stored in source.
public static class CampusSubmissionReview
{
    public static void Validate()
    {
        var args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-submissionBlockList");
        if (index < 0 || index + 1 >= args.Length)
            throw new ArgumentException("A submission block list is required.");
        var terms = Encoding.UTF8.GetString(Convert.FromBase64String(args[index + 1]))
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var report = new List<string>();
        int texts = 0, objects = 0;
        var paths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes", "Assets/Interiors", "Assets/CampusPlanRevision" })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p).ToArray();
        foreach (var path in paths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                objects++;
                Check(transform.name, terms, path + " object");
                foreach (var text in transform.GetComponents<TextMesh>())
                {
                    texts++;
                    Check(text.text, terms, path + " TextMesh");
                }
                foreach (var text in transform.GetComponents<TMPro.TMP_Text>())
                {
                    texts++;
                    Check(text.text, terms, path + " TMP");
                }
            }
            if (path == "Assets/Scenes/CampusLiving.unity")
            {
                if (!scene.GetRootGameObjects().Any(r => r.name == "校园环境"))
                    throw new InvalidOperationException("Outdoor root binding is inconsistent.");
                var game = UnityEngine.Object.FindFirstObjectByType<CampusGame>();
                if (!game || !game.mapData) throw new InvalidOperationException("Missing campus or map binding.");
                Check(game.mapData.text, terms, path + " map data");
            }
            report.Add("PASS scene: " + path);
        }
        report.Insert(0, $"PASS: {paths.Length} scenes, {objects} objects, {texts} visible text components; scene names, labels and outdoor map are anonymous.");
        Directory.CreateDirectory("QA/Submission");
        File.WriteAllLines("QA/Submission/scene-audit.txt", report);
        Debug.Log("SUBMISSION_SCENE_AUDIT_PASS: " + paths.Length);
    }

    static void Check(string value, string[] terms, string location)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (terms.Any(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
            throw new InvalidOperationException("Submission identity remains in " + location);
    }
}
