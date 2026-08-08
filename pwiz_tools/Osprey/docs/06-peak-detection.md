# 06. CWT Peak Detection (C#)

> Pipeline stage: Stage 4 (main first-pass search). C# port of Rust docs/05-peak-detection.md. Corresponds to Rust osprey CWT consensus peak detection (`osprey-chromatography/src/cwt.rs`, `lib.rs`, `pipeline.rs::run_search`).

This document describes how the C# Osprey port detects chromatographic peaks in
fragment-ion chromatograms (XICs), selects the winning peak among candidates, and
turns that peak into the boundaries, apex, area, and S/N that feed FDR scoring
(`see 03-spectral-scoring.md`) and, ultimately, the .blib output (`see
13-blib-output-schema.md`). It also folds in the intent of the historical Rust
design spec `docs/DIA-PeakDetectionPlan.md` as context and calls out where that
early plan was superseded.

The port keeps the pipeline byte-identical to Rust on the Stellar
(`--resolution unit`) and Astral (`--resolution hram`) reference datasets, so
almost every routine below is a deliberate line-for-line translation, including
the tie-break and float-order details that make cross-impl parity hold. Those
details are noted where they matter.

---

## Where this runs in the pipeline

Peak detection is invoked per isolation window, per candidate precursor, from the
coelution scoring engine:

- `Osprey.Scoring/ScoringPipeline.cs` computes the per-file RT tolerance and RT
  penalty sigma once, then dispatches windows in parallel to
  `CoelutionScorer.ScoreWindow` (`ScoringPipeline.cs:277`).
- `Osprey.Scoring/CoelutionScorer.cs::ScoreWindow` (`CoelutionScorer.cs:70`)
  sorts the window spectra, finds candidate library entries, preprocesses XCorr,
  and calls `ScoreCandidate` for each candidate.
- `Osprey.Scoring/PeakDataExtractor.cs::TryExtract` (`PeakDataExtractor.cs:63`) is
  the detection half of Rust `run_search`: it picks the scan range, extracts
  XICs, runs the 3-tier peak detector, rank-selects the winning peak, and
  publishes the peak-data view.
- The wavelet math lives in `Osprey.Chromatography/CwtPeakDetector.cs`; the
  legacy Savitzky-Golay detector and shared area/SNR helpers live in
  `Osprey.Chromatography/PeakDetector.cs`; the additive-model decomposition lives
  in `Osprey.Scoring/TukeyMedianPolish.cs`.

The same peak-detection code is also used by the calibration pass (`see
04-calibration.md`) to find calibration matches, and by Stage 6 reconciliation
re-scoring through the boundary-override path (`see 11-boundary-overrides.md`).

---

## Step 0: RT search window vs apex acceptance (the decouple)

`PeakDataExtractor.FindScanRange` (`PeakDataExtractor.cs:491`) chooses which
scans feed the detector. For the normal (non-override) search path the
extraction half-width is deliberately **wider** than the apex-acceptance
tolerance:

```
xicHalfWidth = rtTolerance + max(rtTolerance, 0.1)   // PeakDataExtractor.cs:518
scan i is in range when |windowRts[i] - expectedRt| <= xicHalfWidth
```

`expectedRt` is the calibration-predicted RT (`rtCalibration.Predict`,
`PeakDataExtractor.cs:97`). `rtTolerance` is the per-file global tolerance
`3 * MAD * 1.4826` clamped to `[MinRtTolerance, MaxRtTolerance]` = `[0.5, 3.0]`
min (`RTCalibration.SearchWindowHalfWidth`, `RTCalibration.cs:318`;
`ScoringPipeline.cs:175`).

The apex-acceptance filter is applied later, during rank scoring: a detected
peak whose apex RT lies farther than `rtTolerance` from `expectedRt` is dropped
(`PeakDataExtractor.cs:256-259`). This is exactly the decouple the Rust doc
describes: the wider window lets CWT see both tails of a peak whose apex has
drifted toward the acceptance edge (so boundaries are not truncated to
`start == apex`), while the apex filter preserves first-pass selectivity. The C#
comment at `PeakDataExtractor.cs:248-255` cites the same Rust commit (885339b)
and rationale.

