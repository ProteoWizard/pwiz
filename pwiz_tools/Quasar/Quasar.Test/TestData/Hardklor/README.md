# Hardklor.exe reference output

Expected results for `HardklorTest.TestHardklorMatchesHardklorExe` (skyline-glimpse's
`HardklorTests.AgreesWithHardklorExe`, where these files were made), made once with Skyline's Hardklor.exe
(v2.3.6, `pwiz_tools/Skyline/bin/x64/Release/Hardklor.exe`). The tests never run Hardklor.exe.

## Input

The 14 MS1 scans of pwiz's `example_data/small.RAW` (LTQ-FT), centroided by the Thermo library:

    msconvert small.RAW --mzML --64 --filter "peakPicking vendor msLevel=1-" --filter "msLevel 1" --outfile small_ms1_centroid.mzML

with pwiz-sharp's msconvert (the same reader Glimpse uses). Glimpse's test reads `small.RAW` through
`PwizMsDataSource.ReadSpectrum(i, centroid: true)`; the peaks were checked to be identical to the mzML's.

Quasar's tests read `../small-raw-centroided.tsv.gz` instead: every spectrum of `small.RAW` (SHA-256
071F845DEC57B5FE3A6023326ACBA6B179D03EAE4A8E86DBA416B8B50EC42E63) as that same call reads it, Thermo
vendor-centroided, with its header (MS level, retention time, isolation window, analyzer). Doubles are
written round-trip ("R"), so the peaks are exactly the reader's; the file's own header lists the columns.
It was written once with pwiz-sharp's Thermo reader, and the pinned values in the tests (envelope counts,
index, mass and intensity sums) were reproduced from it exactly.

## Runs

    Hardklor.exe hk_orbitrap.conf     -> small_orbitrap.hk
    Hardklor.exe hk_fticr_matthews.conf -> small_fticr_matthews.hk

- `hk_orbitrap.conf` is the configuration Skyline's `HardklorSearchEngine.cs` writes, with its defaults
  (Orbitrap, resolution 60,000, minimum idotp 0.9 so correlation 0.987688340595138, charge 1 to 7, depth 2).
  Skyline also writes an `isotope_data` file of its traditional abundances; for H, C, N, O and S those are
  Hardklor's built-in values, so the file is left out.
- `hk_fticr_matthews.conf` is the same with FTICR at 100,000 (the LTQ-FT's analyzer) and
  `isotope_data = ISOTOPE_matthews.DAT`: Hardklor's ISOTOPE.DAT with the H, C, N, O and S entries (and their
  Hx..Sx copies) replaced by exactly what `PwizEnvelopeModel` uses: pwiz-sharp's NIST abundances for H, N, O
  and S, and 13C 0.01084 (12C 0.98916), Dwight Matthews' value as used by IDCalc, Hardklor and Skyline. This
  isolates the port from the isotope abundances.
