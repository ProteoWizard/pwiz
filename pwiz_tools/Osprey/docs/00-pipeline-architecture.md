# 00. Pipeline Architecture and the Sidecar File Contract (C#)

> Pipeline stage: Cross-cutting (all stages). Unlike docs 01-20 this document has no
> Rust counterpart and no "Divergences" section: it states the architecture the C#
> pipeline is built to, which the port established rather than inherited.

Osprey is designed to search a 500-run experiment on a 64 GB machine, and to split
that same search across an HPC cluster at any batch size from one run per node to the
whole cohort in one process, with identical output. Neither property comes from an
algorithm. Both come from a small set of rules about **what a task may hold in memory**
and **what a task may write to disk** - rules that are invisible in any single stage
document because every stage obeys them.

This document states those rules once. It covers the shape of the pipeline (four tasks
over seven stages, alternating fan-out and join), the two tiers of sidecar file
(per-run and experiment-wide) and what each tier is for, and the write and resume
discipline that lets a run be interrupted, moved, adopted or distributed without ever
producing a subtly wrong answer.

Start here before changing what any task reads, writes, or keeps.

For the per-stage algorithms, see the ordered index in [README.md](README.md).

---

## Scope of this document

Three documents divide the file-and-orchestration subject between them. Each owns its
layer, and none repeats another:

| Doc | Owns | Answers |
|---|---|---|
| **00** (this doc) | Scope, contract, principles, relay | Which file, whose, when, and who may read it |
| [14-intermediate-files](14-intermediate-files.md) | Bytes | Headers, versions, schemas, hashing, invalidation mechanics |
| [15-hpc-scoring-split](15-hpc-scoring-split.md) | Operations | CLI flags, `--input-scores` ordering, orchestration recipes |

If you are asking "what does this file's header look like?", you want 14. "How do I
launch the third worker?" is 15. "Is this task allowed to read that file?" is here.

**How we work on Osprey** - test gates, datasets, machine paths, environment
variables, run layout - is not documentation of the code and lives in `ai/docs`,
outside this repository's `docs/` tree.

---

## Runs and files

Throughout this document a **run** is one MS data file: one mzML or one vendor raw
file, one LC-MS/MS acquisition. It is the unit the pipeline fans out over.

The code spells this concept `file`, because a run arrives as a file and is keyed by
its file stem. The two fan-out tasks are named `PerFileScoring` and `PerFileRescoring`
in the CLI, the `HpcTask` enum, and the `.osprey.task` sidecar names, and this document
uses those spellings whenever it names a task, a type, or a path. It says **run** in
prose, where "per-run" and "experiment-wide" is the distinction that carries the
architecture and "per-file" invites confusion with the dozen other files in play. The
`--model-diagnostics` report uses the same prose convention.

One run, one stem, one set of per-run sidecars. A cohort of 500 runs is 500 stems.

---

## Scale targets and the deployment model

### 500 runs on 64 GB

The binding constraint on a large Osprey search is **memory, not wall clock**. A
cohort that does not fit fails; a cohort that fits but is slow still finishes. So the
target is stated as a resident-memory ceiling at a cohort size, and every design
question below resolves in favour of bounding memory.

The concrete target is a 500-run experiment completing on a 64 GB machine. That number
sets the whole architecture: at 500 runs, any structure that allocates per run times
per library entry is fatal, and no amount of available RAM changes that - it is a
scaling shape, not a constant factor.

### Three execution modes, one output

The same pipeline runs three ways, and all three must produce identical output:

| Mode | Shape | What it is for |
|---|---|---|
| **Single process** | All four tasks in one process, no `--task` | Ordinary searches; the default |
| **HPC split** | One `--task` per node, any batch size | Cohorts too large or too slow for one machine |
| **Resumed** | Re-invoke the same command line | A crashed, killed, or deliberately staged run |

These are not three code paths. They are the same four tasks with different subsets
included in the driver loop, reading the same sidecar files from disk. A single-process
run writes every file an HPC run writes; that is what makes the HPC mode testable
without a cluster, and why regression mode 3 can assert that a per-node reconciled
parquet is byte-identical to the one the straight-through run produced.

### What "bounded" means

Bounded means the peak resident set does not grow with the number of runs. Concretely,
a structure is admissible when its size is:

- **O(library)** - one entry per library precursor. Fixed by the library, not the cohort.
- **O(entries in one run)** - the working set of a single run, held only while that run
  is being processed.
- **O(distinct entries)** - one entry per distinct library precursor surviving across the
  whole experiment. Grows with library and discovery depth, not with run count.

And inadmissible when it is:

- **O(runs x entries)** - a per-run collection of per-entry data, all resident at once.

That last shape is the single failure mode this architecture exists to prevent. It
appears innocently: a dictionary keyed by file name whose values are per-entry lists, a
map built once "for convenience" outside a per-run loop, a join that materialises every
run's rows to compute a summary over them. At 3 runs it is invisible. At 500 it is the
whole 64 GB.

---

## The task graph

### Four tasks over seven stages

The pipeline is a fixed, four-element list, always in this order
(`AnalysisPipeline.CanonicalPipeline()`). It alternates fan-out and join:

| Task | Stages | Shape | Nodes | May hold resident |
|---|---|---|---|---|
| `PerFileScoring` | 1-4 | fan-out (split 1) | 1..N | library + one run |
| `FirstPassFDR` | 5 | **join** (barrier 1) | 1 | O(distinct entries) |
| `PerFileRescoring` | 6 | fan-out (split 2) | 1..N | baseline + one run |
| `SecondPassFDR` | 7 | **join** (barrier 2) | 1 | O(survivors) |

Stages 1-4 are library preparation, mzML processing, calibration, and the main
first-pass search that computes the 21 PIN features. Stage 5 is first-pass FDR plus the
Stage 6 reconciliation plan. Stage 6 is the per-run rescore and gap-fill. Stage 7 is
second-pass FDR, protein FDR, and the `.blib` write. The stage-to-document map is in
[README.md](README.md); the task-name-to-enum-to-class map is in
[15-hpc-scoring-split](15-hpc-scoring-split.md).

A fan-out task's node count is free. One node per run, five runs per node, or all 500
in one process are the same computation, and the pipeline does not know which it is
doing. That freedom is the point of the whole design, and every principle in the next
section exists to preserve it.

### Two selectable tasks that are not pipeline tasks

The `HpcTask` enum has six members, but only the four above are pipeline stages.
`AnalysisPipeline.CanonicalPipeline()` contains those four and nothing else; the other
two are reachable only by naming them in `--task`, and neither participates in a run
that does not:

- **`--task SpectraCache`** builds each input's `.spectra.bin` and stops. It is the
  data-staging step *ahead* of the pipeline, not a node within it, which is why it needs
  no `--library` and publishes no byproducts - caching depends only on the input file.
  It exists because staging a cohort would otherwise mean running all of Stages 1-4 and
  paying for calibration, scoring and a parquet write that staging does not need. The
  caches it writes are byte-for-byte what a full run would have written.
- **`--task ModelDiagnostics`** regenerates the `--model-diagnostics` HTML for a
  completed analysis from that run's own outputs, suppressing every artifact write
  except the report. It exists so that judging a diagnostics change on a large cohort
  does not mean re-running the search - seven hours on the 82-run SEA-AD set - or
  accepting a page written by an older build.

Both are appended after the four pipeline members so the existing ordinal values are
undisturbed. Neither is a fan-out node, and neither belongs in an HPC relay plan.

### Scatter and barrier: what a join may hold

A join is the expensive part of the pipeline and the only place a whole-experiment
structure is permitted at all. The two joins exist because two questions genuinely
cannot be answered one run at a time:

- **`FirstPassFDR`** trains one SVM over all runs' evidence and computes
  experiment-wide q-values, then plans reconciliation - which requires knowing where
  every other run found the same precursor.
- **`SecondPassFDR`** competes precursors experiment-wide, runs protein parsimony and
  picked-protein FDR, and writes the single `.blib`.

Everything else is per-run work and belongs in a fan-out task. When a join grows a new
responsibility, that is the signal to ask whether the work decomposes - not to add
another dictionary keyed by file name. A join is the bottleneck of an HPC run by
construction: it is the one node everything waits for, and the one node that cannot be
made smaller by adding hardware.

### The per-run loop, and where the memory goes

