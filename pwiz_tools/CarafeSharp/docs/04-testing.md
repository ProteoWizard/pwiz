# 04. Building and testing

How to build CarafeSharp on the CPU or CUDA, on Windows or Linux, where the test data comes from, and
what each test pass checks. Scripts: `build.ps1`, `build.sh` and `build.bat` in `pwiz_tools/CarafeSharp`.
The data packages are listed in `testdata.json`, which the tests and the packaging script both read.

---

## Building

| Platform | Command | Notes |
|---|---|---|
| Windows | `pwsh -File build.ps1` or `build.bat` | Needs PowerShell 7 and the .NET SDK that `global.json` names. |
| Linux, WSL2 | `./build.sh` | Installs what is missing without root: the SDK into `~/.dotnet` (through `pwiz-sharp/scripts/ensure-dotnet.sh`) and PowerShell as a dotnet global tool. Under WSL2 it adds `/usr/lib/wsl/lib`, where the NVIDIA driver's CUDA libraries live, to the library path. Every argument passes through to `build.ps1`. |

`build.ps1` builds `CarafeSharp.sln` (Release, x64) and runs the tests. Its options:

| Option | Effect |
|---|---|
| `-Configuration Debug` | Debug build. |
| `-Torch cuda` | The CUDA 12.8 libtorch (about 4 GB of native files) instead of the CPU one; runs the `Cuda` test category. |
| `-NoTests`, `-NoBuild` | Build only, or test the existing build. |
| `-TestName <text>` | Run the tests whose name contains the text, whatever their category. |
| `-TestCategory <name>` | Run one category (`Astral`, `Cuda`). |
| `-RequireData` | Fail when any test did not run (see "Test data"). |
| `-Coverage` | Run the tests under dotCover and print each assembly's statement coverage (Windows). |
| `-TeamCity` | TeamCity service messages; imports the test results and coverage. |

The CPU build writes to `bin` and `obj`, the CUDA build to `bin-cuda` and `obj-cuda`, so switching backends
does not recopy libtorch. `ai/scripts/CarafeSharp/Build-CarafeSharp.ps1` goes through `build.ps1` and adds
the CRLF fix and the ReSharper inspection.

