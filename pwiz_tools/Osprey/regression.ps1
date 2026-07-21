<#
.SYNOPSIS
    Osprey overnight end-to-end regression. Self-contained entry point for
    the scheduled TeamCity "Osprey Windows .NET Regression" config (via
    tctest.bat) and for local developer runs.

.DESCRIPTION
    Acquires real DIA test data the way Skyline perf tests do (download a
    panorama zip into the shared <Downloads>\Perftests folder, extract,
    skip-if-present), then runs the full Osprey pipeline on each dataset
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
      mode 3  HPC 4-task worker-chain self-consistency -- runs the distributed
              --task pipeline (PerFileScoring -> FirstPassFDR -> PerFileRescoring ->
              SecondPassFDR), each phase rehydrating the prior phase's on-disk
              sidecars exactly as a multi-computer distribution would, and
              asserts the chain's final blib equals the straight-through blib at
              1e-9. Where mode 2 covers in-process straight-through resume, mode 3
              covers the cross-process --task boundary rehydrate paths. Stages all
              inputs + sidecars by copy under the run dir (the read-only data dir
              is never touched); per-stage parquet/sidecar bisection of a red gate
              lives in ai/scripts/Osprey/Compare (Compare-Stage7-Rehydration-
              Strict-CSharp.ps1).

    NO dependency on the sibling ai/ checkout: data acquisition, blib golden
    capture/compare, and the tolerance comparators all live under
    pwiz_tools/Osprey/Regression. Mirrors build.ps1's TeamCity service
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

.PARAMETER SkipHpcChain
    Skip the mode-3 HPC 4-task worker-chain leg. The overnight gate leaves it on
    (the chain is part of the standing cadence); this switch is for fast local
    iteration when only the straight-through correctness matters.

.PARAMETER DownloadsPath
    Override the downloads folder (default: Windows Downloads, honoring
    SKYLINE_DOWNLOAD_PATH and a relocated Downloads).

.PARAMETER Threads
    --threads for each run (default 16).

.PARAMETER TeamCity
    Emit TeamCity service messages (progressMessage, buildProblem). No artifacts
    are published.

.PARAMETER NoBuild
    Skip the Osprey build step (use the existing Release binary).

.PARAMETER KeepRunDirs
    Number of most-recent TestResults\regression-* run dirs to keep when pruning
    ORPHANS at startup (default 0 -- keep none). A normal run now removes its own
    output when it finishes (see -KeepOutput), so this only clears dirs left behind
    by a previously killed run (TeamCity timeout / OOM). Raise it to retain old run
    dirs on a roomy local disk.

.PARAMETER KeepOutput
    Keep this run's TestResults\regression-<stamp> output instead of deleting it. By
    default the run deletes its scratch as it goes -- each HPC-chain phase and each
    dataset as soon as it is consumed, then the whole run root at the end -- so it
    leaves no multi-GB output behind to starve the next run on a shared build agent.
    The raw input data (downloaded mzML/library) is NEVER touched. Pass this locally
    to retain output for post-mortem; a red CI gate's diagnosis lives in the build
    log, not these files.

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
    [switch]$SkipHpcChain,
    [string]$DownloadsPath,
    [int]$Threads = 16,
    [switch]$TeamCity,
    [switch]$NoBuild,
    [ValidateRange(0, [int]::MaxValue)]
    [int]$KeepRunDirs = 0,
    [switch]$KeepOutput,
    [double]$Tolerance = 1e-9
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptRoot   = Split-Path -Parent $PSCommandPath
$regressionDir = Join-Path $scriptRoot 'Regression'
$goldenRoot   = Join-Path $scriptRoot 'osprey-regression.data'
$ospreyBinDir = Join-Path $scriptRoot 'Osprey\bin\x64\Release\net8.0'
$ospreyExe    = Join-Path $ospreyBinDir 'Osprey.exe'

# Bit-parity version pin. The build stamps a daily Skyline-scheme version
# (YEAR.ORDINAL.BRANCH.DOY) that changes every day, but the committed blib golden
# compares the osprey_version metadata cell exactly. Pin OspreyVersion.Current to
# a canonical constant for every Osprey invocation in this run (the env var
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

