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
using SharedBatch.Properties;


namespace SharedBatch
{
    /// <summary>
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
    /// </summary>
    public partial class AlertDlg : Form
    {
        private const int MaxHeight = 500;
        private readonly int _originalFormHeight;
        private readonly int _originalMessageHeight;
        private string _message;
        private string _detailMessage;

        private AlertDlg(string appName, string message, Image icon, bool fitToText = false)
        {
            InitializeComponent();
            _originalFormHeight = Height;
            _originalMessageHeight = labelMessage.Height;
            Message = message;
            btnMoreInfo.Parent.Controls.Remove(btnMoreInfo);
            Text = appName;
            pictureBox1.Image = icon;
            if (fitToText)
            {
                Paint += FitWidthToMessage;
            }
        }

        public static void ShowWarning(IWin32Window parent, string appName, string message)
        {
            Show(parent, appName, message, SystemIcons.Warning.ToBitmap(), MessageBoxButtons.OK);
        }
        public static void ShowError(IWin32Window parent, string appName, string message)
        {
            Show(parent, appName, message, SystemIcons.Error.ToBitmap(), MessageBoxButtons.OK);
        }
        public static void ShowInfo(IWin32Window parent, string appName, string message)
        {
            Show(parent, appName, message, SystemIcons.Information.ToBitmap(), MessageBoxButtons.OK);
        }
        public static DialogResult ShowQuestion(IWin32Window parent, string appName, string message)
        {
            return Show(parent, appName, message, SystemIcons.Question.ToBitmap(), MessageBoxButtons.YesNo);
        }

        public static DialogResult ShowOkCancel(IWin32Window parent, string appName, string message)
        {
            return Show(parent, appName, message, SystemIcons.Question.ToBitmap(), MessageBoxButtons.OKCancel);
        }

        public static DialogResult ShowLargeOkCancel(IWin32Window parent, string appName, string message)
        {
            var questionDlg = new AlertDlg(appName, message, SystemIcons.Question.ToBitmap(), true);
            return questionDlg.ShowAndDispose(parent, MessageBoxButtons.OKCancel);
        }

        private static DialogResult Show(IWin32Window parent, string appName, string message, Image icon, MessageBoxButtons messageBoxButtons)
        {
            return new AlertDlg(appName, message, icon).ShowAndDispose(parent, messageBoxButtons);
        }
        public static void ShowErrorWithException(IWin32Window parent, string appName, string message, Exception exception)
        {
            new AlertDlg(appName, message, SystemIcons.Error.ToBitmap()) { Exception = exception }.ShowAndDispose(parent, MessageBoxButtons.OK);
        }

        public string Message
        {
            get
            {
                return _message;
            }
            private set
            {
                _message = value;
                labelMessage.Text = TruncateMessage(_message);
                int formGrowth = Math.Max(labelMessage.Height - _originalMessageHeight * 3, 0);
                formGrowth = Math.Min(formGrowth, MaxHeight);
                Height = _originalFormHeight + formGrowth;
            }
        }

        private DialogResult ShowAndDispose(IWin32Window parent, MessageBoxButtons messageBoxButtons)
        {
            AddMessageBoxButtons(messageBoxButtons);
            using (this)
            {
                return ShowDialog(parent);
            }
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

        public void FitWidthToMessage(object sender, PaintEventArgs e)
        {
            var messageStart = labelMessage.Location.X;
            var padding = 30;
            var measureString = labelMessage.Text;
            SizeF stringSize = e.Graphics.MeasureString(measureString, labelMessage.Font);
            Width = messageStart + padding + (int)stringSize.Width;
            labelMessage.MaximumSize = new Size((int)stringSize.Width, 0);
            Paint -= FitWidthToMessage;
        }

        private void AddMessageBoxButtons(MessageBoxButtons messageBoxButtons) 
        {
            foreach (var dialogResult in GetDialogResults(messageBoxButtons).Reverse())
            {
                AddButton(dialogResult);
            }
        }

        /// <summary>
        /// Returns the buttons on the button bar from LEFT to RIGHT.
        /// </summary>
        private IEnumerable<Button> VisibleButtons =>
            // return the buttons in reverse order because buttonPanel is a right-to-left FlowPanel.
            buttonPanel.Controls.OfType<Button>().Reverse();

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

        private Button FindButton(DialogResult buttonDialogResult)
        {
            return VisibleButtons.FirstOrDefault(button => button.DialogResult == buttonDialogResult);
        }

        private void ClickButton(Button button)
        {
            if (!button.Visible)
            {
                throw new NotSupportedException();
            }
            CheckDisposed();
            button.PerformClick();
        }

        private void CheckDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(@"Form disposed");
            }
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

        private static string GetDefaultButtonText(DialogResult dialogResult)
        {
            switch (dialogResult)
            {
                case DialogResult.OK:
                    return Resources.AlertDlg_GetDefaultButtonText_OK;
                case DialogResult.Cancel:
                    return Resources.AlertDlg_GetDefaultButtonText_Cancel;
                case DialogResult.Yes:
                    return Resources.AlertDlg_GetDefaultButtonText_Yes;
                case DialogResult.No:
                    return Resources.AlertDlg_GetDefaultButtonText_No;
                case DialogResult.Abort:
                    return Resources.AlertDlg_GetDefaultButtonText_Abort;
                case DialogResult.Retry:
                    return Resources.AlertDlg_GetDefaultButtonText_Retry;
                case DialogResult.Ignore:
                    return Resources.AlertDlg_GetDefaultButtonText_Ignore;
                default:
                    throw new ArgumentException();
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

        public string DetailMessage
        {
            get => _detailMessage;
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

        private const int MaxMessageLength = 50000;
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
            if (message.Length < MaxMessageLength)
            {
                return message;
            }
            return TextUtil.LineSeparate(message.Substring(0, MaxMessageLength),
                Resources.AlertDlg_TruncateMessage_Message_truncated__Press_Ctrl_C_to_copy_entire_message_to_the_clipboard_);
        }

        /// <summary>
        /// Sealed to keep ReSharper happy, because we set it in constructors
        /// </summary>
        public sealed override string Text
        {
            get => base.Text;
            set => base.Text = value;
        }
    }
}
