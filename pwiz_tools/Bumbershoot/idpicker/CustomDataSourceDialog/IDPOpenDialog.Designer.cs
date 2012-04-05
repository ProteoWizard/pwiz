//
// $Id: TableExporter.cs 287 2011-08-05 16:41:22Z holmanjd $
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
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2011 Vanderbilt University
//
// Contributor(s): 
//

namespace CustomDataSourceDialog
{
    partial class IDPOpenDialog
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(IDPOpenDialog));
            this.cancelButton = new System.Windows.Forms.Button();
            this.openButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.sourceTypeComboBox = new System.Windows.Forms.ComboBox();
            this.FolderListIcons = new System.Windows.Forms.ImageList(this.components);
            this.SearchBox = new System.Windows.Forms.TextBox();
            this.FileTree = new System.Windows.Forms.TreeView();
            this.BreadCrumbPanel = new System.Windows.Forms.Panel();
            this.MenuPanel = new System.Windows.Forms.Panel();
            this.SubfolderBox = new System.Windows.Forms.CheckBox();
            this.VertSplit = new System.Windows.Forms.SplitContainer();
            this.RemoveNodeButton = new System.Windows.Forms.Button();
            this.AddNode = new System.Windows.Forms.Button();
            this.FileTreeView = new System.Windows.Forms.TreeView();
            this.TopSplit = new System.Windows.Forms.SplitContainer();
            this.ArrowPicture = new System.Windows.Forms.PictureBox();
            this.ForwardPicture = new System.Windows.Forms.PictureBox();
            this.BackPicture = new System.Windows.Forms.PictureBox();
            this.progressPanel = new System.Windows.Forms.Panel();
            this.importProgressBar = new System.Windows.Forms.ProgressBar();
            this.importProgressCancelButton = new System.Windows.Forms.Button();
            this.MenuPanel.SuspendLayout();
            this.VertSplit.Panel1.SuspendLayout();
            this.VertSplit.Panel2.SuspendLayout();
            this.VertSplit.SuspendLayout();
            this.TopSplit.Panel1.SuspendLayout();
            this.TopSplit.Panel2.SuspendLayout();
            this.TopSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ArrowPicture)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ForwardPicture)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.BackPicture)).BeginInit();
            this.progressPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // cancelButton
            // 
            this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cancelButton.Location = new System.Drawing.Point(674, 475);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(66, 21);
            this.cancelButton.TabIndex = 30;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            // 
            // openButton
            // 
            this.openButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.openButton.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.openButton.Location = new System.Drawing.Point(602, 476);
            this.openButton.Name = "openButton";
            this.openButton.Size = new System.Drawing.Size(66, 20);
            this.openButton.TabIndex = 29;
            this.openButton.Text = "&Open";
            this.openButton.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 479);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(84, 13);
            this.label1.TabIndex = 28;
            this.label1.Text = "Sources of &type:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // sourceTypeComboBox
            // 
            this.sourceTypeComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.sourceTypeComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.sourceTypeComboBox.FormattingEnabled = true;
            this.sourceTypeComboBox.Location = new System.Drawing.Point(102, 475);
            this.sourceTypeComboBox.Name = "sourceTypeComboBox";
            this.sourceTypeComboBox.Size = new System.Drawing.Size(494, 21);
            this.sourceTypeComboBox.TabIndex = 27;
            this.sourceTypeComboBox.SelectedIndexChanged += new System.EventHandler(this.sourceTypeComboBox_SelectedIndexChanged);
            // 
            // FolderListIcons
            // 
            this.FolderListIcons.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("FolderListIcons.ImageStream")));
            this.FolderListIcons.TransparentColor = System.Drawing.Color.Transparent;
            this.FolderListIcons.Images.SetKeyName(0, "RecentDocuments.png");
            this.FolderListIcons.Images.SetKeyName(1, "Desktop.png");
            this.FolderListIcons.Images.SetKeyName(2, "MyDocuments.png");
            this.FolderListIcons.Images.SetKeyName(3, "MyComputer.png");
            this.FolderListIcons.Images.SetKeyName(4, "MyNetworkPlaces.png");
            this.FolderListIcons.Images.SetKeyName(5, "LocalDrive.png");
            this.FolderListIcons.Images.SetKeyName(6, "OpticalDrive.png");
            this.FolderListIcons.Images.SetKeyName(7, "NetworkDrive.png");
            this.FolderListIcons.Images.SetKeyName(8, "folder.png");
            this.FolderListIcons.Images.SetKeyName(9, "DataProcessingFolder.png");
            this.FolderListIcons.Images.SetKeyName(10, "DataProcessing.png");
            // 
            // SearchBox
            // 
            this.SearchBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.SearchBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SearchBox.ForeColor = System.Drawing.SystemColors.GrayText;
            this.SearchBox.Location = new System.Drawing.Point(0, 0);
            this.SearchBox.Name = "SearchBox";
            this.SearchBox.Size = new System.Drawing.Size(173, 21);
            this.SearchBox.TabIndex = 31;
            this.SearchBox.Text = "Search";
            this.SearchBox.TextChanged += new System.EventHandler(this.SearchBox_TextChanged);
            this.SearchBox.Leave += new System.EventHandler(this.SearchBox_Leave);
            this.SearchBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.SearchBox_KeyPress);
            this.SearchBox.Enter += new System.EventHandler(this.SearchBox_Enter);
            // 
            // FileTree
            // 
            this.FileTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FileTree.HideSelection = false;
            this.FileTree.ImageIndex = 0;
            this.FileTree.ImageList = this.FolderListIcons;
            this.FileTree.Location = new System.Drawing.Point(0, 0);
            this.FileTree.Name = "FileTree";
            this.FileTree.SelectedImageIndex = 0;
            this.FileTree.Size = new System.Drawing.Size(275, 404);
            this.FileTree.TabIndex = 33;
            this.FileTree.AfterCollapse += new System.Windows.Forms.TreeViewEventHandler(this.FileTree_AfterCollapse);
            this.FileTree.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler(this.FileTree_BeforeExpand);
            this.FileTree.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.FileTree_KeyPress);
            // 
            // BreadCrumbPanel
            // 
            this.BreadCrumbPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.BreadCrumbPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.BreadCrumbPanel.Location = new System.Drawing.Point(0, 0);
            this.BreadCrumbPanel.Name = "BreadCrumbPanel";
            this.BreadCrumbPanel.Size = new System.Drawing.Size(481, 23);
            this.BreadCrumbPanel.TabIndex = 34;
            this.BreadCrumbPanel.Resize += new System.EventHandler(this.BreadCrumbPanel_Resize);
            // 
            // MenuPanel
            // 
            this.MenuPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.MenuPanel.BackColor = System.Drawing.SystemColors.ControlDark;
            this.MenuPanel.Controls.Add(this.SubfolderBox);
            this.MenuPanel.Location = new System.Drawing.Point(10, 39);
            this.MenuPanel.Name = "MenuPanel";
            this.MenuPanel.Size = new System.Drawing.Size(730, 27);
            this.MenuPanel.TabIndex = 42;
            // 
            // SubfolderBox
            // 
            this.SubfolderBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.SubfolderBox.AutoSize = true;
            this.SubfolderBox.Location = new System.Drawing.Point(614, 7);
            this.SubfolderBox.Name = "SubfolderBox";
            this.SubfolderBox.Size = new System.Drawing.Size(113, 17);
            this.SubfolderBox.TabIndex = 0;
            this.SubfolderBox.Text = "Search Subfolders";
            this.SubfolderBox.UseVisualStyleBackColor = true;
            // 
            // VertSplit
            // 
            this.VertSplit.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.VertSplit.Location = new System.Drawing.Point(10, 65);
            this.VertSplit.Name = "VertSplit";
            // 
            // VertSplit.Panel1
            // 
            this.VertSplit.Panel1.Controls.Add(this.FileTree);
            // 
            // VertSplit.Panel2
            // 
            this.VertSplit.Panel2.Controls.Add(this.progressPanel);
            this.VertSplit.Panel2.Controls.Add(this.RemoveNodeButton);
            this.VertSplit.Panel2.Controls.Add(this.AddNode);
            this.VertSplit.Panel2.Controls.Add(this.FileTreeView);
            this.VertSplit.Size = new System.Drawing.Size(730, 404);
            this.VertSplit.SplitterDistance = 275;
            this.VertSplit.TabIndex = 43;
            this.VertSplit.TabStop = false;
            // 
            // RemoveNodeButton
            // 
            this.RemoveNodeButton.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.RemoveNodeButton.Location = new System.Drawing.Point(6, 205);
            this.RemoveNodeButton.Name = "RemoveNodeButton";
            this.RemoveNodeButton.Size = new System.Drawing.Size(30, 23);
            this.RemoveNodeButton.TabIndex = 2;
            this.RemoveNodeButton.Text = "<";
            this.RemoveNodeButton.UseVisualStyleBackColor = true;
            this.RemoveNodeButton.Click += new System.EventHandler(this.RemoveNode_Click);
            // 
            // AddNode
            // 
            this.AddNode.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.AddNode.Location = new System.Drawing.Point(6, 176);
            this.AddNode.Name = "AddNode";
            this.AddNode.Size = new System.Drawing.Size(30, 23);
            this.AddNode.TabIndex = 1;
            this.AddNode.Text = ">";
            this.AddNode.UseVisualStyleBackColor = true;
            this.AddNode.Click += new System.EventHandler(this.AddNode_Click);
            // 
            // FileTreeView
            // 
            this.FileTreeView.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.FileTreeView.CheckBoxes = true;
            this.FileTreeView.ImageIndex = 8;
            this.FileTreeView.ImageList = this.FolderListIcons;
            this.FileTreeView.Location = new System.Drawing.Point(42, 0);
            this.FileTreeView.Name = "FileTreeView";
            this.FileTreeView.SelectedImageIndex = 8;
            this.FileTreeView.Size = new System.Drawing.Size(409, 404);
            this.FileTreeView.TabIndex = 0;
            this.FileTreeView.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.FileTreeView_AfterCheck);
            this.FileTreeView.BeforeCheck += new System.Windows.Forms.TreeViewCancelEventHandler(this.FileTreeView_BeforeCheck);
            // 
            // TopSplit
            // 
            this.TopSplit.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.TopSplit.Location = new System.Drawing.Point(82, 9);
            this.TopSplit.Name = "TopSplit";
            // 
            // TopSplit.Panel1
            // 
            this.TopSplit.Panel1.Controls.Add(this.BreadCrumbPanel);
            // 
            // TopSplit.Panel2
            // 
            this.TopSplit.Panel2.Controls.Add(this.SearchBox);
            this.TopSplit.Size = new System.Drawing.Size(658, 23);
            this.TopSplit.SplitterDistance = 481;
            this.TopSplit.TabIndex = 45;
            // 
            // ArrowPicture
            // 
            this.ArrowPicture.ErrorImage = null;
            this.ArrowPicture.Image = ((System.Drawing.Image)(resources.GetObject("ArrowPicture.Image")));
            this.ArrowPicture.InitialImage = null;
            this.ArrowPicture.Location = new System.Drawing.Point(68, 6);
            this.ArrowPicture.Name = "ArrowPicture";
            this.ArrowPicture.Size = new System.Drawing.Size(11, 30);
            this.ArrowPicture.TabIndex = 39;
            this.ArrowPicture.TabStop = false;
            this.ArrowPicture.MouseLeave += new System.EventHandler(this.ArrowPicture_MouseLeave);
            this.ArrowPicture.Click += new System.EventHandler(this.ArrowPicture_Click);
            this.ArrowPicture.MouseEnter += new System.EventHandler(this.ArrowPicture_MouseEnter);
            // 
            // ForwardPicture
            // 
            this.ForwardPicture.ErrorImage = null;
            this.ForwardPicture.Image = ((System.Drawing.Image)(resources.GetObject("ForwardPicture.Image")));
            this.ForwardPicture.InitialImage = null;
            this.ForwardPicture.Location = new System.Drawing.Point(40, 6);
            this.ForwardPicture.Name = "ForwardPicture";
            this.ForwardPicture.Size = new System.Drawing.Size(28, 30);
            this.ForwardPicture.TabIndex = 38;
            this.ForwardPicture.TabStop = false;
            this.ForwardPicture.Tag = "Faded";
            this.ForwardPicture.MouseLeave += new System.EventHandler(this.ForwardPicture_MouseLeave);
            this.ForwardPicture.Click += new System.EventHandler(this.ForwardPicture_Click);
            this.ForwardPicture.MouseEnter += new System.EventHandler(this.ForwardPicture_MouseEnter);
            // 
            // BackPicture
            // 
            this.BackPicture.ErrorImage = null;
            this.BackPicture.Image = ((System.Drawing.Image)(resources.GetObject("BackPicture.Image")));
            this.BackPicture.InitialImage = null;
            this.BackPicture.Location = new System.Drawing.Point(10, 6);
            this.BackPicture.Name = "BackPicture";
            this.BackPicture.Size = new System.Drawing.Size(30, 30);
            this.BackPicture.TabIndex = 37;
            this.BackPicture.TabStop = false;
            this.BackPicture.Tag = "Faded";
            this.BackPicture.MouseLeave += new System.EventHandler(this.BackPicture_MouseLeave);
            this.BackPicture.Click += new System.EventHandler(this.BackPicture_Click);
            this.BackPicture.MouseEnter += new System.EventHandler(this.BackPicture_MouseEnter);
            // 
            // progressPanel
            // 
            this.progressPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.progressPanel.Controls.Add(this.importProgressCancelButton);
            this.progressPanel.Controls.Add(this.importProgressBar);
            this.progressPanel.Location = new System.Drawing.Point(42, 375);
            this.progressPanel.Name = "progressPanel";
            this.progressPanel.Size = new System.Drawing.Size(409, 28);
            this.progressPanel.TabIndex = 3;
            // 
            // importProgressBar
            // 
            this.importProgressBar.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.importProgressBar.Location = new System.Drawing.Point(3, 4);
            this.importProgressBar.Name = "importProgressBar";
            this.importProgressBar.Size = new System.Drawing.Size(367, 21);
            this.importProgressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.importProgressBar.TabIndex = 0;
            // 
            // importProgressCancelButton
            // 
            this.importProgressCancelButton.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.importProgressCancelButton.Location = new System.Drawing.Point(376, 3);
            this.importProgressCancelButton.Name = "importProgressCancelButton";
            this.importProgressCancelButton.Size = new System.Drawing.Size(30, 23);
            this.importProgressCancelButton.TabIndex = 2;
            this.importProgressCancelButton.Text = "Ø";
            this.importProgressCancelButton.UseVisualStyleBackColor = true;
            this.importProgressCancelButton.Click += new System.EventHandler(this.importProgressCancelButton_Click);
            // 
            // IDPOpenDialog
            // 
            this.AcceptButton = this.openButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cancelButton;
            this.ClientSize = new System.Drawing.Size(752, 516);
            this.Controls.Add(this.MenuPanel);
            this.Controls.Add(this.ArrowPicture);
            this.Controls.Add(this.ForwardPicture);
            this.Controls.Add(this.BackPicture);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.openButton);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.sourceTypeComboBox);
            this.Controls.Add(this.TopSplit);
            this.Controls.Add(this.VertSplit);
            this.MinimumSize = new System.Drawing.Size(500, 300);
            this.Name = "IDPOpenDialog";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Open Data Source";
            this.Load += new System.EventHandler(this.IDPOpenDialog_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.OpenDataSourceDialogue_FormClosing);
            this.MenuPanel.ResumeLayout(false);
            this.MenuPanel.PerformLayout();
            this.VertSplit.Panel1.ResumeLayout(false);
            this.VertSplit.Panel2.ResumeLayout(false);
            this.VertSplit.ResumeLayout(false);
            this.TopSplit.Panel1.ResumeLayout(false);
            this.TopSplit.Panel2.ResumeLayout(false);
            this.TopSplit.Panel2.PerformLayout();
            this.TopSplit.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.ArrowPicture)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ForwardPicture)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.BackPicture)).EndInit();
            this.progressPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button openButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox sourceTypeComboBox;
        private System.Windows.Forms.TextBox SearchBox;
        private System.Windows.Forms.TreeView FileTree;
        private System.Windows.Forms.Panel BreadCrumbPanel;
        private System.Windows.Forms.PictureBox BackPicture;
        private System.Windows.Forms.PictureBox ArrowPicture;
        private System.Windows.Forms.PictureBox ForwardPicture;
        private System.Windows.Forms.ImageList FolderListIcons;
        private System.Windows.Forms.Panel MenuPanel;
        private System.Windows.Forms.SplitContainer VertSplit;
        private System.Windows.Forms.SplitContainer TopSplit;
        private System.Windows.Forms.Button RemoveNodeButton;
        private System.Windows.Forms.Button AddNode;
        private System.Windows.Forms.TreeView FileTreeView;
        private System.Windows.Forms.CheckBox SubfolderBox;
        private System.Windows.Forms.Panel progressPanel;
        private System.Windows.Forms.Button importProgressCancelButton;
        private System.Windows.Forms.ProgressBar importProgressBar;
    }
}