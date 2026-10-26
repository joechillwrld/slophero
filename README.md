# SlopHero 0.4.111 alpha

**Sloptifine** is the name for SlopHero's performance work: measured, reversible
optimizations that preserve guitar timing and polling, online play, and existing
mod features.

A MelonLoader mod targeting the supplied **Clone Hero v1.1.0.6142 Windows x64**
build, compiled against its loader and generated assemblies. Version 0.4.111
adds separate default-off HUD-camera and guitar-camera render benchmarks for
offline solo play. The HUD test hides score and gameplay HUD; the guitar-camera
test hides notes, highway, and 3D effects while scoring and the HUD continue.
Only one camera test runs at a time, and both restore on pause, focus loss,
scope exit, or scene change. The black-background camera option now pauses
only its chart-targeted video while that camera is hidden. Version 0.4.110
adds a default-off 120-to-80-unit guitar-camera far-clip experiment. These are
measurement candidates, not claimed FPS gains; use matched PresentMon captures.
Version 0.4.109 fixes the F8 renderer
failure reported in the latest log, removes the invented
green readiness label, and consolidates the settings into seven categories
with scrolling on the long HUD and Performance pages. Settings rows use less
boxed styling. Focus restoration now subscribes to Unity's focus-change event.
The transparent wheelchair illustration appears at the right of the settings
panel; the PNG is decoded on first open and drawn only while F8 is open.
Version 0.4.108 added separate default-off experiments to pause Rewired
keyboard snapshots in offline solo songs and sample SlopHero restart/practice
hotkeys at 120 Hz. The keyboard switch also pauses keyboard-bound game
controls; the hotkey switch can add up to 8 ms to those shortcuts. Neither
touches joystick/controller or instrument polling. The slower SpriteAttach
cache was removed, and resume reapplication for the optional Star Power side
glows and personal-best popup was added. The customizable Menu Accent colors
selected pages, section marks, and active switches only while F8 is open.
These changes have no measured FPS result. Version 0.4.107 moved the visual resume attempt into the
next LateUpdate and moved setup for the optional legacy-mouse detour out of
Gameplay scene initialization and into mod startup. Version 0.4.106
reduces repeat discovery work in the fret-effect and combo-smoke overrides.
It also keeps the optional Unity worker limit steady across brief offline solo
pauses, restoring it immediately when the option is off or online play begins.
These changes have no measured FPS result. Version 0.4.105
prepares the optional native legacy-mouse detour and Rewired mouse prefix
during Gameplay initialization. The detour remains inactive until eligible
solo play; the Rewired prefix passes mouse input through while paused or
online. This moves their first installation out of the active song update.
The first-song hitch improvement is unmeasured, and the Rewired prefix adds a
small pass-through check outside solo play when that option is enabled.
Version 0.4.104
adds camera phase attribution to the optional timing capture: per camera it
reports CPU time from pre-cull to pre-render and from pre-render to post-render.
These callback intervals help identify which camera is busy; they do not measure
GPU presentation and add no callbacks outside an active capture. Version 0.4.103
fixes optional visual discovery to use Unity's native Animator type and the
current live component references. It also deactivates the two Star Power
side-glow branches during eligible solo play when enabled, restoring them on
pause or song exit; their animation may restart. Hiding fret strings now also
pauses only their five cosmetic alignment scripts, restoring their prior
states when shown. These changes have no measured FPS result and the Sloptifine
3 switches need a Windows activation test. Version 0.4.102
resolves the optional personal-best popup and Star Power side glows through
live Gameplay components instead of prefab hierarchy paths. The v0.4.101
log confirmed both options were enabled but neither path found its target.
Version 0.4.101
fixes Sloptifine 3 visual discovery so it tolerates runtime child-component
changes. The side-glow switch now targets only the left and right branches,
leaving the separate center glow visible. A one-time warning identifies a
missing live target. These switches still need a Windows activation test.
Version 0.4.100
adds separate default-off switches under **Sloptifine 3** for the personal-best
popup and Star Power side glows during active offline solo Quickplay songs.
They restore on pause, online play, or scene exit. The Star Power controller,
scoring, and note path stay active. Their FPS effect is not yet measured.
Version 0.4.99
moves the optional TMP label scan out of the first playable frames when the
setting is already enabled, and confines mouse UI/legacy-event suppression to
offline solo play. The PresentMon report tool can show one-second presented
FPS windows from raw captures. These changes have no measured FPS gain yet.
Version 0.4.98
avoids changing Unity's worker count for brief song-start states, stops
retrying an optional TMP experiment when the scene has no eligible labels,
and skips empty mod GUI events while its Canvas counter is showing. These
changes have not been measured as a presented-FPS gain. Version 0.4.97
extends the default-off gameplay UI-event pause to suspend the three built-in
HUD drag-handle Canvases while their only Images are transparent. They restore
when a handle becomes visible or the UI pause ends. The supplied scene has
three enabled world-space drag-handle Canvases with transparent Images; FPS
impact is not yet measured. The SpriteAttach cache remains optional and is now
labeled as potentially slower because its IL2CPP checks can exceed the small
native update it skips. Version 0.4.96
extends the default-off hidden-leaderboard switch to pause its dedicated
Canvas, scaler, and raycaster when the list is fully invisible offline. It
restores their prior states on reveal or online play. The game keeps its
leaderboard objects and fade logic active; the FPS effect is unmeasured.
Version 0.4.95
extends the same default-off hidden-leaderboard switch to suspend the 30
score-list `Image` components (14 initially active in the scene hierarchy)
while the entire list is alpha zero offline.
It restores each image's prior enabled state when the list reappears or online
play starts. This may reduce hidden canvas work; its FPS effect is unmeasured.
Version 0.4.94
extends the default-off hidden-leaderboard option to pause the score list's
TMP labels while it is fully invisible offline. It restores each label's
original enabled state when the list appears or online play starts. The
supplied scene contains 24 such labels; the FPS effect is unmeasured.
Version 0.4.93
fixes the optional SpriteAttach cache so a skipped update does not recapture
the same camera and transform state. The opted-in hook now stays dormant across
pause/resume instead of being repatched, and three experiments share one
offline-song state read. These changes have no measured FPS result yet.
Version 0.4.92
adds a default-off black-background experiment that skips the full-screen
background camera during offline solo songs, transferring its screen clear to
the guitar camera and restoring the original camera settings on pause, menus,
focus loss, scene changes, or when disabled. Chart images and videos stay hidden
while the option is active. The option has logged activation/restoration in
game; its visual behavior and FPS impact are not yet verified. Version 0.4.91
adds two default-off offline-solo
experiments: pause Rewired's mouse-only snapshot while preserving keyboard,
controller, and 1000 Hz instrument input; and cache `SpriteAttach.Update` only
while its camera, viewport, sprite, scaling inputs, and output transforms stay
unchanged. The cache is limited to the supplied exact game build and fails open
to vanilla updates. These prototypes may have little or negative performance
impact until tested in-game; test one at a time before combining them. Version
0.4.90 keeps the TMP scale-update experiment default-off and broadens it to
labels under Animator/Animation parents. Ordinary labels are checked at 4 Hz
and animated-parent labels at 20 Hz; a detected scale change restores TMP's
per-frame scale checks for that label. TMP layout, graphics, and culling
updates remain active. Scale animation could soften text briefly before the
next check; this prototype has no measured FPS gain yet. The one-time TMP
discovery also skips result sorting. Version 0.4.89 adds a separate default-off
Sloptifine switch for Unity's legacy camera mouse-event dispatch. It detours the
exact supplied GameAssembly build and skips `OnMouse*` callbacks only during
active chart play; vanilla callbacks return on pause, menus, settings, focus
loss, scene transitions, and unload. It is independent of the EventSystem
mouse-UI raycast switch and leaves Rewired/controller/guitar polling alone.
Other mods may rely on legacy callbacks, and this native hook was verified
active and restored in the supplied Windows Latest.log; no FPS gain is claimed
without a matched capture. Version 0.4.88 adds a default-off TMP scale-update
experiment to Sloptifine 2. During offline solo play it marks only stable,
unanimated Gameplay labels static, skipping their per-frame SDF scale checks
while leaving TMP layout, graphics, and culling updates active. It restores on
pause, focus loss, menus/editor, online play, or scene exit. Version 0.4.87
resolves configured joystick bindings once per second instead of enumerating
the joystick list on every mod update. It leaves Rewired's polling rate and
the game's instrument input untouched. Version 0.4.86
uses the existing default-off UI-event pause to disable the Unity EventSystem
component during eligible solo songs, removing its per-frame update dispatch
as well as mouse raycasts. It restores the component and prior UI selection on
pause, menus, and scene changes; if the live component cannot be resolved, the
existing Update-prefix behavior remains as a fallback. This is based on the
measured UI path, but has no matched FPS result yet. Version 0.4.85
keeps the default-off Unity worker limit active across window focus changes
during eligible offline solo play, avoiding repeated native worker-pool
reconfiguration when alt-tabbing. It adds a separate default-off drum MIDI
shutdown option that uses Clone Hero's own input-device cleanup path and
requires a restart to restore; the existing live option only pauses device
discovery. Guitar polling, online play, and note timing stay untouched, and no
FPS gain is claimed without a matched capture. Version 0.4.84
extends the default-off hidden leaderboard switch to pause six invisible
leaderboard layout scripts and the offline spectator-count callback. They
restore when the leaderboard appears, the game goes online, the option turns
off, or the scene ends. It also skips the empty FC-ring updater when custom
ring colors are off. These are CPU-work reductions; no FPS gain is claimed
without a matched in-game comparison. Version 0.4.83
moves engine-level performance switches onto **Sloptifine 2** so the
packed Sloptifine page has room and its controls stay comfortably inside the
menu. It also reduces repeated player-effect hierarchy discovery from every
five seconds to every 30 seconds, while immediately discovering newly added
player necks. Version 0.4.82 added a default-off Unity Job System experiment:
it lowers the active worker
limit by one only during focused, unpaused, offline solo gameplay and restores
the previous limit on pause, menus, online play, scene changes, and unload. It
uses Unity's generated `SetJobQueueMaximumActiveThreadCount` API; no direct
native detour is used. The first mod-timing A/B showed no callback-rate gain;
there is no PresentMon FPS comparison yet, so leave it off unless testing.
The counter HUD also reuses the existing 20 Hz network-state cache instead of
performing its own duplicate IL2CPP lookup at each stats refresh.
Version 0.4.81 shares the 20 Hz online-state check across the offline-only optimizations, the
hidden leaderboard, and restart/practice eligibility. The retained counter HUD
also skips its Unity object synchronization when its content and appearance are
unchanged. Version 0.4.80
reuses one offline-solo state check for the optional solo-only performance
switches and avoids reading optional gameplay state when those switches are
off. Version 0.4.79
pauses only `CheatManager.Update` during eligible solo songs instead of toggling
the component's enabled state; this avoids depending on a live singleton lookup
and preserves its lifecycle state. It also folds hidden-leaderboard polling
into the existing 20 Hz override pass. Version 0.4.78
uses the exact whammy byte-writer to mark texture data changes, then lets the
matching upload through once per change. Unchanged frames skip the whole game
upload wrapper; there is no per-frame stopwatch check or general Unity texture
hook. Version 0.4.77 first moved the cap to the game-specific wrapper. Version
0.4.76 removed the zero-match whammy pixel-write hooks after current-game
disassembly showed that whammy data is written through a raw byte buffer.
Version 0.4.75 added a zero-allocation
counter for mouse UI calls actually bypassed by the raycast experiment,
reported at shutdown so its engagement is visible in the normal log. Version
0.4.74 added a 1×24 CPU pixel-write gate based on the older source. Current
game disassembly shows the whammy path writes through `NativeArray<byte>`; the
hook saw no matches and was removed in 0.4.76. Version 0.4.73
fixes the mouse-raycast experiment to intercept the integer overload that
Clone Hero actually calls during gameplay; the old wrapper hook only appeared
installed and did not stop the measured path. Version 0.4.72 checks the two
optional visual-hook preferences at 20 Hz rather than on every simulation
update; in-game and profile changes apply within 50 ms. Version 0.4.71
selects the local online counter source by its Rewired controller reference,
instead of a transient remote-combo flag, and immediately hides the counter HUD
when its master switch is turned off. It also logs a one-time local-player
selection message. It leaves audio, note timing, and controller polling alone.
Version 0.4.70 installs the FC ring color hooks only during Gameplay while
custom ring colors are enabled, removing those Harmony callbacks from the
game's setter path otherwise. No performance gain is claimed without a matched
capture.
Version 0.4.69 adds a default-off solo-play experiment that pauses the game's highway,
background-video, and track-fade update while a chart is active. It is visual
only and may freeze those animations; no performance gain is claimed yet.
Version 0.4.68 added live state reporting for the cheat-scanner pause option,
and 0.4.67 added a separate default-off switch that suspends Unity UI events
during active solo songs, resuming on pause. The cheat-scanner switch also
pauses its persistent per-tick scan during solo songs. The whammy upload cap
is default-off and scoped to Gameplay. GPU compute rendering remains
untouched. Earlier versions also added a separate option to suppress the
star-progress particle burst. The star bar, score, note timing, and 1000 Hz
instrument polling remain untouched. When UI event processing is enabled,
keyboard/controller UI navigation pauses during the song and returns on pause.
See the [performance audit](docs/performance-audit.md) for evidence and limits.

