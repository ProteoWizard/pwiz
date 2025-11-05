# TODO-20251019_skyp_webclient_replacement.md

## Branch Information
- **Branch**: `Skyline/work/20251019_skyp_webclient_replacement`
- **Created**: 2025-10-19
- **Objective**: Migrate `.skyp` file support from WebClient to HttpClient

## Pull Request
**PR**: #3656
**Merged**: 2025-10-22
**Status**: Merged to master
**TeamCity**: All tests passing

## Background

### What are .skyp files?
`.skyp` files are Skyline file packages - compressed archives containing Skyline documents and associated data files. They can be:
1. **Local files** - Created and opened from disk
2. **Web URLs** - Downloaded from remote servers (e.g., PeptideAtlas, tutorials)

### Current Implementation
- **Location**: `pwiz_tools/Skyline/Model/DocSettings/SkypSupport.cs`
- **WebClient usage**: Downloads `.skyp` files from URLs
- **Integration**: Used by `SkylineWindow` for opening remote documents

### Why Migrate?
- **Last WebClient usage** in core `Skyline.exe` (excluding PanoramaClient)
- **Consistency**: Should use same `HttpClientWithProgress` infrastructure as other downloads
- **User experience**: Should provide progress reporting and cancellation like other network operations
- **Error handling**: Should provide localized, user-friendly error messages

### Dependencies
This work builds directly on the WebClient migration foundation established in the `20251010_webclient_replacement` branch:
- `HttpClientWithProgress` infrastructure
- `HttpClientTestHelper` testing framework
- Standardized exception handling patterns
- Localized error messages in `MessageResources.resx`

## Task Checklist

### Phase 1: Analysis ✅
- [x] Read and understand `SkypSupport.cs`
  - [x] Identify all WebClient usage points
  - [x] Understand download flow and progress reporting
  - [x] Check error handling patterns
  - [x] Identify all callers in `SkylineWindow` and elsewhere

### Phase 2: Migration ✅
- [x] Replace WebClient with `HttpClientWithProgress`
  - [x] Update download method signatures if needed
  - [x] Integrate with existing progress reporting (likely `LongWaitDlg`)
  - [x] Ensure proper exception handling with `MapHttpException()`
  - [x] Maintain backward compatibility

### Phase 3: Testing ✅
- [x] Create test coverage in existing or new test file
  - [x] Test successful download of `.skyp` from URL
  - [x] Test network failure scenarios using `HttpClientTestHelper`
  - [x] Test user cancellation with `CancelClickedTestException`
  - [x] Test invalid URLs (HTTP 404, 500, etc.)
  - [x] Verify progress reporting works correctly

### Phase 4: Validation ✅
- [x] Manual testing
  - [x] Open `.skyp` file from web URL (e.g., tutorial)
  - [x] Test with WiFi off (should show network error)
  - [x] Test cancellation during download
  - [x] Verify error messages are user-friendly
- [x] Run full test suite locally in all locales
- [x] Verify TeamCity tests pass

## Tools & Scripts

### Finding .skyp Usage
```bash
# Find WebClient usage in SkypSupport.cs
rg "WebClient" pwiz_tools/Skyline/Model/DocSettings/SkypSupport.cs

# Find all .skyp file handling code
rg "\.skyp" pwiz_tools/Skyline/ --type cs

# Find callers of SkypSupport methods
rg "SkypSupport\." pwiz_tools/Skyline/ --type cs
```

### Testing Framework
- Use `HttpClientTestHelper` from `TestUtil/` for all network simulation
- Use `LongWaitDlg.CancelClickedTestException` for cancellation tests
- Follow DRY patterns established in `HttpClientWithProgressIntegrationTest`

## Risks & Considerations

### Low Risk Factors
- **Small scope**: Single file, likely 1-2 methods to change
- **Well-tested infrastructure**: `HttpClientWithProgress` is battle-tested
- **Existing patterns**: Can copy from `ToolStoreDlg` or `StartPage` download patterns
- **Good test coverage**: Comprehensive testing framework already in place

### Potential Issues
- **Caller integration**: May need to update `SkylineWindow` if signature changes
- **Progress reporting**: Ensure progress works for large `.skyp` files
- **Timeout handling**: `.skyp` files can be large (100s of MB), may need longer timeout

### Mitigation
- Keep method signatures compatible if possible
- Add timeout parameter to `HttpClientWithProgress` if needed for large files
- Test with both small and large `.skyp` files
- Follow established exception handling patterns

---

## Design Integrity & Encapsulation Notes

The migration to `HttpClientWithProgress` should maintain clear encapsulation boundaries
and reinforce separation of concerns:

