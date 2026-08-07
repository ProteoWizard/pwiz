<#
.SYNOPSIS
Ensure Inno Setup's ISCC.exe is installed and discoverable on this machine.

.DESCRIPTION
Idempotent. Used by tcbuild.bat to bootstrap fresh CI agents (and convenient
locally too).

  1. ISCC.exe already discoverable -> done.
  2. Otherwise download Inno Setup's installer and run it /VERYSILENT
     /CURRENTUSER. Per-user install, no admin needed.

We deliberately don't go through winget here. winget bootstrap on locked-down
agents needs PSGallery + the Microsoft App Installer msixbundle download
infrastructure, and our TC fleet hits intermittent reachability issues with
those endpoints. Inno Setup is a single static dependency — direct download
is one HTTP call, no package-manager state machine.

Download source: a PINNED versioned installer on the official GitHub releases
(release-assets.githubusercontent.com — reliable, returns real bytes). We do NOT
use jrsoftware.org/download.php/is.exe as the primary source: that endpoint used
to redirect to the installer binary but now redirects to an HTML landing page
(isdl.php). The old script saved that HTML as the .exe and then failed at launch
with a cryptic "The file or directory is corrupted and unreadable" (a fresh AWS
agent hit exactly this). Every download is now VALIDATED (size + PE 'MZ' magic +
Authenticode) before we run it, so a non-binary response is rejected with a clear
message instead of a corrupt-exe error. download.php is kept only as a validated
last resort in case the pinned asset ever moves.

Inno Setup 6.x is API-compatible across minor versions, so ISCC will compile our
.iss regardless of which 6.x we land. Bump PinnedUrl when adopting a newer 6.x.

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

# Returns $true only if $path is a real, Inno-Setup-sized, PE executable. A
# non-binary response (HTML landing/error/rate-limit page) or a truncated
# download is rejected here — before we ever try to launch it.
function Test-InnoInstaller {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Warning "  validation: file missing after download"
        return $false
    }
    $len = (Get-Item -LiteralPath $Path).Length
    if ($len -lt 1MB) {
        Write-Warning "  validation: only $len bytes (expected a ~10 MB installer) -- likely an HTML/error page, not the binary"
        $firstLine = Get-Content -LiteralPath $Path -TotalCount 1 -ErrorAction SilentlyContinue
        if ($firstLine) { Write-Warning ("  first line: " + $firstLine.Substring(0, [Math]::Min(120, $firstLine.Length))) }
        return $false
    }
    # PE 'MZ' magic (0x4D 0x5A).
    $head = New-Object byte[] 2
    $fs = [System.IO.File]::OpenRead($Path)
    try { $n = $fs.Read($head, 0, 2) } finally { $fs.Dispose() }
    if ($n -lt 2 -or $head[0] -ne 0x4D -or $head[1] -ne 0x5A) {
        Write-Warning ("  validation: not a PE executable (first bytes {0:X2} {1:X2}, expected 4D 5A 'MZ')" -f $head[0], $head[1])
        return $false
    }
    # Authenticode: reject only clear tamper/unsigned; tolerate trust-chain gaps
    # on a bare agent (missing roots) since size + MZ already passed.
    try {
        $sig = Get-AuthenticodeSignature -LiteralPath $Path
        switch ($sig.Status) {
            'Valid'        { Write-Host   ("  validation: OK ({0:N1} MB, signed: {1})" -f ($len / 1MB), $sig.SignerCertificate.Subject.Split(',')[0]) }
            'NotSigned'    { Write-Warning "  validation: not Authenticode-signed -- rejecting"; return $false }
            'HashMismatch' { Write-Warning "  validation: Authenticode hash mismatch (corrupt/tampered) -- rejecting"; return $false }
            default        { Write-Warning "  validation: Authenticode status '$($sig.Status)' -- accepting (size+MZ ok; agent may lack the signing root)" }
        }
    } catch {
        Write-Warning "  validation: Authenticode check errored ($($_.Exception.Message)) -- accepting on size+MZ"
    }
    return $true
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

# Pinned official installer (GitHub releases), then the legacy redirect as a
# validated last resort. See .DESCRIPTION for why download.php is no longer primary.
$PinnedUrl = 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe'
$urls = @($PinnedUrl, 'https://jrsoftware.org/download.php/is.exe')
$exe = Join-Path $env:TEMP "innosetup-installer-$([Guid]::NewGuid().ToString('N')).exe"

$got = $false
:sources foreach ($url in $urls) {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        Write-Host "Downloading Inno Setup installer from $url (attempt $attempt/3) ..."
        Remove-Item -LiteralPath $exe -Force -ErrorAction SilentlyContinue
        try {
            Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing -MaximumRedirection 10 -TimeoutSec 180
        } catch {
            Write-Warning "  download error: $($_.Exception.Message)"
            Start-Sleep -Seconds (3 * $attempt)
            continue
        }
        if (Test-InnoInstaller -Path $exe) { $got = $true; break sources }
        Start-Sleep -Seconds (3 * $attempt)
    }
}

if (-not $got) {
    Remove-Item -LiteralPath $exe -Force -ErrorAction SilentlyContinue
    Write-Error ("Could not obtain a valid Inno Setup installer from any source:`n  " + ($urls -join "`n  ") +
                 "`nSee the validation warnings above (a small/HTML response means the source URL changed or egress is being redirected to a landing page).")
    exit 1
}

# Inno Setup's own installer is built with Inno Setup, so the standard silent
# flags apply: /VERYSILENT (no UI), /SUPPRESSMSGBOXES (no popups), /NORESTART
# (don't reboot), /CURRENTUSER (per-user install — no admin).
Write-Host "Running Inno Setup installer (/VERYSILENT /CURRENTUSER) ..."
& $exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER | Out-Host
$code = $LASTEXITCODE
if ($code -ne 0) {
    Write-Error "Inno Setup installer exited with code $code (installer kept at $exe for diagnosis)"
    exit 1
}
Remove-Item -LiteralPath $exe -Force -ErrorAction SilentlyContinue

$iscc = Find-Iscc
if ($iscc) {
    Write-Host "Inno Setup installed at $iscc"
    if ($PassThru) { $iscc }
    exit 0
}
Write-Error "Installer reported success but ISCC.exe is still not discoverable."
exit 1
