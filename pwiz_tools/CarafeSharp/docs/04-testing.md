# 04. Building and testing

How to build CarafeSharp on the CPU or CUDA, on Windows or Linux, where the test data comes from, and
what each test pass checks. Scripts: `build.ps1`, `build.sh` and `build.bat` in `pwiz_tools/CarafeSharp`.
The data packages are listed in `testdata.json`, which the tests and the packaging script both read.

---

## Building

| Platform | Command | Notes |
|---|---|---|
| Windows | `pwsh -File build.ps1` or `build.bat` | Needs PowerShell 7 and the .NET SDK that `global.json` names. |
| Linux, WSL2 | `./build.sh` | Installs what is missing without root: the SDK into `~/.dotnet` (through `scripts/ensure-dotnet.sh`) and PowerShell as a dotnet global tool. Under WSL2 it adds `/usr/lib/wsl/lib`, where the NVIDIA driver's CUDA libraries live, to the library path. Every argument passes through to `build.ps1`. |

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
- **Fallback:** at run time `--device gpu` falls back to the CPU, as Carafe does, and logs why.
- **Tested hardware:** a GeForce GTX 1650 (Turing, 7.5, 4 GB) under Windows. `build.sh` and the test passes
  have not yet been verified on Linux or WSL2.

## Test data

The parity tests compare CarafeSharp with Carafe 2.2.0 reference runs, which are too large for the
repository. They come as zips with one top folder each:

| Package | Holds | Extracted | Tests |
|---|---|---|---|
| `carafesharp-testfiles-v1` | Stellar Carafe runs (libraries, predictions, digests, fine-tuned models), 12 stage-1 builds, small library references | 1.5 GB zip, 5.6 GB extracted | the default pass |
| `carafesharp-testfiles-astral-v1` | the Astral Carafe run | 4.8 GB zip, 22 GB extracted | category `Astral` |
| `carafesharp-export-v1` | Osprey's training export of the Stellar run, searching Carafe's starting library | 67 MB zip, 67 MB extracted | masking parity |
| `carafesharp-export-v2` | Osprey's training export of the Stellar run, searching CarafeSharp's Chronologer starting library | 60 MB zip, 60 MB extracted | `regression.ps1` |
| `carafesharp-export-astral-v2` | Osprey's training export of the Astral run, searching CarafeSharp's Chronologer starting library | 144 MB zip, 144 MB extracted | `regression.ps1 -Dataset Astral` |

