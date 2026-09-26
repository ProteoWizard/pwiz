# AlphaPeptDeep models as Carafe runs them

This is the specification CarafeSharp's TorchSharp port (`CarafeSharp.Models`) implements. It
is Carafe's **v2** Python path (`maccoss/carafe` `src/main/resources/py/v2/{models.py, ai.py,
ai_pred.py}`), which vendors peptdeep's model code and does not import the `peptdeep` package.
Pinned Python stack: torch 2.5.1, alphabase 1.2.1 (`MannLabs/alphabase@63322f8a`),
transformers 4.47.0, peptdeep fork `wenbostar/alphapeptdeep_dia@f549bb70` (1.1.0). All
Apache-2.0.

CarafeSharp runs the same networks on libtorch 2.10.0 through TorchSharp 0.106.0.

## Pretrained weights

- Archive: `https://github.com/MannLabs/alphapeptdeep/releases/download/pre-trained-models/pretrained_models.zip`,
  25,614,761 bytes, SHA-256 `75e6037db3280a513d0f6010a21dba4e8ea47a8d67127f38c77fb1f9a7d408eb`.
  The URL is unversioned (the release later gained `_v2`/`_v3` zips); CarafeSharp refuses any
  other archive unless told otherwise (`PretrainedModels.PINNED_SHA256`).
- Default location, shared with peptdeep and Carafe: `~/peptdeep/pretrained_models/pretrained_models.zip`.
- Members: `generic/ms2.pth`, `generic/rt.pth`, `generic/ccs.pth`, plus `phospho/rt_phos.pth`
  and `digly/rt_digly.pth` for the phospho and ubiquitin modes.
- Each member is `torch.save(model.state_dict())` in the zip format. peptdeep loads with
  `strict=False`; CarafeSharp requires an exact key and shape match (`StateDict.Load`).
- Keys can carry `module.` (saved from `nn.DataParallel`) or `_orig_mod.` (saved after
  `torch.compile`) prefixes. Both are stripped on load.
- The RT checkpoint's LSTM weights share one storage at non-zero offsets, which
  TorchSharp.PyBridge cannot read; `PthReader` reads whole storages and rebuilds strided views.

## MS2 model: `ModelMS2Bert`

Built as `ModelMS2Bert(num_frag_types=8, num_modloss_types=4, mask_modloss, dropout=0.1,
nlayers=4, hidden=256)`. `mask_modloss` is true in general and ubiquitin modes.

State dict (95 tensors, 3,988,974 parameters):

| Key | Shape | Notes |
|---|---|---|
| `input_nn.mod_nn.nn.weight` | [2, 103] | Linear(103 to 2), no bias |
| `input_nn.aa_emb.weight` | [27, 240] | Embedding(27, 240, padding_idx=0) |
| `input_nn.pos_encoder.pe` | [1, 200, 248] | persistent buffer |
| `meta_nn.nn.weight` / `.bias` | [7, 9] / [7] | |
| `hidden_nn.bert.layer.{0..3}.attention.self.{query,key,value}.*` | [256,256] / [256] | |
| `hidden_nn.bert.layer.{i}.attention.output.{dense,LayerNorm}.*` | | |
| `hidden_nn.bert.layer.{i}.intermediate.dense.*` | [1024,256] / [1024] | |
| `hidden_nn.bert.layer.{i}.output.{dense,LayerNorm}.*` | [256,1024] / [256] | |
| `output_nn.nn.{0,1,2}.*` | [64,256], [64], [1], [4,64], [4] | Linear, PReLU, Linear |
| `modloss_nn.0.bert.layer.0.*`, `modloss_nn.1.nn.*` | | always built, unused when masked |

BERT configuration (`_Pseudo_Bert_Config`): hidden 256, 8 heads of 32, intermediate 1024,
exact erf GELU, **LayerNorm eps 1e-8**, dropout 0.1, `_attn_implementation = "eager"`,
absolute position type (no distance embedding). **No attention mask**: every position,
including both terminal pad tokens, attends to every other, so inference is grouped by exact
peptide length.

