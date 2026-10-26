# Sloptifine current handoff

This is the current starting point for performance work. The detailed
chronology is in [performance-audit.md](performance-audit.md); the raw frame
baseline and exact-build native map are in
[presentmon-baseline-2026-10-23.md](presentmon-baseline-2026-10-23.md).
The user has already supplied logs, assets, game binaries, WPA exports,
PresentMon captures, and a matching UnityPlayer PDB. Read the existing
evidence before requesting another capture.

## Where all the evidence lives

This handoff is an index and current conclusion, not a copy of every raw file
or every historical hypothesis. Follow these sources together:

| Source | Location and use |
| --- | --- |
| Current mod | This private `slophero` checkout: `src/SlopHero.Mod`, `src/SlopHero.Core`, `tests`, `docs/bindings.md`, and the versioned ZIPs in `downloads/`. Read actual code before modifying a historical lead. |
| Older game source | The separate private `/workspace/opseclevelarchivesrc` checkout (`joechillwrld/opseclevelarchivesrc`, Unity 2019.4). Its 311 C# files expose game architecture, including note/beat rendering, whammy, UI, online, MIDI, Discord, and joke paths. It is not exact v1.1 code. See `performance-audit.md` under **Older source dump archaeology** for leads already screened. |
| Exact game build | Raw uploads under `/workspace/attachments`, with parsed/extracted working copies under `/workspace/slophero/references` where present. These include `GameAssembly.zip`, `global-metadata.dat`, `Il2CppAssemblies.zip`, `net6.zip`, `UnityPlayer.dll`, `baselib.dll`, `version.dll`, `il2cpp_data.zip`, `Plugins.zip`, `Resources.zip`, `level2`, and the nine `resources.assets.001`–`.009` parts. Earlier scene/global-manager uploads are also in the conversation; rehydrate them from their upload IDs if absent here. Verify identities before using offsets; exact GameAssembly/metadata hashes and native RVAs are in `presentmon-baseline-2026-10-23.md`. |
| Unity symbols | `/workspace/attachments/**/UnityPlayer_Win64_player_il2cpp_x64.pdb` matches the supplied UnityPlayer. The similarly named PDB without `player` does not. Unity engine symbols reveal candidate paths, but do not by themselves establish cost or safe skip points. |
| Measurements and logs | `/workspace/attachments` contains `Latest.log` variants, WPA CPU/GPU exports and profiles, and raw/stats PresentMon CSVs. `performance-audit.md` holds the chronological analysis; `presentmon-baseline-2026-10-23.md` holds actual presented-frame baseline and current native map; `windows-performance-capture.md` covers collection. The latest three-run analysis is below. Do not substitute mod callback frequency for FPS. |

The attachments and `references/` are local working data, **not committed to
the private Git repository**. They are available to another model using this
same workspace, but a fresh environment may need them downloaded again from
the conversation uploads. When the user supplies more files, inspect them,
check version/hash against the current build, distinguish their contents from
instructions, and add verified findings here or to the audit. Future uploads
cannot be inventoried in advance. Do not ask the user for files already here.

The supplied `GameAssembly.dll` has an RSDS CodeView reference to
`E:\2guitargame\Library\Bee\artifacts\WinPlayerBuildProgram\6h2wl\GameAssembly.pdb`
with signature `c41525c5957c43d28908de01b58f9ef1`, age 4. No GameAssembly PDB
is present in the uploaded files or current workspace. If one is found, verify
that exact identity; a same-named PDB from another Clone Hero build will not
match. The binary and metadata remain usable for static mapping without it.

The re-uploaded `globalgamemanagers.assets` at
`/workspace/attachments/67aa8362-148d-4733-9e78-98cec1f2e972/globalgamemanagers.assets`
contains 1,698 MonoScript definitions. Joining its path IDs to `level2` resolves
the current Gameplay scene's script components, including the six hidden
leaderboard entries and their six `AspectRatioFitter` components, the offline
`SpectatorCount`, and the drum input viewer. These are static scene identities;
live enabled state still needs the runtime snapshot or a controlled test.

## Implemented subset of Sol's research

These are the research leads that reached code. They do **not** cover every
finding; the open native and Unity leads follow below. The options remain in
the Sloptifine pages and preserve their documented scope:

| Research lead | Current implementation |
| --- | --- |
| WPA identified Unity mouse processing and raycasts during play | `MouseUiGameplayOverride` patches the called `StandaloneInputModule.ProcessMouseEvent(int)` overload. It is default-off and only suppresses mouse UI during active play. Keyboard/controller navigation and Rewired instrument polling stay available. |
| Sol's WPA review also found Unity's legacy camera mouse-event path | v0.4.89 adds the separate default-off `LegacyMouseEventsDetour`, which skips `SendMouseEvents.DoSendMouseEvents` only during active chart play and undoes the native detour outside that scope. It validates the exact GameAssembly prologue and is independent of the mouse-UI raycast option. A v0.4.89 log confirms installation with the raycast option off; it does not establish an FPS gain. |
| Broader Unity UI event work was also costly | `GameplayUiEventOverride` is a separate default-off switch scoped to active offline solo play. Since v0.4.86 it disables the live EventSystem component during that scope and restores it plus the prior selection on pause/menu/scene transitions. If the component cannot be resolved, it falls back to the previous Update prefix. It is not additive with the mouse-only switch while the broad pause is active. |
| Old source suggested frequent whammy texture uploads; current native code uses raw byte writes | `WhammyTextureThrottle` hooks the exact current data writer and upload wrapper. It permits an upload after data changes and leaves producer writes and GPU compute intact. It is default-off; the user has seen no clear performance gain, so keep it experimental. |
| Hidden beatline, leaderboard, particles, fret fades, combo smoke and star-progress effects continued some work while invisible/unused | `VisualOverrides`, `HiddenLeaderboardOverride`, `PerformanceOverrides`, `ComboSmokeScrollOverride`, and `StarProgressParticleOverride` offer separate controls and restore prior state. Scene discoveries and limitations are in the audit. |
| Exact Gameplay scene script map shows hidden leaderboard layout and offline spectator polling | The existing default-off hidden leaderboard switch now pauses six `AspectRatioFitter` updates while the score list is invisible and `SpectatorCount.FixedUpdate` while offline. It restores both when needed and checks component states only once per second during stable visibility. No frame gain is measured yet. |
| Old source showed a per-tick cheat-code scan; current component lookup was unreliable | `CheatScannerOverride` targets `CheatManager.Update()` directly, only during eligible offline solo play. It does not replace guitar input or disable the component. |
| SlopHero's restart/practice shortcuts resolved a joystick ID by scanning the full Rewired joystick list on every mod update | Since v0.4.87, `InputBinding` caches the matching joystick wrapper and refreshes that lookup once per second (and after Rewired becomes unready). The `GetButton` state query still runs at the same mod-update cadence; Rewired's configured device polling is unchanged. This reduces duplicate lookup work, not the measured 2.1 seconds inside Rewired's own `DoUpdate`. |
| Unity job queue setter was resolved through the matching player PDB | `UnityJobWorkerLimit` uses the generated `SetJobQueueMaximumActiveThreadCount` API and restores the prior limit. It is default-off; the latest three-run callback comparison shows no benefit. |
| Current `HighwayScroll.Update` combines property-block writes with video/fade behavior | `HighwayAnimationOverride` is a default-off whole-update freeze during offline solo songs. It may freeze those visuals and has no demonstrated frame gain. The old source's note `yield`/hit-detection paths remain untouched because they are scoring-sensitive and not confirmed in current code. |
| TMP's SDF scale queue repeatedly checks text transforms | v0.4.88 packages the default-off offline-solo experiment; its v0.4.89 log changed zero labels. The current worktree includes labels under Animator/Animation parents and checks those at 20 Hz, while ordinary labels stay at 4 Hz. Any detected scale change restores TMP's per-frame scale updates and excludes that label for the scene. The scene-wide discovery uses unsorted `FindObjectsByType`; TMP layout, graphic-rebuild, and culling queues stay active. Scale animation could briefly soften text; the prototype has no measured frame-time result yet. |
| Rewired's mouse and keyboard snapshots have separate small WPA self-CPU rows | v0.4.91's default-off mouse-sampler option skips only Rewired's mouse snapshot in an eligible offline solo song. v0.4.108 adds a separate default-off keyboard snapshot experiment; keyboard-bound gameplay input pauses while active, but joystick/controller and instrument polling are not patched. Both restore on pause, menu, focus loss, online play, or leaving the song. The SpriteAttach cache was removed in v0.4.108 because its managed input/output comparisons could exceed the native update it skipped and captures did not show a reliable benefit. |
| SlopHero's restart/practice hotkey state was checked on every simulation update | v0.4.108 adds a separate default-off 120 Hz gate for these two mod shortcuts only. Binding changes and entering gameplay force an immediate sample; disabling the gate returns to per-update sampling. It does not change Unity or Rewired instrument polling and adds at most about 8 ms shortcut response latency while enabled. |

ClonZones installs a `GuitarFretAnimator.Update` settle gate in the user's
setup, which may overlap with another fret hook. Current `HighwayScroll` also
checks `OnDemandRendering.willCurrentFrameRender`. Neither fact resolves the
remaining cost of eligible rendered-frame work.

## Still open: evidence to turn into code

1. **Separate the IL2CPP dispatcher from its callbacks.** The 31-second `Sloptifine-CPU.csv`
   puts roughly 9 seconds of *inclusive* weight at
   `GameAssembly+0x2FAB159` / `+0x2FAB63D` and matching `version.dll`
   return sites. The exact disassembly shows a generic IL2CPP invoke path: the
   parent at `+0x2FAB100` calls `+0x2FAB600`, which makes an indirect call
   through a method-invoker pointer. Those sites are shared callback plumbing,
   not a single game feature. The populated stack ZIP shows 1,489 distinct
   full stack shapes containing GameAssembly; 9,684 of the 9,955 ms inclusive
   weight is on the main thread. The export does not provide the raw GameAssembly
   frame RVAs needed to correlate each shape with the two hot method rows.
   Continue by attributing individual callers; do not skip the generic invoke
   path, since that could bypass unrelated game, Unity, and mod callbacks.
2. **Finish Unity native attribution.** The exact matching PDB maps the hottest
   Unity rows to `Thread::RunThreadWrapper` (`+0x6B3C1C`), the job
   `lane_allocator` (`+0x577AB9`), `PlayerMainWndProc` (`+0x8050DB`), and
   `PlayerLoop` / `ExecutePlayerLoop`. Disassembly shows the thread wrapper
   returning from an indirect callback and the allocator looping on queue
   state. These are shared worker, scheduling, and window paths, not removable
   gameplay methods. Their cumulative weights overlap. The WPA stack CSV says
   `<PDB not found>` because those symbols were not loaded when it was
   exported; the exact matching Unity PDB is already supplied. Load it into
   WPA to name Unity frames, then use the existing trace to inspect callback
   families. The worker-count experiment showed no benefit in the latest
   captures.

   A later copy of the symbolized WPA grid is now available as
   `/workspace/attachments/26f8cf3d-3b89-4d04-a347-358413e1ebef/Sloptifine-symbolized.tsv`.
   It contains 420 rows from the expanded main-thread tree. WPAExporter with
   the saved `.wpaProfile` produced only 85 process totals with blank Stack
   cells; that route does not preserve this grid expansion. In the copied
   tree, `PlayerRender` has 11,463 ms inclusive weight, including
   `RenderManager::RenderCameras` at 10,344 ms. Under the camera branch,
   `Camera::CustomRender` has 5,440 ms and sibling `Camera::CustomCull` has
   4,654 ms. `CullResults::GetOrCreateSharedRendererScene` has 2,314 ms;
   `ExtractSceneRenderNodeQueue` has 2,006 ms. Separately under the player
   loop, `BaseBehaviourManager::CommonUpdate<BehaviourManager>` has 6,942 ms
   and `DirectorManager::ExecuteStage` has 3,771 ms. These are inclusive
   sampled weights in a copied, partly expanded tree, not exclusive costs or
   additive elapsed times. The specific camera/culling work and game callbacks
   beneath the collapsed branches remain unidentified. `RenderOverlays` is
   478 ms and `RenderOffscreenCameras` is 45 ms in this tree, so this sample
   does not point to broad overlay or offscreen-camera suppression as the
   main gain.
3. **Reopen visual setters only with new attribution.** `HighwayScroll.Update`
   already checks `OnDemandRendering.willCurrentFrameRender`; its mapped row is
   only about 22 ms inclusive in the 31-second WPA method view, and its update
   also advances video/fades. `GuitarFretAnimator.Update` has the same render
   guard, and ClonZones already installs its settle gate. The 26 SpriteMask
   objects in resource prefabs are not a runtime count; the sampled Unity
   SpriteMask job is about 0.04% of WPA sample weight, and `GhostMeterRing.Update`
   returns immediately when current and target scale match. These do not
   support another visual suppression or global setter hook. Revisit only if a
   new trace attributes substantial exclusive cost to one of these paths.
