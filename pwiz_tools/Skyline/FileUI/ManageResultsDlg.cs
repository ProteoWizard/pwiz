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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class ManageResultsDlg : FormEx
    {
        // ReSharper disable InconsistentNaming
        // ReSharper disable UnusedMember.Local
        public enum TABS { Replicates, Libraries }
        // ReSharper restore UnusedMember.Local
        // ReSharper restore InconsistentNaming

        public ManageResultsDlg(IDocumentUIContainer documentUIContainer, LibraryManager libraryManager)
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

            var libraries = settings.PeptideSettings.Libraries;
            if (libraries.HasLibraries && libraries.HasDocumentLibrary)
            {
                DocumentLibrarySpec = libraries.LibrarySpecs.FirstOrDefault(x => x.IsDocumentLibrary);
                if (null != DocumentLibrarySpec)
                {
                    DocumentLibrary = libraryManager.TryGetLibrary(DocumentLibrarySpec);
                    if (null != DocumentLibrary)
                    {
                        foreach (var dataFile in DocumentLibrary.LibraryDetails.DataFiles)
                        {
                            listLibraries.Items.Add(dataFile);
                        }
                        listLibraries.SelectedIndices.Add(0);
                    }
                }
            }
            if (listLibraries.Items.Count == 0)
            {
                checkBoxRemoveLibraryRuns.Visible = false;
                int heightTabPage = manageResultsTabControl.TabPages[(int) TABS.Replicates].Height;
                int changeHeight = heightTabPage - listResults.Bottom - btnRemove.Top;
                listResults.Height += changeHeight;
                Height -= changeHeight;
                manageResultsTabControl.TabPages.RemoveAt((int)TABS.Libraries);
            }
            else if (listResults.Items.Count == 0)
            {
                checkBoxRemoveReplicates.Visible = false;
                int heightTabPage = manageResultsTabControl.TabPages[(int)TABS.Libraries].Height;
                int changeHeight = heightTabPage - listLibraries.Bottom - btnRemoveLibRun.Top;
                listLibraries.Height += changeHeight;
                Height -= changeHeight;
                manageResultsTabControl.TabPages.RemoveAt((int)TABS.Replicates);
            }

            LibraryRunsRemovedList = new List<string>();
            ChromatogramsRemovedList = new List<ChromatogramSet>();
        }

        public IDocumentUIContainer DocumentUIContainer { get; private set; }
        public LibrarySpec DocumentLibrarySpec { get; private set; }
        public Library DocumentLibrary { get; private set; }
        public List<string> LibraryRunsRemovedList { get; private set; }
        public IEnumerable<ChromatogramSet> ChromatogramsRemovedList { get; private set; } 

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
                return SelectedIndices(listResults).Select(i => listResults.Items[i])
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

        public IEnumerable<string> LibraryRuns
        {
            get { return listLibraries.Items.Cast<string>(); }
        }

        public IEnumerable<string> SelectedLibraryRuns
        {
            get
            {
                return SelectedIndices(listLibraries).Select(i => listLibraries.Items[i])
                    .Cast<string>().Select(a => a.ToString(CultureInfo.InvariantCulture));
            }

            set
            {
                listLibraries.SelectedItems.Clear();
                foreach (var action in listLibraries.Items.Cast<string>()
                        .Where(action => value.Contains(action.ToString(CultureInfo.InvariantCulture))).ToArray())
                {
                    listLibraries.SelectedItems.Add(action);
                }
            }
        }

        public bool IsRemoveAllLibraryRuns
        {
            get { return listLibraries.Items.Count == 0 && LibraryRunsRemovedList.Count > 0; }
        }
        
        private int[] SelectedIndices(ListBox list)
        {
            var listSelectedIndices = new List<int>();
            var selectedIndices = list.SelectedIndices;
            for (int i = 0; i < selectedIndices.Count; i++)
                listSelectedIndices.Add(selectedIndices[i]);
            return listSelectedIndices.ToArray();
        }

        public void OkDialog()
        {
            if (IsRemoveCorrespondingLibraries)
                RemoveCorrespondingLibraryRunsForReplicates();

            if (IsRemoveCorrespondingReplicates)
                RemoveCorrespondingReplicatesForLibraryRuns();

            DialogResult = DialogResult.OK;
        }

        private void RemoveCorrespondingLibraryRunsForReplicates()
        {
            if (listLibraries.Items.Count != 0)
            {
                foreach (var chrom in ChromatogramsRemovedList)
                {
                    foreach (var chromFileInfo in chrom.MSDataFileInfos)
                    {
                        foreach (var dataFile in DocumentLibrary.LibraryDetails.DataFiles)
                        {
                            if (MeasuredResults.IsBaseNameMatch(chromFileInfo.FilePath.GetFileNameWithoutExtension(),
                                                            Path.GetFileNameWithoutExtension(dataFile)))
                            {
                                int foundIndex = listLibraries.FindString(dataFile);
                                if (ListBox.NoMatches != foundIndex)
                                {
                                    LibraryRunsRemovedList.Add(dataFile);
                                    listLibraries.Items.RemoveAt(foundIndex);
                                    if (listLibraries.Items.Count == 0)
                                        return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void RemoveCorrespondingReplicatesForLibraryRuns()
        {
            if (listResults.Items.Count != 0)
            {
                var newChromRemList = new List<ChromatogramSet>(ChromatogramsRemovedList);
                foreach (var dataFile in LibraryRunsRemovedList)
                {
                    var matchingFile =
                        DocumentUIContainer.Document.Settings.MeasuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(dataFile));
                    if (null != matchingFile)
                    {
                        int foundIndex = listResults.FindString(matchingFile.Chromatograms.Name);
                        if (ListBox.NoMatches != foundIndex)
                        {
                            newChromRemList.Add(matchingFile.Chromatograms);
                            listResults.Items.RemoveAt(foundIndex);
                            if (listResults.Items.Count == 0)
                                break;
                        }
                    }
                }
                ChromatogramsRemovedList = newChromRemList;
            }
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            RemoveReplicates();
        }

        public void RemoveReplicates()
        {
            using (new UpdateList(this, listResults))
            {
                var removedList = RemoveSelected(listResults);
                ChromatogramsRemovedList = removedList.Cast<ManageResultsAction>().Select(a => a.Chromatograms);
            }
        }

        /// <summary>
        /// Removes all selected items from the list.
        /// </summary>
        /// <returns>A list containing the removed items in reverse order</returns>
        private List<object> RemoveSelected(ListBox list)
        {
            // Remove all selected items
            var listRemovedItems = new List<object>();
            var selectedIndices = SelectedIndices(list);
            for (int i = selectedIndices.Length - 1; i >= 0; i--)
            {
                int iRemove = selectedIndices[i];
                listRemovedItems.Add(list.Items[iRemove]);
                list.Items.RemoveAt(iRemove);
            }
            // Select the same position that had the focus, unless it was beyond
            // the end of the remaining items
            if (list.Items.Count > 0)
            {
                int iNext = selectedIndices[selectedIndices.Length - 1] - selectedIndices.Length + 1;
                list.SelectedIndices.Add(Math.Min(iNext, list.Items.Count - 1));
            }

            return listRemovedItems;
        }

        private void btnRemoveAll_Click(object sender, EventArgs e)
        {
            RemoveAllReplicates();
        }

        public void RemoveAllReplicates()
        {
            using (new UpdateList(this, listResults))
            {
                ChromatogramsRemovedList = listResults.Items.Cast<ManageResultsAction>().Select(a => a.Chromatograms);
                listResults.Items.Clear();
            }
            UpdateReplicatesButtons();
        }


        private void btnUp_Click(object sender, EventArgs e)
        {
            MoveUp();
        }

        public void MoveUp()
        {
            using (new UpdateList(this, listResults))
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
            using (new UpdateList(this, listResults))
            {
                MoveSelected(1);
            }
        }

        private void MoveSelected(int increment)
        {
            var selectedIndices = SelectedIndices(listResults);
            if (selectedIndices.Length == 0)
                return;

            listResults.BeginUpdate();

            // Remove currently selected items
            var listRemovedItems = RemoveSelected(listResults);
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
            var selectedIndices = SelectedIndices(listResults);
            if (selectedIndices.Length == 0)
                return;

            int[] listResultsSelectedIndices = SelectedIndices(listResults);
            int iFirst = listResultsSelectedIndices[0];
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
                MessageDlg.Show(this, Resources.ManageResultsDlg_ReimportResults_All_results_must_be_completely_imported_before_any_can_be_re_imported);
                return;
            }

            var missingFiles = FindMissingFiles(SelectedChromatograms);
            if (missingFiles.Length > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine(Resources.ManageResultsDlg_ReimportResults_Unable_to_find_the_following_files_either_in_their_original_locations_or_in_the_folder_of_the_current_document)
                    .AppendLine()
                    .Append(TextUtil.LineSeparate(missingFiles));
                MessageDlg.Show(this, sb.ToString());
                return;
            }

            using(new UpdateList(this, listResults))
            {
                int[] selectedIndices = SelectedIndices(listResults);
                foreach (int selectedIndex in selectedIndices)
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

        private void btnRescore_Click(object sender, EventArgs e)
        {
            Rescore();
        }

        public void Rescore()
        {
            using (var dlg = new RescoreResultsDlg(DocumentUIContainer))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // If the RescoreResultsDlg did work then cancel out of this dialog
                    DialogResult = DialogResult.Cancel;
                }
            }
        }

        private void btnMinimize_Click(object sender, EventArgs e)
        {
            MinimizeResults();
        }

        public void MinimizeResults()
        {
            bool dispose = true;
            var dlg = new MinimizeResultsDlg(DocumentUIContainer);
            try
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // If the MinimizeResultsDlg did work then cancel out of this dialog
                    DialogResult = DialogResult.Cancel;
                }
            }
            catch
            {
                dispose = false;
                throw;
            }
            finally
            {
                // NOTE: We do a manual Dispose rather than "using", because something in the constructor
                // is occasionally throwing an exception.  Unfortunately, that exception gets masked when
                // "using" calls Dispose during exception processing, and Dispose throws an exception.
                if (dispose)
                {
                    int count = 0;
                    while (!dlg.IsDisposed)
                    {
                        try
                        {
                            dlg.Dispose();
                            break;
                        }
                        catch
                        {
                            // After 10 tries, just throw the exception (usually only once is required)
                            if (count++ > 10)
                                throw;
                            Thread.Sleep(50);   // Give the dialog time to finish whatever caused this exception
                        }
                    }
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
                MsDataFilePath msDataFilePath = filePath as MsDataFilePath;
                if (null == msDataFilePath)
                {
                    continue;
                }
                string filePathPart = msDataFilePath.FilePath;
                if (setPaths.Contains(filePathPart))
                    continue;
                setPaths.Add(filePathPart);
                if (ChromatogramSet.GetExistingDataFilePath(cachePath, msDataFilePath) != null)
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
                UpdateReplicatesButtons();
        }

        private void UpdateReplicatesButtons()
        {
            bool enable = listResults.SelectedIndices.Count > 0;
            btnRemove.Enabled = enable;
            btnReimport.Enabled = enable;
            btnUp.Enabled = enable;
            btnDown.Enabled = enable;
            btnRename.Enabled = enable;
            btnRemoveAll.Enabled = btnMinimize.Enabled = btnRescore.Enabled = listResults.Items.Count > 0;
        }

        private sealed class UpdateList : IDisposable
        {
            private readonly ManageResultsDlg _dlg;
            private readonly ListBox _list;

            public UpdateList(ManageResultsDlg dlg, ListBox list)
            {
                _dlg = dlg;
                _list = list;
                _dlg.InListUpdate = true;
                _list.BeginUpdate();
            }

            public void Dispose()
            {
                _list.EndUpdate();
                _dlg.InListUpdate = false;
                _list.Focus();
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
                    return string.Empty;
                return (IsReimport ? "*" : string.Empty) + Chromatograms.Name; // Not L10N
            }
        }

        private void btnRemoveLibRun_Click(object sender, EventArgs e)
        {
            RemoveLibraryRuns();
        }

        public void RemoveLibraryRuns()
        {
            using (new UpdateList(this, listLibraries))
            {
                var removedList = RemoveSelected(listLibraries);
                foreach (var item in removedList)
                {
                    LibraryRunsRemovedList.Add(item.ToString());
                }
            }

            UpdateLibraryRunButtons();
        }

        private void listLibraries_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!InListUpdate)
                UpdateLibraryRunButtons();
        }

        private void UpdateLibraryRunButtons()
        {
            bool enable = listLibraries.SelectedIndices.Count > 0;
            btnRemoveLibRun.Enabled = enable;
            btnRemoveAllLibs.Enabled = enable;
        }

        private void btnRemoveAllLibs_Click(object sender, EventArgs e)
        {
            RemoveAllLibraryRuns();
        }

        public void RemoveAllLibraryRuns()
        {
            using (new UpdateList(this, listLibraries))
            {
                foreach (var item in listLibraries.Items)
                {
                    LibraryRunsRemovedList.Add(item.ToString());
                }
                listLibraries.Items.Clear();
            }
            UpdateLibraryRunButtons();
        }

        public void SelectLibraryRunsTab()
        {
            manageResultsTabControl.SelectTab(libRunsTab);
        }

        public void SelectReplicatesTab()
        {
            manageResultsTabControl.SelectTab(replicatesTab);
        }

        public bool IsRemoveCorrespondingReplicates
        {
            get { return checkBoxRemoveReplicates.Checked; }
            set { checkBoxRemoveReplicates.Checked = value; }
        }

        public bool IsRemoveCorrespondingLibraries
        {
            get { return checkBoxRemoveLibraryRuns.Checked; }
            set { checkBoxRemoveLibraryRuns.Checked = value; }
        }
    }
}
