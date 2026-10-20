# Player Gender Selection and Free Character Replacement Design

Date: 2026-10-18

## Objective

Add a first-start male/female character choice and replace the current playable student visual with two free, same-style Unity Asset Store characters. Preserve the existing player movement, collision, camera, quest save, additive interior travel, and automated QA behavior.

## Scope

This change covers the playable student's visual model, animation playback, first-start choice, persistence, pause-menu switching, and regression validation. It does not add character customization beyond male/female, change the guide or background NPCs, redesign movement, or alter quests and building interiors.

## Selected asset

Use one male and one female character from Hodaart's free [Low Poly Character Collection 3](https://assetstore.unity.com/packages/3d/characters/humanoids/low-poly-character-collection-3-388758).

Only young-adult characters in casual clothing qualify for the playable pair; child and elderly models are excluded. After the compatibility import, preview all qualifying models side by side and obtain user approval for the exact male and female prefabs before editing campus scenes. Record their final prefab paths in the implementation plan.

Reasons:

- Both choices come from one visual family.
- The package supplies male and female Humanoid characters and the required Idle, Walk, Run, and Jump animations.
- All characters share a material and texture atlas, reducing visual drift and draw-call overhead.
- The package is small and lists native URP support.
- It is distributed under the Standard Unity Asset Store EULA.

The package was authored with Unity 6000.4.6 while this project uses Unity 6000.3.26f1. Import is therefore a compatibility gate, not an assumption. The package must be acquired through the user's Unity Asset Store account and imported into a disposable or reversible project state before scene integration.

## Existing constraints

- The current `Assets/Characters/CampusTraveler.fbx` is imported as Legacy Animation.
- `CampusGame` currently finds an `Animation` component and calls clips named `Idle`, `Walk`, `Run`, `Jump`, and `Fall`.
- The FBX is instantiated directly in three campus scenes and loaded by `CampusBuilder.cs`.
- The player Transform is also the movement root, camera target, teleport target, collision reference, and interior traveler.
- QA expects a visible `SkinnedMeshRenderer`, a character height between 1 and 2.5 metres, and working movement animations.

## Architecture

### Stable player root

Keep the current player root Transform and all gameplay references unchanged. Remove the direct dependency between gameplay code and a particular imported FBX. The selected male or female visual becomes a child of this root.

This boundary ensures that changing appearance cannot reset position, break camera tracking, replace the collision capsule, or invalidate interior-travel state.

### PlayerCharacterSelector

Add a focused `PlayerCharacterSelector` component to the player root. It owns:

- references to the male and female visual prefabs;
- the active instantiated visual;
- the active `Animator` and primary `SkinnedMeshRenderer`;
- load, validate, apply, and persist operations;
- the normalized motion API used by `CampusGame`.

The component exposes a small contract such as `HasSavedChoice`, `CurrentChoice`, `ApplyChoice`, `PlayMotion`, and `PrimaryRenderer`. It does not own input, movement, camera behavior, quests, or menus.

### Unified animation controller

Create a project-owned Animator Controller rather than depending on a package demo controller. It contains normalized states for:

- `Idle`
- `Walk`
- `Run`
- `Jump`
- `Fall`

Package clips are assigned to those states. Use the supplied jump clip for `Jump`; if no separate fall clip exists, configure `Fall` from the descending portion of that same clip. Gameplay motion and gravity remain authoritative. Locomotion clips run in place because position continues to be controlled by `CampusGame`.

`CampusGame` sends semantic motion requests to `PlayerCharacterSelector` instead of reading a Legacy `Animation` component. Playback speed continues to reflect actual movement speed and backward movement behavior where supported.

### Start and pause UI

Extend the existing immediate-mode start panel in `CampusGame.OnGUI()` so the feature matches the current UI technology and does not introduce a second UI framework.

On first launch, when no valid character preference exists:

1. Keep the game paused and cursor free.
2. Show the title `选择你的校园角色`.
3. Show `男生` and `女生` choices with a clear selected state.
4. Keep `开始探索` unavailable until a choice is made.
5. Apply the visual immediately for preview.
6. Save the choice when the user starts.

