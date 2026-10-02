<#
.SYNOPSIS
    Package Osprey for redistribution: a self-contained, per-RID ZIP for each
    target platform and (optionally) a Windows Setup.exe installer.

.DESCRIPTION
    Produces the canonical Osprey redistributable artifacts described in
    ai/todos/active/TODO-20260627_osprey_redistribution.md:

      * For each RID, a `dotnet publish -c Release -f net10.0 -r <rid>
        --self-contained` is laid out under a single versioned top-level
        folder (Osprey.exe + runtime/dependency DLLs + Documentation/ +
        README + LICENSE) and zipped to `Osprey-<version>-<rid>.zip`. The
        single containing folder means multiple versions coexist when
        unzipped side by side, and extraction never explodes ~200 files into
        the user's download dir.

      * With -Setup, the win-x64 publish is additionally packaged into an
        Inno Setup installer (Osprey-Setup-<version>.exe): per-user or
        per-machine at the user's choice, optional PATH entry and Start Menu
        shortcuts, an Add/Remove-Programs entry, and a "version-specific"
        install option for keeping several versions side by side. See
        Installer/Setup.iss.

    Self-contained means ZERO system-.NET dependency: copy the folder to an
    HPC node and run it. net10.0 is the canonical distribution runtime.

    This script is standalone (build.ps1 / Osprey.sln are the dev+CI build;
    this is the redistribution step on top of them). It is NOT wired into
    Boost.Build / the ProteoWizard release -- Osprey ships as its own tool.

