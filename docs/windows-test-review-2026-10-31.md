# 0.4.143 Windows follow-up (working draft)

The uploaded `Latest.log` identifies Clone Hero v1.1.0.6142 and SlopHero
v0.4.143. Its rainbow warning says the `GuitarNoteContainer.SetState` hook
cannot install. The 23-second gameplay video shows the rainbow notes changing
visibly during play and remaining stable when paused. A rendered-frame tint
fallback has been added in the working source. It still needs a Windows check,
including note recycling and sustained notes, before it can replace the alpha
ZIP.

The image and video show the compact score/combo Canvas text while the native
star and song-progress artwork remains. The exact scene inventory in
`performance-followup-2026-10-29f.md` lists 15 score, 8 combo, 8 star, and 4
song-progress sprites. The working source now suppresses all 35 renderers only
when those exact branches are present and the replacement Canvas is visible.
The game components stay active; the original renderer states are restored
on exit or when the option is disabled. Verify that the score, combo, stars,
and progress graphics all reappear on pause and after leaving the song.

`CounterFont` previously retained font assets across scene transitions. The
working source discards its cache when the choice changes and rescans live
TextMeshPro labels when a saved asset is gone. The 0.4.143 log also names
`Sitka Display Bold Italic` as unavailable in that scene; a font absent from
the game's loaded assets still cannot be selected until bundled fonts are
implemented in the deferred font update.

The outro gate previously required both a resolved judgment flag for every
accuracy head and more than three seconds left. The working source relies on
the latest validated note or sustain-tail end time, including disjoint child
tails, and permits a skip while at least half a second remains. It still needs
the Windows manual and automatic long-sustain checks from the validation list.

The video and image support a center crop for thumbnails. At 1920x1080, the
working source crops a centered 960x864 region, keeping all five lanes and
removing much of the surrounding background. The in-game camera capture path
and PNG output still need validation.

The displayed negative Misses report is not explained by this branch's Core
implementation: `LiveStats.FromResolvedHeads` clamps misses to zero, and the
0.4.143 package has no negative-miss log entry. The supplied video shows only
positive Misses. Do not alter scoring from this report alone; verify the exact
on-screen counter and chart/section transition in the next Windows test.

The friend's `mads log.log` is from SlopHero v0.4.136. Windows Application
Control blocked `UnityEngine.TextRenderingModule.dll` while MelonLoader was
resolving dependencies, before SlopHero could load. This requires fixing the
local Windows policy or loader installation; it is not evidence of a mod
exception in v0.4.143.

The restored Core checks pass 163/163. The release project's .NET 6 mod build
uses the supplied game assemblies and succeeds with zero warnings or errors.
The Core check executable ran under the installed .NET 8 runtime with
major-version roll-forward. The v0.4.144 ZIP is a private Windows test build;
the gameplay behaviors above are not yet verified in the game.
