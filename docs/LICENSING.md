# Licensing and GitHub distribution

This document describes the components and package layout prepared for Volume + 1.2.4 with bundled .NET runtime 10.0.12. It is a maintainer guide; the actual license texts remain authoritative.

## License scope

| Component | Distribution in this project | License / terms |
| --- | --- | --- |
| Original Volume + application, core library, tests, build scripts, and documentation | Source repository and source ZIP; compiled in app ZIP | MIT, as recorded in `LICENSE` |
| .NET runtime 10.0.12 | Included in the default self-contained Windows x64 app ZIP | Upstream license and third-party notices in `third-party/dotnet/` |
| Windows Desktop / WPF, Windows Forms integration, and Windows SDK-related runtime components | Included in the default Windows x64 app ZIP, from runtime 10.0.12 | Upstream MIT, attribution files, and applicable Microsoft Windows redistribution terms in `third-party/wpf/`, `third-party/winforms/`, and `third-party/microsoft/` |
| Equalizer APO | User installs it separately; no source or binary included | GPL-2.0-or-later, from the [official Equalizer APO source](https://sourceforge.net/p/equalizerapo/code/ci/main/tree/DeviceAPOInfo.cpp) |
| Windows operating system and installed system fonts | Provided by the user's Windows installation; not copied into this package | Terms of the user's Windows installation |

`LICENSE` grants rights only for original project material. It does not relicense upstream license files or embedded third-party runtime code. Microsoft runtime redistribution conditions must be preserved alongside the permissive-license notices.

## Included legal files

| File | Purpose |
| --- | --- |
| `LICENSE` | MIT license for original Volume + code |
| `NOTICE.md` | Component and independent-dependency notices |
| `third-party/dotnet/LICENSE.txt` | Unchanged .NET runtime package license |
| `third-party/dotnet/THIRD-PARTY-NOTICES.txt` | Unchanged runtime attribution file |
| `third-party/wpf/LICENSE.txt` | Unchanged Windows Desktop runtime package license |
| `third-party/wpf/THIRD-PARTY-NOTICES.txt` | WPF attribution file from the exact upstream source revision |
| `third-party/winforms/LICENSE.txt` and `THIRD-PARTY-NOTICES.txt` | Windows Forms license and attribution file from the exact upstream source revision |
| `third-party/microsoft/DOTNET-LIBRARY-LICENSE.txt` and `.html` | Microsoft .NET Library terms and original HTML |
| `third-party/microsoft/WINDOWS-SDK-LICENSE.txt`, `.md`, and `.html` | Microsoft Windows SDK terms and preserved source forms |
| `third-party/microsoft/license-information-windows.md` | Official Windows license inventory and redistribution guidance |
| `third-party/license-sources.json` | Versions, original sources, and SHA-256 hashes |
| `docs/LICENSING.md` | This packaging guide |
| `docs/PRIVACY.md` | Local app data and Windows diagnostic-data disclosure |

The default build fixes the runtime version to the inventory's reviewed version and validates the preserved license files before packaging. A runtime-version change requires updating the inventory and its corresponding upstream license and notice files; a newer .NET SDK alone must not silently change the distributed runtime.

## Publishing the source

1. Upload the source repository or prepared source ZIP, including `LICENSE`, `NOTICE.md`, `docs/`, `third-party/`, and `.github/`.
2. Keep `dist/`, `artifacts/`, `bin/`, `obj/`, local NuGet / .NET caches, and user preferences out of the repository. The `.gitignore` and source packaging script exclude these files.
3. Run the core tests and build the package using `scripts/build.ps1` on Windows.

The GitHub Actions CI workflow builds and tests the source on Windows. The manual **Package release** workflow creates downloadable workflow artifacts with read-only repository permissions; it does not publish a GitHub Release.

## Publishing the Windows binary

Distribute `dist/VolumePlus-v1.2.4-win-x64.zip` with its `.sha256` checksum. The app ZIP includes the legal files listed above as well as `VolumePlus.exe`. The EXE also embeds the license texts and notices for its license viewer, but the complete ZIP is the supported distribution: it retains separately readable legal, provenance, privacy, and packaging documents. The companion source ZIP is `dist/VolumePlus-v1.2.4-source.zip`.

The included Microsoft .NET Library terms (section 3.a.ii) and Windows SDK terms (section 2.a.ii) require distributors and external end users to agree to terms that protect the code at least as much as those agreements. A distributor must review and agree to these applicable terms before distributing the bundled proprietary components. Volume + shows the included terms and requires explicit end-user acceptance before opening the main audio-settings window. Preserve that acceptance mechanism and the full terms when redistributing the default package; if you change the distribution model, provide an equivalent agreement mechanism that meets the license requirements.

Do not replace Microsoft terms with Volume +'s MIT License. A framework-dependent build relies on a separately installed .NET runtime and is a different distribution form; document that requirement if you distribute it.

The source ZIP contains Volume + source rather than Microsoft runtime source. This project does not make a corresponding-source offer for proprietary Microsoft components. Sources for open-source runtime components are identified by the preserved upstream provenance and license records.

## Equalizer APO

The current Volume + architecture writes Equalizer APO configuration text and neither copies its implementation nor loads its DLLs. Equalizer APO is obtained independently by the user. See the [GNU GPL FAQ on separate programs and aggregation](https://www.gnu.org/licenses/gpl-faq.en.html#MereAggregation) for background on licensing boundaries.

If you later redistribute Equalizer APO, preserve the applicable GPL text and copyright notices, and provide corresponding source through a mechanism permitted by its license. If you link, embed, or copy Equalizer APO implementation code, reassess the license of that combined work before publishing it. A download link is not a substitute for the obligations that apply when you actually distribute its binaries.

## Maintaining the inventory

When adding a package, copied source file, font, image, icon, installer, or runtime component, record its source and version, retain its license and copyright notices, and include the required distribution material before publishing. Keep third-party attribution text intact. For Microsoft components, consult the version-specific terms and [official Windows license information](https://github.com/dotnet/core/blob/main/license-information-windows.md).
