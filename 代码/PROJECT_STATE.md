# Project State — 2026-10-20 local review

## Outdoor glare repair — 2026-10-23

User identified floating white light blobs, rather than geometry or UI flicker. Visible Windows framebuffer A/B confirmed outdoor Bloom (0.12) expands isolated highlights into white screen glows; disabling outdoor Bloom removes the blobs at the same pose (14,804 fewer expanded bright pixels, no newly bright pixels). Retained tone mapping, daytime lighting, physical night lamps and independent interior presentation. No GUI-state or vSync edits were made. Rebuilt Windows player; verified camera rotation, bridge approach and 19:00 lamps with isolated-save `CampusGlareQA`. See `QA/Submission/光斑修复.md`. Not committed or pushed.

## Submission organization and anonymity — 2026-10-23

Integrated remote `c687bed3` before organizing the repository; retained the local final environment repairs. The repository root now contains `代码/`, `README.md` and `启动游戏.cmd`; GitHub Desktop keeps the existing repository root, while Unity Hub must open `代码/`. The portable Windows launcher builds saved scenes when source changes, then starts the current player.

Current source, map data, serialized scene names/signs, UI, menus and documentation use anonymous campus wording. Historical QA media and the real-campus balcony photograph are archived outside the repository; the balcony now uses an in-game garden render with the same resource GUID. Third-party attribution remains intact. Existing Git history has not been rewritten and is outside this current-version anonymity check.

Windows verification: launcher rebuild succeeded; 16 serialized scenes / 48,490 objects / 777 text components passed the external identity-term audit; 15 building entrance round trips and map display/traversal passed; updated dormitory runtime passed 210 checks, daily activities passed 63 checks. Original gameplay saves were isolated from QA. See `QA/Submission/README.md` and `docs/仓库目录说明.md`. These changes are not committed or pushed.

## Windows environment inspection — 2026-10-22

Local work after `d14df57b`: final inspection of all 14 enabled scenes, the connected 48-road campus network and 15 entrance round trips. Fixed 25 reflected outdoor BoxColliders, one junction lamp, 9 route endpoint/entrance mismatches, 87 library signs, 6 duplicate cafeteria stair-post renderers and HUD/map text layout. Reproduced D3D12Core shutdown crashes on this laptop; Windows now defaults to D3D11 and traversal runs exit normally. Real Windows editor GUI regression: library booking/study 62/62 and ordinary-day plan 22/22. Earlier Mac evidence below is retained as historical context. See `QA/FinalAudit/README.md` for exact checks, standalone results, frame timings and limits. These final environment changes are local and have not been committed or pushed.

## Current baseline

Remote main at `392ccd0` already includes UMA male/female player selection and persistent choice. The earlier character-selection design/planning notes remain in `docs/superpowers`; the old statement that runtime selection was not implemented is obsolete.

## User-authorized work

Build warm new-student onboarding with Xiaolan instead of memory collection, plus six logical floors of dormitory public corridor: central entrance, horizontal double-sided corridor, 12 numbered closed room doors, stairs at both ends, interaction-based floor transitions. Bedroom-door interaction and bedroom interiors are deferred. User confirmed this interpretation on 2026-10-20.

Work on Mac, demonstrate locally before GitHub upload. User approval of the result is still pending; no push performed. Local branch: `local/welcome-dormitory-review`.

## Implemented locally

- Seven dialogue stages with two choices each; separate persisted progress, resume and reset.
- Milestones driven by second-floor arrival, cafeteria arrival, completed tray return, teaching-building arrival, actual library study start, and return to dormitory ground floor.
- Runtime-generated public corridor for all existing `dormitory-*` scenes, preserving travel contract. Six logical floors reuse geometry; 101–112 through 601–612; floor one is the sole campus exit.
- Local interactive browser preview at `QA/WelcomePreview`, with shared story/layout JSON. Browser preview is not Unity footage.

## Validation

49 isolated checks passed in actual Unity 6000.3.26f1. The component harness uses stubbed game services. Browser screenshots/checks cover the approximately 410px IAB width. The original project has since been run on Mac; see the real-project audit below. Windows runtime and wider desktop browser layout remain unverified.

## Review and next step

