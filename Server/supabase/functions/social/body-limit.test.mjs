import test from "node:test";
import assert from "node:assert/strict";
import {
  MAX_SOCIAL_REQUEST_BYTES,
  readBoundedBody,
  RequestTooLargeError
} from "./body-limit.mjs";

const fake = (chunks, headers = {}) => ({
  headers: new Headers(headers),
  body: new ReadableStream({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(chunk);
      controller.close();
    }
  })
});
const bytes = (value) => new TextEncoder().encode(value);

test("reads ordinary UTF-8 JSON request without changing its text", async () => {
  const value = '{"city":"Москва","score":70}';
  const req = new Request("https://example.test", { method: "POST", body: value });
  assert.equal(await readBoundedBody(req), value);
});

test("accepts exactly the configured request byte cap", async () => {
  const req = fake([new Uint8Array(MAX_SOCIAL_REQUEST_BYTES)]);
  const value = await readBoundedBody(req);
  assert.equal(value.length, MAX_SOCIAL_REQUEST_BYTES);
});

test("rejects a chunked request crossing the cap by one byte", async () => {
  const req = fake([
    new Uint8Array(MAX_SOCIAL_REQUEST_BYTES - 5),
    new Uint8Array(6),
  ]);
  await assert.rejects(() => readBoundedBody(req), RequestTooLargeError);
});

test("rejects large Content-Length without reading the actual stream", async () => {
  const req = fake([bytes("small")], {
    "content-length": String(MAX_SOCIAL_REQUEST_BYTES + 1)
  });
  await assert.rejects(() => readBoundedBody(req), RequestTooLargeError);
});

test("rejects malformed or negative content length", async () => {
  for (const value of ["-1", "1.2", "Infinity", "NaN", "12abc"]) {
    const req = fake([bytes("hello")], { "content-length": value });
    await assert.rejects(() => readBoundedBody(req), RequestTooLargeError);
  }
});

test("does not trust a deceptively small advertised length", async () => {
  const req = fake([new Uint8Array(129)], { "content-length": "1" });
  await assert.rejects(() => readBoundedBody(req, 128), RequestTooLargeError);
});

test("checks actual UTF-8 byte size, not JavaScript character count", async () => {
  const req = fake([bytes("Москва")]);
  await assert.rejects(() => readBoundedBody(req, 6), RequestTooLargeError);
  const smaller = fake([bytes("Москва")]);
  assert.equal(await readBoundedBody(smaller, 12), "Москва");
});

test("rejects invalid UTF-8 instead of substituting replacement characters", async () => {
  const req = fake([Uint8Array.from([0xff, 0xfe, 0xfa])]);
  await assert.rejects(() => readBoundedBody(req), TypeError);
});

test("allows absent body and denies invalid limits", async () => {
  assert.equal(await readBoundedBody({ headers: new Headers(), body: null }), "");
  await assert.rejects(() => readBoundedBody(null), TypeError);
  await assert.rejects(() => readBoundedBody(fake([]), 0), TypeError);
  await assert.rejects(() => readBoundedBody(fake([]), NaN), TypeError);
});

test("handles boundaries split across individual UTF-8 stream chunks", async () => {
  const raw = bytes("🚚 Москва");
  const req = fake([raw.slice(0, 1), raw.slice(1, 4), raw.slice(4)]);
  assert.equal(await readBoundedBody(req), "🚚 Москва");
});