## Install

[Download SlopHero v0.4.111 alpha](https://raw.githubusercontent.com/joechillwrld/slophero/slophero-v1.1-alpha/downloads/SlopHero-0.4.111-alpha.zip)

1. Close Clone Hero. When upgrading, replace the existing `Mods/SlopHero.dll`;
   keep only one copy of the mod.
2. Copy `SlopHero.dll` into the game's `Mods` folder, alongside ClonZones.
   The release ZIP contains `Mods/SlopHero.dll`; extract it into the game folder.
3. Start Clone Hero normally with your existing MelonLoader installation.
4. Play a solo guitar song in Quickplay. The HUD appears near the upper left.

No extra loader installation, replacement game files, or supplied game DLLs
are needed. Remove `Mods/SlopHero.dll` to uninstall. Preferences can remain.

## Controls

| Control | Default |
| --- | --- |
| Restart full song | Hold **R** for **0.75 seconds** |
| Switch to practice | Hold **P** for **0.75 seconds** |
| Open/close SlopHero settings | **F8** |
| Close/cancel settings | **Escape** |

Pause the game before editing settings. Press **F8** for the settings panel.

On the game's pre-song menu, highlight **Song Speed** and type a whole percentage
from 10 to 5000. Each digit appears on the game's speed row as you type; a trailing
`*` marks a pending value. It applies after a short pause or when you press Enter.
Backspace removes the last digit; Delete clears the entry; Escape cancels the edit. Use the
game's normal left/right controls to change the value by the configured step (5%
by default). Set that step on the SlopHero **Controls** page.

