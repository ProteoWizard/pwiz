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
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using pwiz.Common.SystemUtil.PInvoke;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Drives a native Windows common file dialog (such as the OpenFileDialog shown by
    /// <c>SkylineWindow.ShowOpenFileDialog</c>) using UI Automation. The native dialog is not a
    /// WinForms form, so it cannot be introspected or manipulated the way the rest of Skyline's
    /// UI is (e.g. via <see cref="JsonUiService"/> walking <c>FormUtil.OpenForms</c>); UI
    /// Automation is the mechanism that lets an external driver interact with it the same way a
    /// user would.
    ///
    /// This is shared product code with two consumers:
    ///  - The functional test framework (AbstractFunctionalTest.OpenDocument), and
    ///  - The Skyline MCP server's generic UI interaction layer.
    ///
    /// The dialog is modal and runs its own message loop on the Skyline UI thread, so the action
    /// that displays it must be posted to the UI thread (e.g. with BeginInvoke) and these methods
    /// must be called from a different thread (the test thread or the MCP pipe thread), never from
    /// the UI thread itself.
    /// </summary>
    public static class NativeFileDialogAutomation
    {
        // Control identifiers assigned by the Windows common file dialog. Unlike the
        // localized control captions, these are stable across Windows versions and locales.
        private const string FILE_NAME_COMBO_ID = @"1148"; // "File name:" combo box (cmb13)
        private const string DIALOG_CLASS_NAME = @"#32770"; // Win32 dialog window class

        private const int DEFAULT_TIMEOUT_MILLIS = 30 * 1000;
        private const int POLL_INTERVAL_MILLIS = 100;

        private const int VK_RETURN = 0x0D;

        /// <summary>
        /// Waits for a native file dialog to appear in the current process and opens the file at
        /// the given path. The full path is typed into the file name box and then Enter is
        /// pressed, the same way a user can paste a full path and press Enter to navigate to the
        /// folder and open the file in one action -- so this does not depend on whatever folder
        /// the dialog happened to open in. The path is set on the Edit control inside the "File
        /// name" combo box (setting it on the combo box would trigger auto-complete that discards
        /// the directory portion), and Enter is posted as a window message because invoking the
        /// Open button through UI Automation -- the common file dialog is a DirectUI surface --
        /// is not always honored.
        /// </summary>
        public static void EnterPathAndAccept(string path, int millisTimeout = DEFAULT_TIMEOUT_MILLIS)
        {
            var dialog = WaitForDialog(millisTimeout);
            User32.SetForegroundWindow(new IntPtr(dialog.Current.NativeWindowHandle));
            var fileNameComboBox = WaitForElement(dialog, FILE_NAME_COMBO_ID, millisTimeout);
            var edit = fileNameComboBox.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            if (edit == null)
                throw new InvalidOperationException(@"Could not find the file name edit control in the file dialog.");
            var valuePattern = (ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern);
            valuePattern.SetValue(path);
            var editHandle = new IntPtr(edit.Current.NativeWindowHandle);
            User32.PostMessageA(editHandle, User32.WinMessageType.WM_KEYDOWN, VK_RETURN, 0);
            User32.PostMessageA(editHandle, User32.WinMessageType.WM_KEYUP, VK_RETURN, 0);
        }

        /// <summary>
        /// Waits for a native file dialog to appear in the current process and dismisses it
        /// without selecting a file. A WM_CLOSE posted to the dialog window cancels it the same
        /// way the title-bar close button or the Cancel button would; this is more reliable than
        /// invoking the Cancel button through UI Automation, which the common file dialog does not
        /// always honor.
        /// </summary>
        public static void Cancel(int millisTimeout = DEFAULT_TIMEOUT_MILLIS)
        {
            var dialog = WaitForDialog(millisTimeout);
            var handle = new IntPtr(dialog.Current.NativeWindowHandle);
            User32.PostMessageA(handle, User32.WinMessageType.WM_CLOSE, 0, 0);
        }

        /// <summary>
        /// Identifies an open native common file dialog: its window caption and window handle.
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
        /// Returns the native common file dialogs (Open/Save) currently open in this process,
        /// found via UI Automation. These are native windows, not WinForms forms, so they do not
        /// appear in FormUtil.OpenForms. Must be called from a thread other than the UI thread.
        /// </summary>
        public static IList<NativeDialogInfo> GetOpenDialogs()
        {
            var result = new List<NativeDialogInfo>();
            var seen = new HashSet<IntPtr>();
            foreach (var dialog in FindFileDialogElements())
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

        private static AutomationElement WaitForDialog(int millisTimeout)
        {
            var processId = Process.GetCurrentProcess().Id;
            var dialogCondition = new AndCondition(
                new PropertyCondition(AutomationElement.ProcessIdProperty, processId),
                new PropertyCondition(AutomationElement.ClassNameProperty, DIALOG_CLASS_NAME));
            // The common file dialog is an owned window of the Skyline main window, so in the UI
            // Automation tree it appears as a descendant of that window rather than as a direct
            // child of the desktop root. Scope the search to this process's top-level windows.
            var topLevelCondition = new AndCondition(
                new PropertyCondition(AutomationElement.ProcessIdProperty, processId),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
            return WaitFor(millisTimeout, @"native file dialog", () =>
            {
                var topLevelWindows = AutomationElement.RootElement.FindAll(TreeScope.Children, topLevelCondition);
                foreach (AutomationElement topLevelWindow in topLevelWindows)
                {
                    var dialog = topLevelWindow.FindFirst(TreeScope.Subtree, dialogCondition);
                    if (dialog != null)
                        return dialog;
                }
                return null;
            });
        }

        /// <summary>
        /// Finds the open native common file dialogs of the current process via UI Automation,
        /// for enumeration (as opposed to <see cref="WaitForDialog"/>, which finds one to drive).
        /// A common file dialog is an owned window of the Skyline main window, so in the UI
        /// Automation tree it appears either as a direct child of the desktop root or as a direct
        /// child of its owner window. We look only at this process's top-level windows and their
        /// direct children -- never a full subtree walk, which is prohibitively slow over
        /// Skyline's large control tree.
        /// </summary>
        private static IList<AutomationElement> FindFileDialogElements()
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
                    AddIfFileDialog(window, result);
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
                        AddIfFileDialog(childDialog, result);
                }
            }
            catch (ElementNotAvailableException)
            {
                // The UI Automation tree changed during enumeration; return what was found.
            }
            return result;
        }

        private static void AddIfFileDialog(AutomationElement element, IList<AutomationElement> result)
        {
            try
            {
                if (element.Current.ClassName != DIALOG_CLASS_NAME)
                    return;
                // Restrict to common file dialogs, identified by the file name combo box. This
                // search is bounded to the (small) dialog subtree, not the whole window tree.
                if (element.FindFirst(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.AutomationIdProperty, FILE_NAME_COMBO_ID)) == null)
                    return;
                result.Add(element);
            }
            catch (ElementNotAvailableException)
            {
                // Element vanished mid-inspection; skip it.
            }
        }

        private static AutomationElement WaitForElement(AutomationElement root, string automationId, int millisTimeout)
        {
            var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
            return WaitFor(millisTimeout, @"dialog control with AutomationId " + automationId,
                () => root.FindFirst(TreeScope.Descendants, condition));
        }

        private static AutomationElement WaitFor(int millisTimeout, string description, Func<AutomationElement> find)
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
                if (stopwatch.ElapsedMilliseconds > millisTimeout)
                    throw new TimeoutException(string.Format(@"Timed out after {0} ms waiting for {1}.", millisTimeout, description));
                Thread.Sleep(POLL_INTERVAL_MILLIS);
            }
        }
    }
}
