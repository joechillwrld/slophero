# 0.4.144 Windows feedback and 0.4.145 test

The uploaded 0.4.144 `Latest.log` confirms the 35-renderer compact HUD path
activated. The user reports that the compact HUD works. Highway thumbnail
capture was not tested.

Rainbow notes still flickered during play. The log reports the attempted
`GuitarNoteContainer.SetState` hook was unavailable in each Gameplay scene;
the 0.4.144 late-update fallback was insufficient. Version 0.4.145 uses the
same IL2CPP camera callback pattern already used by runtime timing diagnostics
to reassert the current tint just before the player camera renders. The new
log should confirm the callback actually fires. This is unverified in-game.

The 0.4.144 log contains no outro-skip request. The 0.4.145 gate no longer
depends on the restart shortcut's `isSongPlaying` and coroutine checks once
the final chart tail has elapsed. It accepts negative preroll note times and
does not reject chart times merely for exceeding an estimated song length.
The next log records the selected mode and bounded end-of-song gate state, so
another failed transition can be traced to its actual condition. Manual and
automatic results-screen transitions remain unverified in-game.

The user identified the negative Misses display as SlopHero's purple counter
while playing a practice section. Practice now counts judged misses within
the selected tick range and corrects a recent miss if a late hit is accepted.
It no longer subtracts the engine's full hit total from section-only resolved
heads. Full-song counts and ghost inputs keep their existing paths. Section
restart and late-hit behavior need the next Windows check.

The user reports chosen fonts work after restarting a song but cannot see
their appearance when selecting them. The font chosen by the selector now
remains cached through that menu change, and a live counter sample appears at
the counter position while the Counter HUD settings page is open. Fonts absent
from the game's loaded assets still fall back to its default; bundled fonts
remain deferred.
