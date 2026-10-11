<div align="center">

<img src="src/CategoryBar/Assets/App.svg" width="96" alt="Category Bar icon">

# Category Bar for Windows 11

**Category tabs right above the taskbar: hover a tab and its apps open in a column.**

[![Release](https://img.shields.io/github/v/release/Chikanut/Windows-Category-Bar?label=release)](https://github.com/Chikanut/Windows-Category-Bar/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Chikanut/Windows-Category-Bar/total?label=downloads)](https://github.com/Chikanut/Windows-Category-Bar/releases)
![Windows 11](https://img.shields.io/badge/Windows-11-0078D4)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)

[Download](https://github.com/Chikanut/Windows-Category-Bar/releases/latest) · [Features](#features) · [Build from source](#build-from-source)

**English** · [Українська](README.uk.md)

</div>

---

Once the taskbar fills up with pinned icons, the one you need gets hard to find. Category Bar sorts your shortcuts into tabs — "Development", "Browsers", "Games" — and places them directly above the Windows taskbar. The taskbar itself keeps working as before.

> The interface is available in English, Ukrainian, German, Spanish, French, Portuguese, Simplified Chinese and Japanese. The screenshots below show the Ukrainian one.

<p align="center">
  <img src="Documentation/screenshots/onboarding-appearance.png" width="760" alt="First-run wizard with a live preview of the bar and a category menu">
</p>

## Why use it

- **One move instead of a search.** Hover a tab and its apps appear in a column. A click starts the app.
- **Set up in a minute.** The wizard imports the apps pinned to your taskbar and sorts them by type.
- **Behaves like part of Windows.** The bar shows and hides together with the taskbar and follows the system theme and accent colour.
- **Nothing gets lost.** Every change can be undone, and settings are saved with five backup copies.
- **Eight languages.** The app follows the Windows language, or you pick one in Settings → General.
- **Easy installation and updates.** .NET is included; updates arrive through GitHub Releases.

## Install

1. Download `CategoryBar-win-Setup.exe` from the [latest release](https://github.com/Chikanut/Windows-Category-Bar/releases/latest).
2. Exit any old portable copy from its tray menu, then run the installer.
3. Launch Category Bar from the Start menu. Existing settings are reused.

Requires Windows 11 x64; .NET is included. The installer is not code-signed, so SmartScreen may warn on first launch.

Updates are checked at startup and in **Settings → General → Check for updates**. Click **Update and restart** to install a new version. Categories and settings are preserved.

To uninstall, exit the app and remove Category Bar in Windows Settings → Apps. User data remains in `%LOCALAPPDATA%\WindowsCategoryBar` unless you delete it yourself.

Release process: [Documentation/updates.md](Documentation/updates.md).

## First run

A three-step wizard opens on first launch. Nothing is saved until you press "Done".

| Step | What happens |
| --- | --- |
| **1. Method** | Import the apps pinned to the taskbar, start with ready-made empty categories, or start with an empty bar. |
| **2. Categories** | A board with columns: drag apps between categories and drag categories by their header. Rename, add and delete here too. |
| **3. Appearance** | Theme, accent colour, start with Windows. |

<p align="center">
  <img src="Documentation/screenshots/onboarding-categories.png" width="760" alt="Wizard step two: the category board with apps">
</p>

The wizard can be reopened from Settings → General. Settings and Exit are in the tray icon menu and in the bar's right-click menu.

> Windows 11 does not let programs unpin taskbar icons, so after importing you unpin them by hand. How the import works (in Ukrainian): [Documentation/taskbar-import.md](Documentation/taskbar-import.md).

## Features

### Bar and menus

- A menu opens on hover after a short delay; a click opens and closes it at once, `Esc` closes it.
- Drag a tab to reorder categories, double-click to rename in place.
- Drag a shortcut inside a menu to reorder it; drag it over another tab to move it to that category.
- Drop files from Explorer (`.exe`, `.lnk`, `.url`, folders) onto a tab or into a menu.
- A desktop shortcut can be deleted after you add it: the app keeps its own copy.
- Search in large categories, a grid from 20 shortcuts up, extra tabs fold into "More N".
- Web links and Steam games (`steam://…`) are added like any other shortcut.
- Every change comes with an "Undo" button in its notification.

### App sets

A set is one icon that starts several apps together. The icon shows up to four of its apps.

- **From running windows.** Open the apps you need, arrange their windows, choose "Create set" and tick them under "Running now". The set remembers the size and position of each window — maximized and snapped ones too — and restores them on launch.
- **From the app list.** Apps picked under "All apps" start the usual way, with no window arrangement.
- A click starts only the apps that are not open yet and moves the open ones back into place. `Shift` + click starts everything again.
- In the set's right-click menu: "Save the window layout" updates the positions from the current windows, "Split up the set" turns the set back into separate shortcuts.

"Create set" is at the bottom of a category menu and in a tab's right-click menu. Limits: a minimized window has no position to remember; an app running as administrator cannot be moved; when an app has several windows, one is used.

### App status

<img align="right" src="Documentation/screenshots/menu-states.png" width="250" alt="Category menu: running apps, a broken shortcut and a run-as-administrator shortcut">

- A running app has a mark under its icon; a category with running apps has one under its name.
- Clicking a running app switches to its window.
- A shortcut whose file is missing shows a warning; clicking it offers to pick a new file or remove the shortcut.
- "Run as administrator" shortcuts show a shield, and a spinner appears while an app starts.

| Action | Result |
| --- | --- |
| Click | Start the app or switch to its window |
| `Shift` + click | Open a new window; for a set, start everything again |
| `Ctrl` + `Shift` + click | Run as administrator |
| Right-click | Properties, move, open file location |

<br clear="right">

### Appearance

- Theme: same as Windows, dark or light. "Same as Windows" switches together with the system.
- Accent: the Windows colour or your own (presets or `#RRGGBB`), optionally tinting the surfaces.
- Font, 80–150 % scale, opacity, corner radius. Everything applies immediately.

<p align="center">
  <img src="Documentation/screenshots/settings-appearance-light.png" width="760" alt="Appearance settings in the light theme">
</p>

### Languages

English, Ukrainian, German, Spanish, French, Portuguese, Simplified Chinese and Japanese. A new installation follows the Windows display language and falls back to English; change it in Settings → General → Interface language. The app restarts to apply the change.

Translations live in `src/CategoryBar.Core/Languages/<code>.json` as a table from the Ukrainian source text to the translation. Corrections are welcome.

### Placement

- Alignment as in Windows, left, centre or right.
- Primary monitor or all monitors; on top of windows or with screen space reserved.
- Miniature mode: the bar hides behind the taskbar and only an arrow stays visible. Hovering slides the bar out; it hides again when the pointer leaves.

<p align="center">
  <img src="Documentation/screenshots/settings-general.png" width="760" alt="General settings in the dark theme">
</p>

## Data and backups

Your categories and settings are stored locally. Update checks contact GitHub; your configuration is not sent.

| What | Where |
| --- | --- |
| Categories and settings | `%LOCALAPPDATA%\WindowsCategoryBar\config.json` |
| Backups | `config.json.bak1` … `config.json.bak5` next to it |
| Copies of added shortcuts | `%LOCALAPPDATA%\WindowsCategoryBar\ImportedShortcuts` |
| Error log | `errors.log` next to the configuration |

Saving is atomic; if the main file is damaged, the app restores settings from the latest working copy. To use a separate configuration, run `CategoryBar.exe --config <path>`.

## Build from source

C# / .NET 10, WPF, Win32 / DWM / Windows Shell, System.Text.Json. Velopack provides installer packaging and updates. Requires the .NET SDK 10.0.4xx.

```powershell
.\scripts\build.ps1 -SelfContained   # build, core checks, executable in artifacts\portable
.\scripts\run.ps1 -LocalConfig       # build and run with a separate configuration in .local\dev
.\scripts\run.ps1 -Fresh             # the same with a clean configuration, so the wizard shows again
```

The app icon is a vector file: `src/CategoryBar/Assets/App.svg`. After editing it, `.\scripts\make-icon.ps1` rebuilds `App.ico` (rasterising needs Edge or Chrome).

| Path | Contents |
| --- | --- |
| `src/CategoryBar.Core` | Model, persistence, undo, visibility policy, classifier |
| `src/CategoryBar/Session.cs` | Application state: changes, undo, notifications, launching |
| `src/CategoryBar/Theme.cs`, `Themes/Controls.xaml` | Theme as dynamic resources and control styles |
| `src/CategoryBar/Services` | Icons, shortcuts, app catalogue, status — all off the UI thread |
| `src/CategoryBar/Views` | Bar, menus, notifications, settings, wizard, category board |
| `src/CategoryBar/Interop` | Win32 and screen space reservation |
| `tests/CategoryBar.Tests` | Core checks, run as part of the build |

## Known limits

The main scenarios were checked on Windows 11 with a single 1920×1080 monitor at 100 % scale. Multiple monitors, an auto-hiding taskbar, full-screen games and space reservation have not been checked by hand in this version. Details (in Ukrainian): [Documentation/validation.md](Documentation/validation.md).

Found a bug or have an idea? Open an [issue](https://github.com/Chikanut/Windows-Category-Bar/issues).