- **Encapsulation Principle**: Exception translation and status-code extraction logic
  belong entirely within `HttpClientWithProgress`. External callers must not attempt
  to parse messages or reconstruct internal mapping rules.

- **Rationale**: Duplicating this logic elsewhere creates brittle dependencies on
  message formats, localizations, and private implementation details.
  Centralizing the mapping ensures uniform behavior and simplifies testing.

- **Law of Demeter / "Tell, Don’t Ask"**:
  Callers should ask `HttpClientWithProgress` or its returned exceptions for needed
  information (e.g., `StatusCode`, `RequestUri`), not probe exception chains manually.

- **Recommended Design Options**:
  1. Introduce a dedicated `NetworkRequestException` carrying `StatusCode` and `Uri`
     as optional properties.
  2. Alternatively, expose a public helper
     `HttpClientWithProgress.TryGetErrorStatusCode(Exception, out HttpStatusCode)`.

- **Review Guidance**:
  - Reject any new code that parses exception message strings to infer HTTP status.
  - Encourage contributors and AI agents to use established helper APIs
    rather than duplicating logic.

---

## Current Work: NetworkRequestException Refactoring

### Problem Identified
During initial implementation, `SkypSupport.GetErrorStatusCode()` was parsing exception message strings to extract HTTP status codes:
```csharp
// ANTI-PATTERN: Message parsing violates encapsulation
var message = httpRequestException.Message;
if (message.Contains("401"))
    return HttpStatusCode.Unauthorized;
```

This violates the "Design Integrity & Encapsulation Notes" principles above.

### Solution: NetworkRequestException
Implementing Option 1 from the design notes - a dedicated exception type that:
- **Extends IOException** for backward compatibility
- **Carries StatusCode property** (HttpStatusCode?) for structured access
- **Carries RequestUri property** (Uri) for context
- **Thrown by HttpClientWithProgress.MapHttpException()** for HTTP errors
- **Eliminates all message parsing** in external code

### Implementation Tasks - Phase 1 (Complete)
- [x] Identify encapsulation violation in SkypSupport.GetErrorStatusCode()
- [x] Add NetworkRequestException class to HttpClientWithProgress.cs
- [x] Update MapHttpException to throw NetworkRequestException for HTTP status errors
- [x] Add AddAuthorizationHeader() API to HttpClientWithProgress
- [x] Add automatic download size formatting to HttpClientWithProgress
- [x] Migrate WebDownloadClient to HttpDownloadClient using HttpClientWithProgress
- [x] Use FileSaver pattern for atomic file operations
- [x] Simplify exception handling - let exceptions propagate naturally
- [x] Update SkypTest to use HttpClientTestHelper and HttpStatusCode enum
- [x] Update WORKFLOW.md with AI agent build/test/ReSharper guidelines
- [x] Developer: Build, test, and verify SkypTest passes

### Implementation Tasks - Phase 2 (Cleanup - Post-Commit)

**Goal**: Remove unnecessary abstraction layers that don't add value now that WebClient is replaced.

#### Task 1: Remove DownloadClientCreator pattern
**Why**: Creator pattern was needed for WebClient's async/callback model. HttpClient is simpler - no creator needed.

**Reference**: See `ToolStoreDlg.cs` for the IToolStoreClient pattern with `TestToolStoreClient.INSTANCE`.

**Changes needed**:
- [ ] Make `IDownloadClient` extend `IDisposable`
- [ ] Add `public static TestDownloadClient INSTANCE { get; private set; }` to TestDownloadClient
- [ ] TestDownloadClient constructor: set `INSTANCE = this`
- [ ] TestDownloadClient.Dispose(): set `INSTANCE = null`
- [ ] In SkypSupport.Download(), replace:
  ```csharp
  var downloadClient = DownloadClientCreator.Create(progressMonitor, progressStatus);
  ```
  with:
  ```csharp
  using var downloadClient = TestDownloadClient.INSTANCE ??
      (IDownloadClient)new HttpDownloadClient(progressMonitor, progressStatus);
  ```
- [ ] Remove `DownloadClientCreator` class entirely
- [ ] Remove `SkypSupport.DownloadClientCreator` property
- [ ] Update tests to use `using var test = new TestDownloadClient(...)` pattern

#### Task 2: Simplify or remove SkypDownloadException
**Why**: After refactoring, SkypDownloadException is never thrown - it's just two static helper methods.

**Current state**: Only used for `GetErrorStatusCode()` and `GetMessage()` - no actual exceptions thrown.

**Decision**: Move helpers to SkypSupport, delete SkypDownloadException class.

