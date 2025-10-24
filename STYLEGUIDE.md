# Skyline C# coding style

This guide captures Skyline-specific C# conventions to keep AI- and human-authored code consistent with `pwiz_tools/Skyline`.

**Universal AI Guidelines**: This file serves as the comprehensive style guide for all AI tools (Cursor, Claude Code, GitHub Copilot, ChatGPT, etc.). The `.cursorrules` file references this document to avoid duplication.

## Control flow
- If statements must not be single-line. If braces are omitted, keep the condition and body on separate lines.
  
  Bad:
  
  ```csharp
  if (condition) DoThing();
  ```
  
  Good (without braces):
  
  ```csharp
  if (condition)
      DoThing();
  ```
  
  Good (with braces):
  
  ```csharp
  if (condition)
  {
      DoThing();
  }
  ```

## File and member ordering (within a class)
Order members to make high-level logic easy to read first:
1) static variables/fields
2) static public interface methods
3) private instance fields
4) constructor(s)
5) public interface (instance) methods and properties
6) private helper methods

Additional guidance:
- Place private helpers after the public methods that use them; keep helpers close to their primary call sites.
- Avoid "old C" style where helpers appear at the top and the main logic at the bottom.

## General guidelines
- Match surrounding file style (indentation, spacing, line breaks).
- Prefer focused edits; do not reformat unrelated code.
- Keep names descriptive and intention-revealing; avoid abbreviations.
- Keep methods small and cohesive; extract helpers as needed (placed after usage as above).
- **Blank lines should be completely empty** - do not include spaces or tabs for indentation on blank lines.

## Error handling and diagnostics

### Debug.WriteLine for diagnostic logging
Use `Debug.WriteLine()` for diagnostic messages that should only appear during development:
- Add `using System.Diagnostics;` at the top of the file (do not fully qualify)
- Use the standard comment: `// Ignore but log to debug console in debug builds`
- **Always use verbatim string format `$@"..."` instead of `$"..."`** to avoid ReSharper warnings about localizable strings
- Common scenarios:
  - Catch blocks where the error is handled but you want diagnostics
  - Non-critical failures that shouldn't interrupt the user
  - Information useful for debugging but not for production

Example:
```csharp
catch (Exception ex)
{
    // Ignore but log to debug console in debug builds
    Debug.WriteLine($@"Failed to connect: {ex.Message}");
}
```

Note: The `$@""` format (verbatim interpolated string) prevents ReSharper from flagging the text as user-visible and requiring localization.

Benefits:
- Only appears in Debug builds (stripped from Release)
- Visible in Visual Studio Debug Output window
- Provides a convenient breakpoint location
- No performance impact in production

Use alternatives for:
- `Console.WriteLine()` - Command-line tools only
- Exception throwing - Critical errors that must be handled
- Logging frameworks - Production diagnostics that need persistence

### Non-localizable text (debugging only)
Use `$@""` format for strings not intended for user display to avoid ReSharper localization warnings:

**ToString() for debugging:**
```csharp
// Text for debugging only
public override string ToString() => $@"Connection[Host={_host}, Port={_port}]";
```

**Exception messages not shown to users:**
```csharp
// Exception text for internal diagnostics, not displayed to user
throw new InvalidOperationException($@"Invalid state: {_currentState}");
```

Add the comment `// Text for debugging only` or `// Exception text for internal diagnostics, not displayed to user` to document that the string is intentionally non-localized.

## File I/O patterns

### FileSaver for atomic file writes
**Always use `FileSaver` when writing to disk to avoid partially written corrupted files.**

`FileSaver` (from `pwiz.Common.SystemUtil`) implements the atomic file write pattern:
1. Writes to a temporary file (`.tmp` extension)
2. Only renames to the final destination when `Commit()` is called
3. Automatically deletes the temp file if `Dispose()` is called without `Commit()`

This ensures that if a write operation fails (exception, crash, power loss), the destination file is either:
- Completely written (success case)
- Untouched (failure case)

**Never** a partially written corrupted file.

#### Standard usage pattern

```csharp
using (var fileSaver = new FileSaver(destinationPath))
{
    // Write to fileSaver.SafeName (the temp file path)
    File.WriteAllText(fileSaver.SafeName, content);
    
    // Or use with streams
    using (var writer = new StreamWriter(fileSaver.SafeName))
    {
        writer.WriteLine(content);
    }
    
    // Only commit if all writes succeeded
    // If an exception occurs before this, Dispose() auto-cleans the temp file
    fileSaver.Commit();
}
// After using block: destination file now exists with complete content
```

#### With exception handling