This is the shape every fan-out task has, and the reason batch size is free. Taking
`PerFileRescoring` on one node handed a batch of runs `{r1 .. rk}`:

```
    load once, before the loop            RESIDENT BASELINE
      library + entry-id index              O(library)
      experiment-wide sidecars              O(distinct entries)
    ..................................................  <- floor: never freed,
                                                           never grows with k or N

    for each run r in the batch:          PER-RUN WORKING SET
        load   r.scores.parquet               |
        load   r.1st-pass.fdr_scores.bin      |
        load   r.reconciliation.json          |  O(entries in r)
        load   r.calibration.json             |
        load   r.spectra.bin (windowed)       |
              ---- rescore r ----             |
        write  r.scores-reconciled.parquet    |
        free   everything loaded for r        v
    end for

    peak resident = baseline + max over r of (working set of r)
                  = O(library) + O(entries in one run)
```

Read the last line carefully: **k does not appear, and neither does N**. That identity
is the contract. A node handed one run and a node handed a hundred have the same peak,
so the orchestrator may batch however it likes, and a cohort that fits at 3 runs fits
at 500.

The identity holds only while both halves hold: the baseline must be bounded by the
library rather than the cohort, and the per-run working set must actually be freed at
the end of each iteration. A single collection that accumulates across iterations -
even one holding just a few values per entry - converts the sum into O(runs x entries)
and silently reintroduces the wall.

> **In flight** - `PerFileRescoring`'s baseline does not yet meet this contract:
> `RescorePassInputs` still carries several maps keyed by file name whose values are
> per-entry (consensus targets, reconciliation targets, per-run calibrations, gap-fill
> targets), and the task is entered with a materialised all-runs entry list. Reducing
> the baseline to the bounded set above is the target shape in
> ai/todos/active/TODO-20260901_osprey_stage5_reload_materialization.md.

### Phase boundaries inside a task

A task is not the unit of persistence. `FirstPassFDR` alone passes through model
training, a scoring pass over every run, the experiment-wide barrier, a second pass,
protein FDR, and Stage 6 planning - and it writes its products at the end of the phase
that computes them, not at the end of the task.

This matters for two reasons. It bounds crash exposure to a single in-flight file
regardless of how large the cohort is, and it is what makes a phase's output available
to a downstream task that starts mid-pipeline. A task that persisted only at its own
end would force a resumed run to redo every phase it had already completed.

---

## Principles

Fifteen rules, in four groups. The first group makes the pipeline decomposable, the
second bounds its memory, the third makes its files trustworthy, and the fourth makes
an interrupted run resumable. Each is stated as a rule with the reason it exists,
because a rule whose reason is not recorded gets optimised away.

### Decomposition: what makes HPC possible

**P1. Every durable artifact is either per-run or experiment-wide.** There is no third
tier, and an artifact that fits neither is a design error rather than a special case.
Scope is a statement about *content* - whose data is in the file - and it is what the
memory model in P5 and P6 is built on.

Scope is not the same question as naming, and the two must not be conflated. Most
per-run content is named for its run stem and most experiment-wide content is named for
the analysis, but an experiment-wide payload small enough to duplicate may be
*replicated* under every run stem so a fan-out node can find it by the one derivation it
already knows. `<stem>.1st-pass.model.json` is exactly that: identical bytes beside
every run, any one copy authoritative. It is experiment-wide content, and the memory
model counts it as baseline, whatever its name says.

**P2. A per-run iteration reads only its own run's artifacts plus experiment-wide
summaries written by an earlier phase, and builds nothing spanning runs.** This is the
rule that makes batch size free. An iteration that reaches for a second run's file has
made the node's correctness depend on which runs it was handed, and the orchestrator
can no longer split the cohort arbitrarily.

**P3. Work goes in the fan-out tasks; a join holds O(distinct), never O(runs x
entries).** The joins are the bottleneck - one node, no horizontal scaling - so work
placed there is paid at full cost by every cohort. Moving work into a `PerFile*` task
makes it scale with the cluster; leaving it in a join makes it scale with nothing.

