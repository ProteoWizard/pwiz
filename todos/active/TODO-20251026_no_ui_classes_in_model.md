# TODO: Remove UI class dependencies from Skyline.Model

**Status:** Active
**Priority:** Medium
**Estimated Effort:** Medium to Large (28 remaining violations as of 2025-10-28)
**Branch:** Skyline/work/20251026_no_ui_classes_in_model
**Updated:** 2025-10-28

## Problem

The `pwiz.Skyline.Model` namespace currently has 28 remaining instances where it depends on UI code (namespaces: `pwiz.Skyline.Alerts`, `pwiz.Skyline.Controls`, `pwiz.Skyline.*UI`, `System.Windows.Forms`, `pwiz.Common.GUI`).

This violates the Model-View separation principle and creates unnecessary coupling between business logic and presentation layers.

## Current State

The CodeInspection test detects these violations but tolerates 28 existing instances:

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

- `CommandLine.cs` has 2 tolerated violations (unchanged in this branch)
- `CommandArgs.cs` inspection (no violations tolerated)
- See `pwiz_tools/Skyline/Test/CodeInspectionTest.cs` near the `AddForbiddenUIInspection` calls for exact rules

## Changes completed in this branch (as of 2025-10-28)

Testing and inspections
- Added a second inspection to catch fully-qualified UI namespaces (no using directive present) in the same files targeted by our cues.
- Reduced Model UI tolerance from 35 to 28 after refactors removed 7 incidents.
- Kept CLI tolerance at 2 pending verification and cleanup in a subsequent pass.

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
    - `Model/Proteome/AssociateProteinsResults.cs`
    - `Model/Results/SpectrumFilterPair.cs`

UI layer adjustments (to support the separation)
- Updated several files under `pwiz_tools/Skyline/Controls/*` and `Controls/GroupComparison/*` to keep UI logic on the UI side while referencing new/cleaned Model logic.

Other changes on branch
- `pwiz_tools/Skyline/Test/*`: updates aligned with inspection changes and Model refactors.
- Some changes under `pwiz/data/vendor_readers/Thermo/*` are present; they appear unrelated to the Model UI-dependency effort and can be tracked separately in the PR description.

## How to verify current state
1) Open `pwiz_tools/Skyline/Test/CodeInspectionTest.cs` and confirm:
     - The fully-qualified namespace inspection exists (the second AddTextInspection inside `AddForbiddenUIInspection`).
     - The Model tolerance is `28`.
     - CLI inspections remain: `CommandLine.cs` tolerated `2`; `CommandArgs.cs` no tolerated count.
2) Run CodeInspectionTest to enumerate the 28 remaining Model offenders and the 2 CLI incidents (if still present).

## Next steps
- Triage the 28 remaining Model offenders and refactor to remove UI references (events/callbacks, interfaces, move code to UI layer as needed).
- Verify the CLI path:
    - If the 2 tolerated incidents in `CommandLine.cs` are gone, set tolerated to 0.
    - Otherwise, create a small follow-up to remove them and then set to 0.
- Once Model is clean, set Model tolerance to 0 and consider removing the tolerance argument entirely.

## Notes for PR description (when ready)
- Tests: added fully-qualified UI namespace inspection; reduced Model UI tolerance from 35 to 28.
- Model: extracted `FoldChangeRows.cs`; refactored multiple Model files to reduce UI coupling.
- UI: adjusted GroupComparison and related UI to reference Model-only logic.
- Out of scope: Thermo vendor reader changes also present on this branch; list separately in PR summary.
