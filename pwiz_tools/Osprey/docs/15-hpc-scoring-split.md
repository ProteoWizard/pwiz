# 15. HPC Scoring Split (the four --task workers) (C#)

> Pipeline stage: Cross-cutting (orchestration). C# port of Rust docs/18-hpc-scoring-split.md. Corresponds to Rust osprey `--no-join` / `--join-at-pass` HPC scoring split.

For large experiments (hundreds to thousands of mzML files) the Osprey pipeline can be split into phases that scale very differently. The embarrassingly-parallel per-file phases (Stages 1-4 scoring, Stage 6 rescore) can fan out across N HPC nodes; the single-process join phases (Stage 5 first-pass FDR + reconciliation planning, Stages 7-8 second-pass FDR) run once on a head node and require all-file evidence.

The C# port implements this split as **four pipeline tasks** driven by a single `--task <Name>` CLI selector, rather than the Rust doc's `--no-join` / `--join-at-pass` / `--join-only` flag family. Each task is a subclass of `OspreyTask` (`Osprey.Tasks/OspreyTask.cs`), and the orchestration model is a per-task membership predicate walked by a driver loop (`Osprey/AnalysisPipeline.cs:99-112`) rather than a contiguous `[start..stop]` stage window.

> **This document owns operations**: CLI flags, the membership truth table, `--input-scores` resolution and ordering, footer-hash validation, and concurrency. *Why* the split has this shape — the scope of each artifact, which task may read what, and the exact file list a node must be shipped at each boundary — is owned by [00-pipeline-architecture.md](00-pipeline-architecture.md), and the byte formats by [14-intermediate-files.md](14-intermediate-files.md). Read 00 before changing what any task writes.

> **In flight** - this document describes `--task PerFileRescoring` as rehydrating `FirstPassFdrTask` and reading an all-runs `CompactedEntries` buffer (the membership truth table below, and the Stage 6 section). That is what the branch `Skyline/work/20260901_osprey_firstpass_resume` replaces with a per-run hydrate, so both statements change when it lands. **Deviations from the target architecture are tracked in one place - 00's `## In flight` section - not per document**; this note exists so a reader of 15 alone knows to look there.

## Task-name mapping (CLI name vs internal enum)

The CLI `--task` spelling, the `HpcTask` enum member, and the task class are now one name per task. The only one that still needs a second look is `PerFileRescoring` (CLI) vs `PerFileRescore` (enum and class); the rest differ at most in the `Fdr`/`FDR` casing.

| Pipeline stage | CLI `--task` name | `HpcTask` enum | `OspreyTask` subclass (`OspreyTask.Name`) |
|---|---|---|---|
| Stages 1-4 per-file scoring | `PerFileScoring` | `HpcTask.PerFileScoring` | `PerFileScoringTask` (`"PerFileScoring"`) |
| Stage 5 first-pass FDR | `FirstPassFDR` | `HpcTask.FirstPassFdr` | `FirstPassFdrTask` (`"FirstPassFDR"`) |
| Stage 6 per-file rescore | `PerFileRescoring` | `HpcTask.PerFileRescore` | `PerFileRescoreTask` (`"PerFileRescoring"`) |
| Stages 7-8 second-pass FDR | `SecondPassFDR` | `HpcTask.SecondPassFdr` | `SecondPassFdrTask` (`"SecondPassFDR"`) |

