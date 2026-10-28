# v0.4.129 Windows follow-up, 2026-10-27

This note covers the 20:56–21:01 `Latest.log` and three PresentMon CSV pairs.

## Rainbow failure and repair

The log repeatedly reports that rainbow notes tracked 2,000 pooled sprites,
then about one second later stopped with `Method unstripping failed`. The local
`GuitarPlayer.GetComponentsInChildren<GuitarNoteContainer>(true)` scan worked;
the next scan found no newly added renderers and fell through to
`Resources.FindObjectsOfTypeAll<GuitarNoteContainer>()`, whose generic entry
point is stripped in this game. Version 0.4.130 removes that fallback. It
continues discovering the local pool, backs off to five-second checks after
the initial population, and does not write colors to inactive pooled notes.

The timing snapshot also shows `disableFretEffects=True`. Version 0.4.129
suppressed rainbow flames whenever that option was enabled, and its suppression
path disabled flame renderers as well as sparks and lightning. Version 0.4.130
lets rainbow flames take precedence for the five guitar flame animators only;
sparks and lightning remain suppressed. That preserves the requested visual,
but those flame animators retain their normal CPU work while the rainbow option
is enabled.

## PresentMon captures

All three runs are about 30 seconds from PID 7480 in Hardware Independent Flip
mode with tearing allowed and sync interval zero. The CSV measures application
presents; these rates do not establish the monitor's displayed refresh rate.

| Capture | Average app presents/s | 1% low | P99 present interval | Mean CPU busy | Mean GPU busy |
| --- | ---: | ---: | ---: | ---: | ---: |
| 20:56:12 | 1,733.28 | 1,345.19 | 0.743 ms | 0.550 ms | 0.227 ms |
| 20:56:53 | 1,706.18 | 1,312.43 | 0.762 ms | 0.559 ms | 0.237 ms |
| 20:59:29 | 1,730.67 | 1,340.90 | 0.746 ms | 0.551 ms | 0.226 ms |

The first run lacks the native held-glow activation message; it is active in
the latter two. The input-viewer switch was not logged in v0.4.129, so its
state cannot be matched to a capture. Other solo options were enabled in these
runs, and chart sections changed. The 1.6% spread from the first to second run
is not repeatable in the third. No isolated FPS or frame-pacing benefit is
established for either option.

CPU busy averages about 0.55 ms per app present while GPU busy averages about
0.23 ms, so this trace does not point to GPU saturation. The log's internal
timing still reports roughly 3,320–3,449 simulation Updates/s and 1,660–1,725
scheduled render callbacks/s; those are callback rates, not displayed FPS. It
also records the regular-sustain cache skipping 3,753–4,480 repeat submissions
per 30-second run. That confirms the cache sees work, but not how much CPU time
it saves. The WPA camera/culling stacks remain shared Unity work without safe
camera attribution. Keep those options experimental and do not claim gains.

## Cross-capture check: prior high scores are camera benchmarks

I also checked the older PresentMon files against their matching logs. The
1,860 presents/s capture from 00:26:31 and 1,937 presents/s capture from
00:27:14 on 2026-10-26 are not full-visual gameplay baselines: the first has
the HUD-camera benchmark active (hiding camera-rendered HUD), and the second
has the guitar-camera benchmark active (hiding notes, highway, and 3D effects).
Those numbers cannot be used to claim the normal-play 1,706–1,733 captures are
a regression. The older 2026-10-25 run at 1,630 presents/s appears to be
ordinary gameplay with black-background mode, but its chart and option state
differ, so it is directional only.

For the three latest captures, frame intervals over 2 ms number 24, 21, and
28; none exceeds 4 ms. Every frame over 3 ms is CPU busy for about 99% of its
present interval, while GPU busy averages 0.26 ms on those same frames. The
largest CPU-bound frames recur around similar capture offsets (roughly 5–12 s
and later in the captures), but logs do not identify a matching game callback
or chart event. This is a lead for time-correlated sampling, not proof of a
beat/event-driven game cost. Mean CPU busy remains about 0.55 ms versus 0.23 ms
GPU busy per present, keeping the main-thread/render-submit path ahead of
shader work as the optimization target.

The matching logs also show why the three latest captures cannot validate the
individual options: the 80-unit guitar far plane, EventSystem pause, legacy
mouse pause, solo cheat-scanner pause, native sustain cache, and worker-count
change were combined; held-glow filtering differs between runs, and the
input-viewer switch was not recorded. The native sustain cache skipped about
4,000 property-block submissions per full song, but submission counts alone
do not establish saved CPU time. No single one of these toggles can be credited
with the measured frame rate from this batch.
