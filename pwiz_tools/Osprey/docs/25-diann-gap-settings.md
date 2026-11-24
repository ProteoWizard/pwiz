# Settings that close the DIA-NN gap

**The recipe is now the default**, except where one-at-a-time drops showed a setting does not pay. For HRAM data
(`ResolutionDefaults.HRAM`, `Osprey.Core/ResolutionDefaults.cs`): gbdt, `--protein-boost best` (and so the two-round
training), 12 fragment XICs, the 7 ppm correlation tolerance, the correlation-chosen reference XIC, the local RT
tolerance over 15 neighbors, the 0.40 min tc_* window, the 1.0 min RT tolerance floor and the 0.10 LOESS bandwidth,
besides the `default` feature set. At every
resolution: out-of-fold gbdt scoring, no reconciliation refit and the ratio PEP. Unit-resolution data keeps
`--protein-boost compact` (the protein-compact second pass) until Stellar is measured on a larger cohort, and the
values in the Default column for the rest. Each variable still overrides its default
(`OSPREY_FDR_MODEL=svm`, `OSPREY_GBT_OOF=0`, `--protein-boost compact`, `OSPREY_XIC_FRAGMENTS=6`,
`OSPREY_CORR_FRAGMENT_TOLERANCE=off`, `OSPREY_REF_XIC=intensity`, `OSPREY_LOCAL_RT_TOLERANCE=off`,
`OSPREY_MIN_RT_TOLERANCE=0.5`, `OSPREY_TC_HALF_WINDOW=0.15`, `OSPREY_LOESS_BANDWIDTH=0.3`, `OSPREY_RECON_REFIT=on`,
`OSPREY_PEP_FORMULA=mixture`).

The tc_* window and the LOESS bandwidth looked like noise in drop-one checks on 3 and 20 SEA-AD files, but on all
82 files each one alone lost detections at 1% true FDP: 0.15 min for the window -0.7%, 0.3 for the bandwidth -1.8%
(pwiz-ai `TODO-20261114_osprey_hram_defaults.md`). Judge a scoring default on the full cohort. The 1.0 min floor
gained 2.8% at 20 files for about 20% more scoring time and 10% more scores parquet. The seed stays 42.

