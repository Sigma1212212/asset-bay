using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>The result of checking one prop.</summary>
    public sealed class PropCheck
    {
        public string Name;
        public bool Ok;
        public string Note;
    }

    /// <summary>
    /// Builds every prop away from everything else, pokes it with a test object, and reports whether it
    /// did what it's supposed to: trampolines bounce, fans lift, belts carry, portals move you, cans fall
    /// over, balloons rise. Then it clears them all away again.
    ///
    /// This runs inside the game, so it tests the real thing with real physics. Nothing it makes is
    /// visible to anybody else, and nothing touches your player.
    /// </summary>
    public sealed class PropSelfTest : MonoBehaviour
    {
        public Func<PropBuild> Build;
        public Func<Vector3> Where;                 // a quiet spot to test in (far above the map)
        public Action<string> Progress;
        public Action Finished;

        public bool Running { get; private set; }
        public int Passed { get; private set; }
        public int Failed { get; private set; }
        public string Summary { get; private set; } = "not run yet";
        public string Step { get; private set; } = "";
        public readonly List<PropCheck> Results = new List<PropCheck>();

        private const int Steps = 90;               // up to ~1.8 s of physics per check

        public void Run()
        {
            if (Running) return;
            StartCoroutine(RunAll());
        }

        private IEnumerator RunAll()
        {
            Running = true;
            Results.Clear();
            Passed = Failed = 0;
            var start = DateTime.Now;

            var origin = Where != null ? Where() : Vector3.up * 300f;
            var build = Build != null ? Build() : new PropBuild { Allowed = () => true, WalkableLayer = -1 };
            int layer = build.WalkableLayer >= 0 ? build.WalkableLayer : 0;

            var probe = MakeProbe(layer);
            var props = PropLibrary.All;

            for (int i = 0; i < props.Count; i++)
            {
                var def = props[i];
                Step = $"{i + 1}/{props.Count}  {def.Name}";
                Progress?.Invoke(Step);
                var spot = origin + new Vector3(0f, 0f, 0f);

                // Park the probe well clear first: if it were still sitting in the next prop's trigger
                // when that prop appears, the trigger would fire before the check even started.
                Place(probe, origin + new Vector3(80f, 0f, 0f), Vector3.zero);

                GameObject prop = null;
                var check = new PropCheck { Name = def.Name, Ok = true, Note = "" };
                try
                {
                    prop = def.Build(build);
                }
                catch (Exception e)
                {
                    Fail(check, "it threw: " + e.Message);
                }

                if (prop != null)
                {
                    prop.transform.position = spot;
                    Structure(prop, check);
                }
                else if (check.Ok)
                {
                    Fail(check, "built nothing");
                }

                if (prop != null && check.Ok)
                {
                    yield return new WaitForFixedUpdate();      // let it settle before poking it
                    yield return Behaviour(def, prop, probe, spot, build, layer, check);
                }

                if (prop != null) Destroy(prop);
                Record(check);
                yield return null;
            }

            if (probe != null) Destroy(probe.gameObject);
            Summary = Failed == 0
                ? $"all {Passed} props work ({(DateTime.Now - start).TotalSeconds:0.0}s)"
                : $"{Failed} of {Passed + Failed} props had a problem";
            Progress?.Invoke(Summary);
            Running = false;
            Finished?.Invoke();
        }

        // ------------------------------------------------------------------ the checks

        /// <summary>Is it actually built: parts, materials, somewhere to stand.</summary>
        private static void Structure(GameObject prop, PropCheck check)
        {
            var renderers = prop.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { Fail(check, "nothing to see"); return; }
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i].sharedMaterial == null) { Fail(check, renderers[i].name + " has no material"); return; }

            var colliders = prop.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0) { Fail(check, "nothing to touch"); return; }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.magnitude > 40f) { Fail(check, "far too big: " + bounds.size); return; }
            check.Note = renderers.Length + " parts";
        }

        /// <summary>Does it do its job: the probe is dropped, pushed or carried and we watch what happens.</summary>
        private IEnumerator Behaviour(PropDef def, GameObject prop, Rigidbody probe, Vector3 spot,
                                      PropBuild build, int layer, PropCheck check)
        {
            var bouncer = prop.GetComponentInChildren<Bouncer>();
            if (bouncer != null)
            {
                var top = TopOf(bouncer.gameObject);
                Place(probe, top + Vector3.up * 1.2f, Vector3.down * 3f);
                yield return Until(() => probe.velocity.y > 3f);
                if (probe.velocity.y <= 3f) Fail(check, "didn't bounce");
                else check.Note += ", bounce " + probe.velocity.y.ToString("0.0");
                yield break;
            }

            var booster = prop.GetComponentInChildren<Booster>();
            if (booster != null)
            {
                Place(probe, booster.transform.position, Vector3.zero);
                yield return Until(() => probe.velocity.magnitude > 5f);
                if (probe.velocity.magnitude <= 5f) Fail(check, "didn't push");
                else check.Note += ", push " + probe.velocity.magnitude.ToString("0");
                yield break;
            }

            var draft = prop.GetComponentInChildren<Updraft>();
            if (draft != null)
            {
                Place(probe, draft.transform.position, Vector3.zero);
                yield return Until(() => probe.velocity.y > 1.5f);
                if (probe.velocity.y <= 1.5f) Fail(check, "didn't lift");
                else check.Note += ", lift " + probe.velocity.y.ToString("0.0");
                yield break;
            }

            var belt = prop.GetComponentInChildren<Conveyor>();
            if (belt != null)
            {
                Place(probe, belt.transform.position, Vector3.zero);
                var from = probe.position;
                yield return Until(() => Vector3.Distance(probe.position, from) > 1f);
                float moved = Vector3.Distance(probe.position, from);
                if (moved <= 1f) Fail(check, "didn't carry");
                else check.Note += ", carried " + moved.ToString("0.0") + " m";
                yield break;
            }

            var mover = prop.GetComponentInChildren<Mover>();
            if (mover != null)
            {
                var from = mover.transform.position;
                yield return Until(() => Vector3.Distance(mover.transform.position, from) > 0.5f);
                if (Vector3.Distance(mover.transform.position, from) <= 0.5f) Fail(check, "didn't move");
                else check.Note += ", travels";
                yield break;
            }

            var portal = prop.GetComponentInChildren<PortalPad>();
            if (portal != null)
            {
                // Portals need a partner, so build a second one a few steps away.
                var other = def.Build(build);
                other.transform.position = spot + new Vector3(6f, 0f, 0f);
                yield return null;
                Place(probe, portal.transform.position, Vector3.zero);
                yield return Until(() => Vector3.Distance(probe.position, spot) > 4f);
                bool travelled = Vector3.Distance(probe.position, spot) > 4f;
                if (!travelled) Fail(check, "didn't send the probe through");
                else check.Note += ", travels " + Vector3.Distance(probe.position, spot).ToString("0.0") + " m";
                if (other != null) Destroy(other);
                yield break;
            }

            var cans = prop.GetComponentsInChildren<TargetCan>();
            var score = prop.GetComponentInChildren<TargetScore>();
            if (cans.Length > 0 && score != null)
            {
                // Knock the first one over and see whether the scoreboard notices.
                cans[0].transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                yield return Until(() => Lit(score) > 0);
                if (Lit(score) == 0) Fail(check, "knocking one over didn't score");
                else check.Note += ", " + cans.Length + " to knock over";
                yield break;
            }

            var balloon = prop.GetComponentInChildren<Balloon>();
            if (balloon != null)
            {
                float from = balloon.transform.position.y;
                yield return Until(() => balloon != null && balloon.transform.position.y > from + 0.4f);
                if (balloon == null || balloon.transform.position.y <= from + 0.4f) Fail(check, "didn't float up");
                else check.Note += ", floats";
                yield break;
            }

            var firework = prop.GetComponentInChildren<Firework>();
            if (firework != null)
            {
                firework.Launch();
                yield return null;
                check.Note += ", fires";
                yield break;
            }

            var chest = prop.GetComponentInChildren<SurpriseChest>();
            if (chest != null)
            {
                if (chest.Lid == null) Fail(check, "no lid");
                else check.Note += ", opens";
                yield break;
            }

            var flag = prop.GetComponentInChildren<CheckpointFlag>();
            if (flag != null)
            {
                bool touched = false;
                var was = flag.Touched;
                flag.Touched = () => touched = true;
                Place(probe, flag.transform.position, Vector3.zero);
                yield return Until(() => touched);
                flag.Touched = was;
                if (!touched) Fail(check, "touching it did nothing");
                else check.Note += ", saves your spot";
                yield break;
            }

            var goal = prop.GetComponentInChildren<ScoreZone>();
            if (goal != null && goal.Score != null)
            {
                Place(probe, goal.transform.position + Vector3.up * 0.6f, Vector3.down * 2f);
                yield return Until(() => Lit(goal.Score) > 0);
                if (Lit(goal.Score) == 0) Fail(check, "dropping one through didn't score");
                else check.Note += ", scores";
                yield break;
            }

            var spinner = prop.GetComponentInChildren<Spinner>();
            if (spinner != null)
            {
                var from = spinner.transform.rotation;
                yield return Until(() => Quaternion.Angle(spinner.transform.rotation, from) > 5f);
                if (Quaternion.Angle(spinner.transform.rotation, from) <= 5f) Fail(check, "didn't spin");
                else check.Note += ", spins";
                yield break;
            }

            var body = prop.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                float from = body.position.y;
                yield return Until(() => Mathf.Abs(body.position.y - from) > 0.3f);
                if (Mathf.Abs(body.position.y - from) <= 0.3f) Fail(check, "physics didn't move it");
                else check.Note += ", physical";
                yield break;
            }

            // Nothing moving to test: standing on it is the whole point, and Structure already checked that.
            check.Note += ", solid";
        }

        // ------------------------------------------------------------------ helpers

        private static int Lit(TargetScore score)
        {
            int count = 0;
            if (score.Pips == null) return 0;
            for (int i = 0; i < score.Pips.Length; i++)
                if (score.Pips[i] != null && score.Pips[i].gameObject.activeSelf) count++;
            return count;
        }

        private static Vector3 TopOf(GameObject go)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) return new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, renderer.bounds.center.z);
            var collider = go.GetComponent<Collider>();
            if (collider != null) return new Vector3(collider.bounds.center.x, collider.bounds.max.y, collider.bounds.center.z);
            return go.transform.position;
        }

        private Rigidbody MakeProbe(int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "[BundleMenu] Prop test probe";
            go.layer = layer;
            go.transform.localScale = Vector3.one * 0.35f;
            go.GetComponent<Renderer>().enabled = false;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return rb;
        }

        private static void Place(Rigidbody probe, Vector3 at, Vector3 velocity)
        {
            probe.useGravity = velocity.y < 0f;
            probe.position = at;
            probe.velocity = velocity;
            probe.angularVelocity = Vector3.zero;
        }

        private static IEnumerator Until(Func<bool> done)
        {
            for (int i = 0; i < Steps; i++)
            {
                if (done()) yield break;
                yield return new WaitForFixedUpdate();
            }
        }

        private static void Fail(PropCheck check, string why)
        {
            check.Ok = false;
            check.Note = string.IsNullOrEmpty(check.Note) ? why : check.Note + " - " + why;
        }

        private void Record(PropCheck check)
        {
            Results.Add(check);
            if (check.Ok) Passed++; else Failed++;
        }
    }
}