F8 has seven pages: **General**, **Counters**, **HUD**, **Visuals**, **Colors**, **Profiles**, and **Performance**. Long HUD and Performance pages scroll. The centered window uses a left category sidebar and a separate options panel. Boolean options use switches throughout. The **Menu accent** color on the Colors page applies to the title bar, selected page, section marks, and active switches; it is included in color themes.

- Click a numeric or color value and type a replacement. Enter or **Apply** saves;
  Escape or **Cancel** discards. Ctrl+A selects the whole value, Ctrl+V pastes.
  Colors accept six hex digits with an optional `#`, for example `FF8800`.
  Percentage inputs use 0–100; out-of-range numbers are clamped.
  Each color has a swatch in the menu. Clicking the color value opens hex input and red,
  green, and blue sliders with a larger preview. **Apply** commits the draft;
  **Cancel** leaves the saved color unchanged.
- Counters: individually show/hide misses, hit percentage, ghost inputs, and
  best streak. **Best streak** uses the game's highest streak for the current attempt.
- Song HUD: chart title, artist, charter, and live chart speed are separate toggleable lines.
  It has independent position, text size, color, and background opacity.
- Game HUD: move and resize the game's practice stats bar. Move and resize the FPS
  row, and recolor its actual FPS number. FPS must first be enabled
  in Clone Hero's own settings. Both positions can also be adjusted with numeric offsets.
- Profiles: use the dropdown to switch profiles or rename one from its row, save
  full settings or color-only themes, or edit JSON files. Files live in `UserData/SlopHero/Profiles`; refresh the page after
  adding or editing files there. A theme contains only color-related options, so
  applying one leaves your layout and gameplay options intact.
  The menu accent can use Classic, Ocean, Sage, or Amber; it only colors the
  active page tab, and its preview swatch shows the selected accent.
- The counter and song HUDs have **Move this HUD**: drag and release to save. F8 returns to
  settings; Escape finishes. Coordinates can also be entered directly.
- Highway: optionally hide sides, fret strings, or beatlines. These options
  affect renderers, preserving gameplay objects and practice initialization.
- Colors: enable custom FC/perfect-FC rings or score colors, then enter colors
  for rings, stars, score/accuracy text, star progress, and song progress bars.
  Ring color switches from perfect FC to regular FC when the guitar's native
  ghost-input counter becomes nonzero. Native ring visibility is preserved.
- **Constant practice note speed** keeps note rendering, beatlines, and
  scrolling highway textures at the configured profile values through practice
  tempo changes. It is experimental and **off by default**. It does not alter
  audio, chart timing, or hit judgments. Animated
  video highways are not retimed. Test at 50%, 100%, and above 100%, and toggle
  it off to compare vanilla behavior.

Changes persist in `UserData/MelonPreferences.cfg` under `SlopHero`. Visual
options default off. Turning them off restores the properties captured before
SlopHero applied the override. Other mods that edit the same renderer properties
may compete with these options.

Click a binding button, then press a
keyboard key or a guitar/controller button. Controller buttons are read through
the game's Rewired input system. Avoid fret or strum buttons unless you intend
those presses to trigger a hold action. Bind different buttons for the two actions.
Bindings and hold duration are saved to `UserData/MelonPreferences.cfg`, under
`SlopHero`. You can also edit `HudX`, `HudY`, `FontSize`, `ShowHud`,
`BackgroundOpacity`, and `TextRed`/`TextGreen`/`TextBlue` there with the game
closed. Opacity and RGB channels use values from 0 to 1. Hold duration is clamped to 0.25–3 seconds.

Controller bindings store the Rewired joystick ID and button index. If device
order changes after reconnecting a controller, bind its button again in F8.
The restart and practice controls apply only to offline, single-player Quickplay,
outside practice and setlists. Release the button before triggering it again.

Restart calls the game's own restart handler without opening the pause menu.
It still uses the game's normal fade/reload; this build does not promise a
zero-loading-time restart.