Start with `QA/WelcomePreview/README.md` and `迎新剧情稿.md`. Let user inspect preview and request changes. Do not push until user approves this concrete result. Before pushing, inspect remote changes and preserve any Windows work committed meanwhile. Full Windows gameplay verification remains necessary.

## Real-project audit — 2026-10-20

Original assets are now downloaded and the actual project compiles/loads CampusLiving. Real UMA male/female avatars were exercised through real dormitory entry/exit, capsule movement and floor transitions. Missing UMAIgnore/UMAKeepChain tags were found and added. Actual third-person captures exposed leg cropping; a dormitory-only FOV/target adjustment was added and the outside FOV is restored. Final full traversal retest passed 237 assertions and produced 17 actual camera renders; scope is movement/travel and third-person framing in the original Mac Unity project, not full visual release or Windows validation.

Deferred visual issue (user instruction): original UMA skin/eye/mouth overlay references do not resolve in this clone, and actual rendered characters show white skin and abnormal materials. See QA/WelcomeIntegration/character-assets/overlay-textures.txt and README.md. This is not a fully verified visual release; Windows tests and original texture dependency repair remain outstanding. No push.

## Xiaolan/stairs revision — user requested

Original texture issue explicitly deferred by user. Fixed Xiaolan runtime UMA slot initialization and completion-time locomotion binding; actual displacement drives idle/walk, humanoid scale matches current player. Descending stairs now descend, ground slab no longer covers their well, platform accommodates the original capsule, terminal floors have safety barriers. Real CampusLiving revision run passed 102 assertions on Mac, including baked male/female standing-height comparisons, actual animated limb motion and floors 1–6 traversal through the original E dispatcher. See QA/WelcomeRevision/README.md and actual render/GIF evidence. Windows not run. No commit or push.

## First semester extension — 2026-10-20

User explicitly authorized implementing the semester/time-system draft in the actual Unity project, then asked to continue. Fixed calendar, timetable/reminders, contextual real-room attendance and seating, library targeted learning/homework, energy/allowance, 24 two-choice story beats through finals/ending, grades/credits/award pre-evaluation, ordinary-day scheduling, and complete daily/semester rollback with backup are implemented locally. Shared wallet/inventory/reservation and the original E dispatcher remain the sources of truth.

Original CampusLiving Mac Play Mode validation passed 253 checks; evidence and explicit coverage limits are in `QA/Semester/README.md`, `result.txt`, `unity-final.log`, and seven Game View captures. Later story tests set dates/prerequisites directly; this is not a continuous manual semester playthrough. Three-course assessments currently use automatic character-preparation scoring rather than a standalone question bank. New characters use identity-based location dialogue; additional NPC models, voice, a camera photography system, physical exhibition and next-semester scholarship disbursement are deferred. Existing UMA material issue remains deferred per user instruction. Windows remains unverified. No commit or push. See `docs/2026-10-20-第一学期实现说明.md` for controls and actual implementation boundaries.

Standalone macOS development build completed successfully (13 scenes, approximately 1.3GB), then the built player passed the same 253 isolated checks. See `QA/Semester/player-result.txt`, `mac-player.log` and `player-captures/`. Existing `启动游戏.command` launches the new local build.

## Route line and day/night — 2026-10-20

User requested a toggleable line to tracked objectives, gradual daylight changes, and lights at 18:00. Added collision-aware outdoor/indoor ground routing, doorway and dormitory transition targets, persistent line display preference in the existing handbook, and tracked target persistence in semester snapshots. Added fixed-clock daylight/sunset, sky/ambient/fog and exterior shader night shading, existing courtyard lamp heads plus path lamps, on at 18:00/off at 06:00 with a bounded eight-light pool. Interior authored ambient light is preserved. Original-project Mac Play Mode passed 41 focused checks and the existing 253-check semester regression. See `QA/WorldGuidance/README.md` and actual captures. Windows not tested; original UMA overlay issue remains deferred. No push.

The route/day-night macOS build completed successfully (13 scenes, approximately 1.3GB) and its standalone Player passed all 41 focused checks; see `QA/WorldGuidance/player-result.txt`, `mac-player.log` and `player-captures/`. Existing local launcher now opens this build.

