# Build and Test - Comprehensive Guide

Detailed reference for building, testing, and analyzing Skyline from LLM-assisted IDEs.

**Quick reference**: See [../WORKFLOW.md](../WORKFLOW.md) for essential build commands.

## Prerequisites

- Visual Studio 2022 Community/Professional installed
- Initial full build completed with `bs.bat` (Boost.Build + native dependencies)

## ⚠️ CRITICAL: Never Call MSBuild Directly

**All builds MUST use the `Build-*.ps1` scripts**, never call `msbuild.exe` directly:

- ✅ `.\pwiz_tools\Skyline\ai\Build-Skyline.ps1`
- ✅ `.\pwiz_tools\Skyline\Executables\SkylineBatch\ai\Build-SkylineBatch.ps1`
- ✅ `.\pwiz_tools\Skyline\Executables\AutoQC\ai\Build-AutoQC.ps1`
- ❌ `msbuild.exe Skyline.sln ...` (NEVER DO THIS)

**Why**: Build scripts handle working directory changes, find MSBuild automatically, fix line endings, and provide consistent output formatting.

**Scripts work from any working directory** - they automatically change to the correct project directory, so you don't need to `Set-Location` first.

## Quick Start (PowerShell Helper Script)

### Main Skyline Application

```powershell
# Build entire solution (DEFAULT - recommended, matches Visual Studio Ctrl+Shift+B)
# Works from ANY working directory - no need to cd first
.\pwiz_tools\Skyline\ai\Build-Skyline.ps1

# Pre-commit validation (recommended before committing)
.\pwiz_tools\Skyline\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection

# Build, run ReSharper inspection
.\pwiz_tools\Skyline\ai\Build-Skyline.ps1 -RunInspection

# Build entire solution and run unit tests
.\pwiz_tools\Skyline\ai\Build-Skyline.ps1 -RunTests

# Build and run specific test
.\pwiz_tools\Skyline\ai\Build-Skyline.ps1 -RunTests -TestName CodeInspection

# Build specific project only
.\pwiz_tools\Skyline\ai\Build-Skyline.ps1 -Target Skyline
.\pwiz_tools\Skyline\ai\Build-Skyline.ps1 -Target Test
.\pwiz_tools\Skyline\ai\Build-Skyline.ps1 -Target TestFunctional

# Clean build
.\pwiz_tools\Skyline\ai\Build-Skyline.ps1 -Target Clean

# Release build (entire solution)
.\pwiz_tools\Skyline\ai\Build-Skyline.ps1 -Configuration Release
```

### SkylineBatch

```powershell
# Build solution (works from any directory)
.\pwiz_tools\Skyline\Executables\SkylineBatch\ai\Build-SkylineBatch.ps1

# Build and run tests
.\pwiz_tools\Skyline\Executables\SkylineBatch\ai\Build-SkylineBatch.ps1 -RunTests

# Build, inspect, and test
.\pwiz_tools\Skyline\Executables\SkylineBatch\ai\Build-SkylineBatch.ps1 -RunInspection -RunTests

# Release build
.\pwiz_tools\Skyline\Executables\SkylineBatch\ai\Build-SkylineBatch.ps1 -Configuration Release
```

### AutoQC

```powershell
# Build solution (works from any directory)
.\pwiz_tools\Skyline\Executables\AutoQC\ai\Build-AutoQC.ps1

# Build and run tests
.\pwiz_tools\Skyline\Executables\AutoQC\ai\Build-AutoQC.ps1 -RunTests

# Build, inspect, and test
.\pwiz_tools\Skyline\Executables\AutoQC\ai\Build-AutoQC.ps1 -RunInspection -RunTests
```

**Key Features**:
- **Works from any directory** - scripts auto-change to correct project directory
- **Finds MSBuild automatically** using vswhere
- **Fixes line endings** in modified files (CRLF standard)
- **Clear success/failure output** with exit codes
- **Default behavior**: Builds entire solution (all projects including tests)

This ensures all compilation errors are detected, matching typical Visual Studio workflow.

> ⚠️ **Tests always run the previously compiled binaries.** Run one of the build commands above after every edit and before invoking any test command.

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

## How Build Scripts Work

All `Build-*.ps1` scripts follow this pattern:

1. **Auto-locate script directory**: `$scriptRoot = Split-Path -Parent $PSCommandPath`
2. **Change to project directory**: `Set-Location $projectRoot`
3. **Find MSBuild**: Use `vswhere.exe` to locate Visual Studio installation
4. **Fix line endings**: Run `fix-crlf.ps1` on modified files
5. **Build**: Invoke MSBuild with appropriate parameters
6. **Restore location**: Return to original directory in `finally` block

This means **you can call the script from anywhere** without manually changing directories first.

**Exit codes**: `0` = success, non-zero = build failed

## Running Tests

