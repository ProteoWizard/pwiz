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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// 
    /// </summary>
    public partial class ShareResultsFilesDlg : Form
    {
        private readonly SrmDocument _document;
        public List<string> ReplicateFilesToInclude { get; private set; } // Data files that user wants to include in the .sky.zip file
        public ShareResultsFilesDlg(SrmDocument document) // Srm document passed in to allow access to raw files currently loaded and being used
        {
            InitializeComponent();
            Icon = Resources.Skyline;
            _document = document; // Document for extracting current raw files
            PopulateListView();
        }

        /// <summary>
        /// Populate checked list box based on replicate files currently being used.
        /// File location information is scraped from srm document data structure. Duplicate
        /// file addresses are removed and sorted in natural sort order. Boxes are checked
        /// by default.
        /// </summary>
        private void  PopulateListView()
        {
            var paths = new HashSet<string>(); // List of file paths
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
                    }
                }
                var repFiles = paths.ToList(); // Convert to list. Prevents duplicates from being present
                repFiles.Sort(NaturalComparer.Compare); // Natural Sort
                
                // Add to list view for selection
                checkedListBox.Items.AddRange(paths.ToArray());
                SelectOrDeselectAll(true); // All check box elements start selected

                UpdateLabel();
            }
        }

        /// <summary>
        /// Cancel button pressed. No need to return information. Handled by ShareTypeDlg
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Btn_Cancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// Accept button pressed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Btn_Accept_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        /// <summary>
        /// Add all checked boxes to list of files to be zipped
        /// </summary>
        private void OkDialog()
        {
            ReplicateFilesToInclude = new List<string>();
            foreach (ListViewItem a in checkedListBox.CheckedItems)
            {
                ReplicateFilesToInclude.Add(a.Text); //Get the file path of each checked item
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }


        // /// <summary>
        // /// Switch select all checkbox state and update checked status
        // /// </summary>
        // /// <param name="sender"></param>
        // /// <param name="e"></param>
        private void checkboxSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            if (checkboxSelectAll.CheckState == CheckState.Indeterminate)
            {
                return;
            }
            SelectOrDeselectAll(checkboxSelectAll.CheckState == CheckState.Checked);
            UpdateLabel(); // Update file status label
        }


        /// <summary>
        /// Change the state of all elements in the checkedItemBox
        /// </summary>
        /// <param name="select"></param>
        public void SelectOrDeselectAll(bool select)
        {
            for (int i = 0; i < checkedListBox.Items.Count; i++)
            {
                checkedListBox.SetItemChecked(i, select); // Change checked status for all checked list box items
            }
        }
        

        /// <summary>
        /// Update check 3-state check box to the correct state/graphic
        /// </summary>
        private void UpdateSelectAll()
        {
            int checkCount = checkedListBox.CheckedIndices.Count;
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
        private void checkedListBoxResults_SelectIndexChanged(object sender, EventArgs e)
        {
            UpdateSelectAll(); // Update 3-state box
            UpdateLabel(); // Update file status label
        }


        /// <summary>
        /// Displays information about included files
        /// </summary>
        private void UpdateLabel()
        {
            int count = checkedListBox.Items.Count; // Number of total elements in the checked list box
            int checkCount = checkedListBox.CheckedIndices.Count; // Number of elements currently checked in the checked list box
            if (checkCount == checkedListBox.Items.Count) // All files are selected
            {
                checkedStatus.Text = "All " + count + " files will be included";
            }
            else if(checkedListBox.CheckedItems.Count == 0) // No items are selected
            {
                checkedStatus.Text = "Missing all " + count + " files";
            }
            else
            {
                checkedStatus.Text = checkCount + " out of " + count + " will be included"; // Some number of elements are selected
            }
        }
    }
}