Practice switching starts **the current named section from its beginning**, with
that single section selected. This is the chosen default for the first build.
The feature changes to practice, reloads through the game's transition, and
applies the captured section only after the new Gameplay scene, practice section
menu, and beat renderer finish initializing. The previous build called into a
renderer before it was ready. A section-selection error now leaves the HUD
running and lets you choose a section manually instead of disabling the mod. Before the
first named section, it leaves the song alone and displays a notice.

## 0.4.8 practice tempo correction

The constant practice speed option measures how quickly the gameplay song clock
advances against real time, then applies the inverse rate to note, beatline, and
highway renderer speeds. The game’s displayed practice percentage is logged for
comparison but is no longer assumed to equal that measured rate. This supports
any configured note speed and avoids guessing a multiplier from one percentage.

## 0.4.2 full-song speed reset

After a gameplay scene reload, if Clone Hero retains renderer values from a
practice session, SlopHero now reconciles note, beatline, and highway speeds
with the configured profile speed before full-song play. It inversely adjusts
the note lookahead window to preserve the same visible range. A log entry is
written only when any value needs correction.

## 0.4.1 practice transition diagnostics

On every practice-mode entry and exit, `Latest.log` now records the note,
beatline, and highway scroll speeds before and after SlopHero's update. This
shows whether a slowdown comes from the base setting, stale renderer values,
or the tempo compensation. Practice rendering target logs remain in place.

## 0.4.0 performance hot-path changes

Removed the low-overhead HUD refresh option. It was unrelated to Clone Hero's
frame performance and is not part of Performance mode. Constant practice note
speed now exits before reading the player's speed setting during full-song play;
that data and renderer work is only needed in practice or while restoring a
previous override. FC ring checks run at 10 Hz rather than every frame. The
game's Harmony callbacks still update a changed ring immediately, and ring
discovery remains at 2 Hz. Timing reports separate FC rings and other visual
overrides to guide further profiling.

## 0.4.9 idle-work reduction

The supplied log confirms that the game and both mods loaded, but it has no
frame-time capture. This build removes avoidable SlopHero work from idle frames:
it skips visual callbacks when all related options are off and no prior changes
need restoring, samples practice time only when constant-speed compensation is
enabled, and scans the IL2CPP player list at the HUD's 5 Hz refresh rate instead
of every frame. These changes reduce mod overhead; they cannot establish a whole-
game FPS improvement without an in-game comparison using the same chart/settings.

## 0.4.10 custom star-color optimization

The timing capture showed visual overrides as the largest SlopHero callback.
Custom star coloring now caches which child sprites are star graphics instead
of walking their transform ancestry every frame. The cache resets with each
gameplay scene so replacement UI objects are discovered normally.

## 0.4.11 log cleanup

Removed resolved practice-speed and FC-shader diagnostic traces, along with
scene readiness chatter. Action notices, failure details, and the opt-in timing
capture remain available for support and performance checks.

## 0.4.12 visual timing detail

The opt-in 30-second timing report now separates the player/speed override work
from score graphics work inside the visual callback. These measurements are
reported only when timing capture is started from the settings menu.

## 0.4.13 score-color refresh

Custom score, star, and progress colors are checked at up to 60 Hz. This keeps
color changes responsive while avoiding repeated renderer scans at unlocked
frame rates. Practice speed and FC ring updates keep their existing cadence.

## 0.4.14 native HUD controls and idle work

Added position controls for the native practice stats bar and FPS display, plus
optional FPS text coloring. Native HUD objects are only polled when customization
is configured or being edited. Counter and song metadata sampling stops when both
SlopHero HUDs are hidden.

## 0.4.15 settings cleanup and FPS row

Fixed a missed score-color restore that could keep visual overrides active after
turning custom colors off. Static player and score graphics are refreshed at 10 Hz;
practice speed compensation remains on the live gameplay path. FPS position targets
its shared row; text color targets the actual FPS number.

## 0.4.16 profiles and HUD repaint work

Added JSON full-profile and color-theme save/load controls to F8 → Profiles.
Profiles apply immediately, and can also be edited or added in
`UserData/SlopHero/Profiles`. HUD line-height calculation no longer allocates
temporary arrays during every repaint.

## 0.4.17 profile renaming

Added an in-game rename action for the selected full profile or color theme.
Renaming preserves its settings and refuses to overwrite another profile.

## 0.4.18 settings menu cleanup

Profile selection is now a dropdown with an inline rename control beside each
profile. The menu shows a preview for its four built-in themes, and the active
settings page is visibly selected. Spacing and instructions were tightened while
keeping the existing page structure.

## 0.4.19 menu layout and theme polish

Restored standard page buttons so tabs do not inherit checkbox styling. Menu
themes now tint only the active tab, using restrained Ocean, Sage, and Amber
accents. The profile dropdown shows four rows per page to leave more room for
the controls and status below it.

## 0.4.22 pre-song speed range

Raised the editor's maximum from 1000% to the game's normal 5000% ceiling.

## 0.4.23 native pre-song speed editing

Removed the separate speed popup. Typing digits while the game's Song Speed row
is selected now changes the game's own setting and label; the game's normal
left/right speed changes use the configurable SlopHero step. The settings panel
scales around screen center so resizing keeps it centered.

## 0.4.24 live speed entry and deletion

Typing on the selected native Song Speed row now shows each digit in that row as
it is entered. Backspace removes the last digit and Delete clears the entry; the
last valid speed remains applied until a valid number is typed. The increment
size remains a separate Controls-page setting.

## 0.4.26 stable speed entry

Clone Hero no longer supports the old 1–9% range consistently: the chart speed
changes while audio playback stays at normal speed. SlopHero now restores a
persisted sub-10% value to 10% and prevents those values from being applied.
Typed digits stay visible while being entered, then the mod applies the completed
speed once after a brief pause or on Enter. This avoids recalculating speed for
every partial number.

## 0.4.49 hidden leaderboard updates

The Sloptifine tab adds an off-by-default switch that suspends the six
leaderboard entry motion checks only while their score list is fully invisible
and Clone Hero is offline. Online score displays and visible entries retain
the game's original behavior. Full settings profiles now include all
Performance switches.

## 0.4.50 low-frequency optional-service checks

The optional MIDI-input and Discord Rich Presence switches are now serviced
at 10 Hz instead of being visited on every Unity simulation tick. That cadence
is sufficient for toggles and service discovery; it leaves controller input
polling and chart timing untouched.

## 0.4.51 lower-frequency idle game-state reads

