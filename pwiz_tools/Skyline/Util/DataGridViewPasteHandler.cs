/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Can be attached to a DataGridView so that when the user pastes (Ctrl+V), and the 
    /// the text on the clipboard is parsed into rows and columns and pasted into the
    /// editable cells going right and down from the currently selected cell.
    /// </summary>
    public class DataGridViewPasteHandler
    {
        private DataGridViewPasteHandler(SkylineWindow skylineWindow, DataGridView dataGridView)
        {
            SkylineWindow = skylineWindow;
            DataGridView = dataGridView;
            DataGridView.KeyDown += DataGridViewOnKeyDown;
        }

        /// <summary>
        /// Attaches a DataGridViewPasteHandler to the specified DataGridView.
        /// </summary>
        public static DataGridViewPasteHandler Attach(SkylineWindow skylineWindow, DataGridView dataGridView)
        {
            return new DataGridViewPasteHandler(skylineWindow, dataGridView);
        }

        public DataGridView DataGridView { get; private set; }
        public SkylineWindow SkylineWindow { get; private set; }

        private void DataGridViewOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }
            if (DataGridView.IsCurrentCellInEditMode && !(DataGridView.CurrentCell is DataGridViewCheckBoxCell))
            {
                return;
            }
            if (SkylineWindow.IsPasteKeys(e.KeyData))
            {
                var clipboardText = ClipboardEx.GetText();
                if (null == clipboardText)
                {
                    return;
                }
                using (var reader = new StringReader(clipboardText))
                using (var undoTransaction = SkylineWindow.BeginUndo(Resources.DataGridViewPasteHandler_DataGridViewOnKeyDown_Paste))
                {
                    if (Paste(reader))
                    {
                        undoTransaction.Commit();
                        e.Handled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Pastes tab delimited data into rows and columns starting from the current cell.
        /// If an error is encountered (e.g. type conversion), then a message is displayed,
        /// and the focus is left in the cell which had an error.
        /// Returns true if any changes were made to the document, false if there were no
        /// changes.
        /// </summary>
        private bool Paste(TextReader reader)
        {
            IDataGridViewEditingControl editingControl;
            DataGridViewEditingControlShowingEventHandler onEditingControlShowing =
                (sender, args) => editingControl = args.Control as IDataGridViewEditingControl;
            bool resultsGridSynchSelectionOld = Settings.Default.ResultsGridSynchSelection;
            try
            {
                DataGridView.EditingControlShowing += onEditingControlShowing;
                Settings.Default.ResultsGridSynchSelection = false;
                bool anyChanges = false;
                var columnsByDisplayIndex =
                    DataGridView.Columns.Cast<DataGridViewColumn>().Where(column => column.Visible).ToArray();
                Array.Sort(columnsByDisplayIndex, (col1, col2) => col1.DisplayIndex.CompareTo(col2.DisplayIndex));
                int iFirstCol;
                int iFirstRow;
                if (null == DataGridView.CurrentCell)
                {
                    iFirstRow = 0;
                    iFirstCol = 0;
                }
                else
                {
                    iFirstCol = columnsByDisplayIndex.IndexOf(col => col.Index == DataGridView.CurrentCell.ColumnIndex);
                    iFirstRow = DataGridView.CurrentCell.RowIndex;
                }

                for (int iRow = iFirstRow; iRow < DataGridView.Rows.Count; iRow++)
                {
                    string line = reader.ReadLine();
                    if (null == line)
                    {
                        return anyChanges;
                    }
                    var row = DataGridView.Rows[iRow];
                    var values = SplitLine(line).GetEnumerator();
                    for (int iCol = iFirstCol; iCol < columnsByDisplayIndex.Count(); iCol++)
                    {
                        if (!values.MoveNext())
                        {
                            break;
                        }
                        var column = columnsByDisplayIndex[iCol];
                        if (column.ReadOnly)
                        {
                            continue;
                        }
                        DataGridView.CurrentCell = row.Cells[column.Index];
                        string strValue = values.Current;
                        editingControl = null;
                        DataGridView.BeginEdit(true);
// ReSharper disable ConditionIsAlwaysTrueOrFalse
                        if (null != editingControl)
// ReSharper restore ConditionIsAlwaysTrueOrFalse
// ReSharper disable HeuristicUnreachableCode
                        {
                            object convertedValue;
                            if (!TryConvertValue(strValue, DataGridView.CurrentCell.FormattedValueType, out convertedValue))
                            {
                                return anyChanges;
                            }
                            editingControl.EditingControlFormattedValue = convertedValue;
                        }
// ReSharper restore HeuristicUnreachableCode
                        else
                        {
                            object convertedValue;
                            if (!TryConvertValue(strValue, DataGridView.CurrentCell.ValueType, out convertedValue))
                            {
                                return anyChanges;
                            }
                            DataGridView.CurrentCell.Value = convertedValue;
                        }
                        if (!DataGridView.EndEdit())
                        {
                            return anyChanges;
                        }
                        anyChanges = true;
                    }
                }
                return anyChanges;
            }
            finally
            {
                DataGridView.EditingControlShowing -= onEditingControlShowing;
                Settings.Default.ResultsGridSynchSelection = resultsGridSynchSelectionOld;
            }
        }

        private static readonly char[] COLUMN_SEPARATORS = {'\t'};
        private IEnumerable<string> SplitLine(string row)
        {
            return row.Split(COLUMN_SEPARATORS);
        }

        protected bool TryConvertValue(string strValue, Type valueType, out object convertedValue)
        {
            if (null == valueType)
            {
                convertedValue = strValue;
                return true;
            }
            try
            {
                convertedValue = Convert.ChangeType(strValue, valueType);
                return true;
            }
            catch (Exception exception)
            {
                string message = string.Format(Resources.DataGridViewPasteHandler_TryConvertValue_Error_converting___0___to_required_type___1_, strValue,
                                               exception.Message);
                MessageBox.Show(DataGridView, message, Program.Name);
                convertedValue = null;
                return false;
            }
        }
    }
}
