# Phase 1: MsDataFileImpl → pwiz-sharp mapping

Working doc for the .NET 8 port (see `ai/todos/active/TODO-20260612_net8_port.md`).
Decision: refactor `MsDataFileImpl` in place rather than write a shim. This file
catalogues every public member, its current `pwiz.CLI.*` implementation, the
pwiz-sharp equivalent, and any gap that needs filling on the pwiz-sharp side
before the refactor can begin.

Delete this file before the branch merges.

## Categorisation

Sections that follow group ~80 public members by responsibility, matching the
order they appear in `MsDataFileImpl.cs`. Status column key:
- ✅ Direct map exists in pwiz-sharp; refactor is mechanical.
- 🟡 Map exists but with a shape / type change at the boundary.
- 🔴 Gap on the pwiz-sharp side — needs porting before this surface can move.
- ⚪ Static helper / value type — moves with the file, no pwiz dep.

---

## A. Constructor & lifecycle

| Member | pwiz.CLI today | pwiz-sharp target | Status |
|---|---|---|---|
| `MsDataFileImpl(path, sampleIndex, lockmassParameters, simAsSpectra, srmAsSpectra, acceptZeroLengthSpectra, requireVendorCentroidedMS1/2, ignoreZeroIntensityPoints, preferOnlyMsLevel, combineIonMobilitySpectra, trimNativeId, passEntireDiaPasefFrame)` | `FULL_READER_LIST.read(path, _msDataFile, sampleIndex, _config)` | `new DefaultReaderList().Read(path, msd, readerConfig)` or use the pre-built `ReaderList.Default`. | 🟡 |
| `Dispose()` | Disposes `MSData` + cached spectrum list refs | `MSData : IDisposable` (already); same pattern. | ✅ |
| `EnableCaching(int? size)` / `DisableCaching()` | Wraps spectrum list in `SpectrumList_Cache` | pwiz-sharp has spectrum list factory chain (`SpectrumListFactory.Wrap`) — verify it supports a Cache wrapper. | 🟡 |

**ReaderConfig flag coverage:**

| pwiz.CLI flag | pwiz-sharp ReaderConfig | Status |
|---|---|---|
| `simAsSpectra` | `SimAsSpectra` | ✅ |
| `srmAsSpectra` | `SrmAsSpectra` | ✅ |
| `acceptZeroLengthSpectra` | `AcceptZeroLengthSpectra` | ✅ |
| `ignoreZeroIntensityPoints` | `IgnoreZeroIntensityPoints` | ✅ |
| `preferOnlyMsLevel` | `PreferOnlyMsLevel` | ✅ |
| `allowMsMsWithoutPrecursor` | `AllowMsMsWithoutPrecursor` | ✅ |
| `combineIonMobilitySpectra` | `CombineIonMobilitySpectra` | ✅ |
| `ignoreCalibrationScans` | `IgnoreCalibrationScans` | ✅ |
| `globalChromatogramsAreMs1Only` | `GlobalChromatogramsAreMs1Only` | ✅ |
| `reportSonarBins` | **missing** (Waters reader appears to always report) | 🔴 verify behavior matches default-on |
| `includeIsolationArrays` | **missing** (Bruker TDF) | 🔴 |
| `passEntireDiaPasefFrame` | **missing** | 🔴 |
| `sampleIndex` (positional arg) | `RunIndex` on ReaderConfig | 🟡 rename |
| `lockmassParameters` | pwiz-sharp has `SpectrumList_LockmassRefiner` in Analysis | 🟡 must be wired post-read, not via config |
| `trimNativeId` | Skyline-internal post-process? not a pwiz config flag | ⚪ stays in MsDataFileImpl |

**Action:** Add `ReportSonarBins`, `IncludeIsolationArrays`, `PassEntireDiaPasefFrame` to `Pwiz.Data.MsData.ReaderConfig` and thread through to the Waters / Bruker readers.

---

## B. File-level metadata

