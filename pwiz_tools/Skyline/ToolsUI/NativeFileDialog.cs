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

        // A file dialog is not introspected as a set of child controls; it is driven entirely at the form
        // level -- set_value types the path (its controlId is ignored, a file dialog has the one file-name
        // field) and the accept action commits it (the cancel verb cancels).
        protected override void SetValueCore(string value) => EnterPath(value);
    }
}
