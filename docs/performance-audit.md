# Performance audit notes

## Sloptifine scope

Sloptifine is the name for SlopHero's performance work. The safe path is
incremental: keep the existing IL2CPP-facing feature controllers, preserve
their restoration of original game state, and optimize only work shown to be
active in gameplay. A broad rewrite would put score counters, practice
transitions, 1000 Hz controller polling, and optional visual hooks at risk
without evidence that their structure is a bottleneck. Changes should stay
isolated and reversible, with native gameplay/input methods left alone.

### v0.4.52 live capture and asset follow-up

The latest log confirms v0.4.52 loaded with the matching Clone Hero build and
completed a 30-second capture without a SlopHero exception or Canvas fallback.
The optional Canvas counter path was enabled. The capture's one-time snapshot
found the hidden offline leaderboard at alpha 0 with zero of six entry updates
eligible, and 48 active guitar input-viewer sprites (zero GHL/drum sprites).
Ten `CustomFadeProperties`, one `CameraShake`, two `UVScroll`, and one
`LyricDisplay` components were live. These are now the best concrete visual-only
objects for continued investigation; their presence alone is not enough reason
to disable them. The snapshot also found 257 enabled SpriteRenderers under
active objects, 3 cameras with no render-texture targets, 11 canvases, 3
animators, 1 active VideoPlayer out of 2, and 1 playing ParticleSystem.

At `renderFrameInterval=2`, Unity ran about 2,371 simulation Updates/s and
scheduled about 1,186 render callbacks/s. The simulation-Update interval was
at or below 0.58 ms at p95 and 0.66 ms at p99, with a 36.36 ms maximum. The
scheduled-render interval was at or below 1.01 ms at p95 and 1.12 ms at p99,
with a 3.62 ms maximum. This points to mostly steady CPU-side callback cadence
with one isolated simulation stall; neither stream measures GPU presentation
or proves an in-game FPS change.

Using the report's rounded averages, the named SlopHero callbacks sum to about
0.26 CPU seconds over 30 seconds (about 0.9% of one core): Update 0.14 s, HUD
repaint 0.088 s, visual overrides 0.009 s, performance effects 0.005 s, FC
rings 0.004 s, and Canvas property synchronization 0.008 s. Nested player/speed
and score-graphics work is already included in visual overrides. The largest
reported callback maxima were 0.629 ms for performance effects and 0.611 ms
for HUD repaint; their means were 0.008 ms and 0.053 ms, respectively. This
capture is consistent with lower measured mod-side HUD work than the earlier
IMGUI-heavy captures, but the run
is not a controlled A/B and the Canvas renderer's GPU cost is not measured.
The evidence supports keeping the Canvas path available, not claiming a whole-
game performance gain or enabling it by default yet.

The nine supplied `resources.assets` chunks reassemble to 88,131,400 bytes and
parse as 24,479 Unity objects, matching the previously recorded inventory.
They add no live-cost evidence by themselves: most objects are asset or prefab
templates, and MonoBehaviour script references in this file do not resolve to
class names. This parsed file has no `MonoScript` objects to bridge those
references to class names. The fresh runtime snapshot is more useful for
prioritizing the active candidates above.

Audit correction: I previously described matching component counts as strong
class-to-path matches. That overstates the evidence. The ten serialized scripts
under `PracticeSectionMenu/List/Text Mask/Text` happen to match the live
`CustomFadeProperties` count, but the practice-menu parent is inactive in the
serialized scene and the supplied assets do not identify those script
references by class. Likewise, two script references on lyric-background
objects do not prove they are the two live `UVScroll` instances. The new
opt-in capture logs actual hierarchy paths for the live candidate classes so
the next capture can establish those identities directly.

### v0.4.53 path mistake and v0.4.54 correction

The latest log shows the two enabled `UVScroll` components at
`Guitar Player(Clone)/Combo/multiplier/smoke_combo_fast` and
`Guitar Player(Clone)/Combo/multiplier/smoke_combo_slow`. The live lyrics object
is `HUDCam/Menu Canvas/Lyrics` and has no `UVScroll`. This directly disproves
the path assumption used in 0.4.53: its lyric-background switch searched the
wrong hierarchy and did nothing. That was my implementation error.

Version 0.4.54 changes **F8 → Performance → Pause combo smoke scrolling** to
target only those two named child objects beneath the active local input
guitar's `Combo/multiplier`. It disables each `UVScroll` component, not its
renderer, so the smoke stays visible. Remote players are excluded. Original
enabled states restore when the option turns off, a scene changes, or the mod
shuts down. While enabled, hierarchy discovery retries every two seconds to
catch player visual objects recreated during practice transitions; it does not
walk the hierarchy each frame. The feature remains default-off.

