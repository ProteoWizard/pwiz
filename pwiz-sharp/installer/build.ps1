<#
.SYNOPSIS
Build the pwiz-sharp installer (Inno Setup).

.DESCRIPTION
End-to-end packaging pipeline:
  1. build/VendorPinsGenerator — bake the current vendor SDK commit pins into
     VendorSdkPins.generated.cs (no-op if pins haven't changed).
  2. dotnet build Tools/MsConvertGUI/src/MsConvertGUI.csproj -c Release
     (transitively builds MsConvert, vendor projects, etc.)
  3. Stage a filtered copy of the build output (strips vendor SDK DLLs +
     debug symbols + cross-platform runtimes + BCL localization satellites)
  4. Download the .NET 10 desktop runtime installer EXE (cached under
     installer/cache/) so we can embed it in the Setup.exe
  5. Compile installer/Setup.iss with Inno Setup's ISCC → installer/build/
     ProteoWizard-Setup.exe (~58 MB, single self-contained installer)

The Inno installer asks the user "Install for me / Install for everyone" at
runtime — drops the dual-MSI complexity of the prior WiX build. .NET 10 prereq
is detected via registry and the bundled runtime EXE installs (with UAC) if
missing.

.USAGE
    pwsh -File pwiz-sharp/installer/build.ps1
    pwsh -File pwiz-sharp/installer/build.ps1 -SkipBuild   # dotnet output is fresh
#>

#requires -Version 7.0
param(
    [switch] $SkipBuild,
    # Also produce ProteoWizard-WithVendorSdks-Setup-<ver>.exe: the bundled-runtime
    # installer plus every Windows vendor SDK, pre-extracted into VendorSdkLoader's cache.
    # That variant never contacts raw.githubusercontent.com, which is what makes it usable
    # in an offline or containerised deployment (see ProteoWizard/container).
    [switch] $WithVendorSdks
)
$ErrorActionPreference = 'Stop'

# .NET 10 desktop runtime download. Cached locally to avoid re-fetching on every
# build. The aka.ms URL redirects to the latest stable 10.0.x; pinned-by-content
# is not strictly required since we bundle it into the Setup.exe (the user
# downloads our installer, not the runtime separately).
$dotnetRuntimeUrl = "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"

$installerDir = $PSScriptRoot
# Anchor every path off the pwiz-sharp/ root. Get-PwizSharpRoot uses sentinel
# discovery (pwiz/+Tools/+Pwiz.sln), so future tree restructures don't
# invalidate the paths below — only the discovery helper itself.
. (Join-Path $installerDir "../scripts/Get-PwizSharpRoot.ps1")
$pwizSharp       = $PwizSharpRoot
$msconvertGui    = Join-Path $pwizSharp "Tools/MsConvertGUI/src/MsConvertGUI.csproj"
$seems           = Join-Path $pwizSharp "Tools/SeeMS/src/SeeMS.csproj"
$msconvertGuiOut = Join-Path $pwizSharp "Tools/MsConvertGUI/src/bin/Release/net10.0-windows"
$seemsOut        = Join-Path $pwizSharp "Tools/SeeMS/src/bin/Release/net10.0-windows"
$msconvertOut    = Join-Path $pwizSharp "Tools/Commandline/MsConvert/src/bin/Release/net10.0"
$outDir         = Join-Path $installerDir "build"
$stagingDir     = Join-Path $outDir "stage"
$cacheDir       = Join-Path $installerDir "cache"

if (-not (Test-Path $outDir))   { New-Item -ItemType Directory $outDir   | Out-Null }
if (-not (Test-Path $cacheDir)) { New-Item -ItemType Directory $cacheDir | Out-Null }

# Read the vendor table from build/vendor-sdk-pins.json (single source of truth, shared
# with build/VendorPinsGenerator).
$pinsJson = Join-Path $pwizSharp "build/vendor-sdk-pins.json"
$vendorSdkPrefixes = (Get-Content -Raw $pinsJson | ConvertFrom-Json).vendors |
    ForEach-Object { $_.prefixes } | Sort-Object -Unique

# 1. Refresh vendor SDK pins.
#    --require-all-pins because step 3 strips every vendor SDK from the payload (it filters on
#    the very prefixes this table supplies), leaving the pinned URLs as the installed app's only
#    route to a vendor SDK. Without it an uncommitted archive just warns, the installer builds
#    green, and the missing vendor shows up as "Unable to load DLL '<x>'" on a user's machine.
Write-Host "==> VendorPinsGenerator" -ForegroundColor Cyan
$pinsGenProj = Join-Path $pwizSharp "build/VendorPinsGenerator/VendorPinsGenerator.csproj"
dotnet run --project $pinsGenProj -c Release -- (Split-Path -Parent $pwizSharp) --require-all-pins
if ($LASTEXITCODE -ne 0) { throw "VendorPinsGenerator failed (exit $LASTEXITCODE)" }

# 2. Build MSConvertGUI + SeeMS Release. MSConvertGUI's chain produces
#    msconvert.exe + MSConvertGUI-sharp.exe + all Pwiz.* DLLs. SeeMS is a
#    separate WinExe target that produces seems-sharp.exe (plus its own copies
#    of the shared Pwiz.* DLLs which are identical so deduping is trivial at
#    staging time).
if (-not $SkipBuild) {
    Write-Host "`n==> dotnet build (Release)" -ForegroundColor Cyan
    dotnet build $msconvertGui -c Release "-p:IAgreeToVendorLicenses=true" --nologo
    if ($LASTEXITCODE -ne 0) { throw "MSConvertGUI build failed (exit $LASTEXITCODE)" }
    dotnet build $seems        -c Release "-p:IAgreeToVendorLicenses=true" --nologo
    if ($LASTEXITCODE -ne 0) { throw "SeeMS build failed (exit $LASTEXITCODE)" }
}

foreach ($exe in @("MSConvertGUI-sharp.exe", "msconvert.exe", "7za.exe")) {
    if (-not (Test-Path (Join-Path $msconvertGuiOut $exe))) {
        throw "expected $exe in $msconvertGuiOut but it's missing — did the build succeed?"
    }
}
if (-not (Test-Path (Join-Path $seemsOut "seems-sharp.exe"))) {
    throw "expected seems-sharp.exe in $seemsOut but it's missing — did the SeeMS build succeed?"
}

# 3. Stage a filtered copy of the build output. We walk MSConvertGUI's bin
#    first, then SeeMS's bin — the second pass adds seems-sharp.exe + any
#    SeeMS-only deps (ZedGraph, MSGraph, DigitalRune.Windows.Docking) without
#    overwriting files MSConvertGUI already staged.
Write-Host "`n==> stage payload (strip vendor SDKs + debug symbols + i18n satellites)" -ForegroundColor Cyan
if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
New-Item -ItemType Directory $stagingDir -Force | Out-Null

function Should-Skip([string] $relName) {
    if ($relName -match '\.(pdb|xml)$') { return $true }
    if ($relName -match '^runtimes[\\/](?!win-x64[\\/]|win[\\/])') { return $true }
    if ($relName -match '^(cs|de|es|fr|it|ja|ko|pl|pt-BR|ru|tr|zh-Hans|zh-Hant)[\\/]') { return $true }
    # Bruker's CompassXtract runtime (YEP / FID) is fetched to the vendor cache and then copied
    # next to the executable on first use, so a developer bin that has converted a YEP holds ~25 MB
    # of it. The DLLs are caught by the vendor prefixes below; these two are not, and the marker in
    # particular MUST NOT ship: it is what tells the installed app the payload is already in place,
    # so shipping it without the payload would disable the install-on-first-use it stands for.
    if ($relName -match '\.(installed|cxttmp)$') { return $true }
    # .manifest joins dll/exe in the prefix check because the CompassXtract SxS manifests are
    # vendor payload too. It is a prefix match, not a blanket rule, so the Microsoft.VC90.*
    # manifests we deliberately ship (see Bruker.csproj) are untouched.
    if ($relName -match '\.(dll|exe|manifest)$') {
        $leaf = Split-Path -Leaf $relName
        foreach ($p in $vendorSdkPrefixes) {
            if ($leaf -like "$p*") { return $true }
        }
    }
    return $false
}

function Stage-From([string] $source, [switch] $TopLevelOnly, [string] $DestRoot = $stagingDir) {
    $copied = 0; $bytesCopied = 0L; $bytesSkipped = 0L; $dups = 0
    $items = if ($TopLevelOnly) { Get-ChildItem $source -File } else { Get-ChildItem $source -Recurse -File }
    $items | ForEach-Object {
        $rel = $_.FullName.Substring($source.Length + 1)
        if (Should-Skip $rel) { $bytesSkipped += $_.Length; return }
        $dest = Join-Path $DestRoot $rel
        if (Test-Path $dest) { $dups++; return }
        $destDir = Split-Path -Parent $dest
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory $destDir -Force | Out-Null }
        Copy-Item $_.FullName $dest
        $copied++; $bytesCopied += $_.Length
    }
    Write-Host "    from $((Split-Path -Leaf (Split-Path -Parent $source))): $copied new ($([math]::Round($bytesCopied/1MB, 2)) MB), $dups dup, $([math]::Round($bytesSkipped/1MB, 1)) MB skipped"
}

