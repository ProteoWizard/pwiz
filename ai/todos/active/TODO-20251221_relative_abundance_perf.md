# TODO-20251221_relative_abundance_perf.md

## Branch Information
- **Branch**: `Skyline/work/20251221_relative_abundance_perf`
- **Base**: `Skyline/work/20251122_PeakImputationTutorial` (will rebase to master after parent merges)
- **Created**: 2025-12-21
- **Completed**: (pending)
- **Status**: 🚧 In Progress
- **PR**: (pending)
- **Objective**: Improve Relative Abundance graph performance by moving computation to background threads

## Background
Improve the performance of the Relative Abundance graph for large documents by moving more computation to background threads.

## Problem
The Relative Abundance graph (`SummaryRelativeAbundanceGraphPane`) shows sluggish UI updates with large documents (e.g., 500K transitions). While initial data gathering runs on a background thread, `CalcDataPositions()` runs on the UI thread and includes:
- Iterating through all graph points
- Creating PointPairs for each point
- **Sorting the entire list by Y values**
- Iterating again to set X coordinates

This causes noticeable UI freezes when changing replicate selection or other updates.

## Contrast with RT Graph
The Retention Times regression graph (`RTLinearRegressionGraphPane`) shows "Calculating..." and does all heavy computation in the background producer. When `TryGetProduct` returns, results are ready for immediate display.

## Proposed Solution
Move `CalcDataPositions` logic into the background producer so that:
1. Sorting happens on the background thread
2. The UI thread only needs to apply the pre-computed positions
3. Consider caching sorted results since Y values don't change based on selection

### Implementation Options
1. **Pre-sort in background** - Sort once when data is produced, store sorted order
2. **Include ResultsIndex in cache key** - Allow background computation to include replicate-specific positioning
3. **Incremental updates** - Only recalculate when data actually changes, not on every selection change

## Additional Issue: X-Axis Range
The x-axis range shows total proteins in the document instead of proteins with actual values. For example, if only 2000 proteins have data in a replicate, the axis should scale to ~2000, not 5000.

## Files to Modify
- `pwiz_tools/Skyline/Controls/Graphs/SummaryRelativeAbundanceGraphPane.cs`
  - `CalcDataPositions()` method (lines 665-708)
  - `UpdateGraph()` method
  - Consider changes to the `Producer` pattern

## Related
- Discovered during PR #3707 (Peak Imputation DIA Tutorial) review
- Large dataset: ExtracellularVesicalMagNet.sky with 500K transitions
