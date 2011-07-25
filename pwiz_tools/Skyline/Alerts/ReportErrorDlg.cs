/*
 * Original author: Shannon Joyner <sjoyner .at. u.washington.edu>,
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
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Alerts
{
    public partial class ReportErrorDlg : Form
    {
        public ReportErrorDlg(Exception e)
        {
            _exception = e;

            InitializeComponent();

            Icon = Resources.Skyline;

            tbErrorDescription.Text = e.Message;

            tbSourceCodeLocation.Text = e.StackTrace;
        }

        public static Exception _exception;

        public string MessageBody
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                if (!string.IsNullOrEmpty(tbEmail.Text))
                    sb.Append("User Email Address: ").AppendLine(tbEmail.Text);
                if (!string.IsNullOrEmpty(tbMessage.Text))
                    sb.Append("User Comments:").AppendLine().AppendLine(tbMessage.Text).AppendLine();
                sb.Append("Error Message: ").AppendLine(_exception.Message).AppendLine();
                sb.AppendLine("Stack Trace:").AppendLine(_exception.StackTrace);

                for (var x = _exception.InnerException; x != null; x = x.InnerException)
                {
                    if (ReferenceEquals(x, _exception.InnerException))
                        sb.AppendLine("Inner Exceptions:");
                    else
                        sb.AppendLine("---------------------------------------------------------------");
                    sb.AppendLine(x.Message).AppendLine(x.StackTrace);
                }

                return sb.ToString();
            }
        }

        public void btnClipboard_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(MessageBody, true);
        }
    }
}
