# Performance audit notes

## 0.4.43 targeted changes

The mod compiles against the user's re-uploaded v1.1.0.6142 IL2CPP and
MelonLoader references. Windows gameplay measurements are still needed.

- When the pre-song speed row is absent, `SongSpeedControl` used to call
  `GameObject.Find("SongOptions")` on every unlocked menu update. Missing-object
  discovery is now capped at 20 Hz; once the menu object is found, digit input
  continues to run every frame so brief presses are not missed.
- With score colors enabled but no highway or speed overrides, the visual pass
  still traversed players at 10 Hz between the 5-Hz score-color refreshes.
  Score-only mode now omits those in-between player traversals.
- Optional MIDI/Discord service switches used to discover both managers every
  0.5 seconds even when both switches were off. Default-off mode now skips
  discovery, while enabled switches and restoration remain active.
- Enabled particle/effect switches still discover newly created or reactivated
  effects once per second, but avoid repeating `Stop`, `SetActive(false)`, and
  `enabled = false` writes to effects that are already suppressed.

These changes remove identifiable work; they do not establish a whole-game FPS
gain. A controlled in-game timing comparison is needed before claiming one.

This note summarizes what the supplied timing logs and the SlopHero hot paths
currently establish. The latest `Latest.log` attached with v0.4.33 contains no
timing capture; it records the settings-page exception described below, so it
cannot measure current gameplay cost.

## Measurements from the supplied logs

The clearest matched signal is the visual-override dispatcher. In v0.4.31
captures it ran about 2,303–2,598 times per second, effectively once per game
update. In v0.4.32 the 20 Hz gate reduced that to 595 calls over 30 seconds
(19.8 per second), roughly 116–131 times fewer entries. In that v0.4.32 capture,
the override callback averaged 0.014 ms, score graphics 0.030 ms (inside the
override callback), and the main mod `Update` 0.004 ms. The counter HUD repaint
averaged 0.015 ms across 28,961 repaints, about 965 repaints per second.

The broad frame-time sample in the same capture had p99 at or below 0.69 ms and
one 85.845 ms maximum. The largest recorded mod callbacks were 0.142 ms for
`Update`, 0.601 ms for visual overrides, and 0.556 ms for HUD repaint. These
measurements do not prove what caused the isolated frame spike, but they do not
show a mod callback of comparable duration. Earlier v0.4.31 captures also had
frame maxima above 120 ms, so that spike pattern predates the 20 Hz change.
Frame timings are whole-frame samples; callback timings cover only the named
mod methods and should not be conflated with total game CPU/GPU time.

Using the rounded averages from that v0.4.32 capture, the non-overlapping
`Update`, visual-dispatch, and HUD repaint buckets account for approximately
0.77 CPU-seconds in the 30-second sample, or about 2.6% of one fully occupied
CPU core. This is an estimate from rounded callback averages, not a profiler
measurement of total process cost. The steady callbacks are small; the large
frame maximum is the more important unresolved symptom. Across the paired
v0.4.31 and v0.4.32 captures, dispatch frequency is the clearest comparison:
about 2,303–2,598 entries/s before versus 19.8/s after. Those logs are not a
controlled A/B for total FPS, so they do not support an exact whole-game
speedup claim.

## Current hot paths

- `Core.OnUpdate` runs at the game's unlocked update rate. The measured average
  is about 4 microseconds per call. Chart-stat formatting and note scans are
  gated to 5 Hz and are skipped when their fields are hidden.
- The custom counter HUD draws one combined background/text control per repaint.
  The measured average was about 15 microseconds. It is now the largest steady
  SlopHero callback in the latest measured capture; replacing it with a more complex
  render path is not justified without a matched benchmark.
- Visual overrides are dispatched at 20 Hz. Score-color traversal is limited
  to 5 Hz, the practice clock sample to 20 Hz, and renderer compensation to
  10 Hz. These limits were added because earlier logs showed the dispatcher
  running thousands of times per second despite its inner work being throttled.
- `OnLateUpdate` used to resolve `GameManager` and read settings before the
  20 Hz guard on every game update. It now checks the cadence first, then reads
  preferences and Unity objects only at the 20 Hz work cadence. The
  practice-mode flag comes from the already-running main update path. This
  cuts those repeated checks and active override manager lookups from the
  unlocked update rate to at most 20 per second, with settings taking effect
  within 50 ms.
- Optional MIDI and Discord service discovery is internally limited to 2 Hz.
  Particle and fret-effect discovery runs at most once per second while those
  opt-in switches are active. Neither path scans the whole scene each frame.

## Asset and code inspection

The supplied `resources.assets` parts were reassembled and parsed with
UnityPy. Clone Hero's gameplay score, star-progress, and song-progress graphics
are `SpriteRenderer`s. The prior resize outline searched only Unity UI
`Graphic`s, which explains why it could not find bounds for those gameplay HUDs;
the current editor collects both UI graphics and renderer bounds. In the Star
HUD prefab, `StarProgressBG` is a separate sprite renderer beside `ProgressBar`
and `ProgressBar_End`; `StarProgress.starCountBG` refers to the empty-track
renderer. It must be excluded from the generic star tint pass.

The current release's resize-page failure was traced from the log stack to
`GUI.SelectionGrid(string[])`: its Unity 2022.3 IL2CPP wrapper reaches
`GUIContent.Temp(Il2CppStringArray)` and `GUI.DoButtonGrid`, both of which throw
`Method unstripping failed` in the supplied build. The picker now uses ordinary
single-label buttons, and the page navigation uses two rows so labels fit.

The 88.1 MB reassembled `resources.assets` file contains 24,479 serialized
objects: 7,127 GameObjects, 4,033 SpriteRenderers, 2,572 MonoBehaviours, 2,051
RectTransforms, 1,576 CanvasRenderers, 1,037 textures, 705 sprites, 34 particle
systems, 14 animators, 13 canvases, 3 cameras, and 3 video players. These are
asset-file counts, not counts of active runtime objects. Most describe prefab
templates or optional content; removing them from the asset file would affect
loading and memory, not necessarily per-frame gameplay cost.

The matching IL2CPP dump confirms `MIDIInputManager.Update` is a real per-frame
callback with a 0.2-second work interval. That makes MIDI suppression a
reasonable optional switch for players who do not use drum input, though no
capture isolates its cost. `DiscordController` has a `FixedUpdate` and an
`OnDisable` path; SlopHero's Discord switch is therefore explicitly
restart-required. Leaderboard/network managers remain untouched. The `News`
class issues `UnityWebRequest`s through coroutines and has no `Update` callback
in its metadata, so the evidence points to menu/network activity rather than a
song-frame hot loop. Pong's per-frame update belongs to its ball object and
appears useful only while that minigame is active. Neither item is currently a credible
gameplay-FPS removal target. Likewise, particle and video prefab counts alone
do not prove those effects are active during a given song.

## Next optimization target

The evidence points to the always-visible custom HUD repaint as the next
measurable mod cost, while the long frame spikes remain outside the measured
callback maxima. Any change to HUD rendering should be compared with the same
chart, HUD visibility, settings, resolution, and capture duration. A future
timing capture is needed to judge the current fix; the latest log did not run
one.
