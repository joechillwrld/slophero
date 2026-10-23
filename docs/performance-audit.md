# Performance audit notes

## Measurement correction and 0.4.47 capture

Earlier versions labeled `Time.unscaledDeltaTime` samples taken from
`Core.OnUpdate` as whole-frame times. That was incorrect. Clone Hero can run
multiple simulation Updates for one render, so the 2,320/s sample rate in the
latest log measures the *simulation* cadence, not display FPS or GPU frame
time. This also changes how the older captures below must be read wherever
they say “frame” or “whole frame”: their histograms are simulation-Update
intervals. The named mod callback timings remain valid CPU measurements.

The v0.4.47 log measured 69,622 simulation Updates in 30 seconds, while the
counter HUD drew on 36,996 GUI repaint calls. The Update callback averaged
0.004 ms and HUD repaint averaged 0.017 ms. Using those rounded averages,
their combined measured cost is about 0.91 CPU seconds per 30 seconds, around
3% of one core; other visual/effect buckets add only a small amount and some
are nested. This is *not* a measure of native game work, GPU cost, or a
before/after FPS gain. The v0.4.47 HUD cache is not proven faster by this
capture: repaint average was 0.014 ms in v0.4.46 under different conditions.

The next timing capture records simulation deltas, `OnDemandRendering`'s
scheduled render callbacks, and the change in `Time.renderedFrameCount`
separately. Wall-clock gaps between scheduled render callbacks are an estimate
of CPU render cadence; they are not GPU presentation times. The completed
render counter cross-checks the number of actual rendered frames. At capture
start, a one-time live scene snapshot logs whether the leaderboard entries and
instrument-viewer branches are active. These diagnostics run only during an
explicitly started 30-second capture; the scene inspection occurs before the
timer begins.

### Base-game leads ranked by evidence

| Lead | Current evidence | Decision |
| --- | --- | --- |
| Hidden in-game leaderboard | Gameplay serializes six enabled entry scripts beneath `ScoreList`, whose `CanvasGroup.alpha` starts at zero. Native entry `Update` reads its transform and adjusts alpha on motion; the manager skips its formatting path until its state flag and engine pointer are set. | Investigate *live* offline state and the score list's rendering/layout cost. Do not disable it yet: online/leaderboard visibility can change. |
| Input viewer scene hierarchy | 2,159 GameObjects exist under the viewer, but its native setup toggles instrument branches with `GameObject.SetActive`. Only 46 serialized viewer button scripts occur; their class has no `Update`. | Serialized object count is not a per-frame workload. The game already exposes a viewer setting; keep it functional. |
| FPS text | Native `FrameRate.Update` counts simulation and scheduled render ticks, then formats the label only after `updateInterval` elapses. | No per-tick string-formatting fix to make here. |
| News and Pong | Neither is present in the Gameplay scene; News has no declared `Update`. | Menu cleanup cannot explain song FPS without evidence that a separate persistent instance remains active. |
| Note/input/score pipeline | These are required for chart timing and scoring; the user's controller polling must stay at 1000 Hz. | Do not throttle or skip these paths on speculation. |

The supplied Unity scene is a static snapshot. It has 1,755 SpriteRenderers,
but only 357 are enabled under active parents in that snapshot, 322 beneath
the input-viewer hierarchy before runtime instrument selection. A count of
serialized renderers therefore cannot justify removing all of them. Those 357
enabled renderers reference just two material pointers in the scene snapshot,
so even the pre-selection count does not imply 357 separate draw calls. Large
Geometry Dash speedup examples do not establish a comparable Clone Hero
bottleneck; the next large gain must follow a measured CPU or GPU hot path.

## Base-game investigation after 0.4.45

The v0.4.44 Windows capture measured roughly 2,289 game updates/s. SlopHero's
named callbacks account for about 0.008 ms of CPU time per observed update on
average: `Update` ~0.001 ms, HUD repaint ~0.007 ms after amortizing its 35,066
calls over 68,658 updates, and visual/FC passes well below 0.001 ms each.
This is an estimate from rounded averages, excludes unmeasured hooks and
loader overhead, and is not a whole-game profiler result. It does show why
throttling the shortcut path had little prospect of changing frame pacing.

The v1.1.0.6142 generated Clone Hero assembly contains 103 types declaring
`Update`, `LateUpdate`, `FixedUpdate`, or `OnGUI`. These declarations show
possible work, not which components are active in the Gameplay scene or how
expensive their native implementations are. The strongest *investigation*
candidates outside the note/input/score path are:

