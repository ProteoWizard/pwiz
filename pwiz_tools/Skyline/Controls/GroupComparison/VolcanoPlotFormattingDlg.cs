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
using pwiz.Skyline.Properties;
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

        private readonly DataGridViewComboBoxColumn _symbolCombo;
        private readonly DataGridViewComboBoxColumn _pointSizeCombo;

        public VolcanoPlotFormattingDlg(FoldChangeVolcanoPlot volcanoPlot, IList<MatchRgbHexColor> colorRows,
            FoldChangeBindingSource.FoldChangeRow[] foldChangeRows, Action<List<MatchRgbHexColor>> updateGraph)
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
            var expressionColumn = CreateDataGridViewColumn<DataGridViewTextBoxColumn>(false,
                @"Expression", // Column name not header text
                GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Expression,
                DataGridViewAutoSizeColumnMode.Fill);
            expressionColumn.Resizable = DataGridViewTriState.True;
            _expressionIndex = 0;
            regexColorRowGrid1.Columns.Insert(_expressionIndex, expressionColumn);

            // Match Expression Button Column
            var createExpressionBtn =
                CreateDataGridViewColumn<DataGridViewButtonColumn>(false, null, string.Empty,
                    DataGridViewAutoSizeColumnMode.None);
            createExpressionBtn.Resizable = DataGridViewTriState.False;
            createExpressionBtn.Text = @"...";
            createExpressionBtn.UseColumnTextForButtonValue = true;
            createExpressionBtn.Width = createExpressionBtn.MinimumWidth = 20;
            _createExprButtonIndex = 1;
            regexColorRowGrid1.Columns.Insert(_createExprButtonIndex, createExpressionBtn);

            // Symbol Combobox
            _symbolCombo = CreateDataGridViewColumn<DataGridViewComboBoxColumn>(false,
                @"PointSymbol", // Column name not header text
                GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Symbol, DataGridViewAutoSizeColumnMode.DisplayedCells);
            _symbolCombo.DisplayMember = @"DisplayString";
            _symbolCombo.ValueMember = @"PointSymbol";
            _symbolCombo.Items.AddRange(
                new PointSymbolStringPair(PointSymbol.Circle,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Circle),
                new PointSymbolStringPair(PointSymbol.Square,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Square),
                new PointSymbolStringPair(PointSymbol.Triangle,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Triangle),
                new PointSymbolStringPair(PointSymbol.TriangleDown,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_TriangleDown),
                new PointSymbolStringPair(PointSymbol.Diamond,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Diamond),
                new PointSymbolStringPair(PointSymbol.XCross,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_X_Cross),
                new PointSymbolStringPair(PointSymbol.Plus,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Plus),
                new PointSymbolStringPair(PointSymbol.Star,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Star)
            );
            regexColorRowGrid1.Columns.Insert(6, _symbolCombo);

            // Size Combobox
            _pointSizeCombo = CreateDataGridViewColumn<DataGridViewComboBoxColumn>(false,
                @"PointSize", // Column name not header text
                GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Size, DataGridViewAutoSizeColumnMode.DisplayedCells);
            _pointSizeCombo.DisplayMember = @"DisplayString";
            _pointSizeCombo.ValueMember = @"PointSize";
            _pointSizeCombo.Items.AddRange(
                new PointSizeStringPair(PointSize.x_small,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_X_Small),
                new PointSizeStringPair(PointSize.small,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Small),
                new PointSizeStringPair(PointSize.normal,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Normal),
                new PointSizeStringPair(PointSize.large,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Large),
                new PointSizeStringPair(PointSize.x_large,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_X_Large));
            regexColorRowGrid1.Columns.Insert(7, _pointSizeCombo);

            // Labeled Checkbox Column
            var labeledColumn = CreateDataGridViewColumn<DataGridViewCheckBoxColumn>(false,
                @"Labeled", // Column name not header text
                GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Labeled,
                DataGridViewAutoSizeColumnMode.AllCells);
            regexColorRowGrid1.Columns.Add(labeledColumn);

            advancedCheckBox.Checked = Settings.Default.ShowAdvancedVolcanoPlotFormatting;
            UpdateAdvancedColumns();

            regexColorRowGrid1.Owner = this;

            SetExpressionMinimumWidth();
        }

        public class PointSizeStringPair
        {
            public PointSizeStringPair(PointSize pointSize, string displayString)
            {
                PointSize = pointSize;
                DisplayString = displayString;
            }

            // These are actually used by the combo box
            // ReSharper disable once MemberCanBePrivate.Local
            public PointSize PointSize { get; set; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            // ReSharper disable once MemberCanBePrivate.Local
            public string DisplayString { get; set; }
        }

        public class PointSymbolStringPair
        {
            public PointSymbolStringPair(PointSymbol pointSymbol, string displayString)
            {
                PointSymbol = pointSymbol;
                DisplayString = displayString;
            }

            // These are actually used by the combo box
            // ReSharper disable once MemberCanBePrivate.Local
            public PointSymbol PointSymbol { get; set; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            // ReSharper disable once MemberCanBePrivate.Local
            public string DisplayString { get; set; }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _bindingList.ListChanged -= _bindingList_ListChanged;

            base.OnHandleDestroyed(e);
        }

        public FoldChangeVolcanoPlot VolcanoPlot { get; private set; }

        private void SetExpressionMinimumWidth()
        {
            var grid = regexColorRowGrid1.DataGridView;

            using (var g = grid.CreateGraphics())
            {
                var minimumWidth = 5.0f; // 5 is the default MinimumWidth

                foreach (var row in _bindingList)
                {
                    var width = g.MeasureString(row.Expression, grid.Font).Width;
                    minimumWidth = Math.Max(minimumWidth, width);
                }

                grid.Columns[_expressionIndex].MinimumWidth = (int)minimumWidth;
            }
        }

        private void _bindingList_ListChanged(object sender, ListChangedEventArgs e)
        {
            if (e.ListChangedType == ListChangedType.ItemAdded || e.ListChangedType == ListChangedType.ItemDeleted ||
                e.ListChangedType == ListChangedType.Reset ||
                e.PropertyDescriptor.DisplayName == @"Expression")
            {
                SetExpressionMinimumWidth();
            }

            _updateGraph(ResultList);
        }

        private void regexColorRowGrid1_OnCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == _createExprButtonIndex)
            {
                ClickCreateExpression(e.RowIndex);
            }
            else
            {
                // If the color button was clicked, this has already been handled in the color grid
                if (e.RowIndex == regexColorRowGrid1.DataGridView.NewRowIndex &&
                    e.ColumnIndex != regexColorRowGrid1.ButtonColumnIndex)
                    CommitCellChanges();
            }
        }

        private void CommitCellChanges()
        {
            var dataGridView = regexColorRowGrid1.DataGridView;
            regexColorRowGrid1.BindingSource.EndEdit();
            dataGridView.NotifyCurrentCellDirty(true);
            dataGridView.EndEdit();
            dataGridView.NotifyCurrentCellDirty(false);
        }


        public void ClickCreateExpression(int row)
        {
            if (row >= 0 && row < regexColorRowGrid1.DataGridView.RowCount)
            {
                using (var createMatchExpression =
                    new CreateMatchExpressionDlg(this, _foldChangeRows, _bindingList[row]))
                {
                    if (createMatchExpression.ShowDialog(this) == DialogResult.OK)
                    {
                        _bindingList[row].Expression = createMatchExpression.GetCurrentMatchExpression().ToString();
                        CommitCellChanges();
                    }
                }
            }
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        public new void CancelDialog()
        {
            DialogResult = DialogResult.Cancel;
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
                    throw new ArgumentOutOfRangeException(nameof(displayMode), displayMode, null);
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
            if (e.RowIndex < 0)
                return;

            if (e.ColumnIndex == _expressionIndex)
            {
                var grid = (DataGridView) sender;
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

                grid[e.ColumnIndex, e.RowIndex].Style.ForeColor = foreColor;
            }
        }

        public List<MatchRgbHexColor> ResultList
        {
            get { return _bindingList.ToList(); }
        }

        private T CreateDataGridViewColumn<T>(bool readOnly, string dataPropertyName, string headerText,
            DataGridViewAutoSizeColumnMode autoSizeMode) where T : DataGridViewColumn, new()
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

        private void UpdateAdvancedColumns()
        {
            _symbolCombo.Visible = _pointSizeCombo.Visible =
                Settings.Default.ShowAdvancedVolcanoPlotFormatting = advancedCheckBox.Checked;
        }

        private void advancedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            UpdateAdvancedColumns();
        }
    }
}
