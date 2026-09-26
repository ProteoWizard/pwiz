# 02. Training data and fragment masking

How CarafeSharp turns an Osprey search into MS2 and RT training rows, and how that compares
with Carafe. Code: `CarafeSharp.Training/OspreyMaskingPolicy.cs`, `OspreyTrainingSet.cs`,
`OspreyModificationMapper.cs`, `TrainingExportLocator.cs`, and `CarafeSharp/ModelTrainer.cs`.
Input: Osprey's `<stem>.training.parquet`, written by `osprey --training-export`; its schema is
in `pwiz_tools/Osprey/docs/22-training-export.md`.

Carafe reads the raw data itself. It matches fragments in the apex spectrum, extracts and smooths
XICs, refines the peak boundaries, and decides which ions to trust. CarafeSharp reads no spectra.
Osprey exports its evidence for every ladder ion, and CarafeSharp applies Carafe's rules to that
evidence.

---

## The fragment grid ("slots")

AlphaPeptDeep predicts, and trains on, a grid of `L - 1` cleavage positions by 4 ion types for a
peptide of length `L`. The types are b 1+, b 2+, y 1+ and y 2+. Position `p` holds `b(p+1)` and
`y(L-1-p)`, and a slot is `p * 4 + t`. Carafe's `fragment_intensity_df.tsv` and
`fragment_intensity_valid.tsv` hold one row per position with those four columns. Osprey's
per-ion blobs use the same order.

Each slot of a training spectrum has one of three states:

- **Matched and valid.** It trains on the observed intensity, relative to the top ion.
- **Unmatched and valid.** It trains toward 0; the model learns that the ion is absent.
- **Masked.** It is left out of the loss (valid count > 0).

---

## Carafe's rules

Carafe applies these in `AIGear.get_ms2_matches_diann`, which is its path for `-se Osprey` with
DIA data (maccoss/carafe origin/main 251ad30). The settings shown are those of the Osprey
workflow: `-cor 0.8 -n_ion_min 2 -c_ion_min 2 -lf_frag_n_min 2 -nf 4 -min_n 4 -valid -nm`.

### Per slot

The final value is a sum of increments. A slot is valid only when the sum is 0.

| Rule | Carafe | Slots |
|---|---|---|
| Shared peak | `ion_matrix = count - 1` from `scan2mz2count`, keyed by (apex scan, observed peak m/z). Every b/y match of every PSM whose apex is that scan counts, including two ions of the **same** PSM on one peak. | matched |
| Correlation | Pearson correlation of the 3-point-smoothed XIC with the "best ion" (weighted by apex intensity, skewed XICs excluded), over the refined boundaries. Masked below `-cor` (a value equal to the threshold passes). A PSM with fewer than 4 XICs or 3 scans fails for every ion. | matched |
| Boundary skew | With `M = max apex`, `Lmed = 1.5 * median(x[start])` and `Rmed = 1.5 * median(x[end])`, set `f = 0.10` when the ion's apex is at least `0.5 * M`, else `0.25`. The ion is skewed on a side when it is above both `Lmed` (or `Rmed`) and `f * apex` there. Skew on both sides masks it. | matched |
| m/z range | A theoretical m/z outside the MS2 scan window resets the slot to **valid, 0**; it is not masked. | any |
| Intense low ordinal | b ions up to `-n_ion_min` and y ions up to `-c_ion_min` with intensity >= 0.5 x the top ion are masked unless correlation > 0.9 and skew <= 1. | matched |
| Ordinal floor | Ordinal below `-lf_frag_n_min` (b1 and y1, both charges) is always masked. | any |

Nothing else masks an unmatched slot. On the June Stellar data, the 31% of unmatched slots that
are masked are exactly the b1 and y1 slots, 4 per PSM.

### Per PSM, in order

1. Some ion of ordinal 2 or more is matched, and at least `-nf` ions are matched in total.
2. At least `-min_n` slots have an intensity and are valid.
3. With `-valid`, the top ion is valid. The top ion is the most intense matched ion of ordinal
   2 or more.

Intensities are divided by the top ion's intensity.

### RT rows

Carafe takes every loaded PSM, before the MS2 gates, and collapses them to one row per peptide
form, keeping the one with the minimum q-value. It sets `rt_norm = apex_rt / rt_max`, where
`rt_max` is the last MS2 retention time + 0.1 min.

