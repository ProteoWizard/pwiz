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
using System.Windows.Forms;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
    /// </summary>
    public partial class MessageDlg : FormEx
    {
        public static void Show(IWin32Window parent, string message)
        {
            ShowWithException(parent, message, null);
        }

        public static void ShowException(IWin32Window parent, Exception exception)
        {
            ShowWithException(parent, exception.Message, exception);
        }

        public static void ShowWithException(IWin32Window parent, string message, Exception exception)
        {
            string detailMessage = null;
            if (exception != null)
            {
                detailMessage = exception.ToString();
            }
            using (var dlg = new MessageDlg(message, detailMessage))
            {
                dlg.ShowWithTimeout(parent, message);
            }
        }

        protected MessageDlg(string message, string detailMessage)
        {
            InitializeComponent();

            int height = labelMessage.Height;
            labelMessage.Text = Message = message;
            if (null != detailMessage)
            {
                tbxDetail.Text = DetailMessage = detailMessage;
                btnMoreInfo.Visible = true;
            }
            else
            {
                btnMoreInfo.Visible = false;
            }
            Height += Math.Max(0, labelMessage.Height - height*3);
        }

        protected override void CreateHandle()
        {
            base.CreateHandle();

            Text = Program.Name;
        }

        public string Message { get; private set; }

        public string DetailMessage { get; private set; }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
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
            const string separator = "---------------------------"; // Not L10N
            List<string> lines = new List<String>();
            lines.Add(separator);
            lines.Add(Text);
            lines.Add(separator);
            lines.Add(Message);
            lines.Add(separator);
            if (null != DetailMessage)
            {
                lines.Add(TextUtil.SpaceSeparate(btnOk.Text, btnMoreInfo.Text));
                lines.Add(separator);
                lines.Add(DetailMessage);
            }
            else
            {
                lines.Add(btnOk.Text);
            }
            lines.Add(separator);
            lines.Add(string.Empty);
            ClipboardEx.SetText(TextUtil.LineSeparate(lines));
        }

        private void btnMoreInfo_Click(object sender, EventArgs e)
        {
            if (!tbxDetail.Visible)
            {
                Height += tbxDetail.Height;
                tbxDetail.Dock = DockStyle.Bottom;
                tbxDetail.Visible = true;
            }
        }
    }
}
