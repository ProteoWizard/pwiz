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
                {
                    e.Handled = PerformUndoableOperation(Resources.DataGridViewPasteHandler_DataGridViewOnKeyDown_Paste, 
                        () => Paste(reader));
                }
            }
            else if (e.KeyCode == Keys.Delete && 0 == e.Modifiers)
            {
                e.Handled = PerformUndoableOperation(Resources.DataGridViewPasteHandler_DataGridViewOnKeyDown_Clear_cells, ClearCells);
            }
        }

        private bool PerformUndoableOperation(string description, Func<bool> operation)
        {
            using (var undoTransaction = SkylineWindow.BeginUndo(description))
            {
                bool resultsGridSynchSelectionOld = Settings.Default.ResultsGridSynchSelection;
                try
                {
                    Settings.Default.ResultsGridSynchSelection = false;
                    if (operation())
                    {
                        undoTransaction.Commit();
                        return true;
                    }
                    return false;
                }
                finally
                {
                    Settings.Default.ResultsGridSynchSelection = resultsGridSynchSelectionOld;
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
                    if (!TrySetValue(row.Cells[column.Index], values.Current))
                    {
                        return anyChanges;
                    }
                    anyChanges = true;
                }
            }
            return anyChanges;
        }

        private bool ClearCells()
        {
            if (DataGridView.SelectedRows.Count > 0)
            {
                return false;
            }
            var columnIndexes = DataGridView.SelectedCells.Cast<DataGridViewCell>().Select(cell => cell.ColumnIndex).Distinct().ToArray();
            if (columnIndexes.Any(columnIndex => DataGridView.Columns[columnIndex].ReadOnly))
            {
                return false;
            }
            bool anyChanges = false;
            var cellsByRow = DataGridView.SelectedCells.Cast<DataGridViewCell>().ToLookup(cell => cell.RowIndex).ToArray();
            Array.Sort(cellsByRow, (g1,g2)=>g1.Key.CompareTo(g2.Key));
            foreach (var rowGrouping in cellsByRow)
            {
                var cells = rowGrouping.ToArray();
                Array.Sort(cells, (c1, c2) => c1.ColumnIndex.CompareTo(c2.ColumnIndex));
                foreach (var cell in cells)
                {
                    if (!TrySetValue(cell, string.Empty))
                    {
                        return anyChanges;
                    }
                    anyChanges = true;
                }
            }
            return anyChanges;
        }

        private bool TrySetValue(DataGridViewCell cell, string strValue)
        {
            IDataGridViewEditingControl editingControl = null;
            DataGridViewEditingControlShowingEventHandler onEditingControlShowing =
                (sender, args) => editingControl = args.Control as IDataGridViewEditingControl;
            try
            {
                DataGridView.EditingControlShowing += onEditingControlShowing;
                DataGridView.CurrentCell = cell;
                DataGridView.BeginEdit(true);
                if (null != editingControl)
                {
                    object convertedValue;
                    if (!TryConvertValue(strValue, DataGridView.CurrentCell.FormattedValueType, out convertedValue))
                    {
                        return false;
                    }
                    editingControl.EditingControlFormattedValue = convertedValue;
                }
                else
                {
                    object convertedValue;
                    if (!TryConvertValue(strValue, DataGridView.CurrentCell.ValueType, out convertedValue))
                    {
                        return false;
                    }
                    DataGridView.CurrentCell.Value = convertedValue;
                }
                if (!DataGridView.EndEdit())
                {
                    return false;
                }
                return true;
            }
            finally
            {
                DataGridView.EditingControlShowing -= onEditingControlShowing;
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
