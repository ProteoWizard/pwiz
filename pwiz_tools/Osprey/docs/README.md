# Osprey (C#) algorithm documentation

Deep-dive documentation of the algorithms in **Osprey**, the C# (.NET 8) port of
Mike MacCoss's Rust [Osprey](https://github.com/maccoss/osprey) — a
peptide-centric DIA search tool that scores fragment-XIC co-elution with rigorous
FDR control and emits BiblioSpec `.blib` output directly consumable by Skyline.

Unlike the Rust `docs/` set (numbered by when each algorithm was written), **this
set is numbered by pipeline execution order** — read it top to bottom and you
follow a run from library preparation through the `.blib` write. Each numbered doc
is a one-to-one deep dive on a Rust source doc and **ends with a "Divergences from
the Rust documentation" section**; those are consolidated in
[DIVERGENCES.md](DIVERGENCES.md).

> **Just want to run it?** Jump to the [command-line reference](20-command-line.md)
> — every flag with defaults, plus copy-paste examples for unit (Stellar) and HRAM
> (Astral) datasets.

## Ordered index

| # | Doc | What it covers |
|---|-----|----------------|
| 01 | [decoy-generation](01-decoy-generation.md) | Enzyme-aware sequence reversal (cleavage residue preserved), fragment m/z recompute for b/y swaps, FDRBench decoy-pairing manifest. |
| 02 | [xcorr-scoring](02-xcorr-scoring.md) | The Comet-style XCorr primitive: spectrum preprocessing, bin sizes, flanking-bin subtraction, the vectorized dot product. |
| 03 | [spectral-scoring](03-spectral-scoring.md) | The 21 PIN features and how each is computed (pairwise coelution, peak shape, spectral-at-apex, mass accuracy, RT deviation, MS1, Tukey median polish, SG-weighted multi-scan). |
| 04 | [calibration](04-calibration.md) | Per-file RT + MS1 + MS2 calibration: quick unit-resolution XCorr pass, LDA scoring, LOESS fit, mass-error stats, calibration-JSON caching. |
| 05 | [rt-alignment](05-rt-alignment.md) | LOESS calibration + inverse prediction, weighted-median consensus library RT, peak imputation — the RT machinery shared by calibration and reconciliation. |
| 06 | [peak-detection](06-peak-detection.md) | CWT consensus peak detection (Mexican Hat wavelet, pointwise median, ±2σ boundaries with valley guard), the product-form pick, and the opt-in learned linear pick model. |
| 07 | [fdr-control](07-fdr-control.md) | First-pass two-level FDR (run + experiment): native Percolator SVM, the C#-only GBDT method, simple TDC, dual precursor+peptide q-values, PEP, and the opt-in mean(best-N) experiment-wide aggregation (`OSPREY_EXPERIMENT_AGG`). |
| 08 | [protein-parsimony](08-protein-parsimony.md) | Protein parsimony (bipartite graph, identical-set merging, subset elimination, razor) and picked-protein FDR; the first pass builds the **protein-compact stratum** used by second-pass FDR. |
| 09 | [multi-charge-consensus](09-multi-charge-consensus.md) | Post-FDR sharing of peak boundaries across charge states; the best passing charge leads (lowest-charge tie-break), disagreeing charges re-scored at consensus boundaries. |
| 10 | [cross-run-reconciliation](10-cross-run-reconciliation.md) | Consensus RT, per-file LOESS refit, and the per-entry reconciliation plan (Keep / UseCwtPeak / ForcedIntegration). |
| 11 | [boundary-overrides](11-boundary-overrides.md) | How the search accepts override boundaries so consensus and reconciliation targets re-score on the same window / XCorr path as the first pass. |
| 12 | [second-pass-fdr](12-second-pass-fdr.md) | Stage-7 second-pass FDR and the frozen-model q-value modes — transfer-compete and protein-compact — selected by `OSPREY_PASS2_QVALUE`. |
| 13 | [blib-output-schema](13-blib-output-schema.md) | BiblioSpec SQLite schema plus Osprey extension tables and the nullable `retentionTime` convention for Skyline ID lines. |
| 14 | [intermediate-files](14-intermediate-files.md) | On-disk caches / sidecars (calibration JSON, spectra cache, `.scores.parquet`, FDR sidecars), SHA-256 footer hashing, and the tiered memory architecture. |
| 15 | [hpc-scoring-split](15-hpc-scoring-split.md) | The four `--task` workers, their input/output files, `--input-scores` ordering rules, and validity sidecars for HPC / NextFlow orchestration. |
| 16 | [determinism](16-determinism.md) | Patterns that keep results bit-identical across runs: thread-order independence, float stability, cross-validation fold assignment. |
| 17 | [vectorization](17-vectorization.md) | Performance-critical vectorization — the SIMD / BLAS-equivalent paths for XCorr and matrix operations. |
| 18 | [peptide-trace](18-peptide-trace.md) | The per-peptide diagnostic dump facility (C# `OSPREY_DUMP_*` / `OSPREY_DIAG_*` in place of the Rust `OSPREY_TRACE_PEPTIDE`). |
| 19 | [testing](19-testing.md) | The C# test suite and the standing gates: `regression.ps1` (straight-through correctness at 1e-9) and the cross-impl drift bridge against Rust. |
| 20 | [command-line](20-command-line.md) | Full CLI option reference (every flag, default, and value list) with copy-paste unit (Stellar) and HRAM (Astral) examples and the four-task HPC split. |

## Supplementary

| Doc | What it covers |
|-----|----------------|
| [DIVERGENCES.md](DIVERGENCES.md) | Consolidated cross-implementation parity report — every per-doc "Divergences from the Rust documentation" section in one place, classified (stale-doc / intentional-C# / port-error / unverified). |
| [peak-model-training.md](peak-model-training.md) | Retraining the opt-in learned peak-pick model (06): feature capture → train → promote, with the score equations. |
| [fractional-entrapment.md](fractional-entrapment.md) | Entrapment-based FDR-accuracy diagnostics (`--model-diagnostics`): why a fractional entrapment ratio still yields a valid FDP estimate. |

## Cross-implementation status

The C# port is end-to-end **bit-identical to the Rust reference** for Stages 1–7
plus `.blib` output on the Stellar (`--resolution unit`) and Astral
(`--resolution hram`) reference datasets, enforced by the `regression.ps1` 1e-9
gate. Stages 1–4 PIN features are ULP-identical on Stellar; on Astral 19 of 21 are
ULP, with `xcorr` and `sg_weighted_xcorr` drifting at ~1e-7 from the intrinsic f32
HRAM preprocessed-bin cache. Stages 5–6 are byte-equal at every diagnostic dump,
Stage 7 protein FDR matches at 1e-9, and the `.blib` matches at the SQL row +
column level.

A handful of features are **C#-only additions beyond the reference** (the
`--fdr-method gbdt` classifier; the opt-in learned pick model, which also exists in
Rust; the pass-2 frozen q-value modes, which the C# originated and Rust is porting
back). Those are called out in the relevant docs and in
[DIVERGENCES.md](DIVERGENCES.md); none changes the default, parity-gated path.
