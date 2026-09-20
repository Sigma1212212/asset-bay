// Presence: lets Asset Bay menus in the same Gorilla Tag room find each other.
//
// Privacy: the server only ever sees SHA-256 hashes. `room` = hash("assetbay:" + roomName) and
// `player` = hash("assetbay:" + roomName + ":" + userId), computed on the players' PCs. Two menus in the
// same room compute matching hashes; the server can't tell who anyone is or which room they're in.
//
// Two ways in, same room:
//   - live:  GET /live?room=&player= upgrades to a WebSocket. Updates and button presses are pushed the
//            moment they happen. The socket uses Cloudflare's hibernation API, so an idle room costs
//            nothing, and pings are answered without waking it.
//   - poll:  POST /presence every few seconds (older menus, or when a socket can't be opened).
// Nothing is stored: members live in memory / on their sockets, and anyone silent for 15 s is dropped.
//
// Remote control ("let others use my menu"): a press for another member is only accepted when both
// are in the room AND the target's own shared state says control is on ("browse" or "full"). Live
// targets get it instantly; polling targets get it with their next update (expires after 10 s).
// The target's menu re-checks the permission level before acting on anything.

import { count, flush } from "./budget.js";

const HEX64 = /^[0-9a-f]{64}$/;
const TTL_MS = 15000;
const MAX_MEMBERS = 20;
const MAX_STATE_BYTES = 1600; // room for a mirror of the current page (8 short rows)
const MIN_INTERVAL_MS = 1000; // polling
const MIN_LIVE_STATE_MS = 200; // live: state updates
const CONTROL_LEVELS = new Set(["browse", "mods", "full"]);
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

const room = (env, hash) => env.PRESENCE.get(env.PRESENCE.idFromName(hash));

export async function handlePresence(request, env) {
  const [body, error, text] = await readBody(request, 3072);
  if (error) return error;
  if (!body || !HEX64.test(body.room || "") || !HEX64.test(body.player || "")) return json({ error: "bad ids" }, 400);
  return room(env, body.room).fetch("https://presence/update", { method: "POST", body: text });
}

export async function handleControl(request, env) {
  const [body, error, text] = await readBody(request, 1024);
  if (error) return error;
  if (!body || !HEX64.test(body.room || "") || !HEX64.test(body.from || "") || !HEX64.test(body.target || ""))
    return json({ error: "bad ids" }, 400);
  if (body.from === body.target) return json({ error: "can't control yourself" }, 400);
  if (!validCommand(body.cmd)) return json({ error: "bad command" }, 400);
  return room(env, body.room).fetch("https://presence/control", { method: "POST", body: text });
}

export async function handleLive(request, env) {
  if (request.headers.get("Upgrade") !== "websocket") return json({ error: "expected a WebSocket" }, 426);
  const url = new URL(request.url);
  const roomHash = url.searchParams.get("room") || "", player = url.searchParams.get("player") || "";
  if (!HEX64.test(roomHash) || !HEX64.test(player)) return json({ error: "bad ids" }, 400);
  return room(env, roomHash).fetch(`https://presence/live?player=${player}`, request);
}

const validCommand = (cmd) => cmd && typeof cmd === "object" && !Array.isArray(cmd) && JSON.stringify(cmd).length <= MAX_COMMAND_BYTES;

export class PresenceRoom {
  constructor(ctx, env) {
    this.ctx = ctx;
    this.env = env;
    this.polled = new Map(); // polling members: hash -> { state, seen, queue, lastSent }
    // Keep-alive pings are answered by Cloudflare without waking (or billing) this object.
    ctx.setWebSocketAutoResponse(new WebSocketRequestResponsePair("ping", "pong"));
  }

  // ---------------------------------------------------------------- live members (on their sockets)

  /** Live members, rebuilt from the sockets so it survives hibernation. */
  live() {
    const map = new Map();
    for (const ws of this.ctx.getWebSockets()) {
      const a = ws.deserializeAttachment();
      if (a?.player) map.set(a.player, { ws, ...a });
    }
    return map;
  }

  members(now) {
    for (const [id, m] of this.polled) if (now - m.seen > TTL_MS) this.polled.delete(id);
    const all = new Map();
    for (const [id, m] of this.live()) all.set(id, { state: m.state ?? {}, ws: m.ws }); // connected = present
    for (const [id, m] of this.polled) all.set(id, { state: m.state, polled: m });
    return all;
  }

  broadcast(message, except) {
    const text = JSON.stringify(message);
    for (const ws of this.ctx.getWebSockets()) {
      if (ws === except) continue;
      try { ws.send(text); } catch { /* closing */ }
    }
  }

  async fetch(request) {
    const url = new URL(request.url);
    const now = Date.now();
    if (url.pathname === "/live") return this.accept(url.searchParams.get("player"), now);
    const body = JSON.parse(await request.text());
    return url.pathname === "/control" ? this.control(body.from, body.target, body.cmd, now) : this.update(body, now);
  }

