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
The latest packaged build is 0.4.97. It extends the default-off gameplay
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
