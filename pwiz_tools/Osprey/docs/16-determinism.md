# 16. Determinism (C#)

> Pipeline stage: Cross-cutting. C# port of Rust docs/09-determinism.md. Corresponds to Rust osprey determinism patterns.

The C# Osprey port is designed to produce **bit-identical results** across runs
on the same input, regardless of thread scheduling, `--threads`/`--parallel-files`
count, or which .NET runtime target (net472 vs net8.0) executes it. This is the
same guarantee the Rust tool makes, and it is enforced by the same standing
oracle described at the end of this doc: `regression.ps1` compares whole `.blib`
outputs (and a protein-FDR dump) at a `1e-9` tolerance. This document catalogs
the determinism patterns the C# code actually uses, in the order the pipeline
relies on them, and anchors each to concrete C# code.

The determinism-critical invariant is broader than "same answer twice": the
regression gate also requires that a **resumed** run (rehydrated from on-disk
Parquet/sidecars) and a **distributed 4-`--task`** run reproduce the
straight-through result exactly (see 14-intermediate-files.md and
15-hpc-scoring-split.md). That means the deterministic entry ORDER established
during scoring must survive a Parquet round-trip and a cross-process boundary,
not just an in-memory sort.

## Sources of non-determinism in the .NET port

The Rust doc lists four hazards (HashMap iteration order, Rayon collection
order, floating-point accumulation order, RNG). The C# port faces the direct
.NET analogs:

1. **`Dictionary<K,V>` / `HashSet<T>` iteration order** — .NET does not
   guarantee iteration order, and for reference-type keys (notably `string`)
   the per-process randomized hash seed makes `HashSet<string>` enumeration
   order vary across processes. Every collect-from-dictionary site must sort
   before use.
2. **`Parallel.For` / PLINQ completion order** — results must be placed by
   index or sorted afterward, never appended in completion order.
3. **Floating-point accumulation order** — `a + (b + c)` differs from
   `(a + b) + c`; SIMD lane reduction differs from a scalar left-fold. The port
   holds these within the `1e-9` cross-impl gate (see "Float stability").
4. **Random number generation** — all sampling/shuffling uses a fixed-seed
   deterministic PRNG (`XorShift64`, seed 42).

## Step 1 — Total ordering of doubles (`TotalOrder`)

`Osprey.Core/TotalOrder.cs` is the C# equivalent of Rust's `f64::total_cmp`. It
maps a `double` to a sign-flipped `long` key (`TotalOrder.Key`,
`Osprey.Core/TotalOrder.cs:55`) so the natural `long` ordering reproduces the
IEEE 754-2008 §5.10 total order: `-0.0` sorts below `+0.0` and NaNs order
consistently. `TotalOrder.Comparer` (`:47`) pairs with LINQ
`OrderBy`/`OrderByDescending` (stable per the .NET contract) to reproduce Rust's
`sort_by(... .total_cmp(...))` byte-for-byte; `TotalOrder.Greater` (`:67`) is
the boolean form used by the main-search peak-ranking tie-break. The header note
records that this transform was relocated verbatim out of `AbstractScoringTask`
(two former copies) with the arithmetic unchanged, so cross-impl parity is
unaffected.

## Step 2 — Deterministic parallel scoring (place-by-index, not append)

The per-window main search runs under `Parallel.For`
(`Osprey.Scoring/ScoringPipeline.cs:269`). Each window writes its result list to
a pre-sized slot `windowResults[wIdx]` (`:290`) keyed by window index, and the
final flatten iterates `wIdx` in order (`:301-305`). The comment at `:249-251`
states the intent explicitly: "Per-window results land in `windowResults[wIdx]`
so the final flatten is deterministic in window order regardless of completion
order." This is the C# realization of the Rust "sort after parallel collection"
pattern — implemented as an indexed scatter so no post-sort is even needed for
the flatten itself.

Within a window, spectra are sorted by `(RetentionTime, ScanNumber)` before XIC
extraction (`Osprey.Scoring/CoelutionScorer.cs:97`), `ScanNumber` giving a unique
total order so the sort never ties.

## Step 3 — Sort after dictionary collection

Every site that collects results out of a `Dictionary`/`HashSet` sorts into a
stable key order before the values are used downstream:

