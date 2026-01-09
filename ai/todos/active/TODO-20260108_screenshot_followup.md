# Screenshot Testing Follow-up Improvements

## Branch Information
- **Branch**: `Skyline/work/20260108_screenshot_followup`
- **Base**: `master`
- **Created**: 2026-01-08
- **GitHub Issue**: [#3778](https://github.com/ProteoWizard/pwiz/issues/3778)
- **PR**: [#3779](https://github.com/ProteoWizard/pwiz/pull/3779)

## Objective

Follow-up items from the screenshot consistency sprint that were not critical for achieving consistent ACG screenshots but would improve the overall screenshot testing infrastructure.

## Tasks

- [N/R] **Fix MethodRefinement s-09** - Does not reproduce; ran all 3 languages with no diffs
- [ ] **Consider DDA Search output s-13** - "reading MS2 spectra into scan collection: {count}/81704" the count is not predictable
- [x] **Fix LiveReports audit log screenshots (s-02, s-68, s-69)** - Added TestTimeProvider wrapper
- [ ] **X-Axis Label Orientation Inconsistency (MS1Filtering s-21, ja/zh-CHS only)** - Labels flip between horizontal/vertical based on space
- [x] **Fix tests ending up in accessibility mode** - Send WM_CHANGEUISTATE before each capture
- [ ] **ImageComparer Diff Amplification Features** - Add diff-only view and amplification slider for 1-2px diffs
- [ ] **Fix CleanupBorder Algorithm** - May have off-by-one error for Windows 11 curved corners
- [ ] **Remove unused timeout parameter** - Clean up unused parameter from PauseFor*ScreenShot functions
- [x] **Paint over ACG per-file progress bars** - Extended FillProgressBar to handle all visible file progress bars

## Technical Notes

### Accessibility Mode Fix
Windows UI state tracks keyboard vs mouse mode. Send `WM_CHANGEUISTATE` before capture:
```csharp
const int WM_CHANGEUISTATE = 0x0127;
const int UIS_SET = 1;
const int UISF_HIDEFOCUS = 0x1;
const int UISF_HIDEACCEL = 0x2;

SendMessage(form.Handle, WM_CHANGEUISTATE,
    (IntPtr)((UIS_SET << 16) | UISF_HIDEFOCUS | UISF_HIDEACCEL), IntPtr.Zero);
```

### ImageComparer Enhancements
1. "Diff-only" view - paint only diff pixels on white background
2. "Amplification" slider - expand visual area around changed pixels (radius 1-10)

## Progress Log

### 2026-01-08 - Session Start

Starting work on this issue. Created branch and TODO file.

### 2026-01-08 - LiveReports audit log fix

Added `TestTimeProvider` wrapper to `LiveReportsTutorialTest.TestLiveReportsTutorial()` to ensure
consistent audit log timestamps in screenshots s-02, s-68, s-69. This follows the pattern established
in `AuditLogTutorialTest`.

**File**: `pwiz_tools/Skyline/TestTutorial/LiveReportsTutorialTest.cs`

### 2026-01-08 - ACG per-file progress bar painting

Added ability to paint over per-file progress bars in AllChromatogramsGraph screenshots to prevent
Windows progress bar animation inconsistencies (only consistent over RemoteDesktop otherwise).

**Changes**:
1. `FileProgressControl.cs` - Added `ProgressBar` property to expose the progress bar control
2. `AllChromatogramsGraph.cs` - Added `GetVisibleFileProgressBars()` method to enumerate visible bars
3. `ScreenshotProcessingExtensions.cs` - Added `FillProgressBars(IEnumerable<ProgressBar>)` overload
4. `TestFunctional.cs` - Updated `PauseForAllChromatogramsGraph` to fill all progress bars

### 2026-01-08 - Accessibility mode fix (focus rectangles and mnemonics)

Fixed long-standing issue where focus rectangles and mnemonic underscores would appear in screenshots
even when tests were started with a mouse click. Windows tracks keyboard vs mouse mode, and any
keypress (including F5 to start the test) flips into keyboard mode showing these UI cues.

**Solution**: Send `WM_CHANGEUISTATE` message to the top-level form immediately before each screenshot
capture to hide focus rectangles (`UISF_HIDEFOCUS`) and mnemonic underscores (`UISF_HIDEACCEL`).

**Changes**:
1. `User32.cs` - Added `WM_CHANGEUISTATE` to `WinMessageType` enum and `UISF_HIDEALL` constant
2. `ScreenshotManager.cs` - Added `HideKeyboardCues()` method called from `HideSensitiveFocusDisplay()`

**Tested**: Started test with F5, saw focus rectangles in UI during test, but none in screenshots.
