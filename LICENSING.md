# Licensing notes: hamburbur, hamburbur-injector, and this project

Findings from reading both repositories (checked 2026-09-18). This isn't legal advice. It covers
what the licence files say, what the bundled files turned out to be, and what that means for you.

## 1. hamburbur (the menu): CC BY-NC-ND 4.0

`LICENSE.md` is **Creative Commons Attribution-NonCommercial-NoDerivatives 4.0**.

- **ND (No Derivatives)** means you may not share a modified version of their code.
- **This project copies none of it.** hamburbur was only studied for architecture: how it loads its bundle, how it
  animates open/close, how it pages buttons. Every file here was written from scratch with a different structure.
  Ideas and techniques aren't covered by copyright; the code is. Keep it that way: don't paste snippets from
  hamburbur into this project.
- Don't use the name "hamburbur", its logo or its assets.

## 2. hamburbur-injector: GPL-3.0, containing two files that aren't the author's

`LICENSE.txt` is **GNU GPL v3**. You're allowed to fork it, as long as your fork is also GPL-3.0 and you publish its source.
The problem is what's bundled inside it:

| File in `Resources/` | What it really is | Problem |
|---|---|---|
| `SharpMonoInjector.dll` | **SharpMonoInjector** by warbler, MIT licence, "Copyright (c) 2017 Biney" (github.com/warbler/SharpMonoInjector, archived). | MIT allows reuse, **but its copyright and licence text must travel with every copy.** The repo ships the DLL with no notice. |
| `comicz.ttf` | **Comic Sans MS Bold Italic**, "© 2018 Microsoft Corporation. All Rights Reserved.", copied from Windows. | A proprietary Microsoft font. **It can't legally be redistributed**, and the GPL can't relicense it. |
| `cone.png`, `load.png`, `success1.png`, `icon.ico` | Origin unknown. | Presumably the author's own art under the repo's GPL, but I can't verify that. Replace them if you want to be sure. |

It also can't load this menu as-is. It downloads `hamburbur.dll` from their GitHub releases and calls
`hamburbur.Main.Inject` inside the process named `Gorilla Tag`. Using it for this project means modifying it.

## 3. Options for loading BundleMenu.dll into the game

| Option | What you'd do | Licence obligations |
|---|---|---|
| **A. BepInEx plugin** (already installed in your Gorilla Tag folder) | A ~15-line plugin whose `Awake()` calls `BundleMenu.Loader.Inject()`. No injector needed. | None beyond BepInEx's own LGPL, which you don't redistribute. **Cleanest.** |
| **B. Your own small launcher** | Write a new injector app from scratch that uses SharpMonoInjector (MIT) to load `BundleMenu.dll` with namespace `BundleMenu`, class `Loader`, method `Inject`. | Include SharpMonoInjector's MIT licence text in a `THIRD-PARTY-NOTICES.txt`. Your own code can use any licence you choose. |
| **C. Fork hamburbur-injector** ← **chosen, done in `Injector/`** | Point it at your DLL and entry point. | The fork must stay **GPL-3.0** with source published. Keep their copyright/licence. Add SharpMonoInjector's MIT notice. **Delete `comicz.ttf`** and use a free font such as *Comic Neue* (SIL Open Font License). Remove the hamburbur name, links, status checks and icons. |

In every case, Gorilla Tag's own rules still apply. Mods are tolerated in modded and private lobbies. In public
lobbies they can get your account banned, even for client-side features like this one.
