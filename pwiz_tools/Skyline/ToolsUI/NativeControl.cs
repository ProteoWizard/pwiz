/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// A control on a native dialog (<see cref="NativeDialog"/>) -- a real Win32 child window, wrapped as a
    /// <see cref="UiElement"/> so a native dialog's contents are walked, matched and acted on by exactly the same
    /// machinery as a managed form's controls. Everything it needs is a window-manager read (class, control id,
    /// text, enabled), so it is safe to call from ANY thread -- including the UI thread the dialog's modal loop
    /// is running on. That is why a native dialog needs no threading contract of its own.
    ///
    /// <para>Each kind reports the ElementType of the managed control it is the native counterpart of (a button is
    /// a <see cref="Button"/>, a text field a <see cref="TextBox"/>), so a caller reading get_controls cannot tell
    /// -- and need not care -- whether a dialog is native.</para>
    /// </summary>
    public abstract class NativeControl : UiElement
    {
        // The window classes of the controls a native dialog exposes as real windows. Everything else in a modern
        // file dialog (the navigation pane, the file list, the breadcrumb) is DirectUI: drawn, not windowed, so it
        // has no handle and cannot appear here.
        public const string BUTTON_CLASS = @"Button";
        public const string EDIT_CLASS = @"Edit";
        public const string STATIC_CLASS = @"Static";
        public const string TREE_CLASS = @"SysTreeView32";

        protected NativeControl(IntPtr hwnd, CancellationToken cancellationToken) : base(cancellationToken)
        {
            Hwnd = hwnd;
        }

        /// <summary>The control's window handle -- what every gesture is aimed at.</summary>
        public IntPtr Hwnd { get; }

        /// <summary>The control's dialog control id. For a control in a dialog this is the same number UI
        /// Automation used to report as its AutomationId, so the ids the file-dialog classes match on are
        /// unchanged -- only the way they are read is.</summary>
        public int ControlId => User32.GetDlgCtrlID(Hwnd);

        public override string Name => string.Empty;
        public override string Label => User32.GetWindowText(Hwnd);
        public override bool IsEnabled => User32.IsWindowEnabled(Hwnd);

        // A GESTURE, though, does go through the dialog's UI thread (DialogWatcher marshals it there), so it is
        // counted like any other action and a modal it raises is detected by the wait -- which is how a native
        // button's click can now report the dialog it opened, the way a managed button's does.
        internal override IntPtr FormHwnd => Hwnd;
    }

    /// <summary>A push button on a native dialog -- a message box's "Yes"/"No"/"OK", a file dialog's Open/Save or
    /// Cancel. Reports itself as a <see cref="Button"/>, and is matched by its caption exactly like a managed one
    /// (the match ignores the '&amp;' mnemonic).</summary>
    public sealed class NativeButton : NativeControl, IClickableElement
    {
        public NativeButton(IntPtr hwnd, CancellationToken cancellationToken) : base(hwnd, cancellationToken)
        {
        }

        public override Type ElementType => typeof(Button);

        // BM_CLICK is SENT, and the caller (UiAction.Invoke) has already put us on the dialog's own UI thread: the
        // click acts on the button directly (no message-loop translation needed, unlike an Enter keypress), and a
        // click that raises a nested modal -- the Save dialog's "replace it?" prompt -- blocks HERE, on that thread,
        // keeping the action counted so the wait sees the new modal and names it, instead of pinning the pipe thread.
        public void ClickNow() =>
            User32.SendMessage(Hwnd, User32.WinMessageType.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>A text field on a native dialog -- in practice a file dialog's file-name box. Reports itself as a
    /// <see cref="TextBox"/>. By default its Label is null (the Static beside it is what names it); the file dialog
    /// passes it the label "File name" so a caller can read/set the box by that name, since the adjacent
    /// "File name:" static would otherwise shadow the caption-less field.</summary>
    public sealed class NativeTextBox : NativeControl, IValueElement
    {
        private readonly string _label;

        public NativeTextBox(IntPtr hwnd, CancellationToken cancellationToken, string label = null)
            : base(hwnd, cancellationToken)
        {
            _label = label;
        }

        public override Type ElementType => typeof(TextBox);

        // Its window text is its VALUE, not its label -- otherwise a file dialog's file-name box would answer to
        // whatever path happens to be typed in it. The label, when given, is the caption of the static beside it.
        // Read by SENDING WM_GETTEXT (see User32.SendGetText): GetWindowText off-thread returns only the stored
        // caption, which is empty for a ComboBoxEx's edit that keeps its own text.
        public override string Label => _label;
        public override object GetValueNow() => User32.SendGetText(Hwnd);

        public void SetValueNow(object value) => SetText(value?.ToString() ?? string.Empty);

        /// <summary>Sets the field's text. Thread-agnostic: WM_SETTEXT blocks until the owning thread pumps it,
        /// which the dialog's modal loop does.</summary>
        public void SetText(string text)
        {
            if (!User32.SetWindowText(Hwnd, text))
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Could not type into the native dialog's text field."));
        }
    }

    /// <summary>A static text on a native dialog -- a message box's message, a file dialog's "File name:" caption.
    /// Reports itself as a <see cref="Label"/>: informational, with no action of its own.</summary>
    public sealed class NativeLabel : NativeControl
    {
        public NativeLabel(IntPtr hwnd, CancellationToken cancellationToken) : base(hwnd, cancellationToken)
        {
        }

        public override Type ElementType => typeof(Label);
    }

    /// <summary>The Open/Save file dialog's address breadcrumb, surfaced (via
    /// <see cref="NativeFileDialog.EnumerateChildren"/>) so a caller can read the folder the dialog is currently
    /// showing -- to confirm a navigation before selecting files in it. Read-only: it reports itself as a
    /// <see cref="Label"/> and its <see cref="GetValueNow"/> is the current folder path. It carries the visible
    /// label "Address", so a caller reads the current folder with get_value on the "Address" control.</summary>
    public sealed class NativeAddressBar : NativeControl
    {
        public NativeAddressBar(IntPtr hwnd, CancellationToken cancellationToken) : base(hwnd, cancellationToken)
        {
        }

        public override Type ElementType => typeof(Label);
        public override string Label => @"Address";

        // The current folder: the breadcrumb caption ("Address: C:\dir") with everything up to and including the
        // first ": " removed. A path cannot contain ": " (':' is legal only in a drive's "C:\"), so this drops the
        // localized "Address:" label without touching the path; a caption with no ": " is returned unchanged.
        public override object GetValueNow()
        {
            var text = User32.GetWindowText(Hwnd);
            var separator = text?.IndexOf(@": ", StringComparison.Ordinal) ?? -1;
            return separator < 0 ? text : text.Substring(separator + 2);
        }
    }
}