When no restart/practice shortcut is held, repeated IL2CPP game-state reads
now run at 20 Hz; HUD counters remain on their existing 5 Hz refresh. Shortcut
bindings are still polled on active gameplay updates, and a held shortcut
continues to receive live eligibility checks. Practice-transition readiness
may be recognized up to 50 ms later.

## 0.4.52 optional Canvas counter HUD and visual snapshot

**F8 → Sloptifine → Try Canvas renderer for counters** is off by default.
When on, a separate, noninteractive Unity Canvas displays the counter HUD
during gameplay. Counter values retain their 5 Hz refresh; the Canvas receives
position, color, opacity, and text changes at most 20 times per second. The
normal HUD renderer handles moving and resizing. Switching the option off
removes the Canvas on the next visual refresh, and a UI-wrapper failure restores the original
renderer for that session. The song HUD is unchanged. Canvas GPU/rendering
cost is not included in SlopHero's callback timing bucket, so a lower HUD
callback time alone does not establish better FPS.

An opt-in 30-second timing capture now also logs active counts for visual
scripts, SpriteRenderers, cameras, canvases, animators, video players, and
particle systems. Those counts guide future game-side changes; they do not
disable any native game components.

## 0.4.58 April Fools suppression and beatline-update optimization

The new default-on **Disable April Fools / NoteBomber chaos and Pong** switch
forces the current game's shared joke-mode flags off and deactivates active
`PongersGame` objects without destroying them. Turning it off restores the
state the game had before SlopHero suppressed it; shutdown also restores it.
The current generated assembly has no `GlobalVariables.isAprilFools` field,
so this uses its present `BaseMenu.HAVEFUN`, `GlobalVariables.isNoteBomber`, and
`PongersGame` hooks. The game uses `HAVEFUN` for related NoteBomber effects, so
those are suppressed too while the option is on.

The optional **Skip beatline updates while hidden** switch avoids a per-frame
beat scan only when beatlines are hidden. It remains off by default and can
suppress beat-triggered effects if the April Fools switch is turned off.

## Beatline update optimization

**F8 → Sloptifine → Skip beatline updates while hidden** is off by default and
requires **Hide beatlines** to be enabled. It suspends the beatline renderer
while its visuals are hidden, then restores its original enabled state when
the switch is turned off or the scene changes. The older source reference
shows that this renderer performs per-frame beat scans and transform/color
updates; it also drives April Fools beat-triggered effects. If April Fools
suppression is turned off, this optimization can suppress those effects too.
This is a code-path reduction awaiting an in-game comparison, not a measured
FPS claim.

The older source dump and current-version verification limits are recorded in
the [performance audit](docs/performance-audit.md).

## 0.4.57 settings sidebar and performance follow-up

The settings window now uses a left category rail and a separate right-side
options area. Its purple accent can be edited on the Colors page, shown in the
color preview, and saved with color themes. The color is applied to the title
bar, selected page, and resize outlines. The window remains centered and its
checkboxes and other menu controls are only drawn while F8 is open.

The hidden-offline-leaderboard option now checks visibility at 20 Hz rather
than 60 Hz, reducing repeated network, canvas, and six-entry checks; the
entries restore within 50 ms when the list appears. The combo-smoke discovery
also no longer requires Clone Hero's `inputEnabled` flag during transitions,
and it checks the parent by IL2CPP object pointer. The 0.4.56 capture showed
the two live smoke scrollers still enabled with this option on, so this target
fix is based on that live-path mismatch. The new log's main callback percentiles
were close to the prior run but slightly slower; isolated 2–5 ms HUD/Canvas
callback spikes need another capture to determine whether they recur.

## 0.4.56 optional effect-update reductions and menu cleanup

Every Boolean setting now uses a checkbox instead of an On/Off button. The
Sloptifine page groups visual effects separately from services and HUD options.
These controls render only while the settings panel is open; no overlay or
menu work is added to the normal gameplay update loop.

The **Fret flames/sparks** Sloptifine switch now also pauses the ten
`CustomFadeProperties` components observed on the guitar's `Fret_*/lightning`
and `lightning_white` objects. They do no visible work while those effects are
hidden. Their enabled states restore with the existing switch; note timing,
scoring, and input are unaffected. This builds on the exact live paths and
switch state in the supplied capture.

**F8 → Sloptifine → Pause combo smoke scrolling** is off by default. When
enabled, SlopHero targets only `smoke_combo_fast` and `smoke_combo_slow` beneath
the local guitar's `Combo/multiplier` object. It pauses their `UVScroll`
components while leaving the smoke renderers visible; remote players are not
targeted. It restores original component states when disabled, on scene
changes, and during mod shutdown. Discovery repeats at a two-second interval
while enabled so practice transitions that recreate player visuals are
covered without searching the hierarchy every frame.

The 0.4.53 option searched below `HUDCam/Menu Canvas/Lyrics`, but the supplied
live log showed both UV components under the guitar combo smoke instead. That
release's switch therefore matched nothing. The same log's off/on captures
cannot measure this optimization: the old switch was a no-op, and the runs had
different durations and callback cadence. The captures show stable-looking
callback percentiles in both runs, but do not isolate the UV scripts' native
cost or measure GPU presentation.

## 0.4.31 Performance options

The original Performance tab offered five independent switches, all off by default:
note-hit particles, sustain particles, fret flames/sparks/lightning, drum MIDI
input polling, and Discord Rich Presence. The effect switches deactivate their
effect objects to stop their simulation, rather than only hiding their drawing.
The original MIDI switch pauses periodic device discovery and restores it when
switched off. Version 0.4.85 adds a separate default-off drum MIDI shutdown
that closes connected devices; switch it off and restart to restore MIDI.
Discord Rich Presence shuts down for the current session; switch it off and
restart the game to restore it. Online play,
leaderboards, scoring, notes, timing, and all SlopHero features are preserved.

## 0.4.34 sizing menu and star background

The HUD picker uses individual buttons instead of Unity's array-based selection
grid, whose IL2CPP wrapper throws `Method unstripping failed` in v1.1.0.6142.
Two navigation rows keep all page titles visible. The page can resize the
score box, input, star-power, and practice-stats HUDs. FPS row resizing is on
the Game HUD page. Target outlines use screen-projected UI and sprite bounds,
cached at 20 Hz while the editor is open. Corner scaling keeps a fixed drag
origin so refreshed bounds do not shift the resize ratio mid-gesture. Star graphics exclude the
`starCountBG` renderer, so the empty progress track keeps its original color.
The 20 Hz visual-dispatch gate now runs before preference and `GameManager`
reads, removing those repeated checks from unlocked update frames.

