# TODO-skylinebatch_test_cleanup.md

- Branch: Skyline/work/20251123_skylinebatch_test_cleanup
- Created: 2025-11-23
- PR: tbd

## Objective
Fix SkylineBatch test quality issues: file pollution, flaky tests, and proper test infrastructure.

## Background

### The Problem

SkylineBatch tests currently create files in the **source tree** instead of the **test sandbox**:

**Files Created in Source Tree:**
1. **Log files:** `SkylineBatchTest/Test/OldLogs/TestLog_*.log`
2. **Temporary test files:** `BcfgFileTest` writes temp `.bcfg` files to `Test/BcfgTestFiles/`
3. **Other artifacts:** Various tests may create additional files

**Behavior:**
- ✅ **When tests pass:** Files are cleaned up (not visible)
- ❌ **When tests fail:** Files left behind, pollute source tree
- ❌ **During development:** Git shows unversioned files requiring manual cleanup

**Root `.gitignore` Issues:**
```
/*.log  # Only ignores root-level logs, not subdirectories!
```

**Why This is Wrong:**
- Tests should be **self-contained** in sandboxes
- Failed tests shouldn't leave artifacts in versioned directories
- Developers waste time manually cleaning up test debris
- Risk of accidentally committing test artifacts
- Violates test isolation principles

### Current Workaround

**Developer must manually:**
1. Delete `SkylineBatchTest/Test/OldLogs/*.log` files after test runs
2. Clean up any temp `.bcfg` files in source tree
3. Verify no test artifacts before commits

**Better Solution:**
Fix tests to write to proper locations, eliminate manual cleanup burden.

## Current Implementation

### Log File Creation (TestUtils.cs)

```csharp
public static Logger GetTestLogger(string logFolder = "")
{
    // PROBLEM: Points to SOURCE TREE!
    logFolder = string.IsNullOrEmpty(logFolder) ? GetTestFilePath("OldLogs") : logFolder;
    var logName = "TestLog" + DateTime.Now.ToString("_HHmmssfff") + ".log";
    return new Logger(Path.Combine(logFolder, logName), logName, true);
}

public static string GetTestFilePath(string fileName)
{
    var currentPath = Directory.GetCurrentDirectory();
    if (File.Exists(Path.Combine(currentPath, "SkylineCmd.exe")))
        currentPath = Path.Combine(currentPath, "..", "..", "..", "Executables", "SkylineBatch", "SkylineBatchTest");
    else
    {
        currentPath = Path.GetDirectoryName(Path.GetDirectoryName(currentPath));
    }

    var batchTestPath = Path.Combine(currentPath, "Test");
    // Returns: <source_root>/SkylineBatchTest/Test/<fileName>
    return Path.Combine(batchTestPath, fileName);
}
```

**Usage Locations:**
- `BcfgFileTest.cs` - Multiple test methods call `GetTestLogger()`
- `ConfigManagerTest.cs` - Multiple test methods call `GetTestLogger()`
- `ConfigRunnerTest.cs` - Test methods call `GetTestLogger()`
- `TemplateFileDependencyTest.cs` - Test methods call `GetTestLogger()`
- `SkylineBatchLoggerTest.cs` - Tests log file creation/deletion
- `TestUtils.GetTestConfigManager()` - Helper creates logger
- `TestUtils.GetTestConfigRunner()` - Helper creates logger

### Temporary File Creation (BcfgFileTest.cs)

```csharp
private void ImportExportCompare(string filePathImport, string filePathExpectedExport)
{
    // Creates files like "21_1_0_189_complex_actual_replaced.bcfg" in source tree
    var filePathActualExport = filePathExpectedExport.Replace("expected", "actual");
    var configManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger());
    configManager.Import(filePathImport, null);
    // ...
    configManager.Export(filePathActualExport, indiciesToSave);
    // Only deleted if test passes!
}
```

## Solution Design

### Principle: Use Standard Test Infrastructure

**Skyline.exe tests use `TestContext.GetTestResultsPath()`:**
```csharp
// Example from ToolUpdatesTest:
public TestContext TestContext { get; set; }

var testSandbox = TestContext.GetTestResultsPath();
// Returns: pwiz_tools/Skyline/TestResults/<test_specific_folder>/
```

**Benefits:**
- Each test gets isolated sandbox directory
- Automatic cleanup by test framework
- Files never touch source tree
- Git stays clean
- Follows established project conventions

### Implementation Options

**Option 1: Add TestContext Support (Recommended)**

Make tests use standard `TestContext` pattern:

