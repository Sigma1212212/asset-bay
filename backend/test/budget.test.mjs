// Spending guard test: the same Worker with tiny limits, to prove each feature switches off.
//   node test/budget.test.mjs
import { unstable_startWorker } from "wrangler";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { cpSync, rmSync } from "node:fs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const state = join(root, ".wrangler/budget-test-state");
// Fresh counters every run: its own state folder, with only the seeded R2 files copied in.
rmSync(state, { recursive: true, force: true });
cpSync(join(root, ".wrangler/state/v3/r2"), join(state, "v3/r2"), { recursive: true });

const vars = { BUDGET_REQUESTS_PER_DAY: "14", BUDGET_R2_READS_PER_MONTH: "3", BUDGET_DO_CALLS_PER_DAY: "1000", BUDGET_FLUSH_EVENTS: "1" };
const worker = await unstable_startWorker({
  config: join(root, "wrangler.toml"),
  bindings: Object.fromEntries(Object.entries(vars).map(([k, v]) => [k, { type: "plain_text", value: v }])),
  dev: { server: { port: 0 }, persist: state, inspector: false, remote: false },
});

let failures = 0;
const check = (name, ok, detail = "") => {
  console.log(`${ok ? "PASS" : "FAIL"}  ${name}${detail ? "  " + detail : ""}`);
  if (!ok) failures++;
};
const get = (path, init) => worker.fetch("http://localhost" + path, init);
const settle = () => new Promise((r) => setTimeout(r, 150)); // let the background report land
const hex = (c) => c.repeat(64);

try {
  let r = await get("/asset-bay/budget");
  const start = await r.json();
  check("budget report", r.status === 200 && start.limits.r2ReadsPerMonth === 3, JSON.stringify(start.limits));
  const base = start.usage.requests ?? 0, baseR2 = start.usage.r2Reads ?? 0;
  check("fresh counters", base <= 1 && baseR2 === 0, JSON.stringify(start.usage));

  r = await get("/asset-bay/feed"); await settle();          // 2 R2 reads
  check("feed works under the limit", r.status === 200);
  r = await get("/asset-bay/media/videos/test.bin"); await r.arrayBuffer(); await settle(); // 3rd read
  check("media works under the limit", r.status === 200);
  r = await get("/asset-bay/media/videos/test.bin"); await settle();
  const body = await r.json().catch(() => ({}));
  check("R2 pauses at its limit", r.status === 503 && /R2/.test(body.paused || ""), `status ${r.status} ${JSON.stringify(body)}`);
  check("paused reply says when to retry", Number(r.headers.get("Retry-After")) > 0);

  r = await get("/asset-bay/presence", { method: "POST", body: JSON.stringify({ room: hex("a"), player: hex("b"), state: { open: true } }) });
  check("presence still works while R2 is paused", r.status === 200, `status ${r.status}`);

  let pausedAt = 0;
  for (let i = 0; i < 20 && !pausedAt; i++) {
    r = await get("/asset-bay/health"); await settle();
    if (r.status === 503) pausedAt = i + 1;
  }
  check("everything pauses at the daily request limit", pausedAt > 0, `after ${pausedAt} more requests`);

  r = await get("/asset-bay/budget");
  const end = await r.json();
  check("budget report still reachable while paused", r.status === 200 && end.paused.all && end.paused.r2, JSON.stringify(end.paused));
} finally {
  await worker.dispose();
}
console.log(failures ? `FAILURES: ${failures}` : "FAILURES: 0");
process.exit(failures ? 1 : 0);
