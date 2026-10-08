# Demultiplexing test fixture: Orbitrap Eclipse staggered DIA

Used by `DemuxTest.TestDemuxEclipseFixture`. A small slice of a real staggered acquisition, so
the demultiplexer is tested on real centroids (with their ppm jitter), real window geometry and
real elution, which synthetic data does not reproduce.

| File | What it is |
|---|---|
| `eclipse-ev13-staggered-slice.mzML` | The acquired spectra: 204 MS2 scans, 8 staggered 12 Th windows (centers 502.48-544.50 m/z, 496.5-550.5 m/z in all), 45-48 min |
| `eclipse-ev13-msconvert-demux-slice.mzML` | msconvert's demultiplexing of exactly that slice: 408 spectra in 9 bins of 6 Th |
| `eclipse-ev13-osprey-demux.golden.tsv` | Osprey's default demultiplexing of the slice, summarized per bin; the test pins against it |

Source run: `Ecl_2022_0705_Beads_EV13_SAXN_12mz_10.raw` (Orbitrap Eclipse, plasma EVs on beads,
12 Th windows staggered by 6 Th, MacCoss lab). The whole run has 101 windows over 394-1006 m/z.

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
channel tolerance.

**Regenerating the golden.** It changes only when the demultiplexer's output is meant to change.
Set `OSPREY_REBLESS_DEMUX_FIXTURE=1`, run `TestDemuxEclipseFixture` (it rewrites the TSV and
fails, so a rebless is never silent), review the diff, and say why in the commit.
