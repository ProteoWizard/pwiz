# TODO-remove_async_and_await.md

## Branch Information (Future)
- **Branch**: Not yet created - will be `Skyline/work/YYYYMMDD_remove_async_and_await`
- **Objective**: Remove all `async` and `await` keywords from Skyline codebase

## Background

### The Problem with async/await
Skyline has a **strict policy against `async` and `await` keywords** (documented in MEMORY.md). The reasons are well-established:

1. **Viral spread**: Once introduced, `async` spreads throughout the codebase like a virus
2. **Maintenance nightmare**: Difficult to reason about execution flow
3. **Testing challenges**: Impossible to test with confidence that thread handles aren't leaking
4. **Large codebase complexity**: In 900K+ LOC with 17+ years of evolution, async/await becomes unmaintainable

### How They Crept In
Some `async`/`await` usage crept into the codebase during a recent project. The developer was likely using an LLM without clear instructions on our async usage patterns. **LLMs default to async/await** because it's the "modern" .NET pattern, but senior engineers on the team universally agree it's inappropriate for Skyline's context.

### The Skyline Alternative
Skyline uses **explicit threading patterns** instead:
- `ActionUtil.RunAsync()` for background operations (Skyline code)
- `CommonActionUtil.RunAsync()` for shared libraries
- `Control.Invoke()` and `Control.BeginInvoke()` to marshal to UI thread
- Explicit thread management with clear ownership

This approach provides:
- ✅ Clear execution flow
- ✅ Testable thread behavior
- ✅ Predictable resource management
- ✅ No hidden async state machines

## Prerequisites
- Read MEMORY.md async patterns section thoroughly
- Understand `ActionUtil.RunAsync()` vs `CommonActionUtil.RunAsync()`
- Familiarity with `Control.Invoke()` patterns
- Understanding of UI thread marshaling requirements

## Task Checklist

### Phase 1: Discovery & Analysis
- [ ] Find all `async` method declarations in Skyline code
  ```powershell
  git grep -n "async Task" -- "*.cs" ":!Executables" ":!libraries"
  git grep -n "async void" -- "*.cs" ":!Executables" ":!libraries"
  ```
- [ ] Find all `await` keyword usage
  ```powershell
  git grep -n "\bawait\b" -- "*.cs" ":!Executables" ":!libraries"
  ```
- [ ] Document each occurrence with:
  - File and line number
  - Purpose of the async operation
  - Callers of the async method
  - Impact scope (how far does it spread?)
- [ ] Create prioritized list (most isolated to most viral)

### Phase 2: Refactoring Strategy
For each async method, determine the appropriate pattern:

**Pattern A: Background work with UI callback**
```csharp
// BEFORE (async/await)
private async Task DoWorkAsync()
{
    var result = await SomeOperationAsync();
    UpdateUI(result);
}

// AFTER (ActionUtil.RunAsync)
private void DoWork()
{
    ActionUtil.RunAsync(() =>
    {
        var result = SomeOperation();
        RunUI(() => UpdateUI(result));
    });
}
```

**Pattern B: Sequential background operations**
```csharp
// BEFORE (async/await)
private async Task ProcessAsync()
{
    var data = await DownloadAsync();
    var processed = await ProcessAsync(data);
    return processed;
}

// AFTER (explicit sequencing)
private void Process(Action<ProcessedData> callback)
{
    ActionUtil.RunAsync(() =>
    {
        var data = Download();
        var processed = ProcessData(data);
        RunUI(() => callback(processed));
    });
}
```

**Pattern C: HttpClient operations**
```csharp
// BEFORE (async/await with HttpClient)
private async Task<string> DownloadAsync(string url)
{
    using var client = new HttpClient();
    return await client.GetStringAsync(url);
}

// AFTER (HttpClientWithProgress)
private string Download(string url, IProgressMonitor progressMonitor)
{
    using var client = new HttpClientWithProgress(progressMonitor);
    return client.DownloadString(new Uri(url));
}
```

### Phase 3: Refactoring Execution
- [ ] Start with most isolated async methods (fewest callers)
- [ ] Refactor one method at a time
- [ ] Update all callers to use new non-async signature
- [ ] Run tests after each refactoring
- [ ] Commit frequently with clear messages
- [ ] Work toward root async methods (where virus started)

### Phase 4: Code Inspection Test
- [ ] Add prohibition to `CodeInspectionTest` for async/await usage
- [ ] Verify test passes (no async/await in Skyline code)

