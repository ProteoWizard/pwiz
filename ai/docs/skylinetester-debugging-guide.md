# SkylineTester Debugging Guide

Guide for LLM-assisted debugging using SkylineTester with automated dev-build-test cycles.

> **See also:** [skylinetester-guide.md](skylinetester-guide.md) for comprehensive SkylineTester reference (tabs, file paths, common workflows).

## Overview

SkylineTester provides a GUI for running Skyline tests repeatedly, which is essential for reproducing intermittent failures. This guide documents workflows discovered during paired debugging sessions that enable Claude to iterate on debugging problems with minimal human intervention.

## Prerequisites

Before starting automated debugging cycles, **always ask the user for permission**. This workflow:
- Kills running processes (SkylineTester, TestRunner, Skyline)
- Rebuilds code
- Auto-launches tests

The user may have unsaved work or other reasons to not want processes killed.

## The Dev-Build-Test Loop

### Manual Steps (User Required)
```powershell
# 1. Kill existing processes
pwsh -Command "Get-Process -Name '*TestRunner*','*Skyline*' -ErrorAction SilentlyContinue | Stop-Process -Force"

# 2. Build using the proper script (NEVER call MSBuild directly!)
./pwiz_tools/Skyline/ai/Build-Skyline.ps1

# 3. Launch SkylineTester with auto-run
pwsh -Command "Start-Process 'C:\proj\scratch\pwiz_tools\Skyline\bin\x64\Debug\SkylineTester.exe' -ArgumentList '--autorun'"
```

### Critical: Always Use Build-Skyline.ps1

**NEVER** call MSBuild directly. Always use:
```powershell
./pwiz_tools/Skyline/ai/Build-Skyline.ps1
```

The build script:
- Finds MSBuild automatically via vswhere
- Fixes CRLF line endings
- Works from any directory
- Provides consistent output

### Kill Both Processes

When tests are running or hung, you must kill **both**:
- `SkylineTester.exe` - The test runner GUI
- `TestRunner.exe` - The actual test execution process
- `Skyline*.exe` - Any Skyline instances launched by tests

```powershell
pwsh -Command "Get-Process -Name '*TestRunner*','*Skyline*' -ErrorAction SilentlyContinue | Stop-Process -Force"
```

## The --autorun Flag

SkylineTester now supports `--autorun` to automatically start tests after the UI loads:

```powershell
Start-Process 'path\to\SkylineTester.exe' -ArgumentList '--autorun'
```

This enables fully automated test cycles without requiring the user to press F5.

**Implementation**: Added in `SkylineTesterWindow.cs`:
- Constructor parses `--autorun` flag and stores in `_autoRun` field
- `BackgroundLoadCompleted` handler triggers `Run()` via `BeginInvoke` when flag is set

## Log File Location

Test output is written to:
```
pwiz_tools/Skyline/SkylineTester.log
```

This log is overwritten each time SkylineTester starts a new test run.

## Detecting Hangs from Log Output

When a test hangs, the log stops updating. Key patterns:

### Successful Test Run
```
[DEBUG] WaitForComplete(line 85) poll #55, pane=AreaRelativeAbundanceGraphPane, result=True
[DEBUG] WaitForComplete(line 88) poll #1, pane=AreaRelativeAbundanceGraphPane, result=True
...
  0 failures, 5.48/150.1 MB, 73/530 handles, 179 sec.
```
Note: Test completes with "0 failures" and timing info.

### Hung Test
```
[DEBUG] WaitForComplete(line 156) poll #1, pane=RTLinearRegressionGraphPane, result=False
```
Then nothing - no more output. The test is hung if:
1. Several minutes pass with no new log entries
2. Poll count stops incrementing
3. No "failures" or completion line appears

## Diagnostic Printf Debugging

When investigating hangs, add Console.WriteLine statements:

### In Test Code (TestPerf/*.cs)
```csharp
private void WaitForComplete(GraphSummary graphSummary, [CallerLineNumber] int callerLine = 0)
{
    int pollCount = 0;
    WaitForConditionUI(() => CheckGraphComplete(graphSummary, ref pollCount, callerLine));
}

private static bool CheckGraphComplete(GraphSummary graphSummary, ref int pollCount, int callerLine)
{
    try
    {
        pollCount++;
        var pane = graphSummary.GraphControl.GraphPane;
        bool result = /* condition check */;
        Console.WriteLine($"[DEBUG] WaitForComplete(line {callerLine}) poll #{pollCount}, pane={pane?.GetType().Name}, result={result}");
        return result;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DEBUG] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
        throw;
    }
}
```

### In Production Code (Skyline/*.cs)
```csharp
// Track callback execution
Console.WriteLine($"[DEBUG] ClassName.MethodName START on thread {Thread.CurrentThread.ManagedThreadId}");
// ... method body ...
Console.WriteLine("[DEBUG] ClassName.MethodName END");
```

## Common Hang Patterns

### WaitForConditionUI Hang
**Symptom**: Poll #1 logged, poll #2 never appears
**Meaning**: The Invoke() to the UI thread isn't completing
**Possible causes**:
- UI thread blocked by something
- Deadlock between threads
- BeginInvoke callback blocking subsequent Invokes

### Investigation Steps
1. Add logging to both the test (poll side) and the production code (UI side)
2. Log thread IDs to confirm which thread is executing
3. Log entry/exit of callbacks that run on UI thread
4. Check if background calculation callbacks (ProductAvailableAction) are involved

## Automated Iteration Workflow

Once permission is granted, Claude can iterate on debugging:

1. **Make code changes** (add diagnostics, try fixes)
2. **Kill processes**: `Get-Process ... | Stop-Process -Force`
3. **Build**: `./pwiz_tools/Skyline/ai/Build-Skyline.ps1`
4. **Launch**: `Start-Process ... -ArgumentList '--autorun'`
5. **Wait** for tests to complete or hang (monitor log file)
6. **Read log**: Check `SkylineTester.log` for results
7. **Analyze** and repeat

### Detecting Completion vs Hang

Check log periodically. If no new output for 2+ minutes and no completion message, test is likely hung:

```powershell
# Read log to check status
Get-Content 'pwiz_tools/Skyline/SkylineTester.log' -Tail 20
```

## Future Improvements

Potential enhancements to this workflow:
- Script to fully automate kill-build-launch cycle
- Log monitoring with automatic hang detection
- Automatic log archival before each run

## Related Documentation

- [build-and-test-guide.md](build-and-test-guide.md) - Build system details
- [debugging-principles.md](debugging-principles.md) - General debugging methodology