- **Target-decoy pair dedup**: after HashMap-based pairing, entries are sorted by
  `EntryId` (`Osprey.Scoring/ScoringPipeline.cs:579`; the comment at `:567`
  notes this makes order "deterministic regardless of Dictionary" enumeration).
- **Competition winners** (`PercolatorFdr.CompeteAll`): winners are sorted by
  **score descending, then `base_id` ascending**
  (`Osprey.FDR/PercolatorFdr.cs:1599-1602`). `base_id` = `entry_id & 0x7FFFFFFF`
  (`:258`) is unique per winner, so the secondary key makes the comparator total
  — the note at `:1602` records that the unique `base_id` tie-break is what
  licenses `Array.Sort` (an unstable sort) here.
- **Best-per-precursor selection** result is sorted by entry index
  (`Osprey.FDR/PercolatorFdr.cs:2604`).
- **Protein picked-FDR winners** are sorted by score descending, tie-broken by a
  canonical sorted-accessions `SortKey` string, not by the HashMap-order
  `GroupId` (`Osprey.FDR/ProteinFdr.cs:628-636`).

The double-counting dedup similarly re-sorts by `EntryId`
(`Osprey.Scoring/ScoringPipeline.cs:579`), and the reconciliation CWT/forced
lists sort by `EntryId` (`Osprey.Tasks/FirstPassFdrTask.cs:1143-1144`).

## Step 4 — Deterministic fold assignment (round-robin over sorted peptide groups)

Cross-validation fold assignment is **not** a hash-modulo of the sequence. Both
the Percolator SVM and the calibration LDA build peptide groups keyed by the
target sequence (via `base_id`), sort the distinct group keys with
`StringComparer.Ordinal`, and round-robin `i % nFolds` over the sorted keys:

- **Percolator SVM**: `PercolatorFdr.CreateStratifiedFoldsByPeptide`
  (`Osprey.FDR/PercolatorFdr.cs:2453`) — groups by target peptide (`:2456-2490`),
  `sortedKeys.Sort(StringComparer.Ordinal)` (`:2494`), `fold = i % nFolds`
  (`:2499`).
- **Calibration LDA**: `CalibrationScorer.CreateStratifiedFoldsByPeptide`
  (`Osprey.Scoring/CalibrationScorer.cs:409`) — target and decoy groups sorted
  by `string.CompareOrdinal` (`:435,:437`), each round-robined independently
  (`:439-452`). The doc-comment at `:402-407` states it is a "Direct port of
  `create_stratified_folds_by_peptide` in calibration_ml.rs" that "sorts each
  group list by ordinal sequence, and assigns peptide groups to folds."

This grouping keeps the cross-validation invariants: all charge states and the
paired decoy of a peptide (same `base_id` → same target sequence group) go to the
same fold, so target-decoy pairs are never split across the train/test boundary
(see 07-fdr-control.md).

Because the assignment is a pure function of the ordinal-sorted group keys, it is
deterministic across runs and thread counts with **no** PRNG involvement.

## Step 5 — Seeded PRNG (`XorShift64`, seed 42)

The only randomness in the pipeline is training-subset shuffling, and it uses a
fixed-seed deterministic PRNG. `XorShift64` (`Osprey.ML/LinearSvmClassifier.cs:266`)
matches the Rust generator exactly (`x ^= x << 13; x ^= x >> 7; x ^= x << 17`).
`PercolatorConfig.Seed` defaults to `42` (`Osprey.FDR/PercolatorFdr.cs:64,:121`),
and is threaded through both the direct and streaming Percolator paths
(`PercolatorEngine.cs:566,:797`). The SVM's per-iteration index permutation is a
Fisher-Yates shuffle over the same PRNG (`LinearSvmClassifier.FisherYatesShuffle`,
`:668`), whose header note states it "Matches the Rust implementation exactly."

## Step 6 — Deterministic subsampling

When the deduped training set exceeds `MaxTrainSize`, the peptide-grouped
subsample is also deterministic: `SubsampleByPeptideGroup`
(`Osprey.FDR/PercolatorFdr.cs:2611`) builds peptide groups, sorts the group keys
with `StringComparison.Ordinal` (`:2654`), then applies a Fisher-Yates shuffle
seeded from the same `seed` (`:2656-2666`) before greedily taking whole groups up
to the budget and re-sorting the selected indices (`:2677`). Subsampling operates
on whole `base_id` groups (targets + decoys + all charges together), not
individual entries, preserving the cross-validation grouping invariant. The
prior `SelectBestPerPrecursor` dedup (`:2569`) itself sorts its output
(`:2604`), so the subsample input order is stable. `BuildTrainingSubset`
(`:2522`) is the single owner both the direct and streaming Percolator paths call
so they select **identical** subsets for identical input.

