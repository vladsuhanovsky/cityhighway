import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const source = (path) => readFileSync(new URL("../../../../" + path, import.meta.url), "utf8");
const retention = source("Assets/CityHighways/Presentation/ReplayRetention.cs");
const tx = source("Assets/CityHighways/Presentation/ReplayIndexTransaction.cs");
const local = source("Assets/CityHighways/Presentation/ReplayLocalSanity.cs");
const io = source("Assets/CityHighways/Presentation/ReplayStorageIO.cs");
const replays = source("Assets/CityHighways/Presentation/Replays.cs");
const unit = source("Assets/CityHighways/Tests/ReplayRetentionTests.cs");
const transTests = source("Assets/CityHighways/Tests/ReplayIndexTransactionTests.cs");
const localTests = source("Assets/CityHighways/Tests/ReplayLocalSanityTests.cs");
const diskTests = source("Assets/CityHighways/Tests/ReplayStorageIOTests.cs");
const cloudTests = source("Assets/CityHighways/Tests/SocialReplaySafetyTests.cs");
const workflow = source(".github/workflows/social-replay-validation.yml");

test("retention policy is pure and deterministic", () => {
  assert.match(retention, /public static List<Replays\.Entry> Select\(/);
  assert.match(retention, /ReplayStorageIO\.IsSafeId\(e\.id\)/);
  assert.match(retention, /GroupBy\(e => e\.id, StringComparer\.Ordinal\)/);
  assert.match(retention, /ThenBy\(e => e\.id, StringComparer\.Ordinal\)/);
  assert.doesNotMatch(retention, /File\.Delete|WriteAtomic|File\.Replace/);
});

test("author best score is retained for each mode and map up to hard quota", () => {
  assert.match(retention, /e\.author \?\? ""\)\.ToLowerInvariant\(\)/);
  assert.match(retention, /OrderByDescending\(e => e\.authorScore\)/);
  assert.match(retention, /\.Take\(keep\)\.ToList\(\)/);
  assert.match(unit, /ABestScoreIsProtectedAgainstNewerLowerScores/);
  assert.match(unit, /MoreDistinctBestScoreGroupsThanQuotaRetainsNewestGroups/);
});