> ⚠️ **Always build first.** Calling `Run-Tests.ps1` without a prior build simply reuses the last binaries in `bin\x64\Debug\`.

**Test output directory**: `bin\x64\Debug\`

### Unit Tests (Test.dll - fast, no UI)
```powershell
pwiz_tools\Skyline\ai\Run-Tests.ps1 -TestName Test.dll
```

### Unit Tests with Data (TestData.dll)
```powershell
pwiz_tools\Skyline\ai\Run-Tests.ps1 -TestName TestData.dll
```

### Functional Tests (UI tests - slower)
```powershell
pwiz_tools\Skyline\ai\Run-Tests.ps1 -TestName TestFunctional.dll
```

### Run Specific Test
```powershell
# Run single [TestMethod] from FastaImporterTest.cs (use the method name, not the file name)
pwiz_tools\Skyline\ai\Run-Tests.ps1 -TestName TestFastaImport
```

> ℹ️ **Tip:** If you only know the `.cs` file name, open it and search for `[TestMethod]` to get the method names. Passing the file name (e.g. `FastaImporterTest.cs`) will not work—the script passes method names through to TestRunner.

### Run Multiple Tests / Languages
```powershell
# Comma-separate test method names and languages.
# Example: run all FastaImporter tests in English, Chinese, and French.
pwiz_tools\Skyline\ai\Run-Tests.ps1 `
    -TestName "TestBasicFastaImport,TestFastaImport,WebTestFastaImport" `
    -Language "en,zh-CHS,fr-FR"
```

> ℹ️ **Tip:** Pass `-Language` to exercise multiple UI languages; omit it for the default English run.

## AI/SkylineTester Integration

Run-Tests.ps1 and SkylineTester share a test list file (`SkylineTester test list.txt`) for bidirectional workflow. This enables seamless handoff between human-driven and LLM-driven test execution.

### Using the Shared Test List

```powershell
# Run tests that developer selected in SkylineTester
pwiz_tools\Skyline\ai\Run-Tests.ps1 -UseTestList

# Run tests from SkylineTester list in specific language(s)
pwiz_tools\Skyline\ai\Run-Tests.ps1 -UseTestList -Language ja
pwiz_tools\Skyline\ai\Run-Tests.ps1 -UseTestList -Language fr,tr

# Update test list and run (developer will see tests pre-checked in SkylineTester)
pwiz_tools\Skyline\ai\Run-Tests.ps1 -TestName "TestA,TestB,TestC" -UpdateTestList
```

### Integration Workflows

**Human → LLM Handoff:**
1. Developer selects tests in SkylineTester UI (Tests tab)
2. LLM runs: `Run-Tests.ps1 -UseTestList`
3. Same tests run without re-specifying

**LLM → Human Handoff:**
1. LLM runs: `Run-Tests.ps1 -TestName "TestA,TestB" -UpdateTestList`
2. Developer opens SkylineTester → tests automatically checked
3. Developer can review, modify, or re-run

**Sprint Test Set Management:**
1. Developer curates test set for current work in SkylineTester
2. LLM runs `Run-Tests.ps1 -UseTestList` throughout development
3. Test set persists across SkylineTester restarts and LLM sessions

**Failed Tests Workflow:**
1. Developer runs tests in SkylineTester, some fail
2. Developer clicks "Check Failed Tests" button
3. Developer closes SkylineTester, makes fixes
4. Developer reopens SkylineTester → failed tests still checked (auto-restored)
5. LLM can run same failed tests: `Run-Tests.ps1 -UseTestList`

### Test List File Format

**Location:** `pwiz_tools\Skyline\SkylineTester test list.txt`

```
# SkylineTester test list
# One test name per line
CodeInspection
TestPanoramaDownloadFile
TestProteomeDb
```

Lines starting with `#` are comments. When `-UpdateTestList` is used, the existing file is backed up with a timestamp before being overwritten.

## Code Coverage Analysis

Skyline uses JetBrains dotCover for code coverage tool integrated with ReSharper, to measure test coverage.

### Running Tests with Coverage

```powershell
# Run tests with code coverage enabled
pwiz_tools\Skyline\ai\Run-Tests.ps1 -TestName TestFilesTreeForm -Coverage

# Coverage output defaults to: ai\.tmp\coverage-{timestamp}.json
# Custom output path:
pwiz_tools\Skyline\ai\Run-Tests.ps1 -TestName TestFilesTreeForm -Coverage -CoverageOutputPath ".\coverage.json"
```

