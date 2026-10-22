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
Osprey -i path/to/file ... -l path/to/file.blib|.tsv -o path/to/file.blib [options]
Osprey -i path/to/file ... --fasta path/to/file.fasta -o path/to/file.blib [options]
```

The three required inputs are `-i`/`--input` (one or more mzML), a library - `-l`/`--library`
(a DIA-NN TSV or `.blib`) or `--fasta` (a protein FASTA the library is predicted from, see
[Library build](#library-build)) - and `-o`/`--output` (the result `.blib`). Everything else
has a sensible default; `--resolution unit` is the one a search of unit-resolution data (an ion
trap such as Stellar) must give, since the default is `hram` and nothing detects it from the data.

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
Osprey -i sample.mzML -l hela.tsv -o results.blib --resolution unit

# A replicate set, with protein-level FDR
Osprey -i rep1.mzML rep2.mzML rep3.mzML -l hela.blib -o results.blib \
       --resolution unit --protein-fdr 0.01

# Glob + keep derived artifacts and the spectra cache out of a read-only data dir
Osprey -i /data/stellar/*.mzML -l hela.tsv -o results.blib \
       --resolution unit --work-dir /scratch/osprey_run
```

Unit resolution has no usable MS1 features, so the two MS1 PIN features evaluate to
0.0 and the pick uses the Stellar-trained defaults where the learned pick is enabled
(see [06-peak-detection.md](06-peak-detection.md)). Fragment tolerance is forced to
`mz 0.5` at unit resolution regardless of `--fragment-tolerance`.

### HRAM (e.g. Astral)

```bash
# Single file at high resolution (10 ppm fragment tolerance is the default)
Osprey -i sample.mzML -l predicted.tsv -o results.blib --resolution hram

# A larger replicate set with protein FDR; score several files at once
Osprey -i /data/astral/*.mzML -l predicted.blib -o results.blib \
       --resolution hram --protein-fdr 0.01 --parallel-files

# Tighten the fragment tolerance to 8 ppm and write the model-diagnostics report
Osprey -i sample.mzML -l predicted.tsv -o results.blib \
       --resolution hram --fragment-tolerance 8 --model-diagnostics
```

HRAM turns on the MS1 precursor-coelution and isotope-cosine features and selects the
Astral-trained defaults for the learned pick. `hram` is the default; the resolution is not
inferred from the data, so a unit-resolution search must pass `--resolution unit`.

### Useful extras

```bash
# Broaden the reconciliation pool (looser peptide gate for first-pass compaction)
Osprey -i *.mzML -l lib.tsv -o out.blib --resolution hram --reconciliation-compaction-fdr 0.05

# Razor shared-peptide rollup for protein inference
Osprey -i *.mzML -l lib.tsv -o out.blib --resolution hram --protein-fdr 0.01 --shared-peptides razor

# Trust decoys already in the library instead of generating reverse decoys
Osprey -i *.mzML -l lib_with_decoys.tsv -o out.blib --resolution hram --decoys-in-library

# Timestamped, memory-stamped log to a file (for perf visualization)
Osprey -i *.mzML -l lib.tsv -o out.blib --resolution hram --timestamp --memstamp --log-file run.log
```

---

## Options

Defaults and value lists are from `Osprey/OspreyCommandArgs.cs`; the parser accepts
`--name value` (space-separated), short aliases (`-i`), and a positional mzML fallback.

### General I/O

