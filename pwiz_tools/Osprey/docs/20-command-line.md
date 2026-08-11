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
| `--cache-dir` | `<dir>` | Directory for the `.spectra.bin` cache (overrides `--work-dir`). |
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
| `--fdr-method` | `percolator \| gbdt \| simple` | `percolator` | FDR engine. `gbdt` is a **C#-only** gradient-boosted-tree classifier; `simple` is bare TDC. See [07-fdr-control.md](07-fdr-control.md). |
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

### Performance

| Option | Value | Default | Effect |
|--------|-------|---------|--------|
| `--parallel-files` | `[<N>]` | one at a time | Files scored concurrently (OUTER). No value = auto from free RAM + cores; `<N>` = exactly N. **Single-node** mode — do not use under an HPC scheduler that already fans out. |
| `--threads` | `<count>` | all cores | Per-file main-search threads (INNER), divided across concurrently-scored files. |

### Distributed / HPC

| Option | Value | Effect |
|--------|-------|--------|
| `--task` | `PerFileScoring \| FirstPassFDR \| PerFileRescoring \| SecondPassFDR` | Run exactly one pipeline task (one node = one task). Omit for the whole pipeline. See [15-hpc-scoring-split.md](15-hpc-scoring-split.md). |
| `--input-scores` | `<paths\|dir>` | One or more `.scores.parquet` files, or a single directory (non-recursive). Mutually exclusive with `--input`. |

### Logging

| Option | Effect |
|--------|--------|
| `--timestamp` | Prefix each output line with `[yyyy/MM/dd HH:mm:ss]`. |
| `--memstamp` | Prefix each line with managed + private memory in MB (pair with `--timestamp` for perf visualization). |
| `--log-file <path>` | Write all output to a file instead of stderr. |
| `--perf-stats` | Emit machine-parseable `[COUNT]`/`[TIMING]`/`[STAGE-WALL]` lines. |
| `--verbose` | Show implementer-grade detail (e.g. per-fold Percolator iterations). |

### Diagnostics & Info

| Option | Effect |
|--------|--------|
| `-d`, `--diagnostics` | Write the cross-impl bisection dump bundle (`OSPREY_DUMP_*`). See [18-peptide-trace.md](18-peptide-trace.md). |
| `--model-diagnostics` | Write a self-contained interactive HTML report of the trained scoring model and FDR calibration. |
| `-h`, `--help` | Show help. Accepts a format: `[ascii\|unicode\|sections\|html\|<Section>]`. |
| `-v`, `--version` | Show version. |

---

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

# join 1 — one process over ALL parquets (pass a DIRECTORY so order is deterministic)
osprey --task FirstPassFDR     --input-scores ./scores_dir -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
#   writes beside each parquet: <stem>.1st-pass.fdr_scores.bin, <stem>.reconciliation.json

# split 2 — one process per file (parquet + its two sidecars co-located)
osprey --task PerFileRescoring --input-scores s1.scores.parquet -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
#   writes: <stem>.scores-reconciled.parquet

# join 2 — one process over ALL reconciled parquets (writes out.blib)
osprey --task SecondPassFDR     --input-scores ./reconciled_dir -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
```

- `--input-scores` takes a **directory** (globbed and sorted internally) or an explicit
  file list (consumed in the order given). FirstPassFDR reconciliation is order-sensitive,
  so for `FirstPassFDR` and `SecondPassFDR` pass a directory or a deterministically sorted list.
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
| `OSPREY_PASS2_QVALUE` | Second-pass q-value mode: `protein-compact` (**default**) / `transfer-compete` / `transfer`. An unrecognized value is a startup ERROR - `percolator` was removed | [12](12-second-pass-fdr.md) |
| `OSPREY_GBT_*` | GBDT hyperparameters (with `--fdr-method gbdt`) | [07](07-fdr-control.md) |
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
  set — an unrecognized `--fdr-method` / `--fdr-level` value warns and falls back to the
  default rather than erroring (this is why the deprecated `--fdr-method fasttree` alias
  still resolves to `gbdt`).

## Divergences from the Rust CLI

The C# CLI is a redesign, not a flag-for-flag port; the output is unchanged. The
recurring differences (all in [DIVERGENCES.md](DIVERGENCES.md)):

- **HPC flags.** The Rust `--no-join` / `--join-at-pass` / `--join-only` family is
  replaced by the single `--task {PerFileScoring|FirstPassFDR|PerFileRescoring|SecondPassFDR}`
  selector. See [15-hpc-scoring-split.md](15-hpc-scoring-split.md).
- **`--fdr-method`.** Adds the C#-only `gbdt`; Mokapot is not wired to the CLI (`percolator`
  and `simple` only, plus `gbdt`). See [07-fdr-control.md](07-fdr-control.md).
- **`--fdr-level`.** No `protein` value (the enum is `precursor | peptide | both`);
  protein q-values are computed and reported but cannot gate the blib from the CLI.