**Code to add to CodeInspectionTest.cs**:
```csharp
[TestMethod]
public void TestNoAsyncAwait()
{
    // async/await keywords are prohibited in Skyline codebase
    // Use ActionUtil.RunAsync() instead
    // See MEMORY.md for detailed async patterns and rationale
    
    AssertEx.NoOccurencesInSources(
        "async Task",
        SkylineDirectory,
        new[] { "*.cs" },
        new[] { 
            "Executables",        // Tools may have different requirements
            "libraries",          // Third-party code
            "TestUtil"            // May need async for test infrastructure
        });
    
    AssertEx.NoOccurencesInSources(
        "async void",
        SkylineDirectory,
        new[] { "*.cs" },
        new[] { 
            "Executables",
            "libraries",
            "TestUtil"
        });
    
    // Note: "await" keyword is harder to detect reliably in grep
    // Manual review recommended during code reviews
}
```

### Phase 5: Documentation & Prevention
- [ ] Update MEMORY.md if any new patterns emerged
- [ ] Add comment to .cursorrules emphasizing no async/await
- [ ] Consider adding to code review checklist
- [ ] Remove TODO file before merging to master

## Discovery Commands

### Find async methods
```powershell
# All async Task methods
git grep -n "async Task" -- "*.cs" ":!Executables" ":!libraries"

# All async void methods (especially problematic)
git grep -n "async void" -- "*.cs" ":!Executables" ":!libraries"

# All await usages
git grep -n "\bawait\b" -- "*.cs" ":!Executables" ":!libraries"
```

### Count occurrences
```powershell
git grep -c "async Task" -- "*.cs" ":!Executables" ":!libraries" | grep -v ":0$"
```

## Common Pitfalls

### Don't Just Remove Keywords
❌ **Bad**: Simply removing `async` and `await` without restructuring  
✅ **Good**: Refactor to use explicit threading patterns

### Don't Block on Async Operations
❌ **Bad**: `asyncMethod().Wait()` or `.Result` (deadlock risk)  
✅ **Good**: Refactor to callback pattern or synchronous operation

### Don't Mix Patterns
❌ **Bad**: Using both `async/await` and `ActionUtil.RunAsync()` in same method  
✅ **Good**: Choose one pattern and stick with it

### Test Thoroughly
- ❗ Threading changes are subtle and can cause race conditions
- ❗ UI marshaling errors may only appear under specific conditions
- ❗ Test cancellation scenarios carefully

## Risks & Considerations

### Threading Complexity
- Refactoring async operations requires careful thread management
- Easy to introduce deadlocks or race conditions
- UI thread marshaling must be correct

### Large Impact
- async/await is viral - removing it may touch many files
- Callers must be updated when signatures change
- May require significant test updates

### Behavior Changes
- Timing of operations may change
- Error handling patterns may need adjustment
- Cancellation mechanisms may differ

## Success Criteria
- Zero `async` or `await` keywords in core Skyline code (Skyline.exe, TestRunner.exe)
- All functionality works correctly with explicit threading
- No deadlocks, race conditions, or UI marshaling errors
- Tests pass reliably
- Code inspection test passes
- MEMORY.md patterns are followed consistently

## Out of Scope
- Executables may keep async/await if needed (separate build processes)
- Third-party libraries (libraries/ directory)
- Adding new functionality (pure removal/refactoring only)

## References
- MEMORY.md - Async patterns section (authoritative source)
- `ActionUtil.RunAsync()` - Skyline pattern for background work
- `CommonActionUtil.RunAsync()` - Shared library pattern
- `DocumentationViewer.cs` - Example of correct async pattern usage

## Handoff Prompt for Branch Creation

```
I want to start work on removing async/await keywords from TODO-remove_async_and_await.md.

This is critical technical debt cleanup. async/await is prohibited in Skyline (see MEMORY.md) but some usage crept in.

Please:
1. Create branch: Skyline/work/YYYYMMDD_remove_async_and_await (use today's date)
2. Rename TODO file to include the date
3. Update the TODO file header with actual branch information
4. Begin Phase 1: Discovery & Analysis - run the grep commands to find all occurrences

Key context: Don't just remove keywords - refactor to ActionUtil.RunAsync() patterns. Test thoroughly for threading issues.

The TODO file contains full context and refactoring patterns. Let's start by discovering all async/await usage.
```
