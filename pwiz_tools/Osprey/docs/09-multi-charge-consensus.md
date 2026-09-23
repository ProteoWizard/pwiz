# 09. Multi-Charge Consensus (C#)

> Pipeline stage: Stage 5 (post-FDR consensus). C# port of Rust docs/06-multi-charge-consensus.md. Corresponds to Rust osprey `select_post_fdr_consensus`.

## Problem

A peptide elutes at one retention time regardless of ionization charge state. In DIA, different charge states of the same peptide (e.g. `PEPTIDEK` at 2+ and 3+) have different precursor m/z and fall into **different DIA isolation windows**. Those windows are scored by independent parallel work, so each charge state finds its chromatographic peak on its own — and they can land on different peaks. If 2+ is detected at RT 25.3 and 3+ at RT 27.1, one of them is on the wrong peak. Forcing all charge states of a peptide to share one apex and one integration window improves accuracy and downstream quantification consistency.

Because charge states live in different windows, cross-charge agreement cannot be reached inside the per-window scoring loop. Osprey resolves it in a **post-FDR** step: it picks a consensus leader per peptide, then re-scores the disagreeing charge states at the leader's boundaries via the same forced-integration (boundary-override) path used by reconciliation.

## Where this runs in the pipeline

Multi-charge consensus is **Phase 1 of Stage 6 planning**, and it is the first planning phase to run — before cross-run consensus RTs, before the calibration refit, before reconciliation planning. See `Osprey.Tasks/Stage6Planner.cs:80` (`Plan`) and `Stage6Planner.cs:106` (`ComputeMultiChargeConsensus`, "runs first per Rust pipeline.rs:3217").

