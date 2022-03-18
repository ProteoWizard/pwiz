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
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    public partial class ReintegrateDlg : FormEx, IAuditLogModifier<ReintegrateDlg.ReintegrateDlgSettings>, IFeatureScoreProvider
    {
        /// <summary>
        /// For performance tests only: add an annotation for combined score?
        /// </summary>
        private bool _scoreAnnotation;

        // Cached scores
        private FeatureCalculators _cacheCalculators;

        private PeakTransitionGroupFeatureSet _cachedFeatureScores;

        public SrmDocument Document { get; private set; }

        private readonly SettingsListComboDriver<PeakScoringModelSpec> _driverPeakScoringModel;

        public ReintegrateDlg(SrmDocument document)
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            Document = document;
            _scoreAnnotation = false;
            _driverPeakScoringModel = new SettingsListComboDriver<PeakScoringModelSpec>(comboBoxScoringModel, Settings.Default.PeakScoringModelList);
            _driverPeakScoringModel.EditItemEvent += comboBoxScoringModel_EditItem;
            var peakScoringModel = document.Settings.PeptideSettings.Integration.PeakScoringModel;
            _driverPeakScoringModel.LoadList(peakScoringModel != null ? peakScoringModel.Name : null);
        }

        private void comboBoxScoringModel_EditItem(object sender, SettingsListComboDriver<PeakScoringModelSpec>.EditItemEventArgs e)
        {
            e.Tag = this;
        }

        public PeakTransitionGroupFeatureSet GetFeatureScores(SrmDocument document, IPeakScoringModel scoringModel,
            IProgressMonitor progressMonitor)
        {
            if (!ReferenceEquals(document, Document) ||
                !Equals(_cacheCalculators, scoringModel.PeakFeatureCalculators))
            {
                Document = document;
                _cacheCalculators = scoringModel.PeakFeatureCalculators;
                _cachedFeatureScores = document.GetPeakFeatures(_cacheCalculators, progressMonitor);
            }

            return _cachedFeatureScores;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            double qCutoff = double.MaxValue;
            if (reintegrateQCutoff.Checked)
            {
                if (!helper.ValidateDecimalTextBox(textBoxCutoff, 0.0, 1.0, out qCutoff))
                    return;
            }

            using (var longWaitDlg = new LongWaitDlg
                {
                    Text = Resources.ReintegrateDlg_OkDialog_Reintegrating,
                })
            {
                try
                {
                    var scoringModel = _driverPeakScoringModel.SelectedItem;
                    if (Equals(scoringModel, LegacyScoringModel.DEFAULT_UNTRAINED_MODEL))
                    {
                        scoringModel = LegacyScoringModel.DEFAULT_MODEL;
                    }
                    if (scoringModel == null || !scoringModel.IsTrained)
                    {
                        throw new InvalidDataException(Resources.ReintegrateDlg_OkDialog_You_must_train_and_select_a_model_in_order_to_reintegrate_peaks_);
                    }
                    PeakTransitionGroupFeatureSet featureScores = null;
                    if (Equals(_cacheCalculators, scoringModel.PeakFeatureCalculators))
                        featureScores = _cachedFeatureScores;
                    var resultsHandler = new MProphetResultsHandler(Document, scoringModel, featureScores)
                    {
                        QValueCutoff = qCutoff,
                        OverrideManual = checkBoxOverwrite.Checked,
                    };
                    longWaitDlg.PerformWork(this, 1000, pm =>
                        {
                            resultsHandler.ScoreFeatures(pm);
                            if (resultsHandler.IsMissingScores())
                            {
                                throw new InvalidDataException(Resources.ReintegrateDlg_OkDialog_The_current_peak_scoring_model_is_incompatible_with_one_or_more_peptides_in_the_document___Please_train_a_new_model_);
                            }
                            Document = resultsHandler.ChangePeaks(pm);
                        });
                    if (longWaitDlg.IsCanceled)
                        return;
                }
                catch (Exception x)
                {
                    var message = TextUtil.LineSeparate(string.Format(Resources.ReintegrateDlg_OkDialog_Failed_attempting_to_reintegrate_peaks_),
                                                                      x.Message);
                    MessageDlg.ShowWithException(this, message, x);
                    return;
                }
            }

            var newPeakScoringModel = _driverPeakScoringModel.SelectedItem;
            if (!Equals(newPeakScoringModel, Document.Settings.PeptideSettings.Integration.PeakScoringModel))
            {
                Document = Document.ChangeSettings(Document.Settings.ChangePeptideIntegration(
                    i => i.ChangePeakScoringModel(newPeakScoringModel)));
            }

            DialogResult = DialogResult.OK;
        }

        public ReintegrateDlgSettings FormSettings
        {
            get { return new ReintegrateDlgSettings(this); }
        }

        public class ReintegrateDlgSettings : AuditLogOperationSettings<ReintegrateDlgSettings>, IAuditLogComparable
        {
            public override MessageInfo MessageInfo
            {
                get { return new MessageInfo(MessageType.reintegrated_peaks, SrmDocument.DOCUMENT_TYPE.none, PeakScoringModel.Name); }
            }

            public ReintegrateDlgSettings(ReintegrateDlg dlg)
                : this(dlg._driverPeakScoringModel.SelectedItem, dlg.ReintegrateAll, !dlg.ReintegrateAll, dlg.QValueCutoff, dlg.OverwriteManual)
            {
            }

            public ReintegrateDlgSettings(IPeakScoringModel peakScoringModel, bool reintegrateAll, bool reintegrateQCutoff,
                double? cutoff, bool overwriteManualIntegration)
            {
                PeakScoringModel = peakScoringModel;
                ReintegrateAll = reintegrateAll;
                ReintegrateQCutoff = reintegrateQCutoff;
                Cutoff = cutoff;
                OverwriteManualIntegration = overwriteManualIntegration;
            }

            [TrackChildren]
            public IPeakScoringModel PeakScoringModel { get; private set; }

            [Track]
            public bool ReintegrateAll { get; private set; }
            [Track]
            public bool ReintegrateQCutoff { get; private set; }
            [Track]
            public double? Cutoff { get; private set; }
            [Track]
            public bool OverwriteManualIntegration { get; private set; }

            public object GetDefaultObject(ObjectInfo<object> info)
            {
                return new ReintegrateDlgSettings(null, false, false, null, false);
            }
        }

        public double? QValueCutoff
        {
            get
            {
                if (!ReintegrateAll)
                {
                    var helper = new MessageBoxHelper(this);
                    double result;
                    if (helper.ValidateDecimalTextBox(textBoxCutoff, 0.0, 1.0, out result))
                        return result;
                }

                return null;
            }
        }

        #region TestHelpers

        public double Cutoff
        {
            get { return double.Parse(textBoxCutoff.Text); }
            set { textBoxCutoff.Text = value.ToString(CultureInfo.CurrentCulture); }
        }

        public bool OverwriteManual
        {
            get { return checkBoxOverwrite.Checked; }
            set { checkBoxOverwrite.Checked = value; }
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

        public bool ScoreAnnotation
        {
            get { return _scoreAnnotation; }
            set { _scoreAnnotation = value; }
        }

        public void AddPeakScoringModel()
        {
            _driverPeakScoringModel.AddItem();
        }

        public void EditPeakScoringModel()
        {
            _driverPeakScoringModel.EditList();
        }

        public string ComboPeakScoringModelSelected
        {
            get { return comboBoxScoringModel.SelectedItem.ToString(); }
            set
            {
                foreach (var item in comboBoxScoringModel.Items)
                {
                    if (item.ToString() == value)
                    {
                        comboBoxScoringModel.SelectedItem = item;
                        return;
                    }
                }
                throw new InvalidDataException(Resources.EditPeakScoringModelDlg_SelectedModelItem_Invalid_Model_Selection);
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

        private void comboBoxScoringModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if ((_driverPeakScoringModel.EditCurrentSelected() || _driverPeakScoringModel.AddItemSelected()) &&
                    Document.Settings.MeasuredResults == null)
            {
                MessageDlg.Show(this, Resources.PeptideSettingsUI_comboPeakScoringModel_SelectedIndexChanged_The_document_must_have_imported_results_in_order_to_train_a_model_);
                return;
            }
            _driverPeakScoringModel.SelectedIndexChangedEvent(sender, e);
        }
    }
}
