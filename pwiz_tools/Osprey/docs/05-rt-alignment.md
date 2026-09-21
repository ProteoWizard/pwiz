# 05. RT Alignment (C#)

> Pipeline stage: Stage 3 calibration + reconciliation consensus RT. C# port of Rust docs/14-rt-alignment.md. Corresponds to Rust osprey RT alignment & consensus library RT.

This document describes how the C# Osprey port aligns retention times across runs and computes a consensus library RT for each peptide. The consensus library RT is the run-independent anchor that drives cross-run reconciliation (see `10-cross-run-reconciliation.md`) and forced-integration boundary imputation (see `11-boundary-overrides.md`).

## Problem

Retention times drift across runs (LC system, gradient, column conditioning, matrix effects). A peptide might apex at RT=25.3 in file1 and RT=25.8 in file2. To reconcile peak boundaries across runs the port needs a **run-independent representation** of where each peptide elutes: the consensus library RT.

## RT spaces

The port works in three RT spaces, exactly as the Rust doc describes:

| Space | Description | C# representation |
|-------|-------------|-------------------|
| **Library RT** | RT from the spectral library (predicted/measured on a reference system) | `LibraryEntry.RetentionTime` |
| **Measured RT** | RT observed in a specific run | `FdrEntry.ApexRt` |
| **Consensus library RT** | Weighted median library RT across all high-confidence detections | `PeptideConsensusRT.ConsensusLibraryRt` |

The key invariant is unchanged: **library RT space is run-independent**. Measured apex RTs are mapped back to library RT space (`RTCalibration.InversePredict`), aggregated with a robust weighted median, and then mapped forward again per-run (`RTCalibration.Predict`) to predict where a peptide should elute in any file.

## LOESS calibration primitive

Both the initial (Stage 3) and refined (Stage 6) calibrations are fitted by the same LOESS engine.

- `LoessRegression.Fit` (`Osprey.Chromatography/LoessRegression.cs:239`) fits local weighted linear regressions: tricube distance weights (`Tricube`, `LoessRegression.cs:383`), bisquare robustness iterations (`Bisquare`, `LoessRegression.cs:393`), 2 robustness iterations by default.
- The inner fit (`LoessFitInternal`, `LoessRegression.cs:401`) is parallelized with `Parallel.For` over output indices — the C# port's replacement for the Rust auto-vectorized serial loop (see `17-vectorization.md`). Each fitted point solves a weighted 2x2 normal-equation system.
- `LoessModel` (`LoessRegression.cs:35`) stores the sorted x-values and fitted y-values and serves prediction by linear interpolation (`Predict`, `LoessRegression.cs:59`) with linear extrapolation outside the fitted range.
- `RTCalibrator.Fit` (`Osprey.Chromatography/RTCalibration.cs:100`) wraps the LOESS fit, adds optional outlier removal, computes residuals/`ResidualSD`, and produces an `RTCalibration`.

Cross-impl determinism details that are load-bearing for bit-identity: the outer sort in `RTCalibrator.Fit` is a stable `OrderBy(x).ThenBy(y)` (`RTCalibration.cs:126`) rather than an unstable `Array.Sort`, so duplicate library RTs (multi-charge peptides share a library RT) do not scramble the paired y-values; the bisquare weight is computed as `t*t` not `Math.Pow(t,2)` (`LoessRegression.cs:315`) to match Rust `powi(2)` at the last ULP; and `PercentileValue` rounds half-away-from-zero (`LoessRegression.cs:377`) to match Rust `f64::round`.

### Robustness-iteration mode flag

`RTCalibratorConfig.ClassicalRobustIterations` (`RTCalibration.cs:63`) selects between two robustness behaviors: when `true` (the shipped default), absolute residuals are recomputed from the current fit each iteration (classical Cleveland 1979); when `false`, residuals from the initial fit are reused. Both the initial calibration (`Calibrator.cs:508`) and the refit (`CalibrationRefit.cs:97`) read `OspreyEnvironment.LoessClassicalRobust` for this flag so the two calibrations use the same mode. Default is `true`, matching Rust `calibration_ml.rs` v26.3.1+; override with `OSPREY_LOESS_CLASSICAL_ROBUST=0` on both tools together.

