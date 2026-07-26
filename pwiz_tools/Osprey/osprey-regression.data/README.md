# Osprey regression golden data

Committed golden output for the self-contained C# regression gate:

```
pwsh -File ./pwiz_tools/Osprey/regression.ps1 -Dataset Stellar   # or -Dataset All
```

`blib_summary.tsv` is the per-dataset digest (row counts and per-column
sum / min / max); `tables/*.tsv` are the full table dumps. The gate compares a
fresh run against these at 1e-9. A diff here means output moved -- which is
sometimes a bug and sometimes the point of the change. Re-bless deliberately, and
say why in the PR.

## Datasets

| Golden folder | Decoys | Entrapment | Resolution | Role |
|---|---|---|---|---|
| `stellar` | generated (reverse) | no | unit | the fast local pre-commit gate |
| `stellar-libdecoy` | library-supplied (Carafe) | yes, r=1.0 | unit | the path we recommend; the only one that can measure true FDP |
| `astral` | generated (reverse) | no | hram | larger, HRAM, MS1 features live |

`stellar` and `astral` cover the **generated**-decoy construction; `stellar-libdecoy`
covers the **library**-supplied one. Before this split, every parity and determinism
signal we had was collected on generated decoys only -- the construction we do not
recommend.

## diagnostics.tsv -- and why a golden alone is not enough

Datasets whose spec sets `ModelDiagnostics` also carry `diagnostics.tsv`: a flat
metric projection pulled out of the run's `--model-diagnostics` HTML (null-alignment
tilt, plateau pi0, the paired decoy-win coin, and -- where entrapment exists -- the
true FDP at a reported 1% q).

It is compared at 1e-9 like everything else here, but it exists for a second reason.
The blib golden proves the SEARCH did not change; it cannot prove the FDR CALIBRATION
is still correct, because a change can leave the ranking intact and only wreck the
reported q-values. That is precisely what the b<->y decoy swap did -- ~12x the claimed
error rate, with a perfectly self-consistent golden.

So `Regression/DiagnosticsGolden.ps1` also carries **sanity bounds that `-CreateGolden`
does NOT regenerate** (FDP ceiling, coin within a tolerance of a fair 0.5, a tilt
ceiling). A rebaseline records whatever the run produced; the bounds are the only thing
that fails when a bad change is blessed into the baseline. `-CreateGolden` refuses to
capture a golden from a run that violates them.

**Reading the FDP numbers**: they are quoted at the LAST q-grid point with q <= 0.01,
which is both correct FDR semantics and the convention every recorded measurement in the
gendecoy investigation used. Quote **Pass 1**, not Pass 2 -- the pass-2 Percolator
retrain is known to inflate FDR.

## 2026-07: Stellar lost ~10% of its IDs on purpose

The intensity log-conditioning change (pwiz#4412, maccoss/osprey#53) re-blessed
both goldens, and they moved in OPPOSITE directions:

| Dataset | RefSpectra (IDs) | Proteins |
|---------|------------------|----------|
| Astral  | 160,358 -> 165,500 (+3.2%) | 13,989 -> 14,201 (+1.5%) |
| Stellar | 57,112 -> **51,444 (-9.9%)** | 7,350 -> **7,042 (-4.2%)** |

**The Stellar drop is correct. Do not restore the old counts.**

`peak_apex`, `peak_area`, and `peak_sharpness` used to reach the Percolator SVM as
raw, heavy-tailed values, so a lone high-intensity DIA interference could
standardize to a z-score of 100-300 and hijack the top of the score ranking. On
Stellar that was inflating FDR: the old, higher ID count included IDs that only
passed because the q-values were optimistic. Conditioning the features with
`log10(max(x, 0) + 1)` removes the hijack, the q-values become honest, and fewer
IDs clear 1% q. Fewer IDs at a nominal threshold is not a regression when the
threshold was previously being missed.

This was **validated against the entrapment oracle** (Brendan, 2026-07-13), which
is the only thing that could settle it: decoy-derived statistics alone cannot
distinguish "FDR became more conservative" from "discrimination got worse" -- both
produce fewer IDs and a higher mean q. See the FDRBench entrapment section of
`ai/docs/osprey-development-guide.md` (sibling pwiz-ai repo) -- for any change that
moves the discovery set, the oracle outranks both the golden and cross-impl parity.

Cross-impl parity on the new Stellar golden is confirmed: `Compare-EndToEnd-Crossimpl`
passes at 1e-9, with the C# and Rust implementations independently producing the same
51,444 precursors.
