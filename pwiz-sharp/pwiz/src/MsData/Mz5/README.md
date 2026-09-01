# mz5 port

Read-only port of `pwiz/data/msdata/mz5/`. mz5 is an HDF5-backed
file format from 2011 (Wilhelm/Kirchner). Largely superseded by
mzMLb (2017, also ported), but kept for legacy file support.

## Status: WIP — foundation only

Done:
- `Mz5Datasets` — the ~25-value enum of named HDF5 datasets that make up an mz5 file
- `Mz5Configuration` — dataset name table + version constants + fixed string lengths
- `Mz5Types` — HDF5 compound type definitions for 3 of the ~30 mz5 record types
  (`FileInformationMZ5`, `ContVocabMZ5`, `CVRefMZ5`). Demonstrates the pattern:
  `[StructLayout(Sequential, Pack=1)]` POD + a static `CreateType()` that
  registers the HDF5 compound type with `H5T.insert` per field. Variable-length
  strings (cpp `char*`) become `IntPtr` (read with `Marshal.PtrToStringAnsi`).

Remaining (~30 record types, ~3500-5000 lines of mechanical porting):
- Hot tables: `CVParamMZ5`, `UserParamMZ5`, `RefMZ5`, `RefListMZ5`, `ParamListMZ5`,
  `ParamGroupMZ5`, `ParamListsMZ5`
- MSData top-level: `SourceFileMZ5`, `SampleMZ5`, `SoftwareMZ5`, `ScanSettingMZ5`,
  `ComponentMZ5`, `ComponentListMZ5`, `ComponentsMZ5`,
  `InstrumentConfigurationMZ5`, `ProcessingMethodMZ5`, `ProcessingMethodListMZ5`,
  `DataProcessingMZ5`, `RunMZ5`
- Per-spectrum/chromatogram: `SpectrumMZ5`, `ChromatogramMZ5`, `BinaryDataMZ5`,
  `ScanMZ5`, `ScanListMZ5`, `PrecursorMZ5`, `PrecursorListMZ5`
- `Mz5Connection` — file open + per-dataset typed read API
- `Mz5ReferenceRead` — walks all the flat HDF5 datasets and rebuilds the
  MSData object graph by resolving CVRef / RefParam / RefList back-references
- `Mz5ReaderAdapter` — `IReader` implementation; identify via HDF5 magic +
  "FileInformation" dataset presence; wire into `DefaultReaderList`
- Test fixture: round-trip an mzML through `cpp msconvert --mz5` to get a real
  mz5 file, add to `MzmlbReaderTests.cs`-style integration test

## Next-session plan

1. Port the hot-table types: `CVParamMZ5`, `UserParamMZ5`, `RefMZ5`, `RefListMZ5`
   (these are the most heavily-referenced records — every cvParam in every
   spectrum routes through them).
2. Port `ParamListMZ5` + `ParamGroupMZ5` (the cvParam/userParam container types).
3. Port one full MSData-graph branch (e.g. `RunMZ5` → `SpectrumMZ5` + `BinaryDataMZ5`)
   to validate the pattern scales.
4. Port remaining metadata types in bulk.
5. `Mz5Connection` read API + `Mz5ReferenceRead`.
6. Wire `Mz5ReaderAdapter` into `DefaultReaderList`.
7. Add round-trip integration test against a cpp-generated mz5 fixture.

## Why so much code

Each of the ~30 compound types needs:
1. `[StructLayout]` POD with explicit field types (fixed-length strings → `fixed
   char[N]` requires `unsafe`, or just `IntPtr` + manual offset math).
2. Static `CreateType()` registering the HDF5 compound type with per-field
   names + offsets + element types. The names must match cpp byte-for-byte
   (they're part of the on-disk format).
3. A `Decode()` extension that converts the read record into the corresponding
   MSData type (e.g. `CVRefMZ5` → resolve to `Pwiz.Data.Common.Cv.CVID`).

The `ReferenceRead_mz5` walker is ~500 lines on its own — it reads every
dataset, resolves all the cross-table references, and rebuilds the in-memory
MSData object graph.

## Why we're not using a higher-level HDF5 library

PureHDF / HDF5.NET are pure-managed and look ergonomic, but they don't fully
support the cpp-style POD-struct-with-explicit-offsets pattern mz5 relies on
(byte-for-byte binary parity with cpp matters because we can't generate test
fixtures any other way). HDF.PInvoke also keeps us on one HDF5 stack across
the mzMLb + mz5 readers.
