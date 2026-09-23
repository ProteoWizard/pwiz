# 02. XCorr Scoring (C#)

> Pipeline stage: Stage 3/4 (XCorr primitive). C# port of Rust docs/04-xcorr-scoring.md. Corresponds to Rust osprey XCorr scoring.

XCorr (cross-correlation) is the Comet/SEQUEST spectral-similarity primitive. In
the C# port it is implemented in `Osprey.Scoring/SpectralScorer.cs` and consumed
in two places: the calibration candidate scorer (`Osprey.Tasks/Calibrator.cs`,
always unit-resolution bins) and the apex-spectrum feature calculators
(`Osprey.Scoring/XcorrCalculators.cs`, feature 6 `xcorr` and feature 17
`sg_weighted_xcorr`). The arithmetic matches Comet's fast-XCorr exactly; the
port's only real differences are infrastructure (managed scalar loops instead of
BLAS, and a sparse HRAM cache instead of a dense `float[]`).

The scoring form is the same as Comet's: **the experimental spectrum is
preprocessed (bin + sqrt + windowing + flanking subtraction); the theoretical
spectrum is never preprocessed** — XCorr simply sums the preprocessed
experimental values at the library fragment bin positions and scales by `0.005`.

---

## Step 0: Binning parameters (`Osprey.Core/BinConfig.cs`)

Both spectra are binned with Comet's `BIN` macro, implemented as
`BinConfig.MzToBin` (`BinConfig.cs:96`):

```csharp
public readonly int MzToBin(double mz)
{
    return (int)(mz * InverseBinWidth + OneMinusOffset);
}
```

`OneMinusOffset = 1.0 - BinOffset`, so this is `(int)(mz / bin_width + (1 -
offset))`, identical to the Rust `BIN` macro.

| Parameter | Unit resolution (`BinConfig.UnitResolution`, `BinConfig.cs:42`) | HRAM (`BinConfig.HRAM`, `BinConfig.cs:73`) |
|-----------|------------------------------------|--------------------------------------------|
| `BinWidth` | `1.0005079` Da | `0.02` Da |
| `BinOffset` | `0.4` (so `OneMinusOffset = 0.6`) | `0.0` (so `OneMinusOffset = 1.0`) |
| `MaxMz` | `2000.0` | `2000.0` |
| `NBins` | `(int)(2000 * (1/1.0005079) + 0.6) + 1 = 2000` | `(int)(2000/0.02 + 1.0) + 1 = 100002` |

`BinConfig.ForResolution(ResolutionMode)` (`BinConfig.cs:65`) selects HRAM vs
unit; everything else in this stage is resolution-agnostic once the `BinConfig`
is chosen. See `04-calibration.md` for how `--resolution auto` resolves to a
`ResolutionMode`.

---

## Step 1: Experimental spectrum preprocessing (`SpectralScorer.cs`)

The full Comet fast-XCorr preprocessing pipeline lives in `XcorrAtScan`
(`SpectralScorer.cs:396`) and its cache-building siblings. The five documented
steps map 1:1 onto the C# code, all in `f64`:

### 1a. Bin + square-root transform (`SpectralScorer.cs:419`)

```csharp
for (int i = 0; i < spectrum.Mzs.Length; i++)
{
    int bin = _binConfig.MzToBin(spectrum.Mzs[i]);
    if (bin >= 0 && bin < n)
        binned[bin] += Math.Sqrt(spectrum.Intensities[i]);   // sqrt, accumulated
}
```

Intensities are `float`; `Math.Sqrt` widens to `double` first, matching Rust
`(intensity as f64).sqrt()` (comment at `SpectralScorer.cs:616-618`). Multiple
peaks in the same bin **accumulate** (`+=`).

### 1b. Windowing normalization (`ApplyWindowingNormalizationD`, `SpectralScorer.cs:522`)

Comet's `MakeCorrData`: 10 equal-width windows, each normalized so its max
becomes `50.0`, with a global 5%-of-base-peak noise floor.

