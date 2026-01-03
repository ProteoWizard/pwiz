# Fix TestImportHundredsOfReplicates performance regression (+2524% slowdown)

## Branch Information
- **Branch**: `Skyline/work/20260102_FilesViewPerfRegression`
- **Base**: `master`
- **Created**: 2026-01-02
- **GitHub Issue**: https://github.com/ProteoWizard/pwiz/issues/3745

## Objective

Fix the performance regression in `TestImportHundredsOfReplicates` which regressed from 136s to 3568s (59 minutes) after the Files view feature merge on 2024-12-14.

## Tasks

- [x] Profile TestImportHundredsOfReplicates to identify bottleneck
- [x] Determine if Files view initialization is O(n²) with replicate count
- [x] Fix or optimize the slow path
- [x] Verify test returns to ~136s baseline

## Resolution

Two fixes combined to eliminate the regression:

1. **Check-before-set in OnModelChanged** (FilesTreeNode.cs)
   - TreeNode property setters (Name, Text, ImageIndex) trigger UI events even when value unchanged
   - Added equality checks before setting properties
   - Impact: 3568s → 239s (15x faster)

2. **Timer debouncing for document changes** (FilesTree.cs)
   - Added 100ms debounce timer following the UpdateGraphPanes pattern from SkylineGraphs.cs
   - Rapid document changes during import now coalesce into single tree refresh
   - Impact: 239s → 213s (now at baseline)

**Final result**: Files view overhead reduced from +3353s (+1556%) to essentially zero.

## Progress Log

### 2026-01-02 - Session Start

Starting work on this issue. Will begin by profiling the test to identify the bottleneck.

### 2026-01-02 - Root Cause Analysis

Identified two O(n²) issues:
- `OnModelChanged` setting TreeNode properties on every call triggered UI events
- `HandleDocumentEvent` called for every document change during rapid import

Profiling showed:
- `FilesTree.MergeNodes`: 163,903 ms (4.9%)
- `FileSystemUtil.IsFileInDirectory`: 109,013 ms (3.3%)
- `BackgroundActionService` for FilesTree: ~385,000 ms (27% of Consume)

### 2026-01-02 - Fix Implemented and Verified

After both fixes, profiling shows:
- `MergeNodes`: 16,628 ms (0.8%) - 90% reduction
- `IsFileInDirectory`: No longer in top list
- `BackgroundActionService`: ~205,000 ms (24%) - 47% reduction

Test time: 213s (baseline without Files view: 215s)
