# 20. Command-Line Reference (C#)

> Reference doc (cross-cutting). The option tables below mirror the build-generated
> help (`Osprey --help`, archived at `../Documentation/Help/en/CommandLine.html`),
> which is generated from `Osprey/OspreyCommandArgs.cs` so it never drifts from the
> binary. This doc adds curated **unit (Stellar)** and **HRAM (Astral)** examples and
> ties each option back to the algorithm docs.

Osprey reads DIA mzML files plus a spectral library and writes a BiblioSpecLite
(`.blib`) library of FDR-controlled results that imports directly into Skyline. It
runs as a standalone .NET 8 executable on Windows and Linux.

```
osprey -i <file1.mzML ...> -l <library.tsv|.blib> -o <output.blib> [options]
```

The three required inputs are `-i`/`--input` (one or more mzML), `-l`/`--library`
(a DIA-NN TSV or `.blib`), and `-o`/`--output` (the result `.blib`). Everything else
has a sensible default; `--resolution` is the one flag you will almost always set.

A DIA-NN TSV is validated in full before any search: a value Osprey cannot read in any
column the library has (a fragment charge of `1.0` or `0`, an ion type other than
a/b/c/x/y/z, an unknown loss or decoy flag, an empty cell) or a modification with no known
mass refuses the library, listing each line and column to fix, as Skyline's transition
list import does. Nothing is guessed for a bad value.

---

## Quick start

### Unit resolution (e.g. Stellar)

```bash
# Single file, precursor + peptide FDR at 1%
osprey -i sample.mzML -l hela.tsv -o results.blib --resolution unit

# A replicate set, with protein-level FDR and a TSV report
osprey -i rep1.mzML rep2.mzML rep3.mzML -l hela.blib -o results.blib \
       --resolution unit --protein-fdr 0.01 --report results.tsv

# Glob + keep derived artifacts and the spectra cache out of a read-only data dir
osprey -i /data/stellar/*.mzML -l hela.tsv -o results.blib \
       --resolution unit --work-dir /scratch/osprey_run
```

Unit resolution has no usable MS1 features, so the two MS1 PIN features evaluate to
0.0 and the pick uses the Stellar-trained defaults where the learned pick is enabled
(see [06-peak-detection.md](06-peak-detection.md)). Fragment tolerance is forced to
`mz 0.5` at unit resolution regardless of `--fragment-tolerance`.

### HRAM (e.g. Astral)

```bash
# Single file at high resolution (10 ppm fragment tolerance is the default)
osprey -i sample.mzML -l predicted.tsv -o results.blib --resolution hram

# A larger replicate set with protein FDR; score several files at once
osprey -i /data/astral/*.mzML -l predicted.blib -o results.blib \
       --resolution hram --protein-fdr 0.01 --parallel-files

# Tighten the fragment tolerance to 8 ppm and write the model-diagnostics report
osprey -i sample.mzML -l predicted.tsv -o results.blib \
       --resolution hram --fragment-tolerance 8 --model-diagnostics
```

HRAM turns on the MS1 precursor-coelution and isotope-cosine features and selects the
Astral-trained defaults for the learned pick. `--resolution auto` (the default) infers
unit vs. HRAM from the data, but passing it explicitly is clearer for batch scripts.

### Useful extras

```bash
# Broaden the reconciliation pool (looser peptide gate for first-pass compaction)
osprey -i *.mzML -l lib.tsv -o out.blib --resolution hram --reconciliation-compaction-fdr 0.05

# Razor shared-peptide rollup for protein inference
osprey -i *.mzML -l lib.tsv -o out.blib --resolution hram --protein-fdr 0.01 --shared-peptides razor

# Trust decoys already in the library instead of generating reverse decoys
osprey -i *.mzML -l lib_with_decoys.tsv -o out.blib --resolution hram --decoys-in-library

# Timestamped, memory-stamped log to a file (for perf visualization)
osprey -i *.mzML -l lib.tsv -o out.blib --resolution hram --timestamp --memstamp --log-file run.log
```

---

## Options

Defaults and value lists are from `Osprey/OspreyCommandArgs.cs`; the parser accepts
`--name value` (space-separated), short aliases (`-i`), and a positional mzML fallback.

### General I/O

