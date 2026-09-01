# 13. BLIB Output Schema (C#)

> Pipeline stage: Output (Stage 7). C# port of Rust docs/08-blib-output-schema.md. Corresponds to Rust osprey BiblioSpec (.blib) output.

Osprey emits its final results as a **BiblioSpec (`.blib`)** SQLite database — the spectral-library format Skyline consumes for chromatogram extraction, ID lines, and quantification. The C# port writes this file with a hand-rolled managed SQLite writer (`System.Data.SQLite`), not by shelling out to ProteoWizard's C++ BiblioSpec. It mirrors the schema and byte conventions of Skyline's own `BlibData`/`BlibDb` writer closely enough to be byte-identical to the Rust output on the reference datasets.

Two layers are involved:

- **`Osprey.IO/BlibWriter.cs`** — the low-level SQLite layer: schema creation, prepared-statement inserts, zlib compression, sequence cleanup, finalize. One instance == one `.blib` file.
- **`Osprey.Tasks/BlibOutputWriter.cs`** — the Stage 7 sequencer that composes rows from the passing-precursor lookup tables and drives `BlibWriter`. It is invoked once from `Osprey.Tasks/SecondPassFdrTask.cs:365` (the `SecondPassFDR` HPC worker — see 15-hpc-scoring-split.md).

Reading back (for library input, and for round-trip tests) is `Osprey.IO/BlibLoader.cs`.

---

## Overview

The blib contains standard BiblioSpec tables (Skyline-compatible) plus five Osprey extension tables:

```text
Standard BiblioSpec tables (BlibWriter.CreateSchema, BlibWriter.cs:723-886)
  LibInfo, ScoreTypes, IonMobilityTypes, SpectrumSourceFiles,
  RefSpectra, RefSpectraPeaks, RefSpectraPeakAnnotations,
  Modifications, Proteins, RefSpectraProteins, RetentionTimes

Osprey extension tables (same CreateSchema block)
  OspreyMetadata, OspreyPeakBoundaries, OspreyRunScores,
  OspreyExperimentScores, OspreyCoefficients
```

The C# schema declares two columns the Rust doc's table listings omit: `SpectrumSourceFiles.workflowType TINYINT` and `ScoreTypes.probabilityType VARCHAR(128)` (BlibWriter.cs:750, 737). Both exist for BiblioSpecLite compatibility with Skyline.

---

## Pipeline-order walkthrough

Stage 7 blib emission runs entirely inside `BlibOutputWriter.Write(...)` (BlibOutputWriter.cs:53-97). The gating (which precursors pass, and which run is "best" per precursor) is done by the caller and handed in as pre-built lookup tables; this stage only composes rows.

### Step 1 — Create the file with WAL journaling

`new BlibWriter(path)` deletes any existing file, opens a fresh SQLite connection, and sets `PRAGMA journal_mode=WAL` + `PRAGMA synchronous=NORMAL` (BlibWriter.cs:88-106). The write happens to a `FileSaver` sibling temp; `saver.Commit()` atomically renames it into `config.OutputBlib` only after `FinalizeDatabase()` has checkpointed and torn down the WAL (BlibOutputWriter.cs:73-96). This is why `FinalizeDatabase()` must be the last writer call.

### Step 2 — Schema + seed rows

`CreateSchema()` (BlibWriter.cs:723) creates all 16 tables in one batch, then seeds:

- **`LibInfo`**: one row, `libLSID = urn:lsid:osprey:blib:<guid>`, `createTime = datetime('now')`, `numSpecs = 0` (updated at finalize), `majorVersion = 1`, `minorVersion = 11` (constants `BLIB_MAJOR_VERSION`/`BLIB_MINOR_VERSION`, BlibWriter.cs:39-40).
- **`ScoreTypes`**: the full ProteoWizard BiblioSpec score-type enumeration, IDs 0-20 (BlibWriter.cs:899-922). ID 1 = `PERCOLATOR QVALUE`, ID 14 = `MORPHEUS SCORE`, **ID 19 = `GENERIC Q-VALUE`** with `probabilityType = PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT`. Osprey stamps every spectrum with score type **19** (`SCORE_TYPE_GENERIC_QVALUE`, BlibWriter.cs:41).
- **`IonMobilityTypes`**: 0=none, 1=driftTime(msec), 2=inverseK0(Vsec/cm^2), 3=compensation(V) (BlibWriter.cs:936-951).

