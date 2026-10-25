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

- Latest packaged build: SlopHero **0.4.90** on the private
  `slophero-v1.1-alpha` branch. The latest uploaded `Latest.log` is from
  0.4.89 (SHA-256
  `6bfd5b29167d86e05e37996606bc6b72df03f96e0c228bba954df27c65b0c596`).
  It confirms the legacy mouse detour's in-game install/restore path; its
  performance limits are described below. The earlier 0.4.87 log (SHA-256
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
