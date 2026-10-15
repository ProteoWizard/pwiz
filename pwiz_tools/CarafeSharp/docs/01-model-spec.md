# AlphaPeptDeep models as Carafe runs them

This is the specification CarafeSharp's TorchSharp port (`CarafeSharp.Models`) implements. It
is Carafe's **v2** Python path (`maccoss/carafe` `src/main/resources/py/v2/{models.py, ai.py,
ai_pred.py}`), which vendors peptdeep's model code and does not import the `peptdeep` package.
Pinned Python stack: torch 2.5.1, alphabase 1.2.1 (`MannLabs/alphabase@63322f8a`),
transformers 4.47.0, peptdeep fork `wenbostar/alphapeptdeep_dia@f549bb70` (1.1.0). All
Apache-2.0.

CarafeSharp runs the same networks on libtorch 2.10.0 through TorchSharp 0.106.0. For RT it uses
Chronologer by default instead (07-chronologer.md); AlphaPeptDeep's RT model below is `-rt_model
alphapeptdeep`.

## Pretrained weights

- Archive: `https://github.com/MannLabs/alphapeptdeep/releases/download/pre-trained-models/pretrained_models.zip`,
  25,614,761 bytes, SHA-256 `75e6037db3280a513d0f6010a21dba4e8ea47a8d67127f38c77fb1f9a7d408eb`.
  The URL is unversioned (the release later gained `_v2`/`_v3` zips); CarafeSharp refuses any
  other archive unless told otherwise (`PretrainedModels.PINNED_SHA256`).
- Committed with CarafeSharp at `models/alphapeptdeep-v1/pretrained_models.zip` and copied beside the
  executable. `PretrainedModels.DefaultPath` takes `CARAFESHARP_PRETRAINED_MODELS` when it is set (a
  missing file fails), then that bundled copy, then peptdeep's and Carafe's shared
  `~/peptdeep/pretrained_models/pretrained_models.zip`. See `models/alphapeptdeep-v1/README.md`.
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

## RT model: `Model_RT_LSTM_CNN` (`-rt_model alphapeptdeep`)

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

## RT model: Chronologer (the default; `-rt_model chronologer`)

CarafeSharp's default RT model is Chronologer (Searle lab, Apache-2.0), not Carafe's `Model_RT_LSTM_CNN`
(`-rt_model alphapeptdeep`): AlphaPeptDeep's generic RT model plateaus at the end of the gradient, Chronologer
does not (#4759). 07-chronologer.md documents it: its source and pins, what it predicts (a hydrophobic index, the
% acetonitrile at which a peptide elutes) and on what scale, the network and its encoding, library prediction,
how CarafeSharp fine-tunes it, how Chronologer's own training handles data from many sources, and the results.

In short:
- State dict (120,321 values): `seq_embed.weight` [55,64]; three `resnet_blocks.{0,1,2}` (dilation 1, 2, 3), each
  `process_blocks.{0,1}.0.{0,1}` (a 1x1, then a kernel-7 Conv1d(64, 64, same padding), each with BatchNorm1d and
  ReLU) and a `shortcut.{0,1}` (Conv1d + BatchNorm1d) that forward never uses; `output` Linear(52 x 64, 1).
- Input: 52 tokens, an N-terminal token, one per residue (modified residues have their own), `_`, padding 0.
  Forms of 5 or fewer or more than 50 residues, with a modification without a token, or with a C-terminal
  modification are rejected; library prediction predicts them with AlphaPeptDeep's pretrained RT model.
- Output `[B]`: pretrained, and fine-tuned on runs of different gradients, the hydrophobic index, which a library
  maps to minutes by the training runs' maps, or without them to iRT through the 11 iRT kit peptides above (`rt_max`
  does not apply); fine-tuned on runs of one gradient, the normalized RT (`rt / rt_max`), clipped at 0, whose library
  RT is `rt_pred * rt_max`.