`PrepareStatements()` (BlibWriter.cs:114) then compiles one reusable `SQLiteCommand` per insert kind so the tight per-spectrum loops avoid re-parsing SQL.

### Step 3 — Source files

`CreateSourceFiles` (BlibOutputWriter.cs:103-115) adds one `SpectrumSourceFiles` row per input file via `AddSourceFile` (BlibWriter.cs:263). Column semantics:

- `fileName = <fileStem>.mzML` (the spectrum source; `kvp.Key` is the file stem).
- `idFileName = Path.GetFileName(config.LibrarySource.Path)` — the **library** basename, i.e. the "ID source". Skyline expects the ID source to be the library file, not a copy of the spectrum path.
- `cutoffScore = fdrThreshold = config.RunFdr` (default 0.01, BlibOutputWriter.cs:62,112).
- `workflowType = 1` (hard-coded, BlibWriter.cs:267).

The path stored is a bare `<stem>.mzML` filename, not a computed relative path with `../` segments.

### Step 4 — Parallel fragment pre-compression

`PrecompressSpectra` (BlibOutputWriter.cs:121-153) pulls the **library theoretical fragments** (b/y ions) for each passing precursor from the in-memory library (`libraryById[entry.EntryId].Fragments`), packs m/z as little-endian `f64` and intensity as little-endian `f32`, and zlib-compresses them in parallel via `BlibWriter.CompressMzs` / `CompressIntensities` (static, thread-safe). These are the library predicted fragments, **not** observed DIA peaks — Skyline uses them to build XICs for the correct transitions. The compressed blobs are held per-index so the sequential emit loop stays deterministic.

Compression detail (BlibWriter.cs:980-1008): Osprey uses **DotNetZip `Ionic.Zlib` at level 6** — the same library and level as Skyline's `pwiz.Skyline.Util.Extensions.UtilDB.Compress` — rather than `System.IO.Compression.DeflateStream`, which emits a subtly different byte stream on small inputs and would break byte parity with both Rust (`flate2` stock-zlib backend) and Skyline's `BlibData`. If the compressed output is not smaller than the raw bytes, the raw bytes are stored (BlibWriter.cs:1005-1007); the reader detects this by comparing blob length to the expected uncompressed size.

### Step 5 — RefSpectra + child rows (sequential, one per passing precursor)

`EmitSpectrumRows` (BlibOutputWriter.cs:159-260) iterates the best-per-precursor entries in order. For each:

- **`AddSpectrumPrecompressed`** (BlibWriter.cs:311) inserts the `RefSpectra` row and its `RefSpectraPeaks` blob row. Before insert it runs `StripFlankingChars` (removes `_`, `.`, `-`, and `K.PEPTIDE.R`-style flanks, bracket-aware; BlibWriter.cs:635) and `ConvertUnimodToMass` (rewrites `[UniMod:4]` → `[+57.0215]` using the built-in `UNIMOD_MASSES` table, BlibWriter.cs:653, 46-65). Fixed column values: `prevAA`/`nextAA = '-'`, `ionMobility`/`ccs`/`highEnergyOffset = 0.0`, `ionMobilityType = 0`, `moleculeName`/`chemicalFormula`/`precursorAdduct`/`inchiKey`/`otherKeys = ''` (empty strings, BlibWriter.cs:127-131).
  - `retentionTime`/`startTime`/`endTime` = the cross-charge **shared** apex/start/end if the peptide was detected at multiple charges in this file (`sharedBounds`, BlibOutputWriter.cs:208-218), else the entry's own boundaries.
  - `copies = nRunsDetected` (count of runs this precursor was detected in, BlibOutputWriter.cs:198-204).
  - `score = scoreQvalue`, the **experiment-precursor q-value** (min across observations, `bestExpPrecursorQ`, BlibOutputWriter.cs:190-193). This is a raw q-value (lower = better), consistent with score type 19 being a "probability the ID is incorrect". `scoreType = 19`.
