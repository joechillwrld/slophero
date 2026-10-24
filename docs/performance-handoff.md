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

## Implemented subset of Sol's research

These are the research leads that reached code. They do **not** cover every
finding; the open native and Unity leads follow below. The options remain in
the Sloptifine pages and preserve their documented scope:

| Research lead | Current implementation |
| --- | --- |
| WPA identified Unity mouse processing and raycasts during play | `MouseUiGameplayOverride` patches the called `StandaloneInputModule.ProcessMouseEvent(int)` overload. It is default-off and only suppresses mouse UI during active play. Keyboard/controller navigation and Rewired instrument polling stay available. |
| Broader Unity UI event work was also costly | `GameplayUiEventOverride` is a separate default-off switch scoped to active offline solo play. It pauses Unity EventSystem work and resumes outside that state. It is not additive with the mouse-only switch while the broad pause is active. |
| Old source suggested frequent whammy texture uploads; current native code uses raw byte writes | `WhammyTextureThrottle` hooks the exact current data writer and upload wrapper. It permits an upload after data changes and leaves producer writes and GPU compute intact. It is default-off; the user has seen no clear performance gain, so keep it experimental. |
| Hidden beatline, leaderboard, particles, fret fades, combo smoke and star-progress effects continued some work while invisible/unused | `VisualOverrides`, `HiddenLeaderboardOverride`, `PerformanceOverrides`, `ComboSmokeScrollOverride`, and `StarProgressParticleOverride` offer separate controls and restore prior state. Scene discoveries and limitations are in the audit. |
| Old source showed a per-tick cheat-code scan; current component lookup was unreliable | `CheatScannerOverride` targets `CheatManager.Update()` directly, only during eligible offline solo play. It does not replace guitar input or disable the component. |
| Unity job queue setter was resolved through the matching player PDB | `UnityJobWorkerLimit` uses the generated `SetJobQueueMaximumActiveThreadCount` API and restores the prior limit. It is default-off; the latest three-run callback comparison shows no benefit. |
| Current `HighwayScroll.Update` combines property-block writes with video/fade behavior | `HighwayAnimationOverride` is a default-off whole-update freeze during offline solo songs. It may freeze those visuals and has no demonstrated frame gain. The old source's note `yield`/hit-detection paths remain untouched because they are scoring-sensitive and not confirmed in current code. |

ClonZones installs a `GuitarFretAnimator.Update` settle gate in the user's
setup, which may overlap with another fret hook. Current `HighwayScroll` also
checks `OnDemandRendering.willCurrentFrameRender`. Neither fact resolves the
remaining cost of eligible rendered-frame work.

## Still open: evidence to turn into code

1. **Name the expensive native caller.** The 31-second `Sloptifine-CPU.csv`
   puts roughly 9 seconds of *inclusive* weight at
   `GameAssembly+0x2FAB159` / `+0x2FAB63D` and matching `version.dll`
   return sites. Sol traced those GameAssembly PCs through
   `il2cpp_runtime_invoke` and the version proxy through MelonLoader's
   `InvokeDetour`. These rows overlap on one call path; they do not establish
   that reflection, MelonLoader, or SlopHero costs 9 seconds exclusively.
   The supplied `CPU_Usage_(Sampled)_Utilization_by_Process,_Thread,_Stack.zip`
   has populated stacks, while the method CSV has native offsets, but their
   exports lack a shared row key. Inspect the existing raw files and exact
   binaries first; if the caller cannot be recovered, state that limit before
   proposing an instrumented or symbolized follow-up. A blanket hook on
   `il2cpp_runtime_invoke` would touch the whole game and is not the fix.
2. **Finish Unity native attribution.** The matching PDB resolves
   `ExecutePlayerLoop`, camera/render dispatch, window messages, and job queue
   code. The sampled `UnityPlayer+0x6B3C1C` follows an indirect callback,
   `+0x577AB9` is job-queue state, and `+0x8050DB` is a Windows message/timer
   path. These are named *containers*, not removable gameplay work. Use the
   supplied PDB, stack ZIP, exact UnityPlayer, and native offsets to identify
   a concrete callback or render operation before patching. The worker-count
   experiment showed no benefit in the latest captures.
