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
its file stem. The two fan-out tasks are `PerFileScoring` and `PerFileRescoring` in the
CLI and in the `.osprey.task` sidecar names; the `HpcTask` enum spells three of the four
differently (`FirstPassFdr`, `PerFileRescore`, `SecondPassFdr`), so a name copied from the
enum will not match a filename. Stage 6's canonical name is "Per-file rescore".
**Proper names are quoted as they are spelled** -
tasks, types, paths, stage names - and this document says **run** everywhere else,
because "per-run versus experiment-wide" is the distinction that carries the
architecture and "per-file" invites confusion with the dozen other files in play. The
`--model-diagnostics` report and the workflow diagram's role labels use the same
convention.

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
run writes every file an HPC run writes, which is what makes the HPC mode testable without
a cluster.

**`regression.ps1` mode 3 is the enforcement of this section and of the relay checklist at
the end of this document**, not an anecdote about it: it runs the split as separate
processes, stages the files the checklist names, and compares the split run's products
against the straight-through run's. Read what it actually asserts before relying on it: the
per-run FDR sidecars and the `.blib` are compared at the run tolerance, and only
`*.fdr_experiment.bin` byte for byte. It does **not** open the reconciled parquets, so a
Stage 6 content change can leave mode 3 green. A relay obligation mode 3 does not stage is
an obligation nothing is checking - and `<blib-stem>.1st-pass.model-diagnostics.json` is
currently one of those.

Its one blind spot is worth stating, because it has already hidden a defect: a phase-3 node
is handed **one** run, and at N=1 a correct per-run implementation and an all-runs one
produce identical bytes, so the mode stayed green while the per-run path was not running at
all. **Where a contract cannot be distinguished by output at N=1, the gate must assert the
path** - a marker line the run emits, checked as a string - rather than trusting the bytes
to reveal it.

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

### Visited is not resident

Those three shapes describe what is **resident**. A separate question is how many runs a
computation **visits**, and conflating the two leaves no vocabulary for the shape that
does most of the work in the joins: a **fold over a stream** - visit every run in turn,
hold one at a time, and accumulate a bounded summary.

That is a third shape, and it is why a join is not automatically expensive. Every
genuinely whole-experiment computation named in this document is one of these folds: the
best-of-runs experiment-q floor, protein parsimony, the pre-blib q re-clamp. The code says
so where it matters - "both maps are O(distinct) ... nothing here needs a whole-run view" -
and `StreamingFdr.StreamingFirstPassQ` is the worked example, pinned against its resident
twin by a test.

Read the vocabulary this way:

| | Visits | Holds |
|---|---|---|
| Fan-out iteration | one run | one run |
| Fold over a stream | every run | a bounded summary |
| Resident whole-experiment structure | every run | every run - **inadmissible** |

The distinction matters because "it stays in the join" and "it may be resident" are
different permissions, and the gap between them is where an all-runs pre-pass can grow
without violating any rule stated in terms of fan-out versus join alone.


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
- **`--task ModelDiagnostics`** renders the `--model-diagnostics` HTML from a completed
  analysis's own diagnostics products. It **processes nothing**: no pool is constructed,
  no Percolator runs, and no artifact but the page is written. It exists so that judging
  a diagnostics change on a large cohort does not mean re-running the search - seven hours
  on the 82-run SEA-AD set - or accepting a page written by an older build.

  Three states, and the first two are the reason it is a state machine rather than a
  write: with no completed first pass it **aborts** (the `.1st-pass.model.json` precedent
  below, not the `fdr_experiment.bin` one that continues into a wrong answer); with a first
  pass but no second it renders the first-pass page and says so **in the page**, because an
  absent pass-2 section is otherwise indistinguishable from a complete report of a run that
  found nothing in pass 2; with both, it renders both.

  Suppressing the writes was never the same as suppressing the work. Before the diagnostics
  products were retained, this task fell through to the ordinary pipeline with
  `DiagnosticsOnly` merely silencing its writers, so it still built the whole-run survivor
  pool - asking a 446-run analysis to describe itself cost what running it did, and met the
  same memory wall.

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

The converse of the replication below is the shape to watch for: **a per-run artifact
carrying an analysis-wide payload.** `<stem>.reconciliation.json` does this today - every
one of its N copies restates the same join-wide `first_pass_base_ids` array, which at 446
runs is 2.79 GB of pure duplication inside 10.7 GB of envelopes that a fan-out worker then
has to parse to rebuild a union it could have been handed. Replication is only benign when
the payload is small and fixed, as the frozen model is; when it scales with the experiment
it belongs in one experiment-wide artifact, and the fan-out reads that instead. This is the
P6 startup rule seen from the writer's side.

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
key is the base key - search parameters, library identity, and the peak-pick arm - and
deliberately omits the file set, because a run's Stage 1-4 scores do not depend on which
other runs are being searched.
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

**P6. Per-run artifacts are loaded and freed inside one iteration, and the task's startup
is independent of batch size in wall clock as well as memory.** Nothing a run's processing
allocates may outlive its iteration, so the peak is `baseline + max(one run)`, independent
of batch size and of cohort size - the identity in the figure above, and the property the
64 GB target rests on.

