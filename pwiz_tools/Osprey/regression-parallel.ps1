<#
.SYNOPSIS
    Run the Osprey regression suite as two concurrent lanes instead of serially.

.DESCRIPTION
    The suite is dominated by one dataset. Measured 2026-09-12 on a -Dataset All run
    with every mode enabled, per-leg seconds from the phase-cost tables:

        Astral                 3,614 s   (Astral lane)
        StellarLibDecoy        1,662 s
        Stellar                1,181 s
        StellarGenDecoyEntrap  1,040 s   (the three together: 3,883 s, the other lane)

    Astral alone was almost exactly the other three combined, so the first split
    (2026-09-05) put it in its own lane and cut the serial 2h04m to ~65 min without
    dropping a leg. The sparse matrix that followed (regression.ps1's SkipModes, mapped
    in regression.html) cut Astral to 2,245 s and StellarGenDecoyEntrap to 402 s, so
    the balanced pairing is now Astral+StellarGenDecoyEntrap (2,646 s) against
    Stellar+StellarLibDecoy (2,593 s): wall ~44 min on this machine.

    The lanes share READ-ONLY data and nothing else. StellarGenDecoyEntrap reads the
    `stellar` folder and the stellar-libdecoy extract that the other lane reads too;
    the one derived artifact under TestResults\_derived (its decoy-free library) is
    written by that dataset alone. The first-time download, extraction and derivation
    are therefore staged ONCE below, before the lanes launch, the same way the build
    is - two lanes finding a shared library absent at the same moment would otherwise
    race the same extraction.

    Two shared-path collisions had to be fixed before any of this was possible (both in
    2026-09-05 commits): SQLite.Interop.dll was overwritten unconditionally while
    being held open by the other lane, and the run root was keyed on a whole-second
    timestamp so lanes started in the same second shared - and deleted - one
    directory. Do not assume new shared state is safe; add it per-lane or stage it once.

.PARAMETER Threads
    Threads per LANE, not for the machine. Defaults to logical processors divided
    by the lane count, which is the only value that is right on more than one box:
    this dev machine is 32 logical (2 lanes x 16) and MacCoss TeamCity Agent 1 is
    16 (2 lanes x 8). Hardcoding 16 would oversubscribe the agent 2:1.

.PARAMETER Dataset
    Which datasets to run, default all four. A single dataset runs serially, since
    there is nothing to overlap it with.

.NOTES
    Lanes contend measurably - 4-17% per lane when two run together - so the total is
    somewhat above the longer lane rather than equal to it.
#>
param(
    [ValidateSet('Stellar', 'StellarLibDecoy', 'StellarGenDecoyEntrap', 'Astral', 'All')]
    [string[]]$Dataset = 'All',
    [int]$Threads = 0,   # 0 = auto: logical processors / lane count
    [switch]$NoBuild,
    [switch]$TeamCity,
    [string]$LogDir
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$scriptRoot = Split-Path -Parent $PSCommandPath
$regression = Join-Path $scriptRoot 'regression.ps1'
$ospreyExe  = Join-Path $scriptRoot 'Osprey\bin\x64\Release\net8.0\Osprey.exe'
if (-not $LogDir) { $LogDir = Join-Path $scriptRoot 'TestResults' }
if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir -Force | Out-Null }

$all = @('Stellar', 'StellarLibDecoy', 'StellarGenDecoyEntrap', 'Astral')
$selected = if ($Dataset -contains 'All') { $all } else { @($Dataset) }

# Astral leads one lane and takes StellarGenDecoyEntrap, the cheapest dataset under the
# sparse matrix, as its partner; Stellar and StellarLibDecoy make the other. Derived from
# $selected rather than hardcoded so a subset still splits sanely.
$laneA = @($selected | Where-Object { $_ -in @('Astral', 'StellarGenDecoyEntrap') })
$laneB = @($selected | Where-Object { $_ -notin @('Astral', 'StellarGenDecoyEntrap') })
# Add to a List, do NOT build with @($laneA, $laneB). PowerShell FLATTENS nested array
# literals, so an empty $laneA collapses the pair and $laneB's three names become three
# separate lanes, which would run three or four datasets concurrently on a box sized
# for two. Caught by the lane-split test, 2026-09-05.
$lanes = [System.Collections.Generic.List[object]]::new()
if ($laneA.Count -gt 0) { $lanes.Add($laneA) }
if ($laneB.Count -gt 0) { $lanes.Add($laneB) }

