# Installer NOTES

Design notes for `pwiz-sharp/installer/`. Aimed at future maintainers who need to
understand *why* the installer looks the way it does - the surface-level "how"
lives in `Setup.iss` + `build.ps1`'s comments.

## Goals (in priority order)

1. **One downloadable EXE** that a non-technical user can double-click and end up
   with `MSConvertGUI-sharp` on their Start Menu. No prerequisite hunt, no
   "next-next-next" prompts where they have to read terms.
2. **Per-user OR per-machine** install at the user's choice, surfaced at install
   time with no separate downloads.
3. **Standard uninstall path** (Programs and Features) that cleanly removes
   *everything* this installer added.
4. **CI-buildable** from `dotnet`/`pwsh`-only tooling - no SDKs that require a
   commercial license or a special build environment.
5. **End-to-end testable** - a real install on a TC agent, smoke a vendor file
   through `msconvert-sharp`, real uninstall, assert clean state.

The legacy cpp pwiz installer has historically used WiX (for the MSI) plus Burn
(for the bundle + prereqs). pwiz-sharp started there and moved off - see below.

## Why Inno Setup, not WiX

The trajectory was **WiX 5 → WiX 4.0.5 → Inno**. Three pain points stacked up.

### 1. WiX 5 introduced an "Open Source Maintenance Fee" (OSMF)

In late 2024 the WiX project moved to a model where commercial use of v5+
requires a paid maintenance subscription. The licensing language is broad enough
that "we ship pwiz-sharp installers built by WiX 5" is plausibly in scope, and
keeping that question continuously answered (legal review, license tracking,
renewals) is overhead a long-lived open-source project doesn't want.

We tried sidestepping by **pinning to WiX 4.0.5** (MIT-licensed, OSMF-free) -
that's what `33ddbce2a3` did. But "v4 might change its terms too" remained a
plausible future-state concern, and pinning to a release of an MSI toolkit
nobody upstream is actively maintaining isn't a stable position.

**Inno Setup has never adopted any licensing scheme that's recurring or
ambiguous about commercial use.** Its license has been static since the early
2000s. That's the cleanest answer to the licensing question.

### 2. Burn's per-machine cache complicated the build

Microsoft's "Burn" bundler is the standard way to wrap an MSI + prereq installer
(like the .NET runtime EXE) into a single user-facing `.exe`. Burn requires a
**per-machine cache directory** under `ProgramData\Package Cache\{guid}\` where
it persists the MSI + the bundle for later modify/repair/uninstall operations.

That constraint forces two MSIs:

- `ProteoWizard-Sharp.msi` - per-user payload (installs under `%LocalAppData%`)
- `ProteoWizard-Sharp-perMachine.msi` - per-machine payload (installs under
  `%ProgramFiles%`)

Inside the Burn bundle, the chain picks which MSI runs based on the install
scope the user requested. So you end up building three artifacts: two MSIs +
the bundle. The bundle's per-machine cache itself always lives at the
per-machine path even for "for me" installs - which means Burn-bundled "for
me" installs trigger UAC purely for the cache write, not for any actual
payload deployment. That's a confusing UX wart.

Inno handles install scope at install-time via `PrivilegesRequired=lowest` +
`PrivilegesRequiredOverridesAllowed=dialog`. **One Setup.exe, one payload,
one MSBuild-style staging tree.** UAC fires only when the user picks "for
everyone" or when the .NET runtime install (which is unavoidably per-machine -
the runtime goes into `%ProgramFiles%\dotnet\`) actually runs.

### 3. WiX's prerequisite-detection language was fiddly

Detecting "is .NET 8 Desktop Runtime installed?" in WiX involves:

- A `bal:Condition` or `WixDependencyExt` reference
- An `<MsiPackage Vital="no">` for the bundled runtime EXE so its non-zero
  return code (e.g. "already installed") doesn't abort the whole bundle
- A `DetectCondition` expression that reads either a registry key or a file
  path via WiX's variable expansion language

Inno's equivalent is **25 lines of Pascal** in `[Code]` that walks
`C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.*\` and reports
whether the runtime is present. The filesystem layout is the same shape across
all .NET versions (it's what the `dotnet` host actually walks at startup), so
the check is authoritative regardless of how the runtime got installed
(`dotnet-install.ps1`, runtime installer EXE, MSI repackaged by an enterprise
deployment tool, etc.).

## What the current architecture looks like

```
pwiz-sharp/installer/
├── Setup.iss              ← one Inno script; the source of truth
├── build.ps1              ← orchestrator: refresh pins → dotnet build →
│                            stage → cache runtime → 2× ISCC → write sidecar
├── Refresh-VendorPins.ps1 ← vendor SDK pin generator (content-addressed
│                            from the 7z archives; called by build.ps1)
├── Ensure-InnoSetup.ps1   ← idempotent winget-bootstrap of ISCC.exe;
│                            no-op if Inno Setup is already installed
├── cache/                 ← .NET 8 runtime EXE (~56 MB, .gitignored)
└── build/                 ← output artifacts (.gitignored):
    ├── ProteoWizard-Sharp-Setup-<ver>.exe              (~62 MB)
    ├── ProteoWizard-Sharp-NoNetRuntime-Setup-<ver>.exe (~6.7 MB)
    ├── installer-version.txt
    └── stage/             ← filtered copy of the dotnet build output
