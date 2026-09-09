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
using SkylineTool;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Base for the native common file dialogs, Open and Save. Both take a file name and then accept or cancel,
    /// but they expose their file-name field differently.
    /// </summary>
    public abstract class NativeFileDialog : NativeDialog
    {
        // The "Address: <folder>" breadcrumb toolbar.
        private const string ADDRESS_BAR_CLASS = @"ToolbarWindow32";
        private const int ADDRESS_BAR_ID = 1001;

        protected const int IDOK = 1;

        // lst1 in dlgs.h: the classic template's file list. The modern dialog is built on that template and keeps
        // this ListBox, hidden, from milliseconds after its window is created until the window is destroyed --
        // unlike the controls that tell Open from Save: the Save dialog carries the classic file-name combo (the
        // Open dialog's mark) for its first ~50 ms, destroys it, and creates its own file-name field ~150 ms later.
        private const int CLASSIC_FILE_LIST_ID = 1120;

        protected NativeFileDialog(IntPtr windowHandle, CancellationToken cancellationToken) : base(windowHandle, cancellationToken)
        {
        }

        /// <summary>Whether the "#32770" is a common file dialog, Open or Save, by the classic file list that
        /// neither a message box nor the Browse-For-Folder dialog has.</summary>
        public static bool IsFileDialog(IntPtr hwnd)
        {
            return HasDescendant(hwnd, NativeControl.LISTBOX_CLASS, CLASSIC_FILE_LIST_ID);
        }

        /// <summary>The Open or Save wrapper for the common file dialog at <paramref name="handle"/>, or null while
        /// it has neither dialog's file-name field: a file dialog the shell is still building or tearing down. Open
        /// is checked first: the Save dialog starts out with the Open dialog's combo and has destroyed it by the
        /// time it has a field of its own.</summary>
        internal static NativeFileDialog Classify(IntPtr handle, CancellationToken cancellationToken)
        {
            if (NativeOpenFileDialog.IsOpenFileDialog(handle))
                return new NativeOpenFileDialog(handle, cancellationToken);
            if (NativeSaveFileDialog.IsSaveFileDialog(handle))
                return new NativeSaveFileDialog(handle, cancellationToken);
            return null;
        }

        /// <summary>The dialog's visible controls plus the address breadcrumb, with the file-name box labeled
        /// "File name": its adjacent "File name:" static would otherwise shadow the caption-less field.</summary>
        public override IEnumerable<UiElement> EnumerateChildren()
        {
            var fileNameEdit = FindFileNameEdit();
            foreach (var child in base.EnumerateChildren())
            {
                // Field-label statics carry no value and would shadow the fields they name.
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
        /// Types the file name(s) into the dialog's file-name field without accepting, and throws if the text did
        /// not register. Must be called off the dialog's own thread.
        ///
        /// <para>To select several files in a multiselect Open dialog, first navigate to their folder (EnterPath
        /// the folder path, accept), then EnterPath their bare names in that folder, double-quoted and
        /// space-separated (<c>"a.raw" "b.raw"</c>). A list of full paths does not work.</para>
        /// </summary>
        public void EnterPath(string path)
        {
            string actual;
            // The shell hides the field while it lays its view out, shortly after the dialog appears and again on
            // every navigation, and nothing observable says when it comes back. No deadline: the wait ends when the
            // field is shown or the client disconnects.
            while (null == (actual = CallFunction(() => TypeFileName(path))))
            {
                CancellationToken.WaitHandle.WaitOne(FIELD_POLL_MILLIS);
                CancellationToken.ThrowIfCancellationRequested();
            }
            // An empty box means the shell consumed the path to navigate; the path itself means a file name is
            // staged. Anything else means the set did not take.
            if (actual.Length == 0 || Equals(actual, path))
                return;
            throw new InvalidOperationException(LlmInstruction.Format(
                @"Tried to set file path to '{0}' but it says '{1}'", path, actual));
        }

        /// <summary>Types the path into the file-name field and returns what the field then holds, or null when
        /// the field is not shown. Runs on the dialog's own thread, which lays the field out, so the shell cannot
        /// hide the field between finding and setting it, nor rewrite the text between setting and reading it.</summary>
        private string TypeFileName(string path)
        {
            var hwnd = FindShownFileNameEdit();
            if (hwnd == IntPtr.Zero)
                return null;
            var textBox = new NativeTextBox(hwnd, CancellationToken);
            textBox.SetText(path);
            return textBox.GetValueNow() as string ?? string.Empty;
        }

        /// <summary>Clicks the commit button, found by control id rather than localized caption, on the dialog's
        /// thread; a modal the click raises comes back named. Must be called off the dialog's own thread.
        ///
        /// <para>Does not wait for the dialog to close, because it may not: a folder path navigates and leaves it
        /// open, and on the multiselect Open dialog the click can be spent closing the combo's autocomplete
        /// drop-down, so a caller checks whether the dialog closed and clicks again if not. Use
        /// <see cref="NativeDialog.DismissWithAcceptButton"/> when it must close.</para></summary>
        public ActionResult Accept()
        {
            return PerformAction(() => UiActions.Click.InvokeNow(AcceptButton, null));
        }

        protected NativeButton AcceptButton => RequireButton(IDOK, CommitButtonDescription);

        /// <summary>What the commit button is called, for the message when it cannot be found.</summary>
        protected abstract string CommitButtonDescription { get; }

        /// <summary>The control id of this dialog's file-name Edit: 1148 for the Open dialog's classic combo,
        /// 1001 for the Save dialog's DirectUI-hosted field.</summary>
        protected abstract int FileNameControlId { get; }

        /// <summary>The commit button rather than the file-name field, which the shell hides again each time it
        /// lays the dialog's view out.</summary>
        protected override bool IsOpenComplete =>
            User32.IsWindowVisible(FindDescendant(NativeControl.BUTTON_CLASS, IDOK));

        private const int FIELD_POLL_MILLIS = 30;

        private IntPtr FindShownFileNameEdit()
        {
            var hwnd = FindFileNameEdit();
            return hwnd != IntPtr.Zero && User32.IsWindowVisible(hwnd) ? hwnd : IntPtr.Zero;
        }

        /// <summary>The file-name Edit, or IntPtr.Zero until it exists. Virtual because the multiselect Open
        /// dialog does not put <see cref="FileNameControlId"/> on the Edit itself.</summary>
        protected virtual IntPtr FindFileNameEdit() => FindDescendant(NativeControl.EDIT_CLASS, FileNameControlId);

        // The controlId is ignored: a file dialog has the one field to set.
        protected override void SetValueCore(string value) => EnterPath(value);
    }
}