## Publication authorization — 2026-10-20
User authorized publishing the current semester, route and day/night updates directly to GitHub main, with README updated and packaged apps excluded. Earlier no-push notes above record historical review stages and are superseded for this publication. Source/assets, final test results and representative screenshots are included; local builds and intermediate debugging captures remain local. Windows runtime remains unverified.

## Library booking and study repair — 2026-10-22
Local changes add missing fourth-floor seat markers (192 chairs total), repair the registry MonoScript, preserve existing seat IDs and route seat E through the main dispatcher. After the user reported remaining phone/UI issues, added area-separated seat selection with explicit selected-ID feedback, a dedicated seated subject-study panel, modal input ownership and correct in-use reservation lifecycle. Final original-project Mac editor GUI flow: 62 checks passed, covering mouse selection/booking, all three subjects at 30/60/90 minutes, exact time/energy/mastery, conflict confirmation/cancel, insufficient energy, cross-midnight rejection, standing/release and forced-exit cleanup. QA/LibraryFlow documents evidence and limits; prior 1173 seat checks did not cover this full GUI path. Windows and standalone players were not rebuilt. These repairs remain uncommitted and unpushed.

## Ordinary-day plan feedback repair — 2026-10-22
The reported screenshot shows Sep 1 at 08:05, violating both first-week and before-08:00 restrictions. Availability preview and RoutineDay now share a read-only validator, show the current reasons and disable unavailable confirmation; semester-end validation is included. Confirmation buttons remain outside the scrolling content. Failed transactions preserve the confirmation and visible feedback; successful actions retain their result in the main content. Original-project Mac editor regression passed 22 checks, including actual IMGUI mouse confirmation/cancel, 07:59/08:00 boundaries, changing balance, exact 20-yuan charge and 21:00 advance, no-op rejection, assignments and important days. Evidence and scope: QA/RoutineDay/README.md. Existing UMA assembly-reload issue persists; Windows/standalone not rebuilt. Local repairs remain uncommitted and unpushed.

## Patch publication authorization and integration — 2026-10-22
User authorized publishing the completed library/ordinary-day repair patch to GitHub main and explicitly required preserving teammate changes. Merged remote 1569bb80 (including 71e2ea18) without conflict. Compared the final patch against that remote: only reviewed repair files/documentation/QA differ; remote library stair/dome authoring, CampusMapUI, VRoid character labels, world assets and settings remain. Re-ran library GUI 62/62 and ordinary-day 22/22 on the combined source. README updated; builds, logs and unrelated captures excluded. Earlier unpushed notes above describe prior stages and are superseded by this publication authorization.

## Environment rendering refinement — 2026-10-22

User requested a more polished campus environment. Local first pass adds world-scale grass variation, restrained outdoor ambient light, foliage backlighting, procedural water/sky reflection and ripples, sunset sky colours, an outdoor-only grading volume, 4x MSAA and finer soft shadows. Campus layout, meshes, collision and interaction contracts are retained; simple plant silhouettes remain an asset-quality limitation. Three matched before/after editor views and documentation are in `QA/EnvironmentQuality`. Editor Play Mode and the rebuilt standalone Mac player each passed 49 route/day-night/presentation lifecycle checks with isolated progress keys. Full Mac build succeeded (14 scenes, about 2.0 GB) and the existing launcher opens it. Windows graphics/performance and sustained memory usage remain unverified. This refinement is local and has not been committed or pushed; pre-existing balcony-glass material edits are preserved.

## Story and daily life extension — 2026-10-22

User requested richer story and daily activities. Added 15 beats (39 total): continuing classmate exchanges, club-specific programming/photography stories, and three daily-participation memory stories. Added 12 two-choice daily templates, three date-stable optional offers per day, real-place/time/energy/course-conflict validation, bounded growth/rest effects, persistent choice history, later dialogue recalls and sleep recaps. The handbook has a new daily-life tab and first-night guidance. Old saves acquire an empty daily history without reset; snapshots include the new state through the existing semester key. Actual-project semester regression passed 363 checks, daily backend passed 54, and the rebuilt visible Mac player passed 63 including real GUI rendering and the exact preview/cancel/confirm callbacks. Physical mouse hit testing was not validated: injected editor input was ignored and native desktop inspection timed out; failed attempts and explicit scope are documented in QA/DailyLife. Screenshots cover 1440x900 and 1280x720. Full local Mac build updated (14 scenes, about 2.0 GB). Windows and frame-rate profiling remain unverified. No commit or push; earlier visual changes and the pre-existing glass material edit remain.

