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
            this.exportButton = new System.Windows.Forms.Button();
            this.exportMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyToClipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.copySelectedCellsToClipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportSelectedCellsToFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSelectedCellsInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.MinRowLabel = new System.Windows.Forms.Label();
            this.MinRowBox = new System.Windows.Forms.TextBox();
            this.MinColumnBox = new System.Windows.Forms.TextBox();
            this.MinColumnLabel = new System.Windows.Forms.Label();
            this.unimodButton = new System.Windows.Forms.Button();
            this.roundToNearestUpDown = new System.Windows.Forms.NumericUpDown();
            this.label1 = new System.Windows.Forms.Label();
            this.pivotModeComboBox = new System.Windows.Forms.ComboBox();
            this.switchtoTableButton = new System.Windows.Forms.Button();
            this.GridPanel = new System.Windows.Forms.Panel();
            this.dataGridView = new IDPicker.Controls.PreviewDataGridView();
            this.TablePanel = new System.Windows.Forms.Panel();
            this.label3 = new System.Windows.Forms.Label();
            this.roundToNearestTableUpDown = new System.Windows.Forms.NumericUpDown();
            this.tablePeptidesFilterBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.detailDataGridView = new System.Windows.Forms.DataGridView();
            this.switchToGridButton = new System.Windows.Forms.Button();
            this.exportTableButton = new System.Windows.Forms.Button();
            this.unimodTableButton = new System.Windows.Forms.Button();
            this.exportDetailMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyToClipboardDetailToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToFileDetailToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelDetailToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.copySelectedCellsToClipboardDetailToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportSelectedCellsToFileDetailToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showSelectedCellsInExcelDetailToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.siteColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.massColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.spectraColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.matchesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.peptidesColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.unimodColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.exportMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.roundToNearestUpDown)).BeginInit();
            this.GridPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).BeginInit();
            this.TablePanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.roundToNearestTableUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.detailDataGridView)).BeginInit();
            this.exportDetailMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportButton.Location = new System.Drawing.Point(718, 3);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(30, 23);
            this.exportButton.TabIndex = 7;
            this.exportButton.UseVisualStyleBackColor = true;
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
            // MinRowLabel
            // 
            this.MinRowLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.MinRowLabel.AutoSize = true;
            this.MinRowLabel.Location = new System.Drawing.Point(312, 8);
            this.MinRowLabel.Name = "MinRowLabel";
            this.MinRowLabel.Size = new System.Drawing.Size(75, 13);
            this.MinRowLabel.TabIndex = 5;
            this.MinRowLabel.Text = "Row minimum:";
            // 
            // MinRowBox
            // 
            this.MinRowBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.MinRowBox.Location = new System.Drawing.Point(394, 5);
            this.MinRowBox.Name = "MinRowBox";
            this.MinRowBox.Size = new System.Drawing.Size(47, 20);
            this.MinRowBox.TabIndex = 3;
            this.MinRowBox.Text = "2";
            this.MinRowBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.MinCountFilter_KeyPress);
            this.MinRowBox.Leave += new System.EventHandler(this.ModFilter_Leave);
            // 
            // MinColumnBox
            // 
            this.MinColumnBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.MinColumnBox.Location = new System.Drawing.Point(259, 5);
            this.MinColumnBox.Name = "MinColumnBox";
            this.MinColumnBox.Size = new System.Drawing.Size(47, 20);
            this.MinColumnBox.TabIndex = 2;
            this.MinColumnBox.Text = "2";
            this.MinColumnBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.MinCountFilter_KeyPress);
            this.MinColumnBox.Leave += new System.EventHandler(this.ModFilter_Leave);
            // 
            // MinColumnLabel
            // 
            this.MinColumnLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.MinColumnLabel.AutoSize = true;
            this.MinColumnLabel.Location = new System.Drawing.Point(164, 8);
            this.MinColumnLabel.Name = "MinColumnLabel";
            this.MinColumnLabel.Size = new System.Drawing.Size(88, 13);
            this.MinColumnLabel.TabIndex = 7;
            this.MinColumnLabel.Text = "Column minimum:";
            // 
            // unimodButton
            // 
            this.unimodButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.unimodButton.Location = new System.Drawing.Point(559, 3);
            this.unimodButton.Name = "unimodButton";
            this.unimodButton.Size = new System.Drawing.Size(77, 23);
            this.unimodButton.TabIndex = 5;
            this.unimodButton.Text = "Unimod Filter";
            this.unimodButton.UseVisualStyleBackColor = true;
            this.unimodButton.Click += new System.EventHandler(this.unimodButton_Click);
            // 
            // roundToNearestUpDown
            // 
            this.roundToNearestUpDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.roundToNearestUpDown.DecimalPlaces = 4;
            this.roundToNearestUpDown.Increment = new decimal(new int[] {
            1,
            0,
            0,
            262144});
            this.roundToNearestUpDown.Location = new System.Drawing.Point(105, 5);
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
            this.roundToNearestUpDown.Size = new System.Drawing.Size(53, 20);
            this.roundToNearestUpDown.TabIndex = 1;
            this.roundToNearestUpDown.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.roundToNearestUpDown.ValueChanged += new System.EventHandler(this.roundToNearestUpDown_ValueChanged);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 8);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(92, 13);
            this.label1.TabIndex = 11;
            this.label1.Text = "Round to nearest:";
            // 
            // pivotModeComboBox
            // 
            this.pivotModeComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pivotModeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.pivotModeComboBox.FormattingEnabled = true;
            this.pivotModeComboBox.Items.AddRange(new object[] {
            "Spectra",
            "Distinct Matches",
            "Distinct Peptides"});
            this.pivotModeComboBox.Location = new System.Drawing.Point(447, 4);
            this.pivotModeComboBox.Name = "pivotModeComboBox";
            this.pivotModeComboBox.Size = new System.Drawing.Size(106, 21);
            this.pivotModeComboBox.TabIndex = 4;
            this.pivotModeComboBox.SelectedIndexChanged += new System.EventHandler(this.pivotModeComboBox_SelectedIndexChanged);
            // 
            // switchtoTableButton
            // 
            this.switchtoTableButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.switchtoTableButton.Location = new System.Drawing.Point(642, 3);
            this.switchtoTableButton.Name = "switchtoTableButton";
            this.switchtoTableButton.Size = new System.Drawing.Size(70, 23);
            this.switchtoTableButton.TabIndex = 6;
            this.switchtoTableButton.Text = "Detail View";
            this.switchtoTableButton.UseVisualStyleBackColor = true;
            this.switchtoTableButton.Click += new System.EventHandler(this.switchViewButton_Click);
            // 
            // GridPanel
            // 
            this.GridPanel.Controls.Add(this.switchtoTableButton);
            this.GridPanel.Controls.Add(this.dataGridView);
            this.GridPanel.Controls.Add(this.pivotModeComboBox);
            this.GridPanel.Controls.Add(this.exportButton);
            this.GridPanel.Controls.Add(this.label1);
            this.GridPanel.Controls.Add(this.MinRowLabel);
            this.GridPanel.Controls.Add(this.roundToNearestUpDown);
            this.GridPanel.Controls.Add(this.MinRowBox);
            this.GridPanel.Controls.Add(this.MinColumnBox);
            this.GridPanel.Controls.Add(this.MinColumnLabel);
            this.GridPanel.Controls.Add(this.unimodButton);
            this.GridPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.GridPanel.Location = new System.Drawing.Point(0, 0);
            this.GridPanel.Name = "GridPanel";
            this.GridPanel.Size = new System.Drawing.Size(751, 262);
            this.GridPanel.TabIndex = 14;
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
            this.dataGridView.Size = new System.Drawing.Size(749, 235);
            this.dataGridView.TabIndex = 8;
            // 
            // TablePanel
            // 
            this.TablePanel.Controls.Add(this.label3);
            this.TablePanel.Controls.Add(this.roundToNearestTableUpDown);
            this.TablePanel.Controls.Add(this.tablePeptidesFilterBox);
            this.TablePanel.Controls.Add(this.label2);
            this.TablePanel.Controls.Add(this.detailDataGridView);
            this.TablePanel.Controls.Add(this.switchToGridButton);
            this.TablePanel.Controls.Add(this.exportTableButton);
            this.TablePanel.Controls.Add(this.unimodTableButton);
            this.TablePanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.TablePanel.Location = new System.Drawing.Point(0, 0);
            this.TablePanel.Name = "TablePanel";
            this.TablePanel.Size = new System.Drawing.Size(751, 262);
            this.TablePanel.TabIndex = 15;
            this.TablePanel.Visible = false;
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(248, 8);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(92, 13);
            this.label3.TabIndex = 21;
            this.label3.Text = "Round to nearest:";
            // 
            // roundToNearestTableUpDown
            // 
            this.roundToNearestTableUpDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.roundToNearestTableUpDown.DecimalPlaces = 4;
            this.roundToNearestTableUpDown.Increment = new decimal(new int[] {
            1,
            0,
            0,
            262144});
            this.roundToNearestTableUpDown.Location = new System.Drawing.Point(346, 5);
            this.roundToNearestTableUpDown.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.roundToNearestTableUpDown.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            262144});
            this.roundToNearestTableUpDown.Name = "roundToNearestTableUpDown";
            this.roundToNearestTableUpDown.Size = new System.Drawing.Size(53, 20);
            this.roundToNearestTableUpDown.TabIndex = 1;
            this.roundToNearestTableUpDown.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.roundToNearestTableUpDown.ValueChanged += new System.EventHandler(this.roundToNearestUpDown_ValueChanged);
            // 
            // tablePeptidesFilterBox
            // 
            this.tablePeptidesFilterBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.tablePeptidesFilterBox.Location = new System.Drawing.Point(506, 5);
            this.tablePeptidesFilterBox.Name = "tablePeptidesFilterBox";
            this.tablePeptidesFilterBox.Size = new System.Drawing.Size(47, 20);
            this.tablePeptidesFilterBox.TabIndex = 2;
            this.tablePeptidesFilterBox.Text = "2";
            this.tablePeptidesFilterBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.MinCountFilter_KeyPress);
            this.tablePeptidesFilterBox.Leave += new System.EventHandler(this.ModFilter_Leave);
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(405, 8);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(95, 13);
            this.label2.TabIndex = 18;
            this.label2.Text = "Minimum Peptides:";
            // 
            // detailDataGridView
            // 
            this.detailDataGridView.AllowUserToAddRows = false;
            this.detailDataGridView.AllowUserToDeleteRows = false;
            this.detailDataGridView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.detailDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.detailDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.siteColumn,
            this.massColumn,
            this.spectraColumn,
            this.matchesColumn,
            this.peptidesColumn,
            this.unimodColumn});
            this.detailDataGridView.Location = new System.Drawing.Point(2, 28);
            this.detailDataGridView.Name = "detailDataGridView";
            this.detailDataGridView.RowHeadersVisible = false;
            this.detailDataGridView.Size = new System.Drawing.Size(749, 234);
            this.detailDataGridView.TabIndex = 6;
            // 
            // switchToGridButton
            // 
            this.switchToGridButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.switchToGridButton.Location = new System.Drawing.Point(642, 3);
            this.switchToGridButton.Name = "switchToGridButton";
            this.switchToGridButton.Size = new System.Drawing.Size(70, 23);
            this.switchToGridButton.TabIndex = 4;
            this.switchToGridButton.Text = "Grid View";
            this.switchToGridButton.UseVisualStyleBackColor = true;
            this.switchToGridButton.Click += new System.EventHandler(this.switchViewButton_Click);
            // 
            // exportTableButton
            // 
            this.exportTableButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportTableButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportTableButton.Location = new System.Drawing.Point(718, 3);
            this.exportTableButton.Name = "exportTableButton";
            this.exportTableButton.Size = new System.Drawing.Size(30, 23);
            this.exportTableButton.TabIndex = 5;
            this.exportTableButton.UseVisualStyleBackColor = true;
            this.exportTableButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // unimodTableButton
            // 
            this.unimodTableButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.unimodTableButton.Location = new System.Drawing.Point(559, 3);
            this.unimodTableButton.Name = "unimodTableButton";
            this.unimodTableButton.Size = new System.Drawing.Size(77, 23);
            this.unimodTableButton.TabIndex = 3;
            this.unimodTableButton.Text = "Unimod Filter";
            this.unimodTableButton.UseVisualStyleBackColor = true;
            this.unimodTableButton.Click += new System.EventHandler(this.unimodButton_Click);
            // 
            // exportDetailMenu
            // 
            this.exportDetailMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyToClipboardDetailToolStripMenuItem,
            this.exportToFileDetailToolStripMenuItem,
            this.showInExcelDetailToolStripMenuItem,
            this.toolStripSeparator2,
            this.copySelectedCellsToClipboardDetailToolStripMenuItem,
            this.exportSelectedCellsToFileDetailToolStripMenuItem,
            this.showSelectedCellsInExcelDetailToolStripMenuItem});
            this.exportDetailMenu.Name = "contextMenuStrip1";
            this.exportDetailMenu.Size = new System.Drawing.Size(247, 142);
            // 
            // copyToClipboardDetailToolStripMenuItem
            // 
            this.copyToClipboardDetailToolStripMenuItem.Name = "copyToClipboardDetailToolStripMenuItem";
            this.copyToClipboardDetailToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.copyToClipboardDetailToolStripMenuItem.Text = "Copy to Clipboard";
            this.copyToClipboardDetailToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // exportToFileDetailToolStripMenuItem
            // 
            this.exportToFileDetailToolStripMenuItem.Name = "exportToFileDetailToolStripMenuItem";
            this.exportToFileDetailToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.exportToFileDetailToolStripMenuItem.Text = "Export to File";
            this.exportToFileDetailToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // showInExcelDetailToolStripMenuItem
            // 
            this.showInExcelDetailToolStripMenuItem.Name = "showInExcelDetailToolStripMenuItem";
            this.showInExcelDetailToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.showInExcelDetailToolStripMenuItem.Text = "Show in Excel";
            this.showInExcelDetailToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(243, 6);
            // 
            // copySelectedCellsToClipboardDetailToolStripMenuItem
            // 
            this.copySelectedCellsToClipboardDetailToolStripMenuItem.Name = "copySelectedCellsToClipboardDetailToolStripMenuItem";
            this.copySelectedCellsToClipboardDetailToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.copySelectedCellsToClipboardDetailToolStripMenuItem.Text = "Copy Selected Cells to Clipboard";
            this.copySelectedCellsToClipboardDetailToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // exportSelectedCellsToFileDetailToolStripMenuItem
            // 
            this.exportSelectedCellsToFileDetailToolStripMenuItem.Name = "exportSelectedCellsToFileDetailToolStripMenuItem";
            this.exportSelectedCellsToFileDetailToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.exportSelectedCellsToFileDetailToolStripMenuItem.Text = "Export Selected Cells to File";
            this.exportSelectedCellsToFileDetailToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // showSelectedCellsInExcelDetailToolStripMenuItem
            // 
            this.showSelectedCellsInExcelDetailToolStripMenuItem.Name = "showSelectedCellsInExcelDetailToolStripMenuItem";
            this.showSelectedCellsInExcelDetailToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.showSelectedCellsInExcelDetailToolStripMenuItem.Text = "Show Selected Cells in Excel";
            this.showSelectedCellsInExcelDetailToolStripMenuItem.Click += new System.EventHandler(this.ExportTable);
            // 
            // siteColumn
            // 
            this.siteColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.siteColumn.FillWeight = 50F;
            this.siteColumn.HeaderText = "Site";
            this.siteColumn.MinimumWidth = 20;
            this.siteColumn.Name = "siteColumn";
            // 
            // massColumn
            // 
            this.massColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.massColumn.FillWeight = 50F;
            this.massColumn.HeaderText = "Mass";
            this.massColumn.MinimumWidth = 20;
            this.massColumn.Name = "massColumn";
            // 
            // spectraColumn
            // 
            this.spectraColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.spectraColumn.HeaderText = "Filtered Spectra";
            this.spectraColumn.MinimumWidth = 20;
            this.spectraColumn.Name = "spectraColumn";
            // 
            // matchesColumn
            // 
            this.matchesColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.matchesColumn.HeaderText = "Distinct Matches";
            this.matchesColumn.MinimumWidth = 20;
            this.matchesColumn.Name = "matchesColumn";
            // 
            // peptidesColumn
            // 
            this.peptidesColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.peptidesColumn.HeaderText = "Distinct Peptides";
            this.peptidesColumn.MinimumWidth = 20;
            this.peptidesColumn.Name = "peptidesColumn";
            // 
            // unimodColumn
            // 
            this.unimodColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.unimodColumn.FillWeight = 200F;
            this.unimodColumn.HeaderText = "Unimod";
            this.unimodColumn.MinimumWidth = 20;
            this.unimodColumn.Name = "unimodColumn";
            this.unimodColumn.ReadOnly = true;
            this.unimodColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // ModificationTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(751, 262);
            this.Controls.Add(this.TablePanel);
            this.Controls.Add(this.GridPanel);
            this.DockAreas = ((DigitalRune.Windows.Docking.DockAreas)(((((DigitalRune.Windows.Docking.DockAreas.Left | DigitalRune.Windows.Docking.DockAreas.Right) 
            | DigitalRune.Windows.Docking.DockAreas.Top) 
            | DigitalRune.Windows.Docking.DockAreas.Bottom) 
            | DigitalRune.Windows.Docking.DockAreas.Document)));
            this.Name = "ModificationTableForm";
            this.TabText = "ModificationTableForm";
            this.Text = "ModificationTableForm";
            this.exportMenu.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.roundToNearestUpDown)).EndInit();
            this.GridPanel.ResumeLayout(false);
            this.GridPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView)).EndInit();
            this.TablePanel.ResumeLayout(false);
            this.TablePanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.roundToNearestTableUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.detailDataGridView)).EndInit();
            this.exportDetailMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private IDPicker.Controls.PreviewDataGridView dataGridView;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.ContextMenuStrip exportMenu;
        private System.Windows.Forms.ToolStripMenuItem copyToClipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportToFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem copySelectedCellsToClipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedCellsToFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showSelectedCellsInExcelToolStripMenuItem;
        private System.Windows.Forms.Label MinRowLabel;
        private System.Windows.Forms.TextBox MinRowBox;
        private System.Windows.Forms.TextBox MinColumnBox;
        private System.Windows.Forms.Label MinColumnLabel;
        private System.Windows.Forms.Button unimodButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox pivotModeComboBox;
        private System.Windows.Forms.NumericUpDown roundToNearestUpDown;
        private System.Windows.Forms.Button switchtoTableButton;
        private System.Windows.Forms.Panel GridPanel;
        private System.Windows.Forms.Panel TablePanel;
        private System.Windows.Forms.Button switchToGridButton;
        private System.Windows.Forms.Button exportTableButton;
        private System.Windows.Forms.Button unimodTableButton;
        private System.Windows.Forms.DataGridView detailDataGridView;
        private System.Windows.Forms.TextBox tablePeptidesFilterBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.NumericUpDown roundToNearestTableUpDown;
        private System.Windows.Forms.ContextMenuStrip exportDetailMenu;
        private System.Windows.Forms.ToolStripMenuItem copyToClipboardDetailToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportToFileDetailToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelDetailToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem copySelectedCellsToClipboardDetailToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedCellsToFileDetailToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showSelectedCellsInExcelDetailToolStripMenuItem;
        private System.Windows.Forms.DataGridViewTextBoxColumn siteColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn massColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn spectraColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn matchesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn peptidesColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn unimodColumn;

    }
}