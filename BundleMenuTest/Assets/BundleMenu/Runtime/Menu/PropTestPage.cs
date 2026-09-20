using System.Collections.Generic;

namespace BundleMenu
{
    /// <summary>
    /// The prop self-test: builds every prop far above the map, pokes each one to see that it does its
    /// job, and clears them away again. Run it in game to be sure everything still works there.
    /// </summary>
    public sealed class PropTestPage : MenuPage
    {
        public override string Title => "Prop check";

        public override List<RowSpec> BuildRows(BundleMenuController ctx)
        {
            var rows = new List<RowSpec>();
            var test = ctx.PropTest;

            rows.Add(new RowSpec
            {
                Key = "run",
                Label = test != null && test.Running ? "Checking..." : "Check every prop",
                Value = test != null ? test.Summary : "not run yet",
                Light = test == null ? StatusLight.Idle
                      : test.Running ? StatusLight.Busy
                      : test.Failed > 0 ? StatusLight.Error
                      : test.Passed > 0 ? StatusLight.Ok : StatusLight.Idle,
                Interactable = test == null || !test.Running,
                OnClick = () => { ctx.TestProps(); ctx.RefreshNow(); },
            });

            if (test == null)
            {
                rows.Add(RowSpec.Message("what", "Every prop is built out of sight, dropped on, pushed and carried to check it behaves, then removed. Takes about half a minute."));
                return rows;
            }

            if (test.Running) rows.Add(RowSpec.Info("now", test.Step));

            foreach (var result in test.Results)
            {
                var r = result;
                rows.Add(new RowSpec
                {
                    Key = "r:" + r.Name,
                    Label = r.Name,
                    Value = r.Note,
                    Light = r.Ok ? StatusLight.Ok : StatusLight.Error,
                    Interactable = false,
                });
            }

            if (!test.Running && test.Results.Count > 0)
                rows.Add(RowSpec.Message("note", test.Failed == 0
                    ? "Everything behaved. Spawn them from Props."
                    : "The ones in red didn't do what they should here - tell Asset Bay what the note says."));
            return rows;
        }
    }
}
