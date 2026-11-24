# Measure real City3DScene generation stages

This is a one-time wall-clock breakdown of the actual scene-building path, not a synthetic mesh microbenchmark. `City3DScene.Build` records monotonic `Stopwatch.GetTimestamp()` checkpoints at 16 stage boundaries. All phases are measured in sequence; their durations add up to the reported **Build total**. No random seed, map placement, mesh generation or save data is changed.

## Using the Unity Editor

1. Enter Play Mode and open a 3D city. A completed `City3DScene` keeps the latest timing report in `ActiveScene.GenerationTiming`.
2. Open **City Highways → 3D Objects → Generation Stage Timings**.
3. Inspect durations in construction order. The window identifies the longest measured stage.
4. Click **Save stage timings CSV** for the timing rows, recorded UTC timestamp, Unity version, city, quality, stage name, milliseconds and percentage of build time.

The report begins when `Build` starts and ends after the frame monitor is attached. It **does not** include earlier render-pipeline eligibility checks, scene root construction, or all subsequent gameplay initialization. A failed build is not published as a completed timing report.

Measured stages include: rivers and relief; city planning; prefab replacements; rendering and materials; ground; decals; river meshes; district buildings; trees; props and replacement models; road setup; junctions; stations; bridges; authored scene decorations; reflections and sampling setup.

## Repeatable profiling workflow

Use the **same city, quality, scene settings, machine, Unity version and project revision** for each run. Restart the scene several times and keep the raw CSV results, recording the exact Git SHA separately. Compare the time cost of each stage, investigate unexpectedly expensive ones in **Unity Profiler** (CPU Timeline, GC allocations and relevant rendering counters), then validate any change in the real 3D scene.

These are *wall-clock creation durations*, so they include loading, Editor overhead, garbage collection, main-thread work and any scheduling interruptions; percentages are descriptive and not statistical significance estimates. The tool cannot infer improvements in FPS or GPU utilization. Timing instrumentation itself adds a small fixed overhead to each build.

The existing frame monitor and append microbenchmark measure different things and remain unchanged. This stage timing tool is read-only except for explicit CSV exports and does not change CI thresholds.
