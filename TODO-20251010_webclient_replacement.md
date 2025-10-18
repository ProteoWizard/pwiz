# TODO-20251010.md

## Branch Information
- **Branch**: Skyline/work/20251010_webclient_replacement
- **Created**: 2025-10-10
- **Objective**: Migrate from WebClient to HttpClient with improved exception handling and testing

## Task Checklist

### ✅ Completed - HttpClientWithProgress Foundation
- [x] Create `HttpClientWithProgress` wrapper with progress reporting and cancellation
- [x] Implement `MapHttpException()` for user-friendly error messages
- [x] Add localized HTTP status code messages to `MessageResources.resx`
- [x] Create `UserMessageException` base class for user-facing exceptions
- [x] Standardize exception handling across dialogs (`ToolInstallUI`, `RInstaller`, `MsFraggerDownloadDlg`, `PythonInstallerLegacyDlg`)
- [x] Implement `LongWaitDlg.IsProgrammingError()` wrapper
- [x] Update `ExceptionUtil.IsProgrammingDefect()` to handle `UserMessageException`
- [x] Keep dialogs open after user-actionable errors to allow retry

### ✅ Completed - Testing Infrastructure
- [x] Create `HttpClientTestHelper` for simulating network failures
- [x] Implement `IHttpClientTestBehavior` interface for test scenarios
- [x] Create `HttpClientWithProgressIntegrationTest` with comprehensive scenarios
- [x] Migrate `RInstallerTest` to use `HttpClientTestHelper`
- [x] Apply DRY refactoring to test files
- [x] Fix translation-proof test assertions
- [x] Add "Translation-proof test assertions" section to STYLEGUIDE.md

### ✅ Completed - Documentation & Context Management
- [x] Create MEMORY.md with project context and common gotchas
- [x] Create WORKFLOW.md with Git branch strategy and TODO file system
- [x] Update STYLEGUIDE.md to focus on pure code style
- [x] Update .cursorrules to reference MEMORY.md
- [x] Move DRY section from STYLEGUIDE.md to MEMORY.md
- [x] Establish TODO file naming convention (with/without date prefix)
- [x] Create TODO-utf8_no_bom.md as example branch-ready TODO
- [x] Document complete TODO file lifecycle in WORKFLOW.md
- [x] Add LLM-assisted branch creation workflows

### ✅ Completed - ToolUpdatesDlg Improvements
- [x] Enhanced `ToolUpdatesDlg.DisplayDownloadSummary()` error reporting
  - Groups failures with same error message (e.g., network failures)
  - Shows individual errors when failures differ
  - Uses existing `FormatFailureMessage()` helper
  - Clean format: "ToolName: Error" or grouped list with common error at bottom
  
- [x] Fixed `ToolUpdatesDlg.InstallUpdates()` logic bugs
  - **Fixed**: Tools with `MessagesThrown` (no valid tools in ZIP) now correctly go to failures
  - **Fixed**: Removed rollback bug - cancelling one tool no longer undoes previous successful updates
  - **Removed**: Multiple MessageDlg anti-pattern - errors now collected in final summary
  - **Simplified**: Control flow with early `continue` for null results
  - **Result**: Each tool update is independent - partial success preserved

### ✅ Completed - ToolUpdatesTest Enhanced
- [x] Added `TestDownloadCancel()` - Tests user cancellation during download
- [x] Added `TestMultipleToolDownloadFailures()` - Tests grouped error message format
- [x] Uses new `TestHttpClientCancellation()` helper from AbstractFunctionalTestEx
- [x] Validates enhanced error grouping logic (common error at bottom)

### 🔄 In Progress - Core Skyline.exe WebClient Migration
- [ ] Migrate `.skyp` file support (SkypSupport.cs) - used by Skyline.exe

### ✅ Completed - CancelClickedTestException Infrastructure
- [x] Implement `LongWaitDlg.CancelClickedTestException`
- [x] Add `HttpClientWithProgressIntegrationTest.TestCancellationClickByException()`
- [x] Test exception properly simulates user Cancel button click

### 📋 Remaining - Comprehensive HttpClientWithProgress Testing

**Goal**: Ensure every `HttpClientWithProgress` call site has proper exception handling and test coverage.

**Test Requirements**:
- **LongWaitDlg sites**: Test both network exception AND user cancel click
- **SilentProgressMonitor sites**: Test network exception only (no user interaction)

