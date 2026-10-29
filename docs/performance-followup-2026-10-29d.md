# 0.4.139 color controls and four-capture follow-up

## Implemented

- The rainbow cycle slider now reaches **stopped**. At zero, hue rotation selects a reproducible fixed palette; at a moving speed, rotation shifts the phase of the cycle. A full 360-degree shift was identical to zero, so the UI now ends at 355 degrees in its five-degree steps. The note/flame defaults and normal colors remain unchanged.
- Saturation and brightness sliders now reach 200%. Saturation over 100% expands RGB from its luminance after HSV conversion; brightness can send RGB values above one to Unity. Additive flame blending or the display pipeline can still clip or wash out the colors at high values. This is a customization range, not a performance optimization.
- A separate default-off 60 Hz moving-palette cap can reduce the optional rainbow pass from as many as 120 to 60 executions per second. The stopped palette checks pooled notes at 30 Hz. The latest two in-game timing reports with moving rainbow measured 3,522 and 3,514 palette passes per 30 seconds, with 0.099 and 0.106 ms average CPU per pass. A 60 Hz cap has an approximate upper bound of 6 ms CPU saved per second at those costs; its effect on presented frame times and visual smoothness requires a Windows A/B.

## Captures and setting attribution

The uploaded v0.4.138 `Latest.log` places the raw PresentMon runs as follows. Times are local timestamps in the user's files. Values below discard the first and final second of each capture; the samples still cover different songs or song sections.

| Start | Logged condition | Trimmed presents/s | Mean frame interval | P99 frame interval | CPU busy | GPU busy |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 02:42:09 | Before ClonZones original-fret and bridge-retention selection; rainbow off | 1,571 | 0.637 ms | 0.859 ms | 0.608 ms | 0.250 ms |
| 02:43:27 | Same logged state; another change the user recalls may be sustain-related but cannot identify | 1,547 | 0.647 ms | 0.874 ms | 0.618 ms | 0.254 ms |
| 02:47:11 | ClonZones original frets and inactive bridge retention selected; rainbow notes/flames on | 1,514 | 0.661 ms | 0.937 ms | 0.631 ms | 0.247 ms |
| 02:49:25 | ClonZones original frets and inactive bridge retention selected; rainbow notes/flames off | 1,561 | 0.641 ms | 0.884 ms | 0.612 ms | 0.242 ms |

The first two runs were not a labelled single-switch comparison; their difference remains unknown. The last two change rainbow together with other scene conditions. They cannot isolate the original-fret, retention, or rainbow FPS effects. Original-fret mode coincides with approximately 20 fewer enabled sprite renderers in the scene inventory, but it does not establish an FPS improvement. The private branch retains these as default-off experiments.

PresentMon shows CPU busy near the frame interval and GPU busy about 0.24–0.25 ms, with no PSO compilations in these runs. The mod's ordinary `Core.Update` measured roughly 0.001 ms per simulation update. `PlayerCamera` and `HUDCam` rendered on each frame. The 173 and 228 `Background Cam` callbacks in two timing captures happened in the 0.19 and 0.25 seconds between starting camera timing and activating black-background mode; they do not show a recurring background pass during the song.

The 48-sprite input viewer was present and its existing **Hide input viewer during active songs** switch was off. Hiding it is a separate normal-play rendering test for anyone who does not need the viewer; the renderer count alone does not predict a gain. An identical-song, repeated on/off PresentMon test is needed before crediting it. The large native Unity cull/render work remains shared across the player and HUD cameras; the current capture does not name an isolated operation to safely remove while preserving both visuals.

## Next in-game check

Confirm the stopped palette and hue rotation, then try moving rainbow at normal and capped rates for visual stepping. For frame times, keep song, resolution, video/background, overlays and all other options fixed. Test the 60 Hz cap on/off with rainbow enabled, and separately test input-viewer hide on/off with rainbow disabled. Restart each song before each capture. Do not compare the four mixed v0.4.138 runs as if they were matched A/Bs.

The Release build succeeds with zero warnings or errors, and all 128 Core checks pass. The ZIP contains only `Mods/SlopHero.dll`; SHA-256: `e8b31b40183a57f2fb1526fb5dafb1e846d57a3cad796053e54eddd2c937d944`. Windows visual and presented-frame behavior for 0.4.139 is untested.