The comparison uses the abs-diff form `Math.Abs(windowRts[i] - expectedRt) <=
xicHalfWidth`, not a precomputed `rtHi` compare, because the two arithmetic
chains round differently in the last bit and a single boundary spectrum slipping
in or out of the window cascades through CWT detection into a different apex pick
(`PeakDataExtractor.cs:483-489`).

Before detection, a signal pre-filter (gated by `PrefilterEnabled`, default true)
requires at least 2 of the top-6 fragments present in at least 3 of 4 consecutive
scans, skipping noise-only candidates (`PeakDataExtractor.cs:117-143`). It is
bypassed on the boundary-override path.

---

## Step 1: Fragment XIC extraction

`TopFragmentExtractor.ExtractFragmentXics` (`PeakDataExtractor.cs:146`) extracts
the top fragment XICs within `[startScan, endScan]` using the configured fragment
tolerance. Each XIC is an `Osprey.Chromatography/CwtPeakDetector.cs::XicData`
holding a fragment index and parallel `RetentionTimes` / `Intensities` arrays on
the shared window scan axis. Fewer than 2 XICs aborts the candidate
(`PeakDataExtractor.cs:162`).

---

## Step 2: CWT consensus peak detection

`CwtPeakDetector.DetectConsensusPeaks(xics, minConsensusHeight = 0.0)`
(`CwtPeakDetector.cs:511`) is the primary detector. It requires at least 2 XICs
of at least 5 scans each, all the same length, and at least one non-zero
intensity, else returns an empty list (`CwtPeakDetector.cs:515-547`).

### 2a. Scale estimation

`EstimateScale` (`CwtPeakDetector.cs:161`) measures the FWHM of each fragment XIC
that has signal (apex found via last-on-tie `>=`, half-max crossings via linear
interpolation), takes the median FWHM, and converts to a Gaussian sigma:

```
sigma = medianFWHM / 2.355        (FWHM_TO_SIGMA = 2.355)
clamp to [MIN_SCALE=2.0, MAX_SCALE=20.0]
fallback DEFAULT_SCALE = 4.0 when no FWHM can be measured
```

The apex uses `>=` (last equal element) deliberately to match Rust
`Iterator::max_by` tie semantics; a strict `>` would diverge on flat tops
(`CwtPeakDetector.cs:171-189`).

### 2b. Mexican Hat kernel

`MexicanHatKernel(sigma, kernelRadius)` (`CwtPeakDetector.cs:85`) builds the
Ricker wavelet — the negative normalized second derivative of a Gaussian:

```
psi(t) = (2 / (sqrt(3*sigma) * pi^(1/4))) * (1 - (t/sigma)^2) * exp(-t^2 / (2*sigma^2))
```

followed by a zero-mean (DC-offset) correction so the kernel gives zero response
to a constant signal (`CwtPeakDetector.cs:99-107`). Kernel radius is
`min(ceil(5*sigma), nScans/2)` (`CwtPeakDetector.cs:551`), so the ~99.99%
energy-capture radius is capped by the available scans. (`MIN_KERNEL_POINTS = 11`
is declared but the detector uses the radius formula directly.)

### 2c. Convolution and pointwise-median consensus

Each XIC is convolved with the kernel using direct "same"-size, zero-padded
convolution (`Convolve`, `CwtPeakDetector.cs:122`). `ConsensusMedianCwt`
(`CwtPeakDetector.cs:265`) then takes the pointwise **median** of the per-fragment
CWT coefficients at each scan. The median (not mean) is the interference-rejection
mechanism: a peak-like shape must be present in the majority of transitions for
the consensus to rise.

### 2d. Apex detection and boundaries (zero-crossing ±2σ with valley guard)

`FindPeaks` (`CwtPeakDetector.cs:298`) collects consensus local maxima above
`minConsensusHeight` (interior plus both endpoints), then sorts them by consensus
coefficient descending via `OrderByDescending` — LINQ's stable sort is used
specifically because `Array.Sort` is an unstable introsort and would reorder ties
relative to Rust's stable `slice::sort_by` (`CwtPeakDetector.cs:334-344`).

