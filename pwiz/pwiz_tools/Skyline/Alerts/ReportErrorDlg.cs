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
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class ReportErrorDlg : FormEx
    {
        private string _exceptionType;
        private string _exceptionMessage;
        private string _stackTraceText;

        public static string UserGuid
        {
            get
            {
                string guid = Settings.Default.InstallationId;
                if (String.IsNullOrEmpty(guid))
                    guid = Settings.Default.InstallationId = Guid.NewGuid().ToString();
                return guid;
            }
        }

        protected ReportErrorDlg()
        {
        }

        public ReportErrorDlg(Exception e, StackTrace stackTraceExceptionCaughtAt)
        {
            Init(e.GetType().Name, e.Message, GetStackTraceText(e, stackTraceExceptionCaughtAt));

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

                SetTitleAndIntroText(
                    Text,
                    Resources.ReportErrorDlg_ReportErrorDlg_An_unexpected_error_has_occurred_as_shown_below,
                    Resources.ReportErrorDlg_ReportErrorDlg_An_error_report_will_be_posted);
            }
        }

        protected void Init(string exceptionType, string exceptionMessage, string stackTraceText)
        {
            InitializeComponent();

            _exceptionType = exceptionType;
            _exceptionMessage = exceptionMessage;
            _stackTraceText = stackTraceText;

            Icon = Resources.Skyline;
            tbErrorDescription.Text = _exceptionMessage;
            tbSourceCodeLocation.Text = stackTraceText;
        }

        protected virtual string PostTitle
        {
            get
            {
                var stackTraceReader = new StringReader(_stackTraceText);
                string line;
                while ((line = stackTraceReader.ReadLine()) != null)
                {
                    if (line.Contains(typeof (Program).Namespace ?? string.Empty))
                    {
                        int iSuffix = line.LastIndexOf("\\", StringComparison.Ordinal);  // Not L10N
                        if (iSuffix == -1)
                            iSuffix = line.LastIndexOf(".", StringComparison.Ordinal); // Not L10N

                        string location = line.Substring(iSuffix + 1);
                        string userInputIndicator = string.Empty;
                        if (!string.IsNullOrEmpty(tbEmail.Text))
                            userInputIndicator = "*"; // Not L10N
                        else if (!string.IsNullOrEmpty(tbMessage.Text))
                            userInputIndicator = "+"; // Not L10N
                        string version = Install.Version;
                        string guid = UserGuid;
                        guid = guid.Substring(guid.LastIndexOf('-') + 1);
                        return userInputIndicator + _exceptionType + " | " + location + " | " + version + " | " + guid; // Not L10N
                    }
                }
                return _exceptionType;
            }
        }

        private void OkDialog()
        {
            if (!Equals(Settings.Default.StackTraceListVersion, Install.Version))
            {
                Settings.Default.StackTraceListVersion = Install.Version;
            }

            SendErrorReport(MessageBody, _exceptionType);

            DialogResult = DialogResult.OK;
        }

        private void SendErrorReport(string messageBody, string exceptionType)
        {
            WebClient webClient = new WebClient();

            const string address = "https://skyline.gs.washington.edu/labkey/announcements/home/issues/exceptions/insert.view"; // Not L10N
            
            // ReSharper disable NonLocalizedString
            NameValueCollection form = new NameValueCollection
                                           {
                                               { "title", PostTitle},
                                               { "body", messageBody },
                                               { "fromDiscussion", "false"},
                                               { "allowMultipleDiscussions", "false"},
                                               { "rendererType", "TEXT_WITH_LINKS"}
                                           };
            webClient.UploadValues(address, form);
        }
        // ReSharper restore NonLocalizedString

        protected static string GetStackTraceText(Exception exception, StackTrace stackTraceExceptionCaughtAt = null)
        {
            StringBuilder stackTrace = new StringBuilder("Stack trace:"); // Not L10N

            stackTrace.AppendLine().AppendLine(exception.StackTrace).AppendLine();

            for (var x = exception.InnerException; x != null; x = x.InnerException)
            {
                if (ReferenceEquals(x, exception.InnerException))
                    stackTrace.AppendLine("Inner exceptions:"); // Not L10N
                else
                    stackTrace.AppendLine("---------------------------------------------------------------"); // Not L10N
                stackTrace.Append("Exception type: ").Append(x.GetType().FullName).AppendLine(); // Not L10N
                stackTrace.Append("Error message: ").AppendLine(x.Message); // Not L10N
                stackTrace.AppendLine(x.Message).AppendLine(x.StackTrace);
            }
            if (null != stackTraceExceptionCaughtAt)
            {
                stackTrace.AppendLine("Exception caught at: "); // Not L10N
                stackTrace.AppendLine(stackTraceExceptionCaughtAt.ToString());
            }
            return stackTrace.ToString();
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
                        sb.Append(" (64-bit)"); // Not L10N
                    sb.AppendLine();
                }

                sb.Append("Installation ID: ").AppendLine(UserGuid); // Not L10N

                sb.Append("Exception type: ").AppendLine(_exceptionType); // Not L10N
                sb.Append("Error message: ").AppendLine(_exceptionMessage).AppendLine(); // Not L10N
                
                // Stack trace with any inner exceptions
                sb.AppendLine(tbSourceCodeLocation.Text);

                return sb.ToString();
            }
        }

        protected void SetTitleAndIntroText(string title, string line1, string line2)
        {
            Text = title;
            lblReportError.Text = new StringBuilder()
                .AppendLine(line1)
                .Append(line2)
                .ToString();
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