3. **Target current game visual setters narrowly.** Current
   `GuitarFretAnimator.Update` can repeat seven sorting-order writes in its
   idle branch. Current `HighwayScroll.Update` performs
   `GetPropertyBlock` → `SetVector` → `SetPropertyBlock` on eligible render
   callbacks and also advances video/fade state. The existing whole-update
   freeze is a user-selected visual compromise, not an unchanged-value fast
   path. Exact-build disassembly shows both methods already check
   `OnDemandRendering.willCurrentFrameRender`; `HighwayScroll` derives a new
   texture offset from `songTime` on every scheduled render. Check ClonZones
   overlap before considering a cache; preserve normal animation transitions.
4. **Attribute optional hook and UI costs.** Inventory enabled Harmony
   prefixes/postfixes under the user's actual settings. The broad
   `EventSystem.Update` pause prevents the narrower mouse handler from
   running in active solo play, so their effects cannot be added. SlopHero's
   own timed callbacks are small, but those timers exclude hook dispatch,
   Canvas rendering, GPU work, and the game's native callbacks. Evaluate the
   hooks and UI layout from the existing trace before suggesting a rewrite.

### Direct check of the supplied stack ZIP

I parsed the 86 MB stack CSV inside the uploaded ZIP. It has 113,554 Clone
Hero rows and timestamps from the capture. Rows whose stack traverses
GameAssembly sum to about 9,955 ms across 1,218 distinct stack shapes; that
is a broad inclusive total, not one 9.955-second function. The method CSV's
two hottest GameAssembly addresses are nested call sites in this invocation
path, but the stack export has no addresses with which to identify their
callers.

The stack table does name several managed callbacks: SlopHero
`Core.OnUpdate` totals about 101 ms, `ClonZones.Core.OnUpdate` about 55 ms,
and MelonLoader `ComponentSiblingFix` about 183 ms across the 31-second view.
These are also inclusive weights; the calls overlap their MelonLoader and
IL2CPP trampoline frames. `ComponentSiblingFix` appears throughout gameplay,
but its measured cost is small beside the broad native total and it maintains
component ordering for mod callbacks. Do not disable it globally. This check
narrows attribution and rejects a blanket MelonLoader or SlopHero update-loop
rewrite; it still does not identify the game callback behind the largest
native paths.

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

The next work session should start with the supplied native stack ZIP and
method CSV, inspect the exact `GameAssembly` and Unity paths above, and look
for a concrete callback that can be changed without touching note timing,
audio, online state, or 1000 Hz controller polling. In parallel, examine the
current generated assemblies and older source for visual-only per-frame work
the game's settings cannot suppress. Batch independent, grounded fixes into
one reviewable build; preserve existing SlopHero features. If a hot native
caller remains unidentifiable from these exports, record that specific limit
and keep working on the independent leads. Do not ask for the same ETL, WPA
exports, assets, or PDB again.

## Build and evidence

- Latest released build: SlopHero **0.4.83** on the private
  `slophero-v1.1-alpha` branch. The latest uploaded three-run `Latest.log`
  is from that version; its SHA-256
  `8DFD151C5664E71831849636E6E95EBC89C603C697DB3300FEDFF95D8AA56E46`
  matches the DLL in that release ZIP. Clone Hero is **1.1.0.6142**, Unity
  **2022.3.62f2**, with ClonZones 2.0.0 also loaded. This log has no
  SlopHero errors. A small FC-ring cleanup is pending in source; it is not
  a 0.4.84 release or a substitute for the open research above.
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
run with ring customization disabled. The pending source cleanup skips these
empty calls while other visual settings are active, and still runs once to
restore ring state after the user turns customization off. This is a small mod
  cleanup with no measured frame-rate effect; do not release it alone as the
answer to the broader optimization request.

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
