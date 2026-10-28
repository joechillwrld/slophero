# v0.4.135 always-running work and resume hitch

The v0.4.133 log observed roughly 3,241–3,291 simulation Updates per second.
`Core.OnUpdate` previously read `Application.isFocused` more than a dozen
places per tick, including the unfocused cleanup. Version 0.4.135 reads it
once per tick and uses that snapshot for the same cleanup and song eligibility
decisions. The first focus loss still restores immediately. Later cleanup
checks run at 20 Hz while unfocused, so a scene load can still be reconciled
without running a dozen idempotent restorations thousands of times per second.

When selected, the exact-build native regular-sustain, fret-sorting, and
held-glow hooks are now deactivated, rather than uninstalled, on focus loss
or opening SlopHero settings. Their inactive stubs pass through to the stock
game calls; toggling a setting off or leaving Gameplay still removes them.
This prevents a selected hook from doing another expensive install on the
first song update after Alt+Tab or closing settings. Native callsites remain
patched while inactive within Gameplay, which is the same state already used
for ordinary pause/resume. The previous log's ~118 ms first-song maxima were
consistent with three installs; this change targets repeats, not the initial
install cost.

Outside Gameplay, the song-speed editor still handles a selected speed row on
every Update. When `SongOptions` is absent or the speed row is inactive, a
cached negative result now avoids its Escape/key polling and Unity activity
lookup until the next 20–50 Hz check. Active speed editing keeps the original
per-tick input path; opening or selecting the row can be noticed up to 20 ms
later than before.

These changes remove avoidable work but have no isolated Windows FPS result.
The raw PresentMon comparisons in the prior follow-up did not show a gain
from the two native caches, so their settings remain default off. A new
SpriteAttach experiment for the separate Star Power bar and rock meter was
discussed but is not part of this build without the user's approval.

Release build: zero warnings/errors; 128 Core checks pass. The private ZIP
contains only `Mods/SlopHero.dll`; SHA-256:
`080a043433d18f60d087f9dbe03808dcf707c840c3ef05df7d1196c5a551995d`.
