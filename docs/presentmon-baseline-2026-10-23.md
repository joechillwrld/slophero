# Sloptifine baseline: Clone Hero 1.1.0.6142, 2026-10-23

This is the v0.4.75 baseline and historical native research.
[performance-handoff.md](performance-handoff.md) records what has since
been implemented and what the latest v0.4.83 captures show.

The user supplied the raw Intel PresentMon capture
`pmcap-Clone Hero.exe-261023-195523.csv`, its stats CSV, and the matching
`MelonLoader/Latest.log`. This document records conclusions and the next
implementation targets; the raw capture is not committed. Reproduce the
numbers with `python tools/presentmon_report.py <raw-capture.csv>`.

## What this run establishes

The 29.9-second capture contains 37,717 frames from one Clone Hero process and
one swapchain, all in `Hardware: Independent Flip` with VSync interval zero.
PresentMon reports 1,261.2 average FPS, 818.6 1st-percentile FPS, and 997.7
5th-percentile FPS. Its raw frame intervals have 0.793 ms mean, 1.002 ms p95,
1.222 ms p99, and 3.613 ms maximum. There are 23 frames above 2 ms, 10 above
3 ms, and none above 5 ms. Six of the 3 ms frames cluster around capture
second 23.71–23.73. This capture does **not** demonstrate a large ongoing
stutter; it gives a stable baseline for later A/B comparisons.

CPU busy is 0.760 ms mean and 1.179 ms p99. GPU busy is 0.281 ms mean and
0.412 ms p99; GPU utilization averages 20.8%. The largest frame has 3.543 ms
CPU busy and 0.623 ms GPU busy. This run is mainly limited by CPU-side frame
submission/game/Unity work, although PresentMon alone cannot name the CPU
method. `MsGPUTime` is **not** GPU work here: it includes waiting and closely
tracks the present interval. Use `MsGPUBusy` when reasoning about actual GPU
activity. CPU and GPU busy can overlap and must not be added.

The log identifies SlopHero **v0.4.75**, the matching game fingerprint, and
ClonZones. Gameplay started before the capture. SlopHero logged gameplay UI
event suspension from 19:55:20.968 through 19:55:58.708, covering the whole
19:55:23–19:55:53 PresentMon window. Its narrower mouse-process hook was also
installed, but there was no skipped-call summary at shutdown: in this run the
broad UI-event pause likely prevented that narrower path from being entered.
Treat these switches as overlapping, not additive. The whammy Apply cap
matched 156,105 calls and skipped 132,246 across the **whole game session**;
those counts are not restricted to the 29.9-second PresentMon interval.
The column-write match count remained zero. The reported `columnSetPixelsHook`
is its **shutdown** state after scene cleanup, so `False` alone does not prove
the hook was absent during play. Current-game native disassembly explains the
zero matches: its whammy data path writes a `NativeArray<byte>`, not the old
source's `Texture2D.SetPixels` column. Both speculative pixel-write hooks were
removed after that discovery. The working Apply cap remains default-off.

No new game binaries, assets, WPA CSV exports, or symbols are needed to start
the next code batch. The present capture establishes the bottleneck class,
not a method-level native call graph. The older source is a lead generator,
not an exact specification for current obfuscated methods.

## Concrete next code batch

1. **Use the current whammy path for the next experiment.** The exact-build
   native map below identifies a 5 ms CPU byte-write step and a separate
   LateUpdate upload. The existing Apply cap skips many uploads, but does not
   skip byte writes. Those writes already run only when the 5 ms time step
   advances; a second blind rate cap would risk missing whammy data. A future
   upload gate could key off that step's actual progress, with explicit
   handling for setup, resets, pause, and direct writes, and must fail open
   if the exact build binding does not match. Keep GPU compute, whammy input,
   and online timing untouched.
2. **Profile current idle visual callbacks at their actual call sites.**
   `GuitarFretAnimator.Update` still writes seven sprite sorting orders in an
   idle/render path (exact-build native map below). It already checks
   `OnDemandRendering.willCurrentFrameRender`; preserve that behavior. Its
   exclusive cost and unchanged-value frequency have not been measured.
   The current `HighwayScroll.Update` performs `GetPropertyBlock` →
   `SetVector` → `SetPropertyBlock` per update, but also handles video/fade
   state. The existing highway switch freezes the whole updater and is not a
   general replacement. Measure call counts and unchanged-state frequency
   before adding a narrow idle fast path to either method.
3. **Audit enabled Harmony hooks and Unity UI work as one system.** With
   `EventSystem.Update` suspended, the mouse-processing hook has no apparent
   active work to remove. Avoid stacked hooks that do no useful work in the
   same mode. Continue checking Canvas/TMP dirties and current game HUD
   setters; the retained SlopHero counter HUD already avoids changing its
   text and properties when values are equal. Do not claim a Canvas saving
   from a managed callback timing alone.
4. **Keep the high-risk loops separate.** Rewired input, note hit detection,
   scoring, audio sync, and online player state are not candidates for blind
   throttling. Preserve 1000 Hz controller polling and every SlopHero feature.
   A visual optimization should have its own reversible switch until matched
   captures show benefit and normal behavior.

For each proposed performance patch, compare the same chart section and
settings in raw PresentMon CSVs with only that patch changed. Use `p99`, the
count of frames above 2/3/5 ms, and CPU/GPU busy alongside average FPS. A
single favorable average is not proof of a gain. The previous WPA export has
inclusive, overlapping stacks and cannot assign the 0.760 ms CPU work to a
specific current native method on its own.

## Current-game native map (research, no new gameplay patch)

This map is for the supplied v1.1.0.6142 `GameAssembly.dll` (SHA-256
`12bc21e5a88a2fc9abc259d5d67f24835145eabca6c3f863b075ba37ad75f197`)
and `global-metadata.dat` (SHA-256
`5f14231810f2641639167eb72956e85cced2f36cb4a84682bdefb1e9da3e3d42`).
The offsets are image RVAs, not stable addresses across builds.

| RVA | Current native behavior | Consequence |
| --- | --- | --- |
| `0x22B760` | Static whammy updater advances in 0.005-second steps and calls the raw-byte writer only for elapsed steps. | The old-source 200 Hz scheduler exists in a new form; it is already gated. |
| `0x1C7D20` | Writes each elapsed step's 32 lanes into a `NativeArray<byte>`. | A `Texture2D.SetPixels` prefix cannot suppress this work. |
| `0x22BE10` | Static upload wrapper invokes the texture object's upload method. | A possible narrow gate, but all direct writes and initial clears must be preserved. |
| `0x1C74C0` | Small wrapper calls `Texture2D.Apply()` for the CPU texture. | Explains the many Apply matches in the log. |
| `0xB4500` | `GameManager.LateUpdate` calls the static upload wrapper. | Upload attempts can greatly exceed the 200 Hz data-step rate. |
| `0xAA890`, `0xA88E0`, `0xA9ED0` | `GuitarFretAnimator.Update`, visual helper, and idle helper; the idle helper repeats seven `SpriteRenderer.sortingOrder` writes on eligible render callbacks. | Measure before adding a state cache; generic `SpriteRenderer` hooks may cost more than they save. |

The whammy texture uses 32 lanes and 1,024 time slots in this build. Its
static initializer references `_GlobalWhammyTexture`. This mapping explains
the prior zero column-write matches without assuming the hook was broken.
It does **not** establish the cost of the byte writes, sorting setters, or
uploads in isolation; the PresentMon capture gives only whole-frame CPU/GPU
busy. A safe future code change needs an exact-build call path, a default-off
switch, and matched gameplay checks, including practice and online play.
