# 17. Vectorization (managed, no BLAS) (C#)

> Pipeline stage: Cross-cutting (performance). C# port of Rust docs/15-blas-vectorization.md. Corresponds to Rust osprey BLAS/SIMD vectorization.

## What this stage is about

The Rust reference implementation accelerates its numerical hot paths with an external
BLAS library (OpenBLAS, linked through `ndarray`'s `blas` feature) plus LLVM
auto-vectorization of idiomatic iterator code. The C# port is a self-contained managed
.NET assembly: it takes **no native BLAS dependency at all** and cannot rely on a
Rust/LLVM auto-vectorizer. This document describes what the managed port actually does
in each numeric hot path, where it uses `System.Numerics.Vector<T>` SIMD, and where it
deliberately falls back to plain scalar loops -- and confirms that in every case the
outputs stay bit-for-bit on the cross-impl parity gate.

There is **no `--blas`, `--simd`, or `--no-vectorize` switch anywhere in the C# CLI.**
Vectorization here is an implementation detail, not a user-facing feature. The only flags
that touch this stage are the general performance/threading and resolution flags (see
"Flags and switches").

## The numeric hot paths, in pipeline order

### 1. Calibration spectral scoring (Stage 3 discovery)

Rust reformulates calibration-phase spectral matching as a single BLAS `sgemm`:
`library_matrix (n_lib x n_bins) @ spectra_matrix.T (n_bins x n_spectra)` computes all
pairwise cosine similarities at once, ~20x faster than pairwise scoring, using f32 to
double SIMD throughput.

The C# port **ports the data structures but not the all-pairs matrix multiply into the
live pipeline**:

- `PreprocessedLibrary` (Osprey.Scoring/BatchScorer.cs:46) stores library entries as a
  `float[,]` row-major matrix (`n_entries x n_bins`), sqrt-preprocessed and L2-normalized
  exactly like Rust (BatchScorer.cs:95-122). f32 is preserved for parity with the Rust
  matrix dtype.
- `PreprocessedSpectra` (BatchScorer.cs:129) does the same for spectra (BatchScorer.cs:145-200).
- `BatchScorer.BatchXCorr` (BatchScorer.cs:248-267) is the "matrix multiply", but it is a
  plain triple-nested scalar loop (`dot += library.Matrix[lib,b] * spectra.Matrix[spec,b]`),
  explicitly labelled "Uses direct loops (no BLAS dependency)" (BatchScorer.cs:247) and
  "Port of BatchScorer ... (simplified, no BLAS)" (BatchScorer.cs:205).

However, `BatchXCorr` / `BatchScorer.FindBestMatches` are **not called anywhere in the
analysis pipeline** -- their only references are inside `BatchScorer.cs` itself and the
unit test `Osprey.Test/ScoringTest.cs:284`. The actual calibration discovery loop scores
**per candidate**, not all-pairs:

- `Calibrator.cs:1184` computes the apex LibCosine feature with
  `SpectralScorer.LibCosine(apexSpectrum, entry, tolerance)` -- one library entry against
  one apex spectrum.
- `Calibrator.cs:1189-1191` computes the apex XCorr through
  `SpectralScorer.XcorrFromPreprocessed` against a **per-window** preprocessed bin array
  (`windowPreprocessed[apexWindowIdx]`) that is built once per isolation window and reused
  across all candidates in that window -- the same "preprocess once, O(n_fragments) bin
  lookup" strategy Rust reserves for the *main* search (see step 2), applied to
  calibration too.
- `CalibrationScorer.cs` (`CalibrationScorer.TrainAndScoreCalibration`,
  CalibrationScorer.cs:84) consumes those per-candidate features.

Net effect: the C# port never materializes the 2.5-million-cell all-pairs score matrix in
production. It reaches the same calibration matches through per-candidate scoring plus
per-window XCorr preprocessing. Outputs are bit-identical on the reference datasets; the
BLAS `sgemm` reformulation is simply absent. See 04-calibration.md.

### 2. Main-search XCorr (Stage 3/4 scoring primitive)

Here the C# port and the Rust "Where BLAS is Not Used" section agree in architecture. Each
precursor's XCorr is an individual sparse gather, not a matrix multiply:

- Per isolation window, the sliding-window-subtracted Comet fast-XCorr bin vector is
  prepared once and reused across every candidate in the window.
- `SpectralScorer.XcorrFromPreprocessed(double[]/float[], entry, visitedBins)`
  (SpectralScorer.cs:186-216 for f64, SpectralScorer.cs:234 for f32) walks only the
  candidate's ~10-30 fragment bins (`MzToBin` lookup), summing `preprocessed[bin]` with a
  de-dup guard (`visitedBins`). This is an O(n_fragments) sparse dot product, **not** an
  O(n_bins) dense one and not a matmul -- exactly the Rust main-search design.
- The `xcorr` feature calculator (XcorrCalc, Osprey.Scoring/XcorrCalculators.cs:53) and the
  Savitzky-Golay apex+/-2 sweep (SgWeightedSweep, XcorrCalculators.cs:104) both route
  through `IResolutionStrategy.ScoreXcorr`, which selects the f64 (Unit) or f32 (HRAM)
  cache. The accumulation is a strict scalar left-to-right sum -- the doc-comment at
  XcorrCalculators.cs:95-97 explicitly says "Do not vectorize or reorder" to preserve the
  golden. See 02-xcorr-scoring.md.

No SIMD is applied to XCorr; the gather is data-dependent and sparse, so vectorizing it
would not help and would risk perturbing the f64 reduction order the parity gate depends
on.

### 3. HRAM XCorr cache: managed memory design (no Rust analogue)

The managed runtime has a garbage collector and a Large Object Heap (LOH) that Rust does
not. Two C# constructs exist purely to keep the XCorr hot path off the LOH allocator; they
change nothing about the arithmetic:

- `SparseXcorrSpectrum` (Osprey.Scoring/SparseXcorrSpectrum.cs:55, issue #4398) stores the
  HRAM spectrum as its sparse pre-subtraction peaks + a prefix sum instead of a dense
  `float[100001]` (391 KB, LOH) per spectrum. `CenteredAt(bin)` (SparseXcorrSpectrum.cs:93)
  recovers the Comet fast-XCorr centered value on demand and **narrows to `float`** so it
  matches the old dense f32 cache bit-for-bit (SparseXcorrSpectrum.cs:44-50 documents the
  IEEE-754 `x + 0.0 == x` argument that makes the sparse prefix equal the dense prefix
  element-for-element).
- `XcorrScratchPool` / `XcorrScratch` (Osprey.Scoring/XcorrScratchPool.cs:41,72) is a
  reuse pool of the f64 scratch buffers (Binned/Windowed/Prefix/Preprocessed/VisitedBins,
  each `double[NBins]`) so repeated XCorr calls do not re-allocate ~800 KB LOH arrays. The
  pool grows to its high-water mark (typically NThreads) and never shrinks. This is the
  perf gate (see 19-testing.md, Test-PerfGate.ps1); the pool is threaded through every
  `ScoreXcorr` call.

Rust has no equivalent because it has no GC/LOH; these are INTENTIONAL-CSHARP-DESIGN
infrastructure with identical outputs.

### 4. SVM training: the one hand-vectorized SIMD path (Stage 5/7 FDR)

This is the single place the C# port uses explicit SIMD, and it is the mirror-image of the
Rust situation. Rust wrote idiomatic `w[..p].iter().zip(row).map(...).sum()` and let LLVM
auto-vectorize it into AVX2/AVX-512 FMA. RyuJIT does **not** auto-vectorize the equivalent
indexed scalar loop, so the C# dual-coordinate-descent SVM hand-vectorizes with
`System.Numerics.Vector<double>`:

- The `w . x_i` dot product (Osprey.ML/LinearSvmClassifier.cs:516-568) accumulates
  per-lane partial sums in a `Vector<double>` then horizontal-sums with
  `Vector.Dot(sumVec, Vector<double>.One)` (LinearSvmClassifier.cs:560), followed by a
  scalar tail for the remaining `< vecSize` columns and the bias term.
- The `w += d * x_i` weight update (LinearSvmClassifier.cs:589-602) uses the same lane
  pattern (`(wv + dVec * xv).CopyTo(w, k2)`).
- `Vector.Dot`/`Vector.One` (rather than `Vector.Sum`, which is .NET 7+) are used so the
  code also compiles for the net472 target (LinearSvmClassifier.cs:557-559).

Reduction-order caveat (LinearSvmClassifier.cs:531-545): the SIMD path accumulates
`Vector<double>.Count` lane-partial sums (4 on AVX2, 8 on AVX-512, 2 on ARM NEON) whereas
Rust does a strict scalar left-fold. Per-op drift is sub-ULP; on the reference data at
p=21 features the cumulative drift stays inside the 1e-9 cross-impl parity gate. The
comment explicitly warns that the gate's headroom assumes a lane-stride-stable runtime -- a
future AVX2->AVX-512 swap shifts the lane stride and hence the exact divergence pattern; if
parity ever breaches 1e-9 after such a swap, bisect by forcing the scalar tail
(`vecSize > cols`). See 07-fdr-control.md and 16-determinism.md.

### 5. ML matrix / linear-algebra ops: plain scalar loops (LDA calibration)

The `osprey-ml` matrix ops are the small-matrix operations Rust also keeps off BLAS. The
C# port implements them as straightforward scalar loops with no SIMD and no parallelism:

- `Matrix.Dot` (Osprey.ML/Matrix.cs:235-254) and `Matrix.DotVector` (Matrix.cs:259-275)
  are textbook triple/double scalar loops over a row-major `double[]`.
- `Matrix.PowerMethod` (Matrix.cs:348) and `Matrix.Transpose` / `ExtractRows` are scalar.
- `GaussSolver` (Osprey.ML/GaussSolver.cs:41) solves `Sw * x = Sb` for LDA via scalar
  Gauss-Jordan elimination with partial pivoting (Echelon GaussSolver.cs:133, Reduce/
  Backfill), plus the escalating-epsilon diagonal regularization (GaussSolver.cs:56-67).

These matrices are tiny (e.g. LDA scatter is n_features x n_features), so SIMD/BLAS
dispatch overhead would exceed any gain -- the same rationale the Rust doc gives for
osprey-ml using manual loops. Both `Matrix.cs` and `GaussSolver.cs` carry a header noting
they originate from Sage (`osprey-ml/src/matrix.rs`, `gauss.rs`).

### 6. Pearson / cosine scalar kernels (feature computation)

The pairwise-correlation and cosine feature kernels are all scalar single-pass reductions
with fixed accumulation order for parity:

- `PearsonCorrelation.Pearson` / `CoelutionSum` / `MeanPairwiseCorrelation`
  (Osprey.Scoring/PearsonCorrelation.cs:38,104,72) -- scalar sums, no-variance guard at
  `denom < 1e-30`.
- `ScoringMath.PearsonOverRange` / `PearsonCorrelationInRange`
  (Osprey.Scoring/ScoringMath.cs:60,89) -- range-windowed scalar Pearson (two independent
  ports with different no-variance guards, deliberately not merged; ScoringMath.cs:38-51).
- `SgWeightedSweep.ComputeCosineAtScan` (XcorrCalculators.cs:177) and
  `SpectralScorer.CosineAngle` (SpectralScorer.cs:731) -- scalar sqrt-intensity L2 cosine.

None are vectorized; the intent is exact reproduction of the Rust scalar reduction order.

## Flags and switches

There are **no vectorization-specific flags** in the C# CLI. BLAS is not present, SIMD is
always on where the code uses it (SVM), and there is no way to disable it. The flags that
indirectly affect this stage:

| Flag / config | Default | Effect on this stage |
| --- | --- | --- |
| `--threads <count>` | all cores | INNER parallelism (per-window scoring, FDR). Vectorization is per-thread; more threads multiply the SIMD hot paths but do not change per-call arithmetic. |
| `--parallel-files [N]` | off (sequential; no value = auto) | OUTER parallelism across files. Independent of vectorization. |
| `--resolution {unit\|hram\|auto}` | auto | Selects the XCorr cache dtype: Unit reads the f64 `Doubles` cache, HRAM reads the f32-narrowed `SparseXcorrSpectrum` value (XcorrCalculators.cs:43-46). Determines whether step 2/3 run in f64 or f32, mirroring the Rust f32 batch choice. |
| `--fragment-tolerance` / `--fragment-unit` | ppm (unit-resolution forces mz 0.5) | Governs bin/match tolerance in the scalar kernels above; not a vectorization control. |

Environment variables: none specific to vectorization. (`OSPREY_DUMP_*` diagnostics touch
calibration/scoring dumps but not the SIMD paths.)

Config fields: `OspreyConfig` exposes no BLAS/SIMD toggle. The `BatchScorer` bin geometry
(`DEFAULT_NUM_BINS = 2000`, `DEFAULT_MIN_MZ = 200.0`, `DEFAULT_BIN_WIDTH = 1.0005`,
BatchScorer.cs:209-211) is a private constant, and `BatchScorer` is not on any pipeline
path.

## Divergences from the Rust documentation

- **[INTENTIONAL-CSHARP-DESIGN] No BLAS/OpenBLAS dependency** - Rust doc says calibration
  batch scoring is a single OpenBLAS `sgemm` call linked via `ndarray`'s `blas` feature ->
  `blas-src` -> system OpenBLAS; the C# port is pure managed .NET with no native BLAS.
  Evidence: Osprey.Scoring/BatchScorer.cs:205,247 ("simplified, no BLAS" / "Uses direct
  loops (no BLAS dependency)"). Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] All-pairs matrix multiply not used in the calibration
  pipeline** - Rust doc says calibration computes ~2.5M cosine similarities as one matrix
  multiply per window (`BatchScorer::score_all`, `library.matrix.dot(&spectra.matrix.t())`);
  the C# `BatchScorer.BatchXCorr` port exists but is a scalar triple loop referenced only by
  its own `FindBestMatches` and by `Osprey.Test/ScoringTest.cs:284`. The live calibration
  loop scores per candidate (`SpectralScorer.LibCosine` + per-window
  `XcorrFromPreprocessed`). Evidence: Osprey.Tasks/Calibrator.cs:1184,1189-1191;
  BatchScorer.cs:248-267 (no pipeline caller). Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] Explicit SIMD only in the SVM, via System.Numerics** - Rust
  doc frames SIMD as auto-vectorization by LLVM across the code; in the C# port RyuJIT does
  not auto-vectorize, so the only hand-written SIMD is the SVM dot product / weight update
  using `Vector<double>`, with a documented sub-ULP lane-reduction-order caveat kept inside
  the 1e-9 gate. Evidence: Osprey.ML/LinearSvmClassifier.cs:516-568,589-602. Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] Managed LOH/GC avoidance infrastructure with no Rust
  analogue** - Rust doc has nothing on GC because Rust has no GC; the C# port adds
  `SparseXcorrSpectrum` (sparse pre-subtraction storage + on-demand f32 recovery, issue
  #4398) and `XcorrScratchPool` (reuse pool for `double[NBins]` scratch) purely to keep the
  XCorr hot path off the .NET Large Object Heap. Arithmetic and outputs are unchanged.
  Evidence: Osprey.Scoring/SparseXcorrSpectrum.cs:55,93; Osprey.Scoring/XcorrScratchPool.cs:41,72.
  Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] osprey-ml matrix ops are scalar, no BLAS (matches Rust
  intent)** - Rust doc states osprey-ml uses "manual loops with Rayon parallelization,
  without BLAS"; the C# `Matrix.Dot`/`DotVector` and `GaussSolver` are scalar loops with no
  SIMD and (unlike Rust) no Rayon-style parallelism -- acceptable because these matrices are
  tiny. Evidence: Osprey.ML/Matrix.cs:235-275; Osprey.ML/GaussSolver.cs:133-230. Severity: info.

- **[STALE-RUST-DOC] Bin-count off-by-one in the (unused) batch geometry** - Rust doc says
  "2,001 bins spanning 200-2000 Da at 1.0005 Da width"; the C# `BatchScorer` default is
  `DEFAULT_NUM_BINS = 2000`. Because `BatchScorer` is not on any pipeline path this has no
  behavioral effect, and the live per-window XCorr uses its own `BinConfig`, not this
  constant. Evidence: Osprey.Scoring/BatchScorer.cs:209-211. Severity: info.

Verified: the C# port has no BLAS linkage; the only production numeric paths are the
per-candidate/per-window scalar XCorr and cosine kernels, the hand-vectorized SVM
(`Vector<double>`), and scalar osprey-ml matrix/Gauss-Jordan ops -- all producing outputs
on the 1e-9 cross-impl parity gate. See 02-xcorr-scoring.md, 04-calibration.md,
07-fdr-control.md, and 16-determinism.md.