For each apex:

1. Walk out to zero-crossings of the consensus signal (`leftZc`, `rightZc`;
   `CwtPeakDetector.cs:353-359`). For a Gaussian these sit near ±σ (~68% area).
2. Estimate asymmetric sigma from the zero-crossing distances and set targets
   `apex ± COVERAGE_FACTOR * max(sigma, 1)` where `COVERAGE_FACTOR = 2.0`, giving
   ~95% coverage (`CwtPeakDetector.cs:362-372`).
3. **Valley guard**: extend from each zero-crossing toward the target while
   tracking a running minimum of the raw reference signal; if the reference rises
   more than `VALLEY_THRESHOLD = 0.05 * refApex` above that running minimum, stop
   at the valley minimum — this prevents bleeding into an adjacent peak
   (`CwtPeakDetector.cs:374-416`).
4. Require at least 3 scans (`CwtPeakDetector.cs:419`).
5. Recompute the apex as the max of the **reference signal** within the
   boundaries (last-on-tie `>=`), and compute `area` (trapezoidal) and
   `signal_to_noise` from that reference signal (`CwtPeakDetector.cs:430-445`).

The reference signal here is the **sum** of the raw fragment intensities
(`refSignal`, built at `CwtPeakDetector.cs:562-568`). Consensus CWT coefficients
are used only for detection and boundaries; all quantitative values come from raw
intensities. Peaks are returned sorted by consensus coefficient descending.

The output type is `Osprey.Core/XICPeakBounds.cs` (`apex_rt`, `apex_intensity`,
`apex_index`, `start_rt`, `end_rt`, `start_index`, `end_index`, `area`,
`signal_to_noise`) — field-for-field the Rust `XICPeakBounds` struct.

---

## Step 3: Fallback chain

`PeakDataExtractor.DetectCandidatePeaks` (`PeakDataExtractor.cs:543`) applies the
three-tier priority the Rust doc lists, in order:

1. CWT consensus (`DetectConsensusPeaks`, above).
2. If empty: peak detection on the Tukey median-polish elution profile via
   `PeakDetector.DetectAllXicPeaks(profile, 0.01, 5.0)`
   (`PeakDataExtractor.cs:566-583`).
3. If still empty: `DetectAllXicPeaks` on the highest-total-intensity fragment XIC
   (`PeakDataExtractor.cs:585-602`).

`DetectAllXicPeaks` (`PeakDetector.cs:334`) is the legacy Savitzky-Golay detector:
5-point SG smoothing `[-3,12,17,12,-3]/35` with negative clamp
(`SmoothSavitzkyGolay`, `PeakDetector.cs:296`), local maxima above `minHeight`
(stable `OrderByDescending`), DIA-NN-style valley boundary walks
(`WalkBoundaryLeft/Right`, `PeakDetector.cs:452,480`: stop at 20%-of-signal
threshold or at a valley below 50% of apex and 50% of the rising neighbor), and
asymmetric FWHM capping at `apex ± 2 * half-width` (`ComputeAsymmetricHalfWidths`,
`PeakDetector.cs:515`). S/N uses 5 raw flanking points each side
(`ComputeSnr`, `PeakDetector.cs:224`).

---

## Step 4: Reference XIC selection

The reference XIC used for apex/area/SNR recompute and for the reconciliation
CWT-candidate list is the single fragment with the highest total intensity,
selected with last-on-tie `>=` (`PeakDataExtractor.cs:204-213`). The `>=` is
load-bearing for parity: Rust's `max_by` returns the last equal element, and
using strict `>` here diverged ~33k Stellar entries on tied fragments.

---

## Step 5: Peak selection among candidates (RT-penalized rank score)

Detection returns candidate peaks; selection picks one. For each candidate peak
that passes the apex-acceptance filter (Step 0), `PeakDataExtractor.cs:262-289`
computes:

```
coelutionScore = mean pairwise Pearson correlation of fragment intensities
                 within [start_index, end_index]              (PeakDataExtractor.cs:262-278)
rtPenalty      = exp(-(rtResidual^2) / (2 * rtSigma^2))       (PeakDataExtractor.cs:280)
intensityWeight= ln(1 + refApexIntensity)                     (PeakDataExtractor.cs:287)
rankScore      = coelutionScore * rtPenalty * intensityWeight (PeakDataExtractor.cs:289)
```

The highest `rankScore` wins, compared with IEEE-754 total-order semantics
(`TotalOrder.Greater`, `PeakDataExtractor.cs:300`) so signed-zero ties (when
`intensityWeight = 0`) resolve the same way Rust's `f64::total_cmp` resolves them.

This is where the C# implementation (matching the current Rust *code*) is richer
than the Rust *doc*: the doc's Stage 2 describes selection as pure mean pairwise
correlation. The C# adds:

- a **Gaussian RT penalty** so a strong interferer at the wrong RT cannot beat the
  correct peak on coelution alone (this penalty is the peak-selection quality that
  the Rust CLAUDE.md notes is critical for protein-FDR calibration), and
- a **log-intensity tiebreaker** so the main peak wins over its own shoulder when
  coelution scores are nearly equal.

The RT penalty sigma is computed once per file as **unclamped** `5 * MAD * 1.4826`
with a 0.1 min floor (`ScoringPipeline.cs:177`), distinct from the scan-window
`rtTolerance` which is `3 * MAD * 1.4826` clamped to `[0.5, 3.0]`. Keeping the two
values separate is what makes ranking bit-identical to Rust regardless of the
config clamp (`ScoringPipeline.cs:150-157`).

### Post-selection apex/area/SNR recompute

For non-override entries the winning peak's apex is recomputed over
`ref_xic[start..end]` (the single highest-intensity fragment, last-on-tie `>=`),
and `area` / `signal_to_noise` are recomputed from that same slice
(`PeakDataExtractor.cs:375-420`). This discards the consensus-signal apex (which
used the summed reference) so the persisted `bounds_area` / `bounds_snr` stay
consistent with the ref-XIC boundary — the C# comment documents ~32k `peak_apex`
and ~560 `bounds_area` rows that diverged before this recompute was added.

### Calibration-path minimum correlation

In the calibration pass, `Osprey.Tasks/Calibrator.cs:54` defines
`MIN_COELUTION_CORR_SCORE = 0.5`: a calibration match is rejected if no candidate
peak reaches that mean-correlation floor, matching the Rust `batch.rs` threshold.
The main first-pass search (`PeakDataExtractor`) does not apply a hard correlation
floor at detection — coelution enters the SVM as feature 0.

### Optional: learned linear pick model (opt-in)

The product-form `rankScore` above is the **default** pick — it is the
Rust-parity / regression-golden path, so it is what the reference datasets are
frozen against. An opt-in alternative replaces that product with a **frozen
standardized linear model** over the same terms plus the per-candidate
median-polish library cosine (`PickLdaModel.Score`, `PeakDataExtractor.cs:330`;
`Osprey.Scoring/PickLdaModel.cs`):

```
rank = w0*z(coelution) + w1*z(ln_intensity) + w2*z(rt_penalty) + w3*z(median_polish)
z(x) = (x - mean) / scale          (a zero scale contributes 0 for that term)
```

The argmax and `TotalOrder` tie-break are unchanged, so only the ranking scalar
differs. Selection precedence, resolved once at process start
(`Osprey.Core/OspreyEnvironment.cs`; `PeakDataExtractor.cs:231-239`):

1. `OSPREY_PICK_LDA_MODEL` set to an existing JSON file → that model (test / retrain override);
2. else `OSPREY_PICK_LDA` is not `0` (**default**) → the hardcoded resolution-keyed model
   (`PickLdaModel.ForResolution` — Stellar weights for unit resolution, Astral for HRAM);
3. else (`OSPREY_PICK_LDA=0`) → the legacy product pick.

A model JSON must list `features` as `[coelution, ln_intensity, rt_penalty,
median_polish]` in that exact order (positional weights; `LoadFromEnv` throws a
`FormatException` otherwise). Models are trained offline from an
`OSPREY_PICK_DUMP_CANDIDATES` per-candidate capture — see
[peak-model-training.md](peak-model-training.md) for the capture → train →
promote recipe and the score equations. The Rust reference carries the identical
model and loader (`crates/osprey/src/pick_lda.rs`), kept in lock-step for parity.

