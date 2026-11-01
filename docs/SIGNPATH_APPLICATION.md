# SignPath Foundation application preparation

This is a prepared application, not a claim of acceptance. Apply at https://signpath.org/apply after the public release and checklist below are verified. Terms: https://signpath.org/terms.

## Suggested application answers

**Project name:** ResMode

**Source repository / project home:** https://github.com/arywnz/ResMode

**Download / releases:** https://github.com/arywnz/ResMode/releases

**License:** MIT for project code, documentation and logo; SIL Open Font License 1.1 for bundled unmodified Geist fonts. No proprietary game artwork or game binaries are distributed. Windows and .NET Framework are system dependencies, not bundled components.

**Maintainer / signing approver:** arywnz (https://github.com/arywnz). Enter the maintainer's preferred verified contact email in the application; it is deliberately not published in this repository.

**Description:** ResMode is a Windows x64 desktop utility that automatically selects a driver-supported display resolution when a monitored game starts and restores the desktop resolution when the game session ends. It provides a local game library, persistent exclusions, launcher discovery, per-monitor display selection, local Steam achievement information and desktop/window restoration. It runs without administrator rights, game injection, a kernel driver or a Windows service.

**Reason for requesting signing:** We would like to provide verifiable, publicly trusted Windows releases built from our public source code. Unsigned local development builds can be rejected by Smart App Control. We understand that program acceptance and signature availability are subject to SignPath Foundation review and that a signature is not a blanket guarantee of Windows policy acceptance.

**Build / provenance:** GitHub Actions on GitHub-hosted Windows runners, using the system .NET Framework compiler and source-controlled build, test and package scripts. Workflows pin actions to commit IDs. Unsigned input is uploaded as a workflow artifact before SignPath submission. We do not submit developer-built executables or private test certificates. Compilation embeds the original Geist fonts and OFL license. Release packages retain the prebuilt executable rather than recompiling during installation.

**Artifacts:** A ZIP workflow artifact containing ResMode.exe; product name ResMode, product/file version 0.1.0.0. See signpath/artifact-configuration.xml. Only this project's executable is requested for signing.

**Roles / approvals:** arywnz maintains the source, reviews external contributions and approves releases/signing requests. Production signing must require manual SignPath approval. MFA for GitHub and SignPath is a prerequisite to be confirmed by the maintainer before submission. We do not claim an independent second reviewer exists.

**Privacy:** https://github.com/arywnz/ResMode/blob/main/PRIVACY.md. The runtime has no project telemetry backend. It may automatically query Steam app details with public app IDs during library discovery; this behavior and Valve's privacy policy are disclosed. Game paths, Steam account details, playtime, achievement data and diagnostic logs are not uploaded to the project or SignPath.

**Code signing policy:** https://github.com/arywnz/ResMode/blob/main/CODE_SIGNING.md

## Prerequisites before submission

- [x] Public repository is accessible at the URL above and the MIT/OFL license files are visible.
- [x] The GitHub-hosted build and regression workflow is green for the release commit: https://github.com/arywnz/ResMode/actions/runs/41258818787.
- [x] Unsigned prerelease with a downloadable binary ZIP and checksums: https://github.com/arywnz/ResMode/releases/tag/v0.1.0-alpha.1.
- [x] Maintainer confirmed GitHub MFA is enabled on 2026-10-31.
- [ ] Maintainer confirms MFA is enabled on SignPath after account setup.
- [ ] Maintainer checks all Foundation terms and confirms project ownership and contact details.
- [ ] Application submitted by maintainer. Do not claim approval while awaiting the decision.

## After SignPath acceptance

1. Install/authorize the official SignPath GitHub App only for this repository. That permission grant must be reviewed by the maintainer.
2. Create the SignPath project, trusted GitHub build integration and artifact configuration from signpath/artifact-configuration.xml. Use the actual slugs assigned by SignPath.
3. Require manual production approval in SignPath. Set up the GitHub `signing` environment with required maintainer review and main-only deployment protection before enabling the workflow.
4. Protect main against force pushes and deletion; require review of outside contributions and successful CI. Review workflow changes as release-critical code. On a single-maintainer project, do not claim independent self-review or a second reviewer.
5. Add `SIGNPATH_API_TOKEN` as an environment secret, never in source. Add environment variables `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`. Set repository variable `SIGNPATH_ENABLED=true` only after these requirements are met.
6. Update CODE_SIGNING.md and README status/attribution to reflect actual approval.
7. Tag the reviewed source, start `Sign reviewed release` from main, supply that tag, review the request in SignPath and approve manually. Validate the returned Authenticode signature and package before publishing a signed release with checksums and CI provenance.

The workflow is prepared but disabled by default. No SignPath account, token, approval or certificate is fabricated. Do not turn off Windows application-control protections to validate a release.
