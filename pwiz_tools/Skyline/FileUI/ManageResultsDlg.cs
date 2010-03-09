/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.FileUI
{
    public partial class ManageResultsDlg : Form
    {
        public ManageResultsDlg(SrmDocument document)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            if (document.Settings.HasResults)
            {
                listResults.DisplayMember = "Name";
                foreach (var chromatogramSet in document.Settings.MeasuredResults.Chromatograms)
                {
                    listResults.Items.Add(chromatogramSet);
                }
                listResults.SelectedIndices.Add(0);
            }
        }

        public IEnumerable<ChromatogramSet> Chromatograms
        {
            get
            {
                foreach (ChromatogramSet chromatogramSet in listResults.Items)
                    yield return chromatogramSet;
            }
        }

        public IEnumerable<ChromatogramSet> SelectedChromatograms
        {
            get
            {
                foreach (var i in SelectedIndices)
                    yield return (ChromatogramSet) listResults.Items[i];
            }

            set
            {
                listResults.SelectedItems.Clear();
                foreach (var chromSet in value)
                    listResults.SelectedItems.Add(chromSet);
            }
        }

        public int[] SelectedIndices
        {
            get
            {
                var listSelectedIndices = new List<int>();
                var selectedIndices = listResults.SelectedIndices;
                for (int i = 0; i < selectedIndices.Count; i++)
                    listSelectedIndices.Add(selectedIndices[i]);
                return listSelectedIndices.ToArray();
            }
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            Remove();
        }

        public void Remove()
        {
            using (new UpdateList(this))
            {
                RemoveSelected();
            }
        }

        /// <summary>
        /// Removes all selected items from the list.
        /// </summary>
        /// <returns>A list containing the removed items in reverse order</returns>
        private List<ChromatogramSet> RemoveSelected()
        {
            // Remove all selected items
            var listRemovedItems = new List<ChromatogramSet>();
            var selectedIndices = SelectedIndices;
            for (int i = selectedIndices.Length - 1; i >= 0; i--)
            {
                int iRemove = selectedIndices[i];
                listRemovedItems.Add((ChromatogramSet) listResults.Items[iRemove]);
                listResults.Items.RemoveAt(iRemove);
            }
            // Select the same position that had the focus, unless it was beyond
            // the end of the remaining items
            if (listResults.Items.Count > 0)
            {
                int iNext = selectedIndices[selectedIndices.Length - 1] - selectedIndices.Length + 1;
                listResults.SelectedIndices.Add(Math.Min(iNext, listResults.Items.Count - 1));
            }

            return listRemovedItems;
        }

        private void btnRemoveAll_Click(object sender, EventArgs e)
        {
            RemoveAll();
        }

        public void RemoveAll()
        {
            using (new UpdateList(this))
            {
                listResults.Items.Clear();
            }
            UpdateButtons();
        }


        private void btnUp_Click(object sender, EventArgs e)
        {
            MoveUp();
        }

        public void MoveUp()
        {
            using (new UpdateList(this))
            {
                MoveSelected(-1);
            }
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            MoveDown();
        }

        public void MoveDown()
        {
            using (new UpdateList(this))
            {
                MoveSelected(1);
            }
        }

        private void MoveSelected(int increment)
        {
            var selectedIndices = SelectedIndices;
            if (selectedIndices.Length == 0)
                return;

            listResults.BeginUpdate();

            // Remove currently selected items
            var listRemovedItems = RemoveSelected();
            // Insert them in their new location in reverse
            int iInsert;
            if (increment < 0)
            {
                iInsert = Math.Max(0, selectedIndices[0] + increment);
            }
            else
            {
                iInsert = Math.Min(listResults.Items.Count,
                                   selectedIndices[selectedIndices.Length - 1] - selectedIndices.Length + 1 + increment);
            }
            foreach (var removedItem in listRemovedItems)
                listResults.Items.Insert(iInsert, removedItem);

            // Select the newly added items
            listResults.SelectedIndices.Clear();
            for (int i = 0; i < listRemovedItems.Count; i++)
                listResults.SelectedIndices.Add(iInsert + i);

            listResults.EndUpdate();            
        }

        private void btnRename_Click(object sender, EventArgs e)
        {
            RenameResult();
        }

        private void listResults_DoubleClick(object sender, EventArgs e)
        {
            RenameResult();
        }

        public void RenameResult()
        {
            int iFirst = SelectedIndices[0];
            listResults.SelectedIndices.Clear();
            listResults.SelectedIndices.Add(iFirst);

            var chromSetSelected = (ChromatogramSet) listResults.Items[iFirst];
            var listExisting = from chromSet in Chromatograms
                               where !ReferenceEquals(chromSet, chromSetSelected)
                               select chromSet.Name;

            var dlg = new RenameResultDlg(chromSetSelected.Name, listExisting);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                listResults.Items[iFirst] = chromSetSelected.ChangeName(dlg.ReplicateName);
            }

            listResults.Focus();
        }

        private bool InListUpdate { get; set; }

        private void listResults_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!InListUpdate)
                UpdateButtons();
        }

        private void UpdateButtons()
        {
            bool enable = listResults.SelectedIndices.Count > 0;
            btnRemove.Enabled = enable;
            btnUp.Enabled = enable;
            btnDown.Enabled = enable;
            btnRename.Enabled = enable;
            btnRemoveAll.Enabled = listResults.Items.Count > 0;            
        }

        private sealed class UpdateList : IDisposable
        {
            private readonly ManageResultsDlg _dlg;

            public UpdateList(ManageResultsDlg dlg)
            {
                _dlg = dlg;
                _dlg.InListUpdate = true;
                _dlg.listResults.BeginUpdate();
            }

            public void Dispose()
            {
                _dlg.listResults.EndUpdate();
                _dlg.InListUpdate = false;
                _dlg.listResults.Focus();
            }
        }
    }
}
