# Player Gender Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a persistent male/female choice to the first-start flow and replace the playable student's visual with two approved free characters without changing movement, collision, camera, quests, or interior travel.

**Architecture:** Keep the existing player Transform as the authoritative gameplay root. A focused `PlayerCharacterSelector` instantiates one approved visual prefab beneath that root, drives a project-owned Animator state set, persists the choice, and falls back to `CampusTraveler.fbx` when validation fails. `CampusGame` remains responsible for input and movement and delegates only semantic motion and selection UI behavior.

**Tech Stack:** Unity 6000.3.26f1, URP 17.3.0, C#, IMGUI, Mecanim Humanoid Animator, Unity Asset Store assets, `PlayerPrefs`, editor automation, existing Windows development-build QA.

## Global Constraints

- Use Unity 6000.3.26f1 and URP 17.3.0; do not upgrade the editor or render pipeline.
- Use one young-adult male and one young-adult female in casual clothing from Hodaart's free `Low Poly Character Collection 3`; exclude child and elderly models.
- Treat import from the package's Unity 6000.4.6 authoring version as a compatibility gate.
- Keep the current player root, collision, camera target, movement, teleport, quest, additive interior travel, stair traversal, and fall recovery behavior unchanged.
- Persist `1 = Male` and `2 = Female` under `CampusUnityCharacterChoiceV1`; missing, zero, and all other values mean no completed choice.
- Automated QA uses Male without overwriting the user's saved choice.
- Keep `Assets/Characters/CampusTraveler.fbx` as the runtime fallback until all validation passes.
- Do not add another player controller, camera, AudioListener, input system, or UI framework.
- Preserve every pre-existing unstaged path captured by the Task 1 baseline, including the library materials, `ProjectSettings/ShaderGraphSettings.asset`, and `ProjectSettings/URPProjectSettings.asset`.
- Commit imported assets with their `.meta` files and use Git LFS rules already present in the repository.

---

## File Structure

**Create**

- `Assets/Scripts/PlayerCharacterCatalog.cs` — enum and male/female prefab catalog.
- `Assets/Scripts/PlayerCharacterSelector.cs` — selection, persistence, visual swapping, fallback, and semantic animation API.
- `Assets/Editor/PlayerCharacterSelectionInstaller.cs` — source-selection commands, wrapper/controller/catalog generation, and safe three-scene installation.
- `Assets/Editor/PlayerCharacterSelectionEditorTests.cs` — headless editor regression checks and QA report writer.
- `Assets/Characters/PlayerSelection/Prefabs/PlayerMale.prefab` — normalized project-owned wrapper around the approved male source.
- `Assets/Characters/PlayerSelection/Prefabs/PlayerFemale.prefab` — normalized project-owned wrapper around the approved female source.
- `Assets/Characters/PlayerSelection/Animations/PlayerLocomotion.controller` — normalized `Idle`, `Walk`, `Run`, `Jump`, and `Fall` state names.
- `Assets/Characters/PlayerSelection/PlayerCharacterCatalog.asset` — runtime references to the two wrapper prefabs.
- `QA/Characters/package-audit.md` — exact imported source paths and compatibility observations.
- `QA/Characters/editor-result.txt` — editor test result.

**Modify**

- `Assets/Scripts/CampusGame.cs:23-36` — initialize the selector and refresh the active renderer.
- `Assets/Scripts/CampusGame.cs:103-106` — delegate semantic motion to the selector.
- `Assets/Scripts/CampusGame.cs:126-143` — first-start choice and pause-menu change UI.
- `Assets/Scripts/CampusGame.cs:169-180` — selection-aware runtime QA and screenshots.
- `Assets/Editor/CampusBuilder.cs:103-120` — generated scenes attach the selector to the player while the guide keeps the Legacy visual.
- `Assets/Scenes/CampusLiving.unity` — attach selector/catalog/fallback references to the existing player root.
- `Assets/Scenes/Campus.unity` — same integration for the migration backup scene.
- `Assets/Scenes/CampusPixel.unity` — same integration for the historical pixel scene.
- `README.md` — document first-start selection, pause-menu switching, persistence, and asset provenance.
- `PROJECT_STATE.md` — record selected source prefab paths, validation results, commits, and remaining work after each task.

**Import without editing package files**

- `Assets/Hodaart/**` — Asset Store package contents as supplied by the publisher.

---

### Task 1: Import, Compatibility Gate, and Exact Character Approval

**Files:**

- Import: `Assets/Hodaart/**`
- Create: `QA/Characters/package-audit.md`
- Modify: `PROJECT_STATE.md`

**Interfaces:**

- Consumes: Unity Asset Store entitlement for `Low Poly Character Collection 3`.
- Produces: exact approved male/female source prefab paths and verified Idle/Walk/Run/Jump clip paths for Task 4.

- [ ] **Step 1: Protect the current worktree state**

Run:

```powershell
git status --short
git diff -- Assets/Interiors/LibraryInterior/UpperHallLandscape/PlantingSoil.mat Assets/Interiors/LibraryInterior/UpperHallLandscape/WarmCeramic.mat Assets/Interiors/LibraryInterior/UpperHallLandscape/WarmPaper.mat ProjectSettings/ShaderGraphSettings.asset ProjectSettings/URPProjectSettings.asset
```

Expected: save the complete pre-import `git status --short` output and relevant diffs in the execution notes; do not stage any baseline path.

- [ ] **Step 2: Acquire and import the free package**

In Unity 6000.3.26f1, open `Window > Package Manager > My Assets`, locate `Low Poly Character Collection 3`, download it under the user's Unity account, and import the complete package. Do not import or enable the package's demo input scripts in the playable campus scene.

Expected: package content appears under `Assets/Hodaart/`; Unity finishes compilation without an error dialog.

- [ ] **Step 3: Run the compatibility compile gate**

Close the interactive Unity project, then run:

```powershell
& 'C:\Unity\Editors\6000.3.26f1\Editor\Unity.exe' -batchmode -quit -projectPath 'D:\大学竞赛\unity游戏\campus-echo-unity-main-full' -logFile 'D:\大学竞赛\unity游戏\campus-echo-unity-main-full\QA\Characters\package-import.log'
```

Expected: exit code `0`; the log contains no `Compilation failed`, `Shader error`, `Deserialize` failure, or missing-script error from the imported package.

- [ ] **Step 4: Inspect and approve the exact pair**

Open the package demo scene or a temporary empty scene. Preview every young-adult casual male/female candidate at the same camera distance. Exclude children, elderly characters, and visibly occupational/fantasy outfits. Present side-by-side screenshots to the user and wait for approval of exactly one male and one female prefab.

Expected: two approved source prefab paths, both using valid Humanoid avatars, supported URP materials, and compatible proportions.

- [ ] **Step 5: Record the concrete asset contract**

Create `QA/Characters/package-audit.md` titled `Player Character Package Audit`. Record Unity version `6000.3.26f1`, package name/version, import compile result, the exact AssetDatabase paths of the approved male prefab, female prefab, Idle clip, Walk clip, Run clip, and Jump clip, plus explicit PASS/FAIL lines for both Humanoid avatars, URP materials, and user pair approval. Every asset entry must be a real path copied from Unity's Inspector and no asset entry may be empty.

- [ ] **Step 6: Commit the compatibility-gated import**

Run:

```powershell
git add -- Assets/Hodaart QA/Characters/package-audit.md PROJECT_STATE.md
git diff --cached --check
git commit -m "assets: import selectable student characters"
```

Expected: commit succeeds; every baseline path remains unstaged.

---

### Task 2: Character Catalog and Selector Runtime Core

**Files:**

- Create: `Assets/Scripts/PlayerCharacterCatalog.cs`
- Create: `Assets/Scripts/PlayerCharacterSelector.cs`
- Create: `Assets/Editor/PlayerCharacterSelectionEditorTests.cs`
- Create: corresponding `.meta` files through Unity import
- Modify: `PROJECT_STATE.md`

**Interfaces:**

- Consumes: wrapper prefabs assigned later to `PlayerCharacterCatalog`.
- Produces:
  - `enum PlayerCharacterChoice { None = 0, Male = 1, Female = 2 }`
  - `PlayerCharacterCatalog.Configure(GameObject male, GameObject female)`
  - `PlayerCharacterSelector.Configure(PlayerCharacterCatalog catalog, GameObject fallbackVisual)`
  - `bool PlayerCharacterSelector.ApplyChoice(PlayerCharacterChoice choice, bool persist)`
  - `bool PlayerCharacterSelector.PersistCurrentChoice()`
  - `PlayerCharacterChoice PlayerCharacterSelector.ReadSavedChoice()`
  - `bool PlayerCharacterSelector.PlayMotion(string state, float speed, bool backwards)`
  - `SkinnedMeshRenderer PlayerCharacterSelector.PrimaryRenderer`
  - `event Action VisualChanged`

- [ ] **Step 1: Write failing editor tests for value mapping and visual swaps**

Create the test entry with concrete assertions:

```csharp
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PlayerCharacterSelectionEditorTests
{
    const string ResultPath = "QA/Characters/editor-result.txt";

    public static void Run()
    {
        Directory.CreateDirectory("QA/Characters");
        try
        {
            TestChoicePersistence();
            TestVisualSwapPreservesRoot();
            File.WriteAllText(ResultPath, "PASS: choice persistence; one active visual; root transform preserved; invalid choice falls back.\n");
            Debug.Log("PLAYER_CHARACTER_EDITOR_TESTS_PASS");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText(ResultPath, "FAIL: " + error + "\n");
            Debug.LogException(error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw;
        }
    }

    static void TestChoicePersistence()
    {
        PlayerPrefs.DeleteKey(PlayerCharacterSelector.PreferenceKey);
        var root = new GameObject("Selector Test Root");
        var selector = root.AddComponent<PlayerCharacterSelector>();
        var catalog = MakeCatalog();
        var fallback = MakeVisual("Fallback");
        fallback.transform.SetParent(root.transform, false);
        selector.Configure(catalog, fallback);
        Require(selector.ReadSavedChoice() == PlayerCharacterChoice.None, "Missing preference must resolve to None");
        Require(selector.ApplyChoice(PlayerCharacterChoice.Female, false), "Female preview must apply");
        Require(!PlayerPrefs.HasKey(PlayerCharacterSelector.PreferenceKey), "Preview must not persist before Start");
        Require(selector.PersistCurrentChoice(), "Confirmed choice must persist");
        Require(PlayerPrefs.GetInt(PlayerCharacterSelector.PreferenceKey) == 2, "Female must persist as 2");
        PlayerPrefs.DeleteKey(PlayerCharacterSelector.PreferenceKey);
        UnityEngine.Object.DestroyImmediate(root);
        DestroyCatalog(catalog);
    }

    static void TestVisualSwapPreservesRoot()
    {
        var root = new GameObject("Selector Test Root");
        root.transform.SetPositionAndRotation(new Vector3(11, 2, -7), Quaternion.Euler(0, 37, 0));
        var position = root.transform.position;
        var rotation = root.transform.rotation;
        var selector = root.AddComponent<PlayerCharacterSelector>();
        var catalog = MakeCatalog();
        var fallback = MakeVisual("Fallback");
        fallback.transform.SetParent(root.transform, false);
        selector.Configure(catalog, fallback);
        Require(selector.ApplyChoice(PlayerCharacterChoice.Male, false), "Male choice must apply");
        Require(selector.ApplyChoice(PlayerCharacterChoice.Female, false), "Female choice must apply");
        Require(root.transform.position == position && root.transform.rotation == rotation, "Visual swap must preserve root pose");
        Require(selector.PrimaryRenderer != null, "Active visual must expose a renderer");
        Require(!selector.ApplyChoice((PlayerCharacterChoice)99, false), "Invalid choice must fail");
        Require(fallback.activeSelf, "Invalid choice must reactivate fallback");
        UnityEngine.Object.DestroyImmediate(root);
        DestroyCatalog(catalog);
    }

    static PlayerCharacterCatalog MakeCatalog()
    {
        var catalog = ScriptableObject.CreateInstance<PlayerCharacterCatalog>();
        catalog.Configure(MakeVisual("Male"), MakeVisual("Female"));
        return catalog;
    }

    static GameObject MakeVisual(string name)
    {
        var visual = new GameObject(name);
        visual.AddComponent<Animator>();
        var renderer = visual.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = new Mesh { name = name + " Mesh" };
        return visual;
    }

    static void DestroyCatalog(PlayerCharacterCatalog catalog)
    {
        UnityEngine.Object.DestroyImmediate(catalog.MalePrefab);
        UnityEngine.Object.DestroyImmediate(catalog.FemalePrefab);
        UnityEngine.Object.DestroyImmediate(catalog);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
```

- [ ] **Step 2: Run the test and verify the expected compile failure**

Run:

```powershell
& 'C:\Unity\Editors\6000.3.26f1\Editor\Unity.exe' -batchmode -quit -projectPath 'D:\大学竞赛\unity游戏\campus-echo-unity-main-full' -executeMethod PlayerCharacterSelectionEditorTests.Run -logFile 'D:\大学竞赛\unity游戏\campus-echo-unity-main-full\QA\Characters\selector-red.log'
```

Expected: non-zero exit; compiler reports missing `PlayerCharacterSelector`, `PlayerCharacterCatalog`, and `PlayerCharacterChoice`.

- [ ] **Step 3: Implement the catalog**

Create `PlayerCharacterCatalog.cs`:

```csharp
using UnityEngine;

public enum PlayerCharacterChoice { None = 0, Male = 1, Female = 2 }

[CreateAssetMenu(menuName = "Campus/Player Character Catalog")]
public sealed class PlayerCharacterCatalog : ScriptableObject
{
    [SerializeField] GameObject malePrefab;
    [SerializeField] GameObject femalePrefab;

    public GameObject MalePrefab => malePrefab;
    public GameObject FemalePrefab => femalePrefab;

    public void Configure(GameObject male, GameObject female)
    {
        malePrefab = male;
        femalePrefab = female;
    }

    public GameObject Resolve(PlayerCharacterChoice choice)
    {
        return choice == PlayerCharacterChoice.Male ? malePrefab :
               choice == PlayerCharacterChoice.Female ? femalePrefab : null;
    }
}
```

- [ ] **Step 4: Implement the selector with exact persistence and fallback rules**

Implement `PlayerCharacterSelector.cs` with these required branches:

