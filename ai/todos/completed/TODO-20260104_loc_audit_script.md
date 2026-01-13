# TODO-20260104_loc_audit_script.md

## Branch Information
- **Branch**: `ai-context` (documentation work)
- **Base**: `ai-context`
- **Created**: 2026-01-04
- **Completed**: 2026-01-04
- **Status**: Done
- **PR**: N/A (ai-context branch)
- **Objective**: Create a lines-of-code audit script that accurately counts team-written code, excluding third-party/vendored libraries

## Background

User wanted to replace manual LineCounter usage with an automated script. Historical claims:
- >900K LOC for Skyline.sln
- ~190K LOC for testing

Investigation revealed the historical numbers used LOSC (Lines of Source Code) while cloc counts LOEC (Lines of Executable Code) - roughly a 2.2x difference.

## Completed Work

### 1. Installed cloc
- [x] Installed via `winget install AlDanial.Cloc`
- cloc is the industry-standard tool for counting lines of code

### 2. Created audit-loc.ps1
- [x] Created `ai/scripts/audit-loc.ps1`
- [x] Renamed old `audit-testdata.ps1` to `audit-skyline-testdata.ps1`

### 3. Identified third-party/vendored code to exclude
- [x] ZedGraph (pwiz_tools/Shared/zedgraph/) - charting library (~23K)
- [x] alglib (pwiz_tools/Shared/Common/DataAnalysis/alglib/) - math library (~163K)
- [x] Hardklor (pwiz_tools/Skyline/Executables/Hardklor/) - git submodule (~234K, includes sqlite)
- [x] BullseyeSharp (pwiz_tools/Skyline/Executables/BullseyeSharp/) - git submodule (~1K)

### 4. Fixed CSV parsing issue
- [x] Discovered .NET auto-generated files in obj/ folders have commas in path names
- [x] Fixed by adding `--exclude-dir=obj,bin,TestResults` to cloc command

### 5. Added C# Designer files
- [x] Discovered cloc treats .Designer.cs as separate "C# Designer" language
- [x] Added to language filter (~90K lines were being missed)

### 6. Added BiblioSpec to Shared
- [x] BiblioSpec is integral to Skyline (library builder)
- [x] Moved from OtherTools to Shared category

### 7. Reorganized categories
- [x] Separated Bumbershoot from Pwiz Tools (not team-maintained)
- [x] Created clear hierarchy:
  - **Skyline** (team-maintained): Core, Shared, Tests, Executables
  - **ProteoWizard** (team-maintained): C++ Core, Pwiz Tools
  - **Bumbershoot** (not team-maintained): Legacy Vanderbilt tools
  - **Third-party** (excluded): ZedGraph, alglib, Hardklor, BullseyeSharp

### 8. Added drilldown mode
- [x] `-Drilldown` parameter shows subdirectory breakdown
- [x] Useful for understanding where code lives within each category

## Final Results

```
Skyline (team-maintained):
  Core (main app)                          406,233 lines
  Shared Libraries                          84,960 lines
  Tests                                    158,467 lines
  Executables                               58,725 lines
  SKYLINE TOTAL                            708,385 lines

ProteoWizard (team-maintained):
  C++ Core (pwiz/)                         208,262 lines
  Pwiz Tools                                29,528 lines
  PROTEOWIZARD TOTAL                       237,790 lines

TEAM TOTAL                                 946,175 lines

Not team-maintained:
  Bumbershoot                              190,785 lines

Third-party/Vendored (excluded):           420,142 lines
```

## Usage

```powershell
# Summary mode
pwsh -Command "& './ai/scripts/audit-loc.ps1'"

# Drilldown mode (shows subdirectory breakdown)
pwsh -Command "& './ai/scripts/audit-loc.ps1' -Drilldown"
```

Reports saved to `ai/.tmp/loc-audit-YYYYMMDD-HHMM.md`

## Key Insights

1. **LOSC vs LOEC**: Historical 900K claim used LOSC (Lines of Source Code). cloc counts LOEC (executable lines). Ratio is ~2.2x.

2. **Bumbershoot history**: Legacy proteomics tools from David Tabb's lab at Vanderbilt, mostly Matt Chambers' work from 2007+. Not actively team-maintained.

3. **Hardklor**: Mike Hoopman's work, uses separate data access layer (MSToolkit), properly excluded as third-party.

## Future Enhancements (optional)

- [ ] Create `audit-staticdata.ps1` for XML/CSV/JSON data files
- [ ] Break down Executables into Product/DevTools/Tools subcategories