4. **Use the next runtime log to validate configuration, not infer FPS.** The
   broad offline-solo EventSystem pause suppresses the mouse handler beneath
   it, so those switches are not additive in that scope. Existing mod timing
   shows very small average callback cost, counter contents refresh at 5 Hz
   only when visible, and the Canvas sync call at 20 Hz exits on unchanged
   content. The passive overlay skips non-repaint events or returns while
   hidden. A new `Latest.log` can confirm active settings, hook installation,
   state restoration, and errors; it cannot measure native frame cost. Use
   matched PresentMon/WPA frame data for performance conclusions and do not ask
   for the already supplied exports again.

### 2026-10-24 scene and native follow-up

I reassembled the nine uploaded `resources.assets` parts and parsed the exact
uploaded Gameplay `level2` scene. Its two serialized cameras are `Background
Cam` (layer-13 mask, solid-color clear) and `HUDCam` (layers 5 and 12, no
clear). The live snapshot's third camera is created or supplied at runtime.
Among serialized objects with active ancestors, 357 SpriteRenderers are
enabled and visible to `HUDCam`: 322 are under `Input Viewer` and 35 under
`HUD`. None are on the background camera's layer in the static scene. This is
not a live draw count: the previous in-game snapshot found only 48 enabled
guitar-viewer sprites and zero GHL/drum-viewer sprites after instrument setup.
The serialized input-viewer variants are therefore already being deactivated;
removing their prefab renderers would not explain the live culling cost. The
background camera owns the screen clear and the video layer, so disabling it
from the static zero-sprite count would be unsound.

I built Il2CppDumper against the exact GameAssembly/metadata pair to check
older-source leads. The current `HighScoreDisplay` and `GHLFretAnimator`
classes no longer declare an `Update` method; disabling their old empty
callbacks would do no work. `DrumsPlayer` and `DrumsNoteRenderer` still declare
`Update`, but the existing live guitar snapshot has no active drum-viewer
sprites and the method export's prominent game callbacks are guitar-specific.
The existing MIDI switch covers the persistent optional drum-input service.
Do not add a general drum-disable switch until a drum-only component is shown
to keep running in guitar mode.

The method export maps `BaseGuitarPlayer.Update` (~457 ms inclusive),
`BasePlayer.Update` (~414 ms), `BaseGuitarNoteRenderer.Update` (~114 ms), and
`Rewired.InputManager_Base.DoUpdate` (~2,116 ms) in its 31-second view.
Together with the symbolized WPA grid's camera render/cull branches, this
separates necessary guitar input/scoring from Unity rendering. Do not throttle
Rewired or skip the player update to pursue the rendering cost.

### Direct check of the supplied stack ZIP

I parsed the 86 MB stack CSV inside the uploaded ZIP. Its 113,554 Clone Hero
rows extend past the 31.1-second gameplay window into WPA's `Trace Rundown`
tail (the graph continues to 42.28 seconds). About 18.8 CPU-seconds of rows
after 31.1 seconds have `Stack=n/a` and no GameAssembly frames; they are not
useful gameplay attribution. The matching method export ends at 31.092
seconds, so analyses should filter the stack rows to 0–31.1 seconds. In that
gameplay window, rows whose stack traverses
GameAssembly sum to about 9,955 ms across 1,489 distinct full stack strings;
1,365 of those shapes occur on main thread 13808, which accounts for 9,684 ms
(97.3%) of that inclusive total. This is the same broad time-spread path from
the method export, not one 9.955-second function. The weight is distributed
throughout the 31-second gameplay interval at roughly 0.25–0.38 seconds per
second rather than being one isolated spike.

Looking at the last recorded frame in those stacks makes the spread concrete:
5,523 ms end at an unresolved GameAssembly frame, 2,555 ms continue into an
unresolved UnityPlayer frame, and 993 ms continue into MelonLoader's
`IL_STUB_ReversePInvoke`; the remaining weight ends in several smaller native
or input/audio paths. These are mutually exclusive stack-tail buckets, while
the method export's parent/child rows are inclusive and overlap. The exact
GameAssembly disassembly confirms `+0x2FAB159` is after a call to
`+0x2FAB600`, and `+0x2FAB63D` is after that helper's indirect call through a
method-invoker pointer. That shared dynamic dispatch naturally appears with
different stack depths and callback tails. The current export proves the
shape variation is real, but does not identify a single callback responsible
for the broad native weight: its unresolved GameAssembly frames contain no
RVA, and the separate method CSV has no common row key to join.

This rules out a blanket skip of `il2cpp_runtime_invoke` as a safe
optimization. The native dispatcher validates and invokes arbitrary managed
methods; skipping it could suppress unrelated game lifecycle, UI, scoring,
and mod callbacks. The useful target would be one specific caller or callback
whose own cost is high, not the common dispatch bridge.

### Rendering follow-up (2026-10-25)

The exact Gameplay scene and generated current-build `SongBackground` class
narrow the background-render lead. `Background Cam` is the screen-clear
camera and owns layer 13; `HUDCam` renders layers 5 and 12 without clearing.
Both serialized cameras use the Forward path with HDR/MSAA and occlusion
culling disabled, and neither targets a render texture. A third gameplay
camera is supplied at runtime, so the scene file alone cannot attribute the
full `RenderCameras` WPA branch to a specific camera.

On `Background Image`, Unity's `Image` component is disabled. The enabled
`VideoPlayer` has no serialized clip or URL, does not play on awake, and waits
for an explicit prepare/play path. The `SongBackground` class has `Start` and
`OnDestroy` but no per-frame `Update`; its image/video work is driven by
song-selection and playback callbacks. The supplied runtime snapshot also
reported one enabled but non-playing video player. This does not support an
always-on background video tick as a target. Disabling the background camera
itself would remove its screen clear and song-background layer, and would be
visibly wrong.

The same image object has an enabled `AspectRatioFitter`, so I checked its
exact current-build implementation rather than assuming it recalculates every
frame. `AspectRatioFitter.Update` at GameAssembly RVA `0x2AA2850` tests its
`m_DelayedSetDirty` flag and returns immediately when clear; only a pending
layout change calls `UpdateRect`. On a stable hidden image this leaves a tiny
callback/branch, not continuous layout work. Disabling and re-enabling the
component would also invalidate layout, so there is no case for adding another
hook without a trace that attributes meaningful cost to it.

The WPA tree still makes camera rendering/culling the strongest Unity-side
lead: `PlayerRender` is 11,463 ms inclusive, `RenderCameras` 10,344 ms,
`Camera::CustomRender` 5,440 ms, and its `Camera::CustomCull` sibling 4,654
ms. `CullResults::GetOrCreateSharedRendererScene` is 2,314 ms and
`ExtractSceneRenderNodeQueue` 2,006 ms. These are overlapping sampled weights,
not exclusive costs, and the trace does not name the camera or renderers
responsible. Existing scene counts (3 live cameras, 12 canvases, 181–201
enabled SpriteRenderers) cannot identify draw cost; measured GPU busy time is
also substantially below CPU busy time. The 283 ms SpriteMask job remains too
small to justify a new toggle.

The user does not use chart backgrounds, and Clone Hero already exposes
background image/video controls. A Sloptifine duplicate-disable switch would
not currently target a measured recurring cost and could only save decoding
or uploads when a video is actually active. No background or camera override
was added. To reopen that branch, first compare a matched video-background-on
run with an otherwise identical no-video run, or obtain per-camera CPU/GPU
render attribution. Keep the existing camera masks, gameplay notes, and
instrument visuals intact while pursuing renderer-side gains.

### ETW aggregation and raycast pass (2026-10-25)

The stack CSV export groups identical stacks and replaces unresolved
GameAssembly frames with `<PDB not found>` without retaining their RVAs. Those
lost frame addresses cannot be recovered from that CSV, so re-exporting the
same stack view will not make its collapsed GameAssembly paths more specific.
The existing ETWAnalyzer method CSV does retain each sampled GameAssembly
instruction RVA. `tools/symbolize_etw_methods.py` joins those rows to the exact
build's Il2CppDumper `script.json`; it mapped 3,191 of 21,945 rows in the
supplied `Sloptifine-CPU.csv`. The detailed output keeps original timing
columns and adds a method name, signature, and offset into the mapped method.
Its 0x2000 default limit leaves the distant shared-dispatch offsets unresolved
instead of guessing a method. Add `--summary-csv` for a second, flat report
that combines instruction-RVA rows by exact method entry and sorts by `CPU ms`:

```text
python tools/symbolize_etw_methods.py Sloptifine-CPU.csv script.json Sloptifine-CPU-symbolized.csv --summary-csv Sloptifine-CPU-by-method.csv
```

The detailed file is best for tracing individual hot instruction offsets; the
flat report is best for sorting candidate methods. The flat report is still
aggregated sample weight, not raw timestamped samples, and its totals should
not be added to WPA's inclusive caller/callee weights.

The exact raycast chain is now easy to isolate in that output:
`EventSystem.Update` (1,610 ms), `StandaloneInputModule.Process` (1,484 ms),
`ProcessMouseEvent(int)` (1,451 ms), `GetMousePointerEventData(int)` (1,394
ms), `EventSystem.RaycastAll` (1,358 ms), `GraphicRaycaster.Raycast` (1,105
and 914 ms across its two overloads), and
`RectangleContainsScreenPoint` (906 ms). These rows are nested/overlapping
weights from the same older 31.1-second capture, not additive costs or a
current FPS result.

The exact Gameplay scene has four enabled `GraphicRaycaster`s and no
`PhysicsRaycaster`. Three are single-Image canvases named `HUD Drag Handle`;
the fourth is on a canvas with no serialized Graphic component. This narrows
the UI raycast inventory, but the scene data does not reveal the handles'
runtime `raycastTarget` or editing visibility. The existing default-off
mouse-only hook remains the safest complete suppression: it bypasses
`ProcessMouseEvent(int)` before pointer-data allocation and all raycasters,
while preserving keyboard/controller navigation and the separate Rewired
instrument polling. The broad EventSystem option also suppresses those
raycasts, but is not additive and pauses UI navigation too. I removed the
mouse hook's per-call skipped-count increment; the active prefix now only
checks its cached state and returns, so no diagnostic write runs on each
suppressed frame.

There is no useful global "disable aggregation" switch for WPA's sampled-CPU
stack table: it groups matching call stacks by design. To focus it, select a
short time window on the existing graph and then copy the expanded table rows;
this limits the grouped window without pretending the result is raw events.
The current stack CSV has irreversibly lost unresolved GameAssembly frame IPs.
The retained method CSV is the better existing artifact for exact function
attribution; the mapper added no new logging and requires no new capture.

The raycast bypass already exists in v0.4.88 as **Skip mouse UI raycasts
during gameplay**. It patches the measured `ProcessMouseEvent(int)` path before
pointer-data creation and `GraphicRaycaster` calls, while preserving keyboard,
controller navigation, Rewired instrument polling, and controller polling
rate. It is default-off. Do not enable the separate broad EventSystem pause at
the same time when judging the mouse-only effect, since the broad option
already prevents this path from running. I removed the mouse hook's per-call
diagnostic increment; the suppressed path now only reads its cached state and
returns. This trims hook overhead but is not a measured FPS gain. No extra
raycaster or camera was disabled because the scene snapshot cannot prove those
objects are idle or safe to remove.

### Separate legacy camera mouse path (2026-10-25)

The exact-build method export reveals another mouse path that the
`StandaloneInputModule.ProcessMouseEvent(int)` patch does not reach:
`UnityEngine.SendMouseEvents.DoSendMouseEvents` at RVA `0x28C4CD0`, with about
315 CPU ms in the same 31-second profile. Its disassembly calls `UpdateMouse`,
enumerates the camera list, checks camera target/display/rect/event masks,
builds screen-point rays, updates hit records, and dispatches the legacy mouse
events. `Camera.ScreenPointToRay` is another 124 ms method row in this export;
these are sampled weights and must not be added as if they were independent.

I resolved the scene's external MonoScript references through the supplied
`globalgamemanagers.assets`. The serialized Gameplay scene contains no 2D or
3D collider components, and its mapped active scripts have no Clone Hero
`OnMouseDown`/`OnMouseUp`/`OnMouseEnter`/`OnMouseExit`/`OnMouseOver` handlers.
The only collider components in the reassembled resources are a 2D circle and
box on the Pong ball and paddle prefabs. This makes legacy camera mouse-event
dispatch a plausible unnecessary song-time path, but runtime-spawned objects
or external mods could still register callbacks.

This is separate from the EventSystem graphic-raycast switch and does not
touch Rewired's mouse polling or instrument/controller polling. The generated
Il2Cpp UnityEngine assemblies do not expose this internal class, so Harmony
reflection cannot target it. The prototype instead uses MonoMod's native
detour at GameAssembly RVA `0x28C4CD0`, after the existing exact-game hash guard
and an additional 17-byte function-prologue check. Its delegate matches the
IL2CPP signature (`int skipRTCameras`, hidden `MethodInfo*`) and returns
without dispatching callbacks only during active gameplay. The detour is
undone on pause, settings/menu use, focus loss, restart/practice transitions,
scene changes, and unload; disabling the preference also removes it. It is
default-off. I verified repeated Apply/Undo behavior against a local native
probe and confirmed the prologue bytes against the uploaded exact-build
GameAssembly. The 2026-10-25 v0.4.89 log then confirmed that it installed and
was active during a song with the separate raycast option off, and restored
outside active play without a logged error. External mods may rely on
`OnMouse*` callbacks, and the 315 ms sample weight is not a measured frame-rate
gain.

