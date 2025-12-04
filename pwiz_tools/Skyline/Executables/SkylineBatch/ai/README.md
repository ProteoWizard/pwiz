# SkylineBatch LLM Tooling

Scripts and tools for LLM-assisted development of SkylineBatch.

## Contents

### Build-SkylineBatch.ps1
PowerShell script for building and testing SkylineBatch from LLM-assisted IDEs.

**Usage** (works from any working directory):
```powershell
# Build solution
.\pwiz_tools\Skyline\Executables\SkylineBatch\ai\Build-SkylineBatch.ps1

# Build and run tests
.\pwiz_tools\Skyline\Executables\SkylineBatch\ai\Build-SkylineBatch.ps1 -RunTests

# Build, inspect, and test
.\pwiz_tools\Skyline\Executables\SkylineBatch\ai\Build-SkylineBatch.ps1 -RunInspection -RunTests

# Release build
.\pwiz_tools\Skyline\Executables\SkylineBatch\ai\Build-SkylineBatch.ps1 -Configuration Release
```

**Key feature**: Script automatically changes to the correct project directory, so you don't need to `Set-Location` first. This makes it easier to use from LLM-assisted IDEs where the current working directory may not be transparent.

## Differences from Skyline

SkylineBatch uses **standard MSTest** for testing:
- Tests run with `vstest.console.exe` (Visual Studio Test Platform)
- No multi-language support (English only)
- No custom test runner infrastructure
- Simpler test execution model

## Purpose

This directory follows the pattern established in `pwiz_tools/Skyline/ai/`:
- **Project-specific tooling** lives in project's `ai/` directory
- **Repository-wide guidance** lives in `/ai/` at repository root

See [../../../../ai/docs/documentation-maintenance.md](../../../../ai/docs/documentation-maintenance.md) for the full LLM documentation system architecture.

