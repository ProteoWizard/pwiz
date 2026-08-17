# Osprey C# Port — Cross-Implementation Divergence Report

This report consolidates every divergence found by the 18 per-document parity reviews of the C# port of Osprey against the Rust reference documentation (`00-overview.md` … `19-testing.md`). Each per-doc review compared the Rust algorithm doc against the actual C# source and classified every gap.

## Executive summary

Across all 18 algorithm documents the C# port is a faithful, parity-focused reproduction of the Rust engine: on the Stellar/Astral reference datasets it is bit-identical, enforced by the `regression.ps1` 1e-9 gate (golden + resume + HPC-chain legs) plus `OSPREY_CROSS_IMPL_*` byte-parity hooks. Of **110 catalogued divergences**, the overwhelming majority are either **documentation staleness** (the Rust prose lagging its own evolving Rust code, which the C# port correctly tracks) or **intentional C# infrastructure/CLI redesigns** that preserve output. There is exactly **one genuine PORT-ERROR** — the Razor shared-peptide assignment order in protein parsimony — and it sits off the default, bit-identical-tested path. **Eight items are UNVERIFIED** (the reviewer could not confirm one side against source and flagged them for a human to check). Nothing on the default analysis path was found to change output relative to Rust.

### Count by classification

| Classification | Count |
|---|---:|
| STALE-RUST-DOC | 49 |
| INTENTIONAL-CSHARP-DESIGN | 50 |
| UNVERIFIED | 8 |
| FLAG-GATED | 2 |
| PORT-ERROR | 1 |
| **Total** | **110** |

### Count by severity

| Severity | Count |
|---|---:|
| major | 6 |
| minor | 49 |
| info | 55 |

The 6 `major` items are: the sparse-library calibration retry omission (doc 04), the reconciliation RT-tolerance formula (doc 05, C# tracks current Rust — doc stale), the missing `FdrLevel::Protein` variant (doc 07), the `--task` CLI redesign (doc 15), the Razor determinism concern (doc 16), and the absence of the `OSPREY_TRACE_PEPTIDE` facility (doc 18). Only the first and fifth of those are substantive behavioral concerns; the rest are intentional redesigns or doc-lag.

> **Numbering note.** This report was generated against the original doc numbering
> (Rust-doc order); the detail sections below are grouped by algorithm topic. All
> links point to the current **operation-order** C# doc numbers (see
> [README.md](README.md) for the index). Doc-number references in prose use the new
> numbering.