The user captured a run with the old switch off and another with it on, but
those runs do not form an A/B test because the old switch matched no objects.
They also differ in duration (23.7 versus 30.0 seconds) and update cadence.
Both report `renderFrameInterval=2` and 100% song speed. The off run recorded
simulation p95/p99 intervals of 0.65/0.79 ms and scheduled-render p95/p99 of
1.12/1.33 ms; the on run recorded 0.58/0.66 ms and 1.02/1.14 ms. These values
show no evident slowdown in either capture, but they cannot be attributed to
the switch and do not measure native `UVScroll` CPU time or GPU presentation.
The corrected build reports how many combo smoke components it tracked and
paused so a future capture can confirm the switch actually engaged.

Other leads from the same capture, ranked by what is actually known:

| Candidate | Current evidence | Action |
| --- | --- | --- |
| Combo-smoke `UVScroll` | Two enabled local objects, with live hierarchy paths confirmed. Native `Update` advances texture offsets and writes a material property block on simulation ticks. | Reversible, default-off switch added. Actual CPU and frame-time gain remain unmeasured. |
| `CameraShake` | One enabled. Native `Update` returns immediately when its shake-state flag is clear; the heavier path is event-driven while shaking. | Leave alone; no idle hot path established. |
| `LyricDisplay` | One enabled. Its native `Update` has an early path when there is no current lyric phrase. | Leave alone; disabling it would break lyrics, and no material cost is shown. |
| `CustomFadeProperties` | Ten enabled beneath guitar fret-lightning objects before v0.4.55; the newer capture confirms 0/10 while fret effects are disabled. Native `Update` compares four cached values and returns when unchanged. | v0.4.55 pauses only these scripts while the user has already hidden fret effects; state restores with that switch. New callback data is encouraging, though GPU cost remains unknown. |
| `SoftMaskController`, `BlurScaler`, `BeatPulse` | No enabled instances found in this song capture. | Not gameplay targets in this run. |
| `VideoPlayer` | Latest capture finds one enabled of two and zero playing; an older run had one playing. | The existing game video toggle should remain authoritative. |
| 3 cameras, 11 canvases, 257 enabled SpriteRenderers, 3 animators | Counts only; they do not measure overdraw, Canvas rebuilds, draw calls, or GPU cost. | Need Unity Profiler/RenderDoc or comparable GPU/CPU profiling before any changes. Avoid disabling scene objects by count alone. |

The opt-in timing capture logs hierarchy paths for live camera-shake,
UV-scroll, lyrics, and custom-fade components, and records whether active video
players are playing. It discovers these once before the 30-second window, not
per frame. These paths replace the previous unsupported lyric-background
guess with direct runtime evidence.

### v0.4.54 confirmation and v0.4.55 fret-fade result

The next runtime log confirms that 0.4.54 engaged the intended effect:
`comboSmokeTracked/paused=2/2`, while the scene snapshot reports
`uvScroll=0/2` and no active `UVScroll` paths. The option is enabled in this
capture. The 30-second run recorded simulation p95/p99 intervals of
0.58/0.66 ms and scheduled-render p95/p99 intervals of 1.01/1.14 ms. Compared
with the earlier 0.4.53 capture, those intervals are modestly lower, but the
older run had a different duration and the switch in 0.4.53 did not touch the
smoke components. Treat this as encouraging but non-causal; the maximum
simulation interval is still about 39.6 ms, close to the earlier 39.3 ms
outlier. The report's scheduled-render interval is callback spacing, not GPU
presentation time.

The 0.4.54 run has `disableFretEffects=true` while all ten
`CustomFadeProperties` components remain enabled on the five `Fret_*` lightning
and white-lightning objects. Version 0.4.55 extends the existing fret-effects
switch to pause only those ten components while their visual effects are
disabled, restoring their original enabled state with the switch. This avoids
changing visible gameplay or introducing another setting. The components'
native update has a cached-value early return, so the expected savings were
uncertain.

