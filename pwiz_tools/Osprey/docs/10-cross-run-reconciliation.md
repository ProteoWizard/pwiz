# 10. Cross-Run Reconciliation (C#)

> Pipeline stage: Stage 5 (plan) / Stage 6 (apply). C# port of Rust docs/10-cross-run-reconciliation.md. Corresponds to Rust osprey cross-run peak reconciliation (`crates/osprey/src/reconciliation.rs`).

Cross-run reconciliation aligns peak integration boundaries across replicate files so that the same peptide is quantified at a consistent chromatographic position in every run. It runs after first-pass (run-level) FDR and before the final experiment-level FDR, and only applies to multi-file experiments. The C# port keeps the algorithm identical to Rust but splits it across two HPC stages joined by a per-file `<stem>.reconciliation.json` envelope:

- **Stage 5 — planning** (`Osprey.Tasks/Stage6Planner.cs`, driven by `FirstPassFdrTask` / `--task FirstPassFDR`): computes consensus RTs, refits per-file calibration, and produces the Keep / UseCwtPeak / ForcedIntegration action plan plus gap-fill targets. Writes them to `reconciliation.json`.
- **Stage 6 — apply** (`Osprey.Tasks/PerFileRescoreTask.cs`, `--task PerFileRescoring`): re-scores the flagged entries at the planned boundaries.
- **Stage 7 — second-pass FDR** (`SecondPassFdrTask`, `--task SecondPassFDR`): recomputes experiment-level q-values on the reconciled pool.

The core planning/consensus code lives in `Osprey.FDR/Reconciliation/`. See also 09-multi-charge-consensus.md (the sibling post-FDR alignment that shares the same rescore pass), 07-fdr-control.md (the two FDR passes bracketing reconciliation), 08-protein-parsimony.md (first-pass protein q-values used as a rescue gate), 11-boundary-overrides.md (the rescore mechanism), and 15-hpc-scoring-split.md (the `--task` worker split).

---

## Problem

Without reconciliation, each file's per-window search finds peaks independently using CWT peak detection and pairwise coelution scoring. For a given peptide the search may confidently find the correct peak in most runs but select a co-eluting interferer, a noise spike, or an isomer in one or more runs where the signal is weaker.

The first line of defense is RT-penalized peak selection during the first-pass search (a Gaussian RT penalty × log-intensity weight applied to CWT candidate ranking). That lives in the scoring stage, not here — see 06-peak-detection.md. Reconciliation is the second line of defense: it uses the high-confidence runs to establish where a peptide actually elutes, then goes back to files where the selected peak is wrong and either switches to an alternate CWT candidate at that RT or imputes the integration boundaries at the expected position.

---

## Algorithm Overview

The four planning phases run in `Stage6Planner.Plan` (`Osprey.Tasks/Stage6Planner.cs:80`), in this order:

```text
After per-run (first-pass) FDR:
  1. MultiChargeConsensus.SelectRescoreTargets  — intra-file multi-charge leaders (09-multi-charge-consensus.md)
  2. ConsensusRts.Compute                        — sigmoid(score)-weighted median library RT across runs
  3. CalibrationRefit.Refit                      — tighter per-run LOESS from consensus points
  4. ReconciliationPlanner.Plan                  — Keep | UseCwtPeak | ForcedIntegration per entry
  (then) GapFillTargetIdentifier.Identify        — precursors missing from a file (FirstPassFdrTask)
  (then) PerFileRescoreTask                       — re-score via boundary overrides (Stage 6)
  (then) SecondPassFdrTask                            — second-pass experiment-level FDR (Stage 7)
```

Cross-run consensus, refit, and reconciliation planning only run when there is more than one file: `ConsensusRts.Compute` is called only when `perFileEntries.Count > 1` (`Stage6Planner.cs:171-178`); with a single file the consensus list is empty and phases 3-4 degenerate to no work, leaving multi-charge consensus rescore as the only Stage 6 work. Multi-charge consensus (phase 1) runs unconditionally.

---

## Step 1: Consensus Peptide Selection

`ConsensusRts.Compute` (`Osprey.FDR/Reconciliation/ConsensusRts.cs:70`) selects the peptides whose detections drive the consensus RT. The qualification gate is `ConsensusRts.Qualifies` (`ConsensusRts.cs:242-250`):