| Option | Value | Effect |
|--------|-------|--------|
| `-i`, `--input` | `path/to/file ...` | Mass spectrometry data files: mzML, or any format ProteoWizard reads (vendor files where the vendor readers are installed). Variadic; also accepts positional paths. |
| `-l`, `--library` | `path/to/file.blib\|.tsv` | Spectral library — DIA-NN TSV or BiblioSpec `.blib` (see [01-decoy-generation.md](01-decoy-generation.md), [14-intermediate-files.md](14-intermediate-files.md)). |
| `-o`, `--output` | `path/to/file.blib` | The search result, for Skyline (see [13-blib-output-schema.md](13-blib-output-schema.md)): an entry per precursor that passes FDR with the library spectrum it was searched with (the predicted one with `--fasta`), its best run's retention time and boundaries, its experiment-wide precursor q-value and PEP and peptide q-value, and a row per run it was detected in with that run's retention time, boundaries and q-value; its protein groups with their q-values. (`OSPREY_DIANN_REPORTS=1` also writes `<output>.protein_groups.tsv` and `<output>.stats.tsv`, the same results in the shape of DIA-NN's reports, for comparing the two tools.) |
| `--work-dir` | `path/to/folder` | Write derived artifacts **and** the spectra cache here, so the input data dir can be read-only. Default: beside the input. |
| `--output-dir` | `path/to/folder` | Directory for derived artifacts (overrides `--work-dir`). |
| `--cache-dir` | `path/to/folder` | Where Osprey keeps the files it builds once and reuses in later searches: each run's `.spectra.bin` and, for a `.tsv` library, the `.blib` it is converted to (`hela.tsv.blib`). By default each goes beside the file it comes from, or under `--output-dir` / `--work-dir` when that folder is read-only. Give it to put them elsewhere (data and library on a read-only share, or one cache shared by several analyses). An HPC run whose `--output-dir` is away from the data should give every `--task` node the same one. |
| `--export-library` | `path/to/file.blib` | Write the `-l` library to this path as a `.blib` and exit without searching: the same file a search caches for a `.tsv` library (`hela.tsv.blib`, through the same `LibraryBlibWriter`), with its decoys and pairing in its own `DecoyPairs` table (with `--decoys-in-library`, and `--decoy-pairing-manifest` for a `.tsv`), so it needs no manifest and searches back exactly as the original did. It differs from the cache only in recording its command line, carrying no validity hash, and prefixing decoy accessions. |

### Library build

One command goes from a protein FASTA and the runs to a `.blib`, the way DIA-NN's does from
`--fasta` and `--f`: the library is predicted in-process with CarafeSharp's models and then
searched by the same four stages, with no library-building step the user runs and no second
executable. Three modes, chosen by the options given, on top of the search of a given library:

| Mode | Command (shape) | What runs |
|---|---|---|
| search | `Osprey -i runs... -l library.blib -o out.blib` | the four stages |
| predict | `Osprey -i runs... --fasta proteome.fasta -o out.blib` | predict with the pretrained models (AlphaPeptDeep MS2, Chronologer RT), then the four stages |
| tune | `Osprey -i runs... --fasta proteome.fasta --training-input run2 -o out.blib` | predict with the pretrained models, search the tuning runs with the training export, fine-tune the models on it, predict again with the tuned models, then the four stages |
| saved model | `Osprey -i runs... --fasta proteome.fasta --model out.model.zip -o out2.blib` | as predict, with a model an earlier tuned run saved (any proteome) |

Tuning is never inferred: without `--training-input` the pretrained models (or `--model`) predict
the library. A tuning run may be one of the `-i` runs (the lab's "search the middle file") or not
(held-out tuning); one that is not is searched for the tuning only, absent from the result's
`SpectrumSourceFiles` and recorded in its `LibMetadata` as `osprey.training_input`. `--model`
with `--training-input` tunes the saved model further.

| Option | Value | Effect |
|--------|-------|--------|
| `--fasta` | `path/to/file.fasta` | Predict the library from this proteome and search it, in place of `-l`. Refused together with `-l`, `--export-library` and `--decoy-pairing-manifest`. On a cluster each phase is a `--task` of its own (see "The library build on a cluster" below). |
| `--training-input` | `path/to/file ...` | Tune the models on these runs before the library is predicted (variadic, as `-i`). Their training exports (`--training-export-max-q` and the other export options apply to them) fine-tune the MS2 and RT models; the tuned model is saved as `<out>.model.zip`. The options of the **Library Tuning** section apply. |
| `--training-input-list` | `path/to/file.txt` | A file of `--training-input` runs, one path per line (`#` comments ignored), as `--input-list` is for `-i`: a long run list can pass the Windows command-line limit and hides the other arguments. |
| `--model` | `path/to/file.model.zip` | Predict with this saved fine-tuned model instead of the pretrained models; with `--training-input`, start the tuning from it. Its training run's NCE, instrument and gradient length apply where the command line gives none. |
| `--device` | `gpu \| cpu` | Where the library is predicted. `gpu` (the default) falls back to the CPU with a warning when no CUDA runtime is present, as CarafeSharp does. |
| `--entrapment` | `[<ratio>]` | Add an entrapment peptide to every target (or to this fraction of them, 0.1 to 1), shuffled from the target or, with `--entrapment-fasta`, drawn mass-matched from a foreign proteome, each with a decoy of its own. The pairing manifest FDRBench reads is written beside the working files. |
| `--entrapment-fasta` | `path/to/file.fasta` | With `--entrapment`: the foreign-species proteome to draw from. |

**The library-build parameters are CarafeSharp's.** The digest (`--enzyme`, `--miss-c`,
`--min-length`, `--max-length`, `--clip-n-m`, `--i2l`), the modifications (`--fix-mod`,
`--var-mod`, `--max-var`), the charges and precursor m/z window (`--min-pep-charge`,
`--max-pep-charge`, `--min-pep-mz`, `--max-pep-mz`), the fragments kept
(`--lf-frag-mz-min`, `--lf-frag-mz-max`, `--lf-top-n-frag`, `--lf-min-n-frag`,
`--lf-frag-n-min`), the acquisition the models predict for (`--nce`, `--ms-instrument`,
`--activation`, `--analyzer`, `--rt-max`, `--ccs`), the RT model (`--rt-model`) and the
pretrained archive (`--pretrained`) are CarafeSharp's declarations,
presented in Osprey's help as the sections **Library Digest** and **Library Prediction** under
Osprey's grammar, with CarafeSharp's text and defaults. The tuning options are likewise, in
**Library Tuning**: which models train (`--tf`), Carafe's rules for the ions trained on (`--cor`,
`--n-ion-min`, `--c-ion-min`, `--nf`, `--min-n`, `--valid`, `--no-masking`), the seed (`--seed`)
and the training runs' RT alignment (`--rt-align`, `--rt-select`, `--rt-reference`); any of them
without `--training-input` is refused. Their defaults are CarafeSharp's, which are Carafe's; the
lab's workflow script passes `--cor 0.8 --n-ion-min 2 --c-ion-min 2 --valid`. They are listed in the standard form
(`--miss-c 1`); for compatibility with Java Carafe's command lines, each is also accepted with
Carafe's underscores (`--miss_c 1`). Any of them without `--fasta` is refused rather than
ignored. What Osprey derives is not surfaced: CarafeSharp's `-db`, `-o`, `-lf_type`, the pairing
manifest and the model folder.

