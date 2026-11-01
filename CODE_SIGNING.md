# Code signing policy

## Current status

ResMode is preparing an application for the SignPath Foundation open-source program. **The project has not been approved and public releases are unsigned until explicitly announced otherwise.** A locally self-signed development build is not a publicly trusted release and is never used as a substitute for SignPath signing.

After approval and activation, the required attribution will be:

> Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

The attribution above describes the intended provider after approval, not a currently active service.

## Team and approval

- Committer and reviewer: [@arywnz](https://github.com/arywnz).
- Release and signing approver: [@arywnz](https://github.com/arywnz).
- Changes from external contributors must be reviewed before merging.
- Source-control and SignPath accounts involved in releases must use multi-factor authentication. This is a release prerequisite; this repository does not assert that private account settings have been verified.
- Production signing requires explicit manual approval in SignPath. Never configure automatic approval for this project.

## Scope and provenance

Only ResMode executables built from this repository's source may be signed. Releases use a pinned GitHub Actions workflow on GitHub-hosted Windows runners. No local executables, developer certificates, game binaries or artifacts from other projects may be submitted.

The unsigned workflow artifact is uploaded to GitHub before submission, so SignPath can verify its build origin. The artifact configuration in `signpath/artifact-configuration.xml` restricts product name and version and signs only `ResMode.exe`. GitHub secrets are used only in the manual signing workflow for a tagged commit on the protected main branch, after approval of the `signing` environment.

The executable's product name is ResMode; file and product versions are 0.1.0.0. The release identifier is 0.1.0-alpha.1. Update the source metadata, artifact restrictions and release documentation together for future versions.

## Privacy

See [PRIVACY.md](PRIVACY.md), including the automatic Steam app-type lookup. ResMode does not transmit local game paths, Steam account details, playtime or achievement data to project maintainers or SignPath.

## Verification and incidents

Signed release notes identify the source tag, CI run and SHA256 checksums. Users can inspect `Get-AuthenticodeSignature .\dist\ResMode.exe` and compare published checksums. A valid signature alone is not a promise of compatibility or a guarantee against every Windows policy block.

Report security or signing concerns using [SECURITY.md](SECURITY.md). Suspend release signing when provenance, secrets or release integrity are in doubt. Never disable Windows protection to make a release pass validation.
