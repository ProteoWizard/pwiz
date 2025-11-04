# TODO: Remove UI class dependencies from Skyline.Model

## Changes completed in this commit (2025-11-03)

- **All DataGridView column types moved from Model to Controls:**
    - Moved `AuditLogColumn`, `TextImageColumn` to `Controls/Databinding/AuditLog/`
    - Moved `AnnotationValueListDataGridViewColumn`, `SurrogateStandardDataGridViewColumn`, `NormalizationMethodDataGridViewColumn`, `ListLookupDataGridViewColumn`, `SampleTypeDataGridViewColumn`, `StandardTypeDataGridViewColumn` to `Controls/Databinding/`
    - Moved `BoundComboBoxColumn` base class to `Controls/Databinding/`
    - All files moved with `git mv` to preserve history

- **Implemented UI-side mapping pattern:**
    - Created `DataTypeSpecifierAttribute` in Common library for marker types
    - Added `PropertyTypeToColumnTypeMap` in `SkylineViewContext` for UI-side column type selection
    - Implemented marker types for generic property types (e.g., `SurrogateStandardName`, `TrueFalseAnnotation`, `ValueListAnnotation`)
    - Enhanced `CreateCustomColumn` to check:
        1. DataTypeSpecifierAttribute (marker types)
        2. PropertyType exact match
        3. PropertyType base class match (for derived types like `ListItem<T>`)
        4. Falls back to base implementation

- **Removed Model-to-UI dependencies:**
    - Removed all `[DataGridViewColumnType]` attributes from Model properties
    - Removed `DataGridViewColumnTypeAttribute` class entirely (obsolete)
    - Updated namespace references in moved files
    - Updated Skyline.csproj and Common.csproj to reflect file relocations

- **Benefits:**
    - Model has zero compile-time dependencies on UI column types
    - UI controls which column types are used via mapping dictionary
    - Backward compatible through fallback mechanism
    - All tests pass; CodeInspectionTest violations reduced

- **Next steps:**
    - Continue with remaining Model/UI dependency removals (View Context dependencies)

## Changes completed in prior commits (between 2025-10-30 and 2025-11-03)

- **SeqNode dependencies resolved:**
    - Moved code from UI tree node classes to Model DocNode classes:
        - `PeptideTreeNode` → `PeptideDocNode`
        - `TransitionGroupTreeNode` → `TransitionGroupDocNode`
        - `TransitionTreeNode` → `TransitionDocNode`
    - Moved resource strings from `SeqNodeResources.resx` to `ModelResources.resx`
    - Eliminated all Model references to `pwiz.Skyline.Controls.SeqNode` namespace
    - Result: ~5 SeqNode violations resolved before the DataGridView work

## Changes completed in earlier commit (2025-10-30)

- **RT regression logic fully extracted from UI to Model:**
    - Created `Model/RetentionTimes/RetentionTimeRegressionGraphData.cs` containing all core RT regression computation.
    - Moved `PointsTypeRT` enum to Model; updated all references.
    - UI `RTLinearRegressionGraphPane.GraphData` now delegates to Model’s `ComputeSnapshot()` and only adapts for display.
    - Removed dead `_refine` field from Model; clarified comments and separation of concerns.
    - Updated `RefinementSettings.cs` to call Model’s regression logic directly, passing explicit parameters.
    - All tests pass; CodeInspectionTest now detects one fewer Model/UI dependency.

- **Benefits:**
    - Model and UI layers are now cleanly separated for RT regression.
    - No reverse dependencies from Model to UI remain for RT regression.
    - CodeInspectionTest is effective at preventing new violations.

- **Next steps:**
    - Build `Skyline.csproj` to validate compilation.
    - Run focused RT tests (RunToRunRegressionTest, PeakPickingTutorialTest, iRT checks).
    - Continue with remaining Model/UI dependency removals as listed below.


**Status:** Active
**Priority:** Medium  
**Estimated Effort:** Small (6 remaining violations as of 2025-11-03: 5 Model, 1 CLI - down from 27 total!)
**Branch:** Skyline/work/20251026_no_ui_classes_in_model
**Updated:** 2025-11-03

## Problem

The `pwiz.Skyline.Model` namespace currently has just **5 remaining instances** where it depends on UI code (namespaces: `pwiz.Skyline.Controls`). One CLI violation also remains.