| Option | Value | Effect |
|--------|-------|--------|
| `-i`, `--input` | `<file1.mzML ...>` | Input mzML file(s). Variadic; also accepts positional mzML paths. |
| `-l`, `--library` | `<library.tsv\|.blib>` | Spectral library — DIA-NN TSV or BiblioSpec `.blib` (see [01-decoy-generation.md](01-decoy-generation.md), [14-intermediate-files.md](14-intermediate-files.md)). |
| `-o`, `--output` | `<output.blib>` | Output `.blib` (see [13-blib-output-schema.md](13-blib-output-schema.md)). |
| `--work-dir` | `<dir>` | Write derived artifacts **and** the spectra cache here, so the input data dir can be read-only. Default: beside the input. |
| `--output-dir` | `<dir>` | Directory for derived artifacts (overrides `--work-dir`). |
| `--cache-dir` | `<dir>` | Directory for the rebuildable caches - `.spectra.bin` and the `<library-leaf>.libcache` (overrides `--work-dir`). Required on a `--task` leg whose `--output-dir` differs from the data directory: such a leg has no raw input path to resolve the cache from. |
| `--report` | `<report.tsv>` | Also write a TSV report. |

### Scoring & Tolerance

| Option | Value | Default | Effect |
|--------|-------|---------|--------|
| `--resolution` | `unit \| hram \| auto` | `auto` | Resolution mode. Gates MS1 features and the default pick model; unit forces `mz 0.5` fragment tolerance. See [03-spectral-scoring.md](03-spectral-scoring.md), [06-peak-detection.md](06-peak-detection.md). |
| `--fragment-tolerance` | `<value>` | `10` | Fragment m/z tolerance (ignored at unit resolution). |
| `--fragment-unit` | `ppm \| mz` | `ppm` | Unit for `--fragment-tolerance`. |
| `--no-prefilter` | — | prefilter on | Disable the coelution signal pre-filter (scores every candidate; ~30% slower). See [06-peak-detection.md](06-peak-detection.md). |

### FDR & Protein Inference

| Option | Value | Default | Effect |
|--------|-------|---------|--------|
| `--run-fdr` | `<threshold>` | `0.01` | Run-level FDR threshold (also the Percolator train/test FDR). See [07-fdr-control.md](07-fdr-control.md). |
| `--experiment-fdr` | `<threshold>` | `0.01` | Experiment-level FDR threshold. |
| `--reconciliation-compaction-fdr` | `<threshold>` | `0.01` | Peptide q-value gate for first-pass compaction; loosen (e.g. `0.05`) to broaden the reconciliation pool. See [10-cross-run-reconciliation.md](10-cross-run-reconciliation.md). |
| `--protein-fdr` | `<threshold>` | off → 0.01 gate | Enable protein-level FDR at this threshold (parsimony always runs regardless). See [08-protein-parsimony.md](08-protein-parsimony.md). |
| `--fdr-level` | `precursor \| peptide \| both` | `precursor` | Which q-value gates the reported output. (`protein` is not a valid value.) |
| `--shared-peptides` | `all \| razor \| unique` | `all` | Shared-peptide handling for protein inference. See [08-protein-parsimony.md](08-protein-parsimony.md). |
| `--fdrbench` | `<input.tsv>` | off | Write an FDRBench-compatible input TSV (every reported target with the raw SVM score) for entrapment true-FDR. Level follows `--fdr-level`. See [fractional-entrapment.md](fractional-entrapment.md). |
| `--fdrbench-per-run` | — | off | With `--fdrbench`: one row per (precursor, run) using run-level q-values. |
| `--fdrbench-pass` | `1 \| 2 \| both` | `2` | With `--fdrbench`: which pass to emit (2 = reported second-pass survivors; 1 = full pre-compaction first-pass pool). |

### Decoys

| Option | Value | Effect |
|--------|-------|--------|
| `--decoys-in-library` | — | Trust decoys already in the library instead of generating reverse decoys (hard error if none recognised). See [01-decoy-generation.md](01-decoy-generation.md). |
| `--decoy-pairing-manifest` | `<manifest.tsv>` | FDRBench 5-column pairing manifest, used with `--decoys-in-library`. |
| `--write-pin` | — | Write PIN files for external tools (diagnostic only; the engine does not consume them). |

