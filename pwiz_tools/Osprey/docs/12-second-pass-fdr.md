# 12. Second-Pass FDR (C#)

> Pipeline stage: Stage 7 (merge node). C#-originated; the Rust reference is
> porting these modes back (maccoss/osprey#57), so there is no Rust `docs/` source
> for this document. Corresponds to `Osprey.Tasks/Pass2FdrSidecar.cs`,
> `Osprey.FDR/FrozenModelScorer.cs`, `Osprey.FDR/PercolatorFdr.cs`.

After cross-run reconciliation (see [10-cross-run-reconciliation.md](10-cross-run-reconciliation.md))
re-scores moved / gap-filled peaks, the merge node recomputes FDR over the
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
The frozen modes below avoid retraining on the depleted pool.

## `OSPREY_PASS2_QVALUE` modes

`OspreyEnvironment` parses the flag; an unrecognized token normalizes to
`percolator` with a warning.

| Mode | Retrain? | Null population | Level |
|------|----------|-----------------|-------|
| `percolator` (default) | **yes** | reconciled + compacted pool | precursor + peptide |
| `transfer` | no | pass-1 q carried through; only moved peaks re-mapped | precursor + peptide |
| `transfer-compete` | no (frozen model) | fresh full-population target-decoy competition | precursor |
| `protein-compact` | no (frozen model) | competition constrained to the protein stratum | precursor |

**Interaction with `OSPREY_EXPERIMENT_AGG`**: after a first pass run under the
experimental `mean-best-<N>` aggregation, `transfer-compete` and `protein-compact` are
**refused** - both would rewrite the reported experiment q from a MAX-aggregated
competition. `transfer` is the compatible mode. See
[Experiment-wide aggregation](07-fdr-control.md#experiment-wide-aggregation-osprey_experiment_agg).

### `percolator` (default)

Retrains the second-pass Percolator SVM and recomputes a target/decoy null on the
reconciled + compacted pool (`ComputePass2Resident` → `FirstJoinTask.RunPercolatorFdr`).
This is the historical path; the decoy-depletion caveat above applies, which is
why the frozen modes were added.

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
- **Retrain** trains a fresh SVM/GBDT on the post-reconciliation pool (the default
  `percolator` mode, or the `OSPREY_PROTEIN_COMPACT_RETRAIN` A/B toggle).

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
reporting looser FDR than a cold run under the same flag. The default
(`percolator`) path is unaffected.

## Flags and switches

| Flag / env var | Default | Effect |
|---|---|---|
| `OSPREY_PASS2_QVALUE` | `percolator` | Selects the second-pass q-value mode: `percolator` \| `transfer` \| `transfer-compete` \| `protein-compact`. Unrecognized → `percolator` + warning. |
| `OSPREY_PROTEIN_COMPACT_RETRAIN` | off (frozen) | With `protein-compact`, retrain the second pass instead of using the frozen model + stratum competition (A/B lever). |
| `OSPREY_FDR_PROJECTION` | on | Streams the FDR peak via the thin `FdrProjection` slice; the frozen modes stream one file at a time so routing them does not hold all features resident. |

## Divergences from the Rust documentation

- **[C#-ORIGINATED] The pass-2 frozen q-value modes originated in C#** - The Rust
  algorithm doc set has no second-pass-FDR document because these modes
  (`transfer`, `transfer-compete`, `protein-compact`, and the frozen-model
  machinery) were developed in the C# implementation first; the Rust reference is
  porting them back in maccoss/osprey#57. The default `percolator` mode matches the
  long-standing two-pass behavior both implementations share. Parity for the frozen
  modes is therefore tracked **C# → Rust** rather than Rust → C#. Evidence:
  `Osprey.Tasks/Pass2FdrSidecar.cs`, `Osprey.FDR/FrozenModelScorer.cs`. Severity: info.
