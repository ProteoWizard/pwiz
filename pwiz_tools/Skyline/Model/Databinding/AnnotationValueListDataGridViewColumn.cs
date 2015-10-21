/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Model.Databinding
{
    public class AnnotationValueListDataGridViewColumn : DataGridViewComboBoxColumn
    {
        protected override void OnDataGridViewChanged()
        {
            base.OnDataGridViewChanged();
            var items = GetDropdownItems() ?? new string[0];
            if (items.SequenceEqual(Items.Cast<object>()))
            {
                return;
            }
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }
            FlatStyle = FlatStyle.Flat;
        }

        private IList<string> GetDropdownItems()
        {
            if (null == DataGridView || null == DataPropertyName)
            {
                return null;
            }
            var bindingSource = DataGridView.DataSource as BindingSource;
            if (null == bindingSource)
            {
                return null;
            }
            var columnPropertyDescriptor = bindingSource.GetItemProperties(null)[DataPropertyName] as ColumnPropertyDescriptor;
            if (null == columnPropertyDescriptor)
            {
                return null;
            }
            var annotationPropertyDescriptor =
                columnPropertyDescriptor.DisplayColumn.ColumnDescriptor.ReflectedPropertyDescriptor as
                    AnnotationPropertyDescriptor;
            if (null == annotationPropertyDescriptor)
            {
                return null;
            }
            return new[]{string.Empty}.Concat(annotationPropertyDescriptor.AnnotationDef.Items).ToArray();
        }
    }
}
