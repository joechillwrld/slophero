# v0.4.132 native and online follow-up

The optional regular-sustain cache now checks the exact current-build
`GetPropertyBlock` call at RVA `0x1156B10`. When that renderer and sustain
length match a prior successful regular write, it resumes at `0x1156B49`,
past `GetPropertyBlock`, `SetFloat`, and `SetPropertyBlock`. The existing
`0x1156B44` hook commits the cache, and the other five tail submissions
invalidate the renderer slot. Changed values and cache collisions use the
original sequence. The early branch leaves the later color and note-loop
instructions intact. This extends the existing default-off switch; it does
not add a second setting.

The Oct 27 log counted about 3,750–4,480 repeated submissions per full song.
That is the number of candidate three-call sequences, not a measured CPU or
FPS gain. The verifier checks the exact DLL hash and both call targets, then
emulates first write, repeat, changed length, invalidation, disabled state,
and hash collision. It checks that the early branch reaches the instruction
after the original setter with the stack and call arguments intact.

The existing Star Power side-glow and personal-best visual switches now apply
during active online songs as well as solo songs. Side-glow discovery and the
combo-smoke optimization use the local controller-backed guitar slot.
`isRemotePlayerPlaying` indicated remote combo activity, so it was not a
reliable ownership test for the combo-smoke search. These changes touch the
local display only.

The exact sustain helpers also contain six calls to
`SpriteRenderer.set_sprite` at `0x115590E`, `0x11559BF`, `0x115628A`,
`0x115651D`, `0x1156AFE`, and `0x1156DAD`. They are a separate repeat-write
candidate for a later exact-build prototype. The current cache does not alter
those sites; they require pool-reuse and third-party sprite-write handling
before bypassing them.

Release build: zero warnings/errors; 128 Core checks and the native sustain,
fret sorting, and held-glow verifiers pass. The private alpha ZIP contains
only `Mods/SlopHero.dll`; SHA-256:
`48b40c79c13e160c18c11fd0494ba1b3b4b711d2d78269f30d10cf6bdd1f72cf`.
Windows visual behavior and presented FPS remain unmeasured for this build.