| Member | pwiz.CLI today | pwiz-sharp target | Status |
|---|---|---|---|
| `RunId` | `_msDataFile.run.id` | `_msd.Run.Id` | ✅ |
| `RunStartTime` | `_msDataFile.run.startTimeStamp` parse | `_msd.Run.StartTime` (DateTime? on RunInfo) | ✅ |
| `ConfigInfo` (returns `MsDataConfigInfo`) | Builds from InstrumentConfiguration + content list + DIA window probe | Same fields available — `InstrumentConfigurations`, `FileDescription.FileContent`, vendor-specific DIA windows | 🟡 |
| `GetLog()` | Perf measurement, no pwiz dep | n/a | ⚪ |
| `IsProcessedBy(name)` | Walks DataProcessing → ProcessingMethod → Software name | `_msd.DataProcessings → ProcessingMethods → Software.Id`, same shape | ✅ |
| `GetFileContentList()` | Yields `cvParamChild(MS_spectrum_representation)` names | `_msd.FileDescription.FileContent.CvParamChildren(MS_spectrum_representation)` | ✅ |
| `GetInstrumentConfigInfoList()` | Iterates instrument configs, builds `MsInstrumentConfigInfo` | `_msd.InstrumentConfigurations` | ✅ |
| `GetInstrumentSerialNumber()` | Walks first instrumentConfig for instrument_serial_number cvParam | Same path | ✅ |
| `GetNativeIdAndFileFormat(out, out)` | Pulls from FileDescription.SourceFile[0] | Same | ✅ |
| `HasDeclaredMSnSpectra` | Walks `_msd.fileDescription.fileContent.cvParamChildren(MS_mass_spectrum)` for ms_level>=2 | Same | ✅ |
| `GetSampleId()` | Sample list lookup | `_msd.Samples` | ✅ |

---

## C. Vendor detection

| Member | pwiz.CLI today | pwiz-sharp target | Status |
|---|---|---|---|
| `IsABFile`, `IsAgilentFile`, `IsThermoFile`, `IsWatersFile`, `IsShimadzuFile` | Each checks `_msd.fileDescription.sourceFiles[0]` for a vendor-format cvParam | `_msd.FileDescription.SourceFiles[0].HasCVParam(MS_Thermo_…_format)` etc. | ✅ |
| `IsMzWiffXml` | Sourcefile name endswith `.wiff` AND container is mzML | Same shape | ✅ |
| `IsWatersLockmassCorrectionCandidate` | Waters file + missing lockmass-applied flag | Same | ✅ |
| `IsWatersLockmassSpectrum(s)` | Walks spectrum cvParams for lockmass | Same | ✅ |
| `IsWatersSonarData()` | Queries `SpectrumList_Waters.isSonarData()` | pwiz-sharp `SpectrumList_Waters` has SONAR support (line 275); needs an `IsSonarData()` accessor surfaced | 🟡 likely small addition |

---

## D. Ion mobility + CCS calculator

| Member | pwiz.CLI today | pwiz-sharp target | Status |
|---|---|---|---|
| `IonMobilityUnits` | Maps from spectrumList vendor type | Have `MS_ion_mobility_drift_time` / `MS_inverse_reduced_ion_mobility` / `MS_FAIMS_compensation_voltage` CV scrutiny in pwiz-sharp | 🟡 need accessor matching `eIonMobilityUnits` enum |
| `ProvidesCollisionalCrossSectionConverter` | Cast spectrumList to `pwiz.CLI.analysis.IonMobilityCalculator` | **Unknown** — does pwiz-sharp port the IonMobilityCalculator? | 🔴 verify; likely a Bruker/Waters-specific interface |
| `IonMobilityFromCCS(ccs, mz, charge)` | Calls calculator | Same as above | 🔴 |
| `CCSFromIonMobilityValue(...)` / `CCSFromIonMobility(...)` | Calls calculator | Same | 🔴 |
| `GetMaxIonMobility()` | Walks spectrumList for max IM value | Same shape, but requires `IIonMobilitySpectrumList` in pwiz-sharp | 🟡 verify |
| `HasCombinedIonMobilitySpectra` | `_ionMobilitySpectrumList.hasCombinedIonMobility()` | pwiz-sharp `IIonMobilitySpectrumList` exists; need `HasCombinedIonMobility` property | 🟡 |
| `HasIonMobilitySpectra` | Checks `_ionMobilitySpectrumList != null` | Same | ✅ |
| `GetIonMobility(scanIndex)` | Per-spectrum IM | Per-spectrum IM access exists in pwiz-sharp readers | ✅ |

**Action:** Audit pwiz-sharp's `IIonMobilitySpectrumList` for `HasCombinedIonMobility` + CCS converter surface. The CCS calculator chain is the biggest gap — verify before committing.

---

