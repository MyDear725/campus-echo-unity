# Project State — 2026-10-20 local review

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
