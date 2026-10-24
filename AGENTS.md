# SlopHero work guide

Read [docs/performance-handoff.md](docs/performance-handoff.md) before changing
performance code. [docs/performance-audit.md](docs/performance-audit.md) is a
chronological research record; older "next steps" there may be superseded.

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
