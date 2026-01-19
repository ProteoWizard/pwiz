# TODO-20260102_safebeginvoke_toctou.md

## Branch Information
- **Branch**: `Skyline/work/20260102_safebeginvoke_toctou`
- **Created**: 2026-01-02
- **Status**: Complete - PR Created
- **GitHub Issue**: [#3738](https://github.com/ProteoWizard/pwiz/issues/3738)
- **PR**: [#3743](https://github.com/ProteoWizard/pwiz/pull/3743)
- **Related**: PR #3739 (Sprint 1 fix), TODO-20251227_filestree_deadlock.md

## Objective

Fix TOCTOU (time-of-check-to-time-of-use) race condition in `SafeBeginInvoke` that can still cause deadlocks even after the Sprint 1 fix.

## Problem Analysis

### The Race Condition

From PR #3739 discussion (Nick Shulman & Brendan MacLean):

```csharp
public static bool SafeBeginInvoke(Control control, Action action)
{
    if (control == null || !control.IsHandleCreated)  // ← Check
        return false;
    try
    {
        control.BeginInvoke(action);  // ← Use: handle can be destroyed between check and here
        return true;
    }
    catch (Exception)
        return false;
}
```

**Critical insight from Brendan**: The try/catch does NOT protect against deadlock. When `BeginInvoke` is called after handle destruction begins, .NET attempts to **recreate the handle**, and that recreation can deadlock the UI thread. There's no exception to catch - just a hang.

### Evidence

Nick found another hang in run 79688 where `BackgroundActionService.RunUI` (which uses `SafeBeginInvoke`) was on the callstack:
https://skyline.ms/home/development/Nightly%20x64/testresults-showRun.view?runId=79688

## Proposed Solution

Add infrastructure to `CommonFormEx` that signals early shutdown, then check it in `SafeBeginInvoke`.

### Step 1: Add IsClosingOrDisposing to CommonFormEx

```csharp
public class CommonFormEx : Form, IFormView
{
    private volatile bool _isClosingOrDisposing;

    public bool IsClosingOrDisposing => _isClosingOrDisposing;

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!e.Cancel)
            _isClosingOrDisposing = true;  // Earliest point we know window is going away
        base.OnFormClosing(e);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _isClosingOrDisposing = true;  // Belt-and-suspenders
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        _isClosingOrDisposing = true;
        // ... existing code
    }
}
```

### Step 2: Update SafeBeginInvoke

```csharp
public static bool SafeBeginInvoke(Control control, Action action)
{
    if (control == null || !control.IsHandleCreated)
        return false;

    // Check for CommonFormEx early shutdown signal
    var parentForm = control.FindForm();
    if (parentForm is CommonFormEx formEx && formEx.IsClosingOrDisposing)
        return false;

    try
    {
        control.BeginInvoke(action);
        return true;
    }
    catch (Exception)
        return false;
}
```

### Why This Helps

1. **Setting the flag in OnFormClosing gives real margin** - the flag is set well before the handle is actually destroyed
2. **The volatile keyword ensures visibility** across threads without locks
3. **Shrinks the race window dramatically** - from "anytime after IsHandleCreated check" to "microseconds between flag check and BeginInvoke while UI thread is simultaneously in the flag-setting code"

### Limitations

This isn't mathematically airtight - there's still a tiny race window. But hitting it would require extremely precise timing that's essentially impossible in normal operation.

## Tasks

- [x] Create branch `Skyline/work/20260102_safebeginvoke_toctou`
- [x] Create this TODO file
- [x] Review CommonFormEx current implementation
- [x] Review SafeBeginInvoke current implementation
- [x] Add `_isClosingOrDisposing` field and property to CommonFormEx
- [x] Override OnFormClosing in CommonFormEx
- [x] Override OnHandleDestroyed in CommonFormEx
- [x] Update Dispose in CommonFormEx
- [x] Update SafeBeginInvoke to check IsClosingOrDisposing
- [x] Run FilesTree tests
- [x] Run broader test suite to verify no regressions (1000 tests passed)
- [x] Create PR

## Key Files

- `pwiz_tools/Shared/CommonUtil/SystemUtil/CommonFormEx.cs` - Add IsClosingOrDisposing flag
- `pwiz_tools/Shared/CommonUtil/SystemUtil/CommonActionUtil.cs` - Update SafeBeginInvoke

## References

- PR #3739 discussion: https://github.com/ProteoWizard/pwiz/pull/3739
- Nick's safe pattern example: `pwiz.Common.SystemUtil.Caching.Receiver` class
- Nick's hang evidence: https://skyline.ms/home/development/Nightly%20x64/testresults-showRun.view?runId=79688

## Progress Log

### 2026-01-02 - Session 1
- Created branch from master
- Created this TODO file
- Reviewed CommonFormEx.cs and CommonActionUtil.cs current implementations
- Implemented the fix:
  - Added `_isClosingOrDisposing` volatile field and `IsClosingOrDisposing` property to CommonFormEx
  - Added `OnFormClosing` override (sets flag after base call if not cancelled)
  - Added `OnHandleDestroyed` override (belt-and-suspenders)
  - Updated `Dispose` to set flag before base call
  - Updated `SafeBeginInvoke` to check parent form's `IsClosingOrDisposing` flag
- Full test suite passed (1000 tests, English)
- Created PR