## Dormitory collision and interior refinement — 2026-10-22

User requested dormitory void prevention, full walls instead of first-floor stair barriers, and more polished hallways/bedrooms. Closed terminal stair mouths with full-height plaster/sage/skirting walls, hide unavailable flights, enclose floorless flight-side strips with masonry and add stair roofs/foundations. Removed 84 tiny floor-seam colliders. Dorm-specific recovery now preserves the active room/floor rather than spawning in the hidden corridor. Added hallway stone borders, noticeboards and oak trim, two unshadowed stair fills, and bedroom fabric variations, linen curtains, bedding, desk supplies, storage and balcony herbs; redundant crude lamps and flower columns are hidden. Decorative meshes combine per material with colliders/labels retained and runtime resources released. Six shared-layout dorm entrances are covered; the seventh retains its authored split-level structure. Editor first pass passed 200 physical checks; the final 14-scene/2.0GB Mac application passed 210 including jump boundary probes, real stair interactions, opposite-side rooms, recovery, all dorm entrances and render/material/batching checks. Final captures/results are in QA/DormitoryPolish/player. Original launcher opens the updated app. No FPS benchmark or Windows test; existing editor UMA reload exception and two shutdown native allocations remain. No commit or push; all earlier changes and pre-existing glass material edit preserved.

The existing route regression caught the old downstairs goal touching the new inner stairwell wall. Moved the goal to the physically accessible landing inside the E interaction zone. Rebuilt the complete Mac application; all 49 original route/day-night/presentation/reset checks passed. Failure and final success are retained in QA/DormitoryPolish/world-attempt-1-result.txt and world-result.txt.

## Full chronological story replay and repairs — 2026-10-22

User requested rewalking every story and fixing discovered bugs. The existing 363-check baseline passed but its direct TrySit/prerequisite injection missed a real blocker: the persistent LibraryStudy retained a destroyed CampusGame reference after semester/day restoration, making SeatNearPlayer return null before E could reach TrySit's own rebind. Preserved the failed second journey and a minimal before-fix reproduction; rebind now occurs before proximity selection. Corrected welcome text that incorrectly allowed the seventh dorm's LG/1F hall to satisfy a six-storey dorm objective, replaced stale bedroom-unavailable text with current room/rest instructions, and hid the persistent HUD while the handbook owns the screen so its close control remains visible.

The final complete Mac app (14 scenes, about 2.0 GB) passed 4612 checks across four fresh chronological journeys, covering all 78 semester choices, 14 welcome choices and 24 daily choices. Prerequisites come from actual meals/classes/study/submissions/daily participation/exams; no story flags, club selections, grades or daily milestones are injected. Travel poses skip repeated walking; passive seconds are frozen while normal wait/rest/actions advance the calendar through January 17. Save/reload, re-starting welcome, both club branches, memories and rewards are included. Actual UI screenshots at 1440×900 and 1280×720 confirm the close control is exposed. Tests use isolated save keys; no native mouse hit testing, Windows or frame-rate claim. QA/StoryJourney retains failures, intermediate scope, final results, per-choice coverage and final route states. Earlier rendering/story/dormitory changes and pre-existing glass edits are preserved. No commit or push.

The same final app subsequently passed the existing 363-check semester regression and 63-check daily/UI-callback/legacy-save regression. These focused fixtures intentionally set state and are documented separately from the four natural-unlock journeys. All final results are under QA/StoryJourney.

## Publication authorization — 2026-10-22, current refinement bundle

User explicitly requested uploading the current updates to campus-echo-unity. Fetched origin/main and confirmed it matches the local pre-change baseline d14df57b, so no teammate merge is required. Publication includes all current source/resource changes (including the pre-existing balcony-glass blend adjustment), four implementation documents, representative real captures and final/failure verification evidence. Packaged players, caches, logs and duplicate diagnostics remain local through .gitignore. Earlier unpushed notes are historical and are superseded by this authorization. Previously completed Mac runtime verification remains valid because publication preparation changes only documentation and ignore rules; Windows remains unverified.
