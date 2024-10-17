/*
 * Original authors: Clark Brace <clarkbrace@gmail.com>,
 *                   Brendan MacLean <brendanx@proteinms.net
 *                   MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Controls;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Intended to allow users to select replicate results files they wish to include when sharing
    /// their current Skyline document. Information regarding which files are currently being used in Skyline
    /// is collected and displayed allowing the user to ZIP everything in one clean package. Files are
    /// displayed in natural sort order in a checked list for user selection. Any missing/not accessible
    /// files can be selected individually using the <see cref="OpenDataSourceDialog"/> or by selecting
    /// a folder known to contain the files. Once located, missing files are added to the set of
    /// files the user can choose from in in the checked list. By default missing files
    /// are checked when discovered and added.
    /// </summary>
    public partial class ShareResultsFilesDlg : ModeUIInvariantFormEx
    {
        private string _documentPath;

        public ShareResultsFilesDlg(SrmDocument document, string documentPath, AuxiliaryFiles auxiliaryFiles) // Srm document passed in to allow access to raw files currently loaded and being used
        {
            _documentPath = documentPath;

            InitializeComponent();

            Icon = Resources.Skyline;

            PopulateListViews(auxiliaryFiles ?? new AuxiliaryFiles(document, documentPath)); // Populate the selection and missing files list boxes
            UpdateStatusLabel();
            UpdateSelectAll();
        }

        /// <summary>
        /// Populate checked list box based on replicate files currently being used.
        /// File location information is scraped from srm document data structure. Duplicate
        /// file addresses are removed and sorted in natural sort order. Boxes are checked
        /// by default. Should previous selections already exist load them in as opposed to
        /// searching again.
        /// </summary>
        private void PopulateListViews(AuxiliaryFiles auxiliaryFiles)
        {
            foreach (var fileChoice in auxiliaryFiles.FoundFiles)
            {
                checkedListBox.Items.Add(fileChoice.Filename, fileChoice.IsIncluded);
            }

            foreach (var missingFile in auxiliaryFiles.MissingFiles)
            {
                listboxMissingFiles.Items.Add(missingFile);
            }

            UpdateMissingFilePane();
        }

        private void UpdateMissingFilePane()
        {
            // Don't bother the user with missing files stuff when there are no missing files
            bool showMissingFiles = listboxMissingFiles.Items.Count > 0;
            btnLocateFiles.Enabled = btnFindInFolder.Enabled =
                listboxMissingFiles.Enabled = labelMissingFiles.Enabled =
                    showMissingFiles;
            splitContainer1.Panel2Collapsed = !showMissingFiles;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        public AuxiliaryFiles FilesInfo
        {
            get
            {
                var checkedItems = (
                    from object item in checkedListBox.Items
                    select checkedListBox.CheckedItems.Contains(item)
                        ? new FileChoice(item.ToString(), true)
                        : new FileChoice(item.ToString(), false)).ToList();

                var missingItems = (
                    from object item in listboxMissingFiles.Items
                    select item.ToString()).ToList();

                return new AuxiliaryFiles(checkedItems, missingItems);
            }
        }

        public int IncludedFilesCount => checkedListBox.CheckedIndices.Count;
        public int MissingFilesCount => listboxMissingFiles.Items.Count;

        /// <summary>
        /// Switch select all checkbox state and update checked status
        /// </summary>
        private void checkboxSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            if (checkboxSelectAll.CheckState == CheckState.Indeterminate)
                return;

            SelectOrDeselectAll(checkboxSelectAll.Checked);
        }


        /// <summary>
        /// Change the state of all elements in the checkedItemBox
        /// </summary>
        /// <param name="select">true to select all, false to deselect all</param>
        private void SelectOrDeselectAll(bool select)
        {
            // Change checkbox for all items without events
            checkedListBox.ItemCheck -= checkedListBox_ItemCheck;
            int count = checkedListBox.Items.Count;
            for (var i = 0; i < count; i++)
                checkedListBox.SetItemChecked(i, select);
            checkedListBox.ItemCheck += checkedListBox_ItemCheck;

            // Update the summary UI to match the resulting state
            UpdateSelectAll(select ? count : 0);
            UpdateStatusLabel();
        }

        /// <summary>
        /// Update UI elements that depend on selection state just before that
        /// selection is about to change.
        /// </summary>
        private void checkedListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            var checkCount = IncludedFilesCount;
            if (e.NewValue == CheckState.Checked)
                checkCount++;
            else
                checkCount--;

            UpdateSelectAll(checkCount);

            // Because the checked state in the selection list does not accurately
            // reflect what the status is about to become, it is necessary to craft
            // the status text to match the near future.
            checkedStatus.Text = AuxiliaryFiles.GetStatusText(checkCount,
                checkedListBox.Items.Count + listboxMissingFiles.Items.Count,
                listboxMissingFiles.Items.Count);
        }

        /// <summary>
        /// Updates the status label describing what files will be included and what is missing
        /// </summary>
        private void UpdateStatusLabel()
        {
            checkedStatus.Text = FilesInfo.ToString();
        }

        /// <summary>
        /// Update check 3-state check box to the correct state/graphic without
        /// triggering the event to change the selection list
        /// </summary>
        private void UpdateSelectAll(int? checkCountNullable = null)
        {
            int checkCount = checkCountNullable ?? IncludedFilesCount;
            checkboxSelectAll.CheckedChanged -= checkboxSelectAll_CheckedChanged;
            if (checkCount == 0) // Nothing is checked
                checkboxSelectAll.CheckState = CheckState.Unchecked;
            else if (checkCount == checkedListBox.Items.Count)  // Everything is checked
                checkboxSelectAll.CheckState = CheckState.Checked;
            else // A mix of checked and unchecked
                checkboxSelectAll.CheckState = CheckState.Indeterminate;
            checkboxSelectAll.CheckedChanged += checkboxSelectAll_CheckedChanged;
            checkboxSelectAll.Enabled = checkedListBox.Items.Count > 0;
        }

        private void listboxMissingFiles_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            LocateMissingFiles();
        }

        private void btnLocateFiles_Click(object sender, EventArgs e)
        {
            LocateMissingFiles();
        }

        /// <summary>
        /// Allow user to search for missing raw files by searing through the open data source dialog.
        /// Only those files that have yet to be located are presented to the user. The dialog allows
        /// the user to select one or multiple missing files. The user is prevented from searching should
        /// there be no remaining files to locate. Missing files that have been located are added to the
        /// checked list of discovered files and automatically presented in their checked state.
        /// </summary>
        public void LocateMissingFiles()
        {
            Assume.IsTrue(listboxMissingFiles.Items.Count > 0); // Should only be in here with files to find

            // Get list of all files that are currently missing
            var missingFiles = (from object missingItem in listboxMissingFiles.Items select missingItem.ToString()).ToList();

            using var openDataSource = new OpenDataSourceDialog(Settings.Default.RemoteAccountList, missingFiles);
            openDataSource.RestoreState(_documentPath, Settings.Default.OpenDataSourceState);
            if (openDataSource.ShowDialog(this) == DialogResult.OK)
            {
                Settings.Default.OpenDataSourceState = openDataSource.GetState(_documentPath);

                var directory = openDataSource.CurrentDirectory; // Current directory from dialog selection

                // Search through all selected files
                foreach (var selectedFile in openDataSource.SelectedFiles)
                { 
                    // Check if the selected file is one of those missing
                    if (listboxMissingFiles.Items.Contains(selectedFile))
                    { 
                        // True file path to be checked against
                        var filePath = Path.Combine(directory.GetFilePath(), selectedFile);

                        // Confirm the path is correct
                        if (ScanProvider.FileExists(filePath, directory))
                        {
                            checkedListBox.Items.Add(filePath, true);
                            listboxMissingFiles.Items.Remove(selectedFile); // Removed found file from missing lis box
                        }
                    }
                }
            }

            UpdateMissingFilePane();
            UpdateStatusLabel();
            UpdateSelectAll();
        }

        public void btnFindInFolder_Click(object sender, EventArgs e)
        {
            LocateMissingFilesFromFolder();
        }

        /// <summary>
        /// Allow the user to select a folder to search for missing files from.
        /// This code can't be tested because it uses a CommonDialog form.
        /// </summary>
        private void LocateMissingFilesFromFolder()
        {
            Assume.IsTrue(listboxMissingFiles.Items.Count > 0); // Should only be in here with files to find

            // Set initial directory to the one containing the document
            var initialDir = Path.GetDirectoryName(_documentPath);
            if (string.IsNullOrEmpty(initialDir))
                initialDir = null;

            // Ask the user for the directory to search
            using var searchFolderDialog = new FolderBrowserDialog();
            searchFolderDialog.ShowNewFolderButton = false;
            searchFolderDialog.SelectedPath = initialDir;
            searchFolderDialog.Description = AlertsResources.ShareResultsFilesDlg_LocateMissingFilesFromFolder_Please_select_the_folder_containing_the_missing_files_;

            if (searchFolderDialog.ShowDialog() == DialogResult.OK)
            {
                SearchDirectoryForMissingFiles(searchFolderDialog.SelectedPath);
            }
        }

        /// <summary>
        /// Preforms search for missing items within directory given directory path. Split
        /// for testing purposes and incompatibility with form types.
        /// </summary>
        public void SearchDirectoryForMissingFiles(string folderPath)
        {
            var matchedFiles = new List<string>();
            using (var longWaitDlg = new LongWaitDlg())
            {
                longWaitDlg.Text = Resources.ImportResultsControl_FindResultsFiles_Searching_for_Results_Files;
                try
                {
                    var missingNames = new HashSet<string>(listboxMissingFiles.Items.OfType<string>());
                    longWaitDlg.PerformWork(this, 1000, longWaitBroker =>
                        FindDataFiles(folderPath, missingNames, matchedFiles, longWaitBroker));
                }
                catch (Exception x)
                {
                    MessageDlg.ShowWithException(this, TextUtil.LineSeparate(
                        Resources.ImportResultsControl_FindResultsFiles_An_error_occurred_attempting_to_find_results_files_,
                        x.Message), x);
                }
            }

            // Update the UI with any matched files
            // CONSIDER: Seems not worth it to insert these into the checked list. Instead they are added to the end in sorted order.
            matchedFiles.Sort(NaturalFilenameComparer.Compare);
            foreach (var matchedFile in matchedFiles)
            {
                checkedListBox.Items.Add(matchedFile, true);
                listboxMissingFiles.Items.Remove(Path.GetFileName(matchedFile));
            }

            UpdateMissingFilePane();
            UpdateStatusLabel();
            UpdateSelectAll();
        }

        private void FindDataFiles(string directory, HashSet<string> namesToMatch, IList<string> matchedFiles, ILongWaitBroker longWaitBroker)
        {
            // Don't search if every spectrum source file has an exact match and an alternate match
            if (directory == null || !Directory.Exists(directory))
                return;

            if (longWaitBroker != null)
            {
                longWaitBroker.Message =
                    string.Format(Resources.ImportResultsControl_FindResultsFiles_Searching_for_matching_results_files_in__0__, directory);
            }

            try
            {
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (longWaitBroker != null && longWaitBroker.IsCanceled)
                        return;

                    if (entry != null && DataSourceUtil.IsDataSource(entry))
                    {
                        if (namesToMatch.Contains(Path.GetFileName(entry)))
                        {
                            matchedFiles.Add(entry);
                        }
                    }
                }
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
                // No permissions on folder
            }
        }

        #region Functional testing support

        public bool? IsSelectAll
        {
            get
            {
                if (checkboxSelectAll.CheckState == CheckState.Indeterminate)
                    return null;
                return checkboxSelectAll.CheckState == CheckState.Checked;
            }
            set
            {
                checkboxSelectAll.Checked = value ?? false;
            }
        }

        public void SetFileChecked(int fileIndex, bool fileChecked)
        {
            checkedListBox.SetItemChecked(fileIndex, fileChecked);
        }

        public string StatusText => checkedStatus.Text;

        #endregion

        /// <summary>
        /// User choices for data source file inclusion
        /// CONSIDER: This should probably be moved to Model to support command-line use
        /// </summary>
        public class AuxiliaryFiles
        {
            public AuxiliaryFiles(IList<FileChoice> foundFiles, IList<string> missingFiles)
            {
                FoundFiles = foundFiles;
                MissingFiles = missingFiles;
            }

            public AuxiliaryFiles(SrmDocument document, string documentPath)
            {
                // Search for files based on their expected location 
                var paths = new HashSet<string>(); // List of file paths
                var missingPaths = new HashSet<string>(); // List of missing paths
                if (document.Settings.MeasuredResults != null)
                {
                    foreach (var chromatogramSet in document.Settings.MeasuredResults.Chromatograms)
                    {
                        foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                        {
                            // Check for path validity, using our standard rules for locating data files when they aren't in current working directory
                            if (ScanProvider.FileExists(documentPath, chromFileInfo.FilePath,
                                    out var path))
                            {
                                paths.Add(path);
                            }
                            else
                            {
                                missingPaths.Add(chromFileInfo.FilePath.GetFileName());
                            }
                        }
                    }

                    var repFiles = paths.ToList(); // Convert to list. Prevents duplicates from being present
                    repFiles.Sort(NaturalFilenameComparer.Compare);
                    FoundFiles = repFiles.Select(f => new FileChoice(f, true)).ToArray();

                    var missingRepFiles = missingPaths.ToList(); // Convert to list. Prevents duplicates from being present
                    missingRepFiles.Sort(NaturalFilenameComparer.Compare);
                    MissingFiles = missingRepFiles.ToArray();
                }
            }

            public int IncludeFilesCount => FoundFiles.Count(f => f.IsIncluded);
            public int TotalFilesCount => FoundFiles.Count + MissingFiles.Count;
            public int MissingFilesCount => MissingFiles.Count;

            public IList<FileChoice> FoundFiles { get; }
            public IList<string> MissingFiles { get; }

            private IEnumerable<FileChoice> IncludeFileChoices => FoundFiles.Where(f => f.IsIncluded);

            /// <summary>
            /// The set of files and/or directories to pass to ZipFileShare.AddFile().
            /// For .wiff files this requires also including the .wiff.scan files.
            /// For now this is handled here, because this class report the size-on-disk
            /// for each FileChoice.
            /// </summary>
            public IEnumerable<string> FilesToIncludeInZip => IncludeFileChoices.SelectMany(f => f.RootFiles);

            public override string ToString()
            {
                return GetStatusText(IncludeFilesCount, TotalFilesCount, MissingFilesCount);
            }

            /// <summary>
            /// Gets display text about selected files. This includes the number of files the user
            /// has selected to include in the zip file in comparison to the total number of files
            /// in the document. The number of missing files is also displayed alerting the user
            /// to the fact that some files cannot be found based on the document path and the
            /// path stored at the time they were imported.
            /// CONSIDER: Sure would be nice to report the size on disk of the included files
            /// </summary>
            public static string GetStatusText(int includedFilesCount, int totalFilesCount, int missingFilesCount)
            {
                var labelText = string.Format(AlertsResources.AuxiliaryFiles_GetStatusText__0__of__1__files_will_be_included_,
                    includedFilesCount, totalFilesCount);

                if (missingFilesCount != 0)
                {
                    labelText = TextUtil.SpaceSeparate(labelText, string.Format(AlertsResources.AuxiliaryFiles_GetStatusText__0__files_have_not_been_located_,
                        missingFilesCount));
                }
                return labelText;
            }
        }

        /// <summary>
        /// Included or not included status for a file that is known to exist
        /// </summary>
        public class FileChoice
        {
            public FileChoice(string filename, bool isIncluded)
            {
                Filename = filename;
                IsIncluded = isIncluded;
            }

            public string Filename { get; }
            public bool IsIncluded { get; }

            /// <summary>
            /// The directory or set of files to be included in the ZIP file.
            /// In the case of a .wiff file this may also include the .wiff.scan
            /// file. It does not currently expand directories. This is done here
            /// because this class should include the total cost in size on disk
            /// for this choice.
            /// </summary>
            public IEnumerable<string> RootFiles => DataSourceUtil.GetCompanionFiles(Filename);
        }
    }
}