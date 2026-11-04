#requires -Version 7
<#
.SYNOPSIS
    Developer-run parity check: CarafeSharp against the Carafe 2.2.0 reference runs, at full size.

.DESCRIPTION
    The full-size comparisons with Carafe's own output are not part of any test pass. Each has a
    committed subset twin that runs on every commit (docs/04-testing.md, "Committed subsets"); the
    full-size versions read 1.5 GB of downloaded reference packages (5.6 GB extracted) and are
    run by a developer, after a change to what they compare, the way Osprey's comparison with
    the Rust version is (ai/scripts/Osprey/Compare/Compare-EndToEnd-Crossimpl.ps1). No TeamCity
    configuration runs them.

    The script:
      1. Builds CarafeSharp (Release, on the -Torch backend) unless -NoBuild, and stops if the
         build fails.
      2. Fetches the reference packages the comparisons read that are not there yet
         (build.ps1 -TestData Fetch: testdata.json's URL, size and SHA-256, then each package's
         MANIFEST.sha256), into <Downloads>/Perftests or CARAFESHARP_TESTDATA.
      3. Runs the comparisons, which are CarafeSharp.Test methods in a test category of their own
         (FullParity for Stellar, Astral for Astral), with every one required to run to a result.
      4. Reports each comparison: what it checks, its outcome and time, and the failure message,
         with the backend it ran on, and keeps each dataset's results as
         TestResults/CarafeParity-<Dataset>-<cpu|cuda>.trx.

    -Torch cuda builds the CUDA libtorch (build.ps1 -Torch cuda: a 4 GB NuGet runtime on first use,
    an NVIDIA GPU of compute capability 7.0 or later, driver 570 or later) and predicts and
    fine-tunes on the GPU. A machine without a usable GPU fails: build.ps1 checks the GPU and driver
    before building, and each comparison that predicts fails rather than falling back to the CPU
    (CARAFESHARP_REQUIRE_CUDA=1, TestData.ComparisonDeviceName). The tolerances are the CPU's, which
    no GPU result is expected to match bit for bit. On an Ada GPU the RT predictions exceed them (the
    prediction and predicted-library comparisons, Stellar and Astral), mostly from libtorch's default
    TF32 convolutions; the comparisons that do not predict compute the same values on both backends.
    See docs/04-testing.md, "Full-size parity", for the measured differences.

    Stellar, the default, is the eight comparisons on carafesharp-testfiles-v1 (and, for masking,
    carafesharp-export-v1), about 4 minutes after the fetch on an i9-14900 (5.5 minutes with a
    first fetch on a fast connection):

      TestPeptideFormsMatchCarafe        peptidoforms and precursor m/z
      TestFragmentMzMatchesCarafe        fragment m/z against Carafe's float32 values
      TestLibraryAssemblyMatchesCarafe   library rows assembled from Carafe's own predictions
      TestPredictedLibraryMatchesCarafe  predicted library (and .blib) end to end
      TestPredictionsMatchCarafe         pretrained and fine-tuned MS2 and RT predictions
      TestFineTuneMatchesCarafe          fine-tuning on Carafe's training tables
      TestFullFastaBuildsMatchCarafe     the 12 stage-1 entrapment FASTA builds
      TestMaskingAgreesWithCarafe        Osprey export masking against Carafe's training

    Astral is the same comparisons on the Astral run (4 tests, 4.8 GB download, 22 GB extracted,
    about 4 minutes on the CPU after the fetch, measured 2026-11-03).

    The exit code is 0 only when every comparison ran and passed.

.PARAMETER Dataset
    Stellar (default), Astral, or All (Stellar, then Astral).

.PARAMETER TestName
    Run only the comparisons whose name contains this text. Needs -Dataset Stellar or Astral.

.PARAMETER Torch
    libtorch backend, as build.ps1's -Torch: cpu (default) or cuda.

.PARAMETER NoBuild
    Use the existing Release build of the -Torch backend.

.EXAMPLE
    pwsh -File pwiz_tools/CarafeSharp/scripts/Compare-CarafeParity.ps1
.EXAMPLE
    pwsh -File pwiz_tools/CarafeSharp/scripts/Compare-CarafeParity.ps1 -Dataset All
.EXAMPLE
    pwsh -File pwiz_tools/CarafeSharp/scripts/Compare-CarafeParity.ps1 -Torch cuda
.EXAMPLE
    pwsh -File pwiz_tools/CarafeSharp/scripts/Compare-CarafeParity.ps1 -NoBuild -TestName FineTune
