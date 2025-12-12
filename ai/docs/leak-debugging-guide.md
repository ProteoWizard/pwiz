# Handle Leak Debugging Guide

This guide documents the methodology for identifying and fixing handle leaks in Skyline, developed during the Files view feature work (December 2025).

## End-to-End Workflow

The complete leak debugging workflow spans two phases:

### Phase 1: Leak Detection and Test Selection (Human)

The nightly test system runs tests on both the Integration branch and Master branch, reporting handle leak counts per test. A human reviews these results to:

1. **Compare Integration vs Master**: Significant difference indicates a leak introduced by the Integration branch
2. **Identify candidate tests**: Tests showing consistent handle leaks
3. **Calculate "handles leaked per second"**: Key metric for selecting the optimal test for isolation
   - Formula: `average_handles_leaked / test_duration_seconds`
   - Higher values = faster feedback during bisection
4. **Select the best test**: Balance between leak magnitude and test runtime
   - Example: `TestAuditLogSaving` leaked ~10 handles in ~64 seconds (~0.16 handles/sec)
   - A 10-iteration loop takes ~10 minutes and clearly shows the leak trend

### Phase 2: Leak Isolation and Fix (Claude Code)

Once a test is selected, Claude Code can autonomously:

1. **Establish baseline**: Run 10-iteration loop with `-ReportHandles -SortHandlesByCount`
2. **Identify leaking handle type**: User/GDI (forms/controls) vs kernel handles (threads/events)
3. **Bisect the test**: Systematically add `return;` statements to narrow down the leak location
4. **Analyze and fix**: Examine the isolated code, identify the leak, implement fix
5. **Validate**: Re-run the loop test to confirm handles are stable

This separation of concerns allows efficient collaboration: the human leverages server-side data to identify *what* to investigate, then hands off to Claude Code for the detailed *how* of isolation and fixing.

## Overview

Handle leaks occur when Windows handles (HWNDs, GDI objects, kernel objects) are allocated but not properly released. Over time, leaked handles accumulate and can cause:
- Memory growth
- Resource exhaustion
- Application instability
- Test failures in long-running test sessions

## Handle Types

Windows has several categories of handles:

| Category | Examples | Common Causes |
|----------|----------|---------------|
| **User handles** | HWNDs (windows, controls) | Forms/controls not disposed, parentless dialogs |
| **GDI handles** | Icons, pens, brushes, fonts, bitmaps | Graphics resources not disposed, Icon.FromHandle() |
| **Kernel handles** | Threads, events, mutexes, files, semaphores | Threads not joined, synchronization primitives not disposed |

## Detection Tools

### Nightly Test Reports

Skyline's nightly test system tracks handle counts:
- **Integration branch** vs **Master branch** comparison
- Per-test handle leak counts
- Memory growth trends

The nightly report format shows `<User+GDI>/<Total>` handles, e.g., `220/550`.

### TestRunner Handle Reporting

Use the `-ReportHandles` flag to see handle counts during test runs:

```powershell
# Basic handle reporting
.\ai\Run-Tests.ps1 -TestName TestAuditLogSaving -Loop 10 -ReportHandles

# Sort by count (leaking types rise to top over multiple runs)
.\ai\Run-Tests.ps1 -TestName TestAuditLogSaving -Loop 10 -ReportHandles -SortHandlesByCount
```

Output format:
```
[11:01]   2.0   TestAuditLogSaving   (en)   0 failures, 4.67/5.52/97.0 MB, 93/550 handles, 4 sec.
# Handles User: 26	GDI: 67	EtwRegistration: 145	Event: 110	...
```

The `# Handles` line shows breakdown by type. When sorted by count, leaking types accumulate and rise to the top of the list.

## Investigation Methodology

### Step 1: Establish a Reproducible Test Case

Find a test that triggers the leak consistently:

```powershell
# Run 10 iterations to see if handles grow
.\ai\Run-Tests.ps1 -TestName TestSuspectedLeak -Loop 10 -ReportHandles -SortHandlesByCount
```

Look for patterns:
- **Stable**: Handles fluctuate but don't trend upward (no leak)
- **Growing**: Handles increase steadily each run (leak detected)

