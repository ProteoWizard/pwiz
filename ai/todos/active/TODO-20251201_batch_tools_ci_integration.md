# TODO-20251201_batch_tools_ci_integration.md

## Branch Information
- **Branch**: `Skyline/work/20251201_batch_tools_ci_integration`
- **Created**: 2025-12-01
- **Completed**: (pending)
- **Status**: 🚧 In Progress
- **PR**: (pending)
- **Objective**: Enable CI to reliably build and test SkylineBatch and AutoQC with graceful skipping of Panorama-auth tests

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