**What a run writes**, with `-o out.blib`:

```
out.blib                the result: what the search detected, with its sidecars, as for any search
out.predicted.blib      the predicted library the result was searched with (-l takes it next time)
out.model.zip           with --training-input: the tuned model (--model takes it next time)
out.predicted/          the library build's working files (with --entrapment, pairing.tsv for FDRBench)
  initial.blib          with --training-input: the library the tuning runs were searched with
  training/             the tuning runs' search: its artifacts, <stem>.training.parquet, training.blib
  model/                Carafe's model folder the tuning wrote, the library's input
```

The two `.blib` files differ in what their spectra are: `out.blib` holds the precursors the
search detected, with their observed retention times, for Skyline; `out.predicted.blib` holds a
predicted spectrum for every precursor the search looked for, which is what makes it a library to
search again.

They land beside `out.blib`, or under `--output-dir` when it is given, as the run's other
artifacts do. The library carries the prediction's validity stamp and the command line that
made it (`LibMetadata`), and the search keys on the library file as it keys on any given
library. So a re-run of the same command line predicts nothing and searches nothing; a changed
digest or prediction option predicts the library again and the search follows; a changed `-i`
alone searches again against the current library. With tuning, each phase checkpoints the same
way - the training library, the tuning runs' search (its own artifacts, in `out.predicted/training/`;
a tuning run that is also an input is parsed once, from the shared spectra cache), the model and
the library - so a changed tuning option tunes again and predicts again without searching the
tuning runs again, `--entrapment` changes only the final library, and a run that died resumes
at the phase it died in.

