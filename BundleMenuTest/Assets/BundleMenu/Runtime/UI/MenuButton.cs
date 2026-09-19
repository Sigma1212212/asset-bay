using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BundleMenu
{
    /// <summary>
    /// A themed button. Built on Selectable so mouse, keyboard/gamepad navigation and VR poke all
    /// share the same Normal / Hover / Pressed / Selected / Disabled states.
    ///
    /// Visual feedback lives on its own transform ("Body"), separate from the entrance animation's
    /// transform, so a hover can never fight an animating row.
    /// </summary>
    public sealed class MenuButton : Selectable, IPointerClickHandler, ISubmitHandler
    {
        public Action OnClick;
        public Action OnAltClick;          // right click / secondary action

        [NonSerialized] public Image Fill;
        [NonSerialized] public Image Edge;
        [NonSerialized] public MenuTheme Theme;

        /// <summary>"On" = this item represents something active (a loaded bundle, the current option).</summary>
        public bool IsOn
        {
            get => isOn;
            set { if (isOn == value) return; isOn = value; Retarget(); }
        }

        /// <summary>Scale to use when hovered. Rows use a subtle 1.02, icon buttons a punchier 1.1.</summary>
        public float HoverScale = 1.025f;
        public float PressScale = 0.95f;
        /// <summary>Scales the fill's alpha in every state (borderless row looks use ~0-0.3).</summary>
        public float FillAlpha = 1f;
        /// <summary>How far the button travels down onto its base when pressed (0 = flat button).</summary>
        public float PressDepth;
        /// <summary>The base it stands on. Shown / hidden together with the button.</summary>
        [NonSerialized] public GameObject Base;

        private bool isOn;
        private SelectionState visualState = SelectionState.Normal;
        private Color fillTarget, edgeTarget;
        private float scaleTarget = 1f, scale = 1f, scaleVelocity;
        private float slideTarget, slide, press;
        private Vector2 applied; // offset currently added to our position (slide + press)
        private float lastClickTime = -1f;

        public void Init(MenuTheme theme, Image fill, Image edge)
        {
            Theme = theme;
            Fill = fill;
            Edge = edge;
            targetGraphic = fill;
            Retarget(instant: true);
        }

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None; // we animate ourselves in DoStateTransition
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            scale = scaleTarget = 1f;
            scaleVelocity = 0f;
            transform.localScale = Vector3.one;
            if (Base != null) Base.SetActive(true);
            Retarget(instant: true);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Base != null) Base.SetActive(false);
            // Drop any hover / press offset so the layout position is intact next time.
            if (applied != Vector2.zero && transform is RectTransform rt) rt.anchoredPosition -= applied;
            applied = Vector2.zero;
            slide = press = 0f;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;
            if (eventData.button == PointerEventData.InputButton.Right) Fire(OnAltClick ?? OnClick);
            else if (eventData.button == PointerEventData.InputButton.Left) Fire(OnClick);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable()) return;
            DoStateTransition(SelectionState.Pressed, false);
            Fire(OnClick);
            Invoke(nameof(ReturnFromSubmit), 0.1f);
        }

        /// <summary>For non-pointer interactors (VR finger poke): drive hover / press / click.</summary>
        public void SetPokeHover(bool hovering) => SimHover(hovering);

        // ---- simulated pointer (click GUI, desktop mouse, VR poke). Goes through the same Selectable
        //      handlers a real EventSystem pointer would, so states and visuals are identical.

        private static PointerEventData SimData(bool right) =>
            new PointerEventData(EventSystem.current)
            {
                button = right ? PointerEventData.InputButton.Right : PointerEventData.InputButton.Left,
            };

        public void SimHover(bool hovering)
        {
            if (hovering) OnPointerEnter(SimData(false)); else OnPointerExit(SimData(false));
        }

        public void SimDown(bool right = false)
        {
            if (!IsActive() || !IsInteractable()) return;
            OnPointerDown(SimData(right));
        }

        /// <summary>Release; fires the click when the pointer is still over this button.</summary>
        public void SimUp(bool click, bool right = false)
        {
            var data = SimData(right);
            OnPointerUp(data);
            if (click) OnPointerClick(data);
        }

        public void PokePress()
        {
            if (!IsActive() || !IsInteractable()) return;
            DoStateTransition(SelectionState.Pressed, false);
            Fire(OnClick);
            Invoke(nameof(ReturnFromSubmit), 0.12f);
        }

        private void ReturnFromSubmit() => DoStateTransition(currentSelectionState, false);

        private void Fire(Action action)
        {
            // Debounce: VR pokes and double events can otherwise trigger twice.
            if (Time.unscaledTime - lastClickTime < 0.12f) return;
            lastClickTime = Time.unscaledTime;
            // A tiny extra dip on click so the press reads even for very fast taps.
            scaleVelocity -= 2.5f;
            try { action?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            visualState = state;
            Retarget(instant);
        }

        private void Retarget(bool instant = false)
        {
            if (Theme == null) return;

            Color fill = Theme.ButtonFill, edge = Theme.ButtonEdge;
            scaleTarget = 1f;

            switch (visualState)
            {
                case SelectionState.Highlighted:
                    fill = Theme.ButtonFillHover; edge = Theme.ButtonEdgeHover;
                    // The theme decides how hovering feels: grow, slide sideways, or only light up.
                    bool wide = ((RectTransform)transform).rect.width > 150f;
                    scaleTarget = Theme.Hover == HoverStyle.Grow || !wide ? HoverScale : 1f;
                    slideTarget = Theme.Hover == HoverStyle.Slide && wide ? 10f : 0f;
                    break;
                case SelectionState.Pressed:
                    fill = Theme.ButtonFillPressed; edge = Theme.ButtonEdgeHover; scaleTarget = PressScale; break;
                case SelectionState.Selected:
                    // keyboard / gamepad focus: an edge highlight, no scale (so it doesn't look "stuck" hovered)
                    edge = Color.Lerp(Theme.ButtonEdge, Theme.ButtonEdgeHover, 0.7f); break;
                case SelectionState.Disabled:
                    fill.a *= 0.45f; edge.a *= 0.3f; break;
            }

            if (isOn)
            {
                fill = Color.Lerp(fill, Theme.Accent, 0.16f);
                edge = Color.Lerp(edge, Theme.Accent, 0.55f);
                edge.a = Mathf.Max(edge.a, 0.55f);
            }

            if (visualState != SelectionState.Highlighted) slideTarget = 0f;
            fill.a *= FillAlpha;
            fillTarget = fill;
            edgeTarget = edge;

            if (instant)
            {
                if (Fill != null) Fill.color = fillTarget;
                if (Edge != null) Edge.color = edgeTarget;
            }
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float k = 1f - Mathf.Exp(-dt * 18f);
            if (Fill != null) Fill.color = Color.Lerp(Fill.color, fillTarget, k);
            if (Edge != null) Edge.color = Color.Lerp(Edge.color, edgeTarget, k);

            // Under-damped spring: presses squish, releases bounce back slightly past rest.
            const float stiffness = 520f, damping = 24f;
            scaleVelocity += ((scaleTarget - scale) * stiffness - scaleVelocity * damping) * dt;
            scale += scaleVelocity * dt;
            transform.localScale = new Vector3(scale, scale, 1f);

            // Offsets on top of wherever the layout put us: sideways slide on hover, and pressing down
            // onto the base (fast on the way down, springier on the way up).
            slide = Mathf.Lerp(slide, slideTarget, k);
            float pressTarget = visualState == SelectionState.Pressed ? PressDepth : 0f;
            press = Mathf.Lerp(press, pressTarget, 1f - Mathf.Exp(-dt * (pressTarget > press ? 40f : 16f)));
            var want = new Vector2(slide, -press);
            if ((want - applied).sqrMagnitude > 0.0001f)
            {
                ((RectTransform)transform).anchoredPosition += want - applied;
                applied = want;
            }
        }
    }
}
