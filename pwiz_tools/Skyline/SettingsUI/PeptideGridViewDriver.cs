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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

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
                {
                    return string.Format(Resources.PeptideGridViewDriver_ValidateUniquePeptides_The_peptide__0__appears_multiple_times_in_the_added_list,
                                         multiplePeptides.First());
                }
                if (countDuplicates < 15)
                {
                    return TextUtil.LineSeparate(Resources.PeptideGridViewDriver_ValidateUniquePeptides_The_following_peptides_appear_multiple_times_in_the_added_list,
                                                 string.Empty,
                                                 TextUtil.LineSeparate(multiplePeptides));
                }
                return string.Format(Resources.PeptideGridViewDriver_ValidateUniquePeptides_The_added_lists_contains__0__peptides_which_appear_multiple_times,
                                     countDuplicates);
            }
            if (existing != null)
            {
                var intersectingPeptides = peptidesArray.Intersect(existing).ToArray();
                countDuplicates = intersectingPeptides.Length;
                if (countDuplicates == 1)
                {
                    return string.Format(Resources.PeptideGridViewDriver_ValidateUniquePeptides_The_peptide__0__already_appears_in_the__1__list,
                                         intersectingPeptides.First(), existingName);
                }
                if (countDuplicates < 15)
                {
                    return TextUtil.LineSeparate(string.Format(Resources.PeptideGridViewDriver_ValidateUniquePeptides_The_following_peptides_already_appear_in_the__0__list,
                                                               existingName),
                                                 string.Empty,
                                                 TextUtil.LineSeparate(multiplePeptides));
                }
                return string.Format(Resources.PeptideGridViewDriver_ValidateUniquePeptides_The_added_lists_contains__0__peptides_which_already_appear_in_the__1__list,
                                     countDuplicates, existingName);
            }
            return null;
        }

        public static bool ValidateRowWithTime(object[] columns, IWin32Window parent, int lineNumber)
        {
            return ValidateRow(columns, parent, lineNumber, true);
        }

        public static bool ValidateRowWithIrt(object[] columns, IWin32Window parent, int lineNumber)
        {
            return ValidateRow(columns, parent, lineNumber, false);
        }

        public static bool ValidateRow(object[] columns, IWin32Window parent, int lineNumber, bool postiveTime)
        {
            if (columns.Length != 2)
            {
                MessageDlg.Show(parent, string.Format(Resources.PeptideGridViewDriver_ValidateRow_The_pasted_text_must_have_two_columns_));
                return false;
            }

            string seq = columns[COLUMN_SEQUENCE] as string;
            string time = columns[COLUMN_TIME] as string;
            string message = null;
            if (string.IsNullOrWhiteSpace(seq))
            {
                message = string.Format(Resources.PeptideGridViewDriver_ValidateRow_Missing_peptide_sequence_on_line__0_, lineNumber);
            }
            else if (!FastaSequence.IsExSequence(seq))
            {
                message = string.Format(Resources.PeptideGridViewDriver_ValidateRow_The_text__0__is_not_a_valid_peptide_sequence_on_line__1_, seq, lineNumber);
            }
            else
            {
                try
                {
                    columns[COLUMN_SEQUENCE] = SequenceMassCalc.NormalizeModifiedSequence(seq);
                }
                catch (Exception x)
                {
                    message = x.Message;
                }

                if (message == null)
                {
                    double dTime;
                    if (string.IsNullOrWhiteSpace(time))
                    {
                        message = string.Format(Resources.PeptideGridViewDriver_ValidateRow_Missing_value_on_line__0_, lineNumber);
                    }
                    else if (!double.TryParse(time, out dTime))
                    {
                        message = string.Format(Resources.PeptideGridViewDriver_ValidateRow_Invalid_decimal_number_format__0__on_line__1_, time, lineNumber);
                    }
                    else if (postiveTime && dTime <= 0)
                    {
                        message = string.Format(Resources.PeptideGridViewDriver_ValidateRow_The_time__0__must_be_greater_than_zero_on_line__1_, time, lineNumber);
                    }
                    else
                    {
                        return true;
                    }                    
                }
            }

            MessageDlg.Show(parent, message);
            return false;
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
                        errorText = string.Format(Resources.PeptideGridViewDriver_DoCellValidating_The_sequence__0__is_already_present_in_the_list, sequence);
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
