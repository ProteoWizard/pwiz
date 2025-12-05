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
- `SkylineBatchTest/TestUtils.cs` (~535 lines)
- `AutoQCTest/TestUtils.cs` (~326 lines)
- `SkylineBatchTest/AbstractSkylineBatchFunctionalTest.cs`, `AbstractSkylineBatchUnitTest.cs`

## Audit Results (2025-12-04)

### Duplicated Utilities (Candidates for SharedBatchTest)

#### 1. `WaitForCondition` - **EXACT DUPLICATE** ✅
**SkylineBatchTest** (lines 426-435):
```csharp
public static void WaitForCondition(ConditionDelegate condition, TimeSpan timeout, int timestep, string errorMessage)
```
**AutoQCTest** (lines 215-224):
```csharp
public static void WaitForCondition(Func<bool> condition, TimeSpan timeout, int timestep, string errorMessage)
```
- Identical logic, identical signature (just different delegate typedef vs Func<bool>)
- **Action**: Move to SharedBatchTest.TestUtils (static method)

#### 2. `GetTestFilePath` - **SEMANTICALLY SIMILAR** ⚠️
**SkylineBatchTest** (lines 79-93):
- Navigates from current directory or bin directory
- Uses fixed path: "Test" subfolder
**AutoQCTest** (lines 70-84):
- Uses ExtensionTestContext.GetProjectDirectory(@"Executables\AutoQC")
- Uses fixed path: "TestData" subfolder
- **Different test data locations**: SkylineBatch uses "Test/", AutoQC uses "TestData/"
- **Action**: Keep project-specific (path differences intentional)

#### 3. `InitializeSettingsImportExport` - **SEMANTICALLY SIMILAR** ⚠️
**SkylineBatchTest** (lines 403-407):
```csharp
ConfigList.Importer = SkylineBatchConfig.ReadXml;
ConfigList.XmlVersion = SkylineBatch.Properties.Settings.Default.XmlVersion;
```
**AutoQCTest** (lines 226-230):
```csharp
ConfigList.Importer = AutoQcConfig.ReadXml;
ConfigList.XmlVersion = AutoQC.Properties.Settings.Default.XmlVersion;
```
- Same pattern, different config types
- **Action**: Keep project-specific (uses project-specific config types)

#### 4. `GetTestConfig` - **PROJECT-SPECIFIC** 🔸
- Both create test configs, but use different config types (SkylineBatchConfig vs AutoQcConfig)
- **Action**: Keep project-specific

#### 5. `ConfigListFromNames` - **SEMANTICALLY IDENTICAL** ✅
**SkylineBatchTest** (lines 342-350):
```csharp
public static List<IConfig> ConfigListFromNames(List<string> names)
{
    var configList = new List<IConfig>();
    foreach (var name in names)
        configList.Add(GetTestConfig(name));
    return configList;
}
```
**AutoQCTest** (lines 184-192):
```csharp
public static List<AutoQcConfig> ConfigListFromNames(string[] names)
{
    var configList = new List<AutoQcConfig>();
    foreach (var name in names)
        configList.Add(GetTestConfig(name));
    return configList;
}
```
- Same logic, different types (IConfig vs AutoQcConfig, List vs array)
- **Action**: Keep project-specific (type differences make consolidation awkward)

### SkylineBatch-Specific Utilities (KEEP LOCAL)

- **Mock R Installations** (lines 57-77): `SetupMockRInstallations`, `ClearMockRInstallations`
- **R Version Handling** (lines 255-293): `ReplaceRVersionWithCurrent`, `CreateBcfgWithCurrentRVersion`
- **Config Builders** (lines 95-340): `GetChangedConfig`, `GetChangedMainSettings`, etc.
- **File Utilities** (lines 437-531): `CompareFiles`, `CopyFileFindReplace`, `CopyFileWithLineTransform`
- **Test Results Path** (lines 374-396): `GetTestResultsPath`, `GetTestLogger` (uses TestContext)

### AutoQC-Specific Utilities (KEEP LOCAL)

- **Panorama Credentials** (lines 33-284): `GetPanoramaWebUsername`, `GetPanoramaWebPassword`, environment variable handling
- **Panorama Test Folder Management** (lines 286-304): `CreatePanoramaWebTestFolder`, `DeletePanoramaWebTestFolder`
- **Skyline Bin Resolution** (lines 86-117): `GetSkylineBinDirectory` (finds Debug/Release based on modification time)
- **Config Manager** (lines 194-213): `GetTestConfigManager`
- **Text Assertion** (lines 232-251): `AssertTextsInThisOrder`

### Shared Infrastructure Already in SharedBatchTest

- ✅ `ExtensionTestContext` - Used by AutoQC for `GetProjectDirectory`
- ✅ `AssertEx` - Used by AutoQC for `NoExceptionThrown`
- ✅ Abstract base classes - `AbstractBaseFunctionalTest`, `AbstractUnitTest`

### Consolidation Plan

