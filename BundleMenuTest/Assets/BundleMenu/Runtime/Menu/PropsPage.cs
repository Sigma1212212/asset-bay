using System.Collections.Generic;
using UnityEngine;

namespace BundleMenu
{
    /// <summary>
    /// Things to spawn that come with the menu: a trampoline, a boost ring, a campfire, a boombox that
    /// follows your Spotify, and more. They're built out of shapes in your theme's colours, so they need
    /// no downloads and always match. Only you see them.
    /// </summary>
    public sealed class PropsPage : MenuPage
    {
        public override string Title => "Props";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var rows = new List<RowSpec>();
            var props = PropLibrary.All;
            bool allowed = ctx.Mods == null || ctx.Mods.Allowed;

            rows.Add(RowSpec.Info("note", "Built into the menu - nothing to download",
                ctx.Spawner.Count > 0 ? ctx.Spawner.Count + " out" : ""));
            rows.Add(new RowSpec
            {
                Key = "gun",
                Label = "Place them with the gun",
                Value = ctx.Props?.Chosen?.Name ?? "-",
                OnClick = () => { ctx.UsePropGun(); ctx.RefreshNow(); },
            });

            string group = null;
            foreach (var prop in props)
            {
                var def = prop;
                if (def.Group != group)
                {
                    group = def.Group;
                    rows.Add(RowSpec.Info("g:" + group, group));
                }

                bool blocked = def.NeedsMods && !allowed;
                rows.Add(new RowSpec
                {
                    Key = "p:" + def.Name,
                    Label = def.Name,
                    Value = blocked ? "private rooms only" : def.About,
                    Multiline = false,
                    Interactable = !blocked,
                    Light = ctx.Props != null && ctx.Props.Chosen == def ? StatusLight.Ok : StatusLight.None,
                    OnClick = () => { ctx.SpawnProp(def); ctx.RefreshNow(); },
                    OnAltClick = () => { ctx.ChooseProp(def); ctx.RefreshNow(); },
                    OnSecondary = () => { ctx.ChooseProp(def); ctx.RefreshNow(); },
                    SecondaryIcon = Icon.ChevronRight,
                });
            }

            rows.Add(RowSpec.Message("how", "Click to spawn one in front of you. Right-click to load it into the gun instead."));
            rows.Add(new RowSpec
            {
                Key = "check", Label = "Check every prop works",
                Value = ctx.PropTest != null ? ctx.PropTest.Summary : "",
                ShowChevron = true,
                OnClick = () => ctx.Navigate(new PropTestPage()),
            });
            if (ctx.Spawner.Count > 0)
                rows.Add(new RowSpec
                {
                    Key = "clear",
                    Label = "Clear what I spawned",
                    Value = ctx.Spawner.Count.ToString(),
                    OnClick = () => { ctx.ClearSpawned(); ctx.RefreshNow(); },
                });
            return rows;
        }
    }
}