## Step 7 — Float stability within the 1e-9 gate

The port cannot use BLAS; the SVM hot loop is hand-vectorized with
`System.Numerics.Vector<double>` (`Osprey.ML/LinearSvmClassifier.cs:516-604`,
see 17-vectorization.md). This changes the floating-point reduction ORDER
relative to Rust's strict scalar left-fold: the SIMD path accumulates per-lane
partial sums (lane stride = `Vector<double>.Count`, 4 on AVX2, 8 on AVX-512, 2 on
ARM NEON) then horizontal-sums via `Vector.Dot`. The extensive note at `:531-545`
records the consequence precisely: per-op drift is sub-ULP, cumulative drift over
the `n*iter` dot products at `p=21` features stays inside the `1e-9` cross-impl
parity gate, but that headroom **implicitly assumes a lane-stride-stable
runtime** — a production CPU/runtime swap that changes the lane stride shifts the
divergence pattern, and the documented bisection is to force the scalar tail
(set `vecSize > cols`) to confirm.

Elsewhere the port keeps scalar left-fold sums (`MlMath.Norm`/`Mean`/`Std`,
`Osprey.ML/MlMath.cs:39-71`; `FeatureStandardizer.Fit`,
`Osprey.ML/LinearSvmClassifier.cs:314`) to match Rust's accumulation order. The
port also avoids nondeterministic parallel float reduction: the SVM training and
scoring reductions are per-row and per-lane deterministic, not a thread-order
`Parallel` sum.

## Step 8 — Canonical entry order across Parquet and process boundaries

The deterministic entry order established during scoring must be reproduced after
a rehydrate, because the resume and HPC-chain legs of the regression gate depend
on it. The scoring task orders entries and writes them to the per-file
`.scores.parquet` in that order; on any warm/resume path
`PerFileRescoreTask.SortFileEntriesCanonical` (`Osprey.Tasks/PerFileRescoreTask.cs:1301`)
re-imposes the exact `(EntryId, Charge, ScanNumber, ParquetIndex)` order a cold
run establishes, with `ParquetIndex` as a unique terminal key so the sort never
ties (`:1306-1315`). The comment at `:1287-1299` explains why this is applied to
**every** file (even no-work files with no reconciled Parquet): otherwise
`SecondPassFDR`'s `BuildSharedBoundaries` could iterate a different order and, on a
q-value tie between charge states, pick a different shared `(modseq, file)`
boundary. Parquet preserves exact IEEE-754 values, so a rehydrated entry is
bit-identical to the in-memory original (see 14-intermediate-files.md).

The PEP estimator is fed a `base_id`-ascending-sorted union so its
non-associative KDE sum is order-stable (`Osprey.FDR/PercolatorFdr.cs:934,:959-977`),
and experiment-level q-values are propagated through a `base_id`-keyed map
(`:2342-2352`) rather than dictionary iteration.

## Step 9 — Razor shared-peptide assignment

`SharedPeptideMode.Razor` (`Osprey.FDR/ProteinFdr.cs:432-467`, off by default —
default is `All`) resolves each shared peptide to a single protein group. The C#
implementation collects the shared peptides (`:434-439`) and, for each, picks the
group with the most unique peptides (tie-break lowest `GroupId`), mutating the
winning group's unique set as it goes (`:441-465`). See the divergence note below:
this is a single-pass greedy, which differs from the iterative greedy set cover
the Rust doc describes, and its input order derives from `HashSet<string>`
enumeration. The default `All` mode and the `Unique` mode do no
order-dependent reassignment. Protein parsimony grouping itself is made
deterministic by ordinal-sorted set keys and a descending set-size sort with a
canonical `SortKey` tie-break (`ProteinFdr.cs:255,:288,:628-636`); see
08-protein-parsimony.md.

## Step 10 — Outer file parallelism does not affect results

