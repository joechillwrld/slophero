# 0.4.140 separate fret-lightning experiment

The user's v0.4.138 Gameplay snapshots contain ten enabled `CustomFadeProperties` scripts at the five fret colors' `lightning` and `lightning_white` objects. The existing **Disable fret flames, sparks, and lightning** switch pauses them, but also removes effects the user wants to retain. The older sampled method export does not give these scripts a separate CPU cost, so the snapshot identifies work to test rather than a measured gain.

Version 0.4.140 adds **Skip fret lightning only (keep sparks and flames)** under Sloptifine Performance. It defaults off. During an active song it uses the existing neck-effect cache and restoration path to disable the two lightning animator families and the ten matching fade scripts. Spark and flame animators stay on, including the ClonZones rainbow flame bridge. On pause, settings, or leaving Gameplay, it restores the captured enabled states. If the existing all-fret-effects switch is also on, it remains authoritative for the flame/spark branches; turning the new switch off does not reactivate lightning while the broad switch remains on. It affects the local visual effect only and can also run during online songs.

The code was built against the supplied game assemblies with zero warnings/errors and passes the existing 128 Core checks. A Windows play test must confirm lightning is absent while sparks and normal/rainbow flames still work, and that toggling off and pausing restore lightning. An identical-song PresentMon on/off comparison with the broad fret-effects switch off is needed to measure frame-time impact. No FPS gain is claimed.

The ZIP contains only `Mods/SlopHero.dll`; SHA-256: `198806fc51caf031f720d8e2c21e528a5e0c86ffdc277805408cfab114e3208e`.
