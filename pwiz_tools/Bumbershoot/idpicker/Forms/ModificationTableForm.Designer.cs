//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

namespace IDPicker.Forms
{
    partial class ModificationTableForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose (bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent ()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.exportButton = new System.Windows.Forms.ToolStripButton();
            this.exportMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyToClipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.copySelectedCellsToClipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportSelectedCellsToFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSelectedCellsInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.minRowLabel = new System.Windows.Forms.ToolStripLabel();
            this.minRowTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.minColumnTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.minColumnLabel = new System.Windows.Forms.ToolStripLabel();
            this.unimodButton = new System.Windows.Forms.ToolStripButton();
            this.roundToNearestUpDown = new IDPicker.Controls.ToolStripNumericUpDown();
            this.pivotModeComboBox = new System.Windows.Forms.ToolStripComboBox();
            this.viewModeComboBox = new System.Windows.Forms.ToolStripComboBox();
            this.minMatchesLabel = new System.Windows.Forms.ToolStripLabel();
            this.roundToNearestLabel = new System.Windows.Forms.ToolStripLabel();
            this.minMatchesTextBox = new System.Windows.Forms.ToolStripTextBox();
            this.dataGridView = new IDPicker.Controls.AutomationDataGridView();
            this.detailDataGridView = new IDPicker.Controls.AutomationDataGridView();
            this.accessionColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.offsetColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.siteColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.massColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.probabilityColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.peptidesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.matchesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.spectraColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.unimodColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.invalidFilterLabel = new System.Windows.Forms.Label();
            this.exportMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.detailDataGridView)).BeginInit();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // exportButton
            // 
            this.exportButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.exportButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(23, 23);
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // exportMenu
            // 
            this.exportMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyToClipboardToolStripMenuItem,
            this.exportToFileToolStripMenuItem,
            this.showInExcelToolStripMenuItem,
            this.toolStripSeparator1,
            this.copySelectedCellsToClipboardToolStripMenuItem,
            this.exportSelectedCellsToFileToolStripMenuItem,
            this.showSelectedCellsInExcelToolStripMenuItem});
            this.exportMenu.Name = "contextMenuStrip1";
            this.exportMenu.Size = new System.Drawing.Size(247, 142);
            // 
            // copyToClipboardToolStripMenuItem
            // 
            this.copyToClipboardToolStripMenuItem.Name = "copyToClipboardToolStripMenuItem";
            this.copyToClipboardToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.copyToClipboardToolStripMenuItem.Text = "Copy to Clipboard";
            this.copyToClipboardToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // exportToFileToolStripMenuItem
            // 
            this.exportToFileToolStripMenuItem.Name = "exportToFileToolStripMenuItem";
            this.exportToFileToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.exportToFileToolStripMenuItem.Text = "Export to File";
            this.exportToFileToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // showInExcelToolStripMenuItem
            // 
            this.showInExcelToolStripMenuItem.Name = "showInExcelToolStripMenuItem";
            this.showInExcelToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.showInExcelToolStripMenuItem.Text = "Show in Excel";
            this.showInExcelToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(243, 6);
            // 
            // copySelectedCellsToClipboardToolStripMenuItem
            // 
            this.copySelectedCellsToClipboardToolStripMenuItem.Name = "copySelectedCellsToClipboardToolStripMenuItem";
            this.copySelectedCellsToClipboardToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.copySelectedCellsToClipboardToolStripMenuItem.Text = "Copy Selected Cells to Clipboard";
            this.copySelectedCellsToClipboardToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // exportSelectedCellsToFileToolStripMenuItem
            // 
            this.exportSelectedCellsToFileToolStripMenuItem.Name = "exportSelectedCellsToFileToolStripMenuItem";
            this.exportSelectedCellsToFileToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.exportSelectedCellsToFileToolStripMenuItem.Text = "Export Selected Cells to File";
            this.exportSelectedCellsToFileToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // showSelectedCellsInExcelToolStripMenuItem
            // 
            this.showSelectedCellsInExcelToolStripMenuItem.Name = "showSelectedCellsInExcelToolStripMenuItem";
            this.showSelectedCellsInExcelToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.showSelectedCellsInExcelToolStripMenuItem.Text = "Show Selected Cells in Excel";
            this.showSelectedCellsInExcelToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // minRowLabel
            // 
            this.minRowLabel.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.minRowLabel.Name = "minRowLabel";
            this.minRowLabel.Size = new System.Drawing.Size(89, 23);
            this.minRowLabel.Text = "Row minimum:";
            // 
            // minRowTextBox
            // 
            this.minRowTextBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.minRowTextBox.Name = "minRowTextBox";
            this.minRowTextBox.Size = new System.Drawing.Size(47, 26);
            this.minRowTextBox.Text = "2";
            this.minRowTextBox.Leave += new System.EventHandler(this.ModFilter_Leave);
            this.minRowTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.MinCountFilter_KeyPress);
            // 
            // minColumnTextBox
            // 
            this.minColumnTextBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.minColumnTextBox.Name = "minColumnTextBox";
            this.minColumnTextBox.Size = new System.Drawing.Size(47, 26);
            this.minColumnTextBox.Text = "2";
            this.minColumnTextBox.Leave += new System.EventHandler(this.ModFilter_Leave);
            this.minColumnTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.MinCountFilter_KeyPress);
            // 
            // minColumnLabel
            // 
            this.minColumnLabel.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.minColumnLabel.Name = "minColumnLabel";
            this.minColumnLabel.Size = new System.Drawing.Size(109, 23);
            this.minColumnLabel.Text = "Column minimum:";
            // 
            // unimodButton
            // 
            this.unimodButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.unimodButton.Name = "unimodButton";
            this.unimodButton.Size = new System.Drawing.Size(83, 23);
            this.unimodButton.Text = "Unimod Filter";
            this.unimodButton.Click += new System.EventHandler(this.unimodButton_Click);
            // 
            // roundToNearestUpDown
            // 
            this.roundToNearestUpDown.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.roundToNearestUpDown.DecimalPlaces = 4;
            this.roundToNearestUpDown.Increment = new decimal(new int[] {
            1,
            0,
            0,
            262144});
            this.roundToNearestUpDown.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.roundToNearestUpDown.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            262144});
            this.roundToNearestUpDown.Name = "roundToNearestUpDown";
            this.roundToNearestUpDown.Size = new System.Drawing.Size(62, 23);
            this.roundToNearestUpDown.Text = "0.0001";
            this.roundToNearestUpDown.Value = new decimal(new int[] {
            1,
            0,
            0,
            262144});
            this.roundToNearestUpDown.ValueChanged += new System.EventHandler(this.roundToNearestUpDown_ValueChanged);
            // 
            // pivotModeComboBox
            // 
            this.pivotModeComboBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.pivotModeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.pivotModeComboBox.Items.AddRange(new object[] {
            "Spectra",
            "Distinct Matches",
            "Distinct Peptides"});
            this.pivotModeComboBox.Name = "pivotModeComboBox";
            this.pivotModeComboBox.Size = new System.Drawing.Size(106, 26);
            this.pivotModeComboBox.SelectedIndexChanged += new System.EventHandler(this.pivotModeComboBox_SelectedIndexChanged);
            // 
            // viewModeComboBox
            // 
            this.viewModeComboBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.viewModeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.viewModeComboBox.DropDownWidth = 120;
            this.viewModeComboBox.Items.AddRange(new object[] {
            "Grid View",
            "Detail View",
            "Phosphosite View"});
            this.viewModeComboBox.Name = "viewModeComboBox";
            this.viewModeComboBox.Size = new System.Drawing.Size(120, 26);
            this.viewModeComboBox.SelectedIndexChanged += new System.EventHandler(this.switchViewButton_Click);
            // 
            // minMatchesLabel
            // 
            this.minMatchesLabel.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.minMatchesLabel.Name = "minMatchesLabel";
            this.minMatchesLabel.Size = new System.Drawing.Size(111, 23);
            this.minMatchesLabel.Text = "Minimum Matches:";
            // 
            // roundToNearestLabel
            // 
            this.roundToNearestLabel.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.roundToNearestLabel.Name = "roundToNearestLabel";
            this.roundToNearestLabel.Size = new System.Drawing.Size(100, 23);
            this.roundToNearestLabel.Text = "Round to nearest:";
            // 
            // minMatchesTextBox
            // 
            this.minMatchesTextBox.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.minMatchesTextBox.Name = "minMatchesTextBox";
            this.minMatchesTextBox.Size = new System.Drawing.Size(47, 26);
            this.minMatchesTextBox.Text = "2";
            this.minMatchesTextBox.Leave += new System.EventHandler(this.ModFilter_Leave);
            this.minMatchesTextBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.MinCountFilter_KeyPress);
            // 
            // dataGridView
            // 
            this.dataGridView.AllowUserToAddRows = false;
            this.dataGridView.AllowUserToDeleteRows = false;
            this.dataGridView.AllowUserToResizeColumns = false;
            this.dataGridView.AllowUserToResizeRows = false;
            this.dataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
            this.dataGridView.BackgroundColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridView.DefaultCellStyle = dataGridViewCellStyle2;
            this.dataGridView.GridColor = System.Drawing.SystemColors.Control;
            this.dataGridView.Location = new System.Drawing.Point(2, 28);
            this.dataGridView.Name = "dataGridView";
            this.dataGridView.ReadOnly = true;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dataGridView.RowHeadersWidth = 80;
            this.dataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.dataGridView.Size = new System.Drawing.Size(1035, 235);
            this.dataGridView.TabIndex = 8;
            // 
            // detailDataGridView
            // 
            this.detailDataGridView.AllowUserToAddRows = false;
            this.detailDataGridView.AllowUserToDeleteRows = false;
            this.detailDataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.detailDataGridView.BackgroundColor = System.Drawing.SystemColors.Window;
            this.detailDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.detailDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.accessionColumn,
            this.offsetColumn,
            this.siteColumn,
            this.massColumn,
            this.probabilityColumn,
            this.peptidesColumn,
            this.matchesColumn,
            this.spectraColumn,
            this.unimodColumn});
            this.detailDataGridView.Location = new System.Drawing.Point(2, 26);
            this.detailDataGridView.Name = "detailDataGridView";
            this.detailDataGridView.ReadOnly = true;
            this.detailDataGridView.RowHeadersVisible = false;
            this.detailDataGridView.Size = new System.Drawing.Size(1035, 236);
            this.detailDataGridView.TabIndex = 6;
            this.detailDataGridView.SortCompare += new System.Windows.Forms.DataGridViewSortCompareEventHandler(this.detailDataGridView_SortCompare);
            // 
            // accessionColumn
            // 
            this.accessionColumn.HeaderText = "Accession";
            this.accessionColumn.Name = "accessionColumn";
            this.accessionColumn.ReadOnly = true;
            this.accessionColumn.Width = 140;
            // 
            // offsetColumn
            // 
            this.offsetColumn.HeaderText = "Position";
            this.offsetColumn.Name = "offsetColumn";
            this.offsetColumn.ReadOnly = true;
            this.offsetColumn.Width = 60;
            // 
            // siteColumn
            // 
            this.siteColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.ColumnHeader;
            this.siteColumn.FillWeight = 50F;
            this.siteColumn.HeaderText = "Site";
            this.siteColumn.MinimumWidth = 20;
            this.siteColumn.Name = "siteColumn";
            this.siteColumn.ReadOnly = true;
            this.siteColumn.Width = 50;
            // 
            // massColumn
            // 
            this.massColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.massColumn.FillWeight = 50F;
            this.massColumn.HeaderText = "ΔMass";
            this.massColumn.MinimumWidth = 20;
            this.massColumn.Name = "massColumn";
            this.massColumn.ReadOnly = true;
            this.massColumn.Width = 70;
            // 
            // probabilityColumn
            // 
            this.probabilityColumn.HeaderText = "Probability";
            this.probabilityColumn.Name = "probabilityColumn";
            this.probabilityColumn.ReadOnly = true;
            this.probabilityColumn.Width = 60;
            // 
            // peptidesColumn
            // 
            this.peptidesColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.peptidesColumn.HeaderText = "Distinct Peptides";
            this.peptidesColumn.MinimumWidth = 20;
            this.peptidesColumn.Name = "peptidesColumn";
            this.peptidesColumn.ReadOnly = true;
            this.peptidesColumn.Width = 110;
            // 
            // matchesColumn
            // 
            this.matchesColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.matchesColumn.HeaderText = "Distinct Matches";
            this.matchesColumn.MinimumWidth = 20;
            this.matchesColumn.Name = "matchesColumn";
            this.matchesColumn.ReadOnly = true;
            this.matchesColumn.Width = 110;
            // 
            // spectraColumn
            // 
            this.spectraColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.spectraColumn.HeaderText = "Filtered Spectra";
            this.spectraColumn.MinimumWidth = 20;
            this.spectraColumn.Name = "spectraColumn";
            this.spectraColumn.ReadOnly = true;
            this.spectraColumn.Width = 110;
            // 
            // unimodColumn
            // 
            this.unimodColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.unimodColumn.FillWeight = 200F;
            this.unimodColumn.HeaderText = "Description";
            this.unimodColumn.MinimumWidth = 20;
            this.unimodColumn.Name = "unimodColumn";
            this.unimodColumn.ReadOnly = true;
            this.unimodColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // toolStrip
            // 
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportButton,
            this.unimodButton,
            this.viewModeComboBox,
            this.pivotModeComboBox,
            this.roundToNearestUpDown,
            this.roundToNearestLabel,
            this.minMatchesTextBox,
            this.minMatchesLabel,
            this.minRowTextBox,
            this.minRowLabel,
            this.minColumnTextBox,
            this.minColumnLabel});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip.Size = new System.Drawing.Size(1037, 26);
            this.toolStrip.TabIndex = 15;
            this.toolStrip.Text = "Tools";
            // 
            // invalidFilterLabel
            // 
            this.invalidFilterLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.invalidFilterLabel.AutoSize = true;
            this.invalidFilterLabel.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.invalidFilterLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.invalidFilterLabel.Location = new System.Drawing.Point(197, 125);
            this.invalidFilterLabel.Name = "invalidFilterLabel";
            this.invalidFilterLabel.Size = new System.Drawing.Size(665, 24);
            this.invalidFilterLabel.TabIndex = 16;
            this.invalidFilterLabel.TabStop = true;
            this.invalidFilterLabel.Text = "Select a single protein, protein group, gene, or gene group to enable the phospho" +
    "site view.";
            this.invalidFilterLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.invalidFilterLabel.UseCompatibleTextRendering = true;
            // 
            // ModificationTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1037, 262);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.detailDataGridView);
            this.Controls.Add(this.dataGridView);
            this.Controls.Add(this.invalidFilterLabel);
            this.DockAreas = ((DigitalRune.Windows.Docking.DockAreas)(((((DigitalRune.Windows.Docking.DockAreas.Left | DigitalRune.Windows.Docking.DockAreas.Right) 
            | DigitalRune.Windows.Docking.DockAreas.Top) 
            | DigitalRune.Windows.Docking.DockAreas.Bottom) 
            | DigitalRune.Windows.Docking.DockAreas.Document)));
            this.Name = "ModificationTableForm";
            this.TabText = "ModificationTableForm";
            this.Text = "ModificationTableForm";
            this.exportMenu.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.detailDataGridView)).EndInit();
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private IDPicker.Controls.AutomationDataGridView dataGridView;
        private System.Windows.Forms.ToolStripButton exportButton;
        private System.Windows.Forms.ContextMenuStrip exportMenu;
        private System.Windows.Forms.ToolStripMenuItem copyToClipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportToFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem copySelectedCellsToClipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedCellsToFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showSelectedCellsInExcelToolStripMenuItem;
        private System.Windows.Forms.ToolStripLabel minRowLabel;
        private System.Windows.Forms.ToolStripTextBox minRowTextBox;
        private System.Windows.Forms.ToolStripTextBox minColumnTextBox;
        private System.Windows.Forms.ToolStripLabel minColumnLabel;
        private System.Windows.Forms.ToolStripButton unimodButton;
        private System.Windows.Forms.ToolStripComboBox pivotModeComboBox;
        private System.Windows.Forms.ToolStripComboBox viewModeComboBox;
        private IDPicker.Controls.AutomationDataGridView detailDataGridView;
        private System.Windows.Forms.ToolStripTextBox minMatchesTextBox;
        private System.Windows.Forms.ToolStripLabel minMatchesLabel;
        private System.Windows.Forms.ToolStripLabel roundToNearestLabel;
        private IDPicker.Controls.ToolStripNumericUpDown roundToNearestUpDown;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.DataGridViewTextBoxColumn accessionColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn offsetColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn siteColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn massColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn probabilityColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn peptidesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn matchesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn spectraColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn unimodColumn;
        private System.Windows.Forms.Label invalidFilterLabel;

    }
}