# Binding evidence for the supplied build

Static analysis, not in-game validation. Native RVAs identify the uploaded file,
not stable APIs for future versions. SlopHero uses generated interop properties
and methods and does not install native detours or write raw pointers.

Fingerprints:

- GameAssembly.dll SHA-256: `12bc21e5a88a2fc9abc259d5d67f24835145eabca6c3f863b075ba37ad75f197`
- global-metadata.dat SHA-256: `5f14231810f2641639167eb72956e85cced2f36cb4a84682bdefb1e9da3e3d42`
- Metadata version: 31.

The uploaded individual generated assemblies match those in the later ZIP.
Il2CppDumper generated type layouts and native method addresses successfully,
then exited nonzero at its final interactive ReadKey due to redirected stdin.
Its already-written outputs were inspected. Capstone provided instruction traces.

## Results-to-engine counters

PlayerStatsBox's display routine at RVA `0x126280` reads the results snapshot:

| Label | Snapshot offset | Source |
| --- | --- | --- |
| Notes Hit | 0x40 | Engine 0x9C |
| Total Notes | 0x44 | Count of included chart heads |
| Notes Missed | 0x44 minus 0x40 | Total included heads minus hits |
| Ghost Inputs | 0x78 | Engine 0xD4 |

The snapshot factory at RVA `0x20F3DC0` copies engine 0x9C to snapshot 0x40
and engine 0xD4 to snapshot 0x78. The live generated engine type is
Il2Cpp.ObjectPublicAbstractDoBoDoInBoObDoInSiBoUnique; the respective properties
are field_Public_Int32_6 and field_Public_Int32_17.

Live misses cannot use total chart length minus hits: that includes future
notes. SlopHero reads the same time-ordered chart list as the results factory,
from the getter at RVA `0x20F4F90`, generated property
prop_List_1_ObjectPublicObInObDoSiDoUIInBoInUnique_0, and counts resolved missed
heads. It processes heads incrementally, resetting on engine/chart replacement,
time rewind, reduced hit count, or changed practice bounds. Catch-up is bounded
to 4096 heads per refresh; large seeks may take several refreshes.

Note flags at 0x7C identify Disjoint (2) and Child (4); the snapshot factory
excludes heads having both flags. Note state at 0x84 identifies Hit (1) and
Missed (2). The miss routine at RVA 0x20F3610 sets Missed with the setter at
0x214B680. Other state flags describe dynamics, sustain, and squeeze status.
The generated properties map to these exact fields.

## Song controls

- PauseMenu.Method_Private_Void_PDM_11, RVA 0x23C6B0: native restart handler;
  disables gameplay input, checks networking, then reloads Gameplay through
  FadeBehaviour. SlopHero calls it only in offline solo Quickplay.
- PauseMenu.Method_Private_Void_PDM_3, RVA 0x23DAF0: sets
  GlobalVariables.isPracticeEnabled and reloads Gameplay.
- GameManager.Method_Public_Void_Int32_Int32_Boolean_Boolean_0, RVA 0xBBC70:
  sets the start section and additional-section count, resolves section ticks,
  resets the practice player's chart range, refreshes the practice UI, hides the
  section menu, seeks audio, and optionally starts its playback coroutine.
  SlopHero passes (capturedSection, 0, true, true). A/B tick overrides are cleared
  first. The routine writes practiceDisplayStartTick and practiceDisplayEndTick;
  these are used for the practice HUD range.

The startup state machine at RVA 0xD2190 shows the practice section menu when
isPracticeEnabled is true. The mod waits for songLoaded, playersInit, a practice
engine, and completion of outstanding initialization coroutines before selecting
the section. Whether these conditions suffice needs an in-game test.

## Validation boundary

The mod compiles against the exact uploaded interop/loader dependencies.
Portable tests exercise percentage math, hold latching, pause/scene gating,
clock reversal, and chart-head classification. Unity, Rewired, native scene
transitions, counter agreement, long-chart performance, and coexistence with
ClonZones need Windows testing. None are claimed runtime-verified.


## 0.2.0 practice regression fix

The user's 0.1.0 log shows automatic selection throwing in BeatRenderer's
reset helper (RVA 0x26ECC0), called by BasePlayer's range reset and then
GameManager's section-selection routine. The exception precedes the Gameplay
scene-initialized callback. Scene-loaded plus game-ready flags were insufficient.

BeatRenderer.Start (RVA 0x26DC40) initializes its beat data at native offset 0x28.
The reset helper requires that data. The 0.2.0 transition waits for:

- the Gameplay scene-initialized callback;
- a game manager distinct from the outgoing song's manager;
- a ready practice player, chart, audio sync, and practice UI;
- the visible section menu with its initialized section list;
- initialized BeatRenderer beat data and renderer arrays;
- no outstanding initialization coroutines.

Native selection is attempted once after these checks. A selection exception
is handled within the practice feature: pending selection is cancelled, further
practice shortcuts are disabled for that session, and the HUD remains enabled.
Seven regression checks cover premature-scene, stale-manager, missing-renderer,
missing-menu, missing-player, active-coroutine, and fully-ready conditions.
The repaired transition has not yet been tested in the Windows game.