test("retention removal list never includes unsafe or duplicate disk IDs", () => {
  assert.match(retention, /public static List<string> Removed\(/);
  assert.match(retention, /ReplayStorageIO\.IsSafeId\(e\.id\) && !kept\.Contains\(e\.id\)/);
  assert.match(retention, /\.Distinct\(StringComparer\.Ordinal\)\.ToList\(\)/);
  assert.match(unit, /RemovalPlanNeverContainsUnsafeIds/);
});

test("transaction plans all changes without deleting files", () => {
  assert.match(tx, /public sealed class ReplayIndexTransaction/);
  assert.match(tx, /Entries = ReplayRetention\.Select\(before, keep\)/);
  assert.match(tx, /RemovedIds = ReplayRetention\.Removed\(before, Entries\)/);
  assert.doesNotMatch(tx, /File\.Delete|File\.Move|File\.Replace/);
});

test("file cleanup cannot happen before durable index persistence", () => {
  const index = tx.indexOf("public bool Commit(");
  const tail = tx.slice(index);
  const persisted = tail.indexOf("if (!persist(Entries)) return false;");
  const deleted = tail.indexOf("deleteCommitted?.Invoke(RemovedIds)");
  assert.ok(persisted >= 0 && deleted > persisted);
  assert.match(transTests, /FailedIndexCommitNeverDeletesExistingReplayFiles/);
  assert.match(transTests, /SuccessfulIndexCommitDeletesOnlyAfterPersistence/);
});

test("replay save writes the JSON first, then index, then performs cleanup", () => {
  const save = replays.slice(replays.indexOf("public static bool Store("),
    replays.indexOf("static void DeletePruned("));
  assert.match(save, /ReplayStorageIO\.RequireUtf8Size\(payload, ReplayStorageIO\.MaxLocalReplayBytes\)/);
  assert.match(save, /ReplayStorageIO\.WriteAtomic\(FileOf\(replay\.id\), payload\)/);
  assert.match(save, /transaction\.Commit\(/);
  assert.match(save, /if \(!transaction\.Commit\([\s\S]*?\)\) return false;/);
  assert.match(replays, /_index = new Index \{ entries = transaction\.Entries \};/);
});

test("local replay payload is byte bounded before any disk mutation", () => {
  assert.match(io, /public static void RequireUtf8Size\(string content, int maxBytes\)/);
  assert.match(io, /new UTF8Encoding\(false, true\)\.GetByteCount\(content\)/);
  assert.match(io, /throw new InvalidDataException\("Replay JSON exceeds permitted byte size\."\)/);
  assert.match(diskTests, /Utf8SizeGuardRejectsMultibyteOverflow/);
});

test("new index files also obey the same bounded-byte quota", () => {
  assert.match(replays, /ReplayStorageIO\.RequireUtf8Size\(json, ReplayStorageIO\.MaxIndexBytes\)/);
  assert.match(replays, /ReplayStorageIO\.WriteAtomic\(IndexPath, json\)/);
  assert.match(replays, /static bool SaveIndex\(Index index\)/);
});

test("recovery prunes through transaction after rebuilding orphan catalog", () => {
  assert.match(replays, /int recovered = RecoverOrphans\(_index\)/);
  assert.match(replays, /new ReplayIndexTransaction\(_index\.entries, Keep\)/);
  assert.match(replays, /transaction\.Commit\(/);
  assert.match(replays, /if \(transaction\.Commit\([\s\S]*?\)\)[\s\S]*?_index\.entries = transaction\.Entries/);
});

test("failed index writes leave unindexed replay recoverable", () => {
  assert.match(replays, /if \(!transaction\.Commit\([\s\S]*?\)\) return false;/);
  assert.match(replays, /On index failure the file is an orphan, recoverable at the/);
  assert.match(transTests, /FailedCommitCanBeRetriedWithoutLosingRemovalPlan/);
});

test("replay shape validator rejects missing map, config, invalid player arrays", () => {
  assert.match(local, /replay\.start\.config == null/);
  assert.match(local, /replay\.start\.players\.Length < 2 \|\| replay\.start\.players\.Length > 8/);
  assert.match(local, /replay\.scores\.Length != replay\.players\.Length/);
  assert.match(local, /replay\.authorSeat < -1 \|\| replay\.authorSeat >= replay\.players\.Length/);
  assert.match(localTests, /PlayerAndScoreArrayMismatchesAreRejected/);
});

test("local unfinished and legacy replays remain readable", () => {
  assert.match(local, /replay\.history == null \|\| replay\.history\.Length > Replays\.MaxCloudReplayActions/);
  assert.match(local, /replay\.humans != null && replay\.humans\.Length != replay\.players\.Length/);
  assert.match(localTests, /UnfinishedGameWithEmptyHistoryAndUnknownAuthorIsReadable/);
  assert.match(localTests, /LegacyLocalReplayWithoutOptionalCharactersOrHumanArrayIsReadable/);
});

test("both local and downloaded replays pass consistent playability checks", () => {
  assert.match(replays, /if \(!ReplayLocalSanity\.IsReadable\(replay\)\) return false;/);
  assert.match(replays, /replay\.id == id && ReplayLocalSanity\.IsReadable\(replay\)/);
  assert.match(replays, /ValidCloudReplay\(replay\) && ReplayLocalSanity\.IsReadable\(replay\)/);
  assert.match(cloudTests, /RejectsCloudReplayWithMissingGameConfiguration/);
});

test("transaction tests cover rollback, successful commit and many-record retention", () => {
  assert.match(transTests, /FailedIndexCommitNeverDeletesExistingReplayFiles/);
  assert.match(transTests, /FailedCommitCanBeRetriedWithoutLosingRemovalPlan/);
  assert.match(transTests, /EveryCommittedIndexRemainsWithinQuota/);
  assert.match(unit, /QuotaNeverExceedsSixtyForManyUniqueAuthors/);
});

test("both GitHub replay guard triggers include new transaction sources", () => {
  for (const name of ["ReplayRetention.cs", "ReplayIndexTransaction.cs",
    "ReplayLocalSanity.cs", "ReplayRetentionTests.cs",
    "ReplayIndexTransactionTests.cs", "ReplayLocalSanityTests.cs"]) {
    assert.equal(workflow.split(name).length - 1, 2, name);
  }
});
