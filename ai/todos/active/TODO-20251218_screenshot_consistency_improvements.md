# TODO-20251218_screenshot_consistency_improvements.md

## Branch Information
- **Branch**: `Skyline/work/20251218_screenshot_consistency`
- **Base**: `master`
- **Created**: 2025-12-18
- **Status**: 🚧 In Progress
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

- [x] Investigate AllChromatogramsGraph timing issues
- [x] Implement IVersionProvider for audit log version display
- [x] Implement frozen progress for AllChromatogramsGraph (file bars, total bar, elapsed time)
- [x] Add PauseForAllChromatogramsGraphScreenShot() shortcut
- [x] Add ScreenshotPreviewForm Ctrl+Alt+S to save diff images
- [x] Add focus rectangle removal before screenshots
- [ ] Address X-axis label orientation inconsistency
- [ ] Enhance ImageComparer with diff amplification features
- [ ] Fix CleanupBorder algorithm for consistent 1px borders

## Progress Log

### 2025-12-20: API Standardization

Standardized all test files to use the new frozen progress API:
- Replaced `SetFreezeProgressPercent(percent, elapsedTime)` → `SetFrozenProgress(threshold, elapsedTime, totalProgress, fileProgress)`
- Replaced `SetFreezeProgressPercent(null, null)` → `ReleaseFrozenProgress()`
- Replaced `PauseForScreenShot<AllChromatogramsGraph>(...)` → `PauseForAllChromatogramsGraphScreenShot(...)`

All 8 tutorial tests updated to use new API consistently.

### 2025-12-20: AllChromatogramsGraph Frozen Progress & Screenshot Improvements

**Major accomplishments:**

#### 1. AllChromatogramsGraph Frozen Progress System
Complete control over the import progress UI during screenshot capture:

**API: `SetFrozenProgress(freezeThreshold, elapsedTime, totalProgress, fileProgress)`**
- `freezeThreshold`: Percent at which to freeze (triggers when any file exceeds this)
- `elapsedTime`: Fixed elapsed time string to display (e.g., "00:00:35")
- `totalProgress`: Fixed total progress bar percentage
- `fileProgress`: Dictionary mapping filename to progress percentage for each file bar

**Files changed:**
- `pwiz_tools/Skyline/Controls/Graphs/AllChromatogramsGraph.cs`:
  - Added `_frozenFileProgress` and `_frozenTotalProgress` fields
  - `SetFrozenProgress()` and `ReleaseFrozenProgress()` methods
  - `Finish()` now skips hiding UI when frozen (keeps progress bar/Cancel button visible)
  - `ReleaseFrozenProgress()` completes UI state if import finished while frozen
  - Added `ProgressBarTotal` property to expose progress bar for screenshot processing
- `pwiz_tools/Skyline/Controls/Graphs/FileProgressControl.cs`:
  - Extended `IStateProvider.GetFrozenProgress(MsDataFileUri)` with partial filename matching

#### 2. PauseForAllChromatogramsGraphScreenShot() Shortcut
Added convenience method in `TestFunctional.cs` that:
- Gets the AllChromatogramsGraph form
- Calls existing `FillProgressBar()` extension to paint over animated progress bar with static representation

**Usage:**
```csharp
var allChrom = WaitForOpenForm<AllChromatogramsGraph>();
allChrom.SetFrozenProgress(41, "00:00:35", 33, new Dictionary<string, int>
{
    { "F_A_018", 44 },
    { "M_A_001", 40 }
});
WaitForConditionUI(() => allChrom.IsProgressFrozen());
PauseForAllChromatogramsGraphScreenShot("Importing results form");
allChrom.ReleaseFrozenProgress();
```

#### 3. ScreenshotPreviewForm Diff Image Saving
- Added Ctrl+Alt+S to save diff image to `ai\.tmp` folder
- Added Ctrl+Alt+C to copy diff image to clipboard
- Added `GetDiffFileName()` and `GetAiTmpFolder()` to `ScreenshotFile` class

#### 4. Focus Rectangle Removal
- Added `RemoveFocusCues()` in `ScreenshotManager.cs`
- Creates temporary off-screen Button to steal focus from controls that show focus rectangles
- Automatically called before screenshot capture

#### 5. User32 Enhancements
- Added `WM_CHANGEUISTATE` message constant
- Added `HideFocusCues()` and `ShowFocusCues()` methods (available but focus-stealing approach preferred)

**Tests updated with frozen progress values:**
- MethodRefinementTutorialTest (s-03) - MethodRefine - exact match achieved
- GroupedStudies1TutorialTest (s-03) - GroupedStudies - exact match achieved
- Ms1FullScanFilteringTutorial (s-09) - MS1Filtering
- TargetedMSMSTutorialTest (s-15) - PRM
- SmallMolLibrariesTutorialTest (s-08) - SmallMoleculeIMSLibraries - re-recorded
- DiaSwathTutorialTest (s-13) - DIA-TTOF, DIA-QE
- DiaSwathTutorialTest (s-14) - DIA-PASEF
- DiaUmpireTutorialTest (s-17) - DIA-Umpire-TTOF
- DriftTimePredictorTutorialTest (s-05) - IMSFiltering

**Remaining challenges:**
- Progress bar animation (Windows glow effect) - use Remote Desktop or `FillProgressBar()` processShot
- Graph extraction boundary for non-SRM data - may need time-based freeze or re-recording
- Focus rectangles on some controls (combo boxes) - testing off-screen focus sink approach

### 2025-12-18: IVersionProvider Implementation Complete

Implemented `IVersionProvider` pattern for audit log version display following the existing `ITimeProvider` pattern:

**Files changed:**
- `pwiz_tools/Skyline/Model/AuditLog/AuditLogEntry.cs` - Added `IVersionProvider` interface and `VersionProvider` static property. Changed `_skylineVersion` from field to property that checks provider first.
- `pwiz_tools/Skyline/TestTutorial/AuditLogTutorialTest.cs` - Added `TestVersionProvider` class and made both `TestTimeProvider` and `TestVersionProvider` implement `IDisposable` with save/restore pattern for proper test isolation.

**Key improvement:** Both provider classes now:
1. Save previous provider on construction
2. Set themselves as current provider
3. Restore previous provider on `Dispose()`
4. Can be used with `using` statements for guaranteed cleanup

Version is now set to "Skyline 25.1.0 (64-Bit)" for consistent screenshots that won't change on version bumps.

## Priority

Medium - These are quality-of-life improvements for screenshot maintenance. Not blocking releases but reduce manual review burden.
