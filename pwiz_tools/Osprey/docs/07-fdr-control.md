# 07. FDR Control (C#)

> Pipeline stage: Stage 5 (first-pass) & Stage 7 (second-pass). C# port of Rust docs/07-fdr-control.md. Corresponds to Rust osprey FDR control.

This document describes how the C# Osprey port controls the false discovery rate
(FDR) of peptide/precursor identifications. It is a native managed reimplementation
of the Rust `osprey-fdr` + `osprey-ml` crates: there is **no Python dependency, no
external Mokapot subprocess, and no PIN-file round-trip on the default path**. The
default engine is a native linear-SVM Percolator that trains and scores entirely
in-process. See `08-protein-parsimony.md` for protein-level FDR (parsimony +
picked-protein), and `10-cross-run-reconciliation.md` for how Stage 6 reconciliation
feeds the second FDR pass.

Primary C# code:

| File | Role |
|------|------|
| `Osprey.FDR/PercolatorEngine.cs` | Orchestration: build `PercolatorEntry` input, dispatch, write results back onto stubs, best-of-runs clamp |
| `Osprey.FDR/PercolatorFdr.cs` | Native Percolator: standardize, subsample, fold assignment, SVM training, Granholm calibration, PEP, q-values |
| `Osprey.FDR/PercolatorEntryBuilder.cs` | Build the flat `PercolatorEntry` list from `FdrEntry` stubs |
| `Osprey.FDR/FdrController.cs` | Simple target-decoy competition (used by `--fdr-method simple`) |
| `Osprey.FDR/FdrProjection.cs`, `FdrProjectionOutput.cs` | Thin peak-buffer projection path (issue #4355) driving the identical SVM core |
| `Osprey.ML/LinearSvmClassifier.cs` | L2-regularized linear SVM via dual coordinate descent + grid search for C |
| `Osprey.ML/PepEstimator.cs` | Posterior error probability (KDE + isotonic/PAVA), Sage-derived |
| `Osprey.ML/QValueCalculator.cs`, `MlMath.cs` | Conservative q-value helpers and math primitives |
| `Osprey.Core/FdrEntry.cs` | The FDR result stub with the six q-value fields + `EffectiveRunQvalue`/`EffectiveExperimentQvalue` |
| `Osprey.Tasks/FirstPassFdrTask.cs` | Stage 5 first-pass FDR driver (HPC `--task FirstPassFDR`) |
| `Osprey.Tasks/Pass2FdrSidecar.cs`, `SecondPassFdrTask.cs` | Stage 7 second-pass FDR driver (HPC `--task SecondPassFDR`) |

---

## Overview: pipeline order

The FDR stage runs twice in the two-pass architecture:

```text
Stage 5 (first pass, after per-file coelution scoring):
  1. Each observation carries a 21-feature PIN vector (see 03-spectral-scoring.md),
     cached per file in .scores.parquet; the resident buffer is a lightweight FdrEntry stub.
  2. Build a flat PercolatorEntry list (one per observation) from the stubs.
  3. Dispatch by --fdr-method:
       percolator (default) -> native streaming linear-SVM Percolator
       gbdt                 -> same Percolator framework, GBDT classifier per fold (C#-only)
       simple               -> direct target-decoy competition on coelution_sum
  4. Native Percolator produces four q-value levels (run/experiment x precursor/peptide),
     PEP, and a combined SVM score; write them back onto the FdrEntry stubs.
  5. Best-of-runs clamp on experiment q-values.
  6. (Optional) first-pass protein FDR gate; compaction drops non-passing stubs.

Stage 6: cross-run reconciliation re-scores moved / gap-filled peaks (10-cross-run-reconciliation.md).

Stage 7 (second pass, authoritative):
  7. Re-run the identical Percolator core over the reconciled entries, write
     .2nd-pass.fdr_scores.bin sidecars, reload stubs with the fresh q-values.
  8. Re-apply the best-of-runs clamp (Stage 6 reset the run q-values of moved peaks).
  9. Second-pass protein FDR (authoritative) + blib output.
```

The two passes call the **same** `PercolatorEngine.RunPercolatorFdr` core (with
`passLabel` `"First-pass"` vs `"Second-pass"`), so training, calibration, and q-value
math are shared source (`PercolatorEngine.cs:59`, `Pass2FdrSidecar.cs:521`).

---

## Step 1 — FDR input: `FdrEntry` stubs and the `PercolatorEntry` list

After per-file coelution scoring, each observation is represented by an
`FdrEntry` stub (`Osprey.Core/FdrEntry.cs:33`): `EntryId`, `ParquetIndex`,
`IsDecoy`, `Charge`, `ScanNumber`, RT bounds, `CoelutionSum`, `Score`, the six
q-value fields, `Pep`, and the interned `ModifiedSequence`. The heavy 21-feature
vector is *not* held resident on the streaming path — it is reloaded on demand from
`.scores.parquet` by `ParquetIndex` (`FirstPassFdrTask.cs` `loadFileFeatures`
delegate).

Target/decoy pairing uses the high bit of `EntryId`: `base_id = EntryId & 0x7FFFFFFF`
(`PercolatorFdr.cs:258`, `BASE_ID_MASK = 0x7FFFFFFF`). A target and its paired decoy
share `base_id`; the decoy has the high bit set.

`PercolatorEntryBuilder.Build` (`PercolatorEngine.cs:105`) emits exactly one
`PercolatorEntry` per stub in nested `(file, entry)` order. Results are later zipped
back **by position** — the former psm_id-keyed re-join was removed as redundant
(`PercolatorEngine.ApplyPercolatorResults`, `PercolatorEngine.cs:404`). Before
building, each file's entries are sorted by `(EntryId, Charge, ScanNumber,
ParquetIndex)` so the SVM working-set order is canonical across Rust and C#
(`PercolatorEngine.cs:82`).

---

## Step 2 — Dispatch by FDR method

`FirstPassFdrTask.cs` switches on `config.FdrMethod`:

- `FdrMethod.Percolator` (default) → `PercolatorEngine.RunPercolatorFdr` (linear SVM)
- `FdrMethod.Gbdt` → the **same** Percolator framework with a gradient-boosted-tree
  classifier swapped in per fold (`--fdr-method gbdt`; C#-only, see below)
- `FdrMethod.Simple` → `PercolatorEngine.RunSimpleFdr`
- any other value → falls back to `RunSimpleFdr` with a warning

`--fdr-method` accepts `percolator | gbdt | simple` (`OspreyCommandArgs.cs:120`; the
deprecated alias `fasttree` also parses → `Gbdt`). `FdrMethod.Mokapot` exists in the
enum but **is not reachable from the CLI** — there is no Mokapot runner, no PIN-writing
pre-competition, and no external subprocess in the C# port. See Divergences.

### Simple FDR (`--fdr-method simple`)

`PercolatorEngine.RunSimpleFdr` (`PercolatorEngine.cs:350`) runs
`FdrController.CompeteAndFilter` per file, scoring directly by `e.CoelutionSum`
(PIN feature 0, `fragment_coelution_sum`) with no ML reranking and no ROC-AUC feature
selection. Passing targets get `RunPrecursorQvalue = RunPeptideQvalue =
ExperimentPrecursorQvalue = ExperimentPeptideQvalue = FdrAtThreshold`; everything else
stays at the `1.0` default. This is a baseline path; the default is Percolator.

### GBDT FDR (`--fdr-method gbdt`) — C#-only, no Rust counterpart

`gbdt` reuses the **entire** Percolator scaffold documented below — feature
standardization, 3-fold peptide-grouped CV, semi-supervised positive-set iteration,
cross-fold score calibration, PEP, and the four q-value levels — and swaps only the
per-fold classifier: `GradientBoostedTrees` (`Osprey.ML/GradientBoostedTrees.cs`)
instead of the linear SVM. It is a pure-managed second-order (Newton) boosting
implementation with the XGBoost regularized objective (logistic loss, per-leaf L2/L1,
min split gain, row/column subsampling, histogram split finding), made deterministic
with `XorShift64` and single-threaded float accumulation.

Two structural differences from the SVM path (`PercolatorFdr.TrainFoldGbt`): there is
**no `GridSearchC`** (trees have no cost parameter), and iteration selection is
**honest** — because trees grow monotonically the in-sample passing count would always
pick the most-overfit round, so the best iteration is chosen on a held-out inner split
(`OSPREY_GBT_INNER_FOLDS`, default 5). Full-population scoring averages fold **scores**
(trees can't be weight-averaged). The default iteration cap is `OSPREY_GBT_MAX_ITERATIONS`
= 30 (vs the SVM's fixed 10); hyperparameters are overridable via `OSPREY_GBT_*`
(gamma / lambda / alpha / max-depth / n-trees / min-child-weight / learning-rate /
subsample / colsample).

**This method has no Rust counterpart** — grepping the Rust crates for
`gbdt`/`GradientBoost` returns nothing. It is a C# addition *beyond* the reference
engine, so it carries no cross-impl parity claim; `--fdr-method percolator` remains the
default and the parity-gated path.

---

## Step 3 — Native Percolator (default)

`PercolatorFdr.RunPercolator` (`PercolatorFdr.cs:264`) implements the semi-supervised
Percolator of Käll et al. (2007). Both targets and their paired decoys enter — no
upstream competition. The C# port is **streaming-only**: the former sub-threshold
"direct" branch that trained on all entries was removed to match Rust's streaming-only
change, so C# and Rust fit the standardizer on the identical best-per-precursor subset
at every scale (`PercolatorEngine.DispatchSvm`, `PercolatorEngine.cs:336`;
`RunPercolatorStreaming`, `PercolatorEngine.cs:473`).

### 3a. Standardize features

`FeatureStandardizer.FitTransform` standardizes every feature to zero mean / unit
variance (`PercolatorFdr.cs:292`). On the streaming path the standardizer is fit on the
**training subset**, not the full population (`RunPercolatorStreaming`).

### 3b. Best-per-precursor dedup + peptide-grouped subsample

`PercolatorFdr.BuildTrainingSubset` (called at `PercolatorFdr.cs:338` and from both
streaming callers) does two things, keeping target/decoy pairs and all charge states of
a peptide together:

1. **Best-per-precursor**: `SelectBestPerPrecursor` picks the single best-scoring
   observation per `(base_id, isDecoy)` across all files, ranked by `CoelutionSum`
   (byte-identical to `Features[0]` on the first pass). With N files this avoids the SVM
   seeing the same precursor's pair N times. This dedup applies on *all* multi-file
   inputs — the comment at `PercolatorFdr.cs:320` notes Rust was patched to match.
2. **Subsample**: if the dedup set still exceeds `MaxTrainSize` (default **300000**,
   `PercolatorConfig` ctor `PercolatorFdr.cs:123`), `SubsampleByPeptideGroup` samples
   whole peptide groups using the same XOR-shift PRNG seed (default **42**) and
   peptide-key sort order as Rust.

The learned model is later applied to **all** entries, not just the subset.

### 3c. Fold assignment

`CreateStratifiedFoldsByPeptide` (`PercolatorFdr.cs:390`) assigns 3 folds
(`NFolds = 3`) grouping by target peptide via `base_id`, so all charge states and the
paired decoy of a peptide land in the same fold. This enforces the critical invariant:
splitting pairs across folds would let unpaired targets auto-win competition in a
training fold and make the SVM too permissive.

### 3d. Best initial feature

`FindBestInitialFeature` (`PercolatorFdr.cs:411`) scores every entry by each single
standardized feature (ascending only) and counts targets passing after paired
competition; the feature with the most passing targets seeds iteration 0. If zero pass at
the train FDR, it relaxes to 5% (`PercolatorFdr.cs:414`). The chosen feature name is
logged via `config.FeatureInfos`.

### 3e. Iterative SVM training per fold

Folds train in parallel via `OspreyParallel.For` (explicit dedicated threads, chosen over
TPL because the TaskReplicator throttled effective parallelism) — `PercolatorFdr.cs:488`.
Each fold runs `TrainFold` up to `MaxIterations = 10` iterations:

1. Select the positive training set: targets passing `TrainFdr` on the current scores;
   if fewer than `MIN_POSITIVE = 50` (`PercolatorFdr.cs:259`) pass, relax progressively.
2. Build the SVM set: selected targets (positive) + all decoys (negative).
3. Grid-search C over `CValues = {0.001, 0.01, 0.1, 1.0, 10.0, 100.0}`
   (`PercolatorFdr.cs:122`) via inner CV each iteration.
4. Train an L2-regularized linear SVM by dual coordinate descent
   (`LinearSvmClassifier.cs`).
5. Score, count passing targets, track the best model; stop after 2 non-improving
   iterations.

The selected per-fold C is reported on the console (`PercolatorFdr.cs:516`).

### 3f. Score all entries

Held-out CV entries are scored by their fold's model; entries outside the training subset
are scored by the **average** of all fold models (`PercolatorFdr.cs:565-627`). On the
streaming path this is done by `ScorePopulationAndComputeFdr` /
`ScoreProjectionAndComputeFdrInPlace`, which average the fold weights + bias and apply
`standardizer` + averaged model to every entry, reloading features one file at a time
(`PercolatorFdr.cs:760`, `PercolatorFdr.cs:1110`).

### 3g. Granholm score calibration between folds

`CalibrateScoresBetweenFolds` (`PercolatorFdr.cs:2105`) linearly normalizes each fold's
scores per Granholm et al. (2012): the score at the FDR threshold maps to 0 and the median
decoy score maps to -1, via `(score - thresholdScore) / (thresholdScore - medianDecoy)`
(`PercolatorFdr.cs:2149-2154`).

### 3h. Posterior error probability (PEP)

`PepEstimator.FitDefault` (`Osprey.ML/PepEstimator.cs:67`) fits PEP on the competition
**winners only** using KDE for the target/decoy score densities, Bayes' rule for
`P(decoy | score)`, and isotonic regression (PAVA) for monotonicity (default 1000 bins,
`DEFAULT_N_BINS`). Non-winners get `Pep = 1.0`. For byte-exact cross-impl parity the
winner arrays are re-sorted **base_id-ascending** before the fit, because the KDE sum is
non-associative (`ComputeStreamingCompetitionQvalues`, `PercolatorFdr.cs:977`). PEP is
Sage-derived (MIT-licensed header at `PepEstimator.cs:31`), the same origin as the Rust
implementation.

### 3i. Q-values at four levels

All q-values use the conservative `(decoys + 1) / targets` estimate with a backward
monotonicity sweep (`ComputeQvaluesCore`, `PercolatorFdr.cs:1881`,
`decoyOffset = 1` for conservative). The four levels
(`ScorePopulationAndComputeFdr` / `ComputeStreamingCompetitionQvalues`,
`PercolatorFdr.cs:998-1019`):

| Level | Scope | Method |
|-------|-------|--------|
| Run precursor | within each file, per precursor | `ComputePerRunPrecursorQvalues` |
| Run peptide | within each file, best precursor per peptide | `ComputePerRunPeptideQvalues` |
| Experiment precursor | across files, **by default** the best (max) obs per precursor | `ComputeExperimentPrecursorQvalues` |
| Experiment peptide | across files, **by default** the best (max) obs per peptide | `ComputeExperimentPeptideQvalues` |

The two experiment rows describe the **default** reduction. How a unit's per-run
observations collapse to the one score that competes is selectable - see
[Experiment-wide aggregation](#experiment-wide-aggregation-osprey_experiment_agg)
for the opt-in mean(best-N) alternative.

**Single-file shortcut**: when only one file is present, experiment q-values are a clone
of the run q-values (`PercolatorFdr.cs:682`, `PercolatorFdr.cs:1008`) — no separate
aggregation.

### 3j. Best-of-runs clamp on experiment q-values

Experiment-level FDR competes each precursor's single best observation against a thinner
de-duplicated decoy null, so the raw experiment q can fall **below** every per-run q,
producing reported peptides with no run-level ID line. The clamp floors each entry's
experiment q up to its own best (min-over-runs) **combined** run q
(`runBoth = max(runPrecursorQ, runPeptideQ)`, i.e. `FdrLevel.Both`):

```text
ExperimentPrecursorQvalue <- max(ExperimentPrecursorQvalue, min-over-runs runBoth)  [by EntryId]
ExperimentPeptideQvalue   <- max(ExperimentPeptideQvalue,   min-over-runs runBoth)  [by (ModifiedSequence, IsDecoy)]
```

Both floors key on the target/decoy-specific identity (never the shared `base_id` or bare
sequence), so a decoy's good run cannot lower its paired target's floor. Two identical
implementations exist: the memory-bounded flat form `ClampExperimentQToBestRunFlat`
runs in-pass over the score arrays (`PercolatorFdr.cs:1040`); the resident overload
`PercolatorEngine.ClampExperimentQToBestRun` (`PercolatorEngine.cs:864`) is re-applied
after Stage 6 reconciliation in `SecondPassFdrTask`, because reconciliation resets the run
q-values of moved and gap-filled peaks (issue #4390).

---

## Step 4 — Dual precursor + peptide FDR (the effective q-value)

A precursor must pass at **both** the precursor level (`ModifiedSequence` + `Charge`) and
the peptide level (`ModifiedSequence` only). This is the `FdrLevel.Both` selection —
`max(precursor_q, peptide_q)` — implemented in `FdrEntry.EffectiveRunQvalue` /
`EffectiveExperimentQvalue` (`FdrEntry.cs:136`, `FdrEntry.cs:154`):

```csharp
case FdrLevel.Precursor: return RunPrecursorQvalue;
case FdrLevel.Peptide:   return RunPeptideQvalue;
case FdrLevel.Both:      return Math.Max(RunPrecursorQvalue, RunPeptideQvalue);
```

Which level actually gates the reported output is controlled by `--fdr-level`
(default **Precursor** — see Flags). Note that the best-of-runs clamp above always floors
by run-**Both** regardless of `--fdr-level`, so "reported ⇒ some run has an ID line" holds
even under precursor-level control (`PercolatorEngine.cs:850` comment).

---

## Step 5 — Two-level FDR and multi-file observation propagation

- **Run-level FDR**: each file scored independently; controls the per-file FDR.
- **Experiment-level FDR**: the single best-scoring observation per precursor
  (`ModifiedSequence` + `Charge`) across all files competes; controls the experiment-wide
  FDR. (Default reduction; `OSPREY_EXPERIMENT_AGG` can replace it - see
  [Experiment-wide aggregation](#experiment-wide-aggregation-osprey_experiment_agg).)

After experiment-level FDR determines passing precursors, all per-file target
observations for those precursors are carried into the blib output with the best
experiment q-value propagated to each, so a precursor seen in 3 of 5 files yields 3 blib
entries (each with its own RT boundaries). See `13-blib-output-schema.md` for the nullable
`retentionTime` ID-line semantics.

---

## Experiment-wide aggregation (`OSPREY_EXPERIMENT_AGG`)

> **EXPERIMENTAL, opt-in, off by default.** `max` is the shipped behavior and is
> byte-identical to the committed regression golden. `mean-best-<N>` and the two
> floor toggles below are measurement levers for reproducibility-weighted scoring
> (issue #4484), not supported production settings.

Steps 3i and 5 above reduce a unit's per-run observations to one score before the
experiment-wide target/decoy competition. `OSPREY_EXPERIMENT_AGG` selects that
reduction. `OspreyEnvironment` parses the flag once at process start; an
out-of-range or malformed value normalizes to `max` **with a warning**
(`ExperimentAggUnrecognized`), because the whole point of the flag is A/B
measurement and a typo that silently ran the default would be recorded as a
mean(best-N) result rather than failing.

| Value | Experiment-wide precursor score | Output |
|---|---|---|
| `max` (default) | the unit's single best (max) per-run score | byte-identical to the golden |
| `mean-best-<N>` | the mean of the unit's best N per-run scores, missing runs filled with a decoy-derived floor | q-values change |

### `max` (default)

Each precursor, and each peptide, keeps its single best-scoring observation across
runs and that score competes against the decoy null. The mean(best-N) code is never
entered: the effective score array **is** the raw score array
(`PercolatorQValues.cs`, `effScores == scores`), so a default run costs nothing and
stays byte-identical to the committed golden.

### `mean-best-<N>`

The experiment-wide **precursor** score becomes the mean of that precursor's best N
per-run scores (`TargetDecoyCompetition.ComputeBaseIdMeanBestN`, per-group
accumulator `MeanBestNAcc`). Stage-4 dedup guarantees at most one entry per
`base_id` per file, so a group's members are its per-run scores and the top N are
the N best runs. A unit detected in only k < N runs has its remaining N - k slots
filled with a **missing-run floor** estimated from the decoy score distribution:

```text
score = (sum of the k best per-run scores + (N - k) * floor) / N
```

Larger N rewards detection in more runs, driving the ranking toward the ">= N runs"
reproducibility frontier. Every row of a `(base_id, side)` group receives the same
aggregate, so the existing max-per-`base_id` experiment competition becomes a no-op
reduction over that value (precursor = mean(best-N)), and
`PercolatorSampling.BestPrecursorPerPeptide` rolls it up to **peptide by MAX** over
the peptide's precursors.

**Decoys are aggregated identically**, so the null stays honest. That is what makes
this the symmetric sensitivity lever, as opposed to the target-conditioned
second-pass modes in [12-second-pass-fdr.md](12-second-pass-fdr.md).

Protein-level FDR is **not** re-ranked on the aggregate: `ProteinFdr`
(`CollectBestPeptideScores`) still scores each group by the maximum **raw**
per-peptide SVM discriminant, which never sees the aggregated array. mean(best-N)
reaches protein FDR only indirectly, through which peptides pass the experiment
q-value gate.

A single-file run is unaffected: with one observation per group the aggregate is the
uniform monotonic transform `x -> (x + (N - 1) * floor) / N`, so the ranking and every
q-value are identical to `max` (and the single-file shortcut in Step 3i clones the run
q-values anyway).

#### N is bounded to [2, 64]

N is read from the flag value and must be an unsigned decimal integer in
`[2, OspreyEnvironment.MEAN_BEST_N_MAX]`, where `MEAN_BEST_N_MAX = 64`. Parsing is
strict (`NumberStyles.None`): no leading sign and no surrounding whitespace, so
`mean-best-+3` and `mean-best- 3` are rejected rather than becoming a second
spelling of the same arm in an A/B log. Anything else - a bad prefix, N < 2, N > 64 -
normalizes to `max` and warns.

The cap exists because two unbounded failures were reachable. An N-wide accumulator
is allocated per `(base_id, side)`, so `mean-best-1000000` allocates gigabytes and
aborts seconds into Stage 5. And for any N at or above the largest observation count
every unit is floor-filled, which saturates the statistic - the aggregate is
`(S - L * floor) / N + floor`, whose **ranking no longer depends on N at all** - so
the arm would be recorded as distinct while measuring the same thing. 64 is far above
any plausible reproducibility frontier for a DIA experiment.

#### N must not exceed the number of runs

For that same saturation reason, an analysis with fewer runs than N is **refused**,
not run (`OspreyEnvironment.ValidateExperimentAggSettings(fileCount)`). The check runs
from one helper at two sites: at startup in `Program.ValidateArgs` before any I/O (a bad
value costs a second instead of the hours a large run spends reaching Stage 5), and again
at the Stage-5 consuming site in `FirstPassFdrTask.Run`.

The two sites are fed **different counts on purpose**, and neither subsumes the other.
Startup counts the files named on the command line; Stage 5 counts the files that actually
produced scored entries, which is smaller when a file fails to score or yields nothing. So
a run can pass at startup and still be refused at Stage 5 - that is the second check doing
its job, not a drift to be eliminated. Conversely `FirstPassFdrTask.Run` is skipped on
`--task SecondPassFDR`, on a Rehydrate and on any warm resume, which is exactly why the
startup check exists. The bound itself is also approximate: the statistic saturates at the
largest per-unit observation count, which is at most the file count and usually less, so
some N below the file count are already saturated and are not refused.

Because the Stage-5 check can throw, it runs **before** that task deletes the validity
sidecars of the outputs it is about to write. Otherwise an argument error would destroy the
cached Stage-5 state of a run that computed no FDR at all, and the operator would pay for a
full recompute after fixing a typo - concentrated precisely on the sweep workflow, since the
arm is part of the validity key and a flipped arm therefore always takes the compute path.

Every check in that helper is gated on the aggregation being **engaged**. With
`OSPREY_EXPERIMENT_AGG` unset none of these variables is read, so an ordinary run
that merely inherited an exported sweep variable is untouched.

### The missing-run floor

The floor is the expected score of a **non-detection**, estimated from the decoy
(null) score distribution and applied as one global scalar to the `N - k` missing
slots of under-detected units:

| Setting | Missing-run floor |
|---|---|
| neither variable set (default) | decoy **MEDIAN** |
| `OSPREY_MEANBEST2_FLOOR_MEAN=1` | decoy **MEAN** |
| `OSPREY_MEANBEST2_FLOOR_PCT=<0..100>` | that **percentile** of the decoy scores (linear interpolation, 50 = median); a low value is a harder reproducibility cut |

The median default is deliberate: the typical null score is negative, whereas 0 is the
SVM decision boundary and would drag a missing unit **up** toward detection. With no
decoys at all the floor is 0. Non-finite decoy scores are excluded from the sample on
both code paths.

**The `MEANBEST2` name is historical** - it predates the best-2 to best-N
generalization. Both toggles apply at every N.

Two refusals (both from `ValidateExperimentAggSettings`, both gated on the
aggregation being engaged):

- **Setting BOTH is refused.** They are not composable: `FLOOR_MEAN` wins and the
  percentile is never consulted, so an operator sweeping `OSPREY_MEANBEST2_FLOOR_PCT`
  with a stale `OSPREY_MEANBEST2_FLOOR_MEAN=1` still exported would log a percentile
  arm while measuring the decoy mean.
- **`OSPREY_MEANBEST2_FLOOR_PCT` outside [0, 100]** (or NaN) is refused **at
  startup**, not where the floor is computed: the streaming estimator reaches its
  percentile only at the very end of Stage 5, hours into a large run, and the resident
  twin would silently clamp the same value.

**Known approximation (documented and bounded, not a defect).** The two floor
estimators are the same statistic computed two ways. The resident path
(`TargetDecoyCompetition.ComputeFloorFromDecoyScores`) sorts the exact decoy score
list and interpolates between two observed values. The memory-bounded streaming path
(`StreamingFdr.StreamingDecoyFloor`) accumulates a fixed-width histogram (200000 bins
over [-100, 100], **bin width 1e-3**) and interpolates uniformly inside the straddling
bin.

The two floors agree to within **bin width + the local spacing of the decoy scores
around the quantile**, which is the bound
`FdrTest.TestStreamingMeanBestNFloorPathMatchesResident` asserts. The spacing term is
not slack: where the decoys are sparser than the bins the streaming estimator cannot
reach across the gap to the next observation, so the disagreement is set by the DATA
SPACING rather than by the bin width, and on a deliberately sparse fixture it exceeds
the bin width by roughly 18x. In production the decoy count is in the millions over a
narrow score range - several per bin - so the spacing term vanishes and the bound does
collapse to about the bin width.

Neither estimator can return a value outside the observed decoy range: the streaming
path answers every out-of-histogram case with the smallest or largest score it
actually saw, mirroring what the resident path's sorted-list lookup would return. That
matters because a floor ABOVE every real score would invert the feature, PROMOTING
under-detected units instead of demoting them.

The `FLOOR_MEAN` branch is an exact running sum on both paths and does not drift, and
the floor only affects units detected in fewer than N runs.

### First pass only, and the second-pass interaction

`OSPREY_EXPERIMENT_AGG` is a **first-pass score by definition**. Both score passes
gate the aggregation on the pass label (`applyExperimentAgg: passLabel ==
FIRST_PASS_LABEL` in `PercolatorEngine`), so **the second pass never re-aggregates**.
Two premises break on the post-reconciliation survivor pool: gap-fill rows are
appended there, so a group's observation count is inflated by fabricated detections
and "runs detected" starts counting non-independent evidence, inverting the
reproducibility metric the feature rests on; and the decoy floor would come from the
small, compaction-enriched survivor decoy set instead of the full null.

That makes the second-pass q-value mode
([`OSPREY_PASS2_QVALUE`](12-second-pass-fdr.md)) part of the arm:

| `OSPREY_PASS2_QVALUE` | Behavior after a mean(best-N) first pass |
|---|---|
| `transfer` | **The compatible mode.** Carries the first-pass q through unchanged, so the reported experiment q stays mean(best-N). |
| `transfer-compete` | **Refused** (`Pass2FdrSidecar` throws). It rewrites every survivor's experiment q from a MAX-aggregated competition, making a reproducibility-weighted run indistinguishable from a default run in its own output. |
| `protein-compact` (default) | **Refused** (but see the caveat below). Worse than uniform: on-stratum survivors would get the MAX-aggregated value while off-stratum survivors keep their first-pass mean(best-N) q, giving one reported column with two statistics and no way for a consumer to tell which row used which. |

**Because `protein-compact` is the DEFAULT, a mean(best-N) arm must set
`OSPREY_PASS2_QVALUE=transfer` explicitly or the run aborts at SecondPassFDR.** This is
deliberate: the alternative - silently using `transfer` whenever the first pass was mean(best-N) -
would make the effective default depend on another variable, which is harder to reason about than
a loud failure whose message names the fix.

> **Caveat: `protein-compact` + `OSPREY_PROTEIN_COMPACT_RETRAIN=1` is NOT refused.** The refusal
> lives in the frozen-model recompute, and that A/B lever deliberately bypasses it to retrain
> instead - so the combination retrains and silently reports a MAX-aggregated experiment q. This is
> left as-is rather than guarded because the combination is a three-way diagnostic opt-in, and
> these environment variables are development instrumentation rather than a supported interface
> (see `ai/docs/osprey-development-guide.md`). Do not read the "Refused" row above as covering it.

The refusal gates on the arm the **first pass recorded** - persisted as
`ExperimentAgg` in the per-file `<stem>.1st-pass.model.json` sidecar
(`FirstPassModelIO`) - not on the live process environment, because a
`--task SecondPassFDR` node reloads the frozen model from disk and never
trained pass 1. A sidecar written before arm recording reports null, which means
UNKNOWN rather than "max": the refusal then infers the arm from this process's
environment and says so in the message. Making the streamed second-pass competition
aggregate-aware is the real fix and is deliberately deferred (it depends on the
gap-fill run-count exclusion, issue #4511).

### Operator checks

- **The run states its own arm.** The startup settings block always prints one line
  beginning `Experiment aggregation:`:

  ```text
  Experiment aggregation: max (default - best observation per unit)
  Experiment aggregation: mean-best-3 ACTIVE - experiment-wide precursor score is the mean of its best 3 per-run scores; missing-run floor = decoy MEDIAN (default)
  ```

  It is printed unconditionally, active or not, because the dominant real failure of
  an environment-variable flag is that the variable never reached the process (a
  `Start-Process` without the parent environment, an HPC job spec, a scheduled launch,
  a service account) - and in the output itself, unset is indistinguishable from a
  typo that normalized to the default.
- **Flipping the arm invalidates cached results.** `ExperimentAggValidityKeySuffix()`
  is appended to the validity keys of `FirstPassFdrTask` (Stage 5), `PerFileRescoreTask`
  (Stage 6) and `SecondPassFdrTask`, and covers both floor toggles as well as
  the arm, so a warm rerun in the same output directory **re-runs** those stages
  instead of reusing the other arm's cached q-values, sidecars and `.blib`. The suffix
  is empty when the aggregation is off, so directories produced by default runs
  (including any predating the feature) stay valid.

---

## Step 6 — Second pass (Stage 7)

`Pass2FdrSidecar` / `SecondPassFdrTask` (`--task SecondPassFDR`) reload the reconciled
`.scores.parquet` entries and re-run the identical Percolator core with `passLabel =
"Second-pass"` (`Pass2FdrSidecar.cs:521`), writing per-file `.2nd-pass.fdr_scores.bin`
sidecars so reruns can skip SVM training. Only `FdrMethod.Percolator` is supported in the
SecondPassFDR second pass — any other method throws
(`Pass2FdrSidecar.cs:530`). The second-pass q-values are authoritative for blib output;
the best-of-runs clamp is re-applied afterward.

---

## Step 7 — Protein-level FDR (brief; see `08-protein-parsimony.md`)

Protein parsimony always runs. Two-pass picked-protein FDR (Savitski 2015) writes
`FdrEntry.RunProteinQvalue` (first pass, `ProteinFdr.cs:826`) and
`FdrEntry.ExperimentProteinQvalue` (second pass, authoritative, `ProteinFdr.cs:781`).
The ranking score is the maximum peptide SVM discriminant per group; protein-level PEP is
intentionally not computed. **However**, unlike Rust, the C# `FdrLevel` enum has no
`Protein` variant, so protein q-values are computed and reported in the protein report but
**cannot gate the blib output** from the CLI — see Divergences.

---

## Flags and switches

All defaults are from `Osprey.Core/OspreyConfig.cs` and `Osprey/OspreyCommandArgs.cs`.

| Flag / field | Default | Effect on this stage |
|--------------|---------|----------------------|
| `--fdr-method {percolator\|gbdt\|simple}` | `percolator` (`OspreyConfig.cs:188`) | Selects the FDR engine. `gbdt` swaps a gradient-boosted-tree classifier into the Percolator framework (**C#-only**; `fasttree` is a deprecated alias). `mokapot` exists in the enum but is **not accepted** by the CLI (`OspreyCommandArgs.cs:120`). |
| `OSPREY_GBT_MAX_ITERATIONS` / `OSPREY_GBT_*` | `30` / classifier defaults | Iteration cap and hyperparameters for `--fdr-method gbdt` (max-depth, n-trees, learning-rate, subsample, λ/α/γ, min-child-weight, inner folds). Ignored by the SVM path. |
| `--fdr-level {precursor\|peptide\|both}` | `precursor` (`OspreyConfig.cs:284`) | Which q-value gates reported output via `EffectiveRunQvalue`/`EffectiveExperimentQvalue`. `protein` is **not** a valid value (`OspreyCommandArgs.cs:138`). |
| `--run-fdr <threshold>` | `0.01` (`OspreyConfig.cs:120`) | Run-level q-value threshold; also the Percolator `TrainFdr`/`TestFdr` (`PercolatorEngine.cs:302`). |
| `--experiment-fdr <threshold>` | `0.01` (`OspreyConfig.cs:123`) | Experiment-level q-value threshold. |
| `--reconciliation-compaction-fdr <threshold>` | `0.01` (`OspreyConfig.cs:135`) | Peptide q-value gate for first-pass compaction; loosen (e.g. 0.05) to broaden the reconciliation pool. Protein rescue is additive. |
| `--protein-fdr <threshold>` | unset → `EffectiveProteinFdr = 0.01` (`OspreyConfig.cs:271`) | Protein machinery **always runs** regardless; this only sets the passing-group threshold. |
| `--shared-peptides {all\|razor\|unique}` | `all` (`OspreyConfig.cs:274`) | Protein-level shared-peptide handling (see `08-protein-parsimony.md`). |
| `--write-pin` | off (`OspreyCommandArgs.cs:196`) | Writes PIN files for external tools; diagnostic only — the FDR engine does not consume them. |
| `--fdrbench <tsv>` | off (`OspreyCommandArgs.cs:179`) | Emit an FDRBench-compatible input TSV (entrapment true-FDR). Level taken from `--fdr-level`. |
| `--fdrbench-per-run` | off (`OspreyCommandArgs.cs:181`) | One row per (precursor, run) with run-level q-values; default is one row per precursor with experiment q-values. |
| `--fdrbench-pass {1\|2\|both}` | `2` (`OspreyCommandArgs.cs:183`) | Which pass to emit: 2 = post-compaction second-pass survivors; 1 = full pre-compaction first-pass pool; both = both with `.pass1`/`.pass2` suffixes. |
| `OSPREY_EXPERIMENT_AGG` | `max` | **Experimental.** Experiment-wide aggregation: `max` (default, byte-identical golden) or `mean-best-<N>` with N in [2, 64]. Out-of-range / malformed values fall back to `max` with a warning; N above the run count is refused. First pass only. See [Experiment-wide aggregation](#experiment-wide-aggregation-osprey_experiment_agg). |
| `OSPREY_MEANBEST2_FLOOR_MEAN` | off (median) | **Experimental.** Missing-run floor = decoy MEAN instead of the default decoy MEDIAN. Read only under `mean-best-<N>`; the `MEANBEST2` name is historical and it applies at every N. |
| `OSPREY_MEANBEST2_FLOOR_PCT` | unset (median) | **Experimental.** Missing-run floor = this percentile (0-100) of the decoy scores; a low value is a harder reproducibility cut. Refused outside [0, 100], and refused together with `OSPREY_MEANBEST2_FLOOR_MEAN`. |
| `--model-diagnostics` | off | Also collects per-feature target/decoy histograms + feature-contribution report (`CollectFeatureHistograms`, `PercolatorEngine.cs:310`); forces the resident first-pass pool. Byte-neutral when off. |
| `--task {PerFileScoring\|FirstPassFDR\|PerFileRescoring\|SecondPassFDR}` | (single-process) | HPC split. The internal `HpcTask` enum values are `PerFileScoring, FirstPassFdr, PerFileRescore, SecondPassFdr` (`OspreyConfig.cs`); note the enum spells `PerFileRescore` where the CLI takes `PerFileRescoring`. See `15-hpc-scoring-split.md`. |

Internal Percolator constants (not CLI-exposed; `PercolatorConfig` ctor
`PercolatorFdr.cs:115`): `MaxIterations = 10`, `NFolds = 3`, `Seed = 42`,
`CValues = {0.001,0.01,0.1,1,10,100}`, `MaxTrainSize = 300000`.

**Diagnostic env vars** (Stage 5 dumps, carried in via `PercolatorDiagnosticsConfig`,
never read directly by the engine): `OSPREY_DUMP_STANDARDIZER`, `OSPREY_DUMP_PERC_INPUT`,
`OSPREY_DUMP_SUBSAMPLE`, `OSPREY_DUMP_SVM_WEIGHTS`, each with an `*_ONLY` variant that
aborts after the dump (`PercolatorFdr.cs:302-534`). `OSPREY_DUMP_LDA_SCORES` affects the
calibration LDA, not Percolator.

---

## Divergences from the Rust documentation

- **[INTENTIONAL-CSHARP-DESIGN] Native managed Percolator replaces external Mokapot** -
  Rust doc says three FDR methods exist (native Percolator, external Mokapot via
  `pip install mokapot` + PIN files + subprocess, and Simple) and documents Mokapot's
  two-step `--save_models`/`--load_models`/`--aggregate` flow, ROC-AUC pre-competition,
  and `--subset_max_train` memory logic. C# ships only the native Percolator and Simple:
  `--fdr-method` accepts `percolator | simple`; the `FdrMethod.Mokapot` enum value is
  never wired to the CLI and there is no MokapotRunner, no PIN round-trip on the default
  path, and no Python dependency. Evidence: `Osprey/OspreyCommandArgs.cs:120`,
  `Osprey.Core/OspreyConfig.cs:422`. Behavior/outputs of the retained engine match Rust.
  Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] No `FdrLevel::Protein`; `--fdr-level protein` is
  unreachable in C#** - Rust doc §"FDR Filtering Level" documents four modes
  `{precursor, peptide, protein, both}` and `--fdr-level protein` output filtering that
  reads `experiment_protein_qvalue`. The C# `FdrLevel` enum has only
  `{Precursor, Peptide, Both}` (`OspreyConfig.cs:411`) and `--fdr-level` accepts only
  `precursor | peptide | both` (`OspreyCommandArgs.cs:138`).
  `FdrEntry.EffectiveRunQvalue`/`EffectiveExperimentQvalue` have no `Protein` case and
  would throw `ArgumentOutOfRangeException` on one (`FdrEntry.cs:136`). Protein q-values
  are still computed and written to the protein report, but there is **no CLI path to
  gate the blib output by protein-level FDR**. Two stale in-code comments still reference
  "`--fdr-level protein` output filtering" (`OspreyConfig.cs:258`, `SecondPassFdrTask.cs:157`)
  even though the mode is unreachable. Evidence: `Osprey.Core/OspreyConfig.cs:411`,
  `Osprey/OspreyCommandArgs.cs:138`, `Osprey.Core/FdrEntry.cs:136`. Severity: major.

- **[STALE-RUST-DOC] Default `--fdr-level` is Precursor, not Peptide** - Rust doc §"FDR
  Filtering Level" states the default is `Peptide` ("`fdr_level: Peptide  # default`").
  C# defaults to `FdrLevel.Precursor`, and its own comment states this matches the current
  Rust `FdrLevel::default() = Precursor` and that a prior `Both` default was a
  cross-impl-corrupting bug. The Rust doc's "Peptide default" prose is stale relative to
  the Rust config. Evidence: `Osprey.Core/OspreyConfig.cs:284`. Severity: minor.