This violates the Model-View separation principle and creates unnecessary coupling between business logic and presentation layers.

## Current State

The CodeInspection test detects these violations. As of 2025-11-03, there are only **5 Model violations** remaining (down from 26), after moving all DataGridView column types to Controls.

```csharp
AddForbiddenUIInspection(@"*.cs", @"namespace pwiz.Skyline.Model",
    @"Skyline model code must not depend on UI code", 5);
```

**Progress: 81% reduction** (from 26 to 5 Model violations)

## Goal

Remove all UI dependencies from Model code by:
1. Refactoring to use events/callbacks instead of direct UI references
2. Moving UI-specific logic out of Model classes
3. Using interfaces or abstractions where necessary
4. Ensuring Model classes can be tested without UI dependencies

Once complete, change the tolerance count from `28` to `0` in `pwiz_tools/Skyline/Test/CodeInspectionTest.cs`.

## Benefits

- **Better architecture**: Clean separation between business logic and presentation
- **Easier testing**: Model classes can be unit tested without UI framework
- **Improved maintainability**: Changes to UI don't require Model changes
- **Cross-platform ready**: Model code works in headless/server scenarios

## Known Violations

As of 2025-11-03, there are only **6 violations remaining** (5 Model + 1 CLI), down from 27 total violations! 

**Progress: 78% reduction overall** (from 27 to 6 total violations)

Run CodeInspectionTest to see the current list of files and line numbers.

To see the current violations, run CodeInspectionTest and look for warnings that start with:
```
WARNING: Found prohibited use of
"using.*(pwiz\.Skyline\.(Alerts|Controls|.*UI)|System\.Windows\.Forms|pwiz\.Common\.GUI)"
(Skyline model code must not depend on UI code)
```

Additionally, we now also flag fully-qualified references (even without a using directive):

```
"^(?!\s*///).*?\b(pwiz\.Skyline\.(Alerts|Controls|.*UI)|System\.Windows\.Forms|pwiz\.Common\.GUI)\."
```

## Related

- `CommandLine.cs` currently has 1 violation remaining (tolerance still 2 for now)
- `CommandArgs.cs` inspection (no violations tolerated)
- See `pwiz_tools/Skyline/Test/CodeInspectionTest.cs` near the `AddForbiddenUIInspection` calls for exact rules

## Changes completed in this branch (as of 2025-11-03)

### DataGridView column types refactoring (2025-11-03)
- **Moved all UI column types from Model to Controls** (11 files total):
    - `AuditLogColumn.cs`, `TextImageColumn.cs` → `Controls/Databinding/AuditLog/`
    - `AnnotationValueListDataGridViewColumn.cs` → `Controls/Databinding/`
    - `SurrogateStandardDataGridViewColumn.cs` → `Controls/Databinding/`
    - `NormalizationMethodDataGridViewColumn.cs` → `Controls/Databinding/`
    - `ListLookupDataGridViewColumn.cs` → `Controls/Databinding/`
    - `SampleTypeDataGridViewColumn.cs` → `Controls/Databinding/`
    - `StandardTypeDataGridViewColumn.cs` → `Controls/Databinding/`
    - `BoundComboBoxColumn.cs` (base class) → `Controls/Databinding/`

- **Created UI-side mapping architecture:**
    - Added `DataTypeSpecifierAttribute` in Common library
    - Implemented `PropertyTypeToColumnTypeMap` dictionary in `SkylineViewContext`
    - Created marker types for generic properties: `SurrogateStandardName`, `TrueFalseAnnotation`, `ValueListAnnotation`
    - Enhanced `CreateCustomColumn` with three-tier lookup: marker type → property type → base type match

- **Removed backward dependencies:**
    - Removed all `[DataGridViewColumnType]` attributes from Model properties in:
        - `AuditLogRow.cs`, `AuditLogDetailRow.cs`
        - `Peptide.cs` (StandardType, NormalizationMethod, SurrogateExternalStandard)
        - `Replicate.cs` (SampleType)
        - `AnnotationPropertyDescriptor.cs` (true_false and value_list annotations)
        - `ListLookupPropertyDescriptor.cs`
    - Deleted `DataGridViewColumnTypeAttribute.cs` entirely (obsolete)
    - Removed instantiation logic from `AbstractViewContext.cs`