## E. Spectrum surface

| Member | pwiz.CLI today | pwiz-sharp target | Status |
|---|---|---|---|
| `SpectrumCount` / `GetSpectrumCount()` | `SpectrumList.size()` | `_spectrumList.Count` (already int, no checked cast) | ✅ |
| `GetSpectrumIndex(string id)` | `SpectrumList.find(id)` | `_spectrumList.Find(id)` | ✅ |
| `GetSpectrum(int, out double[] mz, out double[] inten)` | spectrum.getMZArray.data, intensityArray.data | `_spectrumList.GetSpectrum(i, true).GetMZArray().Data` etc. | ✅ |
| `GetSpectrum(int) → MsDataSpectrum` | Full metadata build | Composable from pwiz-sharp Spectrum | ✅ |
| `GetSpectrumMetadata(int)` | Returns `SpectrumMetadata` (lightweight) | Build from pwiz-sharp Spectrum | ✅ |
| `GetSpectrumId(int)` | `SpectrumList.spectrumIdentity(i).id` | `_spectrumList.SpectrumIdentity(i).Id` | ✅ |
| `IsCentroided(int)` | spectrum.hasCVParam(MS_centroid_spectrum) | Same | ✅ |
| `GetMsLevel(int)` | spectrum.cvParam(MS_ms_level).valueAs<int>() | Same | ✅ |
| `GetScanDescription(int)` | spectrum.cvParam(MS_scan_description) | Same | ✅ |
| `GetStartTime(int)` | Walks Scan list | Same | ✅ |
| `GetWindowGroup(int)` | Bruker-specific window group lookup | Verify pwiz-sharp Bruker reader exposes this | 🟡 |
| `GetPrecursors(int, level)` | Returns IList<MsPrecursor>, including multi-precursor walk | pwiz-sharp Spectrum.Precursors is List<Precursor>; mzPeak port already does multi-precursor — verify all vendor readers too | ✅ |
| `GetScanTimes()` | Walks all spectra start times | Same | ✅ |
| `GetTotalIonCurrent()` | Walks all spectra TIC | Same | ✅ |
| `GetScanTimesAndMsLevels(ct, out, out)` | Both arrays in one pass with cancellation | Same | ✅ |
| `HasSrmSpectra` / `IsSrmSpectrum(i)` / `GetSrmSpectrum(i)` | SRM detection via cvParam | Same | ✅ |
| `IsValidDiaPasefPoint(...)` | ConfigInfo lookup | ConfigInfo equivalent needed | ✅ via Bruker IsolationMzAndMobilityFilter |
| `GetMetaDataValue<T>(scanIndex, getter, ...)` | Template over Spectrum | Same shape | ✅ |

---

## F. Chromatogram surface

| Member | pwiz.CLI today | pwiz-sharp target | Status |
|---|---|---|---|
| `ChromatogramCount` | `ChromatogramList.size()` | `_chromatogramList.Count` | ✅ |
| `HasChromatogramData` | Walks chromatogram list for non-empty | Same | ✅ |
| `GetChromatogramId(i, out indexId)` | identity.id + index | Same | ✅ |
| `GetChromatogramCollisionEnergy(i)` | precursor activation CE cvParam | Same | ✅ |
| `GetChromatogramMetadata(i, out id, polarity, precursorMz, productMz)` | Walks chromatogram cvParams | Same | ✅ |
| `GetChromatogram(i, out id, out time, out intensities)` | time + intensity arrays | `chrom.GetTimeArray().Data`, `chrom.GetIntensityArray().Data` | ✅ |
| `GetQcTraces()` / `QcTrace` class | Walks chromatograms for non-TIC/BPC ones with quality cvParam | Same | ✅ |

---

## G. SONAR (Waters-only)

| Member | pwiz.CLI today | pwiz-sharp target | Status |
|---|---|---|---|
| `SonarMzToBinRange(mz, tol)` | `SpectrumList_Waters.sonarMzToBinRange(...)` | `SpectrumList_Waters.SonarMzToBinRange` exists (line 275) — but signature returns tuple; MsDataFileImpl returns `Tuple<int,int>` | 🟡 |
| `SonarBinToPrecursorMz(bin)` | `SpectrumList_Waters.sonarBinToPrecursorMz` | Already in pwiz-sharp (line 281) | ✅ |

---

## H. Output

