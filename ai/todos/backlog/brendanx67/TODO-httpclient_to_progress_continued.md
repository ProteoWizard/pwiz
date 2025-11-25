# TODO-httpclient_to_progress_continued.md

## Branch Information
- **Branch**: `Skyline/work/YYYYMMDD_httpclient_to_progress_continued` (to be created when work starts)
- **Status**: Backlog
- **Objective**: Continue consolidating Skyline HTTP usage onto `HttpClientWithProgress`, migrating remaining `HttpClient` and `HttpWebRequest` usage in Ardia integration and other areas

## Background

This TODO continues the work from `TODO-20251107_httpclient_to_progress.md`, which successfully migrated `WebEnabledFastaImporter` to use `HttpClientWithProgress` with comprehensive HTTP recording/playback infrastructure. This follow-up work focuses on:

- **Ardia Integration**: Migrating `EditRemoteAccountDlg` and `ArdiaLoginDlg` from bare `HttpClient` to `HttpClientWithProgress`
- **HttpWebRequest Retirement**: Replacing remaining `HttpWebRequest` usage throughout the codebase
- **Code Inspection**: Adding automated detection of bare `HttpClient` and `HttpWebRequest` usage
- **Connected Tests Organization**: Comprehensive review and proper organization of tests requiring network access

## Prerequisites
- ✅ `HttpClientWithProgress` complete with upload/download/cookie/header support
- ✅ Comprehensive testing infrastructure (`HttpClientTestHelper`) with recording/playback
- ✅ PanoramaClient migration complete (establishes cookie/session patterns)
- ✅ WebEnabledFastaImporter migration complete (establishes HTTP recording/playback pattern)

## Scope

### In-Scope: Core Skyline.exe
1. **`ToolsUI/EditRemoteAccountDlg.cs`** - Ardia API session management
   - Uses `HttpClient` with `CookieContainer` for session cookies
   - Similar to PanoramaClient pattern
   - Should use `HttpClientWithProgress` for consistency

2. **`Alerts/ArdiaLoginDlg.cs`** - Ardia OAuth authentication
   - Uses bare `HttpClient` for OAuth flows
   - **Contains async/await keywords** (see `TODO-remove_async_and_await.md`)
   - OAuth library integration may require special handling
   - Consider: Can we refactor to remove async/await while migrating to HttpClientWithProgress?

3. **Remaining `HttpWebRequest` usage** - Throughout codebase
   - Replace with `HttpClientWithProgress` where applicable
   - Document acceptable usage (e.g., status codes, header enums only)

4. **Connected Tests Organization** - Comprehensive review
   - Identify all tests requiring network access
   - Move appropriate tests to `TestConnected` project
   - Fix TeamCity configuration for connected test runs

### Out-of-Scope (Separate TODOs)
- **SkylineTester** - Build/test infrastructure (separate from product)
- **ScreenshotPreviewForm** - Test utilities only
- **Executables/DevTools** - Developer tools (see `TODO-tools_webclient_replacement.md`)

## Related Work

**This TODO continues:**
- `TODO-20251107_httpclient_to_progress.md` - WebEnabledFastaImporter migration with HTTP recording/playback

**This TODO builds on:**
- `TODO-20251010_webclient_replacement.md` - Established HttpClientWithProgress foundation
- `TODO-20251019_skyp_webclient_replacement.md` - Added authentication header support
- `TODO-20251023_panorama_webclient_replacement.md` - Added cookie/session/upload support

**This TODO relates to:**
- `TODO-remove_async_and_await.md` - ArdiaLoginDlg has async/await to remove
- `TODO-tools_webclient_replacement.md` - Tools migration (deferred, lower priority)

## Task Checklist

### Phase 1: Inventory & Analysis
- [ ] Inventory all remaining `System.Net` usage in Skyline and shared libraries
  - Classify occurrences: `HttpClient`, `HttpWebRequest`, `WebRequest`, `HttpStatusCode`, headers, sockets
  - Identify files that still compile against `HttpWebRequest`
  - Document any acceptable `System.Net` usage (e.g., `HttpStatusCode`, `HttpRequestHeader` enums)
- [ ] For each file encountered, temporarily remove `using System.Net;` and build to understand dependencies
  - Record which types still require `System.Net`
  - Note dead code exposed by the removal (unused helpers, obsolete flows)
- [ ] Analyze `EditRemoteAccountDlg` Ardia API usage
  - Cookie/session management patterns
  - API endpoints called
  - Error handling approach
  - Progress/cancellation needs
- [ ] Analyze `ArdiaLoginDlg` OAuth integration
  - Identify async/await usage
  - Understand OAuth library requirements
  - Determine if synchronous refactor is feasible
  - Plan async/await removal strategy
- [ ] Identify other Panorama/AutoQC/SkylineBatch code paths still using `HttpWebRequest`
  - Confirm overlap with TODOs in backlog (`TODO-tools_webclient_replacement`, `TODO-skylinebatch_test_cleanup`)
  - Propose ownership if migration belongs in this branch vs. follow-up TODO

### Phase 2: EditRemoteAccountDlg Migration
- [ ] Replace bare `HttpClient` with `HttpClientWithProgress`
- [ ] Use `CookieContainer` parameter for session management
- [ ] Add progress reporting for API calls
- [ ] Add cancellation support
- [ ] Update error handling to use `MapHttpException`
- [ ] Create tests using `HttpClientTestHelper`