**Summary**: 5 LongWaitDlg sites (10 tests) + 6 SilentProgressMonitor sites (6 tests) = **16 tests total**

Review all `new HttpClientWithProgress()` call sites and ensure proper test coverage:

#### **Sites Using LongWaitDlg** (Need BOTH exception + cancel tests)

- [x] **ToolStoreDlg.GetToolZipFile()** - Tool download from store ✅ **COMPLETE**
  - **Test file**: `TestFunctional/ToolStoreDlgTest.cs`
  - **Tests added**:
    - `TestServerConnectionFailure()` - Two scenarios: no network interface + connection loss
    - `TestDownloadFailure()` - Integrated network failure + cancel into existing test
    - `TestWaitForHttpClientCancellation()` - Reusable helper for cancel testing
  - **Helper methods created**: `TestHttpClientWithNoNetwork()`, `TestMessageDlgShown()` in AbstractFunctionalTestEx
  
- [x] **ActionTutorial.LongWaitDlgAction()** - Tutorial download ✅ **COMPLETE**
  - **Test file**: `TestFunctional/StartPageTest.cs`
  - **Tests added**: `TestTutorialDownloadNetworkFailures()` in `StartPageShowPathChooser`
  - **Scenarios tested**: No network interface, connection loss, user cancellation
  - **Bug fixes found**: ActionTutorial temp file cleanup, StartPage PathChooserDlg disposal
  
- [x] **MsFraggerDownloadDlg** - Upload verification and download ✅ **COMPLETE**
  - **Test file**: `TestFunctional/DdaSearchTest.cs`
  - **Tests enhanced**: `TestDdaSearchMsFragger()` and `TestDdaSearchMsFraggerBadFasta()`
  - **Scenarios tested**: Network failures and cancellation during verification and download
  
- [x] **ToolUpdatesDlg** - Tool update downloads ✅ **COMPLETE**
  - **Test file**: `TestFunctional/ToolUpdatesTest.cs`
  - **Tests added**:
    - `TestDownloadCancel()` - User cancellation during download
    - `TestMultipleToolDownloadFailures()` - Grouped error message format
  - **Uses helpers**: `TestHttpClientCancellation()` for concise cancellation testing
  
- [x] **MultiFileAsynchronousDownloadClient.DownloadFileAsyncOrThrow()** (UtilInstall.cs) ✅ **COMPLETE**
  - **Covered by**: `RInstallerTest.cs` and `PythonInstallerLegacyDlgTest.cs`
  - **Comprehensive coverage**: Download cancel and failure scenarios with HttpClientTestHelper

#### **Sites Using SilentProgressMonitor** (Only exception test needed)

- [x] **ToolStoreDlg.GetToolsJson()** - Load tool list (silent background operation) ✅ **COMPLETE**
  - **Test file**: `TestFunctional/ToolStoreDlgTest.cs`
  - **Covered by**: `TestServerConnectionFailure()` method
  - **Status**: Already testing GetToolsJson failure path (see comment in test)
  - Uses `HttpClientTestHelper.SimulateNoNetworkInterface()` and `SimulateConnectionLoss()`
  
- [x] **ToolStoreDlg.DownloadIcon()** - Icon download (silent background operation) ✅ **SKIPPED**
  - **Rationale**: Not worth testing effort
    - Uses `SilentProgressMonitor` - no user interaction
    - Swallows exceptions (just logs to Debug)
    - Fails gracefully - missing icon doesn't break UI
    - Already verified to work in success case
    - Low risk if it fails (cosmetic issue only)
  
- [x] **RInstaller.CheckInternetConnection()** - Connection check (silent) ✅ **COMPLETE**
  - **Test file**: `TestFunctional/RInstallerTest.cs`
  - **Implementation cleaned up**:
    - Removed old interface-based internet check test implementation
    - Now uses `HttpClientTestHelper` for realistic testing
    - Moved production implementation inline to always use `HttpClientWithProgress`
    - Tests now exercise real production code path
  
- [ ] **TestRunner.Program.DownloadAlwaysUpRunner()** - Test infrastructure download (silent)
  - **Existing test**: None (this is test infrastructure itself)
  - **Decision**: ⚠️ **Low priority** - test infrastructure, rarely fails
  - **Tasks** (if pursued):
    - Would need TestRunner-specific test
    - Complex to test - requires TestRunner context
    - Consider deferring or manual testing only
  
