# 19. Testing & Standing Gates (C#)

> Pipeline stage: Cross-cutting. C# port of Rust docs/11-testing.md. Corresponds to Rust osprey testing guide.

This document describes how the C# Osprey port is tested. The Rust project put
every test inline in `#[cfg(test)] mod tests` blocks in the source files and
had no separate integration project (Rust 11-testing.md, "All tests are unit
tests"). The C# port keeps that same unit-test-heavy philosophy but reorganizes
it into a **separate MSTest project** (`Osprey.Test`) and adds two **standing
CI gates that have no Rust equivalent**: an end-to-end golden/resume/HPC
regression (`regression.ps1`) and an A/B performance gate (`Test-PerfGate.ps1`).
A Skyline engineer reading this should come away knowing where the tests live,
what actually enforces cross-impl parity, and how the two overnight gates work.

---

## 1. Test architecture: one MSTest project, not inline modules

The C# tests live in a single project, `Osprey.Test`
(`Osprey.Test/Osprey.Test.csproj:1`), which references all seven production
projects (`Osprey.Test.csproj:43-51`: Core, ML, IO, Chromatography, Scoring,
FDR, and the Osprey CLI). This is the structural inverse of Rust, where each
crate carried its own `#[cfg(test)]` tests compiled into that crate.

- **Framework**: MSTest v3.6.4 (`MSTest.TestAdapter` + `MSTest.TestFramework`,
  `Osprey.Test.csproj:12-13`) on `Microsoft.NET.Test.Sdk` 17.12.0
  (`Osprey.Test.csproj:11`). Tests are annotated with `[TestClass]` /
  `[TestMethod]`, the Skyline-standard MSTest attributes.
- **Multi-target**: the test project builds for both `net472` and `net8.0`. The
  `net472` leg needs extra native/managed wiring the SDK gives `net8.0` for
  free: an explicit `IronCompress` native `nironcompress.dll` copy for Parquet
  Zstd (`Osprey.Test.csproj:17-36`) and an explicit `System.Memory` reference
  (`Osprey.Test.csproj:38-41`). The production regression build runs the
  `net8.0` binary (`regression.ps1:123`).
- **Scale**: 41 `[TestClass]` types and **492 `[TestMethod]` tests** across the
  `Osprey.Test/*.cs` files (measured). Rust 11-testing.md tabulates "~302
  tests." The larger C# count reflects extra tests written specifically to lock
  down cross-impl parity (Parquet/sidecar byte layout, codec round-trips, CLI
  argument plumbing) that Rust did not need to guard against a second
  implementation.
- **Assertions**: tests use plain MSTest `Assert` / `CollectionAssert` /
  `StringAssert` (measured: 1,314 `Assert.AreEqual`, 323 `Assert.IsTrue`, 41
  `CollectionAssert.AreEqual`, etc.). There is **no `AssertEx` helper** and no
  shared abstract base test class in `Osprey.Test` — unlike Skyline's `TestUtil`
  `AssertEx`, this project does not reference Skyline test infrastructure.

The largest test files by method count are `IOTest.cs` (64), `ProgramTests.cs`
(62), `ReconciliationTest.cs` (46), `FdrTest.cs` (45), `MLTest.cs` (42), and
`ScoringTest.cs` (40) — roughly mirroring the Rust per-crate distribution
(chromatography/scoring/fdr/io heaviest).

## 2. Tests by project (maps to Rust "Tests by Crate")

Each production project has a corresponding `*Test.cs` (some split across
several), porting the Rust crate's inline tests. The correspondence is direct:

| C# project | C# test file(s) | Rust crate (Rust 11-testing.md) |
|---|---|---|
| `Osprey.Core` | `CoreTypesTest.cs` (24), `IsotopeDistributionTest.cs` (11), `OspreyConfigTest.cs` (2) | `osprey-core` (28) |
| `Osprey.IO` | `IOTest.cs` (64), `DiannDecoyColumnTest.cs`, `CwtCandidateCodecTest.cs` (9), `CwtCandidateLoaderTest.cs` | `osprey-io` (26) |
| `Osprey.Chromatography` | `ChromatographyTest.cs` (13), `CalibrationTest.cs` (20), `PeakDetectorTest.cs` (9) | `osprey-chromatography` (66) |
| `Osprey.Scoring` | `ScoringTest.cs` (40), `OspreyFeatureCalculatorsTest.cs` (9), `FragmentSelectionTest.cs` (11), `IsotopeDistributionTest.cs` | `osprey-scoring` (70) |
| `Osprey.FDR` | `FdrTest.cs` (45), `FdrControllerTest.cs`, `ReconciliationTest.cs` (46), `MultiChargeConsensusTest.cs`, `Pass2FdrSidecarTest.cs`, `PercolatorEntryBuilderTest.cs`, `ReconciledParquetWriterTest.cs`, `GapFillTargetIdentifierTest.cs`, `MedianPolishMetricsTest.cs` | `osprey-fdr` (41) |
| `Osprey.ML` | `MLTest.cs` (42) | `osprey-ml` (30) |
| `Osprey` (CLI) + `Osprey.Tasks` | `ProgramTests.cs` (62), `OspreyCommandArgsTests.cs` (6), `PipelineMembershipTest.cs`, `PerFileResumeDriverTest.cs`, `FileParallelismResolverTests.cs`, `MultiProgressReporterTest.cs`, `ArtifactPathsTest.cs` | `osprey` (main, 26–41) |

The header comments make the porting explicit: `FdrTest.cs:25` "Ported from Rust
test suite in osprey-fdr"; `ScoringTest.cs:36` "Tests for Osprey.Scoring, ported
from osprey-scoring Rust tests"; `IOTest.cs:42` "Ported from osprey-io Rust
tests." Like Rust, these are pure unit tests over synthetically constructed
inputs (spectra, library entries, feature vectors) with no external data
dependency.

Additional C# test files with no direct Rust analog cover port-specific
infrastructure: `ProgramTests.cs` (CLI `--task` argument validation and
`--input-scores` directory expansion — see 15-hpc-scoring-split.md),
`ByproductContextTest.cs`, `DiagnosticsTest.cs`, `ModelDiagnosticsDataTest.cs`
(the `--model-diagnostics` HTML report), `FileSaverTest.cs` (the safe
copy-and-verify NAS-write pattern), `DecoyPairingManifestTest.cs` and
`EntrapmentPairingTest.cs` (decoy/entrapment pairing — see 01-decoy-generation.md).

## 3. Parity-locked tests and the "translation-proof" register

Because the C# code must be bit-identical to Rust (see 16-determinism.md), a
class of C# tests goes beyond "does the answer look right" to "does this match
Rust exactly." Three techniques appear:

1. **Hand-computed reference values ported from the Rust test.** e.g.
   `ScoringTest.cs:972` "Full XCorr pipeline verification: hand-computed
   reference value"; the comment at `ScoringTest.cs:1073` notes "The f64 result
   is what both C# and Rust (after the flip) should use." Comparisons are done
   on the exact bit pattern where it matters: `IOTest.cs:2102-2115` compares
   `BitConverter.DoubleToInt64Bits(...)` of every numeric field rather than an
   approximate `AreEqual` with a tolerance.

2. **Behavioral notes that encode a Rust semantic.** e.g. `ScoringTest.cs:613`
   uses `>=` "to match Rust's `Iterator::max_by` which returns the [last]"
   element on ties; `ScoringTest.cs:839` and the `CodeInspectionTest`
   unstable-sort guard (section 4) both exist because .NET introsort reorders
   ties differently than Rust's stable `slice::sort_by`.

3. **Cross-impl byte-parity hooks.** Certain tests write a real on-disk artifact
   (an FDR-score sidecar, a reconciliation sidecar) using hardcoded inputs that
   are identical to the Rust sibling test's inputs, and — when an environment
   variable points them at an output path — copy that artifact out so a harness
   can byte-compare the C# and Rust outputs. The hook for the FDR-score sidecar
   is `OspreyEnvironment.CrossImplFdrSidecarOut`
   (`Osprey.Core/OspreyEnvironment.cs:104`, env var
   `OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT`), consumed at `IOTest.cs:2082-2083`; the
   reconciliation sidecar hook is `CrossImplReconciliationOut`
   (`OspreyEnvironment.cs:112`, env var `OSPREY_CROSS_IMPL_RECONCILIATION_OUT`).
   `IOTest.cs:2064-2065` states the values "match Rust's
   `fdr_scores_sidecar_v3_round_trip` exactly so the ... byte-parity gate
   compares identical inputs on both sides." `ScoringTest.cs:1283-1286` similarly
   requires the sparse HRAM cache to be "BIT-IDENTICAL to the dense f32 cache."

Note: the phrase "translation-proof" is a description of intent, not a literal
class or attribute name in the code; the mechanism is the ported reference
values plus the byte-parity hooks above.

## 4. Static-analysis gate: `CodeInspectionTest`

`CodeInspectionTest.cs` (`Osprey.Test/CodeInspectionTest.cs:44`) is a C#-only
test class with no Rust counterpart. Modeled on Skyline's
`Test/CodeInspectionTest.cs` (`CodeInspectionTest.cs:32-36`) but scoped to the
Osprey tree, it runs source-text static analysis over the production `*.cs`
files (skipping `Osprey.Test`, `bin`, `obj` — `CodeInspectionTest.cs:53-58`) and
fails the build on cross-impl-parity hazards. Two rules are implemented:

- **`TestNoUnstableSort`** (`CodeInspectionTest.cs:85`): forbids `Array.Sort` and
  `List<T>.Sort` in production code, because .NET introsort is **unstable** and
  reorders equal-keyed elements differently from Rust's stable `slice::sort_by`,
  silently diverging tie-ordering in scoring code (`CodeInspectionTest.cs:60-82`).
  The fix is `OrderBy(...).ThenBy(...)` (LINQ stable sort) or an explicit index
  permutation. A call that is provably tie-safe can be exempted with an inline
  `// Array.Sort OK: <reason>` comment (`CodeInspectionTest.cs:94`). The canonical
  incident cited is `ProteinFdr`'s `winners.Sort(...)` with a HashMap-order
  tiebreak (`CodeInspectionTest.cs:66-70`) — see 08-protein-parsimony.md.
- **`TestReconciliationPairsDecoysByBaseIdNotPrefix`**
  (`CodeInspectionTest.cs:150`): forbids any `"DECOY_"` string literal in the
  `Osprey.FDR/Reconciliation/` code (`CodeInspectionTest.cs:159`), enforcing that
  reconciliation pairs a target with its decoy by `base_id`
  (`entry_id & 0x7FFFFFFF`), never by stripping a modseq prefix — prefix
  stripping misses library-supplied decoys that carry no prefix and biases
  second-pass FDR (`CodeInspectionTest.cs:139-148`). Exemption tag:
  `// DECOY_ pairing OK:`. See 10-cross-run-reconciliation.md and
  01-decoy-generation.md.

Both rules parse only the code portion of each line (a shared `IndexOfLineComment`
string-literal-aware scanner, `CodeInspectionTest.cs:260`) so a comment that
mentions the forbidden pattern does not trip the rule.

## 5. Standing gate 1 — `regression.ps1` (golden + resume + HPC chain at 1e-9)

`Osprey/../regression.ps1` (`regression.ps1:1`) is the overnight end-to-end
correctness gate, wired to the scheduled TeamCity "Osprey Windows .NET
Regression" config via `tctest.bat` (`regression.ps1:99`). It has **no Rust
equivalent** — Rust shipped only inline unit tests. It builds the Release
`net8.0` binary (`regression.ps1:201-206`), acquires real DIA data by
downloading a Panorama zip into `<Downloads>\Perftests`
(`regression.ps1:137,217-220`, TestPerf-style skip-if-present), and runs the
full pipeline on two reference datasets — **Stellar** (unit resolution) and
**Astral** (hram) — with inputs referenced read-only and all derived artifacts
under a per-run timestamped `TestResults/regression-<stamp>` via `--work-dir`
(`regression.ps1:142-145,229-231`). It then asserts a no-copy invariant that the
read-only data dir is byte-for-byte untouched (`regression.ps1:277-293,477-481`).

It runs three complementary correctness legs, all at a **1e-9 tolerance**
(`regression.ps1:114`, `-Tolerance` default):

- **mode 1 — straight-through vs a committed text golden**
  (`regression.ps1:490-500`). Compares the Stage 7 protein-FDR dump (emitted via
  `OSPREY_DUMP_STAGE7_PROTEIN_FDR`, `regression.ps1:244`), a deterministic
  ~500-precursor subset, and a full-set summary against the committed
  `osprey-regression.data/<dataset>/` capture. The golden is small committed text
  (Stellar ~1.1 MB, Astral ~2.4 MB — `Regression/README.md:44-45`), versioned
  with the code so an older tagged commit compares against its own baseline. The
  subset is selected by `MD5(peptideModSeq|precursorCharge) % 120 == 0`
  (`Regression/README.md:56`) — machine-independent. Refresh only on an
  intentional reviewed change with `-CreateGolden` (`regression.ps1:48-50,483-488`).
  Golden tables under `osprey-regression.data/*/tables/` include RefSpectra,
  RetentionTimes, Peak­Boundaries, Osprey{Run,Experiment}Scores,
  OspreyCoefficients, Proteins, PeakDigest (per-spectrum SHA-256), and
  OspreyMetadata.
- **mode 3 — HPC 4-task worker-chain self-consistency**
  (`regression.ps1:502-522`, `Invoke-HpcChain` at `regression.ps1:338`). Runs the
  distributed `--task` pipeline `PerFileScoring → FirstPassFDR → PerFileRescoring
  → SecondPassFDR` (`regression.ps1:27-28`), each phase rehydrating the prior
  phase's on-disk sidecars across a real process boundary (copied inputs, nothing
  held in memory), and asserts the chain's final blib equals the straight-through
  blib at 1e-9. This exercises the cross-process `--task` rehydrate paths that
  mode 2 (in-process resume) does not. See 15-hpc-scoring-split.md for the task
  worker semantics.
