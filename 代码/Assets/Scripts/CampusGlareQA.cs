using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Visible-player rendering check. Compare the previous outdoor bloom to the
// shipped setting at the same pose, then turn the camera and check day/night.
// GithubIntegrationQA gives this opt-in run separate save keys.
public sealed class CampusGlareQA : MonoBehaviour
{
    CampusGame game;
    string folder;
    readonly List<string> report = new List<string>();
    Bloom bloom;
    bool finished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Environment.GetCommandLineArgs().Contains("-frameStabilityQA")) return;
        var go = new GameObject("Outdoor glare rendering review");
        DontDestroyOnLoad(go);
        go.AddComponent<CampusGlareQA>();
    }

    IEnumerator Start()
    {
        folder = Path.Combine(Application.dataPath, "../QA/FrameStability/glare");
        Directory.CreateDirectory(folder);
        var stack = new Stack<IEnumerator>(); stack.Push(Run());
        while (stack.Count > 0)
        {
            bool next; object value = null;
            try { next = stack.Peek().MoveNext(); if (next) value = stack.Peek().Current; }
            catch (Exception error)
            {
                File.WriteAllText(folder + "/result.txt", "FAIL: " + error + "\n" + string.Join("\n", report));
                Debug.LogException(error); Time.timeScale = 1; finished = true; Application.Quit(1); yield break;
            }
            if (!next) { stack.Pop(); continue; }
            if (value is IEnumerator nested) stack.Push(nested); else yield return value;
        }
    }

    void Update()
    {
        if (!finished && Time.realtimeSinceStartup > 120)
        {
            File.WriteAllText(folder + "/result.txt", "FAIL: graphical review timed out; use a visible player window.");
            Time.timeScale = 1; Application.Quit(1);
        }
    }

    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(4);
        game = FindFirstObjectByType<CampusGame>();
        if (!game) throw new Exception("Missing game.");
        while (CampusSemester.Instance.Restoring) yield return null;
        foreach (var item in game.memories) if (item) item.SetActive(false);
        Time.timeScale = 0; game.paused = false; game.SetUiCursor(true);
        var volume = game.GetComponentInChildren<CampusEnvironmentPresentation>().GetComponent<Volume>();
        if (!volume.sharedProfile.TryGet(out bloom) || bloom.intensity.value != 0)
            throw new Exception("Outdoor highlight expansion is still enabled.");
        var clock = CampusSemester.Instance.Clock;
        clock.Restore(0); CampusSemester.Instance.Lighting.ApplyNow();
        game.SetTravelPose(game.MapToWorld(new Vector3(3, .23f, 194)), 180);
        bloom.intensity.Override(.12f);
        yield return Settle();
        var before = Capture("01-previous-bloom");
        bloom.intensity.Override(0f);
        yield return Settle();
        var after = Capture("02-fixed-daylight");
        long reduced = 0, brighter = 0; double reduction = 0;
        for (int i = 0; i < before.Length; i++)
        {
            int a = Math.Min(before[i].r, Math.Min(before[i].g, before[i].b));
            int b = Math.Min(after[i].r, Math.Min(after[i].g, after[i].b));
            if (a > 220 && a - b > 18) reduced++;
            if (b > 220 && b - a > 18) brighter++;
            reduction += Math.Max(0, a - b);
        }
        report.Add("Same-pose A/B: removed bright expanded pixels=" + reduced + "; newly bright pixels=" + brighter + "; total luminance reduction=" + reduction);
        report.Add("Outdoor bloom intensity=" + bloom.intensity.value + "; tone mapping, daylight and physical lamp settings retained.");
        game.SetTravelPose(game.MapToWorld(new Vector3(3, .23f, 194)), 230);
        yield return Settle(); Capture("03-camera-turn");
        game.SetTravelPose(game.MapToWorld(new Vector3(86, .23f, 10)), 270);
        yield return Settle(); Capture("04-bridge-approach");
        clock.Restore(11 * 3600); CampusSemester.Instance.Lighting.ApplyNow();
        yield return Settle(); Capture("05-night-lamps");
        if (!CampusSemester.Instance.Lighting.LampsOn || bloom.intensity.value != 0)
            throw new Exception("Night lighting or highlight setting regressed.");
        report.Add("Night lamps still scheduled and enabled at 19:00.");
        report.Insert(0, "PASS: actual framebuffer A/B, camera rotation, bridge approach and night lamps; no normal saves modified.");
        File.WriteAllLines(folder + "/result.txt", report);
        Time.timeScale = 1; finished = true; Application.Quit(0);
    }

    IEnumerator Settle()
    {
        yield return new WaitForSecondsRealtime(.65f);
        yield return new WaitForEndOfFrame();
    }

    Color32[] Capture(string name)
    {
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        var pixels = image.GetPixels32();
        if (pixels.Count(p => p.r > 15 || p.g > 15 || p.b > 15) < pixels.Length / 10)
            throw new Exception("Empty capture; run with a visible player window: " + name);
        File.WriteAllBytes(folder + "/" + name + ".png", image.EncodeToPNG());
        Destroy(image); return pixels;
    }
}
