# 14. Intermediate Files, Caches & Cache Invalidation (C#)

> Pipeline stage: Cross-cutting (all stages). C# port of Rust docs/12-intermediate-files.md. Corresponds to Rust osprey intermediate file formats.

The C# Osprey port writes the same family of per-file intermediate artifacts as the Rust
tool, and — where cross-impl byte parity matters (spectra cache, scores parquet, FDR
sidecars, reconciliation JSON) — it writes them byte-for-byte identically so a cache produced
by one implementation is consumable by the other. This document describes each artifact as the
C# code actually writes and reads it, the SHA-256 hashing that gates cache reuse, and the two
resume mechanisms the port adds (a per-task `.osprey.task` validity sidecar and the
`.scores-reconciled.parquet` split output) that have no exact Rust doc counterpart.

## File overview

| File pattern | Format | C# writer/reader | Purpose |
|---|---|---|---|
| `<stem>.calibration.json` | JSON (Newtonsoft) | `Osprey.Chromatography/CalibrationIO.cs` | RT + MS1/MS2 mass calibration parameters |
| `<stem>.spectra.bin` | Custom binary v3 | `Osprey.IO/SpectraCache.cs` | Decoded MS1/MS2 spectra for fast reload |
| `<stem>.scores.parquet` | Apache Parquet (ZSTD) | `Osprey.IO/ParquetScoreCache.cs` | Scored entries: 21 PIN features, fragments, CWT candidates + footer metadata |
| `<stem>.scores-reconciled.parquet` | Apache Parquet (ZSTD) | `Osprey.Tasks/ReconciledParquetWriter.cs` | Stage 6 reconciled rewrite (separate file, not in-place) |
| `<stem>.1st-pass.fdr_scores.bin` | Custom binary v4 | `Osprey.IO/FdrScoresSidecar.cs` | SVM score + 4 q-values + PEP + run_protein_qvalue + experiment_aggregate_score after first-pass Percolator |
| `<stem>.2nd-pass.fdr_scores.bin` | Custom binary v4 | `Osprey.IO/FdrScoresSidecar.cs` | Same record shape after second-pass Percolator |
| `<stem>.reconciliation.json` | JSON (Newtonsoft) | `Osprey.IO/ReconciliationFile.cs` | Stage 5 planner output: actions, gap-fill targets, refined RT calibration |
| `<output>.<TaskName>.osprey.task` | JSON (hand-rolled) | `Osprey.Tasks/TaskValiditySidecar.cs` | **C# addition**: per-(output, task) resume validity record |
| `<lib>.<...>` library cache | Custom binary v2 | `Osprey.IO/LibraryCache.cs` | Parsed spectral library reload cache |
| `<output>.blib` | SQLite (BiblioSpec) | `Osprey.IO/BlibWriter.cs` | Final output; see 13-blib-output-schema.md |

All per-file artifact paths resolve their directory through `ArtifactPaths`
(`Osprey.IO/ArtifactPaths.cs:47`), so `--output-dir` / `--cache-dir` / `--work-dir`
redirection is applied atomically to every artifact.

---

## 0. Path resolution and safe writes (cross-cutting)

### `ArtifactPaths` directory redirection

`ArtifactPaths` (`Osprey.IO/ArtifactPaths.cs:47`) holds two process-wide static properties,
`OutputDir` and `CacheDir`, both defaulting to `null` (write beside the input file — the
historical default). `--work-dir` sets both; `--output-dir` / `--cache-dir` set them
individually.

- `ResolveOutputDir(inputPath)` (`ArtifactPaths.cs:77`) returns `OutputDir` when set, else the
  input file's own directory. Used by the scores parquet, calibration JSON, FDR sidecars, and
  reconciliation JSON.
- `ResolveCacheDir(inputPath)` (`ArtifactPaths.cs:91`) resolves the `.spectra.bin` location:
  explicit `CacheDir` → beside the data file if that directory is writable (probed once and
  memoized, `ArtifactPaths.cs:112`) → `OutputDir`. The cache is settings-independent, so a
  shared `CacheDir` lets many analyses reuse one parse.

### Atomic writes via `FileSaver` (the C# stand-in for Rust `copy_and_verify`)

Every cache writer stages through `Osprey.Core/FileSaver.cs`. The pattern is: construct a
`FileSaver(finalPath)`, which allocates a **sibling** temp file in the *same directory*
(`FileSaver.cs:68`); write to `saver.SafeName`; call `saver.Commit()`
(`FileSaver.cs:92`) to delete any existing destination and `File.Move` the temp into place; on
exception the `using` block's `Dispose()` (`FileSaver.cs:116`) deletes the temp without
touching the destination. A crash mid-write therefore leaves either the previous content or no
file — never a half-written destination a resume check could mistake for finished output.

