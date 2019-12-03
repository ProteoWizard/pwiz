/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI.Irt
{
    public enum AddIrtPeptidesAction { skip, replace, average }
    public enum AddIrtPeptidesLocation { irt_database, spectral_library }

    public partial class AddIrtPeptidesDlg : FormEx
    {
        private readonly Dictionary<DataGridViewRow, RegressionGraphData[]> _regressionGraphData;

        public AddIrtPeptidesDlg(AddIrtPeptidesLocation location, ProcessedIrtAverages processed)
            : this(location, processed, new Target[] { }, new Target[] { }, new Target[] { })
        {
        }

        public AddIrtPeptidesDlg(
            AddIrtPeptidesLocation location,
            ProcessedIrtAverages processed,
            IReadOnlyCollection<Target> existingPeptides,
            IReadOnlyCollection<Target> overwritePeptides,
            IReadOnlyCollection<Target> keepPeptides)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            _regressionGraphData = new Dictionary<DataGridViewRow, RegressionGraphData[]>();

            var successStyle = new DataGridViewCellStyle { BackColor = Color.LightGreen };
            var failStyle = new DataGridViewCellStyle { BackColor = Color.LightCoral };

            foreach (var data in processed.ProviderData)
            {
                var missingIndices = new HashSet<int>();
                var outlierIndices = new HashSet<int>();
                for (var i = 0; i < data.Peptides.Count; i++)
                {
                    if (data.Peptides[i].Missing)
                        missingIndices.Add(i);
                    else if (data.Peptides[i].Outlier)
                        outlierIndices.Add(i);
                }

                var graphData = new RegressionGraphData
                {
                    Title = data.RetentionTimeProvider.Name,
                    LabelX = Resources.AddIrtsResultsDlg_dataGridView_CellContentClick_Measured,
                    LabelY = Resources.AddIrtPeptidesDlg_dataGridView_CellContentClick_iRT,
                    XValues = data.Peptides.Select(peptide => peptide.RetentionTime.GetValueOrDefault()).ToArray(),
                    YValues = data.Peptides.Select(peptide => peptide.Irt).ToArray(),
                    Tooltips = Enumerable.Range(0, data.Peptides.Count).ToDictionary(i => i, i => data.Peptides[i].Target.ToString()),
                    MissingIndices = missingIndices,
                    OutlierIndices = outlierIndices,
                    RegressionLine = data.RegressionRefined,
                    RegressionLineCurrent = data.Regression,
                    RegressionName = data.RegressionSuccess
                        ? Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_Regression_Refined
                        : Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_Regression_Attempted,
                    ShowCurrentR = true,
                    MinR = RCalcIrt.MIN_IRT_TO_TIME_CORRELATION,
                    MinPoints = data.MinPoints
                };

                string filename;
                try
                {
                    filename = Path.GetFileName(data.RetentionTimeProvider.Name);
                }
                catch (Exception)
                {
                    filename = data.RetentionTimeProvider.Name;
                }
                dataGridView.Rows.Add(
                    filename,
                    graphData.RegularPoints.Count,
                    data.RegressionRefined != null ? data.RegressionRefined.Slope.ToString(@"F04") : string.Empty,
                    data.RegressionRefined != null ? data.RegressionRefined.Intercept.ToString(@"F04") : string.Empty,
                    graphData.R.ToString(@"F03"),
                    data.RegressionSuccess ? Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_Success : Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_Failed);
                var lastRow = dataGridView.Rows[dataGridView.RowCount - 1];
                lastRow.DefaultCellStyle = data.RegressionSuccess ? successStyle : failStyle;
                lastRow.Tag = data;

                _regressionGraphData[lastRow] = new[] {graphData};
            }

            PeptidesCount = processed.DbIrtPeptides.Count() - existingPeptides.Count - overwritePeptides.Count - keepPeptides.Count;
            RunsConvertedCount = processed.ProviderData.Count(data => data.RegressionSuccess);
            RunsFailedCount = processed.ProviderData.Count - RunsConvertedCount;

            string locationStr;
            switch (location)
            {
                default:
                    locationStr = Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_iRT_database;
                    break;
                case AddIrtPeptidesLocation.spectral_library:
                    locationStr = Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_spectral_library;
                    break;
            }

            if (PeptidesCount == 0)
                labelPeptidesAdded.Text = string.Format(Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_No_new_peptides_will_be_added_to_the__0__, locationStr);
            else if (PeptidesCount == 1)
                labelPeptidesAdded.Text = string.Format(Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_1_new_peptide_will_be_added_to_the__0__, locationStr);
            else
                labelPeptidesAdded.Text = string.Format(labelPeptidesAdded.Text, PeptidesCount, locationStr);

            if (RunsConvertedCount == 0)
                labelRunsConverted.Visible = false;
            else
            {
                labelRunsConverted.Text = RunsConvertedCount > 1
                                              ? string.Format(labelRunsConverted.Text, RunsConvertedCount)
                                              : Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_1_run_was_successfully_converted;
            }

            if (RunsFailedCount == 0)
                labelRunsFailed.Visible = false;
            else
            {
                labelRunsFailed.Text = RunsFailedCount > 1
                                           ? string.Format(labelRunsFailed.Text, RunsFailedCount)
                                           : Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_1_run_was_not_converted_due_to_insufficient_correlation;
            }
                
            listExisting.Items.AddRange(existingPeptides.Cast<object>().ToArray());
            listOverwrite.Items.AddRange(overwritePeptides.Cast<object>().ToArray());
            listKeep.Items.AddRange(keepPeptides.Cast<object>().ToArray());

            labelExisting.Text = string.Format(labelExisting.Text, listExisting.Items.Count);
            labelOverwrite.Text = string.Format(labelOverwrite.Text, listOverwrite.Items.Count);
            labelKeep.Text = string.Format(labelKeep.Text, listKeep.Items.Count);

            panelExisting.Anchor &= ~AnchorStyles.Bottom;
            if (!processed.ProviderData.Any())
            {
                dataGridView.Visible = false;
                panelOverwrite.Top -= dataGridView.Height;
                panelKeep.Top -= dataGridView.Height;
                panelExisting.Top -= dataGridView.Height;
                Height -= dataGridView.Height;
            }
            if (listOverwrite.Items.Count == 0)
            {
                panelOverwrite.Visible = false;
                panelKeep.Top -= panelOverwrite.Height;
                panelExisting.Top -= panelOverwrite.Height;
                Height -= panelOverwrite.Height;
            }
            if (listKeep.Items.Count == 0)
            {
                panelKeep.Visible = false;
                panelExisting.Top -= panelKeep.Height;
                Height -= panelKeep.Height;
            }
            panelExisting.Anchor |= AnchorStyles.Bottom;
            if (listExisting.Items.Count == 0)
            {
                panelExisting.Visible = false;
                Height -= panelExisting.Height;
            }

            if (!listOverwrite.Items.OfType<object>().Any() && !listKeep.Items.OfType<object>().Any() && !listExisting.Items.OfType<object>().Any())
            {
                if (processed.ProviderData.Any())
                    dataGridView.Anchor |= AnchorStyles.Bottom;
                else
                    FormBorderStyle = FormBorderStyle.FixedDialog;
            }
        }

        public int PeptidesCount { get; private set; }
        public int RunsConvertedCount { get; private set; }
        public int RunsFailedCount { get; private set; }
        public int KeepPeptidesCount { get { return listKeep.Items.Count; } }
        public int OverwritePeptidesCount { get { return listOverwrite.Items.Count; } }
        public int ExistingPeptidesCount { get { return listExisting.Items.Count; } }

        public AddIrtPeptidesAction Action
        {
            get
            {
                if (radioAverage.Checked)
                    return AddIrtPeptidesAction.average;
                if (radioReplace.Checked)
                    return AddIrtPeptidesAction.replace;
                return AddIrtPeptidesAction.skip;
            }

            set
            {
                switch (value)
                {
                    case AddIrtPeptidesAction.average:
                        radioAverage.Checked = true;
                        break;
                    case AddIrtPeptidesAction.replace:
                        radioReplace.Checked = true;
                        break;
                    default:
                        radioSkip.Checked = true;
                        break;
                }
            }
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private void dataGridView_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == dataGridView.ColumnCount - 1)
                ShowRegression(e.RowIndex);
        }

        public void ShowRegression(int rowIndex)
        {
            var row = GetRow(rowIndex);
            if (row == null)
                return;
            RegressionGraphData[] data;
            if (!_regressionGraphData.TryGetValue(row, out data))
                return;

            using (var graph = new GraphRegression(data) {Width = 800, Height = 600})
            {
                graph.ShowDialog(this);
            }
        }

        public bool IsConverted(int rowIndex)
        {
            if (0 > rowIndex || rowIndex >= dataGridView.RowCount)
                return false;
            var row = GetRow(rowIndex);
            if (row == null)
                return false;
            var data = row.Tag as RetentionTimeProviderData;
            return data != null && data.RegressionSuccess;
        }

        private DataGridViewRow GetRow(int rowIndex)
        {
            return 0 <= rowIndex && rowIndex < dataGridView.RowCount ? dataGridView.Rows[rowIndex] : null;
        }
    }
}
