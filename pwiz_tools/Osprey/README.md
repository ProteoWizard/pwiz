# Osprey

A C# port of Mike MacCoss's [Osprey](https://github.com/maccoss/osprey)
(Rust) DIA peptide-centric search tool, built under
`pwiz_tools/Osprey/` so it can be integrated into Skyline and reuse
ProteoWizard's existing infrastructure for mzML reading, BiblioSpecLite
(blib) I/O, and the Shared chemistry libraries.

**Status**: Osprey is now the path forward for Osprey. It is
end-to-end cross-implementation **bit-identical** to the Rust reference
from Stage 1 through Stage 7 + `.blib` output on the Stellar
(`--resolution unit`) and Astral (`--resolution hram`) 3-file datasets,
and it now **meets or beats** the Rust reference on wall-clock. The Rust
implementation is being retired to a parity oracle; Osprey ships as a
standalone .NET 8 executable (Windows + Linux) and will move onto the
Skyline release/versioning scheme.

## Why a C# implementation?

Osprey is a well-documented, test-backed DIA search pipeline that does
fragment XIC co-elution scoring with rigorous FDR control and produces
blib output directly consumable by Skyline. A C# implementation lets the
Skyline team:

- run and maintain Osprey's algorithms without a Rust toolchain;
- progressively replace Osprey's bundled chemistry / mzML paths with
  Skyline's own validated implementations (`Shared/CommonUtil/Chemistry`,
  `ProteoWizardWrapper`, `BiblioSpec`) — done carefully so it keeps
  running on .NET 8 and Linux;
- iterate on scoring against a known reference with a
  cross-implementation bisection methodology that catches port errors
  before they reach production.

The port strategy was **bit-identical where possible**: the C#
implementation must produce the same intermediate values as Rust on the
same input, so any divergence is genuinely a bug (in the port or the
reference), not a "similar but different" interpretation. That bar has
now been met end-to-end on the reference datasets; the cross-impl harness
remains in place as a standing regression oracle until the Rust reference
is fully retired.

## Relationship to the Rust reference

| Rust crate               | C# project                       | Purpose                                    |
|--------------------------|----------------------------------|--------------------------------------------|
| `osprey-core`            | `Osprey.Core`               | Shared types, config, enums                |
| `osprey-ml`              | `Osprey.ML`                 | LDA, SVM, matrix, q-values, PEP, PRNG      |
| `osprey-io`              | `Osprey.IO`                 | DIA-NN TSV, blib, parquet readers/writers  |
| `osprey-chromatography`  | `Osprey.Chromatography`     | CWT peak detection, LOESS, RT/mass calib   |
| `osprey-scoring`         | `Osprey.Scoring`            | Spectral scoring, calibration LDA, batch   |
| `osprey-fdr`             | `Osprey.FDR`                | Percolator SVM, protein FDR                |
| `osprey` (bin + pipeline)| `Osprey` (CLI), `Osprey.Tasks` (pipeline) | End-to-end orchestration, CLI, HPC tasks |

The upstream Rust reference is Mike MacCoss's Osprey project at
`github.com/maccoss/osprey`, kept in sync for cross-implementation parity
validation only.

## Pipeline overview

The DIA search runs top to bottom through Stages 1-7 plus `.blib` output.
**Every stage is ported and cross-impl validated** on the reference
datasets (Stages 1-4 PIN features ULP-identical on Stellar; on Astral 19
of 21 features are ULP and xcorr / sg_weighted_xcorr drift at ~1e-7 from
the intrinsic f32 HRAM preprocessed-bin cache; Stages 5-6 byte-equal at
every dump; Stage 7 protein FDR matches at 1e-9 and `.blib` matches at the
SQL row + column level).

For the authoritative, richly annotated picture — per-stage port status,
the HPC task boundaries, the per-task input/output files, and end-to-end
performance tables — open **[`Osprey-workflow.html`](Osprey-workflow.html)**
in a browser. It is a self-contained inline-SVG page (also usable as a
publication-figure starting point). GitHub serves `.html` as source, so to
render it without cloning, use a proxy such as
`https://raw.githack.com/ProteoWizard/pwiz/master/pwiz_tools/Osprey/Osprey-workflow.html`.

