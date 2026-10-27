# Sustain geometry batch and follow-on targets, 2026-10-26

The v1.1.0.6142 `CloneHero/Sustains` shader has a compiled vertex variant
without `EXTERNALVERTEXSIZING_ON`. Its sizing variant maps every input vertex
with `y >= 1` to `max(max(_SustainLength, 0) + y - 100, 0)` and leaves lower
vertices unchanged. The no-sizing variant uses the input positions directly.
The D3D11 variant table contains both keyword states. Both normal and
`TRACKFADE_ON` paths use the same sizing formula; track fade derives screen
coordinates from the transformed output position. This permits per-tail CPU
deformation into one mesh without replacing the shader or its fade effect.

Current regular sustain sprites 1373, 1633, and 1648 in `resources.assets`
are 73×70 pixels, have a 38-pixel **top** border, bottom-center pivot, and
100 pixels per unit. Prefab `Sustain` uses sliced drawing, material 30, and
sorting order -1000. The older source initializes regular renderer height to
100 units; the prototype checks that value at runtime before batching.
The 250-slot pool is scanned for active objects at 20 Hz; a new tail missed by
that scan stays on its native renderer until the next scan. Each accepted tail contributes six vertices and four triangles, with the
original atlas UV split and source color baked into the mesh. It preserves the
existing `CloneHero/Sustains` material and turns off the external-sizing
keyword on a private clone. The source `SpriteRenderer`s are disabled only
between the guitar camera's pre-cull and post-render callbacks. Fewer than
eight compatible upcoming tails use the original renderers. Held tails, open
tails, and glow are outside this first batch.

The new `PerformanceBatchSustainGeometry` option is default off and limited to
active offline solo songs. The old `PerformanceBatchSustainBodies` material-
flag test was removed after a user reported warped tails while it was on;
the new build clears a saved old value, and old profiles cannot re-enable it.
The new preference is independent, because old saved settings must not
silently enable new geometry. A failed Unity call restores source renderers
and stops the batch for the song. Source shader/atlas variation and sorting
differences are reasons to compare visual parity in-game. Mesh construction,
array uploads, and renderer toggles may cost more CPU than they save in sparse
or moderately dense charts. **No presented-FPS or draw-call gain is measured.**

This game strips the managed `MaterialPropertyBlock.GetFloat` wrapper. The
prototype resolves the current Unity player's
`UnityEngine.MaterialPropertyBlock::GetFloatImpl` internal call through
`il2cpp_resolve_icall` to read `_SustainLength`. Resolution is deferred until
the new switch is enabled. A missing binding fails open; an incorrect native
signature remains a crash risk until the exact Windows build is exercised.

| Possible follow-on | Exact-build evidence | Priority / missing check |
| --- | --- | --- |
| Open sustain bodies | `CloneHero/OpenSustain` (resource shader 1108, material 26) also sets `DisableBatching=true` and has no instancing variant. Its compiled shader applies the same length formula but also scales X by 1.5 and uses per-tail `_uUVProperties`. | Next geometry candidate after regular tails; map its UV and whammy inputs separately. |
| Held regular / open glow | `CloneHero/SustainGlow` (global shader 15) and `CloneHero/OpenSustainGlow` (resource shader 1110, material 25) also disable batching and lack instancing variants. The open glow uses per-tail UV and animated whammy state. | More complex. The independent `PerformanceHideHeldSustainGlow` switch already omits these pooled renderers for users who prefer plain tails. |
| Note heads and input-viewer sprites | Current resource prefabs chiefly reference `FadeSprite`, `FadeSpriteAdd1Intense`, and `Sprites-Mask`; these shaders do not declare `DisableBatching=true`. The current Gameplay snapshot had 48 enabled guitar input-viewer sprites, while serialized counts include inactive variants. | Only attempt a manual mesh if a current draw-call capture shows their existing Unity batching is inadequate; material, atlas, fade, and transparent sorting may separate draws. |
| HUD text / Canvas | The current scene contains Canvas renderers and the mod has an experimental Canvas counter HUD. | Unity Canvas already performs its own geometry batching. Optimize rebuild frequency and material changes before a second handwritten batch. |

The four sustain shader assets are split between the supplied
`globalgamemanagers.assets` (regular body/glow) and `resources.assets`
(open body/glow). All four shader passes report `m_HasInstancingVariant=false`
and `m_HasProceduralInstancingVariant=false`. Those flags rule out the existing
material-instancing switch as an instanced-draw solution, but they do not
measure draw calls or prove a dynamic mesh is faster.
