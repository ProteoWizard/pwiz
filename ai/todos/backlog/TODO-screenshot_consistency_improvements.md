# TODO-screenshot_consistency_improvements.md

## Branch Information
- **Branch**: (to be created when work starts)
- **Base**: `master`
- **Created**: (pending)
- **Status**: Backlog
- **PR**: (pending)
- **Objective**: Make tutorial screenshot captures deterministic to eliminate false-positive diffs

## Background

During the 26.0.9 screenshot review (TODO-20251215_update_screenshots_26_0_9.md), several categories of screenshots had to be reverted because they could not be captured consistently. These represent classes of non-deterministic behavior that cause spurious diffs between runs.

## Issues to Address

### 1. Import Progress Monitor (AllChromatogramsGraph)

**Affected screenshots**: GroupedStudies s-03, MethodRefine s-03, MS1Filtering s-09, PRM s-15, DIA-PASEF s-14, DIA-QE s-13, SmallMolIMSLibraries s-08 (en, ja, zh-CHS)

**Issue**: Import progress monitor form shows timing-dependent state that varies between runs. The progress percentage, elapsed time, and file being processed can all differ.

**Prior work**: Some work done to improve consistency, but still not 100% accurate.

**Proposed solution**: Investigate making progress state deterministic during screenshot capture mode. Options:
- Wait for specific progress milestones before capture
- Mock the progress reporting during screenshot mode
- Capture at completion state only

### 2. Audit Log Version Display

**Affected screenshots**: AuditLog s-23 (reverted - only version change)

**Issue**: Audit Log form displays Skyline version number, causing false-positive diffs on version bumps.

**Prior work**: `ITimeProvider` pattern already implemented for consistent dates/times in audit log screenshots.

**Proposed solution**: Create an `IVersionProvider` interface following the same pattern:

```csharp
public interface IVersionProvider
{
    string Version { get; }
}

public static IVersionProvider VersionProvider { get; set; }

// Usage in version display code:
public static string SkylineVersion =>
    VersionProvider?.Version ??
    (string.IsNullOrEmpty(Install.Version)
        ? string.Format(@"Developer build, document format {0}", DocumentFormat.CURRENT)
        : Install.Version)
    + (Install.Is64Bit ? @" (64-Bit)" : string.Empty);
```

This would allow tests to set a fixed version string (e.g., "26.0.9.999 (64-Bit)") for screenshot consistency.

### 3. X-Axis Label Orientation

**Affected screenshots**: MS1Filtering s-21 (ja, zh-CHS only)

**Issue**: X-axis labels on graphs can flip between horizontal and vertical orientation depending on available space. This is inconsistent between runs and causes screenshot diffs even when no functional change occurred.

**Proposed solutions**:
- Make graphs slightly wider to ensure labels consistently remain horizontal
- Implement deterministic label orientation during screenshot capture mode
- Force a specific orientation based on locale/graph type

### 4. ImageComparer Diff Amplification

**Affected screenshots**: DDASearch s-16 (1px diff), ExistingQuant s-07 (1px diff), IMSFiltering s-14/s-17

**Issue**: When ImageComparer shows only 1-2 pixel differences, it's nearly impossible to visually locate the changed pixels to diagnose the root cause.

**Root cause hypothesis**: The `ScreenshotProcessingExtensions.CleanupBorder` algorithm that draws borders around screenshots (because window borders are translucent) may have an off-by-one error for certain window sizes. In Windows 11, curved corners are translucent and must be reproduced consistently.

**Proposed enhancements to ImageComparer**:
1. Add a "diff-only" view mode that removes the image and paints only the diff pixels on a white background
2. Add an "Amplification" slider that expands the visual area around each changed pixel (radius 1-10)
3. This would help identify exact pixel locations and potentially fix the border drawing algorithm

## Reference: Existing ITimeProvider Pattern

The `ITimeProvider` mechanism is already working for audit log date/time consistency:

**Interface** (`pwiz_tools/Skyline/Model/AuditLog/AuditLogEntry.cs:560-573`):
```csharp
public interface ITimeProvider
{
    DateTime Now { get; }
}

public static ITimeProvider TimeProvider { get; set; }

public static DateTime Now
{
    get { return TimeProvider?.Now ?? DateTime.UtcNow; }
}
```

**Test Implementation** (`pwiz_tools/Skyline/TestTutorial/AuditLogTutorialTest.cs`):
```csharp
public class TestTimeProvider : AuditLogEntry.ITimeProvider
{
    private readonly DateTime _startTime;
    private TimeSpan _elapsedTime = TimeSpan.Zero;
    private Random _random = new Random(1); // Consistent random series

    public TestTimeProvider()
    {
        var localTime = new DateTime(2025, 1, 1, 9, 35, 0, DateTimeKind.Local);
        _startTime = localTime.ToUniversalTime();
    }

    public DateTime Now
    {
        get
        {
            _elapsedTime += TimeSpan.FromSeconds(_random.Next(2, 10));
            return _startTime.Add(_elapsedTime);
        }
    }
}
```

## Tasks

- [ ] Investigate AllChromatogramsGraph timing issues
- [ ] Implement IVersionProvider for audit log version display
- [ ] Address X-axis label orientation inconsistency
- [ ] Enhance ImageComparer with diff amplification features
- [ ] Fix CleanupBorder algorithm for consistent 1px borders

## Priority

Medium - These are quality-of-life improvements for screenshot maintenance. Not blocking releases but reduce manual review burden.
