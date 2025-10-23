# TODO-20251023_panorama_webclient_replacement.md

## Branch Information
- **Branch**: `Skyline/work/20251023_panorama_webclient_replacement`
- **Created**: 2025-10-23
- **Objective**: Migrate PanoramaClient from WebClient to HttpClient

## Background
This is a **focused Phase 2** branch specifically for PanoramaClient migration. This is separate from the tools migration (`TODO-tools_webclient_replacement.md`) because:

- **Size and complexity**: PanoramaClient is a significant, complex component
- **High value**: Core Panorama integration used throughout Skyline.exe
- **Authentication**: Handles server authentication and session management
- **File uploads**: Large file uploads with progress reporting
- **API calls**: Multiple Panorama API endpoints
- **Deserves dedicated focus**: Testing and validation require careful attention

## Prerequisites
- ✅ Phase 1 complete: Core Skyline.exe WebClient → HttpClient migration merged to master
- ✅ `HttpClientWithProgress` established and tested
- ✅ Exception handling patterns standardized
- ✅ Testing infrastructure (`HttpClientTestHelper`) available

## Architectural Analysis (2025-10-23)

### ✅ Key Findings: Better Than Expected!

**Architecture is well-designed** - Built by junior dev with senior oversight (developer who built `.skyp` support and has deep HTTP/Panorama experience).

**Three-Layer Architecture:**
```
Skyline.exe / SkylineBatch / AutoQC (different UI contexts)
    ↓
WebPanoramaPublishClient (Skyline/Util/) - Skyline-specific wrapper
    ↓
WebPanoramaClient (Shared/PanoramaClient/) - SHARED across all solutions
    ↓ (uses)
LabkeySessionWebClient - Extends WebClient with cookies/CSRF
```

**Critical Discovery: IProgressMonitor Already Integrated!**
- `IPanoramaClient` interface already accepts `IProgressMonitor` and `IProgressStatus`
- No API changes needed - internal implementation only
- `DownloadFile()` and `SendZipFile()` already have progress parameters

**WebClient Usage (5 locations in Shared/PanoramaClient/):**
1. `WebPanoramaClient.ValidateUri()` - Server validation
2. `WebPanoramaClient.DownloadFile()` - File downloads with progress
3. `WebPanoramaClient` methods via `LabkeySessionWebClient`
4. `LabkeySessionWebClient` - Cookie/session management + CSRF tokens
5. `NonStreamBufferingWebClient` - Variant for large uploads

**SharedBatch Relationship:**
- SharedBatch has simplified copy of `LongWaitDlg` for console apps
- Uses same `IProgressMonitor` interface
- Lighter weight, suitable for AutoQC/SkylineBatch
- **No changes needed to SharedBatch** - it just consumes the Shared/PanoramaClient API

### Migration Complexity: Moderate (Not High!)

**Why manageable:**
- ✅ IProgressMonitor already integrated
- ✅ Well-defined interfaces (`IPanoramaClient`, `IRequestHelper`)
- ✅ Shared project location (one codebase)
- ✅ Existing test infrastructure

**Key challenges:**
- ⚠️ Cookie/session management (`CookieContainer`)
- ⚠️ CSRF token handling
- ⚠️ Async uploads with progress events
- ⚠️ Multi-solution testing (Skyline, AutoQC, SkylineBatch)

### Migration Strategy

**Phase 1: Extend HttpClientWithProgress**
- Add cookie container support (use `HttpClientHandler.CookieContainer`)
- Verify custom header support (already has `AddAuthorizationHeader()`)
- Add streaming upload with progress callback support

**Phase 2: Migrate WebPanoramaClient Methods (One at a Time)**
1. Start: `ValidateUri()` - Simple GET request
2. Next: `DownloadFile()` - Download with progress
3. Then: `SendZipFile()` - Upload with progress (most complex)
4. Finally: Authentication/validation methods

