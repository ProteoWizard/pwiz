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

## Phase 1.5: Multi-Replicate Caching (PLANNED)

### Issue
Currently switching replicates always triggers recalculation because the `Producer`/`Receiver` pattern only caches the most recent result. Switching from replicate 1 → 2 → 1 recomputes replicate 1.

### Research Needed
Review other document-wide summary graphs that support single replicate vs. all mode:
- `MassErrorHistogramGraphPane` - may have multi-replicate caching
- Other summary graph panes

### Potential Solutions
1. Modify `ProductionFacility` to keep multiple cached results
2. Maintain local cache of `GraphData` per replicate in the pane

## Phase 2: Incremental Updates (PLANNED)

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
