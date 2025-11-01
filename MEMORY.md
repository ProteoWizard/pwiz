# Skyline Project Memory & Context

This document provides essential context for AI tools working on the Skyline project, including architectural patterns, common gotchas, and project scale information.

## Project Scale & Context

### Codebase Size
- **~900,000 lines of code** across multiple languages (C#, C++, JavaScript, etc.)
- **17+ years of evolution** - long-term maintenance is critical
- **8 active developers** working concurrently
- **100+ hours daily** of automated testing across multiple platforms

### Development Environment
- **Primary platform**: Windows with Visual Studio
- **Build system**: Boost.Build with MSVC/GCC support
- **Version control**: Git with GitHub
- **Testing**: Comprehensive automated test suite (unit, functional, performance)
- **Localization**: English, Chinese (Simplified), Japanese

## Common Gotchas

### Threading Guidelines
- **UI thread only** for WinForms operations
- Use `Control.Invoke()` to marshal operations back to UI thread
- Background operations use `ActionUtil.RunAsync()` (NOT async/await keywords)
- Never access UI controls from background threads

### Asynchronous Programming Patterns

#### CRITICAL: No async/await keywords
- **DO NOT use `async`/`await` keywords** in C# code
- Use `ActionUtil.RunAsync()` for background operations in Skyline code
- Use `CommonActionUtil.RunAsync()` only in Common/CommonUtil libraries (no Skyline dependencies)

#### Choosing the right RunAsync
**In Skyline code (`pwiz_tools/Skyline/`):**
- **Use `ActionUtil.RunAsync()`** from `pwiz.Skyline.Util.Extensions`
- Provides proper localization/translation support for resource strings
- Required for code that accesses RESX files during background operations
- Add `using pwiz.Skyline.Util.Extensions;`

**In Common libraries (`pwiz_tools/Shared/`):**
- **Use `CommonActionUtil.RunAsync()`** from `pwiz.Common.SystemUtil`
- For libraries that must not depend on Skyline-specific code
- Does not provide Skyline localization support

### CRITICAL: .resx file workflow
When adding new resource strings to a .resx file, you MUST also add the corresponding public static string properties to the .Designer.cs file. The compiler will fail with CS0117 errors if the Designer.cs file is not updated.

Example workflow:
1. Add `<data name="MyNewString" xml:space="preserve"><value>My New String</value></data>` to .resx
2. Add `public static string MyNewString => ResourceManager.GetString("MyNewString", resourceCulture);` to .Designer.cs
3. Ensure properties are added in alphabetical order
4. Build to verify no CS0117 errors

### Resource Designer File Ordering
- All resource properties in `.Designer.cs` files (e.g., `PropertiesResources.designer.cs`) **must be kept in strict alphabetical order**.
- When adding new resource strings, insert the corresponding property into the correct alphabetical position—not at the end of the file.
- This ensures maintainability and consistency for all contributors.

### Translation-Proof Testing
**NEVER use English text literals in test assertions** - all UI text is localized to Chinese and Japanese, so English-only assertions will break.

**Bad (will break in localized builds):**
```csharp
// ❌ This will fail when UI is in Chinese or Japanese
StringAssert.Contains(messageDlg.Message, "connection");
Assert.IsTrue(errorDlg.Message.Contains("not found"));
```

**Good (translation-proof):**
```csharp
// ✅ Reconstruct expected message from resource strings
var expectedMessage = string.Format(
    MessageResources.HttpClientWithProgress_MapHttpException_Failed_to_connect_to__0___Please_check_your_network_connection__VPN_proxy__or_firewall_,
    "cran.r-project.org");
Assert.AreEqual(expectedMessage, messageDlg.Message);
```

### DRY (Don't Repeat Yourself) - Critical for Long-Term Maintenance
**Skyline has been maintained for over 17 years.** Repetitive code becomes a maintenance nightmare in long-lived projects. We strongly enforce DRY principles.

**Why DRY Matters in Skyline**
- **17+ years of evolution** - Code changes frequently, and repetitive patterns multiply maintenance burden
- **Large codebase** - Duplication compounds across thousands of files
- **Multiple developers** - Inconsistent patterns make code harder to understand and modify
- **Bug fixes** - Must be applied in multiple places, increasing risk of missed locations

**DRY Violations to Avoid**

❌ **Bad - Repetitive Setup Code:**
```csharp
// Test 1
private static void TestDownloadSuccess()
{
    var packages = new Collection<ToolPackage>();
    var installer = ShowDialog<RInstaller>(() => InstallProgram(PPC, packages, false));
    WaitForConditionUI(10 * 1000, () => installer.IsLoaded);
    RunUI(() => installer.TestRunProcess = new TestRunProcess { ExitCode = 0 });
    // ... test logic
}

// Test 2 - DUPLICATED SETUP
private static void TestDownloadFailure()
{
    var packages = new Collection<ToolPackage>();
    var installer = ShowDialog<RInstaller>(() => InstallProgram(PPC, packages, false));
    WaitForConditionUI(10 * 1000, () => installer.IsLoaded);
    RunUI(() => installer.TestRunProcess = new TestRunProcess { ExitCode = 1 });
    // ... test logic
}
```

✅ **Good - DRY with Helper:**
```csharp
// Helper method eliminates duplication
private static RInstaller FormatRInstaller(int installExitCode)
{
    var packages = new Collection<ToolPackage>();
    var installer = ShowDialog<RInstaller>(() => InstallProgram(PPC, packages, false));
    WaitForConditionUI(10 * 1000, () => installer.IsLoaded);
    RunUI(() => installer.TestRunProcess = new TestRunProcess { ExitCode = installExitCode });
    return installer;
}

// Tests focus on behavior, not setup
private static void TestDownloadSuccess()
{
    var installer = FormatRInstaller(installExitCode: 0);
    // ... test logic
}

private static void TestDownloadFailure()
{
    var installer = FormatRInstaller(installExitCode: 1);
    // ... test logic
}
```

**DRY Patterns in Skyline**

1. **Test Setup Helpers**
   - Extract common dialog setup into helper methods
   - Use parameters to customize behavior (exit codes, connection states, etc.)
   - Place helpers after the tests that use them

2. **Resource String Reconstruction**
   - Reconstruct expected messages from resource strings instead of hardcoding
   - Use `string.Format()` with the same resource strings as production code
   - Ensures tests work in all languages (English, Chinese, Japanese)

3. **Common UI Patterns**
   - Extract repeated UI interaction patterns
   - Use consistent naming for similar operations across different dialogs
   - Create reusable validation methods

4. **Exception Handling**
   - Use centralized exception mapping (`MapHttpException`, `IsProgrammingDefect`)
   - Avoid duplicating exception classification logic
   - Standardize error message construction

**When to Extract Helpers**

Extract when you see:
- 3+ lines of identical code across multiple methods
- Similar patterns with only parameter differences
- Repeated resource string lookups or formatting
- Duplicated validation logic
- Common UI interaction sequences

Don't extract:
- Single-use code (unless it's clearly a future pattern)
- Trivial one-liners
- Code that's only similar but not identical

**Maintenance Benefits**

Before DRY:
- Change requires editing 8 places
- Risk of missing locations
- Inconsistent behavior across similar scenarios
- Harder to understand overall patterns

After DRY:
- Change requires editing 1 place
- Impossible to miss locations
- Consistent behavior guaranteed
- Clear, focused test logic

**Remember:** In a 17-year-old project, every line of duplicated code is a future maintenance burden. Be ruthless about eliminating repetition.

### Exception Handling Patterns
- Use centralized exception mapping (`MapHttpException`, `IsProgrammingDefect`)
- Avoid duplicating exception classification logic
- Standardize error message construction
- Use `UserMessageException` base class for user-facing exceptions
- Distinguish between programming errors (re-thrown) and user-actionable errors (displayed)

### File and Member Ordering
Order members to make high-level logic easy to read first:
1. static variables/fields
2. static public interface methods
3. private instance fields
4. constructor(s)
5. public interface (instance) methods and properties
6. private helper methods

Place private helpers after the public methods that use them; keep helpers close to their primary call sites.

### Build System
- **Use `quickbuild.bat` on Windows** - do not introduce new build systems
- **Respect existing indentation** - avoid unrelated reformatting
- **Keep code style intact** - do not reformat unrelated files; preserve tabs/spaces
- **Update appropriate Jamfile or Visual Studio project** when adding sources
- **Use existing vendor libraries** from `libraries/`; do not fetch replacements

## Project Structure

### Key Directories
- `pwiz/` - Core C++ libraries and tools
- `pwiz_tools/Skyline/` - Main Skyline application (C#)
- `pwiz_tools/Shared/` - Shared utilities and common code
- `libraries/` - Third-party dependencies
- `build-nt-x86/msvc-release-x86_64/` - Build outputs

### Entry Points
- `quickbuild.bat` - Main build script
- `pwiz.sln` - Visual Studio solution
- `pwiz_tools/Skyline/Skyline.sln` - Skyline-specific solution
- `doc/index.html` - Documentation

### Testing
- **Unit tests**: `Test` and `TestData` projects (fast, no UI)
- **Functional tests**: `TestFunctional`, `TestPerf`, `TestTutorial` projects (UI required)
- **Performance tests**: Large datasets, run less frequently
- **Tutorial tests**: Automated implementation of documentation tutorials

## LLM-Specific Guidelines

### Context Management
- Always read `TODO-YYYYMMDD.md` for current branch context
- Update TODO file with every commit
- Remove TODO file before merging to master
- Follow branch naming conventions: `Skyline/work/YYYYMMDD_description`

### Code Quality
- Match surrounding file style exactly
- Prefer focused edits over broad refactoring
- Keep methods small and cohesive
- Extract helpers when duplication exceeds 3 lines
- Use descriptive, intention-revealing names

### Testing Requirements
- All new code must have appropriate tests
- Use translation-proof assertions
- Follow DRY principles in test code
- Prefer functional tests for UI features
- Use unit tests for pure logic

### Documentation
- Update relevant documentation files
- Add concise notes to `README.md` and `doc/` when needed
- Use XML documentation for public APIs
- Keep comments focused and meaningful