Within the gameplay window, the stack table does name several managed callbacks: SlopHero
`Core.OnUpdate` totals about 101 ms, `ClonZones.Core.OnUpdate` about 55 ms,
and MelonLoader `ComponentSiblingFix` about 183 ms across the 31-second view.
The stack also names ClonZones' `GuitarFretPatch.FretUpdateNativeDetour`
(about 271 ms), `IsFretSettled` (284 ms), and `IsHeadSettledAtIdle` (104 ms).
These are inclusive weights; callback totals overlap their MelonLoader and
IL2CPP trampoline frames and are spread across the trace. SlopHero's named
callbacks are individually small beside the broad native total. ClonZones'
fret hooks and MelonLoader's `ComponentSiblingFix` are separate mod work, not
evidence that the shared dispatcher itself should be disabled. This narrows
the remaining target to a specific native/game callback, which the current
stack export still cannot name.

The method export's largest Unity inclusive weights further explain the
distributed stacks: `Thread::RunThreadWrapper` is about 47.3 seconds,
`lane_allocator` about 34.4 seconds, `PlayerMainWndProc` about 27.7 seconds,
and the `PlayerLoop` / `ExecutePlayerLoop` rows are about 27.2 seconds each.
These totals overlap across nested frames and threads, so they are not extra
elapsed seconds or separate optimization wins. The PDB maps `+0x6B3C1C` to
the wrapper immediately after an indirect callback call; `+0x577AB9` sits in
a queue-state retry loop. That accounts for why the same hot framework rows
show many stack shapes: execution flows through generic worker and callback
machinery into different targets. The current export still cannot connect
those targets to exact stack samples, and neither common wrapper is safe to
skip wholesale. As a useful sanity check, `UnityMain2+0xb` and
`UnityMainImpl+0x1d8d` each carry about 29.1 seconds of inclusive weight,
although `UnityMain2` is a tiny wrapper around a helper and process-exit
imports. That weight follows the work called beneath the wrapper; it does not
mean Unity spent 29 seconds terminating the process. This is why the hot
method totals must be read as call-tree attribution, not exclusive self-time.

The current PresentMon baseline measured 0.760 ms mean CPU busy against
0.281 ms mean GPU busy, with 1,261.2 average presented FPS. This makes CPU
submission/game/Unity work a stronger lead than broad GPU object removal,
but it does not attribute a particular method. The scene snapshot has 3
cameras, 12 canvases, 181–201 active SpriteRenderers, and one enabled but
non-playing VideoPlayer; raw counts are not draw-call or CPU costs. Old source
and scene assets suggest other candidates, but several (`SoftMaskController`,
`BlurScaler`, `BeatPulse`) had no live instances, `LyricDisplay` has an empty
phrase fast path, and `CameraShake` returns quickly while idle. Consult the
audit before adding controls for those.

Continue using the supplied stack ZIP and method CSV; do not ask the user to
repeat those exports. The next profiling improvement is a stack export that
preserves module-relative frame addresses so a stack row can be associated
with a hot RVA. The matching UnityPlayer PDB can name Unity frames when loaded
in WPA; it cannot name GameAssembly frames. Until per-frame RVAs are available,
use named stack tails to rule out individual mod callbacks and continue
screening independent current-game visual paths. One experimental candidate
is TMP's per-component SDF scale-update queue: the existing sample shows about
320 ms in its dispatcher and 279 ms in nested label updates over 31 seconds.
The exact setter can unregister eligible text labels without skipping TMP's
required layout or graphic rebuild queues. Its default-off offline-solo
experiment is in the unbuilt worktree; animated and scale-changing labels are
excluded, and modified flags restore at gameplay-state boundaries. Its
possible gain is small and unmeasured. The News coroutine has no per-frame
Update and the Discord fixed callback is only about 10 ms in that export;
neither supports another gameplay change. Preserve note timing, audio, online
behavior, 1000 Hz input polling, and existing SlopHero features.

## Build and evidence

- Latest packaged build: SlopHero **0.4.109** on the private
  `slophero-v1.1-alpha` branch. The latest uploaded `Latest.log` is from
  0.4.108 (SHA-256
  `cd30bd2c138e790abc48da678e72bc4e896318ac4c47331d8dfe3ed86d943a2a`)
  and is analyzed below. The earlier 0.4.106 log has SHA-256
  `4ac81cbd9bd988c148ed9da5fbe4524811cb3455e48f8bc287bb98f1081af183`.
  The earlier 0.4.102 log has SHA-256
  `dd91a809156a86dab321d7563aef1d07b6f8c9e8f498812394ad0e039cf9e0c6`.
  The earlier 0.4.101 log has SHA-256
  `8361a5c0873b28ce1e18f39cf9e952c5debf2a71d3a72b80ae77b82db7590f36`.
  The earlier 0.4.100 log has SHA-256
  `4516179dc70583a82f5358c028afe03104f8cec06a3cc3b5c0f73aadec32cf75`.
  The earlier 0.4.99 log has SHA-256
  `c679d72bc53c5d6acb6dc679d74ab77d0177ab78b735905edd482080f1ab061e`.
  An earlier 0.4.95 log (SHA-256
  `686ab1bc9132560aaed79a8a2880502d63d91dd938bba68ab83ecd29c7ea11a2`)
  records the previous timing windows. The earlier
  0.4.91 log (SHA-256
  `9a036617279b5d5c175b1ca060868f4f06a7d9409b65d318238d511708a44d5d`)
  confirms the Rewired mouse and SpriteAttach hooks installed without errors.
  The earlier 0.4.89 log (SHA-256
  `6bfd5b29167d86e05e37996606bc6b72df03f96e0c228bba954df27c65b0c596`)
  confirms the legacy mouse detour's install/restore path. The 0.4.87 log (SHA-256
  `f1f2b1b5411260cfcd0e1025a6e114da3a356cecaf147e206d5cd67861517cf0`)
  contains three 30-second captures with 2,636–2,691 simulation updates/s,
  `Core.Update` averaging 0.002 ms, visual overrides 0.015 ms, and Canvas sync
  averaging 0.010–0.019 ms. One Canvas sync hit 5.259 ms in the first capture;
  the following two peak below 0.15 ms. That older log confirms configured
  switches and live scene counts, but does not isolate a switch or measure
  presented frames/GPU work. The 0.4.83 and
  0.4.84 captures below are historical. Clone Hero is **1.1.0.6142**, Unity
  **2022.3.62f2**, with ClonZones 2.0.0 also loaded. Version 0.4.85 separates
  the worker limit from foreground focus, clarifies the MIDI discovery
  switch, and adds a default-off restart-required drum MIDI shutdown using the
  game cleanup path. Version 0.4.86 extends the existing default-off solo UI
  event pause to disable the live EventSystem component during eligible
  offline gameplay and restore it plus menu selection on exit. Version 0.4.87
  avoids scanning Rewired's joystick list for SlopHero hotkeys on every mod
  update; it does not change controller polling or the game's input path. Both
  are candidates without matched PresentMon A/B results. Version 0.4.88 adds
  the default-off offline-solo TMP scale-update experiment described above.
  The 0.4.88 build has zero warnings/errors and core checks pass (101 checks).
  Version 0.4.89 adds the separate default-off native legacy mouse-event
  detour described above and removes the mouse-raycast hook's per-call
  diagnostic counter increment. The .89 build has zero warnings and errors;
  core checks pass (101 checks). The supplied Windows game log confirms the
  native hook installs and restores in its intended scope; neither new mouse
  change has a matched PresentMon A/B result. The .89 package SHA-256 is
  `2e6684d416794205fb6ddc04ccd9972d95a85dfc54967019448844c0aecc0697`, and
  the DLL SHA-256 is
  `e355fab889392b898f8342701c1d5c2385aaf21e49d93fac130c5fa1c41d63a0`.
  Version 0.4.90 broadens the default-off TMP scale experiment to include
  labels under Animator/Animation parents, polling those scales at 20 Hz;
  ordinary labels remain at 4 Hz. A changed scale restores the label's TMP
  scale queue and excludes it for the scene. Discovery uses an unsorted Unity
  query. The experiment builds with zero warnings/errors and core checks pass
  (101 checks). It has not been runtime-tested and has no measured frame-time
  result; scale animation may briefly soften text when the experiment is
  enabled. ZIP SHA-256:
  `bb3ce4111e99cb975637ba2da2d270f64fc0d8ec6c4e26058b97227341a19161`; DLL
  SHA-256: `eb3fe04566269d1f95a34f0202871043976a50463c227d9797e1c2fe91211c81`.
  Version 0.4.91 adds the separate default-off Rewired mouse-snapshot pause
  and exact-build `SpriteAttach.Update` input/output cache described above.
  It builds with zero warnings/errors and core checks pass (114). Both now
  install at runtime, but lack matched frame captures. The mouse
  switch leaves keyboard/controller and 1000 Hz instrument polling untouched;
  mouse-bound actions pause during its offline-solo scope. The SpriteAttach
  cache fails open to vanilla and may have negative net performance because
  it checks Unity inputs every call. ZIP SHA-256:
  `cb8f15d86b0dc01250134780e79029820595fb0d90c580a96bcc8795a09c4951`; DLL
  SHA-256: `b51773181fe2f9a48953b62a51d011c678117f84d5be4378effb1cb4accbc5be`.
  Version 0.4.92 adds the default-off black-background camera option for
  active offline solo guitar Quickplay. It transfers the background color
  clear to the guitar camera, disables the background camera, and restores
  both on pause, settings, focus loss, scene exit, or option disable. The build
  has zero warnings/errors and the 114 core checks pass. Its activation and
  restoration ran in the game without logged errors; the visual behavior and
  FPS impact are unverified. ZIP SHA-256:
  `ef7507568f3f7bc0b4561048e196527d1ad5fede0eed90ab059add81a5eeb52b`; DLL
  SHA-256: `6718841690e1ff4fc5026d07b7bb06a42ce71519ed58d297aff868ec2e3de4b5`.
  The 0.4.88 package SHA-256 is
  `527b5367bceb523a64375ad958360356eb0a72a28353636e57c1078f976c054d`, and
  its DLL SHA-256 is
  `57611b4fc606ad7c01af89ec1bc866b8afe15d7c01743d555e73684713656b33`.
  The 0.4.87 package hashes are retained in Git history.
  Version 0.4.84 ships the
  FC-ring no-op cleanup and extends the default-off hidden leaderboard switch
  to pause its six layout fitters
  while invisible and the spectator counter while offline. It reduces the
  switch's repeated native component-state checks to a one-second recovery
  pass when visibility is stable. The switch restores those components on
  visibility, online transition, option disable, and scene exit. No measured
  frame-rate improvement is claimed for this build.
- The exact UnityPlayer PDB is
  `UnityPlayer_Win64_player_il2cpp_x64.pdb`, GUID
  `6313482A-F658-4F38-906E-79EC0C4DBC51`, age 1. The earlier
  `UnityPlayer_Win64_il2cpp_x64.pdb` upload has a different GUID and must not
  be used. `BuildGuard.cs` records the exact GameAssembly and metadata hashes.
- The older raw PresentMon baseline measured 1,261.2 average presented FPS,
  0.793 ms mean and 1.222 ms p99 frame interval, 0.760 ms mean CPU busy,
  and 0.281 ms mean GPU busy. It is a CPU-side baseline from v0.4.75, not a
  direct A/B result for 0.4.83. Use `tools/presentmon_report.py` for future
  raw CSVs. Do not equate `MsGPUTime` with GPU work; use `MsGPUBusy`.

## Three 30-second v0.4.83 captures

All three report 100% chart speed, render frame interval 2, Canvas counters,
hit/sustain/fret/star particle suppression, paused MIDI polling, UI-event
pause, mouse UI skip, whammy gate, and the same HUD placement and scales.
The log does not record chart
identity, so it cannot prove identical sections.
The worker state follows the activation/restoration messages.

| Run | Active Unity workers | FC / score colors | Scheduled render callbacks/s | Callback p99 | Largest simulation Update gap | Performance-effects max |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| 1 | 15 | on / on | 1,326.4 | ≤0.99 ms | 39.005 ms | 0.626 ms |
| 2 | 14 | on / on | 1,320.8 | ≤0.99 ms | 8.091 ms | 0.610 ms |
| 3 | 15 | off / off | 1,318.9 | ≤0.99 ms | 7.685 ms | 0.577 ms |

The 14-worker run is about 0.4% lower than the first 15-worker run in
callback rate, while p99 is identical. The third run, also with 15 workers,
is lower still. These differences fit normal run variation; the worker
limit has no demonstrated benefit. The 39 ms outlier happened with the worker
limit **off**, correcting the earlier suggestion that it caused the prior
large outlier. Keep the worker switch experimental and default-off.

Turning off FC and score colors removed the score-graphics work:
149–150 calls at about 0.024–0.025 ms each became zero. That saves only
about 3.7 ms across 30 seconds in the timed managed bucket. The shared
`RingColors.Update` scope still reports calls when the colors are off,
because the visual pass times its early return; the color Harmony hooks
themselves are disabled. The callback p99 and rate show no gain from turning
these colors off in this sample. Preserve the feature.

