# OspreySharp

A C# port of Mike MacCoss's [Osprey](https://github.com/maccoss/osprey)
(Rust) DIA peptide-centric search tool, built under
`pwiz_tools/OspreySharp/` so it can be integrated into Skyline and reuse
ProteoWizard's existing infrastructure for mzML reading, BiblioSpecLite
(blib) I/O, and the Shared chemistry libraries.

**Status**: active port, under development. Not yet shippable.

## Why port?

Osprey is a well-documented, test-backed DIA search pipeline that does
fragment XIC co-elution scoring with rigorous FDR control and produces
blib output directly consumable by Skyline. Porting the pipeline to C#
lets the Skyline team:

- reuse Osprey's algorithms inside Skyline without a Rust toolchain;
- replace Osprey's custom chemistry and mzML paths with Skyline's own
  validated implementations (`Shared/CommonUtil/Chemistry`,
  `ProteoWizardWrapper`, `BiblioSpec`);
- iterate on scoring improvements against a known reference
  implementation with a cross-implementation bisection methodology that
  catches ports errors before they reach production.

The port strategy is **bit-identical where possible**: the C#
implementation must produce the same intermediate values as Rust on the
same input, so any divergence we observe is genuinely a bug (in the port
or the reference), not a "similar but different" interpretation.

## Relationship to the Rust reference

| Rust crate               | C# project                       | Purpose                                    |
|--------------------------|----------------------------------|--------------------------------------------|
| `osprey-core`            | `OspreySharp.Core`               | Shared types, config, enums                |
| `osprey-ml`              | `OspreySharp.ML`                 | LDA, SVM, matrix, q-values, PEP, PRNG      |
| `osprey-io`              | `OspreySharp.IO`                 | DIA-NN TSV, blib, elib readers/writers     |
| `osprey-chromatography`  | `OspreySharp.Chromatography`     | CWT peak detection, LOESS, RT/mass calib   |
| `osprey-scoring`         | `OspreySharp.Scoring`            | Spectral scoring, calibration LDA, batch   |
| `osprey-fdr`             | `OspreySharp.FDR`                | Percolator SVM, protein FDR                |
| `osprey` (bin + pipeline)| `OspreySharp` (CLI + pipeline)   | End-to-end orchestration, CLI              |

The upstream Rust reference is Mike MacCoss's Osprey project at
`github.com/maccoss/osprey`. OspreySharp development currently tracks a
private fork that carries cross-implementation diagnostics, scratch
instrumentation, and a handful of parity fixes; the decision of whether
any individual change flows back upstream is made per-change.

## Pipeline overview

The full DIA search pipeline runs top to bottom through eight phases.
The phase where we currently have high-fidelity cross-implementation
validation is the **calibration / recalibration** block. Everything
after that is a mix of "ported but never diffed" and "not ported yet".

```
  Stage                                        Port status
  -------------------------------------------- --------------------------

  INPUTS
  +- Spectral library (TSV / blib / elib)
  +- DIA mzML file(s)

  STAGE 1  -  Library preparation
  +- Load + dedup                              done, bit-identical
  +- Decoy generation (collision-safe reverse) done, bit-identical

  STAGE 2  -  mzML processing
  +- Parse + isolation window grouping         done (different reader
                                                than Rust; not bit-exact
                                                by design)

  STAGE 3  -  Calibration (recalibration)        <-- YOU ARE HERE
  +- 2D grid sampling (m/z x RT)               done, bit-identical
  +- Per-entry calibration windows             done, bit-identical
  +- Pass-1 scoring                            done, bit-identical
  |    XIC extraction + CWT peaks + fallback
  |    apex selection + SNR
  |    4 features (correlation, libcosine,
  |    top6, xcorr)
  +- Pass-1 LDA (iterative non-negative CV)    done, bit-identical
  |    3-fold stratified CV by peptide
  |    positive training set selection
  |    consensus averaging + best iteration
  +- Pass-1 S/N quality filter (SNR >= 5.0)    likely matches (pending
  |                                             explicit verification)
  +- Pass-1 LOESS fit (lib_rt -> expected_rt)  expected (upstream now
  |                                             matches; pending broad
  |                                             cross-impl diff)
  +- Pass-2 refinement loop                    partial: verified on
       refined MAD-based tolerance              entry 0 only; broad
       re-score, re-LDA, re-LOESS               cross-impl diff pending

  STAGE 4  -  Main first-pass search             not yet verified
  +- Per-entry XIC + multiple CWT candidates
  +- 21 PIN features per entry

  STAGE 5  -  First-pass FDR                     ported, not yet diffed
  +- Best-per-precursor subsampling
  +- 3-fold stratified CV by peptide
  +- Iterative SVM with non-negative weights
  |    grid search for C, Percolator-style
  |    positive training set selection,
  |    best-iteration tracking
  +- Cross-fold score calibration
  |    (Granholm 2012)
  +- Target-decoy competition
  |    (run-level + experiment-level,
  |     precursor + peptide, max-combined)
  +- FDR compaction (drop non-passing,
       keep paired target + decoy)

  STAGE 6  -  Refinement (post-FDR)              stubs / missing
  +- Multi-charge consensus
  +- Cross-run reconciliation
  |    (boundary overrides from consensus)
  +- Second-pass rescore + Percolator
       (re-extract XICs at locked boundaries,
        re-train SVM on corrected features)

  STAGE 7  -  Protein FDR                        ported, not yet diffed
       parsimony + subset elimination +
       picked-protein FDR (TDC)

  STAGE 8  -  Output                             works, but upstream
       BiblioSpecLite .blib                    divergences mean numbers
       (RefSpectra, RetentionTimes,             still differ from Rust
        Modifications, Proteins,
        Osprey library fragment tables)
```

