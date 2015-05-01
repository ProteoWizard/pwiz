/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.EditUI
{
    public partial class AddPeakCompareDlg : FormEx
    {
        public SrmDocument Document { get; private set; }
        public ComparePeakBoundaries BoundaryComparer { get; private set; }

        private readonly SettingsListComboDriver<PeakScoringModelSpec> _driverPeakScoringModel;
        private readonly IEnumerable<ComparePeakBoundaries> _existing;
        private readonly ComparePeakBoundaries _current;

        public AddPeakCompareDlg(SrmDocument document, ComparePeakBoundaries current, IEnumerable<ComparePeakBoundaries> existing)
            : this(document, existing)
        {
            // If second argument is null, treat it as if it wasn't there -- for EditListDlg
            if (current == null)
                return;
            _current = current;
            radioButtonModel.Checked = current.IsModel;
            radioButtonFile.Checked = !current.IsModel;
            textFilePath.Text = current.FilePath;
            textName.Text = current.FileName;
            var model = current.PeakScoringModel;
            _driverPeakScoringModel.LoadList(model != null ? model.Name : null);
        }

        public AddPeakCompareDlg(SrmDocument document, IEnumerable<ComparePeakBoundaries> existing)
        {
            InitializeComponent();
            Document = document;
            _existing = existing;
            _driverPeakScoringModel = new SettingsListComboDriver<PeakScoringModelSpec>(comboBoxModel, Settings.Default.PeakScoringModelList);
            var peakScoringModel = document.Settings.PeptideSettings.Integration.PeakScoringModel;
            _driverPeakScoringModel.LoadList(peakScoringModel != null ? peakScoringModel.Name : null);
            UpdateSelection(true);
        }

        private void modelComparisonBtn_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSelection(radioButtonModel.Checked);
        }

        private void radioButtonFile_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSelection(radioButtonModel.Checked);
        }

        public void UpdateSelection(bool isModel)
        {
            textName.Enabled = !isModel;
            textFilePath.Enabled = !isModel;
            btnBrowse.Enabled = !isModel;
            comboBoxModel.Enabled = isModel;
        }

        private void comboBoxModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if ((_driverPeakScoringModel.EditCurrentSelected() || _driverPeakScoringModel.AddItemSelected()) &&
                    Document.Settings.MeasuredResults == null)
            {
                MessageDlg.Show(this, Resources.PeptideSettingsUI_comboPeakScoringModel_SelectedIndexChanged_The_document_must_have_imported_results_in_order_to_train_a_model_);
                return;
            }
            _driverPeakScoringModel.SelectedIndexChangedEvent(sender, e);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            bool isModel = radioButtonModel.Checked;
            if (isModel)
            {
                var model = _driverPeakScoringModel.SelectedItem;
                if (model == null)
                {
                    MessageDlg.Show(this, Resources.PeakBoundaryCompareTest_DoTest_Must_select_a_model_for_comparison_);
                    return;
                }
                if (!model.IsTrained)
                {
                    MessageDlg.Show(this, Resources.AddPeakCompareDlg_OkDialog_Model_must_be_trained_before_it_can_be_used_for_peak_boundary_comparison_);
                    return;
                }
                BoundaryComparer = new ComparePeakBoundaries(model);
            }
            else
            {
                string displayName = textName.Text;
                if (displayName.Length == 0)
                {
                    MessageDlg.Show(this, Resources.AddPeakCompareDlg_OkDialog_Comparison_name_cannot_be_empty_);
                    return;
                }
                string filePath = textFilePath.Text;
                if (filePath.Length == 0)
                {
                    MessageDlg.Show(this, Resources.AddPeakCompareDlg_OkDialog_File_path_cannot_be_empty_);
                    return;
                }
                if (!File.Exists(filePath))
                {
                    MessageDlg.Show(this, Resources.AddPeakCompareDlg_OkDialog_File_path_field_must_contain_a_path_to_a_valid_file_);
                    return;
                }
                BoundaryComparer = new ComparePeakBoundaries(displayName, filePath);
            }
            var compNames = _existing.Select(comp => comp.Name);
            if (compNames.Contains(BoundaryComparer.Name) && (_current == null || _current.Name != BoundaryComparer.Name))
            {
                var message = isModel
                    ? Resources.AddPeakCompareDlg_OkDialog_The_selected_model_is_already_included_in_the_list_of_comparisons__Please_choose_another_model_
                    : Resources.AddPeakCompareDlg_OkDialog_There_is_already_an_imported_file_with_the_current_name___Please_choose_another_name;
                MessageDlg.Show(this, message);
                return;
            }
            using (var longWaitDlg = new LongWaitDlg
            {
                Text = isModel ? Resources.AddPeakCompareDlg_OkDialog_Comparing_Models : Resources.AddPeakCompareDlg_OkDialog_Comparing_Imported_Files
            })
            {
                try
                {
                    longWaitDlg.PerformWork(this, 1000, pm => BoundaryComparer.GenerateComparison(Document, pm));
                    if (BoundaryComparer.Matches.Count == 0)
                    {
                        throw new IOException(Resources.AddPeakCompareDlg_OkDialog_Document_has_no_eligible_chromatograms_for_analysis___Valid_chromatograms_must_not_be_decoys_or_iRT_standards_);
                    }
                    if (BoundaryComparer.Matches.All(match => match.IsMissingPickedPeak))
                    {
                        throw new IOException(Resources.AddPeakCompareDlg_OkDialog_The_selected_file_or_model_does_not_assign_peak_boundaries_to_any_chromatograms_in_the_document___Please_select_a_different_model_or_file_);
                    }
                    if (BoundaryComparer.HasNoQValues && BoundaryComparer.HasNoScores)
                    {
                        throw new IOException(Resources.AddPeakCompareDlg_OkDialog_The_current_file_or_model_has_no_q_values_or_scores_to_analyze___Either_q_values_or_scores_are_necessary_to_compare_peak_picking_tools_);
                    }
                    if (BoundaryComparer.CountMissing > 0)
                    {
                        var missingMessage = string.Format(Resources.AddPeakCompareDlg_OkDialog_The_imported_file_does_not_contain_any_peak_boundaries_for__0__transition_group___file_pairs___These_chromatograms_will_be_treated_as_if_no_boundary_was_selected_,
                            BoundaryComparer.CountMissing);
                        var dlgMissing = MultiButtonMsgDlg.Show(this, missingMessage, MultiButtonMsgDlg.BUTTON_OK);
                        if (dlgMissing == DialogResult.Cancel)
                            return;
                    }
                    // Show a warning message and give a chance to cancel, if there are unrecognized peptides
                    if (BoundaryComparer.Importer != null && !BoundaryComparer.Importer.UnrecognizedPeptidesCancel(this))
                    {
                        return;
                    }
                }
                catch (Exception x)
                {
                    string initMessage = isModel
                        ? Resources.AddPeakCompareDlg_OkDialog_Error_comparing_model_peak_boundaries___0_
                        : Resources.AddPeakCompareDlg_OkDialog_Error_applying_imported_peak_boundaries___0_;
                    MessageDlg.ShowWithException(this, string.Format(initMessage, x.Message), x);
                    return;
                }
            }
            DialogResult = DialogResult.OK;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            Browse();
        }

        public void Browse()
        {
            using (OpenFileDialog dlg = new OpenFileDialog
            {
                Title = Resources.CreateIrtCalculatorDlg_ImportTextFile_Import_Transition_List__iRT_standards_,
                InitialDirectory = Settings.Default.ActiveDirectory,
                DefaultExt = TextUtil.EXT_CSV,
                Filter = TextUtil.FileDialogFiltersAll(TextUtil.FileDialogFilter(
                    Resources.SkylineWindow_importMassListMenuItem_Click_Transition_List, TextUtil.EXT_CSV, TextUtil.EXT_TSV))
            })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    Settings.Default.ActiveDirectory = Path.GetDirectoryName(dlg.FileName);
                    textFilePath.Text = dlg.FileName;
                    textFilePath.Focus();
                }
            }
        }

        #region Functional test support

        public bool IsModel
        {
            get { return radioButtonModel.Checked; }
            set
            {
                radioButtonModel.Checked = value;
                radioButtonFile.Checked = !value;
                UpdateSelection(value);
            }
        }

        public string FileName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public string FilePath
        {
            get { return textFilePath.Text; }
            set { textFilePath.Text = value; }
        }

        public string PeakScoringModelSelected
        {
            get { return comboBoxModel.SelectedItem.ToString(); }
            set
            {
                foreach (var item in comboBoxModel.Items)
                {
                    if (item.ToString() == value)
                    {
                        comboBoxModel.SelectedItem = item;
                        return;
                    }
                }
                throw new InvalidDataException(Resources.EditPeakScoringModelDlg_SelectedModelItem_Invalid_Model_Selection);
            }
        }

        public void AddPeakScoringModel()
        {
            _driverPeakScoringModel.AddItem();
        }

        public void EditPeakScoringModel()
        {
            _driverPeakScoringModel.EditList();
        }

        #endregion
    }
}