**Coverage Output**:
- Coverage snapshot (`.dcvr`) is created in the test output directory (`bin\x64\Debug\`)
- JSON report is exported to `ai\.tmp\coverage-{timestamp}.json` by default
- JSON can be analyzed programmatically or opened in Visual Studio (ReSharper > Unit Tests > Coverage)

### dotCover Installation

**Setup**: See `ai/docs/developer-setup-guide.md` for installation instructions.

The `Run-Tests.ps1` script automatically searches for `dotCover.exe` in these locations:

1. **Libraries directory** (recommended):
   ```
   libraries\jetbrains.dotcover.commandlinetools\{version}\tools\dotCover.exe
   ```

2. **.NET global tools** (if installed via `dotnet tool install --global JetBrains.dotCover.CommandLineTools`):
   ```
   %USERPROFILE%\.dotnet\tools\dotCover.exe
   ```

3. **Other common locations**: Downloads folder, user tools directory, etc.

**Note**: dotCover 2025.3.0.2 has a known bug with JSON export. If JSON export fails, use version 2025.1.7 or earlier.

### Analyzing Coverage Results

Use the `Analyze-Coverage.ps1` script to analyze coverage for specific code patterns:

```powershell
# Analyze coverage for Files view feature
pwsh -File pwiz_tools/Skyline/ai/scripts/Analyze-Coverage.ps1 `
  -CoverageJsonPath "ai/.tmp/coverage-20251207-224957.json" `
  -PatternsFile "ai/.tmp/coverage-patterns-filesview.txt" `
  -ReportTitle "FILES VIEW CODE COVERAGE"

# Or use an array of patterns directly
pwsh -File pwiz_tools/Skyline/ai/scripts/Analyze-Coverage.ps1 `
  -CoverageJsonPath "ai/.tmp/coverage-20251207-224957.json" `
  -Patterns "FilesTree","FileModel","FileSystemService" `
  -ReportTitle "CUSTOM COVERAGE ANALYSIS"
```

The script produces a summary report showing:
- **Overall coverage percentage** for matched types
- **Coverage by type** (covered/total statements, percentage)
- **Low coverage warnings** (types < 50%)
- **Uncovered items** (types with 0% coverage)

**Pattern Files**: Create a text file in `ai\.tmp` with one type name pattern per line (wildcards supported):
```
# Coverage patterns for Files view feature
FilesTree
FilesTreeNode
FilesTreeForm
FileModel
FileSystemService
```

**Example workflow**:
1. Run tests with `-Coverage` flag
2. Analyze with `Analyze-Coverage.ps1` to identify gaps
3. Add tests to improve coverage
4. Re-run with coverage to verify improvement

**Visual Studio Integration**: Open the `.dcvr` snapshot file in Visual Studio:
- ReSharper > Unit Tests > Coverage > Import from Snapshot
- View coverage highlights in source code
- Export to HTML or JSON reports

### Coverage Best Practices

- **Run coverage on feature-specific tests** to focus analysis on new code
- **Aim for >80% coverage** on new features (higher for critical paths)
- **Review zero-coverage items** - these indicate untested code paths
- **Use coverage to identify** missing edge case tests
- **Don't obsess over 100%** - some code paths may be impractical to test

**Example**: Analyzing Files view feature coverage:
```powershell
# Run Files view tests with coverage
pwiz_tools\Skyline\ai\Run-Tests.ps1 -TestName "TestFilesTreeForm,TestFilesModel,TestFilesTreeFileSystem" -Coverage

# Coverage JSON saved to: ai\.tmp\coverage-{timestamp}.json
# Review coverage for FilesTree, FilesModel, and related types
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

### Handling Intermittent Test Failures

**Critical Rule**: Never commit, push, or merge code without proving tests can pass reliably.

When a test failure is believed to be intermittent or pre-existing:

1. **Re-run the specific failing test** to verify it's truly intermittent:
   ```powershell
   .\pwiz_tools\Skyline\ai\Run-Tests.ps1 -TestName FailingTestName
   ```

2. **If the test fails consistently**, it is NOT intermittent—your changes may have made it repeatable, or the "known issue" memory is incorrect.

3. **If the test passes on re-run**, document the intermittent behavior (frequency, suspected cause) and consider:
   - Filing an issue to fix the flakiness
   - Adding retry logic or better test isolation
   - Running multiple iterations to establish failure rate

4. **Document pre-existing failures** in the PR description or commit message if proceeding with known failing tests, but only after confirming they fail on `master` branch as well.

**Rationale**: "I thought it was intermittent" or "I remember it failing before" is not sufficient due diligence. Test failures must be reproducible and understood before being dismissed.

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

### "The term '.\ai\Build-*.ps1' is not recognized" or "cannot open file"

**Symptom**: Build script fails with PowerShell errors about not recognizing the command or file not found.

**Root cause**: You're calling the script with an incorrect path or execution policy issue.

**Solution**: Use the **full relative path from repo root**:
```powershell
# ✅ CORRECT - Works from any directory
.\pwiz_tools\Skyline\ai\Build-Skyline.ps1
.\pwiz_tools\Skyline\Executables\SkylineBatch\ai\Build-SkylineBatch.ps1
.\pwiz_tools\Skyline\Executables\AutoQC\ai\Build-AutoQC.ps1

# ❌ WRONG - These patterns will fail
./ai/Build-Skyline.ps1                    # Wrong directory
c:\proj\...\Build-Skyline.ps1            # Absolute paths fail
& "path\to\script"                        # Call operator fails with RemoteSigned policy
```

**Note**: The scripts automatically change to the correct project directory, so you don't need to `Set-Location` first.

### "MSBuild not found"
Use `vswhere.exe` to locate MSBuild (installed with Visual Studio 2022). The Build scripts do this automatically.

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