That identity alone is not the whole requirement, because it constrains only the *peak*. A
worker that opens all 446 `reconciliation.json` envelopes to build a union, then frees them
before the loop starts, never exceeds the peak and has still done O(runs) work before
rescoring its first run. That is not hypothetical: it shipped, as 8m42s and 17.2 GB of
startup on an 86-run plate - work the rescore loop then discarded and redid. So the rule is
both terms: **no fan-out task may have a loading phase whose cost grows with the number of
runs it was handed.** Where such a task needs a cohort-wide fact, an earlier phase computes
it once and writes it (P12) and the fan-out reads that bounded summary - it never conducts
its own survey of the batch.

**P7. Persist at phase end, not task end.** Crash exposure is one in-flight file no
matter how large the cohort, and a downstream task starting mid-pipeline finds each
phase's product already on disk.

**Killing the process is a SUPPORTED OPERATION, not an accident to be survived.** A
500-file search runs for a day or more, on a machine its owner needs for other things.
They must be able to stop it whenever they like, for any reason, without knowing which
moment is safe and without losing more than the file in flight. "Do not interrupt between
X and Y" is not a constraint this pipeline may impose, because nobody could honour it -
and a user who believes stopping is risky will instead not run the analysis at all.

That guarantee holds only if **a file's outputs become valid TOGETHER**. Where one file
has several products, a resume must not be able to read one of them as done and another as
outstanding, because a kill lands between the two writes eventually - and at 446 files it
lands there roughly once per run.

This is not hypothetical. On 2026-09-04 a native fault killed a 446-file run between a
run's reconciled-parquet write and its 2nd-pass sidecar write. The resume then counted that
file as outstanding (the cohort count reads the sidecar) and skipped it as complete (the
per-file check read the parquet's stamp) four log lines later: 448 "skipping (outputs
valid)" lines, zero re-scores, and a .blib silently missing a run. Two notions of "done"
is the defect, so the fix is one predicate, not a second check.

The gate leg for this is mode 9 in `regression.ps1`, which cuts ONLY the later product and
asserts the file is re-scored. Note why the pre-existing mode 8 could not catch it: it
amputates BOTH products, which puts the two checks back into agreement - an interruption
test has to leave the state an interruption actually leaves, not a tidier one.

### Write discipline: what makes a file trustworthy

**P8. Every durable artifact commits through `FileSaver`, so presence proves
completeness.** The writer stages into a sibling temp file in the same directory and
promotes it with an in-volume rename; a crash mid-write leaves the previous content or
no file, never a half-written one. Every consumer in the pipeline relies on this: a
file that exists is a file that is whole, so no reader needs a completeness check, a
length prefix, or a two-phase protocol. The claim is checkable rather than aspirational -
[14-intermediate-files](14-intermediate-files.md) enumerates the call sites, and a new
durable artifact that does not appear there is a defect.

**Three artifacts do not yet obey this, and they are defects rather than exceptions.** The
Stage-7 reports `<output>.protein_groups.tsv` and `<output>.stats.tsv` are written with a
truncating `StreamWriter`, and both flags default on, so a kill during Stage 7 destroys the
previous run's report and leaves a half-written one. `--write-pin` output is the third.
None of them is in the contract table below - nothing in the pipeline reads them - but the
"presence proves completeness" guarantee a *user* draws from a report file is exactly as
strong as `FileSaver`, which is to say currently absent for these three.

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
producing task's name: a later phase can ask "who wrote this?" without opening the
artifact. This rule was learned from a defect. Stage 7 once decided whether the Stage 6
worker had run by reading a pipeline byproduct the worker published in memory; correct in
one process, and in an HPC chain - where the two are separate processes - nothing
published in Stage 6 reaches Stage 7, so it concluded no worker had run and rewrote every
sidecar with survivors only: 332,269 records where the straight-through route produced
407,624, on artifacts whose whole purpose is to be route-independent. An in-memory signal
cannot answer a question about a file that outlives the process.

The corollary governs the byproduct registry, which is how tasks pass state in-process.
Tasks do not take each other's output as constructor arguments; they `Publish` typed
byproducts and `Get` them, and a `Get` that misses lazily materialises the producing task
through its `Rehydrate` (disk-load) path. That is what lets a worker starting mid-pipeline
pull upstream state from the boundary files - but it also means one `Get` can materialise
several tasks in a chain, and that the in-process route can quietly stop resembling the
distributed one. So: **every byproduct that crosses a task boundary must have a disk
counterpart, and the in-process path should read the same artifact the distributed path
does.** Otherwise the two shapes drift, because the distributed route is forced to write
files while the in-process route just reaches backwards through a cache - and a change that
stops producing something in memory will not have stopped the consumers that rebuild it
for themselves.

**P11. Artifacts are write-once: a new column means a new file, never a revisit.** A
phase writes its product once and no later phase reopens it. Reconciled scores go to
`<stem>.scores-reconciled.parquet` rather than overwriting `<stem>.scores.parquet`,
which is what lets Stage 6 rerun without destroying its own input and lets two arms
share one Stage 1-4 result.