function Build-Payload([string] $DestRoot) {
Stage-From $msconvertGuiOut -DestRoot $DestRoot
Stage-From $seemsOut -DestRoot $DestRoot
# msconvert's own bin, third: MSConvertGUI's chain builds msconvert.exe, but a referencing
# project only inherits the referenced project's assemblies, not every transitive package
# asset it resolved. MsConvert pulls five that MSConvertGUI does not -- notably
# System.Configuration.ConfigurationManager, which Agilent's MHDAC needs to read
# BaseDataAccess.dll.config. Staging the GUI alone shipped an msconvert.exe whose Agilent
# reader died at the first spectrum with "Could not load ... ConfigurationManager".
#
# Top level only: this bin also holds a nested win-x64/ RID tree that is a second copy of
# everything already staged above. Recursing it added 706 files / 238 MB and pushed the
# installer from 68 MB to 113 MB for five assemblies that all sit in the root.
Stage-From $msconvertOut -TopLevelOnly -DestRoot $DestRoot

# ...plus msconvert's wiff2/ subdirectory, which MSConvertGUI's bin also lacks. Wiff2LoadContext
# loads its Cecil-patched Unity.Abstractions and the SDK-matched System.Data.SQLite 1.0.109 from
# AppContext.BaseDirectory\wiff2; without it the wiff2 ALC initialises against the wrong
# dependency versions and the read fails later, reaching for SCIEX.Apis.Control.v1 through the
# default ALC (which no archive supplies). Named explicitly rather than recursing $msconvertOut:
# the only other subtree there is a win-x64/ RID copy of everything already staged.
$wiff2Src = Join-Path $msconvertOut "wiff2"
if (Test-Path $wiff2Src) {
    $wiff2Dest = Join-Path $DestRoot "wiff2"
    New-Item -ItemType Directory $wiff2Dest -Force | Out-Null
    $n = 0
    Get-ChildItem $wiff2Src -File | ForEach-Object {
        if (Should-Skip "wiff2\$($_.Name)") { return }
        Copy-Item $_.FullName (Join-Path $wiff2Dest $_.Name)
        $n++
    }
    Write-Host "    from wiff2/: $n files"
}
}

