# Third-party notices

ResMode source code, documentation and the project-supplied logo are distributed under the MIT license in LICENSE. Third-party fonts retain their own license below. Game artwork, game names and launcher trademarks belong to their respective owners; game covers are read from the user's local installation and are not bundled in this repository or release packages.

## Geist Sans and Geist Mono

- Copyright 2024 The Geist Project Authors.
- Source: https://github.com/vercel/geist-font/releases/tag/v1.9.0
- License: SIL Open Font License 1.1, reproduced in assets/fonts/OFL.txt.
- Eight unmodified static TTF files: Regular, Medium, SemiBold and Bold for both families.
- Upstream archive SHA256: 1f2dd9ea90e68d0358a1d234a5f156807b2c3e0e295d78d87980a6dbad1fa58c.
- Font bytes and OFL text are embedded into ResMode.exe. The release also includes the OFL text separately.

## System components

Windows, .NET Framework, system DLLs and system icon fonts are used through operating system APIs and are not redistributed. Their vendors retain the applicable rights and terms. ResMode is not affiliated with Microsoft, Valve, NVIDIA, AMD or game publishers.

## Build tooling

GitHub Actions tooling and the SignPath submission action execute in CI; their code is not linked into or bundled with ResMode.exe. Each workflow pins external actions to a commit. Their upstream licenses apply to those tools.

The MSI is built with WiX Toolset 4.0.6 and its unmodified standard UI/.NET detection extensions. WiX is Copyright (c) .NET Foundation and contributors, Microsoft Reciprocal License (MS-RL). Its notice/license is included as WIX-LICENSE.txt in the MSI installation; the public release also provides the corresponding upstream source archive. Source and license: https://github.com/wixtoolset/wix/tree/v4.0.6 . ResMode.exe does not link to WiX code; the MSI's WiX-derived UI retains the upstream license.
