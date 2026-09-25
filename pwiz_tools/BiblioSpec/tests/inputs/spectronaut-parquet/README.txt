Spectronaut "Skyline" parquet export (Biognosys, September 2026) of the SWATH Gold Standard
sample 1 runs (B_D140314_SGSDSsample1_R01..R03_MHRM_T0), cut down to 27 precursors.

The two files keep Spectronaut's exact schema, which is DIA-NN's report-lib.parquet /
report.parquet column set with underscores instead of dots plus these differences:
File_Name for Run, RT_End for RT.Stop, no Fragment_Charge or Flags, Proteotypic and the
results' Q_Value as strings, Decoy as bool, IM NaN in the results. The footer was written by
parquet-cpp-arrow 24 (stock Parquet.Net 4.25.0 cannot parse it). Mods are Unimod titles:
[Carbamidomethyl (C)], [Oxidation (M)], [Acetyl (Protein N-term)].

Precursors were chosen to cover charges 1-4, each mod alone, N-terminal Acetyl merged with
Oxidation and with Carbamidomethyl on residue 1, a non-proteotypic precursor, all three runs,
and two precursors present in the results but not in the library.