1. A detection must **not** be a decoy.
2. Its **per-entry run precursor q-value** must be `<= consensusFdr` (default 0.01). This is a **hard gate** (`ConsensusRts.cs:246`) — protein FDR cannot rescue poor precursor evidence.
3. EITHER its run peptide-level q-value is `<= consensusFdr`, OR (protein-rescue) `proteinFdrThreshold > 0` AND its first-pass run protein q-value is `<= proteinFdrThreshold` (`ConsensusRts.cs:248-249`). The threshold passed in is `config.EffectiveProteinFdr` (`Stage6Planner.cs:176`); when protein FDR is disabled it is 0 and the rescue branch is off.

### Why precursor-level evidence is a hard gate

Consensus RT is driven by each surviving entry's own `ApexRt`. If a detection qualified only because its protein group was strong (but the entry itself has a weak/wrong-peak apex), that wrong apex would be pulled into the weighted median and could shift the consensus toward an interferer. Requiring `RunPrecursorQvalue <= consensusFdr` first prevents strong proteins' wrong-peak charge-state detections from poisoning the charge-agnostic consensus RT. This mirrors the Rust regression case (Stellar, DAQVVGMTTTGAAK) documented in the source Rust doc; the C# port applies the identical gate.

### Paired decoys via base_id linkage

Paired decoys are included so the second FDR pass sees fair competition. The C# port pairs decoys by **base_id** (`EntryId & 0x7FFFFFFF`), not by stripping a `DECOY_` prefix from the modified sequence (`ConsensusRts.cs:93-118`). This is deliberate: the prefix-strip approach only works for Osprey-generated reverse decoys and silently misses library-supplied decoys (Carafe / FDRBench manifest) whose modified sequence carries no prefix. Qualifying target base_ids are recorded (`ConsensusRts.cs:102`), then any decoy sharing a qualifying base_id contributes all of its detections (`ConsensusRts.cs:115-116`). Target and decoy consensus RTs are computed independently to avoid information leakage.

---

## Step 2: Consensus Library RT

For each consensus peptide, its qualifying detections across all files are collected as `(FileName, ApexRt, Score, PeakWidth, CoelutionSum)` where `PeakWidth = EndRt - StartRt` (`ConsensusRts.cs:143-150`). Each measured `ApexRt` is mapped back to library RT space using the run's inverse calibration: `cal.InversePredict(det.ApexRt)` (`ConsensusRts.cs:171`).

The **consensus library RT** is the **weighted median** of these library RTs (`ConsensusRts.WeightedMedian`, `ConsensusRts.cs:264-287` — cumulative-weight median over value-sorted pairs). Weights come from the SVM discriminant score through a sigmoid, floored at 1e-6:

```text
weight = max(1e-6, 1 / (1 + exp(-score)))     // ConsensusRts.cs:178
```

The SVM score is the strongest per-detection quality signal (it is what FDR ranks on). A detection with positive score dominates the weighted median; a detection with negative score (interferer-like) contributes near-zero weight. The 1e-6 floor guarantees a non-zero total weight even when every score is very negative.

Two sanity filters run before a detection reaches the median (`ConsensusRts.cs:172`): the inverse-predicted library RT must be finite, and `CoelutionSum > 0` (rejects anti-correlated "noise integration" detections). Per-peptide peak widths are aggregated with the same sigmoid-score weighted median (`ConsensusRts.cs:180, 197`), so `MedianPeakWidth` reflects high-quality detections' peak shapes.

The result is a `PeptideConsensusRT` (`Osprey.FDR/Reconciliation/PeptideConsensusRT.cs`) carrying `ModifiedSequence`, `IsDecoy`, `ConsensusLibraryRt`, `MedianPeakWidth`, `NRunsDetected`, and `ApexLibraryRtMad`.

### Within-peptide MAD

`ApexLibraryRtMad` is the median absolute deviation of a peptide's library-space apex RTs across replicates, computed only when `NRunsDetected >= 3` (otherwise null — a MAD on 2 points is not robust) (`ConsensusRts.cs:203-214`). It captures the LC/instrument reproducibility floor and feeds the planner's global RT tolerance (Step 4).

### Charge-agnostic grouping (multi-charge / GPF)

