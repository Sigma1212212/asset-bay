// Asset Bay backend - a Cloudflare Worker on randomthingsthatarecool.dev/asset-bay/*
//
//   GET /asset-bay/health          -> {"ok":true}
//   GET /asset-bay/feed            -> {"feed": "<exact feed.json text>", "sig": "<base64 RSA signature>"}
//   GET /asset-bay/media/<key>     -> a file from the R2 bucket (videos, bundles), with Range support
//
// Read-only by design: there are no upload or admin routes, so there is nothing here to break into.
// Content is published from your PC with publish-feed.ps1 (wrangler writes to R2 directly), and the
// menu only trusts a feed whose signature matches the public key built into it.

const PREFIX = "/asset-bay";
const SAFE_KEY = /^[A-Za-z0-9][A-Za-z0-9._\-\/]{0,200}$/;

export default {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (!url.pathname.startsWith(PREFIX)) return notFound();
    const path = url.pathname.slice(PREFIX.length) || "/";

    if (request.method !== "GET" && request.method !== "HEAD") {
      return new Response("Method not allowed", { status: 405, headers: { Allow: "GET, HEAD" } });
    }

    try {
      if (path === "/health") return json({ ok: true });
      if (path === "/feed") return await feed(env);
      if (path.startsWith("/media/")) return await media(request, env, decodeURIComponent(path.slice("/media/".length)));
      return notFound();
    } catch (err) {
      return json({ error: "internal error" }, 500);
    }
  },
};

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

function notFound() {
  return json({ error: "not found" }, 404);
}