Write-once is fully achieved today. The per-file sidecars were once written and then
revisited - `PatchProteinQvalues` rewrote every first-pass sidecar after protein FDR, and
`PatchExperimentValues` every second-pass sidecar after the experiment competition -
because the experiment-scope columns were the only ones not knowable when a run's records
were written. Issue #4486 moved those columns into the experiment-wide sidecar and deleted
all three patch paths, so a per-file sidecar is now written exactly once on each pass and
no later stage reopens it.

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
the stages in order, reusing each output whose sidecar key matches and recomputing the
rest. Invalidation is by key rather than by cascade, and the asymmetry in the cost of
getting a key wrong is what every key component in the codebase is arguing about: an
over-inclusive key costs an unnecessary recompute, while an under-inclusive one silently
reuses an artifact computed under different settings and reports it as the new result.
Erring toward more key is therefore always the safe direction, and "I cannot tell"
resolves to re-run.

Identity is also mostly free of paths: the library hash uses file name, size and mtime
with the directory excluded, and the reconciliation hash names sorted file *stems*. Two
user-supplied paths do reach it, both noted below. A completed run can therefore be moved, copied, or hard-linked into a new
output directory and still be recognised as valid - the property external tooling relies
on to adopt a prior run's Stage 1-4 artifacts instead of recomputing them for hours.

**The exception is `--decoy-pairing-manifest`**, whose path goes into
`SearchParameterHash` verbatim and unnormalised. It is the only path anywhere in artifact
identity, so it is the only reason a move invalidates: relocate a cohort that was searched
with a pairing manifest and every artifact invalidates, because the manifest is named from
somewhere else now. Restoring the original path with a junction is the cheap fix. Anything
that adds a second path to a hash removes relocatability for every run, not just
entrapment ones.

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
| `<stem>.spectra.bin` | per-run cache | `PerFileScoring` (Stage 2), or `--task SpectraCache` | `PerFileScoring`, `PerFileRescoring` | with the run |
| `<stem>.calibration.json` | per-run product | `PerFileScoring` (Stage 3) | `PerFileScoring`, `PerFileRescoring`, `FirstPassFDR`, `SecondPassFDR` | with the run, on **every** leg |
| `<stem>.scores.parquet` | per-run product | `PerFileScoring` (Stage 4) | `FirstPassFDR`, `PerFileRescoring`, **`SecondPassFDR`** (fallback for runs with no reconciled sibling) | with the run |
| `<stem>.1st-pass.fdr_scores.bin` | per-run product | `FirstPassFDR` (pass 1) | `PerFileRescoring`; `SecondPassFDR` only under `OSPREY_PASS2_VERIFY_WORKER` or where no worker answer exists | with the run |
| `<stem>.reconciliation.json` | per-run product | `FirstPassFDR` (Stage 6 planning) | `PerFileRescoring`, `SecondPassFDR` (gap-fill entry ids) | with the run |
| `<blib-stem>.1st-pass.fdr_experiment.bin` | experiment product | `FirstPassFDR` | `PerFileRescoring`, `SecondPassFDR`, `PerFileScoring` (rehydrate) | **every node** |
| `<stem>.1st-pass.model.json` | experiment product, replicated | `FirstPassFDR` (training) | `PerFileRescoring`, `SecondPassFDR` | **every node** (any one copy) |
| `<stem>.scores-reconciled.parquet` | per-run product | `PerFileRescoring` (Stage 6) | `SecondPassFDR`, and `PerFileRescoring` itself on its per-run resume arm | with the run |
| `<stem>.2nd-pass.fdr_decoys.bin` | per-run product | `PerFileRescoring` (pass-2 worker) | `SecondPassFDR` | with the run |
| `<stem>.2nd-pass.fdr_scores.bin` | per-run product | `PerFileRescoring` (pass-2 worker), else `SecondPassFDR` | `SecondPassFDR` | with the run |
| `<blib-stem>.2nd-pass.fdr_experiment.bin` | experiment product | `SecondPassFDR` | `SecondPassFDR` on a resume | n/a |
| `<blib-stem>.1st-pass.model-diagnostics.json` | experiment product (`--model-diagnostics`) | `FirstPassFDR` (pass 1) | `SecondPassFDR`, `--task ModelDiagnostics` | **every node** running `SecondPassFDR` |
| `<blib-stem>.2nd-pass.model-diagnostics.json` | experiment product (`--model-diagnostics`) | `SecondPassFDR` (pass 2) | `--task ModelDiagnostics` | n/a |
| `<blib-stem>.model-diagnostics.html` | experiment **cache** (`--model-diagnostics`) | the render step, at the end of whichever phase or task last wrote a diagnostics JSON | terminal | n/a |
| `<output>.<TaskName>.osprey.task` | scope of its artifact | every task, via `PerFileResumeDriver` | the driver | with its artifact |

In that last row `<output>` is the **full artifact path including its extension** - `foo.scores.parquet.PerFileScoring.osprey.task` - not the blib stem it means in the rows above it. A staging glob written from the uniform reading misses every per-run stamp.
| `<output>.blib` | experiment product (terminal) | `SecondPassFDR` | Skyline | n/a |

Nothing else is part of this contract. The `--fdrbench` pairing manifests and the
peptide-trace dumps are side channels: no task reads them, nothing relays them, and
removing one changes no result. Do not add a pipeline dependency on any of them.

