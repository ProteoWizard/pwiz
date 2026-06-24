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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Base class for driving a single native Windows dialog (window class "#32770", such as the
    /// common Open/Save file dialog) using UI Automation. An instance wraps one open dialog,
    /// identified by its window handle. A native dialog is not a WinForms form, so it does not
    /// appear in FormUtil.OpenForms and cannot be introspected or driven the way the rest of
    /// Skyline's UI is; UI Automation reaches it the same way a user would.
    ///
    /// This is shared product code with two consumers: the functional test framework
    /// (AbstractFunctionalTest.OpenDocument) and the Skyline MCP server's UI interaction layer.
    ///
    /// A native dialog is modal and runs its own message loop on the Skyline UI thread, so the
    /// action that displays it must be posted to the UI thread (e.g. with BeginInvoke) and these
    /// methods must be called from a different thread (the test thread or the MCP pipe thread),
    /// never the UI thread itself.
    ///
    /// Use <see cref="WaitForDialog{T}"/> or <see cref="GetOpenDialogs"/> to obtain an instance;
    /// <see cref="Create"/> chooses the subclass that matches a given dialog element.
    ///
    /// A dialog is a <see cref="UiElement"/>: it presents to the connector exactly like any other form (it
    /// is listed by GetOpenForms and addressed by a path whose Text is its id), and its <see cref="Children"/>
    /// are connector elements (buttons, a file-name field) that dispatch their actions to this automation
    /// (Accept/Cancel/EnterPath) instead of to a WinForms control.
    /// </summary>
    public abstract class NativeDialog : UiElement, IFormElement
    {
        protected const string DIALOG_CLASS_NAME = @"#32770"; // Win32 dialog window class
        private const int DEFAULT_TIMEOUT_MILLIS = 30 * 1000;
        private const int POLL_INTERVAL_MILLIS = 100;
        private const int VK_RETURN = 0x0D;

        private AutomationElement _dialogElement;

        protected NativeDialog(IntPtr windowHandle)
        {
            WindowHandle = windowHandle;
        }

        /// <summary>Handle of the dialog window this instance drives.</summary>
        public IntPtr WindowHandle { get; }

        /// <summary>How long the wait helpers poll for a control before throwing.</summary>
        public int MillisTimeout { get; set; } = DEFAULT_TIMEOUT_MILLIS;

        /// <summary>
        /// Short identifier for the kind of dialog (e.g. "FileDialog"), for callers that classify
        /// dialogs without knowing the concrete subclass.
        /// </summary>
        public abstract string DialogTypeName { get; }

        /// <summary>The dialog window's caption.</summary>
        public string Title => DialogElement.Current.Name;

        // UiElement: a native dialog is the root of its own path, so most of these are not used for matching
        // (the dialog is found by its form id, not walked into as a child). They are implemented so the
        // dialog is a first-class element whose children can be listed and acted on like any form's.
        public override string Name => string.Empty;
        public override Type ElementType => GetType();
        public override bool IsEnabled => User32.IsWindowEnabled(WindowHandle);
        public override bool IsVisible => true;

        protected AutomationElement DialogElement =>
            _dialogElement ?? (_dialogElement = AutomationElement.FromHandle(WindowHandle));

        /// <summary>
        /// Returns the automation wrapper for the given native dialog element, choosing the
        /// subclass that matches the dialog, or null if no subclass handles it.
        /// </summary>
        public static NativeDialog Create(AutomationElement dialog)
        {
            IntPtr handle;
            try
            {
                if (dialog.Current.ClassName != DIALOG_CLASS_NAME)
                    return null;
                handle = new IntPtr(dialog.Current.NativeWindowHandle);
            }
            catch (ElementNotAvailableException)
            {
                return null;
            }
            if (handle == IntPtr.Zero)
                return null;
            if (OpenFileDialogAutomation.IsOpenFileDialog(dialog))
                return new OpenFileDialogAutomation(handle);
            // Check Save after Open: the modern Open dialog has the classic file-name combo
            // (AutomationId 1148) that IsOpenFileDialog keys on; the Save dialog does not, so the
            // two never both match.
            if (SaveFileDialogAutomation.IsSaveFileDialog(dialog))
                return new SaveFileDialogAutomation(handle);
            return null;
        }

        /// <summary>
        /// Returns automation wrappers for the native dialogs currently open in this process,
        /// found via UI Automation. Must be called from a thread other than the UI thread.
        /// </summary>
        public static IList<NativeDialog> GetOpenDialogs()
        {
            var result = new List<NativeDialog>();
            var seen = new HashSet<IntPtr>();
            foreach (var element in FindDialogElements())
            {
                var automation = Create(element);
                if (automation != null && automation.WindowHandle != IntPtr.Zero && seen.Add(automation.WindowHandle))
                    result.Add(automation);
            }
            return result;
        }

        /// <summary>
        /// Waits for a native dialog of the given type to appear in this process and returns its
        /// automation wrapper.
        /// </summary>
        public static T WaitForDialog<T>(int millisTimeout = DEFAULT_TIMEOUT_MILLIS) where T : NativeDialog
        {
            return PollUntil(millisTimeout, typeof(T).Name,
                () => GetOpenDialogs().OfType<T>().FirstOrDefault());
        }

        /// <summary>
        /// Dismisses the dialog by posting WM_CLOSE, which cancels it the way the title-bar close
        /// button or the Cancel button would. This is more reliable than invoking the Cancel
        /// button through UI Automation, which the dialog (a DirectUI surface) does not always
        /// honor.
        /// </summary>
        public void Cancel()
        {
            User32.PostMessageA(WindowHandle, User32.WinMessageType.WM_CLOSE, 0, 0);
        }

        /// <summary>
        /// Accepts the dialog -- the equivalent of clicking its default/OK button. Implemented per
        /// dialog type because the accept gesture differs by surface.
        /// </summary>
        public abstract void Accept();

        // ---- IFormElement: drive the dialog the way JsonUiService drives any form -------------------
        // Everything runs on the calling (pipe) thread: the dialog runs its own modal loop on the UI thread,
        // so marshalling to it (or into RunWithDialogWatch's background work) would deadlock. The dialog's
        // gestures post Win32 messages / drive UI Automation, which are safe from any thread.

        /// <summary>The id this dialog is addressed by (see skyline_get_open_forms): "FileDialog:Open …".</summary>
        public string FormId => DialogTypeName + @":" + Title;

        public ControlInfo[] GetControls() => GetChildren();

        // A native dialog has no caption-addressable buttons (matching "OK"/"Cancel" would not survive a
        // localized build). Confirm it with the accept action and dismiss it with Close (close_form) instead.
        public void ClickButton(string button)
        {
            throw new ArgumentException(LlmInstruction.Format(
                @"The native dialog '{0}' has no named buttons. Use the accept action to confirm it, or close_form to cancel it.",
                FormId));
        }

        // A native dialog takes a value only as its file name (a Save dialog, an Open dialog); the base
        // refuses, and the file dialogs override SetValueCore to type the path.
        public void SetValue(string controlId, string value)
        {
            VerifyNotBlocked();
            SetValueCore(value);
        }

        protected virtual void SetValueCore(string value)
        {
            throw new ArgumentException(LlmInstruction.Format(
                @"Setting values is not supported for native dialog {0}.", FormId));
        }

        public void Close() => Cancel();

        public object PerformAction(UiElementPath path, UiAction action, object value)
        {
            var element = JsonUiService.RequireAction(JsonUiService.ResolvePathFrom(path, this), action);
            return action.Invoke(element, value);
        }

        public System.Drawing.Bitmap CaptureImage() => JsonUiService.CaptureNativeWindow(WindowHandle);

        private void VerifyNotBlocked()
        {
            if (!IsEnabled)
                throw new InvalidOperationException(LlmInstruction.Format(
                    @"Cannot interact with native dialog '{0}' because it is blocked.", FormId));
        }

        protected void BringToForeground()
        {
            User32.SetForegroundWindow(WindowHandle);
        }

        /// <summary>
        /// Accepts the dialog by posting Enter to the given control. Posting the key is more
        /// reliable than invoking the default button through UI Automation (the dialog is a
        /// DirectUI surface).
        /// </summary>
        protected void PressEnter(AutomationElement element)
        {
            var handle = new IntPtr(element.Current.NativeWindowHandle);
            User32.PostMessageA(handle, User32.WinMessageType.WM_KEYDOWN, VK_RETURN, 0);
            User32.PostMessageA(handle, User32.WinMessageType.WM_KEYUP, VK_RETURN, 0);
        }

        /// <summary>Waits for a descendant control of the dialog with the given AutomationId.</summary>
        protected AutomationElement WaitForElement(string automationId)
        {
            return WaitForElement(
                new PropertyCondition(AutomationElement.AutomationIdProperty, automationId),
                @"dialog control with AutomationId " + automationId);
        }

        /// <summary>Waits for a descendant control of the dialog matching the given condition.</summary>
        protected AutomationElement WaitForElement(Condition condition, string description)
        {
            return PollUntil(MillisTimeout, description,
                () => DialogElement.FindFirst(TreeScope.Descendants, condition));
        }

        /// <summary>
        /// Finds the open native dialogs (window class "#32770") of the current process by
        /// enumerating top-level windows with Win32 EnumWindows. A common file dialog is an *owned*
        /// top-level window (not a child window), and EnumWindows enumerates owned windows -- so this
        /// finds a dialog owned by a nested modal form (e.g. the "Add Input Files" dialog owned by
        /// the Import Peptide Search wizard), which a UI Automation child walk from the desktop root
        /// misses because such a dialog is nested below its owner in the automation tree. EnumWindows
        /// visits only top-level windows, never the control subtree, so it stays cheap.
        /// </summary>
        private static IList<AutomationElement> FindDialogElements()
        {
            var processId = (uint)Process.GetCurrentProcess().Id;
            var dialogHandles = new List<IntPtr>();
            User32.EnumWindows((hwnd, lparam) =>
            {
                User32.GetWindowThreadProcessId(hwnd, out var windowProcessId);
                if (windowProcessId == processId && GetWindowClassName(hwnd) == DIALOG_CLASS_NAME)
                    dialogHandles.Add(hwnd);
                return true; // keep enumerating
            }, IntPtr.Zero);

            var result = new List<AutomationElement>();
            foreach (var hwnd in dialogHandles)
            {
                try
                {
                    var element = AutomationElement.FromHandle(hwnd);
                    if (element != null)
                        result.Add(element);
                }
                catch (ElementNotAvailableException)
                {
                    // Window closed between enumeration and binding; skip it.
                }
                catch (ArgumentException)
                {
                    // Handle no longer valid; skip it.
                }
            }
            return result;
        }

        private static string GetWindowClassName(IntPtr hwnd)
        {
            var buffer = new StringBuilder(256);
            int length = User32.GetClassName(hwnd, buffer, buffer.Capacity);
            return length > 0 ? buffer.ToString() : string.Empty;
        }

        private static TResult PollUntil<TResult>(int millisTimeout, string description, Func<TResult> find)
            where TResult : class
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                // UI Automation can briefly throw while a window is being created or torn down.
                TResult value = null;
                try
                {
                    value = find();
                }
                catch (ElementNotAvailableException)
                {
                    // Retry below.
                }
                if (value != null)
                    return value;
                if (stopwatch.ElapsedMilliseconds > millisTimeout)
                    throw new TimeoutException(string.Format(@"Timed out after {0} ms waiting for {1}.", millisTimeout, description));
                Thread.Sleep(POLL_INTERVAL_MILLIS);
            }
        }

    }
}