## 0.2.1 rendering regression fix

The user's zipped 0.2.0 log confirms successful practice selection at section
indices 0 and 2. Its only error category is the repeatedly thrown
System.NotSupportedException: Method unstripping failed from GUI.DrawTexture,
which reached the HUD before the settings menu could render.

Managed IL inspection of the supplied UnityEngine.IMGUIModule.dll shows that the
two-argument DrawTexture overload forwards through several overloads to an
12-argument colors/borders overload whose implementation is a hard-coded throw.
This is not a counter or practice-engine failure.

The repair draws the background with the native-backed GUI.Box overload accepting
a GUIContent and GUIStyle, reached by the string/style wrapper. A white texture
is assigned to the background style and tinted with GUI.backgroundColor for
opacity. The relevant string-based Label, Box, Button, and HorizontalSlider call
paths were inspected for reachable unstripping-failed stubs; none were found.
Texture-only Label/Box/Button overloads were found to reach another stub in
GUIContent.Temp and are deliberately not used. This static inspection does not
establish Windows GUI behavior; the updated UI still needs a user test.

HUD rendering failures fall back to the original native-backed string Label
path. Settings failures close the editor instead of leaving invisible input
gating enabled. Errors are logged once per rendering category to prevent the
previous repeated-exception log growth. Practice selection logic remains unchanged.


## 0.2.2 practice input restoration

The user reports that the HUD, F8 editor, and section switching now work in
0.2.1, with no errors in the latest log. Guitar input required a keyboard action
following automatic practice selection.

The vanilla PracticeSectionMenu confirmation routine at RVA 0x10B7E0 calls:

1. Section selection at 0xBBC70 with the selected range and both booleans true.
2. GameManager.Method_Public_Void_Boolean_0(true), native RVA 0xBF510, to reset
   the practice attempt.
3. GameManager.Method_Public_Void_2(), native RVA 0xB6570, which walks enabled
   local player objects and sets BasePlayer.inputEnabled.
4. Clears GameManager.RestartAfterPause.

The shortcut previously stopped after step 1. Version 0.2.2 follows this native
sequence through step 4 and logs the practice player's inputEnabled state.
It uses the game's routine rather than writing player input flags directly.
The existing scene and renderer readiness checks still apply. Compilation was
verified; automatic guitar input restoration needs the user's Windows test.


## 0.3.0 feature bindings

- Best streak: native engine offset `0x98`, generated `field_Public_Int32_5`.
  The results snapshot copies it into `0x20` (factory RVA `0x20F3DC0`), which
  PlayerStatsBox reads for “Best Streak”. Read the native value rather than
  sampling the current combo, which could lose a peak between refreshes.
- Song metadata: `SongEntry.Name_StrippedTags`, `Artist_StrippedTags`, and
  `Charter_StrippedTags`; chart title uses Name rather than the chart filename.
- Highway sides: `TrackSidebarColoring.field_Private_SpriteRenderer_0`.
  Strings: renderers beneath `BaseNeckController.fretStrings`. Beatlines:
  `BeatRenderer.field_Protected_Il2CppReferenceArray_1_SpriteRenderer_0`.
  Leave components/game objects active, particularly the practice beat renderer.
- FC rings: sprites beneath `BasePlayer.FCIndicator`; guitar ghost-input count
  distinguishes zero-ghost perfect FC from regular FC. This does not turn the
  ring on after a miss. RGB overrides preserve native alpha and visibility.
- Score text: `ScoreManager.scoreFont/comboFont` sprite renderers and
  `BasePlayer.AccuracyText`. Stars: `StarProgress.starCount`. Star progress:
  `progressBar/progressBarEnd`. Song progress: `SongProgress.barTransform/endTransform`.
- Practice compensation uses `GameManager.practiceSpeed`, a tempo ratio (native
  restart telemetry multiplies it by 100 for `songSpeed`, RVA `0xBFDB9`).
  `GuitarNoteRenderer.noteSpeed` is initialized from the profile at `0x1162A0A`.
  Its cached horizon uses `(noteZPosFarLimit - strikeLine) / noteSpeed`
  (`BaseNoteRenderer.Start`, RVA `0x1162980`). Beat positions multiply chart-time
  difference by speed at offset `0x68` (`0x26E8BD`); highway texture scrolling
  uses offset `0x50` (`0x38194D`). Divide these rendering speeds by tempo,
  maintaining separate baselines and restoring them when disabled. Audio and
  engine timing are unchanged. No Windows runtime validation yet.
- All supplied `GUI.TextField` overloads reach an unstripping-failed
  `GUI.DoTextField` stub. The editor uses native string `GUI.Box/Button/Label`
  and `Event` character/key input. Audited character/key/control/rawType getters,
  GUIStyle font/wordWrap setters, and clipboard getter contain no reachable
  unstripping-failed wrapper stubs. Source inspection does not establish runtime
  compatibility, so keyboard/color editing still needs an in-game test.

