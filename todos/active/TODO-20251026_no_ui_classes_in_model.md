# TODO: Remove UI class dependencies from Skyline.Model# TODO: Remove UI class dependencies from Skyline.Model



**Status:** Active  ## Changes completed in this commit (2025-11-04)

**Priority:** Medium  

**Estimated Effort:** Small (5 remaining violations as of 2025-11-04: 4 Model, 1 CLI)  - **All DataGridView column types moved from Model to Controls:**

**Branch:** Skyline/work/20251026_no_ui_classes_in_model      - Moved `AuditLogColumn`, `TextImageColumn` to `Controls/Databinding/AuditLog/`

**PR:** https://github.com/ProteoWizard/pwiz/pull/3663      - Moved `AnnotationValueListDataGridViewColumn`, `SurrogateStandardDataGridViewColumn`, `NormalizationMethodDataGridViewColumn`, `ListLookupDataGridViewColumn`, `SampleTypeDataGridViewColumn`, `StandardTypeDataGridViewColumn` to `Controls/Databinding/`

**Started:** 2025-10-26      - Moved `BoundComboBoxColumn` base class to `Controls/Databinding/`

**Updated:** 2025-11-04      - All files moved with `git mv` to preserve history



## Overall Progress Summary- **Implemented UI-side mapping pattern:**

    - Created `DataTypeSpecifierAttribute` in Common library for marker types

- **Starting point (2025-10-26):** 35 Model violations detected by CodeInspectionTest    - Added `PropertyTypeToColumnTypeMap` in `SkylineViewContext` for UI-side column type selection

- **Current state (2025-11-04):** 4 Model violations + 1 CLI violation = **5 total remaining**    - Implemented marker types for generic property types (e.g., `SurrogateStandardName`, `TrueFalseAnnotation`, `ValueListAnnotation`)

- **Progress:** **86% reduction** in Model violations (from 35 to 5)    - Enhanced `CreateCustomColumn` to check:

        1. DataTypeSpecifierAttribute (marker types)

**Violation reduction timeline:**        2. PropertyType exact match

- Oct 26 (start): 35 violations        3. PropertyType base class match (for derived types like `ListItem<T>`)

- Oct 28 (CodeInspection improvements): 28 violations (-7)        4. Falls back to base implementation

- Oct 29 (ILongWaitBroker): 25 violations (-3)

- Oct 29 (PeptideTreeNode): 24 violations (-1)- **Removed Model-to-UI dependencies:**

- Oct 31 (DocNode/TreeNode flip): 20 violations (-4)    - Removed all `[DataGridViewColumnType]` attributes from Model properties

- Nov 1 (GroupComparison): 18 violations (-2)    - Removed `DataGridViewColumnTypeAttribute` class entirely (obsolete)

- Nov 1 (ListDefList): 17 violations (-1)    - Updated namespace references in moved files

- Nov 1 (SpectrumFilterAutoComplete): 16 violations (-1)    - Updated Skyline.csproj and Common.csproj to reflect file relocations

- Nov 3 (SpectrumDisplayInfo): 15 violations (-1)

- Nov 3 (TreeNode.TITLE): 14 violations (-1)- **Benefits:**

- Nov 3 (ReportOrViewSpecList): 13 violations (-1)    - Model has zero compile-time dependencies on UI column types

- Nov 3 (DataGridView columns): **5 violations** (-8)    - UI controls which column types are used via mapping dictionary

    - Backward compatible through fallback mechanism

The work involved systematic refactoring across six major categories over 11 commits.    - All tests pass; CodeInspectionTest violations reduced



---- **Next steps:**

    - Continue with remaining Model/UI dependency removals (View Context dependencies)

### Update (2025-11-04): Replicate sublist helper moved to Model

- Added `Model/Databinding/SublistPaths.cs` with `public static PropertyPath GetReplicateSublist(Type)`.
- Removed the method from `Controls/Databinding/SkylineViewContext` and updated call sites to use `SublistPaths.GetReplicateSublist(...)`:
    - `Model/Databinding/DocumentViewTransformer.cs`
    - `Controls/Databinding/SkylineViewContext.cs` (`GetDefaultViewInfo`)
    - `Controls/Databinding/PivotReplicateAndIsotopeLabelWidget.cs`
- Impact: Eliminated the Model → Controls dependency in `DocumentViewTransformer`, reducing Model violations from 5 to 4. Remaining Model offenders include:
    - `Model/Databinding/ReportSharing.cs` (instantiates `DocumentGridViewContext`)
    - `Model/Tools/ToolDescription.cs` (instantiates `DocumentGridViewContext`)
    - Plus 1–2 residual references to be confirmed via CodeInspectionTest.

## Problem

## Changes completed in prior commits (between 2025-10-30 and 2025-11-03)

The `pwiz.Skyline.Model` namespace currently has just **5 remaining instances** where it depends on UI code (namespaces: `pwiz.Skyline.Controls`). One CLI violation also remains.

- **SeqNode dependencies resolved:**

This violates the Model-View separation principle and creates unnecessary coupling between business logic and presentation layers.    - Moved code from UI tree node classes to Model DocNode classes:

        - `PeptideTreeNode` → `PeptideDocNode`