### Training Export

| Option | Value | Default | Effect |
|--------|-------|---------|--------|
| `--training-export` | - | off | Write `<stem>.training.parquet` per run: every target at run q <= `--training-export-max-q` with its full b/y ladder's observed intensities and per-ion interference evidence. Written by `PerFileRescoring`; adding it to a finished run writes only the exports and re-scores nothing. See [22-training-export.md](22-training-export.md). |
| `--training-export-max-q` | `<q>` | `--run-fdr` | With `--training-export`: the run precursor q-value a target must reach (second pass where `PerFileRescoring` wrote one, else first; [22](22-training-export.md)). |
| `--training-export-claimant-q` | `<q>` | 0.01 | With `--training-export`: the run q-value at which another target counts as a claimant of a shared peak. |
| `--training-export-xics` | - | off | With `--training-export`: also write each precursor's per-ion XIC matrix over its final peak. |

### Performance

| Option | Value | Default | Effect |
|--------|-------|---------|--------|
| `--parallel-files` | `[<N>]` | one at a time | Files scored concurrently (OUTER). No value = auto from free RAM + cores; `<N>` = exactly N. **Single-node** mode — do not use under an HPC scheduler that already fans out. |
| `--parallel-files-caching` | `[<N>]` | `--parallel-files` | Files whose spectra are cached concurrently (`--task SpectraCache`), overriding `--parallel-files` for that stage only. Same forms. A vendor decode is single-threaded (~20 MB/s per Thermo file), so this stage gains from far more lanes than scoring; `--threads` is not divided here. mzML parses still run one at a time, and a full run caches inside scoring at the scoring count. |
| `--parallel-files-scoring` | `[<N>]` | `--parallel-files` | Files scored concurrently (`PerFileScoring`, Stages 1-4), overriding `--parallel-files` for that stage only. Same forms. |
| `--parallel-files-rescoring` | `[<N>]` | `--parallel-files` | Files re-scored concurrently (`PerFileRescoring`, Stage 6, and Stage 7's per-run fold), overriding `--parallel-files` for that stage only. Same forms. Its per-file working set is a fraction of scoring's. |
| `--threads` | `<count>` | all cores | Per-file main-search threads (INNER), divided across the files a scoring or re-scoring stage runs concurrently. |

Each per-file stage resolves its own count, once, from its own flag, else `--parallel-files`, else
`OSPREY_MAX_PARALLEL_FILES` (not for caching), else one at a time, and logs a `File parallelism: N (...)` line naming
the argument that decided. First-pass FDR's lanes are separate (from `--threads` and free memory).
None of these counts enters a validity key or changes an output byte; one lane is the plain loop.

### Distributed / HPC

| Option | Value | Effect |
|--------|-------|--------|
| `--task` | `SpectraCache \| PerFileScoring \| FirstPassFDR \| PerFileRescoring \| SecondPassFDR \| TrainingExport \| ModelDiagnostics` | Run exactly one pipeline task (one node = one task). Omit for the whole pipeline. `SpectraCache` stages the `.spectra.bin` caches and needs no library; `TrainingExport` is `--training-export` with no `--task`: a selector, not a stage, that runs the whole pipeline with the export on, so a completed run writes only its missing exports; `ModelDiagnostics` regenerates only the `--model-diagnostics` report for a completed run. EVERY task takes `-i`/`--input-list` naming the data files; the parquets and sidecars are derived from their stems. See [15-hpc-scoring-split.md](15-hpc-scoring-split.md). |

### Logging

| Option | Effect |
|--------|--------|
| `--timestamp` | Prefix each output line with `[yyyy/MM/dd HH:mm:ss]`. |
| `--memstamp` | Prefix each line with managed + private memory in MB (pair with `--timestamp` for perf visualization). |
| `--log-file <path>` | Write all output to a file instead of stderr. |
| `--perf-stats` | Emit the machine-channel lines (`[COUNT]`, `[TIMING]`, `[BENCH]`, `[STAGE-WALL]`, `[PATH]`, `[TRAIN]`); see [Log format](#log-format). |
| `--verbose` | Show implementer-grade detail (e.g. per-fold Percolator iterations). |

### Diagnostics & Info

| Option | Effect |
|--------|--------|
| `-d`, `--diagnostics` | Write the cross-impl bisection dump bundle (`OSPREY_DUMP_*`). See [18-peptide-trace.md](18-peptide-trace.md). |
| `--model-diagnostics` | Write a self-contained interactive HTML report of the trained scoring model and FDR calibration. |
| `-h`, `--help` | Show help. Accepts a format: `[ascii\|unicode\|sections\|html\|<Section>]`. |
| `-v`, `--version` | Show version. |

### Log format

The log carries two kinds of line.

**Prose** is written for the person watching the run. It may be reworded in any change and
will be translated, so no script or test may key off it.

**Tagged lines** start with `[TAG]` and are the machine channel. Their text is ASCII, never
translated, and it is the only part of the log a script or test may read.

| Tag | Written when | Carries |
|-----|--------------|---------|
| `[TASK]` | always | a task's start, skip and finish: `[TASK] <Name>:starting` / `:skipping (outputs valid)` / `:done (<s>s)`. The names are the `--task` values. |
| `[COUNT]` | `--perf-stats` | a count, e.g. `[COUNT] library-fragments-released: released=N entries=M retained=K scope=rescore-gap-fill` |
| `[PATH]` | `--perf-stats` | which code route the run took, e.g. `[PATH] second-pass-join: per-run runs=3` |
| `[TIMING]`, `[STAGE-WALL]`, `[BENCH]` | `--perf-stats` | timings the perf tools read |
| `[TRAIN]` | `--perf-stats` | which population a model trained on |
| `[MEM <label>]` | `OSPREY_LOG_MEMORY` | a memory probe |

Prose that the user asked for with an option (`--model-diagnostics`, `--training-export`,
`-d`, an `OSPREY_*` setting) may carry a category tag (`[MODEL-DIAGNOSTICS]`,
`[TRAIN-EXPORT]`, `[BISECT]`, ...). The tag labels the line and stays ASCII; the text after
it is prose. In a plain default run `[TASK]` is the only tag.

**Warnings and errors are prose, not tags.** They start with `Warning:` and `Error:`, as in
Skyline's command line, and are translated with the rest of the text. The exit code and the
error lines always agree, as they do in Skyline:

| Exit code | Meaning |
|-----------|---------|
| 0 | success; no `Error:` line was written |
| 1 | failure; at least one `Error:` line says why |
| 2 | an `Error:` line was written but the run otherwise completed |

A script deciding whether a run failed reads the exit code. A script scanning a log for
errors matches `Error:` in every shipped language (`Error:`, `エラー：`, `错误：`) at the
start of the message, after any `--timestamp`/`--memstamp` columns - the shared
`CommandStatusWriter.IsErrorLine` does exactly that. If Osprey ever finds the two
disagreeing it repairs them and writes a `[PATH] exit-reconciled` line, which
`regression.ps1` treats as a failure.

Rules for code and for consumers:

- **Read tagged lines only.** A script or test that matches prose is a defect in the consumer,
  not a reason to freeze the prose.
- **Keyed lines are `[TAG] key: value` or `[TAG] key: name=value ...`.** Numbers use the
  invariant culture with no group separators. Adding a key is free; renaming one means updating
  its consumers (`regression.ps1`, the `ai/scripts/Osprey` tools) in the same change.
- **Every tag comes from `LogTag`** (`Osprey.Core/LogTag.cs`), and the route and count keys
  come from `LogKey` in the same file. Code writes `log.LogInfo(LogTag.COUNT, format, args)`
  through an `IOspreyLog`: that overload formats with the invariant culture, so a tagged line
  reads `12.3s` under every UI language. `OspreyLog.Write` is the one place that decides whether
  the line is emitted.
  `CodeInspectionTest.TestLogTagsComeFromLogTag` fails on a tag written as a string literal.

---

### Localization

Osprey's prose - the log, `--help`, warnings and errors - comes from resource files and follows
the user's culture, as Skyline's does:

- One `.resx` per assembly that writes user text: `OspreyCoreResources`, `OspreyIOResources`,
  `OspreyScoringResources`, `OspreyFDRResources`, `OspreyTasksResources` and `OspreyResources`
  (the exe). Each project opts in to ReSharper's `LocalizableElement` inspection with a
  `<Project>.csproj.DotSettings`, so a plain string literal fails `Build-Osprey.ps1 -RunInspection`.
- Translations are Japanese (`.ja.resx`) and Chinese (`.zh-Hans.resx`) only, produced by
  Skyline's translation pipeline (`pwiz_tools/Skyline/Executables/DevTools/ResourcesOrganizer`),
  which scans every `.resx` under `pwiz_tools`.
- Text written for a PERSON uses the current culture: in fr-FR a count reads `1 234 567` and a
  fraction `12,5 %`. Text written for a PROGRAM uses the invariant culture: every output file
  (blib, TSV report, FDRBench input, parquet, JSON, `.osprey.task`) and every tagged log line.
  A run under any culture writes byte-identical files.
- `@"..."` marks text that is deliberately NOT translated: tagged lines, file headings and keys,
  argument and environment variable names, internal-invariant exceptions, and diagnostics reached
  only through `-d` or an `OSPREY_*` setting.
- `--culture <name>` (internal, not in `--help`, as in Skyline) runs Osprey under a named culture
  instead of the OS one, e.g. `--culture fr-FR` or `--culture ja`. The unit tests take the same
  choice from `OSPREY_TEST_CULTURE` (`Build-Osprey.ps1 -RunTests -Culture ja-JP`); fr-FR and
  tr-TR are test cultures for number formatting, not translation targets.

## Distributed execution (HPC)

Run with no `--task` for the whole pipeline in one process. For distributed (HPC /
workflow-engine) execution the pipeline splits at its join / fan-out boundaries into
four single-task workers — **one node = one `--task`**:

```
PerFileScoring (split, per file) → FirstPassFDR (join, all files)
    → PerFileRescoring (split, per file) → SecondPassFDR (join, all files)
```

Pass the **same** `--library` and search options to every task; the parquet integrity
check rejects inputs whose search/library hash does not match.

```bash
# split 1 — one process per mzML (writes <stem>.scores.parquet, <stem>.calibration.json beside each input)
osprey --task PerFileScoring   -i s1.mzML -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01

# join 1 — one process over ALL runs (pass a sorted list so order is deterministic)
osprey --task FirstPassFDR     --input-list runs.txt -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
#   writes beside each parquet: <stem>.1st-pass.fdr_scores.bin, <stem>.reconciliation.json

# split 2 — one process per file (parquet + its two sidecars co-located)
osprey --task PerFileRescoring -i s1.mzML -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
#   writes: <stem>.scores-reconciled.parquet

# join 2 — one process over ALL runs, reading their reconciled parquets (writes out.blib)
osprey --task SecondPassFDR    --input-list runs.txt -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
```

- Every task names its runs by their **data files**, and the data file itself need not
  still exist: a task after Stage 4 is accepted when the run's `.scores.parquet` (or its
  reconciled sibling) is on disk, which is the state a staged worker directory is in.
  `--input-scores`, which named parquets instead, has retired - it was a second way of
  saying what `--task` already says.
- FirstPassFDR reconciliation is **order-sensitive**, so pass a deterministically sorted
  list. `--input-list` (one path per line) is what a cohort past a few hundred runs needs:
  446 `-i` paths is ~87% of the Windows command-line limit.
- Rehydration sidecars must travel with their parquet into each worker's working
  directory. Let the scheduler fan out (one file per split process) rather than
  `--parallel-files`, which is the single-node multi-file mode.

Full detail: [15-hpc-scoring-split.md](15-hpc-scoring-split.md).

---

## Environment variables

Env vars are the escape hatch for experimental / diagnostic behavior that is not on the
CLI; they are read once at process start. The ones most likely to matter:

| Variable | What it does | Doc |
|----------|--------------|-----|
| `OSPREY_PICK_LDA` / `OSPREY_PICK_LDA_MODEL` | Learned linear pick model, **on by default**; `OSPREY_PICK_LDA=0` restores the legacy product pick, and `OSPREY_PICK_LDA_MODEL` overrides the built-in with a JSON file | [06](06-peak-detection.md) |
| `OSPREY_PICK_DUMP_CANDIDATES` | Dump per-candidate pick terms for offline model training | [peak-model-training.md](peak-model-training.md) |
| `OSPREY_TRAIN_PICK_RUN` | First-pass training selection, **on by default**: each precursor is represented by one uniformly drawn run's best candidate peak. `OSPREY_TRAIN_PICK_RUN=0` restores the pre-26.1 cross-run maximum. C#-only — Rust still takes the maximum | [07](07-fdr-control.md) |
| `OSPREY_MAX_TRAIN_SIZE` | Cap on training rows (default 300000). Unchanged by the 26.1 selection flip: at matched FDP, 300K and 1M are indistinguishable | [07](07-fdr-control.md) |
| `OSPREY_SVM_C_TOLERANCE` | First-pass SVM C selection: keep the most regularized C within this fraction of the best inner-CV count (default 0.01, in [0, 1); anything else is a startup ERROR). 0 is the strict maximum, the pre-#4703 rule; Rust uses the 1% default with no opt-out, so leave it unset for cross-implementation comparisons. Set the same value on every node of a relay chain | [07](07-fdr-control.md) |
| `OSPREY_PASS2_QVALUE` | Second-pass q-value mode: `protein-compact` (**default**) / `transfer`. An unrecognized value is a startup ERROR - `percolator` and `transfer-compete` were removed | [12](12-second-pass-fdr.md) |
| `OSPREY_FDR_MODEL` | First-pass classifier: unset / `svm` = linear SVM (**default**), `gbdt` = **experimental** gradient-boosted trees (C#-only). An unrecognized value is a startup ERROR. Replaced the removed `--fdr-method` | [07](07-fdr-control.md) |
| `OSPREY_GBT_*` | GBDT hyperparameters; apply only under `OSPREY_FDR_MODEL=gbdt` | [07](07-fdr-control.md) |
| `OSPREY_EXPERIMENT_AGG` | Experimental first-pass experiment-wide aggregation (`max` / `mean-best-<N>`) | [07](07-fdr-control.md) |
| `OSPREY_MEANBEST2_FLOOR_MEAN` / `OSPREY_MEANBEST2_FLOOR_PCT` | Missing-run floor arm for `mean-best-<N>` (decoy mean / decoy percentile instead of the default median) | [07](07-fdr-control.md) |
| `OSPREY_DUMP_*` / `OSPREY_DIAG_*` | Cross-impl bisection dumps (also via `-d`) | [18](18-peptide-trace.md) |

The full set is enumerated in the relevant algorithm docs; there is no single flat
listing on the CLI by design (these are not user-facing knobs).

---

## Notes

- **Exit codes.** A failing run returns a non-zero process exit code, so a workflow
  engine can gate on it.
- **`--help` formats.** `osprey --help html` writes the same reference as HTML;
  `osprey --help <Section>` prints one group (e.g. `osprey --help "FDR & Protein Inference"`).
- **Value validation.** Osprey's tokenizer does **not** reject values outside the listed
  set - an unrecognized `--fdr-level` value warns and falls back to the default rather than
  erroring. An unknown ARGUMENT is an error, and that includes the removed `--fdr-method`.

## Divergences from the Rust CLI

The C# CLI is a redesign, not a flag-for-flag port; the output is unchanged. The
recurring differences (all in [DIVERGENCES.md](DIVERGENCES.md)):

- **HPC flags.** The Rust `--no-join` / `--join-at-pass` / `--join-only` family is
  replaced by the single `--task {PerFileScoring|FirstPassFDR|PerFileRescoring|SecondPassFDR}`
  selector. See [15-hpc-scoring-split.md](15-hpc-scoring-split.md).
- **No `--fdr-method`.** Rust's `--fdr-method {percolator|mokapot|simple}` has no C#
  counterpart and is rejected as an unknown argument (removed with no alias, #4543). The
  only choice left is the classifier inside the Percolator framework, and it moved to the
  `OSPREY_FDR_MODEL` environment variable (unset / `svm` = linear SVM, `gbdt` = the
  experimental C#-only trees). `simple` was deleted and Mokapot was never wired. A Rust
  command line that passes `--fdr-method percolator` must drop it. See
  [07-fdr-control.md](07-fdr-control.md).
- **`--fdr-level`.** No `protein` value (the enum is `precursor | peptide | both`);
  protein q-values are computed and reported but cannot gate the blib from the CLI.
