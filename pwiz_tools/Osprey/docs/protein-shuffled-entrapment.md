# Protein-shuffled entrapment: auditing protein-aware FDR

Entrapment estimates the true false-discovery proportion (FDP) of a search from
known-absent peptides mixed into the library (see
[fractional-entrapment.md](fractional-entrapment.md) for the estimators). This note
covers a blind spot of the standard entrapment design, and a variant of it that
removes the blind spot: the **protein-shuffled entrapment audit**. The idea is
Brendan MacLean's (2026-11-01).

## The blind spot

Any step that uses protein evidence - a peptide is more believable when other
peptides of its protein were detected - can only be audited by entrapment peptides
that are able to receive that evidence.

In the standard design every entrapment peptide sits on its **own** entrapment
protein (an `_p_target` accession). Entrapment proteins are made only of false
peptides, so they rarely collect enough detections to count as present. Protein
evidence therefore reaches the real targets and their decoys, and almost never an
entrapment peptide. If a protein-aware step lets false targets through on
detected proteins, the entrapment set does not see it, because no entrapment peptide
is on a detected protein. The measured FDP stays near nominal while the real FDP
does not.

This is not specific to Osprey. It applies to any protein-aware scorer and any
protein-aware FDR step (a protein prior, a sibling-peptide feature, a stratum of
detected proteins): **an entrapment set can only audit a selection rule if its
peptides are exchangeable with the false targets under that rule.** For a rule
built on protein grouping, they are not.

## The shuffle design

Keep the spectra, the sequences and the target-decoy pairs. Change only which
protein each entrapment peptide belongs to:

- Pool every distinct target and entrapment protein accession.
- Give each entrapment peptide one accession drawn uniformly from that pool. With a
  1:1 entrapment library about half the entrapment now sits on target proteins,
  spread over them in proportion to nothing but chance.
- Targets keep their proteins. Every decoy carries its paired peptide's (possibly
  moved) protein, as the library construction rule already requires.

Search the shuffled library beside the original. Because nothing else differs,
**any change in a result is caused by protein information alone**:

- A step that uses no protein information gives bit-identical q-values on the two
  libraries. That is the negative control.
- A protein-aware step that is calibrated keeps its FDP near nominal on the shuffled
  library: the entrapment peptides on detected proteins get the same boost as the
  false targets there, and the decoys that pair with them get it too.
- A protein-aware step that is not calibrated shows it on the shuffled library: the
  entrapment on detected target proteins is accepted at a rate its decoys do not
  account for.

The shuffled arm measures FDR control under protein evidence. It still uses the
ordinary entrapment estimators, with one caution: inside the detected proteins the
entrapment-to-false-target ratio is not the library's `r` (about half the
entrapment shares those proteins with all of the target peptides that are absent),
so the plain combined estimate is a lower bound there. Splitting entrapment hits by
"on a detected target protein" against "elsewhere" (the `assign.tsv` the builder
writes is the truth for that split) shows where the error comes from.

## Building the shuffled library

`ai/scripts/Osprey/Library/Build-ProteinShuffledEntrapment.py` (pwiz-ai) derives the
shuffled variant of any library in the Carafe entrapment layout:

```
python Build-ProteinShuffledEntrapment.py --library-dir <target+decoy+entrapment dir> --out-dir <dir>
```

Osprey takes proteins from the pairing manifest in `--decoys-in-library` mode, so
only `osprey_library_db_pairing.tsv` is rewritten; the spectral library, its
`.blib` and the peptide FASTA are hard-linked. The default seed (20261101)
reproduces the SEA-AD audit library: 1,391,232 entrapment peptides reassigned over
41,274 accessions, 696,316 of them onto a target protein.

## What it found in Osprey

Measured on one SEA-AD Astral file (A10_014), `target+decoy+entrapment-20260817`,
library decoys, r = 1.0.

- **Pass 1 is the negative control.** First-pass q-values are identical on the two
  libraries: the first pass uses no protein information.
- **Pass 2 (`protein-compact`) was not calibrated.** In the original audit (a
  development build with gradient-boosted first-pass scoring) the entrapment on
  detected target proteins at q 1% rose roughly tenfold on the shuffled library and
  the combined FDP at a nominal 1% more than doubled. The original library hid most
  of it.

### The defect

`protein-compact` re-competes, in pass 2, the precursors of proteins with 2 or more
detected peptides (the stratum; [12-second-pass-fdr.md](12-second-pass-fdr.md)).
Membership is by `base_id`, so a target and its paired decoy always land in the same
half - the stratum is already pair-complete. Two candidate causes were checked
offline on the audit run's own sidecars, re-running the competition with the stored
scores:

- **The stratum counting only target peptides (issue #4581's framing) is not the
  main cause.** Excluding each peptide's own detection from its protein's count
  (leave-one-out, at either `>= 1` or `>= 2` other peptides) barely moved the
  shuffled FDP (1.52% to 1.45-1.50% in the offline re-run). Inside the stratum the
  competition is calibrated: at q 1%, entrapment targets won 260 pairs against 232
  for their paired decoys, and decoy winners were 654 of 65,530 accepted.
- **The cause was the complement.** Precursors outside the stratum kept their
  first-pass q (or, when Stage 6 changed their peak, competed pooled with the
  stratum and kept their first-pass experiment q). The stratum is chosen by target
  evidence, so it takes most of the high-scoring targets and only the base rate of
  the high-scoring decoys. The decoys it leaves behind count against no one: at a 1%
  first-pass q the off-stratum survivors held 1,302 targets against 478 decoy
  winners. The union reported at a nominal 1% held 1,132 decoy winners against 66,832
  targets.

The fix competes the stratum and its complement **separately, each against its own
paired decoys**, at run and at experiment scope. Every decoy then counts in the half
its target is in - the same protein evidence as its target.

### Before and after

Pass 2, one SEA-AD file, before (master) and after (this fix), on the standard and
the protein-shuffled library. "FDP" is the combined entrapment estimate at a reported
q of 1%; "protein-aware" re-weights it by the entrapment hit rate on detected target
proteins, applied to the targets' own split between detected and other proteins
(the better estimate on the shuffled library). "Real at FDP 0.5%" counts real
targets at an entrapment FDP of 0.5%. "E on det." is entrapment at q 1% on a detected
target protein.

| first pass | library | build | at q 1% | entrapment | FDP | protein-aware FDP | real at FDP 0.5% (protein-aware) | E on det. |
|---|---|---|---|---|---|---|---|---|
| SVM (default) | standard | master | 44,228 | 4 | 0.02% | 0.01% | 46,608 | 0 |
| | | fix | 44,357 | 4 | 0.02% | 0.01% | 46,658 | 0 |
| | shuffled | master | 41,784 | 117 | 0.56% | 1.35% | 40,604 (36,180) | 117 |
| | | fix | 41,841 | 120 | 0.57% | 1.38% | 40,604 (36,180) | 120 |
| GBDT, out-of-fold | standard | master | 51,338 | 134 | 0.52% | 0.57% | 50,893 (50,528) | 28 |
| | | fix | 50,073 | 9 | 0.04% | 0.04% | 52,217 | 1 |
| | shuffled | master | 49,755 | 302 | 1.21% | 2.18% | 45,494 (42,385) | 196 |
| | | fix | 48,497 | 192 | 0.79% | 1.92% | 45,976 (41,929) | 192 |

Pass 1, the negative control, is identical on the two libraries: 33,151 at q 1% with
FDP 0.65% (SVM), 39,979 with 0.67% (GBDT).

What this says:

- **The complement's excess is gone.** With GBDT scoring the master build accepted 134
  entrapment precursors on the standard library and 106 off detected proteins on the
  shuffled one; the fix accepts 9 and 0, and gains real targets at a matched FDP
  (50,893 to 52,217). With the default SVM the master build already dropped most of
  the complement (an unchanged off-stratum survivor took run q 1.0), so the fix
  changes little there.
- **On the standard library pass 2 looks almost perfect (FDP 0.02%)**, because every
  entrapment peptide sits on an undetected entrapment protein and lands in the
  complement. That is the blind spot this audit exists for.
- **Inside the stratum some excess remains on the shuffled library** (protein-aware
  1.4% with SVM, 1.9% with GBDT out-of-fold), and the fix does not change it. The
  stratum's own competition counts its decoys correctly (offline: entrapment won 260
  pairs against 232 for their decoys), so this is not the complement defect. Shuffled
  (anagram) entrapment shares its target's fragment masses and is over-identified
  relative to foreign-species entrapment, which accounts for part of it. Under GBDT
  out-of-fold scoring the second pass also rescores training rows with the averaged
  model rather than out of fold, which raises targets and lowers decoys among
  stratum members; that is a separate defect.

## See also

- [fractional-entrapment.md](fractional-entrapment.md) - the entrapment estimators.
- [12-second-pass-fdr.md](12-second-pass-fdr.md) - `protein-compact` and its stratum.
- Issues #4581 (the stratum gate), #4580 (a continuous sibling-evidence feature),
  #4538 (validating `protein-compact`).