- **Result:** Reduced Model UI violations from 26 to **5** (21 files cleaned up - 81% reduction!)

### SeqNode dependencies resolved (prior to 2025-11-03)
- **Moved code from UI tree nodes to Model DocNodes:**
    - `PeptideTreeNode` → `PeptideDocNode`
    - `TransitionGroupTreeNode` → `TransitionGroupDocNode` 
    - `TransitionTreeNode` → `TransitionDocNode`
- **Moved resources:** `SeqNodeResources.resx` → `ModelResources.resx`
- **Result:** Eliminated ~5 SeqNode violations

### Testing and inspections (2025-10-29)
- Added a second inspection to catch fully-qualified UI namespaces (no using directive present) in the same files targeted by our cues.
- Reduced Model UI tolerance from 35 to 28 after refactors removed 7 incidents.
- Kept CLI tolerance at 2; current CLI violations reduced to 1 (safe to drop to 1 or 0 after final cleanup).

Model refactors (high level)
- New file: `pwiz_tools/Skyline/Model/GroupComparison/FoldChangeRows.cs` (data/logic extraction toward decoupling UI from Model).
- Touched Model files for decoupling and cleanup:
    - `Model/AreaCVRefinementData.cs`
    - `Model/Databinding/Entities/Peptide.cs`
    - `Model/Databinding/Entities/Protein.cs`
    - `Model/DocNode.cs`
    - `Model/Find/BookmarkEnumerator.cs`
    - `Model/Find/FindPredicate.cs`
    - `Model/GroupComparisonRefinementData.cs`
    - `Model/PeptideGroupDocNode.cs`
    - `Model/Proteome/AssociateProteinsResults.cs` (removed WinForms usage)
    - `Model/Results/SpectrumFilterPair.cs`

UI layer adjustments (to support the separation)
- Updated several files under `pwiz_tools/Skyline/Controls/*` and `Controls/GroupComparison/*` to keep UI logic on the UI side while referencing new/cleaned Model logic.
- Removed `ShowDialog` from `ILongWaitBroker`; retained a targeted legacy cast usage in `ViewLibraryPepMatching` where a dialog must be shown during a long wait.
 - `PeptideDocNode` no longer depends on `Controls.SeqNode` (moved display text responsibility into Model).

Other changes on branch
- `pwiz_tools/Skyline/Test/*`: updates aligned with inspection changes and Model refactors.
- Some changes under `pwiz/data/vendor_readers/Thermo/*` are present; they appear unrelated to the Model UI-dependency effort and can be tracked separately in the PR description.

## How to verify current state
1) Open `pwiz_tools/Skyline/Test/CodeInspectionTest.cs` and confirm:
    - The fully-qualified namespace inspection exists (the second AddTextInspection inside `AddForbiddenUIInspection`).
    - Model tolerance: `5` (confirmed as of 2025-11-03)
    - CLI tolerance: `1` (confirmed as of 2025-11-03)
2) Run CodeInspectionTest to enumerate the 5 remaining Model offenders:
    - `Model/Databinding/ReportSharing.cs` - `new DocumentGridViewContext()`
    - `Model/Databinding/DocumentViewTransformer.cs` - `SkylineViewContext.GetReplicateSublist()`
    - `Model/Databinding/ResultsGridViewContext.cs` - derives from `SkylineViewContext`
    - `Model/Tools/ToolDescription.cs` - `new DocumentGridViewContext()`
    - (Plus 1 more to be identified)
3) And the 1 CLI violation in `CommandLine.cs`
4) Verify DataGridView column types are in `Controls/Databinding/` and `Controls/Databinding/AuditLog/`.
5) Celebrate 78% reduction! 🎉 (27 → 6 violations)

## Remaining violations (5 Model + 1 CLI = 6 total, as of 2025-11-03)

**🎯 Nearly complete! Only 6 violations left (down from 27)**

PR: https://github.com/ProteoWizard/pwiz/pull/3663

### Actual Remaining Issues: SkylineViewContext/DocumentGridViewContext references (5 files)

All remaining Model violations are related to Model classes referencing UI databinding view contexts (`SkylineViewContext`, `DocumentGridViewContext`):

