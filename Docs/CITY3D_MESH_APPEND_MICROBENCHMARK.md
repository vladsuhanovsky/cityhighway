# Manual MeshBuilder.Append CPU microbenchmark

Run **City Highways → 3D Objects → Benchmark MeshBuilder Append** in the Unity Editor. Results are logged to the Console; the benchmark does not modify scenes, saves or game objects.

Each trial creates a destination MeshBuilder and appends the same twelve-quad source 250 times. It exercises three fixed transforms: identity, translation, and rotation with nonuniform scale. Two warmup runs are discarded; seven measurements are sorted, with median, range and average microseconds per append reported. The expected vertex count is checked after each run.

## Interpretation

- Run on the same machine, Unity version and Editor workload before and after a change; use the same benchmark configuration.
- These numbers include destination creation and buffer growth; they measure synthetic managed CPU wall time, **not** isolated GPU time, end-to-end city generation or FPS.
- Because the benchmark runs in the Editor, JIT, GC, Editor activity and OS scheduling can introduce noise. Use repeated comparisons, then cross-check changes in the Unity Profiler on representative seeded 3D cities.
- Profiling **GC Alloc**, **CPU time** and **peak memory** in the real scene is still necessary. Do not claim a performance gain based on these microbenchmarks alone.

The menu command intentionally is not an automated CI assertion: performance tests on shared CI runners are noisy and should not gate correctness checks.
