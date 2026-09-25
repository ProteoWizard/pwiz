# 22. Training Export (C#)

> Pipeline stage: an OPTIONAL fifth task, after Stage 7. No Rust counterpart: the task, its
> file and its schema are C#-only. This document is the contract a consumer reads - every
> column, blob, flag bit and footer key of `<stem>.training.parquet` is defined here, and
> `TrainingExportParquetTest` fails when the writer emits a column this document does not name.

The training export writes, for every run, the evidence a fragment-intensity or
retention-time model needs to train on Osprey's identifications: each confidently identified
target precursor, the observed intensities of its FULL b/y ladder (not just the library's
fragments), and per-ion interference evidence taken from Osprey's own final peak boundaries,
its own median-polish fit and its own view of which other identified precursors claim the same
peaks. Its first consumer is CarafeSharp, which then never has to read raw data: only Osprey
does.

Osprey decides nothing from this evidence. The consumer applies its own masking thresholds
(Carafe's correlation, skew and shared-peak rules, or new ones) to the columns below.

---

## Where it sits

| | |
|---|---|
| Task | `TrainingExport` (`Osprey.Tasks/TrainingExportTask.cs`), the fifth entry of `OspreyTasks.Pipeline`, after `SecondPassFDR` |
| Shape | fan-out over runs (`IsPerFileWorker`), one run's artifacts at a time, and the spectra of the isolation windows in flight - one per worker thread, so up to `--threads` windows |
| Enabled | only with `--training-export` (or `--task TrainingExport`, which implies it); never under `--task ModelDiagnostics`. Accepted but inert under any other `--task`, which logs where the export runs instead |
| Reads | the run's `.scores-reconciled.parquet`, `.2nd-pass.fdr_scores.bin`, `.calibration.json`, `.spectra.bin`, `.run-info.json` (optional); the analysis-wide `<blib-stem>.2nd-pass.fdr_experiment.bin` and `<blib-stem>.1st-pass.retained_base_ids.bin`; the library |
| Writes | `<stem>.training.parquet` per run, and its `.TrainingExport.osprey.task` stamp. Nothing else |

**Off means absent.** With the option off, `OspreyConfig.Includes` excludes the task under
every selection (`ISelectableTask.IsEnabled`), so it is not run, not stamped and not logged, and
every other artifact of the run is byte-identical to a run without the feature.

**Why after Stage 7 and not inside it.** The export needs the FINAL answer - the Stage 6
reconciled boundaries and the Stage 7 q-values and PEP - so it cannot run earlier. It is not
part of `SecondPassFDR` because that task is a join that holds no spectra; making the export one
of its outputs would re-run the whole join every time an export setting changed, and would put
per-run spectral work on the one node that cannot scale out (P3).

**Why it reads artifacts only.** The straight-through run, a pay-later run and an HPC node all
take the same path (P10). The one in-process shortcut is the library: when an earlier stage of
the same process still holds it, the task reuses it rather than loading the `.libcache` a
second time. A load retains fragments only for the base_ids in the retained summary, as
`--task SecondPassFDR` loads it: every row the export reads is a reconciled survivor, so none
loses its spectrum, and the rest of the library's peaks are never allocated.

**What it checks before reading a run.** The reconciled parquet's footer, as every other
post-Stage-4 consumer checks it (`ParquetScoreCache.ValidateScoresParquetGroup`): this build's
version, this search and library, and `osprey.reconciled`. A mismatch fails the run with the
file named.

**Pay later.** The four earlier tasks' keys do not change with the option, so adding
`--training-export` to a finished run's command line reports each of them
`skipping (outputs valid)` and runs only this task. Measured on one Stellar run (the smoke run
in the development TODO): 24.6 s for the task, 18.7 s of it the export itself and the rest the
library load, against 17.5 minutes for the analysis.

---

## Command line

| Flag | Default | Meaning |
|---|---|---|
| `--training-export` | off | Write `<stem>.training.parquet` for every run |
| `--training-export-max-q <q>` | `--run-fdr` | Export targets whose second-pass run precursor q-value is at most `q` |
| `--training-export-claimant-q <q>` | 0.01 | The run q-value at or below which another target counts as a CLAIMANT of shared peaks |
| `--training-export-xics` | off | Also write each precursor's full per-ion XIC matrix over its final peak |
| `--task TrainingExport` | | Run only this task (an HPC node, or a pay-later run that should not even check the others) |

The three settings are refused without the export (they would be silently inert), and a q
outside (0, 1] is refused.

---

## Which rows

One row per exported precursor per run, in ascending `entry_id` order:

- **Targets only.** Decoys are never exported.
- **Run q.** The precursor's second-pass run precursor q-value (`.2nd-pass.fdr_scores.bin`) is at
  most `--training-export-max-q`. The experiment q-values and PEP are exported as columns; filter
  on them in the consumer if wanted.
- **Entrapment is exported and marked.** `is_entrapment` / `peptide_kind` come from the
  library's protein accessions (`EntrapmentLibraryClassifier`: a `_p_target` accession is an
  entrapment peptide), so a consumer can exclude entrapment peptides from training while still
  using them to measure false discovery.
