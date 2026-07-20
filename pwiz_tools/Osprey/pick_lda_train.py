#!/usr/bin/env python3
"""Train a frozen linear peak-PICK model per platform, mirroring the calibration LDA.
Reads per-candidate dumps (targets + paired decoys), seeds the pick with coelution, runs
target-decoy competition to select confident target best-peaks (positives) + decoy best-peaks
(negatives), fits a non-negative Fisher LDA over the 4 z-normalized terms, re-picks, iterates.
Emits {features,weights,means,scales} JSON the C# frozen pick consumes: rank = sum w_i*z(x_i).

Usage: python pick_lda_train.py <platform> <capture_dir> <out_json>
"""
import sys, glob, json, os
import numpy as np

FEATURES = ["coelution", "ln_intensity", "rt_penalty", "median_polish"]
COLS = {c: i for i, c in enumerate(
    ["base_id","is_decoy","cand_index","coelution","ln_intensity","rt_penalty","median_polish",
     "apex_rt","start_rt","end_rt","is_picked"])}

def load(capture_dir, holdout=None):
    import pyarrow.csv as pacsv
    files = sorted(glob.glob(os.path.join(capture_dir, "*.pick_candidates.tsv")))
    if holdout:
        files = [f for f in files if holdout not in os.path.basename(f)]
        print(f"  HELD-OUT '{holdout}' excluded; training on {len(files)} files")
    assert files, f"no pick_candidates.tsv in {capture_dir}"
    need = ["base_id","is_decoy","coelution","ln_intensity","rt_penalty","median_polish"]
    parts = []
    for fi, f in enumerate(files):
        t = pacsv.read_csv(f, parse_options=pacsv.ParseOptions(delimiter="\t"),
                           convert_options=pacsv.ConvertOptions(include_columns=need))
        n = t.num_rows
        m = np.empty((n, 7), dtype=np.float64)
        m[:, 0] = fi
        for j, c in enumerate(need):
            m[:, j+1] = t.column(c).to_numpy(zero_copy_only=False)
        parts.append(m); print(f"    {os.path.basename(f)}: {n:,} rows")
    a = np.concatenate(parts, 0)
    print(f"  loaded {len(files)} files, {len(a):,} candidate rows")
    return a  # cols: file, base_id, is_decoy, coel, lnI, rtpen, mp

def qvalues(scores, is_decoy):
    """Standard target-decoy q-values over per-precursor best scores (desc)."""
    order = np.argsort(-scores)
    dec = is_decoy[order].astype(bool)
    nt = np.cumsum(~dec); nd = np.cumsum(dec)
    fdr = nd / np.maximum(nt, 1)
    q = np.minimum.accumulate(fdr[::-1])[::-1]   # monotone q
    out = np.empty_like(q); out[order] = q
    return out

def train(a, platform, out_json, max_iter=10):
    key = a[:, 0] * 10_000_000 + a[:, 1] * 2 + a[:, 2]   # (file,base_id,is_decoy) unique precursor id
    feats = a[:, 3:7]
    is_dec = a[:, 2]
    # normalization: mean/std over ALL candidate rows (frozen into the model)
    mean = feats.mean(0); scale = feats.std(0); scale[scale < 1e-9] = 1.0
    z = (feats - mean) / scale
    # group candidate rows by precursor
    uniq, inv = np.unique(key, return_inverse=True)
    prec_dec = np.zeros(len(uniq)); prec_dec[inv] = is_dec
    # paired target/decoy: a target and its paired decoy share (file,base_id) => key//2
    pair_id = (uniq // 2).astype(np.int64)
    upair, pinv = np.unique(pair_id, return_inverse=True)
    is_target = ~prec_dec.astype(bool)
    print(f"  {len(uniq):,} precursors, {len(upair):,} target/decoy pairs, {int(is_target.sum()):,} targets")
    # seed weights: coelution only (in z-space)
    w = np.array([1.0, 0.0, 0.0, 0.0])
    prev_pos = -1
    for it in range(max_iter):
        cand_score = z @ w
        # best candidate per precursor (vectorized: sort by (group, score); last per group = argmax)
        order = np.lexsort((cand_score, inv))
        best_row = np.empty(len(uniq), dtype=int)
        best_row[inv[order]] = order          # last write within a group wins = highest score
        pscore = cand_score[best_row]
        # paired competition: per pair, target best-peak score vs decoy best-peak score
        t_score = np.full(len(upair), -np.inf); np.maximum.at(t_score, pinv[is_target], pscore[is_target])
        d_score = np.full(len(upair), -np.inf); np.maximum.at(d_score, pinv[~is_target], pscore[~is_target])
        win = t_score > d_score               # target beat its paired decoy
        pos = is_target & win[pinv]           # positives = winning target best-peaks
        neg = ~is_target                      # negatives = all decoy best-peaks
        npos = int(pos.sum()); nneg = int(neg.sum())
        if npos < 50:
            print(f"    iter{it}: only {npos} positives, stopping"); break
        Xp = z[best_row[pos]]; Xn = z[best_row[neg]]
        mp_, mn_ = Xp.mean(0), Xn.mean(0)
        Sw = np.cov(Xp, rowvar=False) * (len(Xp)-1) + np.cov(Xn, rowvar=False) * (len(Xn)-1)
        Sw /= (len(Xp) + len(Xn) - 2)
        Sw += np.eye(4) * 1e-6
        wnew = np.linalg.solve(Sw, mp_ - mn_)
        wnew = np.maximum(wnew, 0.0)          # non-negative (all terms positive-sense)
        n = np.linalg.norm(wnew)
        if n < 1e-9:
            print(f"    iter{it}: degenerate LDA, keeping previous w"); break
        wnew /= n
        w = wnew
        print(f"    iter{it}: pos={npos} neg={nneg}  w(z)=[{', '.join(f'{x:.3f}' for x in w)}]")
        if abs(npos - prev_pos) <= max(5, 0.001*npos):
            prev_pos = npos; break
        prev_pos = npos
    model = {"platform": platform, "features": FEATURES,
             "weights": [float(x) for x in w],
             "means": [float(x) for x in mean], "scales": [float(x) for x in scale],
             "n_positives": int(npos), "n_candidate_rows": int(len(a))}
    with open(out_json, "w") as fh: json.dump(model, fh, indent=2)
    # report separation on the final picks
    print(f"  FINAL {platform}: w(z)={model['weights']}  positives={npos}")
    print(f"    feature weight shares: " + ", ".join(f"{FEATURES[i]}={w[i]/w.sum()*100:.0f}%" for i in range(4)))
    print(f"  wrote {out_json}")
    return model

if __name__ == "__main__":
    platform, capture_dir, out_json = sys.argv[1], sys.argv[2], sys.argv[3]
    holdout = sys.argv[4] if len(sys.argv) > 4 else None
    print(f"=== training pick-LDA for {platform} (holdout={holdout}) ===")
    a = load(capture_dir, holdout)
    train(a, platform, out_json)