```csharp
using System;
using UnityEngine;

public sealed class PlayerCharacterSelector : MonoBehaviour
{
    public const string PreferenceKey = "CampusUnityCharacterChoiceV1";
    [SerializeField] PlayerCharacterCatalog catalog;
    [SerializeField] GameObject fallbackVisual;
    GameObject activeVisual;
    Animator activeAnimator;
    string currentMotion = "Idle";

    public PlayerCharacterChoice CurrentChoice { get; private set; }
    public SkinnedMeshRenderer PrimaryRenderer { get; private set; }
    public event Action VisualChanged;

    public void Configure(PlayerCharacterCatalog value, GameObject fallback)
    {
        catalog = value;
        fallbackVisual = fallback;
    }

    public PlayerCharacterChoice ReadSavedChoice()
    {
        int value = PlayerPrefs.GetInt(PreferenceKey, 0);
        return value == 1 ? PlayerCharacterChoice.Male :
               value == 2 ? PlayerCharacterChoice.Female : PlayerCharacterChoice.None;
    }

    public bool ApplyChoice(PlayerCharacterChoice choice, bool persist)
    {
        GameObject prefab = catalog ? catalog.Resolve(choice) : null;
        if (!prefab) return UseFallback("Character prefab is missing for " + choice);
        GameObject next = Instantiate(prefab, transform, false);
        var renderer = next.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (!renderer) { DestroyObject(next); return UseFallback("Character renderer is missing for " + choice); }
        if (activeVisual) DestroyObject(activeVisual);
        activeVisual = next;
        activeAnimator = next.GetComponentInChildren<Animator>(true);
        PrimaryRenderer = renderer;
        CurrentChoice = choice;
        if (fallbackVisual) fallbackVisual.SetActive(false);
        if (persist) { PlayerPrefs.SetInt(PreferenceKey, (int)choice); PlayerPrefs.Save(); }
        PlayMotion(currentMotion, 0, false);
        VisualChanged?.Invoke();
        return true;
    }

    public bool PersistCurrentChoice()
    {
        if (CurrentChoice != PlayerCharacterChoice.Male && CurrentChoice != PlayerCharacterChoice.Female) return false;
        PlayerPrefs.SetInt(PreferenceKey, (int)CurrentChoice);
        PlayerPrefs.Save();
        return true;
    }

    public bool PlayMotion(string state, float speed, bool backwards)
    {
        currentMotion = state;
        if (!activeAnimator || !activeAnimator.runtimeAnimatorController) return false;
        int hash = Animator.StringToHash("Base Layer." + state);
        if (!activeAnimator.HasState(0, hash)) return false;
        if (activeAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash != hash) activeAnimator.CrossFade(hash, .16f);
        activeAnimator.speed = state == "Walk" || state == "Run"
            ? Mathf.Clamp(speed / (state == "Run" ? 10f : 5.4f), .35f, 1.3f)
            : 1f;
        // The selected free pack has no dedicated backwards clip. Mecanim playback
        // stays positive; gameplay movement remains authoritative in CampusGame.
        return true;
    }

    bool UseFallback(string error)
    {
        if (activeVisual) DestroyObject(activeVisual);
        activeVisual = null; activeAnimator = null; PrimaryRenderer = null; CurrentChoice = PlayerCharacterChoice.None;
        if (fallbackVisual) { fallbackVisual.SetActive(true); PrimaryRenderer = fallbackVisual.GetComponentInChildren<SkinnedMeshRenderer>(true); }
        Debug.LogError(error, this); VisualChanged?.Invoke(); return false;
    }

    static void DestroyObject(GameObject value)
    {
        if (!value) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
```

- [ ] **Step 5: Run the selector tests green**

Run the Step 2 command again with log name `selector-green.log`.

Expected: exit code `0`; `QA/Characters/editor-result.txt` begins with `PASS` and the log contains `PLAYER_CHARACTER_EDITOR_TESTS_PASS`.

- [ ] **Step 6: Commit the runtime core**

```powershell
git add -- Assets/Scripts/PlayerCharacterCatalog.cs Assets/Scripts/PlayerCharacterCatalog.cs.meta Assets/Scripts/PlayerCharacterSelector.cs Assets/Scripts/PlayerCharacterSelector.cs.meta Assets/Editor/PlayerCharacterSelectionEditorTests.cs Assets/Editor/PlayerCharacterSelectionEditorTests.cs.meta QA/Characters/editor-result.txt PROJECT_STATE.md
git diff --cached --check
git commit -m "feat: add player character selector core"
```

---

### Task 3: Integrate Motion Delegation and Choice UI in CampusGame

**Files:**

- Modify: `Assets/Scripts/CampusGame.cs:23-36`
- Modify: `Assets/Scripts/CampusGame.cs:103-106`
- Modify: `Assets/Scripts/CampusGame.cs:126-143`
- Modify: `Assets/Scripts/CampusGame.cs:169-180`
- Modify: `Assets/Editor/PlayerCharacterSelectionEditorTests.cs`
- Modify: `PROJECT_STATE.md`

**Interfaces:**

