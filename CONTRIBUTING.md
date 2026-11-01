# Contributing

Use Windows x64 with .NET Framework 4.8 and Windows PowerShell 5.1. Build with `powershell -NoProfile -File .\Build.ps1`; this compiles only and does not install or launch ResMode. Run `powershell -NoProfile -File .\Test.ps1` on a permitted development machine. Tests do not launch games or change display modes; manual display, multi-monitor and UI checks still require an interactive desktop.

Open an issue before large changes. Keep pull requests focused, preserve the user's configuration and add meaningful regression coverage for changed behavior. New contributors' changes require maintainer review. Contributions are accepted under the repository MIT license; preserve third-party notices and do not submit proprietary assets or game covers.

Never commit user data, logs, local builds, signing certificates or secrets. A release must be built on GitHub-hosted runners from the reviewed source; never upload a developer's executable for production signing.

## Publishing a version the app can update to

Installed copies find new releases through the GitHub releases list (`src/Updater.cs`). For a release to be offered and installed:

- The tag must look like `v0.2.3` or `v0.2.3-alpha.2`, and be higher than `AppInfo.Release` in `src/Native.cs`. Prereleases count; drafts do not.
- The release must carry the MSI named exactly `ResMode-<version>-win-x64.msi` (the name Build-Msi.ps1 writes, with the tag's version). GitHub adds the SHA-256 digest to the asset itself; without it the app only opens the release page.
- Before tagging, bump the version in step: `AppInfo.Release` (`src/Native.cs`), `src/AssemblyInfo.cs`, the `Version` in `installer/ResMode.wxs` (numeric part only), the file names and version check in `Build.ps1`, `Build-Msi.ps1`, `Package.ps1` and the workflows. The MSI replaces the previous one in place (`MajorUpgrade`) and keeps `%LOCALAPPDATA%\ResMode`.
- Copies that did not come from the MSI (ZIP, build folder) are not replaced; they are sent to the release page.

## MSI and security tests

`Build-Msi.ps1` additionally requires the .NET 8 SDK and restores pinned WiX 4.0.6 tooling/extensions from NuGet. This build dependency is not needed on end-user PCs. The MSI installs per user under `%LOCALAPPDATA%\Programs\ResMode`, does not automatically start the app, and keeps runtime data in `%LOCALAPPDATA%\ResMode`.

`Test.ps1` includes DPAPI round-trip, corruption rejection, file-purpose binding, legacy-data compatibility, protected logs and a separate recovery-reader process. `tests/InstallerChecks.ps1` installs/uninstalls only on the disposable GitHub runner and refuses an existing installation or data directory. Never run destructive installer smoke tests against a user's working copy. The normal MSI path does not require SignPath.
