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
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding.Layout;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Can be attached to a DataGridView so that when the user pastes (Ctrl+V), the text on the
    /// clipboard is parsed into rows and columns and pasted into the editable cells going right and
    /// down from the currently selected cell.
    ///
    /// This base class only interacts with the <see cref="DataGridView"/> -- it sets each cell's value
    /// through the grid's editing control, exactly the way a user's keystrokes would, so it works on any
    /// DataGridView (including one whose data source is not a <see cref="BindingListSource"/>, e.g. the
    /// List Designer's property grid: entering a cell adds the new row to the underlying list, and
    /// committing the editing control persists the value). <see cref="BoundDataGridViewPasteHandler"/>
    /// adds the part that has to interact with the BindingListSource -- wrapping the paste in a single
    /// batch-modify so it is one undoable Skyline operation.
    /// </summary>
    public class DataGridViewPasteHandler
    {
        protected DataGridViewPasteHandler(DataGridView dataGridView)
        {
            DataGridView = dataGridView;
        }

        /// <summary>
        /// Attaches a handler to a plain DataGridView (one not backed by a BindingListSource) so a Ctrl-V
        /// there pastes tab-separated text into its editable cells, going right and down from the current
        /// cell. Use <see cref="BoundDataGridViewPasteHandler.Attach"/> for a grid bound to a Skyline document.
        /// </summary>
        public static DataGridViewPasteHandler Attach(DataGridView dataGridView)
        {
            var handler = new DataGridViewPasteHandler(dataGridView);
            dataGridView.KeyDown += handler.DataGridViewOnKeyDown;
            return handler;
        }

        /// <summary>
        /// Pastes <paramref name="text"/> into the grid at its current cell, exactly as a Ctrl-V would,
        /// without attaching to the grid's key events. Used by automation (the AI Connector) for a plain
        /// grid that is not backed by a BindingListSource. Returns true if any cell changed.
        /// </summary>
        public static bool PasteText(DataGridView dataGridView, string text)
        {
            return new DataGridViewPasteHandler(dataGridView).PasteText(text);
        }

        public DataGridView DataGridView { get; }

        /// <summary>The view name recorded in the audit log for a paste; null for an unbound grid.</summary>
        protected virtual string ViewName => null;

        /// <summary>The row filter recorded in the audit log for a paste; empty for an unbound grid.</summary>
        protected virtual RowFilter RowFilter => RowFilter.Empty;

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
            [Track(defaultValues: typeof(DefaultValuesNull))]
            public string ViewName { get; private set; }
            [TrackChildren]
            public RowFilter Filter { get; private set; }
            public string ExtraInfo { get; private set; }
        }

        protected void DataGridViewOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }
            if (DataGridView.IsCurrentCellInEditMode && !(DataGridView.CurrentCell is DataGridViewCheckBoxCell))
            {
                return;
            }

            if (ClipboardHelper.IsPaste(e.KeyData))
            {
                var clipboardText = ClipboardHelper.GetClipboardText(DataGridView);
                if (null == clipboardText)
                {
                    return;
                }
                using (var reader = new StringReader(clipboardText))
                {
                    e.Handled = PerformUndoableOperation(UtilResources.DataGridViewPasteHandler_DataGridViewOnKeyDown_Paste,
                        monitor => Paste(monitor, reader),
                        new BatchModifyInfo(BatchModifyAction.Paste, ViewName,
                            RowFilter, clipboardText));
                }
            }
            else if (e.KeyCode == Keys.Delete && 0 == e.Modifiers)
            {
                e.Handled = PerformUndoableOperation(
                    UtilResources.DataGridViewPasteHandler_DataGridViewOnKeyDown_Clear_cells, ClearCells,
                    new BatchModifyInfo(BatchModifyAction.Clear, ViewName, RowFilter));
            }
        }

        /// <summary>
        /// Pastes tab-delimited <paramref name="text"/> starting at the current cell, exactly as a
        /// Ctrl-V of that text would, but reading from the supplied string instead of the system
        /// clipboard. Used by automation (the AI Connector). Returns true if any cell changed.
        /// </summary>
        public bool PasteText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            using (var reader = new StringReader(text))
            {
                return PerformUndoableOperation(UtilResources.DataGridViewPasteHandler_DataGridViewOnKeyDown_Paste,
                    monitor => Paste(monitor, reader),
                    new BatchModifyInfo(BatchModifyAction.Paste, ViewName, RowFilter, text));
            }
        }

        /// <summary>
        /// Runs <paramref name="operation"/> against the grid: disables the grid, validates and re-sets the
        /// current cell, runs the operation with a progress monitor, and restores the grid afterward.
        /// The base does not make the change undoable -- it just edits the grid; an unbound grid manages its
        /// own data through the cell edits. <see cref="BoundDataGridViewPasteHandler"/> overrides
        /// <see cref="ExecuteOperation"/> to wrap it in a single batch-modify on the Skyline document.
        /// </summary>
        public bool PerformUndoableOperation(string description, Func<ILongWaitBroker, bool> operation, BatchModifyInfo batchModifyInfo)
        {
            bool enabledOld = DataGridView.Enabled;
            bool focusedOld = DataGridView.Focused;
            try
            {
                DataGridView.Enabled = false;
                var cellAddress = DataGridView.CurrentCellAddress;
                if (cellAddress.Y < 0 || cellAddress.Y >= DataGridView.RowCount ||
                    cellAddress.X < 0 || cellAddress.X >= DataGridView.ColumnCount)
                {
                    return false;
                }
                DataGridView.CurrentCell = DataGridView.Rows[cellAddress.Y].Cells[cellAddress.X];
                return ExecuteOperation(description, operation, batchModifyInfo);
            }
            finally
            {
                DataGridView.Enabled = enabledOld;
                if (focusedOld && !DataGridView.Focused)
                {
                    DataGridView.Focus();
                }
                // Call "PerformLayout" so that the scrollbar displays the correct scroll position
                DataGridView.PerformLayout();
            }
        }

        /// <summary>
        /// Runs the operation with a progress monitor. The base just runs it (the cell edits are the change);
        /// a bound handler overrides this to make it a single undoable document modification.
        /// </summary>
        protected virtual bool ExecuteOperation(string description, Func<ILongWaitBroker, bool> operation, BatchModifyInfo batchModifyInfo)
        {
            var longOperationRunner = new LongOperationRunner
            {
                ParentControl = FormUtil.FindTopLevelOwner(DataGridView),
                JobTitle = description
            };
            return longOperationRunner.CallFunction(operation);
        }

        /// <summary>
        /// Pastes tab delimited data into rows and columns starting from the current cell.
        /// If an error is encountered (e.g. type conversion), then a message is displayed,
        /// and the focus is left in the cell which had an error.
        /// Returns true if any cells were changed, false if there were no changes.
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
                longWaitBroker.Message = string.Format(UtilResources.DataGridViewPasteHandler_Paste_Pasting_row__0_, iRow + 1);
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
                longWaitBroker.Message = string.Format(UtilResources.DataGridViewPasteHandler_ClearCells_Cleared__0___1__rows, iGrouping, cellsByRow.Length);
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
                try
                {
                    DataGridView.CurrentCell = cell;
                }
                catch (Exception)
                {
                    return false;
                }
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
                string message = string.Format(UtilResources.DataGridViewPasteHandler_TryConvertValue_Error_converting___0___to_required_type___1_, strValue,
                                               exception.Message);

                // CONSIDER(bspratt): this is probably not the proper parent. See "Issue 775: follow up on possible improper parenting of MessageDlg"
                MessageDlg.Show(DataGridView, message);
                convertedValue = null;
                return false;
            }
        }
    }

    /// <summary>
    /// A <see cref="DataGridViewPasteHandler"/> for a grid whose data source is a <see cref="BindingListSource"/>
    /// over a Skyline document (the Document Grid, Results Grid, ...). Beyond editing the grid (the base class),
    /// it wraps the whole paste/clear/fill-down in a single batch-modify of the document, so it is one undoable
    /// operation captured in the audit log.
    /// </summary>
    public class BoundDataGridViewPasteHandler : DataGridViewPasteHandler
    {
        protected BoundDataGridViewPasteHandler(DataGridView boundDataGridView, BindingListSource bindingListSource)
            : base(boundDataGridView)
        {
            BindingListSource = bindingListSource;
        }

        /// <summary>
        /// Attaches a handler to the specified DataGridView so a Ctrl-V there pastes through it.
        /// </summary>
        public static BoundDataGridViewPasteHandler Attach(DataGridView boundDataGridView, BindingListSource bindingListSource)
        {
            var handler = new BoundDataGridViewPasteHandler(boundDataGridView, bindingListSource);
            boundDataGridView.KeyDown += handler.DataGridViewOnKeyDown;
            return handler;
        }

        /// <summary>
        /// Pastes <paramref name="text"/> into the grid at its current cell, exactly as a Ctrl-V would,
        /// without attaching to the grid's key events. Used by automation (the AI Connector). Returns true
        /// if the document changed.
        /// </summary>
        public static bool PasteText(DataGridView boundDataGridView, BindingListSource bindingListSource, string text)
        {
            return new BoundDataGridViewPasteHandler(boundDataGridView, bindingListSource).PasteText(text);
        }

        public BindingListSource BindingListSource { get; }

        private SkylineDataSchema SkylineDataSchema => BindingListSource?.ViewInfo?.DataSchema as SkylineDataSchema;

        protected override string ViewName => BindingListSource?.ViewInfo?.Name;

        protected override RowFilter RowFilter => BindingListSource?.RowFilter ?? RowFilter.Empty;

        /// <summary>
        /// Runs the operation as a single undoable modification of the Skyline document: takes the document
        /// change lock, begins a batch modify, runs the grid edits (the base implementation), and commits --
        /// rolling back on the way out so a failed or cancelled paste leaves the document unchanged.
        /// </summary>
        protected override bool ExecuteOperation(string description, Func<ILongWaitBroker, bool> operation, BatchModifyInfo batchModifyInfo)
        {
            if (SkylineDataSchema == null)
            {
                return false;
            }
            bool resultsGridSynchSelectionOld = Settings.Default.ResultsGridSynchSelection;
            try
            {
                Settings.Default.ResultsGridSynchSelection = false;
                lock (SkylineDataSchema.SkylineWindow.GetDocumentChangeLock())
                {
                    SkylineDataSchema.BeginBatchModifyDocument();
                    if (base.ExecuteOperation(description, operation, batchModifyInfo))
                    {
                        SkylineDataSchema.CommitBatchModifyDocument(description, batchModifyInfo);
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                Settings.Default.ResultsGridSynchSelection = resultsGridSynchSelectionOld;
                SkylineDataSchema.RollbackBatchModifyDocument();
            }
        }
    }
}
