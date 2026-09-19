// Presence: lets Asset Bay menus in the same Gorilla Tag room find each other.
//
// Privacy: the server only ever sees SHA-256 hashes. `room` = hash("assetbay:" + roomName) and
// `player` = hash("assetbay:" + roomName + ":" + userId), computed on the players' PCs. Two menus in the
// same room compute matching hashes; the server can't tell who anyone is or which room they're in.
//
// One Durable Object per room hash keeps members in memory only (nothing is stored), forgets anyone
// silent for 15 s, and caps room size, state size and post rate.
//
// Remote control ("let others use my menu"): a member can queue a button press for another member with
// POST /control. It's only accepted when both are live members of the room AND the target's own shared
// state says control is on ("browse" or "full") - the owner opts in, and the menu turns it off again
// every time the game restarts. Queued presses ride back to the target on its next presence update and
// expire after 10 s. The target's menu re-checks the permission level before acting on anything.

const HEX64 = /^[0-9a-f]{64}$/;
const TTL_MS = 15000;
const MAX_MEMBERS = 20;
const MAX_STATE_BYTES = 1600; // room for a mirror of the current page (8 short rows)
const MIN_INTERVAL_MS = 1000;
const CONTROL_LEVELS = new Set(["browse", "full"]);
const COMMAND_TTL_MS = 10000;
const MAX_QUEUE = 8;
const MIN_COMMAND_GAP_MS = 150;
const MAX_COMMAND_BYTES = 256;

async function readBody(request, limit) {
  if (request.method !== "POST") return [null, json({ error: "use POST" }, 405)];
  const text = await request.text();
  if (text.length > limit) return [null, json({ error: "too large" }, 413)];
  try { return [JSON.parse(text), null, text]; } catch { return [null, json({ error: "bad json" }, 400)]; }
}

export async function handlePresence(request, env) {
  const [body, error, text] = await readBody(request, 3072);
  if (error) return error;
  if (!body || !HEX64.test(body.room || "") || !HEX64.test(body.player || "")) return json({ error: "bad ids" }, 400);
  const stub = env.PRESENCE.get(env.PRESENCE.idFromName(body.room));
  return stub.fetch("https://presence/update", { method: "POST", body: text });
}

export async function handleControl(request, env) {
  const [body, error, text] = await readBody(request, 1024);
  if (error) return error;
  if (!body || !HEX64.test(body.room || "") || !HEX64.test(body.from || "") || !HEX64.test(body.target || ""))
    return json({ error: "bad ids" }, 400);
  if (body.from === body.target) return json({ error: "can't control yourself" }, 400);
  if (!body.cmd || typeof body.cmd !== "object" || JSON.stringify(body.cmd).length > MAX_COMMAND_BYTES)
    return json({ error: "bad command" }, 400);
  const stub = env.PRESENCE.get(env.PRESENCE.idFromName(body.room));
  return stub.fetch("https://presence/control", { method: "POST", body: text });
}

export class PresenceRoom {
  constructor(state, env) {
    this.members = new Map(); // player hash -> { state, seen, queue: [{from, cmd, at}], lastSent }
  }

  async fetch(request) {
    const body = JSON.parse(await request.text());
    const now = Date.now();
    for (const [id, m] of this.members) if (now - m.seen > TTL_MS) this.members.delete(id);
    return new URL(request.url).pathname === "/control" ? this.control(body, now) : this.update(body, now);
  }

  update(body, now) {
    const me = this.members.get(body.player);
    if (body.state === null) {
      this.members.delete(body.player); // leaving
    } else if (!me || now - me.seen >= MIN_INTERVAL_MS) {
      const state = JSON.stringify(body.state ?? {});
      if (state.length > MAX_STATE_BYTES) return json({ error: "state too large" }, 413);
      if (!me && this.members.size >= MAX_MEMBERS) return json({ error: "room full" }, 429);
      this.members.set(body.player, { state: JSON.parse(state), seen: now, queue: me?.queue ?? [], lastSent: me?.lastSent ?? 0 });
    }

    // Hand over any presses queued for me (fresh ones only), then forget them.
    const self = this.members.get(body.player);
    const commands = self ? self.queue.filter((c) => now - c.at <= COMMAND_TTL_MS) : [];
    if (self) self.queue = [];

    const others = [];
    for (const [id, m] of this.members) if (id !== body.player) others.push({ player: id, state: m.state });
    return json({ members: others, commands, ttl: TTL_MS });
  }

  control(body, now) {
    const sender = this.members.get(body.from);
    const target = this.members.get(body.target);
    if (!sender || !target) return json({ error: "not in this room" }, 404);
    if (!CONTROL_LEVELS.has(target.state?.control)) return json({ error: "that menu isn't shared for control" }, 403);
    if (now - sender.lastSent < MIN_COMMAND_GAP_MS) return json({ error: "too fast" }, 429);
    sender.lastSent = now;

    target.queue = target.queue.filter((c) => now - c.at <= COMMAND_TTL_MS);
    if (target.queue.length >= MAX_QUEUE) target.queue.shift();
    target.queue.push({ from: body.from, cmd: body.cmd, at: now });
    return json({ ok: true, queued: target.queue.length });
  }
}

function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { "Content-Type": "application/json; charset=utf-8" } });
}
