# 01. Decoy Generation (C#)

> Pipeline stage: Stage 1 (Library preparation). C# port of Rust docs/01-decoy-generation.md. Corresponds to Rust osprey decoy generation (`osprey-scoring` `DecoyGenerator`, `osprey-core` library-decoy marking/pairing, `osprey-io` pairing manifest).

Osprey builds a target/decoy library for FDR control using the target-decoy
competition approach. Decoys are either **generated** by Osprey from the target
library (enzyme-aware sequence reversal with a cycling fallback) or **supplied**
by the input library (DIA-NN / EncyclopeDIA / Carafe output) and marked + paired
in a post-load step. This stage runs inside `PerFileScoringTask` immediately
after the library is loaded and before any scoring.

> Code references below name a **file and symbol**, never a line number. Line numbers
> in this document were wrong within one PR of being written, and a confidently wrong
> pointer costs more than no pointer at all.

The relevant C# types:

- `Osprey.Scoring/DecoyGenerator.cs` — `DecoyGenerator`, `Enzyme` enum, reversal /
  cycling / fragment recalculation.
- `Osprey.Core/LibraryEntry.cs` — `LibraryEntry`, `DECOY_ID_BIT`,
  `LooksLikeLibraryDecoy`.
- `Osprey.IO/LibraryDecoyMarker.cs` — `ApplyLibraryDecoyMarking` (prefix / column marking).
- `Osprey.Core/LibraryDecoyPairing.cs` — `PairLibraryDecoysByComposition` (composition fallback).
- `Osprey.IO/DecoyPairingManifest.cs` — FDRBench manifest pairing + protein-ID substitution.
- `Osprey.Tasks/PerFileScoringTask.cs` — the dispatch that ties it all together.

## Dispatch: generate vs. mark-and-pair

`PerFileScoringTask.LoadLibraryAndBuildDecoys` (`Osprey.Tasks/PerFileScoringTask.cs`)
chooses one of three paths after `LibraryLoader.Load`:

```
librarySuppliesDecoys = config.DecoysInLibrary || config.DecoyMethod == FromLibrary
```

1. **`--task SecondPassFDR` (`config.ExpectReconciledInput`)** — decoys are not
   rebuilt; the reconciled Parquet already carries both target and decoy FDR rows.
   `decoys = new List<LibraryEntry>()` (`PerFileScoringTask.cs`). Skipping the
   rebuild saves ~45s on Astral 1-file.
2. **Generated decoys (default)** — `!librarySuppliesDecoys`:
   `DecoyGenerator.GenerateAllWithCollisionDetection(...)` is called and the target
   list is replaced with the collision-filtered `validTargets`
   (`PerFileScoringTask.cs`).
3. **Library-supplied decoys** — `MarkSuppliedDecoys` runs first (before the target
   count is taken, `PerFileScoringTask.cs`), then `TryPairSuppliedDecoys`
   (`PerFileScoringTask.cs`). A failure here returns `false` with `ExitCode = 1`.

`DecoyMethod.FromLibrary` is treated as a synonym for `DecoysInLibrary = true`
(`PerFileScoringTask.cs`); the comment there notes it historically fell
through to Reverse generation, which was a bug.

Generated and supplied decoys are concatenated onto the (valid) targets into
`fullLibrary` (`PerFileScoringTask.cs`), then indexed by `Id` into
`libraryById` (`PerFileScoringTask.cs`).

## Generated decoys

### Enzyme-aware sequence reversal

`DecoyGenerator.ReverseSequence` (`Osprey.Scoring/DecoyGenerator.cs`) reverses
a peptide while preserving one terminal residue so tryptic specificity survives:

- **C-terminus-preserving enzymes** (`Enzyme.Trypsin`, `Enzyme.LysC`,
  `Enzyme.Unspecific`; `EnzymeExtensions.PreservesCTerminus`, `DecoyGenerator.cs`):
  positions `0..len-2` are reversed and position `len-1` is held fixed
  (`DecoyGenerator.cs`). `PEPTIDEK` → `EDITPEPK` (K stays at C-terminus).
- **N-terminus-preserving enzymes** (`Enzyme.LysN`, `Enzyme.AspN`): position 0 is
  held fixed and `1..len-1` are reversed (`DecoyGenerator.cs`).
- Sequences of length ≤ 2 are returned unchanged with an identity position mapping
  (`DecoyGenerator.cs`).

