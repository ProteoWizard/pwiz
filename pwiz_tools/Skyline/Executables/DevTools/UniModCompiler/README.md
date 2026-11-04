# UniModCompiler

Code generator that compiles protein modification definitions from UniMod and vendor-specific
modification lists into Skyline's `Model/DocSettings/UniModData.cs` static data class.

## Author

Alana Killeen, MacCoss Lab

## Inputs

- `pwiz/data/common/unimod.obo` - the UniMod snapshot the rest of ProteoWizard reads
  (pwiz-sharp's `Pwiz.Data.Common.Unimod`, Osprey, BiblioSpec, the mzML CV list). It is the
  source of record for which modifications exist and what they are: ids, titles, compositions,
  specificities and neutral losses. `UniModOboReader.cs` reads it; the composition bricks
  (Hex, HexNAc, ...) and UniMod's element order, which the obo does not spell out, are held there.
- `InputFiles/get_params.pl.txt` - Mascot's modification list (http://www.matrixscience.com/cgi/get_params.pl).
  An overlay: its `[MODS]` entries are the modifications Skyline shows, its `[HIDDEN_MODS]` the
  hidden ones, and its names group the sites ("Phospho (ST)").
- `InputFiles/ProteinPilot.DataDictionary.xml` - an overlay of ProteinPilot three-letter short names.

Every obo modification no Mascot entry matches is still written, hidden, named the way Mascot
names its own (one entry per UniMod site group, one per terminal specificity), so Skyline can name
every UniMod id. Protein-terminal specificities have no Skyline equivalent and are skipped; an obo
id left with nothing to write is listed in a trailing `//Not in table:` comment.

## Usage

```
pwsh -File ai/scripts/Skyline/Build-Skyline.ps1 -Target UniModCompiler
pwiz_tools/Skyline/Executables/DevTools/UniModCompiler/bin/Debug/net10.0-windows/UniModCompiler.exe
```

The tool takes no arguments; it finds the inputs and the output from its own location and
overwrites `pwiz_tools/Skyline/Model/DocSettings/UniModData.cs`. Review the diff, then run
`TestUniMod` twice (the first run rewrites `Test/UniModStaticList.xml` and `Test/UniModHeavyList.xml`)
and `TestUniModAgreesWithObo`.

## When to Run

In the same commit as every `unimod.obo` update (the CV update procedure in `build/CvGen/Program.cs`).
`TestUniModAgreesWithObo` holds `UniModData.cs` to the obo: every entry's id, six-decimal
monoisotopic mass, residues and terminus, and every obo id the table lacks must be on its committed
exclusion list with a reason. Run it also when the Mascot or ProteinPilot lists change.