| Component | Evidence in generated assembly | What must be checked first |
| --- | --- | --- |
| `SoftMaskController` and `BlurScaler` | Both declare `Update`; the former holds a render texture and camera, the latter a camera and two blur materials. | Whether they are active during songs and actually schedule extra render passes. They may only serve menus or video effects. |
| `CustomFadeProperties` | Declares `Update`, a sprite renderer, camera, and material property block. | Active instance count in Gameplay and whether its per-frame fades are already disabled by game settings. |
| `InGameLeaderboardManager` and `InGameLeaderboardEntry` | Both declare `Update`; the manager also owns a task/coroutine and entry array. | Whether they update during offline songs. Any optimization must preserve optional leaderboards and online play. |
| `BeatPulse`, `CameraShake`, `UVScroll`, `LyricDisplay` | Each declares `Update` and owns visual-only fields. | Active instance count, user-visible effect, and whether Clone Hero already provides an equivalent toggle. |

`BasePlayer`, note renderers, `GameManager`, `BeatRenderer`, and `HighwayScroll`
also declare update methods, but they are part of scoring, chart visibility, or
input timing. They are not candidates for blind throttling. `MenuBackground`,
`BuildCheck`, `News`, and Pong's `Ball`/`Paddle` are menu or minigame leads,
unlikely to explain a normal song's frame time unless they persist in Gameplay.

The re-uploaded native `GameAssembly.dll` and `global-metadata.dat` match the
documented v1.1.0.6142 hashes. The external `resources.assets` script map is
still unavailable in this workspace, so the scene's MonoBehaviour references
cannot be joined conclusively to the generated class names. No base-game
component has been disabled or changed from this investigation.

### Gameplay scene (`level2`) findings

The newly supplied `level2` is the Gameplay scene and contains 7,369 serialized
objects, including 2,466 GameObjects, 404 MonoBehaviours, 1,755
SpriteRenderers, two cameras, one VideoPlayer, and one ParticleSystem. These
are scene defaults; scripts can activate/deactivate them at runtime. At the
serialized scene state, 1,988 GameObjects are active through their parent
hierarchy, but only 357 SpriteRenderers are also enabled. Raw object counts
must therefore not be treated as draw-call or frame-cost measurements.

- `HUDCam/Leaderboard` is active in the serialized hierarchy, with six
  `InGameLeaderboardPrefab` entry objects and their script components active.
  The assembly declares `Update` on the leaderboard manager and entries. The
  scene's external `MonoScript` references are unresolved without the matching
  resources file, so the exact script-to-class mapping is inferred from object
  names for now. This is the best lead for checking whether offline play still
  performs unnecessary leaderboard work, while preserving online features.
- `HUDCam/Input Viewer` holds 2,159 GameObjects across guitar, GHL, and drums
  variants. Its three instrument branches have 322 enabled SpriteRenderers in
  the serialized state, and many more disabled ones. The manager has an
  `Update` method, but the scene does not show which variants it disables when
  the player/instrument is initialized. Disabling the viewer wholesale would
  duplicate an existing visual preference and remove a useful display.
- `HUDCam/Menu Canvas/Lyrics` is active by default and has one script component
  on its root. It may be a candidate only if lyric updates occur when no
  lyrics are displayed and there is no equivalent in-game setting.
- The named `Text Mask` object is beneath the initially inactive practice
  section menu. Its script identity is unconfirmed; this scene does not
  establish an always-running gameplay soft-mask cost.
- The background VideoPlayer is enabled but `playOnAwake=false`. The single
  score-star ParticleSystem is also `playOnAwake=false`, non-looping, and capped
  at 40 particles. Neither is evidence of constant background work.

The remaining question is whether leaderboard entry `Update` and the input
viewer keep running during offline gameplay. Static code shows the leaderboard
manager returns before formatting unless its state flag and engine reference
are both set, so the possible `String.Format` allocation is not a confirmed
offline hot path. Entry updates are still a small unmeasured candidate. A
runtime profiler is needed to establish live component state and actual CPU/GPU
cost before altering base-game behavior.

### Matching native code inspection

The re-uploaded `GameAssembly.dll` and `global-metadata.dat` match the
previously documented SHA-256 fingerprints exactly. Il2CppDumper mapped the
following methods in the v1.1.0.6142 native binary:

- `InGameLeaderboardManager.Update` (RVA `0x3728A0`) first checks a private
  state flag at offset `0x70` and an engine pointer at `0x58`, returning when
  either is absent. On its active path, it calculates an accuracy percentage,
  boxes the result, and calls `String.Format` (RVA `0x1982590`) before updating
  entries. This can allocate per update while the manager is active, but the
  static files do not establish that this path is active in offline play.
