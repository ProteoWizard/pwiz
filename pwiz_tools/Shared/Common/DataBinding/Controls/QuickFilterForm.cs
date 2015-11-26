/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding.Internal;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding.Controls
{
    public partial class QuickFilterForm : Form
    {
        public QuickFilterForm()
        {
            InitializeComponent();
        }
        public DataSchema DataSchema { get; private set; }
        public IViewContext ViewContext { get; set; }
        public PropertyDescriptor PropertyDescriptor { get; private set; }
        public RowFilter RowFilter { get; private set; }

        public void SetPropertyDescriptor(DataSchema dataSchema, PropertyDescriptor propertyDescriptor)
        {
            DataSchema = dataSchema;
            PropertyDescriptor = propertyDescriptor;
            PopulateCombo(comboOperation1, FilterOperations.OP_HAS_ANY_VALUE.DisplayName, dataSchema, propertyDescriptor.PropertyType);
            PopulateCombo(comboOperation2, Resources.QuickFilterForm_SetPropertyDescriptor_No_other_filter, dataSchema, propertyDescriptor.PropertyType);
            Text = string.Format(Resources.QuickFilterForm_SetPropertyDescriptor_Show_rows_where__0____, propertyDescriptor.DisplayName);
        }

        private void PopulateCombo(ComboBox comboBox, String hasAnyValueCaption, DataSchema dataSchema,
            Type propertyType)
        {
            comboBox.Items.Clear();
            foreach (var filterOperation in FilterOperations.ListOperations())
            {
                if (!filterOperation.IsValidFor(dataSchema, propertyType))
                {
                    continue;
                }
                string displayName;
                if (filterOperation == FilterOperations.OP_HAS_ANY_VALUE)
                {
                    displayName = hasAnyValueCaption;
                }
                else
                {
                    displayName = filterOperation.DisplayName;
                }
                comboBox.Items.Add(new FilterItem(displayName, filterOperation));
            }
        }

        public void SetFilter(DataSchema dataSchema, PropertyDescriptor propertyDescriptor, RowFilter rowFilter)
        {
            RowFilter = rowFilter;
            SetPropertyDescriptor(dataSchema, propertyDescriptor);
            var columnFilters = rowFilter.GetColumnFilters(propertyDescriptor).ToArray();
            if (columnFilters.Length >= 1)
            {
                SetFilterOperation(comboOperation1, tbxOperand1, columnFilters[0]);
                tbxOperand1.Text = columnFilters[0].Operand;
            }
            if (columnFilters.Length >= 2)
            {
                SetFilterOperation(comboOperation2, tbxOperand2, columnFilters[1]);
            }
        }

        private RowFilter GetCurrentFilter()
        {
            RowFilter rowFilter = RowFilter;
            var columnFilters = new List<RowFilter.ColumnFilter>();
            var columnFilter = MakeColumnFilter(comboOperation1, tbxOperand1);
            if (null != columnFilter)
            {
                columnFilters.Add(columnFilter);
            }
            columnFilter = MakeColumnFilter(comboOperation2, tbxOperand2);
            if (null != columnFilter)
            {
                columnFilters.Add(columnFilter);
            }
            columnFilters.AddRange(rowFilter.ColumnFilters.Where(
                filter => filter.ColumnCaption != PropertyDescriptor.DisplayName));
            return rowFilter.SetColumnFilters(columnFilters);
        }

        private void SetFilterOperation(ComboBox comboOperation, TextBox textBoxOperand, RowFilter.ColumnFilter columnFilter)
        {
            for (int i = 0; i < comboOperation.Items.Count; i++)
            {
                FilterItem filterItem = comboOperation.Items[i] as FilterItem;
                if (null != filterItem && Equals(columnFilter.FilterOperation, filterItem.FilterOperation))
                {
                    comboOperation.SelectedIndex = i;
                    break;
                }
            }
            textBoxOperand.Text = columnFilter.Operand;
        }

        private RowFilter.ColumnFilter MakeColumnFilter(ComboBox comboOperation, TextBox text)
        {
            FilterItem filterItem = comboOperation.SelectedItem as FilterItem;
            if (null == filterItem || FilterOperations.OP_HAS_ANY_VALUE == filterItem.FilterOperation)
            {
                return null;
            }
            return new RowFilter.ColumnFilter(PropertyDescriptor.DisplayName, filterItem.FilterOperation, text.Text);
        }

        private void FilterChanged(ComboBox comboOperation, TextBox text)
        {
            FilterItem filterItem = comboOperation.SelectedItem as FilterItem;
            if (null == filterItem ||
                null == filterItem.FilterOperation.GetOperandType(DataSchema, PropertyDescriptor.PropertyType))
            {
                text.Text = string.Empty;
                text.Enabled = false;
            }
            else
            {
                text.Enabled = true;
            }
        }

        public class FilterItem
        {
            public FilterItem(string displayName, IFilterOperation filterOperation)
            {
                DisplayName = displayName;
                FilterOperation = filterOperation;
            }
            public string DisplayName { get; private set; }
            public IFilterOperation FilterOperation { get; private set; }
            public override string ToString()
            {
                return DisplayName;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            RowFilter = GetCurrentFilter();
            try
            {
                RowFilter.GetPredicate(PropertyDescriptor);
            }
            catch (Exception e)
            {
                ShowErrorMessage(e);
                return;
            }
            DialogResult = DialogResult.OK;
        }

        public virtual void ShowErrorMessage(Exception e)
        {
            if (null != ViewContext)
            {
                ViewContext.ShowMessageBox(this, e.Message, MessageBoxButtons.OK);
            }
            else
            {
                MessageBox.Show(this, e.Message);
            }
        }

        private void btnClearFilter_Click(object sender, EventArgs e)
        {
            RowFilter = RowFilter.SetColumnFilters(
                RowFilter.ColumnFilters.Where(
                    columnFilter => PropertyDescriptor.DisplayName != columnFilter.ColumnCaption));
            DialogResult = DialogResult.OK;
        }

        private void btnClearAllFilters_Click(object sender, EventArgs e)
        {
            RowFilter = RowFilter.Empty;
            DialogResult = DialogResult.OK;
        }

        private void comboOperation1_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilterChanged(comboOperation1, tbxOperand1);
        }

        private void comboOperation2_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilterChanged(comboOperation2, tbxOperand2);
        }

#region methods for testing

        public bool SetFilterOperation(int index, IFilterOperation filterOperation)
        {
            var comboBox = new[] {comboOperation1, comboOperation2}[index];
            for (int i = 0; i < comboBox.Items.Count; i++)
            {
                if (Equals(filterOperation, ((FilterItem) comboBox.Items[i]).FilterOperation))
                {
                    comboBox.SelectedIndex = i;
                    return true;
                }
            }
            return false;
        }

        public bool SetFilterOperand(int index, string operand)
        {
            var textBox = new[] {tbxOperand1, tbxOperand2}[index];
            if (!textBox.Enabled)
            {
                return false;
            }
            textBox.Text = operand;
            return true;
        }

#endregion
    }
}