**Changes needed**:
- [ ] Move `GetErrorStatusCode(Exception e)` to SkypSupport as private static method
- [ ] Move `GetMessage(SkypFile skyp, Exception ex, HttpStatusCode? statusCode)` to SkypSupport as private static method
- [ ] Update all call sites from `SkypDownloadException.GetErrorStatusCode()` to just `GetErrorStatusCode()`
- [ ] Delete entire `SkypDownloadException` class
- [ ] Delete helper methods like `Unauthorized()`, `Forbidden()` - inline the status code checks

#### Phase 2 Workflow
- [ ] Developer: Review and commit Phase 1 changes (this commit)
- [ ] Implement Task 1: Remove DownloadClientCreator
- [ ] Run SkypTest to verify functionality
- [ ] Implement Task 2: Simplify SkypDownloadException
- [ ] Run SkypTest to verify functionality
- [ ] Run ReSharper > Inspect > Code Issues in Solution
- [ ] Address any new warnings
- [ ] Commit Phase 2 cleanup

### Benefits
- **Type safety**: Use `ex.StatusCode` instead of parsing messages
- **Localization safe**: No dependency on English error text
- **Single source of truth**: Status code extraction only in HttpClientWithProgress
- **Law of Demeter**: Callers ask the exception, don't probe internals
- **Maintainable**: Changes to error messages won't break status code detection

---

## SkypSupport Migration Design

### Required HttpClientWithProgress API Additions

The `.skyp` download implementation needs two capabilities not yet in `HttpClientWithProgress`:

#### 1. Authorization Header Support
**Decision: `AddAuthorizationHeader(string authHeaderValue)`**
- More specific than generic `AddDefaultHeader()` (better encapsulation)
- Doesn't expose all request headers (security boundary)
- Doesn't dictate auth scheme (Basic, Bearer, etc.)
- Caller uses domain-specific helpers: `PanoramaServer.GetBasicAuthHeader(username, password)`

**Why not AddDefaultHeader?**
- Too broad - exposes implementation details
- Authorization is the primary use case for authenticated downloads
- More self-documenting API

#### 2. Download Progress with File Size
**Decision: Always append download size when `totalBytes > 0`**
- Format: `"{original message}\n\n{downloaded} / {total}"` (e.g., "5.2 MB / 10.4 MB")
- Uses `FileSizeFormatProvider` from `pwiz.Common.SystemUtil` (already available)
- Automatic - no flag needed (can add flag later if needed)
- Common need for file downloads - better default behavior

**Why automatic instead of flag/callback?**
- Encapsulation - HttpClientWithProgress owns progress formatting
- Simpler caller code - no callbacks to wire up
- Consistent behavior across all file downloads

### Testing Pattern (Established by Previous Migrations)

**Key Insight:** The established pattern from ToolStoreDlg/RInstaller migrations (SHA-1: c9a6a181):
- **Keep the interface** (`IDownloadClient`, `DownloadClientCreator`) for testability
- **Test implementation** provides SUCCESS path only (mock data from local files)
- **Failure testing** uses real production code + `HttpClientTestHelper`

**Why this pattern?**
- No network access in tests (success OR failure)
- Production code exercises real `HttpClientWithProgress` in tests
- `HttpClientTestHelper` intercepts at `HttpClientWithProgress` level to simulate failures
- Test interface stays simple - just copy local file for success case

**Example Structure:**
```csharp
// Production - uses real HttpClientWithProgress
public class HttpClientDownloadClient : IDownloadClient {
    public void Download(SkypFile skyp) {
        using var httpClient = new HttpClientWithProgress(progressMonitor, progressStatus);
        if (skyp.HasCredentials())
            httpClient.AddAuthorizationHeader(PanoramaServer.GetBasicAuthHeader(...));
        httpClient.DownloadFile(skyp.SkylineDocUri, skyp.DownloadPath);
    }
}

// Test - SUCCESS path only, copies local file
public class TestDownloadClient : IDownloadClient {
    private readonly string _srcPath;
    public void Download(SkypFile skyp) {
        File.Copy(_srcPath, skyp.DownloadPath); // Mock success
    }
}

// Test - FAILURES use HttpClientTestHelper with real production code
using (var helper = HttpClientTestHelper.SimulateHttp401()) {
    var skypSupport = new SkypSupport(SkylineWindow); // Uses real HttpClientDownloadClient
    errDlg = ShowDialog<AlertDlg>(() => skypSupport.Open(skypPath, existingServers));
    // Verify error message contains expected 401 error
}
```

**What NOT to do:**
- ❌ Don't make test interface return error codes or exceptions
- ❌ Don't create TestDownloadClientError401, TestDownloadClientError403, etc.
- ❌ Don't parse exception messages in tests - use `HttpClientTestHelper.GetExpectedMessage()`

