# Performance screen, 2026-10-28

This screen combines the exact-build v1.1.0.6142 data, the v0.4.128 runtime
log, the symbolized 42.3-second WPA tree, and the previous PresentMon captures.
The v0.4.128 log has no PresentMon data, so its scheduled render callback
rates cannot validate presented FPS. This pass narrows the sustain-glow filter
to its exact game callsites and adds a separate visual-only input-viewer test.

## Current evidence and changes

| Area | Finding | Change / limit |
| --- | --- | --- |
| Sustain glow | The v0.4.128 log finds 300 pooled glow renderers. Its global Harmony setter hook blocked 16,038, 16,705, and 16,514 redundant disable writes in three songs, with no attempted enables blocked. | The current worktree replaces that global renderer hook with an exact-build native pointer filter at seven `Renderer.enabled` callsites inside the two sustain helpers. It keeps the 300 renderers disabled only during eligible offline solo play and restores their captured states on scope exit. Static call bytes and stub behavior are checked; Windows activation, visuals, stability, and presented-frame benefit remain untested. The toggle remains default-off. |
| Regular sustain writes | The same log records 3,796, 4,325, and 4,488 repeated length property-block submissions skipped in three songs by the existing native cache. | This confirms repeat values occur in live play, but does not measure the work saved or prove the cache wins. Keep it optional and compare a dense same-chart A/B before expanding its scope. |
| Input viewer | The live scene snapshot finds 48 active guitar-viewer sprites and zero GHL/drum-viewer sprites. | A separate default-off switch deactivates only `HUDCam/Input Viewer` during eligible offline guitar songs; it restores the branch on pause, menu, focus loss, online play, and scene exit. It changes no instrument input or polling. The 48 sprites are an upper bound on removed renderers, not an FPS estimate. |
| Camera and renderer work | WPA places 11.46 seconds inclusive under `PlayerRender`, 10.34 seconds under `RenderCameras`, 5.44 seconds under `Camera::CustomRender`, and 4.65 seconds under `Camera::CustomCull`. `ExtractSceneRenderNodeQueue` contains 2.01 seconds, including 1.20 seconds in `ujob_wait_for`. | These are overlapping sampled weights. The cull node has no expanded child attribution and the export cannot separate PlayerCamera from HUDCam work. Occlusion culling is already off; skipping a whole camera or native cull callback is still unsupported. |
| Mod callback work | In three 30-second captures, `Core.Update` averages 0.002–0.003 ms; visual overrides average 0.021–0.024 ms; the `Performance effects` bucket averages 0.037–0.199 ms at two calls per second. | These buckets do not explain the Unity render/cull tree, and nested timing buckets overlap. One 9.315 ms maximum is an isolated outlier, not a recurring target. |
| Frame evidence | The same log reports about 3,159–3,249 simulation Updates/s and 1,580–1,624 scheduled-render callbacks/s. | Those are callback rates, not presented FPS. Previous PresentMon captures span different builds/settings, so they cannot establish whether the current work improves frame pacing. |

## Other leads screened

- The render-every-update experiment stays retired: earlier captures showed
  29–30% more scheduled-render callbacks alongside roughly 35% fewer
  simulation Updates. There is no reason to restore it without a matched
  PresentMon result and a new design that preserves simulation.
- The Unity worker-count adjustment showed no consistent benefit. Leave the
  normal worker pool alone by default.
- SpriteAttach's input/output cache was removed after its per-call comparisons
  cost more than the native method it skipped. Do not revive that cache.
- The native HUD merge and sustain-geometry batch had no reliable visual or
  performance win and previously hit stripped Unity API paths. Do not revive
  them from the inclusive camera weights alone.
- The background camera's video is enabled but not playing in the supplied
  snapshot, so this path does not show recurring decode work. Its camera also
  supplies the background clear and visible background layer.
- The current input-viewer branch is the only newly isolated high-population
  visual subtree that can be removed without changing note, score, or input
  paths. Its cost still needs A/B validation.

## Rainbow state

The 0.4.128 test confirms the prior implementation found note sprites and flame
animators, but the user reported fast-looking note color motion and white
flames. The current worktree slows the hue cycle from 0.24 to 0.12 rotations
per second, rechecks the local guitar pool once per second (including inactive
children, so newly created note slots are admitted), and reapplies flame tint
after the game's flame Animator update. The additive flame tint uses lower
brightness. These are code-level corrections only: ClonZones changes the
textures and blending environment, so the rainbow visuals cannot be called
confirmed until tested in the game.

## Next performance test

After a build, use the same solo chart/settings and compare raw PresentMon
intervals with each new option separately: held-sustain glow, regular-tail
write cache, then input-viewer suppression. Keep all three off for a baseline;
change one switch at a time and include a pause/resume check for restoration.
This separates visual tradeoffs and prevents callback rates from being
mistaken for FPS. No optimization in this screen has a measured FPS gain yet.
