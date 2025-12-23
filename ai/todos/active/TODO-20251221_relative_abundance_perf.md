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

## Phase 3: Incremental Updates ✅ COMPLETE

### Concept
When the document changes slightly (e.g., one peak's integration changes), we can update the sorted list incrementally instead of re-sorting from scratch.

### Key Architectural Insight
SrmDocument and its tree nodes are **immutable**. Modifications clone from the changed node up to root. Unchanged subtrees keep the **same object references**. This means `ReferenceEquals(nodeNew, nodeOld)` tells us instantly if a subtree changed.

See `ai/docs/architecture-data-model.md` for full documentation.

### Algorithm: Single-Pass Merge - O(n + k log k)

1. **Partition prior data** - Walk prior PointPairList in sorted order, check if each node reference exists in new doc
2. **Calculate changed points** - For new/changed nodes only, calculate abundance values
3. **Sort changed** - O(k log k) for the small changed list
4. **Merge** - Single-pass merge of unchanged (already sorted) with changed (just sorted)

### Implementation
- `GraphDataParameters.PriorGraphData` - passes prior result for incremental update
- `GraphDataParameters.CanUseIncrementalUpdate` - checks `HasEqualQuantificationSettings()`
- `NodePosition` struct - stores index, Y value, and doc node reference for ReferenceEquals
- `CalcDataPositionsIncremental()` - implements the merge algorithm
- Helper methods: `BuildNewDocNodeSet()`, `PartitionPriorData()`, `CalculateChangedPoints()`, `MergeSortedLists()`

### Settings Comparison
- Added `SrmSettings.HasEqualQuantificationSettings()` stub method
- Currently compares `PeptideSettings.Quantification`
- TODO for Nick: Expand to cover all settings that affect abundance calculations

## Phase 4: Progress Bar Improvements ✅ COMPLETE

### Issues Fixed
1. **Timer-based throttling** - Progress bar updates now use stopwatch-based throttling:
   - 300ms initial delay before showing (avoids flash for fast calculations)
   - 100ms throttle between updates after first show
2. **Thread safety** - Changed `_localCache` in `ReplicateCachingReceiver` to `ConcurrentDictionary`
   - `CompletionListener.OnProductAvailable` is called on background thread, was writing to regular Dictionary
   - This helped but did not fully fix intermittent test failures
3. **Removed ProgressMonitor.cs** - Deleted unused file that was accidentally left in .csproj
4. **RT graph cleanup** - Removed "Calculating..." title and legend hiding from RTLinearRegressionGraphPane
   - Progress bar alone is sufficient visual feedback
   - Prevents layout shift during calculation

### Files Modified
- `RTLinearRegressionGraphPane.cs` - Added progress throttling, removed title/legend changes
- `SummaryRelativeAbundanceGraphPane.cs` - Added progress throttling
- `ReplicateCachingReceiver.cs` - Changed to ConcurrentDictionary, added TryGetCachedResult()
- `SrmSettings.cs` - Added HasEqualQuantificationSettings() stub
- Deleted `ProgressMonitor.cs`

## Phase 5: Exception Handling & Bug Fix ✅ COMPLETE

### Problem Discovered
Intermittent test failure in `PeakImputationDiaTutorial` - test hangs waiting for `IsComplete` which never returns true. Using `LaunchDebuggerOnWaitForConditionTimeout = true` caught the issue:

**Symptom**: `CalcDataPositions` throws `ArgumentException: "An item with the same key has already been added"` at line 935 when adding to `nodePositions` dictionary.

### Root Cause Analysis Journey
1. **Initial theory**: Duplicate `PeptideGroupDocNode` references in `Document.MoleculeGroups`
2. **Added assertions** using `RuntimeHelpers.GetHashCode()` to trace duplicates
3. **Assertions fired** showing apparent duplicates at various indices
4. **Key insight**: Different proteins with different names were "duplicating"
5. **Discovery**: The duplicates were **hash collisions**, not actual reference duplicates!

### The Real Bug: `RuntimeHelpers.GetHashCode()` Collisions
`RuntimeHelpers.GetHashCode()` returns an identity hash code that is **NOT guaranteed unique**. With thousands of objects, collisions occur:
- Two different `PeptideGroupDocNode` objects can have the same hash code
- When used as dictionary keys, this causes "duplicate key" exceptions

**Evidence**:
- Error showed proteins like `sp|O60518|RNBP6_HUMAN` and `sp|Q9UBI1|COMD3_HUMAN` (clearly different) "colliding"
- Switching to `DocNode.Id.GlobalIndex` (guaranteed unique via `Interlocked.Increment()`) eliminated all false positives

### The Fix
Replaced all uses of `RuntimeHelpers.GetHashCode(docNode)` with `docNode.Id.GlobalIndex` in `SummaryRelativeAbundanceGraphPane.cs`:

1. `BuildNewDocNodeSet()` - line 788
2. `PartitionPriorData()` - line 818
3. `CalculateChangedPoints()` - line 852
4. `MergeSortedLists()` - lines 934, 996
5. Updated comment on `_nodePositions` dictionary

### Lesson Learned
- `RuntimeHelpers.GetHashCode()` is a **debugging convenience**, not a uniqueness guarantee
- For true object identity, use `DocNode.Id.GlobalIndex` (assigned via `Interlocked.Increment()`)
- This pattern exists because the codebase predates widespread knowledge of `RuntimeHelpers`

### Files Modified
- `SummaryRelativeAbundanceGraphPane.cs` - Replaced 5 uses of `RuntimeHelpers.GetHashCode()` with `GlobalIndex`

### Exception Handling (retained from earlier work)
The LongWaitDlg-pattern exception handling remains in place:
- `ReplicateCachingReceiver` - `ThrowIfError()`, `HasError`, single-throw guarantee
- Graph panes - try-catch with `DisplayOrReportException()`
- Background exceptions now surface immediately to tests

### Diagnostic Code
Temporary diagnostic assertions were added during investigation to trace duplicates through `DocNodeChildren`, `SrmDocument`, and `DocumentReader`. These were removed after confirming the root cause was hash collisions, not actual duplicates. Test passed 43 iterations (over 2x the longest pre-fix run) confirming the fix.

## Testing
- Use ExtracellularVesicalMagNet.sky document from Peak Imputation DIA tutorial
- Test replicate switching performance
- Test protein selection responsiveness
- Test peak integration changes (Phase 2)

## Related
- Discovered during PR #3707 (Peak Imputation DIA Tutorial) review
- Pattern follows `RTLinearRegressionGraphPane` approach for background computation