```

### Two installer variants from one source

`Setup.iss` has a single `#ifndef NoNetRuntime` gate around the `[Files]` line
that bundles the runtime EXE and the matching `[Run]` entry that invokes it.
`build.ps1` runs ISCC twice over the same script:

- **Pass 1**: no preprocessor define → bundled variant.
- **Pass 2**: `/DNoNetRuntime` → lightweight variant; `InitializeSetup` aborts
  with a download-link dialog if .NET 8 is missing.

This is cheaper to maintain than two parallel `.iss` files (the bulk of the
script - install dirs, shortcuts, registry entries, AppId - is identical and
shouldn't drift between variants). The downside is the conditional preprocessor
syntax requires careful handling inside `[Code]` blocks (Inno's PascalScript
doesn't support `#ifdef`'d local `const` declarations; the runtime check inlines
its URL at the `ShellExec` site instead).

### Version stamping: 4.0.YYDOY-gitsha

`build.ps1` computes the version at compile time from:

- **`4.0`** - major version. The pwiz-sharp lineage is on 4.x; cpp pwiz stays
  on its existing 3.x series.
- **`YYDOY`** - 2-digit year + 3-digit day-of-year (e.g. `26140` = 2026-05-20).
  Sortable, year-boundary-safe (the YY increments before DOY resets), and
  zero-padded so lexical sort matches chronological sort.
- **`gitsha`** - 7-char prefix of `HEAD`. Identifies the source commit at a
  glance; matches `git log --abbrev=7` output.

Examples: `4.0.26140-345eff6`, `4.0.26140-a80263e`.

The version threads through three places:

1. **`Setup.iss` `[Setup]` block** - `AppVersion={#MyAppVersion}` is what
   Programs and Features displays. Also gets embedded in the AppId
   (`{guid}_{version}`) so multiple versions install side-by-side under
   distinct uninstall slots.
2. **Output filename suffix** - `ProteoWizard-Sharp-Setup-4.0.26140-gitsha.exe`
   so multiple builds can coexist in `installer/build/` without overwriting
   each other (release verification, cherry-pick smoke testing, local debug
   builds piled up while iterating).
3. **`installer-version.txt` sidecar** - `Installer.Tests` reads this to know
   what registry key to look under post-install. The test can't reliably
   parse it back from `Setup.iss` because the build-time `/DMyAppVersion=`
   override beats the in-file `#define` fallback.

`Setup.iss` keeps a `#ifndef MyAppVersion` fallback (`4.0.0-dev`) so direct
ISCC invocations outside `build.ps1` still produce a versioned installer for
local script debugging.

### Side-by-side multi-version installs

The `AppId` in `Setup.iss` embeds the version: `{{guid}}_{#MyAppVersion}`.
Inno keys every install on `AppId`, so a versioned `AppId` means:

- Each version gets its own uninstall slot in Programs and Features.
- Each version installs into its own dir (`...\ProteoWizard-Sharp\{version}\`).
- Same-version reinstall upgrades in place; different-version install
  coexists side-by-side.

Shared resources (Windows Explorer right-click verbs) use **last-installed-wins**
semantics: each install overwrites the verb's command to point at its own EXEs;
uninstall *doesn't* remove the verb. This is the deliberate choice that the
"latest installed" pwiz-sharp is the one a user gets when they right-click a
.raw file. The trade-off: if the user uninstalls every version they have, the
verb is orphaned until they install something else.

Start Menu group + Desktop shortcuts ARE versioned (the names include the
version string), so each install owns its own shortcuts and uninstall cleans
them up.

## Alternatives considered and rejected

| | Why not |
|---|---|
| **WiX 5** | OSMF licensing overhead (see above). |
| **WiX 4** | MIT-licensed today, but pinning to a no-longer-actively-maintained release of a Microsoft installer toolkit is a fragile position. The dual-MSI Burn shape is also more complex than what the project needs. |
| **MSIX** | Single-package, modern Windows packaging - appealing on paper. But: per-machine install requires the package be signed by a code-signing cert chained to a Microsoft-trusted root (we don't have that), per-user install isolates the app into a sandbox that complicates the vendor-SDK-extraction-at-runtime flow that `Pwiz.Vendor.Common`'s `VendorSdkLoader` uses, and the registry-based Explorer verb integration is more constrained in MSIX. |
| **Squirrel.Windows / Velopack** | Auto-update friendly, used by VS Code and others. But: per-user-only by default (the `--allUsers` install path requires a separate flow), and the vendor SDK extraction story doesn't fit cleanly. |
| **dotnet tool install** | Works for `msconvert-sharp` (a CLI exe), but doesn't deploy MSConvertGUI / SeeMS (WinForms apps need a host install) and doesn't register Explorer verbs. We do already ship `msconvert-sharp` via dotnet tool for the headless / CI use case. |
| **Manual zip + README** | Forces users to handle PATH, runtime install, Start Menu shortcuts themselves. Fine for advanced users; we still publish a zip for them, but the installer is the default user-facing path. |

## Things to watch / future work

### Code signing

The installer EXEs are currently unsigned. SmartScreen will warn the first
N users until reputation accumulates. A real Authenticode cert + signing the
output of every ISCC run would fix that, but the cert acquisition + signing
infrastructure is its own project. The signing step would slot into `build.ps1`
between the two ISCC passes and the report section.

### Cross-platform

Inno Setup is Windows-only. macOS and Linux installs of pwiz-sharp today rely
on `dotnet tool install` for `msconvert-sharp` and manual unzip for SeeMS.
A proper macOS .pkg / Linux .deb/.rpm story is out of scope for now -
mass-spec data analysis on those platforms tends to use containerized or
script-driven workflows where a per-platform installer adds friction rather
than removes it.

### Vendor SDKs ship via runtime download, not in the installer payload

Vendor SDK DLLs (Thermo's RawFileReader, Bruker's BAF2SQL / TDF SDK, Waters
MassLynx, etc.) are NOT included in the installer. Two reasons: (1) the
combined vendor footprint is ~100 MB which would dominate the installer size,
and (2) most users only ever read one or two vendors' files - shipping all of
them upfront wastes bandwidth.

What the installer DOES ship for vendor support:

- `Pwiz.Vendor.Common.dll` - hosts the `VendorSdkLoader` runtime that fetches
  vendor archives on demand.
- `VendorSdkPins.generated.cs` (compiled into `Pwiz.Vendor.Common.dll`) - a
  table of `(vendor, version, sha256, download URL)` tuples regenerated on
  every build by `Refresh-VendorPins.ps1` from the 7z archives in the
  source tree.
- `7za.exe` - the unpacker `VendorSdkLoader` shells out to for extraction.

At runtime, the first call into a vendor reader (e.g. opening a `.raw` file
through `Reader_Thermo`) triggers `VendorSdkLoader.Load(vendor)`. That:

1. Looks up the pinned `(version, sha256, url)` for the vendor.
2. Downloads the 7z archive into a per-user cache directory if not already
   present (`%LocalAppData%\Pwiz.Vendor\sdk\<vendor>-<version>.7z`).
3. Verifies the SHA-256.
4. Extracts via `7za.exe` into the cache.
5. `Assembly.LoadFrom`s the DLLs.

The staging step in `build.ps1` actively *excludes* vendor DLLs from the
installer payload via a name-prefix match against the pin table - even though
the DLLs are present in the local `vendor-assemblies/` directory at build time
(needed for `dotnet build` to resolve references against the SDK types). The
filter ensures they don't leak into the staged tree.

Trade-offs to be aware of:

- **First-vendor-use is online-required.** A user with no network on first
  launch can open mzML / mzXML / MGF / generic formats fine, but vendor-format
  reads fail until the SDK has been fetched at least once. Subsequent calls
  hit the cache and work offline.
- **The pin table is the authority.** Bumping a vendor SDK means dropping a
  new 7z archive into the source tree and re-running `Refresh-VendorPins.ps1`
  (build.ps1 calls it automatically). The new pin ships in the next installer
  build; users running a previous installer keep fetching their pinned
  version, so vendor-side breaking changes can't silently affect production
  installs.
- **Air-gapped installs** can pre-populate the cache by hand (drop the right
  7z files into `%LocalAppData%\Pwiz.Vendor\sdk\`) or run the vendor readers
  once on a connected machine and copy the cache across.

### .NET runtime version pinning

The bundled runtime is the latest stable .NET 8 at build time (resolved via
`https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe`, which redirects
to the current 8.0.x release). pwiz-sharp's projects target `net8.0` /
`net8.0-windows` so any 8.0.x runtime works. If we ever pin to a specific
8.0.x SDK + runtime pair for reproducibility, the URL becomes a versioned one
and the cache filename should follow.

### TeamCity integration

`tcbuild.bat` calls `installer/build.ps1` as part of the standard build via
`build.bat`. `Installer.Tests` then exercises the bundled variant end-to-end:
silent install per-user, file deployment check, msconvert smoke against a real
vendor sample, silent uninstall, registry-cleanup check. The per-machine
variant is gated `Inconclusive` when the test runner isn't elevated, so it
self-skips on the default TC agent.

The NoNetRuntime variant doesn't have its own TC test - running the same install
flow against a different .exe doesn't add coverage when the .NET runtime is
present on the agent (which it always is, since `dotnet test` runs first).
The `InitializeSetup` abort path is exercised when a developer tries it on a
machine without .NET 8 - manual; not currently CI-covered.