The first 0.4.55 capture confirms the extension worked: `customFade=0/10` and
`fretFadeScriptsPausedByMod=10`, with all the same visual switches still
enabled and `renderFrameInterval=2`, `configuredSpeed=100%`. Against the
previous 30-second 0.4.54 run with matching logged settings, observed
simulation Updates increased from 2,394/s to 2,536/s (+5.9%), and scheduled
render callbacks increased from 1,197/s to 1,268/s (+5.9%). Simulation p95/p99
intervals improved from 0.58/0.66 ms to 0.54/0.60 ms; scheduled-render
p95/p99 improved from 1.01/1.14 ms to 0.93/1.00 ms. The similar setup and
matching 30-second windows make this the strongest comparison so far and
support the user's observed improvement. The max simulation interval stayed
near 39 ms, so this change did not remove the rare hitch. These are Unity
callback rates and CPU intervals, not a GPU or display-presentation measure.

The F8 menu now calls this page **Sloptifine** and uses native GUI checkboxes
for boolean options on every settings page. The checkbox controls render only
while the settings panel is open; they add no work to gameplay updates.

Version 0.4.56 also separates the optional particle/fret-effect controller
from the general visual-override dispatcher. Before this change, any enabled
effect switch kept the general visual pass active at 20 Hz, even though the
effect controller itself only rescanned cached player effects once per second.
The controller now runs immediately when a switch changes and otherwise only
at that existing one-second scan interval. Its work no longer triggers FC-ring
and score-visual refreshes when those features are otherwise idle. This leaves
the 50 ms cadence for actual visual overrides, and the one-second effect
discovery cadence, unchanged. It is a small, code-level reduction in SlopHero
work; no whole-game frame-rate gain is claimed without a new matched capture.

### v0.4.56 follow-up and v0.4.57 changes

The new 0.4.56 log loaded without a SlopHero error and recorded 30 performance-
effect calls during 30 seconds, matching the controller's one-second schedule.
It also shows the combo-smoke setting enabled while both known local
`UVScroll` objects remain enabled (`tracked/paused=0/0`). The older capture had
tracked both, so this is a real regression in discovery conditions to fix. The
discovery filter required `inputEnabled`; the live game can temporarily report
that false while the local player's visual hierarchy is already present. The
0.4.57 search therefore includes enabled nonremote player guitars and compares
the child transform pointer with the intended multiplier parent. Remote
players remain excluded.

The 0.4.56 simulation p95/p99 intervals were 0.56/0.63 ms and scheduled-render
p95/p99 were 0.95/1.03 ms, slightly above the prior run's 0.54/0.60 and
0.93/1.00 ms. These differences are small and the callback rates are not
display-FPS measurements. The capture also had isolated 2.37 ms HUD repaint
and 4.74 ms Canvas-sync maxima; the means were 0.054 and 0.021 ms. The Canvas
sync spike is not enough to justify changing counter refresh behavior without
a repeat capture.

The hidden-leaderboard override previously checked online state, canvas
visibility, and six component states at 60 Hz. Version 0.4.57 performs that
visual-only poll at 20 Hz, consistent with the other menu overrides; on
visibility, the six entries resume within 50 ms. This cuts two thirds of those
mod-side checks while preserving offline-only behavior and prompt restoration.
The F8 window now follows the user's left-sidebar mockup. Its editable accent
color is used on the title bar, selected category, and resize outline and is
included in color themes. The menu remains centered, and its controls have no
draw/update cost while closed.

The safe rewrite decision is therefore to avoid a wholesale rewrite. The
current measurements show SlopHero's named callback work is already small
beside the high-frequency game loop, and the largest remaining uncertainty is
the cost of the active native visual scripts and GPU/presentation work. Future
changes should target one measured component at a time, preserve immediate
restore behavior, and never alter the game's controller polling cadence.

## Live capture findings and measurement correction

### v0.4.52 experimental counter Canvas

The v0.4.51 capture attributed about 0.51 CPU seconds per 30 seconds to the
counter HUD's IMGUI repaint callback. Version 0.4.52 offers a default-off
Canvas path for counters only. It creates its own screen-space Canvas with one
background image and one TextMeshPro label during gameplay, without a graphics
raycaster. It synchronizes retained UI properties at the existing 20 Hz visual
cadence, and only writes text, color, size, or position when they change. The
Canvas is hidden while editing, so the original move and resize interactions
remain in IMGUI. Disabling the option destroys the Canvas; exceptions during
construction or update latch a fallback to IMGUI for the session. Scene changes
also destroy the Canvas. The option is experimental because compile-time checks
cannot confirm the game's stripped UI methods or the relative rendering cost.

The timing report includes a `Canvas HUD sync` bucket and records the switch's
state. This bucket measures only SlopHero's UI property updates, not Unity's
Canvas layout, rendering, GPU work, or presentation. A drop in the IMGUI HUD
bucket therefore cannot by itself prove a game FPS gain. The default render
path and all note/input/score logic are unchanged.