```
  Stage                                          Shape
  ---------------------------------------------- ----------------------
  INPUTS  -  spectral library (TSV / blib) + DIA mzML file(s)

  STAGE 1  Library preparation                   per file
  STAGE 2  mzML processing                       per file
  STAGE 3  Calibration (recalibration)           per file
  STAGE 4  Main first-pass search (21 features)  per file
       --------------------------------------------------- first join
  STAGE 5  First-pass FDR + reconciliation plan  join (all files)
       --------------------------------------------------- fan back out
  STAGE 6  Per-file rescore + gap-fill           per file
       --------------------------------------------------- second join
  STAGE 7  2nd-pass FDR + protein FDR + .blib     merge node
```

## HPC distribution: the four `--task` workers

Run with no `--task` for the whole pipeline in one process. For
distributed (HPC / NextFlow) execution the pipeline splits at the join /
fan-out boundaries into four single-task workers — one node = one
`--task`:

| `--task` | shape | reads | writes (next to the input) |
|----------|-------|-------|-----------------------------|
| `PerFileScoring`   | split 1 — per file | mzML (`-i`) + library (`-l`) | `<stem>.scores.parquet`, `<stem>.calibration.json` |
| `FirstPassFDR`     | join 1 — all files | every `<stem>.scores.parquet` (`--input-scores`) | `<stem>.1st-pass.fdr_scores.bin`, `<stem>.reconciliation.json` |
| `PerFileRescoring` | split 2 — per file | `<stem>.scores.parquet` + co-located `.1st-pass.fdr_scores.bin`, `.reconciliation.json` | `<stem>.scores-reconciled.parquet` |
| `SecondPassFDR`    | join 2 — merge node | every `<stem>.scores-reconciled.parquet` (`--input-scores`) | `<output>.blib` (+ `<stem>.2nd-pass.fdr_scores.bin` when protein FDR is on) |

The driver also writes a `<output>.<TaskName>.osprey.task` validity
sidecar next to each output; re-running a task whose outputs already exist
with a matching validity key (search-parameter + library hashes, plus the
reconciliation hash for the rescore/merge tasks) skips the recompute.

### Worked example (Stellar 3-file: `s1.mzML s2.mzML s3.mzML`, library `hela.tsv`)

```bash
# Single-process baseline (no HPC split) — useful as a smoke test:
Osprey -i *.mzML -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01

# Split 1 — PerFileScoring, one process per mzML (run x3, in parallel on the cluster):
Osprey --task PerFileScoring -i s1.mzML -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
#   -> s1.scores.parquet, s1.calibration.json   (next to s1.mzML; -o is ignored here)

# Join 1 — FirstPassFDR, one process over ALL parquets (pass a DIRECTORY so order is fixed):
Osprey --task FirstPassFDR --input-scores ./scores_dir -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
#   -> <stem>.1st-pass.fdr_scores.bin, <stem>.reconciliation.json   (next to each parquet)

# Split 2 — PerFileRescoring, one process per file (parquet + its two sidecars co-located):
Osprey --task PerFileRescoring --input-scores s1.scores.parquet -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
#   -> s1.scores-reconciled.parquet

# Join 2 — SecondPassFDR, one process over ALL reconciled parquets (DIRECTORY again):
Osprey --task SecondPassFDR --input-scores ./reconciled_dir -l hela.tsv -o out.blib --resolution unit --protein-fdr 0.01
#   -> out.blib
```

### Notes that matter for a workflow engine (NextFlow, etc.)

- **Same parameters on every task.** Pass an identical `-l <library>` and
  identical search flags (`--resolution`, `--protein-fdr`, ...) to all
  four tasks. The parquet integrity check (`osprey.search_hash` footer
  metadata) rejects `--input-scores` files whose search/library hash does
  not match the current invocation.
- **`--input-scores` ordering is significant.** A *directory* argument is
  globbed and sorted internally (deterministic). An explicit *file list*
  is consumed in the order given. First-join reconciliation is
  order-sensitive, so for the join tasks pass a directory or a
  deterministically sorted list — a workflow engine's channel order is
  otherwise nondeterministic and would cause run-to-run drift.