- `InGameLeaderboardEntry.Update` (RVA `0x17C0E0`) reads its transform position
  each call, compares it with a cached position, and adjusts its CanvasGroup
  when it moves. It has no check of leaderboard visibility or alpha inside
  that method. Six entry objects start enabled in the serialized scene, though
  gameplay code could disable them later. This is a small confirmed source of
  potentially unnecessary per-frame work, not a measured bottleneck.
- `InputViewer.Update` (RVA `0x1C0730`) is short and checks game/paused/viewer
  state before calling deeper routines. Its 2,159 scene objects do not imply
  2,159 active scripts or a large update cost. `LyricDisplay.Update` (RVA
  `0x3AF8C0`) has an early path for an empty phrase list. `BlurScaler.Update`
  (RVA `0x120560`) tests dimensions before the expensive render-texture resize
  path; no per-frame reallocation is evident when dimensions are unchanged.

The investigation now favors measuring the leaderboard manager's active
branch and entry callbacks in a normal offline song before changing their
cadence. A future change should leave note/input polling untouched and retain
online leaderboard behavior. No SlopHero binary was built for this inspection.

## 0.4.45 input cadence restoration

The v0.4.44 capture used the same mod settings as v0.4.43 except that its
configured speed was 110% instead of 150%. It measured about 2,289 updates/s,
with simulation-Update interval p50 <=0.47 ms, p95 <=0.65 ms, p99 <=0.76 ms,
and a 9.832 ms maximum.
`OnUpdate` fell from roughly 0.004 ms to 0.001 ms per call, while FC rings,
visual overrides, score graphics, and HUD repaint stayed similar. The
simulation-Update interval percentiles were slightly worse, and the unmatched speed setting
prevents attributing that change to the mod. The user's experience was similar
or slightly worse. No SlopHero runtime errors were logged.

The 120 Hz gate also delayed SlopHero's restart/practice button polling. It did
not alter Clone Hero's note-input polling, but the added shortcut latency is
unwanted. Version 0.4.45 removes that gate so the shortcuts are checked on
every game update again. Other established lower-frequency HUD and visual
refreshes are unchanged. There is no measured FPS benefit to claim for this
rollback; future work should target a demonstrated bottleneck.

## 0.4.47 HUD text layout cache

The v0.4.46 capture measured HUD repaint at 0.014 ms average across 35,576
calls in 30 seconds. Passive counters and song info were recalculating text
height/wrapping for every repaint even though their strings update at most five
times per second. Version 0.4.47 caches those measurements by immutable text
instance and the font/width values that affect layout. It does not throttle
repaints or change draw visibility, so Unity still receives each normal HUD
repaint. This reduces repeated managed layout work; the expected gain is small
and needs a matched capture before it can be quantified.

## 0.4.46 Windows capture

The supplied v0.4.46 log recorded 69,855 simulation-Update samples over 30 seconds at about
2,328 updates/s, with p50 <=0.47 ms, p95 <=0.59 ms, p99 <=0.68 ms, and a
7.737 ms maximum. `Performance effects` averaged 0.008 ms across 597 calls
(about 4.8 ms of measured CPU work for the entire capture), with a 0.582 ms
maximum. HUD repaint averaged 0.014 ms; `OnUpdate` averaged 0.004 ms. There
were no SlopHero runtime errors. These values show low steady callback cost,
not an attributable whole-game FPS gain.

The log also records a preferences save 0.761 seconds after the timing capture
started, while the largest `OnUpdate` took 6.714 ms and the largest simulation interval
took 7.737 ms. The timing report does not timestamp those maxima, so the save
is a plausible source of the isolated hitch but not a confirmed cause.

## 0.4.46 effect-discovery cache

When the optional hit-particle, sustain-particle, or fret-effect suppression
was enabled, its 1-Hz pass rediscovered every particle below each player neck,
re-enumerated fret animators, re-read every particle object's name, and fetched
each animator's child renderers again. Version 0.4.46 caches those per-session
lists and name classifications, and refreshes hierarchy discovery every five
seconds or immediately when a switch changes. The existing 1-Hz suppression
pass remains, newly added player necks are cached as soon as they appear, and
turning the options off or changing scenes restores original component states
and clears the caches. This avoids repeated interop and enumeration work while
keeping the feature reversible. No matched Windows capture yet measures the
size of the gain, and this optimization has no effect when all three options
are off. The opt-in 30-second timing report now has a separate `Performance
effects` bucket for this callback, so a matched run can quantify the remaining
cost without mixing it with visual overrides.

## 0.4.44 gameplay update cadence

