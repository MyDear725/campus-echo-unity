using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Opt-in screenshots of real IMGUI, plus the existing welcome progression flow.
// GithubIntegrationQA assigns private save keys to this QA launch before scene load.
public sealed class CampusStoryPresentationQA : MonoBehaviour
{
    CampusGame game;
    string folder;
    bool done;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Environment.GetCommandLineArgs().Contains("-storyStyleQA")) return;
        new GameObject("Story presentation QA").AddComponent<CampusStoryPresentationQA>();
    }
    void LateUpdate() { if (game) game.paused = false; }
    void Call(string method, params object[] args)
    { typeof(CampusWelcomeStory).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game.Story, args); }
    void Require(bool ok, string text) { if (!ok) throw new Exception(text); }
    IEnumerator Start()
    {
        folder = Path.Combine(Application.dataPath, "../QA/StoryPresentation"); Directory.CreateDirectory(folder);
        var run = Run();
        while (true)
        {
            bool next;
            try { next = run.MoveNext(); }
            catch (Exception error) { File.WriteAllText(Path.Combine(folder, "result.txt"), "FAIL: " + error); Debug.LogException(error); Application.Quit(1); yield break; }
            if (!next) break;
            yield return run.Current;
        }
    }
    IEnumerator Shot(string name)
    {
        yield return new WaitForSecondsRealtime(.7f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        yield return new WaitForSecondsRealtime(.5f);
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(4);
        game = FindFirstObjectByType<CampusGame>();
        Require(game && game.Story, "Actual welcome story missing");
        game.paused = false;
        game.Story.ResetProgress(); game.SetUiCursor(true);
        yield return Shot("01-guidance");
        game.SetUiCursor(false); game.Story.Reach("guide"); Call("OpenDialogue");
        Require(game.Story.DialogueOpen && game.UiBlocking, "Dialogue must own cursor and movement");
        yield return Shot("02-dialogue");
        for (int i = 1; i < game.Story.Data.stages[0].lines.Length; i++) Call("Advance");
        yield return Shot("03-choices");
        Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        yield return Shot("04-choices-1280x720");
        Call("Choose", 1); yield return Shot("05-reply");
        Call("Advance"); yield return Shot("06-next-objective");
        Call("CloseDialogue");
        Require(!game.Story.DialogueOpen && !game.UiBlocking, "Close releases cursor ownership");
        Call("OpenDialogue"); Require(game.Story.DialogueOpen, "Pending dialogue can reopen");
        for (int i = 1; i < game.Story.Data.stages[0].lines.Length; i++) Call("Advance");
        Call("Choose", 0); Call("Advance"); Call("Advance");
        Require(game.Story.StageIndex == 1 && !game.Story.DialogueOpen && !game.UiBlocking, "Choice / reply / closing must advance exactly one stage");
        Screen.SetResolution(1600, 1000, FullScreenMode.Windowed);
        CampusSemester.Instance.UI.Open("story"); yield return Shot("07-story-handbook");
        CampusSemester.Instance.UI.Close();
        done = true;
        File.WriteAllText(Path.Combine(folder, "result.txt"), "PASS: guidance, dialogue, two choices, reply, closing and handbook screenshots; 1600x1000 and 1280x720; close/reopen; cursor ownership; exactly one welcome stage advanced.\n");
        Debug.Log("STORY_PRESENTATION_QA_PASS"); Application.Quit(0);
    }
    void Update()
    {
        if (!done && Time.realtimeSinceStartup > 100) { Debug.LogError("Story presentation QA timeout"); Application.Quit(1); }
    }
}
