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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

using pwiz.Common.SystemUtil.PInvoke;
using SkylineTool;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Drives the classic Windows "Browse For Folder" dialog, the SHBrowseForFolder tree shown by WinForms
    /// <see cref="System.Windows.Forms.FolderBrowserDialog"/>. It has no file-name box: a folder is chosen in a
    /// tree, so set_value selects the folder by sending the dialog BFFM_SETSELECTION with the path.
    /// </summary>
    public class NativeFolderBrowserDialog : NativeDialog
    {
        // Sent with wParam TRUE (lParam is a path string rather than a PIDL) and lParam the path.
        private const int BFFM_SETSELECTIONW = 0x0400 + 103; // WM_USER + 103
        private const int IDOK = 1;

        public override string DialogTypeName => @"FolderBrowserDialog";

        public NativeFolderBrowserDialog(IntPtr windowHandle, CancellationToken cancellationToken) : base(windowHandle, cancellationToken)
        {
        }

        /// <summary>Whether the "#32770" is the classic Browse-For-Folder dialog, identified by its folder tree.
        /// (The file dialogs' navigation pane also has a tree; they are classified first.)</summary>
        public static bool IsFolderBrowserDialog(IntPtr hwnd)
        {
            return new NativeFolderBrowserDialog(hwnd, CancellationToken.None)
                .FindDescendants(NativeControl.TREE_CLASS).Any();
        }

        /// <summary>Shown, not merely present: BFFM_SETSELECTION on a tree the shell has not displayed yet fails
        /// silently, and the dialog is then accepted on the default folder.</summary>
        protected override bool IsOpenComplete =>
            FindDescendants(NativeControl.TREE_CLASS).Any(User32.IsWindowVisible);

        // BFFM_SETSELECTION must be sent, not posted: the dialog reads the path string while this blocks. It only
        // navigates the tree (no nested modal), so the synchronous send does not wedge.
        protected override void SetValueCore(string value)
        {
            var pathPtr = Marshal.StringToHGlobalUni(value);
            try
            {
                User32.SendMessage(Hwnd, (User32.WinMessageType)BFFM_SETSELECTIONW, (IntPtr)1, pathPtr);
            }
            finally
            {
                Marshal.FreeHGlobal(pathPtr);
            }
        }

        // OkDialog sends BM_CLICK on the dialog's own thread and waits for the dialog to close; a cross-thread send
        // would wedge the caller.
        public override ActionResult DismissWithAcceptButton()
        {
            var okButton = RequireButton(IDOK, @"OK");
            return OkDialog(okButton.ClickNow);
        }
    }
}