The zips are in the PanoramaWeb perftests folder beside the Osprey test files,
<https://panoramaweb.org/_webdav/MacCoss/software/%40files/perftests/>, and anyone can download them.
`testdata.json` has each one's URL, size and SHA-256. The export packages hold format 2 exports of the
Stellar `_21` and Astral `_55` runs, each a search of a starting library built from its test-files package's
train FASTA and pairing manifest; each README records the commands:
- `carafesharp-export-v1`: Osprey #4708 read the .raw and searched Carafe's own starting library, whose
  AlphaPeptDeep RT plateaus at the end of the gradient: it found no precursor past 19.8 min (#4759). Masking
  parity compares it with Carafe's training on the same search.
- `carafesharp-export-v2` and `carafesharp-export-astral-v2`: Osprey (#4717's, built with the vendor reader) read
  the same .raw files and searched CarafeSharp's starting library, which predicts RT with the pretrained
  Chronologer, as the CarafeSharp workflow does (`Run-CarafeSharpWorkflow.ps1` stages 2 and 3). Its exports from
  the vendor-peak-picked mzML of `osprey-testfiles-mzML-v2` were the same record for record. Stellar: 27,424 precursors
  (v1 22,761), 2,015 of them past 20 min; Astral: 98,142 (v1 86,886), 4,212 past 20 min. The goldens
  fine-tune on them, so they test the end of the gradient.

The packages go in `<Downloads>/Perftests/`, where the Skyline and Osprey perf tests keep theirs:
- `<Downloads>` is `SKYLINE_DOWNLOAD_PATH` when it is set, else the user's Downloads folder.
- Or set `CARAFESHARP_TESTDATA` to the folder that holds the package folders.

**Fetching and verifying:** `build.ps1 -TestData Fetch` (or `ai/scripts/CarafeSharp/Build-CarafeSharp.ps1
-TestData Fetch`) downloads the packages the run reads that are not there yet, before it builds:
- By default those of the default test pass (`testfiles`, `export`), plus `-TestCategory`'s (`Astral` adds
  `astral` and `astral-export`); `-TestDataPackage all`, or a comma-separated list of ids, picks others.
- Each zip is checked against its size and SHA-256 in `testdata.json` and kept beside the packages. A zip
  already there is checked rather than downloaded again, and downloaded again only if it is wrong.
- It is extracted to a temporary folder that is renamed into place when complete, and its files are then
  checked against its `MANIFEST.sha256`. A package folder already there is left alone.

`-TestData Verify` checks every file of the packages against their `MANIFEST.sha256`, a zip kept beside
them against `testdata.json`, and the committed `pretrained_models.zip` and Chronologer files
(`models/chronologer-20220601193755`) against their pins; it reads every file, so the Astral package
takes a few minutes. Either fails on any problem and names it.
`./build.ps1 -NoBuild -NoTests -TestData Fetch` only fetches. You can also download a zip from the page
above and extract it there yourself.

This works from Visual Studio or ReSharper too, where no script sets variables.

**Committed subsets.** Each parity test also runs on a committed subset of its packages, in the default pass
and so on every commit, with no download. The zips are in `CarafeSharp.Test/TestData`, each under 5 MB,
holding package folders laid out as the full packages are, each with its `MANIFEST.sha256`. A test opens one
with `TestData.UseSubset`, which makes it the data root and ignores the variables below. The scripts that cut
them, byte-identically, are in `ai/scripts/CarafeSharp/SubsetData` (its README has the rules).

| Zip | Size | Holds | Tests |
|---|---|---|---|
| `LibraryParitySubset.zip` | 3.4 MB | about 1,000 precursors of each of 5 reference runs (Stellar initial and new, nocut, trypsin, varmod), whole FASTA record groups with their rows, re-indexed fragments and trimmed .blibs | `TestLibraryParitySubset`, `TestPretrainedPredictionsMatchCarafeSubset` |
| `TrainingParitySubset.zip` | 4.0 MB | Carafe's training tables with intensities kept for the held-out spectra only, and 1/20 of the sequences of `carafesharp-export-v1` | `TestFineTuneMatchesCarafeSubset`, `TestMaskingAgreesWithCarafeSubset` |
| `Stage1Inputs.zip` | 1.9 MB | the stage-1 input FASTAs and the expected SHA-256 of each output: 3 signatures and 10 of the 12 builds | `TestSubsetFastaBuildsMatchCarafe` |
| `RegressionSubset.zip` | 4.2 MB | 1/15 of the peptide forms of the Stellar export (`carafesharp-export-v2`) and 1/500 of its pair groups | `TestRegressionSubset` (see "Regression") |

What stays download-only: prediction with Carafe's fine-tuned models (16 MB of weights, which do not
compress), the two builds that need the 14 MB mouse FASTA, the full fine-tune of
`CARAFESHARP_FINETUNE_FULL`, and the full-scale versions of every check.

**What the tests do with the data:**
- **Nothing present:** with no package and no variable set, the full-scale parity tests are Inconclusive,
  and their subset versions run.
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
| `CARAFESHARP_CHRONOLOGER_MODEL` | the Chronologer folder: a folder holding both pinned files (a set variable naming a missing folder fails rather than falling back) |

`ai/scripts/CarafeSharp/New-CarafeSharpTestData.ps1` builds the zips from `testdata.json`. A published
package is never republished under the same name, because extraction never overwrites; a change gets a
new version.

