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
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Ionic.Zip;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Alerts
{
    public partial class MsFraggerDownloadDlg : FormEx
    {
        private const string LICENSE_URL = @"https://msfragger.arsci.com/upgrader/MSFragger-LICENSE.pdf";
        private const string DOWNLOAD_URL = @"http://msfragger-upgrader.nesvilab.org/upgrader/upgrade_download.php";
        private const string DOWNLOAD_METHOD = @"POST";
        private readonly Uri DOWNLOAD_URL_FOR_FUNCTIONAL_TESTS = new Uri($@"https://example.com/MSFragger-{MsFraggerSearchEngine.MSFRAGGER_VERSION}.zip");

        private Tuple<int, int> licenseLinkStartEnd;

        public MsFraggerDownloadDlg()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            const string BOLD_PATTERN = "<b>(.*?)</b>";
            var academicBoldMatch = Regex.Match(rtbAgreeToLicense.Text, BOLD_PATTERN);
            rtbAgreeToLicense.Select(academicBoldMatch.Index, academicBoldMatch.Length);
            try
            {
                rtbAgreeToLicense.SelectionFont = new Font(rtbAgreeToLicense.SelectionFont, FontStyle.Bold);
            }
            catch (Exception)
            {
                // Ignore failed attempt to set text to bold
            }
            rtbAgreeToLicense.SelectedText = Regex.Replace(rtbAgreeToLicense.SelectedText, BOLD_PATTERN, "$1");

            const string LINK_PATTERN = "<link>(.*?)</link>";
            var licenseLinkMatch = Regex.Match(rtbAgreeToLicense.Text, LINK_PATTERN);
            rtbAgreeToLicense.Select(licenseLinkMatch.Index, licenseLinkMatch.Length);
            try
            {
                rtbAgreeToLicense.SelectionFont = new Font(rtbAgreeToLicense.SelectionFont, FontStyle.Underline);
            }
            catch (Exception)
            {
                // Ignore failed attempt to set text to underline
            }
            rtbAgreeToLicense.SelectionColor = SystemColors.HotTrack;
            rtbAgreeToLicense.SelectedText = Regex.Replace(rtbAgreeToLicense.SelectedText, LINK_PATTERN, "$1");
            licenseLinkStartEnd = new Tuple<int, int>(licenseLinkMatch.Index, licenseLinkMatch.Index + licenseLinkMatch.Length - 13);

            rtbAgreeToLicense.Select(0, 0);
            tbUsageConditions.Select(0, 0);

            rtbAgreeToLicense.MouseMove += RtbAgreeToLicense_MouseMove;
            rtbAgreeToLicense.MouseClick += RtbAgreeToLicense_MouseClick;

            tbName.TextChanged += tbTextChanged;
            tbEmail.TextChanged += tbTextChanged;
            tbInstitution.TextChanged += tbTextChanged;

            // do not allow focusing on the text boxes (so caret stays hidden)
            rtbAgreeToLicense.GotFocus += (sender, args) => cbAgreeToLicense.Focus();
            tbUsageConditions.GotFocus += (sender, args) => tbName.Focus();
        }

        public void SetValues(string name, string email, string institution)
        {
            tbName.Text = name;
            tbEmail.Text = email;
            tbInstitution.Text = institution;
            cbAgreeToLicense.Checked = true;
        }

        public void Download()
        {
            using (var downloadProgressDlg = new LongWaitDlg { Message = string.Format(Resources.MsFraggerDownloadDlg_Download_Downloading_MSFragger__0_, MsFraggerSearchEngine.MSFRAGGER_VERSION) })
            {
                if (Program.FunctionalTest && !Program.UseOriginalURLs)
                {
                    var msFraggerDownloadInfo = MsFraggerSearchEngine.MsFraggerDownloadInfo;
                    msFraggerDownloadInfo.DownloadUrl = DOWNLOAD_URL_FOR_FUNCTIONAL_TESTS;
                    downloadProgressDlg.PerformWork(this, 50, () => SimpleFileDownloader.DownloadRequiredFiles(new[] { msFraggerDownloadInfo }, downloadProgressDlg));
                    return;
                }

                using (var client = new WebClient())
                {
                    client.UploadProgressChanged += (o, args) => downloadProgressDlg.ProgressValue = Math.Max(0, Math.Min(100, (args.ProgressPercentage - 50) * 2)); // ignore the upload part of the progress calculation

                    downloadProgressDlg.PerformWork(this, 50, () =>
                    {
                        var postData = new NameValueCollection();
                        postData[@"transfer"] = @"academic";
                        postData[@"agreement1"] = @"on";
                        postData[@"agreement2"] = @"on";
                        postData[@"agreement3"] = @"on";
                        postData[@"name"] = tbName.Text;
                        postData[@"email"] = tbEmail.Text;
                        postData[@"organization"] = tbInstitution.Text;
                        postData[@"download"] = $@"Release {MsFraggerSearchEngine.MSFRAGGER_VERSION}$zip";
                        if (cbReceiveUpdateEmails.Checked)
                            postData[@"receive_email"] = @"on";

                        // temporarily disable Expect100Continue to avoid (417) Expectation Failed
                        bool lastExpect100ContinueValue = ServicePointManager.Expect100Continue;
                        ServicePointManager.Expect100Continue = false;

                        var uploadTask = client.UploadValuesTaskAsync(DOWNLOAD_URL, DOWNLOAD_METHOD, postData);
                        var msFraggerZipBytes = uploadTask.Result;

                        ServicePointManager.Expect100Continue = lastExpect100ContinueValue;

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
                }
            }
        }

        public void ClickAccept() { btnAccept.PerformClick(); }

        private bool IsReadyToDownload => cbAgreeToLicense.Checked && tbName.TextLength > 0 && tbEmail.Text.IsValidEmail() && tbInstitution.TextLength > 0;

        private void btnAccept_Click(object sender, EventArgs e)
        {
            Download();

            DialogResult = DialogResult.OK;
        }

        public bool CheckCursorWithinRange(MouseEventArgs e, Tuple<int, int> range)
        {
            int charIndex = rtbAgreeToLicense.GetCharIndexFromPosition(e.Location);
            return range.Item1 <= charIndex && range.Item2 >= charIndex;
        }

        private void RtbAgreeToLicense_MouseMove(object sender, MouseEventArgs e)
        {
            if (CheckCursorWithinRange(e, licenseLinkStartEnd))
                Cursor = Cursors.Hand;
            else
                ResetCursor();
            base.OnMouseMove(e);
        }

        private void RtbAgreeToLicense_MouseClick(object sender, MouseEventArgs e)
        {
            if (CheckCursorWithinRange(e, licenseLinkStartEnd))
                WebHelpers.OpenLink(this, LICENSE_URL);
            else
                cbAgreeToLicense.Checked = !cbAgreeToLicense.Checked;
        }

        private void tbTextChanged(object sender, EventArgs e)
        {
            btnAccept.Enabled = IsReadyToDownload;
        }

        private void cbAgreeToLicense_CheckedChanged(object sender, EventArgs e)
        {
            btnAccept.Enabled = IsReadyToDownload;
        }
    }
}
