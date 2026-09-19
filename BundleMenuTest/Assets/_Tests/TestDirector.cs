using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BundleMenu;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drives the real menu in a built player: walks every page, theme, placement and animation,
/// saves screenshots, checks behaviour, writes results.txt and quits. Only active with -bundlemenutest.
/// Not part of the deliverable.
/// </summary>
public sealed class TestDirector : MonoBehaviour
{
    private BundleMenuController menu;
    private string outDir;
    private readonly List<string> results = new List<string>();
    private int failures;

    private void Start()
    {
        if (!Environment.GetCommandLineArgs().Contains("-bundlemenutest")) { enabled = false; return; }
        menu = GetComponent<BundleMenuController>();
        outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "shots"));
        Directory.CreateDirectory(outDir);
        StartCoroutine(Run());
    }

    private void Check(string name, bool ok, string detail = "")
    {
        results.Add($"{(ok ? "PASS" : "FAIL")}  {name} {detail}");
        if (!ok) failures++;
    }

    private IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(outDir, name + ".png"));
        yield return null;
        yield return null;
    }

    private static IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

    private RowView Row(string labelContains) =>
        FindObjectsOfType<RowView>().Where(r => r.gameObject.activeInHierarchy)
            .FirstOrDefault(r => r.name.IndexOf(labelContains, StringComparison.OrdinalIgnoreCase) >= 0);

    private void Click(MenuButton b)
    {
        var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(b.gameObject, data, ExecuteEvents.pointerClickHandler);
    }

    private IEnumerator Run()
    {
        yield return Wait(1.0f);
        Check("dummy catalog listed", menu.Service.Entries.Count == 15, $"count={menu.Service.Entries.Count}");

        // ---- open + mid-animation
        menu.Open();
        yield return Wait(0.11f);
        yield return Shot("01_open_mid_animation");
        yield return Wait(1.0f);
        yield return Shot("02_library");
        Check("menu open", menu.IsOpen);

        // ---- real pointer click on a row navigates
        var props = Row("Props");
        Check("props row exists", props != null);
        if (props != null) Click(props.Button);
        yield return Wait(0.9f);
        Check("click navigated to category", Row("Load all") != null);
        yield return Shot("03_category_props");

        // ---- load with dependency
        var crates = menu.Service.Get("props/crates");
        menu.LoadBundle(crates);
        menu.LoadBundle(menu.Service.Get("props/furniture"));
        yield return Wait(0.2f);
        yield return Shot("04_loading_progress");
        yield return Wait(1.8f);
        var shared = menu.Service.Get("shared/materials");
        Check("crates loaded", crates.IsLoaded);
        Check("dependency auto-loaded", shared.IsLoaded && shared.DependentCount == 2, $"dependents={shared.DependentCount}");
        yield return Shot("05_loaded");

        menu.UnloadBundle(crates);
        Check("dependency kept while furniture needs it", shared.IsLoaded && shared.DependentCount == 1);
        menu.UnloadBundle(menu.Service.Get("props/furniture"));
        Check("dependency released when unused", !shared.IsLoaded);

        // ---- bundle detail + spawn
        menu.LoadBundle(crates);
        yield return Wait(1.6f);
        menu.Navigate(new BundlePage("props/crates"));
        yield return Wait(0.5f);
        var crateRow = Row("Crate Stack");
        if (crateRow != null) Click(crateRow.Button);
        var barrelRow = Row("Barrel");
        yield return Wait(0.2f);
        if (barrelRow != null) Click(barrelRow.Button);
        yield return Wait(0.8f);
        Check("spawned 2 assets", menu.Spawner.Count == 2, $"count={menu.Spawner.Count}");
        yield return Shot("06_bundle_detail_spawned");

        // ---- failure path
        menu.Home();
        menu.Navigate(new CategoryPage("Effects"));
        var corrupt = menu.Service.Get("effects/corrupted");
        menu.LoadBundle(corrupt);
        menu.LoadBundle(menu.Service.Get("effects/sparks"));
        yield return Wait(1.8f);
        Check("corrupted bundle fails cleanly", corrupt.Status == BundleStatus.Failed && !string.IsNullOrEmpty(corrupt.Error), corrupt.Error);
        yield return Shot("07_error_state");
        menu.Navigate(new BundlePage("effects/corrupted"));
        yield return Wait(0.8f);
        yield return Shot("08_error_detail");

        // ---- settings
        menu.Home();
        menu.Navigate(new SettingsPage());
        yield return Wait(0.9f);
        yield return Shot("09_settings");

        // ---- each entrance style, caught mid-flight
        foreach (EntranceStyle style in Enum.GetValues(typeof(EntranceStyle)))
        {
            while (menu.Entrance != style) menu.CycleEntrance(+1);
            menu.ReplayEntrance();
            yield return Wait(0.12f);
            yield return Shot("10_anim_" + style);
        }
        while (menu.Entrance != EntranceStyle.Staggered) menu.CycleEntrance(+1);

        // ---- themes
        menu.Home();
        foreach (var t in new[] { ThemePreset.Solstice, ThemePreset.Circuit, ThemePreset.Velvet })
        {
            menu.SetTheme(t);
            yield return Wait(1.0f);
            yield return Shot("11_theme_" + t);
        }
        menu.SetTheme(ThemePreset.Halo);

        // ---- placements
        menu.CyclePlacement(+1); // Floating
        yield return Wait(1.0f);
        yield return Shot("12_placement_floating");

        // VR poke: a fake fingertip pushes through the first row of the floating panel.
        var tip = new GameObject("TestFingertip").transform;
        menu.pokeTips.Add(tip);
        var target = Row("Props") ?? FindObjectsOfType<RowView>().First(r => r.gameObject.activeInHierarchy);
        var rt = (RectTransform)target.Button.transform;
        Vector3 center = rt.TransformPoint(rt.rect.center);
        Vector3 normal = -rt.forward; // toward the viewer
        string before = menu.Service.Entries.Count.ToString();
        int pagesBefore = FindObjectsOfType<RowView>().Count(r => r.gameObject.activeInHierarchy && r.name.Contains("Load all"));
        for (float d = 0.08f; d >= -0.01f; d -= 0.005f)
        {
            tip.position = center + normal * d;
            yield return null;
        }
        yield return Wait(0.8f);
        Check("VR poke pressed a row", Row("Load all") != null && pagesBefore == 0);
        menu.pokeTips.Clear();
        Destroy(tip.gameObject);
        menu.Home();

        menu.CyclePlacement(+1); // Wrist (simulated)
        yield return Wait(1.0f);
        yield return Shot("13_placement_wrist");
        menu.CyclePlacement(+1); // back to screen
        yield return Wait(0.6f);

        // ---- close / reopen interrupt
        menu.Close();
        yield return Wait(0.08f);
        menu.Open(); // reverse mid-close
        yield return Wait(0.6f);
        Check("reopen mid-close works", menu.IsOpen && menu.Animator.PanelProgress > 0.99f);
        menu.Close();
        yield return Wait(0.6f);
        yield return Shot("14_closed_toggle_button");

        // ---- real local bundles from StreamingAssets
        menu.Open();
        menu.SetSource(BundleSourceMode.Local);
        yield return Wait(1.5f);
        var ids = string.Join(",", menu.Service.Entries.Select(e => e.Id));
        Check("local catalog from manifest", menu.Service.Entries.Count == 4, ids);
        Check("catalog.json rename", menu.Service.Get("effects/orbs")?.DisplayName == "Glow Orbs");
        yield return Shot("15_local_library");

        bool ok = false;
        yield return Await(menu.Service.LoadAsync("props/crates"), r => ok = r);
        var localShared = menu.Service.Get("shared/materials");
        Check("local bundle loads", ok);
        Check("local dependency loaded", localShared != null && localShared.IsLoaded);
        menu.Navigate(new BundlePage("props/crates"));
        yield return Wait(0.6f);
        var localCrate = Row("Crate");
        if (localCrate != null) Click(localCrate.Button);
        yield return Wait(0.8f);
        Check("local prefab spawned", menu.Spawner.Count >= 1);
        var spawned = GameObject.Find("Crate");
        var mat = spawned != null ? spawned.GetComponent<Renderer>().sharedMaterial : null;
        Check("spawned prefab kept dependency material", mat != null && mat.name.StartsWith("SharedBlue"), mat != null ? mat.name : "none");
        yield return Shot("16_local_spawned");

        menu.ToggleUnloadMode(); // destroy spawned on unload
        menu.Service.Unload("props/crates");
        yield return null;
        Check("unload(true) cleaned up spawned copies", GameObject.Find("Crate") == null);
        Check("local dependency released", !localShared.IsLoaded);

        File.WriteAllLines(Path.Combine(outDir, "results.txt"), results.Append($"FAILURES: {failures}"));
        Debug.Log("TESTDIRECTOR DONE failures=" + failures);
        yield return Wait(0.3f);
        Application.Quit(failures == 0 ? 0 : 1);
    }

    private static IEnumerator Await<T>(System.Threading.Tasks.Task<T> task, Action<T> done)
    {
        while (!task.IsCompleted) yield return null;
        done(task.Result);
    }
}
