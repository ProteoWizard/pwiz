# TODO-20251107_httpclient_to_progress.md

## Branch Information
- **Branch**: `Skyline/work/20251107_httpclient_to_progress`
- **Created**: 2025-11-07
- **Completed**: 2025-11-17
- **Status**: ✅ Completed
- **PR**: [#3669](https://github.com/ProteoWizard/pwiz/pull/3669)
- **Objective**: Migrate `WebEnabledFastaImporter` to use `HttpClientWithProgress` with comprehensive HTTP recording/playback infrastructure for offline testing

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

## Completed Work Summary

### Phase 1: WebEnabledFastaImporter Migration ✅
- [x] Replaced `WebEnabledFastaImporter` network access with `HttpClientWithProgress`
  - [x] Updated `WebSearchProvider.GetWebResponseStream` to stream via HttpClientWithProgress
  - [x] Replaced `GetXmlTextReader(string url)` with HttpClient-based retrieval
  - [x] Propagated `IProgressMonitor` and cancellation into web requests
  - [x] Preserved timeout behavior using linked cancellation tokens
- [x] Dropped legacy `WebRequest`/`HttpWebRequest` usage from FASTA importer tests
- [x] Introduced `HttpClientTestHelper`-based mocks for FASTA web responses
- [x] Converted `ProteomeDbTest` offline mode to `HttpClientTestHelper` playback
  - [x] Added `IsRecordMode` property and HTTP interaction recording/playback infrastructure
  - [x] Created `ProteomeDbWebData.json` for recorded HTTP interactions
  - [x] Removed `FakeWebSearchProvider` fallback - now requires recorded data for offline tests
  - [x] Full metadata validation now works in offline mode (same as web mode)
  - [x] Test runs sub-second without network access

### Phase 2: Testing Infrastructure & Performance ✅
- [x] Implemented `FastWebSearchProvider` to skip politeness delays during playback
  - [x] Measured ~10 seconds improvement (19 sec → 10-11 sec per test run)
  - [x] Test passes in all 5 languages (en, zh, fr, ja, tr)
- [x] Established naming convention for paired web/offline tests
  - [x] Applied suffix pattern: `TestFastaImport`/`TestFastaImportWeb` and `TestOlderProteomeDb`/`TestOlderProteomeDbWeb`
  - [x] Tests now sort together alphabetically in test runners

### Phase 3: Documentation ✅
- [x] Documented HTTP recording/playback pattern in `ai\docs\testing-patterns.md`
  - [x] Used `ProteomeDbTest` as the clean, simple example
  - [x] Provided clear instructions for developers and LLMs
  - [x] Documented minimal implementation pattern
  - [x] Added `TestName[Web]` naming convention guidance
- [x] Updated comment style guidelines in `ai/docs/style-guide.md`
  - [x] Comments should start with capital letter
  - [x] True sentences should end with period
  - [x] XML documentation should use `<see cref="ClassName">` for class references
  - [x] Return-only documentation guidelines (no empty `<param>` or `<returns>` tags)

### Phase 4: Code Quality Improvements ✅
- [x] Fixed `ToString()` bug in `ProteinSearchInfo` (PreferredName was being overwritten)
- [x] Improved `BuildUniprotSearchTerm` with explicit null return and clarifying comments
- [x] Restored valuable XML example comment block in `ReadEntrezSummary`
- [x] Fixed comment capitalization throughout `WebEnabledFastaImporter`
- [x] Improved error handling consistency with `ThrowResponseFailedException` method
  - [x] Ensures both upload and download paths consistently notify test behaviors
  - [x] Prevents future inconsistencies in error handling

## Remaining Work

**Moved to:** `ai/todos/backlog/TODO-httpclient_to_progress_continued.md`

The following work has been deferred to a future PR:
- Ardia integration migration (`EditRemoteAccountDlg`, `ArdiaLoginDlg`)
- Remaining `HttpWebRequest` retirement
- Code inspection updates
- Connected tests review and organization

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

## Progress (2025-11-16)
- Completed `ProteomeDbTest` HTTP recording/playback implementation as clean template for simple HTTP interaction recording
  - Removed `FakeWebSearchProvider` fallback - offline tests now require recorded data, enabling full metadata validation
  - Test runs sub-second without network access (<10 UniProt requests)
  - Cleaned up naming and documentation to accurately reflect HTTP interaction recording (not "expectations")
  - Added `FastWebSearchProvider` class to `FastaImporterTest.cs` for performance optimization
- Implemented `FastWebSearchProvider` in `TestFastaImport` playback mode
  - Measured ~10 seconds improvement (19 sec → 10-11 sec per test run)
  - Skips politeness delays (333ms per Entrez request, 10ms per UniProt request) during in-memory playback
  - Test passes in all 5 languages (en, zh, fr, ja, tr)
- Documented HTTP recording/playback pattern in `ai\docs\testing-patterns.md` using `ProteomeDbTest` as clean example
- Established naming convention for paired web/offline tests: suffix pattern (e.g., `TestFastaImportWeb`, `TestOlderProteomeDbWeb`)
  - Applied to `TestFastaImport`/`TestFastaImportWeb` and `TestOlderProteomeDb`/`TestOlderProteomeDbWeb`
  - Tests now sort together alphabetically in test runners
  - Documented `TestName[Web]` naming pattern in `ai\docs\testing-patterns.md`
- **Ready for checkpoint merge to master** - This PR covers WebEnabledFastaImporter HTTP recording/playback infrastructure
- Deferred to future PR (Phase 9): Comprehensive review and organization of all "Connected" tests
  - Identify all tests requiring network access (fail when disconnected)
  - Move appropriate tests to `TestConnected` project
  - Fix TeamCity configuration to properly enable internet access for connected tests
  - Establish clear identification mechanism (naming convention or annotation)

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

