# 08. Command line

> Reference doc. The option tables are in the build-generated help (`CarafeSharp -h`,
> archived at `../Documentation/Help/en/CommandLine.html`), which is generated from the
> declarations in `CarafeSharp.Proteome/CarafeCommandLine.cs` and the argument text in
> `CarafeCommandArgUsage.resx`, so it never drifts from the binary. This doc covers what the
> help cannot: the grammar, the modes, and how CarafeSharp's command line relates to Carafe's.

CarafeSharp takes Carafe's own command lines. The reason is parity: CarafeSharp is a
cross-implementation port of [Carafe](https://github.com/maccoss/carafe) 2.2.0, and the parity
tests feed it the exact command lines of recorded Carafe runs (`CarafeReferenceRun` reads the
`Command line:` of a run's `parameter.txt`). Every Carafe 2.2.0 option is therefore declared,
whether or not these modes read it.

```
CarafeSharp --build_entrapment_fasta <out.fasta> --db <proteins.fasta> [options]
CarafeSharp --reconcile_manifest <out.tsv> --manifest <in.tsv> --predicted_library <library.tsv|.blib>
CarafeSharp --db <peptides.fasta|proteins.fasta> -o <folder> [options]            (library prediction)
CarafeSharp -i <osprey.blib> --ms <runs> -o <folder> [--db <fasta>] [options]     (training)
CarafeSharp -i <run.training.parquet|folder> [--ms <runs>] -o <folder> [--db <fasta>] [options]
CarafeSharp --model_info <file.carafemodel>
CarafeSharp -h [ascii|unicode|sections|html|<Section>]
```

The help and this doc spell options the standard pwiz_tools way, `--name`, as Osprey's help does
(cross-tool consistency within pwiz); Carafe's `-name` is accepted for every option (cross-impl
compatibility with Noble-Lab/Carafe), and the help ends with a paragraph saying so. The option
names themselves stay Carafe's (`--lf_type`, `--miss_c`), because they are the names a Carafe
user and a recorded Carafe run know.

---

## Grammar

The parser is the shared one in `pwiz_tools/Shared/CommonUtil/CommandLine`
(`CommandLineParser<TContext>`), which Skyline and Osprey parse with too. What differs per tool
is the grammar it installs (`CommandLineGrammar`), never the parser. CarafeSharp's:

| Rule | CarafeSharp (Carafe's grammar) | For comparison: Osprey | Skyline |
|---|---|---|---|
| Option spelling | `--name` as shown; Carafe's `-name` accepted for every option | `--name`, short aliases `-x` | `--name` only |
| Value | `--name value` or `--name=value` | `--name value` or `--name=value` | `--name=value` only |
| A dash-prefixed next token | a value unless it names a declared option (`-seed -5`, `-mode -`) | always the next option (`-o -l x` fails fast) | never a value |
| Repeated option | the first value wins | the last value wins | the last value wins |
| Unknown option | error: `Unrecognized option: -x` | error | error |
| A bare word | ignored, as Carafe ignores it | an input file if it exists, else reported | ignored |
| A flag given a value (`-fast=true`) | error | error | error |
| A value outside the declared list | the settings decide, with Carafe's message | the handler decides | error |

Both helps end the same way: a paragraph on the other forms the parser accepts (Osprey:
`--name=value` and a bare input file; CarafeSharp: `--name=value` and Carafe's single dash).

`-h` is Carafe's help flag and Osprey's; `-help` and `--help` are the same declaration. Its
optional value picks the form: `ascii` or `unicode` (default) tables, `sections` (the section
names), `html` (the page archived under `Documentation/Help`), or a section name for that
section alone.

A usage error prints the message and `Run CarafeSharp --help for the options.`, with exit code 1.
The messages the settings raise still spell options Carafe's way (`-db`, `-rt_reference`); they
are literal text, and move to the standard spelling with the deferred resx extraction.

## Modes

The mode is decided from the options given, in Carafe's order of precedence:

| Given | Mode | Runs |
|---|---|---|
| nothing, or `-h` | help | prints the usage |
| `-build_entrapment_fasta` | entrapment FASTA build | `EntrapmentFastaBuilder`: digest `-db`, add decoys and entrapment, write the pairing manifest of `-manifest` |
| `-model_info` | model info | describes a saved `.carafemodel` |
| `-reconcile_manifest` | manifest reconciliation | `PairingManifestReconciler` against `-predicted_library` |
| `-ms`, or `-i` naming training exports, without `-model_dir` | training | `ModelTrainer` on Osprey's `.training.parquet` exports, then the library of `-db` with the fine-tuned models when `-db` is given |
| `-db` | library prediction | `LibraryGenerator` with the pretrained models, a `-model_dir` folder or a saved `-model` |

Carafe trains only with `-ms`. CarafeSharp also trains on Osprey's training exports named by
`-i` directly (one file, a comma-separated list, or a folder), which Carafe could never have
read; `-i` with a `.blib` and no `-ms` is library prediction, as in Carafe.

## Carafe options CarafeSharp does not read

The help's last section lists them. Each is accepted so a Carafe command line runs unchanged:

- XIC extraction (`-itol`, `-itolu`, `-sg`, `-min_mz`, `-rf`, `-rf_rt_win`, `-rt_win_offset`):
  reported and ignored; the fragment matches, XICs and peak boundaries come from Osprey's
  training export (`pwiz_tools/Osprey/docs/22-training-export.md`).
- Diagnostic files (`-ez`, `-xic`, `-export_mgf`, `-skyline`): reported and ignored.
- Checked for the one value supported: `-se Osprey`, `-mode general`, `-ai_version v2`,
  `-na 0`; `-lf_format parquet` and `-lf_type mzSpecLib` are refused.
- Not ported, refused: `-printPTM`, `-build_koina_library`, `-mod2mass`, `-user_var_mods`,
  and for training `-cs`, `-use_all_peaks`, `-y1`.
- Silently ignored, as Carafe ignores them in these modes: `-ptm_site_prob`,
  `-ptm_site_qvalue`, `-data_type`, `-koina_url`, `-koina_ms2_model`, `-koina_rt_model`,
  `-nce_ms`, `-python`, `-verbose`, `-nm`, `-torch_compile`.

## CarafeSharp's own options

Beyond Carafe's: `-pretrained`, `-model`, `-model_info`, `-activation`, `-analyzer`,
`-rt_model`, `-rt_align`, `-rt_select` and `-rt_reference`, and the output options every mode
reads, `-log_file`, `-timestamp` and `-memstamp` (Osprey's `--log-file`, `--timestamp` and
`--memstamp`). Their semantics are in
`01-model-spec.md`, `06-saved-models.md` and `07-chronologer.md`; the help has the one-line
version of each.

## Adding or changing an option

1. Declare it in `CarafeCommandLine.cs` with `Value`, `Choice` or `Flag`, and put it in a group.
2. Add its usage text to `CarafeCommandArgUsage.resx` under `_<name>`.
3. Read it in `Interpret` (or the settings method of its mode) through `Has` / `TryGet`.
4. Run `CarafeSharp.Test`: `CommandArgsTest` fails on an option that is undeclared, ungrouped
   or undescribed, and regenerates `Documentation/Help/en/CommandLine.html` for you to commit.
