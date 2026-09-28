<#
.SYNOPSIS
    Build and test CarafeSharp on Windows or Linux, on the CPU or with CUDA.

.DESCRIPTION
    Builds CarafeSharp.sln with the .NET SDK and runs the CarafeSharp.Test suite. It is
    self-contained: it needs no sibling ai/ checkout, so the pwiz repo builds and tests
    CarafeSharp on its own. build.bat (Windows) and build.sh (Linux, which also installs
    the .NET SDK and PowerShell when missing) call it. ai/scripts/CarafeSharp/Build-CarafeSharp.ps1
    wraps it for LLM-assisted development and adds ReSharper inspection.

    The libtorch native runtime is chosen at build time with -Torch. The CPU build runs
    anywhere .NET 10 runs on x64 Windows or Linux. The CUDA build needs an NVIDIA GPU of
    compute capability 7.0 or later (Volta and newer: the libtorch 2.10 cu128 binaries carry
    kernels for sm_70, 75, 80, 86, 90, 100 and 120, and no PTX) and a driver of release 570 or
    later. No CUDA toolkit is needed; the NuGet packages carry the runtime, about 4 GB. Each
    backend builds into its own bin/obj folders (Directory.Build.props), so switching between
    them does not rebuild the other.

    Tests read their reference data from <Downloads>/Perftests (or %CARAFESHARP_TESTDATA%);
    see docs/04-testing.md. Without the data, data-dependent tests report Inconclusive.

.PARAMETER Configuration
    Debug or Release. Default Release.

.PARAMETER Torch
    libtorch backend: cpu (default) or cuda.

.PARAMETER NoTests
    Build only.

.PARAMETER NoBuild
    Run the tests against the existing build.

.PARAMETER TestName
    Run only tests whose fully qualified name contains this text.

.PARAMETER TestCategory
    Run only this test category (for example Astral or Cuda). By default the CPU build runs
    every test except the Cuda and Astral categories, and the CUDA build runs the Cuda
    category with a GPU required.

.PARAMETER RequireData
    Fail when any test did not run to a pass or fail (NotExecuted or Inconclusive), which is
    how a missing or incomplete test-data package shows up.

.PARAMETER TeamCity
    Emit TeamCity service messages and import the test results.

.PARAMETER Verbosity
    MSBuild verbosity (quiet|minimal|normal|detailed|diagnostic). Default minimal.

.EXAMPLE
    ./build.ps1                                # CPU build and tests
.EXAMPLE
    ./build.ps1 -Torch cuda                    # CUDA build and the Cuda test category
.EXAMPLE
    ./build.ps1 -NoBuild -TestCategory Astral  # the Astral parity tests on the existing build
#>
param(
    [ValidateSet('Debug', 'Release')] [string]$Configuration = 'Release',
    [ValidateSet('cpu', 'cuda')] [string]$Torch = 'cpu',
    [switch]$NoTests,
    [switch]$NoBuild,
    [string]$TestName,
    [string]$TestCategory,
    [switch]$RequireData,
    [switch]$TeamCity,
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'minimal'
)

$ErrorActionPreference = 'Stop'
# A failing native command must reach the outcome report below, not throw first.
$PSNativeCommandUseErrorActionPreference = $false
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
# ValidateSet ignores case, but the value names output folders, which Linux compares by case.
$Configuration = @('Debug', 'Release') | Where-Object { $_ -eq $Configuration }

$scriptRoot = Split-Path -Parent $PSCommandPath
$sln = Join-Path $scriptRoot 'CarafeSharp.sln'
$platform = 'x64'
$binFolder = if ($Torch -eq 'cuda') { 'bin-cuda' } else { 'bin' }
$testDll = Join-Path $scriptRoot "CarafeSharp.Test/$binFolder/$platform/$Configuration/net10.0/CarafeSharp.Test.dll"
$exeDir = Join-Path $scriptRoot "CarafeSharp/$binFolder/$platform/$Configuration/net10.0"

function Format-TcMessage([string]$s) {
    if ($null -eq $s) { return '' }
    return $s.Replace('|', '||').Replace("'", "|'").Replace("`n", '|n').Replace("`r", '|r').Replace('[', '|[').Replace(']', '|]')
}
function Write-Step([string]$msg) {
    if ($TeamCity) {
        Write-Host ("##teamcity[progressMessage '{0}']" -f (Format-TcMessage $msg))
    } else {
        Write-Host "==> $msg" -ForegroundColor Cyan
    }
}
function Stop-WithProblem([string]$msg, [int]$code = 1) {
    if ($TeamCity) {
        Write-Host ("##teamcity[buildProblem description='{0}']" -f (Format-TcMessage $msg))
    }
    Write-Host "ERROR: $msg" -ForegroundColor Red
    exit $code
}

