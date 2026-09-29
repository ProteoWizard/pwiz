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
| CPU, no data | `build.ps1` | 68, of which the 8 parity tests are Inconclusive | about 1 min |
| CPU, with data | `build.ps1 -RequireData` | all 68 | about 10 min |
| Astral | `build.ps1 -TestCategory Astral -RequireData` | 4, on the Astral package | about 12 min |
| CUDA | `build.ps1 -Torch cuda` | the `Cuda` category: pretrained predictions on the GPU against the CPU | not yet timed |

- **CUDA:** the pass sets `CARAFESHARP_REQUIRE_CUDA=1`, so a GPU test that finds no usable GPU fails
  instead of passing untested. The GPU and CPU predictions agree within 3e-5 in intensity and 5e-5 in
  normalized RT, a few times what was measured.
- **Parity coverage:** the Stellar parity checks compare every precursor. The Astral run is ten times
  larger, so its two whole-set checks compare one hash partition of the precursor keys, a sixteenth, the
  same on both sides. `CARAFESHARP_FULL_PARITY=1` compares them all, at about 13 GB of memory.

## Coverage

With no test data (the pretrained archive is bundled, so its tests run), statement coverage from
`build.ps1 -Coverage` is:

| Assembly | Statements |
|---|---|
| CarafeSharp | 94.0% |
| CarafeSharp.Core | 94.2% |
| CarafeSharp.IO | 98.7% |
| CarafeSharp.Models | 96.8% |
| CarafeSharp.Proteome | 98.0% |
| CarafeSharp.Training | 99.6% |

The no-data unit tests work on synthetic inputs built in the test:
- the masking rules at, above and below each threshold;
- a training export written as parquet;
- the fine-tune loop on random models: the loss falls, a checkpoint round-trips, and the seed is honored;
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