The opt-in timing snapshot also counts enabled candidate visual scripts
(`SoftMaskController`, `BlurScaler`, `CustomFadeProperties`, `BeatPulse`,
`CameraShake`, `UVScroll`, and `LyricDisplay`), SpriteRenderers, cameras and
render-texture targets, canvases, animators, video players, and playing particle
systems. These are one-time discovery calls before the 30-second timing window;
no scene traversal was added to every rendered frame. Counts identify live
objects but not their CPU or GPU cost, so they do not justify disabling a
component alone.

### v0.4.51 source-level optimization

The v0.4.50 capture showed that `Core.OnUpdate` ran around 2,342 times/s while
its average measured body cost rounded to 0.004 ms. The body was also reading
the IL2CPP game manager, song readiness, practice mode, and eligibility fields
on every tick even though HUD statistics only change at 5 Hz. Version 0.4.51
keeps the configured restart/practice bindings polled on each active gameplay
update, but performs idle game-state and transition-readiness checks at 20 Hz.
When either shortcut is held, eligibility still updates each tick. This keeps
the responsive control path live and removes redundant manager/property reads
while idle. The likely gain is small and needs a comparable live capture; the
prior 0.004 ms average is too coarsely rounded to quantify it.

### v0.4.51 follow-up capture (30 seconds)

The new log loaded v0.4.51 with the same reported gameplay settings as the
v0.4.50 capture. The hidden leaderboard was still invisible with zero of six
entry updates eligible, and the input-viewer snapshot again found 48 active
guitar sprites and none in the GHL/drum branches. SlopHero reported no runtime
exceptions; the only warning was the same Il2CppInterop startup fallback.

The run observed 2,361.7 simulation Updates/s and 1,180.8 scheduled-render and
GUI-repaint callbacks/s. Compared with v0.4.50, p95/p99 intervals were slightly
shorter: simulation 0.59/0.66 ms (previously 0.64/0.73), scheduled render
1.02/1.12 ms (1.06/1.22), and repaint 1.01/1.11 ms (1.06/1.23). The maxima
were higher, however: 9.906 ms, 15.115 ms, and 10.189 ms respectively. The
percentile movement is small and the isolated maxima do not identify a cause;
these runs are not a controlled A/B, and the callback frequencies are not
presented FPS or GPU frame times.

Using the three-decimal averages, named non-overlapping SlopHero work is about
0.60 CPU seconds per 30 seconds (roughly 2.0% of one continuously occupied
core): Update 0.001 ms across 70,852 calls, HUD repaint 0.014 ms across 36,482
calls, FC rings 0.005 ms across 597 calls, visual overrides 0.016 ms across
597 calls, and performance effects 0.007 ms across 597 calls. The player/speed
and score-graphics buckets are included within visual overrides. The estimate
for the v0.4.50 capture was about 0.84 seconds, so the lower rounded Update
average is consistent with less idle work after v0.4.51, but the rounded and
uncontrolled captures do not establish a whole-game performance gain. The
counter HUD remains the largest measured mod path at about 0.51 seconds per
30-second run; replacing IMGUI with a Unity canvas would need a real A/B before
it is justified.

### v0.4.50 follow-up capture (30 seconds)

The new log loaded v0.4.50 with the same reported settings as the v0.4.49
capture. The hidden leaderboard remains suspended while its list is invisible
(alpha 0; zero of six entry updates eligible), and the guitar viewer still
reports 48 enabled sprites while GHL/drums report none. No SlopHero runtime
exception appears in the log.

This run observed 2,342 simulation Updates/s and 1,171 scheduled-render and
GUI-repaint callbacks/s. The prior capture recorded about 2,300 and 1,150/s.
The p50/p95/p99 callback intervals changed only slightly: simulation
0.46/0.64/0.73 ms, scheduled render 0.83/1.06/1.22 ms, and GUI repaint
0.82/1.06/1.23 ms. The new maxima were 9.034 ms for simulation, 8.459 ms for
scheduled render, and 8.184 ms for repaint. Those maxima appear across all
three callback streams, consistent with one shared scheduling delay, but the
log cannot identify its source. The percentile cadence remains much steadier
than those isolated maxima suggest.

Rounded named SlopHero callback costs sum to about 0.84 CPU seconds over the
30-second run, roughly 2.8% of one core: Update 0.004 ms across 70,277 calls,
HUD repaint 0.015 ms across 36,350 calls, performance effects 0.008 ms across
597 calls, visual overrides 0.012 ms across 597 calls, and separate FC-ring
work 0.004 ms across 597 calls. Player/speed and score-graphics timings are
subsets of visual overrides. The HUD repaint is still the largest named
aggregate, but the cost is only around half a second per 30-second capture.

