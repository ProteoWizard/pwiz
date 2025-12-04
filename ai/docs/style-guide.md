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

## Using directive ordering

**System and Windows namespaces come first** (not strictly alphabetical). This matches the ReSharper setting: "Place 'System.*' and 'Windows.*' namespaces first when sorting 'using' directives".

### Correct ordering
```csharp
// ✅ GOOD - System namespaces first, then external libraries, then project namespaces
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Model;
using Formatting = Newtonsoft.Json.Formatting;  // Aliases at the end
```

### Incorrect ordering
```csharp
// ❌ BAD - External libraries before System namespaces
using Microsoft.VisualStudio.TestTools.UnitTesting;  // Should come after System
using Newtonsoft.Json;
using System;  // System should come first
using System.Collections.Generic;
using pwiz.Common.SystemUtil;
```

```csharp
// ❌ BAD - Strictly alphabetical (doesn't match ReSharper setting)
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using pwiz.Common.SystemUtil;  // Project namespace before System
using pwiz.Skyline;
using System;  // System namespace should come first
using System.Collections.Generic;
```

**Why this matters:**
- ReSharper is configured to place `System.*` and `Windows.*` namespaces first
- AI tools may sort alphabetically by default, which conflicts with this convention
- Consistent ordering makes code reviews easier and matches developer expectations

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

### User-facing exception messages vs diagnostic details

**Principle: Exception.Message for users, InnerException for developers**

When creating exceptions that will be shown to users, follow this pattern:
- **`Exception.Message`**: Simple, actionable message the user can understand and act on
- **`InnerException`**: Technical details, stack traces, and diagnostic information for developers

**Why this matters:**

Most error dialogs show `Exception.Message` with a "More Info" button that expands to show the full exception chain and stack trace. Users see the simple message; developers who need to diagnose issues can click "More Info" and use the "Copy" button to send us the full diagnostic details.

**Good example:**
```csharp
try
{
    httpClient.DownloadFile(uri, fileName);
}
catch (HttpRequestException httpEx)
{
    // User-friendly message in outer exception
    throw new NetworkRequestException(
        "The request to panoramaweb.org timed out. Please try again.",  // User sees this
        NetworkFailureType.Timeout,
        uri,
        httpEx);  // Technical details in InnerException (for "More Info")
}
```

**What users see in MessageDlg:**
```
The request to panoramaweb.org timed out. Please try again.
[More Info button] [Copy button]
```

**What developers see after clicking "More Info":**
```
NetworkRequestException: The request to panoramaweb.org timed out. Please try again.
  at HttpClientWithProgress.MapHttpException(...)
  InnerException: TaskCanceledException: A task was canceled.
    at System.Net.Http.HttpClient.SendAsync(...)
```

**When we have both LabKey errors and HTTP errors:**
```csharp
// LabKey provides specific server-side error
var labKeyError = new LabKeyError("File name contains invalid characters", 400);

// Don't duplicate with generic HTTP message
// BAD:  "Error: Response status code does not indicate success: 400 (Bad Request)"
//       "LabKey Error: File name contains invalid characters"
// GOOD: "Error: File name contains invalid characters"
//       "Response status: 400"

throw new PanoramaServerException(
    errorMessageBuilder
        .LabKeyError(labKeyError)      // User sees this
        .Uri(uri)
        .ToString(),
    networkRequestException);          // Full HTTP details in InnerException
```

**Guidelines:**
- ✅ `Exception.Message`: What the user can understand ("Server is offline", "Invalid credentials", "File not found")
- ✅ Include actionable guidance ("Please check your network connection", "Go to Tools > Options to update password")
- ❌ Don't include technical jargon in Message ("Response status code does not indicate success: 500 (Internal Server Error)")
- ❌ Don't include implementation details ("HEAD request failed", "TaskCanceledException thrown")
- ✅ Put all technical details in `InnerException` chain
- ✅ When wrapping exceptions, preserve the original as `InnerException`
- ✅ When you have a server-specific error (LabKey, Panorama), prefer it over generic HTTP status messages

**Exception hierarchy for diagnostics:**
```
PanoramaServerException: "File was not uploaded to the server..."
  ↓ InnerException
  NetworkRequestException: "The server panoramaweb.org encountered an error (HTTP 500)..."
    ↓ InnerException
    HttpRequestException: "Response status code does not indicate success: 500 (Internal Server Error)"
      ↓ InnerException (if applicable)
      SocketException: "The remote server closed the connection"
```

Users see the top-level message. Developers troubleshooting can inspect the entire chain via "More Info".

### Error messages should explain cause AND solution

**Principle: Tell users what went wrong AND how to fix it**

Good error messages have two parts:
1. **What happened** (the cause/problem)
2. **What to do about it** (the solution/next steps)

