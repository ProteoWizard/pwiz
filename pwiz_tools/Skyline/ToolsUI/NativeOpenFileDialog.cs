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
    /// Drives the native Windows common Open/Save file dialog (such as the OpenFileDialog shown
    /// by <c>SkylineWindow.ShowOpenFileDialog</c>), through its real Win32 child windows. See
    /// <see cref="NativeDialog"/> for the threading contract and how to obtain an
    /// instance.
    /// </summary>
    public class NativeOpenFileDialog : NativeFileDialog
    {
        // The control id the Windows common Open dialog gives its "File name" combo (cmb13) -- carried by the
        // ComboBoxEx32, its ComboBox and the Edit inside it alike. Unlike the localized captions it is stable
        // across Windows versions and locales, and its presence tells the Open dialog from every other "#32770".
        private const int FILE_NAME_COMBO_ID = 1148;

        public override string DialogTypeName => @"OpenFileDialog";

        protected override int FileNameControlId => FILE_NAME_COMBO_ID;

        public NativeOpenFileDialog(IntPtr windowHandle, CancellationToken cancellationToken) : base(windowHandle, cancellationToken)
        {
        }

        /// <summary>
        /// Whether the "#32770" is the common Open dialog, identified by its classic file-name combo (control id
        /// 1148). The whole combo -- the ComboBoxEx32, its ComboBox and the Edit inside it -- carries that id; the
        /// Save dialog has no such control, so the two never both match.
        /// </summary>
        public static bool IsOpenFileDialog(IntPtr hwnd)
        {
            return new NativeOpenFileDialog(hwnd, CancellationToken.None)
                .HasDescendantWithControlId(FILE_NAME_COMBO_ID);
        }

        /// <summary>
        /// Opens the file at the given path. The full path is typed into the file name box and then Enter is
        /// pressed, the same way a user can paste a full path and press Enter to navigate to the folder and open
        /// the file in one action -- so this does not depend on whatever folder the dialog happened to open in. The
        /// path is set on the Edit INSIDE the file-name combo (setting it on the combo itself would trigger the
        /// auto-complete that discards the directory portion).
        /// </summary>
        public void EnterPathAndAccept(string path)
        {
            var edit = FileNameTextBox;
            edit.SetText(path);
            PostEnter(edit.Hwnd);
        }

        /// <summary>
        /// Types a file name into the dialog's file name box without accepting; call
        /// <see cref="NativeDialog.DismissWithAcceptButton"/> to open. Pass several double-quoted, space-separated
        /// paths (<c>"a" "b"</c>) to select multiple files in a multiselect dialog.
        /// </summary>
        public override void EnterPath(string path)
        {
            BringToForeground();
            FileNameTextBox.SetText(path);
        }

        /// <summary>Accepts by pressing Enter in the file-name box (rather than clicking Open, which would act on
        /// whatever the file list has selected rather than the path just typed). OkDialog POSTS Enter on the
        /// dialog's UI thread -- so the modal loop translates it into accept, see PostEnter -- and waits for the
        /// dialog to close.</summary>
        public override ActionResult DismissWithAcceptButton()
        {
            var handle = FileNameTextBox.Hwnd;
            return OkDialog(() => PostEnter(handle));
        }
    }
}