```csharp
try
{
    using (var fileSaver = new FileSaver(downloadPath))
    {
        // Perform file operations on fileSaver.SafeName
        DownloadToFile(url, fileSaver.SafeName);
        
        // Only commit if download succeeded
        fileSaver.Commit();
    }
    return true;
}
catch (Exception e)
{
    // No need to manually delete temp file - FileSaver.Dispose() already cleaned it up
    // No need to delete destination - it was never created
    
    // Automatically handles programming defects (reports as bugs) vs user errors (shows MessageDlg)
    ExceptionUtil.DisplayOrReportException(this, e);
    return false;
}
```

**With complex error handling:**

```csharp
try
{
    using (var fileSaver = new FileSaver(downloadPath))
    {
        DownloadToFile(url, fileSaver.SafeName);
        fileSaver.Commit();
    }
    return true;
}
catch (Exception e)
{
    // Re-throw programming defects for bug reporting
    if (ExceptionUtil.IsProgrammingDefect(e))
        throw;
    
    // Complex handling for expected errors (e.g., check HTTP status codes)
    var statusCode = GetHttpStatusCode(e);
    if (statusCode == HttpStatusCode.NotFound)
    {
        MessageDlg.ShowWithException(this, 
            Resources.Error_FileNotFound, e);
    }
    else
    {
        MessageDlg.ShowException(this, e);
    }
    return false;
}
```

#### Key points
- `fileSaver.SafeName` is the temp file path to write to
- Call `fileSaver.Commit()` only after all writes succeed
- `FileSaver` is `IDisposable` - always use with `using` statement
- If `Dispose()` is called without `Commit()`, temp file is automatically deleted
- Destination file is never touched until `Commit()` succeeds
- No need to manually delete files on error paths - `FileSaver` handles cleanup

#### Common mistake to avoid
```csharp
// BAD - directly writes to destination, risks corruption
File.WriteAllText(destinationPath, content);

// GOOD - uses FileSaver for atomic write
using (var fileSaver = new FileSaver(destinationPath))
{
    File.WriteAllText(fileSaver.SafeName, content);
    fileSaver.Commit();
}
```

## Naming conventions (mirrors ReSharper rules)
- Private instance fields: prefix with `_` and use `camelCase` (e.g., `_filePath`).
- Private static fields: prefix with `_` and use `camelCase`.
- Constants (any access): `ALL_CAPS_WITH_UNDERSCORES`.
- Static readonly (any access): `ALL_CAPS_WITH_UNDERSCORES` when used like constants.
- Locals and parameters: `camelCase`.
- Types and namespaces: `PascalCase`.
- Interfaces: `I` prefix (e.g., `IResultSet`).
- Type parameters: `T` prefix (e.g., `TItem`).
- Enum members: `snake_case` (e.g., `not_set`).

## Whitespace and formatting
- Tabs are disallowed; use spaces. Do not change existing files' indentation, but when adding new code use spaces.
- Avoid mixing tabs and spaces. Align with existing file formatting.
- **Line endings**: Use Windows-style line endings (`\r\n`, CRLF) for all files. This is the standard for Windows development and matches the team's development environment. When creating or modifying files, ensure line endings are `\r\n`, not Unix-style `\n` (LF).

## ASCII vs Unicode characters
**Strongly prefer ASCII characters over Unicode in all code and documentation files.** Use simple ASCII alternatives whenever possible to avoid encoding issues, improve compatibility, and simplify text editing.

### Required ASCII usage
- **Hyphens/Dashes**: Use ASCII hyphen-minus `-` (character 45), not em dash (U+2014) or en dash (U+2013)
- **Quotes**: Use ASCII double quote `"` (character 34) and single quote `'` (character 39), not Unicode quotes like curly quotes
- **Apostrophes**: Use ASCII single quote `'` (character 39), not Unicode apostrophe (U+2019)

### General guidance
- When a Unicode symbol has an ASCII or simple text alternative, prefer the ASCII/text version
  - Example: Use "to" or "->" instead of right arrow (U+2192)
  - Example: Use "..." instead of ellipsis (U+2026)
- In code comments and documentation, write out words rather than using Unicode symbols
- LLMs often default to "typographically correct" Unicode punctuation - avoid this in technical files
- Exception: Unicode is acceptable when there is no reasonable ASCII alternative and the character is essential to the meaning

### Why ASCII matters
- **Encoding safety**: ASCII works everywhere without UTF-8 vs Windows-1252 confusion
- **Git compatibility**: Simpler diffs, fewer merge conflicts
- **Editor compatibility**: Works in all text editors, terminals, and build tools
- **Easier to type**: Users can type ASCII characters directly without copy-paste
- **Search/replace**: ASCII characters are easier to find and replace programmatically

## Tools
- We develop with Visual Studio 2022 and ReSharper; aim for warning-free under its inspections.
- Follow the Skyline build guide for environment setup: https://skyline.ms/wiki/home/software/Skyline/page.view?name=HowToBuildSkylineTip

