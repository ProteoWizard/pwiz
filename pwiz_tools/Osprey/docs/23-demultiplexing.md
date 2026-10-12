# 23. Demultiplexing overlapping-window DIA (C#)

Osprey-only: there is no Rust counterpart, so like [00](00-pipeline-architecture.md) and
[21](21-user-facing-text.md) this document has no "Divergences" section.

**What it is for.** Some DIA methods sample each narrow precursor range more than once, through
different isolation windows. The common case is **staggered DIA**: two sets of, say, 12 Th
windows offset by half a window, so every 6 Th bin is covered by one window from each set.
Demultiplexing recovers the narrow bins from the wide windows. Searching the result gets the bin
width's precursor selectivity; searching the acquired spectra gets the window width's, and scores
every precursor in two windows.

**Osprey does no demultiplexing of its own.** With `--demux auto` the run is read through
ProteowizardWrapper's `MsDataFileImpl` with ProteoWizard's demultiplexer applied
(`pwiz/analysis/SpectrumListDemux.cs`, the C# port of msconvert's `demultiplex` filter). The
vendor file goes straight to a demultiplexed `<stem>.demux.spectra.bin`: no mzML is written, and
the run is read once.

## Using it

```
osprey -i run1.raw -i run2.raw -l library.tsv -o results.blib --resolution hram --demux auto
```

| `--demux` | What happens |
|---|---|
| `off` (default) | Spectra are searched as acquired. A multiplexed run is still detected when its spectra cache is built from the source, and a warning names the file and suggests `--demux auto`: searched as acquired, a precursor can be scored in more than one window. |
| `auto` | Each run is checked for a multiplexing scheme. A multiplexed run is demultiplexed and the result searched; any other run is searched as acquired, so `auto` is safe to pass for every run. |

The option takes no tuning parameters. Every task of an HPC chain must be given the same
`--demux` value (see [Settings and validity](#settings-and-validity)).

An mzML that msconvert has already demultiplexed also works, with `--demux off`: its bins do not
overlap, so it is not detected as multiplexed and is searched as it is.

## Where it runs in the pipeline

Demultiplexing happens where the spectra cache is built, in `PerFileScoring` (Stage 2) or
`--task SpectraCache`, both through `ScoringTaskShared.EnsureSpectraCache`. With `--demux auto`
it looks for, in order:

1. **A valid `<stem>.demux.spectra.bin`** (`DemuxCache.TryOpen`): magic, version, source
   fingerprint and descriptor all match. It is searched and nothing else is read, so a run
   staged demultiplexed can be searched after its source and `.spectra.bin` are gone.
2. **A valid `<stem>.spectra.bin`**, used only when the source shows the run is not
   multiplexed (`DemuxCache.IsDemultiplexedRun`, which reads the metadata of the first cycles).
   The `.spectra.bin` of a multiplexed run, written by an earlier search without `--demux`, is
   passed over.
3. **The source.** `SpectrumFileReader.LoadAllSpectra` opens it with the demultiplex settings.
   A multiplexed run is read demultiplexed and written to `<stem>.demux.spectra.bin`; any other
   run is read as acquired and written to `<stem>.spectra.bin`, exactly as without `--demux`.

The log line for a demultiplexed read gives the scheme and the result, for example
`Demultiplexed EV13.raw: 101 isolation windows per cycle into 102 narrower windows,
158,700 MS/MS spectra`.

The demultiplexed cache is built **from the source**, never from the `.spectra.bin`: a
demultiplexed read is a different read of the run, with the demultiplexer working on the
centroided spectra as the reader decodes them. So it can only be rebuilt while the source exists.

## Detection

Detection is ProteoWizard's, `SpectrumListDemux.DetectScheme`. It builds msconvert's
`PrecursorMaskCodec` from the metadata of the first cycles (no peaks are decoded) and reports a
scheme only when both hold:

- the run has more than one precursor per MS2 spectrum (MSX), or its isolation windows overlap
  (each window covers more than one demux window), and
- every MS2 spectrum of two cycles at the start, middle and end of the run maps onto the same
  number of demux windows, so a method whose windows change partway is not multiplexed.

The second condition separates staggered windows from ordinary DIA whose neighbouring windows
share a margin of 0.5-1 Th. In such a run the margins become thin demux windows covered twice,
but each body window then covers three demux windows and each edge window two. msconvert's own
demultiplexer would refuse that run spectrum by spectrum; Osprey searches it as acquired.

A run with no MS2, no repeating cycle, a varying number of precursors per spectrum, or SRM
spectra is not multiplexed.

The same detection answers for a `.spectra.bin` whose source is not at hand (a run cached
without `--demux`, then searched with it): it runs over the isolation window the cache recorded
for every MS2 spectrum, sampling the same start, middle and end. A cache records one window per
spectrum, so an MSX run cannot be recognized this way (see [Limitations](#limitations)).

## Relationship to msconvert

The spectra in `<stem>.demux.spectra.bin` are those msconvert writes for

```
msconvert run.raw --filter "peakPicking vendor msLevel=1-" --filter "demultiplex optimization=overlap_only massError=10.0ppm interpolation=makima poissonWeights=true"
```

Osprey requests vendor centroiding for both MS levels, and `MsDataFileImpl` applies the
demultiplexer after centroiding (and lockmass correction), in msconvert's filter order. The
settings (`DemuxCache.CreateParams`) are msconvert's defaults with `optimization=overlap_only`, a
10 ppm mass error, and two options from Mike MacCoss's demultiplexer in #4710, added to
ProteoWizard by pwiz #4827: makima interpolation, and a Poisson-weighted refit of each NNLS
solution (row weights 1 / max(A x, floor) from the unweighted fit). The refit is what #4710's
identification gain came from (see [Results](#results)). For an MSX scheme `MsDataFileImpl` switches to ProteoWizard's MSX
demultiplexer, as `Scheme.Optimization` selects.

The one difference is speed. The solve was made parallel on this branch (below); the output is
byte-identical at any thread count, and so is the same as single-threaded msconvert's.

## Files

| File | Written | Contents |
|---|---|---|
| `<stem>.spectra.bin` | a run that is not multiplexed, or any run with `--demux off` | The spectra as acquired. |
| `<stem>.demux.spectra.bin` | `--demux auto` on a multiplexed run | The demultiplexed spectra: one record per demux window per acquired MS2 spectrum, with its parent's scan number and retention time and the demux window as its isolation window. MS1 spectra pass through unchanged. |

Both live in the cache directory (`--cache-dir`, else `--work-dir`, else beside the source; see
[00](00-pipeline-architecture.md)). The byte layout is in
[14](14-intermediate-files.md#demultiplexed-spectra-cache-stemdemuxspectrabin): the plain
VERSION 4 layout with the magic `OSPRDMX\0` and a descriptor after the header.

`SpectraWindowIndex.IsolationWindows` lists every distinct isolation window in the run, each as
its first record's window, sorted by center. It used to take only the first cycle's windows, up
to the first repeated key. That is the same set for ordinary DIA, but a demultiplexed run's top
bin first appears after the cycle repeats, so the first-cycle list left it unscored.

## Settings and validity

The **descriptor** (`DemuxCache.Descriptor`) names the descriptor version and every setting that
changes the demultiplexed spectra. With the current settings it is:

```
pwiz-demux/1;massError=10Ppm;interpolateRT=True;weighting=True;sumNormalize=True;variableFill=False;nnlsMaxIter=50;nnlsEps=1E-10;blockExtra=0;minWindowSize=0.2;removeNonOverlappingEdges=False;interpolation=Makima;poissonWeights=True
```

The thread count is left out, because the output does not depend on it. `DESCRIPTOR_VERSION` is
bumped when ProteoWizard's demultiplexer changes its output under the same settings.

- A cache whose descriptor differs is refused (`DemuxSettingsChanged`) and, while the source
  exists, rebuilt from it. A plain cache read where a demultiplexed one is expected, or the
  reverse, is refused the same way.
- The descriptor is part of every task's validity key (`DemuxCache.ValidityKeySuffix`), so the
  scores and FDR results computed from the old spectra are recomputed with the cache.
- The search hash gets `demux:auto` only when `--demux auto` is given, because `Osprey.Core`
  cannot see the descriptor. With `--demux off`, neither the key nor the hash changes, so
  existing hashes, intermediate files and the cross-implementation match with Rust are
  unaffected.

## Rescoring and HPC staging

- **Start-up.** An input whose source is missing is accepted when its `.spectra.bin` exists, or
  when its `<stem>.demux.spectra.bin` is valid under the current settings. A demultiplexed cache
  that exists but is stale or was written with other settings, with no `.spectra.bin` beside it,
  is an error at start-up rather than hours in: it can be rebuilt only from the source, and the
  message says to restore it.
- **`PerFileRescoring`** (and the training export) read the same `<stem>.demux.spectra.bin`
  Stages 1-4 searched (`ScoringTaskShared.LoadSpectraForRescore`). If only the `.spectra.bin`
  is found while the source shows the run is multiplexed, rescoring stops with an error naming
  `PerFileScoring` and `--demux auto`.
- **Relay.** A multiplexed run travels with its `<stem>.demux.spectra.bin`; it alone is enough
  for every task while its descriptor matches. Keep the source of any cohort that may be searched
  again by a later Osprey: a descriptor change can be absorbed only by re-reading it.

## Performance

One Orbitrap Eclipse staggered run (EV13: 1.24 GB `.raw`, 101 x 12 Th windows at 2-fold
overlap, demultiplexed into 102 bins of 6 Th and 158,700 MS2 spectra), vendor file to spectra
cache (`--task SpectraCache --demux auto`), on an idle 32-thread machine, five runs each,
interleaved, median:

| Demultiplexer | Time | Peak working set |
|---|---|---|
| ProteoWizard's, makima + Poisson refit (`--demux auto`) | 19.6 s | 4.7 GB |
| ProteoWizard's, msconvert's default interpolation (natural spline) | 23.2 s | 4.4 GB |
| #4710, default (weighted) engine | 43.7 s | 9.1 GB |
| #4710, msconvert-style engine (2026-10-10, three runs) | 21.9-23.1 s | 3.0 GB |
| msconvert's demultiplexer before these changes, single-threaded | 214.6 s | |

`SpectrumListDemux` solves ahead when `SolveThreads` is above 1 (Osprey passes its thread count):
blocks are solved in batches on worker threads while the reader consumes the batch before, the
spectra of the next batch are read while this one is solved, and the spectrum cache is sized
from the cycle so no spectrum is decoded twice. Each block depends only on its source spectrum,
so the output is byte-identical at any thread count.

Until pwiz #4817 the read took 40 s: the Thermo reader's precursor lookup walked back to the
first scan of the run for every spectrum carrying "Master Scan Number: 0" (every DIA scan), 22 s
of the serial read path. Makima is faster than the MathNet natural spline it replaces here
(MathNet builds a general spline for every value); the refit adds about 1.4 s.

## Results

Six Eclipse staggered runs per set, three Percolator seeds per demultiplexer at a 1.5M-row
training cap (at the default 300k cap the seed alone moves a search 6-11%, pwiz #4812),
experiment level at q <= 0.01, entrapment FDP from 1:1 entrapment. Mean precursors (entrapment
FDP range):

| Demultiplexer | Webinar 27 runs, Mike MacCoss's library | EV13-15 + Total01-03, human Carafe library |
|---|---|---|
| ProteoWizard's, `--demux auto` (makima + Poisson refit) | **43,514** (0.38-0.40%) | **39,033** (0.44-0.47%) |
| ProteoWizard's, msconvert's settings | 42,188 (0.32-0.37%) | 37,611 (0.47-0.52%) |
| #4710, weighted | 43,481 (0.32-0.40%) | 37,171 (0.47-0.57%) |
| #4710, weighted with its Poisson refit switched off | 42,260 (0.30-0.34%) | |
| #4710, msconvert-style | 42,767 (0.38-0.41%) | 37,284 (0.43-0.48%) |

The Poisson refit alone gives +2.3% and +2.4%, and makima adds +0.8% and +1.3%. Replicate
precision is unchanged (median peak-area CV over EV13-15 0.028, against 0.029 with msconvert's
settings and 0.030 for #4710 weighted). Measured on the runs' own fragment chromatograms,
makima's apex bias equals the 3-point natural spline's; its advantage is lower noise.

## Limitations

- **MSX** (several precursors per spectrum) is detected and demultiplexed by ProteoWizard's MSX
  demultiplexer, but has not been tested on real data. An MSX run cached without `--demux` is
  not recognized from its `.spectra.bin` alone, so with `--demux auto` it is searched as
  acquired from that cache; delete the cache, or keep the source, to have it demultiplexed.
- **Scanning quadrupole** (SCIEX ZT Scan, Waters SONAR) is not supported: ProteoWizard has no
  scanning demultiplexer.
- **The Thermo reader decodes on one thread**, so vendor decoding is serial under the parallel
  solve (see [Performance](#performance)).
- **Unit-resolution data** (Stellar centroids) is demultiplexed with the same 10 ppm mass error,
  which has not been validated.
- **No rebuild without the source.** Unlike #4710's cache, the demultiplexed cache cannot be
  derived from the `.spectra.bin`.

## Relationship to #4710

ProteoWizard/pwiz PR #4710 (Mike MacCoss) added `--demux` with its own demultiplexer library
(`Osprey.Demux`), which builds `<stem>.demux.spectra.bin` from the `.spectra.bin`. This branch
uses ProteoWizard's demultiplexer instead, applied while the vendor file is read. Ported from
#4710, by Mike MacCoss:

- the `--demux off|auto` option, and `demux:auto` in the search hash only when enabled;
- the demultiplexed cache contract: the `<stem>.demux.spectra.bin` name, the `OSPRDMX\0` magic
  and descriptor in the header, the descriptor in every task's validity key, and the start-up
  check of a demultiplexed cache standing in for a missing source;
- the `SpectraWindowIndex` fix that lists every distinct isolation window, not only the first
  cycle's;
- in ProteoWizard (pwiz #4827): the Poisson-weighted NNLS refit and makima interpolation, which
  `--demux auto` uses.

Two behaviours differ: with `--demux off` a multiplexed run is searched as acquired with a
warning, where #4710 refuses it; and the demultiplexed cache is built from the source, where
#4710 derives it from the `.spectra.bin`.
