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
using System.Windows.Automation;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Drives a native Windows message box (a "#32770" dialog with push buttons and a message, such as
    /// the "Confirm Save As / replace it?" box the common Save dialog raises when the file already
    /// exists). Skyline normally shows its alerts as managed forms, but a few come from Windows itself
    /// and so are not WinForms forms; without this the connector would see the box (it is enumerated as
    /// a native form) but have no way past it. Unlike a file dialog, a message box's choices ARE named
    /// buttons (Yes / No / OK / Cancel / ...), so it exposes them and clicks one by its caption. See
    /// <see cref="NativeDialog"/> for the threading contract and how to obtain an instance.
    /// </summary>
    public class NativeMessageBox : NativeDialog
    {
        public NativeMessageBox(IntPtr windowHandle) : base(windowHandle)
        {
        }

        public override string DialogTypeName => @"MessageBox";

        /// <summary>
        /// Returns true if the given native dialog is a message box -- a "#32770" with push buttons.
        /// Checked after the file dialogs (which have their own file-name field), so any remaining
        /// "#32770" that has a button is treated as a message box.
        /// </summary>
        public static bool IsMessageBox(AutomationElement dialog)
        {
            try
            {
                return dialog.FindFirst(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)) != null;
            }
            catch (ElementNotAvailableException)
            {
                return false;
            }
        }

        // The message box's text and its push buttons, so a caller can read the prompt and see which
        // choices it offers. The buttons are addressed by their caption with the accept/close actions or
        // ClickFormButton; the text rows are informational.
        public override ControlInfo[] GetControlInfos()
        {
            var result = new List<ControlInfo>();
            foreach (var text in FindChildren(ControlType.Text))
            {
                var content = text.Current.Name;
                if (!string.IsNullOrEmpty(content))
                    result.Add(new ControlInfo { Path = new UiElementPath(null, content, null, @"Text") });
            }
            foreach (var button in FindChildren(ControlType.Button))
                result.Add(new ControlInfo
                {
                    Path = new UiElementPath(null, button.Current.Name, null, @"Button"),
                    Enabled = button.Current.IsEnabled,
                });
            return result.ToArray();
        }

        // Clicks the button whose caption matches -- the way the MCP gets past a confirm box (e.g. "Yes").
        // Posts BM_CLICK (does not send it): the click may dismiss the box and unwind a nested modal loop,
        // so a synchronous send could wedge the caller (see NativeSaveFileDialog.Accept).
        public override void ClickButton(string button)
        {
            var buttons = FindChildren(ControlType.Button).ToList();
            var match = buttons.FirstOrDefault(b => CaptionMatches(b.Current.Name, button));
            if (match == null)
                throw new ArgumentException(LlmInstruction.Format(
                    @"The message box '{0}' has no button '{1}'. Its buttons are: {2}.",
                    FormId, button, string.Join(@", ", buttons.Select(b => b.Current.Name))));
            User32.PostMessageA(new IntPtr(match.Current.NativeWindowHandle), User32.WinMessageType.BM_CLICK, 0, 0);
        }

        // Accepts the box by activating its default button (the one Enter presses).
        public override void Accept()
        {
            PressEnter(DialogElement);
        }

        private IEnumerable<AutomationElement> FindChildren(ControlType controlType)
        {
            try
            {
                return DialogElement.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, controlType)).Cast<AutomationElement>();
            }
            catch (ElementNotAvailableException)
            {
                return Enumerable.Empty<AutomationElement>();
            }
        }

        // A button caption matches the requested text ignoring case and the '&' mnemonic marker.
        private static bool CaptionMatches(string actual, string requested)
        {
            return string.Equals(StripMnemonic(actual), StripMnemonic(requested), StringComparison.OrdinalIgnoreCase);
        }

        private static string StripMnemonic(string text)
        {
            return (text ?? string.Empty).Replace(@"&", string.Empty).Trim();
        }
    }
}