- **mode 2 — resume vs straight-through self-consistency**
  (`regression.ps1:524-543`). Copies the straight-through blib aside, invalidates
  the Stage 5 join + blib (`Invoke-ResumeInvalidation`, `regression.ps1:298-304`),
  re-runs the identical command so the rehydrate paths fire, and asserts the
  resume blib equals the cold blib at 1e-9. The build is its own oracle — no
  external baseline. See 14-intermediate-files.md for the cache/sidecar formats
  that resume depends on.

Version pinning: the daily build stamps a changing `YEAR.ORDINAL.BRANCH.DOY`
version, but the golden compares the `osprey_version` metadata cell exactly, so
the harness pins `OSPREY_VERSION_OVERRIDE = '26.1.1.0'` (`regression.ps1:133`,
honored at `Osprey.Core/OspreyVersion.cs:93`) to keep the stamp deterministic.
Any mismatch emits a TeamCity `buildProblem` and a non-zero exit
(`regression.ps1:565-570`). The comparators (`Compare-BlibGolden`,
`Compare-BlibFull`) live in `Regression/BlibGolden.ps1` with data acquisition in
`Regression/RegressionData.ps1` — self-contained, with **no dependency on the
sibling `ai/` checkout** (`regression.ps1:38-41`, `Regression/README.md:4-7`).