- Consumes: `PlayerCharacterSelector` API from Task 2.
- Produces: `bool RequiresCharacterChoice`, `void ChooseCharacter(PlayerCharacterChoice choice)`, selection-aware start/pause UI, deterministic QA override.

- [ ] **Step 1: Extend the editor test with startup-choice assertions**

Add a source-independent helper test that verifies the selection contract:

```csharp
static void TestStartupChoiceResolution()
{
    PlayerPrefs.DeleteKey(PlayerCharacterSelector.PreferenceKey);
    Require(PlayerCharacterSelector.ResolveStartupChoice(false, null) == PlayerCharacterChoice.None,
        "Normal first launch must require a choice");
    Require(PlayerCharacterSelector.ResolveStartupChoice(true, null) == PlayerCharacterChoice.Male,
        "Automated QA must default to Male");
    Require(PlayerCharacterSelector.ResolveStartupChoice(true, "female") == PlayerCharacterChoice.Female,
        "Female QA override must be respected");
    Require(!PlayerPrefs.HasKey(PlayerCharacterSelector.PreferenceKey),
        "QA resolution must not persist a preference");
}
```

Call it from `Run()` before writing PASS. Add this exact selector method:

```csharp
public static PlayerCharacterChoice ResolveStartupChoice(bool automated, string qaOverride)
{
    if (string.Equals(qaOverride, "male", StringComparison.OrdinalIgnoreCase))
        return PlayerCharacterChoice.Male;
    if (string.Equals(qaOverride, "female", StringComparison.OrdinalIgnoreCase))
        return PlayerCharacterChoice.Female;
    if (automated)
        return PlayerCharacterChoice.Male;

    int saved = PlayerPrefs.GetInt(PreferenceKey, 0);
    return saved == (int)PlayerCharacterChoice.Male ? PlayerCharacterChoice.Male :
           saved == (int)PlayerCharacterChoice.Female ? PlayerCharacterChoice.Female :
           PlayerCharacterChoice.None;
}
```

- [ ] **Step 2: Run the test red**

Run the Task 2 batch command with log `QA/Characters/startup-red.log`.

Expected: compile failure because `ResolveStartupChoice` does not exist.

- [ ] **Step 3: Add deterministic startup resolution**

Implement `ResolveStartupChoice` so `male` and `female` overrides are case-insensitive, automated mode defaults to Male, and normal mode reads only values `1` and `2`. Do not call `PlayerPrefs.SetInt` in this method.

- [ ] **Step 4: Replace the Legacy animation dependency in CampusGame**

Change the runtime fields and initialization to this shape:

```csharp
PlayerCharacterSelector characterSelector;
SkinnedMeshRenderer face;
string motion = "";
bool characterPickerOpen;

void RefreshCharacterRenderer()
{
    face = characterSelector ? characterSelector.PrimaryRenderer : player.GetComponentInChildren<SkinnedMeshRenderer>();
    if (face) face.updateWhenOffscreen = true;
}
```

In `Awake()`, require or locate the selector, subscribe `VisualChanged`, resolve `-characterQA male|female`, apply the startup choice without persisting QA overrides, and set `characterPickerOpen` when the result is `None`. Keep the guide's existing Legacy animation untouched.

Replace the existing `Animate(string state, float speed, bool backwards = false)` method with:

```csharp
void Animate(string state, float speed, bool backwards = false)
{
    if (motion == state && state != "Walk" && state != "Run") return;
    if (characterSelector) characterSelector.PlayMotion(state, speed, backwards);
    motion = state;
}
```

- [ ] **Step 5: Implement the exact UI state machine**

In the paused IMGUI panel:

- When `characterPickerOpen`, show `选择你的校园角色`, two buttons `男生` and `女生`, the selected value, and `开始探索`.
- `开始探索` is disabled while choice is `None`.
- Normal later launches show the existing `开始 / 继续探索` button.
- The pause panel adds `更换角色`; it opens the picker without changing position or quest fields.
- On the first-start picker, clicking a gender calls `ApplyChoice(choice, false)` for preview; clicking `开始探索` calls `PersistCurrentChoice()` before unpausing.
- In the pause-menu change flow, clicking a gender calls `ApplyChoice(choice, true)` so the confirmed existing-player change persists immediately.
- Closing the picker or starting restores the existing cursor behavior.

Use `GUI.enabled` only around the start button and restore it to `true` immediately afterward so reset and pause controls remain clickable.

- [ ] **Step 6: Make runtime QA selection-aware**

Replace the old `Animation` clip assertions with:

```csharp
if (!characterSelector || !characterSelector.PrimaryRenderer)
    throw new Exception("Selectable player visual missing");
foreach (var state in new[] { "Idle", "Walk", "Run", "Jump", "Fall" })
    if (!characterSelector.PlayMotion(state, state == "Run" ? 10f : 5.4f, false))
        throw new Exception("Character motion state missing: " + state);
```

Write character screenshots under `QA/Characters/Male` or `QA/Characters/Female` based on the active choice so the two runs do not overwrite each other.

