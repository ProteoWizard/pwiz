<#
.SYNOPSIS
Ensure Inno Setup's ISCC.exe is installed and discoverable on this machine.

.DESCRIPTION
Idempotent. Used by tcbuild.bat to bootstrap fresh CI agents (and convenient
locally too).

Cascade (each step is best-effort; we fall through on any failure):
  1. ISCC.exe already discoverable -> done.
  2. winget already on PATH -> winget install JRSoftware.InnoSetup.
  3. winget missing -> bootstrap via the Microsoft.WinGet.Client PowerShell
     module's Repair-WinGetPackageManager cmdlet (per-user, no admin), then
     winget install Inno Setup. Skipped silently if PSGallery is unreachable
     from the agent.
  4. Both winget paths failed -> direct download of Inno Setup's installer
     from jrsoftware.org and run /VERYSILENT /CURRENTUSER. This last fallback
     has no dependency on PSGallery / Microsoft bootstrap URLs / winget, so
     it works on locked-down or offline-ish agents as long as
     https://jrsoftware.org is reachable.

Exits 0 iff ISCC.exe is discoverable at completion, 1 otherwise.

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

function Try-WingetBootstrap {
    Write-Host "Bootstrapping winget via Microsoft.WinGet.Client PowerShell module..."
    try {
        # Force TLS 1.2 for older Windows PowerShell that defaults to 1.0/1.1.
        # No-op on pwsh 7 (already uses modern TLS).
        try {
            [Net.ServicePointManager]::SecurityProtocol =
                [Net.ServicePointManager]::SecurityProtocol -bor
                [Net.SecurityProtocolType]::Tls12
        } catch { }

        # NB: deliberately NOT calling `Install-PackageProvider -Name NuGet`.
        # On the TC agents that hit this path, that call fails with
        # "No match was found for the specified search criteria for the
        # provider 'NuGet'" because the bootstrap URL
        # onegetcdn.azureedge.net is unreachable. PowerShell 7 (what
        # tcbuild.bat runs) has the NuGet provider built into PowerShellGet
        # already, so the explicit install isn't required — Install-Module
        # from PSGallery just works.
        if (-not (Get-Module -ListAvailable -Name Microsoft.WinGet.Client)) {
            Install-Module -Name Microsoft.WinGet.Client `
                -Force -Scope CurrentUser -Repository PSGallery -AllowClobber `
                -ErrorAction Stop
        }
        Import-Module Microsoft.WinGet.Client -ErrorAction Stop

        # Repair-WinGetPackageManager downloads the App Installer msixbundle plus
        # its VCLibs / Microsoft.UI.Xaml dependencies and installs them per-user
        # via Add-AppxPackage.
        Repair-WinGetPackageManager -ErrorAction Stop
    } catch {
        Write-Warning "winget bootstrap failed: $($_.Exception.Message)"
        return $false
    }
    return [bool](Get-Command winget -ErrorAction SilentlyContinue)
}

function Try-WingetInstallInno {
    Write-Host "winget install JRSoftware.InnoSetup ..."
    & winget install --id JRSoftware.InnoSetup `
        --silent --accept-source-agreements --accept-package-agreements `
        --disable-interactivity 2>&1 | ForEach-Object { Write-Host $_ }
    $code = $LASTEXITCODE
    # 0 = installed. -1978335189 = APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE
    # (already up-to-date). Both are success.
    if ($code -eq 0 -or $code -eq -1978335189) { return $true }
    Write-Warning "winget install JRSoftware.InnoSetup failed with exit $code"
    return $false
}

function Try-DirectInnoInstall {
    # Fallback when winget isn't available and can't be bootstrapped. Inno
    # Setup's own installer is itself built with Inno Setup, so it accepts
    # /VERYSILENT /CURRENTUSER and friends.
    $url = 'https://jrsoftware.org/download.php/is.exe'
    $exe = Join-Path $env:TEMP "innosetup-installer-$([Guid]::NewGuid().ToString('N')).exe"
    Write-Host "Downloading Inno Setup installer from $url ..."
    try {
        Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing -MaximumRedirection 10
    } catch {
        Write-Warning "Inno Setup direct download failed: $($_.Exception.Message)"
        return $false
    }
    Write-Host "Running Inno Setup installer (/VERYSILENT /CURRENTUSER) ..."
    & $exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER | Out-Host
    $code = $LASTEXITCODE
    Remove-Item $exe -Force -ErrorAction SilentlyContinue
    if ($code -ne 0) {
        Write-Warning "Inno Setup direct installer exited with code $code"
        return $false
    }
    return $true
}

# --- cascade ---

$iscc = Find-Iscc
if ($iscc) {
    Write-Host "Inno Setup already present at $iscc"
    if ($PassThru) { $iscc }
    exit 0
}

# Strategy 1: winget (existing or bootstrapped).
$haveWinget = [bool](Get-Command winget -ErrorAction SilentlyContinue)
if (-not $haveWinget) {
    $haveWinget = Try-WingetBootstrap
}
if ($haveWinget) {
    [void](Try-WingetInstallInno)
    $iscc = Find-Iscc
    if ($iscc) {
        Write-Host "Inno Setup installed via winget at $iscc"
        if ($PassThru) { $iscc }
        exit 0
    }
}

# Strategy 2: direct download fallback (no winget, no PSGallery — just
# Invoke-WebRequest against jrsoftware.org).
Write-Host "winget path didn't yield Inno Setup; falling back to direct installer download."
if (Try-DirectInnoInstall) {
    $iscc = Find-Iscc
    if ($iscc) {
        Write-Host "Inno Setup installed via direct download at $iscc"
        if ($PassThru) { $iscc }
        exit 0
    }
}

Write-Error ("All Inno Setup install paths failed. Install manually on this " +
             "agent: https://jrsoftware.org/isdl.php")
exit 1