Switches: `-Dataset {Stellar|Astral|All}` (`regression.ps1:103`), `-CreateGolden`,
`-SkipResume` (mode-2 off), `-SkipHpcChain` (mode-3 off), `-NoBuild`, `-Threads`
(default 16), `-TeamCity`, `-KeepOutput`, `-Tolerance` (default 1e-9).

## 6. Standing gate 2 — `Test-PerfGate.ps1` (interleaved A/B wall-time)

`ai/scripts/Osprey/Test-PerfGate.ps1` (`Test-PerfGate.ps1:1`) is the performance
companion to gate 1, also with **no Rust equivalent**. Where `regression.ps1`
asserts the OUTPUT is unchanged, this asserts the SPEED is not degraded by a
refactor (`Test-PerfGate.ps1:9-11`). It does a **same-session A/B** of a branch
build against a **pinned baseline worktree** (`C:\proj\pwiz-perfbase`, default
`-BaselineRoot`, `Test-PerfGate.ps1:119`) so machine/thermal/SDK conditions are
shared and cancel (`Test-PerfGate.ps1:16-20`). Key design points:

- **Build both binaries fresh** in the same session unless `-SkipBuild`
  (`Test-PerfGate.ps1:192-200`), and stage both into a common parent to remove
  any PATH asymmetry (`Test-PerfGate.ps1:211-220`).
