# 08. Protein Parsimony & Picked-Protein FDR (C#)

> Pipeline stage: Stage 5 & 7 (protein FDR). C# port of Rust docs/16-protein-parsimony.md. Corresponds to Rust osprey protein parsimony + two-pass picked-protein FDR.

Protein parsimony resolves the peptide-to-protein mapping from the spectral
library into a minimal set of protein groups that can explain the observed
peptides. In the C# port the **parsimony machinery always runs** (it is not
gated by any flag), because the resulting peptide-to-group mapping feeds
protein-aware compaction, reconciliation consensus selection, and the
picked-protein FDR passes. The `--protein-fdr` flag is a **threshold only**: even
when it is omitted the config falls back to `EffectiveProteinFdr = 0.01`
(`Osprey.Core/OspreyConfig.cs:271`) and the two protein-FDR passes still execute.

The whole stage is a native managed C# port of `osprey-fdr/src/protein.rs`. There
is no external dependency and no Python component. The two public entry points
are `ProteinFdr` (the pure algorithm, `Osprey.FDR/ProteinFdr.cs`) and
`ProteinFdrEngine` (the Tasks-layer orchestration facade,
`Osprey.FDR/ProteinFdrEngine.cs`).

See `07-fdr-control.md` for the peptide/precursor FDR that produces the SVM
discriminant scores and q-values this stage consumes, and
`10-cross-run-reconciliation.md` for how the first-pass protein q-values are used
as a consensus rescue gate.

---

## Data types

`Osprey.FDR/ProteinFdr.cs` defines the value types used throughout:

- `ProteinGroup` (`ProteinFdr.cs:44`): `Id` (uint), `Accessions` (list of
  protein accessions sharing an identical peptide set), `UniquePeptides` and
  `SharedPeptides` (both `HashSet<string>` of modified sequences).
- `ProteinParsimonyResult` (`ProteinFdr.cs:69`): `Groups` +
  `PeptideToGroupMap` (`Dictionary<string, List<uint>>`).
- `PeptideScore` (`ProteinFdr.cs:87`): per-peptide `Score` (best SVM
  discriminant), `IsDecoy`, and `BestQvalue` (best/lowest run-level **peptide**
  q-value across files — the Savitski target-side gate).
- `ProteinFdrResult` (`ProteinFdr.cs:113`): `GroupQvalues` and `GroupScores`
  (target winners only) + `PeptideQvalues`. There is intentionally **no**
  `GroupPep` field — protein-level PEP is not computed (matches Rust).

Protein membership comes from `LibraryEntry.ProteinIds`
(`Osprey.Core/LibraryEntry.cs:54`), a `List<string>` of accessions carried on
each library entry alongside `ModifiedSequence` and the `IsDecoy` flag
(`LibraryEntry.cs:56`).

---

## Step 1 — Bipartite graph (target entries only)

`ProteinFdr.BuildProteinParsimony` (`ProteinFdr.cs:215`) walks the full library
and builds two dictionaries, `peptideToProteins` and `proteinToPeptides`
(`ProteinFdr.cs:220-248`). For each entry it:

- Skips decoys: `if (entry.IsDecoy) continue;` (`ProteinFdr.cs:226`).
- Skips undetected peptides when a detected set is supplied:
  `if (detectedPeptides != null && !detectedPeptides.Contains(entry.ModifiedSequence)) continue;`
  (`ProteinFdr.cs:228`). The detected set is the peptides passing the relevant
  peptide-level FDR gate (see the two passes below).
- Adds an edge for every accession in `entry.ProteinIds`
  (`ProteinFdr.cs:230-247`). A peptide with an **empty** `ProteinIds` list
  contributes no edges and therefore never appears in any group — matching the
  Rust "empty protein_ids excluded" edge case.

## Step 2 — Identical-set merging

