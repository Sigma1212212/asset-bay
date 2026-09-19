// Settings sync: a player's menu settings, stored so they follow them to any PC.
//
//   GET  /asset-bay/settings?id=<64 hex>          -> {"settings": {...}, "updated": <ms>}  or 404
//   PUT  /asset-bay/settings  {"id": "<64 hex>", "settings": {...}}  -> {"ok": true, "updated": <ms>}
//
// `id` is SHA-256("assetbay-settings:" + sync code). The sync code is a random code made on the
// player's PC; it's never sent here, and nothing identifies the player (not their Gorilla Tag id,
// which other players can see). Knowing the code = owning the settings, so it's shown only in the
// menu's own safe-mode window. One Durable Object per id; capped size and write rate.

const HEX64 = /^[0-9a-f]{64}$/;
const MAX_BYTES = 4096;
const MIN_WRITE_MS = 3000;

export async function handleSettings(request, env) {
  const url = new URL(request.url);
  let id, text = null;

  if (request.method === "GET") {
    id = url.searchParams.get("id") || "";
  } else if (request.method === "PUT") {
    text = await request.text();
    if (text.length > MAX_BYTES + 200) return json({ error: "too large" }, 413);
    let body;
    try { body = JSON.parse(text); } catch { return json({ error: "bad json" }, 400); }
    if (!body || typeof body.settings !== "object" || body.settings === null || Array.isArray(body.settings))
      return json({ error: "settings must be an object" }, 400);
    id = body.id || "";
    text = JSON.stringify(body.settings);
    if (text.length > MAX_BYTES) return json({ error: "settings too large" }, 413);
  } else {
    return json({ error: "use GET or PUT" }, 405);
  }
  if (!HEX64.test(id)) return json({ error: "bad id" }, 400);

  const stub = env.SETTINGS.get(env.SETTINGS.idFromName(id));
  return stub.fetch("https://settings/", { method: request.method, body: text });
}

export class SettingsStore {
  constructor(state) {
    this.storage = state.storage;
  }

  async fetch(request) {
    const saved = await this.storage.get("s"); // { settings: "<json>", updated: ms }
    if (request.method === "GET") {
      if (!saved) return json({ error: "nothing saved" }, 404);
      return json({ settings: JSON.parse(saved.settings), updated: saved.updated });
    }
    const now = Date.now();
    if (saved && now - saved.updated < MIN_WRITE_MS) return json({ error: "too fast" }, 429);
    const settings = await request.text();
    await this.storage.put("s", { settings, updated: now });
    return json({ ok: true, updated: now });
  }
}

function json(data, status = 200) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" },
  });
}
