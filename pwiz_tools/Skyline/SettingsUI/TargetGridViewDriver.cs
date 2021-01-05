/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.SettingsUI
{
    /// <summary>
    /// Makes it possible to show "caffeine" in a grid view instead of "#$#caffeine#C8H10N4O2#", and adds formula, InChiKey etc columns as needed
    /// </summary>
    public abstract class TargetGridViewDriver<TItem> : SimpleGridViewDriver<TItem>
    {
        protected TargetGridViewDriver(DataGridViewEx gridView, BindingSource bindingSource, SortableBindingList<TItem> items, 
            IEnumerable<Target> targets, TargetResolver targetResolver, SrmDocument.DOCUMENT_TYPE modeUI, bool smallMolDetailColumnsReadOnly)
            : base(gridView, bindingSource, items)
        {
            SmallMoleculeColumnsManager = new SmallMoleculeColumnsManager(gridView,
                targetResolver ?? TargetResolver.MakeTargetResolver(Program.ActiveDocumentUI, targets), 
                modeUI, smallMolDetailColumnsReadOnly);
            // Find any TargetColumns and have them use same small molecule column manager
            foreach (var col in this.GridView.Columns)
            {
                (col as TargetColumn)?.SetSmallMoleculesColumnManagementProvider(SmallMoleculeColumnsManager); // Makes it possible to show "caffeine" instead of "#$#caffeine#C8H10N4O2#",and adds formula, InChiKey etc columns as needed
            }

            // Adjust parent form width for any small molecule columns that were automatically added (assumes the gridView is suitably anchored to parent form)
            if (SmallMoleculeColumnsManager.HeadersAdded != null)
            {
                for (var parent = gridView.Parent; parent != null; parent = parent.Parent)
                {
                    if (parent is Form form)
                    {
                        var g = form.CreateGraphics();
                        var extraWidth = 0;
                        foreach (var header in SmallMoleculeColumnsManager.HeadersAdded)
                        {
                            extraWidth += (int)g.MeasureString(header + @" ", gridView.Font).Width; // Rough adjustment - not worrying about precise margins etc
                        }

                        form.Width += extraWidth;
                        break;
                    }
                }
            }

            GridView.RowLeave += gridView_RowLeaving;
            GridView.DataGridViewKey += gridView_DataGridViewKey; // Deals with strange behavior with Enter key
        }

        public TargetResolver TargetResolver { get { return SmallMoleculeColumnsManager.TargetResolver; } }
        public SmallMoleculeColumnsManager SmallMoleculeColumnsManager { get; private set;  }

        void gridView_DataGridViewKey(object sender, KeyEventArgs e)
        {
            // Enter causes a row change before validation happens, so catch it here
            // See https://stackoverflow.com/questions/21873361/datagridview-enter-key-event-handling
            if (e.KeyCode == Keys.Enter)
            {
                var row = GridView.CurrentCell.RowIndex;
                var col = GridView.CurrentCell.ColumnIndex;
                if (!DoRowValidating(row, true)) // Insist on complete information in row
                {
                    GridView.CurrentCell = GridView.Rows[row].Cells[col]; // Don't leave yet
                    e.Handled = true;
                }
            }
        }

        private void gridView_RowLeaving(object sender, DataGridViewCellEventArgs e)
        {
            if (!DoRowValidating(e.RowIndex, true)) // Insist on complete information in row
            {
                GridView.CurrentCell = GridView.Rows[e.RowIndex].Cells[e.ColumnIndex]; // Don't leave yet
            }
        }

        protected abstract string DoRowValidatingNonTargetColumns(int rowIndex);

        protected virtual bool DoRowValidating(int rowIndex, bool requireCompleteMolecule)
        {
            if (rowIndex >= Items.Count)
            {
                return true;
            }
            if (GridView.Rows[rowIndex].IsNewRow)
                return true;
            var column = SmallMoleculeColumnsManager.TargetColumnIndex;
            string errorText;
            var target = TryResolveTarget(GridView.Rows[rowIndex].Cells[column].FormattedValue?.ToString(), rowIndex, out errorText, requireCompleteMolecule);
            var isValidTarget = target != null && target.IsComplete;
            if (errorText == null)
            {
                SmallMoleculeColumnsManager.UpdateSmallMoleculeDetails(target, GridView.Rows[rowIndex]); // If this is a small molecule, show formula, InChiKey etc
                if (!Equals(target, GridView.Rows[rowIndex].Cells[column].Value))
                {
                    GridView.Rows[rowIndex].Cells[column].Value = target;
                }

                if (isValidTarget)
                {
                    // Don't complain about other columns until molecule is properly defined
                    errorText = DoRowValidatingNonTargetColumns(rowIndex);
                }
            }
            if (errorText != null)
            {
                bool messageShown = false;
                try
                {
                    GridView.CurrentCell = GridView.Rows[rowIndex].Cells[column];
                    MessageDlg.Show(MessageParent, errorText);
                    messageShown = true;
                    GridView.BeginEdit(true);
                }
                catch (Exception)
                {
                    // Exception may be thrown if current cell is changed in the wrong context.
                    if (!messageShown)
                        MessageDlg.Show(MessageParent, errorText);
                }
                return false;
            }

            return isValidTarget;
        }


        public void SetTargetResolver(TargetResolver targetResolver)
        {
            SmallMoleculeColumnsManager = SmallMoleculeColumnsManager.ChangeTargetResolver(targetResolver);
        }

        // For verifying data already in the grid
        protected Target TryResolveTarget(string targetText, int row, out string errorText, bool strict)
        {
            var cells = GridView.Rows[row].Cells;
            var values = new List<string>();
            for (var i = 0; i < cells.Count; i++)
            {
                // Omit any hidden columns (e.g. "high energy offset" column in imsdb editor which may be hidden)
                if (IsColumnVisible(i))
                {
                    var formattedValue = cells[i].EditedFormattedValue;
                    values.Add(formattedValue?.ToString());
                }
            }

            return TryResolveTarget(targetText, values, row, out errorText,
                strict); // Be tolerant of partially described small molecules?
        }

        // For verifying data not yet in the grid
        protected Target TryResolveTarget(string targetText, IEnumerable<object> values, int row, out string errorText, bool strict = true)
        {
            if (strict && string.IsNullOrEmpty(targetText))
            {
                errorText = Resources
                    .MeasuredPeptide_ValidateSequence_A_modified_peptide_sequence_is_required_for_each_entry;
                return null;
            }

            var target = TargetResolver.TryResolveTarget(targetText, out errorText);
            if (target == null)
            {
                target = SmallMoleculeColumnsManager.TryGetSmallMoleculeTargetFromDetails(targetText, values, row, out _, strict);
                if (target != null)
                {
                    errorText = null;
                }
            }

            return target;
        }

        public static bool ValidateRowTarget(object[] columns, IWin32Window parent, DataGridView grid, int lineNumber, int targetColumnNumber)
        {
            var seq = columns[targetColumnNumber] as string;
            string message = null;
            if (string.IsNullOrWhiteSpace(seq))
            {
                message = string.Format(Resources.PeptideGridViewDriver_ValidateRow_Missing_peptide_sequence_on_line__0_, lineNumber);
            }
            else if (!FastaSequence.IsExSequence(seq))
            {
                // Use target resolver if available
                var targetColumn = grid?.Columns[targetColumnNumber] as TargetColumn;
                if (targetColumn?.TryResolveTarget(seq, columns.Select(c => c as string).ToArray(), lineNumber, out _) == null)
                {
                    message = string.Format(Resources.PeptideGridViewDriver_ValidateRow_The_text__0__is_not_a_valid_peptide_sequence_on_line__1_, seq, lineNumber);
                }
            }
            else
            {
                try
                {
                    columns[targetColumnNumber] = SequenceMassCalc.NormalizeModifiedSequence(seq);
                }
                catch (Exception x)
                {
                    message = x.Message;
                }
            }

            if (message == null)
            {
                return true;
            }

            MessageDlg.Show(parent, message);
            return false;
        }


    }
}