**Phase 3: Migrate RequestHelper**
- `PanoramaRequestHelper` wraps `LabkeySessionWebClient`
- Needs same cookie/CSRF support

**Phase 4: Multi-Solution Testing**
- Test in Skyline.exe (WinForms `LongWaitDlg`)
- Test in AutoQC (SharedBatch console `LongWaitDlg`)
- Test in SkylineBatch (SharedBatch console `LongWaitDlg`)

### Estimated Effort: 1-2 weeks

## Task Checklist

### Phase 1: Analysis & Planning ✅
- [x] Read and understand architecture layers
- [x] Identify all WebClient usage patterns:
  - [x] Authentication flows (session cookies, CSRF tokens)
  - [x] File upload mechanisms (`UploadFileAsync` with progress events)
  - [x] API endpoint calls (GET, POST via `RequestHelper`)
  - [x] Error handling patterns (`PanoramaServerException`, `LabKeyError`)
  - [x] Progress reporting (`IProgressMonitor` already integrated)
- [x] Document current behavior and edge cases
- [x] Detailed code analysis of `WebPanoramaClient` implementation
- [x] Identify test coverage
- [x] Finalize migration strategy

## Detailed Implementation Analysis

### WebClient Usage Locations

**1. `WebPanoramaClient.ValidateUri()` (lines 584-631)**
- Simple `WebClient.DownloadString()` for server validation
- Tries HTTP/HTTPS protocol switching
- Migration: Use `HttpClientWithProgress.DownloadStringAsync()`

**2. `WebPanoramaClient.DownloadFile()` (lines 773-817)**
- Uses `LabkeySessionWebClient.DownloadFileAsync()` 
- Progress events: `DownloadProgressChanged`, `DownloadFileCompleted`
- Polling loop with `Thread.Sleep(100)` checking `IsCanceled`
- Formats progress: "Downloading {name}\n\n{downloaded} / {total}"
- Migration: Use `HttpClientWithProgress.DownloadFileAsync()` (already has progress support)

**3. `WebPanoramaClient.DownloadStringAsync()` (lines 838-864)**
- Uses `LabkeySessionWebClient.DownloadStringAsync()`
- Polling loop with `CancellationToken`
- Migration: Replace with `HttpClientWithProgress.DownloadStringAsync()`

**4. `LabkeySessionWebClient` - Cookie/CSRF Management (PanoramaUtil.cs lines 645-748)**
- **Cookies:** `CookieContainer` attached in `GetWebRequest()` override
- **CSRF Token:** 
  - Retrieved from response cookie `X-LABKEY-CSRF` after first request
  - Added as header `X-LABKEY-CSRF` on all POST requests
  - Handles redirect edge case (302) where token appears in request cookies
  - `GetCsrfTokenFromServer()` - explicit call to ensure token present
- **Authentication:** Basic auth header added if username/password provided
- Migration: Need `HttpClientHandler` with `CookieContainer` support

**5. `NonStreamBufferingWebClient` (PanoramaUtil.cs lines 750-784)**
- Extends `LabkeySessionWebClient`
- Sets `AllowWriteStreamBuffering = false` for large uploads
- Sets `Timeout = Timeout.Infinite`
- Migration: Use `HttpClient.Timeout = Timeout.InfiniteTimeSpan` + streaming upload

**6. `PanoramaRequestHelper` - API Calls (RequestHelper.cs lines 196-310)**
- Wraps `LabkeySessionWebClient` for GET/POST operations
- Methods: `DoGet()`, `DoPost()`, `AsyncUploadFile()`
- Calls `GetCsrfTokenFromServer()` before POST operations
- Handles `UploadFileAsync` with progress events
- Migration: Create `HttpPanoramaRequestHelper` using `HttpClientWithProgress`

### Test Coverage Assessment

**Existing Tests:**
1. **`TestPanoramaClient.cs`** - UI testing (PanoramaFilePicker, PanoramaDirectoryPicker)
   - Tests navigation, JSON parsing, folder browsing
   - Uses `TestClientJson` (mock, no network)
   