- **`AddModifications`** (BlibWriter.cs:399): one `Modifications` row per mod, `position` converted from internal **0-based to 1-based** (`mod.Position + 1`, BlibWriter.cs:403), `mass = MassDelta`. Verified by `Osprey.Test/IOTest.cs:107` (`TestBlibModifications1Based`).
- **`AddProteinMapping`** (BlibWriter.cs:414): de-duplicates accessions via `_proteinCache`, inserting `Proteins` + `RefSpectraProteins` rows.
- **`WriteRetentionTimes`** (see Step 6).
- **Osprey extension rows** (BlibOutputWriter.cs:247-258): `AddPeakBoundaries` (best-run start/end/apex + `IntegratedArea = entry.BoundsArea`, `ApexIntensity = 0.0` placeholder), `AddRunScores` (`RunQValue = EffectiveRunQvalue(Both)`, `DiscriminantScore`/`PosteriorErrorProb = 0.0` placeholders), `AddExperimentScores` (`ExperimentQValue = scoreQvalue`, `NRunsDetected`, `NRunsSearched = perFileEntries.Count`).

### Step 6 — RetentionTimes: nullable retentionTime for Skyline ID lines

`WriteRetentionTimes` (BlibOutputWriter.cs:279-339) emits one `RetentionTimes` row for **every run where this precursor was detected** (`entriesByPrecursor[key]` observations). The `retentionTime` column drives whether Skyline draws an ID line:

- If the run's `EffectiveRunQvalue(Both) <= fdrThreshold` → `retentionTime = apex RT` (ID line shown).
- Otherwise → `retentionTime = NULL` (`DBNull.Value` in `AddRetentionTime`, BlibWriter.cs:446-447): Skyline uses `startTime`/`endTime` for quantification boundaries but shows no ID line ("integrate here, but not an independent identification").

The C# adds a **fallback the Rust doc does not describe**: if *no* run passes run-level FDR, the run with the lowest `run_qvalue` still gets `retentionTime` populated (`bestRunFile`, BlibOutputWriter.cs:288-313), so every `RefSpectra` has at least one ID line. `bestSpectrum = 1` for the observation whose file equals the best-precursor file, else 0. `score = runQ`. Cross-charge shared boundaries are applied here too.

### Step 7 — Metadata

`WriteMetadata` (BlibOutputWriter.cs:264-272) writes four `OspreyMetadata` key/value rows (via `INSERT OR REPLACE`): `osprey_version`, `search_mode = coelution`, `run_fdr`, `experiment_fdr`.

### Step 8 — Finalize

`FinalizeDatabase()` (BlibWriter.cs:561-579): updates `LibInfo.numSpecs` to the `RefSpectra` count, creates 10 indices (the 7 the Rust doc lists plus `idx_expscores_refid`, `idx_coefficients_refid`, `idx_rettimes_refid`), then `PRAGMA wal_checkpoint(TRUNCATE)` + `PRAGMA journal_mode=DELETE` to merge and remove the WAL sidecars so the atomic rename in Step 1 moves a single self-contained file.

### OspreyCoefficients (reserved, zero rows)

`AddCoefficient` exists (BlibWriter.cs:547) but is never called from the pipeline (only `SecondPassFdrTask`/`BlibOutputWriter` drive the writer, and neither calls it). The `OspreyCoefficients` table is created and indexed but written with zero rows — the per-scan XIC time series is not plumbed through Stage 7. This matches the Rust doc's "(optional)" label.

---

## Reading back (`BlibLoader.cs`)

`BlibLoader.Load` (BlibLoader.cs:51) reads `RefSpectra` + `RefSpectraPeaks` (`LoadSpectra`) and `RefSpectraProteins`/`Proteins` (`LoadProteinMappings`). Peak blobs are decoded by `DecodeBlibPeaks` / `DecompressPeakBlobs` (BlibLoader.cs:320, 389), which try raw-first then zlib (`TryZlibDecompress` skips the 2-byte zlib header and inflates with `DeflateStream`, BlibLoader.cs:293), tolerate f32 or f64 intensities, and normalize intensity to the max. Modifications are re-parsed from the `peptideModSeq` string (not the `Modifications` table) by `ParseBlibModifications` + `IdentifyModification` (BlibLoader.cs:181, 238), which recognizes common mods by mass within `MOD_TOLERANCE = 0.01` and handles both mass-shift (`[+57.0]`) and absolute-mass (`[160.0]`) notation.

---

## Flags and switches