.PARAMETER Rid
    Runtime identifiers to package. Default: win-x64, linux-x64. linux-x64
    cross-builds fine from Windows (the binaries just can't be run here).

.PARAMETER Configuration
    Debug or Release. Default Release.

.PARAMETER OutputDir
    Where the .zip/.exe artifacts land. Default <scriptRoot>/dist (gitignored).
    The per-RID publish trees are staged under <OutputDir>/_staging.

.PARAMETER Setup
    Also build the win-x64 Setup.exe (requires Inno Setup 6, bootstrapped by
    pwiz-sharp/installer/Ensure-InnoSetup.ps1 if absent, and that win-x64 is
    among -Rid). See Installer/Setup.iss.

.PARAMETER NoZip
    Skip the .zip step (e.g. -Setup -NoZip to produce only the installer).

.PARAMETER IncludePdb
    Keep *.pdb files in the package. Default: stripped (leaner release artifact).

.PARAMETER Sign
    Authenticode-sign Osprey.exe, the Setup.exe and its uninstaller. OFF by default. Also enabled by
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
    # Windows zip + Setup.exe only
    .\package.ps1 -Rid win-x64 -Setup

.EXAMPLE
    # CI
    .\package.ps1 -TeamCity -Setup
#>
#Requires -Version 7.0
param(
    [string[]]$Rid = @('win-x64','linux-x64'),
    [ValidateSet('Debug','Release')] [string]$Configuration = 'Release',
    [string]$OutputDir,
    [switch]$Setup,
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
# Absolute, because it is handed to ISCC, which resolves relative paths against the script.
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
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
function Test-OspreySignEnabled {
    return $Sign -or ($env:OSPREY_SIGN -eq '1')
}

# The signtool.exe path and the arguments that precede the file to sign, resolved
# from the OSPREY_SIGN* environment. Shared by Invoke-OspreySign (Osprey.exe in the
# stage) and New-OspreySetup, which hands the same command to ISCC so the Setup.exe
# and its uninstaller are signed as part of the compile.
function Get-OspreySignCommand {
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
    return [pscustomobject]@{ Exe = $signtool; Args = $signArgs }
}

function Invoke-OspreySign {
    param([string]$Path)
    if (-not (Test-OspreySignEnabled)) { return }

    $cmd = Get-OspreySignCommand
    $signArgs = $cmd.Args + $Path

    Write-Progress-Tc "Signing $(Split-Path -Leaf $Path)"
    & $cmd.Exe @signArgs | Out-Host
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

    Write-Progress-Tc "Publishing $folderName ($Configuration, self-contained net10.0)"
    $publishArgs = @(
        'publish', $ospreyCsproj,
        '-c', $Configuration,
        '-f', 'net10.0',
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

    # Sign the Windows exe inside the stage before zipping/Setup packaging.
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

    # README.md ships, but docs/*.md does not, and the workflow page lands in
    # Documentation/ rather than beside the README. Retarget both link shapes on
    # the COPY so the bundled README has no dead links: docs/ links go to GitHub
    # (there is nothing local to point at), and the workflow link gets its
    # subdirectory. Same approach as the CommandLine.html retarget above.
    #
    # The docs URL deliberately points at master rather than this build's commit.
    # A commit URL would describe exactly the code in the ZIP, but it 404s for any
    # bundle built from an unpushed branch (every developer build), and a dead link
    # is worse than a slightly newer one. Revisit if bundles are ever produced only
    # from pushed release tags.
    #
    # Read and write with explicit encoding rather than Get-Content/Set-Content
    # defaults: the README is BOM-less UTF-8 containing em dashes, and the default
    # round-trip mangles them and prepends a BOM on Windows PowerShell 5.1. The
    # #Requires above should prevent that, but the file is data, so make the
    # handling of it explicit anyway.
    $readmeStaged = Join-Path $StageDir 'README.md'
    Copy-Item (Join-Path $scriptRoot 'README.md') $readmeStaged -Force
    $readme = [System.IO.File]::ReadAllText($readmeStaged, [System.Text.Encoding]::UTF8)
    $docsUrl = 'https://github.com/ProteoWizard/pwiz/blob/master/pwiz_tools/Osprey/docs/'
    $readme = [regex]::Replace($readme, '\]\(docs/([^)]+)\)', ('](' + $docsUrl + '$1)'))
    $readme = [regex]::Replace($readme, '\]\(Osprey-workflow\.html\)', '](Documentation/Osprey-workflow.html)')
    [System.IO.File]::WriteAllText($readmeStaged, $readme, (New-Object System.Text.UTF8Encoding $false))

    Copy-Item (Join-Path $repoRoot 'LICENSE') (Join-Path $StageDir 'LICENSE') -Force
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

# --- Setup.exe (delegates layout to Installer/Setup.iss) ----------------
function New-OspreySetup {
    param([string]$StageDir)

    $iss = Join-Path $scriptRoot 'Installer\Setup.iss'
    if (-not (Test-Path $iss)) {
        Write-Error "Installer/Setup.iss not found at $iss"
        exit 2
    }

    # Inno Setup 6 is a per-user install of ~10 MB; Ensure-InnoSetup.ps1 fetches it
    # when the machine lacks it and prints the ISCC.exe path either way.
    $ensure = Join-Path $repoRoot 'pwiz-sharp\installer\Ensure-InnoSetup.ps1'
    $iscc = & pwsh -NoProfile -File $ensure -PassThru | Select-Object -Last 1
    if ($LASTEXITCODE -ne 0 -or -not $iscc -or -not (Test-Path $iscc)) {
        Write-Error "Inno Setup (ISCC.exe) is not available; cannot build the Setup.exe."
        exit 2
    }

    $baseName = "Osprey-Setup-$version"
    $setupPath = Join-Path $OutputDir "$baseName.exe"
    if (Test-Path $setupPath) { Remove-Item $setupPath -Force }

    $isccArgs = @(
        '/Q',
        "/DStagingDir=$StageDir",
        "/DOutputDir=$OutputDir",
        "/DOutputBaseFilename=$baseName",
        "/DMyAppVersion=$version"
    )
    if (Test-OspreySignEnabled) {
        # Inno signs the uninstaller and the Setup.exe itself with a named sign tool.
        # $f is replaced by the (already quoted) file being signed and $q is a literal
        # quote; a literal $ in an argument (a password, say) has to be written $$.
        $cmd = Get-OspreySignCommand
        $quotedArgs = $cmd.Args | ForEach-Object {
            $escaped = $_.Replace('$', '$$')
            if ($escaped -match '\s') { '$q' + $escaped + '$q' } else { $escaped }
        }
        $signCommand = ('$q' + $cmd.Exe.Replace('$', '$$') + '$q ' + ($quotedArgs -join ' ') + ' $f')
        $isccArgs += @('/DSignSetup', "/Sospreysign=$signCommand")
    }
    $isccArgs += $iss

    Write-Progress-Tc "Building $(Split-Path -Leaf $setupPath) (Inno Setup)"
    & $iscc @isccArgs | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Error "ISCC failed (exit $LASTEXITCODE)"
        exit $LASTEXITCODE
    }

    Publish-Artifact-Tc $setupPath
    return $setupPath
}

# --- Main ---------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null

Write-Host "Osprey package version $version" -ForegroundColor Green
Write-Host "  RIDs:       $($Rid -join ', ')" -ForegroundColor Green
Write-Host "  Output:     $OutputDir" -ForegroundColor Green
Write-Host "  Zip:        $(-not $NoZip)   Setup: $Setup   Sign: $(Test-OspreySignEnabled)" -ForegroundColor Green

$artifacts = @()
$winStage = $null
foreach ($r in $Rid) {
    $stage = New-OspreyStage -Rid $r
    if ($r -eq 'win-x64') { $winStage = $stage }
    if (-not $NoZip) {
        $artifacts += (New-OspreyZip -StageDir $stage -Rid $r)
    }
}

if ($Setup) {
    if (-not $winStage) {
        Write-Error "-Setup requires win-x64 in -Rid (got: $($Rid -join ', '))"
        exit 2
    }
    $artifacts += (New-OspreySetup -StageDir $winStage)
}

Write-Host "`nArtifacts:" -ForegroundColor Green
foreach ($a in $artifacts) {
    $sizeMb = [math]::Round((Get-Item $a).Length / 1MB, 1)
    Write-Host ("  {0}  ({1} MB)" -f $a, $sizeMb) -ForegroundColor Green
}
exit 0
