# TODO: Remove UI class dependencies from Skyline.Model

**Status:** Backlog
**Priority:** Medium
**Estimated Effort:** Medium to Large (37 violations to fix)

## Problem

The `pwiz.Skyline.Model` namespace currently has 37 instances where it depends on UI code (classes from `pwiz.Skyline.Alerts`, `pwiz.Skyline.Controls`, `pwiz.Skyline.*UI`, `System.Windows.Forms`, or `pwiz.Common.GUI`).

This violates the Model-View separation principle and creates unnecessary coupling between business logic and presentation layers.

## Current State

The CodeInspection test detects these violations but tolerates 37 existing instances:

```csharp
AddForbiddenUIInspection(@"*.cs", @"namespace pwiz.Skyline.Model",
    @"Skyline model code must not depend on UI code", 37);
```

These violations were allowed before the inspection was added, and new violations are prevented, but the existing ones need cleanup.

## Goal

Remove all UI dependencies from Model code by:
1. Refactoring to use events/callbacks instead of direct UI references
2. Moving UI-specific logic out of Model classes
3. Using interfaces or abstractions where necessary
4. Ensuring Model classes can be tested without UI dependencies

Once complete, change the tolerance count from `37` to `0` in CodeInspectionTest.cs line 187.

## Benefits

- **Better architecture**: Clean separation between business logic and presentation
- **Easier testing**: Model classes can be unit tested without UI framework
- **Improved maintainability**: Changes to UI don't require Model changes
- **Cross-platform ready**: Model code works in headless/server scenarios

## Known Violations

As of 2025-10-19, there are 37 violations detected by the CodeInspection test. Run the test to see the current list of files and line numbers.

To see the current violations, run CodeInspectionTest and look for warnings that start with:
```
WARNING: Found prohibited use of
"using.*(pwiz\.Skyline\.(Alerts|Controls|.*UI)|System\.Windows\.Forms|pwiz\.Common\.GUI)"
(Skyline model code must not depend on UI code)
```

## Related

- CommandLine.cs has 2 tolerated violations
- CommandArgs.cs inspection (no violations currently)
- See CodeInspectionTest.cs lines 187-190 for all UI dependency inspections