The v0.4.50 change moved MIDI/Discord service-manager calls to 10 Hz, but the
`Update` average remains 0.004 ms at the report's three-decimal precision.
This capture therefore does not resolve or prove a gain from that change.
The observed rate and interval differences from v0.4.49 are small enough to
come from ordinary run-to-run variation; this was not a controlled A/B test.
The capture also still measures callback spacing rather than GPU presentation
or whole-game FPS.

### v0.4.49 live capture (30 seconds)

The newly supplied log confirms the v0.4.49 build loaded against the matching
v1.1.0.6142 files and completed its capture without a SlopHero runtime error.
The snapshot shows the hidden-leaderboard switch working: the score list was
active but fully transparent (`alpha=0`), and zero of its six entry `Update`
components were eligible. The guitar input-viewer branch reported 48 enabled
sprites; the GHL and drum branches reported none in this run.

The game ran about 2,300 simulation Updates/s with `renderFrameInterval=2`.
Scheduled render callbacks and GUI repaints both measured about 1,150/s. Their
interval percentiles were roughly 0.84–0.85 ms at p50, 1.07 ms at p95, and
1.21–1.22 ms at p99, with maximum intervals of 6.439 ms and 6.495 ms. These
remain CPU callback cadence measurements, not display FPS, presented-frame
times, or GPU timings. The 8.392 ms maximum simulation interval is not itself
evidence of an 8.392 ms rendered-frame hitch.

Rounded callback costs give about 0.79 CPU seconds of measured SlopHero work
over the 30-second capture (roughly 2.6% of one core): Update 0.004 ms across
68,998 calls, HUD repaint 0.014 ms across 35,240 calls, performance effects
0.008 ms across 597 calls, and visual overrides 0.013 ms across 597 calls.
Nested player/speed and score-graphics buckets are already included in visual
overrides; FC-ring work is separate and averaged 0.005 ms. These timings do not
include native Clone Hero work, GPU cost, or driver wait, so the log does not
support attributing a whole-game performance change to SlopHero.

This capture had several visual/customization options enabled, including FC
and score colors, highway-side/string/beatline hiding, constant practice speed,
and the three optional effect suppressors. It is not an A/B comparison, so it
cannot tell whether those options improve or reduce frame performance. The
optional MIDI service switch was enabled; v0.4.50 reduces its service-manager
checks from every simulation tick to 10 Hz. That is a small CPU cleanup, not a
promised FPS gain, and it does not alter 1000 Hz controller polling.

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

The v0.4.48 log shows why `Time.renderedFrameCount` needs special care here:
with `renderFrameInterval=2`, it advanced at the same 2,330/s rate as the
simulation Updates, while `OnDemandRendering.willCurrentFrameRender` scheduled
callbacks at 1,165/s. It is not a useful rendered-frame counter in this game
configuration. The next report drops that counter and measures Unity GUI
Repaint callback spacing as a second render-loop signal. Scheduled render and
GUI Repaint intervals estimate CPU cadence; neither measures GPU presentation.
The one-time capture snapshot will also count live, enabled input-viewer
SpriteRenderers by instrument branch.

### v0.4.48 live capture

The latest capture recorded 2,329 simulation Updates/s and
`renderFrameInterval=2`. Scheduled render callbacks measured 1,164.8/s, with
p50 <=0.84 ms, p95 <=1.05 ms, p99 <=1.19 ms, and a 5.179 ms maximum. The
existing HUD repaint bucket ran 1,199.5/s. These are CPU-side callback
intervals, not a GPU timing result. The 8.933 ms maximum simulation interval
does not recur in the scheduled-render maximum, so it should not be reported
as an 8.933 ms rendered-frame hitch.

`Update` averaged 0.004 ms and HUD repaint averaged 0.015 ms. At these rounded
averages, those two paths account for about 0.82 CPU seconds per 30 seconds,
or 2.7% of one core. The opt-in performance-effects callback averaged 0.007
ms across 597 calls. This still leaves native game CPU, graphics-driver, and
GPU costs unmeasured.

