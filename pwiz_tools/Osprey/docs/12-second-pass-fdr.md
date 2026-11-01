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
**how the null is built** for the second pass, selected by `OSPREY_PASS2_QVALUE`.

It runs in two halves. The **per-run** half runs in `PerFileRescoring`, one run at a time, in
`Pass2PerFileWorker`: it scores the run's reconciled peaks with the frozen 1st-pass model,
runs the per-run part of one of the modes below, and writes the run's
`<stem>.2nd-pass.fdr_scores.bin` - for every run, in every mode, whether or not Stage 6 had
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
second pass in every mode**, and second-pass
retraining has been removed outright - see "Frozen vs. retrain" below.

## `OSPREY_PASS2_QVALUE` modes

`OspreyEnvironment` parses the flag. An unrecognized token is a **startup
error**, not a fallback: a run that silently substituted the default would
report q-values the caller never asked for. This is checked in `Program` before
the pipeline starts, so a script still passing the removed `percolator` token
fails in seconds rather than after Stage 1-5.

| Mode | Retrain? | Null population | Level |
|------|----------|-----------------|-------|
| `protein-compact` (default) | no (frozen model) | competition constrained to the protein stratum | precursor |
| `transfer` | no | none: pass-1 experiment q kept; moved and gap-filled peaks mapped through the pass-1 score-to-q table (first-pass calibration) | precursor + peptide |

These two are the whole list. `percolator` was removed for retraining against a
decoy-depleted null (above), and the `OSPREY_PROTEIN_COMPACT_RETRAIN` A/B toggle went with
the retrain it existed to measure.

`transfer-compete` was removed for a **related but distinct** reason, and it is worth
stating precisely because the surviving default shares part of it. Its competition ran over
a **target-conditioned subset**: survivors were selected by per-run q on the TARGET side,
and decoys entered only by `base_id` pairing with those targets. That strips decoys which
WON the first-pass competition - the highest-scoring part of the null - so the second-pass q
improves with **no added evidence**. Measured on the 82-file SEA-AD cohort: **1.96% true FDP
at a nominal 1%, accepting 34,325**, against **1.53% and 37,624** for `protein-compact`. It
is dominated on both axes, which is why it is a removal rather than a demotion.