Before that, Osprey's defaults reproduced the master analysis. The settings below, then all opt-in, are what took Osprey from
~40% behind DIA-NN to within ~5% on SEA-AD Astral (82 files) with the gradient-boosted trees, and ahead of
DIA-NN without its neural network using the linear SVM. They cost time and, above all, disk; a much smaller
feature set is under study as the eventual default (issue #5018).

## The recipe

| Setting | Value | Default (unit resolution; HRAM where marked) | What it changes |
|---|---|---|---|
| `--protein-boost` | `best` | `best` | `best`: a second training round with each precursor's best first-pass score among the other peptides of its protein, and the transfer second pass ([12](12-second-pass-fdr.md)) |
| `OSPREY_FDR_MODEL` | `gbdt` (or `svm`) | `gbdt` | First-pass classifier ([07](07-fdr-control.md)) |
| `OSPREY_GBT_OOF` | `1` (gbdt) | on | Out-of-fold tree scoring |
| `OSPREY_FEATURE_MASK` | `none` | unset: `--feature-set core\|efficient\|default\|all` decides (`default`, 70 features, at both resolutions) | Every one of the 109 PIN features reaches the classifier (and is recorded in the scores parquet); `--feature-set all` does the same without the research switch |
| `OSPREY_XIC_FRAGMENTS` | `12` | `6` (HRAM: `12`) | Fragment XICs per candidate ([03](03-spectral-scoring.md)) |
| `OSPREY_CORR_FRAGMENT_TOLERANCE` | `7` (ppm) | the calibrated tolerance (HRAM: 7) | A second, wider XIC set for the correlation features |
| `OSPREY_REF_XIC` | `corr` | most intense fragment (HRAM: `corr`) | Correlation-chosen reference XIC |
| `OSPREY_LOCAL_RT_TOLERANCE` | `15` | off (HRAM: 15) | RT window widened at each library RT by the residual spread of the 15 nearest calibration points |
| `OSPREY_MIN_RT_TOLERANCE` | `1.0` (min) | 0.5 (HRAM: 1.0) | RT search-window floor ([05](05-rt-alignment.md)) |
| `OSPREY_TC_HALF_WINDOW` | `0.40` (min) | 0.15 (HRAM: 0.40) | Half-width of the tc_* time-correlation window |
| `OSPREY_LOESS_BANDWIDTH` | `0.10` | 0.3 (HRAM: 0.10) | RT calibration LOESS bandwidth ([05](05-rt-alignment.md)) |
| `OSPREY_RECON_REFIT` | `off` | off | Skips the Stage 6 consensus refit; each run keeps its own first-pass RT calibration |
| `OSPREY_FDR_SEED` | `1` | 42 | Percolator seed (results vary ~1.5% SD across seeds at 20 files) |

`--protein-boost best` also turns on two-round training at 200,000 rows (`OSPREY_GBT_PREFILTER_SIZE`).

## Results (SEA-AD Astral, split entrapment, precursor level, true FDP 1%)

Matched 3 files (A01_005, A06_010, A10_014), split-sized library, after the second pass, DIA-NN 2.7.0 on the
same peptides with its own split draw:

| Arm | Found at true FDP 1% | Reported q 1%: kept (true FDP) | vs DIA-NN |
|---|---|---|---|
| Osprey defaults | 45,858 | 46,000 (1.01%) | -41.7% vs NN, -26.6% vs `--no-nn` |
| Osprey, recipe, `OSPREY_FDR_MODEL=gbdt` | 74,646 | 73,978 (0.91%) | -5.1% vs NN |
| Osprey, recipe, `OSPREY_FDR_MODEL=svm` | 65,728 | 64,415 (0.85%) | +5.2% vs `--no-nn` |
| DIA-NN NN | 78,668 | 75,125 (0.59%) | |
| DIA-NN `--no-nn` | 62,472 | 56,662 (0.42%) | |

82 files, GBDT recipe (BRENDANX-UW8, i9-14900K, 32 threads, same machine for both): Osprey 7 h 27 min,
96,654 at FDP 1%; DIA-NN 9 h 49 min, 101,269 (Osprey 1.32x faster, -4.6%). At 3 and 16 files: 1.33x and 1.24x
faster, -5.1% and -6.2%.

## Running it

Set the variables in the environment of the Osprey process and pass the arguments:

```powershell
$env:OSPREY_FDR_MODEL = 'gbdt'; $env:OSPREY_GBT_OOF = '1'; $env:OSPREY_PASS2_QVALUE = 'transfer'
$env:OSPREY_FEATURE_MASK = 'none'; $env:OSPREY_XIC_FRAGMENTS = '12'; $env:OSPREY_CORR_FRAGMENT_TOLERANCE = '7'
$env:OSPREY_REF_XIC = 'corr'; $env:OSPREY_LOCAL_RT_TOLERANCE = '15'; $env:OSPREY_MIN_RT_TOLERANCE = '1.0'
$env:OSPREY_TC_HALF_WINDOW = '0.40'; $env:OSPREY_LOESS_BANDWIDTH = '0.10'; $env:OSPREY_RECON_REFIT = 'off'
$env:OSPREY_FDR_SEED = '1'
Osprey -i <runs> -l <library> -o out.blib --resolution hram --protein-boost best
```

The log's "Environment switches:" line lists every variable the run read; keep it with the results. Every variable is
in the validity keys of the tasks it changes except `OSPREY_GBT_OOF`, so run a change of that one into a new output
directory.

## Cost

Measured on the matched 3 files, per run:

| | Defaults | Recipe (GBDT) |
|---|---|---|
| `.scores.parquet` | 1.8-1.9 GB | 3.7-3.9 GB (109 features, float64) |
| `.scores-reconciled.parquet` | 0.37-0.40 GB (protein-compact) | 0.16-0.17 GB (transfer reconciles only reported precursors) |
| run directory, 3 files | 7.7 GB | 13 GB |

Both also keep the 4.4 GB `.spectra.bin` cache per run beside the input. DIA-NN 2.7.0 reading the .raw keeps a 74 MB
`.quant` per run and its reports.
