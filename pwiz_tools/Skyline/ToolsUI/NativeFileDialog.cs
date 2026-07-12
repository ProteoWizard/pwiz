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
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Base for the native common file dialogs -- Open (<see cref="NativeOpenFileDialog"/>) and
    /// Save (<see cref="NativeSaveFileDialog"/>). Both are "#32770" dialogs that take a file name
    /// and then accept or cancel, but they expose their file-name field differently, so the
    /// path-entry gesture is abstract. Like every native dialog they report the generic
    /// <see cref="NativeDialog.DialogTypeName"/> "Dialog" (their file-dialog nature is not knowable the
    /// instant the window appears -- see that property -- so it is not baked into the form id).
    /// </summary>
    public abstract class NativeFileDialog : NativeDialog
    {
        protected NativeFileDialog(IntPtr windowHandle, CancellationToken cancellationToken) : base(windowHandle, cancellationToken)
        {
        }

        /// <summary>
        /// Types the file name(s) into the dialog's file name field without accepting; call
        /// <see cref="NativeDialog.DismissWithAcceptButton"/> to open/save. The Open dialog accepts several
        /// double-quoted, space-separated paths for a multiselect; the Save dialog takes a single path.
        /// </summary>
        public abstract void EnterPath(string path);

        /// <summary>The control id of this dialog's file-name Edit -- 1148 for the Open dialog's classic combo,
        /// 1001 for the Save dialog's DirectUI-hosted field.</summary>
        protected abstract int FileNameControlId { get; }

        /// <summary>The dialog's file-name field, found by its CONTROL ID and WAITED FOR until it is actually shown.
        ///
        /// <para>Both halves matter. By control id, because the field is not "the dialog's only text box": the
        /// address bar carries a second, collapsed Edit. And waited for, because a native dialog becomes
        /// discoverable -- its window exists, GetOpenForms reports it, and it classifies as a file dialog -- a moment
        /// BEFORE the shell has finished showing and populating it. Typing into the field in that window does
        /// nothing: the shell overwrites the text as it finishes initializing, and the dialog then accepts an empty
        /// name. So a caller driving a dialog the instant it appears (which is exactly what the connector and the
        /// tests do) MUST wait for the field to be visible first.</para></summary>
        protected NativeTextBox FileNameTextBox =>
            PollUntil(MillisTimeout, @"the dialog's file name field", () =>
            {
                var hwnd = FindDescendant(NativeControl.EDIT_CLASS, FileNameControlId);
                return hwnd != IntPtr.Zero && User32.IsWindowVisible(hwnd)
                    ? new NativeTextBox(hwnd, CancellationToken)
                    : null;
            });

        // set_value types the path: its controlId is ignored, because a file dialog has the one field to set.
        protected override void SetValueCore(string value) => EnterPath(value);
    }
}