Consensus keys are `(ModifiedSequence, IsDecoy)` — **charge is not part of the key** (`ConsensusRts.cs:137`). All detections of a peptide across every file and charge state contribute to one shared `PeptideConsensusRT`. A peptide's 2⁺ in file1 and 3⁺ in file2 reinforce each other, and gas-phase fractionation (where different charge states land in different files) is handled automatically because the cross-run consensus IS the multi-charge alignment. The per-file `MultiChargeConsensus.SelectRescoreTargets` (09-multi-charge-consensus.md) handles the standard-DIA intra-file case and is effectively a no-op under GPF.

### Determinism

The consensus list is sorted `(IsDecoy, ModifiedSequence ordinal)` before returning (`ConsensusRts.cs:231-237`), giving deterministic output regardless of input file order.

---

## Step 3: Calibration Refit

`CalibrationRefit.Refit` (`Osprey.FDR/Reconciliation/CalibrationRefit.cs:48`) refits each run's LOESS RT calibration from `(consensus_library_rt → measured_apex_rt)` pairs, using **target** peptides in that run whose **experiment-level** q-value at `FdrLevel.Both` is `<= consensusFdr` (`CalibrationRefit.cs:73`).

The refit configuration (`CalibrationRefit.cs:92-98`):
- `Bandwidth = 0.3`
- `OutlierRetention = 1.0` — outlier removal disabled (these are FDR-controlled detections; LOESS robustness iterations still downweight stragglers)
- `MinPoints = MIN_CONSENSUS_POINTS = 20`
- `ClassicalRobustIterations = OspreyEnvironment.LoessClassicalRobust` — mirrors the Stage 4 initial calibration so refit fitted values stay cross-impl identical

If fewer than 20 consensus points exist for a run, or the fit throws, `Refit` returns null and that run falls back to its original first-pass calibration (`CalibrationRefit.cs:81-82, 104-110`; consumed at `Stage6Planner.cs:222-225` and `ReconciliationPlanner.cs:156-157`).

---

## Step 4: Reconciliation Planning

`ReconciliationPlanner.Plan` (`Osprey.FDR/Reconciliation/ReconciliationPlanner.cs:65`) assigns each entry a `ReconcileAction`. For each entry it predicts `expectedRt = cal.Predict(consensusEntry.ConsensusLibraryRt)` from the refined (or fallback original) calibration (`ReconciliationPlanner.cs:212`), then compares apex proximity via `DetermineAction`.

### The three actions

`ReconcileAction` (`Osprey.FDR/Reconciliation/ReconcileAction.cs`) is a discriminated union with a private constructor and three nested subclasses:

| Action | Condition (`ReconciliationPlanner.DetermineAction`, `ReconciliationPlanner.cs:248-291`) | What happens |
|--------|------------------------------------------------------------------------------------------|--------------|
| **Keep** (singleton `ReconcileAction.Keep`) | `\|apexRt - expectedRt\| <= rtTolerance` | No change; omitted from the returned map (implicit absence). |
| **UseCwtPeak** | A stored CWT candidate has `\|cand.ApexRt - expectedRt\| <= rtTolerance` | Switch to the candidate whose apex is **closest** to expectedRt. Carries `CandidateIndex`, `StartRt`, `ApexRt`, `EndRt`. |
| **ForcedIntegration** | Neither current peak nor any CWT candidate is within tolerance | Integrate at `expectedRt ± halfWidth`, where `halfWidth = MedianPeakWidth / 2` (`ReconciliationPlanner.cs:225`). |

`DetermineAction` uses **apex proximity**, not boundary containment: a wrong-apex peak with a wide trailing edge spanning the expected RT is correctly rejected. CWT candidate selection also picks the closest apex, not merely any candidate whose boundaries overlap (`ReconciliationPlanner.cs:259-285`). Only non-Keep actions are stored, keyed `(fileName, entryIndex)` (`ReconciliationPlanner.cs:227-229`).

### Entry preconditions

Two filters run before an entry is planned:
1. The entry's `(ModifiedSequence, IsDecoy)` must be in the consensus map (`ReconciliationPlanner.cs:200-202`).
2. The entry's `(EntryId & 0x7FFFFFFF, Charge)` must be in `passingBaseIds` — precursors where the minimum of the four q-values (run/experiment × precursor/peptide) clears `experimentFdr` (`ReconciliationPlanner.cs:131-144, 209`). Reconciliation must include a passing precursor (and its paired decoy, matched by base_id) in every file to keep per-file boundaries self-consistent. Note the planner receives `config.Reconciliation.ConsensusFdr` as its `experimentFdr` argument (`Stage6Planner.cs:285`), which defaults to 0.01.