- **Interleave with alternating order per repeat** (rep 1 baseline-first, rep 2
  branch-first, …) so neither side is systematically the hotter later run
  (`Test-PerfGate.ps1:391-400`), after discarding `-WarmupRuns` warm-ups
  (`Test-PerfGate.ps1:388-390`).
- **Pair each rep's baseline+branch and take the per-rep % delta; the median is
  the headline** (`Get-PairedDelta`, `Test-PerfGate.ps1:424-437`;
  judging at `Test-PerfGate.ps1:459-485`). Walls are parsed from the binary's
  `[STAGE-WALL]` lines emitted under `--perf-stats` (`Test-PerfGate.ps1:320-331`).
- **Hard-fail on TOTAL wall only** — median per-rep delta over `-TotalThresholdPct`
  (default 4%, `Test-PerfGate.ps1:123`) AND every rep agreeing the branch was
  slower (`Test-PerfGate.ps1:470-478`). Heavy stages (`stage1to4`, `stage6`,
  `Test-PerfGate.ps1:160`) report as WARN, never fail — a refactor that moves time
  between stages at equal total is not a regression (`Test-PerfGate.ps1:24-31`).

Rust is not needed for the A/B (the interleaved same-session design already
cancels the environment); it stays the optional change-immune anchor only when a
flagged regression must be confirmed as code vs environment
(`Test-PerfGate.ps1:36-38`). Cross-binary parallelism is controlled via
`OSPREY_MAX_PARALLEL_FILES` rather than `--parallel-files`, because the pinned
baseline predates that argument (`Test-PerfGate.ps1:79-84,296-306`).