This stage has no dedicated feature flags; it always runs when a `-o/--output` blib is produced. The values it stamps into the blib come from these config fields:

| Flag / field | Default | Effect on this stage |
|---|---|---|
| `-o` / `--output <path>` | required | `config.OutputBlib` — the `.blib` written (BlibOutputWriter.cs:73). |
| `-l` / `--library <path>` | required | `Path.GetFileName` becomes `SpectrumSourceFiles.idFileName` (BlibOutputWriter.cs:107). |
| `-i` / `--input <mzML...>` | required | one `SpectrumSourceFiles` row each; `fileName = <stem>.mzML` (BlibOutputWriter.cs:112). |
| `--run-fdr <v>` | 0.01 | `config.RunFdr` — `cutoffScore` and the ID-line threshold for `RetentionTimes.retentionTime` NULL-vs-apex (BlibOutputWriter.cs:62,297). Written to `OspreyMetadata.run_fdr`. |
| `--experiment-fdr <v>` | 0.01 | Written to `OspreyMetadata.experiment_fdr` (BlibOutputWriter.cs:270). Experiment q-value (via the FDR stage) becomes `RefSpectra.score` and `OspreyExperimentScores.ExperimentQValue`. |
| `--threads <count>` | all cores | `config.NThreads` — degree of parallelism for the fragment pre-compression pass (BlibOutputWriter.cs:82,131). Does not change output bytes. |
| `--fdr-level {precursor\|peptide\|both}` | precursor | Governs which precursors pass upstream; within this stage `EffectiveRunQvalue(FdrLevel.Both)` is hard-coded for the ID-line decision regardless of the global level (BlibOutputWriter.cs:252,296,310). |

There is no CLI switch to enable `OspreyCoefficients` output, to choose the score type, or to change the compression backend — all are fixed in code.

---

## Divergences from the Rust documentation

