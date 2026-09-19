using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BundleMenu
{
    public enum GuiView { Both, Flat, Preview }

    /// <summary>
    /// The desktop GUI: a dedicated, draggable window with two panes.
    ///
    ///   Window (CanvasGroup)
    ///   ├ Frame: glow, gradient body, rim
    ///   ├ Top bar: brand · [2D][3D][Both] · close      ← drag the window here
    ///   ├ Flat slot   → a flat MenuView (the 2D GUI)   ← owned by the controller
    ///   └ Preview slot (rounded well, RawImage)
    ///       shows the REAL world-space menu, parked far off-map on its own layer and filmed by a hidden
    ///       perspective camera, tilted so it reads as 3D and leaning a little toward the mouse.
    ///       Mouse → preview UV → camera ray → the hidden panel, so it's clickable too.
    /// </summary>
    public sealed class ClickGui
    {
        public static readonly Vector3 HiddenOrigin = new Vector3(6000f, -6000f, 6000f);

        private const float Pad = 20f, Gap = 16f, TopBar = 46f, PreviewWidth = 440f;
        private const float BaseYaw = -20f, BasePitch = 4f, Parallax = 7f, Fov = 24f, SuperSample = 2f;

        public RectTransform Window { get; }
        public CanvasGroup Group { get; }
        public RectTransform FlatSlot { get; }
        public Camera Camera { get; }
        public int Layer { get; }
        public GuiView View { get; private set; }
        /// <summary>User-chosen size; the open/close animation scales relative to this.</summary>
        public float BaseScale { get; private set; } = 1f;
        public bool Visible => Window.gameObject.activeSelf;
        public bool PreviewVisible => Visible && View != GuiView.Flat;

        public event Action<GuiView> ViewChanged;
        public event Action CloseRequested;

        private readonly Canvas screenCanvas;
        private readonly RectTransform previewSlot, topBar;
        private readonly RawImage previewImage;
        private readonly Image frameBody, frameRim, frameGlow, previewWell, previewMask;
        private readonly TextMeshProUGUI brand;
        private MenuButton viewBoth, view2D, view3D, close;
        private RenderTexture texture;
        private Transform worldCanvas;
        private Vector2 panelSize;
        private float yaw = BaseYaw, pitch = BasePitch, lastRender = -1f, fastUntil;
        private Camera excludedFrom;
        private int excludedMask;
        private MenuTheme theme;

        public ClickGui(Canvas screenCanvas)
        {
            this.screenCanvas = screenCanvas;
            Layer = FindFreeLayer();

            var camGo = new GameObject("[BundleMenu] Desktop GUI Camera");
            UnityEngine.Object.DontDestroyOnLoad(camGo);
            Camera = camGo.AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.cullingMask = 1 << Layer;
            Camera.fieldOfView = Fov;
            Camera.nearClipPlane = 0.05f;
            Camera.farClipPlane = 20f;
            Camera.allowHDR = false;
            Camera.allowMSAA = false;
            Camera.depth = -100;
            Camera.enabled = false;

            Window = UIFactory.Rect("Desktop GUI", screenCanvas.transform);
            Window.anchorMin = Window.anchorMax = Window.pivot = new Vector2(0.5f, 0.5f);
            Group = Window.gameObject.AddComponent<CanvasGroup>();

            frameGlow = UIFactory.Image(Window, "Glow", null, Color.white);
            frameBody = UIFactory.Image(Window, "Body", null, Color.white);
            frameBody.rectTransform.Stretch();
            frameBody.gameObject.AddComponent<UIGradient>();
            frameRim = UIFactory.Image(Window, "Rim", null, Color.white);
            frameRim.rectTransform.Stretch();
            frameRim.gameObject.AddComponent<UIGradient>();

            topBar = UIFactory.Rect("TopBar", Window);
            topBar.TopStrip(0f, TopBar, Pad, Pad);
            brand = UIFactory.Text(topBar, "Brand", null, 13, TextAlignmentOptions.MidlineLeft, Color.white, FontStyles.Bold | FontStyles.UpperCase, 4f);
            brand.rectTransform.Stretch(2f, 0f, 240f, 0f);

            FlatSlot = UIFactory.Rect("Flat", Window);

            previewSlot = UIFactory.Rect("Preview", Window);
            previewWell = UIFactory.Image(previewSlot, "Well", null, Color.black);
            previewWell.rectTransform.Stretch();
            previewMask = UIFactory.Image(previewSlot, "Mask", null, Color.white);
            previewMask.rectTransform.Stretch();
            previewMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            previewImage = UIFactory.Rect("View", previewMask.transform).Stretch().gameObject.AddComponent<RawImage>();
            previewImage.raycastTarget = false;

            Window.gameObject.SetActive(false);
        }

        // ================================================================== show / hide / layout

        public void Show(Transform worldCanvasTransform, Vector2 panel, GuiView view, float scale, Vector2 normalizedPosition, MenuTheme menuTheme, string brandText)
        {
            worldCanvas = worldCanvasTransform;
            panelSize = panel;
            View = view;
            ApplyTheme(menuTheme);
            brand.text = brandText;

            bool flat = view != GuiView.Preview, preview = view != GuiView.Flat;
            float contentW = (flat ? panel.x : 0f) + (flat && preview ? Gap : 0f) + (preview ? PreviewWidth : 0f);
            var size = new Vector2(Pad + contentW + Pad, TopBar + panel.y + Pad);
            Window.sizeDelta = size;
            BaseScale = scale;
            Window.localScale = Vector3.one * scale;

            FlatSlot.gameObject.SetActive(flat);
            FlatSlot.Pin(new Vector2(0, 1), new Vector2(0, 1), new Vector2(Pad, -TopBar), new Vector2(panel.x, panel.y));
            previewSlot.gameObject.SetActive(preview);
            previewSlot.Pin(new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(Pad + (flat ? panel.x + Gap : 0f), -TopBar), new Vector2(PreviewWidth, panel.y));

            if (preview) SetupPreview();
            RefreshViewButtons();

            Place(normalizedPosition);
            Window.gameObject.SetActive(true);
            Window.SetAsLastSibling();
            RequestRender();
            if (preview) ExcludeFromMainCamera(); else RestoreMainCamera();
        }

        public void Hide()
        {
            Camera.enabled = false;
            Window.gameObject.SetActive(false);
            RestoreMainCamera();
        }

        private void SetupPreview()
        {
            int w = Mathf.CeilToInt(PreviewWidth * SuperSample), h = Mathf.CeilToInt(panelSize.y * SuperSample);
            if (texture == null || texture.width != w || texture.height != h)
            {
                if (texture != null) { texture.Release(); UnityEngine.Object.Destroy(texture); }
                texture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "BundleMenu Desktop GUI", useMipMap = false };
                texture.Create();
                Camera.targetTexture = texture;
                previewImage.texture = texture;
            }

            // Frame the tilted panel with room for its glow: fit the height at this field of view.
            float unit = worldCanvas.lossyScale.x;
            float frameHeight = panelSize.y * unit * 1.18f;
            float distance = frameHeight * 0.5f / Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad);
            Camera.transform.SetPositionAndRotation(HiddenOrigin + Vector3.back * distance, Quaternion.identity);
            Camera.farClipPlane = distance * 3f;
            ApplyTilt();
        }

        public void ApplyTheme(MenuTheme t)
        {
            theme = t;
            float r = t.PanelRadius + 6f;
            frameBody.sprite = UISprites.RoundedFill(r);
            frameBody.type = Image.Type.Sliced;
            frameBody.GetComponent<UIGradient>().Set(Darken(t.PanelTop, 0.72f), Darken(t.PanelBottom, 0.6f));
            frameRim.sprite = UISprites.RoundedEdge(r, 1.5f);
            frameRim.type = Image.Type.Sliced;
            frameRim.GetComponent<UIGradient>().Set(t.EdgeTop.WithAlpha(0.55f), t.EdgeBottom.WithAlpha(0.55f));
            frameGlow.sprite = UISprites.RoundedGlow(r, 40f);
            frameGlow.type = Image.Type.Sliced;
            frameGlow.color = Color.Lerp(t.EdgeTop, t.EdgeBottom, 0.5f).WithAlpha(t.GlowStrength * 0.6f);
            frameGlow.rectTransform.Stretch(-40f, -40f, -40f, -40f);
            frameGlow.transform.SetAsFirstSibling();

            var well = Darken(t.PanelBottom, 0.5f);
            previewWell.sprite = UISprites.RoundedFill(t.PanelRadius);
            previewWell.type = Image.Type.Sliced;
            previewWell.color = well;
            previewMask.sprite = UISprites.RoundedFill(t.PanelRadius);
            previewMask.type = Image.Type.Sliced;
            Camera.backgroundColor = well; // the render blends into its well

            brand.color = t.SubText;
            BuildTopBarButtons(t);
            RequestRender();
        }

        private void BuildTopBarButtons(MenuTheme t)
        {
            foreach (var b in new[] { viewBoth, view2D, view3D, close })
                if (b != null) UnityEngine.Object.Destroy(b.gameObject);

            float x = -Pad;
            close = UIFactory.IconButton(topBar, "Close", t, Icon.Close, 30f, 8f);
            close.GetComponent<RectTransform>().Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0f, 0f), new Vector2(30, 30));
            close.OnClick = () => CloseRequested?.Invoke();
            x = -30f - 12f;

            view3D = Chip("3D", GuiView.Preview, ref x, t);
            view2D = Chip("2D", GuiView.Flat, ref x, t);
            viewBoth = Chip("Both", GuiView.Both, ref x, t);
            RefreshViewButtons();
        }

        private MenuButton Chip(string label, GuiView view, ref float right, MenuTheme t)
        {
            float w = label.Length > 2 ? 58f : 42f;
            var chip = UIFactory.IconButton(topBar, "View " + label, t, Icon.Dot, 28f, 8f);
            UnityEngine.Object.Destroy(chip.transform.Find("Icon").gameObject);
            chip.GetComponent<RectTransform>().Pin(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(right, 0f), new Vector2(w, 28f));
            var text = UIFactory.Text(chip.transform, "Label", t, 13, TextAlignmentOptions.Center, t.Text, FontStyles.Bold);
            text.rectTransform.Stretch();
            text.text = label;
            chip.HoverScale = 1.06f;
            chip.OnClick = () => SetView(view);
            right -= w + 6f;
            return chip;
        }

        private void RefreshViewButtons()
        {
            if (viewBoth != null) viewBoth.IsOn = View == GuiView.Both;
            if (view2D != null) view2D.IsOn = View == GuiView.Flat;
            if (view3D != null) view3D.IsOn = View == GuiView.Preview;
        }

        public void SetView(GuiView view)
        {
            if (view == View) return;
            View = view;
            RefreshViewButtons();
            ViewChanged?.Invoke(view);
        }

        public void Place(Vector2 normalized)
        {
            var canvasRect = ((RectTransform)screenCanvas.transform).rect;
            var half = Window.sizeDelta * Window.localScale.x * 0.5f;
            var pos = new Vector2((normalized.x - 0.5f) * canvasRect.width, (normalized.y - 0.5f) * canvasRect.height);
            pos.x = Mathf.Clamp(pos.x, -canvasRect.width / 2 + half.x * 0.3f, canvasRect.width / 2 - half.x * 0.3f);
            pos.y = Mathf.Clamp(pos.y, -canvasRect.height / 2 + half.y * 0.3f, canvasRect.height / 2 - half.y * 0.3f);
            Window.anchoredPosition = pos;
        }

        public Vector2 NormalizedPosition
        {
            get
            {
                var canvasRect = ((RectTransform)screenCanvas.transform).rect;
                return new Vector2(Window.anchoredPosition.x / canvasRect.width + 0.5f, Window.anchoredPosition.y / canvasRect.height + 0.5f);
            }
        }

        public void Drag(Vector2 screenDelta)
        {
            Window.anchoredPosition += screenDelta / screenCanvas.scaleFactor;
            Place(NormalizedPosition);
        }

        // ================================================================== hit testing

        public bool Contains(Vector2 screenPoint) =>
            Visible && RectTransformUtility.RectangleContainsScreenPoint(Window, screenPoint, null);

        public bool InTopBar(Vector2 screenPoint) =>
            Visible && RectTransformUtility.RectangleContainsScreenPoint(topBar, screenPoint, null);

        public bool OverPreview(Vector2 screenPoint) =>
            PreviewVisible && RectTransformUtility.RectangleContainsScreenPoint(previewSlot, screenPoint, null);

        /// <summary>Screen point over the 3D preview → the matching point on the hidden panel.</summary>
        public bool PreviewToWorld(Vector2 screenPoint, RectTransform panelHost, out Vector3 world)
        {
            world = default;
            if (!OverPreview(screenPoint)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(previewImage.rectTransform, screenPoint, null, out var local)) return false;
            var r = previewImage.rectTransform.rect;
            var uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);

            var ray = Camera.ViewportPointToRay(new Vector3(uv.x, uv.y, 0f));
            var plane = new Plane(panelHost.forward, panelHost.position);
            if (!plane.Raycast(ray, out float d)) return false;
            world = ray.GetPoint(d);
            var hostLocal = panelHost.InverseTransformPoint(world);
            return panelHost.rect.Contains(new Vector2(hostLocal.x, hostLocal.y));
        }

        // ================================================================== per frame

        /// <summary>Call every frame while open. Handles the parallax tilt and renders only when needed.</summary>
        public void Tick(Vector2 mouse, bool activity)
        {
            if (!PreviewVisible || worldCanvas == null) { Camera.enabled = false; return; }

            // Lean toward the mouse while it's over the preview.
            float targetYaw = BaseYaw, targetPitch = BasePitch;
            if (OverPreview(mouse) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(previewSlot, mouse, null, out var local))
            {
                var r = previewSlot.rect;
                float nx = Mathf.Clamp((local.x - r.center.x) / (r.width * 0.5f), -1f, 1f);
                float ny = Mathf.Clamp((local.y - r.center.y) / (r.height * 0.5f), -1f, 1f);
                targetYaw += nx * Parallax;
                targetPitch -= ny * Parallax * 0.6f;
            }
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 6f);
            float newYaw = Mathf.Lerp(yaw, targetYaw, k), newPitch = Mathf.Lerp(pitch, targetPitch, k);
            bool tilting = Mathf.Abs(newYaw - yaw) > 0.01f || Mathf.Abs(newPitch - pitch) > 0.01f;
            yaw = newYaw;
            pitch = newPitch;
            ApplyTilt();

            // Full frame rate while anything moves (plus a short tail for hover fades), ~5 fps when idle.
            float now = Time.unscaledTime;
            if (activity || tilting) fastUntil = now + 0.4f;
            bool render = now < fastUntil || now - lastRender > 0.2f;
            Camera.enabled = render;
            if (render) lastRender = now;
        }

        public void RequestRender() => fastUntil = Time.unscaledTime + 0.4f;

        private void ApplyTilt()
        {
            if (worldCanvas == null) return;
            worldCanvas.SetPositionAndRotation(HiddenOrigin, Quaternion.Euler(pitch, yaw, 0f));
        }

        public void Dispose()
        {
            RestoreMainCamera();
            if (Camera != null) UnityEngine.Object.Destroy(Camera.gameObject);
            if (texture != null) { texture.Release(); UnityEngine.Object.Destroy(texture); }
            if (Window != null) UnityEngine.Object.Destroy(Window.gameObject);
        }

        // ================================================================== helpers

        private static Color Darken(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1f);

        private void ExcludeFromMainCamera()
        {
            var main = Camera.main;
            if (main == null || main == excludedFrom || Layer == 5) return;
            RestoreMainCamera();
            excludedFrom = main;
            excludedMask = main.cullingMask;
            main.cullingMask &= ~(1 << Layer);
        }

        private void RestoreMainCamera()
        {
            if (excludedFrom != null)
            {
                if ((excludedMask & (1 << Layer)) != 0) excludedFrom.cullingMask |= 1 << Layer;
                excludedFrom = null;
            }
        }

        private static int FindFreeLayer()
        {
            for (int i = 31; i >= 8; i--)
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
            return 5;
        }

        public static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