# A CUDA build is a 4 GB download that cannot run on a GPU the binaries have no kernels for,
# so check the GPU and driver first and say which requirement fails.
function Test-CudaHardware {
    $smi = Get-Command nvidia-smi -ErrorAction SilentlyContinue
    if (-not $smi -and (Test-Path '/usr/lib/wsl/lib/nvidia-smi')) { $smi = '/usr/lib/wsl/lib/nvidia-smi' }
    if (-not $smi) {
        Stop-WithProblem 'The CUDA build needs an NVIDIA GPU and driver, but nvidia-smi was not found. Use -Torch cpu.'
    }
    # Standard error is left out of the rows parsed below.
    $rows = @(& $smi --query-gpu=name,compute_cap,driver_version,memory.total --format=csv,noheader 2>$null)
    if ($LASTEXITCODE -ne 0 -or $rows.Count -eq 0) {
        Stop-WithProblem 'nvidia-smi could not query a GPU. Check the NVIDIA driver, or use -Torch cpu.'
    }
    # libtorch runs on the first CUDA device, so that is the one that must qualify. CUDA orders
    # devices fastest first and nvidia-smi by bus, so with several GPUs each one is reported and
    # the first listed is checked; set CUDA_VISIBLE_DEVICES to pick another.
    $invariant = [Globalization.CultureInfo]::InvariantCulture
    for ($i = 0; $i -lt $rows.Count; $i++) {
        # The name comes first and may itself contain commas, so read the fields from the end.
        $fields = @("$($rows[$i])".Split(',') | ForEach-Object { $_.Trim() })
        $n = $fields.Count
        $memory = if ($n -ge 4) { $fields[$n - 1] } else { '' }
        $driver = if ($n -ge 4) { $fields[$n - 2] } else { '' }
        $cap = if ($n -ge 4) { $fields[$n - 3] } else { '' }
        $name = if ($n -ge 4) { $fields[0..($n - 4)] -join ',' } else { "$($rows[$i])" }
        Write-Host ("GPU {0}: {1}, compute capability {2}, driver {3}, {4}" -f $i, $name, $cap, $driver, $memory)
        if ($i -gt 0) {
            continue
        }
        $capValue = 0.0
        $driverMajor = 0
        if (-not [double]::TryParse($cap, [Globalization.NumberStyles]::Float, $invariant, [ref]$capValue)) {
            Stop-WithProblem "nvidia-smi reported no compute capability for $name ('$cap'). Use -Torch cpu."
        }
        if (-not [int]::TryParse(($driver -split '\.')[0], [Globalization.NumberStyles]::Integer, $invariant, [ref]$driverMajor)) {
            Stop-WithProblem "nvidia-smi reported no driver version for $name ('$driver')."
        }
        if ($capValue -lt 7.0) {
            Stop-WithProblem "$name has compute capability $cap, below 7.0: the CUDA 12.8 libtorch has no kernels for it (Maxwell and Pascal). Use -Torch cpu."
        }
        if ($driverMajor -lt 570) {
            Stop-WithProblem "Driver $driver is older than release 570, which CUDA 12.8 needs. Update the driver, or use -Torch cpu."
        }
    }
}

if (-not (Test-Path -LiteralPath $sln)) {
    Stop-WithProblem "CarafeSharp.sln not found at $sln" 2
}
if ($Torch -eq 'cuda') {
    Test-CudaHardware
}

if (-not $NoBuild) {
    Write-Step "Building CarafeSharp.sln ($Configuration|$platform, libtorch $Torch)"
    $buildStart = Get-Date
    & dotnet build $sln -c $Configuration "-p:Platform=$platform" "-p:CarafeSharpTorch=$Torch" "-v:$Verbosity" -nologo
    if ($LASTEXITCODE -ne 0) {
        Stop-WithProblem "Build failed (exit $LASTEXITCODE)" $LASTEXITCODE
    }
    Write-Host ("Build succeeded in {0:F1}s" -f ((Get-Date) - $buildStart).TotalSeconds) -ForegroundColor Green
    if ($Torch -eq 'cuda') {
        $cudaLib = if ($IsWindows) { 'runtimes/win-x64/native/torch_cuda.dll' } else { 'runtimes/linux-x64/native/libtorch_cuda.so' }
        if (-not (Test-Path -LiteralPath (Join-Path $exeDir $cudaLib))) {
            Stop-WithProblem "The CUDA build did not deliver $cudaLib to $exeDir"
        }
    }
}

