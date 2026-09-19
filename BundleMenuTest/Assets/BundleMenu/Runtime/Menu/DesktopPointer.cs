using System;
using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// All mouse interaction, done by hand instead of through the EventSystem, so it works in games whose
    /// EventSystem only listens to VR controllers (Gorilla Tag).
    ///
    /// Each frame it finds the button under the cursor, wherever it's drawn:
    ///   1. screen overlay (the ☰ toggle, and the panel in ScreenRight placement)
    ///   2. the click-GUI window: mouse → window UV → the hidden world panel
    ///   3. a world panel (Floating / Wrist): camera ray → canvas plane
    /// then drives the button's real hover / press / click handlers.
    /// </summary>
    public sealed class DesktopPointer : MonoBehaviour
    {
        /// <summary>Supplied by the controller: which surfaces exist right now.</summary>
        public Func<IEnumerable<MenuButton>> OverlayButtons;   // always-screen buttons (toggle, ScreenRight panel)
        public Func<IEnumerable<MenuButton>> WorldButtons;     // buttons on the world panel (GUI / Floating / Wrist)
        public Func<ClickGui> Gui;                             // non-null while the click GUI is showing
        public Func<RectTransform> PanelHost;
        public Func<Camera> WorldCamera;                       // for Floating / Wrist mouse rays
        public Func<bool> WorldMouseEnabled;
        public Action<int> OnScroll;                           // +1 next page, -1 previous
        public Action<Vector2> OnWindowDragged;                // new normalized window position


        private MenuButton hovered, pressed;
        private bool pressedRight, dragging;
        private Vector2 lastMouse;
        private bool prevLeft, prevRight;
        private readonly List<MenuButton> scratch = new List<MenuButton>();

        /// <summary>True while the cursor is over any menu surface (lets games ignore clicks there).</summary>
        public bool OverMenu { get; private set; }

        private void Update()
        {
            Vector2 mouse = MenuInput.MousePosition;
            bool left = MenuInput.MouseHeld(0), right = MenuInput.MouseHeld(1);
            bool leftDown = left && !prevLeft, leftUp = !left && prevLeft;
            bool rightDown = right && !prevRight, rightUp = !right && prevRight;
            prevLeft = left;
            prevRight = right;

            var hit = FindButton(mouse, out bool overGui, out bool overSurface, out Vector2 panelLocal);
            OverMenu = overSurface;

            // Dragging the click GUI window by its header.
            if (dragging)
            {
                if (left) Gui?.Invoke()?.Drag(mouse - lastMouse);
                else
                {
                    dragging = false;
                    var gui = Gui?.Invoke();
                    if (gui != null) OnWindowDragged?.Invoke(gui.NormalizedPosition);
                }
                lastMouse = mouse;
                SetHovered(null);
                return;
            }
            lastMouse = mouse;

            SetHovered(pressed != null ? (hit == pressed ? pressed : null) : hit);

            if ((leftDown || rightDown) && pressed == null)
            {
                if (hit != null)
                {
                    pressed = hit;
                    pressedRight = rightDown && !leftDown;
                    hit.SimDown(pressedRight);
                }
                else if (leftDown && overGui && Gui?.Invoke() is ClickGui g && g.InTopBar(mouse))
                {
                    dragging = true;
                }
            }

            if (pressed != null && ((pressedRight && rightUp) || (!pressedRight && leftUp)))
            {
                var p = pressed;
                pressed = null;
                p.SimUp(click: hit == p, right: pressedRight);
            }

            if (overSurface)
            {
                float scroll = MenuInput.Scroll;
                if (scroll != 0f) OnScroll?.Invoke(scroll < 0f ? +1 : -1);
            }
        }

        private void SetHovered(MenuButton next)
        {
            if (next == hovered) return;
            if (hovered != null) hovered.SimHover(false);
            hovered = next;
            if (hovered != null) hovered.SimHover(true);
        }

        private MenuButton FindButton(Vector2 mouse, out bool overGui, out bool overSurface, out Vector2 panelLocal)
        {
            overGui = false;
            overSurface = false;
            panelLocal = default;

            // 1. Screen overlay: topmost, so the ☰ toggle wins even over the window.
            var overlayHit = Smallest(OverlayButtons?.Invoke(), b => RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform, mouse, null));
            if (overlayHit != null) { overSurface = true; return overlayHit; }

            var host = PanelHost?.Invoke();
            if (host == null || !host.gameObject.activeInHierarchy) return null;

            // 2. Desktop GUI window: its 2D pane and top bar are overlay buttons (handled above);
            //    the 3D preview maps through the hidden camera onto the real panel.
            var gui = Gui?.Invoke();
            if (gui != null)
            {
                if (!gui.Contains(mouse)) return null;
                overGui = overSurface = true;
                return gui.PreviewToWorld(mouse, host, out var world) ? WorldHit(world) : null;
            }

            // 3. World panel under a camera ray.
            if (WorldMouseEnabled != null && WorldMouseEnabled())
            {
                var cam = WorldCamera?.Invoke();
                if (cam == null) return null;
                var ray = cam.ScreenPointToRay(mouse);
                var plane = new Plane(-host.forward, host.position);
                if (!plane.Raycast(ray, out float dist)) return null;
                var world = ray.GetPoint(dist);
                var local = host.InverseTransformPoint(world);
                if (!host.rect.Contains(new Vector2(local.x, local.y))) return null;
                overSurface = true;
                return WorldHit(world);
            }
            return null;
        }

        private MenuButton WorldHit(Vector3 world) =>
            Smallest(WorldButtons?.Invoke(), b =>
            {
                var rt = (RectTransform)b.transform;
                var local = rt.InverseTransformPoint(world);
                return rt.rect.Contains(new Vector2(local.x, local.y));
            });

        /// <summary>The innermost (smallest) interactable button that passes the test.</summary>
        private MenuButton Smallest(IEnumerable<MenuButton> buttons, Func<MenuButton, bool> contains)
        {
            if (buttons == null) return null;
            MenuButton best = null;
            float bestArea = float.MaxValue;
            scratch.Clear();
            scratch.AddRange(buttons);
            foreach (var b in scratch)
            {
                if (b == null || !b.isActiveAndEnabled || !b.IsInteractable()) continue;
                if (!contains(b)) continue;
                var r = ((RectTransform)b.transform).rect;
                float area = r.width * r.height;
                if (area < bestArea) { best = b; bestArea = area; }
            }
            return best;
        }

        private void OnDisable()
        {
            SetHovered(null);
            if (pressed != null) { pressed.SimUp(false, pressedRight); pressed = null; }
            dragging = false;
        }
    }
}
