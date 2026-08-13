# UniModCompiler

Code generator that compiles protein modification definitions from the UniMod XML database
and vendor-specific modification lists into Skyline's `UniModData.cs` static data class.

## Author

Alana Killeen, MacCoss Lab

## Usage

Run as a standalone console application (no arguments). Reads from input files in the
`InputFiles/` directory:

- `unimod.xml` - from [unimod.org](http://www.unimod.org)
- `ProteinPilot.DataDictionary.xml` - vendor modification data
- Various modification list text files

Outputs `UniModData.cs`, which should be copied into the Skyline source tree.

## When to Run

Run this tool when the UniMod database is updated or when new vendor-specific modifications
need to be incorporated. This is infrequent - typically once every few years.
