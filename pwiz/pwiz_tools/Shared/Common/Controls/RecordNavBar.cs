/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Windows.Forms;
using pwiz.Common.Properties;

namespace pwiz.Common.Controls
{
    public partial class RecordNavBar : UserControl
    {
        private DataGridView _dataGridView;
        private bool _updatePending;
        public RecordNavBar()
        {
            InitializeComponent();
        }

        public DataGridView DataGridView
        {
            get
            {
                return _dataGridView;
            }
            set
            {
                if (_dataGridView == value)
                {
                    return;
                }
                DetachEvents();
                _dataGridView = value;
                findBox.DataGridView = value;
                AttachEvents();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AttachEvents();
            UpdateAll();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            DetachEvents();
        }

        private void AttachEvents()
        {
            if (!IsHandleCreated)
            {
                return;
            }
            if (DataGridView != null)
            {
                DataGridView.RowsAdded += DataGridViewOnRowsAdded;
                DataGridView.RowsRemoved += DataGridViewOnRowsRemoved;
                DataGridView.CurrentCellChanged += DataGridViewOnCurrentCellChanged;
            }
        }

        private void DetachEvents()
        {
            if (DataGridView != null)
            {
                DataGridView.RowsAdded -= DataGridViewOnRowsAdded;
                DataGridView.RowsRemoved -= DataGridViewOnRowsRemoved;
                DataGridView.CurrentCellChanged -= DataGridViewOnCurrentCellChanged;
                
            }
        }

        void DataGridViewOnCurrentCellChanged(object sender, EventArgs e)
        {
            UpdateAll();
        }

        void DataGridViewOnRowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            UpdateAll();
        }

        void DataGridViewOnRowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
        {
            UpdateAll();
        }

        void UpdateAll()
        {
            if (_updatePending)
            {
                return;
            }
            _updatePending = true;
            BeginInvoke(new Action(UpdateNow));
        }

        private void UpdateNow()
        {
            _updatePending = false;
            if (DataGridView == null)
            {
                return;
            }
            int filteredRowCount = GetVisibleRowCount();
            int totalRowCount = DataGridView.Rows.Count;
            lblFilteredFrom.Text = filteredRowCount == totalRowCount
                ? string.Empty 
                : string.Format(Resources.RecordNavBar_UpdateNow__Filtered_from__0__, totalRowCount);
            int currentVisibleRowIndex = GetVisibleRowIndex(GetCurrentRowIndex());
            btnNavFirst.Enabled = btnNavPrev.Enabled = currentVisibleRowIndex > 0;
            btnNavNext.Enabled = btnNavLast.Enabled = currentVisibleRowIndex < GetVisibleRowCount() - 1;
            if (!tbxRecordNumber.Focused)
            {
                tbxRecordNumber.Text = string.Format(Resources.RecordNavBar_UpdateNow__0__of__1_, (currentVisibleRowIndex + 1), filteredRowCount);
            }
        }

        private void BtnNavFirstOnClick(object sender, EventArgs e)
        {
            NavToRow(GetAbsoluteRowIndex(0));
        }


        private void NavToRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= DataGridView.Rows.Count)
            {
                return;
            }
            var row = DataGridView.Rows[rowIndex];
            if (!row.Visible)
            {
                return;
            }

            int? columnIndex = DataGridView.CurrentCell != null
                              ? DataGridView.CurrentCell.ColumnIndex
                              : DataGridView.FirstDisplayedCell != null
                                    ? DataGridView.FirstDisplayedCell.ColumnIndex
                                    : (int?) null;
            if (!columnIndex.HasValue)
            {
                return;
            }
            DataGridView.CurrentCell = row.Cells[columnIndex.Value];
        }

        private void BtnNavLastOnClick(object sender, EventArgs e)
        {
            NavToRow(GetAbsoluteRowIndex(GetVisibleRowCount() - 1));
        }

        private int GetVisibleRowCount()
        {
            return findBox.VisibleRowIndexes == null ? DataGridView.Rows.Count : findBox.VisibleRowIndexes.Length;
        }

        private int GetVisibleRowIndex(int absoluteRowIndex)
        {
            if (findBox.VisibleRowIndexes == null)
            {
                return absoluteRowIndex;
            }
            int visibleIndex = Array.BinarySearch(findBox.VisibleRowIndexes, absoluteRowIndex);
            if (visibleIndex < 0)
            {
                return -1;
            }
            return visibleIndex;
        }

        private int GetAbsoluteRowIndex(int visibleRowIndex)
        {
            if (findBox.VisibleRowIndexes == null)
            {
                return visibleRowIndex;
            }
            if (visibleRowIndex < 0)
            {
                return 0;
            }
            if (visibleRowIndex >= findBox.VisibleRowIndexes.Length)
            {
                return DataGridView.Rows.Count;
            }
            return findBox.VisibleRowIndexes[visibleRowIndex];
        }

        private int GetCurrentRowIndex()
        {
            return DataGridView.CurrentCell == null ? -1 : DataGridView.CurrentCell.RowIndex;
        }

        private void BtnNavPrevOnClick(object sender, EventArgs e)
        {
            NavToRow(GetVisibleRowIndex(GetCurrentRowIndex()) - 1);
        }

        private void BtnNavNextOnClick(object sender, EventArgs e)
        {
            NavToRow(GetVisibleRowIndex(GetCurrentRowIndex()) + 1);
        }

        private void TbxRecordNumberOnEnter(object sender, EventArgs e)
        {
            int currentVisibleRowIndex = GetVisibleRowIndex(GetCurrentRowIndex());
            tbxRecordNumber.Text = (currentVisibleRowIndex + 1).ToString(CultureInfo.InvariantCulture);
        }

        private void TbxRecordNumberOnLeave(object sender, EventArgs e)
        {
            int value;
            if (int.TryParse(tbxRecordNumber.Text, out value))
            {
                NavToRow(GetAbsoluteRowIndex(value - 1));
            }
            UpdateAll();
        }
    }
}
