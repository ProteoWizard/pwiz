# TODO-panorama_webclient_replacement.md

## Branch Information (Future)
- **Branch**: Not yet created - will be `Skyline/work/YYYYMMDD_panorama_webclient_replacement`
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

## Task Checklist

### Phase 1: Analysis & Planning
- [ ] Read and understand `WebPanoramaPublishClient` in `PanoramaPublishUtil.cs`
- [ ] Identify all WebClient usage patterns:
  - Authentication flows
  - File upload mechanisms
  - API endpoint calls
  - Error handling patterns
  - Progress reporting
- [ ] Document current behavior and edge cases
- [ ] Identify test coverage gaps
- [ ] Plan migration strategy

### Phase 2: HttpClient Migration
- [ ] Replace WebClient with HttpClient/HttpClientWithProgress
- [ ] Migrate authentication handling
- [ ] Migrate file upload with progress reporting
- [ ] Migrate API calls (GET, POST, etc.)
- [ ] Ensure proper resource disposal (IDisposable patterns)
- [ ] Update error handling to use `MapHttpException`
- [ ] Maintain backward compatibility with existing callers

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
