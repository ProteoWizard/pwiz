# Screenshot Testing Follow-up Improvements

## Branch Information
- **Branch**: `Skyline/work/20260108_screenshot_followup`
- **Base**: `master`
- **Created**: 2026-01-08
- **GitHub Issue**: https://github.com/ProteoWizard/pwiz/issues/3778

## Objective

Follow-up items from the screenshot consistency sprint that were not critical for achieving consistent ACG screenshots but would improve the overall screenshot testing infrastructure.

## Tasks

- [ ] **Fix MethodRefinement s-09** - Showing zoomed chromatogram graph instead of zoomed-out
- [ ] **Consider DDA Search output s-13** - "reading MS2 spectra into scan collection: {count}/81704" the count is not predictable
- [ ] **Fix LiveReports audit log screenshots (s-02, s-68, s-69)** - Use existing Audit Log screenshot solution from AuditLog tutorial
- [ ] **X-Axis Label Orientation Inconsistency (MS1Filtering s-21, ja/zh-CHS only)** - Labels flip between horizontal/vertical based on space
- [ ] **Fix tests ending up in accessibility mode** - Focus rectangles and mnemonic underscores appear unexpectedly
- [ ] **ImageComparer Diff Amplification Features** - Add diff-only view and amplification slider for 1-2px diffs
- [ ] **Fix CleanupBorder Algorithm** - May have off-by-one error for Windows 11 curved corners
- [ ] **Remove unused timeout parameter** - Clean up unused parameter from PauseFor*ScreenShot functions

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
