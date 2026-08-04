# Fractional entrapment for FDR-accuracy diagnostics

Osprey's `--model-diagnostics` report estimates the true false-discovery proportion
(FDP) of a search from an *entrapment* set — known-absent sequences (e.g. peptides
from a foreign species) mixed into the searched library, whose reported hits reveal
how well the reported q-values control the real error rate.

The entrapment set does **not** have to be the same size as the target set. Osprey
supports a *fractional* entrapment ratio — for example a routine **10% overlay**
(entrapment:target ratio `r ≈ 0.1`) — which measures the FDP at a fraction of the
detection cost of a full 1:1 (`r = 1`) entrapment library. This note explains why a
non-1:1 ratio still yields a valid estimate, so the calibration curve in the report
can be trusted (and cited) when `r ≠ 1`.

## The estimator is ratio-corrected, so any ratio is valid

Entrapment error estimation counts how often a known-false marker is reported above a
score threshold and scales that count by the marker-to-target database-size ratio.
The ratio is carried explicitly in the estimator, so any ratio is admissible as long
as it is accounted for. With `N_T`, `N_E` the target and entrapment discoveries and
`r = |E| / |T|` the entrapment:target size ratio, the **combined** upper-bound
estimator is

    FDP = (1 + 1/r) · N_E / (N_T + N_E)

`r` is a free parameter; the `1/r` term is the ratio correction. Its only assumption
is the ratio-aware *equal-chance* condition — that an incorrect match lands on a
target or a marker in proportion to their database sizes — which does not depend on
`r`. The **lower-bound** estimator, `N_E / (r·(N_T + N_E))`, is likewise ratio-aware
and valid at any ratio (it can only demonstrate a failure of FDR control, never
confirm it); note `combined = lower-bound + N_E/(N_T + N_E)`, so at `r = 1` the
combined value is exactly twice the lower bound.

A 1:1 ratio is required **only** for the **paired** estimator, a distinct refinement
that pairs each target with one unique entrapment twin (so `r = 1`) *and* requires
that twin be a shuffle or reversal of the target. Foreign-species entrapment has no
such twin, so the paired estimator does not apply to it at any ratio. Osprey's report
therefore shows the combined and lower-bound curves at any ratio and suppresses the
paired curve when the library is not ~1:1.

## Published basis

This is a long-standing, peer-reviewed result, not a new relaxation.

**Fitzgibbon, Li & McIntosh (2008)** derived the ratio-corrected estimator and
demonstrated the fractional case directly. Writing `k = |target| / |decoy|`:

> "when not of equal size, the fundamental assumption of both PeptideProphet and the
> classic FIR approach can be stated as: an incorrect hit will match either the decoy
> or target database in proportion to their size. Specifically, when the relative
> size of the target to decoy database is k to 1 then
> **FIR = (k + 1)(no. decoy > x)/(no. total > x)**, which defaults to a more standard
> calculation when k = 1."

With `k = 1/r`, this is `(1 + 1/r) · N_E / (N_T + N_E)` — algebraically identical to
the combined estimator above. Their Figure 1D plots yield versus error rate for
decoy databases at **1/2, 1/4, and 1/10** of the target size (`r` = 0.5, 0.25, 0.1)
and one 10× larger, and concludes:

> "For decoy databases which are smaller or equal to the target, the overall
> performance is not highly distinguishable. For the larger database, the yield
> declines dramatically …"

and that "using equally sized databases … is not a strict requirement," so "using
smaller ones in situations where [the error rate] can be estimated sufficiently with
them would be preferred," because larger databases buy precision "with the cost of …
fewer identifications."

The equal-size *requirement* that is sometimes assumed universal belongs specifically
to the separate-search q-value formulation of **Käll, Storey, MacCoss & Noble
(2008)** — the lineage of the *paired* estimator — not to the ratio-corrected
combined estimator. **Wen et al. (2025)** carry the same split into the entrapment
setting: their combined estimator is valid at any `r` (the classic-FIR lineage),
while their paired estimator requires `r = 1` and shuffled twins.

## Validation on the Stellar test dataset

Measuring the combined estimate over a matched foreign-species (*Arabidopsis*)
entrapment set at several ratios (reported precursor pool, 1% reported q):

| r (entrapment:target) | combined FDP | target detections |
|---|---|---|
| 1.00 | 0.96% | 27,112 |
| 0.50 | 1.05% | 28,595 |
| 0.25 | 0.96% | 29,116 |
| 0.10 | 1.09% | 29,576 |

- The estimate is essentially **invariant (~1.0%) across a 10× change in `r`** — the
  ratio correction behaving exactly as the theory predicts.
