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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    public partial class ManageResultsDlg : FormEx
    {
        public ManageResultsDlg(IDocumentUIContainer documentUIContainer)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            DocumentUIContainer = documentUIContainer;
            var settings = DocumentUIContainer.Document.Settings;
            if (settings.HasResults)
            {
                foreach (var chromatogramSet in settings.MeasuredResults.Chromatograms)
                {
                    listResults.Items.Add(new ManageResultsAction(chromatogramSet));
                }
                listResults.SelectedIndices.Add(0);
            }
        }

        public IDocumentUIContainer DocumentUIContainer { get; private set; }

        public IEnumerable<ChromatogramSet> Chromatograms
        {
            get { return listResults.Items.Cast<ManageResultsAction>().Select(a => a.Chromatograms); }
        }

        public IEnumerable<ChromatogramSet> ReimportChromatograms
        {
            get
            {
                return listResults.Items.Cast<ManageResultsAction>()
                    .Where(a => a.IsReimport).Select(a => a.Chromatograms);
            }
        }

        public IEnumerable<ChromatogramSet> SelectedChromatograms
        {
            get
            {
                return SelectedIndices.Select(i => listResults.Items[i])
                    .Cast<ManageResultsAction>().Select(a => a.Chromatograms);
            }

            set
            {
                listResults.SelectedItems.Clear();
                foreach (var action in listResults.Items.Cast<ManageResultsAction>()
                        .Where(action => value.Contains(action.Chromatograms)).ToArray())
                {
                    listResults.SelectedItems.Add(action);
                }
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
        private List<object> RemoveSelected()
        {
            // Remove all selected items
            var listRemovedItems = new List<object>();
            var selectedIndices = SelectedIndices;
            for (int i = selectedIndices.Length - 1; i >= 0; i--)
            {
                int iRemove = selectedIndices[i];
                listRemovedItems.Add(listResults.Items[iRemove]);
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
            CheckDisposed();
            var selectedIndices = SelectedIndices;
            if (selectedIndices.Length == 0)
                return;

            int iFirst = SelectedIndices[0];
            listResults.SelectedIndices.Clear();
            listResults.SelectedIndices.Add(iFirst);

            var selected = (ManageResultsAction) listResults.Items[iFirst];
            var listExistingNames = from chromSet in Chromatograms
                               where !ReferenceEquals(chromSet, selected.Chromatograms)
                               select chromSet.Name;

            using (var dlg = new RenameResultDlg(selected.Chromatograms.Name, listExistingNames))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    selected.Chromatograms = (ChromatogramSet) selected.Chromatograms.ChangeName(dlg.ReplicateName);
                    listResults.Items[iFirst] = ManageResultsAction.EMPTY;  // Forces text update for case change
                    listResults.Items[iFirst] = selected;
                }
            }
            listResults.Focus();
        }

        private void btnReimport_Click(object sender, EventArgs e)
        {
            ReimportResults();
        }

        public void ReimportResults()
        {
            CheckDisposed();
            if (!DocumentUIContainer.DocumentUI.Settings.MeasuredResults.IsLoaded)
            {
                MessageDlg.Show(this, "All results must be completely imported before any can be re-imported.");
                return;
            }

            var missingFiles = FindMissingFiles(SelectedChromatograms);
            if (missingFiles.Length > 0)
            {
                MessageDlg.Show(this, string.Format("Unable to find the following files, either in their original locations or in the folder of the current document:\n\n{0}", string.Join("\n", missingFiles)));
                return;
            }

            using(new UpdateList(this))
            {
                foreach (int selectedIndex in SelectedIndices)
                {
                    var selected = (ManageResultsAction) listResults.Items[selectedIndex];
                    selected.IsReimport = true;
                    listResults.Items[selectedIndex] = selected;
                    // Make sure the selected items stay selected
                    listResults.SelectedItems.Add(selected);
                }
            }
            listResults.Focus();
        }

        private void btnMinimize_Click(object sender, EventArgs e)
        {
            MinimizeResults();
        }

        public void MinimizeResults()
        {
            using (var dlg = new MinimizeResultsDlg(DocumentUIContainer))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // If the MinimizeResultsDlg did work then cancel out of this dialog
                    DialogResult = DialogResult.Cancel;
                }
            }
        }

        private string[] FindMissingFiles(IEnumerable<ChromatogramSet> chromatogramSets)
        {
            string documentPath = DocumentUIContainer.DocumentFilePath;
            string cachePath = ChromatogramCache.FinalPathForName(documentPath, null);

            // Collect all missing paths
            var listPathsMissing = new List<string>();
            // Avoid checking paths multiple times for existence
            var setPaths = new HashSet<string>();
            foreach (var filePath in chromatogramSets.SelectMany(set => set.MSDataFilePaths))
            {
                string filePathPart = SampleHelp.GetPathFilePart(filePath);
                if (setPaths.Contains(filePathPart))
                    continue;
                setPaths.Add(filePathPart);
                if (ChromatogramSet.GetExistingDataFilePath(cachePath, filePath, out filePathPart) != null)
                    continue;
                listPathsMissing.Add(filePathPart);
            }
            listPathsMissing.Sort();
            return listPathsMissing.ToArray();
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
            btnReimport.Enabled = enable;
            btnUp.Enabled = enable;
            btnDown.Enabled = enable;
            btnRename.Enabled = enable;
            btnRemoveAll.Enabled = btnMinimize.Enabled = listResults.Items.Count > 0;            
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

        private sealed class ManageResultsAction
        {
            public static readonly ManageResultsAction EMPTY = new ManageResultsAction(null);

            public ManageResultsAction(ChromatogramSet chromatograms)
            {
                Chromatograms = chromatograms;
            }

            public ChromatogramSet Chromatograms { get; set; }
            public bool IsReimport { get; set; }

            public override string ToString()
            {
                if (Chromatograms == null)
                    return "";
                return (IsReimport ? "*" : "") + Chromatograms.Name;
            }
        }
    }
}
