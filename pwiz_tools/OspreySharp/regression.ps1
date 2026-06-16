<#
.SYNOPSIS
    OspreySharp overnight end-to-end regression. Self-contained entry point for
    the scheduled TeamCity "Osprey Windows .NET Regression" config (via
    tctest.bat) and for local developer runs.

.DESCRIPTION
    Acquires real DIA test data the way Skyline perf tests do (download a
    panorama zip into the shared <Downloads>\Perftests folder, extract,
    skip-if-present), then runs the full OspreySharp pipeline on each dataset
    with ZERO input copies -- inputs are referenced in place (read-only) and all
    derived artifacts + caches go to a per-run timestamped run dir under
    TestResults via --work-dir (gitignored scratch; nothing is published as a
    TeamCity artifact). Two complementary correctness legs:

      mode 1  straight-through vs a committed text golden (osprey-regression.data)
              -- the user-facing correctness gate. Compares the Stage 7 protein
              FDR dump + a deterministic precursor subset + full-set summary at
              1e-9. Refresh the golden with -CreateGolden on an intentional,
              reviewed behavior change.
      mode 2  resume vs straight-through self-consistency -- re-runs the build in
              resume mode (invalidate the Stage 5 join + blib, re-run the same
              command so the rehydrate paths fire) and asserts the resume blib
              equals the straight-through blib at 1e-9. The build is its own
              oracle, so no baseline is needed.

    NO dependency on the sibling ai/ checkout: data acquisition, blib golden
    capture/compare, and the tolerance comparators all live under
    pwiz_tools/OspreySharp/Regression. Mirrors build.ps1's TeamCity service
    messages and tool-discovery; emits a buildProblem + nonzero exit on any
    mismatch.

.PARAMETER Dataset
    Stellar, Astral, or All (default). Stellar is unit-resolution + fast; Astral
    is hram + larger.

.PARAMETER CreateGolden
    Capture/refresh the committed golden from this run instead of comparing
    against it. Use only on an intentional, reviewed behavior change.

.PARAMETER SkipResume
    Skip the mode-2 resume self-consistency leg (mode 1 only).

.PARAMETER DownloadsPath
    Override the downloads folder (default: Windows Downloads, honoring
    SKYLINE_DOWNLOAD_PATH and a relocated Downloads).

.PARAMETER Threads
    --threads for each run (default 16).

.PARAMETER TeamCity
    Emit TeamCity service messages (progressMessage, buildProblem). No artifacts
    are published.

.PARAMETER NoBuild
    Skip the OspreySharp build step (use the existing Release binary).

.EXAMPLE
    # Local: run Stellar straight-through + resume against the committed golden
    .\regression.ps1 -Dataset Stellar

.EXAMPLE
    # Refresh the goldens after a reviewed behavior change
    .\regression.ps1 -Dataset All -CreateGolden

.EXAMPLE
    # TeamCity (what tctest.bat invokes)
    .\regression.ps1 -TeamCity -Dataset All