**Decoys and pairing.** The digest is made in memory from the FASTA - its targets, their
decoys (reversed, C-terminus preserved, unique against every target, as CarafeSharp's
entrapment FASTA build makes them) and with `--entrapment` the entrapment peptides and theirs -
and the predicted library carries every pair in its `DecoyPairs` table. The search therefore
takes the library's decoys (`--decoys-in-library` is implied) and reads no manifest. Protein
accessions are the proteome's, `db|accession|entry` joined by `;` across a shared peptide's
proteins, with decoys prefixed `decoy_`.

Log lines: the prediction is a task like the four stages, with `[TASK] PredictLibrary:starting`
/ `:skipping (outputs valid)` / `:done (<s>s)` in the machine channel; with tuning,
`PredictTrainingLibrary`, the four stages of the tuning runs' search and `TrainModel` come first.
Each is also a `--task` of its own on a cluster (see "The library build on a cluster").

### Scoring & Tolerance

| Option | Value | Default | Effect |
|--------|-------|---------|--------|
| `--resolution` | `hram \| unit` | `hram` | How spectra are scored. `hram` (Orbitrap, Astral, TOF): fine bins, the MS1 precursor features, 10 ppm fragment and precursor tolerance. `unit` (ion traps such as Stellar): coarse bins, no MS1 features, 0.5 m/z fragment and 1.0 m/z precursor tolerance. Also selects the default pick model. Not detected from the data. See [03-spectral-scoring.md](03-spectral-scoring.md), [06-peak-detection.md](06-peak-detection.md). |
| `--fragment-tolerance` | `<number>` | `10` ppm; `0.5` m/z at `unit` | Fragment m/z tolerance, in the `--fragment-unit` unit. |
| `--fragment-unit` | `ppm \| mz` | `ppm`; `mz` at `unit` | Unit for `--fragment-tolerance`. |
| `--no-prefilter` | — | prefilter on | Disable the coelution signal pre-filter (scores every candidate; ~30% slower). See [06-peak-detection.md](06-peak-detection.md). |
| `--demux` | `off \| auto` | `off` | Demultiplex overlapping-window (staggered) or MSX DIA with ProteoWizard's demultiplexer while the source is read, into `<stem>.demux.spectra.bin` (see [23-demultiplexing.md](23-demultiplexing.md)). `auto` reads a run that is not multiplexed as acquired. With `off`, a multiplexed run is searched as acquired, with a warning suggesting `--demux auto`. Give every task of an HPC chain the same value. Osprey-only. |

### FDR & Protein Inference

