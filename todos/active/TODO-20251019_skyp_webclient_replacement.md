# TODO-20251019_skyp_webclient_replacement.md

## Branch Information
- **Branch**: `Skyline/work/20251019_skyp_webclient_replacement`
- **Created**: 2025-10-19
- **Objective**: Migrate `.skyp` file support from WebClient to HttpClient

## Pull Request
**PR**: #3656
**Status**: Open (TeamCity tests running)
**Updated**: Merged latest master into PR branch (2025-10-22)

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

### Phase 1: Analysis
- [ ] Read and understand `SkypSupport.cs`
  - [ ] Identify all WebClient usage points
  - [ ] Understand download flow and progress reporting
  - [ ] Check error handling patterns
  - [ ] Identify all callers in `SkylineWindow` and elsewhere

### Phase 2: Migration
- [ ] Replace WebClient with `HttpClientWithProgress`
  - [ ] Update download method signatures if needed
  - [ ] Integrate with existing progress reporting (likely `LongWaitDlg`)
  - [ ] Ensure proper exception handling with `MapHttpException()`
  - [ ] Maintain backward compatibility

### Phase 3: Testing
- [ ] Create test coverage in existing or new test file
  - [ ] Test successful download of `.skyp` from URL
  - [ ] Test network failure scenarios using `HttpClientTestHelper`
  - [ ] Test user cancellation with `CancelClickedTestException`
  - [ ] Test invalid URLs (HTTP 404, 500, etc.)
  - [ ] Verify progress reporting works correctly

### Phase 4: Validation
- [ ] Manual testing
  - [ ] Open `.skyp` file from web URL (e.g., tutorial)
  - [ ] Test with WiFi off (should show network error)
  - [ ] Test cancellation during download
  - [ ] Verify error messages are user-friendly
- [ ] Run full test suite locally in all locales
- [ ] Verify TeamCity tests pass

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

## Success Criteria

### Functional
- [ ] `.skyp` files download successfully from web URLs
- [ ] Progress reporting works correctly during download
- [ ] User can cancel download mid-stream
- [ ] Network errors show user-friendly, localized messages
- [ ] HTTP status errors (404, 500) handled gracefully
- [ ] **No message-based parsing logic outside `HttpClientWithProgress`**

### Testing
- [ ] Test coverage for all network failure scenarios
- [ ] Test coverage for cancellation
- [ ] Test coverage for successful download
- [ ] All tests pass locally in 5 locales (en, zh-CHS, ja, tr, fr)
- [ ] TeamCity tests pass

### Code Quality
- [ ] Follows established `HttpClientWithProgress` patterns
- [ ] Uses `HttpClientTestHelper` for all test simulations
- [ ] Maintains DRY principles in test code
- [ ] Includes XML documentation
- [ ] Preserves encapsulation boundaries; no external code re-implements internal exception mapping
- [ ] No regressions in existing `.skyp` functionality

### No WebClient Remaining
- [ ] `SkypSupport.cs` has zero WebClient usage
- [ ] Core `Skyline.exe` has zero WebClient usage (excluding PanoramaClient)
- [ ] Ready to add `CodeInspectionTest` for WebClient prohibition (after PanoramaClient migration)

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

## Notes

This is the **last WebClient usage in core Skyline.exe** (excluding PanoramaClient, which is deferred to a future branch). Completing this work will mean that all user-facing download operations in the main application use the modern `HttpClientWithProgress` infrastructure with consistent error handling, progress reporting, and cancellation.

After this branch, the only remaining WebClient usages will be:
1. **PanoramaClient** (complex, deserves dedicated branch)
2. **Auxiliary tools** (separate executables, lower priority)