On later launches, load the saved choice before the first rendered gameplay frame and retain the current `开始 / 继续探索` behavior.

The pause menu shows the current choice and a `更换角色` action. Changing it immediately swaps the visual and saves the new choice. Player position, rotation, collected memories, completion state, and current interior remain unchanged.

## Persistence and data flow

Store the choice in the versioned `PlayerPrefs` integer key `CampusUnityCharacterChoiceV1`, separate from quest keys. Value `1` means male and value `2` means female. Missing, zero, or any other value means no choice has been completed.

Startup data flow:

1. `CampusGame.Awake()` locates `PlayerCharacterSelector`.
2. The selector reads and validates the saved value.
3. A valid value activates the corresponding visual.
4. A missing value leaves the game paused in required-selection mode.
5. Automated QA bypasses the prompt with the male character as its deterministic default without overwriting the user's saved choice.

Runtime motion flow:

1. Existing movement code calculates grounded state, actual speed, and requested semantic motion.
2. `CampusGame` calls `PlayerCharacterSelector.PlayMotion(...)`.
3. The selector drives the active Animator.
4. A visual swap reapplies the current motion so the new model does not remain frozen.

## Asset organization

Keep imported third-party files isolated under their publisher/package folder. Store project-owned integration assets under a dedicated folder such as:

- `Assets/Characters/PlayerSelection/Prefabs`
- `Assets/Characters/PlayerSelection/Animations`
- `Assets/Characters/PlayerSelection/Materials`

Do not overwrite `CampusTraveler.fbx`. Retain it until the new system passes all validation so rollback is immediate. Do not edit package demo scenes or depend on package movement/input scripts.

## Compatibility gate and failure handling

Before modifying scenes:

1. Import the Asset Store package using the user's licensed Unity account.
2. Confirm the project compiles without API or serialization errors.
3. Confirm selected male and female prefabs load in Unity 6000.3.26f1.
4. Confirm both rigs configure as valid Humanoid avatars.
5. Confirm URP materials render without pink shaders.
6. Confirm animation clips are present, in place, and usable by both selected models.

If any gate fails, do not modify the three campus scenes and do not remove the old visual. Record the exact incompatibility and return to candidate selection. Runtime validation falls back to the retained `CampusTraveler.fbx` visual if a selected prefab, Animator, renderer, or required state is missing; it logs a clear error rather than leaving an invisible player.

## Scene and builder integration

Update `CampusBuilder.cs` to instantiate a stable player visual host instead of binding future generated scenes to one FBX. Apply the same integration to the current `CampusLiving`, `Campus`, and `CampusPixel` scenes, while treating `CampusLiving` as the primary playable scene.

Scene updates must preserve existing serialized references to the player root. The guide character remains unchanged unless explicitly requested later.

## Validation

### Editor validation

- Project compiles with no new errors.
- Both selected prefabs have valid Humanoid avatars, compatible Animator bindings, and supported URP materials.
- The active visual exposes at least one `SkinnedMeshRenderer`.
- Rendered height remains between 1 and 2.5 metres and feet align with the player root ground plane.
- Only one visual is active at a time.

### Functional validation

- First launch requires a choice before gameplay starts.
- Male and female previews appear correctly.
- Choice persists after restarting the game.
- Pause-menu switching works outdoors and indoors without moving the player or resetting progress.
- Idle, forward/backward walk, run, jump, and fall states play without root-motion displacement.
- Camera orbit, collision avoidance, teleport shortcuts, building entrance/exit, physical stair traversal, and fall recovery still work.
- Quest start, collection, completion, and reset retain their previous save behavior.

### Automated and visual QA

- Update automated QA to choose a deterministic default without changing saved preferences.
- Run the existing campus and interior travel checks.
- Capture comparable male and female screenshots for idle, running, jumping, and an indoor stair location.
- Verify no pink materials, invisible meshes, duplicate cameras, duplicate AudioListeners, or character-height failures.

## Rollback

Until final validation passes, keep the old FBX and its scene data available. The integration should allow restoring the old visual without reverting unrelated campus, library, dormitory, or URP changes.
