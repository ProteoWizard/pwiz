<#
.SYNOPSIS
    Package Osprey for redistribution: a self-contained, per-RID ZIP for each
    target platform and (optionally) a Windows .msi installer.

.DESCRIPTION
    Produces the canonical Osprey redistributable artifacts described in
    ai/todos/active/TODO-20260627_osprey_redistribution.md:

      * For each RID, a `dotnet publish -c Release -f net8.0 -r <rid>
        --self-contained` is laid out under a single versioned top-level
        folder (Osprey.exe + runtime/dependency DLLs + Documentation/ +
        README + LICENSE) and zipped to `Osprey-<version>-<rid>.zip`. The
        single containing folder means multiple versions coexist when
        unzipped side by side, and extraction never explodes ~200 files into
        the user's download dir.

      * With -Msi, the win-x64 publish is additionally packaged into a
        per-machine `C:\Program Files\Osprey` WiX installer
        (Osprey-<version>-win-x64.msi) with an Add/Remove-Programs entry.

    Self-contained means ZERO system-.NET dependency: copy the folder to an
    HPC node and run it. net8.0 is the canonical distribution runtime; net472
    is intentionally not packaged.

    This script is standalone (build.ps1 / Osprey.sln are the dev+CI build;
    this is the redistribution step on top of them). It is NOT wired into
    Boost.Build / the ProteoWizard release -- Osprey ships as its own tool.

.PARAMETER Rid
    Runtime identifiers to package. Default: win-x64, linux-x64. linux-x64
    cross-builds fine from Windows (the binaries just can't be run here).

.PARAMETER Configuration
    Debug or Release. Default Release.

.PARAMETER OutputDir
    Where the .zip/.msi artifacts land. Default <scriptRoot>/dist (gitignored).
    The per-RID publish trees are staged under <OutputDir>/_staging.

.PARAMETER Msi
    Also build the win-x64 .msi (requires the `wix` dotnet tool and that
    win-x64 is among -Rid). See Installer/Osprey.wxs.

.PARAMETER NoZip
    Skip the .zip step (e.g. -Msi -NoZip to produce only the installer).

.PARAMETER IncludePdb
    Keep *.pdb files in the package. Default: stripped (leaner release artifact).

.PARAMETER Sign
    Authenticode-sign Osprey.exe and the .msi. OFF by default. Also enabled by
    setting OSPREY_SIGN=1. Requires signtool on PATH (or OSPREY_SIGNTOOL) and a
    cert: either OSPREY_SIGN_PFX (+ OSPREY_SIGN_PFX_PASSWORD) or, with no PFX,
    signtool's machine-store auto-select (/a). If signing is requested but the
    tool or cert is unavailable this script HARD-FAILS rather than shipping an
    unsigned artifact that looks signed.

.PARAMETER TeamCity
    Emit TeamCity service messages (progress + publishArtifacts) so a CI config
    can collect the artifacts.

.EXAMPLE
    # Local: both platform zips into dist/
    .\package.ps1

.EXAMPLE
    # Windows zip + msi only
    .\package.ps1 -Rid win-x64 -Msi

.EXAMPLE
    # CI
    .\package.ps1 -TeamCity -Msi
#>
param(
    [string[]]$Rid = @('win-x64','linux-x64'),
    [ValidateSet('Debug','Release')] [string]$Configuration = 'Release',
    [string]$OutputDir,
    [switch]$Msi,
    [switch]$NoZip,
    [switch]$IncludePdb,
    [switch]$Sign,
    [switch]$TeamCity
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot   = (Resolve-Path (Join-Path $scriptRoot '..\..')).Path
$ospreyCsproj = Join-Path $scriptRoot 'Osprey\Osprey.csproj'
if (-not (Test-Path $ospreyCsproj)) {
    Write-Error "Osprey.csproj not found at $ospreyCsproj"
    exit 2
}

. (Join-Path $scriptRoot 'version.ps1')
$version = Get-OspreyVersion -RepoPath $scriptRoot

if (-not $OutputDir) { $OutputDir = Join-Path $scriptRoot 'dist' }
$stagingRoot = Join-Path $OutputDir '_staging'

# --- TeamCity service-message helpers (mirror build.ps1) ----------------
function Format-TcMessage([string]$s) {
    if ($null -eq $s) { return '' }
    return $s.Replace('|', '||').Replace("'", "|'").Replace("`n", '|n').Replace("`r", '|r').Replace('[', '|[').Replace(']', '|]')
}
function Write-Progress-Tc([string]$msg) {
    if ($TeamCity) {
        Write-Host ("##teamcity[progressMessage '{0}']" -f (Format-TcMessage $msg))
    } else {
        Write-Host "==> $msg" -ForegroundColor Cyan
    }
}
function Publish-Artifact-Tc([string]$path) {
    if ($TeamCity) {
        Write-Host ("##teamcity[publishArtifacts '{0}']" -f (Format-TcMessage $path))
    }
}

# --- Signing (env-gated, hard-fail when requested-but-unavailable) -------
function Invoke-OspreySign {
    param([string]$Path)
    $enabled = $Sign -or ($env:OSPREY_SIGN -eq '1')
    if (-not $enabled) { return }

    $signtool = $env:OSPREY_SIGNTOOL
    if (-not $signtool) {
        $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
        if ($cmd) { $signtool = $cmd.Source }
    }
    if (-not $signtool -or -not (Test-Path $signtool)) {
        Write-Error "Signing requested but signtool.exe not found (set OSPREY_SIGNTOOL or add it to PATH). Refusing to ship an unsigned artifact."
        exit 3
    }

    $timestampUrl = if ($env:OSPREY_SIGN_TIMESTAMP_URL) { $env:OSPREY_SIGN_TIMESTAMP_URL } else { 'http://timestamp.digicert.com' }
    $signArgs = @('sign', '/fd', 'SHA256', '/tr', $timestampUrl, '/td', 'SHA256')
    if ($env:OSPREY_SIGN_PFX) {
        if (-not (Test-Path $env:OSPREY_SIGN_PFX)) {
            Write-Error "OSPREY_SIGN_PFX points to a missing file: $($env:OSPREY_SIGN_PFX)"
            exit 3
        }
        $signArgs += @('/f', $env:OSPREY_SIGN_PFX)
        if ($env:OSPREY_SIGN_PFX_PASSWORD) { $signArgs += @('/p', $env:OSPREY_SIGN_PFX_PASSWORD) }
    } else {
        # No PFX: let signtool auto-select a suitable cert from the machine store.
        $signArgs += '/a'
    }
    $signArgs += $Path

    Write-Progress-Tc "Signing $(Split-Path -Leaf $Path)"
    & $signtool @signArgs | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Error "signtool failed (exit $LASTEXITCODE) for $Path"
        exit 3
    }
}

# --- Per-RID publish + stage --------------------------------------------
function New-OspreyStage {
    param([string]$Rid)

    $folderName = "Osprey-$version-$Rid"
    $stageDir = Join-Path $stagingRoot $folderName
    if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $stageDir | Out-Null

    Write-Progress-Tc "Publishing $folderName ($Configuration, self-contained net8.0)"
    $publishArgs = @(
        'publish', $ospreyCsproj,
        '-c', $Configuration,
        '-f', 'net8.0',
        '-r', $Rid,
        '--self-contained', 'true',
        '-p:PublishSingleFile=false',
        '-p:Platform=x64',
        "-p:Version=$version",
        '-o', $stageDir,
        '-v', 'minimal', '--nologo'
    )
    # Out-Host keeps publish output visible/logged without it leaking into this
    # function's return value (PowerShell returns the whole success stream).
    & dotnet @publishArgs | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed for $Rid (exit $LASTEXITCODE)"
        exit $LASTEXITCODE
    }

    if (-not $IncludePdb) {
        Get-ChildItem -Path $stageDir -Filter *.pdb -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
    }

    Add-OspreyDocs -StageDir $stageDir

    # Sign the Windows exe inside the stage before zipping/MSI packaging.
    if ($Rid -like 'win-*') {
        Invoke-OspreySign -Path (Join-Path $stageDir 'Osprey.exe')
    }

    return $stageDir
}

