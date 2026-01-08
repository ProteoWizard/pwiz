# TODO-screenshot_consistency_followup.md

## Overview
- **Category**: Enhancement
- **Priority**: Low
- **Origin**: Deferred from TODO-20251218_screenshot_consistency_improvements.md
- **Created**: 2026-01-01

## Description

Follow-up items from the screenshot consistency sprint that were not critical for achieving consistent ACG screenshots but would improve the overall screenshot testing infrastructure.

## Tasks

### 1. Fix MethodRefinement s-09
- Showing zoomed chromatogram graph instead of zoomed-out

### 2. Consider DDA Search output s-13
- "reading MS2 spectra into scan collection: {count}/81704" the "count" is not predictable

### 3. Fix LiveReports audit log screenshots
**Affected screenshots**: s-02, s-68, s-69

Use existing Audit Log screenshot solution found in the AuditLog tutorial.

### 4. X-Axis Label Orientation Inconsistency
**Affected screenshots**: MS1Filtering s-21 (ja, zh-CHS only)

X-axis labels on graphs can flip between horizontal and vertical orientation depending on available space. This causes screenshot diffs even when no functional change occurred.

**Proposed solutions**:
- Make graphs slightly wider to ensure labels consistently remain horizontal
- Implement deterministic label orientation during screenshot capture mode
- Force a specific orientation based on locale/graph type

### 5. Fix tests ending up in accessibility mode

The system can end up in accessibility mode where it shows focus rectangles (on buttons, lists, and dropdown lists - combo boxes) and even mnemonics on control labels. This creates false-positive screenshot changes. Research whether there is a way to force the system out of this mode before taking a screenshot. Even when a test starts out not in this mode and can take screenshots for an hour without these effects, they sometimes appear later even for a computer left alone overnight to take screenshots.

**Root Cause Analysis**:

Windows maintains a UI state system that tracks whether the user is in "keyboard mode" or "mouse mode." When any key is pressed (including F5 to start a test run), Windows flips into keyboard mode and begins showing:
- Focus rectangles (dotted borders on focused controls)
- Mnemonic underscores (the underlined letters in labels like "&File")

This explains why tests can run for an hour without these artifacts, then suddenly show them—any stray keypress on the machine triggers the switch.

**Potential Solutions**:

1. **Process launch method**: If tests are started via mouse click rather than keyboard (F5), the child process inherits "mouse mode" UI state. This is fragile but explains observed inconsistency.

2. **Send WM_CHANGEUISTATE before each capture**:
   ```csharp
   const int WM_CHANGEUISTATE = 0x0127;
   const int UIS_SET = 1;
   const int UISF_HIDEFOCUS = 0x1;
   const int UISF_HIDEACCEL = 0x2;
   
   // Send to top-level form; propagates to children
   SendMessage(form.Handle, WM_CHANGEUISTATE,
       (IntPtr)((UIS_SET << 16) | UISF_HIDEFOCUS | UISF_HIDEACCEL), IntPtr.Zero);
   ```
   This should be sent immediately before capture, not just at startup, since subsequent key events can re-enable the cues.

3. **Override ShowFocusCues**: Custom control subclasses could override `ShowFocusCues => false`, but this is invasive for an existing codebase.

**Status**: Needs testing to confirm `WM_CHANGEUISTATE` reliably suppresses cues when sent immediately before capture.

### 6. ImageComparer Diff Amplification Features
**Affected screenshots**: DDASearch s-16 (1px diff), ExistingQuant s-07 (1px diff), IMSFiltering s-14/s-17

When ImageComparer shows only 1-2 pixel differences, it's nearly impossible to visually locate the changed pixels.

**Proposed enhancements**:
1. Add a "diff-only" view mode that removes the image and paints only the diff pixels on a white background
2. Add an "Amplification" slider that expands the visual area around each changed pixel (radius 1-10)
3. This would help identify exact pixel locations for debugging

### 7. Fix CleanupBorder Algorithm
The `ScreenshotProcessingExtensions.CleanupBorder` algorithm that draws borders around screenshots may have an off-by-one error for certain window sizes. In Windows 11, curved corners are translucent and must be reproduced consistently.

### 8. Remove Unused `timeout` Parameter
Remove the unused `timeout` parameter from all `PauseFor*ScreenShot` functions for API cleanliness.

## Notes

These items are not blocking screenshot consistency for the main ACG use cases. The primary sprint (TODO-20251218) successfully achieved:
- Frozen progress display (file bars, total bar, elapsed time)
- Y-axis locking with graphIntensityMax parameter
- X-axis freezing for progressive (DIA) data
- Proper handling of SRM vs progressive data modes
- Background thread blocking during screenshot capture
