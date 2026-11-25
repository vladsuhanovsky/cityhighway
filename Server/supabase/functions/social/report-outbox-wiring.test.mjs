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
  assert.match(delivery, /long nowTicks = DateTime\.UtcNow\.Ticks;/);
  assert.match(delivery, /ReportOutbox\.Next\(steam\.SteamId, nowTicks\)/);
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


test("outbox enqueue, completion and backoff roll back when disk writes fail", () => {
  assert.match(outbox, /_entries\.Add\(entry\);[\s\S]*?if \(Save\(\)\) return true;[\s\S]*?_entries\.Remove\(entry\)/);
  assert.match(outbox, /_entries\.RemoveAt\(index\);[\s\S]*?if \(Save\(\)\) return true;[\s\S]*?_entries\.Insert\(index, entry\)/);
  assert.match(outbox, /int previousAttempts = entry\.attempts;[\s\S]*?if \(Save\(\)\) return true;[\s\S]*?entry\.attempts = previousAttempts/);
});

test("outbox recovery retains a known-good backup when the primary is corrupt", () => {
  assert.match(outbox, /_recoveredFromBackup = candidate != _path/);
  assert.match(outbox, /if \(_recoveredFromBackup\)[\s\S]*?File\.Delete\(_path\)[\s\S]*?File\.Move\(temp, _path\)/);
  assert.match(outbox, /Encoding\.UTF8\.GetByteCount\(json\) > MaxFileBytes/);
});

test("server ACKs with failed local removal are retried on disk, not resent on HTTP", () => {
  assert.match(delivery, /static SocialReportOutbox\.Entry _acknowledgedPending/);
  assert.match(delivery, /if \(_acknowledgedPending != null\)[\s\S]*?ReportOutbox\.Complete\(_acknowledgedPending\)/);
  assert.match(delivery, /if \(!ReportOutbox\.Complete\(entry\)\)[\s\S]*?_acknowledgedPending = entry;[\s\S]*?DeferAfterDiskError\(\)/);
});

test("failed retry persistence has an in-memory delay instead of a per-frame HTTP storm", () => {
  assert.match(delivery, /if \(nowTicks < _diskRetryAfterUtcTicks\) return/);
  assert.match(delivery, /if \(!ReportOutbox\.Reschedule\(entry, DateTime\.UtcNow\.Ticks, status\)\)[\s\S]*?DeferAfterDiskError\(\)/);
});

test("null report response is not acknowledged as an uploaded game", () => {
  assert.match(delivery, /if \(response == null\)[\s\S]*?ReportOutbox\.Reschedule\(entry, DateTime\.UtcNow\.Ticks, 0\)/);
});


const ack = source("Assets/CityHighways/Presentation/SocialReportAck.cs");

test("report ACK validation runs before the generic JSON deserializer can supply default primitives", () => {
  assert.match(client, /if \(request\.action == "report"\)[\s\S]*?SocialReportAck\.TryParse\(text, out var ack\)/);
  assert.match(client, /if \(!SocialReportAck\.TryParse\(text, out var ack\)\)[\s\S]*?reportFailure\?\.Invoke\(Problem, 0\);[\s\S]*?return;/);
  assert.match(client, /done\?\.Invoke\(ack as T\)/);
});

test("report ACK validation requires typed best, improved and explicit null or club details", () => {
  assert.match(ack, /root\.TryGetValue\("improved"/);
  assert.match(ack, /root\.TryGetValue\("best"/);
  assert.match(ack, /root\.TryGetValue\("club"/);
  assert.match(ack, /ReadInt\(rawBest, -100, 1000, out _\)/);
  assert.match(ack, /club\.TryGetValue\("amount"/);
  assert.match(ack, /club\.TryGetValue\("closed"/);
  assert.match(ack, /club\.TryGetValue\("logo"/);
});

test("ACK reader rejects duplicate keys, oversized payloads and nested depth bombs", () => {
  assert.match(ack, /json\.Length > MaxCharacters/);
  assert.match(ack, /const int MaxDepth = 12/);
  assert.match(ack, /fields\.ContainsKey\(key\)/);
  assert.match(ack, /ReadValue\(depth, out var item\)/);
});

test("Steam account switches during HTTP report upload do not show another user's result", () => {
  assert.match(delivery, /if \(NetPlatforms\.Steam\?\.SteamId == entry\.ownerSteamId\)[\s\S]*?AfterReport\(response\)/);
  assert.match(delivery, /if \(!ReportOutbox\.Complete\(entry\)\)/);
});

test("existing server sends expected ACK fields even for report without club membership", () => {
  assert.match(server, /return \{ improved, best, club \};/);
  assert.match(server, /club = \{ kind, amount: accepted \? amount : 0, closed, logo \};/);
});


test("null club acknowledgement is reconstructed as null instead of Unity JsonUtility phantom object", () => {
  assert.match(ack, /SocialCloud\.ReportClub clubAck = null/);
  assert.match(ack, /if \(rawClub != null\)[\s\S]*?clubAck = new SocialCloud\.ReportClub/);
  assert.match(ack, /response = new SocialCloud\.ReportResponse[\s\S]*?club = clubAck/);
  assert.doesNotMatch(ack, /JsonUtility\.FromJson<SocialCloud\.ReportResponse>/);
});
