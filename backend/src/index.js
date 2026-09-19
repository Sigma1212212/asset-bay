// Asset Bay backend - a Cloudflare Worker on randomthingsthatarecool.dev/asset-bay/*
//
//   GET /asset-bay/health          -> {"ok":true}
//   GET /asset-bay/feed            -> {"feed": "<exact feed.json text>", "sig": "<base64 RSA signature>"}
//   GET /asset-bay/media/<key>     -> a file from the R2 bucket (videos, bundles), with Range support
//   POST /asset-bay/presence       -> menus in the same room find each other (hashed ids only, see presence.js)
//   POST /asset-bay/control        -> press a button on another member's menu, only if they allowed it
//   GET/PUT /asset-bay/settings    -> settings sync by anonymous sync-code hash (see settings.js)
//   GET /asset-bay/budget          -> today's / this month's usage against the free-tier cut-offs (budget.js)
//
// Spending guard: every feature switches itself off before Cloudflare could bill for it, and comes back
// when the day / month rolls over. Paused features answer 503 {"paused": ...}; the menu shows "offline".
//
// Read-only for content: there are no upload or admin routes. The only write is presence, which keeps
// hashed, size-capped state in memory for 15 seconds.
// Content is published from your PC with publish-feed.ps1 (wrangler writes to R2 directly), and the
// menu only trusts a feed whose signature matches the public key built into it.

import { handleControl, handlePresence, PresenceRoom } from "./presence.js";
import { Budget, count, flush, isPaused, report } from "./budget.js";
import { handleSettings, SettingsStore } from "./settings.js";
export { PresenceRoom, Budget, SettingsStore };

const PREFIX = "/asset-bay";
const SAFE_KEY = /^[A-Za-z0-9][A-Za-z0-9._\-\/]{0,200}$/;

export default {
  async fetch(request, env, ctx) {
    count("requests");
    try {
      return await route(request, env);
    } finally {
      // Report after the request is counted in full, without delaying the response.
      ctx?.waitUntil?.(flush(env));
    }
  },
};

async function route(request, env) {
  const url = new URL(request.url);
  if (!url.pathname.startsWith(PREFIX)) return notFound();
  const path = url.pathname.slice(PREFIX.length) || "/";

  // The usage report stays reachable while paused, so you can see why (80k cap leaves 20k of headroom).
  if (path === "/budget" && request.method === "GET") return json(await report(env), 200, { "Cache-Control": "no-store" });
  if (isPaused("all")) return pausedResponse("daily request allowance");

  if (path === "/settings") {
    // Same Durable Object allowance as presence.
    if (isPaused("presence")) return pausedResponse("daily Durable Object allowance");
    count("doCalls");
    return await handleSettings(request, env);
  }

  if (path === "/control") {
    if (isPaused("presence")) return pausedResponse("daily Durable Object allowance");
    count("doCalls");
    return await handleControl(request, env);
  }

  if (path === "/presence") {
    if (isPaused("presence")) return pausedResponse("daily Durable Object allowance");
    count("doCalls");
    return await handlePresence(request, env);
  }

  if (request.method !== "GET" && request.method !== "HEAD") {
    return new Response("Method not allowed", { status: 405, headers: { Allow: "GET, HEAD" } });
  }

  try {
    if (path === "/health") return json({ ok: true });
    if (path === "/feed" || path.startsWith("/media/")) {
      if (isPaused("r2")) return pausedResponse("monthly R2 read allowance");
      if (path === "/feed") { count("r2Reads", 2); return await feed(env); }
      count("r2Reads");
      return await media(request, env, decodeURIComponent(path.slice("/media/".length)));
    }
    return notFound();
  } catch (err) {
    return json({ error: "internal error" }, 500);
  }
}

async function feed(env) {
  const [body, sig] = await Promise.all([env.MEDIA.get("feed.json"), env.MEDIA.get("feed.json.sig")]);
  if (!body || !sig) return json({ error: "no feed published yet" }, 404);

  // The feed is returned as the exact text that was signed, so the menu can verify it byte for byte.
  return json(
    { feed: await body.text(), sig: (await sig.text()).trim() },
    200,
    { "Cache-Control": "public, max-age=60" },
  );
}

async function media(request, env, key) {
  if (!SAFE_KEY.test(key) || key.includes("..") || key.startsWith("feed.json")) return notFound();

  const object = await env.MEDIA.get(key, { range: request.headers, onlyIf: request.headers });
  if (object === null) return notFound();

  const headers = new Headers();
  object.writeHttpMetadata(headers);
  headers.set("ETag", object.httpEtag);
  headers.set("Accept-Ranges", "bytes");
  headers.set("Cache-Control", "public, max-age=86400");

  // onlyIf matched a precondition (If-None-Match etc.): no body.
  if (!("body" in object) || !object.body) return new Response(null, { status: 304, headers });

  let status = 200;
  if (object.range && request.headers.has("Range")) {
    const offset = object.range.offset ?? 0;
    const length = object.range.length ?? object.size - offset;
    headers.set("Content-Range", `bytes ${offset}-${offset + length - 1}/${object.size}`);
    headers.set("Content-Length", String(length));
    status = 206; // video players seek with Range requests
  } else {
    headers.set("Content-Length", String(object.size));
  }

  return new Response(request.method === "HEAD" ? null : object.body, { status, headers });
}

function json(data, status = 200, extra = {}) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "Content-Type": "application/json; charset=utf-8", ...extra },
  });
}

function pausedResponse(what) {
  // Retry-After: seconds until the next UTC midnight (daily limits) - monthly ones just keep saying so.
  const now = new Date();
  const midnight = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate() + 1);
  return json({ error: "paused to stay in the free tier", paused: what }, 503,
    { "Retry-After": String(Math.ceil((midnight - now.getTime()) / 1000)) });
}

function notFound() {
  return json({ error: "not found" }, 404);
}
