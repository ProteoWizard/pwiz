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

#### Implementation Steps (COMPLETED)

1. ✅ Create new `SharedBatch.AbstractUnitTest` (minimal root with TestContext, WaitForCondition)
2. ✅ Move old `SharedBatch.AbstractUnitTest` → `AutoQC.AbstractAutoQcUnitTest`
3. ✅ Update `SharedBatch.AbstractBaseFunctionalTest`:
   - Changed base class from standalone to `: AbstractUnitTest`
   - Inherited `WaitForCondition(Func<bool>)` from base (removed duplicate)
4. ✅ Update `SkylineBatch.AbstractSkylineBatchUnitTest` to extend `SharedBatch.AbstractUnitTest`
5. ✅ Update 3 AutoQC unit test files to use `AbstractAutoQcUnitTest`
6. ✅ Mark `WaitForCondition` in both `TestUtils.cs` files as obsolete (throws NotImplementedException with migration guidance)
7. ✅ Update `SkylineBatchLoggerTest` to use instance method `WaitForCondition()`
8. ✅ Add `AbstractAutoQcUnitTest.cs` to AutoQCTest.csproj
9. ✅ Fix `GetTestResultsPath()` - kept project-specific implementations that call `TestUtils.GetTestResultsPath()`

## Implementation Results (2025-12-04)

### Test Results - Phase 1: Test Utility Consolidation
- **AutoQC**: ✅ 18/18 tests pass
- **SkylineBatch**: ✅ 37/38 tests pass (1 network-related failure in DataDownloadTest)

### Files Changed - Phase 1
1. `SharedBatchTest/AbstractUnitTest.cs` - Replaced with minimal root base class
2. `SharedBatchTest/AbstractBaseFunctionalTest.cs` - Now extends AbstractUnitTest
3. `AutoQCTest/AbstractAutoQcUnitTest.cs` - NEW: AutoQC-specific base class
4. `AutoQCTest/AutoQCTest.csproj` - Added new file
5. `AutoQCTest/ConfigManagerTest.cs` - Uses AbstractAutoQcUnitTest
6. `AutoQCTest/AutoQcConfigTest.cs` - Uses AbstractAutoQcUnitTest
7. `AutoQCTest/PanoramaTest.cs` - Uses AbstractAutoQcUnitTest
8. `AutoQCTest/TestUtils.cs` - WaitForCondition marked obsolete
9. `SkylineBatchTest/AbstractSkylineBatchUnitTest.cs` - Extends SharedBatch.AbstractUnitTest
10. `SkylineBatchTest/AbstractSkylineBatchFunctionalTest.cs` - Minor cleanup
11. `SkylineBatchTest/SkylineBatchLoggerTest.cs` - Uses instance WaitForCondition
12. `SkylineBatchTest/TestUtils.cs` - WaitForCondition marked obsolete

## Implementation Results (2025-12-05)

### Test Results - Phase 2: AutoQC Test Reliability
- **AutoQC**: ✅ 18/18 tests pass (100% reliable, no intermittent failures)
- **Test time**: 103 seconds (down from 669 seconds in initial failing run)
- **Output**: Clean (only pre-existing log4net warning)

### Files Changed - Phase 2
1. `AutoQC/AnnotationsFileWatcher.cs` - Added IDisposable, _cancelled flag, event handler guards
2. `AutoQC/AutoQCFileSystemWatcher.cs` - Added IDisposable, _cancelled flag, event handler guards
3. `AutoQC/ConfigRunner.cs` - Added IDisposable to dispose owned watchers
4. `AutoQC/AutoQcConfigManager.cs` - Added RemoveConfigRunner() helper, fixed collection modification bug
5. `AutoQC/MainForm.cs` - Use DisplayErrorWithException() for better diagnostics
6. `AutoQCTest/AutoQcFileSystemWatcherTest.cs` - Removed async/await, added using blocks, simplified timing
7. `SharedBatch/Logger.cs` - Added defensive null check in WriteToBuffer()
8. `SharedBatchTest/AbstractUnitTest.cs` - Enhanced WaitForCondition() with optional params, better timing
9. `CommonUtil/SystemUtil/CommonActionUtil.cs` - RunAsync() now returns Thread for proper synchronization

### Known Issues - Test Reliability (For CI Integration)

#### ✅ FIXED: Logger NRE from FileSystemWatcher Events (2025-12-05)
**Problem**: NullReferenceExceptions in `Logger.WriteToBuffer()` during test cleanup
- Root cause: FileSystemWatcher event handlers firing after Logger disposal
- Occurred in background threads, didn't fail tests but created noise
- Stack trace: `AutoQCFileSystemWatcher.FileAdded/OnFileWatcherError` → `Logger.WriteToBuffer` → NRE on `_logBuffer`

