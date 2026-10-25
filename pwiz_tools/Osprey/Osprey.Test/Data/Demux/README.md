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

# Real-data fixtures for every demultiplexing path

Used by `DemuxFixtureTest`. One slice per acquisition type, each through the path Osprey or the tool runs it on,
with its output pinned to a golden: one line per output spectrum, its source scan, window, peak count, summed
intensity and intensity-weighted mean m/z. The scan, window and peak count must match exactly; the two sums to a
relative 1e-9. The solvers give the same bits on every machine (the joint solve's vector sums are reduced in a fixed
lane order), and the output does not depend on the thread count, so a refactor reproduces every line. A changed
setting does not: run with `OSPREY_DEMUX_MIN_CHANNEL_IONS=1`, the Astral test fails on its first spectrum.

| File | What it is |
|---|---|
| `astral-me-staggered-slice.mzML` | Orbitrap Astral, 4 Th windows staggered by 2 Th: 344 MS2 spectra, 8 windows (centers 594.52-608.53 m/z), 15.0-17.0 min, the 500 most intense peaks of each, with fill times and the Astral analyzer |
| `astral-me-osprey-demux.golden.tsv` | Osprey's `--demux auto` of it: 688 spectra in 9 bins of 2 Th |
| `stellar-superoverlap-profile-slice.mzML` | Thermo Stellar SuperOverlap, profile: 200 MS2 spectra, 8 windows of 4 Th in four passes 1 Th apart (centers 600.52-607.53 m/z), 10.0-12.0 min, fragments 450-750 m/z on the 0.2 Th grid, zero points present, with fill times and the ion trap analyzer |
| `stellar-superoverlap-joint-demux.golden.tsv` | The staggered joint solve, split and Centrix (`--scheme staggered --profile --joint --ions`): 800 spectra in 11 bins of 1 Th |
| `ztscan-a1-profile-slice.mzML` | SCIEX ZT Scan (ZenoTOF 8600), profile: six whole sweeps (MS1 at 300.062-304.916 s and their 35 encoded bins 584.83-625.00 m/z, 210 MS2 spectra), fragments 540-565 m/z |
| `ztscan-a1-kernel.profile.tsv` | The run's measured quadrupole transmission (pwiz-ai `scripts/Osprey/Demux/kernels/A1_rt3-8.profile.tsv`) |
| `ztscan-a1-joint-demux.golden.tsv` | The scanning joint solve with its own centroids (`--joint --layout centered:7`, output 600.2-609.6 m/z): 60 spectra, six sweeps of the survey scan, centroided by the same solve, and nine layout spectra |

Source runs (MacCoss lab):
- `Astral-Neo-18June2024-ME-4mz-stag-DIA-400-900-10maxIT-spleen-Rep1_09.raw` (Orbitrap Astral, mouse spleen; Panorama
  `/MacCoss/maccoss/2024-06 Astral Normal vs Staggered`).
- `20261009_Stellar_WWopt_4Th-SuperOverlap_400-900mz_24min_profile_TR02.raw` (Thermo Stellar, HeLa).
- `250814_ZTScan_100spd_A_1_A1.wiff2` (ZenoTOF 8600, ZT Scan at 100 samples per day).

Cut with `Osprey.DemuxTool` built with the vendor readers (Release, `Build-Osprey.ps1 -VendorReader`), whose `--filter`
takes msconvert's filters in msconvert's syntax and `--scheme none` writes the input as read; a filtered slice keeps no
chromatograms. The vendor files are read directly, through the same readers as Osprey's:

```
Osprey.DemuxTool --in Astral-...-ME-...-Rep1_09.raw --out astral-me-staggered-slice.mzML --scheme none
    --filter "scanTime [900,1020]"
    --filter "mzPrecursors [594.52008,596.521,598.52197,600.52289,602.5238,604.52472,606.52557,608.52649] target=isolated"
    --filter "threshold count 500 most-intense"

Osprey.DemuxTool --in 20261009_Stellar_WWopt_4Th-SuperOverlap_..._profile_TR02.raw --out stellar-superoverlap-profile-slice.mzML
    --scheme none --profile --filter "scanTime [600,720]"
    --filter "mzPrecursors [600.523,601.523,602.524,603.524,604.525,605.525,606.526,607.526] target=isolated"
    --filter "mzWindow [450,750]"

Osprey.DemuxTool --in 250814_ZTScan_100spd_A_1_A1.wiff2 --out ztscan-a1-profile-slice.mzML --scheme none --profile
    --filter "scanTime [300.05,305.88]"
    --filter "mzPrecursors [<every other bin's target>] target=isolated mode=exclude"
    --filter "mzWindow [540,565]"
```

The ZT Scan slice keeps its survey scans, which delimit the sweeps: `mode=exclude` with the targets of the 394 bins
of a sweep (of 429) outside 162-196, read from one sweep's spectra (`--scheme none --profile --filter "scanTime
[300,302]"`), keeps MS1. The time range runs from one survey scan to just before the seventh. Its output range keeps
the 4 context bins of the test's settings and the transmission's 9-bin reach on each side, and nine output bins, so
the middle three gather all seven positions of `centered:7`. The m/z windows and the peak thresholds keep each file
under 5 MB; the peaks they drop are fragments and the weakest peaks, not anything a solve needs to be exercised.

**Regenerating the goldens.** As for the Eclipse golden: only when a demultiplexer's output is meant to change. Set
`OSPREY_REBLESS_DEMUX_FIXTURE=1`, run the test (it rewrites its golden and fails), review the diff, and say why in
the commit. Each golden is a few tens of KB, so the diff shows which spectra moved.
