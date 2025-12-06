# TODO-standardize_thread_use.md

## Branch Information (Future)
- **Branch**: Not yet created - will be `Skyline/work/YYYYMMDD_standardize_thread_use`
- **Objective**: Standardize thread creation patterns throughout Skyline codebase to use `ActionUtil.RunAsync()` instead of direct `new Thread()` calls

## Background

### The Problem with Direct Thread Creation
Skyline has established patterns for thread creation that provide:
1. **Proper exception handling**: Exceptions are reported via `Program.ReportException`
2. **Localization initialization**: `LocalizationHelper.InitThread` is called automatically
3. **Consistent error reporting**: All background thread exceptions are captured and logged
4. **Maintainability**: Single point of control for thread creation patterns

### Current State
A code inspection was added to `CodeInspectionTest.cs` to detect direct `new Thread()` usage, but it was commented out pending review because:
- There are currently **21 instances** of `new Thread()` in the codebase
- Some are legitimate (e.g., `ActionUtil.cs`, `CommonActionUtil.cs`, `BackgroundEventThreads.cs`)
- Others may need to be converted to use `ActionUtil.RunAsync()` or `CommonActionUtil.RunAsync()`
- We need to establish proper thresholds and exemption patterns before enabling the inspection

### The Skyline Standard
Skyline uses **explicit threading patterns**:
- `ActionUtil.RunAsync()` for Skyline code (provides exception handling and localization)
- `CommonActionUtil.RunAsync()` for shared libraries
- Direct `new Thread()` only in infrastructure code (ActionUtil itself, CommonActionUtil, BackgroundEventThreads)
- Exemption comment: `// Purposely using new Thread() here` for legitimate cases

## Code Inspection Details

The following inspection code was added to `pwiz_tools/Skyline/Test/CodeInspectionTest.cs` (currently commented out):

```csharp
// Looking for bare use of "new Thread()" which should use ActionUtil.RunAsync() instead
// ActionUtil.RunAsync() provides proper exception handling and localization initialization
const string newThreadExemptionComment = @"// Purposely using new Thread() here";
AddTextInspection(@"*.cs", // Examine files with this mask
    Inspection.Forbidden, // This is a test for things that should NOT be in such files
    Level.Error, // Any failure is treated as an error, and overall test fails
    NonSkylineDirectories().Append(@"ActionUtil.cs").Append(@"CommonActionUtil.cs").Append(@"BackgroundEventThreads.cs").ToArray(), // Exclude ActionUtil itself and other infrastructure
    string.Empty, // No file content required for inspection
    @"new Thread\(", // Forbidden pattern - match "new Thread("
    true, // Pattern is a regular expression
    @"use ActionUtil.RunAsync() instead - this ensures proper exception handling (exceptions are reported via Program.ReportException) and localization initialization (LocalizationHelper.InitThread). If this really is a legitimate use (e.g., in ActionUtil itself) add this comment to the offending line: '" + newThreadExemptionComment + @"'", // Explanation for prohibition, appears in report
    newThreadExemptionComment, // Exemption comment to look for
    21); // Tolerate 21 existing incidents (legitimate uses in infrastructure, tests, and other components)
```

**Current status**: Commented out with TODO reference to this backlog item.

## Prerequisites
- Read `ai/MEMORY.md` async/threading patterns section thoroughly
- Understand `ActionUtil.RunAsync()` vs `CommonActionUtil.RunAsync()`
- Review existing `new Thread()` usage patterns
- Familiarity with test code that may legitimately need direct thread creation

## Task Checklist

### Phase 1: Discovery & Analysis
- [ ] Find all `new Thread()` instances in Skyline code
  ```powershell
  git grep -n "new Thread(" -- "*.cs" ":!Executables" ":!libraries"
  ```
- [ ] Categorize each instance:
  - [ ] Legitimate infrastructure (ActionUtil, CommonActionUtil, BackgroundEventThreads)
  - [ ] Test code that may legitimately need direct thread control
  - [ ] Production code that should use `ActionUtil.RunAsync()`
  - [ ] Shared library code that should use `CommonActionUtil.RunAsync()`
