# Centrix test fixture: Thermo Stellar profile spectra and Rust Centrix's centroids

Used by `CentrixTest.TestCentrixRustGolden`, and by `TestCentrixInOsprey` (the reader's `--centroid centrix` path
against the same golden) and `TestCentrixCalibration` (the peak sigma measured from real spectra). Real ion trap
profile spectra and the centroids Rust Centrix (maccoss/centrix) gives them, so the C# port in `Osprey.Centroid` is
held to the Rust implementation on real data: the same number of centroids in every spectrum, each at the same m/z
and intensity. What Centrix is and how Osprey uses it: `pwiz_tools/Osprey/docs/24-profile-centroiding.md`.

| File | What it is |
|---|---|
| `stellar-2th-profile-slice.mzML` | 16 profile spectra as converted from the `.raw`: 2 MS1 (1/15 Th grid) and 14 MS2 (0.2 Th grid), zero points present |
| `stellar-joint-split-slice.mzML` | 14 MS2 profiles as the staggered joint solve splits them among their bins (k=2): positive points only, the input Centrix gets inside the demultiplexer |
| `stellar-centrix-rust.golden.tsv` | Rust Centrix's 4,836 centroids of both files: input file, MS level, ordinal among that level's spectra, m/z, intensity (32-bit, as Centrix writes it) |

Source run: `20261009_Stellar_WWopt_2Th_400-900mz_24min_profile_TR01.raw` (Thermo Stellar, HeLa, 2 Th windows,
profile mode, MacCoss lab), spectra 30000-35999 converted with ProteoWizard msconvert 3.0.26056 and no peak picking
(`--filter "index [30000,35999]" -z --mz64 --inten32`). The profile slice is spectra 0, 240, 460, 922, 1384, 1845,
2307, 2769, 3012, 3229, 3691, 4153, 4614, 5076, 5538 and 5999 of that conversion. The joint-split slice is the
same positions less the two MS1 (all MS2), taken after re-multiplexing the conversion into 4 Th windows (`Osprey.DemuxTool --remultiplex
2`) and demultiplexing those with `--scheme staggered --joint --joint-profile --fill-time-ions 1000`
(an option since replaced by `--ions`, which also writes ions rather than ions per second). Spectra are
copied byte for byte by `slice_mzml.py`.

Golden made with Rust Centrix 0.1.0 at maccoss/centrix 39370f1, built in WSL (`cargo build --release`):

```
centrix run -i stellar-2th-profile-slice.mzML -i stellar-joint-split-slice.mzML -o . --sigma-ms1 0.2 --sigma-ms2 0.33
python centrix_golden_tsv.py stellar-centrix-rust.golden.tsv
    stellar-2th-profile-slice.mzML=stellar-2th-profile-slice.centrix.mzML
    stellar-joint-split-slice.mzML=stellar-joint-split-slice.centrix.mzML
```

The sigmas are given because Centrix's own calibration misreads the Stellar (its scan-rate preset gives 0.849 Th and
its wavelet fit 0.61); fits to isolated peaks give 0.327 Th for MS2 and about 0.19 Th for MS1. The scripts are in
pwiz-ai, `scripts/Osprey/Centrix`.

On these spectra the port matches Rust to 1e-11 Th and to 32-bit precision in intensity. Over all 6,000 joint-split
spectra of the slice, 0.02% of centroids differ by up to 4e-4 Th and 0.4%: Rust sums A'y with BLAS, the port in
index order, and a solve stopped by Centrix's 1e-6 absolute tolerance can stop a sweep apart. The test's tolerances
allow that and no more.

**Regenerating the golden.** Only for a change to Rust Centrix that the port follows: rebuild Rust Centrix at the new
commit, rerun the two commands above, and update the commit and the counts here. The C# port is never used to make
it.
