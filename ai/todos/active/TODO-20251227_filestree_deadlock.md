# TODO-20251227_filestree_deadlock.md

## Branch Information
- **Branch**: `Skyline/work/20251227_filestree_deadlock`
- **Created**: 2025-12-27
- **Status**: Complete - PR Created
- **GitHub Issue**: [#3738](https://github.com/ProteoWizard/pwiz/issues/3738)
- **PR**: [#3739](https://github.com/ProteoWizard/pwiz/pull/3739)
- **Related**: PR #3867 (Files view feature), TODO-20251126_files_view.md

## Objective

Fix deadlock in FilesTree disposal that causes TestTreeRestoration to hang on nightly tests.

## Problem Analysis

### The Deadlock (from Nick Shulman's analysis)

**Thread 1 (UI Thread)** - Disposing FilesTree:
```
DestroyFilesTreeForm → FilesTree.Dispose → LocalFileSystemService.StopWatching
→ ManagedFileSystemWatcher.DisposeWatcher → FileSystemWatcher.Dispose
→ Monitor.Enter (BLOCKED)
```

**Thread 2 (Background Thread)** - FileSystemWatcher callback:
```
FileSystemWatcher.CompletionStatusChanged → Control.BeginInvoke
→ Control.get_Handle → Control.CreateHandle
→ WaitMessage (BLOCKED - waiting for UI thread)
```

### Root Cause

Classic deadlock pattern:
1. UI thread is disposing `FileSystemWatcher` (holds internal lock, waiting for callbacks to complete)
2. FSW background thread receives a file system event and tries to `BeginInvoke` to the FilesTree control
3. `BeginInvoke` needs to get/create the control's window handle, which requires the UI thread
4. UI thread is blocked waiting for FSW disposal → Deadlock

### Test Failure Details

- **Test**: TestTreeRestoration
- **Machine**: EKONEIL01
- **Run ID**: 79671
- **Date**: 2025-12-26

## Solution Approach

### Option 1: Disable FSW events before disposal
Set `FileSystemWatcher.EnableRaisingEvents = false` before calling `Dispose()`. This should prevent new callbacks from being raised during disposal.

### Option 2: Use IsDisposed check in callback
Before calling `BeginInvoke`, check if the control is disposed or disposing. This prevents the callback from trying to marshal to a dying control.

### Option 3: Async disposal pattern
Dispose FSW on a background thread to avoid blocking the UI thread. However, this may introduce other race conditions.

### Recommended: Combination of Options 1 + 2
1. Set `EnableRaisingEvents = false` before disposal
2. Add `IsDisposed`/`IsDisposing` check in the FSW callback before `BeginInvoke`

## Actual Solution (Implemented)

### Root Cause Discovery

The actual root cause was **line 58 in ManagedFileSystemWatcher.cs**:
```csharp
FileSystemWatcher.SynchronizingObject = SynchronizingObject;
```

Setting `SynchronizingObject` on a `FileSystemWatcher` causes .NET to **automatically** call `BeginInvoke` on that control to marshal events to the UI thread. This happens inside FSW's internal implementation, not in our code.

During disposal:
1. UI thread calls `FileSystemWatcher.Dispose()` which waits for internal callbacks to complete
2. FSW's internal callback tries to `BeginInvoke` to the SynchronizingObject (FilesTree control)
3. BeginInvoke needs the UI thread to get/create the control handle
4. UI thread is blocked waiting for FSW.Dispose() → **Deadlock**

### The Fix

**Remove the SynchronizingObject entirely.** This was unnecessary because the FilesTree code already handles thread marshaling safely:

1. FSW event handlers in `LocalFileSystemService` use `BackgroundActionService.AddTask()` to queue work
2. `BackgroundActionService.RunUI()` uses `CommonActionUtil.SafeBeginInvoke()` which:
   - Checks `control.IsHandleCreated` before calling BeginInvoke
   - Catches exceptions if the control is disposed

### Files Changed

**ManagedFileSystemWatcher.cs**:
- Removed `SynchronizingObject` property and constructor parameter
- Removed `FileSystemWatcher.SynchronizingObject = SynchronizingObject;` line
- Removed `using System.Windows.Forms;` (no longer needed)
- Added comment explaining why SynchronizingObject must NOT be set

**FileSystemService.cs** (LocalFileSystemService.WatchDirectory):
- Updated constructor call: `new ManagedFileSystemWatcher(directoryPath)` (removed second parameter)

## Tasks

- [x] Reproduce the issue locally (if possible)
- [x] Review ManagedFileSystemWatcher.DisposeWatcher() implementation
- [x] Review where BeginInvoke is called from FSW callbacks
- [x] Implement fix (remove SynchronizingObject - see Solution below)
- [x] Test with Files view test suite (4 tests passing)
- [ ] Create PR

## Key Files

- `pwiz_tools/Skyline/Controls/FilesTree/ManagedFileSystemWatcher.cs` ← **Modified**
- `pwiz_tools/Skyline/Controls/FilesTree/FileSystemService.cs` ← **Modified**
- `pwiz_tools/Skyline/Controls/FilesTree/BackgroundActionService.cs` (has SafeBeginInvoke pattern)
- `pwiz_tools/Shared/CommonUtil/SystemUtil/CommonActionUtil.cs` (SafeBeginInvoke implementation)

## Progress Log

### 2025-12-27 - Session 1
- Created GitHub Issue #3738
- Created branch `Skyline/work/20251227_filestree_deadlock`
- Created this TODO file
- Initial analysis from Nick's email with call stacks

### 2025-12-27 - Session 2
- Discovered actual root cause: `FileSystemWatcher.SynchronizingObject` property
- Found that setting SynchronizingObject causes FSW to internally use BeginInvoke for event marshaling
- Identified that FilesTree already has safe marshaling via `BackgroundActionService.RunUI()` → `CommonActionUtil.SafeBeginInvoke()`
- Implemented fix: removed SynchronizingObject from ManagedFileSystemWatcher
- All 4 Files view tests passing:
  - TestFilesModel (16s)
  - TestFilesTreeFileSystem (4s)
  - TestFilesTreeForm (40s)
  - TestSkylineWindowEvents (0s)
- Ready for PR creation
