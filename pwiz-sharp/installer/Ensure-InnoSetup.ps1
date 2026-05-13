<#
.SYNOPSIS
Ensure Inno Setup's ISCC.exe is installed and discoverable on this machine.

.DESCRIPTION
Idempotent. Used by tcbuild.bat to bootstrap fresh CI agents (and convenient
locally too).

Cascade:
  1. Already installed? — return immediately.
  2. winget on PATH? — `winget install --id JRSoftware.InnoSetup`.
  3. winget missing? — install winget via the Microsoft.WinGet.Client
     PowerShell module's Repair-WinGetPackageManager cmdlet (per-user, no
     admin), then `winget install --id JRSoftware.InnoSetup`.

Exits 0 if ISCC.exe is discoverable at completion, 1 otherwise. Logs progress
to stdout; errors go to stderr.

.PARAMETER PassThru
Print the resolved ISCC.exe path on stdout if successful.

.USAGE
    pwsh -NoProfile -ExecutionPolicy Bypass -File installer/Ensure-InnoSetup.ps1
#>
[CmdletBinding()]
param(
    [switch] $PassThru
)
#requires -Version 5.1
$ErrorActionPreference = 'Stop'

function Find-Iscc {
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($p in @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe")) {
        if (Test-Path $p) { return $p }
    }
    return $null
}

function Ensure-Winget {
    if (Get-Command winget -ErrorAction SilentlyContinue) { return $true }

    Write-Host "winget not on PATH; bootstrapping via Microsoft.WinGet.Client PowerShell module..."
    try {
        # NuGet provider is needed before Install-Module can pull from PSGallery on a
        # fresh box. Already-installed is a no-op.
        Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser | Out-Null

        if (-not (Get-Module -ListAvailable -Name Microsoft.WinGet.Client)) {
            # PSGallery is untrusted by default; -Force accepts the trust prompt for
            # this install only and doesn't change the global trust setting.
            Install-Module -Name Microsoft.WinGet.Client `
                -Force -Scope CurrentUser -Repository PSGallery -AllowClobber
        }
        Import-Module Microsoft.WinGet.Client -ErrorAction Stop

        # Repair-WinGetPackageManager downloads the App Installer msixbundle plus
        # its VCLibs / Microsoft.UI.Xaml dependencies and installs them per-user
        # via Add-AppxPackage. Idempotent on machines that already have winget.
        Repair-WinGetPackageManager
    } catch {
        Write-Warning "Failed to bootstrap winget: $($_.Exception.Message)"
        return $false
    }
    return [bool](Get-Command winget -ErrorAction SilentlyContinue)
}

# 1. Fast path: already installed.
$iscc = Find-Iscc
if ($iscc) {
    Write-Host "Inno Setup already present at $iscc"
    if ($PassThru) { $iscc }
    exit 0
}

# 2 / 3. Make sure winget is available, then install Inno Setup with it.
if (-not (Ensure-Winget)) {
    Write-Error ("Could not provision winget; install Inno Setup manually on this " +
                 "agent (winget install --id JRSoftware.InnoSetup, or download from " +
                 "https://jrsoftware.org/isdl.php).")
    exit 1
}

Write-Host "Installing Inno Setup via winget..."
& winget install --id JRSoftware.InnoSetup `
    --silent --accept-source-agreements --accept-package-agreements `
    --disable-interactivity
$wingetExit = $LASTEXITCODE
# 0 = installed, -1978335189 (0x8A150049) = APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE
# (i.e. already up-to-date). Both are success states for an idempotent ensure.
if ($wingetExit -ne 0 -and $wingetExit -ne -1978335189) {
    Write-Error "winget install JRSoftware.InnoSetup failed with exit $wingetExit"
    exit 1
}

$iscc = Find-Iscc
if ($iscc) {
    Write-Host "Inno Setup installed at $iscc"
    if ($PassThru) { $iscc }
    exit 0
}
Write-Error "winget reported success but ISCC.exe is still not discoverable."
exit 1
