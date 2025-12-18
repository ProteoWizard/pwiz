# TODO-move_timeout_to_common_alert_dlg.md

## Branch Information
- **Branch**: `Skyline/work/YYYYMMDD_move_timeout_to_common_alert_dlg`
- **Base**: `master`
- **Created**: (pending)
- **Status**: Backlog
- **PR**: (pending)
- **Objective**: Move ShowWithTimeout functionality from AlertDlg to CommonAlertDlg for shared library reuse

## Background

Nick Shulman suggested that the timeout functionality in `AlertDlg.ShowWithTimeout()` should be moved to `CommonAlertDlg` so it can be used by non-Skyline applications (like SharedBatch) that use the shared library.

Currently, `AlertDlg.ShowWithTimeout()` uses Skyline-specific references:
```csharp
if (Program.FunctionalTest && Program.PauseSeconds == 0 && !Debugger.IsAttached)
```

But the exact same values are already available in the shared library:
```csharp
CommonApplicationSettings.FunctionalTest
CommonApplicationSettings.PauseSeconds
```

## Class Hierarchy

```
System.Windows.Forms.Form
│
└── CommonFormEx (pwiz.Common.SystemUtil)
    │   • TestMode → CommonApplicationSettings.FunctionalTest
    │   • PauseMode → CommonApplicationSettings.PauseSeconds
    │   • Offscreen, ShowFormNames
    │   • Tracks undisposed forms for test cleanup
    │
    ├── CommonAlertDlg (pwiz.Common.GUI) ─────────────────── SHARED LIBRARY
    │   │   • Message, DetailMessage, Exception
    │   │   • MessageIcon, AddButton(), VisibleButtons
    │   │   • LABEL_LEFT_PADDING, LABEL_RIGHT_PADDING
    │   │   • CopyMessage(), DetailedMessage
    │   │
    │   └── AlertDlg (pwiz.Skyline.Alerts) ──────────────── SKYLINE-SPECIFIC
    │       │   • ModeUIExtender (peptide→molecule translation)
    │       │   • ShowWithTimeout() ← uses Program.FunctionalTest, Program.PauseSeconds
    │       │   • ShowAndDispose()
    │       │
    │       ├── MessageDlg (2015, Nick)
    │       │       • Static Show() convenience methods
    │       │       • ignoreModeUI option
    │       │       • ShowError(), ShowException(), ShowWithDetails()
    │       │
    │       └── MultiButtonMsgDlg (2010, Alana)
    │               • Custom button text (btnYesText, btnNoText)
    │               • 1-3 buttons with DialogResult mapping
    │               • Btn0Click(), Btn1Click(), BtnYesClick()
    │
    └── FormEx (pwiz.Skyline.Util) ──────────────────────── SKYLINE-SPECIFIC
            • ModeUIExtender (peptide→molecule translation)
            • General Skyline form base class
```

## Tasks

### Move to CommonAlertDlg
- [ ] Move `ShowWithTimeout()` method to `CommonAlertDlg`
- [ ] Move `ShowAndDispose()` method to `CommonAlertDlg`
- [ ] Move `TIMEOUT_SECONDS` constant to `CommonAlertDlg`
- [ ] Change references from `Program.FunctionalTest` to `CommonApplicationSettings.FunctionalTest`
- [ ] Change references from `Program.PauseSeconds` to `CommonApplicationSettings.PauseSeconds`

### Keep in AlertDlg
- [ ] Verify `ModeUIExtender` / ModeUI support stays (Skyline-specific)
- [ ] Verify `ClipboardHelper.SetSystemClipboardText()` override handling

### Testing
- [ ] Run existing AlertDlg tests
- [ ] Verify SharedBatch still works with CommonAlertDlg
- [ ] Run CodeInspection to check for issues

## Benefits

1. **SharedBatch and other tools** using `CommonAlertDlg` get timeout behavior automatically
2. **DRY principle** - no duplication of test-aware logic
3. **Consistency** - `CommonFormEx` already uses `CommonApplicationSettings` for `TestMode` and `PauseMode`

## Files to Modify

- `pwiz_tools/Shared/CommonUtil/GUI/CommonAlertDlg.cs` - Add timeout methods
- `pwiz_tools/Skyline/Alerts/AlertDlg.cs` - Remove moved methods, keep ModeUI support

## Origin

Suggested by Nick Shulman during PR #3715 review (screenshot updates for 26.0.9).
