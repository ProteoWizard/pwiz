# 12. Second-Pass FDR (C#)

> Pipeline stage: Stage 7 (`SecondPassFDR`). C#-originated; the Rust reference is
> porting these modes back (maccoss/osprey#57), so there is no Rust `docs/` source
> for this document. Corresponds to `Osprey.Tasks/Pass2FdrSidecar.cs`,
> `Osprey.FDR/FrozenModelScorer.cs`, `Osprey.FDR/PercolatorScorer.cs`.

After cross-run reconciliation (see [10-cross-run-reconciliation.md](10-cross-run-reconciliation.md))
re-scores moved / gap-filled peaks, SecondPassFDR recomputes FDR over the
reconciled entries to produce the **authoritative** experiment-level q-values that
gate the `.blib` output. This is the "second pass." The FDR *framework* is the
same one documented in [07-fdr-control.md](07-fdr-control.md); what differs is
**how the null is built** for the second pass. One way is left, the `transfer`; the three
it replaced are recorded under "Removed modes" with the measurements that removed them.

It runs in two halves. The **per-run** half runs in `PerFileRescoring`, one run at a time, in
`Pass2PerFileWorker`: it scores the run's reconciled peaks with the frozen 1st-pass model,
runs the per-run part of the transfer below, and writes the run's
`<stem>.2nd-pass.fdr_scores.bin` - for every run, whether or not Stage 6 had
work for it (#4486, #4665). The **experiment-wide** half is the join,
`Pass2FdrSidecar.ComputeAndPersist` in `SecondPassFDR`: it folds those per-run answers into the
analysis-wide `<blib-stem>.2nd-pass.fdr_experiment.bin` and puts the fresh q-values onto the
post-compaction stubs. It writes no per-run file and recomputes none; a run without a current
answer stops it.

A cohort in which Stage 6 re-scored nothing anywhere takes the same path - every run is
answered from its own unchanged peaks. Rust skips its second pass entirely there
(`total_rescored > 0`, pipeline.rs:5209); see DIVERGENCES.md.

## Why a second-pass null is a problem

First-pass compaction drops precursors that did not pass run-level FDR in any
replicate (it frees ~21 GB on a 240-file experiment). That compacted pool is
**decoy-depleted** — many decoys were dropped with the non-passing targets — so
simply *retraining* a Percolator SVM on it estimates the null from a thin,
biased decoy population and reports **anti-conservative** (optimistic) q-values.

That is why the `percolator` mode was **removed** rather than demoted. It was
measured at 1.57% true FDP against a nominal 1% on Stellar libdecoy entrapment
(the first-pass q gives 0.92% on the same data), and around 9% on an 82-file
SEA-AD set - the error grows with run count. **The model the first pass trained (the
linear SVM, or the tree ensemble under `OSPREY_FDR_MODEL=gbdt`) is now the model for the
second pass**, and second-pass
retraining has been removed outright - see "Frozen vs. retrain" below.

## `OSPREY_PASS2_QVALUE`: one mode

The second pass assigns q-values one way, the `transfer` described below.
`OSPREY_PASS2_QVALUE` is a developer variable that names it and accepts no other value.
`OspreyEnvironment` parses it, and an unrecognized token is a **startup error**, not a
fallback: a run that silently substituted the default would report q-values the caller never
asked for. This is checked in `Program` before the pipeline starts, so a script still passing
a removed token (`percolator`, `transfer-compete`, `protein-compact`) fails in seconds rather
than after Stage 1-5. The name stays in the validity keys (`;pass2=transfer`), so a directory
written under a removed mode never reads as this one's.

A user chooses how detections are boosted by their protein's other detections,
`--protein-boost best|off`: `best` trains the first pass with the protein-best column and
`off` trains without it, and both carry the first pass's q-values through the transfer.
`best` is the default at both resolutions.

| Mode | Retrain? | Null population | Level |
|------|----------|-----------------|-------|
| `transfer` (`--protein-boost best` or `off`) | no | none: pass-1 experiment q kept; moved and gap-filled peaks mapped through the pass-1 score-to-q table (first-pass calibration) | precursor + peptide |

### `transfer` - first-pass calibration

**In one sentence:** the second pass maps each re-scored peak through the first pass's calibrated
score-to-q relationship, and keeps the first pass's experiment-level q. Precursors are *identified*
on the first pass; reconciliation decides which peak is *quantified* in each run. "First-pass
calibration" describes it better than the token does; the token stays `transfer`.

No retrain and no new competition. For every compaction survivor, in the rescore worker
(`TransferOneFile`):

| Peak (classified by bit-exact score equality with pass 1) | Run-level q | Experiment-level q |
|---|---|---|
| **Unchanged** - reconciliation left it where the first pass picked it | its pass-1 run q | pass-1 value |
| **Moved** - re-picked or force-integrated at the consensus RT, re-scored with the frozen model | its NEW score looked up in this run's pass-1 score-to-q table | pass-1 value |
| **GapFill** - integrated in a run where the first pass had not scored it | its score looked up in the same table | pass-1 value |

The table is the run's own pass-1 (score, run q) pairs - precursor and peptide q separately - smoothed
into equal-count quantile bins and made monotone by PAVA isotonic regression (`BuildScoreToQTable`,
`LookupQForScore`). The join then carries each precursor's pass-1 experiment values
(`ComputePass2TransferFold`) and raises them to the best-of-runs floors.

**Why the q-values are legitimate.** A peak moved to a worse-scoring location gets a worse run q,
because its new score is looked up in the same calibrated table; and if that was the precursor's
best run, the best-of-runs floor raises its experiment q too. Nothing in this mode can lower a
q-value below what the evidence at the reported peak supports. The table itself comes from the
first pass's competition over every precursor and every decoy, before compaction - the competition
whose q-values entrapment shows to be calibrated. A reconciled peak is placed by the other runs'
consensus rather than chosen as the best of its own RT window, so a false one tends to score below
the best-of-window null the table was built from, which makes the lookup, if anything, conservative.

**What it gives up.** Reconciliation cannot rescue a precursor the first pass rejected: experiment q
is a pass-1 property. On the Astral and Stellar test runs that rescue was worth nothing measurable
once it was measured on a library where entrapment can reach every competition (below).

**PEP.** The experiment-level PEP (`FdrEntry.Pep`, one per precursor for the whole analysis) is
carried from pass 1 for unchanged and moved peaks; there is no run-level PEP, and a gap-filled
peak's PEP is not set by this mode. A per-run score-to-PEP map from the first pass, applied to
re-scored peaks the same way as q, is planned.

**Measured** (2026-10-28; the three Astral and the three Stellar test runs, natural Arabidopsis
entrapment at 1:1, entrapment filed under its paired target's protein - see "Measuring the second
pass with entrapment" below):

| | reported q <= 1% | measured FDP (combined / lower bound) |
|---|---|---|
| Astral, experiment level, default scoring | 93,528 precursors | 1.03% / 0.51% |
| Stellar, experiment level, default scoring | 31,693 precursors | 0.71% / 0.35% |
| Astral, run level, unchanged peaks | 301,219 rows | 0.90% / 0.45% |
| Astral, run level, moved peaks | 2,619 rows | 0.76% / 0.38% |
| Astral, run level, gap-filled peaks | 728 rows | **11.6% / 5.8%** |

The gap-filled row is the known weak spot: a gap fill happens only where the precursor is missing
from the run, and false identifications are disproportionately the ones seen in a single run, so
the run-wide q does not fit that population. Most gap fills (96% on Astral) correctly get run q
above 1% - "not present in this sample"; per-run filtering for quantification should not yet trust
the few that pass. The planned run-level PEP is the fix to evaluate.

**Every precursor gets the same competition.** The transfer has no protein stratum, so a protein
identified by a single peptide is not at a disadvantage - a requirement for proteomics, where small
proteins (under ~100 residues) often yield one tryptic peptide.

### Measuring a protein-aware step with entrapment

`--entrapment` files each entrapment peptide under its own protein (the paired target's accession
with FDRBench's `_p_target` marker). That is right for protein-level FDP, but an entrapment peptide
then almost never sits on a protein with other detections, so it cannot measure any step that
credits a precursor for its protein's other peptides: the removed `protein-compact` mode, and the
protein-best column that replaced it. On Astral, 13 of the 7,451 proteins `protein-compact`
re-competed were entrapment, so 99% of what it reported came from a competition entrapment could
not see, and its measured FDP (0.54%) was meaningless. To measure such a step at the precursor
level, file entrapment rows under target proteins' real accessions (a copy of the library; label
entrapment by sequence afterwards); measured that way `protein-compact` was 1.36%.
[protein-shuffled-entrapment.md](protein-shuffled-entrapment.md) describes the audit. The
transfer itself and a first pass trained with `--protein-boost off` use no protein information, so
either library gives the same answer for them.

## Removed modes

Three modes were removed, each because entrapment measured more false discoveries than its
q-values claimed. They are recorded here so the measurements do not have to be repeated; git
history holds the code.

**`percolator`** retrained the classifier on the reconciled, compacted pool - the depleted
null described above. Its `OSPREY_PROTEIN_COMPACT_RETRAIN` A/B toggle went with the retrain it
existed to measure.

**`transfer-compete`** re-ran the competition over a **target-conditioned subset**: survivors
were selected by per-run q on the TARGET side, and decoys entered only by `base_id` pairing
with those targets. That strips decoys which WON the first-pass competition - the
highest-scoring part of the null - so the second-pass q improves with **no added evidence**.
Measured on the 82-file SEA-AD cohort: **1.96% true FDP at a nominal 1%, accepting 34,325**,
against **1.53% and 37,624** for `protein-compact`. See issue #4484 for the default decision
and #4363 for the depleted-null finding.

**`protein-compact`** (`--protein-boost compact`, the default second pass until 2026-11) was
removed in 2026-11. It scored the reconciled targets and decoys with the frozen first-pass
model and re-ran the target-decoy competition **constrained to the protein stratum**: the
`base_id`s of proteins that had two or more peptides pass first-pass protein FDR, admitted as
target+decoy pairs. The reasoning was independent filtering (Bourgon 2010): a competition over
a biologically pre-filtered set carries a smaller multiple-testing burden, which recovers
marginal peptides of proteins already detected. Unlike `transfer-compete` it brought new
protein-level evidence to the ranking. What removed it:

- **Its q-values under-reported at cohort scale.** The stratum is chosen by target evidence,
  so it takes most of the high-scoring targets and only the base rate of the high-scoring
  decoys. At a reported q of 1% it measured 1.53% true FDP on the 82-file SEA-AD cohort
  (with entrapment on its own proteins, which under-reads it), 1.36% on Astral with entrapment
  filed under target proteins, and 1.29-1.59% across the Astral scoring arms of 2026-10-28,
  where `transfer` measured 1.03% on the same data.
- **Competing the complement fixed one half of it.** Until
  [#4993](https://github.com/ProteoWizard/pwiz/pull/4993), precursors outside the stratum kept
  their first-pass q while the decoys the stratum excluded counted against no one
  ([#4581](https://github.com/ProteoWizard/pwiz/issues/4581)). #4993 competed the stratum's
  complement against its own paired decoys, at run and at experiment scope. The excess
  inside the stratum remained (protein-aware FDP 1.4% with the SVM and 1.9% with out-of-fold
  GBDT at a reported 1%, one SEA-AD file);
  see [protein-shuffled-entrapment.md](protein-shuffled-entrapment.md).
- **It could not be combined with the protein-best column.** Both credit a precursor for its
  protein's other detections, so together they count that evidence twice: on three SEA-AD
  files a reported q of 1% read 1.47% true FDP, where protein-best with the transfer read
  0.91%.
- **It needed one first-pass model for the whole analysis.** Each run now trains its own
  first-pass model in `PerFileScoring`, and the mode had been refused from that change on.

The protein-best column (below, and [07](07-fdr-control.md)) is what carries the protein
evidence now: it reaches the first-pass model as a feature a target shares with its paired
decoy, and the second pass re-competes nothing. The mode's artifacts are no longer written
or read: `<blib-stem>.1st-pass.stratum.json` and `<stem>.2nd-pass.fdr_decoys.bin`.

## Frozen vs. retrain

- **Frozen** applies the captured first-pass model — fold weights/biases +
  standardizer for the SVM, or the fold GBT ensembles — to the reconciled
  features with **no new training**. It routes through `FrozenModelScorer`
  (`TryCreate` averages fold weights or takes the tree ensemble; `Score` goes
  through `PercolatorScorer.ScoreStandardizedRow`, so it is classifier-agnostic and
  works for `OSPREY_FDR_MODEL=gbdt` too). The model is captured on the streaming
  first pass via the `captureModel` hook.
- **Retrain** trained a fresh SVM/GBDT on the post-reconciliation pool. **It is gone.**
  `percolator` was removed for the depleted-null reason above, and the
  `OSPREY_PROTEIN_COMPACT_RETRAIN` A/B toggle that was the last way to reach it has been
  removed too - the question it measured is settled and recorded here. Do not re-add it;
  git history holds the dropped approach.

**There is therefore no second-pass model.** The model the first pass trained - the linear
SVM, or the tree ensemble under `OSPREY_FDR_MODEL=gbdt` - IS the model for pass 2, unchanged. Only the score DISTRIBUTIONS differ, because pass 2
runs on a subset - which is why a pass-2 feature-contribution view needs the frozen
coefficients plus per-feature target/decoy means, and nothing that has to be retrained.

### The protein-best column (`--protein-boost best`, the HRAM default)

A first pass trained with the protein-best column (see [07](07-fdr-control.md)) has a frozen model one
feature wider than the reconciled feature vectors. The column of a row is what the OTHER peptides on its
protein scored in the first pass's round 1, which moving the row's own peak does not change, and it cannot
be recomputed later (round 1's models are not saved). So `FirstPassFDR` writes each run's column to
`<stem>.1st-pass.protein_best.bin` as it scores the run, and `FrozenModelScorer` appends to every rescored
row - moved, gap-filled or CWT, target or decoy - its entry's value in that run, or the run's floor (the
median of its decoy precursors' round-1 scores) for an entry the run had no first-pass row for. A row the
first pass left alone therefore scores bit for bit as it did, and the transfer classifies it UNCHANGED.
The floor for such a row is conservative: the first pass would have read the peptide's sibling value had
it scored the precursor in that run. The column file is a declared `FirstPassFDR` output and a
`PerFileRescoring` input; a missing one, or one stamped by another first pass, stops the run.

## Inputs from the first pass

The transfer reads artifacts, not in-process state, so a `PerFileRescoring` or
`SecondPassFDR` node that never ran the first pass reads everything it needs from disk:

| Artifact | Carries |
|---|---|
| `<stem>.scores.parquet` (the stub) | the run's first-pass scores and run q-values, which the run's score-to-q table is built from |
| `<stem>.1st-pass.model.json` | the run's own first-pass model, which scores the run's moved and gap-filled rows |
| `<blib-stem>.1st-pass.fdr_experiment.bin` | the first pass's experiment-scope q-values and PEP, carried forward |

See [00-pipeline-architecture.md](00-pipeline-architecture.md) for the full contract
and the per-boundary relay checklist.

## Fail-fast

A missing input stops the run rather than defaulting. The transfer requires the
analysis-wide first-pass experiment file (`Pass2FdrSidecar.RequireTransferExperimentSidecar`):
its loader reads absence as an empty map, which would carry every precursor forward at q = 1.0
and drop it from the output. `SecondPassFDR` requires every run's current second-pass answer
from `PerFileRescoring` before it folds any (`RequireWorkerAnswers`); it recomputes none.

## Flags and switches

| Flag / env var | Default | Effect |
|---|---|---|
| `OSPREY_PASS2_QVALUE` | unset (`transfer`) | Names the second-pass q-value mode; `transfer` is the only value. Unrecognized, including the removed modes → startup error. |
| `OSPREY_FDR_MODEL` | unset (linear SVM) | **Experimental** when `gbdt`. Selects the classifier the first pass trains, which is also the frozen model pass 2 scores with; `svm` names the default. Unrecognized -> startup error. See [07](07-fdr-control.md). |

## Divergences from the Rust documentation

- **[C#-ORIGINATED] The pass-2 frozen q-value modes originated in C#** - The Rust
  algorithm doc set has no second-pass-FDR document because these modes
  (`transfer`, `protein-compact`, and the frozen-model
  machinery) were developed in the C# implementation first; the Rust reference
  ported them back in maccoss/osprey#57, with the complement's own competition (#4993)
  and the stratum's peptide-level experiment q (#4823). C# has since removed
  `protein-compact` and made `transfer` its only second pass, so the two differ wherever
  the Rust reference still runs `protein-compact`. Evidence:
  `Osprey.Tasks/Pass2FdrSidecar.cs`, `Osprey.FDR/FrozenModelScorer.cs`. Severity: info.
