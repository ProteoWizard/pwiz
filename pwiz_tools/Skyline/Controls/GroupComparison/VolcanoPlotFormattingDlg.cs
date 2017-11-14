/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class VolcanoPlotFormattingDlg : FormEx, ColorGrid<MatchRgbHexColor>.IColorGridOwner
    {
        private readonly FoldChangeBindingSource.FoldChangeRow[] _foldChangeRows;
        private readonly Action<List<MatchRgbHexColor>> _updateGraph;
        private readonly BindingList<MatchRgbHexColor> _bindingList;
        private readonly int _expressionIndex;
        private readonly int _createExprButtonIndex;

        public VolcanoPlotFormattingDlg(FoldChangeVolcanoPlot volcanoPlot, IList<MatchRgbHexColor> colorRows, FoldChangeBindingSource.FoldChangeRow[] foldChangeRows, Action<List<MatchRgbHexColor>> updateGraph)
        {
            InitializeComponent();

            VolcanoPlot = volcanoPlot;
            _foldChangeRows = foldChangeRows;
            _updateGraph = updateGraph;
            _bindingList = new BindingList<MatchRgbHexColor>(colorRows);
            _bindingList.ListChanged += _bindingList_ListChanged;

            regexColorRowGrid1.AllowUserToOrderColumns = true;
            regexColorRowGrid1.AllowUserToAddRows = true;

            // Match Expression Textbox Column
            var expressionColumn = CreateDataGridViewColumn<DataGridViewTextBoxColumn>(false, "Expression", // Not L10N: column name not header text
                GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Expression, DataGridViewAutoSizeColumnMode.Fill);
            expressionColumn.Resizable = DataGridViewTriState.True;
            _expressionIndex = 0;
            regexColorRowGrid1.Columns.Insert(_expressionIndex, expressionColumn);

            // Create Match Expression Button Column
            var createExpressionBtn = CreateDataGridViewColumn<DataGridViewButtonColumn>(false, null, string.Empty, DataGridViewAutoSizeColumnMode.None);
            createExpressionBtn.Resizable = DataGridViewTriState.False;
            createExpressionBtn.Text = "..."; // Not L10N
            createExpressionBtn.UseColumnTextForButtonValue = true;
            createExpressionBtn.Width = createExpressionBtn.MinimumWidth = 20;
            _createExprButtonIndex = 1;
            regexColorRowGrid1.Columns.Insert(_createExprButtonIndex, createExpressionBtn);

            // Labeled Checkbox Column
            var labeledColumn = CreateDataGridViewColumn<DataGridViewCheckBoxColumn>(false, "Labeled", // Not L10N: column name not header text
                GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Labeled, DataGridViewAutoSizeColumnMode.AllCells);
            regexColorRowGrid1.Columns.Add(labeledColumn);

            regexColorRowGrid1.Owner = this;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {    
            _bindingList.ListChanged -= _bindingList_ListChanged;

            base.OnHandleDestroyed(e);
        }

        public FoldChangeVolcanoPlot VolcanoPlot { get; private set; }

        void _bindingList_ListChanged(object sender, ListChangedEventArgs e)
        {
            _updateGraph(ResultList);
        }

        private void regexColorRowGrid1_OnCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == _createExprButtonIndex)
            {
                using (var createMatchExpression = new CreateMatchExpressionDlg(this, _foldChangeRows, _bindingList[e.RowIndex]))
                {
                    if (createMatchExpression.ShowDialog() == DialogResult.OK)
                        _bindingList[e.RowIndex].Expression = createMatchExpression.GetCurrentMatchExpression().ToString();
                    else
                        return;
                }
            }

            //If the color button was clicked, this has already been handled in the color grid
            var dataGridView = (DataGridView) sender;
            if (e.RowIndex == dataGridView.NewRowIndex && e.ColumnIndex != regexColorRowGrid1.ButtonColumnIndex)
            {
                regexColorRowGrid1.BindingSource.EndEdit();
                dataGridView.NotifyCurrentCellDirty(true);
                dataGridView.EndEdit();
                dataGridView.NotifyCurrentCellDirty(false);
            }
        }

        private static MatchOption DisplayModeToMatchOption(ProteinMetadataManager.ProteinDisplayMode displayMode)
        {
            switch (displayMode)
            {
                case ProteinMetadataManager.ProteinDisplayMode.ByName:
                    return MatchOption.ProteinName;
                case ProteinMetadataManager.ProteinDisplayMode.ByAccession:
                    return MatchOption.ProteinAccession;
                case ProteinMetadataManager.ProteinDisplayMode.ByPreferredName:
                    return MatchOption.ProteinPreferredName;
                case ProteinMetadataManager.ProteinDisplayMode.ByGene:
                    return MatchOption.ProteinGene;
                default:
                    throw new ArgumentOutOfRangeException("displayMode", displayMode, null); // Not L10N
            }
        }

        public MatchExpression GetDefaultMatchExpression(string regex)
        {
            MatchOption? matchOption = null;
            if (VolcanoPlot.PerProtein)
            {
                if (VolcanoPlot.AnyProteomic)
                {
                    matchOption = DisplayModeToMatchOption(SequenceTree.ProteinsDisplayMode);
                }
                else if (VolcanoPlot.AnyMolecules)
                {
                    matchOption = MatchOption.MoleculeGroupName;
                }
            }
            else
            {
                if (VolcanoPlot.AnyProteomic)
                {
                    matchOption = MatchOption.PeptideSequence;
                }
                else if (VolcanoPlot.AnyMolecules)
                {
                    matchOption = MatchOption.MoleculeName;
                }
            }

            return matchOption.HasValue
                ? new MatchExpression(regex, new[] {matchOption.Value})
                : new MatchExpression(regex, new MatchOption[] { });
        }

        private void regexColorRowGrid1_OnCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == _expressionIndex)
            {
                if (e.RowIndex >= 0)
                {
                    var row = _bindingList[e.RowIndex];

                    var foreColor = Color.Black;

                    // If the MatchExpression is null, parsing failed, so we create a default expression where
                    // the regular expression is the Expression that failed to parse and the matchOptions are default ones (see GetDefaultMatchExpression)
                    if (row.MatchExpression == null)
                    {
                        if (!row.InvalidMatchOptions && MatchExpression.IsRegexValid(row.Expression))
                            row.Expression = GetDefaultMatchExpression(row.Expression).ToString();
                        else
                            foreColor = Color.Red;
                    }

                    ((DataGridView) sender)[e.ColumnIndex, e.RowIndex].Style.ForeColor = foreColor;

                }
            }  
        }

        public List<MatchRgbHexColor> ResultList
        {
            get { return _bindingList.ToList(); }
        }

        private T CreateDataGridViewColumn<T>(bool readOnly, string dataPropertyName, string headerText, DataGridViewAutoSizeColumnMode autoSizeMode) where T : DataGridViewColumn, new()
        {
            var column = new T
            {
                ReadOnly = readOnly,
                DataPropertyName = dataPropertyName,
                HeaderText = headerText,
                AutoSizeMode = autoSizeMode
            };

            return column;
        }

        public BindingList<MatchRgbHexColor> GetCurrentBindingList()
        {
            return _bindingList;
        }
    }
}