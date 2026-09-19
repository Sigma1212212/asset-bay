// Edge cache test.   node test/cache.test.mjs
import { unstable_startWorker } from "wrangler";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { cpSync, rmSync } from "node:fs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const state = join(root, ".wrangler/cache-test-state");
rmSync(state, { recursive: true, force: true });
cpSync(join(root, ".wrangler/state/v3/r2"), join(state, "v3/r2"), { recursive: true });
const worker = await unstable_startWorker({
  config: join(root, "wrangler.toml"),
  dev: { server: { port: 0 }, persist: state, inspector: false, remote: false },
});

let failures = 0;
const check = (name, ok, detail = "") => {
  console.log(`${ok ? "PASS" : "FAIL"}  ${name}${detail ? "  " + detail : ""}`);
  if (!ok) failures++;
};
const get = (path, init) => worker.fetch("http://localhost/asset-bay" + path, init);
const wait = (ms) => new Promise((r) => setTimeout(r, ms));
const usage = async () => (await (await get("/budget")).json()).usage.r2Reads;

try {
  let r = await get("/feed"); await r.text();
  check("feed first request is a miss", r.status === 200 && r.headers.get("X-Cache") === "MISS");
  await wait(200);
  r = await get("/feed"); const body = await r.json();
  check("feed second request is a hit", r.headers.get("X-Cache") === "HIT" && typeof body.sig === "string");

  // Range first: served from R2, full file cached in the background.
  r = await get("/media/videos/test.bin", { headers: { Range: "bytes=0-99" } });
  const first = new Uint8Array(await r.arrayBuffer());
  check("first seek served from storage", r.status === 206 && r.headers.get("X-Cache") === "MISS" && first.length === 100);
  await wait(400);
  const before = await usage();
  r = await get("/media/videos/test.bin", { headers: { Range: "bytes=500-599" } });
  const part = new Uint8Array(await r.arrayBuffer());
  check("later seeks come from the cache", r.status === 206 && r.headers.get("X-Cache") === "HIT" && part.length === 100 && part[0] === (500 % 256),
    `status ${r.status} ${r.headers.get("X-Cache")} ${r.headers.get("Content-Range")}`);
  r = await get("/media/videos/test.bin"); const all = new Uint8Array(await r.arrayBuffer());
  check("whole file from the cache", r.status === 200 && r.headers.get("X-Cache") === "HIT" && all.length === 1024);
  await wait(200);
  check("cache hits don't use R2 reads", (await usage()) === before, `before ${before} after ${await usage()}`);

  r = await get("/media/videos/missing.bin");
  check("missing files still 404", r.status === 404);
  r = await get("/media/feed.json");
  check("feed still not reachable as media", r.status === 404);
  r = await get("/media/videos/test.bin", { method: "HEAD" });
  check("HEAD still works", r.status === 200);
} finally {
  await worker.dispose();
}
console.log(failures ? `FAILURES: ${failures}` : "FAILURES: 0");
process.exit(failures ? 1 : 0);