For a richer visual version, open
[`Osprey-workflow.html`](Osprey-workflow.html) in a browser. The HTML
file is a self-contained inline SVG and can also be used as a starting
point for a publication figure.

## Current port status (summary)

- **Proven bit-identical**: stages 1-3 through pass-1 LDA q-values.
  Confirmed on Stellar file 20 (Mike's test data, HeLa DIA): 192,289
  calibration matches, 11,937/11,937 targets at 1% FDR, 192,288/192,289
  discriminants bit-equal (1 row at 1e-10 ULP), all 192,289 q-values
  bit-equal.
- **Expected to match, pending verification**: pass-1 LOESS fit,
  pass-1 S/N filter, broader pass-2 XIC verification.
- **Ported but never cross-checked end-to-end**: main first-pass
  search, first-pass Percolator SVM, protein FDR.
- **Missing or stubbed**: multi-charge consensus, cross-run
  reconciliation, second-pass rescoring, second-pass Percolator.
- **Performance**: C# is currently ~2.4x Rust wall-clock on the
  reference benchmark (Stellar file 20, pass-1 calibration exit). The
  primary gap is in the per-entry scoring hot loop; we have not yet
  started performance optimization.

## Path conventions

Throughout this document, `<pwiz-root>` refers to a local checkout of the
[`ProteoWizard/pwiz`](https://github.com/ProteoWizard/pwiz) repository
(this repo) and `<ai-root>` refers to a local checkout of the companion
[`ProteoWizard/pwiz-ai`](https://github.com/ProteoWizard/pwiz-ai)
repository, which holds the session TODOs, debugging methodology, and
other AI-assisted development notes referenced below.

## Project layout

```
pwiz_tools/OspreySharp/
  OspreySharp.sln
  OspreySharp/                  CLI + end-to-end pipeline (AnalysisPipeline.cs)
  OspreySharp.Core/             Types, config, enums
  OspreySharp.ML/               LDA, SVM, Matrix, QValueCalculator, PEP, PRNG
  OspreySharp.IO/               Readers/writers (DIA-NN TSV, blib, elib)
  OspreySharp.Chromatography/   CWT peaks, LOESS, RT/mass calibration
  OspreySharp.Scoring/          Spectral scoring, CalibrationScorer
  OspreySharp.FDR/              Percolator SVM, protein FDR
  OspreySharp.Test/             Unit tests (167 at last count)
  README.md                     (this file)
  Osprey-workflow.html          Visual pipeline diagram (inline SVG)
```

## Build

Release build from a shell that has MSBuild on PATH (e.g. a VS 2022
Developer Prompt, or via direct invocation):

```bash
"$MSBUILD_EXE" \
  "<pwiz-root>/pwiz_tools/OspreySharp/OspreySharp/OspreySharp.csproj" \
  //p:Configuration=Release //p:Platform=x64 //nologo //verbosity:minimal
```

The solution also builds via the Skyline `quickbuild.bat` path, since
OspreySharp lives under `pwiz_tools/` alongside the other tools.

## Running against test data

The project's reference test datasets are not in this repo. They live
on the MacCoss lab Panorama server, in the `osprey-testfiles` folder at
<https://panoramaweb.org/MacCoss/maccoss/Shared_w_lab/project-begin.view?pageId=Raw%20Data>.

Mike MacCoss's instructions for running the Rust Osprey reference
against that data:

> You only need the mzML files. There is also a spectral library
> (in the DIANN tsv format). For the Astral dataset there are ~1.5
> million peptides. With the mzML files and library in a directory
> you can search it using
>
>     osprey -i *.mzML -l hela-filtered-SkylineAI_spectral_library.tsv \
>            -o stellar-ospreyoutput.blib --resolution unit --protein-fdr 0.01
>
> It will generate the blib that can be imported into Skyline. I also
> added the fasta that makes that easier too.
>
> The Stellar data goes way faster. You can search that with
>
>     osprey -i *.mzML -l hela-filtered-SkylineAI_spectral_library.tsv \
>            -o stellar-ospreyoutput.blib --resolution unit --protein-fdr 0.01

OspreySharp accepts the same CLI arguments as the Rust Osprey binary,
so the equivalent invocation of the C# port from a directory
containing the mzML files and library is:

```bash
pwiz.OspreySharp.exe -i *.mzML \
  -l hela-filtered-SkylineAI_spectral_library.tsv \
  -o stellar-ospreyoutput.blib --resolution unit --protein-fdr 0.01
```

For cross-implementation bisection work, the following diagnostic env
vars are shared between Osprey and OspreySharp. Setting one causes both
tools to dump coordinated intermediate state that can be diffed
directly:

| Env var                          | Effect                                   |
|----------------------------------|------------------------------------------|
| `OSPREY_DUMP_CAL_SAMPLE=1`       | Dump 2D grid calibration sample          |
| `OSPREY_DUMP_CAL_WINDOWS=1`      | Dump per-entry m/z + RT windows          |
| `OSPREY_DUMP_CAL_MATCH=1`        | Dump pass-1 scoring + 6 feature columns  |
| `OSPREY_DIAG_XIC_ENTRY_ID=<id>`  | Dump per-entry XIC + candidate peaks     |
| `OSPREY_DIAG_XIC_PASS={1,2}`     | Select XIC dump pass                     |
| `OSPREY_DUMP_LDA_SCORES=1`       | Dump LDA discriminant + q-value per entry |

Each `OSPREY_DUMP_*` has a corresponding `OSPREY_*_ONLY=1` that exits
the pipeline after writing the dump.

## Tests

```bash
"$VSTEST_EXE" \
  "<pwiz-root>/pwiz_tools/OspreySharp/OspreySharp.Test/bin/x64/Release/pwiz.OspreySharp.Test.dll"
```

Unit tests cover the ML primitives (LDA, SVM, matrix ops, q-values,
PEP, PRNG), the peak detection and calibration algorithms, the feature
extraction, the FDR controllers, and the I/O readers/writers.

**Known gap (tracked in the project TODO):** the existing unit tests
all passed before Session 5's end-to-end Stellar validation began, and
Sessions 5 through 8 uncovered several serious port bugs that the unit
tests did not catch (e.g. XCorr windowing normalization, apex selection
tie-break direction, missing iterative LDA refinement). We owe a set of
targeted regression tests that would have failed against the pre-fix
implementations of each bug class.

## Cross-implementation validation methodology

The port follows a strict bisection-from-the-first-selection-step
methodology (documented in `<ai-root>/docs/debugging-principles.md`):

1. Walk downstream from the first stage that involves a selected /
   randomized choice.
2. Instrument both Rust and C# with coordinated diagnostic dumps
   behind env vars, exiting after the dump for fast iteration.
3. Diff the outputs with hard data (sorted TSVs, numerical comparison,
   F10 precision to avoid banker's-vs-half-up rounding artifacts). A
   stage is "proven match" only when `diff` reports zero real
   numerical differences at the f64 rounding noise floor (~1e-10).
4. Commit, then move the bisection anchor to the next stage
   downstream. Never skip ahead  -  features in a PIN file are
   meaningless if the two tools picked different peaks.

Session-by-session progress is logged in
`<ai-root>/todos/active/TODO-20260409_osprey_sharp.md` (Phase 2 current)
and `<ai-root>/todos/active/TODO-20260409_osprey_sharp-phase1.md`
(Sessions 1-7 archive).
