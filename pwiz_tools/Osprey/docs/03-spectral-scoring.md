# 03. Spectral Scoring & the 21 PIN Features (C#)

> Pipeline stage: Stage 3/4 (scoring). C# port of Rust docs/03-spectral-scoring.md. Corresponds to Rust osprey spectral scoring (calibration XCorr/E-value/isotope) and main-search 21-PIN-feature co-elution scoring.

This document covers the two distinct scoring regimes in the C# Osprey port:

1. **Calibration-phase spectral scoring** (Stage 3): XCorr + LibCosine + top-fragment
   matching + isotope cosine, combined by a 4-feature LDA for target-decoy
   competition that yields calibration points. See also `04-calibration.md`.
2. **Main-search scoring** (Stage 4): fragment-XIC co-elution analysis producing the
   **21 PIN features** consumed by the Percolator SVM in `07-fdr-control.md`.

The XCorr primitive that both regimes share is documented in depth in
`02-xcorr-scoring.md`; peak detection (which drives the main search) is in
`06-peak-detection.md`.

---

## Part A — Calibration-phase spectral scoring (Stage 3)

### A.1 XCorr scorer construction — always unit-resolution bins

Calibration builds the scorer with **unit-resolution binning regardless of the
data's resolution mode**, matching the Rust doc's "always 2001 bins" note.

- `Osprey.Tasks/Calibrator.cs:73` — `new SpectralScorer(BinConfig.UnitResolution())`.
- `Osprey.Scoring/SpectralScorer.cs:52` — the parameterless ctor defaults to
  `BinConfig.UnitResolution()`.
- `Osprey.Core/BinConfig.cs:42` — unit config: `binWidth = 1.0005079`, `offset = 0.4`,
  `maxMz = 2000.0`, `NBins = (int)(maxMz/binWidth + 0.6) + 1`. This evaluates to
  **2000 bins**, not the 2001 the Rust doc rounds to (see Divergences).

The Comet fast-XCorr preprocessing (bin sqrt intensities → 10-window normalization
to max 50 with a 5% global-max threshold → sliding-window subtraction with
`offset = 75`, `scale = 0.005`) lives in `SpectralScorer.cs`:
`PreprocessSpectrumForXcorr` (`:92`), `ApplyWindowingNormalizationD` (`:522`),
`ApplySlidingWindowD` (`:573`), and the per-scan form `XcorrAtScan` (`:396`). The
theoretical spectrum is NOT preprocessed — `XcorrFromPreprocessed` (`:186`) simply
sums the preprocessed observed values at each fragment bin (deduping repeated bins
via a visited-bin array) and scales by `0.005`. This exactly matches the Rust doc's
"Comet does NOT preprocess the theoretical spectrum" invariant.

### A.2 LibCosine at apex

`SpectralScorer.LibCosine` (`SpectralScorer.cs:669`) matches each library fragment to
the closest observed peak within tolerance, applies `sqrt` intensity preprocessing to
both, and returns the clamped `[0,1]` cosine (`CosineAngle`, `:731`). The value is
stored on `CalibrationMatch.LibcosineApex`.

### A.3 Fragment matching / MS2 mass errors

The calibration match struct (`Osprey.Scoring/CalibrationScorer.cs:34`,
`CalibrationMatch`) carries `Top6MatchedApex`, `XcorrScore`, `Ms2MassErrors[]`, and
`Ms1Error?`. These feed MS2/MS1 mass calibration for the main search (see
`04-calibration.md`). The Rust doc's "top-3 fragment matching by binary search" and
the closest-peak-by-mz tie-break are realized in the main-search apex matcher
(`ApexMatchCalculators.cs:107`, strict `<` keeps the lower-index peak on a distance
tie — the same `match_fragments` semantics).

### A.4 Isotope cosine (MS1 quality)

`CalibrationMatch.IsotopeCosine` holds the isotope-envelope cosine computed from the
peptide's exact elemental composition (`IsotopeDistribution.PeptideIsotopeCosine`,
reused by main-search feature 14 — see A.6 / B). This is only meaningful when MS1
spectra are present.

### A.5 LDA discriminant & target-decoy competition (4 features)

`CalibrationScorer.TrainAndScoreCalibration` (`CalibrationScorer.cs:84`) is the C#
port of Rust `train_and_score_calibration`. The 4-feature matrix
(`ExtractFeatureMatrix`, `:581`) normalizes exactly as the Rust doc lists:

| LDA feature | Source field | Normalization | Evidence |
|-------------|--------------|---------------|----------|
| correlation | `CorrelationScore` | `clamp(x/6.0, 0, 1)` | `CalibrationScorer.cs:590` |
| libcosine   | `LibcosineApex`    | `clamp(x, 0, 1)`     | `:591` |
| top6 matched| `Top6MatchedApex`  | `clamp(x/6.0, 0, 1)` | `:592` |
| xcorr       | `XcorrScore`       | `clamp(x/3.0, 0, 1)` | `:593` |

Training is iterative non-negative CV LDA (`TrainLdaWithNonNegativeCv`, `:227`):
best-single-feature baseline → up to 3 iterations of 3-fold stratified-by-peptide CV,
average fold weights, clip negatives, renormalize, keep the best-passing iteration,
early-stop after 2 non-improvements. Target-decoy competition
(`CompeteCalibrationPairs`, `:150`) groups by `entry_id & 0x7FFFFFFF`, and **ties go
to the decoy** (strict `>` in favor of target, `:197`) — the conservative rule the
Rust doc specifies. Winners are q-valued via `QValueCalculator.ComputeQValues` and
filtered at 1% FDR.

### A.6 E-value — NOT computed in C#

The Rust doc describes a Comet-style E-value from the XCorr survival function,
"computed and stored but not used for FDR." **The C# `CalibrationMatch` has no
`evalue` field** (`CalibrationScorer.cs:34-56`) and no survival-function fit exists in
the port. Because the LDA discriminant (not the E-value) drives competition, output
parity is unaffected. See Divergences.

---

## Part B — Main-search scoring: the 21 PIN features (Stage 4)

The main search does NOT use XCorr+E-value. It runs fragment-XIC co-elution analysis
and computes a **21-element PIN feature vector** per candidate peak, which is written
to the `.scores.parquet` cache and consumed by the SVM.

### B.1 Pipeline order

```
AnalysisPipeline / PerFileScoring task
  └─ ScoringPipeline.RunCoelutionScoring          (ScoringPipeline.cs:65)
       ├─ context.Resolution.CreateScorer()        resolution-mode bins (Unit or HRAM)
       ├─ apply MS2 calibration to a LOCAL spectra copy   (:104-129)
       ├─ group spectra by isolation-window key     (:132)
       ├─ compute GLOBAL rt tolerance (3*MAD*1.4826) + rt sigma (5*MAD*1.4826)  (:159-187)
       └─ Parallel.For over isolation windows       (:269)
            └─ CoelutionScorer.ScoreWindow          (CoelutionScorer.cs:70)
                 ├─ find candidates whose precursor m/z ∈ window   (:106)
                 ├─ PreprocessWindowSpectra (per-window XCorr cache) (:130)
                 └─ for each candidate: ScoreCandidate (:180)
                      ├─ PeakDataExtractor.TryExtract   (prefilter, XICs, CWT peaks, apex)
                      ├─ publish Tukey median-polish byproduct
                      └─ run the 21 OspreyFeatureCalculators → double[21]
```

Windows run in parallel (`MaxDegreeOfParallelism = config.NThreads`,
`ScoringPipeline.cs:271`); per-window results are placed into a fixed-index array and
flattened in **window order** (`:301`) so the parquet byte stream is reproducible
regardless of completion order.

### B.2 Per-candidate data production — `PeakDataExtractor`

`PeakDataExtractor.TryExtract` (`Osprey.Scoring/PeakDataExtractor.cs`) is the producer
seam mirroring Skyline's results layer. In order:

1. **Expected RT** = `rtCalibration.Predict(candidate.RetentionTime)`
   (`PeakDataExtractor.cs:97`); scan range from the global RT tolerance (`:100-108`).
   A range shorter than 5 scans is rejected.
2. **Signal pre-filter** (`:117-143`): require ≥2 of the top-6 fragments present in
   ≥3 of 4 consecutive scans (`FragmentMath.HasTopNFragmentMatch`). Gated by
   `config.PrefilterEnabled` (default true; `--no-prefilter` disables) and **skipped
   for boundary overrides** (the Stage-6 rescore path already decided to score here).
3. **XIC extraction** (`TopFragmentExtractor.ExtractFragmentXics`, `:146`); `< 2` XICs
   rejects the candidate.