**P4. A per-run artifact's validity key must not name the cohort.** `PerFileScoring`'s
key is search parameters plus library identity, and deliberately omits the file set,
because a run's Stage 1-4 scores do not depend on which other runs are being searched.
Were the cohort in that key, scoring one run per node would stamp a different key than
the join expects, and every artifact would invalidate on rebatching - the HPC split
would be defeated by its own bookkeeping. Contrast `ReconciliationParameterHash`, which
*does* fold in the sorted file stems, correctly: reconciliation output genuinely depends
on the cohort.

### The two tiers: where the memory model lives

**P5. The experiment-wide artifacts are the memory baseline of a fan-out task.** They
are loaded once before the per-run loop and held for its whole duration, so their
combined size is the floor under the task's resident set. This is the reason
experiment-wide artifacts must be *summaries* - O(library) or O(distinct entries) - and
never a concatenation of per-run data. An experiment-wide file that grows with the
cohort puts the cohort in the floor.

**P6. Per-run artifacts are loaded and freed inside one iteration.** Nothing a run's
processing allocates may outlive its iteration. The peak is then
`baseline + max(one run)`, independent of batch size and of cohort size, which is the
identity in the figure above and the property the 64 GB target rests on.

**P7. Persist at phase end, not task end.** Crash exposure is one in-flight file no
matter how large the cohort, and a downstream task starting mid-pipeline finds each
phase's product already on disk.

### Write discipline: what makes a file trustworthy

**P8. Every durable artifact commits through `FileSaver`, so presence proves
completeness.** The writer stages into a sibling temp file in the same directory and
promotes it with an in-volume rename; a crash mid-write leaves the previous content or
no file, never a half-written one. Every consumer in the pipeline relies on this: a
file that exists is a file that is whole, so no reader needs a completeness check, a
length prefix, or a two-phase protocol.

**P9. A validity key answers set inclusion, not completeness.** This follows from P8
and is the most easily confused point in the design. Because atomic placement already
guarantees the bytes are whole, the key never asks "did the writer finish?" - it asks
only **"was this computed under the same software version, library, parameters and file
set as the run now asking?"** Those are orthogonal questions with orthogonal mechanisms:
existence answers completeness, the key answers membership. `PerFileResumeDriver.IsCurrent`
tests both, because a sidecar can outlive its output.

**P10. A question one phase asks about another phase's work is answered by an artifact,
never by process state.** Under an HPC split the phases are separate processes, so any
signal held in memory is simply absent on the next node - and a task that infers from
its own emptiness infers wrongly. This is why the `.osprey.task` stamp records the
producing task's name: a later phase can ask "who wrote this, and is it current?"
without opening the artifact. The rule is written in blood. Stage 7 once decided whether
the Stage 6 worker had run by checking an in-process flag; correct in one process, and
in an HPC chain it concluded no worker had run and rewrote every sidecar with survivors
only - 332,269 records where the straight-through route produced 407,624, on artifacts
whose whole purpose is to be route-independent.

**P11. Artifacts are write-once: a new column means a new file, never a revisit.** A
phase writes its product once and no later phase reopens it. Reconciled scores go to
`<stem>.scores-reconciled.parquet` rather than overwriting `<stem>.scores.parquet`,
which is what lets Stage 6 rerun without destroying its own input and lets two arms
share one Stage 1-4 result.

> **In flight** - `FdrScoresSidecar.PatchProteinQvalues` still rewrites an existing
> first-pass sidecar in place to add protein q-values. Splitting that column into its
> own experiment-wide file, written by the phase that computes it, is tracked in
> ai/todos/active/TODO-20260901_osprey_firstpassfdr_resume.md.

**P12. A column lives in the file written by the phase that computes it.** The
corollary of P11 that decides where a *new* column goes. Adding a column to an existing
file means either revisiting it (forbidden) or delaying its writer until the later value
is known (which breaks P7).

**P13. Never conditionally write an output artifact.** A phase that has nothing to say
writes the header-only or zero-record file rather than skipping the write. Otherwise
absence is ambiguous - "nothing to report" and "this phase never ran" look identical on
disk - and P8's guarantee that presence proves completeness has no counterpart for
absence.

