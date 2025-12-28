# TODO-ban_runtime_helpers_gethashcode.md

## Summary

Add a CodeInspection check to warn against using `RuntimeHelpers.GetHashCode()` as a unique identifier.

## Background

During PR #3730 (Relative Abundance graph performance), we discovered an intermittent bug caused by using `RuntimeHelpers.GetHashCode()` as dictionary keys. The assumption was that it provides unique values per object instance, but this is **not guaranteed**.

### The Problem

`RuntimeHelpers.GetHashCode()` returns a 32-bit integer. On 64-bit Windows with a 64-bit address space, collisions are inevitable when you have thousands of objects. We observed collisions with ~5,000 `PeptideGroupDocNode` objects - different proteins with different names were getting the same hash code.

### Historical Context

- `RuntimeHelpers.GetHashCode()` has existed since .NET 2.0
- On 32-bit Windows, it may have been more reliable (addresses fit in 32 bits)
- On 64-bit Windows, it's essentially a content-reducing hash with collision potential
- The Skyline codebase predates widespread knowledge of this function, which led to the creation of `Identity.GlobalIndex` - a monotonically increasing counter that IS guaranteed unique

### The Fix Pattern

Use `Identity.GlobalIndex` instead - assigned via `Interlocked.Increment()`, guaranteed unique per object instance, and still has billions of headroom.

## Proposed Implementation

### Option A: Banned API Check

Add to CodeInspection a check that flags any usage of `RuntimeHelpers.GetHashCode()`:

```csharp
// Error: RuntimeHelpers.GetHashCode() is not guaranteed unique.
// Use Identity.GlobalIndex for identity-based dictionary keys.
// If you need this for debugging only, use CommonUtil.GetDebugHashCode().
```

### Option B: Wrapper Method

Create a wrapper that makes the limitation explicit:

```csharp
// In pwiz.Common.SystemUtil or similar
public static class IdentityUtil
{
    /// <summary>
    /// Returns an identity hash code for debugging purposes only.
    /// WARNING: This is NOT guaranteed unique - collisions occur with large object counts.
    /// For unique identity, use Identity.GlobalIndex instead.
    /// </summary>
    public static int GetDebugHashCode(object obj)
    {
        return RuntimeHelpers.GetHashCode(obj);
    }
}
```

Then ban direct `RuntimeHelpers.GetHashCode()` usage and require the wrapper.

## Scope

1. Add CodeInspection check (either banned API or require wrapper)
2. Search codebase for existing usages and fix/acknowledge them
3. Document in CRITICAL-RULES.md or similar

## Estimated Effort

Small - primarily adding the inspection rule and reviewing existing usages.

## References

- PR #3730 - Where the bug was discovered and fixed
- `pwiz_tools/Skyline/Controls/Graphs/SummaryRelativeAbundanceGraphPane.cs` - The fixed code
- `ai/docs/architecture-data-model.md` - Documents `Identity.GlobalIndex` pattern
