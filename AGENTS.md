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
The latest packaged build is 0.4.87. It caches SlopHero's configured joystick
binding lookup while preserving Rewired's polling rate. Version 0.4.86 extends
the default-off solo UI-event pause to disable Unity's live EventSystem
component, based on the measured raycast call path. Neither change has a
matched FPS result; this is not the complete native optimization project.