The optional performance-effects bucket stayed at 0.032–0.038 ms average,
with maxima under 0.63 ms in all three runs. It combines neck effects and
star-progress particles. This is consistent with the v0.4.83 reduction of
full neck-hierarchy scans from every 5 seconds to every 30, but it does not
prove causation; the earlier 9.478 ms maximum was a single mixed-bucket
outlier. A new player neck is still discovered on its next apply pass.
Canvas HUD sync averaged 0.009–0.018 ms, with one 4.821 ms maximum in run 1;
the other two maxima were 0.165/0.228 ms. Treat that as an isolated outlier.

The 0.4.83 capture showed 597 `FC rings` calls at a 0.003 ms average in the
run with ring customization disabled. The 0.4.84 cleanup skips these empty
calls while other visual settings are active, and still runs once to restore
ring state after the user turns customization off. This has no measured
frame-rate effect and is one small part of the broader optimization work.

The capture's callback intervals are measured inside the mod. They do not
measure displayed FPS, GPU time, or the full cost of Unity Canvas rendering.
No new PresentMon CSV accompanies the three runs.

## Next code investigations

1. **Exact game visual setters.** The current GameAssembly native map identifies
   `GuitarFretAnimator.Update` at RVA `0xAA890`, which can repeat sorting-order
   writes. ClonZones already installs a fret settle gate in this setup, so
   check its interaction before touching that same method. The native
   `HighwayScroll.Update` path repeats material property-block work and
   also controls video/fades. A narrow unchanged-value fast path may help;
   a blind skip could break visual state. Preserve
   `OnDemandRendering.willCurrentFrameRender` behavior.
2. **Unity UI overlap.** The latest tests have both the broad
   `EventSystem.Update` pause and the narrower mouse-raycast skip enabled.
   During active solo play, the broad pause can prevent the narrower path
   from being called, so do not count their gains as additive. The WPA export
   identified UI raycasting as a real inclusive CPU path, but these three
   captures cannot isolate either switch.
3. **Native engine paths.** The matching PDB names `ExecutePlayerLoop`,
   `PlayerRender`, `RenderManager::RenderCameras`,
   `Camera::CustomCull`, `GfxDeviceWorker::RunCommand`,
   `PlayerMainWndProc`, and job-queue scheduling. These are broad engine
   paths, not safe functions to skip. The WPA stack CSV contains populated
   stacks but unresolved symbols; the method CSV has module offsets without
   a common stack key. Do not request the already-supplied exports again.
   Windows `RtlWalkFrameChain` / `EtwpTraceStackWalk` samples are ETW
   collection overhead.
4. **Scene inventory.** A gameplay snapshot shows 3 cameras, 12 canvases,
   181–201 enabled SpriteRenderers, and one enabled but non-playing VideoPlayer.
   Counts alone do not show cost or uselessness. Inspect a specific component's
   current-build call path before offering a switch, especially where Clone
   Hero already has an equivalent visual option.

Keep each proposed change narrow and reversible. Avoid hot-path logging or
per-frame hierarchy searches. Preserve every existing SlopHero feature,
online behavior, and 1000 Hz controller polling. Do not infer a performance
win until a controlled frame capture supports it.

## v0.4.91 prototype result and next rendering lead

The 2026-10-25 log has three 30-second mod timing windows. The first two
install both the Rewired mouse sampler and SpriteAttach cache; the third
installs the mouse sampler but does not install SpriteAttach. The native legacy
mouse hook is present in all three once gameplay starts. Scheduled render
callback rates are 1,265, 1,252, and 1,312/s, respectively; callback p99s
are 1.17, 1.17, and 1.06 ms. There are no hook failures. The cache-off run is
faster on this internal measure, but a song restart, chart position, and normal
run variation prevent causal attribution. **These callback rates are not FPS.**
The only available raw PresentMon CSV remains the earlier v0.4.75 capture:
1,261 presents/s, 1.222 ms p99 frame interval, 0.760 ms mean CPU busy, and
0.281 ms mean GPU busy. It cannot be matched to the new log's switch states.

The WPA method rows give only about 275 ms self CPU in 31.1 seconds for
`SpriteAttach.Update` and 317 ms for Rewired's mouse snapshot. At roughly
1,200 rendered frames/s, perfect elimination would save only about 0.007 and
0.008 ms per frame before Harmony and interop overhead. This sharply limits
their likely impact. Keep both opt-in; prioritize larger current-game or Unity
render paths rather than treating these prototypes as major wins.

The exact `level2` Gameplay scene has two static cameras. `Background Cam`
(depth -5, solid-color clear, culling mask 8192) sees layer 13, whose scene
objects are `Background Cam`, `Background Image`, and `Background Canvas`.
The image has an enabled `VideoPlayer`; the latest gameplay snapshots report
one enabled but **non-playing** VideoPlayer. `HUDCam` (depth 6, clear flags 4,
culling mask 4128) handles separate HUD layers; the live scene adds a guitar
camera. The rebuilt current `resources.assets` contains three `PlayerCamera`
prefab variants: all use depth 0 and mask 512, two use clear flags 3 and one
uses clear flags 4. All three have full-screen rects and target display 0.
The static background camera's clear flags are 2, full-screen, target display
0, with transparent black background color. Thus the guitar/HUD cameras do
**not** replace its color clear; merely disabling `Background Cam` could leave
the previous frame on screen. The default-off option in
`BackgroundCameraOverride.cs` handles this only during active offline solo
guitar Quickplay. It validates camera masks, clear flags, order, rect, display,
and render-texture targets; transfers the background clear to the guitar
camera; and disables `Background Cam`. It restores both cameras on pause,
settings, scene exit, focus loss, or option disable. These changes occur on
state transitions, with no per-frame hierarchy search or camera setter call.
The option is on Sloptifine and participates in settings profiles and
timing-setting snapshots. Runtime visuals and frame benefit remain untested.
The WPA tree attributes substantial *inclusive* time to
`Camera::CustomRender` and `Camera::CustomCull` across cameras, without
per-camera cost. The user authorized the default-off black-background option;
do not claim its cost or benefit from the WPA sum. Compile verification passed
with zero warnings/errors against the supplied generated assemblies. The
v0.4.92 package exists. The Windows log confirms activation and restoration,
but visual behavior and measured frame benefit remain unverified.

## v0.4.92 Windows runtime log (2026-10-25)

The new `Latest.log` SHA-256 is
`da127fa224b26ada8a126568654dee3c0c094c50fd7828fa78a7ce612893dcd1`.
It reports SlopHero 0.4.92 and matches the supplied v1.1.0.6142 game files.
`BackgroundCameraOverride` activated repeatedly and logged restoration on
pause/transition/restart, with no camera-experiment failure or cleanup error.
This verifies that the live camera lookup and guarded mutation path ran; the
log alone cannot confirm what the player saw on screen.

Four 30-second SlopHero timing windows report 1,364–1,473 scheduled render
callbacks/s and p99 callback intervals of 0.96–1.12 ms. Three windows contain
maximum callback gaps around 59–111 ms. These are possible hitch clues, not
PresentMon frame-present measurements. Mouse/UI, worker, HUD, visual, and
other performance settings overlap in this session, and there is no matching
PresentMon capture, so neither the camera switch's frame effect nor the
cause of the large gaps can be isolated. No FPS gain is claimed from this log.
All four windows also had `gameplayUiEvents=True`; the live EventSystem was
suspended during each song. That means the mouse-only `ProcessMouseEvent`
switch could not add work avoidance in those windows, even in the captures
where its own preference was enabled. The legacy camera mouse-event option
was enabled in two windows and disabled in two, but the mod callback timings
are not a controlled measure of its effect on presented frames.

## v0.4.93 SpriteAttach cache correction

The optional `SpriteAttach.Update` cache previously ran its full `Capture`
postfix even when its prefix had skipped the game's update. On a cache hit,
that repeated the camera, sprite, matrix, and target-transform reads that the
cache was intended to avoid. The prefix now passes a Harmony `__state` flag to
the postfix; only an update that this cache allowed recaptures state. A later change
to any input or cached output still causes the original method to run. While
the setting remains on, pause and menu transitions now make the hook pass
through instead of unpatching and repatching it; scene exit and turning the
setting off still remove it. This avoids patch setup on resume but leaves a
pass-through prefix/postfix while the opted-in scene is paused.

The Rewired mouse, SpriteAttach, and background-camera experiments also now
share one `OfflineSoloSongScope` evaluation per 20 Hz state refresh. When all
three are off, the IL2CPP fields for that gate are not read. These are source
level reductions, with no Windows runtime result or measured FPS gain yet.
The v0.4.93 build has zero warnings/errors and the 114 core checks pass.
ZIP SHA-256: `3eb6cc993a0f292b672a0aff75991071ba409f631cd83de5cbf9c7b1c90d88f5`;
DLL SHA-256: `cf8e22fdc216419d41cfdee92e6e659dc6af79cf1c2db30010ba14cf5405ef99`.

## 2026-10-25 game and renderer follow-up

I screened the exact-build ETW method map, the symbolized WPA tree, the
Gameplay scene, and the reassembled resource prefabs together:

- Rewired's `InputManager_Base.DoUpdate` and the large obfuscated controller
  manager/device update methods are core polling paths. Their sampled weights
  overlap. The existing mouse-only option is the narrow input experiment;
  these methods do not justify reducing the 1000 Hz instrument rate.
- The WPA tree still places the largest Unity work in camera render/cull and
  scene extraction (`RenderCameras` 10,344 ms inclusive over 42.3 seconds).
  `RenderManager::UpdateAllRenderers` is about 780 ms and
  `SortingGroupManager::Update` about 240 ms in that same tree. The static
  Gameplay scene has **zero** SortingGroup components. The resource prefabs
  have 17: note variants, combo indicators, arrows, volume overlays, kick
  flames, a disabled fret variant, and a Pong paddle. These are serialized
  prefab counts, not live costs. Removing note/combo sorting groups without
  proving they are idle could change draw order.
- The Gameplay scene serializes 86 `TextMeshProUGUI` components. Thirty-three
  have enabled components and active ancestors in the static hierarchy; 24
  are children of `HUDCam/Leaderboard/Canvas/ScoreList`. The latest live
  snapshot reports that list active with CanvasGroup alpha zero. The existing
  default-off hidden-leaderboard option paused entries and layout fitters but
  left these text components enabled. It now records each label's original
  state and pauses the list's TMP labels only while fully hidden offline,
  restoring them when the list appears or online play starts. The older trace
  has 320 ms inclusive in `TMP_UpdateManager.DoRebuilds` and 279 ms nested in
  `TextMeshProUGUI.InternalUpdate`; it cannot assign that work to the 24 labels
  or predict this option's FPS effect.
- Current `BaseGuitarNoteRenderer.Update` is already gated to rendered frames;
  `InputViewer.Update`, `HighwayScroll.Update`, and `GuitarFretAnimator.Update`
  have small mapped method rows. Those paths do not support another blanket
  song-time skip.

The v0.4.94 build compiles with zero warnings/errors and the 114 core checks
pass. The archive contains `Mods/SlopHero.dll`. The new text pause has not been
verified in a Windows game session. ZIP SHA-256:
`db19c90b7603f33c4203a2d52458d38ce087bbe0bb6e0b0790b951c6ce22d0f6`;
DLL SHA-256: `1c6565c9114b28dec2bfeb837c9cbbfb72f8daee2362ba40b0752b7047d3862b`.

## WPA hot-callback attribution follow-up

I matched the existing 2026-10-23 `Sloptifine-CPU.csv` method RVAs against the
supplied `script.json` generated from the exact game binary. The method export
identifies a substantial Unity mouse-UI callback family; it does not by itself
tie those samples to the generic-dispatch PCs in WPA's stack tree:

- `GameAssembly+0x2AD228D` falls inside `EventSystem.Update` (about 1,610 ms
  inclusive CPU weight in the 31.1-second trace).
- Nearby nested rows map to `StandaloneInputModule.Process` (~1,484 ms),
  `ProcessMouseEvent` (~1,451 ms), `PointerInputModule.GetMousePointerEventData`
  (~1,394 ms), `EventSystem.RaycastAll` (~1,358 ms),
  `GraphicRaycaster.Raycast` (~1,105 ms), and
  `RectTransformUtility.RectangleContainsScreenPoint` (~906 ms).

These rows overlap along the same call chain and must not be summed. The
method-level export does not preserve a stack key linking these methods to the
two generic-dispatch PCs, so it does not establish how much of those PCs'
weight comes from this path. It does independently identify Unity mouse UI
raycasting as a substantial callback family. The existing default-off `Pause
UI events during solo songs` option removes this path when it disables the
live EventSystem; the mouse-only raycast switch cannot add a separate saving
in that state.

The same method map places a hot `GameAssembly+0x1EC1AAE` site inside
`Rewired.InputManager_Base.DoUpdate` (~2,116 ms). Rewired's `ReInput.DoUpdate`
and controller update loop also appear among the hot rows. This is required
device polling work, so it does not justify suppressing the controller or
changing the 1000 Hz polling setting. The mouse-only sampler remains a narrow
optional reduction; it cannot remove most of this input-manager cost. These
are old WPA inclusive weights, not current-run exclusive method timings or
presented-frame costs.

## v0.4.94 log and exact-scene research (2026-10-25)