- CoreModule audit also found throwing generic overloads for
  `Component.GetComponentInChildren<T>(bool)` and `Object.FindObjectsOfType<T>(bool)`.
  Use the available `GameObject.GetComponentInChildren<T>(bool)` and active-object
  `Object.FindObjectsOfType<T>()` paths instead. Child renderer collection overloads
  used by the mod and native renderer color/visibility properties pass this audit.


## 0.3.1 repairs

The supplied runtime log loaded 0.3.0 without managed exceptions; the reported
speed/ring errors concern state management rather than failed bindings.

Practice compensation now reads the same configured value used by all three
native renderers at initialization. BasePlayer.player (`0x88`) → profile (`0x10`)
→ note-speed setting (`0x20`) → raw int (`0x10`). The profile is explicitly
viewed as `ObjectPublicObBoObAcBo1ObBoObObUnique` because the generated child
profile shadows an identically named setting field at `0xD8`.
`field_Public_Object2PublicBoSiInSiDoStSiStStUnique_0.field_Protected_T_0`
is the raw configured int. The convenient `Method_Public_get_Int32_0` computes
a derived percentage and must not be used here. Native reads: note renderer
`0x1162A0A`, beat renderer `0x26DD18`, highway texture `0x37F84F`.

FC sprites have `DesaturateSprite` shader properties `_OutputSaturation` and
`_OutputHue`, IDs stored at component offsets `0x30`/`0x34` (Awake `0x2E2A30`).
Vanilla hue setters (`0x2E3120`, `0x2E2E80`, `0x2E34A0`) select hues 0, ~0.167,
and ~0.35; the saturation setter is `0x2E3900`. Scoped Harmony postfixes on the
corresponding generated public methods capture native property changes and
reapply the requested HSV to registered FC components only. Vertex grayscale
controls brightness. Native alpha/activation remain unchanged; captured hue
and saturation are restored on opt-out. Scene unload clears native references.

New shader/property-block/HSV APIs and existing string IMGUI/slider APIs were
checked for reachable unstripping-failed wrappers. 42 portable checks pass.
Windows validation of the new hooks, shader result, and color picker is pending.
No game-wide performance mode or FPS improvement is claimed in this release.


## 0.3.2 visibility and overhead investigation

The new user log contains no managed SlopHero exception. Color behavior is
reported working; notes disappear when compensation is enabled. The preceding
code recalculated native lookahead at `BaseNoteRenderer` offset `0xF0` from live
clip limits. Native guitar rendering compares future note time against this
window at `0x115A5B5` and stops enumerating when outside it. This makes an invalid
window sufficient to hide notes without throwing. The new version preserves
and scales the initialized native window and rejects invalid inputs. It logs
initial/target speed/window values for the next Windows reproduction; static
inspection does not establish the runtime cause conclusively.

FC updates now skip identical selected colors; native hue/saturation postfixes
continue forcing the custom color immediately. Renderer discovery is throttled
and references clear on scene changes. Hide toggles configure renderer state
when settings/player resources change. Disabled score coloring skips its scans.
Passive IMGUI draw work occurs only for repaint events, with full event handling
retained for settings and dragging. Parsed colors and background GUIStyle are
cached. Counter resets use a lower-bound search on the ordered chart ticks,
including duplicate ticks at practice boundaries. A 100,000-head synthetic seek
needs fewer than 20 reads; this measures algorithmic work, not Windows FPS.

54 portable checks pass: existing hold/accuracy/readiness checks, configured
speed regressions, lookahead validity/scaling, and boundary-safe practice search.
The optional two-Hz HUD mode trades display refresh rate for less counter work.
Thirty-second optional callback captures use managed Stopwatch timestamps and
write summaries only; no additional Unity APIs or removal of gameplay objects.
There is no measured game-wide FPS improvement or game-system performance mode.


## 0.3.3 concrete speed read

Runtime evidence disproved the generated generic accessor used in 0.3.1/0.3.2:
`field_Protected_T_0` produced 1,344,637,400 while the initialized note renderer
reported 23. At tempo 1.9500008 this applied ~689,557,400, explaining disappearing
notes independently of the lookahead window. The earlier generated-field mapping
must not be treated as a working raw-value accessor in this IL2CPP installation.

`ConfiguredNoteSpeed.Read` now uses read-only Marshal reads of the concrete native
pointer path: BasePlayer.player's object → `+0x10` profile → `+0x20` setting →
`+0x10` int. These are the same reads traced at native note renderer initialization
(0x11629F0–0x1162A0A), beat initialization (0x26DCFE–0x26DD18), and highway texture
initialization (0x37F835–0x37F84F). The caller remains under BuildGuard and initialized
player gating. Missing pointers/implausible settings disable compensation rather
than applying them. Effective speed has an additional finite/magnitude guard.
Portable regression checks construct and free a real unmanaged pointer chain,
covering the observed 23 and corrupt magnitude; 63 checks pass in total.

The latest timing captures identify HUD repaint as the largest average mod
callback, but overlapping FPS ranges do not demonstrate a benefit from reduced
counter refresh. Native HUD style setters and song-color parsing now run only
when the corresponding preferences change. No FPS gain or game-wide CPU/GPU
bottleneck is claimed. The concrete reader and changed HUD path need Windows
validation; logs are not included in the release package.