`ReverseSequence` returns a `positionMapping` array where
`positionMapping[newPos] = oldPos`, used to remap modifications and fragment masses.

There is a `DecoyGenerator.DetectEnzyme` helper (`DecoyGenerator.cs`) that
infers the enzyme from the C-terminal residue (K/R → Trypsin, else N-terminal
K → LysN, D → AspN, else Unspecific). **Note:** the batch path
`GenerateAllWithCollisionDetection` does **not** call it — each worker constructs
`new DecoyGenerator()` (`DecoyGenerator.cs`), which defaults to `Enzyme.Trypsin`
(C-terminus preserved). So the production decoy path treats every peptide as
C-terminus-preserving regardless of its actual terminal residue. This is fine for
tryptic reference libraries (Stellar/Astral) and is the behavior the cross-impl
byte-parity gate locks in.

### Collision detection and cycling fallback

`DecoyGenerator.GenerateAllWithCollisionDetection`
(`Osprey.Scoring/DecoyGenerator.cs`) mirrors the Rust
`generate_all_with_collision_detection` and implements the pyXcorrDIA multi-strategy
approach:

1. Build a `HashSet<string>` (ordinal) of all non-decoy target **stripped**
   sequences for collision detection (`DecoyGenerator.cs`).
2. Generate decoys in parallel with `Parallel.For` (matches Rust `par_iter`;
   `DecoyGenerator.cs`). Targets that are already decoys or have no fragments
   are skipped (`kind = 0`, `DecoyGenerator.cs`).
3. **Reversal first** (`kind = 1`): accept the reversed sequence if it differs from
   the original **and** is not already a target sequence
   (`DecoyGenerator.cs`).
4. **Cycling fallback** (`kind = 2`): try cycle lengths `1..min(len, 10)` via
   `CycleSequence(seq, cycleLength, ...)`; accept the first cycled sequence that is
   both distinct from the original and not a target (`DecoyGenerator.cs`).
5. **Exclude** (`kind = 3`): if no unique decoy can be produced, drop the target
   from the analysis (`DecoyGenerator.cs`).

Results are collected sequentially to preserve order (`DecoyGenerator.cs`),
so `validTargets` and `decoys` stay index-aligned and deterministic. Counters track
reversed / cycled / excluded / skipped; the summary is logged as
`Generated N decoys from M targets (K excluded due to collisions)`
(`DecoyGenerator.cs`).

`CycleSequence` (`DecoyGenerator.cs`) rotates the internal residues by
`cycleLength % middleLen` while preserving the same terminus as reversal
(`DecoyGenerator.cs`), producing e.g. `PEPTIDEK` → `EPTIDEPK` (shift 1, K
fixed).

The instance method `DecoyGenerator.Generate` (`DecoyGenerator.cs`) is a
simpler single-entry API: it reverses, and if `reversed == original` cycles by 1,
but does **not** check the reversed sequence against the target database. The
production pipeline uses the batch `GenerateAllWithCollisionDetection`, not
`Generate`.

### Fragment m/z recalculation

Because reversal moves residues, fragment m/z values must be recomputed.
`RecalculateFragments` (static wrapper `RecalculateFragmentsStatic`) walks the
target's fragments and:

- **b-ion and y-ion**: ion type and ordinal are carried through unchanged, and only
  the m/z is recomputed for the permuted sequence. A target y7 yields a decoy y7.
- **Any other `IonType`** (A/C/X/Z/Precursor/Internal/Immonium/Unknown from
  `Osprey.Core/FragmentAnnotation.cs`) is copied verbatim with its original m/z and
  annotation. The Rust doc only discusses b/y; the C# code preserves non-b/y
  fragments unchanged.
- Ordinals outside `1..seqLen` are dropped.

The annotation carries over the original **charge** and **neutral loss** (loss code +
custom mass) from the target fragment.

**This replaced a b ↔ y swap** that sent target b_k to decoy y_{n-k} and carried the
copied intensity along with the relabel. Fragment intensity is dominated by ion TYPE,
not by which residues an ion spans - y ions are systematically more intense than b
ions - so the swap inverted the decoy spectrum's intensity structure relative to any
real peptide. The decoys were easy to beat, the target-decoy null was not a null, and
the reported q-values were far too optimistic. Entrapment-measured true FDP at a
claimed 1% q was 10.9% on Stellar and 7.6% on Astral with the swap, against 1.5% and
2.0% without (library-decoy reference: 1.9%). Skyline, OpenSWATH, DIA-NN, EncyclopeDIA
and SpectraST all map the intensity to the same ion; none of them swaps.