```csharp
// In each test class:
public TestContext TestContext { get; set; }

// Update GetTestLogger():
public static Logger GetTestLogger(TestContext testContext, string logFolder = "")
{
    if (string.IsNullOrEmpty(logFolder))
    {
        var testSandbox = testContext.GetTestResultsPath();
        logFolder = Path.Combine(testSandbox, "Logs");
    }
    
    if (!Directory.Exists(logFolder))
        Directory.CreateDirectory(logFolder);
        
    var logName = "TestLog" + DateTime.Now.ToString("_HHmmssfff") + ".log";
    return new Logger(Path.Combine(logFolder, logName), logName, true);
}

// Update all call sites:
var logger = TestUtils.GetTestLogger(TestContext);
```

**Pros:**
- Standard pattern (matches Skyline.exe)
- Proper test isolation
- Framework handles cleanup
- Files organized by test name

**Cons:**
- Requires updating ~15 call sites
- Need to add `TestContext` property to all test classes
- Some helper methods need TestContext parameter

**Option 2: Use System Temp Folder**

```csharp
public static Logger GetTestLogger(string logFolder = "")
{
    if (string.IsNullOrEmpty(logFolder))
    {
        logFolder = Path.Combine(Path.GetTempPath(), "SkylineBatchTest", "Logs");
    }
    
    if (!Directory.Exists(logFolder))
        Directory.CreateDirectory(logFolder);
        
    var logName = "TestLog" + DateTime.Now.ToString("_HHmmssfff") + ".log";
    return new Logger(Path.Combine(logFolder, logName), logName, true);
}
```

**Pros:**
- Minimal code changes
- No source tree pollution
- OS handles cleanup (eventually)

**Cons:**
- Temp folder can accumulate garbage
- Harder to find logs for debugging
- Not organized by test name
- Less aligned with Skyline patterns

**Option 3: Hybrid - TestResults Without TestContext**

```csharp
public static Logger GetTestLogger(string logFolder = "")
{
    if (string.IsNullOrEmpty(logFolder))
    {
        // Navigate to TestResults from source tree
        var testResultsPath = Path.Combine(
            GetTestFilePath(".."), // Up from Test/
            "..", // Up from SkylineBatchTest/
            "TestResults", 
            "SkylineBatchTest_Logs");
        logFolder = testResultsPath;
    }
    
    if (!Directory.Exists(logFolder))
        Directory.CreateDirectory(logFolder);
        
    var logName = "TestLog" + DateTime.Now.ToString("_HHmmssfff") + ".log";
    return new Logger(Path.Combine(logFolder, logName), logName, true);
}
```

**Pros:**
- Uses TestResults folder (correct location)
- Minimal call site changes
- Aligns with project conventions

**Cons:**
- Not test-specific (all logs in same folder)
- Less clean than TestContext approach

## Recommended Approach

**Phase 1: Quick Fix (Option 2 - System Temp)**
- Gets files out of source tree immediately
- Minimal code changes
- Low risk

**Phase 2: Proper Fix (Option 1 - TestContext)**
- Do this when refactoring test infrastructure anyway
- Aligns with Skyline patterns
- Proper test isolation
- Can be part of broader test quality improvements

**Rationale:**
- Phase 1 stops the bleeding (no more source tree pollution)
- Phase 2 does it right (when touching code anyway for coverage improvements)
- Incremental improvement > perfect solution that never ships

## Implementation Plan

### Phase 1: Stop Source Tree Pollution (Quick Win)

**Step 1: Fix `GetTestLogger()` to use temp folder**

```csharp
public static Logger GetTestLogger(string logFolder = "")
{
    if (string.IsNullOrEmpty(logFolder))
    {
        // Use system temp instead of source tree
        logFolder = Path.Combine(Path.GetTempPath(), "SkylineBatchTest", "Logs");
    }
    
    if (!Directory.Exists(logFolder))
        Directory.CreateDirectory(logFolder);
        
    var logName = "TestLog" + DateTime.Now.ToString("_HHmmssfff") + ".log";
    return new Logger(Path.Combine(logFolder, logName), logName, true);
}
```

**Step 2: Verify tests still pass**
- Run full SkylineBatch test suite
- Check logs are created in temp folder
- Verify no source tree pollution

**Step 3: Update `SkylineBatchLoggerTest.cs`**

The `TestTinyLog()` test explicitly tests log file management in `OldLogs/TestTinyLog`. Need to update it to use temp folder:

```csharp
[TestMethod]
public void TestTinyLog()
{
    // Use temp folder instead of source tree
    var logFolder = Path.Combine(Path.GetTempPath(), "SkylineBatchTest", "TestTinyLog");
    if (!Directory.Exists(logFolder)) Directory.CreateDirectory(logFolder);
    
    // Rest of test remains same...
}
```

