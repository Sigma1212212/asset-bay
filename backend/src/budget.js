// Spending guard: keeps Asset Bay inside Cloudflare's free allowances by switching features off
// *before* anything could be billed, instead of paying for overage.
//
// What can cost money, and the cut-off used (about 80% of each free allowance):
//   Worker requests       free 100k/day            -> everything pauses at 80k/day
//   Durable Object calls  free 100k/day            -> presence pauses at 60k/day  (presence + this counter)
//   R2 reads (Class B)    free 10M/month           -> feed + media pause at 8M/month
//   R2 storage            free 10 GB               -> publish-feed.ps1 refuses uploads past 9 GB
// Days and months roll over in UTC, the same as Cloudflare's billing, and everything turns back on then.
//
// Counting is batched: each Worker instance adds up locally and reports to one Budget object every
// ~30 s or 200 events, so the guard itself only uses a few thousand of the Durable Object calls a day.
// Instances learn the "paused" state from each report, so a cut-off takes effect within ~30 s.

const DEFAULTS = {
  REQUESTS_PER_DAY: 80_000,
  DO_CALLS_PER_DAY: 60_000,
  R2_READS_PER_MONTH: 8_000_000,
  FLUSH_MS: 30_000,
  FLUSH_EVENTS: 200,
};

const limit = (env, name) => {
  const v = Number(env?.["BUDGET_" + name]);
  return Number.isFinite(v) && v >= 0 ? v : DEFAULTS[name];
};

// Per-instance state (module scope lives as long as the Worker instance).
let pending = { requests: 0, doCalls: 0, r2Reads: 0 };
let pendingTotal = 0;
let lastFlush = 0;
let flushing = null;
let paused = { all: false, presence: false, r2: false };

export function count(kind, n = 1) {
  pending[kind] += n;
  pendingTotal += n;
}

/** "all" | "presence" | "r2" -> is that feature switched off right now? */
export function isPaused(kind) {
  return paused.all || paused[kind];
}

/** Report counts if it's time (or forced). Safe to call on every request; use ctx.waitUntil. */
export async function flush(env, force = false) {
  if (!env.BUDGET) return;
  const due = force || Date.now() - lastFlush >= limit(env, "FLUSH_MS") || pendingTotal >= limit(env, "FLUSH_EVENTS");
  if (!due || flushing) return flushing;

  const batch = pending;
  pending = { requests: 0, doCalls: 0, r2Reads: 0 };
  pendingTotal = 0;
  lastFlush = Date.now();
  batch.doCalls += 1; // this report is itself a Durable Object call

  flushing = (async () => {
    try {
      const stub = env.BUDGET.get(env.BUDGET.idFromName("global"));
      const r = await stub.fetch("https://budget/add", { method: "POST", body: JSON.stringify(batch) });
      const status = await r.json();
      paused = status.paused;
    } catch {
      // Couldn't report: put the counts back so they're sent next time.
      for (const k of Object.keys(batch)) pending[k] += batch[k];
    } finally {
      flushing = null;
    }
  })();
  return flushing;
}

/** Current usage and limits (GET /asset-bay/budget). Doesn't count as usage beyond the call itself. */
export async function report(env) {
  await flush(env, true);
  const stub = env.BUDGET.get(env.BUDGET.idFromName("global"));
  return (await stub.fetch("https://budget/status")).json();
}

export class Budget {
  constructor(state, env) {
    this.storage = state.storage;
    this.env = env;
  }

  async fetch(request) {
    const now = new Date();
    const day = now.toISOString().slice(0, 10);   // UTC, like Cloudflare's daily limits
    const month = day.slice(0, 7);

    let usage = (await this.storage.get("usage")) || {};
    if (usage.day !== day) usage = { ...usage, day, requests: 0, doCalls: 0 };
    if (usage.month !== month) usage = { ...usage, month, r2Reads: 0 };

    if (request.method === "POST") {
      const add = await request.json();
      for (const k of ["requests", "doCalls", "r2Reads"]) {
        const n = Number(add[k]);
        if (Number.isFinite(n) && n > 0) usage[k] = (usage[k] || 0) + Math.min(n, 1e7);
      }
      await this.storage.put("usage", usage);
    }

    const limits = {
      requestsPerDay: limit(this.env, "REQUESTS_PER_DAY"),
      doCallsPerDay: limit(this.env, "DO_CALLS_PER_DAY"),
      r2ReadsPerMonth: limit(this.env, "R2_READS_PER_MONTH"),
    };
    const paused = {
      all: usage.requests >= limits.requestsPerDay,
      presence: usage.doCalls >= limits.doCallsPerDay,
      r2: usage.r2Reads >= limits.r2ReadsPerMonth,
    };
    return new Response(JSON.stringify({ usage, limits, paused }), {
      headers: { "Content-Type": "application/json" },
    });
  }
}
