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
using System.Windows.Forms;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
    /// </summary>
    public partial class MessageDlg : FormEx
    {
        public static void Show(IWin32Window parent, string message, params object[] args)
        {
            var formatMessage = string.Format(message, args);
            using (var dlg = new MessageDlg(formatMessage))
            {
                dlg.ShowWithTimeout(parent, formatMessage);
            }
        }

        protected MessageDlg(string message)
        {
            InitializeComponent();

            int height = labelMessage.Height;
            labelMessage.Text = Message = message;
            Height += Math.Max(0, labelMessage.Height - height*3);
        }

        protected override void CreateHandle()
        {
            base.CreateHandle();

            Text = Program.Name;
        }

        public string Message { get; private set; }

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
            const string formatMessage =
@"---------------------------
{0}
---------------------------
{1}
---------------------------
OK
---------------------------
"; // Not L10N
            ClipboardEx.SetText(string.Format(formatMessage, Text, Message));
        }
    }
}