### RT tolerance: global within-peptide MAD

`rtTolerance` is driven by the experiment's **within-peptide** RT reproducibility, not cross-peptide calibration residuals:

```text
globalWithinPeptideMadLib = median over target consensus peptides with ApexLibraryRtMad set
                            of their per-peptide ApexLibraryRtMad          // ReconciliationPlanner.cs:96-118
peptideTolerance = min( max(globalWithinPeptideMadLib * 1.4826 * 3.0, 0.1),
                        fileCalToleranceCeiling )                          // ReconciliationPlanner.cs:188-190
```

Constants: `MIN_RT_TOLERANCE = 0.1`, `MAD_TO_SIGMA = 1.4826`, `SIGMA_FACTOR = 3.0` (`ReconciliationPlanner.cs:40, 47, 50`). When no target peptide has `>= 3` detections (e.g. a 2-replicate experiment), the global MAD falls back to `FALLBACK_GLOBAL_MAD_LIB = 0.05` (`ReconciliationPlanner.cs:44, 108`).

Using a single global number (the median across all peptides) avoids the self-fulfilling-prophecy failure of a per-point local tolerance, where a wrong-apex peptide inflates the tolerance at its own RT. Per-peptide MADs are still computed and stored on `PeptideConsensusRT` for diagnostics but are not the primary tolerance.

### Per-file safety ceiling (sigma-clipped)

The per-file calibration MAD is retained as a ceiling so a pathologically tight global MAD cannot force every peak to be re-scored (`ReconciliationPlanner.cs:166-183`):

```text
rawMad         = cal.Stats().MAD
clipThreshold  = rawMad * 1.4826 * 3.0
clippedMad     = SigmaClippedMad(cal.AbsResiduals, clipThreshold)   // ReconciliationPlanner.cs:299-322
refinedTol     = max(clippedMad * 1.4826 * 3.0, 0.1)
cap            = max(originalCal.MAD * 1.4826 * 3.0, 0.1)           // each pass can only tighten
ceiling        = min(refinedTol, cap)
```

`SigmaClippedMad` filters absolute residuals at the clip threshold and returns the median of the survivors, falling back to the unclipped median if fewer than `SIGMA_CLIP_MIN_SURVIVORS = 20` remain (`ReconciliationPlanner.cs:299-322`). This sigma-clipping is more detailed than the plain "per-file calibration MAD ceiling" the Rust prose describes; the code comment ties it to the Rust docstring at `reconciliation.rs:570-607`, so the two implementations agree (see Divergences).

### CWT candidates

CWT candidates were stored during the first-pass search (default 5 per precursor, `ReconciliationConfig.TopNPeaks`) and persisted in the per-file Parquet score cache. During planning they are loaded selectively via `CwtCandidateLoader.Load` (`Stage6Planner.cs:267`), keyed per entry by `ParquetIndex` (`ReconciliationPlanner.cs:214-218`) — avoiding a full re-extraction. `ForcedIntegration` is the last resort when no stored candidate's apex lands within tolerance.

---

## Step 5: Re-Scoring via Boundary Overrides (Stage 6)

`PerFileRescoreTask` (`Osprey.Tasks/PerFileRescoreTask.cs`) applies the plan. It reuses the same coelution scoring path as the first pass with a `BoundaryOverrides` map, so re-scoring inherits parallel window processing and per-window XCorr preprocessing (see 11-boundary-overrides.md and 02-xcorr-scoring.md).