- **The final peak.** Apex scan, boundaries and features are the reconciled parquet's row - the
  Stage 6 answer - and the q-values are Stage 7's.
- **Paired exactly, or refused.** A row joins its library entry by `entry_id`, and a row whose
  modified sequence or charge is not that entry's fails the run (the parquet was scored against
  another library). It joins its second-pass record by (`entry_id`, apex RT), not `entry_id`
  alone: Stage 6 gap-fill can leave two rows of one `entry_id` in a run (one target scored in
  two overlapping windows, so two scans), and each takes its own peak's record. Every writer of
  `.2nd-pass.fdr_scores.bin` records the row's own apex RT; position is exact only for the
  per-file worker's sidecar, not for the Stage 7 writers. Two records, or two rows, the key cannot
  separate fail the run rather than one silently replacing the other.

A run with nothing to export still gets a valid zero-row file with its footer (P13).

---

## File format

Apache Parquet, ZSTD, written through `FileSaver` (`Osprey.IO/TrainingExportParquet.cs`), in row
groups of 20,000 rows. Scalar columns are plain parquet columns. Every per-ion array is a
`byte[]` column holding a **little-endian typed blob with no length prefix**, exactly the scores
parquet's convention (`ParquetBlobCodec`): the element count is `bytes / sizeof(element)`, and
an empty array is a NULL cell, never a zero-length blob.

### Slot order

Every per-ion blob has `n_slots = 4 * (L - 1)` elements for a peptide of `L` residues, in
AlphaPeptDeep's order (`Osprey.Core/FragmentLadder.cs`):

```
slot = p * 4 + t      p = 0 .. L-2  (cleavage after residue p, 0-based)
t = 0  b ion, charge 1      position p holds b(p+1)
t = 1  b ion, charge 2
t = 2  y ion, charge 1      position p holds y(L-1-p)
t = 3  y ion, charge 2
```

So slot 0 is b1+, slot 2 is y(L-1)+, and the last slot is y1++. A slot is **applicable** when
its fragment charge is at most `min(precursor charge, 2)` and its m/z is defined (every residue
has a standard mass). A non-applicable slot is NaN in every float blob, 0 in every integer
blob, and has no flag bits.

m/z is `PeptideFragmentMass.CalculateFragmentMz` with the precursor's modifications - the same
residue masses and order of additions decoy generation and the blib annotation check use.
Modifications at one position add: an N-terminal modification and one of the first residue both
sit at position 0 (`(UniMod:1)M(UniMod:35)`), and every b ion carries both. `mod_positions` /
`mod_masses` list them separately. No neutral losses and no fragment charge above 2 are on the
ladder.

### Per-precursor columns

