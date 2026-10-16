# 24. The Osprey library contract (C#)

> What a correct Osprey spectral library is, written down once and checked by one piece of code
> (`pwiz_tools/CarafeSharp/CarafeSharp.Contracts/LibraryContract.cs`) that CarafeSharp runs before
> it keeps a library and Osprey runs when it loads one that claims the contract. A library that
> meets it loads in Osprey with no warning, no repair and no refusal. No Rust counterpart.

Osprey tolerates a great deal in the libraries it loads: it strips Carafe's per-peptide
accession suffixes, recognizes decoys by prefix when nothing else marks them, replaces protein
lists from the pairing manifest, pairs decoys by composition when the manifest does not, and
keeps the best of duplicate precursors. Each repair made sense while Carafe was someone else's
tool. With CarafeSharp in this repository the two tools are one system, and a repair in the
reader is a bug report the writer never receives - one such repair hid a defect (a decoy merged
with an identical real target) for two months. So the two tools now agree on what is correct,
in the terms below, and either side fails loudly when a library is not.

---

## Scope

The contract applies to a library that **claims** it: a `.blib` whose `LibMetadata` table
holds the key `library_contract` (value `1`, `LibraryContract.METADATA_KEY`). CarafeSharp writes
that key only in contract mode (`--library_contract`, below), after validating what it is about
to write. Osprey validates a library that carries the key and fails the load on any violation.

A library without the key - a DIA-NN or Spectronaut export, a Carafe (Python) library, an older
CarafeSharp library - loads as it always has, with Osprey's repairs and their warnings. Whether
those repairs stay a silent default for third-party libraries or move behind a logged opt-in is
an open question, not decided here.

---

## The contract

Terms: a **row** is one library precursor, identified by its modified sequence (BiblioSpec
mass notation, `PEPTC[+57.0215]K`) and charge. The **manifest** is the FDRBench pairing manifest
([22](22-training-export.md) names its columns; `PairingManifest` in CarafeSharp.Contracts reads
and writes it): one line per peptide sequence with its kind (`target`, `decoy`, `p_target`,
`p_decoy`), its clean source accessions and its pair index; a pair index groups a target, its
decoy, and, with entrapment, the entrapment target and its decoy.

1. **One row per precursor.** No two rows share a modified sequence and charge.
2. **Clean accessions.** Every accession is a source accession as the FASTA gave it, with at
   most the two markers below. No per-peptide suffix (`_pepNNNNN`), which Carafe stamps to
   keep its own rows apart and which breaks protein parsimony and picked-protein FDR.
