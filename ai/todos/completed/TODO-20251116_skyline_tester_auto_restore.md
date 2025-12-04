# TODO: SkylineTester Auto-Restore Checked Tests

## Branch Information
- **Branch**: Skyline/work/20251116_skyline_tester_auto_restore
- **Created**: 2025-11-16
- **PR**: #3680

## Objective

Enable SkylineTester to automatically restore checked tests from `SkylineTester test list.txt` on startup, providing workflow persistence across sessions and enabling seamless handoff with LLM-driven test execution.

## Problem Statement

**Current behavior:**
- Developer selects tests in SkylineTester UI (Tests tab)
- Developer closes SkylineTester, makes code changes
- Developer reopens SkylineTester → **all test selections lost**
- Developer must remember and re-check the same tests

**Specific pain points:**
1. **"Check Failed Tests" workflow broken by restart**: Developer clicks "Check Failed Tests" → close → fix code → reopen → selections lost, must re-identify which tests failed
2. **No LLM handoff visibility**: LLM runs tests via Run-Tests.ps1 → developer opens SkylineTester → can't see which tests LLM ran
3. **Sprint test set management**: Developer must manually re-select sprint test set each time SkylineTester opens
4. **Lost context on interruptions**: Phone call, meeting, end of day → test selections lost

## Proposed Solution

**On SkylineTester startup:**
1. Check if `SkylineTester test list.txt` exists
2. If exists, read test names from file (skip comments `#` and blank lines)
3. Automatically check matching tests in Tests tab
4. If file doesn't exist or is empty, start with default state (no tests checked, or previous default)

**Implementation location:** `pwiz_tools/Skyline/SkylineTester/SkylineTesterWindow.cs` (or equivalent form class)

## Key Workflows Enabled

### Workflow 1: "Check Failed Tests" Persistence
```
1. Developer runs tests in SkylineTester, some fail
2. Developer clicks "Check Failed Tests" button
3. SkylineTester checks only failed tests, writes to SkylineTester test list.txt
4. Developer closes SkylineTester
5. Developer makes code fixes
6. Developer reopens SkylineTester → failed tests AUTOMATICALLY re-checked ✨
7. Developer clicks "Run" to verify fixes
```

### Workflow 2: Sprint Test Set Persistence
```
1. Developer selects 10 tests for current sprint in SkylineTester
2. SkylineTester writes selections to SkylineTester test list.txt
3. Developer closes SkylineTester at end of day
4. Next morning, developer opens SkylineTester → 10 tests AUTOMATICALLY checked ✨
5. No need to remember or re-select tests
```

### Workflow 3: LLM Handoff Visibility (Requires Run-Tests.ps1 enhancement in future TODO)
```
1. LLM runs tests via Run-Tests.ps1 with -UpdateTestList
2. LLM writes test names to SkylineTester test list.txt
3. Developer opens SkylineTester → tests AUTOMATICALLY checked ✨
4. Developer can see exactly which tests LLM ran
5. Developer can review results, modify test set, re-run
```

## Implementation Plan

### Phase 1: Core Auto-Restore Functionality ✅ COMPLETED
- [x] Locate SkylineTester main form class (SkylineTesterWindow.cs)
- [x] Add `RestoreCheckedTestsFromFile()` method
- [x] Call method from `BackgroundLoad` after tree is populated
- [x] Implement file parsing logic:
  - Read `SkylineTester test list.txt` line by line
  - Skip blank lines
  - Skip comment lines (start with `#`)
  - Trim whitespace from test names
- [x] Match test names to Tests tab items
- [x] Check matching tests programmatically

