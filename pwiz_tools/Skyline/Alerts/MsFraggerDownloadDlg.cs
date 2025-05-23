/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Ionic.Zip;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Alerts
{
    public partial class MsFraggerDownloadDlg : FormEx
    {
        private static readonly string LICENSE_URL = Settings.Default.MsFraggerLicenseUrl;
        private static readonly string VERIFY_URL = Settings.Default.MsFraggerVerifyUrl;
        private static readonly string DOWNLOAD_URL_WITH_TOKEN = $@"{Settings.Default.MsFraggerDownloadUrl}?token={{0}}&download=Release%20{MsFraggerSearchEngine.MSFRAGGER_VERSION}%24zip";
        private const string VERIFY_METHOD = @"POST";
        private static readonly Uri DOWNLOAD_URL_FOR_FUNCTIONAL_TESTS = new Uri($@"https://ci.skyline.ms/skyline_tool_testing_mirror/MSFragger-{MsFraggerSearchEngine.MSFRAGGER_VERSION}.zip");

        public class LinkInfo
        {
            public object Source;
            public int Start, End;
            public string Target;
        }
        private List<LinkInfo> linkInfos;

        public MsFraggerDownloadDlg()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            linkInfos = new List<LinkInfo>();
            formatRichTextMarkup(rtbAgreeToLicense);
            formatRichTextMarkup(rtbUsageConditions);
        }

        private void formatRichTextMarkup(RichTextBox rtb)
        {
            const string BOLD_PATTERN = "<b>(.*?)</b>";
            var boldMatch = Regex.Match(rtb.Text, BOLD_PATTERN);
            rtb.Select(boldMatch.Index, boldMatch.Length);
            try
            {
                rtb.SelectionFont = new Font(rtb.SelectionFont, FontStyle.Bold);
            }
            catch (Exception)
            {
                // Ignore failed attempt to set text to bold
            }
            rtb.SelectedText = Regex.Replace(rtb.SelectedText, BOLD_PATTERN, "$1");

            const string LINK_PATTERN = "<link(?:\\s*target=\"(.*?)\")?>(.*?)</link>";
            for (int i=0; i < 100; ++i)
            {
                var match = Regex.Match(rtb.Text, LINK_PATTERN);
                if (!match.Success)
                    break;

                rtb.Select(match.Index, match.Length); // select the match
                try
                {
                    rtb.SelectionFont = new Font(rtb.SelectionFont, FontStyle.Underline);
                }
                catch (Exception)
                {
                    // Ignore failed attempt to set text to underline
                }

                rtb.SelectionColor = SystemColors.HotTrack;
                rtb.SelectedText = Regex.Replace(rtb.SelectedText, LINK_PATTERN, "$2");
                linkInfos.Add(new LinkInfo
                {
                    Source = rtb,
                    Start = match.Index,
                    End = match.Index + match.Groups[2].Length,
                    Target = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value
                });
            }

            rtb.Select(0, 0);

            // do not allow focusing on the text boxes (so caret stays hidden)
            rtb.GotFocus += (sender, args) => cbAgreeToLicense.Focus();

            // listen for click and move to handle links and cursor changing
            rtb.MouseMove += RtbAgreeToLicense_MouseMove;
            rtb.MouseClick += RtbAgreeToLicense_MouseClick;
        }

        public void SetValues(string firstName, string lastName, string email, string institution)
        {
            tbFirstName.Text = firstName;
            tbLastName.Text = lastName;
            tbEmail.Text = email;
            tbInstitution.Text = institution;
            cbAgreeToLicense.Checked = true;
            tbVerificationCode.Text = @"1234";
            // now clicking "Accept" button should start download
        }

        private string RequestVerificationCode()
        {
            if (Program.FunctionalTest && !Program.IsPaused)
                return string.Empty;

            using var downloadProgressDlg = new LongWaitDlg();
            using var client = new WebClient();

            var postData = new NameValueCollection();
            postData[@"transfer"] = @"academic";
            postData[@"agreement1"] = @"on";
            postData[@"agreement2"] = @"on";
            postData[@"agreement3"] = @"on";
            postData[@"first_name"] = tbFirstName.Text;
            postData[@"last_name"] = tbLastName.Text;
            postData[@"email"] = tbEmail.Text;
            postData[@"organization"] = tbInstitution.Text;
            postData[@"download"] = $@"Release {MsFraggerSearchEngine.MSFRAGGER_VERSION}$zip";
            postData[@"is_fragpipe"] = @"true";

            string resultString = string.Empty;
            downloadProgressDlg.PerformWork(this, 50, broker =>
            {
                var uploadTask = client.UploadValuesTaskAsync(VERIFY_URL, VERIFY_METHOD, postData);
                uploadTask.Wait(broker.CancellationToken);
                if (uploadTask.Exception != null)
                    throw uploadTask.Exception;

                var resultBytes = uploadTask.Result;
                resultString = Encoding.UTF8.GetString(resultBytes);
            });
            return resultString;
        }

        private bool Download()
        {
            using (var downloadProgressDlg = new LongWaitDlg())
            {
                downloadProgressDlg.Message = string.Format(AlertsResources.MsFraggerDownloadDlg_Download_Downloading_MSFragger__0_, MsFraggerSearchEngine.MSFRAGGER_VERSION);
                if (Program.FunctionalTest && !Program.IsPaused) // original URLs not possible with "verification code" system
                {
                    var msFraggerDownloadInfo = MsFraggerSearchEngine.MsFraggerDownloadInfo;
                    msFraggerDownloadInfo.DownloadUrl = DOWNLOAD_URL_FOR_FUNCTIONAL_TESTS;
                    downloadProgressDlg.PerformWork(this, 50, pm => SimpleFileDownloader.DownloadRequiredFiles(new[] { msFraggerDownloadInfo }, pm));
                    return true;
                }

                using (var client = new WebClient())
                {
                    var status = downloadProgressDlg.PerformWork(this, 50, broker =>
                    {
                        client.DownloadProgressChanged += (sender, args) => broker.UpdateProgress(new ProgressStatus().ChangePercentComplete(args.ProgressPercentage));

                        string downloadUrl = string.Format(DOWNLOAD_URL_WITH_TOKEN, tbVerificationCode.Text);
                        var msFraggerZipBytes = client.DownloadData(downloadUrl);

                        // check if downloaded bytes are actually a zip file
                        if (!ZipFile.IsZipFile(new MemoryStream(msFraggerZipBytes), false))
                        {
                            var resultString = Encoding.UTF8.GetString(msFraggerZipBytes);
                            broker.UpdateProgress(new ProgressStatus().ChangeErrorException(
                                new InvalidOperationException(string.Format(@"{0}{1}{1}{2}",
                                    Resources.TestToolStoreClient_GetToolZipFile_Error_downloading_tool,
                                    Environment.NewLine, resultString))));
                            return;
                        }

                        var installPath = MsFraggerSearchEngine.MsFraggerDirectory;
                        var downloadFilename = Path.Combine(installPath, MsFraggerSearchEngine.MSFRAGGER_FILENAME + @".zip");
                        using (var fileSaver = new FileSaver(downloadFilename))
                        {
                            File.WriteAllBytes(fileSaver.SafeName, msFraggerZipBytes);
                            fileSaver.Commit();
                        }

                        Directory.CreateDirectory(installPath);
                        using (var zipFile = new ZipFile(downloadFilename))
                        {
                            zipFile.ExtractAll(installPath, ExtractExistingFileAction.OverwriteSilently);
                        }
                        FileEx.SafeDelete(downloadFilename);
                    });

                    if (status.IsError)
                    {
                        MessageDlg.Show(this, status.ErrorException.Message);
                        return false;
                    }
                    return true;
                }
            }
        }

        public void ClickAccept() { btnAccept.PerformClick(); }

        private bool IsReadyToVerify => cbAgreeToLicense.Checked && tbFirstName.TextLength > 0 &&
                                        tbLastName.TextLength > 0 && tbEmail.Text.IsValidEmail() &&
                                        tbInstitution.TextLength > 0;

        private void btnAccept_Click(object sender, EventArgs e)
        {
            if (!Download())
                return;

            DialogResult = DialogResult.OK;
        }

        private void btnRequestVerificationCode_Click(object sender, EventArgs e)
        {
            var verificationResultHtml = RequestVerificationCode();
            lblVerificationCode.Enabled = tbVerificationCode.Enabled = true;
            var checkEmailDlg =
                new AlertDlg(AlertsResources.MsFraggerDownloadDlg_btnRequestVerificationCode_Click_Check_your_email, MessageBoxButtons.OK)
                {
                    DetailMessage = verificationResultHtml,
                    StartPosition = FormStartPosition.CenterParent
                };
            checkEmailDlg.Show(this);
        }

        private bool CheckCursorWithinRange(RichTextBox rtb, MouseEventArgs e, LinkInfo linkInfo)
        {
            if (linkInfo.Source != rtb)
                return false;

            int charIndex = rtb.GetCharIndexFromPosition(e.Location);
            return linkInfo.Start <= charIndex && linkInfo.End >= charIndex;
        }

        private void RtbAgreeToLicense_MouseMove(object sender, MouseEventArgs e)
        {
            var rtb = sender as RichTextBox;
            if (rtb == null) return;

            if (linkInfos.Any(p => CheckCursorWithinRange(rtb, e, p)))
            {
                if (Cursor != Cursors.Hand)
                    Cursor = rtb.Cursor = Cursors.Hand;
            }
            else if (Cursor == Cursors.Hand)
                Cursor = rtb.Cursor = Cursors.Default;
            else
                base.OnMouseMove(e);
        }

        private void RtbAgreeToLicense_MouseClick(object sender, MouseEventArgs e)
        {
            foreach (var linkInfo in linkInfos)
            {
                if (CheckCursorWithinRange(sender as RichTextBox, e, linkInfo))
                {
                    var target = linkInfo.Target;
                    if (target == @"lic")
                        target = LICENSE_URL;
                    WebHelpers.OpenLink(this, target);
                    return;
                }
            }

            if (sender == rtbAgreeToLicense)
                cbAgreeToLicense.Checked = !cbAgreeToLicense.Checked;
        }

        private void CheckVerificationReady()
        {
            btnRequestVerificationCode.Enabled = IsReadyToVerify;
        }

        private void tbTextChanged(object sender, EventArgs e)
        {
            CheckVerificationReady();
        }

        private void cbAgreeToLicense_CheckedChanged(object sender, EventArgs e)
        {
            CheckVerificationReady();
        }

        private void tbVerificationCodeChanged(object sender, EventArgs e)
        {
            btnAccept.Enabled = tbVerificationCode.TextLength > 0;
        }
    }
}
