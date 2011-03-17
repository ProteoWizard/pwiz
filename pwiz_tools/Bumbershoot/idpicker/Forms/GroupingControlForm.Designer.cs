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
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

namespace IDPicker.Forms
{
    partial class GroupingControlForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
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
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GroupingControlForm));
            this.gbGroups = new System.Windows.Forms.GroupBox();
            this.pnlBgGroups = new System.Windows.Forms.Panel();
            this.pnlGroupsSpacer = new System.Windows.Forms.Panel();
            this.msGroups = new System.Windows.Forms.MenuStrip();
            this.miResetFiles = new System.Windows.Forms.ToolStripMenuItem();
            this.miExpandGroups = new System.Windows.Forms.ToolStripMenuItem();
            this.miCollapseGroups = new System.Windows.Forms.ToolStripMenuItem();
            this.tlvGroupedFiles = new BrightIdeasSoftware.TreeListView();
            this.tlvGroups = new BrightIdeasSoftware.OLVColumn();
            this.gbFiles = new System.Windows.Forms.GroupBox();
            this.pnlUngroupedFiles = new System.Windows.Forms.Panel();
            this.pnlUngroupedSpacer = new System.Windows.Forms.Panel();
            this.tbStartHere = new System.Windows.Forms.TextBox();
            this.msUngroupedFiles = new System.Windows.Forms.MenuStrip();
            this.miDefaultGroups = new System.Windows.Forms.ToolStripMenuItem();
            this.lvNonGroupedFiles = new System.Windows.Forms.ListView();
            this.saveButton = new System.Windows.Forms.Button();
            this.cancelButton = new System.Windows.Forms.Button();
            this.cmRightClickGroupNode = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addGroupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeGroupToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.renameGroupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cmRightClickFileNode = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.removeFileNodeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.gbGroups.SuspendLayout();
            this.pnlBgGroups.SuspendLayout();
            this.msGroups.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tlvGroupedFiles)).BeginInit();
            this.gbFiles.SuspendLayout();
            this.pnlUngroupedFiles.SuspendLayout();
            this.msUngroupedFiles.SuspendLayout();
            this.cmRightClickGroupNode.SuspendLayout();
            this.cmRightClickFileNode.SuspendLayout();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // gbGroups
            // 
            this.gbGroups.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.gbGroups.BackColor = System.Drawing.Color.Transparent;
            this.gbGroups.Controls.Add(this.pnlBgGroups);
            this.gbGroups.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gbGroups.Location = new System.Drawing.Point(12, 10);
            this.gbGroups.Name = "gbGroups";
            this.gbGroups.Size = new System.Drawing.Size(340, 465);
            this.gbGroups.TabIndex = 127;
            this.gbGroups.TabStop = false;
            this.gbGroups.Text = "tlvBranch Hierarchy";
            // 
            // pnlBgGroups
            // 
            this.pnlBgGroups.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlBgGroups.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.pnlBgGroups.Controls.Add(this.pnlGroupsSpacer);
            this.pnlBgGroups.Controls.Add(this.msGroups);
            this.pnlBgGroups.Controls.Add(this.tlvGroupedFiles);
            this.pnlBgGroups.Location = new System.Drawing.Point(13, 22);
            this.pnlBgGroups.Name = "pnlBgGroups";
            this.pnlBgGroups.Size = new System.Drawing.Size(314, 431);
            this.pnlBgGroups.TabIndex = 9;
            // 
            // pnlGroupsSpacer
            // 
            this.pnlGroupsSpacer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlGroupsSpacer.BackColor = System.Drawing.SystemColors.Window;
            this.pnlGroupsSpacer.Location = new System.Drawing.Point(1, 26);
            this.pnlGroupsSpacer.Name = "pnlGroupsSpacer";
            this.pnlGroupsSpacer.Size = new System.Drawing.Size(312, 5);
            this.pnlGroupsSpacer.TabIndex = 2;
            // 
            // msGroups
            // 
            this.msGroups.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.msGroups.AutoSize = false;
            this.msGroups.Dock = System.Windows.Forms.DockStyle.None;
            this.msGroups.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miResetFiles,
            this.miExpandGroups,
            this.miCollapseGroups});
            this.msGroups.Location = new System.Drawing.Point(1, 1);
            this.msGroups.Name = "msGroups";
            this.msGroups.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.msGroups.ShowItemToolTips = true;
            this.msGroups.Size = new System.Drawing.Size(312, 24);
            this.msGroups.TabIndex = 1;
            this.msGroups.Text = "menuStrip1";
            // 
            // miResetFiles
            // 
            this.miResetFiles.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.miResetFiles.Image = global::IDPicker.Properties.Resources.fillrighths;
            this.miResetFiles.Name = "miResetFiles";
            this.miResetFiles.Size = new System.Drawing.Size(28, 20);
            this.miResetFiles.Text = "Reset files";
            this.miResetFiles.ToolTipText = "Remove all groups";
            this.miResetFiles.Click += new System.EventHandler(this.miResetFiles_Click);
            // 
            // miExpandGroups
            // 
            this.miExpandGroups.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.miExpandGroups.Image = global::IDPicker.Properties.Resources.Expand_large;
            this.miExpandGroups.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.miExpandGroups.Name = "miExpandGroups";
            this.miExpandGroups.Size = new System.Drawing.Size(25, 20);
            this.miExpandGroups.Text = "Expand groups";
            this.miExpandGroups.ToolTipText = "Expand groups";
            this.miExpandGroups.Click += new System.EventHandler(this.miExpandGroups_Click);
            // 
            // miCollapseGroups
            // 
            this.miCollapseGroups.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.miCollapseGroups.Image = global::IDPicker.Properties.Resources.Collapse_large;
            this.miCollapseGroups.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
            this.miCollapseGroups.Name = "miCollapseGroups";
            this.miCollapseGroups.Size = new System.Drawing.Size(25, 20);
            this.miCollapseGroups.Text = "miCollapseGroups";
            this.miCollapseGroups.TextDirection = System.Windows.Forms.ToolStripTextDirection.Horizontal;
            this.miCollapseGroups.ToolTipText = "Collapse groups";
            this.miCollapseGroups.Click += new System.EventHandler(this.miCollapseGroups_Click);
            // 
            // tlvGroupedFiles
            // 
            this.tlvGroupedFiles.AllColumns.Add(this.tlvGroups);
            this.tlvGroupedFiles.AlternateRowBackColor = System.Drawing.Color.White;
            this.tlvGroupedFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tlvGroupedFiles.CellEditActivation = BrightIdeasSoftware.ObjectListView.CellEditActivateMode.F2Only;
            this.tlvGroupedFiles.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.tlvGroups});
            this.tlvGroupedFiles.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.tlvGroupedFiles.IsSimpleDragSource = true;
            this.tlvGroupedFiles.IsSimpleDropSink = true;
            this.tlvGroupedFiles.Location = new System.Drawing.Point(0, 29);
            this.tlvGroupedFiles.Name = "tlvGroupedFiles";
            this.tlvGroupedFiles.OwnerDraw = true;
            this.tlvGroupedFiles.ShowGroups = false;
            this.tlvGroupedFiles.Size = new System.Drawing.Size(314, 402);
            this.tlvGroupedFiles.TabIndex = 11;
            this.tlvGroupedFiles.UseCompatibleStateImageBehavior = false;
            this.tlvGroupedFiles.UseHotItem = true;
            this.tlvGroupedFiles.UseTranslucentHotItem = true;
            this.tlvGroupedFiles.View = System.Windows.Forms.View.Details;
            this.tlvGroupedFiles.VirtualMode = true;
            this.tlvGroupedFiles.CellEditStarting += new BrightIdeasSoftware.CellEditEventHandler(this.tlvGroupedFiles_CellEditStarting);
            this.tlvGroupedFiles.MouseDown += new System.Windows.Forms.MouseEventHandler(this.tvGroups_MouseDown);
            this.tlvGroupedFiles.KeyDown += new System.Windows.Forms.KeyEventHandler(this.tvGroups_KeyDown);
            this.tlvGroupedFiles.CellEditFinishing += new BrightIdeasSoftware.CellEditEventHandler(this.tlvGroupedFiles_CellEditFinishing);
            this.tlvGroupedFiles.CanDrop += new System.EventHandler<BrightIdeasSoftware.OlvDropEventArgs>(this.tlvGroupedFiles_CanDrop);
            this.tlvGroupedFiles.Dropped += new System.EventHandler<BrightIdeasSoftware.OlvDropEventArgs>(this.tlvGroupedFiles_Dropped);
            // 
            // tlvGroups
            // 
            this.tlvGroups.AspectName = "";
            this.tlvGroups.FillsFreeSpace = true;
            this.tlvGroups.Text = "Groups / Sources";
            this.tlvGroups.Width = 300;
            // 
            // gbFiles
            // 
            this.gbFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.gbFiles.Controls.Add(this.pnlUngroupedFiles);
            this.gbFiles.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gbFiles.Location = new System.Drawing.Point(3, 10);
            this.gbFiles.Name = "gbFiles";
            this.gbFiles.Size = new System.Drawing.Size(339, 465);
            this.gbFiles.TabIndex = 128;
            this.gbFiles.TabStop = false;
            this.gbFiles.Text = "Non-Grouped Files";
            // 
            // pnlUngroupedFiles
            // 
            this.pnlUngroupedFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlUngroupedFiles.BackColor = System.Drawing.SystemColors.InactiveCaption;
            this.pnlUngroupedFiles.Controls.Add(this.pnlUngroupedSpacer);
            this.pnlUngroupedFiles.Controls.Add(this.tbStartHere);
            this.pnlUngroupedFiles.Controls.Add(this.msUngroupedFiles);
            this.pnlUngroupedFiles.Controls.Add(this.lvNonGroupedFiles);
            this.pnlUngroupedFiles.Location = new System.Drawing.Point(12, 22);
            this.pnlUngroupedFiles.Name = "pnlUngroupedFiles";
            this.pnlUngroupedFiles.Size = new System.Drawing.Size(314, 431);
            this.pnlUngroupedFiles.TabIndex = 10;
            // 
            // pnlUngroupedSpacer
            // 
            this.pnlUngroupedSpacer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.pnlUngroupedSpacer.BackColor = System.Drawing.SystemColors.Window;
            this.pnlUngroupedSpacer.Location = new System.Drawing.Point(1, 26);
            this.pnlUngroupedSpacer.Name = "pnlUngroupedSpacer";
            this.pnlUngroupedSpacer.Size = new System.Drawing.Size(312, 5);
            this.pnlUngroupedSpacer.TabIndex = 2;
            // 
            // tbStartHere
            // 
            this.tbStartHere.AllowDrop = true;
            this.tbStartHere.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tbStartHere.BackColor = System.Drawing.SystemColors.Window;
            this.tbStartHere.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tbStartHere.Enabled = false;
            this.tbStartHere.Font = new System.Drawing.Font("Tahoma", 8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tbStartHere.ForeColor = System.Drawing.Color.Gray;
            this.tbStartHere.Location = new System.Drawing.Point(16, 153);
            this.tbStartHere.Multiline = true;
            this.tbStartHere.Name = "tbStartHere";
            this.tbStartHere.ReadOnly = true;
            this.tbStartHere.Size = new System.Drawing.Size(287, 101);
            this.tbStartHere.TabIndex = 3;
            this.tbStartHere.Text = resources.GetString("tbStartHere.Text");
            this.tbStartHere.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.tbStartHere.Visible = false;
            // 
            // msUngroupedFiles
            // 
            this.msUngroupedFiles.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.msUngroupedFiles.AutoSize = false;
            this.msUngroupedFiles.Dock = System.Windows.Forms.DockStyle.None;
            this.msUngroupedFiles.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miDefaultGroups});
            this.msUngroupedFiles.Location = new System.Drawing.Point(1, 1);
            this.msUngroupedFiles.Name = "msUngroupedFiles";
            this.msUngroupedFiles.ShowItemToolTips = true;
            this.msUngroupedFiles.Size = new System.Drawing.Size(312, 24);
            this.msUngroupedFiles.TabIndex = 1;
            this.msUngroupedFiles.Text = "menuStrip1";
            // 
            // miDefaultGroups
            // 
            this.miDefaultGroups.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.miDefaultGroups.Image = global::IDPicker.Properties.Resources.filllefths;
            this.miDefaultGroups.Name = "miDefaultGroups";
            this.miDefaultGroups.Size = new System.Drawing.Size(28, 20);
            this.miDefaultGroups.Text = "Reset files";
            this.miDefaultGroups.ToolTipText = "Apply default groups";
            this.miDefaultGroups.Click += new System.EventHandler(this.ApplyDefaultGroups);
            // 
            // lvNonGroupedFiles
            // 
            this.lvNonGroupedFiles.AllowDrop = true;
            this.lvNonGroupedFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lvNonGroupedFiles.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.lvNonGroupedFiles.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lvNonGroupedFiles.LabelWrap = false;
            this.lvNonGroupedFiles.Location = new System.Drawing.Point(1, 31);
            this.lvNonGroupedFiles.Name = "lvNonGroupedFiles";
            this.lvNonGroupedFiles.ShowGroups = false;
            this.lvNonGroupedFiles.Size = new System.Drawing.Size(312, 399);
            this.lvNonGroupedFiles.Sorting = System.Windows.Forms.SortOrder.Ascending;
            this.lvNonGroupedFiles.TabIndex = 3;
            this.lvNonGroupedFiles.UseCompatibleStateImageBehavior = false;
            this.lvNonGroupedFiles.View = System.Windows.Forms.View.List;
            this.lvNonGroupedFiles.DragDrop += new System.Windows.Forms.DragEventHandler(this.lvNonGroupedFiles_DragDrop);
            this.lvNonGroupedFiles.DragEnter += new System.Windows.Forms.DragEventHandler(this.lvNonGroupedFiles_DragEnter);
            this.lvNonGroupedFiles.KeyDown += new System.Windows.Forms.KeyEventHandler(this.lvNonGroupedFiles_KeyDown);
            this.lvNonGroupedFiles.ItemDrag += new System.Windows.Forms.ItemDragEventHandler(this.lvNonGroupedFiles_ItemDrag);
            this.lvNonGroupedFiles.DragOver += new System.Windows.Forms.DragEventHandler(this.lvNonGroupedFiles_DragOver);
            // 
            // saveButton
            // 
            this.saveButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.saveButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.saveButton.Location = new System.Drawing.Point(619, 483);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(75, 23);
            this.saveButton.TabIndex = 1;
            this.saveButton.Text = "Save";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(538, 483);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(75, 23);
            this.cancelButton.TabIndex = 130;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // cmRightClickGroupNode
            // 
            this.cmRightClickGroupNode.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmRightClickGroupNode.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addGroupToolStripMenuItem,
            this.removeGroupToolStripMenuItem1,
            this.renameGroupToolStripMenuItem});
            this.cmRightClickGroupNode.Name = "cmRightClickGroupNode";
            this.cmRightClickGroupNode.Size = new System.Drawing.Size(125, 70);
            // 
            // addGroupToolStripMenuItem
            // 
            this.addGroupToolStripMenuItem.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.addGroupToolStripMenuItem.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.addGroupToolStripMenuItem.Name = "addGroupToolStripMenuItem";
            this.addGroupToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.addGroupToolStripMenuItem.Text = "Add";
            this.addGroupToolStripMenuItem.Click += new System.EventHandler(this.addGroupToolStripMenuItem_Click);
            // 
            // removeGroupToolStripMenuItem1
            // 
            this.removeGroupToolStripMenuItem1.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.removeGroupToolStripMenuItem1.Name = "removeGroupToolStripMenuItem1";
            this.removeGroupToolStripMenuItem1.Size = new System.Drawing.Size(124, 22);
            this.removeGroupToolStripMenuItem1.Text = "Remove";
            this.removeGroupToolStripMenuItem1.Click += new System.EventHandler(this.removeGroupToolStripMenuItem_Click);
            // 
            // renameGroupToolStripMenuItem
            // 
            this.renameGroupToolStripMenuItem.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.renameGroupToolStripMenuItem.Name = "renameGroupToolStripMenuItem";
            this.renameGroupToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.renameGroupToolStripMenuItem.Text = "Rename";
            this.renameGroupToolStripMenuItem.Click += new System.EventHandler(this.renameGroupToolStripMenuItem_Click);
            // 
            // cmRightClickFileNode
            // 
            this.cmRightClickFileNode.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmRightClickFileNode.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.removeFileNodeToolStripMenuItem});
            this.cmRightClickFileNode.Name = "cmRightClickFileNode";
            this.cmRightClickFileNode.Size = new System.Drawing.Size(125, 26);
            // 
            // removeFileNodeToolStripMenuItem
            // 
            this.removeFileNodeToolStripMenuItem.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.removeFileNodeToolStripMenuItem.Name = "removeFileNodeToolStripMenuItem";
            this.removeFileNodeToolStripMenuItem.Size = new System.Drawing.Size(124, 22);
            this.removeFileNodeToolStripMenuItem.Text = "Remove";
            this.removeFileNodeToolStripMenuItem.Click += new System.EventHandler(this.removeFileNodeToolStripMenuItem_Click);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.IsSplitterFixed = true;
            this.splitContainer1.Location = new System.Drawing.Point(0, 2);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.gbGroups);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.gbFiles);
            this.splitContainer1.Size = new System.Drawing.Size(706, 485);
            this.splitContainer1.SplitterDistance = 356;
            this.splitContainer1.SplitterWidth = 1;
            this.splitContainer1.TabIndex = 131;
            // 
            // GroupingControlForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(706, 518);
            this.ControlBox = false;
            this.Controls.Add(this.saveButton);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.splitContainer1);
            this.Name = "GroupingControlForm";
            this.Text = "Source Grouping";
            this.gbGroups.ResumeLayout(false);
            this.pnlBgGroups.ResumeLayout(false);
            this.msGroups.ResumeLayout(false);
            this.msGroups.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tlvGroupedFiles)).EndInit();
            this.gbFiles.ResumeLayout(false);
            this.pnlUngroupedFiles.ResumeLayout(false);
            this.pnlUngroupedFiles.PerformLayout();
            this.msUngroupedFiles.ResumeLayout(false);
            this.msUngroupedFiles.PerformLayout();
            this.cmRightClickGroupNode.ResumeLayout(false);
            this.cmRightClickFileNode.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox gbGroups;
        private System.Windows.Forms.Panel pnlBgGroups;
        private System.Windows.Forms.Panel pnlGroupsSpacer;
        private System.Windows.Forms.MenuStrip msGroups;
        private System.Windows.Forms.ToolStripMenuItem miResetFiles;
        private System.Windows.Forms.ToolStripMenuItem miExpandGroups;
        private System.Windows.Forms.ToolStripMenuItem miCollapseGroups;
        private System.Windows.Forms.GroupBox gbFiles;
        private System.Windows.Forms.Panel pnlUngroupedFiles;
        private System.Windows.Forms.Panel pnlUngroupedSpacer;
        private System.Windows.Forms.TextBox tbStartHere;
        private System.Windows.Forms.MenuStrip msUngroupedFiles;
        private System.Windows.Forms.ToolStripMenuItem miDefaultGroups;
        private System.Windows.Forms.ListView lvNonGroupedFiles;
        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.ContextMenuStrip cmRightClickGroupNode;
        private System.Windows.Forms.ToolStripMenuItem addGroupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeGroupToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem renameGroupToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip cmRightClickFileNode;
        private System.Windows.Forms.ToolStripMenuItem removeFileNodeToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private BrightIdeasSoftware.TreeListView tlvGroupedFiles;
        private BrightIdeasSoftware.OLVColumn tlvGroups;

    }
}