3. **Decoys are marked, and only decoys.** Every accession of a decoy row starts with a decoy
   prefix (CarafeSharp's `--decoy_prefix`, `rev_` by default; any of Osprey's `--decoy-prefix`
   list; compared ignoring case); no accession of a target row does. Where the library carries
   a `DecoyPairs` table (CarafeSharp's), its `IsDecoy` agrees. Without a manifest, a row with
   any decoy accession, or one its `DecoyPairs` row marks, is a decoy row, and the rest of its
   accessions must agree.
4. **Entrapment is marked.** Every accession of an entrapment row carries `_p_target`
   (`LibraryContract.ENTRAPMENT_MARKER`, Osprey's `EntrapmentLibraryClassifier` marker), as the
   manifest's `proteins` column spells it; no accession of a real-protein row does.
5. **The manifest is authoritative.** Every row's sequence is in the manifest - a peptide
   clipped of its N-terminal methionine (Carafe's `clip_n_m`) counts as its unclipped manifest
   peptide; the row's kind (target / decoy, real / entrapment, from rules 3 and 4) is the kind
   the manifest gives that sequence; the row's accessions are the manifest's `proteins` for
   that sequence, as a set. A row uniformly of the other kind - every accession a decoy's where
   the manifest says target, which is the merged-decoy defect - disagrees with the manifest; a
   row of the right kind with a stray accession is a marking defect of that accession (rules 3
   and 4).
6. **Pairs are whole.** Every decoy row has the target row of its pair at the same charge, and
   every target row has its decoy row at the same charge; the entrapment partition pairs the
   same way. A clipped row pairs with the clip of its partner, and has nothing to check when
   the partner does not start with methionine. A pair the manifest gives no partner (a manifest
   built with `--no_decoys`) has nothing to check. A library with no manifest cannot be checked
   for this rule and cannot claim the contract.

Rules 1 to 4 are checked without a manifest; 5 and 6 need one. `LibraryContract.Validate` can
also be narrowed to rows (rules 1 to 5, for peptides whose charges are not known yet) or to
pairs (rules 1 and 6, for written precursors whose accessions are not at hand), which is how
CarafeSharp splits the check around prediction.

---

## Violations, by class

The validator returns every violation, grouped by class, each class with its count and the
first few rows as examples (`LibraryContractReport`). First-only reporting hides the class of
defect behind most failures, which is the Skyline experience the design follows.

| Class | Rule | What it names |
|---|---|---|
| `DuplicatePrecursor` | 1 | the modified sequence and charge, and how many rows share them |
| `AccessionSuffix` | 2 | the accession carrying `_pepNNNNN` |
| `DecoyUnmarked` | 3 | a decoy row with an accession lacking the decoy prefix |
| `TargetMarkedDecoy` | 3 | a target row with an accession carrying the decoy prefix |
| `EntrapmentUnmarked` | 4 | an entrapment row with an accession lacking `_p_target` |
| `NotInManifest` | 5 | a sequence the manifest does not list |
| `KindDisagreesWithManifest` | 5 | a row marked one kind whose sequence the manifest lists as another |
| `ProteinsDisagreeWithManifest` | 5 | a row whose accessions are not the manifest's `proteins` |
| `UnpairedDecoy` | 6 | a decoy row whose pair's target is not in the library at its charge |
| `UnpairedTarget` | 6 | a target row whose pair's decoy is not in the library at its charge |

The report's text (`LibraryContractReport.Describe`) says how many classes of defect the
library has, then each class with its count and up to five examples, so one message tells a
reader how many kinds of defect a library has and what each looks like. Class names are the
identifiers above in every language; the examples name the row (sequence, or sequence/charge)
and, for an accession defect, the accession.

---

## Where it runs

**CarafeSharp, contract mode** (`--library_contract`, which requires `--lf_type blib` and
`--pairing_manifest`; `LibraryGenerator`): the generator validates the digested peptides with
their proteins against the manifest (rules 2 to 5) after the models are opened and before any
prediction, and refuses the run with the full report if they violate the contract, so hours of
prediction are not spent on a library Osprey would refuse. After the library is written it
checks rules 1 and 6 over the written precursors (the same list the DecoyPairs plan is made
from, with each precursor's modified sequence) and, on a violation, discards the library - the
writers' temp files go with their disposal, so the output folder holds nothing - rather than
keep one that lies about itself. The `library_contract` key is written with the library's
tables (the shared writer's `LibMetadata`, before any spectrum), so a library that survives both
checks carries it. Outside contract mode CarafeSharp writes what Carafe writes, so other Carafe
users' libraries are unchanged.

The entrapment FASTA builder's default accession format carries the `_pepNNNNN` suffix for
Carafe parity; contract mode needs a FASTA built without it, which the one-command pipeline
(PR 4) sets for itself.

**Osprey, at load** (`LibraryLoader.CheckLibraryContract`): after the blib loader has parsed the
file and before any repair (accession normalizing, dedup, decoy marking, pairing), a library
carrying the key (`BlibMetadata.Read`) is validated with its `DecoyPairs` marking and pairing
(`BlibDecoyPairs.Read`) against the manifest `--decoy-pairing-manifest` names (rules 1 to 6;
without a manifest rules 1 to 4 from the rows and rule 6 from the table's pair ids, each of
which must name one decoy and one target at one charge, which the report says). A violation
fails the load with the report; nothing is repaired or cached. A library without the key takes
the tolerant route. A library cache is written only after the check, and its composition hash
covers the manifest, so a cached load is of a library that passed.

**Osprey, pairing a blib's decoys** (`LibraryLoader.TryFinishSuppliedDecoys`, decision of
2026-10-15): a `.blib` searched with its own decoys (`--decoys-in-library`) must carry
`DecoyPairs`, and Osprey pairs its decoys from the table alone (`BlibDecoyPairs.Pair`: the
table's side is authoritative over the accession prefixes, and each pair gives the decoy its
target's id with the decoy bit). A decoy-bearing blib without the table is refused with the
message to search with generated decoys instead. The pairing manifest and the amino-acid
composition fallback stay for TSV libraries, which have no table to carry their pairing; the
manifest still serves a blib's entrapment pairing and FDRBench output, which the table does not
cover. Osprey's own `--export-library` writes `DecoyPairs` for the decoys the load paired, so an
exported decoy library searches back.

---

## Open

- Third-party libraries: keep the silent repairs, or a logged opt-in that restores them for
  such libraries only. Until decided, the tolerant route is the default for any library
  without the key.
- The Rust Carafe path (Python) gets no contract; CarafeSharp is the producer Osprey builds
  libraries with.
- `DecoyPairMinFraction` stays at 0.80 for the tolerant route; a contract library is whole, so
  the threshold does not apply to it.
