# BuildMethod

Suite of instrument-specific method builders that convert Skyline-generated transition
lists into vendor-specific instrument acquisition method files. Each subdirectory targets
a different instrument platform.

## Author

Brendan MacLean, MacCoss Lab

## Tools

| Tool | Instrument Platform |
|------|-------------------|
| BuildAgilentMethod | Agilent 6400 series QQQ |
| BuildAnalystMethod | Sciex Analyst (legacy) |
| BuildAnalystFullScanMethod | Sciex Analyst full-scan |
| BuildBrukerMethod | Bruker instruments |
| BuildLTQMethod | Thermo LTQ (legacy) |
| BuildQTRAPMethod | Sciex QTRAP |
| BuildShimadzuMethod | Shimadzu instruments |
| BuildThermoMethod | Thermo instruments |
| BuildWatersMethod | Waters Xevo/Quattro Premier |

## Usage

Each tool follows the same general pattern:

```
BuildXyzMethod [options] <template_method_file> [transition_list_files]*
```

Input: A template method file from the instrument vendor software plus one or more CSV
transition list files exported from Skyline.

Output: A ready-to-use instrument method file.

Supports reading transition lists from stdin for pipeline integration.

## Dependencies

Each tool depends on the corresponding vendor SDK being installed on the build machine.
These are Windows-only tools tied to specific vendor library versions.