This is the C# realization of Rust's "safe NAS file writes / `copy_and_verify`" pattern.
Because the temp is a sibling, the promote is an in-volume rename rather than a
local-temp → NAS cross-volume copy, which sidesteps the truncation risk `copy_and_verify`
guards against. Callers that use it: `ParquetScoreCache.WriteScoresParquet`
(`ParquetScoreCache.cs:265`, `:457`), `SpectraCache.SaveSpectraCache` (`SpectraCache.cs:85`),
`CalibrationIO.SaveCalibration` (`CalibrationIO.cs:50`), `FdrScoresSidecar.WriteInternal`
(`FdrScoresSidecar.cs:366`) and `PatchRunProteinQvalues` (`FdrScoresSidecar.cs:257`),
`ReconciliationFile.Save` (`ReconciliationFile.cs:203`), `LibraryCache.SaveCache`
(`LibraryCache.cs:77`), and `TaskValiditySidecar.Write` (`TaskValiditySidecar.cs:130`).

---

## 1. Calibration JSON (`<stem>.calibration.json`)

**C# source**: `Osprey.Chromatography/CalibrationIO.cs`, model types in
`Osprey.Chromatography/CalibrationParams.cs`.

`CalibrationIO.SaveCalibration` (`CalibrationIO.cs:42`) serializes a `CalibrationParams` with
Newtonsoft `Formatting.Indented`; `LoadCalibration` (`CalibrationIO.cs:62`) deserializes it.
The path is `<stem>.calibration.json` in `ArtifactPaths.ResolveOutputDir(inputFile)`
(`CalibrationIO.cs:97`, assembled at the call site
`PerFileScoringTask.cs:1736`).

The schema mirrors Rust's `CalibrationMetadata` / `MzCalibration` / `RTCalibrationParams`
(`CalibrationParams.cs` model classes carry the same `metadata` / `ms1_calibration` /
`ms2_calibration` / `rt_calibration` / `second_pass_rt` JSON keys). The metadata block written
by `ResolveCalibration` (`PerFileScoringTask.cs:1702`) populates
`calibration_successful`, `num_confident_peptides`, `num_sampled_precursors`, `timestamp`, and
the DIA `isolation_scheme` (so an HPC SecondPassFDR node with no mzML can rehydrate the gap-fill m/z
filter, `PerFileScoringTask.cs:1758`).

**How calibration is reused in C#.** The Rust doc says the calibration file is reused when its
`search_hash` matches, and deleted+recomputed when the hash is missing/stale. The C# port does
**not** implement that per-calibration-file check:

- `CalibrationMetadata.SearchHash` exists as a field (`CalibrationParams.cs:125`, with
  `NullValueHandling.Ignore`) to keep schema parity, but `ResolveCalibration` never assigns it,
  so the C# `.calibration.json` omits the `search_hash` key entirely.
- The only load path is `OSPREY_LOAD_CALIBRATION` (`PerFileScoringTask.cs:1623`, via
  `OspreyEnvironment.LoadCalibrationPath`), an explicit cross-impl bisection hook that loads a
  named JSON instead of computing. When it is unset and RT calibration is enabled, calibration
  is always recomputed and re-saved.
- Reuse-across-runs is instead handled one level up, at the *task* level, by the
  `.osprey.task` validity sidecar (Section 8) whose key already folds the search + library
  hashes: if that key matches, the whole `PerFileScoringTask` is skipped and no calibration
  runs at all; if it does not, the task re-runs and rewrites the calibration JSON. Net effect
  matches Rust (stale parameters ⇒ recalibrate) but the trigger is the task sidecar, not a
  `search_hash` embedded in the calibration file.

---

## 2. Binary spectra cache (`<stem>.spectra.bin`)

**C# source**: `Osprey.IO/SpectraCache.cs`. Path: `SpectraCache.GetCachePath` (`SpectraCache.cs:210`)
= `<stem>.spectra.bin` in `ArtifactPaths.ResolveCacheDir(inputFile)`.

A raw little-endian dump of decoded MS1 and MS2 spectra, written after the first parse and
reloaded during Stage 6 reconciliation re-scoring to avoid re-parsing mzML.

### Header and format (VERSION 3)

The C# header is **larger than the 20-byte header in the Rust doc** because it adds a source
fingerprint (`SpectraCache.cs:90`):

```
[magic:        8 bytes  "OSPRSPC\0"]
[version:      uint32   = 3]
[source_size:  uint64   source file length, 0 when unknown]
[source_mtime: int64    source last-write time, Unix ms UTC, 0 when unknown]
[n_ms2:        uint32]
[n_ms1:        uint32]
```

Per-MS2 record: `scan_number:u32, retention_time:f64, precursor_mz:f64, iso_center:f64,
iso_lower:f64, iso_upper:f64, n_peaks:u32, mzs:f64×n, intensities:f32×n`
(`SpectraCache.cs:99`). Per-MS1 record drops the three precursor/isolation fields
(`SpectraCache.cs:113`). Per-peak storage is 12 bytes (f64 m/z + f32 intensity), matching Rust.

The Unix-ms mtime is deliberately not .NET ticks so that C# and Rust compute an *identical*
fingerprint for the same file and can share one cache (`SpectraCache.cs:43`,
`ComputeSourceFingerprint` at `SpectraCache.cs:227`).

### Version-bump invalidation

`LoadSpectraCache` (`SpectraCache.cs:132`) returns `null` (⇒ re-parse + rewrite) on: missing
file, wrong magic, or `version != 3` (`SpectraCache.cs:152`). The version constant records its
own history (`SpectraCache.cs:61`): v1→v2 (2026-05-09) because non-monotonic centroids are now
sorted before caching; v2→v3 (2026-06-09) added the source fingerprint. Any older cache is
rejected and repopulated.