**P14. When a phase writes several files for one run, the file whose presence gates
downstream reuse lands last.** `FileSaver` makes each individual write atomic; it says
nothing about a *set* of writes. So the set needs an ordering discipline, and the rule
is that the gate file is written after everything it implies. `PerFileRescoring` writes
`<stem>.2nd-pass.fdr_decoys.bin` before `<stem>.2nd-pass.fdr_scores.bin` for exactly
this reason: an interruption between them leaves a file the next phase recomputes,
rather than one it trusts and folds against a companion that was never written.

### Resume and relocation

**P15. Resume is a forward scan, and identity is path-independent.** The driver walks
the stages in order, reusing each output whose sidecar key matches and recomputing from
the first that does not; a stage that runs invalidates everything downstream. Because
the scan is forward and downstream invalidation is total, a key that is not exhaustive
enough costs a recompute, never a wrong answer - which is why "I cannot tell" resolves
to re-run.

Identity is also deliberately free of paths: the library hash uses file name, size and
mtime with the directory excluded, and the reconciliation hash names sorted file
*stems*. A completed run can therefore be moved, copied, or hard-linked into a new
output directory and still be recognised as valid - the property external tooling relies
on to adopt a prior run's Stage 1-4 artifacts instead of recomputing them for hours.

---

## The sidecar file contract

This is the authoritative list of what the pipeline writes, who may read it, and what
must travel with what. Every row was verified against the path-building code named in
its prose; where this table and a stage document disagree, this table is the one that
was checked.

### Reading the table

Two axes classify every artifact, and both matter:

- **Scope** - per-run or experiment-wide (P1). Content, not naming.
- **Kind** - **product** or **cache**. A product is a phase's output and the only copy
  of what it says; a cache is derivable again from a source that still exists. Products
  go to `--output-dir`, caches to `--work-dir`. The distinction is not academic: a
  cache may be deleted to reclaim disk, and a product may not.

The **Relay** column is the HPC instruction: what an orchestrator must place on a node
before that node's task can run. "with the run" means the file travels alongside its own
run's other artifacts to whichever node processes that run. "every node" means every
node running that task needs a copy, whatever batch it was handed.

| File | Scope / kind | Written by | Read by | Relay |
|---|---|---|---|---|
| `<library-leaf>.libcache` | experiment cache | library load, any task | all tasks | rebuild locally |
| `<stem>.spectra.bin` | per-run cache | `PerFileScoring` (Stage 2) | `PerFileScoring`, `PerFileRescoring` | with the run |
| `<stem>.calibration.json` | per-run product | `PerFileScoring` (Stage 3) | `PerFileRescoring` | with the run |
| `<stem>.scores.parquet` | per-run product | `PerFileScoring` (Stage 4) | `FirstPassFDR`, `PerFileRescoring` | with the run |
| `<stem>.1st-pass.fdr_scores.bin` | per-run product | `FirstPassFDR` (pass 1) | `PerFileRescoring`, `SecondPassFDR` | with the run |
| `<stem>.reconciliation.json` | per-run product | `FirstPassFDR` (Stage 6 planning) | `PerFileRescoring` | with the run |
| `<blib-stem>.1st-pass.fdr_experiment.bin` | experiment product | `FirstPassFDR` | `PerFileRescoring`, `SecondPassFDR` | **every node** |
| `<stem>.1st-pass.model.json` | experiment product, replicated | `FirstPassFDR` (training) | `PerFileRescoring`, `SecondPassFDR` | **every node** (any one copy) |
| `<stem>.scores-reconciled.parquet` | per-run product | `PerFileRescoring` (Stage 6) | `SecondPassFDR` | with the run |
| `<stem>.2nd-pass.fdr_decoys.bin` | per-run product | `PerFileRescoring` (pass-2 worker) | `SecondPassFDR` | with the run |
| `<stem>.2nd-pass.fdr_scores.bin` | per-run product | `PerFileRescoring` (pass-2 worker), else `SecondPassFDR` | `SecondPassFDR` | with the run |
| `<blib-stem>.2nd-pass.fdr_experiment.bin` | experiment product | `SecondPassFDR` | terminal | n/a |
| `<output>.<TaskName>.osprey.task` | scope of its artifact | every task, via `PerFileResumeDriver` | the driver | with its artifact |
| `<output>.blib` | experiment product (terminal) | `SecondPassFDR` | Skyline | n/a |

