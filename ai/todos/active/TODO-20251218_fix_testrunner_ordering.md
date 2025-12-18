# TODO-20251218_fix_testrunner_ordering.md

## Branch Information
- **Branch**: `Skyline/work/20251218_fix_testrunner_ordering`
- **Base**: `master`
- **Created**: 2025-12-18
- **Status**: 🚧 In Progress
- **PR**: (pending)
- **Objective**: Fix TestRunner test ordering bugs that conflate perftests=on with nightly runs

## Background

During tutorial screenshot testing, we discovered that test ordering was unexpected. Tests with `NoLeakTesting` attribute were being pushed to the end of the non-perf test list, even when running interactively with `perftests=on`.

**Root cause**: A recent change to optimize nightly test runs (separating 9-hour and 12-hour runs by leak test coverage) incorrectly used `perftests` flag instead of `asNightly` flag. This caused the leak test reordering logic to apply to ALL runs with `perftests=on`, not just actual nightly runs.

Additionally, when tests are explicitly specified via file or command line, their order is carefully preserved in an array, but then immediately discarded by an alphabetical sort.

## Current Behavior (Buggy)

With `perftests=on`:
1. `DoNotLeakTest` flag is inverted for all tests
2. Pass 2 reorders tests by `DoNotLeakTest` status
3. Result: Tests with `NoLeakTesting` attribute run last in their category

Test ordering always sorts alphabetically with perf tests last, ignoring user-specified order.

## Expected Behavior

1. `DoNotLeakTest` inversion should only occur during actual nightly runs (`asNightly && perftests`)
2. Pass 2 reordering should only occur during nightly runs
3. When tests are explicitly specified, their order should be preserved

## Bugs to Fix

### BUG-1: DoNotLeakTest inversion applies to non-nightly runs

**File**: `pwiz_tools/Skyline/TestRunner/Program.cs`
**Location**: Lines 1432-1440

**Current code**:
```csharp
if (!perftests)
{
    testList.RemoveAll(test => test.IsPerfTest);
    unfilteredTestList.RemoveAll(test => test.IsPerfTest);
}
else
{
    // Take advantage of the extra time available in perftest runs to do the leak tests we
    // skip in regular nightlies - but skip leak tests covered in regular nightlies
    foreach (var test in unfilteredTestList)
    {
        test.DoNotLeakTest = !test.DoNotLeakTest;
    }
}
```

**Fix**: Change `else` to `else if (asNightly)`:
```csharp
if (!perftests)
{
    testList.RemoveAll(test => test.IsPerfTest);
    unfilteredTestList.RemoveAll(test => test.IsPerfTest);
}
else if (asNightly)
{
    // Take advantage of the extra time available in nightly perftest runs...
```

### BUG-2: Pass 2 reordering applies to non-nightly runs

**File**: `pwiz_tools/Skyline/TestRunner/Program.cs`
**Location**: Lines 1756-1759

**Current code**:
```csharp
// Move any tests with the NoLeakTesting attribute to the front of the list for pass 2, as we skipped them in pass 1
testList = testList.Where(t => t.DoNotLeakTest)
    .Concat(testList.Where(t => !t.DoNotLeakTest))
    .ToList();
```

**Fix**: Wrap in `if (asNightly)`:
```csharp
// Move any tests with the NoLeakTesting attribute to the front of the list for pass 2, as we skipped them in pass 1
if (asNightly)
{
    testList = testList.Where(t => t.DoNotLeakTest)
        .Concat(testList.Where(t => !t.DoNotLeakTest))
        .ToList();
}
```

### BUG-3: User-specified test order is discarded

**File**: `pwiz_tools/Skyline/TestRunner/Program.cs`
**Location**: Lines 1991-1992

**Current code**:
```csharp
// Sort tests alphabetically, but run perf tests last for best coverage in a fixed amount of time.
return testList.OrderBy(e => e.IsPerfTest).ThenBy(e => e.TestMethod.Name).ToList();
```

**Fix**: Only sort when tests were not explicitly specified:
```csharp
// Sort tests alphabetically, but run perf tests last for best coverage in a fixed amount of time.
// However, if tests were explicitly specified, preserve that order.
if (testNames.Count == 0)
{
    return testList.OrderBy(e => e.IsPerfTest).ThenBy(e => e.TestMethod.Name).ToList();
}
else
{
    // User explicitly specified test order - preserve it exactly
    return testList;
}
```

## Key Variables

- `perftests` - Command line flag `perftests=on`
- `asNightly` - Computed as `offscreen && qualityMode` (line 1409)
- `testNames` - List of explicitly specified test names (from file or command line)
- `DoNotLeakTest` - Per-test flag from `NoLeakTesting` attribute

## Tasks

- [ ] Fix BUG-1: Add `asNightly` guard to DoNotLeakTest inversion
- [ ] Fix BUG-2: Add `asNightly` guard to pass 2 reordering
- [ ] Fix BUG-3: Preserve user-specified test order
- [ ] Update comments to clarify nightly-only behavior
- [ ] Test with `perftests=on` to verify ordering is now correct
- [ ] Verify nightly behavior is unchanged (if possible)

## Testing

1. Run SkylineTester with tutorial tests and `perftests=on`
2. Verify tests run in the order specified in the test list file
3. Verify tests with `NoLeakTesting` attribute are NOT pushed to the end

## Files to Modify

- `pwiz_tools/Skyline/TestRunner/Program.cs`
