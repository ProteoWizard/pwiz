# Testing Guidelines for Skyline/ProteoWizard

## Table of Contents
1. [Test Project Structure](#test-project-structure)
2. [Test Types and Organization](#test-types-and-organization)
3. [Test Execution Tools](#test-execution-tools)
4. [Dependency Injection Patterns for Testing](#dependency-injection-patterns-for-testing)
5. [Assertion Best Practices](#assertion-best-practices)
6. [HttpClient Testing with HttpClientTestHelper](#httpclient-testing-with-httpclienttesthelper)
7. [Recording Test Data with IsRecordMode](#recording-test-data-with-isrecordmode)
8. [Translation-Proof Testing](#translation-proof-testing)
9. [Test Performance Considerations](#test-performance-considerations)
10. [Code Coverage Validation](#code-coverage-validation)

---

## Test Project Structure

Skyline uses a multi-project test architecture to separate tests by type, complexity, and resource requirements.

### Test Projects Overview

| Project | Purpose | Base Class | Data Files | Network | UI |
|---------|---------|-----------|------------|---------|-----|
| **Test.csproj** | Unit tests (fast, no data files) | `AbstractUnitTest` | ❌ | ❌ | ❌ |
| **TestData.csproj** | Unit tests with mass spec data | `AbstractUnitTestEx` | ✅ | ❌ | ❌ |
| **TestFunctional.csproj** | General functional tests (UI) | `AbstractFunctionalTestEx` * | ✅ | ❌ | ✅ |
| **TestConnected.csproj** | Tests requiring network access | varies | ✅ | ✅ | varies |
| **TestTutorial.csproj** | Automated tutorial tests (screenshot generation) | `AbstractFunctionalTestEx` * | ✅ | ❌ | ✅ |
| **TestPerf.csproj** | Performance tests (large datasets >100MB) | `AbstractFunctionalTestEx` * | ✅ (large) | ❌ | ✅ |
| **TestUtil.csproj** | Shared testing utilities | N/A | N/A | N/A | N/A |

\* Use `AbstractFunctionalTestEx` for most tests (high-level helpers). Use `AbstractFunctionalTest` only when you need fine-grained control.

### Project Details

#### Test.csproj - Unit Tests
- **Purpose**: Fast, isolated unit tests without data files
- **Base class**: `AbstractUnitTest`
- **Characteristics**:
  - No mass spectrometry data files required
  - May access file system for temporary files
  - No UI interaction
  - Fast execution (milliseconds per test)
  - Ideal for testing pure logic, algorithms, utilities

#### TestData.csproj - Unit Tests with Data Files
- **Purpose**: Unit tests requiring actual mass spectrometry data
- **Base class**: `AbstractUnitTestEx`
- **Characteristics**:
  - Works with real mass spec data files
  - Must access file system
  - No UI interaction
  - Moderate execution time
  - Tests data parsing, file format handling, spectrum processing

#### TestFunctional.csproj - Functional Tests
- **Purpose**: Standard functional tests for UI features
- **Base class**: `AbstractFunctionalTestEx` (preferred), or `AbstractFunctionalTest` (primitives only)
- **Characteristics**:
  - Shows and destroys `SkylineWindow` instance
  - Tests UI workflows and user interactions
  - `AbstractFunctionalTestEx` provides high-level helpers like `ImportResultsFile()`, `ShareDocument()`
  - `AbstractFunctionalTest` provides low-level primitives like `RunUI()`, `ShowDialog<T>()`
  - Slower than unit tests (seconds per test)
  - Most common test project for Skyline features

#### TestConnected.csproj - Network Tests
- **Purpose**: Tests requiring actual network access
- **Base class**: varies by test type
- **Characteristics**:
  - Accesses real web services (Panorama, UniProt, etc.)
  - May be skipped in offline environments
  - Run less frequently than other tests
  - Examples: Panorama upload/download, web service integration

#### TestTutorial.csproj - Tutorial Tests
- **Purpose**: Automated implementation of documentation tutorials
- **Base class**: `AbstractFunctionalTestEx` (for workflow helpers)
- **Characteristics**:
  - Auto-generates tutorial screenshots
  - Validates documentation accuracy
  - Tests step-by-step tutorial workflows
  - Test data stored in `Skyline/Documentation/Tutorials/`
  - Examples: `TestIrtTutorial` for `iRT` tutorial
  - Ensures tutorials stay synchronized with product
  - Uses `AbstractFunctionalTestEx` helpers for common tutorial operations (import, export, etc.)

#### TestPerf.csproj - Performance Tests
- **Purpose**: Data-intensive tests with large datasets
- **Base class**: `AbstractFunctionalTestEx` (for workflow helpers)
- **Characteristics**:
  - Requires significant disk space (>1GB)
  - Requires significant memory (>8GB RAM)
  - Test data stored in Downloads folder
  - May not run on resource-constrained machines
  - Run less frequently (nightly builds, not every commit)
  - Tests performance, memory usage, large file handling
  - Uses `AbstractFunctionalTestEx` helpers to navigate complex workflows with large data

#### TestUtil.csproj - Shared Testing Utilities
- **Purpose**: Shared testing infrastructure and helpers
- **Contains**:
  - `AbstractUnitTest`, `AbstractFunctionalTest` base classes
  - `AssertEx` - Extended assertion library
  - `HttpClientTestHelper` - HTTP testing utilities
  - `TestFilesDir` - Test file management
  - Common test helpers and utilities

---

## Test Execution Tools

Skyline provides several tools for running tests outside Visual Studio/ReSharper:

### TestRunner.exe (TestRunner.csproj)
- **Purpose**: Console application for running tests programmatically
- **Used by**: TeamCity CI, nightly test runs, automated build systems
- **Features**:
  - Command-line test execution
  - Parallel test execution
  - XML test result output
  - Locale/language switching for localization testing
  - Integration with TeamCity and other CI systems

### TestRunnerLib (TestRunnerLib.csproj)
- **Purpose**: Shared library used by TestRunner.exe
- **Contains**:
  - Core test execution logic
  - Test discovery and filtering
  - Result reporting and logging
  - Language/locale management

### SkylineTester.exe (SkylineTester.csproj)
- **Purpose**: Full-featured test harness UI for developers
- **Features**:
  - Configure which tests to run (by project, by test name, by pattern)
  - Monitor test execution in real-time
  - View test output logs in dedicated Output tab
  - Locale selection for localization testing
  - Test result summary with pass/fail counts
  - Integration with TeamCity for nightly runs
- **Usage**: Primary tool for developers running tests locally
- **Workflow**:
  1. Select test projects and filters
  2. Click "Run" - launches TestRunner.exe
  3. Monitor progress in UI
  4. Review output logs and results

### SkylineNightly.exe (SkylineNightly.csproj)
- **Purpose**: Small program for scheduling and running nightly tests
- **Features**:
  - Schedules nightly test runs on developer machines
  - Runs tests using TestRunner.exe via SkylineTester
  - Uploads results to TeamCity or shared storage
  - Sends notifications on failures
- **Usage**: Automated nightly testing on developer workstations

### SkylineNightlyShim.exe (SkylineNightlyShim.csproj)
- **Purpose**: Very small bootstrapper program
- **Features**:
  - Downloads latest SkylineNightly.exe from TeamCity artifacts
  - Downloads latest SkylineTester.exe from TeamCity artifacts
  - Ensures nightly runs use most recent test infrastructure
  - Minimal code to avoid needing updates itself
- **Usage**: Scheduled task on developer machines calls this first
- **Workflow**:
  1. Shim downloads latest SkylineNightly.exe and SkylineTester.exe
  2. Shim launches updated SkylineNightly.exe
  3. SkylineNightly.exe runs tests with SkylineTester.exe/TestRunner.exe

---

## Test Types and Organization

### Unit Tests vs Functional Tests

**Unit Tests** (`Test.csproj`, `TestData.csproj`):
- Derive from `AbstractUnitTest` or `AbstractUnitTestEx`
- Fast, isolated, no UI
- Test individual components in isolation
- Preferred when UI is not needed

**Functional Tests** (`TestFunctional.csproj`, `TestPerf.csproj`, `TestTutorial.csproj`):
- Derive from `AbstractFunctionalTest` or `AbstractFunctionalTestEx`
- Show SkylineWindow, drive UI
- Test complete workflows and user interactions
- Use when testing UI features or integration scenarios

### Functional Test Base Classes

**`AbstractFunctionalTest`** - Testing primitives:
- **Purpose**: Low-level functional testing infrastructure
- **Provides**:
  - `RunUI()` - Execute code on UI thread
  - `ShowDialog<T>()` - Show and wait for modal dialogs
  - `WaitForCondition()` - Poll for conditions
  - `WaitForOpenForm<T>()` - Wait for form to open
  - `FindOpenForm<T>()` - Find already-open form
  - Basic SkylineWindow management
- **Use when**: You need fine-grained control over test flow

**`AbstractFunctionalTestEx`** - High-level helpers:
- **Extends**: `AbstractFunctionalTest`
- **Purpose**: Common multi-step operations as single method calls
- **Provides**:
  - `ImportResultsFile()` - Import results with all dialogs
  - `ExportReport()` - Export report with configuration
  - `ShareDocument()` - Share to Panorama with full workflow
  - `ImportPeptideSearch()` - Import peptide search with wizard navigation
  - `ImportFasta()` - Import FASTA with background proteome handling
  - Many more high-level workflow helpers
- **Use when**: Testing common workflows (most tests should use this)

**Recommendation**: Prefer `AbstractFunctionalTestEx` unless you need only the low-level primitives from `AbstractFunctionalTest`. The `Ex` helpers save significant code duplication and make tests more readable.

### Test Structure Best Practices

**Use `RunFunctionalTest()` pattern for functional tests:**

```csharp
[TestMethod]
public void MyFeatureTest()
{
    RunFunctionalTest();
}

protected override void DoTest()
{
    // Test implementation here
    TestStep1();
    TestStep2();
    TestStep3();
}

private void TestStep1()
{
    // First validation
}

private void TestStep2()
{
    // Second validation
}
```

**Consolidate related validations into single test method:**

```csharp
// ✅ GOOD - One test method, multiple validation steps
[TestMethod]
public void ToolStoreTest()
{
    RunFunctionalTest();
}

protected override void DoTest()
{
    TestToolNotInstalled();
    TestToolInstalled();
    TestDownloadSuccess();
    TestDownloadFailure();
}

// ❌ AVOID - Separate test methods for each validation (slow)
[TestMethod]
public void TestToolNotInstalled() { ... }

[TestMethod]
public void TestToolInstalled() { ... }

[TestMethod]
public void TestDownloadSuccess() { ... }
```

**Why consolidation matters:**
- Functional tests have **significant overhead** (create/destroy SkylineWindow)
- Each `[TestMethod]` starts fresh - can't share SkylineWindow instance
- Consolidating validations into private methods within one test is much faster
- See `ai/MEMORY.md` "DRY Principle in Testing" for detailed examples

---

## Dependency Injection Patterns for Testing

### Pattern Decision: Call Stack Depth Matters

The choice between **constructor injection** and **static test seam + IDisposable** depends on whether tests can construct the object directly or need to intercept deep in the call stack.

### Pattern 1: Constructor Injection (Shallow Call Stack)

**Use when:** Tests construct the object under test directly.

**Example:** `SkypSupport` (from `SkypSupport.cs`)

```csharp
// Production class
public class SkypSupport
{
    private readonly Func<IProgressMonitor, IProgressStatus, IDownloadClient> _clientFactory;

    // Production constructor - uses real HttpDownloadClient
    public SkypSupport(SkylineWindow skyline)
        : this(skyline, CreateHttpDownloadClient)
    {
    }

    // Test constructor - accepts custom factory
    public SkypSupport(SkylineWindow skyline, Func<IProgressMonitor, IProgressStatus, IDownloadClient> clientFactory)
    {
        _skyline = skyline;
        _clientFactory = clientFactory;
    }

    // Named factory function (better than lambda for debugging)
    private static IDownloadClient CreateHttpDownloadClient(IProgressMonitor monitor, IProgressStatus status)
    {
        return new HttpDownloadClient(monitor, status);
    }
}

// Test code
var skypSupport = new SkypSupport(SkylineWindow, (monitor, status) =>
    new TestDownloadClient(srcPath, skyp, monitor, status));
```

**Benefits:**
- No static mutable state
- Clear dependency flow
- Easy to understand and debug

**When this works:**
- Tests already construct the object directly
- No need to traverse deep call stacks

---

### Pattern 2: Static Test Seam + IDisposable (Deep Call Stack)

**Use when:** Tests call high-level production code that eventually needs the dependency deep in the call stack.

**Example:** `ToolStoreUtil.ToolStoreClient` (from `ToolStoreDlg.cs`)

```csharp
// Production class with static test seam
public static class ToolStoreUtil
{
    public static IToolStoreClient ToolStoreClient { get; set; }

    public static IToolStoreClient CreateClient()
    {
        return new WebToolStoreClient();
    }

    static ToolStoreUtil()
    {
        ToolStoreClient = CreateClient();
    }
}

// Test class with IDisposable to manage static state
public class TestToolStoreClient : IToolStoreClient, IDisposable
{
    private readonly IToolStoreClient _originalClient;

    public TestToolStoreClient(string toolDirPath)
    {
        _toolDir = new DirectoryInfo(toolDirPath);
        _originalClient = ToolStoreUtil.ToolStoreClient;
        ToolStoreUtil.ToolStoreClient = this;  // Inject test implementation
    }

    public void Dispose()
    {
        ToolStoreUtil.ToolStoreClient = _originalClient;  // Restore original
    }
}

// Test code - high-level production code path
using (var client = new TestToolStoreClient(toolDirPath))
{
    // Calls SkylineWindow.ShowToolStoreDlg()
    //   -> ToolInstallUI.InstallZipFromWeb()
    //   -> ToolStoreDlg (uses ToolStoreUtil.ToolStoreClient)
    //   -> Network download
    var toolStoreDlg = ShowDialog<ToolStoreDlg>(SkylineWindow.ShowToolStoreDlg);
    // Test implementation is automatically used deep in the call stack
}
```

**Also see:** `HttpClientWithProgress.TestBehavior` - uses the same pattern for the same reason.

**Benefits:**
- Tests can exercise full production code paths from entry points (e.g., `SkylineWindow.ShowToolStoreDlg()`)
- No need to add test parameters to every layer
- Clean production APIs - no test concerns leak through

**When this is necessary:**
- Deep call stacks (3+ layers)
- Tests need to call high-level entry points (`SkylineWindow` methods)
- Alternative would pollute many production methods with test parameters

---

### Decision Heuristic

```
Can tests construct the object directly?
  ✅ YES → Constructor injection (Pattern 1)
  ❌ NO  → Static test seam + IDisposable (Pattern 2)

How many layers between test entry point and dependency?
  1-2 layers  → Constructor injection usually works
  3+ layers   → Static test seam often better
```

---

## Assertion Best Practices

### Use AssertEx Instead of Custom Wrappers

Skyline provides **`AssertEx`** (in `pwiz.SkylineTestUtil`) with many useful assertion methods. **Prefer these over writing custom assertion helpers.**

### Common AssertEx Methods

#### String Assertions

```csharp
// ✅ GOOD - Use AssertEx.Contains
AssertEx.Contains(actualString, expectedSubstring);
AssertEx.DoesNotContain(actualString, unexpectedSubstring);

// ❌ AVOID - Custom wrapper
private void AssertErrorContains(string actual, string expected)
{
    AssertEx.Contains(actual, expected);  // Just call AssertEx directly!
}

// ❌ AVOID - Manual Assert.IsTrue
Assert.IsTrue(actualString.Contains(expectedSubstring),
    $"Expected string to contain '{expectedSubstring}' but got: {actualString}");
```

**Why AssertEx.Contains is better:**
- Provides clear failure messages automatically
- Shows both actual and expected values
- Consistent with other assertion methods
- No need to write custom formatting logic

#### File Assertions

```csharp
// Check file existence
AssertEx.FileExists(filePath);
AssertEx.FileNotExists(filePath);

// Compare file contents
AssertEx.FileEquals(expectedPath, actualPath);

// Compare strings with diff output
AssertEx.NoDiff(expectedString, actualString);
```

#### Exception Assertions

```csharp
// Assert exception is thrown
AssertEx.ThrowsException<InvalidDataException>(() =>
{
    // Code that should throw
});

// Assert NO exception is thrown (useful for regression tests)
AssertEx.ThrowsNoException(() =>
{
    // Code that should succeed
});
```

#### Serialization Testing

```csharp
// Verify object is serializable
AssertEx.Serializable<SrmDocument>(document);
```

### Translation-Proof Assertions with HttpClientTestHelper

When testing network error messages, **always use `HttpClientTestHelper.GetExpectedMessage()`** to get the expected localized error message:

```csharp
// ✅ GOOD - Works in all locales
using (var helper = HttpClientTestHelper.SimulateHttp401())
{
    var errDlg = ShowDialog<AlertDlg>(() => skypSupport.Open(skypPath, existingServers));
    var expectedError = helper.GetExpectedMessage(skyp.SkylineDocUri);
    AssertEx.Contains(errDlg.Message, expectedError);
}

// ❌ BAD - Hardcoded English text breaks in other locales
Assert.IsTrue(errDlg.Message.Contains("HTTP 401"), "Expected 401 error");
```

### Complete AssertEx API

For the full list of assertion methods, see `AssertEx.cs` in `pwiz_tools/Skyline/TestUtil/`.

Notable methods include:
- `Contains(string, string)` / `DoesNotContain(string, string)`
- `FileExists(string)` / `FileNotExists(string)`
- `FileEquals(string, string)`
- `NoDiff(string, string)`
- `ThrowsException<TEx>(Action)` / `ThrowsNoException(Action)`
- `Serializable<TObj>(TObj)`
- `AreEqual<T>(T, T)` with better formatting than Assert.AreEqual
- `AreNotEqual<T>(T, T)`
- `IsNull<T>(T)` / `IsNotNull<T>(T)`

---

## HttpClient Testing with HttpClientTestHelper

### Test Pattern: SUCCESS vs FAILURE

When testing code that uses `HttpClientWithProgress`, follow this established pattern:

1. **SUCCESS path:** Test implementation provides mock data from local files
2. **FAILURE path:** Real production code + `HttpClientTestHelper` intercepts at HttpClient level

### Example: SkypSupport Download Testing

```csharp
// SUCCESS - Test implementation copies local file
public class TestDownloadClient : IDownloadClient
{
    private readonly string _srcPath;

    public void Download(SkypFile skyp)
    {
        File.Copy(_srcPath, skyp.DownloadPath);  // Mock success
    }
}

// FAILURE - Real production code + HttpClientTestHelper
[TestMethod]
public void TestSkypDownloadFailure()
{
    using (var helper = HttpClientTestHelper.SimulateNoNetworkInterface())
    {
        var skypSupport = new SkypSupport(SkylineWindow);  // Uses real HttpDownloadClient
        var errDlg = ShowDialog<AlertDlg>(() => skypSupport.Open(skypPath, null));

        // Assert using helper's expected message
        var expectedError = helper.GetExpectedMessage(skyp.SkylineDocUri);
        AssertEx.Contains(errDlg.Message, expectedError);
    }
}
```

### HttpClientTestHelper Simulation Methods

```csharp
// Network failures
HttpClientTestHelper.SimulateNoNetworkInterface()
HttpClientTestHelper.SimulateHttpTimeout()

// HTTP status codes
HttpClientTestHelper.SimulateHttp401()  // Unauthorized
HttpClientTestHelper.SimulateHttp403()  // Forbidden
HttpClientTestHelper.SimulateHttp404()  // Not Found
HttpClientTestHelper.SimulateHttp500()  // Internal Server Error

// User cancellation
HttpClientTestHelper.SimulateCancellation()
```

### Why This Pattern?

**Benefits:**
- ✅ No network access in tests (success OR failure)
- ✅ Production code exercises real `HttpClientWithProgress` in tests
- ✅ Test interface stays simple - just mock success case
- ✅ Failure testing uses production error handling paths

**What NOT to do:**
- ❌ Don't make test interface return error codes or exceptions
- ❌ Don't create `TestClientError401`, `TestClientError403`, etc.
- ❌ Don't parse exception messages in tests - use `GetExpectedMessage()`

---

## Recording Test Data with IsRecordMode

### Overview

Many tests need to capture expected data during one run and validate against that data in subsequent runs. This pattern is used for:
- **HTTP interactions** - Recording live web service responses for offline playback
- **Graph labels and formatting** - Capturing expected chart axis labels, legends, tooltips
- **Model weights and calculations** - Recording expected values from complex algorithms
- **Localized strings** - Capturing expected UI text in different locales
- **File format outputs** - Recording expected file contents for format validation

The `IsRecordMode` pattern provides a standardized way to toggle between recording and validation modes in tests.

### Base Class Support

**`AbstractUnitTestEx`** provides the recording infrastructure:

```csharp
public abstract class AbstractUnitTestEx : AbstractUnitTest
{
    /// <summary>
    /// Override this property to enable recording mode.
    /// When true, the test writes expected data instead of validating it.
    /// </summary>
    protected virtual bool IsRecordMode
    {
        get { return false; }
    }

    /// <summary>
    /// Call this at the end of test methods to prevent committing code with IsRecordMode = true.
    /// Functional tests automatically call this in RunTest().
    /// </summary>
    protected void CheckRecordMode()
    {
        Assert.IsFalse(IsRecordMode, "Set IsRecordMode to false before commit");
    }
}
```

**`AbstractFunctionalTestEx`** automatically calls `CheckRecordMode()` in `RunTest()`, so functional tests don't need to add it manually.

### Implementing Recording in Tests

**Step 1: Override `IsRecordMode` property**

```csharp
[TestClass]
public class MyTest : AbstractUnitTestEx
{
    protected override bool IsRecordMode => false;  // Always default to false
}
```

**Step 2: Add conditional logic for recording vs. validation**

```csharp
[TestMethod]
public void TestMyFeature()
{
    if (IsRecordMode)
    {
        // Record expected data
        var expectedData = RunFeatureAndCaptureResults();
        SaveExpectedData(expectedData);
        return;  // Exit early - don't validate
    }
    
    // Validation mode - load and compare
    var expectedData = LoadExpectedData();
    var actualData = RunFeatureAndCaptureResults();
    ValidateResults(expectedData, actualData);
    
    // Guard against committing with record mode enabled
    CheckRecordMode();
}
```

**Step 3: Use JSON files for recorded data (preferred)**

The modern pattern is to write recorded data to JSON files with the same name prefix as the test:

```csharp
private const string EXPECTED_DATA_JSON = @"CommonTest\MyTestExpectedData.json";

private void SaveExpectedData(MyExpectedData data)
{
    var jsonPath = TestContext.GetProjectDirectory(EXPECTED_DATA_JSON);
    var json = JsonConvert.SerializeObject(data, Formatting.Indented);
    File.WriteAllText(jsonPath, json, new UTF8Encoding(false));
    Console.Out.WriteLine(@"Recorded expected data to: " + jsonPath);
}

private MyExpectedData LoadExpectedData()
{
    var jsonPath = TestContext.GetProjectDirectory(EXPECTED_DATA_JSON);
    if (!File.Exists(jsonPath))
        return new MyExpectedData();  // Return empty/default if not recorded yet
    
    var json = File.ReadAllText(jsonPath, Encoding.UTF8);
    return JsonConvert.DeserializeObject<MyExpectedData>(json) ?? new MyExpectedData();
}
```

**Legacy pattern (still exists in codebase):** Some older tests output C# code to the console for developers to copy-paste into test source. This pattern is being phased out in favor of JSON files.

### Recording HTTP Interactions

For tests that record HTTP traffic, use `HttpClientTestHelper.HttpInteractionRecorder`:

```csharp
[TestMethod]
public void WebTestFastaImport()
{
    HttpClientTestHelper.HttpInteractionRecorder recorder = null;
    if (IsRecordMode && AllowInternetAccess)
    {
        recorder = new HttpClientTestHelper.HttpInteractionRecorder();
    }

    using (recorder != null ? HttpClientTestHelper.BeginRecording(recorder) : null)
    {
        // Run test that makes HTTP requests
        var results = RunFastaImportWithWebLookups();
        
        if (IsRecordMode)
        {
            // Save both protein metadata and HTTP interactions
            RecordExpectedData(results, recorder?.Interactions?.ToList());
            return;
        }
        
        // Validation mode - load and compare
        var expectedData = LoadExpectedData();
        ValidateResults(expectedData, results);
    }
    
    CheckRecordMode();
}
```

The recorded HTTP interactions are serialized to JSON and can be played back offline using `HttpClientTestHelper.PlaybackFromInteractions()`.

### Workflow: Recording New Test Data

> ⚠️ **After changing `IsRecordMode`, rebuild before running the test.** Without a build the test harness will execute the previous assembly and nothing will be recorded.

**1. Enable recording mode**

```csharp
protected override bool IsRecordMode => true;  // Temporarily set to true
```

**2. Run the test with required resources**

- For HTTP tests: Run with `-EnableInternet` flag
- For UI tests: Run in appropriate locale if testing localization
- For algorithm tests: Run with representative input data

**3. Verify the recorded data**

- Check that the JSON file was created/updated
- Inspect the file contents to ensure they look correct
- Verify file is in the expected location (usually `CommonTest/` or `TestData/`)

**4. Disable recording mode and validate**

```csharp
protected override bool IsRecordMode => false;  // Set back to false
```

> ⚠️ **Rebuild again after restoring `IsRecordMode => false`** to ensure validation uses the updated code.

**5. Run the test again (offline if applicable)**

- Test should now load recorded data and validate against it
- All assertions should pass
- Test should run without network access (for HTTP tests)

**6. Commit the changes**

- Include the new/updated JSON file in the commit
- Ensure `IsRecordMode => false` is committed
- The `CheckRecordMode()` assertion will fail if you forget to set it back

### Best Practices

**DO:**
- ✅ **Always default to `false`** - Recording mode should be the exception, not the rule
- ✅ **Use JSON files** for recorded data (easier to inspect, version control friendly)
- ✅ **Add `CheckRecordMode()`** at the end of unit test methods
- ✅ **Document what gets recorded** in code comments or test descriptions
- ✅ **Version control the JSON files** - they're test artifacts that should be committed
- ✅ **Structure JSON clearly** - Use descriptive property names, add comments in JSON if needed
- ✅ **Test both modes** - Verify recording works AND validation works with recorded data

**DON'T:**
- ❌ **Never commit with `IsRecordMode => true`** - The `CheckRecordMode()` assertion prevents this
- ❌ **Don't record sensitive data** - Avoid API keys, passwords, user tokens in recorded files
- ❌ **Don't record large binary data** - Use references or hashes instead
- ❌ **Don't record timestamps** - Use relative times or remove time-dependent fields
- ❌ **Don't record absolute file paths** - Use relative paths or path placeholders
- ❌ **Don't skip validation** - Always test that recorded data can be loaded and used

### Common Patterns

#### Pattern 1: Simple Value Recording

```csharp
[TestMethod]
public void TestCalculation()
{
    var result = PerformComplexCalculation();
    
    if (IsRecordMode)
    {
        var expected = new { Value = result, Timestamp = DateTime.Now };
        SaveToJson("TestCalculationExpected.json", expected);
        return;
    }
    
    var expected = LoadFromJson<ExpectedResult>("TestCalculationExpected.json");
    Assert.AreEqual(expected.Value, result, tolerance: 0.001);
    CheckRecordMode();
}
```

#### Pattern 2: HTTP Interaction Recording

```csharp
[TestMethod]
public void WebTestFeature()
{
    var recorder = IsRecordMode && AllowInternetAccess 
        ? new HttpClientTestHelper.HttpInteractionRecorder() 
        : null;
    
    using (recorder != null ? HttpClientTestHelper.BeginRecording(recorder) : null)
    {
        var results = CallWebService();
        
        if (IsRecordMode)
        {
            var data = new TestData
            {
                Results = results,
                HttpInteractions = recorder?.Interactions?.ToList()
            };
            SaveToJson("WebTestFeatureData.json", data);
            return;
        }
        
        var expected = LoadFromJson<TestData>("WebTestFeatureData.json");
        ValidateResults(expected, results);
    }
    
    CheckRecordMode();
}
```

#### Pattern 3: UI Element Recording

```csharp
[TestMethod]
public void TestGraphLabels()
{
    var graph = CreateGraph();
    var labels = ExtractGraphLabels(graph);
    
    if (IsRecordMode)
    {
        SaveToJson("TestGraphLabelsExpected.json", new { Labels = labels });
        return;
    }
    
    var expected = LoadFromJson<ExpectedLabels>("TestGraphLabelsExpected.json");
    Assert.AreEqual(expected.Labels, labels);
    CheckRecordMode();
}
```

### Troubleshooting

**Issue: Test fails with "Set IsRecordMode to false before commit"**

**Solution:** You forgot to set `IsRecordMode => false` after recording. This is intentional - the assertion prevents committing code in recording mode.

**Issue: JSON file not found during validation**

**Solution:** 
- Ensure the file was created during recording (check file exists)
- Verify the path is correct (use `TestContext.GetProjectDirectory()`)
- Check that the file is included in the project (should be visible in Solution Explorer)

**Issue: Recorded data doesn't match validation**

**Solution:**
- Check if the data source changed (web service responses, algorithm behavior)
- Verify you're using the same test data and environment
- Consider if the change is expected (e.g., web service updated their API)
- Re-record if the change is legitimate

**Issue: JSON file is too large**

**Solution:**
- Consider splitting into multiple files
- Use references/IDs instead of duplicating data
- Store large binary data separately (e.g., in `TestData/` folder)
- Compress or summarize data where possible

### Integration with HttpClientTestHelper

The HTTP recording infrastructure built for `HttpClientTestHelper` demonstrates the full pattern:

- **Recording:** `HttpClientTestHelper.BeginRecording()` captures all HTTP requests/responses
- **Storage:** Interactions serialized to JSON with URLs, status codes, response bodies, exceptions
- **Playback:** `HttpClientTestHelper.PlaybackFromInteractions()` replays recorded interactions offline
- **Validation:** Tests run identically in both online (recording) and offline (playback) modes

See `FastaImporterTest.cs` for a complete example of HTTP interaction recording.

---

## Translation-Proof Testing

**CRITICAL**: Skyline is localized into multiple languages (English, Chinese, Japanese, Turkish, French). **All test assertions must work in all locales.**

### The Problem

```csharp
// ❌ BREAKS IN OTHER LOCALES - English-only assertion
Assert.IsTrue(errorMessage.Contains("File not found"));
Assert.AreEqual("Invalid format", exception.Message);
```

These tests pass in English but **fail in Chinese or Japanese** where UI text is translated.

### The Solution

**Always use resource strings for test assertions:**

```csharp
// ✅ WORKS IN ALL LOCALES - Uses same resource string as production code
AssertEx.Contains(errorMessage, Resources.ErrorMessage_FileNotFound);
Assert.AreEqual(Resources.ErrorMessage_InvalidFormat, exception.Message);
```

### Best Practices

1. **Use the same resource strings production code uses**
   - Don't duplicate English text in tests
   - Reference `Resources`, `FileUIResources`, `ToolsUIResources`, etc.
   - Ensures tests validate actual user-visible text

2. **Use HttpClientTestHelper for network errors**
   ```csharp
   using (var helper = HttpClientTestHelper.SimulateHttp404())
   {
       // Production code throws exception
       var errDlg = ShowDialog<AlertDlg>(() => skypSupport.Open(path));

       // Get expected message in current locale
       var expectedError = helper.GetExpectedMessage(uri);
       AssertEx.Contains(errDlg.Message, expectedError);
   }
   ```

3. **Reconstruct expected messages the same way production code does**
   ```csharp
   // Production code builds message like this:
   var message = string.Format(Resources.Template_WithArgs, arg1, arg2);

   // Test validates using same pattern:
   var expected = string.Format(Resources.Template_WithArgs, arg1, arg2);
   Assert.AreEqual(expected, actualMessage);
   ```

### Testing Localization

**All tests must pass in all supported locales:**

- **English** (en-US) - Default
- **Chinese Simplified** (zh-CHS)
- **Japanese** (ja-JP)
- **Turkish** (tr-TR)
- **French** (fr-FR)

**Use SkylineTester.exe or TestRunner.exe to run tests in different locales:**

```bash
# Run tests in Japanese locale
TestRunner.exe /locale:ja-JP

# Run tests in Chinese locale
TestRunner.exe /locale:zh-CHS
```

**TeamCity runs full test suite in all locales** - tests must pass everywhere.

### Common Mistakes to Avoid

```csharp
// ❌ BAD - Hardcoded English
Assert.IsTrue(message.Contains("Download failed"));
Assert.IsTrue(message.Contains("HTTP 401"));
Assert.AreEqual("Success", statusText);

// ✅ GOOD - Resource strings or helper methods
AssertEx.Contains(message, Resources.DownloadFailed);
AssertEx.Contains(message, helper.GetExpectedMessage(uri));
Assert.AreEqual(Resources.Status_Success, statusText);
```

---

## Test Performance Considerations

### Functional Test Overhead

**Functional tests are significantly slower than unit tests:**

- **Unit test**: Milliseconds (in-memory, no UI, no file I/O)
- **Functional test**: Seconds (creates SkylineWindow, loads UI, file operations)

**Each `[TestMethod]` in functional tests incurs overhead:**
1. Create new `SkylineWindow` instance
2. Initialize UI framework
3. Load settings and resources
4. Execute test
5. Tear down and destroy SkylineWindow

**Consolidating validations into a single test method saves significant time:**

```csharp
// ❌ SLOW - 4 tests × overhead = ~40 seconds
[TestMethod] public void Test1() { /* 5 sec test + 5 sec overhead */ }
[TestMethod] public void Test2() { /* 5 sec test + 5 sec overhead */ }
[TestMethod] public void Test3() { /* 5 sec test + 5 sec overhead */ }
[TestMethod] public void Test4() { /* 5 sec test + 5 sec overhead */ }

// ✅ FAST - 1 test × overhead = ~25 seconds
[TestMethod]
public void ConsolidatedTest()
{
    RunFunctionalTest();
}

protected override void DoTest()
{
    Test1Logic();  // 5 sec
    Test2Logic();  // 5 sec
    Test3Logic();  // 5 sec
    Test4Logic();  // 5 sec
}
// Single overhead (~5 sec) instead of 4× overhead
```

### Best Practices for Fast Tests

1. **Consolidate related validations** into single test method
2. **Prefer unit tests** when UI is not required
3. **Minimize file I/O** - use in-memory operations when possible
4. **Share test data** within a test class
5. **Avoid duplicate setup** - use helper methods
6. **See ai/MEMORY.md** for detailed DRY testing examples

### When to Split Tests

**Do split tests when:**
- Tests are logically independent features
- Tests require different test data setup
- One test failing shouldn't block others from running
- Tests are in different test projects (unit vs functional)

**Don't split tests when:**
- Tests validate different aspects of same feature
- Tests can share SkylineWindow instance
- Tests execute sequentially anyway

---

## Code Coverage Validation

### Overview

Code coverage validation using JetBrains dotCover is a critical methodology for ensuring that code changes (especially migrations, refactorings, or new features) are adequately tested. This approach is particularly valuable when working with LLM assistants to validate that automated code changes have complete test coverage.

**Key Benefits:**
- **Verify test coverage** of migrated/refactored code paths
- **Identify untested code** that may need additional tests
- **Validate LLM-generated code** has been properly exercised by existing tests
- **Catch gaps** before merge that could lead to production bugs
- **Document testing rigor** for code reviews and pull requests

### dotCover Setup and Configuration

#### Prerequisites

1. **JetBrains dotCover** (bundled with ReSharper or available standalone)
2. **Visual Studio 2022** with MSTest test adapter
3. **Test projects** configured and building successfully

#### Running Coverage Analysis

**Option 1: Cover All Tests in Solution (Recommended for comprehensive analysis)**

1. In Visual Studio, open **Test Explorer** (`Test` > `Test Explorer`)
2. Right-click on the test project or solution node
3. Select **Cover All Tests with dotCover**
4. Wait for tests to complete (dotCover shows progress)
5. dotCover Coverage window opens automatically

**Option 2: Cover Specific Tests**

1. In Test Explorer, select specific test classes or methods
2. Right-click selection
3. Select **Cover Tests with dotCover**

**Option 3: Cover from Code (for targeted analysis)**

1. Navigate to a test method in the editor
2. Click the unit test icon in the left margin
3. Select **Cover Unit Tests**

#### Exporting Coverage Data

**Export to JSON (for LLM analysis):**

1. After coverage run completes, click the **Export** dropdown in the Coverage window
2. Select **Export to JSON...**
3. Navigate to the appropriate `TestResults` folder:
   - For Skyline: `pwiz_tools/Skyline/TestResults/`
   - For SkylineBatch: `pwiz_tools/Skyline/Executables/SkylineBatch/TestResults/`
   - For AutoQC: `pwiz_tools/Skyline/Executables/AutoQC/TestResults/`
4. Save with naming convention: `<ProjectName>Coverage.json` or simply `Coverage.json`

**Example file locations:**
```
pwiz_tools/Skyline/TestResults/SkylineCoverage.json
pwiz_tools/Skyline/Executables/SkylineBatch/TestResults/SkylineBatchCoverage.json
pwiz_tools/Skyline/Executables/AutoQC/TestResults/AutoQCCoverage.json
```

**Export to HTML (for human visual inspection):**

1. Click **Export** dropdown
2. Select **Export to HTML...**
3. Save to same `TestResults` folder with `.html` extension
4. Open in browser for visual coverage inspection (green = covered, red = uncovered)

### JSON Coverage Format

The JSON export uses a hierarchical structure that is easily parsable by both humans and LLMs:

```json
{
  "DotCoverVersion": "2025.1",
  "Kind": "SolutionRoot",
  "CoveredStatements": 12379,
  "TotalStatements": 26539,
  "CoveragePercent": 47,
  "Children": [
    {
      "Kind": "Project",
      "Name": "PanoramaClient",
      "CoveredStatements": 574,
      "TotalStatements": 1163,
      "CoveragePercent": 49,
      "Children": [
        {
          "Kind": "Namespace",
          "Name": "pwiz.PanoramaClient",
          "Children": [
            {
              "Kind": "Type",
              "Name": "HttpPanoramaRequestHelper",
              "CoveredStatements": 234,
              "TotalStatements": 267,
              "CoveragePercent": 88,
              "Children": [
                {
                  "Kind": "Method",
                  "Name": "DoPost(Uri,string):string",
                  "CoveredStatements": 12,
                  "TotalStatements": 15,
                  "CoveragePercent": 80
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

**Hierarchy:** `Project` → `Namespace` → `Type` (Class) → `Method`/`Property` → (nested members)

**Key Fields:**
- `CoveredStatements`: Number of statements executed during tests
- `TotalStatements`: Total number of executable statements
- `CoveragePercent`: Coverage percentage (0-100)
- `Kind`: Node type (`Project`, `Namespace`, `Type`, `Method`, etc.)
- `Name`: Fully qualified name with signature for methods

### Analysis Workflow

#### For Developers

**1. Identify Critical Code Paths**

After making changes (migration, refactoring, new feature):
- List the modified classes and methods
- Identify the most critical/risky changes
- Determine acceptable coverage thresholds (e.g., 80%+ for critical paths)

**2. Run Coverage Analysis**

- Run tests with dotCover coverage
- Export to both HTML (visual) and JSON (analysis)

**3. Visual Inspection (HTML)**

- Open HTML report in browser
- Navigate to modified classes
- Look for red (uncovered) lines in critical paths
- Assess risk of uncovered code

**4. Share with LLM (JSON)**

- Provide JSON file path to LLM assistant
- Ask for coverage analysis of specific classes/methods
- LLM can parse JSON and identify gaps programmatically

#### For LLM Assistants

**Effective Coverage Analysis Pattern:**

```
Developer: "Please analyze coverage for HttpPanoramaRequestHelper in 
           pwiz_tools/Shared/PanoramaClient/TestResults/PanoramaClientCoverage.json"

LLM: [Uses grep to find the class in JSON]
     [Reads coverage statistics]
     [Identifies uncovered methods]
     [Reports findings with percentages]
     [Recommends additional tests if needed]
```

**Search Strategy:**

1. Use `grep` to find specific class names in JSON:
   ```
   grep '"Name": "HttpPanoramaRequestHelper"' Coverage.json -A 20
   ```

2. Look for methods with `"CoveragePercent": 0` (completely untested)

3. Check critical methods have >80% coverage

4. Report uncovered code paths with context

**Example Analysis Output:**

```
Coverage Analysis for Server.GetSize():

✅ GOOD: PanoramaServerConnector.GetFileInfo() - 90% coverage (54/60 statements)
   - HttpClientWithProgress.DownloadFile() path is well-tested

❌ GAP: Server.GetSize() - 0% coverage (0/14 statements)  
   - HttpClientWithProgress.GetResponseHeadersRead() path is UNTESTED
   - RECOMMENDATION: Add test or document as acceptable risk

Overall Assessment: 1 critical gap found requiring attention.
```

### Use Cases

#### Use Case 1: Validating Code Migration

**Scenario:** Migrating from WebClient to HttpClientWithProgress

**Process:**
1. Complete migration code changes
2. Run all tests (verify they pass)
3. Run dotCover on test suites that exercise the migrated code
4. Export JSON coverage reports
5. Share with LLM: "Analyze coverage of [migrated classes]"
6. LLM identifies any uncovered migration paths
7. Assess risk and add tests if needed
8. Document coverage in PR description

**Example:** See `todos/completed/TODO-20251023_panorama_webclient_replacement.md` for a real-world example of this methodology validating a complete WebClient → HttpClientWithProgress migration.

#### Use Case 2: Validating New Feature

**Scenario:** Adding new public API methods

**Process:**
1. Implement new feature with tests
2. Run coverage on test project
3. Verify new methods show >80% coverage
4. If gaps exist, add tests or document rationale
5. Include coverage report in PR

#### Use Case 3: Regression Prevention

**Scenario:** Refactoring existing code

**Process:**
1. Before refactoring: Run coverage baseline
2. After refactoring: Run coverage again  
3. Compare: Ensure coverage didn't decrease
4. Add tests if coverage dropped

### Best Practices

**DO:**
- ✅ Export both HTML (visual) and JSON (analysis)
- ✅ Store coverage files in project `TestResults/` folders
- ✅ Use consistent naming: `<ProjectName>Coverage.json`
- ✅ Focus on critical paths (new/changed code)
- ✅ Share JSON with LLM for systematic gap identification
- ✅ Document acceptable risks (untested error paths, defensive code)
- ✅ Re-run coverage after adding tests to verify gaps closed

**DON'T:**
- ❌ Aim for 100% coverage (diminishing returns, false security)
- ❌ Commit coverage files to git (large, binary, regenerated frequently)
- ❌ Test defensive/unreachable code just for coverage metrics
- ❌ Ignore 0% coverage on critical new features
- ❌ Skip coverage validation on complex migrations

### Acceptable Coverage Gaps

Not all uncovered code requires tests. Acceptable gaps include:

**1. Defensive Programming**
```csharp
// Unreachable in practice - validates external invariants
if (parameter == null)
    throw new ArgumentNullException(nameof(parameter));
```

**2. Error Paths Hard to Simulate**
```csharp
catch (OutOfMemoryException ex)
{
    // Nearly impossible to test reliably
    Logger.LogCritical(ex);
    throw;
}
```

**3. Legacy Code Not Modified**
- Code untouched by current changes
- Low-risk, stable code with no recent bugs
- Code scheduled for deprecation/removal

**4. Platform-Specific Code**
```csharp
if (Environment.OSVersion.Platform == PlatformID.MacOSX)
{
    // Mac-specific logic, not testable on Windows build server
}
```

**Document these gaps** in PR descriptions or code comments.

### Integration with Workflow

This coverage validation methodology integrates with the workflow documented in `WORKFLOW.md`:

**Workflow 3: Before Creating PR (Coverage Validation)**

1. **Complete code changes** (implementation + tests)
2. **Run full test suite** locally (verify all pass)
3. **Run dotCover coverage** on affected test projects
4. **Export JSON** to `TestResults/<ProjectName>Coverage.json`
5. **Analyze with LLM** (share JSON path, request gap analysis)
6. **Address gaps** (add tests or document acceptable risks)
7. **Create PR** (include coverage summary in description)
8. **Push to TeamCity** (validate on build server)

**PR Description Template (with coverage):**
```markdown
## Changes
- Migrated PanoramaClient from WebClient to HttpClientWithProgress

## Testing
- All existing tests pass (Skyline, SkylineBatch, AutoQC)
- Added comprehensive HttpClient tests (13 upload scenarios)

## Coverage Analysis
- **HttpPanoramaRequestHelper**: 88% coverage (234/267 statements)
- **WebPanoramaClient**: 92% coverage (156/170 statements)
- **Gap identified**: Server.GetSize() 0% coverage (untested helper, low risk)
- **Action taken**: Documented as acceptable risk (rarely used, defensive code)

See: TestResults/PanoramaClientCoverage.json
```

### Troubleshooting

**Issue: dotCover shows 0% coverage for all code**

**Solution:**
- Ensure tests are actually running (check Test Explorer output)
- Verify PDB files are being generated (Debug build configuration)
- Check dotCover isn't filtering out test assemblies

**Issue: JSON export is huge (>10MB)**

**Solution:**
- This is normal for solution-wide coverage
- Focus analysis on specific projects/namespaces
- LLMs can grep specific sections efficiently
- Use HTML for visual overview, JSON for targeted analysis

**Issue: Coverage differs between local and TeamCity**

**Solution:**
- TeamCity may run different test subsets
- Compare test counts (local vs. CI)
- Focus on critical path coverage, not absolute percentages

**Issue: LLM cannot parse JSON structure**

**Solution:**
- Verify JSON is valid (check for truncation)
- Use grep to extract specific sections
- Provide class/method names explicitly for targeted search

---

## Additional Resources

### Documentation
- **`ai/WORKFLOW.md`** - Build, test, and commit workflows (includes AI agent guidelines)
- **`ai/STYLEGUIDE.md`** - Coding conventions and style guidelines
- **`ai/MEMORY.md`** - DRY principles in testing, translation-proof testing, common patterns
- **`ai/todos/completed/TODO-20251010_webclient_replacement.md`** - Detailed WebClient → HttpClient migration patterns

### Source Code
- **`AssertEx.cs`** (`pwiz_tools/Skyline/TestUtil/`) - Full assertion library source
- **`HttpClientTestHelper.cs`** (`pwiz_tools/Skyline/TestUtil/`) - HTTP testing utilities
- **`AbstractFunctionalTest.cs`** (`pwiz_tools/Skyline/TestUtil/`) - Functional test base class (low-level primitives)
- **`AbstractFunctionalTestEx.cs`** (`pwiz_tools/Skyline/TestUtil/`) - Functional test base class (high-level workflow helpers)
- **`AbstractUnitTest.cs`** (`pwiz_tools/Skyline/TestUtil/`) - Unit test base class
- **`AbstractUnitTestEx.cs`** (`pwiz_tools/Skyline/TestUtil/`) - Unit test base class (with data file support)

### Updating Other Documents

Testing information previously in `ai/STYLEGUIDE.md` and `ai/MEMORY.md` has been consolidated here. Those documents now reference `ai/TESTING.md` for comprehensive testing guidelines.

---

## Summary

### Test Project Selection
1. **Test.csproj** - Fast unit tests, no data, no UI
2. **TestData.csproj** - Unit tests with mass spec data
3. **TestFunctional.csproj** - Standard UI functional tests (most common)
4. **TestConnected.csproj** - Tests requiring network access
5. **TestTutorial.csproj** - Automated tutorial validation
6. **TestPerf.csproj** - Large dataset performance tests

### Testing Best Practices
1. **Choose the right test project** based on requirements (data, UI, network)
2. **Choose the right dependency injection pattern** based on call stack depth
3. **Use AssertEx methods** instead of custom assertion wrappers
4. **Use HttpClientTestHelper** for all network failure testing
5. **Never hardcode English error messages** - always use resource strings
6. **Consolidate functional tests** to minimize overhead
7. **Test in all locales** - use SkylineTester.exe for locale-specific testing
8. **Keep test implementations simple** - mock success, use helpers for failures

### Critical Rules
- ❌ **NEVER** use English text literals in assertions
- ❌ **NEVER** parse exception messages for status codes (use structured properties)
- ❌ **NEVER** create multiple `[TestMethod]` functional tests for related validations
- ✅ **ALWAYS** use resource strings for user-facing text validation
- ✅ **ALWAYS** use `HttpClientTestHelper.GetExpectedMessage()` for network errors
- ✅ **ALWAYS** consolidate related functional test validations into private methods