### The fragment-overlap gate on candidate sequences

A permutation that merely differs from the target and collides with no other target is
not automatically a usable decoy. If the candidate's theoretical b/y ladder nearly
coincides with the target's, the decoy cannot lose the target/decoy competition on
fragment evidence, so it is not an honest null. `IsCandidateAcceptable` rejects a
candidate whose ladder overlaps its target's by more than `MAX_FRAGMENT_OVERLAP` = 0.4
of the candidate's rungs, within a fixed `LADDER_MATCH_TOLERANCE` = 0.02 Da window;
the cycling fallback then supplies another candidate.

- **0.4 is EncyclopeDIA's threshold** (`PeptideUtils.getSmartDecoy` rejects above it
  and reshuffles), and it is only their number if measured over their statistic, so
  the comparison uses the FULL ladder including the two rungs that are invariant under
  any C-terminus-preserving permutation (y1, and b_{n-1}, whose prefix multiset never
  changes). Those two always match and put a structural `1/(n-1)` floor under the
  ratio, which is why `LibraryValidation.ValidatePeptideLength` enforces a 6-residue
  minimum at load: the worst case is then 1/5 = 0.2 against a 0.4 budget.
- **The window is deliberately NOT the run's fragment tolerance.** The decoy set must
  be a pure function of the library; keying it to the search tolerance would make the
  same library produce different decoys under `unit` vs `hram`, and a fixed window
  lets Rust apply the identical rule without plumbing config into `DecoyGenerator`.
- **Computed from the stripped sequences only** - no modifications, no loaded fragment
  lists. Modifications shift both ladders alike so they cannot change whether the two
  coincide, and excluding them keeps the lean (`OmitFragments`) library path
  bit-identical to a full load.
- **`TheoreticalLadder` uses prefix sums for b and SUFFIX sums for y**, so only ions
  actually spanning an unknown residue are dropped. Deriving y as `total - prefix`
  poisons every y ion the moment any residue is unknown, and a LEADING unknown empties
  the ladder outright - which the caller reads as "accept", silently switching the gate
  off for exactly the peptides that need it. Selenocysteine (U) and the ambiguity codes
  B/Z/X/J/O are absent from `STANDARD_AA_MASSES` and do occur in UniProt-derived
  libraries.

Measured effect at library scale is nil - on the order of 1e-4 of peptides excluded,
entrapment FDP unchanged within noise. It is kept for robustness at SMALL library
scale, where palindromes, low-complexity runs and isobaric I/L permutations are a far
larger fraction.

`CalculateFragmentMz` (`DecoyGenerator.cs`) computes masses from first
principles against the `STANDARD_AA_MASSES` monoisotopic table
(`DecoyGenerator.cs`) with `PROTON_MASS = 1.007276` and `H2O_MASS = 18.010565`
(`DecoyGenerator.cs`):

- b-ion: sum of residues `[0, ordinal)` + proton (`DecoyGenerator.cs`).
- y-ion: sum of residues `[seqLen-ordinal, seqLen)` + H2O + proton
  (`DecoyGenerator.cs`).
- Per-residue modification mass deltas are added by new position via `modMasses`
  (`DecoyGenerator.cs`).
- Neutral loss is subtracted when present (`DecoyGenerator.cs`).
- Final m/z: `(mass + (charge-1)*proton) / charge` (`DecoyGenerator.cs`).

This matches the Rust `calculate_fragment_mz` pseudocode step for step, including
the constants.

### Modification remapping

`RemapModifications` (`DecoyGenerator.cs`, static wrapper
`RemapModificationsStatic`) builds a reverse map `old_pos → new_pos` from
the position mapping and moves each modification to its new position, copying
`Position`, `UnimodId`, `MassDelta`, `Name` (`DecoyGenerator.cs`). Mods whose
original position isn't in the mapping are dropped. Because the amino-acid
composition and the modification set are unchanged (only positions move), the
**precursor m/z is conserved** — the decoy `LibraryEntry` is constructed with the
target's `PrecursorMz` and `Charge` verbatim (`DecoyGenerator.cs`), so decoys
compete fairly with targets at the precursor level.