#>
param(
    [ValidateSet('Stellar', 'Astral', 'All')] [string]$Dataset = 'All',
    [switch]$CreateGolden,
    [switch]$SkipResume,
    [string]$DownloadsPath,
    [int]$Threads = 16,
    [switch]$TeamCity,
    [switch]$NoBuild,
    [double]$Tolerance = 1e-9
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptRoot   = Split-Path -Parent $PSCommandPath
$regressionDir = Join-Path $scriptRoot 'Regression'
$goldenRoot   = Join-Path $scriptRoot 'osprey-regression.data'
$ospreyBinDir = Join-Path $scriptRoot 'OspreySharp\bin\x64\Release\net8.0'
$ospreyExe    = Join-Path $ospreyBinDir 'OspreySharp.exe'

# Bit-parity version pin. The build stamps a daily Skyline-scheme version
# (YEAR.ORDINAL.BRANCH.DOY) that changes every day, but the committed blib golden
# compares the osprey_version metadata cell exactly. Pin OspreyVersion.Current to
# a canonical constant for every OspreySharp invocation in this run (the env var
# is inherited by the child processes), so the stamp is deterministic and the
# golden stays green without the comparator skipping the field. Must match the
# osprey_version value committed in osprey-regression.data/*/tables/OspreyMetadata.tsv.
$env:OSPREY_VERSION_OVERRIDE = '26.1.1.0'

# The mzML data zip on panorama (raw-data zip is future work). The URL's
# second-to-last segment ("perftests") maps to <Downloads>\Perftests.
$dataUrl = 'https://panoramaweb.org/_webdav/MacCoss/software/%40files/perftests/osprey-testfiles-mzML.zip'

# --- Dataset table (standalone; mirrors ai/ Dataset-Config.ps1) --------------
# Subfolder under the extracted root + resolution mode. Input mzML files and the
# .tsv library are discovered from the folder so filenames are not hard-coded.
$datasets = [ordered]@{
    Stellar = @{ Folder = 'stellar'; Resolution = 'unit' }
    Astral  = @{ Folder = 'astral';  Resolution = 'hram' }
}
$selected = if ($Dataset -eq 'All') { @($datasets.Keys) } else { @($Dataset) }

# --- TeamCity service-message helpers (mirror build.ps1) ----------------------
function Format-TcMessage([string]$s) {
    if ($null -eq $s) { return '' }
    return $s.Replace('|', '||').Replace("'", "|'").Replace("`n", '|n').Replace("`r", '|r').Replace('[', '|[').Replace(']', '|]')
}
function Write-Progress-Tc([string]$msg) {
    if ($TeamCity) { Write-Host ("##teamcity[progressMessage '{0}']" -f (Format-TcMessage $msg)) }
    else { Write-Host "==> $msg" -ForegroundColor Cyan }
}
function Write-Problem-Tc([string]$msg) {
    if ($TeamCity) { Write-Host ("##teamcity[buildProblem description='{0}']" -f (Format-TcMessage $msg)) }
    Write-Host "ERROR: $msg" -ForegroundColor Red
}

# --- Build (unless -NoBuild) --------------------------------------------------
if (-not $NoBuild) {
    Write-Progress-Tc 'Building OspreySharp (Release, net8.0)'
    $buildPs1 = Join-Path $scriptRoot 'build.ps1'
    & $buildPs1 -Configuration Release -Framework net8.0 -NoTests
    if ($LASTEXITCODE -ne 0) { Write-Problem-Tc "OspreySharp build failed (exit $LASTEXITCODE)"; exit $LASTEXITCODE }
}
if (-not (Test-Path $ospreyExe)) {
    Write-Problem-Tc "OspreySharp.exe not found at $ospreyExe (build first, or drop -NoBuild)"
    exit 2
}

# --- Load standalone helpers + SQLite ----------------------------------------
. (Join-Path $regressionDir 'RegressionData.ps1')
. (Join-Path $regressionDir 'BlibGolden.ps1')
Initialize-Sqlite -OspreyBinDir $ospreyBinDir

# --- Acquire data (download + unzip + skip-if-present) ------------------------
Write-Progress-Tc 'Acquiring regression data'
$extractedRoot = Get-RegressionData -Url $dataUrl -DownloadsPath $DownloadsPath `
    -Log { param($m) Write-Progress-Tc $m }

# Per-run timestamped run root under TestResults (gitignored scratch; nothing
# here is published as a TeamCity artifact). The full timestamp makes every
# invocation its own dir, so a re-run never inherits a prior run's
# resumed/invalidated state (the mode-2 invalidation + leftover output_cold.blib)
# -- which would otherwise make the next straight-through leg resume instead of
# run clean. These dirs hold the multi-GB .spectra.bin caches (via --work-dir),
# so the agent should treat TestResults as ephemeral and clean it periodically.
$runStamp = (Get-Date).ToString('yyyyMMdd_HHmmss')
$runRoot  = Join-Path $scriptRoot ("TestResults\regression-$runStamp")
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null

# --- Run one OspreySharp invocation (no input copies) -------------------------
function Invoke-OspreyRun {
    param([string[]]$Mzmls, [string]$Library, [string]$Resolution, [string]$WorkDir,
          [string]$LogName, [switch]$DumpProteinFdr)
    New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null
    $logPath = Join-Path $WorkDir $LogName
    $cliArgs = @()
    foreach ($m in $Mzmls) { $cliArgs += @('-i', $m) }
    $cliArgs += @('-l', $Library, '-o', 'output.blib',
                  '--resolution', $Resolution, '--protein-fdr', '0.01',
                  '--threads', $Threads.ToString(), '--work-dir', $WorkDir)
    if ($DumpProteinFdr) { $env:OSPREY_DUMP_STAGE7_PROTEIN_FDR = '1' }
    # Run with CWD = work dir so the -o blib and the Stage 7 protein-FDR dump
    # (both CWD-relative, NOT --work-dir-relative -- only derived artifacts +
    # caches honor --work-dir) land in the work dir. Inputs, library, and
    # --work-dir are absolute paths, so the CWD change does not affect them.
    Push-Location $WorkDir
    try {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        & $ospreyExe @cliArgs 2>&1 | Tee-Object -FilePath $logPath | Out-Null
        $exit = $LASTEXITCODE
        $sw.Stop()
    } finally {
        Pop-Location
        if ($DumpProteinFdr) { Remove-Item Env:OSPREY_DUMP_STAGE7_PROTEIN_FDR -ErrorAction SilentlyContinue }
    }
    if ($exit -ne 0) { throw "OspreySharp exited $exit (see $logPath)" }
    return @{ Wall = $sw.Elapsed; Log = $logPath }
}

# Resolve a dataset's inputs from the extracted read-only data folder.
function Resolve-DatasetInputs {
    param([string]$Folder)
    $dir = Join-Path $extractedRoot $Folder
    if (-not (Test-Path $dir)) { throw "Dataset folder not found in data: $dir" }
    $mzmls = @(Get-ChildItem -Path $dir -Filter '*.mzML' -File | Sort-Object Name | ForEach-Object { $_.FullName })
    if ($mzmls.Count -eq 0) { throw "No .mzML files in $dir" }
    $libs = @(Get-ChildItem -Path $dir -Filter '*.tsv' -File | ForEach-Object { $_.FullName })
    if ($libs.Count -ne 1) { throw "Expected exactly one .tsv library in $dir, found $($libs.Count)" }
    return @{ Dir = $dir; Mzmls = $mzmls; Library = $libs[0] }
}

# Snapshot file sizes + mtimes of the read-only data dir, to assert no-copy
# leaves it untouched after the run.
function Get-DirFingerprint {
    param([string]$Dir)
    $fp = @{}
    foreach ($f in Get-ChildItem -Path $Dir -File) { $fp[$f.Name] = "$($f.Length):$($f.LastWriteTimeUtc.Ticks)" }
    return $fp
}
function Compare-DirFingerprint {
    param([hashtable]$Before, [string]$Dir)
    $after = Get-DirFingerprint -Dir $Dir
    $changed = [System.Collections.Generic.List[string]]::new()
    foreach ($k in $after.Keys) {
        if (-not $Before.ContainsKey($k)) { $changed.Add("new: $k") }
        elseif ($Before[$k] -ne $after[$k]) { $changed.Add("modified: $k") }
    }
    foreach ($k in $Before.Keys) { if (-not $after.ContainsKey($k)) { $changed.Add("removed: $k") } }
    return $changed
}

# Invalidate the Stage 5 join + blib so a re-run resumes (rehydrate paths fire)
# rather than recomputing from spectra. Mirrors the proven repro from
# TODO-20260605_ospreysharp_resume_reconciled_rt.
function Invoke-ResumeInvalidation {
    param([string]$WorkDir)
    Get-ChildItem -Path $WorkDir -File | Where-Object {
        $_.Name -like '*.FirstJoin.osprey.task' -or
        $_.Name -eq 'output.blib' -or $_.Name -eq 'output.blib.MergeNode.osprey.task'
    } | Remove-Item -Force
}

# --- Per-dataset legs ---------------------------------------------------------
$overallFail = $false
$summaryLines = [System.Collections.Generic.List[string]]::new()

foreach ($name in $selected) {
    $cfg = $datasets[$name]
    Write-Progress-Tc "Dataset $name"
    $inputs = Resolve-DatasetInputs -Folder $cfg.Folder
    $dataFp = Get-DirFingerprint -Dir $inputs.Dir

    $straightDir = Join-Path $runRoot "$name\straight"
    $proteinDump = Join-Path $straightDir 'cs_stage7_protein_fdr.tsv'
    $goldenDir   = Join-Path $goldenRoot $cfg.Folder

    # ---- Straight-through ----
    # (The per-run timestamped $runRoot guarantees $straightDir is fresh, so the
    # straight-through leg always runs clean -- no prior-run state to inherit.)
    Write-Progress-Tc "${name}: straight-through run ($($inputs.Mzmls.Count) files, $($cfg.Resolution))"
    $rStraight = Invoke-OspreyRun -Mzmls $inputs.Mzmls -Library $inputs.Library -Resolution $cfg.Resolution `
        -WorkDir $straightDir -LogName 'straight.log' -DumpProteinFdr
    $straightBlib = Join-Path $straightDir 'output.blib'
    Write-Host ("  straight-through wall {0:mm\:ss}; blib {1:N0} bytes" -f $rStraight.Wall, (Get-Item $straightBlib).Length)

    # ---- No-copy assertion: read-only data dir unchanged ----
    $changed = Compare-DirFingerprint -Before $dataFp -Dir $inputs.Dir
    if ($changed.Count -gt 0) {
        $overallFail = $true
        Write-Problem-Tc "${name}: read-only data dir was modified by the run: $($changed -join '; ')"
    }

    if ($CreateGolden) {
        Write-Progress-Tc "${name}: capturing golden"
        Save-BlibGolden -Blib $straightBlib -GoldenDir $goldenDir -ProteinFdrTsv $proteinDump
        $summaryLines.Add("$name golden CAPTURED -> $goldenDir")
        continue
    }

    # ---- mode 1: straight-through vs committed golden ----
    Write-Progress-Tc "${name}: comparing vs golden (mode 1)"
    $m1 = Compare-BlibGolden -Blib $straightBlib -GoldenDir $goldenDir -ProteinFdrTsv $proteinDump -Tolerance $Tolerance
    if ($m1.Pass) {
        $summaryLines.Add("$name mode1 (vs golden): PASS")
    } else {
        $overallFail = $true
        Write-Problem-Tc "$name mode1 (vs golden): FAIL -- $($m1.Issues.Count) issue(s)"
        $m1.Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        $summaryLines.Add("$name mode1 (vs golden): FAIL ($($m1.Issues.Count) issues)")
    }

    # ---- mode 2: resume vs straight-through self-consistency ----
    if (-not $SkipResume) {
        Write-Progress-Tc "${name}: resume self-consistency (mode 2)"
        $coldBlib = Join-Path $straightDir 'output_cold.blib'
        Copy-Item $straightBlib $coldBlib -Force
        Invoke-ResumeInvalidation -WorkDir $straightDir
        $rResume = Invoke-OspreyRun -Mzmls $inputs.Mzmls -Library $inputs.Library -Resolution $cfg.Resolution `
            -WorkDir $straightDir -LogName 'resume.log'
        $resumeBlib = Join-Path $straightDir 'output.blib'
        Write-Host ("  resume wall {0:mm\:ss}; blib {1:N0} bytes" -f $rResume.Wall, (Get-Item $resumeBlib).Length)
        $m2 = Compare-BlibFull -BlibExpected $coldBlib -BlibActual $resumeBlib -Tolerance $Tolerance
        if ($m2.Pass) {
            $summaryLines.Add("$name mode2 (resume==straight): PASS")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode2 (resume==straight): FAIL -- $($m2.Issues.Count) issue(s)"
            $m2.Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode2 (resume==straight): FAIL ($($m2.Issues.Count) issues)")
        }
    }
}

# --- Summary + exit -----------------------------------------------------------
Write-Host ""
Write-Host "=== OspreySharp regression summary ===" -ForegroundColor Cyan
$summaryLines | ForEach-Object { Write-Host "  $_" }
# No artifacts are published. The diagnosis on a red gate lives in the build log
# (every per-file log is Tee'd to the console TeamCity captures) and the
# buildProblem line (which names the failing dataset + leg + first divergent
# columns); the run outputs stay on the agent under the gitignored TestResults.
if ($overallFail) {
    Write-Problem-Tc 'OspreySharp regression FAILED'
    exit 1
}
Write-Host "OspreySharp regression PASSED" -ForegroundColor Green
exit 0
