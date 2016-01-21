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
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
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
        private string _email;
        private string _message;

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

                SetIntroText(
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
                        if (!string.IsNullOrEmpty(_email))
                            userInputIndicator = "*"; // Not L10N
                        else if (!string.IsNullOrEmpty(_message))
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

        public void OkDialog()
        {
            if (!Equals(Settings.Default.StackTraceListVersion, Install.Version))
            {
                Settings.Default.StackTraceListVersion = Install.Version;
            }
            using (var detailedReportErrorDlg = new DetailedReportErrorDlg())
            {
                if (detailedReportErrorDlg.ShowDialog(this) == DialogResult.OK)
                {
                    var skyFile = detailedReportErrorDlg.SkylineFile;
                     _email = detailedReportErrorDlg.Email;
                    _message = detailedReportErrorDlg.Message;

                    SendErrorReportAttachment(_exceptionType, detailedReportErrorDlg.ScreenShots, 
                        skyFile, detailedReportErrorDlg.IsTest);
                }
                else
                {
                    DialogResult = DialogResult.Cancel;
                }

            }
        }

        private void SendErrorReportAttachment(string exceptionType, IEnumerable<Image> screenShots, string skyFile, bool isTest)
        {
            if (isTest) // We don't want to be submitting an exception every time the ReportErrorDlgTest is run.
            {
                DialogResult = DialogResult.OK;
                return;  
            }

            const string reportUrl = "https://skyline.gs.washington.edu/labkey/announcements/home/issues/exceptions/insert.view"; // Not L10N
            
            var nvc = new NameValueCollection
            {
                {"title", PostTitle}, // Not L10N
                {"body", MessageBody}, // Not L10N
                {"fromDiscussion", "false"}, // Not L10N
                {"allowMultipleDiscussions", "false"}, // Not L10N
                {"rendererType", "TEXT_WITH_LINKS"} // Not L10N
            };
            var files = new Dictionary<string, byte[]>();
            foreach (var screenShot in screenShots)
            {
                var memoryStream = new MemoryStream();
                screenShot.Save(memoryStream, ImageFormat.Jpeg);
                string name = "Image-" + (files.Count + 1) + ".jpg"; // Not L10N
                files.Add(name, memoryStream.ToArray());
            }

            if (!string.IsNullOrEmpty(skyFile))
            {
                byte[] skyFileBytes = new byte[skyFile.Length * sizeof(char)];
                Buffer.BlockCopy(skyFile.ToCharArray(), 0, skyFileBytes, 0, skyFileBytes.Length);
                files.Add("skylineFile.sky", skyFileBytes); // Not L10N
            }
       
            HttpUploadFiles(reportUrl, "image/jpeg", nvc, files); // Not L10N 

            DialogResult = DialogResult.OK;
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
                var sb = new StringBuilder();
                if (!String.IsNullOrEmpty(_email))
                    sb.Append("User email address: ").AppendLine(_email); // Not L10N
                
                if (!String.IsNullOrEmpty(_message))
                    sb.Append("User comments:").AppendLine().AppendLine(_message).AppendLine(); // Not L10N
                
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
                sb.Append("--------------------").AppendLine().AppendLine();  // Not L10N
                // Stack trace with any inner exceptions
                sb.AppendLine(tbSourceCodeLocation.Text);

                return sb.ToString();
            }
        }

        protected void SetTitleAndIntroText(string title, string line1, string line2)
        {
            Text = title;
            SetIntroText(line1, line2);
        }

        private void SetIntroText(string line1, string line2)
        {
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

        public static void HttpUploadFiles(string url, string contentType, NameValueCollection nvc, IEnumerable<KeyValuePair<string, byte[]>> files)
        {
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x"); // Not L10N
            byte[] boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n"); // Not L10N

            var wr = (HttpWebRequest) WebRequest.Create(url);
            wr.ContentType = "multipart/form-data; boundary=" + boundary; // Not L10N
            wr.Method = "POST"; // Not L10N
            wr.KeepAlive = true;
            wr.Credentials = CredentialCache.DefaultCredentials;

            var rs = wr.GetRequestStream();

            const string formDataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}"; // Not L10N
            foreach (string key in nvc.Keys)
            {
                rs.Write(boundarybytes, 0, boundarybytes.Length);
                string formitem = string.Format(formDataTemplate, key, nvc[key]);
                byte[] formitembytes = Encoding.UTF8.GetBytes(formitem);
                rs.Write(formitembytes, 0, formitembytes.Length);
            }
            int fileCount = 0;
            foreach (var fileEntry in files)
            {
                rs.Write(boundarybytes, 0, boundarybytes.Length);
                const string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n"; // Not L10N
                string paramName = string.Format("formFiles[{0:D2}", fileCount); // Not L10N
                string header = string.Format(headerTemplate, paramName, fileEntry.Key, contentType); //formFiles[00]
                byte[] headerbytes = Encoding.UTF8.GetBytes(header);
                rs.Write(headerbytes, 0, headerbytes.Length);
                rs.Write(fileEntry.Value, 0, fileEntry.Value.Length);
                fileCount ++;
            }
            byte[] trailer = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n"); // Not L10N
            rs.Write(trailer, 0, trailer.Length);
            rs.Close();


            WebResponse wresp = null;
            try
            {
                wresp = wr.GetResponse();
                var stream2 = wresp.GetResponseStream();
                if (stream2 != null)
                {
                    var reader2 = new StreamReader(stream2);
                    // ReSharper disable once LocalizableElement
                    Console.WriteLine("File uploaded, server response is: {0}", reader2.ReadToEnd()); // Not L10N
                }
            }
            catch (Exception ex)
            {
                // ReSharper disable once LocalizableElement
                Console.WriteLine("Error uploading file: {0}", ex); // Not L10N
                if (wresp != null)
                {
                    wresp.Close();
                }
            }
        }
    }
}