### Source-fingerprint invalidation

When the stored `source_size != 0` and a `sourcePath` is supplied, the loader recomputes the
fingerprint and rejects the cache if size or mtime changed (`SpectraCache.cs:162`). When the
cache recorded no fingerprint, or the source is unavailable (e.g. a resume run whose mzML is not
beside the cache), the check is skipped and the within-run cache is trusted.

Safe to delete: yes — recreated on next run.

---

## 3. Scores parquet cache (`<stem>.scores.parquet`)

**C# source**: `Osprey.IO/ParquetScoreCache.cs` (uses the managed `Parquet.Net` library, not a
native Arrow/Parquet dependency). Path: `GetScoresPath` (`ParquetScoreCache.cs:1029`).

Per-file cache of every scored entry (targets + decoys) with the 21 PIN feature columns, the
binary blob columns, boundary columns, and a footer of key-value metadata. Written after Stage
4 scoring, read selectively during FDR, reconciliation, and blib output.

### Schema

`BuildWriteSchema` (`ParquetScoreCache.cs:142`) writes 19 fixed columns followed by the 21 PIN
feature columns. Column types are deliberately aligned with Rust: `entry_id` and `scan_number`
are `UInt32`, `charge` is `UInt8`, so a C#-written parquet loads under Rust's strict downcasts
and vice versa (`ParquetScoreCache.cs:90`). Pre-2026-04-19 C#-written parquets used `Int32` and
must be regenerated.

- **Identity (8)**: `entry_id`, `is_decoy`, `sequence`, `modified_sequence`, `charge`,
  `precursor_mz`, `protein_ids` (nullable, `;`-joined), `file_name`.
- **Boundary (6)**: `scan_number`, `apex_rt`, `start_rt`, `end_rt`, `bounds_area`, `bounds_snr`.
- **Binary (5)**: `cwt_candidates`, `fragment_mzs`, `fragment_intensities`,
  `reference_xic_rts`, `reference_xic_intensities`.
- **Features (21)**: the names in `PIN_FEATURE_NAMES` (`ParquetScoreCache.cs:51`), all Float64,
  in the exact Rust `get_pin_feature_names()` order.

Rows are written in canonical sorted order `(entry_id, charge, scan_number)`
(`ParquetScoreCache.cs:226`, `:351`) so per-side parquets have identical physical row layout
regardless of which implementation wrote them. Compression is ZSTD
(`writer.CompressionMethod = CompressionMethod.Zstd`, `ParquetScoreCache.cs:270`, `:462`).

**Binary blob encoding.** The `FdrEntry` write overload (`ParquetScoreCache.cs:299`) encodes the
blobs exactly as Rust: `fragment_mzs` / `reference_xic_*` as little-endian f64 with no length
prefix (`EncodeF64Blob`, `ParquetScoreCache.cs:635`); `fragment_intensities` as little-endian
f32 (`EncodeF32Blob`, `ParquetScoreCache.cs:660`); `cwt_candidates` via `CwtCandidateCodec`
with a mandatory 4-byte count prefix so an empty candidate list is a zero-length blob, never a
null cell (`ParquetScoreCache.cs:399`–`:422`). Length is recovered on read as `bytes /
sizeof(element)`.

> **Note:** the older `CoelutionScoredEntry` write overload (`ParquetScoreCache.cs:182`) still
> writes the five binary columns as null placeholders (`ParquetScoreCache.cs:114`); populating
> them there is marked a future sprint. The pipeline's actual per-file writes go through the
> `FdrEntry` overload, which populates them.

### Selective loaders

Mirroring Rust's specialized loaders, the C# side never loads the full parquet unless needed:

| Loader (`ParquetScoreCache.cs`) | Columns read | Purpose |
|---|---|---|
| `LoadFdrStubsFromParquet` (`:723`) | 10 identity/boundary scalars | Lightweight `FdrEntry` stubs for FDR; sets `ParquetIndex = row` |
| `ReadFdrStubScalars` (`:788`) | 5 scalar columns, zero-alloc callback | Streams stub scalars for the first-pass FDR projection without materializing `FdrEntry`s |
| `LoadCwtCandidatesFromParquet` (`:835`) | `cwt_candidates` only | Stage 6 reconciliation planning |
| `LoadPinFeaturesFromParquet` (`:981`) | 21 feature columns | SVM re-scoring |
| `LoadFullFdrEntries` (`:897`) | all scalar + feature + blob columns | Full rehydration for Stage 6 reconciled write-back |

`ParquetIndex` bookkeeping is load-bearing: both write overloads and every loader assign
`ParquetIndex` to the post-sort row so Stage 5's per-file CWT lookup indexes each entry's own
candidate list. A code comment (`ParquetScoreCache.cs:364`) documents a bisected bug where the
in-memory path once left `ParquetIndex = 0` and force-integrated nearly every entry.

### Footer metadata and cache invalidation (SHA-256 hashes)

Every scores parquet carries footer key-value metadata. For a Stage 4 write
(`PerFileScoringTask.cs:228`):

