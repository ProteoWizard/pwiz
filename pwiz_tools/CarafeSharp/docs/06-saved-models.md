# 06. Saved models (.carafemodel)

A fine-tuned model saved as one file, to predict libraries of any peptides from later without
training again. Every training run writes one, `-model` predicts from it, and `-model_info`
describes it. The file also records what the model was trained on (instruments, fragmentation,
NCE, gradient, windows, charges, peptides and how the fine-tune scored), so a user can choose among
saved models: on CarafeSharp's command line, in its GUI to come, or in Skyline.

This is CarafeSharp's, not Carafe's. Carafe keeps its fine-tuned models in the training run's output
folder and predicts from that folder with `-model_dir`, which also takes the training run's precursor
window. `-model_dir` still works as Carafe's does.

## Using one

```
CarafeSharp -i <x.training.parquet|folder> -ms <runs> -o train_out -db proteins.fasta [training options]
    # also writes train_out/carafe_fine_tuned_model.carafemodel

CarafeSharp -model_info train_out/carafe_fine_tuned_model.carafemodel
    # what the model was trained on

CarafeSharp -db other_proteins.fasta -model train_out/carafe_fine_tuned_model.carafemodel -o new_library [library options]
    # predicts other_proteins.fasta with the fine-tuned model, no training
```

The file can be renamed, copied and shared; CarafeSharp reads it by its content, not its name.

**Which settings apply.** A saved model predicts any peptides, so the new run's command line decides
what to predict. The training run supplies only what its fine-tuned models depend on:

| Setting | From |
|---|---|
| Precursor m/z window (`-min_pep_mz`, `-max_pep_mz`), charges, digestion, modifications | the command line |
| Fragment m/z range, top fragments, minimum fragments, library format | the command line |
| NCE, instrument | the training run, unless `-nce` or `-ms_instrument` is given |
| rt_max | the training run, unless `-rt_max` is given |

rt_max comes from the training run because the fine-tuned RT model predicts retention time on the
training run's gradient: its predictions are in minutes only when scaled by that run's rt_max. Give
`-rt_max` only for a run on a different gradient, knowing that the model was trained on another.
With several training runs, the library takes the last run's NCE and instrument and the largest
rt_max, as the library predicted right after training does.

**Which models apply.** The file holds the fine-tuned models the training chose to predict with:
- MS2: the fine-tuned model only when it beat the pretrained one on all four held-out metrics (as
  Carafe decides it); otherwise the file has none and the bundled pretrained model predicts MS2.
- RT: the fine-tuned model whenever the RT model was trained (`-tf all` or `-tf rt`).

`-tf ms2` or `-tf rt` on the prediction command takes only that model from the file, and the other
pretrained. The log says which it used: `Use the saved model ...: MS2 fine-tuned, RT fine-tuned;
trained on ...`.

**Reproducibility.** Over the training library's own precursor window, the saved model predicts it
again exactly (358 of 358 spectra byte for byte on the Stellar subset). Over another window,
intensities can differ in the last float32 digits (8.6e-7 of the base peak at most), because the
other peptides in a prediction batch change the rounding; m/z, retention times and the fragments kept
are the same.

## The format

For every reader: CarafeSharp itself, its GUI, and Skyline, which lists a folder's `.carafemodel`
files and shows what each was trained on. A `.carafemodel` file is a zip holding:

| Entry | Holds | Present |
|---|---|---|
| `manifest.json` | what the file is, and what the models were trained on (below) | always |
| `ms2.safetensors` | the fine-tuned MS2 model (AlphaPeptDeep's BERT, safetensors) | when `models.ms2.used` |
| `rt.safetensors` | the fine-tuned RT model (AlphaPeptDeep's LSTM/CNN, safetensors) | when `models.rt.used` |
| `model_evaluation_metrics.json` | the held-out metrics of the pretrained and fine-tuned models, as Carafe writes them | when the training wrote it |
| `meta.json` | the training runs, as Carafe writes it (what `-model_dir` reads) | when the training wrote it |

A reader that only lists models, as a model picker does, needs `manifest.json` alone.

`manifest.json` from the chained regression leg's run on the Stellar subset (three runs; only the first
is shown, and hashes and the path are shortened). The fine-tuned MS2 model lost to the pretrained one there
(SPC 0.90760 against 0.90765), so the file holds only the fine-tuned RT model:

```json
{
  "format": "carafemodel-1",
  "creator": "CarafeSharp 26.1.1.0+a943ca409b...",
  "created": "2026-09-30T21:15:19.0027444Z",
  "training_type": "all",
  "models": {
    "ms2": {
      "fine_tuned": true,
      "used": false,
      "entry": null
    },
    "rt": {
      "fine_tuned": true,
      "used": true,
      "entry": "rt.safetensors"
    }
  },
  "pretrained_sha256": "75e6037db3280a513d0f6010a21dba4e8ea47a8d67127f38c77fb1f9a7d408eb",
  "ms2_start_model": null,
  "training": {
    "settings": {
      "fdr": 0.01,
      "min_correlation": 0.8,
      "masking": true,
      "seed": 2024
    },
    "data": {
      "ms2_spectra": 107,
      "rt_peptide_forms": 179,
      "ms2_charges": {
        "2": 86,
        "3": 21
      },
      "peptide_length_min": 9,
      "peptide_length_max": 19,
      "modifications": {
        "Carbamidomethyl@C": 23
      }
    },
    "runs": [
      {
        "run": "Ste-2024-12-02_HeLa_4mz_sDIA_400-900_20",
        "ms_file": "D:\\data\\Ste-2024-12-02_HeLa_4mz_sDIA_400-900_20.mzML",
        "instrument_vendor": "Thermo",
        "instrument_model": "Stellar",
        "instrument": "",
        "nce": 30,
        "dissociation_methods": {
          "HCD": 200
        },
        "collision_energies": {
          "30": 200
        },
        "rt_min": 6.5132626409,
        "rt_max": 13.47915817605,
        "isolation_mz_min": 592.520080566406,
        "isolation_mz_max": 596.520080566406,
        "ms2_mz_min": 200.34360733447488,
        "ms2_mz_max": 1500.0818885844749,
        "fragment_tolerance": 0.3259665514463067,
        "fragment_tolerance_unit": "Th",
        "precursors": 118,
        "precursor_charges": {
          "2": 92,
          "3": 26
        },
        "run_q_pass": "2",
        "max_q": 0.01,
        "osprey_version": "26.1.1.273",
        "search_hash": "269039de...",
        "library_hash": "e9b7ebb1..."
      }
    ],
    "held_out_metrics": {
      "ms2.finetuned.cos": 0.8564939498901367,
      "ms2.finetuned.pcc": 0.8445116877555847,
      "ms2.finetuned.sa": 0.65472412109375,
      "ms2.finetuned.spc": 0.9076035618782043,
      "ms2.pretrained.cos": 0.8522642850875854,
      "ms2.pretrained.pcc": 0.8413179516792297,
      "ms2.pretrained.sa": 0.6495422720909119,
      "ms2.pretrained.spc": 0.9076478481292725,
      "rt.finetuned.mae_normalized": 0.013386447002795454,
      "rt.finetuned.r2": 0.9475898538461316,
      "rt.pretrained.mae_normalized": 0.34781907597887807,
      "rt.pretrained.r2": -7.661814407028599
    }
  },
  "prediction_defaults": {
    "nce": 30,
    "instrument": null,
    "rt_max": 13.58782254745
  },
  "entries": {
    "rt.safetensors": "56668607...",
    "model_evaluation_metrics.json": "2834903c...",
    "meta.json": "cdcc4271..."
  }
}
```


| Field | Meaning |
|---|---|
| `format` | `carafemodel-1`. A reader refuses any other value: a new value means a change old readers cannot read. Fields may be added within a format; readers ignore fields they do not know. |
| `creator`, `created` | The CarafeSharp version that wrote the file, and when (UTC, ISO 8601). |
| `training_type` | The training run's `-tf`: `all`, `ms2` or `rt`. |
| `models.<ms2\|rt>.fine_tuned` | The training fine-tuned this model. |
| `models.<ms2\|rt>.used` | The file holds the fine-tuned model, at `entry`, and prediction uses it. False with `fine_tuned` true: the fine-tuned MS2 model did not beat the pretrained one. |
| `pretrained_sha256` | The SHA-256 of the pretrained archive the training started from (AlphaPeptDeep v1, `models/alphapeptdeep-v1`), or null. |
| `ms2_start_model` | The file name of the `-ms2_model` the MS2 fine-tune started from instead of the pretrained model, or null. |
| `training` | What the models were trained on, or null (below). |
| `prediction_defaults` | The NCE, instrument and rt_max a library takes unless its command line gives its own. `instrument` is null when the training runs' instrument is not one of Carafe's classes. |
| `entries` | Every other entry and its SHA-256 (lowercase hex). |

`training`:

| Field | Meaning |
|---|---|
| `settings` | The run q-value the precursors were selected at (`-fdr`), the fragment correlation masking kept ions at (`-cor`), whether masking was on (`-no_masking`), and the seed. |
| `data.ms2_spectra`, `data.rt_peptide_forms` | How many MS2 spectra and RT peptide forms the models trained on. |
| `data.ms2_charges` | The MS2 spectra by precursor charge. |
| `data.peptide_length_min`, `_max` | The training peptides' lengths. |
| `data.modifications` | The training peptide forms carrying each modification (alphabase names). |
| `runs[]` | Each training run, from Osprey's training export footer (Osprey's docs/22-training-export.md). |
| `runs[].run`, `ms_file` | The run's name, and its file as `-ms` named it. |
| `runs[].instrument_vendor`, `instrument_model` | As the run's data file reports them; null when Osprey searched it without the file. |
| `runs[].instrument` | The instrument class the models were trained for (Carafe's: Lumos, Astral, ...), or empty when the model is none of them. |
| `runs[].nce` | The NCE the models were trained with. |
| `runs[].dissociation_methods`, `collision_energies` | MS2 spectra by dissociation method and by collision energy as the file reports it (normalized for Thermo, eV for Sciex), over the spectra Osprey sampled; empty without the data file. |
| `runs[].rt_min`, `rt_max` | The run's first and last MS2 retention time, minutes. |
| `runs[].isolation_mz_min`, `_max` | The range of the run's isolation windows. |
| `runs[].ms2_mz_min`, `_max` | The MS2 m/z range the run measured. |
| `runs[].fragment_tolerance`, `_unit` | The calibrated fragment tolerance Osprey matched with. |
| `runs[].precursors`, `precursor_charges` | The target precursors the run gave the training, and those by charge. |
| `runs[].run_q_pass`, `max_q` | The Osprey pass whose run q-values selected them (`2`; `1` for a run with no second pass), and the threshold. |
| `runs[].osprey_version`, `search_hash`, `library_hash` | The Osprey that searched the run, and the search and library it searched with. |
| `held_out_metrics` | Every score of `model_evaluation_metrics.json`, as `<model>.<pretrained\|finetuned>.<metric>`: MS2 `cos`, `pcc`, `sa`, `spc`; RT `r2`, `mae_normalized` and the like. |

A value a run's export does not carry is null. CarafeSharp checks the format and every listed entry's
SHA-256 when it opens a file, before it predicts or describes anything, and names what is wrong: a
missing file, one that is not a zip or has no manifest, a format it does not read, or an entry that
does not match its SHA-256.

## Tests

- `LibraryCommandLineTest.TestSavedModel`: a saved model predicts the same spectra as its models from
  their folder, with the command line's precursor window (a test that fails when the training run's
  window is applied instead); the training run's NCE, instrument and rt_max unless given; an MS2
  model that lost to the pretrained one left out; a damaged entry, a file that is not a zip, one
  without a manifest, a newer format and a missing file each refused.
- `FineTuneLoopTest.TestModelTrainerRun`: a training run writes the file, with the model it chose, its
  start model, the run's defaults, and the training description (settings, data, the run's
  instrument, windows, gradient and charges, the held-out metrics), which `-model_info` prints.
- `LibraryCommandLineTest.TestLibraryCommandLine`: `-model` and `-model_info` are parsed, and `-model`
  is refused with `-model_dir`, with training, and without `-db`.
- The chained regression leg (`regression.ps1 -Leg Chained`, [04](04-testing.md)): the model a real
  training run saved predicts a second library with `-model` over a wider precursor window; every
  precursor of the library training predicted is in it, with the same m/z, retention time and
  fragments, and intensities within 1e-5.