  accept(player, now) {
    const all = this.members(now);
    if (!all.has(player) && all.size >= MAX_MEMBERS) return json({ error: "room full" }, 429);
    // One socket per player: a reconnect replaces the old one.
    for (const ws of this.ctx.getWebSockets()) if (ws.deserializeAttachment()?.player === player) ws.close(1000, "replaced");

    const [client, server] = Object.values(new WebSocketPair());
    this.ctx.acceptWebSocket(server);
    server.serializeAttachment({ player, state: null, lastState: 0, lastSent: 0 });
    const others = [];
    for (const [id, m] of all) if (id !== player) others.push({ player: id, state: m.state });
    server.send(JSON.stringify({ t: "members", members: others, ttl: TTL_MS }));
    return new Response(null, { status: 101, webSocket: client });
  }

  async webSocketMessage(ws, message) {
    count("doCalls"); // every incoming message counts toward the spending guard
    this.ctx.waitUntil(flush(this.env));
    const now = Date.now();
    const me = ws.deserializeAttachment();
    if (!me?.player || typeof message !== "string" || message.length > 3072) return;
    let msg;
    try { msg = JSON.parse(message); } catch { return; }

    if (msg.t === "state") {
      if (now - me.lastState < MIN_LIVE_STATE_MS) return;
      const state = JSON.stringify(msg.state ?? {});
      if (state.length > MAX_STATE_BYTES) { ws.send(JSON.stringify({ t: "error", error: "state too large" })); return; }
      me.state = JSON.parse(state);
      me.lastState = now;
      ws.serializeAttachment(me);
      this.broadcast({ t: "member", player: me.player, state: me.state }, ws);
    } else if (msg.t === "control") {
      const reply = this.control(me.player, msg.target, msg.cmd, now, ws);
      if (reply.error) ws.send(JSON.stringify({ t: "error", error: reply.error }));
    }
  }

  webSocketClose(ws) {
    const me = ws.deserializeAttachment();
    if (!me?.player) return;
    // A reconnect closes the player's old socket; they haven't left if a newer one is still open.
    for (const other of this.ctx.getWebSockets())
      if (other !== ws && other.deserializeAttachment()?.player === me.player) return;
    this.broadcast({ t: "leave", player: me.player }, ws);
  }

  webSocketError(ws) { this.webSocketClose(ws); }

  // ---------------------------------------------------------------- polling members

  update(body, now) {
    const me = this.polled.get(body.player);
    if (body.state === null) {
      this.polled.delete(body.player);
      this.broadcast({ t: "leave", player: body.player });
    } else if (!me || now - me.seen >= MIN_INTERVAL_MS) {
      const state = JSON.stringify(body.state ?? {});
      if (state.length > MAX_STATE_BYTES) return json({ error: "state too large" }, 413);
      if (!me && this.members(now).size >= MAX_MEMBERS) return json({ error: "room full" }, 429);
      const next = { state: JSON.parse(state), seen: now, queue: me?.queue ?? [], lastSent: me?.lastSent ?? 0 };
      this.polled.set(body.player, next);
      this.broadcast({ t: "member", player: body.player, state: next.state }); // live members see it at once
    }

    const self = this.polled.get(body.player);
    const commands = self ? self.queue.filter((c) => now - c.at <= COMMAND_TTL_MS) : [];
    if (self) self.queue = [];

    const others = [];
    for (const [id, m] of this.members(now)) if (id !== body.player) others.push({ player: id, state: m.state });
    return json({ members: others, commands, ttl: TTL_MS });
  }

  // ---------------------------------------------------------------- remote control

  /** Returns a Response for HTTP callers; for live callers ({@link webSocketMessage}) returns {error?}. */
  control(from, to, cmd, now, fromSocket) {
    const fail = (error, status) => (fromSocket ? { error } : json({ error }, status));
    if (!HEX64.test(from || "") || !HEX64.test(to || "") || from === to) return fail("bad ids", 400);
    if (!validCommand(cmd)) return fail("bad command", 400);

    const all = this.members(now);
    const sender = all.get(from), target = all.get(to);
    if (!sender || !target) return fail("not in this room", 404);
    if (!CONTROL_LEVELS.has(target.state?.control)) return fail("that menu isn't shared for control", 403);

    // Rate limit per sender (kept on their socket or polling record).
    const senderRecord = sender.polled ?? (fromSocket ? fromSocket.deserializeAttachment() : null);
    if (senderRecord) {
      if (now - (senderRecord.lastSent || 0) < MIN_COMMAND_GAP_MS) return fail("too fast", 429);
      senderRecord.lastSent = now;
      if (fromSocket) fromSocket.serializeAttachment(senderRecord);
    }

    if (target.ws) {
      target.ws.send(JSON.stringify({ t: "cmd", from, cmd })); // instant
      return fromSocket ? {} : json({ ok: true, live: true });
    }
    const q = target.polled.queue.filter((c) => now - c.at <= COMMAND_TTL_MS);
    if (q.length >= MAX_QUEUE) q.shift();
    q.push({ from, cmd, at: now });
    target.polled.queue = q;
    return fromSocket ? {} : json({ ok: true, queued: q.length });
  }
}

function json(data, status = 200) {
  return new Response(JSON.stringify(data), { status, headers: { "Content-Type": "application/json; charset=utf-8" } });
}
