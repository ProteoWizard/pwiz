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
            lblSummary.Text = string.Format(lblSummary.Text, DiannHelpers.DIANN_VERSION);
        }

        public void ClickAccept() { btnAccept.PerformClick(); }
        public void ClickSpecifyManually() { btnSpecifyManually.PerformClick(); }
        public bool AgreeToLicense { get => cbAgreeToLicense.Checked; set => cbAgreeToLicense.Checked = value; }

        private void cbAgreeToLicense_CheckedChanged(object sender, EventArgs e)
        {
            btnAccept.Enabled = cbAgreeToLicense.Checked;
        }

        private void btnAccept_Click(object sender, EventArgs e)
        {
            if (!DownloadAndExtract())
                return;
            DialogResult = DialogResult.OK;
        }

        private void btnSpecifyManually_Click(object sender, EventArgs e)
        {
            DialogResult = SpecifyManuallyResult;
        }

        private void linkLicense_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenLink(this, DiannHelpers.DIANN_LICENSE_URL.AbsoluteUri);
        }

        private bool DownloadAndExtract()
        {
            string msiPath = Path.Combine(Path.GetTempPath(),
                $@"DIA-NN-{DiannHelpers.DIANN_VERSION}-Academia.msi");
            try
            {
                using (var dlg = new LongWaitDlg())
                {
                    dlg.Message = string.Format(AlertsResources.DiannDownloadDlg_Downloading_DIA_NN__0_,
                        DiannHelpers.DIANN_VERSION);
                    var status = new ProgressStatus(dlg.Message);
                    var result = dlg.PerformWork(this, 50, broker =>
                    {
                        using var httpClient = new HttpClientWithProgress(broker, status);
                        httpClient.DownloadFile(DiannHelpers.DIANN_MSI_URL.AbsoluteUri, msiPath);
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
                        extractedBinary = DiannHelpers.ExtractDiannMsi(msiPath, DiannHelpers.DiannDirectory);
                    });
                }

                if (string.IsNullOrEmpty(extractedBinary) || !File.Exists(extractedBinary))
                {
                    MessageDlg.Show(this, AlertsResources.DiannDownloadDlg_Installation_failed);
                    return false;
                }

                // Remove any previous entry so we can write the freshly-extracted path.
                if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.DIANN))
                    Settings.Default.SearchToolList.Remove(Settings.Default.SearchToolList[SearchToolType.DIANN]);
                Settings.Default.SearchToolList.Add(new SearchTool(SearchToolType.DIANN,
                    extractedBinary, string.Empty, DiannHelpers.DiannDirectory, true));
                return true;
            }
            catch (Exception ex)
            {
                ExceptionUtil.DisplayOrReportException(this, ex);
                return false;
            }
            finally
            {
                if (File.Exists(msiPath))
                    FileEx.SafeDelete(msiPath, true);
            }
        }
    }
}
