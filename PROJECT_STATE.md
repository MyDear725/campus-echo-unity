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