2. **`PanoramaClientPublishTest.cs`** - Publish workflow testing
   - Tests document upload, folder validation, error handling
   - Uses mock implementations (`TestLabKeyErrorPanoramaClient`)
   - **No actual network operations tested**

3. **`PanoramaClientDownloadTest.cs`** (TestConnected)
   - Tests with **real Panorama server** (panoramaweb.org)
   - Requires network connection
   - Tests file downloads, authentication

**Test Coverage Gaps:**
- ❌ No tests for cookie/session management
- ❌ No tests for CSRF token retrieval and usage
- ❌ No tests for upload progress reporting
- ❌ No tests for download cancellation
- ❌ No tests for network failures (would break with HttpClientTestHelper)
- ❌ No tests for HTTP error status codes (401, 403, 404, 500)
- ✅ Good coverage of API response parsing and error handling

### Migration Approach

**Phase 2A: Extend HttpClientWithProgress**
1. Add `HttpClientHandler` configuration support:
   ```csharp
   public void ConfigureHandler(Action<HttpClientHandler> configureHandler)
   {
       // Allow caller to configure CookieContainer, credentials, etc.
   }
   ```

2. Add header management methods (already has `AddAuthorizationHeader()`):
   ```csharp
   public void AddHeader(string name, string value)  // For CSRF token
   ```

3. Add streaming upload with progress:
   ```csharp
   public Task<HttpResponseMessage> UploadFileAsync(Uri uri, string method, string filePath,
       IProgress<UploadProgress> progress, CancellationToken cancellationToken)
   ```

**Phase 2B: Create HttpPanoramaClient**
- Replace `LabkeySessionWebClient` with `HttpClientWithProgress` wrapper
- Implement cookie container and CSRF token management
- Maintain same public API (`IPanoramaClient` interface)
- Use existing `IProgressMonitor` parameters

**Phase 2C: Migrate Methods One-by-One**
1. Start: `ValidateUri()` - Simple GET, no sessions
2. Next: Authentication methods - Cookie handling, CSRF
3. Then: `DownloadFile()` - Progress reporting
4. Then: `SendZipFile()` - Upload with progress (most complex)
5. Finally: `DownloadStringAsync()` and other helpers

**Phase 3: Update RequestHelper**
- Create `HttpPanoramaRequestHelper` class
- Replace `PanoramaRequestHelper` usage
- Maintain `IRequestHelper` interface

**Phase 4: Comprehensive Testing**
1. Unit tests with `HttpClientTestHelper`:
   - Network failures
   - HTTP status codes (401, 403, 404, 500)
   - CSRF token handling
   - Cookie persistence
   - Cancellation scenarios
   
2. Integration tests:
   - Mock Panorama server responses
   - Upload/download with progress
   - Session management across multiple requests
   
3. Multi-solution smoke tests:
   - Skyline.exe publish workflow
   - AutoQC Panorama integration
   - SkylineBatch Panorama operations

### Key Decisions

**Cookie Management:**
- Use `HttpClientHandler.CookieContainer` - standard HttpClient pattern
- Single `CookieContainer` instance per client session
- Automatic cookie persistence across requests

**CSRF Token:**
- Retrieve from `Set-Cookie` header after first request
- Store in client instance
- Add as custom header on all POST requests
- Implement `GetCsrfTokenFromServer()` helper

**Progress Reporting:**
- `DownloadFile()` - Use existing `HttpClientWithProgress` progress
- `SendZipFile()` - Need streaming upload with progress callback
- Maintain `IProgressMonitor.UpdateProgress()` pattern

**Async Pattern:**
- Use `HttpClient.SendAsync()` with `await` internally
- Expose synchronous API to callers (existing pattern)
- Use polling loop for cancellation (matches existing code)

### Risks & Mitigation

