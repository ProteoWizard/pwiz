/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    public partial class ReintegrateDlg : FormEx
    {

        public SrmDocument Document { get; private set; }

        public ReintegrateDlg(SrmDocument document)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            Document = document;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            // TODO: Remove this
            var e = new CancelEventArgs();
            var helper = new MessageBoxHelper(this);

            double qCutoff = double.MaxValue;
            if (reintegrateQCutoff.Checked)
            {
                if (!helper.ValidateDecimalTextBox(e, textBoxCutoff, 0.0, 1.0, out qCutoff))
                    return;
            }

            using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.ReintegrateDlg_OkDialog_Reintegrating,
                })
            {
                try
                {
                    var scoringModel = Document.Settings.PeptideSettings.Integration.PeakScoringModel;
                    var resultsHandler = new MProphetResultsHandler(Document, scoringModel);
                    longWaitDlg.PerformWork(this, 1000, pm =>
                        {
                            resultsHandler.ScoreFeatures(pm);
                            if (resultsHandler.IsMissingScores())
                            {
                                throw new InvalidDataException(Resources.ReintegrateDlg_OkDialog_The_current_peak_scoring_model_is_incompatible_with_one_or_more_peptides_in_the_document___Please_train_a_new_model_);
                            }
                            Document = resultsHandler.ChangePeaks(qCutoff, pm);
                        });
                    if (longWaitDlg.IsCanceled)
                        return;
                }
                catch (Exception x)
                {
                    var message = TextUtil.LineSeparate(string.Format(Resources.ReintegrateDlg_OkDialog_Failed_attempting_to_reintegrate_peaks_),
                                                                      x.Message);
                    MessageDlg.Show(this, message);
                    return;
                }
            }
            DialogResult = DialogResult.OK;
        }

        #region TestHelpers

        public double Cutoff
        {
            get { return double.Parse(textBoxCutoff.Text); }
            set { textBoxCutoff.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public bool ReintegrateAll
        {
            get { return reintegrateAllPeaks.Checked; }
            set 
            { 
                reintegrateAllPeaks.Checked = value;
                reintegrateQCutoff.Checked = !value;
            }
        }

        #endregion

        private void reintegrateAllPeaks_CheckedChanged(object sender, EventArgs e)
        {
            textBoxCutoff.Enabled = !reintegrateAllPeaks.Checked;
        }

        private void reintegrateQCutoff_CheckedChanged(object sender, EventArgs e)
        {
            textBoxCutoff.Enabled = reintegrateQCutoff.Checked;
        }


    }
}
