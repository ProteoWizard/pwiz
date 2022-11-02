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
using pwiz.Common.DataBinding.Clustering;
using pwiz.Common.DataBinding.Internal;
using pwiz.Common.DataBinding.Layout;
using pwiz.Common.Properties;
using pwiz.Common.SystemUtil;

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
                    BindingListSource.AllRowsChanged -= BindingSourceOnListChanged;
                    BindingListSource.CurrentChanged -= BindingSourceOnCurrentChanged;
                }
                _bindingListSource = value;
                bindingNavigator1.BindingSource = BindingListSource;
                if (BindingListSource != null)
                {
                    BindingListSource.ListChanged += BindingSourceOnListChanged;
                    BindingListSource.AllRowsChanged += BindingSourceOnListChanged;
                    BindingListSource.CurrentChanged += BindingSourceOnCurrentChanged;
                }
            }
        }

        public BindingNavigator BindingNavigator { get { return bindingNavigator1; }}

        public IViewContext ViewContext
        {
            get { return BindingListSource == null ? null : BindingListSource.ViewContext; } 
        }

        [DefaultValue(@"Waiting for data...")]
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
        }

        void RefreshUi()
        {
            navBarButtonViews.Enabled = navBarButtonExport.Enabled = ViewContext != null && BindingListView != null && BindingListView.ViewInfo != null;
            navBarButtonActions.Visible = ViewContext != null && ViewContext.HasRowActions;
            navBarButtonClusterGrid.Checked = BindingListSource?.ClusteringSpec != null;
            if (BindingListSource != null && BindingListView != null)
            {
                var queryResults = BindingListView.QueryResults;
                tbxFind.Enabled = true;
                if (queryResults.ResultRows != null)
                {
                    if (queryResults.SourceRows == null)
                    {
                        lblFilterApplied.Text = string.Format(@"({0})", WaitingMessage);
                        lblFilterApplied.Visible = true;
                    }
                    else
                    {
                        if (!BindingListSource.IsComplete)
                        {
                            lblFilterApplied.Text = Resources.NavBar_RefreshUi_Transforming_data___;
                            lblFilterApplied.Visible = true;
                        }
                        else
                        {
                            bool filterApplied = false;
                            if (queryResults.TransformResults.RowTransform is RowFilter)
                            {
                                int filteredCount = queryResults.TransformResults.PivotedRows.RowCount;
                                int unfilteredCount = queryResults.TransformResults.Parent.PivotedRows.RowCount;
                                if (filteredCount != unfilteredCount)
                                {
                                    lblFilterApplied.Text = string.Format(Resources.NavBar_RefreshUi__Filtered_from__0__, unfilteredCount);
                                    lblFilterApplied.Visible = true;
                                    filterApplied = true;
                                }
                            }
                            if (!filterApplied)
                            {
                                lblFilterApplied.Visible = false;
                            }
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
                lblFilterApplied.Text = string.Format(@"({0})", WaitingMessage);
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
                        if (!ViewContext.CanDisplayView(viewSpec))
                        {
                            continue;
                        }

                        items.Add(NewChooseViewItem(group, viewSpec));
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

        private ViewName GetViewName()
        {
            var viewInfo = BindingListView.ViewInfo;
            if (viewInfo == null)
            {
                return default(ViewName);
            }
            if (viewInfo.ViewGroup == null)
            {
                return new ViewName(default(ViewGroupId), viewInfo.Name);
            }
            return viewInfo.ViewGroup.Id.ViewName(viewInfo.Name);
        }

        ToolStripItem NewChooseViewItem(ViewGroup viewGroup, ViewSpec viewSpec)
        {
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

        private void btnGroupTotal_Click(object sender, EventArgs e)
        {
            ShowPivotDialog(false);
        }

        public void ShowPivotDialog(bool alwaysAddNewLevel)
        {
            if (null == BindingListSource)
            {
                return;
            }
            PivotEditor.ShowPivotEditor(FormUtil.FindTopLevelOwner(this), BindingListSource, alwaysAddNewLevel);
        }

        private void UpdateGroupTotalDropdown()
        {
            ViewLayoutList layouts;
            TransformStack transformStack;
            DataSchema dataSchema;
            ViewName viewName = GetViewName();
            if (ViewContext == null || BindingListSource == null)
            {
                layouts = ViewLayoutList.EMPTY;
                transformStack = TransformStack.EMPTY;
                dataSchema = null;
            }
            else
            {
                layouts = ViewContext.GetViewLayoutList(GetViewName());
                transformStack = BindingListSource.BindingListView.TransformStack;
                dataSchema = BindingListSource.ViewContext.DataSchema;
            }
            btnGroupTotal.DropDownItems.Clear();
            if (!layouts.IsEmpty)
            {
                foreach (var layout in layouts.Layouts)
                {
                    btnGroupTotal.DropDownItems.Add(MakeLayoutMenuItem(layout));
                }
                btnGroupTotal.DropDownItems.Add(new ToolStripSeparator());
            }
            if (!transformStack.IsEmpty && dataSchema != null)
            {
                var transformsMenuItem = new ToolStripMenuItem(Resources.NavBar_UpdateGroupTotalDropdown_Transforms);
                for (int i = 0; i <= transformStack.RowTransforms.Count; i++)
                {
                    var curTransform = transformStack.ChangeStackIndex(i);
                    var curMenuItem = MakeTransformStackMenuItem(dataSchema, curTransform);
                    if (i == transformStack.StackIndex)
                    {
                        curMenuItem.Checked = true;
                    }
                    transformsMenuItem.DropDownItems.Add(curMenuItem);
                }
                btnGroupTotal.DropDownItems.Add(transformsMenuItem);
            }
            btnGroupTotal.DropDownItems.Add(new ToolStripMenuItem(Resources.NavBar_UpdateGroupTotalDropdown_New_Pivot___, null, (sender, args) =>
            {
                ShowPivotDialog(true);
            }));
            if (transformStack.CurrentTransform is PivotSpec)
            {
                btnGroupTotal.DropDownItems.Add(new ToolStripMenuItem(Resources.NavBar_UpdateGroupTotalDropdown_Modify_Current_Pivot___, null, (sender, args) =>
                {
                    ShowPivotDialog(false);
                }));
            }
            if (CanRememberView(viewName.GroupId))
            {
                btnGroupTotal.DropDownItems.Add(new ToolStripMenuItem(Resources.NavBar_UpdateGroupTotalDropdown_Remember_current_layout___, null, (sender, args) =>
                {
                    RememberCurrentLayout();
                }));
            }
            if (layouts.Layouts.Any())
            {
                btnGroupTotal.DropDownItems.Add(
                    new ToolStripMenuItem(Resources.NavBar_UpdateGroupTotalDropdown_Manage_layouts___, null, (sender, args) => ManageLayouts()));
            }
        }

        private bool CanRememberView(ViewGroupId viewGroupId)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            return !Equals(viewGroupId, default(ViewGroupId)) && !Equals(viewGroupId, ViewGroup.BUILT_IN.Id);
        }

        public void RememberCurrentLayout()
        {
            if (ViewContext == null || BindingListSource == null)
            {
                return;
            }
            var viewSpec = BindingListSource.ViewSpec;
            if (viewSpec == null)
            {
                return;
            }
            var viewName = GetViewName();
            if (viewName.GroupId.Equals(ViewGroup.BUILT_IN.Id) || viewName.GroupId.Equals(default(ViewGroupId)))
            {
                return;
            }
            var viewLayouts = ViewContext.GetViewLayoutList(GetViewName());
            using (var dlg = new NameLayoutForm(ViewContext,
                new HashSet<string>(viewLayouts.Layouts.Select(layout => layout.Name))))
            {
                if (dlg.ShowDialog(FormUtil.FindTopLevelOwner(this)) == DialogResult.OK)
                {
                    var newLayout = new ViewLayout(dlg.LayoutName);
                    newLayout = PopulateLayout(newLayout);
                    var newLayouts = viewLayouts.Layouts.Where(layout => layout.Name != dlg.LayoutName).Concat(new[]
                    {
                        newLayout
                    });
                    viewLayouts = viewLayouts.ChangeLayouts(newLayouts);
                    if (dlg.MakeDefault)
                    {
                        viewLayouts = viewLayouts.ChangeDefaultLayoutName(dlg.LayoutName);
                    }
                    ViewContext.SetViewLayoutList(viewName.GroupId, viewLayouts);
                }
            }
        }

        public void ManageLayouts()
        {
            if (ViewContext == null || BindingListSource == null)
            {
                return;
            }
            var viewName = GetViewName();
            var viewLayouts = ViewContext.GetViewLayoutList(viewName);
            using (var dlg = new ManageLayoutsForm())
            {
                dlg.ViewLayoutList = viewLayouts;
                if (dlg.ShowDialog(FormUtil.FindTopLevelOwner(this)) == DialogResult.OK)
                {
                    ViewContext.SetViewLayoutList(viewName.GroupId, dlg.ViewLayoutList);
                }
            }
        }

        /// <summary>
        /// Fill in the ViewLayout with the current set of transformations, column widths, formats, etc.
        /// </summary>
        public ViewLayout PopulateLayout(ViewLayout newLayout)
        {
            var transformStack = BindingListView.TransformStack;
            newLayout = newLayout.ChangeRowTransforms(
                transformStack.RowTransforms.Skip(transformStack.StackIndex));
            var columnIds = new HashSet<ColumnId>();
            var columnFormats = new List<Tuple<ColumnId, ColumnFormat>>();
            foreach (var pd in BindingListSource.ItemProperties)
            {
                var columnId = ColumnId.GetColumnId(pd);
                if (columnId == null)
                {
                    continue;
                }
                if (!columnIds.Add(columnId))
                {
                    continue;
                }
                var columnFormat = BindingListSource.ColumnFormats.GetFormat(columnId);
                if (columnFormat.IsEmpty)
                {
                    continue;
                }
                columnFormats.Add(Tuple.Create(columnId, columnFormat));
            }
            newLayout = newLayout.ChangeColumnFormats(columnFormats);
            newLayout = newLayout.ChangeClusterSpec(BindingListSource.ClusteringSpec);
            return newLayout;
        }

        private ToolStripMenuItem MakeLayoutMenuItem(ViewLayout viewLayout)
        {
            return new ToolStripMenuItem(viewLayout.Name, null, (sender, args) =>
            {
                BindingListSource.ApplyLayout(viewLayout);
            });
        }

        private ToolStripMenuItem MakeTransformStackMenuItem(DataSchema dataSchema, TransformStack transformStack)
        {
            var toolStripMenuItem = new ToolStripMenuItem();
            if (transformStack.CurrentTransform == null)
            {
                toolStripMenuItem.Text = Resources.NavBar_MakeTransformStackMenuItem_No_Transforms;
            }
            else
            {
                toolStripMenuItem.Text = transformStack.CurrentTransform.Summary;
                toolStripMenuItem.ToolTipText = transformStack.CurrentTransform.GetDescription(dataSchema);
                if (transformStack.CurrentTransform is PivotSpec)
                {
                    toolStripMenuItem.Image = Resources.Pivot;
                }
                else if (transformStack.CurrentTransform is RowFilter)
                {
                    toolStripMenuItem.Image = Resources.Filter;
                }
                toolStripMenuItem.ImageTransparentColor = Color.Magenta;
            }
            toolStripMenuItem.Click += (sender, args) =>
            {
                BindingListSource.BindingListView.TransformStack = transformStack;
            };
            return toolStripMenuItem;
        }

        private void btnGroupTotal_DropDownOpening(object sender, EventArgs e)
        {
            UpdateGroupTotalDropdown();
        }

        private void bindingNavigatorAddNewItem_Click(object sender, EventArgs e)
        {
            BindingListSource.AddNew();
        }

        private void navBarButtonActions_DropDownOpening(object sender, EventArgs e)
        {
            if (ViewContext != null)
            {
                ViewContext.RowActionsDropDownOpening(navBarButtonActions.DropDownItems);
            }
        }

        public ToolStripDropDownButton ActionsButton
        {
            get { return navBarButtonActions; }
        }
        public ToolStripDropDownButton ReportsButton
        {
            get { return navBarButtonViews; }
        }

        private void navBarButtonCluster_ButtonClick(object sender, EventArgs e)
        {
            if (null != BindingListSource.ClusteringSpec && !BindingListSource.IsComplete &&
                !(BindingListSource.ReportResults is ClusteredReportResults))
            {
                return;
            }
            BindingListSource.ViewContext.ToggleClustering(BindingListSource, BindingListSource.ClusteringSpec == null);
        }

        private void navBarButtonClusterGrid_Click(object sender, EventArgs e)
        {
            BindingListSource.ViewContext.ToggleClustering(BindingListSource, !navBarButtonClusterGrid.Checked);
        }

        public ToolStripSplitButton ClusterSplitButton
        {
            get { return navBarButtonCluster; }
        }

        private void navBarButtonCluster_DropDownOpening(object sender, EventArgs e)
        {
            // clusterRowsToolStripMenuItem.Checked = true == BindingListSource?.ClusteringSpec?.ClusterRows;
            // clusterColumnsToolStripMenuItem.Checked = true == BindingListSource?.ClusteringSpec?.ClusterColumns;
        }

        private void advancedToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowClusteringEditor();
        }

        public void ShowClusteringEditor()
        {
            var reportResults = BindingListSource?.ReportResults;
            var dataSchema = BindingListSource?.ViewInfo.DataSchema;
            if (reportResults == null || dataSchema == null)
            {
                return;
            }

            using (var clusteringEditor = new ClusteringEditor())
            {
                clusteringEditor.SetData(dataSchema, reportResults, BindingListSource.ClusteringSpec);
                if (clusteringEditor.ShowDialog(FormUtil.FindTopLevelOwner(this)) == DialogResult.OK)
                {
                    BindingListSource.ClusteringSpec = clusteringEditor.GetClusteringSpec();
                }
            }
        }
    }
}
