# 04. Calibration (C#)

> Pipeline stage: Stage 3 (Calibration / recalibration). C# port of Rust docs/04-calibration.md. Corresponds to Rust osprey joint RT + MS1/MS2 m/z calibration.

Osprey performs joint calibration of retention time (RT), MS1 (precursor) m/z, and
MS2 (fragment) m/z from a single set of high-confidence peptide matches discovered
by a co-elution search. The calibration tightens the RT and mass windows used by
the main search (Stage 4) and is persisted to a `.calibration.json` sidecar for
reuse. In the C# port this stage is owned by `Osprey.Tasks/Calibrator.cs`, invoked
once per input file from `PerFileScoringTask.RunCalibrationForFile`
(`Osprey.Tasks/PerFileScoringTask.cs:1671`).

The three calibrations share a single discovery phase: a windowed co-elution search
scored with a 4-feature LDA, followed by paired target-decoy competition at 1% FDR
and an S/N >= 5.0 quality gate. The surviving targets provide
`(library_rt, measured_rt)` pairs for the LOESS RT fit and MS1/MS2 mass-error
samples for the m/z calibrations.

Entry method:
`Calibrator.RunCalibration(library, spectra, ms1Spectra, context, out ms1Cal, out ms2Cal, out numSampledPrecursors, out initialRtTolerance)`
(`Osprey.Tasks/Calibrator.cs:117`). It is a direct port of Rust's
`run_calibration_discovery_windowed` in `osprey/src/pipeline.rs`.

---

## Step 1 — RT range analysis and linear pre-fit mapping

`Calibrator.cs:142-190`. Before any scoring, the calibrator computes the library
target RT range and the mzML spectrum RT range, then decides whether the two RT
scales are "similar":

```
rangesSimilar = libRtRange > 0 && mzmlRtRange > 0 &&
    max(libRtRange/mzmlRtRange, mzmlRtRange/libRtRange) < 2.0 &&
    |libMinRt - mzmlMinRt| < libRtRange * 0.5
```

- **Scales similar**: identity mapping (`rtSlope = 1`, `rtIntercept = 0`), initial
  RT tolerance = `mzmlRtRange * 0.2` (20% of the gradient).
- **Scales differ**: a linear mapping `measured ≈ slope·lib + intercept` is fit from
  the two bounding boxes (`Calibrator.cs:178-184`), and the initial tolerance widens
  to `mzmlRtRange * 0.5` (50%).

The chosen wide tolerance is returned via `initialRtTolerance` (the "before" number
in the console calibration summary).

## Step 2 — Stratified library sampling

`Calibrator.SampleLibraryForCalibration` (`Calibrator.cs:756`), a direct port of Rust
`sample_library_for_calibration`. This is the first randomized/selected step in the
whole pipeline, so it must match Rust bit-for-bit for the two tools to calibrate on
the same peptides.

- If the library has `<= sampleSize` targets, all entries are used (sampling
  skipped). Otherwise a **2D stratified grid** over `(RT × precursor_mz)` is built.
- `binsPerAxis = max(5, ceil(sqrt(sampleSize) / 2))` (`Calibrator.cs:781`). For the
  default 100,000 sample this is 159, a 159×159 grid.
- Each target is assigned to its grid cell (`Calibrator.cs:808-818`); occupied cells
  are counted and `perCell = sampleSize / nOccupied` (`Calibrator.cs:827`).
- **First pass**: from each occupied cell, take `perCell` targets using a
  deterministic stride `(cellOffset + j*stride) % cell.Count`; each sampled target
  drags in its paired decoy via `decoyMap[target.Id]`
  (`Calibrator.cs:842-874`).
- **Second (top-up) pass**: if fewer than `sampleSize` unique targets were collected,
  loop the occupied cells again adding unsampled entries until the quota is met
  (`Calibrator.cs:877-910`).

