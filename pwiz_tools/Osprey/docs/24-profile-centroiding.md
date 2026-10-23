# Profile centroiding (Centrix)

Osprey-only: Rust Osprey reads vendor centroids, so like [23](23-demultiplexing.md) this document
has no "Divergences" section. The algorithm is a port of a separate Rust tool, Centrix
(maccoss/centrix); how closely the port follows it is in [Fidelity](#fidelity-to-rust-centrix).

**What it is for.** A linear ion trap (Thermo Stellar, the ion trap of a Fusion) has unit
resolution: a fragment's profile peak is 0.4 to 0.8 Th wide at half maximum. The vendor's
centroider places one centroid per local maximum, so two fragments less than about 1 Th apart
come out as one centroid at an m/z that belongs to neither. Centrix fits each region of the
profile with Gaussian peaks of the instrument's width, one candidate per profile point, by a
non-negative lasso, and keeps the few that explain the signal. Signals closer than 1 Th come out
separately.

**Where it runs.**
- **Osprey, `--centroid centrix`**: the reader opens the file's profile and centroids it with
  Centrix in place of the vendor library. See [Using it](#using-it).
- **The staggered joint demultiplexer** (Stellar profile data, `Osprey.DemuxTool --scheme
  staggered --joint`): the joint solve splits each acquired spectrum's profile among its narrow
  bins, and Centrix centroids each bin's share. See
  [23-demultiplexing.md](23-demultiplexing.md#stellar-profile-data-linear-ion-trap).
- **`Osprey.DemuxTool --centroid centrix`**: writes Centrix-centroided mzML, of a profile file as
  it is (`--scheme none`) or of a demultiplexer's output.

## Using it

| Option | Default | Meaning |
|---|---|---|
| `--centroid vendor\|centrix` | `vendor` | Who centroids profile spectra. `centrix` reads the profile even from a vendor file |
| `--centrix-sigma-ms1 <Th>` | measured | The MS1 peak sigma; 0 or absent measures it from the run (see [The peak sigma](#the-peak-sigma)) |
| `--centrix-sigma-ms2 <Th>` | measured | The MS2 peak sigma, likewise |
| `--centrix-solver descent\|active-set` | `descent` | Centrix's coordinate descent, or the exact solve (see [Solvers](#solvers)) |

```
Osprey.exe -i run_profile.raw -l library.tsv -o out.blib --resolution unit --centroid centrix
```

What happens in a run (`SpectrumFileReader.LoadAllSpectra`):
1. The file is opened without vendor peak picking.
2. Unless both sigmas are given, up to 3,000 spectra spread evenly over the file are read first
   and each MS level's sigma is measured from their peaks.
3. The file is read in order and its profile spectra are centroided in parallel, 512 at a time
   (`--threads`). Each spectrum is centroided on its own, so the output does not depend on the
   thread count. Spectra the file holds centroided pass through.
4. The log names the sigmas: "Centroided the profile spectra with Centrix: peak sigma 0.170 Th
   for MS1, 0.309 Th for MS2".
5. The spectra are cached as `<stem>.centrix-<8 hex>.spectra.bin` (see
   [Caches and keys](#caches-and-keys)).

A centroid's intensity is the sum of its peak's profile samples, about 3.4 times the peak height a
Stellar vendor centroid reports, so absolute intensities differ between the two modes.

`Osprey.DemuxTool`: `--centroid centrix` with `--centrix-sigma S` (MS2), `--centrix-sigma-ms1 S`
and `--centrix-param Name=Value` (any `CentrixParams` setting, `Solver=active_set` among them).
`--scheme none` writes the input centroided and nothing else. With `--scheme staggered --joint`,
MS2 is centroided inside the joint solve, whose sigma is `--peak-sigma-th` (measured when
absent), and `--centroid centrix` then centroids the MS1.

## The algorithm

Per spectrum, in `CentrixCentroider` (`Osprey.Centroid`):

1. **Rough noise.** Baseline the 10th percentile of all intensities; noise sigma the interquartile
   range / 1.349, at least a tenth of the baseline and at least 1 (`NoiseProfile.Rough`). The floor
   of 1 assumes an instrument's intensity units, an ion trap's rates; feed it intensities, not ions.
2. **Signal regions.** Runs of points above baseline + 3 noise sigmas, runs at most 2 points apart
   joined, each extended by 3 points a side, regions under 3 points dropped (`SignalRegion.Detect`).
3. **Pass 1.** Each region is fitted on basis positions every grid spacing, one beyond each end:
   `A[i, j] = exp(-(mz_i - grid_j)^2 / (2 sigma^2))`. The non-negative lasso
   `min 1/2 |y - A b|^2 + lambda |b|_1, b >= 0` with lambda = 3 rough noise sigmas
   (`NonNegativeLasso`). The Gram matrix is the analytic Toeplitz one of an infinite grid
   (`CentrixBasis.GramRow`), as Centrix does, not A'A of the region's own points.
4. **Noise refinement.** In 20 Th windows stepped 5 Th, the root mean square of pass 1's
   residuals and of the points outside every region, smoothed by a 3-point median
   (`NoiseProfile.Refine`).
5. **Pass 2.** Regions whose lambda the refined noise changes by more than 20% are solved again
   from their pass-1 solution; their centroids replace pass 1's.
6. **Centroids.** Every coefficient above zero is a centroid, moved between grid points to the
   vertex of the parabola through the logarithms of it and its neighbors when both are above zero.
   Its intensity is the coefficient times `sigma sqrt(2 pi) / spacing`, the sum of its peak's
   samples. Centroids closer than sigma merge, intensity-weighted.

| File | Role |
|---|---|
| `CentrixCentroider.cs` | the two passes, the merge, the basis per grid spacing |
| `CentrixParams.cs` | settings (Centrix's defaults) and the solver choice |
| `CentrixBasis.cs` | Gaussian basis, Gram row, basis positions |
| `NonNegativeLasso.cs` | coordinate descent, the active-set solve, sub-grid refinement |
| `NoiseProfile.cs`, `SignalRegion.cs` | steps 1, 2 and 4 |
| `CentrixCalibration.cs` | scan rates and the peak sigma measured from a run |

## The peak sigma

The sigma is the instrument's resolution, and a linear ion trap's resolution is set by its scan
rate: the faster the scan, the wider the peaks and the coarser the profile points. The firmware
fixes the point spacing per scan rate, so the spacing identifies the rate. Centrix's table gives
each rate's broadest peak; the peaks a run actually shows are narrower.

| Scan rate | Point spacing | Broadest peak (FWHM) | Measured on the Stellar (FWHM, sigma) |
|---|---|---|---|
| 33 kTh/s | 1/30 Th | 0.5 Th | - |
| 67 kTh/s | 1/15 Th | 0.6 Th | MS1: 0.40 Th, sigma 0.170 Th |
| 125 kTh/s | 1/8 Th | 0.8 Th | - |
| 200 kTh/s | 1/5 Th | 2.0 Th | MS2: 0.72 Th, sigma 0.309 Th |

Centrix gives no spacing for 200 kTh/s; 0.2 Th is what the Stellar's MS2 at that rate has, five
points per Th on the 1000 / rate pattern of the others.

**How it is measured** (`CentrixCalibration`, `CentrixSigmaCalibrator`), for each MS level apart:
1. The scan rate from the point spacing of the level's first profile spectrum
   (`LinearIonTrapScanRate.FromSpacing`).
2. In each of 3,000 spectra spread evenly over the run, the isolated intense peaks: local maxima
   above the spectrum's 99th percentile whose intensity falls without rising again, or reaching
   zero, to half the apex on both sides, over points with none missing between them. Each one's
   full width at half maximum, the apex taken from the log-parabola through its top three points.
3. The sigma is the widths' lower quartile / 2.3548, no wider than the scan rate's broadest. With
   under 20 peaks it is the scan rate's broadest; with no scan rate either, 0.34 Th, Centrix's
   fallback.

**Why this way.**
- *A wider peak is most often two or more species the instrument did not resolve*, which is what
  Centrix is there to separate, so the width is taken from the narrow side of the distribution,
  not its middle. The scan rate's broadest bounds it from above.
- *Full width at half maximum, not a fitted Gaussian.* These peaks have a narrow core and broad
  wings, and a Gaussian fit's sigma grows with the points it sees. On the Stellar's MS2 a
  log-parabola gives 0.31 Th from three points and 0.36 from five; linear fits over 5, 7, 9 and 11
  points give 0.36, 0.43, 0.51 and 0.61 Th. The last is what Centrix's own wavelet calibration
  reports.
- *The lower quartile, not the mode.* The MS2 widths form a plateau from 0.70 to 0.85 Th, so their
  mode (a half-sample mode was tried) moved by 6% between samples of 3,000 peaks; the lower
  quartile moved by 0.6%.
- *Spikes left out.* An ion trap ejects each ion at one instant, so a peak of a few ions lands on
  one or two points with zeros beside it, narrower than the peak shape the scan rate gives. On the
  Stellar's MS2, read through Thermo's library, a quarter of the profile points are zeros and such
  spikes are a third of the intense local maxima. Counted, they pull the lower quartile from 0.72
  to 0.63 Th (sigma 0.305 to 0.266 Th over a whole 2 Th run). A reader that drops the zeros leaves
  a gap instead, which is rejected the same way, so the width does not depend on the reader.
- *Sampled across the run, every sample used.* A first version stopped at 3,000 peaks, all from
  the run's first minutes, and with the spikes counted read sigma 0.21 Th where the whole run reads
  0.29. Over a whole run the sample gives 10,000 to 15,000 peaks.
- *Not chosen by identifications.* At unit resolution those depend on mass accuracy, a separate
  question from the peak width.

**What moves it, left unmodeled.**
- The width does not depend on m/z (lower quartile 0.71 to 0.74 Th from 200 to 1200 m/z), as a
  linear ion trap's should not, but it rises with intensity: 0.63 Th in the weakest fifth of
  peaks, 0.81 Th in the strongest. Space charge, or more merged species among larger peaks.
- Spectra of wider windows merge more species. The 2 Th windows give sigma 0.309 Th; the same
  spectra summed two and four at a time, as staggered windows are, give 0.331 and 0.340 Th. The
  joint demultiplexer measures from the acquired, multiplexed spectra, so it reads the wider value.

## The Stellar's filter strings: "Z" and Adaptive RT

Centrix reads the scan rate from a letter in the filter string (n, r, t, u for 33, 67, 125,
200 kTh/s, after "NSI"). On the Stellar's MS1 that works ("ITMS + p NSI r Full ms", 67 kTh/s).
Its MS2 carry "Z" instead: "ITMS + p NSI Z ms2 480.4683@hcd20.00 [200.0000-1200.0000]". In a
Thermo filter "Z" is a zoom scan, and ProteoWizard reports it as such (MS:1000497), but the
Stellar performs no zoom scans; it uses that annotation to store its Adaptive RT spectra. Yet in
the 2026-10-09 window-width HeLa runs every DIA MS2 carries it (the 2Th TR01 run: 142,317 MS2,
all "Z", all 2 m/z windows), and the method has no zoom scans. So:
- the scan rate comes from the point spacing, never from the "Z";
- spectra must not be dropped for carrying "Z": in these runs that would drop every MS2.

Adaptive RT spectra are normally 20 or 50 m/z wide, and Stellar DIA never uses windows over 10 m/z,
so the isolation width, not the annotation, is how to recognize them. Osprey does not yet filter
them; see [Status](#status-and-open-items).

## Solvers

The basis peaks of neighboring grid points correlate strongly (0.91 at the Stellar's MS2 spacing,
1.65 points per sigma), so Centrix's coordinate descent converges slowly. On 6,000 Stellar MS2
spectra it averaged 347 sweeps per region and stopped at its 2,000-sweep cap in 6% of regions,
which then keep an unconverged solution.

`--centrix-solver active-set` (`NonNegativeLasso.SolveActiveSet`) reaches the exact optimum of
the same problem, the point coordinate descent converges to, by Lawson and Hanson's active-set
method: the few nonzero coefficients are solved directly by Cholesky, stepping back to
feasibility when a solve leaves one at or below zero. If a factorization fails (adjacent
coefficients are ill-conditioned: about 1e9 for 20 of them at the MS2 spacing), it falls back to
coordinate descent.

On the same 6,000 spectra it took 18-31 s against 64-169 s for coordinate descent (file I/O
included, on a loaded machine). It moves 8.5% of centroids by more than 1e-4 and changes the count
in 23% of spectra (net +0.13%): the capped solves, and through pass 1's residuals the refined
noise and so other regions' penalties. Against truth the two are equal to 0.001 (below).
Coordinate descent stays the default, so the output stays comparable with Rust Centrix's.

## Fidelity to Rust Centrix

The port follows Rust Centrix at 39370f1 with the coordinate-descent solver.
`CentrixTest.TestCentrixRustGolden` holds it there: 30 real Stellar profile spectra (vendor-read
MS1 and MS2, and joint-split MS2) and the 4,836 centroids Rust Centrix gives them
(`Osprey.Test/Data/Centrix`, whose README says how the golden was made and how to regenerate it).
Every spectrum must have the same number of centroids and each centroid match within 1e-3 Th and
1%. The port matches to 1e-11 Th; a 1.7% change to the lasso penalty fails the test.

On all 6,000 joint-split spectra of the benchmark slice, the port and Rust give the same 832,332
centroids; 0.02% differ by more than 1e-4, by up to 4e-4 Th and 0.4%. Two deliberate differences:
- Rust sums A'y with BLAS, the port in index order, so a solve stopped by the 1e-6 absolute
  tolerance can stop a sweep apart.
- The port detects the grid spacing per spectrum (keeping a basis per spacing); Rust takes the
  first spectrum's of each MS level.

Not ported: Centrix's scan rate and wavelet calibration (see [The peak sigma](#the-peak-sigma)),
and its mzML reading and writing (Osprey reads through ProteoWizard).

**Truth benchmark** (pwiz-ai `scripts/Osprey/Demux/eval_remux.py`; results in the pwiz-ai TODO
`TODO-20261021_osprey_stellar_demux.md`): a 2 Th Stellar run's narrow-window spectra summed into
staggered windows (`Osprey.DemuxTool --remultiplex K`), demultiplexed by the joint solve, and
compared with the narrow spectra centroided the same way, on a 0.05 Th grid blurred by 0.3 Th.
Median cosine, ion-weighted median, 10th percentile, at sigma 0.33 Th:

| Overlap | Coordinate descent | Active set |
|---|---|---|
| k=2 | 0.982 / 0.985 / 0.926 | 0.982 / 0.985-0.986 / 0.927 |
| k=3 | 0.959 / 0.967 / 0.798 | 0.959 / 0.967-0.968 / 0.798 |
| k=4 | 0.930 / 0.942 / 0.684 | 0.930 / 0.942-0.943 / 0.683-0.684 |

The benchmark does not distinguish sigmas between 0.31 and 0.34 Th: the measured sigmas (0.331 for
k=2, 0.340 for k=4, truth at 0.309) score within 0.003 of the fixed 0.33 Th against either truth.

## Caches and keys

Spectra Osprey centroided itself are not the vendor's, so they never share a cache with them.
`ProfileCentroiding` (`Osprey.Tasks`) gives three things, all empty for `--centroid vendor`, so a
default run is unchanged:
- **A cache tag**, `centrix-` and the first 8 hex digits of the SHA-256 of a descriptor holding
  `CentrixCentroider.ALGORITHM_VERSION` and every setting. `Program` sets it on
  `ArtifactPaths.SpectraCacheTag`, and `SpectraCache.GetCachePath` and `GetDemuxCachePath` insert
  it: `<stem>.centrix-1a2b3c4d.spectra.bin`, `<stem>.centrix-1a2b3c4d.demux.spectra.bin`. Every
  reader of the cache, Stage 6's rescore and the training export included, finds the same file;
  the vendor-centroided `<stem>.spectra.bin` beside it is untouched.
- **A validity-key term**, `;centroid=<descriptor>`, in every task's key, so scores from one
  centroiding are never reused for another.
- **The search hash** (`SearchIdentity`) records the mode and settings.

Raise `ALGORITHM_VERSION` with any change that moves a centroid; the tag and keys change with it.

## Status and open items

- **Identifications.** Searches of the Stellar 2 Th profile runs with vendor centroids, Centrix
  and Centrix's active set, and of the staggered runs through the joint demultiplexer, are
  recorded in the pwiz-ai TODO above.
- **Adaptive RT spectra** (isolation over 10 m/z on a Stellar) are not filtered from Osprey's
  input or from the mzML DemuxTool writes for Skyline.
- **The intensity dependence of the width** is not modeled; one sigma serves each MS level.
- **The joint demultiplexer's sigma** is measured from multiplexed spectra (above).
- **Few MS1 peaks.** A Stellar run has one MS1 per cycle of some 250 spectra, and an MS1 holds
  about four isolated intense peaks, so the 3,000 sampled spectra give about a dozen MS1 and some
  50 peaks; enough for the quartile (20 needed), but a short run may fall back to the scan rate's
  broadest.
- **Linear grids only.** Orbitrap and TOF profiles are not uniform in m/z; Centrix does not apply
  to them as it stands.
- **Other linear ion traps** (a Fusion's) follow the same table but are untested.
