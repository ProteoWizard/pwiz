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
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
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
        private DataGridViewPasteHandler(BoundDataGridView boundDataGridView)
        {
            DataGridView = boundDataGridView;
            DataGridView.KeyDown += DataGridViewOnKeyDown;
        }

        /// <summary>
        /// Attaches a DataGridViewPasteHandler to the specified DataGridView.
        /// </summary>
        public static DataGridViewPasteHandler Attach(BoundDataGridView boundDataGridView)
        {
            return new DataGridViewPasteHandler(boundDataGridView);
        }

        public BoundDataGridView DataGridView { get; private set; }

        public enum BatchModifyAction { Paste, Clear, FillDown }

        public class BatchModifyInfo : AuditLogOperationSettings<BatchModifyInfo> // TODO: this is a little lazy, consider rewriting
        {
            public BatchModifyInfo(BatchModifyAction batchModifyAction, string viewName, RowFilter rowFilter, string extraInfo = null)
            {
                BatchModifyAction = batchModifyAction;
                ViewName = viewName;
                Filter = rowFilter;
                ExtraInfo = extraInfo;
            }

            public BatchModifyAction BatchModifyAction { get; private set; }
            [Track(defaultValues:typeof(DefaultValuesNull))]
            public string ViewName { get; private set; }
            [TrackChildren]
            public RowFilter Filter { get; private set; }
            public string ExtraInfo { get; private set; }
        }

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
            var bindingListSource = DataGridView.DataSource as BindingListSource;
            var rowFilter = bindingListSource == null ? RowFilter.Empty : bindingListSource.RowFilter;
            var viewName = bindingListSource == null ? null : bindingListSource.ViewInfo.Name;

            if (Equals(e.KeyData, Keys.Control | Keys.V))
            {
                var clipboardText = ClipboardHelper.GetClipboardText(DataGridView);
                if (null == clipboardText)
                {
                    return;
                }
                using (var reader = new StringReader(clipboardText))
                {
                    e.Handled = PerformUndoableOperation(Resources.DataGridViewPasteHandler_DataGridViewOnKeyDown_Paste,
                        monitor => Paste(monitor, reader),
                        new BatchModifyInfo(BatchModifyAction.Paste, viewName,
                            rowFilter, clipboardText));
                }
            }
            else if (e.KeyCode == Keys.Delete && 0 == e.Modifiers)
            {
                e.Handled = PerformUndoableOperation(
                    Resources.DataGridViewPasteHandler_DataGridViewOnKeyDown_Clear_cells, ClearCells,
                    new BatchModifyInfo(BatchModifyAction.Clear, viewName, rowFilter));
            }
        }

        public bool PerformUndoableOperation(string description, Func<ILongWaitBroker, bool> operation, BatchModifyInfo batchModifyInfo)
        {
            var skylineDataSchema = GetDataSchema();
            if (skylineDataSchema == null)
            {
                return false;
            }
            bool resultsGridSynchSelectionOld = Settings.Default.ResultsGridSynchSelection;
            bool enabledOld = DataGridView.Enabled;
            try
            {
                Settings.Default.ResultsGridSynchSelection = false;
                var cellAddress = DataGridView.CurrentCellAddress;
                DataGridView.Enabled = false;
                DataGridView.CurrentCell = DataGridView.Rows[cellAddress.Y].Cells[cellAddress.X];
                lock (skylineDataSchema.SkylineWindow.GetDocumentChangeLock())
                {
                    skylineDataSchema.BeginBatchModifyDocument();
                    var longOperationRunner = new LongOperationRunner
                    {
                        ParentControl = FormUtil.FindTopLevelOwner(DataGridView),
                        JobTitle = description
                    };
                    if (longOperationRunner.CallFunction(operation))
                    {
                        skylineDataSchema.CommitBatchModifyDocument(description, batchModifyInfo);
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                DataGridView.Enabled = enabledOld;
                Settings.Default.ResultsGridSynchSelection = resultsGridSynchSelectionOld;
                skylineDataSchema.RollbackBatchModifyDocument();
            }
        }

        private SkylineDataSchema GetDataSchema()
        {
            var bindingListSource = DataGridView.DataSource as BindingListSource;
            if (bindingListSource == null || bindingListSource.ViewInfo == null)
            {
                return null;
            }
            return bindingListSource.ViewInfo.DataSchema as SkylineDataSchema;
        }

        /// <summary>
        /// Pastes tab delimited data into rows and columns starting from the current cell.
        /// If an error is encountered (e.g. type conversion), then a message is displayed,
        /// and the focus is left in the cell which had an error.
        /// Returns true if any changes were made to the document, false if there were no
        /// changes.
        /// </summary>
        private bool Paste(ILongWaitBroker longWaitBroker, TextReader reader)
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
                if (longWaitBroker.IsCanceled)
                {
                    return anyChanges;
                }
                longWaitBroker.Message = string.Format(Resources.DataGridViewPasteHandler_Paste_Pasting_row__0_, iRow + 1);
                string line = reader.ReadLine();
                if (null == line)
                {
                    return anyChanges;
                }
                var row = DataGridView.Rows[iRow];
                using (var values = SplitLine(line).GetEnumerator())
                {
                    for (int iCol = iFirstCol; iCol < columnsByDisplayIndex.Length; iCol++)
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
            }
            return anyChanges;
        }

        private bool ClearCells(ILongWaitBroker longWaitBroker)
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
            for (int iGrouping = 0; iGrouping < cellsByRow.Length; iGrouping++)
            {
                if (longWaitBroker.IsCanceled)
                {
                    return anyChanges;
                }
                longWaitBroker.ProgressValue = 100 * iGrouping / cellsByRow.Length;
                longWaitBroker.Message = string.Format(Resources.DataGridViewPasteHandler_ClearCells_Cleared__0___1__rows, iGrouping, cellsByRow.Length);
                var rowGrouping = cellsByRow[iGrouping];
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
                (sender, args) =>
                {
                    Assume.IsNull(editingControl);
                    editingControl = args.Control as IDataGridViewEditingControl;
                };
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
            catch (Exception e)
            {
                Program.ReportException(e);
                return false;
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
