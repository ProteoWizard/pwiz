# Build and Test - Comprehensive Guide

Detailed reference for building, testing, and analyzing Skyline from LLM-assisted IDEs.

**Quick reference**: See [../WORKFLOW.md](../WORKFLOW.md) for essential build commands.

## Prerequisites

- Visual Studio 2022 Community/Professional installed
- Initial full build completed with `bs.bat` (Boost.Build + native dependencies)
- Working directory: `C:\proj\pwiz\pwiz_tools\Skyline`

## Quick Start (PowerShell Helper Script)

**Easiest method for LLM-assisted IDEs:**

```powershell
# Build entire solution (DEFAULT - recommended, matches Visual Studio Ctrl+Shift+B)
.\ai\Build-Skyline.ps1

# Pre-commit validation (recommended before committing)
.\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection

# Build, run ReSharper inspection
.\ai\Build-Skyline.ps1 -RunInspection

# Build entire solution and run unit tests
.\ai\Build-Skyline.ps1 -RunTests

# Build and run specific test
.\ai\Build-Skyline.ps1 -RunTests -TestName CodeInspection

# Build specific project only
.\ai\Build-Skyline.ps1 -Target Skyline
.\ai\Build-Skyline.ps1 -Target Test
.\ai\Build-Skyline.ps1 -Target TestFunctional

# Clean build
.\ai\Build-Skyline.ps1 -Target Clean

# Release build (entire solution)
.\ai\Build-Skyline.ps1 -Configuration Release
```

**Script location**: `pwiz_tools\Skyline\ai\Build-Skyline.ps1`  
**Working directory**: `pwiz_tools\Skyline`

**Default behavior**: Builds entire solution (all projects including all test projects). This ensures all compilation errors are detected, matching typical Visual Studio workflow.

This script automatically finds MSBuild, handles paths, and provides clear success/failure output.

## MSBuild Path

Use `vswhere` to find MSBuild dynamically:

```powershell
$vsPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$msbuild = "$vsPath\MSBuild\Current\Bin\amd64\MSBuild.exe"
```

Or use standard path (Community edition):
```
C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe
```

## Iterative Build Commands

**Working directory: `pwiz_tools\Skyline`**

### Build Skyline (Main Application)
```powershell
& $msbuild Skyline.sln /t:Skyline /p:Configuration=Debug /p:Platform=x64 /nologo /verbosity:minimal
```

**Exit code**: `0` = success, non-zero = build failed

**Output**: Errors and warnings only (verbosity:minimal)

### Build Specific Project
```powershell
# Build Test project only
& $msbuild Skyline.sln /t:Test /p:Configuration=Debug /p:Platform=x64 /nologo /verbosity:minimal

# Build TestFunctional project only
& $msbuild Skyline.sln /t:TestFunctional /p:Configuration=Debug /p:Platform=x64 /nologo /verbosity:minimal

# Build multiple projects
& $msbuild Skyline.sln /t:Skyline /t:Test /t:TestFunctional /p:Configuration=Debug /p:Platform=x64 /nologo /verbosity:minimal
```

### Clean Build
```powershell
& $msbuild Skyline.sln /t:Clean /p:Configuration=Debug /p:Platform=x64 /nologo /verbosity:minimal
```

### Rebuild (Clean + Build)
```powershell
& $msbuild Skyline.sln /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /nologo /verbosity:minimal
```

## Running Tests

**Test output directory**: `bin\x64\Debug\`

### Unit Tests (Test.dll - fast, no UI)
```powershell
cd bin\x64\Debug
.\TestRunner.exe log=Test.log buildcheck=1 test=Test.dll
```

**Exit code**: `0` = all tests passed, non-zero = failures

**Output file**: `bin\x64\Debug\Test.log`

### Unit Tests with Data (TestData.dll)
```powershell
.\TestRunner.exe log=TestData.log buildcheck=1 test=TestData.dll
```

### Functional Tests (UI tests - slower)
```powershell
.\TestRunner.exe log=TestFunctional.log buildcheck=1 test=TestFunctional.dll
```

### Run Specific Test
```powershell
.\TestRunner.exe log=MyTest.log buildcheck=1 test=MyTestName
```

## ReSharper Code Inspection

**Using ReSharper Command Line Tools (if installed)**:

```powershell
$jetbrainsHome = $env:LOCALAPPDATA\JetBrains
$inspectCode = "$jetbrainsHome\commandline\inspectcode.exe"  # or specific version like commandline9.0

