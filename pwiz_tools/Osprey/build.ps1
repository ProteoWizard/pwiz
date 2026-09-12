<#
.SYNOPSIS
    Build + test Osprey.  Self-contained entry point for local
    dev (via build.bat) and CI (via tcbuild.bat).

.DESCRIPTION
    Drives the build of Osprey.sln, then runs the Osprey.Test
    suite via vstest.console.exe.  Optional dotCover wrap for coverage
    measurement, optional TeamCity service messages for CI reporting.

    Does NOT depend on the sibling ai/ checkout -- the pwiz repo can
    be built and tested standalone, which is what CI needs.  A
    superset script aimed at LLM-driven dev workflows (with line-
    ending fixes, ReSharper inspection, dataset-aware test runs)
    lives at ai/scripts/Osprey/Build-Osprey.ps1.

.PARAMETER Configuration
    Debug or Release.  Default Release (CI canonical).

.PARAMETER NoTests
    Build only.

.PARAMETER Coverage
    Wrap test execution in JetBrains dotCover.  Writes .dcvr
    coverage data under TestResults/.  dotCover is restored from
    this directory's .config/dotnet-tools.json, so no global
    install and no agent provisioning is needed.

.PARAMETER TeamCity
    Emit TeamCity service messages: progress lines during the
    build, vstest TRX import after tests, dotCover .dcvr import
    after coverage, and a buildProblem line on any failure.
    The agent's TeamCity runner consumes these automatically.

.PARAMETER Verbosity
    MSBuild verbosity (quiet|minimal|normal|detailed|diagnostic).
    Default minimal.

.EXAMPLE
    # Local dev
    .\build.bat

.EXAMPLE
    # Local dev: build only
    .\build.bat -NoTests

.EXAMPLE
    # TeamCity (what tcbuild.bat invokes)
    .\build.ps1 -TeamCity -Coverage -Configuration Release
