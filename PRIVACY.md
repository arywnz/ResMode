# Privacy policy

ResMode does not have accounts, analytics, advertising, crash uploads or a telemetry backend. It does not request Steam passwords or Web API keys.

## Information kept on your PC

ResMode reads installed-launcher manifests and registry entries, game executable paths and running-process information to detect games. Optional game discovery examines directories on fixed drives for game-engine/library signatures. It reads local Steam artwork, playtime and achievement caches. The app records configuration, exclusions, catalog entries, playtime, window placement and diagnostic logs under `%LOCALAPPDATA%\ResMode`.

Display topology, resolution and window placement are used locally for restoring the desktop. Logs can contain game names, paths and error details. Review and redact them before attaching them to a public issue. No game files, memory, save data or passwords are uploaded.

## Steam network request

During library discovery ResMode can automatically contact `https://store.steampowered.com/api/appdetails` to determine whether an installed Steam app is a game or another kind of software. The request includes its public app ID and is initiated by the application, not by a separate consent prompt. Valve also receives normal network information such as the source IP address. No Steam account identifier, local path, playtime or achievement data is sent by ResMode in this request. Some results are cached locally.

Steam artwork and achievements are read from local files; ResMode does not download cover images or query an achievements API. Achievement freshness depends on Steam updating its local cache.

Valve's policy applies to requests to Steam: https://store.steampowered.com/privacy_agreement/.

Launching a game asks its local launcher or executable to open. That third-party software can perform its own network operations under its own privacy policy.

## Project and signing services

GitHub hosts the public source, issues and build artifacts. SignPath, if the project is accepted, receives release build artifacts and their GitHub provenance for signing. These development services are not contacted by the running ResMode app. No signing approval or certificate is claimed before acceptance.

GitHub: https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement

SignPath: https://about.signpath.io/privacy-policy

## Removal and contact

`Desinstalar.cmd` restores the desktop, stops only ResMode, removes its shortcuts and deletes the ResMode installation and local data directory. Back up settings before uninstalling if needed. Cloned source folders and copies of release packages are not removed.

Privacy questions: https://github.com/arywnz/ResMode/issues. Do not post private logs or credentials in public issues.
