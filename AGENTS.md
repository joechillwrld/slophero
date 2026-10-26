# SlopHero work guide

Read [docs/performance-handoff.md](docs/performance-handoff.md) before changing
performance code. [docs/performance-audit.md](docs/performance-audit.md) is a
chronological research record; older "next steps" there may be superseded.
The handoff's **Still open: evidence to turn into code** section is essential.
The existing Sloptifine switches implement only part of Sol's research. The
unresolved `il2cpp_runtime_invoke` caller, Unity native attribution, and
current-game visual setters remain investigations; do not describe them as
implemented or ship a tiny mod cleanup as the complete optimization batch.
Use the existing binaries, symbols, source archive, and captures to pursue
several independent leads before building. A diagnostic log or another
capture request alone is not the performance work the user asked for.
The newest aggressive candidates and exact callsite/shader evidence are in
`docs/aggressive-render-targets-2026-10-26.md` and its linked JSON. The
0.4.117 Windows log confirms three sustain prototypes activate, but HUD merge
reaches another stripped group in `CommandBuffer`. Version 0.4.118 prototypes
native Unity draw bindings for HUD merge, keeps glow suppressed against game
re-enables, prepares cache hooks before song play, and changes combo-smoke
discovery. These fixes compile but need runtime and frame validation. See the
performance audit and handoff. Do not report gains as measured.
The handoff indexes current mod code, older source, exact-game binaries,
Unity symbols, logs, captures, and local raw uploads. `references/` and
`/workspace/attachments` are not committed to Git; in a fresh environment,
restore needed files from conversation uploads. Inventory any new uploads and
update the handoff with verified findings.

The user wants SlopHero's existing features preserved and wants Sloptifine to
improve both the mod and Clone Hero itself. Favor current-build game/Unity work
that the game's own settings cannot already do. Keep 1000 Hz instrument polling,
note timing, audio synchronization, online play, and profiles intact. Treat
the older source archive as a lead, not an exact map of v1.1.0.6142.

Use measured evidence. SlopHero's "scheduled render callbacks" and Update
intervals are not presented FPS. PresentMon's raw frame intervals and CPU/GPU
busy provide frame evidence; WPA sampled stack weights are inclusive and
overlap. Never claim a gain from one favorable average or a single outlier.
Use the matching UnityPlayer PDB GUID in the handoff; an earlier PDB upload
had the wrong GUID. Do not ask the user to repeat the supplied WPA exports.

