# TODO-tools_webclient_replacement.md

## Branch Information (Future)
- **Branch**: Not yet created - will be `Skyline/work/YYYYMMDD_tools_webclient_replacement`
- **Objective**: Migrate WebClient to HttpClient in auxiliary tools (not core Skyline.exe)

## Background
This is **Phase 2** of the WebClient → HttpClient migration. Phase 1 (branch `Skyline/work/20251010_webclient_replacement`) focused on core Skyline.exe and TestRunner.exe code, which provides the primary user value and test coverage.

This phase targets auxiliary tools that are built separately from Skyline.sln:
- Executables (AutoQC, SkylineBatch, Installer, etc.)
- Supporting utilities (SkylineNightly, SkylineNightlyShim)

These tools are less frequently used and have separate build processes, making them lower priority for migration.

## Prerequisites
- ✅ Phase 1 complete: Core Skyline.exe WebClient → HttpClient migration merged to master
- ✅ `HttpClientWithProgress` established and tested
- ✅ Exception handling patterns standardized
- ✅ Testing infrastructure (`HttpClientTestHelper`) available

## Task Checklist

### Phase 1: Executables/AutoQC
- [ ] **AutoQC/PanoramaSettings.cs** - WebClient usage for Panorama connectivity
  - Review usage patterns
  - Determine if `HttpClientWithProgress` is appropriate
  - Consider AutoQC-specific requirements (service vs. UI)
  - Test with actual Panorama server connections

- [ ] **AutoQCTest/PanoramaTest.cs** - Test code for AutoQC Panorama features
  - Update tests to use `HttpClientTestHelper` if applicable
  - Ensure test coverage for network failures

### Phase 2: Executables/SkylineBatch
- [ ] **SkylineBatch/Server.cs** - WebClient usage in batch processing
- [ ] **SkylineBatch/PanoramaServerConnector.cs** - Panorama connectivity for batch
- [ ] **SkylineBatch/DownloadDlg.cs** - Download UI in batch tool
  - These may share patterns with AutoQC
  - Consider code sharing opportunities for Panorama connectivity

### Phase 3: Nightly Build Tools
- [ ] **SkylineNightly/Nightly.cs** - WebClient usage in nightly build automation
- [ ] **SkylineNightlyShim/Program.cs** - Shim wrapper for nightly builds
  - These are automated tools, may not need progress UI
  - Consider simpler HttpClient usage without full `HttpClientWithProgress`
  - Ensure robust error handling for automated scenarios

### Phase 4: Installer
- [ ] **Executables/Installer/SetupDeployProject.cs** - WebClient usage in installer
  - Review carefully - installer failures are critical user experience
  - Ensure compatibility with installer environment
  - Test thoroughly on clean systems

## Considerations

### Code Sharing Opportunities
Multiple tools connect to Panorama servers. Consider:
- Shared Panorama connectivity library?
- Common HTTP client configuration?
- Standardized error handling across tools?

### UI vs. Headless Tools
- **UI tools** (AutoQC, SkylineBatch DownloadDlg): May benefit from `HttpClientWithProgress`
- **Headless tools** (Nightly, Installer): May use simpler HttpClient patterns

### Testing Strategy
- Executables have separate test projects (AutoQCTest)
- May require integration tests with actual servers
- Consider mock server for Panorama API testing

### Build Considerations
- Executables are NOT built by Skyline.sln
- Each has separate solution or build process
- Changes must be tested independently
- Deployment and installation testing required

## Risks & Dependencies
- **Lower priority**: Core Skyline.exe already migrated
- **Separate build processes**: Each tool requires independent verification
- **Panorama integration**: Requires server access or extensive mocking
- **Less frequent usage**: Harder to get real-world testing feedback

## Success Criteria
- All auxiliary tools migrated from WebClient to HttpClient
- No regressions in tool functionality
- Appropriate error handling for each tool's context
- Tests updated and passing
- Tools remain buildable and deployable independently

## Out of Scope
This TODO does **not** include:
- Core Skyline.exe code (completed in Phase 1)
- TestRunner.exe test infrastructure (completed in Phase 1)
- `.skyp` file support (SkypSupport.cs) - may be included in Phase 1 or separate branch
- PanoramaClient (`WebPanoramaPublishClient`) - likely separate branch due to size

## References
- Phase 1 branch: `Skyline/work/20251010_webclient_replacement`
- `HttpClientWithProgress.cs` - Core wrapper established in Phase 1
- `HttpClientTestHelper.cs` - Testing infrastructure from Phase 1
- MEMORY.md - Project context and patterns

## Handoff Prompt for Branch Creation

```
I want to start work on tools WebClient → HttpClient migration from TODO-tools_webclient_replacement.md.

This is Phase 2 - core Skyline.exe migration is complete. We're now migrating auxiliary tools (AutoQC, SkylineBatch, Installer, etc.).

Please:
1. Create branch: Skyline/work/YYYYMMDD_tools_webclient_replacement (use today's date)
2. Rename TODO file to include the date
3. Update the TODO file header with actual branch information
4. Begin Phase 1: Executables/AutoQC

Key context: These tools build separately from Skyline.sln. Test each independently. Consider code sharing for Panorama connectivity.

The TODO file contains full context. Let's start with AutoQC/PanoramaSettings.cs.
```
