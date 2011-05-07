using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

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
                if (_dataGridView != null)
                {
                    _dataGridView.RowsAdded -= _dataGridView_RowsAdded;
                    _dataGridView.RowsRemoved -= _dataGridView_RowsRemoved;
                    _dataGridView.CurrentCellChanged -= _dataGridView_CurrentCellChanged;
                }
                _dataGridView = value;
                findBox.DataGridView = value;
                if (_dataGridView != null)
                {
                    _dataGridView.RowsAdded += _dataGridView_RowsAdded;
                    _dataGridView.RowsRemoved += _dataGridView_RowsRemoved;
                    _dataGridView.CurrentCellChanged += _dataGridView_CurrentCellChanged;
                }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateAll();
        }

        void _dataGridView_CurrentCellChanged(object sender, EventArgs e)
        {
            UpdateAll();
        }

        void _dataGridView_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {
            UpdateAll();
        }

        void _dataGridView_RowsAdded(object sender, DataGridViewRowsAddedEventArgs e)
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
            if (filteredRowCount == totalRowCount)
            {
                lblFilteredFrom.Text = "";
            }
            else
            {
                lblFilteredFrom.Text = "(Filtered from " + totalRowCount + ")";
            }
            int currentVisibleRowIndex = GetVisibleRowIndex(GetCurrentRowIndex());
            btnNavFirst.Enabled = btnNavPrev.Enabled = currentVisibleRowIndex > 0;
            btnNavNext.Enabled = btnNavLast.Enabled = currentVisibleRowIndex < GetVisibleRowCount() - 1;
            if (!tbxRecordNumber.Focused)
            {
                tbxRecordNumber.Text = (currentVisibleRowIndex + 1) + " of " + filteredRowCount;
            }
        }

        private void btnNavFirst_Click(object sender, EventArgs e)
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

        private void btnNavLast_Click(object sender, EventArgs e)
        {
            NavToRow(GetAbsoluteRowIndex(GetVisibleRowCount() - 1));
        }

        private int GetVisibleRowCount()
        {
            return findBox.FilteredRowIndexes == null ? DataGridView.Rows.Count : findBox.FilteredRowIndexes.Length;
        }

        private int GetVisibleRowIndex(int absoluteRowIndex)
        {
            if (findBox.FilteredRowIndexes == null)
            {
                return absoluteRowIndex;
            }
            int visibleIndex = Array.BinarySearch(findBox.FilteredRowIndexes, absoluteRowIndex);
            if (visibleIndex < 0)
            {
                return -1;
            }
            return visibleIndex;
        }

        private int GetAbsoluteRowIndex(int visibleRowIndex)
        {
            if (findBox.FilteredRowIndexes == null)
            {
                return visibleRowIndex;
            }
            if (visibleRowIndex < 0)
            {
                return 0;
            }
            if (visibleRowIndex >= findBox.FilteredRowIndexes.Length)
            {
                return DataGridView.Rows.Count;
            }
            return findBox.FilteredRowIndexes[visibleRowIndex];
        }

        private int GetCurrentRowIndex()
        {
            return DataGridView.CurrentCell == null ? -1 : DataGridView.CurrentCell.RowIndex;
        }

        private void btnNavPrev_Click(object sender, EventArgs e)
        {
            NavToRow(GetVisibleRowIndex(GetCurrentRowIndex()) - 1);
        }

        private void btnNavNext_Click(object sender, EventArgs e)
        {
            NavToRow(GetVisibleRowIndex(GetCurrentRowIndex()) + 1);
        }

        private void tbxRecordNumber_Enter(object sender, EventArgs e)
        {
            int currentVisibleRowIndex = GetVisibleRowIndex(GetCurrentRowIndex());
            tbxRecordNumber.Text = (currentVisibleRowIndex + 1).ToString();
        }

        private void tbxRecordNumber_Leave(object sender, EventArgs e)
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
