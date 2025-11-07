# Pre-Commit Validation Workflow

**Recommended** validation steps before committing LLM-generated code. These checks help prevent TeamCity failures.

## Why This Matters

LLMs frequently create code that:
- ✅ Compiles successfully (passes MSBuild)
- ❌ Triggers ReSharper warnings (unused usings, naming violations, etc.)
- ❌ Fails CodeInspection test
- ❌ Causes TeamCity tests to fail

**TeamCity requires zero warnings and all tests passing.**

## Pre-Commit Command

From `pwiz_tools\Skyline`, run:

```powershell
.\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection
```

This single command performs all recommended validation:
1. ✅ Builds entire solution
2. ✅ Runs ReSharper code inspection
3. ✅ Runs Code Inspection Test

**Exit code 0 = Safe to commit. Non-zero = Fix issues first.**

## Individual Validation Steps

If you need to run checks separately:

### 1. Build
```powershell
.\ai\Build-Skyline.ps1
```
- Compiles all projects
- Detects syntax errors, missing references

### 2. ReSharper Code Inspection
```powershell
.\ai\Build-Skyline.ps1 -RunInspection
```
- Detects unused using directives
- Finds naming convention violations
- Identifies potential bugs (null references, etc.)
- Validates localization (string literals that should be resources)

### 3. CodeInspection Test
```powershell
.\ai\Build-Skyline.ps1 -RunTests -TestName CodeInspection
```
- Validates all ReSharper inspection results
- Ensures no new warnings introduced
- Must pass before any commit

### 4. All Unit Tests (Optional but Recommended)
```powershell
.\ai\Build-Skyline.ps1 -RunTests
```
- Runs all unit tests in Test.dll
- Catches logic errors and regressions

## Common ReSharper Issues from LLMs

### 1. Unused Using Directives
```csharp
using System.Net;  // Not used anywhere in file - ReSharper warning
```
**Fix**: Remove unused using directives

### 2. Naming Convention Violations
```csharp
private int MyField;  // Should be _myField
```
**Fix**: Follow naming conventions in `ai/STYLEGUIDE.md`

### 3. String Literals (Should Be Resources)
```csharp
MessageBox.Show("File not found");  // Should use resource string
```
**Fix**: Move to .resx file, reference as `Resources.ErrorMessage_FileNotFound`

### 4. Redundant Code
```csharp
if (condition == true)  // == true is redundant
```
**Fix**: Simplify to `if (condition)`

## Setup Requirements

### ReSharper Command-Line Tools

**Required** for `-RunInspection` to work.

**Installation** (one-time setup):
```powershell
dotnet tool install -g JetBrains.ReSharper.GlobalTools
```

This installs the `jb` command as a .NET global tool. Verify installation:
```powershell
jb --version
# Should output: JetBrains ReSharper Global Tools 2025.2.x
```

**Documentation**: https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html

**Note**: The script will gracefully skip inspection if not installed, but it's **strongly recommended** for pre-commit validation to help prevent TeamCity failures.

## Workflow Integration

### Before Any Commit
```powershell
# 1. Make code changes (with LLM assistance)

# 2. Run pre-commit validation
.\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection

# 3. If issues found, fix them and re-run

# 4. When all checks pass (exit code 0), commit
git add .
git commit -m "Your commit message"
```

### Quick Iteration During Development
```powershell
# Build only (fast feedback)
.\ai\Build-Skyline.ps1

# Fix errors, then build again
.\ai\Build-Skyline.ps1

# When compiling cleanly, run full validation before commit
.\ai\Build-Skyline.ps1 -RunInspection -RunTests -TestName CodeInspection
```

## Exit Codes

- **0** = All checks passed, safe to commit
- **Non-zero** = Failures detected, do NOT commit

## Output Interpretation

### Build Failed
```
❌ Build FAILED (exit code: 1) in 5.2s
```
**Action**: Fix compilation errors shown in output

### Inspection Failed
```
⚠️ Code inspection found issues (exit code: 1) in 12.3s

Found 3 issue(s):
  [WARNING] ResourcesTest.cs(25): Using directive is not required
  [WARNING] Program.cs(42): Redundant 'this' qualifier
  [SUGGESTION] Helper.cs(15): Name can be simplified
```
**Action**: Fix ReSharper warnings listed above

### Tests Failed
```
❌ Tests FAILED (exit code: 1) in 2.1s
See log: bin\x64\Debug\Test.log
```
**Action**: Fix failing tests, see log for details

### All Passed
```
✅ Build succeeded in 8.2s
✅ Code inspection passed in 12.3s
✅ All tests passed in 2.1s
✅ All operations completed successfully
```
**Action**: Safe to commit!

## See Also

- **[Build-Skyline.ps1](Build-Skyline.ps1)** - The validation script
- **[ai/docs/build-and-test-guide.md](../../../ai/docs/build-and-test-guide.md)** - Comprehensive build documentation
- **[ai/STYLEGUIDE.md](../../../ai/STYLEGUIDE.md)** - Coding conventions
- **[ai/CRITICAL-RULES.md](../../../ai/CRITICAL-RULES.md)** - Absolute constraints

