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

### CRITICAL: .resx file workflow
When adding new resource strings to a .resx file, you MUST also add the corresponding public static string properties to the .Designer.cs file. The compiler will fail with CS0117 errors if the Designer.cs file is not updated.

Example workflow:
1. Add `<data name="MyNewString" xml:space="preserve"><value>My New String</value></data>` to .resx
2. Add `public static string MyNewString => ResourceManager.GetString("MyNewString", resourceCulture);` to .Designer.cs
3. Build to verify no CS0117 errors

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

## Asynchronous programming patterns

### CRITICAL: No async/await keywords
- **DO NOT use `async`/`await` keywords** in C# code
- Use `ActionUtil.RunAsync()` for background operations in Skyline code
- Use `CommonActionUtil.RunAsync()` only in Common/CommonUtil libraries (no Skyline dependencies)
- Use `Control.Invoke()` and `Control.BeginInvoke()` to return to UI thread
- See `DocumentationViewer.cs` for example implementation pattern

### Choosing the right RunAsync
**In Skyline code (`pwiz_tools/Skyline/`):**
- **Use `ActionUtil.RunAsync()`** from `pwiz.Skyline.Util.Extensions`
- Provides proper localization/translation support for resource strings
- Required for code that accesses RESX files during background operations
- Add `using pwiz.Skyline.Util.Extensions;`

**In Common/CommonUtil libraries:**
- **Use `CommonActionUtil.RunAsync()`** from `pwiz.Common.SystemUtil`
- Cannot depend on Skyline-specific types
- Add `using pwiz.Common.SystemUtil;`

Note: These two implementations are not yet fully interchangeable. Prefer `ActionUtil` in Skyline code to ensure correct localization behavior during tests.

### Background operation pattern
```csharp
// Background operation with UI callback
ActionUtil.RunAsync(() =>
{
    try
    {
        // Background work here
        var result = DoBackgroundWork();
        
        // Return to UI thread for updates
        RunUI(() => UpdateUI(result));
    }
    catch (Exception ex)
    {
        RunUI(() => CommonAlertDlg.ShowException(this, ex));
    }
});

private void RunUI(Action act)
{
    Invoke(act);
}
```

### WebClient replacement pattern
When replacing WebClient with HttpClient:
- Maintain synchronous interface for existing callers
- Use `RunAsync()` to execute HTTP operations in background
- Use `Control.Invoke()` to return results to UI thread
- Preserve existing error handling patterns

## Testing guidelines

### Test project structure
- **Unit tests**: `Test` and `TestData` projects
  - Derive from `AbstractUnitTest` and `AbstractUnitTestEx`
  - Fast execution, no UI overhead (no SkylineWindow)
  - **`Test`**: General unit tests, may access file system but no UI
  - **`TestData`**: Unit tests that work with actual mass spectrometry data files, must access file system
- **Functional tests**: `TestFunctional`, `TestPerf`, and `TestTutorial` projects
  - Derive from `AbstractFunctionalTest` and `AbstractFunctionalTestEx`
  - Require SkylineWindow and UI interaction
  - Significant overhead due to window creation/destruction and UI driving

### Test types by project
- **`TestFunctional`**: Standard functional tests for UI features
- **`TestPerf`**: Performance tests with large datasets (>100MB)
  - Run less frequently, only on machines with >100GB free disk space
  - Test data stored in Downloads folder
- **`TestTutorial`**: Automated tutorial implementation tests
  - Implement step-by-step tutorial instructions from `Skyline/Documentation/Tutorials`
  - Capture automated screenshots for tutorials (e.g., `s-01.png`, `s-02.png`, `cover.png`)
  - Screenshots stored in language subfolders (`en`, `ja`, `zh-CHS`)
  - Examples: `TestIrtTutorial` for `Skyline/Documentation/Tutorials/iRT`

### Test structure
- Use `AbstractFunctionalTest` for UI tests that require SkylineWindow
- Use `RunFunctionalTest()` in test methods, implement `DoTest()` for test logic
- Use `RunUI()` for UI operations to avoid cross-thread access exceptions
- Use `ShowDialog<T>()` for modal dialogs instead of `RunUI()` + `WaitForOpenForm()`
- Call public methods directly rather than simulating UI clicks
- Use `AssertEx.Contains()` for string assertions
- Use resource strings for test assertions to support localization

### Functional test performance
- **Functional tests have significant overhead** - they must show and destroy a SkylineWindow and often drive the UI
- This makes them much slower than unit tests that run entirely in memory without file system or network access
- **Prefer consolidating multiple validations** into a single functional test class with one `[TestMethod]`
- Use private helper methods within the test class for different validation concerns
- Avoid creating separate test classes for related functional validations that could share the same SkylineWindow instance
