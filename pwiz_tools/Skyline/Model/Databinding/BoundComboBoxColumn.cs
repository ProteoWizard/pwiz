/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Model.Databinding
{
    /// <summary>
    /// ComboBoxColumn which updates its item list in response to changes
    /// in document.
    /// </summary>
    public abstract class BoundComboBoxColumn : DataGridViewComboBoxColumn
    {
        private ColumnPropertyDescriptor _columnPropertyDescriptor;
        private readonly IDocumentChangeListener _documentChangeListener;

        protected BoundComboBoxColumn(DataGridViewComboBoxCell cellTemplate)
        {
            // ReSharper disable VirtualMemberCallInConstructor
            CellTemplate = cellTemplate;
            // ReSharper restore VirtualMemberCallInConstructor
            FlatStyle = FlatStyle.Flat;
            DisplayStyleForCurrentCellOnly = true;
            _documentChangeListener = new DocumentChangeListener(this);
        }

        protected BoundComboBoxColumn()
            : this(new BoundComboBoxCell())
        {
        }
        protected override void OnDataGridViewChanged()
        {
            base.OnDataGridViewChanged();
            ColumnPropertyDescriptor = GetColumnPropertyDescriptor();
        }

        public virtual void UpdateDropdownItems()
        {
            var newDropdownItems = GetDropdownItems();
            if (newDropdownItems.SequenceEqual(Items.Cast<object>()))
            {
                return;
            }
            Items.Clear();
            Items.AddRange(newDropdownItems);
        }

        protected abstract object[] GetDropdownItems();

        public ColumnPropertyDescriptor ColumnPropertyDescriptor
        {
            get { return _columnPropertyDescriptor; }
            set
            {
                if (ReferenceEquals(ColumnPropertyDescriptor, value))
                {
                    return;
                }
                if (SkylineDataSchema != null)
                {
                    SkylineDataSchema.Unlisten(_documentChangeListener);
                }
                _columnPropertyDescriptor = value;
                if (SkylineDataSchema != null)
                {
                    SkylineDataSchema.Listen(_documentChangeListener);
                    UpdateDropdownItems();
                }
            }
        }

        public override DataGridViewCell CellTemplate
        {
            get { return base.CellTemplate; }
            set
            {
                base.CellTemplate = value;
                var boundCellTemplate = CellTemplate as BoundComboBoxCell;
                if (boundCellTemplate != null)
                {
                    boundCellTemplate.BoundComboBoxColumn = this;
                }
            }
        }

        public SkylineDataSchema SkylineDataSchema
        {
            get
            {
                if (ColumnPropertyDescriptor == null)
                {
                    return null;
                }
                return ColumnPropertyDescriptor.DisplayColumn.DataSchema as SkylineDataSchema;
            }
        }

        private ColumnPropertyDescriptor GetColumnPropertyDescriptor()
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
            return bindingSource.GetItemProperties(null)[DataPropertyName] as ColumnPropertyDescriptor;
        }

        private class DocumentChangeListener : IDocumentChangeListener
        {
            private readonly BoundComboBoxColumn _boundComboBoxColumn;

            public DocumentChangeListener(BoundComboBoxColumn column)
            {
                _boundComboBoxColumn = column;
            }

            public void DocumentOnChanged(object sender, DocumentChangedEventArgs args)
            {
                _boundComboBoxColumn.UpdateDropdownItems();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                ColumnPropertyDescriptor = null;
            }
        }

        public class BoundComboBoxCell : DataGridViewComboBoxCell
        {
            public override ObjectCollection Items
            {
                get
                {
                    if (BoundComboBoxColumn == null || ReferenceEquals(this, BoundComboBoxColumn.CellTemplate))
                    {
                        return base.Items;
                    }
                    var items = new ObjectCollection(this);
                    items.AddRange(BoundComboBoxColumn.Items.Cast<object>().ToArray());
                    return items;
                }
            }

            public BoundComboBoxColumn BoundComboBoxColumn { get; set; }
        }
    }
}