### Phase 3: ArdiaLoginDlg Migration
- [ ] **Decision point:** Can we remove async/await while migrating?
  - If yes: Refactor to ActionUtil.RunAsync() + HttpClientWithProgress
  - If no: Consider splitting into separate async/await removal branch
- [ ] Migrate OAuth flows to use HttpClientWithProgress (if feasible)
- [ ] Remove async/await keywords (if feasible)
- [ ] Add progress reporting for OAuth flows
- [ ] Create tests for OAuth scenarios

### Phase 4: Retire HttpWebRequest
- [ ] Replace remaining `HttpWebRequest` usage with `HttpClientWithProgress`
- [ ] Update helpers/utilities that currently return `HttpWebRequest`
- [ ] Ensure authentication headers/cookies are handled through `HttpClientWithProgress`
- [ ] Update any tests relying on `HttpWebRequest` seams

### Phase 5: Testing
- [ ] Test Ardia login workflows
- [ ] Test session management
- [ ] Test OAuth device flow
- [ ] Test error scenarios (network failures, auth failures)
- [ ] Verify no regressions in Ardia integration

### Phase 6: Code Inspection & Documentation
- [ ] Update `CodeInspectionTest` to detect bare `HttpClient` usage in core Skyline
- [ ] Add inspection for `HttpWebRequest` in `pwiz_tools` and `pwiz_tools/Shared`
- [ ] Verify all network operations use `HttpClientWithProgress`
- [ ] Document the pattern

### Phase 7: Connected Tests Review & Organization
- [ ] **Comprehensive review of all "Connected" tests**
  - [ ] Identify all tests that fail when `AllowInternetAccess` is enabled but computer is disconnected from network
  - [ ] These are the true "Connected" tests that require network access
  - [ ] Document which tests are currently in wrong project (e.g., `ProteomeDbTest` in `Test` project)
  - [ ] Consider annotation or naming convention (e.g., `[Web]` suffix) to identify connected tests
  - [ ] Review TeamCity configuration for "TestConnected tests" - ensure it runs with `-EnableInternet` flag
    - Current issue: TeamCity runs `TestConnected` project but doesn't set `AllowInternetAccess`, causing `TestFastaImportWeb` to short-circuit
  - [ ] Move appropriate tests from `Test`/`TestData`/`TestFunctional` to `TestConnected` project
  - [ ] Ensure all connected tests follow `TestName[Web]` naming convention
  - [ ] Verify TeamCity configuration properly enables internet access for connected test runs
  - [ ] Document connected test requirements and TeamCity setup

## Success Criteria
- All bare `HttpClient` usage in core Skyline replaced with `HttpClientWithProgress`
- All remaining `HttpWebRequest` and ad-hoc `System.Net` request code retired or justified
- Consistent progress reporting and cancellation across all HTTP operations
- async/await removed from ArdiaLoginDlg (or separate TODO created if too complex)
- Comprehensive test coverage
- Code inspection test passes
- Connected tests properly organized and TeamCity configuration fixed

## Risks & Considerations

### Ardia OAuth Complexity
- OAuth libraries may require async patterns
- Device flow involves polling and timeouts
- May need to keep async/await for OAuth (or use synchronous OAuth library)

### Breaking Ardia Integration
- Ardia login is critical feature
- Must test thoroughly with real Ardia server
- Session management must work identically

## Out of Scope
- Tools/Executables migration (separate TODO)
- SkylineTester/test infrastructure improvements
- Panorama integration (already covered in previous work)

## Estimated Effort
- **EditRemoteAccountDlg**: 1-2 days (straightforward, similar to PanoramaClient)
- **ArdiaLoginDlg**: 3-5 days (complex, async/await considerations)
- **HttpWebRequest retirement**: 2-3 days (inventory + migration)
- **Connected tests organization**: 1-2 days (review + TeamCity config)
- **Total**: ~1.5-2 weeks

## References
- HttpClientWithProgress.cs - Complete foundation with cookies, headers, uploads
- HttpClientTestHelper.cs - Testing infrastructure with recording/playback
- ai/docs/testing-patterns.md - HTTP recording/playback pattern documentation
- MEMORY.md - Async patterns and ActionUtil.RunAsync() guidance
- TODO-remove_async_and_await.md - async/await removal strategy

## Handoff Prompt for Branch Creation

```
I want to continue migrating remaining bare HttpClient and HttpWebRequest usage in core Skyline to HttpClientWithProgress.

This ensures consistency across all network operations and provides users with progress reporting
and cancellation for Ardia authentication and API calls.

Key challenges:
- ArdiaLoginDlg uses async/await with OAuth. May need to remove async/await as part
  of this work, or defer to separate branch if OAuth libraries require it.
- Comprehensive review needed to identify all remaining HttpWebRequest usage
- Connected tests need proper organization and TeamCity configuration fixes

Please:
1. Create branch: Skyline/work/YYYYMMDD_httpclient_to_progress_continued
2. Move and rename TODO file to active with date
3. Begin Phase 1: Inventory & Analysis of remaining HTTP usage

The TODO file contains full context. Let's ensure ALL HTTP operations in Skyline use our
robust HttpClientWithProgress infrastructure.
```