```python
def forward(self, aa_indices, mod_x, charges, NCEs, instrument_indices):
    in_x = self.dropout(self.input_nn(aa_indices, mod_x))
    meta_x = self.meta_nn(charges, NCEs, instrument_indices).unsqueeze(1).repeat(1, in_x.size(1), 1)
    in_x = torch.cat((in_x, meta_x), 2)
    hidden_x = self.hidden_nn(in_x)
    hidden_x = self.dropout(hidden_x[0] + in_x * 0.2)
    out_x = self.output_nn(hidden_x)
    if self._mask_modloss:
        out_x = torch.cat((out_x, torch.zeros(*out_x.size()[:2], 4, device=in_x.device)), 2)
    else:
        modloss_x = self.modloss_nn[0](in_x)[0] + hidden_x
        out_x = torch.cat((out_x, self.modloss_nn[-1](modloss_x)), 2)
    return out_x[:, 3:, :]
```

Layout of the 256-wide hidden vector: [0:240] residue embedding, [240:246] raw C/H/N/O/P/S
counts, [246:248] the 103-to-2 projection, then positional encoding over those 248; [248:255]
meta Linear(one-hot instrument(8) + NCE), [255] charge. Output `[B, nAA-1, 8]`, raw with no
activation: row r is b(r+1) and y(nAA-1-r); columns b_z1, b_z2, y_z1, y_z2, then the four
modloss columns.

Positional encoding: `pe[p, 2i] = sin(p * 200^(-2i/d))`, `pe[p, 2i+1] = cos(...)` with base
**max_len = 200**, stored in the checkpoint. Peptides are limited to 198 residues.

## RT model: `Model_RT_LSTM_CNN`

State dict (708,224 parameters): `rt_encoder.mod_nn.nn.weight` [2,103];
`rt_encoder.input_cnn.cnn_{short,medium,long}.*` Conv1d(35, 35, k=3/5/7, same padding);
`rt_encoder.hidden_nn.rnn_h0`/`rnn_c0` [4,1,128] (frozen parameters, **not zero** in the
checkpoint); `rt_encoder.hidden_nn.rnn.*` LSTM(140, 128, 2 layers, bidirectional,
batch_first); `rt_encoder.attn_sum.attn.0.weight` [1,256]; `rt_decoder.nn.{0,1,2}.*`.

Encoder input is `one_hot(aa, 27)` (padding index 0 one-hots to `[1,0,...]`) concatenated with
the 8-wide mod embedding, then SeqCNN (input plus three convolutions, 35 to 140 channels),
the LSTM, and a softmax attention sum over positions. Output `[B]` is the normalized RT
(`rt / rt_max`), clipped at 0.

iRT: predict the 11 Biognosys peptides (LGGNEQVTR -24.92 ... LFLQFGAQGSPFLK 100.00), fit
`irt = slope * rt_pred + intercept` by least squares. Carafe's library RT is `rt_pred * rt_max`
when the training `rt_max` is known, else `irt_pred`.

## Featurization

- Residue indices: `ord(aa) - 64`, so A=1 .. Z=26, padded with 0 at both ends: `[B, nAA+2]`.
- Modification features `[B, nAA+2, 109]`. Element order is `PeptdeepConstants.MOD_ELEMENTS`
  (C, H, N, O, P, S, then 97 more elements, then 2H, 13C, 15N, 18O, `?`). A modification's
  vector comes from its alphabase composition string: each known element's count is
  **assigned**, unknown elements accumulate into `?`, and unknown or composition-less
  modifications are all zeros. Site 0 (N-term) goes to position 0, residue k to position k,
  site -1 (C-term) to position nAA+1. Features add in float64 per site, then cast to float32.
