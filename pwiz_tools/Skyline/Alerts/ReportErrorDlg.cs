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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Deployment.Application;
using System.Net;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class ReportErrorDlg : FormEx
    {
        public ReportErrorDlg(Exception e, List<string> stackTraceList)
        {
            _exception = e;
            _stackTraceList = stackTraceList;

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

                btnCancel.Text = "Close";
                btnCancel.DialogResult = DialogResult.OK;
                AcceptButton = btnCancel;

                StringBuilder error = new StringBuilder("An unexpected error has occurred, as shown below.");
                error.AppendLine().Append("An error report will be posted.");

                lblReportError.Text = error.ToString();
            }
        }

        private void OkDialog()
        {
            if (!Equals(Settings.Default.StackTraceListVersion, Install.Version))
            {
                Settings.Default.StackTraceListVersion = Install.Version;
                _stackTraceList.Clear();
            }

            string stackText = StackTraceText;
            if (!_stackTraceList.Contains(stackText))
            {
                _stackTraceList.Add(stackText);
                SendErrorReport(MessageBody, ExceptionType);
            }

            DialogResult = DialogResult.OK;
        }

        private void SendErrorReport(string messageBody, string exceptionType)
        {
            WebClient webClient = new WebClient();

            const string address = "https://skyline.gs.washington.edu/labkey/announcements/home/issues/exceptions/insert.view";

            NameValueCollection form = new NameValueCollection
                                           {
                                               { "title", "Unhandled " + exceptionType},
                                               { "body", messageBody },
                                               { "fromDiscussion", "false"},
                                               { "allowMultipleDiscussions", "false"},
                                               { "rendererType", "TEXT_WITH_LINKS"}
                                           };

            webClient.UploadValues(address, form);
        }

        private readonly Exception _exception;
        private readonly List<string> _stackTraceList;

        private string StackTraceText
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

    
        private string MessageBody
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                if (!String.IsNullOrEmpty(tbEmail.Text))
                    sb.Append("User email address: ").AppendLine(tbEmail.Text);
                
                if (!String.IsNullOrEmpty(tbMessage.Text))
                    sb.Append("User comments:").AppendLine().AppendLine(tbMessage.Text).AppendLine();
                
                if (ApplicationDeployment.IsNetworkDeployed)
                {
                    sb.Append("Skyline version: ").AppendLine(Install.Version);
                }

                string guid = Settings.Default.InstallationId;
                if (String.IsNullOrEmpty(guid))
                    guid = Settings.Default.InstallationId = Guid.NewGuid().ToString();
                sb.Append("Installation ID: ").AppendLine(guid);

                sb.Append("Exception type: ").AppendLine(ExceptionType);
                sb.Append("Error message: ").AppendLine(_exception.Message).AppendLine();
                
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