The pretrained AlphaPeptDeep models (Carafe 2.2's own, pinned by SHA-256) are committed in
`models/alphapeptdeep-v1` and copied beside the executable, so a fresh clone needs no download. The lookup
order is in `models/alphapeptdeep-v1/README.md`.

## GPUs

CarafeSharp uses TorchSharp 0.106 with libtorch 2.10 built for CUDA 12.8. Its `torch_cuda` library holds
kernels for compute capabilities 7.0, 7.5, 8.0, 8.6, 9.0, 10.0 and 12.0, and no PTX:

| Runs | Does not run |
|---|---|
| Volta (7.0), Turing (7.5), Ampere (8.0, 8.6), Ada (8.9) and Orin (8.7) on the 8.6 kernels, Hopper (9.0), Blackwell (10.0, 12.0) | Maxwell and Pascal (5.x, 6.x); AMD, Intel and Apple GPUs |

- **Driver:** NVIDIA release 570 or later. No CUDA toolkit is needed.
- **Preflight:** `build.ps1 -Torch cuda` reads `nvidia-smi` before the 4 GB download. It stops if the
  first GPU (the one libtorch uses; `CUDA_VISIBLE_DEVICES` picks another) is older than compute 7.0 or the
  driver is older than 570, and says which.
- **Fallback:** at run time `-device gpu` falls back to the CPU, as Carafe does, and logs why.
- **Tested hardware:** a GeForce GTX 1650 (Turing, 7.5, 4 GB) under Windows. `build.sh` and the test passes
  have not yet been verified on Linux or WSL2.

## Test data

The parity tests compare CarafeSharp with Carafe 2.2.0 reference runs, which are too large for the
repository. They come as zips with one top folder each:

| Package | Holds | Extracted | Tests |
|---|---|---|---|
| `carafesharp-testfiles-v1` | Stellar Carafe runs (libraries, predictions, digests, fine-tuned models), 12 stage-1 builds, small library references | 1.5 GB zip, 5.6 GB extracted | the default pass |
| `carafesharp-testfiles-astral-v1` | the Astral Carafe run | 4.8 GB zip, 22 GB extracted | category `Astral` |
| `carafesharp-export-v1` | Osprey's training export of the Stellar run | 67 MB zip, 67 MB extracted | masking parity |

The zips are in the PanoramaWeb perftests folder beside the Osprey test files,
<https://panoramaweb.org/_webdav/MacCoss/software/%40files/perftests/>, and anyone can download them.
`testdata.json` has each one's URL, size and SHA-256. The export package holds the format 2 export that
Osprey (#4708) wrote from the Stellar `_21` .raw; its README records the command.

Extract a zip into `<Downloads>/Perftests/`, where the Skyline and Osprey perf tests keep theirs:
- `<Downloads>` is `SKYLINE_DOWNLOAD_PATH` when it is set, else the user's Downloads folder.
- Or set `CARAFESHARP_TESTDATA` to the folder that holds the package folders.

This works from Visual Studio or ReSharper too, where no script sets variables.

**What the tests do with the data:**
- **Nothing present:** with no package and no variable set, the parity tests are Inconclusive. That is what
  CI sees.
- **Anything present:** once any data is there, a missing file fails, naming its path. So does a package
  folder without `MANIFEST.sha256`, the checksum list that is the zip's last entry; an extraction that
  stopped part way has none. An item a test reads with no data of its own fails too, as does a variable
  that names no path or a missing one.
- **`-RequireData`:** `dotnet test` reports Inconclusive as a pass, so `build.ps1 -RequireData` counts the
  outcomes and fails on any test that did not run.
- **Paths:** Carafe's own files (`.carafe.sig`, `parameter.txt`, logs) are packaged byte for byte and name
  paths on the machine that made them. The tests find each one's copy in the package: the copy that
  matches the longest part of the recorded path, never a file outside the package. Our own stage-1
  `command.txt` files use a `{DATA}/` prefix for the package's top folder.

Each item also has a variable that replaces the package's copies with folders of its own, several
separated by `;` (`:` on Linux):

| Variable | Replaces |
|---|---|
| `CARAFESHARP_CARAFE_REFERENCE` | pretrained-library runs |
| `CARAFESHARP_CARAFE_FINETUNED` | fine-tuned-library runs |
| `CARAFESHARP_LIBRARY_REFERENCES` | small library references |
| `CARAFESHARP_STAGE1_REFERENCE`, `CARAFESHARP_STAGE1_BUILDS` | stage-1 references and builds |
| `CARAFESHARP_OSPREY_TRAINING_EXPORT` | training exports |
| `CARAFESHARP_PRETRAINED_MODELS` | the pretrained archive |

`ai/scripts/CarafeSharp/New-CarafeSharpTestData.ps1` builds the zips from `testdata.json`. A published
package is never republished under the same name, because extraction never overwrites; a change gets a
new version.

## Test passes

| Pass | Command | Tests | Time |
|---|---|---|---|
| CPU, no data | `build.ps1` | 74, of which the 8 parity tests are Inconclusive | about 1 min |
| CPU, with data | `build.ps1 -RequireData` | all 74 | about 10 min |
| Astral | `build.ps1 -TestCategory Astral -RequireData` | 4, on the Astral package | about 12 min |
| CUDA | `build.ps1 -Torch cuda` | the `Cuda` category: pretrained predictions on the GPU against the CPU | not yet timed |
| Regression | `regression.ps1` | the `Regression` category: a fine-tune and library against the golden (see "Regression") | 20-35 min on the CPU |

- **CUDA:** the pass sets `CARAFESHARP_REQUIRE_CUDA=1`, so a GPU test that finds no usable GPU fails
  instead of passing untested. The GPU and CPU predictions agree within 3e-5 in intensity and 5e-5 in
  normalized RT, a few times what was measured.
- **Parity coverage:** the Stellar parity checks compare every precursor. The Astral run is ten times
  larger, so its two whole-set checks compare one hash partition of the precursor keys, a sixteenth, the
  same on both sides. `CARAFESHARP_FULL_PARITY=1` compares them all, at about 13 GB of memory.

## Regression

`regression.ps1` compares a CarafeSharp run with a golden kept in `regression.data/<dataset>`. Only the
isolated leg exists so far: it fine-tunes on the packaged Osprey training export and predicts the final
library, so it depends on CarafeSharp alone.

```
pwsh -File regression.ps1                                   # CPU run, compared with the golden
pwsh -File regression.ps1 -NoBuild -ExtraArgs "-cor 0.7"    # a sensitivity check, which must fail
pwsh -File regression.ps1 -CompareRun <run folder>          # compare an existing run again
pwsh -File regression.ps1 -Export <x.training.parquet>      # fine-tune on another export instead
pwsh -File regression.ps1 -CreateGolden                     # from a clean tree; -Force to replace
```

**The run.** The inputs come from the test data packages, by the rules the tests use: the training
export from `carafesharp-export`, and the library FASTA and pairing manifest from `carafesharp-testfiles`
(`stellar/carafe-osprey-entrapment`). To keep a CPU run short, the library is predicted from a subset of
whole pair groups: the groups whose `peptide_pair_index` is a multiple of 50, each with its target,
entrapment target, decoy and entrapment decoy. The subset manifest is exactly those groups' rows and the
subset FASTA those peptides' records: 4,378 groups and 17,512 records of the 875,484, so the library
has a DecoyPairs table as a full one does. CarafeSharp runs once, `-tf all` with the library arguments
of the workflow's stage 4-5, so fine-tuning and prediction happen in one call. The library is not
rebuilt with `-model_dir`, because `meta.json` carries Carafe's default `lf_frag_mz_max` of 1800 while
the workflow predicts with 1960. A CPU run takes 20 to 35 minutes; each gets its own folder under
`CARAFESHARP_REGRESSION_WORKDIR` (default `TestResults/regression`), holding `out/`, the log, the subset,
`regression-run.json` and the comparator's `regression-report.txt`.

**The comparator** is `RegressionTest` (category `Regression`, left out of the default pass). It reads
the run folder from `CARAFESHARP_REGRESSION_RUN`, which `regression.ps1` sets, and is Inconclusive
without it. The calibrated tolerances decide pass or fail, on every machine and device. They are each at
least three times the largest spread seen across GPU repeats, CPU against GPU and Windows against Linux:

| Check | Pass when |
|---|---|
| Input SHA-256 (export, FASTA, pairing, subset) | identical; otherwise the golden is of other inputs |
| `use_finetuned_for_prediction` | identical |
| Pretrained held-out metrics | within 1e-5 |
| Fine-tuned held-out metrics | COS and PCC within 1.5e-3, SA 6e-3, SPC 5e-3, RT R2 1e-4, RT MAE 5e-4 |
| Library precursor count, DecoyPairs row count | within 1e-4 of the golden's (at least 2) |
| Library peak count | within 1% |
| DecoyPairs pairs | each is a target and the decoy of its pair group (or the entrapment target and entrapment decoy), of one charge |
| DecoyPairs targets | every target whose decoy was written is paired |
| Sampled precursors | one-sided only with at most 3 fragments; the same precursor m/z |
| Sampled spectral cosine | median 0.99925, p5 0.991, p1 0.975 or more |
| Sampled RT difference (minutes) | median 0.03, p95 0.10, p99 0.16 or less |

- **Exact comparisons, reported and never gated:** the SHA-256 of the four training tables and of
  `ms2.safetensors` and `rt.safetensors`, the held-out metrics as numbers, and the library's content
  hash and counts. The report marks each SAME or DIFFERS. A CPU fine-tune is byte-reproducible on one
  machine, so there they are all SAME; on another device or machine they need not be.
- **The library content hash** is over one line per precursor, sorted: modified sequence, charge,
  precursor m/z, RT, and the fragments in m/z order, with fragment m/z, intensity and RT rounded to 1e-6.
  It is read from the .blib with SQLite, never the file's bytes, which change with `createTime` on every
  write.
- **The DecoyPairs checks** place each precursor in its pair group by its I/L-normalized sequence, apart
  from `DecoyPairPlanner`. A peptide whose I/L twin is another member of its group (a shuffle that only
  swaps I and L) has more than one place in the manifest; the planner pairs such twins with themselves
  and skips them, and the check leaves them out and counts them.
- **The sample** is the precursors whose FNV-1a key hash falls in one tenth, stored with their spectra
  as `library_sample.tsv.gz` beside `golden.json`.

**`-CreateGolden`** refuses a working tree with changes, a GPU request that fell back to the CPU, a
fine-tuned MS2 model that does not beat the pretrained one on COS, PCC, SA and SPC or is not used for
prediction, and a DecoyPairs table that leaves out a target whose decoy was written. With a golden
already there it compares the run with it, lists the `golden.json` values that change, and replaces it
only with `-Force`. The golden records its commit, device, processor, OS, libtorch thread count and the
inputs' SHA-256.

**The Stellar golden** is a CPU run (Intel i9-9900K, Windows, 8 libtorch threads, 12 minutes): 19,344
precursors, 334,744 peaks and 19,340 DecoyPairs rows (9,670 pairs, 4,833 of them entrapment pairs;
all 9,666 targets paired, and 12 I/L twins left out of the check), with a 1,923-precursor sample of
420 KB. It was made from `carafesharp-export-v1`, the format 2 export that Osprey (#4708) wrote from
the .raw. The golden it replaced was made from the June export, which an earlier Osprey wrote from
mzML. Against it, every fine-tuned metric and library check was within tolerance (sampled cosine
median 0.99963, RT difference median 0.016 min); only the export's hash and the pretrained metrics
differed, as they must with another export, because the held-out set comes from it.

**Another export: `-Export`.** The run fine-tunes on the given file instead of the packaged export,
for example one Osprey wrote from the .raw on another platform. The export's SHA-256 is then reported
as information, and the calibrated tolerances decide. Osprey on Linux does not write a byte-identical
export. On the Stellar `_21` .raw its rows are the same 22,761 precursors in the same order, and the
evidence CarafeSharp's masking reads is identical. Five columns differ in the last digit: `score`
(17,998 rows), `mp_cosine`, `mp_overall`, `mp_residual_mad`, and the per-ion `polish_pos_resid_max`
(2 rows). That is consistent with the platforms' math libraries rounding differently; it was not traced
further. The footer's `library_hash` and `search_hash` also differ, because they cover each input
file's mtime.

**On Linux, from the .raw (2026-09-29).** Under WSL2 (Ubuntu 22.04, 10 CPUs), Osprey #4708 built with
the Thermo reader searched the Stellar `_21` .raw in 332 s, with the command that wrote the packaged
export; its export differs from the packaged one only as described above. CarafeSharp's regression then
ran on Linux's CPU twice, once on that export (`-Export`) and once on the packaged one, and both passed
against the Windows golden (fine-tuned COS +1.4e-4, sampled spectral cosine median 0.99995):
- the four training tables of both runs have the same content as the golden run's; they differ only
  in line endings, which the training-table hashes ignore;
- the two Linux runs' models and held-out metrics are byte-identical to each other, so the export's
  last-digit differences do not reach the fine-tune;
- the models differ from the Windows golden's as a CPU fine-tune on Linux and on Windows does.

On the GPU under WSL2 (the Linux libtorch CUDA 12.8 build, GTX 1650, driver 591.86), `build.sh -Torch
cuda` passes the `Cuda` test, and `regression.ps1 -Torch cuda` passes against the CPU golden: fine-tuned
COS -2.8e-5, sampled spectral cosine median 0.99981, fine-tune and library in 3.6 minutes.

**Sensitivity checks.** Each run changes one setting and must fail the golden's tolerances:

| Setting | Gated checks that fail | Exact comparisons (information) |
|---|---|---|
| `-cor 0.7` | the 8 MS2 held-out metrics (fine-tuned COS -6.1e-3 against a tolerance of 1.5e-3); library peaks +2.28% | 3 training tables, `ms2.safetensors` and the library content differ; the RT model and metrics are SAME |
| `-lf_top_n_frag 19` | library peaks -2.76% (tolerance 1%), the only gated check that fails | the library content differs; the models, metrics and training tables are SAME |

`-lf_top_n_frag 19` is caught by the peak count alone: every fragment it drops is a precursor's
weakest, so the sampled cosine stays inside its bounds (median 1.000000, p1 0.99598), and so do the
DecoyPairs checks. Its margin is the peak change against the 1% tolerance, 2.8 times over.

## Coverage

Statement coverage from `build.ps1 -Coverage`, with no test data (what CI sees; the pretrained archive
is bundled, so its tests run) and with the data (`-RequireData`). The regression runs CarafeSharp as a
separate process, so it is in neither.

| Assembly | No data | With data |
|---|---|---|
| CarafeSharp | 97.7% | 98.3% |
| CarafeSharp.Core | 94.2% | 94.2% |
| CarafeSharp.IO | 98.8% | 98.8% |
| CarafeSharp.Models | 96.8% | 97.5% |
| CarafeSharp.Proteome | 98.8% | 99.0% |
| CarafeSharp.Training | 99.6% | 99.6% |

Most of what is left is argument checks, `ToString` and the CUDA path, which a CPU run does not reach.

The no-data unit tests work on synthetic inputs built in the test:
- the masking rules at, above and below each threshold;
- a training export written as parquet;
- the fine-tune loop on random models: the loss falls, a checkpoint round-trips, and the seed is honored;
- a training run that predicts the final library, from the model it wrote and the training run's settings;
- blib annotations and DecoyPairs;
- command-line errors, and outputs left behind by a failed run;
- entrapment from a foreign proteome, and pairing reconciliation.

dotCover 2023.3.3 is pinned in `.config/dotnet-tools.json`, the version Osprey uses.

## Reproducibility

- **CPU fine-tuning:** two identical CPU fine-tunes of one training export give byte-identical models,
  metrics and training tables, at one thread count.
- **GPU fine-tuning:** not deterministic. Repeat fine-tunes differ by about 5e-4 in held-out COS, and the
  libraries built from them by a median spectral cosine of 0.9997.
- **Before the fine-tune:** digests, precursor sets and fragment m/z are identical across CPU and GPU,
  and between Windows and Linux on the CPU. Osprey's export and the training tables are identical whenever
  the initial library's content is. Predicted intensities differ between devices by at most about 1e-5.
