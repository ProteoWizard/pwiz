# 05. CarafeSharp compared with Carafe 2.2.0

What to expect when the same library workflow runs through Carafe 2.2.0 (Java and Python) and
through CarafeSharp, stage by stage, and why each difference is there.

**Provenance.** One end-to-end run per tool and dataset:
- Each fine-tune trained on one run: Stellar HeLa `_21` with `hela-filtered.fasta`, and Astral HeLa
  `_55` with the human FASTA.
- Both arms searched with the same Osprey build, the `--training-export` branch at 0a0b744.
- Carafe 2.2.0 read mzML, because it cannot read .raw. CarafeSharp (a CUDA build of 2026-09-26) read
  the .raw through Osprey.
- Both used the options of `Run-CarafeOspreyWorkflow.ps1` and `Run-CarafeSharpWorkflow.ps1`: NoCut
  digest, shuffle entrapment, `-cor 0.8`, top 20 fragments of at least 1e-4.
- These numbers are preliminary. They will be refreshed with the landed Osprey and the final head.

---

## Summary

| Stage | Result | Why |
|---|---|---|
| Digests | Differ by design | CarafeSharp applies Carafe's decoy/entrapment similarity gate, which the 2.2.0 jar predates. With the gate off (`-no_similarity_gate`), the FASTAs are byte-identical to 2.2.0's. |
| Initial library | The same library | On the precursors both have, fragment lists are identical and spectra agree to the TSV's printed precision (cosine > 0.99999998). |
| Osprey search of the training run | Nearly the same IDs | Totals within 0.5%; precursor Jaccard 0.977 (Stellar) and 0.891 (Astral). The IDs only one arm has sit near the 1% threshold. |
| Training set | 85.7-85.8% slot agreement | Masking comes from Osprey's evidence rather than Carafe's own peak matching (see 02-masking.md). |
| Fine-tuned library | Two different fine-tunes | Median sampled cosine 0.997-0.999, median RT difference 0.02 min. CarafeSharp writes about one fewer weak fragment per precursor. |
| Time | CarafeSharp 1.36x (Stellar) and 1.48x (Astral) faster end to end | No msconvert, and no second pass over the spectra. |

## Digests

- **Similarity gate:** CarafeSharp logs `Similarity gate (max fragment overlap 0.40)`. The gate
  redraws the decoy and entrapment of a group whose shuffle overlapped its target too much.
  - Every CarafeSharp target is also a Carafe target.
  - Whole groups are identical for 98.8% (Stellar) and 98.2% (Astral) of training pairs, and for
    95.2% and 93.2% of library quartets.
  - Groups dropped for want of an acceptable shuffle: 26 and 322 training groups, 62 and 762 library
    groups. A colliding shuffle is redrawn rather than dropped, so on Astral CarafeSharp keeps 609
    more quartets than Carafe.
- **The oracle:** CarafeSharp's digester matches Carafe byte for byte with the gate off, and Carafe's
  origin/main with it on (`EntrapmentFastaParityTest`).

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

## Osprey search of the training run

| 1% FDR | Stellar Carafe / CarafeSharp | Astral Carafe / CarafeSharp |
|---|---|---|
| Run precursors | 23,169 / 23,169 | 86,952 / 87,361 |
| Experiment protein groups | 2,991 / 3,001 | 6,543 / 6,568 |
| Shared precursors, Jaccard | 0.977 | 0.891 |
| Shared IDs on the same apex scan | 99.2% | 93.6% |

- **Spectra:** Osprey's spectra cache from the .raw is byte-identical to the one from the
  vendor-centroided mzML, so the spectra are the same.
- **Why the IDs differ:** the libraries differ by the redrawn decoys and by the TSV rounding. Osprey's
  scoring-model choice is known to move IDs on small input changes.
- **Joining the two arms:** normalize the modification mass first. Carafe's blib writes Cys as
  `C[+57.0215]` and CarafeSharp's as `C[+57.02146372057]`.

## Training sets

Carafe trains from the search's blib plus the mzML, with its own matching and 3-point-smoothed XICs.
CarafeSharp applies Carafe's rules to Osprey's exported evidence.

| On precursors kept by both | Stellar | Astral |
|---|---|---|
| MS2 precursors kept, Carafe / CarafeSharp | 15,586 / 15,371 | 53,741 / 39,296 |
| Per-slot mask agreement | 85.7% | 85.8% |
| Carafe masks, CarafeSharp trains as absent | 26,391 slots | 88,989 slots |
| RT targets bit-identical | 97.3% | 90.8% |

- **The largest systematic difference is peak matching.** Carafe matches within 0.4 Th (Stellar) or
  20 ppm (Astral) and usually masks a peak it finds at the window's edge. Osprey's calibrated window
  is tighter, so those ions are unmatched, and CarafeSharp trains them as absent.
  - **Intended:** CarafeSharp predicts the intensity Osprey will extract, and a peak outside Osprey's
    calibrated window is not an m/z Osprey uses. The wider window mostly adds interference.
  - **Evidence:** masking those ions the Carafe way changed the library but not the IDs
    (02-masking.md).
- **Astral kept fewer MS2 spectra** (39,296 against 53,741) because Osprey's unsmoothed correlation
  runs lower on Astral, so more spectra fail the correlation rule.

## Fine-tuned models and libraries

Each arm scores its own held-out set with its own masks, so the metrics show each fine-tune's gain over
its own pretrained baseline. They do not rank the two tools.

| | Stellar Carafe / CarafeSharp | Astral Carafe / CarafeSharp |
|---|---|---|
| MS2 COS, pretrained to fine-tuned | 0.961 to 0.978 / 0.969 to 0.985 | 0.963 to 0.974 / 0.977 to 0.987 |
| RT R², pretrained to fine-tuned | 0.864 to 0.998 / 0.867 to 0.998 | 0.853 to 0.997 / 0.861 to 0.998 |
| Final library, sampled cosine median | 0.9974 | 0.9987 |
| RT difference, median / p95 (min) | 0.018 / 0.061 | 0.019 / 0.081 |
| Mean fragments per precursor, Carafe / CarafeSharp | 18.7 / 17.4 | 14.9 / 13.8 |

- **Fewer fragments:** the fragments only Carafe writes are weak (median relative intensity 0.03 and
  0.013). On Stellar, 81% of them are 2+ ions. They follow from the training difference above and
  are expected: mostly ions for which Osprey found no peak within its tolerance in the training run.
- **Nondeterminism:** GPU fine-tuning is nondeterministic in both tools. Two CarafeSharp GPU
  fine-tunes of the same data differ by a median cosine of 0.9997. A CPU fine-tune is
  bit-reproducible.

## Time (minutes, one run at a time on one machine)

| | Stellar Carafe / CarafeSharp | Astral Carafe / CarafeSharp |
|---|---|---|
| msconvert | 2.4 / - | 12.9 / - |
| Initial library | 2.3 / 1.5 | 13.0 / 9.2 |
| Osprey search | 4.4 / 5.1 | 12.9 / 19.1 |
| Fine-tune and final library | 8.6 / 6.3 | 41.9 / 26.0 |
| Total | 17.7 / 13.0 | 81.0 / 54.8 |

Osprey reads a .raw more slowly than the mzML: 37 s more per Stellar file and 358 s more per Astral
file. Skipping msconvert more than repays it.