> **Updates since generation (features added after this report).** `--fdr-method
> gbdt` (doc 07) is a **C#-only** addition — the Rust reference has no GBDT scorer,
> so it is not a divergence to reconcile. The opt-in learned peak-pick model (doc 06)
> and the pass-2 frozen q-value modes (doc 12) postdate this report; both exist in
> Rust too (the pick model in lock-step; the pass-2 modes ported C#→Rust in
> maccoss/osprey#57) and are off the default parity-gated path. The one **PORT-ERROR**
> below (P1, Razor rollup order) is tracked as ProteoWizard/pwiz#4441.

---

## Port errors and items to verify

This is the section a maintainer should read first. It lists the **single PORT-ERROR** and all **8 UNVERIFIED** items — the divergences that either are, or might be, real behavioral differences from Rust.

### PORT-ERROR (1)

#### P1. Razor shared-peptide assignment is a per-peptide greedy, not Rust's iterative group-centric set cover
- **Doc:** [08-protein-parsimony.md](08-protein-parsimony.md)
- **Rust says:** Razor sorts shared peptides alphabetically, then repeatedly selects the *group* with the most unique peptides that still owns any unassigned shared peptide and claims **all** of that group's remaining shared peptides in one batch (`crates/osprey-fdr/src/protein.rs:197-266`); explicitly path-independent, pinned by `test_shared_peptides_razor_cascading_assignment`.
- **C# does:** Iterates shared peptides in `Dictionary` (hash-insertion) order, unsorted, and assigns each individually to its current best group while mutating unique counts in place. Diverges on cascading topologies — e.g. `G0 u{A,B} s{X,Y}`, `G1 u{C,D,E} s{X}`, `G2 u{F} s{Y}`: Rust always yields `X→G1, Y→G0` but C# flips `X→G0` when `Y` is processed before `X`.
- **C# evidence:** `Osprey.FDR/ProteinFdr.cs:432-467`
- **Severity:** minor (non-default flag: default is `--shared-peptides all`; the Razor path is not covered by the All-mode reference gates).
- **Recommended action:** Reimplement Razor as the iterative group-centric batch set cover with alphabetical shared-peptide ordering to match Rust, and port `test_shared_peptides_razor_cascading_assignment`. Note this is the same code region flagged as UNVERIFIED/major for determinism in doc 15 (U8 below) — fixing both together is natural: the sorted, group-batch algorithm is inherently path- and process-order-independent.

### UNVERIFIED (8)

#### U1. No sample-expansion retry loop or graduated linear-fit fallback for sparse libraries — **major**
- **Doc:** [04-calibration.md](04-calibration.md)
- **Rust says:** Library sampling uses 3 attempts (`sample_size`, `× retry_factor`, then ALL entries) plus a `MIN_LINEAR_FIT_POINTS` graduated linear fit and an RT-span leverage check for sparse libraries.
- **C# does:** Samples exactly once (seed 43), enforces `MinCalibrationPoints` (200) as the pass-1 floor, and returns `null → fallback tolerance` when unmet; no retry expansion, no graduated linear fit, no RT-span check. `CalibrationRetryFactor` config exists but is never read. On the Stellar/Astral reference datasets the first 100K sample always clears 200 points so parity holds; **small/sparse libraries would fail where Rust recovers.**
- **C# evidence:** `Osprey.Tasks/Calibrator.cs:196-197, 259, 261-267, 479-485`
- **Recommended action:** This is the highest-value item to verify. Confirm whether any supported input library can fall below the 200-point floor; if so, port the retry-expansion loop + graduated linear-fit fallback. `CalibrationRetryFactor` being dead config is a tell that the port stopped short here.

#### U2. Comet-style E-value not computed in the C# calibration path — info
- **Doc:** [03-spectral-scoring.md](03-spectral-scoring.md)
- **Rust says:** A Comet-style E-value is computed from the XCorr survival function and stored on the calibration match (computed but not used for FDR).
- **C# does:** `CalibrationMatch` has no `evalue` field and no survival-function fit exists; only the LDA discriminant drives competition, so output parity is unaffected.
- **C# evidence:** `Osprey.Scoring/CalibrationScorer.cs:34-56` (struct has no evalue field); no survival function in `SpectralScorer.cs`
- **Recommended action:** Low priority — verify the Rust E-value is truly unused downstream (doc says so). If confirmed unused, close as a safe intentional omission.

#### U3. Blib peak boundaries come from the CWT/detected peak, not median-polish FWHM — minor
- **Doc:** [06-peak-detection.md](06-peak-detection.md)
- **Rust says:** Rust doc gives a blib-boundary priority list where Tukey median-polish FWHM (apex ± 1.96σ) is priority 1 and CWT boundaries are fallback.
- **C# does:** `FdrEntry.StartRt/EndRt` written for output are the CWT/detected `bestPeak` boundaries; median polish is consumed only for four scoring features. README asserts blib is bit-identical cross-impl, so Rust likely also writes CWT bounds (doc stale) — but `Osprey.IO/BlibWriter.cs` was not read to confirm no downstream re-derivation.
- **C# evidence:** `Osprey.Scoring/CoelutionScorer.cs:462`
- **Recommended action:** Read `BlibWriter.cs` and confirm boundaries are written straight from the CWT peak with no median-polish re-derivation. Given the byte-identical blib gate this is almost certainly doc-staleness, not a port error.

#### U4. Simple FDR scores on `coelution_sum`, not a ROC-AUC-selected best feature — minor
- **Doc:** [07-fdr-control.md](07-fdr-control.md)
- **Rust says:** Simple applies target-decoy competition on the best single feature selected by ROC AUC.
- **C# does:** `RunSimpleFdr` scores directly by `e.CoelutionSum` (PIN feature 0) with no ROC-AUC selection. Whether current Rust Simple also just uses `coelution_sum` was not confirmed against Rust source.
- **C# evidence:** `Osprey.FDR/PercolatorEngine.cs:359`
- **Recommended action:** Diff against current Rust `simple` FDR. `--fdr-method simple` is a non-default diagnostic path, so low urgency, but the ROC-AUC selection is easy to port if Rust still has it.

#### U5. No production protein report writer (`*.proteins.csv`) found — minor
- **Doc:** [08-protein-parsimony.md](08-protein-parsimony.md)
- **Rust says:** `write_protein_report()` emits `*.proteins.csv` with gene names, PEP, and q-values.
- **C# does:** No production protein report writer found in the reviewed path (grep for `proteins.csv`/`protein_groups`/`ProteinReport` returned no emitter); only env-var-gated diagnostic dumps `cs_stage6_protein_fdr.tsv`/`cs_stage7_protein_fdr.tsv` exist.
- **C# evidence:** `Osprey/OspreyFileDiagnostics.cs:1918,1983`
- **Recommended action:** Lab memory references a default-emitted `protein_groups.tsv` + `stats.tsv` (see MEMORY: "Osprey protein & summary reports"), so a writer likely lives outside the reviewed files. Confirm the emitter exists and matches Rust's report columns; if it genuinely does not exist, this is a missing feature.

#### U6. Six per-row blob columns written null/zero in the reconciled parquet — minor
- **Doc:** [11-boundary-overrides.md](11-boundary-overrides.md)
- **Rust says:** The reconciled cache carries fragment/XIC/bounds blob columns.
- **C# does:** `fragment_mzs`, `fragment_intensities`, `reference_xic_rts`, `reference_xic_intensities`, `bounds_area`, `bounds_snr` are written null/zero today (tracked follow-up); RT boundaries and the 21 features are still computed and written.
- **C# evidence:** `Osprey/RescoreWorker.cs:64-71`
- **Recommended action:** Confirm nothing downstream (blib, reports) reads these six columns off the *reconciled* parquet. If they are consumed anywhere, this is a real data-loss bug; if not, it's a benign tracked follow-up.

#### U7. Python calibration report tooling (`evaluate_calibration.py`) has no verified C# equivalent — info
- **Doc:** [14-intermediate-files.md](14-intermediate-files.md)
- **Rust says:** Recommends `python scripts/evaluate_calibration.py` for calibration visualization.
- **C# does:** No Python dependency; emits HTML via `--model-diagnostics`. Whether an exact equivalent of that script's output exists was not verified.
- **C# evidence:** `Osprey.Chromatography/CalibrationIO.cs` (no report emit); CLI
- **Recommended action:** Cosmetic/tooling only. Confirm `--model-diagnostics` HTML covers the calibration plots the Python script produced; no output-parity impact.

#### U8. Razor mode may be order-sensitive across processes (single-pass greedy) — **major**
- **Doc:** [16-determinism.md](16-determinism.md)
- **Rust says:** Rust doc §9 describes an iterative greedy set cover that each round claims all of the globally-best group's unassigned shared peptides in sorted order, cited as path-independent (`test_shared_peptides_razor_deterministic`, 10 repeats byte-identical).
- **C# does:** `SharedPeptideMode.Razor` iterates the shared peptides once, assigning each to the currently-best group and mutating unique-peptide counts as it goes; iteration order derives from `HashSet<string>`/`Dictionary` enumeration, which under .NET randomized string hashing is **not guaranteed stable across processes**. Off the default/bit-identical path (default `--shared-peptides all`; the regression gate does not exercise razor) and no C# repeat-run razor determinism test was found, so order-sensitivity is unconfirmed.
- **C# evidence:** `Osprey.FDR/ProteinFdr.cs:432-467`
- **Recommended action:** Same root cause as P1. Fixing Razor to the sorted group-batch algorithm removes both the assignment divergence and the process-order nondeterminism at once. Add a repeat-run determinism test mirroring `test_shared_peptides_razor_deterministic`.

---

## All divergences by document

Legend — Classification: **STALE** = STALE-RUST-DOC, **INTENT** = INTENTIONAL-CSHARP-DESIGN, **FLAG** = FLAG-GATED, **PORT** = PORT-ERROR, **UNVER** = UNVERIFIED.

### [01-decoy-generation.md](01-decoy-generation.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| STALE | Shuffle decoy method not implemented | `Shuffle` selectable alongside Reverse/FromLibrary | Enum has `Shuffle` but generator always reverse+cycles regardless of config (only logs it) | `OspreyConfig.cs:403`; `DecoyGenerator.cs:182,210-236` | minor |
| INTENT | Batch generation always Trypsin | 4-enzyme table w/ enzyme-aware terminal preservation | `DetectEnzyme` exists but batch path always Trypsin/C-term-preserving; correct for tryptic libs | `DecoyGenerator.cs:100,208,306-317` | info |
| INTENT | Non-b/y fragments preserved verbatim | Recalculation covers only b/y | Keeps ion type + ordinal and recomputes m/z (the b↔y swap was removed on BOTH sides, pwiz#TBD / maccoss/osprey#58); copies A/C/X/Z/etc through unchanged (no-op for b/y libs) | `DecoyGenerator.RecalculateFragments` | info |
| INTENT | Overlap gate rejects a candidate too similar to its target | Same rule, same constants | EncyclopeDIA's 0.4 ratio over the full b/y ladder in a fixed 0.02 Da window, then cycling fallback; ported to both sides together so the decoy SET stays identical | `DecoyGenerator.IsCandidateAcceptable`; Rust `DecoyGenerator::is_candidate_acceptable` | info |
| INTENT | 6-residue minimum peptide length enforced at load | Same rule, all format loaders | Hard error naming the peptide, so the overlap gate's structural 1/(n-1) floor stays under 0.4 | `LibraryValidation.ValidatePeptideLength`; Rust `library::validate_peptide_length` | info |
| INTENT | Native managed pairing/marking | Pairing in Rust `osprey_core`/`osprey_io` crates | Pure managed C# (`LibraryDecoyMarker`/`Pairing`/`Manifest`), deterministic sort, 80% gate matches | `LibraryDecoyMarker.cs:88`; `LibraryDecoyPairing.cs:120`; `DecoyPairingManifest.cs:264` | info |

### [04-calibration.md](04-calibration.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| STALE | RT candidate selection uses global tolerance | Per-entry `local_tolerance(factor=3.0,min=0.1)` | One global window `clamp(3*MAD*1.4826,…)` for every entry (matches Rust `run_search`); `LocalTolerance` ported but unused | `ScoringPipeline.cs:145-176`; `RTCalibration.cs:251` | minor |
| STALE | Global tolerance is MAD-based | `max(residual_sd*factor, min)` | Prefers `3*MAD*1.4826`, `residual_sd*factor` only as fallback (matches current Rust) | `Calibrator.cs:290`; `ScoringPipeline.cs:170-177` | minor |
| STALE | `min_rt_tolerance` default 0.5 not 0.1 | Doc states 0.1 min | Default 0.5 min (matches current Rust default) | `RTCalibrationConfig.cs:50` | minor |
| STALE | Cache filename `.calibration.json` | `<stem>.osprey_calibration.json` | `<stem>.calibration.json` (matches Rust `calibration_filename_for_input`) | `CalibrationIO.cs:91` | info |
| UNVER | No retry loop / graduated linear fit (**U1**) | 3-attempt sampling + graduated linear fit + RT-span check | Single sample, hard 200-pt floor → fallback tol; sparse libs fail where Rust recovers | `Calibrator.cs:196-197,259,261-267,479-485` | major |
| INTENT | Calibration XCorr is managed, not BLAS | Comet XCorr via BLAS `sdot` | Managed `SpectralScorer(UnitResolution())`, f32 preprocess; equivalent at F10 | `Calibrator.cs:72,563` | info |

### [05-rt-alignment.md](05-rt-alignment.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| STALE | Consensus weight is sigmoid(SVM), not coelution_sum | Weight = `coelution_sum` | Weight = `sigmoid(score)` floored 1e-6, coelution>0 as filter (matches current Rust) | `ConsensusRts.cs:178` | minor |
| STALE | Median peak width is weighted median | `simple_median` | Same sigmoid-weighted `WeightedMedian` | `ConsensusRts.cs:197` | minor |
| STALE | Consensus gate is run-level precursor hard gate + rescue | Experiment-level FDR at `consensus_fdr` | `RunPrecursorQvalue≤fdr` hard gate then peptide OR protein rescue, all run-level (matches current Rust) | `ConsensusRts.cs:242-250` | minor |
| STALE | Reconciliation tol: within-peptide MAD + sigma-clipped ceiling | `max(0.1, 3*MAD*1.4826)` cross-peptide, single global | Global median of per-peptide within-lib apex MADs, floored 0.1, capped by sigma-clipped per-file MAD (matches current Rust) | `ReconciliationPlanner.cs:96-190,299` | major |
| STALE | Decoy pairing by base_id, not prefix | Decoy detections only (older stripped DECOY_) | Links via `EntryId & 0x7FFFFFFF`, recognizes prefix-less lib decoys | `ConsensusRts.cs:102,115` | info |
| INTENT | Parallelized LOESS inner fit | Serial auto-vectorized loop | `Parallel.For` per-point fit, `t*t` + away-from-zero rounding keeps bit-identity | `LoessRegression.cs:420` | info |
| INTENT | Initial min-point gate differs from doc's 50 | Initial calibration min points: 50 | Pass-1 200, pass-2 50, inner 20; the 50 = `RTStratifiedSampler`/pass-2 floor | `Calibrator.cs:60,259`; `RTCalibrationConfig.cs:41` | minor |
| FLAG | `OSPREY_TRACE_PEPTIDE` trace absent in reconciliation | `[trace]` lines in consensus/plan | Structured `OSPREY_DUMP_*` dumps instead; no TRACE_PEPTIDE in tree | `Stage6Planner.cs:167,202,306` | info |

### [03-spectral-scoring.md](03-spectral-scoring.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| UNVER | E-value not computed (**U2**) | Comet E-value from XCorr survival fn, stored (unused for FDR) | No `evalue` field, no survival fn; only LDA drives competition | `CalibrationScorer.cs:34-56` | info |
| STALE | Unit-res bin count is 2000, not 2001 | 2001 bins (1.0005 m/z) | `UnitResolution()` = 2000 bins; confirmed by test comment | `BinConfig.cs:42-59`; `ScoringTest.cs:1087` | info |
| INTENT | Only the 21 PIN features computed | ~47 scores incl hyperscore, dot_product variants, etc. | Exactly the 21 PIN features; unused fields never populated | `OspreyFeatureCalculators.cs:36-44`; `CoelutionScorer.cs:253-274` | minor |
| INTENT | MS1 features gated by resolution, not MS1 presence | MS1 scoring available when MS1 present | Features 13/14 HRAM-only; `UnitStrategy.HasMs1Features=false` → exactly 0.0 at unit res | `ResolutionStrategy.cs:112/163`; `Ms1Calculators.cs:37-38` | info |

### [02-xcorr-scoring.md](02-xcorr-scoring.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| INTENT | Managed scalar loops, no BLAS/SIMD, no dense vector | XCorr via BLAS `sdot`; calibration forms dense theoretical vector | Managed scalar loop; `O(n_fragments)` sum-at-bins form; bit-identical | `SpectralScorer.cs:186,273`; `Calibrator.cs:1189` | info |
| INTENT | Sparse HRAM cache instead of dense `Vec<Vec<f32>>` | Dense `float[NBins]` (~391 KB) per window | `SparseXcorrSpectrum` (~20 B/peak), recovers flanking-subtracted value on demand, bit-identical (issue #4398) | `SparseXcorrSpectrum.cs:55,93`; `ResolutionStrategy.cs:170` | info |
| STALE | Simplified snippet omits fragment-bin dedup | Pseudocode sums per-fragment with no dedup (would double-count) | Dedups via `visitedBins`; matches dense semantics. Covered by `TestXcorrFragmentBinDedup` | `SpectralScorer.cs:198`; `ScoringTest.cs:382` | minor |
| INTENT | Precision split: unit-res f64, HRAM f32-narrowed | Doc silent on precision boundary | Unit/calibration cache f64; HRAM narrows to float, mirroring Rust HRAM f32 cache | `ResolutionStrategy.cs:119`; `SparseXcorrSpectrum.cs:113` | info |

### [06-peak-detection.md](06-peak-detection.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| STALE | Peak selection uses RT-penalized rank score | Highest mean pairwise fragment correlation | `coelution * exp(-dt²/2σ²) * ln(1+apex)` — Gaussian RT penalty + log-intensity tiebreak (both impls have penalty; doc behind) | `PeakDataExtractor.cs:289` | minor |
| STALE | Median-polish 10 iters / tol 0.01 | 20 iters / 1e-4 | Call site `maxIter=10, tol=0.01` (matches current Rust `pipeline.rs`; doc's 20/1e-4 is old default) | `CoelutionScorer.cs:242` | minor |
| UNVER | Blib boundaries from CWT peak, not median-polish FWHM (**U3**) | Median-polish FWHM priority 1, CWT fallback | `StartRt/EndRt` = CWT `bestPeak` bounds; median polish only for 4 features; `BlibWriter.cs` not read to confirm | `CoelutionScorer.cs:462` | minor |
| STALE | Scale-space/ridge-tracking is aspirational | Loop over scales [2,4,8,16] w/ ridge tracking, S/N<3 | Single data-driven scale from median FWHM, `minConsensusHeight=0.0`; only the old plan is aspirational | `CwtPeakDetector.cs:161` | info |
| STALE | No linear-baseline background subtraction | Trapezoidal AUC minus linear baseline (plan §4.4) | Plain trapezoidal integral, no baseline (area is relative, not for quant) | `PeakDetector.cs:178` | info |
| STALE | Consensus aggregation is median, not sum | Plan §4.3 sums per-transition CWTs | Pointwise `ConsensusMedianCwt` (matches production doc) | `CwtPeakDetector.cs:265` | info |

### [09-multi-charge-consensus.md](09-multi-charge-consensus.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| STALE | Re-scoring engine is `RunCoelutionScoring` | Re-score via `run_search()` w/ boundary_overrides | No `run_search`; overrides via `ScoringContext.BoundaryOverrides` from `PerFileRescoreTask` (equivalent) | `PeakDataExtractor.cs:82-167`; `PerFileRescoreTask.cs:733` | info |
| INTENT | Consensus is a pure selection function | One `select_post_fdr_consensus()` selects + hands off | `SelectRescoreTargets` pure; merge + override re-scoring separate in `PerFileRescoreTask` | `MultiChargeConsensus.cs:51`; `PerFileRescoreTask.cs:879,1066` | info |
| STALE | No Mokapot scoring | Lists Percolator/Mokapot | Native Percolator SVM only; Mokapot not CLI-wired | see 08 | info |
| STALE | FDR gate is `RunPrecursorQvalue` | Generic "FDR threshold" | Gates on `RunPrecursorQvalue≤fdr` specifically (matches Rust `pipeline.rs`) | `MultiChargeConsensus.cs:115` | info |

### [07-fdr-control.md](07-fdr-control.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| INTENT | Native Percolator replaces external Mokapot | 3 methods incl Mokapot (Python subprocess, PIN round-trip) | Native Percolator + Simple only; `FdrMethod.Mokapot` enum never CLI-wired; no Python dep | `OspreyCommandArgs.cs:120`; `OspreyConfig.cs:422` | info |
| INTENT | No `FdrLevel::Protein`; `--fdr-level protein` unreachable | Supports {precursor,peptide,protein,both}; protein filters blib | Enum {Precursor,Peptide,Both}; effective-qvalue throws on Protein; protein q computed/reported but can't gate blib; 2 stale in-code comments | `OspreyConfig.cs:411`; `OspreyCommandArgs.cs:138`; `FdrEntry.cs:136` | major |
| STALE | Default `--fdr-level` is Precursor, not Peptide | Default Peptide | Defaults Precursor (matches Rust `FdrLevel::default()`; doc prose stale) | `OspreyConfig.cs:284` | minor |
| STALE | No gbdt/FastTree method in either impl | Lab memory mentions Rust `--fdr-method gbdt` | No gbdt symbol anywhere; enum {Percolator,Mokapot,Simple}, CLI percolator\|simple | `OspreyConfig.cs:422`; `OspreyCommandArgs.cs:120` | info |
| UNVER | Simple scores on coelution_sum, not ROC-AUC best feature (**U4**) | Best single feature by ROC AUC | Scores directly by `CoelutionSum`; Rust Simple behavior not confirmed | `PercolatorEngine.cs:359` | minor |
| INTENT | Streaming-only Percolator (matches Rust v26.7.0) | Direct path removed v26.7.0 | `DispatchSvm` always streams; former direct branch removed for parity | `PercolatorEngine.cs:336` | info |

### [08-protein-parsimony.md](08-protein-parsimony.md) — **diverges**

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| **PORT** | Razor is per-peptide greedy, not group-centric set cover (**P1**) | Group-batch set cover, alphabetical, path-independent | Per-shared-peptide greedy in Dictionary order; flips assignments on cascading topologies | `ProteinFdr.cs:432-467` | minor |
| INTENT | No `--fdr-level protein` / protein-level output filtering | `experiment_protein_qvalue` feeds protein filtering | Enum {Precursor,Peptide,Both}; CLI rejects protein; q computed/propagated but no filter path | `OspreyConfig.cs:411-416`; `OspreyCommandArgs.cs:138-157`; `ProteinFdrEngine.cs:165-170` | minor |
| UNVER | No production protein report writer (**U5**) | `write_protein_report()` → `*.proteins.csv` | No emitter found; only env-gated diagnostic dumps; lab memory refs `protein_groups.tsv`/`stats.tsv` | `OspreyFileDiagnostics.cs:1918,1983` | minor |
| STALE | Second-pass detected set gates on `config.fdr_level` | Gates on second-pass PEPTIDE FDR | Gates `EffectiveExperimentQvalue(FdrLevel)≤fdr` (precursor default); mirrors Rust `pipeline.rs`; doc stale | `ProteinFdrEngine.cs:171-183` | info |

### [10-cross-run-reconciliation.md](10-cross-run-reconciliation.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| INTENT | HPC stage split with JSON envelope | In-process step inside `run_analysis` | Stage 5 plan / `reconciliation.json` v3 handoff / Stage 6 apply / Stage 7 FDR; envelope byte-parity | `ReconciliationFile.cs:52-107`; `Stage6Planner.cs:48-100` | info |
| STALE | `--no-reconciliation` CLI flag absent | Flag disables reconciliation | No such arg; disable only via `Reconciliation.Enabled=false` | `OspreyCommandArgs.cs:116-117`; `ReconciliationConfig.cs:33` | minor |
| STALE | Worked example still uses coelution_sum | Example weights by `coelution_sum` | `max(1e-6, sigmoid(score))` (matches current Rust + same doc's Step 2) | `ConsensusRts.cs:178` | info |
| STALE | Planner passing-precursor precondition undocumented | "For each scored entry" no per-entry gate | Also requires `(base_id,charge)` in `passingBaseIds`; ties to Rust `reconciliation.rs:560-576` | `ReconciliationPlanner.cs:131-144,209` | minor |
| STALE | Ceiling is sigma-clipped MAD, not plain | "calibration-MAD-based ceiling" | Sigma-clipped median of refined residuals, capped by first-pass MAD; ties to Rust docstring | `ReconciliationPlanner.cs:166-183,299-322` | minor |
| INTENT | Decoy pairing by base_id, not prefix | Matched by DECOY_ prefix | Pairs by `EntryId & 0x7FFFFFFF`; recognizes prefix-less lib decoys | `ConsensusRts.cs:93-118`; `ReconciliationPlanner.cs:120-144` | info |
| INTENT | Second-pass FDR is native Percolator only | "Percolator/Mokapot/Simple applies" | No Python Mokapot; native managed Percolator (or simple) | `SecondPassFdrTask.cs`; see 08 | info |

### [11-boundary-overrides.md](11-boundary-overrides.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| INTENT | Override map on `ScoringContext`, not a param | `run_search()` takes `boundary_overrides` param | Rides on `ScoringContext.BoundaryOverrides`, looked up by candidate id | `ScoringContext.cs:58`; `PeakDataExtractor.cs:88` | info |
| INTENT | Optional file-level parallelism added | Re-scoring strictly sequential across files | Keeps window parallelism + whole-file concurrency under `--parallel-files`; byte-identical | `PerFileRescoreTask.cs:547-584` | minor |
| STALE | Gap-fill two-pass not in the doc | Only 3 override types documented | Also gap-fill two-pass (CWT + forced-integration) via same override channel | `PerFileRescoreTask.cs:1337-1477` | info |
| STALE | Gap-fill progress labels differ | "Gap-fill CWT"/"Gap-fill forced" | "Gap-fill scoring"/"Gap-fill forced integration" (console string only) | `PerFileRescoreTask.cs:1385,1453` | info |
| INTENT | Reconciled parquet is a separate sibling file | Updates `.scores.parquet` in place | Writes `.scores-reconciled.parquet`, stamps `osprey.reconciled` footer (crash-resume safety) | `ReconciledParquetWriter.cs:54,200-204` | minor |
| UNVER | Six blob columns null/zero in reconciled parquet (**U6**) | Reconciled cache carries fragment/XIC/bounds blobs | 6 columns written null/zero (tracked follow-up); RT bounds + 21 features still written | `RescoreWorker.cs:64-71` | minor |

### [13-blib-output-schema.md](13-blib-output-schema.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| STALE | Score type is GENERIC Q-VALUE (19), not PERCOLATOR (14) | ID 14, `scoreType=14` | Seeds full PWiz table; stamps `SCORE_TYPE_GENERIC_QVALUE=19` | `BlibWriter.cs:41,899-922`; `BlibOutputWriter.cs:336` | minor |
| STALE | `RefSpectra.score` is raw q-value, not `1-q` | `score = 1 - q_value` | Raw experiment-precursor q-value (lower better), matches type 19 | `BlibOutputWriter.cs:186-193,229` | minor |
| STALE | `minorVersion` is 11, not 10 | 10 | 11 (`BLIB_MINOR_VERSION`) | `BlibWriter.cs:40` | info |
| STALE | `idFileName` is library basename | Same as `fileName` | Library basename; `fileName=<stem>.mzML` | `BlibOutputWriter.cs:107-113` | minor |
| STALE | `cutoffScore` is run-FDR threshold, not 0.0 | 0.0 | `config.RunFdr` (default 0.01) | `BlibOutputWriter.cs:62,112` | info |
| STALE | Stores bare filename, not relative path | Computes `../data/sample.mzML` | `<stem>.mzML`, no dir/relative computation | `BlibOutputWriter.cs:112` | minor |
| STALE | `Modifications.position` is 1-based | 0-indexed | Converts 0→1-based on write; test asserts 4→5; byte-identical implies Rust also 1-based | `BlibWriter.cs:399-408`; `IOTest.cs:107-149` | minor |
| STALE | Nullable-text columns empty strings, not NULL | 5 columns NULL | Empty-string literals | `BlibWriter.cs:127-131` | info |
| STALE | RetentionTimes best-run ID-line fallback undocumented | RT NULL if run fails FDR | Fallback: lowest-q run still gets RT so every RefSpectra has an ID line | `BlibOutputWriter.cs:288-313` | minor |
| STALE | Osprey extension score/intensity are 0.0 placeholders | DiscriminantScore/PEP/ApexIntensity real | 0.0 for all three (not plumbed; no Mokapot) | `BlibOutputWriter.cs:249-254` | minor |
| STALE | OspreyMetadata key set differs | osprey_version, rt_calibration_enabled, run_fdr, fdr_method | osprey_version, search_mode=coelution, run_fdr, experiment_fdr | `BlibOutputWriter.cs:266-271` | minor |
| INTENT | Native managed SQLite writer, not PWiz BiblioSpec | README frames as reusing PWiz BiblioSpec | Hand-rolled `System.Data.SQLite`, reuses only schema + Skyline Ionic.Zlib L6 convention (byte-identical to flate2) | `BlibWriter.cs:37,980-1008` | info |
| INTENT | Extra schema columns/indices not in doc | Omits workflowType/probabilityType; 7 indices | Declares both columns, 3 extra indices for BiblioSpecLite/Skyline compat | `BlibWriter.cs:737,750,573-575` | info |

### [14-intermediate-files.md](14-intermediate-files.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| INTENT | FileSaver sibling-temp rename, not copy_and_verify | Local temp + `copy_and_verify` to NAS | Same-dir sibling temp + `File.Move` promote; same crash-safety | `FileSaver.cs:68,92`; `ParquetScoreCache.cs:265` | info |
| INTENT | No CacheValidity enum; hash mismatch hard-fails | `{ValidReconciled,ValidFirstPass,Stale}`; Stale→delete+rescore | Returns descriptive error + aborts on mismatch; resume via `.osprey.task` | `ParquetScoreCache.cs:1190,1256` | minor |
| INTENT | `.osprey.task` resume sidecar has no Rust counterpart | Reuse inferred from footer hashes | Per-(output,task) JSON sidecar on search+library(+reconciliation) hashes | `TaskValiditySidecar.cs:80,98,144` | info |
| INTENT | Reconciled parquet separate, not in-place | Stage 6 rewrites in place | `.scores-reconciled.parquet` sibling; survives partial Stage 6 crash | `ParquetScoreCache.cs:1036,1055,1103` | minor |
| INTENT | FDR sidecar matches by entry_id, tolerates count mismatch | `entry_count == entries.len()`, positional | entry_id→index map; allows smaller sidecar over larger stub list; stale summary comment | `FdrScoresSidecar.cs:437,484,492,74` | minor |
| STALE | `reconciliation.json` is format_version 3 | v1, omits file_stems/first_pass_base_ids | Requires v3 + both fields (Rust code evolved past its doc) | `ReconciliationFile.cs:75,77,84,124,138,150` | minor |
| STALE | `.spectra.bin` header is v3 w/ fingerprint | v1 (20 bytes), no size/mtime | v3 w/ source_size+source_mtime (Unix ms), invalidates on fingerprint change | `SpectraCache.cs:61,70,90,162` | minor |
| INTENT | Calibration reuse not gated on `search_hash` | Reused when search_hash matches | Never writes search_hash (field unset); reuse is `.osprey.task` decision | `PerFileScoringTask.cs:1702`; `CalibrationParams.cs:125` | minor |
| UNVER | No verified equivalent to `evaluate_calibration.py` (**U7**) | Recommends Python calibration script | No Python dep; HTML via `--model-diagnostics`; exact equivalence unverified | `CalibrationIO.cs`; CLI | info |

### [15-hpc-scoring-split.md](15-hpc-scoring-split.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| INTENT | CLI is `--task <Name>`, not `--no-join`/`--join-at-pass`/`--join-only` | Orchestrated by `--join-at-pass` + modifiers | Single `--task {PerFileScoring\|FirstPassFDR\|PerFileRescoring\|SecondPassFDR}`; old flags retired, fail fast | `Program.cs:86-128`; `OspreyCommandArgs.cs:206-207` | major |
| INTENT | One name per task, describing the FDR pass | Named by pass/join topology | CLI name, enum member and class are one name per task; residual `PerFileRescoring` vs `PerFileRescore` | `OspreyConfig.cs` (`HpcTask`); `Program.cs` (`ResolveTask`) | info |
| INTENT | Stage 6 separate `.scores-reconciled.parquet` | Rewrites `.scores.parquet` | Separate sibling; `--input-scores` prefers reconciled | `PerFileRescoreTask.cs:163-177,944-954` | minor |
| INTENT | Membership-predicate + lazy-rehydrate, not stage window | Each mode runs stages X..Y | Fixed 4-task pipeline; `IsIncluded` + typed byproduct registry; pinned by truth table | `AnalysisPipeline.cs:99-148`; `PipelineMembershipTest.cs:55-93` | info |
| INTENT | No `--parquet-compression`; ZSTD unconditional | `--parquet-compression snappy` for OspreySharp interop | Writes ZSTD; read auto-dispatches; cross-impl ZSTD/Snappy read compat is follow-up | `ParquetScoreCache.cs:270,462` | minor |
| FLAG | `OSPREY_DUMP_PREDICT_RT` declared but disabled | Stage 6 worker bisection dump | `DumpPredictRt` declared; call site commented out (scoring hotspot) → produces nothing | `IOspreyDiagnostics.cs:80`; `PerFileRescoreTask.cs:715-726` | minor |
| STALE | Stage 4 footer omits `osprey.reconciliation_hash` | Lists it among footer metadata | Stage 4 footer version/search/library/reconciled=false only; reconciliation_hash on Stage 6 parquet | `PerFileScoringTask.cs:226-232`; `ReconciledParquetWriter.cs:198-205` | info |

### [16-determinism.md](16-determinism.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| STALE | Fold assignment is round-robin over sorted groups | `fold = hash(mod_seq) % n_folds` | Round-robin `i % nFolds` over ordinal-sorted keys, no hash (matches Rust `create_stratified_folds_by_peptide`) | `PercolatorFdr.cs:2492-2504`; `CalibrationScorer.cs:402-452` | minor |
| INTENT | TotalOrder bit-transform replaces `total_cmp` | Built-in `f64::total_cmp` | IEEE-754 total order via sign-flipped long key + stable LINQ sort; arithmetic unchanged | `TotalOrder.cs:55-70` | info |
| INTENT | SIMD lane-reduction order differs from scalar left-fold | Sequential scalar left-fold | Per-lane partials + horizontal sum; sub-ULP drift inside 1e-9 gate at p=21 | `LinearSvmClassifier.cs:531-545` | minor |
| INTENT | Oracle is PowerShell regression gate, not inline tests | Inline tests + manual two-blib diff | `regression.ps1` 3 legs at 1e-9 (golden/resume/HPC-chain) | `regression.ps1:14-36,490-543` | info |
| UNVER | Razor single-pass greedy may be order-sensitive (**U8**) | Iterative set cover, path-independent, 10-repeat test | Single-pass greedy over HashSet/Dictionary order; not process-stable under randomized string hashing; off default/tested path | `ProteinFdr.cs:432-467` | major |

### [17-vectorization.md](17-vectorization.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| INTENT | No BLAS/OpenBLAS dependency | Calibration batch is one OpenBLAS `sgemm` | Pure managed; `BatchScorer` "simplified, no BLAS", scalar loops | `BatchScorer.cs:205,247` | info |
| INTENT | All-pairs matmul not used in calibration | ~2.5M cosines as one matmul/window | `BatchXCorr` is scalar triple loop used only by test; live loop scores per candidate | `Calibrator.cs:1184,1189-1191`; `BatchScorer.cs:248-267` | minor |
| INTENT | Explicit SIMD only in SVM | SIMD implicit via LLVM auto-vectorization | RyuJIT doesn't auto-vectorize; only SVM dot/weight hand-vectorized w/ `Vector<double>`; sub-ULP caveat inside gate | `LinearSvmClassifier.cs:516-568,589-602` | minor |
| INTENT | Managed LOH/GC-avoidance infra, no Rust analogue | Rust has no GC | `SparseXcorrSpectrum` + `XcorrScratchPool` keep XCorr off LOH (issue #4398); outputs unchanged | `SparseXcorrSpectrum.cs:55,93`; `XcorrScratchPool.cs:41,72` | info |
| INTENT | osprey-ml matrix ops scalar, no BLAS/parallelism | Manual loops w/ Rayon | `Matrix.Dot`/`GaussSolver` scalar, no SIMD/Rayon (matrices tiny) | `Matrix.cs:235-275`; `GaussSolver.cs:133-230` | info |
| STALE | Bin-count off-by-one in unused batch geometry | 2001 bins 200-2000 Da | `DEFAULT_NUM_BINS=2000`; BatchScorer off any pipeline path, no effect | `BatchScorer.cs:209-211` | info |

### [18-peptide-trace.md](18-peptide-trace.md) — **diverges**

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| INTENT | `OSPREY_TRACE_PEPTIDE` facility absent; replaced by structured dumps | Env-gated `[trace]` log at 5 stages, keyed by modified_sequence, auto-matches decoy | None of it exists (zero matches for TRACE_PEPTIDE/is_traced/[trace]); `IOspreyDiagnostics` `OSPREY_DUMP_*`/`OSPREY_DIAG_*` byte-stable TSV dumps instead | `IOspreyDiagnostics.cs:51`; `OspreyFileDiagnostics.cs:84-423` | major |
| INTENT | Peptide selection by entry_id, not modified_sequence | Bare modified_sequence, auto-groups charges + decoy | Keys on numeric entry_id or MS2 scan; no auto-group of charges/decoys | `OspreyFileDiagnostics.cs:379,393,400` | minor |
| INTENT | Dumps are byte-stable files, not interleaved `[trace]` lines | `log::info!` `[trace]` interleaved, grep-isolated | Dedicated TSV/txt w/ F10 + shortest-round-trip f64 for SHA-256 diffing; only `[COUNT]`/`[BISECT]` to log | `OspreyFileDiagnostics.cs:68-82`; `Diagnostics.cs:60` | minor |
| STALE | `OSPREY_DUMP_PREDICT_RT` producer disabled | RT prediction is traceable/dumpable | Gate throws `NotImplementedException`; producer removed from scoring hotspot; comment names what to uncomment | `OspreyFileDiagnostics.cs:431-445` | info |

### [19-testing.md](19-testing.md) — matches-with-notes

| Classification | Title | Rust says | C# does | Evidence | Sev |
|---|---|---|---|---|---|
| INTENT | Separate MSTest project vs inline `#[cfg(test)]` | Inline unit tests, no separate project | One `Osprey.Test` MSTest project (net472+net8.0), 41 classes / 492 methods | `Osprey.Test.csproj:43-51` | info |
| INTENT | Two standing CI gates have no Rust analog | `cargo test` + fmt/clippy | `regression.ps1` (golden+resume+HPC at 1e-9) + `Test-PerfGate.ps1` (A/B wall-time) | `regression.ps1:1`; `Test-PerfGate.ps1:1` | info |
| INTENT | Static-analysis test enforcing determinism | No such test (Rust `sort_by` stable) | `CodeInspectionTest` forbids `Array.Sort`/`List.Sort` + any `"DECOY_"` literal in reconciliation | `CodeInspectionTest.cs:85,150` | info |
| STALE | Mokapot test suite absent | Full `mokapot.rs` test suite | Native Percolator, no Python dep, no `MokapotTest.cs`; `FdrTest.cs` exercises Percolator | `FdrTest.cs:25` | minor |
| INTENT | Plain MSTest Assert, not AssertEx | N/A (recon framing assumed AssertEx) | Plain `Assert`/`CollectionAssert` (1,314 calls); parity via hand-computed refs + bit compares + `OSPREY_CROSS_IMPL_*` | `IOTest.cs:2082-2115`; `OspreyEnvironment.cs:104` | info |
| STALE | Test count is stale/moving (~302 vs 492) | ~302 tests | 492 `[TestMethod]` measured | `Osprey.Test/*.cs` | info |
| STALE | Regression README documents 2 legs, script runs 3 | N/A (C#-side doc lag) | README lists mode1/mode2; script also runs mode3 (HPC 4-task chain) | `regression.ps1:502-522` vs `Regression/README.md:30-39` | minor |

---

## Confirmed intentional design differences (the big ones at a glance)

These are the deliberate, output-preserving architectural choices in the C# port that recur across many documents. None is a defect; they are recorded so a reader knows what to expect and does not re-flag them:

1. **Native managed Percolator, no Mokapot.** The external Python Mokapot path (PIN round-trip, subprocess, `--save_models`/`--load_models`) is not wired to the C# CLI. `--fdr-method` accepts `percolator | gbdt | simple`. The `FdrMethod.Mokapot` enum value survives but is unreachable. (docs 07, 09, 10, 13, 19)

2. **`gbdt` is a C#-only FDR method.** `--fdr-method gbdt` selects a gradient-boosted-tree classifier inside the Percolator framework (`GradientBoostedTrees.cs`); the linear-SVM Percolator remains the default. The Rust reference has **no** GBDT scorer, so this is a C# addition beyond the reference, not a divergence to reconcile. (doc 07)

3. **`FdrLevel` has no `Protein` variant.** The enum is `{Precursor, Peptide, Both}`; `--fdr-level protein` is rejected. Protein q-values are still computed, propagated, and reported, but cannot gate blib output from the CLI. Two stale in-code comments still reference the removed mode. (docs 07, 08)

4. **No BLAS/OpenBLAS; pure managed numerics.** XCorr and cosine are managed scalar loops; the only explicit SIMD is the SVM dual-coordinate descent (`System.Numerics.Vector<double>`). The calibration "one big `sgemm`/`dot`" matmul is replaced by per-candidate scoring. All results stay on the 1e-9 gate. Adds managed-only LOH/GC-avoidance infra (`SparseXcorrSpectrum`, `XcorrScratchPool`). (docs 02, 04, 17)

5. **ProteoWizard/Skyline blib reuse is schema-only.** `BlibWriter` is a hand-rolled `System.Data.SQLite` writer that reuses the BiblioSpec schema and Skyline's Ionic.Zlib level-6 byte convention (byte-identical to Rust `flate2`), not the C++ BiblioSpec code. (doc 13)

6. **MSTest project instead of inline Rust tests.** All tests live in one `Osprey.Test` MSTest project (492 `[TestMethod]`s ported crate-for-crate) plus two CI gates with no Rust analog: `regression.ps1` (golden + resume + HPC-chain at 1e-9) and `Test-PerfGate.ps1` (A/B wall-time), and a `CodeInspectionTest` that forbids unstable `Array.Sort`/`List.Sort` and stray `DECOY_` literals in reconciliation code. (docs 16, 19)

7. **HPC task naming redesign.** The Rust `--no-join`/`--join-at-pass`/`--join-only` flag family is replaced by a single `--task {PerFileScoring|FirstPassFDR|PerFileRescoring|SecondPassFDR}` selector with `IsIncluded` membership predicates and lazy byproduct rehydration; boundary files stay byte-identical. Related: Stage 6 writes a separate `.scores-reconciled.parquet` sibling (crash-safety) rather than rewriting in place, and reconciliation is carried through a serialized `reconciliation.json` (format_version 3) envelope. (docs 10, 11, 14, 15)

---

## Document index

Numbered in **operation order** (the current C# doc numbering).

| # | Document | Parity status |
|---|---|---|
| 01 | [decoy-generation](01-decoy-generation.md) | matches-with-notes |
| 02 | [xcorr-scoring](02-xcorr-scoring.md) | matches-with-notes |
| 03 | [spectral-scoring](03-spectral-scoring.md) | matches-with-notes |
| 04 | [calibration](04-calibration.md) | matches-with-notes |
| 05 | [rt-alignment](05-rt-alignment.md) | matches-with-notes |
| 06 | [peak-detection](06-peak-detection.md) | matches-with-notes |
| 07 | [fdr-control](07-fdr-control.md) | matches-with-notes |
| 08 | [protein-parsimony](08-protein-parsimony.md) | **diverges** |
| 09 | [multi-charge-consensus](09-multi-charge-consensus.md) | matches-with-notes |
| 10 | [cross-run-reconciliation](10-cross-run-reconciliation.md) | matches-with-notes |
| 11 | [boundary-overrides](11-boundary-overrides.md) | matches-with-notes |
| 12 | [second-pass-fdr](12-second-pass-fdr.md) | C#-originated (no Rust doc) |
| 13 | [blib-output-schema](13-blib-output-schema.md) | matches-with-notes |
| 14 | [intermediate-files](14-intermediate-files.md) | matches-with-notes |
| 15 | [hpc-scoring-split](15-hpc-scoring-split.md) | matches-with-notes |
| 16 | [determinism](16-determinism.md) | matches-with-notes |
| 17 | [vectorization](17-vectorization.md) | matches-with-notes |
| 18 | [peptide-trace](18-peptide-trace.md) | **diverges** |
| 19 | [testing](19-testing.md) | matches-with-notes |
