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
        private IViewContext _viewContext;
        private BindingSource _bindingSource;
        private string _waitingMsg = "Waiting for data...";
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

        public IViewContext ViewContext
        {
            get
            {
                return _viewContext;
            } 
            set
            {
                if (ReferenceEquals(_viewContext, value))
                {
                    return;
                }
                _viewContext = value;
                RefreshUi();
            }
        }

        public string WaitingMessage
        {
            get { return _waitingMsg; }
            set
            {
                _waitingMsg = value;
                RefreshUi();
            }
        }

        private BindingListView BindingListView 
        { 
            get
            {
                return BindingSource == null ? null : BindingSource.DataSource as BindingListView;
            } 
        }

        public string GetCurrentViewName()
        {
            var viewInfo = BindingListView == null ? null : BindingListView.ViewInfo;
            return viewInfo == null ? null : viewInfo.Name;
        }

        void BindingSource_ListChanged(object sender, EventArgs e)
        {
            RefreshUi();
        }

        void RefreshUi()
        {
            navBarButtonViews.Enabled = navBarButtonExport.Enabled = ViewContext != null && BindingListView != null && BindingListView.ViewInfo != null;
            if (BindingListView != null)
            {
                var queryResults = BindingListView.QueryResults;
                tbxFind.Enabled = true;
                if (queryResults.ResultRows != null)
                {
                    if (queryResults.Parameters.Rows == null)
                    {
                        lblFilterApplied.Text = string.Format("({0})", WaitingMessage);
                        lblFilterApplied.Visible = true;
                    }
                    else
                    {
                        if (queryResults.ResultRows.Count != queryResults.PivotedRows.Count)
                        {
                            lblFilterApplied.Text = string.Format("(Filtered from {0})", queryResults.PivotedRows.Count);
                            lblFilterApplied.Visible = true;
                        }
                        else
                        {
                            lblFilterApplied.Visible = false;
                        }
                    }
                }
                else
                {
                    lblFilterApplied.Text = "(Transforming data...)";
                    lblFilterApplied.Visible = true;
                }
            }
            else
            {
                tbxFind.Enabled = false;
                lblFilterApplied.Text = string.Format("({0})", WaitingMessage);
                lblFilterApplied.Visible = true;
            }
        }

        private void navBarButtonViews_DropDownOpening(object sender, EventArgs e)
        {
            var contextMenu = new ContextMenuStrip();
            var bindingSource = BindingSource;
            if (bindingSource != null)
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
            BindingListView.ViewSpec = viewSpec;
        }


        private void navBarButtonExport_Click(object sender, EventArgs e)
        {
            OnExport(this, e);
        }

        void OnCustomizeView(object sender, EventArgs eventArgs)
        {
            var newView = ViewContext.CustomizeView(this, BindingListView.ViewSpec);
            if (newView == null)
            {
                return;
            }
            BindingListView.ViewSpec = newView;
        }

        void OnManageViews(object sender, EventArgs eventArgs)
        {
            ViewContext.ManageViews(this);
        }

        void OnExport(object sender, EventArgs eventArgs)
        {
            ViewContext.Export(this, BindingListView);
        }

        private void findBox_TextChanged(object sender, EventArgs e)
        {
            UpdateFilter();
        }
        private void navBarButtonMatchCase_CheckedChanged(object sender, EventArgs e)
        {
            UpdateFilter();
        }

        private void UpdateFilter()
        {
            BindingListView.RowFilter = new RowFilter(tbxFind.Text, navBarButtonMatchCase.Checked);
        }
    }
}