`--parallel-files` (outer, across-files) and `--threads` (inner, per-file) change
only scheduling, never output. `FileParallelismResolver.Resolve`
(`Osprey.Core/FileParallelism.cs:122`) resolves a concurrent-file count from the
CLI request, `OSPREY_MAX_PARALLEL_FILES`, free RAM, and core count, but each file
is scored independently into its own Parquet cache in a deterministic order, and
the FirstPassFDR and SecondPassFDR stages sort by stable keys. The reconciliation re-scoring per-file loop
under `PerFileRescoreTask` (`Osprey.Tasks/PerFileRescoreTask.cs:576`) writes each
file's result independently and SecondPassFDR re-imposes the canonical order
(Step 8), so a run with `--parallel-files 8` and a sequential run produce
identical blibs — which is exactly what the regression gate asserts.

## The determinism oracle — `regression.ps1` at 1e-9

`regression.ps1` (repo root of the Osprey C# tree) is the standing determinism
gate; it has no Rust equivalent (the Rust project used inline unit tests). It
runs the full pipeline on the Stellar (`--resolution unit`) and Astral
(`--resolution hram`) reference datasets and enforces three complementary
`1e-9`-tolerance comparisons (`regression.ps1:14-36,:114`):

- **mode 1 — vs committed golden**: straight-through output compared to a
  committed text golden (`osprey-regression.data`): the Stage 7 protein-FDR dump,
  a deterministic precursor subset, and a full-set summary, all at `1e-9`
  (`:16-20,:490-500`). This is the user-facing correctness gate; the golden is
  refreshed only with `-CreateGolden` on a reviewed behavior change.
- **mode 2 — resume == straight-through**: the Stage 5 join + blib are
  invalidated and the same command re-run so the rehydrate paths fire; the resume
  blib must equal the cold blib at `1e-9` (`:21-25,:524-543`). This is what makes
  Step 8's canonical-order-after-rehydrate essential.
- **mode 3 — HPC 4-task chain == straight-through**: the distributed
  `PerFileScoring → FirstPassFDR → PerFileRescoring → SecondPassFDR` chain, each
  phase rehydrating the prior phase's on-disk sidecars across a process boundary,
  must equal the straight-through blib at `1e-9` (`:26-36,:502-522`).

To keep the version metadata cell byte-stable across the daily version stamp, the
gate pins `OSPREY_VERSION_OVERRIDE = 26.1.1.0` for every child process
(`regression.ps1:133`). The default tolerance is `1e-9` (`:114`), tight enough
that any real reordering of a float reduction or any nondeterministic collection
order surfaces as a red gate.

## Flags and switches

This stage has no dedicated "make it deterministic" flag — determinism is
unconditional. The switches below influence the mechanisms above; none of them
changes the output, only scheduling or which order-sensitive algorithm runs.

| Flag / env var / field | Default | Effect on this stage |
|---|---|---|
| `--threads <count>` | all cores | Inner per-file/per-window/per-fold thread budget. Results are place-by-index (Step 2) and sort-after-collect (Step 3), so thread count never changes the output. |
| `--parallel-files [N]` | absent = Sequential | Outer across-files concurrency (`FileParallelism.cs:35-45`). Each file is scored independently; join stages sort by stable keys. Does not change output. |
| `OSPREY_MAX_PARALLEL_FILES` | unset | Legacy back-compat cap on the outer file count when `--parallel-files` is absent (`FileParallelism.cs:153-165`). Scheduling only. |
| Percolator `Seed` | `42` | Fixed PRNG seed for SVM shuffle + peptide-group subsample (`PercolatorFdr.cs:64,:121`). Not exposed as a CLI flag; the constant matches Rust's `seed=42`. |
| `--shared-peptides {all\|razor\|unique}` | `all` | Selects the shared-peptide reassignment. `all` and `unique` are order-independent; `razor` runs the order-sensitive greedy in Step 9 (see divergence). |
| `--fdr-method {percolator\|simple}` | `percolator` | `percolator` uses the seeded SVM (Steps 4-6); `simple` skips SVM training (no PRNG involved). |
| `OSPREY_VERSION_OVERRIDE` | unset | Pins the `osprey_version` blib metadata cell so the golden compare stays byte-stable; set to `26.1.1.0` by the regression gate (`regression.ps1:133`). |
| `-Tolerance` (regression.ps1) | `1e-9` | The determinism/parity tolerance the oracle enforces across all three modes (`regression.ps1:114`). |

## Divergences from the Rust documentation

- **[STALE-RUST-DOC] Fold assignment is round-robin over sorted peptide groups,
  not a sequence hash** - The Rust doc (§6 "Deterministic Fold Assignment", and
  the `calibration_ml.rs` row of the known-paths table) shows
  `fold = hash(modified_sequence) % n_folds`. The C# code assigns folds by
  round-robin (`i % nFolds`) over the **ordinal-sorted distinct peptide-group
  keys**, with no hashing — and its own doc-comment says this is a direct port of
  Rust `create_stratified_folds_by_peptide`, i.e. the Rust CODE also sorts and
  round-robins. The doc's `hash % n_folds` phrasing is stale relative to both
  implementations. Evidence: `Osprey.FDR/PercolatorFdr.cs:2492-2504`,
  `Osprey.Scoring/CalibrationScorer.cs:402-452`. Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] `TotalOrder` bit-transform replaces `f64::total_cmp`**
  - Rust calls the built-in `total_cmp`. The C# port has no such intrinsic, so it
  reproduces the IEEE-754 total order via a sign-flipped `long` key paired with a
  stable LINQ sort. The header note asserts the arithmetic is unchanged and
  parity is unaffected. Evidence: `Osprey.Core/TotalOrder.cs:55-70`. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] SIMD lane-reduction order differs from Rust's
  scalar left-fold** - The Rust doc treats float determinism as "use `total_cmp`
  and sequential sums." The C# SVM hot loop reduces via `System.Numerics`
  per-lane partial sums instead of a strict left-fold, a deliberate perf choice
  (see 17-vectorization.md). Per-op drift is sub-ULP and stays inside the `1e-9`
  gate at `p=21`, but the divergence pattern is lane-stride dependent (AVX2 vs
  AVX-512 vs NEON). Evidence: `Osprey.ML/LinearSvmClassifier.cs:531-545`.
  Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] The determinism oracle is a PowerShell gate, not
  inline tests** - The Rust doc's "Testing" section names inline tests
  (`test_deduplicate_pairs_deterministic`) and a manual `diff` of two blibs. The
  C# port instead runs a standing `regression.ps1` gate that enforces
  straight-through-vs-golden, resume-vs-straight, and HPC-chain-vs-straight all at
  `1e-9`, covering rehydrate and cross-process determinism the Rust doc's manual
  recipe does not. Evidence: `regression.ps1:14-36,:490-543`. Severity: info.

