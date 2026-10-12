# SlopHero 0.2.0 alpha

A MelonLoader mod targeting the supplied **Clone Hero v1.1.0.6142 Windows x64**
build. Compiled against your installed loader and generated assemblies.
**The HUD and restart ran in the user's 0.1.0 test. The new HUD editor and
practice timing fix in 0.2.0 still need an in-game test.**

## Install

[Download v0.2.0 ZIP](https://github.com/joechillwrld/slophero/blob/slophero-v1.1-alpha/downloads/SlopHero-0.2.0-alpha.zip?raw=true)

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

- Click **Move HUD**, then drag the HUD anywhere on the screen. Release to save.
  F8 returns to settings; Escape finishes. Your mouse cursor is shown while editing.
- Adjust **Background opacity** from fully transparent to opaque black.
- Adjust the **Text red/green/blue** sliders to choose any text color.
- Use **Smaller/Larger** to change text size, or **Reset HUD position** if needed.
- Appearance and position changes are saved automatically. The old default HUD
  position moves below the FPS bar once when upgrading; custom positions stay.

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

## HUD

- **Misses:** resolved chart heads marked missed, using the game's results-screen
  counting rule for disjoint chord children.
- **Hit:** `hits / (hits + misses) * 100` for resolved notes in the current run.
  Before any note resolves it shows an em dash. Extra fret inputs do not reduce
  this percentage.
- **Ghosts:** the game's live **Ghost Inputs** counter, which feeds the guitar
  results screen. This is separate from overstrums and drum ghost-note hits.

Counters refresh up to five times per second and reset when the engine, chart,
practice range, or song attempt changes. The first enabled guitar player's HUD
is shown; per-player multiplayer overlays and drum HUDs are outside this build.

## First in-game check

Compilation passed with no warnings, and all **29 portable logic checks passed**.
Native hooks were traced in the supplied binary. The 0.2.0 changes still need
Windows testing; the prior test log confirms the original HUD and restart ran.

1. Hit some notes, intentionally miss a few, and press extra frets. Compare the
   final HUD counters against the guitar results screen.
2. Tap R briefly: it should do nothing. Hold R: it should restart once. Keep
   holding through the reload: it should not restart again until released.
3. Hold P midway through a named section. Confirm practice starts that section
   from its beginning, then check misses/ghosts across a practice restart.
4. Use F8 to bind a spare guitar button and repeat the hold checks.
5. Move the HUD to another corner and adjust opacity/text color. Restart the game
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