| Column | Type | Meaning |
|---|---|---|
| `entry_id` | uint32 | Osprey entry id (library precursor id; the high bit would mark a decoy) |
| `base_id` | uint32 | `entry_id & 0x7FFFFFFF` - shared by a target and its paired decoy |
| `is_decoy` | bool | Always false (decoys are not exported); kept so a consumer need not assume it |
| `is_entrapment` | bool | The library marks this peptide as entrapment (`_p_target` accession) |
| `peptide_kind` | string | `target` or `p_target` (FDRBench manifest spelling) |
| `sequence` | string | Stripped sequence |
| `modified_sequence` | string | The library's modified sequence |
| `mod_positions` | blob i32 | 0-based residue position of each modification |
| `mod_masses` | blob f64 | Mass delta of each modification, Da |
| `mod_unimod_ids` | blob i32 | UniMod id of each modification, -1 when the library did not give one |
| `charge` | uint8 | Precursor charge |
| `precursor_mz` | double | Library precursor m/z |
| `library_rt` | double | Library retention time (the library's own scale) |
| `protein_ids` | string, nullable | `;`-joined protein accessions as Osprey searched them - empty when the library lists none, NULL only when it has no list, as the scores parquet writes it |
| `file_name` | string | Run stem |
| `scan_number` | uint32 | 0-based spectrum index of the apex scan in the source file (not a vendor scan number) |
| `apex_rt` | double | Final apex retention time, minutes |
| `start_rt`, `end_rt` | double | Final peak boundaries, minutes (exact MS2 retention times of the first and last peak scan) |
| `n_peak_scans` | int32 | MS2 scans of this isolation window from `start_rt` to `end_rt` inclusive |
| `isolation_lower`, `isolation_upper` | double | Isolation window of the apex spectrum, m/z |
| `bounds_area` | double | Osprey's integrated area of the reference XIC over the peak |
| `coelution_sum` | double | Feature `fragment_coelution_sum` |
| `score` | double | Second-pass SVM discriminant |
| `run_precursor_q`, `run_peptide_q` | double | Second-pass run q-values |
| `experiment_precursor_q`, `experiment_peptide_q` | double | Second-pass experiment q-values (NaN when absent) |
| `experiment_protein_q` | double | Second-pass picked-protein q-value of the peptide |
| `pep` | double | Posterior error probability (one value per precursor, from the experiment sidecar) |
| `apex_tic` | double | Sum of every peak intensity in the apex spectrum |
| `explained_intensity` | double | Feature `explained_intensity` |
| `n_slots` | int32 | `4 * (L - 1)` |
| `n_ions_applicable` | int32 | Applicable slots |
| `n_ions_observed` | int32 | Applicable slots with a peak at the apex (`MATCHED_AT_APEX`) |
| `mp_fitted` | bool | Osprey's median polish produced a fit (false under 3 peak scans, as in the scorer) |
| `mp_converged`, `mp_iterations` | bool, int32 | The fit's convergence and iteration count |
| `mp_overall` | double | The fit's overall effect, ln intensity (NaN without a fit) |
| `mp_cosine` | double | Library cosine of the fit - Osprey's feature `median_polish_cosine`, recomputed |
| `mp_cosine_parity` | bool | `mp_cosine` equals the reconciled parquet's `median_polish_cosine` bit for bit |
| `mp_residual_mad` | double | Median absolute residual over the fit's finite cells, ln units (NaN without a fit) |
| `mp_n_core` | int32 | Fragments in the fit (the library's top 6 by relative intensity) |
| `mp_n_fragments_used` | int32 | Core fragments with at least one non-zero cell |
| `boundary_start_ratio_median` | double | Median over core ions of XIC(start scan) / XIC max |
| `boundary_end_ratio_median` | double | Median over core ions of XIC(end scan) / XIC max |
| `n_coeluting_claimants` | int32 | Claimants in this isolation window whose final `[start_rt, end_rt]` contains this apex |
| `n_same_apex_claimants` | int32 | Claimants whose apex is this same scan |
| `ddc_neighbor_n` | int32 | Targets the double-counting dedup would call colliding with this one (see below) |

### Per-ion blobs (`n_slots` elements each)

| Column | Element | Meaning |
|---|---|---|
| `ion_mz` | f64 | Theoretical m/z of the slot (NaN when not applicable) |
| `ion_flags` | u8 | Bit set, below |
| `apex_intensity` | f32 | Intensity of the closest peak within the tolerance in the apex spectrum; 0 when none |
| `apex_mz_error` | f32 | Observed minus theoretical m/z of that peak, Th, after MS2 calibration; NaN when none |
| `library_rel_intensity` | f32 | The library's relative intensity for this ion; NaN when the library does not hold it |
| `n_finite_scans` | u16 | Peak scans with a non-zero intensity for this ion |
| `xic_start` | f32 | Intensity at the first peak scan (`start_rt`) - Carafe's skew rule input |
| `xic_end` | f32 | Intensity at the last peak scan (`end_rt`) |
| `xic_max` | f32 | Highest intensity over the peak scans |
| `corr_polish` | f32 | Pearson correlation of this ion's XIC with the fit's elution profile `exp(overall + col)` |
| `corr_reference` | f32 | Pearson correlation with the reconciled row's reference XIC (Osprey's most intense top-6 fragment), aligned by retention time |
| `polish_row_effect` | f32 | The ion's row effect projected on the fit, ln units |
| `polish_r2` | f32 | R^2 of the projected ion against the fit (sqrt-preprocessed, as `median_polish_min_fragment_r2`) |
| `polish_pos_resid_max` | f32 | Largest positive residual over the peak, ln units, floored at 0 |
| `polish_apex_residual` | f32 | Residual at the apex scan, ln units (NaN when the ion is 0 there) |
| `polish_outlier_z` | f32 | `polish_apex_residual / (1.4826 * mp_residual_mad)` - a robust z of the apex residual against the core's residual noise |
| `polish_apex_ratio` | f32 | Observed / fitted intensity at the apex (0 when unobserved there) |
| `polish_rel_intensity` | f32 | `exp(row_effect - max row_effect)` over the ladder - the fit's interference-resistant intensity, strongest ion = 1 |
| `shared_apex_n` | u8 | Claimants sharing this ion's apex peak (APEX scope), saturating at 255 |
| `shared_coelute_n` | u8 | Co-eluting claimants with an ion at this m/z (CO-ELUTION scope), saturating at 255 |
| `min_claimant_q` | f32 | Lowest run q among the claimants sharing this ion in either scope; NaN when none |

Every projection field is NaN without a fit, and NaN (with `polish_apex_ratio` 0) for an ion
never observed in the peak. `corr_polish` and `corr_reference` are NaN under three peak scans
and 0 when either series is constant.

### `ion_flags` bits

| Bit | Value | Name | Set when |
|---|---|---|---|
| 0 | 1 | `APPLICABLE` | The slot's charge is at most `min(precursor charge, 2)` and its m/z is defined |
| 1 | 2 | `IN_SCAN_RANGE` | `ion_mz` lies inside the MS2 scan window of this precursor's isolation window (the run info's `ms2_scan_windows`; the run-wide window for a run info that predates it; set for every applicable ion when neither is known) |
| 2 | 4 | `MATCHED_AT_APEX` | A peak was found within the tolerance in the apex spectrum |
| 3 | 8 | `CORE` | One of the library fragments Osprey's median polish was fit to |
| 4 | 16 | `LIBRARY_ANNOTATED` | The library holds this ion by annotation (b/y, ordinal, charge, no loss) |
| 5 | 32 | `LIBRARY_MZ_MATCHED` | The library holds an UNannotated fragment matched to this ion by m/z |
| 6 | 64 | `BETTER_CLAIMANT_APEX` | An APEX-scope sharer has a lower run q (or an equal q and a higher score) |
| 7 | 128 | `BETTER_CLAIMANT_COELUTE` | A CO-ELUTION-scope sharer has a lower run q (or an equal q and a higher score) |

