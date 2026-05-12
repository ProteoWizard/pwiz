<#
.SYNOPSIS
Build the pwiz-sharp installer MSI.

.DESCRIPTION
End-to-end packaging pipeline:
  1. Refresh-VendorPins.ps1 — bake the current vendor SDK commit pins into
     VendorSdkPins.generated.cs (no-op if pins haven't changed).
  2. dotnet build src/MsConvertGUI/MsConvertGUI.csproj -c Release
     (transitively builds MsConvert, vendor projects, etc.)
  3. wix build installer/Package.wxs → installer/build/pwiz-sharp.msi
  4. Print artifact location + sha256.

By default, builds the per-user MSI. Pass -PerMachine to build the per-machine
variant instead. Both are buildable from the same .wxs via the BuildScope
preprocessor variable.

.USAGE
    pwsh -File pwiz-sharp/installer/build.ps1
    pwsh -File pwiz-sharp/installer/build.ps1 -PerMachine
    pwsh -File pwiz-sharp/installer/build.ps1 -SkipBuild   # if dotnet output is fresh
#>

#requires -Version 7.0
param(
    [switch] $PerMachine,
    [switch] $SkipBuild
)
$ErrorActionPreference = 'Stop'

$installerDir = $PSScriptRoot
$pwizSharp    = (Resolve-Path "$installerDir/..").Path
$msconvertGui = Join-Path $pwizSharp "src/MsConvertGUI/MsConvertGUI.csproj"
$buildOutput  = Join-Path $pwizSharp "src/MsConvertGUI/bin/Release/net8.0-windows"
$outDir       = Join-Path $installerDir "build"
$stagingDir   = Join-Path $outDir "stage"   # filtered copy of $buildOutput
$scope        = if ($PerMachine) { "perMachine" } else { "perUser" }
$msiName      = if ($PerMachine) { "ProteoWizard-Sharp-perMachine.msi" } else { "ProteoWizard-Sharp.msi" }
$msiPath      = Join-Path $outDir $msiName

# Dot-source the vendor table from Refresh-VendorPins.ps1 (single source of truth).
. (Join-Path $installerDir "Refresh-VendorPins.ps1")
$vendorSdkPrefixes = $Vendors | ForEach-Object { $_.Prefixes } | Sort-Object -Unique

if (-not (Test-Path $outDir)) { New-Item -ItemType Directory $outDir | Out-Null }

# 1. Refresh vendor SDK pins. Source of truth is git + the .7z files in the repo.
Write-Host "==> Refresh-VendorPins" -ForegroundColor Cyan
pwsh -File (Join-Path $installerDir "Refresh-VendorPins.ps1")
if ($LASTEXITCODE -ne 0) { throw "Refresh-VendorPins failed (exit $LASTEXITCODE)" }

# 2. Build MSConvertGUI in Release. Transitive deps (msconvert-sharp, vendor projects,
#    Pwiz.Vendor.Common, etc.) all land in this single bin/ folder.
if (-not $SkipBuild) {
    Write-Host "`n==> dotnet build (Release)" -ForegroundColor Cyan
    dotnet build $msconvertGui -c Release "-p:IAgreeToVendorLicenses=true" --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)" }
}

# Sanity: build output has the EXEs we expect.
foreach ($exe in @("MSConvertGUI-sharp.exe", "msconvert-sharp.exe", "7za.exe")) {
    if (-not (Test-Path (Join-Path $buildOutput $exe))) {
        throw "expected $exe in $buildOutput but it's missing — did the build succeed?"
    }
}

# 3. Stage a filtered copy of the build output. Drop:
#    - Vendor SDK DLLs (the whole reason VendorSdkLoader exists — fetched on first use)
#    - .pdb / .xml doc files (only useful at debug time, ~2.5 MB savings)
#    - runtimes/linux-x64 / osx-x64 / win-x86 (pwiz-sharp ships win-x64 only)
Write-Host "`n==> stage payload (strip vendor SDKs + debug symbols)" -ForegroundColor Cyan
if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
New-Item -ItemType Directory $stagingDir -Force | Out-Null

function Should-Skip([string] $relName) {
    # Strip debug symbols + xml docs (informational only at runtime).
    if ($relName -match '\.(pdb|xml)$') { return $true }
    # Strip cross-platform runtimes/ subtrees — pwiz-sharp is x64 Windows only.
    if ($relName -match '^runtimes[\\/](?!win-x64[\\/]|win[\\/])') { return $true }
    # Strip vendor SDK DLLs (resolved by VendorSdkLoader at runtime).
    if ($relName -match '\.(dll|exe)$') {
        $leaf = Split-Path -Leaf $relName
        foreach ($p in $vendorSdkPrefixes) {
            if ($leaf -like "$p*") { return $true }
        }
    }
    return $false
}

$skipped = 0; $copied = 0; $bytesSkipped = 0L; $bytesCopied = 0L
Get-ChildItem $buildOutput -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($buildOutput.Length + 1)
    if (Should-Skip $rel) {
        $skipped++; $bytesSkipped += $_.Length
    } else {
        $dest = Join-Path $stagingDir $rel
        $destDir = Split-Path -Parent $dest
        if (-not (Test-Path $destDir)) { New-Item -ItemType Directory $destDir -Force | Out-Null }
        Copy-Item $_.FullName $dest
        $copied++; $bytesCopied += $_.Length
    }
}
$mbCopied  = [math]::Round($bytesCopied / 1MB, 1)
$mbSkipped = [math]::Round($bytesSkipped / 1MB, 1)
Write-Host "    payload:  $copied files, $mbCopied MB"
Write-Host "    skipped:  $skipped files, $mbSkipped MB"

# 4. WiX build. The Package.wxs Files element picks up the entire staging tree.
#    WiX 5+ requires a one-time OSMF EULA acceptance — `wix eula accept wix7` if you
#    haven't already. pwiz is an open-source project, qualifies fee-free, but the
#    acceptance is a per-machine action.
Write-Host "`n==> wix build ($scope)" -ForegroundColor Cyan
$packageWxs = Join-Path $installerDir "Package.wxs"
$relStaging = [System.IO.Path]::GetRelativePath($installerDir, $stagingDir)
wix build $packageWxs `
    -d "BuildScope=$scope" `
    -d "StagingDir=$relStaging" `
    -o $msiPath
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nIf the failure is WIX7015 (EULA), accept once on this machine:" -ForegroundColor Yellow
    Write-Host "    wix eula accept wix7" -ForegroundColor Yellow
    throw "wix build failed (exit $LASTEXITCODE)"
}

# 5. Report.
$size = [math]::Round((Get-Item $msiPath).Length / 1MB, 1)
$hash = (Get-FileHash -Path $msiPath -Algorithm SHA256).Hash
Write-Host "`nMSI:   $msiPath" -ForegroundColor Green
Write-Host "Size:  $size MB"
Write-Host "SHA-256: $hash"