The `--model-diagnostics` artifacts are **not** in that category, which is easy to assume
from the flag name. `.1st-pass.model-diagnostics.json` is a real cross-task, cross-process
hand-off - `FirstPassFDR` writes the fully-built pass-1 data model to it because the pass-1
pool and trained model are gone by the time `SecondPassFDR` runs, and `SecondPassFDR` reads
it to render one page with both passes. An HPC orchestrator that omits it from the
`SecondPassFDR` node loses the pass-1 half of the report.

**The two JSONs are products; the HTML is a cache.** Each pass writes its own file when
that pass ends and never revisits the other's (P11/P12), so the pass qualifier in the name
is part of the contract: `.1st-pass.` is whole as soon as `FirstPassFDR` finishes, and the absence
of `.2nd-pass.` means the second pass has not run rather than that it had nothing to say
(P13). The page is a pure projection of whichever JSONs exist, so it is the one artifact
here that is always overwritten - re-rendering cannot lose information, which is exactly
what a cache is and a product is not.

That leaves one thing unresolved, and it is recorded here rather than glossed: the page is
**still** a declared output of `SecondPassFdrTask`. A declared output's presence is evidence
its task completed, so "the HTML is always re-rendered" and "the HTML's presence means
`SecondPassFDR` finished" cannot both hold indefinitely. The resolution is to declare
`.2nd-pass.model-diagnostics.json` in its place - completion judged by the product, not by
the view - and it is not done yet.

`.1st-pass.model-diagnostics.json` used to be a single `.model-diagnostics.data.json` that
`SecondPassFDR` **deleted once consumed**. That one line is why a finished run could not
re-render its own report, and why `--task ModelDiagnostics` re-ran the pipeline to rebuild
what it had just discarded - materialising the survivor pool to describe a cohort it was
only being asked to report on. Retaining and splitting it is what makes that task a render.

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

**The protein-compact stratum rides inside `<stem>.1st-pass.model.json`** as an optional
field, present only under `OSPREY_PASS2_QVALUE=protein-compact`. The mode needs both the
frozen model and the stratum, and a node holding one without the other can do nothing, so
one artifact meant one relay hop and one reload site.

> **In flight** - this is changing, and P12 is why: training does not compute the stratum,
> first-pass protein FDR does, so the column belongs in a file written by that phase.
> `.1st-pass.stratum.json` is a separate artifact on
> `Skyline/work/20260901_osprey_firstpass_resume` and is staged as its own relay hop by
> `regression.ps1`. When that branch lands, this becomes a fourth experiment-wide artifact
> that must relay with the other three.

**`<stem>.2nd-pass.fdr_scores.bin` has two possible writers, and the sidecar's NAME says
which.** When `PerFileRescoring` runs the pass-2 per-run worker it writes this file and
stamps the validity sidecar under its *own* task and key - the artifact belongs to the
task that produced it, so it must be invalidated by whatever invalidates that task.
Stamping `SecondPassFDR`'s key at production time would leave a file that outlives the
inputs it was computed from, which is the one staleness a resume cannot detect by looking.
Where no worker ran, `SecondPassFDR` writes the file itself.

`SecondPassFDR` then decides fold-versus-recompute on the **presence** of a stamp named
for `PerFileRescoring` - never by re-deriving that task's key. The filename already says
who wrote the binary, which is the whole point of stamping it. Recomputing the producer's
key from the consumer's process was tried and failed exactly where it mattered:
`PerFileRescoreTask.ValidityKey` folds in a per-leg flag, so a `--task SecondPassFDR`
process and a `--task PerFileRescoring` process compute *different* keys for the same
task; in an HPC chain the check said "not valid", Stage 7 concluded no worker had run, and
it rewrote every sidecar. **One task cannot reconstruct another task's key from a
different leg, and must not try.** Staleness is still covered, by the task that owns it: if
the worker's inputs changed, `PerFileRescoring`'s own validity fails, the driver re-runs
it, and it rewrites both the binary and the stamp. A stamp that survives is one whose
producer was legitimately skipped as already done.

**`<stem>.calibration.json` is read on every leg, not just the rescore.** Besides RT
calibration for Stage 6, it carries the isolation-scheme windows that give the gap-fill
m/z filter its per-run coverage - which is how a `SecondPassFDR` node with no mzML still
gets that coverage. It is read behind a `File.Exists`, so an orchestrator that leaves it
behind does not get an error: gap-fill filtering silently changes. That is the P13-shaped
hazard this document warns about, in the contract itself.

**A cohort-wide union cannot ride in a per-run envelope, for an ordering reason.** Each
`<stem>.reconciliation.json` is written the instant that run's planning finishes, so the
planned actions of runs planned *later* do not exist yet and cannot be in it. Any fact that
is a union over all runs is therefore only knowable after the last run is planned, which
means it belongs in an artifact written at the end of planning - not replicated into
envelopes that were each sealed too early to hold it. This is why "just add it to the
per-run file" is not an available answer for that class of fact, and the reason is not
deducible from the scope taxonomy alone.

