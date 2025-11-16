# TODO-httpclient_to_progress.md

## Branch Information
- **Branch**: `Skyline/work/20251107_httpclient_to_progress`
- **Created**: 2025-11-07
- **Status**: In progress
- **PR**: [#3669](https://github.com/ProteoWizard/pwiz/pull/3669)
- **Objective**: Finish consolidating Skyline HTTP usage onto `HttpClientWithProgress`, including retiring remaining `HttpWebRequest` and auditing legacy `System.Net` patterns

## Background

After completing the WebClient → HttpClient migration, we discovered several places in core Skyline that use bare `HttpClient` directly, predating the `HttpClientWithProgress` infrastructure. A repository-wide search for `using System.Net;` also revealed lingering `HttpWebRequest` usage and ad-hoc `System.Net` patterns that bypass our new error handling. These should be migrated for:

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

### Phase 1: Inventory & Analysis
- [ ] Inventory all `System.Net` usage in Skyline and shared libraries
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
- [x] Analyze `HttpClientWithProgress` exception handling
  - Determine if `MapHttpRequestException` and `MapUnexpectedWebException` remain reachable
  - If reachable, document real-world repro steps; otherwise plan safe removal/refactor

- [x] Replace `WebEnabledFastaImporter` network access with `HttpClientWithProgress`
  - [x] Update `WebSearchProvider.GetWebResponseStream` to stream via HttpClientWithProgress
  - [x] Replace `GetXmlTextReader(string url)` with HttpClient-based retrieval
  - [x] Propagate `IProgressMonitor` and cancellation into web requests
  - [x] Preserve timeout behavior using linked cancellation tokens
- [x] Drop legacy `WebRequest`/`HttpWebRequest` usage from FASTA importer tests (interfaces updated; legacy seams remain only until HttpClientTestHelper expansion)
- [x] Introduce `HttpClientTestHelper`-based mocks for FASTA web responses
- [x] Document the migration pattern in `STYLEGUIDE.md` or importer-specific docs if needed
- [ ] Convert `ProteomeDbTest` offline mode to `HttpClientTestHelper` playback (replace `FakeWebSearchProvider` usage in `TestOlderProteomeDb`)

### Phase 3: EditRemoteAccountDlg Migration
- [ ] Replace bare `HttpClient` with `HttpClientWithProgress`
- [ ] Use `CookieContainer` parameter for session management
- [ ] Add progress reporting for API calls
- [ ] Add cancellation support
- [ ] Update error handling to use `MapHttpException`
- [ ] Create tests using `HttpClientTestHelper`

### Phase 4: ArdiaLoginDlg Migration
- [ ] **Decision point:** Can we remove async/await while migrating?
  - If yes: Refactor to ActionUtil.RunAsync() + HttpClientWithProgress
  - If no: Consider splitting into separate async/await removal branch
- [ ] Migrate OAuth flows to use HttpClientWithProgress (if feasible)
- [ ] Remove async/await keywords (if feasible)
- [ ] Add progress reporting for OAuth flows
- [ ] Create tests for OAuth scenarios

### Phase 5: Retire HttpWebRequest
- [ ] Replace remaining `HttpWebRequest` usage with `HttpClientWithProgress`
- [ ] Update helpers/utilities that currently return `HttpWebRequest`
- [ ] Ensure authentication headers/cookies are handled through `HttpClientWithProgress`
- [ ] Update any tests relying on `HttpWebRequest` seams

### Phase 6: Testing
- [ ] Test Ardia login workflows
- [ ] Test session management
- [ ] Test OAuth device flow
- [ ] Test error scenarios (network failures, auth failures)
- [ ] Verify no regressions in Ardia integration

### Phase 7: Code Inspection & Documentation
- [ ] Update `CodeInspectionTest` to detect bare `HttpClient` usage in core Skyline
- [ ] Add inspection for `HttpWebRequest` in `pwiz_tools` and `pwiz_tools/Shared`
- [ ] Verify all network operations use `HttpClientWithProgress`
- [ ] Document the pattern

## Success Criteria
- All bare `HttpClient` usage in core Skyline replaced with `HttpClientWithProgress`
- All remaining `HttpWebRequest` and ad-hoc `System.Net` request code retired or justified
- Consistent progress reporting and cancellation across all HTTP operations
- async/await removed from ArdiaLoginDlg (or separate TODO created if too complex)
- Comprehensive test coverage
- Code inspection test passes

## Progress (2025-11-14)
- Hardened FASTA importer failure handling: removed reliance on `LastError`, ensured every 404/timeout/cancellation stamps `ProteinSearchInfo.FailureReason`, and fixed `HandleNoResponses` so batches mark all proteins before completing.
- Added optional diagnostics (`IsDiagnosticMode`) that capture pre/post lookup snapshots to JSON via `TestContext.GetTestResultsPath`, making MSTest/TestRunner investigations reproducible without debugger watch windows.
- Strengthened FASTA importer assertions (normal / 404 / no-network / cancellation) to verify counts, status transitions, and localized messages, and re-recorded `FastaImporterWebData.json`.
- Documented the `IsRecordMode` / diagnostics workflow and “build before test” requirement in `ai/WORKFLOW.md`, `ai/TESTING.md`, and `ai/docs/testing-patterns.md`.
- Verified build + code inspection + FASTA importer tests (offline/online) with the new diagnostics disabled by default.

## Progress (2025-11-15)
- Replaced bespoke FASTA playback seams with `HttpClientTestHelper` recording/interaction files, plus `DiagnosticMode` flags for request/result dumps and row-indexed JSON snapshots.
- Expanded `ProteinSearchInfo` diagnostics (failure reason/exception/detail, taxonomy ID, species normalization, search URL history) and implemented UniProt fallback queues so live/recorded runs capture equivalent behavior.
- Promoted `HttpInteractionRecorder` to reusable API, added passive request logging, and ensured ENT/UniProt handlers stamp search histories before completion to keep playback deterministic.
- Cleaned up `WebEnabledFastaImporter` (removed console logging, fixed 404 failure ordering, species extraction, `SearchUrlHistory` duplication) so production runs stay lean while diagnostics remain opt-in.

## Progress (2025-11-07)
- Removed `MapUnexpectedWebException` and related WebRequest-era helpers; all Panorama exception flows now originate from `NetworkRequestException`.
- Verified via debugger that DNS failures surface as `HttpRequestException` with inner `WebException`; retained handling in `HttpClientWithProgress` and test helper.
- Current legitimate `WebException` references: `HttpClientWithProgress` (DNS inner exception), `HttpClientTestHelper`, Ardia OAuth client (still raw `HttpClient`), Web-enabled FASTA importer (legacy APIs), and associated tests. Further reduction depends on migrating those callers to `HttpClientWithProgress`.
- Began cataloging remaining `System.Net` usage:
  - **Migration targets / follow-up design**
    - `CommonMsData/RemoteApi/Ardia/ArdiaClient.cs` (HttpWebRequest delete path, bespoke HttpClient wrapper)
    - `Shared/ProteomeDb/Fasta/WebEnabledFastaImporter.cs` (WebRequest download pipeline)
    - `Skyline/Alerts/ArdiaLoginDlg.cs`, `Skyline/Alerts/ArdiaLogoutDlg.cs` (HttpListener, cookie exchange during OAuth)
    - `Skyline/Alerts/ReportErrorDlg.cs` (WebRequest upload for LabKey error reporting)
    - `Skyline/Program.cs`, `Skyline/SkylineNightly/Nightly.cs`, `SkylineNightlyShim/Program.cs`, `SkylineTester/Program.cs`, `Skyline/TestRunner/Program.cs` (telemetry/build automation using HttpWebRequest or ServicePointManager)
  - **Acceptable (status/header-only) — annotated inline**
    - `CommonMsData/RemoteApi/Ardia/ArdiaResult.cs`
    - `Shared/PanoramaClient/PanoramaClient.cs`
    - `Shared/PanoramaClient/PanoramaUtil.cs`
    - `Skyline/FileUI/PublishDocumentDlgArdia.cs`
    - `Skyline/FileUI/SkypSupport.cs`
    - `Skyline/SkylineFiles.cs`
    - `Skyline/TestFunctional/PanoramaClientPublishTest.cs`
    - `Skyline/TestFunctional/SkypTest.cs`

- Confirmed FASTA importer networking runs on the UI thread with only status-bar updates; decided to migrate to `HttpClientWithProgress` for cancellation and consistent error handling.
- Drafted migration plan: inject `IProgressMonitor` into `WebSearchProvider`, wrap requests with linked cancellation tokens to respect existing timeout constants, and disable size reporting for small payloads.
- Identified follow-on work: replace `FakeWebSearchProvider`/`DelayedWebSearchProvider` seams with `HttpClientTestHelper` mocks once the importer understands `HttpClientWithProgress`.
- Replaced `WebSearchProvider` HTTP helpers to use `HttpClientWithProgress`, including timeout-aware progress monitors and resource-backed status messages. Tests still rely on legacy fake providers until `HttpClientTestHelper` seams are wired in.
- Verified build + targeted CommonTest suite (`TestBasicFastaImport`, `TestFastaImport`, `WebTestFastaImport`) across English/Chinese/French languages, ensuring cancellation-aware HttpClient flow passes localized scenarios.

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

