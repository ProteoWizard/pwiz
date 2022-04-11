/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
    /// </summary>
    public partial class AlertDlg : FormEx
    {
        private const int MAX_HEIGHT = 500;
        private readonly int _originalFormHeight;
        private readonly int _originalMessageHeight;
        private readonly int _labelPadding;
        private string _message;
        private string _detailMessage;

        public AlertDlg() : this(@"Alert dialog for Forms designer")
        {
        }

        public AlertDlg(string message)
        {
            InitializeComponent();
            _originalFormHeight = Height;
            _originalMessageHeight = labelMessage.Height;
            _labelPadding = messageScrollPanel.Width - labelMessage.MaximumSize.Width;
            Message = message;
            btnMoreInfo.Parent.Controls.Remove(btnMoreInfo);
            Text = Program.Name;
            toolStrip1.Renderer = new NoBorderSystemRenderer();
        }

        public AlertDlg(string message, MessageBoxButtons messageBoxButtons) : this(message, messageBoxButtons, DialogResult.None)
        {
        }

        public AlertDlg(string message, MessageBoxButtons messageBoxButtons, DialogResult defaultButton) : this(message)
        {
            AddMessageBoxButtons(messageBoxButtons, defaultButton);
        }

        public string Message
        {
            get { return _message; }
            set
            {
                _message = value;
                labelMessage.Text = TruncateMessage(_message);
                int formGrowth = Math.Max(labelMessage.Height - _originalMessageHeight * 3, 0);
                formGrowth = Math.Max(formGrowth, 0);
                formGrowth = Math.Min(formGrowth, MAX_HEIGHT);
                Height = _originalFormHeight + formGrowth;
            }
        }

        public string DetailMessage 
        {
            get
            {
                return _detailMessage;
            }
            set
            {
                _detailMessage = value;
                tbxDetail.Text = TruncateMessage(_detailMessage);
                if (string.IsNullOrEmpty(DetailMessage))
                {
                    if (btnMoreInfo.Parent != null)
                    {
                        btnMoreInfo.Parent.Controls.Remove(btnMoreInfo);
                    }
                }
                else
                {
                    if (null == btnMoreInfo.Parent)
                    {
                        buttonPanel.Controls.Add(btnMoreInfo);
                        buttonPanel.Controls.SetChildIndex(btnMoreInfo, 0);
                    }
                }
            }
        }

        public Exception Exception
        {
            set
            {
                if (null == value)
                {
                    DetailMessage = null;
                }
                else
                {
                    DetailMessage = value.ToString();
                }
            }
        }

        public DialogResult ShowAndDispose(IWin32Window parent)
        {
            using (this)
            {
                return ShowWithTimeout(parent, GetTitleAndMessageDetail());
            }
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private void MessageDlg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.C && e.Control)
            {
                CopyMessage();
            }
        }

        public void CopyMessage()
        {
            ClipboardHelper.SetSystemClipboardText(this, GetTitleAndMessageDetail());
        }

        public override string DetailedMessage
        {
            get { return GetTitleAndMessageDetail();  }
        }

        private string GetTitleAndMessageDetail()
        {
            const string separator = "---------------------------";
            List<string> lines = new List<String>();
            lines.Add(separator);
            lines.Add(Text);
            lines.Add(separator);
            lines.Add(Message);
            lines.Add(separator);
            lines.Add(TextUtil.SpaceSeparate(VisibleButtons.Select(btn => btn.Text)));
            if (null != DetailMessage)
            {
                lines.Add(separator);
                lines.Add(DetailMessage);
            }
            lines.Add(separator);
            lines.Add(string.Empty);
            return TextUtil.LineSeparate(lines);
        }

        private void btnMoreInfo_Click(object sender, EventArgs e)
        {
            if (splitContainer.Panel2Collapsed)
            {
                int panel1Height = splitContainer.Panel1.Height;
                Height += 100;
                splitContainer.Panel2Collapsed = false;
                splitContainer.SplitterDistance = panel1Height;
            }
        }

        private void AddMessageBoxButtons(MessageBoxButtons messageBoxButtons, DialogResult defaultDialogResult)
        {
            var buttons = new Dictionary<DialogResult, Button>();
            foreach (var dialogResult in GetDialogResults(messageBoxButtons).Reverse())
            {
                buttons.Add(dialogResult, AddButton(dialogResult));
            }

            // Optionally define the button action when user hits Enter in a text edit etc.
            // Default is the one most recently created by AddButton()
            if (buttons.TryGetValue(defaultDialogResult, out var acceptButton))
            {
                AcceptButton = acceptButton;
            }
        }

        /// <summary>
        /// Returns the buttons on the button bar from LEFT to RIGHT.
        /// </summary>
        public IEnumerable<Button> VisibleButtons
        {
            get
            {
                // return the buttons in reverse order because buttonPanel is a right-to-left FlowPanel.
                return buttonPanel.Controls.OfType<Button>().Reverse();
            }
        }

        public void ClickButton(DialogResult dialogResult)
        {
            ClickButton(FindButton(dialogResult));
        }

        public Button AddButton(DialogResult dialogResult)
        {
            return AddButton(dialogResult, GetDefaultButtonText(dialogResult));
        }

        /// <summary>
        /// Adds a button to the button bar, to the LEFT of any buttons which are already on the button bar.
        /// </summary>
        public Button AddButton(DialogResult dialogResult, string text)
        {
            var button = new Button
            {
                Text = text,
                DialogResult = dialogResult,
                Margin = btnMoreInfo.Margin,
                Height = btnMoreInfo.Height,
            };
            buttonPanel.Controls.Add(button);
            var visibleButtons = VisibleButtons.ToArray();
            if (visibleButtons.Length == 1)
            {
                CancelButton = VisibleButtons.First();
            }
            else
            {
                CancelButton = FindButton(DialogResult.Cancel);
            }
            AcceptButton = visibleButtons.First();
            int tabIndex = 0;
            foreach (Button btn in visibleButtons)
            {
                btn.TabIndex = tabIndex++;
            }
            return button;
        }

        public Button FindButton(DialogResult buttonDialogResult)
        {
            return VisibleButtons.FirstOrDefault(button => button.DialogResult == buttonDialogResult);
        }

        public void ClickButton(Button button)
        {
            if (!button.Visible)
            {
                throw new NotSupportedException();
            }
            CheckDisposed();
            button.PerformClick();
        }

        public void ClickOk()
        {
            ClickButton(DialogResult.OK);
        }

        public void ClickCancel()
        {
            ClickButton(DialogResult.Cancel);
        }

        public void ClickYes()
        {
            ClickButton(DialogResult.Yes);
        }

        public void ClickNo()
        {
            ClickButton(DialogResult.No);
        }

        private static DialogResult[] GetDialogResults(MessageBoxButtons messageBoxButtons)
        {
            switch (messageBoxButtons)
            {
                case MessageBoxButtons.OK:
                    return new[] {DialogResult.OK};
                case MessageBoxButtons.OKCancel:
                    return new[] {DialogResult.OK, DialogResult.Cancel};
                case MessageBoxButtons.AbortRetryIgnore:
                    return new[] {DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore};
                case MessageBoxButtons.RetryCancel:
                    return new[] {DialogResult.Retry, DialogResult.Cancel};
                case MessageBoxButtons.YesNo:
                    return new[] {DialogResult.Yes, DialogResult.No};
                case MessageBoxButtons.YesNoCancel:
                    return new[] {DialogResult.Yes, DialogResult.No, DialogResult.Cancel};
            }
            return new DialogResult[0];
        }

        public static string GetDefaultButtonText(DialogResult dialogResult)
        {
            switch (dialogResult)
            {
                case DialogResult.OK:
                    return Resources.AlertDlg_GetDefaultButtonText_OK;
                case DialogResult.Cancel:
                    return Resources.AlertDlg_GetDefaultButtonText_Cancel;
                case DialogResult.Yes:
                    return Resources.AlertDlg_GetDefaultButtonText__Yes;
                case DialogResult.No:
                    return Resources.AlertDlg_GetDefaultButtonText__No;
                case DialogResult.Abort:
                    return Resources.AlertDlg_GetDefaultButtonText__Abort;
                case DialogResult.Retry:
                    return Resources.AlertDlg_GetDefaultButtonText__Retry;
                case DialogResult.Ignore:
                    return Resources.AlertDlg_GetDefaultButtonText__Ignore;
                default:
                    throw new ArgumentException();
            }
        }

        private void messageScrollPanel_Resize(object sender, EventArgs e)
        {
            int newMaxWidth = messageScrollPanel.Width - _labelPadding;
            newMaxWidth = Math.Max(newMaxWidth, 100);
            labelMessage.MaximumSize = new Size(newMaxWidth, 0);
        }

        private const int MAX_MESSAGE_LENGTH = 50000;
        /// <summary>
        /// Labels have difficulty displaying text longer than 50,000 characters, and SetWindowText
        /// replaces strings longer than 520,000 characters with the empty string.
        /// If the message is too long, and append a line saying it was truncated.
        /// </summary>
        private string TruncateMessage(string message)
        {
            if (message == null)
            {
                return string.Empty;
            }
            if (message.Length < MAX_MESSAGE_LENGTH)
            {
                return message;
            }
            return TextUtil.LineSeparate(message.Substring(0, MAX_MESSAGE_LENGTH),
                Resources.AlertDlg_TruncateMessage_Message_truncated__Press_Ctrl_C_to_copy_entire_message_to_the_clipboard_);
        }

        private void toolStripButtonCopy_Click(object sender, EventArgs e)
        {
            CopyMessage();
        }

        private class NoBorderSystemRenderer : ToolStripSystemRenderer
        {
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
            }
        }
    }
}