---

## Step 6: Tukey median polish (scoring features)

After the winning peak is chosen, `CoelutionScorer.ScoreCandidate` crops the XICs
to `[start..end]` and runs `TukeyMedianPolish.Compute(peakXics, peakRts,
maxIter = 10, tol = 0.01)` (`CoelutionScorer.cs:242`).

`TukeyMedianPolish.Compute` (`TukeyMedianPolish.cs:103`) decomposes the
`fragment × scan` matrix in ln space into `overall + rowEffects + colEffects +
residuals`, treating zero-intensity cells as NaN (missing) via `NanMedian`. Each
iteration does a row sweep then a column sweep, updating `overall`, and checks
`max|new - old| < tol` after both sweeps (`TukeyMedianPolish.cs:146-237`). The
linear-space elution profile is `exp(overall + colEffects[s])`.

Four PIN features are derived from the decomposition (indices per
`CoelutionScorer.cs:269-273`, computed by the `OspreyFeatureCalculators`):

- `median_polish_cosine` — `LibCosine` (`TukeyMedianPolish.cs:291`): sqrt-space
  cosine of row effects vs library fragment intensities.
- `median_polish_residual_ratio` — `ResidualRatio` (`TukeyMedianPolish.cs:344`):
  `sum|obs - pred| / sum(obs)` in linear space.
- `median_polish_min_fragment_r2` — `MinFragmentR2` (`TukeyMedianPolish.cs:385`).
- `median_polish_residual_correlation` — `ResidualCorrelation`
  (`TukeyMedianPolish.cs:428`): mean pairwise Pearson of residuals.

In this C# port the median polish is used **only** for these scoring features (and
as fallback-detector input in Step 3). It does **not** supply the peak boundaries
written to output — those come from the CWT/detected peak (see Step 7 and the
divergences).

---

## Step 7: Building the FdrEntry (boundaries, area, S/N, candidates)

`CoelutionScorer.BuildFdrEntry` (`CoelutionScorer.cs:395`) assembles the
`FdrEntry` from the winning peak:

- `StartRt` / `EndRt` = `windowRts[startScan + bestPeak.Start/EndIndex]`
  (`CoelutionScorer.cs:462-463`) — i.e. the **CWT/detected peak boundaries**.
- `ApexRt` / `ScanNumber` = the apex spectrum's RT and scan.
- `BoundsArea` = `bestPeak.Area`, `BoundsSnr` = `bestPeak.SignalToNoise`
  (`CoelutionScorer.cs:473-474`).
- `CoelutionSum` and `Score` are seeded from feature 0 (mean pairwise coelution).
- `FragmentMzs` / `FragmentIntensities` serialize the **full** library fragment
  list, and `ReferenceXic{Rts,Intensities}` slice the reference XIC across
  `[start..end]` for the reconciled `.scores.parquet` write-back.

### Stage 6 CWT-candidate capture

`CoelutionScorer.CaptureCwtCandidates` (`CoelutionScorer.cs:321`) keeps the
top-`N` peaks (`ReconciliationConfig.TopNPeaks`, default 5;
`ReconciliationConfig.cs:36`) ranked by penalized `rankScore`, each with its
apex/area/SNR recomputed over the reference-XIC slice, storing the **raw**
coelution score (not the RT-penalized rank) as `CoelutionScore`. These become the
`CwtCandidate` list (`Osprey.Core/CwtCandidate.cs`) serialized into the parquet
`cwt_candidates` column (6 little-endian f64 per candidate,
`CwtCandidateCodec`) for cross-run reconciliation (`see
10-cross-run-reconciliation.md`). The override path leaves this list empty,
matching Rust.

---

## Boundary-override path (Stage 6 re-scoring)