The live snapshot confirmed six `InGameLeaderboardEntry.Update` components
were eligible to run while their active parent score list had `CanvasGroup`
alpha 0. The native `Update` checks entry movement and can update its alpha;
it is not doing network or score formatting itself. Version 0.4.49 adds a
default-off Performance switch that suspends those six motion checks only
while the score list is fully invisible and the game is offline. It restores
their original component states within about 17 ms when the list appears or
the game connects, and when the switch turns off or the Gameplay scene closes.
This avoids six native Update
callbacks per simulation tick in that hidden state; it does not remove their
draw calls or change online leaderboard behavior. The per-song gain is
expected to be small.

### Base-game leads ranked by evidence

| Lead | Current evidence | Decision |
| --- | --- | --- |
| Hidden in-game leaderboard | The v0.4.48 live capture found six enabled entries under an active score list at alpha 0; native `Update` checks movement and adjusts entry alpha. | v0.4.49 offers a default-off, offline-only switch that suspends those checks while fully hidden and restores them before display. It does not remove drawing. |
| Input viewer scene hierarchy | All three instrument roots reported active in the v0.4.48 snapshot. Its native setup toggles inner instrument containers; the scene has 2,159 GameObjects and 46 serialized viewer button scripts, whose class has no `Update`. | v0.4.49 records the live enabled SpriteRenderer count under each instrument root. Do not duplicate the game's viewer switch or infer draw cost from prefab counts. |
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

- `Core.OnUpdate` is invoked at the game's unlocked update rate. Shortcut
  bindings remain live at that cadence, but v0.4.51 limits idle game-state
  reads to 20 Hz; state checks return to every update while a shortcut is held.
  The v0.4.50 measured average was about 4 microseconds per call. Chart-stat
  formatting and note scans remain gated to 5 Hz and are skipped when their
  fields are hidden.
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

## Older source dump archaeology

