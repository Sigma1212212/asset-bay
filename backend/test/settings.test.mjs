// Settings sync test.   node test/settings.test.mjs
import { unstable_startWorker } from "wrangler";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { rmSync } from "node:fs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const state = join(root, ".wrangler/settings-test-state");
rmSync(state, { recursive: true, force: true });
const worker = await unstable_startWorker({
  config: join(root, "wrangler.toml"),
  dev: { server: { port: 0 }, persist: state, inspector: false, remote: false },
});

let failures = 0;
const check = (name, ok, detail = "") => {
  console.log(`${ok ? "PASS" : "FAIL"}  ${name}${detail ? "  " + detail : ""}`);
  if (!ok) failures++;
};
const call = (path, init) => worker.fetch("http://localhost/asset-bay" + path, init);
const put = (body) => call("/settings", { method: "PUT", body: JSON.stringify(body) });
const idA = "a".repeat(64), idB = "b".repeat(64);

try {
  let r = await call("/settings?id=" + idA);
  check("nothing saved yet -> 404", r.status === 404);

  r = await put({ id: idA, settings: { theme: 9, style: 7, speed: 1.5 } });
  check("save", r.status === 200 && (await r.json()).ok === true);

  r = await call("/settings?id=" + idA);
  const got = await r.json();
  check("load returns what was saved", r.status === 200 && got.settings.theme === 9 && got.settings.speed === 1.5 && got.updated > 0);

  r = await call("/settings?id=" + idB);
  check("other codes can't see it", r.status === 404);

  r = await put({ id: idA, settings: { theme: 1 } });
  check("rapid rewrite throttled", r.status === 429);

  r = await put({ id: "nothex", settings: {} });
  check("bad id rejected", r.status === 400);
  r = await put({ id: idB, settings: [1, 2] });
  check("non-object rejected", r.status === 400);
  r = await put({ id: idB, settings: { big: "x".repeat(5000) } });
  check("oversized rejected", r.status === 413);
  r = await call("/settings", { method: "POST", body: "{}" });
  check("other methods rejected", r.status === 405);
} finally {
  await worker.dispose();
}
console.log(failures ? `FAILURES: ${failures}` : "FAILURES: 0");
process.exit(failures ? 1 : 0);
