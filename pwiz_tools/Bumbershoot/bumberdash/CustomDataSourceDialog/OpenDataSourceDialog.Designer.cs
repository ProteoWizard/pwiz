//
// $Id: OpenDataSourceDialog.Designer.cs 55 2011-04-28 15:57:33Z chambm $
//
//
// Original author: Jay Holman <jay.holman .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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

namespace CustomDataSourceDialog
{
    partial class OpenDataSourceDialog
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OpenDataSourceDialog));
            this.cancelButton = new System.Windows.Forms.Button();
            this.openButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.sourceTypeComboBox = new System.Windows.Forms.ComboBox();
            this.labelSourcePath = new System.Windows.Forms.Label();
            this.sourcePathTextBox = new System.Windows.Forms.TextBox();
            this.FolderListIcons = new System.Windows.Forms.ImageList(this.components);
            this.SearchBox = new System.Windows.Forms.TextBox();
            this.FileTree = new System.Windows.Forms.TreeView();
            this.BreadCrumbPanel = new System.Windows.Forms.Panel();
            this.MenuPanel = new System.Windows.Forms.Panel();
            this.ViewControl = new System.Windows.Forms.PictureBox();
            this.EffectPanel = new System.Windows.Forms.Panel();
            this.FolderViewList = new System.Windows.Forms.ListView();
            this.SourceName = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SourceSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SourceType = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.DateModified = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.VertSplit = new System.Windows.Forms.SplitContainer();
            this.TopSplit = new System.Windows.Forms.SplitContainer();
            this.SearchButton = new System.Windows.Forms.PictureBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.ArrowPicture = new System.Windows.Forms.PictureBox();
            this.ForwardPicture = new System.Windows.Forms.PictureBox();
            this.BackPicture = new System.Windows.Forms.PictureBox();
            this.MenuPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ViewControl)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.VertSplit)).BeginInit();
            this.VertSplit.Panel1.SuspendLayout();
            this.VertSplit.Panel2.SuspendLayout();
            this.VertSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.TopSplit)).BeginInit();
            this.TopSplit.Panel1.SuspendLayout();
            this.TopSplit.Panel2.SuspendLayout();
            this.TopSplit.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.SearchButton)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ArrowPicture)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ForwardPicture)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.BackPicture)).BeginInit();
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
            this.openButton.Location = new System.Drawing.Point(674, 449);
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
            this.sourceTypeComboBox.Size = new System.Drawing.Size(568, 21);
            this.sourceTypeComboBox.TabIndex = 27;
            this.sourceTypeComboBox.SelectedIndexChanged += new System.EventHandler(this.sourceTypeComboBox_SelectedIndexChanged);
            // 
            // labelSourcePath
            // 
            this.labelSourcePath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelSourcePath.AutoSize = true;
            this.labelSourcePath.Location = new System.Drawing.Point(12, 453);
            this.labelSourcePath.Name = "labelSourcePath";
            this.labelSourcePath.Size = new System.Drawing.Size(73, 13);
            this.labelSourcePath.TabIndex = 26;
            this.labelSourcePath.Text = "Source &name:";
            this.labelSourcePath.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // sourcePathTextBox
            // 
            this.sourcePathTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sourcePathTextBox.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.Suggest;
            this.sourcePathTextBox.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.FileSystem;
            this.sourcePathTextBox.Location = new System.Drawing.Point(102, 449);
            this.sourcePathTextBox.Name = "sourcePathTextBox";
            this.sourcePathTextBox.Size = new System.Drawing.Size(568, 20);
            this.sourcePathTextBox.TabIndex = 25;
            this.sourcePathTextBox.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.sourcePathTextBox_PreviewKeyDown);
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
            this.SearchBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.SearchBox.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.SearchBox.ForeColor = System.Drawing.SystemColors.GrayText;
            this.SearchBox.Location = new System.Drawing.Point(0, 0);
            this.SearchBox.Name = "SearchBox";
            this.SearchBox.Size = new System.Drawing.Size(154, 21);
            this.SearchBox.TabIndex = 31;
            this.SearchBox.Text = "Search";
            this.SearchBox.TextChanged += new System.EventHandler(this.SearchBox_TextChanged);
            this.SearchBox.Enter += new System.EventHandler(this.SearchBox_Enter);
            this.SearchBox.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.SearchBox_KeyPress);
            this.SearchBox.Leave += new System.EventHandler(this.SearchBox_Leave);
            // 
            // FileTree
            // 
            this.FileTree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FileTree.ImageIndex = 0;
            this.FileTree.ImageList = this.FolderListIcons;
            this.FileTree.LabelEdit = true;
            this.FileTree.Location = new System.Drawing.Point(0, 0);
            this.FileTree.Name = "FileTree";
            this.FileTree.SelectedImageIndex = 0;
            this.FileTree.Size = new System.Drawing.Size(275, 378);
            this.FileTree.TabIndex = 33;
            this.FileTree.AfterCollapse += new System.Windows.Forms.TreeViewEventHandler(this.FileTree_AfterCollapse);
            this.FileTree.BeforeExpand += new System.Windows.Forms.TreeViewCancelEventHandler(this.FileTree_BeforeExpand);
            this.FileTree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.FileTree_AfterSelect);
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
            this.MenuPanel.Controls.Add(this.ViewControl);
            this.MenuPanel.Controls.Add(this.EffectPanel);
            this.MenuPanel.Location = new System.Drawing.Point(10, 39);
            this.MenuPanel.Name = "MenuPanel";
            this.MenuPanel.Size = new System.Drawing.Size(730, 27);
            this.MenuPanel.TabIndex = 42;
            // 
            // ViewControl
            // 
            this.ViewControl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ViewControl.ErrorImage = null;
            this.ViewControl.Image = ((System.Drawing.Image)(resources.GetObject("ViewControl.Image")));
            this.ViewControl.InitialImage = null;
            this.ViewControl.Location = new System.Drawing.Point(689, 3);
            this.ViewControl.Name = "ViewControl";
            this.ViewControl.Size = new System.Drawing.Size(38, 21);
            this.ViewControl.TabIndex = 42;
            this.ViewControl.TabStop = false;
            this.ViewControl.Tag = "Details";
            this.ViewControl.Click += new System.EventHandler(this.ViewControl_Click);
            this.ViewControl.MouseEnter += new System.EventHandler(this.ViewControl_MouseEnter);
            this.ViewControl.MouseLeave += new System.EventHandler(this.ViewControl_MouseLeave);
            // 
            // EffectPanel
            // 
            this.EffectPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.EffectPanel.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.EffectPanel.Location = new System.Drawing.Point(687, 1);
            this.EffectPanel.Name = "EffectPanel";
            this.EffectPanel.Size = new System.Drawing.Size(41, 24);
            this.EffectPanel.TabIndex = 43;
            this.EffectPanel.Visible = false;
            // 
            // FolderViewList
            // 
            this.FolderViewList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.SourceName,
            this.SourceSize,
            this.SourceType,
            this.DateModified});
            this.FolderViewList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FolderViewList.LargeImageList = this.FolderListIcons;
            this.FolderViewList.Location = new System.Drawing.Point(0, 0);
            this.FolderViewList.Name = "FolderViewList";
            this.FolderViewList.Size = new System.Drawing.Size(451, 378);
            this.FolderViewList.SmallImageList = this.FolderListIcons;
            this.FolderViewList.TabIndex = 24;
            this.FolderViewList.UseCompatibleStateImageBehavior = false;
            this.FolderViewList.View = System.Windows.Forms.View.Details;
            this.FolderViewList.SelectedIndexChanged += new System.EventHandler(this.FolderViewList_SelectedIndexChanged);
            this.FolderViewList.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FolderViewList_KeyDown);
            this.FolderViewList.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.FolderViewList_MouseDoubleClick);
            // 
            // SourceName
            // 
            this.SourceName.Text = "Name";
            this.SourceName.Width = 200;
            // 
            // SourceSize
            // 
            this.SourceSize.Text = "Size";
            // 
            // SourceType
            // 
            this.SourceType.Text = "Type";
            this.SourceType.Width = 80;
            // 
            // DateModified
            // 
            this.DateModified.Text = "Date Modified";
            this.DateModified.Width = 140;
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
            this.VertSplit.Panel2.Controls.Add(this.FolderViewList);
            this.VertSplit.Size = new System.Drawing.Size(730, 378);
            this.VertSplit.SplitterDistance = 275;
            this.VertSplit.TabIndex = 43;
            this.VertSplit.TabStop = false;
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
            this.TopSplit.Panel2.Controls.Add(this.SearchButton);
            this.TopSplit.Panel2.Controls.Add(this.SearchBox);
            this.TopSplit.Panel2.Controls.Add(this.textBox1);
            this.TopSplit.Size = new System.Drawing.Size(658, 23);
            this.TopSplit.SplitterDistance = 481;
            this.TopSplit.TabIndex = 45;
            // 
            // SearchButton
            // 
            this.SearchButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.SearchButton.Image = global::CustomDataSourceDialog.Properties.Resources.SearchButton;
            this.SearchButton.Location = new System.Drawing.Point(150, 1);
            this.SearchButton.Name = "SearchButton";
            this.SearchButton.Size = new System.Drawing.Size(20, 19);
            this.SearchButton.TabIndex = 32;
            this.SearchButton.TabStop = false;
            this.SearchButton.Click += new System.EventHandler(this.SearchButton_Click);
            this.SearchButton.MouseEnter += new System.EventHandler(this.SearchButton_MouseEnter);
            this.SearchButton.MouseLeave += new System.EventHandler(this.SearchButton_MouseLeave);
            // 
            // textBox1
            // 
            this.textBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox1.ForeColor = System.Drawing.SystemColors.GrayText;
            this.textBox1.Location = new System.Drawing.Point(150, 0);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(23, 21);
            this.textBox1.TabIndex = 33;
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
            this.ArrowPicture.Click += new System.EventHandler(this.ArrowPicture_Click);
            this.ArrowPicture.MouseEnter += new System.EventHandler(this.ArrowPicture_MouseEnter);
            this.ArrowPicture.MouseLeave += new System.EventHandler(this.ArrowPicture_MouseLeave);
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
            this.ForwardPicture.Click += new System.EventHandler(this.ForwardPicture_Click);
            this.ForwardPicture.MouseEnter += new System.EventHandler(this.ForwardPicture_MouseEnter);
            this.ForwardPicture.MouseLeave += new System.EventHandler(this.ForwardPicture_MouseLeave);
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
            this.BackPicture.Click += new System.EventHandler(this.BackPicture_Click);
            this.BackPicture.MouseEnter += new System.EventHandler(this.BackPicture_MouseEnter);
            this.BackPicture.MouseLeave += new System.EventHandler(this.BackPicture_MouseLeave);
            // 
            // OpenDataSourceDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(752, 516);
            this.Controls.Add(this.MenuPanel);
            this.Controls.Add(this.ArrowPicture);
            this.Controls.Add(this.ForwardPicture);
            this.Controls.Add(this.BackPicture);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.openButton);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.sourceTypeComboBox);
            this.Controls.Add(this.labelSourcePath);
            this.Controls.Add(this.sourcePathTextBox);
            this.Controls.Add(this.TopSplit);
            this.Controls.Add(this.VertSplit);
            this.MinimumSize = new System.Drawing.Size(500, 300);
            this.Name = "OpenDataSourceDialog";
            this.Text = "Open Data Source";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.OpenDataSourceDialogue_FormClosing);
            this.Load += new System.EventHandler(this.OpenDataSourceDialogue_Load);
            this.MenuPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.ViewControl)).EndInit();
            this.VertSplit.Panel1.ResumeLayout(false);
            this.VertSplit.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.VertSplit)).EndInit();
            this.VertSplit.ResumeLayout(false);
            this.TopSplit.Panel1.ResumeLayout(false);
            this.TopSplit.Panel2.ResumeLayout(false);
            this.TopSplit.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.TopSplit)).EndInit();
            this.TopSplit.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.SearchButton)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ArrowPicture)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ForwardPicture)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.BackPicture)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.Button openButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox sourceTypeComboBox;
        private System.Windows.Forms.Label labelSourcePath;
        private System.Windows.Forms.TextBox sourcePathTextBox;
        private System.Windows.Forms.TextBox SearchBox;
        private System.Windows.Forms.TreeView FileTree;
        private System.Windows.Forms.Panel BreadCrumbPanel;
        private System.Windows.Forms.PictureBox BackPicture;
        private System.Windows.Forms.PictureBox ArrowPicture;
        private System.Windows.Forms.PictureBox ForwardPicture;
        private System.Windows.Forms.ImageList FolderListIcons;
        private System.Windows.Forms.Panel MenuPanel;
        private System.Windows.Forms.ListView FolderViewList;
        private System.Windows.Forms.ColumnHeader SourceName;
        private System.Windows.Forms.ColumnHeader SourceSize;
        private System.Windows.Forms.ColumnHeader SourceType;
        private System.Windows.Forms.ColumnHeader DateModified;
        private System.Windows.Forms.SplitContainer VertSplit;
        private System.Windows.Forms.SplitContainer TopSplit;
        private System.Windows.Forms.PictureBox ViewControl;
        private System.Windows.Forms.Panel EffectPanel;
        private System.Windows.Forms.PictureBox SearchButton;
        private System.Windows.Forms.TextBox textBox1;
    }
}