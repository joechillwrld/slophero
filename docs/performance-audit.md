# Performance audit notes

The first raw PresentMon baseline for v0.4.75 and its CPU/GPU interpretation
are in [presentmon-baseline-2026-10-23.md](presentmon-baseline-2026-10-23.md).
It records 37,717 actual presented frames and the next native/Unity targets;
use it before inferring gains from older Unity callback or WPA samples.

The [exact-build native map](presentmon-baseline-2026-10-23.md#current-game-native-map-research-no-new-gameplay-patch)
supersedes the older whammy-write hypothesis below. v1.1 writes whammy data
through a `NativeArray<byte>` at a 5 ms step, so the `SetPixels` prefix added
in v0.4.74 had no observed matches. It and an untested `SetPixelsImpl` prefix
have been removed from the source. As of v0.4.78, the optional whammy gate
tracks the exact byte-writer and only permits an upload when its data changes;
older `Texture2D.Apply` observations below describe previous versions.
The map also identifies repeated `GuitarFretAnimator` sorting-order setters,
but their runtime cost has not been measured.

### v0.4.75 trace findings and mouse-path verification

The supplied CPU export covers about 31 seconds. Its largest identified
CloneHero UI samples map directly through the v1.1 `dump.cs`: `EventSystem.Update`
(1,610 sampled ms), `StandaloneInputModule.ProcessMouseEvent(int)` (1,451),
`GetMousePointerEventData(int)` (1,394), `EventSystem.RaycastAll` (1,358),
`GraphicRaycaster.Raycast` (1,105), and its rectangle-hit test (906). These are
inclusive sampled method weights; they overlap and must not be added as
exclusive CPU time. The existing mouse switch targets the exact
`ProcessMouseEvent(int)` entry that the dump shows on this path, bypassing
pointer creation and raycasts while preserving controller polling. The latest
runtime snapshots showed `suppressed=false` while the F8 menu was open, so
they did not prove that the bypass ran during the timing window. v0.4.75 counts
only calls actually skipped and reports the count at shutdown; the hot path
does not log or allocate.

The same export attributes samples to the supplied `version.dll`. PE exports
identify that file as `MelonLoader.Bootstrap.dll`, the loader bootstrap that
hosts its .NET runtime. The export has no usable call stacks, so those samples
cannot be separated into loader, CLR, Harmony, or SlopHero work. The largest
GameAssembly mouse/UI offsets map to the methods above. Other recurring input
offsets map into Rewired `InputManager_Base.DoUpdate` and `ReInput`; the
1000 Hz controller path remains untouched. Kernel stack-walk methods also
appear heavily in this capture, so WPA's sampling cost may influence the
captured run; it is not evidence of equivalent overhead during normal play.

The older source's `Helper.InvokeRepeating` is used only by its whammy updater
and catches up missed intervals in a loop. The current game uses a differently
named static updater with a 5 ms step. The v0.4.74 `SetPixels` experiment had
zero matches in v0.4.75's log; current native disassembly shows a raw
`NativeArray<byte>` write path. That hook was removed after the finding.

### v0.4.74 whammy CPU-write gate

The existing default-off cap rejected redundant `Texture2D.Apply()` uploads,
but only after the caller had already written its pixels. The older source
shows the CPU updater writing one `1x24` column to the shared whammy texture,
then applying it. v0.4.74 added a matching `Texture2D.SetPixels(int,int,int,int,
Color[])` prefix: it skips only a `1x24` write at `y=0` to the captured whammy
texture when the 200 Hz upload interval has not elapsed. The existing `Apply`
prefix remains the final guard and still controls row clears and any other
matching writes. If that Unity overload is unavailable, the mod logs the
fallback and keeps the previous Apply-only cap. The hook is only installed
when the experimental option is enabled in Gameplay; all unrelated textures,
input, scoring, and GPU compute dispatch remained untouched. The hook was
subsequently removed because the current game uses a different write path.

The latest v0.4.73 log matched 20,205 Apply calls to the whammy texture and
skipped 8,982, confirming the upload gate was active. It does not prove the
current game's producer uses the older source's exact `1x24` SetPixels shape.
The next log reported zero candidate write matches. No frame-time gain is
claimed from that hook.

### v0.4.73 mouse-raycast target correction

The latest v0.4.72 log had `PerformanceSkipGameplayMouseUi` enabled, but it
could only confirm that Harmony installed a hook. Disassembly of the supplied
v1.1.0.6142 GameAssembly showed `StandaloneInputModule.Process()` calling
`ProcessMouseEvent(int)` directly; its zero-argument overload is a forwarding
stub that `Process()` does not call. v0.4.73 now patches the integer overload reported in the WPA
trace, so it can return before pointer-data creation and UI raycasting while
leaving the rest of `Process()` available. The same log also had the separate
solo `EventSystem.Update` pause enabled; that broader option suspended UI
events during the timing capture, so the log does not isolate or measure the
mouse-only switch. No FPS or frame-time gain is claimed until the corrected
hook is compared with that broader option off.

### v0.4.72 source and WPA follow-up

`Core.OnUpdate` was checking the whammy-cap and highway-animation preference
values on every Clone Hero simulation update, although both are menu/profile
settings and their hook changes are independent of guitar input. At the
2,544.6 updates/s seen in the newest solo log, this repeated those two reads
and comparisons roughly 2,500 times each second. Version 0.4.72 checks them
at 20 Hz instead, so live changes take effect within 50 ms. Scene changes
still reconcile immediately. This removes redundant mod work; it is not a
claim of a measurable FPS increase.

The supplied WPA CPU sample export also maps repeated `GameAssembly.dll`
samples into Unity UI input paths: `EventSystem`,
`StandaloneInputModule.ProcessMouseEvent`,
`PointerInputModule.GetMousePointerEventData`, `GraphicRaycaster`, and
`RectTransformUtility.RectangleContainsScreenPoint`. This is independent
support for the existing experimental mouse-UI skip. Rewired's
`InputManager_Base.OnGUIUpdate` and `ReInput` paths also appear in the sample,
so the 1000 Hz instrument polling path remains untouched. The export has no
native symbols for those offsets, and inclusive stack samples do not by
themselves quantify exclusive CPU time.

### v0.4.71 online counter source and latest capture

The newest v0.4.70 log is a solo run, so it does not provide evidence about
online chart/audio synchronization or network counter correctness. It reports
2,544.6 simulation Updates/s, p95/p99 Update intervals of 0.58/0.67 ms,
scheduled-render p95/p99 intervals of 0.97/1.17 ms, and a 0.002 ms average
SlopHero `OnUpdate`. The counter Canvas sync averaged 0.020 ms over 598 calls;
its 5.08 ms maximum is a single outlier, and the log notes that this bucket
does not measure Canvas rendering or GPU work. This does not justify a new
performance patch or a claim of an FPS gain.

The online counter bug had a clear code-level cause: player selection used
`isRemotePlayerPlaying`, which Clone Hero updates from remote combo activity
and which drops back to false when the remote combo resets. The older source
also has a stable local identity: `CHPlayer.player` (the Rewired controller) is
present for local players and absent for remote-owned slots. Version 0.4.71
uses that controller-backed identity for both initial selection and cached
fallback, then reads the selected guitar engine's existing counters. This
should prevent remote slots from being mistaken for the local run; online
verification is still needed. Turning the Counter HUD master switch off now
hides the Canvas immediately and clears cached display text.

No change here modifies online transport, audio clocks, note scheduling, or
1000 Hz controller polling. The provided solo log cannot establish the source
of online-only desync, so those paths remain untouched pending a matching
online trace or a reproducible paired capture.

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
`StandaloneInputModule.ProcessMouseEvent(int)` overload, which avoids the
captured mouse path before it reaches pointer raycasts; the parent input module
continues into its keyboard/controller navigation. Mouse
processing is suppressed only while the setting is enabled and Clone Hero
reports active, unpaused gameplay. When enabled, the hook is installed as the
Gameplay scene loads and stays pass-through until the existing 20 Hz
game-state check reports active play. Pause, settings, and focus changes
immediately restore mouse processing
through the prefix; the hook is removed when the option is turned off, on scene
changes, and at shutdown. Installing it before a chart starts avoids Harmony
patch/JIT work during active play. With the option off, no mouse hook remains
installed. The preference is included in saved profiles. Instrument polling
and the 1000 Hz Rewired update are untouched.

The tradeoff is explicit in the menu: mouse clicks and hover interactions on
gameplay UI are unavailable while a chart is active and return on pause. The
implementation compiles, but the trace is still a lead rather than an A/B
result. It does not establish a frame-time gain; that requires a matched run
with the option off/on and presentation or frame-time measurements.

The first v0.4.59 runtime log showed the original lookup failing closed with
`StandaloneInputModule.ProcessMouseEvent()` unresolved. v0.4.60 corrected the
visibility lookup, and later logs confirmed a hook was installed. A native
call-site check against the supplied GameAssembly and generated dump now shows
that the installed zero-argument wrapper was the wrong target: Unity's
`StandaloneInputModule.Process()` calls `ProcessMouseEvent(int)` directly
(GameAssembly RVA `0x2ADE70F` calls `0x2ADCFA0`), while the zero-argument method
at RVA `0x2ADD240` is only a forwarding stub. Thus the old logs showed
installation, not skipped raycasts. v0.4.73 targets the exact int overload
identified in the WPA export. The initial lookup failure and the later
16.9 ms `OnUpdate` maximum remain historical observations; timing alone did
not establish a causal link to Harmony patching.

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

### v0.4.61 runtime verification and particle lead

The new `Latest.log` confirms v0.4.61 against Clone Hero 1.1.0.6142. The
experimental mouse-UI hook installed on Gameplay scene load, before the
30-second timing capture, and reports `mouseUiSkip=True[hook=True]`. The log
contains no SlopHero exception. Its single Il2CppInterop `Class::Init` warning
is emitted during loader startup and is not attributed to SlopHero.

During the capture, simulation Update deltas were at or below 0.54 ms at p95
and 0.63 ms at p99; scheduled-render callbacks were at or below 0.94/1.07 ms
at p95/p99. One simulation interval reached 40.121 ms, while the longest
scheduled-render callback interval was 6.593 ms. This is not a controlled
comparison with mouse processing enabled, and these callback intervals are
not PresentMon frame times. The hook's activation and lifecycle are verified;
its performance benefit is not.

The one-shot runtime snapshot confirms the existing visual suspensions engaged:
the hidden offline leaderboard had zero of six eligible entry updates, all ten
fret-fade scripts and both combo-smoke `UVScroll` components were paused, and
the viewer had 48 active guitar sprites with no GHL or drum sprites. One
`CameraShake`, one lyric component, and one particle system were active. The
supplied Gameplay scene identifies that particle system as
`HUDCam/HUD/Star/Particle System`: it is a non-looping, non-autoplay two-second
burst with a 40-particle cap. This points to the star-progress burst effect,
not an always-running emitter. The scene data and one runtime snapshot do not
measure its CPU/GPU cost, so this is only a small visual-effects candidate.
After the user approved it, v0.4.62 adds a separate default-off
`PerformanceDisableStarProgressParticles` option. It targets the live
`GameManager.starProgress.starParticles` reference only, stops and clears its
particles, then deactivates that dedicated particle-system GameObject. It
checks for replacement or re-enabled instances at 1 Hz only while opted in and
restores the object's original active state when switched off or when the
Gameplay scene ends. The star bar and score state remain untouched. No frame
gain is claimed; the 40-particle cap makes this a small, reversible visual
effect option rather than a high-yield optimization.

The Canvas counter synchronization averaged 0.019 ms across 598 calls, with a
single 4.898 ms maximum. `CounterCanvasHud.Refresh` only assigns text when the
display string changes, but changing text can trigger Unity/TMP canvas work
outside the measured property-sync scope. The isolated maximum does not prove
that a canvas rebuild caused the 40 ms simulation gap. A Unity Profiler capture
showing `Canvas.BuildBatch`, TMP mesh generation, and the main thread would be
needed before changing the retained HUD path. No controller polling or note
timing was changed during this review.

### v0.4.62 runtime and source review

The newest `Latest.log` confirms v0.4.62 loaded on Clone Hero 1.1.0.6142 and
completed its 30-second capture without a SlopHero exception. The mouse-UI
experiment was installed and active; the Canvas HUD path was enabled. The
star-progress particle switch was enabled and reports `starSuppressed=True`;
the one-shot snapshot consequently found zero playing particle systems. This
verifies the option engaged, but it is not an off/on performance comparison.
Measured SlopHero callback averages stayed small: Update 0.002 ms, Canvas HUD
sync 0.012 ms, visual overrides 0.016 ms, and performance effects 0.078 ms.
The simulation interval was at or below 0.58 ms at p95 and 0.67 ms at p99;
the scheduled-render callback interval was at or below 1.01/1.17 ms. A single
43.75 ms simulation interval remains unexplained. These are callback intervals,
not GPU or display-present timings.

I rechecked the earlier WPA CPU-method export alongside the exact-build
Il2CppDumper output. The captured mouse path is identifiable in managed
symbols: `StandaloneInputModule.ProcessMouseEvent` (1,451 sampled CPU ms),
`PointerInputModule.GetMousePointerEventData` (1,394 ms),
`EventSystem.RaycastAll` (1,358 ms), both `GraphicRaycaster.Raycast` overloads
(1,105 and 914 ms), and `RectTransformUtility.RectangleContainsScreenPoint`
(906 ms). These are sampled method weights from a roughly 31-second capture;
do not add nested methods or read them as GPU time. Their presence supports the
existing default-off mouse-UI experiment, whose current log confirms it is
active, but the profile predates this latest run and therefore does not measure
the experiment's effect.

The same export attributes 2,116 sampled CPU ms to
`Rewired.InputManager_Base.DoUpdate`. That remains untouched; it is core input
work, and changing it would risk the user's 1000 Hz controller polling. The
note-render update is visible but much smaller in this capture (about 114
sampled CPU ms in one generic specialization). The export cannot establish
whether the 43.75 ms interval came from garbage collection, a note burst, a
driver wait, or another thread.

Reviewing the older source archive uncovered two worthwhile but unproven
renderer leads. First, the whammy texture helper schedules a 200 Hz update from
each guitar renderer; its CPU fallback calls `Texture2D.Apply` every tick,
while the GPU path uploads a buffer and dispatches a compute shader. A shared
200 Hz cap could remove duplicate updates in multiplayer without changing the
solo cadence, but current logs do not measure its call rate or cost. Current
GameAssembly string literals confirm the CPU whammy texture and global texture
still exist; they do not confirm the old scheduler survived unchanged. Any
runtime patch should therefore remain experimental and preserve controller,
whammy input, audio, note timing, and sustain scoring.

Second, the older note renderer uses yield-based `GetNextBitPos` and pool-range
enumerators inside per-frame render loops. Those iterator state machines may
allocate, but the current obfuscated build's profile does not identify these
allocations or show the note-renderer loop as a large CPU consumer. A Harmony
rewrite of renderer loops would be higher risk than the evidence supports, so
this is a profiling lead rather than a patch target. The safe next focus is to
confirm current whammy scheduling and compare the already-installed mouse
experiment with matched traces; neither finding justifies touching scoring or
controller polling.

The user approved a default-off whammy experiment. v0.4.63 adds a narrowly
scoped hook for `Texture2D.Apply()` on the object the game assigns to the exact
`_GlobalWhammyTexture` shader property. While gameplay is active, it permits at
most one upload every 5 ms (200 Hz) and suppresses closer repeats. The hook is
Texture assignment tracking stays active while the option is enabled so the
target is known before a chart starts. The global `Texture2D.Apply` prefix is
installed only in Gameplay and is removed on scene exit, avoiding per-upload
hook work in menus and other scenes. The hot prefix compares cached native
object pointers instead of Unity's overloaded object equality on every upload
callback. It does not hook compute dispatch or alter whammy input, controller
polling, audio, chart timing, or scoring. The pointer comparison's CPU effect
and the cap's net benefit remain unmeasured.

### v0.4.63 paired online report

The supplied logs do not capture the same runtime state. The user's log loads
SlopHero 0.4.63 and ClonZones 2.0.0 on Windows 10 with MelonLoader 0.8.0; the
friend's log loads SlopHero 0.4.62 on Windows 11 with MelonLoader 0.7.3. Both
target Clone Hero 1.1.0.6142. The friend's file contains startup and preference
save lines only; it has no Gameplay scene, timing capture, connection state, or
counter readings. The user's file has many scene transitions and one 30-second
timing capture, but no online/connection marker or live counter values. Neither
file can establish which runtime state produced the audio/chart mismatch.

The user reports the mismatch occurred online only, and both players saw it.
That makes an offline practice-speed issue unlikely. `VisualOverrides` applies
constant note-speed compensation only when the game's practice flag is set.
Version 0.4.63's only gameplay hook added after 0.4.62 is the optional
`_GlobalWhammyTexture` upload cap; it intercepts matching `Texture2D.Apply`
calls and does not write the chart clock, audio, note state, network state, or
controller input. The friend did not have that hook in 0.4.62, so it cannot by
itself explain a desync seen by both players. The files do not prove the base
game is at fault; they simply contain no network/audio timing evidence to
locate the cause.

The user capture records `renderFrameInterval=2`, simulation updates at
2499.5/s, and scheduled render callbacks at 1249.7/s. These callback rates are
not display-present measurements; the render interval is a game setting, not a
SlopHero write. Its p95/p99 simulation intervals are at or below 0.56/0.63 ms,
and scheduled-render intervals at or below 0.96/1.08 ms. SlopHero's measured
Update average is 0.002 ms; visual overrides average 0.015 ms, and the
performance-effects pass averages 0.079 ms across 60 calls. This points to low
mod-side CPU cost during this capture, but the capture does not compare online
and solo or measure audio/network timing. The prior WPA trace's largest
identified mod-adjacent CPU path was Unity mouse UI raycasting; the user's
current settings report that optional path active. Rewired polling remains
untouched to preserve the requested 1000 Hz controller rate.

The online counter selects an enabled, input-enabled, nonremote GuitarPlayer
and reads that player's native engine hit/ghost fields plus its chart heads.
The same selector is present in 0.4.62. Since the friend's file has no runtime
state, it cannot show whether Clone Hero exposes those engine counters as
player-local or session-aggregated in online play. Changing the selector or
reconstructing multiplayer misses without that evidence risks presenting a
different wrong value; solo correctness does not validate the network engine
case.

The 0.4.63 log also exposed a telemetry contradiction: IL2CPP reports the
captured global texture's wrapper type as `Texture`, while the allowed/skipped
counts show 699,401 matching `Texture2D.Apply` calls (407,309 allowed and
292,092 skipped). The wrapper type was incorrectly treated as proof that the
cap could not match. The runtime message now reports that wrapper type as
inconclusive and reports actual Apply matches separately; timing settings also
include the cap toggle and hook state. This correction changes diagnostics
only, not throttle behavior.

### Whammy/raycast follow-up

The whammy tracker records global texture assignments while enabled, including
assignments made in the pre-song menu. Scene-load handling installs the cap
before Gameplay's first normal frame update. On scene exit it removes the
global `Texture2D.Apply` prefix but keeps the low-frequency assignment tracker
active while the preference remains enabled. This avoids adding a Harmony call
to unrelated texture uploads in menus without losing the selected texture.
Inside the hot prefix, cached `Il2CppObjectBase.Pointer` identity replaces
Unity object `!=`, avoiding overloaded Unity-object comparisons on every upload
candidate. The cached `Texture` wrapper remains strongly referenced while the
tracker is active. The 200 Hz cap itself is unchanged, and no benchmark yet
quantifies this per-call reduction.

The mouse experiment now targets the measured Unity
`StandaloneInputModule.ProcessMouseEvent(int)` handler before pointer-data
creation and raycasting when the chart is active. It remains default-off and
has no effect on keyboard/controller navigation or Rewired polling. A broader
`EventSystem.RaycastAll` bypass would also suppress raycasts requested by
non-mouse UI code, so the trace does not justify broadening the patch. The
latest v0.4.72 log had the option enabled, but that version patched only the
unused wrapper and therefore did not test the mouse-only optimization. The next
useful comparison is a matched WPA capture with the corrected v0.4.73 hook on,
the separate full EventSystem pause off, and the same chart, scene, resolution,
and frame multiplier as baseline.

### v0.4.64 log and v0.4.65 follow-up

The new 0.4.64 log confirms both experimental hooks installed in Gameplay and
reports 10,551 `Texture2D.Apply()` calls on the texture captured for
`_GlobalWhammyTexture`: 6,009 allowed and 4,542 skipped. This establishes the
cap engages on this setup; it does not measure the CPU cost of pixel preparation
before `Apply`, the upload cost, or an FPS gain. The log's 30-second capture
observed 2,492 simulation updates and 1,246 scheduled render callbacks per
second. SlopHero's `OnUpdate` averaged 0.002 ms, visual overrides 0.016 ms,
Canvas HUD sync 0.013 ms, and performance effects 0.078 ms at their own lower
call rates. A 38.133 ms simulation-update gap remains unattributed by these
per-callback timers. No SlopHero exception appears in the file.

Version 0.4.65 removes two redundant mod paths: scene loading already sets
the whammy hook state, so the ordinary simulation loop reconciles it only when
the preference changes; and Pong object discovery, which was previously run
every two seconds, is skipped in Gameplay while April Fools and NoteBomber
suppression still runs. The Gameplay scene and live snapshot contain no Pong
object. Discovery resumes in menus. These changes preserve the option behavior
but are expected to have small, unmeasured frame-time effects.

Current native `HighwayScroll.Update` checks
`OnDemandRendering.willCurrentFrameRender` before it touches the sprite's
material property block, so skipping its Update on non-rendering simulation
ticks would duplicate an existing game optimization. Current `CameraShake.Update`
returns immediately when its animation flag is clear, making its single active
instance a weak performance target. The older whammy source writes pixels
before calling `Texture2D.Apply`, but its renderer startup differs from v1.1;
the current producer and cost are not yet established. Suppressing a guessed
producer would risk whammy visuals or timing, especially online. A matched
current-version CPU/GPU frame capture with the existing mouse/whammy switches
off and on is the next evidence needed for a larger game-side rewrite.

### v0.4.65 log and persistent-service check

The current log reports about 2,423 simulation updates/s and 1,211 render
callbacks/s over 30 seconds, with a 39.757 ms maximum update interval. Its
measured mod callbacks remain below the scale of that outlier. The supplied
CPU export shows expensive Rewired input and Unity mouse-UI paths, but has no
samples attributed to `CheatManager.Update`; a large gain from that scanner
would be unsupported.

The older source makes `CheatManager` persistent across scenes and loops over
players and registered cheat codes each Update. Current 1.1 native code
confirms the loop, and its `OnEnable`/`OnDisable` methods are empty. Version
0.4.66 adds a separate, default-off switch that disables only this component
during active, offline, single-player songs. It restores the component on
pause, menu entry, scene exit, or option disable, preserving its registrations
and leaving normal instrument input and online play alone.

Two other persistent-service leads were rejected: current
`GlobalVariables.Update` polls fullscreen state and notifies listeners when it
changes, while `CHNetManager.Update` returns quickly while disconnected and
accounted for only about 1 ms of sampled CPU in the supplied capture. Current
`BuildCheck.Update` handles an update prompt, and the older source places its
network request in a startup coroutine rather than a per-frame poll. None
supports a safe gameplay-wide disable from the evidence available.

### v0.4.67 broader UI-event experiment

The same 31-second ETW export attributes substantial inclusive CPU weight to
Unity UI event processing: about 1,610 ms in `EventSystem.Update`, 1,484 ms in
`StandaloneInputModule.Process`, 1,451 ms in mouse processing, and 1,358 ms in
`EventSystem.RaycastAll`. These values overlap and come from the older trace;
they are leads, not a current-build FPS result. The v0.4.66 log confirms the
narrow mouse hook installed, but does not measure a paired before/after run.

Version 0.4.67 adds a separate, default-off experiment that skips
`EventSystem.Update` only during active offline solo songs. It restores the
callback on pause, menu entry, scene change, lost focus, or when the option is
off. This also suspends UI keyboard/controller navigation during the song;
instrument polling remains on. The setting has transition-only log messages so
the next capture can verify it engaged without per-frame logging. If it causes
pause, input, or UI problems, turn it off in the Sloptifine tab; it does not
change chart timing or score logic.

### v0.4.68 scanner state follow-up

The latest v0.4.67 log confirms the broad UI hook suspended and restored
correctly. It also records the cheat-scanner pause option as enabled while its
state remained unpaused, so that optimization was not confirmed as active.
Version 0.4.68 adds a single fallback lookup when the game's
singleton is unavailable and reports whether the scanner is paused, already
disabled, still running, or missing. This lookup is only attempted while the
solo pause option is eligible, not on every simulation tick.

The same run matched 19,014 CPU `Texture2D.Apply` calls to the shared whammy
texture and skipped 7,756 under the configured cap. This confirms the cap is
working on that texture; it does not measure GPU compute work or establish an
FPS increase. The two timing captures are not a controlled A/B, so they cannot
quantify the UI hook's gain.

### v0.4.68 follow-up capture

The newer v0.4.68 log confirms that the broad `EventSystem.Update` pause was
active for the full 30-second timing window. The narrower mouse-UI hook was
enabled at the same time, so this capture cannot distinguish their effects;
with the broad hook suppressing `EventSystem.Update`, the mouse handler is
downstream and should not run. A useful future comparison needs to vary those
two switches independently. Rewired polling remained untouched.

The cheat-scanner option was also enabled, but the active-state transition log
reports `already-disabled`, so it had no scanner work to remove in this run.
The later F8 settings snapshot says `state=missing` after the mod restored the
scanner and cleared its cached reference; that is not evidence that the scanner
ran during the timing window.

The 30-second capture reports 2,593.1 mod Update calls/s and 1,296.5 scheduled
render callbacks/s. `Time.unscaledDeltaTime` samples were at or below 0.56 ms
at p95 and 0.62 ms at p99, with a 38.419 ms maximum. Scheduled-render callback
intervals were at or below 0.92/1.01 ms at p95/p99, with a 10.151 ms maximum;
GUI repaint intervals reached 9.612 ms. These are separate Unity-side signals,
not presented-frame timings, and the log does not correlate individual long
samples across them. It shows mostly short intervals with rare outliers, but
cannot tell whether those outliers were visible frame hitches.

SlopHero's measured callbacks remain small in this capture: `OnUpdate` rounds
to 0.002 ms across 77,794 calls, performance-effect synchronization to
0.087 ms across 60 calls, Canvas property synchronization to 0.011 ms across
598 calls, and HUD repaint to 0.058 ms across 369 calls. This makes the mod's
measured managed callbacks an unlikely source of the large isolated interval;
it does not include Harmony-prefix cost, Unity Canvas rebuild/render cost, or
GPU work.

The whammy cap matched 18,107 `Texture2D.Apply` calls and skipped 7,461
(41.2%). This proves the cap is active in the observed CPU upload path. It
does not prove a frame-time benefit: it does not skip any work the producer
may perform before `Apply`, nor GPU compute dispatch. The older 2019.4 source
dump and generated current metadata and native DLL are enough to keep screening
candidates; no more asset chunks or current-build symbols are needed for this
work.

Rechecking the supplied CPU-method CSV exposed another unresolved cluster:
`version.dll` offsets `+0x2CFD57`, `+0x9C4C`, and `+0x2D2124` each carry about
8.9 seconds of sampled CPU weight in the 31.1-second capture. Their weights
closely track the two large `GameAssembly` offsets above (about 9.0 seconds),
so these likely overlap along a hot call path and must not be added together.

The uploaded 7.7 MB x64 `version.dll` is the MelonLoader native bootstrap
proxy: it embeds `MelonLoader.Bootstrap`, and the matching game log reports
MelonLoader v0.8.0-ci.2548. Disassembly places the hot addresses immediately
after indirect calls (`call *%r14`, `call *%rbx`, and `call *0x20(%rax)`).
These are delegate/interface call return sites, so sampled weight attributed
there may be work inside the invoked callback, not overhead in the proxy
wrapper. Its PE debug directory contains type-13 COFF-group metadata but no
CodeView/PDB symbols; the exact callback names cannot be recovered from this
DLL alone.

The same method export names 1,623 ms in `MelonLoader.dll`'s
`IL_STUB_ReversePInvoke`, 960 ms in `IL_STUB_PInvoke`, 400 ms in
`MelonEventBase.Invoke`, 101 ms in `SlopHero.Core.OnUpdate`, and 143 ms in
`SlopHero.Core.OnLateUpdate`. These are overlapping sampled stack weights,
not additive totals. They show that the named SlopHero callbacks are much
smaller than the unresolved ~8.9-second path, but do not locate which callback
or patch owns that path. The process/thread stack exports supplied with this
capture have empty stack columns. A stack-bearing trace from the same capture
would be more useful than additional game assets or the older 2019 source.

The native `GameAssembly` PCs are also nested, not independent hot methods.
Both resolve to its `.text` section. `GameAssembly+0x2FAB159` is the return
site immediately after a direct call to `GameAssembly+0x2FAB600`; the second
hot PC, `GameAssembly+0x2FAB63D`, is the return site after an indirect method
call inside that child function. The exported
`il2cpp_runtime_invoke` at `GameAssembly+0x2F80B50` jumps to the parent at
`+0x2FAB100`, which calls the child. This ties the roughly 9-second matching
weights to IL2CPP's reflection-style method-invocation path, not three separate
game loops. PE unwind data recovers the function ranges without a PDB.

The `version.dll+0x9C4C` return site maps to MelonLoader's IL2CPP
`InvokeDetour`: the binary contains the exact `Internal_ActiveSceneChanged`
and `Invoke hijacked` strings used by its [public source](https://github.com/LavaGang/MelonLoader/blob/master/MelonLoader.Bootstrap/RuntimeHandlers/Il2Cpp/Il2CppHandler.cs), and the surrounding
logic matches `Il2CppHandler.InvokeDetour` forwarding to the original
`il2cpp_runtime_invoke`. That detour stays on the invocation path, but once
startup is complete it quickly forwards and returns. The sampled address is
after the forwarded call, so its 8.9-second weight mostly describes work in
the underlying invocation and callback, not measured MelonLoader wrapper cost.
SlopHero uses reflection only to resolve Harmony targets during setup; a source
scan found no `MethodInfo.Invoke` in its per-frame paths. The trace still lacks
the caller stack needed to name which game callback, Harmony patch, or other
mod is issuing the expensive invocations. This is now the strongest lead, but
it is not a safe patch target until that caller is identified.

### Next screening lanes (no PDB required)

There are several independent paths worth pursuing; none requires current
build symbols:

1. **Name the hot IL2CPP caller.** Re-export the existing ETL's sampled CPU
   view with Clone Hero expanded through Thread and Stack. Even unresolved
   module-plus-offset frames will reveal which caller enters
   `il2cpp_runtime_invoke`; the supplied `GameAssembly.dll` can then be
   disassembled around those addresses. This is the highest-value lead because
   it can separate game callbacks, Harmony patches, and other mods.
2. **Map Unity's native hotspots.** The method export contains several large,
   overlapping `UnityPlayer.dll` offsets and NVIDIA-driver frames. The exact
   installed `UnityPlayer.dll` will let us disassemble those offsets without a
   PDB; stack frames will show which path reaches them. Their inclusive export
   weights alone are not exclusive CPU costs or proof of a GPU bottleneck.
3. **Separate existing gameplay experiments.** The v0.4.68 run enabled both
   broad `EventSystem.Update` suspension and the narrower mouse-event skip, so
   it cannot attribute any change. Compare each independently with the same
   chart/settings. The whammy hook skipped 41.2% of matching CPU texture
   uploads, but needs its own frame-time comparison and still leaves producer
   and GPU-compute work intact.
4. **Audit hook overhead while options are off.** SlopHero's named callbacks
   are small, but current timing buckets do not include Harmony prefix/postfix
   cost. Inventory every persistent patch and test whether an inactive optional
   feature can remove its hook entirely, preserving input polling and immediate
   feature behavior. Any candidate should be gated by call frequency from the
   stack-bearing trace before changing code.

The immediate file that would enable lane 2 is the exact `UnityPlayer.dll`
from the same Clone Hero installation. Lane 1 can use the ETL already captured;
it needs a stack-bearing WPA export, not a PDB or another asset bundle.

### Current native files and hotspot disassembly

The newly supplied `UnityPlayer.dll` is Unity 2022.3.62f2 and includes
unwind records covering the sampled addresses. It has a CodeView PDB identity,
but no PDB was supplied; function bounds and call instructions are still
recoverable from the PE. The exact bytes are SHA-256
`43fb0aea1b80c74963dae88ec7fc1c3c1993d893ce94d12972000fcc9b653ad1`.

Disassembly sharpens, but does not yet name, the top native rows:

- `UnityPlayer+0x6B3C1C` is immediately after an indirect callback call. The
  surrounding function clears callback state and returns through a helper.
  The call target is dynamic, so this remains a strong stack-trace target.
- `UnityPlayer+0x577AB9` is a recheck/loop site after a helper that manipulates
  atomic ring-buffer state. This resembles worker-queue scheduling; the stack
  is needed to tell whether the sampled thread is doing useful work or waiting
  for it. Do not hook or throttle it by address.
- `UnityPlayer+0x8050DB` sits in a Windows event/timer path that calls
  `PeekMessageA`, `GetMessageW`, `CreateEventA`, and `timeBeginPeriod`. These
  services can affect window input and frame pacing, so this is diagnostic
  evidence rather than a suppression target.
- `UnityPlayer+0x80869B` follows a helper and calls `GetCurrentProcess` and
  `TerminateProcess`. That shape is consistent with an exit/error path; its
  large inclusive CPU attribution is not credible as steady gameplay work
  without the caller stack.

The other uploads add context rather than a new per-frame removal target.
`baselib.dll` is the matching Unity 2022.3.62f2 runtime library; its largest
sampled address in this export is only about 114 ms inclusive. `Plugins.zip`
contains BASS audio libraries, RtMidi, Rewired input backends, Discord SDK,
HTTP, file-picker/process helpers, and Burst. The method export attributes
about 1.7 seconds inclusive to BASS and about 73 ms to Discord SDK, while the
current hot rows do not identify a removable plugin workload. Preserve audio,
1000 Hz instrument input, online services, and other player-facing features.
The plugin CPU figures are inclusive stack weights, not exclusive costs.
`Resources.zip` contains only Unity's built-in resource files, and
`il2cpp_data.zip` contains metadata plus framework resource blobs; neither adds
active gameplay code to inspect.

One low-priority stutter lead is managed JIT activity: the export attributes
roughly 190 ms inclusive to CoreCLR JIT compilation during the 31-second
window. It may be normal first-use/background compilation rather than a
gameplay hitch. Compare a warmed-up capture with a just-launched capture before
considering any code warm-up. `ClonZones`'s largest named detour is about 271 ms
inclusive in this trace; that is small, and the user's prior removal test saw
no noticeable change, so it is not a current optimization target.

### Additional gameplay-source screening

I compared the old source's per-frame renderer and player paths with the
current 1.1.0.6142 metadata and the existing method export. The old source is
not a safe patch specification, but it helps identify what to measure next:

- The older `HighwayScroll.Update` used `Renderer.material.mainTextureOffset`.
  The exact current build has already moved to a per-renderer
  `MaterialPropertyBlock`: its native Update at RVA `0x3818E0` reads `songTime`
  and calls `Renderer.GetPropertyBlock`, `MaterialPropertyBlock.SetVector`, and
  `Renderer.SetPropertyBlock` each update. That removes the old material
  instance concern, but the read-modify-write still crosses into Unity every
  frame. The method was not named in the CPU export, so count and cost are
  unknown. It is a concrete low-risk profiling target; do not suppress or
  replace it until its use and effect on highway rendering are measured.
- The old `Fret_Animator.Update` repeatedly assigns sprite, mask, sorting-order,
  and enabled state even when the held/pressed state has not changed. The
  current build still has `GuitarFretAnimator`, but this capture does not
  identify its native Update cost. The named ClonZones detour is about 271 ms
  inclusive and prior removal testing showed no visible gain, so neither is a
  compelling optimization target yet.
- `BasePlayer.UpdateHitDetection` walks chart notes in the active hit window,
  marks misses, and advances chord state. Its cost scales with notes currently
  in range, but it is intertwined with hit windows, late misses, drums, and
  online state. Preserve it; the measured player-update weight is modest, and
  any scoring rewrite would carry a high correctness risk.
- The old `MIDIInputManager.Update` body is empty; MIDI work is handled through
  device callbacks and player mapping. The current trace does not name a MIDI
  hot method. This argues against claiming that disabling the manager improves
  frame performance, while keeping the separate opt-in for users who do not
  use pro drums remains reasonable.

The useful next measurement is an exact-build call count for `HighwayScroll`
and `GuitarFretAnimator` Update methods, ideally with native-inclusive time.
Until those are named in a trace, the established mouse-raycast path remains
the only game-side candidate with strong, directly attributable evidence.

### v0.4.69 highway-animation experiment

The exact-build `HighwayScroll.Update` disassembly confirms a per-update
`Renderer.GetPropertyBlock` → `MaterialPropertyBlock.SetVector` →
`Renderer.SetPropertyBlock` sequence, followed by background-video/track-fade
state handling. The existing CPU-method export did not attribute measurable
exclusive cost to this method, so the potential frame benefit is unknown.

After the user authorized an experimental build, v0.4.69 adds the separate,
default-off `PerformancePauseHighwayAnimations` option. When enabled, it hooks
`HighwayScroll.Update` only in Gameplay and skips the whole visual updater only
during an active, focused, offline solo chart. It resumes on pause, menu focus,
multiplayer/online play, and scene exit. This freezes highway texture motion,
background video, and track-fade presentation while active; it does not alter
notes, timing, audio, score, or input. The hook is installed only when the
option is enabled. The capture confirms the target and scope, not a performance
gain; compare the same solo chart with the option off and on before treating it
as an optimization.

### v0.4.70 FC ring hook gating

`RingColors` previously installed four Harmony postfixes at mod startup even
when `CustomizeFcRings` was false. v0.4.70 installs those hooks only in the
Gameplay scene while custom ring colors are enabled, and unpatches them when
the option is turned off or the scene exits. The existing ring-color update
still restores captured vanilla values when customization is disabled. This
removes optional callbacks from the game's desaturate setters for players who
leave custom rings off; the CPU benefit is plausible but not measured.

The supplied method export has no usable call stacks, so it cannot name the
caller above the reflected IL2CPP invocation path. That uncertainty does not
block the mouse-path correction or the narrowly matched whammy-write gate in
this release. Both can be checked from their existing runtime diagnostics;
there is no new WPA capture step for v0.4.74.

### v0.4.77–0.4.78: use the actual whammy data-change path

The v1.1.0.6142 generated wrapper maps TypeDef 993 to
`Il2Cpp.ObjectPublicAbstractSealedInBoInObDoInSiDoInDoUnique`. Its
`Method_Public_Static_Void_1()` method corresponds to native RVA `0x22BE10`,
the static whammy upload wrapper called from `GameManager.LateUpdate` (RVA
`0xB4500`). The method calls the CPU texture's `Apply` wrapper. Its producer
advances in 5 ms steps, so a 200 Hz upload ceiling matches the producer's
maximum update cadence.

v0.4.77 moved the opt-in cap from `Texture2D.Apply()` to this exact game
wrapper. v0.4.78 completes the path: a postfix on the identified raw byte
writer increments a data version, while the upload prefix compares that
version with the last successfully uploaded one. It skips the full game
wrapper when bytes have not changed and removes per-frame stopwatch sampling.
The upload postfix records the version only after a successful upload, so a
write arriving during an upload remains pending for the next call. The two
global `Shader.SetGlobalTexture` tracking patches are gone. Raw byte writes and
GPU compute remain untouched. This is code-path work saved; its frame-time/FPS
effect is not yet measured. The preference remains off by default, and the
compatibility hash prevents enabling the native-map-dependent mod on a
different game build.

The native map also identifies repeated idle fret sorting-order setters, but
the supplied current log shows ClonZones 2.0.0 already installs a
`GuitarFretAnimator.Update` settle gate. Duplicating that patch in SlopHero
would not save another call in this setup, and its exclusive CPU cost remains
unmeasured.
