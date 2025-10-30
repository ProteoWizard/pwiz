# TODO: Remove UI class dependencies from Skyline.Model

**Status:** Active
**Priority:** Medium
**Estimated Effort:** Medium to Large (27 remaining violations as of 2025-10-29: 26 Model, 1 CLI)
**Branch:** Skyline/work/20251026_no_ui_classes_in_model
**Updated:** 2025-10-29

## Problem

The `pwiz.Skyline.Model` namespace currently has 26 remaining instances where it depends on UI code (namespaces: `pwiz.Skyline.Alerts`, `pwiz.Skyline.Controls`, `pwiz.Skyline.*UI`, `System.Windows.Forms`, `pwiz.Common.GUI`). One CLI violation also remains.

This violates the Model-View separation principle and creates unnecessary coupling between business logic and presentation layers.

## Current State

The CodeInspection test detects these violations but tolerates 28 existing instances (Model). Current count: 26 (Model), 1 (CLI):

```csharp
AddForbiddenUIInspection(@"*.cs", @"namespace pwiz.Skyline.Model",
    @"Skyline model code must not depend on UI code", 28);
```

These violations were allowed before the inspection was added, and new violations are prevented, but the existing ones need cleanup.

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

As of 2025-10-28, there are 28 violations detected by the CodeInspection test. Run the test to see the current list of files and line numbers.

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

## Changes completed in this branch (as of 2025-10-29)

Testing and inspections
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
    - The Model tolerance is `28` (current Model count should be `26`).
    - CLI inspections: `CommandLine.cs` tolerated `2` (current CLI count should be `1`); `CommandArgs.cs` no tolerated count.
2) Run CodeInspectionTest to enumerate the 26 remaining Model offenders and the 1 CLI incident.

## Remaining violations (26 Model + 1 CLI = 27 total, as of 2025-10-29)

PR: https://github.com/ProteoWizard/pwiz/pull/3663

### Category 1: SeqNode dependencies (5 files)
Core document node classes depend on `pwiz.Skyline.Controls.SeqNode` for tree UI logic.

**Files:**
- `Model/SrmDocument.cs:60` - using pwiz.Skyline.Controls.SeqNode;
- `Model/TransitionDocNode.cs:26` - using pwiz.Skyline.Controls.SeqNode;
- `Model/TransitionGroupDocNode.cs:22` - using pwiz.Skyline.Controls.SeqNode;
- `Model/Databinding/Entities/Precursor.cs:23` - using pwiz.Skyline.Controls.SeqNode;
- `Model/Find/FindResult.cs:23` - using pwiz.Skyline.Controls.SeqNode;

**Strategy:** Move SeqNode UI logic (icons, colors, display properties) out of Model; introduce ISeqNodeProvider or similar abstraction if Model needs type info.

### Category 2: System.Windows.Forms dependencies (10 files)
Model code uses WinForms types (DataGridViewColumn, IWin32Window, etc.) which couples it to desktop UI.

**Files:**
- `Model/AuditLog/Databinding/TextImageColumn.cs:22` - using System.Windows.Forms;
- `Model/Databinding/AnnotationPropertyDescriptor.cs:23` - using System.Windows.Forms;
- `Model/Databinding/BoundComboBoxColumn.cs:20` - using System.Windows.Forms;
- `Model/Databinding/ListLookupDataGridViewColumn.cs:23` - using System.Windows.Forms;
- `Model/Databinding/ReportOrViewSpec.cs:24` - using System.Windows.Forms;
- `Model/Databinding/ResultsGridViewContext.cs:22` - using System.Windows.Forms;
- `Model/Databinding/SampleTypeDataGridViewColumn.cs:3` - using System.Windows.Forms;
- `Model/Databinding/StandardTypeDataGridViewColumn.cs:21` - using System.Windows.Forms;
- `Model/GroupComparison/GroupComparisonDefList.cs:20` - using System.Windows.Forms;
- `Model/GroupComparison/GroupComparisonModel.cs:23` - using System.Windows.Forms;
- `Model/Lists/ListDef.cs:23` - using System.Windows.Forms;
- `Model/Results/Spectra/SpectrumFilterAutoComplete.cs:23` - using System.Windows.Forms;

**Strategy:** Replace DataGridViewColumn subclasses with Model-only column descriptors; move IWin32Window and AutoComplete UI concerns to UI layer or use interfaces.

### Category 3: Databinding Control dependencies (5 files)
Model references `pwiz.Skyline.Controls.Databinding` which is UI-specific.

**Files:**
- `Model/Databinding/DocumentViewTransformer.cs:6` - using pwiz.Skyline.Controls.Databinding;
- `Model/Databinding/ReportSharing.cs:27` - using pwiz.Skyline.Controls.Databinding;
- `Model/Databinding/ResultsGridViewContext.cs:25` - using pwiz.Skyline.Controls.Databinding;
- `Model/Tools/ToolDescription.cs:32` - using pwiz.Skyline.Controls.Databinding;

**Strategy:** Extract interfaces for view/databinding concerns; Model should define data shapes, UI should handle display.

### Category 4: GroupComparison UI dependencies (1 file)
- `Model/GroupComparison/GroupComparisonDefList.cs:21` - using pwiz.Skyline.Controls.GroupComparison;

**Strategy:** Extract data/logic into Model (already started with FoldChangeRows.cs); move UI-specific display into Controls layer.

### Category 5: Other Control dependencies (3 files)
- `Model/AuditLog/Databinding/AuditLogColumn.cs:22` - using pwiz.Skyline.Controls.AuditLog;
- `Model/Lists/ListDef.cs:28` - using pwiz.Skyline.Controls.Lists;
- `Model/RefinementSettings.cs:28` - using pwiz.Skyline.Controls.Graphs;
- `Model/Koina/Models/KoinaModel.cs:33` - using pwiz.Skyline.Controls.Graphs;

**Strategy:** Move graph-specific display/rendering to UI; use events or interfaces for AuditLog/Lists UI interaction from Model.

### Category 6: CommandLine UI dependencies (1 file, separate inspection)
- `CommandLine.cs` - using pwiz.Skyline.Controls.Databinding;

**Strategy:** ILongWaitBroker no longer requires IWin32Window (removed); remove databinding control reference in `CommandLine.cs`.

## Next steps (prioritized)
1. **Quick wins (5-10 files):** Files that only reference WinForms for simple types like DataGridViewColumn - introduce Model-only column metadata classes and move the DataGridView usage to UI layer.
2. **SeqNode decoupling (6 files):** Introduce ISeqNodeMetadata or similar; extract icon/color/display logic from Model to UI.
3. **Databinding refactor (5 files):** Define Model interfaces for databinding concerns; implement in UI layer.
4. **GroupComparison cleanup (1 file):** Continue extraction pattern started with FoldChangeRows.cs.
5. **CLI cleanup (2 files):** Abstract ILongWaitBroker for headless; remove databinding dependency.
6. **Verify and reduce tolerance:** After each category, re-run CodeInspectionTest and reduce tolerated count accordingly.

## Triage notes
- **Easiest first:** DataGridViewColumn subclasses - create Model descriptors, use them in UI.
- **Medium effort:** SeqNode - requires interface + UI implementation + Model update.
- **Larger effort:** Databinding - cross-cutting concern, may need architectural discussion.
- **Final step:** Once all 26 Model violations resolved, set tolerance to 0; then tackle CLI separately.

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