### Decoy identity stamped on generated decoys

`BuildDecoyFromSequence` (`DecoyGenerator.cs`) constructs each decoy with:

- `Id = target.Id | 0x80000000u` — high bit set (`DecoyGenerator.cs`). The
  constant is `LibraryEntry.DECOY_ID_BIT = 0x80000000u`
  (`Osprey.Core/LibraryEntry.cs`); `base_id = Id & 0x7FFFFFFF` recovers the
  paired target.
- `IsDecoy = true` (`DecoyGenerator.cs`).
- `ModifiedSequence = "DECOY_" + target.ModifiedSequence` (`DecoyGenerator.cs`).
- Each protein accession prefixed `"DECOY_"` (`DecoyGenerator.cs`).
- Gene names, RT, and `RtCalibrated` copied from the target
  (`DecoyGenerator.cs`).

For Osprey-generated decoys the target/decoy pairing is exact by construction (the
decoy inherits the target's `base_id`).

## Library-supplied decoys

When `DecoysInLibrary` is set, Osprey runs three post-load steps: marking, pairing
(manifest + composition), and (with a manifest) protein-ID substitution.

### Step 1: decoy marking

`LibraryDecoyMarker.ApplyLibraryDecoyMarking`
(`Osprey.IO/LibraryDecoyMarker.cs`, called from
`PerFileScoringTask.MarkSuppliedDecoys` at `PerFileScoringTask.cs`) marks
decoys from two OR'd signals:

- **DIA-NN `Decoy` column**: the TSV loader sets `IsDecoy` at load time
  (`Osprey.IO/DiannTsvLoader.cs`). `ParseDecoyFlag`
  (`DiannTsvLoader.cs`) accepts `1`, `true`, `yes`, `y`, `t` case-insensitively
  (ASCII-only lowering to match Rust `to_ascii_lowercase`; `DiannTsvLoader.cs`);
  everything else including `0`/empty/garbage is a target. Entries already flagged
  by the loader just get `DECOY_ID_BIT` canonicalized onto their `Id` and count in
  `MarkingStats.NViaColumn` (`LibraryDecoyMarker.cs`).
- **Protein-accession prefix scan**: `LibraryEntry.LooksLikeLibraryDecoy`
  (`Osprey.Core/LibraryEntry.cs`) returns true if any protein accession starts
  (case-insensitively) with a configured prefix; matches get `IsDecoy = true` +
  `DECOY_ID_BIT` and count in `MarkingStats.NViaPrefix`
  (`LibraryDecoyMarker.cs`).

Marking is idempotent (`LibraryDecoyMarker.cs`). The log breaks the count down:
`Library-decoy mode: N flagged (X via Decoy column, Y via protein-accession prefix)`
(`PerFileScoringTask.cs`).

### Step 2: target-decoy pairing (hybrid manifest + composition fallback)

`TryPairSuppliedDecoys` (`PerFileScoringTask.cs`) pairs each decoy with a target
so their `base_id`s match — required for SVM competition, LDA calibration, and CV
fold grouping (see 07-fdr-control.md, 16-determinism.md).

First, a **hard guard**: if there are no library decoys at all,
`TryPairSuppliedDecoys` errors and exits with code 1 (`PerFileScoringTask.cs`).
This "no decoys" check runs **before** manifest application, matching Rust
v26.6.0 (`bcd7249`); the comment notes a manifest can therefore not rescue a load
where the prefix scan misses every decoy (`PerFileScoringTask.cs`).

- **Stage 2a — manifest-based** (when `--decoy-pairing-manifest` is set):
  `DecoyPairingManifest.FromTsv` (`Osprey.IO/DecoyPairingManifest.cs`) parses an
  FDRBench 5-column TSV (`sequence`, `decoy`, `proteins`, `peptide_type`,
  `peptide_pair_index`); required columns are `sequence`/`peptide_type`/
  `peptide_pair_index` (`proteins` optional; `DecoyPairingManifest.cs`).
  `ApplyToLibrary` (`DecoyPairingManifest.cs`) buckets entries by
  `(pair_index, partition, charge, is_target_side)` where partition 0 =
  target↔decoy and 1 = p_target↔p_decoy (`DecoyPairingManifest.cs`), sorts
  each bucket by `(Sequence, Id)` and zips target/decoy 1:1
  (`DecoyPairingManifest.cs`). The manifest is **authoritative for
  classification**: any entry whose sequence appears as `decoy`/`p_decoy` gets
  `IsDecoy = true` + `DECOY_ID_BIT` even if the prefix scan missed it (the Carafe
  prefix-stripping failure mode; `DecoyPairingManifest.cs`, counted in
  `NNewlyMarkedDecoy`). Paired decoys get `Id = targetId | DECOY_ID_BIT`
  (`DecoyPairingManifest.cs`). Manifest read failure errors out with
  exit code 1 (`PerFileScoringTask.cs`).
- **Stage 2b — composition fallback** (always runs):
  `LibraryDecoyPairing.PairLibraryDecoysByComposition`
  (`Osprey.Core/LibraryDecoyPairing.cs`, called at `PerFileScoringTask.cs`)
  indexes unclaimed targets by `(stripped_accession, charge, sorted_AA_composition)`
  (`LibraryDecoyPairing.cs`), strips a configured decoy prefix from each
  decoy accession (`StripDecoyPrefix`, `LibraryDecoyPairing.cs`), and matches
  the first unclaimed target with the same key. Determinism: buckets and the decoy
  scan order are sorted by `(Sequence, Id)`; a decoy's accessions are sorted
  ordinally and the first unclaimed target match wins
  (`LibraryDecoyPairing.cs`). Paired decoys get
  `Id = targetId | DECOY_ID_BIT` (`LibraryDecoyPairing.cs`). This recovers
  any decoy that preserves AA composition (reversal, Carafe randomized shuffles).

The chained pass shares a `PairingState` (`Osprey.Core/LibraryDecoyPairing.cs`)
of `ClaimedTargets` / `PairedDecoys` so the composition pass never re-claims a
manifest-paired target (`PerFileScoringTask.cs`).

### Pairing gate (min fraction)

The breakdown is logged as
`Library-decoy pairing: paired A/B decoys (P%); manifest=M, composition=C; U unpaired decoys, V unpaired targets`
(`PerFileScoringTask.cs`). If `PairingStats.PairedFraction <
config.DecoyPairMinFraction` (default **0.80**), Osprey logs an error and exits with
code 1 rather than run with broken competition (`PerFileScoringTask.cs`).
`PairedFraction` returns 1.0 when there are no decoys
(`Osprey.Core/LibraryDecoyPairing.cs`).

### Step 3: protein-ID substitution from the manifest

When the manifest's `proteins` column is non-empty and disagrees with the library's
stored `ProteinIds`, `ApplyToLibrary` replaces `entry.ProteinIds` with the
manifest's clean source-protein list (`DecoyPairingManifest.cs`,
applied), counted in `NProteinsReplaced` and logged as
`manifest replaced protein_ids on N library entries`
(`PerFileScoringTask.cs`). This restores correct protein parsimony /
picked-protein FDR (see 08-protein-parsimony.md) for Carafe libraries that stamp a
per-peptide `_pepNNNNN` suffix into `ProteinID`. Empty / `-` proteins column is a
no-op — the library wins (`DecoyPairingManifest.cs`).

