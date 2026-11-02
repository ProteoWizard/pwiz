# Demultiplexing test fixture: Orbitrap Eclipse staggered DIA

Used by `DemuxTest`. A small slice of a real staggered acquisition, so `--demux auto` is tested
on real centroids (with their ppm jitter), real window geometry and real elution, which synthetic
data does not reproduce. The fixture was made by Mike MacCoss for ProteoWizard/pwiz PR #4710 and
is reused here unchanged.

| File | What it is |
|---|---|
| `eclipse-ev13-staggered-slice.mzML` | The acquired spectra: 204 MS2 scans, 8 staggered 12 Th windows (centers 502.48-544.50 m/z, 496.5-550.5 m/z in all), 45-48 min |
| `eclipse-ev13-msconvert-demux-slice.mzML` | msconvert's (C++) demultiplexing of exactly that slice: 408 spectra in 9 bins of 6 Th |

Source run: `Ecl_2022_0705_Beads_EV13_SAXN_12mz_10.raw` (Orbitrap Eclipse, plasma EVs on beads,
12 Th windows staggered by 6 Th, MacCoss lab). The whole run has 101 windows over 394-1006 m/z.

## How the tests use it

Osprey does no demultiplexing of its own: with `--demux auto` it reads a multiplexed run through
ProteoWizard's C# `SpectrumListDemux`, the port of the C++ filter that wrote the msconvert file
above. `DemuxTest` checks that:

- `SpectrumFileReader.DetectDemultiplexScheme` finds the staggered scheme in the slice (8 windows
  per cycle, each covering 2 of 9 demux windows);
- `SpectrumFileReader.LoadAllSpectra` with demultiplexing gives exactly what `SpectrumListDemux`
  gives reading the slice directly, and the same 408 spectra, isolation windows and retention
  times as the C++ msconvert file. Peaks are not compared with C++: the C# demultiplexer searches
  each bin's full tolerance window where C++ searches only part of it (pwiz #4814). Its parity
  with C++ otherwise is tested in `pwiz/analysis` (`SpectrumList_DemuxTest.data`);
- without demultiplexing the slice is read as acquired and reported as multiplexed;
- `ScoringTaskShared.EnsureSpectraCache` with `--demux auto` writes the slice to
  `<stem>.demux.spectra.bin`, reuses it, and rebuilds it in place of a plain `.spectra.bin`.

Made with ProteoWizard msconvert 3.0.26100.9783a03 (64-bit, C++):

```
msconvert Ecl_2022_0705_Beads_EV13_SAXN_12mz_10.raw --zlib --simAsSpectra
    --filter "peakPicking vendor msLevel=1-"
    --filter "scanTime [2700,2880]"
    --filter "mzPrecursors [502.4783,508.4810,514.4838,520.4865,526.4892,532.4920,538.4947,544.4974] target=isolated"
    --filter "threshold count 400 most-intense"
    --chromatogramFilter "index 999999"
    --outfile eclipse-ev13-staggered-slice.mzML

msconvert eclipse-ev13-staggered-slice.mzML --zlib
    --filter "demultiplex optimization=overlap_only massError=10.0ppm"
    --chromatogramFilter "index 999999"
    --outfile eclipse-ev13-msconvert-demux-slice.mzML
```

The 400-most-intense threshold and the dropped chromatograms keep the fixture about 5 MB.
msconvert demultiplexed the thresholded slice itself, so the two files are a like-for-like pair.
Binary arrays are 32-bit, which quantizes m/z by about 0.06 ppm at 500 m/z, far below the 10 ppm
demultiplexing tolerance.