Execution order (C# task graph):

```text
Stage 4  Per-file scoring (PerFileScoringTask)          -> per-file .scores.parquet
Stage 5  First-pass FDR (FirstPassFdrTask / FirstPassFDR)  -> run-level q-values on FdrEntry stubs
   |         (compaction drops non-passing entries here)
   v
Stage 6  Planning (Stage6Planner, invoked from FirstPassFdrTask):
           Phase 1  MultiChargeConsensus.SelectRescoreTargets  <-- THIS DOC
           Phase 2  ConsensusRts.Compute        (cross-run, multi-file only)
           Phase 3  RefitCalibrations
           Phase 4  ReconciliationPlanner        (cross-run, multi-file only)
   |
   v
Stage 6  Re-scoring (PerFileRescoreTask / PerFileRescoring):
           merge consensus + reconciliation targets (reconciliation wins on conflict)
           re-score via RunCoelutionScoring with BoundaryOverrides
   |
   v
Stage 7  Second-pass FDR (SecondPassFdrTask / SecondPassFDR)   -> final q-values
```

The consensus selection itself is a pure, side-effect-free function; the re-scoring that acts on its output is shared with cross-run reconciliation (see 10-cross-run-reconciliation.md) and the forced-integration mechanism (see 11-boundary-overrides.md).

Note on grouping vs. the Rust doc's four-line sketch: the Rust doc labels consensus "Step 4 / post first-pass FDR." In the C# port the same relationship holds — `SelectRescoreTargets` reads `RunPrecursorQvalue`, which is only populated after first-pass FDR — and consensus is invoked from `FirstPassFdrTask` (the first-pass FDR task) after compaction. `FirstPassFdrTask.cs:146` and `Stage6Planner.cs:115` are the two call sites (compute path and bundle-rehydrate path); both pass `config.RunFdr` as the threshold.

## Algorithm

The whole selection is `MultiChargeConsensus.SelectRescoreTargets(entries, fdrThreshold)` in `Osprey.FDR/Reconciliation/MultiChargeConsensus.cs:51`. It returns a list of `(int Index, double Apex, double Start, double End)` rescore targets — the index is the position in the per-file `entries` list, and the RT triple is the consensus leader's window.

### Step 1: Group by modified sequence

`MultiChargeConsensus.cs:58-68` builds a `Dictionary<string, List<int>>` keyed by `FdrEntry.ModifiedSequence` using `StringComparer.Ordinal`. Grouping by modified sequence naturally separates:

- **Different charge states** of one peptide (same `ModifiedSequence`, different `Charge`) — these land in the same group and are the whole point of the step.
- **Targets from decoys** — decoys carry a `DECOY_` prefix in `ModifiedSequence`, so `DECOY_PEPTIDEK` and `PEPTIDEK` are distinct keys (verified by `ReconciliationTest.cs:1164` `TestMultiChargeDecoyAndTargetAreSeparateGroups`).
- **Different peptides** — distinct keys, independent groups (`ReconciliationTest.cs` covers this via separate-peptide cases).

Groups with a single member are skipped (`MultiChargeConsensus.cs:74-75`) — nothing to reconcile.

### Step 2: Select the consensus leader

For each multi-member group, `PickBestPassing` (`MultiChargeConsensus.cs:105`) chooses the leader:

1. **FDR gate**: only entries with `RunPrecursorQvalue <= fdrThreshold` are eligible (`MultiChargeConsensus.cs:115`). If no charge state passes, the group contributes nothing (`bestIdx < 0`, `MultiChargeConsensus.cs:78-79`). This matches the Rust "only FDR-passing charge states can lead" rule and the "skip groups where no charge passes" optimization (`ReconciliationTest.cs:1057` `TestMultiChargeNoPassingEntryInGroupSkipped`).
   - Note the gate is specifically `RunPrecursorQvalue`, not the effective `max(precursor, peptide)` q-value — this is the run-level precursor q-value from first-pass FDR.
2. **Ranking**: among passing entries, the highest `FdrEntry.Score` (the raw SVM discriminant) wins (`MultiChargeConsensus.cs:123-130`). The SVM score is preferred over `coelution_sum` because it folds in all 21 PIN features (see 03-spectral-scoring.md).
3. **Tie-break**: on an exact score tie, the lower `RunPrecursorQvalue` wins (`MultiChargeConsensus.cs:138`); on a *full* tie (equal score AND equal q-value) the **later** entry wins, because the comparison uses `<=` (`entry.RunPrecursorQvalue <= bestQvalue`). This deliberately mirrors Rust `max_by`, which returns the last element among equal maxima (comment cites `pipeline.rs:7665-7679`). A `MultiChargeConsensusTest.cs:53` test (`TestConsensusLeaderTieBreakPrefersLastEntry`) pins this exact behavior and would fail if the code reverted to a strict `<`.

The leader's `ApexRt`, `StartRt`, `EndRt` become the consensus window (`MultiChargeConsensus.cs:81-83`).

### Step 3: Mark divergent charge states for re-scoring

`MultiChargeConsensus.cs:84-94` computes the match tolerance and flags outliers:

- `consensusWidth = EndRt - StartRt`
- `rtMatchTolerance = Math.Max(MIN_RT_MATCH_TOLERANCE, consensusWidth / 2.0)` where `MIN_RT_MATCH_TOLERANCE = 0.1` min (`MultiChargeConsensus.cs:43`). So the tolerance is half the consensus peak width, floored at 0.1 min — identical to the Rust "within half the consensus peak width (minimum 0.1 min)."
- For each non-leader in the group, if `|ApexRt - consensusApex| > rtMatchTolerance` it is added as a rescore target `(idx, consensusApex, consensusStart, consensusEnd)`; otherwise it already agrees and is left alone (no target emitted, entry kept as-is downstream).

Worked example matching the Rust doc: leader window `[24.8, 25.9]` gives width 1.1, tolerance `max(0.1, 0.55) = 0.55`; a 2+ apex at 25.3 (`diff 0.0`) is kept, a 3+ apex at 27.1 (`diff 1.8 > 0.55`) becomes a rescore target pinned to `(25.3, 24.8, 25.9)`. Test coverage: `ReconciliationTest.cs:1091` (`TestMultiChargeWrongApexChargeIsRescoreTarget`), `:1074` (same-apex → no rescore), `:1113` (three charges, only the divergent one rescored).

### Step 4: Merge with reconciliation and re-score via boundary overrides

The consensus targets are merged with cross-run reconciliation actions in `PerFileRescoreTask.TryAssembleRescoreTargets` (`Osprey.Tasks/PerFileRescoreTask.cs:879`). Both are folded into one `Dictionary<int, (Apex, Start, End)>` keyed by entry index:

```
foreach consensus target:      combinedTargets[idx] = (apex, start, end)   // PerFileRescoreTask.cs:905-906
foreach reconciliation target: combinedTargets[idx] = (apex, start, end)   // PerFileRescoreTask.cs:907-908
```

Because reconciliation is applied second, **reconciliation wins on conflict** when both want to re-score the same entry — the inter-replicate boundary is more authoritative than the intra-file multi-charge boundary (`PerFileRescoreTask.cs:869-873`). This matches the Rust "reconciliation wins" rule.

`BuildScoringSubset` (`PerFileRescoreTask.cs:1066`) then turns the combined index map into `BoundaryOverrides` keyed by `entry_id` (`FdrEntry.EntryId`, `PerFileRescoreTask.cs:1079-1080`) plus a subset `List<LibraryEntry>` containing only the entries to re-score. The overrides are attached to the `ScoringContext` (`PerFileRescoreTask.cs:733`) and consumed by the scoring engine.

Inside scoring, `PeakDataExtractor.cs:87-92` detects a boundary override for a candidate and, when present:

- **Skips the signal pre-filter** — `config.PrefilterEnabled && !overrideBounds.HasValue` (`PeakDataExtractor.cs:117`); the caller has already decided to score here.
- **Skips CWT peak detection** — a synthetic peak is built from the override triple instead of running the 3-tier CWT (`PeakDataExtractor.cs:165-167` and the override branch of `FindScanRange`/`DetectCandidatePeaks`).
- **Maps the RT triple to XIC scan indices** via the override-shaped `FindScanRange` (`PeakDataExtractor.cs:104`).
- **Computes all features at those boundaries**, same feature calculators as first-pass.
- **Drops the entry when there is no signal** at the consensus RT: too few scans in range (`PeakDataExtractor.cs:79-80`, `:107-108`) or fewer than 2 fragment XICs (`PeakDataExtractor.cs:162-163`) all return `false`, so the entry produces no re-scored result and is excluded.

See 11-boundary-overrides.md for the full forced-integration path.

## Why "best SVM score wins"

The C# ranking uses the trained SVM discriminant (`FdrEntry.Score`) among FDR-passing charge states, matching the Rust rationale: the SVM score integrates all 21 discriminative features and a charge state parked on an interference peak scores lower than one on the true peak. Alternatives (highest `coelution_sum`, highest apex intensity, closest-to-expected-RT, majority vote) are the same alternatives the Rust doc lists and rejects.

## Shared peak boundaries at blib output (Stage 7)

The consensus leader above re-scores *divergent* charge states during Stage 6. A
separate, final step at blib-write time (`SecondPassFdrTask.BuildSharedBoundaries`)
makes **all** charge states of one peptide in one file report the **same** RT
window in the `.blib` `RetentionTimes` rows: per `(modified_sequence, fileName)`
the window is taken from the entry with the **minimum run q-value**.

On a run-q **tie** — e.g. two charge states both gap-filled at `q = 1.0` — the
winner is broken deterministically by **lowest charge**
(`rq < existing || (rq == existing && e.Charge < existingCharge)`), *not* by
in-memory entry order. This tie-break is load-bearing for parity: the C# side
iterates per-file entries in memory order while Rust iterates parquet row order,
so without the explicit lowest-charge rule the two implementations could keep
different charges' windows and the blib `RetentionTimes` `startTime`/`endTime`
would diverge (it fixed a 20-row Astral transfer-compete divergence). Both impls
now apply the identical *(lower run q-value, then lower charge)* rule. Pinned by
`MultiChargeConsensusTest.TestSharedBoundariesTieBrokenByLowestCharge`.

## Determinism

- Grouping uses a `Dictionary` (unordered iteration), but this does not affect output: each group's leader is chosen deterministically by `PickBestPassing` (a total order over score then q-value then position), and the emitted targets are consumed by index into keyed dictionaries (`combinedTargets` keyed by entry index at `PerFileRescoreTask.cs:906`, `BoundaryOverrides` keyed by `entry_id` at `PerFileRescoreTask.cs:1080`), so target-list order is immaterial.
- The re-scored entries themselves flow back through `RunCoelutionScoring`, which carries its own deterministic ordering; the reconciled write-back preserves `ParquetIndex` so post-compaction Vec position never corrupts the Parquet row mapping (`PerFileRescoreTask.cs:1101-1105`).

See 16-determinism.md.

## Scope

- **Post-FDR**: consensus needs `RunPrecursorQvalue`, which exists only after first-pass FDR; only FDR-passing charge states can lead.
- **Per-file**: `SelectRescoreTargets` is called once per file over that file's `entries` list (`Stage6Planner.cs:112-116`), so consensus is computed within each file independently.
- **Targets and decoys separately**: enforced by grouping on `ModifiedSequence` (the `DECOY_` prefix separates them).
- **Merged with reconciliation**: consensus and reconciliation rescore targets share one `RunCoelutionScoring` pass per file; reconciliation wins conflicts (`PerFileRescoreTask.cs:869-908`).

### What stays unchanged

- CWT peak detection in the first-pass per-precursor loop (see 06-peak-detection.md) — consensus only overrides it for re-scored entries.
- All 21 PIN features and Percolator scoring (see 03-spectral-scoring.md, 07-fdr-control.md).
- FDR control, blib output, everything downstream.

## Flags and switches

This stage has no dedicated on/off CLI flag — multi-charge consensus always runs as Phase 1 of Stage 6 planning. The flags that affect its inputs and behavior:

| Flag / env var | Default | Effect on this stage |
|---|---|---|
| `--run-fdr <q>` | `0.01` (`OspreyConfig.RunFdr`) | The `fdrThreshold` passed to `SelectRescoreTargets` (`Stage6Planner.cs:115`, `FirstPassFdrTask.cs:146`). Gates which charge states are eligible to lead (`RunPrecursorQvalue <= threshold`) and, implicitly, whether a group produces any rescore target at all. |
| `--no-prefilter` | prefilter ON (`PrefilterEnabled = true`) | Only relevant to the re-scoring pass: with the prefilter on it is still skipped for boundary-override candidates (`PeakDataExtractor.cs:117`), so this flag does not change consensus re-scoring behavior; it affects only free (non-override) scoring. |
| `--reconciliation-compaction-fdr`, `--experiment-fdr`, reconciliation flags | see 08/10 docs | Do not gate consensus selection itself, but reconciliation targets computed later are merged with (and override) consensus targets in the shared re-scoring pass. |
| `OSPREY_DUMP_MULTICHARGE=1` | off | Diagnostic. Writes `cs_stage6_multicharge.tsv` (per-file entries + selected rescore targets) for cross-impl bisection (`Stage6Planner.cs:124-134`, `OspreyFileDiagnostics.cs:261-264`). |
| `OSPREY_MULTICHARGE_ONLY=1` | off | Diagnostic. Exits after the multicharge dump (`Stage6Planner.cs:135-136`, `OspreyFileDiagnostics.cs:266-267`). |

`MIN_RT_MATCH_TOLERANCE = 0.1` min is a compile-time constant (`MultiChargeConsensus.cs:43`), not a flag.

## Divergences from the Rust documentation

- **[STALE-RUST-DOC] Re-scoring engine is `RunCoelutionScoring`, not `run_search()`** - Rust doc says entries are re-scored "via `run_search()` with `boundary_overrides`." C# has no `run_search`; the boundary-override re-scoring is `RunCoelutionScoring` with `ScoringContext.BoundaryOverrides`, driven from `PerFileRescoreTask`. Behavior is equivalent (skip prefilter, skip CWT, force integration at the consensus window). Evidence: `Osprey.Scoring/PeakDataExtractor.cs:82-167`, `Osprey.Tasks/PerFileRescoreTask.cs:733`. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Consensus is a pure selection function separated from re-scoring** - The Rust doc describes one `select_post_fdr_consensus()` that both selects and hands off to re-scoring. The C# port splits this: `MultiChargeConsensus.SelectRescoreTargets` is a pure function returning `(Index, Apex, Start, End)` targets, and the merge-with-reconciliation + boundary-override re-scoring lives in `PerFileRescoreTask.TryAssembleRescoreTargets`/`BuildScoringSubset`. Same overall algorithm, cleaner separation for the .NET task graph. Evidence: `MultiChargeConsensus.cs:51`, `PerFileRescoreTask.cs:879,1066`. Severity: info.

- **[STALE-RUST-DOC] "Percolator/Mokapot scoring" — C# has no Mokapot** - Rust "What stays unchanged" lists "Percolator/Mokapot scoring." The C# port uses a native managed Percolator SVM only; Mokapot is not wired to the CLI. This does not affect consensus logic. Evidence: see 07-fdr-control.md. Severity: info.

- **[STALE-RUST-DOC] FDR gate is `RunPrecursorQvalue`, doc says generic "FDR threshold"** - The Rust doc phrases the eligibility gate as "passing the FDR threshold" without naming the q-value. C# gates specifically on `RunPrecursorQvalue <= fdrThreshold` (run-level precursor q-value), not the effective `max(precursor, peptide)` q-value. The C# source comment confirms this matches the Rust code at `pipeline.rs:7665-7679`, so the doc is merely less specific than the implementation. Evidence: `MultiChargeConsensus.cs:115`. Severity: info.

Otherwise the C# implementation matches the Rust documentation step for step: group by modified sequence (`MultiChargeConsensus.cs:58`), best-SVM-score-among-FDR-passing leader with last-wins-on-full-tie (`:105-149`), half-peak-width tolerance floored at 0.1 min (`:84-85`), rescore targets pinned to the leader's window (`:93`), reconciliation-wins merge (`PerFileRescoreTask.cs:905-908`), and boundary-override re-scoring that skips prefilter + CWT and drops entries with no signal (`PeakDataExtractor.cs:82-167`).
