/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Collections;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Methods to test user interaction with a DataGridView
    /// </summary>
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

        public void MoveToCell(int irow, int icol)
        {
            RunUI(() => DataGridView.CurrentCell = DataGridView.Rows[irow].Cells[icol]);
        }

        public void SetCellValue(int irow, int icol, object value)
        {
            MoveToCell(irow, icol);
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


        /// <summary>
        /// Chooses the item in a combox box editing control based on the <see cref="DataGridViewComboBoxColumn.ValueMember"/>
        /// property values of the drop down items.
        /// </summary>
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

        /// <summary>
        /// Looks for the item in a list where a particular property value matches a specified value.
        /// </summary>
        /// <param name="values">List of items to look through</param>
        /// <param name="valueMember">Name of the property to look at on each item</param>
        /// <param name="value">Value to look for</param>
        /// <returns>Index of matching item in list or -1 if not found</returns>
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