The v0.4.43 capture below observed roughly 2,317 `OnUpdate` calls per second,
while hold-based song controls and `GameManager` readiness checks were repeated
on every call. Version 0.4.44 leaves one-frame key events, binding capture,
menu speed typing, and frame-timing samples on that path. It limits the heavier
gameplay-state and hold checks to 120 Hz; existing counter and visual refresh
cadences are unchanged. A hold shortcut may act up to about 8 ms later than
before. Its configured hold threshold defaults to 750 ms. A matched Windows
capture is needed to measure the actual frame-time effect.

## v0.4.43 Windows capture

The user reported smoother, more consistent play with v0.4.43. The 30-second
capture in the supplied `Latest.log` recorded 69,512 simulation-Update samples at about
2,317 updates/s, with p50 <=0.46 ms, p95 <=0.64 ms, p99 <=0.73 ms, and a
3.695 ms maximum. SlopHero logged no runtime errors. Its `OnUpdate` callback
averaged 0.004 ms across 69,512 calls; HUD repaint averaged 0.013 ms across
35,219 calls; visual overrides averaged 0.013 ms across 597 calls. The inner
player/speed and score-graphics timings are subsets of visual overrides and
must not be added to that total.

The user also reduced the simulated-frame multiplier, which changes displayed
FPS. The apparent ~200 FPS difference is therefore not an optimization gain.
The capture is useful as a simulation-cadence baseline, but it is not a matched
before/after benchmark and cannot assign the reported improvement to one edit.

## 0.4.43 targeted changes

The mod compiles against the user's re-uploaded v1.1.0.6142 IL2CPP and
MelonLoader references. Matched Windows before/after measurements are still
needed to quantify the effect of each change.

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

The earlier v0.4.33 log contained no timing capture; it recorded a settings-page
exception and could not measure gameplay cost. The comparisons below use the
older captures that were available at the time.

## Measurements from the supplied logs

The clearest matched signal is the visual-override dispatcher. In v0.4.31
captures it ran about 2,303–2,598 times per second, effectively once per game
update. In v0.4.32 the 20 Hz gate reduced that to 595 calls over 30 seconds
(19.8 per second), roughly 116–131 times fewer entries. In that v0.4.32 capture,
the override callback averaged 0.014 ms, score graphics 0.030 ms (inside the
override callback), and the main mod `Update` 0.004 ms. The counter HUD repaint
averaged 0.015 ms across 28,961 repaints, about 965 repaints per second.

The simulation-Update interval sample in the same capture had p99 at or below 0.69 ms and
one 85.845 ms maximum. The largest recorded mod callbacks were 0.142 ms for
`Update`, 0.601 ms for visual overrides, and 0.556 ms for HUD repaint. These
measurements do not prove what caused the isolated simulation gap, but they do not
show a mod callback of comparable duration. Earlier v0.4.31 captures also had
simulation-interval maxima above 120 ms, so that spike pattern predates the
20 Hz change. These interval samples are not full render or GPU timings;
callback timings cover only the named mod methods.

Using the rounded averages from that v0.4.32 capture, the non-overlapping
`Update`, visual-dispatch, and HUD repaint buckets account for approximately
0.77 CPU-seconds in the 30-second sample, or about 2.6% of one fully occupied
CPU core. This is an estimate from rounded callback averages, not a profiler
measurement of total process cost. The steady callbacks are small; the large
simulation gap is the more important unresolved symptom. Across the paired
v0.4.31 and v0.4.32 captures, dispatch frequency is the clearest comparison:
about 2,303–2,598 entries/s before versus 19.8/s after. Those logs are not a
controlled A/B for total FPS, so they do not support an exact whole-game
speedup claim.

## Current hot paths

- `Core.OnUpdate` is invoked at the game's unlocked update rate, including
  SlopHero's shortcut polling. The v0.4.43 measured average was about 4
  microseconds per call; v0.4.44 temporarily gated gameplay checks to 120 Hz,
  but v0.4.45 restored the every-update behavior. Chart-stat formatting and
  note scans remain gated to 5 Hz and are skipped when their fields are hidden.
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

The custom HUD repaint remains the largest measured mod callback by aggregate
time, but its measured steady cost is small. Large simulation gaps remain
outside the measured callback maxima. The highest-value next step is to
establish actual rendered-frame pacing and, if it is poor, profile Unity's
CPU render, UI layout, GPU, and driver time on Windows. The leaderboard is a
specific native-work candidate only if its entries and canvas remain active
while hidden in live offline play. Any claimed FPS improvement needs a matched
chart, speed, HUD visibility, settings, resolution, and capture duration.
