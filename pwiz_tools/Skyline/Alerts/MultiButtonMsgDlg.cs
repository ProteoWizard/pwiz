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
        }

        /// <summary>
        /// A dialog box with a custom control, a Cancel button and one other button.
        /// </summary>
        /// <param name="ctl">The control to show</param>
        /// <param name="btnText">The text to show in the non-Cancel button (DialogResult.OK)</param>
        /// <param name="ctlContentAsText">A text representation of the control's contents.</param>
        public MultiButtonMsgDlg(Control ctl, string btnText, string ctlContentAsText) : this(ctlContentAsText, btnText)
        {
            messageScrollPanel.Hide();
            splitContainer.Panel1.Controls.Add(ctl);
        }

        /// <summary>
        /// Show a message box with a set of standard buttons, and an optional custom title.
        /// </summary>
        public static DialogResult Show(IWin32Window parent, string message, MessageBoxButtons buttons)
        {
            return new MultiButtonMsgDlg(message, buttons, DialogResult.None)
                .ShowAndDispose(parent);
        }

        /// <summary>
        /// Show a message box with a set of standard buttons and a specified AcceptButton, and an optional custom title.
        /// </summary>
        public static DialogResult Show(IWin32Window parent, string message, MessageBoxButtons buttons, DialogResult defaultDialogResult)
        {
            return new MultiButtonMsgDlg(message, buttons, defaultDialogResult)
                .ShowAndDispose(parent);
        }

        /// <summary>
        /// Construct a message box with a set of standard buttons.
        /// </summary>
        /// <param name="message">The message to show</param>
        /// <param name="buttons">The set of buttons to show, as also used in MessageBox.</param>
        /// <param name="defaultDialogResult">The button to be assumed if user hits Enter in a different control in the window.</param>
        public MultiButtonMsgDlg(string message, MessageBoxButtons buttons, DialogResult defaultDialogResult)
            : base(message, buttons, defaultDialogResult)
        {
        }


        /// <summary>
        /// A dialog box with a custom control, a Cancel button and two other buttons.
        /// </summary>
        /// <param name="ctl">The control to show</param>
        /// <param name="btnYesText">The text to show in the left-most, default button (DialogResult.Yes)</param>
        /// <param name="btnNoText">The text to show in the second, non-default button (DialogResult.No)</param>
        /// <param name="allowCancel">When this is true a Cancel button is the button furthest to the
        /// right. Otherwise, only the two named buttons are visible.</param>
        /// <param name="ctlContentAsText">A text representation of the control's contents.</param>
        public MultiButtonMsgDlg(Control ctl, string btnYesText, string btnNoText, bool allowCancel, string ctlContentAsText)
        : this(ctlContentAsText, btnYesText, btnNoText, allowCancel)
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