The new `Latest.log` has SHA-256
`ffe44b7d319bcfd985c42b073cd21d5e50888358b946622963389b118c54cf71`.
It identifies SlopHero 0.4.94 and the matched Clone Hero build. Two 30-second
timing windows have 1,451 and 1,466 scheduled render callbacks/s, but those
are not presented-frame FPS. The largest observed simulation gaps are 32.2 and
17.6 ms. Core Update averages 0.002 ms, visual overrides 0.014/0.013 ms,
Canvas HUD sync 0.011/0.010 ms, and HUD repaint 0.068/0.066 ms at their
respective call rates. The log has no SlopHero exception or hook failure. It
does not report how many hidden leaderboard labels were disabled, so it cannot
validate the new v0.4.94 TMP action or establish a frame gain. The broad
EventSystem pause, black-background camera mode, mouse options, worker limit,
and hidden-leaderboard option were all enabled together; the windows cannot
isolate any one of them.

I parsed all 404 MonoBehaviours in the exact uploaded Gameplay `level2` scene,
including script references whose UnityPy typetree is incomplete, by reading
only their common serialized header and joining the script path IDs to the
reuploaded `globalgamemanagers.assets`. I then joined the class names to the
exact GameAssembly dump's update declarations. These are **serialized active**
objects, not proof they stayed enabled at runtime:

| Scene path / component | Exact scene | Existing trace / consequence |
| --- | ---: | --- |
| `HUDCam` drag-handle roots / `HudDragController.Update` | 3 enabled | Only ~8 ms sampled CPU in 31 s. The old source has a constant HUD-position setter, but current v1.1 native `Update` first calls `Input.get_mousePosition` and compares it with cached coordinates before its longer branch; the old behavior cannot be presumed. |
| `HUDCam` drag-handle canvases / `GraphicRaycaster` | 3 enabled single-Image canvases | Part of four scene raycasters. The broad EventSystem pause already prevents their ordinary mouse raycasts during eligible songs; disabling raycasters in that same scope cannot yield an additive raycast saving. Their canvas/render cost is not separately measured. |
| `HUDCam/Leaderboard/Canvas` / `GraphicRaycaster` | 1 enabled | Its score list is alpha zero in the runtime log. v0.4.94 pauses entry, fitter, and TMP components while hidden offline; v0.4.95 pauses child Images; v0.4.96 adds a guarded pause for the dedicated Canvas, scaler, and raycaster. The canvas cost and benefit remain unmeasured. |
| `Background Cam/Background Canvas/Background Image` | enabled Canvas, `AspectRatioFitter`, `SongBackground`, and `VideoPlayer`; disabled `Image` | Runtime reports one enabled but non-playing video. The Canvas is Screen Space - Camera mode targeting `Background Cam`, so it does not render through the disabled camera. `AspectRatioFitter.Update` is only ~4 ms in the old 31 s method export, so further suppression is not a large likely saving. |
| `HUDCam/Menu Canvas/Lyrics` / `LyricDisplay.Update` | 1 enabled | About 4 ms in the earlier method view. Lyric visibility and chart support must be preserved. |
| `HUDCam/Input Viewer/Drums Input Viewer` / `DrumsInputViewer.Update` | 1 enabled in serialized hierarchy | About 1 ms mapped in the earlier method view. Runtime has zero enabled drum-viewer sprites; a broad drums component kill is not supported by this cost. |
| `HUDCam/HUD/Score/PB Notification` / `Animator` | 1 enabled | No isolated Animator cost in the supplied trace. Do not infer cost from the static scene count. |

Other active serialized update scripts include `PracticeUI`, `CanvasFader`,
`SongProgress`, `PracticeSectionDisplay`, `ScoreManager_Versus`, `ScoreManager`,
and `GameManager`. None has a substantial isolated row in the supplied method
export, and practice/score/game state must stay correct. This inventory rules
out several superficially plausible blanket skips. The higher-value unresolved
path remains Unity's camera render/cull and canvas rebuild work. Existing WPA
samples do not identify which of the three live cameras or which Canvas caused
that inclusive cost, so a new global skip would be speculative.

### v0.4.95 hidden score-list graphics

The same exact-scene inventory resolves 30 enabled `UnityEngine.UI.Image`
components under `HUDCam/Leaderboard/Canvas/ScoreList`, 14 of which have
active ancestors in the serialized scene, in addition to the 24 TMP labels
paused in v0.4.94. The existing default-off hidden-leaderboard
switch now records each image's original enabled state, disables images only
when the list is active but its `CanvasGroup.alpha <= .001` in offline play,
and restores them on reveal, online play, option disable, or scene cleanup.
It does not deactivate the score-list GameObject or touch its network/data
manager, so the game's reveal path can still run. The old WPA export attributes
about 731 ms inclusive in 42 seconds to Unity's `UIEventsWillRenderCanvases`
branch, but does not split that by score list or prove an Image-specific gain.
This is a source-level reduction and has no Windows runtime or FPS result yet.
The v0.4.95 build succeeded against the supplied references with zero
warnings/errors, and all 114 core checks passed. The release ZIP contains
`Mods/SlopHero.dll`. ZIP SHA-256:
`406e6f590fb71ea1e83fc28d0077d1a5b760492c4f0a23f6f8ef4dadada41466`;
DLL SHA-256:
`a2f79fc4646de6634cad85967f31f039855c705280da3706f74cbc5b25a332b2`.

### v0.4.95 Windows log

The uploaded `Latest.log` SHA-256 is
`686ab1bc9132560aaed79a8a2880502d63d91dd938bba68ab83ecd29c7ea11a2`.
It loads 0.4.95 against the matching game build. In both captured solo songs,
the hidden-leaderboard preference is on and the score list reports active with
alpha zero and zero eligible entry updates. There is no SlopHero exception or
hidden-leaderboard failure, but the log does not expose individual `Image`
enabled states or measure the canvas savings. Most other performance switches
are also on, so these are not isolated before/after runs.

The two 30-second windows have scheduled-render callback p99 intervals of
0.99 and 1.00 ms. Their maxima are 126.4 and 59.5 ms, mirrored by simulation
delta maxima of 118.5 and 58.2 ms and GUI-repaint maxima of 124.8 and 59.5 ms.
`Core.OnUpdate` wall-clock maxima are 116.1 and 56.9 ms, versus 0.003 ms
average. The image pause runs in `OnLateUpdate` at a 20 Hz gate, outside the
`Core.OnUpdate` timing bucket, so the log does not attribute those isolated
Update maxima to v0.4.95. `RuntimeTimings` uses `Stopwatch`, which includes
time if the thread is descheduled during a callback. Do not infer that 116 ms
of mod code executed or that this was a presented-frame hitch. The TMP scale
experiment still modifies zero labels and incurred a 5.786 ms maximum in the
first window, plausibly its one-time scene scan. That is a separate small
startup lead, not an explanation for the 116 ms maximum.

### Exact camera and canvas follow-up

The Gameplay `HUDCam` and `Background Cam` are **orthographic** (size 5);
all three `PlayerCamera` variants in the reassembled exact `resources.assets`
are **perspective**. `HUDCam` renders layers 5 and 12, while the player camera
renders layer 9. A single-camera merge would change HUD projection and draw
ordering, so it is not a safe culling optimization. The background-camera
experiment is different: it hides only the blank background pass and transfers
the color clear. Also, `Background Cam/Background Canvas` is
Screen Space - Camera mode targeting `Background Cam`, **not** a screen-space
overlay as an earlier note suggested; that Canvas cannot continue rendering
through its disabled camera. The enabled but non-playing VideoPlayer and its
AspectRatioFitter have only small mapped method rows, so extra background
Canvas suppression is not a promising next gain.

The nine serialized Gameplay Canvases include one dedicated world-space
`HUDCam/Leaderboard/Canvas` with `ScoreList` as its sole direct child. Under
it are 80 GameObjects, 24 active-ancestor TMP labels, and 14 active-ancestor
Images (30 Images are enabled in the asset). Its parent Canvas, CanvasScaler,
and GraphicRaycaster remain enabled under the alpha-zero score list. By
contrast, `HUDCam/Menu Canvas` serializes 61 enabled TMP labels and 56 Images,
but only 8 TMP labels and 5 Images have active ancestors; the game already
deactivates most menus. The live log reports 48 enabled guitar input-viewer
sprites and zero GHL/drum viewer sprites, so blindly hiding those roots would
duplicate game behavior or remove visible input feedback.

Version 0.4.96 extends the existing default-off hidden-leaderboard switch to pause
its dedicated Canvas, CanvasScaler, and GraphicRaycaster alongside the hidden
children. Discovery requires the exact one-child, world-space Canvas shape;
otherwise it leaves the parent Canvas untouched. The GameObject, manager,
fade state, and online requests remain active, and prior component states
restore on reveal, online play, setting disable, or scene exit. This targets
the remaining invisible Canvas traversal without inventing an FPS result.
The optional timing snapshot now reports `canvasSuspended` once per capture,
so the next log can confirm this guard engaged without per-frame logging.
The build has zero warnings/errors against the supplied references, all 114
core checks pass, and the release ZIP contains `Mods/SlopHero.dll`. Windows
runtime behavior and presented-frame impact remain untested. ZIP SHA-256:
`0ddd7cd7e17f3d940afc672c90aa5030accb1cf3a604b9d31b84b3e9144fed90`;
DLL SHA-256:
`f9ce7e748e55c0e6cbf4755d5bb58bc135db24fad0d7a14bf004e47f93eea383`.

## v0.4.89 legacy mouse-event test

The 2026-10-25 `Latest.log` SHA-256
`6bfd5b29167d86e05e37996606bc6b72df03f96e0c228bba954df27c65b0c596` has the
legacy camera mouse-event option enabled and the mouse-UI raycast option off.
The native detour installed during active play and reported no install or
restore errors. Two 30-second SlopHero timing windows report 2,732–2,742
simulation updates/s and 1,366–1,371 scheduled render callbacks/s, with
callback interval p50/p95 near 0.39/0.52 ms and 0.72/0.86 ms. The earlier
raycast-only log was about 0.40/0.54 ms and 0.73/0.88 ms respectively. This
small difference is not a measured FPS gain; these logs are not a controlled
PresentMon comparison. The same run reports zero labels changed by the TMP
scale experiment, making its current filter worth revisiting before further
experiments. The first run's TMP experiment bucket peaked at 5.104 ms, while
the second peaked at 0.164 ms. The first-scan enumeration is a plausible cause,
not a proven attribution. The worktree now uses `FindObjectsByType` with no
result sorting for that one-time scan; this narrow cleanup has no measured
frame-time effect yet. It also now checks labels under animation parents at
20 Hz instead of rejecting them categorically; ordinary labels remain on the
4 Hz interval. On any observed scale change, TMP updates are restored for that
label for the rest of the scene. This broader default-off prototype may briefly
soften text during scale animation and has not been tested in-game.

## v0.4.96 SpriteAttach comparison and v0.4.97 hidden HUD handles

The latest uploaded 0.4.96 log (SHA-256
`a7d1d54480fa2c5b36ec4d4eef81d778ee8bf442f87d21e39e91035ea2819`)
has two 30-second timing windows. The first has the SpriteAttach cache off:
1,477 scheduled render callbacks/s, p99 at most 1.01 ms, with 181 enabled
SpriteRenderers. The second installs the cache: 1,418 callbacks/s, p99 at most
1.06 ms, with 190 enabled SpriteRenderers. The single largest callback gaps
are 71.5 and 114.2 ms. These are CPU callback intervals, not presented-frame
FPS, and the chart/renderer populations differ. There is no SlopHero hook
error. The hidden leaderboard option reports `canvasSuspended=True` in both
windows, confirming its exact-scene guard engages and the parent Canvas is
paused while the score list is alpha zero.

The exact GameAssembly disassembly resolves why the SpriteAttach option can
lose FPS. `SpriteAttach.Update` at RVA `0x1B8E20` calls
`OnDemandRendering.get_willCurrentFrameRender` at RVA `0x2895320` and exits
before its expensive work on non-render ticks. The mod's Harmony prefix still
runs on every tick. On rendered ticks, `State.Matches` reads camera matrices,
two screen dimensions, two rects, sprite bounds, positions, anchors, an
IL2CPP array and each target's scale through many managed/native property
calls. On misses, the native method then runs and the postfix recaptures the
same inputs. The old WPA method view sampled only 275 ms in this native method
over 31 seconds, roughly 0.007 ms per rendered frame at 1,200 renders/s even
with perfect elimination. The cache remains default-off and is labeled as
potentially slower; the supplied data does not justify a more invasive
SpriteAttach patch or a claim that the cache improves FPS.

