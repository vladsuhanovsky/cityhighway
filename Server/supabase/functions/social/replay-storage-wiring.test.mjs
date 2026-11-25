import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = (path) => readFileSync(new URL("../../../../" + path, import.meta.url), "utf8");
const io = source("Assets/CityHighways/Presentation/ReplayStorageIO.cs");
const replays = source("Assets/CityHighways/Presentation/Replays.cs");
const unit = source("Assets/CityHighways/Tests/ReplayStorageIOTests.cs");
const workflow = source(".github/workflows/social-replay-validation.yml");

test("replay file names use a bounded portable allowlist", () => {
  assert.match(io, /public const int MaxIdLength = 80;/);
  assert.match(io, /if \(string\.IsNullOrEmpty\(id\) \|\| id\.Length > MaxIdLength\)/);
  assert.match(io, /c != '-' && c != '_'/);
  assert.match(io, /public static string PathFor\(string folder, string id\)/);
  assert.match(io, /!IsSafeId\(id\)/);
});

test("index metadata and Windows reserved devices cannot be overwritten", () => {
  assert.match(io, /upper == "INDEX"/);
  assert.match(io, /upper == "CON"/);
  assert.match(io, /upper == "NUL"/);
  assert.match(io, /upper\.StartsWith\("COM"/);
  assert.match(io, /upper\.StartsWith\("LPT"/);
  assert.match(unit, /RejectsUnsafeOrReservedReplayIds/);
});

test("all local file paths use validated IDs", () => {
  assert.match(replays, /static string FileOf\(string id\) => ReplayStorageIO\.PathFor\(Folder, id\);/);
  assert.match(replays, /if \(replay == null \|\| !ReplayStorageIO\.IsSafeId\(replay\.id\)\) return false;/);
  assert.match(replays, /public static bool Exists\(string id\) => ReplayStorageIO\.IsSafeId\(id\)/);
  assert.match(replays, /_index\.entries\.RemoveAll\(e => e == null/);
  assert.match(replays, /!ReplayStorageIO\.IsSafeId\(e\.id\)/);
});

test("cloud replay IDs follow the same rule as local replay IDs", () => {
  assert.match(replays, /!ReplayStorageIO\.IsSafeId\(replay\.id\)/);
  assert.match(unit, /DownloadedCloudReplayCannotUseTraversalId/);
});

test("on-disk replay and index read limits are enforced on actual bytes", () => {
  assert.match(io, /MaxIndexBytes = 512 \* 1024/);
  assert.match(io, /MaxLocalReplayBytes = 8 \* 1024 \* 1024/);
  assert.match(io, /while \(\(count = input\.Read\(buffer, 0, buffer\.Length\)\) != 0\)/);
  assert.match(io, /if \(output\.Length > maxBytes - count\)/);
  assert.match(replays, /ReadBoundedUtf8\(IndexPath, ReplayStorageIO\.MaxIndexBytes\)/);
  assert.match(replays, /ReadBoundedUtf8\(FileOf\(id\), ReplayStorageIO\.MaxLocalReplayBytes\)/);
});

test("local replay file decoding rejects invalid UTF-8 and supports BOM", () => {
  assert.match(io, /new UTF8Encoding\(false, true\)\.GetString\(output\.ToArray\(\)\)/);
  assert.match(io, /json\[0\] == '\\uFEFF'/);
  assert.match(unit, /BoundedReadRejectsInvalidUtf8/);
  assert.match(unit, /BoundedReadAcceptsBomAndUnicode/);
});

test("replay and index updates use temporary sibling files with atomic replacement", () => {
  assert.match(io, /path \+ "\.tmp-" \+ Guid\.NewGuid\(\)\.ToString\("N"\)/);
  assert.match(io, /File\.Replace\(pending, path, null\)/);
  assert.match(io, /File\.Move\(pending, path\)/);
  assert.match(replays, /ReplayStorageIO\.WriteAtomic\(IndexPath, JsonUtility\.ToJson\(Load\(\)\)\)/);
  assert.match(replays, /ReplayStorageIO\.WriteAtomic\(FileOf\(replay\.id\), JsonUtility\.ToJson\(replay\)\)/);
  assert.doesNotMatch(replays, /File\.WriteAllText\((?:IndexPath|FileOf)/);
});

test("damaged indexes are repaired with safe deduplication", () => {
  assert.match(replays, /if \(_index\.entries == null\)/);
  assert.match(replays, /var unique = new HashSet<string>\(StringComparer\.Ordinal\)/);
  assert.match(replays, /!unique\.Add\(e\.id\)/);
  assert.match(replays, /RecoverOrphans\(_index\) > 0 \|\| repaired/);
});

test("valid orphan replay files are recovered after interrupted index writes", () => {
  assert.match(replays, /static int RecoverOrphans\(Index index\)/);
  assert.match(replays, /ReplayStorageIO\.UnindexedIds\(Folder, known\)/);
  assert.match(io, /Directory\.EnumerateFiles\(folder, "\*\.json", SearchOption\.TopDirectoryOnly\)/);
  assert.match(io, /MaxRecoveryScan = 300/);
  assert.match(io, /if \(\+\+scanned > MaxRecoveryScan\) yield break;/);
  assert.match(io, /IsSafeId\(id\) && !indexedIds\.Contains\(id\)/);
  assert.match(replays, /index\.entries\.Add\(EntryOf\(replay\)\)/);
});

test("reading a replay verifies its embedded ID matches its filename", () => {
  assert.match(replays, /replay\.start != null && replay\.id == id/);
  assert.match(replays, /if \(!Exists\(id\)\) return null;/);
});

test("recording a new replay does not reuse a four-digit random identifier", () => {
  assert.match(replays, /Guid\.NewGuid\(\)\.ToString\("N"\)\.Substring\(0, 16\)/);
  assert.doesNotMatch(replays, /UnityEngine\.Random\.Range\(1000, 9999\)/);
  assert.match(replays, /DateTime\.UtcNow\.ToString\("yyyyMMdd-HHmmss"\)/);
});

test("retention limit protects no more than 60 distinct best-score records", () => {
  assert.match(replays, /public const int Keep = 60;/);
  assert.match(replays, /\.Take\(Keep\)\.Select\(e => e\.id\)/);
  assert.match(replays, /if \(index\.entries\.Count <= Keep\) break;/);
});

test("NUnit coverage and social CI include disk persistence and recovery wiring", () => {
  assert.match(unit, /AtomicallyCreatesAndReplacesExistingReplay/);
  assert.match(unit, /BoundedReadRejectsFileOverLimit/);
  assert.match(unit, /ValidIdStaysInsideFolder/);
  assert.match(workflow, /ReplayStorageIO\.cs/);
  assert.match(workflow, /ReplayStorageIOTests\.cs/);
});

test("actual recovery candidate NUnit tests use isolated temporary files", () => {
  assert.match(unit, /RecoveryScansOnlySafeUnindexedReplayNames/);
  assert.match(unit, /RecoveryScanHandlesMissingFolder/);
  assert.match(unit, /CollectionAssert\.AreEquivalent\(new\[\] \{ "orphan" \}, candidates\)/);
});
