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
using pwiz.Skyline.Model;
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
                    "Fragment Name",
                    "Precursor Charge"
                });
                comboBox.SelectedIndex = 0;
                comboBox.SelectedIndexChanged += comboChanged;       
                ComboHelper.AutoSizeDropDown(comboBox);
            }

            var columns = Importer.RowReader.Indices;

            // It's not unusual to see lines like "744.8 858.39 10 APR.AGLCQTFVYGGCR.y7.light 105 40" where protein, peptide, and label are all stuck together,
            // so that all three lay claim to a single column. In such cases, prioritize peptide.
            columns.PrioritizePeptideColumn();
            Console.WriteLine(Settings.Default.CustomImportTransitionListColumnsList.Count());
            //If there are items on our saved column list, the combo box text is set using that list
            if (Settings.Default.CustomImportTransitionListColumnsList.Count() != 0)
            {
                for (int i = 0; i < Settings.Default.CustomImportTransitionListColumnsList.Count; i++)
                {
                    //The method is called for every tuplet on the list. Item 1 is the index position and item 2 is the name
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
                SetComboBoxText(columns.FragmentNameColumn, "Fragment Name");
                SetComboBoxText(columns.PrecursorChargeColumn, "Precursor Charge");   

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

        // Callback for when a combo box is changed. We use it to update the index of the PeptideColumnIndices and preventing combo boxes from overlapping.
        private void comboChanged(object sender, EventArgs e)  // CONSIDER(bspratt) no charge state columns?
        {
            var comboBox = (ComboBox) sender;
            var comboBoxIndex = ComboBoxes.IndexOf(comboBox);
            var columns = Importer.RowReader.Indices;

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
            else if (comboBox.Text == "Fragment Name")
            {
                CheckForComboBoxOverlap(columns.FragmentNameColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.FragmentNameColumn = comboBoxIndex;
            }
            else if (comboBox.Text == "Precursor Charge")
            {
                CheckForComboBoxOverlap(columns.PrecursorChargeColumn, 0, comboBoxIndex);
                columns.ResetDuplicateColumns(comboBoxIndex);
                columns.PrecursorChargeColumn = comboBoxIndex;
            }
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
                if (columns.PrecursorChargeColumn == comboBoxIndex) columns.PrecursorChargeColumn = -1;
            }
        }
        //Saves column positions between transition lists
        private void updateColumnsList()
        {
            var ColumnList = new List<Tuple<int, string>>();
            var columns = Importer.RowReader.Indices;
            //Adds columns to the list as pairs: the index position and the name
            ColumnList.Add(new Tuple<int, string>(columns.DecoyColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy));
            ColumnList.Add(new Tuple<int, string>(columns.IrtColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT));
            ColumnList.Add(new Tuple<int, string> (columns.LabelTypeColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type));
            ColumnList.Add(new Tuple<int, string> (columns.LibraryColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Relative_Intensity));
            ColumnList.Add(new Tuple<int, string> (columns.PeptideColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence));
            ColumnList.Add(new Tuple<int, string> (columns.PrecursorColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z));
            ColumnList.Add(new Tuple<int, string> (columns.ProductColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z));
            ColumnList.Add(new Tuple<int, string> (columns.ProteinColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name));
            ColumnList.Add(new Tuple<int, string> (columns.FragmentNameColumn, "Fragment Name"));
            ColumnList.Add(new Tuple<int, string> (columns.PrecursorChargeColumn, "Precursor Charge"));

            Settings.Default.CustomImportTransitionListColumnsList = ColumnList;
        }

        private void dataGrid_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            ResizeComboBoxes();
        }
        private void dataGrid_ColumnHeadersHeightChanged(object sender, EventArgs e)
        {
            ResizeComboBoxes();
        }

        private void dataGrid_Scroll(object sender, ScrollEventArgs e)
        {
            comboPanelInner.Location = new Point(-dataGrid.HorizontalScrollingOffset, 0);
        }
        //saves the column indices in a list when the OK button is clicked
        private void buttonOk_Click(object sender, EventArgs e)
        {
            updateColumnsList();
        }
        private void buttonCheckForErrors_Click(object sender, EventArgs e)
        {
            IdentityPath testSelectPath = null;
            List<MeasuredRetentionTime> testIrtPeptides = null;
            List<SpectrumMzInfo> testLibrarySpectra = null;
            List<TransitionImportErrorInfo> testErrorList = null;
            List<PeptideGroupDocNode> testPeptideGroups = null;

            _docCurrent.ImportMassList(_inputs, Importer, null,
                _insertPath, out testSelectPath, out testIrtPeptides, out testLibrarySpectra,
                out testErrorList, out testPeptideGroups);
            if (testErrorList.Any())
            {
                using (var dlg = new ImportTransitionListErrorDlg(testErrorList, true))
                {
                    dlg.ShowDialog(this);
                }
            }
            else
            {
                MessageDlg.Show(this, Resources.PasteDlg_ShowNoErrors_No_errors);
            }
        }

        private void dataGrid_ColumnAdded(object sender, DataGridViewColumnEventArgs e)
        {
            ResizeComboBoxes();
        }

        private void form_Resize(object sender, EventArgs e)
        {
            ResizeComboBoxes();
        }
    }
}  
