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
using System.Threading;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Base for the native common file dialogs -- Open (<see cref="NativeOpenFileDialog"/>) and
    /// Save (<see cref="NativeSaveFileDialog"/>). Both are "#32770" dialogs that take a file name and then accept or
    /// cancel, but they expose their file-name field differently, so the path-entry gesture is abstract. Each says
    /// which it is in its <see cref="NativeDialog.DialogTypeName"/>, so it is distinguishable from the message box a
    /// file dialog raises -- which carries the file dialog's own caption.
    /// </summary>
    public abstract class NativeFileDialog : NativeDialog
    {
        // The address breadcrumb's window class and control id -- the "Address: <folder>" toolbar. Surfaced through
        // EnumerateChildren as a read-only element so a caller can read the dialog's current folder from GetControls.
        private const string ADDRESS_BAR_CLASS = @"ToolbarWindow32";
        private const int ADDRESS_BAR_ID = 1001;

        protected NativeFileDialog(IntPtr windowHandle, CancellationToken cancellationToken) : base(windowHandle, cancellationToken)
        {
        }

        /// <summary>The dialog's controls: its Win32 children (the file-name field, the commit and cancel buttons)
        /// PLUS the address breadcrumb as a read-only <see cref="NativeAddressBar"/> element -- so a caller can read
        /// the folder the dialog is showing from GetControls and confirm a navigation before selecting files. The
        /// file-name box is given the label "File name" so a caller can read/set it by that name (its adjacent
        /// "File name:" static would otherwise shadow the caption-less field, and its own value is empty).</summary>
        public override IEnumerable<UiElement> EnumerateChildren()
        {
            var fileNameEdit = FindFileNameEdit();
            foreach (var child in base.EnumerateChildren())
            {
                // Drop the field-label statics ("File name:", "Files of type:"): they carry no value, and a caption
                // matching a field's would SHADOW it (the static comes first, so a get/set on "File name" would hit
                // the empty static, not the box). The box is given that caption directly, below.
                if (child is NativeLabel)
                    continue;
                yield return child is NativeTextBox textBox && textBox.Hwnd == fileNameEdit
                    ? new NativeTextBox(fileNameEdit, CancellationToken, @"File name")
                    : child;
            }
            var addressBar = FindDescendant(ADDRESS_BAR_CLASS, ADDRESS_BAR_ID);
            if (addressBar != IntPtr.Zero)
                yield return new NativeAddressBar(addressBar, CancellationToken);
        }

        /// <summary>
        /// Types the file name(s) into the dialog's file-name field WITHOUT accepting; call
        /// <see cref="NativeDialog.DismissWithAcceptButton"/> to open/save. Confirms the text registered, and
        /// throws if it did not, so a lost set is reported rather than leaving the dialog to open nothing.
        ///
        /// <para>To select several files in a multiselect Open dialog, FIRST navigate to their folder (EnterPath
        /// the folder path, accept), THEN EnterPath their names -- BARE names in that folder, double-quoted and
        /// space-separated (<c>"a.raw" "b.raw"</c>). A list of FULL paths does not work.</para>
        /// </summary>
        public void EnterPath(string path)
        {
            BringToForeground();
            var textBox = FileNameTextBox;
            textBox.SetText(path);
            // Read back what the box actually HOLDS, rather than predicting what the shell will make of the path.
            // An EMPTY box means the shell consumed the path to navigate -- the caller confirms that through the
            // "Address" control; the path itself means a file name is staged to open. Anything else means the set
            // did not take. The read SENDS WM_GETTEXT (NativeTextBox.GetValueNow), which is thread-agnostic.
            var actual = textBox.GetValueNow() as string;
            if (string.IsNullOrEmpty(actual) || Equals(actual, path))
                return;
            throw new InvalidOperationException(LlmInstruction.Format(
                @"Tried to set file path to '{0}' but it says '{1}'", path, actual));
        }

        /// <summary>The control id of this dialog's file-name Edit -- 1148 for the Open dialog's classic combo,
        /// 1001 for the Save dialog's DirectUI-hosted field.</summary>
        protected abstract int FileNameControlId { get; }

        /// <summary>The dialog's file-name field, found by its CONTROL ID, or a RETRYABLE error if the shell has
        /// not shown it yet.
        ///
        /// <para>By control id, because the field is not "the dialog's only text box": the address bar carries a
        /// second, collapsed Edit. Not-shown-yet is possible because a native dialog becomes discoverable -- its
        /// window exists, GetOpenForms reports it, and it classifies as a file dialog -- a moment BEFORE the shell
        /// has finished showing and populating it, and typing into the field in that window does nothing (the shell
        /// overwrites the text as it initializes, and the dialog then accepts an empty name).</para>
        ///
        /// <para>Rather than BLOCK here polling for the field, this throws an instruction the caller acts on, so the
        /// wait lives with the DRIVER, not inside the primitive both drivers share: the model waits and re-issues
        /// the call, and a test driving the dialog waits on its own state. A single tool call never sits inside a
        /// 30-second sleep.</para></summary>
        protected NativeTextBox FileNameTextBox
        {
            get
            {
                var hwnd = FindFileNameEdit();
                if (hwnd == IntPtr.Zero || !User32.IsWindowVisible(hwnd))
                    throw new InvalidOperationException(LlmInstruction.Format(
                        @"The file dialog is still opening. Wait a moment and try again."));
                return new NativeTextBox(hwnd, CancellationToken);
            }
        }

        /// <summary>The window handle of the file-name Edit, or IntPtr.Zero until it exists. By default the Edit
        /// itself carries <see cref="FileNameControlId"/> (the Save dialog's Edit, the plain Open dialog's combo
        /// Edit); the Open dialog overrides this because its multiselect flavour does not put the id on the Edit.</summary>
        protected virtual IntPtr FindFileNameEdit() => FindDescendant(NativeControl.EDIT_CLASS, FileNameControlId);

        // set_value types the path: its controlId is ignored, because a file dialog has the one field to set.
        protected override void SetValueCore(string value) => EnterPath(value);
    }
}
