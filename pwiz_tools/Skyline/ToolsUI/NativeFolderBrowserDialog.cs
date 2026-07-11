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
    /// Drives the classic Windows "Browse For Folder" dialog -- the SHBrowseForFolder tree shown by WinForms
    /// <see cref="System.Windows.Forms.FolderBrowserDialog"/> (e.g. the "Results Directory" picker used when
    /// importing multi-injection replicates from directories). Unlike the common Open/Save dialog it has no
    /// file-name box: a folder is chosen in a tree. So set_value selects the folder by sending the dialog a
    /// BFFM_SETSELECTION message with the path, and the accept gesture clicks its OK button. See
    /// <see cref="NativeDialog"/> for the threading contract and how an instance is obtained.
    /// </summary>
    public class NativeFolderBrowserDialog : NativeDialog
    {
        // BFFM_SETSELECTION (Unicode): tells an open Browse-For-Folder dialog which folder to select. Sent to
        // the dialog window with wParam TRUE (the lParam is a path string rather than a PIDL) and lParam the path.
        private const int BFFM_SETSELECTIONW = 0x0400 + 103; // WM_USER + 103
        private const int IDOK = 1; // the dialog's OK button carries this control id as its AutomationId

        public NativeFolderBrowserDialog(IntPtr windowHandle) : base(windowHandle)
        {
        }

        public override string DialogTypeName => @"FolderDialog";

        /// <summary>
        /// Returns true if the given "#32770" dialog is the classic Browse-For-Folder dialog, identified by its
        /// folder Tree. The Open/Save file dialogs also have a tree (their navigation pane) but are matched
        /// first by their file-name combo (see <see cref="NativeDialog.Create"/>), so only the folder browser
        /// reaches this check.
        /// </summary>
        public static bool IsFolderBrowserDialog(AutomationElement dialog)
        {
            try
            {
                return dialog.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Tree)) != null;
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
        }

        // set_value selects the folder at the given path in the tree. BFFM_SETSELECTION must be SENT (not
        // posted), so the dialog reads the path string while this blocks and the string stays valid until it
        // returns; it merely navigates the tree (no nested modal), so the synchronous send does not wedge.
        protected override void SetValueCore(string value)
        {
            BringToForeground();
            var pathPtr = Marshal.StringToHGlobalUni(value);
            try
            {
                User32.SendMessage(WindowHandle, (User32.WinMessageType)BFFM_SETSELECTIONW, (IntPtr)1, pathPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(pathPtr);
            }
        }

        // PostAccept clicks OK (posted BM_CLICK, like the file dialogs -- the click closes the dialog and unwinds
        // its modal loop, so a synchronous send could wedge the caller). OK is found by its control id, not a
        // localized caption. The base Accept waits for the dialog to close.
        public override void PostAccept()
        {
            var okButton = WaitForElement(IDOK.ToString());
            User32.PostMessageA(new IntPtr(okButton.Current.NativeWindowHandle), User32.WinMessageType.BM_CLICK, 0, 0);
        }
    }
}