Nothing else is part of this contract. Diagnostic outputs - the `--fdrbench` pairing
manifests, the `--model-diagnostics` HTML and JSON, the peptide-trace dumps - are
side channels: no task reads them, nothing relays them, and removing one changes no
result. Do not add a pipeline dependency on any of them.

### Rows that are not what they look like

**`<stem>.spectra.bin` is a cache until it is not.** It is rebuildable by re-parsing the
source, which makes it a cache. But the supported way to halve a large cohort's disk is
to stage the raw data, build the caches, and delete the sources - and after that this
file is the only copy of the run, and deleting it loses the run. Prune spectra caches
only where the source is still on disk.

**`<stem>.1st-pass.model.json` is experiment-wide content under a per-run name.** The
trained first-pass Percolator model is identical for every run, and a `SecondPassFDR` or
`PerFileRescoring` node running a frozen pass-2 mode needs it. Writing it once under the
analysis name would force every fan-out node to learn a second path convention; writing
it beside each run's other sidecars means a node finds it by the same stem derivation it
already uses. `LoadFromAny` takes the first copy that exists, because the copies are
identical.

**The protein-compact stratum has no file of its own.** It rides inside
`<stem>.1st-pass.model.json` as an optional field, present only under
`OSPREY_PASS2_QVALUE=protein-compact`. This is deliberate and worth not undoing: the
mode needs both the frozen model and the stratum, they are produced by the same
first-pass span, and a node holding one without the other can do nothing. One artifact
means one relay hop and one reload site, and makes it impossible to ship half of what
the mode requires.

**`<stem>.2nd-pass.fdr_scores.bin` has two possible writers, and the sidecar says
which.** When `PerFileRescoring` runs the pass-2 per-file worker it writes this file and
stamps the validity sidecar under its *own* task and key - the artifact belongs to the
task that produced it, so it must be invalidated by whatever invalidates that task.
`SecondPassFDR` then reads the stamp to decide whether to fold the worker's file or
recompute. Stamping `SecondPassFDR`'s key at production time would leave a file that
outlives the inputs it was computed from, which is the one staleness a resume cannot
detect by looking. Where no worker ran, `SecondPassFDR` writes the file itself.

**`<output>.<TaskName>.osprey.task` is per-artifact, not per-task.** One sidecar is
written next to each output, and its name carries the producing task so two tasks
sharing an output path cannot trample each other's record. Reuse requires both the
output to exist *and* its sidecar key to match: a sidecar can outlive its output, so a
matching key alone is not enough (P8 and P9 are separate tests, and
`PerFileResumeDriver.IsCurrent` runs both).

### Where experiment-wide artifacts live

An experiment-wide product takes its **name** from the output blib and its **directory**
from the same resolution the per-run sidecars use - `ArtifactPaths.ResolveOutputDir` off
any input or scores-parquet path of the analysis. Both halves are deliberate, and
getting either wrong breaks the HPC chain rather than the single-process run:

- The **name** must come from the blib because the blib is what names the analysis, and
  two analyses sharing an output directory must not collide.
- The **directory** must not come from the blib, because the blib's directory is not
  stable across phases. Every distributed `--task` invocation runs in its own working
  directory with the same relative `-o output.blib`, so a file placed beside the blib is
  written by one phase into a directory the next phase never looks in.

That is not a hypothetical failure. The first version of this resolution did place the
file beside the blib, and the HPC leg of the regression gate caught it: the second phase
wrote its first-pass experiment sidecar into its own phase directory, and the third
phase looked beside its own blib and found nothing.

### Artifacts that must relay together

Two experiment-wide artifacts answer questions a node cannot answer alone, and shipping
one without the other produces a node that either fails fast or - worse - proceeds on a
default:

- `<blib-stem>.1st-pass.fdr_experiment.bin` carries the experiment-scope q-values every
  downstream pass reads. A `PerFileRescoring` or `SecondPassFDR` node without it cannot
  reconstruct them, because they are a function of all runs.
- `<stem>.1st-pass.model.json` carries the frozen model, and under `protein-compact` the
  stratum with it. A distributed `SecondPassFDR` node that never trained pass 1 has no
  other source for either.

Ship both to every node running either task. A node that has one and not the other is
the case the fail-fast exists for, and it is the orchestrator's job never to create it.