4. **CWT peak detection** (3-tier CWT + fallbacks, `DetectCandidatePeaks`, `:169`) —
   see `06-peak-detection.md`.
5. **Best-peak ranking** (`:185-217`):
   `rank = coelution × exp(-dt²/(2σ²)) × ln(1 + apex_intensity)`, where
   `dt = |peak_apex_rt − expected_rt|` and σ is the **unclamped** `5×MAD×1.4826`
   Gaussian sigma (floor 0.1 min). This RT penalty is essential for FDR calibration
   (it stops wrong-RT interferers from winning on coelution alone).
6. **Reference XIC** = highest total-intensity fragment, **last on tie** (`>=`, seed
   `-1.0`, `:204-212`) — deliberately matching Rust's `max_by` returning the last
   equal element.

Boundary-override scoring (Stage-6 multi-charge consensus / reconciliation / gap-fill)
enters through the same `TryExtract` with `overrideBounds` set, which skips the
prefilter and CWT and maps the supplied `(apex, start, end)` RTs to scan indices. See
`11-boundary-overrides.md`.

### B.3 The 21 calculators — order is the parity contract

`OspreyFeatureCalculators` (`Osprey.Scoring/OspreyFeatureCalculators.cs:41`) holds the
ordered calculator array; **the array index IS the PIN feature index** and the parquet
column order. `CoelutionScorer.ScoreCandidate` invokes `Get(0..20).Calculate(...)`
explicitly (`CoelutionScorer.cs:254-274`). The names match
`ParquetScoreCache.PIN_FEATURE_NAMES` (`Osprey.IO/ParquetScoreCache.cs:51`).

| # | PIN name | Family / tier | Direction (`IsReversedScore`) | C# calculator (file) |
|---|----------|---------------|-------------------------------|----------------------|
| 0 | `fragment_coelution_sum` | Coelution (detailed) | higher | `FragmentCoelutionSumCalc` (CoelutionCalculators.cs:107) |
| 1 | `fragment_coelution_max` | Coelution | higher | `FragmentCoelutionMaxCalc` (:122) |
| 2 | `n_coeluting_fragments` | Coelution | higher | `NCoelutingFragmentsCalc` (:137) |
| 3 | `peak_apex` | Peak shape (detailed) | higher | `PeakApexCalc` (PeakShapeCalculators.cs:116) |
| 4 | `peak_area` | Peak shape | higher | `PeakAreaCalc` (:136) |
| 5 | `peak_sharpness` | Peak shape | higher | `PeakSharpnessCalc` (:168) |
| 6 | `xcorr` | Apex spectrum | higher | `XcorrCalc` (XcorrCalculators.cs:53) |
| 7 | `consecutive_ions` | Apex spectrum | higher | `ConsecutiveIonsCalc` (ApexMatchCalculators.cs:146) |
| 8 | `explained_intensity` | Apex spectrum | higher | `ExplainedIntensityCalc` (:218) |
| 9 | `mass_accuracy_deviation_mean` | Apex spectrum | (signed; false by convention) | `MassAccuracyMeanCalc` (:240) |
| 10 | `abs_mass_accuracy_deviation_mean` | Apex spectrum | lower | `AbsMassAccuracyMeanCalc` (:269) |
| 11 | `rt_deviation` | Summary | (signed; false) | `RtDeviationCalc` (RtDeviationCalculators.cs:33) |
| 12 | `abs_rt_deviation` | Summary | lower | `AbsRtDeviationCalc` (:52) |
| 13 | `ms1_precursor_coelution` | MS1 (detailed) | higher | `Ms1PrecursorCoelutionCalc` (Ms1Calculators.cs:40) |
| 14 | `ms1_isotope_cosine` | MS1 (detailed) | higher | `Ms1IsotopeCosineCalc` (:71) |
| 15 | `median_polish_cosine` | Median polish | higher | `MedianPolishCosineCalc` (MedianPolishCalculators.cs:56) |
| 16 | `median_polish_residual_ratio` | Median polish | lower | `MedianPolishResidualRatioCalc` (:73) |
| 17 | `sg_weighted_xcorr` | Apex ±2 spectra | higher | `SgXcorrCalc` (XcorrCalculators.cs:245) |
| 18 | `sg_weighted_cosine` | Apex ±2 spectra | higher | `SgCosineCalc` (:267) |
| 19 | `median_polish_min_fragment_r2` | Median polish | higher | `MedianPolishMinFragmentR2Calc` (MedianPolishCalculators.cs:92) |
| 20 | `median_polish_residual_correlation` | Median polish | lower | `MedianPolishResidualCorrelationCalc` (:109) |

