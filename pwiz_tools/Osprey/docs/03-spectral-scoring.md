# 03. Spectral Scoring & the PIN Features (C#)

> Pipeline stage: Stage 3/4 (scoring). C# port of Rust docs/03-spectral-scoring.md. Corresponds to Rust osprey spectral scoring (calibration XCorr/E-value/isotope) and main-search 21-PIN-feature co-elution scoring, plus 20 C#-only PIN features (B.7).

This document covers the two distinct scoring regimes in the C# Osprey port:

1. **Calibration-phase spectral scoring** (Stage 3): XCorr + LibCosine + top-fragment
   matching + isotope cosine, combined by a 4-feature LDA for target-decoy
   competition that yields calibration points. See also `04-calibration.md`.
2. **Main-search scoring** (Stage 4): fragment-XIC co-elution analysis producing the
   **41 PIN features** consumed by the Percolator classifier in `07-fdr-control.md`: the 21 of
   the Rust engine, and 20 C#-only features the classifier does not see by default (B.7, which
   also says when to turn them on and how many fragment XICs to extract for each kind of data).

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

## Part B — Main-search scoring: the PIN features (Stage 4)

The main search does NOT use XCorr+E-value. It runs fragment-XIC co-elution analysis
and computes a **41-element PIN feature vector** per candidate peak, which is written
to the `.scores.parquet` cache and consumed by the FDR classifier. The first 21 are the
Rust engine's (B.3-B.4); 21-40 are C#-only and masked from the classifier by default (B.7).

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
                      ├─ publish the candidate-peak byproduct (runner-up features)
                      └─ run the 41 OspreyFeatureCalculators → double[41]
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
3. **XIC extraction** (`TopFragmentExtractor.ExtractFragmentXics`, `:146`) for the top 6
   library fragments, or `OSPREY_XIC_FRAGMENTS` of them (B.7); `< 2` XICs rejects the
   candidate. The prefilter in step 2 and calibration keep 6 either way.
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

### B.3 The calculators — order is the parity contract

`OspreyFeatureCalculators` (`Osprey.Scoring/OspreyFeatureCalculators.cs:41`) holds the
ordered calculator array; **the array index IS the PIN feature index** and the parquet
column order. `CoelutionScorer.ScoreCandidate` invokes `Get(0..20).Calculate(...)`
explicitly, then 21-40 in a loop (`CoelutionScorer.cs:295-317`). The names match
`ParquetScoreCache.PIN_FEATURE_NAMES` (`Osprey.IO/ParquetScoreCache.cs:51`). The table
below is the Rust engine's 21; slots 21-40 are in B.7.

| # | PIN name | Family / tier | Direction (`IsReversedScore`) | C# calculator (file) |
|---|----------|---------------|-------------------------------|----------------------|
| 0 | `fragment_coelution_sum` | Coelution (detailed) | higher | `FragmentCoelutionSumCalc` (CoelutionCalculators.cs:107) |
| 1 | `fragment_coelution_max` | Coelution | higher | `FragmentCoelutionMaxCalc` (:122) |
| 2 | `n_coeluting_fragments` | Coelution | higher | `NCoelutingFragmentsCalc` (:137) |
| 3 | `peak_apex` | Peak shape (detailed) | higher | `PeakApexCalc` (PeakShapeCalculators.cs:116) |
| 4 | `peak_area` | Peak shape | higher | `PeakAreaCalc` (:136) |
| 5 | `peak_sharpness` | Peak shape | higher | `PeakSharpnessCalc` (:168) |
| 6 | `xcorr` | Apex spectrum | higher | `XcorrCalc` (XcorrCalculators.cs:52) |
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
| 17 | `sg_weighted_xcorr` | Apex ±2 spectra | higher | `SgXcorrCalc` (XcorrCalculators.cs:233) |
| 18 | `sg_weighted_cosine` | Apex ±2 spectra | higher | `SgCosineCalc` (:255) |
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
max, `:157-175`). `peak_area` is left-to-right trapezoidal integration over `[start, end)`
(`:204-208`). `peak_sharpness` is the mean of left/right slopes (`:264-278`), with strict
`dt > 1e-10` guards.