The sampler is seeded with `43UL` (`Calibrator.cs:197`) to match Rust's
`42 + attempt` on the first (and, in the C# port, only) calibration attempt.
Paired target/decoy linkage is by `base_id = entry_id & 0x7FFFFFFF`; decoy IDs are
`target_id | 0x80000000` (`Calibrator.cs:774-777`).

## Step 3 — Two-pass discovery loop

`RunCalibration` runs the scoring/FDR/LOESS pass up to twice via
`RunCalibrationScoringPass` (`Calibrator.cs:376`):

- **Pass 1** (`Calibrator.cs:254`) uses the linear pre-fit mapping and the wide
  initial tolerance; its LOESS floor is `config.RtCalibration.MinCalibrationPoints`
  (default 200).
- **Pass 2** (`Calibrator.cs:312`) re-scores the *same* sampled entries but predicts
  the expected RT from pass 1's LOESS fit and uses a much tighter tolerance; its
  floor drops to `ABSOLUTE_MIN_CALIBRATION_POINTS = 50` (`Calibrator.cs:60`).

Each pass performs the following, in order.

### 3a. Co-elution scoring per entry

`ScoreCalibrationEntry` (`Calibrator.cs:926`), run in parallel over sampled entries
(`ScoreCalibrationMatches`, `Calibrator.cs:609`, `MaxDegreeOfParallelism =
config.NThreads`):

1. Find the isolation window containing the entry's precursor m/z. The window index
   is `round(precursorMz * 10)`; neighbour keys and a linear-scan fallback handle
   rounding collisions (`Calibrator.cs:948-975`).
2. Compute `expectedRt`: pass 1 uses `lib_rt·slope + intercept`; pass 2 uses
   `calibrationModel.Predict(lib_rt)` (`Calibrator.cs:983-985`).
3. Filter window spectra to those within `|rt - expectedRt| <= tolerance` that also
   pass the top-N fragment prefilter `FragmentMath.HasTopNFragmentMatch`
   (`Calibrator.cs:1029-1038`). Require at least `MIN_COELUTION_SPECTRA = 3`
   candidates (`Calibrator.cs:53-55, 1040`).
4. Extract XICs for the top `CAL_TOP_N_FRAGMENTS = 6` most intense library fragments
   (`TopFragmentExtractor.ExtractTopNFragmentXics`, `Calibrator.cs:1057`).
5. Detect candidate peaks with **CWT consensus peak detection**
   (`CwtPeakDetector.DetectConsensusPeaks`, `Calibrator.cs:1072`). If CWT returns no
   consensus peaks, fall back to `PeakDetector.DetectAllXicPeaks` on the single
   highest-intensity reference XIC (`Calibrator.cs:1079-1102`). See
   `06-peak-detection.md`.
6. Score each candidate peak by the **sum of pairwise Pearson correlations** between
   fragment XICs over the peak's index range and keep the best
   (`ScorePeaksByCorrelation`, `Calibrator.cs:1225`; ties go to the last peak to
   match Rust's `max_by`). Reject if `bestCorrSum < MIN_COELUTION_CORR_SCORE = 0.5`
   (`Calibrator.cs:54, 1111`).
7. Locate the apex as the argmax of the reference XIC within the peak bounds
   (`Calibrator.cs:1144-1158`); `measuredRt = apexRt`.
8. Compute S/N on the reference fragment's raw intensities
   (`PeakDetector.ComputeSnr`, `Calibrator.cs:1163`).
9. At the apex spectrum, compute the four LDA features (below) plus MS2 fragment mass
   errors (`CollectMs2FragmentErrors`, `Calibrator.cs:1195`) and the MS1 precursor
   mass error (`ComputeMs1MassError`, `Calibrator.cs:1198`).

The result is a `CalibrationMatch` (`Osprey.Scoring/CalibrationScorer.cs:34`)
carrying the four raw features, `Ms2MassErrors`, and optional `Ms1Error`.

### 3b. The four LDA features

`CalibrationScorer.ExtractFeatureMatrix` (`CalibrationScorer.cs:581`) normalizes
exactly four features to roughly `[0,1]`:

| Feature | C# field | Normalization |
|---------|----------|---------------|
| Co-elution correlation sum | `CorrelationScore` | `/ 6.0`, clamped `[0,1]` |
| Spectral similarity at apex | `LibcosineApex` | already `[0,1]` |
| Top-6 fragments matched at apex | `Top6MatchedApex` | `/ 6.0`, clamped |
| Comet-style XCorr | `XcorrScore` | `/ 3.0`, clamped |

Hyperscore, S/N, and isotope cosine are deliberately excluded. The XCorr uses a
dedicated **unit-resolution** scorer regardless of instrument mode
(`Calibrator.s_calXcorrScorer = new SpectralScorer(BinConfig.UnitResolution())`,
`Calibrator.cs:72`; preprocessed per window by `PreprocessWindowsForXcorr`,
`Calibrator.cs:563`).

### 3c. LDA training + target-decoy competition

`CalibrationScorer.TrainAndScoreCalibration` (`CalibrationScorer.cs:84`), a port of
Rust `train_and_score_calibration`. Matches are first sorted deterministically by
`(base_id, entry_id)` (`Calibrator.cs:431-439`). Training procedure
(`TrainLdaWithNonNegativeCv`, `CalibrationScorer.cs:227`):

1. Pick the best single feature at 1% FDR as the baseline (relaxes to 5% FDR if no
   target passes, `CalibrationScorer.cs:254-277`).
2. For up to `MAX_ITERATIONS = 3`:
   - 3-fold CV **grouped by peptide sequence** so all charge states of a peptide (and
     targets vs. decoys, handled independently) stay together
     (`CreateStratifiedFoldsByPeptide`, `CalibrationScorer.cs:409`).
   - Per fold: select high-confidence train targets (`SelectPositiveTrainingSet`,
     progressively relaxing 1% → 5/10/25/50% until `MIN_POSITIVE_EXAMPLES = 50`),
     combine with all train decoys, fit `LinearDiscriminant.Fit`.
   - Average fold weights, **clip negatives to zero**, renormalize to unit length
     (`AverageWeights`, `CalibrationScorer.cs:346-370`).
   - Score all data with the consensus weights; track the best iteration; stop after
     2 consecutive non-improvements (`CalibrationScorer.cs:379-395`).
3. Return the best iteration's scores (may be the baseline).

Competition is paired: `CompeteCalibrationPairs` (`CalibrationScorer.cs:150`) groups
by `base_id`, keeps the best target and best decoy, and competes them with strict
`>` so **ties go to the decoy** (conservative). Only winners enter the q-value walk;
q-values use `QValueCalculator.ComputeQValues` on the winners sorted by score
descending. See `07-fdr-control.md`.

### 3d. S/N quality filter → RT points

`CollectCalibrationPoints` (`Calibrator.cs:651`) keeps only non-decoy winners with
`QValue <= CAL_FDR_THRESHOLD (0.01)` **and** `snr >= MIN_SNR_FOR_RT_CAL = 5.0`
(`Calibrator.cs:53, 675`). Their `(libRt, measuredRt)` pairs are the LOESS input. If
fewer than the pass's `minLoessPoints` survive, the pass returns null
(`Calibrator.cs:479-485`).

### 3e. Mass calibration aggregation

`AggregateMassCalibrations` (`Calibrator.cs:706`) collects MS1 and MS2 errors from
the *same* surviving targets (FDR + S/N passing). MS2 errors are flattened across all
matched fragments; MS1 errors are the single M+0 precursor error per match.
`MzCalibration.CalculateSingleLevel` (`MzCalibration.cs:233`) computes mean, median,
sample SD (n-1 denominator, two left-to-right passes), and
`AdjustedTolerance = |mean| + 3·SD`. The unit ("ppm" vs "Th") is taken from
`config.FragmentTolerance.Unit` (`Calibrator.cs:737`).

### 3f. LOESS fit

`RunCalibrationScoringPass` builds an `RTCalibratorConfig` and calls
`RTCalibrator.Fit` (`Calibrator.cs:501-519`; `RTCalibration.cs:100`):

- `Bandwidth = config.RtCalibration.LoessBandwidth` (default 0.3)
- `Degree = 1` (local linear)
- `MinPoints = min(20, n)`
- `RobustnessIterations = 2`
- `OutlierRetention = 1.0` (LDA + S/N already filtered, so no percentile trim)
- `ClassicalRobustIterations = OspreyEnvironment.LoessClassicalRobust` (default true)

The fit sorts by `(libraryRt, measuredRt)` with a stable LINQ `OrderBy/ThenBy`
(`RTCalibration.cs:126-129`) — critical because multi-charge peptides share a library
RT and an unstable sort would diverge from Rust. See "RT Calibration internals" below.

## Step 4 — Two-pass refinement gating and acceptance

`RunCalibration` (`Calibrator.cs:287-366`), mirroring Rust `pipeline.rs`:

1. Compute pass 1's tolerance from its robust spread:
   `madTolerance = MAD · 1.4826 · 3.0`, clamped to
   `[MinRtTolerance, MaxRtTolerance]` (`Calibrator.cs:290-293`).
2. **Refine only if the tolerance narrowed >= 2×**:
   `pass1Tolerance < initialTolerance * 0.5` (`Calibrator.cs:306`). If pass 1 was
   already tight, pass 2 is skipped.
3. Run pass 2 with the narrowed tolerance and pass 1's LOESS model.
4. **Accept the refined calibration only if `pass2.RSquared >= pass1.RSquared * 0.99`**
   (`Calibrator.cs:337`). On acceptance, pass 2's RT calibration and MS1/MS2 mass
   calibrations replace pass 1's; otherwise pass 1 is kept
   (`Calibrator.cs:348-366`).

`numSampledPrecursors` is reported as pass 1's total scored match count (before any
q/S-N filtering), matching Rust's `accumulated_matches.len()` (`Calibrator.cs:285`).

---

## RT Calibration internals (`RTCalibration.cs`, `LoessRegression.cs`)

### LOESS fitting

`LoessRegression.Fit` (`LoessRegression.cs:239`):

1. Stable sort by `(x, y)` (`LoessRegression.cs:259-262`).
2. Initial unweighted fit (`LoessFitInternal`), then `RobustnessIterations`
   bisquare-weighted refits.
3. Per iteration: `s = 6 · median(|residual|)`; weight `(1 - u²)²` for `|u| < 1`
   else 0, with `u = |r|/s` (`LoessRegression.cs:300-322`). The `(1-u²)²` is written
   as `t*t` (not `Math.Pow`) so the bisquare weight is bit-identical to Rust's
   `powi(2)`.
4. Each local fit uses the `k = ceil(bandwidth·n)` nearest neighbours (two-pointer
   expansion, `FindKNearestSorted`), tricube distance weights, and a weighted 2×2
   linear solve (`LoessFitInternal`, `LoessRegression.cs:401-470`). The local fits are
   parallelized with `Parallel.For` (each output index is independent) to close the
   gap with Rust's auto-vectorized serial loop.

**Classical vs legacy robust iterations**
(`LoessRegression.cs:229-237, 291-298`): when `classicalRobust` is true (production
default via the env var), residuals are recomputed from the current fit at the top of
each iteration (Cleveland 1979). When false, the initial-fit residuals are reused
across all iterations (legacy single-refresh). Toggle with
`OSPREY_LOESS_CLASSICAL_ROBUST=0`.

### Prediction and interpolation

`LoessModel.Predict` (`LoessRegression.cs:59`): binary-search the sorted x for the
bracketing pair, linear-interpolate the fitted values; linearly extrapolate outside
the range. **Duplicate library RTs** (`|x1 - x0| < 1e-12`) average the two fitted
values instead of dividing by zero (`LoessRegression.cs:77-78`) — the NaN fix the
Rust doc calls out.

### Local RT tolerance

`RTCalibration.LocalTolerance(libraryRt, factor, minTolerance)`
(`RTCalibration.cs:251`) interpolates the smoothed absolute residual at a query RT
(`InterpolateAbsResidual` + `SmoothedAbsResidual` over ±2 neighbours,
`RTCalibration.cs:378-413`) and returns `max(localResidual · factor, minTolerance)`.
**This method exists and is unit-tested, but the main search does not use it for
candidate selection** — see the Divergences section.

### Statistics

`RTCalibration.Stats()` (`RTCalibration.cs:258`) reports `NPoints`, `ResidualSD`,
`MeanResidual`, `MaxResidual`, `RSquared`, `P20/P80AbsResidual`, and `MAD` (median of
absolute residuals). `PercentileValue` uses round-half-away-from-zero to match Rust's
`f64::round` (`LoessRegression.cs:367-380`). The final search-window half-width the
main search actually uses is defined once by
`RTCalibration.SearchWindowHalfWidth(mad, min, max) = clamp(3·mad·1.4826, min, max)`
(`RTCalibration.cs:318`), shared by the scoring path, the JSON, and the console
summary.

---

## Mass Calibration internals (`MzCalibration.cs`)

- **Unit-aware**: HRAM instruments report errors in ppm, unit-resolution instruments
  in Th. `MzCalibrationResult.Unit` carries the string; `ToleranceUnit.Mz`
  corresponds to Th (`MzCalibration.cs:200, 235`).
- **MS1 (precursor) error**: `ComputeMs1MassError` (`Calibrator.cs:1298`) finds the
  nearest MS1 to the apex RT (`ScoringTaskShared.FindNearestMs1`), extracts the M+0
  isotope peak with `IsotopeEnvelope.Extract` using the isotope spacing
  `NEUTRON_MASS = 1.002868` Da (`Osprey.Core/IsotopeEnvelope.cs:31`), and reports the
  error in the fragment-tolerance unit so MS1 and MS2 share a unit.
- **MS2 (fragment) error**: `CollectMs2FragmentErrors` (`Calibrator.cs:1268`) matches
  the top-6 library fragments to the apex spectrum within the fragment tolerance and
  records each closest-peak error.
- **Statistics**: mean (systematic offset), median, sample SD (n-1), and
  `AdjustedTolerance = |mean| + 3·SD` (`MzCalibration.cs:280-316`).

---

## Applying calibration in the main search

Stage 4 consumes the calibration in `Osprey.Scoring/ScoringPipeline.cs`:

- **MS2 spectrum correction** (`ScoringPipeline.cs:104-129`): when
  `ms2Calibration.Calibrated`, every spectrum's m/z array is copied and corrected via
  `MzCalibration.ApplyCalibration` (`MzCalibration.cs:147`): PPM
  `corrected = observed - observed·mean/1e6`; Th `corrected = observed - mean`. The
  spectra are copied (not mutated in place) so repeated `run_search` calls do not
  apply the offset cumulatively.
- **Calibrated fragment tolerance** (`ScoringPipeline.cs:193-214`):
  `MzCalibration.CalibratedTolerance` returns `max(3·SD, floor)` where the floor is
  `1.0 ppm` (HRAM) or `0.05 Th` (unit) (`MzCalibration.cs:193-211`). Falls back to the
  configured base tolerance when uncalibrated.
- **RT tolerance** (`ScoringPipeline.cs:145-187`): a single **global** tolerance is
  used, not a per-entry local tolerance —
  `SearchWindowHalfWidth(mad, MinRtTolerance, MaxRtTolerance)` = `clamp(3·mad·1.4826,
  min, max)`. The MAD is preferentially read from the persisted first-pass
  `rt_calibration.mad` (`context.OriginalRtMad`), falling back to the in-memory
  calibration's stats MAD. A separate **unclamped** RT sigma `max(5·mad·1.4826, 0.1)`
  drives the Gaussian RT penalty during CWT peak ranking. When no RT calibration
  exists, `FallbackRtTolerance` (2.0 min) is used.
- **RT prediction of candidates**: `PeakDataExtractor` predicts each candidate's
  expected RT via `rtCalibration.Predict(candidate.RetentionTime)`
  (`Osprey.Scoring/PeakDataExtractor.cs:98`).

---

## Calibration caching (`CalibrationIO.cs`, `CalibrationParams.cs`)

After discovery, `PerFileScoringTask.RunCalibrationForFile`
(`PerFileScoringTask.cs:1700-1738`) serializes a `CalibrationParams` (metadata +
MS1/MS2 `MzCalibrationJson` + `RTCalibrationJson` with the full LOESS `model_params`)
to `{inputStem}.calibration.json` next to the input (or in the resolved output dir).
`CalibrationIO.SaveCalibration` writes atomically via `FileSaver`
(`CalibrationIO.cs:42`). On resume, `PerFileScoringTask` reloads the JSON and
reconstructs the model with `RTCalibration.FromModelParams`
(`PerFileScoringTask.cs:1055, 1303`, `RTCalibration.cs:359`), skipping discovery. The
JSON stores `(library_rts, fitted_rts, abs_residuals)` triples so the curve is
rebuilt by interpolation without re-fitting.

The persisted `RTCalibrationJson` also records `mad`, `p20_abs_residual`, and
`rt_search_window_halfwidth` (`CalibrationParams.cs:296-363`) so the main search and
console report the same window number (issue #4364).

---

## Flags and switches

| Flag / config / env var | Default | Effect on this stage |
|---|---|---|
| `RtCalibration.Enabled` (config) | `true` | Master switch. When false, discovery is skipped and the main search uses `FallbackRtTolerance` with raw library RTs (`PerFileScoringTask.cs:1668`). No dedicated CLI flag. |
| `RtCalibration.LoessBandwidth` | `0.3` | Fraction of points per local LOESS fit (`Calibrator.cs:503`). |
| `RtCalibration.MinCalibrationPoints` | `200` | Pass-1 LOESS floor; below it, calibration fails to fallback (`Calibrator.cs:259, 479`). |
| `RtCalibration.RtToleranceFactor` | `3.0` | Multiplier on the residual/MAD spread. Note: the search path multiplies `MAD·1.4826` by a hardcoded `3.0` in `SearchWindowHalfWidth`, not this config field. |
| `RtCalibration.FallbackRtTolerance` | `2.0` min | RT tolerance when calibration is disabled or fails (`ScoringPipeline.cs:185`). |
| `RtCalibration.MinRtTolerance` | `0.5` min | Lower clamp on the search RT window (`ScoringPipeline.cs:176`) and on the pass tolerances (`Calibrator.cs:291-293`). |
| `RtCalibration.MaxRtTolerance` | `3.0` min | Upper clamp (hard cap) on the search RT window. |
| `RtCalibration.CalibrationSampleSize` | `100000` | Target sampled entries; `0` = use all (`Calibrator.cs:197, 759`). |
| `RtCalibration.CalibrationRetryFactor` | `2.0` | Present on the config but **unused** by the C# `Calibrator` (see Divergences). |
| `--resolution {unit\|hram\|auto}` | `auto` | Selects the main-search scorer and the mass-error unit context; the calibration XCorr is always unit-resolution regardless (`Calibrator.cs:72`). |
| `--fragment-tolerance` / `--fragment-unit` | ppm; forced `mz 0.5` at unit resolution | Base fragment tolerance used before calibration and as the fallback when uncalibrated (`ScoringPipeline.cs:192`). Also sets the mass-error unit ("ppm"/"Th"). |
| `PrecursorTolerance` (config, ppm) | `10.0` | Window for MS1 M+0 isotope extraction (`Calibrator.cs:1305-1308`). |
| `OSPREY_LOESS_CLASSICAL_ROBUST` (env) | on (unset ⇒ classical) | `0` reverts LOESS to legacy single-refresh residuals (`OspreyEnvironment.cs:72`, `LoessRegression.cs:291-298`). |
| `OSPREY_EXIT_AFTER_CALIBRATION` (env) | off | Exit after Stage 3, skipping the main search (`OspreyEnvironment.cs:79`). |
| `OSPREY_LOAD_CALIBRATION=<path>` (env) | unset | Load a Rust-produced `.calibration.json` instead of computing; used for cross-impl bisection (`PerFileScoringTask.cs:1623-1667`). |

### Diagnostic env vars (shared with Rust; each has a `*_ONLY` abort variant)

`OSPREY_DUMP_CAL_SAMPLE` (sampled entry IDs + grid contents, `Calibrator.cs:203, 833`),
`OSPREY_DUMP_CAL_WINDOWS` (per-entry window selection, `Calibrator.cs:403`),
`OSPREY_DUMP_CAL_MATCH` (per-entry match info, `Calibrator.cs:413`),
`OSPREY_DUMP_LDA_SCORES` (per-entry discriminant + q-value, `Calibrator.cs:464`),
`OSPREY_DUMP_LOESS_INPUT` (the `(libRt, measuredRt)` pairs actually fed to the
accepted fit, `Calibrator.cs:274, 343`), `OSPREY_DUMP_MS2_CAL_ERRORS`
(`Calibrator.cs:731`), and the per-entry XIC trace via
`OSPREY_DIAG_XIC_ENTRY_ID` / `OSPREY_DIAG_XIC_PASS` (`Calibrator.cs:1060`). `--write-pin`
also forces the cal-sample dump (`Calibrator.cs:203`).

---

## Divergences from the Rust documentation

- **[STALE-RUST-DOC] RT candidate selection uses a global tolerance, not per-entry
  local tolerance** - The Rust doc's "RT Candidate Selection" section says the main
  search filters candidates by `local_tolerance(library_rt, factor=3.0, min=0.1)`.
  The C# main search instead uses one global window `clamp(3·MAD·1.4826, MinRt, MaxRt)`
  for every entry, matching Rust `run_search` (pipeline.rs:8141-8194). The
  `RTCalibration.LocalTolerance` method is ported and unit-tested but is not wired
  into candidate selection. Evidence: `Osprey.Scoring/ScoringPipeline.cs:145-176`,
  `Osprey.Chromatography/RTCalibration.cs:251`. Severity: minor.

- **[STALE-RUST-DOC] Global RT tolerance is MAD-based, not `residual_sd · factor`** -
  The Rust doc says the global fallback tolerance is `max(residual_sd ·
  rt_tolerance_factor, min_rt_tolerance)`. Both the C# port and current Rust code
  prefer `3 · MAD · 1.4826` (robust SD) and only fall back to the `residual_sd ·
  factor` form when no MAD is available. Evidence:
  `Osprey.Tasks/Calibrator.cs:290`, `Osprey.Scoring/ScoringPipeline.cs:170-177`
  (Rust: pipeline.rs:1307, 8143-8151). Severity: minor.

- **[STALE-RUST-DOC] `min_rt_tolerance` default is 0.5 min, not 0.1 min** - The Rust
  doc's Configuration/"Local RT Tolerance" sections state `min_rt_tolerance = 0.1`
  (6 s). Both the C# config default and the current Rust `default_min_rt_tolerance()`
  are `0.5` min. Evidence: `Osprey.Core/RTCalibrationConfig.cs:50` (Rust:
  config.rs:771). Severity: minor.

- **[STALE-RUST-DOC] Calibration cache filename is `<stem>.calibration.json`, not
  `<stem>.osprey_calibration.json`** - The Rust doc's "Calibration Caching" section
  shows `sample.mzML → sample.osprey_calibration.json`. Both the C# port and the Rust
  code produce `sample.calibration.json`. Evidence:
  `Osprey.Chromatography/CalibrationIO.cs:91` (Rust: io.rs:149). Severity: info.

- **[UNVERIFIED] No sample-expansion retry loop or graduated linear-fit fallback** -
  The Rust doc's "Library Sampling" section (3 attempts: `sample_size`, ×`retry_factor`,
  then ALL entries) and Rust `pipeline.rs:1178-1250` (a retry loop plus a
  `MIN_LINEAR_FIT_POINTS` graduated linear fit and an RT-span leverage check) describe
  a robustness path for sparse libraries. The C# `Calibrator` samples exactly once
  (seed 43) and enforces `MinCalibrationPoints` (200) as the pass-1 floor; if unmet it
  returns null and the search falls back to `FallbackRtTolerance` — there is no
  retry-expansion, no `MIN_LINEAR_FIT_POINTS` graduated fit, and no RT-span check. The
  `CalibrationRetryFactor` config field exists but is never read. On the certified
  Stellar/Astral reference datasets the first 100K sample always clears 200 confident
  peptides, so the retry path is never exercised and cross-impl parity holds; on a
  small or sparse library the C# port would fail calibration where Rust recovers.
  Evidence: `Osprey.Tasks/Calibrator.cs:196-197, 259, 261-267, 479-485`. Severity:
  major. A human should confirm whether the retry/graduated-fit omission is an
  intentional simplification or a genuine port gap for small-library inputs.

- **[INTENTIONAL-CSHARP-DESIGN] Calibration XCorr is managed unit-bin code, not BLAS
  sdot** - The Rust doc's LDA feature table describes the XCorr feature as "Comet-style
  XCorr (unit resolution, BLAS sdot)". The C# port has no BLAS; it uses a managed
  `SpectralScorer(BinConfig.UnitResolution())` with an f32 preprocess path (chosen to
  match Rust's native f32 XCorr and to avoid .NET LOH pressure from 100K-bin arrays).
  Numerically equivalent at F10 rounding. Evidence:
  `Osprey.Tasks/Calibrator.cs:72, 563`. Severity: info. See `17-vectorization.md`.

Everything else verified matches the Rust documentation: stratified grid sampling
(bins, per-cell stride, top-up pass, paired decoys), the 4 LDA features and their
normalizations, 3-fold peptide-grouped CV with non-negative clipped weights, paired
target-decoy competition with ties-to-decoy, the S/N >= 5.0 gate, LOESS parameters
(bandwidth 0.3, degree 1, 2 robustness iterations, tricube/bisquare weights,
`s = 6·MAD`), duplicate-RT averaging, the two-pass refinement gate
(`< 0.5× initial`) and acceptance criterion (`R² >= 0.99×`), MS1 M+0 extraction with
1.002868 Da spacing, MS2 top-6 fragment errors, mass statistics (`|mean| + 3·SD`,
sample SD), the PPM/Th spectrum-correction formulas, the calibrated-tolerance floors
(1.0 ppm / 0.05 Th), and the JSON schema (`model_params` triples).