### Optional XIC matrix (`--training-export-xics`)

| Column | Element | Meaning |
|---|---|---|
| `xic_rts` | f64 | Retention time of each peak scan (`n_peak_scans`) |
| `xic_intensities` | f32 | Intensity of every slot at every peak scan, slot-major: `[slot * n_peak_scans + scan]`; NaN rows for non-applicable slots |

These let a consumer recompute any per-ion statistic with its own rule (Carafe smooths XICs
with a Savitzky-Golay filter; Osprey's XICs are unsmoothed - see Risks).

### Footer keys

| Key | Value |
|---|---|
| `osprey.training_export.format_version` | `1` |
| `osprey.version`, `osprey.search_hash`, `osprey.library_hash` | As in the scores parquet |
| `osprey.file_name` | Run stem |
| `osprey.training_export.rows` | Rows written |
| `osprey.training_export.max_q`, `osprey.training_export.claimant_q` | The thresholds applied |
| `osprey.training_export.xics` | `true` / `false` |
| `osprey.training_export.slot_order` | The slot layout, in words |
| `osprey.training_export.mp_cosine_parity` | `N/M`: rows whose `mp_cosine_parity` is true, of all rows |
| `osprey.rt_min`, `osprey.rt_max` | First and last MS2 retention time of the run, minutes |
| `osprey.isolation_mz_min`, `osprey.isolation_mz_max` | Range of the run's isolation windows |
| `osprey.fragment_tolerance`, `osprey.fragment_tolerance_unit` | The MS2-calibrated tolerance every match above used (`ppm` or `Th`) |
| `osprey.ms2_calibration.calibrated`, `.mean`, `.sd`, `.unit` | The run's MS2 mass calibration |
| `osprey.ddc.tolerance`, `osprey.ddc.tolerance_unit`, `osprey.ddc.rt_neighborhood` | The double-counting dedup's tolerance and RT neighborhood for this run |
| `osprey.run_info` | The run's `.run-info.json` as one line of JSON, or empty when it is absent |
| `osprey.instrument_vendor`, `osprey.instrument_model` | From the run info |
| `osprey.ms2_scan_window` | `lower,upper` run-wide MS2 scan window, or empty when unknown (each isolation window's own is in `osprey.run_info`) |
| `osprey.dissociation_methods`, `osprey.collision_energies` | JSON histograms (method or energy -> MS2 spectrum count) |

Collision energy is exported as the file reports it, which differs by vendor (normalized for
Thermo, eV for Sciex, stepped HCD as several values), hence a histogram rather than a number.

---

## The evidence, and how each part is computed

All of it is `Osprey.Scoring/TrainingEvidence.cs`, per precursor, over one isolation window's
spectra as the scorer saw them: MS2-calibrated (`StreamingWindowSpectraProvider`) and sorted by
(retention time, scan). A precursor is placed in the window whose spectra hold its apex scan -
the window it was scored in.

**Tolerance.** One tolerance for every match: the MS2-calibrated fragment tolerance
(`MzCalibration.CalibratedTolerance`, 3 SD with a 0.05 Th / 1 ppm floor), the value the scorer
searched with. Peaks are matched to the CLOSEST m/z within it
(`TopFragmentExtractor.FindClosestPeakInWindow`), not the most intense.

**The peak** is every scan of the window from `start_rt` to `end_rt`; both are exact MS2
retention times, so the range is exact.

**Osprey's median polish, recomputed.** The core is the library's top six fragments by relative
intensity (`TopFragmentExtractor.ExtractFragmentXics` over the peak scans) and the fit is
`TukeyMedianPolish.Compute` with `TukeyMedianPolish.SCORING_MAX_ITERATIONS` and
`SCORING_TOLERANCE` (10, 0.01) - the inputs and the one definition of the arguments
`CoelutionScorer` uses. Its library cosine therefore reproduces the scored feature
`median_polish_cosine` bit for bit. That is the export's built-in consistency check: the task
logs `[TRAIN-EXPORT] mp_cosine parity: N/M` per run and warns on any mismatch, and a mismatch
means the export is not looking at the peak Osprey scored. `TrainingEvidenceTest` checks the
parity against `CoelutionScorer` itself, scoring a synthetic window at fixed boundaries.

**Projection of every ladder ion.** For an ion with XIC `x[s]` over the peak and the fit's
`overall` and column effects `col[s]`:

```
row effect   r    = median over scans with x > 0 of  ln x[s] - overall - col[s]
residual     e[s] = ln x[s] - (overall + r + col[s])        (NaN where x = 0)
R^2          as TukeyMedianPolish.FragmentR2(overall, r, col, e)
pos max      max(0, max e)
apex         e[apex];  z = e[apex] / (1.4826 * mp_residual_mad);  ratio = x[apex] / exp(overall + r + col[apex])
relative     exp(r - max over the ladder of r)
```

For a core ion this reproduces its own row of the fit to within the fit's convergence
tolerance; for any other ion it asks how well the ion follows the elution profile the core
defined. The fit is robust (medians), so one interfered cell barely moves `r`, and that cell
shows as a large `e`, `z` and ratio.

**Correlations** are Pearson over the peak scans, of the raw linear intensities: against the fit's
profile (`corr_polish`) and against the reconciled row's reference XIC (`corr_reference`), the
latter aligned by exact retention time.

**Library mapping.** A library fragment annotated as b or y at charge 1 or 2 without a loss maps
to its slot directly (`FragmentLadder.SlotOf`); an unannotated one maps to the nearest applicable
slot within the tolerance. The six core fragments carry `CORE` on their slots.

### Shared-peak evidence: two scopes

A **claimant** is another TARGET in the same run and isolation window with a second-pass run
q at most `--training-export-claimant-q`. The precursor itself, and other charge states of the
same modified sequence (which share every fragment by construction and do not compete), are
not claimants.

- **APEX scope - Carafe's semantics.** A claimant whose apex is this same scan and whose own
  ladder matched the very same peak of the apex spectrum this ion matched. Counted in
  `shared_apex_n`; `n_same_apex_claimants` counts such claimants per precursor.
- **CO-ELUTION scope - Osprey's semantics.** A claimant whose final `[start_rt, end_rt]`
  contains this precursor's apex and whose ladder has an applicable ion within the tolerance of
  this ion's m/z, whether or not a peak is there. Counted in `shared_coelute_n`;
  `n_coeluting_claimants` counts such claimants.

The `BETTER_CLAIMANT_*` bits say whether the ion's intensity is more likely the other
precursor's: some sharer in that scope has a lower run q (ties broken by the higher score).

**`ddc_neighbor_n`** is the double-counting dedup's own collision test
(`ScoringPipeline.SharesDoubleCountingFragments`: half the smaller top-6 list matching within its
3-SD tolerance), counted over this run's reconciled targets in the same window whose apex lies
within `osprey.ddc.rt_neighborhood` of this one. The test counts the first entry's fragments
matched in the second, so it is not symmetric; each pair is asked in the dedup's order (apex RT,
then base_id, then entry_id - `ScoringPipeline.CompareDoubleCountingOrder`), earlier entry first. That neighborhood is 5 x the median spacing
between consecutive MS2 retention times of the WHOLE run - all windows interleaved - so on a
DIA cycle it is a fraction of one cycle and the count is almost always 0 (0.01% of rows on the
Stellar smoke run). It is exported as the scorer defines it; the co-elution scope above is the
broader measure.

---

## Run information: `<stem>.run-info.json`

The instrument and fragmentation facts in the footer come from a per-run cache written beside
`.spectra.bin` when that cache is built, from fields the ProteoWizard read already populates
(`Osprey.IO/RunInfoFile.cs`, collected in `SpectrumFileReader`): source file name and
fingerprint, run id and start time, instrument vendor / model / serial number and every declared
instrument configuration, MS1 and MS2 counts and retention-time ranges, MS1 and MS2 scan
windows (the MS2 one run-wide and per isolation window), the MS2 isolation range, and histograms
of MS2 analyzer, dissociation method and collision energy. It is written ONLY when the cache is
built - never on a cache hit - so a run whose cache predates it has none, and its export says so
(empty footer keys and a warning). A run info whose source fingerprint (size, mtime) disagrees
with the one `.spectra.bin` records describes another version of the source; the export ignores
it with a warning. Format and invalidation: [14-intermediate-files](14-intermediate-files.md).

---

## Resume and validity

`TrainingExportTask.ValidityKey` is the base key (search parameters, library identity,
peak-pick arm, and `;libext=ann` for a blib library with fragment annotations), plus:

| Term | Why |
|---|---|
| `fdrsidecar=<version>` | The q-values come from the per-run sidecar format |
| the experiment-aggregation, pass-2 q-value, training-sample and library-fragment arms | The same arms `SecondPassFDR` keys on, because they change the q-values exported |
| `pass2exp=<identity>` | Name + size + mtime of `<blib-stem>.2nd-pass.fdr_experiment.bin`: it is rewritten whenever Stage 7 re-runs, so an export of an older second pass is recomputed |
| `trainexport=<format>;maxq=;claimq=;xics=` | The export's own format and settings |

It deliberately omits the cohort (P4): no reconciliation hash, no stem list, so an HPC node
handed any subset of runs computes the key a straight-through run does.

Each run's parquet is stamped with that key plus the identities (name, size, mtime; `absent`
for a missing file) of the five per-run artifacts it reads that the key does not already
follow (`TrainingExportTask.OutputValidityKey`):

