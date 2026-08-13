# ParseIsotopeAbundancesFromNIST

Code generator that parses NIST isotope abundance data and produces a C# code snippet
for use in `pwiz_tools/Shared/Common/Chemistry/IsotopeAbundances.cs`.

## Author

Brian Pratt, MacCoss Lab (2019)

## Usage

```
ParseIsotopeAbundancesFromNIST <input_file> <output_file>
```

The input file should contain text from the NIST Atomic Weights and Isotopic Compositions
database: https://physics.nist.gov/cgi-bin/Compositions/stand_alone.pl

An example input file is checked into the `Inputs/` folder.

The output file contains a C# code snippet that replaces the isotope abundance data in
`IsotopeAbundances.cs`.

## When to Run

Only needed when NIST updates their isotope abundance data, which is very infrequent.
The tool handles edge cases like elements with no reported isotopic composition by falling
back to standard atomic weight or the median reported mass.
