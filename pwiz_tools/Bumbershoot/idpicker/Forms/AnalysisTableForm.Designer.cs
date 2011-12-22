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
// Contributor(s):
//

using System.Windows.Forms;
using IDPicker.Controls;

namespace IDPicker.Forms
{
    partial class AnalysisTableForm
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.treeDataGridView = new IDPicker.Controls.TreeDataGridView();
            this.exportMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.clipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.copyToClipboardSelectedToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportSelectedCellsToFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelSelectToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip = new System.Windows.Forms.ToolStrip();
            this.exportButton = new System.Windows.Forms.ToolStripButton();
            this.displayOptionsButton = new System.Windows.Forms.ToolStripButton();
            this.nameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.softwareColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.parameterValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize) (this.treeDataGridView)).BeginInit();
            this.exportMenu.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles) ((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.treeDataGridView);
            this.panel1.Location = new System.Drawing.Point(0, 25);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(634, 441);
            this.panel1.TabIndex = 5;
            // 
            // treeDataGridView
            // 
            this.treeDataGridView.AllowUserToOrderColumns = true;
            this.treeDataGridView.AllowUserToResizeRows = false;
            this.treeDataGridView.BackgroundColor = System.Drawing.SystemColors.Window;
            this.treeDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.nameColumn,
            this.softwareColumn,
            this.parameterValue});
            this.treeDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeDataGridView.GridColor = System.Drawing.SystemColors.Window;
            this.treeDataGridView.Location = new System.Drawing.Point(0, 0);
            this.treeDataGridView.Name = "treeDataGridView";
            this.treeDataGridView.ReadOnly = true;
            this.treeDataGridView.RowHeadersVisible = false;
            this.treeDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.treeDataGridView.Size = new System.Drawing.Size(634, 441);
            this.treeDataGridView.TabIndex = 1;
            // 
            // exportMenu
            // 
            this.exportMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.clipboardToolStripMenuItem,
            this.fileToolStripMenuItem,
            this.showInExcelToolStripMenuItem,
            this.toolStripSeparator1,
            this.copyToClipboardSelectedToolStripMenuItem,
            this.exportSelectedCellsToFileToolStripMenuItem,
            this.showInExcelSelectToolStripMenuItem});
            this.exportMenu.Name = "contextMenuStrip1";
            this.exportMenu.Size = new System.Drawing.Size(247, 142);
            // 
            // clipboardToolStripMenuItem
            // 
            this.clipboardToolStripMenuItem.Name = "clipboardToolStripMenuItem";
            this.clipboardToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.clipboardToolStripMenuItem.Text = "Copy to Clipboard";
            this.clipboardToolStripMenuItem.Click += new System.EventHandler(this.clipboardToolStripMenuItem_Click);
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.fileToolStripMenuItem.Text = "Export to File";
            this.fileToolStripMenuItem.Click += new System.EventHandler(this.fileToolStripMenuItem_Click);
            // 
            // showInExcelToolStripMenuItem
            // 
            this.showInExcelToolStripMenuItem.Name = "showInExcelToolStripMenuItem";
            this.showInExcelToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.showInExcelToolStripMenuItem.Text = "Show in Excel";
            this.showInExcelToolStripMenuItem.Click += new System.EventHandler(this.showInExcelToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(243, 6);
            // 
            // copyToClipboardSelectedToolStripMenuItem
            // 
            this.copyToClipboardSelectedToolStripMenuItem.Name = "copyToClipboardSelectedToolStripMenuItem";
            this.copyToClipboardSelectedToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.copyToClipboardSelectedToolStripMenuItem.Text = "Copy Selected Cells to Clipboard";
            this.copyToClipboardSelectedToolStripMenuItem.Click += new System.EventHandler(this.clipboardToolStripMenuItem_Click);
            // 
            // exportSelectedCellsToFileToolStripMenuItem
            // 
            this.exportSelectedCellsToFileToolStripMenuItem.Name = "exportSelectedCellsToFileToolStripMenuItem";
            this.exportSelectedCellsToFileToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.exportSelectedCellsToFileToolStripMenuItem.Text = "Export Selected Cells to File";
            this.exportSelectedCellsToFileToolStripMenuItem.Click += new System.EventHandler(this.fileToolStripMenuItem_Click);
            // 
            // showInExcelSelectToolStripMenuItem
            // 
            this.showInExcelSelectToolStripMenuItem.Name = "showInExcelSelectToolStripMenuItem";
            this.showInExcelSelectToolStripMenuItem.Size = new System.Drawing.Size(246, 22);
            this.showInExcelSelectToolStripMenuItem.Text = "Show Selected Cells in Excel";
            this.showInExcelSelectToolStripMenuItem.Click += new System.EventHandler(this.showInExcelToolStripMenuItem_Click);
            // 
            // toolStrip
            // 
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportButton,
            this.displayOptionsButton});
            this.toolStrip.Location = new System.Drawing.Point(0, 0);
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
            this.toolStrip.Size = new System.Drawing.Size(634, 25);
            this.toolStrip.TabIndex = 15;
            this.toolStrip.Text = "Tools";
            // 
            // exportButton
            // 
            this.exportButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.exportButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.exportButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(23, 22);
            this.exportButton.Text = "Export Options";
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // displayOptionsButton
            // 
            this.displayOptionsButton.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.displayOptionsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            this.displayOptionsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.displayOptionsButton.Name = "displayOptionsButton";
            this.displayOptionsButton.Size = new System.Drawing.Size(94, 22);
            this.displayOptionsButton.Text = "Display Options";
            this.displayOptionsButton.Click += new System.EventHandler(this.displayOptionsButton_Click);
            // 
            // nameColumn
            // 
            this.nameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.nameColumn.HeaderText = "Name";
            this.nameColumn.Name = "nameColumn";
            this.nameColumn.ReadOnly = true;
            // 
            // softwareColumn
            // 
            this.softwareColumn.HeaderText = "Software";
            this.softwareColumn.Name = "softwareColumn";
            this.softwareColumn.ReadOnly = true;
            this.softwareColumn.Width = 120;
            // 
            // parameterValue
            // 
            this.parameterValue.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.parameterValue.HeaderText = "Value";
            this.parameterValue.Name = "parameterValue";
            this.parameterValue.ReadOnly = true;
            // 
            // AnalysisTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(634, 466);
            this.Controls.Add(this.toolStrip);
            this.Controls.Add(this.panel1);
            this.DockAreas = ((DigitalRune.Windows.Docking.DockAreas) (((((DigitalRune.Windows.Docking.DockAreas.Left | DigitalRune.Windows.Docking.DockAreas.Right)
                        | DigitalRune.Windows.Docking.DockAreas.Top)
                        | DigitalRune.Windows.Docking.DockAreas.Bottom)
                        | DigitalRune.Windows.Docking.DockAreas.Document)));
            this.Name = "AnalysisTableForm";
            this.TabText = "AnalysisTableForm";
            this.Text = "AnalysisTableForm";
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize) (this.treeDataGridView)).EndInit();
            this.exportMenu.ResumeLayout(false);
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ContextMenuStrip exportMenu;
        private IDPicker.Controls.TreeDataGridView treeDataGridView;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.ToolStrip toolStrip;
        private System.Windows.Forms.ToolStripButton displayOptionsButton;
        private System.Windows.Forms.ToolStripButton exportButton;
        private System.Windows.Forms.ToolStripMenuItem clipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem copyToClipboardSelectedToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportSelectedCellsToFileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelSelectToolStripMenuItem;
        private DataGridViewTextBoxColumn nameColumn;
        private DataGridViewTextBoxColumn softwareColumn;
        private DataGridViewTextBoxColumn parameterValue;
    }
}