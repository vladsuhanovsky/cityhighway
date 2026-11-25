# 3D crosswalk regression baseline

Source: [PR #137 Edit Mode run #529](https://github.com/vladsuhanovsky/cityhighway/actions/runs/45702691387), [artifact 15064877691](https://github.com/vladsuhanovsky/cityhighway/actions/runs/45702691387/artifacts/15064877691), commit f9d1bd6f8f07ef7f5697eb70a2dd7a3c2f4f113c. Downloaded and examined the four actual JSON reports. Record the stable counts independently of artifact expiry.

| Requested seed | Actual seed | Legacy crosswalk envelope pairs | Other warnings |
|---|---|---:|---:|
| 1 | 1 | 35 | 0 |
| 7 | 7 | 36 | 0 |
| 42 | 42 | 24 | 0 |
| 2026 | 2026 | 23 | 0 |
| Total | | 118 | 0 |

Example reproductions: seed 1 node g00 angle 35.7473°, seed 7 node g04 angle 29.8421°, seed 42 node g26 angle 32.7365°, seed 2026 node g32 angle 24.8633°. The metric is envelope intersection, not a screenshot or a count of painted stripe intersections.

Acceptance: selected envelopes must be pairwise disjoint; keep full-width markings on orthogonal junctions; deterministic selection; candidate = drawn + suppressed; no graph/rules/save changes. Inspect junctions in the actual 3D scene, including several camera angles and zoom levels. Check the visual tradeoff of omitted zebras before considering a more detailed adaptive junction geometry. River/hub/bridge changes are not justified by these four reports alone.