**Estimated Effort:** 1-2 hours

### Phase 2: Proper TestContext Integration (When Refactoring Anyway)

This should be done **together with** `TODO-close_panorama_client_testing_gaps.md` or `TODO-batch_tools_warning_cleanup.md`:

**Step 1: Add TestContext to all test classes**

```csharp
[TestClass]
public class BcfgFileTest
{
    public TestContext TestContext { get; set; }  // Add this
    
    // ...existing tests...
}
```

**Step 2: Update GetTestLogger() signature**

```csharp
public static Logger GetTestLogger(TestContext testContext, string logSubfolder = "")
{
    var testSandbox = testContext.GetTestResultsPath();
    var logFolder = string.IsNullOrEmpty(logSubfolder) 
        ? Path.Combine(testSandbox, "Logs")
        : Path.Combine(testSandbox, logSubfolder);
    
    if (!Directory.Exists(logFolder))
        Directory.CreateDirectory(logFolder);
        
    var logName = "TestLog" + DateTime.Now.ToString("_HHmmssfff") + ".log";
    return new Logger(Path.Combine(logFolder, logName), logName, true);
}
```

**Step 3: Update all call sites (~15 locations)**

```csharp
// Before:
var logger = TestUtils.GetTestLogger();

// After:
var logger = TestUtils.GetTestLogger(TestContext);
```

**Step 4: Fix temp file creation in BcfgFileTest**

```csharp
private void ImportExportCompare(string filePathImport, string filePathExpectedExport)
{
    // Use TestContext sandbox instead of source tree
    var testSandbox = TestContext.GetTestResultsPath();
    var actualExportFileName = Path.GetFileName(filePathExpectedExport).Replace("expected", "actual");
    var filePathActualExport = Path.Combine(testSandbox, actualExportFileName);
    
    var configManager = new SkylineBatchConfigManager(TestUtils.GetTestLogger(TestContext));
    // ... rest of test
}
```

**Estimated Effort:** 4-6 hours

## Files to Modify

### Phase 1 (Quick Fix):
- `pwiz_tools/Skyline/Executables/SkylineBatch/SkylineBatchTest/TestUtils.cs`
  - `GetTestLogger()` method (lines 384-389)
- `pwiz_tools/Skyline/Executables/SkylineBatch/SkylineBatchTest/SkylineBatchLoggerTest.cs`
  - `TestTinyLog()` method (line 34)
  - `GetAllLogFiles()` calls (if needed)

### Phase 2 (Proper Fix):
- All test classes (add `TestContext` property):
  - `BcfgFileTest.cs`
  - `ConfigManagerTest.cs`
  - `ConfigRunnerTest.cs`
  - `SkylineBatchLoggerTest.cs`
  - `TemplateFileDependencyTest.cs`
- `TestUtils.cs`:
  - `GetTestLogger()` signature change
  - `GetTestConfigManager()` signature change
  - `GetTestConfigRunner()` signature change
- All call sites (~15 locations)
- `BcfgFileTest.cs`:
  - `ImportExportCompare()` temp file creation
  - Any other temp file creation

## Integration with Other TODOs

**Combine with:**
- `TODO-batch_tools_warning_cleanup.md` - ReSharper warning cleanup
- `TODO-close_panorama_client_testing_gaps.md` - Test infrastructure improvements

**Rationale:**
- All three involve improving SkylineBatch test quality
- Can be done in same PR
- Share context and understanding
- More efficient than separate efforts

**Suggested Order:**
1. This TODO (test file cleanup) - **FIRST** (clean foundation)
2. Coverage gaps (Panorama testing) - Uses clean infrastructure
3. ReSharper warnings - Polish on top of solid base

## Verification

**After Phase 1:**
- ✅ Run all SkylineBatch tests
- ✅ Check source tree - no new files in `Test/OldLogs/`
- ✅ Check temp folder - logs appear in `%TEMP%/SkylineBatchTest/`
- ✅ All tests still pass

**After Phase 2:**
- ✅ Run all SkylineBatch tests
- ✅ Check source tree - completely clean (no test artifacts)
- ✅ Check `TestResults/` - each test has isolated folder with logs
- ✅ All tests still pass
- ✅ Failed tests don't pollute source tree

## Success Criteria

### Must Have
- ✅ No test artifacts in source tree after test runs
- ✅ Logs go to temp folder (Phase 1) or TestResults (Phase 2)
- ✅ All tests pass
- ✅ No manual cleanup required

### Should Have
- ✅ Tests use `TestContext` for proper isolation (Phase 2)
- ✅ Each test has own sandbox directory
- ✅ Temp files cleaned up even when tests fail
- ✅ Pattern documented for future test authors

