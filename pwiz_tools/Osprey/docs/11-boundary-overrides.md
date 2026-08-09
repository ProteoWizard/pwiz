# 11. Boundary Overrides & Rescore (C#)

> Pipeline stage: Stage 6 (per-file rescore + gap-fill). C# port of Rust docs/13-boundary-overrides.md. Corresponds to Rust osprey `run_search(boundary_overrides)` / `run_rescore`.

This document describes how the C# port re-scores previously-scored entries at
caller-supplied RT boundaries — the mechanism Rust exposes as the
`boundary_overrides` parameter of `run_search()`. In C# the override triple flows
through a `ScoringContext.BoundaryOverrides` map into the same
`ScoringPipeline.RunCoelutionScoring` path the first-pass search uses, so
re-scored entries get features on exactly the same scale as first-pass entries.
Stage 6 is driven by `PerFileRescoreTask` (`--task PerFileRescoring`), planned by
`Stage6Planner`, and consumes the plan produced by cross-run reconciliation
(see 10-cross-run-reconciliation.md) and multi-charge consensus (see
09-multi-charge-consensus.md).

## Overview: the C# override channel

Rust threads an `Option<&HashMap<u32, (f64, f64, f64)>>` parameter into
`run_search()`. C# has no extra parameter on the scoring entry point; instead the
override map is carried on the scoring context:

- `ScoringContext.BoundaryOverrides` is an
  `IReadOnlyDictionary<uint, (double Apex, double Start, double End)>` keyed by
  `LibraryEntry.Id` / `FdrEntry.EntryId` (`Osprey.Scoring/ScoringContext.cs:58`).
- `ScoringPipeline.RunCoelutionScoring(...)` is the `run_search` equivalent
  (`Osprey.Scoring/ScoringPipeline.cs:65`). It takes the same context whether it
  is first-pass scoring or Stage 6 re-scoring.
- Inside per-candidate peak extraction, `PeakDataExtractor.TryExtract` looks up
  the candidate id in `context.BoundaryOverrides`
  (`Osprey.Scoring/PeakDataExtractor.cs:88`). A hit switches the candidate onto
  the override peak-construction path; a miss uses the normal CWT detection path.

This is the same design the Rust doc describes: first-pass search and every
re-scoring path (multi-charge consensus, cross-run reconciliation, gap-fill
forced integration) share one code path; only the source of the boundaries
differs.

## Where the override triples come from

Stage 6 planning (`Stage6Planner.Plan`, `Osprey.Tasks/Stage6Planner.cs:80`) runs
four phases in Rust order (multi-charge consensus, cross-run consensus RTs,
per-file calibration refit, reconciliation planning). Three of those feed
override boundaries into the rescore:

### 1. Multi-charge consensus (forced boundaries)

`Stage6Planner.ComputeMultiChargeConsensus`
(`Osprey.Tasks/Stage6Planner.cs:106`) calls
`MultiChargeConsensus.SelectRescoreTargets(entries, config.RunFdr)` per file. When
a peptide has FDR-passing detections at several charge states, the best
SVM-scoring charge defines the consensus `(apex, start, end)`; the other charge
states are re-scored there. These land in
`PerFileConsensusTargets` keyed by file name.

### 2. UseCwtPeak (stored CWT candidate)

`ReconcileAction.UseCwtPeak` — during reconciliation planning
(`Stage6Planner.PlanReconciliation`, `Osprey.Tasks/Stage6Planner.cs:258`), if the
current peak's apex is not at the expected consensus RT but a stored CWT
candidate is, the planner switches to that candidate's `(start, apex, end)`. CWT
candidates are stored during the first-pass search (up to
`ReconciliationConfig.TopNPeaks`, default 5, `Osprey.Core/ReconciliationConfig.cs:36`)
in the per-file `.scores.parquet` and loaded selectively by `CwtCandidateLoader`.

### 3. ForcedIntegration (imputed boundaries)

