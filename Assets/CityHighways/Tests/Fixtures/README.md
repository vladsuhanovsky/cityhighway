# Legacy save fixtures

`LegacySaveFixtures.Snapshot` creates deterministic version-tagged JSON snapshots
from `TestCities.Small()` and serializes them through Unity's `JsonUtility`.
The tests exercise the compatibility fallback in `Game.Load` for versions
2, 4, 10, and 13.

**Limit:** These are generated compatibility fixtures. They are *not* authentic
serialized files emitted by old versions of City Highways. Before declaring
backward compatibility validated, capture real saves from released older builds
and add them here as read-only golden fixtures, with expected outcomes.

The tests intentionally do not modify shipped player saves.
