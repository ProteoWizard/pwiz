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
using System.Windows.Forms;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.FileUI
{
    public partial class ImportTransitionListColumnSelectDlg : FormEx
    {
        public MassListImporter Importer { get; set; }
        public List<ComboBox> ComboBoxes { get; private set; }

        public ImportTransitionListColumnSelectDlg(MassListImporter importer)
        {
            Importer = importer;

            InitializeComponent();

            fileLabel.Text = Importer.Inputs.InputFilename;
         
            DisplayData();
            InitalizeComboBoxes();
            PopulateComboBoxes();
            dataGrid.Update();
            ResizeComboBoxes();
        }

        private void DisplayData()
        {
            var table = new DataTable();
            var columns = Importer._rowReader.Lines[0].ParseDsvFields(Importer.Separator).Length;
            for (var i = 0; i < columns; i++)
            {
                table.Columns.Add();
            }
            var dots = new string[columns];
            for (var i = 0; i < columns; i++)
            {
                dots[i] = "...";    // Not L10N
            }
            table.Rows.Add(dots);
            for (var index = 0; index < Math.Min(100, Importer._rowReader.Lines.Count); index++)
            {
                var line = Importer._rowReader.Lines[index];
                table.Rows.Add(line.ParseDsvFields(Importer.Separator));
            }
            if (Importer._rowReader.Lines.Count > 100)
            {
                table.Rows.Add(dots);
            }
            dataGrid.DataSource = table;

            var headersNull = Importer._rowReader.Headers == null;
            for (var i = 0; i < columns; i++)
            {
                var header = !headersNull ? Importer._rowReader.Headers[i] : string.Empty;
                dataGrid.Columns[i].HeaderText = header;
                dataGrid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            dataGrid.ColumnHeadersVisible = !headersNull;
        }

        private void InitalizeComboBoxes()
        {
            ComboBoxes = new List<ComboBox>();
            for (var i = 0; i < dataGrid.Columns.Count; i++)
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
                var comboDataSource = new[]
                {
                    string.Empty, 
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Relative_Intensity,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name
                };
                comboBox.DataSource = comboDataSource;
                comboBox.SelectedIndex = 0;
                comboBox.SelectedIndexChanged += comboChanged;       
                ComboHelper.AutoSizeDropDown(comboBox);
            }

            var columns = Importer._rowReader.Indices;
            SetComboValue(columns.DecoyColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy);
            SetComboValue(columns.IrtColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT);
            SetComboValue(columns.LabelTypeColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type);
            SetComboValue(columns.LibraryColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Relative_Intensity);
            SetComboValue(columns.PeptideColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence);
            SetComboValue(columns.PrecursorColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z);
            SetComboValue(columns.ProductColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z);
            SetComboValue(columns.ProteinColumn, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name);
        }

        private void SetComboValue(int index, string text)
        {
            if(index < 0 || index >= ComboBoxes.Count)
                return;
            ComboBoxes[index].Text = text;
        }

        private void SetComboValue(int index, int text, int index2)
        {
            if (index == index2)
                return;
            if (index < 0 || index >= ComboBoxes.Count)
                return;
            ComboBoxes[index].SelectedIndex = text;
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
                comboBox.Width = column.HeaderCell.Size.Width; // + ((i == dataGrid.Columns.Count - 1) ? 1 : 1); Playing with missing line on last combo box
                height = Math.Max(height, comboBox.Height);
                xOffset += column.HeaderCell.Size.Width;
            }
            var scrollBars = dataGrid.ScrollBars == ScrollBars.Vertical || dataGrid.ScrollBars == ScrollBars.Both;
            var scrollWidth = SystemInformation.VerticalScrollBarWidth;
            var gridWidth = dataGrid.Size.Width - (scrollBars ? scrollWidth : 0) - gridBorderWidth;
            comboPanelOuter.Size = new Size(Math.Min(gridWidth, xOffset), height);
            comboPanelInner.Size = new Size(xOffset, height);
            comboPanelInner.Location = new Point(-dataGrid.HorizontalScrollingOffset, 0);
        }

        private void comboChanged(object sender, EventArgs e)
        {
            var combo = (ComboBox) sender;
            var index = ComboBoxes.IndexOf(combo);
            var columns = Importer._rowReader.Indices;

            if (combo.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy)
            {
                SetComboValue(columns.DecoyColumn, 0, index);
                columns.DecoyColumn = index;
            }
            else if (combo.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_iRT)
            {
                SetComboValue(columns.IrtColumn, 0, index);
                columns.IrtColumn = index;
            }
            else if (combo.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type)
            {
                SetComboValue(columns.LabelTypeColumn, 0, index);
                columns.LabelTypeColumn = index;
            }
            else if (combo.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Relative_Intensity)
            {
                SetComboValue(columns.LibraryColumn, 0, index);
                columns.LibraryColumn = index;
            }
            else if (combo.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence)
            {
                SetComboValue(columns.PeptideColumn, 0, index);
                columns.PeptideColumn = index;
            }
            else if (combo.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z)
            {
                SetComboValue(columns.PrecursorColumn, 0, index);
                columns.PrecursorColumn = index;
            }
            else if (combo.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z)
            {
                SetComboValue(columns.ProductColumn, 0, index);
                columns.ProductColumn = index;
            }
            else if (combo.Text == Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name)
            {
                SetComboValue(columns.ProteinColumn, 0, index);
                columns.ProteinColumn = index;
            }
            else
            {
                if (columns.DecoyColumn == index) columns.DecoyColumn = -1;
                if (columns.IrtColumn == index) columns.IrtColumn = -1;
                if (columns.LabelTypeColumn == index) columns.LabelTypeColumn = -1;
                if (columns.LibraryColumn == index) columns.LibraryColumn = -1;
                if (columns.PeptideColumn == index) columns.PeptideColumn = -1;
                if (columns.PrecursorColumn == index) columns.PrecursorColumn = -1;
                if (columns.ProductColumn == index) columns.ProductColumn = -1;
                if (columns.ProteinColumn == index) columns.ProteinColumn = -1;
            }
        }

        private void dataGrid_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            ResizeComboBoxes();
        }

        private void dataGrid_Scroll(object sender, ScrollEventArgs e)
        {
            comboPanelInner.Location = new Point(-dataGrid.HorizontalScrollingOffset, 0);
        }

        private void dataGrid_ColumnHeadersHeightChanged(object sender, EventArgs e)
        {
            ResizeComboBoxes();
        }
    }
}