### Nice to Have
- ✅ Audit other Executables projects (AutoQC, etc.) for same issue
- ✅ Document pattern in TESTING.md
- ✅ Add test to verify no source tree pollution

## Related Issues

**Similar Patterns in Project:**

1. **Skyline.exe tests:** Already use `TestContext.GetTestResultsPath()` ✅
   - See: `TestFunctional/ToolUpdatesTest.cs` for example
   
2. **AutoQC tests:** Check if they have same issue
   - May need same fix

3. **SharedBatch tests:** Check if they have same issue
   - May need same fix

**Project-Wide Principle:**
> Tests should never write to the source tree. Always use TestContext sandbox or system temp folder.

Add this to `TESTING.md` best practices?

## Notes

**Discovered During:**
- PanoramaClient WebClient migration (`TODO-20251023_panorama_webclient_replacement.md`)
- Coverage analysis revealed test file creation patterns
- Git status showed unversioned log files after test runs

**Why Not Just Add to .gitignore:**
> "I would prefer to leave them and have to manually delete them to understand what truly unversioned files I should be considering to make sure we don't just forget about this issue. I would much rather see tests putting them in the right place than expanding .gitignore to deal with poorly behaving tests."
> - Developer, 2025-10-25

**This is the Right Approach:**
- Fix root cause, not symptoms
- Tests should be well-behaved citizens
- `.gitignore` is for build artifacts, not test misbehavior
- Forces us to address the issue properly

## References

**Examples of Proper Patterns:**
- `pwiz_tools/Skyline/TestFunctional/ToolUpdatesTest.cs` - Uses `TestContext`
- `pwiz_tools/Skyline/TestFunctional/HttpClientWithProgressIntegrationTest.cs` - Uses `TestContext`
- Any test in Skyline.exe `TestFunctional` project

**Documentation:**
- `TESTING.md` section 1 - Test Project Structure
- `TESTING.md` section 9 - Code Coverage Validation (mentions TestResults folder)

**Key Classes:**
- `Microsoft.VisualStudio.TestTools.UnitTesting.TestContext` - Standard MSTest infrastructure
- `TestContext.GetTestResultsPath()` - Extension method from Skyline test utilities

## Issue 2: Flaky DataDownloadTest

### Problem
`DataDownloadFunctionalTest.DataDownloadTest()` fails intermittently (2-3 out of 5 runs in full suite):

**Symptoms:**
- Test gets stuck showing `ConnectionErrorForm` with "EmptyTemplate" unable to connect
- Waiting for `CommonAlertDlg` (disk space error) but form never appears
- Usually passes when run in isolation
- Fails more often in full test suite runs

**Error:**
```
Assert.Fail failed. Timeout 240 seconds exceeded in WaitForOpenForm(CommonAlertDlg). 
Open forms: MainForm (Skyline Batch 1000.0.0.0), ConnectionErrorForm (Connection Error)
```

**Root Cause:**
- Likely race condition or timing issue in FTP connection check
- May be environmental (FTP server unreachable/slow in CI)
- Not directly caused by HttpClient migration, but exposed during testing

**Current Workaround:**
- Re-run test in isolation (usually passes)
- Run all tests, then re-run DataDownloadTest

### Potential Solutions

**Option 1: Add Retry Logic**
- Automatically retry connection checks
- Configurable timeout/retry count
- Better for real-world flakiness

**Option 2: Mock FTP Connections in Tests**
- Replace real FTP calls with mocks
- Deterministic behavior
- Faster test execution
- More work to implement

**Option 3: Increase Timeouts**
- Simple but doesn't fix root cause
- May hide real issues
- Not recommended

**Option 4: Skip Connection Check for Test Data**
- Add flag to bypass real connection checks
- Use pre-determined file info
- Fastest, most reliable for tests

### Recommended Approach
**Phase 1:** Investigate actual FTP connection failures (logging/diagnostics)
**Phase 2:** Add retry logic with exponential backoff
**Phase 3:** Consider mocking for unit tests, keep real connections for integration tests

## Priority

**Medium-High** - Should be fixed before major test infrastructure work.

**Rationale:**
- Actively causing developer friction (manual cleanup)
- Foundation for other test improvements
- Quick Phase 1 fix (1-2 hours)
- Proper Phase 2 fix naturally fits with coverage/warning cleanup
- Flaky tests reduce confidence in test suite

**Suggested Timeline:**
- **This week:** Phase 1 (temp folder) - immediate relief for file pollution
- **Next sprint:** Phase 2 (TestContext) - with other test improvements
- **Investigate flaky test:** When time permits, add better logging/retry logic