- [ ] **Step 7: Run editor tests and compile gate**

Run:

```powershell
& 'C:\Unity\Editors\6000.3.26f1\Editor\Unity.exe' -batchmode -quit -projectPath 'D:\大学竞赛\unity游戏\campus-echo-unity-main-full' -executeMethod PlayerCharacterSelectionEditorTests.Run -logFile 'D:\大学竞赛\unity游戏\campus-echo-unity-main-full\QA\Characters\campus-game-green.log'
```

Expected: exit code `0`, all editor tests PASS, no missing reference or compiler errors.

- [ ] **Step 8: Commit the CampusGame integration**

```powershell
git add -- Assets/Scripts/CampusGame.cs Assets/Editor/PlayerCharacterSelectionEditorTests.cs QA/Characters/editor-result.txt PROJECT_STATE.md
git diff --cached --check
git commit -m "feat: add persistent character choice flow"
```

---

### Task 4: Generate Normalized Character Assets and Install Scenes

**Files:**

- Create: `Assets/Editor/PlayerCharacterSelectionInstaller.cs`
- Create: `Assets/Characters/PlayerSelection/Prefabs/PlayerMale.prefab`
- Create: `Assets/Characters/PlayerSelection/Prefabs/PlayerFemale.prefab`
- Create: `Assets/Characters/PlayerSelection/Animations/PlayerLocomotion.controller`
- Create: `Assets/Characters/PlayerSelection/PlayerCharacterCatalog.asset`
- Modify: `Assets/Scenes/CampusLiving.unity`
- Modify: `Assets/Scenes/Campus.unity`
- Modify: `Assets/Scenes/CampusPixel.unity`
- Modify: `Assets/Editor/PlayerCharacterSelectionEditorTests.cs`
- Modify: `PROJECT_STATE.md`

**Interfaces:**

- Consumes: approved prefab/clip paths recorded in `QA/Characters/package-audit.md`.
- Produces: menu commands `Campus/Characters/Set Selected as Male Source`, `Set Selected as Female Source`, `Set Selected as Animation Root`, and `Install Player Character Selection`.

- [ ] **Step 1: Add a failing installed-asset test**

Add this test and call it only when the catalog asset exists:

```csharp
static void TestInstalledCatalog()
{
    const string path = "Assets/Characters/PlayerSelection/PlayerCharacterCatalog.asset";
    var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(path);
    if (!catalog) return;
    Require(catalog.MalePrefab && catalog.FemalePrefab, "Installed catalog needs both prefabs");
    foreach (var prefab in new[] { catalog.MalePrefab, catalog.FemalePrefab })
    {
        var animator = prefab.GetComponentInChildren<Animator>(true);
        var renderer = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
        Require(animator && animator.avatar && animator.avatar.isHuman && animator.avatar.isValid,
            prefab.name + " needs a valid Humanoid avatar");
        Require(animator.runtimeAnimatorController, prefab.name + " needs the project controller");
        Require(renderer && renderer.sharedMaterials.All(m => m && m.shader && m.shader.isSupported),
            prefab.name + " has an unsupported material");
    }
}
```

Add `using System.Linq;` and call it from `Run()`.

- [ ] **Step 2: Implement source selection without hard-coded publisher paths**

The installer stores selected male prefab GUID, female prefab GUID, and animation-root folder GUID in repository-local `EditorPrefs` keys. Each setter rejects the wrong selection type with a clear `InvalidOperationException`. The install command resolves GUIDs to current AssetDatabase paths, so future package folder renames do not affect the generated runtime assets.

- [ ] **Step 3: Generate the normalized Animator Controller**

Search the selected animation root for `AnimationClip` sub-assets. Resolve names case-insensitively with exact-name preference for `Idle`, `Walk`, `Run`, and `Jump`. Fail before writing assets if any clip is missing.

Create one controller layer with states named exactly `Idle`, `Walk`, `Run`, `Jump`, and `Fall`. Set `Idle` as default. Assign the jump clip to `Jump`; assign the same clip to `Fall`, set its cycle offset to the descending portion, and hold or slow that pose without root motion. Do not add input parameters or transitions because `PlayerCharacterSelector` calls `CrossFade` directly.

- [ ] **Step 4: Generate normalized wrapper prefabs**

For each approved source prefab:

1. Instantiate it under a new zeroed wrapper root.
2. Remove or disable package camera, AudioListener, controller, input, and demo movement components.
3. Set `Animator.applyRootMotion = false` and assign `PlayerLocomotion.controller`.
4. Compute combined `SkinnedMeshRenderer` bounds.
5. Uniformly scale the source so rendered height is `1.75 m`.
6. Offset the source so the lowest rendered foot point is at wrapper local `y = 0`.
7. Save as `PlayerMale.prefab` or `PlayerFemale.prefab`.

Abort if final height is outside `1.70–1.80 m`, if there is not exactly one Animator, or if any material shader is unsupported.

- [ ] **Step 5: Generate the catalog and install each scene safely**

