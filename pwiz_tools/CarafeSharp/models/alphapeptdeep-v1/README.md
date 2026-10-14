# AlphaPeptDeep pretrained models, v1

`pretrained_models.zip` holds the pretrained MS2 and RT models (`generic/ms2.pth`, `generic/rt.pth`) that
CarafeSharp predicts from and fine-tunes, and the CCS model (`generic/ccs.pth`) that predicts a library's
timsTOF ion mobility with `-ccs`. They are the exact weights Carafe 2.2 uses.

| | |
|---|---|
| File | `pretrained_models.zip` |
| Size | 25,614,761 bytes |
| SHA-256 | `75e6037db3280a513d0f6010a21dba4e8ea47a8d67127f38c77fb1f9a7d408eb` |
| Source | <https://github.com/MannLabs/alphapeptdeep/releases/download/pre-trained-models/pretrained_models.zip> |
| License | Apache-2.0 (`LICENSE.txt`, from MannLabs/alphapeptdeep at commit 5fa3389473) |

## Why it is committed

The MannLabs "pre-trained-models" release URL is unversioned.
- The asset was published in June 2022 and replaced in October 2022. That is how two Carafe runs came to predict
  from different weights.
- The release has since added `pretrained_models_v2.zip` (2025-03) and `pretrained_models_v3.zip` (2025-05).
  Current peptdeep downloads v3 by default.

Carafe 2.2.0 uses this v1 archive:
- Its vendored `src/main/resources/py/v2/models.py` names `pretrained_models.zip` and the URL above.
- Its Python environment runs peptdeep 1.1.0, whose `default_settings.yaml` says the same.

CarafeSharp's predictions match Carafe 2.2.0's own to about 1e-5 with these weights (`CarafeParityTest`).
Committing the archive means a fresh clone builds and predicts with nothing to download, offline, from the
weights every parity result was measured with.

## How CarafeSharp finds it

The build copies this folder next to `CarafeSharp.exe` and the test assembly (`Directory.Build.targets`).
`PretrainedModels.DefaultPath` looks, in order, at:
1. `%CARAFESHARP_PRETRAINED_MODELS%`, when set. A set variable that names a missing file is an error; it does not
   fall through to the next location.
2. `models/alphapeptdeep-v1/pretrained_models.zip` beside the executable.
3. peptdeep's own `~/peptdeep/pretrained_models/pretrained_models.zip`, which Carafe shares.

`-pretrained <zip>` overrides all three. Any archive whose SHA-256 differs from the pin is refused.

Do not replace this file. A different model (for example Carafe 3.0's foundation model) gets its own folder and
its own pin next to this one, so that results made with v1 stay reproducible.