`ReconcileAction.ForcedIntegration` — when no stored CWT candidate contains the
expected RT, the planner imputes boundaries from the expected RT and half the
median peak width: `(expected_rt, expected_rt - half_width, expected_rt +
half_width)`. `GroupReconciliationActionsByFile`
(`Osprey.Tasks/PerFileRescoreTask.cs:1016-1021`) expands the stored
`ExpectedRt`/`HalfWidth` into the `(apex, start, end)` triple exactly as the Rust
doc specifies.

The `UseCwtPeak` / `ForcedIntegration` split is modeled as a discriminated union
`ReconcileAction` in `Osprey.FDR/Reconciliation`; the planner never emits a
`Keep` action into the map (Keep is the implicit default and is absent from the
persisted arrays).

A fourth source — **gap-fill** — is C#-specific to this stage's write-up but is
part of the same reconciliation machinery (see the Gap-fill section below).

## Merging consensus and reconciliation targets per file

`TryAssembleRescoreTargets` (`Osprey.Tasks/PerFileRescoreTask.cs:879`) builds one
combined override map per file:

1. Multi-charge consensus targets are inserted first
   (`PerFileRescoreTask.cs:905-906`).
2. Reconciliation targets are inserted second and **overwrite on conflict**
   (`PerFileRescoreTask.cs:907-908`) — reconciliation wins because the
   inter-replicate boundary carries more information (refined calibration +
   cross-run consensus RT). This matches the Rust doc's "reconciliation wins on
   conflict" rule.
3. Gap-fill targets are collected separately (they append new rows rather than
   re-scoring existing ones).

A single `RunCoelutionScoring` call per file then processes all override entries
together, so spectra are loaded only once per file.

## The per-file rescore loop

`PerFileRescoreTask.Run` (`Osprey.Tasks/PerFileRescoreTask.cs:185`) pulls the
planning byproducts through the typed pipeline registry
(`ctx.Get<CompactedEntries>()`, `ctx.Get<ReconciliationActions>()`,
`ctx.Get<PerFileConsensusTargets>()`, `ctx.Get<PerFileGapFillForRescore>()`, etc.)
and dispatches into `ExecuteRescore` (`PerFileRescoreTask.cs:491`). Per file,
`RescoreOneFile` (`PerFileRescoreTask.cs:644`) does:

1. **Resume probe** — `TryResumeRescoredFile` skips a file whose
   `.scores-reconciled.parquet` + resume sidecar are already valid, overlaying the
   reconciled boundaries in place (`PerFileRescoreTask.cs:827`).
2. **Assemble targets** — `TryAssembleRescoreTargets` (consensus + reconciliation
   dedup + gap-fill); bails on no-work files.
3. **Build the scoring subset** — `BuildScoringSubset`
   (`PerFileRescoreTask.cs:1066`) turns `combinedTargets` into a
   `boundary_overrides` map keyed by `EntryId` and a library subset containing
   only the entries being re-scored.
4. **Reload spectra** — `LoadSpectraForRescore` (`PerFileRescoreTask.cs:1485`)
   prefers the `.spectra.bin` cache, falling back to re-parsing the mzML.
5. **Reload mass calibrations** — `LoadMassCalibrations`
   (`PerFileRescoreTask.cs:1531`) reads the sibling `.calibration.json` for MS2/MS1
   m/z calibration and the first-pass RT MAD. This is a hard requirement: a
   missing/unreadable calibration sidecar throws (no silent uncalibrated
   fallback).
6. **Pick RT calibration** — refined (Stage 6 refit) wins, first-pass falls back
   (`PerFileRescoreTask.cs:712-713`).
7. **Score the subset** — a fresh `ScoringContext` carries
   `BoundaryOverrides = boundaryOverrides` and `OriginalRtMad`
   (`PerFileRescoreTask.cs:732-735`); `RunCoelutionScoring(..., passLabel:
   "Re-scoring")` re-scores.
8. **Overlay results** — `OverlayRescoredEntries` (`PerFileRescoreTask.cs:1119`)
   writes each re-scored `FdrEntry` back onto the per-file stub by `EntryId`,
   preserving `ParquetIndex` and resetting the discriminant fields to Rust
   `to_fdr_entry` defaults (Score 0.0, all six q-values 1.0, Pep 1.0) so Stage 7
   second-pass Percolator recomputes them.