**Good examples:**
```csharp
// Network error with actionable guidance
"No network connection detected. Please check your internet connection and try again."

// Missing configuration with clear instructions
"The server {0} is not in your list of Panorama servers.\n\n" +
"Use Tools > Options > Panorama to add the server to your settings."

// Permission error with remediation steps
"Access to {0} was denied (HTTP 403).\n\n" +
"Contact your Panorama administrator to request upload permissions for this folder."

// Validation error with what to change
"File name contains invalid characters.\n\n" +
"Remove characters like < > : \" / \\ | ? * from the file name."
```

**Bad examples:**
```csharp
// BAD - Only states the problem, no guidance
"Server not found."

// BAD - Technical jargon without user-friendly explanation
"HTTP 403 Forbidden."

// BAD - Vague, doesn't help user know what to do
"An error occurred."
```

**Guidelines:**
- ✅ First sentence: What went wrong in user-friendly terms
- ✅ Second part: Clear, specific steps to resolve the issue
- ✅ Use "Please..." or "Use..." for actionable instructions
- ✅ Reference specific UI paths when helpful ("Tools > Options > Panorama")
- ✅ For permission errors, tell them who to contact
- ✅ For validation errors, show examples of what's acceptable
- ❌ Don't leave users wondering "what do I do now?"
- ❌ Don't assume users understand technical error codes
- ❌ Don't just throw the exception message at them without context

**Pattern for RESX strings:**
```xml
<!-- Good: Cause + Solution -->
<data name="Error_ServerNotInList" xml:space="preserve">
  <value>The server {0} is not in your list of Panorama servers.

Use Tools &gt; Options &gt; Panorama to add the server to your settings.</value>
</data>

<!-- Good: Problem + What to check -->
<data name="Error_NoNetworkConnection" xml:space="preserve">
  <value>No network connection detected.

Please check your internet connection and try again.</value>
</data>
```

**Future enhancement consideration:**
When error messages suggest manual steps (like "Use Tools > Options"), consider whether you can offer to do it for the user:
```csharp
// CONSIDER: Instead of just showing error, offer to fix it
var result = MultiButtonMsgDlg.Show(this,
    "The server {0} is not in your list...\n\nWould you like to add it now?",
    "Add Server", "Cancel");
if (result == DialogResult.OK)
    ShowAddServerDialog(serverUrl);
```

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

**Prefer `ExceptionUtil.DisplayOrReportException()` over manual checks:**

```csharp
// GOOD - DRY, concise, handles programming defects automatically
catch (Exception e)
{
    ExceptionUtil.DisplayOrReportException(this, e);
    return false;
}

// AVOID - Repetitive pattern that DisplayOrReportException() already handles
catch (Exception e)
{
    if (ExceptionUtil.IsProgrammingDefect(e))
        throw;
    MessageDlg.ShowWithException(this, e.Message, e);
    return false;
}
```

**With complex error handling (when you need custom messages):**

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
    
    // Custom handling for specific error types (when generic message isn't appropriate)
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

## Common utility classes

**Before implementing custom text manipulation functions, check if `TextUtil` already provides what you need.**

`TextUtil` (from `pwiz.Skyline.Util.Extensions`) is a commonly used utility class for text operations throughout the Skyline codebase. Using existing utilities follows DRY principles and leverages well-tested code.

### TextUtil for text manipulation

#### Reading lines from strings
```csharp
// ❌ BAD - Custom implementation (duplicates existing functionality)
private static List<string> SplitIntoLines(string text)
{
    var lines = new List<string>();
    // ... 30+ lines of custom parsing logic
    return lines;
}

// ✅ GOOD - Use existing TextUtil extension method
using pwiz.Skyline.Util.Extensions;

var lines = text.ReadLines().ToList();
```

`TextUtil.ReadLines()` is an extension method that:
- Handles both `\r\n` (Windows) and `\n` (Unix) line endings automatically
- Uses `StringReader.ReadLine()` internally (well-tested .NET API)
- Returns `IEnumerable<string>` (can be converted to `List` with `.ToList()`)

#### Joining lines and values
```csharp
// ✅ Join lines with newline separators
var multiLineText = TextUtil.LineSeparate("line1", "line2", "line3");
// Result: "line1\nline2\nline3"

// ✅ Join values with space separators
var spacedValues = TextUtil.SpaceSeparate("value1", "value2", "value3");
// Result: "value1 value2 value3"
```

#### CSV/TSV/DSV (Delimiter-Separated Values) operations
`TextUtil` provides locale-sensitive CSV/TSV handling:

```csharp
// ✅ Get locale-appropriate CSV separator (comma or semicolon)
char separator = TextUtil.CsvSeparator;  // Uses current culture
char separator = TextUtil.GetCsvSeparator(cultureInfo);  // For specific culture

// ✅ Safely write DSV fields (handles escaping, quotes, newlines)
writer.WriteDsvField(text, separator);

// ✅ Convert string to safe DSV field format
string safeField = text.ToDsvField(separator);
```