### Implementation Steps
1. Add `AddAuthorizationHeader(string authHeaderValue)` to HttpClientWithProgress
2. Add automatic download size formatting to `DownloadFromStream()` in HttpClientWithProgress
3. Migrate `WebDownloadClient` → `HttpClientDownloadClient` in SkypSupport.cs
4. Update `TestDownloadClient` to SUCCESS-only pattern (remove ERROR401/ERROR403 constants)
5. Update SkypTest to use `HttpClientTestHelper` for failure scenarios
6. Apply `NetworkRequestException` fix to `GetErrorStatusCode()` method

---

## Success Criteria ✅

### Functional ✅
- [x] `.skyp` files download successfully from web URLs
- [x] Progress reporting works correctly during download
- [x] User can cancel download mid-stream
- [x] Network errors show user-friendly, localized messages
- [x] HTTP status errors (404, 500) handled gracefully
- [x] **No message-based parsing logic outside `HttpClientWithProgress`**

### Testing ✅
- [x] Test coverage for all network failure scenarios
- [x] Test coverage for cancellation
- [x] Test coverage for successful download
- [x] All tests pass locally in 5 locales (en, zh-CHS, ja, tr, fr)
- [x] TeamCity tests pass

### Code Quality ✅
- [x] Follows established `HttpClientWithProgress` patterns
- [x] Uses `HttpClientTestHelper` for all test simulations
- [x] Maintains DRY principles in test code
- [x] Includes XML documentation
- [x] Preserves encapsulation boundaries; no external code re-implements internal exception mapping
- [x] No regressions in existing `.skyp` functionality

### No WebClient Remaining ✅
- [x] `SkypSupport.cs` has zero WebClient usage
- [x] Core `Skyline.exe` has zero WebClient usage (excluding PanoramaClient)
- [x] Ready to add `CodeInspectionTest` for WebClient prohibition (after PanoramaClient migration)

## Estimated Effort

**Time**: 2-4 hours  
**Complexity**: Low  
**Dependencies**: None (foundation already complete)

This is an ideal "quick win" branch - small scope, well-tested infrastructure, clear patterns to follow.

## Handoff Prompt for Branch Creation

```
I want to start work on migrating .skyp file support from WebClient to HttpClient,
as described in todos/backlog/TODO-skyp_webclient_replacement.md.

Please:
1. Create branch: Skyline/work/YYYYMMDD_skyp_webclient_replacement (use today's date)
2. Move todos/backlog/TODO-skyp_webclient_replacement.md to todos/active/
3. Rename the file to include today's date: TODO-YYYYMMDD_skyp_webclient_replacement.md
4. Update the TODO file header with actual branch information
5. Commit the TODO file to the new branch
6. Begin Phase 1: Analysis by reading SkypSupport.cs

The TODO file contains full context. Let's make core Skyline.exe WebClient-free!
```

---

## ✅ Completion Summary

**Branch**: `Skyline/work/20251019_skyp_webclient_replacement`
**PR**: #3656
**Merged**: 2025-10-22
**TeamCity Results**: All tests passing in all locales (en, zh-CHS, ja, tr, fr)

### What Was Actually Done

**Phase 1: Core Migration**
- Added `NetworkRequestException` to `HttpClientWithProgress` for structured HTTP error handling
- Added `AddAuthorizationHeader()` API to `HttpClientWithProgress` for authenticated downloads
- Added automatic download size formatting to `HttpClientWithProgress` progress messages
- Migrated `SkypSupport.cs` from `WebClient` to `HttpClientWithProgress`
- Implemented `HttpDownloadClient` using `HttpClientWithProgress` and `FileSaver` pattern
- Updated `SkypTest.cs` to use `HttpClientTestHelper` for network failure testing
- Eliminated all message-parsing anti-patterns using `NetworkRequestException.StatusCode` property

**Phase 2: Code Cleanup**
- Replaced `DownloadClientCreator` pattern with `Func<>` dependency injection using named factory functions
- Moved `SkypDownloadException` helper methods to `SkypSupport` as private static methods
- Deleted `SkypDownloadException` class (~150 lines removed)
- Simplified dependency injection - no static state, no IDisposable complexity

**Documentation Improvements**
- Updated `WORKFLOW.md` with AI agent build/test/ReSharper guidelines
- Created comprehensive `TESTING.md` consolidating all testing documentation:
  - Test project structure (Test, TestData, TestFunctional, TestConnected, TestTutorial, TestPerf)
  - Test execution tools (TestRunner, SkylineTester, SkylineNightly, SkylineNightlyShim)
  - Dependency injection patterns for testing (constructor injection vs static+IDisposable)
  - AssertEx assertion library documentation
  - HttpClientTestHelper usage patterns
  - Translation-proof testing practices
  - Test performance optimization guidelines