**`<output>.<TaskName>.osprey.task` is per-artifact, not per-task.** One sidecar is
written next to each output, and its name carries the producing task so two tasks
sharing an output path cannot trample each other's record. Reuse requires both the
output to exist *and* its sidecar key to match: a sidecar can outlive its output, so a
matching key alone is not enough (P8 and P9 are separate tests, and
`PerFileResumeDriver.IsCurrent` runs both).

### Where blib-named experiment-wide artifacts live

This rule governs `fdr_experiment.bin` for both passes. It does **not** govern
`<stem>.1st-pass.model.json`, which is the replicated exception described above: that one
takes its name from the run stem and its directory from that run's own parquet, precisely
so a fan-out node can find it without knowing this rule.

A blib-named artifact takes its **name** from the output blib and its **directory**
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

Ship both to every node running either task. **Their failure behaviour differs, and only
one of them fails safely:**

- A missing `<stem>.1st-pass.model.json` under an explicitly requested frozen pass-2 mode
  aborts with a `ConfigError` rather than degrading to the anti-conservative retrain
  (see [12-second-pass-fdr](12-second-pass-fdr.md), "Fail-fast"). That is the behaviour to
  copy.
- An unreadable `<blib-stem>.1st-pass.fdr_experiment.bin` does **not** abort. First-pass
  compaction logs `[ERROR] First-pass compaction: failed to read the experiment-scope FDR
  sidecar`, sets a non-zero exit code, and **the run continues** - with a different
  retained set, finishing success-shaped hours later. On a large cohort that is a wrong
  answer that looks like a right one.

So this is not a hazard the orchestrator can be trusted to avoid; a task handed an
incomplete relay must refuse to proceed, and the second case does not yet. Treat the
current behaviour as a defect to fix, not a contract to preserve - and until it is fixed,
check the exit code, because the log line is the only other signal.


## Directory resolution

Every artifact path is resolved through `ArtifactPaths`, so redirection applies uniformly
rather than one writer at a time. Two process-wide settings decide where things land,
both defaulting to "beside the input file":

| Setting | Sets | Holds |
|---|---|---|
| `--output-dir` | `ArtifactPaths.OutputDir` | products: scores parquet, calibration JSON, FDR sidecars, reconciliation JSON |
| `--cache-dir` | `ArtifactPaths.CacheDir` | caches: `.spectra.bin`, `.libcache` |
| `--work-dir` | both | the single-flag form |

This is the product/cache split from the contract table made operational. `ResolveOutputDir`
returns `OutputDir` when set and the input's own directory otherwise. `ResolveCacheDir`
prefers an explicit `CacheDir`, falls back to the data file's own directory when that is
writable - probed once and memoized - and otherwise to `OutputDir`. A cache is
settings-independent, which is what lets several analyses share one `--cache-dir` and
reuse a single parse of the raw data.

Reaching for the input file's directory rather than a resolved one is the recurring bug
in this area: it works in a single-process run, where they coincide, and diverges the
moment anyone passes `--output-dir`.

### Identity does not name the directory

Every hash that decides artifact reuse excludes paths, with one exception noted at the end
of this section. `LibraryIdentityHash`
takes the library's file *name*, size and mtime and drops the directory, so the same
library identifies identically across implementations, operating systems, and node-local
versus shared mounts - drive-letter case, slash direction, and relative-versus-absolute
spelling all stop mattering. `ReconciliationParameterHash` names sorted, deduplicated file
*stems* rather than paths, for the same reason and with the ordering removed too, so the
hash cannot depend on the order files were passed on the command line.

The consequence is that **a completed run is relocatable**. Its artifacts can be moved,
copied, or hard-linked into a new output directory and remain valid, because their
identity does not record where they used to be. External tooling relies on this to adopt a
prior run's expensive Stage 1-4 output instead of recomputing it - see
`ai/docs/osprey-run-layout.md` for the runner parameter that does so. That relocatability
is a property of the hashes, and any change that folds a directory into one of them
removes it for every run.

**Two user-supplied paths reach artifact identity, and both defeat relocation for the
runs that use them.** `--decoy-pairing-manifest` enters `SearchParameterHash` verbatim and
unnormalised - the user's own spelling, relative or absolute. `OSPREY_PICK_LDA_MODEL`
enters the *base* key through the peak-pick suffix, so it is inherited by all four tasks:
moving, renaming, or per-node-mounting that model JSON invalidates every `.osprey.task`
stamp in the tree and silently stops `-LinkFrom` adoption. A cohort using either therefore
*does* invalidate when it moves, which is why relocating such a dataset needs a junction
restoring the original path rather than a re-run. Everything else in this section holds
regardless, and adding a third path to a hash would narrow it further.

A warm resume across builds is a separate matter: the version stamp is compared for exact
equality (`YEAR.ORDINAL.BRANCH.DOY`) - **but only where it is checked, which is narrower
than it sounds.** That comparison guards the `--input-scores` parquet load. The
`.osprey.task` resume path does not do it: `TaskValiditySidecar.IsValid` compares the
`validity_key` only, and the `version` field it records is provenance. No version component
is in the base key either. So re-invoking the same straight-through command line the next
day, against yesterday's output directory, reuses every task on a key match alone even
across a build with different scoring - the under-inclusive-key outcome P15 calls the
dangerous direction. Treat that as a gap, not a guarantee. `OSPREY_VERSION_OVERRIDE` pins
the stamp and is the sanctioned way to consume another build's artifacts deliberately.