```csharp
const int numWindows = 10;
int windowSize = (n / numWindows) + 1;
double threshold = globalMax * 0.05;               // 5% noise floor
...
double normFactor = 50.0 / windowMax;
if (spectrum[i] > threshold)
    result[i] = spectrum[i] * normFactor;          // else stays 0
```

Constants (`10`, `50.0`, `0.05`) and the `(n/10)+1` window size match the Rust
`apply_windowing_normalization` exactly.

### 1c. Flanking-bin subtraction / fast XCorr (`ApplySlidingWindowD`, `SpectralScorer.cs:573`)

Prefix-sum O(n) implementation, offset 75, normalizing over 150 flanking bins
(excluding the center):

```csharp
const int offset = XCORR_WINDOW_OFFSET;            // 75, SpectralScorer.cs:36
double normFactor = 1.0 / (2 * offset);            // 1/150

prefix[0] = 0.0;
for (int i = 0; i < n; i++)
    prefix[i + 1] = prefix[i] + spectrum[i];

for (int i = 0; i < n; i++)
{
    int left  = Math.Max(0, i - offset);
    int right = Math.Min(n, i + offset + 1);
    double windowSum = prefix[right] - prefix[left];
    double sumExcludingCenter = windowSum - spectrum[i];
    result[i] = spectrum[i] - sumExcludingCenter * normFactor;
}
```

This is the same prefix-sum recurrence the Rust doc calls the "optimized O(n)"
path (offset 75, norm `1/150`, center excluded, edges clamped without
renormalization).

---

## Step 2: Theoretical spectrum — no preprocessing, O(n_fragments) lookup

The C# port never materializes a dense theoretical unit vector in production.
XCorr is computed as the Comet lookup form: sum the preprocessed experimental
value at each library fragment's bin. The three overloads
(`XcorrFromPreprocessed` for `double[]` and `float[]`, and `XcorrFromSparse`)
are structurally identical (`SpectralScorer.cs:186`, `:234`, `:273`):

```csharp
double xcorrRaw = 0.0;
for (int f = 0; f < entry.Fragments.Count; f++)
{
    int bin = _binConfig.MzToBin(entry.Fragments[f].Mz);
    if (bin >= 0 && bin < n && !visitedBins[bin])   // dedup: count a bin once
    {
        visitedBins[bin] = true;
        xcorrRaw += preprocessed[bin];
    }
}
...
return xcorrRaw * XCORR_SCALING;                     // 0.005, SpectralScorer.cs:37
```

**Fragment-bin dedup (`visitedBins`)**: if two fragments map to the same bin,
the bin contributes **once**. This reproduces the dense-vector semantics
(`theoretical[bin] = 1.0` set idempotently) and is covered by
`TestXcorrFragmentBinDedup` (`ScoringTest.cs:382`). The visited flags are reset
in O(n_visited) after scoring so the shared `bool[]` can be reused across
candidates without a per-call `NBins` allocation.

The only dense theoretical-vector code path is the test-only `XCorr(double[]
observedBins, double[] libraryBins)` (`SpectralScorer.cs:66`), which does a full
dense dot product and is exercised by `TestXcorrPerfectMatch` /
`TestXcorrNoMatch`. Production never calls it.

### Scaling factor (`0.005`)

`XCORR_SCALING = 0.005` (`SpectralScorer.cs:37`), applied once at the end of
every overload. Matches Comet.

---

## Step 3: Per-window preprocessing cache (`ResolutionStrategy.cs`)

During the search, all spectra in a DIA window are preprocessed **once** and
reused across every candidate, exactly as the Rust doc's per-window optimization
describes. The C# port routes this through `IResolutionStrategy`
(`ResolutionStrategy.cs:66`) so pipeline code never branches on resolution:

- **`UnitStrategy.PreprocessWindowSpectra`** (`ResolutionStrategy.cs:119`):
  preprocesses each spectrum with `PreprocessSpectrumForXcorrF32`, then widens
  the `float[]` losslessly into a `double[]` (`WindowXcorrCache.Doubles`). The
  comment notes this pure-f32 preprocess matches Rust upstream.
