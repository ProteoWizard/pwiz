/*
 * Original author: Alex MacLean <alex.maclean2000 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class ImportTransitionListColumnSelectDlg : ModeUIInvariantFormEx
    {
        public MassListImporter Importer { get; set; }
        public List<ComboBox> ComboBoxes { get; private set; }

        // These are only for error checking
        private readonly SrmDocument _docCurrent;
        private readonly MassListInputs _inputs;
        private readonly IdentityPath _insertPath;

        public ImportTransitionListColumnSelectDlg(MassListImporter importer, SrmDocument docCurrent, MassListInputs inputs, IdentityPath insertPath)
        {
            Importer = importer;
            _docCurrent = docCurrent;
            _inputs = inputs;
            _insertPath = insertPath;

            InitializeComponent();

            fileLabel.Text = Importer.Inputs.InputFilename;

            InitializeComboBoxes();
            DisplayData();
            PopulateComboBoxes();
            //dataGrid.Update();
            ResizeComboBoxes();
        }

        private void DisplayData()
        {
            // The pasted data will be stored as a data table
            var table = new DataTable("TransitionList");

            // Create the first row of columns
            var numColumns = Importer.RowReader.Lines[0].ParseDsvFields(Importer.Separator).Length;
            for (var i = 0; i < numColumns; i++)
                table.Columns.Add().DataType = typeof(string);

            // These dots are a placeholder for where the combo boxes will be
            var dots = Enumerable.Repeat(@"...", numColumns).ToArray();
            // The first row will actually be combo boxes, but we use dots as a placeholder because we can't put combo boxes in a data table
            table.Rows.Add(dots);

            // Add the data
            for (var index = 0; index < Math.Min(100, Importer.RowReader.Lines.Count); index++)
            {
                var line = Importer.RowReader.Lines[index];
                table.Rows.Add(line.ParseDsvFields(Importer.Separator));
            }

            // Don't bother displaying more than 100 lines of data
            if (Importer.RowReader.Lines.Count > 100)
                table.Rows.Add(dots);

            // Set the table as the source for the DataGridView that the user sees.
            dataGrid.DataSource = table;

            var headers = Importer.RowReader.Indices.Headers;
            if (headers != null && headers.Length > 0)
            {
                for (var i = 0; i < numColumns; i++)
                    dataGrid.Columns[i].HeaderText = headers[i];
                dataGrid.ColumnHeadersVisible = true;
            }

            dataGrid.ScrollBars = dataGrid.Rows.Count * dataGrid.Rows[0].Height + dataGrid.ColumnHeadersHeight + SystemInformation.HorizontalScrollBarHeight > dataGrid.Height 
                ? ScrollBars.Both : ScrollBars.Horizontal;
        }

        private void InitializeComboBoxes()
        {
            ComboBoxes = new List<ComboBox>();
            for (var i = 0; i < Importer.RowReader.Lines[0].ParseDsvFields(Importer.Separator).Length; i++)
            {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                ComboBoxes.Add(combo);
                comboPanelInner.Controls.Add(combo);
                combo.BringToFront();
            }
        }

        private void PopulateComboBoxes()
        {
            foreach (var comboBox in ComboBoxes)
            {
                comboBox.Text = string.Empty;
                comboBox.Items.AddRange(new object[] {
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Relative_Intensity,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Fragment_Name,
                    // Commented out for consistency because there is no product charge column
                    // Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge
                });
                comboBox.SelectedIndex = 0;
                comboBox.SelectedIndexChanged += ComboChanged;       
                ComboHelper.AutoSizeDropDown(comboBox);
            }

            var columns = Importer.RowReader.Indices;

            // It's not unusual to see lines like "744.8 858.39 10 APR.AGLCQTFVYGGCR.y7.light 105 40" where protein, peptide, and label are all stuck together,
            // so that all three lay claim to a single column. In such cases, prioritize peptide.
            columns.PrioritizePeptideColumn();
            var headers = Importer.RowReader.Indices.Headers;
            // Checks if the headers of the current list are the same as the headers of the previous list,
            // because if they are then we want to prioritize user headers
            bool sameHeaders = false;
            if (headers != null)
            {
                sameHeaders = (headers.ToList().SequenceEqual(Settings.Default.CustomImportTransitionListHeaders));
            }
            // If there are items on our saved column list and the file does not contain headers (or the headers are the same as the previous file),
            // and the number of columns matches the saved column count then the combo box text is set using that list
            if ((Settings.Default.CustomImportTransitionListColumnsList.Count != 0) && ((headers == null) || (sameHeaders)) && Importer.RowReader.Lines[0].ParseDsvFields(Importer.Separator).Length == Settings.Default.CustomImportTransitionListColumnCount)
            {
                for (int i = 0; i < Settings.Default.CustomImportTransitionListColumnsList.Count; i++)
                {
                    // The method is called for every tuplet on the list. Item 1 is the index position and item 2 is the name
                    SetComboBoxText(Settings.Default.CustomImportTransitionListColumnsList[i].Item1, Settings.Default.CustomImportTransitionListColumnsList[i].Item2);
                }
            }
            else {
                SetComboBoxText(columns.DecoyColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy);
                SetComboBoxText(columns.IrtColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT);
                SetComboBoxText(columns.LabelTypeColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type);
                SetComboBoxText(columns.LibraryColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Relative_Intensity);
                SetComboBoxText(columns.PeptideColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence);
                SetComboBoxText(columns.PrecursorColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z);
                SetComboBoxText(columns.ProductColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z);
                SetComboBoxText(columns.ProteinColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name);
                SetComboBoxText(columns.FragmentNameColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Fragment_Name);
                // Commented out for consistency because there is no product charge column
                // SetComboBoxText(columns.PrecursorChargeColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge);   

            }
        }

        public void ResizeComboBoxes()
        {
            const int gridBorderWidth = 1;
            comboPanelOuter.Location = new Point(dataGrid.Location.X + gridBorderWidth,
                dataGrid.Location.Y + (dataGrid.ColumnHeadersVisible ? dataGrid.ColumnHeadersHeight : 1));
            
            var xOffset = 0;
            var height = 0;

            for (var i = 0; i < dataGrid.Columns.Count; i++)
            {
                var column = dataGrid.Columns[i];
                var comboBox = ComboBoxes[i];

                comboBox.Location = new Point(xOffset, 0);
                comboBox.Width = column.Width; // + ((i == dataGrid.Columns.Count - 1) ? 1 : 1); Playing with missing line on last combo box
                height = Math.Max(height, comboBox.Height);
                xOffset += column.Width;
            }
            
            var scrollBars = dataGrid.ScrollBars == ScrollBars.Both;
            var scrollWidth = SystemInformation.VerticalScrollBarWidth;
            var gridWidth = dataGrid.Size.Width - (scrollBars ? scrollWidth : 0) - (2 * gridBorderWidth);
            comboPanelOuter.Size = new Size(gridWidth, height);
            comboPanelInner.Size = new Size(xOffset, height);
            comboPanelInner.Location = new Point(-dataGrid.HorizontalScrollingOffset, 0);
        }

        // Sets the text of a combo box, with error checking
        private void SetComboBoxText(int comboBoxIndex, string text)
        {
            if (comboBoxIndex < 0 || comboBoxIndex >= ComboBoxes.Count)
                return;
            ComboBoxes[comboBoxIndex].Text = text;
        }

        // Ensures two combo boxes do not have the same value. Usually newSelectedIndex will be zero, because that is IgnoreColumn.
        private void CheckForComboBoxOverlap(int indexOfPreviousComboBox, int newSelectedIndex, int indexOfNewComboBox)
        {
            if (indexOfPreviousComboBox == indexOfNewComboBox || indexOfPreviousComboBox < 0 || indexOfPreviousComboBox >= ComboBoxes.Count)
                return;
            ComboBoxes[indexOfPreviousComboBox].SelectedIndex = newSelectedIndex;
        }

        private bool comboBoxChanged;
        // Callback for when a combo box is changed. We use it to update the index of the PeptideColumnIndices and preventing combo boxes from overlapping.
        private void ComboChanged(object sender, EventArgs e)  // CONSIDER(bspratt) no charge state columns? (Seems to be because Skyline infers these and is confused when given explicit values)
        {
            var comboBox = (ComboBox) sender;
            var comboBoxIndex = ComboBoxes.IndexOf(comboBox);
            var columns = Importer.RowReader.Indices;
            comboBoxChanged = true;

            if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy)
            {
                CheckForComboBoxOverlap(columns.DecoyColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.DecoyColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT)
            {
                CheckForComboBoxOverlap(columns.IrtColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.IrtColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type)
            {
                CheckForComboBoxOverlap(columns.LabelTypeColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.LabelTypeColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Relative_Intensity)
            {
                CheckForComboBoxOverlap(columns.LibraryColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.LibraryColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence)
            {
                CheckForComboBoxOverlap(columns.PeptideColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.PeptideColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z)
            {
                CheckForComboBoxOverlap(columns.PrecursorColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.PrecursorColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z)
            {
                CheckForComboBoxOverlap(columns.ProductColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ProductColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name)
            {
                CheckForComboBoxOverlap(columns.ProteinColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.ProteinColumn = comboBoxIndex;
            }
            else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Fragment_Name)
            {
                CheckForComboBoxOverlap(columns.FragmentNameColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.FragmentNameColumn = comboBoxIndex;
            }
            // Commented out for consistency because there is no product charge column
            /*else if (comboBox.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge)
            {
                CheckForComboBoxOverlap(columns.PrecursorChargeColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.PrecursorChargeColumn = comboBoxIndex;
            }*/
            else
            {
                if (columns.DecoyColumn == comboBoxIndex) columns.DecoyColumn = -1;
                if (columns.IrtColumn == comboBoxIndex) columns.IrtColumn = -1;
                if (columns.LabelTypeColumn == comboBoxIndex) columns.LabelTypeColumn = -1;
                if (columns.LibraryColumn == comboBoxIndex) columns.LibraryColumn = -1;
                if (columns.PeptideColumn == comboBoxIndex) columns.PeptideColumn = -1;
                if (columns.PrecursorColumn == comboBoxIndex) columns.PrecursorColumn = -1;
                if (columns.ProductColumn == comboBoxIndex) columns.ProductColumn = -1;
                if (columns.ProteinColumn == comboBoxIndex) columns.ProteinColumn = -1;
                if (columns.FragmentNameColumn == comboBoxIndex) columns.FragmentNameColumn = -1;
                // Commented out for consistency because there is no product charge column
                // if (columns.PrecursorChargeColumn == comboBoxIndex) columns.PrecursorChargeColumn = -1;
            }
        }
        // Saves column positions between transition lists
        private void UpdateColumnsList()
        {
            var ColumnList = new List<Tuple<int, string>>();
            var columns = Importer.RowReader.Indices;
            // Adds columns to the list as pairs: the index position and the name
            ColumnList.Add(new Tuple<int, string>(columns.DecoyColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy));
            ColumnList.Add(new Tuple<int, string>(columns.IrtColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT));
            ColumnList.Add(new Tuple<int, string> (columns.LabelTypeColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type));
            ColumnList.Add(new Tuple<int, string> (columns.LibraryColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Relative_Intensity));
            ColumnList.Add(new Tuple<int, string> (columns.PeptideColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence));
            ColumnList.Add(new Tuple<int, string> (columns.PrecursorColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z));
            ColumnList.Add(new Tuple<int, string> (columns.ProductColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z));
            ColumnList.Add(new Tuple<int, string> (columns.ProteinColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name));
            ColumnList.Add(new Tuple<int, string> (columns.FragmentNameColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Fragment_Name));
            // Commented out for consistency because there is no product charge column yet
            // ColumnList.Add(new Tuple<int, string> (columns.PrecursorChargeColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge));

            Settings.Default.CustomImportTransitionListColumnsList = ColumnList;

            Settings.Default.CustomImportTransitionListColumnCount =
                Importer.RowReader.Lines[0].ParseDsvFields(Importer.Separator).Length;
        }
        // Saves a list of the current document's headers, if any exist, so that they can be compared to those of the next document
        private void UpdateHeadersList()
        {
            var headers = Importer.RowReader.Indices.Headers;
            if (headers != null && headers.Length > 0)
            {
                Settings.Default.CustomImportTransitionListHeaders = headers.ToList();
            }
        }
        private void DataGrid_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            ResizeComboBoxes();
        }
        private void DataGrid_ColumnHeadersHeightChanged(object sender, EventArgs e)
        {
            ResizeComboBoxes();
        }

        private void DataGrid_Scroll(object sender, ScrollEventArgs e)
        {
            comboPanelInner.Location = new Point(-dataGrid.HorizontalScrollingOffset, 0);
        }
        // If a combo box was changed, save the column indices and column count when the OK button is clicked
        private void ButtonOk_Click(object sender, EventArgs e)
        {
            if (comboBoxChanged)
            {
                UpdateColumnsList();
                UpdateHeadersList();
            }
        }

        private void ImportTransitionListColumnSelectDlg_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                if (CheckForErrors(true)) // Look for errors, be silent on success
                {
                    e.Cancel = true; // Errors found, don't close yet
                }
            }
        }

        private void ButtonCheckForErrors_Click(object sender, EventArgs e)
        {
            CheckForErrors(false);
        }

        private static List<string> MissingEssentialColumns { get; set; }
        // If an essential column is missing, add it to a list to display later
        private void CheckEssentialColumn(Tuple<int, string> column)
        {
            if (column.Item1 == -1)
            {
                MissingEssentialColumns.Add(column.Item2);
            }
        }
        /// <summary>
        /// Parse the mass list text, then show a status dialog if:
        ///     errors are found, or
        ///     errors are not found and "silentSuccess" arg is false
        /// Shows a special error message and forces the user to alter their entry if the list is missing Precursor m/z, Product m/z or Peptide Sequence.
        /// Return false if no errors found.
        /// </summary>
        /// <param name="silentSuccess">If true, don't show the confirmation dialog when there are no errors</param>
        /// <returns>True if list contains any errors and user does not elect to ignore them</returns>
        private bool CheckForErrors(bool silentSuccess)
        {
            IdentityPath testSelectPath = null;
            List<MeasuredRetentionTime> testIrtPeptides = null;
            List<SpectrumMzInfo> testLibrarySpectra = null;
            List<TransitionImportErrorInfo> testErrorList = null;
            List<PeptideGroupDocNode> testPeptideGroups = null;
            var columns = Importer.RowReader.Indices;
            MissingEssentialColumns = new List<string>();
            CheckEssentialColumn(new Tuple<int, string>(columns.PeptideColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence));
            CheckEssentialColumn(new Tuple<int, string>(columns.PrecursorColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z));
            CheckEssentialColumn(new Tuple<int, string>(columns.ProductColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z));
            var docNew = _docCurrent.ImportMassList(_inputs, Importer, null,
                _insertPath, out testSelectPath, out testIrtPeptides, out testLibrarySpectra,
                out testErrorList, out testPeptideGroups);
            if (testErrorList.Any())
            {
                // There are errors, show them to user
                var isErrorAll = ReferenceEquals(docNew, _docCurrent);
                DialogResult response;
                if (MissingEssentialColumns.Count != 0)
                {
                    // If the transition list is missing essential columns, tell the user in a 
                    // readable way
                    string errorMessage = Resources.ImportTransitionListErrorDlg_ImportTransitionListErrorDlg_This_transition_list_cannot_be_imported_as_it_does_not_provide_values_for_;
                    for (var i = 0; i < MissingEssentialColumns.Count; i++)
                    {
                        errorMessage = errorMessage + @" " + MissingEssentialColumns[i];
                    }
                    MessageBox.Show(errorMessage);
                    return true;
                }
                else
                {
                    using (var dlg = new ImportTransitionListErrorDlg(testErrorList, isErrorAll, silentSuccess))
                    {
                        response = dlg.ShowDialog(this);
                    }

                    return response == DialogResult.Cancel; // There are errors, and user does not want to ignore them
                }
            }
            else if (!silentSuccess) 
            {
                // No errors, confirm this to user
                MessageDlg.Show(this, Resources.PasteDlg_ShowNoErrors_No_errors);
            }

            return false; // No errors
        }

        private void dataGrid_ColumnAdded(object sender, DataGridViewColumnEventArgs e)
        {
            ResizeComboBoxes();
        }

        private void form_Resize(object sender, EventArgs e)
        {
            ResizeComboBoxes();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {

        }
    }
}  