When `context.BoundaryOverrides` supplies an `(apex, start, end)` RT triple for a
candidate (`PeakDataExtractor.cs:87-92`), peak detection and the signal pre-filter
are skipped. `FindScanRange` uses the override bounds plus a margin
(`max(0.2, peakWidth)` each side) for SNR context (`PeakDataExtractor.cs:499-514`),
and `BuildOverridePeaks` (`PeakDataExtractor.cs:614`) constructs a single synthetic
`XICPeakBounds` by mapping the RT triple onto the reference-XIC axis via
lower-bound binary search with Rust `partition_point` / `saturating_sub`
semantics. Override entries also skip the apex-acceptance filter and the
post-selection apex recompute. See `11-boundary-overrides.md`.

---

## MS1 data production (HRAM only)

On HRAM runs `PeakDataExtractor.ProduceMs1Data` (`PeakDataExtractor.cs:697`)
builds the precursor chromatogram (nearest-MS1 sampling, missing scans skipped),
its co-sampled reference fragment chromatogram, and the apex isotope envelope,
feeding the `ms1_precursor_coelution` and `ms1_isotope_cosine` features. On
unit-resolution runs no MS1 data is produced and both features evaluate to 0.0
(`context.Resolution.HasMs1Features` gate, `PeakDataExtractor.cs:453`).

---

## Flags and switches

| Flag / config / env var | Default | Effect on this stage |
|---|---|---|
| `--no-prefilter` (`OspreyConfig.PrefilterEnabled`) | `true` (prefilter on) | When on, the 2-of-top-6-in-3-of-4-scans signal pre-filter (`PeakDataExtractor.cs:117`) drops noise-only candidates before XIC extraction. `--no-prefilter` scores every candidate. |
| `--fragment-tolerance` / `--fragment-unit` | `ppm`; unit-resolution forces `mz 0.5` | Fragment match tolerance for XIC extraction and the pre-filter. |
| `--resolution {unit\|hram\|auto}` | `auto` | Selects the resolution strategy; HRAM enables the MS1 chromatogram/isotope features produced here, unit resolution disables them. |
| `MinRtTolerance` / `MaxRtTolerance` (`RTCalibrationConfig`) | `0.5` / `3.0` min | Clamp on the scan-window `rtTolerance = 3*MAD*1.4826` (`RTCalibration.SearchWindowHalfWidth`). The XIC extraction half-width is `rtTolerance + max(rtTolerance, 0.1)`; the apex-acceptance filter uses `rtTolerance`. The RT-penalty sigma (`5*MAD*1.4826`, floor 0.1) is deliberately **not** clamped. |
| `FallbackRtTolerance` (`RTCalibrationConfig`) | used when no RT calibration | Sets both `rtTolerance` and `rtSigma` when calibration is absent (`ScoringPipeline.cs:185-186`). |
| `ReconciliationConfig.TopNPeaks` | `5` | Number of CWT candidates captured per entry for reconciliation (`CoelutionScorer.cs:328`). `0` captures none. |
| `--task PerFileRescoring` (Stage 6) | off | Activates the boundary-override path: detection skipped, bounds taken from the supplied triple (`see 11-boundary-overrides.md`). |
| `OSPREY_PICK_LDA` | **on** (learned pick) | The hardcoded resolution-keyed learned linear model (`PickLdaModel.ForResolution`) is the default pick. Set `OSPREY_PICK_LDA=0` for the legacy product form, which is how the A/B stays available. |
| `OSPREY_PICK_LDA_MODEL` | unset | Path to a frozen JSON pick model that overrides the built-in model; its `features` order is validated on load. See [peak-model-training.md](peak-model-training.md). |
| `OSPREY_PICK_DUMP_CANDIDATES` | unset | Dumps one row per CWT candidate (the raw pick terms) to `<stem>.pick_candidates.tsv` for offline pick-model training. Byte-identical / zero cost when unset. |
| `OSPREY_DIAG_XIC_ENTRY_ID` / `OSPREY_DIAG_XIC_PASS` | unset | Dumps the per-entry search XIC and CWT peak list (`PeakDataExtractor.cs:154-160`, `312-347`). |
| `OSPREY_DUMP_CWT_PATH` (CWT-path row) | unset | Emits detection counters (n CWT peaks, n scored, success) per entry (`PeakDataExtractor.cs:466-471`). |
| `OSPREY_TRACE_PEPTIDE` | unset | Per-peptide `[trace]` lines through CWT scoring and downstream stages (`see 18-peptide-trace.md`). |