- **`HramStrategy.PreprocessWindowSpectra`** (`ResolutionStrategy.cs:170`):
  stores each spectrum as a `SparseXcorrSpectrum` (`WindowXcorrCache.Sparse`)
  rather than a dense `float[NBins]`.

`ScoreXcorr` (`ResolutionStrategy.cs:143` / `:192`) dispatches on the cache type:
unit reads `preprocessed.Doubles[i]` via `XcorrFromPreprocessed`; HRAM reads
`preprocessed.Sparse[i]` via `XcorrFromSparse`, falling back to a live
`XcorrAtScan` with a rented scratch when no cache row exists.

### Sparse HRAM cache (`SparseXcorrSpectrum.cs`, issue #4398)

The dense HRAM cache would be `float[100002]` (~391 KB) per spectrum; with ~2,000
spectra per Astral window across `NThreads` concurrent windows this reached
tens of GB. `SparseXcorrSpectrum` instead retains only the ~1–3K nonzero windowed
bins plus a prefix sum over them (~20 B per retained peak), and recovers each
probed bin's flanking-subtracted value on demand in `CenteredAt`
(`SparseXcorrSpectrum.cs:93`):

```csharp
double windowSum = PrefixBelow(right) - PrefixBelow(left);
double sumExcludingCenter = windowSum - v;
double centered = v - sumExcludingCenter * _normFactor;   // _normFactor = 1/(2*offset)
return (float)centered;                                    // narrow to f32, as dense cache did
```

Because IEEE-754 `x + 0.0 == x` exactly and intensities are non-negative, a
prefix accumulated over only the nonzero bins equals the dense prefix bit-for-bit
(`SparseXcorrSpectrum.cs:44-53`). The final `(float)` narrowing is deliberate:
the dense path stored `float` and widened back on read, so the sparse path must
narrow identically or every HRAM XCorr drifts from the golden.
`TestSparseXcorrCacheMatchesDenseCache` (`ScoringTest.cs:1292`) asserts
`XcorrFromSparse == XcorrFromPreprocessed(dense)` to 0.0 tolerance.

### Scratch pooling (`XcorrScratchPool.cs`)

To avoid per-call LOH allocation of the `NBins` f64 work buffers (`Binned`,
`Windowed`, `Prefix`, `Preprocessed`, `VisitedBins`), a `XcorrScratchPool`
(`XcorrScratchPool.cs:72`) rents/returns `XcorrScratch` sets. `Return` re-zeros
only the two accumulator buffers (`Binned`, `VisitedBins`); the fully-overwritten
buffers are left dirty. This pool is threaded into `ScoreXcorr` and is the
subject of the performance gate (`XcorrCalc` doc comment, `XcorrCalculators.cs:48`).

---

## Step 4: Feature calculators (`XcorrCalculators.cs`)

### Feature 6 — `xcorr` (`XcorrCalc`, `XcorrCalculators.cs:53`)

A single apex-spectrum XCorr, delegated straight to the resolution strategy:

```csharp
return context.Resolution.ScoreXcorr(
    context.PreprocessedXcorr, peakData.ApexGlobalIndex, peakData.ApexSpectrum,
    peakData.Candidate, context.Scorer, context.XcorrScratchPool);
```

The cache is indexed at the **window-global apex index**
(`peakData.ApexGlobalIndex = WindowStartIndex + candidate-local apex`), a
different index space from the SG sweep (documented as an "INDEX TRAP" at
`XcorrCalculators.cs:37`). Higher is better (`IsReversedScore == false`).

### Feature 17 — `sg_weighted_xcorr` (`SgXcorrCalc` / `SgWeightedSweep`, `XcorrCalculators.cs:104`)

A Savitzky-Golay quadratic-smoothed XCorr over the apex ±2 spectra. Weights are
`[-3/35, 12/35, 17/35, 12/35, -3/35]` (`XcorrCalculators.cs:109`, matching Rust
`sg_weights`). The sweep runs offsets −2..+2 strictly left-to-right, calling
`ScoreXcorr` per offset (`XcorrCalculators.cs:152`):