- **Outputs land next to inputs; sidecars travel with the parquet.** Each
  task writes its outputs and sidecars beside the input file (not into
  cwd or a separate output dir). `PerFileRescoring` rehydrates from
  `<stem>.1st-pass.fdr_scores.bin` + `<stem>.reconciliation.json` sitting
  next to the parquet, so stage all three into the per-process work
  directory together, and declare the sibling outputs as globs.
- **Let the cluster do the fan-out — not `--parallel-files`.** Run one
  file per `PerFileScoring` / `PerFileRescoring` process.
  `--parallel-files [N]` is the *single-node* multi-file mode (scores
  several files concurrently in one process) and would double-parallelize
  under a scheduler.
- **`--help` is the authoritative flag reference** (`Osprey --help`),
  with a Distributed / HPC group covering `--task` and `--input-scores`.
- **Exit codes**: a failing task returns a non-zero process exit code, so
  a workflow engine can gate on it normally.

## Build

Osprey multi-targets `net472;net8.0`; the `net8.0` target framework
is the one used on Linux. The simplest cross-platform build/run is via the
.NET SDK (8.0+):

```bash
# Framework-dependent (requires the .NET 8 runtime installed on each node):
dotnet publish pwiz_tools/Osprey/Osprey/Osprey.csproj \
  -c Release -f net8.0
#   -> .../bin/Release/net8.0/publish/Osprey.dll   (run as: dotnet Osprey.dll <args>)

# Self-contained single executable (no .NET install needed on the nodes — friendliest for HPC):
dotnet publish pwiz_tools/Osprey/Osprey/Osprey.csproj \
  -c Release -f net8.0 -r linux-x64 --self-contained true
#   -> .../publish/Osprey   (a standalone Linux executable)
```

On Windows, the solution also builds via the Skyline `quickbuild.bat`
path (Osprey lives under `pwiz_tools/` alongside the other tools), or
with MSBuild directly against `Osprey/Osprey.csproj`. The
assembly name is `Osprey`, so the produced binary is `Osprey`
(Linux) / `Osprey.exe` (Windows).

## Redistribution (ZIP / .msi)

`package.ps1` produces the canonical redistributable artifacts on top of the
build above. Each is a self-contained `net8.0` publish (no system .NET needed),
laid out under one versioned top-level folder so multiple versions coexist when
unzipped side by side -- important for pinning an exact Osprey per HPC analysis.

```powershell
# Per-RID ZIPs into dist/ (win-x64 + linux-x64 by default)
pwsh -File ./package.ps1

# Windows ZIP + the per-machine .msi installer
pwsh -File ./package.ps1 -Rid win-x64 -Msi
```

Artifacts (gitignored `dist/`):

```
Osprey-<version>-win-x64.zip      Osprey.exe + runtime DLLs + Documentation/ + README + LICENSE
Osprey-<version>-linux-x64.zip    same layout, Linux self-contained
Osprey-<version>-win-x64.msi      installs to C:\Program Files\Osprey (per-machine), adds PATH
```

The version is the Skyline scheme `YEAR.ORDINAL.BRANCH.DOY` shared with the
build via `version.ps1`. The `.msi` is built with the WiX v5 dotnet tool (see
`Installer/Osprey.wxs`); Authenticode signing is available behind `-Sign`
(off by default -- see the script header for the `OSPREY_SIGN*` env vars). CI
runs this via `tcpackage.bat`. This is the official artifact downstream tools
(e.g. Carafe) should consume rather than building their own Osprey publish.

## Running against test data

The reference test datasets are not in this repo. They live on the
MacCoss lab Panorama server, in the `osprey-testfiles` folder at
<https://panoramaweb.org/MacCoss/maccoss/Shared_w_lab/project-begin.view?pageId=Raw%20Data>.
You only need the mzML files plus the DIA-NN-format spectral library (a
FASTA is also provided to make protein FDR import into Skyline easier).
Stellar (`--resolution unit`) is the small, fast dataset; Astral
(`--resolution hram`) is the large one (~1.5M peptide library).

```bash
# From a directory holding the mzML files and the library:
Osprey -i *.mzML -l hela-filtered-SkylineAI_spectral_library.tsv \
  -o stellar-ospreyoutput.blib --resolution unit --protein-fdr 0.01
```

