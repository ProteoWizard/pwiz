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
      mode 4  warm re-run cache-hit assertion - re-runs the IDENTICAL command a
              second time into the already-populated straight-through dir with
              NOTHING invalidated, and asserts that every canonical task reports a
              cache hit, that no per-file recompute line appears, and that the blib
              is byte-identical. Output comparison ALONE is blind to a
              cache-invalidation regression: a re-run that ignores every cache and
              recomputes the whole pipeline from spectra emits exactly the same
              output as one that resumes, so modes 1-3 stay green through a totally
              broken warm resume. Mode 2 does re-run in place, but it invalidates
              Stage 5 first, so it never exercises the all-cached case; it now
              carries the same skip assertion for the tasks its invalidation leaves
              valid. See Test-TaskCacheHits for the driver log lines both key on.
      mode 5  Stage-5 rehydrate self-consistency - invalidates ONLY the SecondPassFDR
              task (the blib + its SecondPassFDR stamp), leaving the FirstPassFDR
              stamp and every 1st-pass sidecar valid, so the re-run rebuilds its
              post-Stage-5 bundle from those OWN sidecars
              (FirstPassFdrTask.LoadOwnReconciliationBundle - the class name differs
              from the task Name). That loader is what no other leg reaches: mode 2
              deletes the FirstPassFDR stamp so the task RUNS instead, mode 4
              invalidates nothing so nothing demands its state, and mode 3's
              PerFileRescoring phase does enter the rehydrate arm but adopts a
              WORKER-supplied bundle, never the own-sidecar loader. The leg asserts
              a marker logged from INSIDE that loader (a cache hit does not prove it
              ran, and neither does the generic rehydrate line, which a worker bundle
              emits too), that SecondPassFDR's blib still equals the straight-through one
              at 1e-9, and that the --model-diagnostics report re-emitted from those
              sidecars matches the golden. Like every other leg it names no token:
              #4536 gave the rehydrate its own per-file survivor loader, so it streams
              the Stage 6 handoff instead of needing one.
      mode 6  library-fragment release engagement (issue #4532) - asserts, from the
              legs' own logs, that the release RAN on every leg that holds the
              library (straight-through, resume, --task PerFileRescoring, and the
              SecondPassFDR node) and did NOT run on --task FirstPassFDR, which loads with
              OmitFragments and can therefore only fabricate a saving. The release
              is OUTPUT-NEUTRAL by design, which is its safety argument and also
              why modes 1-4 pass identically whether it ran or was deleted
              outright: they can catch an OVER-release (the tripwire throws) but
              are structurally blind to it silently not happening. Every defect
              found reviewing #4534 was in that blind spot. Asserts presence and
              non-zero counts, never exact counts. See Test-LibraryFragmentRelease.

      mode 7  --task ModelDiagnostics regeneration acceptance (issue #4573) - re-enters
              the COMPLETED straight-through run and asserts the task's whole contract:
              exactly one artifact changed and it is the report (a regeneration that
              rewrote a sidecar or the blib would corrupt the run it was asked to
              describe), AND the regenerated report still matches the same golden mode 1b
              holds the straight-through report to (a leg that touched nothing but emitted
              a different page is equally broken, and the file check cannot see it).
              Nothing else reaches this task: it declares no outputs, which is precisely
              what makes CanRehydrate return false so it re-runs on demand. Runs last, in
              the straight-through dir, since it rewrites the report there. ~14 s per
              dataset - it rehydrates Stages 1-5 and re-runs Stage 7 only.

    NO dependency on the sibling ai/ checkout: data acquisition, blib golden
    capture/compare, and the tolerance comparators all live under
    pwiz_tools/Osprey/Regression. Mirrors build.ps1's TeamCity service
    messages and tool-discovery; emits a buildProblem + nonzero exit on any
    mismatch.

.PARAMETER Dataset
    One dataset name or All (default). Four exist, and they cover deliberately
    different failure classes:

      Stellar               unit-resolution, generated decoys, no entrapment.
                            The fast local pre-commit loop.
      StellarLibDecoy       the same mzML with a library that SUPPLIES its own
                            decoys plus entrapment peptides. The path we
                            recommend, and the only one that never calls
                            DecoyGenerator.
      StellarGenDecoyEntrap the same entrapment library with the decoy rows
                            stripped, so Osprey GENERATES decoys while the
                            entrapment peptides remain. The only dataset that can
                            measure a decoy-construction regression against a
                            true-FDP oracle.
      Astral                hram, generated decoys, larger and slower.

.PARAMETER CreateGolden
    Capture/refresh the committed golden from this run instead of comparing
    against it. Use only on an intentional, reviewed behavior change.

.PARAMETER SkipResume
    Skip the mode-2 resume self-consistency leg (mode 1 only).

.PARAMETER SkipWarmRerun
    Skip the mode-4 warm re-run cache-hit leg. The overnight gate leaves it on: it
    is the only leg that can see a cache-invalidation regression, and a fully
    cached re-run costs seconds because it runs no task at all. This switch exists
    for the same fast-local-iteration reason as -SkipHpcChain.

.PARAMETER SkipRehydrate
    Skip the mode-5 Stage-5 rehydrate leg. The overnight gate leaves it on: it is
    the only leg that enters FirstPassFdrTask.Rehydrate at all, and it costs one SecondPassFDR
    re-run. This switch is for fast local iteration, like -SkipHpcChain.

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
    [ValidateSet('Stellar', 'StellarLibDecoy', 'StellarGenDecoyEntrap', 'Astral', 'All')]
    [string]$Dataset = 'All',
    [switch]$CreateGolden,
    [switch]$SkipResume,
    [switch]$SkipWarmRerun,
    [switch]$SkipRehydrate,
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

# Turn ON the per-file second-pass verifier for this gate (issue #4486). Stage 7 recomputes each
# file's competition and asserts it against the answer PerFileRescoring wrote.
#
# COSTS THIS GATE NOTHING. The recompute used to be unconditional, so switching it on here is
# exactly today's behaviour; what changed is that it is now OFF by default, which is what lets a
# cohort-scale run stop paying for it (measured at 82 files: 9.2 GB of 1st-pass re-reads against
# the 2.8 GB the fold needs, inside an 860.8 s Stage 7).
#
# Invoke-HpcChain deliberately turns it OFF for its phases - see the note there. That asymmetry
# is what covers the shipped path, and it also costs nothing, because mode 3 already compares the
# chain's output against the straight leg's.
$env:OSPREY_PASS2_VERIFY_WORKER = '1'

# No leg of this gate may run under a resident-pool allowance, so clear any INHERITED
# one rather than merely declining to set it. A TeamCity agent, or a developer shell
# that just ran a Stage 6 A/B, can have OSPREY_ALLOW_UNFIXED_RESIDENT exported; every
# leg of every dataset would then run under that blanket and a regression back onto an
# O(files) resident pool would pass fully green. That is the ten-day
# OSPREY_PASS2_QVALUE=transfer failure the named-token ratchet was built to stop, so
# "the gate sets it nowhere" has to mean the variable is UNSET when Osprey reads it,
# not just that this script never assigns it. Announced rather than silent: an operator
# who deliberately exported it should see why their run behaves differently here.
# --- Known O(files) resident gaps, stated where the gate can be read -----------
# "No leg sets OSPREY_ALLOW_UNFIXED_RESIDENT" is necessary but NOT sufficient as a
# statement of health, and reading it as sufficient is the trap: a token is only
# required where a guard demands one, so a resident path that no guard covers is
# invisible in a token audit. #4536 was exactly that until it landed - the rehydrate
# published no survivor loader, so Stage6ResidentHandoffGuardError no-oped and nothing
# asked for a token. #4486 was the standing example: the survivor buffer is rebuilt for
# SecondPassFDR to read, so it is resident for the whole of Stage 7 on EVERY path (#4597
# moved the rebuild onto SecondPassFDR's own pull, which changes WHO pays for it and not
# how big it is), and no guard covers that because it is not a resume or a mode - it is
# what Stage 7 takes as input. It is still uncovered, and now MEASURED: 0.196 GB/file
# live, post-GC, which is not what fails at scale. What did was the --task SecondPassFDR
# pre-compaction RELOAD at 2.07 GB/file (~186 GB projected at 82 files), streamed by
# #4486. Zero tokens therefore does NOT mean zero gaps, and this table keeps that legible.
#
# Printed in the run summary (not just parked in a comment) so every CI log states the
# outstanding gaps, and so a fixed entry left here shows up as a stale line in output
# rather than as a comment nobody re-reads.
#
# Rules, from ai/docs/osprey-development-guide.md:
#   * token + warning = an operator asked for residency and was told what they got
#   * warning alone on a default path = INSUFFICIENT, allowed only as an interim
#     tripwire with an open issue against it
#   * any token this gate REQUIRES must have an open issue to remove it, and the token
#     comes OUT of the gate when the issue lands. NONE is required today: #4536 gave the
#     rehydrate its own per-file survivor loader, so mode 5 streams the Stage 6 handoff
#     like every other leg and resume-survivor-handoff - the last entry here - came out
#     with it. Zero is the invariant, not a milestone: a new entry below is a regression
#     to justify in review, not a line to add and move on.
$knownResidentGaps = @(
    # Untokened by nature, which is exactly why it belongs here: no guard demands a token
    # for it, so a token audit cannot see it and a green gate printed "none" while every
    # leg walked it. Measured 2026-08-09 on 82 files rather than estimated - the preamble
    # above names it, and a table that omits the one gap the preamble names is worse than
    # no table. Token NONE, so it does not inflate the required-token count below.
    @{
        Issue = '#4486'
        Token = 'NONE'
        Path  = 'SecondPassFDR pulling RescoredEntries rebuilds the whole-run survivor buffer it reads (#4597 moved the build off the end of Stage 6, which does not shrink it); resident for the whole of Stage 7.'
        # One model, stated explicitly: a fixed library term plus a per-file slope, both from
        # the 4/8/16-file A/B. Quoting a straight-through 82-file endpoint next to that rig's
        # marginal slope produced three numbers no single model reproduced (24.43/82 = 0.298,
        # not 0.197), which is unreadable in a summary that prints on every CI run.
        Legs  = 'Every leg of every dataset. ~4.4 GB library + 0.197 GB/file live post-GC: ~20 GB at 82 files, ~103 GB projected at 500.'
    }
)
# Reachable only outside this gate, tokened, each with an open issue:
#   #4507  fdrbench-pass1 -- --fdrbench-pass 1 walks the pre-compaction pool
# hpc-merge is GONE (#4486): --task SecondPassFDR takes the bounded streaming hydrate, so
# mode 3's join node needs no token. That is the ratchet shrinking a third time.
# By design rather than unfinished, so no issue: projection-off and
# compacted-entries-buffer (the A/B byte-identity oracles) and non-percolator-fdr.

# Preserved and RESTORED at the end of the run (see the finally block): this mutates the
# process environment, so a developer running the gate in their interactive shell would
# otherwise silently lose an exported token for the rest of the session.
$script:priorAllowResident = $env:OSPREY_ALLOW_UNFIXED_RESIDENT
# An operator running a deliberate A/B needs their token: OSPREY_STAGE6_STREAM_SURVIVORS=0
# and OSPREY_FDR_PROJECTION=0 force resident paths ON PURPOSE, and clearing the token that
# admits them would abort the gate on its first leg with a guard error - making the very
# comparison this harness exists to support impossible to run. Ambient tokens are stripped
# ONLY when no such switch is set, which is the case the clearing is aimed at.
$abSwitchSet = ($env:OSPREY_STAGE6_STREAM_SURVIVORS -eq '0') -or ($env:OSPREY_FDR_PROJECTION -eq '0')
if (-not [string]::IsNullOrWhiteSpace($env:OSPREY_ALLOW_UNFIXED_RESIDENT)) {
    if ($abSwitchSet) {
        # Extra parens: -f binds TIGHTER than +, so without them only the LAST fragment is
        # formatted and '{0}' survives verbatim into the output.
        Write-Host (("Keeping inherited OSPREY_ALLOW_UNFIXED_RESIDENT='{0}' - an A/B switch " +
            "(OSPREY_STAGE6_STREAM_SURVIVORS/OSPREY_FDR_PROJECTION=0) is set and needs it.") `
            -f $env:OSPREY_ALLOW_UNFIXED_RESIDENT) -ForegroundColor Yellow
    } else {
        Write-Host (("Clearing inherited OSPREY_ALLOW_UNFIXED_RESIDENT='{0}' - no leg of this " +
            "gate may run under an ambient resident-pool allowance.") `
            -f $env:OSPREY_ALLOW_UNFIXED_RESIDENT) -ForegroundColor Yellow
        Remove-Item Env:OSPREY_ALLOW_UNFIXED_RESIDENT -ErrorAction SilentlyContinue
    }
}

# Every Osprey invocation in this run is timestamped and mem-stamped, so each leg's log
# doubles as a memory-band trace: `[yyyy/MM/dd HH:mm:ss]<TAB>managedMB<TAB>privateMB<TAB>`.
# That makes a red-or-slow gate diagnosable after the fact - the log shows whether the
# per-file memory floor climbed (an O(files) regression) and where the tool went silent -
# without re-running anything. Read it with ai/scripts/perfviz.py (numbers: peak, floor
# drift per file, every reporting gap) or ai/scripts/perfviz.html (plot). The prefix costs
# a GC.GetTotalMemory(false) + a process query per emitted line, which is noise next to the
# pipeline itself. See ai/docs/memory-band-guide.md, including why this trace shows SHAPE
# but not live-set MAGNITUDE.
$memStampArgs = @('--timestamp', '--memstamp')

# The mzML data zip on panorama (raw-data zip is future work). The URL's
# second-to-last segment ("perftests") maps to <Downloads>\Perftests.
#
# -v2 adds the stellar-libdecoy library WITHOUT disturbing the v1 zip: acquisition
# is skip-if-present on the EXTRACTED ROOT, so re-publishing under the same name
# would never reach a machine that already has the tree. A new name also leaves
# older branches pinned to the v1 URL working exactly as before.
$dataUrl = 'https://panoramaweb.org/_webdav/MacCoss/software/%40files/perftests/osprey-testfiles-mzML-v2.zip'

# The Stellar library-decoy library, published SEPARATELY from the mzML bundle.
#
# It has to be separate. The bundle is 24.6 GB and its acquisition is
# skip-if-present on the extracted root, so folding a new library into a -v3
# bundle would either never reach a machine that already has the tree, or force
# every machine to re-download the mzML to get a 258 MB library. Splitting the
# two lets a library revision cost only the library.
#
# v3 vs the v2 copy inside the bundle: v2 carries 21 entrapment peptides whose
# I/L-normalised sequence collides with a real target, which target-decoy
# competition then resolves on the target's own signal. An exact-string audit
# shows 0 collisions for BOTH versions - only the I/L-normalised check separates
# them - so the version cannot be told from the library contents at a glance,
# which is exactly why the zip name is the marker.
$libDecoyV3Url = 'https://panoramaweb.org/_webdav/MacCoss/software/%40files/perftests/stellar-libdecoy-v3.zip'

# --- Dataset table (standalone; mirrors ai/ Dataset-Config.ps1) --------------
# Folder = mzML subfolder under the extracted root; Resolution = instrument mode.
# Input mzML files and the .tsv library are discovered from the folder so
# filenames are not hard-coded. Optional keys (absent = today's behavior):
#   LibraryFolder    library lives in a DIFFERENT subfolder than the mzML, so a
#                    second dataset can reuse one copy of the mzML instead of
#                    duplicating gigabytes of it in the zip.
#   GoldenFolder     golden subfolder name; defaults to Folder. Required when two
#                    datasets share a Folder, or their goldens collide.
#   NestedZip        zip inside LibraryFolder holding the library, extracted on
#                    demand so datasets that are not selected cost nothing.
#   LibraryUrl       a library published separately from the mzML bundle,
#                    downloaded into LibraryFolder when its ZIP is absent there
#                    and extracted into a version-named subfolder BESIDE whatever
#                    the bundle staged, never over it. Wins over NestedZip. The
#                    zip file is both the payload and the version marker, so it
#                    stays on disk after extraction.
#   Library          explicit library filename; bypasses the exactly-one-.tsv
#                    discovery rule (the libdecoy folder also holds a manifest).
#   Manifest         FDRBench pairing manifest -> --decoy-pairing-manifest.
#   DecoysInLibrary  -> --decoys-in-library (library-supplied decoys; Osprey
#                    identifies them by protein-accession prefix, NOT the Decoy
#                    column, which is 0 throughout Carafe entrapment libraries).
#   ModelDiagnostics -> --model-diagnostics --fdrbench-pass both, plus the
#                    diagnostics spot-check golden.
#   MaxPass1Fdp      Tier-2 sanity ceiling on Pass-1 true FDP at a reported 1% q
#                    (default 0.02). Committed here and deliberately NOT
#                    regenerated by -CreateGolden -- a rebaseline is exactly how
#                    a calibration regression gets blessed into the baseline, so
#                    this bound must not come from the run. Per-dataset because
#                    the honest range differs by decoy source: library decoys
#                    measure 0.86-1.47%, generated decoys ~2.03% even with the
#                    b<->y swap removed.
#   MaxAbsTilt       Tier-2 ceiling on |null-alignment tilt| (default 1.0), same
#                    do-not-regenerate rule. Also per-dataset.
#   CoinTolerance    Tier-2 tolerance on |entrapment paired-win fraction - 0.5|
#                    (default 0.05), same do-not-regenerate rule. Only bites on a
#                    dataset that carries entrapment.
$datasets = [ordered]@{
    Stellar = @{ Folder = 'stellar'; Resolution = 'unit' }
    StellarLibDecoy = @{
        Folder           = 'stellar'
        LibraryFolder    = 'stellar-libdecoy'
        GoldenFolder     = 'stellar-libdecoy'
        NestedZip        = 'libdecoy-entrapment.zip'
        LibraryUrl       = $libDecoyV3Url
        Library          = 'carafe_spectral_library.tsv'
        Manifest         = 'osprey_library_db_pairing.tsv'
        Resolution       = 'unit'
        DecoysInLibrary  = $true
        ModelDiagnostics = $true
    }
    # The dataset that actually guards DecoyGenerator. Same entrapment library as
    # StellarLibDecoy with the decoy rows stripped, so Osprey GENERATES the decoys
    # while the entrapment peptides remain -- generated decoys measured against a
    # true-FDP oracle. Neither of the other entrapment-free gendecoy datasets nor the
    # library-decoy dataset can catch a decoy-construction regression: the former have
    # nothing to measure FDP against, the latter never calls DecoyGenerator at all.
    #
    # 2.0% ceiling: generated decoys measure ~1.5% on unit-resolution Stellar with the
    # b<->y swap removed, against 0.86% for library decoys. The pre-fix construction
    # measured 11.81% here and would have blown through this by 6x.
    StellarGenDecoyEntrap = @{
        Folder           = 'stellar'
        LibraryFolder    = 'stellar-libdecoy'
        GoldenFolder     = 'stellar-gendecoy-entrap'
        NestedZip        = 'libdecoy-entrapment.zip'
        LibraryUrl       = $libDecoyV3Url
        Library          = 'carafe_spectral_library.tsv'
        StripDecoys      = $true
        Resolution       = 'unit'
        ModelDiagnostics = $true
        MaxPass1Fdp      = 0.02
    }
    # Astral carries no entrapment, so its tier-2 bound is the null-alignment tilt.
    # 0.5 is an honest ceiling with the b<->y swap removed (this branch measures
    # ~0.25); the pre-fix code measured 1.408 with a real paired-win coin of 0.397,
    # i.e. decoys losing 60% of head-to-head pairs against their own targets. This
    # bound would have failed the old construction, which is the point.
    Astral  = @{ Folder = 'astral';  Resolution = 'hram'; ModelDiagnostics = $true
                 MaxAbsTilt = 0.5 }
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

# --- Dataset table self-check -------------------------------------------------
# Two datasets writing the same golden folder would have the second silently
# overwrite the first under -CreateGolden, and compare against the wrong baseline
# otherwise. GoldenFolder defaults to Folder, so this is one forgotten key away and
# is exactly the collision StellarLibDecoy would have hit. Checked over the WHOLE
# table, not just $selected, so -Dataset Stellar cannot hide a bad table. Placed
# after the service-message helpers so a bad table reports as a buildProblem like
# every other failure rather than as a bare stack trace.
$goldenNames = @($datasets.Keys | ForEach-Object {
    $d = $datasets[$_]
    if ($d.GoldenFolder) { $d.GoldenFolder } else { $d.Folder }
})
$dupGolden = @($goldenNames | Group-Object | Where-Object { $_.Count -gt 1 })
if ($dupGolden.Count -gt 0) {
    Write-Problem-Tc ("Dataset table is invalid: golden folder(s) used by more than " +
        "one dataset: {0}. Give each dataset its own GoldenFolder." -f
        (($dupGolden | ForEach-Object { $_.Name }) -join ', '))
    exit 1
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
. (Join-Path $regressionDir 'DiagnosticsGolden.ps1')
. (Join-Path $regressionDir 'FdrSidecars.ps1')
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

# Dataset-specific CLI flags, shared by the straight-through, resume, and HPC
# legs so every leg of a dataset runs the SAME search. Absent keys add nothing,
# which is why the gendecoy datasets keep their exact historical command line.
# Tier-2 sanity bounds for one dataset, as a splattable hashtable. One place, because
# the bounds are read twice (the -CreateGolden refusal and the mode-1b check) and two
# copies of the fallback chain is two chances for a bound to be enforced on one path
# and not the other -- which is precisely the failure tier 2 exists to prevent.
# A key is emitted only when the dataset overrides it, so Test-DiagnosticsSanity's own
# parameter defaults stay the single source of the default values.
function Get-SanityBounds {
    param([hashtable]$Spec)
    $bounds = @{}
    if ($null -ne $Spec.MaxPass1Fdp)  { $bounds['MaxPass1Fdp']  = $Spec.MaxPass1Fdp }
    if ($null -ne $Spec.MaxAbsTilt)   { $bounds['MaxAbsTilt']   = $Spec.MaxAbsTilt }
    if ($null -ne $Spec.CoinTolerance) { $bounds['CoinTolerance'] = $Spec.CoinTolerance }
    return $bounds
}

function Get-DatasetCliArgs {
    param([hashtable]$Spec, [string]$Manifest)
    $extra = @()
    if ($null -eq $Spec) { return $extra }
    if ($Spec.DecoysInLibrary) { $extra += '--decoys-in-library' }
    if ($Manifest) { $extra += @('--decoy-pairing-manifest', $Manifest) }
    # --model-diagnostics is verified output-neutral (it routes the 2nd pass down
    # the resident path instead of the FDR projection, and the two agree
    # byte-for-byte), so it can ride on the golden-compared run rather than
    # needing a second invocation. It populates the Pass 1 AND Pass 2 FDP views on
    # its own: --fdrbench-pass selects which pass an FDRBench INPUT FILE is written
    # for and does nothing at all without --fdrbench (OspreyCommandArgs warns, and
    # FdrBenchInputWriter returns early on an empty output path), so passing it here
    # only produced a warning on every invocation.
    if ($Spec.ModelDiagnostics) { $extra += '--model-diagnostics' }
    return $extra
}

# --- Run one Osprey invocation (no input copies) -------------------------
function Invoke-OspreyRun {
    param([string[]]$Mzmls, [string]$Library, [string]$Resolution, [string]$WorkDir,
          [string]$LogName, [switch]$DumpProteinFdr, [hashtable]$Spec, [string]$Manifest,
          # Appends --task <name>. Exists so a leg that re-enters a COMPLETED run (mode 7)
          # replays this function's own argument list rather than a copy of it - a copy is
          # only a self-consistency oracle until the two drift, and the drift is silent.
          [string]$TaskName,
          # Return a non-zero exit instead of throwing. For the ONE leg that expects Osprey to
          # refuse: a partial rescore under --model-diagnostics has no plan source, and the
          # correct behaviour is a named error with a non-zero exit. Without this the harness
          # treats that refusal as a crash and ABORTS the remaining legs, so the gate cannot
          # assert the guard it exists to check.
          [switch]$AllowNonZeroExit)
    New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null
    $logPath = Join-Path $WorkDir $LogName
    $cliArgs = @()
    foreach ($m in $Mzmls) { $cliArgs += @('-i', $m) }
    $cliArgs += @('-l', $Library, '-o', 'output.blib',
                  '--resolution', $Resolution, '--protein-fdr', '0.01',
                  '--threads', $Threads.ToString(), '--work-dir', $WorkDir)
    $cliArgs += Get-DatasetCliArgs -Spec $Spec -Manifest $Manifest
    $cliArgs += $memStampArgs
    if ($TaskName) { $cliArgs += @('--task', $TaskName) }
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
    if ($exit -ne 0 -and -not $AllowNonZeroExit) { throw "Osprey exited $exit (see $logPath)" }
    # ExitCode is returned ALWAYS, not just under the switch: a caller that did not opt in never
    # reaches here on a failure, so the field is unambiguous - it is 0 unless the caller asked to
    # handle non-zero itself.
    return @{ Wall = $sw.Elapsed; Log = $logPath; ExitCode = $exit }
}

# Resolve a dataset's inputs from the extracted read-only data folder.
# The mzML folder and the library folder can differ, so a second dataset can
# reuse one copy of the mzML rather than duplicating gigabytes of it in the zip.
function Resolve-DatasetInputs {
    param([hashtable]$Spec)
    $dir = Join-Path $extractedRoot $Spec.Folder
    if (-not (Test-Path $dir)) { throw "Dataset folder not found in data: $dir" }
    $mzmls = @(Get-ChildItem -Path $dir -Filter '*.mzML' -File | Sort-Object Name | ForEach-Object { $_.FullName })
    if ($mzmls.Count -eq 0) { throw "No .mzML files in $dir" }

    $libDir = if ($Spec.LibraryFolder) { Join-Path $extractedRoot $Spec.LibraryFolder } else { $dir }
    if (-not (Test-Path $libDir)) { throw "Library folder not found in data: $libDir" }
    # The bundle's own extraction point, captured BEFORE the LibraryUrl branch
    # below can point $libDir at a version subfolder. The repair step needs it
    # because what it repairs is the bundle's tree, not this run's library.
    $bundleLibDir = $libDir

    # LibraryUrl: a library that ships SEPARATELY from the mzML bundle, so a new
    # library version does not force every machine to re-download 24.6 GB of mzML.
    #
    # STRICTLY ADDITIVE. The zip extracts into its OWN version-named subfolder
    # (<libDir>\stellar-libdecoy-v3\), never over the bundle's extraction point.
    # That is not tidiness, it is a correctness requirement: an older checkout of
    # this script knows nothing about LibraryUrl. It resolves
    # <libDir>\carafe_spectral_library.tsv, and its NestedZip branch is
    # skip-if-present on exactly that path. Overwriting it in place would leave
    # that older code silently running the NEW library while believing it had the
    # bundled one - with no marker it understands and no way to repair the
    # directory for its own use. Extracting beside it leaves the bundle's tree
    # untouched, so switching branches keeps working in both directions.
    #
    # The zip on disk is the version marker AND the payload; it is what makes a
    # version change detectable, since every version yields the same three entry
    # names and the extracted files cannot say which one they are.
    $libraryFromUrl = $false
    if ($Spec.LibraryUrl) {
        $zipName = Split-Path -Leaf $Spec.LibraryUrl
        $marker = Join-Path $libDir $zipName
        $versionDir = Join-Path $libDir ([IO.Path]::GetFileNameWithoutExtension($zipName))
        if (-not (Test-Path $marker)) {
            Write-Host "  downloading library $zipName (one time, ~258 MB)..."
            New-Item -ItemType Directory -Path $libDir -Force | Out-Null
            # Download beside the destination and rename in, so an interrupted
            # download cannot leave a truncated file whose NAME says the version
            # is present and suppress every later attempt.
            $tmp = "$marker.part"
            Remove-Item $tmp -Force -ErrorAction SilentlyContinue
            Save-UrlToFile -Url $Spec.LibraryUrl -OutFile $tmp
            # Prove it is a zip BEFORE promoting it to the marker name. A proxy or
            # sign-in interstitial served as HTTP 200 would otherwise be renamed
            # into place, and every later run would skip the download and die in
            # OpenRead forever, with no -Force path to recover.
            try {
                $probe = [System.IO.Compression.ZipFile]::OpenRead($tmp)
                $probe.Dispose()
            } catch {
                Remove-Item $tmp -Force -ErrorAction SilentlyContinue
                throw "Downloaded library is not a readable zip (server error page?): $($Spec.LibraryUrl)"
            }
            Move-Item $tmp $marker -Force
        }
        # Extract only when the payload is not already unpacked. A partially
        # extracted tree still self-heals: the expected library being absent is
        # what triggers a re-extract, and DoNotOverwrite fills in whatever is
        # missing. Testing the extracted file is safe HERE - unlike at the
        # version gate above - because the version dir already pins the version,
        # so this is only asking "did the unpack finish", not "which library is this".
        $expectedFromUrl = Join-Path $versionDir $Spec.Library
        if (-not (Test-Path $expectedFromUrl)) {
            Write-Host "  extracting $zipName into $(Split-Path -Leaf $versionDir) (one time)..."
            Expand-ZipNoOverwrite -ZipPath $marker -DestFolder $versionDir
            if (-not (Test-Path $expectedFromUrl)) {
                throw "Library zip did not yield $($Spec.Library): $marker"
            }
        }
        # Everything downstream - the library, the pairing manifest, the derived
        # decoy-free copy - resolves inside the version dir from here on.
        $libDir = $versionDir
        $libraryFromUrl = $true
    }

    # SELF-HEAL a tree that the pre-2026-08-20 LibraryUrl acquisition clobbered.
    #
    # That version extracted the separately-published library OVER the bundle's
    # extraction point with -Overwrite. A machine that ran it holds the NEW
    # library under the bundled library's names, with nothing on disk recording
    # the swap. This script is no longer fooled - it resolves its own library
    # from the version subfolder - but anything else reading the bundle's tree is:
    # an older checkout, another branch, and the golden comparison, which is how
    # this surfaced (a session spent hours proving the golden was fine while the
    # library under it had been replaced).
    #
    # Repaired from the bundle's own nested zip, which is already on disk, so the
    # fix costs no download and no manual cleanup - the affected machines include
    # a TeamCity agent whose data lives under the agent user's profile.
    #
    # Runs regardless of $libraryFromUrl: the whole point is to restore the tree
    # this run is NOT using, and only a run that takes the URL path can have
    # damaged it.
    if ($Spec.NestedZip) {
        $bundleNested = Join-Path $bundleLibDir $Spec.NestedZip
        if (Test-Path $bundleNested) {
            $stale = @(Get-ZipEntryMismatches -ZipPath $bundleNested -DestFolder $bundleLibDir)
            if ($stale.Count -gt 0) {
                Write-Host "  repairing $($stale.Count) bundled library file(s) overwritten by a separately-shipped library..."
                foreach ($f in $stale) {
                    Write-Host "    restoring $(Split-Path -Leaf $f)"
                    Remove-Item $f -Force
                }
                Expand-ZipNoOverwrite -ZipPath $bundleNested -DestFolder $bundleLibDir
                $stillStale = @(Get-ZipEntryMismatches -ZipPath $bundleNested -DestFolder $bundleLibDir)
                if ($stillStale.Count -gt 0) {
                    throw "Could not restore bundled library from $bundleNested (still mismatched: $($stillStale -join ', '))"
                }
            }
        }
    }

    # Nested zip: the library ships compressed inside the outer zip and is
    # extracted only when its dataset is actually selected, so a run that does
    # not use it never pays the multi-GB extraction. Skip-if-present, like the
    # outer acquisition. Kept for backward compatibility with bundles whose
    # dataset spec carries no LibraryUrl.
    if ($Spec.NestedZip -and -not $libraryFromUrl) {
        $expected = Join-Path $libDir $Spec.Library
        if (-not (Test-Path $expected)) {
            $nested = Join-Path $libDir $Spec.NestedZip
            if (-not (Test-Path $nested)) { throw "Nested library zip not found: $nested" }
            Write-Host "  extracting nested library zip $($Spec.NestedZip) (one time)..."
            Expand-ZipNoOverwrite -ZipPath $nested -DestFolder $libDir
            if (-not (Test-Path $expected)) { throw "Nested zip did not yield $($Spec.Library): $nested" }
        }
    }

    if ($Spec.Library) {
        # Explicit name: the libdecoy folder also holds the pairing manifest, so
        # the exactly-one-.tsv discovery rule below cannot apply.
        $library = Join-Path $libDir $Spec.Library
        if (-not (Test-Path $library)) { throw "Library not found: $library" }
    } else {
        $libs = @(Get-ChildItem -Path $libDir -Filter '*.tsv' -File | ForEach-Object { $_.FullName })
        if ($libs.Count -ne 1) { throw "Expected exactly one .tsv library in $libDir, found $($libs.Count)" }
        $library = $libs[0]
    }

    # StripDecoys: derive a decoy-free copy of the library so Osprey GENERATES the
    # decoys while the entrapment peptides survive. That combination is what lets the
    # true-FDP bound guard DecoyGenerator: the library-decoy dataset carries entrapment
    # but never calls DecoyGenerator, and the plain gendecoy datasets call it but have
    # no entrapment to measure against, so neither can catch a decoy-construction
    # regression on its own.
    #
    # Derived into gitignored scratch, NOT into the read-only data dir (which the
    # run-level assertion watches). Cached across runs and rebuilt when the source
    # library is newer.
    #
    # Rows are dropped on the decoy_ PREFIX of ProteinID. The Decoy column is 0 on
    # every row of these Carafe libraries, so filtering on it is a silent no-op that
    # yields a byte-identical "stripped" file.
    if ($Spec.StripDecoys) {
        $derivedDir = Join-Path $scriptRoot 'TestResults\_derived'
        New-Item -ItemType Directory -Path $derivedDir -Force | Out-Null
        # The derived name carries the ACQUISITION MARKER, not just the library's own filename.
        # Both library versions extract to the same carafe_spectral_library.tsv, so a name keyed
        # only on that would be reused across a version change - and the mtime check below cannot
        # catch it, because ExtractToFile stamps the extracted file with the ZIP ENTRY's timestamp
        # rather than the extraction time. A machine that derived from the old library AFTER the
        # new zip was built therefore has a derived file NEWER than its source, reuses it, and
        # silently runs the retired library while reporting success. That is the same
        # "leave the old library in place and report success" failure the -Overwrite switch was
        # added to prevent, displaced one layer down.
        $derivedStem = [IO.Path]::GetFileNameWithoutExtension($library)
        if ($Spec.LibraryUrl) {
            $derivedStem += '.' + [IO.Path]::GetFileNameWithoutExtension((Split-Path -Leaf $Spec.LibraryUrl))
        }
        $stripped = Join-Path $derivedDir ($derivedStem + '.nodecoy.tsv')
        $srcInfo = Get-Item $library
        if ((-not (Test-Path $stripped)) -or ((Get-Item $stripped).LastWriteTimeUtc -lt $srcInfo.LastWriteTimeUtc)) {
            Write-Host "  deriving decoy-free library (one time, ~1 min)..."
            # Write to a temp file and rename into place, so the final path only ever
            # holds a complete derivation. An interrupted run (Ctrl-C, a cancelled
            # TeamCity build, an agent reboot) otherwise leaves a TRUNCATED file whose
            # mtime is newer than the source library, so the staleness check above
            # accepts it and EVERY later run fails deep inside the library parse with
            # "Missing PrecursorCharge at row N" - an error naming the library rather
            # than the interruption that caused it. Observed 2026-07-29.
            $tmp = "$stripped.tmp"
            Remove-Item $tmp -Force -ErrorAction SilentlyContinue
            $kept = 0; $dropped = 0
            $reader = [IO.StreamReader]::new($library)
            $writer = [IO.StreamWriter]::new($tmp, $false, [Text.UTF8Encoding]::new($false))
            try {
                $header = $reader.ReadLine()
                if ($null -eq $header) { throw "Empty library: $library" }
                $writer.WriteLine($header)
                $protCol = [array]::IndexOf(($header -split "`t"), 'ProteinID')
                if ($protCol -lt 0) { throw "No ProteinID column in $library" }
                while ($null -ne ($line = $reader.ReadLine())) {
                    $fields = $line -split "`t"
                    if ($fields.Length -gt $protCol -and $fields[$protCol].StartsWith('decoy_', [StringComparison]::Ordinal)) {
                        $dropped++
                    } else {
                        $writer.WriteLine($line); $kept++
                    }
                }
            } finally { $writer.Dispose(); $reader.Dispose() }
            if ($dropped -eq 0) { throw "StripDecoys removed nothing from $library -- the decoy_ convention changed" }
            # Only now is the derivation known good, so publish it atomically.
            Move-Item $tmp $stripped -Force
            Write-Host ("  derived {0}: kept {1:N0} rows, dropped {2:N0} decoy rows" -f (Split-Path -Leaf $stripped), $kept, $dropped)
        }
        $library = $stripped
    }

    $manifest = $null
    if ($Spec.Manifest) {
        $manifest = Join-Path $libDir $Spec.Manifest
        if (-not (Test-Path $manifest)) { throw "Pairing manifest not found: $manifest" }
    }
    return @{ Dir = $dir; LibDir = $libDir; Mzmls = $mzmls; Library = $library; Manifest = $manifest }
}

# Snapshot file sizes + mtimes of the read-only data dir, to assert no-copy
# leaves it untouched after the run.
function Get-DirFingerprint {
    <#
    Size + mtime of every file under Dir, keyed by path RELATIVE to Dir so
    subfolders are covered too. Recursive: a run that drops artifacts into a
    subfolder of a read-only data dir is just as much a violation as one that
    drops them at the top level.
    #>
    param([string]$Dir)
    $fp = @{}
    $root = (Resolve-Path $Dir).Path.TrimEnd('\', '/')
    foreach ($f in Get-ChildItem -Path $Dir -File -Recurse -ErrorAction SilentlyContinue) {
        $rel = $f.FullName.Substring($root.Length).TrimStart('\', '/')
        $fp[$rel] = "$($f.Length):$($f.LastWriteTimeUtc.Ticks)"
    }
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

# Invoke-ResumeInvalidation (invalidate the Stage 5 join + blib so a re-run
# resumes rather than recomputing from spectra) now lives in
# Regression\RegressionData.ps1, dot-sourced above, so the ai-side
# cumulative-coverage harness shares one definition instead of keeping a copy
# that can drift. Mirrors the proven repro from
# TODO-20260605_ospreysharp_resume_reconciled_rt.

# --- Warm-rerun cache assertions (modes 2 and 4) ------------------------------
# Comparing output can never establish that a re-run RESUMED. A run that ignores
# every cache and recomputes the whole pipeline from spectra emits byte-identical
# output, so every leg here stays green through a total cache-invalidation
# regression - which has now happened twice. The only evidence of a cache hit is
# the driver's own log, so these helpers read it.
#
# The canonical four-task pipeline, in execution order. These are the
# OspreyTask.Name values (AnalysisPipeline.CanonicalPipeline): the same tokens
# Invoke-ResumeInvalidation keys off, and the ones the driver stamps into both its
# [TASK] log lines and the .<Name>.osprey.task validity sidecars.
$pipelineTaskNames = @('PerFileScoring', 'FirstPassFDR', 'PerFileRescoring', 'SecondPassFDR')

# The driver's own per-task markers: the cache-hit skip at the top of the task
# loop (AnalysisPipeline.CanRehydrate arm) and RunTask's start line. An included
# task emits exactly one of the two, so a task showing NEITHER is classified
# 'absent' and fails the assertion. That is deliberate. If this wording ever drifts
# from the C#, the leg goes red and names the token it could not find, rather than
# degrading into the silent pass that is the whole failure mode being closed here.
$taskSkipMarker = ':skipping (outputs valid)'
$taskRunMarker  = ':starting'

# The expensive per-file recompute lines: Stage 1-4 reading spectra
# (PerFileScoringTask) and Stage 6 rescoring (PerFileRescoreTask). Matched
# case-SENSITIVELY below, because 'Re-scoring file ' would otherwise also satisfy a
# 'Scoring file ' probe under PowerShell's default case-insensitive comparison and
# a re-run that redid every Stage 1-4 file would look clean.
$coldScoreMarker   = 'Scoring file '
$coldRescoreMarker = 'Re-scoring file '

# The library-fragment release (issue #4532). Two scopes, distinguished by the
# tail of the line: FirstPassFdrTask retains "for rescore + gap-fill", SecondPassFdrTask
# retains "for the reported pool". Captured rather than merely matched, because
# the count is the whole point -- see Test-LibraryFragmentRelease.
$releaseLinePattern =
    'Released library fragments for (\d+) of (\d+) entries \((\d+) base_ids retained for ([^)]+)\)'
$releaseScopeRescore  = 'rescore + gap-fill'
$releaseScopeReported = 'the reported pool'

function Get-TaskCacheMap {
    <#
    Classify every canonical task in one run log as 'skipped' (cache hit), 'ran'
    (recomputed), or 'absent' (neither marker present). Osprey runs here with
    --timestamp --memstamp, so every line carries a prefix and the markers are
    matched ANYWHERE in the line. String.Contains is ordinal and case-sensitive,
    which is what the marker comments above rely on. The trailing ':' in each
    marker is what keeps PerFileScoring from matching PerFileRescoring.
    #>
    param([string[]]$Lines)
    $map = [ordered]@{}
    foreach ($t in $pipelineTaskNames) {
        $skipText = "[TASK] $t$taskSkipMarker"
        $runText  = "[TASK] $t$taskRunMarker"
        $skipped = $false
        $ran     = $false
        foreach ($line in $Lines) {
            if ($line.Contains($skipText)) { $skipped = $true }
            elseif ($line.Contains($runText)) { $ran = $true }
        }
        $map[$t] = if ($skipped -and $ran) { 'skipped+ran' }
                   elseif ($skipped) { 'skipped' }
                   elseif ($ran) { 'ran' }
                   else { 'absent' }
    }
    return $map
}

function Test-TaskCacheHits {
    <#
    Assert that a re-run into an already-populated work dir actually resumed.

    -ExpectSkipped names the tasks whose outputs the leg left valid on disk: each
    must report a cache hit. -ExpectRan names the tasks the leg deliberately
    invalidated: each must recompute, which is what proves the invalidation still
    bites (a cache hit there would make the leg vacuous). -NoColdScoring and
    -NoColdRescoring additionally require that the matching per-file recompute
    lines are absent, so a task-level cache hit cannot hide per-file work.

    Returns the { Pass; Issues } shape the blib and diagnostics comparators use, so
    a caller reports it exactly like any other leg.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$LogPath,
        [string[]]$ExpectSkipped = @(),
        [string[]]$ExpectRan = @(),
        [switch]$NoColdScoring,
        [switch]$NoColdRescoring
    )
    $issues = [System.Collections.Generic.List[string]]::new()
    if (-not (Test-Path -LiteralPath $LogPath)) {
        $issues.Add("run log not found: $LogPath")
        return @{ Pass = $false; Issues = $issues }
    }
    $logName = Split-Path -Leaf $LogPath
    $lines = @(Get-Content -LiteralPath $LogPath)
    $map = Get-TaskCacheMap -Lines $lines
    foreach ($t in $ExpectSkipped) {
        if ($map[$t] -eq 'skipped') { continue }
        # Extra parens around each concatenation: -f binds TIGHTER than +, so without
        # them only the LAST fragment is formatted and the placeholders in the earlier
        # fragments survive verbatim into the failure message.
        if ($map[$t] -eq 'absent') {
            # Neither marker present. Either the task was not included in this run, or
            # the C# log wording drifted from the tokens above. Say so rather than
            # reporting it as a recompute, because the fix is different.
            $issues.Add((("{0}: task {1} logged NEITHER '{2}' nor '{3}' - it was not " +
                "included in this run, or the driver's log wording has drifted from " +
                "AnalysisPipeline.cs and this assertion is no longer reading anything") -f
                $logName, $t, $taskSkipMarker, $taskRunMarker))
        } else {
            $issues.Add((("{0}: task {1} is '{2}', expected 'skipped' - no '[TASK] {1}{3}' " +
                "line, so the re-run recomputed a task whose cached outputs were valid") -f
                $logName, $t, $map[$t], $taskSkipMarker))
        }
    }
    foreach ($t in $ExpectRan) {
        if ($map[$t] -ne 'ran') {
            $issues.Add((("{0}: task {1} is '{2}', expected 'ran' - this leg invalidated its " +
                "outputs, so anything else means the invalidation no longer bites and the " +
                "leg proves nothing") -f $logName, $t, $map[$t]))
        }
    }
    $coldChecks = @()
    if ($NoColdScoring)   { $coldChecks += $coldScoreMarker }
    if ($NoColdRescoring) { $coldChecks += $coldRescoreMarker }
    foreach ($marker in $coldChecks) {
        $hits = @($lines | Where-Object { $_.Contains($marker) })
        if ($hits.Count -gt 0) {
            $issues.Add((("{0}: {1} per-file recompute line(s) containing '{2}' - the re-run " +
                "redid work it already had cached; first: {3}") -f
                $logName, $hits.Count, $marker, $hits[0].Trim()))
        }
    }
    return @{ Pass = ($issues.Count -eq 0); Issues = $issues }
}

# The line FirstPassFDR logs from the OWN-SIDECAR STREAMING arm specifically
# (FirstPassFdrTask.StreamOwnReconciliationBundle).
#
# Deliberately not the 'Bundle hydration: skipping first-pass Percolator' line at the
# top of Rehydrate. That one is emitted before the bundle source is even known, so a
# worker-supplied bundle logs it too - it witnesses "Rehydrate ran", which is NOT what
# mode 5 is here to prove. Mode 3's PerFileRescoring phase also enters Rehydrate (with
# a worker bundle), so "Rehydrate ran" is not even unique to this leg. What IS unique
# is LoadOwnReconciliationBundle rebuilding the bundle from this run's own sidecars,
# and this marker is emitted from inside it.
#
# Matched as a substring for the reason Get-TaskCacheMap matches its own: if the C#
# wording drifts, mode 5 goes red naming the token it could not find rather than
# passing vacuously.
$firstPassFdrRehydrateMarker = 'Resume rehydrate: streaming the first-pass bundle from'

# Mode 3's phase-3 worker must take the PER-RUN hydrate - each run loaded from its own
# artifacts - rather than the all-runs builder kept for the straight-through pipeline.
#
# The OUTCOME cannot tell those apart here, and that is the whole reason this marker exists: a
# phase-3 node is given ONE run, where the all-runs path is O(1) and produces identical bytes.
# So mode 3 would stay green if the per-run path silently stopped being selected, and the
# regression would only surface as an O(runs) startup on a cohort nobody runs in the gate.
# Asserting the outcome alone is exactly what let an earlier resume fix report success while
# testing the old path (defect (b2), TODO-20260901_osprey_firstpassfdr_resume).
$perRunHydrateMarker = 'Per-run rescore: hydrating each of'

# FirstPassFDR's half of the same shape: on a rehydrate where the analysis-wide summary is on
# disk it publishes the survivor loader and builds no experiment-wide bundle, so it emits this
# instead of $firstPassFdrRehydrateMarker. Mode 5 accepts either.
$firstPassFdrPerRunMarker = 'Per-run rescore: FirstPassFDR publishes the survivor loader only'

function Test-LogMarker {
    <#
    Assert a run log contains a marker line proving a specific code path executed.
    Returns the { Pass; Issues } shape every other comparator returns, so a caller
    reports it exactly like any other leg. -Description names what the marker
    proves, so a failure says which path did not run rather than quoting a string.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$LogPath,
        [Parameter(Mandatory = $true)][string]$Marker,
        [Parameter(Mandatory = $true)][string]$Description
    )
    $issues = [System.Collections.Generic.List[string]]::new()
    if (-not (Test-Path -LiteralPath $LogPath)) {
        $issues.Add("run log not found: $LogPath")
        return @{ Pass = $false; Issues = $issues }
    }
    $logName = Split-Path -Leaf $LogPath
    $lines = @(Get-Content -LiteralPath $LogPath)
    $found = @($lines | Where-Object { $_.Contains($Marker) })
    if ($found.Count -eq 0) {
        $issues.Add((("{0}: no line containing '{1}' - {2} did not happen, or the C# log " +
            "wording has drifted and this assertion is no longer reading anything") -f
            $logName, $Marker, $Description))
    }
    return @{ Pass = ($issues.Count -eq 0); Issues = $issues }
}

function Get-ReleaseLogFacts {
    <#
    Parse every library-fragment release line out of one leg's log. Returns an
    array of { Released; Entries; Retained; Scope } in log order, empty when the
    leg logged none.
    #>
    param([Parameter(Mandatory = $true)][string]$LogPath)
    $facts = [System.Collections.Generic.List[hashtable]]::new()
    if (-not (Test-Path -LiteralPath $LogPath)) { return $facts }
    foreach ($line in (Get-Content -LiteralPath $LogPath)) {
        if ($line -match $releaseLinePattern) {
            $facts.Add(@{
                Released = [int]$Matches[1]
                Entries  = [int]$Matches[2]
                Retained = [int]$Matches[3]
                Scope    = $Matches[4]
            })
        }
    }
    return $facts
}

function Test-LibraryFragmentRelease {
    <#
    Assert that the library-fragment release (#4532) actually RAN on the legs that
    must release, and did NOT run on the leg that must not.

    This is the one leg-level assertion the blib comparators structurally cannot
    make. The release is OUTPUT-NEUTRAL by design -- that is its safety argument --
    so mode 1 proves it is HARMLESS, never that it HAPPENED. Deleting the
    production call site leaves every other mode green. Split the failure modes:

      * releases TOO MUCH -> the released-spectrum tripwire throws when Stage 6 or
        the blib reads a released entry, and the leg dies. Already covered.
      * does NOT run      -> output byte-identical, saving silently gone.
      * runs but frees 0  -> output identical, log claims a saving it never made.

    Every defect /code-review found on #4534 was in the last two columns: the
    Rehydrate path never released, --task FirstPassFDR printed millions released
    having freed ZERO bytes directly above a [MEM] probe, and the SecondPassFDR node
    realized nothing at all. None was an over-release. This closes that column.

    -ExpectScopes names the release scopes the log must contain. -RequireFreed
    names the subset that must additionally have freed a non-zero count; it is a
    separate list because the SecondPassFDR node legitimately reports 0 on a
    straight-through run, where FirstJoin already released in the same process and
    ReleaseSpectrum is idempotent -- requiring non-zero there would fail every
    in-process run, while requiring it on the HPC SecondPassFDR node is exactly the
    assertion that would have caught the zero-saving defect. -ExpectNone asserts
    the log contains no release line at all, which pins --task FirstPassFDR: it
    loads with OmitFragments, so a release there can only ever be the fabricated
    kind.

    Asserts PRESENCE and non-zero, never exact counts -- those move with any
    scoring change, and a gate that cries wolf stops being read.

    Returns the { Pass; Issues } shape every other comparator returns.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$LogPath,
        [string[]]$ExpectScopes = @(),
        [string[]]$RequireFreed = @(),
        [switch]$ExpectNone
    )
    $issues = [System.Collections.Generic.List[string]]::new()
    if (-not (Test-Path -LiteralPath $LogPath)) {
        $issues.Add("release assertion: run log not found: $LogPath")
        return @{ Pass = $false; Issues = $issues }
    }
    $logName = Split-Path -Leaf $LogPath
    # @() is REQUIRED, not defensive habit. A function returning a List<T> UNROLLS it
    # into the pipeline, so this arrives as $null for an empty result and -- the case
    # that bites -- as a BARE HASHTABLE for a single result, whose .Count is its KEY
    # count (3) and whose [0] is $null. A one-line log is exactly what the ExpectNone
    # leg looks like when it fires, so without this the fabricated-saving failure
    # message renders its counts blank precisely when someone needs to read them.
    $facts = @(Get-ReleaseLogFacts -LogPath $LogPath)

    if ($ExpectNone) {
        if ($facts.Count -gt 0) {
            $issues.Add((("{0}: {1} release line(s), expected NONE - this leg loads the " +
                "library with OmitFragments, so every entry already holds the shared " +
                "Array.Empty and a release here frees nothing while reporting that it did; " +
                "first: released {2} of {3}") -f
                $logName, $facts.Count, $facts[0].Released, $facts[0].Entries))
        }
        # Matched is reported so the CALLER can tell "correctly released nothing" from
        # "the pattern is dead". On its own an -ExpectNone check cannot: a reworded C#
        # log line makes $facts empty and this branch reports PASS having verified
        # nothing. The run-wide liveness assertion in the mode 6 block closes that -
        # a negative assertion cannot fail closed by itself.
        return @{ Pass = ($issues.Count -eq 0); Issues = $issues; Matched = $facts.Count }
    }

    foreach ($scope in $ExpectScopes) {
        $matching = @($facts | Where-Object { $_.Scope -eq $scope })
        if ($matching.Count -eq 0) {
            $issues.Add((("{0}: no release line for scope '{1}' - either the release no " +
                "longer runs on this leg (the saving is gone and nothing else can see it), " +
                "or the C# log wording drifted from this assertion and it is reading " +
                "nothing. Scopes present: {2}") -f
                $logName, $scope,
                $(if ($facts.Count) { ($facts.Scope | Sort-Object -Unique) -join ', ' } else { '(none)' })))
            continue
        }
        if ($RequireFreed -notcontains $scope) { continue }
        if ($matching[0].Released -le 0) {
            $issues.Add((("{0}: release line for scope '{1}' freed 0 of {2} entries - the " +
                "call site ran but released nothing, which is the fabricated-saving shape") -f
                $logName, $scope, $matching[0].Entries))
        }
        if ($matching[0].Retained -le 0) {
            $issues.Add((("{0}: release line for scope '{1}' retained 0 base_ids - the " +
                "retained set is empty, so the next reader of any spectrum will trip the " +
                "released tripwire") -f $logName, $scope))
        }
    }
    return @{ Pass = ($issues.Count -eq 0); Issues = $issues; Matched = $facts.Count }
}

# --- mode 3: HPC 4-task worker chain ------------------------------------------
# Low-level runner for a single --task phase: CWD = its own scratch dir so the
# task's CWD-relative outputs (parquets, sidecars, blib) land there, mirroring a
# real HPC worker that ships only its inputs and writes beside them. Throws on a
# non-zero exit so the chain aborts loudly at the failing phase.
function Invoke-OspreyTaskRun {
    <#
    Run one --task phase of the HPC chain, and assert it MODIFIED NOTHING IT WAS GIVEN.

    A phase receives its inputs by copy and produces its outputs as new files. Modifying a
    file that was already in the directory is a phase reaching back into another task's
    artifact - the exact violation that let PatchPep rewrite every per-run 2nd-pass sidecar
    after the experiment fold, breaking the write-once contract those files are supposed to
    have and requiring the experiment-wide node to hold write access to output it does not
    own (issue #4486).

    Mode 3 is the right place for this check because here task boundaries ARE process
    boundaries: we know exactly which task ran, so a modification can be attributed. The
    straight-through legs cannot say that, which is why the in-code write-once guard on
    FdrScoresSidecar exists as well - the two catch different halves. New and removed files
    are NOT flagged: producing outputs is the job, and each phase cleans up after itself.
    #>
    param([string]$WorkDir, [string[]]$CliArgs, [string]$LogName)
    New-Item -ItemType Directory -Path $WorkDir -Force | Out-Null
    $before = Get-DirFingerprint -Dir $WorkDir
    $logPath = Join-Path $WorkDir $LogName
    Push-Location $WorkDir
    try {
        & $ospreyExe @CliArgs 2>&1 | Tee-Object -FilePath $logPath | Out-Null
        $exit = $LASTEXITCODE
    } finally {
        Pop-Location
    }
    if ($exit -ne 0) { throw "Osprey --task exited $exit (see $logPath)" }
    # Logs excluded: this phase writes its own, and a re-run appends to it.
    $touched = @(Compare-DirFingerprint -Before $before -Dir $WorkDir |
        Where-Object { $_ -like 'modified:*' -and $_ -notmatch '\.log$' })
    if ($touched.Count -gt 0) {
        throw ("Osprey --task modified {0} file(s) it was given, which no task may do: [{1}]. " +
               "A phase produces new artifacts; rewriting one it received means a later stage " +
               "is reaching back into an earlier stage's output, so that file no longer matches " +
               "the validity sidecar attesting it. See issue #4486. Log: {2}") -f
              $touched.Count, ($touched -join ', '), $logPath
    }
}

# Stage the library (+ its .libcache when present) into a phase dir.
function Copy-LibraryInto {
    param([string]$Library, [string]$Dir, [string]$Manifest)
    Copy-Item $Library (Join-Path $Dir (Split-Path -Leaf $Library))
    $libCache = $Library + '.libcache'
    if (Test-Path $libCache) { Copy-Item $libCache (Join-Path $Dir (Split-Path -Leaf $libCache)) }
    # The pairing manifest is an input like the library: each phase runs with its
    # own dir as CWD and references it by leaf name, so it must be staged too.
    if ($Manifest) { Copy-Item $Manifest (Join-Path $Dir (Split-Path -Leaf $Manifest)) }
}

# Run the distributed --task pipeline end to end against copied inputs under
# $ChainRoot and return the final SecondPassFDR blib. Each phase rehydrates the
# prior phase's on-disk sidecars, exactly as a multi-computer distribution would;
# nothing is held in memory across phases. All inputs/sidecars are copied (never
# referenced in place), so the read-only data dir is untouched.
function Invoke-HpcChain {
    param([string[]]$Mzmls, [string]$Library, [string]$Resolution, [string]$ChainRoot,
          [hashtable]$Spec, [string]$Manifest,
          # Directory holding the straight-through leg's .spectra.bin. Phase 1 is
          # seeded from these instead of the mzML - see the block that stages them.
          [string]$CacheSource)
    # THE SHIPPED PATH RUNS HERE, and this is the whole of what it costs to gate it.
    #
    # The straight leg runs with OSPREY_PASS2_VERIFY_WORKER=1, so Stage 7 recomputes and asserts.
    # This chain runs with it OFF, which is the configuration a real cohort run uses: Stage 7
    # folds the worker's written answer and never opens a 1st-pass sidecar. Mode 3 then compares
    # this chain's output against the straight leg's - a comparison it already made - so the
    # verified and unverified paths are proven to agree at ZERO added run time.
    #
    # Without the asymmetry the gate would only ever exercise the path we do NOT ship, which is
    # the exact shape of defect this sprint keeps finding: a green check on code that production
    # does not run.
    $priorVerifyWorker = $env:OSPREY_PASS2_VERIFY_WORKER
    $env:OSPREY_PASS2_VERIFY_WORKER = $null
    try {
    $libName = Split-Path -Leaf $Library
    # Every phase gets the SAME dataset flags as the straight-through run -- the
    # chain is only a self-consistency oracle if both sides run the same search.
    # The manifest is referenced by leaf name because each phase runs with its own
    # staged dir as CWD.
    $manifestName = if ($Manifest) { Split-Path -Leaf $Manifest } else { $null }
    $extraArgs = Get-DatasetCliArgs -Spec $Spec -Manifest $manifestName
    # Stable, file-order stem list (NOT hashtable key order) so the --input-scores
    # argument order matches the straight-through's file order deterministically.
    $stemList = @($Mzmls | ForEach-Object { [IO.Path]::GetFileNameWithoutExtension($_) })
    $mzmlByStem = @{}
    foreach ($m in $Mzmls) { $mzmlByStem[[IO.Path]::GetFileNameWithoutExtension($m)] = $m }

    # Phase logs are copied here as each phase finishes, because the phase DIRS are
    # freed mid-chain to bound peak disk (phases 1, 2 and every phase-3 worker go
    # before the SecondPassFDR node even starts). A leg-level assertion therefore cannot read
    # them where they were written -- mode 6 found exactly that, seeing only phase 4.
    # A few KB of text survives; the multi-GB inputs still do not.
    $chainLogDir = Join-Path $ChainRoot 'logs'
    New-Item -ItemType Directory -Path $chainLogDir -Force | Out-Null

    # Phase 1: per-file raw workers (Stage 1-4). Writes <stem>.scores.parquet +
    # <stem>.calibration.json per file.
    # ONE PROCESS PER FILE, in its own directory, because that is what PerFileScoring is.
    # Running all three mzMLs through a single process proved the task works on a node that can
    # see every file - the one condition an HPC worker never has. A worker that reached for a
    # sibling's data would have passed here and failed in production.
    $ph1Root = Join-Path $ChainRoot 'phase1_scoring'
    New-Item -ItemType Directory -Path $ph1Root -Force | Out-Null
    $ph1Dirs = @{}
    foreach ($stem in $stemList) {
        $ph1Dirs[$stem] = Join-Path $ph1Root $stem
        New-Item -ItemType Directory -Path $ph1Dirs[$stem] -Force | Out-Null
    }
    # Seeded with the straight-through leg's CACHES, not the mzML. Phase 1 is the only HPC
    # phase that reads spectra and it deletes the copied mzML right after running anyway, so
    # this costs nothing, stops copying multi-GB inputs, and makes the phase the gate on
    # PerFileScoring reading a .spectra.bin with no source. Mode 2 cannot cover that - it
    # requires PerFileScoring to be SKIPPED, and a skipped task opens no cache. Mode 3's
    # chain==straight comparison is what proves the results identical.
    #
    # Also the only leg covering the UNREDIRECTED lookup: phase 1 passes no --work-dir, so
    # ResolveCacheDir returns the input directory, which is what an operator gets by default.
    foreach ($stem in $stemList) {
        $d1 = $ph1Dirs[$stem]
        $srcCache = Join-Path $CacheSource "$stem.spectra.bin"
        if (-not (Test-Path -LiteralPath $srcCache)) {
            throw "regression: HPC phase 1 needs the straight-through spectra cache '$srcCache'; the cache-only PerFileScoring gate cannot run without it."
        }
        # THIS worker's data and nothing else: its own spectra cache and the library. No
        # sibling's cache or parquet is present, and NO mzML - Osprey tolerates a missing
        # data file once the .spectra.bin exists, so the chain stages what an orchestrator
        # would and no longer fakes an input file to satisfy path derivation.
        Copy-Item -LiteralPath $srcCache (Join-Path $d1 "$stem.spectra.bin")
        Copy-LibraryInto -Library $Library -Dir $d1 -Manifest $Manifest
        $a1 = @('-i', "$stem.mzML",
                '-l', $libName, '-o', 'output.blib', '--resolution', $Resolution,
                '--protein-fdr', '0.01', '--threads', $Threads.ToString(), '--task', 'PerFileScoring')
        $a1 += $extraArgs
        $a1 += $memStampArgs
        Invoke-OspreyTaskRun -WorkDir $d1 -CliArgs $a1 -LogName 'phase1.log'
        Copy-Item (Join-Path $d1 'phase1.log') (Join-Path $chainLogDir "phase1_$stem.log") -Force
    }
    # Phase 1's copied mzMLs are dead weight once it has run: phase 2/3 read its
    # parquets + calibration, never its mzML (phase 3 re-copies the mzML from the
    # data dir). Drop them so they don't sit on disk through the per-file rescore loop.
    if (-not $KeepOutput) {
        Get-ChildItem -Path $ph1Root -Recurse -Filter '*.mzML' -File -ErrorAction SilentlyContinue |
            Remove-Item -Force -ErrorAction SilentlyContinue
    }

    # Phase 2: FirstPassFDR (Stage 5). Consumes the per-file parquets, writes the
    # <stem>.1st-pass.fdr_scores.bin + <stem>.reconciliation.json sidecar pair. A
    # 0-byte stub mzML lets the task derive sidecar paths without reading spectra.
    $ph2 = Join-Path $ChainRoot 'phase2_FirstPassFDR'
    New-Item -ItemType Directory -Path $ph2 -Force | Out-Null
    foreach ($s in $stemList) {
        Copy-Item (Join-Path $ph1Dirs[$s] "$s.scores.parquet")   (Join-Path $ph2 "$s.scores.parquet")
        Copy-Item (Join-Path $ph1Dirs[$s] "$s.calibration.json") (Join-Path $ph2 "$s.calibration.json")
    }
    Copy-LibraryInto -Library $Library -Dir $ph2 -Manifest $Manifest
    $a2 = @('--task', 'FirstPassFDR')
    foreach ($s in $stemList) { $a2 += @('--input-scores', "$s.scores.parquet") }
    $a2 += @('-l', $libName, '-o', 'output.blib', '--resolution', $Resolution,
             '--protein-fdr', '0.01', '--threads', $Threads.ToString())
    $a2 += $extraArgs
    $a2 += $memStampArgs
    Invoke-OspreyTaskRun -WorkDir $ph2 -CliArgs $a2 -LogName 'phase2.log'
    Copy-Item (Join-Path $ph2 'phase2.log') (Join-Path $chainLogDir 'phase2.log') -Force

    # Phase 3: per-file rescore workers (Stage 6), one independent worker per
    # file. Stage 6 STREAMS its MS2 from the .spectra.bin cache phase 1 wrote (there is
    # no mzML fallback), so each worker gets phase 1's <stem>.spectra.bin + a 0-byte stub
    # <stem>.mzML (the cache fingerprint check is skipped for a 0-byte source, so the
    # stub is enough for path derivation and forces a cache hit -- the real 6 GB mzML is
    # never shipped to a rescore worker). Plus the Stage 4 parquet/calibration + the
    # Stage 5 sidecar pair; writes <stem>.scores-reconciled.parquet. NOT the 2nd-pass bin:
    # --task PerFileRescoring sets NoJoin, which excludes SecondPassFdrTask entirely, so
    # phase 4 is the only node that writes one.
    $ph3Dirs = @{}
    foreach ($s in $stemList) {
        $ph3 = Join-Path $ChainRoot "phase3_rescore_$s"
        $ph3Dirs[$s] = $ph3
        New-Item -ItemType Directory -Path $ph3 -Force | Out-Null
        Copy-Item (Join-Path $ph1Dirs[$s] "$s.spectra.bin")     (Join-Path $ph3 "$s.spectra.bin")
        Copy-Item (Join-Path $ph1Dirs[$s] "$s.scores.parquet")  (Join-Path $ph3 "$s.scores.parquet")
        Copy-Item (Join-Path $ph1Dirs[$s] "$s.calibration.json") (Join-Path $ph3 "$s.calibration.json")
        Copy-Item (Join-Path $ph2 "$s.1st-pass.fdr_scores.bin") (Join-Path $ph3 "$s.1st-pass.fdr_scores.bin")
        Copy-Item (Join-Path $ph2 "$s.reconciliation.json")     (Join-Path $ph3 "$s.reconciliation.json")
        # The 1st-pass model sidecar (frozen 2nd-pass modes) must ride the same
        # phase2 -> phase3 -> phase4 relay as the other Stage-5 sidecars: $ph2 is
        # deleted below (before SecondPassFDR), so SecondPassFDR can only reach it
        # via a phase-3 hop. Present only for the SVM/percolator framework; absent for
        # the GBDT golden, so guard with Test-Path.
        $ph2model = Join-Path $ph2 "$s.1st-pass.model.json"
        if (Test-Path $ph2model) { Copy-Item $ph2model (Join-Path $ph3 "$s.1st-pass.model.json") }
        # The protein-compact stratum is a SECOND artifact, not a field of the model sidecar:
        # first-pass protein FDR computes it and training does not, so it is written when that
        # later phase ends. It therefore needs its own hop on the same relay. Present only
        # under OSPREY_PASS2_QVALUE=protein-compact, so guard with Test-Path.
        $ph2stratum = Join-Path $ph2 "$s.1st-pass.stratum.json"
        if (Test-Path $ph2stratum) { Copy-Item $ph2stratum (Join-Path $ph3 "$s.1st-pass.stratum.json") }
        # The analysis-wide EXPERIMENT-scope sidecar (format v5, issue #4486) rides the same
        # relay. It is a RUN-level file, not per-stem - one record per distinct entry_id for the
        # whole analysis - but it is copied inside this per-stem loop because each stem gets its
        # own phase-3 directory and a PerFileRescoring worker reads it from its own working
        # directory. Stage 6's compaction reads the protein-rescue q out of this file, so a
        # worker that cannot see it computes a different survivor set than straight-through.
        $ph2exp = Join-Path $ph2 'output.1st-pass.fdr_experiment.bin'
        if (Test-Path $ph2exp) { Copy-Item $ph2exp (Join-Path $ph3 'output.1st-pass.fdr_experiment.bin') }
        # The analysis-wide RETAINED BASE_ID summary rides the same relay, for the same reason
        # and one step further: it is what lets this worker compact its single run without
        # reading every other run's reconciliation.json. FirstPassFDR writes it when planning
        # ends. Unlike the files above this one is NOT guarded with Test-Path - a worker that
        # cannot see it fails rather than silently compacting to a per-run subset, so a missing
        # copy here must surface as that failure and not as a skipped hop.
        $ph2ret = Join-Path $ph2 'output.1st-pass.retained_base_ids.bin'
        Copy-Item $ph2ret (Join-Path $ph3 'output.1st-pass.retained_base_ids.bin')
        Copy-LibraryInto -Library $Library -Dir $ph3 -Manifest $Manifest
        $a3 = @('--task', 'PerFileRescoring', '--input-scores', "$s.scores.parquet",
                '-l', $libName, '-o', 'output.blib', '--resolution', $Resolution,
                '--protein-fdr', '0.01', '--threads', $Threads.ToString())
        $a3 += $extraArgs
        $a3 += $memStampArgs
        Invoke-OspreyTaskRun -WorkDir $ph3 -CliArgs $a3 -LogName 'phase3.log'
        Copy-Item (Join-Path $ph3 'phase3.log') (Join-Path $chainLogDir "phase3_$s.log") -Force
        # This worker has written its reconciled parquet; phase 4
        # consumes it plus the calibration / reconciliation / 1st-pass
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
    # inputs (phase 4 reads only phase-3 outputs). Free them before SecondPassFDR.
    Remove-Scratch $ph1Root
    Remove-Scratch $ph2

    # Phase 4: SecondPassFDR (Stage 7 + blib). Consumes each worker's
    # reconciled parquet + sidecars (never the original Stage 4 parquet, and never
    # an mzML -- a 0-byte stub provides path derivation only) and writes the blib.
    $ph4 = Join-Path $ChainRoot 'phase4_SecondPassFDR'
    New-Item -ItemType Directory -Path $ph4 -Force | Out-Null
    # Where the phase-3 workers' own outputs are kept for comparison after their dirs are cleaned.
    $ph3Out = Join-Path $ChainRoot 'phase3_outputs'
    New-Item -ItemType Directory -Path $ph3Out -Force | Out-Null
    foreach ($s in $stemList) {
        $ph3 = $ph3Dirs[$s]
        Copy-Item (Join-Path $ph3 "$s.scores-reconciled.parquet") (Join-Path $ph4 "$s.scores-reconciled.parquet")
        # THE PER-FILE 1st-PASS SIDECARS ARE DELIBERATELY WITHHELD when the verifier is off.
        #
        # This is the gate for issue #4486's actual goal, and it is an ENFORCEMENT rather than an
        # assertion: phase 4 is given exactly what an HPC orchestrator would give a SecondPassFDR
        # node - the reconciled parquets, the per-run 2nd-pass artifacts, and the analysis-wide
        # experiment sidecar - and nothing per-file from the first pass. If Stage 7 reaches for
        # one, it fails on a missing file rather than passing quietly, and no log line or count
        # has to be trusted to notice.
        #
        # A green mode 3 therefore MEANS the default path is independent of these files. That is
        # not something a passing run could otherwise demonstrate: the straight leg has them
        # sitting in its own directory, so it would read them without anyone knowing.
        #
        # Under OSPREY_PASS2_VERIFY_WORKER they ARE relayed, because the recomputation legitimately
        # needs them - the flag changes what this node is being asked to do.
        # Preserved for the mode-3 comparison regardless: these ARE chain outputs (phase 3 wrote
        # them), they are simply not phase 4's input. The worker dirs are scrubbed a few lines
        # below, so without this the files the comparison needs would be gone.
        Copy-Item (Join-Path $ph3 "$s.1st-pass.fdr_scores.bin") (Join-Path $ph3Out "$s.1st-pass.fdr_scores.bin")

        # WITHHELD ONLY WHEN THE WORKER ANSWERED. The modes with a per-file half
        # (protein-compact, transfer-compete) leave a 2nd-pass sidecar here, and phase 4 folds it
        # without opening anything from the first pass - that is the contract issue #4486
        # establishes, and withholding is how it is proven. OSPREY_PASS2_QVALUE=transfer and the
        # retrain modes have NO per-file half, so Stage 7 legitimately recomputes and legitimately
        # needs these files; withholding them there would fail a mode for a contract it never
        # claimed. Decided from what phase 3 actually WROTE rather than from the mode flag, so a
        # future mode gets the right answer without editing this line.
        if (-not (Test-Path (Join-Path $ph3 "$s.2nd-pass.fdr_scores.bin"))) {
            Copy-Item (Join-Path $ph3 "$s.1st-pass.fdr_scores.bin") (Join-Path $ph4 "$s.1st-pass.fdr_scores.bin")
        }
        Copy-Item (Join-Path $ph3 "$s.calibration.json")          (Join-Path $ph4 "$s.calibration.json")
        Copy-Item (Join-Path $ph3 "$s.reconciliation.json")       (Join-Path $ph4 "$s.reconciliation.json")
        # Ship the persisted 1st-pass model so SecondPassFDR can run the frozen 2nd-pass
        # modes (transfer / transfer-compete / protein-compact) without re-training. Written
        # by the FirstPassFDR join node (phase 2) and relayed into $ph3 above ($ph2 is already
        # deleted by now). Present for the SVM/percolator framework, so guard with Test-Path.
        # protein-compact's stratum is its own artifact (protein FDR computes it, training does
        # not), so it takes the same second hop rather than riding inside the model sidecar.
        $modelSide = Join-Path $ph3 "$s.1st-pass.model.json"
        if (Test-Path $modelSide) { Copy-Item $modelSide (Join-Path $ph4 "$s.1st-pass.model.json") }
        $stratumSide = Join-Path $ph3 "$s.1st-pass.stratum.json"
        if (Test-Path $stratumSide) { Copy-Item $stratumSide (Join-Path $ph4 "$s.1st-pass.stratum.json") }
        # The per-run 2nd-pass sidecar is now an INPUT to phase 4, not its output (#4486): the
        # per-file half of the second pass runs in PerFileRescoring, so phase 3 produces this
        # file and phase 4 folds it. Its VALIDITY STAMP travels with it, because that stamp is
        # how SecondPassFDR knows the worker owns the file - the filename records the producer,
        # so presence answers "who wrote this" without opening it.
        #
        # Copying the binary but not the stamp would be worse than copying neither: phase 4
        # would fold a file it believed it had produced itself. Guarded with Test-Path because
        # the retrain modes have no per-file half and phase 3 writes nothing here.
        #
        # The DECOY SIDE of the same competition travels with it, and is not optional once the
        # sidecar is here: the pool image cannot carry the winning decoy of a base_id (a
        # non-survivor holds no pool row), so phase 4 THROWS rather than folding an experiment
        # q against a decoy-depleted null. Relaying one without the other is the exact defect
        # this relay produced once before - a per-file artifact that silently did not arrive,
        # leaving phase 4 to recompute and answer differently from the straight route.
        $pass2Side = Join-Path $ph3 "$s.2nd-pass.fdr_scores.bin"
        if (Test-Path $pass2Side) {
            $pass2Decoys = Join-Path $ph3 "$s.2nd-pass.fdr_decoys.bin"
            if (-not (Test-Path $pass2Decoys)) {
                throw "PerFileRescoring wrote $s.2nd-pass.fdr_scores.bin but no matching .2nd-pass.fdr_decoys.bin in $ph3"
            }
            Copy-Item $pass2Side (Join-Path $ph4 "$s.2nd-pass.fdr_scores.bin")
            Copy-Item $pass2Decoys (Join-Path $ph4 "$s.2nd-pass.fdr_decoys.bin")
            $decoysStamp = "$pass2Decoys.PerFileRescoring.osprey.task"
            if (Test-Path $decoysStamp) {
                Copy-Item $decoysStamp (Join-Path $ph4 "$s.2nd-pass.fdr_decoys.bin.PerFileRescoring.osprey.task")
            }
            $pass2Stamp = "$pass2Side.PerFileRescoring.osprey.task"
            if (Test-Path $pass2Stamp) {
                Copy-Item $pass2Stamp (Join-Path $ph4 "$s.2nd-pass.fdr_scores.bin.PerFileRescoring.osprey.task")
            }
        }
        # Same relay for the analysis-wide experiment sidecar: SecondPassFDR seeds pass-1
        # scalars from it, and $ph2 is gone by now, so phase 3 is its only route here.
        $ph3exp = Join-Path $ph3 'output.1st-pass.fdr_experiment.bin'
        if (Test-Path $ph3exp) { Copy-Item $ph3exp (Join-Path $ph4 'output.1st-pass.fdr_experiment.bin') -Force }
        # And the retained base_id summary, for the same reason: SecondPassFDR's reconciled-input
        # load streams each run against it, so $ph2 being gone makes phase 3 its only route here.
        $ph3ret = Join-Path $ph3 'output.1st-pass.retained_base_ids.bin'
        Copy-Item $ph3ret (Join-Path $ph4 'output.1st-pass.retained_base_ids.bin') -Force
        # No 2nd-pass bin relay. There was a `if (Test-Path ...) { Copy-Item ... }` here, and
        # it could never fire: --task PerFileRescoring sets NoJoin, so SecondPassFdrTask is not
        # in a phase-3 worker's pipeline and no such file exists to copy. Worse than dead - had
        # it fired it would have handed phase 4 a CURRENT 2nd-pass sidecar, and phase 4 would
        # then have skipped computing its own, quietly turning mode 3 into a test of a copy.
        # Phase 4 is the only node that writes these.
    }
    # SecondPassFDR now has every worker's reconciled output copied in; the phase-3
    # worker dirs are done.
    foreach ($d in $ph3Dirs.Values) { Remove-Scratch $d }
    Copy-LibraryInto -Library $Library -Dir $ph4 -Manifest $Manifest
    $a4 = @('--task', 'SecondPassFDR')
    foreach ($s in $stemList) { $a4 += @('--input-scores', "$s.scores-reconciled.parquet") }
    $a4 += @('-l', $libName, '-o', 'output.blib', '--resolution', $Resolution,
             '--protein-fdr', '0.01', '--threads', $Threads.ToString())
    $a4 += $extraArgs
    $a4 += $memStampArgs
    Invoke-OspreyTaskRun -WorkDir $ph4 -CliArgs $a4 -LogName 'phase4.log'
    Copy-Item (Join-Path $ph4 'phase4.log') (Join-Path $chainLogDir 'phase4.log') -Force

    return (Join-Path $ph4 'output.blib')
    } finally {
        # Restore whatever the straight leg runs under, whether this chain returned or threw.
        $env:OSPREY_PASS2_VERIFY_WORKER = $priorVerifyWorker
    }
}

# --- Per-dataset legs ---------------------------------------------------------
$overallFail = $false
$summaryLines = [System.Collections.Generic.List[string]]::new()

# Resolve every selected dataset UP FRONT, then fingerprint the read-only data
# folders once for the whole run. Two reasons for doing it here rather than
# per-dataset:
#
#   * Resolution is what extracts a nested library zip, so the fingerprint has to
#     be taken after it -- otherwise the legitimate one-time extraction would
#     look like a violation on a first run.
#   * The per-dataset check below fires right after the straight-through leg,
#     which leaves the resume and HPC-chain legs unguarded. Comparing at the very
#     END of the run covers every leg of every dataset.
#
# This mirrors the equivalent Skyline test-harness assertion: code under test
# must not write into folders that are supposed to be read-only inputs. It also
# gives attribution -- a green check here means whatever is dropping artifacts
# into the data dirs is NOT this harness.
$resolvedInputs = [ordered]@{}
foreach ($name in $selected) { $resolvedInputs[$name] = Resolve-DatasetInputs -Spec $datasets[$name] }
$watchedDirs = @($resolvedInputs.Values | ForEach-Object { $_.Dir; $_.LibDir } |
                 Where-Object { $_ } | Sort-Object -Unique)
$runStartFp = @{}
foreach ($d in $watchedDirs) { $runStartFp[$d] = Get-DirFingerprint -Dir $d }
Write-Host ("Watching {0} read-only data folder(s) for changes across the run." -f $watchedDirs.Count)

# Self-cleaning: each dataset's scratch is removed as soon as its legs finish, and
# the whole run root in the finally below -- so the run leaves no multi-GB output
# behind to starve the next run on a shared agent. -KeepOutput (honored by
# Remove-Scratch) opts out for local post-mortem.
try {
foreach ($name in $selected) {
    $cfg = $datasets[$name]
    Write-Progress-Tc "Dataset $name"
    $inputs = $resolvedInputs[$name]
    $dataFp = Get-DirFingerprint -Dir $inputs.Dir

    $straightDir = Join-Path $runRoot "$name\straight"
    $proteinDump = Join-Path $straightDir 'cs_stage7_protein_fdr.tsv'
    # GoldenFolder, not Folder: StellarLibDecoy shares the stellar mzML folder,
    # so keying the golden on Folder alone would collide with Stellar's.
    $goldenFolder = if ($cfg.GoldenFolder) { $cfg.GoldenFolder } else { $cfg.Folder }
    $goldenDir   = Join-Path $goldenRoot $goldenFolder

    # ---- Straight-through ----
    # (The per-run timestamped $runRoot guarantees $straightDir is fresh, so the
    # straight-through leg always runs clean -- no prior-run state to inherit.)
    Write-Progress-Tc "${name}: straight-through run ($($inputs.Mzmls.Count) files, $($cfg.Resolution))"
    $rStraight = Invoke-OspreyRun -Mzmls $inputs.Mzmls -Library $inputs.Library -Resolution $cfg.Resolution `
        -WorkDir $straightDir -LogName 'straight.log' -DumpProteinFdr -Spec $cfg -Manifest $inputs.Manifest
    $straightBlib = Join-Path $straightDir 'output.blib'
    Write-Host ("  straight-through wall {0:mm\:ss}; blib {1:N0} bytes" -f $rStraight.Wall, (Get-Item $straightBlib).Length)

    # ---- No-copy assertion: read-only data dir unchanged ----
    $changed = Compare-DirFingerprint -Before $dataFp -Dir $inputs.Dir
    if ($changed.Count -gt 0) {
        $overallFail = $true
        Write-Problem-Tc "${name}: read-only data dir was modified by the run: $($changed -join '; ')"
    }

    # Model-diagnostics report, written beside the blib (CWD-relative, stem from -o).
    $diagHtml = Join-Path $straightDir 'output.model-diagnostics.html'

    if ($CreateGolden) {
        # Tier 2 runs BEFORE anything is written, and a failure writes NOTHING.
        # Order matters: capturing first and then reporting "refusing to bless" would
        # leave a full set of updated golden files on disk, which is indistinguishable
        # from a legitimate rebaseline in git status -- so the poisoned baseline gets
        # committed anyway and tier 2 protects nothing. Tier 2 is never captured; it
        # is a fixed bound whose whole purpose is that a rebaseline cannot move it.
        if ($cfg.ModelDiagnostics) {
            $bounds = Get-SanityBounds $cfg
            $sane = Test-DiagnosticsSanity -HtmlPath $diagHtml @bounds
            if (-not $sane.Pass) {
                $overallFail = $true
                Write-Problem-Tc "${name}: REFUSED to capture golden -- diagnostics sanity failed (nothing written)"
                $sane.Issues | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
                $summaryLines.Add("$name golden REFUSED (sanity failed; no files written)")
                continue
            }
        }
        Write-Progress-Tc "${name}: capturing golden"
        Save-BlibGolden -Blib $straightBlib -GoldenDir $goldenDir -ProteinFdrTsv $proteinDump
        if ($cfg.ModelDiagnostics) { Save-DiagnosticsGolden -HtmlPath $diagHtml -GoldenDir $goldenDir }
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

    # ---- mode 1c: the 2nd-pass sidecar carries a SECOND-pass protein q -------
    # Single-run property of the straight-through output, so it runs on the DEFAULT arm that
    # TeamCity exercises - no baseline, no second route. It covers the one failure a two-route
    # comparison structurally cannot see: a column both routes copy identically out of pass 1.
    # That is what issue #4559 was, and mode 3 was green on the default arm throughout.
    # ASSERTED, not guarded. This used to skip when no 2nd-pass sidecars existed, because
    # SecondPassFdrTask wrote them only on AnyReconciledParquet and "an arm that legitimately
    # does no reconciliation work has nothing to assert on". That gate is gone: every input
    # file now gets a 2nd-pass sidecar whatever Stage 6 did, because a missing file cannot be
    # told apart from a write that failed and never committed. So the count IS the invariant,
    # and the skip that tolerated absence was the harness half of the same ambiguity - it would
    # have reported a run that silently wrote nothing as SKIPPED rather than red.
    $pass2Sidecars = @(Get-ChildItem -File -Path $straightDir -Filter '*.2nd-pass.fdr_scores.bin' `
        -ErrorAction SilentlyContinue)
    if ($pass2Sidecars.Count -ne $inputs.Mzmls.Count) {
        $overallFail = $true
        Write-Problem-Tc ("$name mode1c (2nd-pass protein q is pass-2): FAIL -- " +
            "$($pass2Sidecars.Count) 2nd-pass sidecar(s) for $($inputs.Mzmls.Count) input file(s); " +
            "every input file must have one")
        $summaryLines.Add(("$name mode1c (2nd-pass protein q is pass-2): FAIL " +
            "($($pass2Sidecars.Count) sidecars for $($inputs.Mzmls.Count) inputs)"))
    }
    else {
    Write-Progress-Tc "${name}: 2nd-pass protein q liveness (mode 1c)"
    $m1c = Test-Pass2ProteinQvalue -RunDir $straightDir
    if ($m1c.Pass) {
        # The gap-fill count is reported, not asserted on: those records have no 1st-pass
        # value to compare against, so they cannot contribute to the liveness check - and a
        # count that is computed but never printed is a claim the gate does not actually make.
        # It is also the population #4559 was originally filed about, so it is worth seeing.
        $summaryLines.Add(("$name mode1c (2nd-pass protein q is pass-2): PASS " +
            "($('{0:N0}' -f $m1c.Differing) of $('{0:N0}' -f $m1c.Matched) shared records moved; " +
            "$('{0:N0}' -f $m1c.GapFill) gap-fill record(s) absent from pass 1)"))
    } else {
        $overallFail = $true
        Write-Problem-Tc "$name mode1c (2nd-pass protein q is pass-2): FAIL -- $($m1c.Issues.Count) issue(s)"
        $m1c.Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        $summaryLines.Add("$name mode1c (2nd-pass protein q is pass-2): FAIL ($($m1c.Issues.Count) issues)")
    }
    }

    # ---- mode 1b: FDR-calibration spot checks -------------------------------
    # Two independent tiers. The golden compare catches drift; the sanity bounds
    # catch a regression that a -CreateGolden rebaseline would otherwise bless
    # into the baseline. The blib golden cannot cover this: a calibration failure
    # can leave the ranking intact and only wreck the reported q-values.
    if ($cfg.ModelDiagnostics) {
        Write-Progress-Tc "${name}: diagnostics spot checks (mode 1b)"
        $md = Compare-DiagnosticsGolden -HtmlPath $diagHtml -GoldenDir $goldenDir -Tolerance $Tolerance
        if ($md.Pass) {
            $summaryLines.Add("$name mode1b (diagnostics vs golden): PASS")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode1b (diagnostics vs golden): FAIL -- $($md.Issues.Count) issue(s)"
            $md.Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode1b (diagnostics vs golden): FAIL ($($md.Issues.Count) issues)")
        }
        $bounds = Get-SanityBounds $cfg
        $ms = Test-DiagnosticsSanity -HtmlPath $diagHtml @bounds
        if ($ms.Pass) {
            $summaryLines.Add("$name mode1b (FDR sanity bounds): PASS")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode1b (FDR sanity bounds): FAIL -- calibration is out of bounds"
            $ms.Issues | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode1b (FDR sanity bounds): FAIL ($($ms.Issues.Count) issues)")
        }
    }

    # ---- mode 3: HPC 4-task worker chain vs straight-through ----
    # Runs BEFORE mode 2: mode 2 invalidates + re-runs $straightDir in place, so
    # $straightBlib is the pristine straight-through output only until then.
    if (-not $SkipHpcChain) {
        Write-Progress-Tc "${name}: HPC 4-task chain self-consistency (mode 3)"
        $chainRoot = Join-Path $runRoot "$name\chain"
        $sw3 = [Diagnostics.Stopwatch]::StartNew()
        # No OSPREY_ALLOW_UNBOUNDED_MEMORY opt-in here, and mode 3's SecondPassFDR leg no
        # longer needs one: since #4486 it streams the reconciled-input load (one file's
        # pre-compaction pool resident at a time) instead of taking the RESIDENT first-pass
        # pool, so no leg of this chain arms the guard at all. Keeping the opt-in would be
        # actively harmful:
        # it wrapped the whole chain and would mask a genuine guard regression on any
        # --input-scores worker (--task PerFileScoring / PerFileRescoring), which is exactly
        # what mode 3 exists to exercise.
        $chainBlib = Invoke-HpcChain -Mzmls $inputs.Mzmls -Library $inputs.Library `
            -Resolution $cfg.Resolution -ChainRoot $chainRoot -Spec $cfg -Manifest $inputs.Manifest `
            -CacheSource $straightDir
        $sw3.Stop()
        Write-Host ("  HPC chain wall {0:mm\:ss}; blib {1:N0} bytes" -f $sw3.Elapsed, (Get-Item $chainBlib).Length)
        # Per-file sidecar comparison, alongside the blib. The blib carries no protein
        # q-value and no per-entry SVM score, so a route that writes different values into
        # every <stem>.2nd-pass.fdr_scores.bin passed this leg green (#4553). Peptide counts,
        # protein-group counts and the blib are all identical while it happens, so this is
        # the only assertion that can see it. The straight-through run's own sidecars are the
        # oracle - same inputs, same library, so the distributed tasks must reproduce them
        # field for field. Those sidecars are also the REHYDRATION input for the distributed
        # and resume paths, which is why a silent divergence here is not cosmetic.
        #
        # Both passes: pass 1 is now an INPUT to pass 2 (the restore seeds from it), so a
        # Stage-5 divergence would otherwise surface here as a pass-2 defect and send the
        # reader to the wrong stage.
        $chainDir = Split-Path $chainBlib -Parent
        $chainPhase3Dir = Join-Path (Split-Path $chainDir -Parent) 'phase3_outputs'
        $m3sIssues = [System.Collections.Generic.List[string]]::new()
        $m3sCompared = 0
        # The 1st-pass sidecars are compared where the chain actually PRODUCES them - the phase-3
        # rescore workers - not in phase 4. Phase 4 is deliberately not given them on the default
        # path (see the withholding in Invoke-HpcChain), because a SecondPassFDR node must run
        # without per-file first-pass input. Looking for them there would turn that contract into
        # a mode-3 failure, and "the file we refused to stage is missing" is not a divergence.
        foreach ($sidecarPass in 1, 2) {
            # Pass 1 is compared against the phase-3 workers' outputs, ALWAYS: Invoke-HpcChain
            # runs its phases with the verifier off whatever the caller set, so phase 4 never
            # receives these files. Reading the ambient flag here asked the caller's value, not
            # the chain's, and sent the comparison to a directory the contract keeps empty.
            $actualDir = if ($sidecarPass -eq 1) { $chainPhase3Dir } else { $chainDir }
            $cmp = Compare-FdrSidecars -ExpectedDir $straightDir -ActualDir $actualDir `
                -Pass $sidecarPass -Tolerance $Tolerance
            $cmp.Issues | ForEach-Object { $m3sIssues.Add("pass${sidecarPass}: $_") }
            $m3sCompared += $cmp.Compared
        }
        # And the analysis-wide EXPERIMENT-scope sidecars (format v5, issue #4486). A byte
        # comparison is exact here and needs no field decoder: both routes write one record per
        # distinct entry_id in ascending entry_id order, so the file is a function of its
        # contents rather than of the order the route walked its inputs. Absence is a FAILURE,
        # not a skip - these files carry the experiment q-values the per-file sidecars used to,
        # so a route that stopped writing one would otherwise pass this leg by having nothing to
        # compare.
        foreach ($expPass in '1st-pass', '2nd-pass') {
            $expName = "*.$expPass.fdr_experiment.bin"
            $expStraight = @(Get-ChildItem -File -Path $straightDir -Filter $expName -ErrorAction SilentlyContinue)
            $expChain = @(Get-ChildItem -File -Path $chainDir -Filter $expName -ErrorAction SilentlyContinue)
            if ($expStraight.Count -eq 0 -or $expChain.Count -eq 0) {
                $m3sIssues.Add("$expName : straight-through has $($expStraight.Count), chain has $($expChain.Count) - expected one each")
                continue
            }
            # OspreyFdrSidecarComparer.CompareBytes, NOT Compare-Object: this is a memcmp, and
            # Compare-Object boxes every byte into a PSObject and hashes it. On Astral (85.8 MB,
            # 2,498,773 records) that took the harness process to a 53 GB working set and stalled
            # this leg for many minutes; the span compare is under a second.
            $expDiff = [OspreyFdrSidecarComparer]::CompareBytes(
                $expStraight[0].FullName, $expChain[0].FullName, 1000)
            if (-not $expDiff.Readable) {
                $m3sIssues.Add("$expName : $($expDiff.Problem)")
            } elseif (-not $expDiff.Equal) {
                # DOUBLE parens, the same idiom as the two sites in Regression/FdrSidecars.ps1
                # and for the reason spelled out at Regression/DiagnosticsGolden.ps1:279: -f
                # binds TIGHTER than the ',' separating method arguments, so with single parens
                # only the first operand reaches the format and the rest become extra arguments
                # to Add. The format then throws "Index (zero based) must be ... less than the
                # size of the argument list", replacing the comparison result with an exception
                # that names neither file. Latent until now because this branch runs ONLY when
                # the experiment sidecars actually differ between routes.
                $m3sIssues.Add((("{0} : {1} differs between routes - lengths {2} vs {3}, first " +
                    "difference at byte {4}, {5}+ differing byte(s)") -f $expName,
                    $expStraight[0].Name, $expDiff.LengthExpected, $expDiff.LengthActual,
                    $expDiff.FirstDiffOffset, $expDiff.DiffCount))
            } else {
                $m3sCompared += [int](([System.IO.FileInfo]$expStraight[0].FullName).Length - 32) / 36
            }
        }

        # Liveness: a comparison that verified nothing is not a passing comparison. Empty or
        # absent sidecars satisfy every field check trivially while breaking every resume,
        # and the rest of this harness fails closed on the same shape (Invoke-ResumeInvalidation
        # throws when it matches no files, mode 6 adds an issue when nothing matched).
        if ($m3sCompared -eq 0) {
            $m3sIssues.Add("compared 0 sidecar records across both passes - the gate verified nothing")
        }
        if ($m3sIssues.Count -eq 0) {
            $summaryLines.Add("$name mode3 (per-file FDR sidecars==straight): PASS ($('{0:N0}' -f $m3sCompared) records)")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode3 (per-file FDR sidecars==straight): FAIL - $($m3sIssues.Count) issue(s)"
            $m3sIssues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode3 (per-file FDR sidecars==straight): FAIL ($($m3sIssues.Count) issues)")
        }

        # PROVE THE SPLIT HAPPENED, do not assume it. mode 3's value as coverage of the shipped
        # fold rests entirely on the two legs having run DIFFERENT paths - straight with the
        # verifier on, this chain with it off. If the env var stopped reaching a child process,
        # both would run whichever path the environment supplied and this comparison would still
        # be green while covering only one of them. Osprey states which fold it ran on every run,
        # so the assertion is a grep, not a new leg.
        $straightFold = Select-String -Path (Join-Path $straightDir 'straight.log') `
            -Pattern 'Second-pass worker verification ACTIVE' -Quiet
        $chainFold = Select-String -Path (Join-Path (Join-Path $chainRoot 'logs') 'phase4.log') `
            -Pattern 'Second-pass worker verification ACTIVE' -Quiet
        # And the chain must have folded a worker answer for EVERY file. "Verification off" is
        # not the same as "the shipped path ran": a node given no 2nd-pass artifacts also has the
        # verifier off, and silently recomputes every file from 1st-pass sidecars. That is exactly
        # what a SEA-AD measurement did for hours while reporting the shipped path (2026-08-31).
        # Only the modes with a per-file half make this claim. OSPREY_PASS2_QVALUE=transfer and
        # the retrain modes compute the second pass in Stage 7 by definition, so there is no
        # worker answer to fold and demanding one would fail them for a contract they never
        # made. Detected from the worker's own validity stamp reaching phase 4, not from the
        # mode flag.
        $chainHasWorkerOutput = @(Get-ChildItem -File -Path $chainDir `
            -Filter '*.2nd-pass.fdr_scores.bin.PerFileRescoring.osprey.task' `
            -ErrorAction SilentlyContinue).Count -gt 0
        $chainAllAnswered = Select-String -Path (Join-Path (Join-Path $chainRoot 'logs') 'phase4.log') `
            -Pattern "worker's written answer for all \d+ file\(s\)" -Quiet
        if (-not $chainHasWorkerOutput) {
            $summaryLines.Add("$name mode3 (shipped fold): SKIP (mode has no per-file half)")
        } elseif (-not $chainAllAnswered) {
            $overallFail = $true
            Write-Problem-Tc ("$name mode3 (shipped fold): FAIL - phase 4 did not report a worker " +
                "answer for ALL files, so it recomputed some from 1st-pass sidecars rather than " +
                "folding what the workers wrote.")
            $summaryLines.Add("$name mode3 (shipped fold): FAIL")
        } else {
            $summaryLines.Add("$name mode3 (shipped fold): PASS (worker answer folded for every file)")
        }

        # Scoped for the same reason as the shipped-fold check above: the verifier only exists on
        # the frozen-competition path, so OSPREY_PASS2_QVALUE=transfer and the retrain modes emit
        # NEITHER fold line and there is no split to assert. Detected from the straight leg having
        # emitted a fold line at all, rather than from the mode flag.
        $straightUsedFrozenPath = Select-String -Path (Join-Path $straightDir 'straight.log') `
            -Pattern 'Second-pass (worker verification ACTIVE|fold )' -Quiet
        if (-not $straightUsedFrozenPath) {
            $summaryLines.Add("$name mode3 (verifier split): SKIP (mode has no per-file half)")
        } elseif (-not $straightFold -or $chainFold) {
            $overallFail = $true
            Write-Problem-Tc ("$name mode3 (verifier split): FAIL - straight leg verified=" +
                "$([bool]$straightFold) (expected True), HPC chain verified=$([bool]$chainFold) " +
                "(expected False). The gate is then comparing two runs of the same path.")
            $summaryLines.Add("$name mode3 (verifier split): FAIL")
        } else {
            $summaryLines.Add("$name mode3 (verifier split): PASS (straight verified, chain shipped-path)")
        }

        # Which PATH the phase-3 workers took, before comparing what they produced. One marker
        # per worker log; every phase-3 node must show it, because a single node quietly falling
        # back to the all-runs hydrate is invisible in the output at one file per node.
        # Not asserted under --model-diagnostics, where the per-run path is DELIBERATELY off:
        # the report is folded from the PRE-compaction rows during the all-runs hydrate, and a
        # per-run worker sees those rows only after the point the report is written - so it would
        # emit no report at all rather than a smaller one. CanHydratePerRun excludes the mode for
        # that reason, and asserting the marker here would fail a leg for doing the right thing.
        # The assertion stays live on every dataset that does NOT set the flag.
        # $cfg, not $Spec: the dataset spec is $cfg in this loop ($cfg = $datasets[$name],
        # above) and $Spec is a parameter of the helper FUNCTIONS. Written as $Spec, this read
        # returned $null on every dataset, so the skip never fired and the assertion failed
        # Astral for doing exactly what CanHydratePerRun asks of it under --model-diagnostics.
        $ph3Logs = @()
        if (-not $cfg.ModelDiagnostics) {
            $ph3Logs = @(Get-ChildItem (Join-Path $chainRoot 'logs\phase3_*.log') -ErrorAction SilentlyContinue)
        }
        if ($cfg.ModelDiagnostics) {
            $summaryLines.Add("$name mode3 (per-run hydrate): SKIP (--model-diagnostics keeps the all-runs hydrate)")
        } elseif ($ph3Logs.Count -eq 0) {
            $overallFail = $true
            Write-Problem-Tc "$name mode3 (per-run hydrate): FAIL - no phase3_*.log to read"
            $summaryLines.Add("$name mode3 (per-run hydrate): FAIL (no worker logs)")
        } else {
            $m3path = @{ Pass = $true; Issues = [System.Collections.Generic.List[string]]::new() }
            foreach ($lg in $ph3Logs) {
                $one = Test-LogMarker -LogPath $lg.FullName -Marker $perRunHydrateMarker `
                    -Description 'the phase-3 worker hydrating its run from that run''s own artifacts'
                foreach ($issue in $one.Issues) { $m3path.Issues.Add($issue) }
            }
            $m3path.Pass = ($m3path.Issues.Count -eq 0)
            if ($m3path.Pass) {
                $summaryLines.Add(
                    "$name mode3 (per-run hydrate): PASS ($($ph3Logs.Count) worker(s))")
            } else {
                $overallFail = $true
                Write-Problem-Tc "$name mode3 (per-run hydrate): FAIL - $($m3path.Issues.Count) issue(s)"
                $m3path.Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
                $summaryLines.Add("$name mode3 (per-run hydrate): FAIL ($($m3path.Issues.Count) issues)")
            }
        }

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

    # ---- mode 4: warm re-run cache-hit assertion ----
    # The gate's structural blind spot, closed. Modes 1 and 3 compare output produced
    # in FRESH dirs, and mode 2 invalidates Stage 5 before it re-runs, so no leg here
    # ever exercised the all-cached path and no leg asserted that anything was skipped
    # at all. Comparing output cannot do it: a re-run that ignores every cache and
    # recomputes from spectra emits a byte-identical blib, which is exactly how a
    # broken warm resume passed a fully green gate.
    #
    # Placed BEFORE mode 2, which invalidates $straightDir in place. This leg needs the
    # pristine post-straight-through state, where every task's outputs sit on disk
    # under a valid sidecar, so re-running the IDENTICAL command must be a no-op. It is
    # nearly free on a healthy build (measured 35 ms on Stellar against a ~4 min
    # straight-through leg): a fully cached run checks its sidecars and exits without
    # reading spectra or even loading the library.
    if (-not $SkipWarmRerun) {
        Write-Progress-Tc "${name}: warm re-run cache hits (mode 4)"
        $warmBefore = (Get-FileHash $straightBlib -Algorithm SHA256).Hash
        # No OSPREY_ALLOW_UNFIXED_RESIDENT opt-in, for mode 3's reason: a healthy warm
        # re-run runs NO task, so it can reach no resident path, and pre-setting the
        # opt-in could only mask the regression this leg exists to catch.
        $rWarm = Invoke-OspreyRun -Mzmls $inputs.Mzmls -Library $inputs.Library -Resolution $cfg.Resolution `
            -WorkDir $straightDir -LogName 'warm.log' -Spec $cfg -Manifest $inputs.Manifest
        $warmAfter = (Get-FileHash $straightBlib -Algorithm SHA256).Hash
        Write-Host ("  warm re-run wall {0:N1}s (a fully cached run does no work)" -f $rWarm.Wall.TotalSeconds)
        $m4 = Test-TaskCacheHits -LogPath $rWarm.Log -ExpectSkipped $pipelineTaskNames `
            -NoColdScoring -NoColdRescoring
        # Byte identity on top of the cache assertion. A fully cached run never rewrites
        # the blib, so this normally hashes an untouched file; it is here to catch a
        # partial regression that re-runs SecondPassFDR and writes a DIFFERENT blib
        # while the upstream tasks still report cache hits.
        #
        # A raw sha256 is legitimate ONLY under this leg's nothing-was-rewritten
        # premise. Do not copy it to mode 2: a genuine recompute produces a
        # semantically identical blib that is NOT byte-identical (measured on Stellar,
        # cold vs resume differ in the file bytes while comparing equal at 1e-9), which
        # is why every other leg goes through Compare-BlibFull. Here, a rewritten blib
        # necessarily means SecondPassFDR ran, which the skip assertion above already
        # catches, so this can only add signal, never a false red.
        if ($warmAfter -ne $warmBefore) {
            $m4.Issues.Add((("output.blib is not byte-identical across the warm re-run: " +
                "sha256 {0} -> {1}") -f $warmBefore.Substring(0, 16), $warmAfter.Substring(0, 16)))
            $m4.Pass = $false
        }
        if ($m4.Pass) {
            $summaryLines.Add("$name mode4 (warm re-run all cached): PASS")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode4 (warm re-run all cached): FAIL - $($m4.Issues.Count) issue(s)"
            $m4.Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode4 (warm re-run all cached): FAIL ($($m4.Issues.Count) issues)")
        }
    }

    # The pristine straight-through blib, kept aside for BOTH legs below that re-run
    # into $straightDir in place. Taken ONCE, here, so each leg is compared against
    # the straight-through output rather than against whatever the previous leg left:
    # mode 2 and mode 5 both rewrite output.blib, so a per-leg copy would make the
    # second leg's oracle the first leg's product. Placed after mode 4, whose
    # byte-identity assertion needs the dir untouched.
    $coldBlib = Join-Path $straightDir 'output_cold.blib'
    Copy-Item $straightBlib $coldBlib -Force

    # ---- mode 2: resume vs straight-through self-consistency ----
    if (-not $SkipResume) {
        Write-Progress-Tc "${name}: resume self-consistency (mode 2)"
        Invoke-ResumeInvalidation -WorkDir $straightDir
        # No OSPREY_ALLOW_UNFIXED_RESIDENT opt-in, and this leg is the reason the variable is
        # now unset on EVERY leg of the gate. It used to name mdiag-full-resume: the
        # invalidation leaves every <stem>.1st-pass.fdr_scores.bin on disk, so under
        # --model-diagnostics Stage 5 held the RESIDENT per-file entries for the batch
        # ModelDiagnosticsReport.Write to read, which is O(files) and genuinely needed
        # suppressing at 3 files. #4505 streamed that report off FirstPassFDR's own per-file load,
        # so the trigger is gone and so is the token.
        #
        # Nothing here replaces it. An opt-in on this leg would mask exactly the regression the
        # leg exists to catch, which is why mode 3 and mode 4 never had one either: the former
        # blanket OSPREY_ALLOW_UNBOUNDED_MEMORY=1 wrapped a whole leg and let a transfer
        # regression ride along unnoticed for ten days.
        # The inputs this leg names DO NOT EXIST: same file names, but under the work dir,
        # where an mzML is never placed - only the .spectra.bin is. The sources stay in the
        # read-only Perftests tree, untouched.
        #
        # SCOPE: this proves the pipeline ACCEPTS absent inputs and still produces the
        # identical blib. It does NOT prove spectra can be READ from cache - the two tasks
        # that open one are the two this leg requires to be skipped. HPC phase 1 covers that.
        $resumeInputs = @($inputs.Mzmls | ForEach-Object { Join-Path $straightDir (Split-Path $_ -Leaf) })
        foreach ($p in $resumeInputs) {
            # Guard the premise. If an mzML ever does land in the work dir, the leg would
            # quietly go back to testing the ordinary path and the cache-only property
            # would stop being covered with nothing turning red.
            if (Test-Path $p) { throw "regression: work dir unexpectedly holds a source input ($p); the resume leg's cache-only premise is broken." }
        }
        $rResume = Invoke-OspreyRun -Mzmls $resumeInputs -Library $inputs.Library -Resolution $cfg.Resolution `
            -WorkDir $straightDir -LogName 'resume.log' -Spec $cfg -Manifest $inputs.Manifest
        $resumeBlib = Join-Path $straightDir 'output.blib'
        Write-Host ("  resume wall {0:mm\:ss}; blib {1:N0} bytes" -f $rResume.Wall, (Get-Item $resumeBlib).Length)

        # The resume run has to have RESUMED, and this is the only place that can say
        # so. The blib compare below passes just as happily on a re-run that ignored
        # every cache and recomputed the whole pipeline from spectra, so on its own it
        # cannot tell a working warm resume from a completely broken one.
        #
        # Invoke-ResumeInvalidation deletes only the Stage 5 join sidecars, the blib,
        # and the blib's own sidecar. So PerFileScoring's per-file parquets and
        # PerFileRescoring's reconciled parquets stay valid and must report cache hits,
        # while FirstPassFDR and SecondPassFDR lost their sidecars and must recompute.
        # Asserting the ran side too is what keeps the leg from going vacuous: if the
        # invalidation ever stopped biting, every task would skip and the "resume"
        # would be comparing the straight-through blib against itself.
        $m2cache = Test-TaskCacheHits -LogPath $rResume.Log `
            -ExpectSkipped @('PerFileScoring', 'PerFileRescoring') `
            -ExpectRan @('FirstPassFDR', 'SecondPassFDR') `
            -NoColdScoring -NoColdRescoring
        # ... and this is the only thing that says the run took the CACHE-ONLY path. The
        # premise guard above proves the named inputs are absent, and a build without the
        # absent-source support would die on them outright - but neither notices if this
        # leg is ever pointed back at the real sources, which would drop the coverage with
        # nothing going red. Same reason mode 5 asserts its rehydrate marker.
        $m2absent = Test-LogMarker -LogPath $rResume.Log `
            -Marker 'are absent but have a spectra cache' `
            -Description 'Osprey resolving inputs from the spectra cache with no source present'
        foreach ($issue in $m2absent.Issues) { $m2cache.Issues.Add($issue) }
        # Repair Pass after mutating Issues - Test-TaskCacheHits computed it at return time.
        $m2cache.Pass = ($m2cache.Issues.Count -eq 0)
        if ($m2cache.Pass) {
            $summaryLines.Add("$name mode2 (resume cache hits): PASS")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode2 (resume cache hits): FAIL - $($m2cache.Issues.Count) issue(s)"
            $m2cache.Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode2 (resume cache hits): FAIL ($($m2cache.Issues.Count) issues)")
        }

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

    # ---- mode 5: Stage-5 rehydrate (FirstPassFDR's rehydrate arm) ----
    # Runs AFTER mode 2, and the order is not arbitrary. Mode 5's SecondPassFDR run
    # rewrites more than output.blib: every <stem>.2nd-pass.fdr_scores.bin, the
    # model-diagnostics report, and its .data.json are SecondPassFDR outputs too, and
    # Invoke-ResumeInvalidation deletes none of them. Running mode 5 first therefore
    # left mode 2 resuming on top of mode-5-produced pass-2 state, which feeds
    # PerFileRescore's pass-2 self-gate and SecondPassFDR's 2nd-pass rehydrate - so mode
    # 2's oracle silently depended on whether -SkipRehydrate was passed, and a defect
    # that only appears when Stage 5 is rehydrated twice would hide behind mode 5's
    # own passing blib assertion. Ordering it last costs nothing: mode 2's resume
    # re-runs FirstPassFDR and SecondPassFDR, so it leaves exactly the all-valid state
    # mode 5 needs, and mode 5 still compares against the pristine $coldBlib.
    #
    # Stated plainly, because the coupling MOVED rather than vanished: mode 2 rewrites
    # every .1st-pass.fdr_scores.bin and .reconciliation.json (its invalidation deletes
    # the FirstPassFDR stamp, so that task RUNS), and mode 5 then streams its bundle from
    # those recomputed sidecars. Under -SkipResume it streams the ORIGINAL straight-through
    # sidecars instead. Both are valid Stage 5 states and mode 2 asserts they agree at 1e-9,
    # so neither is a wrong input - but they are not the SAME input, and a bisection that
    # reproduces a mode 5 failure must match the switch combination that produced it.
    # Removing the coupling entirely needs a second work dir, which costs a full
    # straight-through run per dataset.
    #
    # Task NAMES throughout, not class names: the driver stamps and logs
    # 'FirstPassFDR' and 'SecondPassFDR', while the classes behind them are
    # FirstPassFdrTask and SecondPassFdrTask. Keying prose off the class names is how a
    # copy of the invalidation helper once matched zero files and produced a
    # 'resume' that never resumed (see Invoke-ResumeInvalidation).
    #
    # The one leg that reaches FirstPassFDR's OWN-SIDECAR bundle loader. Mode 2
    # deletes that task's validity stamp, so it RUNS there; mode 4 invalidates
    # nothing, so nothing demands its state; mode 3's PerFileRescoring phase does
    # enter the rehydrate arm, but with a worker-supplied bundle, so it never calls
    # LoadOwnReconciliationBundle. Invalidating ONLY SecondPassFDR leaves the
    # FirstPassFDR stamp and every .1st-pass.fdr_scores.bin + .reconciliation.json
    # sidecar valid, which is exactly the state that loader exists to serve.
    #
    # Names NO token and suppresses nothing - #4536 removed the last one. Note what this
    # leg does and does not buy: a regression that puts STAGE 5 back on the resident pool
    # fails on PerFileScoringTask's guard, but nothing GUARDS FirstPassFDR's own survivor
    # loader, so a regression confined to it does not fail on a guard here.
    #
    # It is still caught, by comparison rather than by a guard. Both sides of this leg go
    # through FirstPassSurvivorLoader since #4536, so this leg alone cannot witness a
    # loader fault that affects them equally - but mode 1 compares the straight-through
    # blib against a COMMITTED golden that predates the loader, so a fault common to both
    # sides fails there, and a fault confined to the resume fails this leg's
    # rehydrate==straight compare. Plus the marker below and the memory trace in the log.
    if (-not $SkipRehydrate) {
        Write-Progress-Tc "${name}: Stage-5 rehydrate self-consistency (mode 5)"
        Invoke-SecondPassOnlyInvalidation -WorkDir $straightDir
        # Delete the straight-through run's report so the comparison below cannot pass
        # against it. Nothing about the invalidation forces FirstPassFDR to re-emit the
        # report - if the rehydrate arm stopped writing one, a surviving file from the
        # straight-through leg would compare EQUAL to the golden and the leg would go
        # green having tested nothing. Removing it makes "the report the rehydrate
        # produced" the only thing that can be compared.
        # BOTH files. SecondPassFdrTask re-renders the HTML from output.model-diagnostics.data.json
        # during its pass-2 enrichment, inside a catch-all that swallows failures - so if any
        # earlier leg threw partway through that block, the .data.json survives, and deleting
        # only the .html would let SecondPassFDR rebuild a complete page from the PREVIOUS leg's
        # pass-1 data. The comparison would then pass against a report the rehydrate never
        # wrote, which is the exact regression this deletion exists to catch.
        if ($cfg.ModelDiagnostics) {
            Remove-Item -LiteralPath $diagHtml -Force -ErrorAction SilentlyContinue
            Remove-Item -LiteralPath ($diagHtml -replace '\.html$', '.data.json') `
                -Force -ErrorAction SilentlyContinue
        }
        # No token. This leg used to set OSPREY_ALLOW_UNFIXED_RESIDENT=resume-survivor-handoff
        # because a resume could not stream the Stage 6 survivor handoff - only a computed
        # Stage 5 built the per-file loader - and FirstPassFDR's rehydrate arm refused to run
        # without it. #4536 gave the rehydrate its own loader, so the arm streams and the token
        # no longer exists; running with nothing suppressed is what now makes this leg prove it.
        $rRehydrate = Invoke-OspreyRun -Mzmls $inputs.Mzmls -Library $inputs.Library -Resolution $cfg.Resolution `
            -WorkDir $straightDir -LogName 'rehydrate.log' -Spec $cfg -Manifest $inputs.Manifest
        $rehydrateBlib = Join-Path $straightDir 'output.blib'
        Write-Host ("  rehydrate wall {0:mm\:ss}; blib {1:N0} bytes" -f $rRehydrate.Wall, (Get-Item $rehydrateBlib).Length)

        # Three tasks kept their outputs; only SecondPassFDR lost its stamp. Asserting
        # the ran side keeps the leg from going vacuous exactly as in mode 2.
        $m5cache = Test-TaskCacheHits -LogPath $rRehydrate.Log `
            -ExpectSkipped @('PerFileScoring', 'FirstPassFDR', 'PerFileRescoring') `
            -ExpectRan @('SecondPassFDR') `
            -NoColdScoring -NoColdRescoring
        # ... and the FirstPassFDR cache hit above is NOT evidence the rehydrate arm
        # ran: a skipped task whose state nobody demands never enters Rehydrate at
        # all. This marker is the only thing that says it did.
        # EITHER rehydrate shape proves the leg is not vacuous, and which one runs depends on
        # whether the analysis-wide retained base_id summary is on disk. With it, FirstPassFDR
        # publishes the survivor loader and builds no experiment-wide bundle at all, so the
        # streaming-bundle line never appears - correctly. Accepting either is NOT loosening the
        # assertion: the leg still fails if NEITHER appears, which is the vacuous case it exists
        # to catch. Asserting only the old marker would have made this leg red for taking the
        # better path.
        $m5marker = Test-LogMarker -LogPath $rRehydrate.Log -Marker $firstPassFdrRehydrateMarker `
            -Description 'FirstPassFDR streaming the post-Stage-5 bundle from its own sidecars'
        if (-not $m5marker.Pass) {
            $m5perRun = Test-LogMarker -LogPath $rRehydrate.Log -Marker $firstPassFdrPerRunMarker `
                -Description 'FirstPassFDR publishing the survivor loader for a per-run rescore'
            if ($m5perRun.Pass) {
                $m5marker = $m5perRun
            } else {
                foreach ($issue in $m5perRun.Issues) { $m5marker.Issues.Add($issue) }
            }
        }
        foreach ($issue in $m5marker.Issues) { $m5cache.Issues.Add($issue) }
        # Repair Pass after mutating Issues. Test-TaskCacheHits computed it at return
        # time, so appending above leaves Pass $true with a non-empty Issues list. Every
        # other leg in this file reports off .Pass; leaving it stale here means the first
        # edit toward that house style silently turns a MISSING rehydrate marker into a
        # mode 5 PASS - green on exactly the regression this leg exists to catch.
        $m5cache.Pass = ($m5cache.Issues.Count -eq 0)
        if ($m5cache.Pass) {
            $summaryLines.Add("$name mode5 (rehydrate entered + cache hits): PASS")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode5 (rehydrate entered + cache hits): FAIL - $($m5cache.Issues.Count) issue(s)"
            $m5cache.Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode5 (rehydrate entered + cache hits): FAIL ($($m5cache.Issues.Count) issues)")
        }

        $m5 = Compare-BlibFull -BlibExpected $coldBlib -BlibActual $rehydrateBlib -Tolerance $Tolerance
        if ($m5.Pass) {
            $summaryLines.Add("$name mode5 (rehydrate==straight): PASS")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode5 (rehydrate==straight): FAIL - $($m5.Issues.Count) issue(s)"
            $m5.Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode5 (rehydrate==straight): FAIL ($($m5.Issues.Count) issues)")
        }

        # The rehydrate re-emits the --model-diagnostics report from the 1st-pass
        # sidecars rather than from a just-scored pool, which is the substitution
        # #4505 is about. Comparing it to the SAME golden mode 1b compares the
        # straight-through report against is what makes the two reports equivalent
        # rather than merely both present.
        #
        # -NoTrainedModel because this run adopted its q-values instead of training
        # Percolator, so featureCount is pinned at 0 rather than compared (see
        # Compare-DiagnosticsGolden). That is pre-existing resume behavior, not a
        # property of the streamed report: FirstPassFDR's rehydrate has always passed a
        # null FeatureContributions, on the resident batch write too. Every metric
        # the resume CAN reproduce - pool composition, the null-alignment density
        # ratio, the paired decoy-win fraction, and pass-1/pass-2 FDP at the reported
        # q - is still compared at $Tolerance, and those are exactly the reductions
        # the streaming accumulator had to reproduce row for row.
        if ($cfg.ModelDiagnostics) {
            # try/catch, not just Test-Path. Absence is only ONE way this fails: a report that
            # exists but carries no JSON payload, or a truncated one, throws out of
            # Get-DiagnosticsPayload / ConvertFrom-Json, and with $ErrorActionPreference='Stop'
            # that unwinds past the dataset loop into the outer catch - which is deliberately
            # NOT per-dataset, so the WHOLE run reports ABORTED and every remaining dataset is
            # skipped. Mode 1b compares the straight-through report first with no guard, so
            # this bites in exactly one case: straight-through fine, REHYDRATE report
            # malformed - precisely the regression this leg exists to catch.
            $m5d = $null
            try {
                if (Test-Path -LiteralPath $diagHtml) {
                    $m5d = Compare-DiagnosticsGolden -HtmlPath $diagHtml -GoldenDir $goldenDir `
                        -Tolerance $Tolerance -NoTrainedModel
                } else {
                    $m5d = [pscustomobject]@{ Pass = $false; Issues = [System.Collections.Generic.List[string]]@(
                        "diagnostics: the rehydrate wrote no model-diagnostics report at $diagHtml") }
                }
            } catch {
                $m5d = [pscustomobject]@{ Pass = $false; Issues = [System.Collections.Generic.List[string]]@(
                    ("diagnostics: the rehydrate's report at {0} could not be read: {1}" -f $diagHtml, $_.Exception.Message)) }
            }
            if ($m5d.Pass) {
                $summaryLines.Add("$name mode5 (rehydrate diagnostics vs golden): PASS")
            } else {
                $overallFail = $true
                Write-Problem-Tc "$name mode5 (rehydrate diagnostics vs golden): FAIL - $($m5d.Issues.Count) issue(s)"
                $m5d.Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
                $summaryLines.Add("$name mode5 (rehydrate diagnostics vs golden): FAIL ($($m5d.Issues.Count) issues)")
            }

            # Tier 2, the same absolute bounds mode 1b applies to the straight-through
            # report. Without it this leg has only the golden compare, and the golden is
            # regenerable: once a bad calibration is blessed by -CreateGolden, comparing the
            # rehydrate report against the poisoned baseline passes forever. That would leave
            # the one leg specifically exercising the streamed accumulator with no independent
            # correctness floor. $bounds is already computed per dataset, so this is free.
            $m5bounds = Get-SanityBounds $cfg
            $m5s = Test-DiagnosticsSanity -HtmlPath $diagHtml @m5bounds
            if ($m5s.Pass) {
                $summaryLines.Add("$name mode5 (rehydrate FDR sanity bounds): PASS")
            } else {
                $overallFail = $true
                Write-Problem-Tc "$name mode5 (rehydrate FDR sanity bounds): FAIL - calibration is out of bounds"
                $m5s.Issues | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
                $summaryLines.Add("$name mode5 (rehydrate FDR sanity bounds): FAIL ($($m5s.Issues.Count) issues)")
            }
        }
    }

    # ---- mode 6: the library-fragment release engaged on every leg that holds the library ----
    # Runs LAST because it reads the logs of all the legs above -- straight-through,
    # resume, and every phase of the HPC chain -- and they have to have been written.
    #
    # This is the assertion the blib comparators cannot make. The release is
    # output-neutral by design, so modes 1-4 pass identically whether it ran or was
    # deleted outright; #4534's review found three separate wirings where it silently
    # did not run, every one caught by a human reading these same logs by hand.
    #
    # The per-leg expectations are calibrated against an observed run, NOT derived from
    # reading the C# -- deriving them is how the original defects got in. Two surprises
    # from that observation are encoded here: --task PerFileRescoring DOES release
    # (FirstPassFdrTask.Rehydrate is reached through a lazy Demand even though IsIncluded
    # excludes it from that leg), and the warm re-run legitimately logs nothing at all
    # because a fully cached run does no work -- asserting a release there would be a
    # false red on every run.
    $releaseChecks = [System.Collections.Generic.List[hashtable]]::new()
    $releaseChecks.Add(@{
        Label = 'straight-through'; Log = (Join-Path $straightDir 'straight.log')
        Scopes = @($releaseScopeRescore, $releaseScopeReported)
        Freed  = @($releaseScopeRescore)
    })
    if (-not $SkipResume) {
        # The resume leg exercises FirstPassFdrTask.RUN, not its rehydrate arm: mode 2's
        # Invoke-ResumeInvalidation deletes the FirstPassFDR stamp, and mode 2 asserts
        # -ExpectRan @('FirstPassFDR', ...) on this very log to prove it. Worth checking
        # anyway - it is a second, independently-invalidated Run - but it is NOT rehydrate
        # coverage. The rehydrate arms are covered below and on the phase-3 workers.
        $releaseChecks.Add(@{
            Label = 'resume (FirstPassFDR re-runs)'; Log = (Join-Path $straightDir 'resume.log')
            Scopes = @($releaseScopeRescore, $releaseScopeReported)
            Freed  = @($releaseScopeRescore)
        })
    }
    if (-not $SkipRehydrate) {
        # THE OWN-SIDECAR REHYDRATE ARM, and the reason it needs its own entry: the release
        # shipped in #4534 not running on rehydrate at all, and that is the run an operator
        # reaches for AFTER the OOM this feature exists to prevent - the worst possible leg
        # to lose the saving on.
        #
        # No other check reaches it. The straight-through and resume legs both RUN
        # FirstPassFDR (above); mode 3's phase-3 workers do enter Rehydrate but adopt a
        # WORKER-supplied bundle, never FirstPassFdrTask.LoadOwnReconciliationBundle. Mode 5's
        # leg is the only one that rebuilds the bundle from the run's OWN sidecars, so
        # without this entry the own-sidecar call site is asserted zero times - delete the
        # release from it, run -SkipHpcChain, and mode 6 still reports PASS.
        $releaseChecks.Add(@{
            Label = 'own-sidecar rehydrate'; Log = (Join-Path $straightDir 'rehydrate.log')
            Scopes = @($releaseScopeRescore, $releaseScopeReported)
            Freed  = @($releaseScopeRescore)
        })
    }
    if (-not $SkipHpcChain) {
        # Read the PRESERVED copies under chain\logs, not the phase dirs: phases 1, 2
        # and every phase-3 worker are freed mid-chain to bound peak disk, so by the
        # time this runs only phase 4's dir still exists.
        $releaseLogDir = Join-Path $runRoot "$name\chain\logs"
        $releaseChecks.Add(@{
            Label = 'HPC --task FirstPassFDR'
            Log = (Join-Path $releaseLogDir 'phase2.log'); None = $true
        })
        foreach ($mzml in $inputs.Mzmls) {
            $stem = [IO.Path]::GetFileNameWithoutExtension($mzml)
            $releaseChecks.Add(@{
                Label = "HPC --task PerFileRescoring ($stem)"
                Log = (Join-Path $releaseLogDir "phase3_$stem.log")
                Scopes = @($releaseScopeRescore); Freed = @($releaseScopeRescore)
            })
        }
        # The SecondPassFDR node is the whole point of requiring a non-zero count anywhere: it is
        # the process that holds the fragment set through pass-2 Percolator, protein FDR
        # AND the blib write, and it realized ZERO saving until #4534 gave it its own
        # release. On the HPC chain it is the only release there is.
        $releaseChecks.Add(@{
            Label = 'HPC SecondPassFDR node'
            Log = (Join-Path $releaseLogDir 'phase4.log')
            Scopes = @($releaseScopeReported); Freed = @($releaseScopeReported)
        })
    }

    Write-Progress-Tc "${name}: library-fragment release engagement (mode 6)"
    $m6Issues = [System.Collections.Generic.List[string]]::new()
    $m6Matched = 0
    foreach ($check in $releaseChecks) {
        $r = if ($check.None) {
            Test-LibraryFragmentRelease -LogPath $check.Log -ExpectNone
        } else {
            Test-LibraryFragmentRelease -LogPath $check.Log `
                -ExpectScopes $check.Scopes -RequireFreed $check.Freed
        }
        $m6Matched += $r.Matched
        if (-not $r.Pass) {
            $r.Issues | ForEach-Object { $m6Issues.Add("$($check.Label): $_") }
        }
    }
    # Run-wide liveness. An -ExpectNone check PASSES on an empty result, so on its own it
    # cannot tell "this leg correctly released nothing" from "the C# log wording changed
    # and the pattern now matches nothing anywhere" - a negative assertion cannot fail
    # closed. Today the scoped checks happen to fail on the same drift, but that is an
    # accident of which legs are enabled: -SkipHpcChain -SkipResume -SkipRehydrate leaves
    # only positive checks whose absence is indistinguishable from a dead regex. Asserting
    # that SOMETHING matched somewhere makes the drift itself the failure.
    if ($m6Matched -eq 0) {
        $m6Issues.Add((("no leg logged a single release line matching '{0}' - the release " +
            "is off for the whole run, or the C# wording drifted from this pattern and " +
            "every assertion above is reading nothing") -f $releaseLinePattern))
    }
    if ($m6Issues.Count -eq 0) {
        $summaryLines.Add("$name mode6 (library-fragment release engaged): PASS")
    } else {
        $overallFail = $true
        Write-Problem-Tc "$name mode6 (library-fragment release engaged): FAIL - $($m6Issues.Count) issue(s)"
        $m6Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        $summaryLines.Add("$name mode6 (library-fragment release engaged): FAIL ($($m6Issues.Count) issues)")
    }

    # ---- mode 7: --task ModelDiagnostics regeneration acceptance ----------------
    # The contract of `--task ModelDiagnostics` is "regenerate the report for a COMPLETED
    # run and touch nothing else". Nothing gated that: the task declares no outputs (which
    # is what makes CanRehydrate return false and the task actually re-run), so no other
    # leg can reach it, and it shipped verified only by an ad-hoc script.
    #
    # Two assertions, because either alone passes on a broken feature. FILE-level: exactly
    # one artifact changed and it is the report - a regeneration that rewrote a sidecar or
    # the blib would silently corrupt the completed run it was asked to describe. VALUE-level:
    # the regenerated report still matches the SAME golden mode 1b compares the
    # straight-through report against - a run that touched nothing but emitted a different
    # page is equally broken, and the file check cannot see it.
    #
    # Runs LAST, in the straight-through dir, because it rewrites the report there; every
    # leg that reads that directory has already run. Costs ~14 s per dataset against a
    # ~5 min straight-through leg, because it rehydrates Stages 1-5 and re-runs Stage 7 only.
    #
    # -NoTrainedModel for mode 5's reason: a regeneration adopts q-values from the sidecars
    # instead of training Percolator, so featureCount is pinned at 0 rather than compared.
    if ($cfg.ModelDiagnostics) {
        Write-Progress-Tc "${name}: diagnostics regeneration acceptance (mode 7)"
        $m7Issues = [System.Collections.Generic.List[string]]::new()
        $m7Before = Get-DirFingerprint -Dir $straightDir
        $rMd = Invoke-OspreyRun -Mzmls $inputs.Mzmls -Library $inputs.Library `
            -Resolution $cfg.Resolution -WorkDir $straightDir -LogName 'mdtask.log' `
            -Spec $cfg -Manifest $inputs.Manifest -TaskName 'ModelDiagnostics'
        Write-Host ("  regeneration wall {0:N1}s" -f $rMd.Wall.TotalSeconds)

        # Logs are excluded: this leg writes its own, and every leg appends to its own log.
        $m7Changed = @(Compare-DirFingerprint -Before $m7Before -Dir $straightDir |
            Where-Object { $_ -notmatch '\.log$' })
        $reportLeaf = Split-Path -Leaf $diagHtml
        foreach ($c in $m7Changed) {
            if ($c -ne "modified: $reportLeaf") {
                $m7Issues.Add(("regeneration touched an artifact other than the report: {0}" -f $c))
            }
        }
        if ($m7Changed.Count -eq 0) {
            $m7Issues.Add(("regeneration changed nothing at all - the report at {0} was not " +
                "rewritten, so the task skipped itself instead of regenerating") -f $reportLeaf)
        }

        $m7d = $null
        try {
            $m7d = Compare-DiagnosticsGolden -HtmlPath $diagHtml -GoldenDir $goldenDir `
                -Tolerance $Tolerance -NoTrainedModel
        } catch {
            $m7Issues.Add(("the regenerated report at {0} could not be read: {1}" -f
                $diagHtml, $_.Exception.Message))
        }
        if ($m7d -and -not $m7d.Pass) {
            $m7d.Issues | ForEach-Object { $m7Issues.Add("vs golden: $_") }
        }

        if ($m7Issues.Count -eq 0) {
            $summaryLines.Add("$name mode7 (diagnostics regeneration: report only, vs golden): PASS")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode7 (diagnostics regeneration): FAIL - $($m7Issues.Count) issue(s)"
            $m7Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode7 (diagnostics regeneration): FAIL ($($m7Issues.Count) issues)")
        }
    }

    # ---- mode 8: a PARTIALLY completed rescore resumes and FINISHES ----------------
    # The state no other leg produces, which is why a real defect shipped. Mode 2 resumes from a
    # COMPLETE Stage-5 directory and mode 4 re-runs with EVERYTHING cached, so neither ever
    # presents a per-file set that is part done - and part done is what every interruption
    # leaves behind. PerFileRescoreTask read "ANY file has a current 2nd-pass sidecar" as "the
    # rescore is finished": a 446-run cohort killed at 141 came back, skipped Stage 5 correctly,
    # rescored NOTHING, and left 305 runs to carry 1st-pass q-values into the picked-protein FDR
    # and the blib - then rebuilt the whole survivor pool toward ~86 GB (2026-09-03).
    #
    # Runs LAST, after mode 7, because it invalidates and rewrites the blib in the
    # straight-through directory; every leg that reads that directory has already run.
    if (-not $SkipResume) {
        Write-Progress-Tc "${name}: partial rescore resume (mode 8)"
        # Captured BEFORE the invalidation: the resume overwrites the blib in place. Mode 1 has
        # already proved this blib matches the committed golden, so comparing against it is
        # comparing against the golden one hop removed - and it stays correct if the golden is
        # ever refreshed.
        $m8Expected = Join-Path $straightDir 'output.blib.premode8'
        Copy-Item (Join-Path $straightDir 'output.blib') $m8Expected -Force
        $m8Cut = Invoke-PartialRescoreInvalidation -WorkDir $straightDir
        Write-Host ("  invalidated the rescore for {0} of {1} run(s)" -f $m8Cut.Cut, $m8Cut.Runs)

        $m8Inputs = @($inputs.Mzmls | ForEach-Object { Join-Path $straightDir (Split-Path $_ -Leaf) })
        $rPartial = Invoke-OspreyRun -Mzmls $m8Inputs -Library $inputs.Library -Resolution $cfg.Resolution `
            -WorkDir $straightDir -LogName 'partial-resume.log' -Spec $cfg -Manifest $inputs.Manifest `
            -AllowNonZeroExit
        $m8Issues = [System.Collections.Generic.List[string]]::new()

        if ($cfg.ModelDiagnostics) {
            # This leg FAILS here, and that is the honest answer: under --model-diagnostics the
            # per-run hydrate is off and no worker bundle exists, so a partial resume cannot
            # finish the cohort. Reporting PASS because the refusal is well-worded would encode
            # the limitation into the gate - the exact trap mode 6 is in with "release engaged",
            # where the assertion checks that a MECHANISM ran rather than that a PROPERTY holds.
            #
            # What -AllowNonZeroExit buys is only that the refusal no longer ABORTS the run, so
            # the legs after this one still execute. It does not make the outcome a pass.
            #
            # The guard IS asserted, because a refusal that is silent or unnamed is worse than
            # one that is loud: before it existed this leg wrote a blib 236 RefSpectra keys short
            # and reported success.
            $m8Issues.Add(("cannot finish the cohort under --model-diagnostics: no per-run " +
                           "hydrate and no worker bundle, so the amputated run has no plan " +
                           "source. Tracked as the --model-diagnostics work; this leg goes green " +
                           "when that lands, and needs no change here when it does"))
            $m8Guard = Test-LogMarker -LogPath $rPartial.Log `
                -Marker 'still need re-scoring, but this process has no plan to do it' `
                -Description 'the rescore naming how many runs it cannot finish, and why'
            foreach ($issue in $m8Guard.Issues) { $m8Issues.Add($issue) }
        }
        else {
            # COUNT. The assertion the defect failed outright: the broken build left the count at the
            # untouched runs and still reported success.
            $m8Recon = @(Get-ChildItem $straightDir -Filter '*.scores-reconciled.parquet' -File |
                         Where-Object { $_.Name -notlike '*.osprey.task' }).Count
            if ($m8Recon -ne $m8Cut.Runs) {
                $m8Issues.Add("only $m8Recon of $($m8Cut.Runs) reconciled parquet(s) after the resume; the rescore did not finish the cohort")
            }

            # VISIBILITY. How much was reused has to be STATED, not inferred from what the run does
            # next; a resume nobody can audit is one nobody can trust after an interruption.
            $m8Marker = Test-LogMarker -LogPath $rPartial.Log `
                -Marker 'Rescore resume:' `
                -Description 'the rescore reporting how many runs it adopted and how many it re-scored'
            foreach ($issue in $m8Marker.Issues) { $m8Issues.Add($issue) }

            # VALUE. Finishing is not finishing CORRECTLY. An interrupted run that completes to a
            # different answer than an uninterrupted one is the failure that actually matters for the
            # resume promise, and the COUNT check above cannot see it.
            $m8 = Compare-BlibFull -BlibExpected $m8Expected `
                -BlibActual (Join-Path $straightDir 'output.blib') -Tolerance $Tolerance
            foreach ($issue in $m8.Issues) { $m8Issues.Add($issue) }
        }
        Remove-Item $m8Expected -Force -ErrorAction SilentlyContinue

        if ($m8Issues.Count -eq 0) {
            $summaryLines.Add("$name mode8 (partial rescore resume): PASS ($($m8Cut.Cut) of $($m8Cut.Runs) run(s) re-scored)")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode8 (partial rescore resume): FAIL - $($m8Issues.Count) issue(s)"
            $m8Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode8 (partial rescore resume): FAIL ($($m8Issues.Count) issues)")
        }
    }

    # ---- mode 9: a CRASH-shaped half-done file is re-scored, not skipped -----------
    # The state mode 8 structurally cannot present. The rescore writes a run's reconciled
    # parquet, stamps it, and only then writes the 2nd-pass sidecar; a process that dies
    # between those two leaves a file the cohort count calls outstanding and the per-file
    # skip calls complete. Mode 8 amputates BOTH products, so its two checks agree and the
    # split never appears.
    #
    # It is not hypothetical: a native AccessViolation killed a 446-file run mid-stamp on
    # 2026-09-04, and the resume then logged 448 "skipping (outputs valid)" lines and ZERO
    # rescores - in a run whose own header said one file still needed re-scoring. The blib
    # came out silently missing that run.
    #
    # Runs after mode 8 and rebuilds from the same directory, so it inherits a cohort mode 8
    # has already restored to whole.
    if (-not $SkipResume -and $cfg.ModelDiagnostics) {
        # SKIP, not FAIL, and the distinction is deliberate. This leg's property - a half-done
        # file is RE-SCORED rather than skipped - requires the resume to be able to rescore at
        # all, and under --model-diagnostics it cannot: no per-run hydrate, no worker bundle, so
        # no plan source. Mode 8 already asserts that exact gap on this dataset and fails on it.
        # A second leg failing for the same reason adds a red without adding information, and
        # three of the four datasets carry mdiag - so it would be three extra reds all saying
        # what mode 8 already said. Same shape as mode 3's own mdiag skip.
        #
        # This goes green on its own when the mdiag work lands, with no edit here.
        $summaryLines.Add("$name mode9 (crash-shaped half-done resume): SKIP (--model-diagnostics has no plan source; mode 8 asserts that gap)")
    }
    elseif (-not $SkipResume) {
        Write-Progress-Tc "${name}: crash-shaped half-done resume (mode 9)"
        $m9Expected = Join-Path $straightDir 'output.blib.premode9'
        Copy-Item (Join-Path $straightDir 'output.blib') $m9Expected -Force
        $m9Cut = Invoke-PartialRescoreInvalidation -WorkDir $straightDir -Pass2SidecarOnly
        Write-Host ("  cut the 2nd-pass sidecar for {0} of {1} run(s), leaving their reconciled parquets stamped" -f
            $m9Cut.Cut, $m9Cut.Runs)

        $m9Inputs = @($inputs.Mzmls | ForEach-Object { Join-Path $straightDir (Split-Path $_ -Leaf) })
        $r9 = Invoke-OspreyRun -Mzmls $m9Inputs -Library $inputs.Library -Resolution $cfg.Resolution `
            -WorkDir $straightDir -LogName 'crash-shaped-resume.log' -Spec $cfg -Manifest $inputs.Manifest `
            -AllowNonZeroExit
        $m9Issues = [System.Collections.Generic.List[string]]::new()

        # THE assertion. A run that skips the cut files re-scores nothing and still exits 0,
        # which is exactly how this shipped: the count and the skip disagreed and nobody
        # compared them. Requiring a rescore LINE is what makes the disagreement visible.
        $m9Rescored = @(Select-String -Path $r9.Log -Pattern 'Re-scoring file ' -SimpleMatch `
            -ErrorAction SilentlyContinue)
        if ($m9Rescored.Count -lt $m9Cut.Cut) {
            $m9Issues.Add((("only {0} file(s) were re-scored after cutting {1} run(s)' 2nd-pass " +
                "sidecar - the resume treated a half-done file as complete, which is the " +
                "silent-drop defect this leg exists for") -f $m9Rescored.Count, $m9Cut.Cut))
        }
        if ($r9.ExitCode -ne 0) {
            $m9Issues.Add("the resume exited $($r9.ExitCode); a recoverable half-done file must not fail the run")
        }

        # VALUE. Finishing is not finishing correctly - the re-scored file has to land the
        # same answer the uninterrupted run did.
        $m9Blib = Compare-BlibFull -BlibExpected $m9Expected `
            -BlibActual (Join-Path $straightDir 'output.blib') -Tolerance $Tolerance
        foreach ($issue in $m9Blib.Issues) { $m9Issues.Add($issue) }
        Remove-Item $m9Expected -Force -ErrorAction SilentlyContinue

        if ($m9Issues.Count -eq 0) {
            $summaryLines.Add("$name mode9 (crash-shaped half-done resume): PASS ($($m9Cut.Cut) of $($m9Cut.Runs) run(s) re-scored)")
        } else {
            $overallFail = $true
            Write-Problem-Tc "$name mode9 (crash-shaped half-done resume): FAIL - $($m9Issues.Count) issue(s)"
            $m9Issues | Select-Object -First 15 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
            $summaryLines.Add("$name mode9 (crash-shaped half-done resume): FAIL ($($m9Issues.Count) issues)")
        }
    }

    # All legs for this dataset are done -- free its scratch now so peak disk stays
    # at ~one dataset (the next dataset / the perf-gate step gets the space back).
    Remove-Scratch (Join-Path $runRoot $name)
}
}
catch {
    # A throw from any leg (a nonzero Osprey exit, a missing input, a comparator
    # blowing up) used to escape straight past the summary and the buildProblem
    # line, so a red CI gate surfaced as a bare stack trace with no statement of
    # WHAT failed. Record it as a failure like any other and fall through to the
    # normal reporting below. Deliberately not per-dataset: a throw is usually the
    # environment or the binary, and running the remaining datasets against a
    # broken build would only bury the real message.
    $overallFail = $true
    $failMsg = $_.Exception.Message
    Write-Problem-Tc "Osprey regression aborted: $failMsg"
    $summaryLines.Add("ABORTED: $failMsg")
    # The stack trace is diagnosis, not the verdict, so it goes to the log under
    # the verdict rather than replacing it.
    Write-Host ($_.ScriptStackTrace) -ForegroundColor DarkGray
}
finally {
    # Restore the operator's environment, whatever happened above.
    if ([string]::IsNullOrWhiteSpace($script:priorAllowResident)) {
        Remove-Item Env:OSPREY_ALLOW_UNFIXED_RESIDENT -ErrorAction SilentlyContinue
    } else {
        $env:OSPREY_ALLOW_UNFIXED_RESIDENT = $script:priorAllowResident
    }
    # Safety net for a dataset that threw before its own cleanup -- drop the whole
    # run root. Raw input data lives outside $runRoot and is untouched.
    Remove-Scratch $runRoot
}

# --- Read-only data folders unchanged across the WHOLE run --------------------
# Compared against the fingerprint taken before the first dataset ran, so this
# covers every leg of every dataset (the per-dataset check above only sees the
# straight-through leg). Deliberately outside the try/finally cleanup: the run
# root is gone by now, and the data dirs are the only thing still being asserted.
foreach ($d in $watchedDirs) {
    $changed = Compare-DirFingerprint -Before $runStartFp[$d] -Dir $d
    if ($changed.Count -eq 0) {
        $summaryLines.Add("data dir unchanged across run: $d")
    } else {
        $overallFail = $true
        Write-Problem-Tc "read-only data dir CHANGED across the run: $d -- $($changed.Count) file(s)"
        $changed | Select-Object -First 20 | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        $summaryLines.Add("data dir CHANGED across run ($($changed.Count) files): $d")
    }
}

# --- Summary + exit -----------------------------------------------------------
Write-Host ""
Write-Host "=== Osprey regression summary ===" -ForegroundColor Cyan
$summaryLines | ForEach-Object { Write-Host "  $_" }

# The gaps this gate KNOWS it still traverses. Printed green-or-red runs alike: these
# are not failures (the legs above passed), they are the O(files) paths a passing gate
# is nonetheless walking, and a passing gate is exactly when nobody goes looking.
Write-Host ""
Write-Host "=== Known O(files) resident paths this gate still traverses ===" -ForegroundColor Cyan
if ($knownResidentGaps.Count -eq 0) {
    Write-Host "  none" -ForegroundColor Green
} else {
    foreach ($g in $knownResidentGaps) {
        Write-Host ("  {0}  token: {1}" -f $g.Issue, $g.Token) -ForegroundColor Yellow
        Write-Host ("      {0}" -f $g.Path)
        Write-Host ("      {0}" -f $g.Legs)
    }
    # DERIVED, not typed. A literal count reads identically before and after a gap is
    # closed, so it can never report the invariant it claims to state - and a hardcoded 0
    # sitting three lines under a table naming a required token is worse than no line.
    $requiredTokens = @($knownResidentGaps | Where-Object { $_.Token -and $_.Token -ne 'NONE' })
    Write-Host (("  Tokens REQUIRED by this gate: {0} (target: 0). Each must have an open " +
        "issue to remove it.") -f $requiredTokens.Count)
}
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