- Updated `STYLEGUIDE.md` to reference `TESTING.md`

### Key Files Modified
- `pwiz_tools/Skyline/Common/SystemUtil/HttpClientWithProgress.cs` - Added NetworkRequestException, AddAuthorizationHeader, download size formatting
- `pwiz_tools/Skyline/FileUI/SkypSupport.cs` - Migrated to HttpClient, simplified dependency injection, removed SkypDownloadException
- `pwiz_tools/Skyline/FileUI/SkypFile.cs` - Added DownloadTempPath and SafePath properties for FileSaver pattern
- `pwiz_tools/Skyline/TestFunctional/SkypTest.cs` - Updated to use HttpClientTestHelper and HttpStatusCode enum
- `WORKFLOW.md` - Added AI agent build/test guidelines
- `TESTING.md` - New comprehensive testing documentation (created)
- `STYLEGUIDE.md` - Updated to reference TESTING.md

### Design Decisions

**NetworkRequestException**: Chose dedicated exception type (Option 1 from design notes) over helper method to provide structured access to HTTP status codes and request URIs, eliminating brittle message parsing.

**Dependency Injection Pattern**: Used `Func<>` factory with named static functions instead of creator pattern. This provides clean dependency injection without static mutable state or IDisposable complexity. Named functions (vs lambdas) improve debuggability and discoverability.

**Testing Pattern**: Maintained interface-based testing with SUCCESS-only test implementations. Failures tested via real production code + `HttpClientTestHelper` intercepting at HttpClient level.

**Static+IDisposable Pattern**: Attempted to refactor `ToolStoreUtil.ToolStoreClient` away from static+IDisposable pattern, but discovered this pattern is **appropriate for deep call stack testing** where tests need to intercept at high-level entry points (`SkylineWindow` methods) without polluting every layer with test parameters. Documented this pattern in `TESTING.md` as Pattern 2.

### Unexpected Findings

1. **Call stack depth determines testing pattern**: Shallow call stacks (SkypSupport) benefit from constructor injection. Deep call stacks (ToolStore, HttpClient) benefit from static+IDisposable pattern. Not all static mutable state is bad - it's a legitimate testing pattern when used correctly.

2. **AssertEx underutilized**: Found custom `AssertErrorContains()` wrapper that just calls `AssertEx.Contains()`. Many developers may not be aware of the full `AssertEx` API (FileExists, ThrowsException, Serializable, NoDiff, etc.). Documented comprehensively in `TESTING.md`.

3. **Test project architecture not documented**: No central documentation existed for test project structure (Test, TestData, TestFunctional, TestConnected, TestTutorial, TestPerf) or test execution tools (TestRunner, SkylineTester, SkylineNightly, SkylineNightlyShim). Created `TESTING.md` to fill this gap.

4. **AbstractFunctionalTestEx not clearly distinguished**: Most tests should use `AbstractFunctionalTestEx` for high-level workflow helpers (ImportResultsFile, ShareDocument, etc.) but this wasn't clearly documented. Updated `TESTING.md` to explain the difference from `AbstractFunctionalTest`.

### Benefits Achieved

**For Users:**
- Consistent progress reporting with download size for .skyp files
- Cancellation support during .skyp downloads
- User-friendly, localized error messages
- Authenticated Panorama .skyp downloads now supported

**For Developers:**
- No more WebClient in core Skyline.exe (except PanoramaClient)
- Structured exception handling eliminates message parsing
- Comprehensive testing documentation in one place
- Clear guidelines on dependency injection patterns
- AI agent workflow guidelines prevent build/test confusion

**For Code Quality:**
- Type-safe HTTP error handling via `NetworkRequestException`
- Eliminated ~150 lines of duplicate code
- Simplified dependency injection (no creator pattern overhead)
- Translation-proof testing patterns documented
- Encapsulation boundaries preserved

### Follow-up Work Created
None - all planned work completed.

---

## Notes

This is the **last WebClient usage in core Skyline.exe** (excluding PanoramaClient, which is deferred to a future branch). Completing this work will mean that all user-facing download operations in the main application use the modern `HttpClientWithProgress` infrastructure with consistent error handling, progress reporting, and cancellation.

After this branch, the only remaining WebClient usages will be:
1. **PanoramaClient** (complex, deserves dedicated branch)
2. **Auxiliary tools** (separate executables, lower priority)

