# Contributing

Use Windows x64 with .NET Framework 4.8 and Windows PowerShell 5.1. Build with `powershell -NoProfile -File .\Build.ps1`; this compiles only and does not install or launch ResMode. Run `powershell -NoProfile -File .\Test.ps1` on a permitted development machine. Tests do not launch games or change display modes; manual display, multi-monitor and UI checks still require an interactive desktop.

Open an issue before large changes. Keep pull requests focused, preserve the user's configuration and add meaningful regression coverage for changed behavior. New contributors' changes require maintainer review. Contributions are accepted under the repository MIT license; preserve third-party notices and do not submit proprietary assets or game covers.

Never commit user data, logs, local builds, signing certificates or secrets. A release must be built on GitHub-hosted runners from the reviewed source; never upload a developer's executable for production signing.