## Test passes

| Pass | Command | Tests | Time (i9-14900) |
|---|---|---|---|
| CPU, no data | `build.ps1` | 105, of which the 8 full-scale parity tests are Inconclusive; the subset tests and `TestRegressionSubset` run | about 2 min |
| CPU, with data | `build.ps1 -RequireData` | all 105 | about 4 min |
| Astral | `build.ps1 -TestCategory Astral -RequireData` | 4, on the Astral package | about 12 min |
| CUDA | `build.ps1 -Torch cuda` | the `Cuda` category: pretrained predictions on the GPU against the CPU, and the subset fine-tuned twice on the GPU | about 2 min |
| Regression | `regression.ps1 [-Dataset Astral]` | the `Regression` category: a fine-tune and library against the golden (see "Regression") | Stellar 12 min, Astral about 36 min on the CPU |

- **CUDA:** the pass sets `CARAFESHARP_REQUIRE_CUDA=1`, so a GPU test that finds no usable GPU fails
  instead of passing untested. The GPU and CPU predictions agree within 3e-5 in intensity and 5e-5 in
  normalized RT, a few times what was measured. `TestRegressionSubsetRepeatsOnGpu` requires two GPU
  fine-tunes of the subset to be the same bytes (see "Reproducibility").

## Continuous integration

CarafeSharp has no TeamCity configuration of its own: Osprey's build it, since it trains the libraries Osprey
searches, and their scripts are in the repository (`pwiz_tools/Osprey`).
- **Every commit** (`tcbuild.bat`, Osprey Windows .NET; `tcbuild.sh`, Osprey Linux .NET): `build.ps1 -TeamCity`
  after Osprey's, with dotCover on Windows. No test data: the committed subsets and `TestRegressionSubset` run, the
  full-scale parity tests are Inconclusive. It adds about 1.5-2 min on TCA1 or Linux, and about 5 min on a slower
  Windows cloud agent. `scripts/misc/vcs_trigger_and_paths_config.py` routes `pwiz_tools/CarafeSharp` (and
  `Shared/Lib/Parquet`) changes to both configs.
