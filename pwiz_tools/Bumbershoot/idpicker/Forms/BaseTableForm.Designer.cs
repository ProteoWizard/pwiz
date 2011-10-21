//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s):
//

namespace IDPicker.Forms
{
    partial class BaseTableForm<GroupByType, PivotByType>
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
            this.groupingSetupButton = new System.Windows.Forms.Button();
            this.pivotSetupButton = new System.Windows.Forms.Button();
            this.displayOptionsButton = new System.Windows.Forms.Button();
            this.exportButton = new System.Windows.Forms.Button();
            this.exportMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.clipboardToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showInExcelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.treeDataGridView = new IDPicker.Controls.TreeDataGridView();
            this.panel1.SuspendLayout();
            this.exportMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize) (this.treeDataGridView)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles) ((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.treeDataGridView);
            this.panel1.Location = new System.Drawing.Point(0, 27);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1064, 335);
            this.panel1.TabIndex = 5;
            // 
            // groupingSetupButton
            // 
            this.groupingSetupButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.groupingSetupButton.Location = new System.Drawing.Point(730, 2);
            this.groupingSetupButton.Name = "groupingSetupButton";
            this.groupingSetupButton.Size = new System.Drawing.Size(100, 23);
            this.groupingSetupButton.TabIndex = 14;
            this.groupingSetupButton.Text = "Tree Grouping";
            this.groupingSetupButton.UseVisualStyleBackColor = true;
            this.groupingSetupButton.Click += new System.EventHandler(this.groupingSetupButton_Click);
            // 
            // pivotSetupButton
            // 
            this.pivotSetupButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pivotSetupButton.Location = new System.Drawing.Point(836, 2);
            this.pivotSetupButton.Name = "pivotSetupButton";
            this.pivotSetupButton.Size = new System.Drawing.Size(84, 23);
            this.pivotSetupButton.TabIndex = 13;
            this.pivotSetupButton.Text = "Pivot Options";
            this.pivotSetupButton.UseVisualStyleBackColor = true;
            this.pivotSetupButton.Click += new System.EventHandler(this.pivotSetupButton_Click);
            // 
            // displayOptionsButton
            // 
            this.displayOptionsButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.displayOptionsButton.Location = new System.Drawing.Point(926, 2);
            this.displayOptionsButton.Name = "displayOptionsButton";
            this.displayOptionsButton.Size = new System.Drawing.Size(96, 23);
            this.displayOptionsButton.TabIndex = 12;
            this.displayOptionsButton.Text = "Display Options";
            this.displayOptionsButton.UseVisualStyleBackColor = true;
            this.displayOptionsButton.Click += new System.EventHandler(this.displayOptionsButton_Click);
            // 
            // exportButton
            // 
            this.exportButton.Anchor = ((System.Windows.Forms.AnchorStyles) ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.exportButton.Image = global::IDPicker.Properties.Resources.Export;
            this.exportButton.Location = new System.Drawing.Point(1028, 2);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(30, 23);
            this.exportButton.TabIndex = 11;
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            // 
            // exportMenu
            // 
            this.exportMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.clipboardToolStripMenuItem,
            this.fileToolStripMenuItem,
            this.showInExcelToolStripMenuItem});
            this.exportMenu.Name = "contextMenuStrip1";
            this.exportMenu.Size = new System.Drawing.Size(172, 70);
            // 
            // clipboardToolStripMenuItem
            // 
            this.clipboardToolStripMenuItem.Name = "clipboardToolStripMenuItem";
            this.clipboardToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.clipboardToolStripMenuItem.Text = "Copy to Clipboard";
            this.clipboardToolStripMenuItem.Click += new System.EventHandler(this.clipboardToolStripMenuItem_Click);
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.fileToolStripMenuItem.Text = "Export to File";
            this.fileToolStripMenuItem.Click += new System.EventHandler(this.fileToolStripMenuItem_Click);
            // 
            // showInExcelToolStripMenuItem
            // 
            this.showInExcelToolStripMenuItem.Name = "showInExcelToolStripMenuItem";
            this.showInExcelToolStripMenuItem.Size = new System.Drawing.Size(171, 22);
            this.showInExcelToolStripMenuItem.Text = "Show in Excel";
            this.showInExcelToolStripMenuItem.Click += new System.EventHandler(this.showInExcelToolStripMenuItem_Click);
            // 
            // treeDataGridView
            // 
            this.treeDataGridView.AllowUserToOrderColumns = true;
            this.treeDataGridView.AllowUserToResizeRows = false;
            this.treeDataGridView.BackgroundColor = System.Drawing.SystemColors.Window;
            this.treeDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeDataGridView.GridColor = System.Drawing.SystemColors.Window;
            this.treeDataGridView.Location = new System.Drawing.Point(0, 0);
            this.treeDataGridView.Name = "treeDataGridView";
            this.treeDataGridView.ReadOnly = true;
            this.treeDataGridView.RowHeadersVisible = false;
            this.treeDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.treeDataGridView.Size = new System.Drawing.Size(1064, 335);
            this.treeDataGridView.TabIndex = 1;
            // 
            // BaseTableForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1064, 362);
            this.Controls.Add(this.groupingSetupButton);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.pivotSetupButton);
            this.Controls.Add(this.exportButton);
            this.Controls.Add(this.displayOptionsButton);
            this.Name = "BaseTableForm";
            this.TabText = "BaseTableForm";
            this.Text = "BaseTableForm";
            this.panel1.ResumeLayout(false);
            this.exportMenu.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize) (this.treeDataGridView)).EndInit();
            this.ResumeLayout(false);

        }

        protected IDPicker.Controls.TreeDataGridView treeDataGridView;
        protected System.Windows.Forms.Panel panel1;

        #endregion
        private System.Windows.Forms.Button groupingSetupButton;
        private System.Windows.Forms.Button pivotSetupButton;
        private System.Windows.Forms.Button displayOptionsButton;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.ContextMenuStrip exportMenu;
        private System.Windows.Forms.ToolStripMenuItem clipboardToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showInExcelToolStripMenuItem;
    }
}