- Fine-tuning folds the least-squares line from HI to the training peptides' normalized RT into `output` (on runs of
  different gradients for training only, folded back out before saving), then
  trains every weight with the RT fine-tune's schedule and L1 loss, the BatchNorm running statistics frozen and
  batches mixing peptide lengths.

## CCS model: `Model_CCS_LSTM` (ion mobility, `-ccs`)

State dict (713,452 parameters): the RT model's layout under `ccs_encoder.*` with one more input
channel, Conv1d(36, 36, k=3/5/7) and LSTM(144, 128, 2 layers, bidirectional); then
`ccs_decoder.nn.{0,1,2}.*`, Linear(257, 64), PReLU, Linear(64, 1).

The encoder (`Encoder_26AA_Mod_Charge_CNN_LSTM_AttnSum`) concatenates `one_hot(aa, 27)`, the
8-wide mod embedding and the scaled charge (`charge * 0.1`, float32, repeated at every position).
The decoder reads the attention sum with the scaled charge appended. Output `[B]` is the
collisional cross section in square angstroms, clipped at 0 (Carafe's `ModelInterface` clips every
prediction at `min_pred_value` 0, as for RT).

1/K0 (timsTOF reduced ion mobility, V s/cm^2), from alphabase's `ccs_to_mobility_bruker`:
`M = precursor_mz * z`, `mu = M * 28 / (M + 28)`, `1/K0 = ccs * sqrt(mu) / z / 1059.62245`. The
precursor m/z is alphabase's (residues, plus water, plus the mods, over z, plus a proton), not
the compomics m/z the library writes.

In the library (`-ccs`):
- The model, as Carafe's `ai_pred.py` picks it: with `-tf all` (the default) the model folder's
  Carafe `ccs_model.pt` (from a timsTOF training run) when there is one, else `generic/ccs.pth`;
  any other `-tf` takes `generic/ccs.pth`. Carafe predicts no CCS for `-tf rt` or `ms2` and then
  fails, so CarafeSharp refuses `-ccs` with them. Carafe takes `generic/ccs.pth` in the phospho and
  ubiquitin modes too.
- With training, as Carafe does on Osprey's results, which carry no ion mobility: the CCS model
  is not fine-tuned, and the library after training predicts 1/K0 as above (with a warning).
- TSV: Carafe's `IonMobility` column after `Tr_recalibrated`, `%.4f`.
- .blib: `ionMobility` with `ionMobilityType` 2 (`inverseK0(Vsec/cm^2)`) in both `RefSpectra` and
  `RetentionTimes`, and `collisionalCrossSectionSqA` NULL, as Carafe writes them. Given a CCS,
  Skyline would convert it with the data file's own calibration and use that instead of the
  library's 1/K0 (`Library.GetLibraryMeasuredIonMobilityAndCCS`); without one it uses the 1/K0
  and derives the CCS itself where the data file allows.

Parity:
- On 23 precursors (charges 1 to 4, lengths 7 to 30, N-term, C-term and residue mods), CCS and
  1/K0 are identical to Carafe 2.2.0's Python on this machine's CPU (`TestCcsPrediction`).
- End to end against the Carafe 2.2.0 jar on 30 HeLa proteins (1,268 precursors, CPU), the TSV
  `IonMobility` column is identical on every row.
