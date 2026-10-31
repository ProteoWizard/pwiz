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
| `--centrix-sigma-ms1 <Th>` | from the run | The MS1 peak sigma; 0 or absent takes it from the run: 1.1 point spacings on a linear ion trap (see [The peak sigma](#the-peak-sigma)) |
| `--centrix-sigma-ms2 <Th>` | from the run | The MS2 peak sigma, likewise |
| `--centrix-solver descent\|active-set` | `descent` | Centrix's coordinate descent, or the exact solve (see [Solvers](#solvers)) |

```
Osprey.exe -i run_profile.raw -l library.tsv -o out.blib --resolution unit --centroid centrix
```

What happens in a run (`SpectrumFileReader.LoadAllSpectra`):
1. The file is opened without vendor peak picking.
2. Unless both sigmas are given, spectra spread evenly over the file are read first until each
   MS level's sigma is settled: its first profile spectrum on a linear ion trap, otherwise up to
   3,000 spectra whose peaks it is measured from.
3. The file is read in order and its profile spectra are centroided in parallel, 512 at a time
   (`--threads`). Each spectrum is centroided on its own, so the output does not depend on the
   thread count. Spectra the file holds centroided pass through.
4. The log names the sigmas: "Centroided the profile spectra with Centrix: peak sigma 0.073 Th
   for MS1, 0.220 Th for MS2" on the Stellar.
5. The spectra are cached as `<stem>.centrix-<8 hex>.spectra.bin` (see
   [Caches and keys](#caches-and-keys)).

A centroid's intensity is the sum of its peak's profile samples, about 3.4 times the peak height a
Stellar vendor centroid reports, so absolute intensities differ between the two modes.

`Osprey.DemuxTool`: `--centroid centrix` with `--centrix-sigma S` (MS2), `--centrix-sigma-ms1 S`
and `--centrix-param Name=Value` (any `CentrixParams` setting, `Solver=active_set` among them).
`--scheme none` writes the input centroided and nothing else. With `--scheme staggered --joint`,
MS2 is centroided inside the joint solve, whose sigma is `--peak-sigma-th` (from the run when
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
| `CentrixCalibration.cs` | scan rates and a run's peak sigma |

## The peak sigma

**On a linear ion trap the sigma is 1.1 point spacings** (`CentrixCalibration.SIGMA_PER_SPACING`).
The firmware fixes the profile point spacing per scan rate, so the spacing identifies the rate
(`LinearIonTrapScanRate.FromSpacing`), and the level's first profile spectrum settles its sigma.

| Scan rate | Point spacing | Broadest peak (FWHM) | Measured on the Stellar (FWHM lower quartile) |
|---|---|---|---|
| 33 kTh/s | 1/30 Th | 0.5 Th | - |
| 67 kTh/s | 1/15 Th | 0.6 Th | MS1: 0.40 Th |
| 125 kTh/s | 1/8 Th | 0.8 Th | - |
| 200 kTh/s | 1/5 Th | 2.0 Th | MS2: 0.63 to 0.72 Th |

Centrix's table gives each rate's broadest peak and no spacing for 200 kTh/s; 0.2 Th is what the
Stellar's MS2 at that rate has, five points per Th on the 1000 / rate pattern of the others.

**Why a little over one point spacing.** Centrix is there to split a peak wider than one species,
and splitting a little too much costs little. The rule comes from one scan rate: the Stellar's 2 Th profile runs
(three HeLa runs, MS2 at 200 kTh/s), centroided at fixed MS2 sigmas (MS1 0.2 Th in every arm) and
searched by Osprey with a library fine-tuned on these runs and carrying natural entrapment:

| MS2 sigma | Experiment precursors at q 1% | Entrapment FDP (combined) |
|---|---|---|
| 0.12 Th | 33,725 | 0.27% |
| 0.15 Th | 34,604 | 0.34% |
| 0.18 Th | 34,621 | 0.32% |
| 0.20 Th (1 spacing) | 35,077 | 0.34% |
| 0.21 Th | 35,849 | 0.51% |
| **0.22 Th (1.1 spacings, the default)** | **36,001** | 0.53% |
| 0.24 Th | 35,680 | 0.52% |
| 0.27 Th | 35,116 | 0.63% |
| 0.30 Th | 34,330 | 0.51% |
| 0.33 Th | 32,526 | 0.53% |
| 0.36 Th | 32,297 | 0.47% |

The same three runs acquired in centroid mode give 47,405 (0.49%). Identifications plateau from
0.21 to 0.24 Th (1.05 to 1.2 spacings), where the entrapment FDP stays flat, and fall on both
sides. At one spacing and below, Osprey's calibrated MS2 window narrows from 0.48 Th to 0.31-0.35
Th and identifications drop, at a lower FDP; by 0.18 Th the centroids per MS2 spectrum have risen
from about 260 to over 400. The default, 1.1 spacings, sits inside the plateau and clear of that
drop. A wide sigma merges a fragment with its M+1 isotope into one centroid between them, which
Osprey's mass calibration reads as a positive offset and a wider window (0.48 Th at sigma 0.21,
1.04 at 0.33). Identifications alone cannot place the rule finely, since unit-resolution
scoring barely penalizes splitting.

**Not a measured width, on the Stellar.** A linear ion trap's peak width depends on the ions in the
peak, so no single width is one species'. The MS2 widths' lower quartile is 0.71 to 0.74 Th at every
m/z from 200 to 1200, as a linear ion trap's should be, but by ions in the peak it rises from 0.41 Th
under 3 ions to 0.50 (3-10), 0.63 (10-30), 0.75 (30-100), 0.81 (100-300) and 0.9 Th over 300, with no
plateau: few ions arrive as short pulses, many ions spread by space charge and merge with unresolved
neighbors. A measured sigma is therefore set by which peaks are counted: over a whole 2 Th run, 0.27
Th (threshold over all points, a quarter of them zeros), 0.31 Th (threshold over positive points, so
stronger peaks), 0.33 Th (peaks of 30 or more ions). All are wider than the identifications' optimum.

**Other scan rates are untested.** At 67 kTh/s the rule gives 0.073 Th, where the Stellar's MS1
measures 0.17 Th (lower quartile). For Osprey's search of these runs it does not matter: the
default (MS1 0.073 Th, MS2 0.22 Th) gives exactly the 36,001 precursors of MS1 at 0.2 Th. Stellar
data at other MS2 scan rates will decide whether the optimum follows the spacing, a fraction of
the scan rate's broadest peak, or the few-ion peak width.

**Elsewhere it is measured** (a spacing no scan rate has: a linear grid other than a linear ion
trap's), for each MS level apart:
1. In each of 3,000 spectra spread evenly over the run, the isolated intense peaks: local maxima
   above the spectrum's 99th percentile whose intensity falls without rising again, or reaching
   zero, to half the apex on both sides, over points with none missing between them. Each one's
   full width at half maximum, the apex taken from the log-parabola through its top three points.
2. The sigma is the widths' lower quartile / 2.3548; with under 20 peaks, 0.34 Th, Centrix's
   fallback.

Why this way:
- *A wider peak is most often two or more species the instrument did not resolve*, so the width is
  taken from the narrow side of the distribution, not its middle.
- *Full width at half maximum, not a fitted Gaussian.* These peaks have a narrow core and broad
  wings, and a Gaussian fit's sigma grows with the points it sees. On the Stellar's MS2 a
  log-parabola gives 0.31 Th from three points and 0.36 from five; linear fits over 5, 7, 9 and 11
  points give 0.36, 0.43, 0.51 and 0.61 Th. The last is what Centrix's own wavelet calibration
  reports.
- *The lower quartile, not the mode.* The Stellar's MS2 widths form a plateau from 0.70 to 0.85 Th,
  so their mode moved by 6% between samples of 3,000 peaks; the lower quartile moved by 0.6%.
- *Spikes left out.* An ion trap ejects each ion at one instant, so a peak of a few ions lands on
  one or two points with zeros beside it. Read through Thermo's library, a quarter of the Stellar's
  profile points are zeros; peaks that fall to zero before half their apex are skipped, and a
  reader that drops the zeros leaves a gap there, which is skipped the same way. Over a whole 2 Th
  run this moves the sigma only from 0.266 to 0.269 Th.
- *Sampled across the run, every sample used.* The run's first 3,000 peaks read 0.17 Th where the
  whole run reads 0.27.
- Spectra of wider windows merge more species: the Stellar's 2 Th windows summed two and four at a
  time, as staggered windows are, read 0.331 and 0.340 Th where the 2 Th windows read 0.309.

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
- **The joint demultiplexer's sigma** follows the same rule, 1.1 point spacings, for both its
  Gaussian and Centrix. On the truth benchmark (below) it scores at least as well as the 0.33 Th
  the staggered searches in [23](23-demultiplexing.md) ran at: median cosine 0.985 against 0.982 at
  k=2, 0.939 against 0.930 at k=4, each scored against truth centroided at the same sigma. The
  joint solve at this sigma is not yet searched.
- **Other scan rates.** The rule rests on 200 kTh/s alone (see [The peak sigma](#the-peak-sigma)).
- **Other grids.** Orbitrap and TOF profiles are not uniform in m/z. `GridCentroider` (Osprey.Demux)
  runs Centrix on any profile grid's sample index, where a grid uniform in some coordinate of m/z is
  uniform (a TOF's, in sqrt(m/z)), with the peak sigma in samples given by m/z
  (`CentrixCentroider`'s varying sigma: each region fitted with the basis of the sigma at its center,
  rounded to 0.01) and the Th settings converted at a reference m/z. Tried on ZT Scan in place of the
  joint solve's own centroids, it did not help ([23](23-demultiplexing.md#the-joint-solve-zt-scan---joint)).
  Counting data also needs a noise floor of its own (`MinNoiseSigma`, 1 by default as in Centrix):
  a profile of counts with its zeros dropped reads a noise of zero.
- **Other linear ion traps** (a Fusion's) follow the same table but are untested.
