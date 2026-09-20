using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// The controls for every mod. Each one has a keyboard key (PC) and a controller button (VR), so the
    /// same mod is used the same way on either. Click a row, press the key you want, and it's saved.
    /// Right-click a hold-style mod to switch between holding the key and tapping it once.
    /// </summary>
    public sealed class ControlsPage : MenuPage
    {
        public override string Title => "Controls";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var rows = new List<RowSpec>();
            var runner = ctx.Mods;
            var binds = runner.Binds;
            bool vr = MenuInput.VRActive;

            if (binds.Listening != null)
            {
                rows.Add(RowSpec.Message("listen",
                    vr || binds.ListeningForVR
                        ? "Press a controller button for " + binds.Listening.Name + "."
                        : "Press a key for " + binds.Listening.Name + ".  Backspace clears it, Escape cancels."));
                return rows;
            }

            rows.Add(RowSpec.Info("where", vr ? "Showing controller buttons" : "Showing keyboard keys",
                vr ? "VR" : "PC"));
            rows.Add(RowSpec.Message("how", "Click a mod to give it a new control. The other device keeps its own."));

            foreach (var mod in runner.Mods)
            {
                var m = mod;
                var bind = m.Bind;
                if (bind == null) continue;

                string clash = binds.ConflictFor(bind);
                string value = vr ? bind.ButtonText : bind.KeyText;
                if (m.Trigger != ModTrigger.Switch && bind.Sticky) value += "  (tap)";

                rows.Add(new RowSpec
                {
                    Key = "bind:" + m.Name,
                    Label = m.Name,
                    Value = value,
                    Light = clash != null && !vr ? StatusLight.Error : m.Enabled ? StatusLight.Ok : StatusLight.None,
                    OnClick = () => { binds.Listen(bind, vr); ctx.RefreshNow(); },
                    OnAltClick = () =>
                    {
                        if (m.Trigger == ModTrigger.Switch) { binds.Listen(bind, !vr); ctx.RefreshNow(); return; }
                        bind.Sticky = !bind.Sticky;
                        binds.Release(m.Name);
                        binds.Save();
                        ctx.Toast(bind.Sticky ? m.Name + ": tap once to keep it going" : m.Name + ": hold the key", ToastKind.Info);
                        ctx.RefreshNow();
                    },
                });

                if (clash != null && !vr)
                    rows.Add(RowSpec.Message("clash:" + m.Name, "Shares " + bind.KeyText + " with " + clash + " - one of them needs a different key."));
            }

            rows.Add(new RowSpec
            {
                Key = "gun-aim",
                Label = "Gun: aim",
                Value = vr ? "right grip" : ModBind.Pretty(ctx.Gun.AimKey),
                Interactable = !vr,
                OnClick = () => { ctx.Gun.ListenForAimKey(); ctx.RefreshNow(); },
            });
            rows.Add(RowSpec.Info("gun-fire", "Gun: fire", vr ? "right trigger" : "left mouse"));
            rows.Add(RowSpec.Info("menu-keys", "Menu", "Tab opens it, H is the PC window"));

            rows.Add(new RowSpec
            {
                Key = "reset",
                Label = "Put the controls back to normal",
                OnClick = () => { ctx.ResetModControls(); ctx.RefreshNow(); },
            });
            return rows;
        }
    }
}