**Risk: CSRF token redirect edge case**
- Current code handles 302 redirect where token appears in request cookies
- Mitigation: Test redirect scenarios thoroughly, maintain same logic

**Risk: Breaking existing workflows**
- Many parts of Skyline depend on PanoramaClient
- Mitigation: Maintain exact same public API, incremental migration, comprehensive testing

**Risk: Multi-solution testing**
- Must work in Skyline, AutoQC, and SkylineBatch
- Mitigation: Test in all three contexts before merging

**Risk: Performance regression**
- WebClient async operations are well-optimized
- Mitigation: Benchmark upload/download speeds, ensure no slowdown

### Phase 2: HttpClient Migration

#### Phase 2A: Extend HttpClientWithProgress ✅ COMPLETE
- [x] Add cookie container support via constructor parameter
  - Optional `CookieContainer` parameter for session management
  - Configures `HttpClientHandler.CookieContainer` and `UseCookies`
  - Follows same pattern as existing `IProgressStatus` parameter
- [x] Add generic header and cookie support (no Panorama-specific knowledge)
  - `AddHeader(string name, string value)` - Generic custom header support
  - `GetCookie(Uri uri, string cookieName)` - Generic cookie retrieval
  - Follows same specific API pattern as `AddAuthorizationHeader()`
  - **LabKey/Panorama specifics stay in PanoramaClient** (e.g., "X-LABKEY-CSRF")
- [x] Add file upload with progress reporting
  - `UploadFile(Uri uri, string method, string fileName)` - Upload file with progress
  - `UploadFromStream()` - Private helper, uses same `WithExceptionHandling` pattern as downloads
  - Progress reported via `IProgressMonitor` (consistent with download pattern)
  - Supports cancellation via `CancellationToken`
  - Uses `ProgressStream` wrapper to track upload progress
  - **Uses `WithExceptionHandling()` and `MapHttpException()` for consistent error handling**
- [x] Fix exception handling to match existing patterns
  - All upload code now uses `WithExceptionHandling()` wrapper
  - Maps exceptions to user-friendly messages via `MapHttpException()`
  - Supports `TestBehavior.FailureException` for testing
- [x] **DRY refactoring: Extract common transfer logic**
  - Created `TransferStreamWithProgress(inputStream, outputStream, totalBytes, uri)`
  - Single implementation of chunked reading with progress, cancellation, timeout
  - Used by both `DownloadFromStream()` and `UploadFromStream()`
  - Eliminates ~40 lines of duplicate code
  - Guarantees identical behavior for upload/download
  - Single place to update chunking, timeout, cancellation logic
  - **Classic DRY win for long-term maintenance** (see MEMORY.md)
- [x] **Add tests to HttpClientWithProgressIntegrationTest** ✅ 17 new tests added
  - Network failure tests (6): DNS, connection, timeout, connection loss, no network, cancellation
  - HTTP status tests (6): 401, 403, 404, 500, 429, generic (503)
  - Success tests (2): UploadFile, UploadData
  - Progress tests (2): Verify chunked upload with progress reporting, regression test for repeated size bug
  - Cancellation test (1): User clicks Cancel button during upload
  - **All tests mirror download pattern** - same helpers, same assertions
- [x] **Extract progress message building to public static method**
  - Created `GetProgressMessageWithSize(baseMessage, transferred, total)` - public static
  - Used by `GetProgressMessage()` for both download and upload
  - Testable: Added `TestProgressMessageDoesNotRepeatSize()` regression test
  - **Prevents original bug** where size was appended repeatedly (e.g., "1KB\n\n2KB" instead of "2KB")
  - Makes progress message format consistent and testable
- [x] **Improve upload testing to validate data integrity**
  - Changed `IHttpClientTestBehavior.SimulateUploadSuccess` → `GetMockUploadStream(Uri uri)`
  - Symmetric with `GetMockResponseStream()` for downloads
  - Upload tests now capture uploaded data and verify it matches source
  - Tests validate: content correctness, byte-for-byte accuracy, special characters, binary data
  - Much more robust than just "no exception" testing
