<#
.SYNOPSIS
Build the pwiz-sharp installer (Inno Setup).

.DESCRIPTION
End-to-end packaging pipeline:
  1. Refresh-VendorPins.ps1 — bake the current vendor SDK commit pins into
     VendorSdkPins.generated.cs (no-op if pins haven't changed).
  2. dotnet build src/MsConvertGUI/MsConvertGUI.csproj -c Release
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

$installerDir = $PSScriptRoot
$pwizSharp    = (Resolve-Path "$installerDir/..").Path
$msconvertGui = Join-Path $pwizSharp "src/MsConvertGUI/MsConvertGUI.csproj"
$buildOutput  = Join-Path $pwizSharp "src/MsConvertGUI/bin/Release/net8.0-windows"
$outDir       = Join-Path $installerDir "build"
$stagingDir   = Join-Path $outDir "stage"
$cacheDir     = Join-Path $installerDir "cache"

if (-not (Test-Path $outDir))   { New-Item -ItemType Directory $outDir   | Out-Null }
if (-not (Test-Path $cacheDir)) { New-Item -ItemType Directory $cacheDir | Out-Null }

# Dot-source the vendor table from Refresh-VendorPins.ps1 (single source of truth).
. (Join-Path $installerDir "Refresh-VendorPins.ps1")
$vendorSdkPrefixes = $Vendors | ForEach-Object { $_.Prefixes } | Sort-Object -Unique

# 1. Refresh vendor SDK pins.
Write-Host "==> Refresh-VendorPins" -ForegroundColor Cyan
pwsh -File (Join-Path $installerDir "Refresh-VendorPins.ps1")
if ($LASTEXITCODE -ne 0) { throw "Refresh-VendorPins failed (exit $LASTEXITCODE)" }

# 2. Build MSConvertGUI Release. Transitive deps land in the same bin/.
if (-not $SkipBuild) {
    Write-Host "`n==> dotnet build (Release)" -ForegroundColor Cyan
    dotnet build $msconvertGui -c Release "-p:IAgreeToVendorLicenses=true" --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }
}

foreach ($exe in @("MSConvertGUI-sharp.exe", "msconvert-sharp.exe", "7za.exe")) {
    if (-not (Test-Path (Join-Path $buildOutput $exe))) {
        throw "expected $exe in $buildOutput but it's missing — did the build succeed?"
    }
}

# 3. Stage a filtered copy of the build output.
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

$copied = 0; $bytesCopied = 0L; $bytesSkipped = 0L
Get-ChildItem $buildOutput -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($buildOutput.Length + 1)
    if (Should-Skip $rel) {
        $bytesSkipped += $_.Length
    } else {
        $dest = Join-Path $stagingDir $rel
        $destDir = Split-Path -Parent $dest
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory $destDir -Force | Out-Null }
        Copy-Item $_.FullName $dest
        $copied++; $bytesCopied += $_.Length
    }
}
Write-Host "    payload:  $copied files, $([math]::Round($bytesCopied/1MB, 1)) MB"
Write-Host "    skipped:  $([math]::Round($bytesSkipped/1MB, 1)) MB"

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

Write-Host "`n==> ISCC compile" -ForegroundColor Cyan
$iss = Join-Path $installerDir "Setup.iss"
# StagingDir, OutputDir come in as preprocessor defines (#define-able from CLI
# via /D). ISCC paths are most reliable when absolute.
& $iscc `
    "/Q" `
    "/DStagingDir=$stagingDir" `
    "/DOutputDir=$outDir" `
    "/DOutputBaseFilename=ProteoWizard-Sharp-Setup" `
    $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC compile failed (exit $LASTEXITCODE)" }

# 6. Report.
$setupPath = Join-Path $outDir "ProteoWizard-Sharp-Setup.exe"
$size = [math]::Round((Get-Item $setupPath).Length / 1MB, 1)
$hash = (Get-FileHash -Path $setupPath -Algorithm SHA256).Hash
Write-Host "`nSetup:   $setupPath" -ForegroundColor Green
Write-Host "Size:    $size MB"
Write-Host "SHA-256: $hash"