1. **Merge targets** (`TryAssembleRescoreTargets`, `PerFileRescoreTask.cs:879-932`): multi-charge consensus targets and reconciliation targets are merged into one per-file map keyed by entry index. On conflict, **reconciliation wins** — its inter-replicate boundary is more authoritative (`PerFileRescoreTask.cs:904-908`).
2. **Build boundary overrides** (`GroupReconciliationActionsByFile`, `PerFileRescoreTask.cs:997-1037`): `UseCwtPeak → (ApexRt, StartRt, EndRt)`; `ForcedIntegration → (ExpectedRt, ExpectedRt - HalfWidth, ExpectedRt + HalfWidth)`. The `BuildScoringSubset` step (`PerFileRescoreTask.cs:1066`) keys these by `EntryId` and subsets the library to only the entries being re-scored.
3. **Call the search** with `context.BoundaryOverrides` set (`PerFileRescoreTask.cs:733`). Entries with an override skip the pre-filter and CWT peak detection, map RT boundaries to XIC scan indices, and recompute all 21 PIN features at the override boundaries.
4. **Write back** the re-scored entries into the reconciled Parquet, keyed by `ParquetIndex` to survive first-pass compaction.

Files are processed **sequentially** at the file level because each file loads spectra (~1-2 GB) plus its Parquet entries; parallel file loading would OOM. Parallelism is within a file, across isolation windows.

---

## Step 6: Second-Pass FDR (Stage 7)

After reconciliation and gap-fill, `SecondPassFdrTask` (`--task SecondPassFDR`) recomputes experiment-level q-values on the updated pool (features recomputed at consensus-aligned boundaries) using the same native Percolator SVM FDR pipeline as the first pass. These are the final experiment-level q-values written to the blib. See 07-fdr-control.md.

---

## Gap-Fill

If a precursor passed run-level FDR in one replicate but was never scored in another, reconciliation alone would leave that file with no integration and the blib missing a per-file boundary. `GapFillTargetIdentifier.Identify` (`Osprey.FDR/Reconciliation/GapFillTargetIdentifier.cs:68`) closes this hole.

### Identification

It builds the set of passing precursors `(ModifiedSequence, Charge)` — any target whose minimum of the four q-values is `<= experimentFdr` (`GapFillTargetIdentifier.cs:98-111`; called with `config.Reconciliation.ConsensusFdr` at `FirstPassFdrTask.cs:947`). For each file it finds passing precursors absent from that file's entries (`GapFillTargetIdentifier.cs:144-155`) and emits a `GapFillTarget` (`Osprey.FDR/Reconciliation/GapFillTarget.cs`) carrying `TargetEntryId` / `DecoyEntryId` (from the library lookup), `ExpectedRt` = `cal.Predict(consensusLibraryRt)`, `HalfWidth` = `MedianPeakWidth / 2`, and `ModifiedSequence` / `Charge` (`GapFillTargetIdentifier.cs:186-197`). Target and decoy are emitted together for symmetric competition. Results are sorted by `TargetEntryId` for deterministic, byte-parity JSON output (`GapFillTargetIdentifier.cs:207`).

### Isolation-window m/z filter (GPF-aware)

Forcing an integration at every missing precursor is wrong for gas-phase fractionation, where a file physically cannot observe an m/z its isolation windows don't select. A candidate survives only if its library precursor m/z falls inside at least one of the target file's `(Lo, Hi)` intervals, with a **strict** upper bound `precursorMz >= Lo && precursorMz < Hi` (`GapFillTargetIdentifier.cs:165-181`). When a file has no stored isolation scheme (null or empty list), the filter is disabled for that file — a graceful fallback to "fill every missing precursor" rather than dropping everything (`GapFillTargetIdentifier.cs:140-142, 165`).

### Scoring

Surviving gap-fill targets are re-scored by the same `PerFileRescoreTask` boundary-override path as reconciliation, using `(ExpectedRt - HalfWidth, ExpectedRt, ExpectedRt + HalfWidth)`, and feed into the second-pass FDR.

---

## The reconciliation.json envelope (Stage 5 → Stage 6)

Because the C# port runs as HPC stages, the Stage 5 plan is serialized per-file to `<stem>.reconciliation.json` (`Osprey.IO/ReconciliationFile.cs`) and read back by the Stage 6 worker. The envelope (schema `format_version = 3`) carries the non-Keep actions split into two homogeneous arrays (`use_cwt_peak_actions`, `forced_integration_actions`), `gap_fill_targets`, the `refined_rt_calibration`, the join-wide `file_stems` and `first_pass_base_ids`, and `library_hash` / `search_hash` for cache validation. All doubles route through `RoundtripDoubleConverter` and the file is normalized to LF with a trailing newline so it is byte-identical to the Rust `serde_json` output. This envelope has no counterpart in the Rust doc, which describes reconciliation as an in-process step; it is the port's HPC-split infrastructure (see 14-intermediate-files.md, 15-hpc-scoring-split.md).

