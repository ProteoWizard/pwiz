/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.IO;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class RescoreResultsDlg : FormEx
    {
        public RescoreResultsDlg(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();

            DocumentUIContainer = documentUIContainer;

            labelMessage.Text =
                Resources.RescoreResultsDlg_RescoreResultsDlg_In_certain_cases__you_may_want_to_have_Skyline_re_caclculate_peaks_and_re_score_them_based_on_the_existing_chromatogram_data___Chromatograms_will_not_be_re_imported_from_raw_data_files__but_peak_integration_information_may_change_;
        }

        public IDocumentUIContainer DocumentUIContainer { get; private set; }

        private void btnRescore_Click(object sender, System.EventArgs e)
        {
            Rescore(false);
        }

        private void btnRescoreAs_Click(object sender, System.EventArgs e)
        {
            Rescore(true);
        }

        public void Rescore(bool asNewFile)
        {
            var document = DocumentUIContainer.DocumentUI;
            if (!document.Settings.HasResults)
            {
                MessageDlg.Show(this, Resources.RescoreResultsDlg_Rescore_There_are_not_results_in_this_document);
                return;
            }
            if (!document.Settings.MeasuredResults.IsLoaded)
            {
                MessageDlg.Show(this, Resources.RescoreResultsDlg_Rescore_All_results_must_be_completely_imported_before_they_can_be_re_scored_);
                return;
            }

            string targetFile = DocumentUIContainer.DocumentFilePath;

            if (asNewFile || string.IsNullOrEmpty(targetFile))
            {
                using (var saveFileDialog =
                    new SaveFileDialog
                    {
                        InitialDirectory = Settings.Default.ActiveDirectory,
                        OverwritePrompt = true,
                        DefaultExt = SrmDocument.EXT,
                        Filter = TextUtil.FileDialogFiltersAll(SrmDocument.FILTER_DOC),
                        FileName = Path.GetFileName(targetFile),
                    })
                {
                    if (saveFileDialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return;
                    }
                    targetFile = saveFileDialog.FileName;
                }
            }
            RescoreToFile(targetFile);
        }

        private void RescoreToFile(string targetFile)
        {
            var skylineWindow = (SkylineWindow)DocumentUIContainer;
            if (!skylineWindow.SaveDocument(targetFile, false))
            {
                return;
            }

            // CONSIDER: Not possible to undo this operation.  Not sure this is the right way to handle it.
            //           May need to clear the Undo stack.
            SrmDocument doc, docNew;
            do
            {
                doc = DocumentUIContainer.Document;
                docNew = doc.ChangeSettingsNoDiff(doc.Settings.ChangeMeasuredResults(
                    doc.Settings.MeasuredResults.ChangeRecalcStatus()));
            }
            while (!skylineWindow.SetDocument(docNew, doc));

            DialogResult = DialogResult.OK;
        }
    }
}
