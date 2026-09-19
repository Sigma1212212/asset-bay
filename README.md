# Bundle Menu ("Asset Bay")

An in-game menu for listing, loading, unloading and spawning Unity **AssetBundles**. Features:
- animated entrances you can switch at runtime
- four original themes
- three placements: right side of the screen, floating in front of you, or on your wrist
- a dummy mode, so you can test with no real bundles

**Play it in Gorilla Tag:** download the launcher from [asset-bay-launcher releases](https://github.com/Sigma1212212/asset-bay-launcher/releases/latest). Start the game, run `AssetBayLauncher.exe`, click **Inject latest**, then press Tab in game.

| folder | what |
|---|---|
| `BundleMenuTest/Assets/BundleMenu/` | **The deliverable.** Copy this folder into any Unity 2021.3+ project. |
| `BundleMenuTest/` | Unity 2022.3 test project: sample bundles, test scene, automated test director. |
| `Injectable/` | Builds the same runtime code into `BundleMenu.dll` against Gorilla Tag's Unity 6000.2 assemblies. |
| `Injector/` | Local copy of the [launcher](https://github.com/Sigma1212212/asset-bay-launcher) (its own MIT repo). Pulls the newest menu from this repo's releases, with a built-in copy as the offline fallback. |
| `publish.ps1` | Builds `BundleMenu.dll` and publishes it as a GitHub release (with SHA-256). |
| `LICENSE` | MIT. |
| `LICENSING.md` | Licences of this repo, the launcher, and the third-party files the test project includes. |

---

## Architecture

There are three layers, and dependencies point one way only. The UI never touches `AssetBundle`, and the loading code never touches the UI.

```
 Data + loading                     Menu logic                      Presentation
 ───────────────                    ──────────                      ────────────
 IBundleSource ─┬ LocalBundleSource  BundleMenuController            MenuView     (panel GameObjects)
                └ DummyBundleSource    ├ pages → RowSpec (plain data) ├ RowView    (one row)
 BundleService  (state, ref-counts,    ├ placement / input / prefs    ├ MenuButton (hover/press/selected)
                 deps, errors)         └ AssetSpawner                 └ UISprites  (procedural art)
                                                                      MenuAnimator + EntranceAnimation
```

| File | Job |
|---|---|
| `Loading/BundleContracts.cs` | `IBundleSource`, `ILoadedBundle`, `BundleDescriptor`, `BundleEntry`. |
| `Loading/BundleService.cs` | Load and unload bundles. Loads dependencies first and ref-counts them. Merges duplicate requests into one load. Any failure becomes a `Failed` state with a message, never a thrown exception. |
| `Loading/LocalBundleSource.cs` | Loads from `StreamingAssets/Bundles`. Uses Unity's build manifest (for dependencies), falls back to a file scan, and reads an optional `catalog.json` for display names. |
| `Loading/DummyBundleSource.cs` | Test mode: 15 fake bundles with a realistic load delay and a shared dependency. One bundle always fails, and you can set a random failure rate. |
| `Animation/EntranceAnimations.cs` | One tiny class per style, plus `EntranceLibrary` and the `Ease` curves. |
| `Animation/MenuAnimator.cs` | Runs the item timeline (with stagger) and the panel open/close. Uses unscaled time and can reverse mid-way. |
| `UI/MenuTheme.cs` | Theme `ScriptableObject` and the four presets. |
| `UI/UISprites.cs` | Rounded panels, borders, glow and icons, all drawn from code, so there are no textures to import. |
| `UI/MenuButton.cs` | A `Selectable`, so mouse, keyboard/gamepad and VR poke share the same hover/press/selected states. Press has a spring bounce. |
| `UI/RowView.cs`, `UI/MenuView.cs` | Build and render the panel. |
| `Menu/MenuPages.cs` | Library, Category, Bundle detail and Settings pages. Each one only returns rows. |
| `Menu/BundleMenuController.cs` | The one component you add. Wires everything together. |
| `Menu/Placement.cs` | `MenuPlacement`, `IRigProvider`, and `PokeInteractor` (VR fingertip presses without physics). |
| `Menu/MenuInput.cs` | Works with the new Input System, the legacy Input Manager, or both. Also reads a VR controller button. |
| `Integration/Loader.cs`, `GorillaTagRig.cs` | Entry point for loading from outside a Unity project, and a reflection-based rig adapter for Gorilla Tag. |

### Key decisions

- **AssetBundles, not Addressables.** You asked to list bundles and load or unload them one at a time. That is exactly what the AssetBundle API does. Addressables hides bundles behind addresses. `IBundleSource` means an Addressables or CDN source can be added later without touching the UI.
- **uGUI + TextMeshPro, not UI Toolkit.** Each row gets its own `RectTransform` and `CanvasGroup`, so per-item animation is simple. It works from 2021.3 through Unity 6, and it also works inside an injected DLL, where UI Toolkit's asset files aren't available.
- **Each row is three nested objects, one job each:**
  - `Row`: owned by the layout group.
  - `Visual`: owned by the entrance animation.
  - `Body`: owned by hover/press feedback.

  The reference menu animated the same transforms from several places at once. This split means those systems can never fight.
- **Pages return data (`RowSpec`), not GameObjects.** When progress ticks, the controller re-applies the data to the existing rows. It only rebuilds and re-animates when the page actually changes.
- **Everything is built from code.** There's no prefab to wire up and nothing to break when you rename something.

---

## Setup (any Unity 2021.3+ project)

1. Copy `BundleMenuTest/Assets/BundleMenu/` into your project's `Assets/`.
2. Make sure **TextMeshPro** and **uGUI** are installed. Unity 6 includes both in `com.unity.ugui`. Import TMP's essentials once via **Window › TextMeshPro › Import TMP Essential Resources**.
3. **Tools › Bundle Menu › Add Menu To Scene**. You can also add `BundleMenuController` to any GameObject yourself.
4. Press Play, then **Tab**.

It creates an EventSystem if the scene doesn't have one, choosing the right input module for your input settings.

**Real bundles:** set asset bundle names on your assets in the Inspector (for example `props/crates`), then run **Tools › Bundle Menu › Build Bundles For Current Platform**. The output goes to `StreamingAssets/Bundles`. Set the source to *Local* (Inspector, or Settings page › Bundle source). The first part of the name becomes the category: `props/wooden_crates` shows as "Wooden Crates" under "Props".

**Want something to test with right away?** **Tools › Bundle Menu › Create Sample Bundles** makes five prefabs in four bundles, including a shared-material dependency.

### Hierarchy it builds at runtime

```
Bundle Menu (BundleMenuController, MenuAnimator, AssetSpawner, PokeInteractor)
└ BundleMenu Screen (Canvas: Screen Space Overlay, sort 1000)
  ├ BundleMenu Panel (MenuView)          ← moves to "BundleMenu World" in Floating/Wrist
  │ └ Panel (CanvasGroup)
  │   ├ Glow · Background · Rim
  │   ├ Back · Settings · Close · Brand · Title
  │   ├ AccentLine
  │   ├ Rows (VerticalLayoutGroup) → Row → Visual → Body (MenuButton) → Light, Label, Value, …
  │   ├ Footer → Prev · Page · Next
  │   └ Status
  └ Toggle (the corner button)
BundleMenu World (Canvas: World Space)
```

---

## Testing in Play Mode

| Input | Action |
|---|---|
| `Tab`, or the corner button, or VR **Y** | Open / close |
| `F2` | Cycle entrance animation |
| `F3` | Cycle placement: Screen → Floating → Wrist |
| `F4` | Cycle theme |
| `Esc` / `Backspace` | Back (Esc closes the menu when you're on the first page) |
| `PageUp` / `PageDown` | Previous / next page |
| `↑` / `↓` then `Enter` | Keyboard / gamepad navigation |
| Right-click a settings row | Cycle that setting backwards |
| Right-click a bundle row | Open its details |

**Changing the animation style:**
- **Settings page** (gear icon) › *Entrance*: Fade, Pop, Slide In, Staggered, Cascade. It replays immediately so you can see it.
- *Stagger*: Auto / Off / Tight / Loose. This applies to any style, so you can have a staggered Pop or a staggered Fade.
- *Speed*: 0.5×–2×.
- *Replay animation* re-runs the entrance on the current page.
- The Inspector fields `entrance`, `stagger` and `animationSpeed` set the defaults.
- Changes are remembered between sessions (turn off `rememberSettings` to stop that).

**Test mode without real bundles:** set the source to *Dummy* (the default). *Dummy fail rate* (0/25/50/100%) forces errors. The **Effects › Corrupted Pack** bundle always fails, so you can see the red light, the error message, and Retry.

**What a row shows:**
- Status light: grey = not loaded, pulsing amber = loading (with a progress fill across the row), green = loaded, red = failed.
- Tap a row to load or unload it.
- Tap **›** to open the bundle's details. From there, tap an asset to spawn it in front of the camera. Audio assets play instead.

**On unload** (Settings): *keep spawned* uses `Unload(false)`, so copies already spawned keep working. *destroy spawned* uses `Unload(true)` and removes the copies too, instead of leaving them pink with missing materials.

### Automated verification

`Assets/_Tests/` (not part of the deliverable) builds a Windows player that drives the real menu through every page, theme, placement and animation, saves screenshots to `BundleMenuTest/shots/`, and checks behaviour.
- Last full run: **20/20 passed**. That covers dummy and real local bundles, dependency ref-counting, error handling, real pointer clicks, a simulated VR fingertip poke, reversing a close mid-animation, and `catalog.json` renames.
- A few small visual fixes made after that run have been compiled but not re-run.

---

## Extending

**New entrance animation:**
1. Add a value to `EntranceStyle`.
2. Write a class like the one below.
3. Add one line to `EntranceLibrary`.

```csharp
public sealed class SpinEntrance : EntranceAnimation
{
    public override string DisplayName => "Spin";
    public override float DefaultStagger => 0.04f;
    public override void Apply(AnimatedItem item, float t, int index)
    {
        item.Visual.localRotation = Quaternion.Euler(0, (1 - Ease.OutCubic(t)) * 90f, 0);
        item.Group.alpha = t;
    }
}
```

The Settings page, the F2 key and the Inspector pick it up automatically. To swap one of the built-in styles from your own code instead, use `EntranceLibrary.Override(style, anim)`.

**New theme:** use **Assets › Create › Bundle Menu › Theme**, set Theme = Custom, and assign the asset. You can also add a preset in `ThemePresets`.

**New page:** subclass `MenuPage`, return `RowSpec`s, and call `ctx.Navigate(new YourPage())`.

**New bundle source (CDN, Addressables…):** implement `IBundleSource`. Throw exceptions on failure; the service turns them into the Failed state.

**VR rig:**
- Assign `wristAnchor` and add fingertip transforms to `pokeTips`.
- Or implement `IRigProvider` and set `controller.Rig`.

---

## Gorilla Tag build (`Injectable/`)

```bash
dotnet build -c Release
```

This compiles the same runtime code into `Injectable/bin/Release/BundleMenu.dll`, against the game's own assemblies (default path is the Steam install; override with `-p:GameDir="..."`).
- Entry point: `BundleMenu.Loader.Inject()` (and `Loader.Eject()` to remove it).
- Inside Gorilla Tag it finds the player's camera and hands by reflection. The menu goes on the left wrist and the right fingertip presses buttons.
- If a game update renames something, the menu falls back to the camera instead of crashing.

Status:
- **Compiles** against Gorilla Tag's Unity 6000.2.9f1 assemblies.
- **Has not been tested loading inside the game.**
- Load it with the launcher in `Injector/`. It handles downloading, updating, testing local builds and ejecting; see `Injector/README.md`.

Notes for the game:
- Bundles you load in the game must be built with **Unity 6000.2.9f1**, the game's exact version. The shaders your bundles use must also exist in the game.
- Everything the menu spawns is **local to your client**. Nothing is sent over the network.
- Using mods in public lobbies can break the game's rules; keep them to private or modded lobbies.
