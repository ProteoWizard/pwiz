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
using System.Threading;
using pwiz.Common.SystemUtil.PInvoke;
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

        private const int IDOK = 1; // the Open button's control id

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

        /// <summary>Accepts by clicking the Open button (BM_CLICK on IDOK). A posted Enter is NOT reliable here: on
        /// the multiselect dialog a single typed file name raises the combo's autocomplete drop-down, which swallows
        /// the Enter (it selects the drop-down item instead of committing) -- so the dialog never closes. Clicking
        /// Open commits in one message. With a file name in the box the dialog opens THAT (not any file-list
        /// selection), which is what the caller typed. OkDialog SENDS the click on the dialog's own thread and waits
        /// for it to close -- so a click that raises a nested modal (an overwrite/error prompt) blocks there,
        /// counted, rather than pinning the pipe thread.</summary>
        public override ActionResult DismissWithAcceptButton()
        {
            return OkDialog(AcceptButton.ClickNow);
        }

        // The Open button, by its control id (IDOK) rather than its localized caption.
        private NativeButton AcceptButton => RequireButton(IDOK, @"Open");

        // The file-name Edit. The classic combo's id (1148) rides the ComboBoxEx32, its ComboBox and (at least on
        // current Windows, the multiselect "Add Input Files" dialog included) the Edit inside it. Take the Edit
        // that carries the id when there is one; otherwise -- a flavour that keeps the id only on the combo -- take
        // the Edit inside the combo that does, which is never the address-bar Edit (not a child of this combo).
        protected override IntPtr FindFileNameEdit()
        {
            var edit = base.FindFileNameEdit();
            if (edit != IntPtr.Zero)
                return edit;
            var combo = User32.EnumChildWindows(Hwnd)
                .FirstOrDefault(hwnd => User32.GetDlgCtrlID(hwnd) == FILE_NAME_COMBO_ID);
            return combo == IntPtr.Zero
                ? IntPtr.Zero
                : User32.EnumChildWindows(combo)
                    .FirstOrDefault(hwnd => User32.GetClassName(hwnd) == NativeControl.EDIT_CLASS);
        }

        /// <summary>
        /// Commits whatever is in the file-name box by clicking the Open button: a FOLDER path navigates into that
        /// folder and leaves the dialog open, while file name(s) open and the dialog closes. Clicking Open (rather
        /// than posting Enter) is what makes this reliable -- see <see cref="DismissWithAcceptButton"/> for why a
        /// posted Enter is swallowed on the multiselect dialog. The caller drives the next step by observing the
        /// result: it confirms a NAVIGATION by reading the dialog's current folder from GetControls (the read-only
        /// "AddressBar" control, see <see cref="NativeAddressBar"/>) and an OPEN by the dialog closing.
        ///
        /// <para>So the multiselect Open dialog is driven like this: <see cref="NativeFileDialog.EnterPath"/> the
        /// folder and Accept, wait (via GetControls) until the folder is reached, then EnterPath the double-quoted
        /// space-separated file names and Accept to open them. The BM_CLICK is SENT, so call this OFF the UI thread,
        /// where the dialog's modal loop can process it.</para>
        ///
        /// <para>One click may not commit: typing the names raises the combo's shell autocomplete drop-down, and a
        /// click can be spent closing that drop-down rather than opening. So the caller VERIFIES -- checks whether
        /// the dialog closed (it is no longer in GetOpenForms) -- and clicks again if it did not, rather than
        /// assuming a single Accept opened the files.</para>
        /// </summary>
        public void Accept()
        {
            AcceptButton.ClickNow();
        }
    }
}
