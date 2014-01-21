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
using pwiz.Common.DataBinding.Internal;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding.Controls
{
    /// <summary>
    /// Extension to <see cref="BindingNavigator"/> which also includes a button to customize the view,
    /// and a button to export the data to a CSV/TSV.
    /// </summary>
    public partial class NavBar : UserControl
    {
        private const string DefaultWaitingMessage = "Waiting for data...";
        private BindingListSource _bindingListSource;
        private string _waitingMsg = DefaultWaitingMessage;
        public NavBar()
        {
            InitializeComponent();
        }
        [TypeConverter(typeof(ReferenceConverter))]
        public BindingListSource BindingListSource
        {
            get
            {
                return _bindingListSource;
            }
            set
            {
                if (BindingListSource == value)
                {
                    return;
                }
                if (BindingListSource != null)
                {
                    BindingListSource.ListChanged -= BindingSourceOnListChanged;
                    BindingListSource.CurrentChanged -= BindingSourceOnCurrentChanged;
                }
                _bindingListSource = value;
                bindingNavigator1.BindingSource = BindingListSource;
                if (BindingListSource != null)
                {
                    BindingListSource.ListChanged += BindingSourceOnListChanged;
                    BindingListSource.CurrentChanged += BindingSourceOnCurrentChanged;
                }
            }
        }

        public BindingNavigator BindingNavigator { get { return bindingNavigator1; }}

        public IViewContext ViewContext
        {
            get { return BindingListSource == null ? null : BindingListSource.ViewContext; } 
        }

        [DefaultValue(DefaultWaitingMessage)]
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
                return BindingListSource == null ? null : BindingListSource.DataSource as BindingListView;
            } 
        }

        public string GetCurrentViewName()
        {
            var viewInfo = BindingListView == null ? null : BindingListView.ViewInfo;
            return viewInfo == null ? null : viewInfo.Name;
        }

        void BindingSourceOnListChanged(object sender, EventArgs e)
        {
            RefreshUi();
        }

        void BindingSourceOnCurrentChanged(object sender, EventArgs e)
        {
            navBarDeleteItem.Visible = navBarDeleteItem.Enabled = ViewContext.DeleteEnabled;
            bindingNavigatorAddNewItem.Visible = BindingListSource.AllowNew;
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
                    if (queryResults.SourceRows == null)
                    {
                        lblFilterApplied.Text = string.Format("({0})", WaitingMessage); // Not L10N
                        lblFilterApplied.Visible = true;
                    }
                    else
                    {
                        if (!BindingListSource.IsComplete)
                        {
                            lblFilterApplied.Text = Resources.NavBar_RefreshUi_Transforming_data___;
                            lblFilterApplied.Visible = true;
                        }
                        else if (queryResults.ResultRows.Count != queryResults.PivotedRows.Count)
                        {
                            lblFilterApplied.Text = string.Format(Resources.NavBar_RefreshUi__Filtered_from__0__, queryResults.PivotedRows.Count);
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
                    lblFilterApplied.Text = Resources.NavBar_RefreshUi__Transforming_data____;
                    lblFilterApplied.Visible = true;
                }
            }
            else
            {
                tbxFind.Enabled = false;
                lblFilterApplied.Text = string.Format("({0})", WaitingMessage); // Not L10N
                lblFilterApplied.Visible = true;
            }
        }

        private void NavBarButtonViewsOnDropDownOpening(object sender, EventArgs e)
        {
            var contextMenu = new ContextMenuStrip();
            var bindingSource = BindingListSource;
            if (bindingSource != null && ViewContext != null)
            {
                bool currentViewIsBuiltIn = ViewContext.BuiltInViews.Select(view => view.Name)
                    .Contains(GetCurrentViewName());
                var builtInViewItems = ViewContext.BuiltInViews.Select(viewInfo => NewChooseViewItem(viewInfo, FontStyle.Regular)).ToArray();
                if (builtInViewItems.Length > 0)
                {
                    contextMenu.Items.AddRange(builtInViewItems);
                    contextMenu.Items.Add(new ToolStripSeparator());
                }
                var customViewItems = ViewContext.CustomViews.Select(viewInfo => NewChooseViewItem(viewInfo, FontStyle.Italic)).ToArray();
                if (customViewItems.Length > 0)
                {
                    contextMenu.Items.AddRange(customViewItems);
                    contextMenu.Items.Add(new ToolStripSeparator());
                }
                if (currentViewIsBuiltIn)
                {
                    contextMenu.Items.Add(new ToolStripMenuItem(Resources.NavBar_NavBarButtonViewsOnDropDownOpening_Customize_View, null, OnCopyView));
                }
                else
                {
                    contextMenu.Items.Add(new ToolStripMenuItem(Resources.NavBar_NavBarButtonViewsOnDropDownOpening_Edit_View___, null, OnEditView));
                }
                contextMenu.Items.Add(new ToolStripMenuItem(Resources.NavBar_NavBarButtonViewsOnDropDownOpening_Manage_Views___, null, OnManageViews));
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
            var viewInfo = ViewContext.GetViewInfo(viewSpec);
            BindingListSource.SetViewContext(ViewContext, viewInfo);
            RefreshUi();
        }


        private void NavBarButtonExportOnClick(object sender, EventArgs e)
        {
            OnExport(this, e);
        }

        void OnEditView(object sender, EventArgs eventArgs)
        {
            CustomizeView();
        }

        public void CustomizeView()
        {
            var newView = ViewContext.CustomizeView(this, BindingListSource.ViewSpec);
            if (newView == null)
            {
                return;
            }
            BindingListSource.SetViewSpec(newView);
        }

        void OnCopyView(object sender, EventArgs eventArgs)
        {
            var newView = ViewContext.CopyView(this, BindingListSource.ViewSpec);
            if (null != newView)
            {
                BindingListSource.SetViewSpec(newView);
            }
        }

        void OnManageViews(object sender, EventArgs eventArgs)
        {
            ViewContext.ManageViews(this);
        }

        void OnExport(object sender, EventArgs eventArgs)
        {
            ViewContext.Export(this, BindingListSource);
        }

        private void FindBoxOnTextChanged(object sender, EventArgs e)
        {
            UpdateFilter();
        }
        private void NavBarButtonMatchCaseOnCheckedChanged(object sender, EventArgs e)
        {
            UpdateFilter();
        }

        private void UpdateFilter()
        {
            BindingListView.RowFilter = new RowFilter(tbxFind.Text, navBarButtonMatchCase.Checked);
        }

        private void NavBarDeleteItemOnClick(object sender, EventArgs e)
        {
            ViewContext.Delete();
        }

        public bool ShowViewsButton
        {
            get
            {
                return navBarButtonViews.Visible;
            }
            set
            {
                navBarButtonViews.Visible = value;
            }
        }
    }
}
