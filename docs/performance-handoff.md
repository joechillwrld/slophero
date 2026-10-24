# Sloptifine current handoff

This is the current starting point for performance work. The detailed
chronology is in [performance-audit.md](performance-audit.md); the raw frame
baseline and exact-build native map are in
[presentmon-baseline-2026-10-23.md](presentmon-baseline-2026-10-23.md).
The user has already supplied logs, assets, game binaries, WPA exports,
PresentMon captures, and a matching UnityPlayer PDB. Read the existing
evidence before requesting another capture.

## Build and evidence

- Latest tested release: SlopHero **0.4.83**, code commit `07a8217`, on the
  private `slophero-v1.1-alpha` branch. The uploaded three-run `Latest.log`
  reports SHA-256 `8DFD151C5664E71831849636E6E95EBC89C603C697DB3300FEDFF95D8AA56E46`,
  which matches the DLL inside `downloads/SlopHero-0.4.83-alpha.zip`.
  Clone Hero is **1.1.0.6142**, Unity **2022.3.62f2**, with ClonZones 2.0.0
  also loaded. There are no SlopHero errors in this log.
- The exact UnityPlayer PDB is
  `UnityPlayer_Win64_player_il2cpp_x64.pdb`, GUID
  `6313482A-F658-4F38-906E-79EC0C4DBC51`, age 1. The earlier
  `UnityPlayer_Win64_il2cpp_x64.pdb` upload has a different GUID and must not
  be used. `BuildGuard.cs` records the exact GameAssembly and metadata hashes.
- The older raw PresentMon baseline measured 1,261.2 average presented FPS,
  0.793 ms mean and 1.222 ms p99 frame interval, 0.760 ms mean CPU busy,
  and 0.281 ms mean GPU busy. It is a CPU-side baseline from v0.4.75, not a
  direct A/B result for 0.4.83. Use `tools/presentmon_report.py` for future
  raw CSVs. Do not equate `MsGPUTime` with GPU work; use `MsGPUBusy`.

## Three 30-second v0.4.83 captures

All three report 100% chart speed, render frame interval 2, Canvas counters,
hit/sustain/fret/star particle suppression, paused MIDI polling, UI-event
pause, mouse UI skip, whammy gate, and the same HUD placement and scales.
The log does not record chart
identity, so it cannot prove identical sections.
The worker state follows the activation/restoration messages.

| Run | Active Unity workers | FC / score colors | Scheduled render callbacks/s | Callback p99 | Largest simulation Update gap | Performance-effects max |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| 1 | 15 | on / on | 1,326.4 | ≤0.99 ms | 39.005 ms | 0.626 ms |
| 2 | 14 | on / on | 1,320.8 | ≤0.99 ms | 8.091 ms | 0.610 ms |
| 3 | 15 | off / off | 1,318.9 | ≤0.99 ms | 7.685 ms | 0.577 ms |

The 14-worker run is about 0.4% lower than the first 15-worker run in
callback rate, while p99 is identical. The third run, also with 15 workers,
is lower still. These differences fit normal run variation; the worker
limit has no demonstrated benefit. The 39 ms outlier happened with the worker
limit **off**, correcting the earlier suggestion that it caused the prior
large outlier. Keep the worker switch experimental and default-off.

Turning off FC and score colors removed the score-graphics work:
149–150 calls at about 0.024–0.025 ms each became zero. That saves only
about 3.7 ms across 30 seconds in the timed managed bucket. The shared
`RingColors.Update` scope still reports calls when the colors are off,
because the visual pass times its early return; the color Harmony hooks
themselves are disabled. The callback p99 and rate show no gain from turning
these colors off in this sample. Preserve the feature.

The optional performance-effects bucket stayed at 0.032–0.038 ms average,
with maxima under 0.63 ms in all three runs. It combines neck effects and
star-progress particles. This is consistent with the v0.4.83 reduction of
full neck-hierarchy scans from every 5 seconds to every 30, but it does not
prove causation; the earlier 9.478 ms maximum was a single mixed-bucket
outlier. A new player neck is still discovered on its next apply pass.
Canvas HUD sync averaged 0.009–0.018 ms, with one 4.821 ms maximum in run 1;
the other two maxima were 0.165/0.228 ms. Treat that as an isolated outlier.

The capture's callback intervals are measured inside the mod. They do not
measure displayed FPS, GPU time, or the full cost of Unity Canvas rendering.
No new PresentMon CSV accompanies the three runs.

## Next code investigations

1. **Exact game visual setters.** The current GameAssembly native map identifies
   `GuitarFretAnimator.Update` at RVA `0xAA890`, which can repeat sorting-order
   writes. ClonZones already installs a fret settle gate in this setup, so
   check its interaction before touching that same method. The native
   `HighwayScroll.Update` path repeats material property-block work and
   also controls video/fades. A narrow unchanged-value fast path may help;
   a blind skip could break visual state. Preserve
   `OnDemandRendering.willCurrentFrameRender` behavior.
2. **Unity UI overlap.** The latest tests have both the broad
   `EventSystem.Update` pause and the narrower mouse-raycast skip enabled.
   During active solo play, the broad pause can prevent the narrower path
   from being called, so do not count their gains as additive. The WPA export
   identified UI raycasting as a real inclusive CPU path, but these three
   captures cannot isolate either switch.
3. **Native engine paths.** The matching PDB names `ExecutePlayerLoop`,
   `PlayerRender`, `RenderManager::RenderCameras`,
   `Camera::CustomCull`, `GfxDeviceWorker::RunCommand`,
   `PlayerMainWndProc`, and job-queue scheduling. These are broad engine
   paths, not safe functions to skip. The WPA stack CSV contains populated
   stacks but unresolved symbols; the method CSV has module offsets without
   a common stack key. Do not request the already-supplied exports again.
   Windows `RtlWalkFrameChain` / `EtwpTraceStackWalk` samples are ETW
   collection overhead.
4. **Scene inventory.** A gameplay snapshot shows 3 cameras, 12 canvases,
   181–201 enabled SpriteRenderers, and one enabled but non-playing VideoPlayer.
   Counts alone do not show cost or uselessness. Inspect a specific component's
   current-build call path before offering a switch, especially where Clone
   Hero already has an equivalent visual option.

Keep each proposed change narrow and reversible. Avoid hot-path logging or
per-frame hierarchy searches. Preserve every existing SlopHero feature,
online behavior, and 1000 Hz controller polling. Do not infer a performance
win until a controlled frame capture supports it.