9. **Gap-fill two-pass** — `RunGapFillTwoPass` (see below).
10. **Write-back** — `WriteReconciledAndStamp` (`PerFileRescoreTask.cs:944`) writes
    the reconciled parquet and stamps the resume sidecar only on success.

## How overrides work inside the scorer (step by step)

`PeakDataExtractor.TryExtract` (`Osprey.Scoring/PeakDataExtractor.cs:63`) mirrors
the detection half of `run_search`. With `overrideBounds` set:

### Step 1: Override detection

`context.BoundaryOverrides.TryGetValue(candidate.Id, out var bnd)` sets a nullable
`overrideBounds` triple (`PeakDataExtractor.cs:87-92`). The rest of the method
branches on `overrideBounds.HasValue`.

### Step 2: RT range selection

`FindScanRange` (`PeakDataExtractor.cs:491`) uses the override boundaries ± a
margin rather than expected-RT ± tolerance:

```
peakWidth = max(0.1, End - Start)
margin    = max(0.2, peakWidth)
rtLo = Start - margin ; rtHi = End + margin
```

Scans with `windowRts[i]` in `[rtLo, rtHi]` define `[startScan, endScan]`. This is
byte-equivalent to the Rust `target_start - margin .. target_end + margin`
window.

### Step 3: Pre-filter skip

The top-fragment signal pre-filter runs only when
`config.PrefilterEnabled && !overrideBounds.HasValue`
(`PeakDataExtractor.cs:117`). Override entries skip it entirely — the caller has
already decided these boundaries must be scored.

### Step 4: RT to XIC index mapping (synthetic peak)

Instead of CWT peak detection, `DetectCandidatePeaks`
(`PeakDataExtractor.cs:543`) routes overrides to `BuildOverridePeaks`
(`PeakDataExtractor.cs:614`), which maps the target RTs onto the reference XIC's
RT axis with `ScoringMath.BinarySearchLowerBound` (Rust `partition_point`
semantics):

- `startIdx = lowerBound(rt, Start)`, then `startIdx--` (Rust `saturating_sub(1)`),
  clamped to `len-1`.
- `endIdx = lowerBound(rt, End)`, clamped to `len-1`.
- `apexIdx = lowerBound(rt, Apex)`, refined to the nearer neighbor, then clamped
  to `[startIdx, endIdx]`.

The reference XIC is the highest total-intensity fragment selected with `>=`
(last-wins on ties) to match Rust `max_by` tie behavior — a deliberately
documented parity point (`PeakDataExtractor.cs:618-639`).

A single synthetic `XICPeakBounds` is returned with `Area` from
`PeakDetector.TrapezoidalArea` and `SignalToNoise` from `PeakDetector.ComputeSnr`.
The apex-acceptance RT filter that gates normal peaks
(`PeakDataExtractor.cs:258`) is bypassed for overrides.

### Step 5: Feature computation

From the synthetic peak, the scorer computes all 21 PIN features through the same
calculators the first-pass path uses (see 03-spectral-scoring.md), sharing the
per-window pre-processed XCorr vectors. No separate re-scoring code path exists.

### Early return on insufficient data

`BuildOverridePeaks` returns `null` (candidate dropped) when the reference XIC has
fewer than 3 points (`if (last < 2) return null;`, `PeakDataExtractor.cs:643`) or
the mapped range is too narrow (`if (endIdx <= startIdx + 1) return null;`,
`PeakDataExtractor.cs:663`) — the same two guards the Rust doc lists.

## Gap-fill two-pass (Phase 2)

