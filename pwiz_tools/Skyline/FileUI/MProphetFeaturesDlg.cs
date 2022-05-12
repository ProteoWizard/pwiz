/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class MProphetFeaturesDlg : FormEx
    {
        public const string EXT = ".csv";

        private readonly IPeakFeatureCalculator[] _calculators;

        private SrmDocument Document { get; set; }
        private string DocumentFilePath { get; set; }

        public MProphetFeaturesDlg(SrmDocument document, string documentPath)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            Document = document;
            DocumentFilePath = documentPath;

            _calculators = PeakFeatureCalculator.Calculators.ToArray();
            foreach (var calculator in _calculators)
            {
                checkedListVars.Items.Add(calculator.Name);
                checkedListVars.SetItemChecked(checkedListVars.Items.Count - 1, true);
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private IPeakFeatureCalculator GetCalcFromName(string name)
        {
            return _calculators.First(c => string.Equals(c.Name, name));
        }

        public void OkDialog()
        {
            using (var dlg = new SaveFileDialog
            {
                Title = Resources.MProphetFeaturesDlg_OkDialog_Export_mProphet_Features,
                OverwritePrompt = true,
                DefaultExt = EXT,
                Filter = TextUtil.FileDialogFilterAll(Resources.MProphetFeaturesDlg_OkDialog_mProphet_Feature_Files, EXT),
            })
            {
                if (!string.IsNullOrEmpty(DocumentFilePath))
                {
                    dlg.InitialDirectory = Path.GetDirectoryName(DocumentFilePath);
                    dlg.FileName = Path.GetFileNameWithoutExtension(DocumentFilePath) + EXT;
                }
                if (dlg.ShowDialog(this) == DialogResult.Cancel)
                    return;

                var displayCalcs = new List<IPeakFeatureCalculator>();
                displayCalcs.AddRange(from object calcName in checkedListVars.CheckedItems select GetCalcFromName(calcName.ToString()));

                IPeakScoringModel currentPeakScoringModel = Document.Settings.PeptideSettings.Integration.PeakScoringModel;
                var mProphetScoringModel = currentPeakScoringModel as MProphetPeakScoringModel;
//                if (mProphetScoringModel == null)
//                {
//                    MessageDlg.Show(this, Resources.MProphetFeaturesDlg_OkDialog_To_export_MProphet_features_first_train_an_MProphet_model_);
//                    return;
//                }
                var resultsHandler = new MProphetResultsHandler(Document, mProphetScoringModel);

                using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.SkylineWindow_OpenSharedFile_Extracting_Files,
                })
                {
                    try
                    {
                        longWaitDlg.PerformWork(this, 1000, b =>
                                                            WriteFeatures(dlg.FileName,
                                                                          resultsHandler,
                                                                          new FeatureCalculators(displayCalcs),
                                                                          LocalizationHelper.CurrentCulture,
                                                                          checkBoxBestOnly.Checked,
                                                                          !checkBoxTargetsOnly.Checked,
                                                                          b));
                        if (longWaitDlg.IsCanceled)
                            return;
                    }
                    catch (Exception x)
                    {
                        var message = TextUtil.LineSeparate(string.Format(Resources.MProphetFeaturesDlg_OkDialog_Failed_attempting_to_save_mProphet_features_to__0__, dlg.FileName),
                                                                          x.Message);
                        MessageDlg.ShowWithException(this, message, x);
                    }
                }
            }

            DialogResult = DialogResult.OK;
        }

        public static void WriteFeatures(string filePath, 
                                         MProphetResultsHandler resultsHandler,
                                         FeatureCalculators calcs,
                                         CultureInfo cultureInfo,
                                         bool bestOnly,
                                         bool includeDecoys,
                                         IProgressMonitor progressMonitor)
        {
            using (var fs = new FileSaver(filePath))
            using (var writer = new StreamWriter(fs.SafeName))
            {
                resultsHandler.ScoreFeatures(progressMonitor);
                resultsHandler.WriteScores(writer, cultureInfo, calcs, bestOnly, includeDecoys, progressMonitor);
                writer.Close();
                fs.Commit();
            }
        }

        #region TestHelpers

        public bool BestScoresOnly
        {
            get { return checkBoxBestOnly.Checked; }
            set { checkBoxBestOnly.Checked = value; }
        }

        public bool TargetsOnly
        {
            get { return checkBoxTargetsOnly.Checked; }
            set { checkBoxTargetsOnly.Checked = value; }
        }

        #endregion
    }
}