# --- Documentation + README + LICENSE -----------------------------------
function Add-OspreyDocs {
    param([string]$StageDir)

    $docOut = Join-Path $StageDir 'Documentation'
    New-Item -ItemType Directory -Force -Path $docOut | Out-Null

    $cmdHelp = Join-Path $scriptRoot 'Documentation\Help\en\CommandLine.html'
    $workflow = Join-Path $scriptRoot 'Osprey-workflow.html'
    Copy-Item $cmdHelp (Join-Path $docOut 'CommandLine.html') -Force
    Copy-Item $workflow (Join-Path $docOut 'Osprey-workflow.html') -Force

    # The in-repo CommandLine.html cross-links to the workflow page via a
    # raw.githack master URL (good for the website). In the offline bundle,
    # both files sit side by side, so retarget that link to the local copy so
    # the docs work with no network. Operates on the COPY, never the source.
    $staged = Join-Path $docOut 'CommandLine.html'
    $html = Get-Content $staged -Raw
    $html = [regex]::Replace($html, 'https?://raw\.githack\.com/ProteoWizard/pwiz/[^"'' ]*Osprey-workflow\.html', 'Osprey-workflow.html')
    Set-Content -Path $staged -Value $html -NoNewline -Encoding utf8

    Copy-Item (Join-Path $scriptRoot 'README.md') (Join-Path $StageDir 'README.md') -Force
    Copy-Item (Join-Path $repoRoot 'LICENSE') (Join-Path $StageDir 'LICENSE') -Force
}