For peptides that pass FDR in sibling replicates but were not detected in this
file, `RunGapFillTwoPass` (`Osprey.Tasks/PerFileRescoreTask.cs:1337`) runs two
passes over target-only gap-fill entries (decoys are intentionally excluded —
see the method's doc comment for the exact-duplicate-row rationale):

1. **CWT pass** — a cloned config with `PrefilterEnabled = false` and **no**
   boundary overrides, so CWT picks peaks freely
   (`passLabel: "Gap-fill scoring"`, `PerFileRescoreTask.cs:1370-1385`). Hits are
   appended as new stubs with `ParquetIndex = uint.MaxValue` and score-reset
   fields.
2. **Forced-integration pass** — for targets the CWT pass missed, an override map
   `(ExpectedRt, ExpectedRt - HalfWidth, ExpectedRt + HalfWidth)` re-scores at the
   imputed window (`passLabel: "Gap-fill forced integration"`,
   `PerFileRescoreTask.cs:1444-1453`). These land through the same override path
   as reconciliation `ForcedIntegration`.

Counts flow into `RescoreStats.TotalGapCwt` / `TotalGapForced`.

## Search-label progress phases

`RunCoelutionScoring` takes a `passLabel` argument
(`Osprey.Scoring/ScoringPipeline.cs:74`) that names the progress phase; the
reporter renders `"{passLabel} isolation windows"` and defaults to `"Scoring"`
when null (`ScoringPipeline.cs:266`). The C# phase labels are:

| Phase | C# `passLabel` | Rust `search_label` |
|-------|----------------|---------------------|
| First-pass search | (null) -> `Scoring` | `Scoring` |
| Consensus + reconciliation re-score | `Re-scoring` | `Re-scoring` |
| Gap-fill CWT pass | `Gap-fill scoring` | `Gap-fill CWT` |
| Gap-fill forced pass | `Gap-fill forced integration` | `Gap-fill forced` |

Under `--parallel-files`, each file's rescore is divided into three equal
progress segments (reload spectra / re-score subset / write reconciled parquet),
advanced via `MultiProgressReporter.Current?.BeginSegment()`
(`PerFileRescoreTask.cs:103`, `:693`, `:742`, `:789`).

## Reconciled parquet write-back

`ReconciledParquetWriter.Write` (`Osprey.Tasks/ReconciledParquetWriter.cs:54`)
reads the original Stage 4 `.scores.parquet`, replaces the re-scored rows,
appends gap-fill rows, and writes a **separate** `.scores-reconciled.parquet`
sibling (the original stays intact for crash-resume safety). The footer metadata
stamps `osprey.reconciled = "true"` and `osprey.reconciliation_hash` alongside
`osprey.version`, `osprey.search_hash`, and `osprey.library_hash`
(`ReconciledParquetWriter.cs:200-204`), which the downstream `--task
SecondPassFDR` node validates.

Note: six per-row blob columns (`fragment_mzs`, `fragment_intensities`,
`reference_xic_rts`, `reference_xic_intensities`, `bounds_area`, `bounds_snr`) are
currently written null/zero — a tracked follow-up noted in
`RescoreWorker.cs:64-71`, not a boundary-override algorithm difference.

## Worker mode, hydration, and compaction

`RescoreWorker.Run` (`Osprey/RescoreWorker.cs:80`) is now a thin alias for
`new AnalysisPipeline().Run(config)` (Phase C): the `--task PerFileRescoring`
worker reuses the canonical pipeline, and the upstream boundary state is
rehydrated lazily rather than hand-assembled.

- `RescoreHydration.HydrateForRescore` (`Osprey.Tasks/RescoreHydration.cs:166`)
  loads pre-compaction `FdrEntry` stubs from each `.scores.parquet`, overlays the
  `.1st-pass.fdr_scores.bin` v3 sidecar (SVM scores + 4 q-values + PEP +
  `RunProteinQvalue`), and parses each `reconciliation.json` envelope into the
  action map + refined calibration + gap-fill targets + join-wide
  `first_pass_base_ids` (v3).
- `RescoreCompaction.Apply` (`Osprey.Tasks/RescoreCompaction.cs:113`) reproduces
  the in-process first-pass compaction: it keeps exactly the join-wide
  `GlobalFirstPassBaseIds` set (hard-fails if absent), **unioned** with the
  base_ids of every reconciliation-action target so cross-file-rescued entries
  survive. Compaction is keyed by `base_id` (`0x7FFFFFFF` mask) so a target and
  its paired decoy stay together, and the action map is re-keyed to
  post-compaction indices.

## Flags and switches

This stage has no dedicated boundary-override CLI flag; the override triples are
computed by Stage 6 planning. The flags that affect this stage:

| Flag / field | Default | Effect on this stage |
|--------------|---------|----------------------|
| `--task {PerFileScoring\|FirstPassFDR\|PerFileRescoring\|SecondPassFDR}` | (in-process, all stages) | `PerFileRescoring` runs this stage as a standalone worker (internal `HpcTask.PerFileRescore`). `SecondPassFDR` (`HpcTask.SecondPassFdr`) rehydrates reconciled parquets instead of re-scoring. |
| `--input-scores <paths\|dir>` | — | Supplies the boundary `.scores.parquet` files the worker rescores; drives `IsIncluded` (`PerFileRescoreTask.cs:123`). |
| `--reconciliation-compaction-fdr <v>` | 0.01 (`OspreyConfig.ReconciliationCompactionFdr`) | First-pass compaction predicate applied upstream in FirstPassFDR; determines which entries survive into the rescore set. |
| `ReconciliationConfig.Enabled` | true | Gates reconciliation planning + `reconciliation.json` inputs (`PerFileRescoreTask.cs:158`). Disabling leaves only multi-charge consensus rescore. |
| `ReconciliationConfig.ConsensusFdr` | 0.01 (`ReconciliationConfig.cs:39`) | Threshold for consensus peptide selection, calibration refit, and reconciliation planning (`Stage6Planner.cs`). Not a CLI flag; config field. |
| `ReconciliationConfig.TopNPeaks` | 5 (`ReconciliationConfig.cs:36`) | CWT candidates stored per precursor at first-pass; the pool `UseCwtPeak` overrides are drawn from. |
| `--no-prefilter` (`PrefilterEnabled`) | true | Only affects non-override entries; override entries always skip the pre-filter (`PeakDataExtractor.cs:117`). The gap-fill CWT pass forces prefilter off regardless (`PerFileRescoreTask.cs:1375`). |
| `--parallel-files [N]` | off (sequential) | Runs per-file rescores concurrently under `EffectiveFileParallelism` (`PerFileRescoreTask.cs:547`); output is byte-identical to sequential (gated by regression.ps1). |
| `--threads <count>` | all cores | Inner window parallelism per file; divided across concurrent files under `--parallel-files` (`PerFileRescoreTask.cs:684`). |
| `--protein-fdr <v>` | optional (`EffectiveProteinFdr`) | Feeds the protein-rescue gate into consensus RT computation (`Stage6Planner.cs:176`), affecting which entries become reconciliation targets. |

Diagnostic env vars that dump this stage (each with a `*_ONLY=1` early-exit
variant): `OSPREY_DUMP_MULTICHARGE`, `OSPREY_DUMP_CONSENSUS`,
`OSPREY_DUMP_INV_PREDICT`, `OSPREY_DUMP_LOESS_FIT`, `OSPREY_DUMP_REFIT`,
`OSPREY_DUMP_RECONCILIATION`, `OSPREY_DUMP_RESCORED`, plus the per-entry
`OSPREY_DIAG_SEARCH_ENTRY_IDS` search-XIC dump and `OSPREY_DUMP_CWT_PATH`
(`Stage6Planner.cs`, `PerFileRescoreTask.cs:297`, `PeakDataExtractor.cs:154`).
`OSPREY_TRACE_PEPTIDE` per-peptide tracing is documented in 18-peptide-trace.md.

## Divergences from the Rust documentation

- **[INTENTIONAL-CSHARP-DESIGN] Override map carried on the context, not a
  function parameter** - Rust doc says `run_search()` takes a
  `boundary_overrides: Option<&HashMap<...>>` parameter; C# has no such parameter
  on `RunCoelutionScoring` and instead carries the map on
  `ScoringContext.BoundaryOverrides`, read inside `PeakDataExtractor.TryExtract`.
  Behavior is identical (same lookup, same override path).
  Evidence: `Osprey.Scoring/ScoringContext.cs:58`,
  `Osprey.Scoring/PeakDataExtractor.cs:88`. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Optional file-level parallelism on the rescore
  loop** - The Rust doc (and Rust CLAUDE.md memory-architecture notes) describe
  reconciliation re-scoring as strictly sequential across files (`iter_mut`, one
  ~3 GB spectra load at a time), relying only on window-level parallelism inside
  `run_search`. C# keeps that window-level parallelism but additionally runs whole
  files concurrently under `--parallel-files` / `EffectiveFileParallelism`, with
  a per-file GC drop skipped in the parallel case. Output is byte-identical
  (regression.ps1-gated); this is an added performance option, not an algorithm
  change. Evidence: `Osprey.Tasks/PerFileRescoreTask.cs:547-584`, `:809-810`.
  Severity: minor.

- **[STALE-RUST-DOC] Gap-fill two-pass not covered by the boundary-override
  doc** - Rust docs/13 documents only the three override types (consensus,
  UseCwtPeak, ForcedIntegration). The C# rescore also runs a gap-fill two-pass
  (CWT-free-pick pass + forced-integration pass) for peptides missing from a
  file, driven through the same override channel. This is part of the
  reconciliation machinery both implementations share (Rust
  `identify_gap_fill_targets`); the boundary-override doc simply predates/omits
  it. Evidence: `Osprey.Tasks/PerFileRescoreTask.cs:1337-1477`. Severity: info.

- **[STALE-RUST-DOC] Gap-fill progress labels differ cosmetically** - Rust doc
  lists progress labels `Gap-fill CWT` and `Gap-fill forced`; C# uses
  `Gap-fill scoring` and `Gap-fill forced integration`. Console-string only; no
  effect on outputs. Evidence: `Osprey.Tasks/PerFileRescoreTask.cs:1385`, `:1453`.
  Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Reconciled parquet written to a separate
  sibling** - The Rust doc frames re-scoring as updating the per-file cache in
  place. C# writes a separate `.scores-reconciled.parquet` (leaving the original
  Stage 4 parquet intact) for crash-resume safety, stamping
  `osprey.reconciled` / `osprey.reconciliation_hash` in the footer. Content of the
  re-scored rows matches; only the output file layout differs.
  Evidence: `Osprey.Tasks/ReconciledParquetWriter.cs:54`, `:200-204`,
  `Osprey.Tasks/PerFileRescoreTask.cs:145-151`. Severity: minor.

