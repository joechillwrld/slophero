# Unused split-alpha sampling candidate

The supplied current Windows assets contain 705 `Sprite` objects in
`resources.assets`; each has `m_RD.alphaTexture` path ID 0. Four materials
used by guitar visuals nevertheless save `ETC1_EXTERNAL_ALPHA` among their
valid keywords, `_EnableExternalAlpha=0`, and no assigned `_AlphaTex`:

| Material path ID | Name | Shader |
| ---: | --- | --- |
| 21 | `FadeSprite` | `CloneHero/Sprite Track FadeIn` |
| 22 | `FadeSpriteAdd` | `CloneHero/Sprite Track FadeIn Additive` |
| 23 | `FadeSpriteAdd1Intense` | `CloneHero/Sprite Track FadeIn Additive` |
| 30 | `Sustains` | `CloneHero/Sustains` |

The current compiled OpenGLCore variants for these shaders include an
`ETC1_EXTERNAL_ALPHA` fragment path that samples `_AlphaTex` before computing
`alpha = mainAlpha + _EnableExternalAlpha * (splitAlpha - mainAlpha)`.
With `_EnableExternalAlpha=0`, the extra sample has no effect on output.
The D3D11 variant tables include both keyword states. Resource material 28,
`SmoothAnim`, also saves the keyword, but its current compiled program has no
`_AlphaTex` sample and is excluded.

The `PerformanceSkipUnusedSpriteAlpha` prototype is a separate default-off
offline-solo setting. On entry it scans active Gameplay `SpriteRenderer`s and
the inactive local guitar pool. It changes a mapped shared material only if
the material's alpha float is zero, its ETC1 keyword is on, at least one
current renderer has an assigned sprite, and no current sprite using it has
an associated split-alpha texture. It restores the keyword on pause/exit.
The geometry-batch material follows the sustain material's guarded keyword
state when both experiments are enabled.

The guard does not observe future third-party sprite substitutions or custom
per-renderer alpha property blocks. This is a GPU shader experiment, not a
measured FPS gain. Compare it independently from the geometry batch; its
activation count only proves that materials passed the guards.
