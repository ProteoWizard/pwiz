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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    /// <summary>
    /// UI for adding, and removing custom views.
    /// </summary>
    public partial class ManageViewsForm : CommonFormEx
    {
        public ManageViewsForm(IViewContext viewContext)
        {
            InitializeComponent();
            ViewContext = viewContext;
            Icon = ViewContext.ApplicationIcon;
            chooseViewsControl1.ViewContext = viewContext;
            UpdateButtons();
        }

        public IViewContext ViewContext { get; private set; }

        private void BtnEditOnClick(object sender, EventArgs e)
        {
            EditView();
        }

        public void EditView()
        {
            var viewInfo = ViewContext.GetViewInfo(chooseViewsControl1.SelectedViewName.GetValueOrDefault());
            if (null == viewInfo)
            {
                return;
            }
            ViewContext.CustomizeView(this, viewInfo.GetViewSpec(), viewInfo.ViewGroup);
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddView();
        }

        public void AddView()
        {
            var newView = ViewContext.NewView(this, chooseViewsControl1.SelectedGroup);
            if (null != newView)
            {
                chooseViewsControl1.SelectView(newView.Name);
            }
        }

        private void BtnRemoveOnClick(object sender, EventArgs e)
        {
            Remove(true);
        }

        public void Remove(bool prompt)
        {
            var selectedViewNames = new HashSet<ViewName>(chooseViewsControl1.SelectedViews);
            if (selectedViewNames.Count == 0)
            {
                return;
            }
            string message;
            if (selectedViewNames.Count == 1)
            {
                message = string.Format(Resources.ManageViewsForm_BtnRemoveOnClick_Are_you_sure_you_want_to_delete_the_view___0___, selectedViewNames.First().Name);
            }
            else
            {
                message = string.Format(Resources.ManageViewsForm_BtnRemoveOnClick_Are_you_sure_you_want_to_delete_these__0__views_, selectedViewNames.Count);
            }
            if (prompt && ViewContext.ShowMessageBox(this, message, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
            {
                return;
            }
            var namesByGroup = selectedViewNames.ToLookup(name => name.GroupId, name => name.Name);
            foreach (var grouping in namesByGroup)
            {
                ViewContext.DeleteViews(grouping.Key, grouping);
            }
            
        }
        private void btnShare_Click(object sender, EventArgs e)
        {
            ExportViews(null);
        }

        public void ExportViews(string filename)
        {
            var viewSpecList = ViewContext.GetViewSpecList(chooseViewsControl1.SelectedGroup.Id);
            var selectedViewNames = new HashSet<string>(chooseViewsControl1.SelectedViews.Select(viewName => viewName.Name));
            viewSpecList = viewSpecList.Filter(view => selectedViewNames.Contains(view.Name));
            if (null == filename)
            {
                ViewContext.ExportViews(this, viewSpecList);
            }
            else
            {
                ViewContext.ExportViewsToFile(this, viewSpecList, filename);
            }
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            ViewContext.ImportViews(this, chooseViewsControl1.SelectedGroup);
        }

        public void ImportViews(string filename)
        {
            ViewContext.ImportViewsFromFile(this, chooseViewsControl1.SelectedGroup, filename);
        }

        public bool AddButtonVisible
        {
            get { return btnAdd.Visible; }
            set { btnAdd.Visible = value; }
        }

        public bool EditButtonVisible
        {
            get { return btnEdit.Visible; }
            set { btnEdit.Visible = value; }
        }

        public bool RemoveButtonVisible
        {
            get { return btnRemove.Visible; }
            set { btnRemove.Visible = value; }
        }

        public bool ShareButtonVisible
        {
            get { return btnShare.Visible; }
            set { btnShare.Visible = value; }
        }

        public void SelectView(string viewName)
        {
            chooseViewsControl1.SelectView(viewName);
        }

        public void SelectViews(IEnumerable<string> viewNames)
        {
            chooseViewsControl1.SelectViews(viewNames);
        }

        private void chooseViewsControl1_SelectionChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        public void UpdateButtons()
        {
            btnCopy.Enabled = btnShare.Enabled = btnRemove.Enabled = chooseViewsControl1.SelectedViews.Any();
            btnEdit.Enabled = CanEdit(chooseViewsControl1.SelectedViewName);
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            ShowCopyContextMenu();
        }

        private bool CanEdit(ViewName? viewName)
        {
            if (!viewName.HasValue)
            {
                return false;
            }
            var viewContext = chooseViewsControl1.ViewContext;
            var viewSpec = viewContext.GetViewSpecList(viewName.Value.GroupId)?.GetView(viewName.Value.Name);
            return viewSpec != null && viewContext.GetRowSourceInfo(viewSpec) != null;
        }

        public void ShowCopyContextMenu()
        {
            openViewEditorContextMenuItem.Enabled = CanEdit(chooseViewsControl1.SelectedViewName);
            copyToGroupContextMenuItem.DropDownItems.Clear();
            foreach (var group in ViewContext.ViewGroups)
            {
                if (!Equals(group, chooseViewsControl1.SelectedGroup))
                {
                    copyToGroupContextMenuItem.DropDownItems.Add(NewCopyToGroupMenuItem(group));
                }
            }
            contextMenuStrip1.Show(btnCopy.Parent, btnCopy.Left, btnCopy.Bottom + 1);
        }

        private ToolStripMenuItem NewCopyToGroupMenuItem(ViewGroup group)
        {
            return new ToolStripMenuItem(group.Label, null, (sender, args) => CopyToGroup(group));
        }

        public void CopyToGroup(ViewGroup group)
        {
            var selectedNames = new HashSet<ViewName>(chooseViewsControl1.SelectedViews);
            var currentGroupId = chooseViewsControl1.SelectedGroup.Id;
            var selectedViews = ViewContext.GetViewSpecList(chooseViewsControl1.SelectedGroup.Id)
                .Filter(view => selectedNames.Contains(currentGroupId.ViewName(view.Name)));
            ViewContext.CopyViewsToGroup(this, group, selectedViews);
        }

        public void CopyView()
        {
            var viewInfo = ViewContext.GetViewInfo(chooseViewsControl1.SelectedViewName.GetValueOrDefault());
            if (null == viewInfo)
            {
                return;
            }
            ViewContext.CustomizeView(this, viewInfo.GetViewSpec().SetName(null), viewInfo.ViewGroup);
        }

        private void openViewEditorContextMenuItem_Click(object sender, EventArgs e)
        {
            CopyView();
        }

        public void OkDialog()
        {
            Close();
        }
    }
}
