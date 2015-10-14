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
        private BindingListSource _bindingListSource;
        private string _waitingMsg;
        public NavBar()
        {
            InitializeComponent();

            _waitingMsg = Resources.NavBar_NavBar_Waiting_for_data___;
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

        [DefaultValue("Waiting for data...")]   // Not L10N
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
                var groups = new List<ViewGroup>{ViewGroup.BUILT_IN};
                groups.AddRange(ViewContext.ViewGroups.Except(groups));
                bool anyOtherGroups = false;

                foreach (var group in groups)
                {
                    List<ToolStripItem> items = new List<ToolStripItem>();
                    var viewSpecList = ViewContext.GetViewSpecList(group.Id);
                    if (!viewSpecList.ViewSpecs.Any())
                    {
                        continue;
                    }
                    foreach (var viewSpec in viewSpecList.ViewSpecs)
                    {
                        var item = NewChooseViewItem(group, viewSpec);
                        if (null != item)
                        {
                            items.Add(item);
                        }
                    }
                    if (!items.Any())
                    {
                        continue;
                    }
                    if (ViewGroup.BUILT_IN.Equals(group) || Equals(ViewContext.DefaultViewGroup, group))
                    {
                        contextMenu.Items.AddRange(items.ToArray());
                        contextMenu.Items.Add(new ToolStripSeparator());
                    }
                    else
                    {
                        var item = new ToolStripMenuItem(group.Label);
                        item.DropDownItems.AddRange(items.ToArray());
                        contextMenu.Items.Add(item);
                        anyOtherGroups = true;
                    }
                }
                if (anyOtherGroups)
                {
                    contextMenu.Items.Add(new ToolStripSeparator());
                }
                bool currentViewIsBuiltIn = ViewGroup.BUILT_IN.Equals(BindingListSource.ViewInfo.ViewGroup);
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

        ToolStripItem NewChooseViewItem(ViewGroup viewGroup, ViewSpec viewSpec)
        {
            var viewInfo = ViewContext.GetViewInfo(new ViewName(viewGroup.Id, viewSpec.Name));
            if (null == viewInfo)
            {
                return null;
            }
            Image image = null;
            int imageIndex = ViewContext.GetImageIndex(viewSpec);
            if (imageIndex >= 0)
            {
                image = ViewContext.GetImageList()[imageIndex];
            }
            ToolStripMenuItem item = new ToolStripMenuItem(viewSpec.Name, image) { ImageTransparentColor = Color.Magenta};

            item.Click += (sender, args) => ApplyView(viewGroup, viewSpec);
            var currentView = BindingListSource.ViewInfo;
            var fontStyle = FontStyle.Regular;
            if (null != currentView && Equals(viewGroup, currentView.ViewGroup) &&
                Equals(viewSpec.Name, currentView.Name))
            {
                fontStyle |= FontStyle.Bold;
                item.Checked = true;
            }
            if (!ViewGroup.BUILT_IN.Equals(viewGroup))
            {
                fontStyle |= FontStyle.Italic;
            }
            item.Font = new Font(item.Font, fontStyle);
            return item;
        }

        public void ApplyView(ViewGroup viewGroup, ViewSpec viewSpec)
        {
            var viewInfo = ViewContext.GetViewInfo(viewGroup, viewSpec);
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
            var viewGroup = BindingListSource.ViewInfo.ViewGroup;
            var viewSpec = BindingListSource.ViewSpec;
            if (Equals(viewGroup, ViewGroup.BUILT_IN))
            {
                viewSpec = viewSpec.SetName(string.Empty);
                viewGroup = ViewContext.DefaultViewGroup;
            }
            var newView = ViewContext.CustomizeView(this, viewSpec, viewGroup);
            if (newView == null)
            {
                return;
            }
            BindingListSource.SetViewContext(ViewContext, ViewContext.GetViewInfo(viewGroup, newView));
        }

        void OnCopyView(object sender, EventArgs eventArgs)
        {
            var viewGroup = BindingListSource.ViewInfo.ViewGroup;
            if (Equals(ViewGroup.BUILT_IN, viewGroup))
            {
                viewGroup = ViewContext.DefaultViewGroup;
            }
            var newView = ViewContext.CustomizeView(this, BindingListSource.ViewSpec.SetName(null), viewGroup);
            if (newView == null)
            {
                return;
            }
            BindingListSource.SetViewContext(ViewContext, ViewContext.GetViewInfo(viewGroup, newView));
        }

        void OnManageViews(object sender, EventArgs eventArgs)
        {
            ManageViews();
        }

        public void ManageViews()
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
            BindingListSource.RowFilter = BindingListSource.RowFilter.SetText(tbxFind.Text, navBarButtonMatchCase.Checked);
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

        private void NavBarButtonCopyAllOnClick(object sender, EventArgs e)
        {
            ViewContext.CopyAll(this, BindingListSource);
        }
    }
}
