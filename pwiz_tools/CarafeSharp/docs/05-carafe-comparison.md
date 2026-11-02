# 05. CarafeSharp compared with Carafe 2.2.0

What to expect when the same library workflow runs through Carafe 2.2.0 (Java and Python) and
through CarafeSharp, stage by stage, and why each difference is there.

**Provenance.** One end-to-end run per tool and dataset, on 2026-09-28 and 29:
- Each fine-tune trained on one run: Stellar HeLa `_21` with `hela-filtered.fasta`, and Astral HeLa
  `_55` with the human FASTA.
- Both arms searched with the same Osprey build: ProteoWizard/pwiz#4708 at a5d15e6a4f, with the
  Thermo reader.
- Carafe is the 2.2.0 build of our fork, maccoss/Carafe (the Carafe app's jar, built 2026-07-23). The
  fork adds the entrapment FASTA builder (`-build_entrapment_fasta`) and, after this build, the
  decoy/entrapment similarity gate; the Noble Lab's Carafe (Noble-Lab/Carafe) has neither.
- Carafe 2.2.0 read mzML (msconvert, vendor peak picking), because it cannot read .raw. CarafeSharp
  (a CUDA build of #4719 at 8a5f701d11, which includes this branch) read the .raw through Osprey.
- Both used the options of `Run-CarafeOspreyWorkflow.ps1` and `Run-CarafeSharpWorkflow.ps1`: NoCut
  digest, shuffle entrapment, `-cor 0.8`, top 20 fragments of at least 1e-4.
- One machine (Intel i9-9900K, NVIDIA GTX 1650), one arm at a time.
- Both predicted RT with AlphaPeptDeep's model, Carafe's and then CarafeSharp's default. CarafeSharp's default is
  now Chronologer (`LibrarySettings.DEFAULT_RT_MODEL`; 07-chronologer.md), which tracks the end of the gradient
  where AlphaPeptDeep's generic model plateaus (#4759). `-rt_model alphapeptdeep` gives the RT model compared
  here, and the parity tests against Carafe (`CarafeParityTest`, `LibraryParityTest`) set it.

---

## Summary

| Stage | Result | Why |
|---|---|---|
| Digests | Differ by design | CarafeSharp applies the decoy/entrapment similarity gate from our Carafe fork's main branch, which the 2.2.0 build predates (the Noble Lab's Carafe has neither the gate nor the entrapment FASTA builder). With the gate off (`-no_similarity_gate`), the FASTAs are byte-identical to 2.2.0's. |
| Initial library | The same library | On the precursors both have, fragment lists are identical and spectra agree to the TSV's printed precision (cosine > 0.99999998). |
| Osprey search of the training run | Nearly the same IDs | Totals within 0.6%; precursor Jaccard 0.977 (Stellar) and 0.896 (Astral). The IDs only one arm has sit near the 1% threshold. |
| Training set | 85.7-85.9% slot agreement | Masking comes from Osprey's evidence rather than Carafe's own peak matching (see 02-masking.md). |
| Fine-tuned library | Two different fine-tunes | Median sampled cosine 0.998-0.999, median RT difference 0.016-0.017 min. CarafeSharp writes about one fewer weak fragment per precursor. |
| Time | CarafeSharp 1.34x (Stellar) and 1.50x (Astral) faster end to end | No msconvert, and no second pass over the spectra. |

## Digests

- **Similarity gate:** CarafeSharp logs `Similarity gate (max fragment overlap 0.40)`. The gate
  redraws the decoy and entrapment of a group whose shuffle overlapped its target too much.
  - Every CarafeSharp target is also a Carafe target.
  - Whole groups are identical for 98.8% (Stellar) and 98.2% (Astral) of training pairs, and for
    95.2% and 93.2% of library quartets.
  - Groups dropped for want of an acceptable shuffle: 26 and 322 training groups, 62 and 762 library
    groups. A colliding shuffle is redrawn rather than dropped, so on Astral CarafeSharp keeps 609
    more quartets than Carafe.
- **The oracle:** CarafeSharp's digester matches the 2.2.0 build byte for byte with the gate off, and
  the fork's main branch (maccoss/Carafe) with it on (`EntrapmentFastaParityTest`).

## Initial libraries

| | Stellar | Astral |
|---|---|---|
| Precursors, Carafe / CarafeSharp | 483,608 / 483,560 | 3,082,915 / 3,082,472 |
| Shared | 480,566 | 3,059,786 |
| Fragment lists identical on shared precursors | 100% | 100% |
| Sampled cosine, minimum | 0.99999999 | 0.99999999 |
| Largest intensity / m/z difference | 5.0e-5 / 6.1e-5 | 5.0e-5 / 6.1e-5 |
| Largest RT difference (iRT) | 0.0056 | 0.0065 |

- **Precursor sets:** they differ only by the redrawn decoys.
- **Values:** Carafe's TSV prints intensities to 4 decimals and RT to 2, so its values carry that
  rounding. The blib stores doubles and floats.
- **Decoy pairs:** CarafeSharp skips DecoyPairs rows whose decoy differs from its target only by I/L,
  where Carafe stops on a primary-key error.
- **Whole pairs (2026-10-01):** with a pairing manifest, CarafeSharp writes a target and its decoy, and
  an entrapment target and its entrapment decoy, only together (`DecoyPairGate`). When one member has
  fewer than `-lf_min_n_frag` fragments, its partner is left out too. Carafe drops each on its own and
  keeps the other unpaired, so a target can face no decoy in target-decoy competition: Osprey counts
  such a target as a winner. It is rare: none in the Stellar runs here, 6 of 61,685 Astral targets
  (7 on a GPU, whose predictions move a precursor across the threshold). The parity tests account for
  the precursors left out and require nothing else to differ.

## Osprey search of the training run

| 1% FDR | Stellar Carafe / CarafeSharp | Astral Carafe / CarafeSharp |
|---|---|---|
| Run precursors | 22,741 / 22,876 | 86,227 / 86,204 |
| Experiment protein groups | 2,908 / 2,942 | 6,467 / 6,505 |
| Shared precursors, Jaccard | 0.977 | 0.896 |
| Shared IDs on the same apex scan | 99.1% | 94.6% |

- **Spectra:** Osprey's spectra cache from the .raw is identical to the one from the
  vendor-centroided mzML apart from its header's source file size and time, so the spectra are the
  same.
- **Why the IDs differ:** the libraries differ by the redrawn decoys and by the TSV rounding. Osprey's
  scoring-model choice is known to move IDs on small input changes.
- **Joining the two arms:** normalize the modification mass first. Carafe's blib writes Cys as
  `C[+57.0215]` and CarafeSharp's as `C[+57.02146372057]`.

## Training sets

Carafe trains from the search's blib plus the mzML, with its own matching and 3-point-smoothed XICs.
CarafeSharp applies Carafe's rules to Osprey's exported evidence.

| On precursors kept by both | Stellar | Astral |
|---|---|---|
| MS2 precursors kept, Carafe / CarafeSharp | 15,441 / 15,259 | 53,889 / 39,807 |
| Per-slot mask agreement | 85.7% | 85.9% |
| Carafe masks, CarafeSharp trains as absent | 26,303 slots | 89,459 slots |
| RT targets bit-identical | 97.2% | 91.2% |

- **The largest systematic difference is peak matching.** Carafe matches within 0.4 Th (Stellar) or
  20 ppm (Astral) and usually masks a peak it finds at the window's edge. Osprey's calibrated window
  is tighter, so those ions are unmatched, and CarafeSharp trains them as absent.
  - **Intended:** CarafeSharp predicts the intensity Osprey will extract, and a peak outside Osprey's
    calibrated window is not an m/z Osprey uses. The wider window mostly adds interference.
  - **Evidence:** masking those ions the Carafe way changed the library but not the IDs
    (02-masking.md).
- **Where the masks disagree most, the tools judged coelution differently.** Carafe correlates each
  ion's smoothed XIC with one best ion, over boundaries it refines itself; in the June Stellar
  fine-tune, AVFDETYPDPVR 2+'s refined peak ends at Carafe's own apex, so it judges only the rising
  edge. CarafeSharp correlates
  Osprey's unsmoothed XICs with the median polish's elution profile over Osprey's peak. The
  intensities both tools train on come from the same apex scan and are identical.
- **Astral kept fewer MS2 spectra** (39,807 against 53,889) because Osprey's unsmoothed correlation
  runs lower on Astral, so more spectra fail the correlation rule.

## Fine-tuned models and libraries

Each arm scores its own held-out set with its own masks, so the metrics show each fine-tune's gain over
its own pretrained baseline. They do not rank the two tools.

| | Stellar Carafe / CarafeSharp | Astral Carafe / CarafeSharp |
|---|---|---|
| MS2 COS, pretrained to fine-tuned | 0.960 to 0.979 / 0.969 to 0.985 | 0.965 to 0.978 / 0.977 to 0.987 |
| RT R², pretrained to fine-tuned | 0.866 to 0.998 / 0.866 to 0.997 | 0.857 to 0.998 / 0.858 to 0.998 |
| Final library, sampled cosine median | 0.9978 | 0.9988 |
| RT difference, median / p95 (min) | 0.017 / 0.058 | 0.016 / 0.065 |
| Mean fragments per precursor, Carafe / CarafeSharp | 18.7 / 17.4 | 14.9 / 13.9 |

- **Fewer fragments:** the fragments only Carafe writes are weak (median relative intensity 0.028 and
  0.013). On Stellar, 81% of them are 2+ ions. They follow from the training difference above and
  are expected: mostly ions for which Osprey found no peak within its tolerance in the training run.
  A null test on 800 Stellar 2+ precursors backs this reading: at a 2+ fragment's m/z, Osprey's
  calibrated window finds a peak at the apex no more often than a few Th away (56% against 60%), and
  a peak that coelutes with the precursor (correlation 0.8 or more with its strongest 1+ ions) only
  slightly more often (9.0% against 6.7%). 1+ fragments stand well clear of their null (93% against
  67%, and 51% against 8%). So what matches at a 2+ fragment of a 2+ precursor on the Stellar is
  mostly interference or noise.
- **NCE of a run in eV (2026-09-30, after these runs):** Carafe trains a run at its file's collision
  energy whatever its unit, so a SCIEX run at 35 eV trains as NCE 35. CarafeSharp calibrates the NCE
  of a run whose energy is in eV on its own spectra (01-model-spec.md). Thermo runs, every run here,
  train at their NCE as Carafe's do.
- **Activation and analyzer (2026-09-30, after these runs):** CarafeSharp's MS2 model also learns
  how a precursor was activated (beam-CID, reCID) and which analyzer read the spectrum out (Orbitrap,
  LIT, ToF), in an acquisition layer of its own (01-model-spec.md); Carafe has neither and trains a
  Stellar run as Eclipse. The layer starts at zero, so the pretrained predictions, and every parity
  number here, are Carafe's; a fine-tune also trains the layer's columns for its runs' activation and
  analyzer, so its models differ from Carafe's by what those columns learned.
- **Nondeterminism:** GPU fine-tuning is nondeterministic in both tools. Two CarafeSharp GPU
  fine-tunes of the same data differ by a median cosine of 0.9997. A CPU fine-tune is
  bit-reproducible.

## Time (minutes, one run at a time on one machine)

| | Stellar Carafe / CarafeSharp | Astral Carafe / CarafeSharp |
|---|---|---|
| msconvert | 2.3 / - | 12.4 / - |
| Initial library | 2.4 / 1.6 | 13.2 / 9.4 |
| Osprey search | 3.9 / 4.7 | 12.2 / 17.5 |
| Fine-tune and final library | 8.6 / 6.5 | 41.3 / 25.8 |
| Total, end to end | 17.3 / 12.9 | 79.5 / 53.1 |

The totals include the digests (under 0.6 min). The Osprey search took 44 s longer on Stellar and
319 s longer on Astral in the CarafeSharp arm, which reads the .raw; reading a .raw is slower than
reading the mzML, and the two arms also searched slightly different libraries. Skipping msconvert more
than repays it.

## The full workflow on three runs

The Stellar CarafeSharp arm's final library, searched over all three Stellar runs from the .raw with the
same Osprey (`Run-CarafeSharpWorkflow.ps1` stage 6), gives 31,158 precursors, 28,422 peptides and 4,285
proteins at 1% experiment-level FDR, with a combined entrapment FDP of 0.62% (FDRBench, paired 0.57%).
Earlier runs of the same workflow gave 31,104 to 31,460 precursors at 0.57-0.66%.
