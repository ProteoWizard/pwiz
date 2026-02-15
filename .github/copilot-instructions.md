# GitHub Copilot Code Review Instructions - Skyline Project

## Project Context

Skyline is a 17-year-old mature C# codebase for proteomics mass spectrometry data analysis. The project maintains strict conventions for maintainability, localization (English UI with Chinese/Japanese translations), and zero-warning builds. Tests run in multiple locales (English, French, Turkish) to verify number formatting and culture-invariant string operations.

## Critical Review Focus Areas

### 1. Asynchronous Programming (CRITICAL)

**NEVER accept `async`/`await` keywords in C# code.**

- Skyline uses `ActionUtil.RunAsync()` for background operations in `pwiz_tools/Skyline/`
- Common libraries (`pwiz_tools/Shared/`) use `CommonActionUtil.RunAsync()`
- Background threads communicate with UI thread via `Control.Invoke()` or `Control.BeginInvoke()`
- Flag any PR introducing `async`, `await`, or `Task<T>` return types
- Exception: External library integrations where async is unavoidable must be wrapped

**Why**: Historical STA threading model and UI synchronization requirements.

**Benefits of `RunAsync()` pattern**:
- Automatic `CurrentCulture`/`CurrentUICulture` propagation for localization testing
- Exception handling wrapper prevents unhandled exceptions from crashing the application
- Consistent threading model across the codebase

**Pattern**:
```csharp
// ✅ ACCEPT - RunAsync with UI updates via Invoke
ActionUtil.RunAsync(() =>
{
    var result = PerformBackgroundWork();
    
    // Update UI on UI thread
    control.BeginInvoke((Action)(() =>
    {
        textBox.Text = result;
    }));
});

// ❌ REJECT - async/await
public async Task DoWorkAsync()
{
    await Task.Run(() => PerformBackgroundWork());
}
```

### 2. Localization and Resource Strings (CRITICAL)

**ALL user-facing text must be in .resx files, NEVER string literals.**

```csharp
// ❌ REJECT - String literal for UI
MessageBox.Show("File not found");
throw new IOException("Cannot open file");

// ✅ ACCEPT - Resource string
MessageBox.Show(Resources.ErrorMessage_FileNotFound);
throw new IOException(Resources.ErrorMessage_CannotOpenFile);
```

**Flag**:
- String literals in MessageBox, exception messages shown to users, dialog text, menu items
- Missing `.Designer.cs` updates when `.resx` is modified
- Resource properties not in alphabetical order

**Exception**: Debug-only strings using `$@""` format are acceptable (e.g., `Debug.WriteLine`, `ToString()`, internal exception messages never shown to users).

### 3. Translation-Proof Testing (CRITICAL)

**NEVER accept English text literals in test assertion comparisons.**

```csharp
// ❌ REJECT - Hardcoded English in comparison
Assert.IsTrue(errorMessage.Contains("File not found"));
Assert.AreEqual("Error", dialogTitle);

// ✅ ACCEPT - Resource-based comparison
AssertEx.Contains(errorMessage, Resources.ErrorMessage_FileNotFound);
AssertEx.AreEqual(Resources.DialogTitle_Error, dialogTitle);

// ✅ ACCEPT - English failure message (acceptable and desirable)
Assert.AreEqual(expected, actual, "Skyline version should match configuration");
AssertEx.Contains(errorMessage, expectedSubstring, "Error message should contain expected fragment");
```

**Flag**:
- English string literals used in **comparisons** (the `expected` or `substring` arguments)
- Direct `.Contains()` instead of `AssertEx.Contains()`
- Network error assertions without `HttpClientTestHelper.GetExpectedMessage()`

**Do NOT flag**:
- English text in assertion failure messages (the optional message parameter)
- English comments explaining test logic

**Why**: Tests must pass in multiple UI languages (English, Chinese, Japanese) and locales (English, French, Turkish). The *comparison values* come from localized resources, but *failure messages* remain in English for developer clarity.

### 4. Test Structure and Performance

**REJECT multiple `[TestMethod]` for related validations.**

```csharp
// ❌ REJECT - Separate test methods (causes 15+ sec overhead each)
[TestMethod] public void TestImportStep1() { }
[TestMethod] public void TestImportStep2() { }
[TestMethod] public void TestImportStep3() { }

// ✅ ACCEPT - Consolidated test with private helpers
[TestMethod]
public void TestImportWorkflow()
{
    RunFunctionalTest();
}

protected override void DoTest()
{
    TestImportStep1();
    TestImportStep2();
    TestImportStep3();
}

private void TestImportStep1() { /* validation */ }
private void TestImportStep2() { /* validation */ }
private void TestImportStep3() { /* validation */ }
```

**Why**: Functional test overhead (SkylineWindow creation) is 5-10 seconds per test method.

### 5. Code Quality Standards

**Require zero ReSharper warnings before merge.**

