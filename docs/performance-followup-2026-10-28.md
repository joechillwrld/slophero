# v0.4.131 performance follow-up, 2026-10-28

## Concrete fixes and scope changes

The newest `Latest.log` shows the rainbow-note pool was discovered, then
`Method unstripping failed` appeared about one second later. It happened again
after manager gaps and option changes. The current build retains the successful
2,000-sprite scan across preference toggles and temporary `GameManager` gaps,
forgets it only on scene changes, and skips list/discovery work between its
20 Hz palette updates. This removes the known repeated generic-scan path. It
does not claim to fix the reported note-animation or white-flame appearance;
those still need a Windows visual check.

Several cosmetic suppressions previously restored merely because play was
online. They now remain eligible for any active chart: particle/fret effects,
the Star Power progress burst, combo-smoke UV animation, and the passive input
viewer. The exact native held-glow, regular-sustain, and fret sorting-order
caches, whammy texture dirty gate, mouse UI/legacy callbacks, and Rewired's
mouse-only sampler also run only during active chart play, online included.
They leave note judgement, keyboard/instrument polling, and network callbacks
alone. Mouse-bound actions are the exception for the mouse-sampler toggle: that
toggle intentionally pauses Rewired mouse polling while enabled.

The hidden-beatline `BeatRenderer` pause, Unity worker-count limit, camera
experiments, Rewired keyboard polling, cheat scanner, and broad UI-event pause
remain offline-solo scoped. Those paths have game-update, input, or callback
side effects that make a blanket online expansion inappropriate.

## New default-off native cache

`PerformanceCacheFretSortingWrites` validates fourteen exact `GameAssembly`
callsites inside `GuitarFretAnimator`'s visible and idle sorting helpers. It
skips only a renderer/order pair already submitted through those helpers;
cache collisions fail open to Unity's setter. All callsite bytes are checked
before patching, and the bridge is removed on pause, focus loss, scene changes,
or when the option is disabled. This is an experiment, not a measured FPS gain.

## Evidence and verification

The Oct 27 PresentMon batch remains a bundled-options CPU-bound reference, not
an A/B measurement: average application presents were 1,706–1,733/s, with
0.55–0.56 ms mean CPU busy and 0.23–0.24 ms GPU busy. It cannot validate the
new cache or the online-scope changes. Do not credit these changes with an FPS
improvement until matched captures isolate them.

Release build: zero warnings/errors. Core checks: 128 passed. Exact-build
native fret-sorting, regular-sustain, and held-glow verifiers all pass against
the supplied `GameAssembly.dll`. Windows runtime behavior, the rainbow visuals,
online transitions, and PresentMon impact still require in-game validation.
The ZIP contains only `Mods/SlopHero.dll`; SHA-256:
`90fe4bec03fd810fd5d719eb02ab6e9ffd83345bae11a038864d5e0b6dc6d5ba`.
