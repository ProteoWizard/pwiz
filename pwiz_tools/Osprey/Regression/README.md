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
4. **mode 5 - Stage-5 rehydrate self-consistency**. Invalidates ONLY the
   `SecondPassFDR` task (the blib + its `SecondPassFDR` stamp), leaving the
   `FirstPassFDR` stamp and every 1st-pass sidecar valid, so the re-run rebuilds
   its post-Stage-5 bundle from those **own** sidecars
   (`LoadOwnReconciliationBundle`). (Task **names** throughout - the classes
   behind them are `FirstPassFdrTask` and `SecondPassFdrTask`, and only the names appear
   in the `[TASK]` log lines and `.osprey.task` stamps.) **No other leg reaches
   that loader**: mode 2 deletes the `FirstPassFDR` stamp, so that task *runs*;
   mode 4 invalidates nothing, so nothing demands its state; and mode 3's
   `PerFileRescoring` phase *does* enter the rehydrate arm, but adopts a
   **worker-supplied** bundle rather than loading its own. Asserts a marker logged
   from inside that loader (a cache hit does not prove it ran, and neither does the
   generic rehydrate line, which a worker bundle emits too), the blib against the
   pristine straight-through one at 1e-9, and, for datasets with
   `ModelDiagnostics`, the report re-emitted from those sidecars against the same
   golden mode 1b uses.

   Runs after mode 2: its SecondPassFDR leg rewrites the 2nd-pass sidecars and the
   diagnostics report as well as the blib, none of which `Invoke-ResumeInvalidation`
   deletes, so running it earlier would leave mode 2 resuming on top of mode-5
   state.

6. **mode 6 - library-fragment release engagement** (issue #4532). Asserts, from
   each leg's own log, that the Stage 5 -> 6 library-fragment release RAN wherever
   the library is held - straight-through, resume, the own-sidecar rehydrate, every
   `--task PerFileRescoring` worker, and the `SecondPassFDR` node - and did **not**
   run on `--task FirstPassFDR`, which loads with `OmitFragments` and so can only
   ever report a saving it did not make.

   No output comparator can make this assertion. The release is **output-neutral by
   design** - that is its safety argument - so modes 1-5 pass identically whether it
   ran or was deleted outright. They catch an *over*-release (the released-spectrum
   tripwire throws) but are structurally blind to it silently not happening, and
   every defect found reviewing #4534 was in that blind spot. Verified by
   construction: with `OSPREY_RELEASE_LIBRARY_FRAGMENTS=0` the other legs stay green
   and only mode 6 goes red.

   Asserts presence and non-zero counts, **never exact counts** - those move with any
   scoring change. One run-wide check asserts the log pattern matched *somewhere*, so
   a reworded C# line fails the gate instead of quietly satisfying the
   "must not release" leg. Always on; there is no skip switch.

   Runs **last**, after mode 5, because it reads the logs every leg above wrote.

**modes 3 and 4** (the HPC 4-task worker chain, and the warm re-run cache-hit
assertion) are described in `regression.ps1`'s own comment header, alongside why
comparing output alone cannot detect a cache-invalidation regression.

**No** leg names a resident-pool token. Mode 5 was the last one that did
(`OSPREY_ALLOW_UNFIXED_RESIDENT=resume-survivor-handoff`, because a resume could not stream
the Stage 6 survivor handoff); issue #4536 gave the rehydrate its own per-file survivor
loader, so that arm streams instead of needing one. An *inherited* value is cleared at
startup unless a deliberate A/B switch needs it. Any token this gate is ever made to require
must have an open issue to remove it, and the run summary prints the outstanding-gap table
so a required token would be visible on every green run - today that table is empty and the
summary prints `none`.

Zero tokens is not zero O(files) paths, and the summary says only the former. The survivor
buffer is rebuilt at the end of Stage 6 for `SecondPassFDR` to read, so it is resident from
there to the end of Stage 7 on every path; no guard covers that because it is Stage 7's
input rather than a mode or a resume. That is #4486.

An ambient allowance on a standing gate can only mask the regression the gate exists to
catch - which is how the former blanket `OSPREY_ALLOW_UNBOUNDED_MEMORY=1` let an
`OSPREY_PASS2_QVALUE=transfer` regression ride along for ten days.

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