Create or update `PlayerCharacterCatalog.asset` with the two wrapper prefabs. For each of the three scene paths:

1. Open the scene additively in editor automation.
2. Find the single `CampusGame` and its existing `player` Transform.
3. Treat the existing `CampusTraveler` child as `fallbackVisual`.
4. Add or reuse exactly one `PlayerCharacterSelector` on the player root.
5. Call `Configure(catalog, fallbackVisual)` through serialized fields so references persist.
6. Save only if validation succeeds.

Never alter `CampusGame.player`, the player Transform pose, the guide, camera, memories, or any interior configuration.

- [ ] **Step 6: Run the installer and editor tests**

In Unity, select and register the approved male prefab, female prefab, and animation-root folder, then run `Campus/Characters/Install Player Character Selection`.

Close Unity and run the Task 3 batch test command with log `QA/Characters/installed-assets-green.log`.

Expected: exit code `0`; both prefabs are 1.70–1.80 m, both avatars are valid Humanoids, all five states exist, all three scenes contain one selector, and no unsupported material is reported.

- [ ] **Step 7: Review scene diffs before commit**

Run:

```powershell
git diff --stat -- Assets/Scenes/CampusLiving.unity Assets/Scenes/Campus.unity Assets/Scenes/CampusPixel.unity
git diff -- Assets/Scenes/CampusLiving.unity | Select-String -Pattern 'PlayerCharacterSelector|PlayerCharacterCatalog|CampusUnity' -Context 5,5
```

Expected: scene changes are limited to the selector component and its catalog/fallback references; no campus geometry or user-owned material changes are staged.

- [ ] **Step 8: Commit generated assets and scene installation**

```powershell
git add -- Assets/Editor/PlayerCharacterSelectionInstaller.cs Assets/Editor/PlayerCharacterSelectionInstaller.cs.meta Assets/Editor/PlayerCharacterSelectionEditorTests.cs Assets/Characters/PlayerSelection Assets/Scenes/CampusLiving.unity Assets/Scenes/Campus.unity Assets/Scenes/CampusPixel.unity QA/Characters/editor-result.txt PROJECT_STATE.md
git diff --cached --check
git commit -m "feat: install selectable student visuals"
```

---

### Task 5: Make Future Campus Rebuilds Preserve the Selection System

**Files:**

- Modify: `Assets/Editor/CampusBuilder.cs:103-120`
- Modify: `Assets/Editor/PlayerCharacterSelectionEditorTests.cs`
- Modify: `PROJECT_STATE.md`

**Interfaces:**

- Consumes: `PlayerCharacterCatalog.asset` and `PlayerCharacterSelector.Configure(PlayerCharacterCatalog catalog, GameObject fallbackVisual)`.
- Produces: `PlayerCharacterSelector CampusBuilder.ConfigureSelectablePlayer(Transform playerRoot, PlayerCharacterCatalog catalog)`; generated guide remains Legacy-only.

- [ ] **Step 1: Add a failing builder-helper regression assertion**

Add this test and call it from the editor-test runner:

```csharp
static void TestBuilderSelectablePlayerSetup()
{
    var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(
        "Assets/Characters/PlayerSelection/PlayerCharacterCatalog.asset");
    Require(catalog, "Installed character catalog is required");
    var root = new GameObject("Generated Player Test");
    var fallback = new GameObject("Campus Traveler");
    fallback.transform.SetParent(root.transform, false);
    fallback.AddComponent<Animation>();
    fallback.AddComponent<SkinnedMeshRenderer>().sharedMesh = new Mesh();
    var selector = CampusBuilder.ConfigureSelectablePlayer(root.transform, catalog);
    Require(selector && selector.transform == root.transform,
        "Builder must attach selector to the existing player root");
    Require(root.GetComponents<PlayerCharacterSelector>().Length == 1,
        "Builder must attach exactly one selector");
    UnityEngine.Object.DestroyImmediate(root);
}
```

Run the editor tests.

Expected: compile failure because `CampusBuilder.ConfigureSelectablePlayer` does not exist.

- [ ] **Step 2: Split player and guide spawning responsibilities**

Refactor the local `SpawnCharacter` logic into:

- `SpawnLegacyCharacter(name, position)` — current Legacy FBX/material/clip path, used by the guide and as the player fallback child.
- `SpawnSelectablePlayer(name, position)` — calls `SpawnLegacyCharacter`, then adds `PlayerCharacterSelector`, loads `Assets/Characters/PlayerSelection/PlayerCharacterCatalog.asset`, and configures the existing child as fallback.
- `public static PlayerCharacterSelector ConfigureSelectablePlayer(Transform playerRoot, PlayerCharacterCatalog catalog)` — validates both arguments, finds the Legacy `Animation` child as fallback, adds or reuses exactly one selector, calls `Configure`, and returns it.

Throw a clear exception if the catalog is missing while preserving the existing check that `CampusTraveler.fbx` imports correctly.

- [ ] **Step 3: Run compile and editor tests**