# --- Reclaim disk: prune orphaned TestResults run dirs ------------------------
# A normal run now removes its OWN TestResults\regression-<stamp> dir as it goes
# (each HPC-chain phase + dataset when consumed, then the run root at the end --
# see the cleanup below and -KeepOutput), so between runs there is normally nothing
# here. This startup prune is the safety net for ORPHANS: a dir left by a run that
# was killed (TeamCity timeout / OOM) before it reached its own cleanup. Run here
# FIRST -- before the build, data acquisition, and the new run dir -- so even a
# near-full disk can run it (deleting needs ~no free space) and the rest of the run
# has the reclaimed space. Keeps the most recent $KeepRunDirs (default 0 = keep
# none). The dir names sort chronologically (regression-YYYYMMDD_HHMMSS), so a Name
# sort orders oldest-first.
function Remove-StaleRunDirs([string]$TestResultsDir, [int]$Keep) {
    if (-not (Test-Path $TestResultsDir)) { return }
    $runDirs = @(Get-ChildItem -Path $TestResultsDir -Directory -Filter 'regression-*' `
        -ErrorAction SilentlyContinue | Sort-Object Name)
    if ($runDirs.Count -le $Keep) { return }
    $stale = $runDirs[0..($runDirs.Count - $Keep - 1)]
    Write-Progress-Tc ("Pruning {0} stale TestResults run dir(s), keeping the most recent {1}" -f $stale.Count, $Keep)
    foreach ($d in $stale) {
        try {
            Remove-Item -LiteralPath $d.FullName -Recurse -Force -ErrorAction Stop
            Write-Host "  pruned $($d.Name)"
        } catch {
            Write-Host "  WARN: failed to prune $($d.FullName): $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}
Remove-StaleRunDirs (Join-Path $scriptRoot 'TestResults') $KeepRunDirs

# Best-effort recursive delete of a scratch path (a run/phase/dataset output dir or
# a single dead-weight input copy). Swallows errors -- reclaiming disk must never
# fail the gate. Honors -KeepOutput so a local post-mortem can retain everything.
function Remove-Scratch([string]$Path) {
    if ($KeepOutput) { return }
    if ([string]::IsNullOrEmpty($Path) -or -not (Test-Path -LiteralPath $Path)) { return }
    Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
}

# --- Build (unless -NoBuild) --------------------------------------------------
if (-not $NoBuild) {
    Write-Progress-Tc 'Building Osprey (Release, net8.0)'
    $buildPs1 = Join-Path $scriptRoot 'build.ps1'
    & $buildPs1 -Configuration Release -Framework net8.0 -NoTests
    if ($LASTEXITCODE -ne 0) { Write-Problem-Tc "Osprey build failed (exit $LASTEXITCODE)"; exit $LASTEXITCODE }
}
if (-not (Test-Path $ospreyExe)) {
    Write-Problem-Tc "Osprey.exe not found at $ospreyExe (build first, or drop -NoBuild)"
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

# --- Run one Osprey invocation (no input copies) -------------------------
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
    if ($exit -ne 0) { throw "Osprey exited $exit (see $logPath)" }
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
        $_.Name -like '*.FirstPassFDR.osprey.task' -or
        $_.Name -eq 'output.blib' -or $_.Name -eq 'output.blib.SecondPassFDR.osprey.task'
    } | Remove-Item -Force
}

# --- mode 3: HPC 4-task worker chain ------------------------------------------
# Low-level runner for a single --task phase: CWD = its own scratch dir so the
# task's CWD-relative outputs (parquets, sidecars, blib) land there, mirroring a
# real HPC worker that ships only its inputs and writes beside them. Throws on a
# non-zero exit so the chain aborts loudly at the failing phase.
function Invoke-OspreyTaskRun {
    param([string]$WorkDir, [string[]]$CliArgs, [string]$LogName)
    New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null
    $logPath = Join-Path $WorkDir $LogName
    Push-Location $WorkDir
    try {
        & $ospreyExe @CliArgs 2>&1 | Tee-Object -FilePath $logPath | Out-Null
        $exit = $LASTEXITCODE
    } finally {
        Pop-Location
    }
    if ($exit -ne 0) { throw "Osprey --task exited $exit (see $logPath)" }
}

# Stage the library (+ its .libcache when present) into a phase dir.
function Copy-LibraryInto {
    param([string]$Library, [string]$Dir)
    Copy-Item $Library (Join-Path $Dir (Split-Path -Leaf $Library))
    $libCache = $Library + '.libcache'
    if (Test-Path $libCache) { Copy-Item $libCache (Join-Path $Dir (Split-Path -Leaf $libCache)) }
}

# Run the distributed --task pipeline end to end against copied inputs under
# $ChainRoot and return the final merge-node blib. Each phase rehydrates the
# prior phase's on-disk sidecars, exactly as a multi-computer distribution would;
# nothing is held in memory across phases. All inputs/sidecars are copied (never
# referenced in place), so the read-only data dir is untouched.
function Invoke-HpcChain {
    param([string[]]$Mzmls, [string]$Library, [string]$Resolution, [string]$ChainRoot)
    $libName = Split-Path -Leaf $Library
    # Stable, file-order stem list (NOT hashtable key order) so the --input-scores
    # argument order matches the straight-through's file order deterministically.
    $stemList = @($Mzmls | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) })
    $mzmlByStem = @{}
    foreach ($m in $Mzmls) { $mzmlByStem[[IO.Path]::GetFileNameWithoutExtension($m)] = $m }

    # Phase 1: per-file raw workers (Stage 1-4). Writes <stem>.scores.parquet +
    # <stem>.calibration.json per file.
    $ph1 = Join-Path $ChainRoot 'phase1_scoring'
    New-Item -ItemType Directory -Path $ph1 -Force | Out-Null
    foreach ($m in $Mzmls) { Copy-Item $m (Join-Path $ph1 (Split-Path -Leaf $m)) }
    Copy-LibraryInto -Library $Library -Dir $ph1
    $a1 = @()
    foreach ($m in $Mzmls) { $a1 += @('-i', (Split-Path -Leaf $m)) }
    $a1 += @('-l', $libName, '-o', 'output.blib', '--resolution', $Resolution,
             '--protein-fdr', '0.01', '--threads', $Threads.ToString(), '--task', 'PerFileScoring')
    Invoke-OspreyTaskRun -WorkDir $ph1 -CliArgs $a1 -LogName 'phase1.log'
    # Phase 1's copied mzMLs are dead weight once it has run: phase 2/3 read its
    # parquets + calibration, never its mzML (phase 3 re-copies the mzML from the
    # data dir). Drop them so they don't sit on disk through the per-file rescore loop.
    if (-not $KeepOutput) {
        Get-ChildItem -Path $ph1 -Filter '*.mzML' -File -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }

    # Phase 2: 1st-join (Stage 5). Consumes the per-file parquets, writes the
    # <stem>.1st-pass.fdr_scores.bin + <stem>.reconciliation.json sidecar pair. A
    # 0-byte stub mzML lets the task derive sidecar paths without reading spectra.
    $ph2 = Join-Path $ChainRoot 'phase2_firstjoin'
    New-Item -ItemType Directory -Path $ph2 -Force | Out-Null
    foreach ($s in $stemList) {
        Copy-Item (Join-Path $ph1 "$s.scores.parquet")   (Join-Path $ph2 "$s.scores.parquet")
        Copy-Item (Join-Path $ph1 "$s.calibration.json") (Join-Path $ph2 "$s.calibration.json")
        New-Item -ItemType File -Path (Join-Path $ph2 "$s.mzML") -Force | Out-Null
    }
    Copy-LibraryInto -Library $Library -Dir $ph2
    $a2 = @('--task', 'FirstPassFDR')
    foreach ($s in $stemList) { $a2 += @('--input-scores', "$s.scores.parquet") }
    $a2 += @('-l', $libName, '-o', 'output.blib', '--resolution', $Resolution,
             '--protein-fdr', '0.01', '--threads', $Threads.ToString())
    Invoke-OspreyTaskRun -WorkDir $ph2 -CliArgs $a2 -LogName 'phase2.log'

    # Phase 3: per-file rescore workers (Stage 6), one independent worker per
    # file. Stage 6 STREAMS its MS2 from the .spectra.bin cache phase 1 wrote (there is
    # no mzML fallback), so each worker gets phase 1's <stem>.spectra.bin + a 0-byte stub
    # <stem>.mzML (the cache fingerprint check is skipped for a 0-byte source, so the
    # stub is enough for path derivation and forces a cache hit -- the real 6 GB mzML is
    # never shipped to a rescore worker). Plus the Stage 4 parquet/calibration + the
    # Stage 5 sidecar pair; writes <stem>.scores-reconciled.parquet + the 2nd-pass bin.
    $ph3Dirs = @{}
    foreach ($s in $stemList) {
        $ph3 = Join-Path $ChainRoot "phase3_rescore_$s"
        $ph3Dirs[$s] = $ph3
        New-Item -ItemType Directory -Path $ph3 -Force | Out-Null
        Copy-Item (Join-Path $ph1 "$s.spectra.bin")             (Join-Path $ph3 "$s.spectra.bin")
        New-Item -ItemType File -Path (Join-Path $ph3 "$s.mzML") -Force | Out-Null
        Copy-Item (Join-Path $ph1 "$s.scores.parquet")          (Join-Path $ph3 "$s.scores.parquet")
        Copy-Item (Join-Path $ph1 "$s.calibration.json")        (Join-Path $ph3 "$s.calibration.json")
        Copy-Item (Join-Path $ph2 "$s.1st-pass.fdr_scores.bin") (Join-Path $ph3 "$s.1st-pass.fdr_scores.bin")
        Copy-Item (Join-Path $ph2 "$s.reconciliation.json")     (Join-Path $ph3 "$s.reconciliation.json")
        Copy-LibraryInto -Library $Library -Dir $ph3
        $a3 = @('--task', 'PerFileRescoring', '--input-scores', "$s.scores.parquet",
                '-l', $libName, '-o', 'output.blib', '--resolution', $Resolution,
                '--protein-fdr', '0.01', '--threads', $Threads.ToString())
        Invoke-OspreyTaskRun -WorkDir $ph3 -CliArgs $a3 -LogName 'phase3.log'
        # This worker has written its reconciled parquet + 2nd-pass bin; phase 4
        # consumes only those plus the calibration / reconciliation / 1st-pass
        # sidecars copied above -- never this worker's spectra cache, input
        # scores.parquet, or library. Drop those big inputs now so at most one
        # worker's 6 GB spectra.bin + library copy is on disk at a time (the
        # out-of-disk failure was several of them coexisting with the
        # straight-through leg's spectra caches).
        if (-not $KeepOutput) {
            Remove-Item (Join-Path $ph3 "$s.spectra.bin") -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $ph3 "$s.mzML") -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $ph3 "$s.scores.parquet") -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $ph3 $libName) -Force -ErrorAction SilentlyContinue
            Remove-Item (Join-Path $ph3 ($libName + '.libcache')) -Force -ErrorAction SilentlyContinue
        }
    }

    # Phases 1 and 2 are fully consumed once every rescore worker has copied its
    # inputs (phase 4 reads only phase-3 outputs). Free them before the merge node.
    Remove-Scratch $ph1
    Remove-Scratch $ph2

    # Phase 4: 2nd-join merge node (Stage 7 + blib). Consumes each worker's
    # reconciled parquet + sidecars (never the original Stage 4 parquet, and never
    # an mzML -- a 0-byte stub provides path derivation only) and writes the blib.
    $ph4 = Join-Path $ChainRoot 'phase4_mergenode'
    New-Item -ItemType Directory -Path $ph4 -Force | Out-Null
    foreach ($s in $stemList) {
        $ph3 = $ph3Dirs[$s]
        Copy-Item (Join-Path $ph3 "$s.scores-reconciled.parquet") (Join-Path $ph4 "$s.scores-reconciled.parquet")
        Copy-Item (Join-Path $ph3 "$s.1st-pass.fdr_scores.bin")   (Join-Path $ph4 "$s.1st-pass.fdr_scores.bin")
        Copy-Item (Join-Path $ph3 "$s.calibration.json")          (Join-Path $ph4 "$s.calibration.json")
        Copy-Item (Join-Path $ph3 "$s.reconciliation.json")       (Join-Path $ph4 "$s.reconciliation.json")
        $pass2 = Join-Path $ph3 "$s.2nd-pass.fdr_scores.bin"
        if (Test-Path $pass2) { Copy-Item $pass2 (Join-Path $ph4 "$s.2nd-pass.fdr_scores.bin") }
        New-Item -ItemType File -Path (Join-Path $ph4 "$s.mzML") -Force | Out-Null
    }
    # The merge node now has every worker's reconciled output copied in; the phase-3
    # worker dirs are done.
    foreach ($d in $ph3Dirs.Values) { Remove-Scratch $d }
    Copy-LibraryInto -Library $Library -Dir $ph4
    $a4 = @('--task', 'SecondPassFDR')
    foreach ($s in $stemList) { $a4 += @('--input-scores', "$s.scores-reconciled.parquet") }
    $a4 += @('-l', $libName, '-o', 'output.blib', '--resolution', $Resolution,
             '--protein-fdr', '0.01', '--threads', $Threads.ToString())
    Invoke-OspreyTaskRun -WorkDir $ph4 -CliArgs $a4 -LogName 'phase4.log'

    return (Join-Path $ph4 'output.blib')
}

# --- Per-dataset legs ---------------------------------------------------------
$overallFail = $false
$summaryLines = [System.Collections.Generic.List[string]]::new()

# Self-cleaning: each dataset's scratch is removed as soon as its legs finish, and
# the whole run root in the finally below -- so the run leaves no multi-GB output
# behind to starve the next run on a shared agent. -KeepOutput (honored by
# Remove-Scratch) opts out for local post-mortem.
try {
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

    # ---- mode 3: HPC 4-task worker chain vs straight-through ----
    # Runs BEFORE mode 2: mode 2 invalidates + re-runs $straightDir in place, so
    # $straightBlib is the pristine straight-through output only until then.
    if (-not $SkipHpcChain) {
        Write-Progress-Tc "${name}: HPC 4-task chain self-consistency (mode 3)"
        $chainRoot = Join-Path $runRoot "$name\chain"
        $sw3 = [Diagnostics.Stopwatch]::StartNew()
        $chainBlib = Invoke-HpcChain -Mzmls $inputs.Mzmls -Library $inputs.Library `
            -Resolution $cfg.Resolution -ChainRoot $chainRoot
        $sw3.Stop()
        Write-Host ("  HPC chain wall {0:mm\:ss}; blib {1:N0} bytes" -f $sw3.Elapsed, (Get-Item $chainBlib).Length)
        $m3 = Compare-BlibFull -BlibExpected $straightBlib -BlibActual $chainBlib -Tolerance $Tolerance
        if ($m3.Pass) {
            $summaryLines.Add("$name mode3 (HPC chain==straight): PASS")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode3 (HPC chain==straight): FAIL -- $($m3.Issues.Count) issue(s)"
            $m3.Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode3 (HPC chain==straight): FAIL ($($m3.Issues.Count) issues)")
        }
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

    # All legs for this dataset are done -- free its scratch now so peak disk stays
    # at ~one dataset (the next dataset / the perf-gate step gets the space back).
    Remove-Scratch (Join-Path $runRoot $name)
}
}
finally {
    # Safety net for a dataset that threw before its own cleanup -- drop the whole
    # run root. Raw input data lives outside $runRoot and is untouched.
    Remove-Scratch $runRoot
}

# --- Summary + exit -----------------------------------------------------------
Write-Host ""
Write-Host "=== Osprey regression summary ===" -ForegroundColor Cyan
$summaryLines | ForEach-Object { Write-Host "  $_" }
# No artifacts are published, and the run's scratch under TestResults is deleted on
# completion (the downloaded raw input data is kept). A red gate's diagnosis lives in
# the build log (every per-file log is Tee'd to the console TeamCity captures) and
# the buildProblem line (which names the failing dataset + leg + first divergent
# columns), NOT in the run output files. Pass -KeepOutput to retain them locally.
if ($overallFail) {
    Write-Problem-Tc 'Osprey regression FAILED'
    exit 1
}
Write-Host "Osprey regression PASSED" -ForegroundColor Green
exit 0