---

## CarafeSharp on Osprey's evidence

`OspreyMaskingPolicy` applies the same rules in the same order. It reads this evidence:

| Carafe | Osprey evidence |
|---|---|
| Match within 0.4 Da, most accurate peak | `MATCHED_AT_APEX`, the closest peak within Osprey's MS2-calibrated tolerance (0.35 Th on Stellar) |
| Shared peak, other PSMs | `shared_apex_n > 0`: other targets at run q <= 0.01 whose apex is this scan and whose ladder matched this peak |
| Shared peak, same PSM | Two matched slots with the same observed m/z (`ion_mz + apex_mz_error`) |
| Correlation to the best ion | `corr_polish`: Pearson correlation with the median polish's elution profile, over Osprey's final peak |
| Skew inputs | `xic_start`, `xic_end` and `apex_intensity`, with Carafe's formula |
| Scan window | `IN_SCAN_RANGE`, from the run's `.run-info.json` |
| `rt_max`, NCE, isolation range | Footer: `osprey.rt_max` + 0.1, the dominant `osprey.collision_energies`, `osprey.isolation_mz_min/max` |

The training set (`OspreyTrainingSet`) keeps targets at run precursor q <= `-fdr` and leaves out
entrapment peptides. It keeps one spectrum per precursor, from the run where the precursor
scored best, and one RT row per peptide form, the one with the lowest q.

### Known differences

- **Peak matching.** Osprey takes the closest peak within its calibrated tolerance; Carafe takes
  the closest within 0.4 Da. 4% of Carafe's valid matches lie 0.31-0.39 Th off and fall outside
  Osprey's tolerance.
- **XICs.** Osprey's XICs are unsmoothed and closest-peak, over Osprey's own boundaries. Carafe
  smooths with 3 points over boundaries it refines itself. Carafe's smoothing lifts the
  correlation of weak ions, so where Carafe keeps an ion that CarafeSharp masks, the ion is
  usually weak (median 0.09 of the top ion).
- **The correlation reference.** It is the median polish's profile rather than Carafe's weighted
  best ion. On the June data, `corr_polish` agrees better than Osprey's reference XIC
  (`corr_reference`): 85.0% of slots against 83.5%.
- **Claimants.** They are confident targets only (run q <= 0.01). Carafe counts every PSM in the
  blib, which is the same set when Osprey writes only passing targets.

### Agreement with Carafe (June Stellar, file _21)

This compares Osprey re-run on the same search as Carafe's June fine-tune (Carafe's generic
library, `--decoys-in-library`, unit resolution) with Carafe's own `fragment_intensity_valid.tsv`:

| Measure | Value |
|---|---|
| Carafe's 14,806 training spectra present in the export | 14,146 |
| ...on the same apex scan | 97% |
| Slot agreement (valid vs masked) over those spectra | 85.0% |
| Spectra kept: CarafeSharp / Carafe / both | 15,345 / 14,806 / 12,404 |
| Correlation threshold 0.75 / 0.8 / 0.85 | 85.1% / 85.0% / 84.5% |
| Skew rule off | 85.1%, with 17,128 spectra kept |

Carafe's thresholds are therefore the defaults. The opt-in `OspreyMaskingParityTest` checks that
agreement stays at or above 83%, using `CARAFESHARP_OSPREY_TRAINING_EXPORT` and
`CARAFESHARP_CARAFE_FINETUNED`.

---

## Options beyond Carafe

These are `OspreyMaskingSettings` fields. None of them is on the command line yet.

| Setting | Default | Effect |
|---|---|---|
| `OutOfRange` | `train_as_absent` (Carafe) | `masked` leaves ions outside the scan window out of the loss instead of training them as absent |
| `Correlation` | `polish` | `reference` uses Osprey's reference XIC |
| `MaskSharedCoelution` | off | Also masks ions a co-eluting confident precursor could explain (`shared_coelute_n`) |
| `SharedOnlyWhenBetterClaimant` | off | Masks a shared ion only when the other precursor is the better identification |
| `MaxPolishOutlierZ` | off | Masks an apex outlier against the median polish; calibrate it on core ions first (see 22-training-export.md) |
| `IntensitySource` | `apex` (Carafe) | `polish` trains on the polish's row effects |