| Term | File |
|---|---|
| `recon=<identity>` | `<stem>.scores-reconciled.parquet` |
| `pass2run=<identity>` | `<stem>.2nd-pass.fdr_scores.bin` |
| `calib=<identity>` | `<stem>.calibration.json` |
| `spectra=<identity>` | `<stem>.spectra.bin` |
| `runinfo=<identity>` | `<stem>.run-info.json` - so rebuilding a spectra cache, which writes it, redoes an export made without it |

A rewritten input redoes that run's export and no other. Resume is per run: a run whose parquet
exists with a matching stamp is skipped (`(current, skipped)`), a stale stamp is cleared before
its run is recomputed, and each run is stamped as it lands, so a killed export loses only the
run in flight. With every run current the driver skips the task.

### HPC relay

A `--task TrainingExport` node needs, per run in its batch: `<stem>.scores-reconciled.parquet`,
`<stem>.2nd-pass.fdr_scores.bin`, `<stem>.calibration.json`, `<stem>.spectra.bin` (with
`--cache-dir` if it is not in the output directory) and, for the instrument footer,
`<stem>.run-info.json`; and experiment-wide, the library, `<blib-stem>.2nd-pass.fdr_experiment.bin`
and `<blib-stem>.1st-pass.retained_base_ids.bin` (without it the library loads every spectrum).
It needs no `.scores.parquet` and no other first-pass artifact.

