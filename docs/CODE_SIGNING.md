# Code signing policy

ResMode is distributed as an unsigned open-source application. A certificate purchase is not required by the application, and the app does not require an online license or signing service to run. Windows can still warn about or block unsigned executables and MSI packages according to the machine's security policy. Packaging as MSI does not bypass that policy.

SignPath Foundation signing is not active. The maintainer reported that the application was not accepted because the project needs an established public user base. No Foundation signature or endorsement is claimed. The optional signing workflow remains disabled unless explicitly configured in the future; it is not part of the normal unsigned build and release process.

Releases are built from public source on GitHub-hosted Windows runners. Their download pages identify the source tag, CI run and SHA256 checksums. Private signing certificates and developer-built test executables are excluded from source control and public packages. A locally self-signed certificate is not used as a publicly trusted signature.

[@arywnz](https://github.com/arywnz) maintains the code, reviews external contributions and approves releases. The main branch requires a pull request and passing CI; release tags are protected from changes. GitHub MFA was confirmed by the maintainer. These controls improve release traceability; they are not a guarantee that software has no defects.

If production signing is enabled in the future, it must use reviewed source, the protected signing environment and explicit manual approval. Never disable Windows security or misrepresent signature status to make a release pass validation.

See [PRIVACY.md](PRIVACY.md) for data handling and [SECURITY.md](SECURITY.md) for reporting concerns.