- [x] **Add comprehensive UTF-8 encoding validation**
  - Updated all success tests (download and upload) with UTF-8 multi-byte characters
  - Tests include: Latin extended (café), Greek (αβγδ), Cyrillic (Москва), CJK (中文, 日本語), emoji (🔬🧬📊)
  - Validates 1-byte, 2-byte, 3-byte, and 4-byte UTF-8 sequences
  - Ensures proper encoding/decoding for international data (critical for PanoramaClient)
  - Round-trip validation: string → bytes → string for upload tests
- [x] Verify no linting errors
- [x] Verify all upload tests added to DoTest() method

**Key Design Decisions:**
- Cookie container as optional constructor parameter (not exposed property)
- Generic methods (`AddHeader`, `GetCookie`) keep HttpClientWithProgress low-level and reusable
- No LabKey/Panorama-specific knowledge in HttpClientWithProgress
- PanoramaClient will call `AddHeader("X-LABKEY-CSRF", token)` and `GetCookie(uri, "X-LABKEY-CSRF")`
- Follows established pattern from `AddAuthorizationHeader()`
- Maintains encapsulation - no direct header/cookie collection exposure
- **Upload uses same exception handling pattern as downloads** (`WithExceptionHandling`, `MapHttpException`)
- Upload progress uses same pattern and helper methods as downloads (`FormatDownloadSize`)
- All methods are synchronous (blocking) with progress callbacks - matches existing pattern

**Methods Added:**
- Constructor: `CookieContainer` parameter
- Headers: `AddHeader(name, value)`, `GetCookie(uri, cookieName)`
- Upload: `UploadFile(uri, method, fileName)`, private `UploadFromStream(uri, method, stream, fileName)`

**Note:** `UploadString()` and `UploadValues()` already exist from previous migrations - no need to add POST methods.

#### Phase 2B: Migrate WebPanoramaClient (Shared/PanoramaClient/)
- [ ] Replace `LabkeySessionWebClient` (extends WebClient) with HttpClientWithProgress
- [ ] Migrate `WebPanoramaClient.ValidateUri()` - Server validation
- [ ] Migrate `WebPanoramaClient.DownloadFile()` - File downloads with progress
- [ ] Migrate `WebPanoramaClient.SendZipFile()` - File uploads with progress (most complex)
- [ ] Migrate `LabkeySessionWebClient` cookie/CSRF management
- [ ] Migrate `PanoramaRequestHelper` to use HttpClientWithProgress
- [ ] Ensure proper resource disposal (IDisposable patterns)
- [ ] Update error handling to use `MapHttpException`
- [ ] Maintain backward compatibility with existing callers

**Multi-Solution Impact:**
This migration affects:
- Skyline.exe (full WinForms UI)
- AutoQC (console app, uses SharedBatch)
- SkylineBatch (console app, uses SharedBatch)

All use `Shared/PanoramaClient` - one migration serves all three solutions.

### Phase 3: Testing
- [ ] Create comprehensive tests using `HttpClientTestHelper`
- [ ] Test authentication flows (success, failure, timeout)
- [ ] Test file uploads (small files, large files, cancellation, network failure)
- [ ] Test API calls (all endpoints, error responses)
- [ ] Test progress reporting accuracy
- [ ] Test cancellation at various stages
- [ ] Integration tests with mock Panorama server
- [ ] Consider tests with actual Panorama test server if available

### Phase 4: Code Inspection Test
- [ ] Add prohibition to `CodeInspectionTest` for WebClient usage
- [ ] Add prohibition to `CodeInspectionTest` for WebBrowser usage (if not already present)
- [ ] Verify test passes (no WebClient/WebBrowser in Skyline.exe code)
- [ ] Document the inspection rules