**Relay with mtimes preserved** (`cp -p`, robocopy `/COPY:DAT`), as the library already
requires: the keys follow each input's name, size and mtime, so a copy that stamps a new mtime
redoes every export it touches.

---

## Risks for a consumer

- **Unit-resolution matches are permissive.** With a ~0.35 Th tolerance, about half of the
  ladder ions a peptide does not produce still find a peak at the apex. Use `corr_polish`,
  `polish_r2`, `n_finite_scans` and the library flags, not `MATCHED_AT_APEX` alone.
- **Osprey's XICs are unsmoothed and closest-peak.** Carafe takes the most intense peak within
  the tolerance and smooths with Savitzky-Golay, so its 0.8 correlation threshold may need
  retuning on these values; the XIC matrix allows recomputing with Carafe's own rule.
- **`polish_rel_intensity` is normalized to the ladder's largest row effect**, which can belong
  to an interfered ion. Renormalize over the ions you keep.
- **`polish_outlier_z` runs large.** Its scale is the core fit's median absolute residual, and
  the polish's own medians set many residuals to exactly zero, so a noise-level residual on a
  clean peak already reads as a z of 2 to 5 (core ions on the Stellar smoke run: 1st-99th
  percentile -5.4 to 4.1, against more than 100 for a six-fold spike in the unit test).
  Calibrate a threshold on the core ions' own distribution rather than reading it as a normal
  z.
- **Collision energy units are the vendor's.**
