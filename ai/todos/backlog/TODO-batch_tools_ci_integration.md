# TODO: Integrate ReSharper Inspection into CI (TeamCity/Jamfile)

**Status**: Backlog  
**Priority**: High  
**Complexity**: Medium-High  
**Created**: 2025-11-24

## Problem

ReSharper code inspections for SkylineBatch and AutoQC are currently manual (invoked via `Build-*.ps1 -RunInspection`). This creates risk:
- Developers may forget to run inspection before commit
- Warnings can accumulate silently until someone manually checks
- No enforcement mechanism for maintaining zero-warning baseline

Additionally, **AutoQC TestEnableInvalid** test is currently disabled due to pre-existing failure exposed during cleanup. This masks a real configuration validation issue.

## Current State

### Inspection Execution
- **Skyline**: Has TeamCity integration and Jamfile inspection targets (established baseline)
- **SkylineBatch**: Manual via `Build-SkylineBatch.ps1 -RunInspection` (~2-5 min)
- **AutoQC**: Manual via `Build-AutoQC.ps1 -RunInspection` (~2-5 min)

### DotSettings Synchronization
- **Automated** as of 2025-11-24: `Sync-DotSettings.ps1` runs on every build
- Ensures consistent baseline across Skyline, SkylineBatch, AutoQC
- LocalizableElement intentionally lowered (WARNING → HINT) for batch tools

### Test Status
- **SkylineBatch**: All tests passing ✅
- **AutoQC**: TestEnableInvalid disabled (pre-existing failure, not caused by recent cleanup)

## Proposed Solution

### Phase 1: CI Inspection Integration (SkylineBatch, AutoQC)

Add ReSharper inspection to TeamCity build configurations:
```xml
<!-- TeamCity build step example -->
<runner type="jetbrains_inspectcode">
  <parameters>
    <param name="jetbrains_resharper_platform">x64</param>
    <param name="jetbrains_resharper_solution">pwiz_tools/Skyline/Executables/SkylineBatch/SkylineBatch.sln</param>
    <param name="jetbrains_resharper_profile">SkylineBatch.sln.DotSettings</param>
    <param name="jetbrains_resharper_severity">WARNING</param>
    <param name="jetbrains_resharper_cache_home">%system.teamcity.build.tempDir%/.inspectcode-cache</param>
  </parameters>
</runner>
```

**Key configuration**:
- Fail build on WARNING or higher (matches `--severity=WARNING` in Build scripts)
- Use persistent cache directory for faster reruns
- Run after successful compilation (inspection requires built assemblies)

### Phase 2: Jamfile Integration (Optional)

For local developer workflow consistency:
```jam
# libraries/boost-build/example-jamfile-snippet
actions inspect-skyline-batch {
    jb inspectcode "$(SOLUTION)" --profile="$(PROFILE)" --output="$(OUTPUT)" --severity=WARNING
}
```

**Decision point**: May be redundant given PowerShell build scripts already support `-RunInspection`.

### Phase 3: Fix Disabled AutoQC Test

**TestEnableInvalid failure details**:
- Pre-existing issue (not introduced by recent cleanup)
- Exposed when dormant thread was fixed in ConfigManagerTest
- Likely related to configuration state validation in `AutoQcConfigManager`

**Resolution steps**:
1. Debug root cause (investigate ConfigRunner validation logic)
2. Fix underlying issue or update test expectations
3. Re-enable test in AutoQC test suite
4. Verify in CI

## Benefits

- **Automated enforcement**: Zero-warning baseline maintained without manual vigilance
- **Early detection**: Catch regressions immediately after commit, not days/weeks later
- **Consistent standards**: CI verifies same inspection rules locally tested via build scripts
- **Reduced friction**: Developers get fast feedback (inspection ~2-5 min vs full Skyline ~20-25 min)

## Implementation Checklist

- [ ] Add TeamCity build step for SkylineBatch inspection
- [ ] Add TeamCity build step for AutoQC inspection
- [ ] Configure failure threshold (WARNING or higher)
- [ ] Set up inspection cache directories for performance
- [ ] Verify builds fail on introduced warnings (negative test)
- [ ] Document CI configuration in team wiki/README
- [ ] (Optional) Add Jamfile inspection targets
- [ ] Debug and fix AutoQC TestEnableInvalid
- [ ] Re-enable TestEnableInvalid in AutoQC test suite

## Risks & Mitigations

**Risk**: Inspection duration adds significant CI time  
**Mitigation**: Run inspection in parallel with other build steps; cache improves repeat times to ~1-2 min

**Risk**: Transient ReSharper CLI failures (tooling issues)  
**Mitigation**: Add retry logic; monitor failure patterns; maintain fallback to manual inspection

**Risk**: TeamCity/Jamfile configuration drift from local build scripts  
**Mitigation**: Single source of truth for severity/profile via DotSettings; periodic sync verification

## Related Work

- **Sync-DotSettings.ps1**: Ensures CI and local inspections use identical configuration
- **TODO-consolidate_test_utilities.md**: Shared test infrastructure improves test reliability (helps fix TestEnableInvalid)
- **Skyline inspection baseline**: Established TeamCity pattern to replicate for batch tools

## References

- Skyline TeamCity configuration (model for batch tool integration)
- ReSharper CLI docs: https://www.jetbrains.com/help/resharper/InspectCode.html
- Build scripts: `Build-SkylineBatch.ps1`, `Build-AutoQC.ps1` (contain inspection command examples)
- Disabled test: `AutoQC/AutoQCTest/ConfigManagerTest.cs::TestEnableInvalid`
