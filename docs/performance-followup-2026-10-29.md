# 0.4.136 rainbow and CPU-path follow-up

The supplied 0.4.135 solo PresentMon capture contains 49,805 presents in
29.94 seconds: about 1,663 presents/s, 0.601 ms mean and 0.798 ms p99
present interval. Mean CPU busy is about 0.572 ms versus 0.241 ms GPU busy;
52 presents exceed 1 ms, 12 exceed 2 ms, and five exceed 4 ms. There is no
matched A/B in this upload. Rainbow notes and flames were **off** in the
corresponding log, so this capture cannot measure their effect. The clean
30-second song timing window reports Core.Update averaging 0.001 ms.

## Changes

- The rainbow palette previously advanced only within the ordinary 20 Hz
  visual pass. The separate optional palette pass now runs at 60–120 Hz,
  scaled and bounded by the selected hue rate. It integrates phase across
  speed changes, so moving the slider no longer multiplies the new rate by
  total process uptime and jumps the colors. No rainbow work runs when both
  options are off and no override needs restoration. Windows visual behavior
  still needs a test.
- The new default-off **Pause stable SP bar / rock meter attachment scripts**
  option checks exactly two SpriteAttach components in the local guitar
  hierarchy at 20 Hz. It compares screen and camera geometry, reference
  sprite, anchors, and target transforms; after stable checks it pauses their
  native Updates. Any detected change re-enables them, and pausing, leaving
  solo play, scene changes, or switching it off restores them. A visual
  camera/resize change can lag by up to 50 ms. This is narrower than the old
  per-update SpriteAttach cache that sometimes reduced FPS. The older WPA
  row gives the entire SpriteAttach method about 275 ms in 31 seconds across
  all instances; the potential saving for two components is therefore small.
- The enabled combo-smoke switch reported 0 tracked scripts even though the
  current runtime snapshot listed both local UVScroll objects. Its discovery
  required a Rewired player wrapper before examining the guitar. For a
  verified offline solo song, the only active guitar can now be matched by
  the exact `Combo/multiplier/smoke_combo_fast|slow` path without that wrapper.
  Online play retains the stricter controller check. This is a targeted
  correction to an existing optimization, not a measured FPS gain.
- The opt-in 30-second mod timing capture now reports separate rainbow
  palette, meter-check, and combo-smoke-check CPU buckets. These measure the mod's work, not
  Unity's native SpriteAttach time or presented FPS; PresentMon remains the
  frame measurement.

## Additional SpriteAttach screen

The current guitar prefab also has a `combo_back` attachment under
`Combo/streak_meter`. Its placement follows a gameplay-changing streak
display, so an input-stability cache could visibly lag combo changes.
`SpamBar` is a separate root prefab, not an additional guitar attachment.
The total old SpriteAttach sample weight is small and the previous broad
cache regressed in some runs. Neither extra component was added to the
prototype pending a more direct per-instance cost and visual check.

For the next Windows test, verify rainbow notes/flames at a fixed slider
value, then move the slider during a song. Test the meter switch alone,
including pause, resize, camera motion, and Star Power/rock meter visuals.
The existing combo-smoke switch should now report two tracked/paused scripts
in a solo song. If comparing FPS, keep the chart and other settings fixed
and collect separate raw PresentMon runs with the meter switch off and on.

Release validation: the .NET 6 Release build completed with zero warnings or
errors, all 128 Core checks passed, and `git diff --check` passed. The three
existing native verifiers passed before the workspace restarted; those native
hook paths were unchanged in this build. The ZIP contains only
`Mods/SlopHero.dll`. SHA-256:
`d2b5c793207151eb159b196bbd508f63349d1aafcdf766dd8c10de3f323062c0`.
