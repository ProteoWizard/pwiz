# 12. Second-Pass FDR (C#)

> Pipeline stage: Stage 7 (`SecondPassFDR`). C#-originated; the Rust reference is
> porting these modes back (maccoss/osprey#57), so there is no Rust `docs/` source
> for this document. Corresponds to `Osprey.Tasks/Pass2FdrSidecar.cs`,
> `Osprey.FDR/FrozenModelScorer.cs`, `Osprey.FDR/PercolatorFdr.cs`.

After cross-run reconciliation (see [10-cross-run-reconciliation.md](10-cross-run-reconciliation.md))
re-scores moved / gap-filled peaks, SecondPassFDR recomputes FDR over the
reconciled entries to produce the **authoritative** experiment-level q-values that
gate the `.blib` output. This is the "second pass." The FDR *framework* is the
same one documented in [07-fdr-control.md](07-fdr-control.md); what differs is
**how the null is built** for the second pass, selected by `OSPREY_PASS2_QVALUE`.

The driver is `Pass2FdrSidecar.ComputeAndPersist`: it reloads the reconciled PIN
features, runs one of the modes below, writes a `<stem>.2nd-pass.fdr_scores.bin`
sidecar per file, and reloads the fresh q-values onto the post-compaction stubs.

## Why a second-pass null is a problem

First-pass compaction drops precursors that did not pass run-level FDR in any
replicate (it frees ~21 GB on a 240-file experiment). That compacted pool is
**decoy-depleted** — many decoys were dropped with the non-passing targets — so
simply *retraining* a Percolator SVM on it estimates the null from a thin,
biased decoy population and reports **anti-conservative** (optimistic) q-values.

That is why the `percolator` mode was **removed** rather than demoted. It was
measured at 1.57% true FDP against a nominal 1% on Stellar libdecoy entrapment
(the first-pass q gives 0.92% on the same data), and around 9% on an 82-file
SEA-AD set — the error grows with run count. **The linear model trained by the
first-pass SVM is now the model for the second pass in every mode**, and second-pass
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
| `transfer` | no | pass-1 q carried through; only moved peaks re-mapped | precursor + peptide |

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
too, tracked as **[#4581](https://github.com/ProteoWizard/pwiz/issues/4581)** (open), with
[#4560](https://github.com/ProteoWizard/pwiz/issues/4560) on the mixed-pass statistics that
ride along. The difference is that `protein-compact` also brings genuinely new protein-level
evidence to the ranking, where `transfer-compete` brought none. See also issue #4484 (closed)
for the default decision, and #4363 (closed) for the depleted-null finding.

**Interaction with `OSPREY_EXPERIMENT_AGG`**: after a first pass run under the
experimental `mean-best-<N>` aggregation, `protein-compact` is **refused** - it would
rewrite the reported experiment q from a MAX-aggregated competition. `transfer` is the
compatible mode. See
[Experiment-wide aggregation](07-fdr-control.md#experiment-wide-aggregation-osprey_experiment_agg).

### `transfer`

No retrain. Pass-1 q-values are carried through unchanged, and **only the per-run
q-value of reconciliation-moved peaks** is re-mapped through each file's own
score→run-q table (`TransferPerRunQ` → `BuildScoreToQTable`, equal-count quantile
bins + PAVA isotonic; `LookupQForScore`). The experiment q is frozen by the
best-peak anchor. Each survivor is classified Unchanged / Moved / GapFill, with
bit-exact score equality as the "Moved" discriminator.

### `protein-compact` (frozen model)

Scores the reconciled **targets and decoys** with the **frozen 1st-pass model** (no
retrain), then recomputes q-values and PEP by a fresh target-decoy competition
(`ComputePass2TransferCompeteFull`, one file resident at a time). Both sides are scored on
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
  through `PercolatorFdr.ScoreStandardizedRow`, so it is classifier-agnostic and
  works for `--fdr-method gbdt` too). The model is captured on the streaming
  first pass via the `captureModel` hook.
- **Retrain** trained a fresh SVM/GBDT on the post-reconciliation pool. **It is gone.**
  `percolator` was removed for the depleted-null reason above, and the
  `OSPREY_PROTEIN_COMPACT_RETRAIN` A/B toggle that was the last way to reach it has been
  removed too - the question it measured is settled and recorded here. Do not re-add it;
  git history holds the dropped approach.

**There is therefore no second-pass model.** The linear model the first-pass SVM trained
IS the model for pass 2, unchanged. Only the score DISTRIBUTIONS differ, because pass 2
runs on a subset - which is why a pass-2 feature-contribution view needs the frozen
coefficients plus per-feature target/decoy means, and nothing that has to be retrained.

## Inputs from the first pass

The frozen modes are frozen against artifacts, not against in-process state, so a
`SecondPassFDR` node that never ran the first pass reads everything it needs from
disk. Three experiment-wide artifacts carry it:

| Artifact | Carries |
|---|---|
| `<stem>.1st-pass.model.json` | the frozen Percolator model (standardizer + per-fold weights and biases) and the first pass's `OSPREY_EXPERIMENT_AGG` provenance |
| `<stem>.1st-pass.stratum.json` | the protein stratum, under `protein-compact`. Split out of the model sidecar in #4633, because first-pass protein FDR computes it and training does not |
| `<blib-stem>.1st-pass.fdr_experiment.bin` | the first pass's experiment-scope q-values |

**They must relay together.** A node holding one without the others cannot proceed:
the model without the stratum cannot constrain a `protein-compact` competition, and
the stratum without the model has nothing to score with. The model sidecar is written
beside **every** run's other Stage-5 artifacts, identical each time, so any one copy
serves.

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
`<stem>.1st-pass.model.json` is written the moment first-pass training returns a
model, and `<stem>.1st-pass.stratum.json` when first-pass protein FDR ends. On a
446-file cohort those two moments are hours apart, and bundling them meant the
model — a few hundred KB, fully computed at minute ~21 — existed only in RAM
until the end of the task, so any interruption threw it away. `LoadFromAny`
merges the two on read, so a consumer still sees one sidecar. **Both must ride
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
| `OSPREY_FDR_PROJECTION` | on | Streams the FDR peak via the thin `FdrProjection` slice; the frozen modes stream one file at a time so routing them does not hold all features resident. |

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