All three are **intensity-scale** features and are conditioned as `log10(x + 1)` by
`PeakShapeReference.ConditionIntensityFeature` (`:144`) before they reach the PIN. The
SVM is linear: fed raw, these features standardize to z-scores in the hundreds and the
model becomes an intensity ranker (measured on SEA-AD as an entrapment FDP collapse,
fixed in #4412/#4418). Per-run normalization and a scale-free sharpness are tracked as
#4466.

**xcorr (6)** — `XcorrCalc.Calculate` (`XcorrCalculators.cs:60`) routes through
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
strategy reports `HasMs1Features` (`ResolutionStrategy.cs:184` HRAM=true, `:133`
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

**SG-weighted (17,18)** — `SgWeightedSweep.Compute` (`XcorrCalculators.cs:129`) sweeps
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
(`CoelutionScorer.cs:202`).

### B.6 FdrEntry assembly & post-scoring dedup

`BuildFdrEntry` (`CoelutionScorer.cs:389`) sets both `CoelutionSum` and `Score` to
`features[0]` (the raw coelution sum is the pre-SVM ranking score), serializes the full
library fragment list and the reference-XIC slice, and stores the top-N CWT candidates
for Stage-6 reconciliation. After scoring, `ScoringPipeline` runs two dedup passes:
`DeduplicateDoubleCounting` (`:324`; drops same-class entries whose top-6 fragments
overlap ≥50% within ±5 spectra, keeping the higher coelution sum) and
`DeduplicatePairs` (`:526`; one best target + one best decoy per `base_id`, sorted by
EntryId for determinism). See `07-fdr-control.md` and `16-determinism.md`.

### B.7 The C#-only features (21-40), the feature mask and the XIC count

Twenty features were added after the Rust port, in four families. Scoring always computes them
and writes them to the `.scores.parquet`; the FDR classifier sees them only when
`OSPREY_FEATURE_MASK` lets it. **By default all 20 are masked**, so a default search reports what
the 21 Rust features report, and every golden is unchanged. Turning them on is recommended for
every kind of data measured so far (see "Which settings for which data" below); making that the
default is a separate decision, because it moves every golden.

| # | PIN name | Family | Direction (`IsReversedScore`) | C# calculator (file) |
|---|----------|--------|-------------------------------|----------------------|
| 21 | `peak_coelution_margin` | Runner-up | higher | `PeakCoelutionMarginCalc` (PeakContextCalculators.cs:89) |
| 22 | `peak_rank_margin` | Runner-up | higher | `PeakRankMarginCalc` (:115) |
| 23 | `peak_candidate_count` | Runner-up | lower | `PeakCandidateCountCalc` (:140) |
| 24 | `context_peptide_length` | Context | (none; false) | `PeptideLengthCalc` (:160) |
| 25 | `context_precursor_charge` | Context | (none; false) | `PrecursorChargeCalc` (:175) |
| 26 | `context_precursor_mz` | Context | (none; false) | `PrecursorMzCalc` (:190) |
| 27-32 | `frag_rank1..6_abs_mass_error` | Per-fragment-rank | lower | `FragmentRankMassErrorCalc` (FragmentRankCalculators.cs:101) |
| 33-38 | `frag_rank1..6_intensity_share` | Per-fragment-rank | (none; false) | `FragmentRankIntensityShareCalc` (:135) |
| 39 | `peak_spectrum_cosine_mean` | Spectrum across the peak | higher | `PeakSpectrumCosineMeanCalc` (PeakSpectrumCalculators.cs:75) |
| 40 | `peak_spectrum_cosine_sd` | Spectrum across the peak | higher (see below) | `PeakSpectrumCosineSdCalc` (:90) |

`IsReversedScore` does not reach the classifier: the SVM learns each weight's sign, and the
direction only labels the feature-contribution table and the model diagnostics (a weight
against the expected direction is flagged there).

**Runner-up (21-23)** - how far the chosen peak stands above the other candidates in its own
window, which no apex score can show. `CoelutionScorer.ScoreCandidate` publishes the candidates
that passed apex acceptance as a `PeakCandidatesByproduct` (`CoelutionScorer.cs:291`).
`peak_coelution_margin` is the chosen peak's mean pairwise fragment coelution minus the best
other candidate's, the other floored at 0 (a lone candidate competes with uncorrelated noise); it
can be negative when the pick preferred a less coeluting peak on RT or intensity.
`peak_rank_margin` is the same difference in pick score (the learned pick model's, or the
product form under `OSPREY_PICK_LDA=0`), 0 for a lone candidate. `peak_candidate_count` counts
the candidates. The boundary-override path (Stage 6) scores a peak someone else chose and has
no candidates: the margins read 0 and the count 1.

**Context (24-26)** - peptide length, precursor charge and precursor m/z, as Percolator and
DIA-NN use them: how reliable a match is depends on what is being matched. A decoy has its
target's values, so these carry no target/decoy information of their own; they let the
classifier weigh the other features differently for, say, a short 2+ precursor and a long 3+
one. Predicted RT and fragment count were left out, because a decoy's are predicted separately
and could differ from its target's for reasons unrelated to the data.

**Per-fragment-rank (27-38)** - the six most intense library fragments, each matched in the apex
spectrum (closest peak by m/z within tolerance, as the apex-match family matches; computed once
per candidate as `FragmentRankApexMatch`). `frag_rankN_abs_mass_error` is rank N's absolute mass
error, falling back to the calibrated fragment tolerance when it is unmatched or the library has
fewer than N fragments (the same fallback as `abs_mass_accuracy_deviation_mean`, and for the same
reason). `frag_rankN_intensity_share` is rank N's share of the matched intensity of the six, 0
when none matched; side by side they show whether the observed intensities fall in the
library's order. Where the apex-match family averages over all fragments, these let the
classifier weigh each rank separately.

**Spectrum across the peak (39-40)** - the library cosine at every scan from the peak's start to
its end (the SG sweep's per-scan kernel, `SgWeightedSweep.ComputeCosineAtScan`, skipping scans
outside the window), reduced to a mean and a population standard deviation. Where
`sg_weighted_cosine` looks at the apex +/- 2 scans, these ask whether the library spectrum holds
across the whole elution. `peak_spectrum_cosine_sd` is higher-is-better, against first
intuition: the trained SVM gives it a positive weight (7th largest on Astral), because a real
peak's cosine rises and falls with its signal across the peak while noise stays uniformly low.

#### The feature mask (`OSPREY_FEATURE_MASK`)

A comma-separated list of PIN feature names the classifier must not use, or `none` to use all
41. Unset, the 20 features above are masked (`PinFeatureMask.DEFAULT_MASKED_FEATURES`). An
unknown name stops the search at startup.

The mask is applied **where the FDR stage loads features**, not at scoring:
`ParquetScoreCache.LoadPinFeaturesFromParquet` reads a masked feature as 0
(`ParquetScoreCache.cs:2228`), so the standardizer sees a constant and the classifier gets
nothing from it. The parquet always holds all 41, so:
- searches that differ only in the mask share their per-file scores. The mask is in the validity
  keys of FirstPassFDR, PerFileRescoring and SecondPassFDR (`;featmask=default|none|<names>`) but
  not of PerFileScoring, so pointing a new mask at existing scores re-runs only the FDR stages
  (minutes on three runs, against the whole search). See `14-intermediate-files.md`.
- the training export (`22-training-export.md`) and anything else reading the parquet see every
  feature unmasked.

#### The XIC count (`OSPREY_XIC_FRAGMENTS`)

How many of a candidate's most intense library fragments the search extracts XICs for: a whole
number from 2 to 20, default 6. The XICs feed CWT peak detection and the coelution, peak-shape
and median-polish features (0-5, 15, 16, 19, 20); the apex-match, per-fragment-rank and spectrum
features match library fragments in the spectra themselves and change only through the peak
that is picked. Calibration and the 2-of-top-6
prefilter keep 6 either way. Because the count changes the per-file scores, every task's
validity key carries it (`;xic=N`), so scores extracted at one count are never adopted at
another.

The count has to follow the instrument's resolution. At 20 ppm (Astral) a weak fragment's XIC
is mostly signal and more fragments help; at 0.4 Th (Stellar) each extra fragment window
collects mostly interference. The library's median is 14 fragments per precursor, and past 12 the
extra fragments are too weak to help.

| Astral, 21 features, SVM | per run at q 1% | first pass at FDP 0.5% |
|---|---|---|
| 6 XICs (default) | 82,449-84,342 | 83,561 |
| 8 XICs | 87,032-88,407 | 87,411 (+4.6%) |
| 12 XICs | 91,340-92,525 | 90,264 (+8.0%) |
| 16 XICs | 88,448-89,222 | lower than 12 |

#### Which settings for which data

All numbers below are experiment-level precursors on the three test runs of each dataset,
against natural (Arabidopsis) entrapment filed under its targets' proteins, with the
`transfer` second pass (`12-second-pass-fdr.md`), whose reported q stays calibrated. "FDP" is
the combined entrapment estimator; "at FDP x" counts precursors at the reported-q threshold
where the measured FDP reaches x. Astral is HRAM (20 ppm), Stellar unit resolution (0.4 Th),
ZT Scan HRAM demultiplexed data with a 9.08M-precursor library.

| Astral (HRAM) | at q 1% (FDP) | at FDP 0.5% |
|---|---|---|
| default: 21 features, SVM, 6 XICs | 93,528 (1.04%) | 83,544 |
| 21 features, SVM, 12 XICs | 100,071 (1.01%) | 90,238 (+8.0%) |
| 41 features, SVM, 12 XICs | 106,064 (1.04%) | 96,270 (+15.2%) |
| 41 features, GBDT, 12 XICs | 112,206 (1.04%) | 102,232 (+22.4%) |

| Stellar (unit resolution) | at q 1% (FDP) | at FDP 0.5% |
|---|---|---|
| default: 21 features, SVM, 6 XICs | 31,693 (0.71%) | 30,012 |
| 21 features, SVM, 12 XICs | 26,313 (0.65%) | 25,413 (-15%) |
| 41 features, SVM, 6 XICs | 33,120 (0.77%) | 31,542 (+5.1%) |
| 41 features, GBDT, 6 XICs | 37,971 (0.97%) | 35,562 (+18.5%) |
| 41 features, GBDT, 12 XICs | 31,000 (1.00%) | 29,516 (-1.7%) |

| ZT Scan (HRAM, demultiplexed) | at q 1% (FDP) | at FDP 0.5% | at FDP 1% |
|---|---|---|---|
| default: 21 features, SVM, 6 XICs | 29,236 (0.98%) | 20,450 | 29,370 |
| 41 features, SVM, 6 XICs | 31,944 (0.88%) | 28,004 (+37%) | 32,708 (+11%) |
| 41 features, SVM, 12 XICs | 30,811 (1.19%) | 21,501 | 28,826 |
| 41 features, GBDT, 6 XICs | 27,975 (0.82%) | 24,402 | 29,248 |
| 41 features, GBDT, 12 XICs | 33,200 (1.14%) | 28,915 | 32,561 |

On a Stellar window-width set (HeLa; 2 Th and 4 Th acquired, 4 Th 50%-overlap and SuperOverlap
demultiplexed; 3 replicates each), 41 features + GBDT + 6 XICs gained +5% to +10% over the default
on acquired data and +16% to +21% on demultiplexed data at FDP 0.5%: the features separate
demultiplexing interference from signal.

Recommendations from these measurements:
- **Features: use all 41 (`OSPREY_FEATURE_MASK=none`) on every kind of data.** They gain on all
  three datasets with every classifier and keep the reported q calibrated or conservative.
- **XIC count: 6 at unit resolution; 12 on HRAM data with GBDT.** 12 XICs cost Stellar 15% and
  gain Astral 8%. With the SVM on HRAM data the answer depends on the data: 12 gains on Astral
  but loses on ZT Scan (-7% at FDP 1% under the same C rule) and makes its first pass optimistic
  there (1.18% at q 1%), so 6 is the safer choice for the SVM until that is understood.
- **Classifier: GBDT (`OSPREY_FDR_MODEL=gbdt`, `07-fdr-control.md`) where it has enough
  confident training targets, the SVM where it does not.** GBDT adds 6-13% over the 41-feature
  SVM on Astral and Stellar, calibrated. On ZT Scan the very large library leaves only about 900
  of GBDT's 150,000 training targets at 1% FDR; there the 41-feature SVM is the best calibrated
  arm, and GBDT finds more only at 10x the training size, with an optimistic q (1.26% at q 1%)
  and 3.3 h of first-pass FDR against about 15 min.

Which family carries the gain depends on the data. Leaving one family out of the 41-feature SVM
(FDR stages only, on shared scores; change at FDP 0.5% / FDP 1%):

| left out | Astral, 12 XICs | Stellar, 6 XICs | ZT Scan, 6 XICs |
|---|---|---|---|
| runner-up (3) | -0.3% / -1.0% | +0.4% / -0.3% | -18.7% / -3.8% |
| context (3) | -0.6% / -0.9% | -2.1% / -2.9% | -4.2% / -1.5% |
| per-fragment-rank (12) | +0.9% / +0.2% | +0.2% / -0.5% | -12.1% / -4.0% |
| spectrum across the peak (2) | -2.9% / -3.4% | -0.9% / -0.5% | -7.6% / -1.1% |
| all 20 (the default mask) | -6.3% / -5.3% | -4.9% / -4.9% | -27% / -10.2% |

No family is needed everywhere and none costs identifications anywhere, so the recommendation is
all 41 rather than a per-instrument subset. Single-run differences under about 1% on Astral and
Stellar are within the run-to-run movement of the SVM's per-fold C pick
(`07-fdr-control.md`, "C selection"). On ZT Scan the C pick alone moves results by up to 14% at
FDP 0.5% and 3% at FDP 1%, because only about 750 training targets pass 1% FDR, so the ZT
per-family numbers are at about that noise level; the gain from all 20 together (+11% at FDP 1%)
is well above it.

---

## Flags and switches

Flags affecting THIS stage (defaults from `Osprey.Core/OspreyConfig.cs` /
`Osprey/OspreyCommandArgs.cs`):

| Flag / field | Default | Effect on scoring |
|--------------|---------|-------------------|
| `--resolution {hram\|unit}` | `hram` | Selects `UnitStrategy` vs `HramStrategy` (`ResolutionStrategy.cs:118`). Unit = 2000 f64 bins, no MS1 features; HRAM = ~100K sparse bins, MS1 features 13/14 active. **Calibration always uses unit bins regardless.** |
| `--fragment-tolerance <v>` | resolution-dependent | Fragment match window for apex-match, cosine, prefilter, LibCosine. |
| `--fragment-unit {ppm\|mz}` | `ppm` (unit-res forces `mz` 0.5) | Tolerance unit; also the reporting unit for mass-error features 9/10. |
| `--no-prefilter` (`PrefilterEnabled`) | `true` (prefilter ON) | When set, disables the 2-of-top-6-in-3-of-4-scans signal prefilter (`PeakDataExtractor.cs:117`). Prefilter is always skipped for boundary-override rescoring. |
| `--threads <count>` | all cores | INNER parallelism: `MaxDegreeOfParallelism` over isolation windows (`ScoringPipeline.cs:271`). Affects speed only, not results. |
| `--parallel-files [N]` | off (sequential) | OUTER parallelism across files; no effect on per-candidate feature values. |
| RT calibration `MinRtTolerance` / `MaxRtTolerance` / `FallbackRtTolerance` | config | Clamp the scan-window half-width; the Gaussian rank sigma uses the UNCLAMPED `5×MAD×1.4826` (`ScoringPipeline.cs:170-187`). |
| `Reconciliation.TopNPeaks` | config | How many CWT candidates `BuildFdrEntry` captures for Stage-6 (`CoelutionScorer.cs:322`). |
| `OSPREY_XIC_FRAGMENTS` (env) | unset = 6 | How many top library fragments get XICs (2-20). Changes peak picking and the XIC-based features; in every task's validity key. 6 for unit resolution, 12 for HRAM with GBDT (B.7). |
| `OSPREY_FEATURE_MASK` (env) | unset = features 21-40 masked | PIN features the FDR classifier must not use (comma-separated names, or `none`). Applied when the FDR stage loads features, so it does not change the scores parquet; in the FDR tasks' validity keys. `none` recommended on every dataset measured (B.7). |

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

- **[INTENTIONAL-CSHARP-DESIGN] Of the Rust engine's scores, only the 21 PIN features are
  computed; the ~26 non-PIN scores are not** - The Rust doc's "Spectral Scores at Apex (15
  features)" and "Co-Elution Features (11 features)" tables list `hyperscore`,
  `dot_product`(+`_smz`, `_top4..6`), `fragment_coverage`, `sequence_coverage`,
  `elution_weighted_cosine`, `fragment_coelution_min`, `n_fragment_pairs`,
  `fragment_corr_0..5`, etc. The C# calculator pass computes the 21 PIN features and none of
  those; the leftover fields still exist on `CoelutionFeatureSet` but are never populated by
  the scoring pass.
  Evidence: `OspreyFeatureCalculators.cs:36-44` ("Scores the Rust engine computes but
  excludes from its PIN features are intentionally NOT here");
  `Osprey.Core/CoelutionFeatureSet.cs:30-128` still declares the unused fields. The Rust doc
  itself notes "21 PIN features (out of ~47 computed)", so this is a deliberate port
  simplification, not a behavioral output difference.
  Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] 20 C#-only PIN features (21-40), masked by default** - the
  runner-up, context, per-fragment-rank and spectrum-across-peak families (B.7) have no Rust
  counterpart. They widen the parquet to 41 feature columns, but with the default
  `OSPREY_FEATURE_MASK` the classifier reads them as 0, so default results match the 21-feature
  engine. Evidence: `ParquetScoreCache.PIN_FEATURE_NAMES` (41 names), `PinFeatureMask`.
  Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] MS1 features gated by resolution strategy, not a config
  flag** - The Rust doc treats MS1 isotope/precursor-coelution as always-on when MS1
  scans exist. In C# they are strictly HRAM-only: `UnitStrategy.HasMs1Features` is
  `false` so the producer emits no MS1 chromatogram/envelope and features 13/14 are
  exactly 0.0.
  Evidence: `ResolutionStrategy.cs:133` (Unit false) vs `:184` (HRAM true);
  `Ms1Calculators.cs:37-38` ("HRAM-only"). This matches the Rust engine's behavior;
  the doc simply doesn't foreground the resolution gate. Severity: info.

Otherwise the C# main-search feature computation matches the Rust documentation step
for step: the pipeline order (calibrated local spectra → per-window parallel scoring →
prefilter → CWT peak ranking with the Gaussian RT penalty → PIN-feature calculator pass
→ dedup), the Comet XCorr preprocessing invariants (no theoretical preprocessing;
window normalization to 50 at 5% threshold; sliding-window offset 75; scale 0.005), the
closest-peak-by-mz tie-breaks, the last-on-tie reference-XIC selection, the calibration
4-feature LDA with decoy-favoring ties at 1% FDR, and the shared
`compute_features_at_peak`/`run_search` boundary-override design (see
`11-boundary-overrides.md`) were all verified against the C# source.
