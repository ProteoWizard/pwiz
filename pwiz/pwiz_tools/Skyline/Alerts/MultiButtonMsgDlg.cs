/*
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

using System;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public sealed partial class MultiButtonMsgDlg : FormEx
    {
        public static string BUTTON_OK { get { return Resources.MultiButtonMsgDlg_BUTTON_OK_OK; } }
        public static string BUTTON_YES { get { return Resources.MultiButtonMsgDlg_BUTTON_YES__Yes; } }
        public static string BUTTON_NO { get { return Resources.MultiButtonMsgDlg_BUTTON_NO__No; } }

        private const int MAX_HEIGHT = 500;

        public static DialogResult Show(IWin32Window parent, string message, string btnText, object tag = null)
        {
            using (var dlg = new MultiButtonMsgDlg(message, btnText) {Tag = tag})
            {
                return dlg.ShowWithTimeout(parent, message);
            }
        }

        public static DialogResult Show(IWin32Window parent, string message, string btnYesText, string btnNoText, bool allowCancel, object tag = null)
        {
            using (var dlg = new MultiButtonMsgDlg(message, btnYesText, btnNoText, allowCancel) {Tag = tag})
            {
                return dlg.ShowWithTimeout(parent, message);
            }
        }

        /// <summary>
        /// Show a message box with a Cancel button and one other button.
        /// </summary>
        /// <param name="message">The message to show</param>
        /// <param name="btnText">The text to show in the non-Cancel button (DialogResult.OK)</param>
        private MultiButtonMsgDlg(string message, string btnText)
            : this(message, null, btnText, true)
        {
        }

        /// <summary>
        /// Show a message box with a Cancel button and two other buttons.
        /// </summary>
        /// <param name="message">The message to show</param>
        /// <param name="btnYesText">The text to show in the left-most, default button (DialogResult.Yes)</param>
        /// <param name="btnNoText">The text to show in the second, non-default button (DialogResult.No)</param>
        /// <param name="allowCancel">When this is true a Cancel button is the button furthest to the
        /// right. Otherwise, only the two named buttons are visible.</param>
        private MultiButtonMsgDlg(string message, string btnYesText, string btnNoText, bool allowCancel)
        {
            InitializeComponent();

            Text = Program.Name;
            if (allowCancel)
                btn1.Text = btnNoText;
            else
            {
                btn1.Text = btnYesText;
                btnCancel.Text = btnNoText;
            }

            if (allowCancel && btnYesText != null)
                btn0.Text = btnYesText;
            else
            {
                btn0.Visible = false;
                AcceptButton = btn1;
                if (allowCancel)
                    btn1.DialogResult = DialogResult.OK;
                else
                {
                    btn1.DialogResult = DialogResult.Yes;
                    btnCancel.DialogResult = DialogResult.No;
                    CancelButton = null;
                }
            }
            int height = labelMessage.Height;
            labelMessage.Text = message;
            Height += Math.Min(MAX_HEIGHT, Math.Max(0, labelMessage.Height - height * 3));
        }

        /// <summary>
        /// Click the left-most button
        /// </summary>
        public void Btn0Click()
        {
            // Tests shouldn't call this function when the button is not visible
            if (!btn0.Visible)
                throw new NotSupportedException();
            CheckDisposed();
            btn0.PerformClick();
        }

        /// <summary>
        /// Click the middle button
        /// </summary>
        public void Btn1Click()
        {
            CheckDisposed();
            btn1.PerformClick();
        }

        /// <summary>
        /// Click the left-most visible button
        /// </summary>
        public void BtnYesClick()
        {
            if (btn0.Visible)
                Btn0Click();
            else
                Btn1Click();
        }

        /// <summary>
        /// Click the right-most button
        /// </summary>
        public void BtnCancelClick()
        {
            CheckDisposed();
            btnCancel.PerformClick();
        }

        public string Message
        {
            get { return labelMessage.Text; }
            set { labelMessage.Text = value;}
        }

        private void MultiButtonMsgDlg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C && e.Control)
            {
                CopyMessage();
            }
        }

        private void CopyMessage()
        {
            const string formatMessage =
@"---------------------------
{0}
---------------------------
{1}
---------------------------
{2}
---------------------------
"; // Not L10N
            var sbButtons = new StringBuilder();
            if (btn0.Visible)
                sbButtons.Append(btn0.Text).Append("    "); // Not L10N
            sbButtons.Append(btn1.Text).Append("    "); // Not L10N
            sbButtons.Append(btnCancel.Text);
            ClipboardEx.SetText(string.Format(formatMessage, Text, Message, sbButtons));
        }
    }
}