## Flags and switches

This stage is driven by test-harness scripts and diagnostic environment
variables rather than product CLI flags. The product `--task`,
`--parallel-files`, `--threads`, `--protein-fdr`, and `--resolution` flags are
exercised *by* the gates but documented in their own stage docs.

`regression.ps1` parameters:

| Switch | Default | Effect |
|---|---|---|
| `-Dataset` | `All` | `Stellar` (unit, fast), `Astral` (hram, large), or `All` |
| `-CreateGolden` | off | Capture/refresh `osprey-regression.data/` instead of comparing (reviewed changes only) |
| `-SkipResume` | off | Skip mode-2 resume self-consistency leg |
| `-SkipHpcChain` | off | Skip mode-3 HPC 4-task worker-chain leg |
| `-NoBuild` | off | Reuse existing Release binary |
| `-Threads` | 16 | `--threads` per run |
| `-TeamCity` | off | Emit TeamCity `progressMessage`/`buildProblem` |
| `-KeepOutput` | off | Retain the run's scratch (default deletes as it goes) |
| `-Tolerance` | `1e-9` | Numeric tolerance for all three legs |

`Test-PerfGate.ps1` parameters:

| Switch | Default | Effect |
|---|---|---|
| `-Dataset` | `Stellar` | `Stellar`, `Astral`, or `Both` |
| `-Repeats` | 3 | Interleaved A/B repeats (median + band) |
| `-WarmupRuns` | 1 | Discarded warm-ups before measured reps |
| `-BaselineRoot` | `C:\proj\pwiz-perfbase` | Pinned baseline worktree |
| `-BranchRoot` | auto (sibling `pwiz`) | Change-under-test worktree |
| `-TotalThresholdPct` | 4.0 | Hard-fail total-wall threshold (every rep must agree) |
| `-StageThresholdPct` | 5.0 | Heavy-stage WARN threshold (never fails) |
| `-SkipBuild` | off | Reuse existing Release/net8.0 binaries |
| `-Threads` | 16 | `--threads` per run |
| `-MaxParallelFiles` | -1 (dataset default: 3 Stellar / 1 Astral) | Sets `OSPREY_MAX_PARALLEL_FILES` for both A/B legs |

Environment variables that affect testing:

| Env var | Where | Effect |
|---|---|---|
| `OSPREY_VERSION_OVERRIDE` | `OspreyVersion.cs:93`; set by `regression.ps1:133` | Pins the logical version string so the golden's `osprey_version` cell stays deterministic |
| `OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT` | `OspreyEnvironment.cs:104`; used `IOTest.cs:2082` | When set, the sidecar round-trip test copies its output there for a byte-compare against the Rust sibling test |
| `OSPREY_CROSS_IMPL_RECONCILIATION_OUT` | `OspreyEnvironment.cs:112` | Same, for the reconciliation sidecar |
| `OSPREY_DUMP_STAGE7_PROTEIN_FDR` | set by `regression.ps1:244` | Emits the Stage 7 protein-FDR dump that mode 1 diffs against the golden |
| `OSPREY_MAX_PARALLEL_FILES` | `Test-PerfGate.ps1:303` | Cross-binary outer-parallelism control for the perf A/B (back-compat for the pinned baseline that predates `--parallel-files`) |

The `CodeInspectionTest` exemption tags are inline-comment "flags," not CLI
flags: `// Array.Sort OK: <reason>` (`CodeInspectionTest.cs:94`) and
`// DECOY_ pairing OK: <reason>` (`CodeInspectionTest.cs:158`).

