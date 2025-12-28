# TODO-program_support_interface.md

## Branch Information
- **Branch**: `Skyline/work/YYYYMMDD_program_support_interface`
- **Base**: `master`
- **Created**: (pending)
- **Completed**: (pending)
- **Status**: Backlog
- **PR**: (pending)
- **Objective**: Establish IProgramSupport singleton pattern in Common layer for application-level services

## Background

Currently, application-level functionality like `Program.ReportException()` is only accessible from the Skyline application layer. Code in Common libraries (e.g., PanoramaClient) cannot access these services, leading to patterns like silently swallowing unexpected exceptions.

### Problem Example

In `PanoramaFilePicker.cs`, exceptions are caught but only specific types are shown to the user:

```csharp
catch (Exception ex)
{
    if (ex is InvalidDataException
        || ex is IOException
        || ex is UnauthorizedAccessException)
    {
        CommonAlertDlg.ShowException(FormUtil.FindTopLevelOwner(this), ex);
    }
    // Other exceptions silently swallowed - no way to report them
}
```

This caused a silent failure when LabKey 25.11 changed date formats - users saw empty file lists with no error message.

### Existing Pattern: IProgressMonitor

`IProgressMonitor` solves a different problem - it has many implementations (LongWaitDlg, CommandProgressMonitor, SilentProgressMonitor, etc.) and is passed as a parameter to specific operations.

### Proposed Pattern: IProgramSupport Singleton

For application-level services that have one implementation per application, a singleton pattern is more appropriate:

- `pwiz.Common.ProgramSupport` - Static class holding the singleton
- `IProgramSupport` - Interface defining application-level services
- One implementation per application (Skyline, SkylineBatch, AutoQC)

## Proposed Design

### Interface (pwiz.Common.SystemUtil)

```csharp
public interface IProgramSupport
{
    /// <summary>
    /// Report an unexpected exception to the application's error reporting system.
    /// </summary>
    void ReportException(Exception ex);

    // Future methods as needed:
    // void LogMessage(string message, LogLevel level);
    // string ApplicationName { get; }
    // Version ApplicationVersion { get; }
}
```

### Singleton Holder (pwiz.Common.SystemUtil)

```csharp
public static class ProgramSupport
{
    public static IProgramSupport Instance { get; set; }

    public static void ReportException(Exception ex)
    {
        Instance?.ReportException(ex);
    }
}
```

### Application Registration

**Skyline (Program.cs):**
```csharp
// Program implements IProgramSupport
ProgramSupport.Instance = new SkylineProgramSupport();
// Or if Program itself implements the interface:
// ProgramSupport.Instance = Program;
```

**SkylineBatch/AutoQC:**
```csharp
// SharedBatch provides implementation
ProgramSupport.Instance = new SharedBatchProgramSupport();
```

### Usage in Common Layer

```csharp
catch (Exception ex)
{
    if (ex is OperationCanceledException)
        return;
    if (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        CommonAlertDlg.ShowException(owner, ex);
    else
        ProgramSupport.ReportException(ex);  // Now accessible from Common
}
```

## Implementation Tasks

- [ ] Create `IProgramSupport` interface in `pwiz.Common.SystemUtil`
- [ ] Create `ProgramSupport` static class in `pwiz.Common.SystemUtil`
- [ ] Implement `IProgramSupport` in Skyline (wrap `Program.ReportException`)
- [ ] Register Skyline's implementation in `Program.Main()`
- [ ] Implement `IProgramSupport` in SharedBatch for SkylineBatch/AutoQC
- [ ] Register SharedBatch implementation in SkylineBatch and AutoQC startup
- [ ] Move relevant logic from `ExceptionUtil` to Common layer
- [ ] Update `PanoramaFilePicker` exception handling to use new pattern
- [ ] Consider what other `Program` functionality could move to this interface

## Related

- PR #3725 - PanoramaFilePicker date parsing fix (master)
- Commit 3d544a023b - Same fix on Skyline/skyline_25_1 release branch
- `ExceptionUtil.DisplayOrReportException()` - Existing pattern in Skyline
- `IProgressMonitor` - Related but different pattern (many implementations, passed as parameter)

## Notes

- This is an incremental improvement - start with `ReportException`, expand interface as needed
- Consider thread safety if `Instance` could be accessed from multiple threads
- Unit tests may want to set a mock `IProgramSupport` for testing exception paths
