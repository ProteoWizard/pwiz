# Osprey Regression Harness (pwiz-standalone)

This folder holds the **self-contained** end-to-end regression for Osprey,
run overnight by the scheduled TeamCity "Osprey Windows .NET Regression" config
and locally by developers. It has **no dependency on the sibling `ai/`
checkout** — data acquisition, blib golden capture/compare, and the tolerance
comparators all live here, so a clean pwiz agent can run it.

The developer-facing bisection tooling (stage-isolated snapshots, cross-impl
Rust comparison) stays in `ai/scripts/Osprey/`; this is the nightly gate,
not the drill-down.

## Entry points

| File | Role |
|------|------|
| `../regression.ps1` | the harness — acquire data, run, compare, report |
| `../tctest.bat` | scheduled TeamCity entry point (`regression.ps1 -TeamCity -Dataset All`) |
| `RegressionData.ps1` | download + unzip + skip-if-present (TestPerf-style) |
| `BlibGolden.ps1` | blib projection schema + golden capture/compare + full blib-vs-blib |
| `DiagnosticsGolden.ps1` | model-diagnostics metric projection + golden compare + fixed FDR sanity bounds |

## Datasets

| Dataset | Decoys | Entrapment | Resolution | Role |
|---|---|---|---|---|
| `Stellar` | generated (reverse) | no | unit | fast local pre-commit gate |
| `StellarLibDecoy` | library-supplied (Carafe) | yes, r=1.0 | unit | the recommended path; the only one that can measure true FDP |
| `Astral` | generated (reverse) | no | hram | larger, HRAM, MS1 features live |

`StellarLibDecoy` reuses the **same** Stellar mzML (via the spec's `LibraryFolder`),
so the published zip carries one copy of the raw data. Its 2.4 GB library ships as a
**nested zip** (`stellar-libdecoy/libdecoy-entrapment.zip`) that is extracted only when
that dataset is selected — so `-Dataset Stellar` never pays for it.

The data zip is **`osprey-testfiles-mzML-v2.zip`**. Acquisition is skip-if-present on
the extracted root, so adding files under the old name would never reach a machine that
already had the tree; the v1 zip and URL stay live for older branches.

## What it asserts

For each dataset, with **zero input copies**
(inputs referenced read-only from `<Downloads>\Perftests\osprey-testfiles-mzML-v2`,
all output + caches under a per-run timestamped `TestResults/regression-<stamp>`
via `--work-dir` — gitignored scratch, **nothing is published as a TeamCity
artifact**, so the multi-GB spectra caches there are harmless):

1. **mode 1 — straight-through vs committed golden** (the user-facing
   correctness gate). Compares the Stage 7 protein-FDR dump + a deterministic
   ~500-precursor subset + a full-set summary against `osprey-regression.data/`
   at 1e-9.
2. **mode 1b — FDR-calibration spot checks** (datasets with `ModelDiagnostics`).
   Two independent tiers over the `--model-diagnostics` report: the metric
   projection vs `diagnostics.tsv` at 1e-9, **plus fixed sanity bounds that
   `-CreateGolden` does not regenerate**. The blib golden proves the search did
   not change; it cannot prove the calibration is still *correct*, because a
   change can leave the ranking intact and only wreck the reported q-values —
   which is exactly what the b↔y decoy swap did at ~12x the claimed error rate.
   The bounds are the only thing that fails when a bad change is blessed into a
   regenerated baseline, so they must not be derived from the run.
3. **mode 2 — resume vs straight-through self-consistency**. Re-runs the build
   in resume mode (invalidate the Stage 5 join + blib, re-run the same command
   so the rehydrate paths fire) and asserts the resume blib equals the
   straight-through blib at 1e-9. The build is its own oracle — no baseline.

Every leg of a dataset — straight-through, resume, and each HPC phase — is given
the **same** dataset CLI flags (`Get-DatasetCliArgs`). A self-consistency oracle
only means anything if both sides ran the same search.

Any mismatch emits a TeamCity `buildProblem` and a non-zero exit.

## The golden (`osprey-regression.data/<dataset>/`)

The real datasets are ~60K precursors, so a full-fidelity blib is 50–135 MB —
too big to commit. Instead the golden is a **small committed text** capture
(measured: Stellar ~1.1 MB, Astral ~2.4 MB; ~3.5 MB total — most of it the
full Stage 7 `protein_fdr.tsv`), versioned with the code (so building an older
tagged commit runs against its own matching baseline), and diff-reviewable:

| Artifact | Contents | Compared at |
|----------|----------|-------------|
| `protein_fdr.tsv` | full Stage 7 protein-FDR dump | 1e-9 |
| `tables/<Table>.tsv` | per-table projection; full for small tables (Proteins, metadata, source files), a deterministic ~500-precursor subset for the large per-precursor tables | 1e-9 |
| `tables/PeakDigest.tsv` | per-spectrum SHA-256 of the peak blobs (subset) | exact |
| `blib_summary.tsv` | full-set per-table row counts + per-numeric-column aggregates | counts exact, aggregates rel-1e-6 |

The subset is selected by `MD5(peptideModSeq|precursorCharge) % 120 == 0` —
order- and machine-independent, spread across the data, ~500 of ~60K. The
full-set summary catches drift on precursors **outside** the subset (coarsely);
the subset catches it precisely at 1e-9.

By design this means mode 1 has one blind spot: a small value change confined to
out-of-subset precursors — below the summary's relative-1e-6 floor, or a
sign-cancelling pair of changes — is invisible to it. That is the accepted cost
of a ~3.5 MB committed golden vs. a 50–135 MB one; **mode 2** (full resume
self-consistency at 1e-9) and the subset are the tight gates, the summary is the
coarse out-of-subset backstop. A change that shifts results broadly (the common
regression) hits the subset and the summary regardless.

### Refreshing the golden

Only on an **intentional, reviewed behavior change**. Re-capture with:

```powershell
pwsh -File ./pwiz_tools/Osprey/regression.ps1 -Dataset All -CreateGolden
```

Then review the `osprey-regression.data/` diff (text — readable) before
committing it alongside the behavior change. Do **not** refresh to make an
unexplained failure go green; a red mode-1 means the output moved.

## Local use

```powershell
# Stellar only, against the committed golden (mode 1 + mode 2)
pwsh -File ./pwiz_tools/Osprey/regression.ps1 -Dataset Stellar

# Mode 1 only (skip the resume leg)
pwsh -File ./pwiz_tools/Osprey/regression.ps1 -Dataset Stellar -SkipResume

# Reuse an existing Release build (skip the build step)
pwsh -File ./pwiz_tools/Osprey/regression.ps1 -Dataset Stellar -NoBuild
```

Data downloads once to `<Downloads>\Perftests` and is reused on later runs
(skip-if-present); CI agents start clean and download every night.