**Key features:**
- **Locale-aware**: Automatically uses semicolon (`;`) in locales where comma is the decimal separator (e.g., German, French)
- **Proper escaping**: Handles quotes, separators, and newlines in field values
- **File dialog filters**: `TextUtil.FILTER_CSV` and `TextUtil.FILTER_TSV` for file dialogs

#### Other TextUtil utilities
- **Constants**: `TextUtil.HYPHEN`, `TextUtil.SPACE`, `TextUtil.EXT_CSV`, `TextUtil.EXT_TSV`, etc.
- **Parsing helpers**: DSV field parsing, CSV/TSV reading
- **String manipulation**: Various text transformation utilities

**When to use TextUtil:**
- ✅ Reading lines from strings (instead of custom `SplitIntoLines()`)
- ✅ Joining lines or values with separators
- ✅ Working with CSV/TSV files (locale-aware, proper escaping)
- ✅ Any text manipulation that might already be implemented

**When NOT to use TextUtil:**
- ❌ Simple string operations that don't need special handling (use standard .NET `string` methods)
- ❌ Operations specific to a single use case with no reuse potential

**Finding TextUtil:**
- **Namespace**: `pwiz.Skyline.Util.Extensions`
- **File**: `pwiz_tools/Skyline/Util/Extensions/Text.cs`
- **Add using**: `using pwiz.Skyline.Util.Extensions;`

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


## Comments and XML documentation

### Comment style
Comments should generally start with a capital letter, especially if they are essentially imperative sentences. True sentences should end with a period.

```csharp
// ❌ BAD - lowercase start, no period
// avoid duplicate consecutive entries
// split on first colon only
// a hit - now use the replacement expression

// ✅ GOOD - capital start, period for sentences
// Avoid duplicate consecutive entries.
// Split on first colon only.
// A hit - now use the replacement expression to get the ProteinMetadata parts.
```

**Guidelines:**
- **Imperative comments** (instructions/commands): Start with capital letter, end with period
  - `// Ensure at least one response if connection is good.`
  - `// Split on first colon only.`
- **Descriptive comments** (explanations): Start with capital letter, end with period if a complete sentence
  - `// This will be the input GI number, or GI equivalent of input.`
  - `// Useful for disambiguation of multiple responses.`
- **Short phrases**: Capital letter, period optional for very short phrases
  - `// A better read on name.` or `// A better read on name`

### XML documentation comments
When referencing class names in XML documentation, use `<see cref="ClassName">` for proper IntelliSense linking.

```csharp
// ❌ BAD - plain text class name
/// <summary>
/// Like the actual WebEnabledFastaImporter.WebSearchProvider,
/// but just notes search terms instead of actually going to the web
/// </summary>

// ✅ GOOD - use cref for class references
/// <summary>
/// Like the actual <see cref="WebEnabledFastaImporter.WebSearchProvider"/>,
/// but just notes search terms instead of actually going to the web.
/// </summary>
```

**Benefits:**
- IntelliSense provides clickable links to the referenced class
- Refactoring tools can update references automatically
- Better IDE navigation and documentation generation

### Return-only documentation
Often the most important part of a function to document is what it returns. It's valid to put just that in the `<summary>` tag without `<param>` or `<returns>` tags.

```csharp
// ✅ GOOD - return-only summary, no empty tags
/// <summary>
/// Returns a number between 0.0 and 1.0 indicating how bright the color is.
/// </summary>
public double GetBrightness(Color color) { ... }

// ❌ BAD - empty <param> and <returns> tags add no value
/// <summary>
/// Returns a number between 0.0 and 1.0 indicating how bright the color is.
/// </summary>
/// <param name="color"></param>
/// <returns></returns>
public double GetBrightness(Color color) { ... }

// ❌ BAD - <returns> tag just repeats the summary
/// <summary>
/// Returns a number between 0.0 and 1.0 indicating how bright the color is.
/// </summary>
/// <returns>A number between 0.0 and 1.0 indicating how bright the color is.</returns>
public double GetBrightness(Color color) { ... }
```

**Guidelines:**
- ✅ **Return-only summary**: If the summary explains what the function returns, omit `<param>` and `<returns>` tags
- ✅ **Parameter documentation**: Only include `<param>` tags when they add value beyond what's clear from the parameter name
- ❌ **Don't include empty tags**: Empty `<param>` or `<returns>` tags add no value and should be removed
- ❌ **Don't repeat information**: If `<returns>` just repeats the summary, omit it
- **ReSharper behavior**: ReSharper only warns when you have `<param>` tags but don't document all parameters. It's fine to omit `<param>` tags entirely if they don't add value.

## Testing guidelines

**For comprehensive testing guidelines, see `ai/TESTING.md`** which covers:
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
