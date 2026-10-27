# Aggressive solo rendering prototypes, 2026-10-26

Research against the supplied v1.1.0.6142 binary and assets. Four independent
default-off implementations first shipped in v0.4.115; the first runtime test
showed all four aborting before activation. Version 0.4.116 traces identified
two stripped Unity calls. Version 0.4.117 replaces those calls with APIs
already used elsewhere in the mod. Its runtime log confirms all three sustain
controls activate, while HUD merge fails at stripped command-buffer methods.
Version 0.4.118 adds a default-off native-command bridge, fixes the game's
glow re-enable behavior, and prepares cache hooks before song play. In-game
visual behavior and performance remain unmeasured. This supplements the
existing camera benchmarks; it does not prove
that sustains dominate guitar-camera cost.

The table below records the initial implementations. The current fixes and
remaining risks are documented in [the handoff](performance-handoff.md#04118-native-hud-commands-and-effective-visual-suppression).

## v0.4.115 implementation status

| Prototype | Runtime behavior | Main limitation to validate |
| --- | --- | --- |
| Repeated sustain writes | `SustainStaticWriteCachePrototype` hooks Unity's sprite/color setters, `MaterialPropertyBlock.SetFloat`, `Renderer.SetPropertyBlock`, and block clearing only while this toggle is active. It caches exact values for renderers using the mapped regular `CloneHero/Sustains` shader. Renderer discovery now enumerates SpriteRenderer children under the active guitar player, avoiding the stripped `GetComponentInChildren<GuitarNoteRenderer>` generic found in 0.4.116. | Hooks run at global Unity setters while active, then filter by renderer/block. That managed interception could cost more than the writes it skips. Position and changed values still run normally. This is broader than native callsite hooks and is a CPU experiment. Runtime activation still needs confirmation. |
| Sustain batching flags | `SustainBatchingPrototype` changes the shared regular-body material's `DisableBatching` override tag to `False` and enables instancing, restoring both on scope exit. | This asks Unity to batch; it does not create combined mesh geometry or verify the shader has an instancing variant. Per-tail property blocks may still prevent batching, and the shader's vertex sizing can distort tails. |
| Plain held tails | `PlainHeldSustainPrototype` disables only sustain-glow SpriteRenderers under the local `GuitarNoteRenderer` through `Renderer.enabled`; it restores their prior states on pause/scope exit. | Removes glow rendering only. The native held-tail helper and all whammy input/scoring stay live. The 0.4.115 `forceRenderingOff` call failed at runtime and was replaced in 0.4.116 with a renderer property already used elsewhere in the mod; the new build still needs in-game confirmation. |
| HUD render merge | `HudCameraMergePrototype` draws visible camera-layer renderers on the guitar camera with HUDCam's view/projection, sorting keys, and 20 Hz state refresh. If camera-space Canvas content is present, it keeps HUDCam enabled with only Canvas layers and merges the remaining renderer layers. Active content discovery uses `Object.FindObjectsOfType<T>()`. Bounds visibility now uses `Camera.WorldToScreenPoint`, avoiding the stripped `GeometryUtility.CalculateFrustumPlanes(Camera)` call found in 0.4.116. | Explicit draw order can differ; the command buffer rebuild adds CPU work and 20 Hz visibility updates can briefly lag. If no layers can be separated, it fails closed and leaves HUDCam alone. The 0.4.117 change still needs in-game activation and visual checks. |

All four switches default off and run only during eligible offline solo songs.
The test path restores on pause, settings interaction, focus loss, transitions,
online play, and scene changes. These switches are experiments, not established
optimizations. Version 0.4.117 replaces the two stripped paths identified by
the 0.4.116 log. First confirm activation and note/HUD parity, then compare raw
PresentMon frame intervals.

## New exact-build evidence

- `globalgamemanagers.assets` shader path IDs **14** (`CloneHero/Sustains`)
  and **15** (`CloneHero/SustainGlow`) both declare `DisableBatching=true`
  in their subshader and pass tags. Resource materials **30** (`Sustains`)
  and **29** (`SustainGlow`) point to those shaders through external file 1,
  `globalgamemanagers.assets`. Their serialized instancing flags are false.
  This identifies a constraint on ordinary batching, **not a measured draw-call
  count**, and does not prove the shipped shader has usable instancing variants.
- The regular body shader exposes `ExternalVertexSizing`, `_SustainLength`,
  `_TextureScaleOffset`, and `_FadeParams`. Its exact shipped OpenGLCore shader
  blob contains readable compiled GLSL, and the D3D11 program table includes
  variants with and without `EXTERNALVERTEXSIZING_ON`. See
  [the geometry mapping](sustain-geometry-batching-2026-10-26.md) for the
  deformation formula, atlas geometry, and opt-in mesh implementation.
- Current closed-generic upcoming-sustain helper **RVA 0x1156710** performs
  unguarded Unity setter calls on its regular-tail branch: enable body at
  `0x1156987`, disable glow at `0x11569BF`, assign sprite at `0x1156AFE`,
  GetPropertyBlock at `0x1156B10`, SetFloat at `0x1156B32`, SetPropertyBlock
  at `0x1156B44`, then assign color at `0x1156B6E`. The float input is XMM12;
  the property ID is read from renderer-instance offset `0x198` and the
  reusable property block from `0x1D0`. The two regular-tail counters and arrays
  must not be inferred from the older source; use this current specialization.
- Held-sustain helper **0x1155460** separately enables the glow at
  `0x11559DB`, writes body/glow colors at `0x1155A0E`/`0x1155A38`, positions
  the object at `0x1155ABD`, and performs two property-block sequences ending
  at `0x1155B9D` and `0x1155C52`. These include dynamic whammy/fade/length
  data. They cannot all be cached merely because the note identity is stable.
- The existing effect switches suppress neck particle systems/fret effects.
  They do **not** suppress the pooled sustain-glow renderers. The existing
  whammy dirty gate skips duplicate texture uploads, not those renderers or
  their material-property submissions.

Machine-readable [evidence](sustain-rendering-evidence-2026-10-26.json) records
file fingerprints, method byte hashes, callsites, shader tags, and materials.
Reproduce it with `tools/screen_sustain_rendering.py --game-assembly ...
--globals-assets ... --resources-assets ...` using `pefile`, `capstone`, and
`UnityPy`. It refuses another GameAssembly hash and never modifies the inputs.

## 1. Cache static sustain outputs inside the producer

Start with **regular, upcoming, non-held tails only**, within `0x1156710`.
Keep note selection, position updates, activation transitions, color-state
changes, and the game's rendered-frame gate. Cache successful assignments per
pool slot/generation for sprite, color, enabled state, and sustain length.
The regular-tail property-block sequence writes one length value, so an
unchanged length is a concrete candidate for skipping that whole copy/write
sequence. Save the original behavior for the first write, recycling, reset,
chart transitions, practice seeks, SP/miss transitions, and scope exit.

Do not reproduce SpriteAttach's expensive managed polling cache. A blanket
Harmony prefix on every Unity setter adds native/managed crossings to unrelated
work. The attractive version compares the producer's already-computed values
at its exact callsites, using native cached storage; implementing that means
carefully preserving registers, stack alignment, exception paths, and original
instructions. A managed helper replacement is possible but could cost more
than the current native path and must be treated as a performance experiment.

Risk: pool reuse or another mod writing the same renderer can invalidate a
last-output cache and leave stale color/length. Restrict to owned pool slots,
restore vanilla on scene changes, and leave held/open tails vanilla initially.
Potential saving is repeated CPU/native renderer work; no gain is measured.

## 2. Batch sustain geometry instead of hundreds of renderer submissions

This is the larger renderer project. Build a dynamic mesh for regular sustain
bodies from the game's current visible-tail outputs, grouping only compatible
texture/material/sorting ranges. Encode tail geometry, UVs, and tint per vertex
instead of requiring a different `_SustainLength` property block per renderer.
Initially keep note heads, held glows, open tails, and all gameplay code native.
Suppress only the native renderers whose geometry the replacement actually
emits, and restore them when the experiment ends.

Preserve premultiplied blending, track fade, SP/missed/tap styling, atlas UVs,
lefty flip, and the game's ordering relative to note heads. The shipped shader's
The exact shader's no-`EXTERNALVERTEXSIZING_ON` variant supports a batch when
the original deformation is baked into mesh vertices. The four current sustain
shader passes have **no instancing variant**, so setting
`Material.enableInstancing=true` alone is insufficient.
Do not simply remove the DisableBatching tag: the old shader deforms geometry
in object space, and the current shader has not been proven safe for automatic
world-space batching.

The first stage targets render submission/culling overhead while retaining the
native producer. Only after visual parity should a second stage bypass the
duplicated native per-tail setup; otherwise mesh reconstruction can add CPU
work and lose FPS. Risk: missing/incorrect lengths, fade/UV seams, or ordering
artifacts on dense chords. Dense sustain charts are the relevant validation
case, alongside sparse charts that expose fixed replacement overhead.

## 3. Optional plain held tails, without animated sustain glow

A separate default-off cosmetic mode could retain the base held tail and its
SP color while omitting the extra whammy/glow renderer. This is distinct from
the existing sustain-particle switch. It targets a shader which currently
disables batching and avoids adding a mesh builder as the first experiment.

First prototype: suppress only the mapped pooled glow renderers immediately
before rendering, restore original states at scope exit, and verify that base
held tails remain visible for regular and open notes. That stage targets GPU
rendering; the native helper still runs and its CPU cost is not saved. A later
trimmed visual helper could omit glow property writes and visual texture work,
but only after mapping all producer/consumer paths.

Do not freeze whammy input or sustain/SP accounting. In particular, **0x22BB00
returns a Vector4** used by material setup; it is not the old source's
SetWhammyPixel writer and must not be replaced as if it were a void writer.
Stopping the texture uploader while a remaining shader samples it can leave
stale glow visuals. Risk: deliberately reduced whammy feedback; this should be
a separate choice from a parity-preserving batching prototype.

## 4. Remove HUDCam through an explicit HUD render path

This remains a larger independent candidate. Current camera tests make it
possible to bound removal of the HUD pass; they do not demonstrate a working
merge. The previous projection objection is a technical requirement to solve,
not proof that a merge is impossible: HUDCam is orthographic, PlayerCamera is
perspective, and simply OR-ing their masks distorts the HUD.

A concrete prototype would render eligible HUD sprites with HUDCam's original
view/projection through an ordered command buffer attached after the guitar
camera's scene pass, then disable HUDCam's automatic rendering. Handle active
Canvas content separately (overlay conversion or an explicit supported path),
preserve sprite-font geometry and sorting, restore the guitar matrices/render
state afterward, and restore all camera/Canvas states before menus, pause,
focus loss, or online transitions. Changing matrices alone does not queue the
sprites; the replacement must actually submit their geometry/renderers.

Do not invoke HUDCam.Render manually and call that a removed camera pass.
Do not promise that CommandBuffer.DrawRenderer honors disabled/force-off
renderers in the required way without validating it in this Unity build.
This route could trade the second camera's culling/extraction for explicit HUD
submission, but rebuilding a command buffer from every renderer every frame
may cost more. Risk: missing HUD elements, broken transparent ordering,
third-party overlay conflicts, or Canvas projection errors. Use offline solo
only and restore before UI interaction.

## Checks that prevent duplicate work

The current note-head state apply method at `0x353210` already compares all
four sprite pointers, Color32 values, group sorting order, individual sorting
orders, and active-state bits before issuing corresponding setter calls.
Do not add another generic cache there based on its list of callsites alone.
The cleanup helper at `0x1160A20` stops at the **first inactive pool object**;
it does not blindly traverse all 250/50 inactive tails each frame. The current
render Update still checks `willCurrentFrameRender` first and caps traversal at
500. Those existing optimizations must survive any replacement.

Recommended investigation order: static regular-tail writes, regular-tail
batching, optional plain held tails, then explicit HUD rendering. These can
have independent default-off controls. No new capture or game file is needed
to begin the prototypes. The user's earlier authorization for investigating a
HUD-camera merge carries forward. Discuss the invasive sustain-rendering
prototypes and the new plain-tail visual compromise before adding them. No new
build is shipped by this research pass.