## Divergences from the Rust documentation

- **[INTENTIONAL-CSHARP-DESIGN] Separate MSTest project vs inline `#[cfg(test)]`** -
  Rust doc says all tests are inline unit tests in `#[cfg(test)] mod tests`
  blocks with no separate integration directory (Rust 11-testing.md lines 3,
  657-659); C# collects all tests into one `Osprey.Test` MSTest project that
  references every production project. Evidence: `Osprey.Test/Osprey.Test.csproj:43-51`,
  41 `[TestClass]` / 492 `[TestMethod]`. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Two standing CI gates have no Rust analog** -
  Rust doc's "Running Tests" is `cargo test --all` plus `cargo fmt`/`cargo clippy`
  (Rust 11-testing.md lines 34-42) with no end-to-end golden or perf gate. C#
  adds `regression.ps1` (golden + resume + HPC-chain at 1e-9) and
  `Test-PerfGate.ps1` (interleaved A/B wall-time). Evidence: `regression.ps1:1`,
  `Test-PerfGate.ps1:1`. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Static-analysis test enforcing cross-impl
  determinism** - Rust needed no sort-stability guard (`slice::sort_by` is
  stable) and no DECOY_-prefix guard as a test; C# adds `CodeInspectionTest`
  because .NET introsort is unstable. Evidence: `CodeInspectionTest.cs:85` (unstable
  sort), `CodeInspectionTest.cs:150` (decoy pairing). Severity: info.

- **[STALE-RUST-DOC] Test count "~302"** - Rust 11-testing.md line 46 states
  "Total: ~302 tests"; the C# port measures 492 `[TestMethod]` tests, and the
  orchestration brief cited a "317-test" Rust model, so neither the round number
  nor the exact figure is authoritative today. This is a moving count, not a
  behavioral difference. Evidence: measured across `Osprey.Test/*.cs`. Severity: info.

- **[STALE-RUST-DOC] Mokapot test coverage** - Rust 11-testing.md documents an
  entire `osprey-fdr/src/mokapot.rs` test suite (PIN format, result parsing,
  runner availability — lines 487-509) predicated on the external Python mokapot
  tool. The C# port has a native managed Percolator SVM and no Python mokapot
  dependency, so those Mokapot-specific tests do not exist in `Osprey.Test`
  (the `FdrMethod` enum retains a `Mokapot` value but it is not wired to the CLI).
  Evidence: `FdrTest.cs` tests the native Percolator path; no `MokapotTest.cs`
  exists under `Osprey.Test/`. See 07-fdr-control.md. Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] Plain MSTest `Assert`, not `AssertEx`** - the
  reconnaissance framing referred to "AssertEx + translation-proof tests"; the
  actual `Osprey.Test` project uses plain MSTest `Assert`/`CollectionAssert`/
  `StringAssert` with no `AssertEx` helper and no reference to Skyline's
  `TestUtil`. "Translation-proof" is realized instead through ported hand-computed
  reference values, `BitConverter.DoubleToInt64Bits` exact comparisons, and the
  `OSPREY_CROSS_IMPL_*` byte-parity hooks. Evidence: no `AssertEx` in
  `Osprey.Test/*.cs`; byte-parity hook at `IOTest.cs:2082`,
  `OspreyEnvironment.cs:104`. Severity: info.

- **[STALE-RUST-DOC] Regression README documents 2 legs; the script runs 3** -
  `Regression/README.md:30-37` lists only mode 1 (golden) and mode 2 (resume),
  but `regression.ps1` also runs mode 3 (HPC 4-task worker chain,
  `regression.ps1:502-522`). This is an internal C#-side doc lag, not a Rust
  divergence, noted here for accuracy. Evidence: `regression.ps1:24-36` (all three
  modes in the synopsis) vs `Regression/README.md:22-39`. Severity: minor.

Verified: I confirmed the MSTest project structure and references
(`Osprey.Test.csproj`), the 41-class/492-method inventory, the plain-`Assert`
usage, the two `CodeInspectionTest` rules and their exemption tags, the three
regression legs and their 1e-9 tolerance and version pin, the cross-impl
byte-parity env-var hooks in `OspreyEnvironment.cs` and their use in `IOTest.cs`,
and the `Test-PerfGate.ps1` interleaved-A/B design and hard-fail-on-total logic.
No product-behavior PORT-ERROR was found; all divergences are infrastructure or
documentation differences.
