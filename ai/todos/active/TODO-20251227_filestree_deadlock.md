# TODO-20251227_filestree_deadlock.md

## Branch Information
- **Branch**: `Skyline/work/20251227_filestree_deadlock`
- **Created**: 2025-12-27
- **Status**: In Progress
- **GitHub Issue**: https://github.com/ProteoWizard/pwiz/issues/3738
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

## Tasks

- [ ] Reproduce the issue locally (if possible)
- [ ] Review ManagedFileSystemWatcher.DisposeWatcher() implementation
- [ ] Review where BeginInvoke is called from FSW callbacks
- [ ] Implement fix (disable events + IsDisposed check)
- [ ] Test with TestTreeRestoration
- [ ] Run full Files view test suite
- [ ] Create PR

## Key Files

- `pwiz_tools/Skyline/Controls/FilesTree/ManagedFileSystemWatcher.cs`
- `pwiz_tools/Skyline/Controls/FilesTree/LocalFileSystemService.cs`
- `pwiz_tools/Skyline/Controls/FilesTree/FilesTree.cs`
- `pwiz_tools/Skyline/Controls/FilesTree/FilesTreeForm.cs`

## Progress Log

### 2025-12-27 - Session 1
- Created GitHub Issue #3738
- Created branch `Skyline/work/20251227_filestree_deadlock`
- Created this TODO file
- Initial analysis from Nick's email with call stacks