Because the repository README states full regeneration may require absent `TempMigration` data, do not run `Campus/Rebuild campus scene` against the working scenes. Compile and run `PlayerCharacterSelectionEditorTests.Run`; the new helper test exercises the builder's selectable-player setup without rebuilding a production scene.

Expected: editor tests PASS; no production scene is overwritten by a failed legacy rebuild.

- [ ] **Step 4: Commit builder preservation**

```powershell
git add -- Assets/Editor/CampusBuilder.cs Assets/Editor/PlayerCharacterSelectionEditorTests.cs QA/Characters/editor-result.txt PROJECT_STATE.md
git diff --cached --check
git commit -m "build: preserve selectable player setup"
```

---

### Task 6: End-to-End Build, Male/Female Runtime QA, and Documentation

**Files:**

- Modify: `README.md`
- Modify: `Assets/Scripts/CampusGame.cs` only if runtime QA exposes a defect
- Modify: `Assets/Scripts/PlayerCharacterSelector.cs` only if runtime QA exposes a defect
- Create: `QA/Characters/Male/**`
- Create: `QA/Characters/Female/**`
- Modify: `PROJECT_STATE.md`

**Interfaces:**

- Consumes: installed scenes, selector, normalized prefabs/controller, and existing `CampusBuilder.Build()`.
- Produces: Windows build, two deterministic QA runs, visual evidence, and user-facing documentation.

- [ ] **Step 1: Document the player-facing behavior and provenance**

Add a concise README section that states:

```markdown
### 可选校园角色

首次点击开始前需要选择男生或女生角色；选择保存在本机，后续启动会自动沿用。暂停菜单中的“更换角色”可以随时切换外观，不会改变人物位置、任务进度或当前建筑。角色模型来自 Unity Asset Store 免费资源 Low Poly Character Collection 3，并按项目的 URP 与动画接口进行了适配。
```

- [ ] **Step 2: Run final editor validation**

Run `PlayerCharacterSelectionEditorTests.Run` with log `QA/Characters/final-editor.log`.

Expected: exit code `0`; report PASS; both visuals valid; all three scenes wired; existing user-modified files remain unstaged.

- [ ] **Step 3: Build the Windows demo**

Run:

```powershell
& 'C:\Unity\Editors\6000.3.26f1\Editor\Unity.exe' -batchmode -quit -projectPath 'D:\大学竞赛\unity游戏\campus-echo-unity-main-full' -executeMethod CampusBuilder.Build -logFile 'D:\大学竞赛\unity游戏\campus-echo-unity-main-full\QA\Characters\build.log'
```

Expected: exit code `0`; log contains `CAMPUS_BUILD_COMPLETE`; `Builds/Windows/CampusEcho.exe` is updated.

- [ ] **Step 4: Run deterministic male QA**

```powershell
& 'D:\大学竞赛\unity游戏\campus-echo-unity-main-full\Builds\Windows\CampusEcho.exe' -campusQA -characterQA male -force-d3d11 -screen-fullscreen 0 -screen-width 1440 -screen-height 900
```

Expected: exit code `0`; `QA/Characters/Male/runtime-result.txt` reports route, camera, animation, and height PASS; screenshots show supported materials and correct foot placement.

- [ ] **Step 5: Run deterministic female QA**

```powershell
& 'D:\大学竞赛\unity游戏\campus-echo-unity-main-full\Builds\Windows\CampusEcho.exe' -campusQA -characterQA female -force-d3d11 -screen-fullscreen 0 -screen-width 1440 -screen-height 900
```

Expected: exit code `0`; `QA/Characters/Female/runtime-result.txt` reports the same checks PASS.

- [ ] **Step 6: Perform manual first-launch and persistence checks**

Back up the current choice value, delete only `CampusUnityCharacterChoiceV1`, launch the build, and verify:

1. Gameplay cannot start before choosing.
2. Both buttons preview the correct character.
3. Restart preserves the chosen value.
4. Pause-menu switching works outdoors and inside a dormitory/teaching building.
5. Player position, collected memories, completion state, and interior travel state remain unchanged.
6. Restore the backed-up value after testing.

- [ ] **Step 7: Inspect final scope and commit**

Run:

```powershell
git diff --check
git status --short
git diff --name-only HEAD
```

Expected: intended character/README/QA/state files only; every Task 1 baseline path remains unstaged and unmodified by this work.

Commit:

```powershell
git add -- README.md Assets/Scripts/CampusGame.cs Assets/Scripts/PlayerCharacterSelector.cs QA/Characters PROJECT_STATE.md
git diff --cached --check
git commit -m "test: validate selectable student characters"
```

- [ ] **Step 8: Final verification summary**

Record in `PROJECT_STATE.md` the final commit hashes, exact male/female source paths, Unity editor test command/results, build result, both runtime QA results, manual persistence result, and any remaining non-character user changes. Do not mark the feature complete if either gender lacks a valid animation state, supported material, correct height, or successful indoor traversal.
