# Retraining the learned peak-pick model

Osprey's optional learned peak pick (see [`06-peak-detection.md`](06-peak-detection.md))
replaces the default product-form CWT candidate rank with a frozen linear model
over four standardized terms. The weights are trained **offline** from feature
dumps produced by a normal search, then copied back into the source as
hard-coded constants. This note is the end-to-end recipe.

There is no single wrapper script yet — the "infrastructure" is one environment
variable (feature capture), one Python script (training), and a manual
copy-into-source step (promotion). The three stages:

```
[C# Osprey run]                     [Python]                 [C# + Rust source]
OSPREY_PICK_DUMP_CANDIDATES=1       pick_lda_train.py        StellarModel / AstralModel
   -> per-file .tsv          -->       -> pick-model-*.json  -->  STELLAR_MODEL / ASTRAL_MODEL
   pick_candidates.tsv              {features,weights,       (copied in verbatim; parity)
                                     means,scales}
```

Models are trained **per platform / resolution**: unit resolution (Stellar) and
HRAM (Astral) get separate models, so capture and train them independently.

## Stage 1 — Capture the per-candidate features

Setting `OSPREY_PICK_DUMP_CANDIDATES` to a non-empty / non-zero value makes the
first-pass search write one row **per CWT candidate peak of every precursor —
targets *and* paired decoys** — to a per-input-file TSV next to the work
directory: `<work-dir>\<inputStem>.pick_candidates.tsv`
(`Osprey.Core/OspreyEnvironment.cs`, `Osprey.Scoring/PickCandidateDump.cs`,
`Osprey.Scoring/ScoringContext.cs`).

```powershell
$env:OSPREY_PICK_DUMP_CANDIDATES = "1"
# Run first-pass per-file scoring on representative runs of ONE platform:
Osprey -i run1.mzML run2.mzML ... -l library.blib --work-dir C:\capture\stellar --resolution unit
```

- The dump captures the **exact raw rank terms** the picker computes, so the
  trainer learns on precisely the values inference will see. Row schema
  (`PickCandidateDump.HeaderLine`):

  | column | meaning |
  |--------|---------|
  | `base_id` | `Id & 0x7FFFFFFF` — precursor id shared by a target and its decoy |
  | `is_decoy` | `(Id & 0x80000000) != 0` |
  | `cand_index` | the rank-loop index of this candidate within the precursor |
  | `coelution` | mean pairwise Pearson of fragments over the peak window |
  | `ln_intensity` | `log(1 + apexIntensity)` |
  | `rt_penalty` | `exp(-rtResidual² / 2σ²)` |
  | `median_polish` | median-polish cosine vs. the library spectrum (1.0 on failure) |
  | `apex_rt`, `start_rt`, `end_rt` | candidate peak bounds |
  | `is_picked` | whether this candidate was the chosen (argmax) peak |

- **Default OFF, zero cost when unset.** With the flag off, no per-candidate
  median polish is computed and no file is written, so the hot loop is
  byte-identical to a normal run. The dump is a diagnostic capture only.
- **The files are large** — on Stellar the candidate set exceeds the CLR's
  contiguous-string limit, which is why `Flush` streams row-by-row rather than
  materializing one string. Expect multi-GB TSVs for a full run.
- Put all of a platform's `*.pick_candidates.tsv` files in one directory; the
  trainer globs the directory.

## Stage 2 — Train (Python)

```
pwiz_tools/Osprey/pick_lda_train.py <platform> <capture_dir> <out_json> [holdout]
```

```bash
python pick_lda_train.py stellar C:\capture\stellar  pick-model-stellar.json
python pick_lda_train.py astral  C:\capture\astral   pick-model-astral.json

# Optional 4th arg = holdout substring: files whose basename contains it are
# EXCLUDED from training (leave-one-run-out cross-validation):
python pick_lda_train.py stellar C:\capture\stellar  pick-model-stellar.json  run3
```

- **Dependencies:** `numpy` and `pyarrow`. (pyarrow parses the huge TSVs
  efficiently; plain `csv` would be far slower.)
- **The `<platform>` argument is just a label** written into the JSON's
  `platform` field — it does not change the math.