---

## Validity and resume

### What the key is made of, and why it decides correctness

Each task composes its own key: a base of search parameters, library identity and the
peak-pick arm, plus per-task additions - `FirstPassFDR` adds six, `PerFileRescoring`
seven, `SecondPassFDR` seven. `PerFileRescoring`'s seventh is the one to know about:
`LibraryFragmentRelease.ValidityKeySuffix` branches on the per-leg flags, which is exactly
why a `SecondPassFDR` process and a `PerFileRescoring` process compute different keys for
the same task - the asymmetry the stamp-presence rule above exists to work around.
**The exact composition, and the defect each component was added to prevent, is
invalidation mechanics and lives in [14-intermediate-files](14-intermediate-files.md).**

What belongs here is why that list matters: correctness under resume rests entirely on
those keys being exhaustive, because nothing cascades. Read the list in 14 as the worked
example of P15's asymmetry - every entry was added after an under-inclusive key reused
something it should not have, and none would have cost more than a recompute if it had
turned out to be unnecessary.

The build stamp is likewise 14's: reuse requires an exact match on all four components of
`YEAR.ORDINAL.BRANCH.DOY`, and a mismatch is a hard failure rather than a warning, because
a cache from another build may carry different scoring.

### Resume is a forward scan

The driver walks the four tasks in order and, for each one that is included, skips it when
every declared output exists *and* carries a validity sidecar matching that task's current
key (`PipelineContext.CanRehydrate`). Otherwise it runs the task and stamps sidecars over
its outputs afterward.

Two details are easy to get wrong:

- **Skipping is per task, on that task's own key.** There is no cascade that invalidates
  downstream artifacts when an upstream task re-runs. This is safe because the pipeline is
  deterministic: a task re-run under an unchanged key writes the same bytes, so a
  downstream artifact stamped with a matching key is still describing the right input.
  Correctness therefore rests entirely on keys being exhaustive, which is why the table
  above exists.
- **A stale sidecar is cleared before its output is recomputed, not after.** A crash
  mid-Run then leaves no stale marker claiming a partially written output is valid. **The
  granularity differs by task and must not be unified.** The two joins clear their single
  coarse output's sidecar at the start of `Run`. The two fan-out tasks clear *per run*,
  immediately before recomputing that run, because a task-level delete would wipe the
  per-run stamps they rely on for their own within-task skip. Hoisting the delete into the
  driver to remove the apparent asymmetry would make a resumed 500-run cohort re-score
  every run - expensive, and it produces correct output, so no gate would catch it.

Sidecars are stamped even for the tasks that deliberately return "stop here" at an HPC
boundary. Gating the stamp on the pipeline continuing would skip sidecar writes for
exactly the successful early-exit modes the split depends on, and break resume for them.

### Per-run guards inside a stage

A stage that has already processed some of its runs resumes per run, not only per task.
The rule for those guards is the one that is easy to get wrong:

> A per-run resume arm must leave the state a fresh run would have left. Skipping the
> work is not the same as producing its result.

`PerFileRescoring` is the worked example. Its per-run guard finds a valid reconciled
parquet and skips re-scoring - but a downstream `SecondPassFDR` in the same process reads
apex and boundary values straight off the in-memory entries, so a guard that only skipped
would leave first-pass retention times in the buffer and ship them in the final `.blib`.
The guard therefore overlays the reconciled values back onto the in-memory entries and
re-sorts them, reproducing the fresh end state in place.

The memory contract applies to the resume arm too. That overlay re-inflates every entry
from the reconciled parquet, so the skip path once rooted the same per-entry arrays the
rescore path does, across every resumed run - reintroducing the O(runs) growth term the
rescore path had already been fixed to avoid. It now releases that payload immediately,
leaving exactly the buffer shape a fresh rescore leaves. A resume path is not exempt from
P6 just because it did no work.

---

## HPC relay checklist

What an orchestrator must place on a node before its task can run. Per-run files travel
with the run they are named for; experiment-wide files go to every node running that
task, whatever batch it was handed.

The general rule, if you remember nothing else: **a fan-out node needs its own runs'
files plus every experiment-wide artifact produced so far.** The checklists below are
that rule made explicit at each boundary.

Four things hold at every boundary and are not repeated in each list:

- **The library, and the `--decoy-pairing-manifest` if the search used one**, go to every
  node of every task. The manifest's path must match the one the search hash was computed
  from, or every artifact on that node invalidates.
- **A fan-out node gets `<stem>.spectra.bin` and no data file at all.** Osprey tolerates
  an absent source once the cache exists - the source fingerprint check treats "absent" as
  the documented resume case - so shipping the real mzML would move gigabytes per run to a
  worker that will not read them. Do **not** substitute a 0-byte stub: an empty file makes
  the mzML path reachable, so a cache that is rejected for any reason (version bump,
  truncated footer) is silently replaced by parsing an empty file, and the run finishes
  success-shaped with zero spectra. With the file simply absent, a rejected cache fails
  loudly.
- **Pass `--cache-dir` on any `--task` leg whose `--output-dir` differs from the data
  directory.** Such a leg has no raw input path to resolve `.spectra.bin` from, so without
  it the cache is not found and the run rebuilds it.

