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
using System.Threading;
using System.Windows.Automation;
using pwiz.Common.SystemUtil.PInvoke;

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
    /// </summary>
    public abstract class NativeDialogAutomation
    {
        protected const string DIALOG_CLASS_NAME = @"#32770"; // Win32 dialog window class
        private const int DEFAULT_TIMEOUT_MILLIS = 30 * 1000;
        private const int POLL_INTERVAL_MILLIS = 100;
        private const int VK_RETURN = 0x0D;

        private AutomationElement _dialogElement;

        protected NativeDialogAutomation(IntPtr windowHandle)
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

        protected AutomationElement DialogElement =>
            _dialogElement ?? (_dialogElement = AutomationElement.FromHandle(WindowHandle));

        /// <summary>
        /// Returns the automation wrapper for the given native dialog element, choosing the
        /// subclass that matches the dialog, or null if no subclass handles it.
        /// </summary>
        public static NativeDialogAutomation Create(AutomationElement dialog)
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
            return null;
        }

        /// <summary>
        /// Returns automation wrappers for the native dialogs currently open in this process,
        /// found via UI Automation. Must be called from a thread other than the UI thread.
        /// </summary>
        public static IList<NativeDialogAutomation> GetOpenDialogs()
        {
            var result = new List<NativeDialogAutomation>();
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
        public static T WaitForDialog<T>(int millisTimeout = DEFAULT_TIMEOUT_MILLIS) where T : NativeDialogAutomation
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
            var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
            return PollUntil(MillisTimeout, @"dialog control with AutomationId " + automationId,
                () => DialogElement.FindFirst(TreeScope.Descendants, condition));
        }

        /// <summary>
        /// Finds the open native dialogs (window class "#32770") of the current process. A native
        /// dialog is an owned window of the Skyline main window, so in the UI Automation tree it
        /// appears either as a direct child of the desktop root or as a direct child of its owner
        /// window. We look only at this process's top-level windows and their direct children --
        /// never a full subtree walk, which is prohibitively slow over Skyline's large control
        /// tree.
        /// </summary>
        private static IList<AutomationElement> FindDialogElements()
        {
            var processId = Process.GetCurrentProcess().Id;
            var dialogClassCondition = new PropertyCondition(AutomationElement.ClassNameProperty, DIALOG_CLASS_NAME);
            var result = new List<AutomationElement>();
            try
            {
                var processWindows = AutomationElement.RootElement.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ProcessIdProperty, processId));
                foreach (AutomationElement window in processWindows)
                {
                    // The top-level window itself may be the dialog.
                    if (IsDialogClass(window))
                        result.Add(window);
                    // Owned dialogs appear as direct children of their owner window.
                    try
                    {
                        result.AddRange(window.FindAll(TreeScope.Children, dialogClassCondition).Cast<AutomationElement>());
                    }
                    catch (ElementNotAvailableException)
                    {
                        // Window vanished mid-enumeration; skip its children.
                    }
                }
            }
            catch (ElementNotAvailableException)
            {
                // The UI Automation tree changed during enumeration; return what was found.
            }
            return result;
        }

        private static bool IsDialogClass(AutomationElement element)
        {
            try
            {
                return element.Current.ClassName == DIALOG_CLASS_NAME;
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
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
