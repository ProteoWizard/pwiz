# Quasar

Spectrum-centric companion to Osprey. Quasar takes the peptide-like MS2 signal that Osprey's
peptide-centric search leaves unexplained, groups co-eluting fragments into deconvoluted,
background-subtracted pseudo-spectra, searches them with semi-tryptic, non-specific and modified
peptides allowed, and writes an FDR-controlled blib that Skyline and Osprey can use directly.

The design is in [docs/00-spec.md](docs/00-spec.md).

## Layout

| Project | Contents |
|---|---|
| `Quasar` | Command-line entry point |
| `Quasar.Core` | Traces, EMG elution modeling (`Elution/`), co-elution clustering |
| `Quasar.Test` | MSTest unit tests |

Quasar references Osprey's projects directly (`Osprey.Chromatography` for the median CWT
consensus, more as later stages need them), and `Quasar.sln` includes them under an `Osprey`
solution folder. Projects for later stages (`Quasar.Features` for Hardklor and Kronik,
`Quasar.SageSharp` for the search, `Quasar.Tasks` for the HPC split) are added when their
stage starts.

## Building

```
build.bat                         Release build and tests
build.bat -Configuration Debug
build.bat -NoTests
```

`build.ps1` uses `dotnet msbuild` and `dotnet test`; every project is plain `net10.0`, so the
same script works on Linux. Warnings are errors in Quasar's projects.

## Test data

The development data is Osprey's HeLa regression set: three Astral runs
(`Ast-2024-12-05_HeLa_3mzDIA_6mIIT_400-900_{49,55,60}.mzML`) and three Stellar runs
(`Ste-2024-12-02_HeLa_4mz_sDIA_400-900_{20,21,22}.mzML`), searched against
`uniprot_human_jan2025_yeastENO1_contam_ADpeps.fasta`.
