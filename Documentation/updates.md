# Releases and updates

The Windows x64 app uses Velopack 1.2.161 and stable public GitHub Releases. Both the NuGet dependency and local `vpk` tool are pinned to the same version. No GitHub credentials are embedded in the app.

Installation files live in `%LOCALAPPDATA%\CategoryBar`; user data remains in `%LOCALAPPDATA%\WindowsCategoryBar`. Do not change the package ID, channel (`win`), repository URL or data path between versions. Uninstall removes the installation and its own autostart entry, but retains user data.

## Publish a release

1. Update `<Version>` in `src/CategoryBar/CategoryBar.csproj`, for example `0.5.0`.
2. Add `Documentation/release-notes-0.5.0.md`.
3. Merge and push the code, then push the matching Git tag `v0.5.0`.
4. The Windows release workflow tests, publishes self-contained files, downloads the previous update package, creates full/delta packages and uploads a **draft** GitHub release.
5. Verify the installer and an update from the previous installed version, then publish the draft. Keep every generated update asset attached. Publishing the release makes it visible to installed apps.

For a local packaging check, run `./scripts/release.ps1` from a fresh checkout. For delta packages, use `-DownloadPrevious` with authenticated `gh`. Generated files are under `artifacts/releases`. Never edit or replace the assets of a published version; fix issues in a new, higher version.

## User behavior

The app checks once, 15 seconds after launch. Network errors do not interrupt startup. A new stable version produces a notification linking to Settings → General. Users can also check from the tray or settings. Checking never downloads or installs a package; **Update and restart** explicitly downloads, saves configuration, closes cleanly and restarts through Velopack. A failed download leaves the current app running and allows retry. Custom `--config` paths survive update restarts. Portable builds without Velopack metadata explain that Setup.exe is required.

## Two-release acceptance check

Install 0.4.0 first, exit any previous portable instance, and confirm the existing categories and settings. Enable autostart if desired. Only after this baseline is installed, integrate `codex/theme-system` into a new version (0.5.0), retaining the updater and the release workflow. Publish the next release and launch 0.4.0 or click **Check for updates**. Verify that 0.5.0 is offered, the restart shows the new styles, settings/shortcuts remain intact, and autostart launches the installed copy. Also test an offline check and retry.
