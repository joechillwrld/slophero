# SlopHero 0.3.0 alpha

A MelonLoader mod targeting the supplied **Clone Hero v1.1.0.6142 Windows x64**
build. Compiled against your installed loader and generated assemblies.
**Version 0.3.0 adds editable values, a separate song HUD, best streak,
per-line visibility, highway visuals, and experimental practice-speed compensation.
The new features build successfully but still need a Windows in-game test.**

## Install

[Download v0.3.0 ZIP](https://github.com/joechillwrld/slophero/blob/slophero-v1.1-alpha/downloads/SlopHero-0.3.0-alpha.zip?raw=true)

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
- Counters: individually show/hide misses, hit percentage, ghost inputs, and
  best streak. **Best streak** uses the game's highest streak for the current attempt.
- Song HUD: chart title, artist, and charter are separate toggleable lines.
  It has independent position, text size, color, and background opacity.
- Both HUDs have **Move this HUD**: drag and release to save. F8 returns to
  settings; Escape finishes. Coordinates can also be entered directly.
- Highway: optionally hide sides, fret strings, or beatlines. These options
  affect renderers, preserving gameplay objects and practice initialization.
- Colors: enable custom FC/perfect-FC rings or score colors, then enter colors
  for rings, stars, score/accuracy text, star progress, and song progress bars.
  Ring color switches from perfect FC to regular FC when the guitar's native
  ghost-input counter becomes nonzero. Native ring visibility is preserved.
- **Constant practice note speed** compensates note rendering, beatlines, and
  scrolling highway textures for practice tempo. It is experimental and **off
  by default**. It does not alter audio, chart timing, or hit judgments. Animated
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

Version 0.3.0 compiles without warnings, and **37 portable logic checks pass**,
including tempo changes, restoration, and protection against repeated speed
compensation. The new GUI calls were checked against the supplied wrappers.
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
7. Compare practice at 50%, 100%, and 150% with constant note speed on and off.
8. Move the HUD to another corner and adjust opacity/text color. Restart the game
   and confirm those settings were retained.

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
