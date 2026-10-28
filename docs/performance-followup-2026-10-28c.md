# v0.4.134 rainbow, first-song hitch, and practice HUD check

The user's v0.4.133 report confirms rainbow notes and ClonZones replacement
flames now work. Version 0.4.134 adds one Colors-page cycle-speed slider for
both. It defaults to 0.18 cycles/s (5.6 seconds per cycle), slightly faster
than the prior hard-coded 0.12 cycles/s (8.3 seconds). The range is 0.05–0.8
cycles/s, and the value is saved in preferences and settings profiles. Color
palette work remains capped at 20 Hz.

## First-song hitch

The new log has two 30-second mod-timing windows with `Core.Update` maxima of
118.271 and 118.876 ms, and simulation-delta maxima near 120 ms. At each first
active song update, preparation messages for the native held-glow, regular-
sustain, and fret-sorting hooks are separated by about 38 ms each. That
sequence is consistent with most of the observed `Core.Update` maximum, but
the log does not have a call stack for the exact spike. The selected hooks
now install inactive in `OnSceneWasInitialized(Gameplay)`, before the first
active song update. They remain pass-through while a selected option is
inactive within Gameplay, and uninstall when its setting is switched off or
the scene ends. This moves the setup cost to the scene transition and avoids
reinstalling after pause/restart; it does not remove the setup cost or prove
a presented-frame improvement. Only selected default-off options prepare.

The log also shows the ClonZones flame-tick option installed while the
existing fret-effects option had suspended all ClonZones flames. That left a
prefix on its high-frequency `Tick` with no flame work to save. The flame-tick
experiment now stays uninstalled in that combination and activates if rainbow
flames are selected or fret-effect suppression is switched off. This is a
small hot-path cleanup, with no isolated FPS result.

The capture's 3,241–3,291 simulation Updates/s and 1,620–1,645 scheduled
render callbacks/s are CPU callback rates. The log contains no raw PresentMon
capture, so it cannot establish displayed FPS or frame-time improvement from
these changes. The four earlier raw comparisons still show no FPS gain from
the regular-sustain or fret-sorting caches; both remain default off.

## Practice section name

The current `level2` Gameplay scene places `PracticeUI` on `PracticeHUD/Panel`
and serializes its `rectTrans` field to that Panel's RectTransform (path ID
6754). The centered `PracticeHUD/Section Name` (path ID 67) is a sibling of
Panel under `PracticeHUD`, with centered X anchors/pivot and anchored X=0.
SlopHero's practice offset/scale code follows `PracticeUI.rectTrans`, so it
changes Panel, not Section Name. The user's log shows nondefault practice
stats offset (+263, -239) and scale (0.89), but no layout exception. This
check does not establish why her label sometimes looks off-center; the
section title text, fonts, chart content, and runtime Canvas layout were not
captured. No section-label transform is changed in this build.

Release checks: the Release build has zero warnings/errors, all 128 Core
checks pass, and the exact-build regular-sustain, fret-sorting, and held-glow
verifiers pass. The private alpha ZIP contains only `Mods/SlopHero.dll`;
SHA-256: `e306439181de8eb91daba7e696672ce7d7457c956d77f1522aaa4c4664d84a1a`.
Windows visual and presented-frame results remain to be checked.
