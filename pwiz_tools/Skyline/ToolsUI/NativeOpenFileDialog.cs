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
using System.Windows.Automation;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Drives the native Windows common Open/Save file dialog (such as the OpenFileDialog shown
    /// by <c>SkylineWindow.ShowOpenFileDialog</c>) using UI Automation. See
    /// <see cref="NativeDialog"/> for the threading contract and how to obtain an
    /// instance.
    /// </summary>
    public class NativeOpenFileDialog : NativeFileDialog
    {
        // Identifier assigned by the Windows common file dialog to its "File name" combo box
        // (cmb13). Unlike the localized control captions, it is stable across Windows versions
        // and locales, and its presence distinguishes a file dialog from other "#32770" dialogs.
        private const string FILE_NAME_COMBO_ID = @"1148";

        public NativeOpenFileDialog(IntPtr windowHandle) : base(windowHandle)
        {
        }

        /// <summary>
        /// Returns true if the given native dialog element is a common Open/Save file dialog,
        /// identified by its "File name" combo box.
        /// </summary>
        public static bool IsOpenFileDialog(AutomationElement dialog)
        {
            try
            {
                return dialog.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, FILE_NAME_COMBO_ID)) != null;
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
        }

        /// <summary>
        /// Opens the file at the given path. The full path is typed into the file name box and
        /// then Enter is pressed, the same way a user can paste a full path and press Enter to
        /// navigate to the folder and open the file in one action -- so this does not depend on
        /// whatever folder the dialog happened to open in. The path is set on the Edit control
        /// inside the "File name" combo box (setting it on the combo box would trigger
        /// auto-complete that discards the directory portion).
        /// </summary>
        public void EnterPathAndAccept(string path)
        {
            var edit = GetFileNameEdit();
            SetEditValue(edit, path);
            PressEnter(edit);
        }

        /// <summary>
        /// Types a file name into the dialog's file name box without accepting; call
        /// <see cref="Accept"/> to open. Pass several double-quoted, space-separated paths
        /// (<c>"a" "b"</c>) to select multiple files in a multiselect dialog.
        /// </summary>
        public override void EnterPath(string path)
        {
            SetEditValue(GetFileNameEdit(), path);
        }

        /// <summary>Posts Enter in the file name box (the base Accept waits for the dialog to close).</summary>
        public override void PostAccept()
        {
            PressEnter(GetFileNameEdit());
        }

        private AutomationElement GetFileNameEdit()
        {
            BringToForeground();
            var fileNameComboBox = WaitForElement(FILE_NAME_COMBO_ID);
            var edit = fileNameComboBox.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            if (edit == null)
                throw new InvalidOperationException(@"Could not find the file name edit control in the file dialog.");
            return edit;
        }

        private static void SetEditValue(AutomationElement edit, string value)
        {
            var valuePattern = (ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern);
            valuePattern.SetValue(value);
        }
    }
}