| Option | Value | Default | Effect |
|--------|-------|---------|--------|
| `--run-fdr` | `<threshold>` | `0.01` | Run-level FDR threshold (also the Percolator train/test FDR). See [07-fdr-control.md](07-fdr-control.md). |
| `--experiment-fdr` | `<threshold>` | `0.01` | Experiment-level FDR threshold. |
| `--reconciliation-compaction-fdr` | `<threshold>` | `0.01` | Peptide q-value gate for first-pass compaction; loosen (e.g. `0.05`) to broaden the reconciliation pool. See [10-cross-run-reconciliation.md](10-cross-run-reconciliation.md). |
| `--protein-fdr` | `<threshold>` | off → 0.01 gate | Enable protein-level FDR at this threshold (parsimony always runs regardless). See [08-protein-parsimony.md](08-protein-parsimony.md). |
| `--fdr-level` | `precursor \| peptide \| both` | `precursor` | Which q-value gates the reported output. (`protein` is not a valid value.) |
| `--shared-peptides` | `all \| razor \| unique` | `all` | Shared-peptide handling for protein inference. See [08-protein-parsimony.md](08-protein-parsimony.md). |
| `--fdrbench` | `path/to/file.tsv` | off | Write an FDRBench-compatible input TSV (every reported target with the raw SVM score) for entrapment true-FDR. Level follows `--fdr-level`. See [fractional-entrapment.md](fractional-entrapment.md). |
| `--fdrbench-per-run` | — | off | With `--fdrbench`: one row per (precursor, run) using run-level q-values. |
| `--fdrbench-pass` | `1 \| 2 \| both` | `2` | With `--fdrbench`: which pass to emit (2 = reported second-pass survivors; 1 = full pre-compaction first-pass pool). |

### Decoys

| Option | Value | Effect |
|--------|-------|--------|
| `--decoys-in-library` | — | Trust decoys already in the library instead of generating reverse decoys (hard error if none recognised). See [01-decoy-generation.md](01-decoy-generation.md). |
| `--decoy-pairing-manifest` | `path/to/file.tsv` | FDRBench 5-column pairing manifest, used with `--decoys-in-library`. |

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
| `--threads` | `<integer>` | all cores | Per-file main-search threads (INNER), divided across the files a scoring or re-scoring stage runs concurrently. |

Each per-file stage resolves its own count, once, from its own flag, else `--parallel-files`, else
`OSPREY_MAX_PARALLEL_FILES` (not for caching), else one at a time, and logs a `File parallelism: N (...)` line naming
the argument that decided. First-pass FDR's lanes are separate (from `--threads` and free memory).
None of these counts enters a validity key or changes an output byte; one lane is the plain loop.

### Distributed / HPC

| Option | Value | Effect |
|--------|-------|--------|
| `--task` | `SpectraCache \| PredictTrainingLibrary \| TrainModel \| PredictLibrary \| PerFileScoring \| FirstPassFDR \| PerFileRescoring \| SecondPassFDR \| TrainingExport \| ModelDiagnostics` | Run exactly one task (one node = one task). Omit for the whole pipeline. `SpectraCache` stages the `.spectra.bin` caches and needs no library; `TrainingExport` is `--training-export` with no `--task`: a selector, not a stage, that runs the whole pipeline with the export on, so a completed run writes only its missing exports; `ModelDiagnostics` regenerates only the `--model-diagnostics` report for a completed run. With `--fasta`, `PredictTrainingLibrary`, `TrainModel` and `PredictLibrary` run the library build one phase at a time, and the four stages search the library it predicts (or, with `--training-search`, the tuning runs). EVERY search task takes `-i`/`--input-list` naming the data files; the parquets and sidecars are derived from their stems. See [15-hpc-scoring-split.md](15-hpc-scoring-split.md). |
| `--training-search` | — | With `--fasta` and `--training-input`: the stage `--task` selects is a stage of the tuning runs' search - over the `--training-input` runs, with `out.predicted/initial.blib`, into `out.predicted/training/`, writing the training exports `TrainModel` reads. Without `--task`, the whole of that search on one node - what a single tuning run needs, since `--task FirstPassFDR` refuses one input. |
| `--task-files` | — | With `--task`: print the files the task reads and writes for this command line - one line each, `input` or `output`, the task and the path, tab-separated - and exit without running it. What a node must be given and what it hands on, from the task's own declarations, for a workflow manager or a harness that stages nodes. |

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
  (blib, TSV report, FDRBench input, parquet, JSON, the embedded validity stamps) and every tagged log line.
  A run under any culture writes byte-identical files.
