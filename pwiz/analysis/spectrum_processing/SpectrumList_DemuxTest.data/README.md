# SpectrumList_Demux test data

| File | Used by | What it is |
|---|---|---|
| `OverlapTest.mzML` | `SpectrumListDemuxTests` | Overlapping-window DIA, with gold-standard intensities from cpp |
| `MsxTest.mzML` | `SpectrumListDemuxTests` | MSX DIA, with gold-standard intensities from cpp |
| `EclipseNnlsFixture.tsv` | `Demux_EclipseNnlsFixture_MatchesCppPeakByPeak` | Real staggered-window MS2, cut down to four fragment m/z |
| `EclipseNnlsFixture.expected.tsv` | the same | cpp msconvert's demultiplexed intensities for those peaks |

## EclipseNnlsFixture

Cut down from a 3-minute slice (45-48 min, 204 MS2 spectra, 8 staggered 12 Th windows centered
502.48-544.50 m/z, vendor-centroided, 400 most intense peaks per spectrum) of
`Ecl_2022_0705_Beads_EV13_SAXN_12mz_10.raw` (Orbitrap Eclipse, plasma EVs, MacCoss lab). The slice
and cpp msconvert's demultiplexing of it were made by Mike MacCoss for Osprey (pwiz PR #4710):

```
msconvert Ecl_2022_0705_Beads_EV13_SAXN_12mz_10.raw --zlib --simAsSpectra
    --filter "peakPicking vendor msLevel=1-"
    --filter "scanTime [2700,2880]"
    --filter "mzPrecursors [502.4783,508.4810,514.4838,520.4865,526.4892,532.4920,538.4947,544.4974] target=isolated"
    --filter "threshold count 400 most-intense"
msconvert <that slice> --zlib --filter "demultiplex optimization=overlap_only massError=10.0ppm"
```

The second command ran msconvert 3.0.26100.9783a03 (C++). The C++ demultiplexer has since been retired, so
the expected values cannot be regenerated; this extraction is the record of them.

`EclipseNnlsFixture.tsv` keeps every MS2 spectrum of the slice (they make up the demux blocks), but only
the peaks within 100 ppm of 701.8357, 1081.5310, 619.3048 and 596.2789 m/z. A demultiplexed peak is
solved from the peaks within the 10 ppm extraction tolerance of its own m/z, so the cut-down input gives
exactly the solves of the full spectra for those peaks. In some spectra these columns' NNLS reaches its
50-iteration limit; cpp keeps the last feasible solution, and a C# port that zeroed it lost peaks of up to
5e7. The same m/z converge normally in the other spectra.

Each spectrum also keeps its highest-m/z peak, marked `M`. `SpectrumPeakExtractor` (cpp and C# alike)
starts its bin search at `query - maxDelta`, where `maxDelta` is the largest bin half-width in the
spectrum, so the highest peak decides whether the upper half of a high bin's tolerance window is
searched. Without it, 1081.53 would be the highest peak and lose the upper half of its window. The `M`
peaks are not compared.

`EclipseNnlsFixture.expected.tsv` holds cpp's demultiplexed peaks at those m/z with intensity of at least 1.
Smaller values are rounding around a true zero, which cpp (Householder QR) and C# (Cholesky) resolve
differently. Intensities are float32, as written by msconvert.