# --- License.rtf for the MSI UI (generated from the plain-text LICENSE) --
function Write-LicenseRtf {
    param([string]$Source, [string]$Destination)
    # WiX's license dialog requires RTF. Generate it from the canonical
    # plain-text LICENSE at build time so the installer license never drifts
    # from the repo. (LICENSE is ASCII, so no \uN escaping is needed.)
    $text = (Get-Content $Source -Raw).Replace('\', '\\').Replace('{', '\{').Replace('}', '\}')
    $body = ($text -split "`r?`n") -join '\par '
    $rtf = '{\rtf1\ansi\deff0{\fonttbl{\f0\fmodern Courier New;}}\fs16 ' + $body + '}'
    Set-Content -Path $Destination -Value $rtf -NoNewline -Encoding ascii
}

# --- Zip (single versioned top-level folder) ----------------------------
function New-OspreyZip {
    param([string]$StageDir, [string]$Rid)
    $zipPath = Join-Path $OutputDir "Osprey-$version-$Rid.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Write-Progress-Tc "Zipping $(Split-Path -Leaf $zipPath)"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    # includeBaseDirectory=$true -> the archive contains the single
    # "Osprey-<version>-<rid>/" folder, never root-exploded files.
    [System.IO.Compression.ZipFile]::CreateFromDirectory(
        $StageDir, $zipPath,
        [System.IO.Compression.CompressionLevel]::Optimal, $true)
    Publish-Artifact-Tc $zipPath
    return $zipPath
}

# --- MSI (delegates layout to Installer/Osprey.wxs) ---------------------
function New-OspreyMsi {
    param([string]$StageDir)

    $wxs = Join-Path $scriptRoot 'Installer\Osprey.wxs'
    if (-not (Test-Path $wxs)) {
        Write-Error "Installer/Osprey.wxs not found at $wxs"
        exit 2
    }
    $wix = Get-Command wix -ErrorAction SilentlyContinue
    if (-not $wix) {
        Write-Error "The 'wix' tool is not installed (dotnet tool install --global wix). Cannot build the .msi."
        exit 2
    }

    # MSI ProductVersion only compares major.minor.build for upgrades (the 4th
    # field is ignored). Map the Osprey version YEAR.ORDINAL.BRANCH.DOY ->
    # YEAR.ORDINAL.DOY so each dated build is upgrade-significant.
    $parts = $version.Split('.')
    $msiVersion = "$($parts[0]).$($parts[1]).$($parts[3])"

    $msiPath = Join-Path $OutputDir "Osprey-$version-win-x64.msi"
    if (Test-Path $msiPath) { Remove-Item $msiPath -Force }

    $licenseRtf = Join-Path $stagingRoot 'License.rtf'
    Write-LicenseRtf -Source (Join-Path $repoRoot 'LICENSE') -Destination $licenseRtf

    Write-Progress-Tc "Building $(Split-Path -Leaf $msiPath) (WiX, per-machine)"
    & wix build $wxs `
        -arch x64 `
        -d "PublishDir=$StageDir" `
        -d "ProductVersion=$msiVersion" `
        -d "InformationalVersion=$version" `
        -d "LicenseRtf=$licenseRtf" `
        -ext WixToolset.UI.wixext `
        -o $msiPath | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Error "wix build failed (exit $LASTEXITCODE)"
        exit $LASTEXITCODE
    }

    Invoke-OspreySign -Path $msiPath
    Publish-Artifact-Tc $msiPath
    return $msiPath
}

# --- Main ---------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null

Write-Host "Osprey package version $version" -ForegroundColor Green
Write-Host "  RIDs:       $($Rid -join ', ')" -ForegroundColor Green
Write-Host "  Output:     $OutputDir" -ForegroundColor Green
Write-Host "  Zip:        $(-not $NoZip)   Msi: $Msi   Sign: $($Sign -or ($env:OSPREY_SIGN -eq '1'))" -ForegroundColor Green

$artifacts = @()
$winStage = $null
foreach ($r in $Rid) {
    $stage = New-OspreyStage -Rid $r
    if ($r -eq 'win-x64') { $winStage = $stage }
    if (-not $NoZip) {
        $artifacts += (New-OspreyZip -StageDir $stage -Rid $r)
    }
}

if ($Msi) {
    if (-not $winStage) {
        Write-Error "-Msi requires win-x64 in -Rid (got: $($Rid -join ', '))"
        exit 2
    }
    $artifacts += (New-OspreyMsi -StageDir $winStage)
}

Write-Host "`nArtifacts:" -ForegroundColor Green
foreach ($a in $artifacts) {
    $sizeMb = [math]::Round((Get-Item $a).Length / 1MB, 1)
    Write-Host ("  {0}  ({1} MB)" -f $a, $sizeMb) -ForegroundColor Green
}
exit 0
