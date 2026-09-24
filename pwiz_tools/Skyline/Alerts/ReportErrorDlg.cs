/*
 * Original author: Shannon Joyner <sjoyner .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Alerts
{
    public partial class ReportErrorDlg : FormEx
    {
        /// <summary>
        /// The maximum size of an attachment to include in an error report.
        /// This number needs to be less than the "Maximum file size, in bytes, to allow in database BLOBs"
        /// setting on the Skyline website (which is currently set to 50,000,000)
        /// </summary>
        public const int MAX_ATTACHMENT_SIZE = 10_000_000;

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

        /// <summary>The exception message shown in the dialog (what went wrong) -- e.g. so the AI connector's
        /// form gate can report this "Unexpected Error" dialog the way it reports a CommonAlertDlg.</summary>
        public override string DetailedMessage => _exceptionMessage;

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
                    AlertsResources.ReportErrorDlg_ReportErrorDlg_An_unexpected_error_has_occurred_as_shown_below,
                    AlertsResources.ReportErrorDlg_ReportErrorDlg_An_error_report_will_be_posted);
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
       
            HttpUploadFiles(reportUrl, @"image/jpeg", nvc, files.Where(entry=>entry.Value.Length <= MAX_ATTACHMENT_SIZE));

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
            try
            {
                using var httpClient = new HttpClientWithProgress(null, null, new CookieContainer());
                SetCSRFToken(httpClient);

                using var content = new MultipartFormDataContent();
                foreach (string key in nvc.Keys)
                    content.Add(CreateFormPart(Encoding.UTF8.GetBytes(nvc[key] ?? string.Empty), key));
                int fileCount = 0;
                foreach (var fileEntry in files)
                {
                    // Missing its closing bracket, but the server has always received this name.
                    string paramName = string.Format(@"formFiles[{0:D2}", fileCount);
                    content.Add(CreateFormPart(fileEntry.Value, paramName, fileEntry.Key, contentType));
                    fileCount++;
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                using var response = httpClient.SendRequest(request);
                // ReSharper disable once LocalizableElement
                Console.WriteLine(@"File uploaded, server response is: {0}", response.Content.ReadAsStringAsync().Result);
            }
            // Only a server that answered with an error is logged and ignored. A failure to reach
            // the server at all propagates, so the user is told the report was not sent.
            catch (NetworkRequestException ex) when (ex.StatusCode.HasValue)
            {
                // ReSharper disable once LocalizableElement
                Console.WriteLine(@"Error uploading file: {0}", ex);
            }
        }

        private static HttpContent CreateFormPart(byte[] data, string name, string fileName = null, string contentType = null)
        {
            var part = new ByteArrayContent(data);
            // Built by hand to keep the wire format the server has always received.
            // ContentDispositionHeaderValue leaves values unquoted unless they are quoted here, and the
            // MultipartFormDataContent.Add(content, name, fileName) overload also adds a filename* parameter.
            part.Headers.ContentDisposition = new ContentDispositionHeaderValue(@"form-data")
            {
                Name = name.Quote(),
                FileName = fileName?.Quote()
            };
            if (contentType != null)
                part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return part;
        }

        private static void SetCSRFToken(HttpClientWithProgress httpClient)
        {
            var url = WebHelpers.GetSkylineLink(@"/project/home/begin.view?");
            try
            {
                httpClient.DownloadString(url);
                var csrf = httpClient.GetCookie(new Uri(url), LABKEY_CSRF);
                if (csrf != null)
                {
                    // The server set a cookie called X-LABKEY-CSRF. Send its value back as a header on the POST.
                    httpClient.AddHeader(LABKEY_CSRF, csrf);
                }
                else
                {
                    Console.WriteLine(@"CSRF token not found.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(@"Error establishing a session and getting a CSRF token: {0}", e);
            }
        }
    }
}