## Goal        - `TransitionGroupTreeNode` → `TransitionGroupDocNode`

        - `TransitionTreeNode` → `TransitionDocNode`

Remove all UI dependencies from Model code by:    - Moved resource strings from `SeqNodeResources.resx` to `ModelResources.resx`

1. Refactoring to use events/callbacks instead of direct UI references    - Eliminated all Model references to `pwiz.Skyline.Controls.SeqNode` namespace

2. Moving UI-specific logic out of Model classes      - Result: ~5 SeqNode violations resolved before the DataGridView work

3. Using interfaces or abstractions where necessary

4. Ensuring Model classes can be tested without UI dependencies## Changes completed in earlier commit (2025-10-30)



Once complete, change the tolerance count from `5` to `0` (Model) and from `1` to `0` (CLI) in `pwiz_tools/Skyline/Test/CodeInspectionTest.cs`.- **RT regression logic fully extracted from UI to Model:**

    - Created `Model/RetentionTimes/RetentionTimeRegressionGraphData.cs` containing all core RT regression computation.

## Current State    - Moved `PointsTypeRT` enum to Model; updated all references.

    - UI `RTLinearRegressionGraphPane.GraphData` now delegates to Model’s `ComputeSnapshot()` and only adapts for display.

The CodeInspection test detects these violations. As of 2025-11-03, there are only **5 Model violations** remaining:    - Removed dead `_refine` field from Model; clarified comments and separation of concerns.

    - Updated `RefinementSettings.cs` to call Model’s regression logic directly, passing explicit parameters.

```csharp    - All tests pass; CodeInspectionTest now detects one fewer Model/UI dependency.

AddForbiddenUIInspection(@"*.cs", @"namespace pwiz.Skyline.Model",

    @"Skyline model code must not depend on UI code", 5);- **Benefits:**

```    - Model and UI layers are now cleanly separated for RT regression.

    - No reverse dependencies from Model to UI remain for RT regression.

**Progress: 86% reduction** (from 35 to 5 Model violations)    - CodeInspectionTest is effective at preventing new violations.



---- **Next steps:**

    - Build `Skyline.csproj` to validate compilation.

## Changes Completed in This Branch (Commit-by-Commit)    - Run focused RT tests (RunToRunRegressionTest, PeakPickingTutorialTest, iRT checks).

    - Continue with remaining Model/UI dependency removals as listed below.

### 1. DataGridView Column Types Refactoring (2025-11-03)

**Commit:** `610d9c4677` - "move document grid column control descriptors into Controls\Databinding from Model\Databinding"

**Status:** Active

**Files moved from Model to Controls (11 total):****Priority:** Medium  

- `AuditLogColumn.cs`, `TextImageColumn.cs` → `Controls/Databinding/AuditLog/`**Estimated Effort:** Small (6 remaining violations as of 2025-11-03: 5 Model, 1 CLI - down from 27 total!)

- `AnnotationValueListDataGridViewColumn.cs` → `Controls/Databinding/`**Branch:** Skyline/work/20251026_no_ui_classes_in_model

- `SurrogateStandardDataGridViewColumn.cs` → `Controls/Databinding/`**Updated:** 2025-11-03

- `NormalizationMethodDataGridViewColumn.cs` → `Controls/Databinding/`

- `ListLookupDataGridViewColumn.cs` → `Controls/Databinding/`## Problem

- `SampleTypeDataGridViewColumn.cs` → `Controls/Databinding/`

- `StandardTypeDataGridViewColumn.cs` → `Controls/Databinding/`The `pwiz.Skyline.Model` namespace currently has just **5 remaining instances** where it depends on UI code (namespaces: `pwiz.Skyline.Controls`). One CLI violation also remains.

- `BoundComboBoxColumn.cs` (base class) → `Controls/Databinding/`

This violates the Model-View separation principle and creates unnecessary coupling between business logic and presentation layers.

**Architectural changes:**

- **Created `DataTypeSpecifierAttribute`** in Common library (renamed from `DataGridViewColumnTypeAttribute`)## Current State

- **Added `PropertyTypeToColumnTypeMap`** in `SkylineViewContext` for UI-side column type selection

- **Implemented marker types** for generic property types: `SurrogateStandardName`, `TrueFalseAnnotation`, `ValueListAnnotation`The CodeInspection test detects these violations. As of 2025-11-03, there are only **5 Model violations** remaining (down from 26), after moving all DataGridView column types to Controls.

- **Enhanced `CreateCustomColumn`** with three-tier lookup:

    1. `DataTypeSpecifierAttribute` (marker types for generic properties like string, bool)```csharp

    2. `PropertyType` exact match (distinct types like `NormalizationMethod`, `SampleType`)AddForbiddenUIInspection(@"*.cs", @"namespace pwiz.Skyline.Model",

    3. `PropertyType` base class match (for derived types like `ListItem<T>`)    @"Skyline model code must not depend on UI code", 5);

    4. Falls back to base implementation```



**Model cleanup:****Progress: 81% reduction** (from 26 to 5 Model violations)