### Phase 2: Error Handling ✅ COMPLETED
- [x] Handle case where file doesn't exist (use default behavior)
- [x] Handle case where file is empty (use default behavior)
- [x] Handle case where test name in file doesn't match any test (skip gracefully)
- [x] Handle file I/O errors (catch exceptions, don't crash)
- [x] Ensure robust error handling doesn't crash SkylineTester on startup

### Phase 3: Tri-State Visual Feedback ✅ COMPLETED (Enhanced from original plan)
- [x] Implement tri-state checkbox indicators for parent nodes
- [x] Gray text = partial selection (some but not all children checked)
- [x] Normal text = all checked or all unchecked
- [x] Dynamic updates when user checks/unchecks tests
- [x] Clear visual indicator without expanding tree nodes

### Phase 4: Testing ✅ COMPLETED
- [x] Test with existing valid test list file
- [x] Test with missing file (works normally)
- [x] Test that manual check/uncheck still works after auto-restore
- [x] Test tri-state visual feedback updates dynamically
- [x] Test "Check Failed Tests" → close → reopen workflow

### Phase 5: Documentation
- [x] Add comments in code explaining the auto-restore feature
- [x] Document in commit message for future reference

## Technical Details

### SkylineTester test list.txt Format

**File location:** `pwiz_tools\Skyline\SkylineTester test list.txt`

**Format:**
```
# SkylineTester test list
# One test name per line
# Lines starting with # are comments
TestPanoramaDownloadFile
TestLibraryBuildNotification
CodeInspection
# TestSlowPerformance  # Commented out - too slow
```

**Parsing rules:**
- One test name per line
- Lines starting with `#` are comments (skip)
- Blank lines are ignored
- Whitespace trimmed from test names
- Test names should match `[TestMethod]` names exactly

### Implementation Pseudocode

```csharp
private void RestoreCheckedTestsFromFile()
{
    var testListPath = Path.Combine(
        Path.GetDirectoryName(Application.ExecutablePath),
        "SkylineTester test list.txt");

    if (!File.Exists(testListPath))
        return; // No file, use default behavior

    try
    {
        var testNames = File.ReadAllLines(testListPath)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .Where(line => !line.StartsWith("#"))
            .ToList();

        if (testNames.Count == 0)
            return; // Empty file, use default behavior

        int restoredCount = 0;
        foreach (var testName in testNames)
        {
            // Find matching test in Tests tab (implementation-specific)
            var testItem = FindTestByName(testName);
            if (testItem != null)
            {
                testItem.Checked = true;
                restoredCount++;
            }
            else
            {
                // Log warning: test name in file doesn't match any test
                Log.Warn($"Test '{testName}' from test list not found");
            }
        }

        // Optional: Show status message
        if (restoredCount > 0)
        {
            statusLabel.Text = $"Restored {restoredCount} test(s) from test list";
        }
    }
    catch (Exception ex)
    {
        // Log error but don't crash
        Log.Error($"Error restoring tests from file: {ex.Message}");
    }
}

private void SkylineTesterWindow_Load(object sender, EventArgs e)
{
    // ... existing initialization code ...

    RestoreCheckedTestsFromFile();

    // ... rest of initialization ...
}
```

## Success Criteria

- [x] SkylineTester reads `SkylineTester test list.txt` on startup
- [x] Tests listed in file are automatically checked in Tests tab
- [x] Invalid test names are skipped gracefully (not crashed)
- [x] Missing/empty file doesn't break SkylineTester (uses default behavior)
- [x] Manual check/uncheck still works normally after auto-restore
- [x] "Check Failed Tests" → close → reopen → tests still checked ✅
- [x] Sprint test set persists across SkylineTester sessions ✅
- [x] Tri-state visual feedback shows partial selections without expanding tree ✅

## Non-Goals

- Modifying test list file format (use existing format)
- Adding UI to enable/disable auto-restore (always on, automatic)
- Real-time synchronization with LLM runs (file-based handoff only)
- Showing which tests were auto-restored vs manually checked (not needed)

## Benefits

1. **"Check Failed Tests" workflow now persistent** - survives SkylineTester restarts
2. **Sprint test set management** - select once, persists across sessions
3. **Reduced cognitive load** - don't need to remember which tests were selected
4. **Enables LLM integration** - foundation for future Run-Tests.ps1 bidirectional sync
5. **Zero learning curve** - automatic behavior, no new UI or commands

## Risks & Mitigations

**Risk:** Test list file contains thousands of test names, slows startup
- **Mitigation:** File parsing is fast, checking UI items should be O(N). Monitor startup time.

**Risk:** Test names in file become stale (tests renamed/deleted)
- **Mitigation:** Skip invalid names gracefully, log warnings. Developer can clean up file manually.

**Risk:** Developer confused why tests are auto-checked
- **Mitigation:** Optional status bar message. Behavior is intuitive (matches user's last selection).

**Risk:** File gets corrupted
- **Mitigation:** Robust error handling, catch exceptions, log errors, don't crash.

## Files to Modify

### C# Code
- `pwiz_tools/Skyline/SkylineTester/SkylineTesterWindow.cs` (or equivalent main form)
  - Add `RestoreCheckedTestsFromFile()` method
  - Call from `Form_Load` or initialization event
  - Add error handling and logging

### Testing
- Manual testing with various test list scenarios
- Verify existing SkylineTester functionality unchanged

## Dependencies

**None** - This is a standalone feature that doesn't depend on any other work.

**Future work depends on this:**
- Run-Tests.ps1 `-UpdateTestList` feature (separate TODO)
- Full LLM/SkylineTester bidirectional integration (separate TODO)

## Notes

- This feature provides immediate value even without LLM integration
- Existing SkylineTester behavior (writing to file when tests checked/unchecked) already works - we just add the restore on startup
- File format is already defined and used - no changes needed
- Simple feature, big quality-of-life improvement for developers

## Related Work

- TODO-ai_test_list_integration.md - Future Run-Tests.ps1 enhancements (Phase 2)
- PR #3667 - Build/test automation tooling foundation
- TODO-20251107_httpclient_to_progress.md - Current work with script improvements
