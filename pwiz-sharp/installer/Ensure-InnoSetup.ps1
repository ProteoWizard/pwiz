<#
.SYNOPSIS
Ensure Inno Setup's ISCC.exe is installed and discoverable on this machine.

.DESCRIPTION
Idempotent. Used by tcbuild.bat to bootstrap fresh CI agents (and convenient
locally too).

  1. ISCC.exe already discoverable -> done.
  2. Otherwise download Inno Setup's installer from jrsoftware.org and run it
     /VERYSILENT /CURRENTUSER. Per-user install, no admin needed.

We deliberately don't go through winget here. winget bootstrap on locked-down
agents needs PSGallery + the Microsoft App Installer msixbundle download
infrastructure, and our TC fleet hits intermittent reachability issues with
those endpoints. Inno Setup is a single static dependency — direct download
is one HTTP call, no package-manager state machine.

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

# --- fast path ---

$iscc = Find-Iscc
if ($iscc) {
    Write-Host "Inno Setup already present at $iscc"
    if ($PassThru) { $iscc }
    exit 0
}

# --- install ---

# Force TLS 1.2 for older Windows PowerShell that defaults to 1.0/1.1.
# No-op on pwsh 7 (already uses modern TLS).
try {
    [Net.ServicePointManager]::SecurityProtocol =
        [Net.ServicePointManager]::SecurityProtocol -bor
        [Net.SecurityProtocolType]::Tls12
} catch { }

# /download.php/is.exe is a stable redirect to the current 6.x installer. Inno
# Setup 6.x is API-compatible across minor versions, so we don't pin to a
# specific build — whatever the current latest is, ISCC will compile our .iss.
$url = 'https://jrsoftware.org/download.php/is.exe'
$exe = Join-Path $env:TEMP "innosetup-installer-$([Guid]::NewGuid().ToString('N')).exe"

Write-Host "Downloading Inno Setup installer from $url ..."
try {
    Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing -MaximumRedirection 10
} catch {
    Write-Error "Inno Setup download failed: $($_.Exception.Message)"
    exit 1
}

# Inno Setup's own installer is built with Inno Setup, so the standard silent
# flags apply: /VERYSILENT (no UI), /SUPPRESSMSGBOXES (no popups), /NORESTART
# (don't reboot), /CURRENTUSER (per-user install — no admin).
Write-Host "Running Inno Setup installer (/VERYSILENT /CURRENTUSER) ..."
& $exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER | Out-Host
$code = $LASTEXITCODE
Remove-Item $exe -Force -ErrorAction SilentlyContinue
if ($code -ne 0) {
    Write-Error "Inno Setup installer exited with code $code"
    exit 1
}

$iscc = Find-Iscc
if ($iscc) {
    Write-Host "Inno Setup installed at $iscc"
    if ($PassThru) { $iscc }
    exit 0
}
Write-Error "Installer reported success but ISCC.exe is still not discoverable."
exit 1