- **Every artifact travels with its `.osprey.task` sidecar.** The stamp is how the
  receiving node learns who produced the file and whether it may be reused; shipping the
  artifact without it turns a valid reuse into a recompute, or worse (P10).
- **Preserve mtime when copying the library.** `LibraryIdentityHash` is file name + size
  + mtime, so a copy tool that stamps a fresh timestamp gives the library a new identity
  and invalidates every artifact on the receiving node - a multi-hour recompute reported
  as a stale cache. Use `robocopy /COPY:DAT`, `rsync -t`, or whatever preserves mtime on
  the cluster.

### Boundary 1 -> 2: `PerFileScoring` to `FirstPassFDR`

The join needs every run's Stage 1-4 output, since training and experiment-wide q-values
are functions of all runs:

- `<stem>.scores.parquet` for **every** run in the cohort
- `<stem>.calibration.json` for **every** run
- the library, and `--input-scores` naming the parquets

`.calibration.json` must travel, which is easy to get wrong because the join reads
parquets rather than spectra. It supplies RT calibration and the isolation-scheme windows
behind the gap-fill m/z filter, and it is read on this leg and the `SecondPassFDR` leg as
well as the rescore. It is read behind a `File.Exists`: leaving it out produces no error,
just different gap-fill filtering.

`<stem>.spectra.bin` does not need to travel - the join reads parquets, not spectra. Leave
the caches on the scoring nodes unless the raw data was deleted after staging, in which
case they are the data and must be preserved.

### Boundary 2 -> 3: `FirstPassFDR` to `PerFileRescoring`

> **In flight** - this list is not yet one a single-run node can run on. The artifact that
> lets a fan-out worker obtain the join-wide compaction set without surveying the batch,
> `<blib-stem>.1st-pass.retained_base_ids.bin`, does not exist on master; a node staged
> with exactly the list below derives a different survivor set rather than erroring. See
> item 2 under `## In flight`. Stage the whole cohort's envelopes until it lands.

Each rescore node needs its own runs' artifacts plus the experiment-wide set:

Per run, for each run in the node's batch:
- `<stem>.scores.parquet`
- `<stem>.1st-pass.fdr_scores.bin`
- `<stem>.reconciliation.json`
- `<stem>.calibration.json`
- `<stem>.spectra.bin`, with no data file beside it

Experiment-wide, to **every** node:
- `<blib-stem>.1st-pass.fdr_experiment.bin`
- `<stem>.1st-pass.model.json` (any one copy) - **mandatory on an ordinary run**, because
  the default pass-2 mode is a frozen one (`protein-compact`); an unset
  `OSPREY_PASS2_QVALUE` is not an opt-out

This is the boundary where a missing experiment-wide file does the most damage, because
the node can often proceed without it and produce a plausible wrong answer rather than
failing. Ship both experiment-wide artifacts together or neither.

### Boundary 3 -> 4: `PerFileRescoring` to `SecondPassFDR`

The final join needs every run's reconciled output:

Per run, for **every** run in the cohort:
- `<stem>.scores-reconciled.parquet`
- `<stem>.2nd-pass.fdr_scores.bin` and `<stem>.2nd-pass.fdr_decoys.bin`, where the
  rescore node ran the pass-2 worker - together with their `.osprey.task` sidecars,
  which is how this task learns the worker produced them and folds them instead of
  recomputing
- `<stem>.reconciliation.json` - read here for the gap-fill entry ids on the Stage-7 fold
- `<stem>.calibration.json` - isolation-window coverage, so a node with no mzML still has it

Experiment-wide:
- `<blib-stem>.1st-pass.fdr_experiment.bin`
- `<stem>.1st-pass.model.json` (any one copy)
- `<blib-stem>.1st-pass.model-diagnostics.json`, when `--model-diagnostics` is on - the
  pass-1 half of the report exists nowhere else by this point

Not needed on the default path: `<stem>.1st-pass.fdr_scores.bin`. Establishing that is
what issue #4486 was for - an orchestrator hands a `SecondPassFDR` node the per-run
second-pass artifacts and the analysis-wide experiment sidecar, and nothing per-run from
the first pass on the **default** mode. Two exceptions: `OSPREY_PASS2_VERIFY_WORKER`, a
test instrument; and `OSPREY_PASS2_QVALUE=transfer`, which reads every run's copy to build
its score-to-run-q table. Under `transfer` a missing sidecar is **not** fatal - it logs that
the run's per-run q is left unadjusted and continues, so trimming these files and later
running a transfer arm yields a whole cohort of reconciliation-moved peaks carrying
pre-reconciliation q-values, in a success-shaped run. Ship them unless you know no transfer
arm will follow.

The `.osprey.task` sidecars are not optional bookkeeping here. Without the stamp,
`SecondPassFDR` cannot tell that a worker wrote the pass-2 files and will recompute them
from survivors only - which is not the same answer (P10).


---

## In flight

**This section is the single list of known deviations from the architecture above**, for
every document in this set. Docs 12, 14 and 15 point here rather than keeping their own,
because a per-document in-flight note is how 15 fell out of step with 00 in the first
place. Items are being settled on branch `Skyline/work/20260901_osprey_firstpass_resume`
and its companion TODOs; each is marked at the point it applies above, so clearing them
after that work merges is a single edit.

