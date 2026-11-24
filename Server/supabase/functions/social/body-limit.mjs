// Limit the *actual streamed bytes* before decoding or JSON parsing.
// Content-Length alone is advisory: chunked requests may omit it.
export const MAX_SOCIAL_REQUEST_BYTES = 768 * 1024;

export class RequestTooLargeError extends Error {
  constructor() {
    super("too large");
    this.name = "RequestTooLargeError";
  }
}

export async function readBoundedBody(request, limit = MAX_SOCIAL_REQUEST_BYTES) {
  if (!request || !Number.isInteger(limit) || limit < 1)
    throw new TypeError("invalid request body limit");

  const header = request.headers?.get("content-length");
  if (header !== null && header !== undefined) {
    if (!/^[0-9]+$/.test(header) || Number(header) > limit)
      throw new RequestTooLargeError();
  }
  if (!request.body) return "";

  const reader = request.body.getReader();
  const chunks = [];
  let size = 0;
  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > limit) throw new RequestTooLargeError();
      chunks.push(value);
    }
    const bytes = new Uint8Array(size);
    let offset = 0;
    for (const chunk of chunks) {
      bytes.set(chunk, offset);
      offset += chunk.byteLength;
    }
    return new TextDecoder("utf-8", { fatal: true }).decode(bytes);
  } finally {
    try { await reader.cancel(); } catch { /* completed or failed stream */ }
    try { reader.releaseLock(); } catch { /* reader already released */ }
  }
}