## 0.4.35 resize editor rendering

The latest log showed `NotSupportedException: Method unstripping failed` from
`DrawResizeHandles`. `GUI.DrawTexture` is not available through this game's
IL2CPP wrapper, so the selection outline and corner handles now use the
supported `GUI.Box(Rect, string)` call. They are rendered only while a resize
target is actively being edited.

## 0.4.36 resize targeting and interaction

The resize grips are larger and more visible, and dragging inside a native HUD
resizes it for easier access when its corners overlap other gameplay elements.
Score, combo, and accuracy text share one sizing control and one combined
selection outline. Empty native-component scans retry during sizing to catch
HUD objects created after gameplay initialization. The log records one bounds
result per selected target and the saved size at the end of a drag.

## 0.4.37 resize bounds and score-box grouping

The v0.4.36 log exposed invalid bounds from off-camera renderers, producing
resize rectangles tens of thousands of pixels off-screen. Bounds now discard
behind-camera and off-screen points and include only bounds projected near the viewport. Score,
combo, accuracy, and star progress share one target and one scale setting; old
star-progress scaling is folded into the score-box scale during upgrade.

## 0.4.41 practice and FPS bounds

The v0.4.40 log confirmed that the practice panel was active, 310×200 pixels,
and in practice mode, yet the editor reported no bounds. The game's
`RectTransform.GetWorldCorners` wrapper takes an `Il2CppStructArray<Vector3>`;
passing a managed array filled a temporary copy. The editor now reuses an
IL2CPP array and reads the filled corners. The same correction covers FPS
bounds and UI-graphic fallback bounds.

## 0.4.40 resize editor and practice panel

The score-box outline uses the backgrounds of the score, combo, star-progress,
and song-progress graphics plus the star face. Leaderboard accuracy text is a
different HUD element and no longer stretches the score outline or scales with
the score box. Dragging the interior never resizes; corners resize, and the
interior moves counters, song info, practice stats, and FPS. Practice stats can
be moved and resized even when the panel is present before `songLoaded` becomes
true. Practice Move and Resize controls are together on Game HUD.

## 0.4.39 native HUD target correction

The supplied gameplay scene places score, combo, star, and song progress under
`HUDCam/HUD`, and the input viewer under `HUDCam/Input Viewer`. There is no
`MainCamera` tag, so the previous outlines could not project their bounds.
Score and input sizing now use those visible shared parents. The FPS readout is
one `FPS` object with a `FrameRate` component and one TextMeshProUGUI label;
the previous search of its parent canvas could pick unrelated menu text.
The outline and handles still draw only while resize mode is active.

## 0.4.38 native HUD target attempt

This release grouped score elements and moved FPS sizing controls to Game HUD,
but its bounds and FPS object lookups did not match the gameplay scene.

## 0.4.32 override dispatch

The latest timing capture showed visual hooks being entered at 2,300–2,600
calls per second even though their internal work refreshes at 5–20 Hz. Native
HUD and visual-override dispatch now runs at 20 Hz, preserving the practice
clock sampler's 20 Hz cadence. Timing reports also record the Performance
switch states so later captures can be compared accurately.

## 0.4.30 HUD rendering

The custom counters and song-info HUD now combine their background and text in
one IMGUI control, removing a separate GUI control and label layout pass from
each visible HUD repaint.

## 0.4.29 idle HUD update

The mod skips `OnGUI` work when the settings, notices, and HUDs are all hidden.
Counter sampling now runs only when visible counter fields need it. Hiding the
counter HUD while keeping song info enabled no longer scans the chart or player.

## 0.4.28 runtime and diagnostic update

The 30-second capture now includes Unity's unscaled frame-time p50/p95/p99/max
and logs both SlopHero HUD positions and native HUD offsets. This helps separate
game-wide frame pacing from the time spent inside SlopHero callbacks. The
gameplay loop also caches parsed hotkey bindings instead of splitting and
validating their strings every frame, and only requests practice-speed
compensation passes while practice is active (plus cleanup after it ends).

## 0.4.27 gameplay update load

Practice note-speed compensation samples the playback clock at 20 Hz and updates
renderers at 10 Hz instead of scanning and rewriting them at unlocked frame rate.
Cosmetic score and star colors refresh at 5 Hz. The supplied timing capture showed
very low average SlopHero callback time but isolated long frames, so this release
reduces avoidable recurring work; verify in-game before treating the stutter as
resolved.

## 0.4.21 pre-song speed editing

When the pre-song **Song Speed** option is highlighted, SlopHero shows an editor
for the game's profile speed setting. Enter and apply a whole percentage from 1
to 5000, or use the minus/plus buttons with a configurable step size (5% by
default). Writes go through the game's setting update method so native setting
callbacks run. The editor is only shown while that option is selected.

## 0.4.20 sizing and progress color

The settings panel is larger by default and can be resized. The Sizing page can
scale score/combo, the input viewer, star-power meter, star-rating progress,
song-progress bar, practice stats, and FPS row. Each game HUD group restores its
original scale when returned to 1.0× or when the gameplay scene changes. Custom
star-progress color follows the fill while preserving the empty track. The
menu-theme selector was removed; saved profiles still support named color themes.

## 0.3.9 profiling and renderer update

Performance no longer duplicated Clone Hero's fret, flame, or particle controls.
The timing capture reports FC rings and other visual overrides separately.
Custom color overrides skip redundant renderer writes when the target RGB is
already applied. This optimization preserves native alpha/fades.

## 0.3.8 performance update

The supplied timing capture had Performance mode enabled and measured the mod's
`Visuals` callback at 0.055 ms average (0.008 ms in the earlier capture). The
cause was repeated traversal of cached effect lists and redundant Unity state
calls each frame. Performance overrides now apply when enabled or when an
option changes, then return immediately on subsequent frames. They still
restore affected objects when disabled or when the scene changes.

The HUD refresh limiter now appears only once, on **F8 → Performance**. It is
independent of Performance mode and retains the existing `LowOverheadHud`
preference. The provided timing measures mod callbacks rather than the full
game CPU/GPU workload, so a new capture is needed to confirm the callback cost
after this change and does not by itself establish whole-game FPS improvement.

## 0.3.7 performance controls and star HUD targeting