Compile-time constants (not CLI-exposed): `COVERAGE_FACTOR = 2.0`,
`VALLEY_THRESHOLD = 0.05`, `MIN_SCALE = 2.0`, `MAX_SCALE = 20.0`,
`DEFAULT_SCALE = 4.0`, `FWHM_TO_SIGMA = 2.355` (`CwtPeakDetector.cs:44-69`);
SG `peak_boundary = 5.0` and `minHeight = 0.01` in the fallback calls
(`PeakDataExtractor.cs:581,601`).

---

## Testing

- `Osprey.Test/ChromatographyTest.cs` — kernel zero-mean / symmetry / positive
  center, convolution same-length and delta response, scale estimation (known
  peak and all-zero fallback to 4.0), consensus single Gaussian, two separated
  peaks, and interference rejection; trapezoidal area and the simple
  `PeakDetector.Detect`.
- `Osprey.Test/PeakDetectorTest.cs` — `Detect` end-of-series and min-width,
  `FindBestPeak`, the Savitzky-Golay smoother (constant preservation, negative
  clamp, short-series passthrough), and the full `DetectAllXicPeaks` valley/FWHM
  path.

These are MSTest ports of the Rust `#[cfg(test)]` cases listed in the Rust doc.
The standing cross-impl parity gate (`regression.ps1`, `see 19-testing.md`)
compares Stage 7 output and the .blib, which transitively exercises this stage on
the reference datasets.

---

## Divergences from the Rust documentation

- **[INTENTIONAL — now the default in both implementations] Learned linear pick
  model** - Beyond the product-form pick the Rust algorithm doc describes, both
  implementations carry a frozen linear pick model (`OSPREY_PICK_LDA` /
  `OSPREY_PICK_LDA_MODEL`; `Osprey.Scoring/PickLdaModel.cs`, Rust
  `crates/osprey/src/pick_lda.rs`, kept in lock-step). It shipped opt-in and is
  now **on by default in both**, so the product pick is reached only via
  `OSPREY_PICK_LDA=0`, which is how the A/B stays available. It substitutes only
  the ranking scalar (argmax + `TotalOrder` tie-break unchanged). The Rust
  `docs/05-peak-detection.md` predates this feature. Trained offline — see
  [peak-model-training.md](peak-model-training.md). Evidence:
  `Osprey.Scoring/PeakDataExtractor.cs:231-239,330`. Severity: info.

- **[STALE-RUST-DOC] Peak selection uses an RT-penalized rank score, not pure
  correlation** - Rust doc §"Peak Selection" (Stage 2) says the peak with the
  highest mean pairwise fragment correlation wins. C# ranks by
  `coelution * exp(-dt^2 / (2*sigma^2)) * ln(1 + apex_intensity)` — a Gaussian RT
  penalty and a log-intensity tiebreaker on top of correlation. The C# comments
  attribute this to Rust `pipeline.rs:6685-6760` (RT penalty) and commit 4d0119d
  (intensity tiebreaker), and the Rust CLAUDE.md itself calls the RT penalty
  critical for protein-FDR calibration, so the Rust *code* has this and the doc is
  behind. Evidence: `Osprey.Scoring/PeakDataExtractor.cs:289`. Severity: minor.

- **[STALE-RUST-DOC] Median-polish iteration/convergence parameters** - Rust doc
  §"Tukey Median Polish / Algorithm" specifies 20 iterations and a `1e-4`
  convergence tolerance. The C# main-search call site uses `maxIter = 10`,
  `tol = 0.01`, with a comment that this "Matches Rust pipeline.rs:5198-5212"
  (i.e. the Rust code was changed and the doc's 20 / 1e-4 is the old
  default). The `Compute` method's own XML default is still documented as
  20 / 1e-4, but the actual call overrides it. Evidence:
  `Osprey.Scoring/CoelutionScorer.cs:242`; defaults at
  `Osprey.Scoring/TukeyMedianPolish.cs:100-101`. Severity: minor.