Example of leak detection:
```
Run 2:  GDI: 53   User: 20   (baseline)
Run 3:  GDI: 56   User: 22   (+3, +2)
Run 4:  GDI: 59   User: 24   (+3, +2)
Run 5:  GDI: 62   User: 25   (+3, +1)
...
Run 11: GDI: 80   User: 31   (clearly growing)
```

### Step 2: Identify the Leaking Handle Type

Focus on the **largest leaking type first**:

- **User handles leaking**: Usually forms or controls not disposed
- **GDI handles leaking**: Usually graphics resources (icons, brushes, etc.)
- **Thread handles leaking**: Usually threads not properly joined
- **Event/Semaphore handles leaking**: Usually synchronization primitives

User handles often "bring" GDI handles with them (each window has associated GDI resources).

### Step 3: Bisect the Test

This is the key technique. Systematically narrow down where in the test the leak occurs:

1. **Find midpoint**: Add `return;` statement at approximately the middle of the test
2. **Run and compare**: If leak persists, it's in the first half; if not, it's in the second half
3. **Repeat**: Continue bisecting until you isolate the specific operation

Example bisection:
```csharp
protected override void DoTest()
{
    OpenDocument("test.sky");

    // ... first quarter of test ...

    return; // BISECT: Testing first quarter

    // ... rest of test ...
}
```

**Tip**: Choose meaningful boundaries (document operations, dialog shows, etc.) rather than arbitrary line counts.

### Step 4: Analyze the Leaking Code

Once isolated to a specific operation, examine:

1. **What handles are being created?**
   - `new Thread()`
   - `Icon.FromHandle()`, `Bitmap.GetHicon()`
   - `new Form()`, `new Control()`
   - `new AutoResetEvent()`, `new ManualResetEvent()`

2. **Are they being disposed?**
   - Check `Dispose()` implementations
   - Check `using` statements
   - Check static caching (vs. repeated creation)

3. **Is disposal actually called?**
   - Forms with `HideOnClose = true` aren't disposed on close
   - Event handlers may prevent GC
   - Circular references may prevent disposal

### Step 5: Fix and Validate

Common fixes:

| Problem | Solution |
|---------|----------|
| Icon created each time | Cache in static readonly field |
| Form not disposed | Ensure `HideOnClose = false` before final close |
| Thread not joined | Wait for thread completion in Dispose |
| Event handlers holding references | Unsubscribe in Dispose |

After fixing, validate with the same loop test:
```powershell
.\ai\Run-Tests.ps1 -TestName TestAuditLogSaving -Loop 10 -ReportHandles -SortHandlesByCount
```

Handles should now be stable (fluctuating but not trending upward).

## Case Study: AuditLogForm Icon Leak (December 2025)

### Detection (Human - Phase 1)

Nightly tests showed 30-41 handle leaks on the Integration branch vs 0-1 on master. The human reviewed per-test leak data from the LabKey server:

| Test | Avg Handles Leaked | Duration (sec) | Handles/sec |
|------|-------------------|----------------|-------------|
| TestAuditLog | 2.9 | 60.49 | 0.048 |
| TestAuditLogSaving | 10.3 | 63.87 | 0.161 |
| TestAuditLogTutorial | 15.4 | 64.87 | 0.237 |

**Test selection rationale**: `TestAuditLogSaving` was chosen because:
- High leak rate (10.3 handles/run) - clearly detectable in 10 iterations
- Moderate duration (~64 sec) - 10-iteration loop completes in ~10 minutes
- Good handles/sec ratio - efficient for bisection cycles

### Investigation (Claude Code - Phase 2)

1. **Established reproducible case**: 10-run loop showed ~3 GDI + ~1 User handles leaked per run
2. **Identified type**: GDI handles were the primary leak (User handles often follow)
3. **Bisected test**:
   - Full test: leaks present
   - First half: leaks present (smaller)
   - First quarter: leaks present
   - Just `OpenDocument()`: NO leak
   - `OpenDocument()` + `ShowAuditLog()`: leaks present
4. **Isolated to**: `SkylineWindow.ShowAuditLog()` call

### Root Cause

