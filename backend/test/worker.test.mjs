// End-to-end test of the Worker in Miniflare (the same runtime `wrangler dev` uses), no server needed.
//   node test/worker.test.mjs
// Uses the local R2 state seeded with `wrangler r2 object put ... --local` (see README).
import { unstable_startWorker } from "wrangler";
import { readFileSync } from "node:fs";
import { createPublicKey, createVerify } from "node:crypto";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
// Same config and local R2 state that `wrangler dev --local` uses; port 0 = any free port, torn down at the end.
const worker = await unstable_startWorker({
  config: join(root, "wrangler.toml"),
  dev: { server: { port: 0 }, persist: join(root, ".wrangler/state"), inspector: false, remote: false },
});

let failures = 0;
const check = (name, ok, detail = "") => {
  console.log(`${ok ? "PASS" : "FAIL"}  ${name}${detail ? "  " + detail : ""}`);
  if (!ok) failures++;
};
const get = (path, init) => worker.fetch("http://localhost" + path, init);

// The public key the menu embeds (RSA XML) -> a Node key object.
const xml = readFileSync(join(root, "../signing/feed-public-key.xml"), "utf8");
const b64url = (tag) => xml.match(new RegExp(`<${tag}>([^<]+)</${tag}>`))[1].replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
const publicKey = createPublicKey({ key: { kty: "RSA", n: b64url("Modulus"), e: b64url("Exponent") }, format: "jwk" });

try {
  let r = await get("/asset-bay/health");
  check("health", r.status === 200 && (await r.json()).ok === true);

  r = await get("/asset-bay/feed");
  const env = await r.json();
  check("feed served", r.status === 200 && typeof env.feed === "string" && typeof env.sig === "string");
  const verifier = createVerify("RSA-SHA256");
  verifier.update(Buffer.from(env.feed, "utf8"));
  check("feed signature verifies with the menu's public key", verifier.verify(publicKey, Buffer.from(env.sig, "base64")));
  const tampered = createVerify("RSA-SHA256");
  tampered.update(Buffer.from(env.feed.replace("Welcome", "Hacked"), "utf8"));
  check("tampered feed fails verification", !tampered.verify(publicKey, Buffer.from(env.sig, "base64")));
  check("feed parses", JSON.parse(env.feed).screens.length === 1);

  r = await get("/asset-bay/media/videos/test.bin");
  const full = new Uint8Array(await r.arrayBuffer());
  check("media full download", r.status === 200 && full.length === 1024, `status ${r.status}, ${full.length} bytes`);

  r = await get("/asset-bay/media/videos/test.bin", { headers: { Range: "bytes=100-199" } });
  const part = new Uint8Array(await r.arrayBuffer());
  check("media range request (video seeking)", r.status === 206 && part.length === 100 && part[0] === 100,
    `status ${r.status}, ${r.headers.get("Content-Range")}`);

  r = await get("/asset-bay/media/../feed.json");
  check("path traversal blocked", r.status === 404);
  r = await get("/asset-bay/media/feed.json");
  check("feed not reachable as media", r.status === 404);
  r = await get("/asset-bay/feed", { method: "POST", body: "x" });
  check("writes rejected", r.status === 405);
  r = await get("/asset-bay/admin");
  check("unknown routes 404", r.status === 404);

  // ---- presence
  const h = (c) => c.repeat(64);
  const post = (body) => get("/asset-bay/presence", { method: "POST", body: JSON.stringify(body) });
  r = await post({ room: h("a"), player: h("1"), state: { open: true, theme: "Neon" } });
  check("presence: first player joins", r.status === 200 && (await r.json()).members.length === 0);
  r = await post({ room: h("a"), player: h("2"), state: { open: false, theme: "Halo" } });
  let j = await r.json();
  check("presence: second player sees the first", j.members.length === 1 && j.members[0].state.theme === "Neon");
  r = await post({ room: h("b"), player: h("3"), state: {} });
  check("presence: other rooms are separate", (await r.json()).members.length === 0);
  r = await post({ room: h("a"), player: h("1"), state: null });
  r = await post({ room: h("a"), player: h("2"), state: { open: true } });
  // player 2 posted <1 s ago, so its state update is ignored but it still gets the member list
  check("presence: leaving removes you", (await r.json()).members.length === 0);
  r = await post({ room: "not-a-hash", player: h("1"), state: {} });
  check("presence: bad ids rejected", r.status === 400);
  r = await post({ room: h("c"), player: h("4"), state: { junk: "x".repeat(600) } });
  check("presence: oversized state rejected", r.status === 413);
  r = await get("/asset-bay/presence");
  check("presence: GET rejected", r.status === 405);
} catch (e) {
  check("no exceptions", false, e.stack);
} finally {
  await worker.dispose();
}

console.log(`FAILURES: ${failures}`);
process.exit(failures ? 1 : 0);
