<#
.SYNOPSIS
Build the pwiz-sharp installer (Inno Setup).

.DESCRIPTION
End-to-end packaging pipeline:
  1. Refresh-VendorPins.ps1 — bake the current vendor SDK commit pins into
     VendorSdkPins.generated.cs (no-op if pins haven't changed).
  2. dotnet build Tools/MsConvertGUI/Tools/MsConvert/src/MsConvertGUI/MsConvertGUI.csproj -c Release
     (transitively builds MsConvert, vendor projects, etc.)
  3. Stage a filtered copy of the build output (strips vendor SDK DLLs +
     debug symbols + cross-platform runtimes + BCL localization satellites)
  4. Download the .NET 8 desktop runtime installer EXE (cached under
     installer/cache/) so we can embed it in the Setup.exe
  5. Compile installer/Setup.iss with Inno Setup's ISCC → installer/build/
     ProteoWizard-Sharp-Setup.exe (~58 MB, single self-contained installer)

The Inno installer asks the user "Install for me / Install for everyone" at
runtime — drops the dual-MSI complexity of the prior WiX build. .NET 8 prereq
is detected via registry and the bundled runtime EXE installs (with UAC) if
missing.

.USAGE
    pwsh -File pwiz-sharp/installer/build.ps1
    pwsh -File pwiz-sharp/installer/build.ps1 -SkipBuild   # dotnet output is fresh
#>

#requires -Version 7.0
param(
    [switch] $SkipBuild
)
$ErrorActionPreference = 'Stop'

# .NET 8 desktop runtime download. Cached locally to avoid re-fetching on every
# build. The aka.ms URL redirects to the latest stable 8.0.x; pinned-by-content
# is not strictly required since we bundle it into the Setup.exe (the user
# downloads our installer, not the runtime separately).
$dotnetRuntimeUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"

$installerDir   = $PSScriptRoot
$pwizSharp      = (Resolve-Path "$installerDir/..").Path
$msconvertGui   = Join-Path $pwizSharp "Tools/MsConvertGUI/Tools/MsConvert/src/MsConvertGUI/MsConvertGUI.csproj"
$seems          = Join-Path $pwizSharp "Tools/SeeMS/src/SeeMS/SeeMS.csproj"
$msconvertGuiOut = Join-Path $pwizSharp "Tools/MsConvertGUI/Tools/MsConvert/src/MsConvertGUI/bin/Release/net8.0-windows"
$seemsOut       = Join-Path $pwizSharp "Tools/SeeMS/src/SeeMS/bin/Release/net8.0-windows"
$outDir         = Join-Path $installerDir "build"
$stagingDir     = Join-Path $outDir "stage"
$cacheDir       = Join-Path $installerDir "cache"

if (-not (Test-Path $outDir))   { New-Item -ItemType Directory $outDir   | Out-Null }
if (-not (Test-Path $cacheDir)) { New-Item -ItemType Directory $cacheDir | Out-Null }

# Dot-source the vendor table from Refresh-VendorPins.ps1 (single source of truth).
. (Join-Path $installerDir "Refresh-VendorPins.ps1")
$vendorSdkPrefixes = $Vendors | ForEach-Object { $_.Prefixes } | Sort-Object -Unique

# 1. Refresh vendor SDK pins.
Write-Host "==> Refresh-VendorPins" -ForegroundColor Cyan
pwsh -File (Join-Path $installerDir "Refresh-VendorPins.ps1")
if ($LASTEXITCODE -ne 0) { throw "Refresh-VendorPins failed (exit $LASTEXITCODE)" }

# 2. Build MSConvertGUI + SeeMS Release. MSConvertGUI's chain produces
#    msconvert-sharp.exe + MSConvertGUI-sharp.exe + all Pwiz.* DLLs. SeeMS is a
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

foreach ($exe in @("MSConvertGUI-sharp.exe", "msconvert-sharp.exe", "7za.exe")) {
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
    if ($relName -match '\.(dll|exe)$') {
        $leaf = Split-Path -Leaf $relName
        foreach ($p in $vendorSdkPrefixes) {
            if ($leaf -like "$p*") { return $true }
        }
    }
    return $false
}

function Stage-From([string] $source) {
    $copied = 0; $bytesCopied = 0L; $bytesSkipped = 0L; $dups = 0
    Get-ChildItem $source -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($source.Length + 1)
        if (Should-Skip $rel) { $bytesSkipped += $_.Length; return }
        $dest = Join-Path $stagingDir $rel
        if (Test-Path $dest) { $dups++; return }
        $destDir = Split-Path -Parent $dest
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory $destDir -Force | Out-Null }
        Copy-Item $_.FullName $dest
        $copied++; $bytesCopied += $_.Length
    }
    Write-Host "    from $((Split-Path -Leaf (Split-Path -Parent $source))): $copied new ($([math]::Round($bytesCopied/1MB, 2)) MB), $dups dup, $([math]::Round($bytesSkipped/1MB, 1)) MB skipped"
}

Stage-From $msconvertGuiOut
Stage-From $seemsOut

# 4. Cache the .NET 8 desktop runtime EXE (bundled into Setup.exe).
$dotnetExe = Join-Path $cacheDir "windowsdesktop-runtime-win-x64.exe"
Write-Host "`n==> .NET 8 desktop runtime (cached)" -ForegroundColor Cyan
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
#    Pass 1: default (bundles the .NET 8 desktop runtime; ~62 MB).
#    Pass 2: /DNoNetRuntime (skips the bundle; ~5 MB; aborts at install time if
#            .NET 8 isn't already present).
$iss = Join-Path $installerDir "Setup.iss"

function Invoke-Iscc {
    param(
        [string] $OutputBaseFilename,
        [string[]] $ExtraDefines = @()
    )
    Write-Host "`n==> ISCC compile: $OutputBaseFilename" -ForegroundColor Cyan
    $args = @(
        "/Q",
        "/DStagingDir=$stagingDir",
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
$bundledName = "ProteoWizard-Sharp-Setup-$appVersion"
$lightName   = "ProteoWizard-Sharp-NoNetRuntime-Setup-$appVersion"
Invoke-Iscc -OutputBaseFilename $bundledName
Invoke-Iscc -OutputBaseFilename $lightName -ExtraDefines @("/DNoNetRuntime")

# Write the resolved version next to the .exes so Installer.Tests can pin to it
# without re-deriving from the filename (the date+sha format is build.ps1's
# internal convention, not a public contract).
Set-Content -Path (Join-Path $outDir "installer-version.txt") -Value $appVersion -NoNewline

# 7. Report.
Write-Host ""
foreach ($base in @($bundledName, $lightName)) {
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
