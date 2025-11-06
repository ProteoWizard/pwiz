# Skyline LLM Tooling

This directory contains scripts and tools specifically for LLM-assisted development of Skyline.

## Contents

### PRE-COMMIT.md
**Recommended** validation workflow before committing LLM-generated code.

Helps prevent TeamCity failures by running:
- Build validation
- ReSharper code inspection
- CodeInspectionTest

**Quick command**:
```powershell
.\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection
```

### Build-Skyline.ps1
PowerShell script for building, testing, and validating Skyline from LLM-assisted IDEs.

**Usage** (from `pwiz_tools\Skyline`):
```powershell
# Build entire solution (default)
.\ai\Build-Skyline.ps1

# Pre-commit validation (recommended)
.\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection

# Build and run ReSharper inspection
.\ai\Build-Skyline.ps1 -RunInspection

# Build and run unit tests
.\ai\Build-Skyline.ps1 -RunTests

# Build and run specific test
.\ai\Build-Skyline.ps1 -RunTests -TestName CodeInspection

# Build specific project
.\ai\Build-Skyline.ps1 -Target Test

# Clean build
.\ai\Build-Skyline.ps1 -Target Clean
```

### Run-Tests.ps1
PowerShell wrapper for TestRunner.exe with natural language-style test execution.

**Usage** (from `pwiz_tools\Skyline`):
```powershell
# Run CodeInspection test in English
.\ai\Run-Tests.ps1 -TestName CodeInspection

# Run CodeInspection in all languages (en, ja, zh, fr, tr)
.\ai\Run-Tests.ps1 -TestName CodeInspection -Language all

# Run specific test in Japanese
.\ai\Run-Tests.ps1 -TestName TestPanoramaDownloadFile -Language ja

# Run tests from SkylineTester test list in English and Japanese
.\ai\Run-Tests.ps1 -TestName "@SkylineTester test list.txt" -Language en,ja

# Run all tests in Test.dll
.\ai\Run-Tests.ps1 -TestName Test.dll
```

**Intelligent error handling**: If you use a class name by mistake (e.g., "CodeInspectionTest"), the script searches for [TestMethod] annotations and suggests the correct method name.

**See also**: [`ai/docs/build-and-test-guide.md`](../../../ai/docs/build-and-test-guide.md) for comprehensive build/test documentation.

## Purpose

This directory separates **project-specific LLM tooling** from **repository-wide LLM guidance** (located in `/ai/` at repository root).

**Repository-wide** (`/ai/`): Rules, patterns, workflows, style guides  
**Project-specific** (`pwiz_tools/Skyline/ai/`): Build scripts, test helpers, project automation

## Pattern for Other Projects

This structure can be replicated for other projects:
```
pwiz_tools/Skyline/Executables/SkylineBatch/ai/Build-SkylineBatch.ps1
pwiz_tools/Skyline/Executables/AutoQC/ai/Build-AutoQC.ps1
```

Each project's `ai/` directory contains tools specific to building/testing that project.

