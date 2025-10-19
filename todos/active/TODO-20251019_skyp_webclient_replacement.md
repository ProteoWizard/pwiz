# TODO-20251019_skyp_webclient_replacement.md

## Branch Information
- **Branch**: `Skyline/work/20251019_skyp_webclient_replacement`
- **Created**: 2025-10-19
- **Objective**: Migrate `.skyp` file support from WebClient to HttpClient

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

## Success Criteria

### Functional
- [ ] `.skyp` files download successfully from web URLs
- [ ] Progress reporting works correctly during download
- [ ] User can cancel download mid-stream
- [ ] Network errors show user-friendly, localized messages
- [ ] HTTP status errors (404, 500) handled gracefully

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

