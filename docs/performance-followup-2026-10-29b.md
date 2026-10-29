# 0.4.137 color controls and performance follow-up

The new raw PresentMon “off” capture has 46,789 presents over 29.96 seconds:
about 1,562 application presents/s, 0.640 ms mean, 0.804 ms p95, and
0.958 ms p99 present intervals. Mean CPU busy is 0.609 ms versus 0.254 ms
GPU busy. There are 322 presents over 1 ms and ten over 2 ms. The “on”
capture has only 55 presents over about **0.035 seconds**. Its computed FPS
is not a useful song average, and these files do not establish whether the
meter option helps or hurts. The 0.4.136 log has no 30-second timing capture
or meter activation message, so it does not identify the tested switch state.

## Changes

- The rainbow-note and flame palettes share new hue offset, lane hue spread,
  saturation, contrast, and brightness controls. Their defaults reproduce the
  existing palette. Each palette is computed only at its existing bounded
  60–120 Hz cadence, not per sprite or input poll. Color-only profiles carry
  the new values.
- The settings menu now applies a configurable surface tint and strength to
  its frame, navigation, controls, and value popup. The existing accent color
  still controls highlights. Tint strength defaults to zero, preserving the
  prior theme, and this work runs only while the menu is drawn.
- The combo-smoke switch resolves the two exact guitar-prefab child paths
  instead of calling scene-wide `FindObjectsOfType<UVScroll>()` every two
  seconds. This reduces its discovery work; the two animations still pause
  and restore under the existing eligibility rules. A Windows log should
  confirm two tracked components with the switch on.
- The default-off stable-meter experiment now reuses its two comparison
  snapshots and target arrays at each 20 Hz check. The previous version
  allocated new managed snapshots and arrays every check. The input-change
  and restore behavior is unchanged; this only removes avoidable managed GC.

## Further screen

The log enters Gameplay 16 times and prepares both the held-glow and regular
sustain native bridges on every entry. Their tables are cleared on activation,
so retaining an inactive bridge *might* remove some scene-entry patch work.
However the held-glow implementation also keeps a warm list of renderer
wrappers from the current scene, and retaining its patch requires explicit
scene-data cleanup, another mod's detour coexistence check, and proof that
its disabled flag passes every call through. The user asked for research
before code, so this build leaves native bridge lifecycles unchanged.

The uploaded ClonZones assembly supports switching off overlay creation and
usage, which would remove roughly 20 extra fret renderers in this pack.
IL inspection confirms that, with overlay usage off, `ApplyPressState` writes
the pack's base, head, and lip frames to the original BaseRenderer,
HeadRenderer, and HalfCoverRenderer. `ApplyFretLayering` re-enables those
original renderers and sets their intended sort orders; its lift branch also
falls back to the original LiftRenderer. The existing ClonZones setters
already read and compare sprite pointers, enabled states, and sort orders
before writing, so another blanket setter cache would duplicate that work.
The original-renderer path still changes fret layering and possibly art;
`HideVanillaOverlayLayers` is called in both modes. The implementation is
already selectable in ClonZones' `[fret]` config; a visual comparison at
rest, press, Star Power, open notes, and sustain is needed before SlopHero
could present it as a safe option. The user requested this visual research
first, so this build leaves that mode unchanged.

Neither the two code-level performance cleanups nor the color controls have
a measured FPS gain. The next useful frame test is a full 30-second run per
setting on the same chart, with the opening transient excluded from both.

Release validation: .NET 6 Release build completed with zero warnings or
errors, all 128 Core checks passed, and `git diff --check` passed. The ZIP
contains only `Mods/SlopHero.dll`; SHA-256:
`9d96bdb70e4d3f31f89b05cb03391126da88fbf6d9ea7b8be497a8c53d444d6e`.
Windows visuals and presented-frame impact still need testing.