- **Nightly** (`tctest.bat`, Osprey Windows .NET Perf/Regression, after Osprey's lanes): `tcregression.ps1` runs
  `build.ps1 -TestData Fetch -RequireData`, `regression.ps1 -Dataset Stellar`, and
  `regression.ps1 -Dataset StellarSubset` twice, whose models and training tables must be the same bytes (a rerun on
  the agent repeats), about 19 minutes on an i9-14900. Its test data goes in `<Downloads>/Perftests/carafesharp`, not the default
  folder, so a per-commit build on the same agent does not find it and run the full-scale tests every commit.
- **Parity coverage:** the Stellar parity checks compare every precursor. The Astral run is ten times
  larger, so its two whole-set checks compare one hash partition of the precursor keys, a sixteenth, the
  same on both sides. `CARAFESHARP_FULL_PARITY=1` compares them all, at about 13 GB of memory.

## Regression

`regression.ps1` fine-tunes on the packaged Osprey training export and predicts the final library, so it
depends on CarafeSharp alone, and compares the run with a golden kept in `regression.data/<dataset>`. It is
the gate on training and prediction. Osprey's export feeding CarafeSharp end to end is checked on Osprey's
side (see "Osprey's export feeding CarafeSharp" below).

`-Dataset StellarSubset` is the isolated leg on the committed `RegressionSubset.zip`, with no test data: 1,831
of the Stellar export's 27,424 precursors (1,672 peptide forms, enough for the KDE alignment) and 438 pair
groups, 1,896 library precursors. It takes about a minute, and `TestRegressionSubset` runs it in-process on
every commit, with the arguments its golden records, against `regression.data/stellar-subset`.

```
pwsh -File regression.ps1                                   # CPU run, compared with the golden
pwsh -File regression.ps1 -NoBuild -ExtraArgs "--cor 0.7"    # a sensitivity check, which must fail
pwsh -File regression.ps1 -CompareRun <run folder>          # compare an existing run again
pwsh -File regression.ps1 -Export <x.training.parquet>      # fine-tune on another export instead
pwsh -File regression.ps1 -CreateGolden                     # from a clean tree; -Force to replace
```

**The run.** The inputs come from the test data packages, by the rules the tests use: the training
export from `carafesharp-export-v2`, and the library FASTA and pairing manifest from `carafesharp-testfiles`
(`stellar/carafe-osprey-entrapment`). To keep a CPU run short, the library is predicted from a subset of
whole pair groups: the groups whose `peptide_pair_index` is a multiple of 50, each with its target,
entrapment target, decoy and entrapment decoy. The subset manifest is exactly those groups' rows and the
subset FASTA those peptides' records: 4,378 groups and 17,512 records of the 875,484, so the library
has a DecoyPairs table as a full one does. `-Dataset Astral` takes the export from
`carafesharp-export-astral-v2` and the FASTA and manifest from `carafesharp-testfiles-astral-v1`
(`astral/Carafe-Osprey-entrapment`): 27,820 groups and 111,322 records. Carafe's digest repeats 39
sequences across records, which is why there are 45 more records than distinct sequences. CarafeSharp runs once, `--tf all` with the library arguments
of the workflow's stage 4-5, so fine-tuning and prediction happen in one call. The library is not
rebuilt with `--model-dir`, because `meta.json` carries Carafe's default `lf_frag_mz_max` of 1800 while
the workflow predicts with 1960. A CPU run takes 20 to 35 minutes; each gets its own folder under
`CARAFESHARP_REGRESSION_WORKDIR` (default `TestResults/regression`), holding `out/`, the log, the subset,
`regression-run.json` and the comparator's `regression-report.txt`.

**The comparator** is `RegressionTest` (category `Regression`, left out of the default pass). It reads
the run folder from `CARAFESHARP_REGRESSION_RUN`, which `regression.ps1` sets, and is Inconclusive
without it. The calibrated tolerances decide pass or fail, on every machine and device. They are each at
least three times the largest spread seen across GPU repeats, CPU against GPU and Windows against Linux
(Stellar and Astral 2026-09-27; the subset 2026-10-13, over 4, 8 and 16 torch threads, two GPU runs and
Linux, its held-out sets being a tenth of Stellar's):

| Check | Pass when (Stellar, Astral) | Pass when (subset) |
|---|---|---|
| Input SHA-256 (export, FASTA, pairing, subset) | identical; otherwise the golden is of other inputs | the same |
| `use_finetuned_for_prediction` | identical | the same |
| Pretrained held-out metrics | MS2 within 1e-5, RT 4e-5 | MS2 1e-5, RT 6e-5 |
| Fine-tuned held-out metrics | COS and PCC within 1.5e-3, SA 6e-3, SPC 5e-3, RT R2 1e-4, RT MAE 5e-4 | COS 2e-3, PCC 4e-3, SA 7e-3, SPC 1.5e-2, RT R2 5e-4, RT MAE 1.5e-3 |
| Library precursor count, DecoyPairs row count | within 1e-4 of the golden's (at least 2) | the same |
| Library peak count | within 1% | within 2% |
| DecoyPairs pairs | each is a target and the decoy of its pair group (or the entrapment target and entrapment decoy), of one charge | the same |
| DecoyPairs targets | every target paired, its decoy written (the library keeps its pairs whole) | the same |
| Sampled precursors | one-sided only with at most 3 fragments; the same precursor m/z | the same |
| Sampled spectral cosine | median 0.99925, p5 0.991, p1 0.975 or more | median 0.998, p5 0.985, p1 0.955 |
| Sampled RT difference (minutes) | median 0.03, p95 0.10, p99 0.16 or less | median 0.09, p95 0.3, p99 0.5 |

- **Exact comparisons** (SHA-256 of the four training tables and of `ms2.safetensors` and `rt.safetensors`,
  the held-out metrics as numbers, and the library's content hash and counts) gate what must repeat:
  - **the training tables** are the same bytes on every system and device ("Reproducibility"), so they must
    match the golden's on every run but one on another export (`-Export`);
  - **the models, metrics and library** repeat only with the same training arithmetic, so they must match on
    the golden's environment (system, device, processor and torch thread count), in a process of its own as
    `regression.ps1` runs CarafeSharp, and are reported as SAME or DIFFERS elsewhere. A rerun on the golden's
    machine that does not reproduce it fails. `TestRegressionSubset` fine-tunes in the test process, after
    other tests, where the MS2 model can sum in another order, so it gates the training tables only.
- **The library content hash** is over one line per precursor, sorted: modified sequence, charge,
  precursor m/z, RT, and the fragments in m/z order, with fragment m/z, intensity and RT rounded to 1e-6.
  It is read from the .blib with SQLite, never the file's bytes, which change with `createTime` on every
  write.
- **The DecoyPairs checks** place each precursor in its pair group by its I/L-normalized sequence, apart
  from `DecoyPairPlanner`. A peptide whose I/L twin is another member of its group (a shuffle that only
  swaps I and L) has more than one place in the manifest; the planner pairs such twins with themselves
  and skips them, and the check leaves them out and counts them. Likewise a target whose partner's
  sequence has another role in its group (Carafe's Astral digest gives some groups' decoy and
  entrapment decoy one sequence) is counted apart: the library holds that precursor once, and the
  planner pairs it once, in its other pair.
- **The sample** is the precursors whose FNV-1a key hash is 0 modulo the golden's modulus, stored with
  their spectra as `library_sample.tsv.gz` beside `golden.json`. The modulus is 10 for each started
  20,000 precursors of the golden's library (10 for Stellar's, 70 for Astral's), so a sample holds about
  2,000; golden.json records it, and a run is sampled at its golden's.

**`-CreateGolden`** refuses a working tree with changes, a GPU request that fell back to the CPU, a
fine-tuned MS2 model that does not beat the pretrained one on COS, PCC, SA and SPC or is not used for
prediction, and a DecoyPairs table that leaves out a target, or a library that lacks a target's decoy. With a golden
already there it compares the run with it, lists the `golden.json` values that change, and replaces it
only with `-Force`. The golden records its commit, device, processor, OS, libtorch thread count and the
inputs' SHA-256.

**The Stellar golden** is a CPU run (Intel i9-14900, Windows, 24 libtorch threads, 11 minutes) on
`carafesharp-export-v2`, made on 2026-10-13 (8e5458db95) when the alignment's reference became float64: 19,344
precursors, 335,838 peaks and 19,340 DecoyPairs rows, with a 1,923-precursor sample. Its training aligns the run's
minutes onto Chronologer's hydrophobic index (the KDE ridge on 24,972 peptide forms, 2.29-23.97 min) and fine-tunes
Chronologer on the aligned minutes (07-chronologer.md). Held-out metrics, pretrained to fine-tuned: MS2 COS 0.9665 to
0.9833 on 17,521 spectra; RT R2 0.9977 to 0.9990, median error 0.0063 to 0.0040 of the gradient. Its models are
byte-identical to a run of the commit before (the float64 reference moved the training targets in their last bits
only); the earlier golden (2026-10-03, 37477a43d4) was an i9-13900H's.

**The StellarSubset golden** is a CPU run on the same machine (1.3 minutes), made on 2026-10-13 (ea3997b2e4): 1,896
precursors, 31,690 peaks and 1,896 DecoyPairs rows, with a 200-precursor sample. Held-out metrics, pretrained to
fine-tuned: MS2 COS 0.9701 to 0.9820 on 90 spectra; RT R2 0.9971 to 0.9984 on 152 forms.

**The Astral golden** is a CPU run on the same machine (31 minutes, most of it the fine-tune on 43,216 MS2 spectra
and 86,699 RT peptide forms) on `carafesharp-export-astral-v2`, made on 2026-10-13 (4c2ad69c26): 123,391 precursors,
1,710,187 peaks and 123,382 DecoyPairs rows, with a 1,738-precursor sample at modulus 70. The run's map is the KDE
ridge on 86,699 forms, 2.16-23.80 min. Held-out metrics, pretrained to fine-tuned: MS2 COS 0.9792 to 0.9876; RT R2
0.9974 to 0.9989, median error 0.0064 to 0.0037. The earlier golden (2026-10-03, d781a0ae3d) was an i9-13900H's.

**What the v2 exports changed.** The goldens before them fine-tuned on the v1 exports, whose search of Carafe's
library stopped at 19.8 min, so their libraries extrapolated the end of the gradient. Scored against DIA-NN's
observed RT on each dataset's three runs (median minutes; order is the distance from an isotonic calibration of the
library RT, which a search that calibrates each run sees, and minutes the library's own, which a targeted assay sees):

| Observed RT | Stellar order, v1 / v2 | Stellar minutes, v1 / v2 | Astral order, v1 / v2 | Astral minutes, v1 / v2 |
|---|---|---|---|---|
| all | 0.063 / 0.061 | 0.094 / 0.088 | 0.084 / 0.081 | 0.104 / 0.102 |
| 20-22 min | 0.258 / 0.129 | 0.259 / 0.178 | 0.410 / 0.099 | 0.364 / 0.151 |
| after 22 min | 0.452 / 0.043 | 1.363 / 0.125 | 0.884 / 0.106 | 1.836 / 0.140 |

Earlier goldens: on 2026-09-30 (8dfe512b59) for the acquisition layer (01-model-spec.md), on 2026-10-01
(8362e74827) when the Astral library began writing its pairs whole, and on 2026-10-02 (3c3d40a502, 9f98c03903) when
Chronologer became the default RT model, all on the v1 exports.

**Osprey's export feeding CarafeSharp.** `regression.ps1 -Leg Chained` ran Osprey and then
CarafeSharp.exe on Osprey's committed Stellar subset. It retired when Osprey's one command gained
`--training-input`, which runs the same chain in one process: Osprey's
`SubsetPipelineTest.TestSubsetLibraryBuildTuned` (`pwiz_tools/Osprey/Osprey.Test`) runs it on every
commit, on the three runs of `StellarSubset.zip`, with the checks the leg made:
- an export per tuning run, each with at least 50 precursors and run q-values from the per-run second
  pass;
- the training tables are not empty, and hold no more PSMs than were exported;
- both models are written and every metric is finite;
- `meta.json` records each run, with a precursor window that holds the subset's (594.52 m/z);
- the model the run saved, given to `--model`, predicts the same spectra as the tuned run's library
  (m/z, retention time and fragments exactly, intensities within 1e-5);
- a further tuning of the saved model (`--model` with `--training-input`) names it as its base, by
  SHA-256.

The fine-tune there is cut to an epoch or two, so it judges the chain, not the models; this script's
golden does.

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
- the four training tables of both runs have the same content as the golden run's; they differed only
  in line endings, which the training-table hashes ignore (since 2026-10-13 every system writes `\n`);
- the two Linux runs' models and held-out metrics are byte-identical to each other, so the export's
  last-digit differences do not reach the fine-tune;
- the models differ from the Windows golden's as a CPU fine-tune on Linux and on Windows does.

On the GPU under WSL2 (the Linux libtorch CUDA 12.8 build, GTX 1650, driver 591.86), `build.sh -Torch
cuda` passes the `Cuda` test, and `regression.ps1 -Torch cuda` passes against the CPU golden: fine-tuned
COS -2.8e-5, sampled spectral cosine median 0.99981, fine-tune and library in 3.6 minutes.

**Sensitivity checks.** Each run changes one setting and must fail the golden's tolerances:

| Setting | Gated checks that fail | Exact comparisons (information) |
|---|---|---|
| `--cor 0.7` | the 8 MS2 held-out metrics (fine-tuned COS -6.1e-3 against a tolerance of 1.5e-3); library peaks +2.28% | 3 training tables, `ms2.safetensors` and the library content differ; the RT model and metrics are SAME |
| `--lf-top-n-frag 19` | library peaks -2.76% (tolerance 1%), the only gated check that fails | the library content differs; the models, metrics and training tables are SAME |

`--lf-top-n-frag 19` is caught by the peak count alone: every fragment it drops is a precursor's
weakest, so the sampled cosine stays inside its bounds (median 1.000000, p1 0.99598), and so do the
DecoyPairs checks. Its margin is the peak change against the 1% tolerance, 2.8 times over.

## Coverage

Statement coverage from `build.ps1 -Coverage`, with no test data (what CI sees; the pretrained archive
is bundled, so its tests run) and with the data (`-RequireData`); and, merged with them, `regression.ps1`'s
isolated and (since retired) chained legs, measured by `ai/scripts/CarafeSharp/Measure-CumulativeCoverage.ps1`
(2026-10-12, before the committed subsets).

| Assembly | No data | With data | With the regression |
|---|---|---|---|
| CarafeSharp | 94.3% | 94.9% | 95.8% |
| CarafeSharp.Core | 96.0% | 96.0% | 96.3% |
| CarafeSharp.IO | 98.9% | 98.9% | 98.9% |
| CarafeSharp.Models | 97.4% | 97.7% | 97.7% |
| CarafeSharp.Proteome | 98.5% | 98.6% | 98.8% |
| CarafeSharp.Training | 97.8% | 97.8% | 97.8% |
| All (7,973 statements) | 97.7% | 97.8% | 98.0% |

The regression adds little coverage; its value is the real data.

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

Measured on the subset (2026-10-13; the regression gates it, see "Regression"):

| What | Windows CPU | Linux CPU | GPU (RTX 4070) |
|---|---|---|---|
| Training tables | the same bytes on all three |  |  |
| Models, metrics, library, run to run | identical | identical | identical |
| Models against Windows CPU | — | differ | differ |

- **Training tables, everywhere.** The run alignment maps each run onto the pretrained Chronologer's
  hydrophobic index. Predicted in float32, that index differed in its last bits between the Windows and Linux
  libtorch builds, and by up to 5e-3 on a GPU, whose cuDNN convolutions use TF32 by default; enough on the GPU to
  move the KDE ridge and the RT targets by up to 0.4 s. The alignment's reference therefore predicts on the CPU in
  float64, rounded to float32 (`ChronologerModel.AlignmentReference`), which gives the same values everywhere.
  The tables are written with `\n` line ends on every system. The KDE's .NET `Math.Cos` still differs between
  Windows and Linux in its last bits (2 ulps at most); it has not moved a ridge.
- **CPU fine-tuning** repeats byte for byte on one system at one thread count. Another thread count, or Linux,
  sums in another order: the models differ slightly, within the tolerances.
- **GPU fine-tuning** repeats byte for byte since its dropout is seeded: TorchSharp's `manual_seed` leaves the
  CUDA generator unseeded, so `FineTuneRun` seeds it too (`cuda.manual_seed_all`). Its models differ from the
  CPU's, as its kernels do. `TestRegressionSubsetRepeatsOnGpu` checks it.
- **Before the fine-tune:** digests, precursor sets and fragment m/z are identical across CPU and GPU,
  and between Windows and Linux on the CPU. Predicted intensities differ between devices by at most about 1e-5.
