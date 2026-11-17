# Chronologer retention-time model, 2022-06-01

`Chronologer_20220601193755.pt` holds the weights of Chronologer (Searle lab), a retention-time predictor trained on
a harmonized database of more than 2.6 million peptide RT observations from 11 datasets. It predicts a hydrophobic
index (HI), the % acetonitrile in 0.1% formic acid at which a peptide elutes from C18; a search engine's calibration
maps HI (or the iRT CarafeSharp writes from it) to minutes. It is CarafeSharp's default RT model, and CarafeSharp
fine-tunes it to predict minutes on a run's gradient: `docs/07-chronologer.md` documents both.

| | |
|---|---|
| Weights | `Chronologer_20220601193755.pt` (505,055 bytes) |
| SHA-256 | `1a500c246b49a1a23643bce7f2df86d5a107359bf0ec34365531c73431b6c0b3` |
| Weights source | <https://github.com/searlelab/chronologer>, `models/`, at commit e518130ceb (2024-08-20) |
| Encoding | `Chronologer_20220601193755.preprocessing.json` (3,030 bytes, SHA-256 `ae67c1343b3b1603eedc3b1df8193dfb3c286655d9d16cbc64e790c48112f967`) |
| Encoding source | <https://github.com/searlelab/jchronologer>, `src/main/resources/models/`, at commit b64a23363d (2026-09-21) |
| License | Apache-2.0 (`LICENSE.txt`; the same text in both repositories) |

The training database (`data/Chronologer_DB_220308.gz` in the chronologer repository) is not included.

## The files

- The weights are a zip-format PyTorch `state_dict` (`torch.save(model.state_dict())`), which `PthReader` reads.
  The network (`src/chronologer/model.py`, `chronologer_utils/core_layers.py`) is:
  - an embedding of 55 tokens to 64 dimensions;
  - three ResNet blocks with dilations 1, 2 and 3, each a 1x1 and a kernel-7 `Conv1d` (`padding='same'`) with
    `BatchNorm1d` and ReLU, plus a residual add. Each block also carries a shortcut convolution whose weights are in
    the checkpoint but which `forward` never uses, since its input and output widths are equal;
  - dropout, flatten, and one `Linear` from 52 x 64 to 1.
- The preprocessing JSON is jchronologer's machine-readable form of Chronologer's encoding (`tensorize.py`): the
  55-token vocabulary, the modification rules (EncyclopeDIA-style mass annotations, such as `C[+57.021464]` to `c`),
  the N-terminal codes, padding index 0, and an input of 52 tokens (`-`, up to 50 residues, `_`, padded).

## Why it is here

AlphaPeptDeep's generic RT model, which CarafeSharp starts from, compresses the end of the gradient: on ZT Scan its
predictions for peptides that elute over the last minute span a tenth of the mid-run slope, and fine-tuning on a
first pass that misses those peptides keeps the plateau (issue #4759). Chronologer does not plateau. Through Koina,
against DIA-NN's IDs, after an isotonic calibration:

| Predictor | ZT Scan late / early error (min) | Stellar late / early error (min) |
|---|---|---|
| Chronologer | 0.041 / 0.072 | 0.163 / 0.138 |
| AlphaPeptDeep generic | 0.205 / 0.160 | 0.681 / 0.225 |

Committing the files means CarafeSharp can predict with Chronologer offline, from the exact weights these results
were measured with.

Do not replace these files. A newer Chronologer gets its own folder and its own pin next to this one.
