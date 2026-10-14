# 07. Chronologer, CarafeSharp's RT model

Chronologer is the retention-time model CarafeSharp predicts and fine-tunes with by default. This document
covers what it predicts and on what scale, how CarafeSharp runs it, how CarafeSharp fine-tunes it, and what
training on data from many experiments would take. 01-model-spec.md has the AlphaPeptDeep models it replaces for
RT; 06-saved-models.md has the saved-model format.

| | |
|---|---|
| Code | `CarafeSharp.Models`: `ChronologerModel`, `Modules/ModelChronologer`, `ChronologerEncoding`, `ChronologerRtPredictor`, `ChronologerFiles`, `ChronologerTrainingExample`; `ModelFineTuner.TrainChronologer`; `FineTuneRun.TrainChronologer` |
| Files | `models/chronologer-20220601193755` (weights, encoding JSON, license, README) |
| Option | `-rt_model chronologer` (the default, `LibrarySettings.DEFAULT_RT_MODEL`) or `-rt_model alphapeptdeep` (Carafe's) |
| Why the default | AlphaPeptDeep's generic RT model plateaus at the end of the gradient; Chronologer does not (#4759, "Results" below) |

## Where it comes from

Chronologer is the Searle lab's RT predictor: D. B. Wilburn, A. E. Shannon, V. Spicer, A. L. Richards, D. Yeung,
D. L. Swaney, O. V. Krokhin and B. C. Searle, "Deep learning from harmonized peptide libraries enables retention
time prediction of diverse post translational modifications", bioRxiv 2023.05.30.542978 (2023). The published
model was trained on a harmonized database of more than 2.6 million RT observations (2.25 million unique peptides)
from 11 community datasets.

- The weights are `Chronologer_20220601193755.pt` from searlelab/chronologer at e518130ceb. The encoding is
  jchronologer's machine-readable form of Chronologer's `tensorize.py` (`Chronologer_20220601193755.preprocessing.json`,
  searlelab/jchronologer at b64a23363d). Both are Apache-2.0.
- Both files are committed in `models/chronologer-20220601193755`, copied beside the executable, and pinned by
  SHA-256 in `ChronologerFiles`. A different file changes every prediction, so any other is refused, before a
  training run starts and by `build.ps1 -TestData Verify`. `CARAFESHARP_CHRONOLOGER_MODEL` names another folder
  holding the same pinned files.
- `ChronologerFiles.VERSION` (`20220601193755`) names this Chronologer. A newer one would get its own folder,
  pins and version, next to this one.
- Chronologer's training database (`data/Chronologer_DB_220308.gz`, most of its repository) is not included.

## What it predicts: the hydrophobic index

Chronologer predicts one number per peptide, its hydrophobic index (HI): in Chronologer's definition, the
percentage of acetonitrile in 0.1% formic acid at which the peptide elutes from C18. HI belongs to the peptide and
the chemistry (C18, formic acid, acetonitrile), not to a gradient: a peptide has the same HI on a 10-minute and a
2-hour gradient, and the precursor charge does not enter it. The gradient decides when the column's acetonitrile
reaches a peptide's HI, so turning HI into minutes is a run's part ("From HI to minutes" below).

CarafeSharp never converts minutes to HI. Chronologer's authors did, to build its training database: each of the
11 datasets, on its own gradient, had its retention times put onto the HI scale first, so that all of them could
train one model ("How Chronologer trains on many sources" below).

The scale in practice:

| | |
|---|---|
| Typical tryptic peptides | FLEQQNK 3.75, VATVSLPR 8.39, LGEHNIDVLEGNEQFINAAK 14.29, MPC[+57]AEDYLSVVLNQLC[+57]VLHEK 24.17 (jchronologer's golden cases, which `ChronologerModelTest` checks) |
| HI to iRT (the 11 Biognosys iRT kit peptides, least squares) | iRT = 7.254 x HI - 45.47: iRT 0 at HI 6.3, iRT 100 at HI 20.1 |
| HI to elution time, Stellar HeLa, 24-min gradient | 0.884 min per HI + 1.95 min |
| HI to elution time, Astral HeLa, 24-min gradient | 0.866 min per HI + 1.87 min |
| HI to elution time, ZT Scan HeLa, 11.4-min gradient | 0.425 min per HI + 0.98 min |

The elution-time lines are the starting fits of the #4759 fine-tunes below (normalized RT = a x HI + b on each
run's confident peptides, times its rt_max). A straight line from the pretrained model's HI already explains
99.1-99.8% of the held-out normalized RT variance on those runs (R2 0.9914 ZT Scan, 0.9973 Astral, 0.9976
Stellar), the end of the gradient included. That is the property AlphaPeptDeep's generic model lacks: its
pretrained R2 on the same runs' held-out peptides was 0.8586-0.9172.

## The network and its encoding

`ModelChronologer` is a TorchSharp port of Chronologer's `model.py` and `core_layers.py`, loading the PyTorch
`state_dict` with `PthReader` (120,321 values):

- `seq_embed`: an embedding of 55 tokens to 64 dimensions, token 0 the padding.
- `resnet_blocks.{0,1,2}`: three ResNet blocks with dilations 1, 2 and 3. Each is two units (`process_blocks`), a
  1x1 `Conv1d(64, 64)` and then a kernel-7 one with same padding, each followed by `BatchNorm1d(64)` and ReLU; the
  block's input is added back and passed through ReLU. Each block also carries a `shortcut` (a `Conv1d` and a
  `BatchNorm1d`) that Chronologer uses only when the input and output widths differ, so its weights are loaded and
  never run.
- Dropout (0.1), flatten (channel-major, as in Python), and `output`: one `Linear` from 52 x 64 to 1.

The nine `BatchNorm1d` layers (six used) each hold a learned scale and shift and the running mean and variance
of their 64 channels, accumulated over Chronologer's own training batches. Those statistics are what the
network normalizes every input with in eval mode; "Fine-tuning" below keeps them as they are.

**Encoding** (`ChronologerEncoding`, from the JSON):
- A peptide is first written as an EncyclopeDIA mass-annotated sequence from its alphabase modifications
  (`ToModifiedSequence`): a residue's modifications as one mass after it, a terminal modification's before the
  sequence, a residue-specific N-terminal one (`Gln->pyro-Glu@Q^Any_N-term`) on residue 1. Modifications at one
  position are one mass, their sum, as EncyclopeDIA and jchronologer write them: N-terminal ammonia loss on a
  carbamidomethyl-Cys becomes the cyclized `C[+39.994915]`.
- The JSON's 18 rules turn each modified residue into a token of its own:

  | Residue and mass | Token | | Residue and mass | Token |
  |---|---|---|---|---|
  | C +57.02 (carbamidomethyl) | `c` | | K +42.01 (acetyl) | `a` |
  | C +39.99 (cyclized carbamidomethyl, N-term) | `d` | | K +100.0 (succinyl), +114.0 (GlyGly) | `b`, `u` |
  | M +15.99 (oxidation) | `m` | | K +14.01, +28.03, +42.04 (methyl) | `n`, `o`, `p` |
  | E -18.01, Q -17.02 (pyro-Glu, N-term) | `e` | | R +14.01, +28.03 (methyl) | `q`, `r` |
  | S, T, Y +79.97 (phospho) | `s`, `t`, `y` | | K +224.1, +229.1 (TMT) | `z`, `x` |

- An N-terminal token leads the sequence: `-` free, `^` acetyl (+42.01), `&` TMT0, `*` TMT10, and `(` and `)` when
  residue 1 is pyro-Glu or cyclized carbamidomethyl-Cys. `_` ends it, and 0 pads it to 52 positions.
- A form is rejected, not guessed at, when it has 5 or fewer or more than 50 residues, a modification no rule
  matches, or a C-terminal modification. A rejected form predicts NaN from `ChronologerModel`.

## In library prediction

Which RT model a library uses: `-rt_model`, else the one a saved model (`-model`) or model folder (`-model_dir`)
holds or names, else Chronologer. A fine-tuned RT model of the other kind is replaced by the named kind's
pretrained model, and the log says so.

- **Pretrained Chronologer:** the library's RT is iRT, from the iRT kit line above (`FitIrtCalibration`), and
  rt_max does not apply (`Ignored rt_max`). Osprey calibrates iRT to each run's minutes, as for any iRT library.
- **Fine-tuned Chronologer, runs on one gradient** (the default, aligned or not): it predicts the training runs'
  normalized RT, so the library's RT is `rt_pred x rt_max`, minutes on the training gradient, as for a fine-tuned
  AlphaPeptDeep model, and the model needs nothing else to give them, as a library for a targeted assay wants.
  Predictions are clipped at 0, as AlphaPeptDeep's are. Aligned, they are the runs' median minutes; with
  `-rt_reference <run>` (the run's name, or a unique end of it such as `_55`) the training runs' maps (`rt_maps.json`,
  kept beside the model) take them to that run's.
- **Chronologer of the hydrophobic index with the runs' maps**: fine-tuned on runs of different gradients, or the
  pretrained one after an aligned `-tf ms2` training. The library takes HI to minutes by the runs' median map
  (`AlignedRtPredictor`), or one run's with `-rt_reference`. When the runs are on different gradients their median is
  none of them: the library is iRT, with a warning naming the runs `-rt_reference` can choose. Minutes are clipped
  at 0.
- **Forms Chronologer rejects** (`ChronologerRtPredictor`): AlphaPeptDeep's pretrained RT model predicts them, and
  its prediction is carried onto Chronologer's scale through both models' iRT kit fits: with
  `iRT = a x alphapeptdeep + b` and `iRT = c x chronologer + d`, `chronologer = (a / c) x alphapeptdeep + (b - d) / c`.
  The log counts them. This is why a library with Chronologer needs the pretrained AlphaPeptDeep archive too. With
  the default digest (7 to 35 residues) and modifications (`-fixMod 1` carbamidomethyl-Cys, `-varMod 2` oxidized
  Met) no form is rejected; protein N-terminal acetyl and phospho S/T/Y are encoded too.
- Batches of 2,048 forms, on the CPU or the GPU (`-device`).

## From HI to minutes, and other gradients

What a library's RT is depends on the model and on whether the training aligned its runs:

| Library | Its RT | Where minutes come from |
|---|---|---|
| Pretrained Chronologer (the starting library) | iRT, from HI by one fixed line (iRT = 7.254 x HI - 45.47) | Osprey fits each run's own map from library RT to minutes on its confident first-pass peptides, as for any iRT library |
| Fine-tuned Chronologer, runs on one gradient (the default) | minutes on the training gradient (`rt_pred x rt_max`); aligned, the runs' median, or one run's with `-rt_reference` | the fine-tune learned that gradient |
| Fine-tuned Chronologer, runs on different gradients | iRT, or one run's minutes (`-rt_reference`) | each training run's monotone map between its minutes and HI, kept beside the model (`rt_maps.json`); the network holds no gradient |

Osprey calibrates every run it searches, whichever the library is.

On a linear gradient, elution time is close to a line in HI: roughly the dwell time plus HI divided by the
gradient's acetonitrile per minute (about 0.87 min per HI on the 24-min Stellar and Astral runs above). On any other
shape, such as segments, a curved ramp or a steep wash, it is the time at which the gradient reaches the peptide's
HI: still a monotone function of HI, though not a line.

- **A new gradient, the same chemistry** (C18, formic acid, acetonitrile; any length or shape). The starting
  library needs nothing new: its iRT is HI rescaled by a line, and Osprey's per-run calibration is not limited to a
  line, so it follows the gradient's shape, given confident peptides across the run. A fine-tune on a run of that
  gradient then learns its shape: the line it starts from (step 3 below) is only a start, and the network fits the
  rest (on ZT Scan's 11.4-min gradient, R2 0.991 from the line and 0.998 fine-tuned). Its library is in minutes for
  that gradient.
- **A model on another gradient.** A model of one gradient predicts minutes on it. For a run on another gradient of
  the same chemistry they keep the elution order, and Osprey's calibration maps them, but fine-tuned further on
  another gradient it is retrained to that one; fine-tuned further on runs of different gradients, which share no
  minutes, Chronologer starts over from its pretrained model, with a warning. A model of different gradients
  predicts HI, which holds no gradient: each gradient keeps its own map.
- **Runs of different gradients together** (`-rt_align kde`): every run's minutes go to HI by its own map, so they
  train one model (on Astral and ZT Scan runs together, each run's RT error stayed at its own model's; "Aligning the
  runs"). Their library is iRT, or one run's minutes with `-rt_reference`.
- **Another chemistry** (another stationary phase, an ion-pairing agent, high pH). Peptides elute in another order,
  which no map from HI can repair, so the starting library is only approximate. A fine-tune on runs of that
  chemistry relearns the order, starting from the C18 model.
- **A library in minutes without fine-tuning the RT model:** `-tf ms2` with the runs aligned leaves the pretrained
  Chronologer and the runs' maps, so its library is the pretrained HI in the runs' median minutes.

## How CarafeSharp fine-tunes Chronologer

`-rt_model chronologer` with `-tf all` or `-tf rt` fine-tunes the pretrained Chronologer, or with `-model` the
saved model's Chronologer, on Osprey's training export. Step by step (`FineTuneRun.TrainChronologer`,
`ModelFineTuner.TrainChronologer`):

1. **The training rows** are AlphaPeptDeep's (`OspreyTrainingSet`): one row per peptide form passing the run
   q-value threshold, from its best precursor (lowest run q, then highest score, across all runs). `rt_max` is one
   normalizer for the whole training set: the largest of every run's last MS2 RT plus 0.1 and `-rt_max`.
   - Aligned (the default): every observation's minutes first go to HI by its run's map, so the row's HI is its
     best precursor's (or, with `-rt_select median`, the median over the runs of each run's best), and its
     normalized RT is the median map's minutes of that HI over `rt_max`: drift taken out. Runs on one gradient train
     on that normalized RT; runs on different gradients on the HI.
   - Unaligned (`-rt_align none`): its target is the normalized RT `apex RT / rt_max`, as Carafe's.
2. **The split** is Carafe's (`TrainingSplit`, seed 1337): up to 1,000 held-out forms whose sequences are not in
   training. Forms Chronologer cannot encode are left out of both, and the log counts them per set. When it can
   encode none of the held-out forms it is tested on its training forms, as the split tests on every row when none
   are left for testing.
3. **The scale is fixed first.** The least-squares line `rt_norm = a x HI + b` over the training forms is folded
   into the output layer (the weights times `a`, the bias times `a` plus `b`), so the network starts at the best
   linear calibration (held-out R2 0.991-0.998 above) and the first epochs refine its features instead of learning
   a new scale.
   - On normalized RT (runs on one gradient), the pretrained HI is fitted to the rows' normalized RT
     (`RescaleToNormalizedRt`), and the model then predicts normalized RT. A saved Chronologer that already does skips
     this.
   - On HI (runs on different gradients, `FineTuneRun.TrainChronologerOnHi`), the rows' HI is fitted to their
     normalized RT, and the targets are put through the same line, so the optimizer sees the scale the other
     fine-tune does. The inverse line is folded back out before saving (`FoldOutputLine`), so the saved model
     predicts HI. It is scored at the median map's normalized RT of what it predicts.
4. **Training** follows Carafe's RT fine-tune (`FineTuneSettings.RtDefaults`): Adam at learning rate 1e-4, 40
   epochs, 10 of them warming up linearly and the rest a half-cosine decay, stepped per epoch; batches of 1,024 forms,
   or, when that gives an epoch fewer than 40 steps, the largest power of two (at least 32) that gives at least 40 (512 for
   22,000-25,000 forms); gradients clipped to norm 1; L1
   loss on the normalized RT; dropout on; the last epoch's weights kept. The seed (`-seed`, default 2024) sets
   libtorch's generator and the batch shuffles.
5. **Every weight trains**, the BatchNorm layers' scale and shift included, but **their running statistics stay
   frozen** (`ModelChronologer.FreezeBatchNorm` keeps those layers in eval mode while the rest trains). Fine-tuning
   batches are small and come from one run; letting them overwrite statistics accumulated over 2.6 million peptides
   of 11 sources would shift every feature the output layer reads.
6. **Batches mix peptide lengths.** AlphaPeptDeep's RT fine-tune groups each batch by length; Chronologer pads every
   peptide to the same 52 positions, and its output layer weighs each position separately, so single-length batches
   pull that layer a different way for each length. On ZT Scan run D1 (22,981 forms) mixed batches took the held-out
   L1 from 0.00916 (single-length) to 0.00628.
7. **Before and after,** the held-out forms are scored (R2, median absolute error of normalized RT), and the model
   is saved as `rt.safetensors` with the metadata `carafesharp.rt_model = chronologer`,
   `carafesharp.rt_scale` (`normalized_rt`, or `hydrophobic_index` for runs on different gradients) and
   `carafesharp.chronologer_version = 20220601193755`. `model.json`
   records `rt.model = chronologer` and the rows trained on, and the saved model names the network, version and
   start (06-saved-models.md).

The fine-tunes of the #4759 matrix, each on one run (held-out normalized RT; "pretrained" is after step 3):

| Run | Forms (rt_max) | Batch | Pretrained R2 / median error | Fine-tuned R2 / median error |
|---|---|---|---|---|
| Stellar `_21` | 25,056 (24.10 min) | 512 | 0.9976 / 0.00622 | 0.9989 / 0.00376 |
| Astral `_55` | 87,879 (23.97 min) | 1,024 | 0.9973 / 0.00642 | 0.9989 / 0.00397 |
| ZT Scan D1 | 22,943 (11.41 min) | 512 | 0.9914 / 0.00801 | 0.9980 / 0.00486 |

A median error of 0.004 of a 24-min gradient is 0.09 min.

**How this differs from Chronologer's own training** (`train.py`, `training_functions.py`, `settings.py`), which
builds or extends the published model from many sources:

| | Chronologer's training | CarafeSharp's fine-tune |
|---|---|---|
| Target | HI, every source aligned onto it first | the run's normalized RT, after the line of step 3 |
| Loss | Laplace negative log-likelihood with a learned scale per source; losses past the 99th percentile of a Laplace fitted to the batch dropped as outliers | L1 |
| BatchNorm statistics | accumulate (train mode) | frozen |
| Schedule | Adam at a constant 1e-3, 100 epochs, the batch growing from 64 to double every 30 epochs (about 630 by epoch 100); the best epoch on validation kept | Adam at 1e-4 with warmup and cosine decay, 40 epochs, the last epoch kept |
| Held out | 20% of each source (seed 2447) | Carafe's split, up to 1,000 forms |
| Batches | shuffled, all lengths | shuffled, all lengths |

CarafeSharp's choices adapt the model to one LC method from a few thousand to a hundred thousand peptides
without moving what the 2.6 million taught it; Chronologer's are for training on many sources at once, which the
next section is about.

## Training data from many experiments

**Without alignment** (`-rt_align none`, as Carafe trains) every RT row is divided by one normalizer, the largest
run's rt_max, and a form found in several runs takes its RT from its best one. That holds for replicates of one
method, though each row carries its run's drift, and fails for runs on different gradients: a form's target depends
on which run it came from, and the library's minutes belong to no run. CarafeSharp aligns the runs first, as
Chronologer does its sources ("Aligning the runs", below).

**How Chronologer trains on many sources.** Its training data carries a source per observation (a dataset, in its
database), and three parts of its training use it:
1. **Each source is aligned onto HI before training.** For a new source, `Align_RT_to_Hydrophobic_Index.py`
   (`kde_alignment.py`) does it with the current model: Chronologer predicts HI for the source's peptides, a
   two-dimensional kernel density of (observed RT, predicted HI) is built on a 3,000 x 3,000 grid with a Silverman
   bandwidth, and the ridge of that density, walked from its apex in both directions, gives a monotone
   piecewise-linear map from the source's RT to HI, clipped to the range it covers. Every source then trains on one target, whatever its gradient, column or instrument.
2. **The loss learns how noisy each source is** (`LogL_Loss`): each observation's one-hot source picks a Laplace
   scale (a bias-free `Linear(n_sources, 1)`, started at 10), and the loss is the Laplace negative log-likelihood of
   the prediction around the observed HI. A source with larger errors learns a larger scale and counts for less. The
   observations past the 99th percentile of a Laplace fitted to the batch's losses are dropped as outliers.
3. **Each source is split on its own**, 20% held out; a source too small to hold out 2 observations is left out.

Its BatchNorm statistics accumulate over batches that mix every source, so they describe the harmonized
multi-source data. That is the second reason CarafeSharp keeps them frozen for a one-run fine-tune, and the reason
a training on many sources would let them update.

### Aligning the runs (`-rt_align kde`, the default)

CarafeSharp takes the first of these: it separates what belongs to the peptide (its HI, which one model predicts)
from what belongs to a run (a monotone map between the run's minutes and HI). The run is the source.
(`OspreyTrainingSet.AlignRuns`, `RtMapFit`, `RtAlignment`)

1. **Each run's points**, one per peptide form the run identified at the run q threshold: the mean apex RT of its
   precursors in that run, against the pretrained Chronologer's HI of the form (forms it cannot encode left out;
   each form predicted once, whichever runs found it). The pretrained model is the reference whatever model trains,
   so every training aligns onto the same scale. A run is named by its export's file stem; exports of one name from
   different folders are two runs, named with their folder (`dayA/QC_01`).
2. **Each run's map**, as firm as its forms allow, in tiers as Osprey calibrates RT (`Calibrator.SelectFitPlan`):

   | Forms in the run | Map |
   |---|---|
   | 1,000 or more | Chronologer's KDE ridge (`KdeRidgeAlignment`, a port of `KDE_align` whose knots match Chronologer's to 1e-9) |
   | 200-999 | a robust LOESS: local lines over the nearest 30% of the forms, tricube weights, 2 bisquare iterations |
   | 100-199 | the same LOESS over 60 forms, a wider fraction as they thin, so the curve stiffens toward a line |
   | 15-99 | a Theil-Sen line, when the forms span at least half the run's gradient |

   A LOESS curve is made monotone by pooling adjacent violators, each pooled block one knot at its mean, so the map
   rises strictly. A run that cannot be mapped gives no RT rows, with a warning, while its spectra still train MS2:
   fewer than 15 forms, minutes or HI that do not vary, a line's forms bunched, or HI that does not rise with minutes
   (for the KDE ridge, whose walk only steps up, a correlation of 0.5 or less; real runs are about 0.98). When no run
   can be mapped, the training is unaligned, and the log warns why. The log names each run's fit.
3. **The rows are chosen on HI**: every observation's minutes go through its run's map, and a form's row is its best
   precursor's HI, or with `-rt_select median` the median over the runs of each run's best. A form counts once,
   whichever run it came from, so a run with more forms carries more of the loss.
4. **Back to minutes**: the median map is the pointwise median of the runs' inverse maps (HI to minutes) on 1,000
   points, extended past their range with the slope of each end's outer 20% of knots. A median of monotone maps is
   monotone. It gives each row's normalized RT (over the training's normalizer, the largest run's rt_max, which the
   maps record) and the library's minutes, which for three replicates lie in the
   middle run: the library sat -0.048, +0.001 and +0.058 min from Astral runs `_49`, `_55` and `_60`.
5. **One gradient or several**: the log gives how far the runs are from the median (the largest and the
   95th-percentile distance over the HI range every run covers). Within 5% of the median's span (1.1 min on 22 min)
   they are one gradient: the model trains on the median minutes and predicts minutes itself, so it needs no map to
   be used on its own. Past it, or with no HI range in common, they are different gradients, whose median is no run's
   minutes: Chronologer trains on
   HI, and its library is iRT, unless `-rt_reference` names a run whose minutes it takes (AlphaPeptDeep, which
   predicts minutes, warns). Three Astral replicates were 0.087 min apart (0.4%); an Astral run and a ZT Scan run
   6.4 min (30%).
6. **The maps are kept** as `rt_maps.json` in the model folder and the saved model: the reference model, the
   training's normalizer, each run's knots and fit, the median map and the spread (06-saved-models.md). With
   `-rt_reference`, a minutes model's predictions go back to the median minutes by that normalizer, whatever the
   library's `-rt_max`, and then to the run's. A training run checks `-rt_reference` before it trains: the run's
   name, `-rt_align kde` and Chronologer.

**Why the KDE ridge needs 1,000 forms.** Measured on Osprey's exports by how closely the runs' maps put one peptide at
one place: the 95th percentile, over the forms the runs share, of the largest difference among the runs (a temporary
harness, not a test). Unaligned, the replicates' minutes differ by 0.185 min.

| Forms fitted per run | Three Astral replicates, KDE / LOESS (min) | Astral and ZT Scan, KDE / LOESS (HI) |
|---|---|---|
| all (85,469 and 22,943) | 0.070 / 0.070 | 0.27 / 0.47 |
| 1,000 | 0.115 / 0.094 | 0.30 / 0.45 |
| 300 | | 0.34 / 0.48 |
| 200 | 0.179 / 0.136 | |
| 100 | 0.26 / 0.15 | |

Across gradients the KDE ridge is better at every size: the LOESS's window, 30% of the forms, bends differently on
gradients of different shape. On replicates that bias is the same in every run and cancels, the two agree on all
forms, and the density grows noisier as the forms thin, until on 100 it is worse than no alignment.

**What alignment did** (each arm a fine-tune and library, scored against DIA-NN's observed RT on each run: the
median distance from an isotonic calibration of the library RT, which a search that calibrates every run sees, and in
parentheses the median distance of the library's minutes, which a targeted assay sees):

| Training runs | Unaligned | Aligned, a model of minutes | Aligned, a model of HI with the maps |
|---|---|---|---|
| Astral `_55` | 0.091 (0.100) min | 0.091 (0.100) min, the default | 0.090 (0.098) min |
| Astral `_49`, `_55`, `_60` | 0.091 (0.100) min | 0.090 (0.099) min, the default | 0.090 (0.097) min, also with `-rt_select median` |
| Astral `_55` and ZT Scan D1, scored on Astral / ZT Scan | 0.099 / 0.135 min (ZT's last RT bin 0.131) | | 0.092 / 0.053 min (0.037), the default |

Runs of one gradient train a model of minutes, which needs nothing beside it to give them: it scored with the
unaligned model on one run, and kept alignment's gain on three, the pooled library's mean offset from the runs'
observed RT +0.003 min against +0.015 unaligned. A model of HI with the maps was within 0.002 min of it, closer only
at the start of the gradient (0.084 against 0.097 min in the first eighth of run `_60`), where the map follows the
gradient's onset. Each run's own model of HI scored 0.090 on Astral and 0.045 on ZT Scan. Unaligned, one model on
both gradients fitted neither, ZT Scan's worst; aligned, it came within 0.008 min of each run's own.

**Not taken from Chronologer:** its per-source Laplace loss scale, which counts a noisy source for less; its outlier
trimming; and BatchNorm statistics that update, which a large many-source training would want and a fine-tune on a
few runs would not. A run with more forms carries more of the loss, which is intended. What is in place for them:
`ModelFineTuner.Train<T>` takes any network and any loss over any row type, `ChronologerTrainingExample` carries the
tokens beside the row, and `RtTrainingExample` the HI.

## Saved models and versions

- A fine-tuned Chronologer is `rt.safetensors` in the model folder and the saved model, told from an AlphaPeptDeep
  RT model by its metadata (`carafesharp.rt_model = chronologer`), with the scale it predicts
  (`carafesharp.rt_scale`: `hydrophobic_index` pretrained or fine-tuned on runs of different gradients,
  `normalized_rt` fine-tuned on one gradient) and the Chronologer it was fine-tuned from
  (`carafesharp.chronologer_version`). A file from another version is refused, as its encoding may differ; a file
  without the key was saved before it, from 20220601193755.
- The saved model's manifest names the RT model (`models.rt.model = chronologer`, `model_version`, `start`);
  06-saved-models.md has the fields. A `-tf ms2` model holds no RT model and names the pretrained one its library
  predicts with.
- Fine-tuning further from a saved Chronologer starts from it (no rescale) and keeps its version; `-rt_model
  alphapeptdeep` on it starts AlphaPeptDeep's pretrained RT model instead, with a warning.
- An aligned training keeps the runs' maps as `rt_maps.json` (format `carafesharp-rt-maps-1`) beside the models and
  in the saved model; a library from either reads them. A later unaligned training into the same folder removes
  them.

## Results (#4759)

Each arm ran the workflow (predict, search one run, fine-tune, search three runs) with one Osprey build,
AlphaPeptDeep against Chronologer; matched FDP is by 1:1 entrapment, at the first crossing:

| | Single-file search | 3-file experiment precursors | Matched FDP 0.3% | DIA-NN recall in the last RT bin, 3-file |
|---|---|---|---|---|
| Stellar | 22,842 to 27,510 | 31,197 to 35,578 | 28,946 to 32,528 | 0.6% to 82.8% |
| Astral | 85,917 to 99,469 | 103,538 to 114,764 | 95,147 to 109,446 | 0.4% to 71.9% |
| ZT Scan | 20,423 to 26,518 | 34,569 to 40,404 | 34,814 to 40,780 | 11.6% to 67.5% |

The fine-tuned Chronologer library's RT error against DIA-NN's observed RT, on a run it did not train on (the
median distance from an isotonic calibration), was 0.06-0.15 min in the last two RT bins of each dataset, where
the fine-tuned AlphaPeptDeep library's was 0.12-0.99 min.

The regression goldens fine-tune on the same kind of search, of the Chronologer starting library
(`carafesharp-export-v2`, `carafesharp-export-astral-v2`; 04-testing.md). On the older exports, searches of Carafe's
library that found nothing past 19.8 min, their libraries' minutes after 22 min were 1.4-1.8 min from DIA-NN's; now
0.13-0.14 min.

## Tests

- `ChronologerModelTest`:
  - `TestChronologerGoldenParity`: jchronologer's golden cases, each coded sequence exactly and its HI to 2e-5, and
    its rejections.
  - `TestChronologerPeptideForms`: alphabase forms to mass-annotated sequences, same-site modifications summed,
    C-terminal modifications rejected.
  - `TestChronologerRtPredictorFallback`: a rejected form predicted by AlphaPeptDeep through the iRT bridge.
  - `TestChronologerFineTune`: the loss falls, the BatchNorm statistics do not move, the model saves and reloads,
    another version is refused, normalized RT clips at 0 and HI does not.
  - `TestChronologerIrtCalibration`, `TestChronologerFilesLocation`: the iRT fit and the pinned files.
- `RtAlignmentTest`:
  - `TestKdeAlignmentMatchesChronologer`: the KDE ridge's knots against Chronologer's own `KDE_align` on the same
    points (`KdeAlignmentReference`).
  - `TestRtMapFit`: each tier on a curved gradient with noise and false identifications, its error and name; a line
    whose forms span too little, HI that does not rise (the KDE ridge's too), values that do not vary, and the
    pooling of a curve that dips, ties first, each block one knot at its mean.
  - `TestMonotoneMap`, `TestRtAlignment`: maps, their inverses, extension and median; the spread, a run found by name
    or the end of it, one run's minutes, different gradients told from replicates, runs that share no HI range, and
    `rt_maps.json` read back with its normalizer.
  - `TestTrainingSetAlignment`: two drifting runs aligned by the LOESS and by the KDE ridge, every row on one line
    of HI; a third run too sparse to align giving no RT rows while the others stay aligned; two acquisitions of one
    file name from two folders as two runs; and a set no run of which can be aligned, saying why.
- `FineTuneLoopTest`:
  - `TestRtAlignmentTrainingRun`: two drifting runs of one gradient aligned by default, their maps, a model that
    predicts minutes after training and from the saved model, one run's minutes with `-rt_reference` (the same with
    `-rt_max` given), an unknown run and `-rt_reference` with AlphaPeptDeep refused, a misspelled run refused before the
    training, an aligned `-tf ms2` library in the median minutes, and an `-rt_align none` run removing the maps.
  - `TestRtAlignmentMixedGradients`: runs of different gradients train a model of HI, whose library is iRT with a
    warning, or one run's minutes with `-rt_reference`; a saved model of one gradient's minutes fine-tuned further on
    them starts over from the pretrained Chronologer.
  - `TestChronologerTrainingRun`: a training run, its saved model and the libraries from both, in minutes; a
    `-tf ms2` model's library in iRT.
  - `TestChronologerSavedModelChoice`: fine-tuning further, the RT model's kind chosen and its lineage recorded.
  - `TestChronologerUnencodableTestSet`: the test-set fallback, the encoded row counts, and start models of the
    wrong kind refused.
- The regression goldens (`regression.data/stellar`, `astral`) are fine-tunes with Chronologer (04-testing.md).
