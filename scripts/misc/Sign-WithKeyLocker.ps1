<#
.SYNOPSIS
Authenticode-signs files with the MacCoss Lab certificate held in DigiCert KeyLocker.

.DESCRIPTION
Signs with signtool through the "DigiCert Signing Manager KSP", the same way
pwiz_tools/Skyline/SignAfterPublish.bat does, then checks that each file has a valid,
timestamped signature. The private key never leaves KeyLocker: the KSP reaches it with
the KeyLocker credentials of the account the script runs as (the SM_* environment
variables and the Windows Credential Manager entry that the KeyLocker tools save). The
signing machine needs the DigiCert KeyLocker tools (smtools) installed, with the KSP
registered, and the Windows SDK's signtool.

Every file signed uses one signature from the KeyLocker allocation.

Build logs are readable by guests, so the diagnostics report which credentials are
present, never their values.

.PARAMETER Path
Files to sign, or directories whose *.exe and *.msi files are signed.

.PARAMETER KeyAlias
The KeyLocker key alias (signtool /kc).

.PARAMETER CertFile
The public certificate of that key (.crt, signtool /f).

.PARAMETER TimestampUrl
RFC 3161 timestamp server. The timestamp keeps a signature valid after the certificate
expires.

.PARAMETER SignTool
Path to signtool.exe. By default it is found on PATH or in the newest Windows SDK.

.PARAMETER AllowEmpty
Succeed when there is nothing to sign, instead of failing.
#>
param(
    [Parameter(Mandatory = $true)] [string[]] $Path,
    [Parameter(Mandatory = $true)] [string] $KeyAlias,
    [Parameter(Mandatory = $true)] [string] $CertFile,
    [string] $TimestampUrl = 'http://timestamp.digicert.com',
    [string] $SignTool,
    [switch] $AllowEmpty
)

$ErrorActionPreference = 'Stop'
$KspName = 'DigiCert Signing Manager KSP'

function Write-BuildProblem([string] $message) {
    $escaped = $message -replace '\|', '||' -replace "'", "|'" -replace '\[', '|[' -replace '\]', '|]' -replace "`r", '|r' -replace "`n", '|n'
    Write-Host "##teamcity[buildProblem description='$escaped']"
    exit 1
}

function Find-SignTool {
    $onPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    $kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    if (Test-Path $kits) {
        $x64 = @(Get-ChildItem -Path $kits -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Directory.Name -eq 'x64' })
        # Newest versioned SDK (bin\10.0.x.y\x64), else the unversioned bin\x64 of older SDKs
        $found = $x64 | Where-Object { $_.Directory.Parent.Name -match '^\d+(\.\d+)+$' } |
            Sort-Object { [version]$_.Directory.Parent.Name } | Select-Object -Last 1
        if (-not $found) { $found = $x64 | Select-Object -First 1 }
        if ($found) { return $found.FullName }
    }
    return $null
}

# What to sign
$files = @()
foreach ($item in $Path) {
    if (Test-Path -LiteralPath $item -PathType Container) {
        $files += Get-ChildItem -LiteralPath $item -File | Where-Object { $_.Extension -in '.exe', '.msi' } | ForEach-Object { $_.FullName }
    } elseif (Test-Path -LiteralPath $item -PathType Leaf) {
        $files += (Resolve-Path -LiteralPath $item).Path
    } else {
        Write-BuildProblem "Nothing to sign at $item"
    }
}
if ($files.Count -eq 0) {
    if ($AllowEmpty) { Write-Host 'Nothing to sign.'; exit 0 }
    Write-BuildProblem "No .exe or .msi files to sign in $($Path -join ', ')"
}

# Diagnostics, without secrets
Write-Host "Signing as $([Environment]::UserDomainName)\$([Environment]::UserName) on $([Environment]::MachineName)"
foreach ($name in 'SM_HOST', 'SM_API_KEY', 'SM_CLIENT_CERT_FILE', 'SM_CLIENT_CERT_PASSWORD') {
    $value = [Environment]::GetEnvironmentVariable($name)
    $state = if ($value) { 'set' } else { 'not set' }
    if ($name -eq 'SM_CLIENT_CERT_FILE' -and $value) {
        $state += if (Test-Path -LiteralPath $value) { ' (file exists)' } else { ' (FILE MISSING)' }
    }
    Write-Host "  $name`: $state"
}
$csps = & certutil -csplist 2>$null | Out-String
if ($csps -notmatch [regex]::Escape($KspName)) {
    Write-BuildProblem "The '$KspName' is not registered on $([Environment]::MachineName); install the DigiCert KeyLocker tools and run smksp_registrar.exe register"
}
Write-Host "  $KspName`: registered"
if (-not (Test-Path -LiteralPath $CertFile)) {
    Write-BuildProblem "The signing certificate $CertFile does not exist on $([Environment]::MachineName)"
}
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $CertFile
Write-Host "  Certificate: $($cert.Subject), thumbprint $($cert.Thumbprint), expires $($cert.NotAfter.ToString('yyyy-MM-dd'))"
if ($cert.NotAfter -lt (Get-Date)) {
    Write-BuildProblem "The signing certificate $CertFile expired on $($cert.NotAfter.ToString('yyyy-MM-dd'))"
}
if (-not $SignTool) { $SignTool = Find-SignTool }
if (-not $SignTool -or -not (Test-Path -LiteralPath $SignTool)) {
    Write-BuildProblem 'signtool.exe was not found; install the Windows SDK signing tools or pass -SignTool'
}
Write-Host "  signtool: $SignTool"

# Sign
foreach ($file in $files) {
    Write-Host "Signing $file"
    & $SignTool sign /csp $KspName /kc $KeyAlias /f $CertFile /tr $TimestampUrl /td SHA256 /fd SHA256 $file
    if ($LASTEXITCODE -ne 0) {
        Write-BuildProblem "signtool failed (exit $LASTEXITCODE) signing $(Split-Path -Leaf $file)"
    }
}

# Verify: valid, by our certificate, and timestamped
foreach ($file in $files) {
    $signature = Get-AuthenticodeSignature -LiteralPath $file
    $leaf = Split-Path -Leaf $file
    if ($signature.Status -ne 'Valid') {
        Write-BuildProblem "$leaf has signature status $($signature.Status): $($signature.StatusMessage)"
    }
    if ($signature.SignerCertificate.Thumbprint -ne $cert.Thumbprint) {
        Write-BuildProblem "$leaf is signed by $($signature.SignerCertificate.Subject), not by $($cert.Subject)"
    }
    if (-not $signature.TimeStamperCertificate) {
        Write-BuildProblem "$leaf is signed but not timestamped"
    }
    Write-Host "Verified $leaf`: signed by $($signature.SignerCertificate.Subject), timestamped by $($signature.TimeStamperCertificate.Subject)"
}
Write-Host "Signed $($files.Count) file(s), using $($files.Count) KeyLocker signature(s)."
exit 0
