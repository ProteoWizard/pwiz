<#
.SYNOPSIS
    Builds Quasar.sln and runs the Quasar.Test suite.

.DESCRIPTION
    Self-contained entry point for local development (through build.bat). Builds with the
    .NET SDK (dotnet msbuild, dotnet test) on every platform: every Quasar project is plain
    net10.0. Does not depend on the sibling ai/ checkout.

.PARAMETER Configuration
    Debug or Release. Default Release.

.PARAMETER NoTests
    Build only.

.PARAMETER IAgreeToVendorLicenses
    Acknowledge the vendor SDK licenses, so pwiz-sharp builds its real vendor readers and
    Quasar can read raw files (as Osprey's build.ps1 does). Without it the vendor readers are
    stubs and only mzML can be read.

.PARAMETER NoBuild
    Run the tests against the existing build output without building first.

.PARAMETER TestFilter
    Optional dotnet test filter expression, for example "FullyQualifiedName~EmgTest".

.PARAMETER ShowTestOutput
    Print what the tests write to the console (dotnet test's detailed console logger).

.PARAMETER Verbosity
    MSBuild verbosity (quiet|minimal|normal|detailed|diagnostic). Default minimal.

.EXAMPLE
    .\build.bat
.EXAMPLE
    .\build.bat -Configuration Debug -NoTests
#>
param(
    [ValidateSet('Debug','Release')] [string]$Configuration = 'Release',
    [switch]$NoTests,
    [switch]$IAgreeToVendorLicenses,
    [switch]$NoBuild,
    [string]$TestFilter = '',
    [switch]$ShowTestOutput,
    [ValidateSet('quiet','minimal','normal','detailed','diagnostic')]
    [string]$Verbosity = 'minimal'
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptRoot = Split-Path -Parent $PSCommandPath
$sln = Join-Path $scriptRoot 'Quasar.sln'
$platform = 'x64'
if (-not (Test-Path $sln)) {
    Write-Error "Quasar.sln not found at $sln"
    exit 2
}

if (-not $NoBuild) {
    Write-Host "==> Building Quasar.sln ($Configuration|$platform)" -ForegroundColor Cyan
    $buildStart = Get-Date
    # NuGetAuditLevel=critical: pwiz-sharp's Sciex.csproj (reached through Osprey.IO and
    # ProteowizardWrapper) references System.Security.Cryptography.Xml 8.0.4 directly, which now
    # has published high-severity advisories, and pwiz-sharp's TreatWarningsAsErrors turns the
    # NU1903 audit warning into a RESTORE failure for every solution that references it. The fix
    # belongs in pwiz-sharp (a newer package); until then only critical advisories are reported.
    $buildArgs = @($sln, '/restore', "/p:Configuration=$Configuration", "/p:Platform=$platform", '/nologo',
        "/verbosity:$Verbosity", '/p:NuGetAuditLevel=critical')
    if ($IAgreeToVendorLicenses) {
        $buildArgs += '/p:IAgreeToVendorLicenses=true'
    }
    & dotnet msbuild @buildArgs
    $buildExit = $LASTEXITCODE
    $buildSec = ((Get-Date) - $buildStart).TotalSeconds
    if ($buildExit -ne 0) {
        Write-Host ("ERROR: build failed in {0:F1}s (exit {1})" -f $buildSec, $buildExit) -ForegroundColor Red
        exit $buildExit
    }
    Write-Host ("Build succeeded in {0:F1}s" -f $buildSec) -ForegroundColor Green
}

if ($NoTests) {
    Write-Host "Skipping tests (-NoTests)" -ForegroundColor Yellow
    exit 0
}

$trxDir = Join-Path $scriptRoot 'TestResults'
New-Item -ItemType Directory -Force -Path $trxDir | Out-Null
$trxName = "Quasar.Test-$Configuration-net10.0.trx"
$testProj = Join-Path $scriptRoot 'Quasar.Test/Quasar.Test.csproj'
$testArgs = @($testProj, '-c', $Configuration, "-p:Platform=$platform", '--no-build', '--nologo',
    '--logger', "trx;LogFileName=$trxName", '--results-directory', $trxDir)
if ($TestFilter) {
    $testArgs += @('--filter', $TestFilter)
}
if ($ShowTestOutput) {
    $testArgs += @('--logger', 'console;verbosity=detailed')
}

Write-Host "==> Running tests" -ForegroundColor Cyan
$testStart = Get-Date
& dotnet test @testArgs
$testExit = $LASTEXITCODE
$testSec = ((Get-Date) - $testStart).TotalSeconds

# A runner that never started writes no TRX; do not report that as a pass.
if ($testExit -eq 0 -and -not (Test-Path (Join-Path $trxDir $trxName))) {
    Write-Host "ERROR: tests reported success but wrote no TRX; the runner likely did not execute" -ForegroundColor Red
    $testExit = 2
}
if ($testExit -eq 0) {
    Write-Host ("Tests passed in {0:F1}s" -f $testSec) -ForegroundColor Green
} else {
    Write-Host ("ERROR: tests failed in {0:F1}s (exit {1})" -f $testSec, $testExit) -ForegroundColor Red
}
exit $testExit