## Step 1 — Initial calibration (Stage 3)

Initial calibration happens during per-file calibration discovery, driven by `Calibrator.RunCalibration` (`Osprey.Tasks/Calibrator.cs:117`), a port of Rust `run_calibration_discovery_windowed`. This produces the forward LOESS mapping `library_rt -> measured_rt`. The full discovery scoring (sampling, LDA target-decoy competition, S/N filtering, mass calibration) is documented in `04-calibration.md`; here we focus on the LOESS RT curve it emits.

Discovery is a **two-pass** refinement (`Calibrator.cs:254`-`366`):

1. **Pass 1** scores stratified-sampled entries with a linear pre-fit RT mapping and a wide initial tolerance, then fits a LOESS `RTCalibration` from the LDA + S/N surviving targets (`Calibrator.cs:254`).
2. **Pass 2** re-scores using pass 1's LOESS curve to predict expected RT with a much tighter tolerance `3 * MAD * 1.4826` clamped to `[MinRtTolerance, MaxRtTolerance]` (`Calibrator.cs:290`-`293`), refits, and is accepted only if R^2 does not degrade by more than 1% (`Calibrator.cs:337`). Otherwise pass 1's calibration is kept.

The LOESS config used at this stage (`Calibrator.cs:501`) sets `Bandwidth = config.RtCalibration.LoessBandwidth` (default 0.3), `Degree = 1`, `RobustnessIterations = 2`, and importantly **`OutlierRetention = 1.0`** — outlier removal is disabled because the LDA 1% FDR gate and the `S/N >= 5.0` filter (`MIN_SNR_FOR_RT_CAL`, `Calibrator.cs:53`) have already cleaned the point set.

Minimum-point gating: pass 1 requires `config.RtCalibration.MinCalibrationPoints` LOESS input points (`Calibrator.cs:259`, default 200 — `RTCalibrationConfig.cs:41`); pass 2 requires `ABSOLUTE_MIN_CALIBRATION_POINTS = 50` (`Calibrator.cs:60`). The inner `RTCalibrator` requires only `Math.Min(20, libRts.Length)` (`Calibrator.cs:505`).

## Step 2 — Inverse calibration

The LOESS calibration also supports inverse prediction: mapping a measured RT back to library RT space. `RTCalibration.InversePredict` (`RTCalibration.cs:238`) delegates to `LoessModel.InversePredict` (`LoessRegression.cs:90`), which checks that the fitted curve is monotonically non-decreasing and, if so, binary-searches the fitted y-values and linearly interpolates the corresponding x. If the fit is non-monotonic it falls back to the nearest fitted value (`LoessRegression.cs:129`). Inverse prediction is what converts each run's measured apex RT into a comparable library RT for consensus aggregation.

## Step 3 — Consensus library RT (Stage 6)

Consensus RTs are computed by `ConsensusRts.Compute` (`Osprey.FDR/Reconciliation/ConsensusRts.cs:70`), invoked from `Stage6Planner.ComputeConsensusRts` (`Osprey.Tasks/Stage6Planner.cs:150`). Cross-file consensus only runs when `perFileEntries.Count > 1` (`Stage6Planner.cs:172`); single-file runs return an empty consensus and skip refit + reconciliation entirely.

The algorithm:

1. **Select qualifying target peptides** (`ConsensusRts.cs:93`-`105`). A target detection qualifies via `Qualifies` (`ConsensusRts.cs:242`):
   - `RunPrecursorQvalue <= consensusFdr` is a **hard precondition**, AND
   - `RunPeptideQvalue <= consensusFdr` OR (protein rescue) `proteinFdrThreshold > 0 && RunProteinQvalue <= proteinFdrThreshold`.
   Protein FDR can only rescue borderline *peptide-level* evidence; it cannot rescue a detection with poor precursor-level evidence, because the consensus RT is driven by each detection's own apex.