- Removed all `[DataGridViewColumnType]` attributes from Model properties:

    - `AuditLogRow.cs`, `AuditLogDetailRow.cs`## Goal

    - `Peptide.cs` (StandardType, NormalizationMethod, SurrogateExternalStandard)

    - `Replicate.cs` (SampleType)Remove all UI dependencies from Model code by:

    - `AnnotationPropertyDescriptor.cs` (true_false and value_list annotations)1. Refactoring to use events/callbacks instead of direct UI references

    - `ListLookupPropertyDescriptor.cs`2. Moving UI-specific logic out of Model classes

- `DataGridViewColumnTypeAttribute` now obsolete (only contains comment explaining the change)3. Using interfaces or abstractions where necessary

- Removed instantiation logic from `AbstractViewContext.CreateCustomColumn()`4. Ensuring Model classes can be tested without UI dependencies



**Impact:**  Once complete, change the tolerance count from `28` to `0` in `pwiz_tools/Skyline/Test/CodeInspectionTest.cs`.

- **Eliminated 8 violations!** (13 → 5)

- Model now has **zero compile-time dependencies** on UI column types## Benefits

- UI controls which column types are used via mapping dictionary

- Backward compatible through fallback mechanism- **Better architecture**: Clean separation between business logic and presentation

- All files moved with `git mv` to preserve history- **Easier testing**: Model classes can be unit tested without UI framework

- **Improved maintainability**: Changes to UI don't require Model changes

---- **Cross-platform ready**: Model code works in headless/server scenarios



### 2. ReportOrViewSpecList to Properties (2025-11-03)## Known Violations

**Commit:** `2feed58d12` - "Moved ReportOrViewSpecList to its own file in Skyline\Properties like the other SettingsList<T> derived classes"

As of 2025-11-03, there are only **6 violations remaining** (5 Model + 1 CLI), down from 27 total violations! 

**Changes:**

- Moved `ReportOrViewSpecList` from `Model/Databinding/ReportOrViewSpec.cs` to `Properties/ReportOrViewSpecList.cs`**Progress: 78% reduction overall** (from 27 to 6 total violations)

- Follows established pattern for `SettingsList<T>` derived classes (like `GroupComparisonDefList`, `ListDefList`)

- Moved related resources from `Resources.resx` to `PropertiesResources.resx`Run CodeInspectionTest to see the current list of files and line numbers.



**Additional improvements:**To see the current violations, run CodeInspectionTest and look for warnings that start with:

- Enhanced CodeInspectionTest to call `AssortResources.exe` for self-healing resource issues```

- Prevents future resource file problemsWARNING: Found prohibited use of

"using.*(pwiz\.Skyline\.(Alerts|Controls|.*UI)|System\.Windows\.Forms|pwiz\.Common\.GUI)"

**Impact:**  (Skyline model code must not depend on UI code)

- **Eliminated 1 violation** (14 → 13)```



---Additionally, we now also flag fully-qualified references (even without a using directive):



### 3. TreeNode TITLE Text to DocNode (2025-11-03)```

**Commit:** `c4eceaed59` - "move *TreeNode.TITLE text to *DocNode to avoid UI reference in Model.Find.FindResult""^(?!\s*///).*?\b(pwiz\.Skyline\.(Alerts|Controls|.*UI)|System\.Windows\.Forms|pwiz\.Common\.GUI)\."

