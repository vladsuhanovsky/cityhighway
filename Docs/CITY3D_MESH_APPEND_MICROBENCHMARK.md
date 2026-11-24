# Manual MeshBuilder.Append CPU microbenchmark

Run **City Highways → 3D Objects → Benchmark MeshBuilder Append** in the Unity Editor. Results are logged to the Console; the benchmark does not modify scenes, saves or game objects.

Each trial creates a destination MeshBuilder and appends the same twelve-quad source 250 times. It exercises three fixed transforms: identity, translation, and rotation with nonuniform scale. Two warmup runs are discarded; seven measurements are sorted, with median, range and average microseconds per append reported. The expected vertex count is checked after each run.

## Interpretation

- Run on the same machine, Unity version and Editor workload before and after a change; use the same benchmark configuration.
- These numbers include destination creation and buffer growth; they measure synthetic managed CPU wall time, **not** isolated GPU time, end-to-end city generation or FPS.
- Because the benchmark runs in the Editor, JIT, GC, Editor activity and OS scheduling can introduce noise. Use repeated comparisons, then cross-check changes in the Unity Profiler on representative seeded 3D cities.
- Profiling **GC Alloc**, **CPU time** and **peak memory** in the real scene is still necessary. Do not claim a performance gain based on these microbenchmarks alone.

The menu command intentionally is not an automated CI assertion: performance tests on shared CI runners are noisy and should not gate correctness checks.

## CSV export

After all three scenarios complete, the Editor offers a Save dialog. Cancel skips saving and leaves the Console result intact. When accepted, the CSV records every **unsorted** measurement in collection order, with UTC capture date, Unity version, scenario, run number, elapsed milliseconds, source-quad count and append count. Values are invariant-culture and CSV-escaped, suitable for later spreadsheet comparison. Capture revision/commit SHA separately in the file name or experiment journal: the exporter does not know the exact source revision.

Do not compare CSV values from different hardware, Unity versions or workloads as if they were controlled A/B results. The original samples remain separate instead of being pooled into one unexplained mean.


## Compare baseline and current CSVs

Open **City Highways → 3D Objects → Compare MeshBuilder Append CSV** in Unity Editor. Load a CSV captured *before* a change and one captured *after* it, then click **Compare median run times**. The tool uses each scenario's median measured **elapsed milliseconds** (not a pooled average) and reports:

- Baseline median, current median, and measured run count for each scenario.
- Percentage change in elapsed time: (current - baseline) / baseline × 100. Negative means the synthetic benchmark completed faster; positive means slower.
- Exact scenario labels, including quoted commas and line breaks, are parsed from the CSV.

The comparator refuses files with different **Unity versions**, source-quad counts, appends per run, scenario sets or measured run counts. It also rejects malformed CSV quoting, missing/duplicate run numbers, invalid values, inconsistent metadata within one capture and zero baseline medians. It permits different capture timestamps and scenario row ordering. Comparing workload metadata does **not** verify that the machine, code revision, Editor workload, power mode or graphics settings matched.

For a meaningful experiment, record the Git commit hashes and hardware alongside the CSV filenames, repeat both measurements under controlled conditions, and review the range/variation of raw runs. A single median difference is not a confidence interval and does not establish a real-game FPS or GPU performance change.

This comparison is advisory: it is deliberately **not** an automated CI performance gate.


## Save a review for pull requests

The CSV comparison window also shows the minimum–maximum observed duration of each scenario in both captures. This gives context for differences in the median; a narrow median change with wide ranges should not be treated as reliable evidence of improvement.

After loading and comparing compatible captures, enter the **baseline Git revision**, **current Git revision**, and **machine / Editor settings**. These labels are **manually provided**, not derived or verified from CSV metadata. Click **Save comparison review (.md)** to export a shareable Markdown file listing source CSV filenames, experiment labels, workload parameters, per-scenario sample counts, median durations, ranges and elapsed-time changes.

The save button requires all three manual provenance fields. You can attach the resulting Markdown report to an optimization pull request, together with both raw CSV exports; preserve the original captures to allow independent review. File export does not run additional benchmarks and never rewrites the game's save schema.

**Important:** benchmark revisions and environment fields are self-reported, and the CSV comparator checks only compatible recorded workload metadata. Neither saved reviews nor synthetic results establish frame-rate improvements in the game. Confirm in representative seeded 3D scenes with Unity Profiler and repeated captures.