- **[C#-ONLY ADDITION] `gbdt` FDR method exists only in C#** - The C# `FdrMethod`
  enum is `{Percolator, Mokapot, Simple, Gbdt}` and `--fdr-method gbdt` (deprecated
  alias `fasttree`) selects a gradient-boosted-tree classifier inside the Percolator
  framework (`Osprey.ML/GradientBoostedTrees.cs`; see "GBDT FDR" above). **The Rust
  reference has no GBDT scorer** — grepping the Rust crates for `gbdt`/`GradientBoost`
  returns nothing — so this is a C# addition *beyond* the reference engine, not a port,
  and carries no cross-impl parity claim. `--fdr-method percolator` remains the default
  and the parity-gated path. Evidence: `Osprey.Core/OspreyConfig.cs:446-455`,
  `Osprey/OspreyCommandArgs.cs:120`. Severity: info.

- **[UNVERIFIED] Simple FDR scores on `coelution_sum`, not a ROC-AUC-selected best
  feature** - Rust doc §"Simple FDR" says the method "applies target-decoy competition
  directly on the best single feature (selected by ROC AUC)". The C# `RunSimpleFdr` scores
  directly by `e.CoelutionSum` (PIN feature 0) with no ROC-AUC selection
  (`PercolatorEngine.cs:359`). Whether the current Rust Simple path also just uses
  `coelution_sum` (making the doc stale) or truly selects by ROC AUC was not confirmed
  against Rust source. Simple is a non-default baseline method. Evidence:
  `Osprey.FDR/PercolatorEngine.cs:350`. Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] Streaming-only, matching Rust's v26.7.0 change** - The
  Rust doc notes the direct (non-streaming) path was removed in v26.7.0 and Percolator
  "always streams". C# matches: `DispatchSvm` always takes the streaming path and the
  in-code comments state the former sub-threshold direct branch was removed for parity
  (`PercolatorEngine.cs:336`, `PercolatorEngine.cs:256`). This is agreement, recorded for
  completeness. Evidence: `Osprey.FDR/PercolatorEngine.cs:336`. Severity: info.

Everything else verified matches the Rust documentation step for step: the semi-supervised
linear-SVM algorithm (standardize → best-per-precursor dedup → peptide-grouped subsample
at 300K → 3-fold peptide-grouped CV → iterative training with C grid search and
`MIN_POSITIVE = 50` relaxation → Granholm cross-fold calibration → KDE+isotonic PEP on
winners → four-level conservative `(decoys+1)/targets` q-values), the base_id `0x7FFFFFFF`
pairing, the dual precursor+peptide `max` rule, the best-of-runs experiment clamp, the
single-file experiment shortcut, multi-file observation propagation, and the two-pass
(Stage 5 / Stage 7) architecture with `.2nd-pass.fdr_scores.bin` sidecars.
