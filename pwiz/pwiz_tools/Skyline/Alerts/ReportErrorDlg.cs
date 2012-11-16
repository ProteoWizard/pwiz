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
using System.Collections.Specialized;
using System.Deployment.Application;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class ReportErrorDlg : FormEx
    {
        private readonly Exception _exception;
        private readonly StackTrace _stackTraceExceptionCaughtAt;

        public ReportErrorDlg(Exception e, StackTrace stackTraceExceptionCaughtAt)
        {
            _exception = e;
            _stackTraceExceptionCaughtAt = stackTraceExceptionCaughtAt;

            InitializeComponent();

            Icon = Resources.Skyline;

            tbErrorDescription.Text = e.Message;

            tbSourceCodeLocation.Text = StackTraceText;

            Install.InstallType installType = Install.Type;

            // The user can choose to send the error report unless he's running the daily version,
            // which automatically sends the report.
            if (installType == Install.InstallType.daily)
            {
                btnOK.Visible = false;
                btnOK.DialogResult = DialogResult.None;

                btnCancel.Text = btnOK.Text;
                btnCancel.DialogResult = DialogResult.OK;
                btnCancel.Click += btnOK_Click;
                AcceptButton = btnCancel;

                lblReportError.Text = new StringBuilder()
                    .AppendLine(Resources.ReportErrorDlg_ReportErrorDlg_An_unexpected_error_has_occurred_as_shown_below)
                    .Append(Resources.ReportErrorDlg_ReportErrorDlg_An_error_report_will_be_posted)
                    .ToString();
            }
        }

        private void OkDialog()
        {
            if (!Equals(Settings.Default.StackTraceListVersion, Install.Version))
            {
                Settings.Default.StackTraceListVersion = Install.Version;
            }

            SendErrorReport(MessageBody, ExceptionType);

            DialogResult = DialogResult.OK;
        }

        private void SendErrorReport(string messageBody, string exceptionType)
        {
            WebClient webClient = new WebClient();

            const string address = "https://skyline.gs.washington.edu/labkey/announcements/home/issues/exceptions/insert.view"; // Not L10N

            NameValueCollection form = new NameValueCollection // Not L10N: Information passed to browser
                                           {
                                               { "title", "Unhandled " + exceptionType},
                                               { "body", messageBody },
                                               { "fromDiscussion", "false"},
                                               { "allowMultipleDiscussions", "false"},
                                               { "rendererType", "TEXT_WITH_LINKS"}
                                           };

            webClient.UploadValues(address, form);
        }

        private string StackTraceText
        {
            get
            {
                StringBuilder stackTrace = new StringBuilder("Stack trace:"); // Not L10N

                stackTrace.AppendLine().AppendLine(_exception.StackTrace).AppendLine();

                for (var x = _exception.InnerException; x != null; x = x.InnerException)
                {
                    if (ReferenceEquals(x, _exception.InnerException))
                        stackTrace.AppendLine("Inner exceptions:"); // Not L10N
                    else
                        stackTrace.AppendLine("---------------------------------------------------------------"); // Not L10N
                    stackTrace.Append("Exception type: ").Append(x.GetType().FullName).AppendLine(); // Not L10N
                    stackTrace.Append("Error message: ").AppendLine(x.Message); // Not L10N
                    stackTrace.AppendLine(x.Message).AppendLine(x.StackTrace);
                }
                if (null != _stackTraceExceptionCaughtAt)
                {
                    stackTrace.AppendLine("Exception caught at: ");
                    stackTrace.AppendLine(_stackTraceExceptionCaughtAt.ToString());
                }
                return stackTrace.ToString();
            }
        }

    
        private string MessageBody
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                if (!String.IsNullOrEmpty(tbEmail.Text))
                    sb.Append("User email address: ").AppendLine(tbEmail.Text); // Not L10N
                
                if (!String.IsNullOrEmpty(tbMessage.Text))
                    sb.Append("User comments:").AppendLine().AppendLine(tbMessage.Text).AppendLine(); // Not L10N
                
                if (ApplicationDeployment.IsNetworkDeployed)
                {
                    sb.Append("Skyline version: ").Append(Install.Version); // Not L10N
                    if (Install.Is64Bit)
                        sb.Append(" (64-bit)");
                    sb.AppendLine();
                }

                string guid = Settings.Default.InstallationId;
                if (String.IsNullOrEmpty(guid))
                    guid = Settings.Default.InstallationId = Guid.NewGuid().ToString();
                sb.Append("Installation ID: ").AppendLine(guid); // Not L10N

                sb.Append("Exception type: ").AppendLine(ExceptionType); // Not L10N
                sb.Append("Error message: ").AppendLine(_exception.Message).AppendLine(); // Not L10N
                
                // Stack trace with any inner exceptions
                sb.AppendLine(tbSourceCodeLocation.Text);

                return sb.ToString();
            }
        }

        private string ExceptionType
        {
            get
            {
                var type = _exception.GetType();
                return type.FullName;
            }
        }

        private void btnClipboard_Click(object sender, EventArgs e)
        {
            ClipboardEx.SetDataObject(MessageBody, true);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }        
    }
}
