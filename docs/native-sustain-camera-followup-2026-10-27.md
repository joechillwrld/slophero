# Native sustain cache and camera/culling follow-up, 2026-10-27

## Default-off regular-tail prototype

The exact supplied `GameAssembly.dll` (SHA-256
`12bc21e5a88a2fc9abc259d5d67f24835145eabca6c3f863b075ba37ad75f197`)
has six `Renderer.SetPropertyBlock` direct calls between RVAs `0x1155460`
and `0x1157010`: `0x1155B9D`, `0x1155C52`, `0x11562D0`, `0x11565E7`,
`0x1156B44`, and `0x1156E74`. The regular upcoming-tail call at `0x1156B44`
receives the renderer in RCX and the already-computed length in XMM12. The
other five calls use the same renderer argument position, but represent other
tail paths and always run normally.

The opt-in patch redirects the regular call to a small native direct-map cache
of renderer pointer and length bits. Identical consecutive output skips the
property-block submission; a different length or renderer always passes
through. The other five callsites invalidate the corresponding renderer slot
before passing through. A hash collision is a safe miss. The cache clears when
an eligible solo song begins, and all six calls take the original path on
pause, focus loss, online play, or when the setting is off. The note selection,
sprite, color, position, scoring, audio, and polling code is untouched.

This is a **native crash and visual-parity experiment**, not a measured FPS
gain. The exact game/metadata build gate and all six call bytes must match
before installation; the toggle defaults off. A third-party mod changing a
renderer property block outside these six callsites can still make a cached
length insufficient as an invalidation key. Dense and sparse regular-tail
charts, held/open transitions, practice seeks, pause/resume, and online entry
need a Windows visual test. The offline static verifier checks the DLL hash,
all six target calls, scans the helper for unlisted property-block calls, and
emulates first write, repeated write, length change, invalidation, hash
collision, and disabled bypass using Unicorn. It cannot reproduce Unity's
live renderer behavior.

## Camera and culling research

The symbolized WPA sample covers 42.3 seconds. Under `PlayerRender`, the
inclusive `RenderCameras` node has about 10.34 seconds of sampled CPU;
`Camera::CustomRender` has 5.44 seconds and the sibling
`Camera::CustomCull` has 4.65 seconds. These weights overlap with their
parents and are not per-camera timings. `CustomCull` has no exported children,
so the stack cannot assign its weight to the guitar or HUD camera or a
renderer type. Under `CustomRender`, renderer-scene extraction has about 2.01
seconds inclusive, of which about 1.20 seconds is `ujob_wait_for`. That is a
promising submission/culling lead but not evidence that skipping a whole
camera's culling would preserve the frame.

The exact Gameplay `level2` scene has two cameras: Background Cam clears
layer 13, and HUDCam draws layers 5 and 12. Neither has occlusion culling
enabled, and each serialized camera object has only a Transform and Camera
component. The current regular `PlayerCamera` prefab is a separate
perspective layer-9 camera with occlusion culling off and a 120-unit far plane.
The 10,000-unit player-camera planes in the assets belong to the other
instrument variants. This rules out a general occlusion toggle and makes a
far-plane cap a narrow, likely small experiment; one already exists. Runtime
instrument setup reduces the scene's 322 serialized input-viewer sprites to
48 live guitar-viewer sprites, so disabling inactive variant renderers again
would add little.

The previous per-camera callback capture counted 48,724 renders each for
PlayerCamera and HUDCam in 30 seconds; Background Cam rendered only 401 times
before the black-background override. Its callback windows were about
0.026/0.058 ms for player pre-cull-to-pre-render/pre-render-to-post-render
and 0.022/0.040 ms for HUD. Those windows do not contain all native culling or
GPU work. The separate camera-isolation PresentMon pair (1,936.77 FPS with
guitar hidden, 1,860 FPS with HUD hidden) is directional because settings and
time differ. This evidence favors cutting actual layer-9 renderer submissions
or object count while preserving both camera passes. The old HUD merge and
regular-tail geometry batch were retired after runtime failure/overhead; they
are not resurrected by the native length-write cache, which only targets one
producer-side CPU call.