- **[STALE-RUST-DOC] Score type is GENERIC Q-VALUE (19), not PERCOLATOR QVALUE (14)** - Rust doc says `ScoreTypes` ID 14 = `PERCOLATOR QVALUE` and `RefSpectra.scoreType = 14`; C# seeds the full ProteoWizard BiblioSpec score-type list (ID 1 = PERCOLATOR QVALUE, ID 14 = MORPHEUS SCORE, ID 19 = GENERIC Q-VALUE) and stamps every spectrum with `SCORE_TYPE_GENERIC_QVALUE = 19`. Evidence: Osprey.IO/BlibWriter.cs:41, BlibWriter.cs:899-922, BlibOutputWriter.cs:336. Severity: minor.
- **[STALE-RUST-DOC] RefSpectra.score is a raw q-value, not `1 - q_value`** - Rust doc says `score = 1 - q_value` (higher is better); C# stores the raw experiment-precursor q-value (lower is better), consistent with score type 19's `PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT`. Evidence: Osprey.Tasks/BlibOutputWriter.cs:186-193,229. Severity: minor.
- **[STALE-RUST-DOC] LibInfo minorVersion is 11, not 10** - Rust doc says `minorVersion = 10`; C# writes 11. Evidence: Osprey.IO/BlibWriter.cs:40. Severity: info.
- **[STALE-RUST-DOC] idFileName is the library basename, not a copy of fileName** - Rust doc says `idFileName` = "Same as fileName"; C# sets `idFileName` to the library file basename (the ID source) and `fileName` to `<stem>.mzML`. Evidence: Osprey.Tasks/BlibOutputWriter.cs:107-113. Severity: minor.
- **[STALE-RUST-DOC] cutoffScore is the run-FDR threshold, not 0.0** - Rust doc says `cutoffScore = 0.0`; C# passes `config.RunFdr` (default 0.01). Evidence: Osprey.Tasks/BlibOutputWriter.cs:62,112. Severity: info.
- **[STALE-RUST-DOC] SpectrumSourceFiles stores a bare filename, not a computed relative path** - Rust doc describes computing relative paths (`../data/sample.mzML`) from the blib directory for WSL2/Windows portability; C# stores `<stem>.mzML` with no directory computation. Evidence: Osprey.Tasks/BlibOutputWriter.cs:112. Severity: minor.
- **[STALE-RUST-DOC] Modifications.position is 1-based, not 0-indexed** - Rust doc says `position` is a "0-indexed position in sequence"; C# converts internal 0-based positions to 1-based on write (and a standing test asserts it). Byte-identical `.blib` parity with Rust implies the Rust code also writes 1-based, so the doc is stale. Evidence: Osprey.IO/BlibWriter.cs:399-408; Osprey.Test/IOTest.cs:107-149. Severity: minor.
- **[STALE-RUST-DOC] Nullable-text columns are written as empty strings, not NULL** - Rust doc says `moleculeName`/`chemicalFormula`/`precursorAdduct`/`inchiKey`/`otherKeys` are NULL; C# inserts empty-string literals. Evidence: Osprey.IO/BlibWriter.cs:127-131. Severity: info.
- **[STALE-RUST-DOC] RetentionTimes best-run ID-line fallback is undocumented** - Rust doc says `retentionTime` is NULL for any run not passing run-level FDR; C# additionally guarantees at least one ID line per RefSpectra by populating `retentionTime` on the lowest-`run_qvalue` run when no run passes. Evidence: Osprey.Tasks/BlibOutputWriter.cs:288-313. Severity: minor.
- **[STALE-RUST-DOC] Osprey extension score/intensity fields are 0.0 placeholders** - Rust doc labels `OspreyRunScores.DiscriminantScore` a "Mokapot discriminant score", `PosteriorErrorProb` a PEP, and `OspreyPeakBoundaries.ApexIntensity` an intensity; C# writes 0.0 for all three (not plumbed through Stage 7), and there is no Mokapot in the C# port. Evidence: Osprey.Tasks/BlibOutputWriter.cs:249-254. Severity: minor.
- **[STALE-RUST-DOC] OspreyMetadata key set differs** - Rust doc lists keys `osprey_version`, `rt_calibration_enabled`, `run_fdr`, `fdr_method`; C# writes `osprey_version`, `search_mode`, `run_fdr`, `experiment_fdr`. Evidence: Osprey.Tasks/BlibOutputWriter.cs:266-271. Severity: minor.
- **[INTENTIONAL-CSHARP-DESIGN] Native managed SQLite writer, not ProteoWizard BiblioSpec** - The README frames the C# blib output as "reusing ProteoWizard BiblioSpec infrastructure"; in fact `BlibWriter` is a hand-rolled `System.Data.SQLite` writer that reuses only the BiblioSpec *schema* and Skyline's `BlibData`/`UtilDB` byte conventions (Ionic.Zlib level 6), not the C++ BiblioSpec code. Rust's `flate2` (stock-zlib backend) and this Ionic.Zlib path produce byte-identical `RefSpectraPeaks` blobs. Evidence: Osprey.IO/BlibWriter.cs:37, 980-1008. Severity: info.
- **[INTENTIONAL-CSHARP-DESIGN] Extra schema columns and indices not in the Rust doc tables** - C# declares `SpectrumSourceFiles.workflowType` and `ScoreTypes.probabilityType`, and creates three indices beyond the Rust doc's seven (`idx_expscores_refid`, `idx_coefficients_refid`, `idx_rettimes_refid`), for BiblioSpecLite/Skyline compatibility. Evidence: Osprey.IO/BlibWriter.cs:737,750, 573-575. Severity: info.

Everything else matches the Rust doc: the standard + extension table set, `RefSpectraPeaks` storing library theoretical fragments (f64 m/z / f32 intensity, zlib-with-raw-fallback), the nullable-`retentionTime` ID-line semantics, one `RetentionTimes` row per detected run, `copies = nRunsDetected`, `OspreyCoefficients` emitted with zero rows, and the write→finalize→atomic-rename flow. No `PORT-ERROR` was found: the C# implementation is internally consistent and (per the standing `Compare-Blib-Crossimpl.ps1` gate referenced in BlibWriter.cs) byte-identical to the Rust output, so the schema-value differences above reflect a stale Rust doc rather than a behavioral defect.

See also 07-fdr-control.md (source of the run/experiment q-values written here), 11-boundary-overrides.md (the reconciled peak boundaries in `RefSpectra`/`RetentionTimes`/`OspreyPeakBoundaries`), 10-cross-run-reconciliation.md (per-file boundary imputation), and 15-hpc-scoring-split.md (the `SecondPassFDR` worker that invokes this stage).