In `AuditLogForm.cs`:
```csharp
// BEFORE (leaking)
public AuditLogForm(...)
{
    InitializeComponent();
    Icon = Resources.AuditLog.ToIcon();  // Creates new HICON every time!
    ...
}
```

`ToIcon()` uses `Icon.FromHandle(bitmap.GetHicon())` which creates a native handle. Each form instance created a new handle that was never properly released.

### Fix

Cache the icon statically:
```csharp
// AFTER (fixed)
public partial class AuditLogForm : DocumentGridForm
{
    private static readonly Icon AUDIT_LOG_ICON = Resources.AuditLog.ToIcon();

    public AuditLogForm(...)
    {
        InitializeComponent();
        Icon = AUDIT_LOG_ICON;  // Reuses single cached icon
        ...
    }
}
```

### Validation

After fix, 10-run loop showed stable handles: GDI fluctuated 60-67, User 26-28, with no upward trend.

## Best Practices

### Prevention

1. **Cache static resources**: Icons, brushes, and other GDI objects that don't change should be cached statically
2. **Use `using` statements**: For any IDisposable that's created and used locally
3. **Implement IDisposable properly**: Classes that hold handles should implement IDisposable
4. **Unsubscribe event handlers**: In Dispose methods
5. **Be careful with `Icon.FromHandle()`**: The comment in `UtilUI.ToIcon()` warns "caller is responsible for disposing"

### Testing

1. **Run leak detection in CI**: Nightly tests should compare Integration vs Master handle counts
2. **Add loop tests for new features**: When adding code that creates handles, verify with loop tests
3. **Document handle-creating code**: Add comments noting disposal requirements

## Tooling Enhancements (December 2025)

The following improvements were made to support leak debugging:

### Run-Tests.ps1

New parameters:
- `-ReportHandles`: Enable handle count diagnostics
- `-SortHandlesByCount`: Sort handle types by count (descending) so leaking types rise to top

### TestRunnerLib/RunTests.cs

- Added `SortHandlesByCount` property and command-line option
- Enhanced `# Handles` output to include User and GDI handles (not returned by HandleEnumeratorWrapper)
- Combined all handle types (User, GDI, kernel) in single sortable list

### TestRunner/Program.cs

- Added `sorthandlesbycount` command-line argument (default: off)

## Future Vision: Automated Leak Detection Workflow

The current workflow requires a human to review nightly test results and select the optimal test for investigation. A planned enhancement would automate Phase 1 through an MCP (Model Context Protocol) server integration.

### Planned Architecture

```
┌─────────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│  LabKey Server      │────▶│  MCP Server      │────▶│  Claude Code    │
│  (Nightly Results)  │     │  (JSON API)      │     │  (Analysis)     │
└─────────────────────┘     └──────────────────┘     └─────────────────┘
```

### Envisioned Capabilities

1. **Automatic leak detection**: MCP server queries nightly results, compares Integration vs Master
2. **Optimal test selection**: Calculates "handles leaked per second" for all leaking tests
3. **Investigation recommendations**: Returns ranked list of tests worth investigating
4. **New slash command**: `/pw-review-leaks` would:
   - Fetch last night's test results via MCP
   - Identify tests with significant handle leaks
   - Recommend which test to investigate first
   - Optionally begin autonomous investigation

### Benefits

- **Faster response**: Leaks detected and investigated within hours of nightly run
- **Consistent methodology**: Same bisection approach applied systematically
- **Reduced manual overhead**: Human only needed to review and approve fixes
- **Complete audit trail**: All investigation steps documented in TODO files

### Implementation Status

- [x] Tooling for leak isolation (this guide)
- [x] Handle reporting improvements (`-ReportHandles`, `-SortHandlesByCount`)
- [ ] MCP server for LabKey integration (see `ai/todos/backlog/brendanx67/TODO-labkey_mcp_exception_triage.md` on ai-context branch)
- [ ] `/pw-review-leaks` slash command
- [ ] Autonomous investigation mode

This represents a path toward having Claude Code proactively identify and fix handle leaks with minimal human intervention, transforming leak debugging from a reactive manual process to an automated continuous improvement system.
