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
using System.Runtime.InteropServices;
using System.Windows.Automation;
using pwiz.Common.SystemUtil.PInvoke;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Drives the native Windows common Save file dialog (the modern Vista-style dialog shown by
    /// <see cref="System.Windows.Forms.SaveFileDialog"/>), using UI Automation to locate controls and
    /// Win32 window messages to drive them. See <see cref="NativeDialog"/> for the threading
    /// contract and how to obtain an instance.
    ///
    /// Unlike the Open dialog, the Save dialog does NOT expose the classic file-name combo
    /// (AutomationId 1148), and its file-name field and Save button do NOT support the UI Automation
    /// Value / Invoke patterns -- they are DirectUI-hosted. They are, however, real Win32 child
    /// windows: the file name is a class "Edit" control (AutomationId 1001) inside an "AppControlHost"
    /// (AutomationId "FileNameControlHost"), and the Save button is a class "Button" with the standard
    /// IDOK control id (1). So the file name is set with WM_SETTEXT and the dialog accepted with
    /// BM_CLICK (Cancel is the inherited WM_CLOSE).
    /// </summary>
    public class NativeSaveFileDialog : NativeFileDialog
    {
        // Host of the file-name Edit. Its presence identifies the modern Save dialog (the Open dialog
        // uses the classic combo instead), and the Edit's own AutomationId (1001) is shared by the
        // address-bar breadcrumb, so we locate the Edit by host + class rather than by that id.
        private const string FILE_NAME_HOST_ID = @"FileNameControlHost";
        private const string ACCEPT_BUTTON_ID = @"1"; // IDOK ("Save")
        private const string EDIT_CLASS_NAME = @"Edit";
        private const string BUTTON_CLASS_NAME = @"Button";

        public NativeSaveFileDialog(IntPtr windowHandle) : base(windowHandle)
        {
        }

        /// <summary>
        /// Returns true if the given native dialog is a modern Save file dialog, identified by its
        /// file-name control host. Mutually exclusive with
        /// <see cref="NativeOpenFileDialog.IsOpenFileDialog"/> (the Open dialog has the classic
        /// combo, the Save dialog has this host).
        /// </summary>
        public static bool IsSaveFileDialog(AutomationElement dialog)
        {
            try
            {
                return dialog.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, FILE_NAME_HOST_ID)) != null;
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
        }

        /// <summary>Types a single file path into the dialog's file name box without accepting.</summary>
        public override void EnterPath(string path)
        {
            BringToForeground();
            SetWindowText(GetFileNameEditHandle(), path);
        }

        /// <summary>Posts the Save-button click (the base Accept waits for the dialog to close).</summary>
        public override void PostAccept()
        {
            // Post (not send) BM_CLICK: clicking Save can raise a nested modal (the overwrite-confirm prompt), whose
            // message loop would never return to a synchronous SendMessage and so would wedge the single-instance
            // connector. PostMessage returns at once, leaving the pipe thread free to drive that second dialog.
            User32.PostMessageA(GetSaveButtonHandle(), User32.WinMessageType.BM_CLICK, 0, 0);
        }

        // The file-name Edit is the class "Edit" control inside the file-name control host. Find it by
        // host then class -- its AutomationId (1001) alone is ambiguous (the address-bar breadcrumb
        // shares it).
        private IntPtr GetFileNameEditHandle()
        {
            var host = WaitForElement(FILE_NAME_HOST_ID);
            var edit = host.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ClassNameProperty, EDIT_CLASS_NAME));
            if (edit == null)
                throw new InvalidOperationException(@"Could not find the file name edit control in the save dialog.");
            return GetNativeHandle(edit, @"file name edit");
        }

        private IntPtr GetSaveButtonHandle()
        {
            var button = WaitForElement(
                new AndCondition(
                    new PropertyCondition(AutomationElement.AutomationIdProperty, ACCEPT_BUTTON_ID),
                    new PropertyCondition(AutomationElement.ClassNameProperty, BUTTON_CLASS_NAME)),
                @"save button");
            return GetNativeHandle(button, @"save button");
        }

        private static IntPtr GetNativeHandle(AutomationElement element, string description)
        {
            var handle = new IntPtr(element.Current.NativeWindowHandle);
            if (handle == IntPtr.Zero)
                throw new InvalidOperationException(
                    string.Format(@"The save dialog's {0} has no native window handle.", description));
            return handle;
        }

        // Sets a control's text with WM_SETTEXT. lParam points at the (Unicode) string; this runs
        // in-process with the dialog (the JSON tool server is hosted inside Skyline), so a pointer
        // allocated here is valid in the receiving control's process.
        private static void SetWindowText(IntPtr handle, string text)
        {
            var lParam = Marshal.StringToHGlobalUni(text);
            try
            {
                User32.SendMessage(handle, User32.WinMessageType.WM_SETTEXT, IntPtr.Zero, lParam);
            }
            finally
            {
                Marshal.FreeHGlobal(lParam);
            }
        }
    }
}