```

**Changes:**

- Moved `TITLE` constant strings from UI TreeNode classes to Model DocNode classes:## Related

    - `PeptideTreeNode.TITLE` → `PeptideDocNode.TITLE`

    - `TransitionGroupTreeNode.TITLE` → `TransitionGroupDocNode.TITLE`- `CommandLine.cs` currently has 1 violation remaining (tolerance still 2 for now)

    - `TransitionTreeNode.TITLE` → `TransitionDocNode.TITLE`- `CommandArgs.cs` inspection (no violations tolerated)

- Moved resources from `SeqNodeResources.resx` to `ModelResources.resx`- See `pwiz_tools/Skyline/Test/CodeInspectionTest.cs` near the `AddForbiddenUIInspection` calls for exact rules

- Updated `Model/Find/FindResult.cs` to use DocNode TITLE constants instead of TreeNode

## Changes completed in this branch (as of 2025-11-03)

**Impact:**  

- **Eliminated 1 violation** in FindResult.cs (15 → 14)### DataGridView column types refactoring (2025-11-03)

- **Moved all UI column types from Model to Controls** (11 files total):

---    - `AuditLogColumn.cs`, `TextImageColumn.cs` → `Controls/Databinding/AuditLog/`

    - `AnnotationValueListDataGridViewColumn.cs` → `Controls/Databinding/`

### 4. SpectrumDisplayInfo to Model (2025-11-03)    - `SurrogateStandardDataGridViewColumn.cs` → `Controls/Databinding/`

**Commit:** `106ebc54d1` - "move SpectrumDisplayInfo into Model to avoid KoinaModel referring to it in Controls.Graphs"    - `NormalizationMethodDataGridViewColumn.cs` → `Controls/Databinding/`

    - `ListLookupDataGridViewColumn.cs` → `Controls/Databinding/`

**Changes:**    - `SampleTypeDataGridViewColumn.cs` → `Controls/Databinding/`

- Moved `SpectrumDisplayInfo` class from `Controls.Graphs.GraphSpectrum` to new file `Model/Lib/SpectrumDisplayInfo.cs`    - `StandardTypeDataGridViewColumn.cs` → `Controls/Databinding/`

- Extracted from GraphSpectrum.cs (removed 70 lines) into standalone Model file (93 lines added)    - `BoundComboBoxColumn.cs` (base class) → `Controls/Databinding/`

- Fixed `Model/Koina/Models/KoinaModel.cs` reference from Controls.Graphs

- **Created UI-side mapping architecture:**

**Rationale:**      - Added `DataTypeSpecifierAttribute` in Common library

- `SpectrumDisplayInfo` is data-centric, not UI-specific    - Implemented `PropertyTypeToColumnTypeMap` dictionary in `SkylineViewContext`

- Koina models in Model layer needed to reference it    - Created marker types for generic properties: `SurrogateStandardName`, `TrueFalseAnnotation`, `ValueListAnnotation`

- Moving to Model eliminates reverse dependency    - Enhanced `CreateCustomColumn` with three-tier lookup: marker type → property type → base type match



**Impact:**  - **Removed backward dependencies:**

- **Eliminated 1 violation** in KoinaModel.cs (16 → 15)    - Removed all `[DataGridViewColumnType]` attributes from Model properties in:

        - `AuditLogRow.cs`, `AuditLogDetailRow.cs`

---        - `Peptide.cs` (StandardType, NormalizationMethod, SurrogateExternalStandard)

        - `Replicate.cs` (SampleType)

### 5. SpectrumFilterAutoComplete to EditUI (2025-11-01)        - `AnnotationPropertyDescriptor.cs` (true_false and value_list annotations)

**Commit:** `0d5819bcea` - "move SpectrumFilterAutoComplete (related to Windows.Forms.AutoCompleteStringCollection) to EditUI"        - `ListLookupPropertyDescriptor.cs`

    - Deleted `DataGridViewColumnTypeAttribute.cs` entirely (obsolete)

**Changes:**    - Removed instantiation logic from `AbstractViewContext.cs`

- Moved `SpectrumFilterAutoComplete` from Model to EditUI

- Removed unused/incomplete `Model/Results/Spectra/SpectrumClassFilters.cs` (116 lines deleted)- **Result:** Reduced Model UI violations from 26 to **5** (21 files cleaned up - 81% reduction!)



**Rationale:**  ### SeqNode dependencies resolved (prior to 2025-11-03)

- `SpectrumFilterAutoComplete` uses `Windows.Forms.AutoCompleteStringCollection`- **Moved code from UI tree nodes to Model DocNodes:**

- Clearly UI-specific functionality    - `PeptideTreeNode` → `PeptideDocNode`

- SpectrumClassFilters was dead code (may have evolved into SpectrumClassFilter.cs)    - `TransitionGroupTreeNode` → `TransitionGroupDocNode` 

    - `TransitionTreeNode` → `TransitionDocNode`

**Impact:**  - **Moved resources:** `SeqNodeResources.resx` → `ModelResources.resx`

- **Eliminated 1 violation** (17 → 16)- **Result:** Eliminated ~5 SeqNode violations



---### Testing and inspections (2025-10-29)

- Added a second inspection to catch fully-qualified UI namespaces (no using directive present) in the same files targeted by our cues.

### 6. ListDefList to Properties (2025-11-01)- Reduced Model UI tolerance from 35 to 28 after refactors removed 7 incidents.

**Commit:** `6ca3860564` - "move ListDefList a SettingsList to Skyline\Properties to remove UI references from Model"- Kept CLI tolerance at 2; current CLI violations reduced to 1 (safe to drop to 1 or 0 after final cleanup).



**Changes:**Model refactors (high level)

- Moved `ListDefList` from `Model/Lists/ListDef.cs` to `Properties/ListDefList.cs`- New file: `pwiz_tools/Skyline/Model/GroupComparison/FoldChangeRows.cs` (data/logic extraction toward decoupling UI from Model).

- Extracted SettingsList-derived class (70 lines) to separate file- Touched Model files for decoupling and cleanup:

- Moved related resources to `PropertiesResources.resx`    - `Model/AreaCVRefinementData.cs`

- Updated MEMORY.md with notes on SettingsList pattern    - `Model/Databinding/Entities/Peptide.cs`

    - `Model/Databinding/Entities/Protein.cs`

**Pattern:**      - `Model/DocNode.cs`

All `SettingsList<T>` derived classes now live in `Properties/`:    - `Model/Find/BookmarkEnumerator.cs`

- `GroupComparisonDefList.cs`    - `Model/Find/FindPredicate.cs`

- `ListDefList.cs`    - `Model/GroupComparisonRefinementData.cs`

- `ReportOrViewSpecList.cs`    - `Model/PeptideGroupDocNode.cs`

    - `Model/Proteome/AssociateProteinsResults.cs` (removed WinForms usage)

**Impact:**      - `Model/Results/SpectrumFilterPair.cs`

- **Eliminated 1 violation** (18 → 17)

UI layer adjustments (to support the separation)

---- Updated several files under `pwiz_tools/Skyline/Controls/*` and `Controls/GroupComparison/*` to keep UI logic on the UI side while referencing new/cleaned Model logic.

- Removed `ShowDialog` from `ILongWaitBroker`; retained a targeted legacy cast usage in `ViewLibraryPepMatching` where a dialog must be shown during a long wait.

### 7. GroupComparison UI Class Removal (2025-11-01) - `PeptideDocNode` no longer depends on `Controls.SeqNode` (moved display text responsibility into Model).

**Commit:** `6b9a8847fb` - "remove use of UI classes from GroupComparison code"

Other changes on branch

**Changes:**- `pwiz_tools/Skyline/Test/*`: updates aligned with inspection changes and Model refactors.

- Removed UI dependencies from `Model/GroupComparison/GroupComparisonModel.cs`- Some changes under `pwiz/data/vendor_readers/Thermo/*` are present; they appear unrelated to the Model UI-dependency effort and can be tracked separately in the PR description.

- Moved `GroupComparisonDefList` from Model to Properties

- Moved UI-specific logic to `Controls/GroupComparison/EditGroupComparisonDlg.cs`## How to verify current state

1) Open `pwiz_tools/Skyline/Test/CodeInspectionTest.cs` and confirm:

**Impact:**      - The fully-qualified namespace inspection exists (the second AddTextInspection inside `AddForbiddenUIInspection`).

- **Eliminated 2 violations** (20 → 18)    - Model tolerance: `5` (confirmed as of 2025-11-03)

    - CLI tolerance: `1` (confirmed as of 2025-11-03)

---2) Run CodeInspectionTest to enumerate the 5 remaining Model offenders:

    - `Model/Databinding/ReportSharing.cs` - `new DocumentGridViewContext()`

### 8. DocNode to TreeNode Dependency Flip (2025-10-31)    - `Model/Databinding/DocumentViewTransformer.cs` - `SkylineViewContext.GetReplicateSublist()`

**Commit:** `9affe1d455` - "flip DocNode to TreeNode dependencies on display strings, since they became critical for audit logging"    - `Model/Databinding/ResultsGridViewContext.cs` - derives from `SkylineViewContext`

    - `Model/Tools/ToolDescription.cs` - `new DocumentGridViewContext()`

**Major architectural change:**      - (Plus 1 more to be identified)

This was a significant refactoring that inverted the dependency relationship between Model's DocNode and UI's TreeNode.3) And the 1 CLI violation in `CommandLine.cs`

4) Verify DataGridView column types are in `Controls/Databinding/` and `Controls/Databinding/AuditLog/`.

**Code moved from TreeNode to DocNode:**5) Celebrate 78% reduction! 🎉 (27 → 6 violations)

- `PeptideTreeNode` → `PeptideDocNode` (16 lines of display text logic added to DocNode)

- `TransitionGroupTreeNode` → `TransitionGroupDocNode` (93 lines added to DocNode)## Remaining violations (5 Model + 1 CLI = 6 total, as of 2025-11-03)

- `TransitionTreeNode` → `TransitionDocNode` (91 lines added to DocNode)

**🎯 Nearly complete! Only 6 violations left (down from 27)**

**Resources moved:**

- Moved related resources from `SeqNodeResources.resx` to `ModelResources.resx`PR: https://github.com/ProteoWizard/pwiz/pull/3663



**Architectural impact:**### Actual Remaining Issues: SkylineViewContext/DocumentGridViewContext references (5 files)

- **Before:** DocNode called TreeNode methods for display strings (Model → UI dependency)

- **After:** TreeNode calls DocNode methods (UI → Model, correct direction!)All remaining Model violations are related to Model classes referencing UI databinding view contexts (`SkylineViewContext`, `DocumentGridViewContext`):

- **Critical for audit logging:** Display strings needed in Model for audit log without requiring UI

**Files:**

**Impact:**  1. **`Model/Databinding/ReportSharing.cs`** - Creates `new DocumentGridViewContext(dataSchema)` for report conversion

- **Eliminated 4 violations** - major refactor! (24 → 20)2. **`Model/Databinding/DocumentViewTransformer.cs`** - Calls static methods `SkylineViewContext.GetReplicateSublist()`

3. **`Model/Databinding/ResultsGridViewContext.cs`** - Class derives from `SkylineViewContext` (should be in Controls)

---4. **`Model/Tools/ToolDescription.cs`** - Creates `new DocumentGridViewContext(dataSchema)` for tool integration

5. **One more file** - (need to verify with CodeInspectionTest output)

### 9. PeptideTreeNode Dependency Removal (2025-10-29)

**Commit:** `9ed4a643a6` - "remove PeptideTreeNode dependency from PeptideDocNode"**Strategy:** 

- Move `ResultsGridViewContext` from Model to Controls (it's a UI view context)

**Changes:**- Extract static helper methods from `SkylineViewContext` to a Model-level helper class

- Removed references from `Model/PeptideDocNode.cs` to `Controls.SeqNode.PeptideTreeNode`- Refactor `ReportSharing` and `ToolDescription` to use Model-level abstractions instead of directly instantiating UI view contexts

- Updated `PeptideTreeNode` to call DocNode methods instead of vice versa

### Category 6: CommandLine UI dependencies (1 file)

**Impact:**  - `CommandLine.cs` - using pwiz.Skyline.Controls.Databinding;

- **Eliminated 1 violation** (25 → 24)

**Strategy:** Remove databinding control reference in `CommandLine.cs`.

---

## Next steps (prioritized)

### 10. ILongWaitBroker.ShowDialog Removal (2025-10-29)1. ✅ **COMPLETED (2025-11-03):** DataGridView column types - moved all 11 UI column classes from Model to Controls; implemented UI-side mapping pattern. **Result: 21 violations eliminated!**

**Commit:** `e24fd8fa38` - "Remove UI method ShowDialog() from ILongWaitBroker since it was only used in one location"2. **View Context refactoring (5 files) - THE FINAL FRONTIER:**

   - Move `ResultsGridViewContext` from Model/Databinding to Controls/Databinding (it's a UI view context class)

**Changes:**   - Extract static methods from `SkylineViewContext` (like `GetReplicateSublist()`) to a Model-level helper class

- Removed `ShowDialog()` method from `ILongWaitBroker` interface   - Refactor `ReportSharing.cs` and `ToolDescription.cs` to avoid directly instantiating UI view contexts

- Changed `ViewLibraryPepMatching.cs` to cast to `LongWaitDlg` for that one specific usage   - Consider introducing an interface or abstract base in Model that UI view contexts implement

- Removed usage from:3. **CLI cleanup (1 file):** Remove databinding dependency from CommandLine.cs.

    - `CommandLine.cs`4. **Victory lap:** Set Model tolerance to 0, CLI tolerance to 0. ✅ Complete separation achieved!

    - `Model/Proteome/AssociateProteinsResults.cs`

    - `Controls/Databinding/ReplicatePivotColumns.cs`## Triage notes

- ✅ **COMPLETED:** DataGridView column types - created UI-side mapping pattern with marker types for properties with generic types (string, bool), PropertyType mapping for distinct types (NormalizationMethod, SampleType, StandardType, ListItem), and base class matching for derived types. **Eliminated 21 violations!**

**Rationale:**- ✅ **COMPLETED:** SeqNode dependencies - resolved by moving code from SeqNode to DocNode classes (done before this commit)

- `ShowDialog()` was only used in one location (ViewLibraryPepMatching.cs)- **Last remaining Model violations (5 files):** ALL are SkylineViewContext/DocumentGridViewContext references in databinding code

- `ILongWaitBroker` is now entirely UI-free  - `ResultsGridViewContext`: Simply move from Model to Controls (it's a UI class)

- Should probably be moved out of UtilUI.cs in the future  - `DocumentViewTransformer`: Extract static helper method to Model

- Important because CommandProgressMonitor implements ILongWaitBroker  - `ReportSharing` & `ToolDescription`: Refactor to use Model abstractions

- **Almost there!** Only 6 total violations remain (5 Model + 1 CLI) from original 27.

**Impact:**  - **Final step:** Resolve view context dependencies → set Model tolerance to 0; resolve CLI → set CLI tolerance to 0. ✅ Mission accomplished!

- **Eliminated 3 violations** (28 → 25)

## Detailed extraction plan: RefinementSettings RT regression dependency

---

**Current violation:**

### 11. CodeInspection Improvements & FoldChangeRows Extraction (2025-10-28)```csharp

**Commit:** `206e550c02` - "Reduce number of warnings from CodeInspectionTest - Added new test to CodeInspection to recognize fully qualified use of UI classes"// Model/RefinementSettings.cs line 261

var outliers = RTLinearRegressionGraphPane.CalcOutliers(document,

**CodeInspectionTest enhancements:**    RTRegressionThreshold.Value, RTRegressionPrecision, UseBestResult);

- Added detection for **fully-qualified UI namespace references** (not just `using` statements)```

- New pattern catches references like `pwiz.Skyline.Controls.SomeClass` even without `using` directive

- Identified at least 1 previously unnoticed fully-qualified violation**Goal:** Move RT regression calculation logic from `Controls.Graphs.RTLinearRegressionGraphPane` to new Model file.

- This is critical for preventing sneaky workarounds

**New file:** `Model/RetentionTimes/RetentionTimeRegressionGraphData.cs`

**Code refactoring:**

- Created new `Model/GroupComparison/FoldChangeRows.cs` (128 lines)**Classes to extract and rename:**

- Extracted data/logic from `Controls/GroupComparison/FoldChangeBindingSource.cs` (removed 100 lines)

- Moved code from `Controls/SeqNode/SrmTreeNode.cs` to `Model/DocNode.cs` (47 lines)1. **RegressionSettings** → **RetentionTimeRegressionParameters**

   - Pure data/configuration holder

**Cleaned up various Model files:**   - Remove `Settings.Default.RTScoreCalculatorList` dependency (pass explicitly)

- `Model/AreaCVRefinementData.cs`   - Keep as internal nested class or make public if needed elsewhere

- `Model/Databinding/Entities/Peptide.cs`

- `Model/Databinding/Entities/Protein.cs`2. **GraphData** → **RetentionTimeRegressionGraphData** (primary class)

- `Model/Find/BookmarkEnumerator.cs`   - Main calculation engine for RT regression outliers

- `Model/Find/FindPredicate.cs`   - Constructor dependencies to externalize:

- `Model/GroupComparisonRefinementData.cs`     - `RTGraphController.PointsType` → pass as parameter

- `Model/PeptideGroupDocNode.cs`     - `RTGraphController.RegressionMethod` → pass as parameter

- `Model/Proteome/AssociateProteinsResults.cs`   - Already clean of Settings.Default (good!)

   - Keep PointInfo as nested class (UI-agnostic data)

**Impact:**  

- Reduced from **35 to 28 violations** through cleanup and detection improvements3. **Static CalcOutliers method** → new public static method on **RetentionTimeRegressionGraphData**

- Note: 7 violations eliminated + new detection found some hidden ones, netting to -7   - Signature change:

     ```csharp

---     // OLD (UI):

     public static PeptideDocNode[] CalcOutliers(SrmDocument document, 

### 12. Branch Start (2025-10-26)         double threshold, int? precision, bool bestResult)

**Commit:** `62eec865b4` - "Start no-ui-classes-in-model work - move TODO to active"     

     // NEW (Model):

- Moved TODO from backlog to active     public static PeptideDocNode[] CalcOutliers(SrmDocument document,

- Established baseline: 35 Model violations         double threshold, int? precision, bool bestResult,

         PointsTypeRT pointsType, RegressionMethodRT regressionMethod,

---         RtCalculatorOption calculatorOption, bool refinePeptides)

     ```

## Benefits Achieved   - Caller (RefinementSettings) will pass Settings.Default values explicitly

   - Caller (RTLinearRegressionGraphPane.CalcOutliers wrapper) will pass controller static properties

### Architectural Improvements

- **Clean separation:** Model has zero compile-time dependencies on UI column types**Dependencies already in Model namespace:**

- **Correct dependency direction:** UI now depends on Model, not vice versa- ✅ `PointsTypeRT` enum (in RTGraphController.cs but will move to Model)

- **Testability:** Model classes can be unit tested without UI framework- ✅ `RegressionMethodRT` enum (already in Model/RetentionTimes/DocumentRetentionTimes.cs)

- **Cross-platform ready:** Model code works in headless/server scenarios- ✅ `RtCalculatorOption` (Model namespace)

- ✅ `ProductionMonitor`, `CancellationToken` (Common/SystemUtil)

### Maintainability

- **Reduced coupling:** Changes to UI don't require Model changes**Dependencies to handle:**

- **Clear boundaries:** Established patterns for SettingsList, DataGridView columns, display strings- `PointsTypeRT` enum: Currently in `Controls/Graphs/RTGraphController.cs`

- **Better organization:** Related code grouped by layer (Model/Properties/Controls)    - **Decision: Move to Model/RetentionTimes** alongside `RegressionMethodRT` (UI-agnostic concept)

    - **Action:**

### Code Quality        1) Define `public enum PointsTypeRT { targets, targets_fdr, standards, decoys }` in `Model/RetentionTimes/DocumentRetentionTimes.cs` (or a new `PointsTypeRT.cs` in the same namespace)

- **Detection improvements:** CodeInspectionTest now catches fully-qualified UI references        2) Update all references to use `pwiz.Skyline.Model.RetentionTimes.PointsTypeRT`

- **Pattern establishment:** UI-side mapping pattern for extensibility        3) Remove the enum from `RTGraphController.cs`

- **Resource cleanup:** Self-healing resource file management in tests        4) Ensure `RTGraphController.PointsType` property continues to work by returning the moved enum type

    - **Rationale:** Allows Model code (extracted regression logic) to accept `PointsTypeRT` without referencing UI

---

**UI wrapper strategy:**

## Remaining Violations (5 Model + 1 CLI = 6 total)After extraction, `RTLinearRegressionGraphPane.CalcOutliers` becomes a thin wrapper:

```csharp

**🎯 Nearly complete! Only 6 violations left (down from 35 - 83% reduction overall)**public static PeptideDocNode[] CalcOutliers(SrmDocument document, 

    double threshold, int? precision, bool bestResult)

### Model Violations: SkylineViewContext/DocumentGridViewContext references (5 files){

    return RetentionTimeRegressionGraphData.CalcOutliers(document,

All remaining Model violations are related to Model classes referencing UI databinding view contexts (`SkylineViewContext`, `DocumentGridViewContext`):        threshold, precision, bestResult,

        RTGraphController.PointsType,

**Known files with violations:**        RTGraphController.RegressionMethod,

1. **`Model/Databinding/ReportSharing.cs`** - Creates `new DocumentGridViewContext(dataSchema)` for report conversion        Settings.Default.RtCalculatorOption,

2. **`Model/Databinding/DocumentViewTransformer.cs`** - Calls static methods `SkylineViewContext.GetReplicateSublist()`        Settings.Default.RTRefinePeptides);

3. **`Model/Databinding/ResultsGridViewContext.cs`** - Class derives from `SkylineViewContext` (should be in Controls)}

4. **`Model/Tools/ToolDescription.cs`** - Creates `new DocumentGridViewContext(dataSchema)` for tool integration```

5. **One more file** - (need to run CodeInspectionTest to identify)

**Model caller update:**

**Refactoring strategy:**`RefinementSettings.cs` changes to:

- Move `ResultsGridViewContext` from Model/Databinding to Controls/Databinding (it's a UI view context class)```csharp

- Extract static helper methods from `SkylineViewContext` (like `GetReplicateSublist()`) to a Model-level helper classvar outliers = RetentionTimeRegressionGraphData.CalcOutliers(document,

- Refactor `ReportSharing.cs` and `ToolDescription.cs` to avoid directly instantiating UI view contexts    RTRegressionThreshold.Value, RTRegressionPrecision, UseBestResult,

- Consider introducing an interface or abstract base in Model that UI view contexts implement    PointsTypeRT.targets,  // Explicit

    RegressionMethodRT.linear,  // Or passed as parameter

### CLI Violation (1 file)    Settings.Default.RtCalculatorOption,  // Or passed

- **`CommandLine.cs`** - `using pwiz.Skyline.Controls.Databinding;`    Settings.Default.RTRefinePeptides);  // Or passed

```

**Strategy:** Remove or refactor databinding control reference in CommandLine.cs.

**Parameterization in GraphData (now RetentionTimeRegressionGraphData):**

---- Replace usage of `RTGraphController.PointsType` with constructor parameter `pointsType`

- Replace usage of `RTGraphController.RegressionMethod` with constructor parameter `regressionMethod`

## Next Steps (Prioritized)

**Risk assessment:**

### Step 1: Identify the 5th Model violation- **Medium complexity:** ~600 lines of code to move

- Run CodeInspectionTest and capture full output- **High test surface:** RT regression is heavily tested

- Update this TODO with the 5th file/line- **Settings dependencies:** 4-5 Settings.Default calls to externalize

- **Enum location:** PointsTypeRT needs decision on placement

### Step 2: View Context refactoring (5 files) - THE FINAL FRONTIER

1. Move `ResultsGridViewContext` from Model/Databinding to Controls/Databinding**Next steps:**

2. Extract static methods from `SkylineViewContext` to Model helper class1. Decide PointsTypeRT placement (move to Model or reference Controls?)

3. Refactor `ReportSharing.cs` and `ToolDescription.cs` to use Model abstractions2. Create RetentionTimeRegressionGraphData.cs with extracted classes

4. Consider interface/abstract base pattern for view contexts3. Update RTLinearRegressionGraphPane to delegate

4. Update RefinementSettings caller

### Step 3: CLI cleanup (1 file)5. Build, run CodeInspectionTest, verify Model violations drop by 1

- Remove databinding dependency from `CommandLine.cs`6. Run full RT regression tests



### Step 4: Victory lap! 🎉## Notes for PR description (when ready)

- Set Model tolerance to `0` in CodeInspectionTest.cs- Tests: added fully-qualified UI namespace inspection; reduced Model UI tolerance from 35 to 28.

- Set CLI tolerance to `0` in CodeInspectionTest.cs- Model: extracted `FoldChangeRows.cs`; refactored multiple Model files to reduce UI coupling.

- ✅ Complete Model/UI separation achieved!- UI: adjusted GroupComparison and related UI to reference Model-only logic.

- Update TODO to completed/, write comprehensive commit message- Out of scope: Thermo vendor reader changes also present on this branch; list separately in PR summary.

- Merge PR #3663

---

## How to Verify Current State

1. **Check CodeInspectionTest.cs:**
   ```csharp
   // Confirm Model tolerance: 5
   AddForbiddenUIInspection(@"*.cs", @"namespace pwiz.Skyline.Model",
       @"Skyline model code must not depend on UI code", 5);
   
   // Confirm CLI tolerance: 1 (or 2 depending on current state)
   ```

2. **Run CodeInspectionTest** to see the 5 remaining Model violations and 1 CLI violation

3. **Verify file locations:**
   - All DataGridView column types in `Controls/Databinding/` and `Controls/Databinding/AuditLog/`
   - All SettingsList classes in `Properties/`
   - SpectrumDisplayInfo in `Model/Lib/`
   - Display string logic in DocNode classes

4. **Celebrate 83% reduction!** 🎉 (35 → 6 violations)

---

## Historical Context

This work builds on the established architecture patterns in Skyline:
- **SettingsList pattern:** Application settings in Properties layer
- **DocNode/TreeNode separation:** Model data vs. UI presentation
- **ViewContext abstraction:** Data binding layer between Model and UI

The refactoring maintains backward compatibility while establishing cleaner architectural boundaries for future development.

---

## Related Files & Tests

**Key test files:**
- `pwiz_tools/Skyline/Test/CodeInspectionTest.cs` - Violation detection
- Tests updated to use new DocNode display methods

**Documentation:**
- `MEMORY.md` - SettingsList pattern documentation
- `WORKFLOW.md` - Git workflow and TODO lifecycle

**PR:**
- https://github.com/ProteoWizard/pwiz/pull/3663