```csharp
for (int offset = -2; offset <= 2; offset++)
{
    double weight = SG_WEIGHTS[offset + 2];
    if (!peakData.TryGetApexOffsetSpectrum(offset, out var s, out int globalIdx))
        continue;                                   // asymmetric edge skip, no renormalization
    sgXcorr += resolution.ScoreXcorr(preprocessedXcorr, globalIdx, s, candidate, scorer, pool) * weight;
    sgCosine += ComputeCosineAtScan(candidate, s, config) * weight;
}
```

The sweep is computed **once** per candidate and shared with feature 18
(`sg_weighted_cosine`) via `context.AddInfo`/`TryGetInfo`, guaranteeing both
features use the identical offset loop, boundary skip, and index mapping. Near a
window edge, out-of-range offsets are `continue`'d (not added as zero) and the
partial sum is left un-renormalized, matching Rust.

Both feature 6 and feature 17 are among the 21 PIN features used for FDR (see
`03-spectral-scoring.md` and `07-fdr-control.md`); neither is behind a flag.

---

## Step 5: Calibration XCorr — always unit resolution (`Calibrator.cs`)

Calibration scores each candidate against its apex spectrum with a scorer that is
**hardcoded to unit-resolution bins** regardless of the data's resolution mode:

```csharp
internal static readonly SpectralScorer s_calXcorrScorer =
    new SpectralScorer(BinConfig.UnitResolution());   // Calibrator.cs:72
```

Window spectra are preprocessed once with `PreprocessSpectrumForXcorrF32`
(widened to `double[][]`, `Calibrator.cs:563-572`) and each candidate's apex
XCorr is the O(n_fragments) lookup (`Calibrator.cs:1189`):

```csharp
double xcorrApex = (windowPreprocessed != null && apexWindowIdx < windowPreprocessed.Length)
    ? s_calXcorrScorer.XcorrFromPreprocessed(windowPreprocessed[apexWindowIdx], entry)
    : s_calXcorrScorer.XcorrAtScan(apexSpectrum, entry);
```

This matches the Rust doc's "Calibration: Always Unit Resolution Bins" section
(~2000 bins even for HRAM data, for ~50× faster calibration scoring). HRAM bins
are used only in the main-search feature-extraction path (Step 3/4).

---

## Flags and switches

XCorr has no dedicated on/off flag — feature 6 (`xcorr`) and feature 17
(`sg_weighted_xcorr`) are always computed and always in the 21-feature PIN set.
The flags that change XCorr behavior for this stage:

| Flag / env var | Default | Effect on XCorr |
|----------------|---------|-----------------|
| `--resolution {unit\|hram\|auto}` | `auto` | Selects `BinConfig` for the **feature-extraction** XCorr: unit (`1.0005079` Da, offset `0.4`, ~2000 bins) or HRAM (`0.02` Da, offset `0.0`, ~100002 bins). `auto` is resolved during calibration. Calibration XCorr ignores this and always uses unit bins (`Calibrator.cs:72`). |
| `--fragment-tolerance <v>` / `--fragment-unit {ppm\|mz}` | `ppm`; unit-res forces `mz 0.5` | **Does not affect XCorr.** XCorr matches by bin index, not ppm tolerance. Tolerance only affects the sibling cosine kernels (`ComputeCosineAtScan`, `LibCosine`) and XIC extraction. |
| `--no-prefilter` | prefilter enabled (`PrefilterEnabled = true`) | Does not change the XCorr math; only affects which candidates reach scoring. |
| `OSPREY_DIAG_XCORR_SCAN` (env var) | unset | When set to a scan number, `XcorrAtScan` (`SpectralScorer.cs:453`) appends a per-scan diagnostic dump (`cs_xcorr_diag.txt`) with `binned`/`windowed`/`preprocessed` sums and per-fragment bin lookups, for cross-impl bisection. Zero overhead when unset. |

