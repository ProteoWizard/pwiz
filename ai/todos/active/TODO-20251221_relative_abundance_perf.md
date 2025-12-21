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
The Relative Abundance graph (`SummaryRelativeAbundanceGraphPane`) showed sluggish UI updates with large documents (e.g., 500K transitions). The `CalcDataPositions()` method was running on the UI thread and included an expensive O(n log n) sort.

## Phase 1: Move Sort to Background Thread ✅ COMPLETE

### Changes Made
1. **Extended `GraphDataParameters` cache key** to include:
   - `ReplicateDisplay ShowReplicate` - the display mode (single/best/all)
   - `int ResultsIndex` - which replicate (only used when ShowReplicate == single)

2. **Moved all computation to `GraphData` constructor** (runs on background thread):
   - Y value calculation for each point
   - Sorting by Y values (the expensive operation)
   - X coordinate assignment
   - Building identity-to-index dictionary for O(1) selection lookup

3. **Simplified UI thread work**:
   - `FindSelectedIndex()` uses dictionary lookup - O(1) instead of O(n)
   - `UpdateGraph()` just calls `FindSelectedIndex()` for selected protein

4. **`GetY()` refactored** to use instance properties instead of static `RTLinearRegressionGraphPane.ShowReplicate`

### Files Modified
- `pwiz_tools/Skyline/Controls/Graphs/SummaryRelativeAbundanceGraphPane.cs`
  - `GraphDataParameters` - added ShowReplicate, ResultsIndex
  - `GraphData` - constructor now does full computation, added `_identityToIndex` dictionary
  - `CalcDataPositions()` - now private, called from constructor
  - `FindSelectedIndex()` - new O(1) method using dictionary
  - `GetY()` - now instance method using stored ShowReplicate
- `pwiz_tools/Skyline/Controls/Graphs/AreaRelativeAbundanceGraphPane.cs`
  - `AreaGraphData` constructor updated for new parameters
  - `GraphDataProducerImpl.ProduceResult()` passes new parameters

### Expected Behavior After Phase 1
- First view of a replicate: brief "Calculating..." then graph appears
- Clicking different proteins: instant (O(1) dictionary lookup)
- UI remains responsive during calculation (work on background thread)
- X-axis properly scales to number of entries with values (bonus fix)
- Note: Replicate switching currently recalculates (see Phase 1.5)

## Phase 1.5: Multi-Replicate Caching ✅ COMPLETE

### Solution: ReplicateCachingReceiver
Created a reusable wrapper class that enhances the Producer/Receiver pattern with:

1. **Local cache by replicate index** - `Dictionary<int, TResult>` for instant switching
2. **Stale-while-revalidate** - Previous graph stays visible while calculating new data (no blank/flash)
3. **Background calculation preservation** - When switching away from an in-progress calculation, a `CompletionListener` keeps it running so results are cached when complete (like browser tabs)
4. **Cache invalidation** - Automatically clears when document or settings change

### Files Added/Modified
- `pwiz_tools/Skyline/Controls/Graphs/ReplicateCachingReceiver.cs` (NEW)
  - Generic wrapper: `ReplicateCachingReceiver<TParam, TResult>`
  - Nested `CompletionListener` class for background preservation
  - Reusable for `RTLinearRegressionGraphPane` and other summary graphs
- `pwiz_tools/Skyline/Controls/Graphs/SummaryRelativeAbundanceGraphPane.cs`
  - Changed field type to `ReplicateCachingReceiver<GraphDataParameters, GraphData>`
  - Moved `Clear()` after `TryGetProduct()` for stale-while-revalidate
- `pwiz_tools/Skyline/Skyline.csproj` - Added new file

### UX Result
- Seamless, flash-free replicate switching
- Rapidly switching replicates: graph never blanks, progress shown over previous data
- Switching back to a replicate: instant if cached, or resumes from where it left off
- Visual comparison between replicates is now possible (stable reference frames)

### Also Updated
- `ai/STYLEGUIDE.md`, `ai/docs/style-guide.md` - Added Claude Code attribution format
- `ai/docs/build-and-test-guide.md` - Documented process detection feature
- `pwiz_tools/Skyline/ai/Build-Skyline.ps1` - Added test process detection with LLM guidance

## Phase 2: Apply to RTLinearRegressionGraphPane ✅ COMPLETE

Applied `ReplicateCachingReceiver` wrapper to `RTLinearRegressionGraphPane` - proving separation of concerns:

### Changes Made
- Changed field type to `ReplicateCachingReceiver<RetentionTimeRegressionSettings, RtRegressionResults>`
- Wrapped receiver with caching config (document, settings, TargetIndex as cache key)
- Added dispose call for cleanup
- Moved `GraphObjList.Clear()` and `CurveList.Clear()` after `TryGetProduct()` for stale-while-revalidate

### Files Modified
- `pwiz_tools/Skyline/Controls/Graphs/RTLinearRegressionGraphPane.cs`

### UX Result
- Both Relative Abundance and RT Regression graphs now have seamless replicate switching
- Tested with ~5,000 proteins, 48,000 peptides - instant switching after first calculation

## Phase 3: Incremental Updates (PLANNED)

### Concept
When the document changes slightly (e.g., one peak's integration changes), we can update the sorted list incrementally instead of re-sorting from scratch.

### Approach
1. **Provide prior state to background computation** - pass the previous `GraphData` to the producer
2. **Diff the documents** using `ReferenceEquals()` on the immutable tree nodes
3. **Identify changed elements** - typically very few (often just one)
4. **Update sorted list incrementally**:
   - Remove changed items from their old positions
   - Insert them at their new positions based on new Y values
   - This is O(k log n) where k is the number of changes, vs O(n log n) for full re-sort

### Benefits
- Single peak integration change: O(1) to find changed item, O(log n) to reposition
- Much faster than full re-sort of thousands of items

## Testing
- Use ExtracellularVesicalMagNet.sky document from Peak Imputation DIA tutorial
- Test replicate switching performance
- Test protein selection responsiveness
- Test peak integration changes (Phase 2)

## Related
- Discovered during PR #3707 (Peak Imputation DIA Tutorial) review
- Pattern follows `RTLinearRegressionGraphPane` approach for background computation