- **Detections are recovered as `r` shrinks** (target detections rise from 27,112 to
  29,576): at `r = 1` a 50%-marker library perturbs the search and suppresses
  detections; near `r ≈ 0.1` the analysis is close to native. This is the
  precision-versus-yield trade-off Fitzgibbon et al. describe, in reverse.

The estimate is also sensitive to marker *realism* independently of ratio:
target-anagram shuffled entrapment reads meaningfully higher than real
foreign-species peptides, because shuffled anagrams share the target's amino-acid
composition (and therefore many fragment masses) and are over-identified.
Foreign-species entrapment is the more faithful model of the sequences that cause
real false identifications.

## Why entrapment, and how to read it

Entrapment is an *external* check: unlike the decoys (which are also the null the
q-values are built on), a foreign-species marker set is an independent estimate of the
real false-positive population, so it can catch a decoy model that is silently wrong.
Bernhardt et al. (2016) established this methodology on high-resolution DIA — using a
foreign proteome (*E. coli*) as a ground-truth negative control, they showed that a
composition-preserving (scrambled/inverted) decoy matches the control and gives an
accurate, slightly conservative FDR, whereas an m/z-shifted decoy underestimates the
FDR while returning the *most* identifications — a mirage. Their lesson is the reason
this report exists: the number of identifications alone is not evidence a change was
good; only an independent false-signal control is.

Read the combined estimate **comparatively**, following Wen et al. (2025) and the
diagFDR reporting framework (Chion et al. 2026):

- `FDP_entrap(α) ≫ α` at the operating cutoff is strong evidence of **anti-conservative**
  error control — the reported q is optimistic.
- `FDP_entrap(α) ≈ α` is *consistent with* valid control but is **not proof** of it: an
  optimistic decoy and a pessimistic entrapment can coincidentally cancel. (In
  particular, if the entrapment shares whatever manipulation makes the decoys unusual —
  e.g. both shifted off the target m/z — the two agree while both mis-model real false
  targets. Keep the entrapment representative and independent of the decoy construction.)

## Recommendation and caveats

A **~10% foreign-species entrapment overlay**, read through the combined and
lower-bound estimators, is a low-cost, valid check on the accuracy of Osprey's
reported FDR: it preserves detections (near-native search) and avoids the doubling of
a full 1:1 library.

- **Power versus cost.** Smaller `r` means fewer entrapment hits and a noisier
  estimate; 10% is a pragmatic operating point, not a proven optimum. Read the value
  as an interval, not a third significant figure.
- **Marker choice.** The entrapment proteome must be genuinely absent from the sample
  (e.g. *Arabidopsis* for human/mammalian samples), sufficiently large to yield
  measurable hits at the operating cutoff, and phylogenetically distant enough to be
  homology-filtered cleanly against the targets (the three criteria of Chion et al.
  2026). It should also **model the sequences that cause real false identifications**:
  match the target precursor-m/z distribution so it samples the same difficulty regime,
  and — on high-resolution data, where MS1 precursor features carry weight — sit at
  physically plausible, occupied precursor m/z rather than being shifted off it, so its
  MS1 behaviour mirrors a true false target rather than an easily-rejected artifact.

## References

1. Elias JE, Gygi SP. Target-decoy search strategy for increased confidence in
   large-scale protein identifications by mass spectrometry. *Nat. Methods* 2007;
   4(3):207–214.
2. Fitzgibbon M, Li Q, McIntosh M. Modes of inference for evaluating the confidence
   of peptide identifications. *J. Proteome Res.* 2008; 7(1):35–39.
   doi:10.1021/pr7007303.
3. Käll L, Storey JD, MacCoss MJ, Noble WS. Assigning significance to peptides
   identified by tandem mass spectrometry using decoy databases. *J. Proteome Res.*
   2008; 7(1):29–34.
4. Wen B, Freestone J, Riffle M, MacCoss MJ, Noble WS, Keich U. Assessment of false
   discovery rate control in tandem mass spectrometry analysis using entrapment.
   *Nat. Methods* 2025; 22:1454–1463.
5. Bernhardt OM, Bruderer R, Gandhi T, Miladinović SM, Bober M, Ehrenberger T, Rinner O,
   Reiter L. General guidelines for validation of decoy models for HRM/DIA/SWATH as
   exemplified using Spectronaut. *Proteomics* 2016 (poster/methods) — foreign-organism
   (*E. coli*) negative-control validation of decoy models on high-resolution DIA.
6. Chion M, Godmer A, Douché T, Matondo M, Giai Gianetto Q. diagFDR: verifiable FDR
   reporting in proteomics via scope, calibration, and stability diagnostics. *bioRxiv*
   2026; doi:10.64898/2026.04.16.718468. (Formalizes the equal-chance assumption and the
   comparative interpretation of entrapment-based FDP.)
