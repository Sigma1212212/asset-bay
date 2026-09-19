using System;
using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>The built-in entrance styles. Shown in the Inspector and cycled at runtime.</summary>
    public enum EntranceStyle
    {
        Fade,
        Pop,
        SlideRight,
        Staggered,
        Cascade,
    }

    /// <summary>
    /// The bit of a menu item an entrance animation is allowed to touch.
    /// "Visual" sits between the layout-controlled row and the button, so animating it
    /// never fights the VerticalLayoutGroup and never fights the hover/press feedback.
    /// </summary>
    public sealed class AnimatedItem
    {
        public readonly RectTransform Visual;
        public readonly CanvasGroup Group;

        public AnimatedItem(RectTransform visual, CanvasGroup group)
        {
            Visual = visual;
            Group = group;
        }

        public void ResetToRest()
        {
            if (Visual == null) return;
            Visual.anchoredPosition = Vector2.zero;
            Visual.localScale = Vector3.one;
            Visual.localRotation = Quaternion.identity;
            if (Group != null) Group.alpha = 1f;
        }
    }

    /// <summary>
    /// One entrance style. To add a new style:
    ///   1. add a value to <see cref="EntranceStyle"/>
    ///   2. subclass this and implement <see cref="Apply"/>
    ///   3. register it in <see cref="EntranceLibrary"/>'s table
    /// That's it - the settings page, hotkey cycling and Inspector all pick it up.
    /// </summary>
    public abstract class EntranceAnimation
    {
        public abstract string DisplayName { get; }

        /// <summary>Seconds one item takes to go from hidden to rest.</summary>
        public virtual float ItemDuration => 0.3f;

        /// <summary>Default delay between consecutive items (0 = all together). The stagger setting can override it.</summary>
        public virtual float DefaultStagger => 0f;

        /// <summary>t runs 0 (fully hidden) to 1 (at rest). Called every frame while the item animates.</summary>
        public abstract void Apply(AnimatedItem item, float t, int index);
    }

    public sealed class FadeEntrance : EntranceAnimation
    {
        public override string DisplayName => "Fade";
        public override float ItemDuration => 0.28f;

        public override void Apply(AnimatedItem item, float t, int index)
        {
            item.Group.alpha = Ease.OutQuad(t);
        }
    }

    public sealed class PopEntrance : EntranceAnimation
    {
        public override string DisplayName => "Pop";
        public override float ItemDuration => 0.34f;

        public override void Apply(AnimatedItem item, float t, int index)
        {
            float s = Mathf.LerpUnclamped(0.55f, 1f, Ease.OutBack(t, 2.2f));
            item.Visual.localScale = new Vector3(s, s, 1f);
            item.Group.alpha = Mathf.Clamp01(t * 3f);
        }
    }

    public sealed class SlideRightEntrance : EntranceAnimation
    {
        public override string DisplayName => "Slide In";
        public override float ItemDuration => 0.32f;

        public override void Apply(AnimatedItem item, float t, int index)
        {
            float width = item.Visual.rect.width > 1f ? item.Visual.rect.width : 300f;
            item.Visual.anchoredPosition = new Vector2((1f - Ease.OutCubic(t)) * (width + 40f), 0f);
            item.Group.alpha = Mathf.Clamp01(t * 2.5f);
        }
    }

    /// <summary>Short slide + fade + settle, one item after another.</summary>
    public sealed class StaggeredEntrance : EntranceAnimation
    {
        public override string DisplayName => "Staggered";
        public override float ItemDuration => 0.36f;
        public override float DefaultStagger => 0.055f;

        public override void Apply(AnimatedItem item, float t, int index)
        {
            float e = Ease.OutCubic(t);
            item.Visual.anchoredPosition = new Vector2((1f - e) * 70f, 0f);
            float s = Mathf.Lerp(0.94f, 1f, Ease.OutBack(t, 1.4f));
            item.Visual.localScale = new Vector3(s, s, 1f);
            item.Group.alpha = Ease.OutQuad(Mathf.Clamp01(t * 1.6f));
        }
    }

    /// <summary>A bonus style showing how cheap new ones are: items drop in from above with a tilt.</summary>
    public sealed class CascadeEntrance : EntranceAnimation
    {
        public override string DisplayName => "Cascade";
        public override float ItemDuration => 0.42f;
        public override float DefaultStagger => 0.04f;

        public override void Apply(AnimatedItem item, float t, int index)
        {
            float e = Ease.OutBack(t, 1.6f);
            item.Visual.anchoredPosition = new Vector2(0f, (1f - e) * 36f);
            item.Visual.localRotation = Quaternion.Euler(0f, 0f, (1f - Ease.OutCubic(t)) * (index % 2 == 0 ? 4f : -4f));
            item.Group.alpha = Mathf.Clamp01(t * 2f);
        }
    }

    public static class EntranceLibrary
    {
        private static readonly Dictionary<EntranceStyle, EntranceAnimation> Table =
            new Dictionary<EntranceStyle, EntranceAnimation>
            {
                { EntranceStyle.Fade,       new FadeEntrance() },
                { EntranceStyle.Pop,        new PopEntrance() },
                { EntranceStyle.SlideRight, new SlideRightEntrance() },
                { EntranceStyle.Staggered,  new StaggeredEntrance() },
                { EntranceStyle.Cascade,    new CascadeEntrance() },
            };

        public static EntranceAnimation Get(EntranceStyle style) =>
            Table.TryGetValue(style, out var anim) ? anim : Table[EntranceStyle.Fade];

        /// <summary>Replace a built-in at runtime (e.g. from your own game code) without editing this file.</summary>
        public static void Override(EntranceStyle style, EntranceAnimation animation)
        {
            if (animation == null) throw new ArgumentNullException(nameof(animation));
            Table[style] = animation;
        }
    }

    public static class Ease
    {
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);
        public static float OutCubic(float t) { float u = 1f - t; return 1f - u * u * u; }
        public static float InOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            float c3 = overshoot + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + overshoot * u * u;
        }
    }
}