Before releasing code, build against `references/`, run
`tests/SlopHero.Core.Checks`, and keep MelonInfo, project version, README,
and ZIP name aligned. Package `Mods/SlopHero.dll`. The user needs the GitHub
download link on the existing private `slophero-v1.1-alpha` branch; workspace
artifacts have failed to download for them. Do not make the repository public.
For a documentation-only change, update the docs without inventing a new
binary version.
The latest packaged build is 0.4.118, a native HUD and sustain follow-up. It
needs an in-game activation check, especially for the native HUD experiment.
The 0.4.114 camera cap
and previous camera-test findings remain below. PresentMon captures are
directional, not a matched A/B, and do not establish a FPS gain. Version
0.4.113 removes the remaining activation gate that blocked
the guitar-camera render benchmark with black-background mode on.
It preserves the black screen by moving the clear to HUDCam for the test, then
restores its prior state. Version 0.4.112 enlarged and bottom-anchored the menu
illustration and thinned the selected-page stripe. Version 0.4.111 added
default-off, offline-solo HUD and guitar-camera render
benchmarks, plus pausing a chart video when its background camera is hidden.
Both camera tests remove different visuals and need a matched PresentMon A/B;
no FPS gain is claimed. Version 0.4.110 adds a
default-off guitar-camera far-clip experiment (120 to 80 units) during eligible
offline solo songs. The setting checks the known camera configuration,
restores on scope exit, and can hide distant notes/effects. Its visual behavior
and FPS impact need an in-game A/B. Version 0.4.105 moved setup of the optional legacy-mouse detour
and Rewired mouse prefix out of the first active song update. The first-song
hitch benefit remains unmeasured. Version 0.4.103 fixes Sloptifine 3 discovery after
0.4.102 hit an unsupported Unity lookup and used the wrong Animator type.
The PB popup and side glows use `UnityEngine.Animator`; Clone Hero also has an
unrelated custom `Il2Cpp.Animator`. Discovery now uses the live
HighScoreDisplay and StarPowerController references without a scene-wide
Animator scan. The side-glow switch deactivates only its two cosmetic branches
during eligible solo play and restores their original active states on
pause/exit; its controller parent remains live. Hide fret strings also pauses
the five cosmetic `SpriteAttach` components on those hidden strings and
restores their previous enabled states when shown. The TMP scale experiment's
ancestor check now recognizes Unity's Animator as well as the game's custom
sprite Animator. These optimizations have no measured FPS result yet. The
older ScoreManager source does not justify replacing the current score-display
path, which already guards at least one sprite-font update when unchanged.
Version 0.4.102 looks up the personal-best display and Star Power side glows
through live Gameplay components after v0.4.101 failed to find either target
by hierarchy path. Version 0.4.101 relaxes the new Sloptifine 3 target
lookup, excludes the separate CenterGlow from the side-glow switch, and
reports a missing live target once. A Windows activation test is still needed.
Version 0.4.100 adds separate default-off Sloptifine 3
switches for the personal-best popup and Star Power side-glow visuals during
offline solo Quickplay. Their presented-FPS impact is unmeasured. Version
0.4.99 moves the optional TMP candidate scan
to Gameplay scene initialization when already enabled, and keeps the native
legacy-mouse and mouse-UI skips offline solo. The PresentMon report tool now
supports one-second presented-FPS windows from raw captures. Runtime impact
is not yet measured. Version 0.4.98 avoids brief Unity worker-count
reconfiguration, stops TMP work after finding no eligible Gameplay labels,
and skips empty mod IMGUI callbacks while the Canvas counter is visible.
These have no matched presented-FPS result yet. Version 0.4.97 extends the default-off gameplay
UI-event pause to suspend the three transparent HUD drag-handle Canvases
during eligible solo songs; they restore when visible or UI events resume.
The optional SpriteAttach cache remains default-off because its full input
comparison can cost more than the native update. Version 0.4.96 extends the default-off hidden
leaderboard option to pause 24 score-list TMP labels only while the list is
fully invisible offline, and now also pauses 30 score-list Image components.
It additionally pauses the list's dedicated world-space Canvas, CanvasScaler,
and GraphicRaycaster under a guarded scene-shape check. It restores prior
states on reveal or online play. The exact scene inventory and limits are in
the handoff. Version 0.4.93
corrects the optional SpriteAttach
cache so a skipped original update no longer recaptures all inputs and outputs;
the hook stays pass-through on pause and menu transitions until scene exit or
setting disable. Three experimental offline-solo settings now share one game
state read. These changes compile, but have no Windows FPS result yet. Version
0.4.92 adds the default-off background-camera suppression. Version 0.4.91
adds default-off Rewired mouse-only
polling pause and an exact-build `SpriteAttach.Update` input/output cache,
both limited to offline solo guitar Quickplay. The mouse switch leaves
keyboard/controller and 1000 Hz instrument polling intact; mouse-bound actions
pause during a song. The cache checks camera/sprite/layout inputs and prior
outputs on every call, so its net performance is unproven and could be
negative; it fails open to vanilla. Both are runtime-unverified prototypes.
Version 0.4.89 added a default-off exact-build native detour that skips Unity's
legacy camera mouse callbacks only during active gameplay, and removed the
mouse-raycast hook's per-call diagnostic counter increment. Version 0.4.88
adds the default-off TMP scale-update experiment. Version 0.4.87
caches SlopHero's configured joystick binding lookup while preserving
Rewired's polling rate. Version 0.4.86 extends
the default-off solo UI-event pause to disable Unity's live EventSystem
component, based on the measured raycast call path. Neither change has a
matched FPS result; this is not the complete native optimization project.