& $inspectCode Skyline.sln `
    /profile=Skyline.sln.DotSettings `
    /output=InspectCodeOutput.xml `
    /no-swea `
    /no-buildin-settings `
    /properties=Configuration=Debug
```

**Alternative - Use Visual Studio Code Analysis**:
```powershell
& $msbuild Skyline.sln /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /p:RunCodeAnalysis=true /verbosity:minimal
```

## Common Workflows

### Workflow: Code Change → Build → Test
```powershell
# 1. Build affected project
& $msbuild Skyline.sln /t:Skyline /p:Configuration=Debug /p:Platform=x64 /nologo /verbosity:minimal

# 2. Check exit code
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 3. Run relevant tests
cd bin\x64\Debug
.\TestRunner.exe log=Test.log buildcheck=1 test=Test.dll

# 4. Check test results
if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests failed"
    exit $LASTEXITCODE
}

Write-Host "Build and tests passed!"
```

### Workflow: Build → Inspect → Test
```powershell
# 1. Clean build
& $msbuild Skyline.sln /t:Rebuild /p:Configuration=Debug /p:Platform=x64 /nologo /verbosity:minimal

# 2. Static analysis (if ReSharper CLI installed)
if (Test-Path "$env:LOCALAPPDATA\JetBrains\commandline\inspectcode.exe") {
    & "$env:LOCALAPPDATA\JetBrains\commandline\inspectcode.exe" Skyline.sln /profile=Skyline.sln.DotSettings /output=InspectCode.xml
}

# 3. Run tests
cd bin\x64\Debug
.\TestRunner.exe log=Test.log buildcheck=1 test=Test.dll
```

## Output Interpretation

### Build Output (verbosity:minimal)

**Success**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**Errors**:
```
Program.cs(42,15): error CS0103: The name 'foo' does not exist in the current context
Build FAILED.
```

**Warnings**:
```
Program.cs(42,15): warning CS0168: The variable 'x' is declared but never used
Build succeeded.
    1 Warning(s)
```

### Test Output

**Success**:
```
100% tests passed, 0 tests failed out of 42
```

**Failures**:
```
The following tests FAILED:
  1 - MyTest (Failed)
Errors while running CTest
33% tests passed, 2 tests failed out of 3
```

## Configuration Options

### Configuration
- `Debug` - Iterative development (faster compile, easier debugging)
- `Release` - Production builds (optimized, slower compile)

### Platform
- `x64` - 64-bit (standard for Skyline)
- `x86` - 32-bit (legacy)

### Verbosity Levels
- `quiet` - Minimal output
- `minimal` - **Recommended** - Errors and warnings only
- `normal` - Standard output
- `detailed` - Verbose output
- `diagnostic` - Maximum detail

## Troubleshooting

### "MSBuild not found"
Use `vswhere.exe` to locate MSBuild (see Prerequisites section above)

### "Cannot find TestRunner.exe"
TestRunner.exe is in the output directory: `bin\x64\Debug\TestRunner.exe`

### Build succeeds but warnings remain
Skyline requires zero warnings - fix all warnings before committing

### Tests fail with "File not found"
Ensure you're running TestRunner.exe from the output directory (`bin\x64\Debug`)

## Pre-Commit Validation (Recommended)

**Before committing LLM-generated code**, it's recommended to run:

```powershell
.\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection
```

This validates:
- ✅ Code compiles (MSBuild)
- ✅ No ReSharper warnings (inspectcode)
- ✅ CodeInspection test passes

**Exit code 0 = Safe to commit. Non-zero = Fix issues first.**

**Why recommended**: LLMs frequently create code that compiles but triggers ReSharper warnings or fails CodeInspection test, which can cause TeamCity failures.

**See**: [../../pwiz_tools/Skyline/ai/PRE-COMMIT.md](../../pwiz_tools/Skyline/ai/PRE-COMMIT.md) for complete pre-commit workflow documentation.

## See Also

- **[../../pwiz_tools/Skyline/ai/PRE-COMMIT.md](../../pwiz_tools/Skyline/ai/PRE-COMMIT.md)** - Recommended pre-commit validation workflow
- [../WORKFLOW.md](../WORKFLOW.md) - Git workflows and TODO system
- [../TESTING.md](../TESTING.md) - Testing guidelines and patterns
- [../CRITICAL-RULES.md](../CRITICAL-RULES.md) - Critical constraints