I also inspected all three built-in HUD drag-handle Canvases in the exact
Gameplay `level2` asset. Each is enabled, world-space, and contains a single
enabled `Image` whose serialized color is `(1, 1, 1, 0)`. Their parent roots
are `HUD Drag Handle Root`, `Input Viewer Drag Handle Root`, and
`Leaderboard HUD Drag Handle Root` under `HUDCam`. These are distinct from
the leaderboard Canvas and from the visible input-viewer sprites. Version
0.4.97 extends the existing default-off **Pause UI events** switch: during
its eligible offline solo scope, it discovers only that exact three-root,
one-Graphic shape and pauses a Canvas only while its Image is still invisible.
It rechecks at the existing 20 Hz state cadence, restores a Canvas if its
Image becomes visible, and restores all immediately when the UI pause ends,
F8 opens the settings, or the scene unloads. No controller polling, guitar
renderers, or game score objects are altered. The exact asset proves the
initial target shape; runtime behavior and presented-frame effect still need
Windows validation. This is an independent render/culling path alongside the
existing leaderboard Canvas work, though its likely gain is modest compared
with Unity's whole camera-render branch.
The 0.4.97 build has zero warnings/errors against the supplied references,
all 114 core checks pass, and its ZIP contains `Mods/SlopHero.dll`. ZIP
SHA-256: `fa8b9fd140113161058c694cf6352c2335b02110c7262ef1e37e0fd91fca1bd2`;
DLL SHA-256: `7e6c73f7dc1c6dae7cb3e9d971ead3b4cd9955aaa30c42e539494f8dd633f244`.

## v0.4.97 runtime log and v0.4.98 follow-up

The 0.4.97 `Latest.log` (SHA-256 above) shows two short offline-solo states
that reconfigure the optional Unity worker pool for only 120 and 150 ms,
then restore it during song startup. Version 0.4.98 waits for 250 ms of
continuous eligibility before reducing the worker count, but still restores
immediately on pause, online play, or scene exit. The same log shows the TMP
scale experiment repeatedly activating with **zero** eligible Gameplay
labels. The mod now stops dispatching it after that scene's one-time scan
returns no labels. These are avoided work and state changes; the log does
not measure their effect on presented frames.

The first timing window ended after 8.6 seconds of song transitions; its
1,295 scheduled render callbacks/s and 4,504 HUD repaint calls are not a
stable FPS comparison. The second 30-second window reports 1,507 scheduled
render callbacks/s, p99 callback spacing at most 1.00 ms, and 398 HUD
repaints. Both windows are CPU callback observations, **not presents**.
Because the normal-play counter uses a retained Canvas, SlopHero now returns
from empty `OnGUI` events before reading Unity's GUI event when that Canvas
is visible and no menu, editor, song HUD, notice, or timing capture needs
IMGUI. No guitar input or game GUI path is skipped.

I also rechecked the exact current-build `BaseGuitarNoteRenderer.Update`:
it tests `OnDemandRendering.get_willCurrentFrameRender` (RVA `0x2895320`)
before doing rendered-note work. A second mod-side render gate would repeat
work rather than remove the necessary note path. The game's `FrameRate.Update`
(RVA `0x37F3D0`) counts simulation ticks and Unity-scheduled render ticks;
neither count proves a frame reached the display. The existing
`tools/presentmon_report.py` uses raw PresentMon `MsBetweenPresents` data for
actual presented rate and frame intervals. A mod-side "presented FPS" label
based on Unity callbacks would be misleading. The native render/cull
branch remains the biggest attributed Unity cost, but this trace still does
not isolate a removable camera or renderer inside it.

The 0.4.98 build succeeds against the supplied references with zero warnings
or errors; all 114 core checks pass. Its ZIP contains only `Mods/SlopHero.dll`.
ZIP SHA-256:
`229bba8ed9fee6f0a82e909afd09a9f9a81471407fcd1dd17ffa9c7bb34a8c19`;
DLL SHA-256:
`f432d0f1fb0331b6a421244a4b610fa73e6cf5f9985d999f08f39fe3555ffbe6`.

## v0.4.98 runtime log and v0.4.99 follow-up

The new `Latest.log` (SHA-256 above) loads 0.4.98 with no SlopHero hook
exception. The optional worker-count experiment activates about 0.4–0.5 s
after the song begins, rather than changing the pool during the brief
120–150 ms startup states seen in 0.4.97. TMP scale discovery runs only
three times in the first 30-second timing capture, then zero times in the
second. Its first scan takes 24.630 ms and finds no eligible label, so it
offers no steady-state benefit on this user's Gameplay scene. Version 0.4.99
prepares that candidate list at Gameplay scene initialization if the option
is already enabled, before the song loop; enabling the option mid-song retains
the prior first-use scan. The setting and per-label restoration remain intact.

The first capture's largest simulation Update is 58.539 ms; the second's is
17.503 ms. The 24.630 ms TMP scan is in LateUpdate, so the log does **not**
prove it caused the 58.539 ms Update gap. Several optional hooks activate in
the opening fraction of a second, including the native legacy-mouse detour
and the Rewired mouse prefix, but the timing buckets do not attribute that
Update gap to one of them. The 1,558 and 1,540 scheduled render callbacks/s
are not presented FPS, and the two captures do not establish a causal gain.
The second song reports SpriteAttach cache installation during transition;
there is no matched raw PresentMon comparison of that experiment.

The older WPA map still makes Unity camera render/cull the largest attributed
render branch, while the exact current guitar-note renderer already checks
Unity's render decision. Do not skip scoring, input, or the whole note loop.
The optional legacy camera mouse and mouse-UI skips were not explicitly
conditioned on offline solo in `Core`; 0.4.99 applies that gate, matching the
other UI optimizations and restoring vanilla behavior online. This corrects
scope; it does not predict an offline FPS gain.

Actual presented-frame analysis remains external: `tools/presentmon_report.py
--fps-windows capture.csv` now prints counts of actual presents in each
complete one-second interval, alongside frame-time percentiles and CPU/GPU
busy. The supplied older raw capture gives 37,717 presents over 29.900 s,
about 1,261.4 presents/s overall, with one-second windows that vary. It is
from v0.4.75 and cannot be used as an A/B result for 0.4.99. A mod-side
Unity callback counter would not measure DXGI presentation.

Next investigation order: (1) attribute the first-song 58 ms Update gap with
existing timing scopes or a matched native trace before changing hook timing;
(2) isolate actual render/cull work by live camera and renderer, without
altering note visibility or user-enabled HUD; (3) revisit the current whammy
raw-byte/write and texture-upload path only if a trace shows redundant work
beyond the existing upload gate. The previous method exports contain large
inclusive Rewired and IL2CPP dispatcher rows; those are shared input/callback
paths and cannot be safely throttled from their aggregate weights.

Version 0.4.99 builds against the supplied references with zero warnings and
errors, passes all 114 core checks, and packages only `Mods/SlopHero.dll`.
The PresentMon report prints 29 complete one-second FPS windows from the
older 29.9-second raw capture. ZIP SHA-256:
`27921e125c1ff3a3a28b64084e83b9877273b7cec595c99810fd8d91c5045059`;
DLL SHA-256:
`f63bca6dce0a968dc510f5c9842e2a964d5c1f7cfc814001fd30bd1c965c5f1f`.

## v0.4.98 versus v0.4.99 presented-frame comparison

The user supplied one raw 29.9-second Intel PresentMon capture and its small
`-stats` summary for each mod version. The raw 0.4.98 CSV SHA-256 is
`462b8d26ee84e1e48c2e84238a25de2882eff0a7523e2da7e6ffa2c43ff3dbfe`;
the raw 0.4.99 CSV is
`220cf9e21b73def27450a786024001023af316bf3eeb27860274be4dbbd1b29c`.
The two ~219-byte files are PresentMon summaries, not second runs. Both raw
captures contain only Clone Hero, one swapchain, DXGI independent-flip
application frames, and zero reported PSO compilations. Average CPU frequency
is effectively equal (~4,402 MHz) in both.

| Raw capture | Presents/s | Present interval p99 | CPU busy mean | GPU busy mean | Frames >3 ms | Longest present interval |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 0.4.98 | 1,574.7 | 0.828 ms | 0.605 ms | 0.259 ms | 8 | 8.253 ms |
| 0.4.99 | 1,570.2 | 0.828 ms | 0.608 ms | 0.260 ms | 2 | 3.951 ms |

Per-second present-rate patterns align best with about a two-second start
offset (correlation 0.94 across the overlapping 27 one-second bins). If the
0.4.98 interval `[2,29)` seconds is compared to the 0.4.99 interval `[0,27)`,
the rates are 1,574.0 versus 1,581.5 presents/s and present p99 values are
0.8279 versus 0.8274 ms. This shift reverses the tiny average-FPS ordering,
showing that the full-window 0.3% difference is not a robust mod effect.
The fewer >3 ms frames in 0.4.99 are a favorable observation, but eight
versus two rare events across single runs cannot establish a repeatable
stutter improvement. The 8.253 ms 0.4.98 outlier is CPU-heavy (8.226 ms
CPU busy, 0.297 ms GPU busy), not a reported shader compile.

The 0.4.99 log confirms the TMP experiment ran zero times in both 30-second
timing windows, consistent with scene preparation and its no-target latch.
It shows no SlopHero hook error. Its first `Core.OnUpdate` maximum is 68.820
ms, but PresentMon's 0.4.99 capture has a 3.951 ms maximum; the log and raw
CSV do not share an absolute capture timestamp, so these outliers cannot be
correlated or called the same displayed-frame stall. The log's two Gameplay
snapshots differ in active SpriteRenderers (181 and 199); they were taken
around different scene phases, another reason not to infer exact chart-state
matching from mod callback counts.

This comparison validates the intended no-work TMP gameplay path. It shows
no sustained presented-FPS improvement or regression attributable to 0.4.99.
The underlying CPU-heavy render/cull and first-song outlier investigations
remain open. Do not redesign the native mouse hooks or Unity worker pool
based on these two rare-spike counts alone.

## v0.4.100 exact-scene visual experiments

The supplied Gameplay `level2` and PlayerCamera prefab show that the HUD,
highway, and background cameras use separate layer masks. The existing
black-background option already disables the background camera during
eligible songs, so these assets do not support a second camera-cull patch.
The apparent Song Details canvas is also already deactivated by the game's
`CanvasFader` after fading. Neither is a new optimization target.

The exact `Updated SP Animation` guitar prefab contains two `SpSideGlows`
Animators, eight `AnimatorColorBehavior` scripts, and 13 sprite renderers
under one `StarPowerController`. The `PB Notification` Gameplay object has a
Canvas and Animator under `HighScoreDisplay`. Version 0.4.100 adds two
independent, default-off Sloptifine 3 switches. They pause only these visual
components during active offline solo Quickplay and restore their captured
enabled states on pause, online transition, focus loss, or scene exit.
The Star Power controller and high-score display script remain enabled.
Discovery requires exact component counts, so a game build with a different
scene shape fails open and leaves visuals active. A 20 Hz reconciliation
pass handles scripts that re-enable those components.

The symbolized WPA capture attributes substantial inclusive time to Unity's
`CustomCull` and camera-render paths, but does not attribute it to either of
these visual groups. `AnimatorColorBehavior.Update` was only a small sampled
path in the older method export. There is no matched presented-FPS test for
0.4.100 yet; gains may be negligible. Continue investigating scene-conditional
CPU work and the main-thread render/cull path rather than claiming all
sampled render time is available to save.
The 0.4.100 mod builds against the supplied IL2CPP references with zero
warnings; the Core checks pass (114/114). Runtime behavior and presented-FPS
impact still require the user's Windows test.

## v0.4.100 log and v0.4.101 correction

The uploaded 0.4.100 log matches the packaged DLL hash
`ec7242a23a1d82598b7d0ff19cc8f70413c3834a447d2f82d588bb170f123dfb`.
Two 30-second song windows report 1,545 and 1,548 scheduled render callbacks/s.
These callback rates are not presented FPS. The largest update gaps were 122.6
and 55.4 ms; there is no timestamp alignment or stack evidence tying them to
the new switches. SlopHero shows no exception. The user confirmed that both
Sloptifine 3 switches were enabled, but neither logged activation. The live
snapshot still counted three active Animators. The log does not record the new
preference values, so exact failure stage is not established.

The first implementation required exact serialized component counts (4
CanvasRenderers for PB; 2 Animators, 8 color scripts and 13 sprites for Star
Power). That is brittle after runtime object creation. It also counted the
separate `CenterGlow` sprite as a side glow. Version 0.4.101 instead identifies
the PB popup by exact hierarchy path plus `HighScoreDisplay`, Canvas, and
Animator, and finds the named left/right Star Power branches under the
controller without requiring fixed child counts. Only left/right descendants
are suppressed; `CenterGlow` stays active. If a requested visual target is
still absent after three one-second attempts, one warning is logged and
vanilla visuals remain. The timing-settings snapshot now records both
preferences and whether each target was suppressed. The fix builds, but its
activation and FPS effect are not established until a Windows run.

## v0.4.101 log and v0.4.102 lookup

The supplied 0.4.101 log records both Sloptifine 3 preferences as `True`,
both suppression states as `False`, and a one-time missing-target warning for
each during two songs. It confirms the count-guard relaxation alone did not
fix either live discovery path. The same windows report 1,554 and 1,539
scheduled render callbacks/s, which cannot establish presented FPS. Their
largest update gaps are 31 and 106 ms. Neither visual optimization ran, so
these windows measure only the existing combined Sloptifine configuration.

Version 0.4.102 resolves the single Gameplay `HighScoreDisplay` component
using `FindObjectsByType` with inactive objects included, then gets its own
Canvas and Animator. It resolves the two named side-glow Animators in the
Gameplay scene under a guitar player, requiring the same parent and touching
only their descendants. This avoids dependence on two serialized hierarchy
paths. A failed lookup continues to leave the game components untouched; the
one-time missing-target warning now includes the observed Gameplay component
count or Animator names if discovery still fails. The match needs a runtime test;
the log cannot tell why the earlier paths failed.