#>
param(
    [ValidateSet('Stellar', 'Astral', 'All')] [string]$Dataset = 'Stellar',
    [string]$TestName,
    [ValidateSet('cpu', 'cuda')] [string]$Torch = 'cpu',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$carafeRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$buildScript = Join-Path $carafeRoot 'build.ps1'

# The packages each dataset's comparisons read (TestData.cs): the Astral comparisons read the
# Astral package alone.
$datasets = [ordered]@{
    Stellar = @{ Category = 'FullParity'; Packages = 'testfiles,export' }
    Astral  = @{ Category = 'Astral'; Packages = 'astral' }
}
$selected = if ($Dataset -eq 'All') { @($datasets.Keys) } else { @($Dataset) }
# build.ps1 fails a run whose filter matches nothing, and most names exist in one dataset only.
if ($TestName -and $selected.Count -gt 1) {
    Write-Host 'OVERALL: FAIL - -TestName needs -Dataset Stellar or Astral, not All' -ForegroundColor Red
    exit 1
}

# ValidateSet ignores case, but the value names build.ps1's output folders and the trx.
$Torch = $Torch.ToLowerInvariant()
$resultsDir = Join-Path $carafeRoot 'TestResults'
$trxPath = Join-Path $resultsDir "CarafeSharp.Test-Release-$Torch.trx"
$backend = $Torch
# build.ps1 checks the GPU and driver itself and stops with the reason when one does not qualify.
if ($Torch -eq 'cuda' -and (Get-Command nvidia-smi -ErrorAction SilentlyContinue)) {
    $gpu = @(& nvidia-smi --query-gpu=name,driver_version --format=csv,noheader 2>$null) | Select-Object -First 1
    if ($gpu) {
        # nvidia-smi lists every GPU; libtorch uses the first one CUDA_VISIBLE_DEVICES leaves it.
        $visible = if ($env:CUDA_VISIBLE_DEVICES) { ", CUDA_VISIBLE_DEVICES=$env:CUDA_VISIBLE_DEVICES" } else { '' }
        $backend = "cuda ($($gpu.Trim())$visible)"
    }
}
$ns = @{ t = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010' }
$rows = @()
$problems = @()
$clock = [Diagnostics.Stopwatch]::StartNew()
# Build once, up front, and stop if it fails: a dataset run after a failed build would compare
# the binaries already on disk.
if (-not $NoBuild) {
    & pwsh -NoProfile -File $buildScript -NoTests -Torch $Torch
    if ($LASTEXITCODE -ne 0) {
        Write-Host ("OVERALL: FAIL - the build failed (build.ps1 exit {0})" -f $LASTEXITCODE) -ForegroundColor Red
        exit 1
    }
}
foreach ($name in $selected) {
    $spec = $datasets[$name]
    Write-Host ''
    Write-Host ("=== {0}: TestCategory={1}, packages {2}, libtorch {3} ===" -f $name, $spec.Category, $spec.Packages, $backend) -ForegroundColor Cyan
    $buildArgs = @('-NoProfile', '-File', $buildScript, '-NoBuild', '-Torch', $Torch, '-TestCategory', $spec.Category, '-RequireData',
        '-TestData', 'Fetch', '-TestDataPackage', $spec.Packages)
    if ($TestName) {
        $buildArgs += @('-TestName', $TestName)
    }
    # build.ps1 writes one trx per configuration and backend; each dataset's is kept under a name of its
    # own, removed first so a run that writes none leaves no earlier results under that name.
    $keptTrx = Join-Path $resultsDir "CarafeParity-$name-$Torch.trx"
    foreach ($path in @($trxPath, $keptTrx)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path
        }
    }
    & pwsh @buildArgs
    $exitCode = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $trxPath)) {
        $problems += "${name}: build.ps1 exited $exitCode with no test results (see its output above)"
        continue
    }
    Copy-Item -LiteralPath $trxPath -Destination $keptTrx -Force
    Write-Host "Results: $keptTrx"
    [xml]$trx = Get-Content -Raw -LiteralPath $trxPath
    foreach ($result in Select-Xml -Xml $trx -XPath '//t:UnitTestResult' -Namespace $ns | ForEach-Object { $_.Node }) {
        $message = (Select-Xml -Xml $result -XPath './/t:Message' -Namespace $ns | Select-Object -First 1).Node.InnerText
        $rows += [pscustomobject]@{
            Dataset  = $name
            Test     = $result.testName
            Outcome  = $result.outcome
            Duration = $(if ($result.duration) { [TimeSpan]::Parse($result.duration) } else { [TimeSpan]::Zero })
            Message  = $message
        }
    }
    if ($exitCode -ne 0) {
        $problems += "${name}: build.ps1 exited $exitCode"
    }
}

Write-Host ''
Write-Host ("=== CarafeSharp parity with Carafe 2.2.0, libtorch {0} ===" -f $backend) -ForegroundColor Cyan
foreach ($row in $rows | Sort-Object Dataset, Test) {
    $color = if ($row.Outcome -eq 'Passed') { 'Green' } else { 'Red' }
    Write-Host ("  {0,-8} {1,-4} {2,-40} {3,-12} {4,8:F1}s" -f $row.Dataset, $Torch, $row.Test, $row.Outcome, $row.Duration.TotalSeconds) -ForegroundColor $color
    if ($row.Outcome -ne 'Passed' -and $row.Message) {
        foreach ($line in @($row.Message -split "`r?`n" | Select-Object -First 5)) {
            Write-Host ("           {0}" -f $line) -ForegroundColor DarkYellow
        }
    }
}
$failed = @($rows | Where-Object { $_.Outcome -ne 'Passed' })
foreach ($row in $failed) {
    $problems += "$($row.Dataset) $($row.Test): $($row.Outcome)"
}
if ($rows.Count -eq 0 -and $problems.Count -eq 0) {
    $problems += 'no comparison ran'
}
Write-Host ("Wall: {0:hh\:mm\:ss}" -f $clock.Elapsed)
if ($problems.Count -gt 0) {
    $summary = @($problems | Select-Object -Unique) -join '; '
    Write-Host ("OVERALL: FAIL ({0}) - {1}" -f $Torch, $summary) -ForegroundColor Red
    exit 1
}
Write-Host ("OVERALL: PASS - {0} comparison(s) match Carafe 2.2.0 on libtorch {1}" -f $rows.Count, $Torch) -ForegroundColor Green
exit 0
