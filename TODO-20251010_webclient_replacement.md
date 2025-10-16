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
- [ ] **ToolStoreDlg.GetToolZipFile()** - Tool download from store
  - Test: Network failure exception
  - Test: User cancel click (CancelClickedTestException)
  
- [ ] **ActionTutorial.LongWaitDlgAction()** - Tutorial download
  - Test: Network failure exception
  - Test: User cancel click (CancelClickedTestException)
  
- [ ] **MsFraggerDownloadDlg.ClickRequestVerificationCode()** - Upload verification
  - Test: Network failure exception
  - Test: User cancel click (CancelClickedTestException)
  
- [ ] **MsFraggerDownloadDlg.ClickDownload()** - Download with verification code
  - Test: Network failure exception
  - Test: User cancel click (CancelClickedTestException)
  
- [ ] **MultiFileAsynchronousDownloadClient.DownloadFileAsyncOrThrow()** (UtilInstall.cs)
  - Test: Network failure exception
  - Test: User cancel click (CancelClickedTestException)
  - Note: Used by RInstaller and PythonInstaller - may already have coverage

#### **Sites Using SilentProgressMonitor** (Only exception test needed)
- [ ] **ToolStoreDlg.GetToolsJson()** - Load tool list (silent background operation)
  - Test: Network failure exception
  
- [ ] **ToolStoreDlg.DownloadIcon()** - Icon download (silent background operation)
  - Test: Network failure exception
  
- [ ] **RInstaller.CheckInternetConnection()** - Connection check (silent)
  - Test: Network failure exception
  
- [ ] **TestRunner.Program.DownloadAlwaysUpRunner()** - Test infrastructure download (silent)
  - Test: Network failure exception
  
- [ ] **AbstractUnitTest.EnsureZipFileDownloaded()** - Test data download (silent)
  - Test: Network failure exception
  
- [ ] **UpgradeManager.GetNewerVersionAvailable()** - Update check (silent)
  - Test: Network failure exception

#### **Already Tested** ✅
- [x] **HttpClientWithProgressIntegrationTest** - Comprehensive integration tests
  - Network failures (DNS, connection, timeout, HTTP status codes)
  - Successful downloads (string, binary, file, with progress)
  - Cancellation (via exception and via button click simulation)

### 📋 Remaining - Phase 1 Completion
- [ ] Complete comprehensive HttpClientWithProgress testing (above)
- [ ] Review any other WebClient usage in Skyline.exe/TestRunner.exe code
- [ ] Final testing of all core Skyline.exe scenarios
- [ ] Verify nightly tests pass with migrations
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