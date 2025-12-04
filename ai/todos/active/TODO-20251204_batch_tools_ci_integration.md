# TODO-20251204_batch_tools_ci_integration.md

## Branch Information
- **Branch**: `Skyline/work/20251204_batch_tools_ci_integration`
- **Created**: 2025-12-04
- **Completed**: (pending)
- **Status**: ⏸️ ON HOLD - Waiting for TeamCity Admin
- **PR**: [#3697](https://github.com/ProteoWizard/pwiz/pull/3697) (DRAFT)
- **Objective**: Enable CI to build and test SkylineBatch and AutoQC with proper Jamfile targets

## Summary
**Priority**: High
**Complexity**: Medium
**Original Planning Date**: 2025-11-24

## Goal (Sprint Scope)
Enable Skyline master/PR TeamCity config to reliably build SkylineBatch and AutoQC and run their test suites, with Panorama-dependent AutoQC tests skipped gracefully when credentials are absent.

## Out of Scope (Can Defer)
The following are valuable but not required for initial integration:
- ReSharper InspectCode enforcement for SkylineBatch/AutoQC
- Full AutoQC test execution against PanoramaWeb (credential provisioning)
- Code coverage integration for batch tools

## Current Gaps
- Primary config builds Skyline (SkylineBatch likely included, AutoQC likely excluded – not in `Skyline/Jamfile.jam`).
- AutoQC tests requiring Panorama authentication cannot run headless without credential injection.
- No standardized skip mechanism for auth-required tests (risk: failures or disabled tests hide issues).
- Disabled AutoQC test: `TestEnableInvalid` (exposed latent logic issue after threading fix).

## Sprint Priorities
1. Confirm/ensure Jamfile (or equivalent build config) includes AutoQC build in master/PR pipeline.
2. Implement test skip mechanism for Panorama-auth-required AutoQC tests (e.g. environment variable `AUTOQC_SKIP_PANORAMA_AUTH=1`).
3. Re-enable and fix `TestEnableInvalid` (root cause analysis + correction) OR clearly annotate reason and create targeted follow-up if fix exceeds sprint time.
4. Run SkylineBatch tests in same pipeline (validate they all pass in CI environment).

## Skip Mechanism Design (AutoQC)
Introduce conditional logic in test setup:
```csharp
// Pseudocode
if (Environment.GetEnvironmentVariable("AUTOQC_SKIP_PANORAMA_AUTH") == "1")
{
    if (TestRequiresPanoramaLogin())
        Assert.Inconclusive("Skipped: requires Panorama credentials");
}
```
Guidelines:
- Use `Assert.Inconclusive` (records skip without failure).
- Centralize `TestRequiresPanoramaLogin()` tagging (attribute or helper).
- Ensure non-auth tests still exercise core logic paths.

## Follow-Up (Post-Sprint Options)
1. Provision secure Panorama credentials via TeamCity parameters/secret storage; run full AutoQC suite.
2. Add SkylineBatch + AutoQC InspectCode to existing "Skyline Code Inspection" TeamCity config (fail on new WARNINGs).
3. Extend coverage config to include SkylineBatch + AutoQC after tests fully green and stable.

## Implementation Checklist (Sprint)
- [ ] Audit `Skyline/Jamfile.jam` for batch tool inclusions (SkylineBatch ✅ / AutoQC ❌?).
- [ ] Add AutoQC build target to Jamfile or CI config.
- [ ] Introduce environment variable driven skip logic for auth-required AutoQC tests.
- [ ] Tag/identify Panorama-auth tests (attribute or naming convention).
- [ ] Investigate and address `TestEnableInvalid` (fix or justify deferral).
- [ ] Validate CI run: Skyline + SkylineBatch + AutoQC non-auth tests all pass.
- [ ] Document skip mechanism in `README` or test guidelines.

## Deferred Checklist (Post-Sprint)
- [ ] Secure credentials for full AutoQC test execution.
- [ ] Integrate InspectCode for SkylineBatch/AutoQC into "Skyline Code Inspection" config.
- [ ] Consolidate coverage reporting across all three (Skyline, SkylineBatch, AutoQC).

## Risks & Mitigations
**Credential Handling Complexity**: Avoid in initial sprint; use skip variable.  
**Hidden Logic in Skipped Tests**: Ensure partial tests still touch core configuration paths.  
**`TestEnableInvalid` Root Cause Larger Than Expected**: Time-box analysis; if unresolved create dedicated TODO.

## Success Criteria
- CI pipeline builds Skyline, SkylineBatch, AutoQC without manual changes.
- Non-auth AutoQC tests run and pass (no blanket disabling).
- Auth-required tests recorded as skipped (Inconclusive) rather than failed or silently ignored.
- Clear path documented for enabling full test + inspection + coverage in future.

## References
- TeamCity configs screenshot (internal) – validates existing code inspection separation.
- `AutoQcConfigManager` and test files for Panorama-dependent logic.
- Existing `Build-*.ps1` scripts show local build/test workflow.

## Related TODOs
- `TODO-batch_tools_consolidate_test_util.md` (shared skip helpers).
- `TODO-panorama_json_typed_models.md` (typed models reduce fragile auth-related parsing).

## Progress Log

### 2025-12-04: Initial Setup and Jamfile Integration

**Branch Setup**
- Created feature branch `Skyline/work/20251204_batch_tools_ci_integration` from master
- Copied TODO from ai-context branch to active directory
- Updated TODO header with branch information

**Jamfile.jam Changes** - [Jamfile.jam:85,213-239,562-585](pwiz_tools/Skyline/Jamfile.jam)
- ✅ Added AssemblyInfo generation for AutoQC (line 85)
- ✅ Added `do_auto_qc` rule and actions (lines 213-225)
- ✅ Added `do_auto_qc_test` rule and actions (lines 227-239)
- ✅ Added `AutoQC.exe` make target (lines 562-572)
- ✅ Added `AutoQCTest` make target (lines 574-585)

**Key Findings**:
- SkylineBatch already integrated in Jamfile (confirmed)
- AutoQC now follows identical pattern to SkylineBatch
- Both use `vstest.console.exe` for test execution
- Targets marked `explicit` - only build when requested (no impact on default Skyline build)
- PANORAMAWEB_PASSWORD environment variable will be configured by TeamCity admin

**Revised Scope** (per discussion with developer):
- ❌ Skip mechanism NOT needed - TeamCity will have PANORAMAWEB_PASSWORD configured
- ✅ Focus on Jamfile targets and TeamCity execution script
- ✅ Tests will run with full credentials once environment variable is set

**Status**: ⏸️ **ON HOLD - Waiting for TeamCity Admin Return**

TeamCity admin on vacation for several weeks. All code changes complete and ready for testing. TeamCity server configuration changes deferred until admin returns.

## TeamCity Integration Details (For Admin)

### Build Targets Added to Jamfile.jam

The following targets are now available and follow the exact SkylineBatch pattern:

**Build AutoQC:**
```bash
bjam AutoQC.exe
```
- Builds `Executables/AutoQC/AutoQC.sln`
- Output: `bin/x64/Release/AutoQC.exe` (and supporting files)
- Depends on: `Skyline.exe` (must build Skyline first)

**Test AutoQC:**
```bash
bjam AutoQCTest
```
- Builds AutoQC solution with test project
- Runs: `vstest.console.exe bin/x64/Release/AutoQCTest.dll`
- **Requires**: `PANORAMAWEB_PASSWORD` environment variable

**Build SkylineBatch** (already existed):
```bash
bjam SkylineBatch.exe
```

**Test SkylineBatch** (already existed):
```bash
bjam SkylineBatchTest
```

### Proposed TeamCity Execution Script (3 Lines)

**Current configuration** (1 line):
```bash
pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe buildcheck=1 test=TestConnected.dll offscreen=0 teamcitytestdecoration=1 runsmallmoleculeversions=on
```

**Proposed configuration** (3 lines - add 2 new lines):
```bash
# Existing: Skyline TestConnected tests
pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe buildcheck=1 test=TestConnected.dll offscreen=0 teamcitytestdecoration=1 runsmallmoleculeversions=on

# New: SkylineBatch tests (no external dependencies)
vstest.console.exe pwiz_tools\Skyline\bin\x64\Release\SkylineBatchTest.dll

# New: AutoQC tests (requires PANORAMAWEB_PASSWORD environment variable)
vstest.console.exe pwiz_tools\Skyline\bin\x64\Release\AutoQCTest.dll
```

### Environment Variable Configuration

**Required for AutoQC tests:**
- **Variable**: `PANORAMAWEB_PASSWORD`
- **Source**: TeamCity admin is configuring this as a secure environment variable on build agents
- **Default test user**: `skyline_tester_admin@proteinms.net` (see `AutoQCTest/TestUtils.cs`)
- **Override user** (optional): Set `PANORAMAWEB_USERNAME` to use different account

**Test behavior:**
- ✅ With `PANORAMAWEB_PASSWORD` set: Full AutoQC test suite runs, including Panorama integration tests
- ❌ Without `PANORAMAWEB_PASSWORD`: Tests fail with clear error message (by design - alerts developers to set credentials)

### vstest.console.exe Path

TeamCity will need the full path to `vstest.console.exe`. Common locations:
```
C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\Extensions\TestPlatform\vstest.console.exe
C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\Extensions\TestPlatform\vstest.console.exe
C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe
```

Or use vswhere to find it dynamically (see `Build-AutoQC.ps1:251-255` for example).

### Build Dependencies

**For TeamCity build configuration:**
1. Build Skyline (existing)
2. Build SkylineBatch: `bjam SkylineBatch.exe` (may already exist)
3. Build AutoQC: `bjam AutoQC.exe` (NEW)
4. Run all three test suites (existing + 2 new lines above)

**No impact on default Skyline build**: AutoQC targets are marked `explicit` in Jamfile - only built when explicitly requested.

### Testing Checklist (Before Enabling in TeamCity)

- [ ] Verify `PANORAMAWEB_PASSWORD` environment variable is set on build agents
- [ ] Test build locally: `bjam AutoQC.exe`
- [ ] Test execution locally: `bjam AutoQCTest` (should pass if credentials set)
- [ ] Verify AutoQCTest.dll exists at expected path after build
- [ ] Run proposed 3-line script manually on build agent to verify paths

### Known Issues / Future Work

**TestEnableInvalid** - Currently disabled in `ConfigManagerTest.cs:203`
- Comment states: "Investigate why this test is failing. It was not running correctly. After fixing its threading issue, the test fails."
- Test attempts to enable invalid configuration, expects it to stay disabled
- Uses 1-second timeout with `TestUtils.WaitForCondition()`
- **Recommendation**: Investigate separately, not blocking for initial CI integration

**Out of Scope** (can be added later):
- ReSharper InspectCode for SkylineBatch/AutoQC
- Code coverage integration for batch tools
- Full Panorama test suite execution (current approach runs all tests with credentials)

## Files Changed

- `pwiz_tools/Skyline/Jamfile.jam` - Added AutoQC build/test targets
- `ai/todos/active/TODO-20251204_batch_tools_ci_integration.md` - This file

## Important Discovery: AssemblyInfo Generation Behavior

**Issue Found**: TeamCity reported AutoQC/Properties/AssemblyInfo.cs as an uncommitted file, even though AutoQC.exe target is marked `explicit`.

**Root Cause**: The `generate-skyline-AssemblyInfo.cs` call in Jamfile.jam (line 85) runs whenever bjam processes the Jamfile, **regardless of whether the target is built**. This is intentional - it ensures version information is always up-to-date.

**Solution**: Added AutoQC's AssemblyInfo.cs to .gitignore (following SkylineBatch pattern):
```
/pwiz_tools/Skyline/Executables/AutoQC/AutoQC/Properties/AssemblyInfo.cs
```

**Why AutoQC Builds in TeamCity**: TeamCity likely runs `bjam` without specific targets, which processes all non-explicit Jam rules including AssemblyInfo generation. The AutoQC.exe build target itself is still `explicit` and won't build unless requested, but the AssemblyInfo generation happens unconditionally.

**No Action Required**: This is expected behavior. The .gitignore addition resolves the uncommitted file issue.

## When TeamCity Admin Returns

1. Review this TODO document
2. Configure `PANORAMAWEB_PASSWORD` environment variable on build agents (if not already done)
3. Add AutoQC build target to build configuration: `bjam AutoQC.exe`
4. Add 2 new test execution lines to test configuration (see script above)
5. Test on build agent before enabling for all builds
6. Monitor first few builds for any issues

## Next Steps (When Work Resumes)

1. Test AutoQC build locally: `bjam AutoQC.exe`
2. Test AutoQCTest locally: `bjam AutoQCTest` (requires PANORAMAWEB_PASSWORD)
3. Create PR for review (can be draft/experimental while admin is away)
4. Coordinate with TeamCity admin upon return for server configuration
5. Monitor first CI runs for any issues
