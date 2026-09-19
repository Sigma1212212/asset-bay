using System;
using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    public enum StaggerMode { Auto, Off, Tight, Loose }

    /// <summary>
    /// Drives two independent things with one clock (unscaled time, so it works while paused):
    ///  - the item timeline: every visible row runs the chosen <see cref="EntranceAnimation"/>, offset by the stagger
    ///  - the panel transition: a 0..1 value that moves toward open/closed and can reverse mid-way
    /// It knows nothing about bundles, themes or placement.
    /// </summary>
    public sealed class MenuAnimator : MonoBehaviour
    {
        [Tooltip("Global speed multiplier for every menu animation.")]
        public float Speed = 1f;

        [Tooltip("Seconds for the whole panel to open or close.")]
        public float PanelDuration = 0.24f;

        // ---- panel
        public float PanelProgress { get; private set; }   // 0 closed .. 1 open
        public bool PanelTargetOpen { get; private set; }
        public bool IsPanelAnimating => !Mathf.Approximately(PanelProgress, PanelTargetOpen ? 1f : 0f);

        /// <summary>Called every frame the panel moves: (linear progress, opening?).</summary>
        public event Action<float, bool> PanelUpdated;
        public event Action PanelClosed;
        public event Action PanelOpened;

        // ---- items
        private readonly List<AnimatedItem> items = new List<AnimatedItem>();
        private EntranceAnimation entranceAnim;
        private float timelineStart;
        private float stagger;
        private bool itemsPlaying;

        public bool IsPlayingItems => itemsPlaying;

        public static float StaggerFor(StaggerMode mode, EntranceAnimation anim)
        {
            switch (mode)
            {
                case StaggerMode.Off:   return 0f;
                case StaggerMode.Tight: return 0.035f;
                case StaggerMode.Loose: return 0.085f;
                default:                return anim.DefaultStagger;
            }
        }

        /// <summary>Start the entrance for these items. Items are hidden immediately so nothing flashes.</summary>
        public void PlayEntrance(IReadOnlyList<AnimatedItem> targets, EntranceAnimation anim, StaggerMode staggerMode, float delay = 0f)
        {
            StopEntrance();
            if (anim == null || targets == null || targets.Count == 0) return;

            items.AddRange(targets);
            entranceAnim = anim;
            stagger = StaggerFor(staggerMode, anim);
            timelineStart = Time.unscaledTime + Mathf.Max(0f, delay) / SafeSpeed;
            itemsPlaying = true;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i]?.Visual == null) continue;
                items[i].ResetToRest();
                entranceAnim.Apply(items[i], 0f, i);
            }
        }

        /// <summary>Snap every item to rest (used when a page is swapped mid-animation).</summary>
        public void StopEntrance()
        {
            foreach (var item in items) item?.ResetToRest();
            items.Clear();
            itemsPlaying = false;
        }

        public void SetPanelOpen(bool open, bool instant = false)
        {
            bool changed = PanelTargetOpen != open;
            PanelTargetOpen = open;
            if (instant)
            {
                PanelProgress = open ? 1f : 0f;
                PanelUpdated?.Invoke(PanelProgress, open);
                if (open) PanelOpened?.Invoke(); else PanelClosed?.Invoke();
            }
            else if (changed)
            {
                // Fire once so listeners can react to the direction change this frame.
                PanelUpdated?.Invoke(PanelProgress, open);
            }
        }

        /// <summary>Restart the open transition from zero (used when the panel jumps to a new placement).</summary>
        public void ReplayOpen()
        {
            PanelTargetOpen = true;
            PanelProgress = 0f;
            PanelUpdated?.Invoke(0f, true);
        }

        private float SafeSpeed => Mathf.Max(0.05f, Speed);

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f); // don't skip whole animations after a hitch
            TickPanel(dt);
            TickItems();
        }

        private void TickPanel(float dt)
        {
            float target = PanelTargetOpen ? 1f : 0f;
            if (Mathf.Approximately(PanelProgress, target)) return;

            float step = dt * SafeSpeed / Mathf.Max(0.01f, PanelDuration);
            PanelProgress = Mathf.MoveTowards(PanelProgress, target, step);
            PanelUpdated?.Invoke(PanelProgress, PanelTargetOpen);

            if (Mathf.Approximately(PanelProgress, target))
            {
                if (PanelTargetOpen) PanelOpened?.Invoke(); else PanelClosed?.Invoke();
            }
        }

        private void TickItems()
        {
            if (!itemsPlaying) return;

            float elapsed = (Time.unscaledTime - timelineStart) * SafeSpeed;
            float duration = Mathf.Max(0.01f, entranceAnim.ItemDuration);
            bool anyRunning = false;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item?.Visual == null) continue;

                float t = (elapsed - i * stagger) / duration;
                if (t >= 1f)
                {
                    item.ResetToRest();
                    continue;
                }
                anyRunning = true;
                entranceAnim.Apply(item, Mathf.Clamp01(t), i);
            }

            if (!anyRunning)
            {
                items.Clear();
                itemsPlaying = false;
            }
        }
    }
}
