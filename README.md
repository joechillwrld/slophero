# SlopHero 0.4.3 alpha

A MelonLoader mod targeting the supplied **Clone Hero v1.1.0.6142 Windows x64**
build. Compiled against your installed loader and generated assemblies.
**Version 0.4.3 keeps the configured note speed during practice instead of
dividing it by tempo, which caused the slowdown.**

## Install

[Download v0.4.3 ZIP](https://github.com/joechillwrld/slophero/blob/slophero-v1.1-alpha/downloads/SlopHero-0.4.3-alpha.zip?raw=true)

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

F8 has five pages: **Controls**, **Counters**, **Song HUD**, **Highway**, and **Colors**.

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
- Both HUDs have **Move this HUD**: drag and release to save. F8 returns to
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

## 0.4.3 practice tempo correction

Timing logs showed that practice at 120% changed raw speed fields from 23 to
19.167, while disabling the option removed the slowdown. Compensation now
keeps raw renderer fields at the configured profile value; Clone Hero applies
practice tempo itself. The former division by tempo scaled twice. Visibility
windows consequently remain at their native range when speed is fixed.

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
maximums, call counts, and observed update rate to `Latest.log`. Start during a
song, close F8, and play the same passage. For comparison, capture once with low
overhead HUD off and once with it on. Keep chart, tempo, visual settings, and
other mods constant. Timing capture ends automatically or with **Finish timing
report**. It is off normally and does not log every frame. Callback timings
exclude most of the game's CPU/GPU workload; a broader performance mode still
needs profiling to choose niche removable work beyond existing settings.

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

- **Misses:** resolved chart heads marked missed, using the game's results-screen
  counting rule for disjoint chord children.
- **Hit:** `hits / (hits + misses) * 100` for resolved notes in the current run.
  Before any note resolves it shows an em dash. Extra fret inputs do not reduce
  this percentage.
- **Best streak:** the native highest combo of this run or practice attempt.
- **Ghosts:** the game's live **Ghost Inputs** counter, which feeds the guitar
  results screen. This is separate from overstrums and drum ghost-note hits.

Counters refresh up to five times per second and reset when the engine, chart,
practice range, or song attempt changes. The first enabled guitar player's HUD
is shown; per-player multiplayer overlays and drum HUDs are outside this build.

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
