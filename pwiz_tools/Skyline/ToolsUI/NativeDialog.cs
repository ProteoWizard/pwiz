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

using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// One open native Windows dialog (window class "#32770": a message box, or the common Open/Save/folder
    /// dialog), driven entirely by Win32. Reads are window-manager calls and are safe from any thread, including
    /// the UI thread the dialog's modal loop runs on; a gesture that waits on the dialog must run off that thread.
    /// </summary>
    public class NativeDialog : StandaloneWindow
    {
        protected const string DIALOG_CLASS_NAME = @"#32770"; // Win32 dialog window class
        private const int VK_RETURN = 0x0D;

        protected NativeDialog(IntPtr windowHandle, CancellationToken cancellationToken) : base(cancellationToken, windowHandle)
        {
        }

        /// <summary>
        /// Which kind of native dialog this is, reported as <see cref="FormInfo.SubType"/>. Not part of the id: a
        /// file dialog and the message box it raises share a caption ("Path does not exist" comes up titled
        /// "Save As"), and the kind is read from children that exist only a moment after the window does, so an
        /// id that included it would not be stable from the moment the window is reported.
        /// </summary>
        public virtual string DialogTypeName => @"MessageBox";

        public const string TYPE_NAME = @"Dialog";

        public override string Title => User32.GetWindowTextNoBlock(Hwnd);
        public override string Name => string.Empty;
        public override Type ElementType => GetType();
        public override bool IsEnabled => User32.IsWindowEnabled(Hwnd);
        public override bool IsModal => true;
        /// <summary>A native dialog never closes itself: Windows has no native equivalent of a LongWaitDlg.</summary>
        public override bool IsTransient => false;
        public override bool IsProgressing => false;
        /// <summary>The dialog's message body (its Static-control text) else its caption.</summary>
        public override string DetailedMessage => NativeBodyText(Hwnd) ?? User32.GetWindowTextNoBlock(Hwnd);

        /// <summary>
        /// Whether the shell has finished bringing this dialog up, so it can be driven. Until then the dialog is
        /// reported by nothing: its controls exist before they are shown, and a caller handed a dialog in that
        /// state fails on its first gesture. An override must key on something that, once true, stays true.
        ///
        /// <para>The default is for the generic message box, driven through its buttons: it is ready once one is
        /// shown. A "#32770" with no button shown yet cannot be driven by this class -- any dialog's window exists
        /// for a moment with no controls at all, and a common file dialog's template is created a control at a
        /// time, its buttons after the file list that identifies it -- so a dialog the shell has only just
        /// created is never reported as a message box.</para>
        /// </summary>
        protected virtual bool IsOpenComplete => FindDescendants(NativeControl.BUTTON_CLASS).Any(User32.IsWindowVisible);

        // The longest Static with text (the icon's Static has none), read without blocking: the thread that owns
        // the dialog may be busy and never pump a send.
        private static string NativeBodyText(IntPtr hwnd)
        {
            return User32.EnumChildWindows(hwnd)
                .Where(child => User32.GetClassName(child) == @"Static")
                .Select(User32.GetWindowTextNoBlock)
                .Where(text => !string.IsNullOrEmpty(text))
                .OrderByDescending(text => text.Length)
                .FirstOrDefault();
        }

        /// <summary>
        /// The wrapper that drives the "#32770" at <paramref name="handle"/>, or null when the window is not a
        /// dialog or is still opening (see <see cref="IsOpenComplete"/>). Every test is a window-manager read, so
        /// this is safe from any thread.
        /// </summary>
        public static NativeDialog Create(IntPtr handle, CancellationToken cancellationToken)
        {
            var dialog = Classify(handle, cancellationToken);
            return dialog?.IsOpenComplete == true ? dialog : null;
        }

        // Which dialog the window is, by what its controls say; null when it is not a "#32770", or is a common
        // file dialog whose kind cannot be told yet. Whether it is ready to be driven is Create's question.
        private static NativeDialog Classify(IntPtr handle, CancellationToken cancellationToken)
        {
            if (handle == IntPtr.Zero || User32.GetClassName(handle) != DIALOG_CLASS_NAME)
                return null;
            // A common file dialog is known first, by a marker it carries for its whole life, because the controls
            // that tell Open from Save are created well after the window (the Save dialog's after it has destroyed
            // the classic field it started out with). In that gap a file dialog has no file-name field and no
            // folder tree, and would otherwise pass for a message box.
            if (NativeFileDialog.IsFileDialog(handle))
                return NativeFileDialog.Classify(handle, cancellationToken);
            // Checked after the file dialogs, whose navigation pane also has a tree.
            if (NativeFolderBrowserDialog.IsFolderBrowserDialog(handle))
                return new NativeFolderBrowserDialog(handle, cancellationToken);
            return new NativeDialog(handle, cancellationToken);
        }

        /// <summary>The native dialogs currently open in this process and ready to be driven.</summary>
        public static IList<NativeDialog> GetOpenDialogs(CancellationToken cancellationToken)
        {
            var result = new List<NativeDialog>();
            var seen = new HashSet<IntPtr>();
            foreach (var hwnd in FindDialogHandles())
            {
                // A dialog can vanish mid-enumeration (it is closing): Create then returns null rather than throwing.
                var dialog = Create(hwnd, cancellationToken);
                if (dialog != null && seen.Add(hwnd))
                    result.Add(dialog);
            }
            return result;
        }

        /// <summary>The dialog's descendant windows of the given class, in window order. A modern file dialog's
        /// DirectUI content is drawn rather than windowed, so it never appears here.</summary>
        protected IEnumerable<IntPtr> FindDescendants(string className)
        {
            return User32.EnumChildWindows(Hwnd).Where(hwnd => User32.GetClassName(hwnd) == className);
        }

        /// <summary>The dialog's descendant of the given class carrying the given control id, or IntPtr.Zero. The
        /// class matters: the Save dialog's file-name Edit and its address breadcrumb both carry control id 1001.</summary>
        protected IntPtr FindDescendant(string className, int controlId)
        {
            return FindDescendants(className)
                .FirstOrDefault(hwnd => User32.GetDlgCtrlID(hwnd) == controlId);
        }

        /// <summary>Whether the dialog has any descendant carrying the given control id, whatever its class. A
        /// control id can ride a stack of windows (a ComboBoxEx32, the ComboBox inside it, the Edit inside that),
        /// and which of them carries it varies by dialog flavour.</summary>
        protected bool HasDescendantWithControlId(int controlId)
        {
            return User32.EnumChildWindows(Hwnd).Any(hwnd => User32.GetDlgCtrlID(hwnd) == controlId);
        }

        protected bool HasDescendant(string className, int controlId)
        {
            return FindDescendant(className, controlId) != IntPtr.Zero;
        }

        protected static bool HasDescendant(IntPtr dialogHwnd, string className, int controlId)
        {
            return User32.EnumChildWindows(dialogHwnd).Any(hwnd =>
                User32.GetClassName(hwnd) == className && User32.GetDlgCtrlID(hwnd) == controlId);
        }

        /// <summary>The dialog's descendant of the given class and control id, or a clear failure. Found by id
        /// rather than among the visible children: a dialog's controls all report themselves invisible until the
        /// shell shows it, while an id is stable from the moment a control is created.</summary>
        protected IntPtr RequireDescendant(string className, int controlId, string description)
        {
            var hwnd = FindDescendant(className, controlId);
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"The native dialog '{0}' has no {1}.", FormId, description));
            return hwnd;
        }

        /// <summary>The dialog's button with the given control id (IDOK, IDCANCEL), found without matching a
        /// localized caption.</summary>
        protected NativeButton RequireButton(int controlId, string description)
        {
            return new NativeButton(RequireDescendant(NativeControl.BUTTON_CLASS, controlId, description + @" button"),
                CancellationToken);
        }

        // The dialog's visible Button / Edit / Static descendants: visibility separates its real controls from the
        // hidden scratch windows a file dialog carries (a collapsed address-bar Edit, a hidden Help button).
        public override IEnumerable<UiElement> EnumerateChildren()
        {
            foreach (var hwnd in User32.EnumChildWindows(Hwnd))
            {
                if (!User32.IsWindowVisible(hwnd))
                    continue;
                var element = ElementFor(hwnd);
                if (element != null)
                    yield return element;
            }
        }

        // Null for a child window that is not something a caller can act on or read (DirectUI hosts, the shell
        // view, scroll bars).
        private NativeControl ElementFor(IntPtr hwnd)
        {
            switch (User32.GetClassName(hwnd))
            {
                case NativeControl.BUTTON_CLASS:
                    return new NativeButton(hwnd, CancellationToken);
                case NativeControl.EDIT_CLASS:
                    return new NativeTextBox(hwnd, CancellationToken);
                case NativeControl.STATIC_CLASS:
                    // A Static with no text is an icon or a spacer, not a label.
                    return string.IsNullOrEmpty(User32.GetWindowTextNoBlock(hwnd))
                        ? null
                        : (NativeControl) new NativeLabel(hwnd, CancellationToken);
                default:
                    return null;
            }
        }

        /// <summary>Accepts the dialog by posting Enter, which its modal loop turns into the default button (see
        /// <see cref="PostEnter"/>). OkDialog runs the gesture on the dialog's UI thread and waits until it closes.</summary>
        public override ActionResult DismissWithAcceptButton()
        {
            return OkDialog(() => PostEnter(Hwnd));
        }

        /// <summary>Cancels the dialog by sending WM_CLOSE on its own UI thread, the way the title-bar close button
        /// would. A message box with no cancel/close affordance (a Yes/No box) ignores WM_CLOSE; dismiss such a box
        /// with <see cref="StandaloneWindow.DismissWithButton"/>. Must be called off the UI thread.</summary>
        public override ActionResult DismissWithCancelButton()
        {
            return OkDialog(() => User32.SendMessage(Hwnd, User32.WinMessageType.WM_CLOSE, IntPtr.Zero, IntPtr.Zero));
        }

        /// <summary>"Dialog:Save As": the constant type, never the kind (see <see cref="DialogTypeName"/>).</summary>
        public override string FormId => TYPE_NAME + @":" + Title;

        // The value is typed synchronously with no follow-on work, so the set is complete on return.
        public override ActionResult SetValue(string controlId, string value)
        {
            VerifyNotBlocked();
            SetValueCore(value);
            return new ActionResult { Completed = true };
        }

        protected virtual void SetValueCore(string value)
        {
            throw new ArgumentException(LlmInstruction.Format(
                @"Setting values is not supported for native dialog {0}.", FormId));
        }

        public override System.Drawing.Bitmap CaptureImage() => JsonUiService.CaptureNativeWindow(Hwnd);

        private void VerifyNotBlocked()
        {
            if (!IsEnabled)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Cannot interact with native dialog '{0}' because it is blocked.", FormId));
        }

        // Enter must be posted, not sent: the dialog's modal message loop translates a queued VK_RETURN into its
        // default action (IsDialogMessage), which a synchronous send to the control's wndproc bypasses.
        protected static void PostEnter(IntPtr handle)
        {
            User32.PostMessageA(handle, User32.WinMessageType.WM_KEYDOWN, VK_RETURN, 0);
            User32.PostMessageA(handle, User32.WinMessageType.WM_KEYUP, VK_RETURN, 0);
        }

        // BM_CLICK acts on the button directly, so unlike Enter it works when sent. Run it on the button's own UI
        // thread, so a click that opens a nested modal blocks there rather than pinning the calling thread.
        protected static void SendClick(IntPtr buttonHandle)
        {
            User32.SendMessage(buttonHandle, User32.WinMessageType.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
        }

        /// <summary>
        /// The process's shown "#32770" windows, by EnumWindows, which includes owned top-level windows (a dialog
        /// owned by a nested modal form). Only a shown dialog counts: a window exists for a while before the shell
        /// shows it, and nothing can be driven until then.
        /// </summary>
        private static IEnumerable<IntPtr> FindDialogHandles()
        {
            var processId = Kernel32.GetCurrentProcessId();
            return User32.EnumWindows().Where(hwnd =>
            {
                // Cheapest tests first: GetClassName allocates, and most windows belong to other processes.
                User32.GetWindowThreadProcessId(hwnd, out var windowProcessId);
                if (windowProcessId != processId || !User32.IsWindowVisible(hwnd))
                    return false;
                return User32.GetClassName(hwnd) == DIALOG_CLASS_NAME;
            });
        }

        public override FormInfo GetFormInfo()
        {
            return new FormInfo
            {
                Type = TYPE_NAME,
                SubType = DialogTypeName,
                Title = Title,
                HasGraph = false,
                DockState = @"Dialog",
                Id = FormId,
                DetailedMessage = JsonUiService.TruncateDetail(DetailedMessage),
                IsNative = true,
            };
        }
    }
}