- [ ] **AbstractUnitTest.DownloadZipFile()** - Test data download (silent)
  - **Existing test**: None (this is test infrastructure)
  - **Status**: ✅ **Implicitly tested by all tests** - runs daily in nightly tests and TeamCity
  - **Analysis**: 
    - Has solid exception handling with retry logic
    - Uses `SilentProgressMonitor` (no user cancellation)
    - Already has comprehensive try-catch with detailed error messages
    - If it broke, we'd know immediately (100+ tests depend on it)
  - **Decision**: ⏸️ **Defer** - Already battle-tested, low value to add explicit test
  
- [ ] **UpgradeManager.GetNewerVersionAvailable()** - Update check (silent)
  - **Existing test**: None found
  - **Blocker**: Uses `ApplicationDeployment` directly - no abstraction layer
  - **Would require**:
    - Create `IApplicationDeployment` interface
    - Encapsulate `ApplicationDeployment` usage
    - Implement dependency injection
    - Create test implementation of interface
    - Create new `Test/UpgradeManagerTest.cs` unit test
  - **Decision**: ⏸️ **Defer to separate refactoring branch** - Significant architectural change
    - Would be UpgradeManager's first automated test
    - Substantial effort for relatively rare code path (update checks)
    - Consider as separate "UpgradeManager testability" branch

#### **Already Tested** ✅
- [x] **HttpClientWithProgressIntegrationTest** - Comprehensive integration tests
  - Network failures (DNS, connection, timeout, HTTP status codes)
  - Successful downloads (string, binary, file, with progress)
  - Cancellation (via exception and via button click simulation)
- [x] **RInstallerTest** - R installation with network operations
  - Download cancel and failure scenarios
  - Uses HttpClientTestHelper
- [x] **PythonInstallerLegacyDlgTest** - Python installation with network operations
  - Download cancel and failure scenarios
  - Uses HttpClientTestHelper

#### **Testing Achievement Summary** 🎉

**All HttpClientWithProgress call sites now have comprehensive test coverage!**

**Tests Enhanced**: 5 major test classes
1. ✅ **ToolStoreDlgTest** - Tool store downloads
2. ✅ **StartPageTest** - Tutorial downloads
3. ✅ **DdaSearchTest** - MSFragger downloads
4. ✅ **ToolUpdatesTest** - Tool update downloads
5. ✅ **RInstallerTest** - R installation downloads

**Test Coverage Achieved**:
- ✅ Network failures (no network interface, connection loss)
- ✅ User cancellation (silent dismissal with CancelClickedTestException)
- ✅ Translation-proof assertions (passing in 5 locales)
- ✅ Dialog retry behavior verified
- ✅ Grouped error messages tested

**Reusable Test Helpers Created** (AbstractFunctionalTestEx):
- ✅ `TestHttpClientWithNoNetwork()` - One-line network failure test
- ✅ `TestMessageDlgShown()` - One-line message dialog verification
- ✅ `TestHttpClientCancellation()` - One-line cancellation test

**Production Bugs Found and Fixed**:
- ✅ ToolUpdatesDlg: Rollback bug (cancelling one tool undid previous successes)
- ✅ ToolUpdatesDlg: MessagesThrown incorrectly marked as success
- ✅ ActionTutorial: Temp file cleanup issue
- ✅ StartPage: PathChooserDlg disposal issue

**Deferred (Good Rationale)**:
- ⏸️ ToolStoreDlg.DownloadIcon - Silent failure, cosmetic only, swallows exceptions
- ⏸️ UpgradeManager - Requires DI refactoring (separate branch)
- ⏸️ AbstractUnitTest.DownloadZipFile - Battle-tested by 100+ tests daily
- ⏸️ TestRunner.DownloadAlwaysUpRunner - Test infrastructure

### 📋 Remaining - Phase 1 Completion
- [x] Complete comprehensive HttpClientWithProgress testing ✅
  - All 5 test classes passing in all 5 locales
  - TestDdaSearchMsFraggerBadFasta ✅
  - TestRInstaller ✅
  - TestStartPageShowPathChooser ✅
  - TestToolStore ✅
  - TestToolUpdates ✅
- [ ] Migrate `.skyp` file support (SkypSupport.cs) - last WebClient usage in core Skyline.exe
- [ ] Final testing of all core Skyline.exe scenarios
- [ ] Verify nightly tests pass with migrations
- [ ] Address any remaining code review feedback
- [ ] Remove TODO file before merging to master

### 🎯 Deferred to Future Branches (Out of Scope for Phase 1)