The source dump at [opseclevelarchivesrc](https://github.com/joechillwrld/opseclevelarchivesrc), commit
`0f0b459df2bc9ba1087a0cf79ba4c3eb52120890`, is a Unity 2019.4 project with
311 C# files. It is an architecture reference, not the source for Clone Hero
1.1.0.6142. The current generated IL2CPP assembly still contains corresponding
types such as `BeatRenderer`, `GuitarNoteRenderer`, `SongOptions`,
`MIDIInputManager`, `GHLFretAnimator`, and `PongersGame`; matching type names do
not establish that their current method bodies still match this dump.

One target was specific enough for a guarded optimization. In the old source,
`BeatRenderer.Update` calls `RenderBeats` every frame. That method scans upcoming
beats and rewrites pooled objects' active states, transforms, and colors.
SlopHero's existing **Hide beatlines** option hid their renderers but left this
work running. Version 0.4.58 adds a separate, default-off **Skip beatline
updates while hidden** switch. It suspends the component only while beatlines
are hidden, remembers the original enabled state, and restores it when either
switch turns off or the scene changes. The old implementation also fired
April-Fools camera shake and roll effects from `RenderBeats`; the separate
April Fools switch suppresses those effects by default. No in-game frame-time
result is available yet; this is a targeted CPU-work reduction, not an FPS
claim.

The current metadata does not expose the old `GlobalVariables.isAprilFools`
field. It does expose `BaseMenu.HAVEFUN`, `GlobalVariables.isNoteBomber`, and
`PongersGame`, alongside the AprilFools cheat name and its April 1 prompt text.
Version 0.4.58 adds a default-on, reversible **Disable April Fools / NoteBomber
chaos and Pong** option: it clears the two current shared joke flags and
deactivates active Pong minigame objects, preserving their original states for
restoration. The shared `HAVEFUN` flag also drives NoteBomber effects, so those
are suppressed too while enabled. This is a targeted content fix; it does not
claim a gameplay-FPS gain.

The supplied current 1.1.0.6142 IL2CPP dump narrows the other leads, but does
not expose enough method bodies to justify changing them:

- The old guitar renderer ran a 200 Hz whammy-texture updater, including a CPU
  path with `Texture2D.SetPixels` and `Apply`. In the current generated dump,
  `GuitarNoteRenderer` is a thin subclass of generic
  `BaseGuitarNoteRenderer`; neither type exposes the old updater name or
  whammy-texture fields. The native methods are obfuscated, so the 200 Hz
  cadence and texture upload path cannot be confirmed from this dump. Changing
  a guessed call rate could damage whammy-tail rendering without reducing work.
- The old note renderer used `yield`-based `GetNextBitPos` and range helpers in
  hot loops. The current dump has an `Update` on the generic base renderer but
  no matching helper names or renderer-owned iterator-state-machine types. That
  is evidence the old allocation pattern is not directly present; it does not
  reveal the current native loop body. No scoring-sensitive replacement is
  warranted from this evidence.

The old `GHLFret_Animator.Update` is empty, and the current generated
`GHLFretAnimator` declares no `Update` override, so there is no current callback
to remove. The old `Gameplay ideas.txt` also contains draft concepts for
randomized party modifiers, a speed-changing multiplayer race, and sight-read
FC achievements. These are design leads from the dump, not existing features
or agreed requirements. It also reveals secret song-options menus behind fret
button sequences and an April-Fools flag toggled by a cheat. The current
assembly's joke-mode fields and `PongersGame` type enabled a current-version
suppression hook without changing note scoring or song timing.

### v0.4.58 capture and current note/whammy path check

The latest `Latest.log` confirms SlopHero v0.4.58 loaded against the supplied
1.1.0.6142 build, with no SlopHero exception during the capture. It records
2,461 simulation Updates/s and 1,230 scheduled-render and GUI-repaint callbacks/s
at `renderFrameInterval=2`. The main `Core.OnUpdate` average is 0.002 ms across
73,833 calls (0.402 ms maximum); the visual-override average is 0.014 ms across
598 calls; the optional effect pass averages 0.147 ms across 30 calls; and the
HUD repaint averages 0.061 ms across 321 calls. These named callbacks remain
small compared with the native game work that is not measured here. The capture
does not measure GPU presentation time.

I disassembled the relevant methods from the supplied current `GameAssembly.dll`
using its matching generated dump. `BaseGuitarNoteRenderer.Update` calls
Unity's `OnDemandRendering.willCurrentFrameRender` before entering the note
traversal and returns immediately on non-rendering updates. The subsequent
note traversal is capped at 500 entries. This directly confirms that the
current game already avoids running its main guitar-note rendering loop on the
extra simulation ticks visible in the log. Adding a SlopHero throttle there
would duplicate this optimization and could leave note visuals stale. The
remaining loop is a real per-render-frame cost, but changing its pooling,
ordering, or visible-note calculations is high risk for gameplay visuals.

The 2019.4 source dump's whammy path is also concrete: `GuitarNoteRenderer.Start`
starts `Helper.InvokeRepeating` at 200 Hz. That coroutine yields once per Unity
frame, then calls its action repeatedly to catch up if more than 5 ms elapsed.
Each call updates a shared 1024x24 whammy texture: the compute path uploads
player data and dispatches a kernel; the CPU fallback calls `SetPixels` and
`Texture2D.Apply`. That is a credible high-value lead in that old build,
especially for the CPU fallback and catch-up bursts after a hitch.

The current 1.1 native `GuitarNoteRenderer.Start` has a different shape: it
initializes fields and delegates to the generic base renderer; it does not
start that old coroutine or create the old renderer-owned texture state. The
current generated dump still contains whammy input/settings and the
`_GlobalWhammyTexture` shader-property string, so this does not prove that every
whammy animation update was removed or moved elsewhere. It does establish that
the old 200 Hz hook is not present at the old startup site. The old note source
also had no `yield` inside its note traversal; the yielding was in the repeating
helper, while song-scan and thread-wait coroutines were menu/loading work.

No additional static game files are needed for this check: the current
`GameAssembly.dll`, metadata, generated assemblies, and old source tree are
already available. To decide whether any relocated whammy updater is expensive
in a live 1.1 run, the useful missing evidence would be a Windows Unity Profiler
CPU Timeline (Deep Profile off) with the same chart/settings, not another copy
of the assets. I am leaving the current note and whammy runtime paths untouched
until that cost and the replacement implementation are confirmed.

### First WPR/WPA CSV export

The first native capture export includes a process-level CPU summary and a GPU
adapter/process summary. Clone Hero accounts for 15.88% of sampled CPU weight;
79.29% is attributed to idle threads. This does not show system-wide CPU
saturation, although one saturated game thread could still limit frame rate.
The exported CPU `Stack` field is blank for Clone Hero, so this export cannot
identify which native game methods consumed that CPU. The other sampled
processes are small individually; for example, WPRUI is 0.34% of weight and
the visible `equibop.exe` instances together are under 1%.

The GPU export contains only a single `MemoryTransfer` row for the RTX 3060 Ti
with 29,014.6 microseconds of GPU time and an `Unknown` process. Its 100% is
the share of this exported table's one row, not 100% GPU utilization. It does
not establish GPU headroom or identify Clone Hero's graphics workload.

No new ETL capture is needed yet. The existing ETL should be reopened in WPA
and the Clone Hero CPU row expanded through Thread and Stack before exporting
again. For the GPU side, export the engine/utilization view with graphics or
3D engine activity, not just the adapter/process memory-transfer summary. No
game or mod change is justified by these two summary tables alone.

### 2026-10-23 ETWAnalyzer method export and UI-input lead

The 8 MB `Sloptifine-CPU.csv` is a valid ETWAnalyzer method export for
Clone Hero PID 7292. It covers 31.1 seconds and contains 21,945 method-address
rows. Matching the supplied GameAssembly and metadata with Il2CppDumper
resolves several of the formerly anonymous GameAssembly offsets. This is a
single trace, with no paired baseline or GPU/presentation table, so it is
useful for finding active code paths rather than claiming a measured FPS gain.

SlopHero's largest named entries are `Core.OnLateUpdate` (142 ms),
`Core.OnUpdate` (101 ms), and `Core.OnGUI` (67 ms); the next largest are
`NativeHudLayout.Apply` (25 ms), `CounterCanvasHud.Refresh` (23 ms),
`PerformanceOverrides.Apply` (22 ms), and `VisualOverrides.Apply` (21 ms).
These method times overlap through nested calls and must not be summed as
exclusive CPU costs. They are consistent with the earlier timed capture:
SlopHero's managed callbacks are not the dominant lead in this run.

The prominent native lead is the Unity UI mouse-event path. Captured PCs map
to `EventSystem.Update` (1,610 ms), `StandaloneInputModule.Process`
(1,484 ms), `StandaloneInputModule.ProcessMouseEvent(int)` (1,451 ms),
`PointerInputModule.GetMousePointerEventData(int)` (1,394 ms),
`EventSystem.RaycastAll` (1,358 ms), and `GraphicRaycaster.Raycast`
(1,105 ms). Unity's shipped UI source confirms the path: the input module
processes mouse input during `Process`; pointer-data creation calls
`RaycastAll`; that loops over active raycasters; and a `GraphicRaycaster`
tests the canvas's registered raycastable graphics. These per-method times
are inclusive/overlapping profiler attribution, not an additive bill. They
do show that the mouse/UI path is running frequently during the capture.

The metadata distinguishes Unity's `StandaloneInputModule` from Rewired's
separate `RewiredStandaloneInputModule`. The observed hot offsets belong to
Unity's built-in module, so Rewired's `allowMouseInput` property would not
control this captured path. A narrowly scoped experiment could bypass the
built-in module's mouse-event handler only while a song is actively playing,
restoring it when paused or outside gameplay. That would preserve the
independent controller polling and keyboard/controller UI navigation paths,
but mouse clicks and hover interactions on gameplay UI would be unavailable
until pausing. This is now implemented as the default-off Sloptifine option
`PerformanceSkipGameplayMouseUi` in the Performance tab. It patches only the
zero-argument `StandaloneInputModule.ProcessMouseEvent()` wrapper, which
avoids the captured mouse path before it reaches pointer raycasts; the parent
input module continues into its keyboard/controller navigation. The hook is
installed only while the setting is enabled and Clone Hero reports active,
unpaused gameplay. Its existing 20 Hz game-state check controls activation;
the hook is removed on pause, settings/menu focus, focus loss, transitions,
scene changes, and shutdown. With the option off, no mouse hook remains
installed. The preference is included in saved profiles. Instrument polling
and the 1000 Hz Rewired update are untouched.

The tradeoff is explicit in the menu: mouse clicks and hover interactions on
gameplay UI are unavailable while a chart is active and return on pause. The
implementation compiles, but the trace is still a lead rather than an A/B
result. It does not establish a frame-time gain; that requires a matched run
with the option off/on and presentation or frame-time measurements.

The trace also attributes 2,116 ms to `Rewired.InputManager_Base.DoUpdate`.
That is kept intact by design: it is input work, and this profile is no reason
to change the user's 1000 Hz controller polling. Two GameAssembly `.text`
addresses (`+0x2FAB159` and `+0x2FAB63D`) receive large inclusive attribution
but are outside Il2CppDumper's managed-method map. PE unwind data and native
disassembly show small wrappers around indirect/generic helper code, but no
reliable source-level identity. I am not attaching hooks or suppressing them
from an address guess.

Finally, stack-walk collector functions are prominent in the export. They are
measurement overhead, not Clone Hero work, and the export's inclusive method
times cannot be used as frame-time percentages. Any future comparison should
use matched captures and actual PresentMon/GPU timing; another large ETL is
not needed to justify the mouse-path experiment, but the option's real benefit
would still need a short A/B after it exists.
