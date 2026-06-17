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
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Accessibility;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Drives the native Windows common Open/Save file dialog (such as the OpenFileDialog shown
    /// by <c>SkylineWindow.ShowOpenFileDialog</c>) using UI Automation. See
    /// <see cref="NativeDialogAutomation"/> for the threading contract and how to obtain an
    /// instance.
    /// </summary>
    public class OpenFileDialogAutomation : FileDialogAutomation
    {
        // Identifier assigned by the Windows common file dialog to its "File name" combo box
        // (cmb13). Unlike the localized control captions, it is stable across Windows versions
        // and locales, and its presence distinguishes a file dialog from other "#32770" dialogs.
        private const string FILE_NAME_COMBO_ID = @"1148";

        public OpenFileDialogAutomation(IntPtr windowHandle) : base(windowHandle)
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
        /// <see cref="Accept"/> to open. Pass several space-quoted paths (<c>"a" "b"</c>, see
        /// <see cref="QuotePaths"/>) to select multiple files in a multiselect dialog.
        /// </summary>
        public override void EnterPath(string path)
        {
            SetEditValue(GetFileNameEdit(), path);
        }

        /// <summary>Accepts the dialog (Enter in the file name box), opening the typed file(s).</summary>
        public override void Accept()
        {
            PressEnter(GetFileNameEdit());
        }

        /// <summary>
        /// Builds the file name box value that selects several files at once: each path double-quoted
        /// and space-separated, the convention the common dialog parses for a multiselect open.
        /// </summary>
        public static string QuotePaths(IEnumerable<string> paths)
        {
            return string.Join(@" ", paths.Select(p => @"""" + p + @""""));
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

        // SPIKE (TODO-20260609_native_file_dialog_automation): does the screen-reader path work?
        // Instead of posting an Enter keystroke, type the path and then "press" the dialog's default
        // button (Open) the way a screen reader does on this DirectUI surface -- through MSAA
        // (IAccessible.accDoDefaultAction). UI Automation's managed wrapper does not expose the
        // LegacyIAccessible pattern, and InvokePattern.Invoke silently no-ops on this dialog, so we
        // go straight to the IAccessible (oleacc) layer that NVDA/JAWS use for legacy surfaces.
        public void EnterPathAndAcceptViaButton(string path)
        {
            BringToForeground();
            var fileNameComboBox = WaitForElement(FILE_NAME_COMBO_ID);
            var edit = fileNameComboBox.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            if (edit == null)
                throw new InvalidOperationException(@"Could not find the file name edit control in the file dialog.");
            var valuePattern = (ValuePattern)edit.GetCurrentPattern(ValuePattern.Pattern);
            valuePattern.SetValue(path);

            InvokeDefaultButtonViaMsaa();
        }

        // MSAA constants (oleacc).
        private const uint OBJID_CLIENT = 0xFFFFFFFC;
        private const int CHILDID_SELF = 0;
        private const int ROLE_SYSTEM_PUSHBUTTON = 0x2B;
        private const int STATE_SYSTEM_DEFAULT = 0x100;
        private const int MAX_MSAA_DEPTH = 12;

        // Finds the default push button in the dialog's accessible tree and performs its default
        // action. On failure, throws a diagnostic listing the push buttons MSAA could see.
        private void InvokeDefaultButtonViaMsaa()
        {
            var iidAccessible = new Guid(@"618736E0-3C3D-11CF-810C-00AA00389B71");
            int hr = AccessibleObjectFromWindow(WindowHandle, OBJID_CLIENT, ref iidAccessible, out var rootObj);
            if (hr != 0 || !(rootObj is IAccessible root))
                throw new InvalidOperationException(
                    string.Format(@"AccessibleObjectFromWindow failed (hr=0x{0:X8}).", hr));

            var pushButtons = new List<string>();
            if (TryInvokeDefaultPushButton(root, 0, pushButtons))
                return;
            throw new InvalidOperationException(
                string.Format(@"No default push button found via MSAA. Push buttons seen: {0}",
                    string.Join(@"; ", pushButtons)));
        }

        private static bool TryInvokeDefaultPushButton(IAccessible container, int depth, List<string> pushButtons)
        {
            if (depth > MAX_MSAA_DEPTH)
                return false;
            int count;
            try
            {
                count = container.accChildCount;
            }
            catch (Exception)
            {
                return false;
            }
            if (count <= 0)
                return false;
            var children = new object[count];
            if (AccessibleChildren(container, 0, count, children, out var obtained) != 0)
                return false;
            for (int i = 0; i < obtained; i++)
            {
                var asAccessible = children[i] as IAccessible;
                // A child is either a full IAccessible (query it with CHILDID_SELF) or a simple
                // element identified by an integer id that is queried on its container.
                var queryTarget = asAccessible ?? container;
                object childId = asAccessible != null ? CHILDID_SELF : children[i];
                if (IsDefaultPushButton(queryTarget, childId, pushButtons))
                {
                    queryTarget.accDoDefaultAction(childId);
                    return true;
                }
                if (asAccessible != null && TryInvokeDefaultPushButton(asAccessible, depth + 1, pushButtons))
                    return true;
            }
            return false;
        }

        private static bool IsDefaultPushButton(IAccessible acc, object childId, List<string> pushButtons)
        {
            try
            {
                if (Convert.ToInt32(acc.get_accRole(childId)) != ROLE_SYSTEM_PUSHBUTTON)
                    return false;
                int state = Convert.ToInt32(acc.get_accState(childId));
                bool isDefault = (state & STATE_SYSTEM_DEFAULT) != 0;
                pushButtons.Add(string.Format(@"'{0}' default={1}", acc.get_accName(childId), isDefault));
                return isDefault;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [DllImport(@"oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint id, ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out object ppvObject);

        [DllImport(@"oleacc.dll")]
        private static extern int AccessibleChildren(IAccessible paccContainer, int iChildStart, int cChildren,
            [Out] object[] rgvarChildren, out int pcObtained);
    }
}