- Charges: float32 tensor times 0.1; NCE: float32 tensor times 0.01 (both in libtorch).
- Instrument: name upper-cased and mapped to a family (Astral, Lumos, Fusion, Eclipse, Velos,
  Elite, OrbitrapTribrid, ThermoTribrid to Lumos; QE, QE+, QEHF, QEHFX, Exploris,
  Exploris480 to QE; timsTOF, SciexTOF, ThermoTOF to themselves; **anything else to Lumos**),
  then indexed QE=0, Lumos=1, timsTOF=2, SciexTOF=3, ThermoTOF=4.

## Prediction post-processing

- Group by nAA ascending, batch 512 (MS2) or 1024 (RT), eval mode, no gradient.
- MS2: per precursor, divide by the maximum over all 8 columns (1 if that maximum is not
  positive), then set values below 1e-4 (negatives included) to 0.
- Fragment m/z (alphabase): residue masses from formulas with most-abundant-isotope element
  masses, proton 1.007276467; mods at sites 0 and 1 both add to residue 0, site -1 to the
  last; `b = cumsum[:-1]`, `y = M + H2O - b`, `mz = mass/z + proton`; z2 columns zero where
  the precursor charge is below 2.

## Fine-tuning (`ai.py` v2)

- Order for `--tf_type all`: RT, CCS (if at least 100 rows), MS2. Java passes `--seed 2024`
  and never passes `--use_best_model`/`--early_stop`, so the **last epoch** is kept.
- Split: `n_test = max(1, min(1000, ceil(0.1 N) - 10))`; training rows are
  `psm_sampling_with_important_mods(N - n_test, top 10 mods, +50 per mod, seed 1337)`; test
  rows are sequences not in training. MS2 targets are normalized per PSM to the maximum over
  all 8 columns; RT uses the median `rt_norm` per (sequence, mods, sites).
- Optimizer: **Adam** (not AdamW), lr 1e-4, default betas and eps, no weight decay, created
  once. Nothing is frozen except `rnn_h0`/`rnn_c0`.
- Schedule: LambdaLR **stepped once per epoch**, with Carafe's lambda (epoch 0 trains at lr 0):

```python
if step < warmup: return float(step) / float(max(1, warmup))
progress = float(step - warmup) / float(max(1, total - warmup))
return max(0.0, 0.5 * (1.0 + math.cos(math.pi * 0.5 * 2.0 * progress)))
```

- MS2: 20 epochs, warmup 10, batch 512; RT: 40 epochs, warmup 10, batch 1024. If
  `ceil(n_train / bs) < 40`, `bs = max(32, 2^floor(log2(max(32, ceil(n_train / 40)))))`.
- Batches: shuffle, group by nAA, visit groups in random order, slice.
- MS2 loss: `mask = (valid <= 0)`, `L1(mask * pred, mask * target, sum) / sum(mask)`. In
  general mode the four modloss columns have mask 1 and target 0, so they add nothing to the
  numerator but **count in the denominator**. RT loss: mean L1 on `rt_norm`.
- `clip_grad_norm_(parameters, 1.0)` after every backward.
- Metrics on the test set per PSM over the flattened `(nAA-1)*8` vector with the mask: masked
  Pearson, cosine, spectral angle `1 - 2*acos(cos)/pi`, masked Spearman on ranks. The
  fine-tuned MS2 model is used for prediction only if **all four medians** beat the
  pretrained model's (strict). The fine-tuned RT model is always used.

## Parity notes

- Same weights, same inputs: inference matches Python to float precision; libtorch 2.10
  against Carafe's torch 2.5.1 changes oneDNN/MKL kernels at about 1e-6 relative.
- Training is not bit-reproducible across implementations (dropout RNG streams, pandas
  sampling). Parity for fine-tuning is statistical: held-out metrics and Osprey ID counts.
- TorchSharp specifics: `use_deterministic_algorithms` is unimplemented; register every
  submodule under its Python name (`[ComponentName]` plus `RegisterComponents()`), because
  TorchSharp also auto-registers fields by field name; cast `one_hot` (int64) to float32
  before concatenating.
