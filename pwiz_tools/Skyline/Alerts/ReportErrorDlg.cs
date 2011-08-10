/*
 * Original author: Shannon Joyner <sjoyner .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Deployment.Application;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Alerts
{
    public partial class ReportErrorDlg : Form
    {
        public enum ReportChoice { choice, always, never }

        public ReportErrorDlg(Exception e, ReportChoice reportChoice)
        {
            _exception = e;

            InitializeComponent();

            Icon = Resources.Skyline;

            tbErrorDescription.Text = e.Message;

            tbSourceCodeLocation.Text = StackTraceText;

            // If the user runs the daily version, they automatically
            // agree to letting us send reports.
            if (reportChoice != ReportChoice.choice)
            {
                btnOK.Visible = false;
                btnOK.DialogResult = DialogResult.None;

                btnCancel.Text = "Close";
                if (reportChoice == ReportChoice.always)
                    btnCancel.DialogResult = DialogResult.OK;
                AcceptButton = btnCancel;

                StringBuilder error = new StringBuilder("An unexpected error has occurred, as shown below.");
                if (reportChoice == ReportChoice.always)
                    error.AppendLine().Append("An error report will be posted.");

                lblReportError.Text = error.ToString();
            }
        }

        public static Exception _exception;

        public string StackTraceText
        {
            get
            {
                StringBuilder stackTrace = new StringBuilder("Stack trace:");

                stackTrace.AppendLine().AppendLine(_exception.StackTrace).AppendLine();

                for (var x = _exception.InnerException; x != null; x = x.InnerException)
                {
                    if (ReferenceEquals(x, _exception.InnerException))
                        stackTrace.AppendLine("Inner exceptions:");
                    else
                        stackTrace.AppendLine("---------------------------------------------------------------");
                    stackTrace.Append("Exception type: ").Append(x.GetType().FullName).AppendLine();
                    stackTrace.Append("Error message: ").AppendLine(x.Message);
                    stackTrace.AppendLine(x.Message).AppendLine(x.StackTrace);
                }
                return stackTrace.ToString();
            }
        }

    
        public string MessageBody
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                if (!string.IsNullOrEmpty(tbEmail.Text))
                    sb.Append("User email address: ").AppendLine(tbEmail.Text);
                
                if (!string.IsNullOrEmpty(tbMessage.Text))
                    sb.Append("User comments:").AppendLine().AppendLine(tbMessage.Text).AppendLine();
                
                if (ApplicationDeployment.IsNetworkDeployed)
                {
                    string version = ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString();
                    sb.Append("Skyline version: ").AppendLine(version);
                }

                string guid = Settings.Default.InstallationId;
                if (string.IsNullOrEmpty(guid))
                    guid = Settings.Default.InstallationId = Guid.NewGuid().ToString();
                sb.Append("Installation ID: ").AppendLine(guid);

                sb.Append("Exception type: ").AppendLine(ExceptionType);
                sb.Append("Error message: ").AppendLine(_exception.Message).AppendLine();
                
                // Stack trace with any inner exceptions
                sb.AppendLine(tbSourceCodeLocation.Text);

                return sb.ToString();
            }
        }

        public string ExceptionType
        {
            get
            {
                var type = _exception.GetType();
                return type.FullName;
            }
        }

        public void btnClipboard_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(MessageBody, true);
        }

        
    }
}