---

## Flags and switches

| Flag / field | Default | Effect on this stage |
|--------------|---------|----------------------|
| `ReconciliationConfig.Enabled` (`Osprey.Core/ReconciliationConfig.cs:33`) | `true` | Master switch. `FirstPassFdrTask.cs:387` and `PerFileRescoreTask.cs:158, 963` gate reconciliation on it. **There is no CLI flag to toggle it** — no `--no-reconciliation`; only a config/YAML `Reconciliation.Enabled = false` disables it. `--task FirstPassFDR` requires it enabled (`Program.cs:395-396`). |
| `ReconciliationConfig.ConsensusFdr` (`ReconciliationConfig.cs:39`) | `0.01` | The consensus qualification threshold (Step 1 hard precursor gate + peptide/protein rescue), the refit experiment-q gate, the planner `experimentFdr` (`Stage6Planner.cs:285`), and the gap-fill passing threshold (`FirstPassFdrTask.cs:947`). |
| `ReconciliationConfig.TopNPeaks` (`ReconciliationConfig.cs:36`) | `5` | Number of CWT candidate peaks stored per precursor for later UseCwtPeak selection. |
| `--reconciliation-compaction-fdr <threshold>` (`OspreyCommandArgs.cs:116-117`) | `0.01` | Peptide-q gate for first-pass compaction, which sets the pool reconciliation operates on. Loosen (e.g. 0.05) to broaden the reconciliation pool. Not the consensus gate itself. |
| `--protein-fdr <threshold>` (`config.EffectiveProteinFdr`, passed at `Stage6Planner.cs:176`) | protein machinery always runs; threshold 0.01 | Enables the Step 1 protein-rescue branch for borderline peptide-level evidence. When 0 / disabled, the rescue branch is off (`ConsensusRts.cs:249`). |
| `--experiment-fdr <threshold>` | `0.01` | The second-pass (Stage 7) experiment FDR threshold; not consumed by planning itself. |
| `OspreyEnvironment.LoessClassicalRobust` (`CalibrationRefit.cs:97`) | `true` | Must match Stage 4 initial calibration; drives refit robustness iterations for cross-impl parity. |
| `OSPREY_DUMP_INV_PREDICT` (+ `_ONLY`) | off | Diagnostic: dumps one `InvPredictRecord` per consensus detection (apex_rt, library_rt, weight) for cross-impl bisection (`Stage6Planner.cs:168-184`). |
| `OSPREY_DUMP_CONSENSUS`, `OSPREY_DUMP_RECONCILIATION`, `OSPREY_DUMP_MULTICHARGE`, `OSPREY_DUMP_LOESS_FIT`, `OSPREY_DUMP_REFIT` (+ each `_ONLY`) | off | Stage 6 cross-impl bisection dumps for each planning phase (`Stage6Planner.cs`). |
| `OSPREY_TRACE_PEPTIDE` | off | Per-peptide trace through consensus, planning, and gap-fill (18-peptide-trace.md). |

---

## Divergences from the Rust documentation

- **[INTENTIONAL-CSHARP-DESIGN] HPC stage split with a JSON envelope** - Rust doc describes reconciliation as an in-process step inside `run_analysis` (consensus → refit → plan → re-score → second-pass FDR, all in memory). C# splits it into Stage 5 planning (`Stage6Planner`/`FirstPassFdrTask`), a serialized `<stem>.reconciliation.json` handoff (`Osprey.IO/ReconciliationFile.cs`, `format_version` 3), Stage 6 apply (`PerFileRescoreTask`), and Stage 7 second-pass FDR (`SecondPassFdrTask`). Behavior/outputs match; the envelope is byte-parity with Rust. Evidence: Osprey.IO/ReconciliationFile.cs:52-107; Osprey.Tasks/Stage6Planner.cs:48-100. Severity: info.

- **[STALE-RUST-DOC] `--no-reconciliation` CLI flag not present in C#** - Rust doc (Configuration section) says "CLI flag: `--no-reconciliation` disables it entirely." The C# CLI has no such argument; the only reconciliation-related CLI flag is `--reconciliation-compaction-fdr`. Reconciliation can only be disabled via the config object's `Reconciliation.Enabled = false`, not from the command line. Evidence: Osprey/OspreyCommandArgs.cs:116-117 (only reconciliation CLI arg); Osprey.Core/ReconciliationConfig.cs:33 (config-only switch). Severity: minor.