Note the array is populated out of numeric order in the source (slots 17/18 are set
alongside slot 6 because they share the XCorr machinery, and the median-polish slots
15/16/19/20 are set together), but each is assigned to its correct PIN index
(`OspreyFeatureCalculators.cs:53-88`).

### B.4 Feature-family details (verified)

**Coelution (0–2)** — `CoelutionStats.Compute` (`CoelutionCalculators.cs:50`) does one
`i<j` pairwise pass of `ScoringMath.PearsonCorrelationInRange` over
`[peak.StartIndex, peak.EndIndex]`. NaN pairs are **skipped** (not zeroed); `max`
seeds `-Infinity` and is only adopted when a valid pair exists (else 0). A fragment is
"coeluting" when its mean pairwise correlation is `> 0`. All three features come from
this single cached pass.

**Peak shape (3–5)** — `PeakShapeReference` (`PeakShapeCalculators.cs:42`) selects the
reference XIC as highest-total-intensity, **last on tie** (`>=`, seed `-1.0`).
`peak_apex` is a direct lookup of the CWT/override apex value (NOT a recomputed local
max, `:124`). `peak_area` is left-to-right trapezoidal integration over `[start, end)`
(`:144`). `peak_sharpness` is the mean of left/right slopes (`:176`), with strict
`dt > 1e-10` guards.