Proteins whose detected peptide set is identical are indistinguishable and are
collapsed into a single group. The C# code builds a canonical set key by sorting
each protein's peptides ordinally and joining with `|`, then bucketing
accessions by that key (`peptideSetToAccessions`, `ProteinFdr.cs:250-265`). The
sort is only used to form the key; because equal peptide strings are
byte-identical, tie order cannot change the key.

## Step 3 — Subset elimination

A group whose peptide set is a strict subset of another group's is dropped. The
naive form is O(groups² × peptides); the C# port uses a **rarest-peptide
candidate scan** (issue #4357, `ProteinFdr.cs:288-377`):

1. Groups are sorted by peptide-count descending (`ProteinFdr.cs:288`).
2. A global `peptideGroupCount` is built (`ProteinFdr.cs:314-323`).
3. For each group, the **rarest** peptide (the pivot minimizing the candidate
   list) is chosen (`ProteinFdr.cs:335-345`); any proper superset must contain
   it. Only already-retained groups sharing that pivot are tested with
   `peptideSet.Count < larger.Key.Count && peptideSet.IsSubsetOf(larger.Key)`
   (`ProteinFdr.cs:354`).
4. Non-subset groups are appended to `retained` and indexed incrementally
   (`ProteinFdr.cs:362-376`).

The code comment documents that this drops exactly the same groups in exactly
the same order as the pairwise scan — byte-identical grouping, near-linear time
(Stellar 3-file dropped from 6.3 s to ~0.5 s). `HashSet<string>` is used instead
of `SortedSet` because `IsSubsetOf` is the hot path (`ProteinFdr.cs:275`).

## Step 4 — Group IDs and unique/shared classification

Group IDs are assigned by retained order: `gid = (uint)idx`
(`ProteinFdr.cs:383-385`). The peptide→groups map is populated, then each peptide
is classified (`ProteinFdr.cs:410-423`):

- Count == 1 → added to that group's `UniquePeptides` (proteotypic).
- Count ≥ 2 → added to every group's `SharedPeptides`.

> **Determinism note.** The retained order — and hence `Id` — derives from
> `HashSet`/`Dictionary` iteration order, which is not cross-impl stable. The
> port therefore never relies on `Id` for a cross-impl-visible tiebreak; the
> picked-FDR winner sort uses a sorted-accessions string instead (see Step 3 of
> ComputeProteinFdr). See `16-determinism.md`.

## Step 5 — Shared-peptide mode

Controlled by `--shared-peptides {all|razor|unique}`
(`SharedPeptideMode` enum, `OspreyConfig.cs:444-448`; default `All`,
`OspreyConfig.cs:274`). Handled in the `switch` at `ProteinFdr.cs:426`:

| Mode | C# behavior | Location |
|------|-------------|----------|
| `All` (default) | No reassignment. Shared peptides stay mapped to all their groups and can contribute to each group's best-peptide score. | `ProteinFdr.cs:428-430` |
| `Razor` | Each shared peptide is reassigned to a single group (most unique peptides, tiebreak lowest group ID), moved into that group's `UniquePeptides`, removed from all others, and its `PeptideToGroupMap` entry rewritten to the single winner. | `ProteinFdr.cs:432-467` |
| `Unique` | Shared peptides are removed from every group's `SharedPeptides` and deleted from `PeptideToGroupMap` entirely, so they never contribute to any protein score. | `ProteinFdr.cs:469-486` |

> **Razor caveat.** The C# razor is a *per-shared-peptide* greedy in dictionary
> iteration order, **not** the group-centric iterative set-cover the Rust code
> and doc describe. This is a genuine divergence — see
> [Divergences](#divergences-from-the-rust-documentation). Razor is behind a
> non-default flag and is not exercised by the bit-identical reference gates
> (which run `All` mode).

---

## How parsimony feeds protein FDR — the two passes

When protein FDR runs (always, per above), the parsimony graph is rebuilt once
per pass on the same library and same shared-peptide mode, but the peptide score
pool differs. Both passes share the same picked-protein core,
`ProteinFdr.ComputeProteinFdr` (`ProteinFdr.cs:536`).

### Collecting best-peptide scores

`ProteinFdr.CollectBestPeptideScores` (`ProteinFdr.cs:726`) reduces the
per-file `FdrEntry` pool to one `PeptideScore` per modified sequence, keeping the
**max** SVM `Score` and the **min** `RunPeptideQvalue` across all files
(`ProteinFdr.cs:735-750`). This is per-peptide **best** — never a sum. A
projection-buffer overload exists for the lean `FdrProjection` path
(`ProteinFdr.cs:850`), documented as byte-identical because max/min are
order-independent.

### The picked-protein core (`ComputeProteinFdr`, Savitski 2015)

1. **Per-group max SVM score on each side** (`ProteinFdr.cs:549-590`). Iterating
   `PeptideToGroupMap` (keyed by target modified sequence):
   - **Target side is gated**: a peptide contributes only if
     `!tps.IsDecoy && tps.BestQvalue <= qvalueGate` (`ProteinFdr.cs:556`). The
     gate is the peptide-level q-value, per Savitski's "proteins we would report"
     convention.
   - **Decoy side is not gated**: the decoy peptide is looked up as
     `DECOY_ + peptide` and every decoy contributes its score
     (`ProteinFdr.cs:573-589`). Gating decoys would collapse the null (most
     losing decoys have q = 1.0).
2. **Pairwise picking** (`ProteinFdr.cs:599-626`). Each group yields exactly one
   winner: target if `t >= d` (ties to target), else decoy. A sorted-accessions
   `SortKey` is carried on each winner for a deterministic tiebreak
   (`ProteinFdr.cs:605-607`).
3. **Cumulative FDR** (`ProteinFdr.cs:632-651`). Winners are sorted by score
   descending, tiebreak `SortKey` ascending (cross-impl deterministic, unlike the
   HashMap-derived `GroupId`). Then `q = min(1.0, cumDecoys / cumTargets)` at each
   rank.
4. **Backward monotonicity sweep** (`ProteinFdr.cs:653-670`): lower score →
   non-decreasing q. Only **target** winners are written to `GroupQvalues` /
   `GroupScores`; decoy winners are statistical machinery only.
5. **Peptide propagation** (`ProteinFdr.cs:687-700`): each peptide's q-value is
   the **min** (best) q across the groups it belongs to; peptides whose only
   groups lost the pair stay at 1.0.

Ranking is by raw SVM discriminant (`FdrEntry.Score`), never by q-value or PEP —
the class doc (`ProteinFdr.cs:496-534`) reproduces the Rust rationale that q/PEP
collapse the decoy null.

### First pass — pre-compaction, gating (`run_protein_qvalue`)

`ProteinFdr.RunFirstPassProteinFdr` (`ProteinFdr.cs:804`), wrapped by
`ProteinFdrEngine.RunFirstPass` (`ProteinFdrEngine.cs:61`), is invoked from
`FirstPassFdrTask` **unconditionally** after first-pass peptide FDR and before
compaction (`Osprey.Tasks/FirstPassFdrTask.cs:305-313`; the comment confirms it is
"not gated on --protein-fdr").

- Detected set = targets with `RunPeptideQvalue <= config.RunFdr`
  (`ProteinFdr.cs:816`) — peptide-level, symmetric with Rust
  `pipeline.rs` (`e.run_peptide_qvalue <= config.run_fdr`).
- `CollectBestPeptideScores` runs over the **full pre-compaction**
  `perFileEntries` (targets and decoys, regardless of whether their base_id
  passed precursor FDR), giving a symmetric target+decoy null.
- Picked-protein FDR runs at the **1× Savitski gate** `config.RunFdr`
  (`ProteinFdr.cs:824`).
- Only `RunProteinQvalue` is set (`setRun: true, setExperiment: false`,
  `ProteinFdr.cs:828`) via `PropagateProteinQvalues` (`ProteinFdr.cs:765`).

`RunProteinQvalue` then feeds protein-aware compaction and the reconciliation
consensus rescue gate (`10-cross-run-reconciliation.md`).

### Second pass — post-reconciliation, authoritative (`experiment_protein_qvalue`)

`ProteinFdrEngine.RunSecondPass` (`ProteinFdrEngine.cs:145`) is invoked from
`SecondPassFdrTask` (`Osprey.Tasks/SecondPassFdrTask.cs:255`) after second-pass peptide
FDR, on the compacted + reconciled + rescored pool.

- Detected set = targets with
  `EffectiveExperimentQvalue(peptideGateLevel) <= config.ExperimentFdr`
  (`ProteinFdrEngine.cs:171-183`), where `peptideGateLevel = config.FdrLevel`
  (default `Precursor`). The long comment (`ProteinFdrEngine.cs:155-171`)
  explains this deliberately mirrors Rust's `config.fdr_level` gate, with Rust's
  `Protein → Peptide` remap being a no-op in C# because the C# `FdrLevel` enum
  has no `Protein` variant.
- Parsimony rebuilt (`ProteinFdrEngine.cs:192`), picked-protein FDR again at
  `config.RunFdr` (1×, `ProteinFdrEngine.cs:203`; the comment notes a previous 2×
  gate was a corrected divergence).
- Both `RunProteinQvalue` and `ExperimentProteinQvalue` are propagated
  (`PropagateProteinQvalues(..., true, true)`, `ProteinFdrEngine.cs:227`).

`ExperimentProteinQvalue` is the authoritative protein q-value on `FdrEntry`
(`Osprey.Core/FdrEntry.cs:50`, default 1.0 at `:129`).

### When `--protein-fdr` is not set

Because `EffectiveProteinFdr` always resolves to a value
(`OspreyConfig.cs:271`) and both passes run unconditionally, parsimony and the
protein q-values are always computed. What the flag changes is only the numeric
threshold used when counting/reporting passing groups
(`ProteinFdrEngine.cs:209-217`). Compaction still adds the protein rescue rule
against `RunProteinQvalue <= EffectiveProteinFdr`.

---

## Edge cases (verified against C#)

- **Empty `ProteinIds`**: peptide contributes no graph edges
  (`ProteinFdr.cs:230`), never appears in `PeptideToGroupMap`, cannot pass
  protein filtering.
- **Decoy library entries**: excluded from the target parsimony graph
  (`ProteinFdr.cs:226`); handled by the picked-protein pass via the `DECOY_`
  prefix pairing (`ProteinFdr.cs:203`, `:574`).
- **Peptides not in the detected set**: skipped when a detected set is provided
  (`ProteinFdr.cs:228`).

---

## Diagnostics

There is no persistent production protein report writer in the examined C# code
path (no `*.proteins.csv` / `protein_groups.tsv` emitter). What exists are
env-var-gated cross-impl bisection dumps in
`Osprey/OspreyFileDiagnostics.cs`:

- `WriteStage6ProteinFdrDump` → `cs_stage6_protein_fdr.tsv`
  (`OspreyFileDiagnostics.cs:1918`), gated by `OSPREY_DUMP_PROTEIN_FDR`
  (`:333`); `OSPREY_PROTEIN_FDR_ONLY` exits after the dump (`:336`).
- `WriteStage7ProteinFdrDump` → `cs_stage7_protein_fdr.tsv`
  (`OspreyFileDiagnostics.cs:1983`), gated by `OSPREY_DUMP_STAGE7_PROTEIN_FDR`
  (`:348`); `OSPREY_STAGE7_PROTEIN_FDR_ONLY` exits after it (`:351`).
- `WriteBestPeptideScoresDump` and `WriteStage7WinnersDump` are fired from
  inside `ProteinFdr` when `FdrDiagnostics.DumpBestPeptideScores` /
  `DumpStage7Winners` are set (`ProteinFdr.cs:677-682`, `:756-757`).

The stage-7 dump sorts rows by group q-value then sorted accessions
(`OspreyFileDiagnostics.cs:2020-2026`) for cross-impl comparison.

---

## Flags and switches

| Flag / field | Default | Effect on this stage |
|---|---|---|
| `--protein-fdr <threshold>` | unset → `EffectiveProteinFdr = 0.01` (`OspreyConfig.cs:265,271`) | **Threshold only.** Parsimony + both protein-FDR passes always run regardless (`FirstPassFdrTask.cs:305`, `SecondPassFdrTask.cs:255`). The flag sets the q-value cutoff used when counting passing groups and the additive compaction rescue rule. |
| `--shared-peptides {all\|razor\|unique}` | `all` (`OspreyConfig.cs:274`) | Selects the Step-5 shared-peptide policy (`ProteinFdr.cs:426-486`). `all` = contribute to every group; `razor` = single-group assignment; `unique` = drop shared peptides. Parsed at `OspreyCommandArgs.cs:158-177`. |
| `--fdr-level {precursor\|peptide\|both}` | `precursor` (`OspreyConfig.cs:284`) | Sets `config.FdrLevel`, which is the **second-pass** detected-peptide gate level (`ProteinFdrEngine.cs:171`). The CLI does **not** accept `protein` (`OspreyCommandArgs.cs:138-157`) and the enum has no `Protein` variant (`OspreyConfig.cs:411-416`). |
| `--run-fdr <v>` | 0.01 | Used as the 1× Savitski picked-protein gate in both passes (`ProteinFdr.cs:824`, `ProteinFdrEngine.cs:203`) and as the first-pass detected-peptide gate (`ProteinFdr.cs:816`). |
| `--experiment-fdr <v>` | 0.01 | Second-pass detected-peptide gate cutoff (`ProteinFdrEngine.cs:178`). |
| `OSPREY_DUMP_PROTEIN_FDR` / `_ONLY` | off | Emit `cs_stage6_protein_fdr.tsv` (first pass) and optionally exit (`OspreyFileDiagnostics.cs:333-336`). |
| `OSPREY_DUMP_STAGE7_PROTEIN_FDR` / `_ONLY` | off | Emit `cs_stage7_protein_fdr.tsv` (second pass) and optionally exit (`:348-351`). |

There is no `--decoy` or protein-report flag specific to this stage; decoy
pairing uses the fixed `DECOY_` prefix constant (`ProteinFdr.cs:203`).

---

## Divergences from the Rust documentation

- **[PORT-ERROR] Razor mode is a per-peptide greedy, not the iterative
  group-centric set cover** - Rust doc §"Razor: Iterative Greedy Set Cover"
  (and the Rust code, `crates/osprey-fdr/src/protein.rs:197-266`) sorts shared
  peptides alphabetically, then repeatedly selects the *group* with the most
  unique peptides that still owns any unassigned shared peptide and claims **all**
  of that group's remaining shared peptides in one batch — explicitly
  path-independent. C# instead iterates shared peptides in `Dictionary` order and
  assigns each individually to its current best group, mutating unique counts
  in-place as it goes (`ProteinFdr.cs:432-467`). The C# collection is **not**
  sorted (`ProteinFdr.cs:434-439`). These algorithms diverge on cascading
  topologies: e.g. G0 unique {A,B} shared {X,Y}, G1 unique {C,D,E} shared {X},
  G2 unique {F} shared {Y} — Rust always yields X→G1, Y→G0, but the C# result
  flips to X→G0 when the dictionary happens to process Y before X (Y lifts G0 to
  3 unique, then X ties G0=G1 and the lowest-ID tiebreak picks G0). Rust's
  `test_shared_peptides_razor_cascading_assignment` pins the group-centric
  result. Evidence: `Osprey.FDR/ProteinFdr.cs:432-467`. Severity: minor (razor is
  the non-default `--shared-peptides razor`; the reference bit-identical gates run
  `All` mode, so this path is unverified by the golden tests; `All` and `Unique`
  match Rust exactly).

- **[INTENTIONAL-CSHARP-DESIGN] No `--fdr-level protein` / no protein-level
  output filtering** - Rust doc §"Second Pass" states
  `experiment_protein_qvalue` "feeds `--fdr-level protein` filtering." The C#
  `FdrLevel` enum has only `{Precursor, Peptide, Both}` (`OspreyConfig.cs:411-416`)
  and the CLI parser rejects `protein`, accepting only `precursor|peptide|both`
  (`OspreyCommandArgs.cs:138-157`). `ExperimentProteinQvalue` is still computed
  and propagated (`ProteinFdrEngine.cs:227`), but there is no reachable path to
  filter the output by it. The `RunSecondPass` comment
  (`ProteinFdrEngine.cs:165-170`) documents this as a deliberate no-op remap.
  Evidence: `Osprey.Core/OspreyConfig.cs:411-416`, `Osprey/OspreyCommandArgs.cs:138-157`.
  Severity: minor.

- **[UNVERIFIED] No production protein report** - Rust doc §"Implementation"
  lists `write_protein_report()` emitting `*.proteins.csv` with gene names, PEP,
  and q-values. No equivalent production writer was found in the C# path
  (grep for `proteins.csv`/`protein_groups`/`ProteinReport` returned no
  emitter); only the env-var-gated diagnostic dumps
  `cs_stage6_protein_fdr.tsv` / `cs_stage7_protein_fdr.tsv` exist
  (`Osprey/OspreyFileDiagnostics.cs:1918`, `:1983`). A human should confirm
  whether a protein report writer lives outside the files reviewed here (lab
  memory references a `protein_groups.tsv` / `stats.tsv` feature that this
  checkout's protein-FDR code does not surface). Severity: minor.

- **[STALE-RUST-DOC] Second-pass detected set gates on `config.fdr_level`, not
  "peptide FDR"** - Rust doc §"Second Pass" step 1 says the second parsimony is
  built "from peptides passing second-pass **peptide** FDR." Both the C# code
  (`ProteinFdrEngine.cs:171-183`) and current Rust
  (`pipeline.rs`, referenced in the C# comment) actually gate on
  `EffectiveExperimentQvalue(config.FdrLevel)`, which is **precursor**-level by
  default. C# and Rust agree; the doc's "peptide" wording is stale. Evidence:
  `Osprey.FDR/ProteinFdrEngine.cs:171`. Severity: info.

Verified as matching the Rust documentation and Rust code: parsimony always runs
regardless of `--protein-fdr` (threshold-only, `FirstPassFdrTask.cs:305`,
`OspreyConfig.cs:271`); decoy and empty-protein exclusion from the target graph
(`ProteinFdr.cs:226,230`); identical-set merging and strict-subset elimination
(`ProteinFdr.cs:250-377`, byte-identical drop order); `All` and `Unique`
shared-peptide modes (`ProteinFdr.cs:428-486`); two-pass architecture with
first-pass on the full pre-compaction pool writing `RunProteinQvalue` and
second-pass writing `ExperimentProteinQvalue` (`ProteinFdr.cs:804-835`,
`ProteinFdrEngine.cs:145-230`); per-protein scoring by **single best peptide**
max SVM discriminant, not a sum (`ProteinFdr.cs:549-590`,
`CollectBestPeptideScores` max/min at `:735-750`); gated target side / ungated
decoy side (`ProteinFdr.cs:556` vs `:573`); pairwise picking, cumulative FDR with
sorted-accessions tiebreak, backward monotonicity sweep, and min-across-groups
peptide propagation (`ProteinFdr.cs:599-700`); no protein-level PEP
(`ProteinFdrResult` has no `GroupPep`, `ProteinFdr.cs:113`); and the 1× Savitski
gate at `config.RunFdr` in both passes (`ProteinFdr.cs:824`,
`ProteinFdrEngine.cs:203`).