## Executables solutions
Projects under `pwiz_tools/Skyline/Executables` are independent solutions (stand-alone EXEs, developer tools, or utilities included with Skyline). They are not built by `Skyline.sln`. Prefer the same conventions as Skyline unless a local project requires an override.

EditorConfig
- All solutions inherit repository-wide `.editorconfig` for C# naming/formatting.
- If a specific Executables project needs different rules, add a minimal project-level `.editorconfig` or project `.DotSettings` override local to that solution only.

## Resource strings (localization)
- **CRITICAL: ALL user-facing text must be in .resx files - NO string literals in .cs files**
- Add new UI strings for menus/dialogs/pages to `pwiz_tools/Skyline/Menus/MenusResources.resx`.
- Strings will be translated to Chinese/Japanese via our translation process; use clear, concise English.
- Generate resource keys from the English text in a ReSharper-like way:
  - Replace all non-alphanumeric characters with underscores `_`.
  - Collapse sequential underscores into one; trim leading/trailing underscores.
  - Preserve digits; use `PascalCase` word boundaries in the base text where natural.
  - Keys often include the context prefix (e.g., `SkylineWindow_`, `EditMenu_`) followed by the transformed text.
  - Example: "Keyboard Shortcuts" → `Keyboard_Shortcuts`; "File > New" → `File_New`.
- Prefer reusing existing keys when text matches; avoid near-duplicate strings.


## User interface guidelines

### Menu items
- All items in `menuMain` and its submenus should have mnemonics (e.g., `&Keyboard Shortcuts`)
- Menu text and action button text should use title-case (e.g., "Keyboard Shortcuts")
- Only menu items in `menuMain` should have mnemonics and keyboard shortcuts
- Context menus should not have mnemonics or keyboard shortcuts

## File headers and AI attribution

### Standard file header
All source files should include the standard header with copyright and license information:

```csharp
/*
 * Original author: [Author Name] <[email] .at. [domain]>,
 *                  [Affiliation]
 * AI assistance: Cursor ([model names]) <cursor .at. anysphere.co>
 *
 * Copyright [Year] University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
```

### AI attribution guidelines
- **Always include AI assistance line** when code is created or significantly modified with AI tools
- **Specify the tool and models used**: `Cursor (Claude Sonnet 4)` or `Cursor (Claude Sonnet 4, ChatGPT-4)`
- **Use current year** for copyright when creating new files
- **Use .at. format** for email addresses to avoid spam harvesting
- **Position AI assistance** after original author but before copyright
- **Multiple AI tools**: List all tools used if multiple AI systems contributed significantly

### Email domain preferences
- New files: Use `uw.edu` (current UW standard)
- Existing files: Keep existing `u.washington.edu` format (no need to change)
- Both formats are acceptable


## Testing guidelines

**For comprehensive testing guidelines, see `TESTING.md`** which covers:
- Test project structure and selection criteria
- Test execution tools (TestRunner, SkylineTester, SkylineNightly)
- Dependency injection patterns for testing
- AssertEx assertion library
- HttpClientTestHelper for network testing
- Translation-proof testing practices
- Test performance optimization

### Quick reference

**Test project selection:**
- **Test.csproj** - Fast unit tests, no data files, no UI
- **TestData.csproj** - Unit tests with mass spectrometry data
- **TestFunctional.csproj** - UI functional tests (most common)
- **TestConnected.csproj** - Tests requiring network access
- **TestTutorial.csproj** - Automated tutorial validation with screenshot generation
- **TestPerf.csproj** - Performance tests with large datasets (>100MB)

**Test structure:**
- Use `AbstractFunctionalTest` for UI tests
- Use `RunFunctionalTest()` in test methods, implement `DoTest()` for test logic
- Use `RunUI()` for UI operations to avoid cross-thread exceptions
- Use `ShowDialog<T>()` for modal dialogs
- Use `AssertEx` methods instead of custom assertion wrappers
- Consolidate related validations into single test method (avoid per-validation `[TestMethod]` overhead)

**Critical testing rules:**
- ❌ **NEVER** use English text literals in test assertions (breaks localization)
- ❌ **NEVER** parse exception messages for status codes (use structured properties)
- ❌ **NEVER** create multiple `[TestMethod]` functional tests for related validations
- ✅ **ALWAYS** use resource strings for user-visible text validation
- ✅ **ALWAYS** use `AssertEx.Contains()` instead of `Assert.IsTrue(string.Contains())`
- ✅ **ALWAYS** use `HttpClientTestHelper.GetExpectedMessage()` for network error validation
- ✅ **ALWAYS** consolidate functional test validations into private helper methods