**Solution Implemented**:
1. Made `AutoQCFileSystemWatcher` and `AnnotationsFileWatcher` implement `IDisposable`
2. `Stop()` sets `_cancelled = true` (allows restart), does NOT dispose FileSystemWatcher
3. `Dispose()` calls `Stop()` then disposes underlying `_fileWatcher`
4. Event handlers check `if (_cancelled) return;` to prevent work after stop
5. Made `ConfigRunner` implement `IDisposable` to dispose owned watchers
6. Created `RemoveConfigRunner()` helper in `AutoQcConfigManager` following DRY principle
7. Tests use `using` statements for proper cleanup

**Test Reliability Improvements**:
1. Fixed async/await anti-pattern in `AutoQcFileSystemWatcherTest.TestGetNewFilesForInstrument()`
   - Changed from `async void` (caused assertions to fail in background threads)
   - Now uses proper synchronous pattern with `using` blocks
   - Test assertions now properly fail tests instead of becoming unhandled exceptions
2. Simplified test timing logic:
   - Replaced `AssertCorrectFileCount()` (20ms timeout with DateTime arithmetic)
   - Now uses `WaitForCondition()` from `AbstractUnitTest` (5 second timeout)
   - Much more reliable and maintainable
3. Created helper methods:
   - `ValidateWatcherFiles()` - validates expected file count and contents
   - `GetWatcherFiles()` - clean enumeration pattern
   - `StartWatching()` - consistent watcher startup with validation
4. Enhanced `CommonActionUtil.RunAsync()` to return Thread:
   - Changed return type from `void` to `Thread`
   - Enables proper test synchronization with `.Join()`
   - Allows containing async work at boundaries instead of spreading async/await
5. Improved `AbstractUnitTest.WaitForCondition()`:
   - Changed to static method with optional parameters
   - Default 5-second timeout, default 100ms timestep
   - Fixed timing logic to use loop count instead of DateTime arithmetic
   - Better error messages with timeout information
6. Added defensive null check in `Logger.WriteToBuffer()`:
   - Protects against calls after disposal or before initialization
   - Prevents NRE in edge cases
7. Fixed error handling in `MainForm.SwitchLogger()`:
   - Changed from `DisplayError()` to `DisplayErrorWithException()`
   - Shows full stack trace for debugging (helped diagnose Logger issues)

**Results**:
- ✅ Logger NREs completely eliminated!
- ✅ All 18 AutoQC tests pass reliably (100%)
- ✅ No unhandled exceptions in test output
- ✅ Clean test output (except pre-existing log4net warning)

#### ⚠️ DEFERRED: Logger.DisplayLogFromFile() Index Out of Range (2025-12-05)
**Problem**: `ArgumentOutOfRangeException` in `Logger.DisplayLogFromFile()` at line 238
- Occurs during config switching when UI tries to display log
- Causes error dialog to appear during `TestPanoramaWebInteraction`
- Stack trace:
  ```
  at System.Collections.Generic.List`1.set_Item(Int32 index, T value)
  at SharedBatch.Logger.DisplayLogFromFile() in Logger.cs:line 238
  at AutoQC.MainForm.<>c__DisplayClass35_0.<SwitchLogger>b__0() in MainForm.cs:line 469
  ```

**Status**: Bug was discovered during FileSystemWatcher testing but appears to be pre-existing race condition. Not related to watcher disposal changes. Test now passes reliably after async/await fixes, suggesting timing improvements may have mitigated the issue.

**Impact**: Previously caused `TestPanoramaWebInteraction` to fail intermittently. Now passing consistently after test reliability improvements.

**Next Steps**: Monitor for recurrence. If it reappears, investigate Logger.DisplayLogFromFile() line 238 - likely array indexing issue during concurrent log updates.

#### ✅ FIXED: AutoQcFileSystemWatcherTest Timing Issues (2025-12-05)
**Problem**: Unhandled exceptions in background threads during `TestGetNewFiles`
- **First failure**: `Assert.AreEqual failed. Expected:<1>. Actual:<0>.` at line 198
  - Test expects 1 file to be detected, watcher found 0
- **Second failure**: `Assert.AreEqual failed. Expected:<5>. Actual:<2>.` at line 230
  - Test expects 5 files in subfolder test, watcher found only 2

**Stack Trace**:
```
at AutoQCTest.AutoQcFileSystemWatcherTest.<TestGetNewFilesForInstrument>d__5.MoveNext() in AutoQcFileSystemWatcherTest.cs:line 198/230
```

**Root Causes**:
1. `async void` test method caused assertions to fail in background threads (unhandled exceptions instead of test failures)
2. Insufficient wait timeout (20ms was too short for FileSystemWatcher event latency)
3. Overly complex `AssertCorrectFileCount()` with manual DateTime arithmetic

**Solution Implemented**:
1. Removed async/await entirely - changed to synchronous test method with proper `using` blocks
2. Replaced 20ms timeout with `WaitForCondition()` using 5-second default timeout
3. Simplified test structure with clean helper methods
4. Proper resource disposal with using statements

**Results**: Test now passes reliably! No more unhandled exceptions. ✅

#### ✅ FIXED: log4net Configuration Warnings (2025-12-05)
**Problem**: `log4net:ERROR Failed to find configuration section 'log4net' in application's .config file`
- Shows in test output: "Test Run deployment issue: Failed to get the file for deployment item 'SkylineLog4Net.config'"
- Missing file: `AutoQCTest\bin\Debug\SkylineLog4Net.config`

