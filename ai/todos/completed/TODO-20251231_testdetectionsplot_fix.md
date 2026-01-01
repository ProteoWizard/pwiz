# TODO-20251231_testdetectionsplot_fix.md

## Branch Information
- **Branch**: `Skyline/work/20251231_testdetectionsplot_fix`
- **Base**: `master`
- **Created**: 2025-12-31
- **Completed**: 2025-12-31
- **Status**: Complete
- **PR**: [#3742](https://github.com/ProteoWizard/pwiz/pull/3742)
- **Objective**: Fix TestDetectionsPlot nightly test failure caused by missing timer-driven update

## Background
PR #3730 (Relative Abundance Graph Performance) removed direct `UpdateUI()` calls from `GraphSummary.OnDocumentUIChanged` to consolidate all graph updates through the timer-driven mechanism in `SkylineWindow.UpdateGraphPanes()`. This prevents duplicate updates and maintains UI responsiveness.

However, the Detections plot (`_listGraphDetections`) was not added to the timer-driven update mechanism, causing `TestDetectionsPlot` to fail on 5 nightly test machines (BDCONNOL-UW1, BRENDANX-DT1, BRENDANX-UW7, EKONEIL01, RITACH-DSK).

## Root Cause
In `SkylineGraphs.cs`, the `UpdateGraphPanes()` method adds visible graphs to a list for timer-based updates:

```csharp
listUpdateGraphs.AddRange(_listGraphRetentionTime.Where(g => g.Visible));
listUpdateGraphs.AddRange(_listGraphPeakArea.Where(g => g.Visible));
listUpdateGraphs.AddRange(_listGraphMassError.Where(g => g.Visible));
// _listGraphDetections was missing here!
```

The Detections plot was relying on the now-removed direct `UpdateUI()` call and had no other update mechanism.

## Fix Applied

Added `_listGraphDetections` to the timer update list in `SkylineGraphs.cs:UpdateGraphPanes()`:

```csharp
listUpdateGraphs.AddRange(_listGraphRetentionTime.Where(g => g.Visible));
listUpdateGraphs.AddRange(_listGraphPeakArea.Where(g => g.Visible));
listUpdateGraphs.AddRange(_listGraphMassError.Where(g => g.Visible));
listUpdateGraphs.AddRange(_listGraphDetections.Where(g => g.Visible));  // Added
```

## Audit: All GraphSummary Lists Now Covered

| List | Controller | In Timer Update? |
|------|------------|------------------|
| `_listGraphRetentionTime` | RTGraphController | Yes |
| `_listGraphPeakArea` | AreaGraphController | Yes |
| `_listGraphMassError` | MassErrorGraphController | Yes |
| `_listGraphDetections` | DetectionsGraphController | Yes (after fix) |

## Files Modified
- `pwiz_tools/Skyline/SkylineGraphs.cs` - Added `_listGraphDetections` to `UpdateGraphPanes()`

## Testing
- [x] TestDetectionsPlot passes locally

## Related
- Parent PR: #3730 (Relative Abundance Graph Performance)
- Phase 9 of TODO-20251221_relative_abundance_perf.md removed the direct UpdateUI() call
