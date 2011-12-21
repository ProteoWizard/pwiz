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

namespace pwiz.Common.DataBinding.Controls
{
    /// <summary>
    /// UI for adding, and removing custom views.
    /// </summary>
    public partial class ManageViewsForm : Form
    {
        private ViewSpec[] _viewSpecs = new ViewSpec[0];
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
                _viewSpecs = ViewContext.CustomViewSpecs.ToArray();
                ListViewHelper.ReplaceItems(listView1, _viewSpecs.Select(vs=>new ListViewItem(vs.Name)).ToArray());
            }
            btnEdit.Enabled = listView1.SelectedIndices.Count == 1;
            btnRemove.Enabled = listView1.SelectedIndices.Count > 0;
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            var newView = ViewContext.CustomizeView(this, new ViewSpec());
            if (newView != null)
            {
                newView = ViewContext.SaveView(newView);
            }
            RefreshUi(true);
            if (newView != null)
            {
                ListViewHelper.SelectIndex(listView1, Array.IndexOf(_viewSpecs, newView));
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            RefreshUi(true);
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            ViewContext.CustomizeView(this, _viewSpecs[listView1.SelectedIndices[0]]);
            RefreshUi(true);
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshUi(false);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            var selectedItems = listView1.SelectedIndices.Cast<int>().Select(i => _viewSpecs[i]).ToArray();
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

        private void listView1_Resize(object sender, EventArgs e)
        {
            colHdrName.Width = listView1.ClientSize.Width;
        }
    }
}
