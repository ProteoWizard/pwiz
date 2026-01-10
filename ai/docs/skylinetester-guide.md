# SkylineTester Guide

Comprehensive reference for SkylineTester, the GUI tool for building and testing Skyline.

## Overview

SkylineTester (`pwiz_tools/Skyline/SkylineTester/`) provides a graphical interface for:
- Building Skyline from source (using bjam)
- Running unit, functional, tutorial, and performance tests (using TestRunner.exe - pwiz_tools\Skyline\TestRunner)
- Testing UI forms in isolation
- Running nightly test configurations
- Analyzing test results and run statistics

## Key File Paths

All paths relative to project root (e.g., `C:\proj\pwiz` or wherever you cloned the repo).

| File | Relative Path |
|------|---------------|
| **SkylineTester.exe** | `pwiz_tools/Skyline/bin/x64/Debug/SkylineTester.exe` |
| **TestRunner.exe** | `pwiz_tools/Skyline/bin/x64/Debug/TestRunner.exe` |
| **Log file** | `pwiz_tools/Skyline/SkylineTester.log` |
| **Test list file** | `pwiz_tools/Skyline/SkylineTester test list.txt` |
| **Test results** | `pwiz_tools/Skyline/SkylineTester Results/` |
| **Settings (XML)** | `pwiz_tools/Skyline/SkylineTester.xml` |
| **Tutorial screenshots** | `pwiz_tools/Skyline/Documentation/Tutorials/<tutorial>/<lang>/` |
| **ImageComparer** | `pwiz_tools/Skyline/Executables/DevTools/ImageComparer/` |

The log file is overwritten each time SkylineTester starts a new test run.

### The Test List as a Shared Resource

The test list file (`SkylineTester test list.txt`) is a powerful shared resource used by multiple tools:

1. **SkylineTester UI** - Reads on startup to restore checked tests; writes when tests are checked/unchecked
2. **Run-Tests.ps1** - LLM reads with `-UseTestList`, writes with `-UpdateTestList`
3. **Visual Studio Debugger** - TestRunner.exe debug settings often configured to read from this file

**Workflow benefits:**
- Developer selects tests in SkylineTester → Close SkylineTester → Claude Code runs same tests via `-UseTestList`
- Claude Code updates test list → Developer starts SkylineTester → Tests already checked
- Developer configures VS debugger to use test list → Same test set for debugging and automated runs
- Test selections persist across all tools and sessions

## Tab Overview

### Forms Tab
Test individual UI forms/dialogs in isolation. Select a form from the list and click Run to launch it standalone. Useful for visual inspection and debugging form layout issues.

### Tutorials Tab
Run tutorial tests that capture screenshots and verify tutorial workflows. Options:
- Select specific tutorials to run
- Specify starting screenshot number (for resuming)
- Enable/disable screenshot capture
- Settings persist across restarts

### Tests Tab
Run automated tests from the test DLLs (Test.dll, TestFunctional.dll, etc.):
- Tree view with test categories and individual tests
- Check/uncheck tests to include in run
- "Check Failed Tests" button to re-run only failures
- Test selections persist via `SkylineTester test list.txt`
- Supports auto-screenshot and covershot modes for long automated screenshot collection
- Screenshots written to `pwiz_tools/Skyline/Documentation/Tutorials/<tutorial>/<lang>/` (cover.png and s-NN.png files)
- Bulk automated screenshot runs are compared with the ImageComparer GUI tool (`pwiz_tools/Skyline/Executables/DevTools/ImageComparer/`)

### Build Tab
Build Skyline from source using bjam (not MSBuild). Options:

| Option | Description |
|--------|-------------|
| **Source: Trunk** | Build from master branch |
| **Source: Branch** | Build from specified branch URL |
| **Build root folder** | Where to clone/build (default: Documents\SkylineBuild) |
| **Architecture** | 32-bit, 64-bit, or both |
| **Run build verification tests** | Run tests after build |
| **Open Skyline in VS after build** | Launch solution in Visual Studio |

**Code synchronization:**
- **Nuke**: Delete build folder, fresh `git clone`
- **Update**: `git pull` in existing folder
- **Incremental re-build**: Use existing code as-is

**For quick rebuilds of current code:** Set build root to your project root directory, select "Incremental re-build".

### Quality Tab
Code quality analysis and metrics. Run code inspection and view results.
- Typically used to run tests indefinitely in the same series (0.French, 1.Leak Check, 2.Repeating cycle) as the nightly test.
- Can be extremely useful for very long runs to diagnose if a leak is real and actually continues to accumulate indefinitely.

### Nightly Tab
Configure and run nightly test configurations. Used by SkylineNightly for automated overnight testing.

### Output Tab
Real-time test output display. Shows:
- Test progress and results
- Build output
- Error messages and stack traces
- Outputs to `pwiz_tools/Skyline/SkylineTester.log`
- Starts each session with the TestRunner.exe command used, which can be copied to re-run a configuration, especially under a debugger in Visual Studio for deeper diagnostics of issues occurring in a test or a test failure.

### Run Stats Tab
Historical test run statistics and timing analysis. View trends across multiple runs.
- Local equivalent of what the LabKeyMcp tool (see `ai/docs/mcp/nightly-tests.md` and `ai/mcp/LabKeyMcp/`) now provides in `save_run_comparison` tool.

