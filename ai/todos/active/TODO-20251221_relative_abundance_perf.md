# TODO-20251221_relative_abundance_perf.md

## Branch Information
- **Branch**: `Skyline/work/20251221_relative_abundance_perf`
- **Base**: `master`
- **Created**: 2025-12-21
- **Completed**: (pending)
- **Status**: 🔍 In Review
- **PR**: [#3730](https://github.com/ProteoWizard/pwiz/pull/3730)
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

#### Future Enhancement: Smarter Cache Invalidation by Normalization Type (Nick's Feedback)

The current implementation invalidates all cached entries when quantification settings change. A smarter approach would consider the normalization method:

1. **Median Normalization**: Any change to targets can invalidate everything due to median instability. Simplify to document reference equality (like RT graph).

2. **Global Standards**: Only changes to global standard peptides invalidate everything. There's existing code to detect global standards changes for ratio recalculation.

3. **Surrogate Standards**: Changes to a surrogate standard only invalidate entries that normalize to it. There's likely existing code similar to global standards detection.

**Implementation Notes:**
- Move `HasEqualQuantificationSettings()` from `SrmSettings` to `SrmDocument` (needs access to `Document.Children`)
- Call outside the per-entry loop in `CleanCacheForIncrementalUpdates` for performance
- Look for existing ratio recalculation code as reference for detecting standard changes

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

## Review Feedback: Producer Composition Pattern

### Nick's Suggestion (PR #3730 Review)

Nick suggested that the ProductionFacility pattern already supports caching via **producer composition**:

> "The way that the ProductionFacility stuff would want this to be accomplished is that there are two different producers, and one of the producers overrides GetInputs to return the other as an 'Input'. For instance, the GetInputs method in `AssociateProteinResults.Producer` requests something that depends only on `ParsimonyIndependentParameters`, which enables switching between parsimony settings without having to recalculate everything."

### The Pattern (from `AssociateProteinsResults.cs`)

```csharp
// Two parameter classes - one subset of the other
class ParsimonyIndependentParameters { Document, FastaFilePath, BackgroundProteome }
class Parameters : ParsimonyIndependentParameters { + ParsimonySettings, IrtStandard, ... }

// Two producers - one depends on the other via GetInputs()
class ResultsProducer : Producer<Parameters, AssociateProteinsResults>
{
    public override IEnumerable<WorkOrder> GetInputs(Parameters parameter)
    {
        // Request the expensive preliminary results (cached by ProductionFacility)
        yield return PRELIMINARY_RESULTS_PRODUCER.MakeWorkOrder(
            new ParsimonyIndependentParameters(parameter.Document, ...));
    }

    public override AssociateProteinsResults ProduceResult(...)
    {
        // Get cached preliminary results from inputs dictionary
        var preliminaryResults = inputs.Values.OfType<ParsimonyIndependentResults>().First();
        // Only do the cheap parsimony-dependent work
        return ProduceResults(productionMonitor, parameter, preliminaryResults);
    }
}
```

The ProductionFacility automatically caches the preliminary producer's results. Changing only parsimony settings reuses the cached expensive computation.

### How This Would Apply to Relative Abundance Graph

**Current structure:**
- `GraphDataParameters` includes replicate index
- Full calculation runs for each replicate

**Producer composition approach:**
1. `ReplicateIndependentParameters` - Document, GraphSettings (no replicate index)
2. `ReplicateIndependentResults` - All points calculated, full sorted list, positions assigned
3. `ReplicateDependentProducer` - Takes base results, applies replicate-specific Y value lookup and highlighting

The expensive O(n log n) sort would be cached, and switching replicates would only run the O(n) replicate-specific processing.

### Assessment: What ReplicateCachingReceiver Provides

| Feature | ProductionFacility | ReplicateCachingReceiver |
|---------|-------------------|--------------------------|
| **Dependency caching** | ✅ Via GetInputs() | ❌ N/A (wrapper pattern) |
| **Stale-while-revalidate** | ❌ No - entry removed on unlisten | ✅ Yes - shows previous result |
| **Completion listeners** | ❌ No - calculation cancelled on unlisten | ✅ Yes - calculation continues |
| **Local replicate cache** | ❌ No - would need producer refactor | ✅ Yes - ConcurrentDictionary |

**Key ProductionFacility behavior** (from `ProductionFacility.cs`):
```csharp
private void AfterLastListenerRemoved()
{
    _cancellationTokenSource?.Cancel();  // Calculation CANCELLED
    Cache.RemoveEntry(this);              // Entry REMOVED
}
```

When the user navigates away (listener removed), ProductionFacility **cancels** the calculation and **removes** the cached entry. This means:
1. No stale-while-revalidate (graph would blank while recalculating)
2. No background completion (switching away loses progress)

### Recommendation

**Keep `ReplicateCachingReceiver` for now** - it provides UX features (stale-while-revalidate, completion listeners) that ProductionFacility doesn't currently support.

**Future consideration**: If ProductionFacility is enhanced with:
1. Option to keep entries alive during recalculation (stale-while-revalidate)
2. Option to continue calculations when listener count drops to zero

Then refactoring to producer composition would be cleaner architecturally and would eliminate the need for the wrapper class.

**Alternatively**: The producer composition pattern could be used for the **incremental update** optimization (Phase 3), where the "prior results" could be an explicit input dependency rather than passed through the parameters.

### Additional Note

`AssociateProteinsResults.cs` uses `RuntimeHelpers.GetHashCode(Document)` in its `Parameters.GetHashCode()` method (lines 118 and 333). This is another candidate for the CodeInspection ban backlog item.

## Phase 6: Two-Phase Change Detection Fix ✅ COMPLETE

### Critical Bug Found
The incremental update was not detecting changed nodes because it only checked identity match (GlobalIndex), not `ReferenceEquals` on the DocNodes themselves.

### Root Cause
1. `BuildNewDocNodeSet` returned `HashSet<int>` of GlobalIndex values
2. `PartitionPriorData` checked `if (newDocNodeKeys.Contains(priorNodeKey))` - only identity match
3. `CalculateChangedPoints` checked `if (priorNodePositions.ContainsKey(nodeKey))` - skipped ALL prior nodes, including changed ones

A node that changed (same identity, different DocNode reference) was:
- NOT added to `unchanged` (ReferenceEquals failed)
- SKIPPED by CalculateChangedPoints (existed in prior data)
- Result: node lost from output entirely

### The Fix (Two-Phase Change Detection)
Per `ai/docs/architecture-data-model.md`:
1. **Phase 1**: Match by identity (GlobalIndex) - like finding a database row by ID
2. **Phase 2**: Check `ReferenceEquals(newNode, priorNode)` - if true, subtree unchanged

Implementation:
1. `BuildNewDocNodeMap` returns `Dictionary<int, DocNode>` mapping identity → DocNode
2. `PartitionPriorData` uses `TryGetValue` + `ReferenceEquals` to find truly unchanged nodes
3. `CalculateChangedPoints` takes `HashSet<int> unchangedKeys` - only skips truly unchanged nodes
4. Added `Assume.AreEqual(newDocNodes.Count, _nodePositions.Count)` assertion

### Files Modified
- `SummaryRelativeAbundanceGraphPane.cs`:
  - `BuildNewDocNodeSet` → `BuildNewDocNodeMap` (returns Dictionary)
  - `PartitionPriorData` - returns only unchanged list, uses ReferenceEquals
  - `CalculateChangedPoints` - takes unchangedKeys HashSet
  - `CalcDataPositionsIncremental` - builds unchangedKeys set, adds assertion

## Phase 7: UI Polish ✅ COMPLETE

### Bug 1: Hand Cursor Flickering
The Relative Abundance graph cursor flickered between Hand and Cross when moving over clickable points.

**Root Cause 1**: `HandleMouseMoveEvent` in `SummaryRelativeAbundanceGraphPane` never set `GraphSummary.Cursor = Cursors.Hand` when over a point.

**Root Cause 2 (ZedGraph bug)**: In `ZedGraphControl_MouseMove`, `SetCursor(mousePt)` was called BEFORE checking if the `MouseMoveEvent` handler handled the event. This caused:
1. `SetCursor` → sets cursor to Cross (zoom enabled)
2. `MouseMoveEvent` handler → sets cursor to Hand, returns true
3. User sees cursor flicker Cross→Hand on every mouse move

**Fix**:
1. Changed to `sender.Cursor = Cursors.Hand;` - must use the ZedGraphControl (sender), not a parent container
2. Simplified else branch to just `return false;` (don't call base, let ZedGraph handle cursor reset)
3. **ZedGraph fix**: Moved `SetCursor(mousePt)` to AFTER the `MouseMoveEvent` check, so it only runs when the handler returns false or doesn't exist

**Files Modified**:
- `SummaryRelativeAbundanceGraphPane.cs` - cursor fix using sender
- `RTLinearRegressionGraphPane.cs` - cursor fix using sender
- `SummaryBarGraphPaneBase.cs` - cursor fix using sender (2 places)
- `pwiz_tools/Shared/ZedGraph/ZedGraph/ZedGraphControl.Events.cs` - moved SetCursor call

**Pattern Note**: Always use `sender.Cursor` in `HandleMouseMoveEvent` handlers, never a parent container like `GraphSummary.Cursor`. The ZedGraphControl's cursor takes precedence over its parent's, so setting the parent's cursor has no effect while ZedGraph manages its own. Using `sender` is foolproof since it's always the control that received the event. Other files use `graphControl.Cursor` which works when `graphControl` IS the ZedGraphControl field, but `sender` is clearer and less error-prone.

### Bug 2: Progress Bar Z-Order
The progress bar can appear behind point annotation labels, making it hard to see.

**Root Cause**: Both progress bar and labels have `ZOrder.A_InFront`. Within same z-order, render order depends on list position. Labels added after progress bar render on top.

**Fix**: In `PaneProgressBar.DrawBar()`, remove and re-add `_left` and `_right` to end of `GraphObjList` to ensure they render last:
```csharp
GraphPane.GraphObjList.Remove(_left);
GraphPane.GraphObjList.Remove(_right);
GraphPane.GraphObjList.Add(_left);
GraphPane.GraphObjList.Add(_right);
```

**File Modified**: `PaneProgressBar.cs`

## Phase 8: Cross-Replicate Incremental Updates ✅ COMPLETE

### Problem
When changing peak integration in one replicate, all other replicate caches were cleared. Switching to another replicate would trigger a full recalculation instead of using incremental updates.

### Root Cause
`ReplicateCachingReceiver.TryGetProduct()` cleared the entire cache whenever the document **reference** changed:

```csharp
if (!ReferenceEquals(document, _cachedDocument) || !Equals(settings, _cachedSettings))
{
    ClearCache();  // Clears ALL replicate caches!
}
```

When a peak integration changes:
1. Document reference changes (new immutable document)
2. UpdateGraph for current replicate correctly uses incremental update
3. But cache for OTHER replicates was cleared and couldn't do incremental update

### The Fix

Modified `ReplicateCachingReceiver` to support identity-based cache invalidation:

1. **New callbacks in constructor**:
   - `getDocumentIdentity`: Extracts identity for cache invalidation (default: reference equality)
   - `getResultDocument`: Extracts document from result for background-completed calculations

2. **CacheEntry struct**: Stores result paired with its source document

3. **Identity-based invalidation**: Cache only cleared when document **Identity** changes (new file opened), not when document **reference** changes (same file edited)

4. **Exact cache hit detection**: A cache entry is only a "hit" if the document reference matches exactly. Otherwise, it's kept for incremental updates.

### Implementation Details

```csharp
// CacheEntry stores result + document for incremental updates
private readonly struct CacheEntry
{
    public TResult Result { get; }
    public SrmDocument Document { get; }
}

// In TryGetProduct:
var documentIdentity = _getDocumentIdentity?.Invoke(document) ?? document;

// Only clear cache when identity changes, not reference
if (!Equals(documentIdentity, _cachedDocumentIdentity) || !Equals(settings, _cachedSettings))
{
    ClearCache();
}

// Cache entry is only a "hit" if document matches exactly
if (_localCache.TryGetValue(cacheKey, out var entry))
{
    if (ReferenceEquals(entry.Document, document))
    {
        result = entry.Result;
        return true;  // Exact hit
    }
    // Entry kept for incremental update via TryGetCachedResultWithDocument
}
```

### Files Modified
- `ReplicateCachingReceiver.cs`:
  - Added `CacheEntry` struct with Result and Document
  - Added `_getDocumentIdentity` and `_getResultDocument` callbacks
  - Changed `_localCache` from `ConcurrentDictionary<int, TResult>` to `ConcurrentDictionary<int, CacheEntry>`
  - Added `TryGetCachedResultWithDocument()` method
  - Updated `TryGetProduct()` for identity-based invalidation and exact-hit detection
  - Updated `CompletionListener.OnProductAvailable()` to store document with result

- `SummaryRelativeAbundanceGraphPane.cs`:
  - Passes `doc => doc.Id.GlobalIndex` as document identity callback
  - Passes `r => r.Document` as result document callback

- `RTLinearRegressionGraphPane.cs`:
  - Same pattern for consistency and better caching

### Result
- Switching replicates after peak integration change now uses incremental updates
- Cache is preserved across document edits (same file)
- Cache is correctly cleared when opening a new file (different identity)

## Testing
- Use ExtracellularVesicalMagNet.sky document from Peak Imputation DIA tutorial
- Test replicate switching performance
- Test protein selection responsiveness
- Test peak integration changes - verify incremental update works
- Test both protein mode AND peptide mode (47,000 peptides)
- **Add iron-clad test coverage** to `TestPeakAreaRelativeAbundanceGraph` for incremental updates

## Phase 8 Refactoring: Clean Cache Design ✅ COMPLETE

### Problem with Current Implementation
The original code passed many lambdas to `ReplicateCachingReceiver` constructor for accessing document, settings, cache key, and result document. This led to poor separation of concerns and was difficult to extend.

### Solution: Interface-Based Contracts

**Two simple interfaces** that parameter and result types implement:

```csharp
// In pwiz.Skyline.Model.CachingContracts.cs
public interface ICachingParameters
{
    SrmDocument Document { get; }
    int CacheKey { get; }           // Typically replicate index
    object CacheSettings { get; }   // Settings that affect result
}

public interface ICachingResult
{
    SrmDocument Document { get; }   // Document result was computed from
}
```

### Named Delegate for Cache Cleaning

```csharp
/// <summary>
/// Callback to determine which cache entries should be removed.
/// </summary>
/// <param name="currentDocument">The current document being displayed</param>
/// <param name="cachedEntries">Map of cache key to the document each entry was computed from</param>
/// <returns>Keys that should be removed from the cache</returns>
public delegate IEnumerable<int> CleanCacheCallback(
    SrmDocument currentDocument,
    IReadOnlyDictionary<int, SrmDocument> cachedEntries);
```

### Type Constraints on ReplicateCachingReceiver

```csharp
public class ReplicateCachingReceiver<TParam, TResult> : IDisposable
    where TParam : ICachingParameters
    where TResult : class, ICachingResult
{
    private readonly CleanCacheCallback _cleanCache;

    // Constructor now only needs receiver + optional cleanCache callback
    public ReplicateCachingReceiver(
        Receiver<TParam, TResult> receiver,
        CleanCacheCallback cleanCache = null)
    {
        _receiver = receiver;
        _cleanCache = cleanCache ?? DefaultCleanCache;
    }

    // TryGetProduct uses interface properties directly
    public bool TryGetProduct(TParam param, out TResult result)
    {
        var document = param.Document;
        var settings = param.CacheSettings;
        var cacheKey = param.CacheKey;
        // ...
    }
}
```

### RT Graph: Simple DefaultCleanCache Implementation

```csharp
// Clear all entries if any cached document differs from current
public static IEnumerable<int> DefaultCleanCache(
    SrmDocument currentDoc,
    IReadOnlyDictionary<int, SrmDocument> cachedEntries)
{
    if (cachedEntries.Values.Any(cachedDoc => !ReferenceEquals(cachedDoc, currentDoc)))
        return cachedEntries.Keys.ToList();
    return Enumerable.Empty<int>();
}
```

### RelativeAbundance Graph: Smart CleanCacheForIncrementalUpdates

The smart cache cleaning validates each entry:

1. **Check ChromatogramSet still exists** by reference in `Document.Settings.MeasuredResults.Chromatograms`
2. **For index -1** (all replicates): Check `ReferenceEquals(cachedDoc.Settings.MeasuredResults, currentDoc.Settings.MeasuredResults)`
3. **Keep valid entries** for incremental updates via `TryGetCachedResultWithDocument()`

### Implementation Changes

1. **Model/CachingContracts.cs** (NEW) - Interface definitions

2. **RetentionTimeRegressionSettings** implements `ICachingParameters`:
   ```csharp
   int ICachingParameters.CacheKey => TargetIndex;
   object ICachingParameters.CacheSettings => new {
       BestResult, Threshold, Refine, PointsType,
       RegressionMethod, CalculatorName, IsRunToRun, OriginalIndex
   };
   ```

3. **RtRegressionResults** implements `ICachingResult`:
   ```csharp
   public SrmDocument Document => RegressionSettings.Document;
   ```

4. **GraphDataParameters** implements `ICachingParameters`:
   ```csharp
   int ICachingParameters.CacheKey => ResultsIndex;
   object ICachingParameters.CacheSettings => new { GraphSettings, ShowReplicate };
   ```

5. **GraphData** implements `ICachingResult`:
   ```csharp
   public SrmDocument Document { get; }  // Already had this property
   ```

6. **Simplified constructors**:
   ```csharp
   // Before: 6 lambda parameters
   _graphDataReceiver = new ReplicateCachingReceiver<...>(
       receiver,
       s => s.Document,
       s => new { s.BestResult, ... },
       s => s.TargetIndex,
       DefaultCleanCache,
       r => r.RegressionSettings.Document);

   // After: 2 parameters (receiver + optional callback)
   _graphDataReceiver = new ReplicateCachingReceiver<...>(
       receiver,
       DefaultCleanCache);  // or CleanCacheForIncrementalUpdates
   ```

### Files Modified
- `pwiz_tools/Skyline/Model/CachingContracts.cs` (NEW)
- `pwiz_tools/Skyline/Model/RetentionTimes/RetentionTimeRegressionGraphData.cs`
- `pwiz_tools/Skyline/Controls/Graphs/RTLinearRegressionGraphPane.cs`
- `pwiz_tools/Skyline/Controls/Graphs/SummaryRelativeAbundanceGraphPane.cs`
- `pwiz_tools/Skyline/Controls/Graphs/ReplicateCachingReceiver.cs`
- `pwiz_tools/Skyline/Skyline.csproj`

### Benefits
- Clear contracts about what parameter/result types must provide
- Type safety via generic constraints
- Ctrl+Click navigation to interface implementations
- Easier to add new graph types that use caching
- Reduced lambda noise in constructors

### Bug Fixes in This Phase
- Fixed cache storing `param.Document` instead of `result.Document` - caused IndexOutOfRangeException
- Added defensive bounds check in `CleanCacheForIncrementalUpdates` for invalid cache entries
- Fixed O(n²) potential in CleanCacheForIncrementalUpdates by using Dictionary lookup
- Added `ClearCache()` call in RTLinearRegressionGraphPane calculator initialization to prevent stale partial results

### Additional Changes in This Phase
- **Extended incremental updates to peptide mode** - Previously only protein mode supported incremental updates. Now both modes use the same two-phase change detection pattern:
  - `NodePosition.DocNode` changed from `PeptideGroupDocNode` to `DocNode` to support both
  - `BuildNewDocNodeMap` returns identity map for either proteins or peptides
  - `CalculateChangedPoints` handles both protein (Protein entity) and peptide (Peptide entity) modes
- **Removed `CanUseIncrementalUpdate` property** - Redundant since `CleanCacheForIncrementalUpdates` callback now validates incremental update eligibility. The caller sets `PriorGraphData` only when appropriate.

### Documentation Updates
- Updated `ai/docs/architecture-data-model.md` to document Identity pattern beyond DocNode
- Added sections on XmlNamedIdElement, ChromatogramSet, and two-phase change detection
- Added "The Change... Pattern" section explaining ChangeProp/ImClone
- Expanded "Identity vs DocNode" with table comparing purposes and mutability

## Phase 9: Incremental Update Test Coverage ✅ COMPLETE

### Problem
Current tests pass whether incremental updates work or not - they just verify the final graph state, not *how* it was computed. If incremental update code regressed to full recalculation, tests would still pass.

### Solution: Counter-Based Verification
Instead of reference equality (which doesn't work due to PointPair recreation during merge), added diagnostic counters:

```csharp
// On GraphData class:
public int CachedNodeCount { get; private set; }
public int RecalculatedNodeCount { get; private set; }

// Exposed on SummaryRelativeAbundanceGraphPane:
public int CachedNodeCount => _graphData?.CachedNodeCount ?? 0;
public int RecalculatedNodeCount => _graphData?.RecalculatedNodeCount ?? 0;
```

Full calculation: `CachedNodeCount=0`, `RecalculatedNodeCount=total`
Incremental: `CachedNodeCount=unchanged`, `RecalculatedNodeCount=changed`

### Bug Fix: Duplicate Graph Updates
During testing, discovered that graphs were updating twice per document change:
1. **Direct call**: `GraphSummary.OnDocumentUIChanged` called `UpdateUI()` immediately
2. **Timer call**: `SkylineWindow.UpdateGraphPanes` called `UpdateUI()` via timer

This bypassed the timer-based debouncing mechanism designed to maintain UI responsiveness during rapid selection changes.

**Fix**: Removed direct `UpdateUI()` call from `GraphSummary.OnDocumentUIChanged`. All summary graphs now update only via the timer mechanism.

### Bug Fix: Stale Cache Data
Cache cleaning happened inside `TryGetProduct`, but `TryGetCachedResult` was called first, retrieving stale prior data before cleaning occurred.

**Fix**: Added `CleanStaleEntries(document)` method to `ReplicateCachingReceiver`. Called before `TryGetCachedResult` in `UpdateGraph`.

### Tests Added (TestIncrementalUpdate)
1. **Protein mode delete**: Delete peptide → 47 cached, 1 recalculated
2. **Protein mode undo**: Undo → 47 cached, 1 recalculated
3. **Peptide mode delete**: Delete peptide → all remaining cached, 0 recalculated
4. **Peptide mode undo**: Undo → N-1 cached, 1 recalculated
5. **Settings change**: Change NormalizationMethod → 0 cached (full recalc)

### Files Modified
- `GraphSummary.cs` - Removed direct UpdateUI() from OnDocumentUIChanged
- `ReplicateCachingReceiver.cs` - Added CleanStaleEntries() method
- `SummaryRelativeAbundanceGraphPane.cs` - Added counters, call CleanStaleEntries before TryGetCachedResult
- `PeakAreaRelativeAbundanceGraphTest.cs` - Added TestIncrementalUpdate with 5 test cases
- `SettingsExtensions.cs` - Added ChangePeptideQuantification extension method

## Phase 10: Normalization-Aware Cache Invalidation ✅ COMPLETE

### Problem
The original `HasEqualQuantificationSettings` only compared `PeptideSettings.Quantification`, but cache invalidation needs to be smarter based on normalization method.

### Implementation

**Enhanced `HasEqualQuantificationSettings` in SrmSettings.cs:**
- For GLOBAL_STANDARDS: Check if global standard peptides changed using `ReferenceEquals` on `GetPeptideStandards(StandardType.GLOBAL_STANDARD)` - follows the same pattern as `SrmDocument.SetChildren`
- Added documentation for all normalization method behaviors
- Left TODO for RatioToSurrogate (requires extracting surrogate name from normalization method)

**Enhanced `CleanCacheForIncrementalUpdates` in SummaryRelativeAbundanceGraphPane.cs:**
- For EQUALIZE_MEDIANS: Require exact document match since any target change affects the median calculation
- This prevents incorrect caching when median normalization is active

### Test Added (Test 5b)
- Delete peptide while EQUALIZE_MEDIANS normalization is active
- Verifies `CachedNodeCount = 0` (no caching allowed)
- Verifies `RecalculatedNodeCount = originalPeptideCount - 1` (full recalculation)

### Files Modified
- `SrmSettings.cs` - Enhanced HasEqualQuantificationSettings with global standards check
- `SummaryRelativeAbundanceGraphPane.cs` - Added EQUALIZE_MEDIANS document equality check
- `PeakAreaRelativeAbundanceGraphTest.cs` - Added Test 5b for median normalization behavior

## Phase 11: Enhanced Test Coverage ✅ COMPLETE

### Test 3 Enhancement: Multi-Peptide Delete
Changed from deleting 1 peptide to deleting 4 disjoint peptides at indices 0, 10, 50, 100:
- Tests non-adjacent node deletion handling
- Verifies `CachedNodeCount = originalPeptideCount - 4`
- Verifies `RecalculatedNodeCount = 0` (delete doesn't recalculate)

### Test 5c Addition: Global Standards Cache Invalidation
Added test verifying `_cachedPeptideStandards` change triggers full recalculation:
- Changes normalization from EQUALIZE_MEDIANS to GLOBAL_STANDARDS
- Uses `FindNode("HLNGFSVPR")` and `SetStandardType(StandardType.GLOBAL_STANDARD)`
- Verifies `CachedNodeCount = 0` (full recalculation)
- Tests the `_cachedPeptideStandards` equality check in `HasEqualQuantificationSettings`

### Test 6 Addition: Document ID Change
Added test for reopening the same document file:
- Verifies `Document.Id` changes (new Identity object created)
- Verifies full recalculation triggered (`CachedNodeCount = 0`)
- Tests the "document identity changed" branch in `CleanCacheForIncrementalUpdates`

### HasEqualQuantificationSettings Refinements
Based on discussion about Nick's EQUALIZE_MEDIANS check being overly conservative:
- Removed the blanket `return false` for EQUALIZE_MEDIANS (settings can differ for reasons unrelated to abundance)
- Per-node invalidation for median normalization is handled in `CleanCacheForIncrementalUpdates`
- Added RatioToSurrogate check using `_cachedPeptideStandards` (conservative, same as GLOBAL_STANDARDS)
- Updated documentation with future optimization notes (compare actual peak areas instead of DocNode equality)

### Coverage Analysis Fix
Fixed `Analyze-Coverage.ps1` to handle generic type parameters:
- JSON has `ReplicateCachingReceiver<TParam,TResult>` but extracted type is `ReplicateCachingReceiver`
- Added regex to strip `<...>` suffix: `$node.Name -replace '<.*>$', ''`
- Now correctly reports coverage for generic classes

### Coverage Test List
Added `TestIrtTutorial` to coverage test list for better RTLinearRegressionGraphPane coverage:
- RTLinearRegressionGraphPane: 48.5% → 61.9% (+13.4%)

### Key Insight: Coverage vs Testing
Coverage measures code execution, not correctness. The existing tests already ran the incremental update code (71% coverage) but never verified the results were from incremental updates. The counter-based assertions catch both:
- **Not efficient enough**: `CachedNodeCount=0` when expected >0 (fell back to full recalc)
- **Too efficient**: `CachedNodeCount>0` when expected 0 (using stale cached data)

### Files Modified
- `SrmSettings.cs` - Refined HasEqualQuantificationSettings for normalization methods
- `PeakAreaRelativeAbundanceGraphTest.cs` - Enhanced Test 3, added Test 5c and Test 6
- `TODO-20251221_relative_abundance_perf-coverage.txt` - Added TestIrtTutorial
- `Analyze-Coverage.ps1` - Fixed generic type parameter handling

## Related
- Discovered during PR #3707 (Peak Imputation DIA Tutorial) review
- Pattern follows `RTLinearRegressionGraphPane` approach for background computation
- Updated `ai/docs/architecture-data-model.md` with two-phase change detection pattern