## v0.4.102 log, v0.4.103 lookup fix, and separate score-path screen

The 0.4.102 log confirms both optional visual preferences were on. On the
first eligible pass, `OptionalHudAnimationsOverride` failed with `Method
unstripping failed`, restored its component states, and latched itself off for
the scene. Neither option ran. The first of two 30-second timing windows has
1,559 scheduled render callbacks/s and a 59.8 ms maximum simulation gap.
There is no presented-FPS capture or causal tie to the optional feature.
Version 0.4.103 removes the new `FindObjectsByType<T>(FindObjectsInactive,
FindObjectsSortMode)` call and generic `GetComponentInParent<GuitarPlayer>`
lookup. A further exact-scene/reference check found a more fundamental bug:
the optional-visual code aliased `Animator` to Clone Hero's custom
`Il2Cpp.Animator`, while both the PB notification and the side glows use
`UnityEngine.Animator`. The current IL2CPP `StarPowerController.animatorLeft`
and `.animatorRight` fields are typed as `UnityEngine.Animator`; the supplied
Gameplay scene's `HighScoreDisplay` serialized animator reference points to
the native Animator on the same PB object. In all four current Star Power
prefabs, the controller's two serialized references resolve to native Animator
components on `SpSideGlows L` and `SpSideGlows R`. That type mismatch would prevent
discovery even after replacing the unsupported search overload.

The release finds the active `HighScoreDisplay` once per discovery
attempt, verifies it belongs to Gameplay, and gets its Canvas and
`UnityEngine.Animator`. For the glows it starts from the local guitar player,
gets its `StarPowerController`, and reads the two serialized Animator fields.
It still validates the expected side-glow names and shared parent before
touching their children. Neither feature needs a full Animator scene scan;
both fail open. Version 0.4.103 builds with zero warnings and the Core checks
pass (114/114). Runtime activation and FPS effect remain unverified.

The same type audit found an independent safety issue in the default-off TMP
scale experiment. Its ancestor check recognized Clone Hero's custom
`Il2Cpp.Animator` and Unity's legacy `Animation`, but missed
`UnityEngine.Animator`; the current PB notification uses that native type.
The release recognizes both Animator types and checks text beneath either
at the experiment's 20 Hz animated-label cadence. This does not turn the
experiment on or alter text during normal use.

In parallel, I compared the older source's `ScoreManager.FixedUpdate` and
`HighScoreDisplay` with exact-build methods and the WPA method export. The
older source calls `SpriteFont.SetSprites` on each score tick, suggesting
redundant work. The current `ScoreManager.Update` at RVA `0x1286B0` has a
cached-value comparison at `+0x142` before calling `SpriteFont` at
`0x224EE0`; the older unconditional call cannot be assumed current. The
current `HighScoreDisplay` no longer declares `Update`. `ScoreManager.Update`
has ~27 ms inclusive sample weight in the older 31-second method export,
while `LyricDisplay.Update` has ~6 ms. Neither supports a scoring/UI detour
on present evidence. The current StarProgress call remains a possible
separate investigation, but its native method updates scoring visuals and
has no isolated cost in the supplied export. Do not throttle it based on the
older source.

Further old-source screening covered `StarProgress.SetProgress`,
`SongSelect.Update`/`SongListAdder.DoListLayout`, `MainMenu.Update`,
`Client_GameplayScene.Update`, and `GuitarNoteRenderer.Update`.
`StarProgress.SetProgress` in the older source already returns when score
matches `lastScore`; the exact current method at RVA `0x2DF7F0` also compares
its cached score at `+0x215` before updating progress. `SongListAdder` checks
canvas height and row count each frame but only rebuilds list components when
the count changes. The older network gameplay scene caches its GameManager
and local player after startup rather than searching each active frame.
MainMenu's observed path polls a few menu keys. The old guitar note renderer
walks visible notes and writes moving transforms/property blocks; the current
renderer also gates some work on `OnDemandRendering`, so freezing its whole
update would break note motion and has no measured justification. These
findings rule out several blanket skips, not the broader render/cull work.
The active Gameplay scene also contains `PracticeSectionDisplay.FixedUpdate`,
but exact-build disassembly of its RVA `0x2E92A0` returns immediately when its
section list is null. It has no mapped sampled cost in the supplied method
export. The older `MainMenu.CycleThroughBuildTag` coroutine waits 15–120
seconds between label writes, and `SongScan` polls its worker only while a
scan is running; neither is a persistent song-frame candidate.

The wider 31-second method export puts `MultiplierScaler.Update` at ~57 ms,
`PlayerCamera.Update` at ~20 ms, `HighwayScroll.Update` at ~22 ms, and
`GuitarFretAnimator.Update` at ~1 ms. The exact current `SongProgress.FixedUpdate`
still writes two transforms as score progress advances, but has no mapped
sampled cost in that export. An extra Harmony call at every tick could
outweigh a blanket skip of these small methods, and song-progress writes
must remain synchronized with practice seeking. The current resource assets
place 31 active serialized sprites under a guitar's `Combo` visual branch,
including FC ring, ghost meter, streak ticks, and two smoke sprites. The
existing combo-smoke switch pauses its UV scripts. Any further removal from
that branch would hide gameplay feedback and needs a separate user choice.

A mechanical scan of the older source's `Update`/`LateUpdate`/`FixedUpdate`
bodies found an apparent per-frame `new byte[3]` in `GHLNoteRenderer.Update`.
The current exact-build `GHLNoteRenderer` no longer overrides `Update`; it
inherits the shared guitar-note renderer path instead. The other old note
renderer `new` expressions in `Update` construct value-type vectors rather
than managed arrays. This rules out porting an old GHL allocation patch to
the current version. The old `HighwayScroll.Update` writes through
`Renderer.material`, whereas the current build uses a property block and its
method accounts for only ~22 ms in the 31-second export; a blanket material
cache rewrite would target obsolete code.

## v0.4.103 Star Power branch optimization and broader screen

Version 0.4.103 deactivates the two side-glow branches behind the existing
default-off switch. In the exact current Guitar Player prefab, the two
`SpSideGlows L/R` branches contain two native Unity Animators, eight
`AnimatorColorBehavior` scripts, and cosmetic SpriteRenderers. The
`StarPowerController` is on the active `Updated SP Animation` parent, beside
`CenterGlow`. The override reads the controller's serialized Animator
references, validates the names and shared parent, saves each branch's
`activeSelf`, and calls `SetActive(false)` only during eligible offline solo
play. It restores the saved states on pause, menu opening, scope exit, and
scene cleanup. Re-enabling a branch can restart its animation. The 31-second
method export maps `AnimatorColorBehavior.Update` to ~22 ms sampled CPU across
all instances, without proving how much belongs to this guitar. Runtime
activation and presented-frame effect are unverified.

A further static screen of the exact assets found 61 enabled SpriteRenderers
with zero serialized alpha and active ancestors across all resource prefabs.
Most are Star Power side glows across the player variants; this is **not** a
live count and a zero-alpha sprite can later become visible. The current
Gameplay scene itself has zero such active SpriteRenderers. Three zero-alpha
resource sprites are under the score's high-score overlay and one is
`CenterGlow`; they are separate animation targets, not safe additions to the
side-glow switch without mapping their state transitions.

The mapped 31-second method view puts other non-input managed work in
`EventSystem.Update`/mouse raycasts (~1.8 s inclusive),
`TMP_UpdateManager.DoRebuilds` (~320 ms), `SpriteAttach.Update` (~275 ms),
and the necessary guitar-note renderer (~261 ms). Existing default-off
switches already target the mouse path; the SpriteAttach cache has sometimes
cost more than the native method. The guitar prefab also contains ten
`CustomFadeProperties` components (no mapped sample row), five
`GuitarFretAnimator`s (tiny mapped cost), and five `SpriteAttach`s. This does
not justify another blanket skip. It does, however, justify one narrower
change: SlopHero's existing Hide fret strings setting already disables all
renderers beneath those five string objects, while their five `SpriteAttach`
scripts continue camera/viewport alignment work. Version 0.4.103 now pauses
only those five scripts as part of the hide setting, remembering each
enabled state and restoring it when strings are shown or overrides end. It
does not touch the separate `SpriteAttach` scripts on the SP bar and rock
meter. The ~275 ms mapped `SpriteAttach.Update` row covers all instances, so
it is an upper bound for this narrower saving, not its measured effect. The
exact Gameplay scene has no serialized
component declaring `OnGUI`, so the ~123 ms `GUIUtility.BeginGUI` row cannot
be assigned to a removable game GUI script from that scene.

The exact player camera prefabs are **perspective** (layer-9 mask), whereas
the Gameplay `HUDCam` is **orthographic** (layers 5 and 12). The background
camera is a third, orthographic clear camera. Merging the player and HUD
cameras would require transforming the HUD geometry and preserving its render
order; simply combining their masks would change the visuals. The existing
default-off background-camera experiment is the bounded camera prototype.
The Unity WPA render/cull weights still lack per-camera or per-renderer
attribution, so they cannot justify a broader camera removal.

Version 0.4.104 adds per-camera CPU phase intervals to the optional
30-second timing capture using Unity's pre-cull, pre-render, and post-render
callbacks. It registers callbacks only during capture and removes them on
completion. The pre-cull/pre-render interval includes culling and any camera
callbacks between those points; the pre-render/post-render interval includes
render submission and callbacks, not GPU presentation. These are useful for
comparing Background Cam, PlayerCamera, and HUDCam within one run, but are not
an exact decomposition of the older WPA `Camera::CustomCull` samples. The
current scene and player-camera prefabs already have occlusion culling off, so
a new occlusion toggle would do nothing. The regular guitar `PlayerCamera`
already has a 120-unit far plane; the 10,000-unit far planes belong to GHL
and drums. A proposed guitar far-plane cap was therefore rejected before
implementation. All three camera masks are specific to their visual layers;
changing the HUD or player masks could hide score/input elements or notes and
needs a narrower runtime target first.

The prefab's active guitar SpriteRenderers are already concentrated on two
material references: 60 use material path ID 21 and 56 use 23; only 16 use
the other five references. There are two active `SortingGroup` components
under the guitar (`Combo` and `RockMeter/arrow`). This static composition
does not prove runtime batching, but it weakens the idea that the guitar
highway spends most of its render time switching among hundreds of materials.
Removing either sorting group could disturb streak/arrow layer order.

Several of the large obfuscated GameAssembly method rows are Rewired input
internals, not mystery gameplay visual loops: the exact dump identifies
`psesWFItqOzeBgCDVCsQpYLqlFby` as `PlatformInputManager` (its `Update`
row is ~468 ms), and `LrrjYPtZvfrRqrFyVCtNgkRgRJHw` as `IInputSource`
(~564 ms for one mapped method). Their weights sit within the broader
`Rewired.InputManager_Base.DoUpdate` (~2.58 s) chain and cannot be added.
They are not targets for lowering the user's 1000 Hz controller rate. The
older online `Client_GameplayScene.Update` contains `GameObject.Find`, but
only while waiting for the `GameManager` before the network song starts; it
is not a per-frame lookup throughout active gameplay.

## v0.4.104 camera capture and later presented frames

The uploaded 0.4.104 `Latest.log` (SHA above) includes one complete 30.0 s
camera timing capture from 20:25:43 to 20:26:13. The black-background option
activated at 20:25:43.478. `Background Cam` rendered 401 times during startup;
`PlayerCamera` and `HUDCam` each rendered 48,724 times. Average callback
intervals, in milliseconds per rendered camera frame:

| Camera | Pre-cull to pre-render | Pre-render to post-render |
| --- | ---: | ---: |
| Background Cam | 0.021 | 0.024 |
| PlayerCamera | 0.026 | 0.058 |
| HUDCam | 0.022 | 0.040 |

The intervals prove that the callbacks execute and identify the two persistent
camera passes. They do not enclose all of Unity's native
`Camera::CustomCull`, nor are they an additive decomposition of the older WPA
tree. The background-camera option already removes its pass during the active
song. The HUD pass still draws score and input feedback, including the 48 live
guitar input-viewer sprites; suspending it would visibly remove useful UI.

The uploaded PresentMon raw CSV (SHA-256
`7bfe314975c9b13056f00d85833713d124ba3279e8b3211f962a39e60f6818d9`)
records a different 29.950 s song window beginning around 20:26:31, after the
camera capture ended. It has 48,804 presents, 1,629.5 presented FPS, 0.614 ms
mean / 0.893 ms p99 present interval, and 0.583 ms mean CPU busy versus
0.242 ms mean GPU busy. Fourteen frames exceed 2 ms; none exceed 5 ms. The
largest 3.784 ms present has 3.749 ms CPU busy and 0.281 ms GPU busy. Mean GPU
busy stays near 0.24 ms in each five-second segment while CPU busy rises in
the slower segments. This supports further CPU work but does not attribute
those spikes to a camera, mod hook, or specific game callback. Because the
windows do not overlap, do not subtract their numbers to claim an FPS gain or
estimate a camera's fraction of that PresentMon frame.

