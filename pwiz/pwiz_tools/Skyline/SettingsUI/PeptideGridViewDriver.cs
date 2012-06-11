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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public abstract class PeptideGridViewDriver<TItem> : SimpleGridViewDriver<TItem>
        where TItem : IPeptideData
    {
        public const int COLUMN_SEQUENCE = 0;
        public const int COLUMN_TIME = 1;

        protected PeptideGridViewDriver(DataGridViewEx gridView, BindingSource bindingSource, SortableBindingList<TItem> items)
            : base(gridView, bindingSource, items)
        {
            GridView.CellValidating += gridView_CellValidating;
            GridView.RowValidating += gridView_RowValidating;
        }

        protected bool AllowNegativeTime { get; set; }

        public static string ValidateUniquePeptides(IEnumerable<string> peptides, IEnumerable<string> existing, string existingName)
        {
            var peptidesArray = peptides.ToArray();
            var multiplePeptides = (from p in peptidesArray
                                   group p by p into g
                                   where g.Count() > 1
                                   select g.Key).ToArray();

            int countDuplicates = multiplePeptides.Length;
            if (countDuplicates > 0)
            {
                if (countDuplicates == 1)
                    return string.Format("The peptide '{0}' appears multiple times in the added list.", multiplePeptides.First());
                if (countDuplicates < 15)
                    return string.Format("The following peptides appear multipe times in the added list:\n\n{0}",
                                         string.Join("\n", multiplePeptides.ToArray()));
                return string.Format("The added lists contains {0} peptides which appear multiple times.",
                                     countDuplicates);
            }
            if (existing != null)
            {
                var intersectingPeptides = peptidesArray.Intersect(existing).ToArray();
                countDuplicates = intersectingPeptides.Length;
                if (countDuplicates == 1)
                    return string.Format("The peptide '{0}' already appears in the {1} list.", intersectingPeptides.First(), existingName);
                if (countDuplicates < 15)
                    return string.Format("The following peptides already appear in the {1} list:\n\n{0}",
                                         string.Join("\n", multiplePeptides.ToArray()), existingName);
                return string.Format("The added lists contains {0} peptides which already appear in the {1} list.",
                                     countDuplicates, existingName);
            }
            return null;
        }

        public static bool ValidateRow(object[] columns, int lineNumber)
        {
            double x;
            if (columns.Length != 2)
                return false;
            string seq = columns[COLUMN_SEQUENCE] as string;
            string time = columns[COLUMN_TIME] as string;
            return (!string.IsNullOrEmpty(seq) &&
                    FastaSequence.IsExSequence(seq) &&
                    !string.IsNullOrEmpty(time) &&
                    double.TryParse(time, out x));
        }

        private void gridView_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            if (!DoCellValidating(e.RowIndex, e.ColumnIndex, e.FormattedValue.ToString()))
                e.Cancel = true;
        }

        protected virtual bool DoCellValidating(int rowIndex, int columnIndex, string value)
        {
            string errorText = null;
            if (columnIndex == COLUMN_SEQUENCE && GridView.IsCurrentCellInEditMode)
            {
                string sequence = value;
                errorText = MeasuredPeptide.ValidateSequence(sequence);
                if (errorText == null)
                {
                    int iExist = Items.ToArray().IndexOf(pep => Equals(pep.Sequence, sequence));
                    if (iExist != -1 && iExist != rowIndex)
                        errorText = string.Format("The sequence '{0}' is already present in the list.", sequence);
                }
            }
            else if (columnIndex == COLUMN_TIME && GridView.IsCurrentCellInEditMode)
            {
                string rtText = value;
                errorText = MeasuredPeptide.ValidateRetentionTime(rtText, AllowNegativeTime);
            }
            if (errorText != null)
            {
                MessageDlg.Show(MessageParent, errorText);
                return false;
            }
            return true;
        }

        private void gridView_RowValidating(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (!DoRowValidating(e.RowIndex))
                e.Cancel = true;
        }

        protected virtual bool DoRowValidating(int rowIndex)
        {
            var row = GridView.Rows[rowIndex];
            if (row.IsNewRow)
                return true;
            var cell = row.Cells[COLUMN_SEQUENCE];
            string errorText = MeasuredPeptide.ValidateSequence(cell.FormattedValue != null
                                                                    ? cell.FormattedValue.ToString()
                                                                    : null);
            if (errorText == null)
            {
                cell = row.Cells[COLUMN_TIME];
                errorText = MeasuredPeptide.ValidateRetentionTime(cell.FormattedValue != null
                                                                      ? cell.FormattedValue.ToString()
                                                                      : null, AllowNegativeTime);
            }
            if (errorText != null)
            {
                bool messageShown = false;
                try
                {
                    GridView.CurrentCell = cell;
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
            return true;
        }
    }
}