## Flags and switches

All flags parsed in `Osprey/OspreyCommandArgs.cs`; defaults in
`Osprey.Core/OspreyConfig.cs`.

| Flag / config | Default | Effect on this stage |
|---|---|---|
| `--decoys-in-library` (`config.DecoysInLibrary`) | off (`false`) | Trust decoys already in the library instead of generating them; runs mark + pair. Sets the flag to true (`OspreyCommandArgs.cs`). Hard error if no decoys are recognized. |
| `config.DecoyMethod` | `Reverse` (`OspreyConfig.cs`) | Enum `{Reverse, Shuffle, FromLibrary}` (`OspreyConfig.cs`). `FromLibrary` is treated as a synonym for `DecoysInLibrary` (`PerFileScoringTask.cs`). **No `--decoy-method` CLI flag exists** — only `Reverse` (default, via generation) and `FromLibrary` (via `--decoys-in-library`) are reachable. `Shuffle` is in the enum but not implemented (see divergences). |
| `config.DecoyPrefixes` | `["DECOY_", "rev_", "decoy_"]` (`OspreyConfig.cs`) | Case-insensitive protein-accession prefixes used by marking and by composition-fallback stripping. No CLI flag; config-file only. |
| `config.DecoyPairMinFraction` | `0.80` (`OspreyConfig.cs`) | Minimum fraction of decoys that must pair with a target in library-decoy mode; below it Osprey errors out. Config-file only. |
| `--decoy-pairing-manifest <manifest.tsv>` (`config.DecoyPairingManifestPath`) | unset (`null`) | FDRBench 5-column manifest for authoritative pairing + classification + protein-ID substitution (`OspreyCommandArgs.cs`). Requires `--decoys-in-library`; setting it without that flag is rejected during validation (`OspreyCommandArgs.cs`). |
| `--write-pin` (`config.WritePin`) | off (`false`) | Writes PIN files for external tools (`OspreyCommandArgs.cs`). Does not change decoy content; downstream of this stage. |