**Root Cause**: Test projects lacked App.config files with log4net configuration. Tests inherited obsolete references to non-existent `SkylineLog4Net.config` from Skyline test infrastructure.

**Solution Implemented**:
1. Created minimal `App.config` files for both test projects:
   - `AutoQCTest/App.config` - Basic log4net configuration section
   - `SkylineBatchTest/App.config` - Basic log4net configuration section
2. Added `<None Include="App.config" />` to both .csproj files
3. Removed obsolete `SkylineLog4Net.config` references from `AbstractAutoQcUnitTest.cs`:
   - Removed assembly-level `[assembly: log4net.Config.XmlConfigurator(ConfigFile = "SkylineLog4Net.config", Watch = true)]`
   - Removed `[DeploymentItem("SkylineLog4Net.config")]` attribute

**Results**:
- ✅ AutoQC: All 18 tests pass, **zero log4net warnings**
- ✅ SkylineBatch: 37/38 tests pass, **zero log4net warnings**
- ✅ Clean test output for both projects

#### ⚠️ INTERMITTENT: SkylineBatch DataDownloadTest Network Dependency (2025-12-05)
**Problem**: `DataDownloadTest` exhibits intermittent failure due to external FTP server dependency
- Test downloads real data from `ftp://ftp.peptideatlas.org/` (PeptideAtlas public FTP server)
- Credentials: username=PASS00589, password=WF6554orn

**Observed Behaviors**:
1. **Run 1**: Failed quickly with `Assert.Inconclusive("Skipping test due to ConnectionErrorForm (network or server unavailable)")`
   - Test properly detected connection error and marked itself inconclusive
2. **Run 2**: Hung indefinitely during test execution
   - Test appears to be waiting for FTP connection that neither fails fast nor succeeds
   - Network timeout longer than test's 30-second timeout

**Analysis**:
- Failure location: `TestSmallDataDownload()` line 62 in `DataDownloadFunctionalTest.cs`
- Uses `FunctionalTestUtil.WaitForCondition()` which checks for `ConnectionErrorForm` to bail out early
- When ConnectionErrorForm doesn't appear quickly enough, test can hang

**Impact**: Pre-existing intermittent issue, not introduced by our changes. Test has built-in detection mechanism but it's not always triggered promptly.

**Root Cause**: External dependency on PeptideAtlas FTP server availability and network conditions beyond our control.

**Next Steps**:
- [ ] Investigate whether test needs more robust timeout/retry logic
- [ ] Consider mocking FTP server or using local test data
- [ ] Test FTP server availability independently to understand if source is flaky or test needs improvement
- [ ] Related to ongoing work to reduce network dependencies in tests

**Status**: Documented for future improvement. Not blocking current work since:
- Pre-existing issue (37/38 tests passed in Phase 1, same ratio now)
- Test has `Assert.Inconclusive()` mechanism to skip when network unavailable
- Our log4net fixes did not make this consistently fail

### Next Steps
- [x] Fix Logger NRE from FileSystemWatcher events (COMPLETED 2025-12-05)
- [x] Improve AutoQcFileSystemWatcherTest timing reliability (COMPLETED 2025-12-05)
- [x] Fix async/await anti-pattern in tests (COMPLETED 2025-12-05)
- [x] Verify all tests pass reliably (COMPLETED 2025-12-05 - 18/18 AutoQC pass)
- [x] Fix log4net configuration warnings (COMPLETED 2025-12-05)
- [ ] Monitor Logger.DisplayLogFromFile() for recurrence (deferred - appears fixed by timing improvements)
- [ ] Investigate DataDownloadTest FTP dependency and improve reliability (future work)
- [ ] Consider additional consolidation opportunities identified in audit (future work)
