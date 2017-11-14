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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Controls.GroupComparison
{
    public partial class CreateMatchExpressionDlg : FormEx
    {
        private readonly FoldChangeBindingSource.FoldChangeRow[] _foldChangeRows;
        private readonly bool _allowUpdateGrid;
        private readonly VolcanoPlotFormattingDlg _formattingDlg;

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

            UpdateGrid();
        }

        private void PopulateComboBoxes()
        {
            // Match
            AddComboBoxItems(matchComboBox, new MatchOptionStringPair(null, GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_None));

            if (_volcanoPlot.PerProtein)
            {
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

                if (_volcanoPlot.AnyMolecules)
                {
                    AddComboBoxItems(matchComboBox,
                        new MatchOptionStringPair(MatchOption.MoleculeGroupName,
                            GroupComparisonStrings.CreateMatchExpression_PopulateComboBoxes_Molecule_Group_Name));
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
            comboBox.ValueMember = "MatchOption"; // Not L10N
            comboBox.DisplayMember = "DisplayString"; // Not L10N
            comboBox.Items.AddRange(items);
        }

        private void SetSelectedItems(MatchRgbHexColor rgbHexColor)
        {
            var matchExpr = rgbHexColor.MatchExpression ?? _formattingDlg.GetDefaultMatchExpression(rgbHexColor.Expression);

            expressionTextBox.Text = matchExpr.RegExpr;

            matchComboBox.SelectedItem = GetSelectedItem(matchComboBox, matchExpr.matchOptions);
            foldChangeComboBox.SelectedItem =
                GetSelectedItem(foldChangeComboBox, matchExpr.matchOptions);
            pValueComboBox.SelectedItem = GetSelectedItem(pValueComboBox, matchExpr.matchOptions);
        }

        private static MatchOptionStringPair GetSelectedItem(ComboBox comboBox, IEnumerable<MatchOption> matchOptions)
        {
            var items = comboBox.Items.Cast<MatchOptionStringPair>().ToArray();

            foreach (var m in matchOptions)
            {
                var item = items.FirstOrDefault(p => p.MatchOption.HasValue && p.MatchOption.Value == m);
                if (item != null)
                    return item;
            }

            return items[0]; // None
        }

        private class MatchOptionStringPair
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

        private void UpdateGrid()
        {
            if (!_allowUpdateGrid)
                return;
           
            var expr = GetCurrentMatchExpression();
            dataGridView1.Rows.Clear();

            expressionTextBox.ForeColor = Color.Black;

            if (!expr.IsRegexValid())
            {
                expressionTextBox.ForeColor = Color.Red;
                return;
            }

            foreach (var row in _foldChangeRows)
            {
                if (expr.Matches(_volcanoPlot.Document, row.Protein, row.Peptide, row.FoldChangeResult, FoldChangeVolcanoPlot.CutoffSettings))
                    dataGridView1.Rows.Add(expr.GetMatchString(_volcanoPlot.Document, row.Protein, row.Peptide) ?? TextUtil.EXCEL_NA);
            }            
        }

        private void expressionTextBox_TextChanged(object sender, EventArgs e)
        {
            UpdateGrid();
        }

        private void comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateGrid();
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
    }
}