**F8 → Performance** previously exposed effect removals now understood to
overlap with in-game controls. Those controls were removed in v0.3.9. The
renderer inventory diagnostics from this release identified the star graphic
as `star_inner[score_star_outline_gold]`.

The star tint now covers graphic renderers on all active StarProgress HUDs,
excluding the numeric count and progress-bar renderers. The latest log also
records each star HUD renderer and its sprite name once, so an incorrect target
can be identified without guessing from field names.

## 0.3.6 practice speed transition handling

On entering practice again, cached speed and note-visibility state is cleared
so compensation starts from that session's live renderer values. The supplied
log showed the feature targeting 18.4 from a configured speed of 23 at 125%
tempo, with no logged exception. The repeated-practice speed drop was not
reproduced from the log, so the reset needs a Windows check after this update.

The previous star tint change did not resolve the visible star in the user's
install. This release broadens the target and includes renderer diagnostics.
The practice transition issue was not reproduced in the attached log; the speed
cache reset remains in place.

## Earlier 0.3.5 changes

The song HUD appends the live chart speed to the title, for example
**Song Title (150%)**. It follows practice tempo changes. Toggle the suffix with
**F8 → Song HUD → Speed beside title**, or `ShowChartSpeed` in preferences.
Native floating-point noise is rounded away; fractional percentages remain
supported. The suffix is hidden when the title itself is disabled.

Custom perfect-FC and regular-FC ring colors now follow the hue state Clone Hero
writes to the guitar ring. The ghost-input count no longer determines which
color is used, since it is independent from the ring state. This native-state
mapping needs confirmation in Windows gameplay, including runs with ghost fret
presses and an actual broken perfect FC.

The latest log confirms the repaired speed reader: configured/native speed 23,
with a target of approximately 15.333 at 150% tempo. That implementation is
retained. FC hue is reported broadly correct; the remaining difference is
lighter/darker appearance. The ring retains its texture shading and native
material, so it need not match a solid UI swatch pixel for pixel. Color-space
conversion is another possibility; its contribution is not established yet.
This release logs the active color space, shader name, selected RGB/HSV, and
original vertex color once per registered ring when custom colors are enabled.
It uses shared material references without cloning or changing materials.
The working hue/saturation path is unchanged; no brightness correction is
applied without evidence from the actual shader/rendering setup.

The latest reported FPS ranges (off 950–980, on 850–1000) overlap. Callback
averages were Update 0.009ms in both runs, Visuals 0.008ms in both, and HUD repaint
0.027ms versus 0.021ms. Reduced callback averages do not establish a game FPS gain.
The two-Hz HUD option remains optional and off by default.

## 0.3.3 disappearing-note cause

The 0.3.2 log recorded `profile=1.3446374E+09`, `nativeSpeed=23`, and
`targetSpeed=689557400` at 195% tempo. The generated generic field accessor
returned a pointer-like magnitude rather than the configured note-speed int.
Such a speed makes notes pass through the viewport almost immediately; the
previous visibility-window changes did not address this cause.

The reader now follows the exact concrete pointer layout used by the native
renderer: player data → profile (`0x10`) → setting (`0x20`) → raw int (`0x10`).
It is read only and used only after the build fingerprint matches. Configuration
values outside 1–1000 are rejected; missing/invalid values skip compensation.
Effective speeds above 10000 or nonfinite are also rejected. These deliberately
broad bounds catch corruption without constraining ordinary note-speed settings.
The initialized lookahead now scales to maintain the same world-space visibility
range, avoiding excess offscreen lookahead at slower practice tempos.

A synthetic native-memory layout check covers reading 23, a live edit to 31,
null pointers, and the observed corrupt magnitude. Windows verification is still
needed: at the logged 195% tempo, a configured speed of 23 should give an
approximately 11.79 chart-time visual speed, not hundreds of millions.

The FPS ranges reported with low-overhead HUD off (800–950) and on (~790–980)
overlap. Callback averages were Update 0.009ms in both runs, Visuals 0.005ms in
both, and HUD repaint 0.029ms versus 0.024ms. These measurements do not establish
an FPS improvement. HUD repaint is the largest average measured mod callback,
so this release avoids reapplying unchanged font sizes, word wrap, and text
colors on each repaint. Repeat timing captures can evaluate that change; broader
CPU/GPU profiling is still needed before removing game systems.

## 0.3.2 note visibility and mod overhead

The speed fix previously recomputed the note spawn/lookahead window from live
clip-plane values. This release uses the renderer's initialized native window
instead, preserves at least that much lookahead, and rejects zero, negative, or
nonfinite windows/speeds. A slower visual speed can expand the window. Disabling
the toggle restores the saved window. Runtime speed/window values are written
once per renderer to `Latest.log` to diagnose the disappearing-note report.
This is a guarded repair, not a claim of an in-game fix verified on Windows.

Automatic overhead reductions:

- FC shader overrides react to native color changes and selected-color changes
  rather than rewriting unchanged shader colors every frame. Component discovery
  runs at most twice per second.
- Hidden sides, strings, and beatlines are configured when toggles/player resources
  change, avoiding repeated per-frame arrays/scans and redundant renderer writes.
- Disabled score colors skip their renderer scans. Parsed colors and HUD background
  style are cached. Passive HUDs draw only for repaint events; editor controls
  still receive their normal input events.
- Practice counters binary-search the sorted chart to the start tick after resets,
  rather than scanning every earlier head again. Boundary chord heads are included.

**F8 → Performance → Low overhead HUD** (removed in v0.4.0) reduced counter refresh from
five to two updates per second. The restart/practice holds still update each
frame. This reduces mod work; it does not disable game systems or existing
vanilla effects.

**F8 → Controls → Capture mod timings for 30 seconds** writes callback averages,
maximums, call counts, observed update rate, Unity unscaled frame-time
percentiles, per-camera render-phase averages, and HUD positions to `Latest.log`.
For each camera, pre-cull to pre-render spans culling and intervening camera
callbacks; pre-render to post-render spans rendering work and callbacks. These
are CPU callback intervals, not isolated native culling cost or GPU presentation.
Start during a song, close F8,
and play the same passage. Keep chart, tempo, visual settings, and other mods
constant; the capture records the HUD positions so placement comparisons can be
checked directly. Timing capture ends automatically or with **Finish timing
report**. It is off normally and does not log every frame. Callback timings
exclude most of the game's CPU/GPU workload; Unity frame times add frame-pacing
context but are not a substitute for an external CPU/GPU frame-time capture.

