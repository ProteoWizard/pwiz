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

**Where it stands.** This is the first step: reproducing, inside Osprey, the overlap
demultiplexing msconvert does (`--filter "demultiplex optimization=overlap_only"`). It reads the
raw file directly and caches the result as Osprey spectra. The goal beyond it is one
demultiplexer for every compressed-sampling scheme, where each measured spectrum mixes several
precursor bins in known proportions:
- staggered windows at any overlap (k = 2, 3, 4) and variable widths, supported now;
- Thermo MSX (random multiplexed co-isolation);
- comb (parallel isolation) acquisitions;
- scanning-quadrupole methods: SCIEX ZT Scan on the ZenoTOF, and Waters SONAR;
- profile-domain demultiplexing with Osprey's own centroiding, for the Stellar.

Only the first is implemented. See [Status and limitations](#status-and-limitations).

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
   That is enough on its own, so a cohort staged with demultiplexing can be searched after its
   `.spectra.bin` and its sources are gone.
2. **A valid `<stem>.spectra.bin`.** The isolation scheme is detected from its window index. A
   non-overlapping run is searched from it as before. An overlapping run is read back in full,
   demultiplexed, and written to `<stem>.demux.spectra.bin`, which is then searched.
3. **Neither.** The source is parsed, `<stem>.spectra.bin` is written exactly as without demux,
   and the spectra still in memory are demultiplexed without being read back.

`PerFileRescoring` (Stage 6) reads the same `<stem>.demux.spectra.bin`. If only `.spectra.bin` is
available and its windows overlap, rescoring stops with an error, because the earlier stages
searched the demultiplexed spectra.

The log records the scheme found, the solver's work and the timing:

```
Demultiplexing Ecl_..._10.raw: 2-fold overlap, 101 windows into 102 bins of 6.000-6.003 Th
  79,350 MS2 spectra -> 158,700; 69,188,527 channel solves (0.0% zero, 0.0% unconstrained, 100.0% active set, 0 at the iteration cap) over 2 block geometries
  Demultiplexed in 18.8s on 16 thread(s); parse 191.0s, ratio 0.10; osprey-demux/3;block=covered_bins;...
```

The last line is the timing gate for the implementation: demultiplexing as a fraction of the raw
parse it follows. On the Orbitrap Eclipse data that fraction is 0.10-0.13, with scalar code.

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
osprey-demux/3;block=covered_bins;interpolation=makima;output=apportioned;channel_tolerance=10ppm;min_bin_width=0.2;block_bins=7
```

The descriptor names the algorithm version and every setting that changes the output. A cache
whose descriptor differs from the current one is rejected and rebuilt from `.spectra.bin`, in
seconds, as it is when its source file changes. The thread count is not part of it, because it
never changes the output.

The descriptor is also part of every task's validity key (`DemuxCacheBuilder.ValidityKeySuffix`),
so the scores and FDR results computed from the old spectra are recomputed along with the cache.
The search hash records only `demux:auto`, because `Osprey.Core` cannot see the descriptor. With
demux off, neither the key nor the hash changes.

## The algorithm

For each acquired MS2 spectrum (the *target*), independently and in parallel:

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

For attributing a difference from msconvert, and nothing else, three environment variables
override the defaults:
- `OSPREY_DEMUX_BLOCK` = `covered_bins` | `truncated_slice`;
- `OSPREY_DEMUX_INTERPOLATION` = `makima` | `pchip` | `natural_three_point` | `linear`;
- `OSPREY_DEMUX_OUTPUT` = `apportioned` | `solution`.

`truncated_slice` with `natural_three_point` is msconvert's configuration. Each value is part of
the descriptor, so a cache built under an override is rebuilt without it.

## Validation

**Unit tests** (`Osprey.Test/DemuxTest.cs`, about a second together):
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
  Eclipse staggered run, and msconvert's demultiplexing of the same slice. The test pins Osprey's
  output against a committed per-bin golden and its agreement with msconvert against measured
  floors. How the slice was made, and how to rebless the golden, is in the folder's README.

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

## Status and limitations

- **Supported:** stepped overlapping windows (staggered DIA) at any overlap factor, including
  variable widths, from centroided data (vendor centroiding, or a centroided mzML).
- **MSX is not supported, and is misread today.** The reader keeps only a spectrum's first
  precursor, so each multiplexed spectrum is treated as a single window, and nothing refuses such
  a file yet. Support needs every precursor and its own fill time, which the ProteoWizard Thermo
  reader does not yet report.
- **Scanning quadrupole (ZT Scan, SONAR) is not supported.** Their bins are reported at their
  nominal width, while the quadrupole transmits about ten times that. They need a transmission
  model fitted from the data rather than 0/1 windows.
- **Unit-resolution data** (Stellar centroids) is demultiplexed with the same fixed 10 ppm channel
  tolerance, which is not right for it and has not been validated.
- **Isotope envelopes straddle narrow bins.** A precursor's M+1 and M+2 can fall in the bin above
  its monoisotopic one. Scoring still looks only in the bin that contains the monoisotopic m/z.
- **No fragment coupling.** Each fragment channel is solved on its own; fragments of one precursor
  do not yet constrain each other.
