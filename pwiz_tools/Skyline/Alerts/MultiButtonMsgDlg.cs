﻿/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public sealed class MultiButtonMsgDlg : AlertDlg
    {
        public static string BUTTON_OK { get { return GetDefaultButtonText(DialogResult.OK); } }
        public static string BUTTON_YES { get { return GetDefaultButtonText(DialogResult.Yes); } }
        public static string BUTTON_NO { get { return GetDefaultButtonText(DialogResult.No); } }

        public static DialogResult Show(IWin32Window parent, string message, string btnText)
        {
            return new MultiButtonMsgDlg(message, btnText).ShowAndDispose(parent);
        }

        public static DialogResult Show(IWin32Window parent, string message, string btnYesText, string btnNoText, bool allowCancel)
        {
            return new MultiButtonMsgDlg(message, btnYesText, btnNoText, allowCancel)
                .ShowAndDispose(parent);
        }

        /// <summary>
        /// Show a message box with a Cancel button and one other button.
        /// </summary>
        /// <param name="message">The message to show</param>
        /// <param name="btnText">The text to show in the non-Cancel button (DialogResult.OK)</param>
        public MultiButtonMsgDlg(string message, string btnText) : base(message)
        {
            AddButton(DialogResult.Cancel);
            AddButton(DialogResult.OK, btnText);

            if (ModeUI != SrmDocument.DOCUMENT_TYPE.proteomic)
            {
                // Force replacement of "peptide" etc with "molecule" etc in all controls on open
                Helpers.PeptideToMoleculeTextMapper.TranslateForm(this, ModeUI);
            }
        }

        /// <summary>
        /// Show a message box with a Cancel button and two other buttons.
        /// </summary>
        /// <param name="message">The message to show</param>
        /// <param name="btnYesText">The text to show in the left-most, default button (DialogResult.Yes)</param>
        /// <param name="btnNoText">The text to show in the second, non-default button (DialogResult.No)</param>
        /// <param name="allowCancel">When this is true a Cancel button is the button furthest to the
        /// right. Otherwise, only the two named buttons are visible.</param>
        public MultiButtonMsgDlg(string message, string btnYesText, string btnNoText, bool allowCancel)
            : base(message)
        {
            if (allowCancel)
            {
                AddButton(DialogResult.Cancel);
            }
            AddButton(DialogResult.No, btnNoText);
            AddButton(DialogResult.Yes, btnYesText);

            if (ModeUI != SrmDocument.DOCUMENT_TYPE.proteomic)
            {
                // Force replacement of "peptide" etc with "molecule" etc in all controls on open
                Helpers.PeptideToMoleculeTextMapper.TranslateForm(this, ModeUI);
            }
        }

        /// <summary>
        /// A dialog box with a custom control, a Cancel button and one other button.
        /// </summary>
        /// <param name="ctl">The control to show</param>
        /// <param name="btnText">The text to show in the non-Cancel button (DialogResult.OK)</param>
        public MultiButtonMsgDlg(Control ctl, string btnText) : this(string.Empty, btnText)
        {
            messageScrollPanel.Hide();
            splitContainer.Panel1.Controls.Add(ctl);
        }

        /// <summary>
        /// A dialog box with a custom control, a Cancel button and two other buttons.
        /// </summary>
        /// <param name="ctl">The control to show</param>
        /// <param name="btnYesText">The text to show in the left-most, default button (DialogResult.Yes)</param>
        /// <param name="btnNoText">The text to show in the second, non-default button (DialogResult.No)</param>
        /// <param name="allowCancel">When this is true a Cancel button is the button furthest to the
        /// right. Otherwise, only the two named buttons are visible.</param>
        public MultiButtonMsgDlg(Control ctl, string btnYesText, string btnNoText, bool allowCancel)
        : this(string.Empty, btnYesText, btnNoText, allowCancel)
        {
            messageScrollPanel.Hide();
            splitContainer.Panel1.Controls.Add(ctl);
        }

        /// <summary>
        /// Click the left-most button (actually the third-to-last button).
        /// </summary>
        public void Btn0Click()
        {
            var buttons = DialogResultButtons.ToArray();
            ClickButton(buttons[buttons.Length - 3]);
        }

        /// <summary>
        /// Click the middle button
        /// </summary>
        public void Btn1Click()
        {
            var buttons = DialogResultButtons.ToArray();
            ClickButton(buttons[buttons.Length - 2]);
        }

        /// <summary>
        /// Click the left-most visible button
        /// </summary>
        public void BtnYesClick()
        {
            ClickButton(DialogResultButtons.First());
        }

        /// <summary>
        /// Click the right-most button
        /// </summary>
        public void BtnCancelClick()
        {
            ClickButton(DialogResultButtons.Last());
        }

        /// <summary>
        /// Returns all the buttons on the button bar except the "More Info" button.
        /// </summary>
        private IEnumerable<Button> DialogResultButtons
        {
            get { return VisibleButtons.Where(button => DialogResult.None != button.DialogResult); }
        }
    }
}