## 0.3.1 speed and ring repairs

Constant practice speed now derives its baseline from the player's configured
note-speed setting each frame, rather than whatever value happens to be on the
renderer when toggled. Enabling it after changing tempo should give the same
visual speed as enabling it first. The setting is also followed if you change
your profile's note speed while compensation is active.

The FC ring uses shader hue/saturation overrides, so sprite tint alone could
revert visually after native FC state changes. SlopHero now applies the selected
hue/saturation and brightness, and uses scoped Harmony postfixes on the game's
ring-color setters to reapply it immediately. Normal visibility, alpha, and FC
eligibility stay under game control. Disabling the override restores the latest
observed vanilla shader state. Custom shaders from other mods need testing.

A game-wide performance mode is **not included**. The requested scope is unnecessary
background work or niche resources beyond existing in-game effects toggles.
Static inspection has not established a removable bottleneck or measured FPS
gain. A future mode needs Windows profiling and comparisons with matching
charts, practice tempo, camera, and settings before choosing what to bypass.

## 0.2.2 practice input repair

After selecting the captured section, the shortcut now completes the same
sequence as the game's practice confirmation: reset the attempt, enable local
player input, and clear the pause/restart flag. This addresses the need to use
the keyboard before the guitar starts responding. The scene-readiness checks
and working HUD editor are retained.

## 0.2.1 rendering repair

The supplied Unity assemblies route `GUI.DrawTexture` to a stub that throws
`Method unstripping failed`. That background call in 0.2.0 stopped drawing
before the counters and menu, while F8 still toggled the invisible editor state.
Version 0.2.1 uses the available native-backed `GUI.Box` renderer instead.
Background opacity and text color controls are retained.

HUD appearance rendering, settings rendering, and song controls have separate
error handling. If appearance rendering fails, basic counters remain available.
If menu rendering fails, the editor closes so it cannot silently block song
controls. Each rendering-error category logs once per session instead of flooding
`Latest.log` every frame. The working practice transition was not changed.

## HUD

- **Misses:** judged chart heads minus the game's native hit count, using the
  results-screen counting rule for disjoint chord children and late hits.
- **Hit:** `hits / (hits + misses) * 100` for resolved notes in the current run.
  Before any note resolves it shows an em dash. Extra fret inputs do not reduce
  this percentage.
- **Best streak:** the native highest combo of this run or practice attempt.
- **Ghosts:** the game's live **Ghost Inputs** counter, which feeds the guitar
  results screen. This is separate from overstrums and drum ghost-note hits.

Counters normally refresh up to five times per second, with a brief faster
refresh when playback ends. They reset when the engine, chart,
practice range, or song attempt changes. Online, SlopHero selects the locally
controlled guitar and hides the counter HUD if it cannot identify one. Solo
play retains the first enabled guitar as a fallback; per-player multiplayer
overlays and drum HUDs are outside this build.

## First in-game check

Version 0.3.9 compiles without warnings. Earlier portable logic checks covered
title-speed formatting, native ring-state classification, chart-speed
percentage formatting, native floating-point noise, the observed corrupt speed,
concrete native-memory reads, and earlier gameplay logic. The new GUI calls were checked against the supplied wrappers.
`GUI.TextField` is an unstripping-failed stub, so numeric/color entry uses string
controls with IMGUI keyboard events. Windows gameplay cannot be run in this
Linux environment; the new features need the checks below.
1. Hit some notes, intentionally miss a few, and press extra frets. Compare the
   final HUD counters against the guitar results screen.
2. Tap R briefly: it should do nothing. Hold R: it should restart once. Keep
   holding through the reload: it should not restart again until released.
3. Hold P midway through a named section. Confirm practice starts that section
   from its beginning, verify the guitar responds immediately without a keyboard
   action, then check misses/ghosts across a practice restart.
4. Use F8 to bind a spare guitar button and repeat the hold checks.
5. Type an opacity and hex color, toggle individual lines, and move both HUDs.
   Verify best streak survives a miss and resets with a new attempt. Check title,
   artist, and charter against the song selection screen.
6. Enable each highway/color option and turn it off to check restoration.
7. Change practice to 50% **before** enabling constant speed, then repeat at
   150%. Change tempo while enabled and change the configured note speed.
   Both enable orders should produce the same visual speed at the same settings.
   Toggle off and confirm vanilla behavior returns.
8. Compare FC/PFC colors before and after ghost inputs, misses, and practice
   restarts; toggle colors off and check the vanilla appearance is restored.
   Ghost inputs alone should no longer switch the selected FC ring color.
9. Capture the two timing reports above while playing a repeatable passage, then
   attach Latest.log with the FPS change observed on the game's FPS display.
10. Verify the title suffix at 100%, 75%, and 150% in full-song/practice
    play, then toggle it off. Enable custom FC colors so the
    next log includes the shader/color-space diagnostics.
11. Move the HUD to another corner and adjust opacity/text color. Restart the game
    and confirm those settings were retained.
12. Enter practice, exit to the song, then enter practice again at both 100% and
    a changed tempo. Confirm note speed does not compound. Change the star color
    and verify the star graphic changes while its number follows the score text color.
13. Capture timings with the same chart and visual settings. Compare `FC rings`
    and `Visual overrides`, and include the FPS display reading with Latest.log.

If the HUD is wrong, an action fails, or the mod disables itself, attach
`MelonLoader/Latest.log` and describe the action. Include whether other mods were
loaded. Native-game failures may not produce a managed exception in that log.

The mod checks the game binary and metadata fingerprints before enabling game
calls. Another build needs a new mapping rather than bypassing that check.

## Build from source

Requires the .NET 6 SDK and your existing MelonLoader installation:

```powershell
.\build.ps1 -GamePath 'C:\Games\CloneHero'
```

The script builds and runs checks; it does not copy files into the game.
Alternatively, place your loader's `net6` and `Il2CppAssemblies` folders inside
the ignored `references/` directory and run:

```powershell
dotnet build src/SlopHero.Mod -c Release
dotnet run --project tests/SlopHero.Core.Checks -c Release
```

[Binding evidence](docs/bindings.md) records the version-specific native trace.
[ClonZones](https://github.com/arceuss/ClonZones) informed the choice of loader;
no ClonZones source has been copied. Game assemblies are local build references
and are excluded from the source/release packages.