2. **Collect paired decoy peptides** by `base_id` linkage: `EntryId & 0x7FFFFFFF` (`ConsensusRts.cs:102`,`115`). A decoy sequence is included if its base_id matches a qualifying target's base_id. This works for both Osprey-generated reverse decoys and library-supplied (Carafe / FDRBench-manifest) decoys whose modified sequence carries no `DECOY_` prefix.
3. **Collect detections** for each consensus peptide (`ConsensusRts.cs:124`-`152`) as `(fileName, apexRt, score, peakWidth = EndRt - StartRt, coelutionSum)`. Targets contribute only qualifying detections; decoys contribute all detections of paired decoy sequences.
4. **Map to library RT space and weight** (`ConsensusRts.cs:167`-`191`): `libraryRt = cal.InversePredict(apexRt)` per file. A detection is dropped if the library RT is non-finite or `coelutionSum <= 0`. The weight is `sigmoid(SVM score)` floored at `1e-6`: `Math.Max(1e-6, 1/(1+exp(-score)))` (`ConsensusRts.cs:178`).
5. **Weighted medians** (`ConsensusRts.cs:196`-`197`): `ConsensusLibraryRt` is the `WeightedMedian` of `(libraryRt, weight)` pairs; `MedianPeakWidth` is the `WeightedMedian` of `(peakWidth, weight)` pairs. `WeightedMedian` (`ConsensusRts.cs:264`) sorts by value ascending and returns the first value whose cumulative weight crosses half the total.
6. **Within-peptide RT MAD** (`ConsensusRts.cs:203`-`214`): when `>= 3` detections contribute, `ApexLibraryRtMad` is set to the median absolute deviation of the per-run library RTs from the consensus. This is the peptide's own reproducibility in library space and feeds the reconciliation tolerance (Step 5).
7. **Deterministic output sort** (`ConsensusRts.cs:231`): targets before decoys, then ordinal by modified sequence.

### Why sigmoid(SVM score) weighting

A wrong-peak detection with a negative SVM score gets weight ~0.02 and cannot poison the median; strong detections with positive scores dominate. This is more discriminating than raw co-elution magnitude because it uses the trained model's judgment of detection quality. The `coelutionSum > 0` sanity filter is retained as a floor.

### Output record

`PeptideConsensusRT` (`Osprey.FDR/Reconciliation/PeptideConsensusRT.cs:33`):

```csharp
public class PeptideConsensusRT {
    string ModifiedSequence;    // peptide-level grouping
    bool   IsDecoy;             // computed independently for targets and decoys
    double ConsensusLibraryRt;  // sigmoid-weighted median in library RT space
    double MedianPeakWidth;     // sigmoid-weighted median peak width (min, measured space)
    int    NRunsDetected;       // number of runs contributing a detection
    double? ApexLibraryRtMad;   // within-peptide RT MAD (library space); null if < 3 detections
}
```

### Target-decoy separation

Targets and decoys receive independent consensus RTs — decoy consensus is computed from decoy detections only (`ConsensusRts.cs:130`-`133`) so no target information leaks into the decoy null, preserving fair second-pass target-decoy competition.

### Cross-impl bisection trace

When `OSPREY_DUMP_INV_PREDICT` is set, `ConsensusRts.Compute` populates an `InvPredictRecord` list (`InvPredictRecord.cs:35`) with the `(fileName, modifiedSequence, isDecoy, apexRt, libraryRt, weight)` of every contributing detection (`ConsensusRts.cs:182`). The `Stage6Planner` drives the dump via the diagnostics sink (`Stage6Planner.cs:167`,`180`). This localizes any `ConsensusLibraryRt` divergence to either the loaded apex RT (Parquet decode) or the LOESS inverse-interpolation step.