## Common Workflows

### Run Selected Tests
1. Go to **Tests** tab
2. Check the tests you want to run (persisted in `pwiz_tools/Skyline/SkylineTester test list.txt`)
3. Press **F5** or click **Run**
4. Monitor progress in **Output** tab (logged to `pwiz_tools/Skyline/SkylineTester.log`)
5. Results saved to `pwiz_tools/Skyline/SkylineTester Results/`

### Re-run Failed Tests
1. After a test run with failures, go to **Output** tab
2. Click **"Check Failed Tests"** button (updates `pwiz_tools/Skyline/SkylineTester test list.txt`)
3. Go to **Tests** tab - failed tests are now checked
4. Press **F5** to re-run only failures

### Quick Build with Current Code
1. Go to **Build** tab
2. Set **Build root folder** to your project root (e.g., where you cloned pwiz)
3. Select **Incremental re-build**
4. Check **64 bit**
5. Click **Run**

### Run Tutorial with Screenshots
1. Go to **Tutorials** tab
2. Check the tutorial(s) to run
3. Set starting screenshot number if resuming
4. Enable screenshot capture
5. Press **F5**

## Integration with Run-Tests.ps1

SkylineTester and `Run-Tests.ps1` share the test list file for bidirectional workflow:

```powershell
# Run tests selected in SkylineTester UI (from pwiz_tools/Skyline/SkylineTester test list.txt)
./pwiz_tools/Skyline/ai/Run-Tests.ps1 -UseTestList

# Update test list and run (tests appear pre-checked in SkylineTester)
./pwiz_tools/Skyline/ai/Run-Tests.ps1 -TestName TestFoo,TestBar -UpdateTestList
```

**Workflows:**
1. Developer selects tests in SkylineTester → LLM runs same tests with `-UseTestList`
2. LLM updates test list with `-UpdateTestList` → Developer sees tests pre-checked in SkylineTester
3. Test selections persist across both SkylineTester restarts and LLM sessions

See [build-and-test-guide.md](build-and-test-guide.md#aiskylinetester-integration) for detailed integration documentation.

## Command-Line Options

```
SkylineTester.exe [options]

Options:
  --autorun    Automatically start tests after UI loads (for automated workflows)
```

**Example - automated test run:**
```powershell
Start-Process 'pwiz_tools\Skyline\bin\x64\Debug\SkylineTester.exe' -ArgumentList '--autorun'
```

## Visual Studio Detection

SkylineTester requires Visual Studio to be installed. It searches for VS in:
- Marketing year folders: `Microsoft Visual Studio\2017\`, `2019\`, `2022\`, etc.
- Internal version folders: `Microsoft Visual Studio\15\`, `16\`, `17\`, `18\`, etc.

**Note:** VS 2026 uses folder `18` (internal version) instead of `2026` (marketing year).

## Process Management

### Build Script Process Detection

`Build-Skyline.ps1` detects running SkylineTester, TestRunner, and Skyline processes **from the current build directory** (processes from other installations like `D:\Nightly` are ignored). When detected, it prompts:

```
BUILD BLOCKED - Running processes detected
Ask the developer: 'May I stop SkylineTester, TestRunner to proceed with the build?'
```

**What gets lost when stopping:**
- Unsaved test list changes (if tests were checked but SkylineTester not closed normally)
- Other session settings not yet persisted to `SkylineTester.xml`

**Iterative development workflow:**
During active debugging sessions, developers may grant blanket permission to stop test processes as needed. This enables a rapid edit-build-test cycle without repeated prompts. However, **always get explicit agreement** before adopting this workflow - some developers prefer to manage process lifecycle manually.

See [skylinetester-debugging-guide.md](skylinetester-debugging-guide.md) for automated dev-build-test cycles with `--autorun`.

## Troubleshooting

### "Visual Studio 2017 (or newer) is required"
Visual Studio detection failed. Check:
1. VS is installed in `C:\Program Files\Microsoft Visual Studio\`
2. For VS 2026, folder should be `18\Community\` (or Professional/Enterprise)

### Build blocked by running processes
Close any running SkylineTester, TestRunner, or Skyline instances before building.

### Tests not appearing checked after restart
Verify `SkylineTester test list.txt` exists and contains test names.

## Related Documentation

- [skylinetester-debugging-guide.md](skylinetester-debugging-guide.md) - Debugging hung tests with automated dev-build-test cycles
- [build-and-test-guide.md](build-and-test-guide.md) - Build system and Run-Tests.ps1 usage
- [testing-patterns.md](testing-patterns.md) - Test infrastructure overview

## Source Code

- `pwiz_tools/Skyline/SkylineTester/` - SkylineTester project
- `pwiz_tools/Skyline/SkylineTester/SkylineTesterWindow.cs` - Main window
- `pwiz_tools/Skyline/SkylineTester/TabBuild.cs` - Build tab logic
- `pwiz_tools/Skyline/SkylineTester/TabTests.cs` - Tests tab logic
- `pwiz_tools/Skyline/SkylineTester/TabTutorials.cs` - Tutorials tab logic
