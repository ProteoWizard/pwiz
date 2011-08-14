/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace pwiz.Common.DataBinding.Controls
{
    /// <summary>
    /// Extension to <see cref="BindingNavigator"/> which also includes a button to customize the view,
    /// and a button to export the data to a CSV/TSV.
    /// </summary>
    public partial class NavBar : UserControl
    {
        private BindingSource _bindingSource;
        private IViewContext _viewContext;
        private ApplyRowFilterTask _applyRowFilterTask;
        private string _filterText = "";
        public NavBar()
        {
            InitializeComponent();
        }
        [TypeConverter(typeof(ReferenceConverter))]
        public BindingSource BindingSource
        {
            get
            {
                return _bindingSource;
            }
            set
            {
                if (BindingSource == value)
                {
                    return;
                }
                if (BindingSource != null)
                {
                    BindingSource.ListChanged -= BindingSource_ListChanged;
                }
                _bindingSource = value;
                bindingNavigator1.BindingSource = BindingSource;
                if (BindingSource != null)
                {
                    BindingSource.ListChanged += BindingSource_ListChanged;
                }
            }
        }

        [TypeConverter(typeof(ReferenceConverter))]
        public IViewContext ViewContext
        {
            get
            {
                return _viewContext;
            }
            set
            {
                if (ViewContext == value)
                {
                    return;
                }
                _viewContext = value;
                RefreshUi();
            }
        }


        public BindingListView GetBindingListView()
        {
            if (BindingSource == null)
            {
                return null;
            }
            return BindingSource.DataSource as BindingListView;
        }

        public string GetCurrentViewName()
        {
            return GetBindingListView().ViewName;
        }

        void BindingSource_ListChanged(object sender, EventArgs e)
        {
            RefreshUi();
        }

        void RefreshUi()
        {
            InvalidateRowFilter();
            var bindingListView = GetBindingListView();
            navBarButtonViews.Enabled = navBarButtonExport.Enabled = (bindingListView != null && ViewContext != null);
            if (bindingListView != null)
            {
                tbxFind.Enabled = true;
                if (bindingListView.Count < bindingListView.InnerList.Count)
                {
                    lblFilterApplied.Text = string.Format("(Filtered from {0})", bindingListView.InnerList.Count);
                    lblFilterApplied.Visible = true;
                }
                else if (_applyRowFilterTask != null)
                {
                    lblFilterApplied.Text = "(Filtering...)";
                    lblFilterApplied.Visible = true;
                }
                else
                {
                    lblFilterApplied.Visible = false;
                }
            }
            else
            {
                tbxFind.Enabled = false;
            }
        }

        private void InvalidateRowFilter()
        {
            if (_applyRowFilterTask != null)
            {
                _applyRowFilterTask.Dispose();
                _applyRowFilterTask = null;
            }
            var bindingListView = GetBindingListView();
            if (bindingListView == null)
            {
                return;
            }
            if (_filterText != tbxFind.Text)
            {
                var unfilteredRows = bindingListView.UnfilteredItems.ToArray();
                if (string.IsNullOrEmpty(tbxFind.Text))
                {
                    bindingListView.SetFilteredItems(unfilteredRows);
                    _filterText = "";
                }
                else
                {
                    _applyRowFilterTask = new ApplyRowFilterTask(unfilteredRows, 
                        bindingListView.GetItemProperties(new PropertyDescriptor[0]).Cast<PropertyDescriptor>().ToArray(), 
                        tbxFind.Text);
                    new Action(_applyRowFilterTask.FilterBackground).BeginInvoke(ApplyRowFilterTaskCallback, _applyRowFilterTask);
                }
            }
        }

        private void ApplyRowFilterTaskCallback(IAsyncResult result)
        {
            if (!result.IsCompleted)
            {
                return;
            }
            if (!ReferenceEquals(_applyRowFilterTask, result.AsyncState))
            {
                return;
            }
            if (InvokeRequired)
            {
                BeginInvoke(new Action<IAsyncResult>(ApplyRowFilterTaskCallback), result);
                return;
            }
            if (!ReferenceEquals(_applyRowFilterTask, result.AsyncState))
            {
                return;
            }
            try
            {
                var bindingListView = GetBindingListView();
                if (bindingListView == null)
                {
                    return;
                }
                bindingListView.SetFilteredItems(_applyRowFilterTask.FilteredRows.ToArray());
                _filterText = _applyRowFilterTask.FilterText;
            }
            finally
            {
                _applyRowFilterTask.Dispose();
                _applyRowFilterTask = null;
            }
            RefreshUi();
        }

        private void navBarButtonViews_DropDownOpening(object sender, EventArgs e)
        {
            var contextMenu = new ContextMenuStrip();
            var bindingListView = GetBindingListView();
            if (bindingListView != null)
            {
                var builtInViewItems = ViewContext.BuiltInViewSpecs.Select(viewSpec => NewChooseViewItem(viewSpec, FontStyle.Regular)).ToArray();
                if (builtInViewItems.Length > 0)
                {
                    contextMenu.Items.AddRange(builtInViewItems);
                    contextMenu.Items.Add(new ToolStripSeparator());
                }
                var customViewItems = ViewContext.CustomViewSpecs.Select(viewSpec => NewChooseViewItem(viewSpec, FontStyle.Italic)).ToArray();
                if (customViewItems.Length > 0)
                {
                    contextMenu.Items.AddRange(customViewItems);
                    contextMenu.Items.Add(new ToolStripSeparator());
                }
                contextMenu.Items.Add(new ToolStripMenuItem("Customize View...", null, OnCustomizeView));
                contextMenu.Items.Add(new ToolStripMenuItem("Manage Views...", null, OnManageViews));
            }
            navBarButtonViews.DropDown = contextMenu;

        }

        ToolStripItem NewChooseViewItem(ViewSpec viewSpec, FontStyle fontStyle)
        {
            var item = new ToolStripMenuItem(viewSpec.Name, null,
                (sender, args) => ApplyView(viewSpec));
            if (GetCurrentViewName() == viewSpec.Name)
            {
                fontStyle |= FontStyle.Bold;
            }
            if (fontStyle != item.Font.Style)
            {
                item.Font = new Font(item.Font, fontStyle);
            }
            return item;
        }

        public void ApplyView(ViewSpec viewSpec)
        {
            var bindingListView = BindingSource.DataSource as BindingListView;
            if (bindingListView == null)
            {
                return;
            }
            var newBindingListView = new BindingListView(new ViewInfo(ViewContext.ParentColumn, viewSpec), bindingListView.InnerList);
            BindingSource.DataSource = newBindingListView;
        }


        private void navBarButtonExport_Click(object sender, EventArgs e)
        {
            OnExport(this, e);
        }

        void OnCustomizeView(object sender, EventArgs eventArgs)
        {
            var bindingListView = GetBindingListView();
            if (bindingListView == null)
            {
                return;
            }
            var newView = ViewContext.CustomizeView(this, bindingListView.GetViewSpec());
            if (newView == null)
            {
                return;
            }
            // First clear out the DataSource so that the DataGridView removes all of its
            // columns.  Otherwise the DataGridView won't add the columns in the correct order.
            BindingSource.DataSource = new object[0];
            BindingSource.DataSource = new BindingListView(new ViewInfo(ViewContext.ParentColumn, newView),
                                                           bindingListView.InnerList);
        }

        void OnManageViews(object sender, EventArgs eventArgs)
        {
            ViewContext.ManageViews(this);
        }

        void OnExport(object sender, EventArgs eventArgs)
        {
            ViewContext.Export(this, GetBindingListView());
        }

        private void findBox_TextChanged(object sender, EventArgs e)
        {
            RefreshUi();
        }

        private void SetFilteredRowsNow(ApplyRowFilterTask applyRowFilterTask, IList<RowItem> rows)
        {
            try
            {
            }
            finally
            {
                applyRowFilterTask.Dispose();
            }
        }
    }
}
