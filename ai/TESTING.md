# Testing Guidelines - Quick Reference

Essential testing patterns and rules. See [ai/docs/testing-patterns.md](docs/testing-patterns.md) for comprehensive details.

## Test Project Selection

| Project | Purpose | Base Class | When to Use |
|---------|---------|-----------|-------------|
| **Test.csproj** | Unit tests | `AbstractUnitTest` | Fast tests, no UI, no data files |
| **TestData.csproj** | Unit tests with data | `AbstractUnitTestEx` | Tests needing mass spec data files |
| **TestFunctional.csproj** | UI tests | `AbstractFunctionalTestEx` | **Most common** - UI workflows |
| **TestConnected.csproj** | Network tests | varies | Tests requiring real network access |
| **TestTutorial.csproj** | Tutorial automation | `AbstractFunctionalTestEx` | Automated tutorial validation |
| **TestPerf.csproj** | Performance tests | `AbstractFunctionalTestEx` | Large datasets (>100MB) |

## Critical Testing Rules

See [ai/CRITICAL-RULES.md](CRITICAL-RULES.md) for the full list. Key rules:

### Build Before Testing
- **ALWAYS** build after edits and before running tests – the test harness executes the last compiled binaries, not your unbuilt source.

### Translation-Proof Testing
- **NEVER** use English text literals in test assertions
- **ALWAYS** use resource strings from production code
- **ALWAYS** use `AssertEx.Contains()` instead of `Assert.IsTrue(string.Contains())`
- **ALWAYS** use `HttpClientTestHelper.GetExpectedMessage()` for network errors

### Test Structure
- **NEVER** create multiple `[TestMethod]` for related validations (causes overhead)
- **ALWAYS** consolidate validations into single test with private helper methods
- Use `RunFunctionalTest()` pattern with `DoTest()` override
- Place helper methods after the tests that use them

### Test Performance
- Functional tests have significant overhead (create/destroy SkylineWindow)
- Consolidating 4 separate tests into 1 can save 15+ seconds
- Prefer unit tests when UI is not required

## Common Patterns

### Functional Test Structure
```csharp
[TestMethod]
public void MyFeatureTest()
{
    RunFunctionalTest();
}

protected override void DoTest()
{
    TestStep1();
    TestStep2();
    TestStep3();
}

private void TestStep1() { /* validation */ }
private void TestStep2() { /* validation */ }
```

### Translation-Proof Assertions
```csharp
// ✅ GOOD - Works in all locales
AssertEx.Contains(errorMessage, Resources.ErrorMessage_FileNotFound);

// ❌ BAD - Breaks in Chinese/Japanese
Assert.IsTrue(errorMessage.Contains("File not found"));
```

### Network Error Testing
```csharp
// ✅ GOOD - HttpClientTestHelper provides expected message
using (var helper = HttpClientTestHelper.SimulateHttp404())
{
    var errDlg = ShowDialog<AlertDlg>(() => operation());
    var expectedError = helper.GetExpectedMessage(uri);
    AssertEx.Contains(errDlg.Message, expectedError);
}

// ❌ BAD - Hardcoded English
Assert.IsTrue(errDlg.Message.Contains("HTTP 404"));
```

## Base Classes

### AbstractFunctionalTestEx (Prefer This)
High-level workflow helpers for common operations:
- `ImportResultsFile()` - Import with all dialogs
- `ExportReport()` - Export with configuration
- `ShareDocument()` - Share to Panorama
- `ImportPeptideSearch()` - Import with wizard
- Many more...

### AbstractFunctionalTest (Low-Level)
Only use when you need fine-grained control:
- `RunUI()` - Execute on UI thread
- `ShowDialog<T>()` - Show modal dialog
- `WaitForCondition()` - Poll for conditions
- Basic primitives only

## Dependency Injection for Testing

### Pattern 1: Constructor Injection
Use when tests construct the object directly:
```csharp
public SkypSupport(SkylineWindow skyline, Func<...> clientFactory)
{
    _clientFactory = clientFactory;
}
```

### Pattern 2: Static Test Seam + IDisposable
Use for deep call stacks (3+ layers):
```csharp
public class TestToolStoreClient : IToolStoreClient, IDisposable
{
    public TestToolStoreClient()
    {
        _original = ToolStoreUtil.ToolStoreClient;
        ToolStoreUtil.ToolStoreClient = this;  // Inject
    }

    public void Dispose()
    {
        ToolStoreUtil.ToolStoreClient = _original;  // Restore
    }
}
```

## AssertEx Quick Reference

Prefer `AssertEx` methods over custom wrappers:
- `AssertEx.Contains(actualString, expectedSubstring)`
- `AssertEx.FileExists(filePath)`
- `AssertEx.ThrowsException<TEx>(() => code)`
- `AssertEx.Serializable<T>(obj)`
- `AssertEx.NoDiff(expected, actual)`

See `pwiz_tools/Skyline/TestUtil/AssertEx.cs` for full API.

## Localization Testing

All tests must pass in all locales:
- English (en-US)
- Chinese Simplified (zh-CHS)
- Japanese (ja-JP)
- Turkish (tr-TR)
- French (fr-FR)

Use `SkylineTester.exe` or `TestRunner.exe /locale:ja-JP` to test in different locales.

## See Also

- [ai/docs/testing-patterns.md](docs/testing-patterns.md) - Comprehensive testing guide
- [ai/CRITICAL-RULES.md](CRITICAL-RULES.md) - All critical testing constraints
- [ai/MEMORY.md](MEMORY.md) - DRY principles in testing
- [ai/WORKFLOW.md](WORKFLOW.md) - Build and test workflows