- [ ] Document exemption patterns for legitimate uses
- [ ] Establish proper threshold count for code inspection

### Phase 2: Code Inspection Setup
- [ ] Review and refine the inspection code in `CodeInspectionTest.cs`
- [ ] Determine proper exclusion list (infrastructure files)
- [ ] Set appropriate tolerance threshold based on legitimate uses
- [ ] Add exemption comment pattern documentation
- [ ] Enable the code inspection test

### Phase 3: Conversion (Optional - Future Work)
- [ ] Convert production code instances to use `ActionUtil.RunAsync()`
- [ ] Convert shared library instances to use `CommonActionUtil.RunAsync()`
- [ ] Add exemption comments to legitimate test/infrastructure uses
- [ ] Verify all conversions maintain existing behavior
- [ ] Run full test suite to ensure no regressions

### Phase 4: Related Work
- [ ] Consider adding code inspection for `async`/`await` usage (see `ai/todos/backlog/TODO-remove_async_and_await.md`)
- [ ] Establish accepted threshold for async/await usage
- [ ] Coordinate both inspections to ensure consistent threading patterns

## Files to Review

### Infrastructure (Expected to use `new Thread()`)
- `pwiz_tools/Skyline/Util/Extensions/ActionUtil.cs`
- `pwiz_tools/Shared/CommonUtil/SystemUtil/CommonActionUtil.cs`
- `pwiz_tools/Skyline/Util/BackgroundEventThreads.cs`

### Production Code (Should use `ActionUtil.RunAsync()`)
- `pwiz_tools/Skyline/Skyline.cs`
- `pwiz_tools/Skyline/Util/UtilIO.cs`
- `pwiz_tools/Skyline/Util/UtilUI.cs`
- `pwiz_tools/Skyline/Controls/CustomTip.cs`
- `pwiz_tools/Skyline/Model/BackgroundLoader.cs`
- `pwiz_tools/Skyline/Model/Results/MsDataFileScanHelper.cs`
- `pwiz_tools/Skyline/ToolsUI/ToolService.cs`
- `pwiz_tools/Skyline/CommandLineUI.cs`
- `pwiz_tools/Skyline/SkylineTool/RemoteService.cs`

### Test Code (May legitimately need direct thread control)
- `pwiz_tools/Skyline/TestUtil/TestFunctional.cs`
- `pwiz_tools/Skyline/TestFunctional/FormUtilTest.cs`
- `pwiz_tools/Skyline/TestFunctional/ZedGraphClipboardTest.cs`
- `pwiz_tools/Skyline/TestTutorial/AuditLogTutorialTest.cs`
- `pwiz_tools/Skyline/TestUtil/ScreenshotManager.cs`
- `pwiz_tools/Skyline/TestUtil/ScreenshotPreviewForm.cs`

### Shared Library Code (Should use `CommonActionUtil.RunAsync()`)
- `pwiz_tools/Shared/CommonUtil/SystemUtil/ProcessStreamReader.cs`
- `pwiz_tools/Shared/CommonUtil/SystemUtil/ProducerConsumerWorker.cs`
- `pwiz_tools/Skyline/Executables/MultiLoad/QueueWorker.cs`
- `pwiz_tools/Skyline/Executables/ImportPerf/QueueWorker.cs`

## Related Documentation
- `ai/MEMORY.md` - Threading patterns and async/await policy
- `ai/todos/backlog/TODO-remove_async_and_await.md` - Related work on async/await removal
- `pwiz_tools/Skyline/Test/CodeInspectionTest.cs` - Code inspection implementation

## Notes
- The code inspection was added during work on `FileSystemHealthMonitor.cs` thread management improvements
- The inspection found 21 instances of `new Thread()` usage
- Some instances may be legitimate (tests, infrastructure), others should be converted
- This TODO tracks the work needed to properly enable and maintain the inspection

