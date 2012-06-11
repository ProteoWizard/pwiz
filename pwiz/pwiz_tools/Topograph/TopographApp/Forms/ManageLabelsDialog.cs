/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding.Controls;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.ui.Forms
{
    public partial class ManageLabelsDialog : WorkspaceForm
    {
        public ManageLabelsDialog(Workspace workspace) : base(workspace)
        {
            InitializeComponent();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RefreshUi(true);
        }

        public void RefreshUi(bool refreshList)
        {
            if (refreshList)
            {
                ListViewHelper.ReplaceItems(listView1, GetTracerDefs().Select(tracerDef=>MakeListViewItem(tracerDef)).ToArray());
            }
            btnEdit.Enabled = listView1.SelectedItems.Count == 1;
            btnRemove.Enabled = listView1.SelectedItems.Count > 0;
        }

        private IList<DbTracerDef> GetTracerDefs()
        {
            var array = Workspace.GetDbTracerDefs().ToArray();
            Array.Sort(array, (dbTracerDef1, dbTracerDef2) => dbTracerDef1.Name.CompareTo(dbTracerDef2.Name));
            return array;
        }

        private ListViewItem MakeListViewItem(DbTracerDef tracerDef)
        {
            return new ListViewItem(tracerDef.Name);
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            var firstSelectedItem = listView1.SelectedItems.Cast<ListViewItem>().FirstOrDefault();
            if (firstSelectedItem == null)
            {
                return;
            }
            var dbTracerDef = Workspace.GetDbTracerDefs().FirstOrDefault(td => firstSelectedItem.Text == td.Name);
            if (dbTracerDef == null)
            {
                RefreshUi(true);
                return;
            }
            EditTracerDef(dbTracerDef);
        }

        protected override void  OnWorkspaceEntitiesChanged(EntitiesChangedEventArgs args)
        {
         	RefreshUi(true); 
            base.OnWorkspaceEntitiesChanged(args);
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            EditTracerDef(null);
        }

        private void EditTracerDef(DbTracerDef dbTracerDef)
        {
            using (var dlg = new DefineLabelDialog(Workspace, dbTracerDef))
            {
                dlg.ShowDialog(this);
            }
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            var selectedItems = new HashSet<string>(listView1.SelectedItems.Cast<ListViewItem>().Select(listViewItem=>listViewItem.Text));
            var allTracerDefs = Workspace.GetDbTracerDefs();
            ICollection<DbTracerDef> tracerDefsToDelete = 
                allTracerDefs.Where(dbTracerDef => selectedItems.Contains(dbTracerDef.Name)).ToArray();
            if (tracerDefsToDelete.Count == 0)
            {
                RefreshUi(true);
                return;
            }
            string message;
            if (tracerDefsToDelete.Count == 1)
            {
                message = string.Format("Are you sure you want to delete the label '{0}'?", tracerDefsToDelete.First().Name);
            }
            else
            {
                message = string.Format("Are you sure you want to delete these '{0}' labels?", tracerDefsToDelete.Count);
            }
            if (DialogResult.OK != MessageBox.Show(this, message, Program.AppName, MessageBoxButtons.OKCancel))
            {
                return;
            }
            var newTracerDefs =
                Workspace.GetDbTracerDefs().Where(dbTracerDef => !selectedItems.Contains(dbTracerDef.Name)).ToList();
            Workspace.SetDbTracerDefs(newTracerDefs);
            RefreshUi(true);
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshUi(false);
        }
    }
}
