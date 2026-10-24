# Sloptifine baseline: Clone Hero 1.1.0.6142, 2026-10-23

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
the hook was absent during play. Its zero match count does show that the exact
`1x24` shape was not observed by that prefix.

No new game binaries, assets, WPA CSV exports, or symbols are needed to start
the next code batch. The present capture establishes the bottleneck class,
not a method-level native call graph. The older source is a lead generator,
not an exact specification for current obfuscated methods.

## Concrete next code batch

1. **Find and gate upstream CPU whammy work.** The existing cap removes many
   `Texture2D.Apply` uploads but does not establish that it skips the writes
   that precede them. The current interop assembly exposes 5- and 6-argument
   `Texture2D.SetPixels` and a `SetPixelsImpl`; v0.4.75 covered only the
   5-argument form and expected the older source's `1x24` shape. This source
   branch now also hooks `SetPixelsImpl` behind the same default-off switch,
   matches only the captured whammy texture and the exact `1x24` base-mip
   shape, and reports separate match/skip counts. It has compiled but **has
   not been tested in the game**. If those counts remain zero, identify the
   actual current call/shape before suppressing any other writes. Keep
   row clears, GPU compute, whammy input, and pause behavior intact. The
   upstream gate should use the existing default-off whammy experiment and
   fail open if a binding is not identified.
2. **Profile current idle visual callbacks at their actual call sites.** The
   old `Fret_Animator.Update` repeatedly sets sprites, masks, sorting order,
   and transforms even when idle; the current build exposes
   `GuitarFretAnimator.Update` and the same head/lift/cover sprite-renderer
   fields. Its current cost and behavior have not been measured.
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
