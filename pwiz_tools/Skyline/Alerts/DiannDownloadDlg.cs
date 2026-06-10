/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.IO;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Alerts
{
    public partial class DiannDownloadDlg : FormEx
    {
        /// <summary>
        /// The DialogResult returned when the user clicks "Specify Path Manually".
        /// Callers should then open Edit &gt; Search Tools to let the user set the path.
        /// </summary>
        public const DialogResult SpecifyManuallyResult = DialogResult.Retry;

        public DiannDownloadDlg()
        {
            InitializeComponent();
            lblSummary.Text = string.Format(lblSummary.Text,
                DiannHelpers.DIANN_VERSION, DiannHelpers.DIANN_191_VERSION);

            // linkLabel2x.Text is a 2-arg format: "DIA-NN {0} ({1})". {0} is the version,
            // {1} is the link phrase resource. Substituting the phrase ourselves means
            // IndexOf finds the exact string we just inserted — no case-sensitivity or
            // localization fragility.
            string linkPhrase = AlertsResources.DiannDownloadDlg_academic_license;
            linkLabel2x.Text = string.Format(linkLabel2x.Text,
                DiannHelpers.DIANN_VERSION, linkPhrase);
            int linkStart = linkLabel2x.Text.IndexOf(linkPhrase, StringComparison.Ordinal);
            if (linkStart >= 0)
                linkLabel2x.LinkArea = new LinkArea(linkStart, linkPhrase.Length);

            // Clicking the non-link portion of the label (the "DIA-NN 2.5.x (" and ")"
            // parts) selects the matching radio. The link-text click still fires
            // LinkClicked, not Click, so the URL opener is untouched.
            linkLabel2x.Click += (s, e) => rb2x.Checked = true;

            rb191.Text = string.Format(rb191.Text, DiannHelpers.DIANN_191_VERSION);
            rb2x.Checked = true;
        }

        public void ClickAccept() { btnAccept.PerformClick(); }
        public void ClickSpecifyManually() { btnSpecifyManually.PerformClick(); }
        public bool AgreeToLicense { get => cbAgreeToLicense.Checked; set => cbAgreeToLicense.Checked = value; }

        /// <summary>Test hook: select the open-license 1.9.1 radio.</summary>
        public void SelectOpenLicenseVersion() { rb191.Checked = true; }

        private void versionRadio_CheckedChanged(object sender, EventArgs e)
        {
            // 1.9.1 carries no separate license to agree to; gray out the checkbox and
            // allow Accept without it. For 2.5.x the checkbox is the gating factor.
            cbAgreeToLicense.Enabled = rb2x.Checked;
            UpdateAcceptEnabled();
        }

        private void cbAgreeToLicense_CheckedChanged(object sender, EventArgs e)
        {
            UpdateAcceptEnabled();
        }

        private void UpdateAcceptEnabled()
        {
            btnAccept.Enabled = rb191.Checked || (rb2x.Checked && cbAgreeToLicense.Checked);
        }

        private void btnAccept_Click(object sender, EventArgs e)
        {
            bool ok = rb191.Checked
                ? DownloadAndExtract(DiannHelpers.DIANN_191_VERSION, DiannHelpers.DIANN_191_ZIP_URL,
                                     DiannHelpers.Diann191Directory,
                                     (path, dir) => DiannHelpers.ExtractDiannZip(path, dir),
                                     isMsi: false)
                : DownloadAndExtract(DiannHelpers.DIANN_VERSION, DiannHelpers.DIANN_MSI_URL,
                                     DiannHelpers.DiannDirectory,
                                     (path, dir) => DiannHelpers.ExtractDiannMsi(path, dir),
                                     isMsi: true);
            if (!ok)
                return;
            DialogResult = DialogResult.OK;
        }

        private void btnSpecifyManually_Click(object sender, EventArgs e)
        {
            DialogResult = SpecifyManuallyResult;
        }

        private void linkLabel2x_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(this, DiannHelpers.DIANN_LICENSE_URL.AbsoluteUri);
        }

        private bool DownloadAndExtract(string version, Uri downloadUrl, string installDir,
                                        Func<string, string, string> extractor, bool isMsi)
        {
            string downloadPath = Path.Combine(Path.GetTempPath(),
                isMsi ? $@"DIA-NN-{version}-Academia.msi"
                      : $@"DIA-NN-{version}-binaries.zip");
            try
            {
                using (var dlg = new LongWaitDlg())
                {
                    dlg.Message = string.Format(AlertsResources.DiannDownloadDlg_Downloading_DIA_NN__0_, version);
                    var status = new ProgressStatus(dlg.Message);
                    var result = dlg.PerformWork(this, 50, broker =>
                    {
                        using var httpClient = new HttpClientWithProgress(broker, status);
                        httpClient.DownloadFile(downloadUrl.AbsoluteUri, downloadPath);
                    });
                    if (result.IsCanceled)
                        return false;
                }

                string extractedBinary = null;
                using (var dlg = new LongWaitDlg())
                {
                    dlg.Message = AlertsResources.DiannDownloadDlg_Installing_DIA_NN;
                    dlg.PerformWork(this, 50, () =>
                    {
                        extractedBinary = extractor(downloadPath, installDir);
                    });
                }

                if (string.IsNullOrEmpty(extractedBinary) || !File.Exists(extractedBinary))
                {
                    MessageDlg.Show(this, AlertsResources.DiannDownloadDlg_Installation_failed);
                    return false;
                }

                // If an unrelated DIA-NN install is already registered, confirm with
                // the user before overwriting it. Same install_dir means the user is
                // reinstalling the same version, which is silent. The Remove/Add below
                // does not delete the other install's files; it only flips which one
                // Skyline drives.
                if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.DIANN))
                {
                    var existing = Settings.Default.SearchToolList[SearchToolType.DIANN];
                    if (!string.IsNullOrEmpty(existing.InstallPath) &&
                        !PathEx.SamePath(existing.InstallPath, installDir))
                    {
                        var msg = string.Format(
                            AlertsResources.DiannDownloadDlg_Replace_existing_registration__0__with__1__,
                            existing.Path, version);
                        if (MultiButtonMsgDlg.Show(this, msg, MultiButtonMsgDlg.BUTTON_YES, MultiButtonMsgDlg.BUTTON_NO, false)
                            != DialogResult.Yes)
                        {
                            return false;
                        }
                    }
                    Settings.Default.SearchToolList.Remove(existing);
                }
                Settings.Default.SearchToolList.Add(new SearchTool(SearchToolType.DIANN,
                    extractedBinary, string.Empty, installDir, true));
                return true;
            }
            catch (Exception ex)
            {
                ExceptionUtil.DisplayOrReportException(this, ex);
                return false;
            }
            finally
            {
                if (File.Exists(downloadPath))
                    FileEx.SafeDelete(downloadPath, true);
            }
        }
    }
}