The exact Gameplay camera assets also have HDR, MSAA, and occlusion culling
disabled, and the regular guitar camera's far clip is already 120 units.
Those settings do not offer a new guitar optimization. The next rendering
experiment needs to preserve the live HUD and note visuals and isolate a
specific native operation; a blanket culling-mask or HUD-camera skip is not
supported by this capture.

### First-song setup prototype after the 0.4.104 capture

The 0.4.104 timing capture's maximum `Core.OnUpdate` is 59.999 ms. On that
first song, the log records the legacy native mouse detour activation at
20:25:43.458 and the Rewired mouse Harmony installation at 20:25:43.476,
after UI/cheat state changes around 20:25:43.418–.420. That roughly 58 ms
cluster is consistent with the worst Update, though log timestamps do not
prove either install's isolated cost. The later PresentMon file is from a
second song, after those hooks were installed, and its worst present is only
3.784 ms; this remains a nonmatched comparison.

Version 0.4.105 prepares the exact-build native detour during
Gameplay scene initialization, immediately undoes it, and reapplies it only
when the existing solo-song gate opens. It also installs the opt-in Rewired
mouse prefix at scene initialization and leaves it pass-through outside an
eligible solo song, removing the first-song Harmony patch from `Core.OnUpdate`.
Turning either preference off and leaving Gameplay still cleans up its hook.
The native detour logs one preparation and one activation duration; no
per-frame diagnostic work was added. It compiles against the supplied game
references with zero warnings. Windows runtime behavior and the size of any
first-song hitch reduction are unverified. The pass-through Rewired prefix
adds small mouse-path overhead while paused/online in Gameplay when that
experimental preference is on, so this tradeoff needs review before release.

The symbolized WPA grid still shows `Camera::CustomCull` as one collapsed
4,654 ms inclusive node shared across cameras. The new Unity callbacks cannot
resolve its internal callees or camera identity. The confirmed camera-side
gain is the existing blank-background pass removal; both remaining cameras
draw necessary highway or HUD content. Avoid a whole-camera or native-cull
skip without a narrower target and a restore path.

## v0.4.105 package

The release build succeeds against the supplied generated assemblies with
zero warnings/errors, and all 114 Core checks pass. The ZIP contains
`Mods/SlopHero.dll`. It moves two optional first-song mouse-hook installs to
Gameplay scene initialization. The native detour remains undone until eligible
solo play, and the Rewired mouse prefix passes through outside that scope.
This has not been validated in a Windows run or matched PresentMon capture;
do not claim a hitch reduction yet.

- ZIP SHA-256: `706479ef82c39a1b84192356b6785ffc3b48ac268ec7f5757546984598e15b23`
- DLL SHA-256: `4e300b584e7bd795313a8aed88799e3fa541ce06da7c48c51ff17440bceca847`

## v0.4.106 package

The release build succeeds against the supplied generated assemblies with
zero warnings/errors, and all 114 Core checks pass. The ZIP contains only
`Mods/SlopHero.dll`. It reduces repeated scene hierarchy discovery in the
optional fret-effect and combo-smoke settings and holds the experimental
Unity worker limit through a brief offline solo pause. These changes are
unmeasured; no FPS gain is claimed. The package includes the v0.4.105
first-song hook preparation described above.

- ZIP SHA-256: `4904372c40c91292cb5741bf25f38ca993157ca0d419991e5d0254009b4635d4`
- DLL SHA-256: `d1c3d93ce647df1a85fa2eff0022a553ed8150b4e0e2fa1bf6333768d5a87238`

### Further screen after 0.4.105 publication

The exact Gameplay `level2` asset has 35 active-ancestor score sprites under
`HUDCam/HUD`, all on one material reference. Its input-viewer variants have
322 active-ancestor serialized sprites using two material references, but
the runtime snapshot has only 48 enabled guitar sprites and zero GHL/drum
viewer sprites. This is weak evidence for a custom material-batching rewrite;
the visible viewer and score graphics cannot simply be culled. The older WPA
`Camera::CustomCull` node remains collapsed and shared across cameras.

The 0.4.104 log's first-song hook-install cluster aligns closely with its
59.999 ms `Core.OnUpdate` maximum, but historical logs also contain larger
Update maxima after optional hooks were already installed. The 0.4.105
preparation change targets one plausible source of the first-song hitch,
not every rare spike. Runtime timing of the two installations is needed to
validate that specific mechanism; no further bulk camera skip is justified.

The v0.4.106 change removes `PerformanceOverrides`' periodic
30-second rediscovery of the fixed fret-effect pools. It still discovers a new
neck on first use and clears the cache on scene transition. The 0.4.104
capture reports up to 0.755 ms for a performance-effects pass, but it does
not identify whether that maximum was the old rescan. The change removes
repeated component-array allocations from stable necks and has no expected
effect on hit particles, scoring, or input; a future Windows capture should
check that effects remain suppressed after long songs.

The older 0.4.98 session at `18:01:54–18:02:27` repeatedly applied and
restored the experimental Unity worker limit (15 ↔ 14) during brief eligible
intervals. The cheat-scanner and UI-event switches changed state at the same
timestamps, which points to the shared solo-gameplay eligibility gate rather
than an isolated worker failure. The newer 0.4.104 log holds 14 workers
through a complete 30-second song, so this is a transition issue, not evidence
of steady-state worker churn. A v0.4.106 change holds an already
applied worker limit through at most one second of an offline solo pause, but
restores immediately on option-off, scene/solo exit, or online play. This
reduces native pool reconfiguration in brief pauses without changing the
ordinary song path; no presented-frame gain is claimed.

I reran the exact-build method mapper over the supplied 31-second ETW method
CSV. Its largest named game-side rows after the already handled UI mouse
chain are `BasePlayer.Update` (456 ms), `BaseGuitarNoteRenderer.Update`
(261 ms), `GameManager.Update` (138 ms), and `InputViewer.Update` (16 ms).
The guitar note renderer already guards its 500-entry pool traversal with
`OnDemandRendering.willCurrentFrameRender`; the score and chart paths under
the player/manager updates are timing-sensitive. The input viewer row is too
small to justify another per-frame interop hook. `SpriteAttach.Update` is
275 ms in the same old trace, but the current exact-input cache has shown
lower FPS in user runs; the five hidden fret-string components are already
disabled through the hide-strings option. The method weights are inclusive
and from an older capture, so none identifies a new removable native loop.

The same runtime snapshots show two local combo-smoke `UVScroll` components
already paused. The existing modifier rediscovered them through
`GetComponentsInChildren` every two seconds even while the same player prefab
remained live. A v0.4.106 change remembers a player after finding
both fixed smoke components. New practice/player objects are still discovered,
and destroyed visual children clear that discovery latch. The native UV work
was already paused; this only removes repeat mod-side hierarchy scans and does
not imply an FPS gain.

A further exact-resource check of the guitar's hidden `sidebar_l/r` objects
mapped their extra MonoBehaviour to `TrackSidebarColoring` (MonoScript path
1541 in the supplied `globalgamemanagers.assets`). The current generated
class declares no `Update`, so hiding the sidebar renderers does not leave a
per-frame sidebar script to suspend. The separate fret-string objects do have
`SpriteAttach` (resource script type 75) and are already handled by the
hide-strings option. This rules out a redundant sidebar hook.

## v0.4.107 package

The release build succeeds against the supplied generated assemblies with
zero warnings/errors, and all 114 Core checks pass. The ZIP contains only
`Mods/SlopHero.dll`. The v0.4.106 runtime log confirms that both optional
visual settings were active and the side-glow branches were being suppressed;
the reported flash occurs on pause/focus resume, between the existing 20 Hz
visual passes. v0.4.107 re-applies suppression immediately when the song
unpauses and when Clone Hero regains application focus. The focus hook is
installed only while one of those visual options is enabled in Gameplay; the
pause-state check runs only while waiting for an eligible offline solo resume.
This corrects the visible gap, with no measured FPS claim.

The optional native legacy-mouse detour is now prepared at mod startup rather
than during Gameplay initialization when its setting is enabled. This moves
the one-time setup cost; it does not remove it. The Rewired mouse prefix still
prepares during Gameplay initialization. The v0.4.106 log reports about
38.5 ms for native detour preparation and 17 ms for the Rewired prefix, so a
Windows log should confirm the native setup now happens before Gameplay.

- ZIP SHA-256: `7ff6599a71e2f4a805888e73ce567f44e9f4d507bfb838559bf47ed7c20ac476`
- DLL SHA-256: `33560bfb5f7e2b13932f171cf163501e40b291a636cfd454bdd22207f687a547`

## v0.4.109 package

The uploaded 0.4.108 log exposed an F8 renderer error: `GUIStyle.get_border`
is an unavailable unstripped IL2CPP wrapper in this game build. The menu also
attempted to patch `GameManager.OnApplicationFocus(bool)`, which is not present
in the supplied generated assembly. Version 0.4.109 removes GUIStyle border,
margin, and padding getter calls, and subscribes to Unity's `Application.focusChanged`
event through the verified IL2CPP delegate wrapper.

The log's three 30-second timing captures report scheduled render callback
rates of 1,583–1,662/s and p99 intervals of 0.82–1.17 ms. Those are Unity
callback intervals, not PresentMon frame presentation, and the settings
changed between captures. They do not isolate an optimization or establish
an FPS gain. The log confirms the Rewired keyboard hook installs, but its
timing snapshots show it unsuppressed; this capture does not verify its active
song behavior.

The F8 menu now groups its eleven former pages into seven categories. HUD and
Performance use scroll views; toggle rows have less card-like fill. The
invented green readiness label was removed. The existing Menu Accent remains
customizable and colors selected navigation, section markers, and active
switches. The transparent character cutout sits on the right side; its PNG is
decoded on first open and drawn only while F8 is open. Build and Core checks
pass; the revised menu and focus event still need in-game verification. No
performance gain is claimed.

- ZIP SHA-256: `11626126073145e5c2669828538b604103eec821f2582a5e4ab6f69dd6ea1f26`
- DLL SHA-256: `10aa07057b19eb7d7fe673f102fbd7be62385faf8819733041b6c12df35e7245`

## v0.4.108 package

Version 0.4.107 still allowed a short visual flash on resume: the immediate
resume pass could see the suppression flags already set and skip checking
whether the game had reactivated the targets. Version 0.4.108 forces those
target-state checks on the resume path, including focus return. The final
in-game result still needs confirmation.

The separate default-off Rewired keyboard experiment targets the generated
`ThreadSafeUnityInput.Keyboard.Update()` method verified in the supplied
exact-build assemblies. It pauses only that keyboard snapshot for offline solo
Quickplay. Keyboard-bound controls stop updating during the song; the mouse,
joystick/controller and instrument polling methods remain independent. It
installs during Gameplay initialization when enabled, or while a user enables
it from the settings page; until the strict eligibility gate opens the prefix
passes through unchanged. The default-off hotkey gate samples only SlopHero's
restart/practice bindings at 120 Hz. It does not patch the game's polling loop.

The SpriteAttach cache and its menu preference were removed. The latest
PresentMon sample reports about 1,629.5 presented FPS, 0.614 ms mean / 0.893 ms
p99 present intervals, 0.583 ms CPU busy mean and 0.242 ms GPU busy mean.
That capture is a CPU-limited baseline, not proof for any new setting.

The settings window now uses a dark panel/sidebar layout inspired by the
user's mockup. The existing Menu Accent color now controls selected navigation,
section markers and active switches. These controls draw only while the F8
settings page is open. Build validation: zero compiler warnings/errors and 123
Core checks. The visual style has not yet been runtime inspected in Clone Hero;
performance and runtime behavior of the three experiments remain unmeasured.

- ZIP SHA-256: `39acec5e41ce9b1ae382e19dea4d92f7d2c62fe606a9c3849dd2fea8704798ae`
- DLL SHA-256: `5c50a83ec38627689a0d25d7f930988f4b5cbf426af70b94bb4fd7e4718fd2d1`

## v0.4.104 package

The release build succeeds against the supplied generated assemblies with
zero warnings/errors, and all 114 Core checks pass. The ZIP contains only
`Mods/SlopHero.dll`. The optional capture now reports per-camera pre-cull to
pre-render and pre-render to post-render CPU averages. No FPS gain is claimed;
those callback intervals are attribution data. The subsequent uploaded log
confirms both camera callbacks and normal song completion, but provides no
matched FPS comparison.

- ZIP SHA-256: `6d60c6a85b65510856a5e05e62b18fb5627ba426a679467d837c48273a615cf8`
- DLL SHA-256: `ae1090753e77ed9919bd2fa1f4852742ae3fed37eda303a1e2fd6eba42965e7f`

## v0.4.103 package

The release build succeeds against the supplied generated assemblies with
zero warnings/errors, and all 114 Core checks pass. The ZIP contains only
`Mods/SlopHero.dll`. The default-off Sloptifine 3 visuals and the conditional
hidden-string alignment pause have no matched PresentMon comparison, so no
FPS gain is claimed.

- ZIP SHA-256: `c931083b30561ea7a532a36b2d4185357850a64fe439f212667e972382031f6f`
- DLL SHA-256: `b65b1e756d8f00abc3080a425c559dfbe0fbb4a16dc971b40977e990179f6c4b`