- In the .blib, 1/K0 agrees to 2e-16 wherever the float32 CCS is the same (753 of 777
  unmodified precursors). The rest differ by 1 or 2 float32 ulps, which batch composition moves,
  at most 1.5e-7 in 1/K0.

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
- **CarafeSharp's acquisition layer (not peptdeep's).** How a precursor was activated and which
  analyzer read the spectrum out are not peptdeep instrument families, and the 8 instrument slots are
  fixed by the pretrained weights (5 trained, slot 7 unknown). So the MS2 model gains a second layer
  beside peptdeep's `meta_nn.nn`: `meta_nn.acquisition_nn`, a linear map with no bias from one input
  column per activation (beam-CID, reCID) and per analyzer (Orbitrap, LIT, ToF) to the same 7 outputs,
  added to peptdeep's. Its weights start at zero in any model that has not trained it (the pretrained
  one, a Carafe checkpoint), which adds exact zeros, so predictions are peptdeep's bit for bit; fine-
  tuning learns each value's effect from the spectra that carry it, as an adjustment to the instrument
  family's prediction. Runs of one activation and one analyzer train the two columns alike (every
  spectrum carries both, so they get the same updates and learn the same shift); only training data
  that differs in one of them tells them apart. The number of values is not fixed by the model: a
  model records its columns by name in its safetensors metadata (`carafesharp.activations`,
  `carafesharp.analyzers`), a value is found by name, one the model lacks predicts with its columns
  zero, and loading a model whose list lacks a known value gives it a zero column, each stored column
  kept under its name. A Stellar and TribridOT name peptdeep's Lumos family.
- **Activation and analyzer of a run**, from Osprey's export footer (`osprey.dissociation_methods`,
  `osprey.ms2_mass_analyzers`), named to avoid vendors' terms:
  - beam-CID: beam-type CID (PSI-MS's HCD) from any vendor, and plain CID from any vendor but Thermo,
    since Sciex's and Bruker's CID is beam-type;
  - reCID: trap-type CID, and plain CID from Thermo, whose CID is resonance CID in an ion trap;
  - none for an electron-based method (ETD, EThcD, EAD), whose columns stay zero;
  - ToF for a time-of-flight analyzer (an Astral's MS2, a timsTOF, a Sciex TOF), LIT for an ion trap
    (a Stellar, a Tribrid's), Orbitrap for an Orbitrap; without the analyzers (an export from before
    #4757 added them, or no data file), a Stellar is LIT and an Astral ToF.
  A run whose MS2 spectra mix activations or analyzers is refused unless `-activation` or `-analyzer`
  names one for all of it. A library predicts for `-activation` and `-analyzer`, else the training
  run's.
- **NCE of a run** (CarafeSharp's; Carafe trains every run at its file's energy). The NCE input is
  Thermo's normalized collision energy. pwiz reports every vendor's energy as PSI-MS's collision
  energy in eV, but a Thermo file's value is its scan filter's NCE, so:
  - a Thermo run trains at its own NCE (the most common over its sampled MS2 spectra), else `-nce`,
    else 27, as Carafe's does;
  - a run from any other vendor, whose energy is in eV, trains at `-nce`, else at the NCE where the
    start MS2 model predicts its training spectra best: the highest median PCC over NCE 20 to 40 in
    steps of 1, on at most 1,000 of its spectra, as AlphaPeptDeep calibrated the NCE of its SCIEX
    TripleTOF fine-tune. A rolling collision energy needs nothing more, since the run's spectra
    decide. A calibration at 20 or 40 is warned about;
  - a run that names neither vendor nor model counts as Thermo.
  A library from the run's models predicts at the NCE it trained with (meta.json, and a saved
  model's `prediction_defaults`), and the saved model records where it came from.

## Prediction post-processing

- Group by nAA ascending, batch 512 (MS2) or 1024 (RT and CCS), eval mode, no gradient.
- MS2: per precursor, divide by the maximum over all 8 columns (1 if that maximum is not
  positive), then set values below 1e-4 (negatives included) to 0.
- Fragment m/z (alphabase): residue masses from formulas with most-abundant-isotope element
  masses, proton 1.007276467; mods at sites 0 and 1 both add to residue 0, site -1 to the
  last; `b = cumsum[:-1]`, `y = M + H2O - b`, `mz = mass/z + proton`; z2 columns zero where
  the precursor charge is below 2.

## Fine-tuning (`ai.py` v2)

- Order for `--tf_type all`: RT, CCS (if at least 100 rows; not in CarafeSharp, which does not
  fine-tune CCS), MS2. Java passes `--seed 2024`
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
- Batches: shuffle, group by nAA, visit groups in random order, slice. (Chronologer's fine-tune
  puts every length in one group; 07-chronologer.md.)
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
