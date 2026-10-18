# Project State

## Objective

Add a male/female character choice to the game-start flow and replace the current playable student with suitable free Unity Asset Store models while preserving movement, animation, camera, and interior/exterior travel behavior.

## Verified facts

- Active project root: `D:/大学竞赛/unity游戏/campus-echo-unity-main-full`.
- The project is a Git repository on `main`; the approved design is committed at `ba318f18`.
- The current player model is `Assets/Characters/CampusTraveler.fbx` (about 3.1 MB). Although the FBX contains avatar metadata, `CharacterImport.cs` deliberately imports it as Legacy Animation.
- `CampusGame` directly drives an `Animation` component with named clips such as `Idle`, `Walk`, `Run`, `Jump`, and `Fall`, and also expects facial blend shapes when present.
- The model is referenced directly as an FBX prefab instance in the three campus scenes and is also loaded by `CampusBuilder.cs`; replacing it safely requires an adapter or coordinated scene/builder changes, not a blind file overwrite.
- Existing user changes are present in three library materials and `ProjectSettings/URPProjectSettings.asset`; they must not be overwritten.
- Candidate selection and compatibility design are in progress. No character replacement has been implemented.
- Official free Asset Store candidates identified so far include Supercyan's `Character Pack: Free Sample` (low-poly male, 11 animations, Humanoid, URP-compatible after material conversion), Hodaart's `Low Poly Character Collection 3` (10 characters, 8 animations, native URP but authored in Unity 6000.4.6), and Vefects' `Stylized Character Female` (free, much larger package, limited public technical metadata).
- The existing start/pause UI is drawn by `CampusGame.OnGUI()` and currently has a single `开始 / 继续探索` button. Quest progress is persisted through `PlayerPrefs`.
- The strongest same-style free pair candidate is Hodaart's `Low Poly Character Collection 3`: one 3.7 MB package with male and female characters sharing one material/texture atlas and the same eight animation types. It is native URP, but its listed authoring version (Unity 6000.4.6) is newer than this project's Unity 6000.3.26f1 and therefore requires an import compatibility trial before adoption.

## Workflow

1. Inspect current character rig, animation clips, scale, materials, and scene/code references.
2. Research current free Unity Asset Store candidates and verify licensing/URP/version compatibility from primary sources.
3. Present 2-3 candidates and a replacement approach for user approval.
4. Write and review the approved design before implementation planning.

## Decisions

- 2026-10-18: The player chooses male or female before the first game start. The choice is persisted across launches, and the pause menu will provide a way to change it later.
- 2026-10-18: Use two same-style characters from the free Hodaart `Low Poly Character Collection 3` package, subject to a compatibility trial in Unity 6000.3.26f1 before permanent integration.
- 2026-10-18: Keep the existing player movement root, collision, camera target, travel state, and quest state unchanged; switch only the visual character and its animation driver.
- 2026-10-18: The user reviewed the written design and approved proceeding to implementation planning.

## Changed files

- `PROJECT_STATE.md` — created as durable handoff context.
- `docs/superpowers/specs/2026-10-18-player-gender-selection-design.md` — approved design covering asset selection, architecture, UI flow, persistence, compatibility gates, validation, and rollback.
- `docs/superpowers/plans/2026-10-18-player-gender-selection.md` — task-by-task TDD implementation plan with exact interfaces, commands, commits, compatibility gate, and runtime QA.

## Validation performed

- Confirmed the active project and Git state.
- Located the current model, importer, generated materials, and builder references.
- Inspected the actual Legacy animation integration and all direct FBX scene references.
- Searched current official Unity Asset Store listings and verified free status, stated render-pipeline compatibility, animation inventory, package size, and license type where available.
- Located the exact start/continue panel, reset action, save keys, and automated character checks in `CampusGame.cs`.
- Completed the design self-review for placeholders, contradictions, scope, and ambiguity. The final spec fixes the save key/value mapping, QA default, qualifying-model criteria, airborne animation behavior, and old-model fallback.
- The approved design and this durable state were committed together using the user-specified repository-local Git identity.
- Self-reviewed the implementation plan for design coverage, placeholder text, interface/type consistency, safe handling of existing user changes, and staged-file scope.

## Next steps

- Choose either subagent-driven or inline plan execution.
- Begin with the Asset Store import/Unity 6000.3.26f1 compatibility gate; obtain approval of the exact young-adult male/female prefab pair before runtime integration.

## Git identity resolution

- The design commit was initially blocked because no local Git author identity was configured.
- The repository remote is `https://github.com/MyDear725/campus-echo-unity.git`.
- The user explicitly requested the repository-local author identity `yang6667779-wq <yang6667779@gmail.com>`; no global Git identity will be changed.
