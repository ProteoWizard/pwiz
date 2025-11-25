# TODO: Consolidate Test Utilities Across Skyline, SkylineBatch, AutoQC

**Status**: Backlog  
**Priority**: Medium  
**Complexity**: Medium  
**Created**: 2025-11-24

## Problem

Test utility code is duplicated across Skyline, SkylineBatch, and AutoQC test projects. This creates maintenance overhead, inconsistent behavior, and missed opportunities for shared improvements.

### Current Duplication Examples

- **WaitForCondition**: Polling loop utility exists in multiple test suites with slight variations
- **File system helpers**: Temporary directory creation, cleanup logic scattered across projects
- **Threading test patterns**: Background task verification, timeout handling reimplemented multiple times

### Impact

- **Maintenance burden**: Bug fixes and improvements must be replicated across codebases
- **Inconsistent behavior**: Different timeout defaults, error messages, retry logic
- **Discoverability**: Developers unaware shared patterns exist, leading to further duplication

## Proposed Solution

### Phase 1: Identify Consolidation Candidates

Audit test projects for:
- Polling/wait utilities (WaitForCondition, timeout loops)
- File system test helpers (temp directories, resource extraction)
- UI automation patterns (if applicable to batch tools)
- Common assertion extensions
- Mock/stub infrastructure

### Phase 2: Create Shared Test Library

**Option A**: Extend existing `CommonUtil` or `Common` projects with test-specific namespace
- Pros: Single dependency, natural evolution of shared code
- Cons: Mixes production and test concerns

**Option B**: New `Skyline.TestUtilities` project referenced by all test assemblies
- Pros: Clear separation, explicit test-only scope
- Cons: Additional project/assembly to maintain

### Phase 3: Migrate Incrementally

1. Move highest-value utilities first (WaitForCondition, temp file helpers)
2. Update consumers project-by-project
3. Remove duplicated implementations
4. Document shared utilities in test coding standards

## Example: WaitForCondition Consolidation

### Current State
```csharp
// SkylineBatch: ConfigManagerTest.cs
void WaitForCondition(Func<bool> condition, int timeoutMs = 5000) { /* implementation A */ }

// AutoQC: ConfigManagerTest.cs  
void WaitForCondition(Func<bool> condition) { /* implementation B - no timeout param */ }

// Skyline: AbstractUnitTest.cs (different signature entirely)
```

### Target State
```csharp
// Skyline.TestUtilities / CommonUtil
public static class TestWaitHelpers
{
    public static void WaitForCondition(Func<bool> condition, int timeoutMs = 5000, string description = null)
    {
        // Single canonical implementation with proper error messages
    }
}
```

## Benefits

- **Single source of truth** for common test patterns
- **Improved maintainability**: One place to fix bugs, add features
- **Consistency**: Same timeout defaults, error messages across all tests
- **Faster test development**: Reusable utilities accelerate new test authoring

## Risks & Mitigations

**Risk**: Breaking existing tests during migration  
**Mitigation**: Migrate incrementally; run full test suites after each utility consolidation

**Risk**: Over-abstraction / premature generalization  
**Mitigation**: Consolidate only after seeing 3+ real usages; prefer simple copies over complex frameworks

## Related Work

- AutoQC dormant thread issue revealed need for shared WaitForCondition
- SkylineBatch cleanup identified duplicated file system helpers
- Future CI integration will benefit from consistent test infrastructure

## Next Steps

1. **Audit phase**: Search for `WaitForCondition`, temp file patterns across test projects
2. **Design decision**: Choose Option A (extend CommonUtil) vs Option B (new project)
3. **Pilot migration**: Move WaitForCondition as proof-of-concept
4. **Document**: Update test coding guidelines with consolidated utilities

## References

- Related to TODO-ci_integration_resharper.md (shared test utilities enable better CI)
- Discovered during: Skyline/work/20251124_batch_tools_warning_cleanup branch
- Discussion context: AutoQC ConfigManagerTest.cs WaitForCondition dormant thread fix