- **[UNVERIFIED] Blib peak boundaries come from the CWT/detected peak, not
  median-polish FWHM** - Rust doc §"Peak Boundaries for Blib Output" gives a
  priority list where Tukey median-polish FWHM (`apex ± 1.96σ`) is priority 1 and
  CWT boundaries are the fallback. In the C# scoring path the `FdrEntry.StartRt /
  EndRt` written for output are the CWT/detected `bestPeak` boundaries; median
  polish is consumed only for the four scoring features, never for output bounds.
  Evidence: `Osprey.Scoring/CoelutionScorer.cs:462-463` (bounds from `bestPeak`);
  median-polish use confined to `CoelutionScorer.cs:242,269-273`. Because the
  README asserts the .blib is cross-impl bit-identical, the Rust code most likely
  also writes CWT bounds (making the doc stale), but the C# BLIB writer
  (`Osprey.IO/BlibWriter.cs`) was not read here to confirm no median-polish
  boundary is re-derived downstream. A human should confirm the RetentionTimes
  `startTime`/`endTime` source in the blib writer. Severity: minor.

- **[STALE-RUST-DOC] Scale-space / ridge-tracking search is aspirational (design
  spec)** - The historical `DIA-PeakDetectionPlan.md` §5.2 proposes wrapping
  detection in a loop over scales `[2, 4, 8, 16]` with ridge tracking across
  scales, and §Phase 2 proposes an `S/N < 3` consensus threshold. Neither is
  implemented: C# estimates a **single** data-driven scale from the median FWHM
  (`CwtPeakDetector.EstimateScale`) and calls `DetectConsensusPeaks` with
  `minConsensusHeight = 0.0` (accept any positive consensus peak). The later
  `05-peak-detection.md` doc already reflects the single-scale reality; only the
  earlier plan is aspirational. Evidence: `Osprey.Chromatography/CwtPeakDetector.cs:161,551`;
  call at `Osprey.Scoring/PeakDataExtractor.cs:560`. Severity: info.

- **[STALE-RUST-DOC] No linear-baseline background subtraction on integrated
  area** - `DIA-PeakDetectionPlan.md` §4.4 (Quantitation) proposes trapezoidal AUC
  minus a linear baseline connecting the start/end intensities. The C# area is a
  plain trapezoidal integral of the raw reference signal with no baseline
  subtraction (`PeakDetector.TrapezoidalArea`), consistent with the current
  `05-peak-detection.md` guidance that XIC area is a relative measure and must not
  be used for quantification (Skyline quantifies within the provided boundaries).
  Evidence: `Osprey.Chromatography/PeakDetector.cs:178-191`. Severity: info.

- **[STALE-RUST-DOC] Consensus aggregation is median, not the sum shown in the
  plan's sample code** - `DIA-PeakDetectionPlan.md` §4.3 sums per-transition CWTs
  in its illustrative Rust (`consensus = consensus + cwt`), noting in a comment
  that the real implementation should use the median. C# uses the pointwise median
  (`ConsensusMedianCwt`), matching the production `05-peak-detection.md` doc.
  Evidence: `Osprey.Chromatography/CwtPeakDetector.cs:265`. Severity: info.

Otherwise the C# implementation matches the Rust `05-peak-detection.md`
description step for step: the Mexican Hat kernel formula and zero-mean
correction, data-driven scale with the same clamps and 4.0 fallback, same-size
zero-padded convolution, pointwise-median consensus, zero-crossing + ±2σ boundary
extension with the 5%-of-apex valley guard, reference-signal apex/area/S/N,
`XICPeakBounds` output fields, the 3-tier fallback chain, the legacy SG detector
(SG smoothing, valley walks, asymmetric FWHM capping, flanking-point S/N), the
median-polish decomposition and its four features, and the extraction-vs-apex
window decouple with its wider extraction window and post-detection apex filter.
The parity-critical tie-breaks (last-on-tie `>=` for apex/reference selection,
stable `OrderByDescending` for peak sorting, `TotalOrder` compares for signed-zero
ranks) are all present and documented in-code.