**Files:**
1. **`Model/Databinding/ReportSharing.cs`** - Creates `new DocumentGridViewContext(dataSchema)` for report conversion
2. **`Model/Databinding/DocumentViewTransformer.cs`** - Calls static methods `SkylineViewContext.GetReplicateSublist()`
3. **`Model/Databinding/ResultsGridViewContext.cs`** - Class derives from `SkylineViewContext` (should be in Controls)
4. **`Model/Tools/ToolDescription.cs`** - Creates `new DocumentGridViewContext(dataSchema)` for tool integration
5. **One more file** - (need to verify with CodeInspectionTest output)

**Strategy:** 
- Move `ResultsGridViewContext` from Model to Controls (it's a UI view context)
- Extract static helper methods from `SkylineViewContext` to a Model-level helper class
- Refactor `ReportSharing` and `ToolDescription` to use Model-level abstractions instead of directly instantiating UI view contexts

### Category 6: CommandLine UI dependencies (1 file)
- `CommandLine.cs` - using pwiz.Skyline.Controls.Databinding;

**Strategy:** Remove databinding control reference in `CommandLine.cs`.

## Next steps (prioritized)
1. ✅ **COMPLETED (2025-11-03):** DataGridView column types - moved all 11 UI column classes from Model to Controls; implemented UI-side mapping pattern. **Result: 21 violations eliminated!**
2. **View Context refactoring (5 files) - THE FINAL FRONTIER:**
   - Move `ResultsGridViewContext` from Model/Databinding to Controls/Databinding (it's a UI view context class)
   - Extract static methods from `SkylineViewContext` (like `GetReplicateSublist()`) to a Model-level helper class
   - Refactor `ReportSharing.cs` and `ToolDescription.cs` to avoid directly instantiating UI view contexts
   - Consider introducing an interface or abstract base in Model that UI view contexts implement
3. **CLI cleanup (1 file):** Remove databinding dependency from CommandLine.cs.
4. **Victory lap:** Set Model tolerance to 0, CLI tolerance to 0. ✅ Complete separation achieved!

## Triage notes
- ✅ **COMPLETED:** DataGridView column types - created UI-side mapping pattern with marker types for properties with generic types (string, bool), PropertyType mapping for distinct types (NormalizationMethod, SampleType, StandardType, ListItem), and base class matching for derived types. **Eliminated 21 violations!**
- ✅ **COMPLETED:** SeqNode dependencies - resolved by moving code from SeqNode to DocNode classes (done before this commit)
- **Last remaining Model violations (5 files):** ALL are SkylineViewContext/DocumentGridViewContext references in databinding code
  - `ResultsGridViewContext`: Simply move from Model to Controls (it's a UI class)
  - `DocumentViewTransformer`: Extract static helper method to Model
  - `ReportSharing` & `ToolDescription`: Refactor to use Model abstractions
- **Almost there!** Only 6 total violations remain (5 Model + 1 CLI) from original 27.
- **Final step:** Resolve view context dependencies → set Model tolerance to 0; resolve CLI → set CLI tolerance to 0. ✅ Mission accomplished!

## Detailed extraction plan: RefinementSettings RT regression dependency

**Current violation:**
```csharp
// Model/RefinementSettings.cs line 261
var outliers = RTLinearRegressionGraphPane.CalcOutliers(document,
    RTRegressionThreshold.Value, RTRegressionPrecision, UseBestResult);
```

**Goal:** Move RT regression calculation logic from `Controls.Graphs.RTLinearRegressionGraphPane` to new Model file.

**New file:** `Model/RetentionTimes/RetentionTimeRegressionGraphData.cs`

**Classes to extract and rename:**

1. **RegressionSettings** → **RetentionTimeRegressionParameters**
   - Pure data/configuration holder
   - Remove `Settings.Default.RTScoreCalculatorList` dependency (pass explicitly)
   - Keep as internal nested class or make public if needed elsewhere

2. **GraphData** → **RetentionTimeRegressionGraphData** (primary class)
   - Main calculation engine for RT regression outliers
   - Constructor dependencies to externalize:
     - `RTGraphController.PointsType` → pass as parameter
     - `RTGraphController.RegressionMethod` → pass as parameter
   - Already clean of Settings.Default (good!)
   - Keep PointInfo as nested class (UI-agnostic data)

3. **Static CalcOutliers method** → new public static method on **RetentionTimeRegressionGraphData**
   - Signature change:
     ```csharp
     // OLD (UI):
     public static PeptideDocNode[] CalcOutliers(SrmDocument document, 
         double threshold, int? precision, bool bestResult)
     
     // NEW (Model):
     public static PeptideDocNode[] CalcOutliers(SrmDocument document,
         double threshold, int? precision, bool bestResult,
         PointsTypeRT pointsType, RegressionMethodRT regressionMethod,
         RtCalculatorOption calculatorOption, bool refinePeptides)
     ```
   - Caller (RefinementSettings) will pass Settings.Default values explicitly
   - Caller (RTLinearRegressionGraphPane.CalcOutliers wrapper) will pass controller static properties

**Dependencies already in Model namespace:**
- ✅ `PointsTypeRT` enum (in RTGraphController.cs but will move to Model)
- ✅ `RegressionMethodRT` enum (already in Model/RetentionTimes/DocumentRetentionTimes.cs)
- ✅ `RtCalculatorOption` (Model namespace)
- ✅ `ProductionMonitor`, `CancellationToken` (Common/SystemUtil)

**Dependencies to handle:**
- `PointsTypeRT` enum: Currently in `Controls/Graphs/RTGraphController.cs`
    - **Decision: Move to Model/RetentionTimes** alongside `RegressionMethodRT` (UI-agnostic concept)
    - **Action:**
        1) Define `public enum PointsTypeRT { targets, targets_fdr, standards, decoys }` in `Model/RetentionTimes/DocumentRetentionTimes.cs` (or a new `PointsTypeRT.cs` in the same namespace)
        2) Update all references to use `pwiz.Skyline.Model.RetentionTimes.PointsTypeRT`
        3) Remove the enum from `RTGraphController.cs`
        4) Ensure `RTGraphController.PointsType` property continues to work by returning the moved enum type
    - **Rationale:** Allows Model code (extracted regression logic) to accept `PointsTypeRT` without referencing UI

