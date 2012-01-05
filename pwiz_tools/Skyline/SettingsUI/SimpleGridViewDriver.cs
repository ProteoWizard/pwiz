/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.SettingsUI
{
    public abstract class SimpleGridViewDriver<TItem>
    {
        private readonly DataGridViewEx _gridView;

        protected SimpleGridViewDriver(DataGridViewEx gridView, BindingSource bindingSource, SortableBindingList<TItem> items)
        {
            _gridView = gridView;
            _gridView.DataGridViewKey += gridView_KeyDown;

            Items = items;
            bindingSource.DataSource = items;
        }

        protected DataGridView GridView { get { return _gridView; } }

        protected Control MessageParent { get { return FormEx.GetParentForm(GridView); } }

        public SortableBindingList<TItem> Items { get; private set; }

        private void gridView_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle Ctrl + V for paste
            if (e.KeyCode == Keys.V && e.Control)
            {
                OnPaste();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                if (_gridView.IsCurrentCellInEditMode || _gridView.IsCurrentRowDirty)
                {
                    _gridView.CancelEdit();
                    _gridView.EndEdit();
                    e.Handled = true;
                }
            }
        }

        public void OnPaste()
        {
            DoPaste();
        }

        protected abstract void DoPaste();
    }
}
