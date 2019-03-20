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
using System.Threading;
using System.Windows.Forms;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class CreateMatchExpressionDlg : ModeUIInvariantFormEx // Dialog has explicit logic for handling UI modes
    {
        private readonly FoldChangeBindingSource.FoldChangeRow[] _foldChangeRows;
        private readonly bool _allowUpdateGrid;
        private readonly VolcanoPlotFormattingDlg _formattingDlg;
        private CancellationTokenSource _cancellationTokenSource;
        private IList<StringWrapper> _filteredRows;

        private FoldChangeVolcanoPlot _volcanoPlot
        {
            get { return _formattingDlg.VolcanoPlot; }
        }

        public CreateMatchExpressionDlg() // for designer
        {
            InitializeComponent();
        }

        public CreateMatchExpressionDlg(VolcanoPlotFormattingDlg formattingDlg, FoldChangeBindingSource.FoldChangeRow[] foldChangeRows, MatchRgbHexColor rgbHexColor)
        {
            InitializeComponent();

            _formattingDlg = formattingDlg;
            _foldChangeRows = foldChangeRows;

            PopulateComboBoxes();

            // The grid gets updated when any selected index changes or the expression textbox text changes
            // Only update manually after all selected items have been set
            _allowUpdateGrid = false;
            SetSelectedItems(rgbHexColor);
            _allowUpdateGrid = true;

            FilterRows();
        }

        private void PopulateComboBoxes()
        {
            // Match
            AddComboBoxItems(matchComboBox, new MatchOptionStringPair(null, GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_None));

            if (_volcanoPlot.AnyProteomic)
            {
                AddComboBoxItems(matchComboBox,
                    new MatchOptionStringPair(MatchOption.ProteinName,
                        GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Protein_Name),
                    new MatchOptionStringPair(MatchOption.ProteinAccession,
                        GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Protein_Accession),
                    new MatchOptionStringPair(MatchOption.ProteinPreferredName,
                        GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Protein_Preferred_Name),
                    new MatchOptionStringPair(MatchOption.ProteinGene,
                        GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Protein_Gene));
            }

            if (_volcanoPlot.PerProtein)
            {
                if (_volcanoPlot.AnyMolecules)
                {
                    AddComboBoxItems(matchComboBox,
                        new MatchOptionStringPair(MatchOption.MoleculeGroupName,
                            GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Molecule_List_Name));
                }
            }
            else
            {
                if (_volcanoPlot.AnyProteomic)
                {
                    AddComboBoxItems(matchComboBox,
                        new MatchOptionStringPair(MatchOption.PeptideSequence, GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Peptide_Sequence),
                        new MatchOptionStringPair(MatchOption.PeptideModifiedSequence, GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Peptide_Modified_Sequence));
                }

                if (_volcanoPlot.AnyMolecules)
                {
                    AddComboBoxItems(matchComboBox,
                        new MatchOptionStringPair(MatchOption.MoleculeName,
                            ColumnCaptions.MoleculeName),
                        new MatchOptionStringPair(MatchOption.CAS,
                            ColumnCaptions.CAS),
                        new MatchOptionStringPair(MatchOption.HMDB,
                            ColumnCaptions.HMDB),
                        new MatchOptionStringPair(MatchOption.InChiKey,
                            ColumnCaptions.InChiKey));
                }
            }

            // Fold Change
            AddComboBoxItems(foldChangeComboBox,
                new MatchOptionStringPair(null, GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_None),
                new MatchOptionStringPair(MatchOption.BelowLeftCutoff, GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Below_left_cutoff),
                new MatchOptionStringPair(MatchOption.AboveRightCutoff, GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Above_right_cutoff));

            // PValue
            AddComboBoxItems(pValueComboBox,
                new MatchOptionStringPair(null, GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_None),
                new MatchOptionStringPair(MatchOption.BelowPValueCutoff, GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Below),
                new MatchOptionStringPair(MatchOption.AbovePValueCutoff, GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Above));
        }

        private void AddComboBoxItems(ComboBox comboBox, params MatchOptionStringPair[] items)
        {
            comboBox.ValueMember = @"MatchOption";
            comboBox.DisplayMember = @"DisplayString";
            comboBox.Items.AddRange(items);
        }

        private void SetSelectedItems(MatchRgbHexColor rgbHexColor)
        {
            var matchExpr = rgbHexColor.MatchExpression ?? _formattingDlg.GetDefaultMatchExpression(rgbHexColor.Expression);

            matchComboBox.SelectedItem = GetSelectedItem(matchComboBox, matchExpr.matchOptions);
            foldChangeComboBox.SelectedItem =
                GetSelectedItem(foldChangeComboBox, matchExpr.matchOptions);
            pValueComboBox.SelectedItem = GetSelectedItem(pValueComboBox, matchExpr.matchOptions);

            expressionTextBox.Text = matchExpr.RegExpr;
        }

        private static MatchOptionStringPair GetSelectedItem(ComboBox comboBox, IEnumerable<MatchOption> matchOptions)
        {
            var items = comboBox.Items.Cast<MatchOptionStringPair>().ToArray();
            return GetMatchOptionStringPair(items, matchOptions);
        }

        public static MatchOptionStringPair GetMatchOptionStringPair(MatchOptionStringPair[] items,
            IEnumerable<MatchOption> matchOptions)
        {

            foreach (var m in matchOptions)
            {
                var item = items.FirstOrDefault(p => p.MatchOption.HasValue && p.MatchOption.Value == m);
                if (item != null)
                    return item;
            }

            return items[0]; // None
        }

        public class MatchOptionStringPair
        {
            public MatchOptionStringPair(MatchOption? matchOption, string displayString)
            {
                MatchOption = matchOption;
                DisplayString = displayString;
            }

            // These are actually used by the combo box
            // ReSharper disable once MemberCanBePrivate.Local
            public MatchOption? MatchOption { get; set; }
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            // ReSharper disable once MemberCanBePrivate.Local
            public string DisplayString { get; set; }
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        public MatchExpression GetCurrentMatchExpression()
        {
            var regExpr = expressionTextBox.Text;
            var matchOptions = new List<MatchOption>();

            var selected = (MatchOptionStringPair)foldChangeComboBox.SelectedItem;
            if (selected.MatchOption.HasValue)
                matchOptions.Add(selected.MatchOption.Value);

            selected = (MatchOptionStringPair)pValueComboBox.SelectedItem;
            if (selected.MatchOption.HasValue)
                matchOptions.Add(selected.MatchOption.Value);

            selected = (MatchOptionStringPair)matchComboBox.SelectedItem;
            if (selected.MatchOption.HasValue)
                matchOptions.Add(selected.MatchOption.Value);

            return new MatchExpression(regExpr, matchOptions);
        }

        private class StringWrapper
        {
            public StringWrapper(string str)
            {
                Value = str;
            }

            // ReSharper disable once MemberCanBePrivate.Local
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public string Value { get; set; }
        }

        private void UpdateGrid(MatchExpression validForExpr)
        {
            if (!_allowUpdateGrid)
                return;
           
            var expr = GetCurrentMatchExpression();
            if (!Equals(expr, validForExpr))
                return;

            bindingSource1.DataSource = null;
            expressionTextBox.ForeColor = Color.Black;

            if (!expr.IsRegexValid())
            {
                expressionTextBox.ForeColor = Color.Red;
                return;
            }

            if (_filteredRows != null)
            {
                bindingSource1.DataSource = new BindingList<StringWrapper>(_filteredRows);
                _filteredRows = null;
            }
            else
            {
                bindingSource1.DataSource = new BindingList<StringWrapper>(_foldChangeRows.Select(row =>
                    RowToString(expr, row)).ToArray());
            }
        }

        private StringWrapper RowToString(MatchExpression expr, FoldChangeBindingSource.FoldChangeRow row)
        {
            return new StringWrapper(expr.GetDisplayString(_volcanoPlot.Document, row.Protein, row.Peptide) ??
                              TextUtil.EXCEL_NA);
        }

        private void FilterRows()
        {
            if (!_allowUpdateGrid)
                return;

            if (_cancellationTokenSource != null)
                _cancellationTokenSource.Cancel();

            _cancellationTokenSource = new CancellationTokenSource();
            _filteredRows = null;
            var expr = GetCurrentMatchExpression();
            if (expr.IsRegexValid())
            {
                Cursor = Cursors.WaitCursor;

                ActionUtil.RunAsync(() =>
                {
                    GetFilteredRows(_cancellationTokenSource.Token, _foldChangeRows, expr);
                });
            }
            else
            {
                _filteredRows = new List<StringWrapper>();
                UpdateGrid(expr);
            }
        }

        private void expressionTextBox_TextChanged(object sender, EventArgs e)
        {
            FilterRows();
        }

        private void GetFilteredRows(CancellationToken canellationToken, FoldChangeBindingSource.FoldChangeRow[] rows, MatchExpression expr)
        {
            IList<StringWrapper> filteredRows = new List<StringWrapper>();

            foreach (var row in rows)
            {
                if (expr.Matches(_volcanoPlot.Document, row.Protein, row.Peptide,
                    row.FoldChangeResult, FoldChangeVolcanoPlot.CutoffSettings))
                {
                    filteredRows.Add(RowToString(expr, row));
                }

                if (canellationToken.IsCancellationRequested)
                    return;
            }

            try
            {
                _formattingDlg.BeginInvoke(new Action(() =>
                {
                    if (!canellationToken.IsCancellationRequested)
                    {
                        _filteredRows = filteredRows;
                        Cursor = Cursors.Default;
                        UpdateGrid(expr);
                    }
                }));
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilterRows();
        }

        private void linkRegex_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            WebHelpers.OpenRegexDocLink(this);
        }

        private void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == nameColumn.Index && e.RowIndex >= 0)
            {
                var row = _foldChangeRows[e.RowIndex];
                _volcanoPlot.Select(_volcanoPlot.PerProtein
                    ? row.Protein.IdentityPath
                    : row.Peptide.IdentityPath);
            }
        }

        #region Function Test Support

        public string Expression
        {
            get { return expressionTextBox.Text; }
            set { expressionTextBox.Text = value; }
        }

        public MatchOptionStringPair PValueSelectedItem
        {
            get { return (MatchOptionStringPair) pValueComboBox.SelectedItem; }
            set { pValueComboBox.SelectedItem = value; }
        }

        public IEnumerable<MatchOptionStringPair> PValueItems
        {
            get { return pValueComboBox.Items.Cast<MatchOptionStringPair>(); }
        }

        public MatchOptionStringPair FoldChangeSelectedItem
        {
            get { return (MatchOptionStringPair)foldChangeComboBox.SelectedItem; }
            set { foldChangeComboBox.SelectedItem = value; }
        }

        public IEnumerable<MatchOptionStringPair> FoldChangeItems
        {
            get { return foldChangeComboBox.Items.Cast<MatchOptionStringPair>(); }
        }

        public MatchOptionStringPair MatchSelectedItem
        {
            get { return (MatchOptionStringPair)matchComboBox.SelectedItem; }
            set { matchComboBox.SelectedItem = value; }
        }

        public IEnumerable<MatchOptionStringPair> MatchItems
        {
            get { return matchComboBox.Items.Cast<MatchOptionStringPair>(); }
        }

        public IEnumerable<string> MatchingRows
        {
            get { return dataGridView1.Rows.OfType<DataGridViewRow>().Select(r => (string) r.Cells[0].Value); }
        }

        #endregion
    }
}