**Code to add to CodeInspectionTest.cs**:
```csharp
// After PanoramaClient migration is complete, prohibit WebClient usage
[TestMethod]
public void TestNoWebClient()
{
    // WebClient is deprecated - use HttpClient with HttpClientWithProgress
    // See MEMORY.md for async patterns and HTTP client usage
    AssertEx.NoOccurencesInSources(
        "new WebClient()",
        SkylineDirectory,
        new[] { "*.cs" },
        new[] { 
            "Executables",        // Tools have separate migration (TODO-tools_webclient_replacement.md)
            "SkylineNightly",     // Build tools deferred
            "SkylineNightlyShim"  // Build tools deferred
        });
}

[TestMethod]
public void TestNoWebBrowser()
{
    // WebBrowser control should not be used - prefer modern alternatives
    AssertEx.NoOccurencesInSources(
        "new WebBrowser()",
        SkylineDirectory,
        new[] { "*.cs" },
        new[] { 
            "Executables"  // May have legitimate uses in tools
        });
}
```

### Phase 5: Documentation & Cleanup
- [ ] Update any documentation referencing WebClient
- [ ] Ensure MEMORY.md includes PanoramaClient migration notes if relevant
- [ ] Remove TODO file before merging to master

## Key Files
- **`pwiz_tools/Skyline/Util/PanoramaPublishUtil.cs`** - Contains `WebPanoramaPublishClient`
- **`pwiz_tools/Skyline/TestFunctional/PanoramaClientPublishTest.cs`** - Existing tests
- **`pwiz_tools/Skyline/TestConnected/PanoramaClientDownloadTest.cs`** - Connected tests
- **`pwiz_tools/Skyline/TestFunctional/TestPanoramaClient.cs`** - Test utilities

## Risks & Considerations

### Authentication Complexity
- Panorama uses session-based authentication
- Cookie handling must be preserved
- Timeout and retry logic must be robust

### Large File Uploads
- Progress reporting must be accurate
- Cancellation must work at any point
- Memory efficiency for large files
- Resume capability consideration

### Backward Compatibility
- Many parts of Skyline depend on PanoramaClient
- Publishing workflows must not break
- Error messages must remain user-friendly

### Testing Challenges
- Requires Panorama server access (or sophisticated mocking)
- Network failures must be simulated comprehensively
- Authentication edge cases are hard to test

## Success Criteria
- All WebClient usage in PanoramaClient replaced with HttpClient
- All existing Panorama functionality works correctly
- Comprehensive test coverage with `HttpClientTestHelper`
- Code inspection test passes (no WebClient in core Skyline code)
- User-friendly error messages preserved
- Progress reporting works accurately
- Cancellation works reliably
- No regressions in Panorama publishing workflows

## Out of Scope
- Tools migration (see `TODO-tools_webclient_replacement.md`)
- Panorama API changes or new features
- Performance optimization beyond WebClient → HttpClient

## References
- Phase 1 branch: `Skyline/work/20251010_webclient_replacement`
- `HttpClientWithProgress.cs` - Core wrapper established in Phase 1
- `HttpClientTestHelper.cs` - Testing infrastructure from Phase 1
- MEMORY.md - Async patterns and project context
- Panorama documentation: https://panoramaweb.org/

## Handoff Prompt for Branch Creation

```
I want to start work on PanoramaClient WebClient → HttpClient migration from TODO-panorama_webclient_replacement.md.

This is a focused Phase 2 branch for PanoramaClient only. Core Skyline.exe and tools are separate.

Please:
1. Create branch: Skyline/work/YYYYMMDD_panorama_webclient_replacement (use today's date)
2. Rename TODO file to include the date
3. Update the TODO file header with actual branch information
4. Begin Phase 1: Analysis & Planning

Key context: PanoramaClient is complex - handles authentication, file uploads, API calls. Requires careful testing with mock or real Panorama server. Will add code inspection test to prohibit WebClient once complete.

The TODO file contains full context. Let's start by analyzing WebPanoramaPublishClient.
```
