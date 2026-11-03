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
      1. Fetches the reference packages the comparisons read that are not there yet
         (build.ps1 -TestData Fetch: testdata.json's URL, size and SHA-256, then each package's
         MANIFEST.sha256), into <Downloads>/Perftests or CARAFESHARP_TESTDATA.
      2. Builds CarafeSharp (Release, CPU) unless -NoBuild.
      3. Runs the comparisons, which are CarafeSharp.Test methods in a test category of their own
         (FullParity for Stellar, Astral for Astral), with every one required to run to a result.
      4. Reports each comparison: what it checks, its outcome and time, and the failure message.

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
    about 12 minutes).

    The exit code is 0 only when every comparison ran and passed.

.PARAMETER Dataset
    Stellar (default), Astral, or All (Stellar, then Astral).

.PARAMETER TestName
    Run only the comparisons whose name contains this text.

.PARAMETER NoBuild
    Use the existing Release CPU build.

.EXAMPLE
    pwsh -File pwiz_tools/CarafeSharp/scripts/Compare-CarafeParity.ps1
.EXAMPLE
    pwsh -File pwiz_tools/CarafeSharp/scripts/Compare-CarafeParity.ps1 -Dataset All
.EXAMPLE
    pwsh -File pwiz_tools/CarafeSharp/scripts/Compare-CarafeParity.ps1 -NoBuild -TestName FineTune
#>
param(
    [ValidateSet('Stellar', 'Astral', 'All')] [string]$Dataset = 'Stellar',
    [string]$TestName,
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

$trxPath = Join-Path (Join-Path $carafeRoot 'TestResults') 'CarafeSharp.Test-Release-cpu.trx'
$ns = @{ t = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010' }
$rows = @()
$problems = @()
$clock = [Diagnostics.Stopwatch]::StartNew()
$built = $NoBuild
foreach ($name in $selected) {
    $spec = $datasets[$name]
    Write-Host ''
    Write-Host ("=== {0}: TestCategory={1}, packages {2} ===" -f $name, $spec.Category, $spec.Packages) -ForegroundColor Cyan
    $buildArgs = @('-NoProfile', '-File', $buildScript, '-TestCategory', $spec.Category, '-RequireData',
        '-TestData', 'Fetch', '-TestDataPackage', $spec.Packages)
    if ($built) {
        $buildArgs += '-NoBuild'
    }
    if ($TestName) {
        $buildArgs += @('-TestName', $TestName)
    }
    if (Test-Path -LiteralPath $trxPath) {
        Remove-Item -LiteralPath $trxPath
    }
    & pwsh @buildArgs
    $exitCode = $LASTEXITCODE
    $built = $true
    if (-not (Test-Path -LiteralPath $trxPath)) {
        $problems += "${name}: build.ps1 exited $exitCode with no test results (see its output above)"
        continue
    }
    [xml]$trx = Get-Content -Raw -LiteralPath $trxPath
    foreach ($result in Select-Xml -Xml $trx -XPath '//t:UnitTestResult' -Namespace $ns | ForEach-Object { $_.Node }) {
        $message = (Select-Xml -Xml $result -XPath './/t:Message' -Namespace $ns | Select-Object -First 1).Node.InnerText
        $rows += [pscustomobject]@{
            Dataset  = $name
            Test     = $result.testName
            Outcome  = $result.outcome
            Duration = [TimeSpan]::Parse($result.duration)
            Message  = $message
        }
    }
    if ($exitCode -ne 0) {
        $problems += "${name}: build.ps1 exited $exitCode"
    }
}

Write-Host ''
Write-Host '=== CarafeSharp parity with Carafe 2.2.0 ===' -ForegroundColor Cyan
foreach ($row in $rows | Sort-Object Dataset, Test) {
    $color = if ($row.Outcome -eq 'Passed') { 'Green' } else { 'Red' }
    Write-Host ("  {0,-8} {1,-40} {2,-12} {3,8:F1}s" -f $row.Dataset, $row.Test, $row.Outcome, $row.Duration.TotalSeconds) -ForegroundColor $color
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
    Write-Host ("OVERALL: FAIL - {0}" -f $summary) -ForegroundColor Red
    exit 1
}
Write-Host ("OVERALL: PASS - {0} comparison(s) match Carafe 2.2.0" -f $rows.Count) -ForegroundColor Green
exit 0
