// Live (WebSocket) presence test.   node test/live.test.mjs
import { unstable_startWorker } from "wrangler";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { rmSync } from "node:fs";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const state = join(root, ".wrangler/live-test-state");
rmSync(state, { recursive: true, force: true });
const worker = await unstable_startWorker({
  config: join(root, "wrangler.toml"),
  dev: { server: { port: 0 }, persist: state, inspector: false, remote: false },
});
const base = (await worker.url).toString().replace(/\/$/, "");

let failures = 0;
const check = (name, ok, detail = "") => {
  console.log(`${ok ? "PASS" : "FAIL"}  ${name}${detail ? "  " + detail : ""}`);
  if (!ok) failures++;
};
const wait = (ms) => new Promise((r) => setTimeout(r, ms));
const room = "7".repeat(64), A = "a".repeat(64), B = "b".repeat(64), C = "c".repeat(64);

function connect(player) {
  const ws = new WebSocket(`${base.replace("http", "ws")}/asset-bay/live?room=${room}&player=${player}`);
  const inbox = [];
  ws.onmessage = (e) => inbox.push(JSON.parse(e.data));
  const opened = new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej; });
  const next = async (t, ms = 2000) => {
    const end = Date.now() + ms;
    while (Date.now() < end) {
      const i = inbox.findIndex((m) => m.t === t);
      if (i >= 0) return inbox.splice(i, 1)[0];
      await wait(20);
    }
    return null;
  };
  return { ws, opened, next, send: (m) => ws.send(JSON.stringify(m)) };
}
const post = (path, body) => fetch(`${base}/asset-bay/${path}`, { method: "POST", body: JSON.stringify(body) });

try {
  const a = connect(A); await a.opened;
  const hello = await a.next("members");
  check("connect gets a member snapshot", hello && hello.members.length === 0);

  const b = connect(B); await b.opened; await b.next("members");
  a.send({ t: "state", state: { open: true, theme: "Lava", control: "browse" } });
  let m = await b.next("member");
  check("state is pushed to others instantly", m && m.player === A && m.state.theme === "Lava");

  let t0 = Date.now();
  b.send({ t: "control", target: A, cmd: { a: "row", page: "Library", key: "videos" } });
  const cmd = await a.next("cmd");
  check("a press arrives live", cmd && cmd.from === B && cmd.cmd.key === "videos", `${Date.now() - t0} ms`);

  a.send({ t: "control", target: B, cmd: { a: "open" } });
  const err = await a.next("error");
  check("refused when the target hasn't allowed control", err && /isn't shared/.test(err.error));

  // A polling (older) menu in the same room sees live members and is seen by them.
  let r = await post("presence", { room, player: C, state: { theme: "Halo" } });
  let j = await r.json();
  check("polling members see live members", j.members.some((x) => x.player === A));
  m = await a.next("member");
  check("live members see polling members", m && m.player === C);
  r = await post("control", { room, from: C, target: A, cmd: { a: "open" } });
  check("polling member's press reaches a live member", (await a.next("cmd"))?.from === C && r.status === 200);

  // Reconnect replaces the old socket without announcing a leave.
  const a2 = connect(A); await a2.opened; await a2.next("members");
  await wait(200);
  check("reconnect isn't reported as leaving", !(b.ws && (await b.next("leave", 300))));

  a2.ws.close();
  const left = await b.next("leave");
  check("closing announces leaving", left && left.player === A);
  b.ws.close();
} finally {
  await worker.dispose();
}
console.log(failures ? `FAILURES: ${failures}` : "FAILURES: 0");
process.exit(failures ? 1 : 0);
