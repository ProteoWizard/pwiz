using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    public class GridTester
    {
        public GridTester(DataGridView dataGridView)
        {
            DataGridView = dataGridView;
        }

        public DataGridView DataGridView { get; private set; }

        public void SetCurrentCellValue(object value)
        {
            PerformEdit(editingControl =>
            {
                if (null != editingControl)
                {
                    editingControl.EditingControlFormattedValue = value;
                }
                else
                {
                    DataGridView.CurrentCell.Value = value;
                }
            });
        }

        public void PerformEdit(Action<IDataGridViewEditingControl> action)
        {
            RunUI(()=>PerformEditNow(action));
        }

        public void EndEdit()
        {
            RunUI(()=>DataGridView.EndEdit());
        }

        private void PerformEditNow(Action<IDataGridViewEditingControl> action)
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
                DataGridView.BeginEdit(true);
                action(editingControl);
            }
            finally
            {
                DataGridView.EditingControlShowing -= onEditingControlShowing;
            }
        }

        public void SetCellAddress(int irow, int icol)
        {
            RunUI(() => DataGridView.CurrentCell = DataGridView.Rows[irow].Cells[icol]);
        }

        public void SetCellValue(int irow, int icol, object value)
        {
            SetCellAddress(irow, icol);
            SetCurrentCellValue(value);
        }

        public void SetCellValue(int iRow, DataGridViewColumn column, object value)
        {
            SetCellValue(iRow, column.Index, value);
        }

        private void RunUI(Action action)
        {
            AbstractFunctionalTest.RunUI(action);
        }

        public void SetComboBoxValueInCurrentCell(object value)
        {
            PerformEdit(editingControl=>SetValueInComboBoxControl(editingControl, value));
        }


        public void SetValueInComboBoxControl(IDataGridViewEditingControl editingControl, object value)
        {
            Assert.AreSame(editingControl.EditingControlDataGridView, DataGridView);
            Assert.IsInstanceOfType(editingControl, typeof(DataGridViewComboBoxEditingControl));
            var comboBoxEditingControl = (DataGridViewComboBoxEditingControl) editingControl;
            var currentColumn = DataGridView.Columns[DataGridView.CurrentCellAddress.X];
            var comboBoxColumn = (DataGridViewComboBoxColumn) currentColumn;
            int index = IndexOfValue(comboBoxEditingControl.Items, comboBoxColumn.ValueMember, value);
            Assert.IsTrue(index >= 0, "Value {0} not found", value);
            comboBoxEditingControl.SelectedIndex = index;
        }

        public static int IndexOfValue(IEnumerable values, string valueMember, object value)
        {
            int index = 0;
            foreach (var item in values)
            {
                if (Equals(value, GetPropertyValue(item, valueMember)))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        public static object GetPropertyValue(object item, string propertyName)
        {
            if (item == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(propertyName))
            {
                return item;
            }

            var property = item.GetType().GetProperty(propertyName);
            Assert.IsNotNull(property);
            return property.GetValue(item);
        }
    }
}
