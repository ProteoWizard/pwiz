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
    /// Drives the native Windows common Save file dialog, the modern dialog shown by
    /// <see cref="System.Windows.Forms.SaveFileDialog"/>. Unlike the Open dialog it has no classic file-name
    /// combo; its file-name field is an Edit (control id 1001) inside the DirectUI surface.
    /// </summary>
    public class NativeSaveFileDialog : NativeFileDialog
    {
        // Shared with the address bar's breadcrumb, which is a ToolbarWindow32, so class + id identifies the Edit.
        private const int FILE_NAME_EDIT_ID = 1001;

        public override string DialogTypeName => @"SaveFileDialog";

        protected override int FileNameControlId => FILE_NAME_EDIT_ID;

        public NativeSaveFileDialog(IntPtr windowHandle, CancellationToken cancellationToken) : base(windowHandle, cancellationToken)
        {
        }

        /// <summary>Whether the "#32770" is a modern Save file dialog, identified by its file-name Edit.</summary>
        public static bool IsSaveFileDialog(IntPtr hwnd)
        {
            return new NativeSaveFileDialog(hwnd, CancellationToken.None)
                .HasDescendant(NativeControl.EDIT_CLASS, FILE_NAME_EDIT_ID);
        }

        /// <summary>Clicks Save on the dialog's own thread: the click can raise the overwrite-confirm prompt, whose
        /// modal loop then runs there with the action still counted, instead of pinning the calling thread.</summary>
        public override ActionResult DismissWithAcceptButton()
        {
            var saveButton = AcceptButton;
            return OkDialog(saveButton.ClickNow);
        }

        protected override string CommitButtonDescription => @"Save";
    }
}
