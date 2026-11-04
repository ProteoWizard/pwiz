# Equal-chance alarms in `--model-diagnostics`

Target-decoy FDR rests on the **equal-chance assumption**: an incorrect match is as likely to
land on a decoy as on a false target. When it fails, q-values are confidently wrong and
nothing in the search output says so. The `--model-diagnostics` report carries four automated
checks of it (issue #4464), shown on the Summary tab as the **Equal-chance alarms** card and
keyed in the page JSON as `equalChance.pass1` / `equalChance.pass2`.

They are **diagnostic only**. They are computed when the page is rendered, from views the
report data already holds (`scores`, `winFraction`, `perFile`), by
`Osprey.Tasks/ModelDiagnostics/EqualChanceAlarms.cs`. No FDR stage reads them, no console
warning is printed, and the `.model-diagnostics.json` sidecars do not change; a page
re-rendered with `--task ModelDiagnostics` recomputes them from those sidecars. Every
threshold is a constant in `EqualChanceThresholds`, and the card prints it beside the value.

Each check reports one of `ok`, `alarm`, `cannot_assess` (the input exists but cannot
support the check; the reason is given) or `not_applicable` (the input is absent for this
pass). Pass 2 has no retrained model since #4528, so its null-alignment and q-band checks
are `not_applicable` and the card points at pass 1.

## A + B. Null alignment: decoys vs a decoy-independent `f_false`

**`f_false`.** A two-component Gaussian mixture is fitted by EM to the **target** composite
score histogram (real targets plus entrapment: every non-decoy, the population a q counts as
targets), bin centers weighted by counts:
`f_target = pi_false * N(mu_false, s_false) + (1 - pi_false) * N(mu_true, s_true)`.
The lower-mean component is `f_false`. The decoys play no part in the fit, so comparing them
with it is not circular. Four deterministic starts (target quantile pairs 0.25/0.75,
0.10/0.90, 0.50/0.95, 0.05/0.50, each component at half the overall SD); the converged fit
with the highest log-likelihood wins; variances are floored at the bin-width variance.

**Guard.** The fit is accepted as a null only when all hold, else `cannot_assess` with the
first reason that fails, in this order (a pass with no decoys is `not_applicable` first):

| Guard | Condition | Constant |
|---|---|---|
| `too_few_targets` | targets in the histogram >= 1000 | `MIXTURE_MIN_TARGETS` |
| `degenerate` | the targets fill at least two bins | |
| `component_weight` | `min(pi_false, 1 - pi_false) >= 0.02` | `MIXTURE_MIN_COMPONENT_WEIGHT` |
| `not_separated` | Ashman's `D = sqrt(2) abs(mu_true - mu_false) / sqrt(s_false^2 + s_true^2) >= 2` | `MIXTURE_MIN_SEPARATION` |
| `not_converged` | EM converged (relative log-likelihood change <= 1e-10 within 1000 iterations) | |

**Metrics.** Decoy moments are taken over the decoy histogram (decoys plus entrapment decoys)
on the same bins.

| Key | Definition |
|---|---|
| `locationShift` | `(mean_decoy - mu_false) / s_false`. Negative: decoys score below the false targets, so the decoy count undercounts them and q is anti-conservative |
| `scaleRatio` | `sd_decoy / s_false` |
| `symmetricDivergence` | binned Jeffreys divergence `sum (p - q) ln(p / q)`, `p` the area-normalized decoy histogram, `q` the bin masses of `f_false` renormalized to the binned range, both floored at 1e-6. Reported, not alarmed: a Gaussian misfits skewed decoys |
| `entrapmentLocationShift` | entrapment-target mean vs `f_false`, in `s_false`. Entrapment is a pure null, so this validates the fit. NaN without entrapment |
| `decoyVsEntrapmentShift` | `(mean_decoy - mean_entrapment) / s_false`. NaN without entrapment |

**Alarm**: `abs(locationShift) > 0.5` (`LOCATION_SHIFT_ALARM`) or
`abs(ln scaleRatio) > ln 1.5` (`SCALE_RATIO_ALARM`).

## B. q-band balance (diagFDR)

diagFDR's `dfdr_equal_chance_qbands`, on q computed from the composite histogram: at each
bin's lower edge `FDR = D(>=) / T(>=)` (targets and decoys as above), and q is the running
minimum of FDR from the bottom up. So q has bin resolution (80 bins), which is ample for the
wide low-confidence band this check reads.

| Key | Definition |
|---|---|
| `breaks`, `bandN`, `bandDecoys`, `bandDecoyFraction` | per band `(lo, hi]` of `QBAND_BREAKS` = 0, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5 (diagFDR defaults): entries (targets + decoys) whose bin q falls in it, its decoys, and their ratio |
| `pooledN`, `pooledDecoyFraction` | over bins with q in [0.2, 0.5] (diagFDR `low_conf`) |
| `deltaBalance` | `pooledDecoyFraction - 0.5` |

**Alarm**: `abs(deltaBalance) > 0.15` (`QBAND_DELTA_BALANCE_ALARM`, the issue's number).
`cannot_assess` below 2000 pooled entries (diagFDR `min_N`). This check catches decoys that
are too STRONG (a decoy-heavy band) far better than decoys too weak: synthetic decoys one
sigma below the null read 0.52 here while the null alignment reads -1 sigma, which is why
both are kept.

## C. Paired coin

The entrapment-pair decoy-win fraction in the win-fraction card's null band
(`winFraction.nullBandEnt`, the lowest 35% of the winner-score range). A fair null wins half
the time; a decoy generator that is systematically weaker than real false targets loses the
coin (gendecoy read 22% while it was miscalibrated).

**Alarm**: `abs(entrapmentCoin - 0.5) > 0.10` (`COIN_DEVIATION_ALARM`). `cannot_assess` below
100 entrapment pairs in the band (`COIN_MIN_PAIRS`; counted from the bins whose centers fall
in it). `not_applicable` without entrapment: the real-pair coin (`realCoin`) is shown for
context but never alarmed, because true positives win real pairs by design.

## D. Decoy support at the operating cutoff

The decoys each run accepts at the run FDR (`perFile[].decoys`, diagFDR's `D_alpha`), and
for the thinnest run the FDP coefficient-of-variation benchmark `sqrt(2 / D_alpha)` (diagFDR's
approximation under equal chance and independence). Few accepted decoys make the FDR estimate
granular: each decoy moves it by `1 / T_alpha`, and the cutoff is fragile.

**Alarm** ("granular regime"): the thinnest run accepts fewer than 50 decoys
(`GRANULAR_MIN_DECOYS`; the CV benchmark passes 20% there). diagFDR's boundary window
(decoys in `(alpha, 1.2 alpha]`) needs per-entry q, which the report's reduced views do not
carry, so it is not computed.

## Calibration on the regression datasets

Read on the regression datasets (pass 1; Stellar's leg does not carry `--model-diagnostics`, so
its report was rendered afterwards with `--task ModelDiagnostics`):

| Dataset | Null alignment | q-band | Paired coin | Decoy support |
|---|---|---|---|---|
| Stellar | ok: location -0.30, scale 0.93, D 3.00 | ok: 46.3% | n/a (no entrapment) | ok: 261 |
| StellarLibDecoy | ok: location -0.10, scale 0.95, D 3.24 | ok: 46.2% | ok: 50.1% | ok: 230 |
| StellarGenDecoyEntrap | ok: location -0.18, scale 0.98, D 2.13 | ok: 43.9% | ok: 46.7% | ok: 239 |
| Astral (from that run's retained diagnostics sidecars) | cannot assess: not separated, D 0.32 | ok: 43.5% | n/a | ok: 994 |

The generated decoys now track the null closely (entrapment FDP about 1.2% at 1% q in pass 1),
so nothing fires on StellarGenDecoyEntrap; it does read further from the null than the library
decoys on every check (-0.11 sigma below its own entrapment, against -0.01 for libdecoy).
Stellar, generated decoys against the plain library, reads furthest from its null (-0.30
sigma) and has no entrapment to confirm it; it is the dataset to watch if a decoy change moves
this metric. The
numbers the issue quotes (22% coin, ~10x miscalibration) predate the decoy-generator fixes.
