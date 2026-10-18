# 13. BLIB Output Schema (C#)

> Pipeline stage: Output (Stage 7). C# port of Rust docs/08-blib-output-schema.md. Corresponds to Rust osprey BiblioSpec (.blib) output.

Osprey emits its final results as a **BiblioSpec (`.blib`)** SQLite database — the spectral-library format Skyline consumes for chromatogram extraction, ID lines, and quantification. The `.blib` format is the lab's own file type, and no project of ours writes it from scratch: Osprey writes it through the one managed writer in `pwiz_tools/Shared/BiblioSpec/BiblioSpec.IO` (`pwiz.BiblioSpec.IO.BlibWriter`, `System.Data.SQLite`), which CarafeSharp's predicted libraries and BiblioSpec's own BlibBuild and BlibFilter (the C# port's `BlibMaker`) go through as well, so a library any of them writes is the same database for every reader. The writer follows the schema (`BlibSchema`, one definition with its version history) and the byte conventions of Skyline's own `BlibData`/`BlibDb` writer closely enough to be byte-identical to the Rust output on the reference datasets.

Three layers are involved:

- **`Shared/BiblioSpec/BiblioSpec.IO/BlibWriter.cs`** — the SQLite layer: schema creation, prepared-statement inserts, zlib compression, the atomic temp-then-move, finalize. One instance == one `.blib` file. It writes one `BlibSpectrumRecord` per `RefSpectra` row and lets the writing tool add its own tables (`IBlibExtensionTables`).
- **`Osprey.IO/OspreyBlibWriter.cs`** — Osprey's adapter over it: the `osprey.*` keys it records in `LibMetadata` (below) and the column values Osprey has always written (score type 19, the spectrum ordinal as `SpecIDinFile`, zero rather than NULL for the ion mobility columns, the peptide text through `BlibPeptideText`).
- **`Osprey.Tasks/BlibOutputWriter.cs`** — the Stage 7 sequencer that composes rows from the passing-precursor lookup tables and drives `OspreyBlibWriter`. It is invoked once from `Osprey.Tasks/SecondPassFdrTask.cs` (the `SecondPassFDR` HPC worker — see 15-hpc-scoring-split.md).

Reading back (for library input, and for round-trip tests) is `Osprey.IO/BlibLoader.cs`.

---

## Overview

The blib contains the standard BiblioSpec tables (Skyline-compatible) and the optional `LibMetadata` table the shared writer creates in every library:

```text
Standard BiblioSpec tables (BlibSchema, created by BlibWriter.CreateSchema)
  LibInfo, ScoreTypes, IonMobilityTypes, SpectrumSourceFiles,
  RefSpectra, RefSpectraPeaks, RefSpectraPeakAnnotations,
  Modifications, Proteins, RefSpectraProteins, RetentionTimes

Optional tables (BlibSchema.CreateLibMetadata / CreateDecoyPairs)
  LibMetadata (key TEXT PRIMARY KEY, value TEXT)   - in every library a tool of ours writes
                                                     (each records its command line in it)
  DecoyPairs (RefSpectraID, IsDecoy, IsEntrapment, PairingID, TargetRefSpectraID, Method)
                                                   - in a library that supplies its decoys
```

