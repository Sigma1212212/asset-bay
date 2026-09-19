// Remote control test.   node test/control.test.mjs
import { unstable_startWorker } from "wrangler";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { rmSync } from "node:fs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const state = join(root, ".wrangler/control-test-state");
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
const post = (path, body) => worker.fetch("http://localhost/asset-bay/" + path, { method: "POST", body: JSON.stringify(body) });
const room = "1".repeat(64), owner = "a".repeat(64), friend = "b".repeat(64), stranger = "c".repeat(64);
const wait = (ms) => new Promise((r) => setTimeout(r, ms));
const cmd = { page: "Library", key: "videos" };

try {
  await post("presence", { room, player: owner, state: { open: true, control: "off" } });
  await post("presence", { room, player: friend, state: { open: false } });

  let r = await post("control", { room, from: friend, target: owner, cmd });
  check("refused while the owner has control off", r.status === 403);

  await wait(1100);
  await post("presence", { room, player: owner, state: { open: true, control: "browse" } });
  r = await post("control", { room, from: friend, target: owner, cmd });
  check("accepted once the owner turns it on", r.status === 200);

  r = await post("control", { room, from: friend, target: owner, cmd });
  check("rapid presses throttled", r.status === 429);

  r = await post("control", { room, from: stranger, target: owner, cmd });
  check("non-members refused", r.status === 404);

  r = await post("control", { room, from: owner, target: owner, cmd });
  check("can't target yourself", r.status === 400);

  r = await post("control", { room, from: friend, target: owner, cmd: { key: "x".repeat(400) } });
  check("oversized command refused", r.status === 400);

  await wait(1100);
  r = await post("presence", { room, player: owner, state: { open: true, control: "browse" } });
  let j = await r.json();
  check("owner receives the press on its next update", j.commands.length === 1 && j.commands[0].from === friend && j.commands[0].cmd.key === "videos");

  await wait(1100);
  r = await post("presence", { room, player: owner, state: { open: true, control: "browse" } });
  j = await r.json();
  check("presses are delivered once", j.commands.length === 0);

  r = await post("presence", { room: "2".repeat(64), player: friend, state: {} });
  r = await post("control", { room: "2".repeat(64), from: friend, target: owner, cmd });
  check("other rooms can't reach the owner", r.status === 404);

  const rows = Array.from({ length: 8 }, (_, i) => ({ k: "row" + i, l: "A label of twenty-four c", v: "value", on: false }));
  await wait(1100);
  r = await post("presence", { room, player: owner, state: { open: true, control: "full", rows } });
  check("a page mirror fits in the state", r.status === 200, `status ${r.status}`);
} finally {
  await worker.dispose();
}
console.log(failures ? `FAILURES: ${failures}` : "FAILURES: 0");
process.exit(failures ? 1 : 0);
