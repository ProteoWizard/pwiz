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
| `stellar-libdecoy` | library-supplied (Carafe) | yes, r=1.0 | unit | the path we recommend; measures true FDP without ever calling `DecoyGenerator` |
| `stellar-gendecoy-entrap` | generated (reverse) | yes, r=1.0 | unit | the same entrapment library with its decoy rows stripped: the only dataset that measures **generated** decoys against a true-FDP oracle |
| `astral` | generated (reverse) | no | hram | larger, HRAM, MS1 features live |

`stellar`, `stellar-gendecoy-entrap` and `astral` cover the **generated**-decoy
construction; `stellar-libdecoy` covers the **library**-supplied one. Before this split,
every parity and determinism signal we had was collected on generated decoys only -- the
construction we do not recommend.

`stellar-gendecoy-entrap` exists because of a gap the other three could not close. The
FDP ceiling is the guard against a `DecoyGenerator` regression, but the only dataset that
HAD entrapment to measure FDP against was `stellar-libdecoy`, which bypasses
`DecoyGenerator` entirely; the two that call it had no entrapment. So the guard could not
see the failure class it was built for. Its library is derived at run time by stripping
rows whose `ProteinID` carries the `decoy_` prefix -- not the `Decoy` column, which is 0
on every row of a Carafe entrapment library. Never "fix" that column.

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

## 2026-07-26: both gendecoy goldens lost a fifth to a third of their IDs on purpose

Removing the decoy b<->y intensity swap (pwiz#4480, maccoss/osprey#58) re-blessed the two
generated-decoy goldens. Both dropped, and by a lot:

| Dataset | RefSpectra (IDs) | Proteins |
|---------|------------------|----------|
| Stellar | 51,444 -> **35,222 (-31.5%)** | 7,042 -> **5,664 (-19.6%)** |
| Astral  | 165,500 -> **126,680 (-23.5%)** | 14,201 -> **11,220 (-21.0%)** |

**These drops are correct. Do not restore the old counts.**

The old construction mapped a target b_k to a decoy y_{n-k} and carried the copied
intensity along with the relabel. Fragment intensity is dominated by ion TYPE, not by
which residues an ion spans -- y ions are systematically more intense than b ions -- so
the swap inverted the decoy spectrum's intensity structure relative to any real peptide.
The decoys were therefore easy to beat, the target-decoy null was not a null, and the
reported q-values were far too optimistic. Entrapment-measured true FDP at a claimed 1% q:

| Dataset | with the swap | without |
|---|---|---|
| Stellar (unit) | 10.9% | 1.5% |
| Astral (hram)  | 7.6%  | 2.0% (library-decoy reference: 1.9%) |

So roughly a tenth of the old Stellar "1% FDR" set was false at best. Fewer IDs at a
nominal threshold is not a regression when the threshold was previously being missed by
an order of magnitude. Skyline, OpenSWATH, DIA-NN, EncyclopeDIA and SpectraST all map the
intensity to the same ion; none of them swaps.

`stellar-libdecoy` is byte-identical across this change, proven by content hash, because
`--decoys-in-library` never calls `DecoyGenerator`. That is the control: the goldens that
moved are exactly the ones that generate decoys.

Astral was then re-recorded a second time for the theoretical-ladder fix (y ions from
suffix sums). It is the only dataset affected, because its library carries 60
selenocysteine (U) peptides and Stellar's carries none; three of them stop getting a
degraded cycled decoy. That recovered 206 IDs (126,474 -> 126,680), which is the
expected direction - a better decoy null is a slightly less pessimistic one.

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