Generation is not otherwise flag-gated: with none of the above set, Osprey
generates reverse decoys with the cycling fallback (the default path). No decoy
env vars are read by this stage. Fragment tolerance / resolution flags
(`--resolution`, `--fragment-tolerance`) affect scoring, not decoy construction.

## Divergences from the Rust documentation

- **[STALE-RUST-DOC] `Shuffle` decoy method is not implemented** — The Rust
  "Configuration" section lists `decoy_method: Shuffle` as an option. The C#
  `DecoyMethod` enum defines `Shuffle` (`Osprey.Core/OspreyConfig.cs`), but
  `GenerateAllWithCollisionDetection` always performs reversal + cycling regardless
  of `config.DecoyMethod` (it only logs the value; `Osprey.Scoring/DecoyGenerator.cs`). No code branch consumes `Shuffle`, and there is no CLI flag to select
  it. Rust almost certainly behaves the same (both treat Shuffle as aspirational);
  the reference datasets use reversal. Severity: minor.

- **[INTENTIONAL-CSHARP-DESIGN] Batch generation always uses Trypsin, not
  per-peptide enzyme detection** — The Rust doc presents a 4-enzyme table
  (Trypsin/Lys-C/Lys-N/Asp-N) and enzyme-aware terminal preservation. The C#
  `DetectEnzyme` helper exists (`DecoyGenerator.cs`) but the production batch
  path constructs `new DecoyGenerator()` per worker, defaulting to `Enzyme.Trypsin`
  / C-terminus preservation, and never calls `DetectEnzyme`
  (`DecoyGenerator.cs`). Every peptide is reversed as if C-terminus-preserving.
  Correct for tryptic reference libraries and locked in by the byte-parity gate;
  the Rust batch generator is expected to do the same. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Non-b/y fragments preserved verbatim on reversal** —
  The Rust doc's ion-swap discussion covers only b↔y. C# `RecalculateFragments`
  copies A/C/X/Z/Precursor/Internal/Immonium/Unknown fragments through with their
  original m/z rather than recomputing (`DecoyGenerator.cs`). For standard
  b/y DIA-NN libraries this is a no-op. Severity: info.

- **[INTENTIONAL-CSHARP-DESIGN] Native managed pairing infrastructure** — The
  library-decoy marking (`LibraryDecoyMarker`), composition pairing
  (`LibraryDecoyPairing`), and FDRBench manifest reader (`DecoyPairingManifest`) are
  pure managed C# with deterministic `(Sequence, Id)` sorting, replacing the Rust
  crate equivalents (`osprey_core::types`, `osprey_io::pairing`). Behavior and the
  logged breakdown match the Rust doc's "Library-supplied decoys" section (marking,
  hybrid pairing, protein substitution, the 80% min-fraction gate). Severity: info.

Everything else in the Rust doc — enzyme-aware reversal preserving the cleavage
terminus, the position mapping, first-principles b/y mass recalculation with
`PROTON = 1.007276` / `H2O = 18.010565`, modification remapping, precursor-mass
conservation, `is_decoy` + `DECOY_ID_BIT (0x80000000)` + `DECOY_` prefixes on
protein accessions and modified sequence, collision detection via reversal →
cycling (lengths 1..10) → exclusion, the DIA-NN `Decoy`-column parsing rules, and
the manifest/composition hybrid pairing with the `decoy_pair_min_fraction` hard
gate — is implemented in the C# code as described and verified at the cited
`file:line` locations. See 07-fdr-control.md for how paired `base_id`s feed
target-decoy competition, 08-protein-parsimony.md for the protein-ID substitution
payoff, and 16-determinism.md for the deterministic sort guarantees.
