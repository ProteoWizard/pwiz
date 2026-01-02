# Fix TestImportHundredsOfReplicates performance regression (+2524% slowdown)

## Branch Information
- **Branch**: `Skyline/work/20260102_FilesViewPerfRegression`
- **Base**: `master`
- **Created**: 2026-01-02
- **GitHub Issue**: https://github.com/ProteoWizard/pwiz/issues/3745

## Objective

Fix the performance regression in `TestImportHundredsOfReplicates` which regressed from 136s to 3568s (59 minutes) after the Files view feature merge on 2024-12-14.

## Tasks

- [ ] Profile TestImportHundredsOfReplicates to identify bottleneck
- [ ] Determine if Files view initialization is O(n²) with replicate count
- [ ] Fix or optimize the slow path
- [ ] Verify test returns to ~136s baseline

## Progress Log

### 2026-01-02 - Session Start

Starting work on this issue. Will begin by profiling the test to identify the bottleneck.
