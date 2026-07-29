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