`DecoyPairs` (written by CarafeSharp and by `--export-library`) is the library's decoy and
entrapment design, one row per spectrum of a library that has one (supplied decoys, or
entrapment peptides): `IsDecoy` and `IsEntrapment` are the row's side
and partition (the four kinds of an FDRBench pairing manifest), `PairingID` is the manifest's
`peptide_pair_index` - shared by a target, its entrapment target and both their decoys, which is
what pairs an entrapment peptide with its original target - `TargetRefSpectraID` is a decoy's
target spectrum at the same charge (a foreign key to `RefSpectra.id`, indexed), and `Method` how
the row's sequence was made from the row it was made from: `reverse` or `cycle` on a decoy,
`shuffle` or `foreign` on an entrapment target (from the pairing manifest's `entrapment_method`
column, which CarafeSharp's entrapment FASTA stage writes), null on an original target. A blib searched with its own decoys must carry it, and Osprey pairs decoys and
entrapment from it alone, with no manifest, reading it on every load that finds it
([01](01-decoy-generation.md) step 2, [24](24-library-contract.md)). The search output blib has
none: its decoys are not reported.

`LibMetadata` holds single-row values about the library as a whole (`BiblioSpec.IO/BlibMetadata.cs`). Keys are namespaced by the writing tool - Osprey's `osprey.version`, `osprey.command`, `osprey.search_mode`, `osprey.run_fdr`, `osprey.experiment_fdr`, `osprey.library_source`, `osprey.library_hash` (the validity of a library derived from a `.tsv` source, [14](14-intermediate-files.md) section 7) and the validity stamp `osprey.validity`; CarafeSharp's `carafe.command` - with a small unprefixed set every tool honors (`library_contract`, see [24](24-library-contract.md)). Every tool that makes or transforms a library records its full command line under `<tool>.command`, so a library carries how it was made. A reader tests for the table, never for `LibInfo.minorVersion`. Until 2026-10-15 the table was Osprey's own `OspreyMetadata` with unprefixed keys; `BlibMetadata.Read` falls back to that name for one release so a search started under it still resumes.

Every per-spectrum value Osprey reports is in the standard tables: the experiment q-value is `RefSpectra.score`, the run count `RefSpectra.copies`, the best run's apex and bounds the `RefSpectra` retention-time columns, and each run's q-value and peak boundaries its `RetentionTimes` row. Rust osprey writes four more tables beside them (`OspreyPeakBoundaries`, `OspreyRunScores`, `OspreyExperimentScores`, `OspreyCoefficients`); Osprey stopped writing them on 2026-10-15 (see "The dropped `Osprey*` tables" below).

The schema declares two columns the Rust doc's table listings omit: `SpectrumSourceFiles.workflowType TINYINT` and `ScoreTypes.probabilityType VARCHAR(128)`. Both exist for BiblioSpecLite compatibility with Skyline.

---

## Pipeline-order walkthrough

Stage 7 blib emission runs entirely inside `BlibOutputWriter.Write(...)`. The gating (which precursors pass, and which run is "best" per precursor) is done by the caller and handed in as pre-built lookup tables; this stage only composes rows.

### Step 1 — Create the file beside its final name

`new OspreyBlibWriter(path)` opens a `BlibWriter` on a `SimpleFileSaver` sibling temp of `config.OutputBlib`, with `PRAGMA journal_mode=MEMORY` and `PRAGMA synchronous=OFF` (the temp is not durable until it is moved, so there is nothing to journal). `Complete()` closes the connection and moves the temp over the final name, replacing any library there; a writer disposed without `Complete()` deletes the temp and leaves the final name as it was. A failure to put the library in place is reported as a `BlibOutputException` naming the output (BlibOutputWriter.CompleteBlib), as a failure to open it is.

### Step 2 — Schema + seed rows

`BlibWriter.CreateSchema` creates the standard tables and `LibMetadata` from `BlibSchema`, then the seed rows:

- **`LibInfo`**: one row, `libLSID = urn:lsid:proteome.gs.washington.edu:spectral_library:bibliospec:nr:<file name>` (the BiblioSpec LSID), `createTime` in ctime format, `numSpecs = 0` (updated at finalize), `majorVersion = 1`, `minorVersion` = `BlibSchema.CurrentMinorVersion` (11).
- **`ScoreTypes`**: the full ProteoWizard BiblioSpec score-type enumeration, IDs 0-20. ID 1 = `PERCOLATOR QVALUE`, ID 14 = `MORPHEUS SCORE`, **ID 19 = `GENERIC Q-VALUE`** with `probabilityType = PROBABILITY_THAT_IDENTIFICATION_IS_INCORRECT`. Osprey stamps every spectrum with score type **19** (`BlibWriter.SCORE_TYPE_GENERIC_QVALUE`).
- **`IonMobilityTypes`**: 0=none, 1=driftTime(msec), 2=inverseK0(Vsec/cm^2), 3=compensation(V).

Both the writer and `OspreyBlibTables` then compile one reusable prepared INSERT per row kind (`BlibWriter.PreparedInsert`) so the tight per-spectrum loops avoid re-parsing SQL.

### Step 3 — Source files

`CreateSourceFiles` (BlibOutputWriter.cs) adds one `SpectrumSourceFiles` row per input file via `OspreyBlibWriter.AddSourceFile`. Column semantics:

- `fileName = <fileStem>.mzML` (the spectrum source; `kvp.Key` is the file stem).
- `idFileName = Path.GetFileName(config.LibrarySource.Path)` — the **library** basename, i.e. the "ID source". Skyline expects the ID source to be the library file, not a copy of the spectrum path.
- `cutoffScore = fdrThreshold = config.RunFdr` (default 0.01).
- `workflowType = 1` (`BlibWriter.WORKFLOW_TYPE_DIA`, the one value Osprey's adapter writes).

The path stored is a bare `<stem>.mzML` filename, not a computed relative path with `../` segments.

### Step 4 — Parallel spectrum preparation

`PrepareSpectra` (BlibOutputWriter.cs) builds a `BlibSpectrum` in parallel for each passing precursor from its in-memory library entry (`libraryById[entry.EntryId]`). `BlibSpectrum.FromLibraryEntry` (Osprey.IO/BlibSpectrum.cs) is the one place a library entry becomes blib rows, shared with `--export-library` (`LibraryBlibWriter`):

- the **library theoretical fragments**, **sorted by m/z** (as BiblioSpec stores them; Skyline's `ReadPeaks` keeps the stored order), packed as little-endian `f64` m/z and `f32` intensity and zlib-compressed via `BlibWriter.CompressMzs` / `CompressIntensities`. These are the library predicted fragments, **not** observed DIA peaks — Skyline uses them to build XICs for the correct transitions;
- the modified sequence, built from the entry's `Modifications` (signed masses of at most four decimals with trailing zeros dropped, so a mass known only to one decimal keeps that precision for Skyline's matching; an N-terminal modification on the first residue; modifications at one residue summed). An entry with a modification its loader could not resolve keeps its own text, so distinct precursors never share a key;
- one `Modifications` row per modified residue, and the protein accessions;
- no `RefSpectraPeakAnnotations` rows: the table stays empty, as BiblioSpec leaves it (below).

The prepared spectra are held per-index so the sequential emit loop stays deterministic.

Compression detail (`BlibWriter.CompressMzs` / `CompressIntensities`): the shared writer uses **DotNetZip `Ionic.Zlib` at level 6** — the same library and level as Skyline's `pwiz.Skyline.Util.Extensions.UtilDB.Compress` — rather than `System.IO.Compression.DeflateStream`, which emits a subtly different byte stream on small inputs and would break byte parity with both Rust (`flate2` stock-zlib backend) and Skyline's `BlibData`. If the compressed output is not smaller than the raw bytes, the raw bytes are stored; the reader detects this by comparing blob length to the expected uncompressed size.

### Step 5 — RefSpectra + child rows (sequential, one per passing precursor)

`EmitSpectrumRows` (BlibOutputWriter.cs) iterates the best-per-precursor entries in order. For each:

- **`OspreyBlibWriter.AddSpectrum(BlibSpectrum, ...)`** fills one `BlibSpectrumRecord` (the `RefSpectra` row with its peak blobs, `Modifications` and proteins) and the shared writer inserts it. Fixed column values: `prevAA`/`nextAA = '-'`, `ionMobility`/`ccs`/`highEnergyOffset = 0.0` (not NULL, as Rust osprey writes them), `ionMobilityType = 0`, `totalIonCurrent = 0.0`, `SpecIDinFile` = the spectrum's ordinal from 0 as text, `moleculeName`/`chemicalFormula`/`precursorAdduct`/`inchiKey`/`otherKeys = ''` (empty strings). `peptideSeq` goes through `BlibPeptideText.StripFlankingChars` and `peptideModSeq` through `ConvertUnimodToMass` as well.
  - `retentionTime`/`startTime`/`endTime` = the cross-charge **shared** apex/start/end if the peptide was detected at multiple charges in this file (`sharedBounds`, BlibOutputWriter.cs:208-218), else the entry's own boundaries.
  - `copies = nRunsDetected` (count of runs this precursor was detected in, BlibOutputWriter.cs:198-204).
  - `score = scoreQvalue`, the **experiment-precursor q-value** (min across observations, `bestExpPrecursorQ`, BlibOutputWriter.cs:190-193). This is a raw q-value (lower = better), consistent with score type 19 being a "probability the ID is incorrect". `scoreType = 19`.
- **Modifications**: one `Modifications` row per modified residue, `position` converted from internal **0-based to 1-based** (`OspreyBlibWriter.ToBlibModifications`), `mass` the summed mass at that residue. Verified by `Osprey.Test/IOTest.cs` (`TestBlibModifications1Based`).
- **Proteins**: the shared writer de-duplicates accessions through its accession cache, inserting `Proteins` + `RefSpectraProteins` rows.
- **`WriteRetentionTimes`** (see Step 6) carries each run's q-value and peak boundaries; there are no other per-spectrum rows.

### Step 6 — RetentionTimes: nullable retentionTime for Skyline ID lines

`WriteRetentionTimes` (BlibOutputWriter.cs:279-339) emits one `RetentionTimes` row for **every run where this precursor was detected** (`entriesByPrecursor[key]` observations). The `retentionTime` column drives whether Skyline draws an ID line:

- If the run's `EffectiveRunQvalue(Both) <= fdrThreshold` → `retentionTime = apex RT` (ID line shown).
- Otherwise → `retentionTime = NULL` (a null `retentionTime` to `AddRetentionTime`): Skyline uses `startTime`/`endTime` for quantification boundaries but shows no ID line ("integrate here, but not an independent identification").

The C# adds a **fallback the Rust doc does not describe**: if *no* run passes run-level FDR, the run with the lowest `run_qvalue` still gets `retentionTime` populated (`bestRunFile`, BlibOutputWriter.cs:288-313), so every `RefSpectra` has at least one ID line. `bestSpectrum = 1` for the observation whose file equals the best-precursor file, else 0. `score = runQ`. Cross-charge shared boundaries are applied here too.

### Step 7 — Metadata

`WriteMetadata` (BlibOutputWriter.cs) writes the `LibMetadata` rows (via `INSERT OR REPLACE`): `osprey.version`, `osprey.search_mode = coelution`, `osprey.run_fdr`, `osprey.experiment_fdr`, and `osprey.command`, the full command line of the run. One more row, `osprey.validity`, holds the blib's embedded validity stamp (`OspreyBlibWriter.AddStamp` / `ReadStamp`; see [14](14-intermediate-files.md) section 8). The stamp and the command line are provenance, not content: the blib comparisons (`BlibComparer` in Osprey.Test, `Regression/BlibGolden.ps1`) exclude both, since an HPC chain reproducing a straight-through run has a different command line, and so must any cross-impl comparison.

### Step 8 — Complete

`Complete()`: updates `LibInfo.numSpecs` to the `RefSpectra` count, creates the indexes - BiblioSpec's own (`BlibSchema.AllIndexStatements`) and the shared writer's three per-spectrum ones on `RefSpectraProteins`, `Modifications` and `RetentionTimes` (`LibMetadata` is keyed by its primary key and gets none) - then closes the connection and moves the temp over the final name (Step 1). Every prepared statement is disposed before the connection: System.Data.SQLite keeps the file open until every statement on a connection is finalized, so a statement outliving the close would make the move fail.

### The dropped `Osprey*` tables

Until 2026-10-15 Osprey also wrote Rust osprey's four tool tables, one row per `RefSpectra` in `OspreyPeakBoundaries` (the best run's bounds and `IntegratedArea`), `OspreyRunScores` (the best run's q-value, with `DiscriminantScore` and `PosteriorErrorProb` as 0.0 placeholders) and `OspreyExperimentScores` (`RefSpectra.score` and `copies` again, plus the run count searched), and `OspreyCoefficients` created and indexed with no rows. They were dropped by decision: writing tool information to tables no reader supports is not to be encouraged, even where every reader ignores them, and every value they held that Skyline could use is already in `RefSpectra` and `RetentionTimes`. The regression golden (`Regression/BlibGolden.ps1`) and `Osprey.Test/BlibComparer` project the standard tables and `LibMetadata`; the cross-impl comparison with Rust (`ai/scripts/Osprey/Compare/Compare-Blib-Crossimpl.ps1`) no longer compares the four tables, an accepted divergence from Rust's output.

---

## Reading back (`BlibLoader.cs`)

`BlibLoader.Load` reads `RefSpectra` + `RefSpectraPeaks` (`LoadSpectra`) and `RefSpectraProteins`/`Proteins` (`LoadProteinMappings`). Peak blobs are decoded by `DecodeBlibPeaks` / `DecompressPeakBlobs`, which try raw-first then zlib (`TryZlibDecompress` skips the 2-byte zlib header and inflates with `DeflateStream`), tolerate f32 or f64 intensities, and normalize intensity to the max. Modifications are re-parsed from the `peptideModSeq` string (not the `Modifications` table); see "Modification masses from `peptideModSeq`" below.

### Fragment typing

`LoadSpectra` types every blib spectrum's peaks from m/z as it reads them
(`Osprey.Core/FragmentTyping.cs`), as Skyline does for a peptide library:

- **Candidates:** the primary b and y ions of `FragmentLadder` - fragment charge 1 to
  min(precursor charge, 2), no neutral losses - with m/z from
  `PeptideFragmentMass.CalculateFragmentMz`, stacked modifications included.
- **Tolerance:** the search's fragment tolerance (`--fragment-tolerance`; 0.5 Th under
  `--resolution unit`, ppm for HRAM), the tolerance chromatogram extraction uses, so a peak typed
  y6 is one extraction counts as y6. Typing runs once per library, at the configured tolerance,
  before any run's MS2 calibration sets that run's extraction tolerance.
- **Most intense first:** each peak takes the preferred candidate within tolerance that no peak
  has claimed. Each ion types one peak, as Skyline's library ranking lets each predicted ion match
  one peak, so a library listing `b2` and `b4^2` as two peaks at one m/z gets the two ions.
- **Preference:** the nearest, counting candidates within 1e-5 Th of each other as equally near
  (isobaric ions differ only by floating-point rounding; distinct ions of tryptic peptides come
  within 1e-4 Th); then the lower fragment charge (`b2` over `b4^2`); then y before b, then the
  shorter ion, so the choice is always made. A peak with a candidate in reach is never left
  untyped: Unknown says the peak was not produced by the peptide's fragmentation at all, which is
  worse than a possibly wrong choice between ions that each could have produced it. A peak no
  unclaimed candidate reaches stays Unknown, and decoy generation copies it unchanged.
- **Logged:** one line with the peaks typed, of all peaks, and the tolerance.
- **What typing changes:** the consecutive-ion feature, generated decoys (`DecoyGenerator`
  recomputes typed fragments' m/z for the permuted sequence), and the training export's
  library flags ([22](22-training-export.md)).
- **Resume and cache safety:** every blib search adds `;blibreader=2` to every task validity key,
  and the library composition carries `blib_reader:2` with the fragment tolerance the typed
  peaks were computed within - in the hash of a `.tsv` library's derived `.blib`, which this
  reader reads back, and of a `.libcache` an earlier release left ([14](14-intermediate-files.md)
  section 7). Both carry `BlibLoader.READER_VERSION`, which also covers the modification parsing
  below.

### Library annotations are not read

A blib's `RefSpectraPeakAnnotations` table is not read. Skyline designed it for small molecules,
and no proteomics software is known to write peptide fragment ions into it, so every blib peak is
typed by Osprey as above. A DIA-NN TSV's fragment type columns are different: they stay the
typing its search uses, and are compared with Osprey's own typing (`FragmentTypeCheck`):

- **Per peak:** *agree* when the stated ion is the one Osprey's own typing chooses; *library's
  choice* when it is another candidate within tolerance; *differ* when it is a primary b or y ion
  not within tolerance; *outside the model* when it is a loss, an a/c/x/z ion or a charge above 2.
- **Reported:** one line naming the library, with counts and the denominator - a warning when a
  peak differs, else information - and the first differing peaks under `--verbose`. A TSV with
  no fragment type column states nothing and costs no typing.

### Modification masses from `peptideModSeq`

`ParseBlibModifications` reads each bracket of `peptideModSeq` - a mass shift
(`PEPC[+57.021464]TIDE`), an absolute cysteine mass (`C[160.0]`) or a UniMod id (`[UniMod:4]`,
or `(UniMod:4)` as DIA-NN writes it) - and identifies it against Osprey's UniMod table
(`Osprey.Core/UniMod.cs`, masses from Skyline's `UniModData.cs`), as Skyline matches library
modifications:

- A mass matches a known modification allowed on its residue or terminus when both round to the
  same value at the precision the text prints, at most four decimals, or lie within 1e-4 Da
  (Skyline's `MassModification.Matches`). BiblioSpec's one-decimal text (`C[+57.0]`, `K[+8.0]`,
  `Q[-17.0]` at the N-terminus) gets the exact known mass. When two match, the table's order
  decides: Skyline's common modifications first (Acetyl before Trimethyl for `K[+42.0]`).
- A mass printed with four or more decimals that matches nothing is used as printed. One printed
  with fewer cannot be, and a UniMod id Osprey does not know or any other text cannot be
  identified: the library is refused, listing each such modification with the number of spectra
  carrying it and the first of them. Like Skyline, a bracket summing two modifications
  (`M[+58.0]`) is not split.
- An unsigned value between 100 and 200 is read as an absolute residue mass only on C
  (`C[160.03]` becomes carbamidomethyl); on any other residue, or signed, it is a mass shift,
  so `K[+114.042927]` is GlyGly.
- An N-terminal modification and a modification of the first residue both sit at position 0,
  and their masses add in every b ion.

The modification parsing is part of `BlibLoader.READER_VERSION`, so the `;blibreader=2` key term and
the `blib_reader:2` cache term cover it for every blib.

### The annotation table Osprey writes

Osprey writes `RefSpectraPeakAnnotations` empty, as BiblioSpec's `BlibBuild` does: a reader
computes peptide fragment types from m/z, as Skyline and Osprey do. Skyline reads the table from
every blib it opens (`BiblioSpecLite.ReadPeakAnnotations`), asserting that `mzObserved` equals
the peak's m/z (within 1e-7) and reading every text column as a string, so a writer that does
fill it must write `mzObserved` as the peak's m/z exactly and empty strings rather than NULL.

---

## Flags and switches

This stage has no dedicated feature flags; it always runs when a `-o/--output` blib is produced. The values it stamps into the blib come from these config fields:

| Flag / field | Default | Effect on this stage |
|---|---|---|
| `-o` / `--output <path>` | required | `config.OutputBlib` — the `.blib` written (BlibOutputWriter.cs:73). |
| `-l` / `--library <path>` | required | `Path.GetFileName` becomes `SpectrumSourceFiles.idFileName` (BlibOutputWriter.cs:107). |
| `-i` / `--input <mzML...>` | required | one `SpectrumSourceFiles` row each; `fileName = <stem>.mzML` (BlibOutputWriter.cs:112). |
| `--run-fdr <v>` | 0.01 | `config.RunFdr` — `cutoffScore` and the ID-line threshold for `RetentionTimes.retentionTime` NULL-vs-apex (BlibOutputWriter.cs:62,297). Written to `LibMetadata` as `osprey.run_fdr`. |
| `--experiment-fdr <v>` | 0.01 | Written to `LibMetadata` as `osprey.experiment_fdr`. Experiment q-value (via the FDR stage) becomes `RefSpectra.score`. |
| `--threads <count>` | all cores | `config.NThreads` — degree of parallelism for the fragment pre-compression pass (BlibOutputWriter.cs:82,131). Does not change output bytes. |
| `--fdr-level {precursor\|peptide\|both}` | precursor | Governs which precursors pass upstream; within this stage `EffectiveRunQvalue(FdrLevel.Both)` is hard-coded for the ID-line decision regardless of the global level (BlibOutputWriter.cs:252,296,310). |

There is no CLI switch to choose the score type or to change the compression backend — both are fixed in code.

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
- **[INTENTIONAL-CSHARP-DESIGN] The four `Osprey*` tool tables are not written** - Rust writes `OspreyPeakBoundaries`, `OspreyRunScores`, `OspreyExperimentScores` and `OspreyCoefficients` beside the standard tables; C# dropped them on 2026-10-15 (see "The dropped `Osprey*` tables"). The cross-impl blib comparison compares the standard tables and the metadata only. Evidence: Osprey.IO/OspreyBlibWriter.cs. Severity: minor.
- **[INTENTIONAL-CSHARP-DESIGN] The metadata table is `LibMetadata` with namespaced keys, not `OspreyMetadata`** - Rust writes `OspreyMetadata` with `osprey_version`, `search_mode`, `run_fdr`, `experiment_fdr` (its doc lists `rt_calibration_enabled` and `fdr_method` too, which it no longer writes); C# writes `LibMetadata`, the table every tool of ours shares, with `osprey.version`, `osprey.search_mode`, `osprey.run_fdr`, `osprey.experiment_fdr`, `osprey.command` and the stamp. The cross-impl comparison maps the three shared values by stripping the prefix. Evidence: Osprey.Tasks/BlibOutputWriter.cs (`WriteMetadata`); BiblioSpec.IO/BlibMetadata.cs. Severity: minor.
- **[INTENTIONAL-CSHARP-DESIGN] Native managed SQLite writer, not ProteoWizard BiblioSpec** - The README frames the C# blib output as "reusing ProteoWizard BiblioSpec infrastructure"; in fact `BlibWriter` is a hand-rolled `System.Data.SQLite` writer that reuses only the BiblioSpec *schema* and Skyline's `BlibData`/`UtilDB` byte conventions (Ionic.Zlib level 6), not the C++ BiblioSpec code. Rust's `flate2` (stock-zlib backend) and this Ionic.Zlib path produce byte-identical `RefSpectraPeaks` blobs. Evidence: Osprey.IO/BlibWriter.cs:37, 980-1008. Severity: info.
- **[INTENTIONAL-CSHARP-DESIGN] Extra schema columns and an index not in the Rust doc tables** - C# declares `SpectrumSourceFiles.workflowType` and `ScoreTypes.probabilityType`, and creates `idx_rettimes_refid` beyond the Rust doc's indexes, for BiblioSpecLite/Skyline compatibility. Evidence: BiblioSpec.IO/BlibSchema.cs, BlibWriter.cs. Severity: info.

Everything else matches the Rust doc: the standard table set, `RefSpectraPeaks` storing library theoretical fragments (f64 m/z / f32 intensity, zlib-with-raw-fallback), the nullable-`retentionTime` ID-line semantics, one `RetentionTimes` row per detected run, `copies = nRunsDetected`, and the write→finalize→atomic-rename flow. No `PORT-ERROR` was found: the C# implementation is internally consistent and (per the standing `Compare-Blib-Crossimpl.ps1` gate referenced in BlibWriter.cs) byte-identical to the Rust output, so the schema-value differences above reflect a stale Rust doc rather than a behavioral defect.

See also 07-fdr-control.md (source of the run/experiment q-values written here), 11-boundary-overrides.md (the reconciled peak boundaries in `RefSpectra`/`RetentionTimes`), 10-cross-run-reconciliation.md (per-file boundary imputation), and 15-hpc-scoring-split.md (the `SecondPassFDR` worker that invokes this stage).
