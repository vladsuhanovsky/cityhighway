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


## Unity Profiler CPU Timeline markers

Alongside CSV checkpoints, the same generation flow now uses sixteen static **Unity CPU ProfilerMarker** instances. Their names start with `City3D.Build/` and correspond to the sixteen stage labels in the CSV.

1. In Unity Editor, open **Window → Analysis → Profiler** before creating the 3D city.
2. Enable recording and select **CPU Usage → Timeline**.
3. Generate a 3D city in Play Mode. Find the `City3D.Build/` samples on the main thread in the frame where it was constructed.
4. Expand the slowest stage to inspect its actual child calls, main-thread work and GC allocations. Repeat the same map and quality level when comparing versions.

Marker scopes are exception-safe (`ProfilerMarker.Auto()`) and close even if scene construction throws. They do not change the generated geometry, load order, scene objects or save schema.

**Scope note:** The timeline marker surrounds the main work of each phase. A few lightweight setup operations (notably tuning/exclusion lookup before the planner call) remain outside that marker but inside the corresponding CSV wall-clock interval; total timings may differ. Timeline recording itself also introduces overhead. The wall-clock CSV and the Profiler are **complementary diagnostics**, not interchangeable CPU/GPU/FPS measurements.

Do not use synthetic append timings as proof of whole-city improvement. Use these stage markers to investigate the actual longest generation phase, then repeat captures under controlled conditions.
