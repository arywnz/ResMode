# Privacy policy

ResMode does not have accounts, analytics, advertising, crash uploads or a telemetry backend. It does not request Steam passwords or Web API keys.

## Information kept on your PC

ResMode reads installed-launcher manifests and registry entries, game executable paths and running-process information to detect games. Optional game discovery examines directories on fixed drives for game-engine/library signatures. It reads local Steam artwork and playtime. Achievement reading and display were removed. The app records configuration, exclusions, catalog entries, playtime, window placement and diagnostic logs under `%LOCALAPPDATA%\ResMode`.

Display topology, resolution and window placement are used locally for restoring the desktop. Logs can contain game names, paths and error details. Review and redact them before attaching them to a public issue. No game files, memory, save data or passwords are uploaded.

New XML saves and log records are encrypted and integrity-protected through Windows DPAPI with CurrentUser scope. Keys are managed by Windows and are not bundled in the installer. Unencrypted legacy XML remains readable and is protected on its next save; existing bounded log files are protected when the app next writes history. No plaintext fallback is used if encryption fails. The in-app history viewer decrypts in memory without making a plaintext export in Temp. XML writes use atomic replacement so interrupted writes do not leave a half-written configuration.

Each Windows user/install creates its own protected data. Copying those files to a different account or computer is not a supported settings transfer. Older ResMode versions cannot read the new encrypted files. DPAPI does not protect against another program already running with the same user's credentials. Existing unrelated backups and copies are not automatically encrypted. No promise of absolute security is made.

## Steam network request

During library discovery ResMode can automatically contact `https://store.steampowered.com/api/appdetails` to determine whether an installed Steam app is a game or another kind of software. The request includes its public app ID and is initiated by the application, not by a separate consent prompt. Valve also receives normal network information such as the source IP address. No Steam account identifier, local path or playtime is sent by ResMode in this request. Some results are cached locally.

Steam artwork and playtime are read from local files; ResMode does not download cover images or query an achievements API.

Valve's policy applies to requests to Steam: https://store.steampowered.com/privacy_agreement/.

Launching a game asks its local launcher or executable to open. That third-party software can perform its own network operations under its own privacy policy.

## Project and signing services

GitHub hosts the public source, issues and build artifacts. No SignPath production integration is active. The project is distributed unsigned. These development services are not contacted by the running ResMode app.

GitHub: https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement

SignPath: https://about.signpath.io/privacy-policy

## Removal and contact

For the MSI installation, close ResMode and uninstall through Windows Settings. The MSI removes its program files and shortcuts but retains `%LOCALAPPDATA%\ResMode` so preferences survive reinstallation. You can delete that directory manually after closing the app if you want to remove local data too. The older source/ZIP installer's `Desinstalar.cmd` deletes the legacy installation and its data directory; do not use that legacy script to remove an MSI installation. Cloned source folders and release downloads are not removed.

Privacy questions: https://github.com/arywnz/ResMode/issues. Do not post private logs or credentials in public issues.
