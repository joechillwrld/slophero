# 0.4.138 scene-entry and rainbow-pool experiments

Four independent switches are default off. They are experiments, not measured
FPS improvements. The ClonZones and native options use exact supplied binaries;
the Release build and static native-bridge verifiers cannot establish Windows
visual or runtime behavior.

## Research and changes

- Decompilation of the supplied `ClonZones.dll` shows that, when its four
  overlay flags are false, `TryGetVisualCache` does not create overlay fret
  renderers. `ApplyPressState` then uses the original base/head/lip sprite
  renderers, and `ApplyFretLayering` re-enables the original renderer layers.
  This removes an approximately 20-renderer overlay path in the supplied pack.
  The new **Use original ClonZones fret renderers next song** option selects
  that existing code path before the next Gameplay scene initializes. It
  accepts only the supplied ClonZones assembly SHA-256
  `2c87cfa737aa84cf247283431f4571170338aa85b7180ded9626359bb6e665d1`
  and restores ClonZones' saved flags on scene exit or mod unload. Layering,
  open-fret art, press art, lift art, and Star Power art still need a Windows
  comparison. The option does nothing when ClonZones is absent.
- The 0.4.136 log enters Gameplay 16 times and prepares three exact-build
  native bridges on every entry. Their callsites already pass through while
  inactive; sustain and sorting tables clear at every activation. The new
  **Retain inactive native bridges between songs** option leaves only selected
  bridges installed across scenes. It disables their flags and discards old
  glow-renderer references and membership before the outgoing scene dies. A
  lifecycle error could crash the game. Each corresponding native cache/glow
  setting must also be enabled to exercise this option. Native callsites are
  fully restored when its feature is switched off or the mod unloads.
- The same log reports separate Rewired mouse/keyboard Harmony installs on
  each Gameplay entry, commonly around 14–16 ms each. The new **Retain
  inactive Rewired hooks between songs** option leaves selected prefixes
  installed but passing through in menus and on pause, then reuses them on
  re-entry. The individual Rewired mouse/keyboard switches must also be on.
  This is a scene-entry hitch test, not a steady-play FPS target.
- Rainbow notes currently discover 500 pooled note groups. With **Cache
  active rainbow-note pool** on, a 30 Hz scan updates the active group list;
  tint updates for that short list still run at their prior 60–120 Hz cadence.
  Deactivated groups are checked on each tint pass. A group appearing between
  membership scans may keep its previous tint for up to 33 ms unless the
  game's color setter updates it immediately. This option only matters with
  rainbow notes enabled and needs a visual check at the strike line.

The exact GameAssembly SHA-256
`12bc21e5a88a2fc9abc259d5d67f24835145eabca6c3f863b075ba37ad75f197`
passed the regular-sustain, fret-sorting, and held-glow native callsite/stub
verifiers. This verifies the static layout and bridge behavior against that
binary, not scene-transition safety in the running game.

## Additional frame-time screen

The usable 0.4.137 “off” PresentMon capture has 46,789 presents over 29.96
seconds. It contains ten intervals above 2 ms; eight are about 4.9–5.8 ms.
The slower events recur roughly every 3.1–3.8 seconds. On those ten rows,
CPU busy occupies 99.3% of the present interval on average, while GPU busy
is about 0.26–0.45 ms; `PSOCompileCount` is zero. This is a concrete recurring
CPU-side frame-time lead. PresentMon alone does not attribute it to SlopHero,
ClonZones, Unity GC, the game, or a driver. The paired “on” capture lasts only
0.035 seconds, so it cannot establish an on/off result. The older source
contains a 3.5-second high-score animation wait, but that coroutine is not
evidence that it causes these recurrent spikes. Native/WPA attribution of a
matching time window is the next research step; no guessed periodic hook was
added to this build.

After packaging, I checked four additional full-length raw captures from the
earlier sustain-cache two-by-two test. Each 30-second run has 9–11 presents
above 3 ms, with about 99% of each slow interval in CPU busy and no PSO
compilation on those rows. The median gap between slow events is about
3.05–3.14 seconds in the `neither`, `skip`, `cache`, and `both` runs. Older
October 26 runs also have CPU-busy spikes, often about 2.2 seconds apart;
the October 28 run has them farther apart. This makes the spikes a persistent
target across several option states, but the changing cadence does not support
attributing them to one fixed 3.5-second timer. The current game callback at
each slow present is still unknown. The source archive's matching 3.5-second
wait belongs to a one-time high-score animation, and its `StartSettingsWrite`
coroutine is triggered on settings writes rather than a gameplay timer.

The current uploaded WPA table is an aggregate stack tree and has no sample
timestamp column, so it cannot be aligned to these slow presents. A future
time-aligned CPU sample trace during the same run would distinguish Unity/game
callback work from collection or driver activity; the saved `.wpaProfile`
alone cannot recover that attribution.

## Compact Windows test

Start with a normal chart and inspect the menu, fret art, note colors, and
input at the strike line. Then enable all four new switches together with
the underlying native sustain, fret-sorting, held-glow, Rewired mouse/keyboard,
and rainbow-note settings you actually want to test. Leave Gameplay and load
the chart again to exercise ClonZones selection and bridge reuse. Check the
frets at rest, press, open notes, lift, sustain, and Star Power; check newly
appearing rainbow notes; pause and resume; restart and enter another song;
then return to the menu and confirm normal mouse/keyboard input. If any visual
or input behavior fails, turn off the relevant new switch and repeat once.
For performance attribution, compare complete equal-length PresentMon runs on
the same chart with one new switch changed at a time. The retention switches
target scene load rather than the 30-second steady-play average.

Release validation: .NET 6 Release build completed with zero warnings/errors,
all 128 Core checks passed, `git diff --check` passed, and the three native
callsite verifiers passed on the supplied GameAssembly. The ZIP contains only
`Mods/SlopHero.dll`; SHA-256:
`57ea98e61e06d3db8ab501973ca07e11580957d8f05e078df522229543fef44b`.
Windows gameplay and presented-frame impact remain unmeasured.
