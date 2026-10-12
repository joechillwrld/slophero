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
