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
    /// Drives the native Windows common Open file dialog, the modern dialog shown by
    /// <see cref="System.Windows.Forms.OpenFileDialog"/>. Its file-name field is the classic combo (cmb13 in
    /// dlgs.h, control id 1148), which the Save dialog does not keep.
    /// </summary>
    public class NativeOpenFileDialog : NativeFileDialog
    {
        // Carried by the ComboBoxEx32, its ComboBox and the Edit inside it alike.
        private const int FILE_NAME_COMBO_ID = 1148;

        public override string DialogTypeName => @"OpenFileDialog";

        protected override int FileNameControlId => FILE_NAME_COMBO_ID;

        public NativeOpenFileDialog(IntPtr windowHandle, CancellationToken cancellationToken) : base(windowHandle, cancellationToken)
        {
        }

        /// <summary>Whether the "#32770" is the common Open dialog, identified by its classic file-name combo.</summary>
        public static bool IsOpenFileDialog(IntPtr hwnd)
        {
            return new NativeOpenFileDialog(hwnd, CancellationToken.None)
                .HasDescendantWithControlId(FILE_NAME_COMBO_ID);
        }

        /// <summary>Clicks Open rather than posting Enter: on the multiselect dialog a typed file name raises the
        /// combo's autocomplete drop-down, which swallows the Enter, so the dialog never closes. With a file name
        /// in the box the dialog opens that, not any file-list selection.</summary>
        public override ActionResult DismissWithAcceptButton()
        {
            return OkDialog(AcceptButton.ClickNow);
        }

        protected override string CommitButtonDescription => @"Open";

        // The Edit that carries the combo's id when there is one; otherwise, for a flavour that keeps the id only
        // on the combo, the Edit inside the combo (never the address-bar Edit, which is not a child of it).
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
    }
}
