# TODO-httpclient_to_progress.md

## Branch Information (Future)
- **Branch**: Not yet created - will be `Skyline/work/YYYYMMDD_httpclient_to_progress`
- **Objective**: Migrate existing bare HttpClient usage to HttpClientWithProgress for consistency and better UX

## Background

After completing the WebClient → HttpClient migration, we discovered several places in core Skyline that use bare `HttpClient` directly, predating the `HttpClientWithProgress` infrastructure. These should be migrated for:

- **Consistency**: All HTTP operations should use the same infrastructure
- **User experience**: Progress reporting and cancellation for all network operations
- **Maintainability**: Single source of truth for HTTP error handling
- **Testing**: Leverage comprehensive `HttpClientTestHelper` infrastructure

## Prerequisites
- ✅ `HttpClientWithProgress` complete with upload/download/cookie/header support
- ✅ Comprehensive testing infrastructure (`HttpClientTestHelper`)
- ✅ PanoramaClient migration complete (establishes cookie/session patterns)

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

### Out-of-Scope (Separate TODOs)
- **SkylineTester** - Build/test infrastructure (separate from product)
- **ScreenshotPreviewForm** - Test utilities only
- **Executables/DevTools** - Developer tools (see `TODO-tools_webclient_replacement.md`)

## Related Work

**This TODO builds on:**
- `TODO-20251010_webclient_replacement.md` - Established HttpClientWithProgress foundation
- `TODO-20251019_skyp_webclient_replacement.md` - Added authentication header support
- `TODO-20251023_panorama_webclient_replacement.md` - Added cookie/session/upload support

**This TODO relates to:**
- `TODO-remove_async_and_await.md` - ArdiaLoginDlg has async/await to remove
- `TODO-tools_webclient_replacement.md` - Tools migration (deferred, lower priority)

## Task Checklist

### Phase 1: Analysis
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

### Phase 4: Testing
- [ ] Test Ardia login workflows
- [ ] Test session management
- [ ] Test OAuth device flow
- [ ] Test error scenarios (network failures, auth failures)
- [ ] Verify no regressions in Ardia integration

### Phase 5: Code Inspection
- [ ] Update `CodeInspectionTest` to detect bare `HttpClient` usage in core Skyline
- [ ] Verify all network operations use `HttpClientWithProgress`
- [ ] Document the pattern

## Success Criteria
- All bare `HttpClient` usage in core Skyline replaced with `HttpClientWithProgress`
- Consistent progress reporting and cancellation across all HTTP operations
- async/await removed from ArdiaLoginDlg (or separate TODO created if too complex)
- Comprehensive test coverage
- Code inspection test passes

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
- Panorama integration (already covered in current branch)

## Estimated Effort
- **EditRemoteAccountDlg**: 1-2 days (straightforward, similar to PanoramaClient)
- **ArdiaLoginDlg**: 3-5 days (complex, async/await considerations)
- **Total**: ~1 week

## References
- HttpClientWithProgress.cs - Complete foundation with cookies, headers, uploads
- HttpClientTestHelper.cs - Testing infrastructure
- MEMORY.md - Async patterns and ActionUtil.RunAsync() guidance
- TODO-remove_async_and_await.md - async/await removal strategy

## Handoff Prompt for Branch Creation

```
I want to review and migrate existing bare HttpClient usage in core Skyline to HttpClientWithProgress.

This ensures consistency across all network operations and provides users with progress reporting
and cancellation for Ardia authentication and API calls.

Key challenge: ArdiaLoginDlg uses async/await with OAuth. May need to remove async/await as part
of this work, or defer to separate branch if OAuth libraries require it.

Please:
1. Create branch: Skyline/work/YYYYMMDD_httpclient_to_progress
2. Move and rename TODO file to active with date
3. Begin Phase 1: Analysis of EditRemoteAccountDlg and ArdiaLoginDlg

The TODO file contains full context. Let's ensure ALL HTTP operations in Skyline use our
robust HttpClientWithProgress infrastructure.
```

