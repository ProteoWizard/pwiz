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
using SkylineTool;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Drives the native Windows common Save file dialog (the modern Vista-style dialog shown by
    /// <see cref="System.Windows.Forms.SaveFileDialog"/>), through its real Win32 child windows. See
    /// <see cref="NativeDialog"/> for how an instance is obtained.
    ///
    /// Unlike the Open dialog, the Save dialog does not have the classic file-name combo (control id 1148); its
    /// file-name field sits inside the DirectUI surface. Both are nonetheless real Win32 child windows -- the file
    /// name is a class "Edit" with control id 1001, and the Save button a class "Button" with the standard IDOK
    /// control id (1) -- so the file name is set with WM_SETTEXT and the dialog accepted with BM_CLICK.
    /// </summary>
    public class NativeSaveFileDialog : NativeFileDialog
    {
        // The Save dialog's file-name Edit. Its control id (1001) is shared with the address bar's breadcrumb, but
        // the breadcrumb is a ToolbarWindow32 -- so class + id identifies the Edit unambiguously, and nothing has to
        // walk to the DirectUI host that owns it.
        private const int FILE_NAME_EDIT_ID = 1001;
        private const int IDOK = 1; // the Save button

        public override string DialogTypeName => @"SaveFileDialog";

        protected override int FileNameControlId => FILE_NAME_EDIT_ID;

        public NativeSaveFileDialog(IntPtr windowHandle, CancellationToken cancellationToken) : base(windowHandle, cancellationToken)
        {
        }

        /// <summary>
        /// Whether the "#32770" is a modern Save file dialog, identified by its file-name Edit. Mutually exclusive
        /// with <see cref="NativeOpenFileDialog.IsOpenFileDialog"/>, which is checked first: the Open dialog has the
        /// classic combo (control id 1148) and this one does not.
        /// </summary>
        public static bool IsSaveFileDialog(IntPtr hwnd)
        {
            return new NativeSaveFileDialog(hwnd, CancellationToken.None)
                .HasDescendant(NativeControl.EDIT_CLASS, FILE_NAME_EDIT_ID);
        }

        /// <summary>Accepts by clicking the Save button. OkDialog SENDS BM_CLICK on the dialog's OWN thread and
        /// waits for the dialog to close: clicking Save can raise a nested modal (the overwrite-confirm prompt), and
        /// running the send on the UI thread lets that modal's loop run there -- the send blocks in it, keeping the
        /// action counted and letting the wait detect the prompt -- instead of pinning the pipe thread, as a
        /// cross-thread send would.</summary>
        public override ActionResult DismissWithAcceptButton()
        {
            var saveButton = AcceptButton;
            return OkDialog(saveButton.ClickNow);
        }

        // The Save button, by its control id rather than its (localized) caption.
        private NativeButton AcceptButton => RequireButton(IDOK, @"Save");
    }
}