- **Algorithm** (mirrors the calibration LDA): seed the pick with coelution
  only, then iterate:
  1. score every candidate `rank = z·w` in standardized space and take the
     argmax candidate per precursor (best peak);
  2. run **paired target/decoy competition** — a target whose best-peak score
     beats its paired decoy's best-peak score is a **positive**; all decoy
     best-peaks are **negatives**;
  3. fit a **non-negative Fisher LDA** over the four z-normalized terms
     (within-class scatter `Sw` with a `1e-6` ridge; clamp weights to `≥ 0`
     since all terms are positive-sense), L2-normalize `w`;
  4. re-pick and repeat (≤ 10 iterations; stop when the positive count
     stabilizes or the LDA degenerates).

  `means`/`scales` are computed over **all** candidate rows and frozen into the
  model, so inference standardizes each term with the same statistics training
  used. The emitted JSON is exactly the schema the C# and Rust loaders consume:

  ```json
  { "features": ["coelution","ln_intensity","rt_penalty","median_polish"],
    "weights": [w0,w1,w2,w3], "means": [m0,m1,m2,m3], "scales": [s0,s1,s2,s3] }
  ```

- **The precursor key must stay collision-free.** The trainer groups candidate
  rows into precursors with a dense per-`(file, base_id)` index
  (`key = pair_index*2 + is_decoy`), so `key // 2` recovers the target/decoy
  pair regardless of how large `base_id` grows. (An earlier fixed multiplier
  collided once `base_id` exceeded ~5M — see the Copilot-review fix on
  pwiz #4446 / maccoss/osprey #57.)

## Stage 3 — Validate, then promote

**Validate without touching source.** Point the engine at the freshly trained
JSON and A/B it against the current pick on a held-out run — this overrides the
built-in model:

```powershell
$env:OSPREY_PICK_LDA_MODEL = "C:\...\pick-model-stellar.json"
Osprey -i heldout.mzML -l library.blib -o out.blib --resolution unit
```

The loader (`Osprey.Scoring/PickLdaModel.cs`, `LoadFromEnv`) requires the JSON's
`features` array to list `[coelution, ln_intensity, rt_penalty, median_polish]`
in that exact order — a re-ordered or older-schema file fails loudly rather than
silently applying weights to the wrong term.

**Promote into the defaults** by copying the JSON `weights` / `means` / `scales`
**verbatim** into the hard-coded models. Because of the C# ↔ Rust parity
requirement, this must land in **both** trees:

- C#: `Osprey.Scoring/PickLdaModel.cs` → `StellarModel` / `AstralModel`
- Rust: `crates/osprey/src/pick_lda.rs` → `STELLAR_MODEL` / `ASTRAL_MODEL`

(The "Values copied verbatim from pick-model-stellar.json / pick-model-astral.json"
comment in `PickLdaModel.cs` marks exactly where.)

### Selection precedence (what the picker actually uses)

The picker resolves the model once, in this order
(`Osprey.Core/OspreyEnvironment.cs`):

1. `OSPREY_PICK_LDA_MODEL` set to an existing file → that JSON model (the
   validation override);
2. else `OSPREY_PICK_LDA` is not `0` (**default**) → the hard-coded
   resolution-keyed model (Stellar for unit, Astral for HRAM);
3. else (`OSPREY_PICK_LDA=0`) → the legacy pure product-form pick, kept so the
   A/B stays available.

## Caveats

- **Promoting weights DOES change production behavior now.** The learned model
  became the default on 2026-08-03; `OSPREY_PICK_LDA=0` selects the legacy
  product pick. This section previously said flipping it on would need "a
  separate, coordinated golden re-baseline ... and cross-impl parity re-confirmed
  on Stellar + Astral" - that is exactly how it was done, so a change to the
  promoted weights now needs the same treatment rather than being a no-op.
- **The `pick-model-*.json` files are developer-local training artifacts** and
  are not committed; only the numeric literals copied into source live in the
  repo.
- **Capture representative data.** The model is only as good as the runs it was
  trained on; use enough representative files per platform to get stable
  positive counts (the trainer stops early and warns if positives are too few).
- **Keep the two trees in lock-step.** Any change to the feature set, the
  standardization, or the weights must be mirrored in both `PickLdaModel.cs` and
  `pick_lda.rs`, or the implementations diverge.

## Source map

| Piece | File |
|-------|------|
| Dump flag + model env vars | `Osprey.Core/OspreyEnvironment.cs` |
| Per-candidate dump writer | `Osprey.Scoring/PickCandidateDump.cs` |
| Dump collector hook | `Osprey.Scoring/ScoringContext.cs` |
| Trainer | `pick_lda_train.py` |
| C# model loader + defaults | `Osprey.Scoring/PickLdaModel.cs` |
| Rust model loader + defaults | `crates/osprey/src/pick_lda.rs` (maccoss/osprey) |