| Member | pwiz.CLI today | pwiz-sharp target | Status |
|---|---|---|---|
| `Write(string path)` | `MSDataFile.write(_msd, path)` (full pwiz writer) | `MSDataFile.Write(_msd, path, new WriteConfig{...})` | ✅ |

---

## I. Static helpers

| Member | Notes | Status |
|---|---|---|
| `InstalledVersion` | Reads pwiz.CLI version | Replace with pwiz-sharp version string | 🟡 cosmetic |
| `GetFileExtensionsByType()` | Static extension map | Build from pwiz-sharp `DefaultReaderList` | ✅ |
| `SupportsVendorPeakPicking(path)` | Vendor-format detection | pwiz-sharp `IReader.Identify` + per-vendor flag | ✅ |
| `SupportsMultipleSamples(path)` | Vendor-format detection | Same | ✅ |
| `ReadIds(path)` | Reads spectrum IDs only | Open file → enumerate spectrum identities | ✅ |
| `IsValidFile(path)` | Tries to open | `DefaultReaderList.Identify(path, head)` | ✅ |
| `GetCvParamName(accession)` | CV term lookup | `CvLookup.CvTermInfo(accession).Name` | ✅ |
| `IsNegativeChargeIdNullable(id)` / `IsSingleIonCurrentId(id)` | Pure string parsing | Move as-is | ⚪ |
| `ToFloatArray(IList<double>)` | Pure utility | Move as-is | ⚪ |
| `GetNonUnicodePath(path)` | Unicode workaround | Move as-is | ⚪ |
| `ForceUncombinedIonMobility` | const flag | Move as-is | ⚪ |
| Prefix consts (PREFIX_TOTAL/SINGLE/PRECURSOR, TIC, BPC) | string consts | Move as-is | ⚪ |

---

## J. Supporting types (move with the file, retarget any `pwiz.CLI.*` references inside)

| Type | Notes |
|---|---|
| `MsDataConfigInfo` | Plain POCO. No pwiz dep. ⚪ |
| `MsPrecursor` | Struct, has `SignedMz` (Skyline type). No pwiz dep. ⚪ |
| `MsDataSpectrum` | Plain POCO. References `SpectrumMetadata`, `MsInstrumentConfigInfo`. No pwiz dep. ⚪ |
| `MsInstrumentConfigInfo` | Plain POCO. ⚪ |
| `MsDataScanCache` | Plain in-memory cache. ⚪ |
| `DiaFrameMsMsWindowItem` (nested in MsDataFileImpl) | Plain POCO. ⚪ |
| `QcTrace` (nested) | Built from `pwiz.CLI.msdata.Chromatogram` — retarget ctor to take pwiz-sharp `Chromatogram`. 🟡 |
| `QcTraceQuality` / `QcTraceUnits` (nested) | string consts. ⚪ |

---

## Identified pwiz-sharp gaps (must close before refactor lands)

After verification dig (2026-06-30), the original 6-gap list collapses to **one
real add + a handful of verifications**:

1. **`ReaderConfig.PassEntireDiaPasefFrame`** — genuinely missing. cpp uses this
   to make Bruker TIMS readers emit one fat spectrum per diaPASEF frame
   instead of one per isolation window. No equivalent in pwiz-sharp's Bruker
   reader. **Real gap; needs porting from cpp `Reader_Bruker.cpp` +
   `SpectrumList_Bruker.cpp`.** Estimated: half-day port + Skyline
   parity test.

The following collapsed during verification (originally suspected, actually
already present or not needed):

- ~~`ReaderConfig.ReportSonarBins`~~ — Waters reader implicitly reports SONAR
  via `IonMobilityUnits.WatersSonar` when `HasSonarFunctions` is true. cpp's
  runtime toggle is effectively always-on in pwiz-sharp. Verify exact
  behavior parity with Skyline's expectation (cpp Skyline defaults `true`).
- ~~`ReaderConfig.IncludeIsolationArrays`~~ — Skyline always passes `false`
  (`MsDataFileImpl.cs:200` — "we infer from WindowGroup and IM"); pwiz-sharp's
  default matches. **Not needed for the Skyline port.**