Osprey accepts the same core CLI arguments as the Rust osprey binary,
so the same invocation works against either tool. The resulting `.blib`
imports directly into Skyline.

For cross-implementation bisection work, the following diagnostic env
vars are shared between the Rust osprey binary and Osprey; setting one causes both
tools to dump coordinated intermediate state that can be diffed directly:

| Env var                          | Effect                                    |
|----------------------------------|-------------------------------------------|
| `OSPREY_DUMP_CAL_SAMPLE=1`       | Dump 2D grid calibration sample           |
| `OSPREY_DUMP_CAL_WINDOWS=1`      | Dump per-entry m/z + RT windows           |
| `OSPREY_DUMP_CAL_MATCH=1`        | Dump pass-1 scoring + feature columns     |
| `OSPREY_DIAG_XIC_ENTRY_ID=<id>`  | Dump per-entry XIC + candidate peaks      |
| `OSPREY_DIAG_XIC_PASS={1,2}`     | Select XIC dump pass                      |
| `OSPREY_DUMP_LDA_SCORES=1`       | Dump LDA discriminant + q-value per entry |

Each `OSPREY_DUMP_*` has a corresponding `OSPREY_*_ONLY=1` that exits the
pipeline after writing the dump. Machine-readable `[COUNT]` / `[TIMING]` /
`[BENCH]` / `[STAGE-WALL]` performance lines are gated behind
`--perf-stats`.

## Project layout

```
pwiz_tools/Osprey/
  Osprey.sln
  Osprey/                  CLI + entry point (Program.cs, OspreyCommandArgs.cs)
  Osprey.Core/             Types, config, enums, process-wide output seam
  Osprey.ML/               LDA, SVM, Matrix, QValueCalculator, PEP, PRNG
  Osprey.IO/               Readers/writers (DIA-NN TSV, blib, parquet, sidecars)
  Osprey.Chromatography/   CWT peaks, LOESS, RT/mass calibration
  Osprey.Scoring/          Spectral scoring, CalibrationScorer
  Osprey.FDR/              Percolator SVM, protein FDR
  Osprey.Tasks/            Pipeline driver + the four HPC --task workers
  Osprey.Diagnostics/      Cross-impl diagnostic dumps (-d / env-var gated)
  Osprey.Test/             Unit + regression tests
  README.md                     (this file)
  Osprey-workflow.html          Visual pipeline diagram (inline SVG)
  regression.ps1                Self-contained correctness gate (golden + resume)
```

## Tests and standing gates

```bash
dotnet test pwiz_tools/Osprey/Osprey.Test/Osprey.Test.csproj -c Release
```

Unit tests cover the ML primitives (LDA, SVM, matrix ops, q-values, PEP,
PRNG), peak detection and calibration, feature extraction, the FDR
controllers, and the I/O readers/writers. Two standing gates guard
algorithm-affecting and structural changes:

- **Correctness** — `regression.ps1` runs the straight-through pipeline
  against a committed C# golden plus a resume leg, both at 1e-9, with no
  Rust checkout required. Also the overnight TeamCity gate.
- **Cross-impl drift** (when a change might diverge from Rust) — the
  `Compare/` bridge scripts re-run Rust and diff Stellar + Astral
  end-to-end. See `pwiz_tools/.../Compare/README.md`.

## Cross-implementation validation methodology

The port followed a strict bisection-from-the-first-selection-step
methodology:

1. Walk downstream from the first stage that involves a selected /
   randomized choice.
2. Instrument both Rust and C# with coordinated diagnostic dumps behind
   env vars, exiting after the dump for fast iteration.
3. Diff the outputs with hard data (sorted TSVs, numerical comparison at
   the f64 rounding-noise floor ~1e-10). A stage is "proven match" only
   when `diff` reports zero real numerical differences at that floor.
4. Commit, then move the bisection anchor to the next stage downstream.
   Never skip ahead — features in a PIN file are meaningless if the two
   tools picked different peaks.

This methodology carried the port to full end-to-end bit-identity on the
reference datasets and remains the regression oracle until the Rust
reference is retired.
