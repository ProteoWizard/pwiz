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
first-pass SVM is now the model for the second pass in every mode**; only the
`OSPREY_PROTEIN_COMPACT_RETRAIN` diagnostic A/B still retrains.

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
| `transfer-compete` | no (frozen model) | fresh full-population target-decoy competition | precursor |

**Interaction with `OSPREY_EXPERIMENT_AGG`**: after a first pass run under the
experimental `mean-best-<N>` aggregation, `transfer-compete` and `protein-compact` are
**refused** - both would rewrite the reported experiment q from a MAX-aggregated
competition. `transfer` is the compatible mode. See
[Experiment-wide aggregation](07-fdr-control.md#experiment-wide-aggregation-osprey_experiment_agg).

### `transfer`

No retrain. Pass-1 q-values are carried through unchanged, and **only the per-run
q-value of reconciliation-moved peaks** is re-mapped through each file's own
score→run-q table (`TransferPerRunQ` → `BuildScoreToQTable`, equal-count quantile
bins + PAVA isotonic; `LookupQForScore`). The experiment q is frozen by the
best-peak anchor. Each survivor is classified Unchanged / Moved / GapFill, with
bit-exact score equality as the "Moved" discriminator.

### `transfer-compete` (frozen model)

Scores the reconciled **targets and decoys** with the **frozen 1st-pass model**
(no retrain), then recomputes q-values and PEP by a **fresh full-population
target-decoy competition** — a non-depleted null, because both sides are scored on
the same frozen scale (`ComputePass2TransferCompeteFull` with `stratumBaseIds ==
null` → `PercolatorFdr.ComputeFullPopulationPrecursorFdrStreaming`, one file
resident at a time). Precursor-level only.

### `protein-compact` (frozen model)

Identical to `transfer-compete` but the competition is **constrained to the
protein stratum** — the `base_id`s of proteins that had ≥2 peptides pass first-pass
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
- **Retrain** trains a fresh SVM/GBDT on the post-reconciliation pool. Since the
  `percolator` mode was removed, the `OSPREY_PROTEIN_COMPACT_RETRAIN` A/B toggle
  is the ONLY way to reach it.

`OSPREY_PROTEIN_COMPACT_RETRAIN` is a diagnostic A/B lever: with `protein-compact`
it **skips** the frozen-model + stratum competition and instead retrains the
second pass over the stratum-expanded compacted pool, isolating the
frozen-vs-retrain calibration difference for the entrapment oracle.

## Fail-fast

An **explicitly requested** frozen mode never silently degrades to the
anti-conservative retrain. If the frozen model, the required sidecars, or the
protein stratum are absent (e.g. a warm rerun that loaded cached SVM scores and
skipped first-pass training, or a present-but-corrupt first-pass sidecar),
`Pass2FdrSidecar` aborts with a `ConfigError` and actionable guidance rather than
reporting looser FDR than a cold run under the same flag.

Because `protein-compact` is now the DEFAULT, this fail-fast reaches ordinary
runs, not just explicitly flagged ones. That is why the first-pass model sidecar
also carries the protein stratum: a distributed `--task SecondPassFDR` node
never trained pass 1 and cannot rebuild the stratum (that needs the full library
plus the first-pass detected peptides), so both are reloaded from the sidecar.

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
| `OSPREY_PASS2_QVALUE` | `protein-compact` | Selects the second-pass q-value mode: `transfer` \| `transfer-compete` \| `protein-compact`. Unrecognized → startup error. |
| `OSPREY_PROTEIN_COMPACT_RETRAIN` | off (frozen) | With `protein-compact`, retrain the second pass instead of using the frozen model + stratum competition (A/B lever). |
| `OSPREY_FDR_PROJECTION` | on | Streams the FDR peak via the thin `FdrProjection` slice; the frozen modes stream one file at a time so routing them does not hold all features resident. |

## Divergences from the Rust documentation

- **[C#-ORIGINATED] The pass-2 frozen q-value modes originated in C#** - The Rust
  algorithm doc set has no second-pass-FDR document because these modes
  (`transfer`, `transfer-compete`, `protein-compact`, and the frozen-model
  machinery) were developed in the C# implementation first; the Rust reference is
  porting them back in maccoss/osprey#57. Both implementations have since removed
  the `percolator` mode and defaulted to `protein-compact` together, so the shipped
  defaults agree. Parity for the frozen
  modes is therefore tracked **C# → Rust** rather than Rust → C#. Evidence:
  `Osprey.Tasks/Pass2FdrSidecar.cs`, `Osprey.FDR/FrozenModelScorer.cs`. Severity: info.