## Step 4 — Refined calibration (Stage 6)

During reconciliation the per-file calibration is **refit** from FDR-controlled consensus peptides by `CalibrationRefit.Refit` (`Osprey.FDR/Reconciliation/CalibrationRefit.cs:48`), called per file from `Stage6Planner.RefitCalibrations` (`Stage6Planner.cs:214`).

- **Points**: `(consensus_library_rt -> measured_apex_rt)` for target peptides in this run whose `EffectiveExperimentQvalue(FdrLevel.Both) <= consensusFdr` and that have a consensus RT (`CalibrationRefit.cs:69`-`79`). Note this refit gate is **experiment-level** (`EffectiveExperimentQvalue`), distinct from the run-level precursor hard gate used to *select* consensus peptides in Step 3.
- **LOESS config** (`CalibrationRefit.cs:92`): `Bandwidth = 0.3`, `OutlierRetention = 1.0` (no outlier removal — all points are FDR-controlled; LOESS robustness iterations still downweight stragglers), `MinPoints = MIN_CONSENSUS_POINTS = 20` (`CalibrationRefit.cs:39`), `ClassicalRobustIterations = OspreyEnvironment.LoessClassicalRobust`.
- **Fallback**: fewer than 20 usable points, or a fit exception, returns `null` (`CalibrationRefit.cs:81`,`104`) and the original first-pass calibration is used downstream.

This produces a tighter calibration because all points are high-confidence FDR-controlled detections and the consensus library RTs are more consistent than first-pass library RTs.

## Step 5 — Predicting expected measured RT and the reconciliation tolerance

Reconciliation planning (`ReconciliationPlanner.Plan`, `Osprey.FDR/Reconciliation/ReconciliationPlanner.cs:65`) uses the consensus RTs and refined calibrations to decide, per (file, entry), whether to keep the current peak, switch to a stored CWT candidate, or force an integration window.

### Expected measured RT

For each entry in the consensus set that passed experiment-FDR (or its paired decoy, keyed by `(base_id, charge)` — `ReconciliationPlanner.cs:131`-`143`,`209`), the expected RT is `cal.Predict(consensusEntry.ConsensusLibraryRt)` (`ReconciliationPlanner.cs:212`), where `cal` is the refined calibration if present, else the original first-pass calibration (`ReconciliationPlanner.cs:155`-`157`).

### RT tolerance — within-peptide MAD, not cross-peptide calibration MAD