# Size threads to the MACHINE, after the lane count is known. Two lanes each asking
# for 16 threads is right on a 32-logical box and 2:1 oversubscription on a 16-logical
# one - and the agent this has to run on is 16. Auto keeps one config correct on both.
if ($Threads -le 0) {
    $Threads = [Math]::Max(1, [int]([Environment]::ProcessorCount / $lanes.Count))
}
Write-Host ("==> {0} lane(s), {1} thread(s) each, {2} logical processor(s)" -f
    $lanes.Count, $Threads, [Environment]::ProcessorCount) -ForegroundColor Cyan

# --- Build ONCE, here, so the lanes cannot race each other's build output ---------
if (-not $NoBuild) {
    Write-Host '==> Building Osprey (Release, net8.0) once for both lanes' -ForegroundColor Cyan
    & (Join-Path $scriptRoot 'build.ps1') -Configuration Release -Framework net8.0 -NoTests
    if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Osprey build failed (exit $LASTEXITCODE)" -ForegroundColor Red; exit $LASTEXITCODE }
}
if (-not (Test-Path $ospreyExe)) {
    Write-Host "ERROR: Osprey.exe not found at $ospreyExe" -ForegroundColor Red
    exit 2
}

# --- Stage the data ONCE, for the build's reason: the lanes share a library folder -----
# Download, extraction and the derived decoy-free library are all skip-if-present, so the
# lanes find everything staged and touch none of it; only a first-time machine pays here.
if ($lanes.Count -ge 2) {
    Write-Host '==> Staging regression data once for both lanes' -ForegroundColor Cyan
    foreach ($ds in $selected) {
        & $regression -Dataset $ds -NoBuild -StageOnly
        if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: staging $ds failed (exit $LASTEXITCODE)" -ForegroundColor Red; exit $LASTEXITCODE }
    }
}

if ($lanes.Count -lt 2) {
    Write-Host "==> One lane only ($($selected -join ', ')); running serially" -ForegroundColor Cyan
    # Splat a hashtable rather than appending a conditional array, which would arrive
    # as a POSITIONAL argument rather than as -TeamCity.
    $serial = @{ Dataset = $selected; Threads = $Threads; NoBuild = $true }
    if ($TeamCity) { $serial['TeamCity'] = $true }
    & $regression @serial
    exit $LASTEXITCODE
}