- `@"..."` marks text that is deliberately NOT translated: tagged lines, file headings and keys,
  argument and environment variable names, internal-invariant exceptions, and diagnostics reached
  only through `-d` or an `OSPREY_*` setting.
- `--culture <name>` (internal, not in `--help`, as in Skyline) runs Osprey under a named culture
  instead of the OS one, e.g. `--culture fr-FR` or `--culture ja`. The unit tests take the same
  choice from `OSPREYDEV_TEST_CULTURE` (`Build-Osprey.ps1 -RunTests -Culture ja-JP`); fr-FR and
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
Osprey --task PerFileScoring   -i s1.mzML -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01

# join 1 — one process over ALL runs (pass a sorted list so order is deterministic)
Osprey --task FirstPassFDR     --input-list runs.txt -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
#   writes beside each parquet: <stem>.1st-pass.fdr_scores.bin, <stem>.reconciliation.json

# split 2 — one process per file (parquet + its two sidecars co-located)
Osprey --task PerFileRescoring -i s1.mzML -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
#   writes: <stem>.scores-reconciled.parquet

# join 2 — one process over ALL runs, reading their reconciled parquets (writes out.blib)
Osprey --task SecondPassFDR    --input-list runs.txt -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
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

### The library build on a cluster

The one command (`--fasta`) splits the same way, one node per task, every node given the same
command line and its own `--task`. Each task reads only what the tasks before it wrote, and each
checkpoint is the file it writes, as in one process:

```bash
ARGS="-i s1.mzML -i s2.mzML -i s3.mzML --fasta human.fasta -o out.blib --training-input s2.mzML --resolution unit"
Osprey --task PredictTrainingLibrary $ARGS          # out.predicted/initial.blib
# the tuning runs' search: the four stages with --training-search, per-file stages one node per tuning run
Osprey --task PerFileScoring   $ARGS --training-search   # (--training-input <one run> on each per-file node)
Osprey --task FirstPassFDR     $ARGS --training-search
Osprey --task PerFileRescoring $ARGS --training-search   # writes out.predicted/training/<stem>.training.parquet
Osprey --task SecondPassFDR    $ARGS --training-search
Osprey --task TrainModel       $ARGS                     # out.model.zip, out.predicted/model/
Osprey --task PredictLibrary   $ARGS                     # out.predicted.blib (refused before TrainModel has run)
# the output's search: the four stages against out.predicted.blib, as above
Osprey --task PerFileScoring   $ARGS                     # (-i <one run> on each per-file node)
...
```

With one tuning run (the lab's habit) the tuning runs' search is one node, `Osprey $ARGS --training-search`
with no `--task`: `--task FirstPassFDR` refuses a single input. Without `--training-input` the build is
`PredictLibrary` and the four stages. The library phases
are single-node tasks (a GPU node, when there is one: `--device gpu`); the tuning runs' search and
the output's search share the spectra cache, so a tuning run that is also an input is parsed once
when both searches see the same `--cache-dir` (or `--work-dir`). `--task-files` prints any node's
inputs and outputs.

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
- **`--help` formats.** `Osprey --help html` writes the same reference as HTML;
  `Osprey --help <Section>` prints one group (e.g. `Osprey --help "FDR & Protein Inference"`).
- **Value validation.** Osprey's tokenizer does **not** reject values outside the listed
  set - an unrecognized `--fdr-level` value warns and falls back to the default rather than
  erroring. An unknown ARGUMENT is an error, and that includes the removed `--fdr-method`.
- **Option spelling.** Names are hyphenated (`--run-fdr`). An underscore in a typed name is
  read as a hyphen (`--run_fdr`), a rule that exists for the library-build options Carafe
  users have in scripts with Carafe's spelling; the help lists every option in the standard
  form.

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