| Key | Value | Source |
|---|---|---|
| `osprey.version` | `OspreyVersion.Current` (Skyline `YEAR.ORDINAL.BRANCH.DOY`) | `OspreyVersion.cs` |
| `osprey.search_hash` | SHA-256 hex | `SearchIdentity.SearchParameterHash()` |
| `osprey.library_hash` | SHA-256 hex | `SearchIdentity.LibraryIdentityHash()` |
| `osprey.reconciled` | `"false"` | literal |

The Stage 6 reconciled rewrite (`ReconciledParquetWriter.BuildReconciliationMetadata`,
`ReconciledParquetWriter.cs:192`) sets `osprey.reconciled = "true"` and adds
`osprey.reconciliation_hash` = `SearchIdentity.ReconciliationParameterHash[ForStems]()`.

**Hash recipes** (`Osprey.Core/SearchIdentity.cs`, must stay byte-identical to Rust
`osprey-core/src/config.rs`):

- `SearchParameterHash()` (`SearchIdentity.cs:60`): resolution mode; fragment + precursor
  tolerance (value + unit); prefilter enabled; decoy method; decoys-in-library; sorted
  lowercased decoy prefixes; decoy pairing manifest path (Rust `{:?}` `Some/None` escaping,
  `SearchIdentity.cs:186`); decoy pair min fraction; all RT-calibration parameters (enabled,
  fallback tolerance, tolerance factor, min/max tolerance, LOESS bandwidth, min calibration
  points, sample size, retry factor); and `reconciliation.top_n_peaks`. Booleans lowercased,
  invariant culture (`SearchIdentity.cs:69`) for cross-impl parity.
- `LibraryIdentityHash()` (`SearchIdentity.cs:143`): library **file name (not path) + size +
  mtime in Unix seconds** — a fast metadata check, no content hashing. Directory is deliberately
  excluded so the same library hashes identically across OSes / HPC nodes.
- `ReconciliationParameterHash()` (`SearchIdentity.cs:235`): the search hash, plus
  `reconciliation.enabled`, `reconciliation.consensus_fdr`, `run_fdr`, and the sorted+deduped
  input file stems. `ReconciliationParameterHashForStems` (`SearchIdentity.cs:262`) lets a
  per-file Stage 6 worker pass the join-wide stem set read from `reconciliation.json`.

**Validation on read.** `CheckParquetMetadata` (`ParquetScoreCache.cs:1190`) — a pure,
unit-testable mirror of Rust's `check_parquet_metadata` — compares cached vs expected
`osprey.version` (component-wise, hard-fail on any mismatch including the day-of-year build
component, `ParquetScoreCache.cs:1216`), then `search_hash`, then `library_hash`, returning a
descriptive error string or `null`. `ValidateScoresParquetGroup` (`ParquetScoreCache.cs:1256`)
runs it over a set of parquets at the start of `--task FirstPassFDR`, and additionally, when
`config.ExpectReconciledInput` (i.e. `--task SecondPassFDR`), requires every input carry
`osprey.reconciled = "true"` (`ParquetScoreCache.cs:1292`).

**No `CacheValidity` enum.** Rust models cache state with a tri-state
`CacheValidity { ValidReconciled, ValidFirstPass, Stale }` and, on `Stale`, silently deletes and
re-scores. The C# port has no such enum (the only reference is a stray comment at
`ReconciledParquetWriter.cs:180`). Instead:

- The HPC `--task` entry points call `ValidateScoresParquetGroup`, which **hard-fails with an
  error** on any hash/version mismatch rather than silently deleting and re-scoring — a cache
  whose compatibility cannot be verified is refused, not quietly discarded.
- The in-process straight-through pipeline decides skip-vs-recompute per task via the
  `.osprey.task` validity sidecar (Section 8), whose key folds the same search + library hashes.
  A key mismatch re-runs the task and overwrites the parquet (net behavior matches Rust's
  "stale ⇒ re-score", via a different mechanism).

### Reconciled output is a separate file, not an in-place overwrite

Stage 6 (`PerFileRescoreTask`) writes `<stem>.scores-reconciled.parquet`
(`GetReconciledScoresPath`, `ParquetScoreCache.cs:1055`) rather than overwriting the Stage 4
`.scores.parquet`. The `.scores-reconciled.parquet` suffix is appended **after** the `.scores`
token so it is an unambiguous "Stage 6 output" signal (`ParquetScoreCache.cs:1036`).
`EffectiveScoresPathFromScoresPath` (`ParquetScoreCache.cs:1103`) is the read-side contract: a
post-Stage-6 reader consumes the reconciled sibling when it exists on disk, else the original —
making the split-file design byte-equivalent to the former in-place overwrite while surviving a
partial Stage 6 crash. This is a C# infrastructure refinement over the Rust doc's single-file
model.

---

## 4. FDR score sidecars (`<stem>.{1st,2nd}-pass.fdr_scores.bin`)

**C# source**: `Osprey.IO/FdrScoresSidecar.cs`; record shape `Osprey.IO/FdrScoreRecord.cs`.
Paths: `Pass1Path` / `Pass2Path` (`FdrScoresSidecar.cs:139`, `:145`), routed through
`ArtifactPaths.ResolveOutputDir`.

