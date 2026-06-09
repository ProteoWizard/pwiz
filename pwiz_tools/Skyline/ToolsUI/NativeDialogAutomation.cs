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
    /// Base class for driving a native Windows dialog (window class "#32770", such as the common
    /// Open/Save file dialog) using UI Automation. A native dialog is not a WinForms form, so it
    /// does not appear in FormUtil.OpenForms and cannot be introspected or driven the way the
    /// rest of Skyline's UI is; UI Automation reaches it the same way a user would.
    ///
    /// This is shared product code with two consumers: the functional test framework
    /// (AbstractFunctionalTest.OpenDocument) and the Skyline MCP server's UI interaction layer.
    ///
    /// A native dialog is modal and runs its own message loop on the Skyline UI thread, so the
    /// action that displays it must be posted to the UI thread (e.g. with BeginInvoke) and these
    /// methods must be called from a different thread (the test thread or the MCP pipe thread),
    /// never the UI thread itself.
    ///
    /// A subclass identifies the specific dialog it automates by overriding <see cref="IsMatch"/>.
    /// </summary>
    public abstract class NativeDialogAutomation
    {
        protected const string DIALOG_CLASS_NAME = @"#32770"; // Win32 dialog window class
        protected const int DEFAULT_TIMEOUT_MILLIS = 30 * 1000;
        private const int POLL_INTERVAL_MILLIS = 100;
        private const int VK_RETURN = 0x0D;

        private readonly int _millisTimeout;

        protected NativeDialogAutomation(int millisTimeout = DEFAULT_TIMEOUT_MILLIS)
        {
            _millisTimeout = millisTimeout;
        }

        /// <summary>
        /// Returns true if the given native dialog element is the kind of dialog this class
        /// automates. Called only for elements of window class "#32770".
        /// </summary>
        protected abstract bool IsMatch(AutomationElement dialog);

        /// <summary>
        /// Identifies an open native dialog: its window caption and window handle.
        /// </summary>
        public sealed class NativeDialogInfo
        {
            public NativeDialogInfo(string title, IntPtr windowHandle)
            {
                Title = title;
                WindowHandle = windowHandle;
            }

            public string Title { get; }
            public IntPtr WindowHandle { get; }
        }

        /// <summary>
        /// Returns the matching native dialogs currently open in this process, found via UI
        /// Automation. Must be called from a thread other than the UI thread.
        /// </summary>
        public IList<NativeDialogInfo> GetOpenDialogs()
        {
            var result = new List<NativeDialogInfo>();
            var seen = new HashSet<IntPtr>();
            foreach (var dialog in FindDialogElements())
            {
                try
                {
                    var handle = new IntPtr(dialog.Current.NativeWindowHandle);
                    if (handle == IntPtr.Zero || !seen.Add(handle))
                        continue;
                    result.Add(new NativeDialogInfo(dialog.Current.Name, handle));
                }
                catch (ElementNotAvailableException)
                {
                    // Dialog closed mid-enumeration; skip it.
                }
            }
            return result;
        }

        /// <summary>
        /// Waits for a matching native dialog to appear and dismisses it by posting WM_CLOSE,
        /// which cancels it the way the title-bar close button or the Cancel button would. This
        /// is more reliable than invoking the Cancel button through UI Automation, which the
        /// dialog (a DirectUI surface) does not always honor.
        /// </summary>
        public void Cancel()
        {
            var dialog = WaitForDialog();
            User32.PostMessageA(GetWindowHandle(dialog), User32.WinMessageType.WM_CLOSE, 0, 0);
        }

        /// <summary>
        /// Waits for a matching native dialog to appear and returns its automation element so its
        /// controls can be driven.
        /// </summary>
        protected AutomationElement WaitForDialog()
        {
            return WaitFor(@"native dialog", () => FindDialogElements().FirstOrDefault());
        }

        protected void BringToForeground(AutomationElement dialog)
        {
            User32.SetForegroundWindow(GetWindowHandle(dialog));
        }

        /// <summary>
        /// Accepts the dialog by posting Enter to the given control. Posting the key is more
        /// reliable than invoking the default button through UI Automation (the dialog is a
        /// DirectUI surface).
        /// </summary>
        protected void PressEnter(AutomationElement element)
        {
            var handle = GetWindowHandle(element);
            User32.PostMessageA(handle, User32.WinMessageType.WM_KEYDOWN, VK_RETURN, 0);
            User32.PostMessageA(handle, User32.WinMessageType.WM_KEYUP, VK_RETURN, 0);
        }

        protected AutomationElement WaitForElement(AutomationElement root, string automationId)
        {
            var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
            return WaitFor(@"dialog control with AutomationId " + automationId,
                () => root.FindFirst(TreeScope.Descendants, condition));
        }

        protected IntPtr GetWindowHandle(AutomationElement element)
        {
            return new IntPtr(element.Current.NativeWindowHandle);
        }

        /// <summary>
        /// Finds the matching native dialogs of the current process via UI Automation. A native
        /// dialog is an owned window of the Skyline main window, so in the UI Automation tree it
        /// appears either as a direct child of the desktop root or as a direct child of its owner
        /// window. We look only at this process's top-level windows and their direct children --
        /// never a full subtree walk, which is prohibitively slow over Skyline's large control
        /// tree.
        /// </summary>
        private IList<AutomationElement> FindDialogElements()
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
                    AddIfMatch(window, result);
                    // Owned dialogs appear as direct children of their owner window.
                    AutomationElementCollection childDialogs;
                    try
                    {
                        childDialogs = window.FindAll(TreeScope.Children, dialogClassCondition);
                    }
                    catch (ElementNotAvailableException)
                    {
                        continue;
                    }
                    foreach (AutomationElement childDialog in childDialogs)
                        AddIfMatch(childDialog, result);
                }
            }
            catch (ElementNotAvailableException)
            {
                // The UI Automation tree changed during enumeration; return what was found.
            }
            return result;
        }

        private void AddIfMatch(AutomationElement element, IList<AutomationElement> result)
        {
            try
            {
                if (element.Current.ClassName == DIALOG_CLASS_NAME && IsMatch(element))
                    result.Add(element);
            }
            catch (ElementNotAvailableException)
            {
                // Element vanished mid-inspection; skip it.
            }
        }

        private AutomationElement WaitFor(string description, Func<AutomationElement> find)
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                // UI Automation can briefly throw while a window is being created or torn down.
                AutomationElement element = null;
                try
                {
                    element = find();
                }
                catch (ElementNotAvailableException)
                {
                    // Retry below.
                }
                if (element != null)
                    return element;
                if (stopwatch.ElapsedMilliseconds > _millisTimeout)
                    throw new TimeoutException(string.Format(@"Timed out after {0} ms waiting for {1}.", _millisTimeout, description));
                Thread.Sleep(POLL_INTERVAL_MILLIS);
            }
        }
    }
}
