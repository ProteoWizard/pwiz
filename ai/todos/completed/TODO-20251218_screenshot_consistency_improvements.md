# TODO-20251218_screenshot_consistency_improvements.md

## Branch Information
- **Branch**: `Skyline/work/20251218_screenshot_consistency`
- **Base**: `master`
- **Created**: 2025-12-18
- **Status**: ✅ Complete
- **PR**: [#3727](https://github.com/ProteoWizard/pwiz/pull/3727)
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
- [x] Block ChromatogramManager background thread while ACG is frozen
- [x] Freeze X-axis for progressive data to avoid non-determinism from parallel file import race
- [x] Add Y-axis (graphIntensityMax) locking for consistent intensity scales
- [x] Add IsTestingResultsProgressOnly for faster screenshot test cycles

**Deferred to future work (see ai/todos/backlog/TODO-screenshot_consistency_followup.md):**
- [ ] Address X-axis label orientation inconsistency
- [ ] Enhance ImageComparer with diff amplification features
- [ ] Fix CleanupBorder algorithm for consistent 1px borders
- [ ] Remove unused `timeout` parameter from all PauseFor*ScreenShot functions
- [ ] Disable graph animation when ACG is frozen

## Progress Log

### 2026-01-01: X-Axis Freezing for Progressive Data and SRM Fix

**Problem:** DIA-QE tutorial s-13.png was showing X-axis inconsistency between runs due to non-deterministic race condition when parallel file imports complete.

**Root cause:** When multiple files import in parallel, whichever file reaches 100% first can affect the X-axis scale. The slight variations in which file finishes first caused the X-axis maximum to vary.

**Solution:** Added X-axis freezing mechanism for progressive (DIA) data:
1. Added `_frozenTimeMax` field to `AsyncChromatogramsGraph2` to lock X-axis scale
2. Added `CaptureXAxisMax()` method to capture current X-axis value
3. For progressive data, capture X-axis when any file reaches 50% (threshold/2) - well before the race to 100%
4. Added `_isProgressiveMode` flag to distinguish DIA (progressive) from SRM data
5. SRM data does not capture X-axis early (was causing SRM graphs to freeze at X=0)

**Bug fix:** SRM data (MethodRefine, GroupedStudies) was showing blank graphs because X-axis was being captured too early when the graph hadn't rendered yet, freezing it at 0. Fixed by only capturing X-axis for progressive mode data.

**Files changed:**
- `AsyncChromatogramsGraph2.cs` - Added `_frozenTimeMax`, `CaptureXAxisMax()`, updated `IsGraphFrozen`, `ThawForScreenshot()`, `timer_Tick()`, `Redraw()`
- `AllChromatogramsGraph.cs` - Added `_isProgressiveMode` flag, call `CaptureXAxisMax()` only for progressive data

### 2025-12-31: ChromatogramManager Freeze and Screenshot Updates

**Major fix: Background thread blocking for ACG screenshots**

When ACG is frozen for screenshot capture, the ChromatogramManager background thread was still completing document updates, which triggered auto-training and showed EditPeakScoringModelDlg on top of the frozen ACG.

**Solution:** Added freeze mechanism to ChromatogramManager using `ManualResetEventSlim`:
- `FreezeProgressForScreenshot()` - blocks background thread before completing document updates
- `ReleaseProgressFreeze()` - allows background thread to continue
- `WaitIfProgressFrozen()` - called before `CompleteProcessing` to block if frozen

**Files changed:**
- `pwiz_tools/Skyline/Model/Results/Chromatogram.cs` - Added freeze mechanism
- `pwiz_tools/Skyline/Controls/Graphs/AllChromatogramsGraph.cs` - Calls freeze/release on ChromatogramManager
- `pwiz_tools/Skyline/Skyline.cs` - Restored IsProgressFrozen() check in UpdateProgressUI

**Other improvements:**
- Removed unused `timeout` parameter from `PauseForAllChromatogramsGraphScreenShot()`
- Added variant-specific frozen progress values to DiaSwathTutorialTest (TTOF, QE, PASEF)
- Updated frozen progress values in DriftTimePredictorTutorialTest and DiaUmpireTutorialTest

**Screenshots updated (26 files):**
- DIA-PASEF s-14, DIA-QE s-13 (en/ja/zh-CHS), DIA-TTOF s-13 (en/ja/zh-CHS)
- DIA-Umpire-TTOF s-17, s-27
- GroupedStudies s-03 (en/ja/zh-CHS), IMSFiltering s-05
- MS1Filtering s-09 (en/ja/zh-CHS), s-44 (zh-CHS)
- MethodRefine s-03 (en/ja/zh-CHS), PRM s-15 (en/ja/zh-CHS)
- SmallMoleculeIMSLibraries s-08 (en)

### 2025-12-30: Frozen Progress Values from Screenshot Analysis

Values extracted by reading actual tutorial screenshots to determine exact frozen progress settings:

| Test | Screenshot | Elapsed | Total % | File Progress | Verified |
|------|------------|---------|---------|---------------|----------|
| **MethodRefinementTutorialTest** | MethodRefine s-03 | `00:00:01` | 19% | {worm_0001: 96%, worm_0002: 98%, worm_0003: 98%} | ✅ |
| **GroupedStudies1TutorialTest** | GroupedStudies s-03 | `00:00:06` | 5% | {D_102_REP1: 72%, D_102_REP2: 71%, D_102_REP3: 72%} | ✅ |
| **Ms1FullScanFilteringTutorial** | MS1Filtering s-09 | `00:00:02` | 90% | {100803_0001_MCF7_TiB_L: 85%, 100803_0005b_MCF7_TiTip3: 95%} | ✅ |
| **TargetedMSMSTutorialTest** | PRM s-15 | `00:00:01` | 10% | {20fmol_uL_tech1: 34%, 20fmol_uL_tech2: 31%} | ✅ |
| **SmallMolLibrariesTutorialTest** | SmallMolIMSLibraries s-08 | `00:00:35` | 33% | {Flies_Ctrl_F_A_018: 44%, Flies_Ctrl_M_A_001: 40%} | ✅ |
| **DriftTimePredictorTutorialTest** | IMSFiltering s-05 | `00:01:10` | 35% | {BSA_Frag_100nM_18: 44%, Yeast_0pt1ug_BSA_1: 28%} | ✅ |
| **DiaUmpireTutorialTest** | DIA-Umpire-TTOF s-17 | `00:00:22` | 40% | {collinsb_I180316_001: 40%, collinsb_I180316_002: 41%} | ✅ |
| **DiaSwathTutorialTest (TTOF)** | DIA-TTOF s-13 | `00:00:22` | 15% | {collinsb_I180316_001: 41%, ...002: 41%, ...003: 41%} | ✅ |
| **DiaSwathTutorialTest (QE)** | DIA-QE s-13 | `00:00:12` | 15% | {collinsb_X1803_171-A: 42%, ...172-B: 43%, ...173-A: 43%} | ✅ |
| **DiaSwathTutorialTest (PASEF)** | DIA-PASEF s-14 | `00:00:11` | 15% | {A210331_bcc_1180: 44%, ...1181: 44%, ...1182: 42%} | ✅ |

**Note:** DiaSwathTutorialTest uses variant-specific values via `InstrumentSpecificValues.FrozenFileProgress` dictionary.

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
