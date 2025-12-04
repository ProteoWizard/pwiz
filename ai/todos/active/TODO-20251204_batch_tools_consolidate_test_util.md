# TODO-20251204_batch_tools_consolidate_test_util.md

## Branch Information
- **Branch**: `Skyline/work/20251204_batch_tools_consolidate_test_util`
- **Created**: 2025-12-04
- **Completed**: (pending)
- **Status**: 🚧 In Progress
- **PR**: (pending)
- **Objective**: Consolidate duplicated test utilities from SkylineBatch and AutoQC into SharedBatchTest

## Summary
**Priority**: Medium
**Complexity**: Medium
**Original Planning Date**: 2025-11-24

## Scope

SkylineBatch and AutoQC test projects currently duplicate test utility code. These projects **explicitly cannot depend on Skyline** and share only:
- **PanoramaClient** (production shared library)
- **CommonUtil** (production shared library)
- **SharedBatch** (production code shared by batch tools)
- **SharedBatchTest** (test infrastructure shared by batch tools)

This TODO focuses on reviewing and normalizing test utilities **within the batch tools ecosystem** using `SharedBatchTest` as the consolidation target.

## Current Duplication

Both `SkylineBatchTest.TestUtils` and `AutoQCTest.TestUtils` exist with potentially overlapping functionality:
- **SkylineBatchTest/TestUtils.cs** (~527 lines): Mock R setup, file paths, config builders, WaitForCondition, etc.
- **AutoQCTest/TestUtils.cs** (~326 lines): Panorama credentials, file paths, Skyline bin resolution, config validation, etc.

Abstract base classes also exist:
- **SkylineBatchTest/AbstractSkylineBatchFunctionalTest** (extends `SharedBatchTest.AbstractBaseFunctionalTest`)
- **SkylineBatchTest/AbstractSkylineBatchUnitTest** (does NOT extend `SharedBatchTest.AbstractUnitTest` – review why)
- AutoQC likely has similar patterns (need audit)

**SharedBatchTest already provides**:
- `AbstractBaseFunctionalTest`, `AbstractUnitTest`
- `AssertEx`, `Helpers`, `ExtensionTestContext`, `TestFilesDir`
- `NormalizedValueCalculatorVerifier`

## Goals

1. **Audit both TestUtils** for duplication (file paths, config builders, wait utilities, assertions).
2. **Consolidate shared utilities** into `SharedBatchTest.TestUtils` (create if needed) or appropriate abstract base classes.
3. **Normalize abstract base classes**:
   - Why doesn't `AbstractSkylineBatchUnitTest` extend `SharedBatchTest.AbstractUnitTest`?
   - Should utilities like `WaitForCondition` move from static `TestUtil` methods to abstract base class protected methods?
   - Are functional test base classes properly aligned?
4. **Keep project-specific utilities local**:
   - Mock R installations (SkylineBatch-specific) stay in `SkylineBatchTest.TestUtils`.
   - Panorama credentials/auth (AutoQC-specific) stay in `AutoQCTest.TestUtils`.
5. **DRY and concise**: Eliminate redundancy, improve discoverability, clean up sprawl.

## Audit Questions

### TestUtils.cs Files
- **File path resolution**: Do both projects solve this the same way? Can `SharedBatchTest.ExtensionTestContext` cover both?
- **Config builders/validation**: SkylineBatch has `GetChangedConfig`, AutoQC has config validation helpers – are these shareable or tool-specific?
- **Wait/polling utilities**: `WaitForCondition` implementations – do they differ? Should they be in abstract base class or `SharedBatchTest.TestUtils`?
- **Mock/test data setup**: Anything beyond R mocks and Panorama auth that's truly shared?

### Abstract Base Classes
- **SkylineBatchTest.AbstractSkylineBatchFunctionalTest** extends `SharedBatchTest.AbstractBaseFunctionalTest` ✅
- **SkylineBatchTest.AbstractSkylineBatchUnitTest** does NOT extend `SharedBatchTest.AbstractUnitTest` ❓ – Review inheritance rationale.
- Does AutoQC have equivalent abstract classes? If so, are they aligned with SharedBatchTest?
- Should common test helpers (WaitForCondition, assertions) be instance methods in abstract bases rather than static TestUtils calls?

### SharedBatchTest Opportunities
- Create `SharedBatchTest.TestUtils` if utilities span both batch tools (avoid forcing into abstract bases if simple static helpers suffice).
- Enhance existing `SharedBatchTest.AssertEx` or `Helpers` if assertion/validation logic is duplicated.
- Ensure `ExtensionTestContext` file path resolution covers both projects' needs.

## Implementation Strategy

1. **Side-by-side comparison**: Print full contents of both `TestUtils.cs` files; highlight overlaps.
2. **Abstract base class review**: Inspect inheritance hierarchy; identify why `AbstractSkylineBatchUnitTest` doesn't extend shared base.
3. **Consolidation plan**:
   - Shared utilities → `SharedBatchTest.TestUtils` (or appropriate abstract base).
   - Tool-specific utilities → remain in `SkylineBatchTest.TestUtils` / `AutoQCTest.TestUtils`.
   - Static vs. instance methods: decide case-by-case (simple helpers stay static; state-dependent move to abstract bases).
4. **Incremental migration**: Move highest-value duplicates first (WaitForCondition, path resolution); validate tests pass after each move.
5. **Document conventions**: Update test coding guidelines with where to add new utilities (SharedBatchTest vs. project-specific).

## Example: WaitForCondition

**Current suspected state** (verify during audit):
```csharp
// SkylineBatchTest – static method in TestUtils or instance in abstract class?
void WaitForCondition(Func<bool> condition, int timeoutMs = 5000) { /* implementation A */ }

// AutoQC – likely similar but possibly different signature/timeout
void WaitForCondition(Func<bool> condition) { /* implementation B */ }
```

**Target state** (decide during implementation):
- **Option A (static utility)**: `SharedBatchTest.TestUtils.WaitForCondition(...)` called by both projects.
- **Option B (abstract base method)**: `SharedBatchTest.AbstractUnitTest.WaitForCondition(...)` inherited by all unit tests.
- Choose based on whether state/context from test instance is needed.

## Success Criteria

- Zero functional duplication between `SkylineBatchTest.TestUtils` and `AutoQCTest.TestUtils` for truly shared utilities.
- Abstract base class hierarchy clean and consistent (both tools extend `SharedBatchTest` bases unless justified exception).
- `SharedBatchTest` provides all common patterns; project-specific utilities clearly scoped.
- Test suites pass with no regressions after consolidation.
- Clear documentation of where to add future test utilities (decision tree: SharedBatchTest vs. project-specific).

## Out of Scope

- **Skyline test utilities**: Batch tools cannot depend on Skyline; no consolidation with `Skyline.Test` infrastructure.
- **Production code consolidation**: Focus is test utilities; `SharedBatch` production code is separate effort.

## Related Work

- **TODO-batch_tools_ci_integration.md**: Consolidated test utilities improve CI skip mechanisms (e.g., shared `Assert.Inconclusive` patterns).
- Discovered during: Skyline/work/20251124_batch_tools_warning_cleanup – WaitForCondition threading fix highlighted duplication.

## References

- `SharedBatchTest` project: AbstractBaseFunctionalTest, AbstractUnitTest, AssertEx, ExtensionTestContext, Helpers, TestFilesDir.
- `SkylineBatchTest/TestUtils.cs` (~527 lines)
- `AutoQCTest/TestUtils.cs` (~326 lines)
- `SkylineBatchTest/AbstractSkylineBatchFunctionalTest.cs`, `AbstractSkylineBatchUnitTest.cs`