Build-Payload $stagingDir

# 3b. Vendor SDK cache (only for -WithVendorSdks).
#
#     Produces one <Vendor>-<ShortSha> directory per Windows pin, laid out exactly as
#     VendorSdkLoader.EnsureExtracted would have produced it on first use, each with the
#     .ok marker that tells the loader not to download or extract. Mirrors that method
#     and FlattenVendorArchiveLayout: 7za with the archive password, then hoist
#     vendor_api/<Vendor>/** to the top level, dropping wrong-arch subtrees. Keep the two
#     in step — a layout the loader disagrees with fails as a silent re-download, not an
#     error. Verify-VendorCache below is the guard against exactly that drift.
$vendorCacheDir = Join-Path $outDir "vendor-cache"

function Build-VendorCache {
    $sevenZa = Join-Path $msconvertGuiOut "7za.exe"
    if (-not (Test-Path $sevenZa)) { throw "7za.exe not found at $sevenZa" }
    # Same fixed licence-agreement password the runtime loader uses.
    $pw = "i-agree-to-the-vendor-licenses"
    $repoRoot = Split-Path -Parent $pwizSharp

    # Versions come from the pin table the generator just rewrote (step 1), so the cache
    # keys cannot disagree with the table the installed app resolves against.
    $pinsCs = Join-Path $pwizSharp "pwiz/src/Vendor/Common/VendorSdkPins.generated.cs"
    $pinsCsText = Get-Content -Raw $pinsCs
    $versions = @{}
    foreach ($m in [regex]::Matches($pinsCsText, 'Name:\s*"([^"]+)",\s*[\r\n]+\s*Version:\s*"([^"]+)"')) {
        $versions[$m.Groups[1].Value] = $m.Groups[2].Value
    }

    if (Test-Path $vendorCacheDir) { Remove-Item $vendorCacheDir -Recurse -Force }
    New-Item -ItemType Directory $vendorCacheDir -Force | Out-Null

    $vendors = (Get-Content -Raw $pinsJson | ConvertFrom-Json).vendors
    foreach ($v in $vendors) {
        # Windows installer: skip pins that exist only for a non-Windows runtime.
        if ($v.os -and $v.os -ne 'windows') {
            Write-Host "    skip $($v.name) (os=$($v.os))"
            continue
        }
        if (-not $versions.ContainsKey($v.name)) {
            throw "no Version for '$($v.name)' in $pinsCs — did VendorPinsGenerator run?"
        }
        $archive = Join-Path $repoRoot $v.path
        if (-not (Test-Path $archive)) { throw "vendor archive missing: $archive" }

        $dest = Join-Path $vendorCacheDir "$($v.name)-$($versions[$v.name])"
        New-Item -ItemType Directory $dest -Force | Out-Null
        & $sevenZa x -y "-p$pw" "-o$dest" $archive | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "7za failed extracting $archive (exit $LASTEXITCODE)" }

        # Flatten vendor_api/<Vendor>/** onto $dest, dropping x86/mips; first file wins.
        $nested = Join-Path $dest "vendor_api"
        if (Test-Path $nested) {
            foreach ($vendorDir in Get-ChildItem $nested -Directory) {
                foreach ($f in Get-ChildItem $vendorDir.FullName -Recurse -File) {
                    $rel = $f.FullName.Substring($vendorDir.FullName.Length + 1)
                    $first = ($rel -split '[\\/]')[0]
                    if ($first -ieq 'x86' -or $first -ieq 'mips') { continue }
                    $target = Join-Path $dest $f.Name
                    if (-not (Test-Path $target)) { Move-Item $f.FullName $target }
                }
            }
            Remove-Item $nested -Recurse -Force
        }

        # Overlay any assembly the build patched. Agilent's BaseCommon / BaseDataAccess are
        # Cecil-rewritten by Agilent.csproj to strip Delegate.BeginInvoke, which .NET 5+ removed;
        # the archive still holds the originals. Extracting straight from the archive therefore
        # caches an unpatched SDK, and Agilent dies on open with "MassSpecDataReader uses
        # delegate.BeginInvoke ... cannot open Agilent .d files" — but only for files that reach
        # the async metadata path, which is why a single-scan fixture does not catch it.
        foreach ($f in Get-ChildItem $dest -File -Filter *.dll) {
            $built = Join-Path $msconvertOut $f.Name
            if ((Test-Path $built) -and ((Get-Item $built).Length -ne $f.Length)) {
                Copy-Item $built $f.FullName -Force
                Write-Host "      overlaid build-patched $($f.Name)"
            }
        }

        # Shimadzu's natives (IOModuleQTFL and friends) are loaded from this cache directory by
        # full path, so the loader resolves THEIR imports from here first, not from the directory
        # holding the executable. The VC++ 2015-2022 runtime they link therefore has to be here
        # too: app-local is enough on a machine carrying the redistributable, but not in a wine
        # container, where the reader instead returns a structurally valid mzML with no spectra
        # at all. MFC140 is the one wine has no builtin for.
        if ($v.name -eq 'Shimadzu') {
            foreach ($dll in @('mfc140.dll','msvcp140.dll','concrt140.dll','vcruntime140.dll','vcruntime140_1.dll')) {
                $src = Join-Path $msconvertOut $dll
                if (Test-Path $src) { Copy-Item $src (Join-Path $dest $dll) -Force }
            }
            Write-Host "      staged VC140 runtime beside the Shimadzu natives"
        }

        Set-Content -Path (Join-Path $dest ".ok") -NoNewline `
            -Value "staged by installer/build.ps1 -WithVendorSdks from $($v.path)"
        $mb = [math]::Round((Get-ChildItem $dest -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
        Write-Host "    $($v.name)-$($versions[$v.name]): $mb MB"
    }
}

function Verify-VendorCache {
    # A cache the loader disagrees with degrades to a silent re-download, so assert the
    # two invariants that would cause it: the .ok marker, and a flattened layout.
    $dirs = Get-ChildItem $vendorCacheDir -Directory
    if ($dirs.Count -eq 0) { throw "vendor cache is empty" }
    foreach ($d in $dirs) {
        if (-not (Test-Path (Join-Path $d.FullName ".ok"))) { throw "$($d.Name): no .ok marker" }
        if (Test-Path (Join-Path $d.FullName "vendor_api")) { throw "$($d.Name): vendor_api not flattened" }
        if (-not (Get-ChildItem $d.FullName -File -Filter *.dll)) { throw "$($d.Name): no DLLs at top level" }
    }
    Write-Host "    verified $($dirs.Count) cache directories"
}

if ($WithVendorSdks) {
    Write-Host "`n==> vendor SDK cache (-WithVendorSdks)" -ForegroundColor Cyan
    Build-VendorCache
    Verify-VendorCache
}