There is **no** XCorr-specific config in `OspreyConfig`; the constants
(`XCORR_WINDOW_OFFSET = 75`, `XCORR_SCALING = 0.005`, 10 windows, `50.0`, 5%
threshold) are compile-time and match Comet/Rust.

---

## Divergences from the Rust documentation

- **[INTENTIONAL-CSHARP-DESIGN] Managed scalar loops, no BLAS/SIMD** — Rust doc
  says XCorr dot products dispatch to BLAS `sdot` via `ndarray::ArrayView1::dot()`
  for contiguous f32 slices. The C# port cannot use BLAS; every XCorr is a
  managed scalar loop, and in fact the production path never forms a dense
  theoretical vector at all — it uses the O(n_fragments) sum-at-fragment-bins
  form (`XcorrFromPreprocessed`/`XcorrFromSparse`). Results are bit-identical.
  Evidence: `Osprey.Scoring/SpectralScorer.cs:186`, `:273`;
  `Osprey.Tasks/Calibrator.cs:1189`. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Sparse HRAM cache instead of dense `Vec<Vec<f32>>`**
  — Rust doc stores each window spectrum's preprocessed XCorr as a dense
  `float[NBins]` (`~391 KB` at HRAM). The C# port stores a `SparseXcorrSpectrum`
  (~20 B per retained peak) and recovers each probed bin's flanking-subtracted
  value on demand, bit-identically (proven by `x + 0.0 == x` in IEEE-754 and a
  final `(float)` narrowing that reproduces the dense cache). This is C#-side
  issue #4398, not described in the Rust doc. Evidence:
  `Osprey.Scoring/SparseXcorrSpectrum.cs:55`, `:93`;
  `Osprey.Scoring/ResolutionStrategy.cs:170`. Severity: info.

- **[STALE-RUST-DOC] Simplified O(n_fragments) snippet omits fragment-bin dedup**
  — The Rust doc's "feature extraction" pseudocode sums `preprocessed[bin]` for
  every fragment with no dedup, which would double-count two fragments falling in
  the same bin. The dense-vector form the same doc presents (`theoretical[bin] =
  1.0`) inherently dedups, so the actual behavior requires dedup. The C# code
  deduplicates via `visitedBins` and has a regression test for it, matching the
  dense semantics rather than the simplified snippet. Evidence:
  `Osprey.Scoring/SpectralScorer.cs:198`; `Osprey.Test/ScoringTest.cs:382`.
  Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] Precision split: unit-res f64, HRAM f32-narrowed**
  — The C# port keeps the unit-resolution / calibration cache in full `f64`
  (`WindowXcorrCache.Doubles`) but narrows each HRAM centered value to `float`
  (mirroring the Rust HRAM code's f32 cache, not spelled out in the doc). The
  narrowing is required for parity with the Rust HRAM path, so behavior matches
  the Rust *code*; the doc simply does not discuss the precision boundary.
  Evidence: `Osprey.Scoring/ResolutionStrategy.cs:119` (f64 unit) vs
  `Osprey.Scoring/SparseXcorrSpectrum.cs:113` (f32 HRAM). Severity: info.

Everything else verified to match the Rust documentation step for step: Comet
`BIN` macro (`BinConfig.cs:96`), unit/HRAM bin parameters, sqrt binning with
in-bin accumulation, 10-window/`50.0`/5%-threshold windowing normalization
(`SpectralScorer.cs:522`), offset-75 prefix-sum flanking subtraction with norm
`1/150` (`SpectralScorer.cs:573`), the `0.005` scaling (`SpectralScorer.cs:37`),
the theoretical-spectrum-not-preprocessed lookup form, per-window preprocess-once
optimization (`ResolutionStrategy.cs:119`/`:170`), and calibration always using
unit-resolution bins (`Calibrator.cs:72`).

See `03-spectral-scoring.md` for the surrounding 21 PIN features,
`04-calibration.md` for resolution selection, and `17-vectorization.md` for the
BLAS-vs-managed performance discussion.
