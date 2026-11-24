import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const root = "../../../../";
const source = (path) => readFileSync(new URL(root + path, import.meta.url), "utf8");
const client = source("Assets/CityHighways/Presentation/SocialCloud.cs");
const outbox = source("Assets/CityHighways/Presentation/SocialReportOutbox.cs");
const delivery = source("Assets/CityHighways/Presentation/SocialCloud.ReportQueue.cs");
const server = readFileSync(new URL("./index.ts", import.meta.url), "utf8");

test("social reports enqueue instead of submitting once and forgetting failures", () => {
  assert.match(client, /QueueReport\(request, replay\.mode \+ ":" \+ replay\.id\)/);
  assert.match(client, /QueueReport\(new Request[\s\S]*?"magnate:" \+ game\.ReportRunId\)/);
  assert.doesNotMatch(client, /Send<ReportResponse>\(request, AfterReport\)/);
});

test("game reports are accepted into the outbox before profile cloud sync", () => {
  assert.match(delivery, /steam != null \? steam\.SteamId : null/);
  assert.match(delivery, /SocialReportOutbox\.ValidOwner\(owner\)/);
  assert.doesNotMatch(delivery, /CloudProfiles\.ServerId/);
});

test("fresh web API ticket is fetched only by send transport, never persisted", () => {
  const assign = delivery.indexOf("request.ticket = null");
  const serialize = delivery.indexOf("JsonUtility.ToJson(request)");
  assert.ok(assign >= 0 && serialize > assign);
  assert.match(client, /steam\.RequestWebApiTicket\(TicketIdentity, ticket =>/);
  assert.match(client, /if \(expectedSteamId != null && steam\.SteamId != expectedSteamId\)/);
});

test("outbox uploads the stored report only when its current owner is online", () => {
  assert.match(delivery, /ReportOutbox\.Next\(steam\.SteamId, DateTime\.UtcNow\.Ticks\)/);
  assert.match(delivery, /expectedSteamId: entry\.ownerSteamId/);
  assert.match(outbox, /e\.ownerSteamId == owner && e\.nextAttemptUtcTicks <= nowTicks/);
});

test("outbox is removed only on success and retries with HTTP backoff", () => {
  assert.match(delivery, /Send<ReportResponse>\(request, response =>[\s\S]*?ReportOutbox\.Complete\(entry\)/);
  assert.match(delivery, /reportFailure: \(message, status\) =>[\s\S]*?ReportOutbox\.Reschedule\(entry, DateTime\.UtcNow\.Ticks, status\)/);
});

test("queue persistence remains bounded and has a backup file", () => {
  assert.match(outbox, /public const int MaxEntries = 24/);
  assert.match(outbox, /public const int MaxPayloadBytes = 740 \* 1024/);
  assert.match(outbox, /File\.Replace\(temp, _path, _path \+ "\.bak"\)/);
});

test("the delayed-upload flag is not a new way to credit the club", () => {
  assert.match(client, /public bool skipClub;/);
  assert.match(delivery, /request\.skipClub = !SocialReportOutbox\.SameUtcWeek\(/);
  assert.match(server, /if \(member && body\.skipClub !== true\)/);
  assert.match(server, /await upsertPersonalBest\(db, me, row\)/);
});

test("UTC Monday boundary, not a rolling 7 day window, controls club credit", () => {
  assert.match(outbox, /\(\(int\)started\.DayOfWeek \+ 6\) % 7/);
  assert.match(outbox, /started\.AddDays\(-startOffset\) == now\.AddDays\(-nowOffset\)/);
});

test("transport includes an isolated result-specific failure callback", () => {
  assert.match(client, /Action<string, long> reportFailure = null/);
  assert.match(client, /reportFailure\?\.Invoke\(Problem, web\.responseCode\)/);
});

test("the server never accepts legacy duplicate Magnate reports without run IDs", () => {
  assert.match(server, /gameFingerprint = await magnateReportFingerprint\(body\)/);
  assert.match(server, /await creditClubGameOnce\(db,/);
});