- **[UNVERIFIED] Razor mode is a single-pass greedy, not the documented iterative
  greedy set cover** - The Rust doc (§9) describes an *iterative greedy set cover*
  that, each round, finds the globally-best group and claims **all** its
  unassigned shared peptides in sorted order, and cites
  `test_shared_peptides_razor_deterministic` (10 repeats, byte-identical). The C#
  `SharedPeptideMode.Razor` instead iterates the shared peptides once, assigning
  each to the currently-best group and mutating the unique-peptide counts as it
  goes; the iteration order comes from `HashSet<string>`/`Dictionary<string,...>`
  enumeration, which under .NET's randomized string hashing is not guaranteed
  stable across processes. This differs algorithmically from the documented Rust
  approach and could be order-sensitive. It is off the bit-identical reference
  path (default `--shared-peptides all`; the `regression.ps1` gate does not
  exercise `razor`), and I could not find a C# repeated-run razor determinism test
  analogous to the Rust one, so I could not confirm whether the C# output is
  actually order-dependent or merely a different-but-stable algorithm. A human
  should add a repeat-run byte-identity test for `--shared-peptides razor` (and
  confirm C#/Rust razor assignments match). Evidence:
  `Osprey.FDR/ProteinFdr.cs:432-467`. Severity: major.

Verified matching (no divergence): total-order float comparison
(`TotalOrder.cs`), place-by-index parallel scoring
(`ScoringPipeline.cs:249-305`), sort-after-dictionary-collection at every
competition/dedup site (`PercolatorFdr.cs:1599-1602`, `ScoringPipeline.cs:579`,
`ProteinFdr.cs:628-636`), fixed-seed `XorShift64`+Fisher-Yates matching Rust
(`LinearSvmClassifier.cs:266,:668`), deterministic peptide-grouped subsample
(`PercolatorFdr.cs:2611-2678`), and canonical entry order preserved across
Parquet round-trip and the `--task` boundary
(`PerFileRescoreTask.cs:1301-1315`), all enforced end-to-end by the `1e-9`
`regression.ps1` oracle.