# 4. Cache the .NET 10 desktop runtime EXE (bundled into Setup.exe). The filename carries
#    the major version on purpose: the old version-agnostic name meant a machine holding a
#    cached .NET 10 exe would silently skip the download and bundle the wrong runtime.
$dotnetExe = Join-Path $cacheDir "windowsdesktop-runtime-10.0-win-x64.exe"
Write-Host "`n==> .NET 10 desktop runtime (cached)" -ForegroundColor Cyan
if (-not (Test-Path $dotnetExe)) {
    Write-Host "    downloading $dotnetRuntimeUrl"
    Invoke-WebRequest -Uri $dotnetRuntimeUrl -OutFile $dotnetExe
}
Write-Host "    $([math]::Round((Get-Item $dotnetExe).Length / 1MB, 1)) MB at $dotnetExe"

# 5. ISCC compile.
$iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if (-not $iscc) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    )
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) {
        throw "ISCC.exe not found. Install Inno Setup 6 (winget install JRSoftware.InnoSetup) and rerun."
    }
    $iscc = $found
} else {
    $iscc = $iscc.Source
}

# 5. Stamp a build-time version: 4.0.YYDOY-gitsha.
#    Mirrors the cpp pwiz tagging convention (major 4 = the .NET-port lineage).
#       YYDOY  = two-digit year + three-digit day-of-year (e.g. 26140 = 2026-05-20).
#                Sortable, ~5 chars, unambiguous across years, and zero-padded so
#                lexical sort matches chronological sort.
#       gitsha = first 7 chars of HEAD; --short defaults to 7. We strip the leading
#                `g` prefix that `git describe` would add — Inno's version field
#                accepts arbitrary text but starting with a letter trips some
#                Win32 version-info parsers.
#    Local "dev" builds with no git history fall back to 4.0.0-dev so direct
#    ISCC invocations still produce a versioned installer.
$today = Get-Date
$yyDoy = "{0:00}{1:000}" -f ($today.Year % 100), $today.DayOfYear
$gitSha = ""
try {
    Push-Location $pwizSharp
    $gitSha = (git rev-parse --short=7 HEAD 2>$null).Trim()
} catch { }
finally { Pop-Location }
if ([string]::IsNullOrWhiteSpace($gitSha)) {
    $appVersion = "4.0.0-dev"
} else {
    $appVersion = "4.0.$yyDoy-$gitSha"
}
Write-Host "`n==> Stamping version: $appVersion" -ForegroundColor Cyan

