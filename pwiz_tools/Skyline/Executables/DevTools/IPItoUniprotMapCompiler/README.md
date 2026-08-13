# IPItoUniprotMapCompiler

Code generator that compiles IPI (International Protein Index) to UniProt accession number
mappings into a compressed data file and C# class used by Skyline's FASTA importer.

## Author

Brian Pratt, MacCoss Lab (2014)

## Background

IPI was a widely-used protein database that was deprecated in 2011 in favor of UniProt.
Many existing FASTA files still use IPI accession numbers. This tool generates the mapping
data that allows Skyline to resolve IPI accessions to their UniProt equivalents during
FASTA import.

## Usage

Run as a standalone console application (no arguments). Reads the mapping file from
`InputFiles/last-UniProtKB2IPI.zip` (originally from `ftp.uniprot.org`) and generates:

- `Shared/ProteomeDb/Fasta/IpiToUniprotMap.cs` - C# class with segment constants
- `Shared/ProteomeDb/Fasta/IpiToUniprotMap.zip` - compressed mapping data

## When to Run

This is a one-time code generator. The IPI database is no longer updated, so the mapping
is fixed. Only re-run if the output format or segmentation strategy needs to change.
