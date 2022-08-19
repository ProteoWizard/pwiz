/*
 * Original author: Clark Brace <clarkbrace@gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// Added by Clark Brace (cbrace3)
    /// Share results dialog is intended to allow users to select raw files they wish to include when sharing
    /// their current Skyline document. Information regarding which raw files are currently being used in Skyline
    /// are collected and displayed allowing the user to zip send everything in one clean package. Files are
    /// displayed in natural sort order and await upon the user's selection. Should files be missing/not accessible
    /// by the document the user can either select individual files utilizing the OpenDataSourceDialog or select
    /// the folder where missing files are known to be using the FolderBrowserDialog. Missing files that are found
    /// are added to the selection of files the user can choose from in their selection. By default missing files
    /// are checked when discovered and added.
    /// </summary>
    public partial class ShareResultsFilesDlg : Form
    {
        private readonly SrmDocument _document; // Document of the current open project where files and potential locations are extracted
        public AuxiliaryFiles _auxiliaryFiles; // Previous file selection 

        // public List<string> ReplicateFilesToInclude { get; private set; } // Data files that user wants to include in the .sky.zip file
        
        public ShareResultsFilesDlg(SrmDocument document, AuxiliaryFiles auxiliaryFiles) // Srm document passed in to allow access to raw files currently loaded and being used
        {
            InitializeComponent();
            Icon = Resources.Skyline; // Have the skyline icon in the upper left corner
            _document = document; // Document for extracting current raw files
            _auxiliaryFiles = auxiliaryFiles; // Load in the skyline auxiliary files which may or may not already exist
            PopulateListView(); // Populate the selection and missing files list boxes
            checkedStatus.Text = UpdateLabel(); // Update the status label on the bottom about the file status
            UpdateSelectAll(); // Checkbox selection to ensure it displays the correct graphic
            UpdateAuxFiles(); // Selection state of files
        }

        /// <summary>
        /// Populate checked list box based on replicate files currently being used.
        /// File location information is scraped from srm document data structure. Duplicate
        /// file addresses are removed and sorted in natural sort order. Boxes are checked
        /// by default. Should previous selections already exist load them in as opposed to
        /// searching again.
        /// </summary>
        private void PopulateListView()
        {
            // Should the users selection already exist load them into the appropriate list boxes
            if (_auxiliaryFiles != null)
            {
                foreach (var checkBoxItems in _auxiliaryFiles._checkBoxFiles)
                {
                    checkedListBox.Items.Add(checkBoxItems.Filename, checkBoxItems.CheckedState);
                }

                foreach (var missingFile in _auxiliaryFiles._missingCheckBoxFiles)
                {
                    missingListBox.Items.Add(missingFile);
                }
                return;
            }
            
            // Search for files based on their expected location 
            var paths = new HashSet<string>(); // List of file paths
            var missingPaths = new HashSet<string>(); // List of missing paths
            if (_document.Settings.MeasuredResults != null)
            {
                foreach (var chromatogramSet in _document.Settings.MeasuredResults.Chromatograms)
                {
                    foreach (var chromFileInfo in chromatogramSet.MSDataFileInfos)
                    {
                        // Check for path validity, using our standard rules for locating data files when they aren't in current working directory
                        if (ScanProvider.FileExists(Program.MainWindow.DocumentFilePath, chromFileInfo.FilePath, out var path))
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
                repFiles.Sort(NaturalComparer.Compare); // Natural Sort
                
                // Add to list view for selection
                checkedListBox.Items.AddRange(paths.ToArray());
                SelectOrDeselectAll(true); // All check box elements start selected

                var missingRepFiles = missingPaths.ToList(); // Convert to list. Prevents duplicates from being present
                missingRepFiles.Sort(NaturalComparer.Compare);

                missingListBox.Items.AddRange(missingRepFiles.ToArray()); //Add all elements present to 
            }
        }


        /// <summary>
        /// Accept button pressed. Selection information saved
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Btn_Accept_Click(object sender, EventArgs e)
        {
            OkDialog();
        }


        /// <summary>
        /// Save all list box and checked list box information about the users current
        /// selection of what to included in the zip file as well as what is still currently
        /// missing.
        /// </summary>
        public void UpdateAuxFiles()
        {
            var checkedItems = (from object checkedItem in checkedListBox.Items
                select checkedListBox.CheckedItems.Contains(checkedItem)
                    ? new CheckedListBoxItems(checkedItem.ToString(), true)
                    : new CheckedListBoxItems(checkedItem.ToString(), false)).ToList();

            // Add all missing auxiliary files to list
            var missingItems = (from object missingItem in missingListBox.Items select missingItem.ToString()).ToList();

            // Save auxiliary file selection 
            _auxiliaryFiles = new AuxiliaryFiles(checkedItems, missingItems); // Save users file selection
        }


        /// <summary>
        ///  Close form after updating files
        /// </summary>
        public void OkDialog()
        {
            // Add auxiliary file data
            UpdateAuxFiles();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }


        /// <summary>
        /// Switch select all checkbox state and update checked status
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckboxSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            if (checkboxSelectAll.CheckState == CheckState.Indeterminate)
            {
                return;
            }
            SelectOrDeselectAll(checkboxSelectAll.CheckState == CheckState.Checked);
            checkedStatus.Text = UpdateLabel(); // Update file status label
        }


        /// <summary>
        /// Change the state of all elements in the checkedItemBox
        /// </summary>
        /// <param name="select">true to select all, false to deselect all</param>
        public void SelectOrDeselectAll(bool select)
        {
            for (var i = 0; i < checkedListBox.Items.Count; i++)
            {
                checkedListBox.SetItemChecked(i, select); // Change checked status for all checked list box items
            }
        }
        

        /// <summary>
        /// Update check 3-state check box to the correct state/graphic
        /// </summary>
        private void UpdateSelectAll()
        {
            var checkCount = checkedListBox.CheckedIndices.Count;
            if (checkCount == checkedListBox.Items.Count)
            {
                checkboxSelectAll.CheckState = checkCount == 0 ? CheckState.Indeterminate : CheckState.Checked; 
            }
            else
            {
                checkboxSelectAll.CheckState = checkCount == 0 ? CheckState.Unchecked : CheckState.Indeterminate;
            }
        }


        /// <summary>
        /// Update 3-state check box and file selection information on checked list box update
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckedListBoxResults_SelectIndexChanged(object sender, EventArgs e)
        {
            UpdateSelectAll(); // Update 3-state box
            checkedStatus.Text = UpdateLabel(); // Update file status label
        }


        /// <summary>
        /// Displays information about selected files. This includes the number of files the user
        /// has selected to included in the zip file in comparison to the total number of files
        /// the document sees the loaded skyline document uses. The number of missing files is
        /// also displayed alerting the user to the fact that some files my be located in a different
        /// place than the document thinks they are.
        /// </summary>
        ///
        public string UpdateLabel()
        {
            string fileIncludingLabel; 
            int total = checkedListBox.Items.Count + missingListBox.Items.Count; // Number of total files
            int checkCount = checkedListBox.CheckedIndices.Count; // Number of elements currently checked in the checked list box
            int missingCount = missingListBox.Items.Count;
            if (checkCount == total) // All files are selected
            {
                fileIncludingLabel = $"All {total} files will be included.";
            }
            else if (checkedListBox.CheckedItems.Count == 0) // No items are selected
            {
                fileIncludingLabel = $"None of the {total} files will be included.";
            }
            else
            {
                fileIncludingLabel = $"{checkCount} out of {total} files will be included."; // Some number of elements are selected
            }
            
            if (missingCount != 0)
            {
                fileIncludingLabel += $" Missing {missingCount} file(s).";

            }
            return fileIncludingLabel;
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
            if (missingListBox.Items.Count > 0)
            {
                // Get list of all files that are currently missing
                var missingFiles = (from object missingItem in missingListBox.Items select missingItem.ToString()).ToList();

                using var openDataSource = new OpenDataSourceDialog(Settings.Default.RemoteAccountList, missingFiles);
                // Find location of the current directory
                var docPath = Program.MainWindow.DocumentFilePath;

                // Set initial directory to default if not applicable
                var initialDir = Path.GetDirectoryName(docPath) ?? Settings.Default.SrmResultsDirectory;
                if (string.IsNullOrEmpty(initialDir))
                    initialDir = null;

                // Set the initial directory to the current working directory if present
                openDataSource.InitialDirectory = new MsDataFilePath(initialDir);

                // Discovered missing files
                var paths = new HashSet<string>();
                // Use dialog to search for files
                if (openDataSource.ShowDialog(this) == DialogResult.OK) // TODO find out what to put here
                {
                    var currentDirectory = openDataSource.CurrentDirectory; // Current directory from dialog selection
                    var selectedFiles = openDataSource.SelectedFiles; // Selected files from dialog (file name)

                    // Search through all selected files
                    foreach (var selectedFile in selectedFiles)
                    { 
                        // Check if the selected file is one of those missing
                        if (missingListBox.Items.Contains(selectedFile))
                        { 
                            // True file path to be checked against
                            var filePath = Path.Combine(currentDirectory.GetFilePath(), selectedFile);

                            // Confirm the path is correct
                            if (ScanProvider.FileExists(filePath, currentDirectory))
                            {
                                paths.Add(filePath); // Add confirmed path to map
                                missingListBox.Items.Remove(selectedFile); // Removed found file from missing lis box
                            }
                        }
                    }
                    // Add each of the discovered missing files to the checked list box checked by default
                    foreach (var discoveredMissingFiles in paths)
                    {
                        checkedListBox.Items.Add(discoveredMissingFiles, true);
                    }
                }
            }
            else
            {
                // Message informing the user of the fact there are no longer any missing files and thus no need to search for more
                MessageBox.Show($"All relevant files are present");
            }
            checkedStatus.Text = UpdateLabel(); // Update the label after potential changes
        }

        /// <summary>
        /// Double click functionality allowing users to browse for missing files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MissingListBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            LocateMissingFiles();
        }

        /// <summary>
        /// Button allowing users to browse for missing files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Btn_addFiles_Click(object sender, EventArgs e)
        {
            LocateMissingFiles();
        }


        /// <summary>
        /// Select and add all missing files from folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void FindResultsFolder_Click(object sender, EventArgs e)
        {
            LocateMissingFilesFromFolder();
        }

        /// <summary>
        /// Allow the user to select a folder to search for missing files from
        /// </summary>
        public void LocateMissingFilesFromFolder()
        {
            // Ask the user for the directory to search
            using var searchFolderDialog = new FolderBrowserDialog();
            searchFolderDialog.ShowNewFolderButton = false;

            var docPath = Program.MainWindow.DocumentFilePath;

            // Set initial directory to default if not applicable
            var initialDir = Path.GetDirectoryName(docPath) ?? Settings.Default.SrmResultsDirectory;
            if (string.IsNullOrEmpty(initialDir))
                initialDir = null;

            // Set the initial directory to the current working directory if present
            searchFolderDialog.SelectedPath = initialDir;

            searchFolderDialog.Description = $"Please select the folder containing the missing files"; // Description

            if (searchFolderDialog.ShowDialog() == DialogResult.OK)
            {
                SearchDirectoryForMissingFiles(searchFolderDialog.SelectedPath);
            }
        }

        /// <summary>
        /// Preforms search for missing items within directory given directory path. Split
        /// for testing purposes and incompatibility with form types.
        /// </summary>
        /// <param name="folderPath"></param>
        public void SearchDirectoryForMissingFiles(string folderPath)
        {
            // Get all file/directory information from given folder path
            var files = Directory.GetFiles(folderPath);
            var folders = Directory.GetDirectories(folderPath);
            var set = new HashSet<string>();

            // Add all file paths in folder to hash set
            foreach (var file in files)
            {
                set.Add(file);
            }

            // Add all folder paths in folder to hash set
            foreach (var folder in folders)
            {
                set.Add(folder);
            }

            set.Remove(null);

            // Check if the folder contained any missing items
            foreach (var rawFiles in set)
            {
                if (missingListBox.Items.Contains(Path.GetDirectoryName(rawFiles) != null) || missingListBox.Items.Contains(Path.GetFileName(rawFiles)))
                {
                    checkedListBox.Items.Add(rawFiles, true);
                    missingListBox.Items.Remove(Path.GetDirectoryName(rawFiles) != null);
                    missingListBox.Items.Remove(Path.GetFileName(rawFiles));
                }
            }
            checkedStatus.Text = UpdateLabel();
        }


        /// <summary>
        /// All list box information used to store the state of the from
        /// </summary>
        public class AuxiliaryFiles
        {
            public List<CheckedListBoxItems> _checkBoxFiles;
            public List<string> _missingCheckBoxFiles;

            public AuxiliaryFiles(List<CheckedListBoxItems> checkedFiles, List<string> missingFiles)
            {
                _checkBoxFiles = checkedFiles;
                _missingCheckBoxFiles = missingFiles;
            }
        }

        /// <summary>
        /// Checked list box information
        /// </summary>
        public class CheckedListBoxItems
        {
            public string Filename { get; }
            public bool CheckedState { get; }

            public CheckedListBoxItems(string filename, bool checkedState)
            {
                Filename = filename;
                CheckedState = checkedState;
            }
        }
    }
}