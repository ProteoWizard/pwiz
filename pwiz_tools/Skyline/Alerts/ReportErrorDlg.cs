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

        private static string LABKEY_CSRF = @"X-LABKEY-CSRF";

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
            Init(e.GetType().Name, e.Message, ExceptionUtil.GetExceptionText(e, stackTraceExceptionCaughtAt));

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
                        // ReSharper disable LocalizableElement
                        int iSuffix = line.LastIndexOf("\\", StringComparison.Ordinal);
                        // ReSharper restore LocalizableElement
                        if (iSuffix == -1)
                            iSuffix = line.LastIndexOf(@".", StringComparison.Ordinal);

                        string location = line.Substring(iSuffix + 1);
                        string userInputIndicator = string.Empty;
                        if (!string.IsNullOrEmpty(_email))
                            userInputIndicator = @"*";
                        else if (!string.IsNullOrEmpty(_message))
                            userInputIndicator = @"+";
                        string version = Install.Version;
                        string guid = UserGuid;
                        guid = guid.Substring(guid.LastIndexOf('-') + 1);
                        return userInputIndicator + _exceptionType + @" | " + location + @" | " + version + @" | " + guid;
                    }
                }
                return _exceptionType;
            }
        }

        public void OkDialog()
        {
            using (var detailedReportErrorDlg = new DetailedReportErrorDlg())
            {
                if (detailedReportErrorDlg.ShowDialog(this) == DialogResult.OK)
                {
                    var skyFile = detailedReportErrorDlg.SkylineFileBytes;
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

        private void SendErrorReportAttachment(string exceptionType, IEnumerable<Image> screenShots, byte[] skyFileBytes, bool isTest)
        {
            if (isTest) // We don't want to be submitting an exception every time the ReportErrorDlgTest is run.
            {
                DialogResult = DialogResult.OK;
                return;  
            }

            string reportUrl = WebHelpers.GetSkylineLink(@"/announcements/home/issues/exceptions/insert.view");
            
            var nvc = new NameValueCollection
            {
                {@"title", PostTitle},
                {@"body", MessageBody},
                {@"fromDiscussion", @"false"},
                {@"allowMultipleDiscussions", @"false"},
                {@"rendererType", @"TEXT_WITH_LINKS"}
            };
            var files = new Dictionary<string, byte[]>();
            foreach (var screenShot in screenShots)
            {
                var memoryStream = new MemoryStream();
                screenShot.Save(memoryStream, ImageFormat.Jpeg);
                string name = @"Image-" + (files.Count + 1) + @".jpg";
                files.Add(name, memoryStream.ToArray());
            }

            if (skyFileBytes != null)
            {
                files.Add(@"skylineFile.sky", skyFileBytes);
            }
       
            HttpUploadFiles(reportUrl, @"image/jpeg", nvc, files);

            DialogResult = DialogResult.OK;
        }
        // ReSharper restore LocalizableElement
    
        private string MessageBody
        {
            get
            {
                var sb = new StringBuilder();
                if (!String.IsNullOrEmpty(_email))
                    sb.Append(@"User email address: ").AppendLine(_email);
                
                if (!String.IsNullOrEmpty(_message))
                    sb.Append(@"User comments:").AppendLine().AppendLine(_message).AppendLine();
                
                sb.Append(@"Skyline version: ").Append(Install.Version);
                if (Install.Is64Bit)
                    sb.Append(@" (64-bit)");
                sb.AppendLine();

                sb.Append(@"Installation ID: ").AppendLine(UserGuid);
                sb.Append(@"Exception type: ").AppendLine(_exceptionType);
                sb.Append(@"Error message: ").AppendLine(_exceptionMessage).AppendLine();
                sb.Append(@"--------------------").AppendLine().AppendLine();
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
            ClipboardHelper.SetClipboardText(this, MessageBody);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public static void HttpUploadFiles(string url, string contentType, NameValueCollection nvc, IEnumerable<KeyValuePair<string, byte[]>> files)
        {
            string boundary = @"---------------------------" + DateTime.Now.Ticks.ToString(@"x");
            // ReSharper disable LocalizableElement
            byte[] boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");
            // ReSharper restore LocalizableElement

            var wr = (HttpWebRequest) WebRequest.Create(url);
            wr.ContentType = @"multipart/form-data; boundary=" + boundary;
            wr.Method = @"POST";
            wr.KeepAlive = true;
            wr.Credentials = CredentialCache.DefaultCredentials;

            SetCSRFToken(wr);

            var rs = wr.GetRequestStream();

            const string formDataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
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
                const string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
                string paramName = string.Format(@"formFiles[{0:D2}", fileCount);
                string header = string.Format(headerTemplate, paramName, fileEntry.Key, contentType); //formFiles[00]
                byte[] headerbytes = Encoding.UTF8.GetBytes(header);
                rs.Write(headerbytes, 0, headerbytes.Length);
                rs.Write(fileEntry.Value, 0, fileEntry.Value.Length);
                fileCount ++;
            }
            // ReSharper disable LocalizableElement
            byte[] trailer = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
            // ReSharper restore LocalizableElement
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
                    Console.WriteLine(@"File uploaded, server response is: {0}", reader2.ReadToEnd());
                }
            }
            catch (Exception ex)
            {
                // ReSharper disable once LocalizableElement
                Console.WriteLine(@"Error uploading file: {0}", ex);
                if (wresp != null)
                {
                    wresp.Close();
                }
            }
        }

        private static void SetCSRFToken(HttpWebRequest postReq)
        {
            var url = WebHelpers.GetSkylineLink(@"/project/home/begin.view?");

            var sessionCookies = new CookieContainer();
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = @"GET";
                request.CookieContainer = sessionCookies;
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    postReq.CookieContainer = sessionCookies;
                    var csrf = response.Cookies[LABKEY_CSRF];
                    if (csrf != null)
                    {
                        // The server set a cookie called X-LABKEY-CSRF, get its value and add a header to the POST request
                        postReq.Headers.Add(LABKEY_CSRF, csrf.Value);
                    }
                    else
                    {
                        Console.WriteLine(@"CSRF token not found.");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(@"Error establishing a session and getting a CSRF token: {0}", e);
            }
        }
    }
}