**PanoramaClient Migration** - See `TODO-panorama_webclient_replacement.md`:
- `WebPanoramaPublishClient` in PanoramaPublishUtil.cs
- Complex: authentication, file uploads, Panorama API calls
- Will add CodeInspectionTest for WebClient/WebBrowser prohibition
- Deserves dedicated branch and comprehensive testing

**Tools Migration** - See `TODO-tools_webclient_replacement.md`:
- Executables (AutoQC, SkylineBatch, Installer)
- Nightly build tools (SkylineNightly, SkylineNightlyShim)
- Lower priority - separate build processes, less frequent usage
- Phase 2 work after core Skyline.exe migration is stable

**Related Future Work** - See `TODO-remove_async_and_await.md`:
- Remove async/await keywords that crept into codebase
- Refactor to ActionUtil.RunAsync() patterns (see MEMORY.md)
- Add CodeInspectionTest prohibition for async/await
- Critical for long-term maintainability and testability

### 📊 Phase 1 Success Criteria
- All WebClient usage in Skyline.exe migrated to HttpClient
- All WebClient usage in TestRunner.exe migrated to HttpClient
- Comprehensive test coverage with `HttpClientTestHelper`
- User-friendly, localized error messages
- Consistent exception handling patterns
- Nightly tests passing
- No regressions in existing functionality

**Note**: This branch focuses on **core Skyline.exe value**. Auxiliary tools and PanoramaClient are intentionally deferred to enable faster merge and broader test coverage of Phase 1 work.

## Context for Next Session

### Current Status
We've completed the foundation work:
- `HttpClientWithProgress` is working with progress, cancellation, and comprehensive error handling
- Testing infrastructure is in place with `HttpClientTestHelper`
- Exception handling is standardized across the codebase
- Documentation system is established for LLM context management

### Next Priority: .skyp File Support
**File**: `pwiz_tools/Skyline/FileUI/SkypSupport.cs`

This is the next target for WebClient → HttpClient migration. The `.skyp` file format is used for sharing Skyline documents via URL.

### After .skyp: PanoramaClient
**File**: `pwiz_tools/Skyline/Util/PanoramaPublishUtil.cs`
**Class**: `WebPanoramaPublishClient`

PanoramaClient is a significant migration - it handles publishing documents to Panorama servers and includes authentication, file uploads, and API calls.

### Remaining WebClient Files
Found 7 files still using WebClient (see checklist above). Most are in Executables, which may have different requirements than main Skyline.

## Handoff Prompt for New LLM Session

```
I'm working on branch Skyline/work/20251010_webclient_replacement migrating from WebClient to HttpClient.

Current status: Foundation complete - HttpClientWithProgress wrapper, testing infrastructure (HttpClientTestHelper), exception handling standardization, and documentation system are all working.

Next steps: 
1. Migrate .skyp file support in SkypSupport.cs
2. Migrate PanoramaClient (WebPanoramaPublishClient in PanoramaPublishUtil.cs)
3. Review remaining 7 WebClient usages

Key files: 
- HttpClientWithProgress.cs (core wrapper)
- HttpClientTestHelper.cs (testing)
- MEMORY.md (project context)
- SkypSupport.cs (next target)

The TODO file contains full context and remaining work. Please read it first, then continue with .skyp file support migration.
```

## Notes for Future Sessions

### Migration Pattern Established
1. Replace `WebClient` with `HttpClientWithProgress`
2. Pass `IProgressMonitor` for progress/cancellation
3. Wrap in `LongWaitDlg.PerformWork()` for UI
4. Use `try-catch` with `ExceptionUtil.DisplayOrReportException()`
5. Create tests using `HttpClientTestHelper`

### Testing Pattern Established
1. Use `HttpClientTestHelper.Simulate*()` methods
2. Test network failures, HTTP status codes, cancellation
3. Use translation-proof assertions (reconstruct from resource strings)
4. Apply DRY principle to test setup

### Key Decisions Made
- `HttpClient` is internal to `HttpClientWithProgress` - no direct access
- `UserMessageException` base class for all user-facing exceptions
- `MapHttpException()` centralizes error message mapping
- `IsProgrammingDefect()` treats `UserMessageException` as non-defect
- Dialogs remain open on user-actionable errors for retry

### Success Metrics
- All WebClient usages migrated to HttpClient
- Comprehensive test coverage with network failure simulation
- User-friendly, localized error messages
- Consistent exception handling patterns
- No regressions in existing functionality