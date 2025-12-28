# Skyline Project Memory & Context

Essential context for AI tools. See [ai/docs/project-context.md](docs/project-context.md) for comprehensive details.

## Project Scale

- **900,000 lines of code** - C#, C++, JavaScript
- **17+ years of evolution** - Long-term maintenance critical
- **8 active developers** - Concurrent work
- **100+ hours daily** automated testing across platforms

## Development Environment

- **Platform**: Windows with Visual Studio 2022
- **Build**: Boost.Build with MSVC/GCC
- **Version Control**: Git with GitHub
- **Testing**: Comprehensive automated test suite (unit, functional, performance)
- **Localization**: English, Chinese (Simplified), Japanese, Turkish, French

## Critical Gotchas

See [ai/CRITICAL-RULES.md](CRITICAL-RULES.md) for full list.

### NO async/await Keywords
```csharp
// ❌ NEVER
public async Task DoWorkAsync() { await Task.Run(...); }

// ✅ ALWAYS
ActionUtil.RunAsync(() => { /* background work */ });  // Skyline code
CommonActionUtil.RunAsync(() => { /* work */ });       // Common libraries
```

### Resource String Workflow
When adding .resx strings:
1. Add `<data>` to .resx file
2. Add corresponding property to .Designer.cs
3. Keep properties in alphabetical order
4. Build to verify no CS0117 errors

### Translation-Proof Testing
```csharp
// ❌ NEVER - Breaks in Chinese/Japanese
Assert.IsTrue(message.Contains("File not found"));

// ✅ ALWAYS - Resource strings
AssertEx.Contains(message, Resources.ErrorMessage_FileNotFound);
```

### DRY Principle - Critical for 17-Year Project
- Extract helpers when duplication exceeds 3 lines
- Place helpers after public methods that use them
- Duplication is maintenance burden in long-lived codebases
- See [ai/docs/project-context.md](docs/project-context.md) for detailed examples

## Threading Guidelines

- **UI thread only** for WinForms operations
- Use `Control.Invoke()` to marshal to UI thread
- Background operations use `ActionUtil.RunAsync()` (NOT async/await)
- Never access UI controls from background threads

## Exception Handling Patterns

- Use centralized mapping (`MapHttpException`, `IsProgrammingDefect`)
- Avoid duplicating exception classification logic
- Use `UserMessageException` base class for user-facing exceptions
- Distinguish programming errors (re-thrown) vs user-actionable errors (displayed)

## File and Member Ordering

1. static variables/fields
2. static public methods
3. private instance fields
4. constructor(s)
5. public methods/properties
6. private helper methods (after methods that use them)

Place helpers close to their primary call sites. Avoid "old C" style with helpers at top.

## Build System

- **Use `quickbuild.bat` on Windows** - do not introduce new build systems
- **Respect existing indentation** - avoid unrelated reformatting
- **Update Jamfile or VS project** when adding sources
- **Use existing vendor libraries** from `libraries/` - do not fetch replacements

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
- `doc/index.html` - ProteoWizard documentation

### Testing Projects
- **Test.csproj**: Fast unit tests, no UI, no data
- **TestData.csproj**: Unit tests with mass spec data
- **TestFunctional.csproj**: UI functional tests (most common)
- **TestConnected.csproj**: Network-requiring tests
- **TestTutorial.csproj**: Automated tutorial validation
- **TestPerf.csproj**: Large dataset performance tests

See [ai/TESTING.md](TESTING.md) for testing guidelines.

## LLM-Specific Guidelines

### Context Management
- Always read `ai/todos/active/TODO-YYYYMMDD_*.md` for current branch context
- Update TODO file with every commit
- Follow branch naming: `Skyline/work/YYYYMMDD_description`

### Code Quality
- Match surrounding file style exactly
- Prefer focused edits over broad refactoring
- Keep methods small and cohesive
- Extract helpers when duplication exceeds 3 lines
- Use descriptive, intention-revealing names

### Testing Requirements
- All new code must have appropriate tests
- Use translation-proof assertions (resource strings, not English literals)
- Follow DRY principles in test code
- Prefer functional tests for UI features, unit tests for pure logic

### Documentation
- Update relevant documentation files
- Use XML documentation for public APIs
- Keep comments focused and meaningful

## See Also

- [ai/docs/project-context.md](docs/project-context.md) - Comprehensive project context with detailed examples
- [ai/CRITICAL-RULES.md](CRITICAL-RULES.md) - All critical constraints
- [ai/WORKFLOW.md](WORKFLOW.md) - Git workflows and TODO system
- [ai/STYLEGUIDE.md](STYLEGUIDE.md) - Coding conventions
- [ai/TESTING.md](TESTING.md) - Testing patterns
