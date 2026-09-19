// Edge caching for the read-only content (feed + media), using Cloudflare's cache in the data centre that
// served the request. A hit never touches R2: it's faster (no storage round trip) and doesn't count
// toward the R2 read allowance. Every response says X-Cache: HIT or MISS so it's easy to check.
//
//   feed   cached 30 s  - a newly published feed shows up within half a minute
//   media  cached 1 day - videos / bundles; video players seek with Range requests, which the cache
//                         answers from the stored full file. When the first request for a file is a
//                         Range request, the full file is fetched once in the background to fill the
//                         cache, so every later seek is a hit.

const FEED_TTL = 30;
const MEDIA_TTL = 86400;
const MAX_WARM_BYTES = 256 * 1024 * 1024; // don't pull huge files into the cache in the background
const warming = new Set();                 // per instance: one background fill per file at a time

const cacheKey = (url, path) => new Request(new URL(path, url).toString(), { method: "GET" });

function tagged(response, state) {
  const r = new Response(response.body, response);
  r.headers.set("X-Cache", state);
  return r;
}

/** GET /feed. `load` reads R2 and returns a Response (only called on a miss). */
export async function cachedFeed(request, ctx, prefix, load) {
  const cache = caches.default;
  const key = cacheKey(request.url, prefix + "/feed");
  const hit = await cache.match(key);
  if (hit) return tagged(hit, "HIT");

  const fresh = await load();
  if (fresh.status === 200) {
    const stored = new Response(fresh.clone().body, fresh);
    stored.headers.set("Cache-Control", `public, max-age=${FEED_TTL}`);
    ctx.waitUntil(cache.put(key, stored));
  }
  return tagged(fresh, "MISS");
}

/**
 * GET/HEAD /media/<key>. `load(request)` serves straight from R2 (Range-aware); `loadFull()` returns the
 * whole object as a 200 Response for filling the cache, or null.
 */
export async function cachedMedia(request, ctx, prefix, objectKey, load, loadFull, onR2Read) {
  if (request.method !== "GET") { onR2Read(); return load(request); } // HEAD etc: straight through

  const cache = caches.default;
  const key = cacheKey(request.url, `${prefix}/media/${encodeURIComponent(objectKey)}`);
  // Match with the client's Range / If-None-Match so the cache can answer 206 / 304 itself.
  const probe = new Request(key.url, { method: "GET", headers: request.headers });
  const hit = await cache.match(probe);
  if (hit) return tagged(hit, "HIT");

  onR2Read();
  const fresh = await load(request);
  if (fresh.status === 200) {
    // Whole file requested: store it as it streams to the client.
    const stored = new Response(fresh.clone().body, fresh);
    stored.headers.set("Cache-Control", `public, max-age=${MEDIA_TTL}`);
    ctx.waitUntil(cache.put(key, stored));
  } else if (fresh.status === 206 && !warming.has(key.url)) {
    // First touch was a seek: fill the cache with the full file in the background (one extra R2 read).
    warming.add(key.url);
    ctx.waitUntil((async () => {
      try {
        onR2Read();
        const full = await loadFull();
        if (full && full.status === 200 && Number(full.headers.get("Content-Length") || 0) <= MAX_WARM_BYTES) {
          full.headers.set("Cache-Control", `public, max-age=${MEDIA_TTL}`);
          await cache.put(key, full);
        }
      } catch { /* cache fill is best effort */ } finally { warming.delete(key.url); }
    })());
  }
  return tagged(fresh, "MISS");
}
