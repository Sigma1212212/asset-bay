// Presence: lets Asset Bay menus in the same Gorilla Tag room find each other.
//
// Privacy: the server only ever sees SHA-256 hashes. `room` = hash("assetbay:" + roomName) and
// `player` = hash("assetbay:" + roomName + ":" + userId), computed on the players' PCs. Two menus in the
// same room compute matching hashes; the server can't tell who anyone is or which room they're in.
//
// One Durable Object per room hash keeps members in memory only (nothing is stored), forgets anyone
// silent for 15 s, and caps room size, state size and post rate.

const HEX64 = /^[0-9a-f]{64}$/;
const TTL_MS = 15000;
const MAX_MEMBERS = 20;
const MAX_STATE_BYTES = 512;
const MIN_INTERVAL_MS = 1000;

export async function handlePresence(request, env) {
  if (request.method !== "POST") return json({ error: "use POST" }, 405);
  const text = await request.text();
  if (text.length > 2048) return json({ error: "too large" }, 413);

  let body;
  try { body = JSON.parse(text); } catch { return json({ error: "bad json" }, 400); }
  if (!body || !HEX64.test(body.room || "") || !HEX64.test(body.player || "")) return json({ error: "bad ids" }, 400);

  const stub = env.PRESENCE.get(env.PRESENCE.idFromName(body.room));
  return stub.fetch("https://presence/update", { method: "POST", body: text });
}

export class PresenceRoom {
  constructor(state, env) {
    this.members = new Map(); // player hash -> { state, seen }
  }

  async fetch(request) {
    const body = JSON.parse(await request.text());
    const now = Date.now();

    for (const [id, m] of this.members) if (now - m.seen > TTL_MS) this.members.delete(id);

    const me = this.members.get(body.player);
    if (body.state === null) {
      this.members.delete(body.player); // leaving
    } else if (!me || now - me.seen >= MIN_INTERVAL_MS) {
      const state = JSON.stringify(body.state ?? {});
      if (state.length > MAX_STATE_BYTES) return json({ error: "state too large" }, 413);
      if (!me && this.members.size >= MAX_MEMBERS) return json({ error: "room full" }, 429);
      this.members.set(body.player, { state: JSON.parse(state), seen: now });
    }

    const others = [];
    for (const [id, m] of this.members) if (id !== body.player) others.push({ player: id, state: m.state });
    return json({ members: others, ttl: TTL_MS });
  }
}

function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { "Content-Type": "application/json; charset=utf-8" } });
}