`protein-compact` has the same paired-subsetting bias - its stratum gate is target-conditioned
too, tracked as **[#4581](https://github.com/ProteoWizard/pwiz/issues/4581)** (open). The
mixed-pass statistics that ride along were [#4560](https://github.com/ProteoWizard/pwiz/issues/4560),
closed 2026-09-18 by contract rather than by a code change: an off-stratum survivor carries
pass-2 run-scope statistics and pass-1 experiment-scope statistics, `Score` stays pass 2 so
it pairs with the run q it competed on, and an unchanged row takes run q 1.0 as "did not
compete" (`Pass2FdrSidecar.FinishRecord`). The difference is that `protein-compact` also brings genuinely new protein-level
evidence to the ranking, where `transfer-compete` brought none. See also issue #4484 (closed)
for the default decision, and #4363 (closed) for the depleted-null finding.

**Interaction with `OSPREY_EXPERIMENT_AGG`**: after a first pass run under the
experimental `mean-best-<N>` aggregation, `protein-compact` is **refused** - it would
rewrite the reported experiment q from a MAX-aggregated competition. `transfer` is the
compatible mode. See
[Experiment-wide aggregation](07-fdr-control.md#experiment-wide-aggregation-osprey_experiment_agg).

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
(`ComputePass2TransferFold`) and raises them to the best-of-runs floors, as in every mode.

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

**Compared with `protein-compact`.** `transfer` gives every precursor the same competition. It has
no protein stratum, so a protein identified by a single peptide is not at a disadvantage - a
requirement for proteomics, where small proteins (under ~100 residues) often yield one tryptic
peptide. On the same data the `protein-compact` default reported more precursors at q <= 1%, but
measured 1.29-1.59% at a reported 1% (Astral), and its extra identifications came from the easier
competition it gives peptides of proteins with >= 2 first-pass peptides.

### Measuring the second pass with entrapment

`--entrapment` files each entrapment peptide under its own protein (the paired target's accession
with FDRBench's `_p_target` marker). That is right for protein-level FDP, but it means entrapment
almost never enters the `protein-compact` stratum (proteins with >= 2 first-pass peptides): on
Astral, 13 of 7,451 stratum proteins were entrapment, so 99% of what `protein-compact` reports came
from a competition entrapment could not see, and its measured FDP (0.54%) was meaningless. To
measure `protein-compact` at the precursor level, file each entrapment row under its paired
target's real accession (a copy of the library; label entrapment by sequence afterwards); measured
that way it was 1.36%. `transfer` and pass 1 use no protein information, so either library gives
the same answer for them.

### `protein-compact` (frozen model)

Scores the reconciled **targets and decoys** with the **frozen 1st-pass model** (no
retrain), then recomputes q-values and PEP by a fresh target-decoy competition - each run's
half in the rescore worker, the experiment-wide fold in `ComputePass2TransferCompeteFull`,
one file resident at a time. Both sides are scored on
the same frozen scale, which is what removes the RETRAIN pathology - but note this does not
make the null unbiased: the stratum gate is target-conditioned, so the in-stratum decoy null
is selected against (#4581). The competition is
**constrained to the protein stratum** — the `base_id`s of proteins that had ≥2 peptides pass first-pass
protein FDR, admitted as target+decoy pairs (the stratum is built by first-pass
protein parsimony; see [08-protein-parsimony.md](08-protein-parsimony.md)).
Off-stratum survivors keep their first-pass q-values, so the report is
`pass-1 ∪ stratum-passers`. Constraining the competition to a biologically
pre-filtered set reduces the multiple-testing burden (independent filtering,
Bourgon 2010).

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

## Inputs from the first pass

The frozen modes are frozen against artifacts, not against in-process state, so a
`SecondPassFDR` node that never ran the first pass reads everything it needs from
disk. Three experiment-wide artifacts carry it:

| Artifact | Carries |
|---|---|
| `<blib-stem>.1st-pass.model.json` | the frozen Percolator model (standardizer + per-fold weights and biases, or per-fold tree ensembles under `OSPREY_FDR_MODEL=gbdt`) and the first pass's `OSPREY_EXPERIMENT_AGG` provenance |
| `<blib-stem>.1st-pass.stratum.json` | the protein stratum, under `protein-compact`. Split out of the model file in #4633, because first-pass protein FDR computes it and training does not |
| `<blib-stem>.1st-pass.fdr_experiment.bin` | the first pass's experiment-scope q-values |

**They must relay together.** A node holding one without the others cannot proceed:
the model without the stratum cannot constrain a `protein-compact` competition, and
the stratum without the model has nothing to score with. Each is written once, under the
analysis name; they were once copied beside every run, which doc 00 (P1) now forbids.

See [00-pipeline-architecture.md](00-pipeline-architecture.md) for the full contract
and the per-boundary relay checklist.

## Fail-fast

An **explicitly requested** frozen mode never silently degrades. If the frozen model,
the required sidecars, or the protein stratum are absent (e.g. a warm rerun that loaded
cached SVM scores and skipped first-pass training, or a present-but-corrupt first-pass
sidecar), `Pass2FdrSidecar` aborts with a `ConfigError` and actionable guidance rather
than reporting looser FDR than a cold run under the same flag.

There is no longer anything to degrade TO - the retrain that used to be the fallback is
gone - so the abort is the only outcome, not the stricter of two.

Because `protein-compact` is now the DEFAULT, this fail-fast reaches ordinary
runs, not just explicitly flagged ones. A distributed `--task SecondPassFDR` node
never trained pass 1 and cannot rebuild the stratum (that needs the full library
plus the first-pass detected peptides), so both the model and the stratum are
reloaded from disk.

They are **two files, not one**, because two different phases produce them:
`<blib-stem>.1st-pass.model.json` is written the moment first-pass training returns a
model, and `<blib-stem>.1st-pass.stratum.json` when first-pass protein FDR ends. On a
446-file cohort those two moments are hours apart, and bundling them meant the
model - a few hundred KB for the linear SVM, about 3.4 MB for a tree model under
`OSPREY_FDR_MODEL=gbdt`, fully computed at minute ~21 - existed only in RAM
until the end of the task, so any interruption threw it away.
`FirstPassModelIO.LoadForAnalysis` merges the two on read, pairing them by their
stamps, so a consumer still sees one sidecar. **Both must ride
the HPC relay**: an orchestrator that copies the model between phase directories
and not the stratum reaches this fail-fast under the default mode.

**Interaction with `mean-best-<N>`, worth knowing before a sweep**: a first pass
run under `OSPREY_EXPERIMENT_AGG=mean-best-<N>` is REFUSED by the default mode,
because the reported column would carry two statistics — on-stratum precursors
max-aggregated, off-stratum precursors on their first-pass mean(best-N) q. Set
`OSPREY_PASS2_QVALUE=transfer` for those arms. The failure is deliberate: an
effective default that silently depended on the first pass's aggregation arm
would be harder to reason about than an explicit variable.

## Flags and switches

| Flag / env var | Default | Effect |
|---|---|---|
| `OSPREY_PASS2_QVALUE` | `protein-compact` | Selects the second-pass q-value mode: `transfer` \| `protein-compact`. Unrecognized → startup error. |
| `OSPREY_FDR_MODEL` | unset (linear SVM) | **Experimental** when `gbdt`. Selects the classifier the first pass trains, which is also the frozen model pass 2 scores with; `svm` names the default. Unrecognized -> startup error. See [07](07-fdr-control.md). |

## Divergences from the Rust documentation

- **[C#-ORIGINATED] The pass-2 frozen q-value modes originated in C#** - The Rust
  algorithm doc set has no second-pass-FDR document because these modes
  (`transfer`, `protein-compact`, and the frozen-model
  machinery) were developed in the C# implementation first; the Rust reference is
  porting them back in maccoss/osprey#57. Both implementations have since removed
  the `percolator` mode and defaulted to `protein-compact` together, so the shipped
  defaults agree. Parity for the frozen
  modes is therefore tracked **C# → Rust** rather than Rust → C#. Evidence:
  `Osprey.Tasks/Pass2FdrSidecar.cs`, `Osprey.FDR/FrozenModelScorer.cs`. Severity: info.
