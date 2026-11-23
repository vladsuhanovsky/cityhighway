# Advisory frame-sample stability analysis

The Unity **City Highways → 3D Objects → Performance Scenario Suite** window now checks variability in the live frame monitor before a capture is saved.

## How it works

After warm-up, collect at least **60 accepted frame intervals**. The window shows:

- Median frame interval in milliseconds
- P95 divided by median
- Number and percentage of frame intervals **strictly over 2× the median**

An *advisory warning* appears if **P95 / median is greater than 1.5** or **at least 5% of samples exceed 2× the median**. These are engineering heuristics, not universal performance limits. A single long frame may appear in the maximum without triggering a warning.

When saving a noisy measurement, Unity offers **Capture anyway** or **Restart sampling**. Restart resets only the live frame buffer and restarts the 2-second warm-up. Previously saved captures and baseline JSON are untouched. The existing minimum sample count, scenario label and baseline compatibility checks continue to apply.

## Scope and limitations

These statistics are computed on demand from the same most recent 600 live intervals as P95. The existing serialized performance snapshot, percentile calculation, reports, game save schemas and runtime presentation remain unchanged. This does **not** determine whether a spike came from Unity Editor overhead, rendering, GC, system load or gameplay; it is not an isolated GPU or CPU profiler. Actual procedural seed, camera and scene workload still require manual control.

## Validation

- [ ] Unity Edit Mode CI passes the extended `City3DFrameMonitorTests`
- [ ] Uniform frames have no warning; repeated slow frames flag a noisy sample
- [ ] Exactly 2× median does not count as a spike; small samples have no stability verdict
- [ ] Existing snapshots retain their original values and saved sample windows remain intact
- [ ] In Play Mode, a noisy measurement offers confirmation or restart without mutating saved suites