This is the most substantive difference from the Rust `14-rt-alignment.md` text (which describes a single global `max(0.1, 3.0 * MAD * 1.4826)` derived from the refined calibration's cross-peptide residuals). The C# port — matching the current Rust code — instead derives the tolerance primarily from **within-peptide** reproducibility:

1. **Global within-peptide MAD** (`ReconciliationPlanner.cs:96`-`118`): the median of all target peptides' `ApexLibraryRtMad` values (per-peptide, library space, `>= 3` detections). If no peptide qualifies (e.g. a 2-replicate experiment), it falls back to `FALLBACK_GLOBAL_MAD_LIB = 0.05` min (`ReconciliationPlanner.cs:44`).
2. **Per-file cross-peptide ceiling** (`ReconciliationPlanner.cs:166`-`183`): a *sigma-clipped* MAD of the refined calibration's absolute residuals (`SigmaClippedMad`, `ReconciliationPlanner.cs:299`) converted to `3 * MAD * 1.4826`, floored at `MIN_RT_TOLERANCE = 0.1` min, and further **capped by** the original first-pass calibration MAD so each pass can only tighten. Sigma-clipping (`SIGMA_CLIP_MIN_SURVIVORS = 20`) prevents wrong-peak residuals from inflating the ceiling.
3. **Final per-peptide tolerance** (`ReconciliationPlanner.cs:188`): `min( max(globalWithinPeptideMadLib * 1.4826 * 3.0, 0.1), fileCalToleranceCeiling )`.

Rationale (from the Rust docstring the C# comments cite): after cross-run alignment, within-peptide scatter is roughly peptide-independent (instrument/LC reproducibility) and the cross-peptide median is a far more stable estimator than any single peptide's 3-5-replicate MAD. Using a global (not per-query-position) tolerance also eliminates the self-fulfilling-prophecy feedback loop the Rust doc warns about, where a wrong-apex detection inflates the local tolerance at its own RT and then passes the proximity check. On Stellar this typically drops the tolerance from ~0.3 min (cross-peptide) to ~0.1 min (within-peptide).

### Reconcile action

`ReconciliationPlanner.DetermineAction` (`ReconciliationPlanner.cs:248`), a port of Rust `determine_reconcile_action`, uses **apex proximity** (not boundary containment):

- If `|apexRt - expectedMeasuredRt| <= rtTolerance` -> `ReconcileAction.Keep` (implicit; not stored).
- Else pick the stored CWT candidate whose apex is closest to `expectedMeasuredRt` and within tolerance -> `ReconcileAction.UseCwtPeak` (`ReconciliationPlanner.cs:280`).
- Else -> `ReconcileAction.ForcedIntegration(expectedRt, halfWidth)` where `halfWidth = consensusEntry.MedianPeakWidth / 2.0` (`ReconciliationPlanner.cs:225`,`288`).

## Step 6 — Peak width for imputation

When no CWT candidate exists at the expected RT, `MedianPeakWidth` becomes the forced-integration window: `forced_start = expected_rt - halfWidth`, `forced_end = expected_rt + halfWidth` (`ReconciliationPlanner.cs:288`, consumed downstream in `11-boundary-overrides.md`). The width is the sigmoid-weighted median across confident detections, so the imputed window matches the peptide's typical chromatographic width.

## Stage ordering

`Stage6Planner.Plan` (`Stage6Planner.cs:80`) runs the four planning phases in Rust pipeline order:

1. Multi-charge consensus per file (independent; see `09-multi-charge-consensus.md`).
2. Cross-run consensus RTs (`ComputeConsensusRts`).
3. Per-file calibration refit (`RefitCalibrations`), which depends on (2).
4. Reconciliation planning (`PlanReconciliation`), which depends on (2) and (3).

## Flags and switches

| Flag / field / env var | Default | Effect on this stage |
|------------------------|---------|----------------------|
| `ReconciliationConfig.ConsensusFdr` (config field) | `0.01` (`ReconciliationConfig.cs:39`) | FDR threshold for selecting consensus peptides (Step 3) and for the refit experiment-FDR gate (Step 4). Not exposed as a dedicated CLI flag; uses the default. |
| `ReconciliationConfig.Enabled` | `true` (`ReconciliationConfig.cs:33`) | Enables inter-replicate reconciliation for multi-file runs. |
| `ReconciliationConfig.TopNPeaks` | `5` (`ReconciliationConfig.cs:36`) | CWT candidate peaks stored per precursor for reconciliation planning. |
| `--protein-fdr <t>` -> `OspreyConfig.EffectiveProteinFdr` | `0.01` (default threshold; machinery always runs) | When passed as `proteinFdrThreshold` to `ConsensusRts.Compute` (`Stage6Planner.cs:176`), enables protein-FDR rescue of borderline peptide-level detections in consensus selection. |
| `--experiment-fdr <t>` | `0.01` | Sets the experiment-level FDR threshold; passing precursors (any q-level) are the ones reconciliation includes per file. |
| `RTCalibrationConfig.LoessBandwidth` | `0.3` (`RTCalibrationConfig.cs:38`) | LOESS bandwidth for both initial and refined RT calibration. |
| `RTCalibrationConfig.MinCalibrationPoints` | `200` (`RTCalibrationConfig.cs:41`) | Minimum LOESS input points for initial-calibration pass 1. |
| `RTCalibrationConfig.MinRtTolerance` / `MaxRtTolerance` | `0.5` / `3.0` min (`RTCalibrationConfig.cs:50`,`59`) | Clamp on the initial-calibration search tolerance (`3*MAD*1.4826`). Distinct from the reconciliation `MIN_RT_TOLERANCE = 0.1` floor. |
| `CalibrationRefit.MIN_CONSENSUS_POINTS` (const) | `20` (`CalibrationRefit.cs:39`) | Minimum consensus points to attempt a per-file refit. |
| `ReconciliationPlanner.FALLBACK_GLOBAL_MAD_LIB` (const) | `0.05` min (`ReconciliationPlanner.cs:44`) | Within-peptide MAD fallback when no peptide has `>= 3` detections. |
| `ReconciliationPlanner.MIN_RT_TOLERANCE` (const) | `0.1` min (`ReconciliationPlanner.cs:40`) | Floor on the reconciliation RT tolerance. |
| `ReconciliationPlanner.SIGMA_CLIP_MIN_SURVIVORS` (const) | `20` (`ReconciliationPlanner.cs:54`) | Minimum survivors before the sigma-clipped ceiling falls back to the unclipped median. |
| `OSPREY_LOESS_CLASSICAL_ROBUST` (env) | `1`/true (`OspreyEnvironment.cs:72`) | Robustness-iteration mode for all LOESS fits. Set to `0` on both tools together to validate legacy behavior. |
| `OSPREY_DUMP_INV_PREDICT` (`+ _ONLY`) (env) | off | Dumps the per-detection `(apex_rt, library_rt, weight)` inverse-prediction trace (`Stage6Planner.cs:167`). `_ONLY` exits after the dump. |
| `OSPREY_DUMP_CONSENSUS` (`+ _ONLY`) | off | Dumps the consensus RT table (`Stage6Planner.cs:202`). |
| `OSPREY_DUMP_REFIT` / `OSPREY_DUMP_LOESS_FIT` (`+ _ONLY`) | off | Dumps refined-calibration state (`Stage6Planner.cs:231`,`238`). |
| `OSPREY_DUMP_RECONCILIATION` (`+ _ONLY`) | off | Dumps the per-(file,entry) reconciliation action plan (`Stage6Planner.cs:306`). |

## Divergences from the Rust documentation

- **[STALE-RUST-DOC] Consensus weight is sigmoid(SVM score), not coelution_sum** - Rust doc says the weighted median uses `coelution_sum` as the weight for both consensus library RT and median peak width; C# weights by `sigmoid(SVM score)` floored at `1e-6`, keeping `coelution_sum > 0` only as a sanity filter. This matches the current Rust code (the Rust CLAUDE.md records "Consensus weighting: sigmoid(SVM score), not coelution_sum"), so parity is preserved and the doc is out of date. Evidence: `Osprey.FDR/Reconciliation/ConsensusRts.cs:178`. Severity: minor.

- **[STALE-RUST-DOC] Median peak width is a weighted median, not a simple median** - Rust doc computes `median_peak_width` via `simple_median([...])`; C# uses the same sigmoid-weighted `WeightedMedian` as the RT (a consequence of the weighting change above). Evidence: `Osprey.FDR/Reconciliation/ConsensusRts.cs:197`. Severity: minor.

- **[STALE-RUST-DOC] Consensus selection gate is a run-level precursor hard precondition plus peptide/protein rescue, not "experiment-level FDR"** - Rust doc says consensus peptides are those "passing experiment-level FDR at consensus_fdr". C# `Qualifies` requires `RunPrecursorQvalue <= consensusFdr` as a hard gate, then `RunPeptideQvalue <= consensusFdr` OR protein rescue — all run-level. This matches the current Rust code (Rust CLAUDE.md: "Consensus qualification gate: precursor q-value is a hard precondition"). Evidence: `Osprey.FDR/Reconciliation/ConsensusRts.cs:242`-`250`. Severity: minor.

- **[STALE-RUST-DOC] Reconciliation RT tolerance uses within-peptide MAD (with a sigma-clipped cross-peptide ceiling), not a single cross-peptide calibration MAD** - Rust doc gives `rt_tolerance = max(0.1, 3.0 * MAD * 1.4826)` from the refined calibration's cross-peptide residuals. C# derives the tolerance from the global median of per-peptide within-library-space apex MADs, floors it at 0.1, and caps it by a sigma-clipped per-file calibration MAD that is itself capped by the original first-pass MAD. This matches the current Rust code (Rust CLAUDE.md: "Reconciliation RT tolerance: within-peptide MAD, not cross-peptide calibration MAD"). Evidence: `Osprey.FDR/Reconciliation/ReconciliationPlanner.cs:96`-`190`,`299`. Severity: major.

- **[STALE-RUST-DOC] Decoy pairing is by base_id linkage, not DECOY_ prefix stripping** - Rust doc describes decoy consensus computed "from decoy detections only" and the older Rust implementation stripped a `DECOY_` prefix. C# links paired decoys via `EntryId & 0x7FFFFFFF`, which also recognizes library-supplied decoys (Carafe / FDRBench manifest) whose modified sequence carries no prefix. The target/decoy-independence property the doc requires is preserved. Evidence: `Osprey.FDR/Reconciliation/ConsensusRts.cs:102`,`115`. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Parallelized LOESS inner fit instead of BLAS/auto-vectorization** - The Rust code relies on an auto-vectorized serial inner loop; the C# port parallelizes the per-point fit with `Parallel.For` and uses direct `t*t` / away-from-zero rounding to preserve bit-identity. Numerical results match; only the execution strategy differs. Evidence: `Osprey.Chromatography/LoessRegression.cs:420`. See `17-vectorization.md`. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Initial-calibration minimum-point gate differs from the doc's "50"** - Rust `14-rt-alignment.md` lists "Minimum points: 50" for initial calibration. In the C# discovery path pass 1 requires `MinCalibrationPoints` (default 200) and pass 2 requires 50 (`ABSOLUTE_MIN_CALIBRATION_POINTS`); the inner `RTCalibrator` requires only 20. The "50" figure corresponds to `RTStratifiedSampler.MinCalibrationPoints` and the pass-2 floor, not the pass-1 gate. This is a Stage 3 detail; see `04-calibration.md`. Evidence: `Osprey.Tasks/Calibrator.cs:60`,`259`; `Osprey.Core/RTCalibrationConfig.cs:41`. Severity: minor.

- **[FLAG-GATED] `OSPREY_TRACE_PEPTIDE` per-peptide trace not present in this stage of the C# port** - Rust documents an `OSPREY_TRACE_PEPTIDE` env var that emits `[trace]` lines inside `compute_consensus_rts` and `plan_reconciliation`. The C# reconciliation code exposes structured *dump* diagnostics instead (`OSPREY_DUMP_INV_PREDICT`, `OSPREY_DUMP_CONSENSUS`, `OSPREY_DUMP_RECONCILIATION`); no `OSPREY_TRACE_PEPTIDE` handling was found under `Osprey.FDR/Reconciliation` or `Osprey.Tasks`. A human should confirm whether the peptide trace exists elsewhere in the C# port before relying on it. Evidence: no match for `TRACE_PEPTIDE` in the C# tree. See `18-peptide-trace.md`. Severity: info.

Verified against the Rust doc step for step: the three RT spaces, forward/inverse LOESS prediction, two-pass initial calibration, per-file consensus refit at bandwidth 0.3 / retention 1.0 / min-20, weighted-median consensus with independent target/decoy sets, `predict(consensus_library_rt)` for expected RT, apex-proximity reconcile action, and `median_peak_width/2` forced-integration window all match. The behavioral deltas above are all cases where the C# port tracks the *current* Rust code and the Rust `14-rt-alignment.md` prose is stale.