**xcorr (6)** — `XcorrCalc.Calculate` (`XcorrCalculators.cs:61`) routes through
`context.Resolution.ScoreXcorr` at the **window-global** apex index
(`ApexGlobalIndex`). Unit reads the f64 dense cache; HRAM reads the sparse cache
(`SparseXcorrSpectrum.CenteredAt`, bit-identical to the old dense f32 cache — issue
#4398). See `02-xcorr-scoring.md` and `17-vectorization.md`.

**Apex-match (7–10)** — `ApexFragmentMatchSet.Compute` (`ApexMatchCalculators.cs:62`)
does ONE closest-peak-by-mz pass over the apex MS2 spectrum:
- `explained_intensity` = matched / total apex intensity (0 when total ≤ 1e-12).
- `mass_accuracy_deviation_mean` = signed mean fragment mass error (0 on no match).
- `abs_mass_accuracy_deviation_mean` = mean abs error, **falling back to the live
  calibrated `FragmentTolerance.Tolerance` (NOT 0.0) on no match** (`:282`) — a 0.0
  fallback historically caused ~65 divergent Astral rows.
- `consecutive_ions` (7) is a SEPARATE boolean `SpectralScorer.HasMatch` pass keyed by
  ion type + ordinal (`:154`), returning the longest consecutive b- or y-ion run.

**RT deviation (11–12)** — `apex_rt − expected_rt` and its absolute value
(`RtDeviationCalculators.cs:45`, `:60`). NaN propagates by design (no fallback).

**MS1 (13–14)** — HRAM-only. The producer (`PeakDataExtractor`) emits the MS1
precursor XIC / reference XIC and the apex isotope envelope only when the resolution
strategy reports `HasMs1Features` (`ResolutionStrategy.cs:163` HRAM=true, `:112`
Unit=false). `ms1_precursor_coelution` is the Pearson correlation of the two MS1
chromatograms (`< 3` samples → 0.0; NaN → 0.0, `Ms1Calculators.cs:52-57`).
`ms1_isotope_cosine` gates on the M0 peak (`envelope[1] > 0`) then calls
`IsotopeDistribution.PeptideIsotopeCosine` on the **unmodified** `Candidate.Sequence`
(`:79-92`). For Unit-resolution runs both are exactly 0.0.

**Median polish (15,16,19,20)** — the Tukey median-polish fit is published **by the
harness** (`CoelutionScorer.ScoreCandidate:242`, `TukeyMedianPolish.Compute` with 10
iterations, 0.01 tol) because its bisection dump lives in the exe layer. XICs are
cropped to the peak range (`peakLen >= 3`) first. The four calculators read the
`MedianPolishByproduct` and apply family defaults when the fit is null:
`cosine` → 0.0, `residual_ratio` → **1.0** (NOT 0.0), `min_fragment_r2` → 0.0,
`residual_correlation` → 0.0 (`MedianPolishCalculators.cs:56-123`).

**SG-weighted (17,18)** — `SgWeightedSweep.Compute` (`XcorrCalculators.cs:130`) sweeps
offsets `-2..+2` with Savitzky-Golay quadratic weights `[-3,12,17,12,-3]/35`. Out-of-
range offsets at window edges are **skipped, not zero-filled, and not renormalized**
(matches Rust). `sg_weighted_xcorr` accumulates per-scan `ScoreXcorr × weight`;
`sg_weighted_cosine` accumulates a mass-range-filtered sqrt-intensity L2-normalized
cosine (`ComputeCosineAtScan`, `:177` — a distinct kernel from
`SpectralScorer.LibCosine`). The candidate-local index is bound-checked against the
window length before mapping to the window-global index — a different index convention
from feature 6 (documented as an "INDEX TRAP" in the source).

### B.5 Byproduct caching

`OspreyScoringContext` (`Osprey.Scoring/OspreyScoringContext.cs:44`) mirrors Skyline's
`PeakScoringContext`: a typed byproduct cache (`AddInfo`/`TryGetInfo`) lets one
producer publish an intermediate (coelution stats, peak-shape reference, apex-match
set, SG sweep, median-polish fit) that its sibling calculators read. The context is
reused across candidates with `ClearByproducts` between them
(`CoelutionScorer.cs:208`).

### B.6 FdrEntry assembly & post-scoring dedup

`BuildFdrEntry` (`CoelutionScorer.cs:395`) sets both `CoelutionSum` and `Score` to
`features[0]` (the raw coelution sum is the pre-SVM ranking score), serializes the full
library fragment list and the reference-XIC slice, and stores the top-N CWT candidates
for Stage-6 reconciliation. After scoring, `ScoringPipeline` runs two dedup passes:
`DeduplicateDoubleCounting` (`:324`; drops same-class entries whose top-6 fragments
overlap ≥50% within ±5 spectra, keeping the higher coelution sum) and
`DeduplicatePairs` (`:526`; one best target + one best decoy per `base_id`, sorted by
EntryId for determinism). See `07-fdr-control.md` and `16-determinism.md`.

---

## Flags and switches

Flags affecting THIS stage (defaults from `Osprey.Core/OspreyConfig.cs` /
`Osprey/OspreyCommandArgs.cs`):

| Flag / field | Default | Effect on scoring |
|--------------|---------|-------------------|
| `--resolution {unit\|hram\|auto}` | `auto` | Selects `UnitStrategy` vs `HramStrategy` (`ResolutionStrategy.cs:97`). Unit = 2000 f64 bins, no MS1 features; HRAM = ~100K sparse bins, MS1 features 13/14 active. **Calibration always uses unit bins regardless.** |
| `--fragment-tolerance <v>` | resolution-dependent | Fragment match window for apex-match, cosine, prefilter, LibCosine. |
| `--fragment-unit {ppm\|mz}` | `ppm` (unit-res forces `mz` 0.5) | Tolerance unit; also the reporting unit for mass-error features 9/10. |
| `--no-prefilter` (`PrefilterEnabled`) | `true` (prefilter ON) | When set, disables the 2-of-top-6-in-3-of-4-scans signal prefilter (`PeakDataExtractor.cs:117`). Prefilter is always skipped for boundary-override rescoring. |
| `--threads <count>` | all cores | INNER parallelism: `MaxDegreeOfParallelism` over isolation windows (`ScoringPipeline.cs:271`). Affects speed only, not results. |
| `--parallel-files [N]` | off (sequential) | OUTER parallelism across files; no effect on per-candidate feature values. |
| RT calibration `MinRtTolerance` / `MaxRtTolerance` / `FallbackRtTolerance` | config | Clamp the scan-window half-width; the Gaussian rank sigma uses the UNCLAMPED `5×MAD×1.4826` (`ScoringPipeline.cs:170-187`). |
| `Reconciliation.TopNPeaks` | config | How many CWT candidates `BuildFdrEntry` captures for Stage-6 (`CoelutionScorer.cs:328`). |

Diagnostic env vars for this stage (shared with Rust for cross-impl bisection):

| Env var | Effect |
|---------|--------|
| `OSPREY_DIAG_XCORR_SCAN` | Dumps XCorr internals for a matching scan to `cs_xcorr_diag.txt` (`SpectralScorer.cs:453`). |
| `OSPREY_DIAG_SEARCH_ENTRY_IDS` | Dumps per-entry search XIC data (`ScoringPipeline.cs:224`, `PeakDataExtractor.cs:154`). |
| `OSPREY_MAX_SCORING_WINDOWS` | Caps the number of scored windows for profiling (`ScoringPipeline.cs:240`). |
| `OSPREY_DUMP_LDA_SCORES` | Calibration LDA score dump (calibration path). |

The **isotope-cosine LDA feature toggle** for calibration is the `useIsotopeFeature`
argument to `TrainAndScoreCalibration` (`CalibrationScorer.cs:84`), driven by MS1
presence rather than a CLI flag.

---

## Divergences from the Rust documentation

- **[UNVERIFIED] E-value not computed in the C# calibration path** - Rust doc says a
  Comet-style E-value is computed from the XCorr survival function and stored on the
  calibration match ("computed but not used for FDR"). The C# `CalibrationMatch` has no
  `evalue` field and no survival-function fit exists in the port.
  Evidence: `Osprey.Scoring/CalibrationScorer.cs:34-56` (struct has no evalue),
  `SpectralScorer.cs` (no survival function). Because competition uses the LDA
  discriminant, not the E-value, output parity is unaffected. Whether the current Rust
  *code* still computes/stores E-value could not be checked here; likely
  STALE-RUST-DOC or an intentional drop of dead output. Severity: info.

- **[STALE-RUST-DOC] Unit-resolution bin count is 2000, not 2001** - Rust doc repeatedly
  says calibration uses "2001 bins (1.0005 m/z)". The C# `BinConfig.UnitResolution()`
  computes `NBins = (int)(2000/1.0005079 + 0.6) + 1 = 2000`.
  Evidence: `Osprey.Core/BinConfig.cs:42-59`; confirmed by the test comment "unit
  resolution: 2000 bins" at `Osprey.Test/ScoringTest.cs:1087`. The Rust code produces
  the same 2000 (bit-identical parity per README); "2001" is a doc approximation.
  Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Only the 21 PIN features are computed; the ~26 non-PIN
  scores are not** - The Rust doc's "Spectral Scores at Apex (15 features)" and
  "Co-Elution Features (11 features)" tables list `hyperscore`, `dot_product`(+`_smz`,
  `_top4..6`), `fragment_coverage`, `sequence_coverage`, `elution_weighted_cosine`,
  `fragment_coelution_min`, `n_fragment_pairs`, `fragment_corr_0..5`, etc. The C#
  calculator pass computes exactly the 21 PIN features and nothing else; the leftover
  fields still exist on `CoelutionFeatureSet` but are never populated by the scoring
  pass.
  Evidence: `OspreyFeatureCalculators.cs:36-44` ("Scores the Rust engine computes but
  excludes from the 21 PIN features are intentionally NOT here"); `CoelutionScorer.cs:253-274`
  builds only a `double[21]`; `Osprey.Core/CoelutionFeatureSet.cs:30-128` still declares
  the unused fields. The Rust doc itself notes "21 PIN features (out of ~47 computed)",
  so this is a deliberate port simplification, not a behavioral output difference.
  Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] MS1 features gated by resolution strategy, not a config
  flag** - The Rust doc treats MS1 isotope/precursor-coelution as always-on when MS1
  scans exist. In C# they are strictly HRAM-only: `UnitStrategy.HasMs1Features` is
  `false` so the producer emits no MS1 chromatogram/envelope and features 13/14 are
  exactly 0.0.
  Evidence: `ResolutionStrategy.cs:112` (Unit false) vs `:163` (HRAM true);
  `Ms1Calculators.cs:37-38` ("HRAM-only"). This matches the Rust engine's behavior;
  the doc simply doesn't foreground the resolution gate. Severity: info.

Otherwise the C# main-search feature computation matches the Rust documentation step
for step: the pipeline order (calibrated local spectra → per-window parallel scoring →
prefilter → CWT peak ranking with the Gaussian RT penalty → 21-feature calculator pass
→ dedup), the Comet XCorr preprocessing invariants (no theoretical preprocessing;
window normalization to 50 at 5% threshold; sliding-window offset 75; scale 0.005), the
closest-peak-by-mz tie-breaks, the last-on-tie reference-XIC selection, the calibration
4-feature LDA with decoy-favoring ties at 1% FDR, and the shared
`compute_features_at_peak`/`run_search` boundary-override design (see
`11-boundary-overrides.md`) were all verified against the C# source.