#>
param(
    [ValidateSet('Debug','Release')] [string]$Configuration = 'Release',
    [switch]$NoTests,
    # Acknowledge the vendor SDK EULAs. pwiz-sharp gates its real vendor readers on
    # this, so without it Osprey builds against the no-vendor-support stubs.
    [switch]$IAgreeToVendorLicenses,
    [switch]$Coverage,
    [switch]$TeamCity,
    [ValidateSet('quiet','minimal','normal','detailed','diagnostic')]
    [string]$Verbosity = 'minimal'
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptRoot = Split-Path -Parent $PSCommandPath
$sln        = Join-Path $scriptRoot 'Osprey.sln'
$platform   = 'x64'
if (-not (Test-Path $sln)) {
    Write-Error "Osprey.sln not found at $sln"
    exit 2
}

# --- TeamCity service-message helpers -----------------------------------
function Format-TcMessage([string]$s) {
    # https://www.jetbrains.com/help/teamcity/service-messages.html#Escaped+Values
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
function Write-Problem-Tc([string]$msg) {
    if ($TeamCity) {
        Write-Host ("##teamcity[buildProblem description='{0}']" -f (Format-TcMessage $msg))
    }
    Write-Host "ERROR: $msg" -ForegroundColor Red
}

# --- Tool discovery -----------------------------------------------------
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    Write-Problem-Tc "vswhere.exe not found (install Visual Studio Installer)"
    exit 2
}
$vsInstall = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationPath
if (-not $vsInstall) {
    Write-Problem-Tc "vswhere found no VS installation with MSBuild component"
    exit 2
}
$msbuild = Join-Path $vsInstall 'MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msbuild)) {
    Write-Problem-Tc "MSBuild not found at $msbuild"
    exit 2
}
$vstest = $null
if (-not $NoTests) {
    $candidates = @(
        (Join-Path $vsInstall 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe'),
        (Join-Path $vsInstall 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe')
    )
    foreach ($c in $candidates) { if (Test-Path $c) { $vstest = $c; break } }
    if (-not $vstest) {
        Write-Problem-Tc "vstest.console.exe not found under $vsInstall"
        exit 2
    }
}
$dotcover = $null
if ($Coverage) {
    # Restore dotCover from this directory's tool manifest rather than requiring a global
    # install. The previous check demanded `dotcover` on PATH, which held only on the
    # long-lived agent that happened to have it: every ephemeral CI agent failed here, and the
    # package name the message suggested (JetBrains.dotCover.GlobalTools) does not exist.
    #
    # The script returns the resolved dotCover.exe. `dotnet dotcover` is not usable here: a
    # local tool is only on the command line when the working directory is at or under its
    # manifest, and TeamCity runs this build from the repo root.
    $ensure = Join-Path $PSScriptRoot '../../pwiz-sharp/scripts/Ensure-DotCover.ps1'
    $dotcover = if ($TeamCity) { & $ensure -ManifestDir $PSScriptRoot -TeamCity } else { & $ensure -ManifestDir $PSScriptRoot }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $dotcover = @($dotcover)[-1]
}

# --- Pre-build cleanup ---------------------------------------------------
# The old pwiz Jamfile (Jamfile.jam before this PR) wrote
# Properties/AssemblyInfo.cs into every Osprey project as a bjam
# side-effect.  Those files are .gitignored / untracked, so on a CI agent
# that reuses its C:\pwiz checkout across configs they survive `git
# checkout` of any PR branch -- and SDK auto-gen on our build then
# conflicts with them and fails with CS0579 (see builds #4029769,
# #4030329).  The Jamfile in this PR no longer writes these files;
# delete any residual ones so the build is reproducible on a shared
# agent until master picks up the fix.
$staleAsmInfo = Get-ChildItem -Path $scriptRoot -Filter AssemblyInfo.cs -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\Properties\\AssemblyInfo\.cs$' -and $_.FullName -notmatch '\\(obj|bin)\\' }
foreach ($f in $staleAsmInfo) {
    Write-Host "Removing stale $($f.FullName)" -ForegroundColor Yellow
    Remove-Item $f.FullName -Force
}

# --- Version (Skyline scheme YEAR.ORDINAL.BRANCH.DOY) -------------------
# Mirrors pwiz_tools/Skyline/Jamfile.jam so a standalone dev/CI build stamps
# the same versioning the Boost build does (rather than the Directory.Build.props
# placeholder). The stamped version becomes OspreyVersion.Current at runtime. The
# regression harness pins OSPREY_VERSION_OVERRIDE on top of this for bit parity.
# The formula lives in version.ps1 so build.ps1 (stamps the binary) and
# package.ps1 (names the redistributable) can never disagree.
. (Join-Path $scriptRoot 'version.ps1')
$ospreyVersion = Get-OspreyVersion -RepoPath $scriptRoot

# --- Build --------------------------------------------------------------
Write-Progress-Tc "Building Osprey.sln ($Configuration|$platform) v$ospreyVersion"
$buildStart = Get-Date
$buildArgs = @(
    $sln,
    '/restore',
    "/p:Configuration=$Configuration",
    "/p:Platform=$platform",
    "/p:Version=$ospreyVersion",
    '/nologo',
    "/verbosity:$Verbosity"
)
if ($IAgreeToVendorLicenses) {
    $buildArgs += '/p:IAgreeToVendorLicenses=true'
}
& $msbuild @buildArgs
$buildExit = $LASTEXITCODE
$buildSec = ((Get-Date) - $buildStart).TotalSeconds
if ($buildExit -ne 0) {
    Write-Problem-Tc ("MSBuild failed in {0:F1}s (exit {1})" -f $buildSec, $buildExit)
    exit $buildExit
}
Write-Host ("Build succeeded in {0:F1}s" -f $buildSec) -ForegroundColor Green

if ($NoTests) {
    Write-Host "Skipping tests (-NoTests)" -ForegroundColor Yellow
    exit 0
}

# --- Test ---------------------------------------------------------------
# Osprey is net10.0 only (issue #4497), so there is one test assembly. Kept as a
# list rather than collapsed inline: the loop below is the same shape either way,
# and a future second TFM would be one entry rather than a restructure.
$testFrameworks = @('net10.0')
$trxDir = Join-Path $scriptRoot 'TestResults'
New-Item -ItemType Directory -Force -Path $trxDir | Out-Null

$overallTestExit = 0
foreach ($fw in $testFrameworks) {
    $testDll = Join-Path $scriptRoot "Osprey.Test\bin\$platform\$Configuration\$fw\Osprey.Test.dll"
    if (-not (Test-Path $testDll)) {
        Write-Problem-Tc "Test DLL not found at $testDll"
        $overallTestExit = 2
        continue
    }

    $trxName = "Osprey.Test-$Configuration-$fw.trx"
    $trxPath = Join-Path $trxDir $trxName
    $vstestArgs = @(
        $testDll,
        "/Platform:$platform",
        "/Logger:trx;LogFileName=$trxName",
        "/ResultsDirectory:$trxDir"
    )

    $testStart = Get-Date
    if ($Coverage) {
        $dcvrPath = Join-Path $trxDir "Osprey.Test-$Configuration-$fw.dcvr"
        Write-Progress-Tc "Running tests under dotCover ($fw)"
        # /PascalCase=value is the console runner syntax through 2024.3.  The
        # 2026.1 rewrite replaced it with kebab-case flags and also dropped both
        # the HTML report type and the native dotCover.exe launcher that
        # TeamCity's agent-side coverage processor needs to turn a .dcvr into the
        # Code Coverage tab, so all three apps pin 2023.3.3 (see
        # Ensure-DotCover.ps1) and this call uses the syntax that runner speaks.
        #
        # https://www.jetbrains.com/help/dotcover/dotCover__Console_Runner_Commands.html
        function Quote-IfNeeded([string]$s) {
            if ($s -match '\s') { return '"' + $s + '"' }
            return $s
        }
        # Cover everything the test run loads except the test assembly itself and
        # the third-party dependencies, which leaves the Osprey.* assemblies we
        # care about.  Stated as exclusions rather than a +:module=Osprey*
        # allowlist so the shared pwiz.CommonUtil code Osprey builds against is
        # measured too.
        $excludeAssemblies = @(
            'Osprey.Test',
            'Apache.Arrow', 'ProDotNetZip', 'IronCompress',
            'JetBrains.*', 'MathNet.*', 'Microsoft.*', 'Newtonsoft.*',
            'Parquet', 'Snappier', 'System.*', 'ZstdSharp',
            'MSTest.*', 'testhost*', 'vstest.*'
        )
        $filters = ($excludeAssemblies | ForEach-Object { "-:module=$_" }) -join ';'
        $dcArgs = @(
            'cover',
            "/TargetExecutable=$(Quote-IfNeeded $vstest)",
            "/Output=$(Quote-IfNeeded $dcvrPath)",
            "/Filters=$(Quote-IfNeeded $filters)",
            '/AttributeFilters=System.CodeDom.Compiler.GeneratedCodeAttribute',
            '/ReturnTargetExitCode',
            '--'
        ) + ($vstestArgs | ForEach-Object { Quote-IfNeeded $_ })
        $dcArgString = $dcArgs -join ' '
        Write-Host "DC ARGS: $dcArgString"
        # Resolved from the local tool manifest, so the version is the pinned one and the call
        # does not depend on the working directory.
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $dotcover
        $psi.Arguments = $dcArgString
        $psi.UseShellExecute = $false
        $proc = [System.Diagnostics.Process]::Start($psi)
        $proc.WaitForExit()
        $exit = $proc.ExitCode
        if ($TeamCity -and (Test-Path $dcvrPath)) {
            Write-Host ("##teamcity[importData type='dotNetCoverage' tool='dotcover' path='{0}']" -f (Format-TcMessage $dcvrPath))
        }
    } else {
        Write-Progress-Tc "Running tests ($fw)"
        & $vstest @vstestArgs
        $exit = $LASTEXITCODE
    }
    $testSec = ((Get-Date) - $testStart).TotalSeconds

    if ($TeamCity -and (Test-Path $trxPath)) {
        Write-Host ("##teamcity[importData type='vstest' path='{0}']" -f (Format-TcMessage $trxPath))
    }
    # Guard against silent runner failures: vstest didn't actually execute if no
    # TRX was produced.  Has happened with dotCover printing help-on-bad-command
    # and exiting 0 (see build #4030001).
    if ($exit -eq 0 -and -not (Test-Path $trxPath)) {
        Write-Problem-Tc ("Tests ($fw) reported success but no TRX at $trxPath -- runner likely didn't execute")
        $exit = 2
    }
    if ($exit -eq 0) {
        Write-Host ("Tests ($fw) passed in {0:F1}s" -f $testSec) -ForegroundColor Green
    } else {
        Write-Problem-Tc ("Tests ($fw) FAILED in {0:F1}s (exit {1})" -f $testSec, $exit)
        $overallTestExit = $exit
    }
}

# --- TeamCity artifacts: intentionally none ---------------------------------
# This config publishes NO downloadable artifacts. The test + coverage results
# are already surfaced via the importData service messages above (Test /
# Coverage tabs); there is no Osprey install story yet, so the built
# binaries are not worth publishing. Critically, publishing
# pwiz_tools/Osprey/TestResults here was fragile: agents reuse one C:\pwiz
# checkout across the Osprey configs, so a sibling config's run (e.g. the
# overnight regression) can leave a multi-GB .spectra.bin in that shared
# TestResults, and the publish would then fail the 4 GB per-artifact limit. With
# nothing published, large scratch files under TestResults are a non-issue.

exit $overallTestExit