- **[UNVERIFIED] Six per-row blob columns written null/zero in the reconciled
  parquet** - `RescoreWorker`'s summary states `fragment_mzs`,
  `fragment_intensities`, `reference_xic_rts`, `reference_xic_intensities`,
  `bounds_area`, and `bounds_snr` are currently written null/zero, tracked as a
  follow-up. This is a serialization gap in the reconciled parquet, not in the
  boundary-override scoring itself (features and RT boundaries are computed and
  written). A human should confirm whether any downstream consumer reads those
  six columns off the reconciled parquet. Evidence: `Osprey/RescoreWorker.cs:64-71`.
  Severity: minor.

Verified to match the Rust doc step for step: the single shared scoring path for
first-pass and re-scoring; override detection by entry id; the
override-window ± margin RT range (`max(0.1, End-Start)`, `max(0.2, width)`);
pre-filter skip for overrides; `partition_point`-equivalent RT-to-index mapping
with `saturating_sub(1)` on start and nearest-neighbor apex refinement; the two
early-return guards (`ref_xic.len() < 3`, `end_index <= start_index + 1`);
consensus-first / reconciliation-wins merge order; and one `RunCoelutionScoring`
call per file so spectra load once.

See 10-cross-run-reconciliation.md for the full reconciliation algorithm,
09-multi-charge-consensus.md for consensus leader selection, 05-rt-alignment.md
for how expected/consensus RTs are computed, 02-xcorr-scoring.md for per-window
XCorr preprocessing, 07-fdr-control.md for the second-pass FDR that consumes the
re-scored features, 14-intermediate-files.md for the `.scores-reconciled.parquet`
/ `reconciliation.json` / `.calibration.json` / `.spectra.bin` sidecars, and
15-hpc-scoring-split.md for the `--task` worker fan-out.
