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
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class VolcanoPlotFormattingDlg : FormEx, ColorGrid<MatchRgbHexColor>.IColorGridOwner
    {
        private readonly object[] _foldChangeRows;
        private readonly Action<IEnumerable<MatchRgbHexColor>> _updateGraph;
        private readonly BindingList<MatchRgbHexColor> _bindingList;

        private readonly int _expressionIndex;
        private readonly int _createExprButtonIndex;

        private readonly DataGridViewComboBoxColumn _symbolCombo;
        private readonly DataGridViewComboBoxColumn _pointSizeCombo;
        private readonly Font _symbolDropdownFont = new Font(SystemFonts.DefaultFont.FontFamily, 16f);

        public VolcanoPlotFormattingDlg(FoldChangeVolcanoPlot volcanoPlot, IList<MatchRgbHexColor> colorRows,
            FoldChangeRow[] foldChangeRows, Action<IEnumerable<MatchRgbHexColor>> updateGraph) : 
            this(true, colorRows, foldChangeRows, updateGraph, 
                volcanoPlot.PerProtein, volcanoPlot.Document)
        {
        }

        public VolcanoPlotFormattingDlg(SummaryRelativeAbundanceGraphPane relativeAbundanceGraph,
            IEnumerable<MatchRgbHexColor> colorRows, object[] proteinAbundances, Action<IEnumerable<MatchRgbHexColor>> updateGraph) : 
            this(false, colorRows, proteinAbundances, updateGraph, 
                Settings.Default.AreaProteinTargets, relativeAbundanceGraph.GraphSummary.DocumentUIContainer.DocumentUI)
        {
        }

        private VolcanoPlotFormattingDlg(bool hasFoldChangeResults, IEnumerable<MatchRgbHexColor> colorRows,
            object[] foldChangeRows, Action<IEnumerable<MatchRgbHexColor>> updateGraph, bool perProtein, SrmDocument document)
        {
            InitializeComponent();
            HasFoldChangeResults = hasFoldChangeResults;
            AnyMolecules = document.HasSmallMolecules;
            AnyProteomic = document.IsEmptyOrHasPeptides;
            PerProtein = perProtein;
            Document = document;

            _foldChangeRows = foldChangeRows;
            _updateGraph = updateGraph;

            _bindingList = new BindingList<MatchRgbHexColor>(colorRows.Select(row=>row.Clone()).ToList());
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
                new PointSymbolStringPair(null,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_None),
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
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Star),
                new PointSymbolStringPair(PointSymbol.OutlineCircle,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_OutlineCircle),
                new PointSymbolStringPair(PointSymbol.OutlineSquare,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_OutlineSquare),
                new PointSymbolStringPair(PointSymbol.OutlineTriangle,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_OutlineTriangle),
                new PointSymbolStringPair(PointSymbol.OutlineTriangleDown,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_OutlineTriangleDown),
                new PointSymbolStringPair(PointSymbol.OutlineDiamond,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_OutlineDiamond)
            );
            regexColorRowGrid1.Columns.Insert(6, _symbolCombo);

            // Size Combobox
            _pointSizeCombo = CreateDataGridViewColumn<DataGridViewComboBoxColumn>(false,
                @"PointSize", // Column name not header text
                GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Size, DataGridViewAutoSizeColumnMode.DisplayedCells);
            _pointSizeCombo.DisplayMember = @"DisplayString";
            _pointSizeCombo.ValueMember = @"PointSize";
            _pointSizeCombo.Items.AddRange(
                new PointSizeStringPair(null,
                    GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_None),
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

            regexColorRowGrid1.AddUseColorColumn(GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Color);
            regexColorRowGrid1.Owner = this;
            regexColorRowGrid1.DataGridView.EditingControlShowing += DataGridView_EditingControlShowing;
            if (!hasFoldChangeResults)
            {
                Text = GroupComparisonResources.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_Protein_Expression_Formatting;
            }
            SetExpressionMinimumWidth();
            layoutLabelsBox.Checked = Settings.Default.GroupComparisonAvoidLabelOverlap;

            // Tooltips/accessible names for the delete/reorder toolbar (image-only buttons).
            btnDeleteRule.Text = GroupComparisonStrings.VolcanoPlotFormattingDlg_Delete_rule;
            btnMoveRuleUp.Text = GroupComparisonStrings.VolcanoPlotFormattingDlg_Move_rule_up;
            btnMoveRuleDown.Text = GroupComparisonStrings.VolcanoPlotFormattingDlg_Move_rule_down;

            var grid = regexColorRowGrid1.DataGridView;
            grid.SelectionChanged += regexColorRowGrid1_SelectionChanged;
            grid.CurrentCellChanged += regexColorRowGrid1_SelectionChanged;
            UpdateRuleButtons();
        }

        public class PointSizeStringPair
        {
            public PointSizeStringPair(PointSize? pointSize, string displayString)
            {
                PointSize = pointSize;
                DisplayString = displayString;
            }

            // These are actually used by the combo box
            // ReSharper disable once MemberCanBePrivate.Local
            public PointSize? PointSize { get; set; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            // ReSharper disable once MemberCanBePrivate.Local
            public string DisplayString { get; set; }
        }

        public class PointSymbolStringPair
        {
            public PointSymbolStringPair(PointSymbol? pointSymbol, string displayString)
            {
                PointSymbol = pointSymbol;
                DisplayString = displayString;
            }

            // These are actually used by the combo box
            // ReSharper disable once MemberCanBePrivate.Local
            public PointSymbol? PointSymbol { get; set; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            // ReSharper disable once MemberCanBePrivate.Local
            public string DisplayString { get; set; }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _bindingList.ListChanged -= _bindingList_ListChanged;
            _symbolDropdownFont.Dispose();
            base.OnHandleDestroyed(e);
        }

        private void DataGridView_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (!(e.Control is ComboBox cb))
                return;

            cb.DrawItem -= SymbolCombo_DrawItem;
            cb.SelectedIndexChanged -= SymbolCombo_SelectedIndexChanged;
            cb.DrawMode = DrawMode.Normal;
            cb.ItemHeight = cb.Font.Height;
            cb.AccessibleName = null;

            if (((DataGridView) sender).CurrentCell?.ColumnIndex != _symbolCombo.Index)
                return;

            cb.DrawMode = DrawMode.OwnerDrawFixed;
            cb.ItemHeight = _symbolDropdownFont.Height + 4;
            cb.DrawItem += SymbolCombo_DrawItem;
            // The dropdown shows glyphs, which screen readers cannot announce meaningfully.
            // Mirror the selected glyph with a readable accessible name and keep it in sync.
            cb.SelectedIndexChanged += SymbolCombo_SelectedIndexChanged;
            UpdateSymbolAccessibleName(cb);
        }

        private void SymbolCombo_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
                return;
            e.DrawBackground();
            var cb = (ComboBox)sender;
            var text = cb.GetItemText(cb.Items[e.Index]);
            // Use normal font for the closed-state display (edit portion); large font for dropdown items.
            var isEditPortion = (e.State & DrawItemState.ComboBoxEdit) != 0;
            var font = isEditPortion ? cb.Font : _symbolDropdownFont;
            TextRenderer.DrawText(e.Graphics, text, font, e.Bounds, e.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            e.DrawFocusRectangle();
        }

        private void SymbolCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateSymbolAccessibleName((ComboBox)sender);
        }

        private static void UpdateSymbolAccessibleName(ComboBox cb)
        {
            cb.AccessibleName = GetSymbolAccessibleName(GetSelectedSymbol(cb));
        }

        private static PointSymbol? GetSelectedSymbol(ComboBox cb)
        {
            if (cb.SelectedItem is PointSymbolStringPair pair)
                return pair.PointSymbol;
            if (cb.SelectedValue is PointSymbol symbol)
                return symbol;
            return null;
        }

        private static string GetSymbolAccessibleName(PointSymbol? symbol)
        {
            if (symbol == null)
                return GroupComparisonStrings.VolcanoPlotFormattingDlg_VolcanoPlotFormattingDlg_None;

            switch (symbol.Value)
            {
                case PointSymbol.Circle:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_Circle;
                case PointSymbol.Square:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_Square;
                case PointSymbol.Triangle:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_Triangle;
                case PointSymbol.TriangleDown:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_TriangleDown;
                case PointSymbol.Diamond:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_Diamond;
                case PointSymbol.XCross:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_XCross;
                case PointSymbol.Plus:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_Plus;
                case PointSymbol.Star:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_Star;
                case PointSymbol.OutlineCircle:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_OutlineCircle;
                case PointSymbol.OutlineSquare:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_OutlineSquare;
                case PointSymbol.OutlineTriangle:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_OutlineTriangle;
                case PointSymbol.OutlineTriangleDown:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_OutlineTriangleDown;
                case PointSymbol.OutlineDiamond:
                    return GroupComparisonStrings.VolcanoPlotFormattingDlg_SymbolName_OutlineDiamond;
                default:
                    return string.Empty;
            }
        }

        public void Select(IdentityPath identityPath)
        {
            DotPlotUtil.Select(Program.MainWindow, identityPath);
        }
        public FoldChangeVolcanoPlot VolcanoPlot { get; private set; }

        public SummaryRelativeAbundanceGraphPane RelativeAbundanceGraph { get; private set; }

        public bool AnyProteomic { get; set; }
        public bool AnyMolecules { get; set; }
        public bool PerProtein { get; set; }
        public bool HasFoldChangeResults { get; set; }
        public SrmDocument Document { get; set; }

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
            UpdateRuleButtons();
        }

        private void regexColorRowGrid1_SelectionChanged(object sender, EventArgs e)
        {
            UpdateRuleButtons();
        }

        /// <summary>
        /// Row indexes of the rules that are currently selected in the grid, excluding the
        /// trailing "new row" placeholder (which is not backed by an entry in <see cref="_bindingList"/>).
        /// </summary>
        private IEnumerable<int> GetSelectedRuleRowIndexes()
        {
            var grid = regexColorRowGrid1.DataGridView;
            var ruleCount = _bindingList.Count;
            return grid.SelectedCells.Cast<DataGridViewCell>().Select(cell => cell.RowIndex)
                .Where(rowIndex => rowIndex >= 0 && rowIndex < ruleCount)
                .Distinct()
                .OrderBy(rowIndex => rowIndex)
                .ToList();
        }

        private void UpdateRuleButtons()
        {
            var selectedIndexes = GetSelectedRuleRowIndexes().ToList();
            btnDeleteRule.Enabled = selectedIndexes.Count > 0;
            btnMoveRuleUp.Enabled = ListViewHelper.IsMoveEnabled(_bindingList.Count, selectedIndexes, true);
            btnMoveRuleDown.Enabled = ListViewHelper.IsMoveEnabled(_bindingList.Count, selectedIndexes, false);
        }

        private void btnDeleteRule_Click(object sender, EventArgs e)
        {
            var selectedIndexes = GetSelectedRuleRowIndexes().ToHashSet();
            if (selectedIndexes.Count == 0)
                return;

            CommitCellChanges();
            var remaining = _bindingList.Where((row, index) => !selectedIndexes.Contains(index)).ToList();
            ReplaceRules(remaining);

            // Keep a sensible selection on the row that now occupies the first deleted slot.
            var firstDeleted = selectedIndexes.Min();
            SelectRule(Math.Min(firstDeleted, _bindingList.Count - 1));
        }

        private void btnMoveRuleUp_Click(object sender, EventArgs e)
        {
            MoveSelectedRules(true);
        }

        private void btnMoveRuleDown_Click(object sender, EventArgs e)
        {
            MoveSelectedRules(false);
        }

        private void MoveSelectedRules(bool upwards)
        {
            var selectedIndexes = GetSelectedRuleRowIndexes().ToList();
            if (!ListViewHelper.IsMoveEnabled(_bindingList.Count, selectedIndexes, upwards))
                return;

            CommitCellChanges();
            var reordered = ListViewHelper.MoveItems(_bindingList.ToList(), selectedIndexes, upwards);
            ReplaceRules(reordered);

            var newSelection = ListViewHelper.MoveSelectedIndexes(_bindingList.Count, selectedIndexes, upwards).ToList();
            SelectRules(newSelection);
        }

        /// <summary>
        /// Replaces the contents of <see cref="_bindingList"/> in place, raising a single reset so the
        /// grid and the live preview (<see cref="_bindingList_ListChanged"/> -&gt; <see cref="_updateGraph"/>)
        /// refresh once.
        /// </summary>
        private void ReplaceRules(IList<MatchRgbHexColor> rules)
        {
            regexColorRowGrid1.DataGridView.CancelEdit();
            _bindingList.RaiseListChangedEvents = false;
            try
            {
                _bindingList.Clear();
                foreach (var rule in rules)
                    _bindingList.Add(rule);
            }
            finally
            {
                _bindingList.RaiseListChangedEvents = true;
            }
            _bindingList.ResetBindings();
        }

        private void SelectRule(int rowIndex)
        {
            SelectRules(new[] { rowIndex });
        }

        private void SelectRules(IEnumerable<int> rowIndexes)
        {
            var grid = regexColorRowGrid1.DataGridView;
            grid.ClearSelection();
            var columnIndex = FirstVisibleColumnIndex(grid);
            var currentSet = false;
            foreach (var rowIndex in rowIndexes)
            {
                if (rowIndex < 0 || rowIndex >= grid.RowCount)
                    continue;
                foreach (DataGridViewCell cell in grid.Rows[rowIndex].Cells)
                {
                    if (cell.Visible)
                        cell.Selected = true;
                }
                if (!currentSet && columnIndex >= 0)
                {
                    grid.CurrentCell = grid.Rows[rowIndex].Cells[columnIndex];
                    currentSet = true;
                }
            }
            UpdateRuleButtons();
        }

        private static int FirstVisibleColumnIndex(DataGridView grid)
        {
            var column = grid.Columns.GetFirstColumn(DataGridViewElementStates.Visible);
            return column?.Index ?? -1;
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
            if (PerProtein)
            {
                if (AnyProteomic)
                {
                    matchOption = DisplayModeToMatchOption(SequenceTree.ProteinsDisplayMode);
                }
                else if (AnyMolecules)
                {
                    matchOption = MatchOption.MoleculeGroupName;
                }
            }
            else
            {
                if (AnyProteomic)
                {
                    matchOption = MatchOption.PeptideSequence;
                }
                else if (AnyMolecules)
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

        public MatchRgbHexColor AddRow(MatchRgbHexColor match = null)
        {
            if (IsLastRowEmpty)
            {
                // Testing for screenshots needs to be able to activate
                // the form which puts to focus on the grid and adds an
                // empty row to the binding list. This was causing tests
                // to fail when they tried to change the binding list
                // with a dirty row. Silently removing the row below fixes
                // the issue.
                _bindingList.RaiseListChangedEvents = false;
                try
                {
                    regexColorRowGrid1.DataGridView.CancelEdit();
                    _bindingList.RemoveAt(_bindingList.Count - 1);
                }
                finally
                {
                    _bindingList.RaiseListChangedEvents = true;
                }
            }

            if (match == null)
                return _bindingList.AddNew();

            _bindingList.Add(match);
            return match;
        }

        private bool IsLastRowEmpty => Equals(_bindingList.LastOrDefault(), MatchRgbHexColor.EMPTY);

        #region Functional test support for the delete/reorder toolbar

        public void SelectRuleRow(int rowIndex)
        {
            SelectRule(rowIndex);
        }

        public void ClickDeleteRule()
        {
            btnDeleteRule.PerformClick();
        }

        public void ClickMoveRuleUp()
        {
            btnMoveRuleUp.PerformClick();
        }

        public void ClickMoveRuleDown()
        {
            btnMoveRuleDown.PerformClick();
        }

        public bool DeleteRuleEnabled => btnDeleteRule.Enabled;
        public bool MoveRuleUpEnabled => btnMoveRuleUp.Enabled;
        public bool MoveRuleDownEnabled => btnMoveRuleDown.Enabled;

        #endregion

        public PointSymbol? GetRowPointSymbol(int rowIndex)
        {
            return _bindingList[rowIndex].PointSymbol;
        }

        public void SetRowPointSymbol(int rowIndex, PointSymbol? pointSymbol)
        {
            _bindingList[rowIndex].PointSymbol = pointSymbol;
        }

        /// <summary>
        /// Used by the <see cref="ColorGrid{T}"/> but not for general use.
        /// Extend the testing interface if you need more.
        /// </summary>
        BindingList<MatchRgbHexColor> ColorGrid<MatchRgbHexColor>.IColorGridOwner.GetCurrentBindingList()
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
        private void layoutLabelsBox_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.GroupComparisonAvoidLabelOverlap = layoutLabelsBox.Checked;
            _updateGraph(ResultList);
        }
    }
}