#### Phase 1: Move Clear Duplicates
1. **WaitForCondition** → `SharedBatchTest.TestUtils.WaitForCondition`
   - Identical implementation
   - Update both projects to use shared version

#### Phase 2: Consider Enhancing SharedBatchTest (Future)
- `AssertTextsInThisOrder` (AutoQC) - useful assertion, could move to SharedBatchTest.AssertEx
- File comparison utilities (SkylineBatch) - if AutoQC needs them

#### Phase 3: Document Patterns
- Update test coding guidelines with where to add utilities
- Document why certain utilities remain project-specific

### Abstract Base Class Review (COMPLETED 2025-12-04)

#### Current Hierarchy Analysis

**SharedBatch:**
- `AbstractUnitTest` (standalone)
  - Properties: TestContext, AllowInternetAccess, RunPerfTests, etc.
  - Used by: AutoQC only (NOT used by SkylineBatch)
  - **Problem**: Name suggests "shared" but only one project uses it

- `AbstractBaseFunctionalTest` (standalone)
  - Complex functional test infrastructure
  - Has: WaitForConditionUI, RunUI, test file management
  - Missing: Plain `WaitForCondition`
  - Used by: Both projects' functional tests

**SkylineBatch:**
- `AbstractSkylineBatchUnitTest` (standalone, does NOT extend SharedBatch.AbstractUnitTest)
  - Simple: TestContext + GetTestResultsPath/GetTestLogger helpers
  - Used by: SkylineBatchTest unit tests
  - **Better candidate for root base class** (simpler, more fundamental)

- `AbstractSkylineBatchFunctionalTest` : SharedBatch.AbstractBaseFunctionalTest ✅

**AutoQC:**
- Unit tests extend SharedBatch.AbstractUnitTest directly
- Functional tests extend SharedBatch.AbstractBaseFunctionalTest

#### Skyline's Pattern (for comparison)

```
AbstractUnitTest (root - simple base)
  └─ AbstractUnitTestEx : AbstractUnitTest (adds optional features)
      └─ AbstractFunctionalTestEx : AbstractUnitTestEx
          └─ Has WaitForCondition as INSTANCE METHOD (not static)
```

#### Design Decision: Create Symmetry Between Projects

**Approved Approach**: Move project-specific extensions to their respective projects, keep SharedBatch truly minimal.

**New Hierarchy:**

```
SharedBatch:
  AbstractUnitTest (moved/renamed from SkylineBatchTest.AbstractSkylineBatchUnitTest)
    ├── TestContext
    ├── GetTestResultsPath()
    ├── GetTestLogger()
    └── Simple, focused, truly reusable ROOT base class

  AbstractBaseFunctionalTest : AbstractUnitTest (changed to extend root)
    ├── WaitForCondition(Func<bool>) - instance method (NEW)
    ├── WaitForConditionUI, RunUI, etc.
    └── Used by both projects' functional tests

SkylineBatch:
  AbstractSkylineBatchUnitTest : SharedBatch.AbstractUnitTest
    ├── GetTestConfigRunner, GetTestConfigManager
    └── SkylineBatch-specific helpers

  AbstractSkylineBatchFunctionalTest : SharedBatch.AbstractBaseFunctionalTest
    └── (unchanged)

AutoQC:
  AbstractAutoQcUnitTest : SharedBatch.AbstractUnitTest (moved from SharedBatch.AbstractUnitTestEx)
    ├── AllowInternetAccess, RunPerfTests, etc.
    └── AutoQC-specific properties

  AutoQC functional tests : SharedBatch.AbstractBaseFunctionalTest
```

#### Design Rationale

1. **Symmetry**: Both projects now have `Abstract[Project]UnitTest` extending shared root
2. **Organic growth**: SharedBatch contains only proven-shared code
3. **Natural migration**: When SkylineBatch needs something from AutoQC's base, push it down to SharedBatch.AbstractUnitTest
4. **Follows Skyline pattern**: Simple root → extended variants → functional tests
5. **WaitForCondition as instance method**: Aligns with Skyline's AbstractFunctionalTestEx pattern

#### Implementation Steps

1. Create new `SharedBatch.AbstractUnitTest` (content from `AbstractSkylineBatchUnitTest`)
2. Rename current `SharedBatch.AbstractUnitTest` → move to `AutoQC.AbstractAutoQcUnitTest`
3. Update `SharedBatch.AbstractBaseFunctionalTest`:
   - Change base class from standalone to `: AbstractUnitTest`
   - Add `WaitForCondition(Func<bool>)` as protected instance method
4. Update `SkylineBatch.AbstractSkylineBatchUnitTest` to extend `SharedBatch.AbstractUnitTest`
5. Update AutoQC unit test files to use `AbstractAutoQcUnitTest`
6. Remove `WaitForCondition` from both `TestUtils.cs` files
7. Remove incorrect static `WaitForCondition` from `SharedBatchTest/Helpers.cs`