- ~~CCS ↔ IM calculator~~ — `IIonMobilityCcsConversion` interface present
  (`pwiz/src/MsData/IIonMobilityCcsConversion.cs`) with
  `CanConvertIonMobilityAndCcs`, `IonMobilityToCcs`, `CcsToIonMobility`.
  Implemented by all 5 IM-capable vendor readers (Waters, Thermo, Mobilion,
  Bruker, Agilent). Small shape note: cpp's `canConvertIonMobilityAndCCS(units)`
  takes a units arg; pwiz-sharp's no-arg property suffices because the
  answer is per-file, not per-units (cpp's arg was over-designed).
- ~~`IIonMobilitySpectrumList.HasCombinedIonMobility`~~ — already a property
  on the interface, implemented by all 5 vendors.
- ~~`SpectrumList_Waters.IsSonarData()`~~ — implemented as `IsWatersSonar` on
  `SpectrumList_Waters` + a dedicated `IWatersSonarSpectrumList` interface
  with `HasSonarFunctions`, `SonarMzToBinRange`, `SonarBinToPrecursorMz`.

**Items to verify but probably trivial:**

- Spectrum-list cache wrapper — does `SpectrumListFactory.Wrap` (or an
  adjacent helper) include a `SpectrumList_Cache` equivalent? Skyline's
  `EnableCaching` is hot-path-ish for repeated chromatogram extraction over
  the same spectrum.
- Bruker `GetWindowGroup(scanIndex)` surface — per-spectrum diaPASEF
  window-group accessor. The data is in `TdfMetadata.HasDiaPasefData` chain;
  verify it surfaces per-spectrum.

**Revised effort estimate:** 1 day for the `PassEntireDiaPasefFrame` port +
0.5 day verifications. Down from 2-3 days.

## Type-shape changes at the API boundary

The refactor preserves `MsDataFileImpl`'s public surface intact (so Skyline call
sites don't change), but a few internal types swap:

- `pwiz.CLI.msdata.MSData` → `Pwiz.Data.MsData.MSData`
- `pwiz.CLI.msdata.Spectrum` → `Pwiz.Data.MsData.Spectra.Spectrum`
- `pwiz.CLI.msdata.SpectrumList` → `Pwiz.Data.MsData.ISpectrumList`
- `pwiz.CLI.msdata.Chromatogram` → `Pwiz.Data.MsData.Spectra.Chromatogram`
- `pwiz.CLI.msdata.ChromatogramList` → `Pwiz.Data.MsData.IChromatogramList`
- `pwiz.CLI.msdata.CVParam` → `Pwiz.Data.Common.Params.CVParam`
- `pwiz.CLI.cv.CVID` → `Pwiz.Data.Common.Cv.CVID`
- `pwiz.CLI.msdata.ReaderList` → `Pwiz.Data.MsData.Readers.ReaderList` (`.Default`)
- `pwiz.CLI.msdata.ReaderConfig` → `Pwiz.Data.MsData.ReaderConfig` (camelCase → PascalCase)
- `pwiz.CLI.util.IterationListener` → `Pwiz.Data.MsData.IIterationListener` + `IterationListenerRegistry`

The `MsDataFileInfo.RunPredicate<T>(filepath, Func<MSData, T>)` signature
changes: its `MSData` parameter type goes from `pwiz.CLI.msdata.MSData` to
`Pwiz.Data.MsData.MSData`. **This IS a breaking change for callers of
`RunPredicate`.** Every Skyline call site needs the parameter type updated.
That's a real grep-and-update pass, but small — probably <20 sites given
RunPredicate is a niche helper.

## Smoke-test plan (Phase 1 exit criterion)

Once gaps are closed and the refactor lands:

1. Build `pwiz_tools/Shared/ProteowizardWrapper` against pwiz-sharp.
2. Build Skyline.csproj — still on .NET Framework — pointing at the refactored
   ProteowizardWrapper. Should compile with NO Skyline-side changes (the
   public surface is preserved).
3. Run a representative import end-to-end through Skyline CommandLine:
   - Thermo .raw → import, build chromatogram cache, verify spectrum count
     and TIC match the pre-refactor output byte-for-byte.
   - Bruker .d → same, including diaPASEF window-group check.
   - Waters .raw → same, including lockmass + SONAR sanity.
   - mzML → format independence check.
4. Compare resulting `.skyd` chromatogram cache files between old and new
   runs — must be bit-identical or differ only in metadata that's expected to
   drift (timestamps, version strings).

Phase 2 (the rest of the wrapper swap) is unblocked only when (1)+(2)+(3)
all pass on at least the 4-file set above.