- All PRs must build with zero warnings (Visual Studio + ReSharper)
- Flag commented-out code (should be removed)
- Flag reformatting of unrelated code (scope creep)
- Flag unused using directives, unused variables, unreachable code

**Exception**: Suppress warnings only when justified with `// ReSharper disable once` comment explaining why.

### 6. Naming Conventions

**Enforce consistent naming:**

- Private fields: `_camelCase`
- Constants: `ALL_CAPS_WITH_UNDERSCORES`
- Types/namespaces: `PascalCase`
- Interfaces: `IPascalCase`
- Enum members: `snake_case`
- Locals/parameters: `camelCase`

**Flag**: Inconsistent naming, especially public API changes.

### 7. DRY Principle (Don't Repeat Yourself)

**Flag code duplication exceeding 3 lines.**

```csharp
// ❌ REJECT - Repeated logic
if (condition1)
{
    DoSetup();
    DoWork();
    DoCleanup();
}
if (condition2)
{
    DoSetup();
    DoWork();
    DoCleanup();
}

// ✅ ACCEPT - Extracted helper
if (condition1 || condition2)
{
    PerformWorkflow();
}

private void PerformWorkflow()
{
    DoSetup();
    DoWork();
    DoCleanup();
}
```

**Why**: 17-year-old codebase with extensive duplication is a maintenance burden.

### 8. File Format and Style

**Require**:
- CRLF line endings (Windows standard)
- Spaces, not tabs
- Blank lines completely empty (no spaces/tabs)
- Prefer ASCII over Unicode characters

**Ordering**:
1. Using directives (System first, then external, then project)
2. Static variables/fields
3. Static public methods
4. Private instance fields
5. Constructor(s)
6. Public methods/properties
7. Private helper methods (after methods that use them)

### 9. Control Flow Style

**Reject single-line if statements:**

```csharp
// ❌ REJECT
if (condition) DoThing();

// ✅ ACCEPT
if (condition)
    DoThing();

// ✅ ACCEPT (preferred for multi-line bodies)
if (condition)
{
    DoThing();
    DoAnotherThing();
}
```

### 10. Build System Integrity

**REJECT**:
- Introduction of new build systems (npm, gradle, cargo, etc.)
- Changes to `.sln` or `.csproj` without corresponding source file additions
- Modifications to `Jamfile` without justification

**WHY**: Windows-focused project uses `quickbuild.bat` and MSBuild exclusively.

## Review Workflow Recommendations

1. **Check for async/await first** - Most critical violation
2. **Scan for string literals in UI code** - Localization check
3. **Review test files for English assertions** - Translation-proof check
4. **Look for multiple `[TestMethod]` in functional tests** - Performance check
5. **Verify zero warnings claim** - Build quality check
6. **Check for code duplication** - DRY principle check
7. **Validate file format (CRLF, spaces)** - Style check
8. **Ensure helpers placed after callers** - Ordering check

## Suggested Review Comments

**Async/await**:
> "Skyline does not use `async`/`await`. Please refactor to use `ActionUtil.RunAsync()` (or `CommonActionUtil.RunAsync()` for shared libraries)."

**String literals in UI**:
> "User-facing text must be in .resx files for localization. Add this string to `MenusResources.resx` and reference via `Resources.PropertyName`."

**Hardcoded test strings**:
> "Test assertion comparisons must be translation-proof. Use resource strings and `AssertEx.Contains()` instead of hardcoded English text in the expected/substring argument. Note: English failure messages (the optional message parameter) are acceptable."

**Multiple test methods**:
> "Consolidate related test validations into a single `[TestMethod]` with private helper methods to avoid functional test overhead (5-10 sec per method)."

**Code duplication**:
> "Extract a helper method for this repeated logic (DRY principle). Place the helper after the public method that uses it."

**Warnings**:
> "This PR introduces ReSharper warnings. Please fix all warnings before merge (zero-warning policy)."

## Out of Scope for Review

**Do not flag**:
- Lack of XML documentation (not enforced project-wide)
- Verbose variable names (Skyline prefers clarity)
- Long methods (common in UI code with many form controls)
- Patterns from legacy code (don't require refactoring unless touched)

## Additional Context

- **Primary IDE**: Visual Studio 2022 + ReSharper
- **Target Framework**: .NET Framework 4.8 (Windows only)
- **Test Runner**: Custom `TestRunner.exe` (not MSTest/NUnit directly)
- **UI Languages**: English (primary), Chinese Simplified, Japanese
- **Test Locales**: English, French (number format), Turkish (case conversion edge cases)
- **Build Time**: ~5 min full rebuild, ~30 sec incremental

---

**When in doubt, prioritize**:
1. No async/await
2. No string literals in UI code
3. No English string literals in test assertion comparisons (failure messages are fine)
4. Consolidate test methods
5. Zero warnings