# --- Launch the lanes -------------------------------------------------------------
# A lane runs its datasets SEQUENTIALLY, one regression.ps1 invocation each, driven by
# a small generated script. regression.ps1 therefore keeps its single-valued -Dataset
# and needs no change for any of this.
#
# The obvious alternative - teaching -Dataset to take a list - does not survive the
# process boundary. `pwsh -File` passes arguments literally: space-separated names bind
# only the first and spill the rest onto the next POSITIONAL parameter (measured: they
# landed on -KeepRunDirs, "Cannot convert value StellarGenDecoyEntrap to type Int32"),
# and a comma-joined token arrives as ONE string that fails ValidateSet. Both were
# tested rather than assumed.
$stamp = (Get-Date).ToString('yyyyMMdd_HHmmss')
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$running = @()
foreach ($lane in $lanes) {
    $name = $lane -join '+'
    $log  = Join-Path $LogDir ("regression-lane-{0}-{1}.log" -f ($lane[0]), $stamp)
    $laneScript = Join-Path $LogDir ("regression-lane-{0}-{1}.ps1" -f ($lane[0]), $stamp)

    $tcArg = if ($TeamCity) { " -TeamCity" } else { "" }
    $body = @("`$worst = 0")
    foreach ($ds in $lane) {
        $body += "& `"$regression`" -Dataset $ds -Threads $Threads -NoBuild$tcArg"
        $body += "if (`$LASTEXITCODE -ne 0) { `$worst = `$LASTEXITCODE }"
    }
    $body += "exit `$worst"
    Set-Content -Path $laneScript -Value $body -Encoding UTF8

    # NOT $args - that is an automatic variable, and assigning to it would shadow the
    # one PowerShell maintains for this scope.
    $laneArgs = @('-NoProfile', '-File', $laneScript)
    # Redirects are correct HERE: this script is the long-lived parent and wants its
    # children's output on disk. (The no-redirect rule in long-running-jobs-guide.md is
    # about escaping an agent's job object, which is this script's own launch problem,
    # not its children's.)
    $p = Start-Process pwsh -ArgumentList $laneArgs -PassThru -NoNewWindow `
        -RedirectStandardOutput $log -RedirectStandardError "$log.err"
    Write-Host ("==> lane {0,-45} pid={1}  log={2}" -f $name, $p.Id, (Split-Path $log -Leaf)) -ForegroundColor Cyan
    $running += [pscustomobject]@{ Name = $name; Proc = $p; Log = $log }
}

foreach ($r in $running) { $r.Proc | Wait-Process }
$sw.Stop()

# --- Merge ------------------------------------------------------------------------
# Reported per lane AND totalled, because a lane that aborts still reports zero
# failures - the leg COUNT is what distinguishes a clean lane from a truncated one.
Write-Host ''
Write-Host '=== Parallel regression summary ===' -ForegroundColor Cyan
$totalPass = 0; $totalFail = 0; $totalSkip = 0; $worst = 0
foreach ($r in $running) {
    $text = if (Test-Path $r.Log) { Get-Content $r.Log } else { @() }
    # -CaseSensitive, and it is not a nicety. Select-String is case-INSENSITIVE by default,
    # so ': FAIL' matched the ': fail' inside any "WARN: failed to ..." line the lane emitted -
    # and one of those (a prune racing a previous run's directory) turned a lane that exited 0,
    # passed all 23 legs and printed "Osprey regression PASSED" into "1 FAIL" and an overall
    # FAILED. A gate that cries wolf about its own warnings is worse than one that stays quiet:
    # the next red gets read as this one. The leg lines these count are emitted in upper case by
    # regression.ps1, so requiring that costs nothing.
    $pass = @($text | Select-String -CaseSensitive -Pattern ': PASS').Count
    $fail = @($text | Select-String -CaseSensitive -Pattern ': FAIL').Count
    $skip = @($text | Select-String -CaseSensitive -Pattern ': SKIP').Count
    $totalPass += $pass; $totalFail += $fail; $totalSkip += $skip
    $code = $r.Proc.ExitCode
    if ($code -gt $worst) { $worst = $code }
    $colour = if ($code -eq 0 -and $fail -eq 0) { 'Green' } else { 'Red' }
    Write-Host ("  {0,-45} exit={1}  {2} PASS / {3} FAIL / {4} SKIP" -f $r.Name, $code, $pass, $fail, $skip) -ForegroundColor $colour
    foreach ($line in ($text | Select-String -CaseSensitive -Pattern ': (PASS|FAIL|SKIP)')) {
        Write-Host ("      " + $line.Line.Trim())
    }
    # Warnings still surface - they were only ever miscounted, not unwanted - but as
    # warnings, in their own colour, where nothing tallies them as legs.
    foreach ($line in ($text | Select-String -CaseSensitive -Pattern '^\s*WARN:')) {
        Write-Host ("      " + $line.Line.Trim()) -ForegroundColor Yellow
    }
}
Write-Host ''
Write-Host ("  TOTAL {0} PASS / {1} FAIL / {2} SKIP in {3:hh\:mm\:ss} wall" -f
    $totalPass, $totalFail, $totalSkip, $sw.Elapsed) -ForegroundColor Cyan
Write-Host '  (per-leg phase costs are in each lane log under "Phase cost")'

if ($worst -ne 0 -or $totalFail -gt 0) {
    Write-Host 'Osprey regression FAILED' -ForegroundColor Red
    exit ($(if ($worst -ne 0) { $worst } else { 1 }))
}
Write-Host 'Osprey regression PASSED' -ForegroundColor Green
exit 0
