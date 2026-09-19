using UnityEngine;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// "Click GUI" for desktop play. The real menu is the same world-space panel VR players get, but it lives
    /// at a hidden spot far outside the map on its own layer. A dedicated camera there renders only that
    /// panel into a RenderTexture, which is shown on screen as a draggable window. Mouse positions over the
    /// window are mapped back onto the hidden panel (see <see cref="ScreenToWorld"/>) so clicks press the
    /// real buttons.
    ///
    ///   BundleMenu World (world canvas, hidden origin, menu layer) ← hidden camera renders this
    ///   Screen canvas
    ///   └ ClickGui Window (CanvasGroup, rounded Mask)  ← what you see and click
    ///     └ View (RawImage showing the RenderTexture)
    /// </summary>
    public sealed class ClickGui
    {
        /// <summary>Far from any map, well past a normal camera's far plane.</summary>
        public static readonly Vector3 HiddenOrigin = new Vector3(6000f, -6000f, 6000f);
        private const float SuperSample = 2f;

        public RectTransform Window { get; }
        public CanvasGroup Group { get; }
        public Camera Camera { get; }
        public int Layer { get; }

        private readonly RawImage view;
        private readonly Image mask;
        private readonly Canvas screenCanvas;
        private RenderTexture texture;
        private Camera excludedFrom;
        private int excludedMask;

        public ClickGui(Canvas screenCanvas)
        {
            this.screenCanvas = screenCanvas;
            Layer = FindFreeLayer();

            // Hidden camera: renders only the menu layer, into our texture, never to the screen.
            var camGo = new GameObject("[BundleMenu] Click GUI Camera");
            Object.DontDestroyOnLoad(camGo);
            Camera = camGo.AddComponent<Camera>();
            Camera.orthographic = true;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.cullingMask = 1 << Layer;
            Camera.nearClipPlane = 0.01f;
            Camera.farClipPlane = 5f;
            Camera.allowHDR = false;
            Camera.allowMSAA = false;
            Camera.depth = -100;
            Camera.enabled = false;
            camGo.transform.SetPositionAndRotation(HiddenOrigin + Vector3.back, Quaternion.identity);

            // On-screen window.
            Window = UIFactory.Rect("ClickGui Window", screenCanvas.transform);
            Window.anchorMin = Window.anchorMax = new Vector2(0.5f, 0.5f);
            Window.pivot = new Vector2(0.5f, 0.5f);
            Group = Window.gameObject.AddComponent<CanvasGroup>();
            mask = Window.gameObject.AddComponent<Image>();
            mask.raycastTarget = false;
            Window.gameObject.AddComponent<Mask>().showMaskGraphic = false; // rounded corners on the texture
            view = UIFactory.Rect("View", Window).Stretch().gameObject.AddComponent<RawImage>();
            view.raycastTarget = false;
            Window.gameObject.SetActive(false);
        }

        /// <summary>Screen-space rect of the window (for hit tests).</summary>
        public bool Contains(Vector2 screenPoint) =>
            Window.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(Window, screenPoint, null);

        /// <summary>Show the window and point the hidden camera at a panel of this size (UI units).</summary>
        public void Show(Transform worldCanvas, Vector2 panelSize, float windowScale, Vector2 normalizedPosition, MenuTheme theme)
        {
            // Frame the panel exactly.
            float unit = worldCanvas.lossyScale.x;
            Camera.orthographicSize = panelSize.y * unit * 0.5f;
            Camera.transform.SetPositionAndRotation(worldCanvas.position - worldCanvas.forward, worldCanvas.rotation);

            int w = Mathf.CeilToInt(panelSize.x * SuperSample), h = Mathf.CeilToInt(panelSize.y * SuperSample);
            if (texture == null || texture.width != w || texture.height != h)
            {
                if (texture != null) { texture.Release(); Object.Destroy(texture); }
                texture = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32) { name = "BundleMenu Click GUI", useMipMap = false };
                texture.Create();
                Camera.targetTexture = texture;
                view.texture = texture;
            }

            ApplyTheme(theme);
            Window.sizeDelta = panelSize * windowScale;
            Place(normalizedPosition);
            Window.gameObject.SetActive(true);
            Window.SetAsLastSibling();
            Camera.enabled = true;
            ExcludeFromMainCamera();
        }

        public void Hide()
        {
            Camera.enabled = false;
            Window.gameObject.SetActive(false);
            RestoreMainCamera();
        }

        public void ApplyTheme(MenuTheme theme)
        {
            // Corners outside the rounded panel show the clear colour; the mask trims them away.
            Camera.backgroundColor = theme.PanelBottom;
            mask.sprite = UISprites.RoundedFill(theme.PanelRadius);
            mask.type = Image.Type.Sliced;
        }

        /// <summary>0..1 position of the window centre within the screen canvas.</summary>
        public void Place(Vector2 normalized)
        {
            var canvasRect = ((RectTransform)screenCanvas.transform).rect;
            var half = Window.sizeDelta * 0.5f;
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

        /// <summary>Move the window by a screen-pixel delta (dragging).</summary>
        public void Drag(Vector2 screenDelta)
        {
            Window.anchoredPosition += screenDelta / screenCanvas.scaleFactor;
            Place(NormalizedPosition); // clamps so it can't be lost off-screen
        }

        /// <summary>
        /// Maps a screen point over the window to the matching world point on the hidden panel.
        /// The camera frames the panel exactly, so window UV == panel UV.
        /// </summary>
        public bool ScreenToWorld(Vector2 screenPoint, RectTransform panelHost, out Vector3 world, out Vector2 panelLocal)
        {
            world = default;
            panelLocal = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Window, screenPoint, null, out var local)) return false;
            var r = Window.rect;
            var uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
            if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return false;

            var hostRect = panelHost.rect;
            panelLocal = new Vector2(hostRect.xMin + uv.x * hostRect.width, hostRect.yMin + uv.y * hostRect.height);
            world = panelHost.TransformPoint(panelLocal);
            return true;
        }

        public void Dispose()
        {
            RestoreMainCamera();
            if (Camera != null) Object.Destroy(Camera.gameObject);
            if (texture != null) { texture.Release(); Object.Destroy(texture); }
            if (Window != null) Object.Destroy(Window.gameObject);
        }

        // Belt and braces: the panel is already kilometres away, but also keep the game's camera from
        // ever drawing the menu layer while the GUI is up.
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
                // Only undo our own bit, in case the game changed the mask meanwhile.
                if ((excludedMask & (1 << Layer)) != 0) excludedFrom.cullingMask |= 1 << Layer;
                excludedFrom = null;
            }
        }

        /// <summary>A layer with no name is unused by the game. Fall back to UI (5) if all are taken.</summary>
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