- **[STALE-RUST-DOC] Worked example still uses `coelution_sum` weighting** - Rust doc's "Example" section (and the Algorithm Overview arrow diagram) computes the weighted median with `weight = coelution_sum`, while the current algorithm — stated correctly elsewhere in the same Rust doc's Step 2 — uses `sigmoid(SVM score)`. C# uses `max(1e-6, sigmoid(score))`, matching current Rust code, not the stale example. Evidence: Osprey.FDR/Reconciliation/ConsensusRts.cs:178. Severity: info.

- **[STALE-RUST-DOC] Planner passing-precursor precondition not described** - Rust doc Step 4 says the planner runs "For each scored entry (target or decoy)" with no mention of a per-entry FDR gate. C# (and, per the code comment, current Rust `reconciliation.rs:560-576`) additionally requires the entry's `(base_id, charge)` to be in `passingBaseIds` — precursors where `min(run/exp × precursor/peptide q) <= experimentFdr` — before an action is assigned. Evidence: Osprey.FDR/Reconciliation/ReconciliationPlanner.cs:131-144, 209. Severity: minor.

- **[STALE-RUST-DOC] Ceiling is a sigma-clipped MAD, not a plain calibration MAD** - Rust doc describes the per-file safety ceiling only as "the calibration-MAD-based ceiling." C# computes it as a sigma-clipped median of the refined absolute residuals (clip at `MAD×1.4826×3`, fall back to unclipped median if fewer than 20 survive), then caps it by the original first-pass calibration MAD so each pass can only tighten. The code comment ties this to Rust docstring `reconciliation.rs:570-607`, so the implementations agree; only the Rust prose is simplified. Evidence: Osprey.FDR/Reconciliation/ReconciliationPlanner.cs:166-183, 299-322. Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] Decoy pairing by base_id, not DECOY_ prefix** - Rust doc says paired decoys are matched by "DECOY_ prefix, any matching modified_sequence." C# pairs by `EntryId & 0x7FFFFFFF` so library-supplied decoys (Carafe / FDRBench manifest) with no prefix are still recognized; the prefix-strip path would silently miss them. The code comment states this mirrors current Rust `compute_consensus_rts`, so this is the shared current behavior, more general than the doc's prose. Evidence: Osprey.FDR/Reconciliation/ConsensusRts.cs:93-118; ReconciliationPlanner.cs:120-144. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Second-pass FDR engine is native Percolator only** - Rust doc Step 6 says "the same Percolator/Mokapot/Simple FDR pipeline applies." The C# port has no Python Mokapot dependency; second-pass FDR runs the native managed Percolator SVM (or `simple`). The `FdrMethod` enum still lists Mokapot but the CLI only accepts `percolator | simple`. Evidence: SecondPassFdrTask (`--task SecondPassFDR`); see 07-fdr-control.md. Severity: info.

Everything else verified matches step for step: the hard run-precursor-q consensus gate (`ConsensusRts.cs:246`), the protein-FDR rescue branch (`ConsensusRts.cs:248-249`), sigmoid(score) weighted median with a 1e-6 floor and a `CoelutionSum > 0` filter (`ConsensusRts.cs:172-197`), within-peptide MAD requiring ≥3 detections (`ConsensusRts.cs:203-214`), the global-within-peptide-MAD RT tolerance with a 0.1-min floor and 3σ×1.4826 factor (`ReconciliationPlanner.cs:96-190`), apex-proximity (not boundary-containment) Keep/UseCwtPeak/ForcedIntegration selection picking the closest CWT candidate (`ReconciliationPlanner.cs:248-291`), calibration refit at 20-point minimum with outlier removal disabled (`CalibrationRefit.cs:39, 92-98`), the GPF isolation-window m/z filter with a strict upper bound and graceful fallback (`GapFillTargetIdentifier.cs:165-181`), single-file skip (`Stage6Planner.cs:171-178`), reconciliation-wins-on-conflict merge (`PerFileRescoreTask.cs:904-908`), and deterministic sorting of consensus and gap-fill output (`ConsensusRts.cs:231-237`, `GapFillTargetIdentifier.cs:207`).