# 6. ISCC compile — produce both installer variants from one Setup.iss source.
#    Pass 1: default (bundles the .NET 10 desktop runtime; ~62 MB).
#    Pass 2: /DNoNetRuntime (skips the bundle; ~5 MB; aborts at install time if
#            .NET 10 isn't already present).
$iss = Join-Path $installerDir "Setup.iss"

function Invoke-Iscc {
    param(
        [string] $OutputBaseFilename,
        [string[]] $ExtraDefines = @(),
        [string] $Staging = $stagingDir
    )
    Write-Host "`n==> ISCC compile: $OutputBaseFilename" -ForegroundColor Cyan
    $args = @(
        "/Q",
        "/DStagingDir=$Staging",
        "/DOutputDir=$outDir",
        "/DOutputBaseFilename=$OutputBaseFilename",
        "/DMyAppVersion=$appVersion"
    ) + $ExtraDefines + @($iss)
    & $iscc @args
    if ($LASTEXITCODE -ne 0) { throw "ISCC compile failed for $OutputBaseFilename (exit $LASTEXITCODE)" }
}

# Version suffix on the filenames so multiple builds can coexist in one folder
# without overwriting each other (releases, nightlies, cherry-pick verifications,
# etc. all drop side-by-side into installer/build/ instead of clobbering the
# previous run's artifact).
$bundledName = "ProteoWizard-Setup-$appVersion"
$lightName   = "ProteoWizard-NoNetRuntime-Setup-$appVersion"
Invoke-Iscc -OutputBaseFilename $bundledName
Invoke-Iscc -OutputBaseFilename $lightName -ExtraDefines @("/DNoNetRuntime")