if ($NoTests) {
    exit 0
}
if (-not (Test-Path -LiteralPath $testDll)) {
    Stop-WithProblem "Test assembly not found: $testDll (build first, without -NoBuild)"
}

# A named test runs whatever its category. Otherwise CPU builds leave out the GPU and Astral
# categories, and CUDA builds run the GPU category.
$filters = @()
if ($TestCategory) {
    $filters += "TestCategory=$TestCategory"
} elseif ($TestName) {
    # No category filter, so a named Astral or Cuda test is not filtered away.
} elseif ($Torch -eq 'cuda') {
    $filters += 'TestCategory=Cuda'
} else {
    $filters += 'TestCategory!=Cuda', 'TestCategory!=Astral'
}
if ($TestName) {
    $filters += "FullyQualifiedName~$TestName"
}

$resultsDir = Join-Path $scriptRoot 'TestResults'
$trxName = "CarafeSharp.Test-$Configuration-$Torch.trx"
$trxPath = Join-Path $resultsDir $trxName
if (Test-Path -LiteralPath $trxPath) { Remove-Item -LiteralPath $trxPath }
Write-Step ("Running tests ({0})" -f ($filters -join ' & '))
# A GPU test that quietly fell back to the CPU would pass without testing anything, so a CUDA
# build's tests must find a GPU. Set for the test run only, not left in the caller's session.
$requireCuda = $env:CARAFESHARP_REQUIRE_CUDA
if ($Torch -eq 'cuda' -or $TestCategory -eq 'Cuda') {
    $env:CARAFESHARP_REQUIRE_CUDA = '1'
}
try {
    & dotnet test $testDll --nologo --filter ($filters -join '&') --results-directory $resultsDir `
        --logger "trx;LogFileName=$trxName" --logger 'console;verbosity=normal'
    $testExit = $LASTEXITCODE
} finally {
    $env:CARAFESHARP_REQUIRE_CUDA = $requireCuda
}
if ($TeamCity -and (Test-Path -LiteralPath $trxPath)) {
    Write-Host ("##teamcity[importData type='vstest' path='{0}']" -f (Format-TcMessage $trxPath))
}
if (-not (Test-Path -LiteralPath $trxPath)) {
    Stop-WithProblem "No test results were written ($trxPath)" $(if ($testExit -ne 0) { $testExit } else { 1 })
}

# dotnet test exits 0 when tests are Inconclusive, which is also what a missing data package
# looks like, so count the outcomes from the results file.
[xml]$trx = Get-Content -Raw -LiteralPath $trxPath
$ns = @{ t = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010' }
$results = Select-Xml -Xml $trx -XPath '//t:UnitTestResult' -Namespace $ns | ForEach-Object { $_.Node }
if (@($results).Count -eq 0) {
    Stop-WithProblem ("No test matched the filter ({0}); results in {1}" -f ($filters -join ' & '), $trxPath)
}
$byOutcome = $results | Group-Object outcome | Sort-Object Name
Write-Host ('Test outcomes: ' + (($byOutcome | ForEach-Object { '{0} {1}' -f $_.Name, $_.Count }) -join ', '))
$notRun = @($results | Where-Object { $_.outcome -in @('NotExecuted', 'Inconclusive') })
foreach ($r in $notRun) {
    $message = (Select-Xml -Xml $r -XPath './/t:Message' -Namespace $ns | Select-Object -First 1).Node.InnerText
    Write-Host ("  {0} {1}: {2}" -f $r.outcome, $r.testName, $message) -ForegroundColor Yellow
}
if ($testExit -ne 0) {
    Stop-WithProblem "Tests failed (exit $testExit); results in $trxPath" $testExit
}
if ($RequireData -and $notRun.Count -gt 0) {
    Stop-WithProblem ("{0} test(s) did not run to a result; -RequireData expects every test to run. Results in {1}" -f $notRun.Count, $trxPath)
}
Write-Host 'All tests passed' -ForegroundColor Green
exit 0
