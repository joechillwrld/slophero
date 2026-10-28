# ClonZones and game render follow-up, 2026-10-28

## What the new files establish

The uploaded ClonZones 2.0 assembly and Moonlight (WoR) pack explain the
rainbow-flame failure. ClonZones disables the game's five template flame
objects and draws pooled `ClonZonesStackedFlame` SpriteRenderers instead. Its
`PrepareVanillaFlame` resets each pooled renderer to white on a hit, so
coloring the five templates could not color visible flames. The Star Power
sheet is blue; the normal sheet contains neutral grayscale versions of the
same 16 shapes. The new rainbow bridge colors the active pooled renderers
after each hit and on the existing 20 Hz palette pass, and selects the neutral
sheet for Star Power while rainbow flames are enabled. It restores the
original frame bank and colors when disabled. Without ClonZones, the existing
game-flame path remains in use. Windows visuals still need checking.

The old fret-effects switch also only suppressed the five template flames.
It now calls ClonZones' own `SetActive(false)` during an eligible song when
fret effects are disabled and rainbow flames are off, then restores
`SetActive(true)` within Gameplay when suppression ends. This does not run
without ClonZones. Its interaction with pausing and chart restarts needs a
Windows check.

The Moonlight pack's `FX/note_hit.png` and `FX/note_hit_blue.png` each have
16 frames; frames 8–15 are fully black and become transparent under
ClonZones' black-to-alpha import. ClonZones still keeps those flame objects
active through the blank half of their lifetime. A separate default-off
Sloptifine option caps active `GuitarFlamePatch.Tick` calls at 240 Hz and, only
when both installed PNG hashes match the uploaded pack, invokes ClonZones'
own deactivation method once a stacked flame reaches frame 8. Other packs
get only the cap. This may save CPU and culling work but may delay a sprite
frame by up to 4.2 ms. Its FPS effect is unmeasured.

Rainbow notes use one hue phase per pooled note container, based on the
game's current lane color, instead of the container's arbitrary pool index.
The color hook now distinguishes an alpha-only game write from a new base
color so it does not accidentally treat its own tint as the color to restore.
The 20 Hz tint pass checks each pooled parent once before visiting active
child sprites. This applies to ordinary Clone Hero too; it reduces redundant
hierarchy queries only when rainbow notes are on. Visual correctness and CPU
savings remain unmeasured on Windows.

## Fret overlay research

The latest log reports `create=True, use=True, head=True, lift=True` for
ClonZones fret overlays. The uploaded pack's `settings.ini` has no `[fret]`
section. ClonZones' `Configure` supports `create_overlay_renderers=false`
and `use_overlay_renderers=false` in that section. Both values are needed:
turning off only `use` leaves the extra overlay objects allocated. With
both off, its decompiled `ApplyPressState` and `ApplyFretLayering` use the
original fret SpriteRenderers and sorting orders instead. Other vanilla
layers remain suppressed by ClonZones. This is a scene-initialization choice,
so it must be tested after restarting Clone Hero. The visual layering and
presented-FPS effect have not been checked; SlopHero does not change this
configuration automatically.

The user's exact pack can be tested by adding this to
`Mods/ClonZones/Moonlight (WoR)/settings.ini`:

```ini
[fret]
create_overlay_renderers=false
use_overlay_renderers=false
```

Keep a copy of the original file. Compare fret art at rest, held, lit,
Star Power, open notes, and sustains before comparing frame times. The startup
log should report `create=False, use=False, head=False, lift=False`. If the
art is wrong, restore the original file and restart.

## Broader game-side performance screen

The four recent 30-second raw PresentMon comparisons did not show a
presented-FPS gain for either default-off native cache. The baseline run
averaged about 1,664 presents/s; the fret sorting skip about 1,618; the
regular-sustain cache about 1,592; both about 1,632. Sequential runs and
chart differences limit attribution, but there is no measured reason to
enable either by default. These values are application presents, not monitor
refresh. A single favorable callback count is not evidence of an FPS gain.

The exact current GameAssembly contains six `SpriteRenderer.set_sprite`
calls in its sustain helpers at RVAs `0x115590E`, `0x11559BF`, `0x115628A`,
`0x115651D`, `0x1156AFE`, and `0x1156DAD`. Disassembly confirms each receives
a renderer and an indexed sprite after bounds checks. The first two occur in
the held-tail region; the middle and final pairs have matching regular/open
tail shapes in two specialized regions. The old source independently shows
that those pool slots alternate among normal, tap, missed, open, and held
sprite variants as note counts shift. It also shows that the game writes the
sprite before reading its UVs for the property block, so a wrong skip can
distort the tail as well as select the wrong art. That source is older and
does not prove current slot reuse frequency. A pointer/value cache at these
calls could become stale if another game or mod path changes that renderer
between calls; an exact-build patch therefore needs a current-sprite check
or a complete invalidation map. The adjacent property-block cache did not
improve the four-run FPS comparison, so these setter sites are a lead, not a
released optimization.

The nearby pool activation path has three direct `GameObject.SetActive`
calls in the same mapped region: `0x1155A53`, `0x1156665`, and `0x1156EF9`.
Each passes a visibility boolean without checking `activeSelf` first. The
target is a small IL2CPP-to-Unity internal-call wrapper, and the game already
limits the containing note traversal to rendered updates. Replacing it with
an `activeSelf` getter would trade one native call for another, while caching
the value could miss changes from other paths. There is no measured saving
from that substitution, so this pass does not add another native detour.

The four-run comparison and this code batch do not resolve Unity's broader
camera/culling cost. That sampled tree is inclusive and shared across
cameras; the existing HUD and background visual switches remain the concrete
camera A/B tools. The next Windows test should isolate rainbow visuals and
the flame-tick option from those camera switches on the same chart.

Release checks: Release build has zero warnings/errors; 128 Core checks pass;
the exact-build regular-sustain, fret-sorting, and held-glow verifiers pass.
`downloads/SlopHero-0.4.133-alpha.zip` contains only `Mods/SlopHero.dll` and
has SHA-256 `fa5a15c3324a0accd04e2847b84ef282a1d0e2f64d587b8dd5de1fb7837fb367`.
The rainbow and ClonZones behavior still needs in-game visual verification.
