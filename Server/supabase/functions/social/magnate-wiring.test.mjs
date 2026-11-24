import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const root = "../../../../";
const load = (path) => readFileSync(new URL(root + path, import.meta.url), "utf8");
const engine = load("Assets/CityHighways/Core/Logistics/LogisticsGame.cs");
const identity = load("Assets/CityHighways/Core/Logistics/MagnateRunIdentity.cs");
const client = load("Assets/CityHighways/Presentation/SocialCloud.cs");
const server = readFileSync(new URL("index.ts", import.meta.url), "utf8");

test("every LogisticsGame owns a stable run ID instead of each report generating one", () => {
  assert.match(engine, /public string ReportRunId \{ get; \} = MagnateRunIdentity\.Create\(\)/);
  assert.match(identity, /Guid\.NewGuid\(\)\.ToString\("N"\)/);
  assert.doesNotMatch(client, /runId = MagnateRunIdentity\.Create\(\)/);
});

test("Unity JSON request has a runId field and the client sends the game property", () => {
  assert.match(client, /public string runId;/);
  assert.match(client, /runId = game\.ReportRunId/);
  assert.match(client, /mode = "magnate"/);
});

test("client sends Magnate results only after match completion", () => {
  assert.match(client, /game\.Phase != MagnatePhase\.GameOver/);
  assert.match(client, /game\.Players\[humanIndex\]\.IsBot/);
  assert.match(client, /MagnateRunIdentity\.IsValid\(game\.ReportRunId\)/);
});

test("server binds Magnate ID validation before any record or club database writes", () => {
  const validation = server.indexOf("gameFingerprint = await magnateReportFingerprint(body)");
  const bestWrite = server.indexOf("await upsertPersonalBest(db, me, row)");
  const contribution = server.indexOf("await creditClubGameOnce(db,");
  assert.ok(validation > 0);
  assert.ok(bestWrite > validation);
  assert.ok(contribution > validation);
});

test("all club contributions use the same transactional receipt operation", () => {
  assert.match(server, /await creditClubGameOnce\(db,/);
  assert.doesNotMatch(server, /db\.rpc\("club_contribute"/);
});

test("Magnate does not overwrite Classic or Coop record tables", () => {
  assert.match(server, /if \(mode === "classic" \|\| mode === "coop"\) \{\s*const row =/);
  assert.match(server, /gameFingerprint = await magnateReportFingerprint\(body\)/);
});
