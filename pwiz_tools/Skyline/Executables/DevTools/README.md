# DevTools

Developer-only tools and utilities for the Skyline project. These are not distributed
to end users and are not included in the Skyline installer.

## Categories

### Resource Management
- **AssortResources** - moves single-use resources from shared `.resx` into per-form files
- **SortRESX** - sorts `.resx` files alphabetically to reduce merge conflicts
- **NormalizeResxWhitespace** - normalizes `.resx` formatting (encoding, whitespace)
- **ResourcesOrganizer** - comprehensive resource file management (supersedes KeepResx)
- **Utf16to8** - converts UTF-16 files to UTF-8

### Code Generation
- **UniModCompiler** - generates `UniModData.cs` from UniMod XML database
- **ParseIsotopeAbundancesFromNIST** - generates isotope abundance data from NIST
- **IPItoUniprotMapCompiler** - generates IPI-to-UniProt mapping data

### Tutorial and Documentation
- **DocumentConverter** - converts Skyline documents between formats
- **ImageComparer** - GUI for visual regression testing of tutorial screenshots
- **ImageComparer.Core** - shared image comparison library
- **ImageComparer.Mcp** - MCP server for AI-assisted screenshot comparison
- **ImageConverter** - image format conversion utilities
- **ImageExtractor** - extracts images from documents
- **TutorialLocalization** - aggregates localized tutorial content

### Performance Testing
- **ImportPerf** - benchmarks parallel chromatogram extraction throughput
- **PeakComparison** - compares peak picking across tools (OpenSwath, PeakView, Spectronaut)
