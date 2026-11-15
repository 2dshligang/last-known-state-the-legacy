# W18 station performance

The baseline is accepted main `b6a6c017a7c1f9900102325f2f2455346d660407` with W17 (#45), W16 (#43), S18 (#40) and S17 (#35). S01 is API 1.0, S16 is disk schema 2 and S17 StationPacing API remains version 1. The production changes apply to the existing W04 generator and ECHO visual presentation.

## Measured bottlenecks and changes

The first ECHO entry showed a 40–47 ms peak in the two release-profile runs. The development probe measured 1.071 ms in Unity's `Shader.CreateGPUProgram` marker in that first ECHO region. The marker does not account for the entire transition. The held ECHO view also submitted the offscreen camera on every frame.

ECHO now reuses one built-in cube mesh and one material, creates renderer-only projection objects and renders each changed cue into the existing 640×360 texture once. The URP camera ends its render request after it draws. It uses neither shadow rendering nor an opaque/depth copy for this unlit viewport. Missing intervals suspend rendering, and texture recreation queues a fresh render if the graphics device loses the cached texture.

In the control run with the sun's shadows disabled, the repeated route dropped from 1188 to 739 p95 draw calls. The final scene keeps the W16 sun, architectural and outdoor rock shadows. The generator removes shadow casting from the six station floors and thin direction markings, for 19 renderer flags. The scene audit retained all 936 serialized identities. Collider, anchor and route checks run against the accepted W04 layout.

## Windows profile

Hardware matches the target PC. Ryzen 5 5600 (6 cores, 12 threads), RTX 2060 SUPER (8006 MiB reported by Unity), 32 GiB installed RAM and Windows 11 Pro build 26200. The player used Direct3D 11, 1920×1080, the PC quality level, VSync off, an uncapped frame rate and muted audio. Unity 6000.6.9f1, URP 17.6.0. Shader/driver caches were not cleared. A fresh process and the repeated route distinguish first-visit costs and warmed execution.

Each run sampled 160 opening frames and two 2880-frame loops. Each loop moved the camera through eleven fixed 240-frame world segments, including the two exterior legs and both airlock directions, followed by 240 frames of the installed ECHO panel. The harness moves the existing player between accepted floor poses and supplies air and water using the accepted power service. This measures rendering and transitions in a reproducible route. It is separate from the S18 gameplay regression and does not report campaign pacing.

| Release run, second loop | Wall p50 / p95 / p99 ms | CPU frame p95 ms | CPU render p95 ms | GPU p95 ms | Draw calls p95 | SetPass p95 | Frames above 16.67 ms |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Baseline 1 | 1.882 / 3.226 / 3.874 | 3.162 | 0.516 | 3.156 | 1188 | 94 | 0 / 2880 |
| Optimized 1 | 1.614 / 3.970 / 5.446 | 3.919 | 0.674 | 3.777 | 1139 | 80 | 0 / 2880 |
| Baseline 2 | 1.611 / 4.242 / 5.528 | 4.184 | 0.558 | 4.131 | 1188 | 94 | 0 / 2880 |
| Optimized 2 | 1.581 / 4.161 / 5.516 | 4.116 | 0.614 | 4.100 | 1139 | 80 | 0 / 2880 |

ECHO p95 draw calls were 370 on the baseline and 320 after the change in both pairs. The first ECHO entrance peaked at 47.335/40.223 ms before and 36.980/39.836 ms after. The paired development runs observed the shader marker once in the first ECHO region, peaking at 1.071 and 0.841 ms respectively. Development wall peaks were 34.347 and 39.072 ms. CPU/GPU time varied between runs. The repeatable gains are lower draw and SetPass counts and elimination of the continuous ECHO camera pass. The two warmed optimized loops stayed within the 16.67 ms frame budget at all sampled positions. Each first optimized loop still contained two frames over budget. Whole-campaign sustained 60 FPS and the source of the remaining first-use hitch still need human-route profiling.

Peak sampled Unity allocation was 323.99/327.91 MiB before and 323.26/323.70 MiB after. Unity reported 565,248,000 reserved bytes in the first baseline run. See per-region report memory for the other runs. The player returned zero for runtime texture usage and process working set queries. Those columns are unavailable data. Engine memory numbers are not system working-set or dedicated VRAM measurements.

FrameTimingManager layers measure CPU and GPU time asynchronously. The raw CSV includes the recorded frame index and uses zero for unavailable timings and -1 for unavailable counters. Percentiles exclude missing values. The first 16 callbacks are labeled as each transition, including any harness preparation between callbacks. Valid GPU sample counts are in the JSON. Draw-call comparisons use the release profiles. The development rendering counter advertised availability yet returned zero draw counts. Timing and shader marker meanings follow the [Unity FrameTiming reference](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/FrameTiming.html) and [shader loading manual](https://docs.unity3d.com/6000.0/Documentation/Manual/shader-loading.html).

## Visual checks

[Same-camera before/after sheet](before-after.png) shows arrival, ECHO terminal, shelter, exterior and photograph overlook. The optimized Windows build separately shows a [recorded cue](echo-recorded.png), a [model estimate](echo-model.png) and the [missing interval](echo-missing.png). Rock, doorway and photograph shadows were observed in the paired views. The ECHO labels and highlighted instrument silhouettes still distinguish observed fictional logs, the model and missing evidence.

## Reproduction

See [commands, samples and verification](verification.md). The profiling helpers live outside Assets. The ordinary W18 Windows build uses the same scene and code without the profiling helpers or the temporary Frame Timing Stats build option.

## Handoff

W19 (Developer 1) and S19 (Developer 2) can use the accepted W18 scene and existing S18 route. Preserve `quest.echo-evidence`, `echo.asterion`, the recorded/model/missing labels, `door.exterior-airlock`, and the 13 authored interaction anchors. W19 should profile a human-paced run with startup/first-use markers and include ECHO entry, lab and finish in the frame-budget review.
