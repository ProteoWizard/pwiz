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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Controls;

namespace pwiz.Common.DataBinding.Controls.Editor
{
    /// <summary>
    /// UI for adding, and removing custom views.
    /// </summary>
    public partial class ManageViewsForm : CommonFormEx
    {
        private ViewSpec[] _views = new ViewSpec[0];
        public ManageViewsForm(IViewContext viewContext)
        {
            InitializeComponent();
            ViewContext = viewContext;
            Icon = ViewContext.ApplicationIcon;
            RefreshUi(true);
        }

        public IViewContext ViewContext { get; private set; }

        public void RefreshUi(bool reloadSettings)
        {
            if (reloadSettings)
            {
                _views = ViewContext.CustomViews.ToArray();
                ListViewHelper.ReplaceItems(listView1, _views.Select(vs=>new ListViewItem(vs.Name)).ToArray());
            }
            btnExportData.Enabled = btnCopy.Enabled = btnEdit.Enabled = listView1.SelectedIndices.Count == 1;
            btnShare.Enabled = btnRemove.Enabled = listView1.SelectedIndices.Count > 0;
            btnUp.Enabled = ListViewHelper.IsMoveUpEnabled(listView1);
            btnDown.Enabled = ListViewHelper.IsMoveDownEnabled(listView1);
            AfterResizeListView();
        }

        private void BtnEditOnClick(object sender, EventArgs e)
        {
            ViewContext.CustomizeView(this, _views[listView1.SelectedIndices[0]]);
            RefreshUi(true);
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddView();
        }

        public void AddView()
        {
            ViewContext.NewView(this);
            RefreshUi(true);
        }

        private void ListView1OnSelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshUi(false);
        }

        private void BtnRemoveOnClick(object sender, EventArgs e)
        {
            var selectedItems = listView1.SelectedIndices.Cast<int>().Select(i => _views[i]).ToArray();
            if (selectedItems.Length == 0)
            {
                return;
            }
            string message;
            if (selectedItems.Length == 1)
            {
                message = string.Format("Are you sure you want to delete the view '{0}'?", selectedItems[0].Name);
            }
            else
            {
                message = string.Format("Are you sure you want to delete these {0} views?", selectedItems.Length);
            }
            if (ViewContext.ShowMessageBox(this, message, MessageBoxButtons.OKCancel) == DialogResult.Cancel)
            {
                return;
            }
            ViewContext.DeleteViews(selectedItems);
            RefreshUi(true);
        }

        private void listView1_SizeChanged(object sender, EventArgs e)
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(AfterResizeListView));
            }
        }

        private void AfterResizeListView()
        {
            listView1.Columns[0].Width = listView1.ClientSize.Width
                        - SystemInformation.VerticalScrollBarWidth - 1;
        }

        private void btnShare_Click(object sender, EventArgs e)
        {
            var selectedItems = listView1.SelectedIndices.Cast<int>().Select(i => _views[i]).ToArray();
            ViewContext.ExportViews(this, selectedItems);
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            ViewContext.ImportViews(this);
            RefreshUi(true);
        }

        private void btnExportData_Click(object sender, EventArgs e)
        {
            if (listView1.SelectedIndices.Count == 0)
            {
                return;
            }
            var view = _views[listView1.SelectedIndices[0]];
            ViewContext.Export(this, ViewContext.ExecuteQuery(this, view));
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

        public bool ExportDataButtonVisible
        {
            get { return btnExportData.Visible; }
            set { btnExportData.Visible = value; }
        }

        public bool UpDownButtonsVisible
        {
            get
            {
                return panelUpDown.Visible;
            }
            set
            {
                panelUpDown.Visible = value;
            }
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            MoveViews(true);
        }


        public void MoveViews(bool up)
        {
            var newViews = ListViewHelper.MoveItems(_views, listView1.SelectedIndices.Cast<int>(), up);
            var newSelectedIndices = ListViewHelper.MoveSelectedIndexes(_views.Length,
                listView1.SelectedIndices.Cast<int>(), up);
            ViewContext.CustomViews = newViews;
            RefreshUi(true);
            ListViewHelper.SelectIndexes(listView1, newSelectedIndices);
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            MoveViews(false);
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            CopyView();
        }

        public void CopyView()
        {
            if (listView1.SelectedIndices.Count != 1)
            {
                return;
            }
            ViewContext.CopyView(this, _views[listView1.SelectedIndices[0]]);
            RefreshUi(true);
        }
    }
}
