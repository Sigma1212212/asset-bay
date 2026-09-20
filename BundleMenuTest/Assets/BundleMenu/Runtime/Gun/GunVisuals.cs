using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// Everything you can see of the gun: the laser, the reticle on the surface, the little gun in your
    /// hand, its muzzle flash and recoil, sparks where shots land, and a crosshair on screen when you're
    /// aiming down the middle. All built from primitives in code - no art files needed.
    ///
    /// It draws only while you're aiming, reuses one pool of sparks, and colours everything through a
    /// property block instead of making new materials.
    /// </summary>
    internal sealed class GunVisuals
    {
        private LineRenderer line;
        private Transform reticle, ring, model, muzzleFlash, muzzlePoint;
        private Material lineMat, partMat;
        private MaterialPropertyBlock block;
        private RectTransform crosshair;
        private Image crossDot;
        private readonly Image[] crossTicks = new Image[4];

        private float kick, flash;
        private const int SparkCount = 10;
        private readonly Transform[] sparks = new Transform[SparkCount];
        private readonly Renderer[] sparkRenderers = new Renderer[SparkCount];
        private Renderer reticleRenderer, flashRenderer;
        private readonly Vector3[] sparkVel = new Vector3[SparkCount];
        private readonly float[] sparkLife = new float[SparkCount];
        private readonly Color[] sparkColor = new Color[SparkCount];
        private int nextSpark;
        private bool built;

        // ------------------------------------------------------------------ building

        private void Build()
        {
            if (built) return;
            built = true;
            block = new MaterialPropertyBlock();

            var shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            lineMat = new Material(shader);

            var lineGo = new GameObject("[BundleMenu] Gun Laser");
            Object.DontDestroyOnLoad(lineGo);
            line = lineGo.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMat;
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.numCapVertices = 4;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            lineGo.SetActive(false);

            reticle = Primitive(PrimitiveType.Sphere, "[BundleMenu] Gun Reticle", null);
            Object.DontDestroyOnLoad(reticle.gameObject);
            reticleRenderer = reticle.GetComponent<Renderer>();
            partMat = new Material(reticleRenderer.sharedMaterial) { name = "gun (menu)" };
            reticleRenderer.sharedMaterial = partMat;
            ring = Primitive(PrimitiveType.Cylinder, "Ring", reticle);
            reticle.gameObject.SetActive(false);

            for (int i = 0; i < SparkCount; i++)
            {
                var spark = Primitive(PrimitiveType.Cube, "Spark", null);
                Object.DontDestroyOnLoad(spark.gameObject);
                spark.gameObject.SetActive(false);
                sparks[i] = spark;
                sparkRenderers[i] = spark.GetComponent<Renderer>();
            }
        }

        private Transform Primitive(PrimitiveType type, string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);   // must never block our own ray
            var renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (partMat != null) renderer.sharedMaterial = partMat;
            if (parent != null) go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ------------------------------------------------------------------ per-frame

        public void Draw(GunLib gun, Vector3 muzzle, GunHit hit, MenuTheme theme, GunMode mode)
        {
            Build();
            line.gameObject.SetActive(true);
            reticle.gameObject.SetActive(true);

            Color a = theme != null ? theme.Accent : Color.cyan;
            Color b = theme != null ? theme.Accent2 : Color.magenta;
            var tint = mode?.Tint(hit);
            if (tint != null) { a = tint.Value; b = Color.Lerp(tint.Value, b, 0.35f); }

            float dt = Time.unscaledDeltaTime;
            kick = Mathf.MoveTowards(kick, 0f, dt * 3.5f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 8f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
            float charge = gun.Charge;

            // Laser: thin at the barrel, wider at the target; white while charging or firing.
            var start = muzzlePoint != null ? muzzlePoint.position : muzzle;
            line.SetPosition(0, start);
            line.SetPosition(1, hit.Point);
            float width = 0.006f + 0.012f * kick + 0.01f * charge;
            line.startWidth = width * 0.6f;
            line.endWidth = width * (hit.HasHit ? 1.6f : 0.8f);
            var head = Color.Lerp(a, Color.white, Mathf.Max(kick, charge));
            head.a = 0.9f;
            var tail = Color.Lerp(b, Color.white, kick);
            tail.a = hit.HasHit ? 0.9f : 0.25f;
            line.startColor = head;
            line.endColor = tail;

            // Reticle: a dot with a ring lying flat on the surface, sized so it reads at any distance.
            reticle.position = hit.Point;
            float size = Mathf.Clamp(hit.Distance * 0.012f, 0.03f, 0.35f) * (1f + 0.15f * pulse + 0.6f * kick + 0.5f * charge);
            reticle.localScale = Vector3.one * size;
            Paint(reticleRenderer, Color.Lerp(a, Color.white, Mathf.Max(kick * 0.7f, charge)));
            ring.gameObject.SetActive(hit.HasHit);
            if (hit.HasHit)
            {
                reticle.rotation = Quaternion.FromToRotation(Vector3.up, hit.Normal);
                float r = 2.4f + 0.4f * pulse + 2f * charge;
                ring.localScale = new Vector3(r, 0.02f, r);
                ring.localPosition = Vector3.zero;
            }

            DrawCrosshair(gun, a, hit, charge);
            StepSparks(dt);
        }

        /// <summary>The gun itself: in your right hand in VR, tucked into the corner of the view on PC.</summary>
        public void DrawModel(GunLib gun, IRigProvider rig, bool vr)
        {
            if (!gun.ShowModel) { HideModel(); return; }
            Build();
            if (model == null) BuildModel();
            model.gameObject.SetActive(true);

            var hand = vr ? (rig as GorillaTagRig)?.RightHandTransform : null;
            var cam = rig?.Camera != null ? rig.Camera.transform : null;
            if (hand != null)
            {
                model.SetPositionAndRotation(hand.position, hand.rotation);
                model.localScale = Vector3.one * 0.6f;
            }
            else if (cam != null)
            {
                // A held-gun pose that leans with your movement, so it doesn't look pasted on the screen.
                float bob = Mathf.Sin(Time.unscaledTime * 6f) * 0.004f;
                var offset = new Vector3(0.17f, -0.13f + bob, 0.32f - 0.05f * kick);
                model.SetPositionAndRotation(cam.TransformPoint(offset), cam.rotation * Quaternion.Euler(-6f + 14f * kick, -8f, 3f));
                model.localScale = Vector3.one * 0.42f;
            }

            if (muzzleFlash != null)
            {
                bool lit = flash > 0.01f;
                if (muzzleFlash.gameObject.activeSelf != lit) muzzleFlash.gameObject.SetActive(lit);
                if (lit)
                {
                    muzzleFlash.localScale = new Vector3(1f, 1f, 1.6f) * (0.06f + 0.16f * flash);
                    Paint(flashRenderer, new Color(1f, 0.92f, 0.6f, flash));
                }
            }
        }

        private void BuildModel()
        {
            var root = new GameObject("[BundleMenu] Gun").transform;
            Object.DontDestroyOnLoad(root.gameObject);

            var body = Primitive(PrimitiveType.Cube, "Body", root);
            body.localPosition = new Vector3(0f, 0f, 0.06f);
            body.localScale = new Vector3(0.055f, 0.075f, 0.22f);

            var barrel = Primitive(PrimitiveType.Cylinder, "Barrel", root);
            barrel.localPosition = new Vector3(0f, 0.015f, 0.22f);
            barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.localScale = new Vector3(0.035f, 0.11f, 0.035f);

            var grip = Primitive(PrimitiveType.Cube, "Grip", root);
            grip.localPosition = new Vector3(0f, -0.075f, -0.01f);
            grip.localRotation = Quaternion.Euler(18f, 0f, 0f);
            grip.localScale = new Vector3(0.05f, 0.12f, 0.06f);

            var core = Primitive(PrimitiveType.Cube, "Core", root);
            core.localPosition = new Vector3(0f, 0.05f, 0.05f);
            core.localScale = new Vector3(0.025f, 0.02f, 0.14f);

            muzzlePoint = new GameObject("Muzzle").transform;
            muzzlePoint.SetParent(root, false);
            muzzlePoint.localPosition = new Vector3(0f, 0.015f, 0.33f);

            muzzleFlash = Primitive(PrimitiveType.Sphere, "Flash", muzzlePoint);
            muzzleFlash.localPosition = Vector3.zero;
            flashRenderer = muzzleFlash.GetComponent<Renderer>();
            muzzleFlash.gameObject.SetActive(false);

            model = root;
        }

        public Vector3 MuzzlePoint(Vector3 fallback) => muzzlePoint != null ? muzzlePoint.position : fallback;

        /// <summary>Recoil and muzzle flash for one shot.</summary>
        public void Kick(float strength)
        {
            kick = Mathf.Clamp01(strength);
            flash = 1f;
        }

        /// <summary>A short burst of sparks where a shot landed.</summary>
        public void Impact(Vector3 point, Vector3 normal, Color color)
        {
            Build();
            for (int i = 0; i < 4; i++)
            {
                int index = nextSpark = (nextSpark + 1) % SparkCount;
                var spark = sparks[index];
                spark.gameObject.SetActive(true);
                spark.position = point + normal * 0.02f;
                spark.localScale = Vector3.one * Random.Range(0.02f, 0.05f);
                spark.rotation = Random.rotation;
                sparkVel[index] = (normal + Random.insideUnitSphere * 0.8f).normalized * Random.Range(1.2f, 3f);
                sparkLife[index] = 1f;
                sparkColor[index] = color;
                liveSparks++;
            }
        }

        private int liveSparks;

        private void StepSparks(float dt)
        {
            if (liveSparks <= 0) return;
            liveSparks = 0;
            for (int i = 0; i < SparkCount; i++)
            {
                if (sparkLife[i] <= 0f) continue;
                sparkLife[i] -= dt * 1.8f;
                if (sparkLife[i] <= 0f) { sparks[i].gameObject.SetActive(false); continue; }
                liveSparks++;
                sparkVel[i] += Physics.gravity * dt * 0.5f;
                sparks[i].position += sparkVel[i] * dt;
                sparks[i].localScale = Vector3.one * (0.05f * sparkLife[i]);
                var c = sparkColor[i];
                Paint(sparkRenderers[i], new Color(c.r, c.g, c.b, sparkLife[i]));
            }
        }

        private void Paint(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------------ crosshair

        private void DrawCrosshair(GunLib gun, Color color, GunHit hit, float charge)
        {
            var canvas = gun.Overlay?.Invoke();
            bool wanted = canvas != null && !MenuInput.VRActive && gun.Aim == GunAim.Crosshair;
            if (!wanted) { if (crosshair != null) crosshair.gameObject.SetActive(false); return; }

            if (crosshair == null)
            {
                crosshair = UIFactory.Rect("[BundleMenu] Crosshair", canvas.transform);
                crosshair.anchorMin = crosshair.anchorMax = new Vector2(0.5f, 0.5f);
                crosshair.pivot = new Vector2(0.5f, 0.5f);
                crosshair.anchoredPosition = Vector2.zero;
                crosshair.sizeDelta = new Vector2(40f, 40f);
                crossDot = UIFactory.Image(crosshair, "Dot", UISprites.RoundedFill(3f), color);
                var dot = crossDot.rectTransform;
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.sizeDelta = new Vector2(4f, 4f);

                for (int i = 0; i < 4; i++)
                {
                    var tick = UIFactory.Image(crosshair, "Tick" + i, UISprites.RoundedFill(1f), color);
                    var rt = tick.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    bool horizontal = i < 2;
                    rt.sizeDelta = horizontal ? new Vector2(8f, 2f) : new Vector2(2f, 8f);
                    crossTicks[i] = tick;
                }
            }

            crosshair.gameObject.SetActive(true);
            float spread = 7f + 5f * kick + 8f * charge + (hit.HasHit ? 0f : 2f);
            for (int i = 0; i < 4; i++)
            {
                var rt = crossTicks[i].rectTransform;
                rt.anchoredPosition = i == 0 ? new Vector2(-spread, 0f)
                                    : i == 1 ? new Vector2(spread, 0f)
                                    : i == 2 ? new Vector2(0f, spread)
                                             : new Vector2(0f, -spread);
                crossTicks[i].color = color.WithAlpha(0.85f);
            }
            crossDot.color = Color.Lerp(color, Color.white, Mathf.Max(kick, charge)).WithAlpha(hit.HasHit ? 1f : 0.5f);
        }

        // ------------------------------------------------------------------ hiding / cleanup

        public void Hide()
        {
            if (line != null && line.gameObject.activeSelf) line.gameObject.SetActive(false);
            if (reticle != null && reticle.gameObject.activeSelf) reticle.gameObject.SetActive(false);
            if (crosshair != null && crosshair.gameObject.activeSelf) crosshair.gameObject.SetActive(false);
            HideModel();
            StepSparks(Time.unscaledDeltaTime);
        }

        public void HideModel()
        {
            if (model != null && model.gameObject.activeSelf) model.gameObject.SetActive(false);
        }

        public void Destroy()
        {
            if (line != null) Object.Destroy(line.gameObject);
            if (reticle != null) Object.Destroy(reticle.gameObject);
            if (model != null) Object.Destroy(model.gameObject);
            if (crosshair != null) Object.Destroy(crosshair.gameObject);
            for (int i = 0; i < SparkCount; i++) if (sparks[i] != null) Object.Destroy(sparks[i].gameObject);
            if (lineMat != null) Object.Destroy(lineMat);
            if (partMat != null) Object.Destroy(partMat);
            built = false;
        }
    }
}