Per-file persistence of FDR state at the Stage 5 → Stage 6 boundary (first pass) and after
second-pass FDR. Carries the SVM discriminant plus every q-value needed for downstream filtering
and protein-FDR-aware compaction.

### Format (v4) — byte-identical to Rust

`FdrScoresSidecar` writes a 32-byte header + fixed 68-byte records (`FdrScoresSidecar.cs:97`):

```
Header (32 bytes):
  [0..8]   magic         = "OSPRYFDR"
  [8]      version       = 4
  [9]      pass          = 1 (first) | 2 (second)
  [10..16] reserved (zero)
  [16..24] entry_count   u64 LE
  [24..32] reserved (zero)
Record (68 bytes):
  [0..4]   entry_id                     u32 LE
  [4..12]  svm_score                    f64 LE
  [12..20] run_precursor_qvalue         f64 LE
  [20..28] run_peptide_qvalue           f64 LE
  [28..36] experiment_precursor_qvalue  f64 LE
  [36..44] experiment_peptide_qvalue    f64 LE
  [44..52] pep                          f64 LE
  [52..60] run_protein_qvalue           f64 LE
  [60..68] experiment_aggregate_score   f64 LE
```

Field order and offsets are single-sourced in `WriteRecord` (`FdrScoresSidecar.cs:390`).
The v2→v3 bump (dated 2026-05-02 in the C# comment, `FdrScoresSidecar.cs:82`) added
`run_protein_qvalue` so a Stage 6 worker can reproduce the in-process compaction predicate
`run_peptide_qvalue ≤ 0.01 OR run_protein_qvalue ≤ 0.01`; records are written pre-compaction but
post first-pass protein FDR so the value is real, not the default 1.0. Cross-impl byte parity is
checked by a harness via the `OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT` hook (`FdrScoresSidecar.cs:43`).

The v3→v4 bump (2026-08-10, issue #4522) appended `experiment_aggregate_score`. `svm_score` is
the per-ROW discriminant, which is what the RUN-scope q-values compete on; the EXPERIMENT-scope
q-values compete on a per-entry roll-up across runs instead, and that quantity was never
persisted — so a consumer re-gating at experiment scope had to rebuild the roll-up itself and
branch on `OSPREY_EXPERIMENT_AGG`, which is silently wrong on exactly the arms where the
aggregation is under study. The new field is that roll-up: max over the entry's rows across runs
under the default aggregation, `ComputeBaseIdMeanBestN`'s value under mean-best-N, produced by
`PercolatorQValues.ComputeExperimentAggregateScoreMap` (resident/flat) and
`StreamingFdr.StreamingFirstPassQ.BuildExperimentAggregateScoreMap` (streaming), which
`FdrTest.TestStreamingFirstPassQMatchesFlat` pins against each other. Every row of an entry
carries the same value.

Two caveats worth keeping straight. It is **not** a general q→score inverse: the best-of-runs
clamp (`ClampExperimentQToBestRunFlat`, issue #4390) floors an experiment q up to a run q, so
after clamping the experiment q is not a monotone function of this score. And the field was
appended at the END specifically so every v3 offset is unchanged, which is what keeps
`PatchRunProteinQvalues`'s `[52..60]` patch valid without modification.

A two-phase write exists for the lean projection path (issue #4355): phase 1 writes records with
a 1.0 placeholder `run_protein_qvalue`; `PatchRunProteinQvalues` (`FdrScoresSidecar.cs:247`)
then streams the file one record at a time and overwrites only bytes `[52..60]` per entry_id,
producing a file byte-identical to a single-phase write.

### Validation and record→entry matching

`TryRead` (`FdrScoresSidecar.cs:437`) rejects: missing file, wrong magic, `version != 4`, a
`pass` byte that disagrees with `expectedPass`, and a file length that does not equal
`32 + 68 × header_count` (with checked arithmetic to reject an overflowing count,
`FdrScoresSidecar.cs:120`).

`ReadScalars` validates magic + version too, as of v4. It previously did not, which was
harmless only while the record width was fixed: at a changed `RecordLength` a stale v3 sidecar
would otherwise be re-cut at the new stride and yield plausible garbage rather than an error.

**Records are matched to entries by `entry_id`, not by position** (`FdrScoresSidecar.cs:484`).
This deliberately diverges from the Rust doc's stated "entry_count must match entries.len();
per-record entry_id checked at load". The C# loader tolerates `header_count < entries.Count`
(the first-pass sidecar is pre-gap-fill, the second-pass is post-compaction) by building an
`entry_id → index` dictionary; it still rejects a record whose `entry_id` is absent from the
caller's superset as corruption (`FdrScoresSidecar.cs:492`). `TryReadOverlay`
(`FdrScoresSidecar.cs:527`) is the dictionary-input variant used by `--task SecondPassFDR`,
which silently skips records not in the (already-compacted) caller dict. The class-summary
comment at `FdrScoresSidecar.cs:74` still describes the older position-based matching and is
stale relative to the method's actual entry_id join.

Safe to delete: the second-pass file, yes. The first-pass file is a Stage 5 → Stage 6 boundary
input; deleting it forces re-running the Stage 5 join.

---

## 5. Reconciliation boundary file (`<stem>.reconciliation.json`)

**C# source**: `Osprey.IO/ReconciliationFile.cs`. Path: `PathForInput`
(`ReconciliationFile.cs:216`) via `ArtifactPaths.ResolveOutputDir`.

Per-file JSON written at the tail of Stage 5 and consumed by the Stage 6 per-file rescore
worker. Together with the `<stem>.1st-pass.fdr_scores.bin` sidecar it is the Stage 5 → Stage 6
boundary pair.

### Cross-impl byte parity

Field declaration order is **alphabetical at every nesting level** via explicit `[JsonProperty(...
Order = N)]` attributes (`ReconciliationFile.cs:77`+), matching Rust's `serde_json`
alphabetical emission. Every `double` is routed through `RoundtripDoubleConverter`
(`ReconciliationFile.cs:185`, registered in the serializer settings) — the C# counterpart to
Rust's canonical fixed-point f64 formatter — sidestepping the Newtonsoft-`R`/Grisu vs.
Rust-`ryu` disagreement on small values. `Save` normalizes CRLF→LF and appends a trailing
newline (`ReconciliationFile.cs:194`) so the bytes match serde_json's LF output.

### Schema and version (v3, larger than the Rust doc's v1)

The C# `CurrentFormatVersion = 3` (`ReconciliationFile.cs:75`) and the envelope carries two
fields **absent from the Rust doc's v1 example**:

- `file_stems` (v2, `ReconciliationFile.cs:77`): the join-wide file set, so a per-file Stage 6
  worker can compute the reconciliation parameter hash the `--task SecondPassFDR` node
  expects (hash is over all joined files, not the worker's single parquet).
- `first_pass_base_ids` (v3, `ReconciliationFile.cs:84`): the join-wide set of base_ids that
  survived first-pass compaction, sorted ascending. A per-file worker uses it to compact to
  exactly the in-memory pipeline's set instead of a per-file subset.

The remaining fields match the Rust doc: `forced_integration_actions`
(`ForcedIntegrationEntry`), `format_version`, `gap_fill_targets` (`GapFillEntry`),
`library_hash`, `refined_rt_calibration` (`RefinedRtCalibrationJson` — LOESS
`abs_residuals`/`fitted_rts`/`library_rts`/`residual_sd`, reconstructed on the worker via
`RTCalibration.FromModelParams`), `search_hash`, and `use_cwt_peak_actions`
(`UseCwtPeakEntry`). As in Rust, `Keep` actions are not persisted and actions are split into two
homogeneous arrays to avoid a discriminator field.

### Validation

`Load` (`ReconciliationFile.cs:113`) rejects a missing file, malformed JSON, any
`format_version != 3` (`ReconciliationFile.cs:124`), a missing/empty `file_stems`
(`ReconciliationFile.cs:138`), and a missing `first_pass_base_ids`
(`ReconciliationFile.cs:150`) — all fail loudly to prevent a per-file worker from silently
computing a single-file hash for a multi-file join. The C# comments note the Rust
`reconciliation_io.rs` carries the matching fields to keep cross-impl byte parity.

For the full HPC orchestration story see 15-hpc-scoring-split.md.

---

## 6. BiblioSpec output (`<output>.blib`)

Final SQLite output; see 13-blib-output-schema.md. The C# port reuses ProteoWizard BiblioSpec
infrastructure where possible (`Osprey.IO/BlibWriter.cs`).

---

## 7. Library reload cache (`<...>` library cache)

**C# source**: `Osprey.IO/LibraryCache.cs`. Binary cache of a parsed spectral library for fast
reload; magic `"OSPRLBR\0"`, `VERSION = 2` (`LibraryCache.cs:49`). v2 stamps the source
library's `SearchIdentity.LibraryIdentityHash()` into the header immediately after the version
(`LibraryCache.cs:84`). `LoadCache` returns a tri-state `LibraryCacheStatus`
(`LibraryCache.cs:55`): `Invalid` (bad magic / wrong version), `IdentityMismatch` (stored hash
≠ expected — the entries are *not* read, skipping a multi-GB load on a stale cache,
`LibraryCache.cs:202`), or `Loaded`. This is the library-side analogue of the parquet
`library_hash` invalidation. (Not called out as a distinct artifact in the Rust doc, which
folds library identity into the parquet metadata check.)

---

## 8. Task validity sidecar (`<output>.<TaskName>.osprey.task`) — C# addition

**C# source**: `Osprey.Tasks/TaskValiditySidecar.cs`. This artifact has **no Rust doc
counterpart**; it is the resume-on-restart mechanism the C# port adds.

For each produced output, a small hand-rolled JSON file is written next to it at
`<output>.<TaskName>.osprey.task` (`TaskValiditySidecar.PathFor`, `TaskValiditySidecar.cs:80`).
It records the producing task, the Osprey version, a `validity_key`, and the input paths
(`TaskValiditySidecar.cs:98`). Example:

```json
{
  "task": "PerFileScoring",
  "version": "26.6.0",
  "validity_key": "search=abc...;library=def...",
  "inputs": ["/path/to/file.mzML", "/path/to/library.tsv"]
}
```

The default `validity_key` is `search=<SearchParameterHash>;library=<LibraryIdentityHash>`
(`OspreyTask.ValidityKey`, `OspreyTask.cs:173`); tasks with extra state (the rescore task)
override to append `ReconciliationParameterHash`. On the next invocation the driver
(`PerFileResumeDriver`) reads each output's sidecar and skips the producing task when
`IsValid` (`TaskValiditySidecar.cs:144`) confirms the recorded key matches the current key;
a missing/malformed/mismatched sidecar returns `false` ("can't tell ⇒ re-run", the conservative
answer). The task-name in the filename disambiguates per-task records for tasks that once shared
an output path (`TaskValiditySidecar.cs:71`). `Delete` (`TaskValiditySidecar.cs:169`) clears a
sidecar when its task starts, so a crash mid-write cannot leave a stale "valid" marker.

This is the C# equivalent of, and refinement over, Rust's implicit "skip-if-cache-valid"
behavior: it makes resume explicit and per-task rather than inferring state from parquet footer
hashes alone.

---

## Cleanup

All intermediate files are safe to delete and are recreated on the next run:

- `<stem>.spectra.bin` — recreated by re-parsing mzML.
- `<stem>.scores.parquet` / `<stem>.scores-reconciled.parquet` — recreated by re-scoring.
- `<stem>.{1st,2nd}-pass.fdr_scores.bin` — first-pass required by the Stage 6 worker; deleting
  forces a Stage 5 re-join.
- `<stem>.reconciliation.json` — deleting forces a Stage 5 re-join.
- `<stem>.calibration.json` — small; worth keeping across runs with the same LC-MS setup, but in
  C# recalibration is triggered by the task sidecar, not by this file's presence.
- `<output>.<TaskName>.osprey.task` — deleting forces the task to re-run.

---

## Flags and switches

Flags in this section affect where and whether the intermediate artifacts in this document are
written or reused. Defaults from `Osprey/OspreyCommandArgs.cs` + `Osprey.Core/OspreyConfig.cs`.

| Flag / env var | Default | Effect on this stage |
|---|---|---|
| `--work-dir <dir>` | unset (beside input) | Sets both `ArtifactPaths.OutputDir` and `CacheDir`; redirects every artifact |
| `--output-dir <dir>` | unset | Sets `ArtifactPaths.OutputDir` (scores parquet, calibration JSON, FDR sidecars, reconciliation JSON) |
| `--cache-dir <dir>` | unset | Sets `ArtifactPaths.CacheDir` (`.spectra.bin` only); a shared cache dir lets analyses reuse one parse |
| `--task <T>` | unset (in-process) | `FirstPassFDR`/`SecondPassFDR` trigger `ValidateScoresParquetGroup`; `SecondPassFDR` sets `ExpectReconciledInput` (requires `osprey.reconciled = "true"`). See 15-hpc-scoring-split.md |
| `--resolution {unit\|hram\|auto}` | `auto` | Folded into `SearchParameterHash` ⇒ changing it invalidates `.scores.parquet` and (via the task key) recalibration |
| `--fragment-tolerance` / `--fragment-unit` | ppm; unit-res forces mz 0.5 | Folded into `SearchParameterHash` |
| `--no-prefilter` | prefilter on | Folded into `SearchParameterHash` |
| decoy flags (`--decoys-in-library`, `--decoy-pairing-manifest`, decoy method/prefixes/pair-min-fraction) | Reverse; DECOY_/rev_/decoy_; 0.80 | All folded into `SearchParameterHash` |
| RT calibration params (bandwidth, tolerance factor, min/max tolerance, sample size, retry factor, min points, enabled) | see `RTCalibrationConfig` | Folded into `SearchParameterHash` |
| `reconciliation.top_n_peaks` (config) | config default | Folded into `SearchParameterHash` |
| `--run-fdr <v>` | 0.01 | Folded into `ReconciliationParameterHash` (⇒ `osprey.reconciliation_hash`) |
| `reconciliation.enabled` / `reconciliation.consensus_fdr` (config) | enabled; 0.01 | Folded into `ReconciliationParameterHash` |
| `--library <path>` | required | File name + size + mtime ⇒ `LibraryIdentityHash` ⇒ `osprey.library_hash` and the library reload cache identity |
| `OSPREY_LOAD_CALIBRATION` (env) | unset | Loads a named `.calibration.json` instead of computing (cross-impl bisection hook) |
| `OSPREY_EXIT_AFTER_CALIBRATION` (env) | unset | Exits after Stage 3, having written `.calibration.json` |
| `OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT` (env) | unset | Test hook for FDR sidecar byte-parity harness |

The `.osprey.task` validity sidecar is written unconditionally (not behind a flag); it is the
default resume mechanism.

---

## Divergences from the Rust documentation

- **[INTENTIONAL-CSHARP-DESIGN] Safe writes use `FileSaver` sibling-temp rename, not
  `copy_and_verify`** - Rust doc says all cache writers write to a local temp then
  `copy_and_verify` to the final (NAS) destination; C# stages through a *same-directory* sibling
  temp and `File.Move`s it into place. Evidence: `Osprey.Core/FileSaver.cs:68,92`; callers e.g.
  `ParquetScoreCache.cs:265`, `SpectraCache.cs:85`. Same crash-safety guarantee; in-volume
  rename rather than cross-volume copy. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] No `CacheValidity` enum; hash mismatch hard-fails instead of
  silent delete-and-rescore** - Rust doc defines `CacheValidity { ValidReconciled, ValidFirstPass,
  Stale }` and, on `Stale`, deletes the cache and re-scores from scratch. C# has no such enum;
  `ValidateScoresParquetGroup` returns a descriptive error and aborts the `--task` run on any
  version/hash mismatch, while the in-process pipeline decides skip-vs-recompute via the
  `.osprey.task` sidecar. Evidence: `ParquetScoreCache.cs:1190,1256`;
  `ReconciledParquetWriter.cs:180` (comment only). Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] `.osprey.task` validity sidecar (resume mechanism) has no Rust
  doc counterpart** - The C# port adds a per-(output, task) JSON sidecar keyed on the search +
  library (+ reconciliation) hashes to drive resume-on-restart; Rust infers reuse from parquet
  footer hashes. Evidence: `Osprey.Tasks/TaskValiditySidecar.cs`; `Osprey.Tasks/OspreyTask.cs:173`.
  Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Reconciled parquet is a separate `.scores-reconciled.parquet`,
  not an in-place overwrite** - Rust doc's model rewrites `.scores.parquet` in place during Stage
  6; C# writes a distinct sibling and selects it on read via
  `EffectiveScoresPathFromScoresPath`, surviving a partial Stage 6 crash. Evidence:
  `ParquetScoreCache.cs:1036,1055,1103`; `ReconciledParquetWriter.cs`. Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] FDR sidecar loader matches records by `entry_id`, tolerating
  `count < entries.len()`** - Rust doc says `entry_count` must equal `entries.len()` and records
  align positionally with an entry_id check. C# builds an `entry_id → index` map and applies
  records by id, deliberately allowing a smaller pre-gap-fill / post-compaction sidecar over a
  larger parquet stub list. Evidence: `FdrScoresSidecar.cs:437,484,492`. The class-summary comment
  at `FdrScoresSidecar.cs:74` describing positional matching is internally stale. Severity: minor.

- **[STALE-RUST-DOC] `reconciliation.json` is format_version 3 with `file_stems` and
  `first_pass_base_ids`, not the doc's v1** - The Rust doc shows `"format_version": 1` and omits
  both fields; the C# envelope requires v3 and both fields, and its comments state the Rust
  `reconciliation_io.rs` carries the matching fields for byte parity — i.e. the Rust code evolved
  past its own doc. Evidence: `ReconciliationFile.cs:75,77,84,124,138,150`. Severity: minor.

- **[STALE-RUST-DOC] `.spectra.bin` header is v3 with a source fingerprint, not the doc's
  20-byte v1** - The Rust doc's header shows `version = 1` and no size/mtime; C# writes v3 with
  `source_size:u64` + `source_mtime:i64` (Unix ms) and invalidates on both version bump and
  fingerprint change, computing the fingerprint as Unix-ms specifically to match Rust. Evidence:
  `SpectraCache.cs:61,70,90,162`. The matching fingerprint implies the Rust code also advanced;
  the doc did not. Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] Calibration reuse is not gated on a `search_hash` inside
  `.calibration.json`** - Rust doc: the calibration file is reused when its `search_hash`
  matches and deleted+recomputed when it is stale. C# never writes `search_hash` into the
  calibration JSON (the field exists for schema parity but is unset), and reuse is instead the
  task-level `.osprey.task` sidecar decision; the only direct-load path is the
  `OSPREY_LOAD_CALIBRATION` bisection hook. Evidence: `PerFileScoringTask.cs:1702` (metadata
  built without `SearchHash`), `CalibrationParams.cs:125`, `CalibrationIO.cs:42`. Net recalibrate-
  on-stale behavior is preserved via the sidecar. Severity: minor.

- **[UNVERIFIED] Mokapot / Python calibration report tooling** - The Rust doc references
  `python scripts/evaluate_calibration.py` for calibration visualization. The C# port has no
  Python dependency (it emits an HTML report via `--model-diagnostics`); whether an exact
  equivalent of that specific script's output exists was not verified in this stage's code.
  Evidence: absence in `Osprey.Chromatography/CalibrationIO.cs` and CLI. Severity: info.

Verified as matching the Rust documentation: the `.scores.parquet` schema (19 fixed + 21 PIN
feature columns, UInt32/UInt8 identity types, ZSTD, canonical `(entry_id, charge, scan_number)`
row sort) and its footer keys (`osprey.version/search_hash/library_hash/reconciled/
reconciliation_hash`); the binary blob encodings (f64/f32 little-endian, cwt count-prefixed);
the FDR sidecar v3 32-byte header + 60-byte record layout and its field offsets; the search /
library / reconciliation SHA-256 hash recipes (invariant culture, lowercase booleans, Rust
`{:?}` escaping); and the reconciliation JSON alphabetical field order with roundtrip-stable f64
formatting.
