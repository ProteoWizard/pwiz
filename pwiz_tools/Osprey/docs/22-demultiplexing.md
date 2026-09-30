# Demultiplexing overlapping-window DIA

Osprey-only: there is no Rust counterpart, so like [00](00-pipeline-architecture.md) and
[21](21-user-facing-text.md) this document has no "Divergences" section.

**What it is for.** Some DIA methods sample each narrow precursor range more than once, through
different isolation windows. The most common is **staggered DIA**: two sets of, say, 12 Th windows
offset by half a window, so every 6 Th bin is covered by one window from each set. The
acquisition measures wide windows, but it carries enough information to recover the narrow bins.
Demultiplexing does that recovery. Searching the result gets the bin width's precursor
selectivity, where searching the raw spectra would get the window width's, and would also score
every precursor in two windows.

**Where it stands.** There are two demultiplexers, in the same library (`Osprey.Demux`):
- **The per-channel demultiplexer**, one algorithm for staggered windows and for a scanning
  quadrupole (SCIEX ZT Scan). It solves each fragment channel with the quadrupole's transmission
  and counting-statistics weights. For staggered windows it is what `--demux auto` runs (the
  *weighted* engine, the default); ZT Scan still runs only in `Osprey.DemuxTool`, which writes a
  demultiplexed mzML that Osprey or DIA-NN can search. See
  [The per-channel demultiplexer](#the-per-channel-demultiplexer-staggered-and-zt-scan).
- **The overlap demultiplexer** (the *msconvert* engine, `OSPREY_DEMUX_ENGINE=msconvert`). It
  reproduces, inside Osprey, the overlap demultiplexing msconvert does
  (`--filter "demultiplex optimization=overlap_only"`). It is kept to compare against msconvert,
  and is to be removed. [The algorithm](#the-algorithm) and
  [Differences from msconvert](#differences-from-msconvert) describe it.

Both engines read the raw file directly, through Osprey's own reader, and cache the result as
Osprey spectra; everything from [Using it](#using-it) to [Files](#files) applies to either.

The goal is one demultiplexer for every compressed-sampling scheme, where each measured spectrum
mixes several precursor bins in known proportions:
- staggered windows at any overlap (k = 2, 3, 4) and variable widths: both demultiplexers;
- scanning-quadrupole methods: SCIEX ZT Scan on the ZenoTOF (the per-channel demultiplexer), and
  Waters SONAR;
- Thermo MSX (random multiplexed co-isolation);
- comb (parallel isolation) acquisitions;
- profile-domain demultiplexing with Osprey's own centroiding, for the Stellar.

See [Status and limitations](#status-and-limitations).

## Using it

```
osprey -i run1.raw -i run2.raw -l library.tsv -o results.blib --resolution hram --demux auto
```

| `--demux` | What happens |
|---|---|
| `off` (default) | Spectra are searched as acquired. A run whose windows overlap is refused with an error naming `--demux auto`. Searched as acquired, every precursor in it would be scored in two or more windows. |
| `auto` | Each run's isolation scheme is detected. If its windows overlap, it is demultiplexed and the result searched; if not, it is searched as acquired. |

The option is off by default while the method is validated. It takes no tuning parameters: the
scheme is read from the data. Every task of an HPC chain must be given the same `--demux` value,
and the search hash includes it when it is on, so scores from a demultiplexed search are never
mixed with scores from one that was not.

Input can be a vendor file or an mzML. msconvert's own demultiplexed mzML also works, with
`--demux off`: its bins do not overlap, so Osprey searches them as they are.

## What happens in a run

Demultiplexing happens where the spectra cache is built: in `PerFileScoring` (Stage 2), or
`--task SpectraCache`, both through `ScoringTaskShared.EnsureSpectraCache`. With `--demux auto`
it looks for, in order:

1. **A valid `<stem>.demux.spectra.bin`.** If present, it is searched and nothing else is read.
   It is enough on its own, but only while its descriptor (see [Files](#files)) matches the
   running Osprey:
   - A cohort staged with demultiplexing can be searched after its sources are gone.
   - A later Osprey whose demux algorithm version differs refuses the demultiplexed cache, and
     can rebuild it only from the `.spectra.bin` or the source.
   - So keep the `.spectra.bin` of any cohort that may be searched again by a later version.
   - An input whose source and `.spectra.bin` are both gone, and whose demultiplexed cache no
     longer matches, is refused at start-up with the reason.
2. **A valid `<stem>.spectra.bin`.** The isolation scheme is detected from its window index. A
   non-overlapping run is searched from it as before. An overlapping run is read back in full,
   demultiplexed, and written to `<stem>.demux.spectra.bin`, which is then searched.
3. **Neither.** The source is parsed, `<stem>.spectra.bin` is written exactly as without demux,
   and the spectra still in memory are demultiplexed without being read back.

`PerFileRescoring` (Stage 6) reads the same `<stem>.demux.spectra.bin`. If only `.spectra.bin` is
available and its windows overlap, rescoring stops with an error, because the earlier stages
searched the demultiplexed spectra.

The log records the scheme found, the solver's work and the timing. With the msconvert engine:

```
Demultiplexing Ecl_..._10.raw: 2-fold overlap, 101 windows into 102 bins of 6.000-6.003 Th
  79,350 MS2 spectra -> 158,700; 69,188,527 channel solves (0.0% zero, 0.0% unconstrained, 100.0% active set, 0 at the iteration cap) over 2 block geometries
  Demultiplexed in 18.8s on 16 thread(s); parse 191.0s, ratio 0.10; osprey-demux/3;block=covered_bins;...
```

The weighted engine reports its fragment channels instead: how many it found, how many were
strong enough to solve, and the share of the ions passed through unsolved.

The last line is the timing gate for the implementation: demultiplexing as a fraction of the raw
parse it follows. On the Orbitrap Eclipse data that fraction is 0.10-0.13 for the msconvert
engine, with scalar code.

## Files

| File | Written | Contents |
|---|---|---|
| `<stem>.spectra.bin` | always | The acquired spectra, **never altered by demultiplexing**. It stays a pure function of the raw file, so it is shareable across analyses, and demultiplexing can be redone from it without re-reading the source. |
| `<stem>.demux.spectra.bin` | `--demux auto` on an overlapping run | The demultiplexed spectra: one record per narrow bin per acquired spectrum. Each keeps its parent's scan number and retention time; its isolation window is the bin. |

Both live in the cache directory (`--cache-dir`, else beside the source; see
[00](00-pipeline-architecture.md)). The demultiplexed cache is about 1.7 times the size of the
plain one (0.85 GB to 1.43 GB for one of the Eclipse runs below): twice the records, each with
fewer peaks.

Its format ([14](14-intermediate-files.md#demultiplexed-cache-stemdemuxspectrabin)) is the plain
cache's VERSION 4 layout with a different magic and a **descriptor** after the header, for
example:

```
osprey-demux/4;engine=weighted;channel_tolerance=10ppm;min_bin_width=0.2;counts_per_ion=100;block_cycles=12;cycle_pad=4;group_bins=16;min_channel_ions=8;min_channel_cells=3;poisson_weights=True;weight_floor_ions=0.5;min_output_ions=0.2
osprey-demux/4;engine=msconvert;channel_tolerance=10ppm;min_bin_width=0.2;block=covered_bins;interpolation=makima;output=apportioned;block_bins=7
```

The descriptor names the algorithm version, the engine, and every setting of that engine that
changes the output. Version 4 added the engine, so caches written before it are rebuilt. A cache
whose descriptor differs from the current one is rejected and rebuilt from `.spectra.bin`, in
seconds, as it is when its source file changes. The thread count is not part of it, because it
never changes the output.

The descriptor is also part of every task's validity key (`DemuxCacheBuilder.ValidityKeySuffix`),
so the scores and FDR results computed from the old spectra are recomputed along with the cache.
The search hash records only `demux:auto`, because `Osprey.Core` cannot see the descriptor. With
demux off, neither the key nor the hash changes.

## The algorithm

This is the overlap demultiplexer, the msconvert engine (`OSPREY_DEMUX_ENGINE=msconvert`). For
each acquired MS2 spectrum (the *target*), independently and in parallel:

1. **Scheme.** Detected once per run from every distinct isolation window
   (`DemuxSchemeDetector`).
   - The narrow bins are the intervals between the sorted union of all window edges. Edges
     closer than 0.2 Th are merged first, because instruments report one nominal edge with
     small differences. No uniform ladder is assumed, so variable-width schedules work.
   - The overlap factor k is the coverage that spans the most m/z. Ordinary DIA whose neighbors
     share a 0.5-1 Th margin is therefore not mistaken for a stagger.
2. **Block.** A local linear system centered on the target's bins. Its core is 7 bins, as in
   msconvert. Its rows are every window that touches the core, and its columns are every bin
   those windows cover. The entries are 0/1: a window's bins are one contiguous isolation with
   one fill, and intensities are rates. There is one factorization per distinct block shape
   (two for the Eclipse runs), built once before the parallel loop.
3. **Channels.** Each of the target's centroids is a fragment channel, ±10 ppm. Where two
   channels overlap they are split at the mean of their four edges, so no peak counts twice.
4. **Retention time.** A neighboring window was not acquired at the target's time. Its channel
   values are interpolated to that time from its own acquisitions: three on each side, by
   **makima** (modified Akima). makima is local and makes no peak-shape assumption. In
   simulation at 3-4 points per FWHM, without noise, its apex error was 0.6-1.3% of peak
   height, against 1.5-3.2% for msconvert's 3-point natural spline and 2.6-4.5% for PCHIP.
   Interpolated values below zero are clamped to zero. The target's own row is its measured
   peaks.
5. **Solve.** One non-negative least-squares fit per channel (`NnlsSolver`): Lawson-Hanson on
   the normal equations, with an unconstrained shortcut when the block has full rank.
6. **Output.** The target's measured intensity for each channel is split among its own bins in
   proportion to the solution ("apportioned", msconvert's rule). The pieces therefore sum back to
   what was measured, at the time it was measured. A bin whose share is below a millionth of the
   channel's total is round-off, and gets no peak.

**Determinism.**
- Every loop runs in index order, ties resolve to the lowest index, and the NNLS iteration count
  is capped.
- The block factorizations are read-only inside the parallel loop, and each spectrum writes only
  its own output.
- Output is bit-identical at any thread count, which `DemuxTest` asserts on synthetic and real
  data.

### Differences from msconvert

| | msconvert | Osprey |
|---|---|---|
| Block columns | The 7-bin core only. A window crossing its edge is cut to the part inside, but still carries signal from its bin outside. When a fragment is in several consecutive bins (y1, immonium and b2 ions often are), that error alternates in sign along the ladder into the target bins. | Every covered bin. The model is exact, and non-negativity resolves the resulting one-dimensional ambiguity for any fragment absent from two adjacent bins. |
| RT interpolant | Natural cubic spline through 3 points; undershoots on peak tails. | makima through 3 points each side. |
| Choosing neighbors | By spectrum index: the ~100 spectra nearest the target, and the spectrum one cycle's count of spectra away as "the same window a cycle away". This assumes the scan order repeats exactly. | By window identity and retention time. |

On the Eclipse runs, Osprey set to msconvert's block and interpolant (the
[developer overrides](#developer-overrides)) matches msconvert with a median per-spectrum cosine
of 0.999; its defaults match with 0.997. Set that way, Osprey still identifies more than
msconvert's demultiplexing does (see [Validation](#validation)). The gain is therefore in
implementation details beyond the first two rows, such as the third. Which detail matters has
not been isolated.

## Developer overrides

For attributing a difference from msconvert, and nothing else, environment variables override
the defaults:
- `OSPREY_DEMUX_ENGINE` = `weighted` | `msconvert`: the engine. The three below apply to the
  msconvert engine only.
- `OSPREY_DEMUX_BLOCK` = `covered_bins` | `truncated_slice`;
- `OSPREY_DEMUX_INTERPOLATION` = `makima` | `pchip` | `natural_three_point` | `linear`;
- `OSPREY_DEMUX_OUTPUT` = `apportioned` | `solution`.

`truncated_slice` with `natural_three_point` is msconvert's configuration. Each value is part of
the descriptor, so a cache built under an override is rebuilt without it.

## Validation

**Unit tests** (`Osprey.Test/DemuxTest.cs`, about a second together; each names the engine it
tests):
- **NNLS** against a brute-force optimum over 3,000 random systems, including underdetermined
  ones.
- **Interpolant exactness**, and the apex-error ordering behind the choice of makima.
- **Scheme detection**: k = 2 and 3, variable widths, jittered edges, margin-overlap DIA, and a
  window that first appears late.
- **Exact round trips** from narrow-bin truth: constant elution; 3 ppm m/z jitter at k = 2 and 3
  and variable widths; and moving elution at about 6 samples per FWHM with per-interpolant error
  bounds.
- **A demonstration of the truncated-block bias.**
- **The demultiplexed cache**: its refusals and window listing, the CLI option, and the search
  hash.
- **The pipeline wiring**: the demux-off refusal, building and reusing the cache, searching from
  it alone, rebuilding it on a settings change, a non-overlapping run left alone, and the
  Stage 6 rule.
- **A real-data fixture** (`Osprey.Test/Data/Demux`): a 3-minute, 8-window slice of an Orbitrap
  Eclipse staggered run, and msconvert's demultiplexing of the same slice. The test pins the
  msconvert engine's output against a committed per-bin golden and its agreement with msconvert
  against measured floors. How the slice was made, and how to rebless the golden, is in the
  folder's README.
- **The weighted engine**: exact recovery of the constant-elution truth, output independent of
  the thread count, and on the fixture a median cosine with msconvert of 0.995 (0.855 of spectra
  at 0.95 or more), with the measured intensity kept.

**Whole runs**: two Orbitrap Eclipse staggered runs (EV13 and EV14; 12 Th windows at k = 2),
searched with a Carafe library carrying 1:1 entrapment.

| Demultiplexing | First-pass run-level precursors (EV13 / EV14) | Experiment precursors | Experiment peptides | Entrapment FDP |
|---|---|---|---|---|
| msconvert | 28,670 / 28,481 | 38,411 | 33,145 | 0.28% |
| Osprey default | 29,260 / 28,726 | 39,298 (+2.3%) | 33,883 (+2.2%) | 0.27% |
| Osprey, msconvert's settings | 29,372 / 29,441 | 39,328 (+2.4%) | 33,957 (+2.4%) | 0.30% |

When these searches ran, Osprey listed only the windows of a run's first cycle, which missed the
top bin of msconvert's output (since fixed). The experiment counts are therefore restricted to
precursors below 1000.70 m/z, a range all three searched.

Differences of 1-2% between single Osprey runs are within its SVM-selection noise, so the two
Osprey rows are not distinguishable. Both, however, identify more than msconvert's
demultiplexing at the same measured FDP.

At the spectrum level, a library-free check agrees:
- Each bin is sampled alternately through its two windows, so a wrong split between bins shows as
  a zig-zag in the bin's fragment chromatograms.
- Osprey's defaults zig-zag least: median 0.211 vs msconvert's 0.221, and 0.48 vs 0.55 for the
  worst tenth.
- That improvement has not, on this data, turned into more identifications. It may matter for
  quantitation, which these runs cannot measure.

The comparison tools are in pwiz-ai, under `ai/scripts/Osprey/Compare`:
- `Compare-DemuxSpectra.py`: spectrum by spectrum;
- `Measure-StaggerConsistency.py`: the zig-zag measure;
- `Compare-DemuxSearches.py`: detections, entrapment FDP and overlap.

## The per-channel demultiplexer (staggered and ZT Scan)

`ScanningDemultiplexer` is the second demultiplexer: one algorithm for a scanning quadrupole (SCIEX
ZT Scan) and for stepped staggered windows. Like the overlap demultiplexer it solves one fragment
m/z channel at a time, y = A x with x >= 0. It never couples a fragment to other fragments or to
the MS1: everything happens at the product-ion level, where MS2 is most sensitive.

It differs from the overlap demultiplexer in four ways, each explained below:

| | Overlap demultiplexer | Per-channel demultiplexer |
|---|---|---|
| Entries of A | 0/1 window coverage | the quadrupole's measured transmission (ZT Scan); 0/1 for staggered windows |
| Fragment channels | each centroid of the target spectrum, ±10 ppm | maxima of the whole block's m/z histogram |
| Solve | NNLS | NNLS, then a refit with Poisson row weights from that fit |
| Output | the target's observed peaks, apportioned | staggered: the same; ZT Scan: solved intensities, several positions per spectrum (a *layout*) |

### Running it

`Osprey.DemuxTool` reads a vendor file (.wiff2 or .raw, vendor-centroided as msconvert's
`peakPicking vendor msLevel=1-` does) or an mzML, and writes a demultiplexed mzML:

```
Osprey.DemuxTool --in run.wiff2 --out run.demux.mzML --kernel kernel.profile.tsv --layout framed:3:1
Osprey.DemuxTool --in run.raw --out run.demux.mzML --scheme staggered
```

| Option | Default | Meaning |
|---|---|---|
| `--scheme` | `scanning` | `scanning` (ZT Scan; needs `--kernel`) or `staggered` |
| `--kernel` | - | the measured transmission profile (see [below](#the-transmission-zt-scan)) |
| `--layout` | `centered:5` | ZT Scan output layout: `centered:k`, `tiled:k` or `framed:k:m` |
| `--min-out` | 0.2 | ZT Scan: solved intensities below this many ions are not written |
| `--position-mz` | off | ZT Scan: write each solved value at the m/z of the peaks it was solved from, in its own sweep (see [Output](#output-zt-scan-and-layouts)) |
| `--source-positions` | off | ZT Scan: place each channel's sources once per block, then solve each sweep over them (see [Source positions](#source-positions)) |
| `--source-l1 L` | 0 | ZT Scan, with `--source-positions`: a non-negative lasso weight on the fit that finds the sources |
| `--min-source-fraction F` | 0.05 | ZT Scan, with `--source-positions`: sources under this fraction of their channel's total are dropped |
| `--sweep-l1 L` | 0 | ZT Scan: a non-negative lasso weight on every per-sweep solve (see [Sparsity](#sparsity-the-lasso)) |
| `--sweep-l1-z Z` | 0 | ZT Scan: a per-position lasso weight of Z standard deviations of the position's score under Poisson noise |
| `--sweep-l1-refit` | off | with a lasso: refit each sweep without the penalty on the positions the lasso kept |
| `--block-support-z Z` | 0 | ZT Scan: choose each channel's positions once per block (z-scaled lasso on its summed counts), then solve each sweep over them unpenalized |
| `--centroid events` | vendor | ZT Scan: centroid the MS2 profile ourselves, one centroid per run of adjacent digitizer samples, single ion events kept (MS1 keeps the vendor's centroids) |
| `--profile` | off | read a vendor file without centroiding (with `--raw`, a profile dump for inspection) |
| `--apportion H` | off | ZT Scan: apportion observed peaks instead of writing solved values |
| `--counts-per-ion` | from the data | the detector counts of one ion, for the weights and the ion thresholds; estimated from each file's lowest intensity levels (see [The joint solve](#the-joint-solve-zt-scan---joint)) |
| `--joint` | off | ZT Scan: demultiplex and centroid the profile in one solve (see [The joint solve](#the-joint-solve-zt-scan---joint)) |
| `--merge-sigmas F` | 2 | with `--joint`: neighbouring positions' centroids closer than F TOF peak sigmas are summed |
| `--ms1 vendor\|joint` | joint with `--joint`, else vendor | MS1 as the vendor's centroids, or centroided by the joint solve on its own grid |
| `--peak-shape gaussian\|measured` | gaussian | with `--joint`: the TOF peak as the Gaussian of the sigma table, or as kernels measured from the file |
| `--joint-param Name=Value` | - | with `--joint`: any scalar setting of `JointDemuxParams`, for tuning |
| `--solve-profile` | off | with `--joint`: print where the solve's time went |
| `--ppm` | 10 | the channel tolerance |
| `--unweighted` | off | skip the Poisson refit |
| `--raw` | off | ZT Scan: write the selected spectra as acquired (a control arm) |
| `--cycles`, `--mz` | all | restrict to sweeps `first:last` (0-based) and precursor m/z `low:high` |
| `--threads` | all | the output does not depend on it |

For staggered data the tool and `--demux auto` run the same pipeline (`StaggeredDemuxPipeline`),
the tool on the file, Osprey on the spectra it has parsed. For ZT Scan the tool is for evaluation
until that is wired into `--demux` too. The Sciex.Wiff2
reader plugin and its native SQLite libraries are staged beside the tool when it is built with
the vendor readers. Osprey.exe does not stage them yet.

### The ZT Scan acquisition

On the ZenoTOF 8600 runs used here (`250814_ZTScan_100spd_*`):
- Each cycle is one MS1 scan and one sweep of the quadrupole from 392.76 to 899.78 m/z. The sweep
  is recorded as 429 MS2 spectra, the *encoded bins*, 1.181866 Th and 2.01 ms apart. A cycle takes
  0.971 s, and a run has 699 of them.
- The file reports each spectrum's isolation window as its own 1.18 Th bin. The quadrupole
  actually transmits a precursor into about 16 consecutive bins (below). So each spectrum mixes
  the precursors of about 16 bins, and each fragment appears in about 16 consecutive spectra of
  every sweep in which its precursor elutes.
- Intensities are about 100 counts per ion, so a fragment is often a few ions per spectrum.
  Counting noise, not the arithmetic of the solve, is what limits demultiplexing here.

### The transmission (ZT Scan)

The kernel is the fraction of a precursor at m/z `m` that an encoded bin centered at `c` records,
as a function of `c - m`. It is **measured from the data**, not assumed (`ScanningKernel`):
- **Probes** are the 40 most intense peaks of each MS1 scan between 405 and 885 m/z. In the sweep
  that follows, a probe's own m/z survives unfragmented in every bin whose quadrupole position
  transmits it. Its intensity against `c - m` therefore traces the transmission, and no
  identifications are needed.
- Each probe's profile is taken over ±20 Th. Profiles whose maximum is under 10 times their median
  are dropped as noise. The median of the normalized profiles is taken on a 0.25 Th grid.
- **Measured on A1 at 3-8 min, from 10,168 probes:**
  - The shape is a trapezoid: a flat top about 4 Th wide and linear edges about 6 Th long.
  - Its width at half height is 10.55 Th, and it is zero outside -9.5 to +10.75 Th.
  - It is nearly symmetric, and the same within noise at low, middle and high m/z. The run's
    method states a Q1 width of 5.9 Th, about half what the quadrupole transmits.
- **An independent check from identified precursors:** the fragments of precursors that DIA-NN
  identified start to appear at about -8.5 Th and disappear at about +8.8 Th. That matches the
  kernel's support. These edges, where the signal starts and where it stops, are what locate a
  precursor within the sweep.

The kernel is a table, linearly interpolated, with values under 0.5% of its peak set to zero. It
is measured by a script in pwiz-ai (`ai/scripts/Osprey/Demux/Measure-ZtScanKernel.py`; the edge
check is `Measure-ZtScanEdges.py` beside it) and passed to the tool with `--kernel`. The C# per-file calibration is not written yet, so one kernel serves all three
replicates.

**The matrix** (`ScanningDemultiplexer.TransmissionMatrix`):
- Row i, column j is the transmission of a precursor at source position j into the spectrum of
  encoded bin i.
- A precursor can lie anywhere in its 1.18 Th position, so the kernel is averaged over 21 points
  across the position.
- The matrix is scaled so that a precursor at the center of its own bin averages 1.
- For staggered windows, A is 0/1 as in the overlap demultiplexer: a window transmits a bin
  entirely or not at all.

### Blocks

Each run is cut into independent blocks, which are solved in parallel:

| | ZT Scan | Staggered |
|---|---|---|
| Output a block owns | 16 encoded bins x 12 sweeps | 16 narrow bins x 12 cycles |
| Source positions (columns) | the owned bins, ±10 | the owned bins, ±max(2, 2k) |
| Spectra (rows) | the encoded bins within the kernel's reach of any column (9 more on each side) | every window that covers any column |
| Time context | ±4 sweeps | ±4 cycles |

- A block reads more than it owns, so the positions at its edges are solved with all the rows that
  see them. Every output peak belongs to exactly one block.
- The time context gives the channel histogram more evidence. For staggered data it also supplies
  the interpolation points.

### Fragment channels

`FragmentChannelFinder` decides which peaks, across a block's spectra, are the same product ion:
1. Every peak in the block goes onto 1 ppm bins of log m/z, weighted by its ions, and the
   histogram is smoothed over ±4 ppm.
2. Maxima are taken largest first. Each claims ±10 ppm around it, so smaller maxima inside that
   range are dropped. A center needs at least 8 ions within its smoothing width.
3. Each peak joins the nearest center within 10 ppm, the lower m/z on a tie.

**Why a histogram.**
- A real fragment recurs at nearly the same m/z in the ~16 spectra of each sweep that transmit
  its precursor, and in every sweep of its elution. It therefore stands out as a sharp maximum,
  even where unrelated peaks fall about every ppm (a ZT Scan spectrum has about 5,000).
- Linking neighboring peaks by their m/z gap does not work at that density: the whole spectrum
  chains into one channel.
- The overlap demultiplexer's choice, each target centroid as its own channel, suits Orbitrap
  spectra but not ZT Scan's density.

**Weak channels are not solved.** A channel with fewer than 8 ions in the block, or seen in fewer
than 3 (spectrum, sweep) cells, has too few counts to place. Its peaks pass through as acquired,
as do peaks near no center. On A1 that was 0.46% of the ions.

### The solve

For each channel, the solve runs once per sweep (ZT Scan) or at each acquired spectrum's time
(staggered):
1. **Observations.** y is the channel's ions in each row.
   - A ZT Scan sweep takes 0.86 s, so a sweep is treated as one moment and needs no interpolation.
   - For staggered data, each window's channel is interpolated by makima to the output spectrum's
     time, from that window's own acquisitions, and clamped at zero. The output spectrum's own
     window uses its measured value.
   - Only spectra in which the channel was observed are solved.
2. **Rows and columns.**
   - The columns are the positions that any row with signal can see.
   - The rows are every row that sees those columns, including rows where the channel is zero.
   - A zero is evidence that no source in the positions that row sees is present. On ZT Scan the
     zeros just before and after the signal are what fix a precursor's position.
3. **Unweighted fit.** min ||y - A x||² with x >= 0 (`NnlsSolver.SolveNormal`: Lawson-Hanson on
   AᵀA and Aᵀy).
4. **Poisson refit.**
   - With mu = A x from step 3, each row gets the weight 1 / max(mu_i, 0.5 ion), and the weighted
     fit min Σ w_i (y_i - (A x)_i)², x >= 0, is solved.
   - The variance of a count is its mean, so this weighs each spectrum by its precision. Rows
     crowded by several strong sources stop dominating the positions that a weak source's own rows
     inform.
   - The 0.5-ion floor keeps rows predicted near zero from taking unbounded weight.

In a simulation of 500 peptides in a realistic ZT Scan background, the weights cut the scatter
of the quantities of targets that did not change from 0.078 to 0.046. In the least abundant
third they cut it from 0.40 to 0.17. On real data they turned a 5-position layout from even with
the raw data into +5% identifications (below). The simulation is in pwiz-ai
(`ai/todos/active/TODO-20260923_osprey_demux/modeling-2026-09-26.md`).

**Implementation.**
- **Shared Gram matrix.** AᵀA over a channel's rows and columns is the same for every sweep. It is
  built once per channel from sparse rows, since a row sees only the positions within the kernel's
  reach.
- **Low-rank weight update.** Rows at or below the floor all share one weight, so the weighted
  Gram is that weight times AᵀA plus a correction from only the rows above the floor.
- **Incremental factorization.** The NNLS keeps a Cholesky factor of its passive set: it appends a
  row when a variable enters and refactors when one leaves.
- **Warm start.** The weighted refit starts from the unweighted solution's passive set.
- **Measured gain.** Together these made the solve about 3 times faster than the dense version, on
  the same machine and load.

### Output: staggered data

As in the overlap demultiplexer and msconvert, each acquired spectrum becomes one spectrum per
narrow bin its window covers. The ids take a ` demux=k` suffix, and each **observed peak is
apportioned**:
- Each peak is split among its window's bins by the solution's shares at that spectrum's time.
  The share of a bin is its transmission times its solved intensity, over the sum across the
  window.
- Each piece keeps the peak's own m/z.
- The solve decides only the split. The intensity and m/z come from what the instrument recorded
  in that spectrum, at that time.
- A solution of zero over the window drops the peak.
- Peaks of channels that pass through are shared equally among the window's bins.

For example, say a window covering bins A and B records a peak at 714.3524 with 10,800 counts,
and the solve assigns 3,000 to A and 7,000 to B:

| | bin A spectrum | bin B spectrum |
|---|---|---|
| solved values | 3,000 at the channel's mean m/z | 7,000 at the channel's mean m/z |
| apportioned (written) | 3,240 at 714.3524 | 7,560 at 714.3524 |

Writing the solved values instead lost 4.5% of precursors on the Orbitrap Eclipse data, for two
reasons:
- **Peaks the instrument cannot record.** The solve leaves small values wherever the fit puts a
  little intensity. Written down to 0.2 ions (20 counts), they doubled the peaks per spectrum,
  while the Orbitrap records nothing under about 780 counts. Apportioning can only divide peaks
  that were recorded.
- **Moved m/z.** A channel pools peaks across many spectra within 10 ppm. Writing each at the
  channel's mean moved 30% of the strong peaks by more than 3 ppm.

The cost is that the pieces always add up to what was measured. A mass-balance check therefore
cannot reveal a biased solve.

### Output: ZT Scan, and layouts

The solved intensity of each source position is written when it reaches `--min-out` ions. By default
it is written at the channel's intensity-weighted mean m/z over the whole block. With `--position-mz`,
each value is written at the m/z of the observed peaks it was solved from, in its own sweep: each
row's peaks count by the share of the row's modeled signal that position explains. That added about
4% identifications on the slice (below). The *layout* decides which positions each output spectrum
carries, and what isolation window it reports:

| Layout | Spectra per sweep | Isolation window written | Positions carried |
|---|---|---|---|
| `centered:k` (k odd) | one per encoded bin | that bin (1.18 Th) | the k positions centered on the bin |
| `tiled:k` | one per k bins | those k bins | those k positions |
| `framed:k:m` | one per k bins | those k bins | those k positions, plus m on each side |

- A window is the union of its bins' spans, so the windows tile the sweep without overlap. A
  framed spectrum carries m positions more on each side than its window.
- Within a spectrum, one channel's peaks from neighboring positions that lie within 5 ppm are
  summed at their weighted m/z. The pass-through peaks of its own bins are added as acquired.

**Why several positions per spectrum.**
- **One position fails.** The kernel's edges slope, so a precursor's column of A depends on where
  inside its 1.18 Th position it sits.
  - In simulation, 29% of a precursor's signal lands in a neighboring position even without
    noise.
  - Under counting noise, the split between neighbors flips from sweep to sweep.
  - On real data, one position per spectrum halved the identifications.
- **Several positions recover it.** A spectrum that carries the positions around a precursor
  collects the signal wherever the split put it. Of 5, 7 and 9, centered:7 did best on the slice.
  The window each spectrum reports is still its own 1.18 Th bin, so DIA-NN selects candidates at
  that width while the content carries 8.3 Th of demultiplexed signal.
- **Tiled windows lose precursors near their edges.** The signal that placement noise moved into
  the next position lands in the next tile. On the slice, tiled:5 lost about 9% against
  centered:5, and tiled:4 lost more.
- **framed:k:m keeps a margin but still tiles.** On the slice, framed:3:1 (3.5 Th windows, each
  carrying 5.9 Th of demultiplexed signal) matched centered:5's identifications and precision,
  with a third of its spectra and 2.8 times smaller files.
  - Over the whole A1 run it fell below the acquired data, with the output floor (27,009) and
    without it (26,991), where centered:5 gained 1.7% (below). The loss is the layout's, and it
    comes above 600 m/z.

**The output floor.** `--min-out 1` stops the tool writing solved values under one ion.
- On the slice it added 3-7% identifications, presumably because sub-ion values act as noise in
  DIA-NN's scoring.
- It raises the replicate CV from about 0.10 to 0.11-0.12, because the quantities of weak
  fragments are truncated.
- One way to get both is to search a file written with the floor and quantify from one written
  without it.

**Apportioning on ZT Scan** (`--apportion H`) scales each observed peak of a bin's spectrum by the
share of that spectrum's modeled signal from positions within ±H. It does worse on both counts
(below). An observed ZT Scan peak mixes about 16 positions and is often a few ions, so one peak
times a small share is a noisy estimate. The solved value pools every spectrum that saw the
source.

### Source positions

`--source-positions` changes what the columns of A are for each channel. By default every 1.18 Th
position within reach is a column, about 40 of them, and each sweep is solved separately. Counting
noise then spreads one precursor's signal over its neighboring positions. With source positions:
1. **The block's summed profile finds the sources.** The channel's counts summed over the block's
   sweeps are fitted on the bin columns, with the same Poisson-weighted refit. Each run of adjacent
   solved columns is one source, at its intensity-weighted center.
   - Sources closer than 0.8 Th merge.
   - Sources under 2 ions, or under 5% of the channel's total, are dropped.
   - At most 12 are kept, the largest.
   - `--source-l1 L` adds a non-negative lasso weight to this fit. With x >= 0 the L1 norm is
     linear, so it enters the normal equations as Aᵀy - L/2. As in the spec's §5.4d, its scale
     comes from the Poisson weights, so it is in the weighted fit's units.
2. **Each position is refined.** A 1-D search per source, ±0.6 Th in 0.1 Th steps and two passes,
   picks the position whose weighted fit of the summed profile has the smallest residual, with the
   kernel evaluated at the exact positions (in the matrix's scale, `KernelScale`).
3. **Each sweep is solved over just those sources**, typically two or three columns, then refitted
   with Poisson weights. The quantities carry no penalty.
4. **Each source is written to the encoded bin nearest its position**, at its own m/z with
   `--position-mz`.

The placement it gives was measured with the Python prototypes (`kernelpos.py`, `kernelpos2.py` in
pwiz-ai). For precursors DIA-NN identified in a sub-slice, the share of their top fragments' signal
near the apex that lands in their own bin went from 0.52 (the per-sweep solve) to 0.69. Within ±1
bin it was 0.85 and 0.86. Grouping fragments across a precursor, to place weak fragments by their
group, over-merged co-eluting precursors and is not implemented.

On the slice with DIA-NN's settings pinned, `--source-positions` with `--min-source-fraction 0` found
2,593 / 2,818 / 2,543 targets against the per-sweep solve's 2,909 / 3,067 / 3,094, with a worse CV: the
drop rule is not what costs it.

### Sparsity: the lasso

Each form below was tested on the slice (centered:7, `--position-mz`, DIA-NN pinned at `--window 6
--mass-acc 14 --mass-acc-ms1 17`), CV compared precursor by precursor on those every arm finds in all
three runs:

| Arm | Targets (A1 / D1 / G1) | Median paired change in CV | Precursors improved |
|---|---|---|---|
| no lasso (the per-sweep solve) | 2,909 / 3,067 / 3,094 | - | - |
| `--sweep-l1 2 --sweep-l1-refit` | 2,900 / 3,012 / 3,130 | +0.0060 | 33% |
| `--sweep-l1 6 --sweep-l1-refit` | 2,923 / 3,150 / 3,026 | +0.0040 | 40% |
| `--sweep-l1-z 2 --sweep-l1-refit` | 2,931 / 2,991 / 3,029 | +0.0003 | 49% |
| `--sweep-l1-z 3 --sweep-l1-refit` | 2,941 / 3,082 / 3,036 | +0.0099 | 36% |
| `--block-support-z 2` | 2,616 / 2,749 / 2,798 | +0.0113 | 38% |
| `--block-support-z 3` | 2,441 / 2,908 / 2,864 | +0.0011 | 49% |

None improves on the unpenalized solve. Changing DIA-NN's settings alone, on the same files, moves the
median paired CV by +0.0015 with 46.5% improved, so the losses above are the lasso's, not DIA-NN's.
- **A fixed weight is the wrong scale.** In the Poisson-weighted fit a position stays at zero while
  its score is within L/2; the score's noise is about sqrt(sum t^2 / mu), so a fixed L is a z threshold
  that loosens where the background is low (about 0.8 sigma at the weight floor for L = 6) and tightens
  where it is high. At L = 2 and 6 the output barely changes (5,839 peaks per spectrum against 5,860 and
  6,012). The z-scaled form, `--sweep-l1-z`, penalizes each position by its own noise: at z = 2 it
  writes 30% fewer peaks, at z = 3 66% fewer, removing nearly every sub-ion value.
- **Selecting per sweep makes chromatograms flicker.** A weak position passes in one sweep and fails in
  the next, which jitters its fragment's chromatogram: with the z-scaled weight, CV is unchanged at
  z = 2 and rises at z = 3. A fixed weight also acts in the unweighted fit, in ions, so it lowers the
  expected counts that set the Poisson weights, and the refit inherits the distorted weights.
- **Selecting per block removes the flicker but smears.** `--block-support-z 3` is CV-neutral, but
  constraining every sweep to the block's positions pushes the signal of the positions left out onto
  their neighbors (the files gain peaks and ions), and about 10% of identifications are lost.

`--counts-per-ion 50`, closer to the vendor centroids' scale (below), was neutral: 2,988 / 3,103 /
3,031 targets, paired CV +0.0022.

### Centroiding and the TOF grid

DIA-NN's advantage on quantities comes largely from reading the `.wiff` itself. Over the three whole runs
(19,656 precursors every arm finds in every run), its scanning mode gives a median CV of 0.090 on the
`.wiff`, 0.103 on msconvert's vendor-centroided mzML, and plain mode 0.113 on the mzML: of the 0.023
between plain and `.wiff`, the scanning algorithm accounts for 0.010 and the data path for 0.013, in
every RT and m/z cell and in every abundance quartile (0.011 to 0.014). The `.wiff` path also takes six
times longer to load, and DIA-NN measures wider peaks on it (3.00 scans against 2.81) at a looser mass
accuracy (24 ppm against 17).

Our reader and msconvert both use SCIEX's vendor centroids (`peakPicking vendor`). Compared with the
profile the SDK returns for the same spectra (A1 sweep 300, 169 spectra over 500-700 m/z):
- The elementary profile event is one digitizer sample of 100 counts. Every one-sample event is
  dropped by the vendor centroiding: 358,819 in the sweep, with the other peaks it leaves out 19.5% of
  the profile intensity. On their own m/z, 21.7% of those single events lie within 10 ppm of a kept
  centroid in the spectra 8 bins either side, against 14.0% at m/z shifted 0.37 Th: part of them are
  fragment ions.
- A kept centroid carries a median 0.25 of its profile peak's summed counts. Small centroids come in
  steps of about 50 per event (100, 150, 200, ...), never below 100.
- The vendor m/z agree with the profile's within 0.3 ppm in every run: the calibration is the same.

**The profile is one exact grid.** Every MS2 profile spectrum is sampled at m/z = (r0 + k x 9.786595e-5)^2,
uniform in sqrt(m/z), that is in flight time: 9.8 ppm per sample at 400 m/z, 7.4 at 700, 5.7 at 1200. The
step, and the samples, are identical to float precision across the spectra of a sweep, across sweeps
(A1 sweep 300 and 600) and across runs (A1 and D1). The TOF peak is close to Gaussian with a sigma of 1.2
samples at 150-400 m/z rising to 1.5 above 1000 (FWHM about 3 samples), with slightly heavier tails.
This is the condition that makes profile-domain demultiplexing and the joint solve of spec §5.4c-d
possible on ZT Scan without resampling or linking.

**Centroiding it ourselves, crudely.** `--centroid events` (`EventCentroider`) makes one centroid per run
of adjacent samples, split at deep valleys, keeping single events. Without demultiplexing, on the slice,
it lowered DIA-NN's CV (0.093 against 0.097 for the vendor centroids, 53.5% of precursors improved) but
lost identifications (2,534 / 2,129 / 2,417 against 2,659 / 2,694 / 2,627), and demultiplexed it lost
both. Part of that loss came from also centroiding MS1 this way (DIA-NN then recommended 31-38 ppm
MS1 tolerances, against 18-19), which `--centroid events` no longer does. The grouping has no peak
model: a sparse peak whose ions land on non-adjacent samples becomes several centroids.

**A first joint prototype** (pwiz-ai `joint_prototype.py`: Poisson-weighted NNLS over A kron B, B a
Gaussian TOF peak), scored by placement on 341 identified precursors (own bin / within one bin / median
per-precursor own share):

| Solve | Own | Within 1 | Median own |
|---|---|---|---|
| per-sweep channel solve, vendor centroids | 0.51 | 0.87 | 0.34 |
| per-sample profile demultiplexing (§5.4c) | 0.43 | 0.81 | 0.28 |
| joint, no L1 | 0.48 | 0.84 | 0.33 |
| joint, per-column L1 of 2 sigma (as `--sweep-l1-z 2`) | 0.49 | 0.85 | 0.35 |

The per-sample solve is clearly worst, as §5.4d argues. The Poisson-scaled L1 helps the joint solve, which
is then comparable to the channel solve on placement while carrying the full profile area, about four
times the vendor centroids' signal. Placement of identified precursors' strong fragments cannot show
what only the joint solve does, separating near-isobaric fragments of different precursors; that needs a
demultiplexed file searched with DIA-NN, and so a C# implementation.

### The joint solve (ZT Scan, `--joint`)

`JointDemultiplexer` solves spec §5.4d on the TOF grid: each sweep's profile counts are
y_r[k] = sum_j A_rj sum_q B[k - q] beta_j[q], with r an encoded bin, k a grid sample, A the measured
transmission, B the TOF peak and beta_j[q] >= 0 the signal of source position j at grid point q.
- **The fit.** Poisson weights 1 / max(mu, 0.5 ion), mu first from the data smoothed by B, then from the
  first solution's expected counts; the second, reweighted solve is essential (without it the answer moves
  0.24 in mean |log2| per peak). A lasso of 2 standard deviations per coefficient, lambda = 2 sqrt(c), with c
  the coefficient's curvature under the weights.
- **The solver.** Units of 48 bins and 12 sweeps, each sweep in chunks of 2,048 grid samples with margins.
  Block coordinate descent grid point by grid point: the positions active at a point solved together by
  NNLS on their block Hessian, the step over-relaxed by 1.5. The active set grows from the full gradient,
  checked four grid points per vector, and loses its zeros after every pass. Only grid points with data
  within a peak's reach are solved, and one is solved again only when a coefficient within two peak
  half-widths moved by more than 0.001 ion. After reweighting, only the points where some lambda moved by
  more than 20% are refitted (as Centrix refits only the regions whose lambda moved).
- **Centroids.** Each position's run of adjacent positive coefficients is one centroid at their
  beta-weighted m/z. In the layout, neighbouring positions' centroids within 2 sigma of the TOF peak, by
  Centrix's running-centre rule, are summed: a peak between two grid points or two positions otherwise
  leaves a close doublet, and DIA-NN quantifies a fragment from one peak.
- **Ion calibration** (spec §6.2, `IonCalibration`): the counts of one ion from the lowest intensity
  levels, by least squares on their spacing. MS2: one value per file (99.665 on ZT Scan, every level within
  0.25 of a multiple). MS1: one per spectrum (5.9 to 19.3 across a run, following the TIC), since SCIEX MS1
  intensities are rates over an accumulation time that varies.
- **MS1** (`--ms1 joint`, the default with `--joint`): centroided by the same solve, one bin and one
  position, on its own TOF grid (step 9.786586e-5, 0.45 of a sample off MS2's), with kernels measured
  from the strong isolated peaks of 30 MS1 spectra (sigma 1.62 samples at 475 m/z, 1.70 at 660).
- **The peak shape.** The Gaussian of the sigma table by default. `--peak-shape measured` (`TofPeakShape`)
  fits kernels averaged from a file's strong isolated MS2 peaks; they are sharper at the core and bring the
  solve closer to convergence, but on the slice they doubled the close doublets and found fewer peptides.

On the slice (sweeps 247-371, precursors 500-700 m/z, three runs, `centered:7`, DIA-NN at 14 / 17 ppm;
CV on the 2,011 precursors all four find in all three runs):

| Arm | Precursors A1 / D1 / G1 | Peptides in all runs / any | CV |
|---|---|---|---|
| per-channel solve, vendor centroids | 2,909 / 3,067 / 3,094 | 2,281 / 3,851 | 0.096 |
| joint, 1 sigma merge, vendor MS1 | 3,330 / 3,652 / 3,328 | 2,516 / 4,457 | 0.093 |
| joint, 2 sigma merge, vendor MS1 | 3,551 / 3,446 / 3,528 | 2,610 / 4,495 | 0.088 |
| **joint, 2 sigma merge, MS1 by the joint solve** | **3,586 / 3,775 / 3,589** | **2,698 / 4,698** | **0.090** |

With DIA-NN choosing its own tolerances the last arm finds 3,447 / 3,788 / 3,532 precursors against the
per-channel solve's 2,885 / 2,890 / 3,027, 2,651 peptides in all runs against 2,197, CV 0.091 against
0.095.

**Convergence.** Against the same 12 sweeps solved to convergence (637 passes), the default lands 0.022 in
ion-weighted mean |log2 ratio| of matched peaks from it with the Gaussian and 0.006 with the measured
kernels. Stopping earlier keeps the identifications but not the precision: 3 rounds and a relative
tolerance of 1e-3 raised the CV by 0.013-0.017, because the unconverged split of a peak between
neighbouring positions differs from run to run. `--joint-param BlockPoints=K` solves up to K = 5
neighbouring grid points per block step: 3 points land twice as close to convergence in 42% of the block
solves, but each block costs more and the solve takes longer.

**Speed.** One whole A1 run from the `.wiff2`, 16 threads on a laptop with 6 performance and 8 efficiency
cores: 6,438 s, a 16.8 GB mzML, against 14.4 min of acquisition. The solve took 96 thread-seconds a sweep:
block passes 53%, gradient checks 31%, weights and curvature 11%. Reading and writing are hidden behind
it.

### Determinism

- The rules are the overlap demultiplexer's: index order everywhere, ties to the lowest index, and
  capped NNLS iterations.
- Blocks are independent, and each writes only the output it owns.
- The tool solves a batch of blocks on worker threads while its calling thread reads the next
  batch. The source file is read from one thread only.
- The output is byte-identical at any thread count, and with or without that overlap.
- The joint solve's peak loops run as fixed-width four-lane vectors where the hardware has them, and
  otherwise as four scalar partial sums in the same lane order, reduced in one fixed order, so every
  machine gets the same bits.

### Speed

- **A full A1 ZT Scan run from .wiff2, centered:5, 10 threads on a shared machine: 8,906 s.**
  - Opening the file took 123-442 s, depending on load.
  - Reading spectra took 3,109 s, about 8-9 ms per spectrum through the SCIEX SDK.
  - Solving took 1,405 s. It now runs while the next batch is read.
  - Most of the rest was writing a 13.6 GB mzML.
- **framed:3:1 with the floor, on the current build and 4 threads: 3,657 s.**
- Inside Osprey, the demultiplexer would run from spectra Osprey has already parsed, as the overlap
  demultiplexer does, and the separate read and write would go away.

### Validation of the per-channel demultiplexer

**Unit tests** (`Osprey.Test/ScanningDemuxTest.cs`, and `DemuxTest` for the solver):
- **Channels:** a jittered fragment is one channel; fragments 30 ppm apart are two, and 6 ppm
  apart one; a weak lone peak is in none; and the peak order does not matter.
- **ZT Scan recovery, simulated through a trapezoid kernel:**
  - noiseless, every fragment returns exactly to its precursor's position, including one fragment
    two precursors share;
  - under Poisson noise, the three positions centered on each precursor hold its intensity within
    15%;
  - apportioning keeps an isolated source's peaks whole;
  - a lone weak peak passes through;
  - the result repeats exactly.
- **Staggered recovery:** two offset window sets acquired half a cycle apart. Each spectrum's
  peaks are apportioned exactly to their sources' bins, and only to its own window's bins.
- **Layouts:** the spectra each layout plans, the merging of neighboring positions, and parsing.
- **NNLS** on the normal equations, cold and warm-started, against the brute-force optimum.

**Orbitrap Eclipse** (EV13 and EV14, searched with Osprey as in [Validation](#validation);
counted together in one pass, below 1000.70 m/z):

| Demultiplexing | Experiment precursors | Entrapment FDP | Peptides |
|---|---|---|---|
| msconvert | 38,411 | 0.28% | 33,145 |
| overlap demultiplexer | 39,355 (+2.5%) | 0.27% | 33,905 |
| per-channel, solved values written | 36,670 (-4.5%) | 0.20% | 30,945 |
| **per-channel, apportioned** | **40,009 (+4.2%)** | **0.26%** | **34,501** |
| per-channel, apportioned, through `--demux auto` | 39,956 (+4.0%) | 0.26% | 34,483 |

- The same channels and weighted solve beat both, at equal FDP: 52 entrapment hits, against 54.
- The first four rows searched the tool's mzML; the last is Osprey demultiplexing the raw files itself
  with the weighted engine, the default. Its demultiplexed cache carries every peak of the tool's
  output unchanged; only record metadata differs (each bin keeps its parent's scan number, and its
  precursor m/z is the bin center), and the counts differ by run-to-run noise, with the same 52
  entrapment hits.
- It shares 31,327 peptides with msconvert, adds 3,174 of its own, and misses 1,818.

**ZT Scan, a slice** (A1, D1 and G1 at 4.1-5.9 min, 500-700 m/z; searched with DIA-NN 2.3.2
against a Carafe library with entrapment; CV of `Precursor.Quantity` over the 1,386 precursors
every arm found in all three runs):

| Arm | Target precursors (A1 / D1 / G1) | CV | A1 file |
|---|---|---|---|
| as acquired | 2,659 / 2,694 / 2,627 | 0.094 | - |
| as acquired, DIA-NN `--scanning-swath` | 2,768 / 2,807 / 2,958 | 0.095 | - |
| centered:5 | 2,712 / 2,804 / 2,701 | 0.099 | 1.51 GB |
| **framed:3:1** | **2,759 / 2,859 / 2,724** | **0.096** | **0.54 GB** |
| centered:5, `--min-out 1` | 2,907 / 2,928 / 2,817 | 0.117 | 0.57 GB |
| framed:3:1, `--min-out 1` | 2,778 / 2,892 / 2,907 | 0.112 | 0.23 GB |
| tiled:5 (Python prototype) | 2,508-2,590 | 0.127 | 0.30 GB |
| apportioned, ±2 positions | 2,592 / 2,773 / 2,828 | 0.136 | - |
| centered:7 | 2,738 / 2,856 / 2,994 | 0.094* | 1.84 GB |
| centered:9 | 2,617 / 2,766 / 2,991 | - | 2.26 GB |
| centered:5, `--position-mz` | 2,891 / 2,831 / 2,841 | 0.097* | - |
| **centered:7, `--position-mz`** | **2,770 / 2,950 / 2,959** | 0.095* | - |

\* On the 1,613 precursors those arms, the acquired data (0.099) and DIA-NN's scanning mode on the
acquired mzML (2,768 / 2,807 / 2,958; 0.097) all found in all three runs. centered:7 with
`--position-mz` identifies 1.7% more than DIA-NN's scanning mode on the slice, at lower FDP
(0.29-0.61%).

- Entrapment FDP was 0.2-1.2% in every arm. These counts are small, so a single run's FDP moves
  with a few hits.
- The C# and Python versions agree spectrum by spectrum (median cosine 0.9997, total intensity
  ratio 1.0001), and within 0-4% on identifications.
- Reading the .wiff2 directly gives the same spectra as msconvert's centroided mzML (cosine
  1.0000).

**ZT Scan, the whole A1 run** (DIA-NN as above; the numbers are DIA-NN's precursors at 1% FDR,
targets only):

| Arm | Target precursors | Entrapment FDP | Peptides |
|---|---|---|---|
| as acquired | 27,341 | 0.72% | 24,459 |
| as acquired, DIA-NN `--scanning-swath` | 29,373 | 0.84% | 26,273 |
| the vendor `.wiff`, DIA-NN `--scanning-swath` | 29,552 | 0.96% | 26,475 |
| centered:5 | 27,794 (+1.7%) | 0.88% | 24,973 |
| **centered:5, DIA-NN settings pinned** | **29,133 (+6.6%)** | **0.85%** | - |
| framed:3:1 | 26,991 (-1.3%) | 0.71% | - |
| framed:3:1, `--min-out 1` | 27,009 (-1.2%) | 0.90% | 24,163 |

- **Pin DIA-NN's settings on a demultiplexed file.** Left to choose, DIA-NN measured narrower peaks
  on the centered:5 file than on the acquired data (2.66 against 2.81 scans), so it picked a scan
  window radius of 5 instead of 6 and a 20 ppm fragment tolerance instead of 17. With
  `--window 6 --mass-acc 17 --mass-acc-ms1 19`, the acquired run's own choices, the same file gives
  29,133 targets: 1.4% short of DIA-NN's scanning mode on the vendor file, at lower FDP.
- **DIA-NN reads ZT Scan from `.wiff`, not `.wiff2`,** through SCIEX's Clearcore libraries. Its README
  says Scanning SWATH and ZT Scan should be read directly rather than from mzML; here the two gave
  nearly the same identifications.

- Over the whole run the gain of centered:5 shrinks to +1.7%, against about +5% on the slice.
- It is +3.8% in the slice's own region, and larger where the run is densest: 4-8 min at 500-700
  m/z. There, co-isolation is worst.
- Early in the gradient (2-4 min) it loses precursors, presumably because there is little
  interference to remove there while demultiplexing still adds noise.
- The identifications turn over far more than their count changes: 23,773 are shared with the
  acquired data, 3,568 are found only there, and 4,021 only after demultiplexing.
- framed:3:1 with the floor has a different pattern. It gains below 600 m/z from 2 to 8 min
  (+170 to +280 per 2-minute, 100 m/z cell). It loses above 600 m/z at nearly every retention
  time, by up to 189 per cell. The slice (500-700 m/z) straddles that boundary and nets a gain.
  The cause of the loss is not yet known.
- Of the precursors DIA-NN's scanning mode finds and the centered:5 file misses, 56% are in the
  lowest abundance quartile: the remaining identification gap is in weak precursors.

**Three replicates, whole runs** (CV of `Precursor.Quantity` on the 17,948 precursors all five arms
found in all three runs):

| Arm | Targets (A1 / D1 / G1) | Entrapment FDP | CV |
|---|---|---|---|
| as acquired, DIA-NN's own settings | 27,341 / 27,952 / 28,470 | 0.72-0.88% | 0.112 |
| as acquired, DIA-NN `--scanning-swath` | 29,373 / 29,872 / 30,485 | 0.81-0.84% | 0.102 |
| the vendor `.wiff`, DIA-NN `--scanning-swath` | 29,552 / 30,285 / 30,882 | 0.90-0.96% | 0.088 |
| centered:5, DIA-NN's own settings | 27,794 / 28,144 / 28,843 | 0.88-0.90% | 0.134 |
| **centered:7, `--position-mz`, DIA-NN pinned** | **31,512 / 31,506 / 32,117** | 0.79-0.89% | 0.119 |

- **Identifications: the demultiplexed runs lead.** centered:7 with `--position-mz`, searched with
  `--window 6 --mass-acc 17 --mass-acc-ms1 19`, finds 4.0-6.6% more targets than DIA-NN's scanning mode
  on the vendor file and 13-15% more than the acquired data, at lower FDP, and 24,600 precursors in all
  three runs against 23,589. In peptides: 28,171 / 28,188 / 28,729 against 26,475 / 27,174 / 27,635 for
  the `.wiff` (+3.7-6.4%), at 0.89-1.00% FDP against 1.01-1.07%.
- **Quantities: still behind.** Pinning and the layout moved the CV from 0.134 to 0.119, but the
  acquired data gives 0.112 and the `.wiff` 0.088. By abundance quartile the demultiplexed CV equals
  the acquired data's at the top (0.093) and exceeds it by 0.010 at the bottom (0.148 against 0.138),
  and the excess grows with m/z: the solve adds counting noise to weak signal. The `.wiff`'s lead over
  the acquired data (0.013-0.018 in every quartile) is the data path ([Centroiding and the TOF
  grid](#centroiding-and-the-tof-grid)), which the demultiplexer, starting from the same vendor
  centroids, inherits.
- On the slice the demultiplexed CV matched the acquired data's, so the slice does not show either.
- `--source-positions` was built for the quantitation gap (two or three columns per sweep instead of
  about 40). On the slice it loses both identifications and precision, with its drop rule removed and
  DIA-NN pinned too ([Source positions](#source-positions)). It stays off by default.

The scripts behind these ZT Scan tables are in pwiz-ai, under `ai/scripts/Osprey/Demux`.

## Status and limitations

- **Supported (`--demux auto`):** stepped overlapping windows (staggered DIA) at any overlap factor, including
  variable widths, from centroided data (vendor centroiding, or a centroided mzML), by the
  per-channel demultiplexer (or the overlap demultiplexer, with `OSPREY_DEMUX_ENGINE=msconvert`).
- **Supported in `Osprey.DemuxTool` only:** the per-channel demultiplexer for SCIEX ZT Scan, and
  the joint solve for ZT Scan. Wiring them into `--demux` still needs:
  - the .wiff2 reader staged for Osprey.exe;
  - the transmission calibrated per file, in C#;
  - for the joint solve, the profile, which the `.spectra.bin` does not hold;
  - its descriptor in the demultiplexed cache.
- **Open questions for ZT Scan:**
  - quantitation over whole runs: the per-channel solve's quantities are noisier than the acquired
    data's (CV 0.119 against 0.112, with DIA-NN pinned), and DIA-NN's scanning mode on the `.wiff`
    reaches 0.088. The joint solve, which carries the profile signal the vendor centroids drop, beats
    the per-channel solve on the slice in identifications and CV
    ([The joint solve](#the-joint-solve-zt-scan---joint)); its whole-run searches are the next measure;
  - the joint solve's speed: a whole run takes 1 h 47 min on a 16-thread laptop, against 14.4 min of
    acquisition;
  - fragment interference: DIA-NN picks candidates at the 1.18 Th bin, but each `centered:7` spectrum
    carries 8.3 Th of positions, and a narrower layout has not been tried with the joint solve;
  - how to recover the early-gradient losses, for instance by scoring a precursor against both the
    acquired and the demultiplexed spectra.
- **MSX is not supported, and is misread today.** The reader keeps only a spectrum's first
  precursor, so each multiplexed spectrum is treated as a single window, and nothing refuses such
  a file yet. Support needs every precursor and its own fill time, which the ProteoWizard Thermo
  reader does not yet report.
- **Scanning quadrupole in `--demux auto`: not supported.** Their bins are reported at their
  nominal width, while the quadrupole transmits about ten times that, so they need the measured
  transmission of the per-channel demultiplexer rather than 0/1 windows. Waters SONAR has not been
  tried.
- **Unit-resolution data** (Stellar centroids) is demultiplexed with the same fixed 10 ppm channel
  tolerance, which is not right for it and has not been validated.
- **Isotope envelopes straddle narrow bins.** A precursor's M+1 and M+2 can fall in the bin above
  its monoisotopic one. Scoring still looks only in the bin that contains the monoisotopic m/z.
- **No fragment coupling.** In both demultiplexers each fragment channel is solved on its own;
  fragments of one precursor do not constrain each other.
