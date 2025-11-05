# CRITICAL RULES

Bare constraints only - no explanations. See ai/MEMORY.md, ai/STYLEGUIDE.md, and ai/TESTING.md for details.

## File Format Requirements
- Line endings: CRLF (`\r\n`) - Windows standard
- Use spaces, not tabs
- Blank lines must be completely empty (no spaces/tabs)
- Prefer ASCII characters over Unicode

## Asynchronous Programming
- **NEVER** use `async`/`await` keywords in C# code
- Use `ActionUtil.RunAsync()` in Skyline code (`pwiz_tools/Skyline/`)
- Use `CommonActionUtil.RunAsync()` in Common libraries (`pwiz_tools/Shared/`)

## Resource Strings (Localization)
- **ALL** user-facing text must be in .resx files
- **NO** string literals in .cs files for UI text
- Add new UI strings to `pwiz_tools/Skyline/Menus/MenusResources.resx`
- .resx changes require corresponding .Designer.cs updates
- Resource properties in .Designer.cs must be in alphabetical order

## Naming Conventions
- Private fields: `_camelCase`
- Constants: `ALL_CAPS_WITH_UNDERSCORES`
- Types/namespaces: `PascalCase`
- Interfaces: `IPascalCase`
- Enum members: `snake_case`
- Locals/parameters: `camelCase`

## Testing - Translation-Proof
- **NEVER** use English text literals in test assertions
- **ALWAYS** use resource strings for expected text
- **ALWAYS** use `AssertEx.Contains()` not `Assert.IsTrue(string.Contains())`
- **ALWAYS** use `HttpClientTestHelper.GetExpectedMessage()` for network errors

## Testing - Structure
- **NEVER** create multiple `[TestMethod]` for related validations
- **ALWAYS** consolidate validations into single test with private helpers
- Test.csproj: Fast unit tests, no UI, no data
- TestFunctional.csproj: UI tests (most common)
- See ai/TESTING.md for comprehensive guidelines

## DRY Principle
- Extract helpers when duplication exceeds 3 lines
- 17-year-old project - duplication is maintenance burden
- Place helpers after public methods that use them

## Control Flow
- If statements must not be single-line
- Keep condition and body on separate lines if braces omitted

## Build System
- Use `quickbuild.bat` on Windows
- **DO NOT** introduce new build systems
- **DO NOT** reformat unrelated code
- Update Jamfile or Visual Studio project when adding sources

## File and Member Ordering
1. static variables/fields
2. static public methods
3. private instance fields
4. constructor(s)
5. public methods/properties
6. private helper methods (after methods that use them)

## Tools and Quality
- Aim for zero warnings in Visual Studio 2022 + ReSharper
- Solution must build with zero warnings
- All tests must pass before commit
- ReSharper must show green (no inspections)

## NEVER
- Use `async`/`await` keywords
- Use English text literals in test assertions
- Parse exception messages for status codes
- Create multiple `[TestMethod]` for related validations
- Use string literals for user-facing text
- Reformat unrelated code
- Mix tabs and spaces
- Use Unicode when ASCII alternative exists