# Pass 3 (opt-in): bundled runtime + the vendor SDK cache. Built on the bundled-runtime
# variant rather than the light one because its whole reason to exist is deployments that
# cannot reach the network, and those cannot fetch a .NET runtime either.
$vendorName = "ProteoWizard-WithVendorSdks-Setup-$appVersion"
if ($WithVendorSdks) {
    Invoke-Iscc -OutputBaseFilename $vendorName -Staging $stagingDir `
        -ExtraDefines @("/DWithVendorSdks", "/DVendorCacheDir=$vendorCacheDir")
}

# Write the resolved version next to the .exes so Installer.Tests can pin to it
# without re-deriving from the filename (the date+sha format is build.ps1's
# internal convention, not a public contract).
Set-Content -Path (Join-Path $outDir "installer-version.txt") -Value $appVersion -NoNewline

# 7. Report.
Write-Host ""
$reportNames = @($bundledName, $lightName)
if ($WithVendorSdks) { $reportNames += $vendorName }
foreach ($base in $reportNames) {
    $setupPath = Join-Path $outDir "$base.exe"
    if (-not (Test-Path $setupPath)) {
        Write-Host "MISSING: $setupPath" -ForegroundColor Red
        continue
    }
    $size = [math]::Round((Get-Item $setupPath).Length / 1MB, 1)
    $hash = (Get-FileHash -Path $setupPath -Algorithm SHA256).Hash
    Write-Host "Setup:   $setupPath" -ForegroundColor Green
    Write-Host "Version: $appVersion"
    Write-Host "Size:    $size MB"
    Write-Host "SHA-256: $hash"
    Write-Host ""
}