The contract above states the **target** in every case. Where today's code does not meet it,
the text says so rather than describing the current shape as though it were the design.

1. **Protein-q split of the experiment-scope FDR sidecar.** This is a P7 and P12 item, not
   a write-once one: `<blib-stem>.1st-pass.fdr_experiment.bin` is written once and never
   mutated, but it is written *late* - after protein FDR - so pass 2's experiment-scope
   product sits in an in-memory accumulator across the whole protein-FDR phase and is lost
   if that phase dies. The target splits it into two immutable files joined on entry_id at
   read time, each written by the phase that computes it: pass 2 writes precursor q,
   peptide q, PEP and aggregate score when pass 2 ends; protein FDR writes protein q when
   protein FDR ends. See "The protein-q split, and the rule behind it" in
   `ai/todos/active/TODO-20260901_osprey_firstpassfdr_resume.md`.

2. **Two experiment-wide artifacts arrive with the fan-out fix**, and both are relay
   obligations the checklist below will gain. `<blib-stem>.1st-pass.retained_base_ids.bin`
   is written by `FirstPassFDR` when Stage 6 planning ends and carries the join-wide
   first-pass base ids unioned with every planned action target - sorted `uint32`,
   library-bounded (1.49 MB at 373,487 ids). It is what lets a fan-out worker obtain the
   compaction set without surveying the batch (P6), and it is the artifact that makes the
   single-run input contract executable: today's Boundary 2 -> 3 list is not one a
   one-run node can actually run on. `<stem>.1st-pass.stratum.json` is the P12 split
   described under "Rows that are not what they look like". Both are on
   `Skyline/work/20260901_osprey_firstpass_resume`; when it lands, "two experiment-wide
   artifacts that must relay together" becomes four.

3. **The bounded-loop shape of `PerFileRescoring`.** The task is still entered with a
   materialised all-runs entry list, and its baseline still carries maps keyed by run whose
   values are per-entry - the `O(runs x entries)` shape P5 and P6 forbid. See "THE TARGET
   SHAPE for --task PerFileRescoring" and its violation table (items 6 and 7,
   `perFileEntries` and `reconciliationActions`) in
   `ai/todos/active/TODO-20260901_osprey_stage5_reload_materialization.md`.

4. **Retiring the resident survivor handoff.** `OSPREY_STAGE6_STREAM_SURVIVORS=0` still
   selects the resident path, where Stage 5's all-runs survivor buffer stays live across
   the whole Stage 6 rescore instead of being refilled one run at a time from each run's
   `.scores.parquet` and first-pass sidecar. The streamed path is the default; the switch
   goes when the resident one does.

5. **Whether the 500-run / 64 GB target is met.** It is not yet, and which stage binds is
   itself moving as each is fixed. The two TODOs above carry the current measurements;
   deliberately not restated here, because a number in a contract document is stale the
   week after it is written.

---

## Cross-references

**By stage** - the algorithm deep dives, numbered by pipeline execution order:

| Stage | Docs |
|---|---|
| 1-2 library and mzML | [01-decoy-generation](01-decoy-generation.md) |
| 3 calibration | [04-calibration](04-calibration.md), [05-rt-alignment](05-rt-alignment.md) |
| 4 first-pass search | [02-xcorr-scoring](02-xcorr-scoring.md), [03-spectral-scoring](03-spectral-scoring.md), [06-peak-detection](06-peak-detection.md), [17-vectorization](17-vectorization.md) |
| 5 first-pass FDR | [07-fdr-control](07-fdr-control.md), [08-protein-parsimony](08-protein-parsimony.md), [09-multi-charge-consensus](09-multi-charge-consensus.md), [10-cross-run-reconciliation](10-cross-run-reconciliation.md) |
| 6 rescore | [10-cross-run-reconciliation](10-cross-run-reconciliation.md), [11-boundary-overrides](11-boundary-overrides.md) |
| 7 second-pass FDR and output | [12-second-pass-fdr](12-second-pass-fdr.md), [13-blib-output-schema](13-blib-output-schema.md) |

**By subject** - the documents this one shares a boundary with:

- [14-intermediate-files](14-intermediate-files.md) - the byte formats of every artifact in
  the contract table.
- [15-hpc-scoring-split](15-hpc-scoring-split.md) - the CLI and orchestration mechanics for
  running the split described here.
- [16-determinism](16-determinism.md) - why a task re-run under an unchanged key writes the
  same bytes, which is the property resume correctness rests on.
- [19-testing](19-testing.md) - the gates that hold this contract, including the regression
  mode that asserts a per-node reconciled parquet is byte-identical to the straight-through
  one.
- [20-command-line](20-command-line.md) - every flag, including `--task`, `--work-dir`,
  `--output-dir` and `--cache-dir`.

**How we work on Osprey** - not documentation of the code, and deliberately outside this
tree - lives in `ai/docs`: the test gates and datasets, the run layout and machine paths,
the environment-variable reference, and the workflow scripts that adopt a prior run's
artifacts rather than recomputing them.
