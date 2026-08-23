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

        // The commit button's control id, so it is found without matching a localized caption.
        protected const int IDOK = 1;

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
                    ? new NativeTextBox(fileNameEdit, CancellationToken, FILE_NAME_FIELD)
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
        ///
        /// <para>Must be called OFF the dialog's own thread: the typing is posted to that thread and waited for.</para>
        /// </summary>
        public void EnterPath(string path)
        {
            string actual;
            // The field is not always shown: the Save dialog's rides the DirectUI surface, which the shell hides
            // while it lays its view out -- shortly after the dialog appears, and again on every navigation. Ask
            // again until it is, because nothing the caller can observe says when it comes back. No deadline: the
            // wait ends when the field is shown or when the client that asked for it disconnects.
            while (null == (actual = CallFunction(() => TypeFileName(path))))
            {
                CancellationToken.WaitHandle.WaitOne(FIELD_POLL_MILLIS);
                CancellationToken.ThrowIfCancellationRequested();
            }
            // An EMPTY box means the shell consumed the path to navigate -- the caller confirms that through the
            // "Address" control; the path itself means a file name is staged to open. Anything else means the set
            // did not take.
            if (actual.Length == 0 || Equals(actual, path))
                return;
            throw new InvalidOperationException(LlmInstruction.Format(
                @"Tried to set file path to '{0}' but it says '{1}'", path, actual));
        }

        /// <summary>Types the path into the file-name field and returns what the field then HOLDS, or null when the
        /// field is not shown and the caller should ask again. Runs on the dialog's OWN thread (see
        /// <see cref="EnterPath"/>), which is what makes the three steps one step: the shell lays the field out on
        /// that thread, so it cannot hide the field between finding it and setting it, nor rewrite the text between
        /// setting it and reading it back.</summary>
        private string TypeFileName(string path)
        {
            var hwnd = FindShownFileNameEdit();
            if (hwnd == IntPtr.Zero)
                return null;
            var textBox = new NativeTextBox(hwnd, CancellationToken);
            textBox.SetText(path);
            // What the box HOLDS, rather than what we predict the shell will make of the path.
            return textBox.GetValueNow() as string ?? string.Empty;
        }

        /// <summary>Clicks the commit button, which is <see cref="StandaloneWindow.ClickButton"/> by control id
        /// rather than by the button's localized caption -- the one thing a test cannot key on. Same marshal: ONE
        /// trip onto the dialog's thread, and a modal the click raises comes back named rather than pinning this
        /// thread. Must be called off the dialog's own thread.
        ///
        /// <para>Does not wait for the dialog to close, because it may not: a FOLDER path navigates and leaves it
        /// open (read the "AddressBar" control to tell), and on the multiselect Open dialog the click can be spent
        /// closing the combo's autocomplete drop-down instead of committing -- so a caller checks whether the dialog
        /// closed and clicks again if it did not. Use <see cref="NativeDialog.DismissWithAcceptButton"/> when it
        /// must close.</para></summary>
        public ActionResult Accept()
        {
            return PerformAction(() => UiActions.Click.InvokeNow(AcceptButton, null));
        }

        protected NativeButton AcceptButton => RequireButton(IDOK, CommitButtonDescription);

        /// <summary>What the commit button is called, for the message when it cannot be found.</summary>
        protected abstract string CommitButtonDescription { get; }

        /// <summary>The label the file-name box is presented under, so a caller can read/set it by name (see
        /// <see cref="EnumerateChildren"/>). Ours, not the shell's, so it is the same in every UI language. It names
        /// the box only while the shell is SHOWING it, since GetControls lists the shown children.</summary>
        public const string FILE_NAME_FIELD = @"File name";

        /// <summary>The control id of this dialog's file-name Edit -- 1148 for the Open dialog's classic combo,
        /// 1001 for the Save dialog's DirectUI-hosted field.</summary>
        protected abstract int FileNameControlId { get; }

        /// <summary>The commit button rather than the file-name field, which the shell hides again each time it
        /// lays the dialog's view out (see <see cref="EnterPath"/>).</summary>
        protected override bool IsOpenComplete =>
            User32.IsWindowVisible(FindDescendant(NativeControl.BUTTON_CLASS, IDOK));

        // How long to wait between asking for the field again.
        private const int FIELD_POLL_MILLIS = 30;

        // The file-name Edit once the shell has shown it, else IntPtr.Zero.
        private IntPtr FindShownFileNameEdit()
        {
            var hwnd = FindFileNameEdit();
            return hwnd != IntPtr.Zero && User32.IsWindowVisible(hwnd) ? hwnd : IntPtr.Zero;
        }

        /// <summary>The window handle of the file-name Edit, or IntPtr.Zero until it exists. By default the Edit
        /// itself carries <see cref="FileNameControlId"/> (the Save dialog's Edit, the plain Open dialog's combo
        /// Edit); the Open dialog overrides this because its multiselect flavour does not put the id on the Edit.</summary>
        protected virtual IntPtr FindFileNameEdit() => FindDescendant(NativeControl.EDIT_CLASS, FileNameControlId);

        // set_value types the path: its controlId is ignored, because a file dialog has the one field to set.
        protected override void SetValueCore(string value) => EnterPath(value);
    }
}