- The enum is defined in `Osprey.Core/OspreyConfig.cs`. Its first four members are the pipeline tasks above, in execution order: `{ PerFileScoring, FirstPassFdr, PerFileRescore, SecondPassFdr }`. Two more are appended — `SpectraCache` (build each input's `.spectra.bin` and stop; the data-staging step *ahead* of the pipeline) and `ModelDiagnostics` (regenerate the `--model-diagnostics` report for a completed analysis). **Neither is a pipeline stage**: both are reachable only by naming them in `--task`, neither appears in `CanonicalPipeline()`, and neither belongs in an HPC relay plan. They are appended rather than ordered first so the existing members keep their ordinal values. See 00-pipeline-architecture.md, "Two selectable tasks that are not pipeline tasks".
- The CLI-name to enum mapping is `Program.ResolveTask` and its inverse `Program.TaskCliName`, both in `Osprey/Program.cs`.
- The `OspreyTask.Name` strings (used in `[TASK]` log lines and `.osprey.task` sidecar naming) ARE the CLI spelling, and the class names now match them: `PerFileScoringTask.Name => "PerFileScoring"`, `FirstPassFdrTask.Name => "FirstPassFDR"` (`Osprey.Tasks/FirstPassFdrTask.cs:71`), `PerFileRescoreTask.Name => "PerFileRescoring"` (`Osprey.Tasks/PerFileRescoreTask.cs:114`), `SecondPassFdrTask.Name => "SecondPassFDR"` (`Osprey.Tasks/SecondPassFdrTask.cs:49`). The one name to read carefully is `PerFileRescore`/`PerFileRescoring`; everything else differs only in the `Fdr`/`FDR` casing, which follows this codebase's own type convention (`FdrEntry`, `FdrController`) rather than the all-caps `pwiz.Osprey.FDR` namespace.

## Orchestration model: `--task` + membership predicates

Instead of Rust's `--no-join` / `--join-at-pass=N` / `--join-only` flags, the C# entry point takes an optional `--task <Name>`, resolves it to an `HpcTask`, and derives three boolean config flags that the four tasks' `IsIncluded` predicates read (`Osprey/Program.cs:118-128`):

```
config.SelectedTask         = selectedTask;
config.NoJoin               = task == PerFileScoring || task == PerFileRescore;
config.StopAfterStage5      = task == FirstPassFdr;
config.ExpectReconciledInput= task == SecondPassFdr;
```

The pipeline is a fixed four-task list, `AnalysisPipeline.CanonicalPipeline()` (`Osprey/AnalysisPipeline.cs:139-148`), always in execution order `PerFileScoring → FirstPassFDR → PerFileRescore → SecondPassFDR`. The driver loop (`Osprey/AnalysisPipeline.cs:99-112`):

1. Skips any task whose `IsIncluded(ctx)` is false (excluded tasks lazy-rehydrate their state on demand if a downstream task reaches for it).
2. Skips any included task whose declared `Outputs` already exist on disk with a matching `.osprey.task` validity-key sidecar (`ctx.CanRehydrate`).
3. Otherwise runs the task via `RunTask` (`Osprey/AnalysisPipeline.cs:165-216`), which measures wall time, marks the task materialized, and writes output sidecars.

Cross-task state flows through a typed byproduct registry (`PipelineContext.Get<T>()` / `Publish<T>()`), not constructor arguments. A `ctx.Get<T>()` cache miss lazily materializes the producing task through its `Rehydrate` (disk-load) path — this is what lets a worker that starts mid-pipeline pull the upstream state from the boundary files on disk.

### Membership truth table

The exact per-task membership per mode is pinned by `Osprey.Test/PipelineMembershipTest.cs:60-78`:

| Mode | `PerFileScoring` | `FirstPassFDR` | `PerFileRescore` | `SecondPassFDR` |
|---|---|---|---|---|
| straight-through (no `--task`, `-i mzML`) | run | run | run | run |
| `--task PerFileScoring` (`NoJoin`) | run | – | – | – |
| `--task FirstPassFDR` (`StopAfterStage5`) | rehydrate | run | – | – |
| `--task PerFileRescoring` (`NoJoin`+`InputScores`) | rehydrate | rehydrate | run | – |
| `--task SecondPassFDR` (`ExpectReconciledInput`) | rehydrate | (skipped) | rehydrate | run |
| `--input-scores`, no `--task` (single-node full) | rehydrate | run | run | run |

("rehydrate" = excluded from the driver loop but lazily materialized on demand from disk; "–" = never touched.) The predicates live in `PerFileScoringTask.IsIncluded` (`:84-88`), `FirstPassFdrTask.IsIncluded` (`:80-95`), `PerFileRescoreTask.IsIncluded` (`:123-130`), and `SecondPassFdrTask.IsIncluded` (`:57-64`).

## Stage 1-4 — Per-file scoring (`--task PerFileScoring`)

`PerFileScoringTask` (`Osprey.Tasks/PerFileScoringTask.cs`). Load the library + generate/pair decoys (`LoadLibraryAndDecoys`, `:695`), then score every input mzML (`Run`, `:173-421`). Each file's parse → RT/mass calibration → coelution scoring writes:

- `<stem>.scores.parquet` — the per-file PIN-feature / fragment / CWT-candidate cache (`ParquetScoreCache.GetScoresPath`).
- `<stem>.calibration.json` — RT + MS1/MS2 mass calibration (`CalibrationIO.CalibrationPathForInput`).
- `<stem>.spectra.bin` — the decoded-spectrum cache. No *join* reads it, but Stage 6 rescore does, and it is what lets a search run at all once the input has been deleted (see 14-intermediate-files.md).

The parquet footer is stamped once against the unmutated outer config (`:226-232`) with `osprey.version`, `osprey.search_hash`, `osprey.library_hash`, and `osprey.reconciled = "false"`. Under `--task PerFileScoring` (`config.NoJoin` with no `--input-scores`) the task stops after writing the parquets and returns false with `ExitCode = 0` (`FinalizeAndCheck`, `:649-658`) — Stage 5+ is skipped, no blib is written. `--output` is accepted but not used (`Osprey/Program.cs:228-232` reports the real per-file parquet output instead of warning).

Under `--task PerFileScoring` the task's `IsIncluded` requires **no** `--input-scores` (`:84-88`); `ValidateArgs` rejects `--task PerFileScoring --input-scores` (`Osprey/Program.cs:357-366`).

`ProcessFile` always writes the parquet regardless of task, matching Rust's end-to-end behavior (the sidecar is needed by Stage 6 reconciliation to lazy-load CWT candidates).

## Stage 5 — First-pass FDR (`--task FirstPassFDR`)

`FirstPassFdrTask` (`Osprey.Tasks/FirstPassFdrTask.cs`). Requires all per-file caches. `Run` (`:183-408`):

1. Reads the pre-compaction shared entry buffer (`ctx.Get<ScoredEntries>()`, `:203`).
2. Runs first-pass Percolator SVM FDR (`RunFdr` / `RunFirstPassProjection`, `:269-286`) with per-file PIN features streamed back on demand from each `.scores.parquet` (`loadFileFeatures`, `:244-249`).
3. Runs first-pass picked-protein FDR on the full pre-compaction pool (`RunFirstPassProteinFdr`, `:309`) — unconditional, not gated on `--protein-fdr` (see 07-fdr-control.md, 08-protein-parsimony.md).
4. Persists the per-file `<stem>.1st-pass.fdr_scores.bin` sidecars **before** compaction, so every stub carries its q-values (`WriteFdrScoresSidecars`, `:322`, `:827-858`).
5. Compacts each file's stub list to the passing base_ids (`CompactFirstPass`, `:367`, `:657-720`): keep a base_id if `RunPeptideQvalue ≤ ReconciliationCompactionFdr` OR `RunProteinQvalue ≤ EffectiveProteinFdr`.
6. Stage 6 **planning** (`PlanStage6`, `:387-392`, `:736-808`): multi-charge consensus, cross-run consensus RTs, per-file calibration refit, reconciliation planning, then writes each `<stem>.reconciliation.json` envelope (`WriteReconciliationFiles`, `:878-1039`).

The boundary file pair per file is thus `<stem>.1st-pass.fdr_scores.bin` + `<stem>.reconciliation.json`. Each reconciliation.json carries `search_hash`, `library_hash`, the sorted join-wide file-stem set, and the global first-pass passing base_id set (`:970-996`), so a single-file Stage 6 worker can reconstruct the join-wide compaction set.

Under `--task FirstPassFDR` (`config.StopAfterStage5`), `PlanStage6` writes the boundary pair and returns true with `ExitCode = 0` before Stage 6 rescore (`:775-797`). `IsIncluded` requires `--input-scores` with 2+ parquets (`ValidateArgs`, `Osprey/Program.cs:379-399`) and `Reconciliation.Enabled = true`.

## Stage 6 — Per-file rescore (`--task PerFileRescoring`)

`PerFileRescoreTask` (`Osprey.Tasks/PerFileRescoreTask.cs`). Consumes the boundary file pair + the per-file parquet and re-scores each file against the consensus + reconciliation boundaries, runs the gap-fill two-pass, and writes a **reconciled** parquet. `Run` (`:185-315`) reads the post-FirstPassFDR buffer (`ctx.Get<CompactedEntries>()`, `:200`; demanding it materializes `FirstPassFdrTask`), self-gates on planning state (`didPlan` or a rescore bundle, and no 2nd-pass sidecar already present, `:232-247`), then calls `ExecuteRescore` (`:491-612`).

`ExecuteRescore` runs the per-file loop `RescoreOneFile` (`:644-813`), which for each file with work: builds `boundary_overrides` keyed by entry_id + the subset library, streams spectra from `.spectra.bin` (`LoadSpectraForRescore`; there is **no** mzML fallback — an absent, stale or wrong-version cache is fatal here) and reloads mass calibrations, picks the refined RT calibration (falling back to first-pass), re-scores via `ScoringPipeline.RunCoelutionScoring`, overlays the rescored entries in place, runs gap-fill, and writes the reconciled parquet. Rescore runs the files **in parallel** under the same `EffectiveFileParallelism` the scoring phase resolved (`:547-584`).

Reconciled output goes to a **separate** `<stem>.scores-reconciled.parquet` sibling, leaving the Stage 4 `<stem>.scores.parquet` intact (`ParquetScoreCache.GetReconciledScoresPath`; `WriteReconciledAndStamp`, `:944-987`). Its footer carries `osprey.reconciled = "true"` plus `osprey.reconciliation_hash` (`Osprey.Tasks/ReconciledParquetWriter.cs:198-205`). This differs from the Rust doc, which says Stage 6 "rewrites each `<stem>.scores.parquet`" in place (see Divergences).

Under `--task PerFileRescoring` (`config.NoJoin` + `--input-scores`), `IsIncluded` (`:123-130`) includes only this task; `PerFileScoringTask` and `FirstPassFdrTask` lazy-rehydrate the upstream state from the boundary files via `ctx.Demand`. `RescoreWorker.Run` (`Osprey/RescoreWorker.cs:80-91`) is now a thin alias that just calls `new AnalysisPipeline().Run(config)` — the hand-rolled worker path was collapsed into the canonical driver. `ValidateArgs` forbids `-i` (mzML paths are derived from the parquet stems) and requires `--library` + `--output` (`Osprey/Program.cs:368-377`).

## Stages 7-8 — Second-pass FDR (`--task SecondPassFDR`)

`SecondPassFdrTask` (`Osprey.Tasks/SecondPassFdrTask.cs`). The terminal aggregator. `Run` (`:115-235`):

1. Second-pass Percolator FDR whenever any reconciled parquet exists on disk (`AnyReconciledParquet`, `:284-294` — the C# analog of Rust's `total_rescored > 0`), via `Pass2FdrSidecar.ComputeAndPersist` (`:149`), which reloads reconciled features, reruns Percolator, writes `<stem>.2nd-pass.fdr_scores.bin`, and reloads them onto the stubs.
2. Second-pass protein FDR — always runs (parsimony + picked-protein at `config.RunFdr`), `RunProteinFdr` (`:249-273`).
3. Re-clamp experiment q to best run q (`PercolatorEngine.ClampExperimentQToBestRun`, `:177`).
4. Write the BiblioSpecLite `.blib` (`WriteBlibOutput`, `:299-370`; see 13-blib-output-schema.md).

Under `--task SecondPassFDR` (`config.ExpectReconciledInput`), `Rehydrate` (`:317-455`) hydrates from the reconciled parquets + sidecars **without** materializing `FirstPassFdrTask` (which would wrongly re-run Stage 5 Percolator on the reconciled parquets), applies its own compaction, and lets `SecondPassFdrTask.Run` do 2nd-pass FDR + protein FDR + blib. The strict reconciled-input gate asserts every `--input-scores` parquet carries `osprey.reconciled = "true"` (`ParquetScoreCache.ValidateScoresParquetGroup`, `Osprey.IO/ParquetScoreCache.cs:1292-1305`). `ValidateArgs` forbids `-i` and requires `--library` + `--output` (`Osprey/Program.cs:401-408`).

`SecondPassFdrTask.Rehydrate` returns `true` as a no-op (`:113`): nothing consumes SecondPassFDR's state in-memory, so it is never demanded.

## Full pipeline (default) and `--input-scores` full run

With no `--task`, all four tasks run in one process (`straight-through` row of the truth table): output is identical to running the four workers in sequence over the same files. `--input-scores` with no `--task` (the `input-scores-full` row) runs Stages 5-8 in one process from existing per-file parquets — `PerFileScoringTask` is excluded (`IsIncluded` returns false when `InputScores` is non-empty, `:84-88`) and lazy-rehydrates the supplied scores instead of recomputing them.

## `--input-scores` resolution and ordering

`Program.ResolveInputScores` (`Osprey/Program.cs:446-483`), wired through `ARG_INPUT_SCORES` (`Osprey/OspreyCommandArgs.cs:208-220`):

- A single **directory** argument is globbed **non-recursively** for `*.parquet`, then classified by suffix: `*.scores-reconciled.parquet` vs `*.scores.parquet` (`:459-465`).
- **Reconciled wins per stem**: a stem with both files returns only the reconciled parquet (the authoritative later pass); a stem with only the original returns the original (`:469-472`).
- The resulting list is **sorted Ordinal** (`:473`), so the file order is deterministic and stable across nodes.
- Explicit path lists are passed through unchanged after existence validation (`:477-482`); repeated `--input-scores` flags accumulate and re-resolve (`OspreyCommandArgs.cs:211-218`).
- Empty directory or missing explicit path throws (`:466-468`, `:479-480`).

At pipeline entry, `AnalysisPipeline.Run` synthesizes `config.InputFiles` from the parquet stems once (`:76-83`, via `RescoreHydration.SyntheticInputFromParquet`) so every per-task accessor sees a populated `InputFiles` regardless of which task the run starts at.

## Parquet footer hash validation

The `FirstPassFDR` and `SecondPassFDR` steps are only safe if every input parquet was scored with the same library, search parameters, and a compatible binary version. `ParquetScoreCache.ValidateScoresParquetGroup` (`Osprey.IO/ParquetScoreCache.cs:1256-1308`) validates each parquet's footer before any work, delegating to `CheckParquetMetadata` (`:1190-1248`):

| Metadata key | Covers | Failure mode |
|---|---|---|
| `osprey.version` | binary year/feature/patch/daily | hard-fail on any mismatch (`:1210-1227`) |
| `osprey.search_hash` | search parameters (`config.Identity.SearchParameterHash()`) | mismatch → named error (`:1231-1236`) |
| `osprey.library_hash` | library identity (`config.Identity.LibraryIdentityHash()`) | mismatch → named error (`:1240-1245`) |
| `osprey.reconciled` | whether already reconciled | `--task SecondPassFDR` requires `"true"` (`:1292-1305`) |
| `osprey.reconciliation_hash` | reconciliation params + sorted file set | written on reconciled parquets (`ReconciledParquetWriter.cs:195-204`) |

On mismatch the run aborts before any FDR/reconciliation work, naming the offending file and which hash mismatched — mirroring the Rust doc's abort-early contract. This runs at the start of `--task FirstPassFDR` load (`PerFileScoringTask.LoadJoinOnlyScores`, `:1003-1006`). The same-task-resume skip is enforced separately by the `.osprey.task` validity-key sidecars (`OspreyTask.ValidityKey`, `OspreyTask.cs:173-176`), and per-file rescore-resume by `PerFileResumeDriver` (`TryResumeRescoredFile`, `:827-867`).

## Persistent files per input

**Moved.** The authoritative list of what each task writes and reads — including the
experiment-wide artifacts this section never covered, and which of them must be relayed to
every node — is the sidecar file contract in
[00-pipeline-architecture.md](00-pipeline-architecture.md). It is verified row by row against
the path-building code; the table that used to sit here had drifted (it attributed
`<stem>.2nd-pass.fdr_scores.bin` to Stage 7, when `PerFileRescoring`'s pass-2 worker writes it
whenever that worker runs).

Per-run notes worth keeping alongside the contract:

- `<stem>.spectra.bin` is **mandatory** for the Stage 6 rescore — streamed per window, with no
  mzML fallback — and is the only spectrum source once the input has been deleted.
- `<stem>.1st-pass.fdr_scores.bin` and `<stem>.reconciliation.json` are both mandatory for a
  Stage 6 worker.
- `<stem>.calibration.json` is read for **inverse RT prediction and isolation windows** — the
  latter giving the gap-fill m/z filter its per-run coverage, which is why a `SecondPassFDR`
  node with no mzML still needs it. Read behind a `File.Exists`, so its absence changes
  gap-fill filtering silently rather than failing.

See 00 for the relay checklist per boundary and 14-intermediate-files.md for the full cache
formats.

## Cross-implementation compatibility and bisection dumps

The parquet cache and `reconciliation.json` boundary file are shared with Rust Osprey at each pipeline seam. Cross-impl byte-parity is validated via env-var-gated diagnostic dumps routed through `ctx.Diagnostics` (`Osprey.Diagnostics/IOspreyDiagnostics.cs`). The C# port exposes the Stage 5/6 dumps the Rust doc lists, including `OSPREY_DUMP_PERCOLATOR` (`DumpPercolator`, written pre-compaction so the diff sees targets + decoys, `FirstPassFdrTask.cs:551-554`), `OSPREY_DUMP_STANDARDIZER`, `OSPREY_DUMP_SUBSAMPLE`, `OSPREY_DUMP_SVM_WEIGHTS`, `OSPREY_DUMP_CALIBRATION`, `OSPREY_DUMP_PROTEIN_FDR`, `OSPREY_DUMP_CONSENSUS`, `OSPREY_DUMP_INV_PREDICT`, `OSPREY_DUMP_MULTICHARGE`, `OSPREY_DUMP_REFIT`, `OSPREY_DUMP_LOESS_FIT`, `OSPREY_DUMP_RECONCILIATION`, `OSPREY_DUMP_MP_INPUTS`, and the Stage 6 rescore dump `OSPREY_DUMP_RESCORED` (`PerFileRescoreTask.cs:297-313`). Each has an `_ONLY` companion that exits after writing. **`OSPREY_DUMP_PREDICT_RT` is declared but its scoring-hotspot call site is disabled** in the C# port for performance (`PerFileRescoreTask.cs:715-726`; see Divergences and 18-peptide-trace.md).

## Safe concurrent writes

Every cache writer stages through a `FileSaver` temp file that is a **sibling in the same
directory** as its destination, then promotes it with an in-volume rename — not a local temp
plus a cross-volume copy-and-verify, which is the Rust pattern this port replaced. A mid-write
process kill on a NAS/CIFS mount therefore leaves either the previous content or no file, never
a corrupt cache a downstream stage must reject. See principle P8 in
[00-pipeline-architecture.md](00-pipeline-architecture.md), and 16-determinism.md.

## Flags and switches

| Flag / field | Default | Effect on this stage |
|---|---|---|
| `--task {PerFileScoring\|FirstPassFDR\|PerFileRescoring\|SecondPassFDR}` | none (full pipeline in one process) | Selects one HPC worker; sets `NoJoin` / `StopAfterStage5` / `ExpectReconciledInput` (`Program.cs:126-128`). Resolved case-insensitively (`ResolveTask`). |
| `--input-scores <paths\|dir>` | none | One or more `.scores.parquet` files or a single directory (globbed non-recursively, reconciled-wins-per-stem, Ordinal-sorted). Mutually exclusive with `-i/--input`. Consumed by `FirstPassFDR` / `PerFileRescoring` / `SecondPassFDR` and the `--input-scores`-only full run. |
| `-i/--input <mzML...>` | none | Required by `PerFileScoring` and the default full pipeline; forbidden by `FirstPassFDR` / `PerFileRescoring` / `SecondPassFDR`. |
| `-l/--library`, `-o/--output` | none | Required by `FirstPassFDR`, `PerFileRescoring`, and `SecondPassFDR`. `--output` is accepted-but-unused by `--task PerFileScoring` (writes per-file parquets, not a blib). |
| `--reconciliation-compaction-fdr <v>` | 0.01 | Peptide-q gate for Stage 5 compaction (`FirstPassFdrTask.cs:689`). |
| `--protein-fdr <v>` | 0.01 (machinery always runs) | Protein-rescue gate for compaction (`EffectiveProteinFdr`, `FirstPassFdrTask.cs:693`) and the SecondPassFDR passing-group count. |
| `Reconciliation.Enabled` (config) | true | Gates Stage 6 planning (`FirstPassFdrTask.cs:387`) and the `--task FirstPassFDR` requirement (`Program.cs:395-398`). |
| `--parallel-files [N]` | sequential (off) | OUTER parallelism: number of input files scored/rescored at once. Both Stage 1-4 scoring and Stage 6 rescore fan out under the same resolved `EffectiveFileParallelism` (`RunPlan.cs:47`). Auto when the flag is given with no value. |
| `--threads <count>` | all cores | INNER per-file main-search thread budget, divided across concurrently running files. |
| `--work-dir` / `--output-dir` / `--cache-dir` | input file's own dir | Redirect where per-file artifacts (parquet, spectra, calibration, sidecars) are written (`ArtifactPaths`, `Program.cs:135-136`). |
| `OSPREY_MAX_PARALLEL_FILES` (env) | unset | Back-compat cap on the resolved concurrent-file count (`FileParallelismResolver`). |
| `OSPREY_DUMP_*` / `OSPREY_*_ONLY` (env) | unset | Env-var-gated cross-impl bisection dumps (see above); `_ONLY` exits after writing. Zero overhead when unset. |
| Retired: `--no-join`, `--join-only`, `--join-at-pass=N`, `--parquet-compression` | — | Not recognized by the C# CLI; any such token fails fast in `ParseArgs` (`Program.cs:83-85`). |

## Divergences from the Rust documentation

- **[INTENTIONAL-CSHARP-DESIGN] CLI surface is `--task <Name>`, not `--no-join` / `--join-at-pass` / `--join-only`** - Rust doc says the HPC split is orchestrated by `--join-at-pass=<N>` with `--no-join` / `--join-only` modifiers; C# retired those flags and uses a single `--task {PerFileScoring|FirstPassFDR|PerFileRescoring|SecondPassFDR}` selector that derives the `NoJoin` / `StopAfterStage5` / `ExpectReconciledInput` membership flags. The unrecognized Rust flags fail fast. Evidence: `Osprey/Program.cs:86-128`, `Osprey/OspreyCommandArgs.cs:206-207`. Severity: major.

- **[INTENTIONAL-CSHARP-DESIGN] One name per task, describing the FDR pass** - The CLI name, the `HpcTask` member, the task class, the `[TASK]` log token, and the `.osprey.task` stamp are all one string per task, describing the FDR pass rather than the join topology. Two of them used to describe the topology instead (`FirstJoinTask`/`FirstPassFDR` and `MergeNodeTask`/`SecondPassFDR`), which cost a reader a mapping table and once produced a resume leg that keyed off the class names, matched zero sidecars, and passed green having resumed nothing; issue #4535 renamed them. The residual mapping is `PerFileRescoring` vs `PerFileRescore`, plus the `Fdr`/`FDR` casing that follows this codebase's type convention (`FdrEntry`, `FdrController`) rather than the all-caps `pwiz.Osprey.FDR` namespace. Folding those two in as well would let `ResolveTask` and `TaskCliName` be deleted outright. Evidence: the `HpcTask` enum in `Osprey.Core/OspreyConfig.cs`, `ResolveTask` / `TaskCliName` in `Osprey/Program.cs`. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Stage 6 writes a separate `.scores-reconciled.parquet`, not an in-place rewrite** - Rust doc says Stage 6 "rewrites each `<stem>.scores.parquet`" with reconciled scores; C# writes a separate `<stem>.scores-reconciled.parquet` sibling and leaves the Stage 4 parquet intact (crash-safety: a partial Stage 6 crash cannot half-rewrite the Stage 4 output). `--input-scores` directory resolution then prefers the reconciled sibling per stem. Evidence: `Osprey.Tasks/PerFileRescoreTask.cs:163-177,944-954`, `Osprey/Program.cs:459-472`. Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] Orchestration is membership-predicate + lazy-rehydrate, not a stage window** - Rust doc frames each mode as "run stages X through Y, load the rest from disk"; C# implements a fixed four-task canonical pipeline where each task's `IsIncluded` decides participation and excluded/valid tasks lazy-rehydrate their state on demand through the typed byproduct registry. Behavior/outputs match the Rust modes (pinned by the membership truth table). Evidence: `Osprey/AnalysisPipeline.cs:99-148`, `Osprey.Test/PipelineMembershipTest.cs:55-93`. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] No `--parquet-compression` flag; C# writes ZSTD** - Rust doc documents `--parquet-compression snappy` on the scoring step for OspreySharp interop (Parquet.Net 3.x Snappy-only). The C# port has no such flag; its `ParquetScoreCache` writer uses `CompressionMethod.Zstd` unconditionally and reading auto-dispatches on per-column-chunk metadata. Cross-impl ZSTD/Snappy read compatibility is tracked as follow-up. Evidence: `Osprey.IO/ParquetScoreCache.cs:270,462`, `Osprey.Tasks/PerFileScoringTask.cs:1481`. Severity: minor.

- **[FLAG-GATED] `OSPREY_DUMP_PREDICT_RT` is declared but disabled** - Rust doc lists `OSPREY_DUMP_PREDICT_RT` among the Stage 6 worker bisection dumps; the C# interface declares `DumpPredictRt`, but its per-file call site in the rescore loop is commented out as a scoring hotspot, so the dump produces nothing today. Evidence: `Osprey.Diagnostics/IOspreyDiagnostics.cs:80`, `Osprey.Tasks/PerFileRescoreTask.cs:715-726`. Severity: minor.

- **[STALE-RUST-DOC] Stage 4 parquet footer omits `osprey.reconciliation_hash`** - Rust doc's hash table lists `osprey.reconciliation_hash` as parquet footer metadata generally; in C# the Stage 4 `.scores.parquet` footer carries only `version` / `search_hash` / `library_hash` / `reconciled = "false"`, and `reconciliation_hash` is written **only** on the Stage 6 reconciled parquet. This matches the semantic intent (the hash is meaningful only post-reconciliation) but the field is not present on every parquet. Evidence: `Osprey.Tasks/PerFileScoringTask.cs:226-232` vs `Osprey.Tasks/ReconciledParquetWriter.cs:198-205`. Severity: info.

Verified as matching the Rust doc: the four-phase split (per-file scoring / FirstPassFDR / per-file rescore / SecondPassFDR) and which stages run vs load-from-disk in each mode; the boundary file pair (`<stem>.1st-pass.fdr_scores.bin` + `<stem>.reconciliation.json`); the SHA-256 footer-hash validation (version / search_hash / library_hash / reconciled) aborting early with a file-named error; the `--task SecondPassFDR` strict `reconciled = "true"` gate; the `--input-scores` directory being scanned non-recursively; the mutual-exclusion validation errors (`Program.ValidateArgs`); the reconciliation.json carrying `search_hash`/`library_hash`; the copy-and-verify safe-write pattern; and the env-var-gated cross-impl bisection dumps.