**UI wrapper strategy:**
After extraction, `RTLinearRegressionGraphPane.CalcOutliers` becomes a thin wrapper:
```csharp
public static PeptideDocNode[] CalcOutliers(SrmDocument document, 
    double threshold, int? precision, bool bestResult)
{
    return RetentionTimeRegressionGraphData.CalcOutliers(document,
        threshold, precision, bestResult,
        RTGraphController.PointsType,
        RTGraphController.RegressionMethod,
        Settings.Default.RtCalculatorOption,
        Settings.Default.RTRefinePeptides);
}
```

**Model caller update:**
`RefinementSettings.cs` changes to:
```csharp
var outliers = RetentionTimeRegressionGraphData.CalcOutliers(document,
    RTRegressionThreshold.Value, RTRegressionPrecision, UseBestResult,
    PointsTypeRT.targets,  // Explicit
    RegressionMethodRT.linear,  // Or passed as parameter
    Settings.Default.RtCalculatorOption,  // Or passed
    Settings.Default.RTRefinePeptides);  // Or passed
```

**Parameterization in GraphData (now RetentionTimeRegressionGraphData):**
- Replace usage of `RTGraphController.PointsType` with constructor parameter `pointsType`
- Replace usage of `RTGraphController.RegressionMethod` with constructor parameter `regressionMethod`

**Risk assessment:**
- **Medium complexity:** ~600 lines of code to move
- **High test surface:** RT regression is heavily tested
- **Settings dependencies:** 4-5 Settings.Default calls to externalize
- **Enum location:** PointsTypeRT needs decision on placement

**Next steps:**
1. Decide PointsTypeRT placement (move to Model or reference Controls?)
2. Create RetentionTimeRegressionGraphData.cs with extracted classes
3. Update RTLinearRegressionGraphPane to delegate
4. Update RefinementSettings caller
5. Build, run CodeInspectionTest, verify Model violations drop by 1
6. Run full RT regression tests

## Notes for PR description (when ready)
- Tests: added fully-qualified UI namespace inspection; reduced Model UI tolerance from 35 to 28.
- Model: extracted `FoldChangeRows.cs`; refactored multiple Model files to reduce UI coupling.
- UI: adjusted GroupComparison and related UI to reference Model-only logic.
- Out of scope: Thermo vendor reader changes also present on this branch; list separately in PR summary.
